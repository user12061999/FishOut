using System;
using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    public static class ShowcaseLevels
    {
        private const int SpreadSamples=24;
        private sealed class Stage
        {
            public int width,height,depth,crabs,rocks,first,second;
            public Stage(int w,int h,int waves,int crabCount,int rockCount,int modelA,int modelB)
            {width=w;height=h;depth=waves;crabs=crabCount;rocks=rockCount;first=modelA;second=modelB;}
        }
        // Size, waves of fish to clear in order, crabs, rocks, and the stage's two character models.
        private static readonly Stage[] Stages=
        {
            new Stage(5,7,2,0,0,0,2),new Stage(6,8,3,0,0,2,5),new Stage(9,11,4,0,2,0,8),new Stage(10,15,5,1,0,5,0),new Stage(12,18,7,2,3,8,5),
            new Stage(11,15,7,2,1,8,0),new Stage(11,17,8,2,2,5,8),new Stage(12,17,8,3,2,0,5),new Stage(12,16,9,2,3,8,5),new Stage(12,13,10,3,2,0,8)
        };
        // Drawn by hand: a formula on so few cells reads as a blob, not a heart.
        private static readonly string[] Heart=
        {
            ".###..###.",
            "##########",
            "##########",
            "##########",
            "##########",
            "##########",
            ".########.",
            ".########.",
            "..######..",
            "...####...",
            "....##...."
        };
        // Level 6 onwards leaves parts of the board empty to draw a shape.
        private static bool InShape(int number,Vector2Int cell,int width,int height)
        {
            float x=(cell.x+.5f)/width*2-1,y=(cell.y+.5f)/height*2-1;
            switch(number)
            {
                case 6: return Mathf.Abs(x)+Mathf.Abs(y)<=.95f;
                case 7: return Mathf.Abs(x)<=.48f+.75f*Mathf.Abs(y);
                case 8: return Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))>=.58f;
                case 9: return Mathf.Abs(y)>=.14f;
                case 10:
                {
                    int row=height-2-cell.y,column=cell.x-1;
                    return row>=0&&row<Heart.Length&&column>=0&&column<Heart[row].Length&&Heart[row][column]=='#';
                }
                default: return true;
            }
        }
        public static LevelData Get(int number)
        {
            var stage=Stages[Mathf.Clamp(number,1,Stages.Length)-1];
            int width=stage.width,height=stage.height;
            var level=new LevelData{number=number,width=width,height=height,showcase=true,gate=new Vector2Int(width*2/3,height+2)};
            int gateX=level.gate.x;
            float top=height-.4f,bottom=-.6f,left=-.3f,right=width-.7f;
            level.channelLines.Add(ScrewConveyorGeometry.Rounded(new[]{new Vector2(gateX,top),new Vector2(left,top),new Vector2(left,bottom),new Vector2(right,bottom),new Vector2(right,top)},true,1.95f));
            level.channelLines.Add(ScrewConveyorGeometry.Resample(new[]{new Vector2(gateX,top),new Vector2(gateX,height+2)},.12f));
            if(number==3)level.channelLines.Add(Smooth(new[]{new Vector2(2.5f,bottom),new Vector2(3,3),new Vector2(4.5f,6),new Vector2(5.5f,9),new Vector2(gateX,top)},16));
            if(number==4)level.channelLines.Add(Smooth(new[]{new Vector2(4.5f,bottom),new Vector2(7.2f,.9f),new Vector2(7.6f,2.6f),new Vector2(5.4f,4.8f),new Vector2(2.2f,7),new Vector2(1.8f,9.2f),new Vector2(3.2f,11.4f),new Vector2(5.8f,13.2f),new Vector2(gateX,top)},16));
            if(number==5)
            {
                level.channelLines.Add(Smooth(new[]{new Vector2(2.5f,bottom),new Vector2(1.8f,3.8f),new Vector2(3.2f,8.5f),new Vector2(5.5f,13.2f),new Vector2(gateX,top)},16));
                level.channelLines.Add(Smooth(new[]{new Vector2(7.8f,bottom),new Vector2(8.3f,3.8f),new Vector2(7.8f,8.5f),new Vector2(7.7f,13.2f),new Vector2(gateX,top)},16));
            }
            if(number==7)level.channelLines.Add(Smooth(new[]{new Vector2(5,bottom),new Vector2(8,1),new Vector2(8.5f,3),new Vector2(6,5.5f),new Vector2(2.5f,8),new Vector2(2,10.5f),new Vector2(3.5f,13),new Vector2(6.5f,15),new Vector2(gateX,top)},16));
            if(number==9)level.channelLines.Add(Smooth(new[]{new Vector2(3,bottom),new Vector2(3,4),new Vector2(5,9),new Vector2(7,13),new Vector2(gateX,top)},16));
            var random=new System.Random(7100+number);
            var water=new List<Vector2Int>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var cell=new Vector2Int(x,y);
                if(!ReefWaterDomain.ContainsWholeCell(level,cell))level.channelCells.Add(cell);
                else if(InShape(number,cell,width,height))water.Add(cell);
            }
            level.channelCells.Add(level.gate);
            var blocked=new HashSet<Vector2Int>();
            var crabs=PlaceObstacles(level,water,blocked,random,stage.crabs,ObstacleKind.Crab);
            var rocks=PlaceObstacles(level,water,blocked,random,stage.rocks,ObstacleKind.Rock);
            // Each crab charges one fish a life to pass; Rescue is the way to avoid paying.
            foreach(var crab in crabs)AddFacingFish(level,water,blocked,crab,random);
            // Each rock holds up one fish until it has been hit twice.
            foreach(var rock in rocks)AddFacingFish(level,water,blocked,rock,random);
            // Shaped boards stay full, or stray gaps would blur the outline.
            float holes=number==1||number>=6?0:.03f;
            foreach(var cell in water)
            {
                if(blocked.Contains(cell)||random.NextDouble()<holes)continue;
                level.fish.Add(new FishData{id=level.fish.Count,cell=cell,facing=Facing.Up,appearance=Palette(number,cell,height)});
            }
            MixDirections(level,blocked,random,stage.depth);return level;
        }
        // Each stage uses two character models only, in bands early on and patches later.
        private static int Palette(int number,Vector2Int cell,int height)
        {
            var stage=Stages[Mathf.Clamp(number,1,Stages.Length)-1];
            bool first=number<=2?cell.y>=height/2:(cell.x/3+cell.y/3)%2==0;
            return first?stage.first:stage.second;
        }
        private static List<Vector2Int> PlaceObstacles(LevelData level,List<Vector2Int> water,HashSet<Vector2Int> blocked,System.Random random,int count,ObstacleKind kind)
        {
            var placed=new List<Vector2Int>();int attempts=0;
            while(placed.Count<count)
            {
                if(++attempts>2000)throw new InvalidOperationException("Cannot place obstacles");
                // Of a few random spots, keep the one farthest from what is already placed, so hazards spread out.
                var cell=new Vector2Int(-1,-1);float farthest=-1;
                for(int sample=0;sample<SpreadSamples;sample++)
                {
                    var candidate=water[random.Next(water.Count)];
                    if(!CanPlace(candidate,water,blocked))continue;
                    float nearest=float.MaxValue;
                    foreach(var other in blocked)nearest=Mathf.Min(nearest,Vector2Int.Distance(candidate,other));
                    if(nearest>farthest){farthest=nearest;cell=candidate;}
                }
                if(farthest<0)continue;
                blocked.Add(cell);placed.Add(cell);level.obstacles.Add(new ObstacleData{cell=cell,kind=kind});
            }
            return placed;
        }
        // An obstacle needs free water on all four sides, so a fish can face it and others can pass.
        private static bool CanPlace(Vector2Int cell,List<Vector2Int> water,HashSet<Vector2Int> blocked)
        {
            if(blocked.Contains(cell))return false;
            foreach(var step in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                if(!water.Contains(cell+step)||blocked.Contains(cell+step))return false;
            return true;
        }
        private static void AddFacingFish(LevelData level,List<Vector2Int> water,HashSet<Vector2Int> blocked,Vector2Int crab,System.Random random)
        {
            int start=random.Next(4);
            for(int i=0;i<4;i++)
            {
                var facing=(Facing)((start+i)%4);var cell=crab-BoardState.Direction(facing);
                if(!water.Contains(cell)||blocked.Contains(cell)||!ClearBeyond(level,blocked,crab,BoardState.Direction(facing)))continue;
                blocked.Add(cell);level.fish.Add(new FishData{id=level.fish.Count,cell=cell,facing=facing,appearance=Palette(level.number,cell,level.height)});return;
            }
            throw new InvalidOperationException("Obstacle has no free neighbour");
        }
        // Once the toll is paid, nothing fixed may stand between the crab and the road.
        private static bool ClearBeyond(LevelData level,HashSet<Vector2Int> blocked,Vector2Int crab,Vector2Int step)
        {
            for(var p=crab+step;p.x>=0&&p.y>=0&&p.x<level.width&&p.y<level.height&&!level.IsChannel(p);p+=step)if(blocked.Contains(p))return false;
            return true;
        }
        // Builds the solution backwards from the first fish to leave, so every stage stays solvable.
        // A fish whose way out crosses earlier leavers must wait for them, which adds a wave.
        private static void MixDirections(LevelData level,HashSet<Vector2Int> blocked,System.Random random,int targetDepth)
        {
            var remaining=new HashSet<Vector2Int>();
            foreach(var fish in level.fish)if(!blocked.Contains(fish.cell))remaining.Add(fish.cell);
            var depth=new Dictionary<Vector2Int,int>();
            while(remaining.Count>0)
            {
                var options=new List<KeyValuePair<FishData,Facing>>();var depths=new List<int>();int best=0;
                foreach(var fish in level.fish)
                {
                    if(!remaining.Contains(fish.cell))continue;
                    for(int d=0;d<4;d++)
                    {
                        var step=BoardState.Direction((Facing)d);var p=fish.cell+step;int wave=1;
                        while(!level.IsChannel(p)&&!remaining.Contains(p)&&!blocked.Contains(p)&&p.x>=0&&p.y>=0&&p.x<level.width&&p.y<level.height)
                        {
                            if(depth.TryGetValue(p,out var ahead))wave=Mathf.Max(wave,ahead+1);
                            p+=step;
                        }
                        if(!level.IsChannel(p))continue;
                        options.Add(new KeyValuePair<FishData,Facing>(fish,(Facing)d));depths.Add(wave);
                    }
                }
                if(options.Count==0)throw new InvalidOperationException("Cannot construct a solvable school");
                // Aim as deep as the target allows; past it, take the shallowest way to stay solvable.
                best=int.MaxValue;
                foreach(var wave in depths)if(wave>targetDepth&&wave<best)best=wave;
                int deepest=0;
                foreach(var wave in depths)if(wave<=targetDepth&&wave>deepest)deepest=wave;
                if(deepest>0)best=deepest;
                int pick=random.Next(options.Count);
                if(depths[pick]>best||random.NextDouble()<.7)
                {
                    var chosen=new List<int>();
                    for(int i=0;i<options.Count;i++)if(depths[i]==best)chosen.Add(i);
                    pick=chosen[random.Next(chosen.Count)];
                }
                var option=options[pick];option.Key.facing=option.Value;depth[option.Key.cell]=depths[pick];remaining.Remove(option.Key.cell);
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
