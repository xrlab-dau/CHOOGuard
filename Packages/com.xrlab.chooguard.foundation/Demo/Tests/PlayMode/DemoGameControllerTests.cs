using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoGameControllerTests
    {
        private GameObject root;
        private DemoGameController game;
        private DemoPlayerController player;
        private DemoInteractable[] targets;
        private Transform spawn;
        private Transform assembly;
        private TextAsset scenario;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Demo game controller test");
            var actor = Child("Player");
            var character = actor.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.center = new Vector3(0, .9f, 0);
            var cameraObject = Child("Camera");
            cameraObject.transform.SetParent(actor.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            player = actor.AddComponent<DemoPlayerController>();
            player.Configure(cameraObject.AddComponent<Camera>());
            spawn = Child("Spawn").transform;
            assembly = Child("Assembly").transform;
            assembly.position = new Vector3(0, 0, 15);
            targets = Enumerable.Range(1, 5).Select(index =>
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.SetParent(root.transform);
                obj.transform.position = new Vector3(index * 4, 1.6f, 2);
                obj.transform.localScale = Vector3.one * .5f;
                var target = obj.AddComponent<DemoInteractable>();
                target.Configure("anchor-" + index.ToString("00"), "Test prop " + index, index - 1);
                return target;
            }).ToArray();
            scenario = new TextAsset(JsonUtility.ToJson(Profile()));
            game = Child("Game").AddComponent<DemoGameController>();
            game.Configure(scenario, player, spawn, targets, assembly);
            game.enabled = false; // Tests drive public gameplay transitions without live keyboard input.
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(scenario);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator EveryRoleCanPlayAndRestartWithOnlyItsOwnPropCompleted()
        {
            for (var index = 0; index < 5; index++)
            {
                game.SelectRole("role-" + (index + 1).ToString("00"));
                game.BeginIncident();
                player.SetControlEnabled(false);
                Assert.That(game.TryCompleteAssembly(), Is.False);
                var oldPosition = targets[index].transform.position;
                targets[index].transform.position = new Vector3(0, 1.6f, 2);
                Physics.SyncTransforms();
                Assert.That(game.TryInteract(), Is.True);
                Assert.That(targets.Count(item => item.Completed), Is.EqualTo(1));
                Assert.That(game.Flow.Phase, Is.EqualTo(DemoPhase.ReachAssembly));
                Assert.That(game.TryCompleteAssembly(), Is.False, "Results require physically reaching the zone.");
                player.Teleport(assembly);
                Assert.That(game.TryCompleteAssembly(), Is.True);
                Assert.That(game.Flow.Phase, Is.EqualTo(DemoPhase.Results));
                game.RestartDemo();
                Assert.That(targets.All(item => !item.Completed), Is.True);
                Assert.That(game.Flow.Phase, Is.EqualTo(DemoPhase.RoleSelection));
                Assert.That(Vector3.Distance(player.transform.position, spawn.position), Is.LessThan(.01f));
                targets[index].transform.position = oldPosition;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WrongDistantOccludedAndUnregisteredPropsNeverCompleteQuest()
        {
            game.SelectRole("role-01");
            game.BeginIncident();
            player.SetControlEnabled(false);
            var initialHash = game.Flow.Session.Snapshot.PreStateHash;
            targets[1].transform.position = new Vector3(0, 1.6f, 2);
            Physics.SyncTransforms();
            Assert.That(game.TryInteract(), Is.False, "Visible wrong-role prop.");
            targets[1].transform.position = new Vector3(8, 1.6f, 2);
            targets[0].transform.position = new Vector3(0, 1.6f, 8);
            Physics.SyncTransforms();
            Assert.That(game.TryInteract(), Is.False, "Correct prop outside reach.");
            targets[0].transform.position = new Vector3(0, 1.6f, 2);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1.6f, 1);
            wall.transform.localScale = new Vector3(2, 2, .2f);
            Physics.SyncTransforms();
            Assert.That(game.TryInteract(), Is.False, "Correct prop behind wall.");
            var unregistered = wall.AddComponent<DemoInteractable>();
            unregistered.Configure("anchor-01", "Duplicate unregistered target", 0);
            Assert.That(game.TryInteract(), Is.False, "Matching anchor on an unregistered object.");
            Assert.That(game.Flow.Session.Snapshot.PreStateHash, Is.EqualTo(initialHash));
            Assert.That(game.Flow.Session.TeamStateProvider.States.All(item => item.State == "idle"), Is.True);
            Assert.That(targets.All(item => !item.Completed), Is.True);
            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            Assert.That(game.TryInteract(), Is.True, "An invalid attempt must not consume the valid interaction.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseUnlocksCursorAndBlocksInteractionAndAssembly()
        {
            game.SelectRole("role-01");
            game.BeginIncident();
            targets[0].transform.position = new Vector3(0, 1.6f, 2);
            Physics.SyncTransforms();
            game.PauseDemo();
            Assert.That(game.IsPaused, Is.True);
            Assert.That(player.ControlEnabled, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(game.TryInteract(), Is.False);
            player.Teleport(assembly);
            Assert.That(game.TryCompleteAssembly(), Is.False);
            game.RestartDemo();
            Assert.That(game.IsPaused, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssemblyBeforeActionOrOnDifferentFloorCannotProduceResults()
        {
            game.SelectRole("role-01");
            game.BeginIncident();
            player.SetControlEnabled(false);
            player.Teleport(assembly);
            Assert.That(game.TryCompleteAssembly(), Is.False);
            player.Teleport(spawn);
            targets[0].transform.position = new Vector3(0, 1.6f, 2);
            Physics.SyncTransforms();
            Assert.That(game.TryInteract(), Is.True);
            var high = Child("Other floor").transform;
            high.position = assembly.position + Vector3.up * 4;
            player.Teleport(high);
            Assert.That(game.TryCompleteAssembly(), Is.False);
            player.Teleport(assembly);
            Assert.That(game.TryCompleteAssembly(), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssemblyGuidanceFollowsAcceptedActionAndResets()
        {
            var guide = Child("Assembly guide");
            game.ConfigureAssemblyGuide(guide.transform);
            Assert.That(guide.activeSelf, Is.False);
            game.SelectRole("role-01");
            game.BeginIncident();
            player.SetControlEnabled(false);
            Assert.That(guide.activeSelf, Is.False);
            targets[0].transform.position = new Vector3(0, 1.6f, 2);
            Physics.SyncTransforms();
            Assert.That(game.TryInteract(), Is.True);
            Assert.That(guide.activeSelf, Is.True);
            game.RestartDemo();
            Assert.That(guide.activeSelf, Is.False);
            yield return null;
        }

        private GameObject Child(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root.transform);
            return obj;
        }

        private static ScenarioProfile Profile()
        {
            return new ScenarioProfile
            {
                schemaVersion = "1.0", scenarioId = "demo-controller-test", scenarioVersion = "foundation-1",
                mapId = "test-room", provisional = true, disclaimer = ScenarioValidation.RequiredDisclaimer,
                representativeRoleId = "role-01",
                roles = Enumerable.Range(1, 5).Select(index => new RoleDefinition
                {
                    roleId = "role-" + index.ToString("00"), temporaryDisplayName = "임시 역할 " + index,
                    briefing = "가상 대상", actionId = "action-" + index.ToString("00"),
                    targetAnchorId = "anchor-" + index.ToString("00"), expectedQuestState = "completed",
                    expectedFeedbackCode = "demo-feedback", feedbackText = "예시 입력 관찰",
                    expectedVirtualTeamEvents = Enumerable.Range(1, 5).Where(other => other != index)
                        .Select(other => new VirtualTeamEventDefinition
                        {
                            roleId = "role-" + other.ToString("00"), eventCode = "demo-notified", state = "notified"
                        }).ToArray()
                }).ToArray()
            };
        }
    }
}
