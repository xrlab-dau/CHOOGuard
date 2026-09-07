using System;
using System.Linq;
using NUnit.Framework;

namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoFlowTests
    {
        [TestCase("role-01", "anchor-01")]
        [TestCase("role-02", "anchor-02")]
        [TestCase("role-03", "anchor-03")]
        [TestCase("role-04", "anchor-04")]
        [TestCase("role-05", "anchor-05")]
        public void EachRoleRequiresBriefingInteractionAndAssembly(string roleId, string anchor)
        {
            var flow = new DemoFlow(Profile());
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.RoleSelection));
            flow.SelectRole(roleId);
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.Briefing));
            Assert.That(flow.TryInteract(anchor), Is.False);
            Assert.That(flow.TryReachAssembly(), Is.False);
            flow.BeginIncident();
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.Incident));
            Assert.That(flow.TryReachAssembly(), Is.False);
            Assert.That(flow.TryInteract(anchor), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.ReachAssembly));
            Assert.That(flow.Session.TeamStateProvider.States.Count(item => item.State == "notified"), Is.EqualTo(4));
            Assert.That(flow.TryInteract(anchor), Is.False, "No duplicate visual effects or team events.");
            Assert.That(flow.TryReachAssembly(), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.Results));
            Assert.That(flow.Session.Feedback.Disclaimer, Is.EqualTo(ScenarioValidation.RequiredDisclaimer));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("anchor-02")]
        public void WrongTargetDoesNotCompleteOrNotify(string anchor)
        {
            var flow = new DemoFlow(Profile());
            flow.SelectRole("role-01");
            flow.BeginIncident();
            var before = flow.Session.Snapshot.PreStateHash;
            Assert.That(flow.TryInteract(anchor), Is.False);
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.Incident));
            Assert.That(flow.Session.Snapshot.PreStateHash, Is.EqualTo(before));
            Assert.That(flow.Session.TeamStateProvider.States.All(item => item.State == "idle"), Is.True);
        }

        [Test]
        public void RestartClearsRoleIncidentTeamAndFeedback()
        {
            var flow = new DemoFlow(Profile());
            flow.SelectRole("role-01");
            flow.BeginIncident();
            flow.TryInteract("anchor-01");
            flow.TryReachAssembly();
            flow.Restart();
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.RoleSelection));
            Assert.That(flow.SelectedRole, Is.Null);
            Assert.That(flow.LastAction, Is.Null);
            Assert.That(flow.Session.Feedback, Is.Null);
            Assert.That(flow.Session.TeamStateProvider.States.All(item => item.State == "idle"), Is.True);
            flow.SelectRole("role-05");
            flow.BeginIncident();
            Assert.That(flow.TryInteract("anchor-05"), Is.True);
        }

        [Test]
        public void InvalidSelectionAndPrematureStartCannotSkipBriefing()
        {
            var flow = new DemoFlow(Profile());
            Assert.Throws<InvalidOperationException>(() => flow.BeginIncident());
            Assert.Throws<ArgumentException>(() => flow.SelectRole("unknown"));
            Assert.That(flow.Phase, Is.EqualTo(DemoPhase.RoleSelection));
        }

        private static ScenarioProfile Profile()
        {
            return new ScenarioProfile
            {
                schemaVersion = "1.0", scenarioId = "demo-test", scenarioVersion = "foundation-1",
                mapId = "test-map", provisional = true, disclaimer = ScenarioValidation.RequiredDisclaimer,
                representativeRoleId = "role-01",
                roles = Enumerable.Range(1, 5).Select(index => new RoleDefinition
                {
                    roleId = "role-" + index.ToString("00"), temporaryDisplayName = "임시 역할 " + index,
                    briefing = "가상 대상 상호작용", actionId = "action-" + index.ToString("00"),
                    targetAnchorId = "anchor-" + index.ToString("00"), expectedQuestState = "completed",
                    expectedFeedbackCode = "demo-observed", feedbackText = "예시 입력을 관찰했습니다.",
                    expectedVirtualTeamEvents = Enumerable.Range(1, 5).Where(other => other != index)
                        .Select(other => new VirtualTeamEventDefinition
                        {
                            roleId = "role-" + other.ToString("00"), eventCode = "demo-notification", state = "notified"
                        }).ToArray()
                }).ToArray()
            };
        }
    }
}
