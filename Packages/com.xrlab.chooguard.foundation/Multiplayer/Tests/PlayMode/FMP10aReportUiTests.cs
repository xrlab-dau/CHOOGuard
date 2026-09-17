using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-10a candidate acceptance: the client report / acknowledgement / escort / hand-off panel shows only
    /// state the real <see cref="AuthoritativeShift"/> approved. Commands, receipts and projected views cross the
    /// same JsonUtility encoding the named-message channels use. Teams, roles and positions are synthetic fixtures,
    /// not an official procedure, and no network transport or second process is involved.
    /// </summary>
    public sealed class FMP10aReportUiTests
    {
        private const string WorldId = "fmp10a-world", ShiftId = "fmp10a-shift";
        private const string TestSourcePath = "Packages/com.xrlab.chooguard.foundation/Multiplayer/Tests/PlayMode/FMP10aReportUiTests.cs";
        private static readonly string[] BoundSources =
        {
            "Packages/com.xrlab.chooguard.foundation/Multiplayer/Runtime/NetworkFieldRuntime.cs",
            "Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs",
            "Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/WorldContracts.cs",
            TestSourcePath
        };

        private sealed class MemorySink : ICommitSink
        {
            public int Writes;
            public void Append(ShiftCommit commit) => Writes++;
        }

        private MemorySink sink;
        private AuthoritativeShift shift;
        private FieldCommandPanel a1, a2, b1;
        private readonly List<string> receiptLog = new List<string>();

        [SetUp]
        public void SetUp()
        {
            sink = new MemorySink();
            receiptLog.Clear();
            shift = new AuthoritativeShift(new WorldState
            {
                WorldId = WorldId, ShiftId = ShiftId,
                Participants = new[]
                {
                    new ParticipantState { ParticipantId = "a1", TeamId = "team-a", RoleId = "synthetic-role-1", RegionId = "hall", Position = new Point3(0, 0, 0) },
                    new ParticipantState { ParticipantId = "a2", TeamId = "team-a", RoleId = "synthetic-role-1", RegionId = "hall", Position = new Point3(1, 0, 0) },
                    new ParticipantState { ParticipantId = "b1", TeamId = "team-b", RoleId = "synthetic-role-2", RegionId = "hall", Position = new Point3(0, 0, 1) }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "incident-1", RegionId = "hall", Kind = EntityKind.Incident, Position = new Point3(.5f, 0, 0) },
                    new EntityState { EntityId = "evacuee-1", RegionId = "hall", Kind = EntityKind.Evacuee, Position = new Point3(.5f, 0, .5f) },
                    new EntityState { EntityId = "incident-far", RegionId = "hall", Kind = EntityKind.Incident, Position = new Point3(40, 0, 0) },
                    new EntityState { EntityId = "evacuee-far", RegionId = "hall", Kind = EntityKind.Evacuee, Position = new Point3(40, 0, 1) }
                }
            }, sink, (actor, target) => true);
            a1 = new FieldCommandPanel("a1"); a2 = new FieldCommandPanel("a2"); b1 = new FieldCommandPanel("b1");
            Refresh(a1, a2, b1);
        }

        private static T Wire<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        // Same fields NetworkFieldRuntime.SendView projects for a non-physical world.
        private FieldView Project(string participantId)
        {
            var actor = shift.Participant(participantId);
            return Wire(new FieldView { Observed = shift.Observe(participantId), Position = actor.Position, TeamId = actor.TeamId,
                RoleId = actor.RoleId, Instructor = actor.IsInstructor, RegionId = actor.RegionId, FrameId = actor.FrameId,
                LocalPosition = actor.LocalPosition });
        }

        private void Refresh(params FieldCommandPanel[] panels) { foreach (var panel in panels) panel.ApplyView(Project(panel.ParticipantId)); }

        /// <summary>Client sends, the server authority decides, and the receipt travels back.</summary>
        private CommandReceipt Send(FieldCommandPanel panel, WorldCommand command)
        {
            Assert.That(command, Is.Not.Null, "the panel must be able to build this command from its current server view");
            var wire = Wire(command);
            panel.Track(command);
            var receipt = Wire(shift.Submit(panel.ParticipantId, wire));
            receiptLog.Add(panel.ParticipantId + ":" + FieldCommandPanel.KindLabel(command.Kind) + ":" + command.TargetId + ":" + receipt.Code);
            return receipt;
        }

        private static WorldCommand Forged(FieldCommandPanel panel, string teamId, CommandKind kind, string target, string id, long revision = 0, string argument = "") =>
            new WorldCommand { WorldId = WorldId, ShiftId = ShiftId, ParticipantId = panel.ParticipantId, TeamId = teamId,
                CommandId = id, Kind = kind, TargetId = target, ExpectedRevision = revision, Argument = argument };

        private string ReportFromA1(string recipient = "", string id = "cmd-report-1")
        {
            Assert.That(shift.DiscoverNearby("a1", _ => true), Is.EqualTo(1), "server discovery of the nearby incident");
            Refresh(a1, a2, b1);
            var receipt = Send(a1, a1.Report("incident-1", recipient, id));
            Assert.That(a1.ApplyReceipt(receipt), Is.True);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
            Refresh(a1, a2, b1);
            return "report-" + receipt.Sequence;
        }

        // Acceptance 1: report submit sends TeamReport entity/observed revision fields and shows the server result.
        [Test]
        public void ReportSubmitSendsObservedFieldsAndShowsServerResult()
        {
            Assert.That(a1.ReportableIncidentIds, Is.Empty, "an undiscovered incident is not in the projected view");
            Assert.That(a1.Report("incident-1"), Is.Null, "no command for an entity the server has not shown");
            Assert.That(shift.DiscoverNearby("a1", _ => true), Is.EqualTo(1));
            Refresh(a1);
            CollectionAssert.AreEqual(new[] { "incident-1" }, a1.ReportableIncidentIds);

            var observed = Project("a1").Observed.Entities.Single(e => e.EntityId == "incident-1");
            var command = a1.Report("incident-1", "", "cmd-report-1");
            Assert.That(command.Kind, Is.EqualTo(CommandKind.Report));
            Assert.That(command.TargetId, Is.EqualTo(observed.EntityId));
            Assert.That(command.ExpectedRevision, Is.EqualTo(observed.Revision), "observed revision travels in the payload");
            Assert.That(command.Argument, Is.EqualTo(""), "empty recipient means the reporter's own team");
            Assert.That(command.ParticipantId, Is.EqualTo("a1"));
            Assert.That(command.TeamId, Is.EqualTo("team-a"));
            Assert.That(command.WorldId, Is.EqualTo(WorldId));
            Assert.That(command.ShiftId, Is.EqualTo(ShiftId));

            var wire = Wire(command);
            a1.Track(command);
            Assert.That(a1.Pending, Has.Length.EqualTo(1));
            Assert.That(a1.Pending[0].RegionId, Is.EqualTo(observed.RegionId), "observed region is kept with the sent command");
            Assert.That(a1.RenderLines(), Has.Some.EqualTo("서버 확인 대기: 보고 incident-1"));
            Assert.That(a1.Results, Is.Empty, "no result before the server answers");
            var receipt = Wire(shift.Submit("a1", wire));
            Assert.That(a1.Reports, Is.Empty, "no optimistic report row before a server view");
            Assert.That(a1.ApplyReceipt(receipt), Is.True);
            Assert.That(a1.Pending, Is.Empty);
            Assert.That(a1.Results.Single().Accepted, Is.True);
            Assert.That(a1.Results.Single().Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(a1.Results.Single().Text, Is.EqualTo("서버 승인: 보고 incident-1"));

            Refresh(a1, a2, b1);
            var authoritative = shift.ExportCheckpoint().Reports.Single();
            foreach (var panel in new[] { a1, a2 })
            {
                var row = panel.Reports.Single();
                Assert.That(row.ReportId, Is.EqualTo(authoritative.ReportId).And.EqualTo("report-" + receipt.Sequence), panel.ParticipantId);
                Assert.That(row.EntityId, Is.EqualTo(command.TargetId), panel.ParticipantId);
                Assert.That(row.RegionId, Is.EqualTo(observed.RegionId).And.EqualTo(authoritative.RegionId), panel.ParticipantId);
                Assert.That(row.ObservedRevision, Is.EqualTo(command.ExpectedRevision).And.EqualTo(authoritative.ObservedRevision), panel.ParticipantId);
                Assert.That(row.FromParticipantId, Is.EqualTo("a1"), panel.ParticipantId);
                Assert.That(row.ToTeamId, Is.EqualTo("team-a"), panel.ParticipantId);
            }
            Assert.That(b1.Reports, Is.Empty, "the other team never receives the row");

            // A rejected report is shown as the server's rejection and creates no row.
            Assert.That(a1.Report("incident-far"), Is.Null, "out-of-view incident cannot be reported from the panel");
            var rejected = Send(a1, Forged(a1, "team-a", CommandKind.Report, "incident-far", "cmd-report-far"));
            Assert.That(a1.ApplyReceipt(rejected), Is.True);
            Assert.That(rejected.Code, Is.EqualTo(CommandCode.NotObserved));
            Assert.That(a1.Results.Last().Accepted, Is.False);
            Assert.That(a1.Results.Last().Text, Is.EqualTo("서버 거부(NotObserved): 보고 incident-far"));
            Refresh(a1);
            Assert.That(a1.Reports, Has.Length.EqualTo(1));
            Assert.That(sink.Writes, Is.EqualTo(2), "discover + one accepted report were committed");
        }

        // Acceptance 2: acknowledgement UI shows the same-team report and AcknowledgedBy from the actual response.
        [Test]
        public void AcknowledgeShowsSameTeamReportAndServerAcknowledgedBy()
        {
            var reportId = ReportFromA1();
            Assert.That(a2.Reports.Single().ReportId, Is.EqualTo(reportId));
            Assert.That(a2.Reports.Single().AcknowledgedBy, Is.Empty);
            Assert.That(a2.Reports.Single().AcknowledgedByLocal, Is.False);

            var command = a2.AcknowledgeNext("cmd-ack-a2");
            Assert.That(command.Kind, Is.EqualTo(CommandKind.AcknowledgeReport));
            Assert.That(command.TargetId, Is.EqualTo(reportId));
            var receipt = Send(a2, command);
            Assert.That(a2.ApplyReceipt(receipt), Is.True);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(a2.Reports.Single().AcknowledgedBy, Is.Empty, "acknowledgement appears only through a server view");
            Assert.That(a2.Results.Single().Text, Is.EqualTo("서버 승인: 수신확인 " + reportId));

            Refresh(a1, a2, b1);
            var authoritative = shift.ExportCheckpoint().Reports.Single(r => r.ReportId == reportId).AcknowledgedBy;
            CollectionAssert.AreEqual(new[] { "a2" }, authoritative);
            CollectionAssert.AreEqual(authoritative, a2.Reports.Single().AcknowledgedBy);
            CollectionAssert.AreEqual(authoritative, a1.Reports.Single().AcknowledgedBy, "the reporter sees who acknowledged");
            Assert.That(a2.Reports.Single().AcknowledgedByLocal, Is.True);
            Assert.That(a1.Reports.Single().AcknowledgedByLocal, Is.False);
            Assert.That(a2.AcknowledgeNext(), Is.Null, "nothing left to acknowledge for a2");
            Assert.That(a1.AcknowledgeNext(), Is.Not.Null);

            // Other team: no row, no panel command, and a forged acknowledgement is displayed as rejected.
            Assert.That(b1.Reports, Is.Empty);
            Assert.That(b1.Acknowledge(reportId), Is.Null);
            var forged = Send(b1, Forged(b1, "team-b", CommandKind.AcknowledgeReport, reportId, "cmd-ack-b1"));
            Assert.That(b1.ApplyReceipt(forged), Is.True);
            Assert.That(forged.Code, Is.EqualTo(CommandCode.UnknownTarget));
            Assert.That(b1.Results.Single().Accepted, Is.False);
            Refresh(a1, b1);
            Assert.That(b1.Reports, Is.Empty);
            CollectionAssert.AreEqual(new[] { "a2" }, a1.Reports.Single().AcknowledgedBy);
        }

        // Acceptance 3: claim/hand-off UI shows accepted and rejected race results and the server target state.
        [Test]
        public void ClaimAndHandOffShowRaceResultAndServerTargetState()
        {
            Assert.That(a1.Evacuees.Select(e => e.EntityId), Is.EqualTo(new[] { "evacuee-1" }), "far evacuee is outside the projected view");
            var first = a1.Claim("evacuee-1", "cmd-claim-a1");
            var second = a2.Claim("evacuee-1", "cmd-claim-a2");
            Assert.That(first.ExpectedRevision, Is.EqualTo(0));
            Assert.That(second.ExpectedRevision, Is.EqualTo(0), "both clients built the claim from the same server view");

            var won = Send(a1, first);
            var lost = Send(a2, second);
            Assert.That(won.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(lost.Code, Is.EqualTo(CommandCode.StaleTarget));
            Assert.That(a1.ApplyReceipt(won) && a2.ApplyReceipt(lost), Is.True);
            Assert.That(a1.Evacuees.Single().Unclaimed && a2.Evacuees.Single().Unclaimed, Is.True, "no optimistic leader before a server view");
            Assert.That(a1.Results.Single().Text, Is.EqualTo("서버 승인: 인솔 evacuee-1"));
            Assert.That(a2.Results.Single().Text, Is.EqualTo("서버 거부(StaleTarget): 인솔 evacuee-1"));

            Refresh(a1, a2, b1);
            foreach (var panel in new[] { a1, a2, b1 })
            {
                Assert.That(panel.Evacuees.Single().LeaderId, Is.EqualTo("a1"), panel.ParticipantId);
                Assert.That(panel.Evacuees.Single().Revision, Is.EqualTo(1), panel.ParticipantId);
            }
            Assert.That(a1.Evacuees.Single().LedByLocal, Is.True);
            Assert.That(a2.Evacuees.Single().LedByLocal || a2.Evacuees.Single().Unclaimed, Is.False);
            Assert.That(a2.Claim("evacuee-1"), Is.Null, "the panel offers no claim on a led evacuee");

            var retry = Send(a2, Forged(a2, "team-a", CommandKind.ClaimEvacuee, "evacuee-1", "cmd-claim-a2-retry", 1));
            Assert.That(a2.ApplyReceipt(retry), Is.True);
            Assert.That(retry.Code, Is.EqualTo(CommandCode.AlreadyClaimed));
            Assert.That(a2.Results.Last().Text, Is.EqualTo("서버 거부(AlreadyClaimed): 인솔 evacuee-1"));

            Assert.That(a2.HandOffLed("a1"), Is.Null, "only the server-shown leader can hand off");
            Assert.That(a1.HandOffLed("a1"), Is.Null, "hand-off to self is not offered");
            var handOff = a1.HandOffLed("a2", "cmd-handoff-a1");
            Assert.That(handOff.Kind, Is.EqualTo(CommandKind.HandOffEvacuee));
            Assert.That(handOff.TargetId, Is.EqualTo("evacuee-1"));
            Assert.That(handOff.Argument, Is.EqualTo("a2"));
            Assert.That(handOff.ExpectedRevision, Is.EqualTo(1));
            var handed = Send(a1, handOff);
            Assert.That(a1.ApplyReceipt(handed), Is.True);
            Assert.That(handed.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(a1.Results.Last().Text, Is.EqualTo("서버 승인: 인계 evacuee-1 → a2"));
            Assert.That(a1.Evacuees.Single().LedByLocal, Is.True, "leader changes only through a server view");

            Refresh(a1, a2, b1);
            Assert.That(a2.Evacuees.Single().LedByLocal, Is.True);
            Assert.That(a1.Evacuees.Single().LedByLocal, Is.False);
            Assert.That(b1.Evacuees.Single().LeaderId, Is.EqualTo("a2"));
            Assert.That(a2.Evacuees.Single().Revision, Is.EqualTo(2));

            var stale = Send(a1, Forged(a1, "team-a", CommandKind.HandOffEvacuee, "evacuee-1", "cmd-handoff-a1-again", 2, "b1"));
            var outsider = Send(b1, Forged(b1, "team-b", CommandKind.HandOffEvacuee, "evacuee-1", "cmd-handoff-b1", 2, "b1"));
            Assert.That(a1.ApplyReceipt(stale) && b1.ApplyReceipt(outsider), Is.True);
            Assert.That(stale.Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(outsider.Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(a1.Results.Last().Accepted || b1.Results.Last().Accepted, Is.False);
            Refresh(a1, a2, b1);
            var authoritative = shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "evacuee-1");
            Assert.That(authoritative.LeaderId, Is.EqualTo("a2"));
            Assert.That(authoritative.Revision, Is.EqualTo(2));
            Assert.That(new[] { a1, a2, b1 }.All(p => p.Evacuees.Single().LeaderId == authoritative.LeaderId), Is.True);
        }

        // Acceptance 4 (negative): a report outside this participant's observation is absent from view and UI state.
        [Test]
        public void ReportOutsideObservationIsAbsentFromViewAndUiState()
        {
            var reportId = ReportFromA1("team-b", "cmd-report-to-b");
            Assert.That(shift.Observe("a1").Reports, Is.Empty, "server view for the reporter's own team omits a report to team-b");
            Assert.That(shift.Observe("a2").Reports, Is.Empty);
            Assert.That(shift.Observe("b1").Reports.Single().ReportId, Is.EqualTo(reportId));
            Assert.That(a1.Reports, Is.Empty);
            Assert.That(a2.Reports, Is.Empty);
            Assert.That(b1.Reports.Single().ToTeamId, Is.EqualTo("team-b"));
            Assert.That(a1.RenderLines().Any(l => l.Contains(reportId)), Is.False);
            Assert.That(a1.Acknowledge(reportId), Is.Null);

            foreach (var panel in new[] { a1, a2, b1 })
            {
                Assert.That(panel.Evacuees.Any(e => e.EntityId == "evacuee-far"), Is.False, panel.ParticipantId);
                Assert.That(panel.ReportableIncidentIds.Contains("incident-far"), Is.False, panel.ParticipantId);
                Assert.That(panel.Claim("evacuee-far"), Is.Null, panel.ParticipantId);
                Assert.That(panel.Report("incident-far"), Is.Null, panel.ParticipantId);
            }

            // A misrouted projection carrying another team's report is not displayed.
            var tampered = Project("a1");
            tampered.Observed.Reports = tampered.Observed.Reports.Concat(Project("b1").Observed.Reports).ToArray();
            var ignoredBefore = a1.IgnoredReports;
            a1.ApplyView(tampered);
            Assert.That(a1.Reports, Is.Empty);
            Assert.That(a1.IgnoredReports, Is.EqualTo(ignoredBefore + 1));

            // Another participant's view is rejected whole.
            var viewsBefore = a1.IgnoredViews;
            a1.ApplyView(Project("b1"));
            Assert.That(a1.IgnoredViews, Is.EqualTo(viewsBefore + 1));
            Assert.That(a1.Reports, Is.Empty);
            Assert.That(a1.TeamId, Is.EqualTo("team-a"));

            // Receipts for commands this client never sent are not displayed.
            var foreign = Send(b1, b1.AcknowledgeNext("cmd-ack-b1"));
            Assert.That(foreign.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(a1.ApplyReceipt(foreign), Is.False);
            var unsolicited = Wire(foreign); unsolicited.ParticipantId = "a1"; unsolicited.CommandId = "never-sent";
            Assert.That(a1.ApplyReceipt(unsolicited), Is.False);
            Assert.That(a1.Results.Count(r => r.CommandId == "never-sent" || r.CommandId == "cmd-ack-b1"), Is.EqualTo(0));
            Assert.That(a1.IgnoredReceipts, Is.EqualTo(2));
        }

        // Review fix: the panel send path used by NetworkFieldRuntime.SubmitPanelCommand, repeated acknowledgement presses,
        // ambiguous server views and views from another shift.
        [Test]
        public void PanelSendPathTracksOnlySentCommandsAndRejectsAmbiguousOrOtherShiftViews()
        {
            var reportId = ReportFromA1();
            var wires = new List<string>();

            // Transport or input not ready: nothing is sent and nothing is tracked.
            Assert.That(a2.Submit(a2.AcknowledgeNext("cmd-ack-offline"), false, wires.Add), Is.EqualTo(PanelSubmitOutcome.TransportNotReady));
            Assert.That(a2.Submit(a2.AcknowledgeNext("cmd-ack-offline"), true, null), Is.EqualTo(PanelSubmitOutcome.TransportNotReady));
            Assert.That(wires, Is.Empty);
            Assert.That(a2.Pending, Is.Empty);

            // A command the panel could not build, or one built for another participant, is not sent.
            Assert.That(a2.Submit(a2.Claim("evacuee-far"), true, wires.Add), Is.EqualTo(PanelSubmitOutcome.NotAllowed));
            var foreignCommand = a1.AcknowledgeNext("cmd-ack-as-a1");
            Assert.That(foreignCommand, Is.Not.Null, "a real command built by another participant's panel");
            Assert.That(a2.Submit(foreignCommand, true, wires.Add), Is.EqualTo(PanelSubmitOutcome.OtherParticipant));
            Assert.That(wires, Is.Empty);

            // Sent: the wire text is exactly the command JSON, and the command is tracked after it was handed over.
            var command = a2.AcknowledgeNext("cmd-ack-a2");
            Assert.That(a2.Submit(command, true, text => { Assert.That(a2.Pending, Is.Empty, "tracked only after send"); wires.Add(text); }),
                Is.EqualTo(PanelSubmitOutcome.Sent));
            Assert.That(wires, Has.Count.EqualTo(1));
            Assert.That(wires[0], Is.EqualTo(JsonUtility.ToJson(command)));
            Assert.That(a2.Pending.Single().CommandId, Is.EqualTo("cmd-ack-a2"));

            // Pressing R again while the acknowledgement waits for its receipt builds no duplicate.
            Assert.That(a2.AcknowledgeNext("cmd-ack-a2-again"), Is.Null);
            // If the receipt never arrives, the waiting acknowledgement stops blocking after enough server views.
            for (var i = 0; i < FieldCommandPanel.AcknowledgeRetryViews - 1; i++) Refresh(a2);
            Assert.That(a2.AcknowledgeNext("cmd-ack-a2-early"), Is.Null, "still inside the retry window");
            Refresh(a2);
            Assert.That(a2.AcknowledgeNext("cmd-ack-a2-retry")?.TargetId, Is.EqualTo(reportId), "resend allowed once the window has passed");
            var receipt = Wire(shift.Submit("a2", JsonUtility.FromJson<WorldCommand>(wires[0])));
            Assert.That(a2.ApplyReceipt(receipt), Is.True);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted));
            Refresh(a2);
            Assert.That(a2.AcknowledgeNext(), Is.Null, "acknowledged by the server view now");

            // A view repeating a report id or an entity id is ignored whole; the previous rows stay.
            var before = a1.Reports.Single().ReportId;
            var views = a1.IgnoredViews;
            var repeatedReport = Project("a1");
            repeatedReport.Observed.Reports = repeatedReport.Observed.Reports.Concat(repeatedReport.Observed.Reports).ToArray();
            Assert.DoesNotThrow(() => a1.ApplyView(repeatedReport));
            var repeatedEntity = Project("a1");
            repeatedEntity.Observed.Entities = repeatedEntity.Observed.Entities.Concat(new[] { repeatedEntity.Observed.Entities[0] }).ToArray();
            Assert.DoesNotThrow(() => a1.ApplyView(repeatedEntity));
            Assert.That(a1.IgnoredViews, Is.EqualTo(views + 2));
            Assert.That(a1.Reports.Single().ReportId, Is.EqualTo(before).And.EqualTo(reportId));

            // Rows without an id are skipped instead of discarding the whole view, and cannot be targeted.
            var blankIds = Project("a1");
            blankIds.Observed.Entities = blankIds.Observed.Entities.Concat(new[] {
                new EntityState { EntityId = "", RegionId = "hall", Kind = EntityKind.Evacuee },
                new EntityState { EntityId = null, RegionId = "hall", Kind = EntityKind.Incident, Active = true } }).ToArray();
            a1.ApplyView(blankIds);
            Assert.That(a1.IgnoredViews, Is.EqualTo(views + 2), "a view with blank ids is still applied");
            Assert.That(a1.Evacuees.All(e => !string.IsNullOrEmpty(e.EntityId)), Is.True);
            Assert.That(a1.ReportableIncidentIds.All(id => !string.IsNullOrEmpty(id)), Is.True);
            Assert.DoesNotThrow(() => a1.Claim(""));
            Assert.That(a1.Claim(""), Is.Null);
            Assert.That(a1.Report(null), Is.Null);
            Assert.That(a1.Evacuees.Select(e => e.EntityId), Is.EquivalentTo(new[] { "evacuee-1" }), "the named evacuee row is still shown");
            Assert.DoesNotThrow(() => a1.Claim("evacuee-1"));

            // A view from another shift is ignored; the runtime also refuses such a snapshot before the panel sees it.
            var otherShift = Project("a1");
            otherShift.Observed.ShiftId = "fmp10a-other-shift";
            a1.ApplyView(otherShift);
            Assert.That(a1.IgnoredViews, Is.EqualTo(views + 3));
            Assert.That(a1.Reports.Single().ReportId, Is.EqualTo(reportId), "rows from the ignored view are not shown");
            Assert.That(a1.Acknowledge(reportId).ShiftId, Is.EqualTo(ShiftId));
        }

        // Acceptance 5: run record binds source/build hash and rendered panel lines to the executed test.
        [Test]
        public void RunRecordBindsSourceAndBuildHashToRenderedPanelState()
        {
            var reportId = ReportFromA1();
            Assert.That(a2.ApplyReceipt(Send(a2, a2.AcknowledgeNext("cmd-ack-a2"))), Is.True);
            var won = Send(a1, a1.Claim("evacuee-1", "cmd-claim-a1"));
            Refresh(a2);
            var lost = Send(a2, Forged(a2, "team-a", CommandKind.ClaimEvacuee, "evacuee-1", "cmd-claim-a2", 0));
            Assert.That(a1.ApplyReceipt(won) && a2.ApplyReceipt(lost), Is.True);
            Refresh(a1, a2, b1);
            Assert.That(a1.ApplyReceipt(Send(a1, a1.HandOffLed("a2", "cmd-handoff-a1"))), Is.True);
            Refresh(a1, a2, b1);

            var record = new RunRecord { TestClass = GetType().FullName, EditorVersion = Application.unityVersion,
                Platform = Application.platform.ToString(), SourceRef = Option("--cg-fmp10a-source-ref", ""),
                Synthetic = true, Result = "this test is reported only when the PlayMode XML records it" };
            record.Sources = BoundSources.Select(path => new SourceDigest { Path = path, Sha256 = Sha256OfFile(path) }).ToArray();
            var location = typeof(NetworkFieldRuntime).Assembly.Location;
            record.BuildHash = !string.IsNullOrEmpty(location) && File.Exists(location) ? Sha256OfFile(location) : "";
            record.BuildHashSource = string.IsNullOrEmpty(record.BuildHash) ? "unavailable" : "assembly:" + Path.GetFileName(location);
            record.RenderEvidence = new[] { a1, a2, b1 }.SelectMany(p => p.RenderLines().Select(line => p.ParticipantId + " | " + line)).ToArray();
            record.ReceiptCodes = receiptLog.ToArray();
            record.CommitWrites = sink.Writes;

            Assert.That(record.Sources.All(s => System.Text.RegularExpressions.Regex.IsMatch(s.Sha256, "^[0-9a-f]{64}$")), Is.True);
            Assert.That(record.BuildHash, Does.Match("^[0-9a-f]{64}$"));
            if (!string.IsNullOrEmpty(record.SourceRef)) Assert.That(record.SourceRef, Does.Match("^[0-9a-f]{40}$"));
            Assert.That(record.RenderEvidence, Has.Some.EqualTo("a1 | 팀 보고 " + reportId + ": incident-1 / hall · 관측 rev 0 · 보고자 a1 · 수신확인 a2"));
            Assert.That(record.RenderEvidence, Has.Some.EqualTo("a2 | 대피자 evacuee-1 / hall · 내가 인솔 중 · rev 2"));
            Assert.That(record.RenderEvidence, Has.Some.EqualTo("a1 | 대피자 evacuee-1 / hall · 인솔자 a2 · rev 2"));
            Assert.That(record.RenderEvidence, Has.Some.EqualTo("a2 | 서버 거부(StaleTarget): 인솔 evacuee-1"));
            Assert.That(record.RenderEvidence, Has.Some.EqualTo("a1 | 서버 승인: 인계 evacuee-1 → a2"));
            Assert.That(record.RenderEvidence.Where(l => l.StartsWith("b1 |")).Any(l => l.Contains(reportId)), Is.False, "team-b render has no team-a report");
            Assert.That(record.RenderEvidence.Any(l => l.Contains("evacuee-far") || l.Contains("incident-far")), Is.False);

            var target = Option("--cg-fmp10a-run-record", "");
            if (string.IsNullOrEmpty(target)) { Debug.Log("FMP-10a run record was not requested: " + JsonUtility.ToJson(record)); return; }
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(target, JsonUtility.ToJson(record, true));
            var reread = JsonUtility.FromJson<RunRecord>(File.ReadAllText(target));
            Assert.That(reread.BuildHash, Is.EqualTo(record.BuildHash));
            Assert.That(reread.Sources.Select(s => s.Sha256), Is.EqualTo(record.Sources.Select(s => s.Sha256)));
            Assert.That(reread.RenderEvidence, Is.EqualTo(record.RenderEvidence));
            Assert.That(reread.ReceiptCodes, Is.EqualTo(record.ReceiptCodes));
        }

        [Serializable] private sealed class SourceDigest { public string Path, Sha256; }
        [Serializable] private sealed class RunRecord
        {
            public string TestClass, EditorVersion, Platform, BuildHash, BuildHashSource, SourceRef = "", Result = "";
            public bool Synthetic;
            public int CommitWrites;
            public SourceDigest[] Sources = Array.Empty<SourceDigest>();
            public string[] RenderEvidence = Array.Empty<string>(), ReceiptCodes = Array.Empty<string>();
        }

        private static string Option(string name, string fallback)
        {
            var bare = name.TrimStart('-');
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
                if (string.Equals(args[index].TrimStart('-'), bare, StringComparison.Ordinal))
                    return index + 1 < args.Length ? args[index + 1] : fallback;
            return fallback;
        }

        private static string Sha256OfFile(string path)
        {
            Assert.That(File.Exists(path), Is.True, "hash input must exist: " + path);
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }
    }
}
