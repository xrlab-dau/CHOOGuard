using System;
using System.Linq;
using ChooGuard.Foundation;
using ChooGuard.Foundation.Demo;
using NUnit.Framework;

namespace ChooGuard.Team.M403.Tests
{
    // M4-03 candidate: the four non-representative provisional roles through the Desktop path, compared field by field
    // with the M4-01 fixture. Lines starting with "M403|" are the run record read back from the test result XML.
    // Nothing here is an official role, procedure or score, and nothing here is HMD evidence.
    public sealed class M403DesktopRoleActionTests
    {
        private M403Fixture fixture;
        private ScenarioProfile scenario;

        [OneTimeSetUp]
        public void LoadInputs()
        {
            fixture = M403Inputs.LoadFixture();
            Assert.That(fixture, Is.Not.Null, "blocked: fixture_missing " + M403Inputs.FixturePath);
            scenario = M403Inputs.LoadScenario();
            TestContext.WriteLine("M403|inputs|fixtureSha256=" + M403Inputs.Sha256(M403Inputs.FixturePath)
                + "|scenarioSha256=" + M403Inputs.Sha256(M403Inputs.ScenarioPath) + "|fixtureId=" + fixture.fixtureId);
        }

        [Test]
        public void FixtureIsBoundToTheCanonicalScenarioAndHasFourNonRepresentativeProvisionalRows()
        {
            Assert.That(M403Inputs.Sha256(M403Inputs.ScenarioPath), Is.EqualTo(fixture.canonicalScenarioSnapshot.sourceSha256),
                "blocked: the fixture was written against a different canonical scenario");
            Assert.That(fixture.roleRows.Length, Is.EqualTo(5));
            Assert.That(fixture.roleRows.Where(row => row.representative).Select(row => row.roleId), Is.EqualTo(new[] { scenario.representativeRoleId }));
            Assert.That(fixture.roleRows.Where(row => !row.representative).Select(row => row.roleId), Is.EqualTo(M403Inputs.NonRepresentativeRoles));
            foreach (var roleId in M403Inputs.NonRepresentativeRoles)
            {
                var row = fixture.roleRows.Single(item => item.roleId == roleId);
                var role = scenario.roles.Single(item => item.roleId == roleId);
                Assert.That(row.scenarioVersion, Is.EqualTo(scenario.scenarioVersion), roleId);
                Assert.That(new[] { row.actionId, row.targetAnchorId, row.expectedQuestState, row.expectedFeedbackCode, row.feedbackText },
                    Is.EqualTo(new[] { role.actionId, role.targetAnchorId, role.expectedQuestState, role.expectedFeedbackCode, role.feedbackText }), roleId);
                Assert.That(M403Inputs.Events(row.expectedVirtualTeamEvents), Is.EqualTo(M403Inputs.Events(role.expectedVirtualTeamEvents)), roleId);
                foreach (var mark in new[] { row.provisionalMarks.role, row.provisionalMarks.action, row.provisionalMarks.feedback })
                {
                    Assert.That(mark.synthetic && mark.provisional, Is.True, roleId);
                    Assert.That(mark.official || mark.officialKorailProcedure || mark.officialScoring, Is.False, roleId);
                }
            }
        }

        [Test]
        public void FourActionsAreDistinctFromEachOtherAndFromTheRepresentativeRole()
        {
            var rows = M403Inputs.NonRepresentativeRoles.Select(id => fixture.roleRows.Single(row => row.roleId == id)).ToArray();
            var representative = fixture.roleRows.Single(row => row.representative);
            foreach (var field in new Func<M403Row, string>[] { row => row.actionId, row => row.targetAnchorId, row => row.feedbackText, row => row.temporaryDisplayName })
            {
                var values = rows.Select(field).ToArray();
                Assert.That(values.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(4));
                Assert.That(values, Has.No.Member(field(representative)));
            }
            TestContext.WriteLine("M403|distinct|actions=" + string.Join(",", rows.Select(row => row.roleId + "=" + row.actionId + "@" + row.targetAnchorId)));
        }

