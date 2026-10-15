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
using Object=UnityEngine.Object;
namespace ChooGuard.Tests.PlayMode
{
    public sealed class Floor1Unit6VectorPlayTests
    {
        const string Work="workers/twin/output/floor1-source-completion-20261009-pass3";
        static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
        static JArray J(Vector3 a)=>new JArray(a.x,a.y,a.z);
        static string PathOf(Transform t){var p=new List<string>();while(t!=null){p.Add(t.name);t=t.parent;}p.Reverse();return string.Join("/",p);}
        static bool FloorGood(Vector3 p,CharacterController cc,HashSet<Collider> self,JArray receipt)
        {
            bool good=true;for(int i=0;i<9;i++)
            {
                var delta=i==0?Vector3.zero:new Vector3(Mathf.Cos((i-1)*Mathf.PI/4),0,Mathf.Sin((i-1)*Mathf.PI/4))*cc.radius*.92f;
                var h=Physics.RaycastAll(p+delta+Vector3.up*.7f,Vector3.down,1.4f,~0,QueryTriggerInteraction.Ignore).Where(x=>!self.Contains(x.collider)).OrderBy(x=>x.distance).FirstOrDefault();
                string owner=h.collider==null?null:PathOf(h.collider.transform);bool allowed=owner=="1층 · 원본 정합/Ground_Floor"||owner=="실내 트윈 마감/점포 내부 1F/Kit_Colliders"||owner=="실내 트윈 마감/1F/Kit_Colliders";
                bool ok=h.collider!=null&&h.normal.y>.9f&&Mathf.Abs(h.point.y)<.08f&&allowed;good&=ok;receipt.Add(new JObject{["ray"]=i,["owner"]=owner,["height"]=h.point.y,["normal"]=J(h.normal),["passed"]=ok});
            }return good;
        }
        [UnityTest,Timeout(180000)]public IEnumerator SourceVectorUnit6KeepsItsSlidingLeavesAndActualBidirectionalCrossing()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;int capture=Time.captureFramerate;var responder=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsSortMode.None).Single();var cc=responder.GetComponent<CharacterController>();var before=responder.transform.position;var rot=responder.transform.rotation;float pitch=responder.PitchDegrees;bool external=responder.ExternalInputMode,paused=responder.IsPaused;var rows=new JArray();
            try
            {
                Time.captureFramerate=30;responder.SetExternalInputMode(true);responder.Pause();Assert.That(cc.radius,Is.EqualTo(.28f).Within(.0001f));Assert.That(cc.height,Is.EqualTo(1.72f).Within(.0001f));var self=new HashSet<Collider>(responder.GetComponentsInChildren<Collider>(true));var d=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None).Single(x=>x.Element=="1F-unit06-front3"&&x.Index==0);var e=JObject.Parse(File.ReadAllText(Work+"/unit6-door-aperture-proof.json"));var centre=V(e["new_centre_world"]);Assert.That(Vector3.Distance(d.World(Vector3.zero),centre),Is.LessThan(.001f));Assert.That(d.Width,Is.EqualTo(1.8f).Within(.00001f));Assert.That(d.Kind,Is.EqualTo(StationDoor.DoorKind.Sliding));Assert.That(d.Leaves.Length,Is.EqualTo(2));Assert.That(d.Mode,Is.EqualTo(StationDoor.OperatorMode.Auto));var normal=(d.World(Vector3.forward)-centre).normalized;var park=centre+normal*4.5f;var closed=d.Leaves.Select(x=>x.ClosedPosition).ToArray();bool motion=true,returned=true;
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
                responder.RestorePhysicalPose(park+Vector3.up*.04f,0,0);Physics.SyncTransforms();yield return new WaitForSeconds(5f);returned=d.Open<.02f;for(int i=0;i<2;i++)returned&=Vector3.Distance(d.Leaves[i].Pivot.localPosition,closed[i])<.002f;var proof=new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["actual_character_controller"]=PathOf(cc.transform),["radius"]=cc.radius,["height"]=cc.height,["opening_width"]=d.Width,["two_actual_slide_leaves_passed"]=motion,["two_bidirectional_walks"]=rows,["closed_return_passed"]=returned,["other_WC_routes_tested"]=0,["whole1F_complete"]=false,["field_accuracy_verified"]=false,["passed"]=motion&&returned&&rows.Count==2&&rows.All(x=>(bool)x["passed"])};string output=".planning/2026-10-09-interior-99-reconstruction/floor1-unit6-vector/actual-player-proof.json";Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,proof.ToString());Assert.That((bool)proof["passed"],Is.True,"Inspect "+output);
            }
            finally{responder.RestorePhysicalPose(before,rot.eulerAngles.y,pitch);responder.SetExternalInputMode(external);if(paused)responder.Pause();else responder.Resume(false);Time.captureFramerate=capture;}
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureFramerate=0;Time.timeScale=1;var s=SceneManager.GetSceneByName(SceneFlow.StationScene);if(s.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("Unit6VectorScratch"));yield return SceneManager.UnloadSceneAsync(s);}}
    }
}
#endif
