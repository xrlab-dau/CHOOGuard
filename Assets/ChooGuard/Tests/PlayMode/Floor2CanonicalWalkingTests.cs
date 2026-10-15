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
using ChooGuard.App.Fps.Shell;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Floor2CanonicalWalkingTests
    {
        const string Packet=".planning/2026-10-09-floor2-completion-pass2/canonical-walk-probe-packet.json";
        const string Output=".planning/2026-10-09-floor2-completion-pass2/canonical-walk-probe-results.json";
        static Vector3 V(JToken p)=>new Vector3((float)p["x"],(float)p["y"],(float)p["z"]);
        static JArray P(Vector3 p)=>new JArray(p.x,p.y,p.z);
        static string PathOf(Transform t){var s=t.name;while(t.parent!=null){t=t.parent;s=t.name+"/"+s;}return s;}
        static RaycastHit[] Support(Vector3 p,GameObject actor)
        {
            var hits=new List<RaycastHit>();
            for(int k=-1;k<8;k++)
            {
                var d=k<0?Vector3.zero:new Vector3(Mathf.Cos(k*Mathf.PI/4)*.24f,0,Mathf.Sin(k*Mathf.PI/4)*.24f);
                var ray=Physics.RaycastAll(p+d+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.gameObject!=actor&&h.normal.y>.9f).OrderBy(h=>h.distance).ToArray();
                if(ray.Length>0)hits.Add(ray[0]);
            }
            return hits.OrderByDescending(h=>h.point.y).ToArray();
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator ExactCanonicalLandingEndpointsAndRailDetoursPermitActualBidirectionalWalking()
        {
            var packet=JObject.Parse(File.ReadAllText(Packet));var routes=(JArray)packet["routes"];Assert.That(routes.Count,Is.GreaterThanOrEqualTo(2));
            float maxStep=(float?)packet["maximum_sample_spacing_m"]??.12f;Assert.That(maxStep,Is.InRange(.025f,.12f));
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;
            Time.captureFramerate=30;var actor=new GameObject("Exact canonical source-apron physics probe");var results=new JArray();
            try
            {
                var cc=actor.AddComponent<CharacterController>();cc.enabled=false;cc.height=1.72f;cc.center=Vector3.up*.86f;cc.radius=.3f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.minMoveDistance=0;
                foreach(var route in routes)foreach(bool reverse in new[]{false,true})
                {
                    var points=((JArray)route["path"]).Select(V).ToArray();if(reverse)Array.Reverse(points);
                    var allowed=((JArray)route["floorOwners"]).Select(t=>(string)t).ToArray();Assert.That(points.Length,Is.GreaterThanOrEqualTo(2));
                    cc.enabled=false;actor.transform.position=points[0]+Vector3.up*.04f;cc.enabled=true;
                    for(int k=0;k<8;k++){cc.Move(Vector3.down*.08f);yield return null;}
                    bool pass=true;var samples=new JArray();string failure=null;
                    for(int j=0;j<points.Length&&pass;j++)
                    {
                        Vector3 start=j==0?points[0]:points[j-1],end=points[j];int steps=j==0?1:Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/maxStep));
                        for(int k=1;k<=steps;k++)
                        {
                            var staged=Vector3.Lerp(start,end,(float)k/steps);var floor=Support(staged,actor);bool valid=floor.Length>0&&Mathf.Abs(floor[0].point.y-staged.y)<.35f;
                            var expected=staged;if(valid)expected.y=floor[0].point.y;
                            bool owned=valid&&floor.Any(h=>allowed.Contains(PathOf(h.collider.transform))&&Mathf.Abs(h.point.y-expected.y)<.08f);
                            var move=expected-actor.transform.position;move.y=0;if(move.magnitude>maxStep)move=move.normalized*maxStep;move.y=-.08f;
                            if(valid&&owned)cc.Move(move);yield return null;
                            var error=actor.transform.position-expected;error.y=0;
                            bool clear=valid&&owned&&error.magnitude<.15f&&Mathf.Abs(actor.transform.position.y-expected.y)<.15f;
                            samples.Add(new JObject{["segment"]=j,["staged"]=P(staged),["expected"]=P(expected),["actual"]=P(actor.transform.position),["horizontal_error"]=error.magnitude,["floor_registration_valid"]=valid,["verified_floor_owner"]=owned,["grounded"]=cc.isGrounded,["support"]=new JArray(floor.Select(h=>new JObject{["owner"]=PathOf(h.collider.transform),["Y"]=h.point.y})),["clear"]=clear});
                            if(!clear){failure=!valid?"missing or mismatched exposed floor":!owned?"unreviewed floor owner":"actual character blocked";pass=false;break;}
                        }
                    }
                    for(int k=0;k<5;k++){cc.Move(Vector3.down*.06f);yield return null;}
                    var settled=Support(actor.transform.position,actor);bool endFloor=settled.Any(h=>allowed.Contains(PathOf(h.collider.transform))&&Mathf.Abs(h.point.y-actor.transform.position.y)<.08f);
                    var endError=actor.transform.position-points.Last();endError.y=0;pass&=endFloor&&endError.magnitude<.15f&&Mathf.Abs(actor.transform.position.y-points.Last().y)<.15f;
                    results.Add(new JObject{["id"]=route["id"],["reverse"]=reverse,["passed"]=pass,["failure"]=failure,["expected_exact_canonical_endpoint"]=P(points[0]),["expected_exact_neighbor_endpoint"]=P(points.Last()),["actual_floor_support_at_finish"]=endFloor,["samples"]=samples});
                }
                string packetHash;using(var hash=SHA256.Create())packetHash=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Packet))).Replace("-","").ToLowerInvariant();
                var report=new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["packet_sha256"]=packetHash,["scene_baseline_sha256"]=packet["scene_baseline_sha256"],["actual_cc_directional_traversals"]=results.Count,["radius_m"]=.3,["height_m"]=1.72,["max_step_m"]=maxStep,["existing_geometry_modified"]=false,["catalog_canonical_endpoints_modified"]=false,["results"]=results,["passed"]=results.All(r=>(bool)r["passed"])};
                File.WriteAllText(Output,report.ToString());Assert.That((bool)report["passed"],Is.True,"Actual canonical walking failed; see "+Output);
            }
            finally{UnityEngine.Object.Destroy(actor);Time.captureFramerate=0;}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;var s=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(s.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("CanonicalWalkScratch"));yield return SceneManager.UnloadSceneAsync(s);}
        }
    }
}
#endif
