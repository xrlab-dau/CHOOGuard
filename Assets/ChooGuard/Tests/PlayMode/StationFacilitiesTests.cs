using System.Collections;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // 역사 설비 조작(2026-09-28): 문 운전 규칙, 승객이 지나가는 문, 역무원 열쇠(R), 엘리베이터 운행, 에스컬레이터 재가동 조건과 발치 게이트.
    // OS 입력을 합성하지 않는다 — FirstPersonResponder.SetExternalInputMode(true) + StepInput 이 결정론적 경로다.
    public sealed class StationFacilitiesTests
    {
        private GameObject playerObject;
        private FirstPersonResponder responder;
        private readonly System.Collections.Generic.List<GameObject> built = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            StationSignals.Clear();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(10, 1, 10);
            built.Add(floor);
            playerObject = new GameObject("시험 역무원", typeof(CharacterController));
            responder = playerObject.AddComponent<FirstPersonResponder>();
            responder.SetExternalInputMode(true);
            responder.Resume(false);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            StationSignals.Clear();
            foreach (var go in built) if (go != null) Object.Destroy(go);
            built.Clear();
            foreach (var car in Object.FindObjectsByType<ElevatorCar>(FindObjectsSortMode.None)) Object.Destroy(car.gameObject);
            Object.Destroy(playerObject);
        }

        private void Place(Vector3 at, float yaw, float pitch = 0) => responder.RestorePhysicalPose(at, yaw, pitch);

        private void Step(bool interact = false, bool secondary = false) =>
            responder.StepInput(Vector2.zero, Vector2.zero, false, interact, false, .02f, secondary);

        // 문 한 벌: 문 평면은 z = 0, 바깥(공용 쪽)은 +z. 미닫이는 문짝 두 장이 가운데서 갈라진다.
        private StationDoor Door(StationDoor.DoorKind kind, StationDoor.DoorUse use, float width = 1.8f)
        {
            var go = new GameObject("시험 문");
            go.SetActive(false);
            built.Add(go);
            var door = go.AddComponent<StationDoor>();
            door.Kind = kind; door.Use = use; door.Label = "시험 문";
            door.Centre = Vector3.zero; door.Normal = Vector3.forward; door.Along = Vector3.right;
            door.Width = width; door.Height = 2.2f;
            if (kind == StationDoor.DoorKind.Swing)
            {
                // 경첩은 -x 끝, 여는 방향은 -z(방 안쪽): 피벗 +x 가 닫힌 문짝 방향, Euler(0,+θ) 는 x 를 -z 로 돌린다.
                var leaf = Leaf(go.transform, new Vector3(-width * .5f, 0, 0), width);
                door.Leaves = new[] { new StationDoor.Leaf { Pivot = leaf, Swing = 90, Length = width } };
            }
            else
            {
                float half = width * .5f;
                door.Leaves = new[]
                {
                    new StationDoor.Leaf { Pivot = Leaf(go.transform, new Vector3(-half, 0, -.1f), half), Slide = new Vector3(-half, 0, 0) },
                    new StationDoor.Leaf { Pivot = Leaf(go.transform, new Vector3(0, 0, -.1f), half), Slide = new Vector3(half, 0, 0) },
                };
            }
            go.SetActive(true);
            return door;
        }

        private static Transform Leaf(Transform parent, Vector3 at, float width)
        {
            var pivot = new GameObject("문짝").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = at;
            var box = pivot.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(width * .5f, 1.1f, 0);
            box.size = new Vector3(width, 2.2f, .05f);
            return pivot;
        }

        [UnityTest]
        public IEnumerator SlidingDoor_OpensForSomeoneApproaching_AndClosesAfterTheyLeave()
        {
            var door = Door(StationDoor.DoorKind.Sliding, StationDoor.DoorUse.Public);
            Place(new Vector3(0, 0, 6), 180);
            yield return new WaitForSeconds(.5f);
            Assert.That(door.Open, Is.EqualTo(0).Within(.01f), "멀리 있으면 닫혀 있다");
            Place(new Vector3(0, 0, 1.5f), 180);
            yield return new WaitForSeconds(StationDoor.SlideOpenSeconds + .3f);
            Assert.That(door.Open, Is.EqualTo(1).Within(.01f), "감지 범위에 들어오면 다 열린다");
            Place(new Vector3(0, 0, 8), 180);
            yield return new WaitForSeconds(1f);
            Assert.That(door.Open, Is.EqualTo(1).Within(.01f), "떠난 뒤에도 잠시 열어 둔다");
            yield return new WaitForSeconds(StationDoor.SlideHoldSeconds + StationDoor.SlideCloseSeconds);
            Assert.That(door.Open, Is.EqualTo(0).Within(.01f));
        }

        [UnityTest]
        public IEnumerator KeySwitch_CyclesOperatorModes_LockBlocksPaths_AndFireSignalOverridesLock()
        {
            var door = Door(StationDoor.DoorKind.Sliding, StationDoor.DoorUse.Public);
            // 문 위 구동부를 올려다본다(키 스위치).
            Place(new Vector3(0, 0, 1.8f), 180, -24);
            yield return null;
            Step();
            Assert.That(responder.CurrentSecondaryPrompt, Does.Contain("상시 개방"));
            Step(secondary: true); Step();
            Assert.That(door.Mode, Is.EqualTo(StationDoor.OperatorMode.HoldOpen));
            Step(secondary: true); Step();
            Assert.That(door.Mode, Is.EqualTo(StationDoor.OperatorMode.Locked));
            yield return new WaitForSeconds(StationDoor.SlideCloseSeconds + .3f);
            Assert.That(door.Open, Is.EqualTo(0).Within(.01f), "잠금은 사람이 있어도 열지 않는다");
            var block = door.GetComponentInChildren<NavMeshObstacle>();
            Assert.That(block != null && block.enabled && block.carving, "잠그면 승객 길을 깎는다");
            StationSignals.FireAlarm = true;
            yield return new WaitForSeconds(StationDoor.SlideOpenSeconds + .3f);
            Assert.That(door.Open, Is.EqualTo(1).Within(.01f), "화재 신호는 피난 경로 자동문을 연다");
            Assert.That(block.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator TenantShopDoor_HasNoStaffKeySwitch()
        {
            Door(StationDoor.DoorKind.Sliding, StationDoor.DoorUse.Tenant);
            Place(new Vector3(0, 0, 1.8f), 180, -24);
            yield return null;
            Step();
            Assert.That(responder.CurrentPrompt, Does.Contain("점포 관리"));
            Assert.That(responder.CurrentSecondaryPrompt, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StaffDoor_OpensWithKeyFromOutside_LeverFromInside_AndCloserShutsIt()
        {
            var door = Door(StationDoor.DoorKind.Swing, StationDoor.DoorUse.Staff, .9f);
            Place(new Vector3(0, 0, 1.2f), 180);
            yield return null;
            Step();
            Assert.That(responder.CurrentPrompt, Is.EqualTo("E · 열쇠로 열기"));
            Step(interact: true); Step();
            Assert.That(responder.LastFeedback, Is.EqualTo("열쇠로 문을 열었습니다"));
            yield return new WaitForSeconds(StationDoor.SwingOpenSeconds + .2f);
            Assert.That(door.Open, Is.EqualTo(1).Within(.01f));
            Place(new Vector3(0, 0, 6), 180);
            yield return new WaitForSeconds(StationDoor.CloserDelay + StationDoor.SwingCloseSeconds + .5f);
            Assert.That(door.Open, Is.EqualTo(0).Within(.01f), "도어 클로저가 닫는다");
            Place(new Vector3(0, 0, -1.2f), 0);
            yield return null;
            Step();
            Assert.That(responder.CurrentPrompt, Is.EqualTo("E · 열기"), "안에서는 열쇠 없이 레버로 연다");
        }

        [UnityTest]
        public IEnumerator SwingDoor_StopsShortOfTheStaffMemberStandingInItsArc()
        {
            var door = Door(StationDoor.DoorKind.Swing, StationDoor.DoorUse.Public, .9f);
            // 문이 열리는 쪽(-z), 경첩에서 0.6 m, 45° 방향에 선다.
            Place(new Vector3(-.45f + .42f, 0, -.42f), 0);
            yield return null;
            Assert.That(door.TryInteract(responder, out _), Is.True);
            yield return new WaitForSeconds(StationDoor.SwingOpenSeconds + .5f);
            Assert.That(door.Open, Is.GreaterThan(.05f).And.LessThan(.5f), "문짝이 사람에게 닿기 전에 멈춘다");
        }

        [UnityTest]
        public IEnumerator Elevator_CarriesTheStaffMemberToTheFloorPressed()
        {
            Time.timeScale = 4;
            var lower = Landing("1", 0);
            var upper = Landing("2", 4);
            yield return null;
            yield return null;
            var car = lower.Car;
            Assert.That(car, Is.Not.Null, "같은 카 번호의 승강장 두 곳이 한 대가 된다");
            Assert.That(upper.Car, Is.SameAs(car));
            Place(new Vector3(0, 0, 1.2f), 180);
            yield return null;
            Assert.That(lower.TryInteract(responder, out _), Is.True, "승강장 문 앞에서 호출한다");
            yield return new WaitUntil(() => car.DoorsOpenAt(0));
            // 카 안으로 들어가 2층을 누른다.
            Place(new Vector3(0, 0, -1.2f), 180);
            yield return null;
            car.Press(1);
            yield return new WaitUntil(() => car.DoorsOpenAt(1));
            Assert.That(responder.transform.position.y, Is.EqualTo(4).Within(.2f), "닫힌 문 뒤에서 같은 모양의 윗층 카로 옮겨졌다");
            Assert.That(responder.transform.position.z, Is.EqualTo(-1.2f).Within(.05f), "카 안의 자리는 그대로다");
            Assert.That(lower.Open, Is.EqualTo(0).Within(.01f));
            Assert.That(upper.Open, Is.EqualTo(1).Within(.01f));
        }

        [UnityTest]
        public IEnumerator Elevator_DoesNotCloseOnSomeoneInTheDoorway()
        {
            Time.timeScale = 4;
            var lower = Landing("1", 0);
            Landing("2", 4);
            yield return null;
            yield return null;
            var car = lower.Car;
            car.Call(0);
            yield return new WaitUntil(() => car.DoorsOpenAt(0));
            Place(new Vector3(0, 0, 0), 180);
            yield return new WaitForSeconds((ElevatorCar.DwellSeconds + 2) * 4);
            Assert.That(car.Now, Is.EqualTo(ElevatorCar.State.Open), "문턱에 서 있으면 문이 닫히지 않는다(되열림)");
        }

        // 승강장 문(문 평면 z=0, 홀은 +z)과 그 뒤 카(원점 z=-.21, +z 가 카 안쪽 = 월드 -z).
        private StationDoor Landing(string floor, float y)
        {
            var go = new GameObject("시험 승강장 " + floor);
            go.SetActive(false);
            go.transform.position = new Vector3(0, y, 0);
            built.Add(go);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = new Vector3(0, -.05f, 0);
            slab.transform.localScale = new Vector3(8, .1f, 8);
            var door = go.AddComponent<StationDoor>();
            door.Kind = StationDoor.DoorKind.Elevator; door.Use = StationDoor.DoorUse.Public; door.Label = "시험 승강장";
            door.Centre = Vector3.zero; door.Normal = Vector3.forward; door.Along = Vector3.right; door.Width = 1.1f; door.Height = 2.1f;
            door.Floor = floor; door.CarId = "시험 승강기";
            door.Leaves = new[]
            {
                new StationDoor.Leaf { Pivot = Leaf(go.transform, new Vector3(-.55f, 0, -.11f), .55f), Slide = new Vector3(-.55f, 0, 0) },
                new StationDoor.Leaf { Pivot = Leaf(go.transform, new Vector3(0, 0, -.11f), .55f), Slide = new Vector3(.55f, 0, 0) },
            };
            var cabin = new GameObject("승강기 카").transform;
            cabin.SetParent(go.transform, false);
            cabin.localPosition = new Vector3(0, 0, -.21f);
            cabin.localRotation = Quaternion.LookRotation(Vector3.back);
            door.Cabin = cabin;
            door.CabinSize = new Vector3(2f, 2.4f, 1.75f);
            go.SetActive(true);
            return door;
        }

        private Escalator TestEscalator()
        {
            var go = new GameObject("시험 에스컬레이터");
            built.Add(go);
            var escalator = go.AddComponent<Escalator>();
            escalator.Setup(new StationPoints.EscalatorEntry { id = "esc-test", label = "시험 에스컬레이터", path = new[] { Vector3.zero, new Vector3(0, 0, 1), new Vector3(0, 3, 6), new Vector3(0, 3, 7) } });
            return escalator;
        }

        // 한 줄짜리 게이트: 캐비닛 사이 0.6 m 플랩 하나, 들어오는 쪽(-z) 표시등 하나. 발치는 z = 0.
        private EscalatorGate Gate(EscalatorGate.GateMode mode, Escalator escalator)
        {
            var go = new GameObject("시험 게이트");
            go.SetActive(false);
            built.Add(go);
            go.transform.position = new Vector3(0, 0, -1);
            var gate = go.AddComponent<EscalatorGate>();
            gate.Mode = mode;
            gate.Flaps = new[] { new EscalatorGate.Flap { Origin = new Vector3(-.6f, .45f, 0), Across = Vector3.right, ClosedReach = .6f, Height = .45f } };
            gate.EntryLamps = new[] { new EscalatorGate.Lamp { Centre = new Vector3(-.7f, .7f, -.7f), Right = Vector3.left, Out = Vector3.back, Width = .09f, Height = .12f } };
            go.SetActive(true);
            gate.Bind(escalator);
            return gate;
        }

        [UnityTest]
        public IEnumerator EscalatorEntryGate_ShutsAndBarsBoardingWhileTheBeltStands_AndOpensAfterRestart()
        {
            var escalator = TestEscalator();
            var entry = Gate(EscalatorGate.GateMode.Entry, escalator);
            var exit = Gate(EscalatorGate.GateMode.Exit, escalator);
            yield return null;
            Assert.That(entry.Shut, Is.False);
            Assert.That(entry.GetComponentsInChildren<BoxCollider>(), Is.Empty, "운행 중에는 플랩이 접혀 길을 막지 않는다");

            escalator.Stop("비상정지 버튼(역무원)");
            yield return null;
            Assert.That(entry.Shut, Is.True);
            Assert.That(escalator.EntryBarred, Is.True, "멈춘 벨트로 사람을 보내지 않는다");
            Assert.That(entry.GetComponentsInChildren<BoxCollider>(), Is.Not.Empty, "닫힌 플랩이 들어가는 길을 막는다");
            Assert.That(exit.Shut, Is.False, "나가는 쪽 게이트는 열어 둔다");

            Assert.That(escalator.Restart(out _), Is.True);
            yield return null;
            Assert.That(entry.Shut, Is.False);
            Assert.That(escalator.EntryBarred, Is.False);

            escalator.Close();
            yield return null;
            Assert.That(entry.Shut, Is.True, "역무원이 이용을 통제하면 게이트도 닫힌다");
        }

        [Test]
        public void PeopleWalkThroughPublicAndShopDoors_NotStaffSealedOrElevatorDoors()
        {
            Assert.That(Door(StationDoor.DoorKind.Sliding, StationDoor.DoorUse.Public).PassableByPeople, Is.True);
            Assert.That(Door(StationDoor.DoorKind.Sliding, StationDoor.DoorUse.Tenant).PassableByPeople, Is.True);
            Assert.That(Door(StationDoor.DoorKind.Swing, StationDoor.DoorUse.Public).PassableByPeople, Is.True, "공용 여닫이는 밀고 지나간다");
            Assert.That(Door(StationDoor.DoorKind.Swing, StationDoor.DoorUse.Staff).PassableByPeople, Is.False, "관계자 문은 승객이 쓰지 않는다");
            Assert.That(Door(StationDoor.DoorKind.Elevator, StationDoor.DoorUse.Public).PassableByPeople, Is.False, "승강장 문은 차가 연다");
            var sealedDoor = Door(StationDoor.DoorKind.Sliding, StationDoor.DoorUse.Public);
            sealedDoor.Sealed = "닫힘 · 문 바로 뒤를 다른 벽이 막고 있습니다";
            Assert.That(sealedDoor.PassableByPeople, Is.False);
        }

        [Test]
        public void Escalator_RestartsWithTheKeyOnlyWhenStopped_AndNeverAfterAQuakeStop()
        {
            var escalator = TestEscalator();
            Assert.That(escalator.Restart(out _), Is.False, "운행 중에는 재가동할 것이 없다");
            escalator.Stop("비상정지 버튼(역무원)");
            Assert.That(escalator.StoppedBy, Is.EqualTo("비상정지 버튼(역무원)"));
            Assert.That(escalator.Restart(out _), Is.True);
            Assert.That(escalator.Running, Is.True);
            escalator.Stop("지진 감지");
            Assert.That(escalator.Restart(out var reason), Is.False);
            Assert.That(reason, Does.Contain("점검"));
        }
    }
}
