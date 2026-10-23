using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Equipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>Stage-1 wheelchair adapter: registration by kind, push follows the staff member, release sets it on the floor, pause lets go.</summary>
    public sealed class WheelchairPointTests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private FirstPersonResponder staff;
        private WheelchairPoint chair;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "floor";
            floor.transform.position = new Vector3(0, -.1f, 0);
            floor.transform.localScale = new Vector3(30, .2f, 30);
            made.Add(floor);

            var staffObject = new GameObject("역무원 fixture");
            staffObject.transform.position = Vector3.zero;
            made.Add(staffObject);
            staff = staffObject.AddComponent<FirstPersonResponder>();
            staff.SetExternalInputMode(true);

            var chairObject = new GameObject("wheelchair fixture");
            chairObject.transform.position = new Vector3(0, 0, 2);
            made.Add(chairObject);
            var box = chairObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, .45f, 0); box.size = new Vector3(.65f, .9f, .83f);
            var equipment = chairObject.AddComponent<StationEquipment>();
            equipment.Assign("wheelchair-test-01", "wheelchair", "휠체어", "hall2f");
            equipment.Interactable = true; equipment.Batchable = false;
            chair = chairObject.AddComponent<WheelchairPoint>();
            Physics.SyncTransforms();
            yield return null;
            staff.Resume(false);
            yield return null;
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var go in made) if (go != null) Object.Destroy(go);
            made.Clear();
        }

        [Test]
        public void TheChairIsRegisteredAsWheelchairEquipmentAndStartsParked()
        {
            Assert.That(EquipmentRegistry.OfKind("wheelchair"), Does.Contain(chair.Equipment));
            Assert.That(chair.Equipment.State, Is.EqualTo(WheelchairPoint.StateParked));
            Assert.That(chair.InteractionPrompt, Is.EqualTo("휠체어 밀기"));
            Assert.That(chair.CanInteract(staff, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator PushingMovesTheChairWithTheStaffMemberAndReleasingSetsItOnTheFloor()
        {
            Assert.That(chair.TryInteract(staff, out var feedback), Is.True);
            Assert.That(feedback, Does.Contain("밀고"));
            Assert.That(chair.Pushing, Is.True);
            Assert.That(chair.transform.parent, Is.EqualTo(staff.transform));
            Assert.That(chair.Equipment.State, Is.EqualTo(WheelchairPoint.StateMoving));
            Assert.That(chair.InteractionPrompt, Is.EqualTo("휠체어 놓기"));

            staff.transform.position = new Vector3(4, 0, 1);
            staff.transform.rotation = Quaternion.Euler(0, 90, 0);
            yield return null;
            var expected = staff.transform.TransformPoint(chair.PushOffset);
            Assert.That(Vector3.Distance(chair.transform.position, expected), Is.LessThan(.001f));

            Assert.That(chair.TryInteract(staff, out feedback), Is.True);
            Assert.That(feedback, Does.Contain("놓았"));
            Assert.That(chair.Pushing, Is.False);
            Assert.That(chair.transform.parent, Is.Null);
            Assert.That(chair.transform.position.y, Is.EqualTo(0f).Within(.01f));
            Assert.That(chair.transform.position.x, Is.EqualTo(expected.x).Within(.01f));
            Assert.That(chair.transform.position.z, Is.EqualTo(expected.z).Within(.01f));
            Assert.That(chair.transform.eulerAngles.y, Is.EqualTo(90f).Within(.1f));
            Assert.That(chair.Equipment.State, Is.EqualTo(WheelchairPoint.StateParked));
        }

        [UnityTest]
        public IEnumerator PausingTheStaffMemberLetsGoOfTheChair()
        {
            Assert.That(chair.TryInteract(staff, out _), Is.True);
            Assert.That(chair.Pushing, Is.True);
            staff.Pause();
            yield return null;
            Assert.That(chair.Pushing, Is.False);
            Assert.That(chair.transform.parent, Is.Null);
            Assert.That(chair.Equipment.State, Is.EqualTo(WheelchairPoint.StateParked));
            Assert.That(chair.CanInteract(staff, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(""));
        }
    }
}
