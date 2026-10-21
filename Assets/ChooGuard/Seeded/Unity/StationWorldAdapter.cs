using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    public sealed class StationWorldAdapter : MonoBehaviour, IDisposable
    {
        public GameObject SnapshotPrefab;
        // Scene references are wired by the editor installer; never clone or reparent live geometry.
        public GameObject[] LiveObjects = Array.Empty<GameObject>();
        public string[] LiveObjectIds = Array.Empty<string>();
        public float[] LiveFloorLevels = new float[] { 0f, 7f };
        public bool LiveMode => LiveObjects != null && LiveObjects.Length > 0;
        readonly List<SnapshotObjectIdentity> liveIdentities = new List<SnapshotObjectIdentity>();
        IEnumerable<SnapshotObjectIdentity> SourceIdentities => LiveMode
            ? liveIdentities : instance.GetComponentsInChildren<SnapshotObjectIdentity>(true);
        public TextAsset SnapshotInventory, SnapshotManifest;
        public WorldObjectRegistry Registry { get; private set; }
        public JObject Report { get; private set; }=new JObject();
        public string Fingerprint => Registry==null?"":Stable.Hash(Registry.Fingerprint+"|"+manifestHash+"|"+NavigationHash);
        public string NavigationHash { get; private set; }="absent";
        public bool Prepared => instance!=null;
        public bool Activated { get; private set; }
        bool navigationAttempted;
        public GameObject TrainVisual { get; private set; }
        public GameObject OfficialFrame { get; private set; }
        public GameObject[] PlatformSourceRoots { get; private set; }=Array.Empty<GameObject>();
        public IReadOnlyDictionary<string,Transform> Objects => objects;
        readonly Dictionary<string,Transform> objects=new Dictionary<string,Transform>(StringComparer.Ordinal);
        readonly Dictionary<Transform,string> physicalIds=new Dictionary<Transform,string>();
        readonly List<GameObject> supports=new List<GameObject>();
        readonly HashSet<GameObject> supportSet=new HashSet<GameObject>();
        readonly List<(string id,Vector3 feet,string floor,int triangle,bool support,string collider)> places=new List<(string,Vector3,string,int,bool,string)>();
        readonly HashSet<string> floorIds=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> regionIds=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> renderOnlyAlternatives=new HashSet<string>(StringComparer.Ordinal);
        GameObject instance;
        string manifestHash="",geometrySceneHash="";
        NavMeshData stationNavigation;
        NavMeshDataInstance stationNavigationInstance;
        Bounds qualifiedNavigationBounds;
        bool hasQualifiedNavigationBounds;

        // The current saved snapshot owns all station/platform/KTX geometry. No raw
        // platform or train prefab is instantiated alongside it.
        public void Prepare(Transform owner)
        {
            if(Prepared)return;
            if((!LiveMode&&SnapshotPrefab==null)||SnapshotInventory==null||SnapshotManifest==null)
                throw new InvalidOperationException("Full world snapshot prefab/inventory/manifest are required");
            if(!MatrixNear(owner.localToWorldMatrix,Matrix4x4.identity,1e-6f))
                throw new InvalidOperationException("Snapshot requires the approved identity world frame");
            var manifest=JObject.Parse(SnapshotManifest.text);
            if((string)manifest["schema"]!="chooguard-geometry-prefab.v1")throw new InvalidOperationException("Unsupported snapshot prefab manifest");
            manifestHash=Stable.Hash(SnapshotManifest.text);
            geometrySceneHash=(string)manifest["geometrySceneSha256"];
            if(geometrySceneHash==null||geometrySceneHash.Length!=64||geometrySceneHash.Any(c=>!(c>='0'&&c<='9')&&!(c>='a'&&c<='f')))
                throw new InvalidOperationException("Frozen geometry scene SHA256 required");
            Registry=WorldObjectRegistry.Parse(SnapshotInventory.text,(string)manifest["inventorySha256"],(string)manifest["sourceSceneSha256"],(string)manifest["boundSceneSha256"]);
            renderOnlyAlternatives.Clear();
            foreach(var token in manifest["renderOnlyAlternativeLodObjectIds"]??new JArray())
            {
                string id=(string)token;
                if(!Registry.Objects.ContainsKey(id)||!renderOnlyAlternatives.Add(id)||
                   (bool?)Registry.Objects[id].GeometryProvenance["renderOnlyAlternativeLod"]!=true)
                    throw new InvalidOperationException("Unqualified alternative LOD identity: "+id);
            }
            if (LiveMode)
            {
                var current=ResolveLiveReferences(manifest);
                string trainId = (string)manifest["runtimeRoots"]["ktx"];
                var platformIds = manifest["runtimeRoots"]["platforms"].Values<string>().ToArray();
                foreach(var source in Registry.Objects.Values)
                {
                    string id=source.Id;var go=current[id];
                    if (go == null || !Registry.Objects.TryGetValue(id, out var record) || objects.ContainsKey(id))
                        throw new InvalidOperationException("Missing current scene object: " + id);
                    // Runtime records reflect live geometry; stale saved correspondences must not be silently reused.
                    var matrix = record.WorldMatrix4x4.Select(x => (float)x).ToArray();
                    if (!MatrixNear(go.transform.localToWorldMatrix, Matrix(matrix), .002f))
                        throw new InvalidOperationException("Current scene transform differs from installed inventory: " + record.Path);
                    var identity = go.AddComponent<SnapshotObjectIdentity>();
                    identity.ObjectId=id; identity.SceneFileId=record.SceneFileId; identity.SourcePath=record.Path;
                    identity.SourceActiveSelf=go.activeSelf; identity.SourceActiveInHierarchy=go.activeInHierarchy;
                    identity.SourcePhysicalEligible=record.PhysicalEligible; identity.SourceWorldMatrix=matrix;
                    var sourceFilter=go.GetComponent<MeshFilter>();
                    identity.SourceMesh=sourceFilter!=null?sourceFilter.sharedMesh:null;
                    var renderer=go.GetComponent<MeshRenderer>(); identity.SourceHasRenderer=renderer!=null;
                    identity.SourceMaterials=renderer!=null?renderer.sharedMaterials:Array.Empty<Material>();
                    identity.SourceColliders=go.GetComponents<Collider>();
                    identity.SourceColliderFileIds=record.Components.Where(c=>c.Type.EndsWith("Collider",StringComparison.Ordinal)||c.Type=="CharacterController").Select(c=>c.FileId).ToArray();
                    identity.GeometryDomain=record.Path==Registry.Objects[trainId].Path||record.Path.StartsWith(Registry.Objects[trainId].Path+"/",StringComparison.Ordinal)?"ktx":
                        platformIds.Any(pid=>record.Path==Registry.Objects[pid].Path||record.Path.StartsWith(Registry.Objects[pid].Path+"/",StringComparison.Ordinal))?"platform":"station";
                    liveIdentities.Add(identity);objects.Add(id,go.transform);physicalIds.Add(go.transform,id);
                }
                TrainVisual=objects[trainId].gameObject;
                PlatformSourceRoots=platformIds.Select(id=>objects[id].gameObject).ToArray();
                OfficialFrame=PlatformSourceRoots[0].transform.parent.gameObject;
                foreach(var id in manifest["runtimeRoots"]["stationFloors"]??new JArray())floorIds.Add((string)id);
                foreach(var id in manifest["runtimeRoots"]["stationRegions"]??new JArray())regionIds.Add((string)id);
                instance=LiveObjects[0];Activated=true;
                Report=new JObject{["source_scene_sha256"]=Registry.SceneSha256,["objects"]=objects.Count,["live_current_scene"]=true,["raw_prefab_instances"]=0,["claim"]="Current scene references; no source geometry clone, replacement or transform change."};
                return;
            }
            var parent=new GameObject("Current saved station geometry snapshot");parent.SetActive(false);parent.transform.SetParent(owner,false);
            try
            {
                instance=Instantiate(SnapshotPrefab,parent.transform,false);
                var identities=instance.GetComponentsInChildren<SnapshotObjectIdentity>(true);
                if(identities.Length!=Registry.Objects.Count)throw new InvalidOperationException("Snapshot prefab object coverage differs from source inventory");
                foreach(var identity in identities.OrderBy(x=>x.ObjectId,StringComparer.Ordinal))
                {
                    if(!Registry.Objects.ContainsKey(identity.ObjectId)||objects.ContainsKey(identity.ObjectId))
                        throw new InvalidOperationException("Snapshot contains unknown or repeated source ID: "+identity.ObjectId);
                    var record=Registry.Objects[identity.ObjectId];
                    if(identity.SceneFileId!=record.SceneFileId||identity.gameObject.activeSelf!=record.ActiveSelf)
                        throw new InvalidOperationException("Snapshot object identity/activity changed: "+identity.ObjectId);
                    if(!MatrixNear(identity.transform.localToWorldMatrix,Matrix(identity.SourceWorldMatrix),.0002f))
                        throw new InvalidOperationException("Snapshot world frame changed: "+identity.ObjectId);
                    objects.Add(identity.ObjectId,identity.transform);physicalIds.Add(identity.transform,identity.ObjectId);
                }
                TrainVisual=objects[(string)manifest["runtimeRoots"]["ktx"]].gameObject;
                PlatformSourceRoots=manifest["runtimeRoots"]["platforms"].Select(id=>objects[(string)id].gameObject).ToArray();
                if(PlatformSourceRoots.Length!=2||PlatformSourceRoots[0].transform.parent!=PlatformSourceRoots[1].transform.parent)
                    throw new InvalidOperationException("Snapshot platform roots differ from qualified source hierarchy");
                OfficialFrame=PlatformSourceRoots[0].transform.parent.gameObject;
                foreach(var id in manifest["runtimeRoots"]["stationFloors"]??new JArray())floorIds.Add((string)id);
                foreach(var id in manifest["runtimeRoots"]["stationRegions"]??new JArray())regionIds.Add((string)id);
                Report=new JObject{["source_scene_sha256"]=Registry.SceneSha256,["geometry_scene_sha256"]=geometrySceneHash,["prefab_manifest_sha256"]=manifestHash,["snapshot_sha256"]=Registry.SnapshotSha256,["objects"]=objects.Count,["source_train_meshes"]=TrainVisual.GetComponentsInChildren<MeshFilter>(true).Length,["source_platform_meshes"]=PlatformSourceRoots.Sum(g=>g.GetComponentsInChildren<MeshFilter>(true).Length),["raw_prefab_instances"]=0,["legacy_behaviours"]=0,["source_world_frame"]="identity; exact saved local hierarchy",["inventory_claim"]="Every saved object is represented; capabilities require explicit evidence."};
            }
            catch
            {
                DestroyOwned(parent);instance=null;objects.Clear();physicalIds.Clear();throw;
            }
        }

        Dictionary<string,GameObject> ResolveLiveReferences(JObject manifest)
        {
            if(LiveObjects.Length!=LiveObjectIds.Length)throw new InvalidOperationException("Current root reference arrays differ");
            var result=new Dictionary<string,GameObject>(StringComparer.Ordinal);
            string schema=(string)manifest["liveBindingSchema"];
            if(schema==null)
            {
                if(LiveObjects.Length!=Registry.Objects.Count)throw new InvalidOperationException("Current scene reference coverage differs from inventory");
                for(int i=0;i<LiveObjects.Length;i++)
                {
                    if(LiveObjects[i]==null||result.ContainsKey(LiveObjectIds[i]))throw new InvalidOperationException("Missing/repeated current scene object: "+LiveObjectIds[i]);
                    result.Add(LiveObjectIds[i],LiveObjects[i]);
                }
            }
            else
            {
                if(schema!="chooguard.live-root-child-indices.v1")throw new InvalidOperationException("Unsupported current scene correspondence");
#if UNITY_EDITOR
                // Scene metadata and its external manifest have separate hashes; the manifest is
                // not embedded into Scene bytes, so this check has no self-hash cycle.
                if(Registry.BoundSceneSha256==null)throw new InvalidOperationException("Bound current Scene hash is required");
                string savedPath=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"..",Registry.ScenePath));
                using(var stream=System.IO.File.OpenRead(savedPath))
                using(var hash=System.Security.Cryptography.SHA256.Create())
                    if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=Registry.BoundSceneSha256)
                        throw new InvalidOperationException("Current saved Scene differs from pinned runtime correspondence");
