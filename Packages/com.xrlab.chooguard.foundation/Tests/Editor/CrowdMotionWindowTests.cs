using System;
using System.Linq;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CrowdMotionWindowTests
    {
        private static CrowdDefinition Definition() => new CrowdDefinition { ProfileId = "hinge-fixture", Spaces = new[] { new CrowdSpace { Id = "floor" } } };
        private static CrowdAgent Agent(string id, double x) => new CrowdAgent { Id = id, ContactSpaceId = "floor", FrameId = "world",
            RegionId = "room", SurfaceId = "flat", Position = new CrowdVector(x, 0), IntentMode = CrowdIntentMode.DesiredVelocity, DesiredVelocity = new CrowdVector(2, 0) };
        private static CrowdMovementWindow Window(string id, double maximum) => new CrowdMovementWindow { AgentId = id, MinX = -10, MaxX = maximum, MinY = -2, MaxY = 2 };

        [Test]
        public void SupportBoundaryParticipatesInTheSameContactProjectionAsAPushingPair()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Agent("rear", 0), Agent("front", .3) });
            Assert.That(model.TryAdvance(.5, null, out var report, new[] { Window("front", .5) }), Is.True, report.Failure);
            var state = model.ExportSnapshot(); var front = state.Agents.Single(a => a.Id == "front"); var rear = state.Agents.Single(a => a.Id == "rear");
            Assert.That(front.Position.X, Is.EqualTo(.5).Within(1e-8));
            Assert.That(front.Position.X - rear.Position.X, Is.GreaterThanOrEqualTo(.3 - 1e-9));
        }

        [Test]
        public void ReachingAHingeThenRebindingPreservesTheCorrectPiecewiseElevation()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Agent("a", 0) });
            Assert.That(model.TryAdvance(1, null, out var report, new[] { Window("a", 1) }), Is.True, report.Failure);
            var state = model.ExportSnapshot(); Assert.That(state.Agents[0].Position.X, Is.EqualTo(1).Within(1e-9));
            Assert.That(state.Agents[0].FootElevationM, Is.Zero);
            var body = CrowdBodyBinding.FromAgent(state.Agents[0]); body.SurfaceId = "ramp"; body.SupportGradient = new CrowdVector(.5, 0);
            Assert.That(model.TryRebind(new CrowdRebind { ExpectedTick = state.Tick, ExpectedGeometryRevision = state.GeometryRevision,
                Spaces = state.Definition.Spaces, Walls = state.Definition.Walls, Bodies = new[] { body } }, out _), Is.True);
            var window = Window("a", 5); window.MinX = 1;
            Assert.That(model.TryAdvance(.1, null, out report, new[] { window }), Is.True, report.Failure);
            var after = model.ExportSnapshot().Agents[0];
            Assert.That(after.FootElevationM, Is.EqualTo((after.Position.X - 1) * .5).Within(1e-10));
        }

        [Test]
        public void InvalidWindowOrAnInitiallyOutsideBodyLeavesTheCompleteCheckpointUnchanged()
        {
            var model = new CrowdMotionModel(Definition(), new[] { Agent("a", 0) }); var before = model.ExportCheckpoint();
            foreach (var windows in new[] { new[] { Window("missing", 1) }, new[] { Window("a", -1) }, new[] { Window("a", 1), Window("a", 1) } })
            { Assert.That(model.TryAdvance(.05, null, out _, windows), Is.False); Assert.That(model.ExportCheckpoint(), Is.EqualTo(before)); }
        }
    }
}
