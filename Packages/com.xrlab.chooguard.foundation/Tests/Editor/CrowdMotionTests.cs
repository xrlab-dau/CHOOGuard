using System;
using System.Diagnostics;
using System.Linq;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CrowdMotionTests
    {
        private static CrowdAgent Agent(string id, double x, double y = 0) => new CrowdAgent
        {
            Id = id, ContactSpaceId = "ground", RegionId = "region", FrameId = "world", SurfaceId = "floor",
            Position = new CrowdVector(x, y), Goal = new CrowdVector(10000, y)
        };
        private static CrowdDefinition Definition(params CrowdWall[] walls) => new CrowdDefinition
        { ProfileId = "synthetic-tests", Spaces = new[] { new CrowdSpace { Id = "ground" } }, Walls = walls };
        private static CrowdWall Wall(string id, double ax, double ay, double bx, double by) => new CrowdWall
        { Id = id, ContactSpaceId = "ground", A = new CrowdVector(ax, ay), B = new CrowdVector(bx, by) };
        private static CrowdAgent Driven(string id, double x, double y, double vx, double vy)
        { var a = Agent(id, x, y); a.IntentMode = CrowdIntentMode.DesiredVelocity; a.DesiredVelocity = new CrowdVector(vx, vy); return a; }
        private static void Step(CrowdMotionModel model, double seconds)
        { Assert.That(model.TryAdvance(seconds, null, out var report), Is.True, report?.Failure); }
        private static CrowdAgent Read(CrowdMotionModel model, string id) => model.ExportSnapshot().Agents.Single(a => a.Id == id);

        [Test]
        public void IsolatedWalkerHasConstantSpeedAndStopsAtItsGoalWithoutOvershoot()
        {
            var a = Agent("a", 0); a.Goal = new CrowdVector(1, 0);
            var model = new CrowdMotionModel(Definition(), new[] { a }, 17);
            Step(model, .5); Assert.That(Read(model, "a").Position.X, Is.EqualTo(.6).Within(1e-10));
            Step(model, 1); Assert.That(Read(model, "a").Position.X, Is.EqualTo(1).Within(1e-10));
            Assert.That(Read(model, "a").Velocity.Length, Is.Zero);
        }

        [Test]
        public void StoppedLeaderGapConvergesToTheContinuousExponentialSolution()
        {
            double Error(double step)
            {
                var definition = Definition(); definition.Parameters.MaxSubstepSeconds = step;
                definition.Parameters.PersonRepulsion = 0; definition.Parameters.TimeGapSeconds = 1;
                var model = new CrowdMotionModel(definition, new[] { Agent("follower", 0), Driven("leader", 1.1, 0, 0, 0) });
                Step(model, 1);
                return Math.Abs((1.1 - Read(model, "follower").Position.X - .3) - .8 * Math.Exp(-1));
            }
            var coarse = Error(.01); var medium = Error(.005); var fine = Error(.0025);
            Assert.That(fine, Is.LessThan(.0005));
            Assert.That(coarse / medium, Is.InRange(1.9, 2.1)); Assert.That(medium / fine, Is.InRange(1.9, 2.1));
        }

        [TestCase(.299, true)]
        [TestCase(.301, false)]
        public void HeadwayUsesEuclideanCenterSpacingInsideTheCombinedBodyWidth(double lateral, bool blocks)
        {
            var d = Definition(); d.Parameters.PersonRepulsion = 0;
            var model = new CrowdMotionModel(d, new[] { Agent("a", 0), Driven("b", .5, lateral, 0, 0) });
            Step(model, .001);
            var expected = blocks ? Math.Sqrt(.5 * .5 + lateral * lateral) - .3 : 1.2;
            Assert.That(Read(model, "a").Velocity.X, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void PersonAndWallRepulsionChangeWalkingDirectionAndOccludedPeopleDoNot()
        {
            var person = new CrowdMotionModel(Definition(), new[] { Agent("a", 0), Driven("b", .3, .25, 0, 0) });
            Step(person, .001); Assert.That(Read(person, "a").Velocity.Y, Is.LessThan(0));
            var wall = Wall("wall", -10, .17, 10, .17);
            var alone = new CrowdMotionModel(Definition(wall), new[] { Agent("a", 0) });
            var hidden = new CrowdMotionModel(Definition(wall), new[] { Agent("a", 0), Driven("b", .3, .5, 0, 0) });
            Step(alone, .001); Step(hidden, .001);
            Assert.That(Read(alone, "a").Velocity.Y, Is.LessThan(0));
            Assert.That(Read(hidden, "a").Velocity.X, Is.EqualTo(Read(alone, "a").Velocity.X).Within(1e-10));
            Assert.That(Read(hidden, "a").Velocity.Y, Is.EqualTo(Read(alone, "a").Velocity.Y).Within(1e-10));
        }

        [Test]
        public void OpposingPairProjectsToTheAnalyticClosestAdmissibleVelocity()
        {
            var d = Definition(); d.Parameters.MaxSubstepSeconds = .1;
            var model = new CrowdMotionModel(d, new[] { Driven("a", -.2, 0, 1, 0), Driven("b", .2, 0, -1, 0) });
            Assert.That(model.TryAdvance(.1, null, out var report), Is.True, report.Failure);
            Assert.That(Read(model, "a").Position.X, Is.EqualTo(-.15).Within(1e-9));
            Assert.That(Read(model, "a").Velocity.X, Is.EqualTo(.5).Within(1e-9));
            Assert.That(Read(model, "b").Velocity.X, Is.EqualTo(-.5).Within(1e-9));
            Assert.That(report.MaximumComplementarityResidual, Is.LessThanOrEqualTo(d.Parameters.ComplementarityTolerance));
            Assert.That(report.MaximumStationarityResidualMS, Is.LessThan(1e-10));
        }

        [Test]
        public void DifferentPlayerAndNpcRadiiKeepTheirCombinedFootprint()
        {
            var d = Definition(); d.Parameters.MaxSubstepSeconds = .1;
            var player = Driven("player", -.3, 0, 1, 0); player.RadiusM = .3;
            var model = new CrowdMotionModel(d, new[] { player, Driven("npc", .3, 0, -1, 0) });
            Step(model, .1);
            Assert.That(Read(model, "npc").Position.X - Read(model, "player").Position.X, Is.EqualTo(.45).Within(1e-9));
            Assert.That(Read(model, "player").RadiusM, Is.EqualTo(.3)); Assert.That(Read(model, "npc").RadiusM, Is.EqualTo(.15));
        }

        [Test]
        public void ConservativeContactMayDeflectWallSeparatedBodiesEvenWithClearTangentPaths()
        {
            var d = Definition(Wall("separating", 0, -2, 0, 2)); d.Parameters.MaxSubstepSeconds = .1;
            var model = new CrowdMotionModel(d, new[] { Driven("a", -.15, 0, 0, 1), Driven("b", .15, .1, 0, -1) });
            Assert.That(model.TryAdvance(.1, null, out var report), Is.True, report.Failure);
            // The unprojected paths have minimum centre spacing .3m: safe tangent motion.
            // The first-order separating halfspace is stricter, and can push bodies away from this wall.
            Assert.That(Read(model, "a").Velocity.X, Is.LessThan(-.1));
            Assert.That(Read(model, "b").Velocity.X, Is.GreaterThan(.1));
            Assert.That(report.MinimumSweptGapM, Is.GreaterThanOrEqualTo(-1e-9));
        }

        [Test]
        public void WallBlockedContactChainTransfersTheConstraintThroughEveryBody()
        {
            var model = new CrowdMotionModel(Definition(Wall("end", 0, -5, 0, 5)),
                new[] { Driven("a", -.15, 0, 1, 0), Driven("b", -.45, 0, 1, 0), Driven("c", -.75, 0, 1, 0) });
            Assert.That(model.TryAdvance(.05, null, out var report), Is.True, report.Failure);
            foreach (var a in model.ExportSnapshot().Agents) Assert.That(a.Velocity.Length, Is.LessThan(1e-7));
            Assert.That(report.MinimumSweptGapM, Is.GreaterThanOrEqualTo(-1e-9));
            Assert.That(report.MaximumDualMultiplierMS, Is.GreaterThan(1));
        }

        [Test]
        public void FastSweptStepCannotTunnelThroughThinWallOrOpposingPerson()
        {
            var d = Definition(Wall("thin", 0, -10, 0, 10)); d.Parameters.MaxSubstepSeconds = .5;
            var wall = new CrowdMotionModel(d, new[] { Driven("a", -1, 0, 10, 0) });
            Step(wall, .5); Assert.That(Read(wall, "a").Position.X, Is.LessThanOrEqualTo(-.15 + 1e-9));
            d.Walls = Array.Empty<CrowdWall>();
            var pair = new CrowdMotionModel(d, new[] { Driven("a", -1, 0, 10, 0), Driven("b", 1, 0, -10, 0) });
            Step(pair, .5); Assert.That(Read(pair, "a").Position.X, Is.LessThanOrEqualTo(-.15 + 1e-9));
        }

        [Test]
        public void TangentMotionAndFiniteWallEndpointRemainGeometricallyAdmissible()
        {
            var tangent = new CrowdMotionModel(Definition(Wall("wall", 0, -2, 0, 2)), new[] { Driven("a", -.15, -1, 0, 1) });
            Step(tangent, 1); Assert.That(Read(tangent, "a").Position.Y, Is.EqualTo(0).Within(1e-10));
            var endpoint = new CrowdMotionModel(Definition(Wall("short", 0, -.1, 0, .1)), new[] { Driven("a", -1, .26, 2, 0) });
            Step(endpoint, 1); Assert.That(Read(endpoint, "a").Position.X, Is.GreaterThan(.5));
            var corner = new CrowdMotionModel(Definition(Wall("x", 0, -3, 0, 3), Wall("y", -3, 0, 3, 0)),
                new[] { Driven("a", -.5, -.5, 1, 1) });
            Step(corner, 1); var p = Read(corner, "a").Position;
            Assert.That(p.X, Is.LessThanOrEqualTo(-.15 + 1e-9)); Assert.That(p.Y, Is.LessThanOrEqualTo(-.15 + 1e-9));
        }

        [TestCase(.299, false)]
        [TestCase(.301, true)]
        public void DoorWidthIsComparedWithTheSameDiameterUsedByContact(double width, bool passes)
        {
            var model = new CrowdMotionModel(Definition(Wall("lower", 0, -5, 0, -width / 2), Wall("upper", 0, width / 2, 0, 5)),
                new[] { Driven("a", -.8, 0, 1, 0) });
            Step(model, 2);
            Assert.That(Read(model, "a").Position.X > .3, Is.EqualTo(passes));
        }

        [Test]
        public void StableIdsMakeArrayOrderAndWallEndpointOrderIrrelevant()
        {
            var walls = new[] { Wall("right", 1, -2, 1, 2), Wall("bottom", -2, -1, 2, -1) };
            var agents = new[] { Driven("c", .7, 0, 1, 0), Driven("a", .3, .05, 1, 0), Driven("b", -.1, 0, 1, 0) };
            var a = new CrowdMotionModel(Definition(walls), agents, 999);
            var reversed = walls.Reverse().Select(w => new CrowdWall { Id = w.Id, ContactSpaceId = w.ContactSpaceId, A = w.B, B = w.A }).ToArray();
            var b = new CrowdMotionModel(Definition(reversed), agents.Reverse().ToArray(), 999);
            Step(a, 1); Step(b, 1);
            Assert.That(JsonUtility.ToJson(a.ExportSnapshot()), Is.EqualTo(JsonUtility.ToJson(b.ExportSnapshot())));
        }

        [Test]
        public void ContactSpaceSeparatesFloorsWhileRegionNamesDoNotSeparateBodies()
        {
            var a = Driven("a", -.2, 0, 1, 0); var b = Driven("b", .2, 0, -1, 0); b.RegionId = "adjacent-region";
            var together = new CrowdMotionModel(Definition(), new[] { a, b }); Step(together, .5);
            Assert.That(Read(together, "a").Position.X, Is.LessThan(0));
            var d = Definition(); d.Spaces = new[] { new CrowdSpace { Id = "ground" }, new CrowdSpace { Id = "upper" } };
            b.ContactSpaceId = "upper"; b.SurfaceId = "upper-floor";
            var apart = new CrowdMotionModel(d, new[] { a, b }); Step(apart, .5);
            Assert.That(Read(apart, "a").Position.X, Is.EqualTo(.3).Within(1e-10));
        }

        [Test]
        public void PeriodicHeadwayEquilibriumAndSeamContactsUseTheSameBodyDiameter()
        {
            var d = Definition(); d.Spaces[0].PeriodicLengthXM = 10;
            var agents = Enumerable.Range(0, 20).Select(i => Agent(i.ToString("D3"), i * .5)).ToArray();
            var model = new CrowdMotionModel(d, agents);
            Step(model, 1);
            foreach (var a in model.ExportSnapshot().Agents) Assert.That(a.Velocity.X, Is.EqualTo(.2).Within(1e-9));
            var seam = new CrowdMotionModel(d, new[] { Driven("a", .2, 0, -1, 0), Driven("b", 9.8, 0, 1, 0) });
            Step(seam, .2); Assert.That(Read(seam, "a").Position.X, Is.GreaterThanOrEqualTo(.15 - 1e-9));
            Assert.That(Read(seam, "b").Position.X, Is.LessThanOrEqualTo(9.85 + 1e-9));
        }

        [Test]
        public void PeriodicRepulsionIsTranslationEquivariantIncludingHalfPeriodTies()
        {
            var d = Definition(); d.Spaces[0].PeriodicLengthXM = 1; d.Parameters.PersonRepulsionRangeM = 1;
            foreach (var shift in new[] { 0.0, .27, .67 })
            {
                var a = Agent("a", (.1 + shift) % 1); var b = Agent("b", (.6 + shift) % 1);
                var model = new CrowdMotionModel(d, new[] { a, b }); Step(model, .001);
                Assert.That(Read(model, "a").Velocity.X, Is.EqualTo(.2).Within(1e-9));
                Assert.That(Read(model, "b").Velocity.X, Is.EqualTo(.2).Within(1e-9));
            }
        }

        [Test]
        public void SnapshotIsDeepCopiedAndRestoresTheExactContinuationAndSeed()
        {
            var d = Definition(); var a = Agent("a", 0); a.KnownClosedPortalIds = new[] { "closed" }; a.LeaderId = "trainer"; a.PathCursor = 3;
            var model = new CrowdMotionModel(d, new[] { a }, 1234); d.Parameters.TimeGapSeconds = 900; a.Position.X = 999;
            Step(model, .1); var saved = model.ExportSnapshot(); var restored = CrowdMotionModel.FromCheckpoint(model.ExportCheckpoint());
            saved.Agents[0].KnownClosedPortalIds[0] = "mutated"; saved.Definition.Parameters.TimeGapSeconds = 200;
            Step(model, .5); Step(restored, .5);
            Assert.That(JsonUtility.ToJson(model.ExportSnapshot()), Is.EqualTo(JsonUtility.ToJson(restored.ExportSnapshot())));
            Assert.That(Read(model, "a").KnownClosedPortalIds.Single(), Is.EqualTo("closed"));
            Assert.That(model.ExportSnapshot().Seed, Is.EqualTo(1234)); Assert.That(Read(model, "a").PathCursor, Is.EqualTo(3));
        }

        [Test]
        public void InvalidRestoreAndProfileOrRosterChangesCannotPartiallyReplaceState()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Agent("a", 0), Agent("b", 1) });
            var before = JsonUtility.ToJson(model.ExportSnapshot());
            foreach (var mutation in new Action<CrowdSnapshot>[] {
                s => s.SchemaVersion = 99, s => s.Agents[0].Position.X = double.NaN,
                s => s.Agents[1].Position = s.Agents[0].Position, s => s.Definition.Parameters.TimeGapSeconds = 2,
                s => s.Agents[0].RadiusM = .3, s => s.Agents[0].Id = "new", s => s.ElapsedSeconds = -1 })
            {
                var bad = model.ExportSnapshot(); mutation(bad);
                Assert.That(model.TryRestore(bad, out _), Is.False);
                Assert.That(JsonUtility.ToJson(model.ExportSnapshot()), Is.EqualTo(before));
            }
        }

        [Test]
        public void StepBudgetAndProjectionFailureRollBackTimeIntentAndAllAgents()
        {
            var d = Definition(Wall("end", 0, -5, 0, 5)); d.Parameters.MaxProjectionIterations = 1; d.Parameters.MaxActiveSetConstraints = 0;
            var model = new CrowdMotionModel(d, new[] { Driven("a", -.15, 0, 1, 0), Driven("b", -.45, 0, 1, 0), Driven("c", -.75, 0, 1, 0) });
            var before = JsonUtility.ToJson(model.ExportSnapshot());
            Assert.That(model.TryAdvance(.05, null, out var report), Is.False); Assert.That(report.Failure, Does.Contain("projection"));
            Assert.That(JsonUtility.ToJson(model.ExportSnapshot()), Is.EqualTo(before));
            d = Definition(); d.Parameters.MaxSubstepsPerTick = 1; model = new CrowdMotionModel(d, new[] { Agent("a", 0) });
            before = JsonUtility.ToJson(model.ExportSnapshot());
            Assert.That(model.TryAdvance(.05, new[] { new CrowdIntent { AgentId = "a", Mode = CrowdIntentMode.DesiredVelocity, DesiredVelocity = new CrowdVector(2, 0) } }, out _), Is.False);
            Assert.That(JsonUtility.ToJson(model.ExportSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void InitialPenetrationDuplicateIdsAndNonfiniteInputsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(), new[] { Agent("a", 0), Agent("b", .2) }));
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(), new[] { Agent("a", 0), Agent("a", 2) }));
            Assert.Throws<ArgumentException>(() => new CrowdMotionModel(Definition(Wall("w", 0, -2, 0, 2)), new[] { Agent("a", .1) }));
            var model = new CrowdMotionModel(Definition(), new[] { Agent("a", 0) });
            Assert.That(model.TryAdvance(double.NaN, null, out _), Is.False);
            Assert.That(model.TryAdvance(.1, new[] { new CrowdIntent { AgentId = "missing" } }, out _), Is.False);
        }

        [TestCase("dispersion", 1.0)]
        [TestCase("dense", .31)]
        [TestCase("counterflow", .6)]
        public void OneHundredAgentsKeepSweptGapsAndResidualsWithinDeclaredNumericalTolerance(string kind, double spacing)
        {
            var agents = Enumerable.Range(0, 100).Select(i => {
                var x = i % 10 * spacing; var y = i / 10 * spacing; var a = Agent(i.ToString("D3"), x, y);
                a.Goal = kind == "dispersion" ? new CrowdVector((x - 4.5) * 100, (y - 4.5) * 100) :
                    new CrowdVector(kind == "counterflow" && i / 10 % 2 != 0 ? -100 : 100, y + .02 * Math.Sin(i));
                return a;
            }).ToArray();
            var model = new CrowdMotionModel(Definition(), agents, 821);
            var timer = Stopwatch.StartNew(); double minGap = double.PositiveInfinity, maxResidual = 0;
            for (var tick = 0; tick < 20; tick++)
            {
                Assert.That(model.TryAdvance(.05, null, out var report), Is.True, report.Failure);
                minGap = Math.Min(minGap, report.MinimumSweptGapM); maxResidual = Math.Max(maxResidual, report.MaximumConstraintViolationMS);
            }
            timer.Stop(); Assert.That(model.ExportSnapshot().Agents.Length, Is.EqualTo(100));
            Assert.That(minGap, Is.GreaterThanOrEqualTo(-1e-9)); Assert.That(maxResidual, Is.LessThanOrEqualTo(1e-8));
            TestContext.Out.WriteLine($"crowd100 kind={kind} simulated=1s elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} min_swept_gap_m={minGap:R} max_constraint_ms={maxResidual:R}");
        }

        [Test]
        public void OneHundredBodiesPressedAgainstAWallSolveActualContactAndRestoreDuringCompression()
        {
            var d = Definition(Wall("end", 3, -1, 3, 10)); d.Parameters.MaxSubstepSeconds = .05;
            var agents = Enumerable.Range(0, 100).Select(i => Driven(i.ToString("D3"), .15 + i % 10 * .3, i / 10 * .4, 1, 0)).ToArray();
            var model = new CrowdMotionModel(d, agents, 731); var timer = Stopwatch.StartNew();
            Assert.That(model.TryAdvance(.05, null, out var report), Is.True, report.Failure); timer.Stop();
            Assert.That(report.MaximumDualMultiplierMS, Is.GreaterThan(9.9));
            foreach (var body in model.ExportSnapshot().Agents) Assert.That(body.Velocity.Length, Is.LessThan(1e-7));
            var restored = CrowdMotionModel.FromCheckpoint(model.ExportCheckpoint());
            Step(model, .05); Step(restored, .05);
            Assert.That(JsonUtility.ToJson(model.ExportSnapshot()), Is.EqualTo(JsonUtility.ToJson(restored.ExportSnapshot())));
            TestContext.Out.WriteLine($"crowd100 wall_contact elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} iterations={report.MaximumProjectionIterations} min_swept_gap_m={report.MinimumSweptGapM:R}");
        }

        [Test]
        public void OneHundredSingleFileBodiesAtAWallConvergeUnderTheDefaultBudget()
        {
            var d = Definition(Wall("end", 0, -2, 0, 2));
            var agents = Enumerable.Range(0, 100).Select(i => Driven(i.ToString("D3"), -(.15 + .3 * i), 0, 1, 0)).ToArray();
            var model = new CrowdMotionModel(d, agents); var timer = Stopwatch.StartNew();
            Assert.That(model.TryAdvance(.05, null, out var report), Is.True, report.Failure); timer.Stop();
            foreach (var body in model.ExportSnapshot().Agents) Assert.That(body.Velocity.Length, Is.LessThan(1e-7));
            Assert.That(report.MaximumDualMultiplierMS, Is.GreaterThan(99.9));
            Assert.That(report.MinimumSweptGapM, Is.GreaterThanOrEqualTo(-1e-9));
            TestContext.Out.WriteLine($"crowd100 single_chain elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} iterations={report.MaximumProjectionIterations}");
        }

        [Test]
        public void DependentActiveWallRowsFallBackToTheIterativeProjection()
        {
            var model = new CrowdMotionModel(Definition(Wall("left", -.15, -2, -.15, 2), Wall("right", .15, -2, .15, 2)),
                new[] { Driven("a", 0, 0, 0, 1) });
            Assert.That(model.TryAdvance(.05, null, out var report), Is.True, report.Failure);
            Assert.That(Read(model, "a").Velocity.X, Is.EqualTo(0).Within(1e-9));
            Assert.That(Read(model, "a").Velocity.Y, Is.EqualTo(1).Within(1e-9));
            Assert.That(report.ActiveSetSolves, Is.Zero);
        }

        [Test]
        public void ExactCheckpointRejectsCorruptionTrailingBytesAndForeignVersionsAtomically()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Agent("a", .15000000003791787) }, 123);
            var encoded = model.ExportCheckpoint();
            Assert.That(CrowdMotionModel.FromCheckpoint(encoded).ExportCheckpoint(), Is.EqualTo(encoded));
            var flipped = Convert.FromBase64String(encoded.Substring(5)); flipped[40] ^= 1;
            foreach (var corrupted in new[] { "CGC9:" + encoded.Substring(5), encoded.Substring(0, encoded.Length - 4), encoded.Substring(0, 5) + Convert.ToBase64String(flipped),
                encoded.Substring(0, 5) + Convert.ToBase64String(Convert.FromBase64String(encoded.Substring(5)).Concat(new byte[] { 1 }).ToArray()) })
            {
                Assert.That(model.TryRestoreCheckpoint(corrupted, out _), Is.False);
                Assert.That(model.ExportCheckpoint(), Is.EqualTo(encoded));
            }
        }
    }
}
