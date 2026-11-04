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
        private int savedLevel,savedSound,savedMusic,savedVibration;
        [UnitySetUp]
        public IEnumerator Load()
        {
            savedLevel=PlayerPrefs.GetInt("FishOut.Level",1);
            savedSound=PlayerPrefs.GetInt("FishOut.Sound",1);savedMusic=PlayerPrefs.GetInt("FishOut.Music",1);savedVibration=PlayerPrefs.GetInt("FishOut.Vibration",1);
            yield return SceneManager.LoadSceneAsync("FishOut");yield return null;
            game=Object.FindFirstObjectByType<FishOutGame>();Assert.IsNotNull(game);Assert.IsNotNull(game.Hud);
            game.SetMusic(false);game.SetSound(false);game.SetVibration(false);
        }
        [UnityTearDown]
        public IEnumerator Restore()
        {
            PlayerPrefs.SetInt("FishOut.Level",savedLevel);PlayerPrefs.SetInt("FishOut.Sound",savedSound);
            PlayerPrefs.SetInt("FishOut.Music",savedMusic);PlayerPrefs.SetInt("FishOut.Vibration",savedVibration);PlayerPrefs.Save();
            yield return null;
        }
        private IEnumerator Idle()
        {
            float timeout=Time.realtimeSinceStartup+5;
            while(game.IsBusy&&Time.realtimeSinceStartup<timeout)yield return null;
            Assert.IsFalse(game.IsBusy,"Animation did not finish");yield return null;
        }
        private void Click(string text)
        {
            var button=game.Hud.modal.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>().text==text);
            button.onClick.Invoke();
        }
        [UnityTest]
        public IEnumerator IllustratedLayoutsLoadAndSpiralFishSwimsThroughGate()
        {
            for(int number=6;number<=9;number++)
            {
                game.StartLevel(number);yield return null;
                Assert.AreEqual(game.Board.Fish.Count,game.World.actors.Count);
                Assert.IsNotNull(GameObject.Find("Sea gate - shared exit"));
                Assert.IsTrue(game.Board.Level.showcase);
            }
            game.StartLevel(8);yield return null;
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
            Assert.IsFalse(game.IsBusy);Assert.AreEqual(count-1,game.Board.Fish.Count);
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
            var actor=game.World.actors[0];var renderer=actor.Visual.GetComponentInChildren<Renderer>();
            var pixel=game.boardCamera.WorldToScreenPoint(renderer.bounds.center);
            Assert.IsTrue(Physics.Raycast(game.boardCamera.ScreenPointToRay(pixel),out var hit,100));
            Assert.AreEqual(0,hit.collider.GetComponentInParent<FishActor>().Id);
            game.TapFish(0);Assert.IsTrue(game.IsBusy);game.TapFish(1);
            Assert.AreEqual(2,game.Board.Fish.Count);yield return Idle();Assert.IsNotNull(game.Board.Find(1));
            game.Hud.hint.onClick.Invoke();Assert.IsTrue(game.Board.HintUsed);
            game.Hud.rescue.onClick.Invoke();game.Hud.rescue.onClick.Invoke();Assert.IsFalse(game.Board.RescueUsed);
            game.Hud.rescue.onClick.Invoke();game.TapFish(1);yield return Idle();Assert.IsTrue(game.Board.RescueUsed);Assert.AreEqual(1,game.Board.Fish.Count);
        }
        [UnityTest]
        public IEnumerator LevelTwoStopPositionSettingsAndRetry()
        {
            game.StartLevel(2);game.TapFish(0);yield return Idle();
            Assert.AreEqual(new Vector2Int(3,2),game.Board.Find(0).cell);Assert.AreEqual(3,game.Board.Lives);
            game.OpenSettings();Assert.IsNotNull(game.Hud.modal);game.SetSound(false);game.SetMusic(false);game.SetVibration(false);
            game.TapFish(1);Assert.AreEqual(10,game.Board.Fish.Count);game.CloseSettings();
            game.ConfirmRetry();Click("RETRY");yield return null;
            Assert.AreEqual(new Vector2Int(0,2),game.Board.Find(0).cell);Assert.AreEqual(3,game.Board.Lives);
            Assert.IsFalse(game.Board.HintUsed);Assert.IsFalse(game.Board.RescueUsed);
        }
        [UnityTest]
        public IEnumerator CrabWarningBlocksMoveThenThreeContactsFail()
        {
            game.StartLevel(4);game.TapFish(0);Assert.IsNotNull(game.Hud.modal);Assert.AreEqual(3,game.Board.Lives);Assert.IsFalse(game.IsBusy);
            game.TapFish(0);Assert.AreEqual(3,game.Board.Lives);Click("GOT IT");
            for(int i=0;i<3;i++){game.TapFish(0);yield return Idle();Assert.AreEqual(2-i,game.Board.Lives);}
            Assert.IsTrue(game.Board.IsFailed);Assert.IsFalse(game.IsPlayingStage);Assert.IsNotNull(game.Hud.modal);
            game.TapFish(1);Assert.AreEqual(20,game.Board.Fish.Count);
            Click("RETRY");Click("RETRY");yield return null;Assert.AreEqual(3,game.Board.Lives);
        }
        [UnityTest]
        public IEnumerator AllFiveStagesClearThroughActualControllerAndNextButtons()
        {
            game.StartLevel(1);
            for(int number=1;number<=5;number++)
            {
                int safety=30;
                while(!game.Board.IsClear&&safety-->0)
                {
                    int id=game.Board.FindEscapingFish();
                    if(id<0)
                    {
                        Assert.AreEqual(1,game.Board.Fish.Count);
                        if(game.Hud.modal)Click("USE RESCUE");else game.BeginRescue();
                        game.TapFish(game.Board.Fish[0].id);
                    }
                    else game.TapFish(id);
                    yield return Idle();
                }
                Assert.IsTrue(game.Board.IsClear,"Level "+number);Assert.IsNotNull(game.Hud.modal);Assert.AreEqual(3,game.Board.Lives);
                if(number<5){Click("NEXT");yield return null;Assert.AreEqual(number+1,game.CurrentLevel);}
            }
        }
    }
}
