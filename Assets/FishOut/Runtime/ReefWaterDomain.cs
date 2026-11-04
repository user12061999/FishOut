using UnityEngine;
namespace FishOut
{
    public static class ReefWaterDomain
    {
        public const float CellSize=.86f;
        public const float ChannelHalfWidth=.82f;
        public const float WaterRoadClearance=1.02f;
        public static float SignedDistance(LevelData level,Vector2 point)
        {
            var q=new Vector2(Mathf.Abs(point.x-7),Mathf.Abs(point.y-10))-new Vector2(6.1f-1.45f,9.35f-1.45f);
            float outside=Vector2.Max(q,Vector2.zero).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-1.45f;
            float distance=-outside;
            for(int line=2;line<level.channelLines.Count;line++)
            {
                var path=level.channelLines[line];float nearest=float.PositiveInfinity;
                for(int i=1;i<path.Length;i++)nearest=Mathf.Min(nearest,ShowcaseLevels.Distance(point,path[i-1],path[i]));
                distance=Mathf.Min(distance,nearest-WaterRoadClearance);
            }
            return distance;
        }
        public static bool ContainsWholeCell(LevelData level,Vector2Int cell)
        {
            // A containing disk and the distance field prove that the entire square is inside.
            return SignedDistance(level,cell)>=CellSize*.5f*Mathf.Sqrt(2)+.025f;
        }
    }
}
