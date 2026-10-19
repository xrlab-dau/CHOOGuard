using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Circulation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Owned current-geometry navigation and independently witnessed legacy-source point migration.</summary>
    internal sealed class CurrentStationNavigation : IDisposable
    {
        internal const string StationPath="Assets/ChooGuard/Scenes/FpsStation.unity";
        readonly Scene scene;
        readonly List<GameObject> supports=new List<GameObject>();
        readonly Transform trainSource,namedBeltsSource;
        readonly HashSet<Collider> newFloorSources=new HashSet<Collider>();
        readonly Dictionary<string,List<Transform>> paths=new Dictionary<string,List<Transform>>(StringComparer.Ordinal);
        readonly List<string> physicalReport=new List<string>();
        NavMeshDataInstance instance;
        public NavMeshData Data {get;private set;}
        public IReadOnlyList<string> PhysicalReport=>physicalReport;
        public Bounds Bounds {get;private set;}

        static string Hash(byte[] bytes)
        { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        static string PathOf(Transform t)=>NativeLandingWalker.Path(t);

        sealed class SceneLease
        {
            public int Handle;
            public string Key;
            public CurrentStationNavigation Navigation;
            public CurrentStationNavigationLifetime Lifetime;
        }
        static readonly Dictionary<int,SceneLease> sameScene=new Dictionary<int,SceneLease>();
        SceneLease cachedLease;
        int leases=1;
        bool sourceSceneRetired;
        internal static CurrentStationNavigation Acquire(StationPoints catalog,IReadOnlyList<EscalatorDriveBinding> bindings)
        {
            var scene=SceneManager.GetSceneByPath(StationPath);
            string key=SourceKey(scene,catalog,bindings);
            if(key!=null&&sameScene.TryGetValue(scene.handle,out var prior))
            {
                if(prior.Navigation.leases!=0)throw new InvalidOperationException("A current station navigation lease is already active");
                if(prior.Key==key&&prior.Lifetime!=null&&prior.Navigation.Data!=null)
                {
                    prior.Navigation.instance=NavMesh.AddNavMeshData(prior.Navigation.Data);
                    if(!prior.Navigation.instance.valid)throw new InvalidOperationException("Current source navigation re-registration failed");
                    prior.Navigation.leases=1;return prior.Navigation;
                }
                prior.Navigation.Dispose();
                if(prior.Lifetime!=null)UnityEngine.Object.Destroy(prior.Lifetime.gameObject);
                sameScene.Remove(scene.handle);
            }
            var navigation=new CurrentStationNavigation(catalog,bindings);
            if(key!=null)
            {
                var owner=new GameObject("Current station navigation lifetime");
                SceneManager.MoveGameObjectToScene(owner,scene);
                var lifetime=owner.AddComponent<CurrentStationNavigationLifetime>();
                var lease=new SceneLease{Handle=scene.handle,Key=key,Navigation=navigation,Lifetime=lifetime};
                navigation.cachedLease=lease;lifetime.Navigation=navigation;sameScene[scene.handle]=lease;
            }
            return navigation;
        }
        internal void Release()
        {
            if(leases<=0)return;
            if(--leases!=0)return;
            if(instance.valid)instance.Remove();
            if(cachedLease==null||sourceSceneRetired)Dispose();
            // Otherwise the source-scene lifetime owns the immutable data until unload.
        }

        internal void RetireSourceScene()
        {
            sourceSceneRetired=true;
            if(cachedLease!=null&&sameScene.TryGetValue(cachedLease.Handle,out var active)&&ReferenceEquals(active,cachedLease))
                sameScene.Remove(cachedLease.Handle);
            // An active World first removes its shifted BakedFloor copy in Dispose,
            // then releases the last lease. Data destruction therefore cannot precede
            // removal of a copy merely because source-scene OnDestroy ran first.
            if(leases==0)Dispose();
        }

        static string SourceKey(Scene scene,StationPoints catalog,IReadOnlyList<EscalatorDriveBinding> bindings)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Current station source scene absent");
            var train=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Single(t=>NativeLandingWalker.Path(t)==(catalog.Train?.root??"FPSWorld/KTXSource"));
            var named=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(t=>NativeLandingWalker.Path(t)=="FPSWorld/맞이방 · 에스컬레이터");
            var colliders=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>())
                .Where(c=>c.enabled&&!StationNavigationRecipe.Excluded(c,train,named)).OrderBy(c=>c.GetInstanceID()).ToArray();
            // No reuse claim can be made for a mesh whose actual source buffers are
            // unavailable. In that case retain the ordinary owned-build lifetime.
            if(colliders.OfType<MeshCollider>().Any(c=>c.sharedMesh!=null&&!c.sharedMesh.isReadable))return null;
            using(var hash=SHA256.Create())
            using(var crypto=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write))
            using(var writer=new BinaryWriter(crypto,System.Text.Encoding.UTF8,true))
            {
                writer.Write(scene.handle);writer.Write(catalog.SourceSha256);
                writer.Write(JsonUtility.ToJson(StationNavigationRecipe.Settings()));
                var witness=Resources.Load<TextAsset>("CurrentStationPointSources");
                writer.Write(witness==null?"absent":Hash(witness.bytes));
                var seen=new HashSet<Mesh>();
                foreach(var collider in colliders)
                {
                    writer.Write(collider.GetInstanceID());writer.Write(collider.GetType().FullName);
                    var matrix=collider.transform.localToWorldMatrix;for(int i=0;i<16;i++)writer.Write(matrix[i]);
                    if(collider is MeshCollider meshCollider)
                    {
                        var mesh=meshCollider.sharedMesh;writer.Write(mesh==null?0:mesh.GetInstanceID());writer.Write(meshCollider.convex);
                        if(mesh!=null&&seen.Add(mesh))
                        {
                            var vertices=mesh.vertices;writer.Write(vertices.Length);
                            foreach(var vertex in vertices){writer.Write(vertex.x);writer.Write(vertex.y);writer.Write(vertex.z);}
                            writer.Write(mesh.subMeshCount);
                            for(int sub=0;sub<mesh.subMeshCount;sub++)
                            {writer.Write((int)mesh.GetTopology(sub));var indices=mesh.GetIndices(sub);writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
                        }
                    }
                    else if(collider is BoxCollider box)
                    {writer.Write(box.center.x);writer.Write(box.center.y);writer.Write(box.center.z);writer.Write(box.size.x);writer.Write(box.size.y);writer.Write(box.size.z);}
                    else if(collider is SphereCollider sphere)
                    {writer.Write(sphere.center.x);writer.Write(sphere.center.y);writer.Write(sphere.center.z);writer.Write(sphere.radius);}
                    else if(collider is CapsuleCollider capsule)
                    {writer.Write(capsule.center.x);writer.Write(capsule.center.y);writer.Write(capsule.center.z);writer.Write(capsule.radius);writer.Write(capsule.height);writer.Write(capsule.direction);}
                    else return null; // Terrain/other mutable geometry needs its own complete content identity.
                }
                foreach(var binding in bindings.Where(b=>b.Bound).OrderBy(b=>b.lane_id,StringComparer.Ordinal))
                {
                    writer.Write(binding.lane_id);var runs=binding.GetComponentsInChildren<PlatformEscalatorRun>(true);writer.Write(runs.Length);
                    foreach(var run in runs)
                    {
                        if(run.Treads==null||run.Treads.Length!=run.ObservedOrigins.Length||run.Treads.Any(t=>t==null||
                            !t.TryGetComponent<MeshFilter>(out var f)||f.sharedMesh==null))
                            throw new InvalidOperationException("Current bound source tread geometry became incomplete");
                        writer.Write(run.SourceAvailable);writer.Write(run.IsBound);writer.Write(run.Treads.Length);
                        foreach(var tread in run.Treads)writer.Write(tread.GetInstanceID());
                        var matrix=run.transform.localToWorldMatrix;
                        for(int i=0;i<16;i++)writer.Write(matrix[i]);
                        writer.Write(run.Up);writer.Write(run.SourcePitch);writer.Write(run.ExposureMinimum);writer.Write(run.ExposureMaximum);
                        foreach(var p in run.ObservedOrigins){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                    }
                }
                writer.Flush();crypto.FlushFinalBlock();return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
            }
        }

        internal CurrentStationNavigation(StationPoints catalog,IReadOnlyList<EscalatorDriveBinding> bindings)
        {
            scene=SceneManager.GetSceneByPath(StationPath);
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Current saved station source scene is absent");
            try
            {
                foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var path=PathOf(t);
                    if(!paths.TryGetValue(path,out var matches))paths[path]=matches=new List<Transform>();
                    matches.Add(t);
                }
                var train=RequirePath(catalog.Train?.root??"FPSWorld/KTXSource");trainSource=train;
                Transform namedBelts=null;
                if(paths.ContainsKey("FPSWorld/맞이방 · 에스컬레이터"))namedBelts=RequirePath("FPSWorld/맞이방 · 에스컬레이터");
                namedBeltsSource=namedBelts;
                var originals=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>())
                    .Where(c=>c.enabled&&!c.isTrigger&&!(c is CharacterController)&&c.GetComponentInParent<FirstPersonResponder>()==null).ToArray();
                if(originals.Length==0)throw new InvalidOperationException("No active saved solid current source supports");
                var bounds=originals[0].bounds;foreach(var collider in originals)bounds.Encapsulate(collider.bounds);
                bounds.Expand(2f);Bounds=bounds;
                // Eligibility follows the canonical PhysicsColliders recipe. Rendering a
                // decorative asset does not grant it a physical floor/obstacle role. Current
                // required floors are independently checked below and all have saved solids.
                bool hasRendered=false;var allRendered=new Bounds();int unreadableVisualOnly=0;
                foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if(!renderer.enabled)continue;
                    if(!hasRendered){allRendered=renderer.bounds;hasRendered=true;}else allRendered.Encapsulate(renderer.bounds);
                    if(renderer.TryGetComponent<MeshFilter>(out var filter)&&filter.sharedMesh!=null&&!filter.sharedMesh.isReadable&&
                        !filter.GetComponents<Collider>().Any(c=>c.enabled&&!c.isTrigger))unreadableVisualOnly++;
                }
                physicalReport.Add("ALL_SOURCE_RENDER_BOUNDS="+allRendered+"; unreadable visual-only meshes="+unreadableVisualOnly+
                    "; no render-only collider/floor role invented. Build extent uses saved solids only.");
                LoadExplicitCurrentFloorSources();
                Physics.SyncTransforms();
                var lanes=MeasureBoundLanes(catalog,bindings,train);
                var sources=StationNavigationRecipe.Collect(scene,bounds,train,namedBelts,lanes,true);
                if(sources.Count==0)throw new InvalidOperationException("No actual current navigation sources");
                Data=NavMeshBuilder.BuildNavMeshData(StationNavigationRecipe.Settings(),sources,bounds,Vector3.zero,Quaternion.identity);
                if(Data==null)throw new InvalidOperationException("Current physical source navigation build failed");
                instance=NavMesh.AddNavMeshData(Data);
                if(!instance.valid)throw new InvalidOperationException("Current source navigation registration failed");
                physicalReport.Add("MODEL_COMPUTED_WALKABILITY: current source collider bounds="+bounds+"; saved supports="+originals.Length+
                    "; exact source mesh supports="+supports.Count+"; physically bound belt modifiers="+lanes.Count+
                    "; sources="+sources.Count+". No source geometry, floor or connector is invented.");
            }
            catch {Dispose();throw;}
        }

        Transform RequirePath(string path)
        { if(!paths.TryGetValue(path,out var value)||value.Count!=1)throw new InvalidOperationException("Missing/ambiguous required current source path: "+path);return value[0]; }

        void LoadExplicitCurrentFloorSources()
        {
            var adapter=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ChooGuard.Seeded.Unity.StationWorldAdapter>(true)).SingleOrDefault();
            if(adapter==null||adapter.SnapshotManifest==null)throw new InvalidOperationException("Current source floor provenance manifest absent");
            var manifest=JObject.Parse(adapter.SnapshotManifest.text);
            var rows=manifest["currentFloorProvenance"]?["current2FFloorAnchors"] as JArray;
            if(rows==null||rows.Count==0)throw new InvalidOperationException("Explicit current source floor anchors absent");
            foreach(var row in rows)
            {
                var source=RequirePath((string)row["path"]);
                var colliders=source.GetComponents<MeshCollider>().Where(c=>c.enabled&&!c.isTrigger&&c.sharedMesh!=null).ToArray();
                if(!source.gameObject.activeInHierarchy||colliders.Length==0)
                    throw new InvalidOperationException("Required current source floor collision support absent: "+(string)row["globalObjectId"]);
                foreach(var collider in colliders)newFloorSources.Add(collider);
            }
        }

        List<StationNavigationGeometry.Lane> MeasureBoundLanes(StationPoints catalog,IReadOnlyList<EscalatorDriveBinding> bindings,Transform train)
        {
            var lanes=new List<StationNavigationGeometry.Lane>();
            var actual=bindings.Where(b=>b.Bound).Select(binding=> {
                var entry=catalog.Escalators.Single(e=>e.id==binding.lane_id);
                var runs=binding.GetComponentsInChildren<PlatformEscalatorRun>(true);
                if(runs.Length!=1||!runs[0].SourceAvailable||!runs[0].IsBound)
                    throw new InvalidOperationException("Bound lane has no unique complete physical source tread run: "+entry.id);
                var run=runs[0];
                var origins=run.ObservedOrigins.Select(run.transform.TransformPoint).ToArray();
                if(origins.Length<2)throw new InvalidOperationException("Current source tread origins absent: "+entry.id);
                var high=origins.OrderByDescending(p=>p.y).First();var low=origins.OrderBy(p=>p.y).First();
                string well=entry.id.Substring("esc-".Length);
                if(well.EndsWith("-0",StringComparison.Ordinal)||well.EndsWith("-1",StringComparison.Ordinal))well=well.Substring(0,well.Length-2);
                return (entry,high,low,well);
            }).ToArray();
            foreach(var group in actual.GroupBy(x=>x.well))
            {
                var high=group.Aggregate(Vector3.zero,(sum,x)=>sum+x.high)/group.Count();
                var low=group.Aggregate(Vector3.zero,(sum,x)=>sum+x.low)/group.Count();
                var up=high-low;up.y=0;up.Normalize();var side=new Vector3(up.z,0,-up.x);
                var ordered=group.OrderBy(x=>Vector3.Dot((x.high+x.low)*.5f,side)).ToArray();
                var down=ordered.Select(x=>!x.entry.up).ToArray();
                var measured=new StationNavigationGeometry.WellSurvey(train).WellLanes(group.Key,group.Key,"",
                    new Vector2(high.x,high.z),new Vector2(low.x,low.z),down,physicalReport).ToArray();
                if(measured.Length!=group.Count()||!measured.Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal)
                    .SequenceEqual(group.Select(x=>x.entry.id).OrderBy(x=>x,StringComparer.Ordinal)))
                    throw new InvalidOperationException("Current source belt/stair footprints are not measurable: "+group.Key);
                lanes.AddRange(measured);
            }
            return lanes;
        }

        internal static string BindingDecision(bool provenAbsent,bool physicalSupported,bool currentNavSupported)
        {
            if(provenAbsent)
            {
                if(physicalSupported)throw new InvalidOperationException("Current source contradicts absent-floor witness");
                return "UNBOUND_PROVEN_NO_CURRENT_FLOOR";
            }
            if(!physicalSupported)throw new InvalidOperationException("Unknown current physical support cannot be silently discarded");
            if(!currentNavSupported)throw new InvalidOperationException("Current floor is supported but navigation is missing");
            return "BOUND_MODEL_COMPUTED_WALKABLE";
        }

        internal StationPoints BindPoints(StationPoints catalog,NavMeshData originalReference)
        {
            var witness=Resources.Load<TextAsset>("CurrentStationPointSources");
            if(witness==null)throw new InvalidOperationException("Current source point partition witnesses absent");
            var data=JObject.Parse(witness.text);
            if((int?)data["version"]!=1||(string)data["sourceCatalogSha256"]!=catalog.SourceSha256)
                throw new InvalidOperationException("Current source point catalog witness mismatch");
#if UNITY_EDITOR
            var scenePath=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"..",StationPath));
            using(var stream=File.OpenRead(scenePath))using(var hash=SHA256.Create())
                if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=(string)data["sourceSceneSha256"])
                    throw new InvalidOperationException("Current point physical witnesses require the exact saved source Scene");
