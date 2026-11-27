using System.Collections;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.Circulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class GroundRideOverlapTests
    {
        private Scene physicsScene;

        [SetUp]
        public void SetUp()
        {
            // CharacterController.Move must measure belt travel without resolving overlaps with the loaded level.
            physicsScene = SceneManager.CreateScene(nameof(GroundRideOverlapTests), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(physicsScene);
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, physicsScene);
            return go;
        }
        private Escalator Drive(string id)
        {
            var drive = CreateObject(id).AddComponent<Escalator>();
            drive.Setup(new StationPoints.EscalatorEntry { id = id, label = id, path = new[] { Vector3.zero, Vector3.forward, new Vector3(0, 3, 6), new Vector3(0, 3, 7) } });
            drive.enabled = false; return drive;
        }
        private EscalatorRide Volume(string name, Escalator drive)
        {
            var go = CreateObject(name); go.AddComponent<BoxCollider>().isTrigger = true;
            var ride = go.AddComponent<EscalatorRide>(); ride.Bind(drive); return ride;
        }

        [Test]
        public void OverlappingRampSeamsCarryOneBodyOncePerDriveAndPhysicsStep()
        {
            var drive = Drive("one-existing-flight"); var incline = Volume("incline", drive); var landing = Volume("landing", drive);
            var controller = CreateObject("rider").AddComponent<CharacterController>();
            Assert.That(incline.TryClaimTransport(controller, 10), Is.True);
            Assert.That(landing.TryClaimTransport(controller, 10), Is.False);
            Assert.That(landing.TryClaimTransport(controller, 10.02), Is.True);
            Assert.That(incline.TryClaimTransport(controller, 10.02), Is.False);
            var other = CreateObject("other rider").AddComponent<CharacterController>();
            Assert.That(incline.TryClaimTransport(other, 10.02), Is.True);
            var anotherFlight = Drive("different-existing-flight"); var separate = Volume("separate flight", anotherFlight);
            Assert.That(separate.TryClaimTransport(controller, 10.02), Is.True);
        }

        [Test]
        public void StoppedOrMissingDriveCannotClaimASeamMoveAndResumeKeepsTheSameStepGuard()
        {
            var drive = Drive("stop-resume-existing-flight"); var a = Volume("a", drive); var b = Volume("b", drive);
            var body = CreateObject("rider").AddComponent<Rigidbody>(); body.isKinematic = true;
            Assert.That(a.TryClaimTransport(body, 20), Is.True);
            drive.Stop(); Assert.That(b.TryClaimTransport(body, 20.02), Is.False);
            Assert.That(drive.Restart(out _), Is.True);
            Assert.That(b.TryClaimTransport(body, 20.02), Is.True);
            Assert.That(a.TryClaimTransport(body, 20.02), Is.False);
            Object.DestroyImmediate(drive.gameObject);
            Assert.That(b.TryClaimTransport(body, 20.04), Is.False);
        }

        [Test]
        public void TwoOverlappingCallbacksDoNotDoubleTheCharacterControllerDisplacement()
        {
            var drive = Drive("callback-existing-flight"); var a = Volume("a", drive); var b = Volume("b", drive);
            var controller = CreateObject("rider").AddComponent<CharacterController>();
            var callback = typeof(EscalatorRide).GetMethod("OnTriggerStay", BindingFlags.NonPublic | BindingFlags.Instance);
            var before = controller.transform.position;
            callback.Invoke(a, new object[] { controller }); var first = controller.transform.position;
            callback.Invoke(b, new object[] { controller }); var second = controller.transform.position;
            float expectedDistance = Escalator.BeltSpeed * Time.deltaTime;
            Assert.That(expectedDistance, Is.GreaterThan(.0001f), "The first callback must move the rider by a measurable distance.");
            Assert.That(Vector3.Distance(first, before), Is.EqualTo(expectedDistance).Within(.0001f));
            Assert.That(Vector3.Distance(first, second), Is.LessThan(.0001f));
        }
    }
}
