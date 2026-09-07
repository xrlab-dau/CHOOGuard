using System;
using System.Linq;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class TrainingSessionTests
    {
        [TestCase(InputModality.VR)]
        [TestCase(InputModality.Desktop)]
        public void RepresentativeRoleCompletesSelectionBriefingActionTeamFeedback(InputModality modality)
        {
            var session = new TrainingSession(Profile());
            Assert.That(session.Snapshot.Phase, Is.EqualTo(TrainingPhase.RoleSelection));
            var briefing = session.SelectRole("role-01");
            Assert.That(briefing.RoleId, Is.EqualTo("role-01"));
            Assert.That(briefing.Disclaimer, Is.EqualTo("KORAIL 검증 전 예시"));
            Assert.That(session.Snapshot.Phase, Is.EqualTo(TrainingPhase.Briefing));
            session.AcknowledgeBriefing();
            var result = session.Submit(Action(session, modality));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.QuestState, Is.EqualTo("completed"));
            Assert.That(result.Feedback.Code, Is.EqualTo("synthetic-feedback-01"));
            Assert.That(result.Feedback.Text, Is.EqualTo("예시 상호작용 완료 01"));
            Assert.That(result.Feedback.Disclaimer, Is.EqualTo("KORAIL 검증 전 예시"));
            Assert.That(result.VirtualTeamEvents.Select(item => item.RoleId),
                Is.EquivalentTo(new[] { "role-02", "role-03", "role-04", "role-05" }));
            Assert.That(result.VirtualTeamEvents.All(item =>
                item.EventCode == "synthetic-action-observed" && item.State == "notified"), Is.True);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(TrainingPhase.Feedback));
            Assert.That(session.TeamStateProvider.States.Single(item => item.RoleId == "role-01").State,
                Is.EqualTo("idle"));
            Assert.That(session.TeamStateProvider.States.Count(item => item.State == "notified"), Is.EqualTo(4));
        }

        [TestCase("role-01")]
        [TestCase("role-02")]
        [TestCase("role-03")]
        [TestCase("role-04")]
        [TestCase("role-05")]
        public void EveryRoleHasIdenticalVrAndDesktopSemantics(string roleId)
        {
            var vr = Ready(roleId);
            var desktop = Ready(roleId);
            Assert.That(vr.Snapshot.PreStateHash, Is.EqualTo(desktop.Snapshot.PreStateHash));
            var vrResult = vr.Submit(Action(vr, InputModality.VR, "vr-attempt"));
            var desktopResult = desktop.Submit(Action(desktop, InputModality.Desktop, "desktop-attempt"));
            Assert.That(vrResult.Accepted && desktopResult.Accepted, Is.True);
            Assert.That(vrResult.QuestState, Is.EqualTo(desktopResult.QuestState));
            Assert.That(vrResult.Feedback.Code, Is.EqualTo(desktopResult.Feedback.Code));
            Assert.That(vrResult.Feedback.Text, Is.EqualTo(desktopResult.Feedback.Text));
            Assert.That(vrResult.VirtualTeamEvents.Select(EventKey),
                Is.EqualTo(desktopResult.VirtualTeamEvents.Select(EventKey)));
            Assert.That(vr.Snapshot.PreStateHash, Is.EqualTo(desktop.Snapshot.PreStateHash));
        }

        [TestCase("version", ActionResultCode.ScenarioVersionMismatch)]
        [TestCase("role", ActionResultCode.RoleMismatch)]
        [TestCase("anchor", ActionResultCode.TargetAnchorMismatch)]
        [TestCase("action", ActionResultCode.ActionMismatch)]
        [TestCase("hash", ActionResultCode.StaleState)]
        [TestCase("attempt", ActionResultCode.InvalidAttemptId)]
        [TestCase("modality", ActionResultCode.InvalidModality)]
        public void InvalidActionDoesNotMutateQuestTeamOrAttemptHistory(string invalidField, ActionResultCode code)
        {
            var session = Ready();
            var before = session.Snapshot;
            var good = Action(session);
            var invalid = new TrainingAction(
                invalidField == "attempt" ? " " : good.AttemptId,
                invalidField == "version" ? "wrong-version" : good.ScenarioVersion,
                invalidField == "role" ? "role-02" : good.RoleId,
                invalidField == "hash" ? "stale-hash" : good.PreStateHash,
                invalidField == "action" ? "unknown-action" : good.ActionId,
                invalidField == "anchor" ? "anchor-02" : good.TargetAnchorId,
                invalidField == "modality" ? (InputModality)99 : good.Modality);
            var result = session.Submit(invalid);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo(code));
            Assert.That(result.Feedback, Is.Null);
            Assert.That(result.VirtualTeamEvents, Is.Empty);
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(before.PreStateHash));
            Assert.That(session.Snapshot.Phase, Is.EqualTo(before.Phase));
            Assert.That(session.Snapshot.QuestState, Is.EqualTo(before.QuestState));
            Assert.That(session.TeamStateProvider.States.All(item => item.State == "idle"), Is.True);
            Assert.That(session.Submit(good).Accepted, Is.True, "Rejected attempts must not consume an attempt ID.");
        }

        [Test]
        public void DuplicateAcceptedAttemptCannotProduceEventsAgain()
        {
            var session = Ready();
            var action = Action(session);
            Assert.That(session.Submit(action).Accepted, Is.True);
            var hash = session.Snapshot.PreStateHash;
            var duplicate = session.Submit(action);
            Assert.That(duplicate.Code, Is.EqualTo(ActionResultCode.DuplicateAttempt));
            Assert.That(duplicate.VirtualTeamEvents, Is.Empty);
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(hash));
        }

        [Test]
        public void NewAttemptWithOldHashIsStaleAfterCompletion()
        {
            var session = Ready();
            var first = Action(session);
            session.Submit(first);
            var hash = session.Snapshot.PreStateHash;
            var replay = new TrainingAction("second", first.ScenarioVersion, first.RoleId,
                first.PreStateHash, first.ActionId, first.TargetAnchorId, first.Modality);
            Assert.That(session.Submit(replay).Code, Is.EqualTo(ActionResultCode.StaleState));
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(hash));
        }

        [Test]
        public void CompletedQuestCannotBeResubmittedWithFreshHash()
        {
            var session = Ready();
            session.Submit(Action(session));
            var before = session.Snapshot.PreStateHash;
            Assert.That(session.Submit(Action(session, attemptId: "second")).Code,
                Is.EqualTo(ActionResultCode.NotReady));
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(before));
        }

        [Test]
        public void BriefingMustBeAcknowledgedBeforeAction()
        {
            var session = new TrainingSession(Profile());
            session.SelectRole("role-01");
            var before = session.Snapshot.PreStateHash;
            Assert.That(session.Submit(Action(session)).Code, Is.EqualTo(ActionResultCode.NotReady));
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(before));
            session.AcknowledgeBriefing();
            Assert.That(session.Submit(Action(session)).Accepted, Is.True);
        }

        [Test]
        public void UnknownRoleAndPrematureBriefingDoNotMutateSession()
        {
            var session = new TrainingSession(Profile());
            var before = session.Snapshot.PreStateHash;
            Assert.Throws<ArgumentException>(() => session.SelectRole("unknown-role"));
            Assert.Throws<InvalidOperationException>(() => session.AcknowledgeBriefing());
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(before));
        }

        [Test]
        public void RoleCannotBeChangedDuringAnAttempt()
        {
            var session = Ready();
            var before = session.Snapshot.PreStateHash;
            Assert.Throws<InvalidOperationException>(() => session.SelectRole("role-02"));
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(before));
        }

        [Test]
        public void HashIsDeterministicAndBindsTheScenarioContent()
        {
            var first = Profile();
            var reordered = Profile();
            Array.Reverse(reordered.roles);
            foreach (var role in reordered.roles) Array.Reverse(role.expectedVirtualTeamEvents);
            Assert.That(new TrainingSession(first).Snapshot.PreStateHash,
                Is.EqualTo(new TrainingSession(reordered).Snapshot.PreStateHash));
            var changed = Profile();
            changed.roles[0].feedbackText = "수정된 예시 피드백";
            Assert.That(new TrainingSession(first).Snapshot.PreStateHash,
                Is.Not.EqualTo(new TrainingSession(changed).Snapshot.PreStateHash));
            Assert.That(new TrainingSession(first).Snapshot.PreStateHash, Does.Match("^[a-f0-9]{64}$"));
        }

        [Test]
        public void CallerCannotMutateTheRunningScenarioThroughDtos()
        {
            var profile = Profile();
            var session = new TrainingSession(profile);
            var before = session.Snapshot.PreStateHash;
            profile.roles[0].actionId = "changed";
            profile.roles[0].expectedVirtualTeamEvents[0].state = "changed";
            profile.roles[0].feedbackText = "changed";
            profile.disclaimer = "changed";
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(before));
            session.SelectRole("role-01");
            session.AcknowledgeBriefing();
            var result = session.Submit(Action(session));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Feedback.Text, Is.EqualTo("예시 상호작용 완료 01"));
            Assert.That(result.VirtualTeamEvents.All(item => item.State == "notified"), Is.True);
        }

        [TestCase("schema")]
        [TestCase("count")]
        [TestCase("provisional")]
        [TestCase("disclaimer")]
        [TestCase("duplicate-role")]
        [TestCase("duplicate-action")]
        [TestCase("duplicate-anchor")]
        [TestCase("unknown-representative")]
        [TestCase("unknown-event-role")]
        [TestCase("self-event")]
        [TestCase("missing-event")]
        [TestCase("duplicate-event-role")]
        [TestCase("invalid-quest")]
        [TestCase("other-role-completed")]
        public void InvalidScenarioFailsBeforeSessionCreation(string fault)
        {
            var profile = Profile();
            switch (fault)
            {
                case "schema": profile.schemaVersion = "2.0"; break;
                case "count": profile.roles = profile.roles.Take(4).ToArray(); break;
                case "provisional": profile.provisional = false; break;
                case "disclaimer": profile.disclaimer = ""; break;
                case "duplicate-role": profile.roles[1].roleId = "role-01"; break;
                case "duplicate-action": profile.roles[1].actionId = "action-01"; break;
                case "duplicate-anchor": profile.roles[1].targetAnchorId = "anchor-01"; break;
                case "unknown-representative": profile.representativeRoleId = "unknown"; break;
                case "unknown-event-role": profile.roles[0].expectedVirtualTeamEvents[0].roleId = "unknown"; break;
                case "self-event": profile.roles[0].expectedVirtualTeamEvents[0].roleId = "role-01"; break;
                case "missing-event": profile.roles[0].expectedVirtualTeamEvents = new VirtualTeamEventDefinition[0]; break;
                case "duplicate-event-role": profile.roles[0].expectedVirtualTeamEvents[1].roleId = "role-02"; break;
                case "invalid-quest": profile.roles[0].expectedQuestState = "passed"; break;
                case "other-role-completed": profile.roles[0].expectedVirtualTeamEvents[0].state = "completed"; break;
            }
            Assert.Throws<ArgumentException>(() => new TrainingSession(profile));
        }

        [Test]
        public void RoleFixtureContainsAUsablePreStateAndReturnsCopies()
        {
            var session = Ready();
            var fixture = session.CreateRoleActionFixture();
            Assert.That(fixture.representative, Is.True);
            Assert.That(fixture.preStateHash, Is.EqualTo(session.Snapshot.PreStateHash));
            fixture.expectedVirtualTeamEvents[0].state = "changed";
            Assert.That(session.Submit(Action(session)).VirtualTeamEvents.All(item => item.State == "notified"), Is.True);
        }

        private static string EventKey(VirtualTeamEvent item)
        {
            return item.RoleId + ":" + item.EventCode + ":" + item.State;
        }

        private static TrainingSession Ready(string roleId = "role-01")
        {
            var session = new TrainingSession(Profile());
            session.SelectRole(roleId);
            session.AcknowledgeBriefing();
            return session;
        }

        private static TrainingAction Action(TrainingSession session,
            InputModality modality = InputModality.Desktop, string attemptId = "attempt-01")
        {
            var suffix = session.Snapshot.RoleId.Substring("role-".Length);
            return new TrainingAction(attemptId, "foundation-1", session.Snapshot.RoleId,
                session.Snapshot.PreStateHash, "action-" + suffix, "anchor-" + suffix, modality);
        }

        private static ScenarioProfile Profile()
        {
            return new ScenarioProfile
            {
                schemaVersion = "1.0", scenarioId = "foundation-demo", scenarioVersion = "foundation-1",
                mapId = "synthetic-room", provisional = true, disclaimer = "KORAIL 검증 전 예시",
                representativeRoleId = "role-01",
                roles = Enumerable.Range(1, 5).Select(index => new RoleDefinition
                {
                    roleId = "role-" + index.ToString("00"), temporaryDisplayName = "임시 역할 " + index,
                    briefing = "예시 대상과 상호작용합니다.", actionId = "action-" + index.ToString("00"),
                    targetAnchorId = "anchor-" + index.ToString("00"), expectedQuestState = "completed",
                    expectedFeedbackCode = "synthetic-feedback-" + index.ToString("00"),
                    feedbackText = "예시 상호작용 완료 " + index.ToString("00"),
                    expectedVirtualTeamEvents = Enumerable.Range(1, 5).Where(other => other != index)
                        .Select(other => new VirtualTeamEventDefinition
                        {
                            roleId = "role-" + other.ToString("00"),
                            eventCode = "synthetic-action-observed", state = "notified"
                        }).ToArray()
                }).ToArray()
            };
        }
    }
}
