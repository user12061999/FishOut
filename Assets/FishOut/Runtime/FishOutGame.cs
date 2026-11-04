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
        public bool IsBusy { get; private set; }
        public bool IsPlayingStage => playing;
        public FishOutHud Hud => hud;
        public FishOutWorld World => world;
        private FishOutHud hud;
        private FishOutWorld world;
        private FishOutAudio audioPlayer;
        private bool playing, selectingRescue, settings, crabWarned, settingsHadDeadlock;
        private bool sound=true,music=true,vibration=true;
        private int tutorialTarget=-1;
        private Text pointer;
        private int screenWidth,screenHeight;
        private int uiInputFrame=-1;
        private readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        private const string SavePrefix="FishOut.";

        private void Start()
        {
            Application.targetFrameRate=60; Screen.orientation=ScreenOrientation.Portrait;
            CurrentLevel=Mathf.Clamp(PlayerPrefs.GetInt(SavePrefix+"Level",1),1,FishOutLevels.Count);
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
            if(!boardCamera||fishPrefabs==null||fishPrefabs.Length==0||!crabPrefab)
            { Debug.LogError("Fish Out scene has missing references. Run Fish Out > Create or update game scene.");enabled=false;return; }
            var audioObject=new GameObject("Game audio"); audioObject.transform.SetParent(transform,false);
            audioPlayer=audioObject.AddComponent<FishOutAudio>(); audioPlayer.Initialize(sound,music);
            var canvas=new GameObject("Fish Out UI",typeof(RectTransform)); canvas.transform.SetParent(transform,false);
            hud=canvas.AddComponent<FishOutHud>(); hud.Build(this,uiFont?uiFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            pointer=hud.Label(canvas.transform,"TAP",new Vector2(.5f,.5f),new Vector2(150,70),30,new Color(1,.91f,.38f));pointer.gameObject.SetActive(false);
            if(!EventSystem.current)
            {
                var events=new GameObject("EventSystem",typeof(EventSystem)); events.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            ShowHome();
        }
        private void RebuildBoard()
        {
            StopAllCoroutines(); IsBusy=false;selectingRescue=false;settings=false;tutorialTarget=-1;pointer.gameObject.SetActive(false);
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
                boardCamera.rect=new Rect(0,.16f,1,.67f);
                aspect=Mathf.Max(.1f,((float)Screen.width/Mathf.Max(1,Screen.height))/.67f);
                boardCamera.orthographicSize=Mathf.Max((Board.Level.height*.8f+6f)*.5f,(Board.Level.width+3.5f)*.5f/aspect);
                boardCamera.transform.position=new Vector3(0,20,-15);boardCamera.transform.LookAt(new Vector3(0,.2f,1.8f));
            }
            boardCamera.clearFlags=CameraClearFlags.SolidColor;boardCamera.backgroundColor=new Color(.035f,.22f,.29f);
        }
        public void ShowHome()
        {
            Time.timeScale=1;playing=false;RebuildBoard();hud.SetHome(true,CurrentLevel);hud.instructions.text="";
        }
        public void StartLevel(int number)
        {
            CurrentLevel=Mathf.Clamp(number,1,FishOutLevels.Count);SaveLevel();
            playing=true;RebuildBoard();hud.SetHome(false,CurrentLevel);crabWarned=false;
            switch(CurrentLevel)
            {
                case 1: hud.instructions.text="Tap a fish to help it escape to the sea.";tutorialTarget=Board.FindEscapingFish();break;
                case 2: hud.instructions.text="Tap the highlighted fish. It stops before another fish.";tutorialTarget=0;break;
                case 3: hud.instructions.text="Rocks block the way. Find the right order.";break;
                case 4: hud.instructions.text="Watch out! Touching a crab costs one life.";break;
                default:hud.instructions.text="Clear a path and help every fish reach the sea.";break;
            }
            world.Highlight(tutorialTarget);pointer.gameObject.SetActive(tutorialTarget>=0);Refresh();
            if(Board.Level.showcase)hud.instructions.text="";
        }
        public void TapFish(int id)
        {
            if(!playing||IsBusy||settings||hud.modal||Board.IsFailed||Board.IsClear||Board.Find(id)==null)return;
            if(selectingRescue){StartCoroutine(RescueFish(id));return;}
            var move=Board.Preview(id);
            if(CurrentLevel==4&&move.hitsCrab&&!crabWarned)
            {
                world.Highlight(id);
                hud.Choice("WATCH OUT!","Crabs cost 1 life each time a fish reaches them. You have 3 lives.\nThis tap has not moved the fish.","GOT IT",()=>{crabWarned=true;world.Highlight(-1);hud.CloseModal();});
                return;
            }
            StartCoroutine(MoveFish(id));
        }
        private IEnumerator MoveFish(int id)
        {
            IsBusy=true;world.Highlight(-1);pointer.gameObject.SetActive(false);Refresh();
            var actor=world.actors[id];
            actor.Mover.orientAlongPath=Board.Level.showcase;
            var plan=Board.CalculatePath(id);
            var path=MovementPath.FromBoard(plan,world.Cell,boardCamera,actor.VisibleBounds());
            var result=Board.Move(id);
            actor.SetSwimming(result.ChangesBoard);
            var end=path.End;
            actor.Mover.maximumSpeed=result.exits?12f:7f;
            audioPlayer.Play(result.ChangesBoard?"swim":"bump");float bubbleClock=0;
            yield return actor.Mover.FollowPath(path,!result.exits,position=>
            {
                bubbleClock+=Time.deltaTime;
                if(result.ChangesBoard&&bubbleClock>.08f){world.Bubbles(position,2);bubbleClock=0;}
            });
            if(result.exits){world.Bubbles(end,5);world.actors.Remove(id);Destroy(actor.gameObject);}
            else
            {
                var direction=BoardState.Direction(Board.Find(id).facing);var vector=new Vector3(direction.x,0,direction.y);
                for(float t=0;t<.18f;t+=Time.deltaTime){actor.transform.position=end+vector*Mathf.Sin(t/.18f*Mathf.PI)*.07f;yield return null;}
                actor.transform.position=end;actor.SetSwimming(false);audioPlayer.Play(result.hitsCrab?"danger":"bump");
            }
            if(result.hitsCrab){hud.Hazard();hud.Toast("CRAB!  -1 LIFE");if(vibration)Handheld.Vibrate();}
            if(CurrentLevel==1){tutorialTarget=Board.FindEscapingFish();world.Highlight(tutorialTarget);pointer.gameObject.SetActive(tutorialTarget>=0);}
            if(CurrentLevel==2&&id==0&&!result.exits)hud.instructions.text="Fish stay where they stop. Clear a path, then tap again.";
            IsBusy=false;Refresh();ResolveBoard();
        }
        private IEnumerator RescueFish(int id)
        {
            if(!Board.Rescue(id))yield break;
            selectingRescue=false;IsBusy=true;world.Highlight(-1);pointer.gameObject.SetActive(false);Refresh();
            var actor=world.actors[id];var original=actor.transform.localScale;
            world.Bubbles(actor.transform.position,14);audioPlayer.Play("swim");
            for(float t=0;t<.25f;t+=Time.deltaTime){actor.transform.localScale=original*(1-t/.25f);yield return null;}
            world.actors.Remove(id);Destroy(actor.gameObject);IsBusy=false;Refresh();ResolveBoard();
        }
        private void ResolveBoard()
        {
            if(Board.IsClear){StartCoroutine(ClearStage());return;}
            if(Board.IsFailed)
            {
                playing=false;hud.Choice("OUT OF LIVES",Board.Fish.Count+" fish are still waiting.","RETRY",()=>ConfirmRetry(),"HOME",()=>ShowHome());return;
            }
            if(Board.IsDeadlocked)ShowDeadlock();
        }
        private IEnumerator ClearStage()
        {
            playing=false;IsBusy=true;pointer.gameObject.SetActive(false);audioPlayer.Play("clear");world.Celebrate();
            for(int i=0;i<6;i++){world.Bubbles(new Vector3(Random.Range(-2.5f,2.5f),.3f,Random.Range(-3,3)),7);yield return new WaitForSeconds(.055f);}
            IsBusy=false;
            if(CurrentLevel<FishOutLevels.Count)
            {
                PlayerPrefs.SetInt(SavePrefix+"Level",CurrentLevel+1);PlayerPrefs.Save();
                hud.Choice("CLEAR","All fish are free!\nCoins earned: 0 (placeholder)","NEXT",()=>StartLevel(CurrentLevel+1));
            }
            else hud.Choice("CLEAR","All fish are free!\nCoins earned: 0 (placeholder)\nAll stages completed.","HOME",()=>ShowHome());
        }
        private void ShowDeadlock()
        {
            pointer.gameObject.SetActive(false);
            hud.Choice("NO MOVES LEFT","No fish can move or escape."+(Board.RescueUsed?"\nRescue has already been used.":"\nUse Rescue to remove one fish, or retry."),"RETRY",()=>ConfirmRetry(),Board.RescueUsed?"HOME":"USE RESCUE",()=>{if(Board.RescueUsed)ShowHome();else{hud.CloseModal();BeginRescue();}});
        }
        public void UseHint()
        {
            if(!CanUseBooster()||Board.HintUsed)return;
            int id=Board.UseHint();
            if(id<0){hud.Toast("No fish can escape right now.");return;}
            tutorialTarget=-1;pointer.gameObject.SetActive(false);world.Highlight(id);hud.Toast("This fish has a clear path to the sea.");Refresh();
        }
        public void OpenShowcaseLevels()
        {
            var panel=hud.ShowModal("LEVEL PREVIEWS","Four illustrated reef layouts");
            var names=new[]{"6  RAINBOW REEF","7  S-CURVE","8  SPIRAL LAGOON","9  TWO TONE"};
            for(int i=0;i<4;i++)
            {
                int number=6+i;
                hud.Button(panel,names[i],new Vector2(.5f,.49f-i*.115f),new Vector2(760,80),FishOutHud.Teal,()=>StartLevel(number),28);
            }
            hud.Button(panel,"BACK",new Vector2(.5f,.05f),new Vector2(300,65),new Color(.30f,.46f,.50f),()=>hud.CloseModal(),25);
        }
        public void BeginRescue()
        {
            if(!CanUseBooster()||Board.RescueUsed)return;
            selectingRescue=!selectingRescue;world.Highlight(-1);pointer.gameObject.SetActive(false);
            hud.Toast(selectingRescue?"Select one fish to rescue. Tap CANCEL to go back.":"Rescue canceled. No booster used.",selectingRescue?60:2);
            Refresh();if(!selectingRescue&&Board.IsDeadlocked)ShowDeadlock();
        }
        private bool CanUseBooster()=>playing&&!IsBusy&&!settings&&!hud.modal&&!Board.IsFailed&&!Board.IsClear;
        public void ConfirmRetry()
        {
            if(IsBusy)return;
            bool failed=Board.IsFailed;
            hud.Choice("RETRY LEVEL "+CurrentLevel+"?","The board and both boosters will reset. You will start with 3 lives.","RETRY",()=>StartLevel(CurrentLevel),"CANCEL",()=>{hud.CloseModal();if(failed)ResolveBoard();else if(Board.IsDeadlocked)ShowDeadlock();});
        }
        public void OpenSettings()
        {
            if(IsBusy||!playing||hud.modal)return;
            settings=true;settingsHadDeadlock=Board.IsDeadlocked;hud.Settings(sound,music,vibration);
        }
        public void CloseSettings(){settings=false;hud.CloseModal();if(settingsHadDeadlock)ShowDeadlock();}
        public void SetSound(bool value){sound=value;audioPlayer.SetSound(value);SaveSetting("Sound",value);}
        public void SetMusic(bool value){music=value;audioPlayer.SetMusic(value);SaveSetting("Music",value);}
        public void SetVibration(bool value){vibration=value;SaveSetting("Vibration",value);}
        private void SaveSetting(string name,bool value){PlayerPrefs.SetInt(SavePrefix+name,value?1:0);PlayerPrefs.Save();}
        private void SaveLevel(){PlayerPrefs.SetInt(SavePrefix+"Level",CurrentLevel);PlayerPrefs.Save();}
        private void Refresh(){hud.Refresh(Board,CurrentLevel,selectingRescue);}
        public void ConsumeUiInput(){uiInputFrame=Time.frameCount;}
        private void OnApplicationPause(bool paused)
        {
            // The specification restarts an interrupted stage from its initial state.
            if(paused&&hud&&playing)StartLevel(CurrentLevel);
        }
        private void Update()
        {
            if(!hud)return;
            if(screenWidth!=Screen.width||screenHeight!=Screen.height)FitCamera();
            if(pointer.gameObject.activeSelf&&world.actors.TryGetValue(tutorialTarget,out var target)&&target)
            {
                var point=boardCamera.WorldToScreenPoint(target.transform.position+Vector3.up*.8f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hud.transform,point,null,out var local);
                pointer.rectTransform.anchoredPosition=local+Vector2.up*(24+Mathf.Sin(Time.unscaledTime*5)*8);
            }
            if(!playing||IsBusy||settings||hud.modal||uiInputFrame==Time.frameCount)return;
            Vector2 position=default;bool pressed=false;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null&&Touchscreen.current.primaryTouch.press.wasPressedThisFrame){position=Touchscreen.current.primaryTouch.position.ReadValue();pressed=true;}
            else if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame){position=Mouse.current.position.ReadValue();pressed=true;}
#else
            if(Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began){position=Input.GetTouch(0).position;pressed=true;}
            else if(Input.GetMouseButtonDown(0)){position=Input.mousePosition;pressed=true;}
#endif
            if(!pressed||!boardCamera.pixelRect.Contains(position))return;
            if(EventSystem.current)
            {
                uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},uiHits);
                if(uiHits.Count>0)return;
            }
            var ray=boardCamera.ScreenPointToRay(position);
            if(Physics.Raycast(ray,out var hit,100))
            {
                var actor=hit.collider.GetComponentInParent<FishActor>();
                if(actor&&Board.Find(actor.Id)!=null&&Board.Inside(Board.Find(actor.Id).cell))TapFish(actor.Id);
            }
        }
    }
}
