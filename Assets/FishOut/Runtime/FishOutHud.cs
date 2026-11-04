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
        public Text level, remaining, lives, notice, instructions;
        public Button hint, rescue;
        public Font font;
        private FishOutGame game;
        private Text homeLevel;
        private Image flash;
        private float flashTime, noticeTime;
        private Rect lastSafe;
        private static Sprite rounded;
        public static readonly Color Ink = new Color(.06f,.22f,.29f);
        public static readonly Color Teal = new Color(.05f,.64f,.65f);

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
            homeLevel = Label(home.transform,"LEVEL 1",new Vector2(.5f,.21f),new Vector2(800,60),32,Color.white);
            Button(home.transform,"PLAY",new Vector2(.5f,.13f),new Vector2(660,125),Teal,()=>game.StartLevel(game.CurrentLevel));
            Button(home.transform,"LEVEL PREVIEWS",new Vector2(.5f,.045f),new Vector2(660,85),new Color(.10f,.36f,.43f),()=>game.OpenShowcaseLevels(),30);
            gameplay = Rect("Gameplay",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject;
            level = Label(gameplay.transform,"LEVEL 1",new Vector2(.24f,.945f),new Vector2(450,80),48,Color.white);
            Button(gameplay.transform,"SETTINGS",new Vector2(.72f,.945f),new Vector2(200,72),new Color(.10f,.36f,.43f),()=>game.OpenSettings(),24);
            Button(gameplay.transform,"RETRY",new Vector2(.92f,.945f),new Vector2(155,72),new Color(.10f,.36f,.43f),()=>game.ConfirmRetry(),24);
            remaining = Label(gameplay.transform,"3 FISH LEFT",new Vector2(.26f,.884f),new Vector2(450,65),32,new Color(.72f,.97f,.95f));
            lives = Label(gameplay.transform,"3 / 3 LIVES",new Vector2(.76f,.884f),new Vector2(410,65),32,new Color(1f,.78f,.63f));
            instructions = Label(gameplay.transform,"",new Vector2(.5f,.795f),new Vector2(970,125),31,Color.white);
            hint = Button(gameplay.transform,"HINT  ·  FREE",new Vector2(.27f,.09f),new Vector2(430,115),new Color(.97f,.76f,.32f),()=>game.UseHint(),33);
            rescue = Button(gameplay.transform,"RESCUE  ·  FREE",new Vector2(.73f,.09f),new Vector2(430,115),Teal,()=>game.BeginRescue(),33);
            notice = Label(safe,"",new Vector2(.5f,.18f),new Vector2(980,100),29,Color.white);
            flash = Rect("Hazard flash",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
            flash.color = Color.clear; flash.raycastTarget = false;
            UpdateSafeArea();
        }
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
            return t;
        }
        public Button Button(Transform parent,string text,Vector2 anchor,Vector2 size,Color color,UnityAction action,int fontSize=42)
        {
            var rt=Rect(text,parent,anchor,anchor,Vector2.zero,size);
            var im=rt.gameObject.AddComponent<Image>(); im.sprite=RoundSprite(); im.type=Image.Type.Sliced; im.color=color;
            var b=rt.gameObject.AddComponent<Button>(); b.targetGraphic=im;
            var colors=b.colors; colors.highlightedColor=new Color(.9f,1f,1f); colors.pressedColor=new Color(.68f,.84f,.87f); colors.disabledColor=new Color(.4f,.5f,.53f,.6f); b.colors=colors;
            b.onClick.AddListener(()=>{game.ConsumeUiInput();action();});
            var label=Label(rt,text,new Vector2(.5f,.5f),size-new Vector2(16,8),fontSize,color==new Color(.97f,.76f,.32f)?Ink:Color.white);
            return b;
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
            level.text="LEVEL "+number; remaining.text=board.Fish.Count+" FISH LEFT";
            lives.text=board.Lives+" / 3 LIVES";
            hint.interactable=!board.HintUsed&&!selecting&&!game.IsBusy;
            rescue.interactable=selecting||(!board.RescueUsed&&!game.IsBusy);
            hint.GetComponentInChildren<Text>().text=board.HintUsed?"HINT  ·  USED":"HINT  ·  FREE";
            rescue.GetComponentInChildren<Text>().text=selecting?"CANCEL RESCUE":board.RescueUsed?"RESCUE  ·  USED":"RESCUE  ·  FREE";
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
        public RectTransform ShowModal(string title,string message)
        {
            CloseModal();
            var overlay=Rect("Modal",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero); modal=overlay.gameObject;
            var dim=overlay.gameObject.AddComponent<Image>(); dim.color=new Color(.015f,.09f,.14f,.84f);
            var panel=Rect("Dialog",overlay,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(920,800));
            var card=panel.gameObject.AddComponent<Image>(); card.sprite=RoundSprite(); card.type=Image.Type.Sliced; card.color=new Color(.93f,.98f,.97f);
            Label(panel,title,new Vector2(.5f,.85f),new Vector2(850,115),64,Ink);
            Label(panel,message,new Vector2(.5f,.64f),new Vector2(810,210),32,Ink);
            return panel;
        }
        public void Choice(string title,string message,string first,UnityAction yes,string second=null,UnityAction no=null)
        {
            var p=ShowModal(title,message);
            Button(p,first,new Vector2(.5f,.31f),new Vector2(740,100),Teal,yes,36);
            if(second!=null) Button(p,second,new Vector2(.5f,.15f),new Vector2(740,90),new Color(.30f,.46f,.50f),no,30);
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
            if(noticeTime>0) { noticeTime-=Time.unscaledDeltaTime; if(noticeTime<=0)notice.text=""; }
            flashTime=Mathf.Max(0,flashTime-Time.unscaledDeltaTime);
            if(flash) flash.color=new Color(1,.08f,.08f,flashTime*.42f);
        }
    }
}
