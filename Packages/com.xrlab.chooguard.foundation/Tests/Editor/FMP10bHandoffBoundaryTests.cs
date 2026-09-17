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

        private WorldState CreateDefaultWorldState()
        {
            return new WorldState
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
        }

        [SetUp]
        public void SetUp()
        {
            sink = new TestCommitSink();
            customVisibility = (actor, target) => true;
            shift = new AuthoritativeShift(CreateDefaultWorldState(), sink, (actor, target) => customVisibility(actor, target));
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

        private static void AssertReceiptLedgerAndStateEqual(
            WorldState before, int writesBefore,
            WorldState after, int writesAfter,
            string relevantEntityId = null)
        {
            Assert.That(writesAfter, Is.EqualTo(writesBefore), "Sink writes count must not change on rejected command");
            Assert.That(after.Sequence, Is.EqualTo(before.Sequence), "World sequence must not change on rejected command");
            Assert.That(after.Receipts.Length, Is.EqualTo(before.Receipts.Length), "Receipt ledger count must not change on rejected command");
            for (int i = 0; i < before.Receipts.Length; i++)
            {
                Assert.That(after.Receipts[i].CommandId, Is.EqualTo(before.Receipts[i].CommandId));
                Assert.That(after.Receipts[i].ParticipantId, Is.EqualTo(before.Receipts[i].ParticipantId));
                Assert.That(after.Receipts[i].Fingerprint, Is.EqualTo(before.Receipts[i].Fingerprint));
                Assert.That(after.Receipts[i].Code, Is.EqualTo(before.Receipts[i].Code));
                Assert.That(after.Receipts[i].Sequence, Is.EqualTo(before.Receipts[i].Sequence));
            }
            if (!string.IsNullOrEmpty(relevantEntityId))
            {
                var eBefore = before.Entities.Single(e => e.EntityId == relevantEntityId);
                var eAfter = after.Entities.Single(e => e.EntityId == relevantEntityId);
                Assert.That(eAfter.LeaderId, Is.EqualTo(eBefore.LeaderId), $"LeaderId of {relevantEntityId} must remain unchanged");
                Assert.That(eAfter.Revision, Is.EqualTo(eBefore.Revision), $"Revision of {relevantEntityId} must remain unchanged");
            }
        }


        [Test]
        public void ClaimRace_ThreeParticipantsCompete_AllPermutations_AcceptsOnlyFirst_SubsequentStaleTarget()
        {
            var guides = new[] { "guide-a", "guide-b", "guide-c" };

            // All 6 permutations of arrival order for 3 concurrent participants with initial revision 0
            var permutations = new[]
            {
                new[] { 0, 1, 2 },
                new[] { 0, 2, 1 },
                new[] { 1, 0, 2 },
                new[] { 1, 2, 0 },
                new[] { 2, 0, 1 },
                new[] { 2, 1, 0 }
            };

            foreach (var perm in permutations)
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);

                var pFirst = guides[perm[0]];
                var pSecond = guides[perm[1]];
                var pThird = guides[perm[2]];

                // First arrival with revision 0 -> Accepted
                var cmd1 = new WorldCommand
                {
                    WorldId = "world-fmp10b", ShiftId = "shift-01",
                    ParticipantId = pFirst, TeamId = "team-alpha",
                    CommandId = "claim-first-" + pFirst, Kind = CommandKind.ClaimEvacuee,
                    TargetId = "npc-evacuee-01", ExpectedRevision = 0
                };
                var receipt1 = testShift.Submit(pFirst, cmd1);
                Assert.That(receipt1.Code, Is.EqualTo(CommandCode.Accepted), $"Permutation [{string.Join(",", perm)}]: first arrival {pFirst} must succeed");
                Assert.That(receipt1.Sequence, Is.EqualTo(1));
                Assert.That(testSink.Writes, Is.EqualTo(1));

                var stateAfter1 = testShift.ExportCheckpoint();
                var evacuee1 = stateAfter1.Entities.Single(e => e.EntityId == "npc-evacuee-01");
                Assert.That(evacuee1.LeaderId, Is.EqualTo(pFirst));
                Assert.That(evacuee1.Revision, Is.EqualTo(1));
                Assert.That(stateAfter1.Sequence, Is.EqualTo(1));
                Assert.That(stateAfter1.Receipts.Length, Is.EqualTo(1));

                // Second arrival racing with same initial snapshot (revision 0) -> StaleTarget
                var cmd2 = new WorldCommand
                {
                    WorldId = "world-fmp10b", ShiftId = "shift-01",
                    ParticipantId = pSecond, TeamId = "team-alpha",
                    CommandId = "claim-second-" + pSecond, Kind = CommandKind.ClaimEvacuee,
                    TargetId = "npc-evacuee-01", ExpectedRevision = 0
                };
                var receipt2 = testShift.Submit(pSecond, cmd2);
                Assert.That(receipt2.Code, Is.EqualTo(CommandCode.StaleTarget), $"Permutation [{string.Join(",", perm)}]: second arrival {pSecond} with stale revision must be StaleTarget");
                Assert.That(testSink.Writes, Is.EqualTo(1), "Rejected second claim must not increment storage writes");

                var stateAfter2 = testShift.ExportCheckpoint();
                var evacuee2 = stateAfter2.Entities.Single(e => e.EntityId == "npc-evacuee-01");
                Assert.That(evacuee2.LeaderId, Is.EqualTo(pFirst), "Leader must remain unchanged after rejected second claim");
                Assert.That(evacuee2.Revision, Is.EqualTo(1));
                Assert.That(stateAfter2.Sequence, Is.EqualTo(1));
                Assert.That(stateAfter2.Receipts.Length, Is.EqualTo(1));

                // Third arrival racing with same initial snapshot (revision 0) -> StaleTarget
                var cmd3 = new WorldCommand
                {
                    WorldId = "world-fmp10b", ShiftId = "shift-01",
                    ParticipantId = pThird, TeamId = "team-alpha",
                    CommandId = "claim-third-" + pThird, Kind = CommandKind.ClaimEvacuee,
                    TargetId = "npc-evacuee-01", ExpectedRevision = 0
                };
                var receipt3 = testShift.Submit(pThird, cmd3);
                Assert.That(receipt3.Code, Is.EqualTo(CommandCode.StaleTarget), $"Permutation [{string.Join(",", perm)}]: third arrival {pThird} with stale revision must be StaleTarget");
                Assert.That(testSink.Writes, Is.EqualTo(1), "Rejected third claim must not increment storage writes");

                var finalState = testShift.ExportCheckpoint();
                var finalEvacuee = finalState.Entities.Single(e => e.EntityId == "npc-evacuee-01");
                Assert.That(finalEvacuee.LeaderId, Is.EqualTo(pFirst), "Final leader must remain the first arrival");
                Assert.That(finalEvacuee.Revision, Is.EqualTo(1));
                Assert.That(finalState.Sequence, Is.EqualTo(1));
                Assert.That(finalState.Receipts.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void ClaimRace_SubsequentClaimWithObservedRevision_RejectedWithAlreadyClaimed()
        {
            // guide-a claims first at revision 0 -> Accepted
            var cmdA = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-a", expectedRevision: 0);
            var receiptA = shift.Submit("guide-a", cmdA);
            Assert.That(receiptA.Code, Is.EqualTo(CommandCode.Accepted));

            // guide-b observes the updated revision 1, but still attempts to claim the led evacuee -> AlreadyClaimed
            var writesBefore = sink.Writes;
            var cmdB = MakeCommand("guide-b", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-b-aware", expectedRevision: 1);
            var receiptB = shift.Submit("guide-b", cmdB);

            Assert.That(receiptB.Code, Is.EqualTo(CommandCode.AlreadyClaimed), "Claim on an already-led entity with current revision must be AlreadyClaimed");
            Assert.That(sink.Writes, Is.EqualTo(writesBefore), "AlreadyClaimed rejection must not write to commit sink");

            var finalState = shift.ExportCheckpoint();
            var evacuee = finalState.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.EqualTo("guide-a"));
            Assert.That(evacuee.Revision, Is.EqualTo(1));
            Assert.That(finalState.Sequence, Is.EqualTo(1));
            Assert.That(finalState.Receipts.Length, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitHandoff_SequentialChainAcrossThreeGuides_SucceedsWithRevisions()
        {
            // Step 1: guide-a claims (0 -> 1)
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
            Assert.That(stateAfterC.Sequence, Is.EqualTo(3));
            Assert.That(sink.Writes, Is.EqualTo(3));
        }

        [Test]
        public void ExplicitHandoff_PreviousLeaderAndThirdPartyCannotHandOff()
        {
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));
            shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b"));

            var writesBefore = sink.Writes;

            // guide-a is no longer leader; attempting to hand off again must fail with RoleDenied
            var staleHandoffByA = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", commandId: "stale-handoff-a", expectedRevision: 2, argument: "guide-c");
            var receiptStaleA = shift.Submit("guide-a", staleHandoffByA);
            Assert.That(receiptStaleA.Code, Is.EqualTo(CommandCode.RoleDenied), "Previous leader must not be permitted to hand off");
            Assert.That(sink.Writes, Is.EqualTo(writesBefore));

            // guide-c (bystander) attempting to hand off guide-b's evacuee must fail with RoleDenied
            var unauthorizedHandoffByC = MakeCommand("guide-c", CommandKind.HandOffEvacuee, "npc-evacuee-01", commandId: "unauth-handoff-c", expectedRevision: 2, argument: "guide-a");
            var receiptUnauthorizedC = shift.Submit("guide-c", unauthorizedHandoffByC);
            Assert.That(receiptUnauthorizedC.Code, Is.EqualTo(CommandCode.RoleDenied), "Non-leader participant must not be permitted to hand off");
            Assert.That(sink.Writes, Is.EqualTo(writesBefore));

            var checkpoint = shift.ExportCheckpoint();
            var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.EqualTo("guide-b"), "Leader must remain guide-b");
            Assert.That(evacuee.Revision, Is.EqualTo(2));
        }

        [Test]
        public void ExplicitHandoff_RecipientValidation_RoleRequirementAndReachability()
        {
            var claim = shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));
            Assert.That(claim.Code, Is.EqualTo(CommandCode.Accepted));

            var writesBefore = sink.Writes;

            // Handoff to recipient who lacks required role (guide-patrol lacks role-evac) -> RoleDenied
            var handoffWrongRole = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-patrol");
            var receiptWrongRole = shift.Submit("guide-a", handoffWrongRole);
            Assert.That(receiptWrongRole.Code, Is.EqualTo(CommandCode.RoleDenied), "Handoff to recipient with non-matching role must be rejected with RoleDenied");
            Assert.That(sink.Writes, Is.EqualTo(writesBefore));

            // Handoff to recipient far outside interaction radius (14m away) -> OutOfReach
            var handoffDistant = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "distant-guide");
            var receiptDistant = shift.Submit("guide-a", handoffDistant);
            Assert.That(receiptDistant.Code, Is.EqualTo(CommandCode.OutOfReach), "Handoff to distant recipient must be rejected with OutOfReach");
            Assert.That(sink.Writes, Is.EqualTo(writesBefore));

            // Handoff to occluded recipient -> OutOfReach
            customVisibility = (actor, target) => actor.ParticipantId != "guide-b";
            var handoffOccluded = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receiptOccluded = shift.Submit("guide-a", handoffOccluded);
            Assert.That(receiptOccluded.Code, Is.EqualTo(CommandCode.OutOfReach), "Handoff to occluded recipient must be rejected with OutOfReach");
            Assert.That(sink.Writes, Is.EqualTo(writesBefore));

            var checkpoint = shift.ExportCheckpoint();
            var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.EqualTo("guide-a"));
            Assert.That(evacuee.Revision, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitHandoff_RecipientInputDisabled_RejectedWithOutOfReach()
        {
            var claim = shift.Submit("guide-a", MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0));
            Assert.That(claim.Code, Is.EqualTo(CommandCode.Accepted), "Initial claim must be accepted");

            // Disable input of recipient guide-b (same role, same region, within 0.5m, visible)
            shift.SetInputEnabled("guide-b", false);

            var stateBefore = shift.ExportCheckpoint();
            var writesBefore = sink.Writes;

            // Attempt handoff to input-disabled recipient -> OutOfReach per AuthoritativeShift.cs line 287
            var handoffToDisabled = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receipt = shift.Submit("guide-a", handoffToDisabled);

            Assert.That(receipt.Code, Is.EqualTo(CommandCode.OutOfReach), "Handoff to input-disabled recipient must be rejected with OutOfReach");
            AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-01");

            var stateAfter = shift.ExportCheckpoint();
            var evacuee = stateAfter.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.EqualTo("guide-a"), "Leader must remain guide-a without transferring to inactive recipient");
            Assert.That(evacuee.Revision, Is.EqualTo(stateBefore.Entities.Single(e => e.EntityId == "npc-evacuee-01").Revision));
            Assert.That(stateAfter.Sequence, Is.EqualTo(stateBefore.Sequence));

            // Positive control: re-enabling input allows the exact same handoff to succeed
            shift.SetInputEnabled("guide-b", true);
            var handoffToEnabled = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", expectedRevision: 1, argument: "guide-b");
            var receiptEnabled = shift.Submit("guide-a", handoffToEnabled);

            Assert.That(receiptEnabled.Code, Is.EqualTo(CommandCode.Accepted), "Handoff to re-enabled recipient must succeed");
            var finalEvacuee = shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(finalEvacuee.LeaderId, Is.EqualTo("guide-b"));
            Assert.That(finalEvacuee.Revision, Is.EqualTo(2));
        }

        [Test]
        public void RegionBoundary_SameCoordinatesDifferentRegion_RejectedWithOutOfReachInSchema1()
        {
            // Initial entity is in "hall" at (1, 0, 1)
            // Move actor to "platform", but place at the EXACT same position (1, 0, 1) -> distance is 0.0m <= 2.5m
            shift.SetServerPosition("guide-a", "platform", new Point3(1f, 0f, 1f));

            var stateBefore = shift.ExportCheckpoint();
            var writesBefore = sink.Writes;
            var cmd = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", expectedRevision: 0);
            var receipt = shift.Submit("guide-a", cmd);

            // In SchemaVersion 1, InReach requires actor.RegionId == target.RegionId
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.OutOfReach), "In SchemaVersion 1, different RegionId must be rejected with OutOfReach even at distance 0");
            AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-01");

            var checkpoint = shift.ExportCheckpoint();
            var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.LeaderId, Is.Empty, "Evacuee must remain unled");
            Assert.That(evacuee.Revision, Is.Zero);
            Assert.That(checkpoint.Sequence, Is.Zero);
        }

        [Test]
        public void DistanceBoundary_InteractionRadiusPrecision_AcceptsAtAndBelow2Point5_RejectsAbove()
        {
            const float epsilon = 0.01f;

            // Target npc-evacuee-01 is at (1, 0, 1)
            // Case 1: Distance = 2.5m - eps = 2.49m -> Position = (1 + 2.49, 0, 1) = (3.49, 0, 1)
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);
                testShift.SetServerPosition("guide-a", "hall", new Point3(1f + 2.5f - epsilon, 0f, 1f));

                var cmd = new WorldCommand
                {
                    WorldId = "world-fmp10b", ShiftId = "shift-01",
                    ParticipantId = "guide-a", TeamId = "team-alpha",
                    CommandId = "claim-below-boundary", Kind = CommandKind.ClaimEvacuee,
                    TargetId = "npc-evacuee-01", ExpectedRevision = 0
                };
                var receipt = testShift.Submit("guide-a", cmd);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted), "Distance at (2.5 - eps) must be accepted");
                Assert.That(testSink.Writes, Is.EqualTo(1));
            }

            // Case 2: Distance = exactly 2.50m -> Position = (1 + 2.50, 0, 1) = (3.50, 0, 1)
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);
                testShift.SetServerPosition("guide-a", "hall", new Point3(1f + 2.5f, 0f, 1f));

                var cmd = new WorldCommand
                {
                    WorldId = "world-fmp10b", ShiftId = "shift-01",
                    ParticipantId = "guide-a", TeamId = "team-alpha",
                    CommandId = "claim-exact-boundary", Kind = CommandKind.ClaimEvacuee,
                    TargetId = "npc-evacuee-01", ExpectedRevision = 0
                };
                var receipt = testShift.Submit("guide-a", cmd);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted), "Exact boundary distance 2.5m (<= 2.5) must be accepted");
                Assert.That(testSink.Writes, Is.EqualTo(1));
            }

            // Case 3: Distance = 2.5m + eps = 2.51m -> Position = (1 + 2.51, 0, 1) = (3.51, 0, 1)
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);
                testShift.SetServerPosition("guide-a", "hall", new Point3(1f + 2.5f + epsilon, 0f, 1f));

                var stateBefore = testShift.ExportCheckpoint();
                var writesBefore = testSink.Writes;

                var cmd = new WorldCommand
                {
                    WorldId = "world-fmp10b", ShiftId = "shift-01",
                    ParticipantId = "guide-a", TeamId = "team-alpha",
                    CommandId = "claim-above-boundary", Kind = CommandKind.ClaimEvacuee,
                    TargetId = "npc-evacuee-01", ExpectedRevision = 0
                };
                var receipt = testShift.Submit("guide-a", cmd);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.OutOfReach), "Distance at (2.5 + eps) must be rejected with OutOfReach");
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, testShift.ExportCheckpoint(), testSink.Writes, "npc-evacuee-01");

                var checkpoint = testShift.ExportCheckpoint();
                var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
                Assert.That(evacuee.LeaderId, Is.Empty);
                Assert.That(evacuee.Revision, Is.Zero);
                Assert.That(checkpoint.Sequence, Is.Zero);
            }
        }

        [Test]
        public void DistanceBoundary_RecipientReachabilityPrecision_AcceptsAtAndBelow2Point5_RejectsAbove()
        {
            const float epsilon = 0.01f;

            // Target npc-evacuee-01 is at (1, 0, 1)
            // Actor guide-a is at (1, 0, 1) (distance 0m)
            // Recipient guide-b is tested at 2.49m, 2.50m, 2.51m from entity
            // Case 1: Recipient at 2.49m -> Accepted
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);
                testShift.Submit("guide-a", new WorldCommand { WorldId = "world-fmp10b", ShiftId = "shift-01", ParticipantId = "guide-a", TeamId = "team-alpha", CommandId = "c1", Kind = CommandKind.ClaimEvacuee, TargetId = "npc-evacuee-01", ExpectedRevision = 0 });

                testShift.SetServerPosition("guide-b", "hall", new Point3(1f + 2.5f - epsilon, 0f, 1f));
                var handoff = new WorldCommand { WorldId = "world-fmp10b", ShiftId = "shift-01", ParticipantId = "guide-a", TeamId = "team-alpha", CommandId = "h1", Kind = CommandKind.HandOffEvacuee, TargetId = "npc-evacuee-01", ExpectedRevision = 1, Argument = "guide-b" };
                var receipt = testShift.Submit("guide-a", handoff);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted), "Recipient distance at 2.49m must be accepted");
                Assert.That(testShift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-evacuee-01").LeaderId, Is.EqualTo("guide-b"));
            }

            // Case 2: Recipient at 2.50m -> Accepted
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);
                testShift.Submit("guide-a", new WorldCommand { WorldId = "world-fmp10b", ShiftId = "shift-01", ParticipantId = "guide-a", TeamId = "team-alpha", CommandId = "c1", Kind = CommandKind.ClaimEvacuee, TargetId = "npc-evacuee-01", ExpectedRevision = 0 });

                testShift.SetServerPosition("guide-b", "hall", new Point3(1f + 2.5f, 0f, 1f));
                var handoff = new WorldCommand { WorldId = "world-fmp10b", ShiftId = "shift-01", ParticipantId = "guide-a", TeamId = "team-alpha", CommandId = "h2", Kind = CommandKind.HandOffEvacuee, TargetId = "npc-evacuee-01", ExpectedRevision = 1, Argument = "guide-b" };
                var receipt = testShift.Submit("guide-a", handoff);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted), "Recipient distance at 2.50m (exact boundary) must be accepted");
                Assert.That(testShift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-evacuee-01").LeaderId, Is.EqualTo("guide-b"));
            }

            // Case 3: Recipient at 2.51m -> OutOfReach
            {
                var testSink = new TestCommitSink();
                var testShift = new AuthoritativeShift(CreateDefaultWorldState(), testSink, (_, __) => true);
                var cReceipt = testShift.Submit("guide-a", new WorldCommand { WorldId = "world-fmp10b", ShiftId = "shift-01", ParticipantId = "guide-a", TeamId = "team-alpha", CommandId = "c1", Kind = CommandKind.ClaimEvacuee, TargetId = "npc-evacuee-01", ExpectedRevision = 0 });
                Assert.That(cReceipt.Code, Is.EqualTo(CommandCode.Accepted), "Preparation claim must be accepted");

                testShift.SetServerPosition("guide-b", "hall", new Point3(1f + 2.5f + epsilon, 0f, 1f));
                var stateBefore = testShift.ExportCheckpoint();
                var writesBefore = testSink.Writes;

                var handoff = new WorldCommand { WorldId = "world-fmp10b", ShiftId = "shift-01", ParticipantId = "guide-a", TeamId = "team-alpha", CommandId = "h3", Kind = CommandKind.HandOffEvacuee, TargetId = "npc-evacuee-01", ExpectedRevision = 1, Argument = "guide-b" };
                var receipt = testShift.Submit("guide-a", handoff);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.OutOfReach), "Recipient distance at 2.51m must be rejected with OutOfReach");
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, testShift.ExportCheckpoint(), testSink.Writes, "npc-evacuee-01");

                var checkpoint = testShift.ExportCheckpoint();
                var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
                Assert.That(evacuee.LeaderId, Is.EqualTo("guide-a"), "Leader must remain guide-a");
                Assert.That(evacuee.Revision, Is.EqualTo(1));
                Assert.That(checkpoint.Sequence, Is.EqualTo(1));
            }
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
            Assert.That(retryReceipt.Sequence, Is.EqualTo(firstReceipt.Sequence));
            Assert.That(retryReceipt.Fingerprint, Is.EqualTo(firstReceipt.Fingerprint));
            Assert.That(sink.Writes, Is.EqualTo(writesAfterFirst), "Duplicate command must not commit new writes to storage");

            var checkpoint = shift.ExportCheckpoint();
            var evacuee = checkpoint.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(evacuee.Revision, Is.EqualTo(1), "Duplicate submission must not increment revision multiple times");
        }

        [Test]
        public void DuplicateCommand_SingleFieldMutationConflict_RejectsWithCommandIdConflict()
        {
            // Submit base command
            const string reuseId = "reuse-single-mutation-cmd";
            var baseCmd = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-free", commandId: reuseId, expectedRevision: 0, argument: "");
            var baseReceipt = shift.Submit("guide-a", baseCmd);
            Assert.That(baseReceipt.Code, Is.EqualTo(CommandCode.Accepted), "Initial base claim must be accepted");

            // Mutation 1: Only Kind modified
            {
                var stateBefore = shift.ExportCheckpoint();
                var writesBefore = sink.Writes;
                var modKind = MakeCommand("guide-a", CommandKind.Operate, "npc-evacuee-free", commandId: reuseId, expectedRevision: 0, argument: "");
                var recKind = shift.Submit("guide-a", modKind);
                Assert.That(recKind.Code, Is.EqualTo(CommandCode.CommandIdConflict), "Reusing CommandId with altered Kind must yield CommandIdConflict");
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-free");
            }

            // Mutation 2: Only TargetId modified
            {
                var stateBefore = shift.ExportCheckpoint();
                var writesBefore = sink.Writes;
                var modTarget = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: reuseId, expectedRevision: 0, argument: "");
                var recTarget = shift.Submit("guide-a", modTarget);
                Assert.That(recTarget.Code, Is.EqualTo(CommandCode.CommandIdConflict), "Reusing CommandId with altered TargetId must yield CommandIdConflict");
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-free");
                var evacuee01Before = stateBefore.Entities.Single(e => e.EntityId == "npc-evacuee-01");
                var evacuee01After = shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-evacuee-01");
                Assert.That(evacuee01After.LeaderId, Is.EqualTo(evacuee01Before.LeaderId), "LeaderId of altered target npc-evacuee-01 must remain unchanged");
                Assert.That(evacuee01After.Revision, Is.EqualTo(evacuee01Before.Revision), "Revision of altered target npc-evacuee-01 must remain unchanged");
            }

            // Mutation 3: Only ExpectedRevision modified
            {
                var stateBefore = shift.ExportCheckpoint();
                var writesBefore = sink.Writes;
                var modRev = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-free", commandId: reuseId, expectedRevision: 42, argument: "");
                var recRev = shift.Submit("guide-a", modRev);
                Assert.That(recRev.Code, Is.EqualTo(CommandCode.CommandIdConflict), "Reusing CommandId with altered ExpectedRevision must yield CommandIdConflict");
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-free");
            }

            // Mutation 4: Only Argument modified
            {
                var stateBefore = shift.ExportCheckpoint();
                var writesBefore = sink.Writes;
                var modArg = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-free", commandId: reuseId, expectedRevision: 0, argument: "unexpected-arg");
                var recArg = shift.Submit("guide-a", modArg);
                Assert.That(recArg.Code, Is.EqualTo(CommandCode.CommandIdConflict), "Reusing CommandId with altered Argument must yield CommandIdConflict");
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-free");
            }

            // Post-conflict retry: submitting the ORIGINAL command again returns the original cached receipt and fingerprint
            {
                var stateBefore = shift.ExportCheckpoint();
                var writesBefore = sink.Writes;
                var replayBase = shift.Submit("guide-a", baseCmd);
                Assert.That(replayBase.Code, Is.EqualTo(CommandCode.Accepted));
                Assert.That(replayBase.CommandId, Is.EqualTo(baseReceipt.CommandId));
                Assert.That(replayBase.Fingerprint, Is.EqualTo(baseReceipt.Fingerprint));
                Assert.That(replayBase.Sequence, Is.EqualTo(baseReceipt.Sequence));
                AssertReceiptLedgerAndStateEqual(stateBefore, writesBefore, shift.ExportCheckpoint(), sink.Writes, "npc-evacuee-free");
            }
        }

        [Test]
        public void DuplicateCommand_HandoffReplayAfterDownstreamTransfer_ReturnsOriginalReceiptWithoutRevertingOwnership()
        {
            // Step 1: guide-a claims (seq 1, rev 1, leader guide-a)
            var claimA = MakeCommand("guide-a", CommandKind.ClaimEvacuee, "npc-evacuee-01", commandId: "claim-cmd-a", expectedRevision: 0);
            var recClaimA = shift.Submit("guide-a", claimA);
            Assert.That(recClaimA.Code, Is.EqualTo(CommandCode.Accepted));

            // Step 2: guide-a hands off to guide-b with cmd-H (seq 2, rev 2, leader guide-b)
            var handoffAB = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", commandId: "cmd-handoff-a-to-b", expectedRevision: 1, argument: "guide-b");
            var recHandoffAB = shift.Submit("guide-a", handoffAB);
            Assert.That(recHandoffAB.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(recHandoffAB.Sequence, Is.EqualTo(2));

            // Step 3: guide-b hands off to guide-c (seq 3, rev 3, leader guide-c)
            var handoffBC = MakeCommand("guide-b", CommandKind.HandOffEvacuee, "npc-evacuee-01", commandId: "cmd-handoff-b-to-c", expectedRevision: 2, argument: "guide-c");
            var recHandoffBC = shift.Submit("guide-b", handoffBC);
            Assert.That(recHandoffBC.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(recHandoffBC.Sequence, Is.EqualTo(3));

            var writesBeforeReplay = sink.Writes;
            var stateBeforeReplay = shift.ExportCheckpoint();
            var seqBeforeReplay = stateBeforeReplay.Sequence;

            // Step 4: guide-a resubmits the ORIGINAL cmd-handoff-a-to-b
            var replayReceipt = shift.Submit("guide-a", handoffAB);

            // Assert exact identity with original receipt
            Assert.That(replayReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(replayReceipt.WorldId, Is.EqualTo(recHandoffAB.WorldId));
            Assert.That(replayReceipt.ShiftId, Is.EqualTo(recHandoffAB.ShiftId));
            Assert.That(replayReceipt.ParticipantId, Is.EqualTo(recHandoffAB.ParticipantId));
            Assert.That(replayReceipt.CommandId, Is.EqualTo(recHandoffAB.CommandId));
            Assert.That(replayReceipt.Fingerprint, Is.EqualTo(recHandoffAB.Fingerprint));
            Assert.That(replayReceipt.Sequence, Is.EqualTo(recHandoffAB.Sequence), "Replay receipt must reflect original Sequence 2");

            // Assert entire receipt ledger and entity state immutability across the cached replay
            var postReplayState = shift.ExportCheckpoint();
            AssertReceiptLedgerAndStateEqual(stateBeforeReplay, writesBeforeReplay, postReplayState, sink.Writes, "npc-evacuee-01");

            // Assert final state did NOT revert ownership to guide-b
            var finalEvacuee = postReplayState.Entities.Single(e => e.EntityId == "npc-evacuee-01");
            Assert.That(finalEvacuee.LeaderId, Is.EqualTo("guide-c"), "Replaying handoff A->B must NOT revert current leader guide-c");
            Assert.That(finalEvacuee.Revision, Is.EqualTo(3));
            Assert.That(postReplayState.Sequence, Is.EqualTo(seqBeforeReplay));
            Assert.That(sink.Writes, Is.EqualTo(writesBeforeReplay), "Replaying cached handoff must not commit a new write");

            // Contrast: If guide-a sends a NEW command ID now, it is evaluated against current state and rejected with RoleDenied
            var newHandoffByFormerLeader = MakeCommand("guide-a", CommandKind.HandOffEvacuee, "npc-evacuee-01", commandId: "new-cmd-by-former-leader", expectedRevision: 3, argument: "guide-b");
            var recFormer = shift.Submit("guide-a", newHandoffByFormerLeader);
            Assert.That(recFormer.Code, Is.EqualTo(CommandCode.RoleDenied), "New handoff command by former leader must be rejected with RoleDenied");
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
    }
}
