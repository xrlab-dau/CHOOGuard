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
        CurrentWalkingSourceIdentity[] capturedWalkingSources;
        CurrentWalkingActualIdentity[] capturedActualWalkingSources;
        NavMeshBuildSource[] originalWalkingNativeSources;
        bool[] originalWalkingNativeHadOwners;
        NavMeshBuildSettings collectedSettings;
        WalkingMachineBox[] boundWalkingMachines;
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

        internal static NavMeshBuildSettings CurrentSourceSettings()
        {
            // Current saved floors include a physically supported .651834m landing.
            // Preserve the .28m agent and canonical source/area rules, but resolve
            // its narrow walkable strip at seven voxels per radius and retain small
            // regions. Physical qualification and every active point's exit route
            // remain mandatory; retaining an island never grants connectivity.
            var settings=StationNavigationRecipe.Settings();
            settings.minRegionArea=0;
            settings.overrideVoxelSize=true;settings.voxelSize=.04f;
            return settings;
        }

        internal static void WriteCurrentSourceSettings(BinaryWriter writer,NavMeshBuildSettings settings)
        {
            // NavMeshBuildSettings exposes properties over private native fields;
            // JsonUtility is not an exact settings identity. Include every public
            // build/debug property plus a versioned current-source profile marker.
            writer.Write("current-source-navigation-seven-voxels-retained-regions-v1");
            writer.Write(settings.agentTypeID);writer.Write(settings.agentRadius);writer.Write(settings.agentHeight);
            writer.Write(settings.agentSlope);writer.Write(settings.agentClimb);writer.Write(settings.ledgeDropHeight);
            writer.Write(settings.maxJumpAcrossDistance);writer.Write(settings.minRegionArea);
            writer.Write(settings.overrideVoxelSize);writer.Write(settings.voxelSize);
            writer.Write(settings.overrideTileSize);writer.Write(settings.tileSize);writer.Write(settings.buildHeightMesh);
            writer.Write(settings.maxJobWorkers);writer.Write(settings.preserveTilesOutsideBounds);writer.Write((int)settings.debug.flags);
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
                WriteCurrentSourceSettings(writer,CurrentSourceSettings());
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
                boundWalkingMachines=lanes.Select(lane=>new WalkingMachineBox(lane.Id,
                    Matrix4x4.TRS(lane.BoxCentre,lane.BoxRotation,Vector3.one).inverse,lane.BoxSize)).ToArray();
                var sources=StationNavigationRecipe.Collect(scene,bounds,train,namedBelts,lanes,true);
                if(sources.Count==0)throw new InvalidOperationException("No actual current navigation sources");
                collectedSources=sources;collectedSettings=CurrentSourceSettings();
                capturedWalkingSources=CaptureCurrentWalkingSources(sources);
                capturedActualWalkingSources=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Collider>(true))
                    .Select(collider=>new CurrentWalkingActualIdentity(collider,StationNavigationRecipe.Excluded(collider,train,namedBelts))).ToArray();
                originalWalkingNativeSources=sources.ToArray();
                originalWalkingNativeHadOwners=sources.Select(source=>source.component!=null).ToArray();
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

        internal readonly struct WalkingMachineBox
        {
            public readonly string Id;
            public readonly Matrix4x4 Inverse;
            public readonly Vector3 Half;
            public WalkingMachineBox(string id,Matrix4x4 inverse,Vector3 size)
            {
                if(string.IsNullOrWhiteSpace(id)||!SourceFrameResolved(inverse)||size.x<=0||size.y<=0||size.z<=0||
                    float.IsNaN(size.x)||float.IsNaN(size.y)||float.IsNaN(size.z)||float.IsInfinity(size.x)||float.IsInfinity(size.y)||float.IsInfinity(size.z))
                    throw new InvalidOperationException("Current bound machinery frame is unresolved");
                Id=id;Inverse=inverse;
                // Actual source-derived NotWalkable box plus the unchanged walking
                // controller radius. No source/modifier extent is reduced.
                Half=size*.5f+Vector3.one*NativeLandingWalker.Radius;
            }
            public bool Intersects(Vector3 from,Vector3 to,out float first,out float last)
            {
                var a=Inverse.MultiplyPoint3x4(from);var b=Inverse.MultiplyPoint3x4(to);
                first=0;last=1;
                for(int axis=0;axis<3;axis++)
                {
                    float start=a[axis],delta=b[axis]-start,half=Half[axis];
                    if(Mathf.Abs(delta)<1e-8f){if(start< -half||start>half)return false;continue;}
                    float low=(-half-start)/delta,high=(half-start)/delta;
                    if(low>high){var swap=low;low=high;high=swap;}
                    first=Mathf.Max(first,low);last=Mathf.Min(last,high);
                    if(first>last)return false;
                }
                return first<=last;
            }
        }

        static Vector3 WalkingPointAt(IReadOnlyList<Vector3> path,float[] distances,float at)
        {
            for(int i=1;i<path.Count;i++)
                if(at<=distances[i]&&distances[i]>distances[i-1])
                    return Vector3.Lerp(path[i-1],path[i],(at-distances[i-1])/(distances[i]-distances[i-1]));
            return path[path.Count-1];
        }

        internal static Vector3[] CurrentPublicWalkingPath(IReadOnlyList<Vector3> source,IReadOnlyList<WalkingMachineBox> machines,out string proof)
        {
            if(source==null||source.Count<3||machines==null)throw new InvalidOperationException("Current walking source inventory is incomplete");
            foreach(var point in source)
                if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||float.IsInfinity(point.z))
                    throw new InvalidOperationException("Current walking source point is nonfinite");
            var distances=new float[source.Count];
            var intervals=new List<(float first,float last)>();var observed=new List<string>();var observedFrames=new SortedSet<string>(StringComparer.Ordinal);
            for(int i=1;i<source.Count;i++)
            {
                float length=Vector3.Distance(source[i-1],source[i]);distances[i]=distances[i-1]+length;
                if(length<1e-8f)continue;
                foreach(var machine in machines)
                    if(machine.Intersects(source[i-1],source[i],out var first,out var last))
                    {
                        intervals.Add((distances[i-1]+first*length,distances[i-1]+last*length));
                        observedFrames.Add(machine.Id+"/inverse="+ProbeMatrix(machine.Inverse)+"/expandedHalf="+ProbePosition(machine.Half));
                        observed.Add(machine.Id+"/sourceSegment="+(i-1)+"/t=["+first.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+
                            last.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"]");
                    }
            }
            float total=distances[distances.Length-1],begin=0,end=total;
            var merged=new List<(float first,float last)>();
            foreach(var interval in intervals.OrderBy(row=>row.first).ThenBy(row=>row.last))
            {
                if(merged.Count==0||interval.first>merged[merged.Count-1].last+1e-5f)merged.Add(interval);
                else{var prior=merged[merged.Count-1];merged[merged.Count-1]=(prior.first,Mathf.Max(prior.last,interval.last));}
            }
            // Only source-qualified contiguous machinery END caps may be removed.
            // A crossing in the public interior cannot become an artificial shortcut.
            if(merged.Count!=0)
            {
                if(merged[0].first<=1e-5f)begin=merged[0].last+.001f;
                if(merged[merged.Count-1].last>=total-1e-5f)end=merged[merged.Count-1].first-.001f;
                foreach(var interval in merged)
                    if(interval.last>begin&&interval.first<end)
                        throw new InvalidOperationException("Current walking source crosses bound machinery in its public interior");
            }
            if(end-begin<.1f)throw new InvalidOperationException("Current walking source has no continuous public interval outside bound machinery");
            var result=new List<Vector3>{WalkingPointAt(source,distances,begin)};
            for(int i=1;i<source.Count-1;i++)if(distances[i]>begin&&distances[i]<end)result.Add(source[i]);
            result.Add(WalkingPointAt(source,distances,end));
            if(Vector3.Distance(result[0],result[result.Count-1])<.1f)
                throw new InvalidOperationException("Current walking public endpoints are not distinct");
            proof="MODEL_CURRENT_PUBLIC_WALK_SOURCE; originalChainage="+total.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                "; retainedChainage=["+begin.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+end.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                "]; completeBoundSourceFrames="+machines.Count+"; sourceBoundArea1PlusControllerRadius="+NativeLandingWalker.Radius+
                "; endcapOutwardMargin=.001; exactSourceFrames={"+string.Join(" | ",observedFrames)+"}; exactSourceIntervals={"+string.Join(" | ",observed)+"}; raw source retained, no machinery traversed as public walking";
            return result.ToArray();
        }

        static bool WalkingActor(Collider collider)=>collider.GetComponentInParent<PersonBody>()!=null;

        internal sealed class CurrentWalkingClearanceFailure : InvalidOperationException
        {
            public readonly bool StaticCurrentSource;
            public CurrentWalkingClearanceFailure(string message,bool staticCurrentSource):base(message)
            {StaticCurrentSource=staticCurrentSource;}
        }

        internal sealed class CurrentWalkingSurfaceRejection : InvalidOperationException
        {
            internal CurrentWalkingSurfaceRejection(string evidence,Exception cause=null):base(evidence,cause) { }
        }

        internal sealed class CurrentWalkingSourceIdentity
        {
            internal readonly int CollectedIndex;
            internal readonly NavMeshBuildSource NativeDescriptor;
            internal readonly MeshCollider Collider;
            internal readonly Mesh SharedMesh;
            internal readonly Matrix4x4 ActualWorldMatrix;
            internal readonly bool Enabled,Active,Trigger,Convex,Actor,HadAttachedRigidbody;
            internal readonly Rigidbody AttachedRigidbody;
            internal CurrentWalkingSourceIdentity(int index,NavMeshBuildSource source,MeshCollider collider)
            {
                CollectedIndex=index;NativeDescriptor=source;Collider=collider;SharedMesh=collider.sharedMesh;
                ActualWorldMatrix=collider.transform.localToWorldMatrix;Enabled=collider.enabled;
                Active=collider.gameObject.activeInHierarchy;Trigger=collider.isTrigger;Convex=collider.convex;Actor=WalkingActor(collider);
                AttachedRigidbody=collider.attachedRigidbody;HadAttachedRigidbody=AttachedRigidbody!=null;
            }
        }

        internal static CurrentWalkingSourceIdentity[] CaptureCurrentWalkingSources(IReadOnlyList<NavMeshBuildSource> sources)
        {
            // Capture the ACTUAL collider state in the same synchronous collect/build
            // transaction. Native shape transforms are a separate representation;
            // no API promises float-equality with Transform.localToWorldMatrix.
            // Null native component metadata cannot acquire a guessed collider owner.
            var result=new List<CurrentWalkingSourceIdentity>();
            for(int i=0;i<sources.Count;i++)if(sources[i].component is MeshCollider collider)
                result.Add(new CurrentWalkingSourceIdentity(i,sources[i],collider));
            return result.ToArray();
        }

        static bool WalkingMatrixFinite(Matrix4x4 matrix)=>Enumerable.Range(0,16).All(i=>
            !float.IsNaN(matrix[i])&&!float.IsInfinity(matrix[i]));

        static bool WalkingNativeDescriptorEligible(CurrentWalkingSourceIdentity source,Collider collider)
        {
            var native=source.NativeDescriptor;
            return source.Collider==collider&&source.SharedMesh!=null&&native.component==collider&&
                native.shape==NavMeshBuildSourceShape.Mesh&&native.sourceObject==source.SharedMesh&&
                WalkingMatrixFinite(native.transform)&&WalkingMatrixFinite(source.ActualWorldMatrix)&&
                !float.IsNaN(native.size.x)&&!float.IsInfinity(native.size.x)&&
                !float.IsNaN(native.size.y)&&!float.IsInfinity(native.size.y)&&
                !float.IsNaN(native.size.z)&&!float.IsInfinity(native.size.z);
        }

        static bool WalkingCapturedStateEligible(CurrentWalkingSourceIdentity source)=>source.Enabled&&source.Active&&
            !source.Trigger&&!source.Convex&&!source.Actor&&!source.HadAttachedRigidbody;

        internal sealed class CurrentWalkingActualIdentity
        {
            internal readonly Collider Collider;
            internal readonly Mesh SharedMesh;
            internal readonly Matrix4x4 ActualWorldMatrix;
            internal readonly Transform[] Ancestors;
            internal readonly bool Enabled,Active,Trigger,Convex,Actor,HadAttachedRigidbody,Excluded;
            internal CurrentWalkingActualIdentity(Collider collider,bool excluded)
            {
                Collider=collider;SharedMesh=(collider as MeshCollider)?.sharedMesh;ActualWorldMatrix=collider.transform.localToWorldMatrix;
                Enabled=collider.enabled;Active=collider.gameObject.activeInHierarchy;Trigger=collider.isTrigger;
                Convex=collider is MeshCollider mesh&&mesh.convex;Actor=WalkingActor(collider);
                HadAttachedRigidbody=collider.attachedRigidbody!=null;Excluded=excluded;
                var ancestors=new List<Transform>();for(var t=collider.transform;t!=null;t=t.parent)ancestors.Add(t);Ancestors=ancestors.ToArray();
            }
        }

        static bool WalkingActualSnapshotEligible(CurrentWalkingActualIdentity source)=>source.Enabled&&source.Active&&!source.Trigger&&
            !source.Convex&&!source.Actor&&!source.HadAttachedRigidbody&&!source.Excluded&&source.SharedMesh!=null&&WalkingMatrixFinite(source.ActualWorldMatrix);

        internal static bool WalkingNativeAffinityEqual(NavMeshBuildSource original,NavMeshBuildSource scoped,Collider target)
        {
            // The ORIGINAL descriptor and every private native field remain intact.
            // Normalize only the optional owner of a COPY of the scoped descriptor.
            if(original.component!=null||scoped.component!=null&&scoped.component!=target)return false;
            scoped.component=null;
            return original.Equals(scoped);
        }

        bool TryNullOwnerWalkingAffinity(Collider collider,out string evidence)
        {
            evidence="NOT_RUN";
            try
            {
                if(collider==null||collider.gameObject.scene!=scene||!(collider is MeshCollider mesh)||mesh.sharedMesh==null||
                    !collider.enabled||!collider.gameObject.activeInHierarchy||collider.isTrigger||mesh.convex||
                    collider.attachedRigidbody!=null||WalkingActor(collider)||StationNavigationRecipe.Excluded(collider,trainSource,namedBeltsSource))
                {evidence="CURRENT_TARGET_NOT_ELIGIBLE; no scoped query";return false;}
                if(capturedActualWalkingSources==null||originalWalkingNativeSources==null||originalWalkingNativeHadOwners==null||
                    originalWalkingNativeHadOwners.Length!=originalWalkingNativeSources.Length)
                {evidence="ORIGINAL_COLLECT_BUILD_SNAPSHOTS_MISSING; no scoped query";return false;}
                if(capturedWalkingSources!=null&&capturedWalkingSources.Any(row=>row.Collider==collider))
                {evidence="ORIGINAL_NATIVE_OWNER_WAS_KNOWN_BUT_DIRECT_CAPTURE_FAILED; no null-owner reinterpretation/scoped query";return false;}
                var own=capturedActualWalkingSources.Where(row=>row.Collider==collider).ToArray();
                if(own.Length!=1||!WalkingActualSnapshotEligible(own[0])||own[0].SharedMesh!=mesh.sharedMesh||
                    !Enumerable.Range(0,16).All(i=>own[0].ActualWorldMatrix[i]==collider.transform.localToWorldMatrix[i]))
                {evidence="ORIGINAL_ACTUAL_TARGET_NOT_UNIQUE_ELIGIBLE_UNCHANGED; snapshotCount="+own.Length+"; no scoped query";return false;}
                var owner=own[0];
                int actualOwners=capturedActualWalkingSources.Count(row=>WalkingActualSnapshotEligible(row)&&row.SharedMesh==owner.SharedMesh&&
                    Enumerable.Range(0,16).All(i=>row.ActualWorldMatrix[i]==owner.ActualWorldMatrix[i]));
                var originalSubtree=capturedActualWalkingSources.Where(row=>row.Enabled&&row.Active&&!row.Trigger&&!row.Excluded&&
                    row.Ancestors.Contains(collider.transform)).ToArray();
                var liveSubtree=collider.GetComponentsInChildren<Collider>(true).Where(row=>row.enabled&&row.gameObject.activeInHierarchy&&
                    !StationNavigationRecipe.Excluded(row,trainSource,namedBeltsSource)).ToArray();
                if(actualOwners!=1||originalSubtree.Length!=1||originalSubtree[0].Collider!=collider||liveSubtree.Length!=1||liveSubtree[0]!=collider)
                {evidence="AMBIGUOUS_ACTUAL_OWNER_OR_SUBTREE; originalEquivalentOwners="+actualOwners+"; originalEligibleSubtree="+
                    originalSubtree.Length+"; liveEligibleSubtree="+liveSubtree.Length+"; no scoped query";return false;}
                var scoped=new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(collider.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),scoped);
                evidence="failure-only exact-target PhysicsColliders native affinity; scopedCount="+scoped.Count+"; originalEquivalentOwners="+actualOwners+
                    "; originalEligibleSubtree="+originalSubtree.Length+"; liveEligibleSubtree="+liveSubtree.Length+"; scoped={"+
                    string.Join(" || ",scoped.Select(row=>"component={"+WalkingComponentIdentity(row.component)+"}; object={"+WalkingObjectIdentity(row.sourceObject)+
                        "}; shape="+row.shape+"; area="+row.area+"; generateLinks="+row.generateLinks+"; size="+ProbePosition(row.size)+"; nativeMatrix="+ProbeMatrix(row.transform)))+"}";
                if(scoped.Count!=1)return false;
                var candidate=scoped[0];
                if(candidate.shape!=NavMeshBuildSourceShape.Mesh||candidate.sourceObject!=owner.SharedMesh||
                    !WalkingMatrixFinite(candidate.transform)||Enumerable.Range(0,3).Any(i=>float.IsNaN(candidate.size[i])||float.IsInfinity(candidate.size[i]))||
                    candidate.component!=null&&candidate.component!=collider)
                {evidence+="; SCOPED_DESCRIPTOR_NOT_ADMISSIBLE";return false;}
                var matches=originalWalkingNativeSources.Select((row,index)=>(row,index)).Where(item=>
                    !originalWalkingNativeHadOwners[item.index]&&item.row.shape==NavMeshBuildSourceShape.Mesh&&item.row.sourceObject==owner.SharedMesh&&
                    WalkingNativeAffinityEqual(item.row,candidate,collider)).ToArray();
                evidence+="; exactOriginalDataDescriptorMatches="+matches.Length+"; originalIndices="+string.Join(",",matches.Select(item=>item.index))+
                    "; exact full native struct equality covers all private flags; only scoped COPY optional component normalized; original never rewritten";
                return matches.Length==1;
            }
            catch(Exception cause) when(!(cause is OutOfMemoryException))
            {evidence+="; API_OR_METADATA_ERROR="+cause.GetType().FullName+":"+cause.Message+"; UNKNOWN_UNQUALIFIED";return false;}
        }

        bool IsCurrentStaticWalkingBlocker(Collider collider)
        {
            if(collider==null||collider.gameObject.scene!=scene||!collider.enabled||!collider.gameObject.activeInHierarchy||
                collider.isTrigger||collider.attachedRigidbody!=null||WalkingActor(collider)||!(collider is MeshCollider mesh)||mesh.sharedMesh==null||mesh.convex)return false;
            var matrix=collider.transform.localToWorldMatrix;
            return collectedSources!=null&&capturedWalkingSources!=null&&capturedWalkingSources.Any(source=>WalkingNativeDescriptorEligible(source,collider)&&
                WalkingCapturedStateEligible(source)&&source.SharedMesh==mesh.sharedMesh&&
                Enumerable.Range(0,16).All(i=>source.ActualWorldMatrix[i]==matrix[i]));
        }

        static string WalkingObjectIdentity(UnityEngine.Object value)=>value==null?"NULL_OR_DESTROYED":
            value.GetType().FullName+"/runLocalInstance="+value.GetInstanceID()+"/name="+value.name;

        static string WalkingComponentIdentity(Component value)=>value==null?"NULL_OR_DESTROYED":
            WalkingObjectIdentity(value)+"/scene="+value.gameObject.scene.path+"/path="+PathOf(value.transform)+
            "/active="+value.gameObject.activeInHierarchy;

        static string WalkingMatrixDifferences(Matrix4x4 source,Matrix4x4 expected)
        {
            var differences=new List<string>();
            for(int i=0;i<16;i++)if(source[i]!=expected[i])
                differences.Add(i+":actual="+source[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                    "/expected="+expected[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                    "/actualBits="+BitConverter.ToString(BitConverter.GetBytes(source[i]))+
                    "/expectedBits="+BitConverter.ToString(BitConverter.GetBytes(expected[i])));
            return differences.Count==0?"NONE_EXACT_FLOAT_EQUAL":string.Join(" | ",differences);
        }

        string DescribeCurrentStaticWalkingBlocker(Collider collider)
        {
            // Failure-only evidence. Native descriptor-vs-actual matrices are
            // observed independently; actual-vs-captured equality stays exact.
            if(collider==null)return "staticSourceCriterion=NULL_COLLIDER; refit remains NOT_RUN";
            var mesh=collider as MeshCollider;var expected=collider.transform.localToWorldMatrix;
            var sources=collectedSources??new List<NavMeshBuildSource>();
            var component=sources.Where(source=>source.component==collider).ToArray();
            var shape=component.Where(source=>source.shape==NavMeshBuildSourceShape.Mesh).ToArray();
            var geometry=shape.Where(source=>mesh!=null&&source.sourceObject==mesh.sharedMesh).ToArray();
            var exact=geometry.Where(source=>Enumerable.Range(0,16).All(i=>source.transform[i]==expected[i])).ToArray();
            var reasons=new List<string>();
            if(collider.gameObject.scene!=scene)reasons.Add("FOREIGN_SCENE");
            if(!collider.enabled)reasons.Add("COLLIDER_DISABLED");
            if(!collider.gameObject.activeInHierarchy)reasons.Add("OBJECT_INACTIVE");
            if(collider.isTrigger)reasons.Add("TRIGGER_COLLIDER");
            if(collider.attachedRigidbody!=null)reasons.Add("ATTACHED_RIGIDBODY");
            if(WalkingActor(collider))reasons.Add("PERSON_BODY_ACTOR");
            if(mesh==null)reasons.Add("NOT_MESH_COLLIDER");
            else{if(mesh.sharedMesh==null)reasons.Add("MISSING_SHARED_MESH");if(mesh.convex)reasons.Add("CONVEX_COLLIDER");}
            if(collectedSources==null)reasons.Add("COLLECTED_SOURCES_NULL");
            if(component.Length==0)reasons.Add("NO_EXACT_COMPONENT_DESCRIPTOR");
            else if(shape.Length==0)reasons.Add("NO_EXACT_COMPONENT_MESH_SHAPE_DESCRIPTOR");
            else if(geometry.Length==0)reasons.Add("NO_EXACT_COMPONENT_SHAPE_SHARED_MESH_DESCRIPTOR");
            var captures=(capturedWalkingSources??Array.Empty<CurrentWalkingSourceIdentity>()).Where(source=>source.Collider==collider).ToArray();
            int eligibleCaptures=captures.Count(source=>WalkingNativeDescriptorEligible(source,collider)&&WalkingCapturedStateEligible(source)&&
                mesh!=null&&source.SharedMesh==mesh.sharedMesh&&Enumerable.Range(0,16).All(i=>source.ActualWorldMatrix[i]==expected[i]));
            if(capturedWalkingSources==null)reasons.Add("COLLECT_BUILD_CAPTURE_NULL");
            if(captures.Length==0)reasons.Add("NO_CAPTURED_EXACT_COLLIDER_IDENTITY");
            else
            {
                if(eligibleCaptures==0)reasons.Add("NO_SINGLE_CAPTURE_SATISFIES_ALL_IDENTITY_STATIC_UNCHANGED_CRITERIA");
                if(!captures.Any(source=>WalkingNativeDescriptorEligible(source,collider)))reasons.Add("CAPTURED_NATIVE_IDENTITY_OR_FINITE_DESCRIPTOR_FAILED");
                if(!captures.Any(WalkingCapturedStateEligible))reasons.Add("CAPTURED_STATIC_FLAGS_FAILED");
                if(!captures.Any(source=>source.SharedMesh==mesh.sharedMesh&&Enumerable.Range(0,16).All(i=>source.ActualWorldMatrix[i]==expected[i])))
                    reasons.Add("ACTUAL_MESH_OR_FULL_MATRIX_CHANGED_SINCE_COLLECT_BUILD");
            }
            var capturedDetails=captures.Select(source=>"collectedIndex="+source.CollectedIndex+"; capturedMesh={"+WalkingObjectIdentity(source.SharedMesh)+
                "}; capturedActualMatrix="+ProbeMatrix(source.ActualWorldMatrix)+"; CURRENT_VS_CAPTURED_ACTUAL_DIFFERENCES={"+
                WalkingMatrixDifferences(expected,source.ActualWorldMatrix)+"}; capturedEnabled="+source.Enabled+"; capturedActive="+source.Active+
                "; capturedTrigger="+source.Trigger+"; capturedConvex="+source.Convex+"; capturedActor="+source.Actor+"; capturedHadAttachedRigidbody="+source.HadAttachedRigidbody+
                "; capturedRigidbody={"+WalkingComponentIdentity(source.AttachedRigidbody)+
                "}; capturedNativeEligible="+WalkingNativeDescriptorEligible(source,collider)+"; capturedStaticEligible="+WalkingCapturedStateEligible(source)+
                "; nativeMatrixFinite="+WalkingMatrixFinite(source.NativeDescriptor.transform)+"; capturedActualMatrixFinite="+WalkingMatrixFinite(source.ActualWorldMatrix)+
                "; capturedNativeComponent={"+WalkingComponentIdentity(source.NativeDescriptor.component)+"}; capturedNativeObject={"+
                WalkingObjectIdentity(source.NativeDescriptor.sourceObject)+"}; capturedNativeShape="+source.NativeDescriptor.shape+
                "; capturedNativeMatrix="+ProbeMatrix(source.NativeDescriptor.transform));
            var related=new List<string>();
            for(int i=0;i<sources.Count;i++)
            {
                var source=sources[i];bool sameComponent=source.component==collider;
                bool sameObject=source.component!=null&&source.component.gameObject==collider.gameObject;
                bool sameMesh=mesh!=null&&mesh.sharedMesh!=null&&source.sourceObject==mesh.sharedMesh;
                bool sameMatrix=Enumerable.Range(0,16).All(index=>source.transform[index]==expected[index]);
                if(!sameComponent&&!sameObject&&!sameMesh&&!sameMatrix)continue;
                related.Add("collectedIndex="+i+"; exactComponent="+sameComponent+"; sameGameObject="+sameObject+
                    "; ORIGINAL_HAD_NATIVE_OWNER="+(originalWalkingNativeHadOwners==null||i>=originalWalkingNativeHadOwners.Length?"UNKNOWN":originalWalkingNativeHadOwners[i].ToString())+
                    "; sameSharedMesh="+sameMesh+"; exactMatrix="+sameMatrix+"; shape="+source.shape+"; area="+source.area+
                    "; component={"+WalkingComponentIdentity(source.component)+"}; sourceObject={"+WalkingObjectIdentity(source.sourceObject)+
                    "}; size="+ProbePosition(source.size)+"; actualMatrix="+ProbeMatrix(source.transform)+
                    "; fullMatrixDifferences={"+WalkingMatrixDifferences(source.transform,expected)+"}");
            }
            return "staticSourceCriteria={"+(reasons.Count==0?"ALL_EXACT_CRITERIA_TRUE":string.Join(" | ",reasons))+
                "}; collectBuildIdentityPredicate="+IsCurrentStaticWalkingBlocker(collider)+"; sameScene="+(collider.gameObject.scene==scene)+
                "; enabled="+collider.enabled+"; active="+collider.gameObject.activeInHierarchy+"; trigger="+collider.isTrigger+"; actor="+WalkingActor(collider)+
                "; attachedRigidbody={"+WalkingComponentIdentity(collider.attachedRigidbody)+"}; expectedSharedMesh={"+
                WalkingObjectIdentity(mesh==null?null:mesh.sharedMesh)+"}; expectedFullMatrix="+ProbeMatrix(expected)+
                "; collectedTotal="+sources.Count+"; exactComponentCount="+component.Length+"; exactComponentMeshShapeCount="+shape.Length+
                "; exactComponentShapeSharedMeshCount="+geometry.Length+"; nativeVsActualExactMatrixCount_DIAGNOSTIC_ONLY="+exact.Length+
                "; collectBuildCaptures="+captures.Length+"; fullyEligibleCaptureCount="+eligibleCaptures+"; captures={"+string.Join(" || ",capturedDetails)+"}"+
                "; ALL related collected descriptors="+related.Count+"; descriptors={"+string.Join(" || ",related)+
                "}; related means exact component OR same GameObject OR same sharedMesh OR exact full matrix; no metadata/epsilon inference grants refit; "+
                "qualification requires native exact component/shape/sharedMesh plus finite build descriptor AND unchanged captured actual collider mesh/fullmatrix/static state";
        }

        string DescribeWalkingFloorHits(Vector3 feet)
        {
            var rows=new List<string>();
            for(int ray=-1;ray<8;ray++)
            {
                var offset=ray<0?Vector3.zero:new Vector3(Mathf.Cos(ray*Mathf.PI/4),0,Mathf.Sin(ray*Mathf.PI/4))*.24f;
                var hits=Physics.RaycastAll(feet+offset+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore)
                    .Where(hit=>!WalkingActor(hit.collider)).OrderBy(hit=>hit.distance).ToArray();
                rows.Add("ray="+ray+"; "+(hits.Length==0?"NO_HIT":ProbeHit(hits[0])));
            }
            return "diagnostic exposed-floor hits at actual capsule feet={"+string.Join(" | ",rows)+"}; no new authority/filter";
        }

        CurrentWalkingClearanceFailure WalkingBodyFailure(string message,Collider[] blockers,Vector3 from,Vector3 to)
        {
            var resolutions=blockers.Select(collider=>
            {
                bool eligible=IsCurrentStaticWalkingBlocker(collider);string affinity="NOT_RUN_DIRECT_EXACT_NATIVE_OWNER_CAPTURE_SUCCEEDED";
                if(!eligible)eligible=TryNullOwnerWalkingAffinity(collider,out affinity);
                return (collider,eligible,affinity);
            }).ToArray();
            bool qualified=resolutions.Length!=0&&resolutions.All(row=>row.eligible);
            var details=resolutions.Take(8).Select(row=>{var collider=row.collider;return ProbeCollider(collider)+"; boundsMin="+ProbePosition(collider.bounds.min)+
                "; boundsMax="+ProbePosition(collider.bounds.max)+"; matrix="+ProbeMatrix(collider.transform.localToWorldMatrix)+
                (collider is MeshCollider mesh?"; mesh="+(mesh.sharedMesh==null?"NULL":mesh.sharedMesh.name)+"; convex="+mesh.convex:
                    "; source shape has no qualified static mesh identity")+"; sourceClassification={"+DescribeCurrentStaticWalkingBlocker(collider)+
                    "}; nullOwnerAffinity={"+row.affinity+"}; finalSourceEligible="+row.eligible;});
            return new CurrentWalkingClearanceFailure(message+"; allBlockingShapes="+blockers.Length+"; allExactStaticCollectedSource="+qualified+
                "; bodyRadius="+NativeLandingWalker.Radius+"; bodyHeight="+NativeLandingWalker.Height+"; bottomMargin=.03; from="+ProbePosition(from)+
                "; to="+ProbePosition(to)+"; blockers={"+string.Join(" | ",details)+"}; omittedBlockers="+Mathf.Max(0,blockers.Length-8)+
                "; overlap/sweep does not identify cooked triangle or penetration depth; nonconvex ClosestPoint not queried; no skin/radius exemption; "+
                DescribeWalkingFloorHits(from),qualified);
        }

        static void WalkingDiscInterval(Vector3 from,Vector3 to,Vector3 centre,double radius,List<(double first,double last)> into)
        {
            double dx=(double)to.x-from.x,dz=(double)to.z-from.z,x=(double)from.x-centre.x,z=(double)from.z-centre.z;
            double a=dx*dx+dz*dz,b=2*(x*dx+z*dz),c=x*x+z*z-radius*radius;
            if(a<=1e-20){if(c<=0)into.Add((0,1));return;}
            double determinant=b*b-4*a*c;if(determinant<0)return;
            double root=Math.Sqrt(determinant),first=Math.Max(0,(-b-root)/(2*a)),last=Math.Min(1,(-b+root)/(2*a));
            if(first<=last)into.Add((first,last));
        }

        static bool WalkingLinearBand(double start,double delta,double low,double high,ref double first,ref double last)
        {
            if(Math.Abs(delta)<=1e-20)return start>=low&&start<=high;
            double a=(low-start)/delta,b=(high-start)/delta;
            if(a>b){double swap=a;a=b;b=swap;}
            first=Math.Max(first,a);last=Math.Min(last,b);return first<=last;
        }

        internal static bool WalkingSegmentWithinSourceCorridor(Vector3 from,Vector3 to,IReadOnlyList<Vector3> source,float reach)
        {
            // Exact interval union of projected source-segment capsules covers the
            // ENTIRE candidate segment, including concave source corners. This is
            // only a negative locality bound, never floor/clearance authority.
            if(source==null||source.Count<2||reach<=1e-5f)return false;
            double radius=(double)reach-1e-5;var intervals=new List<(double first,double last)>();
            for(int i=1;i<source.Count;i++)
            {
                var a=source[i-1];var b=source[i];
                WalkingDiscInterval(from,to,a,radius,intervals);WalkingDiscInterval(from,to,b,radius,intervals);
                double ex=(double)b.x-a.x,ez=(double)b.z-a.z,length=ex*ex+ez*ez;
                if(length<=1e-20)continue;
                double px=(double)from.x-a.x,pz=(double)from.z-a.z,dx=(double)to.x-from.x,dz=(double)to.z-from.z;
                double first=0,last=1,limit=radius*Math.Sqrt(length);
                if(WalkingLinearBand(px*ex+pz*ez,dx*ex+dz*ez,0,length,ref first,ref last)&&
                    WalkingLinearBand(ex*pz-ez*px,ex*dz-ez*dx,-limit,limit,ref first,ref last))intervals.Add((first,last));
            }
            double covered=0;
            foreach(var interval in intervals.OrderBy(row=>row.first).ThenBy(row=>row.last))
            {
                if(interval.first>covered+1e-12)return false;
                covered=Math.Max(covered,interval.last);if(covered>=1)return true;
            }
            return false;
        }

        internal static Vector3[] RequireWalkingRefitGeometry(IReadOnlyList<Vector3> source,IReadOnlyList<Vector3> corners,
            Vector3 start,Vector3 end,out string proof)
        {
            if(source==null||source.Count<2||corners==null||corners.Count<2||corners.Count>=512)
                throw new InvalidOperationException("Current walking refit source/corners are incomplete/truncated");
            foreach(var point in source.Concat(corners).Concat(new[]{start,end}))
                if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||float.IsInfinity(point.z))
                    throw new InvalidOperationException("Current walking refit has unresolved coordinates");
            if(Vector3.Distance(corners[0],start)>.12f||Vector3.Distance(corners[corners.Count-1],end)>.12f)
                throw new InvalidOperationException("Current walking refit changed its strict source-bound anchors");
            float reach=NativeLandingWalker.Radius+.12f,minY=source.Min(point=>point.y)-.35f,maxY=source.Max(point=>point.y)+.35f;
            var current=new List<Vector3>{start};
            foreach(var point in corners)
            {
                if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||float.IsInfinity(point.z)||point.y<minY||point.y>maxY)
                    throw new InvalidOperationException("Current walking refit left its source storey or has unresolved coordinates");
                if((current[current.Count-1]-point).sqrMagnitude>1e-10f)current.Add(point);
            }
            if((current[current.Count-1]-end).sqrMagnitude>1e-10f)current.Add(end);
            if(current.Count==2)current.Insert(1,Vector3.Lerp(start,end,.5f));
            float sourceLength=0,length=0;
            for(int i=1;i<source.Count;i++)sourceLength+=Vector3.Distance(source[i-1],source[i]);
            for(int i=1;i<current.Count;i++)
            {
                if(!WalkingSegmentWithinSourceCorridor(current[i-1],current[i],source,reach))
                    throw new InvalidOperationException("Current walking refit left the complete source-local body/anchor corridor");
                length+=Vector3.Distance(current[i-1],current[i]);
            }
            if(length>sourceLength+2*reach)throw new InvalidOperationException("Current walking refit exceeds its source-tied local length budget");
            proof="currentFineNavSourceLocalRefit; NEGATIVE_MODEL_LOCALITY_AND_LENGTH_CEILINGS_ONLY_NOT_MEASURED_OR_POSITIVE_CORRIDOR; "+
                "caps grant no floor/body/Nav qualification; sameExactAnchors; sourceLength="+sourceLength.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                "; currentLength="+length.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; maxAdditionalLength="+2*reach+
                "; whole-segment XZ distance to retained source <= controllerRadius+.12 ("+reach+"); sourceYBand=["+minY+","+maxY+"]; corners="+current.Count;
            return current.ToArray();
        }

        internal static void RequireNoOpenWalkingOwners(IReadOnlyList<StationWalkingRoute> owners)
        {
            var open=owners.Where(owner=>owner!=null&&!owner.Closed).ToArray();
            if(open.Length!=0)throw new InvalidOperationException("Current walking refit cannot borrow existing owned walking links: "+
                string.Join(" | ",open.Select(owner=>owner.Entry.id+"/instance="+owner.GetInstanceID())));
        }

        static string DescribeWalkingCurve(IReadOnlyList<Vector3> points)=>"count="+(points==null?-1:points.Count)+"; ["+
            (points==null?"NOT_RUN":string.Join(" | ",points.Select(ProbePosition)))+"]";

        Vector3[] CurrentWalkingRefit(IReadOnlyList<Vector3> primary,StationWalkingRouteEntry source,WalkingMachineBox[] machines,
            Func<Collider,bool> currentOwner,out string proof)
        {
            proof="";var start=primary[0];var end=primary[primary.Count-1];
            var forward=new NavMeshPath();var reverse=new NavMeshPath();bool forwardFound=false,reverseFound=false;
            int forwardCount=0,reverseCount=0;Vector3[] forwardCorners=null,reverseCorners=null,candidate=null;
            try
            {
                RequireNoOpenWalkingOwners(UnityEngine.Object.FindObjectsByType<StationWalkingRoute>(FindObjectsInactive.Include,FindObjectsSortMode.None));
                forwardFound=NavMesh.CalculatePath(start,end,NavMesh.AllAreas,forward);
                reverseFound=NavMesh.CalculatePath(end,start,NavMesh.AllAreas,reverse);
                var buffer=new Vector3[512];
                forwardCount=forward.GetCornersNonAlloc(buffer);forwardCorners=buffer.Take(Mathf.Clamp(forwardCount,0,buffer.Length)).ToArray();
                reverseCount=reverse.GetCornersNonAlloc(buffer);reverseCorners=buffer.Take(Mathf.Clamp(reverseCount,0,buffer.Length)).ToArray();
                if(!forwardFound||forward.status!=NavMeshPathStatus.PathComplete||!reverseFound||reverse.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("One current walking refit attempt has no complete bidirectional native path");
                if(forwardCount<2||forwardCount>=buffer.Length||reverseCount<2||reverseCount>=buffer.Length)
                    throw new InvalidOperationException("Current walking refit corners incomplete/truncated");
                candidate=RequireWalkingRefitGeometry(primary,forwardCorners,start,end,out proof);
                var backward=RequireWalkingRefitGeometry(primary,reverseCorners,end,start,out var reverseProof);
                foreach(var path in new[]{candidate,backward})
                {
                    foreach(var corner in path)CurrentWalkingAnchor(corner,source.id+"/current-refit-corner");
                    for(int i=1;i<path.Length;i++)
                        if(NavMesh.Raycast(path[i-1],path[i],out var boundary,NavMesh.AllAreas))
                            throw new CurrentWalkingSurfaceRejection("Current walking refit borrowed off-mesh/transport or crossed a Nav boundary; from="+
                                ProbePosition(path[i-1])+"; to="+ProbePosition(path[i])+"; boundary="+ProbePosition(boundary.position)+"; mask="+boundary.mask);
                }
                // The .28m Nav profile never substitutes for the unchanged .3m body.
                // Validate the SAME chosen curve in both physical traversal directions.
                RequireCurrentPublicWalkingPath(source,candidate,machines,currentOwner,source.id+"/current-refit-forward");
                RequireCurrentPublicWalkingPath(source,candidate.Reverse().ToArray(),machines,currentOwner,source.id+"/current-refit-reverse");
                proof+="; reverseNative={"+reverseProof+"}; no pre-existing walking links; every native segment surface-continuous; complete owner9/capsule/sweep both directions";
                return candidate;
            }
            catch(Exception cause) when(!(cause is OutOfMemoryException))
            {
                var bounds=new Bounds(primary[0],Vector3.zero);foreach(var point in primary)bounds.Encapsulate(point);
                bounds.Expand(new Vector3(2*(NativeLandingWalker.Radius+.12f),.7f,2*(NativeLandingWalker.Radius+.12f)));
                string evidence="Current source-local walking refit failed: "+source.id+
                    "; SAME anchors="+ProbePosition(start)+" -> "+ProbePosition(end)+"; primary={"+DescribeWalkingCurve(primary)+
                    "}; forward="+forwardFound+"/"+forward.status+"; forwardCornerCount="+forwardCount+"; forward={"+DescribeWalkingCurve(forwardCorners)+
                    "}; reverse="+reverseFound+"/"+reverse.status+"; reverseCornerCount="+reverseCount+"; reverse={"+DescribeWalkingCurve(reverseCorners)+
                    "}; candidate={"+DescribeWalkingCurve(candidate)+"}; sourceLocalNegativeBoundsMin="+ProbePosition(bounds.min)+
                    "; sourceLocalNegativeBoundsMax="+ProbePosition(bounds.max)+"; entire-segment source-polyline distance <=.3+.12; "+
                    "sourceYBand=sourceMin/Max+-.35; length<=sourceLength+2*(.3+.12); "+proof+"; finalCause="+cause.Message;
                if(cause is CurrentWalkingSurfaceRejection)throw new CurrentWalkingSurfaceRejection(evidence,cause);
                throw new InvalidOperationException(evidence,cause);
            }
        }

        void RequireCurrentWalkingClearance(Vector3 feet,string identity)
        {
            var bottom=feet+Vector3.up*(NativeLandingWalker.Radius+.03f);
            var top=feet+Vector3.up*(NativeLandingWalker.Height-NativeLandingWalker.Radius);
            var blockers=Physics.OverlapCapsule(bottom,top,NativeLandingWalker.Radius,~0,QueryTriggerInteraction.Ignore)
                .Where(collider=>!WalkingActor(collider)).ToArray();
            if(blockers.Length!=0)throw WalkingBodyFailure("Current public walking capsule is blocked: "+identity,blockers,feet,feet);
        }

        void RequireCurrentWalkingSweep(Vector3 from,Vector3 to,string identity)
        {
            var motion=to-from;float distance=motion.magnitude;
            if(distance<=1e-6f)return;
            var bottom=from+Vector3.up*(NativeLandingWalker.Radius+.03f);
            var top=from+Vector3.up*(NativeLandingWalker.Height-NativeLandingWalker.Radius);
            var blockers=Physics.CapsuleCastAll(bottom,top,NativeLandingWalker.Radius,motion/distance,distance,~0,QueryTriggerInteraction.Ignore)
                .Where(hit=>!WalkingActor(hit.collider)).Select(hit=>hit.collider).Distinct().ToArray();
            // All nonactor solids remain blockers. No skin/radius exemption is used.
            if(blockers.Length!=0)throw WalkingBodyFailure("Current public walking continuous capsule sweep is blocked: "+identity,blockers,from,to);
        }

        void RequireCurrentPublicWalkingPath(StationWalkingRouteEntry source,IReadOnlyList<Vector3> path,WalkingMachineBox[] machines,
            Func<Collider,bool> currentOwner,string identity)
        {
            Vector3? prior=null;
            for(int i=1;i<path.Count;i++)
            {
                foreach(var machine in machines)if(machine.Intersects(path[i-1],path[i],out _,out _))
                    throw new InvalidOperationException("Current public walking segment still crosses bound machinery: "+identity+"; source="+machine.Id);
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(path[i-1],path[i])/NativeLandingWalker.MaxStep));
                for(int step=0;step<=count;step++)
                {
                    var staged=Vector3.Lerp(path[i-1],path[i],step/(float)count);
                    if(!NativeLandingWalker.Floor(staged,source,null,currentOwner,collider=>collider.gameObject.scene==scene,out var height))
                        throw new InvalidOperationException("Current public walking nine-ray floor is absent: "+identity+"; staged="+ProbePosition(staged));
                    var feet=new Vector3(staged.x,height,staged.z);RequireCurrentWalkingClearance(feet,identity);
                    if(prior.HasValue)RequireCurrentWalkingSweep(prior.Value,feet,identity);
                    prior=feet;
                }
            }
        }

        Vector3 CurrentWalkingAnchor(Vector3 source,string identity)
        {
            bool found=NavMesh.SamplePosition(source,out var hit,.12f,NavMesh.AllAreas);
            if(!found||Mathf.Abs(hit.position.y-source.y)>.15f)
                throw new InvalidOperationException("Current public walking endpoint lacks strict Nav attachment: "+identity+"; source="+ProbePosition(source)+
                    "; sampleRadius=.12; maxYDelta=.15; found="+found+(found?"; hit="+ProbePosition(hit.position):""));
            return hit.position;
        }

        internal StationWalkingRouteBinding BindCurrentWalkingRoute(StationWalkingRouteEntry source)
        {
            source.Validate();
            var machines=boundWalkingMachines??throw new InvalidOperationException("Current bound source machinery domains are absent");
            var publicPath=CurrentPublicWalkingPath(source.path,machines,out var trimProof);
            var observedOwners=new HashSet<string>(StringComparer.Ordinal);
            bool CurrentOwner(Collider collider)
            {
                if(collider==null||collider.gameObject.scene!=scene||!newFloorSources.Contains(collider))return false;
                observedOwners.Add(PathOf(collider.transform));return true;
            }
            // Both endpoints are checked before any owned walking link exists.
            // Missing navigation never authorizes a trim or a broader/sideways probe.
            var failures=new List<Exception>();Vector3 start=default,end=default;
            try{start=CurrentWalkingAnchor(publicPath[0],source.id+"/start; "+trimProof);}
            catch(InvalidOperationException cause){failures.Add(cause);}
            try{end=CurrentWalkingAnchor(publicPath[publicPath.Length-1],source.id+"/end; "+trimProof);}
            catch(InvalidOperationException cause){failures.Add(cause);}
            if(failures.Count!=0)throw new InvalidOperationException(string.Join(" | ",failures.Select(cause=>cause.Message)),new AggregateException(failures));
            var current=new List<Vector3>{start};
            foreach(var point in publicPath)if((current[current.Count-1]-point).sqrMagnitude>1e-10f)current.Add(point);
            if((current[current.Count-1]-end).sqrMagnitude>1e-10f)current.Add(end);
            string currentProof="original source-public curve passed unchanged";
            try{RequireCurrentPublicWalkingPath(source,current,machines,CurrentOwner,source.id);}
            catch(CurrentWalkingClearanceFailure original) when(original.StaticCurrentSource)
            {
                // One geometry-qualified correspondence repair; foreign/dynamic,
                // floor/anchor/schema failures never enter this branch. Preserve a
                // passing primary curve and keep the original cause if refit fails.
                observedOwners.Clear();
                try{current=CurrentWalkingRefit(current,source,machines,CurrentOwner,out currentProof).ToList();
                    currentProof+="; source-body-invalid primary={"+original.Message+"}";}
                catch(CurrentWalkingSurfaceRejection rejection)
                {
                    var unavailable=new StationWalkingRouteBinding(source,original,rejection);
                    physicalReport.Add("UNBOUND_SOURCE_OBSTRUCTED_WALKING_CANDIDATE "+source.id+
                        "; complete historical source retained; exact static-current-source body interference; one alternative UNQUALIFIED_NAV_SURFACE_BOUNDARY; "+
                        "no claim that the physical corridor/passage is absent");
                    return unavailable;
                }
                catch(InvalidOperationException refit)
                {throw new InvalidOperationException("Current source walking refit failed: "+source.id+"; original={"+original.Message+"}; oneRefit={"+
                    refit.Message+"}",new AggregateException(original,refit));}
            }
            var bound=source.WithCurrentFloorOwners(observedOwners).WithCurrentPublicPath(current.ToArray(),trimProof+"; "+currentProof+
                "; strict currentNav=.12/.15; complete public owner9-ray/.3m1.72m capsule and continuous sweep; no side probe/floor/link invented");
            physicalReport.Add("CURRENT_WALKING_ROUTE_SOURCE "+source.id+"; original points="+source.path.Length+"; current public points="+bound.path.Length+
                "; "+bound.CurrentSourceProof+"; observed approved current floor owners="+string.Join("|",observedOwners.OrderBy(owner=>owner,StringComparer.Ordinal)));
            return new StationWalkingRouteBinding(source,bound);
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

        static List<Vector3> CurrentPointProposals(StationPoints.Point source,BakedFloor reference)
        {
            var candidates=new List<Vector3>{source.Position};
            if(reference.Sample(source.Position,2f,out var prior)&&Mathf.Abs(prior.y-source.Position.y)<.35f)candidates.Add(prior);
            for(float radius=.25f;radius<=2.001f;radius+=.25f)
                for(int step=0;step<16;step++)candidates.Add(source.Position+Quaternion.Euler(0,step*22.5f,0)*Vector3.forward*radius);
            return candidates;
        }

        // Called ONLY after the selected standing pose exhausts certified seeds.
        // The complete bounded batch is materialized while the historical reference
        // is owned here. Its lease is disposed BEFORE any caller connectivity query.
        internal StationPoints.CurrentRequalificationBatch RequalifyCurrentStandingPoint(StationPoints.CurrentBinding binding,NavMeshData originalReference)
        {
            if(binding==null||!binding.Bound||binding.Source.Kind==PointKind.Exit||binding.Status!="BOUND_MODEL_COMPUTED_WALKABLE")
                throw new InvalidOperationException("Only an existing bound non-exit standing point can be requalified");
            Physics.SyncTransforms();RequireCurrentSourceQueryReady();
            var alternatives=new List<StationPoints.CurrentRequalification>();var outcomes=new List<string>();
            var probes=new PointProbeReport();var seen=new HashSet<Vector3>{binding.Runtime.Position};
            int count;
            using(var reference=new BakedFloor(originalReference))
            {
                var candidates=binding.AuthorizedProposals;
                if(candidates==null||(candidates.Count!=129&&candidates.Count!=130)||!candidates[0].Equals(binding.Source.Position))
                    throw new InvalidOperationException("Original authorized standing proposals were not captured");
                count=candidates.Count;
                for(int index=0;index<candidates.Count;index++)
                {
                    var proposal=candidates[index];probes.Candidates++;
                    string identity=index+":"+ProbePosition(proposal);
                    if(!Footprint(proposal,reference,out var supported,out string proof,probes,"candidate"))
                    {outcomes.Add(identity+":physical-rejected");continue;}
                    bool sampled=NavMesh.SamplePosition(supported,out var hit,.6f,NavMesh.AllAreas);
                    if(!sampled){outcomes.Add(identity+":nav-sample-missing");continue;}
                    if(Mathf.Abs(hit.position.y-supported.y)>.35f){outcomes.Add(identity+":nav-same-storey-rejected");continue;}
                    if(!Footprint(hit.position,reference,out var corroborated,out string navProof,probes,"nav"))
                    {outcomes.Add(identity+":nav-physical-rejected");continue;}
                    if(Vector3.Distance(corroborated,hit.position)>.35f){outcomes.Add(identity+":nav-corroboration-distance-rejected");continue;}
                    if(!seen.Add(hit.position)){outcomes.Add(identity+":already-exhausted-identical-pose="+ProbePosition(hit.position));continue;}
                    outcomes.Add(identity+":qualified="+ProbePosition(hit.position));
                    alternatives.Add(new StationPoints.CurrentRequalification{Binding=binding,ExpectedSource=binding.Source,ExpectedRuntime=binding.Runtime,
                        ExpectedSourceMetadata=StationPoints.CaptureCurrentPointMetadata(binding.Source),ExpectedRuntimeMetadata=StationPoints.CaptureCurrentPointMetadata(binding.Runtime),
                        ExpectedProposals=binding.AuthorizedProposals,ExpectedId=binding.Source.Id,ExpectedStatus=binding.Status,ExpectedEvidence=binding.Evidence,
                        ExpectedPosition=binding.Runtime.Position,ExpectedSourcePosition=binding.Source.Position,Position=hit.position,Proposal=proposal,ProposalIndex=index,
                        Evidence=binding.Evidence+"; REQUALIFIED_CURRENT_STANDING_POSE; exactOriginalProposalIndex="+index+"; proposal="+ProbePosition(proposal)+
                            "; previousRuntime="+ProbePosition(binding.Runtime.Position)+"; currentRuntime="+ProbePosition(hit.position)+
                            "; unchangedRawSource="+ProbePosition(binding.Source.Position)+"; physical="+proof+"; navCorroboration="+navProof});
                }
            }
            return new StationPoints.CurrentRequalificationBatch{Alternatives=alternatives.AsReadOnly(),Audit="authorizedProposalCount="+count+
                "; uniqueNewQualifiedPoses="+alternatives.Count+"; ALLproposalOutcomes={"+string.Join(" | ",outcomes)+"}; "+probes.Describe()};
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
                var candidates=CurrentPointProposals(source,reference);
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
                result.Add(new StationPoints.CurrentBinding{Source=source,Runtime=runtime,Status=status,AuthorizedProposals=Array.AsReadOnly(candidates.ToArray()),
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
            supports.Clear();collectedSources=null;capturedWalkingSources=null;capturedActualWalkingSources=null;originalWalkingNativeSources=null;originalWalkingNativeHadOwners=null;boundWalkingMachines=null;
        }
    }
    internal sealed class CurrentStationNavigationLifetime : MonoBehaviour
    {
        internal CurrentStationNavigation Navigation;
        void OnDestroy(){Navigation?.RetireSourceScene();Navigation=null;}
    }
}
