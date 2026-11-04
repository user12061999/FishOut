using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FishOut
{
    public sealed class ShowcaseScenery : MonoBehaviour
    {
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly List<Material> materials=new List<Material>();
        public void Build(LevelData level,Func<Vector2Int,Vector3> cell)
        {
            var bank=Mat("Curved pond sand bank",new Color(.85f,.57f,.28f));
            var water=Mat("Curved blue water",new Color(.07f,.41f,.55f));
            var belt=Mat("Screw conveyor waterway",new Color(.83f,.50f,.21f));
            var rim=Mat("Screw rounded ivory rim",new Color(1f,.89f,.65f));
            Part("Continuous curved pond bank",CurvedWaterMesh.Build(level,-.15f,-.045f),bank);
            Part("Continuous curved water",CurvedWaterMesh.Build(level,0,-.015f),water);
            for(int line=0;line<level.channelLines.Count;line++)
            {
                var curve=level.channelLines[line];
                var points=new Vector3[curve.Length];
                for(int i=0;i<points.Length;i++)points[i]=ConveyorNetwork.World(level,curve[i],-.13f);
                Part("Screw conveyor path",ScrewConveyorGeometry.Ribbon(points,ReefWaterDomain.ChannelHalfWidth),belt);
                var left=new Vector3[points.Length];var right=new Vector3[points.Length];
                for(int i=0;i<points.Length;i++)
                {
                    var tangent=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(i-1,0)]).normalized;
                    var offset=Vector3.Cross(tangent,Vector3.up).normalized*ReefWaterDomain.ChannelHalfWidth;
                    left[i]=points[i]+offset+Vector3.up*.045f;right[i]=points[i]-offset+Vector3.up*.045f;
                }
                RailRuns(level,line,left,rim);RailRuns(level,line,right,rim);
                if(level.number==8&&line==2)
                {
                    var center=points[0];var tangent=(points[1]-center).normalized;var side=Vector3.Cross(tangent,Vector3.up).normalized;
                    var arc=new List<Vector3>();var vertices=new List<Vector3>{center};var triangles=new List<int>();
                    for(int j=0;j<=32;j++)
                    {
                        float angle=j/32f*Mathf.PI;
                        var p=center+(side*Mathf.Cos(angle)-tangent*Mathf.Sin(angle))*ReefWaterDomain.ChannelHalfWidth;
                        vertices.Add(p);arc.Add(p+Vector3.up*.045f);
                        if(j>0)triangles.AddRange(new[]{0,j+1,j});
                    }
                    var cap=new Mesh{name="Conveyor rounded start cap"};cap.SetVertices(vertices);cap.SetTriangles(triangles,0);cap.RecalculateNormals();
                    Part("Conveyor rounded start cap",cap,belt);Part("Conveyor rounded start rim",ScrewConveyorGeometry.Tube(arc,.045f),rim);
                }
            }
            BuildCells(level,cell);
            var gate=Spawn("ExitGate",cell(level.gate)+new Vector3(0,-.1f,0),1.25f);if(gate)gate.name="Sea gate - shared exit";
            for(int i=0;i<24;i++)
            {
                float z=-9.8f+i%12*1.9f;float x=(i<12?-1:1)*(level.width*.5f+1.1f+Mathf.Sin(i*3.1f)*.25f);
                string asset=i%4==0?"Shell":i%3==0?"CoralGold":i%2==0?"Seaweed":"CoralPink";
                var decoration=Spawn(asset,new Vector3(x,-.3f,z),.6f+(i%3)*.18f);if(decoration)decoration.transform.rotation=Quaternion.Euler(0,i*137,0);
            }
            for(int i=0;i<7;i++)Spawn(i%2==0?"Seaweed":"Shell",new Vector3(-6+i*2,-.3f,-11.7f),.65f);
        }
        private void RailRuns(LevelData level,int ownLine,Vector3[] points,Material material)
        {
            var run=new List<Vector3>();
            foreach(var point in points)
            {
                var local=new Vector2(point.x+(level.width-1)*.5f,point.z+(level.height-1)*.5f);bool junction=false;
                for(int line=0;line<level.channelLines.Count&&!junction;line++)
                {
                    if(line==ownLine)continue;var curve=level.channelLines[line];
                    for(int i=1;i<curve.Length&&!junction;i++)junction=ShowcaseLevels.Distance(local,curve[i-1],curve[i])<ReefWaterDomain.ChannelHalfWidth+.04f;
                }
                if(junction)
                {
                    if(run.Count>1)Part("Screw conveyor joined curved rail",ScrewConveyorGeometry.Tube(run,.045f),material);
                    run.Clear();
                }
                else run.Add(point);
            }
            if(run.Count>1)Part("Screw conveyor joined curved rail",ScrewConveyorGeometry.Tube(run,.045f),material);
        }
        private void BuildCells(LevelData level,Func<Vector2Int,Vector3> cell)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var source=cube.GetComponent<MeshFilter>().sharedMesh;cube.SetActive(false);Destroy(cube);
            for(int parity=0;parity<2;parity++)
            {
                var combine=new List<CombineInstance>();
                foreach(var fish in level.fish)
                {
                    if((fish.cell.x+fish.cell.y)%2!=parity)continue;
                    combine.Add(new CombineInstance{mesh=source,transform=Matrix4x4.TRS(cell(fish.cell)+Vector3.down*.075f,Quaternion.identity,new Vector3(ReefWaterDomain.CellSize,.035f,ReefWaterDomain.CellSize))});
                }
                var mesh=new Mesh{name="Contained blue fish cells"};mesh.CombineMeshes(combine.ToArray());
                Part("One water cell per fish "+parity,mesh,Mat("Blue water cell "+parity,parity==0?new Color(.44f,.79f,.89f):new Color(.37f,.74f,.86f)));
            }
        }
        private Material Mat(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.28f);m.SetFloat("_Cull",0);materials.Add(m);return m;
        }
        private void Part(string name,Mesh mesh,Material material)
        {
            meshes.Add(mesh);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
        }
        private GameObject Spawn(string name,Vector3 position,float scale)
        {
            var prefab=Resources.Load<GameObject>("Reef/"+name);if(!prefab)return null;
            var go=Instantiate(prefab,transform);go.transform.localPosition=position;go.transform.localScale*=scale;
            return go;
        }
        private void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var m in materials)if(m)Destroy(m);}
    }
}
