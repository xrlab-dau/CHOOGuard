#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
using Object=UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    // Root-local replacement fixture only. No Scene/mesh/operator settings changes. Retains24 operators/28leaves, isolates all24 before each of the2 electrical motion checks and4 crossings. All20 stall routes are outside this bounded result; this cannot establish the1F whole-space pass.
    public sealed class Floor1ElectricalRearWallPlayTests
    {
        const string Packet="workers/twin/output/floor1-architecture-20261009-pass2/electrical-runtime-source-fixture.json";
        const string ExpectedPacketSha="4c93dbcc63df85677aebbe6bea069566105576e2e98dc02513212b4c234d0aa2";
        const string Output=".planning/2026-10-09-interior-99-reconstruction/floor1-electrical-rear-wall-runtime-proof.json";
        static JArray V(Vector3 p)=>new JArray(p.x,p.y,p.z);
        static Vector3 ReadV(JToken q)=>new Vector3((float)q[0],(float)q[1],(float)q[2]);
        static string PathOf(Transform t){var names=new List<string>();while(t!=null){names.Add(t.name);t=t.parent;}names.Reverse();return string.Join("/",names);}
        static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        static string Digest(Mesh m)
        {
            using(var stream=new MemoryStream())
            {
                using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    foreach(var v in m.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}foreach(var v in m.normals){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                    foreach(var v in m.tangents){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}foreach(var v in m.colors){w.Write(v.r);w.Write(v.g);w.Write(v.b);w.Write(v.a);}
                    for(int u=0;u<8;u++){var uv=new List<Vector4>();m.GetUVs(u,uv);w.Write(uv.Count);foreach(var v in uv){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}
                    w.Write(m.subMeshCount);for(int s=0;s<m.subMeshCount;s++){w.Write((int)m.GetTopology(s));foreach(int i in m.GetIndices(s))w.Write(i);}
                }
                return Hash(stream.ToArray());
            }
        }
        static Vector3 World(JToken q,Transform floor)=>floor.TransformPoint(new Vector3((float)q[1],(float)q[2],(float)q[0]));
        static RaycastHit[] Support(Vector3 p,CharacterController cc,HashSet<Collider> self)
        {
            var hits=new RaycastHit[9];
            for(int ray=0;ray<9;ray++)
            {
                int k=ray-1;Vector3 d=k<0?Vector3.zero:new Vector3(Mathf.Cos(k*Mathf.PI/4),0,Mathf.Sin(k*Mathf.PI/4))*cc.radius*.92f;
                // Pick the first exposed surface before evaluating height/normal/owner.
                // A raised KWc pan remains a failed floor ray, even if floor lies below it.
                hits[ray]=Physics.RaycastAll(p+d+Vector3.up*.7f,Vector3.down,1.4f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!self.Contains(h.collider)).OrderBy(h=>h.distance).FirstOrDefault();
            }
            return hits;
        }
        static bool ReviewedSurfaceHit(RaycastHit hit,JToken triangle)
        {
            Vector3 a=ReadV(triangle[0]),b=ReadV(triangle[1]),c=ReadV(triangle[2]);
            Vector3 ab=b-a,ac=c-a,ap=hit.point-a,normal=Vector3.Cross(ab,ac).normalized;
            if(Mathf.Abs(Vector3.Dot(ap,normal))>.0004f||Vector3.Dot(hit.normal.normalized,normal)<.9999f)return false;
            float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ac,ac),cross=Vector3.Dot(ab,ac),denominator=aa*bb-cross*cross;
            if(Mathf.Abs(denominator)<1e-10f)return false;
            float along=(bb*Vector3.Dot(ap,ab)-cross*Vector3.Dot(ap,ac))/denominator;
            float across=(aa*Vector3.Dot(ap,ac)-cross*Vector3.Dot(ap,ab))/denominator;
            return along>=-.000005f&&across>=-.000005f&&along+across<=1.000005f;
        }
        static bool SameOrientedTriangle(Vector3[] vertices,int[] indices,int offset,JToken expected)
        {
            for(int shift=0;shift<3;shift++)
                if(Enumerable.Range(0,3).All(k=>Vector3.Distance(vertices[indices[offset+(k+shift)%3]],ReadV(expected[k]))<.0003f))return true;
            return false;
        }
        static bool Allowed(RaycastHit h,JToken route)
        {
            if(h.collider==null)return false;
            string owner=PathOf(h.collider.transform);
            if(((JArray)route["allowed_floor_owner_prefixes"]).Any(x=>owner==(string)x))return true;
            if(route["native_floor_support"] is JArray native)
                return native.Any(x=>owner==(string)x["path"]&&((JArray)x["triangles_world"]).Any(t=>ReviewedSurfaceHit(h,t)));
            return false;
        }
        static bool FloorGood(RaycastHit[] hits,float y,JToken route)=>hits.Length==9&&hits.All(h=>h.collider!=null&&h.normal.y>.9f&&Mathf.Abs(h.point.y-y)<.08f&&Allowed(h,route));
        static JArray SupportRows(RaycastHit[] hits)=>new JArray(hits.Select((h,i)=>new JObject{
            ["ray"]=i,["owner"]=h.collider==null?null:PathOf(h.collider.transform),["height"]=h.collider==null?(JToken)JValue.CreateNull():h.point.y,
            ["normal"]=V(h.normal),["triangle_index"]=h.triangleIndex,["triangle_index_role"]="diagnostic_only",["point"]=V(h.point)}));
        static bool Closed(StationDoor door,Dictionary<StationDoor.Leaf,Quaternion> baseline)=>door.Open<.02f&&door.Leaves.All(l=>Quaternion.Angle(baseline[l],l.Pivot.localRotation)<.5f);
        static IEnumerator CloseAll(StationDoor[] doors,FirstPersonResponder responder,Vector3 park,Dictionary<StationDoor.Leaf,Quaternion> baseline,JArray receipts,string phase)
        {
            // Fixture placement precedes each isolated test. No physical traversal claim
            // is made for this placement; all traversal movement uses the actual CC.
            responder.RestorePhysicalPose(park+Vector3.up*.04f,0,0);Physics.SyncTransforms();
            var requested=new JArray();
            foreach(var d in doors)if(d.Open>.02f)
            {
                bool accepted=d.TryInteract(responder,out string feedback);
                requested.Add(new JObject{["id"]=d.Element,["accepted"]=accepted,["feedback"]=feedback});
            }
            yield return new WaitForSeconds(2.8f);Physics.SyncTransforms();
            receipts.Add(new JObject{["phase"]=phase,["requests"]=requested,["operators"]=new JArray(doors.Select(d=>new JObject{["id"]=d.Element,["open"]=d.Open,["passed"]=Closed(d,baseline)})),["passed"]=doors.All(d=>Closed(d,baseline))});
        }
        static IEnumerator ClosedBarrier(Vector3[] points,JToken route,FirstPersonResponder responder,CharacterController cc,HashSet<Collider> self,JArray checks,bool reverse)
        {
            responder.RestorePhysicalPose(points[0]+Vector3.up*.04f,0,0);Physics.SyncTransforms();for(int j=0;j<6;j++){cc.Move(Vector3.down*.08f);yield return null;}
            bool floorValid=true;float maximumError=0;var samples=new JArray();
            int n=Mathf.CeilToInt(Vector3.Distance(points[0],points.Last())/.04f);
            for(int k=0;k<=n;k++)
            {
                Vector3 target=Vector3.Lerp(points[0],points.Last(),(float)k/n);var support=Support(target,cc,self);floorValid&=FloorGood(support,target.y,route);if(!floorValid)break;
                Vector3 move=target-responder.transform.position;move.y=0;move=Vector3.ClampMagnitude(move,.04f);move.y=-.08f;cc.Move(move);yield return null;
                var actualSupport=Support(responder.transform.position,cc,self);bool actualFloor=FloorGood(actualSupport,target.y,route);floorValid&=actualFloor&&cc.isGrounded;
                Vector3 error=responder.transform.position-target;error.y=0;maximumError=Mathf.Max(maximumError,error.magnitude);
                samples.Add(new JObject{["target"]=V(target),["actual"]=V(responder.transform.position),["horizontal_error"]=error.magnitude,["grounded"]=cc.isGrounded,["actual_floor_supported_footprint"]=actualFloor,["support"]=SupportRows(support),["actual_support"]=SupportRows(actualSupport)});
                if(error.magnitude>.32f)break;
            }
            Vector3 final=responder.transform.position-points.Last();final.y=0;
            checks.Add(new JObject{["route"]=route["id"],["reverse"]=reverse,["reviewed_floor_under_requested_route"]=floorValid,["closed_barrier_observed"]=maximumError>.25f&&final.magnitude>.35f,["maximum_horizontal_error"]=maximumError,["endpoint_error"]=final.magnitude,["samples"]=samples,["passed"]=floorValid&&maximumError>.25f&&final.magnitude>.35f});
        }
        static void VerifyNativeFloorContracts(JObject packet)
        {
            foreach(var contract in (JArray)packet["native_floor_contracts"])
            {
                Assert.That(Hash(File.ReadAllBytes((string)contract["asset"])),Is.EqualTo((string)contract["asset_sha256"]),"Original floor source changed");
                var collider=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Single(c=>PathOf(c.transform)==(string)contract["path"]);
                Assert.That(collider.enabled&&collider.gameObject.activeInHierarchy&&!collider.isTrigger&&!collider.convex,Is.True);
                for(int r=0;r<4;r++)for(int c=0;c<4;c++)Assert.That(Mathf.Abs(collider.transform.localToWorldMatrix[r,c]-(float)contract["world_matrix"][r][c]),Is.LessThan(.00025f));
                var mesh=collider.sharedMesh;Assert.That(mesh.isReadable,Is.True);var vertices=mesh.vertices.Select(v=>collider.transform.TransformPoint(v)).ToArray();var indices=Enumerable.Range(0,mesh.subMeshCount).SelectMany(s=>mesh.GetIndices(s)).ToArray();
                foreach(var triangle in (JArray)contract["triangles"])
                {
                    bool present=false;
                    for(int offset=0;offset<indices.Length;offset+=3)if(SameOrientedTriangle(vertices,indices,offset,triangle["world"])){present=true;break;}
                    Assert.That(present,Is.True,"Reviewed original world surface changed; triangle ordinal is diagnostic only");
                }
            }
        }

        [UnityTest,Timeout(600000)]
        public IEnumerator BothSourceElectricalRearPortalsMoveAndSupportActualBidirectionalControllerCrossings()
        {
            Assert.That(Hash(File.ReadAllBytes(Packet)),Is.EqualTo(ExpectedPacketSha),"Frozen bounded service fixture changed");var packet=JObject.Parse(File.ReadAllText(Packet));var motion=new JArray();var traversals=new JArray();var isolated=new JArray();var closedReturn=new JArray();var closedBarriers=new JArray();
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;
            int beforeCapture=Time.captureFramerate;FirstPersonResponder responder=null;Vector3 beforePosition=default;Quaternion beforeRotation=Quaternion.identity;float beforePitch=0;bool restore=false,beforeExternal=false,beforePause=false;
            try
            {
                Time.captureFramerate=30;
                Assert.That(Hash(File.ReadAllBytes((string)packet["source_spec"])),Is.EqualTo((string)packet["source_spec_sha256"]),"Frozen source-cell geometry packet changed");
                VerifyNativeFloorContracts(packet);
                var floor=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>PathOf(f.transform)==(string)packet["source_floor_path"]);
                var frame=packet["source_floor_world_matrix"];for(int r=0;r<4;r++)for(int c=0;c<4;c++)Assert.That(Mathf.Abs(floor.transform.localToWorldMatrix[r,c]-(float)frame[r][c]),Is.LessThan(.000025f));
                var floorCollider=floor.GetComponent<MeshCollider>();Assert.That(floorCollider,Is.Not.Null);var originalFloorMesh=floorCollider.sharedMesh;Assert.That(originalFloorMesh.isReadable,Is.True);var originalFloorDigest=Digest(originalFloorMesh);
                responder=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsSortMode.None).Single();var cc=responder.GetComponent<CharacterController>();
                Assert.That(cc,Is.Not.Null);Assert.That(cc.radius,Is.EqualTo(.28f).Within(.0001f));Assert.That(cc.height,Is.EqualTo(1.72f).Within(.0001f));
                beforePosition=responder.transform.position;beforeRotation=responder.transform.rotation;beforePitch=responder.PitchDegrees;beforeExternal=responder.ExternalInputMode;beforePause=responder.IsPaused;restore=true;
                responder.SetExternalInputMode(true);responder.Pause();var self=new HashSet<Collider>(responder.GetComponentsInChildren<Collider>(true));
                var all=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None);var root=(string)packet["new_root"];
                var doors=((JArray)packet["operators"]).Select(p=>all.Single(d=>d.Element==(string)p["id"]&&PathOf(d.transform).StartsWith(root+"/",StringComparison.Ordinal))).ToArray();
                Assert.That(doors.Length,Is.EqualTo(24));Assert.That(doors.Sum(d=>d.Leaves.Length),Is.EqualTo(28));
                var baseline=doors.SelectMany(d=>d.Leaves).ToDictionary(l=>l,l=>l.ClosedRotation);
                var meshes=doors.SelectMany(d=>d.GetComponentsInChildren<MeshFilter>(true)).Distinct().ToDictionary(f=>f,f=>Digest(f.sharedMesh));
                var priorMeshes=all.Where(d=>!doors.Contains(d)).SelectMany(d=>d.GetComponentsInChildren<MeshFilter>(true)).Distinct().ToDictionary(f=>f,f=>(mesh:f.sharedMesh,digest:Digest(f.sharedMesh)));
                var park=World(packet["independent_park_UVY"],floor.transform);
                foreach(var door in doors.Where(d=>d.Element=="electric-rear-double-a"||d.Element=="electric-rear-double-b"))
                {
                    yield return CloseAll(doors,responder,park,baseline,isolated,"motion:"+door.Element);
                    bool isolatedGood=(bool)isolated.Last["passed"];
                    var expected=((JArray)packet["operators"]).Single(p=>(string)p["id"]==door.Element);
                    float centreError=Vector3.Distance(door.World(Vector3.zero),World(expected["source_centre_UVY"],floor.transform));
                    bool bindings=door.Kind==StationDoor.DoorKind.Swing&&door.Index==0&&centreError<.04f&&door.Leaves.Length==(int)expected["leaves"]&&door.Leaves.All(l=>l.Pivot!=null&&l.Pivot.GetComponent<BoxCollider>()!=null);
                    string feedback=null;bool request=isolatedGood&&door.TryInteract(responder,out feedback);
                    yield return new WaitForSeconds(1.5f);Physics.SyncTransforms();
                    var leafRows=new JArray(door.Leaves.Select(l=>new JObject{["pivot"]=PathOf(l.Pivot),["angle_degrees"]=Quaternion.Angle(baseline[l],l.Pivot.localRotation),["configured_swing_degrees"]=l.Swing,["collider_active"]=l.Pivot.GetComponent<BoxCollider>().enabled}));
                    // Both electrical double leaves must reach85deg; tiny motion is a failure.
                    bool passed=isolatedGood&&door.Usable&&bindings&&request&&door.Open>.95f&&leafRows.All(l=>(float)l["angle_degrees"]>85&&(bool)l["collider_active"]);
                    motion.Add(new JObject{["id"]=door.Element,["actual_instance_id"]=door.GetInstanceID(),["source_centre_numerical_binding_error"]=centreError,["usable"]=door.Usable,["sealed"]=door.Sealed,["bindings_valid"]=bindings,["operator_isolated"]=isolatedGood,["request_accepted"]=request,["feedback"]=feedback,["open"]=door.Open,["leaves"]=leafRows,["passed"]=passed});
                }
                foreach(var route in ((JArray)packet["routes"]).Where(r=>((string)r["id"]).StartsWith("electric-original-rear-",StringComparison.Ordinal)))
                foreach(bool reverse in new[]{false,true})
                {
                    yield return CloseAll(doors,responder,park,baseline,isolated,"route:"+(string)route["id"]+":"+reverse);
                    bool pass=(bool)isolated.Last["passed"];string reason=pass?null:"neighbor source operators did not return closed";
                    string operatorId=(string)route["operator"];var oper=string.IsNullOrEmpty(operatorId)?null:doors.Single(d=>d.Element==operatorId);
                    bool requested=oper==null;string feedback=null;
                    var closedPoints=((JArray)route["path_UVY"]).Select(q=>World(q,floor.transform)).ToArray();if(reverse)Array.Reverse(closedPoints);
                    if(pass){yield return ClosedBarrier(closedPoints,route,responder,cc,self,closedBarriers,reverse);responder.RestorePhysicalPose(park+Vector3.up*.04f,0,0);Physics.SyncTransforms();}
                    if(oper!=null&&pass){requested=oper.TryInteract(responder,out feedback);yield return new WaitForSeconds(1.4f);Physics.SyncTransforms();}
                    bool doorOpen=oper==null||(requested&&oper.Open>.95f&&oper.Leaves.All(l=>Quaternion.Angle(baseline[l],l.Pivot.localRotation)>85));
                    var points=((JArray)route["path_UVY"]).Select(q=>World(q,floor.transform)).ToArray();if(reverse)Array.Reverse(points);
                    var samples=new JArray();bool geometryAccepted=route["fixture_endpoint_review"]==null||(bool)route["fixture_endpoint_review"]["whole_capsule_clearance_geometry"];
                    if(pass)
                    {
                        responder.RestorePhysicalPose(points[0]+Vector3.up*.04f,0,0);Physics.SyncTransforms();for(int j=0;j<6;j++){cc.Move(Vector3.down*.08f);yield return null;}
                        for(int j=1;j<points.Length&&pass;j++)
                        {
                            int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[j-1],points[j])/.04f));
                            for(int k=0;k<=n;k++)
                            {
                                Vector3 target=Vector3.Lerp(points[j-1],points[j],(float)k/n);var support=Support(target,cc,self);bool targetFloor=FloorGood(support,target.y,route);
                                CollisionFlags flags=CollisionFlags.None;
                                if(targetFloor){Vector3 move=target-responder.transform.position;move.y=0;move=Vector3.ClampMagnitude(move,.04f);move.y=-.08f;flags=cc.Move(move);}yield return null;
                                var actualSupport=Support(responder.transform.position,cc,self);bool actualFloor=FloorGood(actualSupport,target.y,route);
                                Vector3 error=responder.transform.position-target;error.y=0;
                                bool clear=targetFloor&&actualFloor&&error.magnitude<.08f&&Mathf.Abs(responder.transform.position.y-target.y)<.12f&&cc.isGrounded;
                                var blockers=Physics.OverlapCapsule(target+Vector3.up*cc.radius,target+Vector3.up*(cc.height-cc.radius),cc.radius,~0,QueryTriggerInteraction.Ignore).Where(c=>!self.Contains(c)).Select(c=>PathOf(c.transform)).Distinct().ToArray();
                                samples.Add(new JObject{["target"]=V(target),["actual"]=V(responder.transform.position),["horizontal_error"]=error.magnitude,["collision_flags"]=(int)flags,["grounded"]=cc.isGrounded,["floor_supported_footprint"]=targetFloor,["actual_floor_supported_footprint"]=actualFloor,["support"]=SupportRows(support),["actual_support"]=SupportRows(actualSupport),["capsule_contacts"]=JArray.FromObject(blockers),["passed"]=clear});
                                if(!clear){pass=false;reason=!targetFloor||!actualFloor?"first exposed footprint lacks reviewed floor support":"actual player capsule blocked or not grounded";break;}
                            }
                        }
                    }
                    Vector3 finalError=responder.transform.position-points.Last();finalError.y=0;
                    bool endpointReached=pass&&finalError.magnitude<.055f;
                    if(pass&&!endpointReached){pass=false;reason="final actual foot did not reach source endpoint";}
                    bool accepted=pass&&doorOpen&&geometryAccepted;
                    if(pass&&!doorOpen)reason="operator did not fully perform modeled opening";
                    if(pass&&!geometryAccepted)reason="unresolved PDF-cell full-capsule endpoint; diagnostic traversal only";
                    traversals.Add(new JObject{["route"]=route["id"],["kind"]=route["kind"],["reverse"]=reverse,["source_pixels"]=route["source_path_pixels"].DeepClone(),["fixture_endpoint_review"]=route["fixture_endpoint_review"]?.DeepClone(),["operator_request_accepted"]=requested,["operator_feedback"]=feedback,["door_motion_sufficient"]=doorOpen,["physical_walk_passed"]=pass,["endpoint_reached"]=endpointReached,["source_endpoint_accepted"]=geometryAccepted,["passed"]=accepted,["failure"]=reason,["samples"]=samples});
                }
                yield return CloseAll(doors,responder,park,baseline,isolated,"final");
                foreach(var door in doors)closedReturn.Add(new JObject{["id"]=door.Element,["passed"]=Closed(door,baseline)});
                bool preserved=floorCollider.sharedMesh==originalFloorMesh&&Digest(floorCollider.sharedMesh)==originalFloorDigest&&meshes.All(x=>x.Key!=null&&Digest(x.Key.sharedMesh)==x.Value)&&priorMeshes.All(x=>x.Key!=null&&x.Key.sharedMesh==x.Value.mesh&&Digest(x.Key.sharedMesh)==x.Value.digest);
                VerifyNativeFloorContracts(packet);
                var proof=new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["packet_sha256"]=Hash(File.ReadAllBytes(Packet)),["saved_scene_sha256"]=Hash(File.ReadAllBytes("Assets/ChooGuard/Scenes/FpsStation.unity")),["actual_character_controller"]=PathOf(cc.transform),["radius"]=cc.radius,["height"]=cc.height,["stock_parameters_unverified_at_site"]=true,["source_operator_count"]=doors.Length,["source_leaf_count"]=doors.Sum(d=>d.Leaves.Length),["closed_barrier_checks"]=closedBarriers,["two_electrical_operator_motions"]=motion,["closed_surface_return"]=closedReturn,["operator_isolation"]=isolated,["actual_bidirectional_source_crossings"]=traversals,["expected_traversal_count"]=4,["actual_traversal_count"]=traversals.Count,["stall_routes_executed"]=0,["whole1F_unresolved_stall_fixture_count"]=12,["whole1F_complete"]=false,["original_floor_prior_leaf_geometry_unchanged"]=preserved,["field_metric_accuracy_verified"]=false,["passed"]=closedBarriers.Count==4&&closedBarriers.All(x=>(bool)x["passed"])&&preserved&&traversals.Count==4&&motion.Count==2&&motion.All(x=>(bool)x["passed"])&&closedReturn.All(x=>(bool)x["passed"])&&isolated.All(x=>(bool)x["passed"])&&traversals.All(x=>(bool)x["passed"])};
                File.WriteAllText(Output,proof.ToString());Assert.That((bool)proof["passed"],Is.True,"Actual source floor/door/crossing or unresolved endpoint contract failed; inspect "+Output);
            }
            finally
            {
                if(restore&&responder!=null){responder.RestorePhysicalPose(beforePosition,beforeRotation.eulerAngles.y,beforePitch);responder.SetExternalInputMode(beforeExternal);if(beforePause)responder.Pause();else responder.Resume(false);}
                Time.captureFramerate=beforeCapture;
            }
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureFramerate=0;Time.timeScale=1;var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("SourcePdf1FScratch"));yield return SceneManager.UnloadSceneAsync(scene);}}
    }
}
#endif
