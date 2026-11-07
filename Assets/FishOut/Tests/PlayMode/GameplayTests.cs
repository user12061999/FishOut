using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishOut.Tests
{
    public class GameplayTests
    {
        private FishOutGame game;
        private int savedLevel,savedUnlocked,savedSound,savedMusic,savedVibration;
        [UnitySetUp]
        public IEnumerator Load()
        {
            savedLevel=PlayerPrefs.GetInt("FishOut.Level",1);savedUnlocked=PlayerPrefs.GetInt("FishOut.Unlocked",1);
            PlayerPrefs.SetInt("FishOut.Unlocked",1);PlayerPrefs.SetInt("FishOut.Level",1);
            savedSound=PlayerPrefs.GetInt("FishOut.Sound",1);savedMusic=PlayerPrefs.GetInt("FishOut.Music",1);savedVibration=PlayerPrefs.GetInt("FishOut.Vibration",1);
            yield return SceneManager.LoadSceneAsync("FishOut");yield return null;
            game=Object.FindFirstObjectByType<FishOutGame>();Assert.IsNotNull(game);Assert.IsNotNull(game.Hud);
            game.SetMusic(false);game.SetSound(false);game.SetVibration(false);
        }
        [UnityTearDown]
        public IEnumerator Restore()
        {
            PlayerPrefs.SetInt("FishOut.Level",savedLevel);PlayerPrefs.SetInt("FishOut.Unlocked",savedUnlocked);PlayerPrefs.SetInt("FishOut.Sound",savedSound);
            PlayerPrefs.SetInt("FishOut.Music",savedMusic);PlayerPrefs.SetInt("FishOut.Vibration",savedVibration);PlayerPrefs.Save();
            yield return null;
        }
        private IEnumerator Idle()
        {
            float timeout=Time.realtimeSinceStartup+15;
            while(game.IsBusy&&Time.realtimeSinceStartup<timeout)yield return null;
            Assert.IsFalse(game.IsBusy,"Animation did not finish");yield return null;
        }
        private void Click(string text)
        {
            // Built-in dialogs label buttons with Text; the stage clear popup names its button after the label.
            var button=game.Hud.modal.GetComponentsInChildren<Button>().First(b=>{var label=b.GetComponentInChildren<Text>();return label!=null?label.text==text:b.name=="Button "+text;});
            button.onClick.Invoke();
        }
        [UnityTest]
        public IEnumerator EveryStageLoadsAndSpiralFishSwimsThroughGate()
        {
            for(int number=1;number<=FishOutLevels.Count;number++)
            {
                game.StartLevel(number);yield return null;
                Assert.AreEqual(game.Board.Fish.Count,game.World.actors.Count);
                Assert.IsNotNull(GameObject.Find("Sea gate - shared exit"));
                Assert.IsTrue(game.Board.Level.showcase);
            }
            int chosen=-1;MovementPath longest=null;
            foreach(var fish in game.Board.Fish)
            {
                if(!game.Board.Preview(fish.id).exits)continue;
                var actor=game.World.actors[fish.id];
                var path=MovementPath.FromBoard(game.Board.CalculatePath(fish.id),game.World.Cell,game.boardCamera,actor.VisibleBounds());
                if(longest==null||path.Length>longest.Length){chosen=fish.id;longest=path;}
            }
            Assert.GreaterOrEqual(chosen,0);Assert.Greater(longest.Waypoints.Count,10);
            var swimmer=game.World.actors[chosen];int count=game.Board.Fish.Count;game.TapFish(chosen);
            Assert.IsTrue(game.IsBusy);Assert.IsTrue(swimmer.Mover.orientAlongPath);
            float deadline=Time.realtimeSinceStartup+20;
            while(game.IsBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(game.IsBusy);Assert.Less(game.Board.Fish.Count,count);
            Assert.IsFalse(game.World.actors.ContainsKey(chosen));
        }
        [UnityTest]
        public IEnumerator WaypointFollowerTraversesCornersWithoutTeleporting()
        {
            var go=new GameObject("Waypoint motion test");
            try
            {
                var mover=go.AddComponent<WaypointMover>();mover.maximumSpeed=5;mover.acceleration=15;
                var path=new MovementPath(new[]{Vector3.zero,new Vector3(2,0,0),new Vector3(2,0,2)});
                var running=mover.StartCoroutine(mover.FollowPath(path,true));
                float deadline=Time.realtimeSinceStartup+5;var previous=go.transform.position;int samples=0;
                while(mover.IsMoving&&Time.realtimeSinceStartup<deadline)
                {
                    yield return null;var point=go.transform.position;
                    Assert.LessOrEqual(Vector3.Distance(previous,point),mover.maximumSpeed*Time.deltaTime+.002f);
                    Assert.IsTrue(Mathf.Abs(point.z)<.001f||Mathf.Abs(point.x-2)<.001f,"Movement cut across the corner");
                    Assert.GreaterOrEqual(point.x+point.z,previous.x+previous.z-.001f);
                    previous=point;samples++;
                }
                yield return running;Assert.IsFalse(mover.IsMoving);Assert.Greater(samples,2);
                Assert.AreEqual(path.End,go.transform.position);
            }
            finally { Object.Destroy(go); }
        }
        [UnityTest]
        public IEnumerator HitTargetsTapLockAndBoosterCancellation()
        {
            Assert.IsTrue(game.Hud.home.activeSelf);game.StartLevel(1);yield return null;
            int count=game.Board.Fish.Count,id=game.Board.FindEscapingFish();
            int other=game.Board.Fish.First(f=>f.id!=id).id;
            var actor=game.World.actors[id];var renderer=actor.Visual.GetComponentInChildren<Renderer>();
            var pixel=game.boardCamera.WorldToScreenPoint(renderer.bounds.center);
            Assert.IsTrue(Physics.Raycast(game.boardCamera.ScreenPointToRay(pixel),out var hit,100));
            Assert.IsNotNull(hit.collider.GetComponentInParent<FishActor>());
            // Spec §4: a tap is ignored while a fish moves on the board, and accepted again once it is on the road.
            game.TapFish(id);Assert.IsTrue(game.IsBusy);
            int afterFirst=game.Board.Fish.Count;Assert.Less(afterFirst,count);
            int second=game.Board.FindEscapingFish();Assert.GreaterOrEqual(second,0);
            Assert.IsTrue(game.FishMovingOnBoard);game.TapFish(second);Assert.AreEqual(afterFirst,game.Board.Fish.Count,"Tap ignored while a fish moves on the board");
            float wait=Time.realtimeSinceStartup+5;while(game.FishMovingOnBoard&&Time.realtimeSinceStartup<wait)yield return null;
            Assert.IsFalse(game.FishMovingOnBoard);
            game.TapFish(second);Assert.Less(game.Board.Fish.Count,afterFirst);Assert.IsTrue(game.IsBusy);
            yield return Idle();Assert.IsFalse(game.World.actors.ContainsKey(id));Assert.IsFalse(game.World.actors.ContainsKey(second));
            other=game.Board.Fish[0].id;int beforeRescue=game.Board.Fish.Count;
            game.Hud.hint.onClick.Invoke();Assert.IsTrue(game.Board.HintUsed);
            game.Hud.rescue.onClick.Invoke();game.Hud.rescue.onClick.Invoke();Assert.IsFalse(game.Board.RescueUsed);
            game.Hud.rescue.onClick.Invoke();game.TapFish(other);yield return Idle();Assert.IsTrue(game.Board.RescueUsed);Assert.AreEqual(beforeRescue-1,game.Board.Fish.Count);
        }
        [UnityTest]
        public IEnumerator LineStaysTogetherAndRetryWorksMidSwim()
        {
            System.Collections.Generic.List<int> line=null;
            for(int number=1;number<=FishOutLevels.Count&&line==null;number++)
            {
                game.StartLevel(number);yield return null;
                foreach(var fish in game.Board.Fish){var found=game.Board.EscapingLine(fish.id);if(found.Count>=2){line=found;break;}}
            }
            Assert.IsNotNull(line,"Some stage must contain a line");
            var head=game.World.actors[line[0]];var next=game.World.actors[line[1]];
            float start=Vector3.Distance(head.transform.position,next.transform.position);
            game.TapFish(line[0]);Assert.IsTrue(game.IsBusy);
            Assert.IsNull(game.Board.Find(line[1]),"The whole line leaves together");
            float deadline=Time.realtimeSinceStartup+3;float widest=0;
            while(head&&next&&Time.realtimeSinceStartup<deadline)
            {
                widest=Mathf.Max(widest,Vector3.Distance(head.transform.position,next.transform.position));
                yield return null;
            }
            // Cells on the painted board vary in size with perspective, so compare with the starting gap.
            Assert.LessOrEqual(widest,start*1.3f+.2f,"Fish in a line must stay close");
            game.StartLevel(1);yield return null;
            int id=game.Board.FindEscapingFish();game.TapFish(id);Assert.IsTrue(game.IsBusy);
            game.ConfirmRetry();Assert.IsNotNull(game.Hud.modal,"Retry opens while a fish swims");
            yield return Idle();Assert.IsNotNull(game.Hud.modal,"Finishing a swim keeps the dialog open");
            Click("CANCEL");Assert.IsNull(game.Hud.modal);
            game.TapFish(game.Board.FindEscapingFish());game.OpenSettings();Assert.IsNotNull(game.Hud.modal,"Settings open while a fish swims");
            game.CloseSettings();yield return Idle();
        }
        [UnityTest]
        public IEnumerator LevelSelectOpensTheChosenLevel()
        {
            game.OpenLevelSelect();Assert.IsNotNull(game.Hud.modal);
            Click("BACK");Assert.IsNull(game.Hud.modal);
            game.OpenLevelSelect();
            var buttons=game.Hud.modal.GetComponentsInChildren<Button>();
            Assert.AreEqual(FishOutLevels.Count+1,buttons.Length);
            foreach(var button in buttons)
            {
                var text=button.GetComponentInChildren<Text>().text;
                if(text!="BACK")Assert.AreEqual(text=="1",button.interactable,"Only the first level starts unlocked");
            }
            Click("1");yield return null;
            Assert.AreEqual(1,game.CurrentLevel);Assert.IsTrue(game.IsPlayingStage);Assert.IsNull(game.Hud.modal);
        }
        [UnityTest]
        public IEnumerator LevelTwoStopPositionSettingsAndRetry()
        {
            game.StartLevel(2);
            int id=game.Board.Fish.First(f=>{var m=game.Board.Preview(f.id);return m.ChangesBoard&&!m.exits;}).id;
            var start=game.Board.Find(id).cell;var stop=game.Board.Preview(id).to;int count=game.Board.Fish.Count;
            game.TapFish(id);yield return Idle();
            Assert.AreEqual(stop,game.Board.Find(id).cell);Assert.AreEqual(3,game.Board.Lives);
            game.OpenSettings();Assert.IsNotNull(game.Hud.modal);game.SetSound(false);game.SetMusic(false);game.SetVibration(false);
            game.TapFish(game.Board.FindEscapingFish());Assert.AreEqual(count,game.Board.Fish.Count);game.CloseSettings();
            game.ConfirmRetry();Click("RETRY");yield return null;
            Assert.AreEqual(start,game.Board.Find(id).cell);Assert.AreEqual(3,game.Board.Lives);
            Assert.IsFalse(game.Board.HintUsed);Assert.IsFalse(game.Board.RescueUsed);
        }
        [UnityTest]
        public IEnumerator CrabWarningThenEveryBumpCostsALife()
        {
            game.StartLevel(4);int id=game.Board.Fish.First(f=>game.Board.Preview(f.id).hitsCrab).id;
            game.TapFish(id);Assert.IsNotNull(game.Hud.modal);Assert.AreEqual(3,game.Board.Lives);Assert.IsFalse(game.IsBusy);
            Click("GOT IT");Assert.IsNull(game.Hud.modal);
            game.TapFish(id);yield return Idle();Assert.AreEqual(2,game.Board.Lives);
            Assert.IsTrue(game.Board.Preview(id).hitsCrab,"Spec: the crab stays and keeps blocking");
        }
        [UnityTest]
        public IEnumerator BlockedTapChangesNothingAndKeepsTheGuide()
        {
            game.StartLevel(3);yield return null;
            var blocked=game.Board.Fish.First(f=>!game.Board.Preview(f.id).ChangesBoard);
            var start=blocked.cell;int count=game.Board.Fish.Count;
            game.TapFish(blocked.id);yield return Idle();
            Assert.AreEqual(start,game.Board.Find(blocked.id).cell);Assert.AreEqual(count,game.Board.Fish.Count);
            Assert.AreEqual(game.World.actors[blocked.id].transform.position,game.World.Cell(start));
        }
        [UnityTest]
        public IEnumerator ThreeCrabTollsEndTheStageAndRetryStartsOver()
        {
            game.StartLevel(5);yield return null;
            var payer=game.Board.Fish.First(f=>game.Board.Preview(f.id).hitsCrab);
            for(int paid=1;paid<=3;paid++){game.TapFish(payer.id);yield return Idle();Assert.AreEqual(3-paid,game.Board.Lives);}
            Assert.IsTrue(game.Board.IsFailed);Assert.IsFalse(game.IsPlayingStage);Assert.IsNotNull(game.Hud.modal);
            Click("RETRY");yield return null;Assert.AreEqual(3,game.Board.Lives);Assert.IsTrue(game.IsPlayingStage);
        }
        [UnityTest]
        public IEnumerator SwimmersOnTheRoadKeepTheirDistance()
        {
            game.StartLevel(3);yield return null;
            // Taps wait for the board to be still (spec §4), so fish still reach the road one after another.
            for(int i=0;i<8;i++)
            {
                float wait=Time.realtimeSinceStartup+5;while(game.FishMovingOnBoard&&Time.realtimeSinceStartup<wait)yield return null;
                int id=game.Board.FindEscapingFish();if(id>=0)game.TapFish(id);
            }
            float deadline=Time.realtimeSinceStartup+9,closest=float.PositiveInfinity;string detail="";
            while(game.IsBusy&&Time.realtimeSinceStartup<deadline)
            {
                // Deep in the painted cave a fish is fading out of sight and may be overtaken.
                var road=game.World.actors.Values.Where(a=>a&&a.Mover.OnRoad&&!a.Mover.InCave&&a.Mover.Remaining>1.2f).ToList();
                for(int a=0;a<road.Count;a++)for(int b=a+1;b<road.Count;b++){float gap=Vector3.Distance(road[a].transform.position,road[b].transform.position);if(gap<closest){closest=gap;detail=road[a].Id+" rem="+road[a].Mover.Remaining+" held="+road[a].Mover.Held+" pos="+road[a].transform.position+" fwd="+road[a].transform.forward+" | "+road[b].Id+" rem="+road[b].Mover.Remaining+" held="+road[b].Mover.Held+" pos="+road[b].transform.position+" fwd="+road[b].transform.forward+" t="+Time.realtimeSinceStartup;}}
                yield return null;
            }
            Assert.IsFalse(game.IsBusy,"The queue must drain");
            // A fish merging onto the road may brush the one passing it for a moment.
            Assert.GreaterOrEqual(closest,.4f,"Fish overlap on the road: "+detail);
        }
        [UnityTest]
        public IEnumerator EveryStageClearsThroughTheControllerAndNextButtons()
        {
            game.StartLevel(1);
            for(int number=1;number<=FishOutLevels.Count;number++)
            {
                // Rules alone clear the school; only the final fish plays its full animation.
                // Only a fish facing a crab can be stuck; the free Rescue takes it away first.
                var trapped=game.Board.Fish.FirstOrDefault(f=>game.Board.Preview(f.id).hitsCrab);
                if(trapped!=null)Assert.IsTrue(game.Board.Rescue(trapped.id));
                for(int guard=0;guard<1000&&game.Board.Fish.Count>1;guard++)
                {
                    int id=game.Board.FindEscapingFish();Assert.GreaterOrEqual(id,0,"Level "+number+" is stuck");
                    game.Board.Move(id);
                }
                game.TapFish(game.Board.Fish[0].id);
                yield return Idle();
                Assert.IsTrue(game.Board.IsClear,"Level "+number);Assert.IsNotNull(game.Hud.modal);Assert.GreaterOrEqual(game.Board.Lives,1);
                if(number<FishOutLevels.Count){Assert.AreEqual(number+1,game.UnlockedLevel);Click("NEXT");yield return null;Assert.AreEqual(number+1,game.CurrentLevel);}
            }
        }
    }
}
