using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FishOut
{
    // Marching triangles clips to the continuous domain, not the fish grid.
    public static class CurvedWaterMesh
    {
        public const float CausticScale=.12f;
        public static Mesh Build(LevelData level,float threshold,float height)
        {
            return Build(level,p=>ReefWaterDomain.SignedDistance(level,p)-threshold,height,new Vector2(-.68f,-.88f),new Vector2(level.width-1+.68f,level.height-1+.88f),.16f,threshold<0?.36f:float.PositiveInfinity);
        }
        // Cells deeper than skipAbove are left out where a higher layer always covers them.
        public static Mesh Build(LevelData level,Func<Vector2,float> distance,float height,Vector2 min,Vector2 max,float pitch,float skipAbove)
        {
            int nx=Mathf.CeilToInt((max.x-min.x)/pitch),ny=Mathf.CeilToInt((max.y-min.y)/pitch);
            var field=new float[nx+1,ny+1];
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)field[x,y]=distance(new Vector2(x*pitch+min.x,y*pitch+min.y));
            var vertices=new List<Vector3>();var triangles=new List<int>();var polygon=new List<Vector3>(4);var welded=new Dictionary<Vector3Int,int>();
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
            {
                if(field[x,y]>skipAbove&&field[x+1,y]>skipAbove&&field[x+1,y+1]>skipAbove&&field[x,y+1]>skipAbove)continue;
                var a=new Vector2(x*pitch+min.x,y*pitch+min.y);var b=a+Vector2.right*pitch;var c=a+Vector2.one*pitch;var d=a+Vector2.up*pitch;
                Clip(level,a,field[x,y],b,field[x+1,y],c,field[x+1,y+1],height,polygon,vertices,triangles,welded);
                Clip(level,a,field[x,y],c,field[x+1,y+1],d,field[x,y+1],height,polygon,vertices,triangles,welded);
            }
            var normals=new Vector3[vertices.Count];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;
            var mesh=new Mesh{name="Continuous curved pond",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.normals=normals;
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x,vertices[i].z)*CausticScale;
            mesh.uv=uv;mesh.RecalculateBounds();return mesh;
        }
        private static void Clip(LevelData level,Vector2 a,float da,Vector2 b,float db,Vector2 c,float dc,float height,List<Vector3> polygon,List<Vector3> vertices,List<int> triangles,Dictionary<Vector3Int,int> welded)
        {
            polygon.Clear();Edge(level,a,da,b,db,height,polygon);Edge(level,b,db,c,dc,height,polygon);Edge(level,c,dc,a,da,height,polygon);
            if(polygon.Count<3)return;
            int first=Weld(polygon[0],vertices,welded);
            for(int i=1;i<polygon.Count-1;i++)
            {
                int second=Weld(polygon[i],vertices,welded),third=Weld(polygon[i+1],vertices,welded);
                bool upward=Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).y>0;
                triangles.Add(first);triangles.Add(upward?second:third);triangles.Add(upward?third:second);
            }
        }
        // Neighbouring triangles share a vertex when they meet within a millimetre.
        private static int Weld(Vector3 point,List<Vector3> vertices,Dictionary<Vector3Int,int> welded)
        {
            var key=new Vector3Int(Mathf.RoundToInt(point.x*1000),0,Mathf.RoundToInt(point.z*1000));
            if(!welded.TryGetValue(key,out var index)){index=vertices.Count;vertices.Add(point);welded[key]=index;}
            return index;
        }
        private static void Edge(LevelData level,Vector2 a,float da,Vector2 b,float db,float height,List<Vector3> output)
        {
            if(da>=0)output.Add(ConveyorNetwork.World(level,a,height));
            if((da>=0)!=(db>=0))output.Add(ConveyorNetwork.World(level,Vector2.Lerp(a,b,da/(da-db)),height));
        }
    }
}
