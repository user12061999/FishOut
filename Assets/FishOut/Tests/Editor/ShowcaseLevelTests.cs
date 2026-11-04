using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FishOut.Tests
{
    public class ShowcaseLevelTests
    {
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void IllustratedSchoolHasFullSolutionAndEveryExitConnectsToGate(int number)
        {
            var level=FishOutLevels.Get(number);var board=new BoardState(level);
            Assert.Greater(board.Fish.Count,20);Assert.IsTrue(level.showcase);
            var checkedEntries=new HashSet<Vector2Int>();
            while(!board.IsClear)
            {
                int id=board.FindEscapingFish();Assert.GreaterOrEqual(id,0,"School is deadlocked");
                var path=board.CalculatePath(id);Assert.IsTrue(level.IsChannel(path.Result.to));
                if(checkedEntries.Add(path.Result.to))
                {
                    var route=ConveyorNetwork.RouteToGate(level,path.Result.to);
                    Assert.Less(Vector2.Distance(level.gate,route[route.Count-1]),.001f);
                    for(int i=1;i<route.Count;i++)Assert.Less(Vector2.Distance(route[i],route[i-1]),.25f,"Conveyor curve has a discontinuity");
                }
                board.Move(id);
            }
            Assert.AreEqual(3,board.Lives);Assert.IsFalse(board.RescueUsed);
        }
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
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
        [Test]
        public void TwoToneSchoolContainsExactlyRedAndBlue()
        {
            var l=FishOutLevels.Get(9);var palette=new HashSet<int>();
            foreach(var fish in l.fish)palette.Add(fish.appearance);
            CollectionAssert.AreEquivalent(new[]{0,3},palette);
        }
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
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
