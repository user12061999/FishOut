using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FishOut
{
    public sealed class ShowcaseScenery : MonoBehaviour
    {
        public static readonly Color DeepSea=new Color(.22f,.76f,.87f);
        private static Texture2D strongCaustics,softCaustics,softShadow;
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Material> flowing=new List<Material>();
        private readonly List<Texture2D> textures=new List<Texture2D>();
        public Material ShadowMaterial{get;private set;}
        public Mesh ShadowQuad{get;private set;}
        public void Build(LevelData level,Func<Vector2Int,Vector3> cell)
        {
            if(!strongCaustics)strongCaustics=Caustics(.9f);
            if(!softCaustics)softCaustics=Caustics(.97f);
            if(!softShadow)softShadow=Shadow();
            ShadowMaterial=new Material(Resources.Load<Material>("Reef/SeaShadow")){mainTexture=softShadow};materials.Add(ShadowMaterial);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);ShadowQuad=quad.GetComponent<MeshFilter>().sharedMesh;quad.SetActive(false);Destroy(quad);
            Func<Vector2,float> shore=p=>ReefWaterDomain.ShoreDistance(level,p);
            var min=new Vector2(-9,-10);var max=new Vector2(level.width+8,level.height+10);
            if(PaintedBackdrop.Available){BuildOnPainting(level,cell,min,max);return;}
            flowing.Add(Ground("Deep sea",Sea(min*6,max*6,-.34f),new Color(.24f,.84f,.96f),strongCaustics));
            flowing.Add(Ground("Reef water",CurvedWaterMesh.Build(level,p=>shore(p)+3.4f,-.3f,min,max,.3f,1.5f),new Color(.36f,.95f,.98f),strongCaustics));
            flowing.Add(Ground("Shallow water",CurvedWaterMesh.Build(level,p=>shore(p)+1.15f,-.26f,min,max,.25f,1.5f),new Color(.58f,1f,.95f),strongCaustics));
            Ground("Sand shore",CurvedWaterMesh.Build(level,shore,-.2f,min,max,.25f,float.PositiveInfinity),new Color(.98f,.78f,.45f),softCaustics);
            Ground("Board rim",CurvedWaterMesh.Build(level,-.13f,-.05f),new Color(.92f,.66f,.35f),softCaustics);
            Ground("Board floor",CurvedWaterMesh.Build(level,0,-.04f),new Color(1f,.86f,.6f),softCaustics);
            BuildCells(level,cell);
            var decor=new ReefDecor(9100+level.number);meshes.Add(decor.Ball);meshes.Add(decor.Blade);meshes.Add(decor.Can);
            decor.Cave(cell(level.gate)+new Vector3(0,-.2f,0),Mathf.Clamp(level.width/13f,.65f,1.2f));
            var gate=new GameObject("Sea gate - shared exit");gate.transform.SetParent(transform,false);gate.transform.position=cell(level.gate);
            // Allow for wider screens than this one, since the mesh is not rebuilt on a resize.
            decor.SetView(level,Mathf.Max((float)Screen.width/Mathf.Max(1,Screen.height),.65f));
            decor.Scatter(level);
            MergeDecor(decor);
        }
        // The client's painting supplies the sea, shore, road and cave; only the board itself and the
        // inner water roads that some stages cut through it are still drawn on top.
        private void BuildOnPainting(LevelData level,Func<Vector2Int,Vector3> cell,Vector2 min,Vector2 max)
        {
            PaintedBackdrop.Build(transform,level,Resources.Load<Material>("Reef/SeaUnlit"),meshes,materials,textures);
            if(level.channelLines.Count>2)
                Ground("Inner sand roads",CurvedWaterMesh.Build(level,p=>InnerRoad(level,p),-.02f,min,max,.16f,float.PositiveInfinity),new Color(.96f,.78f,.48f),softCaustics);
            var gate=new GameObject("Sea gate - shared exit");gate.transform.SetParent(transform,false);gate.transform.position=cell(level.gate);
        }
        private static float InnerRoad(LevelData level,Vector2 p)
        {
            float nearest=float.MaxValue;
            for(int line=2;line<level.channelLines.Count;line++)
            {
                var path=level.channelLines[line];
                for(int i=1;i<path.Length;i++)nearest=Mathf.Min(nearest,ShowcaseLevels.Distance(p,path[i-1],path[i]));
            }
            return ReefWaterDomain.ChannelHalfWidth+.12f-nearest;
        }
        // All props share one mesh and one material; each colour is a texel in a small palette.
        private void MergeDecor(ReefDecor decor)
        {
            var palette=new Texture2D(decor.Parts.Count,1,TextureFormat.RGBA32,false){name="Reef palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var colored=new List<CombineInstance>();int index=0;
            foreach(var pair in decor.Parts)
            {
                palette.SetPixel(index,0,pair.Key);
                var mesh=new Mesh{name="Reef colour",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());
                var uv=new Vector2[mesh.vertexCount];
                for(int i=0;i<uv.Length;i++)uv[i]=new Vector2((index+.5f)/decor.Parts.Count,.5f);
                mesh.uv=uv;colored.Add(new CombineInstance{mesh=mesh});index++;
            }
            palette.Apply();
            var merged=new Mesh{name="Reef decoration",indexFormat=IndexFormat.UInt32};merged.CombineMeshes(colored.ToArray(),true,false);
            foreach(var part in colored)Destroy(part.mesh);
            var paint=Lit(Color.white);paint.SetTexture("_BaseMap",palette);textures.Add(palette);
            Part("Reef decoration",merged,paint);
        }
        private void BuildCells(LevelData level,Func<Vector2Int,Vector3> cell)
        {
            float half=ReefWaterDomain.CellSize*.5f;
            for(int parity=0;parity<2;parity++)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();
                for(int y=0;y<level.height;y++)for(int x=0;x<level.width;x++)
                {
                    var p=new Vector2Int(x,y);
                    if((x+y)%2!=parity||!ReefWaterDomain.ContainsWholeCell(level,p))continue;
                    var center=cell(p);center.y=-.03f;int start=vertices.Count;
                    vertices.Add(center+new Vector3(-half,0,-half));vertices.Add(center+new Vector3(-half,0,half));vertices.Add(center+new Vector3(half,0,half));vertices.Add(center+new Vector3(half,0,-half));
                    triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
                Ground("Checker cells "+parity,Flat("Checker cells",vertices,triangles),parity==0?new Color(1f,.93f,.72f):new Color(.99f,.82f,.56f),softCaustics);
            }
        }
        private static Mesh Sea(Vector2 min,Vector2 max,float height)
        {
            return Flat("Open sea",new List<Vector3>{new Vector3(min.x,height,min.y),new Vector3(min.x,height,max.y),new Vector3(max.x,height,max.y),new Vector3(max.x,height,min.y)},new List<int>{0,1,2,0,2,3});
        }
        private static Mesh Flat(string name,List<Vector3> vertices,List<int> triangles)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x,vertices[i].z)*CurvedWaterMesh.CausticScale;
            mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private Material Ground(string name,Mesh mesh,Color color,Texture2D texture)
        {
            var material=new Material(Resources.Load<Material>("Reef/SeaUnlit")){name=name,color=color,mainTexture=texture};
            // Upper layers draw first so early depth rejection skips the layers they cover.
            material.renderQueue=2100+Mathf.RoundToInt(-mesh.bounds.center.y*100);
            materials.Add(material);Part(name,mesh,material);return material;
        }
        private Material Lit(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Reef paint",color=color};
            material.SetFloat("_Smoothness",.42f);materials.Add(material);return material;
        }
        private void Part(string name,Mesh mesh,Material material)
        {
            meshes.Add(mesh);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        private void Update()
        {
            var drift=new Vector2(Mathf.Sin(Time.time*.13f),Mathf.Cos(Time.time*.11f))*.05f;
            foreach(var material in flowing)material.mainTextureOffset=drift;
        }
        // Tiling Voronoi edges read as light ripples on the sea floor.
        private static Texture2D Caustics(float floor)
        {
            const int size=256,count=18;var random=new System.Random(42);var points=new Vector2[count];
            for(int i=0;i<count;i++)points[i]=new Vector2((float)random.NextDouble(),(float)random.NextDouble());
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float first=9,second=9;
                foreach(var point in points)
                {
                    float dx=Mathf.Abs(x/(float)size-point.x),dy=Mathf.Abs(y/(float)size-point.y);
                    dx=Mathf.Min(dx,1-dx);dy=Mathf.Min(dy,1-dy);float d=dx*dx+dy*dy;
                    if(d<first){second=first;first=d;}else if(d<second)second=d;
                }
                float line=Mathf.Clamp01(1-(Mathf.Sqrt(second)-Mathf.Sqrt(first))*34);
                var value=(byte)(Mathf.Lerp(floor,1,line*line)*255);pixels[y*size+x]=new Color32(value,value,value,255);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Caustics",wrapMode=TextureWrapMode.Repeat};
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        private static Texture2D Shadow()
        {
            const int size=64;var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float d=new Vector2(x-31.5f,y-31.5f).magnitude/31.5f;float alpha=Mathf.Clamp01(1-d);
                pixels[y*size+x]=new Color32(92,58,24,(byte)(alpha*alpha*(3-2*alpha)*150));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Soft shadow",wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        private void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var m in materials)if(m)Destroy(m);foreach(var t in textures)if(t)Destroy(t);}
    }
}
