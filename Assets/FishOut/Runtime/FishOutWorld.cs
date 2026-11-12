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
        private readonly Dictionary<int,UnityEngine.UI.Text> crabCounters=new Dictionary<int,UnityEngine.UI.Text>();
        private readonly Dictionary<int,FishOutCrabBomb> crabBombs=new Dictionary<int,FishOutCrabBomb>();
        public Vector3 Cell(Vector2Int p) => level==null?new Vector3(p.x-5*.5f,.10f,p.y-7*.5f):ConveyorNetwork.World(level,p,.10f);
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
                // Crabs are pieces now and are built with the fish below; every obstacle here is a rock.
                {
                    var rock=Primitive("Rock",PrimitiveType.Sphere,Cell(o.cell)+Vector3.up*.16f,new Vector3(.87f,.60f,.83f),stone,true);
                    rock.transform.rotation=Quaternion.Euler(11,37,9); rock.transform.SetParent(pieces,true);
                    var cap=Primitive("Rock cap",PrimitiveType.Sphere,Cell(o.cell)+new Vector3(.12f,.41f,.08f),new Vector3(.45f,.22f,.42f),stone,false);cap.transform.SetParent(pieces,true);
                    rocks[o.cell]=new List<GameObject>{rock,cap};
                }
            }
            // Obstacles further up the painted board are drawn a little smaller.
            if(PaintedBackdrop.Warps(level))foreach(Transform piece in pieces){piece.localScale*=PaintedBackdrop.ScaleAt(piece.position);piece.position+=PaintedBackdrop.PieceShift(piece.position);}
            var batcher=gameObject.AddComponent<FishBatcher>();
            foreach(var data in board.Fish)
            {
                var root=new GameObject((data.crab?"Crab ":"Fish ")+data.id); root.transform.SetParent(pieces,false); root.transform.position=Cell(data.cell);
                root.transform.rotation=Quaternion.Euler(0,PaintedBackdrop.Warps(level)?PaintedBackdrop.Yaw(level,data.cell,BoardState.Direction(data.facing)):(int)data.facing*90f,0);
                GameObject model;
                if(data.crab)
                {
                    // A crab swims out like a fish: same actor, its own model, a red disc under it and its bump counter on top.
                    // The crab rig's skinned bounds are far wider than its body, so it needs a larger fit size to read as big as a fish.
                    model=Model(game.crabPrefab,root.transform,CrabSize,game.characterAnimations,3);
                    // Crabs walk sideways: turn the model so its side leads the way it swims.
                    model.transform.localRotation=Quaternion.Euler(0,90,0);
                    var warning=Primitive("Danger marker",PrimitiveType.Cylinder,root.transform.position+Vector3.down*.07f,new Vector3(.95f,.015f,.95f),Mat("Crab danger",new Color(.98f,.35f,.23f)),false);
                    warning.transform.SetParent(root.transform,true);
                    CrabArrow(root.transform);
                    CrabCounter(game,root.transform,data.id,board.CrabLeft(data.id));
                }
                else
                {
                    var prefab=game.fishPrefabs[data.appearance%game.fishPrefabs.Length];
                    if(!prefab)prefab=game.fishPrefabs[0];
                    model=Model(prefab,root.transform,level.showcase?.95f:.83f,game.characterAnimations,(data.appearance%game.fishPrefabs.Length)/3);
                }
                if(scenery)batcher.AddShadow(scenery.ShadowQuad,scenery.ShadowMaterial,root.transform);
                if(!data.crab)foreach(var face in model.GetComponentsInChildren<MeshRenderer>())
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
            // A player build strips collider classes nothing references (the crab marker's cylinder has no CapsuleCollider on device), so the collider may be missing.
            if(!collision) { var c=go.GetComponent<Collider>(); if(c){ c.enabled=false; if(Application.isPlaying)Destroy(c); else DestroyImmediate(c); } }
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
        public static float CrabSize=1.425f;
        // Three white chevrons (dark outline) on the floor ahead of the crab show which way it will walk; a wave runs along them.
        private void CrabArrow(Transform crab)
        {
            var holder=new GameObject("Crab chevrons");holder.transform.SetParent(crab,false);
            var pulse=holder.AddComponent<FishOutChevronPulse>();
            for(int i=0;i<3;i++)
            {
                float z=.55f+i*.216f;
                pulse.Add(ChevronMesh(holder.transform,"Crab chevron outline",z,1.04f,-.050f,new Color(.30f,.05f,.04f),3100+i*2),i);
                pulse.Add(ChevronMesh(holder.transform,"Crab chevron",z,.8f,-.046f,Color.white,3101+i*2),i);
            }
        }
        private Material ChevronMesh(Transform parent,string name,float z,float scale,float height,Color color,int queue)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3(0,height,z);go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one*scale;
            // A flat "V" pointing along +Z (the crab's facing).
            var v=new[]{new Vector3(-.18f,0,-.04f),new Vector3(0,0,.14f),new Vector3(.18f,0,-.04f),new Vector3(.18f,0,-.14f),new Vector3(0,0,.04f),new Vector3(-.18f,0,-.14f)};
            var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=new[]{0,1,4,0,4,5,1,2,3,1,3,4};mesh.RecalculateNormals();mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=OnTopMat(name,color,queue);r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            return r.sharedMaterial;
        }
        // The crab's body would hide its chevrons and number, so they ignore depth and draws over the crab (UI/Default is always in builds).
        private Material OnTopMat(string name,Color color,int queue)
        {
            var m=new Material(Shader.Find("UI/Default")); m.name=name; m.color=color;
            m.SetInt("unity_GUIZTestMode",(int)CompareFunction.Always); m.renderQueue=queue; owned.Add(m); return m;
        }
        // Each crab carries a cartoon bomb (partner art) with its bump counter printed on it, always facing the camera.
        // The fuse burns the whole time; the bomb glows red at its last bump and blows up when it runs out.
        public const float BombScale=.62f, BackHeight=.52f, BombHeight=1.12f, NumberDrop=.38f;
        private void CrabCounter(FishOutGame game,Transform crab,int id,int value)
        {
            var cam=Camera.main;
            
            var prefab=Resources.Load<GameObject>("Props/CrabBomb");
            FishOutCrabBomb bomb=null;
            if(prefab)
            {
                var go=Instantiate(prefab,crab,false);go.name="Crab bomb";
                go.transform.localScale=Vector3.one*BombScale;go.transform.position=OnBack(cam,crab.position);
                if(cam){var flat=Vector3.ProjectOnPlane(cam.transform.position-crab.position,Vector3.up);if(flat.sqrMagnitude>.001f)go.transform.rotation=Quaternion.LookRotation(flat);}
                bomb=go.AddComponent<FishOutCrabBomb>();
            }
            // The number is printed on the lower half of the bomb, centred under its fuse.
            var counter=new GameObject("Crab counter",typeof(RectTransform));
            Vector3 middle=OnBack(cam,crab.position);float radius=.41f*BombScale;
            var toCamera=cam?(cam.transform.position-middle).normalized:Vector3.zero;
            counter.transform.SetParent(bomb?bomb.transform:crab,false);
            counter.transform.position=StackedOver(cam,crab.position,middle+toCamera*radius*1.1f+Vector3.down*radius*NumberDrop);
            counter.transform.localScale=Vector3.one*.0042f;counter.transform.localScale/=counter.transform.parent.lossyScale.x;
            var canvas=counter.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=5;
            ((RectTransform)counter.transform).sizeDelta=new Vector2(110,110);
            counter.AddComponent<FishOutFaceCamera>();
            var text=new GameObject("Number",typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();text.transform.SetParent(counter.transform,false);
            ((RectTransform)text.transform).sizeDelta=new Vector2(110,110);text.alignment=TextAnchor.MiddleCenter;text.fontSize=96;text.color=Color.white;text.raycastTarget=false;
            // Digits taller than the 110 box would be hidden by Text's truncation.
            text.verticalOverflow=VerticalWrapMode.Overflow;text.horizontalOverflow=HorizontalWrapMode.Overflow;
            text.font=game.Hud&&game.Hud.font?game.Hud.font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.material=OnTopMat("Crab number",Color.white,3200);
            var dark=new Color(.30f,.05f,.04f);
            var edge=text.gameObject.AddComponent<UnityEngine.UI.Outline>();edge.effectColor=dark;edge.effectDistance=new Vector2(2.5f,-2.5f);
            var edge2=text.gameObject.AddComponent<UnityEngine.UI.Outline>();edge2.effectColor=dark;edge2.effectDistance=new Vector2(-2.5f,2.5f);
            text.text=value.ToString();crabCounters[id]=text;
            if(bomb){crabBombs[id]=bomb;bomb.SetLeft(value);}
        }
        // The camera looks down at an angle, so a point straight above an off-centre crab drifts sideways on screen.
        // Slide it along its own depth until it sits on the same screen column as the crab.
        private static Vector3 StackedOver(Camera cam,Vector3 anchor,Vector3 point)
        {
            if(!cam)return point;
            var screen=cam.WorldToScreenPoint(point);screen.x=cam.WorldToScreenPoint(anchor).x;
            return cam.ScreenToWorldPoint(screen);
        }
        // Seen from the camera the bomb covers the middle of the crab's back: it rides the camera ray through that spot,
        // lifted to BombHeight so the shell sits in front of the body instead of sinking into it.
        private static Vector3 OnBack(Camera cam,Vector3 crab)
        {
            var back=crab+Vector3.up*BackHeight;
            if(!cam)return crab+Vector3.up*BombHeight;
            var ray=(cam.transform.position-back).normalized;
            return ray.y>.05f?back+ray*((BombHeight-BackHeight)/ray.y):crab+Vector3.up*BombHeight;
        }
        public void RefreshCrab(BoardState board,int id)
        {
            if(!crabCounters.TryGetValue(id,out var text)||!text)return;
            int left=board.CrabLeft(id);
            text.text=Mathf.Max(0,left).ToString();
            if(crabBombs.TryGetValue(id,out var bomb)&&bomb){bomb.SetLeft(left);StartCoroutine(Pop(bomb.transform));}
            else StartCoroutine(Pop(text.transform.parent));
        }
        // Blows up every bomb whose crab has no bumps left; true when at least one went off.
        public bool ExplodeCrabs(BoardState board)
        {
            bool any=false;
            foreach(var pair in crabBombs)
            {
                if(!pair.Value||board.CrabLeft(pair.Key)>0)continue;
                var position=pair.Value.transform.position;
                var blast=SpawnEffect("BombBlast",transform);
                if(blast){blast.transform.position=position;blast.transform.localScale=Vector3.one*BombBlastScale;Destroy(blast,3);}
                pair.Value.gameObject.SetActive(false);any=true;
            }
            return any;
        }
        public const float BombBlastScale=.6f;
        private System.Collections.IEnumerator Pop(Transform badge)
        {
            var size=badge.localScale;
            for(float t=0;t<.3f&&badge;t+=Time.deltaTime){badge.localScale=size*(1+.45f*Mathf.Sin(t/.3f*Mathf.PI));yield return null;}
            if(badge)badge.localScale=size;
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
    public sealed class FishOutChevronPulse : MonoBehaviour
    {
        private readonly List<Material> materials=new List<Material>();private readonly List<int> steps=new List<int>();private readonly List<Color> colors=new List<Color>();
        public void Add(Material material,int step){materials.Add(material);steps.Add(step);colors.Add(material.color);}
        // A bright wave runs from the crab outwards about once a second.
        private void Update()
        {
            for(int i=0;i<materials.Count;i++)
            {
                if(!materials[i])continue;
                float wave=.5f+.5f*Mathf.Cos((Time.time*1.2f-steps[i]/3f)*Mathf.PI*2);
                var c=colors[i];c.a=Mathf.Lerp(.45f,1f,wave);materials[i].color=c;
            }
        }
    }
    // Last-bump warning on a crab's bomb: a red shell over the black bomb that warms at 2 and pulses at 1.
    // The shell uses "UI/Default" (always in the build) with normal depth testing, so the crab still covers it.
    public sealed class FishOutCrabBomb : MonoBehaviour
    {
        public static readonly Color Danger=new Color(1f,.12f,.04f);
        private Material heat;private int left=3;
        public int Left=>left;
        public float Heat=>heat?heat.color.a:0;
        private void Awake()
        {
            var source=GetComponentInChildren<MeshFilter>();
            if(!source||!source.sharedMesh)return;
            var shell=new GameObject("Heat",typeof(MeshFilter),typeof(MeshRenderer));
            shell.transform.SetParent(source.transform,false);shell.transform.localScale=Vector3.one*1.03f;
            shell.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
            heat=new Material(Shader.Find("UI/Default")){name="Bomb heat",renderQueue=3050};
            heat.SetInt("unity_GUIZTestMode",(int)CompareFunction.LessEqual);
            var renderer=shell.GetComponent<MeshRenderer>();renderer.sharedMaterial=heat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            Apply(0);
        }
        public void SetLeft(int value){left=value;if(left>1)Apply(left==2?.22f:0);}
        private void Update(){if(left<=1)Apply(Mathf.Lerp(.35f,.8f,.5f+.5f*Mathf.Sin(Time.time*Mathf.PI*2*2.2f)));}
        private void Apply(float alpha){if(!heat)return;var c=Danger;c.a=alpha;heat.color=c;}
        private void OnDestroy(){if(heat)Destroy(heat);}
    }
    public sealed class FishOutFaceCamera : MonoBehaviour
    {
        private void LateUpdate(){var cam=Camera.main;if(cam)transform.rotation=cam.transform.rotation;}
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
