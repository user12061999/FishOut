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
        
        // Win / lose mascot: a game fish rendered by its own camera into the UI; released with the modal.
        private GameObject mascotStage;
        private RenderTexture mascotTexture;
        // Home mascot lives in its own stage so popups opened over Home do not remove it.
        private GameObject homeStage;
        private RenderTexture homeTexture;
        private RectTransform homeMascotSlot;
        private Text homeCoins;
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
        // Benchmark tutorial card: cream face, purple rim, brown type.
        private static readonly Color TipRim = new Color(.44f,.35f,.86f);
        private static readonly Color TipCard = new Color(1f,.97f,.89f);
        private static readonly Color TipInk = new Color(.42f,.22f,.12f);
        // Shared benchmark palette: purple primary with a pale rim, deep purple outlines and shade, cream, brown, green, red.
        public static readonly Color Purple = new Color(.50f,.39f,.94f);
        public static readonly Color PurpleRim = new Color(.90f,.88f,1f);
        public static readonly Color PurpleDeep = new Color(.27f,.17f,.58f);
        public static readonly Color Cream = new Color(1f,.97f,.89f);
        public static readonly Color Brown = new Color(.42f,.22f,.12f);
        public static readonly Color Green = new Color(.40f,.80f,.24f);
        public static readonly Color Red = new Color(.95f,.36f,.26f);

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
            homeLevel = BuildHome((RectTransform)home.transform);
            gameplay = Rect("Gameplay",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject;
            // Benchmark top bar: purple Level ribbon top-left, purple gear top-right; the cave (top centre-right) stays in view.
            // Retry sits under the gear and lives + fish in a cream pill under the badge, so nothing the owner asked to keep is lost.
            level = LevelBadge(gameplay.transform,new Vector2(.165f,.945f));
            IconButton(gameplay.transform,"Settings",skin?skin.settingsIcon:FishOutIcons.Gear,new Vector2(.905f,.945f),()=>game.OpenSettings());
            IconButton(gameplay.transform,"Retry",skin?skin.retryIcon:FishOutIcons.Retry,new Vector2(.905f,.868f),()=>game.ConfirmRetry());
            // Lives were removed (partner feedback 2026-11-11): the pill only counts the fish left.
            var status=Tile(gameplay.transform,"Status plate",new Vector2(.135f,.862f),new Vector2(170,80),Purple,Cream,7);
            if(skin)Icon(status.transform,"Fish icon",skin.fishIcon,Purple,new Vector2(.32f,.5f),48);
            remaining = Label(status.transform,"3",new Vector2(skin?.68f:.5f,.5f),new Vector2(70,60),40,Brown,false);
            // Benchmark tutorial card: 80% of the screen wide, cream with a purple rim, one line of large brown type.
            var tip=Rect("Instruction plate",gameplay.transform,new Vector2(.1f,.19f),new Vector2(.9f,.19f),Vector2.zero,new Vector2(0,196));
            instructionPlate=tip.gameObject.AddComponent<Image>();instructionPlate.sprite=RoundSprite();instructionPlate.type=Image.Type.Sliced;instructionPlate.color=TipRim;instructionPlate.raycastTarget=false;
            var lift=tip.gameObject.AddComponent<Shadow>();lift.effectColor=new Color(0,0,0,.28f);lift.effectDistance=new Vector2(0,-8);
            var face=Rect("Card",tip,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-16,-16)).gameObject.AddComponent<Image>();
            face.sprite=RoundSprite();face.type=Image.Type.Sliced;face.color=TipCard;face.raycastTarget=false;
            if(skin&&skin.whitePanel){instructionPlate.sprite=skin.whitePanel;instructionPlate.color=Cream;instructionPlate.pixelsPerUnitMultiplier=.8f;face.enabled=false;lift.enabled=false;}
            instructions=Label(tip,"",new Vector2(.5f,.5f),new Vector2(0,150),46,TipInk,false);
            instructions.rectTransform.anchorMin=new Vector2(0,.5f);instructions.rectTransform.anchorMax=new Vector2(1,.5f);instructions.rectTransform.sizeDelta=new Vector2(-70,64);
            // A one-line box: best fit shrinks a long tip instead of wrapping it onto a second line.
            instructions.verticalOverflow=VerticalWrapMode.Truncate;instructions.resizeTextForBestFit=true;instructions.resizeTextMinSize=30;instructions.resizeTextMaxSize=46;
            hint = Pad(gameplay.transform,"HINT",skin?skin.hintIcon:FishOutIcons.Bulb,skin?Color.white:new Color(1f,.86f,.3f),new Vector2(.36f,.082f),()=>game.UseHint(),out hintBadge,out _,skin?skin.hintButton:null);
            rescue = Pad(gameplay.transform,"RESCUE",skin?skin.rescueIcon:FishOutIcons.Buoy,skin?Color.white:new Color(1f,.36f,.36f),new Vector2(.64f,.082f),()=>game.BeginRescue(),out rescueBadge,out rescueCaption,skin?skin.rescueButton:null);
            notice = Label(safe,"",new Vector2(.5f,.255f),new Vector2(920,120),48,Color.white);
            Thick(notice,PurpleDeep,4);
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
        public Text Label(Transform parent,string value,Vector2 anchor,Vector2 size,int point,Color color,bool outlined=true)
        {
            var rt=Rect(value.Length>30?"Text":value,parent,anchor,anchor,Vector2.zero,size);
            var t=rt.gameObject.AddComponent<Text>(); t.font=font; t.text=value; t.fontSize=point;
            t.color=color; t.alignment=TextAnchor.MiddleCenter; t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Overflow;
            if(outlined&&color!=Ink){ var outline=rt.gameObject.AddComponent<Outline>(); outline.effectColor=new Color(PurpleDeep.r,PurpleDeep.g,PurpleDeep.b,.9f); outline.effectDistance=new Vector2(3,-3); }
            return t;
        }
        // The benchmark's fat outline: several offset outlines plus a drop, so the rounded font reads as a sticker.
        public static void Thick(Text t,Color edge,float width)
        {
            foreach(var old in t.GetComponents<Outline>())UnityEngine.Object.DestroyImmediate(old);
            // Each Outline multiplies the glyph vertices by 5, so two is the cap: four overflowed UI meshes on long toasts.
            foreach(var d in new[]{new Vector2(width,-width),new Vector2(width*1.25f,width*.35f)})
            {var o=t.gameObject.AddComponent<Outline>();o.effectColor=edge;o.effectDistance=d;}
            var drop=t.gameObject.AddComponent<Shadow>();drop.effectColor=edge;drop.effectDistance=new Vector2(0,-width*1.8f);
        }
        // A rounded tile with a coloured rim and a face inset by `border`, sitting on a deep purple drop.
        private Image Tile(Transform parent,string name,Vector2 anchor,Vector2 size,Color rim,Color face,float border,bool pack=true)
        {
            var rimImage=Plate(parent,name,anchor,size,rim);
            // Pack look: one white pack tile tinted to the face colour; its own outline and bottom shade replace the drawn rim.
            if(pack&&PackTile(rimImage,face))return rimImage;
            Face(rimImage.transform,face,border);
            var drop=rimImage.gameObject.AddComponent<Shadow>();drop.effectColor=new Color(PurpleDeep.r,PurpleDeep.g,PurpleDeep.b,.6f);drop.effectDistance=new Vector2(0,-7);
            return rimImage;
        }
        private static Image Face(Transform parent,Color color,float border)
        {
            var face=Rect("Face",parent,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-2*border,-2*border)).gameObject.AddComponent<Image>();
            face.sprite=RoundSprite();face.type=Image.Type.Sliced;face.color=color;face.raycastTarget=false;
            return face;
        }
        private bool PackTile(Image image,Color color,float thickness=.75f)
        {
            if(!skin||!skin.whiteTile)return false;
            image.sprite=skin.whiteTile;image.type=Image.Type.Sliced;image.color=color;image.pixelsPerUnitMultiplier=thickness;
            return true;
        }
        // Benchmark level ribbon: pale purple wings behind a taller purple plate with "Level" over the number.
        private Text LevelBadge(Transform parent,Vector2 anchor)
        {
            // Owner (msg 629): keep the drawn badge here, not the pack ribbon.
            // Owner (msg 726): one background only — a single plate holding "Level" and the number.
            var plate=Tile(parent,"Level plate",anchor,new Vector2(150,160),PurpleRim,Purple,7,false);
            var caption=Label(plate.transform,"Level",new Vector2(.5f,.72f),new Vector2(130,46),36,Color.white);Thick(caption,PurpleDeep,2.5f);
            var number=Label(plate.transform,"1",new Vector2(.5f,.33f),new Vector2(130,80),68,Color.white);Thick(number,PurpleDeep,3.5f);
            return number;
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
            if(!sprite&&(color==PurpleRim||color==Purple)){var depth=rt.gameObject.GetComponent<Shadow>();depth.effectColor=new Color(PurpleDeep.r,PurpleDeep.g,PurpleDeep.b,.7f);depth.effectDistance=new Vector2(0,-8);}
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
            // Benchmark top button: purple rounded square with a pale rim and a white pictogram.
            var rt=Rect(name,parent,anchor,anchor,Vector2.zero,new Vector2(124,124));
            var button=Clickable(rt,PurpleRim,action);
            if(!PackTile((Image)button.targetGraphic,Purple))Face(rt,Purple,7);
            var icon=Icon(rt,"Icon",sprite,Color.white,new Vector2(.5f,.5f),78);
            var edge=icon.gameObject.AddComponent<Outline>();edge.effectColor=PurpleDeep;edge.effectDistance=new Vector2(3,-3);
            Depth(icon);
        }
        private Button Pad(Transform parent,string caption,Sprite sprite,Color tint,Vector2 anchor,UnityAction action,out Text badge,out Text label,Sprite plate=null)
        {
            // Benchmark-style booster: cream tile with a purple rim, purple pictogram, brown caption, red count badge.
            var rt=Rect(caption,parent,anchor,anchor,Vector2.zero,new Vector2(200,200));
            var button=Clickable(rt,Purple,action);
            if(!PackTile((Image)button.targetGraphic,Cream,.6f))Face(rt,Cream,8);
            var icon=Icon(rt,"Icon",sprite,skin?Purple:tint,new Vector2(.5f,.6f),104);
            label=Label(rt,caption,new Vector2(.5f,.18f),new Vector2(190,50),34,Brown,false);
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
            level.text=number.ToString(); remaining.text=board.Fish.Count.ToString();
            hint.interactable=!board.HintUsed&&!selecting&&!game.IsLocked;
            rescue.interactable=selecting||(!board.RescueUsed&&!game.IsLocked);
            hintBadge.text=board.HintUsed?"0":"1";
            rescueBadge.text=board.RescueUsed?"0":"1";
            rescueCaption.text=selecting?"CANCEL":"RESCUE";
        }
        public void SetHome(bool value,int number)
        {
            CloseModal(); home.SetActive(value); gameplay.SetActive(!value); homeLevel.text="Level "+number;
            if(homeCoins&&game)homeCoins.text=game.Coins.ToString();
            if(!value)ReleaseHomeMascot();
            else if(!homeStage&&homeMascotSlot)
            {
                var view=Mascot(homeMascotSlot,Vector2.zero,homeMascotSlot.rect.width>0?homeMascotSlot.rect.width:760,true,true);
                if(view)view.rectTransform.SetAsFirstSibling();
            }
            notice.text="";
        }
        public void Toast(string value,float seconds=2.5f) { notice.text=value; noticeTime=seconds; }
        public void Hazard() { flashTime=.5f; }
        public void CloseModal()
        {
            ReleaseMascot();
            if(!modal) return;
            var old=modal; modal=null; old.SetActive(false);
            if(Application.isPlaying) Destroy(old); else DestroyImmediate(old);
        }
        public RectTransform ShowModal(string title,string message){return Dialog(title,message,0,false);}
        private RectTransform ShowModal(string title,string message,Color ribbon,float dimming){return Dialog(title,message,0,false);}
        // Benchmark dialog: cream card, purple ribbon title, brown body; the card grows with its text and content.
        // A sheet sits low with a light veil so the board above (e.g. the highlighted crab) stays visible.
        private RectTransform Dialog(string title,string message,float content,bool sheet,bool buttons=true)
        {
            CloseModal();
            var overlay=Rect("Modal",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero); modal=overlay.gameObject;
            var veil=Rect("Veil",overlay,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,600)).gameObject.AddComponent<Image>();veil.color=new Color(0,0,0,sheet?.6f:.92f);
            int lines=0;
            if(!string.IsNullOrEmpty(message))foreach(var part in message.Split('\n'))lines+=Mathf.Max(1,Mathf.CeilToInt(part.Length/33f));
            float bodyHeight=lines*52;
            float height=150+bodyHeight+(lines>0?40:0)+content+(buttons?180:40);
            var anchor=sheet?new Vector2(.5f,0):new Vector2(.5f,.5f);
            var panel=Rect("Dialog",overlay,anchor,anchor,sheet?new Vector2(0,height*.5f+60):Vector2.zero,new Vector2(920,height));
            var card=panel.gameObject.AddComponent<Image>();card.color=Cream;
            if(skin&&skin.whitePanel){card.sprite=skin.whitePanel;card.type=Image.Type.Sliced;card.pixelsPerUnitMultiplier=.8f;}
            else{card.sprite=RoundSprite();card.type=Image.Type.Sliced;}
            var ribbon=Rect("Title ribbon",panel,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-8),new Vector2(720,150)).gameObject.AddComponent<Image>();ribbon.raycastTarget=false;
            if(skin&&skin.ribbon){ribbon.sprite=skin.ribbon;ribbon.type=Image.Type.Sliced;}else{ribbon.sprite=RoundSprite();ribbon.type=Image.Type.Sliced;ribbon.color=Purple;}
            var head=Label(ribbon.rectTransform,title,new Vector2(.5f,.6f),new Vector2(560,100),52,Cream);Thick(head,PurpleDeep,3);
            var body=Label(panel,message,new Vector2(.5f,1),new Vector2(800,bodyHeight+20),40,Brown,false);body.name="Message";
            body.rectTransform.anchoredPosition=new Vector2(0,-150-bodyHeight*.5f);
            return panel;
        }
        public void Choice(string title,string message,string first,UnityAction yes,string second=null,UnityAction no=null,bool sheet=false)
        {
            var p=Dialog(title,message,0,sheet);
            float y=-p.sizeDelta.y+115;
            if(second!=null){WidePill(p,second,new Vector2(-210,y),Lavender,null,no,390);WidePill(p,first,new Vector2(210,y),Green,null,yes,390);}
            else WidePill(p,first,new Vector2(0,y),Green,null,yes,460);
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
        // Spec §6 clear screen on the project's GUI Pro-SuperCasual "Stage Clear" popup, laid out like the benchmark win screen:
        // dark veil over a hidden HUD, YOU WIN, 3 stars, one big claim button with the coins, a wallet top-right.
        public const string StageClearResource="UI/StageClear";
        public void StageClear(int earnedStars,int coins,string message,string first,UnityAction yes)
        {
            var prefab=Resources.Load<GameObject>(StageClearResource);
            if(!prefab){Result("YOU WIN",message,FishOutIcons.Star,earnedStars,first,yes);return;}
            CloseModal();
            gameplay.SetActive(false);
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
            var rewards=root.Find("ItemFrame02_Basic");
            Sprite coinSprite=skin?skin.coin:null;
            if(!coinSprite&&rewards&&rewards.childCount>0){var packCoin=rewards.GetChild(0).Find("ItemIcon");var packImage=packCoin?packCoin.GetComponent<Image>():null;if(packImage)coinSprite=packImage.sprite;}
            Hide("Slider_Level01_l_Blue");Hide("Button01_AdClaim");Hide("Image_Text_Clear");Hide("Title_Line01");Hide("ItemFrame02_Basic");
            // The bright painted seabed needs a darker veil than the pack's demo scene.
            // The pack's dim sprite is mostly clear; a flat dark veil matches the benchmark's darkened win screen.
            // The project blends in linear space, so a strong alpha is needed to darken the screen to about a third (as in the benchmark).
            var veil=root.Find("Dimed")?.GetComponent<Image>();if(veil){veil.sprite=null;veil.color=new Color(0,0,0,.96f);}
            // Benchmark order top to bottom: title, the happy animal, (our) stars, the claim button.
            // Stars shrink to a row under the mascot so the animal stays the hero, as in the benchmark.
            var group=Part("Group_Stars",new Vector2(0,-110));if(group)group.localScale=Vector3.one*fit*.62f;
            var glow=Part("Group_ImageEffect",new Vector2(0,-30));if(glow)glow.localScale=Vector3.one*fit*.62f;
            var button=Part("Button01_Claim",new Vector2(0,-450));
            Mascot(root,new Vector2(0,250)*fit,560*fit,true);
            // Benchmark title: big cream words with a thick brown rim.
            var titleText=Label(root,"YOU WIN",new Vector2(.5f,.5f),new Vector2(900,170),TitleSize,new Color(1f,.96f,.84f),false);
            var title=titleText.rectTransform;title.name="Title YOU WIN";title.anchoredPosition=new Vector2(0,700)*fit;title.localScale=Vector3.one*fit;
            titleText.horizontalOverflow=HorizontalWrapMode.Overflow;
            foreach(var d in new[]{new Vector2(5,5),new Vector2(7,-2),new Vector2(-2,7)}){var rim=title.gameObject.AddComponent<Outline>();rim.effectColor=Brown;rim.effectDistance=d;}
            var drop=title.gameObject.AddComponent<Shadow>();drop.effectColor=new Color(.2f,.08f,0,.85f);drop.effectDistance=new Vector2(0,-12);
            var wallet=Wallet(root,coinSprite,game.Coins);
            // Stars fill left, centre, right; the popup holds one lit and one unlit sprite to copy from.
            var stars=new Image[3];
            if(group)
            {
                stars[0]=group.Find("Icon_Star (1)")?.GetComponent<Image>();stars[1]=group.Find("Icon_Star")?.GetComponent<Image>();stars[2]=group.Find("Icon_Star (2)")?.GetComponent<Image>();
                Sprite on=stars[1]?stars[1].sprite:null,off=stars[2]?stars[2].sprite:null;
                for(int i=0;i<3;i++)if(stars[i])stars[i].sprite=i<earnedStars?on:off;
            }
            if(!string.IsNullOrEmpty(message))
            {
                // Our own label (the prefab's line is TextMeshPro, which this assembly does not reference): big cream text under the claim button.
                var note=Label(root,first=="HOME"?message+"\nCollect your coins to head home.":message,new Vector2(.5f,.5f),new Vector2(940,130),46,Cream);
                note.name="Message";note.rectTransform.anchoredPosition=new Vector2(0,-605)*fit;note.rectTransform.localScale=Vector3.one*fit;Thick(note,PurpleDeep,3);
            }
            if(button)
            {
                // Benchmark claim button: one big purple button carrying the coin and the amount; a tap collects and moves on.
                button.name="Button "+first;button.sizeDelta=new Vector2(470,160);
                var face=button.GetComponent<Image>();if(face&&skin&&skin.claimButton)face.sprite=skin.claimButton;
                var label=button.Find("Text (TMP)") as RectTransform;
                if(label)
                {
                    SetLabel(label,coins.ToString());SetFontSize(label,78);
                    label.anchorMin=new Vector2(.44f,0);label.anchorMax=Vector2.one;label.offsetMin=new Vector2(0,20);label.offsetMax=new Vector2(-40,-8);
                }
                if(coinSprite){var icon=Icon(button,"Coin",coinSprite,Color.white,new Vector2(.31f,.55f),120);icon.preserveAspect=true;}
                var click=button.GetComponent<Button>();if(!click)click=button.gameObject.AddComponent<Button>();
                click.targetGraphic=face;click.onClick.RemoveAllListeners();
                click.onClick.AddListener(()=>{if(!click.interactable)return;click.interactable=false;StartCoroutine(Claim(root,button,wallet,coins,coinSprite,yes));});
            }
            if(group)StartCoroutine(Reveal(title,System.Array.FindAll(stars,star=>star!=null),fit));
            var lit=new System.Collections.Generic.List<Image>();for(int i=0;i<3;i++)if(stars[i]&&i<earnedStars)lit.Add(stars[i]);
            StartCoroutine(StarShine(root,lit.ToArray()));
            StartCoroutine(Confetti(root));
        }
        private const int TitleSize=128;
        // Benchmark lose screen: dark veil, YOU LOSE in lavender with a purple rim, the fish drooping,
        // one purple Try Again pill, and a Home button in the top-left corner.
        public void Lose(string reason,UnityAction retry,UnityAction home)
        {
            CloseModal();
            gameplay.SetActive(false);
            var root=Rect("Stage failed",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);modal=root.gameObject;
            var veil=root.gameObject.AddComponent<Image>();veil.color=new Color(0,0,0,.96f);
            var titleText=Label(root,"YOU LOSE",new Vector2(.5f,.5f),new Vector2(900,170),TitleSize,new Color(.93f,.9f,1f),false);
            var title=titleText.rectTransform;title.name="Title YOU LOSE";title.anchoredPosition=new Vector2(0,520);
            titleText.horizontalOverflow=HorizontalWrapMode.Overflow;
            foreach(var d in new[]{new Vector2(5,5),new Vector2(7,-2),new Vector2(-2,7)}){var rim=title.gameObject.AddComponent<Outline>();rim.effectColor=PurpleDeep;rim.effectDistance=d;}
            var drop=title.gameObject.AddComponent<Shadow>();drop.effectColor=new Color(.12f,.06f,.3f,.9f);drop.effectDistance=new Vector2(0,-12);
            // Why the stage ended: a crab out of bumps, or no way left to the cave.
            if(!string.IsNullOrEmpty(reason)){var why=Label(root,reason,new Vector2(.5f,.5f),new Vector2(900,80),52,Color.white,false);why.rectTransform.anchoredPosition=new Vector2(0,385);why.name="Lose reason";Thick(why,PurpleDeep,3);}
            Mascot(root,new Vector2(0,20),600,false);
            var again=Rect("Button Try Again",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-540),new Vector2(460,150));
            Clickable(again,Purple,retry,skin?skin.claimButton:null);
            var caption=Label(again,"Try Again",new Vector2(.5f,.56f),new Vector2(430,110),62,Color.white);Thick(caption,PurpleDeep,3);
            var corner=Rect("Button Home",root,new Vector2(0,1),new Vector2(0,1),new Vector2(115,-170),new Vector2(124,124));
            var homeButton=Clickable(corner,PurpleRim,home);
            if(!PackTile((Image)homeButton.targetGraphic,Purple))Face(corner,Purple,7);
            if(skin&&skin.homeIcon){var icon=Icon(corner,"Icon",skin.homeIcon,Color.white,new Vector2(.5f,.5f),78);Depth(icon);}
            else Label(corner,"HOME",new Vector2(.5f,.5f),new Vector2(120,60),30,Color.white);
            StartCoroutine(Reveal(title,new Image[0]));
        }
        // Benchmark wallet: a light pill in the top-right corner with the coin riding over its left end.
        private Text Wallet(RectTransform root,Sprite coin,int amount)
        {
            var pill=Rect("Wallet",root,Vector2.one,Vector2.one,new Vector2(-175,-80),new Vector2(270,84));
            var bg=pill.gameObject.AddComponent<Image>();bg.sprite=RoundSprite();bg.type=Image.Type.Sliced;bg.color=new Color(1f,.97f,.9f);bg.raycastTarget=false;
            PackTile(bg,Cream,.8f);
            if(coin){var icon=Icon(pill,"Coin",coin,Color.white,new Vector2(0,.5f),112);icon.preserveAspect=true;}
            var value=Label(pill,amount.ToString(),new Vector2(.58f,.5f),new Vector2(190,80),46,Ink);value.name="Wallet amount";
            return value;
        }
        // Claim: coins fly from the button into the wallet, which counts up; then the next step runs.
        private System.Collections.IEnumerator Claim(RectTransform root,RectTransform button,Text wallet,int coins,Sprite coin,UnityAction done)
        {
            int start=game.Coins;game.AddCoins(coins);
            var pill=wallet?wallet.rectTransform.parent as RectTransform:null;
            const int count=8;const float flight=.5f,gap=.05f;
            var flyers=new RectTransform[count];
            Vector2 from=root.InverseTransformPoint(button.position);
            Vector2 to=pill?(Vector2)root.InverseTransformPoint(pill.position)-new Vector2(pill.rect.width*.5f,0):from;
            float total=flight+gap*count+.15f;
            for(float t=0;t<total;t+=Time.unscaledDeltaTime)
            {
                if(!root)yield break;
                for(int i=0;i<count;i++)
                {
                    float k=(t-gap*i)/flight;
                    if(k<0)continue;
                    if(k>=1){if(flyers[i]){Destroy(flyers[i].gameObject);flyers[i]=null;}continue;}
                    if(!flyers[i]&&coin){var im=Rect("Flying coin",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),from,Vector2.one*92).gameObject.AddComponent<Image>();im.sprite=coin;im.raycastTarget=false;im.preserveAspect=true;flyers[i]=im.rectTransform;}
                    if(!flyers[i])continue;
                    float e=k*k*(3-2*k);
                    var path=to-from;var side=new Vector2(-path.y,path.x).normalized*Mathf.Sin(e*Mathf.PI)*(i%2==0?110:-110);
                    flyers[i].anchoredPosition=Vector2.Lerp(from,to,e)+side;flyers[i].localScale=Vector3.one*Mathf.Lerp(1.15f,.7f,e);
                }
                float filled=Mathf.Clamp01((t-flight*.8f)/(total-flight*.8f-.1f));
                if(wallet)wallet.text=(start+Mathf.RoundToInt(coins*filled)).ToString();
                if(pill)pill.localScale=Vector3.one*(1+.12f*Mathf.Sin(Mathf.Clamp01((t-flight*.8f)/.25f)*Mathf.PI));
                yield return null;
            }
            if(wallet)wallet.text=(start+coins).ToString();
            if(pill)pill.localScale=Vector3.one;
            done();
        }
        // Fixed TextMeshPro size (auto-sizing off) without a compile-time TextMeshPro dependency.
        private static void SetFontSize(Transform target,float size)
        {
            foreach(var graphic in target.GetComponents<Graphic>())
            {
                var type=graphic.GetType();
                var auto=type.GetProperty("enableAutoSizing");if(auto!=null&&auto.CanWrite)auto.SetValue(graphic,false);
                var font=type.GetProperty("fontSize");
                if(font!=null&&font.CanWrite){if(font.PropertyType==typeof(float))font.SetValue(graphic,size);else if(font.PropertyType==typeof(int))font.SetValue(graphic,(int)size);}
            }
        }
        // Earned stars glow and pulse, and glints twinkle around them while the popup is open.
        private System.Collections.IEnumerator StarShine(RectTransform root,Image[] stars)
        {
            if(stars.Length==0)yield break;
            yield return new WaitForSecondsRealtime(.18f+.12f*3+.2f);
            var halos=new Image[stars.Length];
            for(int i=0;i<stars.Length;i++)
            {
                var star=stars[i];if(!star)continue;
                var halo=Rect("Star glow",star.transform.parent,star.rectTransform.anchorMin,star.rectTransform.anchorMax,star.rectTransform.anchoredPosition,star.rectTransform.sizeDelta*2.6f).gameObject.AddComponent<Image>();
                halo.sprite=FishOutIcons.Glow;halo.color=new Color(1f,.9f,.35f,.0f);halo.raycastTarget=false;
                halo.rectTransform.pivot=star.rectTransform.pivot;halo.transform.SetSiblingIndex(star.transform.GetSiblingIndex());
                halos[i]=halo;
            }
            var glints=new System.Collections.Generic.List<(RectTransform rt,Image im,float born,float life,float spin)>();
            float next=0;
            for(float t=0;root&&root.gameObject.activeInHierarchy;t+=Time.unscaledDeltaTime)
            {
                for(int i=0;i<stars.Length;i++)
                {
                    if(!stars[i]||!halos[i])continue;
                    float wave=.5f+.5f*Mathf.Sin(t*4.2f+i*1.3f);
                    halos[i].color=new Color(1f,.93f,.45f,Mathf.Lerp(.55f,1f,wave)*Mathf.Clamp01(t/.3f));
                    halos[i].transform.localScale=Vector3.one*Mathf.Lerp(.85f,1.2f,wave);
                    stars[i].transform.localScale=Vector3.one*(1+.06f*wave);
                }
                if(t>=next)
                {
                    next=t+UnityEngine.Random.Range(.07f,.16f);
                    var host=stars[UnityEngine.Random.Range(0,stars.Length)];
                    if(host)
                    {
                        var size=host.rectTransform.rect.size;
                        var offset=Vector2.Scale(UnityEngine.Random.insideUnitCircle,size*.62f);
                        var glint=Rect("Glint",host.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),offset,Vector2.one*UnityEngine.Random.Range(46f,92f));
                        var im=glint.gameObject.AddComponent<Image>();im.sprite=FishOutIcons.Twinkle;im.raycastTarget=false;im.color=Color.white;
                        glints.Add((glint,im,t,UnityEngine.Random.Range(.45f,.75f),UnityEngine.Random.Range(-160f,160f)));
                    }
                }
                for(int g=glints.Count-1;g>=0;g--)
                {
                    var item=glints[g];float k=(t-item.born)/item.life;
                    if(!item.rt||k>=1){if(item.rt)Destroy(item.rt.gameObject);glints.RemoveAt(g);continue;}
                    item.rt.localScale=Vector3.one*Mathf.Sin(k*Mathf.PI);item.rt.localEulerAngles=new Vector3(0,0,item.spin*k);
                }
                yield return null;
            }
        }
        // Party poppers: two bursts of paper from the bottom corners over the popup, then a short drift from the top.
        private System.Collections.IEnumerator Confetti(RectTransform root)
        {
            var layer=Rect("Confetti",root,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);layer.SetAsLastSibling();
            var area=root.rect.size;if(area.x<=0)area=new Vector2(1080,1920);
            var palette=new[]{new Color(1f,.32f,.42f),new Color(1f,.82f,.2f),new Color(.3f,.85f,.45f),new Color(.25f,.65f,1f),new Color(.75f,.45f,1f),new Color(1f,.55f,.2f),Color.white};
            var bits=new System.Collections.Generic.List<(RectTransform rt,Vector2 v,float spin,float flip,float phase)>();
            void Spawn(Vector2 at,Vector2 velocity)
            {
                var rt=Rect("Paper",layer,new Vector2(.5f,.5f),new Vector2(.5f,.5f),at,UnityEngine.Random.value<.25f?Vector2.one*UnityEngine.Random.Range(18f,26f):new Vector2(UnityEngine.Random.Range(16f,24f),UnityEngine.Random.Range(30f,44f)));
                var im=rt.gameObject.AddComponent<Image>();im.raycastTarget=false;im.color=palette[UnityEngine.Random.Range(0,palette.Length)];
                if(rt.sizeDelta.x==rt.sizeDelta.y)im.sprite=RoundSprite();
                rt.localEulerAngles=new Vector3(0,0,UnityEngine.Random.Range(0,360f));
                bits.Add((rt,velocity,UnityEngine.Random.Range(-540f,540f),UnityEngine.Random.Range(6f,14f),UnityEngine.Random.Range(0,6.3f)));
            }
            void Popper(float side)
            {
                var origin=new Vector2(side*area.x*.5f,-area.y*.5f);
                for(int i=0;i<70;i++)
                {
                    float angle=(90-side*UnityEngine.Random.Range(12f,38f))*Mathf.Deg2Rad;float speed=UnityEngine.Random.Range(.85f,1.35f)*area.y*1.05f;
                    Spawn(origin,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*speed);
                }
            }
            Popper(-1);Popper(1);
            float gravity=area.y*1.25f,drift=0;bool second=false;
            for(float t=0;t<6f&&layer;t+=Time.unscaledDeltaTime)
            {
                float dt=Time.unscaledDeltaTime;
                if(!second&&t>=.35f){second=true;Popper(-1);Popper(1);}
                // Benchmark: paper keeps raining over the whole screen while the result is on show.
                if(t<3.4f){drift+=dt*55;while(drift>=1){drift--;Spawn(new Vector2(UnityEngine.Random.Range(-.5f,.5f)*area.x,area.y*.55f),new Vector2(UnityEngine.Random.Range(-60f,60f),-UnityEngine.Random.Range(80f,220f)));}}
                for(int i=bits.Count-1;i>=0;i--)
                {
                    var b=bits[i];if(!b.rt){bits.RemoveAt(i);continue;}
                    b.v.y-=gravity*dt;b.v*=Mathf.Pow(.18f,dt);
                    // Paper flutters: it falls no faster than a slow terminal speed and sways as it turns.
                    if(b.v.y<-area.y*.32f)b.v.y=-area.y*.32f;
                    var pos=b.rt.anchoredPosition+b.v*dt+new Vector2(Mathf.Sin(t*3+b.phase)*40*dt,0);
                    b.rt.anchoredPosition=pos;b.rt.localEulerAngles+=new Vector3(0,0,b.spin*dt);
                    var s=b.rt.localScale;s.x=Mathf.Cos(t*b.flip+b.phase);b.rt.localScale=s;
                    if(pos.y<-area.y*.6f){Destroy(b.rt.gameObject);bits.RemoveAt(i);continue;}
                    bits[i]=b;
                }
                yield return null;
            }
            if(layer)Destroy(layer.gameObject);
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
        // The first game fish, posed in a far corner of the world and filmed into a transparent texture shown on the popup.
        private RawImage Mascot(RectTransform parent,Vector2 position,float size,bool happy,bool forHome=false)
        {
            if(forHome)ReleaseHomeMascot();else ReleaseMascot();
            var prefab=game&&game.fishPrefabs!=null&&game.fishPrefabs.Length>0?game.fishPrefabs[0]:null;
            if(!prefab)return null;
            var stage=new GameObject(forHome?"Home mascot stage":"Mascot stage");stage.transform.position=new Vector3(forHome?40:0,-500,0);
            if(forHome)homeStage=stage;else mascotStage=stage;
            var holder=new GameObject("Mascot");holder.transform.SetParent(stage.transform,false);
            var fish=Instantiate(prefab,holder.transform);fish.name=prefab.name;
            foreach(var c in fish.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var animator in fish.GetComponentsInChildren<Animator>())
            {
                animator.applyRootMotion=false;animator.updateMode=AnimatorUpdateMode.UnscaledTime;
                if(game.characterAnimations!=null&&game.characterAnimations.Length>0)animator.runtimeAnimatorController=game.characterAnimations[0];
            }
            var renderers=fish.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float factor=1f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z,.01f);
                fish.transform.localScale*=factor;
                var center=holder.transform.InverseTransformPoint(bounds.center);
                fish.transform.localPosition=-center*factor;
            }
            var camera=new GameObject("Mascot camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);
            camera.transform.localPosition=new Vector3(0,.35f,-3.4f);camera.transform.localRotation=Quaternion.Euler(6,0,0);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0,0,0,0);
            camera.fieldOfView=28;camera.nearClipPlane=.5f;camera.farClipPlane=8;camera.depth=-50;
            var texture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32);texture.name="Mascot";
            if(forHome)homeTexture=texture;else mascotTexture=texture;
            camera.targetTexture=texture;
            var view=Rect("Mascot",parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),position,Vector2.one*size).gameObject.AddComponent<RawImage>();
            view.texture=texture;view.raycastTarget=false;
            StartCoroutine(Pose(holder.transform,view.rectTransform,happy));
            return view;
        }
        // Happy: pops in, then hops and sways. Sad: droops, tilted, with a slow sigh.
        private System.Collections.IEnumerator Pose(Transform fish,RectTransform view,bool happy)
        {
            for(float t=0;fish&&view;t+=Time.unscaledDeltaTime)
            {
                float pop=Mathf.Clamp01(t/.35f);view.localScale=Vector3.one*(pop+Mathf.Sin(pop*Mathf.PI)*.18f);
                if(happy)
                {
                    float hop=Mathf.Abs(Mathf.Sin(t*4.2f));
                    fish.localPosition=new Vector3(0,hop*.16f,0);
                    fish.localRotation=Quaternion.Euler(-8*hop,160+Mathf.Sin(t*2.1f)*22,Mathf.Sin(t*4.2f)*8);
                }
                else
                {
                    fish.localPosition=new Vector3(0,-.08f+Mathf.Sin(t*1.3f)*.03f,0);
                    fish.localRotation=Quaternion.Euler(18,200+Mathf.Sin(t*.9f)*6,-24);
                }
                yield return null;
            }
        }
        private void ReleaseHomeMascot()
        {
            if(homeStage){if(Application.isPlaying)Destroy(homeStage);else DestroyImmediate(homeStage);homeStage=null;}
            if(homeTexture){homeTexture.Release();if(Application.isPlaying)Destroy(homeTexture);else DestroyImmediate(homeTexture);homeTexture=null;}
            if(homeMascotSlot)foreach(Transform child in homeMascotSlot)if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
        }
        private void OnDestroy(){ReleaseMascot();ReleaseHomeMascot();}
        // Benchmark home: avatar + coin pill top-left, logo, the fish hero in the middle, a big green "Level N" pill,
        // and a purple bottom bar with Home raised in the centre. Returns the pill's text.
        private Text BuildHome(RectTransform root)
        {
            var avatar=Rect("Avatar",root,new Vector2(0,1),new Vector2(0,1),new Vector2(110,-110),new Vector2(132,132));
            var avatarFace=avatar.gameObject.AddComponent<Image>();avatarFace.raycastTarget=false;
            if(!PackTile(avatarFace,Purple))Face(avatar,Purple,7);
            var fishPicture=Icon(avatar,"Icon",skin?skin.fishIcon:null,Color.white,new Vector2(.5f,.53f),84);Depth(fishPicture);
            var wallet=Rect("Coins",root,new Vector2(0,1),new Vector2(0,1),new Vector2(345,-110),new Vector2(270,88));
            var walletFace=wallet.gameObject.AddComponent<Image>();walletFace.raycastTarget=false;
            if(!PackTile(walletFace,Cream,.7f))Face(wallet,Cream,7);
            if(skin&&skin.coin){var coin=Icon(wallet,"Coin",skin.coin,Color.white,new Vector2(0,.5f),104);Depth(coin);}
            homeCoins=Label(wallet,"0",new Vector2(.58f,.52f),new Vector2(180,80),50,Brown,false);
            var logo=Label(root,"FISH OUT",new Vector2(.5f,.80f),new Vector2(1000,190),150,Cream);Thick(logo,PurpleDeep,6);
            logo.rectTransform.localRotation=Quaternion.Euler(0,0,3);
            var tag=Label(root,"Help every fish swim home!",new Vector2(.5f,.715f),new Vector2(900,70),40,Cream);Thick(tag,PurpleDeep,2.5f);
            homeMascotSlot=Rect("Mascot slot",root,new Vector2(.5f,.47f),new Vector2(.5f,.47f),Vector2.zero,new Vector2(780,780));
            // Play pill: white rim around a green face, the way the benchmark frames its CTA.
            var rim=Rect("Play rim",root,new Vector2(.5f,.215f),new Vector2(.5f,.215f),Vector2.zero,new Vector2(684,182));
            var rimImage=rim.gameObject.AddComponent<Image>();rimImage.raycastTarget=false;
            if(skin&&skin.whitePill){rimImage.sprite=skin.whitePill;rimImage.type=Image.Type.Sliced;rimImage.color=Color.white;rimImage.pixelsPerUnitMultiplier=.9f;}
            else{rimImage.sprite=RoundSprite();rimImage.type=Image.Type.Sliced;}
            var play=Rect("Button Play",rim,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,2),new Vector2(662,162));
            var playButton=Clickable(play,Green,()=>game.StartLevel(game.CurrentLevel),skin?skin.whitePill:null);
            if(skin&&skin.whitePill){var face=(Image)playButton.targetGraphic;face.color=Green;face.pixelsPerUnitMultiplier=1.1f;}
            var text=Label(play,"Level 1",new Vector2(.5f,.55f),new Vector2(600,140),88,Color.white);Thick(text,new Color(.13f,.38f,.08f),4);
            StartCoroutine(Breathe(rim));
            // Bottom bar bleeds below the safe area so the home indicator area stays purple.
            var bar=Rect("Bottom bar",root,new Vector2(0,0),new Vector2(1,0),new Vector2(0,-50),new Vector2(0,500)).gameObject.AddComponent<Image>();bar.color=new Color(.34f,.22f,.68f);
            var line=Rect("Bottom line",root,new Vector2(0,0),new Vector2(1,0),new Vector2(0,200),new Vector2(0,12)).gameObject.AddComponent<Image>();line.color=PurpleRim;line.raycastTarget=false;
            var current=Rect("Nav Home",root,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,97),new Vector2(330,206)).gameObject.AddComponent<Image>();current.color=new Color(.6f,.53f,.98f);current.raycastTarget=false;
            var homePicture=Icon(current.rectTransform,"Icon",skin?skin.homeIcon:null,Color.white,new Vector2(.5f,.68f),118);Depth(homePicture);
            homePicture.rectTransform.anchoredPosition=new Vector2(0,30);
            var homeName=Label(current.rectTransform,"Home",new Vector2(.5f,.22f),new Vector2(300,64),48,Cream);Thick(homeName,PurpleDeep,2.5f);
            NavSlot(root,"Levels",.17f,skin?skin.levelsIcon:null,()=>game.OpenLevelSelect());
            NavSlot(root,"Settings",.83f,skin?skin.settingsIcon:null,()=>game.OpenHomeSettings());
            return text;
        }
        private void NavSlot(RectTransform root,string caption,float x,Sprite icon,UnityAction action)
        {
            var rt=Rect("Nav "+caption,root,new Vector2(x,0),new Vector2(x,0),new Vector2(0,97),new Vector2(300,190));
            var button=Clickable(rt,new Color(1,1,1,0),action);
            var face=(Image)button.targetGraphic;face.color=new Color(1,1,1,0);
            foreach(var effect in rt.GetComponents<Shadow>())effect.enabled=false;
            var picture=Icon(rt,"Icon",icon,Color.white,new Vector2(.5f,.62f),104);Depth(picture);
            var name=Label(rt,caption,new Vector2(.5f,.2f),new Vector2(280,56),36,Cream);Thick(name,PurpleDeep,2f);
        }
        // Slow pulse on the play pill so the call to action reads first.
        private System.Collections.IEnumerator Breathe(RectTransform target)
        {
            for(float t=0;target;t+=Time.unscaledDeltaTime){target.localScale=Vector3.one*(1+Mathf.Sin(t*3f)*.03f);yield return null;}
        }
        private void ReleaseMascot()
        {
            if(mascotStage){if(Application.isPlaying)Destroy(mascotStage);else DestroyImmediate(mascotStage);mascotStage=null;}
            if(mascotTexture){mascotTexture.Release();if(Application.isPlaying)Destroy(mascotTexture);else DestroyImmediate(mascotTexture);mascotTexture=null;}
        }
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
        // Benchmark level grid: five tiles a row; current green, open lavender, locked grey with a padlock; red X closes.
        public void LevelSelect(int count,int current,int unlocked)
        {
            int rows=Mathf.CeilToInt(count/5f);
            var p=Dialog("Select level","",rows*170,false,false);
            for(int i=0;i<count;i++)
            {
                int number=i+1,row=i/5;int inRow=Mathf.Min(5,count-row*5);bool open=number<=unlocked;
                var face=number==current?Green:open?Lavender:new Color(.68f,.66f,.74f);
                var rt=Rect("Level "+number,p,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2((i%5-(inRow-1)*.5f)*166,-235-row*170),new Vector2(148,148));
                var button=Clickable(rt,PurpleRim,()=>game.StartLevel(number));
                if(!PackTile((Image)button.targetGraphic,face))Face(rt,face,7);
                button.interactable=open;
                var text=Label(rt,number.ToString(),new Vector2(.5f,.56f),new Vector2(140,120),64,Cream);Thick(text,new Color(face.r*.42f,face.g*.42f,face.b*.42f),3);
                if(!open&&skin&&skin.lockIcon){var padlock=Icon(rt,"Lock",skin.lockIcon,Color.white,new Vector2(.8f,.22f),60);Depth(padlock);}
            }
            var close=Rect("Button Close",p,new Vector2(1,1),new Vector2(1,1),new Vector2(-20,0),new Vector2(112,112));
            var closeButton=Clickable(close,PurpleRim,()=>CloseModal());
            if(!PackTile((Image)closeButton.targetGraphic,Red))Face(close,Red,7);
            var cross=Label(close,"X",new Vector2(.5f,.54f),new Vector2(100,100),64,Color.white);Thick(cross,new Color(.55f,.12f,.1f),3);
        }
        // Benchmark settings: a full purple screen, a lighter header with the title and a red close button,
        // a card of three round green toggles, then wide pills (Home, Restart, Resume). Positions are from the top, in canvas units.
        public void Settings(bool sound,bool music,bool vibration,bool fromHome=false)
        {
            CloseModal();
            var root=Rect("Settings",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);modal=root.gameObject;
            // Background and header bleed past the safe area so the notch and the home bar stay purple.
            var back=Rect("Background",root,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,600)).gameObject.AddComponent<Image>();back.color=new Color(.18f,.11f,.36f);
            var band=Rect("Header",root,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-69),new Vector2(0,600)).gameObject.AddComponent<Image>();band.color=new Color(.34f,.22f,.68f);band.raycastTarget=false;
            var line=Rect("Header line",root,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-372),new Vector2(0,14)).gameObject.AddComponent<Image>();line.color=new Color(.9f,.87f,1f);line.raycastTarget=false;
            var title=Label(root,"Settings",new Vector2(.5f,1),new Vector2(700,140),96,Cream);title.rectTransform.anchoredPosition=new Vector2(0,-250);Thick(title,PurpleDeep,4);
            var close=Rect("Button Close",root,new Vector2(1,1),new Vector2(1,1),new Vector2(-105,-250),new Vector2(120,120));
            var closeButton=Clickable(close,PurpleRim,()=>{if(fromHome)CloseModal();else game.CloseSettings();});
            if(!PackTile((Image)closeButton.targetGraphic,Red))Face(close,Red,7);
            var cross=Label(close,"X",new Vector2(.5f,.54f),new Vector2(110,110),72,Color.white);Thick(cross,new Color(.55f,.12f,.1f),3);
            var card=Plate(root,"Card",new Vector2(.5f,1),new Vector2(860,340),new Color(.43f,.31f,.84f));card.rectTransform.anchoredPosition=new Vector2(0,-760);
            PackTile(card,new Color(.43f,.31f,.84f),.7f);
            var cardRect=card.rectTransform;
            RoundToggle(cardRect,"Sound",-280,sound,skin?skin.soundIcon:null,skin?skin.soundOffIcon:null,v=>game.SetSound(v));
            RoundToggle(cardRect,"Music",0,music,skin?skin.musicIcon:null,skin?skin.musicOffIcon:null,v=>game.SetMusic(v));
            RoundToggle(cardRect,"Vibration",280,vibration,skin?skin.vibrationIcon:null,null,v=>game.SetVibration(v));
            if(fromHome){WidePill(root,"Back",new Vector2(0,-1030),Green,skin?skin.homeIcon:null,()=>CloseModal());return;}
            WidePill(root,"Home",new Vector2(0,-1030),Red,skin?skin.homeIcon:null,()=>game.ShowHome());
            WidePill(root,"Restart",new Vector2(0,-1198),Lavender,skin?skin.retryIcon:null,()=>{game.CloseSettings();game.ConfirmRetry();});
            WidePill(root,"Resume",new Vector2(0,-1366),Green,skin?skin.playIcon:null,()=>game.CloseSettings());
        }
        private void RoundToggle(RectTransform card,string caption,float x,bool value,Sprite on,Sprite off,UnityAction<bool> changed)
        {
            var name=Label(card,caption,new Vector2(.5f,.5f),new Vector2(270,64),46,Cream);name.rectTransform.anchoredPosition=new Vector2(x,100);Thick(name,PurpleDeep,2.5f);
            var root=Rect(caption+" toggle",card,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(x,-42),new Vector2(172,172));
            var bg=root.gameObject.AddComponent<Image>();
            if(skin&&skin.circle){bg.sprite=skin.circle;bg.preserveAspect=true;}else{bg.sprite=RoundSprite();bg.type=Image.Type.Sliced;}
            var icon=Icon(root,"Icon",on,Color.white,new Vector2(.5f,.53f),92);if(!on)icon.enabled=false;else Depth(icon);
            var toggle=root.gameObject.AddComponent<Toggle>();toggle.targetGraphic=bg;toggle.transition=Selectable.Transition.None;toggle.isOn=value;
            void Show(bool v){bg.color=v?Green:new Color(.56f,.53f,.68f);if(on)icon.sprite=v||!off?on:off;icon.color=new Color(1,1,1,v?1:.8f);}
            Show(value);
            toggle.onValueChanged.AddListener(v=>{game.ConsumeUiInput();Show(v);changed(v);});
        }
        // Benchmark pill: a wide coloured pack button with a pictogram on the left and a cream word with a darker rim.
        private Button WidePill(Transform parent,string caption,Vector2 position,Color color,Sprite icon,UnityAction action,float width=520)
        {
            var rt=Rect("Button "+caption,parent,new Vector2(.5f,1),new Vector2(.5f,1),position,new Vector2(width,132));
            var button=Clickable(rt,color,action,skin?skin.whitePill:null);
            if(skin&&skin.whitePill){var face=(Image)button.targetGraphic;face.color=color;face.pixelsPerUnitMultiplier=1.2f;}
            var label=Label(rt,caption,new Vector2(icon?.56f:.5f,.55f),new Vector2(width-(icon?140:60),100),56,Cream);
            Thick(label,new Color(color.r*.45f,color.g*.45f,color.b*.45f),3);
            if(icon){var image=Icon(rt,"Icon",icon,Color.white,new Vector2(.17f,.55f),76);Depth(image);}
            return button;
        }
        private static readonly Color Lavender=new Color(.6f,.53f,.98f);
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
            if(instructionPlate){bool show=instructions.text.Length>0;if(instructionPlate.gameObject.activeSelf!=show)instructionPlate.gameObject.SetActive(show);}
            if(noticeTime>0) { noticeTime-=Time.unscaledDeltaTime; if(noticeTime<=0)notice.text=""; }
            flashTime=Mathf.Max(0,flashTime-Time.unscaledDeltaTime);
            if(flash) flash.color=new Color(1,.08f,.08f,flashTime*.42f);
        }
    }
}
