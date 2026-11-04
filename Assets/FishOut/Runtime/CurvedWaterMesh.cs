using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FishOut
{
    // Marching triangles clips to the continuous domain, not the fish grid.
    public static class CurvedWaterMesh
    {
        public static Mesh Build(LevelData level,float threshold,float height)
        {
            const float pitch=.16f;const int nx=96,ny=136;
            var field=new float[nx+1,ny+1];
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)field[x,y]=ReefWaterDomain.SignedDistance(level,new Vector2(x*pitch-.68f,y*pitch-.88f))-threshold;
            var vertices=new List<Vector3>();var triangles=new List<int>();var polygon=new List<Vector3>(4);
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
            {
                var a=new Vector2(x*pitch-.68f,y*pitch-.88f);var b=a+Vector2.right*pitch;var c=a+Vector2.one*pitch;var d=a+Vector2.up*pitch;
                Clip(level,a,field[x,y],b,field[x+1,y],c,field[x+1,y+1],height,polygon,vertices,triangles);
                Clip(level,a,field[x,y],c,field[x+1,y+1],d,field[x,y+1],height,polygon,vertices,triangles);
            }
            var normals=new Vector3[vertices.Count];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;
            var mesh=new Mesh{name="Continuous curved pond",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.normals=normals;mesh.RecalculateBounds();return mesh;
        }
        private static void Clip(LevelData level,Vector2 a,float da,Vector2 b,float db,Vector2 c,float dc,float height,List<Vector3> polygon,List<Vector3> vertices,List<int> triangles)
        {
            polygon.Clear();Edge(level,a,da,b,db,height,polygon);Edge(level,b,db,c,dc,height,polygon);Edge(level,c,dc,a,da,height,polygon);
            if(polygon.Count<3)return;
            int start=vertices.Count;vertices.AddRange(polygon);
            for(int i=1;i<polygon.Count-1;i++)
            {
                if(Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).y>0)triangles.AddRange(new[]{start,start+i,start+i+1});
                else triangles.AddRange(new[]{start,start+i+1,start+i});
            }
        }
        private static void Edge(LevelData level,Vector2 a,float da,Vector2 b,float db,float height,List<Vector3> output)
        {
            if(da>=0)output.Add(ConveyorNetwork.World(level,a,height));
            if((da>=0)!=(db>=0))output.Add(ConveyorNetwork.World(level,Vector2.Lerp(a,b,da/(da-db)),height));
        }
    }
}