#endif
                var anchors=new Dictionary<string,GameObject>(StringComparer.Ordinal);
                for(int i=0;i<LiveObjects.Length;i++)
                {
                    if(LiveObjects[i]==null||LiveObjects[i].transform.parent!=null||anchors.ContainsKey(LiveObjectIds[i]))
                        throw new InvalidOperationException("Missing/repeated current root anchor");
                    anchors.Add(LiveObjectIds[i],LiveObjects[i]);
                }
                var expected=manifest["liveBindingRoots"].Values<string>().ToArray();
                if(expected.Length!=anchors.Count||expected.Distinct(StringComparer.Ordinal).Count()!=expected.Length||expected.Any(id=>!anchors.ContainsKey(id)))
                    throw new InvalidOperationException("Current root anchor coverage differs");
                var seen=new HashSet<GameObject>();
                foreach(var record in Registry.Objects.Values)
                {
                    var binding=record.LiveBinding;
                    if(binding==null||!anchors.TryGetValue((string)binding["rootObjectId"],out var anchor)||!(binding["childIndices"] is JArray indices))
                        throw new InvalidOperationException("Missing native correspondence: "+record.Id);
                    Transform node=anchor.transform;
                    foreach(var token in indices)
                    {
                        if(token.Type!=JTokenType.Integer||(long)token<0||(long)token>=node.childCount)
                            throw new InvalidOperationException("Native child correspondence changed: "+record.Id);
                        node=node.GetChild((int)token);
                    }
                    var go=node.gameObject;
                    if(!seen.Add(go)||go.name!=record.Name||go.layer!=record.Layer||go.tag!=record.Tag||
                       go.activeSelf!=record.ActiveSelf||go.activeInHierarchy!=record.ActiveInHierarchy||
                       !MatrixNear(go.transform.localToWorldMatrix,Matrix(record.WorldMatrix4x4.Select(value=>(float)value).ToArray()),.002f))
                        throw new InvalidOperationException("Current saved identity/pose differs: "+record.Id);
                    result.Add(record.Id,go);
                }
                foreach(var record in Registry.Objects.Values)
                    if(record.ParentId!=null&&result[record.Id].transform.parent!=result[record.ParentId].transform)
                        throw new InvalidOperationException("Current native parent differs: "+record.Id);
            }
            if(result.Count!=Registry.Objects.Count)throw new InvalidOperationException("Incomplete current source correspondence");
            return result;
        }

        public void ActivateGeometry()
        {
            if(!Prepared)throw new InvalidOperationException("Prepare source geometry first");
            if(!LiveMode)instance.transform.parent.gameObject.SetActive(true);Activated=true;Physics.SyncTransforms();
        }

        public WorldObjectApplyResult Apply(World world)
        {
            if(!Prepared)throw new InvalidOperationException("Prepare source geometry before applying its inventory");
            var geometryWords=new Dictionary<string,double>();
            foreach(var pair in new[]{new[]{"source.geometry.sha256",geometrySceneHash},new[]{"source.geometry.prefab-manifest.sha256",manifestHash}})
                for(int word=0;word<8;word++)geometryWords.Add(pair[0]+".word"+word,uint.Parse(pair[1].Substring(word*8,8),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture));
            foreach(var pair in geometryWords)if(world.Fields.TryGetValue(pair.Key,out var value)&&value!=pair.Value)throw new InvalidOperationException("World belongs to another frozen geometry snapshot");
            var bindings=new List<WorldObjectBinding>();
            var declared=new HashSet<string>(Registry.SourceBindings.Select(b=>b.ObjectId),StringComparer.Ordinal);
            foreach(var pair in objects.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                if(declared.Contains(pair.Key))continue;
                var binding=pair.Value.GetComponent<SemanticBinding>();
                if(binding!=null&&world.Entities.ContainsKey(binding.EntityId))
                    bindings.Add(new WorldObjectBinding(pair.Key,binding.EntityId,"alias","WorldSnapshotPrefabManifest.json",manifestHash,"same-current-source-transform"));
            }
            var result=Registry.Apply(world,bindings);
            foreach(var pair in geometryWords)world.Fields[pair.Key]=pair.Value;
            foreach(var place in places)
            {
                if(world.Entities.ContainsKey(place.id))continue;
                var entity=new Entity{Id=place.id,ParentId=place.floor,Kind="station-walk-place",X=place.feet.x,Y=place.feet.y,Z=place.feet.z};
                foreach(var tag in new[]{"place","walkable","source-qualified-floor","training-model"})entity.Tags.Add(tag);
                entity.Set("navigation.qualified",1);world.Add(entity);
            }
            Report["registered_world_digest"]=world.Digest();
            Report["unsupported_capabilities"]=new JArray(Registry.UnsupportedCapabilities);
            return result;
        }

        // Source colliders retain their saved enabled/trigger flags. Missing solid
        // collision is a separate, reported simulation support made from that exact mesh.
        public void BakeStationNavigation()
        {
            if(!Prepared)throw new InvalidOperationException("Prepare source geometry first");
            if(navigationAttempted)return;navigationAttempted=true;
            ActivateGeometry();
            int sourceSolid=0,added=0;var unreadable=new JArray();var zeroSurfaceMeshes=new JArray();var zeroSurfaceNavInputs=new JArray();
            var sources=new List<NavMeshBuildSource>();bool hasBounds=false,hasSavedSolidBounds=false;var bounds=new Bounds();var savedSolidBounds=new Bounds();
            var savedSolids=new List<Collider>();var savedDomainCounts=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var identity in SourceIdentities.OrderBy(x=>x.ObjectId,StringComparer.Ordinal))
            {
                if(!identity.SourceActiveInHierarchy||(!LiveMode&&identity.GeometryDomain!="station"))continue;
                var go=identity.gameObject;
                var colliders=(LiveMode?identity.SourceColliders:go.GetComponents<Collider>()).Where(c=>c!=null&&c.enabled&&!c.isTrigger&&!(c is CharacterController)).ToArray();
                sourceSolid+=colliders.Length;
                savedSolids.AddRange(colliders);
                savedDomainCounts.TryGetValue(identity.GeometryDomain,out var previousCount);savedDomainCounts[identity.GeometryDomain]=previousCount+colliders.Length;
                foreach(var collider in colliders)
                {if(!hasSavedSolidBounds){savedSolidBounds=collider.bounds;hasSavedSolidBounds=true;}else savedSolidBounds.Encapsulate(collider.bounds);}
                var filter=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<MeshRenderer>();
                if(filter!=null&&filter.sharedMesh!=null&&renderer!=null&&renderer.enabled)
                {
                    var mesh=filter.sharedMesh;
                    var indexCounts=SourceIndexCounts(mesh);bool hasSurface=HasIndexedSourceGeometry(mesh.vertexCount,indexCounts);
                    if(!hasSurface)zeroSurfaceMeshes.Add(new JObject{["object_id"]=identity.ObjectId,["source_path"]=identity.SourcePath,["mesh_name"]=mesh.name,["mesh_filter_file_id"]=Registry.Objects[identity.ObjectId].Components.Single(c=>c.Type=="MeshFilter").FileId,["vertex_count"]=mesh.vertexCount,["submesh_index_counts"]=new JArray(indexCounts),["saved_solid_colliders"]=colliders.Length,["source_reference_preserved"]=mesh==identity.SourceMesh,["qualification"]="Zero indexed primitives provide no source surface; inventory/render reference retained, no collision surface invented."});
                    if(!mesh.isReadable)unreadable.Add(identity.ObjectId);
                    if(mesh.isReadable&&hasSurface&&colliders.Length==0&&!renderOnlyAlternatives.Contains(identity.ObjectId))
                    {
                        var support=new GameObject("Exact source-mesh collision support");support.transform.SetParent(go.transform,false);
                        var collider=support.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;
                        supports.Add(support);supportSet.Add(support);physicalIds[support.transform]=identity.ObjectId;added++;
                    }
                    if(!hasBounds){bounds=renderer.bounds;hasBounds=true;}else bounds.Encapsulate(renderer.bounds);
                }
                foreach(var collider in colliders){if(!hasBounds){bounds=collider.bounds;hasBounds=true;}else bounds.Encapsulate(collider.bounds);}
            }
            Physics.SyncTransforms();
            if(LiveMode)
            {
                foreach(var root in LiveObjects.Where(g=>g!=null&&g.transform.parent==null))
                {
                    var collected=new List<NavMeshBuildSource>();
                    NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),collected);
                    sources.AddRange(collected);
                }
            }
            else NavMeshBuilder.CollectSources(instance.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>!(s.component is Collider c)||c.isTrigger||c is CharacterController||renderOnlyAlternatives.Contains(ResolveSourceObject(c))||(!LiveMode&&c.GetComponentInParent<SnapshotObjectIdentity>()?.GeometryDomain!="station"));
            // A zero-primitive source has no navigation surface. This also handles any saved
            // empty MeshCollider without deleting or modifying its native component/reference.
            for(int i=sources.Count-1;i>=0;i--)
            {
                var source=sources[i];if(source.shape!=NavMeshBuildSourceShape.Mesh)continue;
                if(!(source.sourceObject is Mesh mesh))throw new InvalidOperationException("Collected mesh navigation source has no source Mesh");
                var indexCounts=SourceIndexCounts(mesh);if(HasIndexedSourceGeometry(mesh.vertexCount,indexCounts))continue;
                zeroSurfaceNavInputs.Add(new JObject{["object_id"]=ResolveSourceObject(source.component),["mesh_name"]=mesh.name,["vertex_count"]=mesh.vertexCount,["submesh_index_counts"]=new JArray(indexCounts),["qualification"]="No indexed primitive exists; native collider/reference remains unchanged."});
                sources.RemoveAt(i);
            }
            if(!hasBounds||sources.Count==0)throw new InvalidOperationException("No readable active station source geometry for navigation");
            if(LiveMode&&!hasSavedSolidBounds)throw new InvalidOperationException("No active saved solid source colliders qualify a current navigation extent");
            bool hasRegion=false;var regionBounds=new Bounds();float firstFloorLevel=0;
            foreach(var id in regionIds.OrderBy(x=>x,StringComparer.Ordinal))
            {
                var region=objects[id];firstFloorLevel=region.position.y;
                foreach(var renderer in region.GetComponentsInChildren<Renderer>(false))
                {if(!hasRegion){regionBounds=renderer.bounds;hasRegion=true;}else regionBounds.Encapsulate(renderer.bounds);}
            }
            var allSourceBounds=bounds;
            if(LiveMode){bounds=savedSolidBounds;bounds.Expand(new Vector3(2,1,2));}
            else if(hasRegion)bounds=new Bounds(new Vector3(regionBounds.center.x,firstFloorLevel+1.35f,regionBounds.center.z),new Vector3(regionBounds.size.x+2,3.3f,regionBounds.size.z+2));
            else bounds.Expand(new Vector3(2,1,2));
            if(LiveMode)
            {
                foreach(var collider in savedSolids)
                    if(!ContainsBounds(bounds,collider.bounds,.002f))throw new InvalidOperationException("Qualified saved source collider falls outside navigation build bounds");
                foreach(var domain in new[]{"station","platform","ktx"})
                    if(!savedDomainCounts.TryGetValue(domain,out var count)||count==0)throw new InvalidOperationException("Required current saved physical domain absent: "+domain);
                foreach(var id in floorIds)
                {
                    if(!objects.TryGetValue(id,out var floor))throw new InvalidOperationException("Required current floor identity absent: "+id);
                    var identity=floor.GetComponent<SnapshotObjectIdentity>();
                    if(identity==null||!identity.SourceActiveInHierarchy||!identity.SourceColliders.Any(c=>c!=null&&c.enabled&&!c.isTrigger&&!(c is CharacterController)&&ContainsBounds(bounds,c.bounds,.002f)))
                        throw new InvalidOperationException("Required current floor has no qualified saved solid support: "+id);
                }
            }
            qualifiedNavigationBounds=bounds;hasQualifiedNavigationBounds=LiveMode;
            var outsideSupportIds=new JArray(supports.Where(s=>!ContainsBounds(bounds,s.GetComponent<Collider>().bounds,.002f)).Select(s=>ResolveSourceObject(s.GetComponent<Collider>())).OrderBy(id=>id,StringComparer.Ordinal));
            var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.28f;settings.agentHeight=1.72f;settings.agentClimb=.28f;settings.agentSlope=36;
            settings.overrideVoxelSize=true;settings.voxelSize=.10f;settings.overrideTileSize=true;settings.tileSize=256;settings.minRegionArea=.5f;
            stationNavigation=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(stationNavigation==null)throw new InvalidOperationException("Station source geometry NavMesh build failed");
            stationNavigationInstance=NavMesh.AddNavMeshData(stationNavigation);
            if(!stationNavigationInstance.valid)throw new InvalidOperationException("Station NavMesh registration failed");
            NavigationHash=Stable.Hash(Registry.Fingerprint+(LiveMode?"|station-saved-solid-extent-indexed-nav-v3|":"|station-geometry-indexed-nav-v2|")+string.Join("|",sources.Select(s=>ResolveSourceObject(s.component)).OrderBy(x=>x,StringComparer.Ordinal)));
            Activated=true;
            Report["navigation"]=new JObject{["source_geometry_bounds"]=BoundsJson(bounds),["all_source_geometry_bounds"]=BoundsJson(allSourceBounds),["saved_solid_source_bounds"]=hasSavedSolidBounds?BoundsJson(savedSolidBounds):null,["saved_solid_domains"]=JObject.FromObject(savedDomainCounts),["required_current_floor_supports"]=floorIds.Count,["candidate_region_ids"]=new JArray(regionIds),["first_floor_candidate_level"]=firstFloorLevel,["mesh_sources"]=sources.Count,["saved_solid_colliders"]=sourceSolid,["explicit_mesh_collision_supports"]=added,["unreadable_mesh_objects"]=unreadable,["zero_surface_source_meshes"]=zeroSurfaceMeshes,["zero_surface_collected_nav_inputs"]=zeroSurfaceNavInputs,["runtime_supports_outside_qualified_extent"]=outsideSupportIds,["render_only_alternative_lod_excluded"]=new JArray(renderOnlyAlternatives),["live_bounds_scope"]="Union of all active saved solid colliders across station/platform/train; rendered-only supports do not enlarge qualified navigation extent",["outside_extent_claim"]="All source geometry and exact-mesh collision supports are retained. Exterior without saved physical support is unqualified for navigation; source presence is not walking-floor proof.",["hash"]=NavigationHash,["existing_platform_car04_navigation_preserved"]=true,["coverage_claim"]="Current source collider navigation inside the saved physical extent; disconnected islands remain disconnected. No floor or cross-floor connector is invented."};
            QualifyStationPlaces();
        }

        public bool VerifyCar04SourceContract(string accessJson,out string reason)
        {
            var rows=new JArray();var config=JObject.Parse(accessJson);var car=TrainVisual.transform.Find("Car04_T4 4car");
            if(car==null){reason="current-source-car04-root-missing";return false;}
            var origin=new Vector3(62.20914078f,0,8.36741734f);var axis=new Vector3(.347234219f,0,.937778413f);var inward=new Vector3(.937778354f,0,-.347234458f);
            foreach(var suffix in new[]{"000","001","008"})
            {
                var name="Car04_T4 4car_05_R_in_"+suffix;var filters=car.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name==name).ToArray();
                if(filters.Length!=1){reason="current-source-car04-leaf-not-unique:"+name;return false;}
                var filter=filters[0];var identity=filter.GetComponent<SnapshotObjectIdentity>();var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
                if(identity==null||!identity.SourceActiveInHierarchy||mesh==null||!mesh.isReadable||mesh!=identity.SourceMesh||renderer==null||!renderer.sharedMaterials.SequenceEqual(identity.SourceMaterials))
                {reason="current-source-car04-reference-or-activity-mismatch:"+name;return false;}
                var counts=new int[2];var vertices=mesh.vertices;var specs=(JArray)config["cabin_doors"];
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var indices=mesh.GetTriangles(sub);
                    for(int t=0;t<indices.Length;t+=3)for(int leaf=0;leaf<2;leaf++)
                    {
                        var lo=specs[leaf]["closed_bounds_uvh"]["min"].Select(x=>(float)x).ToArray();var hi=specs[leaf]["closed_bounds_uvh"]["max"].Select(x=>(float)x).ToArray();bool inside=true;
                        for(int k=0;k<3;k++){var p=filter.transform.TransformPoint(vertices[indices[t+k]])-origin;var u=Vector3.Dot(p,axis);var v=Vector3.Dot(p,inward);inside&=u>=lo[0]&&u<=hi[0]&&v>=lo[1]&&v<=hi[1]&&p.y>=lo[2]&&p.y<=hi[2];}
                        if(inside)counts[leaf]++;
                    }
                }
                int expected=suffix=="000"?264:suffix=="001"?8:4;
                rows.Add(new JObject{["object_id"]=identity.ObjectId,["mesh_name"]=mesh.name,["near_triangles"]=counts[0],["far_triangles"]=counts[1],["expected_each"]=expected,["current_mesh_reference_preserved"]=true});
                if(counts.Any(n=>n!=expected)){reason="current-source-car04-triangle-contract-mismatch:"+name;Report["car04_source_contract"]=rows;return false;}
            }
            Report["car04_source_contract"]=rows;reason="current-source-three-leaf-contract-pass";return true;
        }

        void QualifyStationPlaces()
        {
            places.Clear();var candidates=new List<(Vector3 feet,string floor,int triangle,bool support,string collider)>();int tested=0,blocked=0;
            var candidateCells=new Dictionary<(int x,int y,int z),List<Vector3>>();
            bool AlreadyCandidate(Vector3 feet)
            {
                int x=Mathf.FloorToInt(feet.x),y=Mathf.FloorToInt(feet.y),z=Mathf.FloorToInt(feet.z);
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)for(int dz=-1;dz<=1;dz++)
                    if(candidateCells.TryGetValue((x+dx,y+dy,z+dz),out var near)&&near.Any(other=>(other-feet).sqrMagnitude<.01f))return true;
                if(!candidateCells.TryGetValue((x,y,z),out var own)){own=new List<Vector3>();candidateCells.Add((x,y,z),own);}own.Add(feet);return false;
            }
            foreach(var id in (regionIds.Count>0?regionIds:floorIds).OrderBy(x=>x,StringComparer.Ordinal))
            {
                if(!objects.TryGetValue(id,out var region))continue;
                var renderers=region.GetComponentsInChildren<Renderer>(false);if(renderers.Length==0)continue;var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                if(LiveMode&&hasQualifiedNavigationBounds)
                {
                    // Regions generate candidates only where the saved physical source
                    // qualifies a NavMesh extent. Far rendered-only bounds cannot consume
                    // the candidate budget before current station floors are reached.
                    var low=bounds.min;var high=bounds.max;
                    low.x=Mathf.Max(low.x,qualifiedNavigationBounds.min.x);low.z=Mathf.Max(low.z,qualifiedNavigationBounds.min.z);
                    high.x=Mathf.Min(high.x,qualifiedNavigationBounds.max.x);high.z=Mathf.Min(high.z,qualifiedNavigationBounds.max.z);
                    if(low.x>high.x||low.z>high.z)continue;
                    bounds.SetMinMax(low,high);
                }
                // A bounded world grid is only a candidate generator. Every accepted
                // point must hit the explicit current floor collider and clear a body.
                for(float x=Mathf.Ceil(bounds.min.x/6)*6;x<=bounds.max.x&&tested<20000;x+=6)
                    for(float z=Mathf.Ceil(bounds.min.z/6)*6;z<=bounds.max.z&&tested<20000;z+=6)
                    {
                        tested++;foreach(float level in LiveMode?LiveFloorLevels:new[]{region.position.y})
                        {
                            var desired=new Vector3(x,level+.02f,z);
                            if(TryStationSpawn(desired,out var feet,out var source,out var triangle,out var support,out var collider))
                            {if(!AlreadyCandidate(feet))candidates.Add((feet,source,triangle,support,collider));}
                            else blocked++;
                        }
                    }
            }
            // Find the largest connected candidate group with actual NavMesh paths;
            // closed source doors remain real obstructions and are never bypassed.
            var groups=new List<List<(Vector3 feet,string floor,int triangle,bool support,string collider)>>();
            foreach(var candidate in candidates.OrderBy(c=>c.feet.x).ThenBy(c=>c.feet.z).ThenBy(c=>c.floor,StringComparer.Ordinal))
            {
                var group=groups.FirstOrDefault(g=>CompletePath(g[0].feet,candidate.feet));
                if(group==null){group=new List<(Vector3,string,int,bool,string)>();groups.Add(group);}group.Add(candidate);
            }
            var largest=groups.OrderByDescending(g=>g.Count).ThenBy(g=>g[0].feet.x).ThenBy(g=>g[0].feet.z).FirstOrDefault();
            var selectedGroups=LiveMode?groups.Where(g=>g.Count>=2).OrderByDescending(g=>g.Count).Take(8).ToList():new List<List<(Vector3 feet,string floor,int triangle,bool support,string collider)>> { largest };
            foreach(var group in selectedGroups)
            {
                if(group==null)continue;
                int stride=Math.Max(1,(int)Math.Ceiling(group.Count/32.0));int added=0;
                for(int i=0;i<group.Count&&added<32&&places.Count<192;i+=stride)
                {var candidate=group[i];places.Add(("station-place:"+places.Count.ToString("D3",System.Globalization.CultureInfo.InvariantCulture),candidate.feet,candidate.floor,candidate.triangle,candidate.support,candidate.collider));added++;}
            }
            Report["station_places"]=new JObject{["candidate_region_ids"]=new JArray(regionIds.OrderBy(x=>x,StringComparer.Ordinal)),["accepted_support_source_ids"]=new JArray(places.Select(p=>p.floor).Distinct().OrderBy(x=>x,StringComparer.Ordinal)),["grid_candidates_tested"]=tested,["failed_floor_nav_or_capsule"]=blocked,["valid_candidates"]=candidates.Count,["connected_groups"]=groups.Count,["largest_group_candidates"]=largest?.Count??0,["places"]=new JArray(places.Select(p=>new JObject{["id"]=p.id,["source_support_object"]=p.floor,["source_collider_or_meshfilter_file_id"]=p.collider,["native_triangle_index"]=p.triangle,["exact_source_mesh_collision_support"]=p.support,["feet"]=new JArray(p.feet.x,p.feet.y,p.feet.z)})),["claim"]="Actual upward current-source collider hits with NavMesh/capsule checks, sampled current-floor connected regions; room/exit completeness remains unverified."};
            NavigationHash=Stable.Hash(NavigationHash+"|places:"+Report["station_places"].ToString(Newtonsoft.Json.Formatting.None));
        }

        static bool CompletePath(Vector3 from,Vector3 to)
        {var path=new NavMeshPath();return NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;}
        bool TryStationSpawn(Vector3 desired,out Vector3 feet,out string floorId,out int triangle,out bool generatedSupport,out string colliderFileId)
        {
            feet=default;floorId="";triangle=-1;generatedSupport=false;colliderFileId="";
            if(!NavMesh.SamplePosition(desired,out var nav,.8f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-desired.y)>.3f)return false;
            if(!Physics.Raycast(nav.position+Vector3.up*.18f,Vector3.down,out var floor,.4f,~(1<<2),QueryTriggerInteraction.Ignore)||floor.normal.y<.7f)return false;
            floorId=ResolveSourceObject(floor.collider);if(string.IsNullOrEmpty(floorId)||!objects.ContainsKey(floorId)||objects[floorId].GetComponent<SnapshotObjectIdentity>().GeometryDomain!="station")return false;
            if(regionIds.Count==0&&!floorIds.Contains(floorId))return false;
            if(Physics.CheckCapsule(nav.position+Vector3.up*.30f,nav.position+Vector3.up*1.42f,.28f,~(1<<2),QueryTriggerInteraction.Ignore))return false;
            triangle=floor.triangleIndex;generatedSupport=supportSet.Contains(floor.collider.gameObject);
            var identity=objects[floorId].GetComponent<SnapshotObjectIdentity>();
            if(generatedSupport)colliderFileId=Registry.Objects[floorId].Components.First(c=>c.Type=="MeshFilter").FileId;
            else{int index=Array.IndexOf(identity.SourceColliders,floor.collider);if(index<0)return false;colliderFileId=identity.SourceColliderFileIds[index];}
            feet=nav.position+Vector3.up*.04f;return true;
        }

        public void DistributeStationNpcs(World world)
        {
            if(places.Count==0)return;int count=0;
            foreach(var actor in world.Entities.Values.Where(e=>e.Has("npc")&&e.Id!="player").OrderBy(e=>e.Id,StringComparer.Ordinal))
            {
                if(Stable.Unit(world.Seed,"station-source-population",0,actor.Id)>=.5)continue;
                var place=places[Math.Min(places.Count-1,(int)(Stable.Unit(world.Seed,"station-source-population",0,actor.Id,1)*places.Count))];
                actor.X=place.feet.x;actor.Y=place.feet.y;actor.Z=place.feet.z;actor.Set("goal.x",actor.X);actor.Set("goal.y",actor.Y);actor.Set("goal.z",actor.Z);actor.Set("population.station.assumed",1);
                foreach(var destination in places)if(CompletePath(place.feet,destination.feet))actor.Links.Add(destination.id);
                string bagId="bag:"+actor.Id.Substring(4);if(world.Entities.TryGetValue(bagId,out var bag)){bag.X=actor.X+.3;bag.Y=actor.Y;bag.Z=actor.Z;}
                count++;
            }
            Report["station_population"]=new JObject{["spawned_from_seed"]=count,["distribution"]="50 percent seeded assignment to qualified current station places; remaining population retains platform baseline",["role_claim"]="Training assumption; no observed population density or room occupancy claim."};
        }

        public string ResolveSourceObject(Component hit)
        {
            if(hit==null)return "";
            for(var node=hit.transform;node!=null;node=node.parent)
                if(physicalIds.TryGetValue(node,out var id))return id;
            return "";
        }

        public void VerifyReplayFingerprint(string expected)
        {if(string.IsNullOrEmpty(expected)||expected!=Fingerprint)throw new InvalidOperationException("Replay belongs to another full world snapshot or navigation contract");}

        public void Dispose()
        {
            if(stationNavigationInstance.valid)stationNavigationInstance.Remove();
            if(stationNavigation!=null)DestroyOwned(stationNavigation);stationNavigation=null;
            foreach(var support in supports)if(support!=null)DestroyOwned(support);supports.Clear();supportSet.Clear();
            if(instance!=null&&!LiveMode)DestroyOwned(instance.transform.parent.gameObject);instance=null;
            foreach(var identity in liveIdentities)if(identity!=null)DestroyOwned(identity);liveIdentities.Clear();
            objects.Clear();physicalIds.Clear();places.Clear();floorIds.Clear();regionIds.Clear();renderOnlyAlternatives.Clear();hasQualifiedNavigationBounds=false;Activated=false;navigationAttempted=false;NavigationHash="absent";TrainVisual=null;OfficialFrame=null;PlatformSourceRoots=Array.Empty<GameObject>();
        }
        void OnDestroy(){Dispose();}
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public static Matrix4x4 Matrix(float[] values)
        {if(values==null||values.Length!=16)throw new InvalidOperationException("Expected row-major matrix with sixteen values");var m=new Matrix4x4();for(int i=0;i<16;i++)m[i/4,i%4]=values[i];return m;}
        public static bool MatrixNear(Matrix4x4 a,Matrix4x4 b,float tolerance)
        {for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(float.IsNaN(a[r,c])||float.IsInfinity(a[r,c])||Mathf.Abs(a[r,c]-b[r,c])>tolerance)return false;return true;}
        static JObject BoundsJson(Bounds b)=>new JObject{["center"]=new JArray(b.center.x,b.center.y,b.center.z),["size"]=new JArray(b.size.x,b.size.y,b.size.z)};
        static uint[] SourceIndexCounts(Mesh mesh)
        {return Enumerable.Range(0,mesh.subMeshCount).Select(sub=>mesh.GetIndexCount(sub)).ToArray();}
        // Metadata qualification only: do not reinterpret topology, fabricate triangles,
        // change a source buffer or treat readable-but-empty mesh data as a solid surface.
        static bool HasIndexedSourceGeometry(int vertexCount,uint[] submeshIndexCounts)
        {
            if(vertexCount<0)throw new ArgumentOutOfRangeException(nameof(vertexCount));
            if(submeshIndexCounts==null)throw new ArgumentNullException(nameof(submeshIndexCounts));
            bool indexed=submeshIndexCounts.Any(count=>count>0);
            if(indexed&&vertexCount==0)throw new InvalidOperationException("Indexed source mesh has no vertices");
            return indexed;
        }
        static bool ContainsBounds(Bounds outer,Bounds inner,float tolerance)
        {var low=outer.min-Vector3.one*tolerance;var high=outer.max+Vector3.one*tolerance;return inner.min.x>=low.x&&inner.min.y>=low.y&&inner.min.z>=low.z&&inner.max.x<=high.x&&inner.max.y<=high.y&&inner.max.z<=high.z;}
    }
}
