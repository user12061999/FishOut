using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FishOut.Tests
{
    public class ShowcaseLevelTests
    {
        // Clears a stage the way a careful player would: only taps that send a piece (fish or crab) out, no booster.
        internal static void ClearStage(BoardState board,System.Action<int> onEscape=null)
        {
            for(int guard=0;guard<1000&&!board.IsClear;guard++)
            {
                int id=board.FindEscapingFish();
                if(id>=0){onEscape?.Invoke(id);Assert.IsTrue(board.Move(id).exits);continue;}
                Assert.Fail("No piece can leave without a booster at level "+board.Level.number);
            }
            Assert.IsTrue(board.IsClear,"Stage did not finish");
        }
        // GDD §8: fish, rocks, crabs per stage.
        [TestCase(1,3,0,0)] [TestCase(2,10,0,0)] [TestCase(3,20,3,0)] [TestCase(4,20,0,1)] [TestCase(5,20,2,2)]
        public void SpecStageIsSolvableWithoutBumpingACrab(int number,int fish,int rocks,int crabs)
        {
            var level=FishOutLevels.Get(number);var board=new BoardState(level);Assert.IsFalse(board.NoWayOut,"Every stage starts solvable");
            Assert.AreEqual(fish,board.Fish.Count(f=>!f.crab));Assert.IsTrue(level.showcase);
            Assert.AreEqual(rocks,level.obstacles.FindAll(o=>o.kind==ObstacleKind.Rock).Count);
            Assert.AreEqual(crabs,level.fish.FindAll(f=>f.crab).Count);Assert.IsFalse(level.obstacles.Exists(o=>o.kind==ObstacleKind.Crab));
            Assert.AreEqual(8,level.width);Assert.AreEqual(10,level.height);
            var checkedEntries=new HashSet<Vector2Int>();
            ClearStage(board,id=>
            {
                var path=board.CalculatePath(id);Assert.IsTrue(level.IsChannel(path.Result.to));
                if(!checkedEntries.Add(path.Result.to))return;
                var route=ConveyorNetwork.RouteToGate(level,path.Result.to);
                Assert.Less(Vector2.Distance(level.gate,route[route.Count-1]),.75f,"The road must end in the cave");
                for(int i=1;i<route.Count;i++)Assert.Less(Vector2.Distance(route[i],route[i-1]),.25f,"Conveyor curve has a discontinuity");
            });
            Assert.IsFalse(board.IsFailed);Assert.IsFalse(board.RescueUsed);
        }
        [Test]
        public void TutorialStagesTeachTheirLesson()
        {
            var first=new BoardState(FishOutLevels.Get(1));
            foreach(var f in first.Fish)Assert.IsTrue(first.Preview(f.id).exits,"Level 1: every fish has a clear path");
            var second=new BoardState(FishOutLevels.Get(2));
            Assert.IsTrue(second.Fish.Any(f=>{var m=second.Preview(f.id);return m.ChangesBoard&&!m.exits;}),"Level 2: a fish stops part-way");
            foreach(var n in new[]{4,5})
            {
                var b=new BoardState(FishOutLevels.Get(n));
                Assert.IsTrue(b.Fish.Any(f=>!f.crab&&b.Preview(f.id).hitsCrab),"Level "+n+": a fish can bump a crab");
                Assert.IsFalse(b.Fish.Any(f=>f.crab&&b.Preview(f.id).exits),"Level "+n+": no crab can leave on the first tap");
            }
        }
        private static LevelData Lined(params FishData[] fish)
        {
            var level=new LevelData{width=3,height=5};level.fish.AddRange(fish);return level;
        }
        private static FishData Fish(int id,int x,int y,Facing facing)=>new FishData{id=id,cell=new Vector2Int(x,y),facing=facing};
        [Test]
        public void TouchingFishWithTheSameFacingEscapeFrontFirst()
        {
            var board=new BoardState(Lined(Fish(1,1,4,Facing.Up),Fish(2,1,3,Facing.Up),Fish(3,1,2,Facing.Up),Fish(4,1,1,Facing.Left),Fish(5,1,0,Facing.Up)));
            CollectionAssert.AreEqual(new[]{1,2,3},board.EscapingLine(3),"A different facing ends the line");
            CollectionAssert.AreEqual(new[]{1,2,3},board.EscapingLine(2),"Tapping mid-line moves the same line");
            foreach(var id in board.EscapingLine(1))Assert.IsTrue(board.Move(id).exits);
            Assert.AreEqual(2,board.Fish.Count);
        }
        [Test]
        public void LineWaitsWhenItsFrontFishCannotEscapeOrThereIsAGap()
        {
            var blocked=new BoardState(Lined(Fish(1,1,4,Facing.Left),Fish(9,0,4,Facing.Right),Fish(2,1,3,Facing.Up),Fish(3,1,2,Facing.Up)));
            CollectionAssert.IsEmpty(blocked.EscapingLine(3));
            var gap=new BoardState(Lined(Fish(1,1,4,Facing.Up),Fish(2,1,2,Facing.Up)));
            CollectionAssert.IsEmpty(gap.EscapingLine(2),"A gap breaks the line, so the rear fish stays behind");
            CollectionAssert.AreEqual(new[]{1},gap.EscapingLine(1));
        }
        private static int Waves(int number)
        {
            var board=new BoardState(FishOutLevels.Get(number));int waves=0;
            for(int guard=0;guard<500;guard++)
            {
                var free=new List<int>();
                foreach(var fish in board.Fish)if(board.Preview(fish.id).exits)free.Add(fish.id);
                if(free.Count==0)return waves;
                waves++;
                foreach(var id in free)if(board.Find(id)!=null&&board.Preview(id).exits)board.Move(id);
            }
            return waves;
        }
        [Test]
        public void DifficultyRisesFromStageToStage()
        {
            Assert.Greater(Waves(3),Waves(1),"Later stages must ask for more waves");
            Assert.GreaterOrEqual(Waves(5),Waves(3),"Later stages must ask for more waves");
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryStageUsesAtMostTwoCharacterModels(int number)
        {
            var models=new HashSet<int>();
            foreach(var fish in FishOutLevels.Get(number).fish)if(!fish.crab)models.Add(fish.appearance);
            Assert.LessOrEqual(models.Count,2);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void RingEntryNextToTheGateTakesTheShortWay(int number)
        {
            var level=FishOutLevels.Get(number);
            var entry=new Vector2(level.gate.x-1,level.height-.4f);
            float length=0;var route=ConveyorNetwork.RouteToGate(level,entry);
            for(int i=1;i<route.Count;i++)length+=Vector2.Distance(route[i-1],route[i]);
            // The neck into the cave (now running on behind the painted arch) is shared by every route; only the ring part varies.
            var neck=level.channelLines[1];float neckLength=0;
            for(int i=1;i<neck.Length;i++)neckLength+=Vector2.Distance(neck[i-1],neck[i]);
            Assert.Less(length-neckLength,4f,"A fish beside the gate must not circle the whole ring");
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EntireFishCellFitsInsideCurvedWater(int number)
        {
            var level=FishOutLevels.Get(number);var positions=new HashSet<Vector2Int>();
            foreach(var fish in level.fish)
            {
                Assert.IsTrue(positions.Add(fish.cell));Assert.IsTrue(ReefWaterDomain.ContainsWholeCell(level,fish.cell));
                for(int y=-4;y<=4;y++)for(int x=-4;x<=4;x++)
                {
                    var point=(Vector2)fish.cell+new Vector2(x/4f,y/4f)*(ReefWaterDomain.CellSize*.5f);
                    Assert.Greater(ReefWaterDomain.SignedDistance(level,point),0,"A tile overhangs a curved water bank");
                }
            }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void RenderedWaterMeshSupportsEveryCellCornerAndEdge(int number)
        {
            var level=FishOutLevels.Get(number);var mesh=CurvedWaterMesh.Build(level,0,-.015f);
            var go=new GameObject("Water containment verification",typeof(MeshCollider));
            try
            {
                var collider=go.GetComponent<MeshCollider>();collider.sharedMesh=mesh;
                foreach(var fish in level.fish)
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    var p=(Vector2)fish.cell+new Vector2(x,y)*(ReefWaterDomain.CellSize*.5f);
                    var point=ConveyorNetwork.World(level,p,1);
                    Assert.IsTrue(collider.Raycast(new Ray(point,Vector3.down),out _,2),"Rendered water does not contain a whole fish cell: "+fish.cell);
                }
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);}
        }
    }
}
