using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;

namespace ChooGuard.NonUnity.Tests
{
    public sealed class AuthorityIntegrityTests
    {
        private sealed class Sink : ICommitSink
        {
            public Action BeforeWrite;
            public Exception Failure;
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public void Append(ShiftCommit commit)
            {
                BeforeWrite?.Invoke();
                Commits.Add(commit.Copy());
                if (Failure != null) throw Failure;
            }
        }

        private static WorldState Initial(bool npcActive = true) => new WorldState
        {
            WorldId = "world", ShiftId = "shift",
            Participants = new[]
            {
                new ParticipantState { ParticipantId = "a", TeamId = "team", RoleId = "responder", RegionId = "hall" },
                new ParticipantState { ParticipantId = "b", TeamId = "team", RoleId = "responder", RegionId = "hall" },
                new ParticipantState { ParticipantId = "c", TeamId = "team", RoleId = "responder", RegionId = "hall" },
                new ParticipantState { ParticipantId = "teacher", TeamId = "command", RoleId = "instructor", RegionId = "hall", IsInstructor = true }
            },
            Entities = new[]
            {
                new EntityState { EntityId = "door", RegionId = "hall", Kind = EntityKind.Equipment, Position = new Point3(1, 0, 0) },
                new EntityState { EntityId = "other-door", RegionId = "hall", Kind = EntityKind.Equipment, Position = new Point3(1, 0, 1) },
                new EntityState { EntityId = "npc", RegionId = "hall", Kind = EntityKind.Evacuee, Position = new Point3(1, 0, 0), Active = npcActive }
            }
        };

        private static WorldCommand Command(string id = "first", CommandKind kind = CommandKind.Operate,
            string target = "door", long revision = 0, string argument = "", string participant = "a") => new WorldCommand
        {
            WorldId = "world", ShiftId = "shift", ParticipantId = participant,
            TeamId = participant == "teacher" ? "command" : "team", CommandId = id,
            Kind = kind, TargetId = target, ExpectedRevision = revision, Argument = argument
        };

