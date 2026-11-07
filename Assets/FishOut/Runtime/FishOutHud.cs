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
        private HudSkin skin;
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
            game = owner; font = uiFont; skin = HudSkin.Load();
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
            if(skin&&skin.plate)SkinPlate(home.transform,"Level plate",new Vector2(.5f,.258f),new Vector2(320,78));
            homeLevel = Label(home.transform,"LEVEL 1",new Vector2(.5f,.258f),new Vector2(800,60),34,Color.white);
            Button(home.transform,"PLAY",new Vector2(.5f,.18f),new Vector2(660,125),Teal,()=>game.StartLevel(game.CurrentLevel));
            Button(home.transform,"LEVELS",new Vector2(.5f,.095f),new Vector2(660,100),new Color(.10f,.36f,.43f),()=>game.OpenLevelSelect(),36);
            gameplay = Rect("Gameplay",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject;
            // The status sits top-left so the painted cave (top centre-right), where fish escape, stays in view.
            IconButton(gameplay.transform,"Settings",skin?skin.settingsIcon:FishOutIcons.Gear,new Vector2(.085f,.955f),()=>game.OpenSettings());
            IconButton(gameplay.transform,"Retry",skin?skin.retryIcon:FishOutIcons.Retry,new Vector2(.915f,.955f),()=>game.ConfirmRetry());
            SkinPlate(gameplay.transform,"Level plate",new Vector2(.315f,.955f),new Vector2(320,96));
            level = Label(gameplay.transform,"LEVEL 1",new Vector2(.315f,.958f),new Vector2(300,80),44,Color.white);
            SkinPlate(gameplay.transform,"Status plate",new Vector2(.315f,.898f),new Vector2(320,72));
            hearts=new Image[3];
            for(int i=0;i<3;i++)
            {
                hearts[i]=Plate(gameplay.transform,"Life "+(i+1),new Vector2(.205f+i*.052f,.899f),new Vector2(54,54),skin?Color.white:new Color(1f,.3f,.38f));
                hearts[i].sprite=skin?skin.heart:FishOutIcons.Heart;hearts[i].type=Image.Type.Simple;
            }
            if(skin)Icon(gameplay.transform,"Fish icon",skin.fishIcon,Color.white,new Vector2(.375f,.899f),48);
            remaining = Label(gameplay.transform,"3",new Vector2(.43f,.898f),new Vector2(64,60),36,Color.white);
            instructionPlate = skin?SkinPlate(gameplay.transform,"Instruction plate",new Vector2(.5f,.178f),new Vector2(900,84),skin.bubble):Plate(gameplay.transform,"Instruction plate",new Vector2(.5f,.178f),new Vector2(980,66),Pill);
            instructions = Label(gameplay.transform,"",new Vector2(.5f,.178f),new Vector2(860,90),31,skin?Ink:Color.white);
            hint = Pad(gameplay.transform,"HINT",skin?skin.hintIcon:FishOutIcons.Bulb,skin?Color.white:new Color(1f,.86f,.3f),new Vector2(.36f,.082f),()=>game.UseHint(),out hintBadge,out _,skin?skin.hintButton:null);
            rescue = Pad(gameplay.transform,"RESCUE",skin?skin.rescueIcon:FishOutIcons.Buoy,skin?Color.white:new Color(1f,.36f,.36f),new Vector2(.64f,.082f),()=>game.BeginRescue(),out rescueBadge,out rescueCaption,skin?skin.rescueButton:null);
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
            Sprite sprite=null;
            if(skin)
            {
                bool small=size.x<200;
                if(small)sprite=color==new Color(.97f,.76f,.32f)?skin.currentLevelButton:skin.levelButton;
                else sprite=color==Teal?skin.primaryButton:skin.secondaryButton;
            }
            var b=Clickable(rt,color,action,sprite);
            if(skin&&size.x>=200)
            {
                // Pack buttons hug their label: bigger type, and only as wide as the word needs (never wider than asked).
                fontSize=Mathf.Max(fontSize,40);size=new Vector2(size.x,Mathf.Max(size.y,104));
            }
            var label=Label(rt,text,new Vector2(.5f,.5f),size-new Vector2(16,8),fontSize,color==new Color(.97f,.76f,.32f)?Ink:Color.white);
            if(skin&&size.x>=200)
            {
                float width=Mathf.Clamp(label.preferredWidth+150,300,size.x);
                rt.sizeDelta=new Vector2(width,size.y);label.rectTransform.sizeDelta=new Vector2(width-16,size.y-8);
            }
            return b;
        }
        private Button Clickable(RectTransform rt,Color color,UnityAction action,Sprite sprite=null)
        {
            var im=rt.gameObject.AddComponent<Image>(); im.sprite=sprite?sprite:RoundSprite(); im.type=Image.Type.Sliced; im.color=sprite?Color.white:color;
            // A hard lower shadow gives the chunky raised look of the benchmark buttons (the pack's sprites draw their own).
            if(!sprite){var depth=rt.gameObject.AddComponent<Shadow>(); depth.effectColor=new Color(.02f,.16f,.22f,.6f); depth.effectDistance=new Vector2(0,-9);}
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
            Clickable(rt,IconPlate,action,skin?skin.topButton:null);
            var icon=Icon(rt,"Icon",sprite,Color.white,new Vector2(.5f,skin?.54f:.5f),skin?70:78);
            if(skin)Depth(icon);
        }
        private Button Pad(Transform parent,string caption,Sprite sprite,Color tint,Vector2 anchor,UnityAction action,out Text badge,out Text label,Sprite plate=null)
        {
            var rt=Rect(caption,parent,anchor,anchor,Vector2.zero,new Vector2(210,200));
            var button=Clickable(rt,PadPlate,action,plate);
            var icon=Icon(rt,"Icon",sprite,tint,new Vector2(.5f,.6f),plate?100:116);
            if(plate)Depth(icon);
            label=Label(rt,caption,new Vector2(.5f,.17f),new Vector2(200,50),28,Color.white);
            if(skin&&skin.badge)Icon(rt,"Badge",skin.badge,Color.white,new Vector2(.93f,.9f),74);
            else Plate(rt,"Badge",new Vector2(.93f,.9f),new Vector2(66,66),IconPlate);
            badge=Label(rt,"1",new Vector2(.93f,.91f),new Vector2(60,60),34,Color.white);
            return button;
        }
        // A dark drop under white pictograms, the way the pack draws its icons on buttons.
        private static void Depth(Image icon){var drop=icon.gameObject.AddComponent<Shadow>();drop.effectColor=new Color(0,0,0,.35f);drop.effectDistance=new Vector2(0,-5);}
        // A pack plate when the skin is present, else the drawn translucent pill.
        private Image SkinPlate(Transform parent,string name,Vector2 anchor,Vector2 size,Sprite sprite=null)
        {
            var image=Plate(parent,name,anchor,size,Pill);
            var chosen=sprite?sprite:skin?skin.plate:null;
            if(chosen){image.sprite=chosen;image.type=Image.Type.Sliced;image.color=Color.white;}
            return image;
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
            level.text="LEVEL "+number; remaining.text=skin?board.Fish.Count.ToString():board.Fish.Count+" FISH";
            // Spec §6: the top bar always shows the 3 lives, even on stages without crabs.
            for(int i=0;i<hearts.Length;i++) { hearts[i].gameObject.SetActive(true); if(skin){hearts[i].sprite=i<board.Lives?skin.heart:skin.heartSpent;hearts[i].color=Color.white;}else hearts[i].color=i<board.Lives?new Color(1f,.3f,.38f):Spent; }
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
            if(skin&&skin.popup)
            {
                // GUI Pro popup: blue card with the ribbon title riding over its top edge.
                card.sprite=skin.popup;card.color=Color.white;
                // The ribbon is sliced down its middle: the centre plate stretches to fit the title, the tails keep their shape.
                var ribbonImage=Icon(panel,"Title ribbon",skin.ribbon,Color.white,new Vector2(.5f,.975f),1);
                ribbonImage.type=Image.Type.Sliced;ribbonImage.rectTransform.sizeDelta=new Vector2(820,150);
                Label(panel,title,new Vector2(.5f,.985f),new Vector2(520,100),46,Color.white);
            }
            else
            {
                Plate(panel,"Title ribbon",new Vector2(.5f,.9f),new Vector2(800,130),ribbon);
                Label(panel,title,new Vector2(.5f,.9f),new Vector2(780,115),56,Color.white);
            }
            var body=Label(panel,message,new Vector2(.5f,.64f),new Vector2(810,210),32,skin?Color.white:Ink);body.name="Message";
            return panel;
        }
        public void Choice(string title,string message,string first,UnityAction yes,string second=null,UnityAction no=null)
        {
            var p=ShowModal(title,message);
            bool pair=skin&&second!=null;
            Button(p,first,pair?new Vector2(.72f,.17f):new Vector2(.5f,.31f),new Vector2(pair?360:740,100),Teal,yes,36);
            if(second!=null) Button(p,second,pair?new Vector2(.28f,.17f):new Vector2(.5f,.15f),new Vector2(pair?360:740,90),new Color(.30f,.46f,.50f),no,30);
        }
        public void Result(string title,string message,Sprite icon,int earned,string first,UnityAction yes,string second=null,UnityAction no=null)
        {
            bool won=icon==FishOutIcons.Star;
            var panel=ShowModal(title,message,won?Teal:new Color(.86f,.24f,.28f),.45f);
            bool pair=skin&&second!=null;
            Button(panel,first,pair?new Vector2(.72f,.17f):new Vector2(.5f,.31f),new Vector2(pair?360:740,100),Teal,yes,36);
            if(second!=null) Button(panel,second,pair?new Vector2(.28f,.17f):new Vector2(.5f,.15f),new Vector2(pair?360:740,90),new Color(.30f,.46f,.50f),no,30);
            var body=(RectTransform)panel.Find("Message");body.anchorMin=body.anchorMax=new Vector2(.5f,.53f);
            var filled=won?new Color(1f,.76f,.16f):new Color(1f,.3f,.38f);
            var icons=new Image[3];
            for(int i=0;i<3;i++)icons[i]=Icon(panel,"Result icon "+(i+1),icon,i<earned?filled:new Color(.74f,.8f,.82f),new Vector2(.5f+(i-1)*.2f,.72f),i==1?170:140);
            StartCoroutine(Reveal(panel,icons));
        }
        // Spec §6 clear screen, built from the project's GUI Pro-SuperCasual "Stage Clear" popup:
        // CLEAR, stars for the lives kept, coins earned (placeholder) and one button.
        public const string StageClearResource="UI/StageClear";
        public void StageClear(int earnedStars,int coins,string message,string first,UnityAction yes)
        {
            var prefab=Resources.Load<GameObject>(StageClearResource);
            if(!prefab){Result("CLEAR",message,FishOutIcons.Star,earnedStars,first,yes);return;}
            CloseModal();
            var root=(RectTransform)Instantiate(prefab,safe,false).transform;root.name="Stage clear";modal=root.gameObject;
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.localScale=Vector3.one;
            // The popup is laid out for a 2340-tall screen; shrink the content if the safe area is shorter than the layout.
            float fit=Mathf.Min(1,(safe.rect.height-60)/1560f);
            RectTransform Part(string name,Vector2 position)
            {
                var part=root.Find(name) as RectTransform;if(!part)return null;
                part.anchoredPosition=position*fit;part.localScale=Vector3.one*fit;return part;
            }
            void Hide(string name){var part=root.Find(name);if(part)part.gameObject.SetActive(false);}
            Hide("Slider_Level01_l_Blue");Hide("Button01_AdClaim");
            // The bright painted seabed needs a darker veil than the pack's demo scene.
            var veil=root.Find("Dimed")?.GetComponent<Image>();if(veil)veil.color=new Color(0,0,0,.72f);
            var title=Part("Image_Text_Clear",new Vector2(0,700));
            var group=Part("Group_Stars",new Vector2(0,400));
            Part("Title_Line01",new Vector2(0,-90));
            var rewards=Part("ItemFrame02_Basic",new Vector2(0,-250));
            var button=Part("Button01_Claim",new Vector2(0,-560));
            // Stars fill left, centre, right; the popup holds one lit and one unlit sprite to copy from.
            var stars=new Image[3];
            if(group)
            {
                stars[0]=group.Find("Icon_Star (1)")?.GetComponent<Image>();stars[1]=group.Find("Icon_Star")?.GetComponent<Image>();stars[2]=group.Find("Icon_Star (2)")?.GetComponent<Image>();
                Sprite on=stars[1]?stars[1].sprite:null,off=stars[2]?stars[2].sprite:null;
                for(int i=0;i<3;i++)if(stars[i])stars[i].sprite=i<earnedStars?on:off;
            }
            if(rewards)
            {
                var grid=rewards.GetComponent<GridLayoutGroup>();if(grid)grid.childAlignment=TextAnchor.MiddleCenter;
                for(int i=0;i<rewards.childCount;i++)rewards.GetChild(i).gameObject.SetActive(i==0);
                if(rewards.childCount>0)
                {
                    var coin=rewards.GetChild(0);
                    var bonus=coin.Find("Label_Tail_Red");if(bonus)bonus.gameObject.SetActive(false);
                    var amount=coin.Find("Text (TMP)");if(amount)SetLabel(amount,coins.ToString());
                }
            }
            if(title&&!string.IsNullOrEmpty(message))
            {
                var line=root.Find("Title_Line01/Text_Title");
                if(line)
                {
                    var note=(RectTransform)Instantiate(line.gameObject,root,false).transform;note.name="Message";
                    note.anchorMin=note.anchorMax=new Vector2(.5f,.5f);note.anchoredPosition=new Vector2(0,170)*fit;note.sizeDelta=new Vector2(940,140);note.localScale=Vector3.one*fit;
                    SetLabel(note,message);
                }
            }
            if(button)
            {
                button.name="Button "+first;button.sizeDelta=new Vector2(560,button.sizeDelta.y);
                var label=button.Find("Text (TMP)");if(label)SetLabel(label,first);
                var click=button.GetComponent<Button>();if(!click)click=button.gameObject.AddComponent<Button>();
                click.targetGraphic=button.GetComponent<Image>();click.onClick.RemoveAllListeners();click.onClick.AddListener(yes);
            }
            if(title&&group)StartCoroutine(Reveal(title,System.Array.FindAll(stars,star=>star!=null),fit));
        }
        // Sets a legacy Text or a TextMeshPro label without a compile-time TextMeshPro dependency.
        private static void SetLabel(Transform target,string value)
        {
            foreach(var graphic in target.GetComponents<Graphic>())
            {
                if(graphic is Text legacy){legacy.text=value;return;}
                var property=graphic.GetType().GetProperty("text");
                if(property!=null&&property.PropertyType==typeof(string)&&property.CanWrite){property.SetValue(graphic,value);return;}
            }
        }
        // The card settles in, then the icons pop one at a time.
        private System.Collections.IEnumerator Reveal(RectTransform panel,Image[] icons,float baseScale=1)
        {
            foreach(var icon in icons)icon.transform.localScale=Vector3.zero;
            const float settle=.18f,gap=.12f,pop=.2f;
            for(float t=0;t<settle+gap*icons.Length+pop;t+=Time.unscaledDeltaTime)
            {
                if(!panel)yield break;
                panel.localScale=Vector3.one*Mathf.Lerp(.85f,1,Mathf.Clamp01(t/settle))*baseScale;
                for(int i=0;i<icons.Length;i++)
                {
                    float k=Mathf.Clamp01((t-settle-gap*i)/pop);
                    icons[i].transform.localScale=Vector3.one*(k+Mathf.Sin(k*Mathf.PI)*.25f);
                }
                yield return null;
            }
            if(!panel)yield break;
            panel.localScale=Vector3.one*baseScale;
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
            Label(p,caption,new Vector2(.36f,y),new Vector2(530,70),32,skin?Color.white:Ink);
            var root=Rect(caption+" toggle",p,new Vector2(.82f,y),new Vector2(.82f,y),Vector2.zero,skin?new Vector2(84,84):new Vector2(80,70));
            var bg=root.gameObject.AddComponent<Image>(); bg.sprite=skin&&skin.toggleBg?skin.toggleBg:RoundSprite(); bg.type=Image.Type.Sliced; bg.color=skin?Color.white:new Color(.70f,.81f,.82f);
            var check=Rect("On",root,new Vector2(.2f,.2f),new Vector2(.8f,.8f),Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>(); check.color=Teal; check.sprite=RoundSprite();
            if(skin&&skin.toggleCheck){check.sprite=skin.toggleCheck;check.color=Color.white;check.preserveAspect=true;check.rectTransform.anchorMin=new Vector2(.12f,.12f);check.rectTransform.anchorMax=new Vector2(.88f,.92f);}
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
