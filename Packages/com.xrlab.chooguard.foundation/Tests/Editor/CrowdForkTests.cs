using System.Linq;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CrowdForkTests
    {
        private CrowdMotionModel Model() => new CrowdMotionModel(new CrowdDefinition { ProfileId = "fork-fixture",
            Spaces = new[] { new CrowdSpace { Id = "world" } }, Walls = new[] { new CrowdWall { Id = "wall", ContactSpaceId = "world", A = new CrowdVector(-2, -2), B = new CrowdVector(-2, 2) } } },
            new[] { new CrowdAgent { Id = "a", ContactSpaceId = "world", RegionId = "room", FrameId = "world", SurfaceId = "floor", RadiusM = .3 } }, 29);
        [Test] public void ForkAndBodyReadHaveNoMutableAliasesAndFailedAdvancePreservesBothBranches()
        {
            var model = Model(); var original = model.ExportCheckpoint(); var fork = model.Fork();
            var exposed = fork.ExportSnapshot(); exposed.Definition.Walls[0].A.X = 0; exposed.Definition.Parameters.PersonRepulsion = 0;
            var bodies = fork.ReadState(); bodies.Agents[0].Position.X = 99;
            Assert.That(fork.ExportCheckpoint(), Is.EqualTo(original));
            Assert.That(fork.TryAdvance(.1, new[] { new CrowdIntent { AgentId = "a", Mode = CrowdIntentMode.DesiredVelocity, DesiredVelocity = new CrowdVector(1, 0) } }, out var report), Is.True, report.Failure);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(original));
            var before = fork.ExportCheckpoint();
            Assert.That(fork.TryAdvance(.1, new[] { new CrowdIntent { AgentId = "a", DesiredVelocity = new CrowdVector(double.NaN, 0) } }, out _), Is.False);
            Assert.That(fork.ExportCheckpoint(), Is.EqualTo(before)); Assert.That(model.ExportCheckpoint(), Is.EqualTo(original));
        }
        [Test] public void ForkGeometryRebindCannotChangeOtherBranchPrivateDefinition()
        {
            var model = Model(); var original = model.ExportCheckpoint(); var fork = model.Fork(); var state = fork.ExportSnapshot();
            state.Definition.Walls[0].A.X = -3; state.Definition.Walls[0].B.X = -3;
            Assert.That(fork.TryRebind(new CrowdRebind { ExpectedTick = 0, ExpectedGeometryRevision = 0, Spaces = state.Definition.Spaces, Walls = state.Definition.Walls, Bodies = state.Agents.Select(CrowdBodyBinding.FromAgent).ToArray() }, out var failure), Is.True, failure);
            Assert.That(model.ExportCheckpoint(), Is.EqualTo(original));
            Assert.That(fork.ExportSnapshot().Definition.Walls[0].A.X, Is.EqualTo(-3));
        }
    }
}
