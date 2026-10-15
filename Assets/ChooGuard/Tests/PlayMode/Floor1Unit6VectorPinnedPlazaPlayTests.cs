#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
#if UNITY_EDITOR
using UnityEditor;

using System.Security.Cryptography;
#endif
using Object=UnityEngine.Object;
namespace ChooGuard.Tests.PlayMode
{
    public sealed class Floor1Unit6VectorPinnedPlazaPlayTests
    {
        const string Work="workers/twin/output/floor1-source-completion-20261009-pass3";
        static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
        static JArray J(Vector3 a)=>new JArray(a.x,a.y,a.z);
        static string PathOf(Transform t){var p=new List<string>();while(t!=null){p.Add(t.name);t=t.parent;}p.Reverse();return string.Join("/",p);}
        const string FloorPin=".planning/2026-10-09-interior-99-reconstruction/pass3-integration/unit6-floor-pins-v2.json";
        static readonly string[] FloorOwners={"1층 · 원본 정합/Ground_Floor","실내 트윈 마감/점포 내부 1F/Kit_Colliders","실내 트윈 마감/1F/Kit_Colliders","FPSWorld/공식 자료 부산역 주변 공간/ContextPlaza/OfficialStation_-유라시아 도로_0"};
        static readonly HashSet<Collider> approvedFloors=new HashSet<Collider>();
        static string approvedFloorPinHash;
        static JObject approvedFloorEvidence;
        static string FileHash(string path)
        {
#if UNITY_EDITOR
            using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();
#else
            throw new InvalidOperationException("Pinned source asset verification requires Editor test runner");
#endif
        }
        static string CompleteDigest(Mesh mesh) { var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ChooGuard.Editor.Twin.ArchitecturalCorrectionRegistry",false)).Single(t=>t!=null); return (string)type.GetMethod("CompleteChannelDigest",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{mesh}); }
        static void LoadAndVerifyFloorPins()
        {
#if UNITY_EDITOR
            approvedFloors.Clear();approvedFloorEvidence=null;
            var pins=JObject.Parse(File.ReadAllText(FloorPin));var rows=(JArray)pins["floor_assets"];
            Assert.That(rows.Count,Is.EqualTo(4));Assert.That(rows.Select(x=>(string)x["owner"]).Distinct().Count(),Is.EqualTo(4));
            Assert.That(rows.Select(x=>(string)x["owner"]),Is.EquivalentTo(FloorOwners));
            Assert.That(FileHash("Assets/ChooGuard/Scenes/FpsStation.unity"),Is.EqualTo((string)pins["scene_sha256"]));
            var colliders=Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var pin in rows)
            {
                var mc=colliders.Single(x=>PathOf(x.transform)==(string)pin["owner"]);
                Assert.That(mc.enabled,Is.True);Assert.That(mc.isTrigger,Is.False);
                Assert.That((bool)pin["enabled"],Is.True);Assert.That((bool)pin["trigger"],Is.False);Assert.That(mc.convex,Is.EqualTo((bool)pin["convex"]));
                var mesh=mc.sharedMesh;Assert.That(mesh,Is.Not.Null);string asset=AssetDatabase.GetAssetPath(mesh);
                Assert.That(asset,Is.EqualTo((string)pin["asset"]));
                Assert.That(AssetDatabase.AssetPathToGUID(asset),Is.EqualTo((string)pin["guid"]));
                Assert.That(FileHash(asset),Is.EqualTo((string)pin["sha256"]));
                Assert.That(CompleteDigest(mesh),Is.EqualTo((string)pin["complete_digest"]));
                var matrix=mc.transform.localToWorldMatrix;var expected=pin["local_to_world_matrix"];
                Assert.That(expected,Is.Not.Null);for(int r=0;r<4;r++)for(int c=0;c<4;c++)Assert.That(Mathf.Abs(matrix[r,c]-(float)expected[r][c]),Is.LessThan(.000025f),"Pinned floor transform changed");
                approvedFloors.Add(mc);
            }
            Assert.That(approvedFloors.Count,Is.EqualTo(4));approvedFloorPinHash=FileHash(FloorPin);approvedFloorEvidence=(JObject)pins.DeepClone();
#else
            throw new InvalidOperationException("Pinned source asset verification requires Editor test runner");
#endif
        }
        static bool FloorGood(Vector3 p,CharacterController cc,HashSet<Collider> self,JArray receipt)
        {
            bool good=true;for(int i=0;i<9;i++)
            {
                var delta=i==0?Vector3.zero:new Vector3(Mathf.Cos((i-1)*Mathf.PI/4),0,Mathf.Sin((i-1)*Mathf.PI/4))*cc.radius*.92f;
                var h=Physics.RaycastAll(p+delta+Vector3.up*.7f,Vector3.down,1.4f,~0,QueryTriggerInteraction.Ignore).Where(x=>!self.Contains(x.collider)).OrderBy(x=>x.distance).FirstOrDefault();
                string owner=h.collider==null?null:PathOf(h.collider.transform);bool allowed=h.collider!=null&&approvedFloors.Contains(h.collider);
                bool ok=h.collider!=null&&h.normal.y>.9f&&Mathf.Abs(h.point.y)<.08f&&allowed;good&=ok;receipt.Add(new JObject{["ray"]=i,["owner"]=owner,["height"]=h.point.y,["normal"]=J(h.normal),["passed"]=ok});
            }return good;
        }
        [UnityTest,Timeout(180000)]public IEnumerator SourceVectorUnit6KeepsItsSlidingLeavesAndActualBidirectionalCrossing()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;int capture=Time.captureFramerate;var responder=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsSortMode.None).Single();var cc=responder.GetComponent<CharacterController>();var before=responder.transform.position;var rot=responder.transform.rotation;float pitch=responder.PitchDegrees;bool external=responder.ExternalInputMode,paused=responder.IsPaused;var rows=new JArray();
            try
            {
                LoadAndVerifyFloorPins();Time.captureFramerate=30;responder.SetExternalInputMode(true);responder.Pause();Assert.That(cc.radius,Is.EqualTo(.28f).Within(.0001f));Assert.That(cc.height,Is.EqualTo(1.72f).Within(.0001f));var self=new HashSet<Collider>(responder.GetComponentsInChildren<Collider>(true));var d=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None).Single(x=>x.Element=="1F-unit06-front3"&&x.Index==0);var e=JObject.Parse(File.ReadAllText(Work+"/unit6-door-aperture-proof.json"));var centre=V(e["new_centre_world"]);Assert.That(Vector3.Distance(d.World(Vector3.zero),centre),Is.LessThan(.001f));Assert.That(d.Width,Is.EqualTo(1.8f).Within(.00001f));Assert.That(d.Kind,Is.EqualTo(StationDoor.DoorKind.Sliding));Assert.That(d.Leaves.Length,Is.EqualTo(2));Assert.That(d.Mode,Is.EqualTo(StationDoor.OperatorMode.Auto));var normal=(d.World(Vector3.forward)-centre).normalized;var park=centre+normal*4.5f;var closed=d.Leaves.Select(x=>x.ClosedPosition).ToArray();bool motion=true,returned=true;
                foreach(bool reverse in new[]{false,true})
                {
                    responder.RestorePhysicalPose(park+Vector3.up*.04f,0,0);Physics.SyncTransforms();yield return new WaitForSeconds(5f);Assert.That(d.Open,Is.LessThan(.02f));
                    var start=centre+normal*(reverse?-.9f:.9f);var end=centre-normal*(reverse?-.9f:.9f);responder.RestorePhysicalPose(start+Vector3.up*.04f,0,0);Physics.SyncTransforms();for(int k=0;k<6;k++){cc.Move(Vector3.down*.08f);yield return null;}yield return new WaitForSeconds(1.4f);Physics.SyncTransforms();bool leaf=d.Open>.99f;
                    for(int i=0;i<2;i++)leaf&=Vector3.Distance(d.Leaves[i].Pivot.localPosition,closed[i]+d.Leaves[i].ClosedRotation*d.Leaves[i].Slide)<.002f&&d.Leaves[i].Pivot.GetComponent<BoxCollider>().enabled;motion&=leaf;var samples=new JArray();bool passed=leaf;int steps=Mathf.CeilToInt(Vector3.Distance(start,end)/.04f);
                    for(int k=0;k<=steps&&passed;k++)
                    {
                        var target=Vector3.Lerp(start,end,(float)k/steps);var support=new JArray();bool floor=FloorGood(target,cc,self,support);var move=target-responder.transform.position;move.y=0;move=Vector3.ClampMagnitude(move,.04f);move.y=-.08f;if(floor)cc.Move(move);yield return null;var actualSupport=new JArray();bool actualFloor=FloorGood(responder.transform.position,cc,self,actualSupport);var error=responder.transform.position-target;error.y=0;passed=floor&&actualFloor&&cc.isGrounded&&error.magnitude<.08f;var blockers=Physics.OverlapCapsule(target+Vector3.up*cc.radius,target+Vector3.up*(cc.height-cc.radius),cc.radius,~0,QueryTriggerInteraction.Ignore).Where(x=>!self.Contains(x)).Select(x=>PathOf(x.transform)).Distinct();samples.Add(new JObject{["target"]=J(target),["actual"]=J(responder.transform.position),["horizontal_error"]=error.magnitude,["target_floor"]=support,["actual_floor"]=actualSupport,["grounded"]=cc.isGrounded,["contacts"]=JArray.FromObject(blockers),["passed"]=passed});
                    }
                    var final=responder.transform.position-end;final.y=0;passed&=final.magnitude<.055f;rows.Add(new JObject{["reverse"]=reverse,["leaf_motion_passed"]=leaf,["endpoint_error"]=final.magnitude,["samples"]=samples,["passed"]=passed});
                }
                responder.RestorePhysicalPose(park+Vector3.up*.04f,0,0);Physics.SyncTransforms();yield return new WaitForSeconds(5f);returned=d.Open<.02f;for(int i=0;i<2;i++)returned&=Vector3.Distance(d.Leaves[i].Pivot.localPosition,closed[i])<.002f;var proof=new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["actual_character_controller"]=PathOf(cc.transform),["radius"]=cc.radius,["height"]=cc.height,["opening_width"]=d.Width,["two_actual_slide_leaves_passed"]=motion,["two_bidirectional_walks"]=rows,["closed_return_passed"]=returned,["four_exact_floor_pins"]=approvedFloorEvidence,["four_exact_floor_pin_sha256"]=approvedFloorPinHash,["nearest_ray_policy_unchanged"]=true,["other_WC_routes_tested"]=0,["whole1F_complete"]=false,["field_accuracy_verified"]=false,["passed"]=motion&&returned&&rows.Count==2&&rows.All(x=>(bool)x["passed"])};string output=".planning/2026-10-09-interior-99-reconstruction/floor1-unit6-vector/actual-player-proof-v2.json";if(File.Exists(output))throw new IOException("Frozen v2 receipt exists");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,proof.ToString());Assert.That((bool)proof["passed"],Is.True,"Inspect "+output);
            }
            finally{responder.RestorePhysicalPose(before,rot.eulerAngles.y,pitch);responder.SetExternalInputMode(external);if(paused)responder.Pause();else responder.Resume(false);Time.captureFramerate=capture;}
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureFramerate=0;Time.timeScale=1;var s=SceneManager.GetSceneByName(SceneFlow.StationScene);if(s.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("Unit6VectorScratch"));yield return SceneManager.UnloadSceneAsync(s);}}
    }
}
#endif
