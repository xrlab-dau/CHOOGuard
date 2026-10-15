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
    public sealed class NamcheonSourceFrontPlayTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SourceAlignedShopDoorMovesOriginalLeavesAndAllowsPhysicalCrossingBothWays()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=30;
            var door=Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None).Single(d=>d.Element=="2F-main-front-04-남천할매떡볶이");
            Assert.That(door.Kind,Is.EqualTo(StationDoor.DoorKind.Sliding));Assert.That(door.Usable,Is.True);Assert.That(door.Leaves.Length,Is.EqualTo(2));
            var centre=door.World(Vector3.zero);Assert.That(Vector3.Distance(centre,new Vector3(1.302433f,7f,-38.3876f)),Is.LessThan(.001f),"Source-aligned opening must remain at installed owner pose");
            var filters=door.Leaves.Select(l=>l.Pivot.GetComponent<MeshFilter>()).ToArray();Assert.That(filters.All(f=>f!=null),Is.True);
            var localVertices=filters.Select(f=>f.sharedMesh.vertices).ToArray();var closed=filters.Select(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray()).ToArray();
            Assert.That(door.RequestButtonOpen(out _),Is.True);door.SetMode(StationDoor.OperatorMode.HoldOpen);yield return new WaitForSeconds(1.4f);Physics.SyncTransforms();
            Assert.That(door.Open,Is.GreaterThan(.99f));
            for(int i=0;i<filters.Length;i++){Assert.That(filters[i].sharedMesh.vertices,Is.EqualTo(localVertices[i]));Assert.That(filters[i].sharedMesh.vertices.Select((v,k)=>Vector3.Distance(filters[i].transform.TransformPoint(v),closed[i][k])).Min(),Is.GreaterThan(.75f));}
            var actor=new GameObject("Source-aligned shop crossing probe");var traversals=new JArray();
            try
            {
                var cc=actor.AddComponent<CharacterController>();cc.enabled=false;cc.height=1.72f;cc.center=Vector3.up*.86f;cc.radius=.3f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.minMoveDistance=0;
                foreach(float sign in new[]{1f,-1f})
                {
                    cc.enabled=false;actor.transform.position=door.World(new Vector3(0,0,-sign))+Vector3.up*.04f;cc.enabled=true;
                    for(int i=0;i<6;i++){cc.Move(Vector3.down*.08f);yield return null;}
                    for(int i=1;i<=40;i++)
                    {
                        var expected=door.World(new Vector3(0,0,sign*(-1f+i*.05f)));var delta=expected-actor.transform.position;delta.y=-.08f;cc.Move(delta);yield return null;
                        var error=actor.transform.position-expected;error.y=0;Assert.That(error.magnitude,Is.LessThan(.12f),"Actual aperture blocked direction="+sign+" sample="+i+" position="+actor.transform.position);
                        Assert.That(Mathf.Abs(actor.transform.position.y-expected.y),Is.LessThan(.15f),"Existing source floor must support both sides");
                    }
                    Assert.That(cc.isGrounded,Is.True);traversals.Add(new JObject{["direction"]=sign,["samples"]=40,["actual_finish"]=new JArray(actor.transform.position.x,actor.transform.position.y,actor.transform.position.z),["grounded"]=cc.isGrounded});
                }
            }
            finally{Object.Destroy(actor);}
            door.SetMode(StationDoor.OperatorMode.Auto);yield return new WaitForSeconds(5f);Assert.That(door.Open,Is.LessThan(.01f));
            for(int i=0;i<filters.Length;i++)Assert.That(filters[i].sharedMesh.vertices.Select((v,k)=>Vector3.Distance(filters[i].transform.TransformPoint(v),closed[i][k])).Max(),Is.LessThan(.001f));
            File.WriteAllText(".planning/2026-10-09-floor2-completion-pass2/namcheon-play-proof.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["passed"]=true,["original_leaf_channels_unchanged"]=true,["closed_surface_return"]=true,["source_floor_appended"]=false,["radius_m"]=.3,["height_m"]=1.72,["maximum_step_m"]=.05,["traversals"]=traversals,["field_dimensions_verified"]=false}.ToString());
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureFramerate=0;Time.timeScale=1;var s=SceneManager.GetSceneByName(SceneFlow.StationScene);if(s.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("NamcheonScratch"));yield return SceneManager.UnloadSceneAsync(s);}}
    }
}
#endif
