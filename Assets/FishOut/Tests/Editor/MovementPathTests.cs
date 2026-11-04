using NUnit.Framework;
using UnityEngine;

namespace FishOut.Tests
{
    public class MovementPathTests
    {
        [Test]
        public void ForwardLineCheckListsOnlyReachableCellsAndDoesNotChangeBoard()
        {
            var board=new BoardState(FishOutLevels.Get(2));
            var path=board.CalculatePath(0);
            CollectionAssert.AreEqual(new[]{new Vector2Int(0,2),new Vector2Int(1,2),new Vector2Int(2,2),new Vector2Int(3,2)},path.Cells);
            Assert.IsFalse(path.Result.exits);Assert.IsFalse(path.Result.hitsCrab);
            Assert.AreEqual(new Vector2Int(0,2),board.Find(0).cell);
            Assert.AreEqual(path.Result.to,board.Move(0).to);
        }
        [Test]
        public void AdjacentCrabProducesStationaryPathAndDamageResult()
        {
            var board=new BoardState(FishOutLevels.Get(4));var path=board.CalculatePath(0);
            Assert.AreEqual(1,path.Cells.Count);Assert.IsTrue(path.Result.hitsCrab);Assert.IsFalse(path.Result.ChangesBoard);
            Assert.AreEqual(3,board.Lives);
        }
        [Test]
        public void PolylineUsesArcLengthThroughUnequalSegmentsAndSkipsDuplicatePoints()
        {
            var path=new MovementPath(new[]{Vector3.zero,Vector3.zero,new Vector3(2,0,0),new Vector3(2,0,3)});
            Assert.AreEqual(3,path.Waypoints.Count);Assert.AreEqual(5,path.Length,.0001f);
            Assert.AreEqual(Vector3.zero,path.Sample(-1));Assert.AreEqual(new Vector3(1,0,0),path.Sample(1));
            Assert.AreEqual(new Vector3(2,0,0),path.Sample(2));Assert.AreEqual(new Vector3(2,0,1),path.Sample(3));
            Assert.AreEqual(new Vector3(2,0,3),path.Sample(9));
        }
        [Test]
        public void StationaryPathHasNoTravelOrDivisionByZero()
        {
            var path=new MovementPath(new[]{Vector3.one,Vector3.one});
            Assert.AreEqual(0,path.Length);Assert.AreEqual(Vector3.one,path.Sample(3));
        }
        [TestCase(Facing.Up,4.3f,.9f)] [TestCase(Facing.Down,4.3f,.9f)]
        [TestCase(Facing.Left,4.3f,.9f)] [TestCase(Facing.Right,4.3f,.9f)]
        [TestCase(Facing.Up,9f,.5f)] [TestCase(Facing.Right,9f,2f)]
        public void EscapeRouteEndsWithEntireModelOutsideCamera(Facing direction,float size,float aspect)
        {
            var go=new GameObject("Path test camera");
            try
            {
                var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;camera.aspect=aspect;
                camera.transform.position=new Vector3(0,12,-9);camera.transform.LookAt(Vector3.zero);
                var level=new LevelData();level.fish.Add(new FishData{id=0,cell=new Vector2Int(2,3),facing=direction});
                var board=new BoardState(level);var plan=board.CalculatePath(0);
                Vector3 Cell(Vector2Int p)=>new Vector3(p.x-2.5f,.1f,p.y-3.5f);
                var start=Cell(plan.Result.from);var bounds=new Bounds(start+Vector3.up*.4f,new Vector3(1,.9f,1));
                var route=MovementPath.FromBoard(plan,Cell,camera,bounds);
                Assert.IsTrue(plan.Result.exits);Assert.IsFalse(board.Inside(plan.Cells[plan.Cells.Count-1]));
                bounds.center+=route.End-start;
                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
                var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var v=camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                    min=Vector2.Min(min,v);max=Vector2.Max(max,v);
                }
                Assert.IsTrue(max.x<0||min.x>1||max.y<0||min.y>1,"A visible part of the model would despawn");
                Assert.AreEqual(start,route.Waypoints[0]);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
