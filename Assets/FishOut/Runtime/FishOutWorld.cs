using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishOut
{
    public sealed class FishOutWorld : MonoBehaviour
    {
        private Material sand, water, stone, glow;
        private readonly List<Material> owned = new List<Material>();
        private Transform pieces;
        private LevelData level;
        private readonly Dictionary<Vector2Int,List<GameObject>> rocks=new Dictionary<Vector2Int,List<GameObject>>();
        private readonly HashSet<Vector2Int> shownCracked=new HashSet<Vector2Int>();
        public readonly Dictionary<int,FishActor> actors = new Dictionary<int,FishActor>();
        public Vector3 Cell(Vector2Int p) => new Vector3(p.x-(level==null?5:level.width-1)*.5f,.10f,p.y-(level==null?7:level.height-1)*.5f);
        public void Build(FishOutGame game, BoardState board)
        {
            level=board.Level;
            sand=Mat("Lagoon sand",new Color(.60f,.83f,.74f));
            water=Mat("Ocean",new Color(.055f,.39f,.49f));
            if(level.showcase)water.color=new Color(.06f,.45f,.58f);
            stone=Mat("Rock",new Color(.32f,.49f,.49f));
            glow=Mat("Selection",new Color(.95f,.91f,.38f));
            ShowcaseScenery scenery=null;
            if(level.showcase){scenery=gameObject.AddComponent<ShowcaseScenery>();scenery.Build(level,Cell);}
            else Primitive("Ocean floor",PrimitiveType.Cube,new Vector3(0,-.60f,0),new Vector3(80,.3f,80),water,false);
            if(!level.showcase)Primitive("Lagoon board",PrimitiveType.Cube,new Vector3(0,-.23f,0),new Vector3(6.4f,.35f,8.4f),sand,false);
            // The rim indicates the board boundary; it never blocks any of the four exits.
            var rim=new GameObject("Open board boundary"); rim.transform.SetParent(transform,false);
            var line=rim.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=4;
            line.SetPositions(new[]{new Vector3(-3.16f,-.02f,-4.16f),new Vector3(3.16f,-.02f,-4.16f),new Vector3(3.16f,-.02f,4.16f),new Vector3(-3.16f,-.02f,4.16f)});
            line.sharedMaterial=glow; line.widthMultiplier=.025f; line.shadowCastingMode=ShadowCastingMode.Off;
            rim.SetActive(!level.showcase);
            pieces=new GameObject("Fish and obstacles").transform; pieces.SetParent(transform,false);
            foreach(var o in board.Level.obstacles)
            {
                if(o.kind==ObstacleKind.Crab)
                {
                    var crab=Model(game.crabPrefab,pieces,.84f,game.characterAnimations,3); crab.name="Crab hazard"; crab.transform.position=Cell(o.cell);
                    var warning=Primitive("Danger marker",PrimitiveType.Cylinder,Cell(o.cell)+Vector3.down*.07f,new Vector3(.95f,.015f,.95f),Mat("Crab danger",new Color(.98f,.35f,.23f)),false);
                    warning.transform.SetParent(pieces,true);
                }
                else
                {
                    var rock=Primitive("Rock",PrimitiveType.Sphere,Cell(o.cell)+Vector3.up*.16f,new Vector3(.87f,.60f,.83f),stone,true);
                    rock.transform.rotation=Quaternion.Euler(11,37,9); rock.transform.SetParent(pieces,true);
                    var cap=Primitive("Rock cap",PrimitiveType.Sphere,Cell(o.cell)+new Vector3(.12f,.41f,.08f),new Vector3(.45f,.22f,.42f),stone,false);cap.transform.SetParent(pieces,true);
                    rocks[o.cell]=new List<GameObject>{rock,cap};
                }
            }
            var batcher=gameObject.AddComponent<FishBatcher>();
            foreach(var data in board.Fish)
            {
                var root=new GameObject("Fish "+data.id); root.transform.SetParent(pieces,false); root.transform.position=Cell(data.cell);
                root.transform.rotation=Quaternion.Euler(0,(int)data.facing*90f,0);
                var prefab=game.fishPrefabs[data.appearance%game.fishPrefabs.Length];
                if(!prefab)prefab=game.fishPrefabs[0];
                var model=Model(prefab,root.transform,level.showcase?.95f:.83f,game.characterAnimations,(data.appearance%game.fishPrefabs.Length)/3);
                if(scenery)batcher.AddShadow(scenery.ShadowQuad,scenery.ShadowMaterial,root.transform);
                foreach(var face in model.GetComponentsInChildren<MeshRenderer>())
                {
                    var mesh=face.GetComponent<MeshFilter>().sharedMesh;var material=face.sharedMaterial;
                    if(!mesh||mesh.subMeshCount!=1||!material||!material.enableInstancing)continue;
                    batcher.AddPart(mesh,material,face.transform);face.enabled=false;
                }
                var actor=root.AddComponent<FishActor>(); actor.Configure(data.id,model.transform,glow);
                actors.Add(data.id,actor);
                // A collider per visible renderer covers the whole body without a large cell-sized hit target.
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer)) continue;
                    var collider=renderer.gameObject.AddComponent<BoxCollider>();
                    var local=renderer.localBounds; collider.center=local.center; collider.size=Vector3.Max(local.size,Vector3.one*.06f);
                }
            }
        }
        private Material Mat(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name; m.color=color;
            m.SetFloat("_Smoothness",.25f); owned.Add(m); return m;
        }
        private GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,bool collision)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(transform,false); go.transform.position=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision) { var c=go.GetComponent<Collider>(); c.enabled=false; if(Application.isPlaying)Destroy(c); else DestroyImmediate(c); }
            return go;
        }
        private GameObject Model(GameObject prefab,Transform parent,float size,RuntimeAnimatorController[] controllers,int species)
        {
            var holder=new GameObject("Model"); holder.transform.SetParent(parent,false);
            var instance=Instantiate(prefab,holder.transform); instance.name=prefab.name;
            instance.transform.localPosition=Vector3.zero; instance.transform.localRotation=Quaternion.identity;
            foreach(var c in instance.GetComponentsInChildren<Collider>()) c.enabled=false;
            foreach(var animator in instance.GetComponentsInChildren<Animator>())
            {
                animator.applyRootMotion=false;
                if(controllers!=null&&species<controllers.Length)animator.runtimeAnimatorController=controllers[species];
            }
            var renderers=instance.GetComponentsInChildren<Renderer>();
            var bounds=new Bounds(holder.transform.position,Vector3.zero); bool first=true;
            foreach(var r in renderers) { if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds); }
            float factor=size/Mathf.Max(bounds.size.x,bounds.size.z,.01f);
            instance.transform.localScale*=factor;
            var center=holder.transform.InverseTransformPoint(bounds.center);
            // Center the artwork in the cell and put the lowest visible point just above the floor.
            instance.transform.localPosition=new Vector3(-center.x*factor,(-center.y+bounds.size.y*.5f)*factor,-center.z*factor);
            return holder;
        }
        // Shows a hit rock losing its cap, then disappearing when a fish breaks through.
        public void RefreshRocks(BoardState board)
        {
            foreach(var cell in new List<Vector2Int>(rocks.Keys))
            {
                var parts=rocks[cell];
                if(board.IsBroken(cell))
                {
                    foreach(var part in parts)if(part)Destroy(part);
                    rocks.Remove(cell);PlayEffect("Bump",Cell(cell));
                }
                else if(board.IsCracked(cell)&&shownCracked.Add(cell))
                {
                    Destroy(parts[1]);parts[0].transform.localScale*=.82f;PlayEffect("Bump",Cell(cell));
                }
            }
        }
        public void Highlight(int id)
        {
            foreach(var pair in actors) if(pair.Value)pair.Value.Highlight(pair.Key==id);
        }
        public void PlayEffect(string name,Vector3 position)
        {
            var effect=SpawnEffect(name,transform);if(!effect)return;
            effect.transform.position=position;
            float longest=0;
            foreach(var system in effect.GetComponentsInChildren<ParticleSystem>())longest=Mathf.Max(longest,system.main.duration+system.main.startLifetime.constantMax);
            Destroy(effect,Mathf.Max(longest,1));
        }
        public GameObject AttachEffect(string name,Transform owner)
        {
            var effect=SpawnEffect(name,owner);if(effect)effect.transform.localPosition=Vector3.up*.3f;
            return effect;
        }
        private static GameObject SpawnEffect(string name,Transform parent)
        {
            var prefab=Resources.Load<GameObject>("Vfx/"+name);
            return prefab?Instantiate(prefab,parent):null;
        }
        public void Celebrate()
        {
            var go=new GameObject("Clear celebration light",typeof(Light));go.transform.SetParent(transform,false);
            go.transform.position=new Vector3(0,3,0);
            var light=go.GetComponent<Light>();light.type=LightType.Point;light.color=new Color(.65f,.95f,1f);light.range=10;light.intensity=3;
            go.AddComponent<FishOutCelebrationLight>();
        }
        private void OnDestroy()
        {
            foreach(var material in owned) if(material) { if(Application.isPlaying)Destroy(material);else DestroyImmediate(material); }
        }
    }
    public sealed class FishOutCelebrationLight : MonoBehaviour
    {
        private float age;
        private void Update()
        {
            age+=Time.deltaTime;GetComponent<Light>().intensity=3*Mathf.Clamp01(1-age/.65f);
            if(age>=.65f)Destroy(gameObject);
        }
    }
}
