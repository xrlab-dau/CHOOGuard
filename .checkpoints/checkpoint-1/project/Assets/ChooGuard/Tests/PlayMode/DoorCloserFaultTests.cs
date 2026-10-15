#if UNITY_INCLUDE_TESTS && CHOOGUARD_DOOR_CLOSER_FAULT
using System.Collections;
using System.Reflection;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>Driver fixtures exercise the real hand gesture and closer driver; they are not surveyed map replacements.</summary>
    public sealed class DoorCloserFaultTests
    {
        private GameObject rig;
        private StationDoor door;
        private FirstPersonResponder staff;
        private DoorOperatorFault fault;
        private int captureRate;
        private float timeScale;

        [SetUp]
        public void Setup()
        {
            captureRate = Time.captureFramerate; timeScale = Time.timeScale;
            Time.timeScale = 1; Time.captureFramerate = 30;
            rig = new GameObject("클로저 구동·수동 조작 시험 fixture");
            rig.transform.position = new Vector3(12000, 0, 12000);
            var doorObject = new GameObject("기존 문 드라이버 fixture");
            doorObject.transform.SetParent(rig.transform, false); doorObject.SetActive(false);
            door = doorObject.AddComponent<StationDoor>();
            door.Kind = StationDoor.DoorKind.Swing; door.Use = StationDoor.DoorUse.Staff;
            door.Label = "클로저 시험 문"; door.Width = .9f;
            var leaf = new GameObject("실제 드라이버가 돌리는 시험 문짝");
            leaf.transform.SetParent(door.transform, false); leaf.transform.localPosition = Vector3.left * .45f;
            var collider = leaf.AddComponent<BoxCollider>();
            collider.center = new Vector3(.45f, 1.05f, 0); collider.size = new Vector3(.9f, 2.1f, .06f);
            door.Leaves = new[] { new StationDoor.Leaf { Pivot = leaf.transform, Length = .9f, Swing = 90 } };
            var staffObject = new GameObject("근무 직원 fixture");
            staffObject.transform.SetParent(rig.transform, false);
            staff = staffObject.AddComponent<FirstPersonResponder>(); staff.SetExternalInputMode(true);
            doorObject.SetActive(true);
        }

        [TearDown]
        public void Teardown()
        {
            if (rig != null) Object.DestroyImmediate(rig);
            Time.captureFramerate = captureRate; Time.timeScale = timeScale;
        }

        private void AtHandle(bool nearby = true)
        {
            var standing = door.World(new Vector3(.45f, 0, nearby ? 1.2f : 5f));
            var ray = door.World(new Vector3(0, 1.05f, 0)) - (standing + Vector3.up * staff.EyeHeight);
            var look = Quaternion.LookRotation(ray).eulerAngles;
            staff.RestorePhysicalPose(standing, look.y, look.x > 180 ? look.x - 360 : look.x);
            staff.Resume(false); Physics.SyncTransforms();
        }

        private IEnumerator MoveLeafByHand(float goal)
        {
            door.BeginHold(staff);
            var leaf = door.Leaves[0];
            for (int step = 0; step < 200 && Mathf.Abs(door.Open - goal) > .02f; step++)
            {
                var offset = leaf.ClosedRotation * (Quaternion.Euler(0, leaf.Swing * door.Open, 0) * Vector3.right * (leaf.Length * .5f));
                var local = staff.PlayerCamera.transform.InverseTransformDirection(HandGesture.SwingDirection(leaf.Pivot.parent, offset, Mathf.Sign(leaf.Swing))).normalized;
                var along = new Vector2(local.x, local.y + local.z);
                Assert.That(along.sqrMagnitude, Is.GreaterThan(.01f));
                door.Hold(staff, along.normalized * (Mathf.Sign(goal - door.Open) * 2f), 0, 1f / 30f);
                yield return null;
            }
            door.EndHold(staff);
            Assert.That(door.Open, Is.EqualTo(goal).Within(.025f));
            yield return null;
        }

        private Responder Technician()
        {
            var go = new GameObject("현장 시설 담당 fixture");
            go.transform.SetParent(rig.transform, false); go.SetActive(false);
            var worker = go.AddComponent<Responder>();
            go.GetComponent<NavMeshAgent>().enabled = false; worker.enabled = false;
            Set(worker, "Agency", Agency.Facility); Set(worker, "Team", Team.Facility);
            Set(worker, "Worker", true); Set(worker, "OnScene", true);
            go.transform.position = door.World(new Vector3(.7f, 0, 1.2f)); go.SetActive(true);
            return worker;
        }

        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);

        [UnityTest]
        public IEnumerator ActualLeafRequiresWitnessManualClosureIsolationMaintenanceAndAutomaticReturnTest()
        {
            yield return null; AtHandle();
            yield return MoveLeafByHand(.65f);
            var closedPose = door.Leaves[0].ClosedRotation;
            var sourcePosition = door.transform.position;
            int objectCount = rig.GetComponentsInChildren<Transform>(true).Length;
            fault = door.gameObject.AddComponent<DoorOperatorFault>();
            Assert.That(fault.BeginFailure(door, staff), Is.True);
            Assert.That(rig.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount), "The fault adds a component, never a proxy object or replacement leaf.");
            Assert.That(door.transform.position, Is.EqualTo(sourcePosition));
            Assert.That(door.TrySecondary(staff, out _), Is.False, "An immediate R click cannot diagnose the failed return.");
            Assert.That(fault.CompleteService(), Is.False);
            Assert.That(fault.AuthorizeService(null), Is.False);
            float opened = door.Open;
            yield return new WaitForSeconds(DoorOperatorFault.WitnessSeconds + .5f);
            Assert.That(door.Open, Is.EqualTo(opened).Within(.0001f), "The existing closer driver must actually stop returning the released leaf.");
            AtHandle(false);
            Assert.That(door.TrySecondary(staff, out _), Is.False, "The same on-duty staff cannot diagnose remotely.");
            AtHandle();
            // Walking away resets continuous observation; the staff must watch again.
            yield return new WaitForSeconds(DoorOperatorFault.WitnessSeconds + .5f);
            Assert.That(door.TrySecondary(staff, out _), Is.True);
            Assert.That(fault.Diagnosed, Is.True);
            Assert.That(door.TrySecondary(staff, out _), Is.False, "Control cannot silently close the physically open leaf.");
            yield return MoveLeafByHand(0);
            Assert.That(Quaternion.Angle(door.Leaves[0].Pivot.localRotation, closedPose), Is.LessThan(3f));
            Assert.That(door.TrySecondary(staff, out _), Is.True);
            Assert.That(fault.Secured, Is.True);
            Assert.That(door.Holdable(staff), Is.False);
            Assert.That(door.TryInteract(staff, out _), Is.False, "An isolated leaf cannot be reopened through the tap API.");
            var hazard = new DoorCloserFaultHazard("fixture-fault", fault) { Where = door.Label };
            Assert.That(hazard.WorkSeconds(Agency.Facility), Is.Zero);
            var worker = Technician();
            Assert.That(fault.AuthorizeService(worker), Is.True);
            Assert.That(hazard.WorkSeconds(Agency.Facility), Is.EqualTo(18));
            Assert.That(hazard.Resolve(Agency.Police), Is.Null);
            worker.transform.position = door.World(new Vector3(.7f, 0, 3));
            Assert.That(hazard.Resolve(Agency.Facility), Is.Null, "Timed work cannot repair remotely from an unreachable stand-off.");
            worker.transform.position = door.World(new Vector3(.7f, 0, 1.2f));
            Assert.That(hazard.Resolve(Agency.Facility), Is.Not.Null);
            Assert.That(fault.Serviced, Is.True); Assert.That(hazard.Active, Is.True);
            Assert.That(hazard.ConfirmVerified(), Is.False, "Teamwork alone must not complete the incident.");
            Assert.That(door.TrySecondary(staff, out _), Is.True);
            Assert.That(fault.Secured, Is.False); Assert.That(fault.VerificationReleased, Is.True);
            yield return MoveLeafByHand(.65f);
            Assert.That(fault.Verified, Is.False);
            yield return new WaitForSeconds(StationDoor.CloserDelay + StationDoor.SwingCloseSeconds + 1);
            Assert.That(door.Open, Is.LessThanOrEqualTo(.03f));
            Assert.That(fault.Verified, Is.True, "The real repaired closer must return the same hand-opened leaf while staff watch.");
            Assert.That(hazard.ConfirmVerified(), Is.True); Assert.That(hazard.Active, Is.False);
        }

        [UnityTest]
        public IEnumerator OriginEligibilityRejectsHeldSealedDisabledAndNonSwingDoors()
        {
            yield return null; AtHandle(); yield return MoveLeafByHand(.65f);
            Assert.That(DoorOperatorFault.CanFail(door), Is.True);
            door.BeginHold(staff); Assert.That(DoorOperatorFault.CanFail(door), Is.False); door.EndHold(staff);
            door.Sealed = "현장 접근 불가"; Assert.That(DoorOperatorFault.CanFail(door), Is.False); door.Sealed = "";
            door.enabled = false; Assert.That(DoorOperatorFault.CanFail(door), Is.False); door.enabled = true;
            door.Kind = StationDoor.DoorKind.Sliding; Assert.That(DoorOperatorFault.CanFail(door), Is.False);
        }
    }
}
#endif
