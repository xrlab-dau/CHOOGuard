using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Equipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace ChooGuard.Tests.PlayMode
{
    // 소화·방화구획 설비의 소비자 관점 규칙: 방화셔터의 2단 하강과 복구 절차, 터진 스프링클러 헤드의 표시, 노출 배관 한 줄이 한 덩어리 메시라는 것.
    public sealed class FireSafetyEquipmentTests
    {
        private readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();
        private float timeScale;

        [SetUp]
        public void SetUp() => timeScale = Time.timeScale;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = timeScale;
            foreach (var item in made) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            made.Clear();
            EquipmentRegistry.Clear();
        }

        private static IEnumerator Until(Func<bool> condition, float seconds = 8f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        // ── 방화셔터 ──

        private FireShutterPoint MakeShutter(string data = "width=5;height=3.4")
        {
            var root = new GameObject("방화셔터 시험");
            made.Add(root);
            var equipment = root.AddComponent<StationEquipment>();
            equipment.Assign("fs-test", FireShutterPoint.ShutterKind, "방화셔터 시험", "hall2f", data);
            var shutter = root.AddComponent<FireShutterPoint>();
            var curtain = new GameObject("Curtain");
            curtain.transform.SetParent(root.transform, false);
            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(root.transform, false);
            blocker.AddComponent<BoxCollider>().enabled = false;
            var obstacle = new GameObject("Obstacle");
            obstacle.transform.SetParent(root.transform, false);
            obstacle.AddComponent<NavMeshObstacle>().enabled = false;
            shutter.OnPlaced();
            Time.timeScale = 30f;
            return shutter;
        }

        [UnityTest]
        public IEnumerator SmokeLowersTheCurtainToTheGapAndHeatClosesItFully()
        {
            var shutter = MakeShutter();

            shutter.Trigger(false, "연기감지기");
            yield return Until(() => !shutter.Moving);
            Assert.That(shutter.ClearHeight, Is.EqualTo(FireShutterPoint.GapHeight).Within(.02f), "연기 감지는 사람이 빠져나갈 틈을 남기고 멈춘다");
            Assert.That(shutter.Blocking, Is.True, "틈이 있어도 서서 지나갈 수는 없다");

            shutter.Trigger(true, "열감지기");
            yield return Until(() => !shutter.Moving);
            Assert.That(shutter.Opening, Is.EqualTo(0f), "열 감지는 바닥까지 내린다");
        }

        [UnityTest]
        public IEnumerator AHeatSignalFirstSkipsTheGap()
        {
            var shutter = MakeShutter();

            shutter.Trigger(true, "열감지기");
            yield return Until(() => shutter.ClearHeight < FireShutterPoint.GapHeight - .1f);

            Assert.That(shutter.Target, Is.EqualTo(0f));
            Assert.That(shutter.Moving, Is.True, "열이 먼저면 1단의 틈에서 멈추지 않고 그대로 바닥까지 내려간다");
        }

        [UnityTest]
        public IEnumerator WhileTheDetectorSignalLastsNothingRaisesTheCurtainAndAfterItOnlyTheControlBoxDoes()
        {
            var shutter = MakeShutter();
            shutter.Trigger(true, "열감지기");
            yield return Until(() => !shutter.Moving);

            Assert.That(shutter.Operate(FireShutterPoint.Command.Up, "역무원"), Is.Not.Null, "신호가 남아 있으면 상향 버튼은 듣지 않는다");
            Assert.That(shutter.Operate(FireShutterPoint.Command.Reset, "역무원"), Is.Not.Null, "수신기 복구 전에는 복구 스위치도 듣지 않는다");
            yield return null;
            Assert.That(shutter.Opening, Is.EqualTo(0f));

            shutter.ClearTrigger();
            yield return Until(() => !shutter.Moving, 1f);
            Assert.That(shutter.Opening, Is.EqualTo(0f), "신호가 사라져도 스스로 올라가지는 않는다");
            Assert.That(shutter.Operate(FireShutterPoint.Command.Up, "역무원"), Is.Null);
            yield return Until(() => !shutter.Moving);
            Assert.That(shutter.Opening, Is.EqualTo(1f));
            Assert.That(shutter.Blocking, Is.False);
        }

        [UnityTest]
        public IEnumerator AFailedControllerNeedsTheResetSwitchBeforeTheCurtainRises()
        {
            var shutter = MakeShutter();
            shutter.Malfunction(0f, true);
            yield return Until(() => !shutter.Moving);

            Assert.That(shutter.Opening, Is.EqualTo(0f));
            Assert.That(shutter.Operate(FireShutterPoint.Command.Up, "역무원"), Is.Not.Null, "고장 난 제어기는 상향 버튼을 받지 않는다");

            Assert.That(shutter.Operate(FireShutterPoint.Command.Reset, "역무원"), Is.Null);
            Assert.That(shutter.ControllerFault, Is.False);
            Assert.That(shutter.SensorFailed, Is.False, "복구하면 장애물 감지가 다시 산다");
            yield return Until(() => !shutter.Moving);
            Assert.That(shutter.Opening, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator StopHoldsTheCurtainWhereItIsAndTheOpeningIsBlockedOnlyWhileItIsLow()
        {
            var shutter = MakeShutter();
            var blocker = shutter.GetComponentInChildren<BoxCollider>(true);
            var obstacle = shutter.GetComponentInChildren<NavMeshObstacle>(true);
            Assert.That(blocker.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.False);

            shutter.Trigger(true, "열감지기");
            yield return Until(() => shutter.ClearHeight < 2.6f);
            Assert.That(shutter.Operate(FireShutterPoint.Command.Stop, "역무원"), Is.Null);
            yield return Until(() => !shutter.Moving, 1f);
            float held = shutter.Opening;
            yield return null;
            Assert.That(shutter.Opening, Is.EqualTo(held), "정지 버튼은 그 자리에서 멈춘다");
            Assert.That(held, Is.GreaterThan(0f).And.LessThan(1f));

            shutter.ClearTrigger();
            shutter.Operate(FireShutterPoint.Command.Down, "역무원");
            yield return Until(() => !shutter.Moving);
            Assert.That(blocker.enabled, Is.True, "내려온 커튼은 플레이어를 막는다");
            Assert.That(obstacle.enabled, Is.True, "내려온 커튼은 군중의 길을 막는다");
            shutter.Operate(FireShutterPoint.Command.Up, "역무원");
            yield return Until(() => !shutter.Moving);
            Assert.That(blocker.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.False);
        }

        // ── 방화문 ──

        private FireDoorPoint MakeDoor(string data)
        {
            var root = new GameObject("방화문 시험");
            made.Add(root);
            root.AddComponent<StationEquipment>().Assign("fd-test", FireDoorPoint.DoorKind, "방화문 시험", "hall2f", data);
            var transomPart = new GameObject("Transom", typeof(MeshFilter), typeof(MeshRenderer));
            transomPart.transform.SetParent(root.transform, false);
            foreach (var name in new[] { "LeafLeft", "LeafRight" }) new GameObject(name).transform.SetParent(root.transform, false);
            var door = root.AddComponent<FireDoorPoint>();
            door.OnPlaced();
            Time.timeScale = 20f;
            return door;
        }

        private FirstPersonResponder MakePerson(Vector3 at)
        {
            var person = new GameObject("역무원 시험", typeof(CharacterController));
            made.Add(person);
            person.transform.position = at;
            person.AddComponent<FirstPersonResponder>();
            return person.GetComponent<FirstPersonResponder>();
        }

        [UnityTest]
        public IEnumerator AClosedFireDoorOpensBothLeavesForAPersonAtItAndTheCloserShutsThemAfterTheyGo()
        {
            var door = MakeDoor("ceiling=3.4");
            var left = door.transform.Find("LeafLeft");
            var right = door.transform.Find("LeafRight");
            Assert.That(door.Closed, Is.True);
            Assert.That(door.Equipment.State, Is.EqualTo("닫힘"));
            var person = MakePerson(door.transform.position + door.transform.forward * 1.2f);
            // 문은 일정한 속도로 열리고 끝까지 가야 '열림'이다: 0.5° 앞에서 멈춰 보면 그 프레임이 마지막 반 도 안에 걸릴 때 '열리는 중'이다.
            yield return Until(() => Mathf.Approximately(door.Angle, FireDoorPoint.MaxAngle));
            Assert.That(door.Angle, Is.EqualTo(FireDoorPoint.MaxAngle).Within(.5f), "열린 채 사람이 서 있는 동안 문은 끝까지 열려 있다");
            Assert.That(left.localEulerAngles.y, Is.EqualTo(FireDoorPoint.MaxAngle).Within(.5f));
            Assert.That(right.localEulerAngles.y, Is.EqualTo(360f - FireDoorPoint.MaxAngle).Within(.5f), "오른쪽 문짝은 반대로 열린다");
            Assert.That(door.Equipment.State, Is.EqualTo("열림"));
            person.transform.position = door.transform.position + door.transform.forward * 20f;
            yield return Until(() => door.Closed);
            Assert.That(door.Closed, Is.True, "사람이 떠나면 도어클로저가 문을 닫는다");
            Assert.That(left.localEulerAngles.y, Is.EqualTo(0f).Within(.5f));
        }

        [Test]
        public void TheTransomFillsTheOpeningFromTheFrameHeadUpToTheCeilingOnlyWhereThereIsCeilingAboveIt()
        {
            var high = MakeDoor("ceiling=3.4");
            var part = high.transform.Find("Transom");
            var mesh = part.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(part.gameObject.activeSelf, Is.True);
            Assert.That(mesh.subMeshCount, Is.EqualTo(2), "steel frame and plaster infill");
            Assert.That(mesh.bounds.min.y, Is.EqualTo(FireDoorPoint.FrameHeight).Within(.001f));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(3.4f).Within(.001f));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(FireDoorPoint.FrameWidth).Within(.001f));
            var low = MakeDoor("ceiling=2.1");
            Assert.That(low.transform.Find("Transom").gameObject.activeSelf, Is.False, "no room above the frame, no transom");
        }

        // ── 경종과 수신기 ──

        private StationEquipment MakeBell(string id, Vector3 at)
        {
            var go = new GameObject(id);
            made.Add(go);
            go.transform.position = at;
            var equipment = go.AddComponent<StationEquipment>();
            equipment.Assign(id, AlarmBellSounder.BellKind, "경종 시험", "hall2f");
            return equipment;
        }

        private AlarmBellSounder MakeSounder(Transform ear)
        {
            var go = new GameObject("경종 소리 시험");
            made.Add(go);
            var sounder = go.AddComponent<AlarmBellSounder>();
            var clip = AudioClip.Create("경종 시험", 4800, 1, 48000, false);
            made.Add(clip);
            sounder.Setup(clip, .2f, ear);
            return sounder;
        }

        [UnityTest]
        public IEnumerator TheBellVoicesStayOnTheNearestBellsAndFollowTheListenerWithoutRestartingTheOnesThatStay()
        {
            var ear = new GameObject("귀").transform;
            made.Add(ear.gameObject);
            // 종 열 개가 10 m 간격으로 줄지어 있다.
            var bells = Enumerable.Range(0, 10).Select(i => MakeBell("bell-test-" + i, new Vector3(i * 10f, 2.4f, 0))).ToList();
            var sounder = MakeSounder(ear);
            Assert.That(sounder.Heard.All(b => b == null), Is.True, "울리기 전에는 소리를 내는 종이 없다");
            ear.position = new Vector3(0, 1.7f, 0);
            sounder.Ring(true);
            yield return null;
            yield return Until(() => sounder.Heard.Count(b => b != null) == AlarmBellSounder.Voices);
            var first = sounder.Heard.Where(b => b != null).Select(b => b.Id).OrderBy(id => id).ToList();
            Assert.That(first, Is.EqualTo(Enumerable.Range(0, AlarmBellSounder.Voices).Select(i => "bell-test-" + i).OrderBy(id => id).ToList()), "듣는 사람에게 가장 가까운 여섯 개");
            int keptVoice = System.Array.IndexOf(sounder.Heard.ToArray(), bells[3]);
            ear.position = new Vector3(32f, 1.7f, 0);
            yield return Until(() => sounder.Heard.Contains(bells[6]));
            var second = sounder.Heard.Where(b => b != null).Select(b => b.Id).OrderBy(id => id).ToList();
            Assert.That(second, Is.EqualTo(Enumerable.Range(1, AlarmBellSounder.Voices).Select(i => "bell-test-" + i).OrderBy(id => id).ToList()), "걸음을 옮기면 멀어진 종의 목소리가 새로 가까워진 종으로 옮겨 간다");
            Assert.That(sounder.Heard[keptVoice], Is.SameAs(bells[3]), "계속 가까운 종은 같은 목소리를 지킨다(다시 울리지 않는다)");
            sounder.Ring(false);
            Assert.That(sounder.Heard.All(b => b == null), Is.True, "경보가 멈추면 모든 목소리가 멈춘다");
        }

        [Test]
        public void TheReceiverScreenReadsNormalOrTheFireZonesAndIgnoresAnUnchangedPicture()
        {
            var go = new GameObject("수신기 시험");
            made.Add(go);
            go.AddComponent<StationEquipment>().Assign("rx-test", AlarmPanelPoint.PanelKind, "수신기 시험", "hall2f");
            var panel = go.AddComponent<AlarmPanelPoint>();
            panel.Show(AlarmPanelPoint.Tone.Fire, new[] { "감지 2층 4구역", "유수 2층 3방호구역" });
            Assert.That(panel.Shown, Does.Contain("화재").And.Contain("감지 2층 4구역").And.Contain("유수 2층 3방호구역"));
            Assert.That(panel.Signal, Is.EqualTo(AlarmPanelPoint.Tone.Fire));
            panel.Show(AlarmPanelPoint.Tone.Normal, new string[0]);
            Assert.That(panel.Shown, Does.Contain("정상").And.Not.Contain("2층"), "복구하면 구역 표시가 사라진다");
            panel.Show(AlarmPanelPoint.Tone.Supervisory, Enumerable.Range(1, 9).Select(i => i + "번 구역").ToList());
            Assert.That(panel.Shown.Split('\n').Length, Is.EqualTo(6), "제목과 다섯 줄까지만 화면에 담는다");
        }

        // ── 스프링클러 헤드 ──

        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ABurstHeadKeepsItsBulbOffWhenTheCameraComesBackWithinDrawDistance()
        {
            var prefab = new GameObject("SprinklerHead");
            made.Add(prefab);
            var template = prefab.AddComponent<StationEquipment>();
            template.DrawDistance = 20f;
            template.Batchable = false;
            prefab.AddComponent<SprinklerHeadPoint>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(prefab.transform, false);
            body.transform.localScale = Vector3.one * .03f;
            var live = new GameObject(EquipmentSpawner.LiveChild);
            live.transform.SetParent(prefab.transform, false);
            var burst = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(burst.GetComponent<Collider>());
            burst.name = "Burst";
            burst.transform.SetParent(live.transform, false);
            burst.transform.localScale = Vector3.one * .03f;
            live.SetActive(false);
            EquipmentRegistry.Unregister(template);
            var catalog = ScriptableObject.CreateInstance<EquipmentCatalog>();
            made.Add(catalog);
            catalog.Prefabs = new[] { prefab };
            catalog.Placements = new[] { new TextAsset("{\"group\":\"test\",\"items\":[{\"id\":\"sh-test\",\"kind\":\"sprinkler_head\",\"label\":\"헤드\",\"zone\":\"hall2f\",\"prefab\":\"SprinklerHead\",\"placementEvidence\":\"test fixture only: head at (0,5,0)\",\"position\":{\"x\":0,\"y\":5,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0},\"data\":\"valve=v1;floor=2\"}]}") };
            var cameraObject = new GameObject("시험 카메라", typeof(Camera));
            made.Add(cameraObject);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 2, 3);
            var parent = new GameObject("설비 시험");
            made.Add(parent);
            EquipmentSpawner.Spawn(catalog, parent.transform);
            var head = EquipmentRegistry.Find("sh-test").GetComponent<SprinklerHeadPoint>();
            head.Bind();
            var intact = head.transform.Find("Body").GetComponent<Renderer>();
            var frame = head.transform.Find(EquipmentSpawner.LiveChild + "/Burst").GetComponent<Renderer>();

            head.Activate(null, false);
            Assert.That(intact.enabled, Is.False, "터진 헤드는 유리관이 사라진다");
            cameraObject.transform.position = new Vector3(300, 2, 0);
            yield return new WaitForSecondsRealtime(.6f);
            cameraObject.transform.position = new Vector3(0, 2, 3);
            yield return new WaitForSecondsRealtime(.6f);

            Assert.That(intact.enabled, Is.False, "다시 가까이 가도 터진 헤드의 유리관이 되살아나지 않는다");
            Assert.That(frame.enabled, Is.True);

            head.Replace();
            Assert.That(intact.enabled, Is.True, "헤드를 갈면 다시 온전하다");
        }

        // ── 노출 배관 ──

        private Mesh RunMesh() => new Mesh
        {
            vertices = new[] { new Vector3(0, -.03f, -.03f), new Vector3(0, .03f, -.03f), new Vector3(3, .03f, -.03f), new Vector3(3, -.03f, -.03f), new Vector3(0, 0, .03f), new Vector3(3, 0, .03f) },
            triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 4, 5, 0, 5, 3 },
            normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward },
        };

        private SprinklerPipeLine MakePipe(string data)
        {
            var root = new GameObject("배관 시험");
            made.Add(root);
            root.AddComponent<StationEquipment>().Assign("sp-test", SprinklerPipeLine.PipeKind, "배관", "hall2f", data);
            var pipe = root.AddComponent<SprinklerPipeLine>();
            var mesh = RunMesh();
            made.Add(mesh);
            typeof(SprinklerPipeLine).GetField("runMesh", Private).SetValue(pipe, mesh);
            typeof(SprinklerPipeLine).GetField("hangerMesh", Private).SetValue(pipe, mesh);
            return pipe;
        }

        [Test]
        public void AnExposedPipeIsBuiltAsOneRunMeshThatSpansItsWholeLengthWithAHangerPerPipeLength()
        {
            var pipe = MakePipe("a=0,6,0;b=9,6,0;dn=65;role=branch;exposed=1;heads=3;ceil=6.4;floor=0");

            pipe.OnPlaced();

            var run = pipe.transform.Find("Run").GetComponent<MeshFilter>().sharedMesh;
            var hangers = pipe.transform.Find("Hanger").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(run.vertexCount, Is.EqualTo(3 * 6), "9 m 는 3 m 배관 셋");
            Assert.That(hangers.vertexCount, Is.EqualTo(3 * 6), "배관 한 토막마다 행거 하나");
            Assert.That(run.bounds.size.x, Is.EqualTo(9f).Within(.05f));
            Assert.That(pipe.Length, Is.EqualTo(9f).Within(.001f));
            Assert.That(pipe.JointNear(.34f), Is.EqualTo(new Vector3(3, 6, 0)).Using(new Vector3EqualityComparer(.001f)), "이음은 3 m 마다");
        }

        [Test]
        public void APipeAboveAFinishedCeilingIsARecordWithoutAMeshThatStillKnowsItsEndsAndItsZone()
        {
            var pipe = MakePipe("a=0,4,0;b=6,4,0;dn=50;role=main;exposed=0;valve=sp-1-0-0;heads=8;floor=0");

            pipe.OnPlaced();

            Assert.That(pipe.transform.childCount, Is.EqualTo(0), "반자 안 배관은 그려지지 않는다");
            Assert.That(pipe.Exposed, Is.False);
            Assert.That(pipe.Valve, Is.EqualTo("sp-1-0-0"));
            Assert.That(pipe.PointAt(.5f), Is.EqualTo(new Vector3(3, 4, 0)).Using(new Vector3EqualityComparer(.001f)));
        }
    }
}
