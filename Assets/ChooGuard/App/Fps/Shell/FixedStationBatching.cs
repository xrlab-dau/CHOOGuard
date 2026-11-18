using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ChooGuard.App.Fps.Shell
{
    // Presentation-only batching for an isolated, fixed source scene. Never use
    // this on live doors/actors: their transforms must stay independent.
    public sealed class FixedStationBatching : MonoBehaviour
    {
        readonly List<MeshRenderer> originals=new List<MeshRenderer>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly Dictionary<string,List<MeshRenderer>> groups=new Dictionary<string,List<MeshRenderer>>();
        readonly Dictionary<Transform,bool> liveHierarchy=new Dictionary<Transform,bool>();
        readonly Dictionary<Type,bool> liveTypes=new Dictionary<Type,bool>();
        bool HasLiveController(Transform node)
        {
            if(node==null)return false;
            if(liveHierarchy.TryGetValue(node,out var found))return found;
            bool live=HasLiveController(node.parent);
            if(!live)foreach(var behaviour in node.GetComponents<MonoBehaviour>())
            {
                if(behaviour==null||!behaviour.enabled)continue;
                var type=behaviour.GetType();
                if(!liveTypes.TryGetValue(type,out var moving))
                {
                    const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly;
                    for(var t=type;t!=null&&t!=typeof(MonoBehaviour);t=t.BaseType)
                    {
                        moving=t.GetMethods(flags).Any(m=>m.Name=="Update"||m.Name=="LateUpdate"||m.Name=="FixedUpdate"||m.Name=="OnAnimatorMove"||
                            (m.Name=="Start"&&typeof(System.Collections.IEnumerator).IsAssignableFrom(m.ReturnType)));
                        if(moving)break;
                    }
                    liveTypes.Add(type,moving);
                }
                if(moving){live=true;break;}
            }
            liveHierarchy.Add(node,live);return live;
        }
        public void Prepare(Scene scene,Transform equipment,Transform chair,Vector3 origin)
        {
            bool probes=LightmapSettings.lightProbes!=null&&LightmapSettings.lightProbes.count>0;
            bool reflections=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ReflectionProbe>()).Any(p=>p.enabled);
            var layouts=new Dictionary<Mesh,string>();
            int vertices=0,eligible=0;
            var candidates=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>())
                .OrderBy(r=>(r.bounds.center-origin).sqrMagnitude);
            foreach(var r in candidates)
            {
                if(!r.enabled||r.forceRenderingOff||r.isPartOfStaticBatch||r.HasPropertyBlock()||r.additionalVertexStreams!=null||
                   HasLiveController(r.transform)||
                   r.GetComponentInParent<Animator>()!=null||r.GetComponentInParent<Rigidbody>()!=null||r.GetComponentInParent<UnityEngine.AI.NavMeshAgent>()!=null||
                   r.transform.IsChildOf(equipment)||r.transform.IsChildOf(chair)||r.lightmapIndex>=0||r.realtimeLightmapIndex>=0||
                   (probes&&r.lightProbeUsage!=LightProbeUsage.Off)||(reflections&&r.reflectionProbeUsage!=ReflectionProbeUsage.Off)||
                   r.localToWorldMatrix.determinant<=0)continue;
                var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;var materials=r.sharedMaterials;
                if(mesh==null||!mesh.isReadable||mesh.vertexCount==0||mesh.vertexCount>10000||mesh.subMeshCount!=1||
                   mesh.GetTopology(0)!=MeshTopology.Triangles||materials.Length!=1||materials[0]==null)continue;
                var material=materials[0];
                if(material.renderQueue>=2500||material.GetTag("DisableBatching",false,"False").Equals("True",StringComparison.OrdinalIgnoreCase))continue;
                eligible++;if(vertices+mesh.vertexCount>6000000)continue;
                if(!layouts.TryGetValue(mesh,out var layout))
                {layout=String.Join(";",mesh.GetVertexAttributes().Select(a=>a.attribute+":"+a.format+":"+a.dimension+":"+a.stream));layouts.Add(mesh,layout);}
                var at=r.bounds.center;
                string key=Mathf.FloorToInt(at.x/8)+","+Mathf.FloorToInt(at.y/4)+","+Mathf.FloorToInt(at.z/8)+"|"+
                    material.GetInstanceID()+"|"+layout+"|"+r.shadowCastingMode+"|"+r.receiveShadows+"|"+r.renderingLayerMask+"|"+
                    r.gameObject.layer+"|"+r.lightProbeUsage+"|"+r.reflectionProbeUsage+"|"+(r.probeAnchor!=null?r.probeAnchor.GetInstanceID():0)+"|"+r.sortingLayerID+"|"+r.sortingOrder+"|"+
                    r.motionVectorGenerationMode+"|"+r.allowOcclusionWhenDynamic+"|"+(r.lightProbeProxyVolumeOverride!=null?r.lightProbeProxyVolumeOverride.GetInstanceID():0);
                if(!groups.TryGetValue(key,out var group)){group=new List<MeshRenderer>();groups.Add(key,group);}
                group.Add(r);vertices+=mesh.vertexCount;
            }
            long before=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();int batches=0;
            foreach(var group in groups.Values)
            {
                var chunk=new List<MeshRenderer>();int count=0;
                foreach(var r in group)
                {
                    int size=r.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                    if(count+size>60000){if(Combine(chunk))batches++;chunk.Clear();count=0;}
                    chunk.Add(r);count+=size;
                }
                if(Combine(chunk))batches++;
            }
            Debug.Log("CG_MERGE eligible="+eligible+" originals="+originals.Count+" batches="+batches+" budgetVertices="+vertices+" memoryDelta="+(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()-before));
            groups.Clear();liveHierarchy.Clear();liveTypes.Clear();
        }
        bool Combine(List<MeshRenderer> source)
        {
            if(source.Count<2)return false;
            var first=source[0];
            var go=new GameObject("고정 역사 렌더 묶음",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(transform,false);go.transform.position=first.bounds.center;
            var worldToLocal=go.transform.worldToLocalMatrix;
            var instances=source.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,subMeshIndex=0,transform=worldToLocal*r.localToWorldMatrix}).ToArray();
            var mesh=new Mesh{name="고정 역사 렌더 묶음",hideFlags=HideFlags.DontSave};
            try{mesh.CombineMeshes(instances,true,true,false);}
            catch{Destroy(mesh);Destroy(go);throw;}
            mesh.UploadMeshData(true);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;meshes.Add(mesh);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=first.sharedMaterial;
            renderer.shadowCastingMode=first.shadowCastingMode;renderer.receiveShadows=first.receiveShadows;
            renderer.renderingLayerMask=first.renderingLayerMask;renderer.lightProbeUsage=first.lightProbeUsage;
            renderer.reflectionProbeUsage=first.reflectionProbeUsage;renderer.probeAnchor=first.probeAnchor;
            renderer.motionVectorGenerationMode=first.motionVectorGenerationMode;renderer.allowOcclusionWhenDynamic=first.allowOcclusionWhenDynamic;
            renderer.lightProbeProxyVolumeOverride=first.lightProbeProxyVolumeOverride;
            renderer.sortingLayerID=first.sortingLayerID;renderer.sortingOrder=first.sortingOrder;go.layer=first.gameObject.layer;
            // Disabling only the superseded renderer also avoids its per-frame
            // renderer-management work. The object and colliders stay active.
            foreach(var r in source){r.enabled=false;originals.Add(r);}
            return true;
        }
        void OnDestroy()
        {
            foreach(var r in originals)if(r!=null)r.enabled=true;
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
