using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    // Explicit source-qualified diagnostic. No production entity/door/seat activation.
    public sealed class Car04CandidateProbeRunner:MonoBehaviour
    {
        public PlatformSeededSimulator Simulator;
        public JObject Result { get; private set; }
        static readonly Vector3 Origin=new Vector3(62.20914078f,0,8.36741734f);
        static readonly Vector3 Axis=new Vector3(.347234219f,0,.937778413f);
        static readonly Vector3 Inward=new Vector3(.937778354f,0,-.347234458f);
        sealed class Copy {public MeshFilter Filter;public Mesh Original,Kept;public MeshCollider Collider;public Material[] Materials;public readonly List<Mesh> Moving=new List<Mesh>();}
        sealed class Leaf {public GameObject Object;public Vector3 Closed,Delta;}
        readonly List<Copy> copies=new List<Copy>();readonly List<Leaf> leaves=new List<Leaf>();readonly List<GameObject> carvers=new List<GameObject>();readonly List<NavMeshLinkInstance> links=new List<NavMeshLinkInstance>();
        NavMeshData floorNav;NavMeshDataInstance floorNavInstance;Mesh floorNavMesh;PlatformNavigation navigation;JObject contract;JArray seatPacket;Transform carRoot;
        Vector3 Frame(Vector3 world){var d=world-Origin;return new Vector3(Vector3.Dot(d,Axis),Vector3.Dot(d,Inward),world.y);}
        static Vector3 World(float u,float v,float height)=>Origin+Axis*u+Inward*v+Vector3.up*height;
        static Vector3 Value(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static string Hash(string file){using(var a=SHA256.Create())return BitConverter.ToString(a.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();}
        IEnumerator Start()
        {
            JObject report=null;
            try
            {
                var sim=Simulator;sim.enabled=false;sim.Prepare();sim.StopRun();sim.Navigation.Dispose();
                string input=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Verification","inputs"));
                contract=JObject.Parse(File.ReadAllText(Path.Combine(input,"source-leaf-contract.json")));seatPacket=(JArray)JObject.Parse(File.ReadAllText(Path.Combine(input,"source-seat-correspondence-packet.json")))["seats"];
                carRoot=sim.TrainVisual.transform.Find("Car04_T4 4car");
                report=new JObject{["claim"]="Isolated reversible original-source cabin-leaf/intercar route diagnostic; direct Car04 external door and luggage rack retained; no production semantic activation.",["play_mode"]=Application.isPlaying,["source_assets_modified"]=false,["source_assembly_manifest_context"]=(string)JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text)["runtime_fingerprint"],["diagnostic_runner_sha256"]=Hash(Path.Combine(Application.dataPath,"Simulator","Runtime","Unity","Car04CandidateProbeRunner.cs")),["source_leaf_contract_sha256"]=Hash(Path.Combine(input,"source-leaf-contract.json")),["source_seat_packet_sha256"]=Hash(Path.Combine(input,"source-seat-correspondence-packet.json")),["articulation_claim"]="0.85m lateral opening is a bounded training approximation; exact field mechanism and timing are unverified."};
                report["source_mesh_partitions"]=Partition();SetOpen(true);
                navigation=new PlatformNavigation(sim.Source,sim.transform);navigation.Bake(sim.PlatformVisual,sim.TrainVisual);
                report["source_floor_samples"]=SourceFloors();report["source_floor_representation_before_nav_adapter"]=SourceFloorRepresentation();report["source_floor_nav_adapter"]=BuildSourceFloorNav();report["source_floor_representation_after_nav_adapter"]=SourceFloorRepresentation();report["source_cushion_correspondence"]=SeatCorrespondence();report["navigation_before_intercar_links"]=navigation.Report.DeepClone();
                report["intercar_links"]=AddIntercarLinks();report["candidate_seats_activated"]=0;report["semantic_operations_activated"]=false;
            }
            catch(Exception e){report=report??new JObject();report["passed"]=false;report["failure"]=e.ToString();Cleanup();Result=report;yield break;}
            yield return new WaitForSecondsRealtime(.4f);
            report["open_routes"]=Routes();SetOpen(false);RemoveIntercarLinks();SetCarvers(true);
            yield return new WaitForSecondsRealtime(.5f);
            report["closed_routes"]=Routes();SetCarvers(false);SetOpen(true);report["restored_intercar_links"]=AddIntercarLinks();
            yield return new WaitForSecondsRealtime(.5f);
            report["restored_routes"]=Routes();
            var candidate=JObject.Parse(Resources.Load<TextAsset>("Car04Candidate").text)["candidate"].ToObject<SourceCar>();var outside=PlatformNavigation.V(candidate.door.outside);var inside=PlatformNavigation.V(candidate.door.inside);var d=inside-outside;
            report["external_door_still_blocked"]=Physics.CapsuleCast(outside+Vector3.up*.52f,outside+Vector3.up*1.42f,.22f,d.normalized,out var hit,d.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);report["external_door_blocker"]=hit.collider==null?null:hit.collider.name;
            bool tests=((JArray)report["open_routes"]).All(x=>(bool)x["complete_path"]&&(bool)x["capsule"]["passed"])&&((JArray)report["closed_routes"]).All(x=>!(bool)x["complete_path"]&&!(bool)x["capsule"]["passed"])&&((JArray)report["restored_routes"]).All(x=>(bool)x["complete_path"]&&(bool)x["capsule"]["passed"]);
            bool floors=((JArray)report["source_floor_samples"]).All(x=>(bool)x["source_supported"]);bool seats=((JArray)report["source_cushion_correspondence"]).Count==44&&((JArray)report["source_cushion_correspondence"]).All(x=>(bool)x["passed"]);
            Cleanup();report["all_original_mesh_references_restored"]=copies.Count==0;report["owned_objects_remaining"]=leaves.Count+carvers.Count;report["passed"]=tests&&floors&&seats&&(bool)report["external_door_still_blocked"]&&cleanupExact;Result=report;
        }
        JArray Partition()
        {
            var rows=new JArray();var specs=(JArray)contract["leaves"];
            foreach(string suffix in new[]{"000","001","008"})
            {
                var filter=carRoot.GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Car04_T4 4car_05_R_in_"+suffix);var original=filter.sharedMesh;var kept=Instantiate(original);var moving=new[]{Instantiate(original),Instantiate(original)};var total=new int[2];var vs=original.vertices;
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
                    moving[j].RecalculateBounds();copy.Moving.Add(moving[j]);var go=new GameObject(filter.name+"_"+(string)specs[j]["id"]+"_DIAGNOSTIC",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(filter.transform.parent,false);go.transform.localPosition=filter.transform.localPosition;go.transform.localRotation=filter.transform.localRotation;go.transform.localScale=filter.transform.localScale;go.GetComponent<MeshFilter>().sharedMesh=moving[j];go.GetComponent<MeshRenderer>().sharedMaterials=copy.Materials;go.GetComponent<MeshCollider>().sharedMesh=moving[j];leaves.Add(new Leaf{Object=go,Closed=go.transform.position,Delta=Inward*(float)specs[j]["open_delta_uvh"][1]});
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
        void SetOpen(bool open){foreach(var leaf in leaves)leaf.Object.transform.position=leaf.Closed+(open?leaf.Delta:Vector3.zero);Physics.SyncTransforms();}
        bool SourceFloor(float u,out Vector3 point,out string source)
        {
            var hits=Physics.RaycastAll(World(u,1.4f,1.2f),Vector3.down,1.5f,~(1<<2),QueryTriggerInteraction.Ignore).Where(x=>x.collider.transform.IsChildOf(Simulator.TrainVisual.transform)&&x.collider.name.IndexOf("Seat",StringComparison.Ordinal)<0&&Vector3.Dot(x.normal,Vector3.up)>.7f).OrderBy(x=>x.distance).ToArray();
            if(hits.Length==0){point=default;source="missing";return false;}point=hits[0].point;source=hits[0].collider.name;return true;
        }
        JArray SourceFloors(){var r=new JArray();foreach(var range in new[]{new[]{1.7f,4.4f},new[]{-17.1f,-14.3f}})for(float u=range[0];u<=range[1]+.001f;u+=.05f){bool ok=SourceFloor(u,out var p,out var mesh);r.Add(new JObject{["u"]=u,["source_supported"]=ok,["point"]=PlatformSeededSimulator.Point(p),["source_mesh"]=mesh});}return r;}
        JArray SourceFloorRepresentation()
        {
            var rows=new JArray();foreach(float u in new[]{6f,3.47f,2.8f,1.6f,-1.5f,-6.5f,-12f,-14.2f,-15.85f,-16.5f,-19.2f})
            {
                var hit=Physics.RaycastAll(World(u,1.4f,1.2f),Vector3.down,1.5f,~(1<<2),QueryTriggerInteraction.Ignore).Where(x=>x.collider.transform.IsChildOf(Simulator.TrainVisual.transform)&&!x.collider.name.Contains("Seat")&&Vector3.Dot(x.normal,Vector3.up)>.7f).OrderBy(x=>x.distance).First();
                var mesh=((MeshCollider)hit.collider).sharedMesh;var triangles=mesh.triangles;var vs=mesh.vertices;int offset=hit.triangleIndex*3;var a=hit.collider.transform.TransformPoint(vs[triangles[offset]]);var b=hit.collider.transform.TransformPoint(vs[triangles[offset+1]]);var c=hit.collider.transform.TransformPoint(vs[triangles[offset+2]]);float up=Vector3.Dot(Vector3.Cross(b-a,c-a).normalized,Vector3.up);
                bool sampled=NavMesh.SamplePosition(hit.point,out var nav,1,NavMesh.AllAreas);rows.Add(new JObject{["u"]=u,["source_mesh"]=hit.collider.name,["source_triangle"]=hit.triangleIndex,["physics_normal_up"]=Vector3.Dot(hit.normal,Vector3.up),["triangle_world_cross_up"]=up,["local_to_world_determinant"]=hit.collider.transform.localToWorldMatrix.determinant,["physics_query_backfaces"]=Physics.queriesHitBackfaces,["nav_attached_within_1m"]=sampled,["nav_attachment_error_m"]=sampled?Vector3.Distance(hit.point,nav.position):999});
            }return rows;
        }
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
            floorNavMesh=new Mesh{name="Car04 exact original floor triangles NAVIGATION ONLY"};floorNavMesh.SetVertices(positions);floorNavMesh.SetTriangles(triangles,0);floorNavMesh.RecalculateBounds();
            var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(Simulator.TrainVisual.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.28f;settings.agentHeight=1.72f;settings.agentClimb=.28f;settings.agentSlope=36;settings.overrideVoxelSize=true;settings.voxelSize=.02f;settings.overrideTileSize=true;settings.tileSize=256;settings.minRegionArea=.5f;
            floorNav=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(World(-6.35f,1.4f,1),new Vector3(12,4.5f,24)),Vector3.zero,Quaternion.identity);if(floorNav==null)throw new InvalidOperationException("Source-floor-only navigation adapter returned no data");floorNavInstance=NavMesh.AddNavMeshData(floorNav);
            return new JObject{["source_floor_meshes"]=evidence,["inspected_original_source_floor_triangles"]=triangles.Count/3,["source_floor_triangles_added"]=0,["local_bounds_world"]=new JObject{["center"]=PlatformSeededSimulator.Point(World(-6.35f,1.4f,1)),["size"]=PlatformSeededSimulator.Point(new Vector3(12,4.5f,24))},["max_world_position_error_m"]=0,["new_physical_floor_or_collider"]=false,["original_mesh_or_material_changes"]=false,["agent_radius_m"]=settings.agentRadius,["voxel_size_m"]=settings.voxelSize,["native_navmesh_data_valid"]=floorNavInstance.valid,["claim"]="Local Car04/gangway navigation precision adapter using all original train collider sources, with0addedfloortriangles andunchanged.28magent;32sourcefloortriangles inspected only. Globalnavigationresolutionandalloriginalphysicalgeometryretained."};
        }
        JArray SeatCorrespondence()
        {
            var r=new JArray();foreach(var seat in seatPacket)
            {
                var p=Value(seat["pad_surface_uvh"]);var world=World(p.x,p.y,p.z);bool found=Physics.Raycast(world+Vector3.up*.08f,Vector3.down,out var hit,.16f,~(1<<2),QueryTriggerInteraction.Ignore);float error=found?Vector3.Distance(hit.point,world):999;bool source=found&&hit.collider.name.Contains("Car04_T4 4car_06_Seat_4car_");
                r.Add(new JObject{["id"]=(string)seat["id"],["source_pad_hit"]=source,["surface_error_m"]=error,["source_mesh"]=found?hit.collider.name:null,["backrest_source_vertex_samples"]=(int)seat["backrest_source_vertex_samples"],["facing_longitudinal_sign"]=(int)seat["facing_longitudinal_sign"],["passed"]=source&&error<.005f&&(int)seat["backrest_source_vertex_samples"]>0});
            }return r;
        }
        JArray AddIntercarLinks()
        {
            RemoveIntercarLinks();var rows=new JArray();foreach(var span in new[]{new[]{1.6f,4.5f},new[]{-17.2f,-14.2f}})
            {
                bool aOk=SourceFloor(span[0],out var a,out var aMesh),bOk=SourceFloor(span[1],out var b,out var bMesh);bool sampleA=NavMesh.SamplePosition(a,out var ha,1,NavMesh.AllAreas),sampleB=NavMesh.SamplePosition(b,out var hb,1,NavMesh.AllAreas);bool ok=aOk&&bOk&&sampleA&&sampleB&&Vector3.Distance(a,ha.position)<.3f&&Vector3.Distance(b,hb.position)<.3f;
                string reason="source support or NavMesh endpoint missing";
                if(ok)
                {
                    var d=b-a;bool blocked=Physics.CapsuleCast(a+Vector3.up*.52f,a+Vector3.up*1.42f,.28f,d.normalized,out var hit,d.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);
                    if(blocked){ok=false;reason="capsule-blocked:"+hit.collider.name;}else{var link=NavMesh.AddLink(new NavMeshLinkData{startPosition=ha.position,endPosition=hb.position,width=.05f,bidirectional=true,agentTypeID=0,area=0,costModifier=-1});ok=NavMesh.IsLinkValid(link);if(ok)links.Add(link);reason=ok?"source floors and actual capsule clearance":"native link rejected";}
                }
                rows.Add(new JObject{["u_start"]=span[0],["u_end"]=span[1],["source_start_floor"]=aMesh,["source_end_floor"]=bMesh,["linked"]=ok,["nav_start_sampled"]=sampleA,["nav_end_sampled"]=sampleB,["nav_start_error_m"]=sampleA?Vector3.Distance(a,ha.position):999,["nav_end_error_m"]=sampleB?Vector3.Distance(b,hb.position):999,["reason"]=reason});
            }return rows;
        }
        void RemoveIntercarLinks(){foreach(var link in links)if(NavMesh.IsLinkValid(link))NavMesh.RemoveLink(link);links.Clear();}
        void SetCarvers(bool closed)
        {
            foreach(var go in carvers)if(go!=null)DestroyImmediate(go);carvers.Clear();if(!closed)return;
            foreach(var u in new[]{-.906f,-13.372f}){var go=new GameObject("Closed source Car04 cabin door diagnostic",typeof(NavMeshObstacle));go.transform.SetParent(Simulator.transform,false);go.transform.position=World(u,1.4f,1.3f);go.transform.rotation=Quaternion.LookRotation(Axis);var obstacle=go.GetComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(.86f,2.2f,.16f);obstacle.carving=true;obstacle.carveOnlyStationary=false;carvers.Add(go);}
        }
        JArray Routes()
        {
            var rows=new JArray();foreach(float start in new[]{6f,-19.2f})
            {
                var a=World(start,1.4f,.448025f);var b=World(-6.5f,1.4f,.448025f);var path=new NavMeshPath();bool startSample=NavMesh.SamplePosition(a,out var ha,1,NavMesh.AllAreas),goalSample=NavMesh.SamplePosition(b,out var hb,1,NavMesh.AllAreas);bool complete=startSample&&goalSample&&NavMesh.CalculatePath(ha.position,hb.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                rows.Add(new JObject{["via_neighbor_car"]=start>0?3:5,["complete_path"]=complete,["path_status"]=path.status.ToString(),["start_sampled"]=startSample,["goal_sampled"]=goalSample,["start_sample_error_m"]=startSample?Vector3.Distance(a,ha.position):999,["goal_sample_error_m"]=goalSample?Vector3.Distance(b,hb.position):999,["sampled_start"]=PlatformSeededSimulator.Point(ha.position),["sampled_goal"]=PlatformSeededSimulator.Point(hb.position),["capsule"]=Walk(a,b)});
            }return rows;
        }
        JObject Walk(Vector3 start,Vector3 target)
        {
            var go=new GameObject("Car04 source route capsule diagnostic",typeof(CharacterController));go.layer=2;var cc=go.GetComponent<CharacterController>();cc.height=1.72f;cc.radius=.28f;cc.center=Vector3.up*.86f;cc.skinWidth=.025f;cc.stepOffset=.28f;cc.slopeLimit=36;go.transform.position=start+Vector3.up*.04f;Physics.SyncTransforms();float vertical=0;CollisionFlags flags=CollisionFlags.None;
            for(int i=0;i<1600;i++){var d=target-go.transform.position;d.y=0;if(d.magnitude<.15f&&Mathf.Abs(go.transform.position.y-target.y)<.2f)break;if(cc.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-30,vertical-9.81f*.02f);flags|=cc.Move(d.normalized*.024f+Vector3.up*vertical*.02f);}
            float error=Vector3.Distance(go.transform.position,target);var r=new JObject{["end"]=PlatformSeededSimulator.Point(go.transform.position),["distance_to_target_m"]=error,["passed"]=error<.3f,["collision_flags"]=flags.ToString(),["radius_m"]=cc.radius,["height_m"]=cc.height};DestroyImmediate(go);return r;
        }
        bool cleanupExact=true;
        void Cleanup()
        {
            RemoveIntercarLinks();SetCarvers(false);if(floorNavInstance.valid)floorNavInstance.Remove();if(floorNav!=null)DestroyImmediate(floorNav);floorNav=null;if(floorNavMesh!=null)DestroyImmediate(floorNavMesh);floorNavMesh=null;navigation?.Dispose();navigation=null;foreach(var leaf in leaves)if(leaf.Object!=null)DestroyImmediate(leaf.Object);leaves.Clear();
            foreach(var c in copies){if(c.Filter!=null){c.Filter.sharedMesh=c.Original;c.Collider.sharedMesh=c.Original;cleanupExact&=c.Filter.sharedMesh==c.Original&&c.Collider.sharedMesh==c.Original&&c.Filter.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(c.Materials);}if(c.Kept!=null)DestroyImmediate(c.Kept);foreach(var m in c.Moving)if(m!=null)DestroyImmediate(m);}copies.Clear();Physics.SyncTransforms();
        }
        void OnDestroy()=>Cleanup();
    }
}
