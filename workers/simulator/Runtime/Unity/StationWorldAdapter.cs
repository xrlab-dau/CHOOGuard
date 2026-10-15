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
        readonly List<(string id,Vector3 feet,string floor,int triangle,bool support,string collider)> places=new List<(string,Vector3,string,int,bool,string)>();
        readonly HashSet<string> floorIds=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> regionIds=new HashSet<string>(StringComparer.Ordinal);
        GameObject instance;
        string manifestHash="",geometrySceneHash="";
        NavMeshData stationNavigation;
        NavMeshDataInstance stationNavigationInstance;

        // The current saved snapshot owns all station/platform/KTX geometry. No raw
        // platform or train prefab is instantiated alongside it.
        public void Prepare(Transform owner)
        {
            if(Prepared)return;
            if(SnapshotPrefab==null||SnapshotInventory==null||SnapshotManifest==null)
                throw new InvalidOperationException("Full world snapshot prefab/inventory/manifest are required");
            if(!MatrixNear(owner.localToWorldMatrix,Matrix4x4.identity,1e-6f))
                throw new InvalidOperationException("Snapshot requires the approved identity world frame");
            var manifest=JObject.Parse(SnapshotManifest.text);
            if((string)manifest["schema"]!="chooguard-geometry-prefab.v1")throw new InvalidOperationException("Unsupported snapshot prefab manifest");
            manifestHash=Stable.Hash(SnapshotManifest.text);
            geometrySceneHash=(string)manifest["geometrySceneSha256"];
            if(geometrySceneHash==null||geometrySceneHash.Length!=64||geometrySceneHash.Any(c=>!(c>='0'&&c<='9')&&!(c>='a'&&c<='f')))
                throw new InvalidOperationException("Frozen geometry scene SHA256 required");
            Registry=WorldObjectRegistry.Parse(SnapshotInventory.text,(string)manifest["inventorySha256"],(string)manifest["sourceSceneSha256"]);
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

        public void ActivateGeometry()
        {
            if(!Prepared)throw new InvalidOperationException("Prepare source geometry first");
            instance.transform.parent.gameObject.SetActive(true);Activated=true;Physics.SyncTransforms();
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
            int sourceSolid=0,added=0;var unreadable=new JArray();
            var sources=new List<NavMeshBuildSource>();bool hasBounds=false;var bounds=new Bounds();
            foreach(var identity in instance.GetComponentsInChildren<SnapshotObjectIdentity>(true).OrderBy(x=>x.ObjectId,StringComparer.Ordinal))
            {
                if(!identity.SourceActiveInHierarchy||identity.GeometryDomain!="station")continue;
                var go=identity.gameObject;
                var colliders=go.GetComponents<Collider>().Where(c=>c.enabled&&!c.isTrigger&&!(c is CharacterController)).ToArray();
                sourceSolid+=colliders.Length;
                var filter=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<MeshRenderer>();
                if(filter!=null&&filter.sharedMesh!=null&&renderer!=null&&renderer.enabled)
                {
                    var mesh=filter.sharedMesh;
                    if(!mesh.isReadable){unreadable.Add(identity.ObjectId);continue;}
                    if(colliders.Length==0)
                    {
                        var support=new GameObject("Exact source-mesh collision support");support.transform.SetParent(go.transform,false);
                        var collider=support.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;
                        supports.Add(support);physicalIds[support.transform]=identity.ObjectId;added++;
                    }
                    if(!hasBounds){bounds=renderer.bounds;hasBounds=true;}else bounds.Encapsulate(renderer.bounds);
                }
                foreach(var collider in colliders){if(!hasBounds){bounds=collider.bounds;hasBounds=true;}else bounds.Encapsulate(collider.bounds);}
            }
            Physics.SyncTransforms();
            NavMeshBuilder.CollectSources(instance.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>!(s.component is Collider c)||c.isTrigger||c is CharacterController||c.GetComponentInParent<SnapshotObjectIdentity>()?.GeometryDomain!="station");
            if(!hasBounds||sources.Count==0)throw new InvalidOperationException("No readable active station source geometry for navigation");
            bool hasRegion=false;var regionBounds=new Bounds();float firstFloorLevel=0;
            foreach(var id in regionIds.OrderBy(x=>x,StringComparer.Ordinal))
            {
                var region=objects[id];firstFloorLevel=region.position.y;
                foreach(var renderer in region.GetComponentsInChildren<Renderer>(false))
                {if(!hasRegion){regionBounds=renderer.bounds;hasRegion=true;}else regionBounds.Encapsulate(renderer.bounds);}
            }
            if(hasRegion)bounds=new Bounds(new Vector3(regionBounds.center.x,firstFloorLevel+1.35f,regionBounds.center.z),new Vector3(regionBounds.size.x+2,3.3f,regionBounds.size.z+2));
            else bounds.Expand(new Vector3(2,1,2));
            var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.28f;settings.agentHeight=1.72f;settings.agentClimb=.28f;settings.agentSlope=36;
            settings.overrideVoxelSize=true;settings.voxelSize=.10f;settings.overrideTileSize=true;settings.tileSize=256;settings.minRegionArea=.5f;
            stationNavigation=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(stationNavigation==null)throw new InvalidOperationException("Station source geometry NavMesh build failed");
            stationNavigationInstance=NavMesh.AddNavMeshData(stationNavigation);
            if(!stationNavigationInstance.valid)throw new InvalidOperationException("Station NavMesh registration failed");
            NavigationHash=Stable.Hash(Registry.Fingerprint+"|station-geometry-nav-v1|"+string.Join("|",sources.Select(s=>ResolveSourceObject(s.component)).OrderBy(x=>x,StringComparer.Ordinal)));
            Activated=true;
            Report["navigation"]=new JObject{["source_geometry_bounds"]=BoundsJson(bounds),["candidate_region_ids"]=new JArray(regionIds),["first_floor_candidate_level"]=firstFloorLevel,["mesh_sources"]=sources.Count,["saved_solid_colliders"]=sourceSolid,["explicit_mesh_collision_supports"]=added,["unreadable_mesh_objects"]=unreadable,["hash"]=NavigationHash,["existing_platform_car04_navigation_preserved"]=true,["coverage_claim"]="Additive 1F walkable islands from current source colliders; upper geometry remains input, with cross-floor routes and every room coverage unverified."};
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
            foreach(var id in (regionIds.Count>0?regionIds:floorIds).OrderBy(x=>x,StringComparer.Ordinal))
            {
                if(!objects.TryGetValue(id,out var region))continue;
                var renderers=region.GetComponentsInChildren<Renderer>(false);if(renderers.Length==0)continue;var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                // A bounded world grid is only a candidate generator. Every accepted
                // point must hit the explicit current floor collider and clear a body.
                for(float x=Mathf.Ceil(bounds.min.x/6)*6;x<=bounds.max.x&&tested<20000;x+=6)
                    for(float z=Mathf.Ceil(bounds.min.z/6)*6;z<=bounds.max.z&&tested<20000;z+=6)
                    {
                        tested++;var desired=new Vector3(x,region.position.y+.02f,z);
                        if(TryStationSpawn(desired,out var feet,out var source,out var triangle,out var support,out var collider))candidates.Add((feet,source,triangle,support,collider));else blocked++;
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
            if(largest!=null)
            {
                int stride=Math.Max(1,(int)Math.Ceiling(largest.Count/64.0));
                for(int i=0;i<largest.Count&&places.Count<64;i+=stride){var candidate=largest[i];places.Add(("station-place:"+places.Count.ToString("D3",System.Globalization.CultureInfo.InvariantCulture),candidate.feet,candidate.floor,candidate.triangle,candidate.support,candidate.collider));}
            }
            Report["station_places"]=new JObject{["candidate_region_ids"]=new JArray(regionIds.OrderBy(x=>x,StringComparer.Ordinal)),["accepted_support_source_ids"]=new JArray(places.Select(p=>p.floor).Distinct().OrderBy(x=>x,StringComparer.Ordinal)),["grid_candidates_tested"]=tested,["failed_floor_nav_or_capsule"]=blocked,["valid_candidates"]=candidates.Count,["connected_groups"]=groups.Count,["largest_group_candidates"]=largest?.Count??0,["places"]=new JArray(places.Select(p=>new JObject{["id"]=p.id,["source_support_object"]=p.floor,["source_collider_or_meshfilter_file_id"]=p.collider,["native_triangle_index"]=p.triangle,["exact_source_mesh_collision_support"]=p.support,["feet"]=new JArray(p.feet.x,p.feet.y,p.feet.z)})),["claim"]="Actual upward current-source collider hits with NavMesh/capsule checks, largest sampled 1F connected region; room/exit completeness and other floors remain unverified."};
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
            triangle=floor.triangleIndex;generatedSupport=supports.Contains(floor.collider.gameObject);
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
                foreach(var destination in places)actor.Links.Add(destination.id);
                string bagId="bag:"+actor.Id.Substring(4);if(world.Entities.TryGetValue(bagId,out var bag)){bag.X=actor.X+.3;bag.Y=actor.Y;bag.Z=actor.Z;}
                count++;
            }
            Report["station_population"]=new JObject{["spawned_from_seed"]=count,["distribution"]="50 percent seeded assignment to qualified 1F station places; remaining population retains platform baseline",["role_claim"]="Training assumption; no observed population density or room occupancy claim."};
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
            if(instance!=null)DestroyOwned(instance.transform.parent.gameObject);instance=null;
            objects.Clear();physicalIds.Clear();supports.Clear();places.Clear();floorIds.Clear();regionIds.Clear();Activated=false;navigationAttempted=false;NavigationHash="absent";TrainVisual=null;OfficialFrame=null;PlatformSourceRoots=Array.Empty<GameObject>();
        }
        void OnDestroy(){Dispose();}
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public static Matrix4x4 Matrix(float[] values)
        {if(values==null||values.Length!=16)throw new InvalidOperationException("Expected row-major matrix with sixteen values");var m=new Matrix4x4();for(int i=0;i<16;i++)m[i/4,i%4]=values[i];return m;}
        public static bool MatrixNear(Matrix4x4 a,Matrix4x4 b,float tolerance)
        {for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(float.IsNaN(a[r,c])||float.IsInfinity(a[r,c])||Mathf.Abs(a[r,c]-b[r,c])>tolerance)return false;return true;}
        static JObject BoundsJson(Bounds b)=>new JObject{["center"]=new JArray(b.center.x,b.center.y,b.center.z),["size"]=new JArray(b.size.x,b.size.y,b.size.z)};
    }
}
