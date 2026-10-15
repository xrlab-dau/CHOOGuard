#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps.Shell;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChooGuard.Tests.PlayMode
{
    public sealed class OriginalStairUpperStripHitRecorder : MonoBehaviour
    {
        public struct Contact { public Collider Collider; public Vector3 Point, Normal; }
        public readonly List<Contact> Contacts=new List<Contact>();
        void OnControllerColliderHit(ControllerColliderHit hit){Contacts.Add(new Contact{Collider=hit.collider,Point=hit.point,Normal=hit.normal});}
    }
    public sealed class OriginalStairUpperStripPlayTests
    {
        const string Packet=".planning/2026-10-09-floor2-completion-pass2/original-stair-upper-strip-root-probe.json";
        const string Result=".planning/2026-10-09-floor2-completion-pass2/original-stair-upper-strip-character-results.json";
        static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static JArray P(Vector3 p)=>new JArray(p.x,p.y,p.z);
        static string PathOf(Transform t){var a=new List<string>();for(;t!=null;t=t.parent)a.Add(t.name);a.Reverse();return string.Join("/",a);}
        static string Hash(string p){using(var f=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static bool Support(Vector3 foot,Transform actor,JObject packet,out float high,out JArray receipt)
        {
            string allowed=(string)packet["original_native_floor_owner"]["path"];
            var indices=new HashSet<int>(packet["original_floor_triangle_correspondences"].Select(t=>(int)t["current_triangle_ordinal"]));
            receipt=new JArray();high=float.NegativeInfinity;bool all=true;
            for(int k=-1;k<8;k++)
            {
                Vector3 offset=k<0?Vector3.zero:new Vector3(Mathf.Cos(k*Mathf.PI/4)*.28f,0,Mathf.Sin(k*Mathf.PI/4)*.28f);
                var hits=Physics.RaycastAll(foot+offset+Vector3.up*.65f,Vector3.down,1.0f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.collider.transform.IsChildOf(actor)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0){all=false;receipt.Add(new JObject{["ray"]=k,["original_exposed_support"]=false,["reason"]="no exposed surface"});continue;}
                var h=hits[0];string owner=PathOf(h.collider.transform);bool source=h.collider is MeshCollider&&owner==allowed&&indices.Contains(h.triangleIndex);
                bool valid=source&&h.normal.y>.95f&&h.point.y>foot.y-.24f&&h.point.y<foot.y+.06f;all&=valid;high=Mathf.Max(high,h.point.y);
                receipt.Add(new JObject{["ray"]=k,["point"]=P(h.point),["owner"]=owner,["triangle_index"]=h.triangleIndex,["normal_y"]=h.normal.y,["exact_original_source_floor"]=source,["original_exposed_support"]=valid});
            }
            return all;
        }
        static GameObject Actor(Vector3 foot,out CharacterController cc,out OriginalStairUpperStripHitRecorder recorder)
        {
            var go=new GameObject("Original upper stair strip test body");cc=go.AddComponent<CharacterController>();cc.enabled=false;
            cc.radius=.3f;cc.height=1.72f;cc.center=Vector3.up*.86f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.minMoveDistance=0;
            // One initial fixture spawn. All subsequent motion uses the real controller's Move.
            go.transform.position=foot+Vector3.up*.04f;recorder=go.AddComponent<OriginalStairUpperStripHitRecorder>();cc.enabled=true;Physics.SyncTransforms();return go;
        }
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
        }
        static bool NativeRail(OriginalStairUpperStripHitRecorder.Contact hit,JObject packet)
        {
            if(hit.Collider==null||PathOf(hit.Collider.transform)!=(string)packet["original_native_floor_owner"]["path"]||Mathf.Abs(hit.Normal.y)>.5f)return false;
            foreach(var t in packet["negative_native_rail"]["native_body_triangle_correspondences"])
            {
                var q=t["current_world_XYZ"];if(Vector3.Distance(hit.Point,Closest(hit.Point,V(q[0]),V(q[1]),V(q[2])))<.06f)return true;
            }
            return false;
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator ActualOriginalUpperTreadStripClearsAfterInferredInfillOmission()
        {
            var packet=JObject.Parse(File.ReadAllText(Packet));Assert.That((string)packet["schema"],Is.EqualTo("chooguard.original-stair-upper-strip-cc-probe.v1"));
            Assert.That(Hash((string)packet["scene"]),Is.EqualTo((string)packet["scene_sha256"]),"Root must repin only the scene epoch after revalidating the exact native support asset and source ownership");
            Assert.That(Hash((string)packet["original_native_floor_asset"]),Is.EqualTo((string)packet["original_native_floor_asset_sha256"]));Assert.That(Hash((string)packet["original_native_floor_asset"]+".meta"),Is.EqualTo((string)packet["original_native_floor_asset_meta_sha256"]));
            int priorCapture=Time.captureFramerate;GameObject actor=null;var positives=new JArray();var negatives=new JArray();
            try
            {
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=20;
            var native=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene==SceneManager.GetActiveScene()&&PathOf(c.transform)==(string)packet["original_native_floor_owner"]["path"]).ToArray();Assert.That(native.Length,Is.EqualTo(1));Assert.That(native[0].enabled&&native[0].gameObject.activeInHierarchy&&!native[0].isTrigger,Is.True);
#if UNITY_EDITOR
            Assert.That(AssetDatabase.GetAssetPath(native[0].sharedMesh),Is.EqualTo((string)packet["original_native_floor_asset"]));
#endif
                foreach(bool reverse in new[]{false,true})
                {
                    var points=packet["positive_world_feet"].Select(V).ToArray();if(reverse)Array.Reverse(points);
                    actor=Actor(points[0],out var cc,out var recorder);for(int k=0;k<12;k++){cc.Move(Vector3.down*.06f);yield return null;}
                    var samples=new JArray();bool passed=Support(actor.transform.position,actor.transform,packet,out float floor,out var initial)&&cc.isGrounded&&Mathf.Abs(actor.transform.position.y-floor)<.06f;
                    string failure=passed?null:"initial actual foot is unsupported/ungrounded";
                    for(int segment=1;segment<points.Length&&passed;segment++)
                    {
                        int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[segment-1],points[segment])/.035f));
                        for(int k=1;k<=n;k++)
                        {
                            var expected=Vector3.Lerp(points[segment-1],points[segment],(float)k/n);var delta=expected-actor.transform.position;delta.y=0;
                            recorder.Contacts.Clear();var flags=cc.Move(Vector3.ClampMagnitude(delta,.04f)+Vector3.down*.06f);yield return null;
                            bool exposed=Support(actor.transform.position,actor.transform,packet,out floor,out var support);var error=actor.transform.position-expected;error.y=0;
                            bool clear=exposed&&cc.isGrounded&&Mathf.Abs(actor.transform.position.y-floor)<.06f&&error.magnitude<.055f;passed&=clear;
                            samples.Add(new JObject{["segment"]=segment,["expected"]=P(expected),["actual_foot"]=P(actor.transform.position),["horizontal_error"]=error.magnitude,["grounded"]=cc.isGrounded,["collision_flags"]=(int)flags,["actual_original_support"]=support,["sample_clear"]=clear});
                            if(!clear){failure="actual controller displacement or exposed original foot support failed";break;}
                        }
                    }
                    for(int k=0;k<5;k++){cc.Move(Vector3.down*.06f);yield return null;}
                    bool end=Support(actor.transform.position,actor.transform,packet,out floor,out var endProof);var apart=actor.transform.position-points.Last();apart.y=0;passed&=end&&apart.magnitude<.055f&&Mathf.Abs(actor.transform.position.y-floor)<.06f;
                    positives.Add(new JObject{["reverse"]=reverse,["walk_passed"]=passed,["failure"]=failure,["initial_original_support"]=initial,["end_original_support"]=endProof,["actual_end"]=P(actor.transform.position),["samples"]=samples});
                    cc.enabled=false;UnityEngine.Object.Destroy(actor);actor=null;yield return null;
                }
                // A static negative support fixture is intentionally rejected. It is not a successful traversal.
                actor=Actor(V(packet["negative_missing_landing"]["world_foot"]),out var gapCC,out var gapRecorder);gapCC.enabled=false;Physics.SyncTransforms();
                bool gapSupport=Support(V(packet["negative_missing_landing"]["world_foot"]),actor.transform,packet,out float gapY,out var gapProof);
                negatives.Add(new JObject{["case"]="uncovered original upper gap",["expected_walk_outcome"]="failure",["walk_passed"]=false,["fixture_rejected_as_expected"]=!gapSupport,["original_support"]=gapProof,["controller_move_performed"]=false});UnityEngine.Object.Destroy(actor);actor=null;yield return null;
                // A fresh real body is driven into the original side rail; no scene blocker is fabricated.
                var railPoints=packet["negative_native_rail"]["world_feet"].Select(V).ToArray();actor=Actor(railPoints[0],out var railCC,out var railRecorder);
                for(int k=0;k<12;k++){railCC.Move(Vector3.down*.06f);yield return null;}
                bool railStart=Support(actor.transform.position,actor.transform,packet,out float railY,out var railStartProof)&&railCC.isGrounded;bool nativeBlocked=false;var railSamples=new JArray();
                for(int k=0;k<90&&railStart&&!nativeBlocked;k++)
                {
                    var before=actor.transform.position;var delta=railPoints[1]-before;delta.y=0;if(delta.magnitude<.04f)break;
                    railRecorder.Contacts.Clear();var flags=railCC.Move(Vector3.ClampMagnitude(delta,.04f)+Vector3.down*.06f);yield return null;
                    var advanced=actor.transform.position-before;advanced.y=0;var contacts=new JArray(railRecorder.Contacts.Select(h=>new JObject{["owner"]=h.Collider==null?null:PathOf(h.Collider.transform),["point"]=P(h.Point),["normal"]=P(h.Normal),["exact_native_stair_body"]=NativeRail(h,packet)}));
                    bool stillSupported=Support(actor.transform.position,actor.transform,packet,out railY,out var railProof);
                    nativeBlocked=railRecorder.Contacts.Any(h=>NativeRail(h,packet))&&(flags&CollisionFlags.Sides)!=0&&advanced.magnitude<.02f&&stillSupported;
                    railSamples.Add(new JObject{["actual_foot"]=P(actor.transform.position),["horizontal_advance"]=advanced.magnitude,["collision_flags"]=(int)flags,["actual_original_support"]=railProof,["body_contacts"]=contacts,["native_barrier_blocked"]=nativeBlocked});
                    if(!stillSupported)break;
                }
                negatives.Add(new JObject{["case"]="original native side rail",["expected_walk_outcome"]="blocked_by_original_native_body",["walk_passed"]=false,["fixture_rejected_as_expected"]=nativeBlocked,["initial_original_support"]=railStartProof,["samples"]=railSamples});
                var receipt=new JObject{["packet"]=Packet,["radius_m"]=.3,["height_m"]=1.72,["positive_actual_walks"]=positives,["negative_fixtures"]=negatives,["positive_walk_count"]=positives.Count,["positive_all_walks_passed"]=positives.All(r=>(bool)r["walk_passed"]),["negative_all_rejected_as_expected"]=negatives.All(r=>(bool)r["fixture_rejected_as_expected"]),["source_axis_or_floor_modified"]=false,["warps_or_teleports_during_motion"]=0,["full_vertical_stair_acceptance"]=false,["unrelated_s34_post_gap_accepted"]=false};
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Result));File.WriteAllText(Result,receipt.ToString());
                Assert.That((bool)receipt["positive_all_walks_passed"],Is.True,"Original source-supported upper tread strip failed; no acceptance should be promoted");Assert.That((bool)receipt["negative_all_rejected_as_expected"],Is.True,"Missing-source support or original source barrier rejection contract failed");
            }
            finally{if(actor!=null){var cc=actor.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;UnityEngine.Object.Destroy(actor);}Time.captureFramerate=priorCapture;}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("OriginalStairStripScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
