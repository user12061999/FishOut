using System;
using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    public static class ShowcaseLevels
    {
        private const int SpreadSamples=24;
        // Spec levels (GDD §8): a 6 × 8 school inside the ring road (8 × 10 cells including the road).
        private sealed class SpecStage
        {
            public int fish,rocks,crabs,trapped,depth,first,second;
            public SpecStage(int fishCount,int rockCount,int crabCount,int facingCrab,int waves,int modelA,int modelB)
            {fish=fishCount;rocks=rockCount;crabs=crabCount;trapped=facingCrab;depth=waves;first=modelA;second=modelB;}
        }
        // Fish, rocks, crabs, fish facing a crab (unused since crabs swim out too: the lesson check asks for a crab that can be bumped), waves, two models.
        private static readonly SpecStage[] Spec=
        {
            // Models: 0 fish blue, 1 fish grey, 2 fish orange, 3-5 turtles, 6-8 octopus. The purple octopus (8) was swapped
            // for the grey fish (1) on 08/11: seen from above its symmetric body did not show which way it faces.
            new SpecStage(3,0,0,0,1,0,2),new SpecStage(10,0,0,0,3,2,5),new SpecStage(20,3,0,0,4,0,1),
            new SpecStage(20,0,1,1,4,5,0),new SpecStage(20,2,2,1,5,1,5)
        };
        public const int SpecCount=5;
        public const int SpecWidth=8,SpecHeight=10;
        public static LevelData Get(int number)
        {
            number=Mathf.Clamp(number,1,Spec.Length);
            // Try seeds until the stage also has what its lesson needs (a fish that stops part-way, a crab to warn about).
            for(int attempt=0;attempt<400;attempt++)
            {
                var level=TryBuild(number,1000*number+attempt);
                if(level!=null)return level;
            }
            throw new InvalidOperationException("Cannot build stage "+number);
        }
        private static LevelData TryBuild(int number,int seed)
        {
            var stage=Spec[number-1];int width=SpecWidth,height=SpecHeight;
            var level=new LevelData{number=number,width=width,height=height,showcase=true};
            // The conveyor follows the sand road and cave painted on the client's background.
            PaintedBackdrop.Roads(level,out var ring,out var road,out level.gate);
            level.channelLines.Add(ring);level.channelLines.Add(road);
            var random=new System.Random(seed);
            var water=new List<Vector2Int>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var cell=new Vector2Int(x,y);
                // A cell on a rounded corner of the painted board is sand, so fish never start there.
                if(!ReefWaterDomain.ContainsWholeCell(level,cell)||!PaintedBackdrop.InsideCut(level,cell,CornerMargin))level.channelCells.Add(cell);
                else water.Add(cell);
            }
            level.channelCells.Add(level.gate);
            var blocked=new HashSet<Vector2Int>();var crabs=new List<Vector2Int>();
            try
            {
                // Crab cells are spread like obstacles (free water on every side), then become pieces that swim out too.
                crabs=PlaceObstacles(level,water,blocked,random,stage.crabs,ObstacleKind.Crab);
                level.obstacles.RemoveAll(o=>o.kind==ObstacleKind.Crab);
                PlaceObstacles(level,water,blocked,random,stage.rocks,ObstacleKind.Rock);
            }
            catch(InvalidOperationException){return null;}
            var free=new List<Vector2Int>();foreach(var cell in water)if(!blocked.Contains(cell))free.Add(cell);
            for(int i=free.Count-1;i>0;i--){int j=random.Next(i+1);var t=free[i];free[i]=free[j];free[j]=t;}
            int need=stage.fish-level.fish.Count;
            for(int i=0;i<need&&i<free.Count;i++)
                level.fish.Add(new FishData{id=level.fish.Count,cell=free[i],facing=Facing.Up,appearance=Palette(number,free[i],height)});
            foreach(var cell in crabs){blocked.Remove(cell);level.fish.Add(new FishData{id=level.fish.Count,cell=cell,facing=Facing.Up,appearance=-1,crab=true});}
            try{MixDirections(level,blocked,random,stage.depth);}catch(InvalidOperationException){return null;}
            return Teaches(level,number)?level:null;
        }
        private const float CornerMargin=.3f;
        // A busy stage reads better when no direction dominates the school.
        private const float MostOneDirection=.4f;
        // Stage 2 needs a fish that stops part-way; stages with crabs need a crab that can actually be hit.
        private static bool Teaches(LevelData level,int number)
        {
            var board=new BoardState(level);
            if(level.fish.Count>=10)
            {
                var facing=new int[4];foreach(var f in level.fish)facing[(int)f.facing]++;
                foreach(var count in facing)if(count>level.fish.Count*MostOneDirection)return false;
            }
            if(number==1)foreach(var f in board.Fish)if(!board.Preview(f.id).exits)return false;
            if(number==2)
            {
                bool stops=false;foreach(var f in board.Fish){var m=board.Preview(f.id);if(m.ChangesBoard&&!m.exits)stops=true;}
                if(!stops)return false;
            }
            // A crab stage needs a fish that could bump a crab, and no crab may start already able to run straight out
            // before the player has seen it, so each crab first needs another piece to leave.
            if(level.fish.Exists(f=>f.crab))
            {
                bool danger=false;foreach(var f in board.Fish)if(!f.crab&&board.Preview(f.id).hitsCrab)danger=true;
                if(!danger)return false;
                foreach(var f in board.Fish)if(f.crab&&board.Preview(f.id).exits)return false;
            }
            return true;
        }
        // Each stage uses two character models only, in bands early on and patches later.
        private static int Palette(int number,Vector2Int cell,int height)
        {
            var stage=Spec[Mathf.Clamp(number,1,Spec.Length)-1];
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
