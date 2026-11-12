using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace FishOut
{
    public sealed class FishOutGame : MonoBehaviour
    {
        public GameObject[] fishPrefabs;
        public GameObject crabPrefab;
        public Font uiFont;
        public Camera boardCamera;
        public RuntimeAnimatorController[] characterAnimations;
        public BoardState Board { get; private set; }
        public int CurrentLevel { get; private set; }
        public int UnlockedLevel { get; private set; }
        // Fish swim independently; only clear and rescue animations lock the whole board.
        public bool IsBusy => locked||moving.Count>0;
        public bool IsLocked => locked;
        public bool IsPlayingStage => playing;
        public FishOutHud Hud => hud;
        public FishOutWorld World => world;
        private FishOutHud hud;
        private FishOutWorld world;
        private FishOutAudio audioPlayer;
        private readonly HashSet<int> moving=new HashSet<int>();
        private readonly List<WaypointMover> swimmers=new List<WaypointMover>();
        private const float QueueGap=.9f,QueueNear=.4f,QueueFar=1.5f,QueueRadius=.5f,QueueFarRadius=.8f;
        private bool locked, playing, selectingRescue, settings, crabWarned;
        private bool sound=true,music=true,vibration=true;
        private int tutorialTarget=-1,stopLessonFish=-1,hintTarget=-1,guidedTaps;
        private bool lessonActive,lineExplained;
        private float lastTapTime,paceReference;
        private readonly List<Vector2> presses=new List<Vector2>();
        private Text pointer;
        private RectTransform pointerHand;
        private int screenWidth,screenHeight;
        private int uiInputFrame=-1;
        private readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        private const string SavePrefix="FishOut.";
        private const float TapForgiveness=.75f;

        private void Start()
        {
            Application.targetFrameRate=60; Screen.orientation=ScreenOrientation.Portrait;
            UnlockedLevel=Mathf.Clamp(PlayerPrefs.GetInt(SavePrefix+"Unlocked",1),1,FishOutLevels.Count);
            CurrentLevel=Mathf.Clamp(PlayerPrefs.GetInt(SavePrefix+"Level",1),1,UnlockedLevel);
            sound=PlayerPrefs.GetInt(SavePrefix+"Sound",1)==1;
            music=PlayerPrefs.GetInt(SavePrefix+"Music",1)==1;
            vibration=PlayerPrefs.GetInt(SavePrefix+"Vibration",1)==1;
            if(!boardCamera)boardCamera=Camera.main;
            if(!GameObject.Find("Ocean background"))
            {
                var background=new GameObject("Ocean background",typeof(Camera)).GetComponent<Camera>();
                background.transform.SetParent(transform,false);background.depth=-10;background.cullingMask=0;
                background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.035f,.22f,.29f);
            }
            // The board camera now fills the screen, so the background one would only add a clear.
            GameObject.Find("Ocean background").GetComponent<Camera>().enabled=false;
            // Fish shadows on the floor are blob quads; real-time shadows would redraw every model.
            foreach(var sun in FindObjectsByType<Light>(FindObjectsSortMode.None))if(sun.type==LightType.Directional)sun.shadows=LightShadows.None;
            if(!boardCamera||fishPrefabs==null||fishPrefabs.Length==0||!crabPrefab)
            { Debug.LogError("Fish Out scene has missing references. Run Fish Out > Create or update game scene.");enabled=false;return; }
            var audioObject=new GameObject("Game audio"); audioObject.transform.SetParent(transform,false);
            audioPlayer=audioObject.AddComponent<FishOutAudio>(); audioPlayer.Initialize(sound,music);
            var canvas=new GameObject("Fish Out UI",typeof(RectTransform)); canvas.transform.SetParent(transform,false);
            hud=canvas.AddComponent<FishOutHud>(); hud.Build(this,Resources.Load<Font>("Fonts/Fredoka-Bold"));
            // Tutorial hand: the benchmark's white glove, its fingertip pressing on the fish to tap (about 15-20% of the screen wide).
            pointer=hud.Label(canvas.transform,"",new Vector2(.5f,.5f),new Vector2(10,10),10,Color.white,false);pointer.name="Tutorial hand";pointer.gameObject.SetActive(false);
            var glove=HudSkin.Load()?HudSkin.Load().tutorialHand:null;
            var hand=FishOutHud.Rect("Hand",pointer.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(230,230)).gameObject.AddComponent<UnityEngine.UI.Image>();
            hand.raycastTarget=false;
            if(glove){hand.sprite=glove;hand.preserveAspect=true;hand.rectTransform.pivot=new Vector2(.2f,.86f);}
            else{hand.sprite=FishOutIcons.Arrow;hand.color=new Color(1,.86f,.2f);hand.rectTransform.pivot=new Vector2(.5f,0);hand.rectTransform.sizeDelta=new Vector2(118,140);}
            var handShade=hand.gameObject.AddComponent<UnityEngine.UI.Shadow>();handShade.effectColor=new Color(0,0,0,.3f);handShade.effectDistance=new Vector2(6,-8);
            pointerHand=hand.rectTransform;
            if(!EventSystem.current)
            {
                var events=new GameObject("EventSystem",typeof(EventSystem)); events.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            // Spec §6: the game opens on Home; on the first play PLAY starts Level 1.
            ShowHome();
        }
        private void RebuildBoard()
        {
            StopAllCoroutines(); locked=false;moving.Clear();swimmers.Clear();linePredecessor.Clear();selectingRescue=false;settings=false;tutorialTarget=-1;hintTarget=-1;guidedTaps=0;lessonActive=false;lineExplained=false;pointer.gameObject.SetActive(false);
            if(world){world.gameObject.SetActive(false);Destroy(world.gameObject);}
            Board=new BoardState(FishOutLevels.Get(CurrentLevel));
            var root=new GameObject("Underwater board");root.transform.SetParent(transform,false);
            world=root.AddComponent<FishOutWorld>();world.Build(this,Board);
            FitCamera();
        }
        private void FitCamera()
        {
            // The board is rebuilt procedurally; baked occlusion can hide newly spawned scenery.
            boardCamera.useOcclusionCulling=false;
            screenWidth=Screen.width;screenHeight=Screen.height;
            boardCamera.transform.position=new Vector3(0,12,-9);boardCamera.transform.rotation=Quaternion.Euler(55,0,0);
            // Aim at the center, keeping the elevated camera fixed during play.
            boardCamera.transform.LookAt(new Vector3(0,.2f,0));
            boardCamera.orthographic=true;boardCamera.rect=new Rect(0,.215f,1,.55f);
            float aspect=Mathf.Max(.1f,((float)Screen.width/Mathf.Max(1,Screen.height))/.55f);
            boardCamera.orthographicSize=Mathf.Max(4.3f,3.65f/aspect);
            if(Board!=null&&Board.Level.showcase)
            {
                FrameShowcase();return;
            }
            boardCamera.clearFlags=CameraClearFlags.SolidColor;boardCamera.backgroundColor=new Color(.035f,.22f,.29f);
        }
        private void FrameShowcase()
        {
            float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);
            if(PaintedBackdrop.Available)
            {
                PaintedBackdrop.Frame(boardCamera,Board.Level,aspect);
                boardCamera.clearFlags=CameraClearFlags.SolidColor;boardCamera.backgroundColor=new Color(.31f,.8f,.88f);return;
            }
            BoardView.Compute(Board.Level,aspect,out var size,out var middle);
            var rotation=BoardView.Rotation;
            boardCamera.rect=new Rect(0,0,1,1);boardCamera.orthographicSize=size;
            boardCamera.transform.rotation=rotation;
            boardCamera.transform.position=rotation*new Vector3(middle.x,middle.y,-40);
            boardCamera.clearFlags=CameraClearFlags.SolidColor;boardCamera.backgroundColor=ShowcaseScenery.DeepSea;
        }
        public void ShowHome()
        {
            Time.timeScale=1;playing=false;RebuildBoard();HideBoardForHome();hud.SetHome(true,CurrentLevel);hud.instructions.text="";
        }
        // Home shows only the sea and the cave: the level's board, its rim and its pieces stay hidden until a level starts.
        private void HideBoardForHome()
        {
            foreach(var part in new[]{"Board checker","Open board boundary","Fish and obstacles"}){var child=world.transform.Find(part);if(child)child.gameObject.SetActive(false);}
        }
        public void OpenLevelSelect(){hud.LevelSelect(FishOutLevels.Count,CurrentLevel,UnlockedLevel);}
        public void StartLevel(int number)
        {
            CurrentLevel=Mathf.Clamp(number,1,FishOutLevels.Count);SaveLevel();
            playing=true;RebuildBoard();hud.SetHome(false,CurrentLevel);hud.FadeIn();crabWarned=false;
            switch(CurrentLevel)
            {
                // One short line each, like the benchmark's tutorial card.
                case 1: hud.instructions.text="Tap a fish to send it to the cave.";tutorialTarget=NearestEscaping();break;
                case 2: hud.instructions.text="Tap it. Fish stop at other fish.";tutorialTarget=stopLessonFish=FindStoppingFish();lessonActive=true;break;
                case 3: hud.instructions.text="Rocks never move. Plan around them.";break;
                case 4: hud.instructions.text="Send crabs out too, before 3 bumps!";break;
                case 5: hud.instructions.text="Mind the rocks and the crabs!";break;
                default:hud.instructions.text="Lead every fish to the cave.";break;
            }
            world.Highlight(tutorialTarget);pointer.gameObject.SetActive(tutorialTarget>=0);lastTapTime=Time.unscaledTime;Refresh();
        }
        // The closest free fish shows the whole loop in the shortest time.
        private int NearestEscaping()
        {
            int best=-1;float shortest=float.PositiveInfinity;
            foreach(var fish in Board.Fish)
            {
                if(moving.Contains(fish.id))continue;
                var move=Board.Preview(fish.id);if(!move.exits)continue;
                var route=ConveyorNetwork.RouteToGate(Board.Level,move.to);float length=Vector2.Distance(fish.cell,move.to);
                for(int i=1;i<route.Count;i++)length+=Vector2.Distance(route[i-1],route[i]);
                if(length<shortest){shortest=length;best=fish.id;}
            }
            return best;
        }
        private void GuideAfterMove(int id,MoveResult result)
        {
            lastTapTime=Time.unscaledTime;tutorialTarget=-1;pointer.gameObject.SetActive(false);
            if(id==hintTarget)hintTarget=-1;
            if(CurrentLevel==1&&guidedTaps<3)tutorialTarget=NearestEscaping();
            if(lessonActive)
            {
                if(id==stopLessonFish||Board.Find(stopLessonFish)==null)lessonActive=false;
                else tutorialTarget=stopLessonFish;
            }
            // Words only while a lesson is running: level 1 until its guided taps are done, level 2 until the lesson
            // fish has stopped and the player has moved on, later stages until their first move.
            if(CurrentLevel==1&&guidedTaps>=3)hud.instructions.text="";
            if(CurrentLevel==2&&!lessonActive&&id!=stopLessonFish)hud.instructions.text="";
            if(CurrentLevel>=3&&result.ChangesBoard)hud.instructions.text="";
            world.Highlight(tutorialTarget>=0?tutorialTarget:Board.Find(hintTarget)!=null?hintTarget:-1);
            pointer.gameObject.SetActive(tutorialTarget>=0);
        }
        private int FindStoppingFish()
        {
            foreach(var fish in Board.Fish){var move=Board.Preview(fish.id);if(move.ChangesBoard&&!move.exits)return fish.id;}
            return Board.FindEscapingFish();
        }
        // Spec §4: taps are ignored while a fish is moving on the board. Once it has reached the road to the cave,
        // it no longer blocks anything on the board, so the next tap is accepted.
        private const float CaveSpeed=3f;
        public bool FishMovingOnBoard
        {
            get
            {
                foreach(var id in moving){if(world.actors.TryGetValue(id,out var actor)&&actor&&actor.Mover.OnRoad)continue;return true;}
                return false;
            }
        }
        public void TapFish(int id)
        {
            if(!playing||locked||settings||hud.modal||Board.IsFailed||Board.IsClear||Board.Find(id)==null||moving.Contains(id)||FishMovingOnBoard)return;
            if(selectingRescue){StartCoroutine(RescueFish(id));return;}
            var move=Board.Preview(id);
            if(CurrentLevel==4&&move.CostsCrab&&!crabWarned)
            {
                world.Highlight(id);
                hud.Choice("Watch out!","Every bump counts a crab's number down,\nalso when the crab runs into something.\nIf it reaches 0, you lose. Free its way out first!","Got it",()=>{crabWarned=true;world.Highlight(-1);hud.CloseModal();ResolveBoard();},null,null,true);
                return;
            }
            if(vibration)FishOutHaptics.Tap();
            if(!move.ChangesBoard&&!move.CostsCrab&&!move.hitsRock){StartCoroutine(BlockedFish(id));return;}
            if(CurrentLevel==1)guidedTaps++;
            var line=Board.EscapingLine(id);
            if(line.Count==0){StartCoroutine(MoveFish(id));return;}
            if(CurrentLevel==1&&line.Count>=2&&!lineExplained){lineExplained=true;hud.Toast("Fish facing the same way leave together!",2);}
            // Each fish leaves the board state as it starts, which clears the way for the next one.
            bool leader=true;WaypointMover previous=null;
            foreach(var member in line)
            {
                if(moving.Contains(member))break;
                var mover=world.actors[member].Mover;
                if(previous!=null)linePredecessor[mover]=previous;
                StartCoroutine(MoveFish(member,leader));leader=false;previous=mover;
            }
        }
        // A blocked tap changes nothing, so it keeps any guide on screen and points at the cause.
        private IEnumerator BlockedFish(int id)
        {
            moving.Add(id);
            var actor=world.actors[id];var rest=actor.transform.position;
            var step=BoardState.Direction(Board.Find(id).facing);var vector=new Vector3(step.x,0,step.y);
            audioPlayer.Play("buffer");world.PlayEffect("Bump",rest+vector*.45f);actor.Dizzy();
            var blocker=Board.BlockerOf(id);
            if(blocker!=null&&world.actors.TryGetValue(blocker.id,out var other)&&other)other.Jolt(vector);
            for(float t=0;t<.14f;t+=Time.deltaTime){actor.transform.position=rest+vector*Mathf.Sin(t/.14f*Mathf.PI)*.22f;yield return null;}
            actor.transform.position=rest;moving.Remove(id);ResolveBoard();
        }
        private float Pace()
        {
            float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);
            if(paceReference<=0)BoardView.Compute(FishOutLevels.Get(1),aspect,out paceReference,out _);
            return Mathf.Clamp(boardCamera.orthographicSize/paceReference,1,2.2f);
        }
        private IEnumerator MoveFish(int id,bool leader=true)
        {
            moving.Add(id);
            var actor=world.actors[id];
            actor.Mover.orientAlongPath=Board.Level.showcase;
            var plan=Board.CalculatePath(id);
            var path=MovementPath.FromBoard(plan,world.Cell,boardCamera,actor.VisibleBounds());
            var result=Board.Move(id);
            Refresh();world.RefreshRocks(Board);
            if(result.exits){actor.Mover.roadStart=plan.Cells.Count-2.2f;swimmers.Add(actor.Mover);}
            // An escaping fish no longer blocks taps on the fish it swims past.
            if(result.exits)foreach(var hitbox in actor.GetComponentsInChildren<Collider>())hitbox.enabled=false;
            GuideAfterMove(id,result);
            actor.SetSwimming(result.ChangesBoard);
            var end=path.End;
            // Speed follows the zoom so fish look equally fast on every board.
            float pace=Pace();
            actor.Mover.maximumSpeed=(result.exits?12f:7f)*pace*PaintedBackdrop.Calibration.boardSpeed;
            actor.Mover.startSpeed=result.exits?5f:3f;
            // A fish that stops at a blocker brakes late and hard, so the hit lands on contact rather than after a slow creep.
            actor.Mover.acceleration=result.exits?96f:150f;
            // An escaping fish eases into the slower road and cave speeds instead of braking hard, so the queue behind it never jolts.
            actor.Mover.easeDown=result.exits&&SmoothQueue?9f*pace:float.PositiveInfinity;
            if(result.ChangesBoard){actor.Pop(.12f,.09f);audioPlayer.Play("peel");}
            // One trail per line keeps a long queue readable and cheap.
            var trail=leader&&result.ChangesBoard?world.AttachEffect("Trail",actor.transform):null;
            // On the painted seabed the fish swims through the cave mouth and on behind the arch, which is drawn over it
            // and darkens the tunnel, so it fades out of sight instead of shrinking away.
            bool painted=PaintedBackdrop.Warps(Board.Level);
            var gate=painted?PaintedBackdrop.CaveMouth(Board.Level,.1f):world.Cell(Board.Level.gate);bool entered=false;
            // Half way into the dark, the fish stops holding up the ones behind it: any overlap is out of sight.
            float darkZ=painted?Mathf.Lerp(gate.z,PaintedBackdrop.CaveEnd(Board.Level,.1f).z,.3f):float.PositiveInfinity;
            yield return actor.Mover.FollowPath(path,!result.exits,position=>
            {
                // Once off the board the fish cruises along the sand road at the calibrated, slower speed.
                if(result.exits&&!entered&&actor.Mover.OnRoad)
                    actor.Mover.maximumSpeed=Mathf.Min(actor.Mover.maximumSpeed,12f*pace*PaintedBackdrop.Calibration.roadSpeed);
                if(!result.exits||position.z<gate.z)return;
                if(position.z>=darkZ)actor.Mover.InCave=true;
                if(!entered)
                {
                    entered=true;audioPlayer.PlayGate();
                    // On the painting the fish slows down inside the tunnel so it can be seen sinking into the dark;
                    // the white burst would hide exactly that, so only the old 3D reef keeps it.
                    if(painted)actor.Mover.maximumSpeed=Mathf.Min(actor.Mover.maximumSpeed,CaveSpeed*Pace());
                    else world.PlayEffect("GateBlast",gate);
                }
                // Without the painted arch, the fish shrinks away over the last cell of the path, inside the cave.
                if(!painted)actor.transform.localScale=Vector3.one*Mathf.Clamp01(1-(position.z-gate.z));
            });
            if(trail)Destroy(trail);
            swimmers.Remove(actor.Mover);linePredecessor.Remove(actor.Mover);
            if(result.exits){world.actors.Remove(id);Destroy(actor.gameObject);}
            else
            {
                var direction=BoardState.Direction(Board.Find(id).facing);var vector=new Vector3(direction.x,0,direction.y);
                // The hit plays the moment the fish touches: effect, sound, dizzy stars and the knock on the fish it ran into,
                // then the short squash-bounce runs on top of it.
                actor.SetSwimming(false);audioPlayer.Play(result.CostsCrab?"reject":"land");
                world.PlayEffect(result.CostsCrab?"CrabHit":"Bump",end+vector*.45f);actor.Dizzy();
                var struck=Board.BlockerOf(id);
                if(struck!=null&&world.actors.TryGetValue(struck.id,out var hit)&&hit)hit.Jolt(vector);
                if(result.CostsCrab)
                {
                    // The crab that took the bump: the one run into, or the crab that did the running.
                    int crab=result.hitsCrab?result.struckId:id;
                    if(result.hitsCrab)world.RefreshCrab(Board,result.struckId);
                    if(result.crabStops)world.RefreshCrab(Board,id);
                    int left=result.hitsCrab&&result.crabStops?Mathf.Min(Board.CrabLeft(id),Board.CrabLeft(result.struckId)):Board.CrabLeft(crab);
                    hud.Hazard();hud.Toast("CRAB!  "+left+" LEFT");if(vibration)Handheld.Vibrate();
                }
                for(float t=0;t<.14f;t+=Time.deltaTime){actor.transform.position=end+vector*Mathf.Sin(t/.14f*Mathf.PI)*.07f;yield return null;}
                actor.transform.position=end;
            }
            if(CurrentLevel==2&&id==stopLessonFish&&!result.exits)hud.instructions.text="Clear a path, then tap it again.";
            moving.Remove(id);Refresh();ResolveBoard();
        }
        private IEnumerator RescueFish(int id)
        {
            if(!Board.Rescue(id))yield break;
            selectingRescue=false;locked=true;world.Highlight(-1);pointer.gameObject.SetActive(false);Refresh();
            var actor=world.actors[id];var original=actor.transform.localScale;
            world.PlayEffect("Rescue",actor.transform.position);audioPlayer.Play("peel");
            for(float t=0;t<.25f;t+=Time.deltaTime){actor.transform.localScale=original*(1-t/.25f);yield return null;}
            world.actors.Remove(id);Destroy(actor.gameObject);locked=false;Refresh();ResolveBoard();
        }
        public const float BombLoseDelay=.9f;
        private IEnumerator LoseAfterBlast()
        {
            var board=Board;locked=true;
            if(vibration)Handheld.Vibrate();
            yield return new WaitForSeconds(BombLoseDelay);
            if(Board!=board)yield break;
            locked=false;ShowLose();
        }
        private void ShowLose()=>hud.Lose(Board.CrabExhausted?"A crab ran out of bumps!":"No way out for the fish!",()=>StartLevel(CurrentLevel),()=>ShowHome());
        private void ResolveBoard()
        {
            // Whatever closes the open dialog resolves the board again.
            if(IsBusy||settings||hud.modal)return;
            if(Board.IsClear){StartCoroutine(ClearStage());return;}
            if(Board.IsFailed)
            {
                playing=false;audioPlayer.Play("fail");pointer.gameObject.SetActive(false);
                // A crab that runs out of bumps sets its bomb off first; the lose screen follows once the blast has read.
                if(Board.CrabExhausted&&world.ExplodeCrabs(Board)){StartCoroutine(LoseAfterBlast());return;}
                ShowLose();return;
            }
            if(Board.IsDeadlocked)ShowDeadlock();
        }
        private IEnumerator ClearStage()
        {
            playing=false;locked=true;pointer.gameObject.SetActive(false);audioPlayer.Play("win");world.Celebrate();
            world.PlayEffect("ClearStars",Vector3.up);world.PlayEffect("Confetti",Vector3.up);
            // Let the celebration play on the empty board; a tap can skip the end of the wait.
            for(float wait=0;wait<.9f;wait+=Time.deltaTime)
            {
                GatherPresses();
                if(wait>=.5f&&presses.Count>0)break;
                yield return null;
            }
            locked=false;
            if(CurrentLevel<FishOutLevels.Count)
            {
                UnlockedLevel=Mathf.Max(UnlockedLevel,CurrentLevel+1);
                PlayerPrefs.SetInt(SavePrefix+"Unlocked",UnlockedLevel);PlayerPrefs.SetInt(SavePrefix+"Level",CurrentLevel+1);PlayerPrefs.Save();
                hud.StageClear(3,ClearCoins(),"","NEXT",()=>StartLevel(CurrentLevel+1));
            }
            else hud.StageClear(3,ClearCoins(),"All stages completed!","HOME",()=>ShowHome());
        }
        // No lives any more (partner feedback 2026-11-11): every clear earns 3 stars and a fixed reward.
        private int ClearCoins()=>150;
        // Coin wallet shown on the win screen; claimed coins are kept between sessions.
        public int Coins=>PlayerPrefs.GetInt(SavePrefix+"Coins",0);
        public void AddCoins(int amount){PlayerPrefs.SetInt(SavePrefix+"Coins",Mathf.Max(0,Coins+amount));PlayerPrefs.Save();}
        private void ShowDeadlock()
        {
            pointer.gameObject.SetActive(false);
            hud.Choice("No moves left","No fish can move or escape."+(Board.RescueUsed?"\nRescue has already been used.":"\nUse Rescue to remove one fish, or retry."),"Retry",()=>ConfirmRetry(),Board.RescueUsed?"Home":"Use Rescue",()=>{if(Board.RescueUsed)ShowHome();else{hud.CloseModal();BeginRescue();}});
        }
        public void UseHint()
        {
            if(!CanUseBooster()||Board.HintUsed)return;
            int id=Board.UseHint();
            if(id<0){hud.Toast("No fish can escape right now.");return;}
            int nearest=NearestEscaping();if(nearest>=0)id=nearest;
            tutorialTarget=-1;hintTarget=id;pointer.gameObject.SetActive(false);world.Highlight(id);hud.Toast("This fish has a clear path to the cave.");Refresh();
        }
        public void BeginRescue()
        {
            if(!CanUseBooster()||Board.RescueUsed)return;
            selectingRescue=!selectingRescue;world.Highlight(-1);pointer.gameObject.SetActive(false);
            hud.Toast(selectingRescue?"Select one fish to rescue. Tap CANCEL to go back.":"Rescue canceled. No booster used.",selectingRescue?60:2);
            Refresh();if(!selectingRescue&&Board.IsDeadlocked)ShowDeadlock();
        }
        private bool CanUseBooster()=>playing&&!locked&&!settings&&!hud.modal&&!Board.IsFailed&&!Board.IsClear;
        public void ConfirmRetry()
        {
            if(locked)return;
            hud.Choice("Retry level "+CurrentLevel+"?","The board, the crabs and both boosters will reset.","Retry",()=>StartLevel(CurrentLevel),"Cancel",()=>{hud.CloseModal();ResolveBoard();});
        }
        public void OpenSettings()
        {
            if(locked||!playing||hud.modal)return;
            settings=true;hud.Settings(sound,music,vibration);
        }
        public void CloseSettings(){settings=false;hud.CloseModal();ResolveBoard();}
        // Home gear: the same screen with only the toggles; closing returns to Home.
        public void OpenHomeSettings(){if(hud.modal)return;hud.Settings(sound,music,vibration,true);}
        public void SetSound(bool value){sound=value;audioPlayer.SetSound(value);SaveSetting("Sound",value);}
        public void SetMusic(bool value){music=value;audioPlayer.SetMusic(value);SaveSetting("Music",value);}
        public void SetVibration(bool value){vibration=value;SaveSetting("Vibration",value);}
        private void SaveSetting(string name,bool value){PlayerPrefs.SetInt(SavePrefix+name,value?1:0);PlayerPrefs.Save();}
        private void SaveLevel(){PlayerPrefs.SetInt(SavePrefix+"Level",CurrentLevel);PlayerPrefs.Save();}
        private void Refresh(){hud.Refresh(Board,CurrentLevel,selectingRescue);}
        public void ConsumeUiInput(){uiInputFrame=Time.frameCount;audioPlayer.Play("button");}
        private void Update()
        {
            if(!hud)return;
            if(screenWidth!=Screen.width||screenHeight!=Screen.height)FitCamera();
            QueueOnRoad();
            if(pointer.gameObject.activeSelf&&world.actors.TryGetValue(tutorialTarget,out var target)&&target)
            {
                var point=boardCamera.WorldToScreenPoint(target.transform.position+Vector3.up*.3f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hud.transform,point,null,out var local);
                // Tap loop: the glove hovers off to the lower right, presses onto the fish, holds a beat, lifts.
                float cycle=Time.unscaledTime*1.1f%1f;
                float press=Mathf.SmoothStep(0,1,cycle<.35f?cycle/.35f:cycle<.55f?1:1-(cycle-.55f)/.45f);
                pointer.rectTransform.anchoredPosition=local+new Vector2(40,-40)*(1-press);
                pointerHand.localScale=Vector3.one*Mathf.Lerp(1.08f,.9f,press);
            }
            if(!playing||locked||settings||hud.modal||uiInputFrame==Time.frameCount)return;
            if(CurrentLevel==1&&!pointer.gameObject.activeSelf&&guidedTaps>=3&&moving.Count==0&&Time.unscaledTime-lastTapTime>4)
            {
                tutorialTarget=NearestEscaping();world.Highlight(tutorialTarget);pointer.gameObject.SetActive(tutorialTarget>=0);lastTapTime=Time.unscaledTime;
            }
            GatherPresses();
            foreach(var position in presses)HandlePress(position);
        }
        // A fish follows whoever is in front of it, so nobody swims through a fish that yielded.
        private void QueueOnRoad()
        {
            foreach(var fish in swimmers)
            {
                fish.Held=false;fish.HoldSpeed=float.MaxValue;
                if(!fish.OnRoad){HoldBeforeJoining(fish);continue;}
                foreach(var other in swimmers)
                {
                    if(other==fish||!other.OnRoad||other.InCave)continue;
                    bool touching=Touching(fish,other);
                    if(!touching&&!Following(fish,other))continue;
                    // Head on, each is in front of the other; the one nearer the gate goes first.
                    if((Touching(other,fish)||Following(other,fish))&&other.Remaining>fish.Remaining)continue;
                    fish.Held=true;fish.HoldSpeed=Mathf.Min(fish.HoldSpeed,touching?0:other.Speed);
                }
                if(SmoothQueue)FollowLikeTraffic(fish);
            }
        }
        // The school stretches and squeezes like traffic instead of stopping and starting: a fish matches the speed of
        // the one ahead on the road and closes or opens the gap gently, so it slows down long before it would touch.
        // Off = the old stop-and-go queue, kept for side-by-side comparison.
        public static bool SmoothQueue=true;
        private const float FollowRange=2.6f,FollowGap=1f,FollowGain=2.4f,FollowRadius=.9f;
        private void FollowLikeTraffic(WaypointMover fish)
        {
            float cell=PaintedBackdrop.CellWorldSize(fish.transform.position);
            foreach(var other in swimmers)
            {
                if(other==fish||!other.OnRoad||other.InCave||other.Remaining>=fish.Remaining)continue;
                float gap=fish.Remaining-other.Remaining;
                if(gap>FollowRange*cell)continue;
                // A fish on this one's route counts, and so does one right beside it where two roads merge;
                // a fish on a road that only merges in later does not.
                bool onRoute=Vector3.Distance(fish.PointAhead(gap),other.transform.position)<FollowRadius*cell;
                bool merging=Vector3.Distance(fish.transform.position,other.transform.position)<FollowGap*cell;
                if(!onRoute&&!merging)continue;
                float speed=Mathf.Max(0,other.Speed+FollowGain*(gap-FollowGap*cell));
                if(speed<fish.HoldSpeed){fish.Held=true;fish.HoldSpeed=speed;}
            }
        }
        // A fish already on the road has the right of way: one about to join waits at the edge
        // while a road fish behind or level with it passes the join point.
        private const float JoinLead=1.6f,JoinRadius=1.35f;
        // Follower → the fish directly in front of it in the line it left with.
        private readonly Dictionary<WaypointMover,WaypointMover> linePredecessor=new Dictionary<WaypointMover,WaypointMover>();
        private void HoldBeforeJoining(WaypointMover fish)
        {
            if(!fish.IsMoving||float.IsInfinity(fish.roadStart))return;
            float cell=PaintedBackdrop.CellWorldSize(fish.transform.position);
            if(fish.ToRoad>JoinLead*cell)return;
            var join=fish.PointAhead(Mathf.Max(0,fish.ToRoad));
            foreach(var other in swimmers)
            {
                if(other==fish||!other.OnRoad||other.Remaining<=fish.Remaining-QueueGap*cell)continue;
                if(Vector3.Distance(other.transform.position,join)<JoinRadius*cell){fish.Held=true;fish.HoldSpeed=0;return;}
            }
        }
        // Looks along this fish's own path, which also catches a fish merging in from the side.
        // Distances are in cells: on the painted board a cell shrinks with perspective.
        private bool Touching(WaypointMover fish,WaypointMover ahead)
        {
            // The fish in front in the same line keeps its exact one-cell spacing, so it is not a crash even round a tight corner.
            if(linePredecessor.TryGetValue(fish,out var front)&&front==ahead)return false;
            float cell=PaintedBackdrop.CellWorldSize(fish.transform.position);
            return Vector3.Distance(fish.PointAhead(QueueNear*cell),ahead.transform.position)<QueueRadius*cell;
        }
        // A line keeps its exact one-cell spacing, so only a closer fish counts this far ahead.
        private static bool Following(WaypointMover fish,WaypointMover ahead)
        {
            float cell=PaintedBackdrop.CellWorldSize(fish.transform.position);
            return fish.Remaining-ahead.Remaining<QueueGap*cell&&Vector3.Distance(fish.PointAhead(QueueFar*cell),ahead.transform.position)<QueueFarRadius*cell;
        }
        // Every finger counts, since several fish may swim at once and two thumbs are natural.
        private void GatherPresses()
        {
            presses.Clear();
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)
                foreach(var touch in Touchscreen.current.touches)if(touch.press.wasPressedThisFrame)presses.Add(touch.position.ReadValue());
            if(presses.Count==0&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)presses.Add(Mouse.current.position.ReadValue());
