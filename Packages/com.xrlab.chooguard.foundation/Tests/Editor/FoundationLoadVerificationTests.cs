using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Multiplayer.Editor;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Tests
{
    [TestFixture]
    public sealed class FoundationLoadVerificationTests
    {
        private const int WarmupTicks = 5;
        private const int MeasuredSamples = 20;
        private const int ExpectedParticipants = 20;
        private const int ExpectedNpc = 100;
        private const int ExpectedActiveIncidents = 2;
        private static readonly DateTime MetricUtcStart = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);

        private string fixtureFolder;
        private bool fixtureMutationStarted;
        private SceneSetup[] previousSceneSetup;
        private ConnectedWorldDefinition world;
        private ConnectedRegionView[] views;

        private sealed class TestCommitSink : ICommitSink
        {
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public void Append(ShiftCommit commit) => Commits.Add(commit.Copy());
        }

        [OneTimeSetUp]
        public void BuildFixture()
        {
            previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Assert.Ignore("Preserve unsaved scenes before running scene builder tests.");
            }

            fixtureMutationStarted = true;
            fixtureFolder = "Assets/CHOOguardGenerated/LoadVerificationTest_" + Guid.NewGuid().ToString("N");
            var scenePaths = ConnectedWorldSceneBuilder.Build(fixtureFolder);
            foreach (var path in scenePaths.Skip(1))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            views = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void CleanFixture()
        {
            if (!fixtureMutationStarted) return;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previousSceneSetup != null && previousSceneSetup.Any(s => s.isActive && s.isLoaded) &&
                previousSceneSetup.All(s => !string.IsNullOrEmpty(s.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }

            if (fixtureFolder != null)
            {
                AssetDatabase.DeleteAsset(fixtureFolder);
            }
        }

        private static (string RoleId, string TeamId, bool IsInstructor) GetParticipantRole(int index)
        {
            if (index == 0) return ("instructor", "command", true);
            var roleNumber = (index - 1) % 5 + 1;
            var teamNumber = (index - 1) / 5 + 1;
            return ("role-" + roleNumber.ToString("D2", CultureInfo.InvariantCulture), "team-" + teamNumber, false);
        }

        private FoundationSimulationProfile CreateLoadProfile(string layout)
        {
            var profile = JsonUtility.FromJson<FoundationSimulationProfile>(
                File.ReadAllText("foundation/world/foundation-simulation-profile.json"));
            profile.NpcCount = ExpectedNpc;
            profile.Schedule.MaximumActive = ExpectedActiveIncidents;
            profile.Schedule.FirstOnsetTick = 1;
            profile.Schedule.MinimumQuietTicks = 1;
            profile.Schedule.MaximumQuietTicks = 1;
            profile.Schedule.Choices = profile.Schedule.Choices.Where(c => c.Kind == FoundationIncidentKind.Fire).ToArray();
            profile.Incidents = profile.Incidents.Where(i => profile.Schedule.Choices.Any(c => c.Id == i.ChoiceId)).ToArray();
            profile.NpcSpawnRegions = layout == "gathered"
                ? new[] { "station_concourse_2f" }
                : world.Regions.Select(r => r.Id).ToArray();
            return profile;
        }

        private WorldState CreateLoadSeed(string layout)
        {
            var seed = JsonUtility.FromJson<WorldState>(File.ReadAllText("foundation/network/connected-world-layout.json"));
            var participants = new List<ParticipantState>();
            var counts = new Dictionary<string, int>();

            for (var i = 0; i < ExpectedParticipants; i++)
            {
                var role = GetParticipantRole(i);
                var participantId = i == 0 ? "instructor" : "participant-" + i.ToString("D2", CultureInfo.InvariantCulture);
                ConnectedRegionDefinition region;
                SpatialPose pose;

                if (layout == "gathered")
                {
                    region = world.Region("station_concourse_2f");
                    pose = world.Pose(region.Id, region.Hub);
                    pose.Position.X += (i % 5 - 2) * 1.2f;
                    pose.Position.Z += (i / 5 - 1.5f) * 1.2f;
                }
                else
                {
                    region = i == 0 ? world.Region("station_concourse_2f") : world.Regions[(i - 1) % 13];
                    pose = world.Pose(region.Id, region.Hub);
                    if (!counts.ContainsKey(region.Id)) counts[region.Id] = 0;
                    pose.Position.X += counts[region.Id] * 1.2f;
                    counts[region.Id]++;
                }

                participants.Add(new ParticipantState
                {
                    ParticipantId = participantId,
                    TeamId = role.TeamId,
                    RoleId = role.RoleId,
                    IsInstructor = role.IsInstructor,
                    RegionId = pose.RegionId,
                    FrameId = pose.FrameId,
                    Position = pose.Position,
                    LocalPosition = pose.LocalPosition,
                    ObservedIds = Array.Empty<string>()
                });
            }

            seed.Participants = participants.ToArray();
            return seed;
        }

        private static void AssertExactRoleRoster(ParticipantState[] participants)
        {
            Assert.That(participants, Has.Length.EqualTo(ExpectedParticipants));
            Assert.That(participants.Select(p => p.ParticipantId).Distinct().Count(), Is.EqualTo(ExpectedParticipants));
            for (var i = 0; i < participants.Length; i++)
            {
                var expected = GetParticipantRole(i);
                Assert.That(participants[i].ParticipantId,
                    Is.EqualTo(i == 0 ? "instructor" : "participant-" + i.ToString("D2", CultureInfo.InvariantCulture)));
                Assert.That(participants[i].RoleId, Is.EqualTo(expected.RoleId), "Role at roster index " + i);
                Assert.That(participants[i].TeamId, Is.EqualTo(expected.TeamId), "Team at roster index " + i);
                Assert.That(participants[i].IsInstructor, Is.EqualTo(expected.IsInstructor), "Instructor flag at roster index " + i);
            }

            Assert.That(participants.Count(p => p.RoleId == "instructor"), Is.EqualTo(1));
            Assert.That(participants.Count(p => p.RoleId == "role-01"), Is.EqualTo(4));
            Assert.That(participants.Count(p => p.RoleId == "role-02"), Is.EqualTo(4));
            Assert.That(participants.Count(p => p.RoleId == "role-03"), Is.EqualTo(4));
            Assert.That(participants.Count(p => p.RoleId == "role-04"), Is.EqualTo(4));
            Assert.That(participants.Count(p => p.RoleId == "role-05"), Is.EqualTo(3));
            Assert.That(participants.Count(p => p.TeamId == "command"), Is.EqualTo(1));
            Assert.That(participants.Count(p => p.TeamId == "team-1"), Is.EqualTo(5));
            Assert.That(participants.Count(p => p.TeamId == "team-2"), Is.EqualTo(5));
            Assert.That(participants.Count(p => p.TeamId == "team-3"), Is.EqualTo(5));
            Assert.That(participants.Count(p => p.TeamId == "team-4"), Is.EqualTo(4));
        }

        private static void AssertPointEquals(Point3 actual, Point3 expected, string message)
        {
            Assert.That(actual.DistanceSquared(expected), Is.LessThanOrEqualTo(1e-8), message);
        }

        private void AssertPoseContract(string identity, string regionId, string frameId, Point3 position, Point3 localPosition)
        {
            var region = world.Region(regionId);
            Assert.That(frameId, Is.EqualTo(region.FrameId), identity + " must use its region's authored frame.");
            AssertPointEquals(position, world.Frame(frameId).ToWorld(localPosition),
                identity + " world position must be the authored frame transform of local position.");
            Assert.That(region.Contains(position), Is.True, identity + " must be realized inside its observed region.");
        }

        private void AssertInitialSpatialLayout(WorldState observed, FoundationSimulationProfile profile, string layout)
        {
            Assert.That(observed.SchemaVersion, Is.EqualTo(3));
            Assert.That(observed.SimulationTick, Is.Zero, "The spatial contract is observed before motion.");
            AssertExactRoleRoster(observed.Participants);

            for (var index = 0; index < observed.Participants.Length; index++)
            {
                var participant = observed.Participants[index];
                var expectedRegion = layout == "gathered"
                    ? "station_concourse_2f"
                    : index == 0 ? "station_concourse_2f" : world.Regions[(index - 1) % world.Regions.Length].Id;
                Assert.That(participant.RegionId, Is.EqualTo(expectedRegion),
                    participant.ParticipantId + " must realize the requested participant distribution.");
                AssertPoseContract(participant.ParticipantId, participant.RegionId, participant.FrameId,
                    participant.Position, participant.LocalPosition);
            }

            var evacuees = observed.Entities.Where(entity => entity.Kind == EntityKind.Evacuee)
                .OrderBy(entity => entity.EntityId, StringComparer.Ordinal).ToArray();
            Assert.That(evacuees, Has.Length.EqualTo(ExpectedNpc));
            for (var index = 0; index < evacuees.Length; index++)
            {
                var evacuee = evacuees[index];
                var expectedId = "npc-" + index.ToString("D3", CultureInfo.InvariantCulture);
                var expectedRegion = profile.NpcSpawnRegions[index % profile.NpcSpawnRegions.Length];
                Assert.That(evacuee.EntityId, Is.EqualTo(expectedId), "Evacuee identity must remain deterministic.");
                Assert.That(evacuee.RegionId, Is.EqualTo(expectedRegion),
                    evacuee.EntityId + " must realize the requested evacuee distribution.");
                AssertPoseContract(evacuee.EntityId, evacuee.RegionId, evacuee.FrameId,
                    evacuee.Position, evacuee.LocalPosition);
            }

            var participantRegions = observed.Participants.GroupBy(participant => participant.RegionId)
                .ToDictionary(group => group.Key, group => group.Count());
            var evacueeRegions = evacuees.GroupBy(evacuee => evacuee.RegionId)
                .ToDictionary(group => group.Key, group => group.Count());
            if (layout == "gathered")
            {
                Assert.That(participantRegions, Has.Count.EqualTo(1));
                Assert.That(participantRegions["station_concourse_2f"], Is.EqualTo(ExpectedParticipants));
                Assert.That(evacueeRegions, Has.Count.EqualTo(1));
                Assert.That(evacueeRegions["station_concourse_2f"], Is.EqualTo(ExpectedNpc));
            }
            else
            {
                Assert.That(participantRegions, Has.Count.EqualTo(world.Regions.Length));
                Assert.That(evacueeRegions, Has.Count.EqualTo(profile.NpcSpawnRegions.Length));
                for (var index = 0; index < profile.NpcSpawnRegions.Length; index++)
                {
                    var expected = ExpectedNpc / profile.NpcSpawnRegions.Length +
                        (index < ExpectedNpc % profile.NpcSpawnRegions.Length ? 1 : 0);
                    Assert.That(evacueeRegions[profile.NpcSpawnRegions[index]], Is.EqualTo(expected),
                        "Dispersed evacuees must realize the deterministic modulo distribution.");
                }
            }
        }

        private static void AssertHistogramAndPercentile(RuntimeMetricHistogram histogram, double[] samples, double percentile)
        {
            Assert.That(histogram.Bounds, Is.Not.Null.And.Not.Empty);
            Assert.That(histogram.Counts, Has.Length.EqualTo(histogram.Bounds.Length + 1));
            Assert.That(histogram.Bounds.All(value => !double.IsNaN(value) && !double.IsInfinity(value)), Is.True);
            Assert.That(histogram.Bounds.All(bound => bound > 0), Is.True);
            Assert.That(histogram.Bounds.Zip(histogram.Bounds.Skip(1), (a, b) => a < b).All(x => x), Is.True);
            Assert.That(histogram.Counts.Sum(), Is.EqualTo(samples.LongLength));

            var expectedCounts = new long[histogram.Bounds.Length + 1];
            foreach (var sample in samples)
            {
                var bucket = 0;
                while (bucket < histogram.Bounds.Length && sample > histogram.Bounds[bucket]) bucket++;
                expectedCounts[bucket]++;
            }
            Assert.That(histogram.Counts, Is.EqualTo(expectedCounts));

            var sorted = samples.OrderBy(x => x).ToArray();
            var rank = (int)Math.Ceiling(percentile * sorted.Length);
            var exact = sorted[rank - 1];
            long cumulative = 0;
            var selectedBucket = 0;
            while (selectedBucket < histogram.Counts.Length)
            {
                cumulative += histogram.Counts[selectedBucket];
                if (cumulative >= rank) break;
                selectedBucket++;
            }

            Assert.That(selectedBucket, Is.LessThan(histogram.Counts.Length),
                "The percentile rank must select an observed histogram bucket.");
            if (selectedBucket == 0)
            {
                Assert.That(exact, Is.InRange(0, histogram.Bounds[0]), "The first histogram bin includes zero.");
            }
            else if (selectedBucket < histogram.Bounds.Length)
            {
                Assert.That(exact, Is.GreaterThan(histogram.Bounds[selectedBucket - 1]));
                Assert.That(exact, Is.LessThanOrEqualTo(histogram.Bounds[selectedBucket]));
            }
            else
            {
                Assert.That(exact, Is.GreaterThan(histogram.Bounds[histogram.Bounds.Length - 1]),
                    "The overflow bin has no invented finite upper bound.");
            }
        }

        private static RuntimeMetricHistogram AccumulateHistogram(double[] samples)
        {
            var accumulator = new RuntimeMetricAccumulator(
                "server", 0, MetricUtcStart, 0, 0, 0, batch: true, nullGraphics: true);
            for (var index = 0; index < samples.Length; index++)
            {
                accumulator.Record(index + 1, samples[index], 0, 0, 0, 0, 0, 0, false, 0);
            }

            return accumulator.Close(samples.Length + 1, MetricUtcStart.AddSeconds(samples.Length + 1)).TickMilliseconds;
        }

        [Test]
        public void HistogramConsistencyAcceptsZeroFiniteBoundsAndOverflowPercentiles()
        {
            var inclusiveSamples = new[] { 0d, 1d };
            var inclusiveHistogram = AccumulateHistogram(inclusiveSamples);
            AssertHistogramAndPercentile(inclusiveHistogram, inclusiveSamples, 0.5);
            AssertHistogramAndPercentile(inclusiveHistogram, inclusiveSamples, 1.0);

            var overflowSamples = Enumerable.Repeat(0d, 18).Concat(new[] { 2500d, 2500d }).ToArray();
            AssertHistogramAndPercentile(AccumulateHistogram(overflowSamples), overflowSamples, 0.95);
        }

        [TestCase("dispersed")]
        [TestCase("gathered")]
        public void RoleDistributionEnforcesExactTwentyParticipantsRoster(string layout)
        {
            AssertExactRoleRoster(CreateLoadSeed(layout).Participants);
        }

        [Test]
        public void SpatialLayoutAssertionRejectsIdenticalObservedDistribution()
        {
            var gatheredProfile = CreateLoadProfile("gathered");
            using var gatheredSimulation = new FoundationWorldSimulation(
                world, views, gatheredProfile, CreateLoadSeed("gathered"));
            var gatheredObservedCopy = gatheredSimulation.InitialState.Copy();
            var dispersedProfile = CreateLoadProfile("dispersed");

            var participantAssertion = Assert.Throws<AssertionException>(() =>
                AssertInitialSpatialLayout(gatheredObservedCopy, dispersedProfile, "dispersed"));
            Assert.That(participantAssertion.Message, Does.Contain("requested participant distribution"));

            using var dispersedSimulation = new FoundationWorldSimulation(
                world, views, dispersedProfile, CreateLoadSeed("dispersed"));
            var wrongEvacueeCopy = dispersedSimulation.InitialState.Copy();
            var gatheredEvacuees = gatheredObservedCopy.Entities
                .Where(entity => entity.Kind == EntityKind.Evacuee).Select(entity => entity.Copy());
            wrongEvacueeCopy.Entities = wrongEvacueeCopy.Entities
                .Where(entity => entity.Kind != EntityKind.Evacuee).Concat(gatheredEvacuees).ToArray();

            var evacueeAssertion = Assert.Throws<AssertionException>(() =>
                AssertInitialSpatialLayout(wrongEvacueeCopy, dispersedProfile, "dispersed"));
            Assert.That(evacueeAssertion.Message, Does.Contain("requested evacuee distribution"));
        }

        [Test]
        public void DispersedLayoutCouplesObservedPopulationMetricsAndSerializedCheckpoint()
        {
            RunCoupledLayout("dispersed");
        }

        [Test]
        public void GatheredLayoutCouplesObservedPopulationMetricsAndSerializedCheckpoint()
        {
            RunCoupledLayout("gathered");
        }

        private void RunCoupledLayout(string layout)
        {
            var profile = CreateLoadProfile(layout);
            var seed = CreateLoadSeed(layout);
            AssertExactRoleRoster(seed.Participants);

            if (layout == "gathered")
            {
                Assert.That(seed.Participants.All(p => p.RegionId == "station_concourse_2f"), Is.True);
                for (var i = 0; i < seed.Participants.Length; i++)
                {
                    for (var j = i + 1; j < seed.Participants.Length; j++)
                    {
                        var a = seed.Participants[i].Position;
                        var b = seed.Participants[j].Position;
                        var distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
                        Assert.That(distance, Is.GreaterThanOrEqualTo(1.19),
                            string.Format(CultureInfo.InvariantCulture, "Participants {0} and {1} overlap.", i, j));
                    }
                }
            }
            else
            {
                Assert.That(seed.Participants.Select(p => p.RegionId).Distinct().Count(), Is.EqualTo(13));
            }

            using var simulation = new FoundationWorldSimulation(world, views, profile, seed) { ProfilePhases = true };
            var observedInitialState = simulation.InitialState;
            AssertInitialSpatialLayout(observedInitialState, profile, layout);
            var shift = new AuthoritativeShift(observedInitialState, new TestCommitSink(), (_, __) => true, simulation.CanOperate);
            foreach (var participant in seed.Participants) shift.SetInputEnabled(participant.ParticipantId, true);
            var inputs = seed.Participants.Select((participant, index) => new ServerMovementInput
            {
                ParticipantId = participant.ParticipantId,
                Enabled = true,
                Velocity = new Point3(index % 2 == 0 ? 0.1f : -0.1f, 0, 0),
                LoadedRegions = world.Regions.Select(region => region.Id).ToArray()
            }).ToArray();

            for (var warmup = 0; warmup < WarmupTicks; warmup++)
            {
                Assert.That(simulation.TryAdvance(shift, inputs, out var report), Is.True, report.Failure);
                Assert.That(report.Tick, Is.EqualTo(warmup + 1));
            }
            Assert.That(simulation.Tick, Is.EqualTo(WarmupTicks), "Warm-up is simulation progress, not a measured sample.");

            var accumulator = new RuntimeMetricAccumulator(
                "server", 0, MetricUtcStart, simulation.Tick, 0, 0, batch: true, nullGraphics: true);
            var durations = new List<double>();
            var observedParticipants = new List<int>();
            var observedNpc = new List<int>();
            var observedActiveIncidents = new List<int>();

            for (var sample = 1; sample <= MeasuredSamples; sample++)
            {
                Assert.That(simulation.TryAdvance(shift, inputs, out var report), Is.True, report.Failure);
                var state = shift.ReadSimulation();
                var participantCount = state.Participants.Length;
                var npcCount = state.Entities.Count(entity => entity.Kind == EntityKind.Evacuee);
                var activeIncidentCount = state.Entities.Count(entity => entity.Kind == EntityKind.Incident && entity.Active);

                Assert.That(state.Tick, Is.EqualTo(WarmupTicks + sample));
                Assert.That(report.Tick, Is.EqualTo(state.Tick));
                AssertExactRoleRoster(state.Participants);
                Assert.That(report.NpcCount, Is.EqualTo(npcCount), "Tick report must match the realized evacuee entities.");
                Assert.That(report.ActiveIncidents, Is.EqualTo(activeIncidentCount), "Tick report must match realized active incident entities.");

                durations.Add(report.TotalMilliseconds);
                observedParticipants.Add(participantCount);
                observedNpc.Add(npcCount);
                observedActiveIncidents.Add(activeIncidentCount);
                accumulator.Record(
                    sample * FoundationWorldSimulation.TickSeconds,
                    report.TotalMilliseconds,
                    0,
                    0,
                    participantCount,
                    npcCount,
                    activeIncidentCount,
                    state.Tick,
                    state.Paused,
                    0);
            }

            var measuredDurationSeconds = MeasuredSamples * FoundationWorldSimulation.TickSeconds;
            var interval = accumulator.Close(measuredDurationSeconds, MetricUtcStart.AddSeconds(measuredDurationSeconds));
            var finalCheckpoint = shift.ExportCheckpoint();

            Assert.That(FoundationWorldSimulation.TickSeconds, Is.EqualTo(0.05), "20Hz is the simulation interval contract.");
            Assert.That(simulation.Tick, Is.EqualTo(WarmupTicks + MeasuredSamples));
            Assert.That(interval.TickCount, Is.EqualTo(MeasuredSamples), "Metric samples exclude warm-up calls.");
            Assert.That(interval.SimulationTickStart, Is.EqualTo(WarmupTicks));
            Assert.That(interval.SimulationTickEnd, Is.EqualTo(WarmupTicks + MeasuredSamples));
            Assert.That(interval.DurationSeconds, Is.EqualTo(measuredDurationSeconds).Within(1e-12));

            Assert.That(observedParticipants, Is.All.EqualTo(ExpectedParticipants));
            Assert.That(observedNpc, Is.All.EqualTo(ExpectedNpc));
            Assert.That(observedActiveIncidents, Is.All.EqualTo(ExpectedActiveIncidents));
            Assert.That(interval.ConnectedClientsMin, Is.EqualTo(observedParticipants.Min()));
            Assert.That(interval.ConnectedClientsMax, Is.EqualTo(observedParticipants.Max()));
            Assert.That(interval.ConnectedClientsMean, Is.EqualTo(observedParticipants.Average()).Within(1e-12));
            Assert.That(interval.NpcCountMin, Is.EqualTo(observedNpc.Min()));
            Assert.That(interval.NpcCountMax, Is.EqualTo(observedNpc.Max()));
            Assert.That(interval.NpcCountMean, Is.EqualTo(observedNpc.Average()).Within(1e-12));
            Assert.That(interval.ActiveIncidentsMin, Is.EqualTo(observedActiveIncidents.Min()));
            Assert.That(interval.ActiveIncidentsMax, Is.EqualTo(observedActiveIncidents.Max()));
            Assert.That(interval.ActiveIncidentsMean, Is.EqualTo(observedActiveIncidents.Average()).Within(1e-12));

            var exactDurations = durations.ToArray();
            Assert.That(exactDurations.All(value => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0), Is.True);
            var maximum = exactDurations.Max();
            var mean = exactDurations.Average();
            var p95 = exactDurations.OrderBy(value => value).ElementAt((int)Math.Ceiling(0.95 * exactDurations.Length) - 1);
            Assert.That(mean, Is.LessThanOrEqualTo(maximum));
            Assert.That(p95, Is.InRange(0, maximum));
            AssertHistogramAndPercentile(interval.TickMilliseconds, exactDurations, 0.95);

            var intervalJson = JsonUtility.ToJson(interval);
            var restoredInterval = JsonUtility.FromJson<RuntimeMetricInterval>(intervalJson);
            Assert.That(restoredInterval.Schema, Is.EqualTo(1));
            Assert.That(restoredInterval.Kind, Is.EqualTo("interval"));
            Assert.That(restoredInterval.TickCount, Is.EqualTo(MeasuredSamples));
            Assert.That(restoredInterval.SimulationTickStart, Is.EqualTo(WarmupTicks));
            Assert.That(restoredInterval.SimulationTickEnd, Is.EqualTo(finalCheckpoint.SimulationTick));
            Assert.That(restoredInterval.TickMilliseconds.Bounds, Is.EqualTo(interval.TickMilliseconds.Bounds));
            Assert.That(restoredInterval.TickMilliseconds.Counts, Is.EqualTo(interval.TickMilliseconds.Counts));

            var checkpointJson = JsonUtility.ToJson(finalCheckpoint);
            var restoredCheckpoint = JsonUtility.FromJson<WorldState>(checkpointJson);
            Assert.That(restoredCheckpoint.SchemaVersion, Is.EqualTo(3));
            Assert.That(restoredCheckpoint.SimulationTick, Is.EqualTo(restoredInterval.SimulationTickEnd));
            Assert.That(restoredCheckpoint.SimulationDefinitionHash, Is.EqualTo(simulation.DefinitionHash));
            Assert.That(restoredCheckpoint.SimulationCheckpoint, Is.EqualTo(finalCheckpoint.SimulationCheckpoint));
            Assert.That(restoredCheckpoint.Participants, Has.Length.EqualTo(restoredInterval.ConnectedClients));
            Assert.That(restoredCheckpoint.Entities.Count(entity => entity.Kind == EntityKind.Evacuee), Is.EqualTo(restoredInterval.NpcCount));
            Assert.That(restoredCheckpoint.Entities.Count(entity => entity.Kind == EntityKind.Incident && entity.Active),
                Is.EqualTo(restoredInterval.ActiveIncidents));
            Assert.DoesNotThrow(() => simulation.Restore(restoredCheckpoint), "Serialized checkpoint must restore at its published boundary.");
        }
    }
}
