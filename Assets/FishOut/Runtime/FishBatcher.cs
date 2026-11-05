using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FishOut
{
    // Faces and floor shadows repeat on every fish, so each kind is drawn once with GPU instancing.
    // Bodies stay skinned renderers: they animate per fish and rely on the SRP Batcher instead.
    public sealed class FishBatcher : MonoBehaviour
    {
        private const float ShadowHeight=.015f,ShadowSize=1.05f;
        private sealed class Group
        {
            public Mesh mesh;
            public RenderParams settings;
            public readonly List<Transform> parts=new List<Transform>();
            public Matrix4x4[] matrices=new Matrix4x4[0];
            public bool shadow;
        }
        private readonly Dictionary<(Mesh,Material),Group> groups=new Dictionary<(Mesh,Material),Group>();
        public void AddPart(Mesh mesh,Material material,Transform part)=>Find(mesh,material,false).parts.Add(part);
        public void AddShadow(Mesh mesh,Material material,Transform owner)=>Find(mesh,material,true).parts.Add(owner);
        private Group Find(Mesh mesh,Material material,bool shadow)
        {
            if(groups.TryGetValue((mesh,material),out var group))return group;
            var settings=new RenderParams(material){shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false,worldBounds=new Bounds(Vector3.zero,Vector3.one*400)};
            return groups[(mesh,material)]=new Group{mesh=mesh,settings=settings,shadow=shadow};
        }
        private void LateUpdate()
        {
            foreach(var group in groups.Values)
            {
                if(group.matrices.Length<group.parts.Count)group.matrices=new Matrix4x4[group.parts.Count];
                int count=0;
                for(int i=group.parts.Count-1;i>=0;i--)
                {
                    var part=group.parts[i];
                    if(!part){group.parts.RemoveAt(i);continue;}
                    group.matrices[count++]=group.shadow?ShadowMatrix(part):part.localToWorldMatrix;
                }
                if(count>0)Graphics.RenderMeshInstanced(group.settings,group.mesh,0,group.matrices,count);
            }
        }
        private static Matrix4x4 ShadowMatrix(Transform owner)
        {
            var position=owner.position;position.y=ShadowHeight;
            return Matrix4x4.TRS(position,Quaternion.Euler(90,0,0),Vector3.one*ShadowSize*owner.lossyScale.x);
        }
    }
}
