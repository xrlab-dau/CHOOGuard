#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps.Shell;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Floor2LandingPlayTests
    {
        static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static JArray P(Vector3 p)=>new JArray(p.x,p.y,p.z);
        [UnityTest,Timeout(180000)]
        public IEnumerator ExistingLandingAxisAllowsActualCharacterMovementOnItsNativeFloor()
        {
            var packet=JObject.Parse(File.ReadAllText("workers/twin/output/floor2-platform-passages-20261009/landing-candidate-root-verification-probes.json"));
            var candidates=((JArray)packet["candidates"]).Where(c=>((string)c["candidate_id"]).EndsWith("out-1p5")).ToArray();Assert.That(candidates.Length,Is.EqualTo(4));
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=20;
            var actor=new GameObject("Native landing physics verification");var results=new JArray();
            try
            {
                var cc=actor.AddComponent<CharacterController>();cc.enabled=false;cc.height=1.72f;cc.center=Vector3.up*.86f;cc.radius=.3f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.minMoveDistance=0;
                foreach(var c in candidates)foreach(bool reverse in new[]{false,true})
                {
                    // Begin outside the moving belt; test the existing one-metre
                    // boarding/arrival apron and the additional native floor.
                    var belt=V(c["native_belt_endpoint"]);var end=V(c["proposed_foot"]);var axis=end-belt;axis.y=0;axis.Normalize();
                    var begin=belt+axis;begin.y=V(c["old_foot"]).y;
                    var a=reverse?end:begin;var b=reverse?begin:end;
                    cc.enabled=false;actor.transform.position=a+Vector3.up*.04f;cc.enabled=true;
                    for(int k=0;k<8;k++){cc.Move(Vector3.down*.08f);yield return null;}
                    var samples=new JArray();bool pass=true;
                    for(int k=1;k<=10;k++)
                    {
                        var target=Vector3.Lerp(a,b,k/10f);var move=target-actor.transform.position;move.y=-.08f;cc.Move(move);yield return null;
                        var error=actor.transform.position-target;error.y=0;float height=Mathf.Abs(actor.transform.position.y-target.y);
                        bool clear=error.magnitude<.15f&&height<.15f;pass&=clear;
                        var contacts=Physics.OverlapCapsule(actor.transform.position+Vector3.up*.3f,actor.transform.position+Vector3.up*1.42f,.32f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.gameObject!=actor);
                        samples.Add(new JObject{["expected"]=P(target),["actual"]=P(actor.transform.position),["horizontal_error"]=error.magnitude,["height_error"]=height,["grounded"]=cc.isGrounded,["clear"]=clear,["contacts"]=new JArray(contacts.Select(h=>h.name+"/"+(h.transform.parent!=null?h.transform.parent.name:"")))});
                    }
                    for(int k=0;k<5;k++){cc.Move(Vector3.down*.06f);yield return null;}
                    bool floor=Physics.RaycastAll(actor.transform.position+Vector3.up*.3f,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject!=actor&&h.normal.y>.9f&&Mathf.Abs(h.point.y-actor.transform.position.y)<.08f);
                    pass&=floor&&Mathf.Abs(actor.transform.position.y-b.y)<.15f;
                    results.Add(new JObject{["settled_actual_floor_support"]=floor,["settled_controller_grounded"]=cc.isGrounded,["candidate_id"]=c["candidate_id"],["reverse"]=reverse,["passed"]=pass,["samples"]=samples});
                }
                File.WriteAllText(".planning/2026-10-09-floor2-guide-restoration/landing-axis-character-results.json",new JObject{["actual_character_controller_walks"]=8,["existing_scene_geometry_modified"]=false,["results"]=results,["passed"]=results.All(r=>(bool)r["passed"])}.ToString());
                Assert.That(results.All(r=>(bool)r["passed"]),Is.True,"Original landing physical blocker; see landing-axis-character-results.json");
            }
            finally{Object.Destroy(actor);Time.captureFramerate=0;}
        }
        static RaycastHit[] FootprintFloors(Vector3 p,GameObject actor)
        {
            var hits=new System.Collections.Generic.List<RaycastHit>();
            for(int k=-1;k<8;k++)
            {
                var offset=k<0?Vector3.zero:new Vector3(Mathf.Cos(k*Mathf.PI/4)*.24f,0,Mathf.Sin(k*Mathf.PI/4)*.24f);
                var rays=Physics.RaycastAll(p+offset+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.gameObject!=actor&&h.normal.y>.9f).OrderBy(h=>h.distance).ToArray();
                if(rays.Length>0)hits.Add(rays[0]);
            }
            return hits.OrderByDescending(h=>h.point.y).ToArray();
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator TwoVerifiedSourceApronsPermitActualRailingDetoursToTheAdjacentStairs()
        {
            var packet=JObject.Parse(File.ReadAllText("workers/twin/output/floor2-platform-passages-20261009/landing-lateral-root-physical-spines.json"));
            var paths=((JArray)packet["bounded_paths"]).Select(p=>new {lane=(string)p["lane_id"],path=((JArray)p["candidates"]).FirstOrDefault(c=>(bool)c["path_found"]&&(float)c["buffer_radius_m"]>.33f)}).Where(p=>p.path!=null).ToArray();Assert.That(paths.Length,Is.EqualTo(2));
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=20;
            var actor=new GameObject("Actual native apron detour verification");var results=new JArray();
            try
            {
                var cc=actor.AddComponent<CharacterController>();cc.enabled=false;cc.height=1.72f;cc.center=Vector3.up*.86f;cc.radius=.3f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.minMoveDistance=0;
                foreach(var p in paths)foreach(bool reverse in new[]{false,true})
                {
                    var points=((JArray)p.path["world_waypoints"]).Select(w=>V(w["world"])).ToArray();
                    // The apron starts one metre outside the moving belt. Its
                    // entry/exit on the belt is tested by the transport suite.
                    var first=points[1]-points[0];first.y=0;points[0]+=first.normalized;
                    if(reverse)System.Array.Reverse(points);
                    cc.enabled=false;actor.transform.position=points[0]+Vector3.up*.04f;cc.enabled=true;for(int k=0;k<8;k++){cc.Move(Vector3.down*.08f);yield return null;}
                    bool pass=true;var samples=new JArray();
                    for(int j=1;j<points.Length;j++)
                    {
                        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[j-1],points[j])/.12f));
                        for(int k=1;k<=steps;k++)
                        {
                            var staged=Vector3.Lerp(points[j-1],points[j],(float)k/steps);var expected=staged;
                            var support=FootprintFloors(staged,actor);
                            Assert.That(support.Length,Is.GreaterThan(0),"Apron lacks actual exposed floor");
                            Assert.That(Mathf.Abs(support[0].point.y-staged.y),Is.LessThan(.35f),"Candidate staged height does not register to its local floor");expected.y=support[0].point.y;
                            var move=expected-actor.transform.position;move.y=-.08f;cc.Move(move);yield return null;
                            var error=actor.transform.position-expected;error.y=0;bool clear=error.magnitude<.15f&&Mathf.Abs(actor.transform.position.y-expected.y)<.15f;pass&=clear;
                            samples.Add(new JObject{["segment"]=j,["staged"]=P(staged),["actual_floor_Y"]=expected.y,["expected"]=P(expected),["actual"]=P(actor.transform.position),["clear"]=clear});
                        }
                    }
                    for(int k=0;k<5;k++){cc.Move(Vector3.down*.06f);yield return null;}
                    bool floor=FootprintFloors(actor.transform.position,actor).Any(h=>Mathf.Abs(h.point.y-actor.transform.position.y)<.08f);pass&=floor;
                    results.Add(new JObject{["lane"]=p.lane,["reverse"]=reverse,["passed"]=pass,["actual_floor_support"]=floor,["samples"]=samples});
                }
                File.WriteAllText(".planning/2026-10-09-floor2-guide-restoration/landing-lateral-character-results.json",new JObject{["actual_walk_count"]=4,["other_two_landing_paths_unresolved"]=true,["results"]=results,["passed"]=results.All(r=>(bool)r["passed"])}.ToString());
                Assert.That(results.All(r=>(bool)r["passed"]),Is.True,"Actual native full detour blocked; see landing-lateral-character-results.json");
            }
            finally{Object.Destroy(actor);Time.captureFramerate=0;}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("LandingScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
