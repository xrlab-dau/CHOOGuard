#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Floor2GuidePlayTests
    {
        static Vector3 World(JToken p){float a=16.2f*Mathf.Deg2Rad;float u=(float)p[0],v=(float)p[1];return new Vector3(u*Mathf.Sin(a)+v*Mathf.Cos(a),(float)p[2],u*Mathf.Cos(a)-v*Mathf.Sin(a));}
        [UnityTest,Timeout(180000)]
        public IEnumerator FourGuideMouthsSupportActualBidirectionalCharacterWalkingIncludingTheNativeRamp()
        {
            var packet=JObject.Parse(File.ReadAllText("workers/twin/output/floor2-platform-passages-20261009/four-mouths-physical-walk-spines.json"));
            var paths=(JArray)packet["paths"];Assert.That(paths.Count,Is.EqualTo(4));
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);
            yield return null;Time.captureFramerate=20;
            var all=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None);
            var ids=paths.SelectMany(p=>((JArray)p["must_hold_open_door_elements"]).Select(x=>(string)x)).Distinct().ToArray();
            foreach(string id in ids){var door=all.Single(d=>d.Element==id);Assert.That(door.Usable,Is.True);door.SetMode(StationDoor.OperatorMode.HoldOpen);}
            yield return new WaitForSeconds(1.7f);Physics.SyncTransforms();
            foreach(string id in ids)Assert.That(all.Single(d=>d.Element==id).Open,Is.GreaterThan(.99f));
            var actor=new GameObject("Guide passage physics test");
            try
            {
                var controller=actor.AddComponent<CharacterController>();controller.enabled=false;controller.height=1.72f;controller.radius=.3f;controller.center=Vector3.up*.86f;controller.stepOffset=.3f;controller.slopeLimit=50;controller.skinWidth=.02f;controller.minMoveDistance=0;
                foreach(var path in paths)foreach(string way in new[]{"forward_UVY","reverse_UVY"})
                {
                    var points=((JArray)path[way]).Select(World).ToArray();Assert.That(points.Length,Is.GreaterThanOrEqualTo(2));
                    controller.enabled=false;actor.transform.position=points[0]+Vector3.up*.04f;controller.enabled=true;
                    for(int i=0;i<8;i++){controller.Move(Vector3.down*.08f);yield return null;}
                    for(int i=0;i+1<points.Length;i++)
                    {
                        var a=points[i];var b=points[i+1];var flat=b-a;flat.y=0;int steps=Mathf.Max(1,Mathf.CeilToInt(flat.magnitude/.16f));
                        for(int j=1;j<=steps;j++)
                        {
                            var expected=Vector3.Lerp(a,b,(float)j/steps);var travel=expected-actor.transform.position;travel.y=-.08f;
                            controller.Move(travel);yield return null;
                            var delta=actor.transform.position-expected;delta.y=0;
                            Assert.That(delta.magnitude,Is.LessThan(.22f),"Blocked real passage "+path["name"]+" "+way+" at "+expected);
                            Assert.That(Mathf.Abs(actor.transform.position.y-expected.y),Is.LessThan(.18f),"Missing floor or failed native ramp step "+path["name"]+" "+way);
                        }
                    }
                    for(int i=0;i<5;i++){controller.Move(Vector3.down*.06f);yield return null;}
                    Assert.That(Mathf.Abs(actor.transform.position.y-points.Last().y),Is.LessThan(.08f),"Passage end is not on its actual floor "+path["name"]);
                    Assert.That(controller.isGrounded,Is.True,"Passage end is not grounded "+path["name"]);
                }
            }
            finally{Object.Destroy(actor);}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("Floor2GuideScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator FourEntranceBayDoorsMoveTheirRealLeavesAndHaveUnblockedFloorSupportedOpenings()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);
            yield return null;Time.captureFramerate=20;
            var all=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None);
            var ids=new[]{"native-auto-f0efc5e621349b0847729fb9","native-auto-9783ce238277b47411a9f63a","native-auto-8d6345ca613539080b115218","native-auto-de9e2b4e5f7a2cb6ab995d09"};
            var doors=ids.Select(id=>all.Single(d=>d.Element==id)).ToArray();
            var allIncludingInactive=Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var id in new[]{"2F-main-front-14-다인명가","2F-main-front-15-호두나니&부산오뎅","2F-main-front-17-청도할매김밥","2F-main-front-18-반월당닭강정"})
                Assert.That(allIncludingInactive.Single(d=>d.Element==id).gameObject.activeSelf,Is.False,"Generated duplicate must not compete with the original operating door "+id);
            foreach(var door in doors){Assert.That(door.Kind,Is.EqualTo(StationDoor.DoorKind.Sliding));Assert.That(door.PassableByPeople,Is.True);door.SetMode(StationDoor.OperatorMode.Locked);}
            yield return new WaitForSeconds(2.3f);
            var leaves=doors.SelectMany(d=>d.Leaves).ToArray();var closed=leaves.Select(l=>l.Pivot.localPosition).ToArray();
            foreach(var door in doors)door.SetMode(StationDoor.OperatorMode.HoldOpen);
            yield return new WaitForSeconds(1.6f);Physics.SyncTransforms();
            foreach(var door in doors)
            {
                Assert.That(door.Open,Is.GreaterThan(.99f));
                var direction=(door.World(new Vector3(0,0,1))-door.World(Vector3.zero)).normalized;
                var hits=Physics.CapsuleCastAll(door.World(new Vector3(0,.3f,-.7f)),door.World(new Vector3(0,1.4f,-.7f)),.18f,direction,1.4f,~0,QueryTriggerInteraction.Ignore);
                Assert.That(hits,Is.Empty,"Static blocker in corrected2F opening "+door.Element+": "+string.Join(",",hits.Select(h=>h.collider.name)));
                foreach(float side in new[]{-.65f,.65f})
                {
                    var p=door.World(new Vector3(0,.65f,side));
                    Assert.That(Physics.RaycastAll(p,Vector3.down,1,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.9f&&Mathf.Abs(h.point.y-7)<.03f),Is.True,"Missing original2F floor "+door.Element);
                }
            }
            for(int i=0;i<leaves.Length;i++)Assert.That(Vector3.Distance(leaves[i].Pivot.localPosition,closed[i]+leaves[i].ClosedRotation*leaves[i].Slide),Is.LessThan(.002f));
            var actor=new GameObject("Actual entrance capsule crossing");
            try
            {
                var controller=actor.AddComponent<CharacterController>();controller.enabled=false;controller.height=1.72f;controller.radius=.3f;controller.center=Vector3.up*.86f;controller.stepOffset=.3f;controller.skinWidth=.02f;controller.minMoveDistance=0;
                foreach(var door in doors)foreach(float sign in new[]{-1f,1f})
                {
                    controller.enabled=false;actor.transform.position=door.World(new Vector3(0,0,-.7f*sign))+Vector3.up*.04f;controller.enabled=true;
                    for(int i=0;i<5;i++){controller.Move(Vector3.down*.08f);yield return null;}
                    for(int i=1;i<=10;i++)
                    {
                        var expected=door.World(new Vector3(0,0,sign*(-.7f+1.4f*i/10)));var delta=expected-actor.transform.position;delta.y=-.08f;
                        controller.Move(delta);yield return null;var error=actor.transform.position-expected;error.y=0;
                        var contacts=Physics.OverlapCapsule(actor.transform.position+Vector3.up*.3f,actor.transform.position+Vector3.up*1.42f,.32f,~0,QueryTriggerInteraction.Ignore).Where(c=>c.gameObject!=actor).Select(c=>c.name+" parent="+(c.transform.parent!=null?c.transform.parent.name:"none")+" bounds="+c.bounds).ToArray();
                        Assert.That(error.magnitude,Is.LessThan(.15f),"Actual character blocked at operating entrance "+door.Element+" sign="+sign+" step="+i+" expected="+expected+" actual="+actor.transform.position+" contacts="+string.Join(";",contacts));
                        Assert.That(Mathf.Abs(actor.transform.position.y-expected.y),Is.LessThan(.12f),"Entrance lost actual floor "+door.Element);
                    }
                    Assert.That(controller.isGrounded,Is.True);
                }
            }
            finally{Object.Destroy(actor);}
            foreach(var door in doors)door.SetMode(StationDoor.OperatorMode.Locked);
            yield return new WaitForSeconds(2.3f);
            for(int i=0;i<leaves.Length;i++)Assert.That(Vector3.Distance(leaves[i].Pivot.localPosition,closed[i]),Is.LessThan(.002f));
        }
    }
}
#endif
