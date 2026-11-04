using System;
using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    public static class ShowcaseLevels
    {
        public static LevelData Get(int number)
        {
            var level=new LevelData{number=number,width=15,height=21,showcase=true,gate=new Vector2Int(7,23)};
            level.title=number==6?"RAINBOW REEF":number==7?"SERPENT CURRENT":number==8?"SPIRAL LAGOON":"TWO TONE TIDE";
            level.channelLines.Add(ScrewConveyorGeometry.Rounded(new[]{new Vector2(7,20.3f),new Vector2(0,20.3f),new Vector2(0,-.3f),new Vector2(14,-.3f),new Vector2(14,20.3f)},true,2.2f));
            level.channelLines.Add(ScrewConveyorGeometry.Resample(new[]{new Vector2(7,20.3f),new Vector2(7,23)},.12f));
            if(number==7)level.channelLines.Add(Smooth(new[]{new Vector2(7,-.3f),new Vector2(10,2),new Vector2(10.5f,4.5f),new Vector2(8,7),new Vector2(4,10),new Vector2(3.5f,12.7f),new Vector2(5,15.3f),new Vector2(9,18),new Vector2(7,20.3f)},16));
            if(number==8)
            {
                var spiral=new List<Vector2>();
                for(int i=0;i<=220;i++)
                {
                    float t=i/220f,angle=-Mathf.PI*2+t*Mathf.PI*2.5f,radius=2.4f+t*5.2f;
                    spiral.Add(new Vector2(7+Mathf.Cos(angle)*radius*.78f,10+Mathf.Sin(angle)*radius*1.08f));
                }
                var end=spiral[spiral.Count-1];var top=new Vector2(7,20.3f);
                for(int i=1;i<=32;i++)
                {
                    float t=i/32f,u=1-t;
                    spiral.Add(u*u*u*end+3*u*u*t*(end+new Vector2(-1f,.15f))+3*u*t*t*(top+Vector2.down*.9f)+t*t*t*top);
                }
                level.channelLines.Add(ScrewConveyorGeometry.Resample(spiral,.12f));
            }
            var random=new System.Random(7100+number);
            for(int y=0;y<level.height;y++)for(int x=0;x<level.width;x++)
            {
                var cell=new Vector2Int(x,y);
                if(!ReefWaterDomain.ContainsWholeCell(level,cell)){level.channelCells.Add(cell);continue;}
                int color=number==9?(y>=10?3:0):number==6?(y>=15?2:y>=10?0:y>=5?3:4):random.Next(5);
                level.fish.Add(new FishData{id=level.fish.Count,cell=cell,facing=Facing.Up,appearance=color});
            }
            level.channelCells.Add(level.gate);MixDirections(level,random);return level;
        }
        private static void MixDirections(LevelData level,System.Random random)
        {
            var remaining=new HashSet<Vector2Int>();foreach(var fish in level.fish)remaining.Add(fish.cell);
            while(remaining.Count>0)
            {
                var options=new List<KeyValuePair<FishData,Facing>>();
                foreach(var fish in level.fish)
                {
                    if(!remaining.Contains(fish.cell))continue;
                    for(int d=0;d<4;d++)
                    {
                        var step=BoardState.Direction((Facing)d);var p=fish.cell+step;
                        while(!level.IsChannel(p)&&!remaining.Contains(p)&&p.x>=0&&p.y>=0&&p.x<level.width&&p.y<level.height)p+=step;
                        if(level.IsChannel(p))options.Add(new KeyValuePair<FishData,Facing>(fish,(Facing)d));
                    }
                }
                if(options.Count==0)throw new InvalidOperationException("Cannot construct a solvable school");
                var option=options[random.Next(options.Count)];option.Key.facing=option.Value;remaining.Remove(option.Key.cell);
            }
        }
        public static float Distance(Vector2 p,Vector2 a,Vector2 b)
        {
            var delta=b-a;float t=delta.sqrMagnitude<.00001f?0:Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
            return Vector2.Distance(p,a+delta*t);
        }
        private static Vector2[] Smooth(Vector2[] points,int steps)
        {
            var result=new List<Vector2>();
            for(int i=0;i<points.Length-1;i++)
            {
                var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
                for(int j=0;j<steps;j++){float t=j/(float)steps;result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
            }
            result.Add(points[points.Length-1]);return ScrewConveyorGeometry.Resample(result,.12f);
        }
    }
}
