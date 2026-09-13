using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class GroundedMovementTests
    {
        private GameObject root;
        [SetUp] public void Setup() => root = new GameObject("Grounded movement fixture");
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [UnityTest]
        public IEnumerator SweepsStopThinWallsAndCeilingsAndRejectVoidAndVerticalInput()
        {
            Box(new Vector3(0, -.15f, 0), new Vector3(8, .3f, 8), true);
            Box(new Vector3(1, 1, 0), new Vector3(.02f, 2, 4), false);
            yield return null; Physics.SyncTransforms();
            Assert.That(GroundedWorldMotor.TryGroundedStep(Vector3.zero, new Vector3(2, 0, 0), out _), Is.False,
                "The endpoint is clear but the thin wall is crossed.");
            Assert.That(GroundedWorldMotor.TryGroundedStep(Vector3.zero, new Vector3(0, 4, 0), out _), Is.False);
            Assert.That(GroundedWorldMotor.TryGroundedStep(new Vector3(0, 0, 3.7f), new Vector3(0, 0, .25f), out _), Is.False,
                "The complete support disk must remain on ground.");
            Assert.That(GroundedWorldMotor.TryGroundedStep(new Vector3(20, 0, 0), new Vector3(.15f, 0, 0), out _), Is.False);
            Box(new Vector3(-1, 1.4f, 0), new Vector3(1, .2f, 2), false);
            Physics.SyncTransforms();
            Assert.That(GroundedWorldMotor.TryGroundedStep(Vector3.zero, new Vector3(-.9f, 0, 0), out _), Is.False);
        }

        [UnityTest]
        public IEnumerator GroundSupportFollowsSlopeWithoutFloatingOrPassingUnderIt()
        {
            var ramp = Box(new Vector3(0, -.12f, 0), new Vector3(4, .24f, 12), true);
            ramp.transform.rotation = Quaternion.Euler(-15, 0, 0);
            yield return null; Physics.SyncTransforms();
            var at = Vector3.zero;
            for (var i = 0; i < 20; i++)
            {
                Assert.That(GroundedWorldMotor.TryGroundedStep(at, Vector3.forward * .15f, out var next), Is.True);
                at = next;
            }
            Assert.That(at.y, Is.GreaterThan(.7f));
            Assert.That(at.y, Is.LessThan(.9f));
        }

        private GameObject Box(Vector3 at, Vector3 size, bool walkable)
        {
            var box = new GameObject(walkable ? "Floor" : "Obstacle"); box.transform.SetParent(root.transform);
            box.transform.position = at; box.transform.localScale = size; box.AddComponent<BoxCollider>();
            if (walkable) box.AddComponent<ConnectedWalkableSurface>();
            return box;
        }
    }
}
