using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class ContinuousElevatorTests
    {
        private GameObject root;
        private StationDoor lower, upper;
        private PhysicalElevatorCarriage carriage;
        private ElevatorCar car;
        private Transform lowerCarLeaf;
        private Vector3 lowerCarLeafClosed;

        [SetUp] public void SetUp() => root = new GameObject("기존 카 연속 운행 시험");
        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        private Transform Part(Transform parent, string name, Vector3 position)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false); transform.localPosition = position;
            return transform;
        }

        private StationDoor Stop(string floor, float y)
        {
            var go = new GameObject("시험 승강장 " + floor);
            go.transform.SetParent(root.transform, false); go.SetActive(false);
            go.transform.localPosition = Vector3.up * y;
            var door = go.AddComponent<StationDoor>();
            door.Kind = StationDoor.DoorKind.Elevator; door.CarId = "실제 부분 바인딩 시험";
            door.Floor = floor; door.Width = 1.1f; door.Centre = Vector3.zero;
            door.Cabin = Part(go.transform, "보존할 기존 카", Vector3.back * .21f);
            door.Cabin.rotation = Quaternion.LookRotation(Vector3.back);
            door.CabinSize = new Vector3(2, 2.4f, 1.75f);
            door.Leaves = new[] {
                new StationDoor.Leaf { Pivot = Part(go.transform, "홀 왼쪽", new Vector3(-.275f, 0, -.11f)), Slide = Vector3.left * .55f },
                new StationDoor.Leaf { Pivot = Part(go.transform, "홀 오른쪽", new Vector3(.275f, 0, -.11f)), Slide = Vector3.right * .55f },
                new StationDoor.Leaf { Pivot = Part(go.transform, "카 왼쪽", new Vector3(-.275f, 0, -.165f)), Slide = Vector3.left * .55f },
                new StationDoor.Leaf { Pivot = Part(go.transform, "카 오른쪽", new Vector3(.275f, 0, -.165f)), Slide = Vector3.right * .55f }
            };
            go.SetActive(true); door.enabled = false;
            return door;
        }

        private void Rig(float upperOffset = 0)
        {
            lower = Stop("2", 7); upper = Stop("3", 12.2f);
            upper.Cabin.position += Vector3.right * upperOffset;
            lowerCarLeaf = lower.Leaves[2].Pivot; lowerCarLeafClosed = lowerCarLeaf.position;
            carriage = lower.gameObject.AddComponent<PhysicalElevatorCarriage>();
            carriage.CarId = lower.CarId;
            carriage.Stops = new[] {
                new PhysicalElevatorCarriage.Stop { Door = lower, SourceCabin = lower.Cabin, CarDoorLeaves = new[] { lower.Leaves[2].Pivot, lower.Leaves[3].Pivot } },
                new PhysicalElevatorCarriage.Stop { Door = upper, SourceCabin = upper.Cabin, CarDoorLeaves = new[] { upper.Leaves[2].Pivot, upper.Leaves[3].Pivot } }
            };
            car = Part(root.transform, "공유 드라이브", Vector3.zero).gameObject.AddComponent<ElevatorCar>();
            car.enabled = false;
            typeof(ElevatorCar).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(car, new object[] { lower.CarId, new List<StationDoor> { upper, lower } });
        }

        private void Depart() { car.Press(1); car.AdvanceDrive(.01f); Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Moving)); }
        private static void DoorFraction(StationDoor door, float value) => typeof(StationDoor).GetProperty("Open").SetValue(door, value);
        private static void Near(Vector3 a, Vector3 b) => Assert.That(Vector3.Distance(a, b), Is.LessThan(.0001f));

        [Test]
        public void CabinAndPassengerMoveThroughMidpointWithoutChangingLandingIdentityEarly()
        {
            Rig();
            var passenger = Part(root.transform, "승객", Vector3.zero).gameObject;
            // This carriage fixture has no walking NavMesh. Configure the required
            // agent while inactive, before its first enable can try to bind one.
            passenger.SetActive(false);
            var body = passenger.AddComponent<PersonBody>();
            body.GetComponent<NavMeshAgent>().enabled = false;
            body.transform.position = carriage.Cabin.TransformPoint(new Vector3(.2f, 0, 1));
            passenger.SetActive(true);
            Assert.That(body.Agent, Is.SameAs(body.GetComponent<NavMeshAgent>()));
            Assert.That(body.Agent.enabled, Is.False);
            Vector3 initial = body.transform.position;
            var rider = new ElevatorCar.Rider { Body = body, Cabin = 0, To = 1, Spot = new Vector3(.2f, 0, 1) };
            car.Riders.Add(rider);
            Depart(); car.AdvanceDrive(1);
            Assert.That(carriage.PositionY, Is.GreaterThan(7).And.LessThan(9.6f));
            car.AdvanceDrive(2.6f);
            Assert.That(carriage.PositionY, Is.EqualTo(9.6f).Within(.0001f));
            Near(body.transform.position, initial + Vector3.up * 2.6f);
            Near(car.SpotWorld(rider), body.transform.position);
            Assert.That(rider.Cabin, Is.Zero, "중간층으로 순간 전환하지 않는다");
            Assert.That(lower.transform.TransformPoint(lower.Centre).y, Is.EqualTo(7));
            Assert.That(upper.transform.TransformPoint(upper.Centre).y, Is.EqualTo(12.2f));
        }

        [Test]
        public void PlayerIsCarriedContinuouslyWithoutResettingViewOrReparenting()
        {
            Rig();
            var player = Part(root.transform, "역무원", Vector3.zero).gameObject.AddComponent<FirstPersonResponder>();
            player.SetExternalInputMode(true);
            player.RestorePhysicalPose(carriage.Cabin.TransformPoint(new Vector3(0, 0, 1)), 74, 23);
            typeof(ElevatorCar).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(car, player);
            var parent = player.transform.parent;
            Depart(); car.AdvanceDrive(1); car.AdvanceDrive(2.6f);
            Assert.That(player.transform.position.y, Is.EqualTo(9.6f).Within(.005f));
            Assert.That(player.YawDegrees, Is.EqualTo(74)); Assert.That(player.PitchDegrees, Is.EqualTo(23));
            Assert.That(player.transform.parent, Is.EqualTo(parent));
        }

        [Test]
        public void AnyOpenHallDoorPreventsDepartureAndAnOpeningDuringTravelStopsTheDrive()
        {
            Rig(); DoorFraction(upper, .4f);
            car.Press(1); car.AdvanceDrive(.01f);
            Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Closing)); Assert.That(carriage.PositionY, Is.EqualTo(7));
            DoorFraction(upper, 0); car.AdvanceDrive(.01f);
            Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Moving));
            car.AdvanceDrive(1); Vector3 before = carriage.Cabin.position;
            DoorFraction(lower, .1f); car.AdvanceDrive(2);
            Assert.That(car.Running, Is.False); Near(carriage.Cabin.position, before);
        }

        [Test]
        public void PowerLossKeepsCabinBetweenFloorsAndResumeDoesNotJumpOrOpenDoors()
        {
            Rig(); Depart(); car.AdvanceDrive(2.7f);
            var stopped = carriage.Cabin.position; float fraction = car.TravelFraction;
            car.Stop(); car.AdvanceDrive(60);
            Near(carriage.Cabin.position, stopped); Assert.That(car.TravelFraction, Is.EqualTo(fraction));
            Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Moving)); Assert.That(car.DoorsOpenAt(1), Is.False);
            car.Resume(); Near(carriage.Cabin.position, stopped);
            car.AdvanceDrive(.1f);
            Assert.That(carriage.PositionY, Is.GreaterThan(stopped.y).And.LessThan(12.2f));
            car.AdvanceDrive(20);
            Assert.That(car.At, Is.EqualTo(1)); Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Opening));
            Assert.That(carriage.PositionY, Is.EqualTo(12.2f).Within(.0001f));
        }

        [Test]
        public void SharedCarLeavesFollowTheDriveWhileHallLeavesAndOriginalSourcesStayDistinct()
        {
            Rig();
            Assert.That(lower.Leaves.Length, Is.EqualTo(2)); Assert.That(upper.Leaves.Length, Is.EqualTo(2));
            Assert.That(upper.Cabin.gameObject.activeSelf, Is.False); Assert.That(lowerCarLeaf.parent, Is.EqualTo(carriage.Cabin));
            carriage.ApplyDoors(1); Near(lowerCarLeaf.position, lowerCarLeafClosed + Vector3.left * .55f);
            carriage.ApplyDoors(0); Near(lowerCarLeaf.position, lowerCarLeafClosed);
            Depart(); car.AdvanceDrive(20);
            Near(lowerCarLeaf.position, lowerCarLeafClosed + Vector3.up * 5.2f);
            Assert.That(car.InCabin(0, carriage.Cabin.TransformPoint(Vector3.forward)), Is.False);
            Assert.That(car.InCabin(1, carriage.Cabin.TransformPoint(Vector3.forward)), Is.True);
        }

        [Test]
        public void MisalignedSourceBankIsRefusedAndCannotResumeIntoTeleportFallback()
        {
            LogAssert.Expect(LogType.Error, "Elevator physical binding refused: 실제 부분 바인딩 시험 · stops_are_not_one_existing_vertical_bank");
            Rig(.1f);
            Assert.That(car.ContinuousMotion, Is.False); Assert.That(car.Running, Is.False);
            car.Resume(); car.Press(1); car.AdvanceDrive(30);
            Assert.That(car.Running, Is.False); Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Idle));
            Assert.That(lower.Cabin.position.y, Is.EqualTo(7)); Assert.That(upper.Cabin.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator RemovingOnlyTheDriveRestoresSurvivingOriginalSourcesAfterDestructionFinishes()
        {
            Rig(); Depart(); car.AdvanceDrive(2);
            Object.DestroyImmediate(car.gameObject);
            Assert.That(carriage.Bound, Is.False);
            yield return null;
            yield return null;
            Assert.That(lower.Leaves.Length, Is.EqualTo(4)); Assert.That(upper.Leaves.Length, Is.EqualTo(4));
            Assert.That(lowerCarLeaf.parent, Is.EqualTo(lower.transform)); Near(lowerCarLeaf.position, lowerCarLeafClosed);
            Assert.That(upper.Cabin.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void DestroyingTheSourceOwnerDoesNotReparentIntoADestroyingHierarchy()
        {
            Rig(); Depart(); car.AdvanceDrive(2);
            Object.DestroyImmediate(root);
            root = null;
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnbindRestoresThePreservedSourceHierarchyAndInvalidTimeCannotMoveTheDrive()
        {
            Rig(); Depart();
            car.AdvanceDrive(float.NaN); car.AdvanceDrive(float.PositiveInfinity); car.AdvanceDrive(-1);
            Assert.That(carriage.PositionY, Is.EqualTo(7));
            car.AdvanceDrive(3); carriage.Unbind();
            Assert.That(lower.Leaves.Length, Is.EqualTo(4)); Assert.That(upper.Leaves.Length, Is.EqualTo(4));
            Assert.That(lowerCarLeaf.parent, Is.EqualTo(lower.transform)); Near(lowerCarLeaf.position, lowerCarLeafClosed);
            Assert.That(upper.Cabin.gameObject.activeSelf, Is.True); Assert.That(lower.Cabin.position.y, Is.EqualTo(7));
        }
    }
}
