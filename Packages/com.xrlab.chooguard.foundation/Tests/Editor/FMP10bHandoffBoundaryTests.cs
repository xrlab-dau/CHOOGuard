using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ChooGuard.Foundation.Multiplayer;

namespace ChooGuard.Foundation.Tests
{
    [TestFixture]
    public sealed class FMP10bHandoffBoundaryTests
    {
        private sealed class TestCommitSink : ICommitSink
        {
            public int Writes;
            public ShiftCommit LastCommit;
            public bool FailOnAppend;

            public void Append(ShiftCommit commit)
            {
                if (FailOnAppend) throw new IOException("simulated storage failure");
                Writes++;
                LastCommit = commit?.Copy();
            }
        }

        private TestCommitSink sink;
        private AuthoritativeShift shift;
        private Func<ParticipantState, EntityState, bool> customVisibility;

        [SetUp]
        public void SetUp()
        {
            sink = new TestCommitSink();
            customVisibility = (actor, target) => true;

            var world = new WorldState
            {
                WorldId = "world-fmp10b",
                ShiftId = "shift-01",
                Participants = new[]
                {
                    new ParticipantState
                    {
                        ParticipantId = "guide-a",
                        TeamId = "team-alpha",
                        RoleId = "role-evac",
                        RegionId = "hall",
                        Position = new Point3(1f, 0f, 1f),
                        InputEnabled = true,
                        ObservedIds = Array.Empty<string>()
                    },
                    new ParticipantState
                    {
                        ParticipantId = "guide-b",
                        TeamId = "team-alpha",
                        RoleId = "role-evac",
                        RegionId = "hall",
                        Position = new Point3(1.5f, 0f, 1f),
                        InputEnabled = true,
                        ObservedIds = Array.Empty<string>()
                    },
                    new ParticipantState
                    {
                        ParticipantId = "guide-c",
                        TeamId = "team-alpha",
                        RoleId = "role-evac",
                        RegionId = "hall",
                        Position = new Point3(2f, 0f, 1f),
                        InputEnabled = true,
                        ObservedIds = Array.Empty<string>()
                    },
                    new ParticipantState
                    {
                        ParticipantId = "guide-patrol",
                        TeamId = "team-alpha",
                        RoleId = "role-patrol",
                        RegionId = "hall",
                        Position = new Point3(1f, 0f, 1f),
                        InputEnabled = true,
                        ObservedIds = Array.Empty<string>()
                    },
                    new ParticipantState
                    {
                        ParticipantId = "distant-guide",
                        TeamId = "team-alpha",
                        RoleId = "role-evac",
                        RegionId = "hall",
                        Position = new Point3(15f, 0f, 1f),
                        InputEnabled = true,
                        ObservedIds = Array.Empty<string>()
                    },
                    new ParticipantState
                    {
                        ParticipantId = "instructor",
                        TeamId = "command",
                        RoleId = "instructor",
                        RegionId = "hall",
                        Position = new Point3(0f, 0f, 0f),
                        IsInstructor = true,
                        InputEnabled = true,
                        ObservedIds = Array.Empty<string>()
                    }
                },
                Entities = new[]
                {
                    new EntityState
                    {
                        EntityId = "npc-evacuee-01",
                        RegionId = "hall",
                        Kind = EntityKind.Evacuee,
                        Position = new Point3(1f, 0f, 1f),
                        RequiredRoleId = "role-evac",
                        Revision = 0,
                        LeaderId = ""
                    },
                    new EntityState
                    {
                        EntityId = "npc-evacuee-free",
                        RegionId = "hall",
                        Kind = EntityKind.Evacuee,
                        Position = new Point3(1f, 0f, 1f),
                        RequiredRoleId = "",
                        Revision = 0,
                        LeaderId = ""
                    }
                },
                Reports = Array.Empty<TeamReport>(),
                Receipts = Array.Empty<CommandReceipt>()
            };

            shift = new AuthoritativeShift(world, sink, (actor, target) => customVisibility(actor, target));
        }

        private WorldCommand MakeCommand(
            string participantId,
            CommandKind kind,
            string targetId,
            string commandId = null,
            long expectedRevision = 0,
            string argument = "")
        {
            var participant = shift.Participant(participantId);
            return new WorldCommand
            {
                WorldId = "world-fmp10b",
                ShiftId = "shift-01",
                ParticipantId = participantId,
                TeamId = participant.TeamId,
                CommandId = commandId ?? Guid.NewGuid().ToString("N"),
                Kind = kind,
                TargetId = targetId,
                ExpectedRevision = expectedRevision,
                Argument = argument
            };
        }

        [Test]
        public void ClaimRace_ThreeParticipantsCompete_AcceptsFirst_RejectsSubsequent()
        {
            // guide-a claims first at revision 0 -> Accepted
            var cmdA = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-a", expectedRevision: 0);
            var receiptA = shift.Submit("guide-a", cmdA);
            Assert.That(receiptA.Code, Is.EqualTo(CommandCode.Accepted), "First claim in race must be accepted");

            var stateAfterA = shift.ExportCheckpoint();
            var evacuee = stateAfterA.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.EqualTo("guide-a"));
            Assert.That(evacuee.Revision, Is.EqualTo(1));

