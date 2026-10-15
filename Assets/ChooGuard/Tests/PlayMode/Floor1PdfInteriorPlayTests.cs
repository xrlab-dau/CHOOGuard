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
    // Actual saved scene, actual player CharacterController, actual StationDoor APIs.
    // Route coordinates come from frozen PDF controls, independently of door.World().
    public sealed class Floor1PdfInteriorPlayTestsDraft
    {
        const string Packet="workers/twin/output/floor1-provided-pdf-20261009/runtime-source-crossing-packet.json";
        const string Output=".planning/2026-10-09-floor2-completion-pass2/floor1-pdf-runtime-proof.json";
        static JArray V(Vector3 p)=>new JArray(p.x,p.y,p.z);
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
        static RaycastHit[] Support(Vector3 p,CharacterController cc)
        {
            var hits=new List<RaycastHit>();
            for(int k=-1;k<8;k++)
            {
                Vector3 d=k<0?Vector3.zero:new Vector3(Mathf.Cos(k*Mathf.PI/4),0,Mathf.Sin(k*Mathf.PI/4))*cc.radius*.92f;
                var h=Physics.RaycastAll(p+d+Vector3.up*.7f,Vector3.down,1.4f,~0,QueryTriggerInteraction.Ignore).Where(x=>x.collider!=cc&&x.normal.y>.9f).OrderBy(x=>x.distance).ToArray();
                if(h.Length>0)hits.Add(h[0]);
            }
            return hits.ToArray();
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator SourceRoomsSupportActualControllerCrossingsAndAll24OperatorsMoveTheir28Leaves()
        {
            var packet=JObject.Parse(File.ReadAllText(Packet));var motion=new JArray();var traversals=new JArray();var closedReturn=new JArray();
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;
            Time.captureFramerate=30;
            var floor=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>PathOf(f.transform)==(string)packet["source_floor_path"]);
            var frame=packet["source_floor_world_matrix"];for(int r=0;r<4;r++)for(int c=0;c<4;c++)Assert.That(Mathf.Abs(floor.transform.localToWorldMatrix[r,c]-(float)frame[r][c]),Is.LessThan(.000025f));
            var floorCollider=floor.GetComponent<MeshCollider>();Assert.That(floorCollider,Is.Not.Null);var originalFloorMesh=floorCollider.sharedMesh;Assert.That(originalFloorMesh.isReadable,Is.True);var originalFloorDigest=Digest(originalFloorMesh);
            var responder=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsSortMode.None).Single();var cc=responder.GetComponent<CharacterController>();
            Assert.That(cc,Is.Not.Null);Assert.That(cc.radius,Is.EqualTo(.28f).Within(.0001f));Assert.That(cc.height,Is.EqualTo(1.72f).Within(.0001f));
            var beforePosition=responder.transform.position;var beforeRotation=responder.transform.rotation;bool beforeExternal=responder.ExternalInputMode,beforePause=responder.IsPaused;
            responder.SetExternalInputMode(true);responder.Pause();
            var all=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None);var root=(string)packet["new_root"];
            var doors=((JArray)packet["operators"]).Select(p=>all.Single(d=>d.Element==(string)p["id"]&&PathOf(d.transform).StartsWith(root+"/",StringComparison.Ordinal))).ToArray();
            Assert.That(doors.Length,Is.EqualTo(24));Assert.That(doors.Sum(d=>d.Leaves.Length),Is.EqualTo(28));
            var leafBefore=doors.SelectMany(d=>d.Leaves).ToDictionary(l=>l,l=>l.Pivot.localRotation);
            var meshes=doors.SelectMany(d=>d.Leaves).Select(l=>l.Pivot.GetComponent<MeshFilter>()).ToDictionary(f=>f,f=>Digest(f.sharedMesh));
            var priorMeshes=all.Where(d=>!doors.Contains(d)).SelectMany(d=>d.GetComponentsInChildren<MeshFilter>(true)).Distinct().ToDictionary(f=>f,f=>f.sharedMesh);
            var park=World(packet["independent_park_UVY"],floor.transform);
            try
            {
                responder.RestorePhysicalPose(park+Vector3.up*.04f,beforeRotation.eulerAngles.y,0);Physics.SyncTransforms();
                foreach(var door in doors)
                {
                    var expected=((JArray)packet["operators"]).Single(p=>(string)p["id"]==door.Element);
                    float centreError=Vector3.Distance(door.World(Vector3.zero),World(expected["source_centre_UVY"],floor.transform));
                    bool bindings=door.Kind==StationDoor.DoorKind.Swing&&door.Index==0&&centreError<.04f&&door.Leaves.Length==(int)expected["leaves"]&&door.Leaves.All(l=>l.Pivot!=null&&l.Pivot.GetComponent<BoxCollider>()!=null);
                    bool request=door.TryInteract(responder,out string feedback);
                    motion.Add(new JObject{["id"]=door.Element,["actual_instance_id"]=door.GetInstanceID(),["actual_index"]=door.Index,["source_centre_numerical_binding_error"]=centreError,["usable"]=door.Usable,["sealed"]=door.Sealed,["actual_leaves"]=door.Leaves.Length,["bindings_valid"]=bindings,["request_accepted"]=request,["feedback"]=feedback});
                }
                yield return new WaitForSeconds(1.5f);Physics.SyncTransforms();
                for(int i=0;i<doors.Length;i++)
                {
                    var door=doors[i];var leafRows=new JArray(door.Leaves.Select(l=>new JObject{["pivot"]=PathOf(l.Pivot),["angle_degrees"]=Quaternion.Angle(leafBefore[l],l.Pivot.localRotation),["collider_active"]=l.Pivot.GetComponent<BoxCollider>().enabled}));
                    var row=motion[i];row["open"]=door.Open;row["leaves"]=leafRows;row["passed"]=(bool)row["usable"]&&(bool)row["bindings_valid"]&&(bool)row["request_accepted"]&&door.Open>.95f&&leafRows.All(l=>(float)l["angle_degrees"]>5&&(bool)l["collider_active"]);
                    if(door.Open>.05f)door.TryInteract(responder,out _);
                }
                yield return new WaitForSeconds(2.8f);Physics.SyncTransforms();
                foreach(var door in doors)closedReturn.Add(new JObject{["id"]=door.Element,["passed"]=door.Open<.02f&&door.Leaves.All(l=>Quaternion.Angle(leafBefore[l],l.Pivot.localRotation)<.5f)});
                // Each path starts/ends at reviewed PDF pixel-derived anchors, never an
                // offset guessed from the installed operator. Includes rear electrical
                // source passages, four WC public mouths and all twenty stall fronts.
                foreach(var route in (JArray)packet["routes"])
                {
                    string operatorId=(string)route["operator"];var oper=string.IsNullOrEmpty(operatorId)?null:doors.Single(d=>d.Element==operatorId);
                    responder.RestorePhysicalPose(park+Vector3.up*.04f,0,0);Physics.SyncTransforms();
                    if(oper!=null){if(oper.Open>.05f){oper.TryInteract(responder,out _);yield return new WaitForSeconds(2.8f);}oper.TryInteract(responder,out _);yield return new WaitForSeconds(1.4f);}
                    foreach(bool reverse in new[]{false,true})
                    {
                        var points=((JArray)route["path_UVY"]).Select(q=>World(q,floor.transform)).ToArray();if(reverse)Array.Reverse(points);
                        responder.RestorePhysicalPose(points[0]+Vector3.up*.04f,0,0);for(int j=0;j<6;j++){cc.Move(Vector3.down*.08f);yield return null;}
                        var samples=new JArray();bool pass=true;string reason=null;
                        for(int j=1;j<points.Length&&pass;j++)
                        {
                            int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[j-1],points[j])/.08f));
                            for(int k=0;k<=n;k++)
                            {
                                Vector3 target=Vector3.Lerp(points[j-1],points[j],(float)k/n);var support=Support(target,cc);var allowed=((JArray)route["allowed_floor_owner_prefixes"]).Select(x=>(string)x).ToArray();
                                bool floorGood=support.Length==9&&support.All(h=>Mathf.Abs(h.point.y-target.y)<.08f&&allowed.Any(a=>PathOf(h.collider.transform).StartsWith(a,StringComparison.Ordinal)));
                                CollisionFlags flags=CollisionFlags.None;if(floorGood){Vector3 move=target-responder.transform.position;move.y=-.08f;flags=cc.Move(move);}yield return null;
                                Vector3 error=responder.transform.position-target;error.y=0;
                                bool clear=floorGood&&error.magnitude<.13f&&Mathf.Abs(responder.transform.position.y-target.y)<.12f&&cc.isGrounded;
                                var blockers=Physics.OverlapCapsule(target+Vector3.up*cc.radius,target+Vector3.up*(cc.height-cc.radius),cc.radius,~0,QueryTriggerInteraction.Ignore).Where(c=>c!=cc).Select(c=>PathOf(c.transform)).Distinct().ToArray();
                                samples.Add(new JObject{["target"]=V(target),["actual"]=V(responder.transform.position),["horizontal_error"]=error.magnitude,["collision_flags"]=(int)flags,["grounded"]=cc.isGrounded,["floor_supported_footprint"]=floorGood,["support"]=new JArray(support.Select(h=>new JObject{["owner"]=PathOf(h.collider.transform),["height"]=h.point.y})),["capsule_contacts"]=JArray.FromObject(blockers),["passed"]=clear});
                                if(!clear){pass=false;reason=!floorGood?"missing/unreviewed floor support":"actual player capsule blocked or not grounded";break;}
                            }
                        }
                        traversals.Add(new JObject{["route"]=route["id"],["kind"]=route["kind"],["reverse"]=reverse,["source_pixels"]=route["source_path_pixels"].DeepClone(),["passed"]=pass,["failure"]=reason,["samples"]=samples});
                    }
                }
                bool preserved=floorCollider.sharedMesh==originalFloorMesh&&Digest(floorCollider.sharedMesh)==originalFloorDigest&&meshes.All(x=>Digest(x.Key.sharedMesh)==x.Value)&&priorMeshes.All(x=>x.Key!=null&&x.Key.sharedMesh==x.Value);
                var proof=new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["packet_sha256"]=Hash(File.ReadAllBytes(Packet)),["saved_scene_sha256"]=Hash(File.ReadAllBytes("Assets/ChooGuard/Scenes/FpsStation.unity")),["actual_character_controller"]=PathOf(cc.transform),["radius"]=cc.radius,["height"]=cc.height,["stock_parameters_unverified_at_site"]=true,["all24operators28leaves"]=motion,["closed_surface_return"]=closedReturn,["actual_bidirectional_source_crossings"]=traversals,["original_floor_prior_leaf_geometry_unchanged"]=preserved,["field_metric_accuracy_verified"]=false,["passed"]=preserved&&motion.All(x=>(bool)x["passed"])&&closedReturn.All(x=>(bool)x["passed"])&&traversals.All(x=>(bool)x["passed"])};
                File.WriteAllText(Output,proof.ToString());Assert.That((bool)proof["passed"],Is.True,"Actual source door/floor crossings failed; inspect "+Output);
            }
            finally{responder.RestorePhysicalPose(beforePosition,beforeRotation.eulerAngles.y,0);responder.SetExternalInputMode(beforeExternal);if(beforePause)responder.Pause();else responder.Resume(false);Time.captureFramerate=0;}
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureFramerate=0;Time.timeScale=1;var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("SourcePdf1FScratch"));yield return SceneManager.UnloadSceneAsync(scene);}}
    }
}
#endif
