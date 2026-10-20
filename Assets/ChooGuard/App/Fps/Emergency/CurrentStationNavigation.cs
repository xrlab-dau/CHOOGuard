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
        List<NavMeshBuildSource> collectedSources;
        NavMeshBuildSettings collectedSettings;
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
                        for(int part=0;part<run.Treads.Length;part++)
                        {
                            var tread=run.Treads[part];var rotation=tread.localRotation;var scale=tread.localScale;
                            writer.Write(rotation.x);writer.Write(rotation.y);writer.Write(rotation.z);writer.Write(rotation.w);
                            writer.Write(scale.x);writer.Write(scale.y);writer.Write(scale.z);
                            var mesh=run.GetOriginalSourceMesh(part);
                            if(!mesh.isReadable)return null;
                            writer.Write(mesh.GetInstanceID());
                            if(seen.Add(mesh))
                            {
                                var vertices=mesh.vertices;writer.Write(vertices.Length);
                                foreach(var vertex in vertices){writer.Write(vertex.x);writer.Write(vertex.y);writer.Write(vertex.z);}
                                writer.Write(mesh.subMeshCount);
                                for(int sub=0;sub<mesh.subMeshCount;sub++)
                                {writer.Write((int)mesh.GetTopology(sub));var indices=mesh.GetIndices(sub);writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
                            }
                        }
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
                collectedSources=sources;collectedSettings=StationNavigationRecipe.Settings();
                Data=NavMeshBuilder.BuildNavMeshData(collectedSettings,sources,bounds,Vector3.zero,Quaternion.identity);
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
            var measured=new List<(string well,StationNavigationGeometry.Lane lane,StationNavigationGeometry.SourceTreadFootprint footprint)>();
            foreach(var binding in bindings.Where(b=>b.Bound))
            {
                var entry=catalog.Escalators.Single(e=>e.id==binding.lane_id);
                var runs=binding.GetComponentsInChildren<PlatformEscalatorRun>(true);
                if(runs.Length!=1||!runs[0].SourceAvailable||!runs[0].IsBound)
                    throw new InvalidOperationException("Bound lane has no unique complete physical source tread run: "+entry.id);
                var run=runs[0];
                var origins=run.ObservedOrigins.Select(run.transform.TransformPoint).ToArray();
                if(origins.Length<2||origins.Length!=run.Treads.Length||run.Up!=entry.up)
                    throw new InvalidOperationException("Current source tread origins/direction are inconsistent: "+entry.id);
                var indexed=new List<float>();
                for(int part=0;part<run.Treads.Length;part++)
                {
                    var tread=run.Treads[part];
                    if(tread==null||tread.parent!=run.transform)
                        throw new InvalidOperationException("Observed source tread parent changed: "+entry.id);
                    var mesh=run.GetOriginalSourceMesh(part);
                    if(!mesh.isReadable)throw new InvalidOperationException("Current native tread source buffer is unreadable: "+entry.id);
                    // Motion changes only local position/visible clipped buffers. Measure
                    // the retained original buffer at its captured observed origin without
                    // moving the source or borrowing an animated boundary derivative.
                    var matrix=run.transform.localToWorldMatrix*Matrix4x4.TRS(run.ObservedOrigins[part],tread.localRotation,tread.localScale);
                    var vertices=mesh.vertices;int before=indexed.Count;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        var indices=mesh.GetIndices(sub);
                        if(indices.Length==0)continue;
                        if(mesh.GetTopology(sub)!=MeshTopology.Triangles||indices.Length%3!=0)
                            throw new InvalidOperationException("Native tread source is not indexed triangles: "+entry.id);
                        foreach(int index in indices)
                        {
                            if(index<0||index>=vertices.Length)throw new InvalidOperationException("Native tread source index out of range: "+entry.id);
                            var point=matrix.MultiplyPoint3x4(vertices[index]);indexed.Add(point.x);indexed.Add(point.y);indexed.Add(point.z);
                        }
                    }
                    if(indexed.Count==before)throw new InvalidOperationException("Native tread source has no indexed surface: "+entry.id);
                }
                var high=origins.OrderByDescending(p=>p.y).First();var low=origins.OrderBy(p=>p.y).First();
                var footprint=new StationNavigationGeometry.SourceTreadFootprint(indexed.ToArray(),high.x,high.z,low.x,low.z);
                var lane=StationNavigationGeometry.SourceTreadLane(entry.id,entry.label,entry.up,entry.from,entry.to,footprint);
                string well=entry.id.Substring("esc-".Length);
                if(well.EndsWith("-0",StringComparison.Ordinal)||well.EndsWith("-1",StringComparison.Ordinal))well=well.Substring(0,well.Length-2);
                measured.Add((well,lane,footprint));
                physicalReport.Add("BOUND_SOURCE_TREAD_MODIFIER "+entry.id+"; original meshes="+run.Treads.Length+
                    "; indexed vertices="+indexed.Count/3+"; source width="+lane.Width+"; bounds="+lane.BoxCentre+" / "+lane.BoxSize);
            }
            // The legacy Editor survey still rediscovers whole historical wells. Runtime
            // already has exact physical source bindings and must not rediscover missing
            // machinery or demand that unrelated cross-section lanes equal that set.
            var survey=new StationNavigationGeometry.WellSurvey(train);
            foreach(var group in measured.GroupBy(row=>row.well))if(group.Count()==1)
            {
                var row=group.Single();
                survey.BoundLaneStairs(row.lane,row.footprint,
                    collider=>collider.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(collider,train,namedBeltsSource),physicalReport);
            }
            return measured.Select(row=>row.lane).ToList();
        }

        internal StationWalkingRouteEntry BindCurrentWalkingRoute(StationWalkingRouteEntry source)
        {
            source.Validate();
            var observedOwners=new HashSet<string>(StringComparer.Ordinal);
            bool CurrentOwner(Collider collider)
            {
                if(collider==null||collider.gameObject.scene!=scene||!newFloorSources.Contains(collider))return false;
                observedOwners.Add(PathOf(collider.transform));return true;
            }
            for(int i=1;i<source.path.Length;i++)
            {
                int samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(source.path[i-1],source.path[i])/.25f));
                for(int j=0;j<=samples;j++)
                    if(!NativeLandingWalker.Floor(Vector3.Lerp(source.path[i-1],source.path[i],j/(float)samples),source,null,CurrentOwner,out _))
                        throw new InvalidOperationException("Current physical walking-route support is absent: "+source.id);
            }
            // Preserve the complete historical route and exact endpoint/polyline
            // coordinates. Bind only current required-floor owners actually exposed
            // by the unchanged nine-ray normal/height/spread checks at this path.
            var bound=source.WithCurrentFloorOwners(observedOwners);
            physicalReport.Add("CURRENT_WALKING_ROUTE_SOURCE "+source.id+"; unchanged source points="+source.path.Length+
                "; observed approved current floor owners="+string.Join("|",observedOwners.OrderBy(owner=>owner,StringComparer.Ordinal)));
            return bound;
        }

        internal void RequireCurrentElevatorSupport(StationPoints.ElevatorStop stop,string identity)
        {
            // A catalog door cannot move to a nearby clear patch simply because a
            // navigation radius finds it. Keep the exact source pose and require
            // its own current saved-collider footprint and standing clearance.
            if(!Footprint(stop.door,null,out _,out var evidence))
                throw new InvalidOperationException("Current source elevator landing has no exposed physical support/clearance: "+identity);
            physicalReport.Add("BOUND_CURRENT_ELEVATOR_LANDING "+identity+"; "+evidence);
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

        // Diagnostics only: these reports never qualify a point or provide an absence witness.
        internal sealed class PointProbeReport
        {
            const int DetailLimit=4096;
            readonly SortedDictionary<string,int> counts=new SortedDictionary<string,int>(StringComparer.Ordinal);
            readonly SortedDictionary<string,string> examples=new SortedDictionary<string,string>(StringComparer.Ordinal);
            public int Candidates;
            public readonly HashSet<Collider> FloorSources=new HashSet<Collider>();
            public readonly List<Vector3> ClearFloorPositions=new List<Vector3>();
            public bool NeedsExample(string stage)=>!examples.ContainsKey(stage);
            public bool Has(string stage)=>counts.ContainsKey(stage);
            public void Record(string stage,string detail=null)
            {
                counts.TryGetValue(stage,out var count);counts[stage]=count+1;
                if(detail!=null&&!examples.ContainsKey(stage))
                    examples[stage]=detail.Length<=DetailLimit?detail:detail.Substring(0,DetailLimit)+" [detail truncated; sibling/component identity retained first]";
            }
            public string Describe()=>"candidateCount="+Candidates+"; stageCounts={"+
                string.Join(", ",counts.Select(row=>row.Key+"="+row.Value))+"}; firstStageObservations={"+
                string.Join("; ",examples.Select(row=>row.Key+": "+row.Value))+"}";
        }

        internal sealed class PointBindingFailures
        {
            public const int Limit=8;
            readonly List<Exception> failures=new List<Exception>();
            public bool Full=>failures.Count==Limit;
            public void Add(string identity,PointProbeReport probes,InvalidOperationException cause)
            {
                if(Full)throw new InvalidOperationException("Point diagnostic failure limit already reached");
                failures.Add(new InvalidOperationException(identity+"; "+probes.Describe()+"; cause="+cause.Message,cause));
            }
            public void ThrowIfAny(int attempted,int total)
            {
                if(failures.Count==0)return;
                throw new InvalidOperationException("Current source point qualification failed: "+failures.Count+
                    " failed row(s); attempted="+attempted+"/"+total+"; diagnosticFailureLimit="+Limit+
                    "; scan="+(attempted<total?"bounded prefix; remaining rows NOT_RUN":"complete")+
                    ". No partial runtime point partition or route graph was returned.\n"+
                    string.Join("\n",failures.Select(failure=>failure.Message)),new AggregateException(failures));
            }
        }

        static string ProbePosition(Vector3 position)=>"["+
            position.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+
            position.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+
            position.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"]";

        static string ProbeCollider(Collider collider)
        {
            // Root/sibling indices plus component index identify the actual source even
            // when unrelated saved objects have duplicate names. Instance ID is run-local.
            var indices=new List<int>();
            for(var current=collider.transform;current!=null;current=current.parent)indices.Add(current.GetSiblingIndex());
            indices.Reverse();
            int component=Array.IndexOf(collider.GetComponents<Component>(),collider);
            return "siblingRoute="+string.Join("/",indices)+"; componentIndex="+component+
                "; type="+collider.GetType().Name+"; runLocalInstanceId="+collider.GetInstanceID()+
                "; scene="+collider.gameObject.scene.path+"; path="+PathOf(collider.transform);
        }

        static string ProbeHit(RaycastHit hit)=>"point="+ProbePosition(hit.point)+"; normal="+ProbePosition(hit.normal)+
            "; distance="+hit.distance.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; "+ProbeCollider(hit.collider);

        static string ProbeFloorHit(int ray,RaycastHit hit)
        {
            var indices=new List<int>();
            for(var current=hit.collider.transform;current!=null;current=current.parent)indices.Add(current.GetSiblingIndex());
            indices.Reverse();
            return "ray="+ray+"; point="+ProbePosition(hit.point)+"; normal="+ProbePosition(hit.normal)+
                "; siblingRoute="+string.Join("/",indices)+"; componentIndex="+Array.IndexOf(hit.collider.GetComponents<Component>(),hit.collider)+
                "; collider="+hit.collider.GetType().Name+"; runLocalInstanceId="+hit.collider.GetInstanceID();
        }

        internal static Bounds CandidateRayVolume(IReadOnlyList<Vector3> candidates)
        {
            if(candidates==null||candidates.Count==0)throw new InvalidOperationException("Current source candidate region is empty");
            var volume=new Bounds(candidates[0],Vector3.zero);
            foreach(var candidate in candidates)
            {
                if(float.IsNaN(candidate.x)||float.IsInfinity(candidate.x)||float.IsNaN(candidate.y)||float.IsInfinity(candidate.y)||
                    float.IsNaN(candidate.z)||float.IsInfinity(candidate.z))
                    throw new InvalidOperationException("Current source candidate region is not finite");
                for(int ray=-1;ray<8;ray++)
                {
                    // Exactly the existing Footprint ray origins and full1.5m segments,
                    // including the ACTUAL optional old-reference candidate and its Y.
                    var offset=ray<0?Vector3.zero:new Vector3(Mathf.Cos(ray*Mathf.PI/4),0,Mathf.Sin(ray*Mathf.PI/4))*.28f;
                    var start=candidate+offset+Vector3.up*.75f;
                    volume.Encapsulate(start);volume.Encapsulate(start+Vector3.down*1.5f);
                }
            }
            // Outward1mm is solely conservative proof padding: it never moves a
            // candidate, increases a query radius, or qualifies any walking surface.
            volume.Expand(.002f);
            return volume;
        }

        int RequireCurrentSourceQueryReady()
        {
            var physicsScene=scene.GetPhysicsScene();
            if(!physicsScene.IsValid()||physicsScene!=Physics.defaultPhysicsScene)
                throw new InvalidOperationException("Current source requires the same valid default PhysicsScene used by its ray/volume queries");
            int count=0;
            foreach(var root in scene.GetRootGameObjects())foreach(var collider in root.GetComponentsInChildren<Collider>())
            {
                if(collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy||
                    StationNavigationRecipe.Excluded(collider,trainSource,namedBeltsSource)||
                    collider.GetComponentInParent<FirstPersonResponder>()!=null||collider.GetComponentInParent<PersonBody>()!=null)continue;
                if(collider is MeshCollider mesh&&mesh.sharedMesh==null)
                    throw new InvalidOperationException("Current source collider has unresolved mesh geometry: "+ProbeCollider(collider));
                if(collider is TerrainCollider terrain&&terrain.terrainData==null)
                    throw new InvalidOperationException("Current source collider has unresolved terrain geometry: "+ProbeCollider(collider));
                var bounds=collider.bounds;var min=bounds.min;var max=bounds.max;
                if(float.IsNaN(min.x)||float.IsInfinity(min.x)||float.IsNaN(min.y)||float.IsInfinity(min.y)||float.IsNaN(min.z)||float.IsInfinity(min.z)||
                    float.IsNaN(max.x)||float.IsInfinity(max.x)||float.IsNaN(max.y)||float.IsInfinity(max.y)||float.IsNaN(max.z)||float.IsInfinity(max.z)||
                    min.x>max.x||min.y>max.y||min.z>max.z)
                    throw new InvalidOperationException("Current source collider has unresolved physical bounds: "+ProbeCollider(collider));
                count++;
            }
            if(count==0)throw new InvalidOperationException("No complete eligible current source collider census");
            return count;
        }

        // Necessary center-ray contact only. Considering ALL face orientations avoids
        // any assumption about interpolated normals, winding, backfaces or mirror parity.
        internal sealed class CenterSupportDomain
        {
            struct Vertex
            {
                public double X,Y,Z;
                public Vertex(double x,double y,double z){X=x;Y=y;Z=z;}
            }
            readonly double x,z,radiusSquared,minY,maxY;
            public CenterSupportDomain(Vector3 source,IReadOnlyList<Vector3> candidates)
            {
                if(candidates==null||candidates.Count==0)throw new InvalidOperationException("No complete center-support domain");
                x=source.x;z=source.z;double radius=0;minY=double.PositiveInfinity;maxY=double.NegativeInfinity;
                foreach(var candidate in candidates)
                {
                    if(!Finite(candidate))throw new InvalidOperationException("Nonfinite center-support domain");
                    double dx=candidate.x-x,dz=candidate.z-z;radius=Math.Max(radius,Math.Sqrt(dx*dx+dz*dz));
                    minY=Math.Min(minY,candidate.y);maxY=Math.Max(maxY,candidate.y);
                }
                if(!Finite(source))throw new InvalidOperationException("Nonfinite source center");
                radiusSquared=(radius+.001)*(radius+.001);minY-=.351;maxY+=.351;
            }
            static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&
                !float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
            static List<Vertex> Clip(List<Vertex> polygon,double height,bool above)
            {
                var result=new List<Vertex>();if(polygon.Count==0)return result;
                var prior=polygon[polygon.Count-1];bool priorIn=above?prior.Y>=height:prior.Y<=height;
                foreach(var current in polygon)
                {
                    bool inside=above?current.Y>=height:current.Y<=height;
                    if(inside!=priorIn)
                    {
                        double t=(height-prior.Y)/(current.Y-prior.Y);
                        result.Add(new Vertex(prior.X+(current.X-prior.X)*t,height,prior.Z+(current.Z-prior.Z)*t));
                    }
                    if(inside)result.Add(current);prior=current;priorIn=inside;
                }
                return result;
            }
            public bool MayContainCenterContact(Vector3 a,Vector3 b,Vector3 c)
            {
                if(!Finite(a)||!Finite(b)||!Finite(c))throw new InvalidOperationException("Nonfinite actual source face");
                if(Math.Max(a.y,Math.Max(b.y,c.y))<minY||Math.Min(a.y,Math.Min(b.y,c.y))>maxY)return false;
                double left=Math.Min(a.x,Math.Min(b.x,c.x)),right=Math.Max(a.x,Math.Max(b.x,c.x));
                double back=Math.Min(a.z,Math.Min(b.z,c.z)),front=Math.Max(a.z,Math.Max(b.z,c.z));
                double dx=Math.Max(left-x,Math.Max(0,x-right)),dz=Math.Max(back-z,Math.Max(0,z-front));
                if(dx*dx+dz*dz>radiusSquared)return false;
                var polygon=Clip(Clip(new List<Vertex>{new Vertex(a.x,a.y,a.z),new Vertex(b.x,b.y,b.z),new Vertex(c.x,c.y,c.z)},minY,true),maxY,false);
                if(polygon.Count==0)return false;
                bool positive=false,negative=false;double area=0;
                for(int i=0;i<polygon.Count;i++)
                {
                    var u=polygon[i];var v=polygon[(i+1)%polygon.Count];
                    double ux=u.X-x,uz=u.Z-z,vx=v.X-x,vz=v.Z-z;
                    double ex=vx-ux,ez=vz-uz,length=ex*ex+ez*ez;
                    double t=length==0?0:Math.Max(0,Math.Min(1,-(ux*ex+uz*ez)/length));
                    double qx=ux+t*ex,qz=uz+t*ez;
                    if(qx*qx+qz*qz<=radiusSquared)return true;
                    double cross=ux*vz-uz*vx;area+=cross;
                    positive|=cross>1e-12;negative|=cross< -1e-12;
                }
                return Math.Abs(area)>1e-12&&!(positive&&negative); // Disc center inside the entire projected polygon.
            }
            public string Describe()=>"centerXZ=["+x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+
                z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"]; radius="+Math.Sqrt(radiusSquared).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                "; minY="+minY.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; maxY="+maxY.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                "; all actual candidate center-ray contacts enclosed; ALL face orientations considered";
        }

        sealed class SourceMeshBuffers
        {
            public readonly Vector3[] Vertices;
            public readonly int[][] Indices;
            public SourceMeshBuffers(Mesh mesh)
            {
                Vertices=mesh.vertices;Indices=new int[mesh.subMeshCount][];
                foreach(var v in Vertices)if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))
                    throw new InvalidOperationException("Nonfinite current source mesh buffer");
                for(int sub=0;sub<Indices.Length;sub++)
                {
                    var indices=mesh.GetIndices(sub);Indices[sub]=indices;
                    if(indices.Length!=0&&(mesh.GetTopology(sub)!=MeshTopology.Triangles||indices.Length%3!=0))
                        throw new InvalidOperationException("Unresolved current source surface topology");
                    foreach(int index in indices)if(index<0||index>=Vertices.Length)throw new InvalidOperationException("Invalid current source triangle index");
                }
            }
        }

        static bool SourceFrameResolved(Matrix4x4 matrix)
        {
            for(int i=0;i<16;i++)if(float.IsNaN(matrix[i])||float.IsInfinity(matrix[i]))return false;
            var a=matrix.MultiplyVector(Vector3.right);var b=matrix.MultiplyVector(Vector3.up);var c=matrix.MultiplyVector(Vector3.forward);
            if(a.sqrMagnitude<=1e-16f||b.sqrMagnitude<=1e-16f||c.sqrMagnitude<=1e-16f)return false;
            return Mathf.Abs(Vector3.Dot(a.normalized,b.normalized))<1e-5f&&Mathf.Abs(Vector3.Dot(a.normalized,c.normalized))<1e-5f&&
                Mathf.Abs(Vector3.Dot(b.normalized,c.normalized))<1e-5f; // Sheared/ambiguous physics shape stays UNKNOWN.
        }

        internal readonly struct SourceFace
        {
            public readonly Vector3 A,B,C;
            public SourceFace(Vector3 a,Vector3 b,Vector3 c){A=a;B=b;C=c;}
        }

        static void AddSourceFace(List<SourceFace> faces,Bounds? volume,Vector3 a,Vector3 b,Vector3 c)
        {
            foreach(var v in new[]{a,b,c})
                if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))
                    throw new InvalidOperationException("Nonfinite current world source face");
            var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
            if(!volume.HasValue||volume.Value.Intersects(bounds))faces.Add(new SourceFace(a,b,c));
        }

        bool CollectSourceFaces(Collider collider,Bounds? volume,Dictionary<Mesh,SourceMeshBuffers> buffers,List<SourceFace> faces,ref int triangles)
        {
            var matrix=collider.transform.localToWorldMatrix;
            if(!SourceFrameResolved(matrix))return false;
            if(collider is MeshCollider meshCollider)
            {
                var mesh=meshCollider.sharedMesh;
                if(mesh==null||meshCollider.convex||!mesh.isReadable)return false;
                if(!buffers.TryGetValue(mesh,out var source))buffers[mesh]=source=new SourceMeshBuffers(mesh);
                foreach(var indices in source.Indices)for(int i=0;i<indices.Length;i+=3)
                {
                    triangles++;
                    AddSourceFace(faces,volume,matrix.MultiplyPoint3x4(source.Vertices[indices[i]]),
                        matrix.MultiplyPoint3x4(source.Vertices[indices[i+1]]),matrix.MultiplyPoint3x4(source.Vertices[indices[i+2]]));
                }
                return true;
            }
            if(collider is BoxCollider box)
            {
                if(matrix.determinant<=0)return false;
                var frame=box.transform.rotation;
                foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
                    if(Mathf.Abs(Vector3.Dot(matrix.MultiplyVector(axis).normalized,frame*axis))<.99999f)return false;
                var size=box.size;
                if(size.x<=0||size.y<=0||size.z<=0||float.IsNaN(size.x)||float.IsNaN(size.y)||float.IsNaN(size.z)||
                    float.IsInfinity(size.x)||float.IsInfinity(size.y)||float.IsInfinity(size.z))return false;
                var corners=new Vector3[8];var bounds=new Bounds(matrix.MultiplyPoint3x4(box.center),Vector3.zero);
                for(int i=0;i<8;i++)
                {
                    corners[i]=matrix.MultiplyPoint3x4(box.center+Vector3.Scale(size,new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f)));
                    bounds.Encapsulate(corners[i]);
                }
                // Source-to-physics primitive frame parity is required; a broad AABB
                // alone cannot certify the rotated/reflected primitive's exact faces.
                if(Vector3.Distance(bounds.min,box.bounds.min)>.0005f||Vector3.Distance(bounds.max,box.bounds.max)>.0005f)return false;
                int[] indices={0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3};
                for(int i=0;i<indices.Length;i+=3)
                {triangles++;AddSourceFace(faces,volume,corners[indices[i]],corners[indices[i+1]],corners[indices[i+2]]);}
                return true;
            }
            return false; // Convex/curved/terrain/other source has no exact face proof here.
        }

        internal static bool RequiredFootprintsHaveNoSource(IReadOnlyList<Vector3> candidates,IReadOnlyList<SourceFace> faces,out string witness)
        {
            // Negative-only necessary-condition proof. Every ACTUAL candidate must
            // lack at least one required ray contact in COMPLETE resolved geometry.
            // Positive/ambiguous faces remain possible, regardless of orientation.
            var missing=new List<string>();
            for(int i=0;i<candidates.Count;i++)
            {
                bool deficient=false;
                for(int ray=-1;ray<8;ray++)
                {
                    var offset=ray<0?Vector3.zero:new Vector3(Mathf.Cos(ray*Mathf.PI/4),0,Mathf.Sin(ray*Mathf.PI/4))*.28f;
                    var center=candidates[i]+offset;
                    var contact=new CenterSupportDomain(center,new[]{center}); // allowed hitY +/- .35, padded1mm
                    if(faces.Any(face=>contact.MayContainCenterContact(face.A,face.B,face.C)))continue;
                    missing.Add(i+":"+ray);deficient=true;break;
                }
                if(!deficient){witness="candidate="+i+" has potential source contacts for all nine required rays";return false;}
            }
            if(candidates.Count==0)throw new InvalidOperationException("Current source candidate cohort is empty");
            witness="everyActualCandidateHasMissingNecessaryRay=true; candidate:missingRay={"+string.Join(",",missing)+"}";
            return true;
        }

        bool CurrentSourceSupportAbsent(Vector3 source,IReadOnlyList<Vector3> candidates,PointProbeReport probes,string identity,
            Dictionary<Mesh,SourceMeshBuffers> buffers,out string proof)
        {
            proof="";var volume=CandidateRayVolume(candidates);
            var overlaps=Physics.OverlapBox(volume.center,volume.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(overlaps==null||overlaps.Any(c=>c==null))throw new InvalidOperationException("Current source overlap evidence is incomplete: "+identity);
            var eligible=overlaps.Where(c=>c.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(c,trainSource,namedBeltsSource)&&
                c.GetComponentInParent<FirstPersonResponder>()==null&&c.GetComponentInParent<PersonBody>()==null).ToArray();
            bool observedFloor=probes.Has("candidate/floor-complete");
            if(eligible.Length==0)
            {
                if(observedFloor)throw new InvalidOperationException("Observed current floor contradicts complete empty source-volume evidence: "+identity);
                proof=StationPoints.CurrentBinding.CurrentSourceEmptyEvidence+" "+identity+"; sourceScene="+scene.path+
                    "; min="+ProbePosition(volume.min)+"; max="+ProbePosition(volume.max)+"; candidates="+candidates.Count+
                    "; Physics.OverlapBox complete; eligibleCookedColliders=0; rawOverlaps="+overlaps.Length+
                    "; mask=all; triggers=Ignore; canonical source/actor exclusions; whole candidate-ray volume+1mm; "+
                    "current modeled endpoint unavailable; no real-world public-exit closure claim";
                probes.Record("source-volume/empty",proof);return true;
            }
            probes.Record("source-volume/nonempty","eligibleCookedColliders="+eligible.Length+"; first="+ProbeCollider(eligible[0]));
            if(observedFloor)return false; // A blocked or Nav-missing supported floor cannot become unavailable.
            var domain=new CenterSupportDomain(source,candidates);int triangles=0;
            var faces=new List<SourceFace>();
            foreach(var collider in eligible)
                if(!CollectSourceFaces(collider,volume,buffers,faces,ref triangles))
                {probes.Record("source-faces/unresolved",ProbeCollider(collider));return false;}
            // Each source buffer is scanned once. Only faces intersecting the complete
            // padded ray envelope are retained for this point's synchronous proof.
            if(!faces.Any(face=>domain.MayContainCenterContact(face.A,face.B,face.C)))
            {
                proof=StationPoints.CurrentBinding.CurrentSourceFloorEvidence+" "+identity+"; sourceScene="+scene.path+
                    "; broadEligibleCookedColliders="+eligible.Length+"; completeSourceTriangles="+triangles+"; "+domain.Describe()+
                    "; no possible center-ray surface contact; no normals/slope/winding qualification; current modeled endpoint unavailable; no real-world public-exit closure claim";
                probes.Record("center-domain/no-source-contact",proof);return true;
            }
            probes.Record("center-domain/potential-contact",domain.Describe());
            if(!RequiredFootprintsHaveNoSource(candidates,faces,out var gaps))
            {probes.Record("required-footprints/potential-or-unknown",gaps);return false;}
            proof=StationPoints.CurrentBinding.CurrentSourceFootprintEvidence+" "+identity+"; sourceScene="+scene.path+
                "; broadEligibleCookedColliders="+eligible.Length+"; completeSourceTriangles="+triangles+"; retainedEnvelopeFaces="+faces.Count+
                "; candidates="+candidates.Count+"; "+gaps+"; all orientations; each necessary ray contact padded1mm in XZ and allowed hitY; "+
                "only the exact authorized candidate footprints are unavailable; no continuous-floor absence or real-world closure claim";
            probes.Record("required-footprints/complete-source-deficiency",proof);return true;
        }

        internal static string CurrentBindingDecision(bool provenAbsent,bool sourceSupportAbsent,bool physicalSupported,bool currentNavSupported)
        {
            if(sourceSupportAbsent)
            {
                if(physicalSupported||currentNavSupported)
                    throw new InvalidOperationException("Current positive support/navigation contradicts the complete current-source support-absence evidence");
                if(!provenAbsent)return StationPoints.CurrentBinding.CurrentSourceUnavailableStatus;
            }
            return BindingDecision(provenAbsent,physicalSupported,currentNavSupported);
        }

        static Vector3 ClosestSegmentPoint(Vector3 point,Vector3 a,Vector3 b)
        {
            var edge=b-a;float length=edge.sqrMagnitude;
            return length<=1e-16f?a:a+edge*Mathf.Clamp01(Vector3.Dot(point-a,edge)/length);
        }

        internal static Vector3 ClosestTrianglePoint(Vector3 point,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;
            if(Vector3.Cross(ab,ac).sqrMagnitude<=1e-16f)
            {
                var choices=new[]{ClosestSegmentPoint(point,a,b),ClosestSegmentPoint(point,b,c),ClosestSegmentPoint(point,c,a)};
                return choices.OrderBy(v=>(v-point).sqrMagnitude).First();
            }
            var ap=point-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return a;
            var bp=point-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
            if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;
            if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=point-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
            if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;
            if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;
            if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float divisor=1f/(va+vb+vc);
            return a+ab*(vb*divisor)+ac*(vc*divisor);
        }

        static string ProbeMatrix(Matrix4x4 matrix)=>"["+string.Join(",",Enumerable.Range(0,16)
            .Select(i=>matrix[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"]";

        string DescribeCollectedSource(NavMeshBuildSource source)
        {
            string geometry="";
            if(source.sourceObject is Mesh mesh)
            {
                long indices=0;double area=0;bool triangleBuffers=mesh.isReadable;
                for(int sub=0;sub<mesh.subMeshCount;sub++)indices+=(long)mesh.GetIndexCount(sub);
                if(triangleBuffers)
                {
                    var vertices=mesh.vertices;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        if(mesh.GetTopology(sub)!=MeshTopology.Triangles){triangleBuffers=false;break;}
                        var parts=mesh.GetIndices(sub);
                        for(int i=0;i+2<parts.Length;i+=3)
                        {
                            var a=source.transform.MultiplyPoint3x4(vertices[parts[i]]);
                            var b=source.transform.MultiplyPoint3x4(vertices[parts[i+1]]);
                            var c=source.transform.MultiplyPoint3x4(vertices[parts[i+2]]);
                            area+=Vector3.Cross(b-a,c-a).magnitude*.5;
                        }
                    }
                }
                geometry="; mesh="+mesh.name+"; meshRunLocalInstance="+mesh.GetInstanceID()+"; readable="+mesh.isReadable+
                    "; vertices="+mesh.vertexCount+"; submeshes="+mesh.subMeshCount+"; indices="+indices+
                    "; allFaceAreaWorld="+(triangleBuffers?area.ToString("R",System.Globalization.CultureInfo.InvariantCulture):"UNKNOWN")+
                    "; geometric area only, not Recast retained area";
            }
            return "shape="+source.shape+"; area="+source.area+"; size="+ProbePosition(source.size)+"; matrix="+ProbeMatrix(source.transform)+geometry;
        }

        string DiagnosticOwnerBelow(Vector3 point)
        {
            var hits=Physics.RaycastAll(point+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(h.collider,trainSource,namedBeltsSource)&&
                    h.collider.GetComponentInParent<FirstPersonResponder>()==null&&h.collider.GetComponentInParent<PersonBody>()==null)
                .OrderBy(h=>h.distance).ToArray();
            return hits.Length==0?"no exposed eligible source hit":ProbeHit(hits[0]);
        }

        static string DescribeSettings(NavMeshBuildSettings settings)
        {
            var invariant=System.Globalization.CultureInfo.InvariantCulture;
            return "agentTypeID="+settings.agentTypeID+"; agentRadius="+settings.agentRadius.ToString("R",invariant)+
                "; agentHeight="+settings.agentHeight.ToString("R",invariant)+"; agentClimb="+settings.agentClimb.ToString("R",invariant)+
                "; agentSlope="+settings.agentSlope.ToString("R",invariant)+"; minRegionArea="+settings.minRegionArea.ToString("R",invariant)+
                "; overrideVoxelSize="+settings.overrideVoxelSize+"; voxelSize="+settings.voxelSize.ToString("R",invariant)+
                "; overrideTileSize="+settings.overrideTileSize+"; tileSize="+settings.tileSize+"; buildHeightMesh="+settings.buildHeightMesh;
        }

        void DescribeMissingNavigation(PointProbeReport probes,NavMeshTriangulation triangulation)
        {
            probes.Record("nav-diagnostic/settings","DIAGNOSTIC ONLY; original-reference lease disposed; actual current recipe="+
                DescribeSettings(collectedSettings)+"; buildBoundsMin="+ProbePosition(Bounds.min)+"; buildBoundsMax="+ProbePosition(Bounds.max)+
                "; collectedSources="+(collectedSources==null?-1:collectedSources.Count)+"; actualClearSupportedPoses="+probes.ClearFloorPositions.Count+
                "; no counter-bake, changed candidate, binding fallback or settings change");
            if(probes.ClearFloorPositions.Count==0)throw new InvalidOperationException("Supported Nav-missing diagnostics lack actual clear supported pose");
            int floorIndex=0;
            foreach(var floor in probes.FloorSources.OrderBy(c=>c.GetInstanceID()))
            {
                var exact=collectedSources.Where(source=>source.component==floor).ToArray();
                var sameObject=collectedSources.Where(source=>source.component!=null&&source.component.gameObject==floor.gameObject).ToArray();
                probes.Record("nav-diagnostic/floor-source-"+floorIndex,ProbeCollider(floor)+"; exactCollectedComponentMatches="+exact.Length+
                    "; sameGameObjectMatches="+sameObject.Length+"; sourceBoundsMin="+ProbePosition(floor.bounds.min)+"; sourceBoundsMax="+ProbePosition(floor.bounds.max)+
                    "; exact descriptors={"+string.Join(" | ",exact.Select(DescribeCollectedSource))+"}");
                floorIndex++;
            }
            int modifierIndex=0;
            foreach(var modifier in collectedSources.Where(source=>source.shape==NavMeshBuildSourceShape.ModifierBox))
            {
                var inverse=modifier.transform.inverse;var local=new Bounds(Vector3.zero,modifier.size);
                int inside=probes.ClearFloorPositions.Count(point=>local.Contains(inverse.MultiplyPoint3x4(point)));
                // Only descriptor/intersection evidence: this is not clearance or a
                // positive navigation decision. Modifier frames are rigid canonical boxes.
                var diagnosticMargin=local;diagnosticMargin.Expand(1.2f);
                int nearby=probes.ClearFloorPositions.Count(point=>diagnosticMargin.Contains(inverse.MultiplyPoint3x4(point)));
                probes.Record("nav-diagnostic/modifier-"+modifierIndex,DescribeCollectedSource(modifier)+"; actualSupportedPosesInside="+inside+
                    "; insideExpandedByDiagnostic0.6m="+nearby);
                modifierIndex++;
            }
            var positions=probes.ClearFloorPositions;
            float nearest=float.PositiveInfinity;int nearestTriangle=-1,nearCount=0;Vector3 nearestPoint=default,nearestSupported=default;
            var window=new Bounds(positions[0],Vector3.zero);foreach(var point in positions)window.Encapsulate(point);window.Expand(16f);
            for(int i=0;i+2<triangulation.indices.Length;i+=3)
            {
                var a=triangulation.vertices[triangulation.indices[i]];var b=triangulation.vertices[triangulation.indices[i+1]];var c=triangulation.vertices[triangulation.indices[i+2]];
                var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                if(!Bounds.Intersects(bounds)||!window.Intersects(bounds))continue;
                bool near=false;
                foreach(var supported in positions)
                {
                    var point=ClosestTrianglePoint(supported,a,b,c);
                    if(Mathf.Abs(point.y-supported.y)>.35f)continue;
                    float distance=Vector3.Distance(point,supported);
                    if(distance<=8)near=true;
                    if(distance<nearest){nearest=distance;nearestTriangle=i/3;nearestPoint=point;nearestSupported=supported;}
                }
                if(near)nearCount++;
            }
            probes.Record("nav-diagnostic/registered-triangulation","DIAGNOSTIC ONLY; global registered coarse triangulation after old-reference removal; "+
                "public API supplies no NavMeshData/Collider ownership; current Data ownership NOT inferred; totalTriangles="+triangulation.indices.Length/3+
                "; sameStoreyWithin8mTriangles="+nearCount+"; closestSameStoreyDistance="+nearest.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            if(nearestTriangle>=0)
            {
                int i=nearestTriangle*3;var a=triangulation.vertices[triangulation.indices[i]];var b=triangulation.vertices[triangulation.indices[i+1]];var c=triangulation.vertices[triangulation.indices[i+2]];
                probes.Record("nav-diagnostic/nearest-triangle","globalTriangleIndex="+nearestTriangle+"; area="+triangulation.areas[nearestTriangle]+
                    "; A="+ProbePosition(a)+"; B="+ProbePosition(b)+"; C="+ProbePosition(c)+"; closest="+ProbePosition(nearestPoint)+
                    "; actualSupported="+ProbePosition(nearestSupported)+"; Ydelta="+(nearestPoint.y-nearestSupported.y).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                    "; nearestPointPhysicalSampleOnly={"+DiagnosticOwnerBelow(nearestPoint)+"}; this sample does not identify the whole triangle's source owner");
            }
            var samplePoses=new List<Vector3>{positions[0]};
            if(nearestTriangle>=0&&Vector3.Distance(positions[0],nearestSupported)>1e-6f)samplePoses.Add(nearestSupported);
            for(int p=0;p<samplePoses.Count;p++)
            {
                var details=new List<string>();
                foreach(float radius in new[]{.6f,1f,2f,4f,8f})
                {
                    bool found=NavMesh.SamplePosition(samplePoses[p],out var hit,radius,NavMesh.AllAreas);
                    details.Add("radius="+radius+"; found="+found+(found?"; hit="+ProbePosition(hit.position)+"; distance="+hit.distance+
                        "; Ydelta="+(hit.position.y-samplePoses[p].y)+"; areaMask="+hit.mask:""));
                }
                probes.Record("nav-diagnostic/native-samples-"+p,"DIAGNOSTIC ONLY; actual supported="+ProbePosition(samplePoses[p])+"; {"+string.Join(" | ",details)+"}");
            }
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
            var failures=new PointBindingFailures();int attempted=0;
            var pending=new List<(string identity,PointProbeReport probes,InvalidOperationException cause,bool navMissing)>();
            // Settle source transforms once before the synchronous qualification scan.
            // No yield, simulation step or source mutation occurs between its queries.
            Physics.SyncTransforms();
            int eligibleSourceCount=RequireCurrentSourceQueryReady();
            // Only meshes needed by failed physical probes are read; buffers live for
            // this synchronous scan, with no scene/frame/persistent proof cache.
            var sourceBuffers=new Dictionary<Mesh,SourceMeshBuffers>();
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
                var probes=new PointProbeReport();attempted=i+1;
                bool physical=false,nav=false;Vector3 feet=source.Position;string evidence="";
                foreach(var candidate in candidates)
                {
                    probes.Candidates++;
                    if(!Footprint(candidate,reference,out var supported,out var proof,probes,"candidate"))continue;
                    physical=true;evidence=proof;
                    if(absent)break;
                    bool sampled=NavMesh.SamplePosition(supported,out var hit,.6f,NavMesh.AllAreas);
                    if(!sampled)
                    {probes.Record("nav/sample-missing","sampled=false; supported="+ProbePosition(supported)+"; radius=.6");continue;}
                    probes.Record("nav/sample-found");
                    if(Mathf.Abs(hit.position.y-supported.y)>.35f)
                    {probes.Record("nav/same-storey-rejected","sampled=true; supported="+ProbePosition(supported)+"; nav="+ProbePosition(hit.position)+"; maxHeightDelta=.35");continue;}
                    if(!Footprint(hit.position,reference,out var corroborated,out var navProof,probes,"nav"))continue;
                    if(Vector3.Distance(corroborated,hit.position)>.35f)
                    {probes.Record("nav/corroboration-distance-rejected","nav="+ProbePosition(hit.position)+"; actualFloor="+ProbePosition(corroborated)+"; maxDistance=.35");continue;}
                    probes.Record("nav/qualified");
                    feet=hit.position;evidence+="; nav corroboration="+navProof;nav=true;break;
                }
                bool sourceSupportAbsent=CurrentSourceSupportAbsent(source.Position,candidates,probes,
                    "sourceIndex="+i+"; sourceId="+source.Id+"; sourceCatalog="+catalog.SourceSha256+
                    "; completeEligibleSourceColliders="+eligibleSourceCount,sourceBuffers,out var unavailableProof);
                string status;
                try{status=CurrentBindingDecision(absent,sourceSupportAbsent,physical,nav);}
                catch(InvalidOperationException cause)
                {
                    pending.Add(("sourceIndex="+i+"; id="+source.Id+"; kind="+source.Kind+"; zone="+source.Zone+
                        "; sourcePosition="+ProbePosition(source.Position)+"; disposition="+disposition+
                        "; walkableFootprint="+physical+"; currentNav="+nav,probes,cause,physical&&!nav));
                    if(pending.Count==PointBindingFailures.Limit)break;
                    continue;
                }
                var runtime=nav?new StationPoints.Point{Id=source.Id,Label=source.Label,Zone=source.Zone,Kind=source.Kind,
                    Position=feet,Anchor=source.Anchor,Yaw=source.Yaw,Slot=source.Slot}:null;
                result.Add(new StationPoints.CurrentBinding{Source=source,Runtime=runtime,Status=status,
                    Evidence=absent?(string)row["evidence"]:sourceSupportAbsent?unavailableProof:evidence+"; projection="+(feet-source.Position).ToString("F5")});
            }
            // BakedFloor's shifted old-reference copy is now removed. Diagnostics
            // cannot add candidates, qualify points, change Data or return a view.
            NavMeshTriangulation? triangulation=null;
            foreach(var failure in pending)
            {
                if(failure.navMissing)
                {
                    try
                    {
                        if(!triangulation.HasValue)triangulation=NavMesh.CalculateTriangulation();
                        DescribeMissingNavigation(failure.probes,triangulation.Value);
                    }
                    catch(Exception diagnosticError) when(!(diagnosticError is OutOfMemoryException))
                    {failure.probes.Record("nav-diagnostic/unavailable",diagnosticError.GetType().Name+": "+diagnosticError.Message);}
                }
                failures.Add(failure.identity,failure.probes,failure.cause);
            }
            failures.ThrowIfAny(attempted,catalog.All.Count);
            return catalog.WithCurrentBindings(result.AsReadOnly());
        }

        bool Footprint(Vector3 staged,BakedFloor reference,out Vector3 floor,out string evidence)
            =>Footprint(staged,reference,out floor,out evidence,null,null);

        bool Footprint(Vector3 staged,BakedFloor reference,out Vector3 floor,out string evidence,PointProbeReport probes,string phase)
        {
            floor=staged;evidence="";float low=float.PositiveInfinity,high=float.NegativeInfinity;
            bool allNew=true;var owners=new HashSet<string>();
            var floorSources=probes==null?null:new HashSet<Collider>();
            var observed=probes!=null&&probes.NeedsExample(phase+"/floor-complete")?new List<string>():null;
            for(int i=-1;i<8;i++)
            {
                var offset=i<0?Vector3.zero:new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4))*.28f;
                var hits=Physics.RaycastAll(staged+offset+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(h.collider,trainSource,namedBeltsSource)&&
                        h.collider.GetComponentInParent<FirstPersonResponder>()==null&&h.collider.GetComponentInParent<PersonBody>()==null)
                    .OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0)
                {probes?.Record(phase+"/floor-ray-missing","nineRaysComplete=false; staged="+ProbePosition(staged)+"; ray="+i);return false;}
                var hit=hits[0];
                if(hit.normal.y<Mathf.Cos(36*Mathf.Deg2Rad))
                {probes?.Record(phase+"/floor-slope-rejected","nineRaysComplete=false; staged="+ProbePosition(staged)+"; ray="+i+"; "+ProbeHit(hit));return false;}
                if(Mathf.Abs(hit.point.y-staged.y)>.35f)
                {probes?.Record(phase+"/floor-height-rejected","nineRaysComplete=false; staged="+ProbePosition(staged)+"; ray="+i+"; "+ProbeHit(hit));return false;}
                low=Mathf.Min(low,hit.point.y);high=Mathf.Max(high,hit.point.y);
                allNew&=newFloorSources.Contains(hit.collider);owners.Add(PathOf(hit.collider.transform));floorSources?.Add(hit.collider);
                observed?.Add(ProbeFloorHit(i,hit));
            }
            if(high-low>.35f)
            {probes?.Record(phase+"/floor-spread-rejected","nineRaysComplete=true; staged="+ProbePosition(staged)+"; low="+low.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; high="+high.ToString("R",System.Globalization.CultureInfo.InvariantCulture));return false;}
            floor.y=high;
            if(floorSources!=null)foreach(var collider in floorSources)probes.FloorSources.Add(collider);
            probes?.Record(phase+"/floor-complete",observed==null?null:"nineRaysComplete=true; staged="+ProbePosition(staged)+"; floor="+ProbePosition(floor)+"; actualHits={"+string.Join(" | ",observed)+"}");
            // The old cache is only an independent partial classification reference;
            // actual current footprint+clearance are mandatory. Explicit current floors
            // independently extend that reference where the old cache is missing.
            bool corroborated=reference!=null&&reference.Sample(floor,.6f,out var old)&&Mathf.Abs(old.y-floor.y)<.35f;
            var bottom=floor+Vector3.up*(.28f+.03f);var top=floor+Vector3.up*(1.8f-.28f);
            foreach(var collider in Physics.OverlapCapsule(bottom,top,.28f,~0,QueryTriggerInteraction.Ignore))
                if(collider.gameObject.scene==scene&&!StationNavigationRecipe.Excluded(collider,trainSource,namedBeltsSource)&&collider.GetComponentInParent<FirstPersonResponder>()==null&&
                    collider.GetComponentInParent<PersonBody>()==null)
                {probes?.Record(phase+"/clearance-blocked","nineRaysComplete=true; floor="+ProbePosition(floor)+"; capsuleBottom="+ProbePosition(bottom)+"; capsuleTop="+ProbePosition(top)+"; radius=.28; "+ProbeCollider(collider));return false;}
            probes?.Record(phase+"/clearance-complete");
            if(phase=="candidate"&&probes!=null)probes.ClearFloorPositions.Add(floor);
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
            supports.Clear();collectedSources=null;
        }
    }
    internal sealed class CurrentStationNavigationLifetime : MonoBehaviour
    {
        internal CurrentStationNavigation Navigation;
        void OnDestroy(){Navigation?.RetireSourceScene();Navigation=null;}
    }
}
