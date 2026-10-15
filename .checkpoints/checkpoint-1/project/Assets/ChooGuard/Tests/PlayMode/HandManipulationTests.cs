using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Facilities;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    // 손으로 조작하는 설비(E 홀드 + 마우스·휠)의 규칙: 덜 돌린 밸브는 여전히 열려 있고 끝까지 돌려야 알린다, 소화전 밸브는 원 그리기·휠로 열리고 물줄기가 거기에 따른다,
    // 누름 버튼은 끝까지 누르고 있어야 눌린다, 분전반 문은 문짝이 열린 정도로 열림을 가른다.
    public sealed class HandManipulationTests
    {
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("손 조작 시험");
            StationSignals.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            StationSignals.Clear();
        }

        [Test]
        public void AGasValveTurnedPartWayIsStillOpenAndOnlyTheFullTurnAnnouncesClosingOnce()
        {
            var go = new GameObject("밸브");
            go.transform.SetParent(parent.transform);
            var equipment = go.AddComponent<StationEquipment>();
            equipment.Assign("shop-시험/valve-1", GasValvePoint.Kind, "가스 중간밸브", "hall2f", "shop=shop-시험;role=intermediate;feeds=shop-시험/range-1");
            var valve = go.AddComponent<GasValvePoint>();
            valve.Bind();
            int closings = 0;
            valve.Closing += (_, __) => closings++;

            valve.BeginHold(null);
            valve.Hold(null, new Vector2(15, 0), 0, .02f);
            Assert.That(valve.Closed, Is.False, "반쯤 돌린 밸브는 아직 열려 있다");
            Assert.That(valve.Openness, Is.EqualTo(.5f).Within(.01f));
            Assert.That(closings, Is.EqualTo(0), "덜 돌렸을 때는 아무것도 알리지 않는다");
            Assert.That(equipment.State, Is.Not.EqualTo("잠김"));

            valve.Hold(null, new Vector2(30, 0), 0, .02f);
            valve.Hold(null, new Vector2(30, 0), 0, .02f);
            Assert.That(valve.Closed, Is.True);
            Assert.That(valve.Openness, Is.EqualTo(0f));
            Assert.That(equipment.State, Is.EqualTo("잠김"));
            Assert.That(closings, Is.EqualTo(1), "끝까지 돌렸을 때 한 번만 알린다");
        }

        [Test]
        public void AGasValveTurnedBackTowardOpenStaysOpen()
        {
            var go = new GameObject("밸브");
            go.transform.SetParent(parent.transform);
            go.AddComponent<StationEquipment>().Assign("shop-시험/valve-2", GasValvePoint.Kind, "가스 중간밸브", "hall2f", "shop=shop-시험;role=intermediate");
            var valve = go.AddComponent<GasValvePoint>();
            valve.Bind();

            valve.BeginHold(null);
            valve.Hold(null, new Vector2(24, 0), 0, .02f);
            valve.Hold(null, new Vector2(0, 0), 5, .02f);

            Assert.That(valve.Closed, Is.False);
            Assert.That(valve.Openness, Is.EqualTo(1f).Within(.001f), "휠을 위로 굴려 도로 열었다");
        }

        [Test]
        public void AHydrantValveOpensByCirclingTheHandCounterClockwiseAndTheWheelAndTheStreamFollowsIt()
        {
            var valve = new HydrantValve();
            Assert.That(valve.ValveOpen, Is.EqualTo(0f));
            Assert.That(valve.Flow, Is.EqualTo(0f), "잠긴 밸브는 물을 보내지 않는다");

            valve.BeginTurn();
            // 시계 반대로 두 바퀴(15도씩 48번 돌린 방향 변화): 밸브는 한 바퀴.
            for (int k = 0; k <= 48; k++)
            {
                float a = k * 15f * Mathf.Deg2Rad;
                valve.Turn(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 0);
            }
            Assert.That(valve.ValveOpen, Is.EqualTo(.5f).Within(.01f), "손으로 두 바퀴 그리면 밸브 한 바퀴(전체의 반)");
            Assert.That(valve.Flow, Is.EqualTo(Mathf.Pow(valve.ValveOpen, .7f)).Within(1e-4f));

            valve.Turn(Vector2.zero, 16);
            Assert.That(valve.ValveOpen, Is.EqualTo(1f).Within(.001f), "휠 한 칸은 1/16 바퀴, 열림 끝에서 멈춘다");
            Assert.That(valve.Flow, Is.EqualTo(1f).Within(1e-4f));

            // 시계 방향으로 돌리면 다시 잠긴다.
            for (int k = 0; k <= 104; k++)
            {
                float a = -k * 15f * Mathf.Deg2Rad;
                valve.Turn(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 0);
            }
            Assert.That(valve.ValveOpen, Is.EqualTo(0f).Within(.001f));
            Assert.That(valve.Flow, Is.EqualTo(0f));
        }

        [Test]
        public void ACallPointOnlyPressesWhenKeptHeldToTheEnd()
        {
            var go = new GameObject("발신기");
            go.transform.SetParent(parent.transform);
            var button = go.AddComponent<CallPointButton>();

            button.BeginHold(null);
            button.Hold(null, Vector2.zero, 0, .5f);
            button.EndHold(null);
            Assert.That(button.Pressed, Is.False, "일찍 놓으면 아무것도 눌리지 않는다");
            Assert.That(StationSignals.FireAlarm, Is.False);

            button.BeginHold(null);
            button.Hold(null, Vector2.zero, 0, .5f);
            button.Hold(null, Vector2.zero, 0, .4f);
            Assert.That(button.Pressed, Is.True, "0.8초 넘게 누르고 있으면 눌린다");
            Assert.That(StationSignals.FireAlarm, Is.True);
        }

        [Test]
        public void ABoardDoorCountsAsOpenOnlyFromSixtyPercentOfItsSwing()
        {
            var go = new GameObject("분전반 문");
            go.transform.SetParent(parent.transform);
            var door = go.AddComponent<BoardDoor>();
            Assert.That(door.IsShut, Is.True);

            door.SetOpening(.3f);
            Assert.That(door.IsShut, Is.False);
            Assert.That(door.IsOpen, Is.False, "조금 열린 문 뒤의 차단기에는 손이 닿지 않는다");

            door.SetOpening(.7f);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.Opening, Is.EqualTo(.7f).Within(1e-4f));
        }
    }
}
