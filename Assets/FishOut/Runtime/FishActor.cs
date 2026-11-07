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
        private float nudgePeriod,nudgePhase;
        private float popLeft,popLength,popSize;
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
                if(Mover&&!Mover.IsMoving)
                {
                    float t=Mathf.Repeat(Time.time+nudgePhase,nudgePeriod);
                    if(t<NudgeLength){float k=t/NudgeLength;lunge=Mathf.Sin(k*Mathf.PI)*(1-.35f*k);}
                }
                Visual.localPosition=resting+Vector3.up*(Mathf.Sin(Time.time*2.8f+Id)*.025f)+Vector3.forward*(lunge*NudgeAmount);
                Visual.localRotation=Quaternion.Euler(lunge*NudgeTilt,0,0)*restingRotation;
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
