using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoInteractionTests
    {
        private GameObject sceneRoot;
        private DemoPlayerController player;

        [SetUp]
        public void SetUp()
        {
            sceneRoot = new GameObject("Demo interaction test");
            var playerObject = new GameObject("Player");
            playerObject.transform.SetParent(sceneRoot.transform);
            playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("View");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            var view = cameraObject.AddComponent<Camera>();
            player = playerObject.AddComponent<DemoPlayerController>();
            player.Configure(view);
            player.SetControlEnabled(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sceneRoot);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator RaycastFindsOnlyFirstVisibleTargetInsideReach()
        {
            var target = Target(new Vector3(0, 1.6f, 2));
            Physics.SyncTransforms();
            yield return null;
            DemoInteractable found;
            Assert.That(player.TryGetTarget(out found), Is.True);
            Assert.That(found, Is.SameAs(target));
            target.transform.position = new Vector3(0, 1.6f, 8);
            Physics.SyncTransforms();
            Assert.That(player.TryGetTarget(out found), Is.False);
            target.transform.position = new Vector3(0, 1.6f, 2);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(sceneRoot.transform);
            wall.transform.position = new Vector3(0, 1.6f, 1);
            wall.transform.localScale = new Vector3(2, 2, .2f);
            Physics.SyncTransforms();
            Assert.That(player.TryGetTarget(out found), Is.False, "A wall must block interaction.");
        }

        [UnityTest]
        public IEnumerator CompletedPropRestoresMaterialAndMovingPartOnReset()
        {
            var target = Target(new Vector3(0, 1.6f, 2));
            var moving = new GameObject("MovingPart");
            moving.transform.SetParent(target.transform, false);
            target.Configure("anchor-05", "Gate", 4);
            var before = moving.transform.localRotation;
            target.ApplyInteraction();
            yield return null;
            Assert.That(target.Completed, Is.True);
            Assert.That(Quaternion.Angle(before, moving.transform.localRotation), Is.GreaterThan(1));
            var properties = new MaterialPropertyBlock();
            target.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(properties.isEmpty, Is.False);
            target.ResetVisual();
            Assert.That(target.Completed, Is.False);
            Assert.That(Quaternion.Angle(before, moving.transform.localRotation), Is.LessThan(.01f));
            target.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(properties.isEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator TeleportClearsPitchAndRestoresSpawnWithControllerEnabled()
        {
            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(sceneRoot.transform);
            spawn.transform.position = new Vector3(4, 0, -2);
            spawn.transform.rotation = Quaternion.Euler(0, 90, 0);
            player.ViewCamera.transform.localRotation = Quaternion.Euler(35, 0, 0);
            player.Teleport(spawn.transform);
            yield return null;
            Assert.That(Vector3.Distance(player.transform.position, spawn.transform.position), Is.LessThan(.01f));
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(Quaternion.Angle(player.ViewCamera.transform.localRotation, Quaternion.identity), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator DetailedHousingKeepsItsMaterialWhenStatusLightChanges()
        {
            var target = Target(new Vector3(0, 1.6f, 2));
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.transform.SetParent(target.transform, false);
            target.ConfigureFeedback(new[] { indicator.GetComponent<Renderer>() });
            target.ApplyInteraction();
            yield return null;
            var block = new MaterialPropertyBlock();
            target.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.True, "Housing must retain its detailed material.");
            indicator.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.False);
            target.ResetVisual();
            indicator.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator InstantiatedHandsKeepTheirSerializedViewPose()
        {
            var original = new GameObject("Hand template");
            original.transform.SetParent(sceneRoot.transform);
            original.SetActive(false);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(original.transform, false);
            var pose = new Vector3(.25f, -.29f, .48f);
            visual.transform.localPosition = pose;
            original.AddComponent<DemoHands>().Configure(player, visual.transform);
            var clone = Object.Instantiate(original, player.ViewCamera.transform);
            clone.SetActive(true);
            player.SetControlEnabled(true);
            yield return null;
            Assert.That(Vector3.Distance(clone.transform.Find("Visual").localPosition, pose), Is.LessThan(.01f));
            player.SetControlEnabled(false);
        }

        [UnityTest]
        public IEnumerator CursorCaptureIgnoresStaleLookThenAllowsNormalLook()
        {
            player.SetControlEnabled(true);
            var facing = player.transform.rotation;
            player.ApplyLook(new Vector2(100, 100));
            Assert.That(Quaternion.Angle(facing, player.transform.rotation), Is.LessThan(.01f));
            yield return new WaitForSecondsRealtime(.16f);
            player.ApplyLook(new Vector2(20, 10));
            Assert.That(Quaternion.Angle(facing, player.transform.rotation), Is.GreaterThan(10));
            player.SetControlEnabled(false);
            facing = player.transform.rotation;
            player.ApplyLook(new Vector2(100, 100));
            Assert.That(Quaternion.Angle(facing, player.transform.rotation), Is.LessThan(.01f));
        }

        private DemoInteractable Target(Vector3 position)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.SetParent(sceneRoot.transform);
            target.transform.position = position;
            target.transform.localScale = Vector3.one * .5f;
            var interactable = target.AddComponent<DemoInteractable>();
            interactable.Configure("anchor-01", "Test panel", 0);
            return interactable;
        }
    }
}
