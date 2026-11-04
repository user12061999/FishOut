using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishOut
{
    // Samples by distance along the complete polyline, without restarting a tween at each corner.
    public sealed class MovementPath
    {
        private readonly Vector3[] points;
        private readonly float[] distances;
        public IReadOnlyList<Vector3> Waypoints { get; }
        public float Length => distances[distances.Length - 1];
        public Vector3 End => points[points.Length - 1];

        public MovementPath(IEnumerable<Vector3> waypoints)
        {
            if (waypoints == null) throw new ArgumentNullException(nameof(waypoints));
            var clean = new List<Vector3>();
            foreach (var point in waypoints)
            {
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                    throw new ArgumentException("Waypoints must be finite");
                if (clean.Count == 0 || (point - clean[clean.Count - 1]).sqrMagnitude > .000001f)
                    clean.Add(point);
            }
            if (clean.Count == 0) throw new ArgumentException("A path needs a start point");
            points = clean.ToArray(); Waypoints = Array.AsReadOnly(points);
            distances = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        public Vector3 Sample(float distance)
        {
            if (distance <= 0 || points.Length == 1) return points[0];
            if (distance >= Length) return End;
            int segment = Array.BinarySearch(distances, distance);
            if (segment >= 0) return points[segment];
            segment = ~segment;
            float t = (distance - distances[segment - 1]) / (distances[segment] - distances[segment - 1]);
            return Vector3.LerpUnclamped(points[segment - 1], points[segment], t);
        }

        public static MovementPath FromBoard(BoardPath boardPath, Func<Vector2Int, Vector3> toWorld,
            Camera camera, Bounds actorBounds)
        {
            var route = new List<Vector3>();
            foreach (var cell in boardPath.Cells) route.Add(toWorld(cell));
            if (boardPath.Result.exits)
            {
                if (boardPath.Level.showcase)
                {
                    var channel=ConveyorNetwork.RouteToGate(boardPath.Level,boardPath.Result.to);
                    foreach(var point in channel)route.Add(ConveyorNetwork.World(boardPath.Level,point));
                    route.Add(toWorld(boardPath.Level.gate+Vector2Int.up*4));
                    route=RoundCorners(route);
                    var last=route[route.Count-1];
                    float tail=OffscreenDistance(camera,route[0],Vector3.forward,actorBounds);
                    route.Add(new Vector3(last.x,last.y,Mathf.Max(last.z,route[0].z+tail)));
                    return new MovementPath(route);
                }
                var start = route[0];
                var direction = (route[route.Count - 1] - start).normalized;
                float distance = OffscreenDistance(camera, start, direction, actorBounds);
                // Always cross the board boundary even if it already lies outside the camera view.
                distance = Mathf.Max(distance, Vector3.Distance(start, route[route.Count - 1]));
                route.Add(start + direction * distance);
            }
            return new MovementPath(route);
        }
        private static List<Vector3> RoundCorners(List<Vector3> input)
        {
            var simple=new List<Vector3>();
            foreach(var p in input)
            {
                if(simple.Count>0&&(p-simple[simple.Count-1]).sqrMagnitude<.00001f)continue;
                while(simple.Count>1&&Vector3.Dot((simple[simple.Count-1]-simple[simple.Count-2]).normalized,(p-simple[simple.Count-1]).normalized)>.999f)simple.RemoveAt(simple.Count-1);
                simple.Add(p);
            }
            var output=new List<Vector3>{simple[0]};
            for(int i=1;i<simple.Count-1;i++)
            {
                float rounding=Mathf.Min(.30f,Vector3.Distance(simple[i],simple[i-1])*.35f,Vector3.Distance(simple[i],simple[i+1])*.35f);
                var before=Vector3.MoveTowards(simple[i],simple[i-1],rounding);
                var after=Vector3.MoveTowards(simple[i],simple[i+1],rounding);
                output.Add(before);
                for(int j=1;j<=5;j++){float t=j/5f;output.Add((1-t)*(1-t)*before+2*(1-t)*t*simple[i]+t*t*after);}
            }
            output.Add(simple[simple.Count-1]);return output;
        }

        private static float OffscreenDistance(Camera camera, Vector3 start, Vector3 direction, Bounds bounds)
        {
            if (!camera || !camera.orthographic)
                throw new ArgumentException("Fish Out exit planning requires its fixed orthographic camera");
            Vector3 origin = camera.WorldToViewportPoint(start);
            Vector3 delta = camera.WorldToViewportPoint(start + direction) - origin;
            var radius = Vector2.zero;
            // Account for the entire animated model, not just its pivot, before despawning.
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var projected = camera.WorldToViewportPoint(corner);
                radius.x = Mathf.Max(radius.x, Mathf.Abs(projected.x - origin.x));
                radius.y = Mathf.Max(radius.y, Mathf.Abs(projected.y - origin.y));
            }
            const float margin = .04f;
            float exit = float.PositiveInfinity;
            if (Mathf.Abs(delta.x) > .000001f)
                exit = Mathf.Min(exit, ((delta.x > 0 ? 1 + radius.x + margin : -radius.x - margin) - origin.x) / delta.x);
            if (Mathf.Abs(delta.y) > .000001f)
                exit = Mathf.Min(exit, ((delta.y > 0 ? 1 + radius.y + margin : -radius.y - margin) - origin.y) / delta.y);
            if (float.IsInfinity(exit)) throw new InvalidOperationException("Movement direction is parallel to the camera view");
            return Mathf.Max(0, exit);
        }
    }
}
