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
            for(int i=projection.segment;i<curve.Length;i++)output.Add(curve[i]);
            if(projection.line!=1)
                foreach(var p in level.channelLines[1])if((p-output[output.Count-1]).sqrMagnitude>.00001f)output.Add(p);
            return output;
        }
        public static Vector3 World(LevelData level,Vector2 p,float y=.1f)=>new Vector3(p.x-(level.width-1)*.5f,y,p.y-(level.height-1)*.5f);
    }
}
