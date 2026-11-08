using System.Collections;
using UnityEngine;

namespace FishOut
{
    public sealed class FishActor : MonoBehaviour
    {
        public int Id { get; private set; }
        public Transform Visual { get; private set; }
        public WaypointMover Mover { get; private set; }
        private LineRenderer halo;
        private Vector3 resting,restingScale;
        private Quaternion restingRotation;
        // Idle "nudge": every couple of seconds the fish twitches towards the way it faces, so its heading reads
        // even from behind. Each fish has its own rhythm so the school never pulses in step.
        private const float NudgeAmount=.11f,NudgeLength=.34f,NudgeTilt=9f;
        // Heading cues (idle nudge + teardrop shadow) are switched off for now at the designer's request (07/11).
        public static bool HeadingCues=false;
        private float nudgePeriod,nudgePhase;
        // Dizzy: stars circling over the head and a wobble after bumping into something.
        private const float DizzyStars=3,DizzyRadius=.32f,DizzySpin=7f,DizzyWobble=12f,DizzySize=.3f;
        private Coroutine dizzy;
        private float wobble;
        private float popLeft,popLength,popSize;
        private const float PaddleBeat=9f,PaddleYaw=11f,PaddleDrift=.035f,PaddleBob=.03f;
        private float paddleWeight;
        public void Configure(int id, Transform visual, Material glow)
        {
            Id=id; Visual=visual; resting=visual.localPosition; restingScale=visual.localScale; restingRotation=visual.localRotation;
            nudgePeriod=2.1f+Mathf.Repeat(id*.37f,1.2f); nudgePhase=Mathf.Repeat(id*.618f,1f)*nudgePeriod;
            Mover=gameObject.AddComponent<WaypointMover>();
            var ring=new GameObject("Selection glow"); ring.transform.SetParent(transform,false);
            halo=ring.AddComponent<LineRenderer>(); halo.useWorldSpace=false;
            halo.loop=true; halo.positionCount=48; halo.widthMultiplier=.09f;
            halo.sharedMaterial=glow; halo.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for(int i=0;i<48;i++) { float a=i*Mathf.PI*2/48; halo.SetPosition(i,new Vector3(Mathf.Cos(a)*.48f,.08f,Mathf.Sin(a)*.48f)); }
            halo.enabled=false;
        }
        public void Pop(float size,float seconds){popSize=size;popLength=popLeft=seconds;}
        // Hit from behind or the side by another fish: knocked a little along the hit and shaking it off.
        private const float JoltLength=.38f,JoltPush=.1f,JoltShake=13f,JoltBeat=42f;
        private Vector3 joltDirection;private float joltLeft;
        public void Jolt(Vector3 worldDirection)
        {
            joltDirection=worldDirection.sqrMagnitude>.0001f?worldDirection.normalized:Vector3.zero;
            joltLeft=JoltLength;Pop(.1f,.2f);
        }
        public void Dizzy(float seconds=.9f)
        {
            if(!isActiveAndEnabled||!Visual)return;
            if(dizzy!=null)StopCoroutine(dizzy);
            foreach(Transform child in transform)if(child.name=="Dizzy stars")Destroy(child.gameObject);
            dizzy=StartCoroutine(DizzyRoutine(seconds));
        }
        private IEnumerator DizzyRoutine(float seconds)
        {
            var holder=new GameObject("Dizzy stars").transform;holder.SetParent(transform,false);
            float scale=Mathf.Max(.01f,transform.lossyScale.y);
            holder.localPosition=Vector3.up*((VisibleBounds().max.y-transform.position.y+.08f)/scale);
            var sprite=FishOutIcons.Star;var stars=new SpriteRenderer[(int)DizzyStars];
            for(int i=0;i<stars.Length;i++)
            {
                stars[i]=new GameObject("Star").AddComponent<SpriteRenderer>();
                stars[i].sprite=sprite;stars[i].color=new Color(1f,.82f,.12f);stars[i].sortingOrder=10;
                stars[i].transform.SetParent(holder,false);
                // A darker, slightly larger star behind gives the cartoon outline so it reads on the pale sand.
                var rim=new GameObject("Outline").AddComponent<SpriteRenderer>();
                rim.sprite=sprite;rim.color=new Color(.55f,.28f,.04f);rim.sortingOrder=9;
                rim.transform.SetParent(stars[i].transform,false);rim.transform.localScale=Vector3.one*1.35f;rim.transform.localPosition=new Vector3(0,0,.001f);
            }
            float size=DizzySize/Mathf.Max(.01f,sprite.bounds.size.x);
            var view=Camera.main?Camera.main.transform:null;
            for(float t=0;t<seconds;t+=Time.deltaTime)
            {
                float k=t/seconds,pop=Mathf.Clamp01(t/.15f),fade=1-Mathf.Clamp01((k-.7f)/.3f);
                wobble=Mathf.Sin(t*24f)*DizzyWobble*(1-k);
                for(int i=0;i<stars.Length;i++)
                {
                    float a=t*DizzySpin+i*Mathf.PI*2/stars.Length;
                    var star=stars[i].transform;
                    star.localPosition=new Vector3(Mathf.Cos(a)*DizzyRadius,Mathf.Sin(a*2)*.03f,Mathf.Sin(a)*DizzyRadius)/scale;
                    star.localScale=Vector3.one*size*pop*(.85f+.15f*Mathf.Sin(a*3))/scale;
                    if(view)star.rotation=view.rotation;
                    var c=stars[i].color;c.a=fade;stars[i].color=c;
                    var rimRenderer=star.GetChild(0).GetComponent<SpriteRenderer>();var rc=rimRenderer.color;rc.a=fade;rimRenderer.color=rc;
                }
                yield return null;
            }
            wobble=0;Destroy(holder.gameObject);dizzy=null;
        }
        public void Highlight(bool value) { if(halo) halo.enabled=value; }
        public void SetSwimming(bool value)
        {
            foreach(var animator in GetComponentsInChildren<Animator>())if(animator.runtimeAnimatorController)animator.SetBool("Swimming",value);
        }
        public Bounds VisibleBounds()
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var renderer in Visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            // Allow for the subtle idle bob while swimming.
            bounds.Expand(.12f);
            return bounds;
        }
        private void Update()
        {
            if(Visual)
            {
                float lunge=0;
                if(HeadingCues&&Mover&&!Mover.IsMoving)
                {
                    float t=Mathf.Repeat(Time.time+nudgePhase,nudgePeriod);
                    if(t<NudgeLength){float k=t/NudgeLength;lunge=Mathf.Sin(k*Mathf.PI)*(1-.35f*k);}
                }
                // Waiting in the queue the fish keeps paddling on the spot: tail sway, a little side to side and a livelier
                // bob, fading in as it slows below half its cruising speed, so a slow school still reads as swimming.
                float paddle=0;
                if(Mover&&Mover.IsMoving&&Mover.maximumSpeed>.1f)paddle=1-Mathf.Clamp01(Mover.Speed/(Mover.maximumSpeed*.5f));
                paddleWeight=Mathf.MoveTowards(paddleWeight,paddle,Time.deltaTime*4f);
                float beat=Time.time*PaddleBeat+Id*1.7f;
                float sway=Mathf.Sin(beat)*PaddleYaw*paddleWeight,drift=Mathf.Sin(beat*.5f)*PaddleDrift*paddleWeight,bob=Mathf.Abs(Mathf.Sin(beat))*PaddleBob*paddleWeight;
                var shift=PaintedBackdrop.Current!=null?transform.InverseTransformVector(PaintedBackdrop.PieceShift(transform.position)):Vector3.zero;
                Vector3 knock=Vector3.zero;float shake=0;
                if(joltLeft>0)
                {
                    joltLeft-=Time.deltaTime;float k=1-Mathf.Clamp01(joltLeft/JoltLength);
                    knock=transform.InverseTransformDirection(joltDirection)*Mathf.Sin(Mathf.Min(1,k*2.5f)*Mathf.PI)*JoltPush/Mathf.Max(.01f,transform.lossyScale.x);
                    shake=Mathf.Sin(k*JoltBeat)*JoltShake*(1-k);
                }
                Visual.localPosition=resting+shift+Vector3.up*(Mathf.Sin(Time.time*2.8f+Id)*.025f+bob)+Vector3.forward*(lunge*NudgeAmount)+Vector3.right*drift+knock;
                Visual.localRotation=Quaternion.Euler(lunge*NudgeTilt,sway+shake,wobble)*restingRotation;
            }
            // Smaller further up the painted board, growing as it swims towards the camera.
            if(PaintedBackdrop.Current!=null) transform.localScale=Vector3.one*PaintedBackdrop.ScaleAt(transform.position);
            if(halo&&halo.enabled) halo.widthMultiplier=.09f+Mathf.Sin(Time.unscaledTime*6f)*.02f;
            if(popLeft>0&&Visual)
            {
                popLeft-=Time.unscaledDeltaTime;
                Visual.localScale=restingScale*(1+popSize*Mathf.Sin(Mathf.Clamp01(1-popLeft/popLength)*Mathf.PI));
            }
        }
    }
}