        [TestCase("io")]
        [TestCase("access")]
        [TestCase("disposed")]
        [TestCase("invalid-operation")]
        [TestCase("cancelled")]
        public void AnySinkFailureStopsTrainingWithoutAcknowledgingOrChangingEquipment(string failure)
        {
            var sink = new Sink { Failure = failure == "io" ? new IOException() :
                failure == "access" ? new UnauthorizedAccessException() :
                failure == "disposed" ? new ObjectDisposedException("journal") :
                failure == "cancelled" ? (Exception)new OperationCanceledException() : new InvalidOperationException() };
            var shift = new AuthoritativeShift(Initial(), sink, (_, __) => true);
            CommandReceipt result = null;
            Assert.DoesNotThrow(() => result = shift.Submit("a", Command()));
            Assert.That(result.Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            var state = shift.ExportCheckpoint();
            Assert.That(state.Sequence, Is.Zero);
            Assert.That(state.Entities.Single(e => e.EntityId == "door").Active, Is.True);
            Assert.That(state.Receipts, Is.Empty);
            Assert.That(state.Paused, Is.True);
            Assert.That(state.Participants.All(p => !p.InputEnabled), Is.True);
            Assert.That(shift.Submit("teacher", Command("resume", CommandKind.ResumeShift, "", participant: "teacher")).Code,
                Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(sink.Commits.Count, Is.EqualTo(1), "An ambiguous flush must not be retried in the same authority.");
            var recovered = AuthoritativeShift.Restore(Initial(), new Sink(), (_, __) => true);
            recovered.Replay(sink.Commits[0]);
            Assert.That(recovered.Submit("a", Command()).Sequence, Is.EqualTo(1));
            Assert.That(recovered.ExportCheckpoint().Entities.Single(e => e.EntityId == "door").Active, Is.False);
        }

        [Test]
        public void SinkCannotNestAnotherAcceptedCommandIntoTheSameSequence()
        {
            var sink = new Sink();
            var shift = new AuthoritativeShift(Initial(), sink, (_, __) => true);
            CommandReceipt nested = null;
            sink.BeforeWrite = () =>
            {
                sink.BeforeWrite = null;
                nested = shift.Submit("a", Command("nested", target: "other-door"));
            };
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(nested.Code.ToString(), Is.EqualTo("AuthorityBusy"));
            Assert.That(sink.Commits.Count, Is.EqualTo(1));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "other-door").Active, Is.True);
            var next = shift.Submit("a", Command("nested", target: "other-door"));
            Assert.That(next.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(next.Sequence, Is.EqualTo(2));
            var recovered = AuthoritativeShift.Restore(Initial(), new Sink(), (_, __) => true);
            foreach (var commit in sink.Commits) recovered.Replay(commit);
            Assert.That(recovered.ExportCheckpoint().Sequence, Is.EqualTo(2));
        }

        [Test]
        public void VisibilityCallbackCannotNestASecondCommand()
        {
            var sink = new Sink(); AuthoritativeShift shift = null; CommandReceipt nested = null;
            var once = true;
            shift = new AuthoritativeShift(Initial(), sink, (_, __) =>
            {
                if (once) { once = false; nested = shift.Submit("a", Command("nested", target: "other-door")); }
                return true;
            });
            CommandReceipt outer = null;
            Assert.DoesNotThrow(() => outer = shift.Submit("a", Command()));
            Assert.That(outer.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(nested.Code.ToString(), Is.EqualTo("AuthorityBusy"));
            Assert.That(sink.Commits.Count, Is.EqualTo(1));
        }

        [Test]
        public void CallerCannotRewriteExpectedRevisionDuringVisibilityValidation()
        {
            var sink = new Sink(); WorldCommand submitted = null;
            var shift = new AuthoritativeShift(Initial(), sink, (_, __) =>
            {
                if (submitted != null) submitted.ExpectedRevision = 1;
                return true;
            });
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
            submitted = Command("stale", revision: 0);
            Assert.That(shift.Submit("a", submitted).Code, Is.EqualTo(CommandCode.StaleTarget));
            Assert.That(sink.Commits.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandoffUsesTheRequestedResponderNotALaterCallbackMutation()
        {
            var sink = new Sink(); WorldCommand submitted = null;
            var shift = new AuthoritativeShift(Initial(), sink, (_, __) =>
            {
                if (submitted != null) submitted.Argument = "c";
                return true;
            });
            shift.Submit("a", Command("claim", CommandKind.ClaimEvacuee, "npc"));
            submitted = Command("handoff", CommandKind.HandOffEvacuee, "npc", 1, "b");
            var receipt = shift.Submit("a", submitted);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc").LeaderId, Is.EqualTo("b"));
            var retry = Command("handoff", CommandKind.HandOffEvacuee, "npc", 1, "b");
            Assert.That(shift.Submit("a", retry).Sequence, Is.EqualTo(receipt.Sequence));
            Assert.That(sink.Commits.Count, Is.EqualTo(2));
        }

        [Test]
        public void PersistenceFaultSignalledDuringAppendCannotBeOverwrittenByPreparedState()
        {
            var sink = new Sink(); var shift = new AuthoritativeShift(Initial(), sink, (_, __) => true);
            sink.BeforeWrite = shift.FaultPersistence;
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(shift.Paused, Is.True);
            Assert.That(shift.ExportCheckpoint().Participants.All(p => !p.InputEnabled), Is.True);
            Assert.That(shift.ExportCheckpoint().Sequence, Is.Zero);
        }

        [Test]
        public void PersistenceFaultSignalledDuringValidationPreventsAppend()
        {
            var sink = new Sink(); AuthoritativeShift shift = null;
            shift = new AuthoritativeShift(Initial(), sink, (_, __) => { shift.FaultPersistence(); return true; });
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(sink.Commits, Is.Empty);
            Assert.That(shift.Paused, Is.True);
        }

        [Test]
        public void InactiveEvacueeCannotAcquireANewLeader()
        {
            var sink = new Sink(); var shift = new AuthoritativeShift(Initial(false), sink, (_, __) => true);
            Assert.That(shift.Submit("a", Command("claim", CommandKind.ClaimEvacuee, "npc")).Code, Is.EqualTo(CommandCode.TargetBlocked));
            Assert.That(sink.Commits, Is.Empty);
        }

        [Test]
        public void InactiveEvacueeCannotBeHandedOff()
        {
            var state = Initial(false); state.Entities.Single(e => e.EntityId == "npc").LeaderId = "a";
            var sink = new Sink(); var shift = new AuthoritativeShift(state, sink, (_, __) => true);
            Assert.That(shift.Submit("a", Command("handoff", CommandKind.HandOffEvacuee, "npc", argument: "b")).Code,
                Is.EqualTo(CommandCode.TargetBlocked));
            Assert.That(sink.Commits, Is.Empty);
        }

        [Test]
        public void CallbackCannotChangeServerPositionInsideACommandTransaction()
        {
            var sink = new Sink(); var shift = new AuthoritativeShift(Initial(), sink, (_, __) => true);
            var refused = false;
            sink.BeforeWrite = () =>
            {
                try { shift.SetServerPosition("a", "hall", new Point3(10, 0, 0)); }
                catch (InvalidOperationException) { refused = true; }
            };
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(refused, Is.True);
            Assert.That(shift.Participant("a").Position.X, Is.Zero);
        }

        [Test]
        public void FailedValidationReleasesTheCommandTransactionGuard()
        {
            var sink = new Sink(); var fail = true;
            var shift = new AuthoritativeShift(Initial(), sink, (_, __) =>
            { if (fail) throw new ArgumentException("synthetic visibility failure"); return true; });
            Assert.Throws<ArgumentException>(() => shift.Submit("a", Command()));
            Assert.That(sink.Commits, Is.Empty);
            fail = false;
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
        }
    }
}