        [TestCase("role-02")]
        [TestCase("role-03")]
        [TestCase("role-04")]
        [TestCase("role-05")]
        public void DesktopFlowMatchesTheFixtureRow(string roleId)
        {
            var plan = M403Inputs.Resolve(fixture, roleId, InputModality.Desktop);
            Assert.That(plan.Ready, Is.True, "blocked: " + plan.Reason);
            var row = plan.Row;

            var flow = new DemoFlow(scenario);
            flow.SelectRole(roleId);
            Assert.That(new[] { flow.SelectedRole.ActionId, flow.SelectedRole.TargetAnchorId }, Is.EqualTo(new[] { row.actionId, row.targetAnchorId }));
            Assert.That(flow.SelectedRole.Disclaimer, Is.EqualTo(ScenarioValidation.RequiredDisclaimer));
            Assert.That(flow.TryInteract(row.targetAnchorId), Is.False, "no action before the briefing is acknowledged");
            flow.BeginIncident();

            var before = flow.Session.Snapshot;
            Assert.That(before.Phase.ToString(), Is.EqualTo(row.preState.phase));
            Assert.That(before.QuestState, Is.EqualTo(row.preState.questState));
            Assert.That(row.preState.briefingAcknowledged, Is.True);
            Assert.That(M403Inputs.Team(flow.Session.TeamStateProvider.States), Is.EqualTo(M403Inputs.Team(row.preState.virtualTeamStates)));

            Assert.That(flow.TryInteract(row.targetAnchorId), Is.True);
            var result = flow.LastAction;
            Assert.That(result.Code, Is.EqualTo(ActionResultCode.Accepted));
            Assert.That(result.QuestState, Is.EqualTo(row.expectedQuestState));
            Assert.That(M403Inputs.Events(result.VirtualTeamEvents), Is.EqualTo(M403Inputs.Events(row.expectedVirtualTeamEvents)));
            Assert.That(result.Feedback.Code, Is.EqualTo(row.expectedFeedbackCode));
            Assert.That(result.Feedback.Text, Is.EqualTo(row.feedbackText));
            Assert.That(result.Feedback.Disclaimer, Is.EqualTo(ScenarioValidation.RequiredDisclaimer));
            var after = flow.Session.TeamStateProvider.States;
            Assert.That(after.Single(state => state.RoleId == roleId).State, Is.EqualTo("idle"), "the actor's own display state is not a teammate event");
            Assert.That(after.Where(state => state.RoleId != roleId).Select(state => state.State), Is.All.EqualTo("notified"));
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.ReachAssembly));
            Assert.That(flow.TryInteract(row.targetAnchorId), Is.False, "a second interaction adds no events");
            Assert.That(M403Inputs.Events(flow.LastAction.VirtualTeamEvents), Is.EqualTo(M403Inputs.Events(row.expectedVirtualTeamEvents)));
            Assert.That(flow.TryReachAssembly(), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.Results));

            TestContext.WriteLine("M403|desktop|role=" + roleId + "|modality=Desktop|path=DemoFlow.TryInteract|preStateHash=" + before.PreStateHash
                + "|code=" + result.Code + "|quest=" + result.QuestState + "|events=" + M403Inputs.Events(result.VirtualTeamEvents)
                + "|feedback=" + result.Feedback.Code + "|postStateHash=" + result.StateHash + "|team=" + M403Inputs.Team(after));
        }

        [TestCase("role-02")]
        [TestCase("role-03")]
        [TestCase("role-04")]
        [TestCase("role-05")]
        public void ExplicitDesktopSubmissionEqualsTheDesktopFlowOutcome(string roleId)
        {
            var row = M403Inputs.Resolve(fixture, roleId, InputModality.Desktop).Row;
            var flow = new DemoFlow(scenario);
            flow.SelectRole(roleId);
            flow.BeginIncident();
            Assert.That(flow.TryInteract(row.targetAnchorId), Is.True);

            var session = Ready(roleId);
            var action = ActionFor(session, row, "m403-explicit-" + roleId, InputModality.Desktop);
            var result = session.Submit(in action);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.StateHash, Is.EqualTo(flow.LastAction.StateHash));
            Assert.That(M403Inputs.Events(result.VirtualTeamEvents), Is.EqualTo(M403Inputs.Events(flow.LastAction.VirtualTeamEvents)));
            Assert.That(result.Feedback.Code + "|" + result.Feedback.Text, Is.EqualTo(flow.LastAction.Feedback.Code + "|" + flow.LastAction.Feedback.Text));
        }

        [TestCase("role-02")]
        [TestCase("role-03")]
        [TestCase("role-04")]
        [TestCase("role-05")]
        public void RejectedDesktopInputsChangeNothingAndTheCorrectActionStillWorks(string roleId)
        {
            var row = M403Inputs.Resolve(fixture, roleId, InputModality.Desktop).Row;
            var other = fixture.roleRows.First(item => !item.representative && item.roleId != roleId);
            var cases = new (string name, Func<TrainingSession, TrainingAction> make, ActionResultCode expected)[]
            {
                ("other-role-anchor", s => With(ActionFor(s, row, "m403-a", InputModality.Desktop), anchor: other.targetAnchorId), ActionResultCode.TargetAnchorMismatch),
                ("missing-anchor", s => With(ActionFor(s, row, "m403-b", InputModality.Desktop), anchor: ""), ActionResultCode.TargetAnchorMismatch),
                ("other-role-action", s => With(ActionFor(s, row, "m403-c", InputModality.Desktop), actionId: other.actionId), ActionResultCode.ActionMismatch),
                ("other-role-id", s => With(ActionFor(s, row, "m403-d", InputModality.Desktop), roleId: other.roleId), ActionResultCode.RoleMismatch),
                ("scenario-version", s => With(ActionFor(s, row, "m403-e", InputModality.Desktop), version: "foundation-0"), ActionResultCode.ScenarioVersionMismatch),
                ("stale-prestate", s => With(ActionFor(s, row, "m403-f", InputModality.Desktop), preState: new string('0', 64)), ActionResultCode.StaleState),
                ("unknown-modality", s => ActionFor(s, row, "m403-g", (InputModality)99), ActionResultCode.InvalidModality),
                ("empty-attempt", s => ActionFor(s, row, " ", InputModality.Desktop), ActionResultCode.InvalidAttemptId),
            };
            foreach (var item in cases)
            {
                var session = Ready(roleId);
                var before = session.Snapshot;
                var action = item.make(session);
                var result = session.Submit(in action);
                Assert.That(result.Code, Is.EqualTo(item.expected), item.name);
                Assert.That(result.VirtualTeamEvents, Is.Empty, item.name);
                Assert.That(result.Feedback, Is.Null, item.name);
                Assert.That(result.StateHash, Is.EqualTo(before.PreStateHash), item.name);
                Assert.That(session.Snapshot.Phase, Is.EqualTo(TrainingPhase.Ready), item.name);
                Assert.That(session.Snapshot.QuestState, Is.EqualTo(row.preState.questState), item.name);
                Assert.That(session.TeamStateProvider.States.Select(state => state.State), Is.All.EqualTo("idle"), item.name);
                var retry = ActionFor(session, row, "m403-retry-" + item.name, InputModality.Desktop);
                Assert.That(session.Submit(in retry).Accepted, Is.True, item.name + " then correct action");
                TestContext.WriteLine("M403|rejected|role=" + roleId + "|case=" + item.name + "|code=" + result.Code + "|stateUnchanged=true|retryAccepted=true");
            }

            var accepted = Ready(roleId);
            var first = ActionFor(accepted, row, "m403-duplicate", InputModality.Desktop);
            Assert.That(accepted.Submit(in first).Accepted, Is.True);
            var afterFirst = accepted.Snapshot.PreStateHash;
            var duplicate = accepted.Submit(in first);
            Assert.That(duplicate.Code, Is.EqualTo(ActionResultCode.DuplicateAttempt));
            Assert.That(duplicate.VirtualTeamEvents, Is.Empty);
            Assert.That(accepted.Snapshot.PreStateHash, Is.EqualTo(afterFirst));
            TestContext.WriteLine("M403|rejected|role=" + roleId + "|case=duplicate-attempt|code=" + duplicate.Code + "|stateUnchanged=true");

            var flow = new DemoFlow(scenario);
            flow.SelectRole(roleId);
            flow.BeginIncident();
            var flowBefore = flow.Session.Snapshot.PreStateHash;
            Assert.That(flow.TryInteract(other.targetAnchorId), Is.False);
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.Incident));
            Assert.That(flow.Session.Snapshot.PreStateHash, Is.EqualTo(flowBefore));
        }

        [Test]
        public void MissingFixtureRowTargetOrModalityIsBlockedAndNotExecuted()
        {
            var cases = new (string expected, M403Fixture input, string roleId, InputModality? modality)[]
            {
                ("fixture_missing", null, "role-02", InputModality.Desktop),
                ("fixture_row_missing", fixture, "role-06", InputModality.Desktop),
                ("representative_role_out_of_scope", fixture, scenario.representativeRoleId, InputModality.Desktop),
                ("modality_missing", fixture, "role-02", null),
                ("modality_unknown", fixture, "role-02", (InputModality)99),
                ("target_missing", Mutated(row => row.targetAnchorId = ""), "role-02", InputModality.Desktop),
                ("action_missing", Mutated(row => row.actionId = null), "role-02", InputModality.Desktop),
                ("prestate_missing", Mutated(row => row.preState = new M403PreState()), "role-02", InputModality.Desktop),
                ("expected_output_missing", Mutated(row => row.expectedVirtualTeamEvents = new VirtualTeamEventDefinition[0]), "role-02", InputModality.Desktop),
                ("expected_output_missing", Mutated(row => row.expectedFeedbackCode = ""), "role-02", InputModality.Desktop),
            };
            foreach (var item in cases)
            {
                var plan = M403Inputs.Resolve(item.input, item.roleId, item.modality);
                Assert.That(plan.Ready, Is.False, item.expected);
                Assert.That(plan.Reason, Is.EqualTo(item.expected));
                Assert.That(plan.Row, Is.Null, "a blocked plan carries no row to execute");
                TestContext.WriteLine("M403|blocked|role=" + item.roleId + "|reason=" + plan.Reason);
            }
        }

        [TestCase("role-02")]
        [TestCase("role-03")]
        [TestCase("role-04")]
        [TestCase("role-05")]
        public void VrEnumValueGivesTheSameSemanticsAsDesktop_SemanticParityOnly_NotHmdEvidence(string roleId)
        {
            var row = M403Inputs.Resolve(fixture, roleId, InputModality.Desktop).Row;
            Assert.That(M403Inputs.Resolve(fixture, roleId, InputModality.VR).Ready, Is.True);
            var desktop = Ready(roleId);
            var vr = Ready(roleId);
            var desktopAction = ActionFor(desktop, row, "m403-parity-desktop", InputModality.Desktop);
            var vrAction = ActionFor(vr, row, "m403-parity-vr", InputModality.VR);
            var a = desktop.Submit(in desktopAction);
            var b = vr.Submit(in vrAction);
            Assert.That(a.Accepted && b.Accepted, Is.True);
            Assert.That(b.QuestState + "|" + M403Inputs.Events(b.VirtualTeamEvents) + "|" + b.Feedback.Code + "|" + b.StateHash,
                Is.EqualTo(a.QuestState + "|" + M403Inputs.Events(a.VirtualTeamEvents) + "|" + a.Feedback.Code + "|" + a.StateHash));
            TestContext.WriteLine("M403|semantic-parity|role=" + roleId + "|vrEnumValueOnly=true|hmd=unknown|xrRuntime=unknown|trackedTarget=unknown");
        }

        private TrainingSession Ready(string roleId)
        {
            var session = new TrainingSession(scenario);
            session.SelectRole(roleId);
            session.AcknowledgeBriefing();
            return session;
        }

        private static TrainingAction ActionFor(TrainingSession session, M403Row row, string attemptId, InputModality modality)
        {
            return new TrainingAction(attemptId, row.scenarioVersion, row.roleId, session.Snapshot.PreStateHash, row.actionId, row.targetAnchorId, modality);
        }

        private static TrainingAction With(TrainingAction action, string anchor = null, string actionId = null, string roleId = null,
            string version = null, string preState = null)
        {
            return new TrainingAction(action.AttemptId, version ?? action.ScenarioVersion, roleId ?? action.RoleId, preState ?? action.PreStateHash,
                actionId ?? action.ActionId, anchor ?? action.TargetAnchorId, action.Modality);
        }

        private M403Fixture Mutated(Action<M403Row> change)
        {
            var copy = UnityEngine.JsonUtility.FromJson<M403Fixture>(UnityEngine.JsonUtility.ToJson(fixture));
            change(copy.roleRows.Single(row => row.roleId == "role-02"));
            return copy;
        }
    }
}
