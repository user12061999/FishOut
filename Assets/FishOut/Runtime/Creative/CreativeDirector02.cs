using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishOut
{
    // Creative 02 (partner ad, 14/11): red and light-blue octopuses share a configurable grid half and half (red on the
    // left, blue on the right, a ragged border between them). Normal gameplay, no crabs. An octopus on its way out leaves
    // a short black dash trail; it shows a happy emoji once it reaches the sand road and an angry/sad one when it bumps
    // into something. Q shows the lose screen, W the win screen.
    public static class CreativeLevel02
    {
        public const int Blue=6,Red=7,Seed=2401;
        public const int StageNumber=3;
        public struct Config
        {
            public int columns,rows,count,firstRow,lastRow;public float redShare;
            public override string ToString()=>columns+"x"+rows+"/"+count+"/"+firstRow+"-"+lastRow+"/"+redShare.ToString("0.00");
        }
        private static readonly Dictionary<string,int> foundSeeds=new Dictionary<string,int>();
        public static LevelData Build(Config config)
        {
            var key=config.ToString();
            if(foundSeeds.TryGetValue(key,out var known))return TryBuild(known,config);
            for(int attempt=0;attempt<2000;attempt++)
            {
                var level=TryBuild(Seed+attempt,config);
                if(level!=null){foundSeeds[key]=Seed+attempt;return level;}
            }
            throw new System.InvalidOperationException("Creative 02: no clearable layout for "+key);
        }
        // Rows are counted 1..rows from the bottom of the whole grid, sand road included, so octopuses use rows 2..rows-1.
        public static List<Vector2Int> Cells(LevelData level,List<Vector2Int> water,Config config)
        {
            int low=Mathf.Max(config.firstRow,2)-1,high=Mathf.Min(config.lastRow,level.height-1)-1;
            var cells=new List<Vector2Int>();
            foreach(var cell in water)if(cell.y>=low&&cell.y<=high)cells.Add(cell);
            return cells;
        }
        private static LevelData TryBuild(int seed,Config config)
        {
            var level=new LevelData{number=StageNumber,width=Mathf.Max(5,config.columns),height=Mathf.Max(7,config.rows),showcase=true};
            PaintedBackdrop.Roads(level,out var ring,out var road,out level.gate);
            level.channelLines.Add(ring);level.channelLines.Add(road);
            var water=new List<Vector2Int>();
            for(int y=0;y<level.height;y++)for(int x=0;x<level.width;x++)
            {
                var cell=new Vector2Int(x,y);
                if(!ReefWaterDomain.ContainsWholeCell(level,cell)||!PaintedBackdrop.InsideCut(level,cell,.3f))level.channelCells.Add(cell);
                else water.Add(cell);
            }
            level.channelCells.Add(level.gate);
            var random=new System.Random(seed);
            var cells=Cells(level,water,config);
            for(int i=cells.Count-1;i>0;i--){int j=random.Next(i+1);var t=cells[i];cells[i]=cells[j];cells[j]=t;}
            int count=Mathf.Clamp(config.count,1,cells.Count);
            var picked=cells.GetRange(0,count);
            // Built backwards: the deepest cells are filled first, each piece taking a facing whose way out is still clear,
            // so clearing simply runs in reverse and every layout is solvable.
            var depth=new Dictionary<Vector2Int,int>();var keys=new Dictionary<Vector2Int,int>();
            foreach(var cell in picked){depth[cell]=Depth(level,cell);keys[cell]=random.Next();}
            picked.Sort((a,b)=>depth[a]!=depth[b]?depth[b].CompareTo(depth[a]):keys[a].CompareTo(keys[b]));
            var occupied=new HashSet<Vector2Int>();var facings=new Dictionary<Vector2Int,Facing>();
            var options=new List<Facing>();
            foreach(var cell in picked)
            {
                options.Clear();
                for(int f=0;f<4;f++)if(Escapes(level,new FishData{cell=cell,facing=(Facing)f},occupied))options.Add((Facing)f);
                if(options.Count==0)return null;
                facings[cell]=options[random.Next(options.Count)];occupied.Add(cell);
            }
            // Red fills the left half, blue the right; each row shifts the border by up to a cell so it reads ragged.
            int redCount=Mathf.RoundToInt(count*Mathf.Clamp01(config.redShare));
            var shift=new Dictionary<int,float>();
            foreach(var cell in picked)if(!shift.ContainsKey(cell.y))shift[cell.y]=random.Next(-1,2)+(float)random.NextDouble()*.4f;
            var byColumn=new List<Vector2Int>(picked);
            byColumn.Sort((a,b)=>{float ka=a.x+shift[a.y],kb=b.x+shift[b.y];return ka!=kb?ka.CompareTo(kb):a.y.CompareTo(b.y);});
            var red=new HashSet<Vector2Int>(byColumn.GetRange(0,redCount));
            picked.Sort((a,b)=>a.y!=b.y?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            foreach(var cell in picked)
                level.fish.Add(new FishData{id=level.fish.Count,cell=cell,facing=facings[cell],appearance=red.Contains(cell)?Red:Blue});
            var facing=new int[4];foreach(var f in level.fish)facing[(int)f.facing]++;
            if(level.fish.Count>=8)foreach(var n in facing)if(n>level.fish.Count*.4f)return null;
            return CreativeLevel01.ClearsByEscapes(level)?level:null;
        }
        private static int Depth(LevelData level,Vector2Int cell)
        {
            int best=int.MaxValue;
            for(int f=0;f<4;f++)
            {
                var step=BoardState.Direction((Facing)f);int n=0;
                for(var c=cell+step;!(level.IsChannel(c)||c.x<0||c.y<0||c.x>=level.width||c.y>=level.height);c+=step)n++;
                best=Mathf.Min(best,n);
            }
            return best;
        }
        private static bool Escapes(LevelData level,FishData fish,HashSet<Vector2Int> occupied)
        {
            var step=BoardState.Direction(fish.facing);
            for(var cell=fish.cell+step;;cell+=step)
            {
                if(level.IsChannel(cell)||cell.x<0||cell.y<0||cell.x>=level.width||cell.y>=level.height)return true;
                if(occupied.Contains(cell))return false;
            }
        }
    }

    public sealed class CreativeDirector02 : MonoBehaviour
    {
        [Header("Board")]
        [Tooltip("Grid size, sand road included (columns x rows). 8 x 10 is the campaign board.")]
        public int gridColumns=8,gridRows=10;
        [Tooltip("How many octopuses start on the board (capped by the free cells in the chosen rows).")]
        public int octopusCount=36;
        [Tooltip("Lowest and highest rows that hold octopuses, counted from the bottom of the grid. Row 1 and the last row are road, so octopuses stay within rows 2..rows-1.")]
        public int firstRow=2,lastRow=9;
        [Tooltip("Share of red octopuses (left side); the rest are light blue (right side).")]
        [Range(0,1)]public float redShare=.5f;
        [Tooltip("How much lighter the blue octopus is drawn (0 = the pack's own blue).")]
        [Range(0,1)]public float blueLighten=.35f;

        [Header("Dash trail")]
        public Color trailColor=Color.black;
        [Tooltip("Seconds a trail point takes to fade out.")]
        public float trailTime=.4f;
        [Tooltip("Trail width in grid cells at the octopus.")]
        public float trailWidth=.55f;

        [Header("Emoji")]
        [Tooltip("Shown over an octopus once it reaches the sand road. One is picked at random each time.")]
        public Sprite[] happyEmojis;
        [Tooltip("Shown over an octopus that bumps into something. One is picked at random each time.")]
        public Sprite[] angryEmojis;
        [Tooltip("Shown over the crying octopus on the lose screen.")]
        public Sprite loseEmoji;
        [Tooltip("Emoji size in grid cells.")]
        public float emojiSize=.8f;
        [Tooltip("How long an emoji stays, in seconds.")]
        public float emojiSeconds=1f;

        [Header("Fail screen art")]
        [Tooltip("Title shown instead of YOU LOSE.")]
        public Sprite failTitle;
        [Tooltip("Picture used for the Try Again button.")]
        public Sprite tryAgainButton;

        private FishOutGame game;
        private Canvas hudCanvas;
        private BoardState dressedBoard;
        private bool ended,loseStaged;
        private int savedLevel,savedUnlocked;
        private Material lightBlue;
        private readonly HashSet<int> cheered=new HashSet<int>();
        private readonly Dictionary<int,int> bumpMarks=new Dictionary<int,int>();
        private readonly Dictionary<int,TrailRenderer> trails=new Dictionary<int,TrailRenderer>();
        private Material trailMaterial;

        private void Awake()
        {
            savedLevel=PlayerPrefs.GetInt("FishOut.Level",1);savedUnlocked=PlayerPrefs.GetInt("FishOut.Unlocked",1);
            FishOutGame.CreativeLevel=()=>CreativeLevel02.Build(new CreativeLevel02.Config{columns=gridColumns,rows=gridRows,count=octopusCount,firstRow=firstRow,lastRow=lastRow,redShare=redShare});
        }
        private void OnDestroy()
        {
            FishOutGame.CreativeLevel=null;Time.timeScale=1;
            PlayerPrefs.SetInt("FishOut.Level",savedLevel);PlayerPrefs.SetInt("FishOut.Unlocked",savedUnlocked);PlayerPrefs.Save();
            if(lightBlue)Destroy(lightBlue);if(trailMaterial)Destroy(trailMaterial);
        }
        private IEnumerator Start()
        {
            game=FindFirstObjectByType<FishOutGame>();
            while(game==null||game.Hud==null){yield return null;if(!game)game=FindFirstObjectByType<FishOutGame>();}
            yield return null;
            hudCanvas=game.Hud.GetComponent<Canvas>();
            Restart();
        }
        private void Restart()
        {
            Time.timeScale=1;ended=false;loseStaged=false;
            game.Hud.CloseModal();
            game.StartLevel(CreativeLevel02.StageNumber);
        }
        private void Update()
        {
            if(game==null||game.Board==null)return;
            bool modal=game.Hud.modal!=null;
            if(hudCanvas&&hudCanvas.enabled!=modal)hudCanvas.enabled=modal;
            if(!modal&&Time.timeScale==0)Time.timeScale=1;
            if(game.CurrentLevel!=CreativeLevel02.StageNumber&&!modal)typeof(FishOutGame).GetProperty("CurrentLevel").GetSetMethod(true).Invoke(game,new object[]{CreativeLevel02.StageNumber});
            if(dressedBoard!=game.Board&&game.World)Dress();
            // The game's own lose card (no way out) is swapped for ours; it already played its jingle.
            if(modal&&!loseStaged&&game.Hud.modal.name=="Stage failed")ShowLose(false);
            if(KeyDown('Q'))ShowLose(true);
            else if(KeyDown('W'))ShowWin();
            Watch();
        }
        private static bool KeyDown(char key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard=Keyboard.current;if(keyboard==null)return false;
            return key=='Q'?keyboard.qKey.wasPressedThisFrame:keyboard.wKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key=='Q'?KeyCode.Q:KeyCode.W);
#endif
        }

        // A new board (start or retry): lighter blue bodies and a trail on every octopus.
        private void Dress()
        {
            dressedBoard=game.Board;cheered.Clear();bumpMarks.Clear();trails.Clear();
            var appearance=new Dictionary<int,int>();
            foreach(var fish in game.Board.Level.fish)appearance[fish.id]=fish.appearance;
            foreach(var pair in game.World.actors)
            {
                var actor=pair.Value;if(!actor)continue;
                if(appearance.TryGetValue(pair.Key,out var look)&&look==CreativeLevel02.Blue)Lighten(actor);
                trails[pair.Key]=AddTrail(actor);
            }
        }
        private void Lighten(FishActor actor)
        {
            foreach(var r in actor.GetComponentsInChildren<Renderer>(true))
            {
                var source=r.sharedMaterial;
                if(!source||!source.name.StartsWith("Octopus Blue"))continue;
                if(!lightBlue)
                {
                    // Emission with the body's own texture lifts the blue toward a pastel without losing its shading.
                    lightBlue=new Material(source){name="Octopus Light Blue"};
                    lightBlue.EnableKeyword("_EMISSION");
                    lightBlue.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                    lightBlue.SetTexture("_EmissionMap",source.mainTexture);
                }
                lightBlue.SetColor("_EmissionColor",Color.white*blueLighten);
                r.sharedMaterial=lightBlue;
            }
        }
        private TrailRenderer AddTrail(FishActor actor)
        {
            if(!trailMaterial)
            {
                var shader=Shader.Find("Sprites/Default");
                if(!shader)shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                trailMaterial=new Material(shader){name="Dash trail"};
            }
            var holder=new GameObject("Dash trail").transform;holder.SetParent(actor.transform,false);
            holder.localPosition=Vector3.up*.08f;
            var trail=holder.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial=trailMaterial;
            trail.emitting=false;trail.minVertexDistance=.03f;trail.numCapVertices=4;
            trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
            trail.alignment=LineAlignment.View;
            return trail;
        }
        // Trails follow octopuses on the way out; emojis pop on reaching the road and on a bump.
        private void Watch()
        {
            if(!game.World)return;
            foreach(var pair in game.World.actors)
            {
                var actor=pair.Value;if(!actor||actor.Mover==null)continue;
                bool leaving=(actor.Mover.IsMoving||actor.Mover.OnRoad)&&!actor.Mover.InCave;
                if(trails.TryGetValue(pair.Key,out var trail)&&trail)
                {
                    float cell=PaintedBackdrop.CellWorldSize(actor.transform.position);
                    trail.time=trailTime;trail.widthMultiplier=trailWidth*cell;
                    trail.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(1,0));
                    var clear=trailColor;clear.a=0;
                    trail.colorGradient=new Gradient{colorKeys=new[]{new GradientColorKey(trailColor,0),new GradientColorKey(trailColor,1)},alphaKeys=new[]{new GradientAlphaKey(trailColor.a*.85f,0),new GradientAlphaKey(0,1)}};
                    trail.emitting=leaving;
                }
                if(actor.Mover.OnRoad&&!actor.Mover.InCave&&cheered.Add(pair.Key))Pop(actor.transform,happyEmojis);
                // The game hangs fresh "Dizzy stars" on an octopus each time it bumps into something.
                var stars=actor.transform.Find("Dizzy stars");
                int mark=stars?stars.GetInstanceID():0;
                bumpMarks.TryGetValue(pair.Key,out var seen);
                if(mark!=0&&mark!=seen)Pop(actor.transform,angryEmojis);
                bumpMarks[pair.Key]=mark;
            }
        }
        private void Pop(Transform target,Sprite[] list)
        {
            if(list==null||list.Length==0)return;
            var sprite=list[Random.Range(0,list.Length)];
            if(sprite)StartCoroutine(Emoji(target,sprite));
        }
        private IEnumerator Emoji(Transform target,Sprite sprite)
        {
            var renderer=new GameObject("Emoji "+sprite.name).AddComponent<SpriteRenderer>();
            renderer.sprite=sprite;renderer.sortingOrder=50;
            var view=game.World.transform;renderer.transform.SetParent(view,true);
            var anchor=target.position;
            float cell=PaintedBackdrop.CellWorldSize(anchor);
            float size=emojiSize*cell/Mathf.Max(.01f,sprite.bounds.size.x);
            var camera=Camera.main;
            for(float t=0;t<emojiSeconds;t+=Time.unscaledDeltaTime)
            {
                if(target)anchor=target.position;
                float k=t/emojiSeconds;
                // Pop in with an overshoot, float up a little, fade over the last quarter.
                float pop=t<.12f?Mathf.Lerp(0,1.25f,t/.12f):t<.22f?Mathf.Lerp(1.25f,1,(t-.12f)/.1f):1;
                renderer.transform.position=anchor+Vector3.up*(.9f+.25f*k)*cell;
                renderer.transform.localScale=Vector3.one*size*pop;
                if(camera)renderer.transform.rotation=camera.transform.rotation;
                renderer.color=new Color(1,1,1,k<.75f?1:1-(k-.75f)/.25f);
                yield return null;
            }
            Destroy(renderer.gameObject);
        }

        // The game's lose card restaged: dark veil, a crying red octopus with a crying emoji, a pulsing Try Again.
        private void ShowLose(bool playSound)
        {
            if(loseStaged)return;
            loseStaged=true;ended=true;Time.timeScale=0;
            hudCanvas.enabled=true;game.Hud.Toast("",0);
            if(playSound)PlaySound("fail");
            if(!game.Hud.modal||game.Hud.modal.name!="Stage failed")game.Hud.Lose("",Restart,Restart);
            var card=game.Hud.modal;
            if(card)
            {
                var veil=card.GetComponent<Image>();if(veil)veil.color=new Color(.02f,.03f,.12f,.86f);
                var home=card.transform.Find("Button Home");if(home)home.gameObject.SetActive(false);
                var again=card.transform.Find("Button Try Again") as RectTransform;
                if(again&&tryAgainButton)
                {
                    // The button keeps its click area; only its look is swapped for the art.
                    foreach(var graphic in again.GetComponentsInChildren<Graphic>(true))
                        if(graphic.transform==again)graphic.color=new Color(1,1,1,0);else graphic.enabled=false;
                    again.sizeDelta=new Vector2(640,214);
                    Art(again,"Try again art",tryAgainButton,Vector2.zero,640);
                }
                if(again)StartCoroutine(Pulse(again));
                var title=card.transform.Find("Title YOU LOSE") as RectTransform;
                if(title&&failTitle)
                {
                    // Art rides on the title so the HUD's reveal animation still pops it in.
                    var text=title.GetComponent<Text>();if(text)text.enabled=false;
                    Art(title,"Fail title",failTitle,new Vector2(0,-20),760);
                }
                var mascot=card.transform.Find("Mascot");if(mascot)((RectTransform)mascot).sizeDelta=Vector2.one*820;
            }
            var stage=GameObject.Find("Mascot stage");
            if(!stage)return;
            var original=stage.transform.Find("Mascot");if(original)original.gameObject.SetActive(false);
            var camera=stage.GetComponentInChildren<Camera>();if(camera){camera.fieldOfView=34;camera.transform.localPosition=new Vector3(0,.55f,-3.6f);}
            var octopus=Pose(game.fishPrefabs[CreativeLevel02.Red],stage.transform,1.05f,CreativeLevel02.Red/3);
            octopus.name="Crying octopus pose";
            // The red octopus's own face scowls; the blue one's softer face reads as tearful.
            Material softFace=null;
            foreach(var r in game.fishPrefabs[CreativeLevel02.Blue].GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial&&r.sharedMaterial.name.Contains("Face"))softFace=r.sharedMaterial;
            if(softFace)foreach(var r in octopus.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial&&r.sharedMaterial.name.Contains("Face"))r.sharedMaterial=softFace;
            foreach(var animator in octopus.GetComponentsInChildren<Animator>())animator.updateMode=AnimatorUpdateMode.UnscaledTime;
            Transform emoji=null;
            var sprite=loseEmoji?loseEmoji:(angryEmojis!=null&&angryEmojis.Length>0?angryEmojis[0]:null);
            if(sprite)
            {
                var r=new GameObject("Crying emoji").AddComponent<SpriteRenderer>();r.sprite=sprite;
                r.transform.SetParent(stage.transform,false);r.transform.localScale=Vector3.one*.5f/Mathf.Max(.01f,sprite.bounds.size.x);
                emoji=r.transform;
            }
            StartCoroutine(Sob(octopus,emoji));
        }
        private static Image Art(RectTransform parent,string name,Sprite sprite,Vector2 position,float width)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.anchoredPosition=position;
            rect.sizeDelta=new Vector2(width,width*sprite.rect.height/Mathf.Max(1,sprite.rect.width));
            var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            return image;
        }
        private void ShowWin()
        {
            ended=true;Time.timeScale=1;
            hudCanvas.enabled=true;game.Hud.Toast("",0);
            PlaySound("win");
            game.Hud.StageClear(3,150,"","NEXT",Restart);
        }
        private void PlaySound(string clip)
        {
            var field=typeof(FishOutGame).GetField("audioPlayer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var player=field!=null?field.GetValue(game) as FishOutAudio:null;
            if(player)player.Play(clip);else Debug.LogWarning("Creative 02: no audio player for "+clip);
        }
        private Transform Pose(GameObject prefab,Transform parent,float size,int species)
        {
            var holder=new GameObject(prefab.name+" pose").transform;holder.SetParent(parent,false);
            var instance=Instantiate(prefab,holder);
            foreach(var c in instance.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var animator in instance.GetComponentsInChildren<Animator>())
            {
                animator.applyRootMotion=false;
                if(game.characterAnimations!=null&&species<game.characterAnimations.Length)animator.runtimeAnimatorController=game.characterAnimations[species];
            }
            var renderers=instance.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float factor=size/Mathf.Max(bounds.size.x,bounds.size.z,.01f);
                instance.transform.localScale*=factor;
                var center=holder.InverseTransformPoint(bounds.center);
                instance.transform.localPosition=new Vector3(-center.x*factor,(-center.y+bounds.size.y*.5f)*factor,-center.z*factor);
            }
            return holder;
        }
        // Sobbing: head bowed toward the viewer, shoulders shaking, the emoji bobbing beside its head.
        private IEnumerator Sob(Transform octopus,Transform emoji)
        {
            for(float t=0;octopus;t+=Time.unscaledDeltaTime)
            {
                float sob=Mathf.Abs(Mathf.Sin(t*7f));
                octopus.localPosition=new Vector3(0,-.3f+sob*.04f,.2f);
                octopus.localRotation=Quaternion.Euler(14+sob*4,180+Mathf.Sin(t*2f)*8,Mathf.Sin(t*14f)*3);
                octopus.localScale=new Vector3(1+sob*.04f,1-sob*.05f,1);
                // Below the FAIL title (partner note: nothing may overlap it), beside the octopus's head and in front of it
                // (nearer the camera than the bowed head, so smaller to look the same size).
                if(emoji)emoji.localPosition=new Vector3(.4f,.51f+Mathf.Sin(t*4f)*.03f,-.8f);
                yield return null;
            }
        }
        private IEnumerator Pulse(Transform target)
        {
            for(float t=0;target;t+=Time.unscaledDeltaTime){target.localScale=Vector3.one*(1+Mathf.Sin(t*5f)*.07f);yield return null;}
        }
    }
}