            // guide-b raced concurrently with expected revision 0 (stale target revision) -> StaleTarget
            var cmdB = MakeCommand("guide-b", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-b", expectedRevision: 0);
            var receiptB = shift.Submit("guide-b", cmdB);
            Assert.That(receiptB.Code, Is.EqualTo(CommandCode.StaleTarget), "Concurrent claim with stale expected revision must be rejected with StaleTarget");

            // guide-c knows current revision is 1, but evacuee already has a leader -> AlreadyClaimed
            var cmdC = MakeCommand("guide-c", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-c", expectedRevision: 1);
            var receiptC = shift.Submit("guide-c", cmdC);
            Assert.That(receiptC.Code, Is.EqualTo(CommandCode.AlreadyClaimed), "Claim on an already-led evacuee must be rejected with AlreadyClaimed");

            // Verify final authoritative state remained unchanged after rejected attempts
            var finalState = shift.ExportCheckpoint();
            var finalEvacuee = finalState.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(finalEvacuee.LeaderId, Is.EqualTo("guide-a"), "Leader must remain guide-a without corruption");
            Assert.That(finalEvacuee.Revision, Is.EqualTo(1), "Revision must remain 1");
        }

        [Test]
        public void ExplicitHandoff_SequentialChainAcrossThreeGuides_SucceedsWithRevisions()
        {
            // Step 1: guide-a claims
            var claimReceipt = shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));
            Assert.That(claimReceipt.Code, Is.EqualTo(CommandCode.Accepted));

            // Step 2: guide-a hands off to guide-b (revision 1 -> 2)
            var handoffToB = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receiptToB = shift.Submit("guide-a", handoffToB);
            Assert.That(receiptToB.Code, Is.EqualTo(CommandCode.Accepted), "Authoritative handoff from A to B must succeed");

            var stateAfterB = shift.ExportCheckpoint();
            var evacueeB = stateAfterB.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacueeB.LeaderId, Is.EqualTo("guide-b"));
            Assert.That(evacueeB.Revision, Is.EqualTo(2));

            // Step 3: guide-b hands off to guide-c (revision 2 -> 3)
            var handoffToC = MakeCommand("guide-b", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 2, argument: "guide-c");
            var receiptToC = shift.Submit("guide-b", handoffToC);
            Assert.That(receiptToC.Code, Is.EqualTo(CommandCode.Accepted), "Authoritative handoff from B to C must succeed");

