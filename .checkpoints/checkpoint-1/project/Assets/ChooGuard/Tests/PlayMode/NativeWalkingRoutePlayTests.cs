#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class NativeWalkingRoutePlayTests
    {
        const string Report=".planning/2026-10-09-floor2-completion-pass2/runtime-walking-tests.json";
        static JArray V(Vector3 p)=>new JArray(p.x,p.y,p.z);
        sealed class OpenKnowledge : RouteGraph.IRules { public float Extra(RouteGraph.Edge edge)=>0; }
        static RouteGraph Graph(StationWalkingRoute owner)
        {
            var r=owner.Entry;var file=new RouteGraph.File{version=1,report="owned walking test",positions=new[]{r.Start.x,r.Start.y,r.Start.z,r.End.x,r.End.y,r.End.z}.Select(v=>Mathf.RoundToInt(v*100)).ToArray(),labels=new[]{"start","end"},from=new[]{0,1},to=new[]{1,0},length=new[]{Mathf.RoundToInt(r.Length*100),Mathf.RoundToInt(r.Length*100)},link=new[]{"walk:"+r.id,"walk:"+r.id}};
            return RouteGraph.Load(new TextAsset(JsonUtility.ToJson(file)),Array.Empty<Escalator>(),new[]{owner});
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator NavOwnerDispatchBlockFailureCancellationAndCurvedHazardCostsStayPhysical()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;
            var art=UnityEditor.AssetDatabase.LoadAssetAtPath<EmergencyArt>("Assets/ChooGuard/Art/Emergency/EmergencyArt.asset");var nav=NavMesh.AddNavMeshData(art.WorldNavMesh);Time.captureFramerate=30;
            var entries=StationWalkingRoutes.Load();var owned=new List<GameObject>();var results=new JArray();
            try
            {
                foreach(var route in entries)
                {
                    var go=new GameObject(route.id);owned.Add(go);var owner=go.AddComponent<StationWalkingRoute>();owner.Setup(route);
                    foreach(bool reverse in new[]{false,true})
                    {
                        Assert.That(NavMesh.SamplePosition(reverse?route.End:route.Start,out var from,.12f,NavMesh.AllAreas),Is.True);
                        Assert.That(NavMesh.SamplePosition(reverse?route.Start:route.End,out var to,.12f,NavMesh.AllAreas),Is.True);
                        var actor=new GameObject("Real owned nav person");owned.Add(actor);actor.transform.position=from.position;
                        var body=actor.AddComponent<PersonBody>();body.Agent.radius=.3f;body.Agent.height=1.72f;body.Agent.speed=1.3f;
                        var own=actor.AddComponent<CapsuleCollider>();own.radius=.3f;own.height=1.72f;own.center=Vector3.up*.86f;
                        var path=new NavMeshPath();Assert.That(body.Agent.CalculatePath(to.position,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True,route.id+" complete owned Nav path");Assert.That(body.Agent.SetPath(path),Is.True);
                        bool dispatched=false;float until=Time.time+30;var samples=new JArray();float largest=0;var previous=body.transform.position;
                        while(Time.time<until)
                        {
                            if(body.Riding==owner)dispatched=true;
                            var p=body.transform.position;largest=Mathf.Max(largest,Vector3.Distance(previous,p));previous=p;samples.Add(V(p));
                            if(dispatched&&body.Riding==null&&!body.Scripted&&body.OnNavMesh&&Vector3.Distance(p,to.position)<.2f)break;
                            yield return null;
                        }
                        results.Add(new JObject{["id"]=route.id,["reverse"]=reverse,["owner_dispatched"]=dispatched,["actual_finish"]=V(body.transform.position),["largest_frame_motion"]=largest,["samples"]=samples});
                        Assert.That(dispatched,Is.True,"PersonBody bypassed the owned floor walk");Assert.That(body.OnNavMesh&&!body.NoRoute&&!body.Scripted&&body.Riding==null,Is.True,"Physical owner failed rebind");
                        Assert.That(Vector3.Distance(body.transform.position,to.position),Is.LessThan(.2f));Assert.That(largest,Is.LessThan(.2f),"A physical route jumped");
                        UnityEngine.Object.Destroy(actor);yield return null;
                    }
                    if(route==entries[0])
                    {
                        var graph=Graph(owner);var a=graph.ByLabel("start");var b=graph.ByLabel("end");var clear=Array.Empty<(Vector3,float)>();float cost=graph.Explore(a,RouteProfile.Default,clear,Time.time).CostOf(b);
                        float danger=graph.Explore(a,RouteProfile.Default,new[]{(route.path[3],.5f)},Time.time).CostOf(b);Assert.That(danger-cost,Is.EqualTo(RouteGraph.DangerCost).Within(.02f),"Curve hazard ignored");
                        var knowledge=graph.Survey(a,RouteProfile.Default,new OpenKnowledge());Assert.That(knowledge.Reaches(b),Is.True);owner.Close();
                        Assert.That(graph.Explore(a,RouteProfile.Default,clear,Time.time+.1f).Reaches(b),Is.False,"Closed owned route remained in live cache");
                        Assert.That(graph.Survey(a,RouteProfile.Default,new OpenKnowledge()).Reaches(b),Is.True,"Guidance leaked an unknown live closure");
                    }
                }
                var entry=entries[0];var blockedGo=new GameObject("Blocked physical route");owned.Add(blockedGo);var blocked=blockedGo.AddComponent<StationWalkingRoute>();blocked.Setup(entry);
                var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);owned.Add(barrier);barrier.transform.position=entry.path[2]+Vector3.up;barrier.transform.localScale=new Vector3(2,2,2);Physics.SyncTransforms();
                Assert.That(NavMesh.SamplePosition(entry.Start,out var begin,.12f,NavMesh.AllAreas),Is.True);
                var stopped=new GameObject("Actual blocked route person");owned.Add(stopped);stopped.transform.position=begin.position;var stoppedBody=stopped.AddComponent<PersonBody>();stoppedBody.Agent.speed=1.3f;Assert.That(stoppedBody.Agent.SetDestination(entry.End),Is.True);
                float stopAt=Time.time+30;while(Time.time<stopAt&&!stoppedBody.NoRoute)yield return null;
                Assert.That(stoppedBody.NoRoute&&blocked.Closed&&!stoppedBody.Scripted&&stoppedBody.Riding==null,Is.True,"Blocked walk did not end cleanly");Assert.That(Vector3.Distance(stoppedBody.transform.position,entry.End),Is.GreaterThan(1f),"Blocked body completed or warped");
                UnityEngine.Object.Destroy(stopped);UnityEngine.Object.Destroy(barrier);yield return null;
                var cancelGo=new GameObject("Cancelled physical route person");owned.Add(cancelGo);cancelGo.transform.position=begin.position;var cancelBody=cancelGo.AddComponent<PersonBody>();cancelBody.enabled=false;cancelBody.Agent.enabled=false;
                bool success=false;var cancelled=NativeLandingWalker.Walk(cancelBody,entry,entry.Direction(false),entry.End,ok=>success=ok);
                for(int i=0;i<5&&cancelled.MoveNext();i++)yield return cancelled.Current;
                cancelGo.SetActive(false);while(cancelled.MoveNext())yield return cancelled.Current;(cancelled as IDisposable)?.Dispose();yield return null;
                Assert.That(success,Is.False);Assert.That(UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Any(c=>c.name=="Actual native landing walking controller"),Is.False,"Cancelled/blocked controller leaked");
                File.WriteAllText(".planning/2026-10-09-floor2-completion-pass2/runtime-owned-link-contract-tests.json",new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["owner_dispatch_walks"]=results.Count,["results"]=results,["block_failure_no_warp"]=true,["cancellation_cleanup"]=true,["live_cache_invalidation"]=true,["knowledge_boundary_preserved"]=true,["curve_danger_cost"]=true,["passed"]=true}.ToString());
            }
            finally{foreach(var go in owned)if(go!=null)UnityEngine.Object.Destroy(go);if(nav.valid)nav.Remove();Time.captureFramerate=0;}
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator RealNpcWalkerUsesCanonicalCurvesAndActualNavAttachmentsAtWalkingAndResponderSpeeds()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;
            var art=UnityEditor.AssetDatabase.LoadAssetAtPath<EmergencyArt>("Assets/ChooGuard/Art/Emergency/EmergencyArt.asset");Assert.That(art.WorldNavMesh,Is.Not.Null);
            var nav=NavMesh.AddNavMeshData(art.WorldNavMesh);var entries=StationWalkingRoutes.Load();Assert.That(entries.Length,Is.GreaterThanOrEqualTo(2));
            var results=new JArray();var owners=new List<GameObject>();Time.captureFramerate=30;
            try
            {
                foreach(var route in entries)
                {
                    var go=new GameObject(route.id);owners.Add(go);var link=go.AddComponent<StationWalkingRoute>();link.Setup(route);
                    Assert.That(NavMesh.SamplePosition(route.Start,out var start,.12f,NavMesh.AllAreas),Is.True,route.id+" start attachment");
                    Assert.That(NavMesh.SamplePosition(route.End,out var end,.12f,NavMesh.AllAreas),Is.True,route.id+" end attachment");
                    foreach(bool reverse in new[]{false,true})foreach(float speed in new[]{1.3f,2.5f})
                    {
                        var from=reverse?end.position:start.position;var to=reverse?start.position:end.position;
                        var actor=new GameObject("Runtime actual NPC test");actor.transform.position=from;var body=actor.AddComponent<PersonBody>();body.enabled=false;body.Agent.enabled=false;body.GoTo(to,speed);
                        bool passed=false;var walker=NativeLandingWalker.Walk(body,route,route.Direction(reverse),to,ok=>passed=ok);var samples=new JArray();
                        try { while(walker.MoveNext()){samples.Add(V(body.transform.position));yield return walker.Current;} }
                        finally {(walker as IDisposable)?.Dispose();}
                        var handle=body.GetComponent<NativeLandingWalkHandle>();
                        results.Add(new JObject{["id"]=route.id,["reverse"]=reverse,["speed"]=speed,["start_nav_attachment"]=V(from),["end_nav_attachment"]=V(to),["passed"]=passed,["actual_finish"]=V(body.transform.position),["failure_phase"]=handle==null?null:handle.FailurePhase,["failure_position"]=handle==null?null:V(handle.FailurePosition),["samples"]=samples});
                        UnityEngine.Object.Destroy(actor);yield return null;
                    }
                }
                File.WriteAllText(Report,new JObject{["executed_utc"]=DateTime.UtcNow.ToString("o"),["actual_runtime_walks"]=results.Count,["max_step_m"]=NativeLandingWalker.MaxStep,["actual_nav_connectors_included"]=true,["results"]=results,["passed"]=results.All(r=>(bool)r["passed"])}.ToString());
                Assert.That(results.All(r=>(bool)r["passed"]),Is.True,"Actual runtime route failure; see "+Report);
                Assert.That(UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Any(c=>c.name=="Actual native landing walking controller"),Is.False,"Temporary walking actor leaked");
                using(var floor=new BakedFloor(art.WorldNavMesh))foreach(var route in entries)
                {
                    var path=new List<Vector3>();Assert.That(floor.Walk(route.Start,route.End,path),Is.True,"Baked guide route missing");
                    foreach(var control in route.path)Assert.That(path.Any(p=>Vector3.Distance(p,control)<.001f),Is.True,"Guide skipped native railing control "+control);
                }
            }
            finally{foreach(var owner in owners)UnityEngine.Object.Destroy(owner);if(nav.valid)nav.Remove();Time.captureFramerate=0;}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureFramerate=0;Time.timeScale=1;var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);
            if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("WalkingRuntimeScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
