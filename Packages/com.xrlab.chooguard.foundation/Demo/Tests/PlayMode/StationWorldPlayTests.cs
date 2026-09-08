#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class StationWorldPlayTests
    {
        private Scene scene;
        private StationWorldController world;
        private DemoWalkGraph graph;
        private CharacterController character;
        private Transform root;
        [UnitySetUp] public IEnumerator Setup()
        {
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity",new LoadSceneParameters(LoadSceneMode.Additive));yield return null;
            root=scene.GetRootGameObjects().Single().transform;world=root.GetComponentInChildren<StationWorldController>();
            Assert.That(world,Is.Not.Null);Assert.That(world.IsConfigured,Is.True);world.enabled=false;
            graph=root.GetComponentInChildren<DemoWalkGraph>();character=world.Player.GetComponent<CharacterController>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        private void Start(int seed){world.StartShift(seed);world.Player.SetControlEnabled(false);}
        private void TickUntilIncident()
        {
            for(var i=0;i<4000&&world.Session.Phase!=StationPhase.Incident;i++)world.TickWorld(.05f);
            Assert.That(world.Session.Active,Is.Not.Null,"Eligible incident must start during a bounded station session.");
        }
        private void WalkTo(Vector3 destination)
        {
            destination.y=.1f;var route=graph.Route(world.Player.transform.position,destination);
            Assert.That(route.Length,Is.GreaterThan(0),"Unreachable player destination "+destination);
            foreach(var point in route.Concat(new[]{destination}))
            {
                var ticks=0;
                while(DemoEvacuee.Horizontal(world.Player.transform.position,point)>.035f&&ticks++<600)
                {
                    var delta=point-world.Player.transform.position;delta.y=0;character.Move(Vector3.ClampMagnitude(delta,.12f));world.TickWorld(.05f);
                }
                Assert.That(ticks,Is.LessThan(600),"Player blocked en route to "+point);
            }
        }
        private DemoInteractable Device(string id){return root.GetComponentsInChildren<DemoInteractable>().Single(x=>x.AnchorId==id);}
        private bool UseDevice(string id)
        {
            var device=Device(id);var at=device.transform.position-device.transform.forward*1.3f;at.y=.1f;WalkTo(at);
            world.Player.ViewCamera.transform.rotation=Quaternion.LookRotation(device.InteractionPoint-world.Player.ViewCamera.transform.position);Physics.SyncTransforms();return world.TryInteract();
        }
        private void ApproachNpc(DemoEvacuee npc)
        {
            var candidates=new List<Vector3>();
            for(var i=0;i<8;i++)
            {
                var p=npc.transform.position+Quaternion.Euler(0,i*45,0)*Vector3.back*1.35f;p.y=.1f;
                if(!Physics.CheckCapsule(p+Vector3.up*.34f,p+Vector3.up*1.46f,.34f,1,QueryTriggerInteraction.Ignore)&&graph.Clear(p,npc.transform.position)&&graph.Route(world.Player.transform.position,p).Length>0)candidates.Add(p);
            }
            Assert.That(candidates,Is.Not.Empty,"NPC needs a reachable approach: "+npc.NpcId+" "+npc.transform.position);
            foreach(var approach in candidates.OrderBy(p=>Vector3.Distance(p,world.Player.transform.position)))
            {
                WalkTo(approach);world.Player.ViewCamera.transform.rotation=Quaternion.LookRotation(npc.transform.position+Vector3.up-world.Player.ViewCamera.transform.position);Physics.SyncTransforms();
                RaycastHit hit;
                if(Physics.Raycast(world.Player.ViewCamera.transform.position,world.Player.ViewCamera.transform.forward,out hit,world.Player.InteractionReach,~0,QueryTriggerInteraction.Collide)&&hit.collider.GetComponentInParent<DemoEvacuee>()==npc)return;
            }
            Assert.Fail("No unoccluded approach to "+npc.NpcId);
        }
        private void ResolveCurrentIncident(bool fakeCloneCheck)
        {
            var incident=world.Session.Active;
            if(!incident.Discovered)Assert.That(UseDevice("anchor-01"),Is.True);
            var chosen=world.Portals.First(x=>x.SiteId!=incident.SiteId);
            Assert.That(UseDevice("choose-"+chosen.SiteId),Is.True);
            var report=UseDevice(incident.ReportAnchor);
            if(!report)report=UseDevice(incident.ReportAnchor); // A timed communication inject may occur while walking.
            Assert.That(report,Is.True,"Use the currently available report channel.");
            if(incident.RequiresNotice)Assert.That(UseDevice("anchor-02"),Is.True);
            var affected=world.Crowd.Where(x=>incident.AffectedNpcIds.Contains(x.NpcId)).ToArray();
            Assert.That(affected.Length,Is.InRange(2,6));
            foreach(var npc in affected)
            {
                ApproachNpc(npc);
                if(fakeCloneCheck)
                {
                    var fake=new GameObject("Unregistered actor");fake.transform.SetParent(root);fake.layer=2;
                    var collider=fake.AddComponent<CapsuleCollider>();collider.isTrigger=true;collider.height=1.75f;collider.radius=.3f;collider.center=Vector3.up*.875f;
                    var clone=fake.AddComponent<DemoEvacuee>();var index=int.Parse(npc.NpcId.Substring(4));
                    clone.Configure(npc.GroupId,index,world.Player.ViewCamera.transform.position+world.Player.ViewCamera.transform.forward*.85f-Vector3.up*.875f,npc.WaitingSlot);
                    Physics.SyncTransforms();Assert.That(world.TryInteract(),Is.False,"Unregistered same-ID NPC cannot consume recruitment.");Assert.That(incident.IsRecruited(npc.NpcId),Is.False);
                    Object.DestroyImmediate(fake);Physics.SyncTransforms();fakeCloneCheck=false;
                }
                Assert.That(world.TryInteract(),Is.True,"Recruit real nearby NPC "+npc.NpcId+" last="+world.Session.Trace.Last().action+"/"+world.Session.Trace.Last().target+"/"+world.Session.Trace.Last().detail);
            }
            var before=world.Crowd.Select(x=>x.transform.position).ToArray();var clock=world.Session.Seconds;
            world.PauseWorld();for(var i=0;i<10;i++)world.TickWorld(1);Assert.That(world.Session.Seconds,Is.EqualTo(clock));Assert.That(world.Crowd.Select(x=>x.transform.position),Is.EqualTo(before));Assert.That(world.TryInteract(),Is.False);
            world.ResumeWorld();world.Player.SetControlEnabled(false);
            WalkTo(chosen.Waypoint-Vector3.forward*.8f);Assert.That(world.CrossedChosenRoute,Is.False,"Approaching the south side alone is not a crossing.");
            WalkTo(chosen.Waypoint+Vector3.forward*.8f);Assert.That(world.CrossedChosenRoute,Is.True);
            WalkTo(root.Find("Markers/AssemblyPoint").position);
            for(var i=0;i<2500&&incident.ArrivedCount<affected.Length;i++)world.TickWorld(.05f);
            Assert.That(incident.ArrivedCount,Is.EqualTo(affected.Length),"Missing arrivals "+incident.SiteId+"/"+incident.Kind+": "+string.Join(";",affected.Select(x=>x.NpcId+"="+x.State+"@"+x.transform.position+" via="+x.EscortPortalVisited)));
            Assert.That(UseDevice("assembly-register"),Is.True);Assert.That(world.Session.Phase,Is.EqualTo(StationPhase.Recovery));
        }
        [UnityTest]
        public IEnumerator OrdinaryPopulationMovesAndUnseenIncidentHasNoSolutionGuide()
        {
            Start(2107);var origin=world.Crowd.Select(x=>x.transform.position).ToArray();
            for(var i=0;i<600;i++)world.TickWorld(.05f);
            Assert.That(world.Session.Phase,Is.EqualTo(StationPhase.Ordinary));
            Assert.That(world.Crowd.Where((x,i)=>DemoEvacuee.Horizontal(x.transform.position,origin[i])>3).Count(),Is.GreaterThanOrEqualTo(4));
            Assert.That(world.Crowd.Count(x=>x.RoutineVisits>0),Is.GreaterThanOrEqualTo(4),string.Join(";",world.Crowd.Select(x=>x.NpcId+" visits="+x.RoutineVisits+"@"+x.transform.position)));
            Assert.That(root.GetComponentInChildren<DemoWorldGuide>().DestinationId,Is.Null);
            TickUntilIncident();Assert.That(world.Session.Active.Discovered,Is.False);Assert.That(root.GetComponentInChildren<DemoWorldGuide>().DestinationId,Is.Null);
            yield return null;
        }
        [UnityTest]
        public IEnumerator RandomIncidentVariationsAndTwoConsecutiveEpisodesRecoverWithoutTeleporting()
        {
            var observed=new HashSet<string>();
            foreach(var seed in new[]{10,1,2,104,2107,6123})
            {
                Start(seed);TickUntilIncident();observed.Add(world.Session.Active.SiteId+world.Session.Active.Kind);
                ResolveCurrentIncident(seed==10);var history=world.Session.History.Last();
                Debug.Log("STATION_WORLD_CASE seed="+seed+" site="+history.site+" kind="+history.kind+" affected="+history.affected+" arrived="+history.arrived);
                Assert.That(history.arrived,Is.EqualTo(history.affected));Assert.That(history.reportedAt,Is.GreaterThanOrEqualTo(history.observedAt));
                var playerPosition=world.Player.transform.position;
                while(world.Session.Phase==StationPhase.Recovery)world.TickWorld(.05f);
                Assert.That(world.Player.transform.position,Is.EqualTo(playerPosition));Assert.That(world.Portals.All(x=>!x.Unavailable),Is.True);
                Assert.That(world.Crowd.All(x=>x.State==EvacueeState.Wander),Is.True,"Return physically to ordinary routines.");
                if(seed==10)
                {
                    var oldId=history.incidentId;TickUntilIncident();Assert.That(world.Session.Active.Id,Is.Not.EqualTo(oldId));ResolveCurrentIncident(false);
                    Assert.That(world.Session.History.Count,Is.EqualTo(2));
                }
                yield return null;
            }
            Assert.That(observed.Count,Is.EqualTo(6),"Cover both incident types at all three locations.");
        }
    }
}
#endif