            var stateAfterC = shift.ExportCheckpoint();
            var evacueeC = stateAfterC.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacueeC.LeaderId, Is.EqualTo("guide-c"));
            Assert.That(evacueeC.Revision, Is.EqualTo(3));
        }

        [Test]
        public void ExplicitHandoff_PreviousLeaderAndThirdPartyCannotHandOff()
        {
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b"));

            // guide-a is no longer leader; attempting to hand off again must fail with RoleDenied
            var staleHandoffByA = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 2, argument: "guide-c");
            var receiptStaleA = shift.Submit("guide-a", staleHandoffByA);
            Assert.That(receiptStaleA.Code, Is.EqualTo(CommandCode.RoleDenied), "Previous leader must not be permitted to hand off");

            // guide-c (bystander) attempting to hand off guide-b's evacuee must fail with RoleDenied
            var unauthorizedHandoffByC = MakeCommand("guide-c", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 2, argument: "guide-a");
            var receiptUnauthorizedC = shift.Submit("guide-c", unauthorizedHandoffByC);
            Assert.That(receiptUnauthorizedC.Code, Is.EqualTo(CommandCode.RoleDenied), "Non-leader participant must not be permitted to hand off");
        }

        [Test]
        public void ExplicitHandoff_RecipientValidation_RoleRequirementAndReachability()
        {
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));

            // Handoff to recipient who lacks required role (guide-patrol lacks role-evac) -> RoleDenied
            var handoffWrongRole = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-patrol");
            var receiptWrongRole = shift.Submit("guide-a", handoffWrongRole);
            Assert.That(receiptWrongRole.Code, Is.EqualTo(CommandCode.RoleDenied), "Handoff to recipient with non-matching role must be rejected with RoleDenied");

            // Handoff to recipient outside interaction radius (> 2.5m) -> OutOfReach
            var handoffDistant = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "distant-guide");
            var receiptDistant = shift.Submit("guide-a", handoffDistant);
            Assert.That(receiptDistant.Code, Is.EqualTo(CommandCode.OutOfReach), "Handoff to out-of-reach recipient must be rejected with OutOfReach");

            // Handoff to occluded recipient -> OutOfReach
            customVisibility = (actor, target) => actor.ParticipantId != "guide-b";
            var handoffOccluded = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receiptOccluded = shift.Submit("guide-a", handoffOccluded);
            Assert.That(receiptOccluded.Code, Is.EqualTo(CommandCode.OutOfReach), "Handoff to occluded recipient must be rejected with OutOfReach");
        }

        [Test]
        public void DuplicateCommand_IdempotentRetry_ReturnsCachedReceiptWithoutRevisionIncrement()
        {
            var cmd = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "idempotent-cmd-01", expectedRevision: 0);

            var firstReceipt = shift.Submit("guide-a", cmd);
            Assert.That(firstReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            var writesAfterFirst = sink.Writes;

            // Resend identical command (network retry simulation)
            var retryReceipt = shift.Submit("guide-a", cmd);
            Assert.That(retryReceipt.Code, Is.EqualTo(CommandCode.Accepted), "Idempotent duplicate command must return original accepted receipt");
            Assert.That(retryReceipt.CommandId, Is.EqualTo("idempotent-cmd-01"));
            Assert.That(sink.Writes, Is.EqualTo(writesAfterFirst), "Duplicate command must not commit new writes to storage");

            var checkpoint = shift.ExportCheckpoint();
            var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.Revision, Is.EqualTo(1), "Duplicate submission must not increment revision multiple times");
        }

        [Test]
        public void DuplicateCommand_ConflictingParameters_RejectedWithCommandIdConflict()
        {
            var cmdFirst = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "reuse-cmd-id", expectedRevision: 0);
            var firstReceipt = shift.Submit("guide-a", cmdFirst);
            Assert.That(firstReceipt.Code, Is.EqualTo(CommandCode.Accepted));

            // Submit different command with identical CommandId -> CommandIdConflict
            var cmdConflict = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", commandId: "reuse-cmd-id", expectedRevision: 1, argument: "guide-b");
            var conflictReceipt = shift.Submit("guide-a", cmdConflict);
            Assert.That(conflictReceipt.Code, Is.EqualTo(CommandCode.CommandIdConflict), "Reusing CommandId with different content must be rejected with CommandIdConflict");
        }

        [Test]
        public void DuplicateCommand_SameLeaderSubmitsNewClaimCommand_RejectedWithAlreadyClaimed()
        {
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-first", expectedRevision: 0));

            // guide-a already leads this evacuee; submitting a second claim with new CommandId -> AlreadyClaimed
            var secondClaim = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-second", expectedRevision: 1);
            var secondReceipt = shift.Submit("guide-a", secondClaim);
            Assert.That(secondReceipt.Code, Is.EqualTo(CommandCode.AlreadyClaimed), "Submitting a redundant claim on already-led entity must be rejected with AlreadyClaimed");
        }

        [Test]
        public void DisconnectionBoundary_LeaderInputDisabled_LeavesEvacueeOrphanedUnderCurrentRules()
        {
            // guide-a claims evacuee
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));

            // Simulated disconnect / input disabled for guide-a
            shift.SetInputEnabled("guide-a", false);

            // 1. Disconnected guide-a cannot execute handoff
            var handoffAttempt = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receiptHandoff = shift.Submit("guide-a", handoffAttempt);
            Assert.That(receiptHandoff.Code, Is.EqualTo(CommandCode.InputPaused), "Disconnected leader cannot issue handoff command");

            // 2. Active guide-b cannot claim the orphaned evacuee (remains AlreadyClaimed)
            var claimByB = MakeCommand("guide-b", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 1);
            var receiptClaimB = shift.Submit("guide-b", claimByB);
            Assert.That(receiptClaimB.Code, Is.EqualTo(CommandCode.AlreadyClaimed), "Other guides cannot claim orphaned evacuee without an explicit unclaim policy");

            // 3. Active guide-b cannot forcibly hand off the evacuee (RoleDenied)
            var takeoverByB = MakeCommand("guide-b", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receiptTakeoverB = shift.Submit("guide-b", takeoverByB);
            Assert.That(receiptTakeoverB.Code, Is.EqualTo(CommandCode.RoleDenied), "Non-leader cannot take over evacuee under current rules");

            // Demonstrates that under current AuthoritativeShift rules, disconnect creates a policy blocker
            var checkpoint = shift.ExportCheckpoint();
            var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.EqualTo("guide-a"), "Evacuee remains frozen to inactive leader until policy decision is enacted");
        }

        [Test]
        public void RegionMovementBoundary_LeaderDepartsRegion_HandoffBlockedByOutOfReach()
        {
            // guide-a claims evacuee in hall
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));

            // guide-a moves to another region without evacuee accompanying
            shift.SetServerPosition("guide-a", "platform", new Point3(10f, 0f, 0f));

            // guide-a attempts to hand off evacuee (still in hall) to guide-b (in hall)
            var handoffFromPlatform = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receipt = shift.Submit("guide-a", handoffFromPlatform);

            // OutOfReach because actor region != target region or distance > InteractionRadius
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.OutOfReach), "Handoff after leader departs region must be blocked by OutOfReach boundary check");
        }
    }
}
