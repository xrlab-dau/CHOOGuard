#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class IndoorCompositionTests : RetainedEmergencySessionFixture
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private string savedKey;
        private int savedSeed;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            savedSeed = EmergencySession.NextSeed;
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20261008;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            EmergencySession.NextSeed = savedSeed;
            Time.timeScale = 1;
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (!emergency.isLoaded && !station.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("IndoorCompositionScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        private static object Field(object instance, string name) => instance.GetType().GetField(name, Members).GetValue(instance);
        private static string Text(object transition, string field) => (string)Field(transition, field);
        private static object[] Origins(IncidentDirector director) => ((IEnumerable)typeof(IncidentDirector).GetMethod("Origins", Members).Invoke(director, null)).Cast<object>().ToArray();

        [UnityTest, Timeout(240000)]
        public IEnumerator LiveOriginsStayInside_ExpiredLocationsAreRejected_AndDifferentSourcesCanShareAKind()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            float until = Time.realtimeSinceStartup + 100;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null) && Time.realtimeSinceStartup < until) yield return null;
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.Crowd, Is.Not.Null);
            Assert.That(session.Jev.Available, Is.False);
            if (!session.Player.IsPaused) session.Player.Pause();
            var director = session.Incidents;
            Assert.That(director.InsideBuilding(session.Player.transform.position), Is.True, "The actual safe indoor spawn must be a valid incident site.");

            var rides = UnityEngine.Object.FindObjectsByType<ChooGuard.Circulation.EscalatorRide>(FindObjectsSortMode.None);
            var bindings = UnityEngine.Object.FindObjectsByType<ChooGuard.App.Fps.Twin.EscalatorDriveBinding>(FindObjectsSortMode.None);
            Assert.That(bindings.Length, Is.EqualTo(21), "All installed source lanes must bind during a real shift, without test-created replacement drives.");
            CollectionAssert.AreEquivalent(session.World.Escalators.Select(e => e.Entry.id), bindings.Select(b => b.lane_id));
            Assert.That(rides.Length, Is.GreaterThan(105), "The 105 platform volumes and preserved concourse/ground volumes must all remain available.");
            foreach (var ride in rides) Assert.That(ride.Drive, Is.Not.Null, "Every installed ride volume must use the actual shift's lane drive.");
            var motions = UnityEngine.Object.FindObjectsByType<ChooGuard.Circulation.EscalatorMotion>(FindObjectsSortMode.None);
            Assert.That(motions.Length, Is.EqualTo(21));
            foreach (var binding in bindings)
            {
                var motion = binding.GetComponent<ChooGuard.Circulation.EscalatorMotion>();
                Assert.That(motion, Is.Not.Null, binding.lane_id);
                Assert.That(motion.Drive, Is.SameAs(session.World.Escalators.Single(e => e.Entry.id == binding.lane_id)), binding.lane_id);
                var platform = binding.GetComponent<ChooGuard.Circulation.PlatformEscalatorRun>();
                if (platform != null) Assert.That(platform.AnimatedOriginalPartCount, Is.EqualTo(41));
                else Assert.That(motion.AnimatedSurfaceCount, Is.GreaterThan(0));
            }
            foreach (var lane in session.World.Escalators)
            {
                lane.Stop("test-only stop");
                foreach (var ride in rides.Where(r => ReferenceEquals(r.Drive, lane))) Assert.That(ride.WorldVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(lane.Restart(out var restartReason), Is.True, restartReason);
            }

            var outside = session.World.Points.Zones.First(z => StationWorld.IsOutdoorZone(z.id));
            var outsidePosition = (outside.min + outside.max) * .5f;
            Assert.That(director.InsideBuilding(outsidePosition), Is.False);
            foreach (var platform in session.World.Points.Platforms)
                Assert.That(director.InsideBuilding((platform.a + platform.b) * .5f), Is.False, "A station platform must not become an indoor incident site even beneath a canopy.");

            var origins = Origins(director);
            Assert.That(origins.Length, Is.GreaterThan(0));
            foreach (var origin in origins)
            {
                var subject = Field(origin, "Subject");
                Assert.That(subject, Is.Not.InstanceOf<TrainService.Car>());
                if (subject is Passenger passenger)
                {
                    Assert.That(passenger.Current, Is.Not.EqualTo(Passenger.Activity.InTrain));
                    Assert.That(director.InsideBuilding(passenger.transform.position), Is.True);
                }
                if (subject is StationPoints.Point point) Assert.That(director.InsideBuilding(point.Position), Is.True);
                CollectionAssert.DoesNotContain(new[] { "track_fall", "track_climb", "underfloor_smoke", "door_trap" }, Text(origin, "Kind"));
            }

            // Query what this real shift offers rather than expecting a particular randomly composed event.
            var passengerOrigin = origins.FirstOrDefault(o => Field(o, "Subject") is Passenger);
            if (passengerOrigin == null)
            {
                var passenger = session.Crowd.People.First(p => !p.Hurt && !p.Hostile);
                passenger.transform.position = session.Player.transform.position;
                Physics.SyncTransforms();
                passengerOrigin = Origins(director).FirstOrDefault(o => ReferenceEquals(Field(o, "Subject"), passenger));
            }
            Assert.That(passengerOrigin, Is.Not.Null, "A live indoor routine passenger must expose at least one existing cause.");
            var source = (Passenger)Field(passengerOrigin, "Subject");
            var originalPosition = source.transform.position;
            var recheck = (Delegate)Field(passengerOrigin, "Recheck");
            Assert.That(recheck.DynamicInvoke(), Is.Not.Null);
            source.transform.position = outsidePosition;
            Physics.SyncTransforms();
            Assert.That(recheck.DynamicInvoke(), Is.Null, "An asynchronous judgment must not carry its passenger's incident out of the building.");
            Assert.That(Origins(director).Any(o => ReferenceEquals(Field(o, "Subject"), source)), Is.False);
            source.transform.position = originalPosition;
            Physics.SyncTransforms();

            var repeatedKind = Origins(director).GroupBy(o => Text(o, "Kind")).FirstOrDefault(g => g.Count() > 1);
            Assert.That(repeatedKind, Is.Not.Null, "Different real sources must be available for the same causal kind.");
            var sameKind = repeatedKind.Take(2).ToArray();
            var used = (HashSet<string>)Field(director, "composedOrigins");
            used.Add(Text(sameKind[0], "Key"));
            var offered = Origins(director);
            Assert.That(offered.Any(o => Text(o, "Key") == Text(sameKind[0], "Key")), Is.False, "An already used exact source transition cannot repeat.");
            Assert.That(offered.Any(o => Text(o, "Key") == Text(sameKind[1], "Key")), Is.True, "A different object must remain available despite sharing the causal kind.");
            typeof(IncidentDirector).GetField("<Stage>k__BackingField", Members).SetValue(director, IncidentDirector.Phase.Incident);
            int beforeOrigins = used.Count;
            typeof(IncidentDirector).GetMethod("Execute", Members).Invoke(director, new object[] { sameKind[1], .3f, 1, "late_origin_commit_test" });
            Assert.That(used.Count, Is.EqualTo(beforeOrigins), "An asynchronous second origin must be rejected at commit time.");
            used.Clear();
            typeof(IncidentDirector).GetField("<Stage>k__BackingField", Members).SetValue(director, IncidentDirector.Phase.Calm);

            var inventory = new JevWorldInventory();
            var snapshot = inventory.Snapshot(0, session.World, session.Crowd, (List<EmergencyArt.HangingItem>)Field(director, "hanging"), director.InsideBuilding);
            var counts = (SortedDictionary<string, int>)snapshot["registered_indoor_equipment_counts"];
            Assert.That(counts.Values.Sum(), Is.EqualTo(EquipmentRegistry.All.Count(e => e != null && e.isActiveAndEnabled && director.InsideBuilding(e.transform.position))));
            Assert.That(ReferenceEquals(snapshot, inventory.Snapshot(.1f, session.World, session.Crowd, null, director.InsideBuilding)), Is.True, "Inventory must be cached between heartbeats.");
            Assert.That(ReferenceEquals(snapshot, inventory.Snapshot(1.1f, session.World, session.Crowd, null, director.InsideBuilding)), Is.False, "Inventory must refresh live state.");

            // A development can name a different person from the original source. Its final application must also
            // reject somebody outside, even when invoked with an old indoor judgment.
            int beforeHazards = HazardRegistry.Active.Count();
            source.transform.position = outsidePosition;
            Physics.SyncTransforms();
            typeof(IncidentDirector).GetMethod("StartStairsFall", Members).Invoke(director, new object[] { source, .5f });
            typeof(IncidentDirector).GetMethod("StartPowerBankFire", Members).Invoke(director, new object[] { source, .5f });
            Assert.That(HazardRegistry.Active.Count(), Is.EqualTo(beforeHazards));
            Assert.That(source.Hurt, Is.False, "An old secondary-injury judgment must not injure a passenger outside.");
            var closestOutside = typeof(IncidentDirector).GetMethod("NearestPerson", Members).Invoke(director,
                new object[] { outsidePosition, 1f, (Func<Passenger, bool>)(p => ReferenceEquals(p, source)) });
            Assert.That(closestOutside, Is.Null, "Development selection must exclude outdoor secondary victims.");
            source.transform.position = originalPosition;
            Physics.SyncTransforms();
            typeof(IncidentDirector).GetMethod("StartPowerBankFire", Members).Invoke(director, new object[] { source, .3f });
            Assert.That(HazardRegistry.Active.Count(), Is.GreaterThan(beforeHazards));
            foreach (var hazard in HazardRegistry.Active.Where(h => h.Localized))
                Assert.That(director.InsideBuilding(hazard.Position), Is.True, "Final applied incident position must stay indoors after local offsets and floor snapping.");
            Assert.That(director.Main.IncidentId, Is.EqualTo(director.Main.Id));
            var sections = (string[])typeof(IncidentDirector).GetMethod("HandoverSections", Members).Invoke(director, null);
            Assert.That(string.Join(" ", sections), Does.Not.Contain(director.Main.Named), "Unperceived world hazards cannot enter the handover.");
            typeof(IncidentDirector).GetMethod("Know", Members).Invoke(director, new object[] { director.Main, "test-only observed incident" });
            var completedWork = (Dictionary<Hazard, float>)Field(director, "workFinishedAt");
            var observedWork = (Dictionary<Hazard, float>)Field(director, "observedAt");
            var observation = (Dictionary<Hazard, string>)Field(director, "seen");
            completedWork[director.Main] = Time.time - 2;
            observedWork[director.Main] = Time.time - 1;
            observation[director.Main] = "synthetic observed state for stale-menu test";
            var tasks = (List<EmergencySession.RadioOption>)typeof(IncidentDirector).GetMethod("WorkflowOptions", Members).Invoke(director, null);
            var recheckOption = tasks.First(o => o.Label.Contains("작업 결과 재확인"));
            completedWork[director.Main] = Time.time;
            recheckOption.Send();
            Assert.That(((HashSet<Hazard>)Field(director, "verifiedWork")).Contains(director.Main), Is.False, "A menu opened before the latest completion must not approve an earlier observation.");
            var resolvedTask = new CollapseHazard("resolved-review-fixture", source, "재확인 시험", "test-only", "test-only", "회복 상태");
            resolvedTask.End();
            typeof(IncidentDirector).GetMethod("Know", Members).Invoke(director, new object[] { resolvedTask, "test-only completed task" });
            completedWork[resolvedTask] = Time.time - 2;
            observedWork[resolvedTask] = Time.time - 1;
            observation[resolvedTask] = "완료 뒤 직접 관찰한 회복 상태";
            tasks = (List<EmergencySession.RadioOption>)typeof(IncidentDirector).GetMethod("WorkflowOptions", Members).Invoke(director, null);
            tasks.First(o => o.Label.Contains(resolvedTask.Named) && o.Label.Contains("작업 결과 재확인")).Send();
            Assert.That(((HashSet<Hazard>)Field(director, "verifiedWork")).Contains(resolvedTask), Is.True, "A resolved non-fire task must remain available for post-completion review.");
            Assert.That(director.TryReassignTeam(Agency.Fire, null, out _), Is.False);
            typeof(IncidentDirector).GetMethod("RequestTeam", Members).Invoke(director, new object[] { 0 });
            typeof(IncidentDirector).GetMethod("SpawnTeam", Members).Invoke(director, new object[] { Agency.Fire });
            var responseTeam = ((List<Responder>)Field(director, "responders")).Where(r => r.Agency == Agency.Fire).ToArray();
            Assert.That(responseTeam.Length, Is.GreaterThan(0));
            var beforePositions = responseTeam.Select(r => r.transform.position).ToArray();
            var otherSite = session.World.Points.All.First(p => p.Kind == PointKind.Office && director.InsideBuilding(p.Position));
            var relatedFire = new FireHazard("reassign-test-related", StationWorld.WalkableNear(otherSite.Position), "test-only", "연관 현장 시험", .2f, session.Art, director.transform);
            typeof(IncidentDirector).GetMethod("Register", Members).Invoke(director, new object[] { relatedFire });
            typeof(IncidentDirector).GetMethod("Know", Members).Invoke(director, new object[] { relatedFire, "test-only perceived related site" });
            Assert.That(director.TryReassignTeam(Agency.Fire, relatedFire, out var reassignFeedback), Is.True, reassignFeedback);
            for (int i = 0; i < responseTeam.Length; i++)
            {
                Assert.That(responseTeam[i].transform.position, Is.EqualTo(beforePositions[i]), "Reassignment must plan a walk rather than teleport a responder.");
                Assert.That(responseTeam[i].OnScene, Is.False);
                Assert.That(((List<Vector3>)Field(responseTeam[i], "legs")).Count, Is.GreaterThan(0));
                Assert.That(Vector3.Distance((Vector3)Field(responseTeam[i], "goal"), relatedFire.Scene), Is.GreaterThan(.75f), "Reassigned fire crew must keep an approach distance from the source.");
            }
            typeof(Responder).GetField("working", Members).SetValue(responseTeam[0], true);
            Assert.That(director.TryReassignTeam(Agency.Fire, director.Main, out _), Is.False, "Hands-on work cannot be abandoned by a reassignment.");
            typeof(Responder).GetField("working", Members).SetValue(responseTeam[0], false);
            Debug.Log("CG_INDOOR_COMPOSITION_TEST origins=" + origins.Length + " indoorEquipment=" + counts.Values.Sum());
        }
    }
}
#endif
