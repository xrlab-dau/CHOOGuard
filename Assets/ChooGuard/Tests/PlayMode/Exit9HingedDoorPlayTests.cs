#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
using Object=UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Exit9HingedDoorPlayTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator RealHingedLeavesOpenCloseAndAllowFloorSupportedCrossingInBothDirections()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=30;
            var door=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None).Single(d=>d.Element=="exit-9-doors");
            Assert.That(door.Kind,Is.EqualTo(StationDoor.DoorKind.Swing));Assert.That(door.Label,Is.EqualTo("9번 출구 유리 여닫이문"));Assert.That(door.Usable,Is.True);
            Assert.That(door.Leaves.Length,Is.EqualTo(2));Assert.That(door.Leaves.All(l=>l.Slide==Vector3.zero&&Mathf.Abs(l.Swing)==52.5f&&l.Pivot.GetComponent<Rigidbody>().isKinematic),Is.True);
            var filters=door.Leaves.Select(l=>l.Pivot.GetComponentInChildren<MeshFilter>()).ToArray();var original=filters.Select(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray()).ToArray();
            var hinges=door.Leaves.Select(l=>l.Pivot.position).ToArray();var localVertices=filters.Select(f=>f.sharedMesh.vertices).ToArray();
            Assert.That(door.TryInteract(null,out _),Is.True);yield return new WaitForSeconds(1.2f);Physics.SyncTransforms();Assert.That(door.Open,Is.GreaterThan(.99f));
            for(int i=0;i<2;i++)
            {
                Assert.That(Vector3.Distance(hinges[i],door.Leaves[i].Pivot.position),Is.LessThan(.00001f),"Hinges must rotate in place");
                Assert.That(filters[i].sharedMesh.vertices,Is.EqualTo(localVertices[i]),"Original source channels must not animate");
                Assert.That(filters[i].sharedMesh.vertices.Select((v,k)=>Vector3.Distance(filters[i].transform.TransformPoint(v),original[i][k])).Max(),Is.GreaterThan(1f),"Actual glass free edge must swing");
            }
            var actor=new GameObject("Exit9 physical crossing probe");var results=new JArray();
            try
            {
                var cc=actor.AddComponent<CharacterController>();cc.enabled=false;cc.height=1.72f;cc.center=Vector3.up*.86f;cc.radius=.3f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.minMoveDistance=0;
                foreach(float sign in new[]{1f,-1f})
                {
                    if(door.Open<.99f){Assert.That(door.TryInteract(null,out _),Is.True);yield return new WaitForSeconds(1.2f);}
                    cc.enabled=false;actor.transform.position=door.World(new Vector3(0,0,-.65f*sign))+Vector3.up*.04f;cc.enabled=true;
                    for(int i=0;i<6;i++){cc.Move(Vector3.down*.08f);yield return null;}
                    for(int i=1;i<=26;i++)
                    {
                        var expected=door.World(new Vector3(0,0,sign*(-.65f+1.3f*i/26)));var delta=expected-actor.transform.position;delta.y=-.08f;cc.Move(delta);yield return null;
                        var error=actor.transform.position-expected;error.y=0;Assert.That(error.magnitude,Is.LessThan(.12f),"Exit9 actual capsule blocked sign="+sign+" sample="+i+" actual="+actor.transform.position);
                        Assert.That(Mathf.Abs(actor.transform.position.y-expected.y),Is.LessThan(.15f),"Exit9 actual source floor must support both sides");
                    }
                    Assert.That(cc.isGrounded,Is.True);results.Add(new JObject{["direction"]=sign,["samples"]=26,["grounded"]=cc.isGrounded,["actual_finish"]=new JArray(actor.transform.position.x,actor.transform.position.y,actor.transform.position.z)});
                }
            }
            finally{Object.Destroy(actor);}
            if(door.Open>.05f)Assert.That(door.TryInteract(null,out _),Is.True);yield return new WaitForSeconds(3f);Assert.That(door.Open,Is.LessThan(.01f));
            for(int i=0;i<2;i++)Assert.That(filters[i].sharedMesh.vertices.Select((v,k)=>Vector3.Distance(filters[i].transform.TransformPoint(v),original[i][k])).Max(),Is.LessThan(.001f),"Exact closed source surface must return");
            File.WriteAllText(".planning/2026-10-09-floor2-completion-pass2/exit9-play-proof.json",new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["passed"]=true,["hinged_operation"]=true,["source_channels_unchanged"]=true,["fixed_outer_hinges"]=true,["closed_surface_return"]=true,["radius_m"]=.3,["height_m"]=1.72,["maximum_step_m"]=.05,["traversals"]=results,["width_field_verified"]=false,["physical_bank_count_verified"]=false}.ToString());
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureFramerate=0;Time.timeScale=1;var s=SceneManager.GetSceneByName(SceneFlow.StationScene);if(s.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("Exit9Scratch"));yield return SceneManager.UnloadSceneAsync(s);}}
    }
}
#endif
