using System;
using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    public static class ConveyorNetwork
    {
        public readonly struct Projection
        {
            public readonly int line,segment;
            public readonly Vector2 point;
            public readonly float distance;
            public Projection(int l,int s,Vector2 p,float d){line=l;segment=s;point=p;distance=d;}
        }
        public static Projection Project(LevelData level,Vector2 point)
        {
            int line=-1,segment=0;float distance=float.PositiveInfinity;var nearest=point;
            for(int l=0;l<level.channelLines.Count;l++)
            {
                var path=level.channelLines[l];
                for(int s=1;s<path.Length;s++)
                {
                    var delta=path[s]-path[s-1];float t=delta.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(point-path[s-1],delta)/delta.sqrMagnitude);
                    var p=path[s-1]+delta*t;float d=(point-p).sqrMagnitude;
                    if(d<distance){line=l;segment=s;distance=d;nearest=p;}
                }
            }
            if(line<0)throw new InvalidOperationException("Missing conveyor curves");
            return new Projection(line,segment,nearest,Mathf.Sqrt(distance));
        }
        public static List<Vector2> RouteToGate(LevelData level,Vector2 entry)
        {
            var projection=Project(level,entry);var output=new List<Vector2>{projection.point};
            var curve=level.channelLines[projection.line];
            if(projection.line==0)output.AddRange(ShortestFromRing(level,projection));
            else for(int i=projection.segment;i<curve.Length;i++)output.Add(curve[i]);
            if(projection.line!=1)
                foreach(var p in level.channelLines[1])if((p-output[output.Count-1]).sqrMagnitude>.00001f)output.Add(p);
            return output;
        }
        // The ring runs both ways and every inner road branches off it, so take whichever way is shortest.
        private static List<Vector2> ShortestFromRing(LevelData level,Projection from)
        {
            var ring=level.channelLines[0];
            var best=Walk(ring,from.segment,ring.Length-1);
            float bestLength=Length(from.point,best);
            Keep(Walk(ring,from.segment,0),from.point,ref best,ref bestLength);
            for(int line=2;line<level.channelLines.Count;line++)
            {
                var road=level.channelLines[line];int nearest=0;
                for(int i=1;i<ring.Length;i++)if((ring[i]-road[0]).sqrMagnitude<(ring[nearest]-road[0]).sqrMagnitude)nearest=i;
                if(Vector2.Distance(ring[nearest],road[0])>.2f)continue;
                var path=Walk(ring,from.segment,nearest);path.AddRange(road);
                Keep(path,from.point,ref best,ref bestLength);
            }
            return best;
        }
        private static void Keep(List<Vector2> path,Vector2 start,ref List<Vector2> best,ref float bestLength)
        {
            float length=Length(start,path);
            if(length<bestLength){best=path;bestLength=length;}
        }
        private static List<Vector2> Walk(Vector2[] ring,int segment,int target)
        {
            var path=new List<Vector2>();
            if(target>=segment)for(int i=segment;i<=target;i++)path.Add(ring[i]);
            else for(int i=segment-1;i>=target;i--)path.Add(ring[i]);
            return path;
        }
        private static float Length(Vector2 start,List<Vector2> path)
        {
            float length=0;var previous=start;
            foreach(var point in path){length+=Vector2.Distance(previous,point);previous=point;}
            return length;
        }
        // On the painted board the grid follows the painting's perspective (see PaintedBackdrop).
        public static Vector3 World(LevelData level,Vector2 p,float y=.1f)=>PaintedBackdrop.Warps(level)?PaintedBackdrop.GridToWorld(level,p,y):new Vector3(p.x-(level.width-1)*.5f,y,p.y-(level.height-1)*.5f);
    }
}
