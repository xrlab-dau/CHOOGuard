using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using ChooGuard.Foundation.Tests;
using NUnit.Framework;

namespace ChooGuard.NonUnity.Tests
{
    public sealed class AuthorityBoundaryTests
    {
        private sealed class Sink : ICommitSink
        {
            public Action BeforeWrite;
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public void Append(ShiftCommit commit)
            {
                BeforeWrite?.Invoke();
                Commits.Add(commit.Copy());
            }
        }

        [TestCase("pose")]
        [TestCase("input")]
        [TestCase("replay")]
        [TestCase("simulation")]
        [TestCase("prepared-simulation")]
        [TestCase("discovery")]
        public void EveryServerMutationRejectsReentryWithoutChangingThePreparedPhysicalBoundary(string operation)
        {
            var initial = SimulatedAuthorityTests.Initial();
            initial.Participants[0].ObservedIds = Array.Empty<string>();
            var sink = new Sink();
            var shift = new AuthoritativeShift(initial, sink, (_, __) => true);
            var otherSink = new Sink();
            var other = new AuthoritativeShift(initial, otherSink, (_, __) => true);
            other.Submit("p", SimulatedAuthorityTests.Command(CommandKind.Operate, "other-commit", "equipment"));
            var update = SimulatedAuthorityTests.Move(initial, 5, 1);
            var physical = PhysicalCheckpoint.Decode(update.Checkpoint, update.DefinitionHash);
            var prepared = PhysicalCheckpoint.Prepare(physical, CrowdMotionModel.FromCheckpoint(physical.CrowdCheckpoint).ExportCheckpointProof());
            var blocked = false;
            sink.BeforeWrite = () =>
            {
                try
                {
                    switch (operation)
                    {
                        case "pose": shift.SetServerPose("p", new SpatialPose { RegionId = "train", FrameId = "train", Position = new Point3(2, 0, 0), LocalPosition = new Point3(2, 0, 0) }); break;
                        case "input": shift.SetInputEnabled("p", false); break;
                        case "replay": shift.Replay(otherSink.Commits[0]); break;
                        case "simulation": shift.ApplyServerSimulation(update); break;
                        case "prepared-simulation": shift.ApplyPreparedServerSimulation(update, prepared); break;
                        case "discovery": shift.DiscoverNearby("p", _ => true); break;
                        default: throw new ArgumentException("Unknown test operation.");
                    }
                }
                catch (InvalidOperationException) { blocked = true; }
            };
            var command = SimulatedAuthorityTests.Command(CommandKind.Operate, "outer", "equipment");
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(blocked, Is.True, operation);
            Assert.That(sink.Commits.Count, Is.EqualTo(1));
            var after = shift.ExportCheckpoint();
            Assert.That(after.Sequence, Is.EqualTo(1));
            Assert.That(after.SimulationTick, Is.Zero);
            Assert.That(after.SimulationCheckpoint, Is.EqualTo(initial.SimulationCheckpoint));
            Assert.That(after.Participants[0].Position.X, Is.Zero);
            Assert.That(after.Participants[0].InputEnabled, Is.True);
            Assert.That(after.Participants[0].ObservedIds, Is.Empty);
            sink.BeforeWrite = null;
            shift.ApplyPreparedServerSimulation(SimulatedAuthorityTests.Move(after, 5, 1), prepared);
            Assert.That(shift.ReadSimulation().Tick, Is.EqualTo(1), "Guard must release after acceptance.");
        }

        [Test]
        public void EquipmentInterlockCannotSubmitAnotherCommandDuringValidation()
        {
            var initial = SimulatedAuthorityTests.Initial(); var sink = new Sink();
            AuthoritativeShift shift = null; CommandReceipt nested = null; var once = true;
            shift = new AuthoritativeShift(initial, sink, (_, __) => true, _ =>
            {
                if (once)
                {
                    once = false;
                    nested = shift.Submit("p", SimulatedAuthorityTests.Command(CommandKind.Operate, "nested", "equipment"));
                }
                return true;
            });
            Assert.That(shift.Submit("p", SimulatedAuthorityTests.Command(CommandKind.Operate, "outer", "equipment")).Code,
                Is.EqualTo(CommandCode.Accepted));
            Assert.That(nested.Code, Is.EqualTo(CommandCode.AuthorityBusy));
            Assert.That(sink.Commits.Count, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EquipmentInterlockFaultAlwaysWinsOverItsReturnedDecision(bool decision)
        {
            var initial = SimulatedAuthorityTests.Initial(); var sink = new Sink(); AuthoritativeShift shift = null;
            shift = new AuthoritativeShift(initial, sink, (_, __) => true, _ => { shift.FaultPersistence(); return decision; });
            Assert.That(shift.Submit("p", SimulatedAuthorityTests.Command(CommandKind.Operate, "operate", "equipment")).Code,
                Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(sink.Commits, Is.Empty);
            Assert.That(shift.Paused, Is.True);
            Assert.That(shift.ReadSimulation().Participants.All(p => !p.InputEnabled), Is.True);
        }

        [Test]
        public void ExistingWireCodeNumbersAndFrozenCommandFieldsRemainCompatible()
        {
            var names = new[] { "Accepted", "InvalidCommand", "IdentityMismatch", "WrongWorld", "CommandIdConflict",
                "RoleDenied", "UnknownTarget", "StaleTarget", "OutOfReach", "Occluded", "NotObserved", "AlreadyClaimed",
                "InputPaused", "ShiftPaused", "PersistenceUnavailable", "TargetBlocked" };
            for (var i = 0; i < names.Length; i++) Assert.That(((CommandCode)i).ToString(), Is.EqualTo(names[i]));
            Assert.That((int)CommandCode.AuthorityBusy, Is.EqualTo(16));
            var original = SimulatedAuthorityTests.Command(CommandKind.HandOffEvacuee, "id", "npc");
            original.Argument = "recipient"; original.ExpectedRevision = 7;
            var copy = original.Copy();
            Assert.That(copy, Is.Not.SameAs(original));
            foreach (var field in typeof(WorldCommand).GetFields()) Assert.That(field.GetValue(copy), Is.EqualTo(field.GetValue(original)), field.Name);
            original.Argument = "different";
            Assert.That(copy.Argument, Is.EqualTo("recipient"));
        }
    }
}
