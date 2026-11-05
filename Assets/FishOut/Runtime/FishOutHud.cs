using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FishOut
{
    public sealed class FishOutHud : MonoBehaviour
    {
        public RectTransform safe;
        public GameObject home, gameplay, modal;
        public Text level, remaining, notice, instructions;
        private Text hintBadge, rescueBadge, rescueCaption;
        private Image[] hearts;
        private Image instructionPlate,fade;
        private float fadeLeft;
        private const float FadeLength=.25f;
        public Button hint, rescue;
        public Font font;
        private FishOutGame game;
        private Text homeLevel;
        private Image flash;
        private float flashTime, noticeTime;
        private Rect lastSafe;
        private static Sprite rounded;
        public static readonly Color Ink = new Color(.06f,.22f,.29f);
        public static readonly Color Teal = new Color(.20f,.70f,.32f);
        private static readonly Color IconPlate = new Color(.42f,.40f,.92f);
        private static readonly Color PadPlate = new Color(.33f,.78f,.30f);
        private static readonly Color Pill = new Color(.04f,.24f,.34f,.72f);
        private static readonly Color Spent = new Color(.25f,.38f,.44f,.75f);

        public void Build(FishOutGame owner, Font uiFont)
        {
            game = owner; font = uiFont;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080,1920);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            safe = Rect("Safe area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            home = Rect("Home", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Label(home.transform,"FISH OUT",new Vector2(.5f,.88f),new Vector2(950,140),100,Color.white);
            homeLevel = Label(home.transform,"LEVEL 1",new Vector2(.5f,.255f),new Vector2(800,60),32,Color.white);
            Button(home.transform,"PLAY",new Vector2(.5f,.18f),new Vector2(660,125),Teal,()=>game.StartLevel(game.CurrentLevel));
            Button(home.transform,"LEVELS",new Vector2(.5f,.095f),new Vector2(660,100),new Color(.10f,.36f,.43f),()=>game.OpenLevelSelect(),36);
            gameplay = Rect("Gameplay",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject;
            IconButton(gameplay.transform,"Settings",FishOutIcons.Gear,new Vector2(.09f,.955f),()=>game.OpenSettings());
            IconButton(gameplay.transform,"Retry",FishOutIcons.Retry,new Vector2(.91f,.955f),()=>game.ConfirmRetry());
            Plate(gameplay.transform,"Level plate",new Vector2(.5f,.955f),new Vector2(420,92),Pill);
            level = Label(gameplay.transform,"LEVEL 1",new Vector2(.5f,.955f),new Vector2(400,80),46,Color.white);
            Plate(gameplay.transform,"Status plate",new Vector2(.5f,.9f),new Vector2(420,70),Pill);
            hearts=new Image[3];
            for(int i=0;i<3;i++)
            {
                hearts[i]=Plate(gameplay.transform,"Life "+(i+1),new Vector2(.345f+i*.052f,.9f),new Vector2(50,50),new Color(1f,.3f,.38f));
                hearts[i].sprite=FishOutIcons.Heart;hearts[i].type=Image.Type.Simple;
            }
            remaining = Label(gameplay.transform,"3 FISH",new Vector2(.6f,.9f),new Vector2(220,60),32,Color.white);
            instructionPlate = Plate(gameplay.transform,"Instruction plate",new Vector2(.5f,.178f),new Vector2(980,66),Pill);
            instructions = Label(gameplay.transform,"",new Vector2(.5f,.178f),new Vector2(970,90),31,Color.white);
            hint = Pad(gameplay.transform,"HINT",FishOutIcons.Bulb,new Color(1f,.86f,.3f),new Vector2(.36f,.082f),()=>game.UseHint(),out hintBadge,out _);
            rescue = Pad(gameplay.transform,"RESCUE",FishOutIcons.Buoy,new Color(1f,.36f,.36f),new Vector2(.64f,.082f),()=>game.BeginRescue(),out rescueBadge,out rescueCaption);
            notice = Label(safe,"",new Vector2(.5f,.235f),new Vector2(980,100),29,Color.white);
            flash = Rect("Hazard flash",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
            flash.color = Color.clear; flash.raycastTarget = false;
            fade = Rect("Level fade",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
            fade.color = Color.clear; fade.raycastTarget = false;
            UpdateSafeArea();
        }
        public void FadeIn() { fadeLeft=FadeLength; }
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name,typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent,false);
            rt.anchorMin=min; rt.anchorMax=max; rt.anchoredPosition=position; rt.sizeDelta=size;
            return rt;
        }
        public Text Label(Transform parent,string value,Vector2 anchor,Vector2 size,int point,Color color)
        {
            var rt=Rect(value.Length>30?"Text":value,parent,anchor,anchor,Vector2.zero,size);
            var t=rt.gameObject.AddComponent<Text>(); t.font=font; t.text=value; t.fontSize=point;
            t.color=color; t.alignment=TextAnchor.MiddleCenter; t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Overflow;
            if(color!=Ink){ var outline=rt.gameObject.AddComponent<Outline>(); outline.effectColor=new Color(.04f,.2f,.3f,.85f); outline.effectDistance=new Vector2(2,-2); }
            return t;
        }
        public Button Button(Transform parent,string text,Vector2 anchor,Vector2 size,Color color,UnityAction action,int fontSize=42)
        {
            var rt=Rect(text,parent,anchor,anchor,Vector2.zero,size);
            var b=Clickable(rt,color,action);
            var label=Label(rt,text,new Vector2(.5f,.5f),size-new Vector2(16,8),fontSize,color==new Color(.97f,.76f,.32f)?Ink:Color.white);
            return b;
        }
        private Button Clickable(RectTransform rt,Color color,UnityAction action)
        {
            var im=rt.gameObject.AddComponent<Image>(); im.sprite=RoundSprite(); im.type=Image.Type.Sliced; im.color=color;
            // A hard lower shadow gives the chunky raised look of the benchmark buttons.
            var depth=rt.gameObject.AddComponent<Shadow>(); depth.effectColor=new Color(.02f,.16f,.22f,.6f); depth.effectDistance=new Vector2(0,-9);
            var b=rt.gameObject.AddComponent<Button>(); b.targetGraphic=im;
            var colors=b.colors; colors.highlightedColor=new Color(.9f,1f,1f); colors.pressedColor=new Color(.68f,.84f,.87f); colors.disabledColor=new Color(.55f,.62f,.65f,.8f); b.colors=colors;
            b.onClick.AddListener(()=>{game.ConsumeUiInput();action();});
            return b;
        }
        private Image Plate(Transform parent,string name,Vector2 anchor,Vector2 size,Color color)
        {
            var image=Rect(name,parent,anchor,anchor,Vector2.zero,size).gameObject.AddComponent<Image>();
            image.sprite=RoundSprite(); image.type=Image.Type.Sliced; image.color=color; image.raycastTarget=false;
            return image;
        }
        private Image Icon(Transform parent,string name,Sprite sprite,Color color,Vector2 anchor,float size)
        {
            var image=Rect(name,parent,anchor,anchor,Vector2.zero,Vector2.one*size).gameObject.AddComponent<Image>();
            image.sprite=sprite; image.color=color; image.raycastTarget=false;
            return image;
        }
        private void IconButton(Transform parent,string name,Sprite sprite,Vector2 anchor,UnityAction action)
        {
            var rt=Rect(name,parent,anchor,anchor,Vector2.zero,new Vector2(116,116));
            Clickable(rt,IconPlate,action);
            Icon(rt,"Icon",sprite,Color.white,new Vector2(.5f,.5f),78);
        }
        private Button Pad(Transform parent,string caption,Sprite sprite,Color tint,Vector2 anchor,UnityAction action,out Text badge,out Text label)
        {
            var rt=Rect(caption,parent,anchor,anchor,Vector2.zero,new Vector2(210,200));
            var button=Clickable(rt,PadPlate,action);
            Icon(rt,"Icon",sprite,tint,new Vector2(.5f,.6f),116);
            label=Label(rt,caption,new Vector2(.5f,.15f),new Vector2(200,50),28,Color.white);
            Plate(rt,"Badge",new Vector2(.93f,.9f),new Vector2(66,66),IconPlate);
            badge=Label(rt,"1",new Vector2(.93f,.9f),new Vector2(60,60),34,Color.white);
            return button;
        }
        private static Sprite RoundSprite()
        {
            if(rounded) return rounded;
            var tex=new Texture2D(64,64,TextureFormat.RGBA32,false); tex.name="FishOut rounded UI"; tex.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                float dx=Mathf.Max(0,Mathf.Abs(x-31.5f)-15.5f),dy=Mathf.Max(0,Mathf.Abs(y-31.5f)-15.5f);
                tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16f-Mathf.Sqrt(dx*dx+dy*dy))));
            }
            tex.Apply(); rounded=Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(18,18,18,18));
            return rounded;
        }
        public void Refresh(BoardState board, int number, bool selecting)
        {
            level.text="LEVEL "+number; remaining.text=board.Fish.Count+" FISH";
            // Only stages with crabs can cost a life.
            bool danger=board.Level.obstacles.Exists(o=>o.kind==ObstacleKind.Crab);
            for(int i=0;i<hearts.Length;i++) { hearts[i].gameObject.SetActive(danger); hearts[i].color=i<board.Lives?new Color(1f,.3f,.38f):Spent; }
            hint.interactable=!board.HintUsed&&!selecting&&!game.IsLocked;
            rescue.interactable=selecting||(!board.RescueUsed&&!game.IsLocked);
            hintBadge.text=board.HintUsed?"0":"1";
            rescueBadge.text=board.RescueUsed?"0":"1";
            rescueCaption.text=selecting?"CANCEL":"RESCUE";
        }
        public void SetHome(bool value,int number)
        {
            CloseModal(); home.SetActive(value); gameplay.SetActive(!value); homeLevel.text="LEVEL "+number;
            notice.text="";
        }
        public void Toast(string value,float seconds=2.5f) { notice.text=value; noticeTime=seconds; }
        public void Hazard() { flashTime=.5f; }
        public void CloseModal()
        {
            if(!modal) return;
            var old=modal; modal=null; old.SetActive(false);
            if(Application.isPlaying) Destroy(old); else DestroyImmediate(old);
        }
        public RectTransform ShowModal(string title,string message){return ShowModal(title,message,Teal,.84f);}
        private RectTransform ShowModal(string title,string message,Color ribbon,float dimming)
        {
            CloseModal();
            var overlay=Rect("Modal",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero); modal=overlay.gameObject;
            var dim=overlay.gameObject.AddComponent<Image>(); dim.color=new Color(.015f,.09f,.14f,dimming);
            var panel=Rect("Dialog",overlay,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(920,800));
            var card=panel.gameObject.AddComponent<Image>(); card.sprite=RoundSprite(); card.type=Image.Type.Sliced; card.color=new Color(.93f,.98f,.97f);
            Plate(panel,"Title ribbon",new Vector2(.5f,.9f),new Vector2(800,130),ribbon);
            Label(panel,title,new Vector2(.5f,.9f),new Vector2(780,115),56,Color.white);
            var body=Label(panel,message,new Vector2(.5f,.64f),new Vector2(810,210),32,Ink);body.name="Message";
            return panel;
        }
        public void Choice(string title,string message,string first,UnityAction yes,string second=null,UnityAction no=null)
        {
            var p=ShowModal(title,message);
            Button(p,first,new Vector2(.5f,.31f),new Vector2(740,100),Teal,yes,36);
            if(second!=null) Button(p,second,new Vector2(.5f,.15f),new Vector2(740,90),new Color(.30f,.46f,.50f),no,30);
        }
        public void Result(string title,string message,Sprite icon,int earned,string first,UnityAction yes,string second=null,UnityAction no=null)
        {
            bool won=icon==FishOutIcons.Star;
            var panel=ShowModal(title,message,won?Teal:new Color(.86f,.24f,.28f),.45f);
            Button(panel,first,new Vector2(.5f,.31f),new Vector2(740,100),Teal,yes,36);
            if(second!=null) Button(panel,second,new Vector2(.5f,.15f),new Vector2(740,90),new Color(.30f,.46f,.50f),no,30);
            var body=(RectTransform)panel.Find("Message");body.anchorMin=body.anchorMax=new Vector2(.5f,.53f);
            var filled=won?new Color(1f,.76f,.16f):new Color(1f,.3f,.38f);
            var icons=new Image[3];
            for(int i=0;i<3;i++)icons[i]=Icon(panel,"Result icon "+(i+1),icon,i<earned?filled:new Color(.74f,.8f,.82f),new Vector2(.5f+(i-1)*.2f,.72f),i==1?170:140);
            StartCoroutine(Reveal(panel,icons));
        }
        // The card settles in, then the icons pop one at a time.
        private System.Collections.IEnumerator Reveal(RectTransform panel,Image[] icons)
        {
            foreach(var icon in icons)icon.transform.localScale=Vector3.zero;
            const float settle=.18f,gap=.12f,pop=.2f;
            for(float t=0;t<settle+gap*icons.Length+pop;t+=Time.unscaledDeltaTime)
            {
                if(!panel)yield break;
                panel.localScale=Vector3.one*Mathf.Lerp(.85f,1,Mathf.Clamp01(t/settle));
                for(int i=0;i<icons.Length;i++)
                {
                    float k=Mathf.Clamp01((t-settle-gap*i)/pop);
                    icons[i].transform.localScale=Vector3.one*(k+Mathf.Sin(k*Mathf.PI)*.25f);
                }
                yield return null;
            }
            if(!panel)yield break;
            panel.localScale=Vector3.one;
            foreach(var icon in icons)icon.transform.localScale=Vector3.one;
        }
        public void LevelSelect(int count,int current,int unlocked)
        {
            var p=ShowModal("SELECT LEVEL","");
            for(int i=0;i<count;i++)
            {
                int number=i+1,row=i/5;int inRow=Mathf.Min(5,count-row*5);
                var anchor=new Vector2(.5f+(i%5-(inRow-1)*.5f)*.175f,.62f-row*.22f);
                var button=Button(p,number.ToString(),anchor,new Vector2(140,130),number==current?new Color(.97f,.76f,.32f):Teal,()=>game.StartLevel(number),48);
                button.interactable=number<=unlocked;
            }
            Button(p,"BACK",new Vector2(.5f,.12f),new Vector2(500,90),new Color(.30f,.46f,.50f),()=>CloseModal(),30);
        }
        public void Settings(bool sound,bool music,bool vibration)
        {
            var p=ShowModal("SETTINGS","");
            Setting(p,"Sound effects",.68f,sound,v=>game.SetSound(v));
            Setting(p,"Background music",.53f,music,v=>game.SetMusic(v));
            Setting(p,"Vibration",.38f,vibration,v=>game.SetVibration(v));
            Button(p,"RESUME",new Vector2(.29f,.16f),new Vector2(360,90),Teal,()=>game.CloseSettings(),30);
            Button(p,"HOME",new Vector2(.71f,.16f),new Vector2(360,90),new Color(.30f,.46f,.50f),()=>game.ShowHome(),30);
        }
        private void Setting(Transform p,string caption,float y,bool value,UnityAction<bool> changed)
        {
            Label(p,caption,new Vector2(.36f,y),new Vector2(530,70),32,Ink);
            var root=Rect(caption+" toggle",p,new Vector2(.82f,y),new Vector2(.82f,y),Vector2.zero,new Vector2(80,70));
            var bg=root.gameObject.AddComponent<Image>(); bg.sprite=RoundSprite(); bg.type=Image.Type.Sliced; bg.color=new Color(.70f,.81f,.82f);
            var check=Rect("On",root,new Vector2(.2f,.2f),new Vector2(.8f,.8f),Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>(); check.color=Teal; check.sprite=RoundSprite();
            var toggle=root.gameObject.AddComponent<Toggle>(); toggle.targetGraphic=bg; toggle.graphic=check; toggle.isOn=value; toggle.onValueChanged.AddListener(changed);
        }
        private void UpdateSafeArea()
        {
            var area=Screen.safeArea; lastSafe=area;
            if(Screen.width<=0||Screen.height<=0) return;
            safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
        }
        private void Update()
        {
            if(Screen.safeArea!=lastSafe) UpdateSafeArea();
            if(fadeLeft>0) { fadeLeft-=Time.unscaledDeltaTime; fade.color=new Color(.02f,.14f,.2f,Mathf.Clamp01(fadeLeft/FadeLength)); }
            if(instructionPlate) instructionPlate.enabled=gameplay.activeSelf&&instructions.text.Length>0;
            if(noticeTime>0) { noticeTime-=Time.unscaledDeltaTime; if(noticeTime<=0)notice.text=""; }
            flashTime=Mathf.Max(0,flashTime-Time.unscaledDeltaTime);
            if(flash) flash.color=new Color(1,.08f,.08f,flashTime*.42f);
        }
    }
}
