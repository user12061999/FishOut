using UnityEngine;
namespace FishOut
{
    public static class ReefWaterDomain
    {
        public const float CellSize=.96f;
        public const float ChannelHalfWidth=.64f;
        public const float WaterRoadClearance=.5f;
        public static float SignedDistance(LevelData level,Vector2 point)
        {
            var center=new Vector2((level.width-1)*.5f,(level.height-1)*.5f);
            var q=new Vector2(Mathf.Abs(point.x-center.x),Mathf.Abs(point.y-center.y))-new Vector2(center.x-.2f-1.2f,center.y+.1f-1.2f);
            float outside=Vector2.Max(q,Vector2.zero).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-1.2f;
            float distance=-outside;
            for(int line=2;line<level.channelLines.Count;line++)
            {
                var path=level.channelLines[line];float nearest=distance+WaterRoadClearance;
                for(int i=1;i<path.Length;)
                {
                    float d=ShowcaseLevels.Distance(point,path[i-1],path[i]);
                    if(d<nearest)nearest=d;
                    // Road segments are at most .12 long, so the next few cannot come any closer.
                    i+=Mathf.Max(1,(int)((d-nearest)/.12f));
                }
                distance=Mathf.Min(distance,nearest-WaterRoadClearance);
            }
            return distance;
        }
        // Positive on the sand: the rounded shore around the ring road, flared into the road up to the gate.
        public static float ShoreDistance(LevelData level,Vector2 point)
        {
            var center=new Vector2((level.width-1)*.5f,(level.height-1)*.5f);
            const float radius=3f,blend=1.5f;
            var q=new Vector2(Mathf.Abs(point.x-center.x),Mathf.Abs(point.y-center.y))-new Vector2(center.x+1.25f-radius,center.y+1.5f-radius);
            float shore=Vector2.Max(q,Vector2.zero).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
            float neck=ShowcaseLevels.Distance(point,new Vector2(level.gate.x,level.height-1),level.gate+Vector2.up*.6f)-1.2f;
            float h=Mathf.Clamp01(.5f+.5f*(neck-shore)/blend);
            return -(Mathf.Lerp(neck,shore,h)-blend*h*(1-h));
        }
        public static bool ContainsWholeCell(LevelData level,Vector2Int cell)
        {
            // A containing disk and the distance field prove that the entire square is inside.
            return SignedDistance(level,cell)>=CellSize*.5f*Mathf.Sqrt(2)+.025f;
        }
    }
}
