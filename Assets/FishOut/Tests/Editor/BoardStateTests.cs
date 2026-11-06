using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FishOut.Tests
{
    public class BoardStateTests
    {
        private LevelData Level(params FishData[] fish)
        { var l=new LevelData();l.fish.AddRange(fish);return l; }
        private FishData Fish(int id,int x,int y,Facing facing)=>new FishData{id=id,cell=new Vector2Int(x,y),facing=facing};
        [TestCase(Facing.Up,2,7)] [TestCase(Facing.Right,5,2)]
        [TestCase(Facing.Down,2,0)] [TestCase(Facing.Left,0,2)]
        public void AllFourEdgesAllowEscape(Facing direction,int x,int y)
        {
            var b=new BoardState(Level(Fish(0,x,y,direction)));
            Assert.IsTrue(b.Move(0).exits);Assert.IsTrue(b.IsClear);
        }
        [Test]
        public void StopsBeforeFishAndRetainsPositionWithoutLosingLife()
        {
            var b=new BoardState(Level(Fish(0,0,2,Facing.Right),Fish(1,4,2,Facing.Up)));
            var move=b.Move(0);Assert.AreEqual(new Vector2Int(3,2),move.to);
            Assert.AreEqual(move.to,b.Find(0).cell);Assert.AreEqual(3,b.Lives);
            Assert.IsFalse(b.Move(0).ChangesBoard);b.Move(1);Assert.IsTrue(b.Move(0).exits);
        }
        [Test]
        public void RockNeverMovesNeverBreaksAndNeverCostsLife()
        {
            var l=Level(Fish(0,0,2,Facing.Right));l.obstacles.Add(new ObstacleData{cell=new Vector2Int(3,2),kind=ObstacleKind.Rock});
            var b=new BoardState(l);Assert.IsTrue(b.Move(0).hitsRock);Assert.AreEqual(new Vector2Int(2,2),b.Find(0).cell);
            var again=b.Move(0);Assert.IsTrue(again.hitsRock);Assert.IsFalse(again.ChangesBoard,"Spec: a rock is fixed and cannot be removed");
            Assert.IsTrue(b.IsDeadlocked);Assert.AreEqual(3,b.Lives);Assert.IsFalse(b.Rescue(999));Assert.IsFalse(b.RescueUsed);
        }
        [Test]
        public void CrabCostsALifeEveryTimeAndNeverLetsAFishPass()
        {
            var l=Level(Fish(0,0,2,Facing.Right));l.obstacles.Add(new ObstacleData{cell=new Vector2Int(3,2),kind=ObstacleKind.Crab});
            var b=new BoardState(l);
            var first=b.Move(0);Assert.IsTrue(first.hitsCrab);Assert.AreEqual(2,b.Lives);Assert.AreEqual(new Vector2Int(2,2),b.Find(0).cell);
            Assert.IsTrue(b.IsDeadlocked,"Nothing can move: offer retry or a booster");
            var second=b.Move(0);Assert.IsTrue(second.hitsCrab);Assert.IsFalse(second.exits);Assert.AreEqual(1,b.Lives);Assert.IsFalse(b.IsClear);
            Assert.IsTrue(b.Rescue(0));Assert.IsTrue(b.IsClear);
            Assert.AreEqual(3,new BoardState(l).Lives);Assert.AreEqual(new Vector2Int(0,2),new BoardState(l).Find(0).cell);
        }
        [Test]
        public void ThreeCrabBumpsFail()
        {
            var l=Level(Fish(0,0,1,Facing.Right),Fish(1,0,3,Facing.Right),Fish(2,0,5,Facing.Right));
            foreach(var y in new[]{1,3,5})l.obstacles.Add(new ObstacleData{cell=new Vector2Int(1,y),kind=ObstacleKind.Crab});
            var b=new BoardState(l);Assert.IsTrue(b.IsDeadlocked);
            b.Move(0);b.Move(1);b.Move(2);Assert.AreEqual(0,b.Lives);Assert.IsTrue(b.IsFailed);
            Assert.Throws<System.InvalidOperationException>(()=>b.Move(0));
        }
        [Test]
        public void OnlyTheFirstBlockerCanCauseDamage()
        {
            var l=Level(Fish(0,0,2,Facing.Right),Fish(1,2,2,Facing.Up));
            l.obstacles.Add(new ObstacleData{cell=new Vector2Int(4,2),kind=ObstacleKind.Crab});
            var b=new BoardState(l);Assert.IsFalse(b.Move(0).hitsCrab);Assert.AreEqual(3,b.Lives);
        }
        [Test]
        public void HintDoesNotConsumeWhenNoFishCanEscape()
        {
            var b=new BoardState(Level(Fish(0,0,0,Facing.Right),Fish(1,1,0,Facing.Left)));
            Assert.AreEqual(-1,b.UseHint());Assert.IsFalse(b.HintUsed);Assert.IsTrue(b.IsDeadlocked);
        }
        [Test]
        public void SuccessfulHintIsOncePerStageAndDoesNotMoveFish()
        {
            var b=new BoardState(Level(Fish(0,0,0,Facing.Up)));
            Assert.AreEqual(0,b.UseHint());Assert.IsTrue(b.HintUsed);Assert.AreEqual(1,b.Fish.Count);Assert.AreEqual(-1,b.UseHint());
        }
        [Test]
        public void RescueClearsLastFishAndOnlyConsumesOnSuccessfulSelection()
        {
            var b=new BoardState(Level(Fish(0,0,0,Facing.Up)));
            Assert.IsFalse(b.Rescue(99));Assert.IsFalse(b.RescueUsed);
            Assert.IsTrue(b.Rescue(0));Assert.IsTrue(b.RescueUsed);Assert.IsTrue(b.IsClear);Assert.IsFalse(b.Rescue(0));
        }
        [Test]
        public void MovableFishPreventsDeadlockEvenWithoutAnEscape()
        {
            var b=new BoardState(Level(Fish(0,0,0,Facing.Right),Fish(1,3,0,Facing.Left)));
            Assert.AreEqual(-1,b.FindEscapingFish());Assert.IsFalse(b.IsDeadlocked);
            b.Move(0);Assert.IsTrue(b.IsDeadlocked);Assert.IsTrue(b.Rescue(1));Assert.IsTrue(b.Move(0).exits);
        }
    }
}
