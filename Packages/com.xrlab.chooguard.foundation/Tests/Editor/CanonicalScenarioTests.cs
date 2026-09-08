using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CanonicalScenarioTests
    {
        // Independently calculated from the committed JSON using Python hashlib and the README encoding.
        // Fixture file SHA-256: bd7dfa87529f2d3e94f517e28d23929ed693129406f6f1823090bff73fef3969.
        // Changing fixture content requires reviewing the fixture AND updating these goldens.
        [TestCase("role-01", "e286c404bc7df3fb629399e92bdf3b383f626eff5b959b34c57f07f972438d2c")]
        [TestCase("role-02", "3164db12407d462339d839362490a51faf963785d82975870296e715a98be5c5")]
        [TestCase("role-03", "7d1f576d2bf1d5fd2ec0b580718779ea1325c5d1df797931263ba35ef2939a3f")]
        [TestCase("role-04", "b7187bf719041d00c91527023c64094b9242ec6e8496cc774028ff6af9e96f47")]
        [TestCase("role-05", "dd41fa21396797957a6fa29bf939db7c70d4b09570b4fa3f751ad4f077a425d9")]
        public void CanonicalJsonHasGoldenReadyState(string roleId, string expectedHash)
        {
            var session = new TrainingSession(LoadCanonicalProfile());
            session.SelectRole(roleId);
            session.AcknowledgeBriefing();
            Assert.That(session.Snapshot.PreStateHash, Is.EqualTo(expectedHash));
            Assert.That(session.CreateRoleActionFixture().preStateHash, Is.EqualTo(expectedHash));
        }

        [TestCase("role-01", InputModality.VR)]
        [TestCase("role-01", InputModality.Desktop)]
        [TestCase("role-02", InputModality.VR)]
        [TestCase("role-02", InputModality.Desktop)]
        [TestCase("role-03", InputModality.VR)]
        [TestCase("role-03", InputModality.Desktop)]
        [TestCase("role-04", InputModality.VR)]
        [TestCase("role-04", InputModality.Desktop)]
        [TestCase("role-05", InputModality.VR)]
        [TestCase("role-05", InputModality.Desktop)]
        public void CanonicalJsonDrivesAllTenRoleModalityPaths(string roleId, InputModality modality)
        {
            var profile = LoadCanonicalProfile();
            var role = profile.roles.Single(item => item.roleId == roleId);
            var session = new TrainingSession(profile);
            var briefing = session.SelectRole(roleId);
            Assert.That(briefing.Text, Is.EqualTo(role.briefing));
            Assert.That(briefing.Disclaimer, Is.EqualTo("KORAIL 검증 전 예시"));
            session.AcknowledgeBriefing();
            var action = new TrainingAction("canonical-" + roleId + "-" + modality,
                profile.scenarioVersion, roleId, session.Snapshot.PreStateHash,
                role.actionId, role.targetAnchorId, modality);
            var result = session.Submit(action);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.QuestState, Is.EqualTo(role.expectedQuestState));
            Assert.That(result.Feedback.Code, Is.EqualTo(role.expectedFeedbackCode));
            Assert.That(result.Feedback.Text, Is.EqualTo(role.feedbackText));
            Assert.That(result.VirtualTeamEvents.Select(item => item.RoleId + ":" + item.EventCode + ":" + item.State),
                Is.EquivalentTo(role.expectedVirtualTeamEvents.Select(item => item.roleId + ":" + item.eventCode + ":" + item.state)));
            Assert.That(session.Snapshot.Phase, Is.EqualTo(TrainingPhase.Feedback));
        }

        private static ScenarioProfile LoadCanonicalProfile()
        {
            var package = PackageInfo.FindForAssembly(typeof(TrainingSession).Assembly);
            Assert.That(package, Is.Not.Null, "Import this code as the local com.xrlab.chooguard.foundation UPM package.");
            var fixturePath = Path.GetFullPath(Path.Combine(package.resolvedPath,
                "..", "..", "foundation", "scenarios", "foundation-demo.json"));
            Assert.That(File.Exists(fixturePath), Is.True,
                "Keep the repository intact and use its Packages/com.xrlab.chooguard.foundation as a local UPM dependency. Missing: " + fixturePath);
            return JsonUtility.FromJson<ScenarioProfile>(File.ReadAllText(fixturePath));
        }
    }
}
