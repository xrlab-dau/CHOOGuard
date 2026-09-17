using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-10c acceptance: instructor control UI, role-gated shift pause/resume,
    /// cryptographic admission gate without UI bypass, and instructor leave/recovery behavior.
    /// Commands and receipts cross the same JsonUtility encoding as multiplayer named-message channels.
    /// </summary>
    public sealed class FMP10cInstructorUiTests
    {
        private const string WorldId = "fmp10c-world", ShiftId = "fmp10c-shift";
        private const string SecretInst = "secret-instructor-key-alpha-99";
        private const string SecretA1 = "secret-trainee-a1-bravo-01";
        private const string SecretB1 = "secret-trainee-b1-charlie-02";

        private sealed class MemorySink : ICommitSink
        {
            public int Writes;
            public void Append(ShiftCommit commit) => Writes++;
        }

        private MemorySink sink;
        private AuthoritativeShift shift;
        private SessionAdmission admission;
        private FieldCommandPanel inst1, a1, b1;
        private readonly List<string> receiptLog = new List<string>();

        [SetUp]
        public void SetUp()
        {
            sink = new MemorySink();
            receiptLog.Clear();

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[]
                {
                    new ParticipantState
                    {
                        ParticipantId = "inst1", TeamId = "control", RoleId = "instructor-lead",
                        IsInstructor = true, RegionId = "ops-center", Position = new Point3(0, 0, 0)
                    },
                    new ParticipantState
                    {
                        ParticipantId = "a1", TeamId = "team-a", RoleId = "trainee-responder",
                        IsInstructor = false, RegionId = "platform-1", Position = new Point3(10, 0, 0)
                    },
                    new ParticipantState
                    {
                        ParticipantId = "b1", TeamId = "team-b", RoleId = "trainee-station",
                        IsInstructor = false, RegionId = "hall-concourse", Position = new Point3(20, 0, 0)
                    }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "incident-1", RegionId = "platform-1", Kind = EntityKind.Incident, Position = new Point3(10.5f, 0, 0), Active = true },
                    new EntityState { EntityId = "evacuee-1", RegionId = "platform-1", Kind = EntityKind.Evacuee, Position = new Point3(11, 0, 0) }
                }
            };

            shift = new AuthoritativeShift(world, sink, (actor, target) => true);

            admission = new SessionAdmission(new ServerSessionConfig
            {
                World = world,
                Tickets = new[]
                {
                    new AdmissionTicket { ParticipantId = "inst1", SecretSha256 = SessionAdmission.Digest(SecretInst) },
                    new AdmissionTicket { ParticipantId = "a1", SecretSha256 = SessionAdmission.Digest(SecretA1) },
                    new AdmissionTicket { ParticipantId = "b1", SecretSha256 = SessionAdmission.Digest(SecretB1) }
                }
            });

            inst1 = new FieldCommandPanel("inst1");
            a1 = new FieldCommandPanel("a1");
            b1 = new FieldCommandPanel("b1");
            Refresh(inst1, a1, b1);
        }

        private static T Wire<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        private FieldView Project(string participantId)
        {
            var actor = shift.Participant(participantId);
            return Wire(new FieldView
            {
                Observed = shift.Observe(participantId),
                Position = actor.Position,
                TeamId = actor.TeamId,
                RoleId = actor.RoleId,
                Instructor = actor.IsInstructor,
                RegionId = actor.RegionId,
                FrameId = actor.FrameId,
                LocalPosition = actor.LocalPosition
            });
        }

        private void Refresh(params FieldCommandPanel[] panels)
        {
            foreach (var panel in panels) panel.ApplyView(Project(panel.ParticipantId));
        }

        private CommandReceipt Send(FieldCommandPanel panel, WorldCommand command)
        {
            Assert.That(command, Is.Not.Null, "Panel must produce a well-formed command.");
            var wire = Wire(command);
            panel.Track(command);
            var receipt = Wire(shift.Submit(panel.ParticipantId, wire));
            receiptLog.Add(panel.ParticipantId + ":" + FieldCommandPanel.KindLabel(command.Kind) + ":" + command.TargetId + ":" + receipt.Code);
            return receipt;
        }

        private static WorldCommand Forged(FieldCommandPanel panel, string teamId, CommandKind kind, string targetId, string commandId, long revision = 0, string argument = "") =>
            new WorldCommand
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = panel.ParticipantId,
                TeamId = teamId,
                CommandId = commandId,
                Kind = kind,
                TargetId = targetId,
                ExpectedRevision = revision,
                Argument = argument
            };

        // Acceptance 1: IsInstructor participant can build, submit, and track PauseShift and ResumeShift commands.
        [Test]
        public void InstructorPanelCanBuildAndSubmitPauseShiftAndResumeShiftCommands()
        {
            Assert.That(inst1.IsInstructor, Is.True);
            Assert.That(a1.IsInstructor, Is.False);
            Assert.That(shift.Paused, Is.False);

            // 1. Instructor pauses the shift
            var pauseCmd = inst1.PauseShift("cmd-inst-pause-1");
            Assert.That(pauseCmd, Is.Not.Null);
            Assert.That(pauseCmd.Kind, Is.EqualTo(CommandKind.PauseShift));
            Assert.That(pauseCmd.ParticipantId, Is.EqualTo("inst1"));
            Assert.That(pauseCmd.WorldId, Is.EqualTo(WorldId));
            Assert.That(pauseCmd.ShiftId, Is.EqualTo(ShiftId));

            var pauseReceipt = Send(inst1, pauseCmd);
            Assert.That(pauseReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.True, "Server authority reflects paused state.");
            Assert.That(inst1.ApplyReceipt(pauseReceipt), Is.True);

            Assert.That(inst1.Results.Single().Accepted, Is.True);
            Assert.That(inst1.Results.Single().Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(inst1.Results.Single().Text, Is.EqualTo("서버 승인: 근무정지"));

            Refresh(inst1, a1, b1);
            Assert.That(inst1.RenderLines(), Has.Some.EqualTo("교관 제어: 근무 정지됨 (복구 승인 대기)"));
            Assert.That(a1.RenderLines(), Has.None.Matches("교관 제어:"));

            // While paused, normal operational command from participant is refused as ShiftPaused
            var claimCmd = Forged(a1, "team-a", CommandKind.ClaimEvacuee, "evacuee-1", "cmd-a1-claim-paused");
            var claimReceipt = Send(a1, claimCmd);
            Assert.That(claimReceipt.Code, Is.EqualTo(CommandCode.ShiftPaused));
            Assert.That(a1.ApplyReceipt(claimReceipt), Is.True);
            Assert.That(a1.Results.Single().Text, Is.EqualTo("서버 거부(ShiftPaused): 인솔 evacuee-1"));

            // 2. Instructor resumes the shift
            var resumeCmd = inst1.ResumeShift("cmd-inst-resume-1");
            Assert.That(resumeCmd, Is.Not.Null);
            Assert.That(resumeCmd.Kind, Is.EqualTo(CommandKind.ResumeShift));
            Assert.That(resumeCmd.ParticipantId, Is.EqualTo("inst1"));

            var resumeReceipt = Send(inst1, resumeCmd);
            Assert.That(resumeReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.False, "Server authority reflects resumed state.");
            Assert.That(inst1.ApplyReceipt(resumeReceipt), Is.True);

            Assert.That(inst1.Results.Last().Accepted, Is.True);
            Assert.That(inst1.Results.Last().Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(inst1.Results.Last().Text, Is.EqualTo("서버 승인: 근무재개"));

            Refresh(inst1, a1, b1);
            Assert.That(inst1.RenderLines(), Has.Some.EqualTo("교관 제어: 정상 근무 진행 중"));
        }

        // Acceptance 2: Non-instructor participants cannot build Pause/Resume commands, and forged attempts record RoleDenied.
        [Test]
        public void NonInstructorParticipantCannotBuildPauseOrResumeAndForgedCommandsRecordedAsRoleDenied()
        {
            Assert.That(a1.IsInstructor, Is.False);
            Assert.That(b1.IsInstructor, Is.False);

            // Client UI panel level: regular participants cannot build pause or resume commands
            Assert.That(a1.PauseShift(), Is.Null, "Non-instructor panel returns null for PauseShift.");
            Assert.That(a1.ResumeShift(), Is.Null, "Non-instructor panel returns null for ResumeShift.");
            Assert.That(b1.PauseShift(), Is.Null);
            Assert.That(b1.ResumeShift(), Is.Null);

            // Forged wire attempt for PauseShift by non-instructor participant a1
            var forgedPause = Forged(a1, "team-a", CommandKind.PauseShift, "", "cmd-forged-pause-a1");
            var receiptPause = Send(a1, forgedPause);

            Assert.That(receiptPause.Code, Is.EqualTo(CommandCode.RoleDenied), "Server strictly rejects non-instructor PauseShift.");
            Assert.That(shift.Paused, Is.False, "Shift must remain running.");
            Assert.That(a1.ApplyReceipt(receiptPause), Is.True);

            Assert.That(a1.Results.Single().Accepted, Is.False);
            Assert.That(a1.Results.Single().Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(a1.Results.Single().Text, Is.EqualTo("서버 거부(RoleDenied): 근무정지"));
            Assert.That(a1.RenderLines(), Has.Some.EqualTo("서버 거부(RoleDenied): 근무정지"));

            // Pause shift via instructor first to test ResumeShift negative case
            var validPause = Send(inst1, inst1.PauseShift("cmd-inst-pause-2"));
            Assert.That(validPause.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.True);
            Refresh(a1);

            // Forged wire attempt for ResumeShift by non-instructor participant a1
            var forgedResume = Forged(a1, "team-a", CommandKind.ResumeShift, "", "cmd-forged-resume-a1");
            var receiptResume = Send(a1, forgedResume);

            Assert.That(receiptResume.Code, Is.EqualTo(CommandCode.RoleDenied), "Server strictly rejects non-instructor ResumeShift.");
            Assert.That(shift.Paused, Is.True, "Shift must remain paused.");
            Assert.That(a1.ApplyReceipt(receiptResume), Is.True);

            Assert.That(a1.Results.Last().Accepted, Is.False);
            Assert.That(a1.Results.Last().Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(a1.Results.Last().Text, Is.EqualTo("서버 거부(RoleDenied): 근무재개"));
            Assert.That(a1.RenderLines(), Has.Some.EqualTo("서버 거부(RoleDenied): 근무재개"));
        }

        // Acceptance 3: Admission allow/deny UI never bypasses SessionAdmission.Allows.
        [Test]
        public void AdmissionGateRequiresBothInstructorRoleAndSessionAdmissionAllowsWithoutBypass()
        {
            var validTraineeCred = new JoinCredential
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = "a1",
                Secret = SecretA1
            };

            // 1. Positive case: instructor approves and SessionAdmission allows
            Assert.That(admission.Allows(validTraineeCred), Is.True);
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: true), Is.True);

            // 2. Bypass rejection: instructor approves, but credential carries incorrect secret
            var wrongSecretCred = new JoinCredential
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = "a1",
                Secret = "forged-or-mistyped-secret"
            };
            Assert.That(admission.Allows(wrongSecretCred), Is.False);
            Assert.That(inst1.EvaluateAdmission(wrongSecretCred, admission, instructorApproved: true), Is.False,
                "Instructor approval cannot bypass cryptographic admission validation.");

            // 3. Bypass rejection: instructor approves, but WorldId or ShiftId mismatches
            var wrongWorldCred = new JoinCredential
            {
                WorldId = "different-world",
                ShiftId = ShiftId,
                ParticipantId = "a1",
                Secret = SecretA1
            };
            Assert.That(admission.Allows(wrongWorldCred), Is.False);
            Assert.That(inst1.EvaluateAdmission(wrongWorldCred, admission, instructorApproved: true), Is.False);

            // 4. Role gate rejection: non-instructor attempts to evaluate/grant admission
            Assert.That(a1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: true), Is.False,
                "Non-instructor participant cannot exercise admission review authority.");
            Assert.That(b1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: true), Is.False);

            // 5. Instructor disapproval rejection: valid ticket rejected if instructor rejects
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: false), Is.False);

            // 6. Defensive null validation
            Assert.That(inst1.EvaluateAdmission(null, admission, true), Is.False);
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, null, true), Is.False);
        }

        // Acceptance 4: Instructor leave/restart behavior follows #99 recovery authority; paused on restore.
        [Test]
        public void InstructorDisconnectAndRecoveryRestartRequireExplicitResumption()
        {
            // 1. Disconnect behavior: instructor input is disabled upon leaving
            shift.SetInputEnabled("inst1", false);
            var instActor = shift.Participant("inst1");
            Assert.That(instActor.InputEnabled, Is.False, "Leaving participant has input disabled.");

            // Roster immutability: participant a1 is NOT automatically promoted to instructor
            var a1Actor = shift.Participant("a1");
            Assert.That(a1Actor.IsInstructor, Is.False, "Participant roles are immutable; no unauthorized auto-promotion.");

            // 2. Checkpoint recovery simulation per #99 recovery contract:
            var checkpoint = shift.ExportCheckpoint();
            var recoverySink = new MemorySink();
            var recoveredShift = AuthoritativeShift.Restore(checkpoint, recoverySink, (actor, target) => true);

            // Invariant: recovered shift starts in Paused state by construction
            Assert.That(recoveredShift.Paused, Is.True, "Recovered shift must remain paused until explicit instructor resumption.");

            // Ordinary commands fail on recovered paused shift
            var traineeCmd = Forged(a1, "team-a", CommandKind.ClaimEvacuee, "evacuee-1", "cmd-a1-recovered-fail");
            var traineeReceipt = recoveredShift.Submit("a1", Wire(traineeCmd));
            Assert.That(traineeReceipt.Code, Is.EqualTo(CommandCode.ShiftPaused));

            // Non-instructor cannot resume the recovered shift
            var traineeResume = Forged(a1, "team-a", CommandKind.ResumeShift, "", "cmd-a1-recovered-resume");
            var traineeResumeReceipt = recoveredShift.Submit("a1", Wire(traineeResume));
            Assert.That(traineeResumeReceipt.Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(recoveredShift.Paused, Is.True);

            // Only re-admitted instructor can resume recovered shift
            var instResume = Forged(inst1, "control", CommandKind.ResumeShift, "", "cmd-inst-recovered-resume");
            var instResumeReceipt = recoveredShift.Submit("inst1", Wire(instResume));
            Assert.That(instResumeReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(recoveredShift.Paused, Is.False, "Recovered shift is resumed only upon authoritative instructor command.");
        }

        // Acceptance 5: Input-disabled instructor can still submit PauseShift and ResumeShift.
        [Test]
        public void InputDisabledInstructorCanStillSubmitPauseAndResume()
        {
            shift.SetInputEnabled("inst1", false);
            Assert.That(shift.Participant("inst1").InputEnabled, Is.False);

            // Instructor can pause even when normal input is disabled
            var pauseCmd = inst1.PauseShift("cmd-disabled-inst-pause");
            var receipt = Send(inst1, pauseCmd);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.True);

            // Instructor can resume even when normal input is disabled
            var resumeCmd = inst1.ResumeShift("cmd-disabled-inst-resume");
            var resumeReceipt = Send(inst1, resumeCmd);
            Assert.That(resumeReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.False);

            // But a non-instructor with disabled input receives InputPaused
            shift.SetInputEnabled("a1", false);
            var forged = Forged(a1, "team-a", CommandKind.PauseShift, "", "cmd-disabled-a1-pause");
            var a1Receipt = Send(a1, forged);
            Assert.That(a1Receipt.Code, Is.EqualTo(CommandCode.InputPaused));
        }

        // Acceptance 6: Receipt ledger immutability and bounded result capacity.
        [Test]
        public void ReceiptTrackingAndResultLedgerIntegrityAcrossMultipleCommands()
        {
            for (var i = 0; i < 10; i++)
            {
                var isPause = i % 2 == 0;
                var cmd = isPause ? inst1.PauseShift("seq-cmd-" + i) : inst1.ResumeShift("seq-cmd-" + i);
                var receipt = Send(inst1, cmd);
                Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
                Assert.That(inst1.ApplyReceipt(receipt), Is.True);
            }

            Assert.That(inst1.Results.Length, Is.EqualTo(FieldCommandPanel.ResultCapacity),
                "Panel results capacity remains strictly bounded.");
            Assert.That(inst1.Pending, Is.Empty, "All tracked commands resolved.");

            // Mismatched participant receipt is rejected by panel
            var foreignReceipt = new CommandReceipt
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = "other-client",
                CommandId = "foreign-cmd",
                Code = CommandCode.Accepted,
                Sequence = 999
            };
            Assert.That(inst1.ApplyReceipt(foreignReceipt), Is.False);
            Assert.That(inst1.IgnoredReceipts, Is.GreaterThan(0));
        }
    }
}
