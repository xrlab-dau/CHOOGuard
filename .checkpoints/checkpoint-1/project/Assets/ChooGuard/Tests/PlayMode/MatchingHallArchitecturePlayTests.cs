#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Shell;
using Object = UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class MatchingHallArchitecturePlayTests
    {
        const string Config = ".planning/2026-10-09-interior-99-reconstruction/matchinghall-runtime-config.json";
        const string ScenePath = "Assets/ChooGuard/Scenes/FpsStation.unity";
        static string Sha(string p) { using(var f=File.OpenRead(p)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }
        static Vector3 V(JToken p) => new Vector3((float)p[0], (float)p[1], (float)p[2]);
        static JArray P(Vector3 p) => new JArray(p.x, p.y, p.z);
        static string PathOf(Transform t) { var parts=new System.Collections.Generic.List<string>(); for(;t!=null;t=t.parent) parts.Add(t.name); parts.Reverse(); return string.Join("/",parts); }
        static float Distance(Vector3 a,Vector3 b) { var d=a-b; d.y=0; return d.magnitude; }
        static bool Ground(Vector3 feet,CharacterController cc,MeshCollider floor,out JArray proof)
        {
            proof=new JArray(); bool valid=true;
            for(int i=-1;i<8;i++)
            {
                var offset=i<0?Vector3.zero:new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4))*cc.radius*.92f;
                var hits=Physics.RaycastAll(feet+offset+Vector3.up*.5f,Vector3.down,1f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider!=cc&&!h.collider.transform.IsChildOf(cc.transform)).OrderBy(h=>h.distance).ToArray();
                bool good=hits.Length>0&&hits[0].collider==floor&&hits[0].normal.y>.95f&&Mathf.Abs(hits[0].point.y-7f)<.025f&&Mathf.Abs(feet.y-hits[0].point.y)<.075f;
                valid&=good; proof.Add(new JObject { ["ring_ray"]=i,["accepted"]=good,["nearest_owner"]=hits.Length>0?PathOf(hits[0].collider.transform):null,["point"]=hits.Length>0?P(hits[0].point):null,["triangle"]=hits.Length>0?hits[0].triangleIndex:-1 });
            }
            return valid;
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator SavedPlayerChecksSourcePassageAndWestMouthInBothDirections()
        {
            var config=JObject.Parse(File.ReadAllText(Config)); string output=(string)config["output"];
            Assert.That(File.Exists(output),Is.False,"Use a fresh proof output");
            var result=new JObject { ["schema"]="chooguard.matchinghall-real-player-proof.v1",["passed"]=false,["scene_geometry_modified"]=false,["traversal_teleports"]=0,["field_accuracy_verified"]=false };
            var trials=new JArray(); result["trials"]=trials;
            FirstPersonResponder player=null; CharacterController cc=null; bool saved=false;
            Vector3 oldPosition=default; float oldYaw=0,oldPitch=0; bool oldExternal=false,oldPause=false;
            int oldCapture=Time.captureFramerate; float oldScale=Time.timeScale;
            var oldCursor=Cursor.lockState; bool oldVisible=Cursor.visible;
            try
            {
                Assert.That(Sha(ScenePath),Is.EqualTo((string)config["scene_sha256"]));
                Assert.That(Sha((string)config["source_packet"]),Is.EqualTo((string)config["source_packet_sha256"]));
                result["saved_scene_sha256"]=Sha(ScenePath); result["config_sha256"]=Sha(Config);
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single); yield return null;
                Time.captureFramerate=30; Time.timeScale=1;
                player=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsSortMode.None).Single(); cc=player.GetComponent<CharacterController>();
                Assert.That(cc.radius,Is.EqualTo(.28f).Within(.0001f)); Assert.That(cc.height,Is.EqualTo(1.72f).Within(.0001f));
                Assert.That(cc.skinWidth,Is.EqualTo(.025f).Within(.0001f));
                var floor=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Single(c=>PathOf(c.transform)==(string)config["floor_owner"]);
                string asset=AssetDatabase.GetAssetPath(floor.sharedMesh);
                Assert.That(asset,Is.EqualTo((string)config["floor_asset"])); Assert.That(Sha(asset),Is.EqualTo((string)config["floor_asset_sha256"]));
                Assert.That(floor.enabled&&!floor.isTrigger&&!floor.convex,Is.True);
                oldPosition=player.transform.position; oldYaw=player.YawDegrees; oldPitch=player.PitchDegrees; oldExternal=player.ExternalInputMode; oldPause=player.IsPaused; saved=true;
                player.SetExternalInputMode(true); player.Pause();
                foreach(var route in config["routes"])
                foreach(bool reverse in new[]{false,true})
                {
                    Vector3 start=V(route[reverse?"end":"start"]),end=V(route[reverse?"start":"end"]),line=end-start; line.y=0;
                    player.RestorePhysicalPose(start+Vector3.up*.04f,oldYaw,oldPitch); Physics.SyncTransforms();
                    for(int i=0;i<8;i++){cc.Move(Vector3.down*.06f);yield return null;}
                    bool support=Ground(player.transform.position,cc,floor,out var initial),reached=false; int stalled=0;
                    var trial=new JObject { ["route"]=route["id"].DeepClone(),["reverse"]=reverse,["fixture_start"]=P(start),["target"]=P(end),["initial_support"]=initial };
                    var samples=new JArray(); trial["samples"]=samples; trials.Add(trial);
                    int limit=Mathf.CeilToInt(line.magnitude/.08f)+40;
                    for(int i=0;i<limit&&support;i++)
                    {
                        var before=player.transform.position; var move=end-before; move.y=0;
                        if(move.magnitude<.06f){reached=true;break;}
                        move=Vector3.ClampMagnitude(move,.08f); var flags=cc.Move(move+Vector3.down*.06f); yield return null;
                        var feet=player.transform.position; bool good=Ground(feet,cc,floor,out var ring);
                        var along=feet-start; along.y=0; float lateral=(along-Vector3.Project(along,line)).magnitude;
                        support&=good&&cc.isGrounded&&(flags&CollisionFlags.Below)!=0&&lateral<.15f;
                        stalled=Distance(feet,before)<.015f?stalled+1:0;
                        samples.Add(new JObject { ["feet"]=P(feet),["grounded"]=cc.isGrounded,["full_footprint_supported"]=good,["floor_rays"]=ring,["lateral_error"]=lateral,["collision_flags"]=(int)flags });
                        if(stalled>=8)break;
                    }
                    reached|=support&&Distance(player.transform.position,end)<.06f;
                    bool expectedClear=(bool)route["expected_clear"],blocked=support&&!reached&&stalled>=8&&Distance(player.transform.position,end)>.3f;
                    trial["actual_end"]=P(player.transform.position); trial["reached"]=reached; trial["ground_support_valid"]=support;
                    trial["blocked"]=blocked; trial["passed"]=support&&(expectedClear?reached:blocked);
                }
                result["passed"]=trials.Count==4&&trials.All(t=>(bool)t["passed"])&&Sha(ScenePath)==(string)config["scene_sha256"];
            }
            finally
            {
                if(saved&&player!=null){player.RestorePhysicalPose(oldPosition,oldYaw,oldPitch);player.SetExternalInputMode(oldExternal);if(oldPause)player.Pause();else player.Resume(!oldExternal);result["pose_restored"]=Vector3.Distance(player.transform.position,oldPosition)<.0001f;result["input_restored"]=player.ExternalInputMode==oldExternal&&player.IsPaused==oldPause;}
                Time.captureFramerate=oldCapture;Time.timeScale=oldScale;Cursor.lockState=oldCursor;Cursor.visible=oldVisible;
                result["time_restored"]=Time.captureFramerate==oldCapture&&Mathf.Abs(Time.timeScale-oldScale)<.0001f;
                Directory.CreateDirectory(Path.GetDirectoryName(output));using(var f=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(f))w.Write(result.ToString());
            }
            Assert.That((bool)result["passed"],Is.True,"Inspect physical source passage proof: "+output);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("MatchingHallProbeScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
