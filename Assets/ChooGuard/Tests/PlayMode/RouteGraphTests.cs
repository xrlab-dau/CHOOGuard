using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // 경로 그래프(Resources/StationRoutes.json)는 navmesh 와 함께 구워 둔 자료다. navmesh 나 역 자료를 다시 구웠는데 그래프를 다시 굽지
    // 않으면 사람들이 길을 못 찾거나 엉뚱한 곳을 향한다: 그때 이 시험이 깨진다(다시 굽기: ChooGuard/Emergency/Bake route graph).
    public sealed class RouteGraphTests : RetainedEmergencySessionFixture
    {
        private string savedKey;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20260930;
        }

        [TearDown]
        public void TearDown() => Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);

        private static IEnumerator StartShift(Action<EmergencySession> ready)
        {
            var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            while (!station.isDone) yield return null;
            var session = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            while (!session.isDone) yield return null;
            float giveUp = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null) && Time.realtimeSinceStartup < giveUp) yield return null;
            Assert.That(EmergencySession.Current, Is.Not.Null, "the shift did not start");
            ready(EmergencySession.Current);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BakedGraph_SitsOnTheNavmeshAndJoinsEveryPlaceToAnExitWithoutTheElevator()
        {
            var historicalRoutes=Resources.Load<TextAsset>(StationWorld.RoutesResource);
            var historicalRouteBytes=historicalRoutes.bytes;
            var historicalWalking=Resources.Load<TextAsset>(StationWalkingRoutes.Resource);
            var historicalWalkingBytes=historicalWalking.bytes;
            var rawWalking=StationWalkingRoutes.Load();
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var world = shift.World;
            var graph = world.Paths.Graph;
            Assert.That(world.UsesCurrentSourceNavigation,Is.True);
            Assert.That(rawWalking.Length,Is.EqualTo(2),"Both historical walking candidates remain source inventory, not a current-placement approval");
            Assert.That(world.SourceWalkingRoutes.Count,Is.EqualTo(rawWalking.Length));
            CollectionAssert.AreEqual(rawWalking.Select(route=>route.id),world.SourceWalkingRoutes.Select(route=>route.id));
            Assert.That(world.WalkingRouteBindings.Count,Is.EqualTo(rawWalking.Length));
            CollectionAssert.AreEqual(rawWalking.Select(route=>route.id),world.WalkingRouteBindings.Select(row=>row.SourceId));
            CollectionAssert.AreEquivalent(world.WalkingRouteBindings.Where(row=>row.Qualified).Select(row=>row.SourceId),
                world.WalkingRoutes.Select(owner=>owner.Entry.id),"Only and every qualified candidate has a current owned walking link");
            Assert.That(world.WalkingRouteBindings.Single(row=>row.SourceId=="esc-well-n-56-low-apron").Qualified,Is.True,
                "The supported current north apron must retain its strict physical binding");
            var currentIds=new HashSet<string>(world.WalkingRoutes.Select(owner=>owner.Entry.id),StringComparer.Ordinal);
            foreach(var row in world.WalkingRouteBindings)
            {
                var original=rawWalking.Single(route=>route.id==row.SourceId);
                CollectionAssert.AreEqual(original.SourcePath,row.SourcePath,"Complete original candidate path/pose remains audit inventory");
                Assert.That(row.SourceEvidence,Is.Not.Null.And.Not.Empty);
                if(row.Qualified)
                {
                    Assert.That(row.Status,Is.EqualTo(StationWalkingRouteBindingStatus.BOUND_CURRENT_SOURCE));
                    Assert.That(row.CurrentEntry,Is.SameAs(world.WalkingRoutes.Single(owner=>owner.Entry.id==row.SourceId).Entry));
                    Assert.That(row.CurrentEntry.CurrentSourceProof,Is.Not.Null.And.Not.Empty);
                }
                else
                {
                    Assert.That(row.Status,Is.EqualTo(StationWalkingRouteBindingStatus.UNBOUND_SOURCE_OBSTRUCTED));
                    Assert.That(row.CurrentEntry,Is.Null);
                    Assert.That(row.SourceEvidence,Does.Contain("allExactStaticCollectedSource=True"),"A query miss/unknown cannot retire a source candidate");
                    Assert.That(row.AlternativeEvidence,Does.StartWith("UNQUALIFIED_NAV_SURFACE_BOUNDARY;"));
                    Assert.That(graph.Nodes.Any(node=>node.Label!=null&&node.Label.StartsWith(StationWalkingRoutes.LinkPrefix+row.SourceId+":",StringComparison.Ordinal)),Is.False,
                        "An unbound historical candidate cannot leave current typed graph endpoints");
                }
                Debug.Log("CURRENT_WALKING_CANDIDATE "+row.SourceId+"; status="+row.Status+"; source={"+row.SourceEvidence+"}; alternative={"+row.AlternativeEvidence+"}");
            }
            var edgeField=typeof(RouteGraph).GetField("edges",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(edgeField,Is.Not.Null);
            var currentEdges=(IEnumerable<RouteGraph.Edge>)edgeField.GetValue(graph);
            var missingField=typeof(RouteGraph.Edge).GetField("Missing",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(missingField,Is.Not.Null);
            Assert.That(currentEdges.All(edge=>!(bool)missingField.GetValue(edge)),Is.True,"No unresolved/unavailable typed source link may remain in the current derived graph");
            Assert.That(currentEdges.All(edge=>edge.Walking==null||currentIds.Contains(edge.Walking.Entry.id)),Is.True,
                "No current graph edge may borrow an unbound historical walking owner");
            Assert.That(world.SourcePoints.All.Count,Is.EqualTo(1449),"All1445 original non-exit records and4 exits remain source inventory");
            Assert.That(world.Points.CurrentBindings.Count,Is.EqualTo(world.SourcePoints.All.Count));
            Assert.That(world.Points.CurrentBindings.Select(r=>r.Source.Id).Distinct().Count(),Is.EqualTo(world.SourcePoints.All.Count));
            for(int i=0;i<world.SourcePoints.All.Count;i++)
            {
                var row=world.Points.CurrentBindings[i];Assert.That(row.Source,Is.SameAs(world.SourcePoints.All[i]));
                Assert.That(row.Evidence,Is.Not.Null.And.Not.Empty);
                if(row.Bound)Assert.That(row.Status,Is.EqualTo("BOUND_MODEL_COMPUTED_WALKABLE"));
                else if(row.Status==StationPoints.CurrentBinding.CurrentSourceUnavailableStatus)
                    Assert.That(row.Evidence.StartsWith(StationPoints.CurrentBinding.CurrentSourceEmptyEvidence,StringComparison.Ordinal)||
                        row.Evidence.StartsWith(StationPoints.CurrentBinding.CurrentSourceFloorEvidence,StringComparison.Ordinal)||
                        row.Evidence.StartsWith(StationPoints.CurrentBinding.CurrentSourceFootprintEvidence,StringComparison.Ordinal),Is.True,
                        "Unavailable modeled endpoints require independent complete current-source support evidence");
                else Assert.That(row.Status,Is.EqualTo("UNBOUND_PROVEN_NO_CURRENT_FLOOR"));
            }
            var destinations=world.Points.CurrentDestinations;
            Assert.That(destinations.Count,Is.EqualTo(1449),"Every raw row needs a separate operational admission row");
            for(int i=0;i<destinations.Count;i++)
            {
                var row=destinations[i];Assert.That(row.Physical,Is.SameAs(world.Points.CurrentBindings[i]));
                Assert.That(row.Source,Is.SameAs(world.SourcePoints.All[i]));Assert.That(row.Evidence,Is.Not.Null.And.Not.Empty);
                if(!row.Physical.Bound)Assert.That(row.Status,Is.EqualTo(StationPoints.NoPhysicalDestination));
                else
                {
                    Assert.That(row.Runtime,Is.SameAs(row.Physical.Runtime),"Pending retains its positive physical Runtime");
                    Assert.That(row.Status,Is.EqualTo(StationPoints.ModelActiveDestination).Or.EqualTo(StationPoints.PendingDestination));
                    Assert.That(row.Runtime.Anchor,Is.EqualTo(row.Source.Anchor));Assert.That(row.Runtime.Yaw,Is.EqualTo(row.Source.Yaw));
                    Assert.That(row.Runtime.Slot,Is.EqualTo(row.Source.Slot));Assert.That(row.Runtime.Kind,Is.EqualTo(row.Source.Kind));
                    if(row.Active)Assert.That(row.Evidence,Does.StartWith("CERTIFIED_CURRENT_MODEL_FOOT_DESTINATION;"));
                    else
                    {
                        Assert.That(row.Source.Kind,Is.Not.EqualTo(PointKind.Exit));
                        Assert.That(row.Evidence,Does.StartWith("PENDING_HISTORICAL_MODEL_CORRESPONDENCE;"));
                        Assert.That(graph.ByLabel(row.Source.Id),Is.Null,"Pending is retained in audit, never a graph target");
                        Assert.Throws<InvalidOperationException>(()=>world.RequireActiveDestination(row.Runtime));
                    }
                }
            }
            Assert.That(world.Points.All,Is.EquivalentTo(destinations.Where(row=>row.Active).Select(row=>row.Runtime)));
            Assert.That(world.Points.All.Count+destinations.Count(row=>row.Status==StationPoints.PendingDestination)+
                world.Points.CurrentBindings.Count(row=>!row.Bound),Is.EqualTo(1449));
            foreach(var group in world.Points.CurrentBindings.Where(row=>row.Bound).GroupBy(row=>row.Source.Kind))
                Assert.That(world.Points.Of(group.Key),Is.Not.Empty,"No physically positive functional kind may silently collapse: "+group.Key);
            var expectedRequired=world.Points.Of(PointKind.Exit).Select(exit=>exit.Id).ToList();
            foreach(var owner in world.Escalators.Where(owner=>owner.NavigationAvailable))
            {expectedRequired.Add(owner.Entry.id+":start");expectedRequired.Add(owner.Entry.id+":end");}
            foreach(var owner in world.WalkingRoutes)
            {expectedRequired.Add(StationWalkingRoutes.LinkPrefix+owner.Entry.id+":start");expectedRequired.Add(StationWalkingRoutes.LinkPrefix+owner.Entry.id+":end");}
            foreach(var owner in world.Elevators.Where(owner=>owner.NavigationAvailable))
                for(int i=0;i<owner.Entry.stops.Length;i++)expectedRequired.Add(owner.Entry.id+":"+i);
            CollectionAssert.AreEquivalent(expectedRequired,world.Points.RequiredCurrentEndpoints.Select(row=>row.Label));
            Assert.That(world.Points.RequiredCurrentEndpoints.Select(row=>row.Label).Distinct().Count(),Is.EqualTo(expectedRequired.Count));
            Assert.That(world.SourcePoints.Of(PointKind.Exit).Count,Is.EqualTo(4),"All four original modeled Exit records remain immutable source inventory");
            Assert.That(world.Points.SourceAll.Count(p=>p.Kind==PointKind.Exit),Is.EqualTo(4));
            Assert.That(world.Points.Of(PointKind.Exit),Is.Not.Empty,"The current world requires a physically qualified active exit");
            CollectionAssert.AreEquivalent(world.Points.CurrentBindings.Where(r=>r.Bound&&r.Source.Kind==PointKind.Exit).Select(r=>r.Source.Id),
                world.Points.Of(PointKind.Exit).Select(p=>p.Id),"Every and only qualified current Exit becomes an active target");
            Assert.That(graph.OriginalSourceJson,Is.EqualTo(historicalRoutes.text));
            CollectionAssert.AreEqual(historicalRouteBytes,historicalRoutes.bytes,"Runtime graph derivation must preserve the original resource");
            Assert.That(graph.Nodes.Count, Is.GreaterThan(200), "the baked route graph is empty");

            var off = graph.Nodes.Where(node => !NavMesh.SamplePosition(node.Position, out _, .6f, NavMesh.AllAreas)).ToList();
            Assert.That(off, Is.Empty, off.Count + " route graph nodes are off the navmesh (the graph is older than the navmesh): " + string.Join(", ", off.Take(5).Select(n => n.Position.ToString("F1"))));

            var exits = world.Points.Of(PointKind.Exit).Select(exit => graph.ByLabel(exit.Id)).ToList();
            Assert.That(exits, Has.None.Null, "every exit needs a node in the route graph");
            var fields = exits.Select(exit => graph.Toward(exit, RouteProfile.Evacuation, Array.Empty<(Vector3, float)>(), Time.time)).ToList();
            foreach(var point in world.Points.All)
            {
                var node=graph.ByLabel(point.Id);Assert.That(node,Is.Not.Null,point.Id+" exact active label is required");
                Assert.That(Vector3.Distance(node.Position,point.Position),Is.LessThanOrEqualTo(.6f));
                Assert.That(Mathf.Abs(node.Position.y-point.Position.y),Is.LessThanOrEqualTo(.35f));
                Assert.That(fields.Any(field=>field.Reaches(node)),Is.True,point.Id+" must reach an exit on foot");
            }
            foreach(var endpoint in world.Points.RequiredCurrentEndpoints)
            {
                var node=graph.ByLabel(endpoint.Label);Assert.That(node,Is.Not.Null);
                Assert.That(endpoint.MaxAttachmentDistance,Is.GreaterThan(0));Assert.That(endpoint.MaxAttachmentHeight,Is.GreaterThan(0));
                Assert.That(Vector3.Distance(node.Position,endpoint.SourcePosition),Is.LessThanOrEqualTo(endpoint.MaxAttachmentDistance));
                Assert.That(Mathf.Abs(node.Position.y-endpoint.SourcePosition.y),Is.LessThanOrEqualTo(endpoint.MaxAttachmentHeight));
                Assert.That(fields.Any(field=>field.Reaches(node)),Is.True,"Mandatory source owner cannot be retired: "+endpoint.Label);
                Assert.That(endpoint.Evidence,Does.Contain("CERTIFIED_CURRENT_DIRECTED_EVACUATION_EXIT_CHAIN"));
            }

            var reference=world.Baked;
            var referenceRoutesField=typeof(BakedFloor).GetField("walkingRoutes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var referenceLinksField=typeof(BakedFloor).GetField("walkingLinks",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(referenceRoutesField,Is.Not.Null);Assert.That(referenceLinksField,Is.Not.Null);
            var referenceRoutes=(StationWalkingRouteEntry[])referenceRoutesField.GetValue(reference);
            var referenceLinks=(List<NavMeshLinkInstance>)referenceLinksField.GetValue(reference);
            CollectionAssert.AreEquivalent(currentIds,referenceRoutes.Select(route=>route.id),"Current guidance reference owns only the same qualified entries");
            Assert.That(referenceLinks.Count,Is.EqualTo(currentIds.Count));
            Assert.That(referenceLinks.All(link=>NavMesh.IsLinkValid(link)),Is.True);
            CollectionAssert.AreEqual(historicalWalkingBytes,historicalWalking.bytes,"Runtime qualification preserves the entire original walking Resource, including historical review metadata");
            // This case checks current static qualification/ownership and the graph.
            // Existing isolated NativeWalkingRoutePlayTests retain actual CC coverage;
            // no current-scene dynamic traversal result is inferred from that fixture.

            var scratch = SceneManager.CreateScene("RouteGraphScratch");
            SceneManager.SetActiveScene(scratch);
            yield return SceneManager.UnloadSceneAsync(SceneFlow.EmergencyScene);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(SceneFlow.StationScene);
        }
    }
}
