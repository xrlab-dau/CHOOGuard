#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class NativeWalkingRoutePlayTests
    {
        sealed class OpenKnowledge:RouteGraph.IRules { public float Extra(RouteGraph.Edge edge)=>0; }
        static RouteGraph Graph(StationWalkingRoute owner)
        {
            var route=owner.Entry;
            var file=new RouteGraph.File
            {
                version=1,report="isolated physical owner contract",
                positions=new[]{route.Start.x,route.Start.y,route.Start.z,route.End.x,route.End.y,route.End.z}.Select(v=>Mathf.RoundToInt(v*100)).ToArray(),
                labels=new[]{"start","end"},from=new[]{0,1},to=new[]{1,0},
                length=new[]{Mathf.RoundToInt(route.Length*100),Mathf.RoundToInt(route.Length*100)},
                link=new[]{"walk:"+route.id,"walk:"+route.id}
            };
            return RouteGraph.Load(new TextAsset(JsonUtility.ToJson(file)),Array.Empty<Escalator>(),new[]{owner});
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator OwnedNavDispatchCurvedHazardKnowledgeFailureAndCancellationRemainPhysical()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var owner=fixture.OwnedNavigationRoute();var route=owner.Entry;yield return fixture.SettlePhysics();
                foreach(bool reverse in new[]{false,true})
                {
                    Assert.That(NavMesh.SamplePosition(reverse?route.End:route.Start,out var from,.12f,NavMesh.AllAreas),Is.True);
                    Assert.That(NavMesh.SamplePosition(reverse?route.Start:route.End,out var to,.12f,NavMesh.AllAreas),Is.True);
                    fixture.PlaceNavigatingBody(from.position);var body=fixture.Body;
                    var path=new NavMeshPath();
                    Assert.That(body.Agent.CalculatePath(to.position,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True);
                    Assert.That(body.Agent.SetPath(path),Is.True);
                    bool dispatched=false;float until=Time.time+20,largest=0;var previous=body.transform.position;
                    while(Time.time<until)
                    {
                        dispatched|=body.Riding==owner;
                        var motion=body.transform.position-previous;motion.y=0;largest=Mathf.Max(largest,motion.magnitude);previous=body.transform.position;
                        if(dispatched&&body.Riding==null&&!body.Scripted&&body.OnNavMesh&&Vector3.Distance(body.transform.position,to.position)<.2f)break;
                        yield return null;
                    }
                    Assert.That(dispatched,Is.True,"The real PersonBody must dispatch its owned link");
                    Assert.That(body.OnNavMesh&&!body.NoRoute&&!body.Scripted&&body.Riding==null,Is.True,
                        "Owned walk outcome: nav="+body.OnNavMesh+"; noRoute="+body.NoRoute+"; scripted="+body.Scripted+"; closed="+owner.Closed+"; phase="+body.GetComponent<NativeLandingWalkHandle>()?.FailurePhase);
                    Assert.That(Vector3.Distance(body.transform.position,to.position),Is.LessThan(.2f));
                    Assert.That(largest,Is.LessThan(.2f),"A physical route must not jump between endpoints");
                }
                var graph=Graph(owner);var a=graph.ByLabel("start");var b=graph.ByLabel("end");var clear=Array.Empty<(Vector3,float)>();
                float cost=graph.Explore(a,RouteProfile.Default,clear,Time.time).CostOf(b);
                float danger=graph.Explore(a,RouteProfile.Default,new[]{(route.path[2],.5f)},Time.time).CostOf(b);
                Assert.That(danger-cost,Is.EqualTo(RouteGraph.DangerCost).Within(.02f),"The curved control segment must contribute its hazard cost");
                Assert.That(graph.Survey(a,RouteProfile.Default,new OpenKnowledge()).Reaches(b),Is.True);
                owner.Close();
                Assert.That(graph.Explore(a,RouteProfile.Default,clear,Time.time+.1f).Reaches(b),Is.False,"Closed owned route remained in live cache");
                Assert.That(graph.Survey(a,RouteProfile.Default,new OpenKnowledge()).Reaches(b),Is.True,"Unknown live closure leaked into guidance knowledge");
            }
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var owner=fixture.OwnedNavigationRoute();var route=owner.Entry;yield return fixture.SettlePhysics();
                fixture.Box("BlockedOwnedWalkFixture",new Vector3(0,1,-.6f),new Vector3(2,2,2));Physics.SyncTransforms();
                Assert.That(NavMesh.SamplePosition(route.Start,out var from,.12f,NavMesh.AllAreas),Is.True);
                fixture.PlaceNavigatingBody(from.position);var body=fixture.Body;
                Assert.That(body.Agent.SetDestination(route.End),Is.True);
                float until=Time.time+20;while(Time.time<until&&!body.NoRoute)yield return null;
                Assert.That(body.NoRoute&&owner.Closed&&!body.Scripted&&body.Riding==null,Is.True,"Blocked walking must close the owned link and finish cleanly");
                Assert.That(Vector3.Distance(body.transform.position,route.End),Is.GreaterThan(1),"A blocked actor must not warp to its destination");
            }
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                var body=fixture.Body;var route=fixture.Route(new Vector3(-1.2f,0,0),Vector3.zero,new Vector3(1.2f,0,0));
                body.transform.position=route.Start+Vector3.up*.04f;bool result=true;
                var walking=NativeLandingWalker.Walk(body,route,route.Direction(false),route.End,ok=>result=ok);
                try
                {
                    for(int index=0;index<5&&walking.MoveNext();index++)yield return walking.Current;
                    body.gameObject.SetActive(false);while(walking.MoveNext())yield return walking.Current;
                }
                finally{(walking as IDisposable)?.Dispose();}
                Assert.That(result,Is.False);yield return null;
                Assert.That(UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Any(controller=>controller.name=="Actual native landing walking controller"),Is.False,"Cancelled walking leaked its physical driver");
            }
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator RealNpcWalkerFollowsItsCurvedPhysicalPointsAtWalkingAndResponderSpeedsInBothDirections()
        {
            using(var fixture=new ArchitectureRuntimeFixture())
            {
                fixture.Box("FixtureBoundary",new Vector3(0,1,0),new Vector3(.5f,2,.5f));
                var route=fixture.Route(new Vector3(-1.5f,0,0),new Vector3(-.8f,0,-1),new Vector3(.8f,0,-1),new Vector3(1.5f,0,0));
                foreach(float speed in new[]{1.3f,2.5f})foreach(bool reverse in new[]{false,true})yield return fixture.Walk(route,reverse,true,speed);
            }
        }
    }
}
#endif
