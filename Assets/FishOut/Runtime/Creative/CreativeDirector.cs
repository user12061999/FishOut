using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishOut
{
    // Creative 01 (partner ad, 13/11): a configurable grid (sand road included) with fish of two kinds in a band of rows.
    // Two crabs walk back and forth across grid rows 2 and rows-1, which hold no fish. A moving fish that touches a
    // crab loses the stage at once. Q shows the lose screen, W the win screen.
    public static class CreativeLevel01
    {
        public const int Blue=0,Orange=2,Seed=1301;
        public const int StageNumber=3;
        public struct Config
        {
            public int columns,rows,fishCount,firstRow,lastRow;public float blueShare;
            public override string ToString()=>columns+"x"+rows+"/"+fishCount+"/"+firstRow+"-"+lastRow+"/"+blueShare.ToString("0.00");
        }
        // The seed search runs once per config; a retry rebuilds the same board from the found seed.
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
            throw new System.InvalidOperationException("Creative 01: no clearable layout for "+key);
        }
        // Rows are counted 1..rows from the bottom of the whole grid, sand road included: row 1 and the last row are road,
        // the crabs walk rows 2 and rows-1, so fish may only use rows 3..rows-2.
        public static int CrabRowLow=>1;
        public static int CrabRowHigh(LevelData level)=>level.height-2;
        public static List<Vector2Int> FishCells(LevelData level,List<Vector2Int> water,Config config)
        {
            int low=Mathf.Max(config.firstRow,3)-1,high=Mathf.Min(config.lastRow,level.height-2)-1;
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
            var cells=FishCells(level,water,config);
            for(int i=cells.Count-1;i>0;i--){int j=random.Next(i+1);var t=cells[i];cells[i]=cells[j];cells[j]=t;}
            int count=Mathf.Clamp(config.fishCount,1,cells.Count);
            var picked=cells.GetRange(0,count);
            // Built backwards: the deepest cells are filled first, and each fish takes a facing whose way out is still
            // clear. Clearing then simply runs in reverse (the last fish placed leaves first), so every layout is solvable.
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
            picked.Sort((a,b)=>a.y!=b.y?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            // Blue goes to a few whole patches, orange everywhere else.
            int blueCount=Mathf.RoundToInt(count*Mathf.Clamp01(config.blueShare));
            var byPatch=new List<Vector2Int>(picked);
            int patchSeed=random.Next(1000);
            byPatch.Sort((a,b)=>Patch(a,patchSeed).CompareTo(Patch(b,patchSeed)));
            var blue=new HashSet<Vector2Int>(byPatch.GetRange(0,blueCount));
            foreach(var cell in picked)
                level.fish.Add(new FishData{id=level.fish.Count,cell=cell,facing=facings[cell],appearance=blue.Contains(cell)?Blue:Orange});
            var facing=new int[4];foreach(var f in level.fish)facing[(int)f.facing]++;
            if(level.fish.Count>=8)foreach(var n in facing)if(n>level.fish.Count*.4f)return null;
            return ClearsByEscapes(level)?level:null;
        }
        // Steps from a cell to the road along its nearest straight way out.
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
        private static int Patch(Vector2Int cell,int seed)=>((cell.x/2)*7+(cell.y/2)*13+seed)%17;
        // Strict check: the board empties by sending out, one by one, a fish whose way out is clear. Letting a fish
        // out never blocks another, so trying them in any order is exact. No Rescue, no search budget.
        public static bool ClearsByEscapes(LevelData level)
        {
            var left=new List<FishData>(level.fish);
            var occupied=new HashSet<Vector2Int>();foreach(var f in left)occupied.Add(f.cell);
            int first=0;foreach(var f in left)if(Escapes(level,f,occupied))first++;
            if(first<3)return false;
            for(bool progress=true;progress&&left.Count>0;)
            {
                progress=false;
                for(int i=0;i<left.Count;i++)
                    if(Escapes(level,left[i],occupied)){occupied.Remove(left[i].cell);left.RemoveAt(i);progress=true;break;}
            }
            return left.Count==0;
        }
        // Same rule as BoardState: a fish leaves when nothing stands between it and the road or the grid edge.
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

    public sealed class CreativeDirector : MonoBehaviour
    {
        [Header("Board")]
        [Tooltip("Grid size, sand road included (columns x rows). 8 x 10 is the campaign board.")]
        public int gridColumns=8,gridRows=10;
        [Tooltip("How many fish start on the board (capped by the free cells in the fish rows).")]
        public int fishCount=24;
        [Tooltip("Lowest and highest rows that hold fish, counted from the bottom of the grid. Rows 1 and the last are road, the crabs walk rows 2 and rows-1, so fish stay within rows 3..rows-2.")]
        public int firstFishRow=4,lastFishRow=7;
        [Tooltip("Share of blue fish; the rest are orange.")]
        [Range(0,1)]public float blueShare=.35f;
        [Header("Crabs")]
        [Tooltip("World yaw of the crab models so that they look at the viewer while walking sideways.")]
        public float crabFaceYaw=180;
        [Tooltip("Patrol speed along the road, in grid cells per second.")]
        public float patrolSpeed=1.1f;
        [Tooltip("Pause at each end of the patrol, in seconds.")]
        public float patrolPause=.35f;
        [Tooltip("Crab size in grid cells.")]
        public float crabSize=1.25f;
        [Tooltip("A fish closer than this to a crab (in cells) is caught.")]
        public float catchRadius=.6f;
        private FishOutGame game;
        private Canvas hudCanvas;
        private readonly List<Patrol> crabs=new List<Patrol>();
        private BoardState patrolBoard;
        private bool ended;
        private int savedLevel,savedUnlocked;

        private sealed class Patrol
        {
            public Transform root,model;
            public Vector2 from,to;
            public float phase;
            public Vector3 position;
        }

        private void Awake()
        {
            savedLevel=PlayerPrefs.GetInt("FishOut.Level",1);savedUnlocked=PlayerPrefs.GetInt("FishOut.Unlocked",1);
            FishOutGame.CreativeLevel=()=>CreativeLevel01.Build(new CreativeLevel01.Config{columns=gridColumns,rows=gridRows,fishCount=fishCount,firstRow=firstFishRow,lastRow=lastFishRow,blueShare=blueShare});
        }
        private void OnDestroy()
        {
            FishOutGame.CreativeLevel=null;Time.timeScale=1;
            PlayerPrefs.SetInt("FishOut.Level",savedLevel);PlayerPrefs.SetInt("FishOut.Unlocked",savedUnlocked);PlayerPrefs.Save();
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
            Time.timeScale=1;ended=false;
            game.Hud.CloseModal();
            game.StartLevel(CreativeLevel01.StageNumber);
        }
        private void Update()
        {
            if(game==null||game.Board==null)return;
            // The ad has no HUD: the canvas shows only for an end screen (ours, or the game's own clear / no-moves card).
            bool modal=game.Hud.modal!=null;
            if(hudCanvas&&hudCanvas.enabled!=modal)hudCanvas.enabled=modal;
            if(!modal&&Time.timeScale==0)Time.timeScale=1;
            // Every clear replays the same stage instead of moving on through the campaign.
            if(game.CurrentLevel!=CreativeLevel01.StageNumber&&!modal)typeof(FishOutGame).GetProperty("CurrentLevel").GetSetMethod(true).Invoke(game,new object[]{CreativeLevel01.StageNumber});
            if(patrolBoard!=game.Board)SpawnCrabs();
            MoveCrabs();
            if(KeyDown('Q'))ShowLose();
            else if(KeyDown('W'))ShowWin();
            if(!ended&&!modal)CatchFish();
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
        // A new board (start or retry) gets its two crabs, walking grid rows 2 and rows-1 across the whole board.
        private void SpawnCrabs()
        {
            foreach(var crab in crabs)if(crab.root)Destroy(crab.root.gameObject);
            crabs.Clear();patrolBoard=game.Board;
            var level=game.Board.Level;
            float left=1,right=level.width-2;
            crabs.Add(MakeCrab(new Vector2(left,CreativeLevel01.CrabRowLow),new Vector2(right,CreativeLevel01.CrabRowLow),0));
            crabs.Add(MakeCrab(new Vector2(right,CreativeLevel01.CrabRowHigh(level)),new Vector2(left,CreativeLevel01.CrabRowHigh(level)),.5f));
        }
        private Patrol MakeCrab(Vector2 from,Vector2 to,float phase)
        {
            var root=new GameObject("Patrol crab").transform;root.SetParent(game.World.transform,false);
            var model=Pose(game.crabPrefab,root,crabSize,3);
            foreach(var animator in model.GetComponentsInChildren<Animator>())animator.SetBool("Swimming",true);
            return new Patrol{root=root,model=model,from=from,to=to,phase=phase};
        }
        // Back and forth along its stretch, easing at each end, always facing the viewer (crabs walk sideways).
        private void MoveCrabs()
        {
            if(crabs.Count==0)return;
            var level=game.Board.Level;
            foreach(var crab in crabs)
            {
                if(!crab.root)continue;
                float length=Vector2.Distance(crab.from,crab.to),walk=length/Mathf.Max(.01f,patrolSpeed);
                float cycle=2*(walk+patrolPause);
                float t=Mathf.Repeat(Time.time/cycle+crab.phase,1)*cycle;
                float k=t<walk?t/walk:t<walk+patrolPause?1:t<2*walk+patrolPause?1-(t-walk-patrolPause)/walk:0;
                var grid=Vector2.Lerp(crab.from,crab.to,Mathf.SmoothStep(0,1,k));
                var world=ConveyorNetwork.World(level,grid,.1f);
                crab.position=world;crab.root.position=world;
                crab.root.localScale=Vector3.one*(PaintedBackdrop.Warps(level)?PaintedBackdrop.ScaleAt(world):1);
                crab.model.rotation=Quaternion.Euler(0,crabFaceYaw,0);
                if(ended)crab.model.localPosition=Vector3.up*Mathf.Abs(Mathf.Sin(Time.unscaledTime*8))*.25f;
            }
        }
                private void CatchFish()
        {
            foreach(var actor in game.World.actors.Values)
            {
                // Only a fish on the move can run into a crab; a resting fish never sits on a crab row.
                if(!actor||actor.Mover==null||!(actor.Mover.IsMoving||actor.Mover.OnRoad)||actor.Mover.InCave)continue;
                foreach(var crab in crabs)
                {
                    if(!crab.root)continue;
                    float cell=PaintedBackdrop.CellWorldSize(crab.position);
                    var a=actor.transform.position;var b=crab.position;a.y=b.y=0;
                    if(Vector3.Distance(a,b)<catchRadius*cell){StartCoroutine(Caught(actor,crab));return;}
                }
            }
        }
        private IEnumerator Caught(FishActor fish,Patrol crab)
        {
            ended=true;
            game.World.PlayEffect("CrabHit",(fish.transform.position+crab.position)*.5f+Vector3.up*.3f);
            fish.Dizzy();
            // A short slow motion on the hit, then everything freezes under the lose screen.
            Time.timeScale=.2f;
            yield return new WaitForSecondsRealtime(.6f);
            ShowLose();
        }
        // The game's own lose card, restaged: dark veil over the board, a crab cheering over a knocked-out fish, a pulsing Try Again.
        private void ShowLose()
        {
            if(game.Hud.modal&&game.Hud.modal.name=="Stage failed")return;
            ended=true;Time.timeScale=0;
            hudCanvas.enabled=true;game.Hud.Toast("",0);
            PlaySound("fail");
            game.Hud.Lose("",Restart,Restart);
            var card=game.Hud.modal;
            if(card)
            {
                var veil=card.GetComponent<Image>();if(veil)veil.color=new Color(.02f,.03f,.12f,.86f);
                var home=card.transform.Find("Button Home");if(home)home.gameObject.SetActive(false);
                var again=card.transform.Find("Button Try Again");if(again)StartCoroutine(Pulse(again));
                var mascot=card.transform.Find("Mascot");if(mascot)((RectTransform)mascot).sizeDelta=Vector2.one*820;
            }
            var stage=GameObject.Find("Mascot stage");
            if(!stage)return;
            var original=stage.transform.Find("Mascot");if(original)original.gameObject.SetActive(false);
            var camera=stage.GetComponentInChildren<Camera>();if(camera){camera.fieldOfView=34;camera.transform.localPosition=new Vector3(0,.55f,-3.6f);}
            var winner=Pose(game.crabPrefab,stage.transform,1.35f,3);winner.localPosition=new Vector3(-.35f,0,.3f);
            foreach(var animator in winner.GetComponentsInChildren<Animator>())animator.updateMode=AnimatorUpdateMode.UnscaledTime;
            var loser=Pose(game.fishPrefabs[CreativeLevel01.Orange],stage.transform,.6f,0);loser.localPosition=new Vector3(.36f,-.12f,.1f);
            foreach(var animator in loser.GetComponentsInChildren<Animator>())animator.enabled=false;
            loser.localRotation=KoPose(0);
            var stars=new Transform[3];
            for(int i=0;i<stars.Length;i++)
            {
                var star=new GameObject("KO star").AddComponent<SpriteRenderer>();star.sprite=FishOutIcons.Star;star.color=new Color(1f,.9f,.25f);
                star.transform.SetParent(stage.transform,false);star.transform.localScale=Vector3.one*.22f/Mathf.Max(.01f,star.sprite.bounds.size.x);stars[i]=star.transform;
            }
            StartCoroutine(Cheer(winner,loser,stars));
        }
        private void ShowWin()
        {
            ended=true;Time.timeScale=1;
            hudCanvas.enabled=true;game.Hud.Toast("",0);
            PlaySound("win");
            game.Hud.StageClear(3,150,"","NEXT",Restart);
        }
        // The popups are opened directly on the HUD, so the jingle the game plays on its own win/fail is played here.
        private void PlaySound(string clip)
        {
            var field=typeof(FishOutGame).GetField("audioPlayer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var player=field!=null?field.GetValue(game) as FishOutAudio:null;
            if(player)player.Play(clip);else Debug.LogWarning("Creative 01: no audio player for "+clip);
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
        private IEnumerator Cheer(Transform crab,Transform fish,Transform[] stars)
        {
            for(float t=0;crab&&fish;t+=Time.unscaledDeltaTime)
            {
                // Dizzy stars ring the knocked-out fish.
                for(int i=0;i<stars.Length;i++)
                {
                    float a=t*4f+i*Mathf.PI*2/stars.Length;
                    stars[i].localPosition=fish.localPosition+new Vector3(Mathf.Cos(a)*.3f,.3f+Mathf.Sin(a*2)*.04f,Mathf.Sin(a)*.15f);
                }
                float hop=Mathf.Abs(Mathf.Sin(t*5f));
                crab.localPosition=new Vector3(-.35f,hop*.14f,.3f);
                crab.localRotation=Quaternion.Euler(-6*hop,crabFaceYaw+Mathf.Sin(t*2.5f)*14,Mathf.Sin(t*5f)*7);
                fish.localRotation=KoPose(t);
                yield return null;
            }
        }
        // Knocked out: lying on its side, belly toward the crab, with a faint twitch.
        private static Quaternion KoPose(float t)=>Quaternion.Euler(Mathf.Sin(t*9f)*3,-120,80);
        private IEnumerator Pulse(Transform target)
        {
            for(float t=0;target;t+=Time.unscaledDeltaTime){target.localScale=Vector3.one*(1+Mathf.Sin(t*5f)*.07f);yield return null;}
        }
    }
}
