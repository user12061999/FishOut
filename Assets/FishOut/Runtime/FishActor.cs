using UnityEngine;

namespace FishOut
{
    public sealed class FishActor : MonoBehaviour
    {
        public int Id { get; private set; }
        public Transform Visual { get; private set; }
        public WaypointMover Mover { get; private set; }
        private LineRenderer halo;
        private Vector3 resting;
        public void Configure(int id, Transform visual, Material glow)
        {
            Id=id; Visual=visual; resting=visual.localPosition;
            Mover=gameObject.AddComponent<WaypointMover>();
            var ring=new GameObject("Selection glow"); ring.transform.SetParent(transform,false);
            halo=ring.AddComponent<LineRenderer>(); halo.useWorldSpace=false;
            halo.loop=true; halo.positionCount=48; halo.widthMultiplier=.045f;
            halo.sharedMaterial=glow; halo.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for(int i=0;i<48;i++) { float a=i*Mathf.PI*2/48; halo.SetPosition(i,new Vector3(Mathf.Cos(a)*.48f,.08f,Mathf.Sin(a)*.48f)); }
            halo.enabled=false;
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
            if(Visual) Visual.localPosition=resting+Vector3.up*(Mathf.Sin(Time.time*2.8f+Id)*.025f);
            if(halo&&halo.enabled) halo.widthMultiplier=.04f+Mathf.Sin(Time.unscaledTime*6f)*.012f;
        }
    }
}
