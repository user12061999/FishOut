using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishOut.Editor
{
    public static class ReefAssetBuilder
    {
        private const string Folder="Assets/FishOut/Resources/Reef/";
        [MenuItem("Fish Out/Generate reef assets")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var sand=Material("Sand",new Color(1f,.79f,.43f));Material("SandLight",new Color(1f,.86f,.57f));
            Material("Current",new Color(.14f,.72f,.78f));Material("Shore",new Color(.92f,.65f,.29f));
            var pink=Material("CoralPinkMaterial",new Color(1f,.34f,.54f));
            var gold=Material("CoralGoldMaterial",new Color(1f,.66f,.16f));
            var green=Material("SeaweedMaterial",new Color(.19f,.68f,.43f));
            var mint=Material("GateMint",new Color(.39f,.95f,.86f));
            var shell=Material("ShellMaterial",new Color(1f,.77f,.68f));
            Save("CoralPink",()=>Coral(pink));Save("CoralGold",()=>Coral(gold));
            Save("Seaweed",()=>
            {
                var root=new GameObject("Seaweed");
                for(int i=0;i<7;i++)
                {
                    var leaf=Part(root,PrimitiveType.Capsule,new Vector3((i-3)*.13f,.45f+(i%3)*.15f,0),new Vector3(.14f,.5f+(i%3)*.1f,.22f),green);
                    leaf.transform.rotation=Quaternion.Euler(15+(i%2)*20,0,(3-i)*13);
                }
                return root;
            });
            Save("Shell",()=>
            {
                var root=new GameObject("Scallop shell");
                for(int i=0;i<7;i++)
                {
                    float angle=(i-3)*16*Mathf.Deg2Rad;
                    var rib=Part(root,PrimitiveType.Capsule,new Vector3(Mathf.Sin(angle)*.28f,.12f,Mathf.Cos(angle)*.28f),new Vector3(.15f,.34f,.12f),shell);
                    rib.transform.rotation=Quaternion.Euler(90,0,-(i-3)*16);
                }
                return root;
            });
            Save("ExitGate",()=>
            {
                var root=new GameObject("Pearl sea gate");
                for(int i=0;i<20;i++)
                {
                    float a=i/20f*Mathf.PI,b=(i+1)/20f*Mathf.PI;
                    var from=new Vector3(Mathf.Cos(a)*1.6f,.15f+Mathf.Sin(a)*1.95f,0);
                    var to=new Vector3(Mathf.Cos(b)*1.6f,.15f+Mathf.Sin(b)*1.95f,0);
                    Branch(root,from,to,.23f,i%4==0?mint:sand);
                }
                Part(root,PrimitiveType.Sphere,new Vector3(-1.6f,.1f,0),new Vector3(.9f,.5f,1f),gold);
                Part(root,PrimitiveType.Sphere,new Vector3(1.6f,.1f,0),new Vector3(.9f,.5f,1f),gold);
                Part(root,PrimitiveType.Sphere,new Vector3(0,2.25f,0),new Vector3(.65f,.65f,.4f),pink);
                return root;
            });
            var colors=new[]{new Color(.12f,.69f,1f),new Color(.36f,.88f,.28f),new Color(.65f,.27f,.94f),new Color(1f,.25f,.32f),new Color(1f,.79f,.12f)};
            var names=new[]{"Blue","Green","Purple","Red","Yellow"};
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cube Animals 02/Customize Here/00 Sample W Root/Fish Grey.prefab");
            for(int i=0;i<names.Length;i++)
            {
                var body=Material("Fish"+names[i]+"Material",colors[i]);string asset="Fish"+names[i];
                Save(asset,()=>
                {
                    var go=UnityEngine.Object.Instantiate(source);go.name=asset;
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>())if(renderer.name=="Fish")renderer.sharedMaterial=body;
                    return go;
                });
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("Generated reef environment and five fish color prefabs in "+Folder);
        }
        private static Material Material(string name,Color color)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(existing)
            {
                if(name=="Current"||name=="Shore"){existing.SetFloat("_Cull",0);EditorUtility.SetDirty(existing);}
                return existing;
            }
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};material.SetFloat("_Smoothness",.22f);
            if(name=="Current"||name=="Shore")material.SetFloat("_Cull",0);
            AssetDatabase.CreateAsset(material,Folder+name+".mat");return material;
        }
        private static void Save(string name,Func<GameObject> build)
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"))return;
            var root=build();root.name=name;PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
        private static GameObject Coral(Material material)
        {
            var root=new GameObject("Branching coral");
            Branch(root,Vector3.zero,new Vector3(0,1.1f,0),.13f,material);
            for(int i=0;i<5;i++)
            {
                float side=i%2==0?-1:1;var start=new Vector3(0,.25f+i*.14f,0);var elbow=start+new Vector3(side*.35f,.18f,(i%3-1)*.15f);
                Branch(root,start,elbow,.10f,material);Branch(root,elbow,elbow+Vector3.up*.35f,.09f,material);
                Part(root,PrimitiveType.Sphere,elbow+Vector3.up*.35f,Vector3.one*.20f,material);
            }
            return root;
        }
        private static void Branch(GameObject root,Vector3 from,Vector3 to,float radius,Material material)
        {
            var part=Part(root,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(radius*2,Vector3.Distance(from,to)*.5f,radius*2),material);
            part.transform.rotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
        }
        private static GameObject Part(GameObject root,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
    }
}
