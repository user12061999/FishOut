using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    // Reef props are merged per colour, so a whole level of decoration costs only a few draw calls.
    public sealed class ReefDecor
    {
        private static readonly Color Rock=new Color(.36f,.46f,.74f),RockLight=new Color(.47f,.57f,.82f),Hollow=new Color(.03f,.09f,.2f);
        private static readonly Color Pink=new Color(1f,.42f,.55f),Orange=new Color(1f,.58f,.14f),Purple=new Color(.62f,.36f,.93f),Yellow=new Color(1f,.8f,.22f);
        private static readonly Color Weed=new Color(.16f,.68f,.33f),WeedLight=new Color(.42f,.82f,.32f),Shell=new Color(1f,.9f,.76f),Star=new Color(.98f,.4f,.56f),Bubble=new Color(.72f,.96f,1f);
        public readonly Dictionary<Color,List<CombineInstance>> Parts=new Dictionary<Color,List<CombineInstance>>();
        public readonly Mesh Ball=BuildBall(10,6),Blade=BuildBall(6,4),Can=BuildCan();
        private bool hasView;
        private Quaternion viewInverse;
        private Vector2 viewCenter,viewHalf;
        private readonly System.Random random;
        public ReefDecor(int seed){random=new System.Random(seed);}
        // Merged props cannot be frustum culled, so anything off screen is left out of the mesh.
        public void SetView(LevelData level,float aspect)
        {
            BoardView.Compute(level,aspect,out var size,out viewCenter);
            viewHalf=new Vector2(size*aspect,size);viewInverse=Quaternion.Inverse(BoardView.Rotation);hasView=true;
        }
        public void Scatter(LevelData level)
        {
            float s=Mathf.Clamp(level.width/11f,.75f,1.35f),step=2.2f*s;
            for(float y=-10;y<level.height+10;y+=step)for(float x=-9;x<level.width+8;x+=step)
            {
                var p=new Vector2(x+R(-.9f,.9f)*s,y+R(-.9f,.9f)*s);
                // Outside distance also limits height, so tall props never cover the road on screen.
                float d=-ReefWaterDomain.ShoreDistance(level,p);
                if(d<.7f*s)continue;
                var at=ConveyorNetwork.World(level,p,-.28f);float roll=R(0,1);
                if(d<1.7f*s)
                {
                    if(roll<.3f)Scallop(at,s*.55f);else if(roll<.5f)Starfish(at,s*.5f);else if(roll<.75f)Boulder(at,s*R(.3f,.5f));
                    continue;
                }
                if(roll<.1f)continue;
                float tall=Mathf.Min(d*1.1f,3f*s);
                if(roll<.6f)Boulder(at,Mathf.Min(s*R(.5f,.95f),d*.5f));
                int features=random.Next(1,4);
                for(int i=0;i<features;i++)
                {
                    var offset=at+new Vector3(R(-1,1),0,R(-1,1))*s;float size=Mathf.Min(s*R(.65f,1f),tall/3f);
                    switch(random.Next(4))
                    {
                        case 0:Branch(offset,size);break;
                        case 1:Tubes(offset,size);break;
                        default:Seaweed(offset,size);break;
                    }
                }
                if(random.NextDouble()<.35)Add(Bubble,Blade,at+new Vector3(R(-1,1),R(.5f,1.8f),R(-1,1))*s,Quaternion.identity,Vector3.one*.16f*s);
            }
        }
        public void Cave(Vector3 at,float s)
        {
            var back=at+Vector3.forward*.75f;
            Add(Hollow,Can,back,Quaternion.Euler(-90,0,0),new Vector3(3.7f*s,.5f,3.7f*s));
            for(int i=0;i<=8;i++)
            {
                float a=i/8f*Mathf.PI;var offset=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*1.95f*s;
                Add(i%2==0?Rock:RockLight,Ball,back+offset,Quaternion.Euler(R(-20,20),R(0,360),R(-20,20)),new Vector3(1.45f,1.25f,1.2f)*s);
            }
            Boulder(back+new Vector3(-3.1f,0,.1f)*s,1.25f*s);Boulder(back+new Vector3(3.1f,0,.1f)*s,1.25f*s);
            Branch(back+new Vector3(-3.4f,.5f,.2f)*s,.8f*s);Tubes(back+new Vector3(3.4f,.5f,.2f)*s,.7f*s);Seaweed(back+new Vector3(-2.6f,.2f,.8f)*s,.75f*s);
        }
        private void Boulder(Vector3 at,float s)
        {
            Add(Rock,Ball,at+Vector3.up*.3f*s,Quaternion.Euler(R(-12,12),R(0,360),R(-12,12)),new Vector3(2f,1.25f,1.7f)*s);
            Add(RockLight,Ball,at+new Vector3(.25f,.75f,-.1f)*s,Quaternion.Euler(R(-12,12),R(0,360),0),new Vector3(1.1f,.6f,1f)*s);
        }
        private void Branch(Vector3 at,float s)
        {
            Add(Pink,Ball,at+Vector3.up*.5f*s,Quaternion.identity,new Vector3(.34f,1.2f,.34f)*s);
            float yaw=R(0,360);
            for(int i=0;i<5;i++)
            {
                float lean=-58+i*29;var rotation=Quaternion.Euler(0,yaw,lean);
                var tip=at+Vector3.up*(.75f+.1f*i%2)*s+rotation*Vector3.up*.65f*s;
                Add(Pink,Ball,tip,rotation,new Vector3(.27f,1.25f,.27f)*s);
            }
        }
        private void Tubes(Vector3 at,float s)
        {
            var color=random.Next(3)==0?Yellow:random.Next(2)==0?Orange:Purple;
            for(int i=0;i<3;i++)
            {
                float a=i*2.1f+R(0,.5f),height=R(.8f,1.45f)*s;var foot=at+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.36f*s;
                var rotation=Quaternion.Euler(R(-9,9),0,R(-9,9));
                Add(color,Can,foot,rotation,new Vector3(.6f*s,height,.6f*s));
                Add(Hollow,Blade,foot+rotation*Vector3.up*height,rotation,new Vector3(.4f*s,.05f*s,.4f*s));
            }
        }
        private void Seaweed(Vector3 at,float s)
        {
            int blades=random.Next(2,4);var color=random.Next(2)==0?Weed:WeedLight;
            for(int b=0;b<blades;b++)
            {
                var foot=at+new Vector3(R(-.4f,.4f),0,R(-.4f,.4f))*s;float phase=R(0,6),length=R(1.6f,2.8f)*s;
                for(int i=0;i<6;i++)
                {
                    float t=i/5f,bend=Mathf.Sin(phase+t*4.2f);
                    var point=foot+new Vector3(bend*.24f*s,t*length,0);
                    Add(color,Blade,point,Quaternion.Euler(0,0,-bend*22),new Vector3(.34f*(1.1f-t*.55f)*s,length*.33f,.3f*s));
                }
            }
        }
        private void Scallop(Vector3 at,float s)
        {
            float yaw=R(0,360);
            for(int i=0;i<5;i++)
            {
                var rotation=Quaternion.Euler(0,yaw-44+i*22,0);
                Add(Shell,Ball,at+rotation*Vector3.forward*.32f*s+Vector3.up*.08f*s,rotation,new Vector3(.3f,.16f,.72f)*s);
            }
        }
        private void Starfish(Vector3 at,float s)
        {
            float yaw=R(0,360);
            for(int i=0;i<5;i++)
            {
                var rotation=Quaternion.Euler(0,yaw+i*72,0);
                Add(Star,Ball,at+rotation*Vector3.forward*.32f*s+Vector3.up*.05f*s,rotation,new Vector3(.3f,.1f,.8f)*s);
            }
        }
        private void Add(Color color,Mesh mesh,Vector3 position,Quaternion rotation,Vector3 scale)
        {
            if(hasView)
            {
                var view=viewInverse*position;float reach=scale.magnitude+.25f;
                if(Mathf.Abs(view.x-viewCenter.x)>viewHalf.x+reach||Mathf.Abs(view.y-viewCenter.y)>viewHalf.y+reach)return;
            }
            if(!Parts.TryGetValue(color,out var list))Parts[color]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation,scale)});
        }
        private float R(float min,float max)=>min+(float)random.NextDouble()*(max-min);
        // Unit primitives kept deliberately low-poly: Unity's built-in sphere is about ten times heavier.
        private static Mesh BuildBall(int sides,int rings)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float v=y/(float)rings*Mathf.PI,u=x/(float)sides*Mathf.PI*2;
                vertices.Add(new Vector3(Mathf.Sin(v)*Mathf.Cos(u),Mathf.Cos(v),Mathf.Sin(v)*Mathf.Sin(u))*.5f);
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {
                int a=y*(sides+1)+x,b=a+sides+1;
                // The first and last rings collapse to a point, so one triangle of each quad has no area.
                if(y>0)triangles.AddRange(new[]{a,a+1,b});
                if(y<rings-1)triangles.AddRange(new[]{a+1,b+1,b});
            }
            var mesh=new Mesh{name="Reef ball"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        private static Mesh BuildCan()
        {
            const int sides=10;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int x=0;x<=sides;x++)
            {
                float u=x/(float)sides*Mathf.PI*2;var edge=new Vector3(Mathf.Cos(u),0,Mathf.Sin(u))*.5f;
                vertices.Add(edge);vertices.Add(edge+Vector3.up);
            }
            for(int x=0;x<sides;x++)
            {
                int a=x*2;
                triangles.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});
            }
            int center=vertices.Count;vertices.Add(Vector3.up);
            for(int x=0;x<sides;x++)triangles.AddRange(new[]{center,x*2+3,x*2+1});
            var mesh=new Mesh{name="Reef can"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
    }
}
