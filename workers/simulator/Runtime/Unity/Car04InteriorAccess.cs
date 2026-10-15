using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    // Runtime adapter only in the isolated simulator. Exact source leaves move;
    // rack, jambs, original floors and external door geometry remain unchanged.
    public sealed class Car04InteriorAccess : IDisposable
    {
        static readonly Vector3 Origin=new Vector3(62.20914078f,0,8.36741734f);
        static readonly Vector3 Axis=new Vector3(.347234219f,0,.937778413f);
        static readonly Vector3 Inward=new Vector3(.937778354f,0,-.347234458f);
        sealed class Copy {public MeshFilter Filter;public Mesh Original,Kept;public MeshCollider Collider;public Material[] Materials;public readonly List<Mesh> Moving=new List<Mesh>();}
        sealed class Leaf {public string Id;public GameObject Object;public Vector3 Closed,Delta;}
        readonly GameObject train;readonly Transform owner,carRoot;readonly JObject config;
        readonly List<Copy> copies=new List<Copy>();readonly List<Leaf> leaves=new List<Leaf>();
        readonly Dictionary<string,bool> open=new Dictionary<string,bool>(StringComparer.Ordinal),linked=new Dictionary<string,bool>(StringComparer.Ordinal);
        readonly Dictionary<string,GameObject> carvers=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        readonly Dictionary<string,NavMeshLinkInstance> links=new Dictionary<string,NavMeshLinkInstance>(StringComparer.Ordinal);
        NavMeshData floorNav;NavMeshDataInstance floorNavInstance;bool disposed;
        public readonly JObject Report=new JObject();
        public string GeometryHash {get;private set;}
        public string Version=>Stable.Hash(GeometryHash+"|"+string.Join(",",DoorIds.Select(id=>id+":"+open[id]+":"+linked[id])));
        public IEnumerable<string> DoorIds=>((JArray)config["cabin_doors"]).Select(x=>(string)x["id"]);
        public int SourceSeatCount=>((JArray)config["seats"]).Count;
        public Car04InteriorAccess(GameObject trainRoot,Transform ownedRoot,string json)
        {
            train=trainRoot;owner=ownedRoot;config=JObject.Parse(json);carRoot=train.transform.Find("Car04_T4 4car");
            if(carRoot==null||SourceSeatCount!=44||!DoorIds.SequenceEqual(new[]{"cabin-door:4:near","cabin-door:4:far"}))throw new InvalidOperationException("Car04 source access contract differs");
            GeometryHash=Stable.Hash(config.ToString(Formatting.None)+"|local-source-nav-voxel.02-radius.28-v1");
            try{Report["source_mesh_partitions"]=Partition();foreach(var id in DoorIds){open[id]=true;linked[id]=false;SetLeaf(id,true);}Report["local_source_navigation"]=BuildSourceFloorNav();foreach(var id in DoorIds)UpdateLink(id,true,null);GeometryHash=Stable.Hash(GeometryHash+"|initial-source-links:"+string.Join(";",DoorIds.Select(id=>id+":"+linked[id]+":"+RecordedPoint(lastEndpoints[id].a).ToString(Formatting.None)+":"+RecordedPoint(lastEndpoints[id].b).ToString(Formatting.None))));Report["geometry_and_initial_link_hash"]=GeometryHash;Report["initial_cabin_link_states"]=new JObject(DoorIds.Select(id=>new JProperty(id,linked[id])));Report["source_seat_count"]=SourceSeatCount;Report["source_floor_samples"]=SourceFloors();Report["source_cushions"]=SeatApproaches(false);Report["claim"]="Original source cabin leaves with approximate .85m training animation, local source-only .02m navigation; no external Car04 door operation or invented floors.";}
            catch{Dispose();throw;}
        }
        static Vector3 Point(JToken p)=>new Vector3((float)p["x"],(float)p["y"],(float)p["z"]);
        static JObject RecordedPoint(Vector3 p)=>new JObject{["x"]=LocalTapeCodec.Float(p.x),["y"]=LocalTapeCodec.Float(p.y),["z"]=LocalTapeCodec.Float(p.z)};
        static Vector3 Value(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        Vector3 Frame(Vector3 world){var d=world-Origin;return new Vector3(Vector3.Dot(d,Axis),Vector3.Dot(d,Inward),world.y);}
        public static Vector3 World(float u,float v,float height)=>Origin+Axis*u+Inward*v+Vector3.up*height;
        JArray Partition()
        {
            var rows=new JArray();var specs=(JArray)config["cabin_doors"];
            foreach(string suffix in new[]{"000","001","008"})
            {
                var filter=carRoot.GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Car04_T4 4car_05_R_in_"+suffix);var original=filter.sharedMesh;var kept=UnityEngine.Object.Instantiate(original);var moving=new[]{UnityEngine.Object.Instantiate(original),UnityEngine.Object.Instantiate(original)};var total=new int[2];var vs=original.vertices;
                for(int sub=0;sub<original.subMeshCount;sub++)
                {
                    var originalIndices=original.GetTriangles(sub);var rest=new List<int>();var selected=new[]{new List<int>(),new List<int>()};
                    for(int t=0;t<originalIndices.Length;t+=3)
                    {
                        int owner=-1;
                        for(int j=0;j<2;j++)
                        {
                            var lo=Value(specs[j]["closed_bounds_uvh"]["min"]);var hi=Value(specs[j]["closed_bounds_uvh"]["max"]);bool inside=true;
                            for(int k=0;k<3;k++){var p=Frame(filter.transform.TransformPoint(vs[originalIndices[t+k]]));inside&=p.x>=lo.x&&p.x<=hi.x&&p.y>=lo.y&&p.y<=hi.y&&p.z>=lo.z&&p.z<=hi.z;}
                            if(inside){if(owner!=-1)throw new InvalidOperationException("Overlapping cabin source leaf contracts");owner=j;}
                        }
                        var destination=owner<0?rest:selected[owner];destination.Add(originalIndices[t]);destination.Add(originalIndices[t+1]);destination.Add(originalIndices[t+2]);
                    }
                    kept.SetTriangles(rest,sub);for(int j=0;j<2;j++){moving[j].SetTriangles(selected[j],sub);total[j]+=selected[j].Count/3;}
                }
                int expected=suffix=="000"?264:suffix=="001"?8:4;if(total.Any(n=>n!=expected))throw new InvalidOperationException("Source cabin leaf triangle count differs for "+filter.name+": "+string.Join(",",total));
                if(!ChannelsEqual(original,kept)||moving.Any(m=>!ChannelsEqual(original,m)))throw new InvalidOperationException("Source vertex channels changed during exact triangle partition");
                kept.RecalculateBounds();var copy=new Copy{Filter=filter,Original=original,Kept=kept,Collider=filter.GetComponent<MeshCollider>(),Materials=filter.GetComponent<MeshRenderer>().sharedMaterials};copies.Add(copy);
                filter.sharedMesh=kept;copy.Collider.sharedMesh=kept;
                for(int j=0;j<2;j++)
                {
                    moving[j].RecalculateBounds();copy.Moving.Add(moving[j]);var go=new GameObject(filter.name+"_"+(string)specs[j]["id"]+"_SOURCE_CABIN_LEAF",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(filter.transform.parent,false);go.transform.localPosition=filter.transform.localPosition;go.transform.localRotation=filter.transform.localRotation;go.transform.localScale=filter.transform.localScale;go.GetComponent<MeshFilter>().sharedMesh=moving[j];go.GetComponent<MeshRenderer>().sharedMaterials=copy.Materials;go.GetComponent<MeshCollider>().sharedMesh=moving[j];go.AddComponent<SemanticBinding>().EntityId=(string)specs[j]["id"];leaves.Add(new Leaf{Object=go,Closed=go.transform.position,Id=(string)specs[j]["id"],Delta=Point(specs[j]["training_open_delta_world"])});
                }
                rows.Add(new JObject{["source_node"]=filter.name,["source_triangles"]=original.triangles.Length/3,["kept_triangles"]=kept.triangles.Length/3,["near_leaf_triangles"]=total[0],["far_leaf_triangles"]=total[1],["all_original_vertex_channels_exact"]=true,["stationary_triangle_partition_exact"]=kept.triangles.Length+moving.Sum(m=>m.triangles.Length)==original.triangles.Length});
            }
            Physics.SyncTransforms();return rows;
        }

        static bool ChannelsEqual(Mesh a,Mesh b)
        {
            bool ok=a.GetVertexAttributes().SequenceEqual(b.GetVertexAttributes())&&a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)&&a.tangents.SequenceEqual(b.tangents)&&a.colors32.SequenceEqual(b.colors32);
            for(int c=0;c<8;c++){var x=new List<Vector4>();var y=new List<Vector4>();a.GetUVs(c,x);b.GetUVs(c,y);ok&=x.SequenceEqual(y);}return ok;
        }

        bool SourceFloor(float u,out Vector3 point,out string source)
        {
            var hits=Physics.RaycastAll(World(u,1.4f,1.2f),Vector3.down,1.5f,~(1<<2),QueryTriggerInteraction.Ignore).Where(x=>x.collider.transform.IsChildOf(train.transform)&&x.collider.name.IndexOf("Seat",StringComparison.Ordinal)<0&&Vector3.Dot(x.normal,Vector3.up)>.7f).OrderBy(x=>x.distance).ToArray();
            if(hits.Length==0){point=default;source="missing";return false;}point=hits[0].point;source=hits[0].collider.name;return true;
        }

        JArray SourceFloors(){var r=new JArray();foreach(var range in new[]{new[]{1.7f,4.4f},new[]{-17.1f,-14.3f}})for(float u=range[0];u<=range[1]+.001f;u+=.05f){bool ok=SourceFloor(u,out var p,out var mesh);r.Add(new JObject{["u"]=u,["source_supported"]=ok,["point"]=PlatformSeededSimulator.Point(p),["source_mesh"]=mesh});}return r;}

        JObject BuildSourceFloorNav()
        {
            var positions=new List<Vector3>();var triangles=new List<int>();var evidence=new JArray();
            foreach(var filter in carRoot.GetComponentsInChildren<MeshFilter>().Where(x=>new[]{"Car04_T4 4car_05_R_in_000","Car04_T4 4car_05_R_in_003","Car04_T4 4car_00_R_out_004"}.Contains(x.name)))
            {
                var mesh=filter.sharedMesh;var vs=mesh.vertices;int selected=0,flipped=0;var submeshes=new JArray();
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    int count=0;var indices=mesh.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=filter.transform.TransformPoint(vs[indices[i]]);var b=filter.transform.TransformPoint(vs[indices[i+1]]);var c=filter.transform.TransformPoint(vs[indices[i+2]]);var cross=Vector3.Cross(b-a,c-a);var f0=Frame(a);var f1=Frame(b);var f2=Frame(c);
                        if(Mathf.Min(a.y,Mathf.Min(b.y,c.y))<.44f||Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.68f||Mathf.Abs(cross.normalized.y)<.85f||cross.magnitude*.5f<.01f||Mathf.Min(f0.y,Mathf.Min(f1.y,f2.y))>1.4f||Mathf.Max(f0.y,Mathf.Max(f1.y,f2.y))<1.4f)continue;
                        var centre=(a+b+c)/3;var hits=Physics.RaycastAll(centre+Vector3.up*.035f,Vector3.down,.07f,~(1<<2),QueryTriggerInteraction.Ignore);if(!hits.Any(h=>h.collider.gameObject==filter.gameObject&&Mathf.Abs(h.point.y-centre.y)<.003f&&h.normal.y>.85f))continue;
                        int offset=positions.Count;positions.Add(a);positions.Add(b);positions.Add(c);triangles.Add(offset);triangles.Add(offset+(cross.y<0?2:1));triangles.Add(offset+(cross.y<0?1:2));if(cross.y<0)flipped++;selected++;count++;
                    }submeshes.Add(new JObject{["submesh"]=sub,["source_floor_triangles"]=count});
                }
                evidence.Add(new JObject{["source_mesh"]=filter.name,["source_floor_triangles"]=selected,["upward_orientation_adapted_triangles"]=flipped,["source_submeshes"]=submeshes,["max_world_position_error_m"]=0});
            }
            if(triangles.Count==0)throw new InvalidOperationException("No physically qualified original source floor triangles for navigation adapter");
            
            var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(train.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.28f;settings.agentHeight=1.72f;settings.agentClimb=.28f;settings.agentSlope=36;settings.overrideVoxelSize=true;settings.voxelSize=.02f;settings.overrideTileSize=true;settings.tileSize=256;settings.minRegionArea=.5f;
            floorNav=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(World(-6.35f,1.4f,1),new Vector3(12,4.5f,24)),Vector3.zero,Quaternion.identity);if(floorNav==null)throw new InvalidOperationException("Source-floor-only navigation adapter returned no data");floorNavInstance=NavMesh.AddNavMeshData(floorNav);
            var native=NavMesh.CalculateTriangulation();var nativeKey=new StringBuilder();foreach(var p in native.vertices)nativeKey.Append(p.x.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(p.z.ToString("R",CultureInfo.InvariantCulture)).Append(';');foreach(var index in native.indices)nativeKey.Append(index).Append(',');string nativeHash=Stable.Hash(nativeKey.ToString());GeometryHash=Stable.Hash(GeometryHash+"|actual-native-geometry:"+nativeHash);
            return new JObject{["source_floor_meshes"]=evidence,["inspected_original_source_floor_triangles"]=triangles.Count/3,["source_floor_triangles_added"]=0,["local_bounds_world"]=new JObject{["center"]=PlatformSeededSimulator.Point(World(-6.35f,1.4f,1)),["size"]=PlatformSeededSimulator.Point(new Vector3(12,4.5f,24))},["max_world_position_error_m"]=0,["new_physical_floor_or_collider"]=false,["original_mesh_or_material_changes"]=false,["agent_radius_m"]=settings.agentRadius,["voxel_size_m"]=settings.voxelSize,["native_navmesh_data_valid"]=floorNavInstance.valid,["native_geometry_hash_with_local_car04_data"]=nativeHash,["native_vertices_with_local_car04_data"]=native.vertices.Length,["native_indices_with_local_car04_data"]=native.indices.Length,["claim"]="Local Car04/gangway navigation precision adapter using all original train collider sources, with0addedfloortriangles andunchanged.28magent;32sourcefloortriangles inspected only. Globalnavigationresolutionandalloriginalphysicalgeometryretained."};
        }

        JObject Walk(Vector3 start,Vector3 target)
        {
            var go=new GameObject("Car04 source route capsule diagnostic",typeof(CharacterController));go.layer=2;var cc=go.GetComponent<CharacterController>();cc.height=1.72f;cc.radius=.28f;cc.center=Vector3.up*.86f;cc.skinWidth=.025f;cc.stepOffset=.28f;cc.slopeLimit=36;go.transform.position=start+Vector3.up*.04f;Physics.SyncTransforms();float vertical=0;CollisionFlags flags=CollisionFlags.None;
            for(int i=0;i<1600;i++){var d=target-go.transform.position;d.y=0;if(d.magnitude<.15f&&Mathf.Abs(go.transform.position.y-target.y)<.2f)break;if(cc.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-30,vertical-9.81f*.02f);flags|=cc.Move(d.normalized*.024f+Vector3.up*vertical*.02f);}
            float error=Vector3.Distance(go.transform.position,target);var r=new JObject{["end"]=PlatformSeededSimulator.Point(go.transform.position),["distance_to_target_m"]=error,["passed"]=error<.3f,["collision_flags"]=flags.ToString(),["radius_m"]=cc.radius,["height_m"]=cc.height};UnityEngine.Object.DestroyImmediate(go);return r;
        }
        void SetLeaf(string id,bool value){foreach(var leaf in leaves.Where(x=>x.Id==id))leaf.Object.transform.position=leaf.Closed+(value?leaf.Delta:Vector3.zero);open[id]=value;Physics.SyncTransforms();}
        void SetCarver(string id,bool closed)
        {
            if(!closed){if(carvers.TryGetValue(id,out var old)){UnityEngine.Object.DestroyImmediate(old);carvers.Remove(id);}return;}
            if(carvers.ContainsKey(id))return;float u=id.EndsWith(":near",StringComparison.Ordinal)?-.906f:-13.372f;
            var go=new GameObject("Closed source cabin navigation · "+id,typeof(NavMeshObstacle));go.transform.SetParent(owner,false);go.transform.position=World(u,1.4f,1.3f);go.transform.rotation=Quaternion.LookRotation(Axis);var obstacle=go.GetComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(.86f,2.2f,.16f);obstacle.carving=true;obstacle.carveOnlyStationary=false;carvers[id]=go;
        }
        string UpdateLink(string id,bool value,JObject recorded)
        {
            bool valid=false;string reason="closed";Vector3 a=default,b=default,navA=default,navB=default;
            bool near=id.EndsWith(":near",StringComparison.Ordinal);
            if(value)
            {
                if(recorded!=null){valid=(bool)recorded["linked"];reason=(string)recorded["reason"];navA=Point(recorded["start"]);navB=Point(recorded["end"]);}
                else
                {
                    bool sourceA=SourceFloor(near?1.6f:-17.2f,out a,out _),sourceB=SourceFloor(near?4.5f:-14.2f,out b,out _);bool aOk=NavMesh.SamplePosition(a,out var ha,.3f,NavMesh.AllAreas),bOk=NavMesh.SamplePosition(b,out var hb,.3f,NavMesh.AllAreas);navA=ha.position;navB=hb.position;valid=sourceA&&sourceB&&aOk&&bOk;reason="source floor or navigation endpoint missing";
                    if(valid){var delta=b-a;valid=!Physics.CapsuleCast(a+Vector3.up*.52f,a+Vector3.up*1.42f,.28f,delta.normalized,out var hit,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);reason=valid?"source floors and capsule clearance":"capsule-blocked:"+hit.collider.name;}
                }
            }
            else if(recorded!=null){if((bool)recorded["linked"])throw new InvalidOperationException("Closed cabin door cannot replay an open link");reason=(string)recorded["reason"];}
            if(links.TryGetValue(id,out var old)&&(!valid||!NavMesh.IsLinkValid(old))){if(NavMesh.IsLinkValid(old))NavMesh.RemoveLink(old);links.Remove(id);}
            if(valid&&!links.ContainsKey(id)){var link=NavMesh.AddLink(new NavMeshLinkData{startPosition=navA,endPosition=navB,width=.05f,bidirectional=true,agentTypeID=0,area=0,costModifier=-1});valid=NavMesh.IsLinkValid(link);if(valid)links[id]=link;else reason="native source link rejected";}
            linked[id]=valid;lastEndpoints[id]=(navA,navB);return reason;
        }
        readonly Dictionary<string,(Vector3 a,Vector3 b)> lastEndpoints=new Dictionary<string,(Vector3,Vector3)>(StringComparer.Ordinal);
        public JArray Project(World state,JArray recorded)
        {
            var rows=new JArray();foreach(var id in DoorIds)
            {
                if(!state.Entities.TryGetValue(id,out var entity))throw new InvalidOperationException("Car04 access resource and logical coverage disagree: "+id);
                bool value=entity.Get("open")>.9&&entity.Get("jammed")<.5;JObject prior=recorded?.OfType<JObject>().Single(x=>(string)x["door_id"]==id);if(prior!=null&&(bool)prior["open"]!=value)throw new InvalidOperationException("Recorded cabin door state differs");SetLeaf(id,value);SetCarver(id,!value);string reason=UpdateLink(id,value,prior);entity.Set("navigation.reachable",value&&linked[id]?1:0);
                var points=lastEndpoints[id];rows.Add(new JObject{["car"]=4,["door_id"]=id,["open"]=value,["linked"]=linked[id],["reason"]=reason,["start"]=prior==null?RecordedPoint(points.a):prior["start"].DeepClone(),["end"]=prior==null?RecordedPoint(points.b):prior["end"].DeepClone()});
            }
            if(state.Entities.TryGetValue("car:4",out var car)){car.Set("reachable",linked.Values.Any(x=>x)?1:0);car.Set("accessible",linked.Values.Any(x=>x)?1:0);}return rows;
        }
        public JArray Routes(bool physical=true)
        {
            var rows=new JArray();foreach(float start in new[]{6f,-19.2f})
            {
                var a=World(start,1.4f,.448025f);var b=World(-6.5f,1.4f,.448025f);var path=new NavMeshPath();bool aOk=NavMesh.SamplePosition(a,out var ha,.1f,NavMesh.AllAreas),bOk=NavMesh.SamplePosition(b,out var hb,.1f,NavMesh.AllAreas);bool complete=aOk&&bOk&&NavMesh.CalculatePath(ha.position,hb.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                var row=new JObject{["via_neighbor_car"]=start>0?3:5,["complete_path"]=complete,["path_status"]=path.status.ToString(),["start_sample_error_m"]=aOk?Vector3.Distance(a,ha.position):999,["goal_sample_error_m"]=bOk?Vector3.Distance(b,hb.position):999};if(physical){row["capsule"]=Walk(a,b);row["reverse_capsule"]=Walk(b,a);}rows.Add(row);
            }return rows;
        }
        public JArray SeatApproaches(bool paths=true)
        {
            var result=new JArray();var center=World(-6.5f,1.4f,.448025f);
            foreach(var seat in (JArray)config["seats"])
            {
                var surface=Point(seat["cushion_surface"]);var aisle=Point(seat["aisle_approach"]);bool found=Physics.Raycast(surface+Vector3.up*.08f,Vector3.down,out var hit,.16f,~(1<<2),QueryTriggerInteraction.Ignore);float error=found?Vector3.Distance(hit.point,surface):999;bool cushion=found&&hit.collider.name.Contains("Car04_T4 4car_06_Seat_4car_");bool sampled=NavMesh.SamplePosition(aisle,out var nav,.12f,NavMesh.AllAreas);var p=new NavMeshPath();bool route=!paths||sampled&&NavMesh.CalculatePath(center,nav.position,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete;
                result.Add(new JObject{["id"]=(string)seat["id"],["source_cushion_matched"]=cushion&&error<.005f,["cushion_surface_error_m"]=error,["aisle_approach_nav_attached"]=sampled,["complete_aisle_approach_path"]=route,["passed"]=cushion&&error<.005f&&sampled&&route});
            }return result;
        }
        public bool ExternalDoorBlocked()
        {
            var a=new Vector3(61.45891953f,0,8.64520454f);var b=new Vector3(63.10002899f,.44802487f,8.03754520f);var d=b-a;return Physics.CapsuleCast(a+Vector3.up*.52f,a+Vector3.up*1.42f,.22f,d.normalized,out _,d.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;foreach(var link in links.Values)if(NavMesh.IsLinkValid(link))NavMesh.RemoveLink(link);links.Clear();foreach(var go in carvers.Values)if(go!=null)UnityEngine.Object.DestroyImmediate(go);carvers.Clear();if(floorNavInstance.valid)floorNavInstance.Remove();if(floorNav!=null)UnityEngine.Object.DestroyImmediate(floorNav);floorNav=null;
            foreach(var leaf in leaves)if(leaf.Object!=null)UnityEngine.Object.DestroyImmediate(leaf.Object);leaves.Clear();foreach(var c in copies){if(c.Filter!=null){c.Filter.sharedMesh=c.Original;c.Collider.sharedMesh=c.Original;}if(c.Kept!=null)UnityEngine.Object.DestroyImmediate(c.Kept);foreach(var m in c.Moving)if(m!=null)UnityEngine.Object.DestroyImmediate(m);}copies.Clear();Physics.SyncTransforms();
        }
    }
}
