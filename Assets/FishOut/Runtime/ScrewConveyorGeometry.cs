// Adapted from screw-bubble-sort/Assets/Game/ScrewBubbleSort3D/Scripts/Runtime/
// FunnelBoardView.cs: Rounded, Ribbon, Tube (2026-11-05). No Screw project edits.
using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    public static class ScrewConveyorGeometry
    {
        public static Vector2[] Rounded(Vector2[] points,bool closed,float rounding)
        {
            var result=new List<Vector2>();
            for(int i=0;i<points.Length;i++)
            {
                if(!closed&&(i==0||i==points.Length-1)){result.Add(points[i]);continue;}
                var p=points[i];var prev=points[(i+points.Length-1)%points.Length];var next=points[(i+1)%points.Length];
                if(Vector2.Dot((prev-p).normalized,(next-p).normalized)<-.999f){result.Add(p);continue;}
                float amount=Mathf.Min(rounding,Vector2.Distance(p,prev)*.35f,Vector2.Distance(p,next)*.35f);
                var a=p+(prev-p).normalized*amount;var b=p+(next-p).normalized*amount;
                for(int n=0;n<=24;n++){float t=n/24f;result.Add((1-t)*(1-t)*a+2*t*(1-t)*p+t*t*b);}
            }
            if(closed)result.Add(result[0]);return Resample(result,.12f);
        }
        public static Vector2[] Resample(IReadOnlyList<Vector2> path,float pitch)
        {
            var output=new List<Vector2>{path[0]};
            for(int i=1;i<path.Count;i++)
            {
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(path[i-1],path[i])/pitch));
                for(int j=1;j<=steps;j++)output.Add(Vector2.Lerp(path[i-1],path[i],j/(float)steps));
            }
            return output.ToArray();
        }
        public static Mesh Ribbon(IReadOnlyList<Vector3> path,float halfWidth)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var normals=new List<Vector3>();
            for(int i=0;i<path.Count;i++)
            {
                var tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(i-1,0)]).normalized;
                var width=Vector3.Cross(tangent,Vector3.up).normalized*halfWidth;
                vertices.Add(path[i]+width);vertices.Add(path[i]-width);normals.Add(Vector3.up);normals.Add(Vector3.up);
                if(i<path.Count-1){int a=i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
            var mesh=new Mesh{name="Screw conveyor ribbon"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetNormals(normals);mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Tube(IReadOnlyList<Vector3> path,float radius)
        {
            const int sides=10;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<path.Count;i++)
            {
                var tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
                var right=Vector3.Cross(tangent,Vector3.up).normalized;
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;vertices.Add(path[i]+right*(Mathf.Cos(a)*radius)+Vector3.up*(Mathf.Sin(a)*radius));
                    if(i==path.Count-1)continue;
                    int x=i*sides+j,y=i*sides+(j+1)%sides,nx=(i+1)*sides+j,ny=(i+1)*sides+(j+1)%sides;
                    triangles.AddRange(new[]{x,nx,y,y,nx,ny});
                }
            }
            var mesh=new Mesh{name="Screw conveyor rounded rim"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