#endif
            var rows=(JArray)data["bindings"];
            if(rows==null||rows.Count!=catalog.All.Count)throw new InvalidOperationException("Source point witness partition is incomplete");
            var result=new List<StationPoints.CurrentBinding>();
            using(var reference=new BakedFloor(originalReference))
            for(int i=0;i<catalog.All.Count;i++)
            {
                var source=catalog.All[i];var row=rows[i];
                if((int?)row["sourceIndex"]!=i||(string)row["id"]!=source.Id)
                    throw new InvalidOperationException("Current source point witness ordering/identity differs");
                var sourcePosition=row["position"];
                if(sourcePosition==null||Mathf.Abs((float)sourcePosition["x"]-source.Position.x)>1e-6f||
                    Mathf.Abs((float)sourcePosition["y"]-source.Position.y)>1e-6f||Mathf.Abs((float)sourcePosition["z"]-source.Position.z)>1e-6f)
                    throw new InvalidOperationException("Current source witness coordinate differs: "+source.Id);
                string disposition=(string)row["disposition"];
                if(disposition!="PROVEN_NO_CURRENT_FLOOR"&&disposition!="REQUIRES_RUNTIME_INDEPENDENT_SOURCE_AND_NAV_QUALIFICATION")
                    throw new InvalidOperationException("Unknown current source witness disposition: "+source.Id);
                bool absent=disposition=="PROVEN_NO_CURRENT_FLOOR";
                if(absent&&string.IsNullOrWhiteSpace((string)row["evidence"]))throw new InvalidOperationException("Absent-floor source witness is empty");
                var candidates=new List<Vector3>{source.Position};
                if(reference.Sample(source.Position,2f,out var prior)&&Mathf.Abs(prior.y-source.Position.y)<.35f)candidates.Add(prior);
                // Same-storey candidate probes propose actual physical points. Their failure
                // never proves absence: unwitnessed/uncertain points fail closed below.
                for(float radius=.25f;radius<=2.001f;radius+=.25f)
                    for(int step=0;step<16;step++)candidates.Add(source.Position+Quaternion.Euler(0,step*22.5f,0)*Vector3.forward*radius);
                bool physical=false,nav=false;Vector3 feet=source.Position;string evidence="";
                foreach(var candidate in candidates)
                {
                    if(!Footprint(candidate,reference,out var supported,out var proof))continue;
                    physical=true;evidence=proof;
                    if(absent)break;
                    if(!NavMesh.SamplePosition(supported,out var hit,.6f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-supported.y)>.35f)continue;
                    if(!Footprint(hit.position,reference,out var corroborated,out var navProof)||Vector3.Distance(corroborated,hit.position)>.35f)continue;
                    feet=hit.position;evidence+="; nav corroboration="+navProof;nav=true;break;
                }
                string status=BindingDecision(absent,physical,nav);
                var runtime=nav?new StationPoints.Point{Id=source.Id,Label=source.Label,Zone=source.Zone,Kind=source.Kind,
                    Position=feet,Anchor=source.Anchor,Yaw=source.Yaw,Slot=source.Slot}:null;
                result.Add(new StationPoints.CurrentBinding{Source=source,Runtime=runtime,Status=status,
                    Evidence=absent?(string)row["evidence"]:evidence+"; projection="+(feet-source.Position).ToString("F5")});
            }
            return catalog.WithCurrentBindings(result.AsReadOnly());
        }

        bool Footprint(Vector3 staged,BakedFloor reference,out Vector3 floor,out string evidence)
        {
            floor=staged;evidence="";float low=float.PositiveInfinity,high=float.NegativeInfinity;
            bool allNew=true;var owners=new HashSet<string>();
            for(int i=-1;i<8;i++)
            {
                var offset=i<0?Vector3.zero:new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4))*.28f;
                var hits=Physics.RaycastAll(staged+offset+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(h.collider,trainSource,namedBeltsSource)&&
                        h.collider.GetComponentInParent<FirstPersonResponder>()==null&&h.collider.GetComponentInParent<PersonBody>()==null)
                    .OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0)return false;var hit=hits[0];
                if(hit.normal.y<Mathf.Cos(36*Mathf.Deg2Rad)||Mathf.Abs(hit.point.y-staged.y)>.35f)return false;
                low=Mathf.Min(low,hit.point.y);high=Mathf.Max(high,hit.point.y);
                allNew&=newFloorSources.Contains(hit.collider);owners.Add(PathOf(hit.collider.transform));
            }
            if(high-low>.35f)return false;
            floor.y=high;
            // The old cache is only an independent partial classification reference;
            // actual current footprint+clearance are mandatory. Explicit current floors
            // independently extend that reference where the old cache is missing.
            bool corroborated=reference.Sample(floor,.6f,out var old)&&Mathf.Abs(old.y-floor.y)<.35f;
            var bottom=floor+Vector3.up*(.28f+.03f);var top=floor+Vector3.up*(1.8f-.28f);
            foreach(var collider in Physics.OverlapCapsule(bottom,top,.28f,~0,QueryTriggerInteraction.Ignore))
                if(collider.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(collider,trainSource,namedBeltsSource)&&collider.GetComponentInParent<FirstPersonResponder>()==null&&
                    collider.GetComponentInParent<PersonBody>()==null)return false;
            evidence="MODEL_COMPUTED_WALKABLE; current9-ray .28m footprint; 1.8m clearance; source="+string.Join("|",owners.OrderBy(x=>x,StringComparer.Ordinal))+
                "; independentReference="+(allNew?"explicit current floor provenance":corroborated?"old area/walkability corroborated by current geometry":"independent actual source geometry; old cache has no corroboration");
            return true;
        }

        public void Dispose()
        {
            leases=0;
            if(cachedLease!=null&&sameScene.TryGetValue(cachedLease.Handle,out var active)&&ReferenceEquals(active,cachedLease))
                sameScene.Remove(cachedLease.Handle);
            cachedLease=null;
            if(instance.valid)instance.Remove();
            if(Data!=null){UnityEngine.Object.Destroy(Data);Data=null;}
            foreach(var support in supports)if(support!=null)UnityEngine.Object.Destroy(support);
            supports.Clear();
        }
    }
    internal sealed class CurrentStationNavigationLifetime : MonoBehaviour
    {
        internal CurrentStationNavigation Navigation;
        void OnDestroy(){Navigation?.RetireSourceScene();Navigation=null;}
    }
}
