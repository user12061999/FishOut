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
        public void RockNeverCostsLifeAndCannotBeRescued()
        {
            var l=Level(Fish(0,0,2,Facing.Right));l.obstacles.Add(new ObstacleData{cell=new Vector2Int(3,2),kind=ObstacleKind.Rock});
            var b=new BoardState(l);b.Move(0);Assert.AreEqual(new Vector2Int(2,2),b.Find(0).cell);
            b.Move(0);Assert.AreEqual(3,b.Lives);Assert.IsFalse(b.Rescue(999));Assert.IsFalse(b.RescueUsed);
        }
        [Test]
        public void CrabCostsExactlyOneLifeEveryTapIncludingAdjacentTaps()
        {
            var l=Level(Fish(0,0,2,Facing.Right));l.obstacles.Add(new ObstacleData{cell=new Vector2Int(3,2),kind=ObstacleKind.Crab});
            var b=new BoardState(l);
            Assert.IsTrue(b.Move(0).hitsCrab);Assert.AreEqual(2,b.Lives);Assert.AreEqual(new Vector2Int(2,2),b.Find(0).cell);
            b.Move(0);Assert.AreEqual(1,b.Lives);b.Move(0);Assert.IsTrue(b.IsFailed);
            Assert.Throws<System.InvalidOperationException>(()=>b.Move(0));
            Assert.AreEqual(3,new BoardState(l).Lives);Assert.AreEqual(new Vector2Int(0,2),new BoardState(l).Find(0).cell);
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
        [TestCase(1,3)] [TestCase(2,10)] [TestCase(3,20)] [TestCase(4,20)] [TestCase(5,20)]
        public void EveryAuthoredStageHasACompleteNoDamageSolution(int number,int expected)
        {
            var b=new BoardState(FishOutLevels.Get(number));Assert.AreEqual(expected,b.Fish.Count);
            var sequence=new List<int>();
            // Removing an immediately escapable fish never blocks another fish.
            for(int i=0;i<expected;i++)
            {
                int id=b.FindEscapingFish();
                if(id<0&&!b.RescueUsed&&b.Fish.Count==1)
                {
                    id=b.Fish[0].id;Assert.IsTrue(b.Rescue(id));sequence.Add(-id-1);continue;
                }
                Assert.GreaterOrEqual(id,0,"No solution within the stage's one free Rescue at step "+i);
                sequence.Add(id);Assert.IsTrue(b.Move(id).exits);
            }
            Assert.IsTrue(b.IsClear);Assert.AreEqual(3,b.Lives);
            TestContext.WriteLine("Level "+number+" solution: "+string.Join(",",sequence));
        }
    }
}
