using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ChooGuard.Foundation.Multiplayer;

namespace ChooGuard.Foundation.Tests
{
    public sealed class AuthoritativeShiftTests
    {
        private sealed class Sink : ICommitSink
        {
            public bool Fail;
            public bool FailAfterWrite;
            public int Writes;
            public ShiftCommit Last;
            public void Append(ShiftCommit commit)
            {
                if (Fail) throw new IOException("fixture disk interruption");
                Writes++;
                Last = commit.Copy();
                if (FailAfterWrite) throw new IOException("fixture ambiguous flush");
            }
        }

        private Sink sink;
        private AuthoritativeShift shift;

        [SetUp]
        public void SetUp()
        {
            sink = new Sink();
            shift = new AuthoritativeShift(new WorldState {
                WorldId = "world", ShiftId = "shift",
                Participants = new[] {
                    new ParticipantState { ParticipantId = "a", TeamId = "red", RoleId = "role-01", RegionId = "hall" },
                    new ParticipantState { ParticipantId = "b", TeamId = "red", RoleId = "role-01", RegionId = "hall" },
                    new ParticipantState { ParticipantId = "c", TeamId = "blue", RoleId = "role-02", RegionId = "hall" },
                    new ParticipantState { ParticipantId = "teacher", TeamId = "command", RoleId = "instructor", IsInstructor = true, RegionId = "hall" }
                },
                Entities = new[] {
                    new EntityState { EntityId = "door", RegionId = "hall", RequiredRoleId = "role-01", Kind = EntityKind.Equipment, Position = new Point3(1, 0, 0) },
                    new EntityState { EntityId = "hidden", RegionId = "hall", Kind = EntityKind.Incident, Position = new Point3(4, 0, 0) },
                    new EntityState { EntityId = "npc", RegionId = "hall", Kind = EntityKind.Evacuee, Position = new Point3(1, 0, 1) }
                }
            }, sink, (actor, target) => true);
        }

        private WorldCommand Command(string participant = "a", CommandKind kind = CommandKind.Operate,
            string target = "door", string id = "cmd", long revision = 0, string argument = "")
        {
            return new WorldCommand { WorldId = "world", ShiftId = "shift", ParticipantId = participant,
                TeamId = participant == "c" ? "blue" : participant == "teacher" ? "command" : "red",
                CommandId = id, Kind = kind, TargetId = target, ExpectedRevision = revision, Argument = argument };
        }

        [Test]
        public void SameEquipmentRaceAcceptsExactlyOneRevision()
        {
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Submit("b", Command("b")).Code, Is.EqualTo(CommandCode.StaleTarget));
            Assert.That(shift.ExportCheckpoint().Entities.Single(x => x.EntityId == "door").Revision, Is.EqualTo(1));
            Assert.That(sink.Writes, Is.EqualTo(1));
        }

