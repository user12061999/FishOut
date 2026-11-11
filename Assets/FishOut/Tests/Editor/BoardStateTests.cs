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
            Assert.AreEqual(move.to,b.Find(0).cell);Assert.IsFalse(b.IsFailed);
            Assert.IsFalse(b.Move(0).ChangesBoard);b.Move(1);Assert.IsTrue(b.Move(0).exits);
        }
        [Test]
        public void RockNeverMovesNeverBreaksAndNeverCostsLife()
        {
            var l=Level(Fish(0,0,2,Facing.Right));l.obstacles.Add(new ObstacleData{cell=new Vector2Int(3,2),kind=ObstacleKind.Rock});
            var b=new BoardState(l);Assert.IsTrue(b.Move(0).hitsRock);Assert.AreEqual(new Vector2Int(2,2),b.Find(0).cell);
            var again=b.Move(0);Assert.IsTrue(again.hitsRock);Assert.IsFalse(again.ChangesBoard,"Spec: a rock is fixed and cannot be removed");
            Assert.IsTrue(b.IsDeadlocked);Assert.IsFalse(b.IsFailed,"Rescue can still free it");Assert.IsFalse(b.Rescue(999));Assert.IsFalse(b.RescueUsed);
        }
        private static FishData Crab(int id,int x,int y,Facing direction)=>new FishData{id=id,cell=new Vector2Int(x,y),facing=direction,crab=true};
        [Test]
        public void FishBumpingACrabCountsItDownAndTheCrabSwimsOutLikeAFish()
        {
            var l=Level(Fish(0,0,2,Facing.Right),Crab(1,3,2,Facing.Up));
            var b=new BoardState(l);Assert.AreEqual(3,b.CrabLeft(1));Assert.IsTrue(b.IsCrab(1));
            var first=b.Move(0);Assert.IsTrue(first.hitsCrab);Assert.AreEqual(1,first.struckId);Assert.AreEqual(2,b.CrabLeft(1));Assert.AreEqual(new Vector2Int(2,2),b.Find(0).cell);
            var second=b.Move(0);Assert.IsTrue(second.hitsCrab);Assert.IsFalse(second.ChangesBoard);Assert.AreEqual(1,b.CrabLeft(1));Assert.IsFalse(b.IsFailed);
            Assert.IsTrue(b.Move(1).exits,"The crab leaves the way it faces");Assert.IsFalse(b.IsClear,"The fish is still on the board");
            Assert.IsTrue(b.Move(0).exits);Assert.IsTrue(b.IsClear);Assert.IsFalse(b.RescueUsed);
            Assert.AreEqual(3,new BoardState(l).CrabLeft(1));
        }
        [Test]
        public void CrabRunningIntoAnythingCountsItselfDownAndThirdBumpFails()
        {
            var l=Level(Crab(0,0,2,Facing.Right),Fish(1,4,5,Facing.Up));l.obstacles.Add(new ObstacleData{cell=new Vector2Int(3,2),kind=ObstacleKind.Rock});
            var b=new BoardState(l);
            var slide=b.Move(0);Assert.IsTrue(slide.crabStops);Assert.IsTrue(slide.ChangesBoard);Assert.AreEqual(2,b.CrabLeft(0));
            var stay=b.Move(0);Assert.IsTrue(stay.crabStops);Assert.IsFalse(stay.ChangesBoard);Assert.AreEqual(1,b.CrabLeft(0));Assert.IsFalse(b.IsFailed);
            b.Move(0);Assert.AreEqual(0,b.CrabLeft(0));Assert.IsTrue(b.CrabExhausted);Assert.IsTrue(b.IsFailed);
            Assert.Throws<System.InvalidOperationException>(()=>b.Move(1));
        }
        [Test]
        public void CrabIntoCrabCountsBothDown()
        {
            var b=new BoardState(Level(Crab(0,0,2,Facing.Right),Crab(1,2,2,Facing.Up)));
            var r=b.Move(0);Assert.IsTrue(r.hitsCrab);Assert.IsTrue(r.crabStops);Assert.AreEqual(2,b.CrabLeft(0));Assert.AreEqual(2,b.CrabLeft(1));
        }
        [Test]
        public void StageFailsWhenNoWayToTheCaveIsLeft()
        {
            // Three fish each stuck behind a rock: one Rescue cannot free them all.
            var l=Level(Fish(0,0,1,Facing.Right),Fish(1,0,3,Facing.Right),Fish(2,0,5,Facing.Right));
            foreach(var y in new[]{1,3,5})l.obstacles.Add(new ObstacleData{cell=new Vector2Int(1,y),kind=ObstacleKind.Rock});
            var b=new BoardState(l);Assert.IsTrue(b.NoWayOut);Assert.IsTrue(b.IsFailed);Assert.IsFalse(b.CrabExhausted);
        }
        [Test]
        public void CrabAndFishFacingEachOtherNeedTheRescue()
        {
            // Crab and fish face each other in one column: neither can ever pass, so only Rescue splits them.
            var l=Level(Crab(0,2,0,Facing.Up),Fish(1,2,7,Facing.Down),Fish(2,5,0,Facing.Up));
            var b=new BoardState(l);Assert.IsFalse(b.NoWayOut);
            Assert.IsTrue(b.Rescue(2));Assert.IsTrue(b.NoWayOut,"Rescue spent on the free fish");
        }
        [Test]
        public void UsingRescueOnTheWrongFishCanLeaveNoWayOut()
        {
            // Fish 0 and 1 face each other in one column: only Rescue can split them. Fish 2 is free.
            var l=Level(Fish(0,2,0,Facing.Up),Fish(1,2,7,Facing.Down),Fish(2,5,0,Facing.Up));
            var b=new BoardState(l);Assert.IsFalse(b.NoWayOut);
            Assert.IsTrue(b.Rescue(2));Assert.IsTrue(b.NoWayOut);Assert.IsTrue(b.IsFailed);
            var again=new BoardState(l);Assert.IsTrue(again.Rescue(1));Assert.IsFalse(again.NoWayOut);
            Assert.IsTrue(again.Move(0).exits);Assert.IsTrue(again.Move(2).exits);Assert.IsTrue(again.IsClear);
        }
        [Test]
        public void OnlyTheFirstBlockerCanCauseDamage()
        {
            var b=new BoardState(Level(Fish(0,0,2,Facing.Right),Fish(1,2,2,Facing.Up),Crab(2,4,2,Facing.Up)));
            Assert.IsFalse(b.Move(0).hitsCrab);Assert.AreEqual(3,b.CrabLeft(2));
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
