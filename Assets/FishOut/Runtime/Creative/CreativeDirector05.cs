using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishOut
{
    // Creative 05 (partner ad, 15/11): grey fish in concentric rings, a closed ring of crabs around them, every crab
    // carrying a bomb at 3. Any bump on a crab runs round the ring as a wave and counts EVERY crab down by 1; at 0 the
    // bombs blow up over the whole screen and the lose card opens. One crab has a way out: tapped, it leaves and the
    // whole chain of crabs touching it goes with it. The fish then work the same way, and a clear board opens the win
    // card. Q shows the lose card, W the win card.
    public static class CreativeLevel05
    {
        public const int Seed=5501;
        public const int StageNumber=5;
        public struct Config
        {
            public int columns,rows,fishLook;
            public float centerRadius,fishInner,fishOuter,crabInner,crabOuter,exitAngle;
        }
        private enum Hit{Escape,Crab,Fish}
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

            // Rings by distance from the middle of the water: fish in the middle and in a ring, the crabs outside.
            int low=int.MaxValue,high=int.MinValue;foreach(var c in water){low=Mathf.Min(low,c.y);high=Mathf.Max(high,c.y);}
            var centre=new Vector2((level.width-1)*.5f,(low+high)*.5f);
            var crabs=new List<Vector2Int>();var fish=new List<Vector2Int>();
            var isCrab=new Dictionary<Vector2Int,bool>();
            foreach(var c in water)
            {
                float d=Vector2.Distance(c,centre);
                if(d>=config.crabInner&&d<config.crabOuter){crabs.Add(c);isCrab[c]=true;}
                else if(d<config.centerRadius||(d>=config.fishInner&&d<config.fishOuter)){fish.Add(c);isCrab[c]=false;}
            }

            // One crab, the one nearest the chosen angle, faces out of the ring and can leave.
            var facings=new Dictionary<Vector2Int,Facing>();
            var exit=new Vector2Int(-1,-1);var exitFacing=Facing.Right;float best=float.MaxValue;
            foreach(var c in crabs)
                foreach(var f in Outward(c,centre))
                {
                    if(FirstHit(level,c,f,isCrab)!=Hit.Escape)continue;
                    float angle=Mathf.Atan2(c.y-centre.y,c.x-centre.x)*Mathf.Rad2Deg;
                    float off=Mathf.Abs(Mathf.DeltaAngle(angle,config.exitAngle));
                    if(off<best){best=off;exit=c;exitFacing=f;}
                    break;
                }
            // The other crabs face along the ring, into a neighbour: no way out until the chain is gone.
            foreach(var c in crabs)facings[c]=c==exit?exitFacing:Pick(level,c,Along(c,centre),isCrab);
            // The fish face outwards, into the crabs where they can reach one: each tap on them is a hit.
            foreach(var c in fish)facings[c]=Pick(level,c,Outward(c,centre),isCrab);

            crabs.Sort(ByRow);fish.Sort(ByRow);
            foreach(var c in crabs)level.fish.Add(new FishData{id=level.fish.Count,cell=c,facing=facings[c],crab=true});
            foreach(var c in fish)level.fish.Add(new FishData{id=level.fish.Count,cell=c,facing=facings[c],appearance=config.fishLook});
            return level;
        }
        private static int ByRow(Vector2Int a,Vector2Int b)=>a.y!=b.y?a.y.CompareTo(b.y):a.x.CompareTo(b.x);
        // Away from the middle, the stronger axis first.
        private static List<Facing> Outward(Vector2Int c,Vector2 centre)=>Axes(c.x-centre.x,c.y-centre.y);
        // Clockwise round the middle.
        private static List<Facing> Along(Vector2Int c,Vector2 centre)=>Axes(c.y-centre.y,-(c.x-centre.x));
        private static List<Facing> Axes(float dx,float dy)
        {
            var h=dx>=0?Facing.Right:Facing.Left;var v=dy>=0?Facing.Up:Facing.Down;
            return Mathf.Abs(dx)>=Mathf.Abs(dy)?new List<Facing>{h,v}:new List<Facing>{v,h};
        }
        // The preferred facings first, then the rest; never one that escapes if anything else is possible,
        // and a crab ahead beats a fish ahead.
        private static Facing Pick(LevelData level,Vector2Int c,List<Facing> preferred,Dictionary<Vector2Int,bool> isCrab)
        {
            var order=new List<Facing>(preferred);
            for(int f=0;f<4;f++)if(!order.Contains((Facing)f))order.Add((Facing)f);
            Facing chosen=order[0];int score=int.MaxValue;
            foreach(var f in order)
            {
                var hit=FirstHit(level,c,f,isCrab);
                int s=hit==Hit.Crab?0:hit==Hit.Fish?1:2;
                if(s<score){score=s;chosen=f;}
            }
            return chosen;
        }
        private static Hit FirstHit(LevelData level,Vector2Int from,Facing facing,Dictionary<Vector2Int,bool> isCrab)
        {
            var step=BoardState.Direction(facing);
            for(var cell=from+step;;cell+=step)
            {
                if(level.IsChannel(cell)||cell.x<0||cell.y<0||cell.x>=level.width||cell.y>=level.height)return Hit.Escape;
                if(isCrab.TryGetValue(cell,out var crab))return crab?Hit.Crab:Hit.Fish;
            }
        }
    }

    public sealed class CreativeDirector05 : MonoBehaviour
    {
        [Header("Board")]
        [Tooltip("Grid size, sand road included (columns x rows). 11 x 13 leaves 9 x 11 cells of water.")]
        public int gridColumns=11,gridRows=13;
        [Tooltip("Fish look (index into Fish Out Game > Fish Prefabs: 0 blue, 1 grey, 2 orange).")]
        public int fishLook=1;
        [Header("Rings (distance from the middle, in cells)")]
        [Tooltip("Fish in the middle, up to this distance.")]
        public float centerRadius=1.2f;
        [Tooltip("The fish ring.")]
        public float fishRingFrom=1.9f,fishRingTo=2.9f;
        [Tooltip("The crab ring.")]
        public float crabRingFrom=3.6f,crabRingTo=4.7f;
        [Tooltip("Where the one crab with a way out sits, as an angle round the middle (0 = right, 90 = top, -90 = bottom).")]
        public float exitCrabAngle=-45f;
        [Header("Waves")]
        [Tooltip("Seconds between two crabs counting down as the wave runs round the ring.")]
        public float waveStep=.06f;
        [Tooltip("Seconds between two pieces of a chain leaving, from the tapped one outwards.")]
        public float chainStep=.035f;
        [Tooltip("Seconds from the last piece leaving until the win card opens.")]
        public float winDelay=1.2f;
        [Header("Explosion (full screen)")]
        [Tooltip("Big blast played on the crab nearest the last hit.")]
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
        private bool ended,waving;
        private int lastTapped=-1;
        private GameObject card;
        private readonly List<GameObject> effects=new List<GameObject>();
        // The number each crab's bomb shows; the board counts down first, the wave brings the bombs along.
        private readonly Dictionary<int,int> shown=new Dictionary<int,int>();
        private int savedLevel,savedUnlocked;

        private void Awake()
        {
            savedLevel=PlayerPrefs.GetInt("FishOut.Level",1);savedUnlocked=PlayerPrefs.GetInt("FishOut.Unlocked",1);
            BoardState.SharedCrabBumps=true;BoardState.SkipDeadEndSearch=true;
            FishOutGame.CreativeLevel=()=>CreativeLevel05.Build(new CreativeLevel05.Config{columns=gridColumns,rows=gridRows,fishLook=fishLook,
                centerRadius=centerRadius,fishInner=fishRingFrom,fishOuter=fishRingTo,crabInner=crabRingFrom,crabOuter=crabRingTo,exitAngle=exitCrabAngle});
            FishOutGame.CreativeCanTap=CanTap;
        }
        private void OnDestroy()
        {
            FishOutGame.CreativeLevel=null;FishOutGame.CreativeCanTap=null;Time.timeScale=1;
            BoardState.SharedCrabBumps=false;BoardState.SkipDeadEndSearch=false;
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
            Time.timeScale=1;ended=false;waving=false;lastTapped=-1;
            game.Hud.CloseModal();
            game.StartLevel(CreativeLevel05.StageNumber);
        }
        private void Update()
        {
            if(game==null||game.Board==null)return;
            // The ad has no HUD at all: its end screens are the card prefabs on their own canvas.
            if(hudCanvas&&hudCanvas.enabled)hudCanvas.enabled=false;
            if(game.CurrentLevel!=CreativeLevel05.StageNumber&&!ended)typeof(FishOutGame).GetProperty("CurrentLevel").GetSetMethod(true).Invoke(game,new object[]{CreativeLevel05.StageNumber});
            if(stagedBoard!=game.Board)Stage();
            if(KeyDown('Q')){ShowLose(true);return;}
            if(KeyDown('W')){ShowWin(true);return;}
            if(ended||waving||game.FishMovingOnBoard)return;
            var board=game.Board;
            if(board.IsFailed){StartCoroutine(Blast());return;}
            if(board.IsClear){StartCoroutine(WinLater());return;}
            foreach(var pair in shown)
                if(board.Find(pair.Key)!=null&&board.CrabLeft(pair.Key)<pair.Value){StartCoroutine(Wave());return;}
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
        private void Stage()
        {
            stagedBoard=game.Board;shown.Clear();
            foreach(var f in game.Board.Fish)if(f.crab)shown[f.id]=game.Board.CrabLeft(f.id);
        }
        // Every tap the game lets through comes here first. A piece that can leave takes its whole chain with it:
        // every piece of the same kind (crab or fish) touching it, also corner to corner, and so on outwards.
        private bool CanTap(int id)
        {
            if(ended||waving||game==null)return false;
            var board=game.Board;lastTapped=id;
            if(!board.Preview(id).exits)return true;
            var tapped=board.Find(id);
            var leaving=new HashSet<int>(board.EscapingLine(id));leaving.Add(id);
            var at=new Dictionary<Vector2Int,FishData>();foreach(var f in board.Fish)at[f.cell]=f;
            var chain=new List<int>();var seen=new HashSet<Vector2Int>{tapped.cell};var queue=new Queue<Vector2Int>();queue.Enqueue(tapped.cell);
            while(queue.Count>0)
            {
                var c=queue.Dequeue();
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                {
                    var n=new Vector2Int(c.x+dx,c.y+dy);
                    if(!seen.Add(n)||!at.TryGetValue(n,out var other)||other.crab!=tapped.crab)continue;
                    queue.Enqueue(n);
                    if(!leaving.Contains(other.id))chain.Add(other.id);
                }
            }
            if(chain.Count>0)StartCoroutine(Chain(chain));
            return true;
        }
        // The chain pops off the board in breadth order, nearest the tapped piece first.
        private IEnumerator Chain(List<int> chain)
        {
            var board=game.Board;
            for(float t=0;t<.12f;t+=Time.deltaTime)yield return null;
            foreach(var id in chain)
            {
                if(game.Board!=board)yield break;
                if(!board.Vanish(id))continue;
                shown.Remove(id);
                if(game.World.actors.TryGetValue(id,out var actor)&&actor)
                {
                    game.World.actors.Remove(id);
                    foreach(var hitbox in actor.GetComponentsInChildren<Collider>())hitbox.enabled=false;
                    game.World.PlayEffect("Rescue",actor.transform.position);
                    StartCoroutine(Shrink(actor.gameObject));
                }
                for(float t=0;t<chainStep;t+=Time.deltaTime)yield return null;
            }
            PlaySound("peel");
        }
        private static IEnumerator Shrink(GameObject piece)
        {
            var size=piece.transform.localScale;
            for(float t=0;t<.22f&&piece;t+=Time.deltaTime){float k=t/.22f;piece.transform.localScale=size*(1+.25f*Mathf.Sin(k*Mathf.PI)-k);yield return null;}
            if(piece)Destroy(piece);
        }
        // A bump took every crab down by 1 on the board; the bombs follow one after another round the ring, both ways
        // from the crab that was hit.
        private IEnumerator Wave()
        {
            waving=true;
            var board=game.Board;var crabs=new List<FishActor>();
            foreach(var pair in game.World.actors)if(pair.Value&&board.IsCrab(pair.Key))crabs.Add(pair.Value);
            if(crabs.Count==0){waving=false;yield break;}
            var centre=Vector3.zero;foreach(var c in crabs)centre+=c.transform.position;centre/=crabs.Count;
            var origin=Origin(crabs);
            float start=Angle(origin.transform.position,centre);
            crabs.Sort((a,b)=>Mathf.Abs(Mathf.DeltaAngle(start,Angle(a.transform.position,centre))).CompareTo(Mathf.Abs(Mathf.DeltaAngle(start,Angle(b.transform.position,centre)))));
            float last=-1;
            foreach(var crab in crabs)
            {
                if(game.Board!=board)yield break;
                // Two crabs at the same distance round the ring (one each way) count down together.
                float off=Mathf.Abs(Mathf.DeltaAngle(start,Angle(crab.transform.position,centre)));
                if(last>=0&&off-last>4f)for(float t=0;t<waveStep;t+=Time.deltaTime)yield return null;
                last=off;
                if(!crab||board.Find(crab.Id)==null)continue;
                if(shown.TryGetValue(crab.Id,out var value)&&value!=board.CrabLeft(crab.Id)){game.World.RefreshCrab(board,crab.Id);crab.Pop(.14f,.12f);}
                shown[crab.Id]=board.CrabLeft(crab.Id);
            }
            waving=false;
        }
        // The crab the bump started from: the tapped crab, or the crab nearest the piece that ran into one.
        private FishActor Origin(List<FishActor> crabs)
        {
            FishActor tapped=null;
            if(lastTapped>=0)game.World.actors.TryGetValue(lastTapped,out tapped);
            if(tapped&&game.Board.IsCrab(lastTapped))return tapped;
            var from=tapped?tapped.transform.position:Vector3.zero;
            FishActor nearest=crabs[0];float best=float.MaxValue;
            foreach(var c in crabs){float d=(c.transform.position-from).sqrMagnitude;if(d<best){best=d;nearest=c;}}
            return nearest;
        }
        private static float Angle(Vector3 p,Vector3 centre)=>Mathf.Atan2(p.z-centre.z,p.x-centre.x)*Mathf.Rad2Deg;
        private IEnumerator WinLater()
        {
            ended=true;
            for(float t=0;t<winDelay;t+=Time.deltaTime)yield return null;
            ShowWin(false);
        }
        // A bomb at 0: the blast and a wall of smoke fill the whole screen, the camera shakes, and the lose card
        // opens a little later.
        private IEnumerator Blast()
        {
            ended=true;
            var crabs=new List<FishActor>();
            foreach(var pair in game.World.actors)if(pair.Value&&game.Board.IsCrab(pair.Key))crabs.Add(pair.Value);
            var crab=crabs.Count>0?Origin(crabs):null;
            var cam=game.boardCamera;
            var at=crab?crab.transform.position:Vector3.zero;
            Spawn(blastPrefab,Lift(cam,at+Vector3.up*.4f),blastScale,1,1);
            Spawn(smokePrefab,Lift(cam,at+Vector3.up*.3f),smokeScale,smokeSpread,smokeLinger);
            StartCoroutine(Shake(cam,.6f));
            // The bomb numbers draw on top of everything; inside the smoke they would float over it.
            foreach(var c in crabs){var bomb=c.transform.Find("Crab bomb");if(bomb)bomb.gameObject.SetActive(false);}
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
        private void ShowWin(bool fromKey)
        {
            if(card&&card.name.StartsWith("Win"))return;
            ended=true;Time.timeScale=0;
            // The game's own clear only starts once the last piece is in the cave, and the frozen card stops it,
            // so the jingle is always played here.
            PlaySound("win");
            OpenCard(winCardPrefab,"Win card");
        }
        private void OpenCard(GameObject prefab,string name)
        {
            if(card)Destroy(card);
            if(!prefab){Debug.LogWarning("Creative 05: no "+name+" prefab assigned");return;}
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
        // The cards are opened by the ad, not the HUD, so the jingles are played here when a key opens them.
        private void PlaySound(string clip)
        {
            var field=typeof(FishOutGame).GetField("audioPlayer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var player=field!=null?field.GetValue(game) as FishOutAudio:null;
            if(player)player.Play(clip);
        }
    }
}