        [Test]
        public void RetryReturnsOriginalReceiptAndNeverWritesTwice()
        {
            var first = shift.Submit("a", Command());
            var second = shift.Submit("a", Command());
            Assert.That(first.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(second.Sequence, Is.EqualTo(first.Sequence));
            Assert.That(second.Code, Is.EqualTo(first.Code));
            Assert.That(sink.Writes, Is.EqualTo(1));
            Assert.That(shift.Submit("a", Command(argument: "different")).Code, Is.EqualTo(CommandCode.CommandIdConflict));
        }

        [Test]
        public void TransportIdentityAndTeamCannotBeClaimedInPayload()
        {
            Assert.That(shift.Submit("c", Command()).Code, Is.EqualTo(CommandCode.IdentityMismatch));
            var forged = Command(); forged.TeamId = "blue";
            Assert.That(shift.Submit("a", forged).Code, Is.EqualTo(CommandCode.IdentityMismatch));
            Assert.That(sink.Writes, Is.Zero);
        }

        [Test]
        public void WrongRoleAndWrongShiftCannotOperate()
        {
            Assert.That(shift.Submit("c", Command("c")).Code, Is.EqualTo(CommandCode.RoleDenied));
            var old = Command(); old.ShiftId = "old";
            Assert.That(shift.Submit("a", old).Code, Is.EqualTo(CommandCode.WrongWorld));
        }

        [Test]
        public void ServerDistanceAndOcclusionAreRequired()
        {
            shift.SetServerPosition("a", "hall", new Point3(20, 0, 0));
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.OutOfReach));
            var blocked = new AuthoritativeShift(shift.ExportCheckpoint(), sink, (actor, target) => false);
            blocked.SetServerPosition("a", "hall", new Point3());
            Assert.That(blocked.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Occluded));
        }

        [Test]
        public void UndiscoveredIncidentIsAbsentEvenWhenNearby()
        {
            Assert.That(shift.Observe("a").Entities.Any(x => x.EntityId == "hidden"), Is.False);
            shift.SetServerPosition("a", "hall", new Point3(3, 0, 0));
            Assert.That(shift.Submit("a", Command(kind: CommandKind.Discover, target: "hidden")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Observe("a").Entities.Any(x => x.EntityId == "hidden"), Is.True);
            Assert.That(shift.Observe("b").Entities.Any(x => x.EntityId == "hidden"), Is.False);
            Assert.That(shift.Observe("c").Entities.Any(x => x.EntityId == "hidden"), Is.False);
        }

        [Test]
        public void OnlyObservedIncidentCanBeReportedAndOnlyOwnTeamReceivesIt()
        {
            Assert.That(shift.Submit("a", Command(kind: CommandKind.Report, target: "hidden")).Code, Is.EqualTo(CommandCode.NotObserved));
            shift.SetServerPosition("a", "hall", new Point3(3, 0, 0));
            shift.Submit("a", Command(kind: CommandKind.Discover, target: "hidden", id: "see"));
            Assert.That(shift.Submit("a", Command(kind: CommandKind.Report, target: "hidden", id: "report")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Observe("b").Reports.Length, Is.EqualTo(1));
            Assert.That(shift.Observe("c").Reports, Is.Empty);
            Assert.That(shift.Observe("b").Entities.Any(x => x.EntityId == "hidden"), Is.False, "Team reports must not grant live world vision");
        }

        [Test]
        public void NpcClaimRaceAndExplicitHandoffUseRevisions()
        {
            Assert.That(shift.Submit("a", Command(kind: CommandKind.ClaimEvacuee, target: "npc")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Submit("b", Command("b", CommandKind.ClaimEvacuee, "npc", revision: 1)).Code, Is.EqualTo(CommandCode.AlreadyClaimed));
            Assert.That(shift.Submit("a", Command(kind: CommandKind.HandOffEvacuee, target: "npc", id: "handoff", revision: 1, argument: "b")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.ExportCheckpoint().Entities.Single(x => x.EntityId == "npc").LeaderId, Is.EqualTo("b"));
        }

        [Test]
        public void LocalInputPauseDoesNotPauseWorldAndInstructorControlsShift()
        {
            shift.SetInputEnabled("a", false);
            Assert.That(shift.ExportCheckpoint().Paused, Is.False);
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.InputPaused));
            Assert.That(shift.Submit("b", Command("b", CommandKind.PauseShift, "")).Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(shift.Submit("teacher", Command("teacher", CommandKind.PauseShift, "")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Submit("b", Command("b")).Code, Is.EqualTo(CommandCode.ShiftPaused));
            Assert.That(shift.Submit("teacher", Command("teacher", CommandKind.ResumeShift, "", "resume")).Code, Is.EqualTo(CommandCode.Accepted));
        }

        [Test]
        public void DiskFailureCannotAcknowledgeOrChangeTheWorld()
        {
            sink.Fail = true;
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(shift.ExportCheckpoint().Sequence, Is.Zero);
            Assert.That(shift.ExportCheckpoint().Entities.Single(x => x.EntityId == "door").Revision, Is.Zero);
            Assert.That(shift.Paused, Is.True);
            sink.Fail = false;
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.PersistenceUnavailable), "A failed writer must be reopened and reconciled before another approval");
            var recovered = AuthoritativeShift.Restore(shift.ExportCheckpoint(), sink, (a, t) => true);
            Assert.That(recovered.Submit("teacher", Command("teacher", CommandKind.ResumeShift, "", "resume-after-storage-recovery")).Code, Is.EqualTo(CommandCode.Accepted));
            recovered.SetInputEnabled("a", true);
            Assert.That(recovered.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
        }

        [Test]
        public void CheckpointRestoreRequiresInstructorAndRetainsDedupe()
        {
            var first = shift.Submit("a", Command());
            var restored = AuthoritativeShift.Restore(shift.ExportCheckpoint(), sink, (a, t) => true);
            Assert.That(restored.ExportCheckpoint().Paused, Is.True);
            Assert.That(restored.Submit("a", Command()).Sequence, Is.EqualTo(first.Sequence));
            Assert.That(restored.Submit("b", Command("b")).Code, Is.EqualTo(CommandCode.ShiftPaused));
            Assert.That(restored.Submit("teacher", Command("teacher", CommandKind.ResumeShift, "")).Code, Is.EqualTo(CommandCode.Accepted));
        }

        [Test]
        public void CheckpointAndViewsDoNotExposeMutableWorldReferences()
        {
            var checkpoint = shift.ExportCheckpoint(); checkpoint.Entities[0].Revision = 999;
            var view = shift.Observe("a"); view.Entities[0].Revision = 333;
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.Accepted));
        }

        [Test]
        public void RejectsNonFiniteServerPositionAndMalformedCommand()
        {
            Assert.Throws<ArgumentException>(() => shift.SetServerPosition("a", "hall", new Point3(float.NaN, 0, 0)));
            var malformed = Command(); malformed.CommandId = "";
            Assert.That(shift.Submit("a", malformed).Code, Is.EqualTo(CommandCode.InvalidCommand));
            malformed = Command(); malformed.Kind = (CommandKind)999;
            Assert.That(shift.Submit("a", malformed).Code, Is.EqualTo(CommandCode.InvalidCommand));
        }

        [Test]
        public void ReplayValidatesAllDeltasBeforeChangingAnyState()
        {
            shift.Submit("a", Command());
            var commit = sink.Last.Copy();
            commit.Entities = commit.Entities.Concat(new[] { new EntityState { EntityId = "missing", RegionId = "hall" } }).ToArray();
            SetUp();
            Assert.Throws<InvalidDataException>(() => shift.Replay(commit));
            Assert.That(shift.ExportCheckpoint().Entities[0].Revision, Is.Zero);
            Assert.That(shift.ExportCheckpoint().Sequence, Is.Zero);
        }

        [Test]
        public void RestoreRejectsMissingReceiptRatherThanExecutingItAgain()
        {
            shift.Submit("a", Command());
            var checkpoint = shift.ExportCheckpoint(); checkpoint.Receipts = Array.Empty<CommandReceipt>();
            Assert.Throws<InvalidDataException>(() => AuthoritativeShift.Restore(checkpoint, sink, (a, t) => true));
        }

        [Test]
        public void AmbiguousAppendStopsFurtherApprovalsUntilDiskReconciliation()
        {
            sink.FailAfterWrite = true;
            Assert.That(shift.Submit("a", Command()).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            sink.FailAfterWrite = false;
            Assert.That(shift.Submit("b", Command("b")).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(sink.Writes, Is.EqualTo(1));
        }

        [Test]
        public void FutureInactiveIncidentCannotBeDiscovered()
        {
            var checkpoint = shift.ExportCheckpoint(); checkpoint.Entities.Single(x => x.EntityId == "hidden").Active = false;
            shift = new AuthoritativeShift(checkpoint, sink, (a, t) => true);
            shift.SetServerPosition("a", "hall", new Point3(3, 0, 0));
            Assert.That(shift.Submit("a", Command(kind: CommandKind.Discover, target: "hidden")).Code, Is.EqualTo(CommandCode.UnknownTarget));
            Assert.That(shift.Observe("a").Entities.Any(x => x.EntityId == "hidden"), Is.False);
        }

        [Test]
        public void OldObservationCannotReportInactiveIncidentState()
        {
            var checkpoint = shift.ExportCheckpoint(); checkpoint.Entities.Single(x => x.EntityId == "hidden").Active = false;
            checkpoint.Participants[0].ObservedIds = new[] { "hidden" };
            shift = new AuthoritativeShift(checkpoint, sink, (a, t) => true);
            shift.SetServerPosition("a", "hall", new Point3(3, 0, 0));
            Assert.That(shift.Submit("a", Command(kind: CommandKind.Report, target: "hidden")).Code, Is.EqualTo(CommandCode.NotObserved));
            Assert.That(shift.Observe("b").Reports, Is.Empty);
        }

        [Test]
        public void HandoffRecipientMustSatisfyTheSameRolePolicyAsDirectClaim()
        {
            var checkpoint = shift.ExportCheckpoint(); checkpoint.Entities.Single(x => x.EntityId == "npc").RequiredRoleId = "role-01";
            shift = new AuthoritativeShift(checkpoint, sink, (a, t) => true);
            shift.Submit("a", Command(kind: CommandKind.ClaimEvacuee, target: "npc"));
            Assert.That(shift.Submit("a", Command(kind: CommandKind.HandOffEvacuee, target: "npc", id: "handoff", revision: 1, argument: "c")).Code, Is.EqualTo(CommandCode.RoleDenied));
        }

        [Test]
        public void ApprovalJournalRetainsAuthoritativePositionsAtItsBoundary()
        {
            var before = shift.ExportCheckpoint();
            shift.SetServerPosition("a", "hall", new Point3(1, 0, 0));
            shift.Submit("a", Command());
            var recovered = AuthoritativeShift.Restore(before, new Sink(), (a, t) => true);
            recovered.Replay(sink.Last);
            Assert.That(recovered.ExportCheckpoint().Participants.Single(x => x.ParticipantId == "a").Position.X, Is.EqualTo(1));
        }

        [Test]
        public void InstructorMenuControlsShiftWhilePersonalMovementIsPaused()
        {
            shift.SetInputEnabled("teacher", false);
            Assert.That(shift.Submit("teacher", Command("teacher", CommandKind.PauseShift, "")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.True);
            Assert.That(shift.Submit("teacher", Command("teacher", CommandKind.ResumeShift, "", "resume")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Participant("teacher").InputEnabled, Is.False);
        }

        [Test]
        public void ServerPerceptionFindsVisibleNearbyIncidentWithoutClientKnowingItsId()
        {
            Assert.That(shift.DiscoverNearby("a", p => true), Is.Zero);
            shift.SetServerPosition("a", "hall", new Point3(3, 0, 0));
            Assert.That(shift.DiscoverNearby("a", p => false), Is.Zero);
            Assert.That(shift.DiscoverNearby("a", p => true), Is.EqualTo(1));
            Assert.That(shift.DiscoverNearby("a", p => true), Is.Zero);
            Assert.That(shift.Observe("a").Entities.Any(x => x.EntityId == "hidden"), Is.True);
        }

        [Test]
        public void ReportsAreBoundedOnWireWhileAllRecordsRemainDurable()
        {
            shift.SetServerPosition("a", "hall", new Point3(3, 0, 0));
            shift.DiscoverNearby("a", p => true);
            for (var i = 0; i < 40; i++)
                Assert.That(shift.Submit("a", Command(kind: CommandKind.Report, target: "hidden", id: "report-" + i)).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Observe("b").Reports.Length, Is.EqualTo(16));
            Assert.That(shift.Observe("b").TotalReports, Is.EqualTo(40));
            Assert.That(shift.ExportCheckpoint().Reports.Length, Is.EqualTo(40));
        }
    }
}
