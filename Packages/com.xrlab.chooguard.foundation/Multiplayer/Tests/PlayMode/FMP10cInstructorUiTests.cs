using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-10c acceptance: instructor control UI, role-gated shift pause/resume,
    /// cryptographic admission gate without UI bypass, and instructor leave/recovery behavior.
    /// Commands and receipts cross the same JsonUtility encoding as multiplayer named-message channels.
    /// Runtime boundaries (SubmitPanelCommand, EvaluateConnectionApproval, Approve) are verified with failure injection.
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
        private readonly List<GameObject> createdObjects = new List<GameObject>();

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

        [TearDown]
        public void TearDown()
        {
            foreach (var go in createdObjects)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            createdObjects.Clear();
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

        private sealed class TestClientRuntimeScope
        {
            public NetworkFieldRuntime Runtime;
            public List<WorldCommand> SentCommands;
            public double Clock = 1000.0;
        }

        private TestClientRuntimeScope CreateTestClientRuntime(FieldCommandPanel panel, bool isInstructor, bool isPaused)
        {
            var go = new GameObject("TestClientRuntime_" + panel.ParticipantId);
            createdObjects.Add(go);
            var runtime = go.AddComponent<NetworkFieldRuntime>();
            runtime.connectedOverride = true;
            runtime.SetControlsForTest(true);
            runtime.SetCommandPanelForTest(panel);

            var scope = new TestClientRuntimeScope
            {
                Runtime = runtime,
                SentCommands = new List<WorldCommand>(),
                Clock = 1000.0
            };
            runtime.TimeProvider = () => scope.Clock;

            var view = Project(panel.ParticipantId);
            view.Observed.Paused = isPaused;
            view.Instructor = isInstructor;
            runtime.SetViewForTest(view);

            runtime.MessageSender = (channel, client, text) =>
            {
                if (channel == NetworkFieldRuntime.CommandChannelName)
                    scope.SentCommands.Add(JsonUtility.FromJson<WorldCommand>(text));
            };
            return scope;
        }

        private NetworkFieldRuntime CreateTestServerRuntime()
        {
            var go = new GameObject("TestServerRuntime");
            createdObjects.Add(go);
            var runtime = go.AddComponent<NetworkFieldRuntime>();
            runtime.SetAdmissionForTest(admission);
            runtime.SetShiftForTest(shift);
            return runtime;
        }

        // Acceptance 1: IsInstructor participant can build, submit, and track PauseShift and ResumeShift commands.
        // Also verifies runtime boundary SubmitPanelCommand enforcing snapshot freshness on resume.
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

            // 2. Runtime boundary verification for SubmitPanelCommand on ResumeShift
            var instScope = CreateTestClientRuntime(inst1, isInstructor: true, isPaused: true);

            // 2a. Failure injection: Stale snapshot prevents ResumeShift submission at runtime boundary
            instScope.Runtime.RecordSnapshotTimeForTest(1000.0);
            instScope.Clock = 1005.0; // 5 seconds later; exceeds MaximumAgeSeconds (0.5s)
            var staleResumeCmd = inst1.ResumeShift("cmd-inst-resume-stale");
            Assert.That(staleResumeCmd, Is.Not.Null);
            var staleSubmitted = instScope.Runtime.SubmitPanelCommand(staleResumeCmd);
            Assert.That(staleSubmitted, Is.False, "Stale snapshot must strictly prevent ResumeShift submission.");
            Assert.That(instScope.SentCommands.Any(c => c.CommandId == "cmd-inst-resume-stale"), Is.False,
                "Command must not be sent over network channel when snapshot is stale.");
            Assert.That(inst1.Pending.Any(p => p.CommandId == "cmd-inst-resume-stale"), Is.False,
                "Command must not be registered in pending tracking when snapshot is stale.");

            // 2b. Positive case: Fresh snapshot permits ResumeShift submission at runtime boundary
            instScope.Runtime.RecordSnapshotTimeForTest(1005.0);
            instScope.Clock = 1005.1; // 0.1s later; well within 0.5s threshold
            var freshResumeCmd = inst1.ResumeShift("cmd-inst-resume-fresh");
            var freshSubmitted = instScope.Runtime.SubmitPanelCommand(freshResumeCmd);
            Assert.That(freshSubmitted, Is.True, "Fresh snapshot allows instructor ResumeShift submission.");
            Assert.That(instScope.SentCommands.Any(c => c.CommandId == "cmd-inst-resume-fresh"), Is.True,
                "Command must be transmitted over CommandChannel.");
            Assert.That(inst1.Pending.Any(p => p.CommandId == "cmd-inst-resume-fresh"), Is.True,
                "Command must be registered in pending tracking.");

            // 3. Authoritative shift resumes upon valid receipt
            var resumeReceipt = Wire(shift.Submit(inst1.ParticipantId, freshResumeCmd));
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

            // Runtime boundary: non-instructor runtime cannot submit null pause/resume
            var a1Scope = CreateTestClientRuntime(a1, isInstructor: false, isPaused: false);
            Assert.That(a1Scope.Runtime.SubmitPanelCommand(a1.PauseShift()), Is.False);
            Assert.That(a1Scope.Runtime.SubmitPanelCommand(a1.ResumeShift()), Is.False);
            Assert.That(a1Scope.SentCommands, Is.Empty);

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

        // Acceptance 3: Admission allow/deny UI never bypasses SessionAdmission.Allows,
        // and instructor admission review is wired into server connection approval.
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

            var validTraineeCredB1 = new JoinCredential
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = "b1",
                Secret = SecretB1
            };

            // Part A: Panel-level decision tracking & evaluation
            // 1. Positive case: instructor approves and SessionAdmission allows
            Assert.That(admission.Allows(validTraineeCred), Is.True);
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: true), Is.True);

            // 2. Instructor decision tracking via SetAdmissionDecision
            Assert.That(inst1.SetAdmissionDecision("a1", true), Is.True);
            Assert.That(inst1.AdmissionDecisions["a1"], Is.True);
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, admission), Is.True);

            // 3. Instructor explicitly denies participant
            Assert.That(inst1.SetAdmissionDecision("a1", false), Is.True);
            Assert.That(inst1.AdmissionDecisions["a1"], Is.False);
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, admission), Is.False,
                "Instructor denial in panel must cause EvaluateAdmission to return false.");
            Assert.That(inst1.RenderLines(), Has.Some.EqualTo("입장 심사: a1 → 교관 거부"));

            // Reset back to approved
            inst1.SetAdmissionDecision("a1", true);
            Assert.That(inst1.RenderLines(), Has.Some.EqualTo("입장 심사: a1 → 교관 승인"));

            // 4. Non-instructor cannot record admission decisions
            Assert.That(a1.SetAdmissionDecision("b1", true), Is.False,
                "Non-instructor panel must reject SetAdmissionDecision.");
            Assert.That(a1.AdmissionDecisions, Is.Empty);
            Assert.That(a1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: true), Is.False,
                "Non-instructor participant cannot exercise admission review authority.");
            Assert.That(b1.EvaluateAdmission(validTraineeCred, admission, instructorApproved: true), Is.False);

            // 5. Bypass rejection: instructor approves, but credential carries incorrect secret
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

            // 6. Bypass rejection: instructor approves, but WorldId or ShiftId mismatches
            var wrongWorldCred = new JoinCredential
            {
                WorldId = "different-world",
                ShiftId = ShiftId,
                ParticipantId = "a1",
                Secret = SecretA1
            };
            Assert.That(admission.Allows(wrongWorldCred), Is.False);
            Assert.That(inst1.EvaluateAdmission(wrongWorldCred, admission, instructorApproved: true), Is.False);

            // Defensive null validation
            Assert.That(inst1.EvaluateAdmission(null, admission, true), Is.False);
            Assert.That(inst1.EvaluateAdmission(validTraineeCred, null, true), Is.False);

            // Part B: Distributed Client-Server Admission Wire Protocol Verification
            // Tests must never manually populate server admission dictionaries; decisions must travel over the wire.
            var serverRuntime = CreateTestServerRuntime();
            var instScope = CreateTestClientRuntime(inst1, isInstructor: true, isPaused: false);
            serverRuntime.RegisterIdentityForTest(1000, "inst1");

            // Wire transport setup: connects instructor client and server message handlers
            instScope.Runtime.MessageSender = (channel, client, text) =>
            {
                if (channel == NetworkFieldRuntime.AdmissionRequestChannel)
                    serverRuntime.ProcessAdmissionRequest(1000, text);
            };

            AdmissionReviewResponse lastTraineeResponse = null;
            serverRuntime.MessageSender = (channel, client, text) =>
            {
                if (channel == NetworkFieldRuntime.AdmissionResponseChannel)
                {
                    if (client == 1000)
                        instScope.Runtime.ProcessAdmissionResponse(text);
                    else if (client == 2000)
                        lastTraineeResponse = JsonUtility.FromJson<AdmissionReviewResponse>(text);
                }
            };

            // B1. Valid connection without denial succeeds
            byte[] validPayloadA1 = Encoding.UTF8.GetBytes(JsonUtility.ToJson(validTraineeCred));
            Assert.That(serverRuntime.EvaluateConnectionApproval(validTraineeCred, out var reasonOk), Is.True);
            Assert.That(reasonOk, Is.Empty);
            Assert.That(serverRuntime.TestApproveConnection(1001, validPayloadA1, out var appReasonOk), Is.True);
            Assert.That(appReasonOk, Is.Empty);

            // B2. Instructor client transmits admission denial over the wire
            // Verifies client RequestInstructorAdmission -> server ProcessAdmissionRequest -> server confirms -> client UI updates
            Assert.That(serverRuntime.InstructorAdmissionDecisions.ContainsKey("b1"), Is.False,
                "Server dictionary must not have b1 decision before wire transmission.");
            var denialRequested = instScope.Runtime.RequestInstructorAdmission("b1", false);
            Assert.That(denialRequested, Is.True, "Instructor client must successfully transmit admission request.");
            Assert.That(serverRuntime.InstructorAdmissionDecisions["b1"], Is.False,
                "Authoritative server dictionary must be updated via wire request processing.");
            Assert.That(inst1.AdmissionDecisions["b1"], Is.False,
                "Instructor client panel must be updated upon receiving server wire confirmation.");
            Assert.That(inst1.RenderLines(), Has.Some.EqualTo("입장 심사: b1 → 교관 거부"),
                "Instructor client UI lines must reflect confirmed server decision.");

            // Subsequent connection attempt with valid ticket is denied by server due to instructor decision
            byte[] validPayloadB1 = Encoding.UTF8.GetBytes(JsonUtility.ToJson(validTraineeCredB1));
            Assert.That(serverRuntime.EvaluateConnectionApproval(validTraineeCredB1, out var deniedReason), Is.False);
            Assert.That(deniedReason, Is.EqualTo("Admission denied by instructor"));
            Assert.That(serverRuntime.TestApproveConnection(1002, validPayloadB1, out var appDeniedReason), Is.False,
                "Instructor wire denial must cause server Approve to reject connection.");
            Assert.That(appDeniedReason, Is.EqualTo("Admission denied by instructor"));

            // B3. Non-instructor trainee client sending forged admission request is rejected with RoleDenied
            serverRuntime.RegisterIdentityForTest(2000, "a1");
            var forgedReq = new AdmissionReviewRequest
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                TargetParticipantId = "b1",
                Approved = true
            };
            var traineeProcessed = serverRuntime.ProcessAdmissionRequest(2000, JsonUtility.ToJson(forgedReq));
            Assert.That(traineeProcessed, Is.False, "Server must reject non-instructor admission review request.");
            Assert.That(lastTraineeResponse, Is.Not.Null);
            Assert.That(lastTraineeResponse.Confirmed, Is.False);
            Assert.That(lastTraineeResponse.Reason, Is.EqualTo("RoleDenied"));
            Assert.That(serverRuntime.InstructorAdmissionDecisions["b1"], Is.False,
                "Authoritative server dictionary must remain unaltered after non-instructor attempt.");

            // B4. Instructor client transmits approval over the wire; cryptographic validation is still never bypassed
            var approvalRequested = instScope.Runtime.RequestInstructorAdmission("b1", true);
            Assert.That(approvalRequested, Is.True);
            Assert.That(serverRuntime.InstructorAdmissionDecisions["b1"], Is.True,
                "Server dictionary updated to approved via wire request.");
            Assert.That(inst1.AdmissionDecisions["b1"], Is.True);
            Assert.That(inst1.RenderLines(), Has.Some.EqualTo("입장 심사: b1 → 교관 승인"));

            // Cryptographic check: even though instructor approved b1, bad secret is rejected
            var badSecretTicketB1 = new JoinCredential
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = "b1",
                Secret = "forged-secret-cannot-bypass"
            };
            byte[] badSecretPayloadB1 = Encoding.UTF8.GetBytes(JsonUtility.ToJson(badSecretTicketB1));
            Assert.That(serverRuntime.EvaluateConnectionApproval(badSecretTicketB1, out var cryptoDenialReason), Is.False);
            Assert.That(cryptoDenialReason, Is.EqualTo("Admission denied"));
            Assert.That(serverRuntime.TestApproveConnection(1003, badSecretPayloadB1, out var appCryptoDenialReason), Is.False,
                "Server Approve must never bypass SessionAdmission.Allows for instructor-approved participant with bad secret.");
            Assert.That(appCryptoDenialReason, Is.EqualTo("Admission denied"));

            // B5. Session mismatch request rejected by server
            var mismatchReq = new AdmissionReviewRequest
            {
                WorldId = "wrong-world",
                ShiftId = ShiftId,
                TargetParticipantId = "b1",
                Approved = true
            };
            var mismatchProcessed = serverRuntime.ProcessAdmissionRequest(1000, JsonUtility.ToJson(mismatchReq));
            Assert.That(mismatchProcessed, Is.False, "Session mismatch request must be rejected by server.");

            // B6. Duplicate participant and malformed payload rejected
            Assert.That(serverRuntime.TestApproveConnection(1004, validPayloadA1, out _), Is.False,
                "Duplicate participant already in server roster must be rejected.");
            Assert.That(serverRuntime.TestApproveConnection(1005, new byte[] { 0x00, 0x11, 0x22 }, out var badPayloadReason), Is.False);
            Assert.That(badPayloadReason, Is.EqualTo("Admission denied"));
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

        // Acceptance 5: Input-disabled instructor can still submit PauseShift and ResumeShift with fresh snapshot.
        [Test]
        public void InputDisabledInstructorCanStillSubmitPauseAndResume()
        {
            shift.SetInputEnabled("inst1", false);
            Assert.That(shift.Participant("inst1").InputEnabled, Is.False);

            // Authoritative shift level
            var pauseCmd = inst1.PauseShift("cmd-disabled-inst-pause");
            var receipt = Send(inst1, pauseCmd);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.True);

            var resumeCmd = inst1.ResumeShift("cmd-disabled-inst-resume");
            var resumeReceipt = Send(inst1, resumeCmd);
            Assert.That(resumeReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.False);

            // Runtime boundary level: controls disabled
            var instScope = CreateTestClientRuntime(inst1, isInstructor: true, isPaused: true);
            instScope.Runtime.SetControlsForTest(false);
            Assert.That(instScope.Runtime.LocalInputEnabled, Is.False, "LocalInputEnabled is false when controls are disabled.");

            // Instructor can pause even when local controls are disabled
            var runtimePause = inst1.PauseShift("cmd-runtime-disabled-pause");
            Assert.That(instScope.Runtime.SubmitPanelCommand(runtimePause), Is.True);
            Assert.That(instScope.SentCommands.Any(c => c.CommandId == "cmd-runtime-disabled-pause"), Is.True);

            // Instructor can resume when fresh, but NOT when stale
            instScope.Runtime.RecordSnapshotTimeForTest(1000.0);
            instScope.Clock = 1000.1;
            var runtimeResumeFresh = inst1.ResumeShift("cmd-runtime-disabled-resume-fresh");
            Assert.That(instScope.Runtime.SubmitPanelCommand(runtimeResumeFresh), Is.True);
            Assert.That(instScope.SentCommands.Any(c => c.CommandId == "cmd-runtime-disabled-resume-fresh"), Is.True);

            // Stale failure injection with disabled controls
            instScope.Clock = 1005.0; // 5 seconds later
            var runtimeResumeStale = inst1.ResumeShift("cmd-runtime-disabled-resume-stale");
            Assert.That(instScope.Runtime.SubmitPanelCommand(runtimeResumeStale), Is.False,
                "Stale snapshot must prevent resume even for instructor with disabled controls.");

            // But a non-instructor with disabled controls receives false from SubmitPanelCommand
            var a1Scope = CreateTestClientRuntime(a1, isInstructor: false, isPaused: true);
            a1Scope.Runtime.SetControlsForTest(false);
            var forged = Forged(a1, "team-a", CommandKind.PauseShift, "", "cmd-disabled-a1-pause");
            Assert.That(a1Scope.Runtime.SubmitPanelCommand(forged), Is.False);
            Assert.That(a1Scope.SentCommands, Is.Empty);

            // And non-instructor with disabled input in authoritative shift receives InputPaused
            shift.SetInputEnabled("a1", false);
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
        // Acceptance 7: Exact wire serialization byte accounting in Send without capacity inflation.
        [Test]
        public void SendByteAccountingAccumulatesExactWireLengthWithoutCapacityInflation()
        {
            var go = new GameObject("TestSendAccounting");
            createdObjects.Add(go);
            var runtime = go.AddComponent<NetworkFieldRuntime>();
            runtime.connectedOverride = true;
            runtime.SetControlsForTest(true);

            Assert.That(runtime.SentBytes, Is.EqualTo(0));

            // 1. ASCII payload
            var asciiText = "CHOOguard";
            var actualAsciiWriterLength = NetworkFieldRuntime.MeasureWireLength(asciiText);
            var oldCapacityAscii = Encoding.UTF8.GetByteCount(asciiText) * 2 + 16;

            var beforeAscii = runtime.SentBytes;
            runtime.SendForTest("test.channel", 1, asciiText);
            var asciiDelta = runtime.SentBytes - beforeAscii;

            Assert.That(asciiDelta, Is.EqualTo(actualAsciiWriterLength),
                "SentBytes delta must equal actual FastBufferWriter.Length for ASCII payload.");
            Assert.That(asciiDelta, Is.Not.EqualTo(oldCapacityAscii),
                "SentBytes must not be inflated by buffer allocation capacity.");

            // 2. Multi-byte payload (Korean characters)
            var koreanText = "근무정지";
            var actualKoreanWriterLength = NetworkFieldRuntime.MeasureWireLength(koreanText);
            var oldCapacityKorean = Encoding.UTF8.GetByteCount(koreanText) * 2 + 16;

            var beforeKorean = runtime.SentBytes;
            runtime.SendForTest("test.channel", 1, koreanText);
            var koreanDelta = runtime.SentBytes - beforeKorean;

            Assert.That(koreanDelta, Is.EqualTo(actualKoreanWriterLength),
                "SentBytes delta must equal actual FastBufferWriter.Length for Korean payload.");
            Assert.That(koreanDelta, Is.Not.EqualTo(oldCapacityKorean),
                "Zero capacity inflation allowed.");

            // 3. Integration with RuntimeMetricAccumulator
            var totalExpected = actualAsciiWriterLength + actualKoreanWriterLength;
            var accumulator = new RuntimeMetricAccumulator("client", 100.0, DateTime.UtcNow, 0, 0, 0, true, true);
            accumulator.Record(101.0, 16.0, runtime.SentBytes, 0, 1, 0, 0, 0, false, 0);
            var metricsRow = accumulator.Close(101.0, DateTime.UtcNow);
            Assert.That(metricsRow.SentPayloadBytes, Is.EqualTo(totalExpected),
                "RuntimeMetricAccumulator.SentPayloadBytes must reflect the exact sum of serialized wire bytes.");

            // 4. Verification with custom MessageSender delegate
            var intercepted = false;
            runtime.MessageSender = (ch, cl, txt) => { intercepted = true; };
            var mockText = "OK";
            var actualMockWriterLength = NetworkFieldRuntime.MeasureWireLength(mockText);
            var beforeMock = runtime.SentBytes;
            runtime.SendForTest("test.mock", 2, mockText);
            Assert.That(intercepted, Is.True);
            Assert.That(runtime.SentBytes - beforeMock, Is.EqualTo(actualMockWriterLength));
        }
    }
}