#else
            for(int i=0;i<Input.touchCount;i++)if(Input.GetTouch(i).phase==TouchPhase.Began)presses.Add(Input.GetTouch(i).position);
            if(presses.Count==0&&Input.GetMouseButtonDown(0))presses.Add(Input.mousePosition);
#endif
        }
        private void HandlePress(Vector2 position)
        {
            if(!boardCamera.pixelRect.Contains(position))return;
            if(EventSystem.current)
            {
                uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},uiHits);
                if(uiHits.Count>0)return;
            }
            var ray=boardCamera.ScreenPointToRay(position);
            var hits=Physics.RaycastAll(ray,100);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                var actor=hit.collider.GetComponentInParent<FishActor>();
                if(actor&&Board.Find(actor.Id)!=null&&!moving.Contains(actor.Id)){TapFish(actor.Id);return;}
            }
            // A tap in the gap between bodies goes to the nearest resting fish.
            if(!new Plane(Vector3.up,Vector3.up*.1f).Raycast(ray,out var enter))return;
            var point=ray.GetPoint(enter);int nearest=-1;float closest=TapForgiveness;
            foreach(var fish in Board.Fish)
            {
                if(moving.Contains(fish.id))continue;
                float distance=Vector3.Distance(world.Cell(fish.cell),point);
                if(distance<closest){closest=distance;nearest=fish.id;}
            }
            if(nearest>=0)TapFish(nearest);
        }
    }
}
