using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CrowdWorldContactTests
    {
        private static CrowdDefinition Definition(params CrowdWall[] walls) => new CrowdDefinition { ProfileId = "world-contact-fixture",
            Spaces = new[] { new CrowdSpace { Id = "station" } }, Walls = walls };
        private static CrowdAgent Body(string id, double x, double elevation = 0, double y = 0) => new CrowdAgent {
            Id = id, ContactSpaceId = "station", RegionId = "room", FrameId = "world", SurfaceId = "floor",
            Position = new CrowdVector(x, y), FootElevationM = elevation, Goal = new CrowdVector(1000, y) };
        private static CrowdAgent Driven(string id, double x, double elevation, double vx, double y = 0)
        { var body = Body(id, x, elevation, y); body.IntentMode = CrowdIntentMode.DesiredVelocity; body.DesiredVelocity = new CrowdVector(vx, 0); return body; }
        private static CrowdWall Wall(double elevation = 0) => new CrowdWall { Id = "wall", ContactSpaceId = "station",
            A = new CrowdVector(.5, -5), B = new CrowdVector(.5, 5), HasVerticalBounds = true, BottomElevationM = elevation, TopElevationM = elevation + 2.5 };
        private static CrowdAgent Read(CrowdMotionModel model, string id) => model.ExportSnapshot().Agents.Single(a => a.Id == id);
        private static void Step(CrowdMotionModel model, double seconds)
        { Assert.That(model.TryAdvance(seconds, null, out var report), Is.True, report.Failure); }
        private static CrowdRebind Rebind(CrowdMotionModel model)
        {
            var snapshot = model.ExportSnapshot();
            return new CrowdRebind { ExpectedTick = snapshot.Tick, ExpectedGeometryRevision = snapshot.GeometryRevision,
                Spaces = snapshot.Definition.Spaces, Walls = snapshot.Definition.Walls,
                Bodies = snapshot.Agents.Select(CrowdBodyBinding.FromAgent).ToArray() };
        }

        [Test]
        public void PinnedPlayerIsAnExactConstraintAndCannotBePushedByATouchingNpc()
        {
            var player = Body("player", 0); player.RadiusM = .3; player.Pinned = true;
            var model = new CrowdMotionModel(Definition(), new[] { player, Driven("npc", -.45, 0, 2) });
            Step(model, .05);
            Assert.That(Read(model, "player").Position.X, Is.Zero); Assert.That(Read(model, "player").Velocity.Length, Is.Zero);
            Assert.That(Read(model, "npc").Position.X, Is.EqualTo(-.45).Within(1e-9));
        }

        [Test]
        public void SameHorizontalCoordinatesOnSeparateFloorsAreValidAndDoNotInteract()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Driven("lower", 0, 0, 1), Driven("upper", 0, 3, -1) });
            Step(model, .5);
            Assert.That(Read(model, "lower").Position.X, Is.EqualTo(.5).Within(1e-10));
            Assert.That(Read(model, "upper").Position.X, Is.EqualTo(-.5).Within(1e-10));
            Assert.That(CrowdMotionModel.FromCheckpoint(model.ExportCheckpoint()).ExportCheckpoint(), Is.EqualTo(model.ExportCheckpoint()));
        }

        [Test]
        public void RampAndLandingBodiesContactDespiteDifferentSurfaceIds()
        {
            var d = Definition(); d.Parameters.MaxSubstepSeconds = .1;
            var ramp = Driven("ramp", -.2, -.05, 1); ramp.SurfaceId = "ramp"; ramp.SupportGradient = new CrowdVector(.25, 0);
            var landing = Driven("landing", .2, 0, -1); landing.SurfaceId = "landing";
            var model = new CrowdMotionModel(d, new[] { ramp, landing }); Step(model, .1);
            Assert.That(Read(model, "landing").Position.X - Read(model, "ramp").Position.X, Is.GreaterThanOrEqualTo(.3 - 1e-9));
            Assert.That(Read(model, "ramp").Velocity.X, Is.LessThan(.8));
        }

        [Test]
        public void WallsOnAnotherFloorNeitherBlockSteerNorOccludePeople()
        {
            var a = Body("a", 0); var b = Driven("b", .3, 0, 0, .25);
            var wall = Wall(3); wall.A.X = .15; wall.B.X = .15;
            var clear = new CrowdMotionModel(Definition(), new[] { a, b });
            var upstairs = new CrowdMotionModel(Definition(wall), new[] { a, b });
            Step(clear, .001); Step(upstairs, .001);
            Assert.That(Read(upstairs, "a").Velocity.X, Is.EqualTo(Read(clear, "a").Velocity.X));
            Assert.That(Read(upstairs, "a").Velocity.Y, Is.EqualTo(Read(clear, "a").Velocity.Y));
            var lower = new CrowdMotionModel(Definition(Wall(3)), new[] { Driven("low", 0, 0, 2), Driven("high", 0, 3, 2) });
            Step(lower, .5);
            Assert.That(Read(lower, "low").Position.X, Is.EqualTo(1).Within(1e-10));
            Assert.That(Read(lower, "high").Position.X, Is.LessThanOrEqualTo(.35 + 1e-9));
        }

        [Test]
        public void SupportSlopePreservesGroundWishSpeedAndUpdatesExactElevationWithoutChangingRadius()
        {
            var body = Body("a", 0, -4); body.SupportGradient = new CrowdVector(.5, 0);
            var model = new CrowdMotionModel(Definition(), new[] { body }); Step(model, .5);
            var after = Read(model, "a");
            Assert.That(after.Position.X, Is.EqualTo(.6 / Math.Sqrt(1.25)).Within(1e-10));
            Assert.That(after.FootElevationM, Is.EqualTo(-4 + after.Position.X * .5).Within(1e-10));
            Assert.That(after.RadiusM, Is.EqualTo(.15));
            var restored = CrowdMotionModel.FromCheckpoint(model.ExportCheckpoint()); Step(model, .05); Step(restored, .05);
            Assert.That(restored.ExportCheckpoint(), Is.EqualTo(model.ExportCheckpoint()));
            body = Body("near", 0); body.SupportGradient = new CrowdVector(.5, 0); body.Goal = new CrowdVector(.01, 0);
            model = new CrowdMotionModel(Definition(), new[] { body }); Step(model, .05);
            Assert.That(Read(model, "near").Position.X, Is.EqualTo(.01).Within(1e-12));
            Assert.That(Read(model, "near").FootElevationM, Is.EqualTo(.005).Within(1e-12));
        }

        [Test]
        public void FullSweptVerticalEligibilityStopsAnApproachThatWouldMissAtTheEndpoint()
        {
            var d = Definition(); d.Parameters.MaxSubstepSeconds = 1;
            var ramp = Driven("ramp", 0, 0, 10); ramp.HeightM = .3; ramp.SupportGradient = new CrowdVector(1, 0);
            var upper = Body("upper", 2, 2); upper.HeightM = .3; upper.Pinned = true;
            var model = new CrowdMotionModel(d, new[] { ramp, upper });
            Assert.That(model.TryAdvance(1, null, out var report), Is.True, report.Failure);
            Assert.That(Read(model, "ramp").Position.X, Is.LessThanOrEqualTo(1.7 + 1e-9));
            Assert.That(report.MinimumSweptGapM, Is.GreaterThanOrEqualTo(-1e-9));
        }

        [Test]
        public void LaterUnsafeVerticalApproachRollsBackTheEntireRequestedTick()
        {
            var rising = Driven("rising", 0, 0, 2); rising.SupportGradient = new CrowdVector(.5, 0);
            var overhead = Body("overhead", 0, 1.84); overhead.Pinned = true;
            var model = new CrowdMotionModel(Definition(), new[] { rising, overhead }); var before = model.ExportCheckpoint();
            Assert.That(model.TryAdvance(.05, null, out var report), Is.False);
            Assert.That(report.AttemptedSubsteps, Is.GreaterThan(1)); Assert.That(report.Failure, Does.Contain("vertical reach"));
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
        }

        [Test]
        public void FullSweptWallHeightWindowIsCheckedEvenWhenTheUnconstrainedEndpointIsAboveIt()
        {
            var wall = Wall(2); wall.A.X = wall.B.X = 2; wall.TopElevationM = 2.3;
            var d = Definition(wall); d.Parameters.MaxSubstepSeconds = 1;
            var ramp = Driven("ramp", 0, 0, 10); ramp.HeightM = .3; ramp.SupportGradient = new CrowdVector(1, 0);
            var model = new CrowdMotionModel(d, new[] { ramp }); Step(model, 1);
            Assert.That(Read(model, "ramp").Position.X, Is.LessThanOrEqualTo(1.85 + 1e-9));
        }

        [Test]
        public void RebindPinsWithoutChangingClockSeedBodyProfileOrIntent()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Driven("a", 0, 0, 1) }, 710); Step(model, .05);
            var before = model.ExportSnapshot(); var request = Rebind(model); request.Bodies[0].Pinned = true;
            Assert.That(model.TryRebind(request, out var failure), Is.True, failure);
            var after = model.ExportSnapshot();
            Assert.That(after.Tick, Is.EqualTo(before.Tick)); Assert.That(after.AcceptedSubsteps, Is.EqualTo(before.AcceptedSubsteps));
            Assert.That(after.ElapsedSeconds, Is.EqualTo(before.ElapsedSeconds)); Assert.That(after.Seed, Is.EqualTo(before.Seed));
            Assert.That(after.GeometryRevision, Is.EqualTo(before.GeometryRevision + 1));
            Assert.That(Read(model, "a").Velocity.Length, Is.Zero); Assert.That(Read(model, "a").DesiredVelocity.X, Is.EqualTo(1));
            Step(model, .05); Assert.That(Read(model, "a").Position.X, Is.EqualTo(before.Agents[0].Position.X));
            Assert.That(model.TryRebind(request, out _), Is.False, "The prior tick/revision is stale.");
        }

        [Test]
        public void OrdinaryRestoreCannotChangeTheSupportBindingWithoutANewGeometryEpoch()
        {
            var body = Driven("a", 0, 2, 1); body.Pinned = true;
            var model = new CrowdMotionModel(Definition(), new[] { body }); var before = model.ExportCheckpoint();
            var altered = model.ExportSnapshot(); altered.Agents[0].Pinned = false;
            Assert.That(model.TryRestore(altered, out _), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
            altered = model.ExportSnapshot(); altered.Agents[0].SupportGradient = new CrowdVector(.2, 0);
            Assert.That(model.TryRestore(altered, out _), Is.False);
            altered = model.ExportSnapshot(); altered.Agents[0].SurfaceId = "different-floor";
            Assert.That(model.TryRestore(altered, out _), Is.False);
            altered = model.ExportSnapshot(); altered.Agents[0].FootElevationM += 1;
            Assert.That(model.TryRestore(altered, out _), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
            var movable = Driven("b", 0, 2, 1); movable.SupportGradient = new CrowdVector(.2, 0);
            model = new CrowdMotionModel(Definition(), new[] { movable }); var earlier = model.ExportSnapshot(); Step(model, .05);
            Assert.That(model.TryRestore(earlier, out _), Is.True, "A previous pose on the identical support plane can be restored.");
        }

        [Test]
        public void ClosingDoorOrRebindingAnUpperBodyIntoOverlapIsAtomic()
        {
            var wall = Wall(); wall.A.X = wall.B.X = 0; wall.Enabled = false;
            var model = new CrowdMotionModel(Definition(wall), new[] { Body("a", 0), Body("b", 0, 3) });
            var before = model.ExportCheckpoint(); var request = Rebind(model); request.Walls[0].Enabled = true;
            Assert.That(model.TryRebind(request, out _), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
            request = Rebind(model); request.Bodies.Single(a => a.AgentId == "b").FootElevationM = 1;
            Assert.That(model.TryRebind(request, out _), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
            request = Rebind(model); request.Bodies[1].AgentId = request.Bodies[0].AgentId;
            Assert.That(model.TryRebind(request, out _), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
            request = Rebind(model); request.Bodies[0].Pinned = true; request.Bodies[0].Velocity.X = double.NaN;
            Assert.That(model.TryRebind(request, out _), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before));
        }

        [Test]
        public void ValidDoorRebindChangesMotionAndSurvivesExactCheckpoint()
        {
            var model = new CrowdMotionModel(Definition(Wall()), new[] { Driven("a", 0, 0, 1) }); Step(model, .5);
            Assert.That(Read(model, "a").Position.X, Is.EqualTo(.35).Within(1e-9));
            var request = Rebind(model); request.Walls[0].Enabled = false;
            Assert.That(model.TryRebind(request, out var failure), Is.True, failure); Step(model, .5);
            Assert.That(Read(model, "a").Position.X, Is.EqualTo(.85).Within(1e-9));
            request = Rebind(model); request.Walls[0].Enabled = true;
            Assert.That(model.TryRebind(request, out failure), Is.True, failure);
            var restored = CrowdMotionModel.FromCheckpoint(model.ExportCheckpoint());
            Assert.That(restored.TryAdvance(.5, new[] { new CrowdIntent { AgentId = "a", Mode = CrowdIntentMode.DesiredVelocity,
                DesiredVelocity = new CrowdVector(-1, 0) } }, out var report), Is.True, report.Failure);
            Assert.That(Read(restored, "a").Position.X, Is.EqualTo(.65).Within(1e-9));
            Assert.That(restored.ExportSnapshot().GeometryRevision, Is.EqualTo(2));
        }

        [Test]
        public void ExplicitFrameRebindCanChangeCoordinatesAndWallsButOrdinaryRestoreCannot()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Driven("a", 300, -8, 1, 15) }, 20); Step(model, .05);
            var before = model.ExportSnapshot(); var request = Rebind(model);
            request.Spaces = new[] { new CrowdSpace { Id = "train-local" } };
            var b = request.Bodies.Single(); b.ContactSpaceId = "train-local"; b.RegionId = "vehicle"; b.FrameId = "train"; b.SurfaceId = "car-floor";
            b.Position = new CrowdVector(-(before.Agents[0].Position.X - 300), 0); b.FootElevationM = 0;
            b.Velocity = new CrowdVector(-1, 0); b.DesiredVelocity = new CrowdVector(-1, 0); b.Goal = new CrowdVector(-10, 0);
            Assert.That(model.TryRebind(request, out var failure), Is.True, failure);
            var rebound = model.ExportSnapshot(); Assert.That(rebound.Tick, Is.EqualTo(before.Tick)); Assert.That(rebound.Seed, Is.EqualTo(before.Seed));
            Assert.That(rebound.Agents[0].RadiusM, Is.EqualTo(before.Agents[0].RadiusM));
            Assert.That(model.TryRestore(before, out _), Is.False);
            var recovered = CrowdMotionModel.FromCheckpoint(model.ExportCheckpoint()); Step(recovered, .05); Step(model, .05);
            Assert.That(recovered.ExportCheckpoint(), Is.EqualTo(model.ExportCheckpoint()));
        }

        [Test]
        public void LegacyCgc1DefaultsMigrateAndContinueWithIdenticalNumericalBits()
        {
            var model = CrowdMotionModel.FromCheckpoint(CrowdLegacyFixture.Before);
            Assert.That(model.ExportCheckpoint(), Does.StartWith("CGC2:"));
            foreach (var body in model.ExportSnapshot().Agents)
            { Assert.That(body.FootElevationM, Is.Zero); Assert.That(body.HeightM, Is.EqualTo(1.8)); Assert.That(body.Pinned, Is.False); Assert.That(body.SupportGradient.Length, Is.Zero); }
            Step(model, .05);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(CrowdMotionModel.FromCheckpoint(CrowdLegacyFixture.After).ExportCheckpoint()));
        }

        [Test]
        public void InvalidVerticalContractsAndOverlappingCheckpointBodiesAreRejected()
        {
            var invalid = Body("a", 0); invalid.HeightM = .1;
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(), new[] { invalid }));
            invalid = Body("a", 0); invalid.FootElevationM = double.NaN;
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(), new[] { invalid }));
            invalid = Body("a", 0); invalid.SupportGradient = new CrowdVector(2, 0);
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(), new[] { invalid }));
            var wall = Wall(); wall.TopElevationM = wall.BottomElevationM;
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(wall), new[] { Body("a", 0) }));
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(), new[] { Body("a", 0), Body("b", 0, 1) }));
        }

        [Serializable] private sealed class Timing { public int Npcs, Players, PinnedPlayers, Ticks; public double SimulatedSeconds, TotalMs, MaxTickMs, MaxResidualMS, MinimumSweptGapM; }
        [TestCase(true)]
        [TestCase(false)]
        public void OneHundredNpcsAndTwentyPlayersResolveAcrossStackedFloorsWithTiming(bool pinnedPlayers)
        {
            var bodies = new List<CrowdAgent>(); var players = 0; var npcs = 0;
            for (var lane = 0; lane < 12; lane++)
            {
                var x = 0.0; var previousRadius = 0.0;
                for (var column = 0; column < 10; column++)
                {
                    var player = column < (lane < 8 ? 2 : 1); var radius = player ? .3 : .15;
                    if (column > 0) x -= previousRadius + radius;
                    var body = Driven(player ? "player" + (players++).ToString("D3") : "npc" + (npcs++).ToString("D3"), x, lane < 6 ? 0 : 3, player ? 3 : 1, lane % 6 * .8);
                    body.RadiusM = radius; body.Pinned = player && pinnedPlayers; body.SurfaceId = lane < 6 ? "lower" : "upper"; bodies.Add(body); previousRadius = radius;
                }
            }
            var definition = Definition(); definition.Parameters.MaxAgents = 120;
            var starts = bodies.ToDictionary(a => a.Id, a => a.Position.X);
            var model = new CrowdMotionModel(definition, bodies.ToArray(), 120); var timing = new Timing { Npcs = npcs, Players = players, PinnedPlayers = pinnedPlayers ? 20 : 0, Ticks = 20, SimulatedSeconds = 1, MinimumSweptGapM = double.PositiveInfinity };
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < timing.Ticks; i++)
            {
                var tick = Stopwatch.StartNew(); Assert.That(model.TryAdvance(.05, null, out var report), Is.True, report.Failure); tick.Stop();
                timing.MaxTickMs = Math.Max(timing.MaxTickMs, tick.Elapsed.TotalMilliseconds); timing.MaxResidualMS = Math.Max(timing.MaxResidualMS, report.MaximumConstraintViolationMS);
                timing.MinimumSweptGapM = Math.Min(timing.MinimumSweptGapM, report.MinimumSweptGapM);
            }
            timer.Stop(); timing.TotalMs = timer.Elapsed.TotalMilliseconds;
            Assert.That(npcs, Is.EqualTo(100)); Assert.That(players, Is.EqualTo(20));
            foreach (var body in model.ExportSnapshot().Agents)
            {
                var speed = pinnedPlayers ? 0 : body.Id.StartsWith("player", StringComparison.Ordinal) ? 3 : 1;
                Assert.That(body.Velocity.Length, Is.EqualTo(speed).Within(1e-7));
                Assert.That(body.Position.X, Is.EqualTo(starts[body.Id] + speed).Within(1e-7));
                if (body.Pinned) Assert.That(body.Velocity.Length, Is.Zero);
            }
            var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp/ChooGuardCrowdWorldContact"); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, pinnedPlayers ? "120-body-pinned-timing.json" : "120-body-moving-timing.json"), JsonUtility.ToJson(timing, true));
        }
    }
}
