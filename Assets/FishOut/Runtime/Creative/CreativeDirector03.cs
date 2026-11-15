using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishOut
{
    // Creative 03 (partner ad, 14/11): two big crabs stand still in the middle of the board, ringed by fish. The fish
    // right next to them face the crabs, so the stage is built to be lost: every hit counts a crab's bomb down from 3,
    // and at 0 it blows up over the whole screen and the lose card (a prefab) opens. Q shows the lose card, W the win card.
    public static class CreativeLevel03
    {
        public const int Seed=3301;
        public const int StageNumber=3;
        public struct Config
        {
            public int columns,rows,fishCount,firstRow,lastRow;public int[] looks;
        }
        public static LevelData Build(Config config)
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
            var waterSet=new HashSet<Vector2Int>(water);
            var random=new System.Random(Seed);

            // The two crabs side by side, as close to the middle of the water as the cut allows.
            int low=int.MaxValue,high=int.MinValue;foreach(var c in water){low=Mathf.Min(low,c.y);high=Mathf.Max(high,c.y);}
            var middle=new Vector2((level.width-1)*.5f,(low+high)*.5f);
            Vector2Int left=new Vector2Int(-1,-1);float best=float.MaxValue;
            foreach(var c in water)
            {
                if(!waterSet.Contains(c+Vector2Int.right))continue;
                float d=Vector2.Distance(new Vector2(c.x+.5f,c.y),middle);
                if(d<best){best=d;left=c;}
            }
            var right=left+Vector2Int.right;
            level.fish.Add(new FishData{id=0,cell=left,facing=Facing.Up,crab=true});
            level.fish.Add(new FishData{id=1,cell=right,facing=Facing.Up,crab=true});
            var occupied=new HashSet<Vector2Int>{left,right};

            // A ring of one empty cell stays around the crabs. Fish just outside it face the crabs:
            // tapped, they slide across the empty cell and hit, so every tap on one of them is a hit.
            var traps=new List<KeyValuePair<Vector2Int,Facing>>
            {
                new KeyValuePair<Vector2Int,Facing>(left+Vector2Int.left*2,Facing.Right),
                new KeyValuePair<Vector2Int,Facing>(right+Vector2Int.right*2,Facing.Left),
                new KeyValuePair<Vector2Int,Facing>(left+Vector2Int.up*2,Facing.Down),
                new KeyValuePair<Vector2Int,Facing>(right+Vector2Int.up*2,Facing.Down),
                new KeyValuePair<Vector2Int,Facing>(left+Vector2Int.down*2,Facing.Up),
                new KeyValuePair<Vector2Int,Facing>(right+Vector2Int.down*2,Facing.Up),
            };
            var facings=new Dictionary<Vector2Int,Facing>();
            foreach(var trap in traps)if(waterSet.Contains(trap.Key)){facings[trap.Key]=trap.Value;occupied.Add(trap.Key);}

            // Everyone else: the rest of the fish rows at random, never inside the empty ring.
            int rowLow=Mathf.Max(config.firstRow,2)-1,rowHigh=Mathf.Min(config.lastRow,level.height-1)-1;
            var others=new List<Vector2Int>();
            foreach(var c in water)
            {
                if(occupied.Contains(c)||c.y<rowLow||c.y>rowHigh)continue;
                bool inRing=Mathf.Abs(c.y-left.y)<=1&&c.x>=left.x-1&&c.x<=right.x+1;
                if(!inRing)others.Add(c);
            }
            for(int i=others.Count-1;i>0;i--){int j=random.Next(i+1);var t=others[i];others[i]=others[j];others[j]=t;}
            var picked=new List<Vector2Int>();
            int count=Mathf.Clamp(config.fishCount-facings.Count,0,others.Count);
            for(int i=0;picked.Count<count&&i<others.Count;i++)picked.Add(others[i]);

            // Built backwards like Creative 01: deepest cells first, each fish takes a facing whose way out is still
            // clear, so the outer fish can be sent out in reverse order. A fish with no way out at all just waits.
            var depth=new Dictionary<Vector2Int,int>();var keys=new Dictionary<Vector2Int,int>();
            foreach(var c in picked){depth[c]=Depth(level,c);keys[c]=random.Next();}
            picked.Sort((a,b)=>depth[a]!=depth[b]?depth[b].CompareTo(depth[a]):keys[a].CompareTo(keys[b]));
            var options=new List<Facing>();
            foreach(var c in picked)
            {
                options.Clear();
                for(int f=0;f<4;f++)if(Escapes(level,c,(Facing)f,occupied))options.Add((Facing)f);
                facings[c]=options.Count>0?options[random.Next(options.Count)]:(Facing)random.Next(4);
                occupied.Add(c);
            }
            var cells=new List<Vector2Int>(facings.Keys);
            cells.Sort((a,b)=>a.y!=b.y?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            var looks=config.looks!=null&&config.looks.Length>0?config.looks:new[]{0};
            foreach(var c in cells)
                level.fish.Add(new FishData{id=level.fish.Count,cell=c,facing=facings[c],appearance=looks[random.Next(looks.Length)]});
            return level;
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
        private static bool Escapes(LevelData level,Vector2Int from,Facing facing,HashSet<Vector2Int> occupied)
        {
            var step=BoardState.Direction(facing);
            for(var cell=from+step;;cell+=step)
            {
                if(level.IsChannel(cell)||cell.x<0||cell.y<0||cell.x>=level.width||cell.y>=level.height)return true;
                if(occupied.Contains(cell))return false;
            }
        }
    }

    public sealed class CreativeDirector03 : MonoBehaviour
    {
        [Header("Board")]
        [Tooltip("Grid size, sand road included (columns x rows). 8 x 10 is the campaign board.")]
        public int gridColumns=8,gridRows=10;
        [Tooltip("How many fish start on the board, the ones facing the crabs included (capped by the free cells).")]
        public int fishCount=40;
        [Tooltip("Lowest and highest rows that hold fish, counted from the bottom of the grid (row 1 and the last row are road).")]
        public int firstFishRow=2,lastFishRow=9;
        [Tooltip("Fish looks picked at random (index into Fish Out Game > Fish Prefabs: 0 blue, 1 grey, 2 orange).")]
        public int[] fishLooks={0,2};
        [Header("Crabs")]
        [Tooltip("Crab size compared with a crab of the normal game.")]
        public float crabScale=1.5f;
        [Header("Intro camera")]
        [Tooltip("Camera size while zoomed on the crabs, as a share of the full view (0.45 = 45%).")]
        [Range(.15f,1)]public float introZoom=.45f;
        [Tooltip("Seconds the camera stays on the crabs, then seconds it takes to pull back to the whole board.")]
        public float introHold=1f,introPull=.6f;
        [Header("Explosion (full screen)")]
        [Tooltip("Big blast played on the crab whose bomb reaches 0.")]
        public GameObject blastPrefab;
        public float blastScale=4f;
        [Tooltip("Smoke burst, played at once in a grid of spots over the whole screen (and on the crab).")]
        public GameObject smokePrefab;
        public float smokeScale=6f;
        [Tooltip("Smoke spots across and down the screen.")]
        public int smokeColumns=2,smokeRows=5;
        [Tooltip("Smoke speed and lifetime multipliers: slower, longer smoke stays thick and covers the screen.")]
        public float smokeSpread=.45f,smokeLinger=3f;
        [Tooltip("How far the effects are pulled toward the camera, in world units, so the scenery cannot hide them.")]
        public float effectLift=15f;
        [Tooltip("Seconds from the blast until the lose card opens.")]
        public float endCardDelay=2f;
        [Tooltip("Camera shake strength on the blast, in world units.")]
        public float shakeStrength=.35f;
        [Header("End cards (prefabs, edit them in Prefab Mode)")]
        public GameObject loseCardPrefab;
        public GameObject winCardPrefab;

        private FishOutGame game;
        private Canvas hudCanvas;
        private BoardState stagedBoard;
        private bool ended,intro;
        private GameObject card;
        private readonly List<GameObject> effects=new List<GameObject>();
        private int savedLevel,savedUnlocked;

        private void Awake()
        {
            savedLevel=PlayerPrefs.GetInt("FishOut.Level",1);savedUnlocked=PlayerPrefs.GetInt("FishOut.Unlocked",1);
            FishOutGame.CreativeLevel=()=>CreativeLevel03.Build(new CreativeLevel03.Config{columns=gridColumns,rows=gridRows,fishCount=fishCount,firstRow=firstFishRow,lastRow=lastFishRow,looks=fishLooks});
            // The crabs stand still: taps on them (or near them) do nothing, and nothing is tapped during the intro.
            FishOutGame.CreativeCanTap=id=>!intro&&!ended&&game!=null&&!game.Board.IsCrab(id);
        }
        private void OnDestroy()
        {
            FishOutGame.CreativeLevel=null;FishOutGame.CreativeCanTap=null;Time.timeScale=1;
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
            StopAllCoroutines();
            if(card)Destroy(card);
            foreach(var effect in effects)if(effect)Destroy(effect);effects.Clear();
            Time.timeScale=1;ended=false;intro=false;
            game.Hud.CloseModal();
            game.StartLevel(CreativeLevel03.StageNumber);
        }
        private void Update()
        {
            if(game==null||game.Board==null)return;
            // The ad has no HUD at all: its end screens are the card prefabs on their own canvas.
            if(hudCanvas&&hudCanvas.enabled)hudCanvas.enabled=false;
            if(game.CurrentLevel!=CreativeLevel03.StageNumber&&!ended)typeof(FishOutGame).GetProperty("CurrentLevel").GetSetMethod(true).Invoke(game,new object[]{CreativeLevel03.StageNumber});
            if(stagedBoard!=game.Board)Stage();
            if(KeyDown('Q'))ShowLose(true);
            else if(KeyDown('W'))ShowWin();
            else if(!ended&&game.Board.IsFailed)StartCoroutine(Blast());
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
        // A new board: the crabs grow and lose their walking arrows (they never move), then the camera intro runs.
        private void Stage()
        {
            stagedBoard=game.Board;
            foreach(var crab in Crabs())
            {
                // The actor squashes and restores its "Model" holder to the scale it had when built, so the crab grows
                // one level down, on the model inside the holder, or a bump would shrink it back.
                var model=crab.transform.Find("Model");
                if(model&&model.childCount>0){var inner=model.GetChild(0);inner.localScale*=crabScale;inner.localPosition*=crabScale;}
                var marker=crab.transform.Find("Danger marker");if(marker){var s=marker.localScale;marker.localScale=new Vector3(s.x*crabScale,s.y,s.z*crabScale);}
                var arrows=crab.transform.Find("Crab chevrons");if(arrows)arrows.gameObject.SetActive(false);
                var bomb=crab.transform.Find("Crab bomb");if(bomb)bomb.position+=Vector3.up*(crabScale-1)*.45f;
            }
            StartCoroutine(Intro());
        }
        private List<FishActor> Crabs()
        {
            var list=new List<FishActor>();
            foreach(var pair in game.World.actors)if(pair.Value&&game.Board.IsCrab(pair.Key))list.Add(pair.Value);
            return list;
        }
        // Opens zoomed on the crabs, then pulls back to the whole board.
        private IEnumerator Intro()
        {
            var cam=game.boardCamera;var crabs=Crabs();
            if(!cam||crabs.Count==0)yield break;
            intro=true;
            var center=Vector3.zero;foreach(var c in crabs)center+=c.transform.position;center/=crabs.Count;
            float size=cam.orthographicSize;var home=cam.transform.position;
            var view=cam.WorldToViewportPoint(center);
            var close=home+cam.transform.right*(view.x-.5f)*2*size*cam.aspect+cam.transform.up*(view.y-.5f)*2*size;
            cam.orthographicSize=size*introZoom;cam.transform.position=close;
            for(float t=0;t<introHold;t+=Time.deltaTime)yield return null;
            for(float t=0;t<introPull;t+=Time.deltaTime)
            {
                float k=Mathf.SmoothStep(0,1,t/introPull);
                cam.orthographicSize=Mathf.Lerp(size*introZoom,size,k);cam.transform.position=Vector3.Lerp(close,home,k);
                yield return null;
            }
            cam.orthographicSize=size;cam.transform.position=home;intro=false;
        }
        // A bomb at 0 (or a board with no way out): the crab's blast and a wall of smoke fill the whole screen, the
        // camera shakes, and the lose card opens a little later.
        private IEnumerator Blast()
        {
            ended=true;
            FishActor crab=null;int least=int.MaxValue;
            foreach(var c in Crabs()){int left=game.Board.CrabLeft(c.Id);if(left<least){least=left;crab=c;}}
            var cam=game.boardCamera;
            var at=crab?crab.transform.position:Vector3.zero;
            Spawn(blastPrefab,Lift(cam,at+Vector3.up*.4f),blastScale,1,1);
            Spawn(smokePrefab,Lift(cam,at+Vector3.up*.3f),smokeScale,smokeSpread,smokeLinger);
            StartCoroutine(Shake(cam,.6f));
            // The bomb numbers draw on top of everything; inside the smoke they would float over it.
            foreach(var c in Crabs()){var bomb=c.transform.Find("Crab bomb");if(bomb)bomb.gameObject.SetActive(false);}
            // The rest of the smoke goes off in quick succession over a grid of spots that spans the screen.
            var spots=new List<Vector3>();
            var floor=new Plane(Vector3.up,Vector3.up*.3f);
            for(int y=0;y<smokeRows;y++)for(int x=0;x<smokeColumns;x++)
            {
                if(!cam)break;
                float vx=(x+.5f)/Mathf.Max(1,smokeColumns)+(y%2==0?-.08f:.08f),vy=(y+.5f)/Mathf.Max(1,smokeRows);
                var ray=cam.ViewportPointToRay(new Vector3(vx,vy,0));
                if(floor.Raycast(ray,out var enter))spots.Add(Lift(cam,ray.GetPoint(enter)));
            }
            float elapsed=0;
            foreach(var spot in spots)
            {
                Spawn(smokePrefab,spot,smokeScale,smokeSpread,smokeLinger);
                for(float t=0;t<.04f;t+=Time.deltaTime){elapsed+=Time.deltaTime;yield return null;}
            }
            for(float t=elapsed;t<endCardDelay;t+=Time.deltaTime)yield return null;
            ShowLose(false);
        }
        // Moved toward the camera along its own view line: the spot stays where it is on screen (the camera is
        // orthographic), but the huge smoke no longer cuts into the sea floor and the scenery, which hid part of it.
        private Vector3 Lift(Camera cam,Vector3 point)=>cam?point-cam.transform.forward*effectLift:point;
        private void Spawn(GameObject prefab,Vector3 position,float scale,float speed,float lifetime)
        {
            if(!prefab)return;
            var go=Instantiate(prefab,position,Quaternion.identity);go.transform.localScale*=scale;
            foreach(var system in go.GetComponentsInChildren<ParticleSystem>())
            {
                var main=system.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                main.startSpeedMultiplier*=speed;main.startLifetimeMultiplier*=lifetime;
            }
            // The painted cave front is drawn after the transparent queue (3100) and would cut the top of the smoke off.
            foreach(var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>())
                foreach(var material in renderer.materials)material.renderQueue=Mathf.Max(material.renderQueue,3150);
            // Kept until the next try: under the lose card the game is frozen, and so is the smoke.
            effects.Add(go);
        }
        private IEnumerator Shake(Camera cam,float time)
        {
            if(!cam)yield break;
            var home=cam.transform.position;
            for(float t=0;t<time&&cam;t+=Time.unscaledDeltaTime)
            {
                float k=1-t/time;
                cam.transform.position=home+(cam.transform.right*Random.Range(-1f,1f)+cam.transform.up*Random.Range(-1f,1f))*shakeStrength*k;
                yield return null;
            }
            if(cam)cam.transform.position=home;
        }
        private void ShowLose(bool fromKey)
        {
            if(card&&card.name.StartsWith("Lose"))return;
            ended=true;Time.timeScale=0;
            // A real loss already played the fail jingle with the game's own fail logic.
            if(fromKey)PlaySound("fail");
            OpenCard(loseCardPrefab,"Lose card");
        }
        private void ShowWin()
        {
            if(card&&card.name.StartsWith("Win"))return;
            ended=true;Time.timeScale=0;
            PlaySound("win");
            OpenCard(winCardPrefab,"Win card");
        }
        private void OpenCard(GameObject prefab,string name)
        {
            if(card)Destroy(card);
            if(!prefab){Debug.LogWarning("Creative 03: no "+name+" prefab assigned");return;}
            card=Instantiate(prefab);card.name=name;
            var canvas=card.GetComponentInChildren<Canvas>();if(canvas&&canvas.sortingOrder<500)canvas.sortingOrder=500;
            if(!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
            {
                var events=new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }
            var end=card.GetComponent<CreativeEndCard>();
            var button=end&&end.button?end.button:card.GetComponentInChildren<Button>();
            if(button)button.onClick.AddListener(Restart);
        }
        // The cards are opened by the ad, not the HUD, so the jingles the game plays on its own win/fail are played here.
        private void PlaySound(string clip)
        {
            var field=typeof(FishOutGame).GetField("audioPlayer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var player=field!=null?field.GetValue(game) as FishOutAudio:null;
            if(player)player.Play(clip);
        }
    }
}
