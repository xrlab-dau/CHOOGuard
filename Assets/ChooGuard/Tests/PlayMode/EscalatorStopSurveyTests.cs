using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    // 에스컬레이터 비상정지 버튼(2026-10-05 실측): 관찰 기록이 있는 승강장에만 버튼이 생기고, 기록의 승강장(아래·위)·쪽(탄 사람의 오른쪽·왼쪽)·
    // 높이·벨트 끝에서의 거리·중심선에서의 거리대로 놓인다. 근거 없는 기록과 읽을 수 없는 기록으로는 버튼이 생기지 않는다.
    public sealed class EscalatorStopSurveyTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private static TextAsset Records(string stops) => new TextAsset("{\"version\":1,\"note\":\"test\",\"stops\":[" + stops + "]}");

        [Test]
        public void OnlyEvidencedButtonsAreBuilt_AtTheirLandingSideAndOffsets()
        {
            var records = EscalatorStopSurvey.Load(Records(
                "{\"escalator\":\"esc-test\",\"landing\":\"bottom\",\"side\":\"right\",\"mount\":\"deck\",\"height\":0.4,\"along\":0.3,\"offset\":0.6,\"placementEvidence\":\"test fixture only: observed at the bottom landing\"}," +
                "{\"escalator\":\"esc-test\",\"landing\":\"top\",\"side\":\"left\",\"mount\":\"newel\",\"height\":0.9,\"along\":0.2,\"offset\":0.55,\"placementEvidence\":\"test fixture only: observed at the top landing\"}," +
                "{\"escalator\":\"esc-test\",\"landing\":\"top\",\"side\":\"right\",\"height\":0.9,\"placementEvidence\":\"\"}," +
                "{\"escalator\":\"esc-test\",\"landing\":\"middle\",\"side\":\"right\",\"height\":0.9,\"placementEvidence\":\"test fixture only: unreadable landing\"}"));
            Assert.That(records.Count, Is.EqualTo(2), "근거 없는 기록과 승강장을 읽을 수 없는 기록은 빠진다");

            // 내려가는 에스컬레이터: 벨트는 위(7 m, z 0)에서 +z 로 아래(0 m, z 12)까지 간다. 탄 사람은 +z 를 보므로 오른쪽은 +x 다.
            // 타는·내리는 계단참 점은 navmesh 에 맞추느라 옆으로 비켜 있다(실제 우물 경로처럼). 버튼은 그 점이 아니라 벨트 끝을 기준으로 놓인다.
            root = new GameObject("시험 에스컬레이터");
            var escalator = root.AddComponent<Escalator>();
            escalator.Setup(new StationPoints.EscalatorEntry { id = "esc-test", label = "시험", path = new[] { new Vector3(1.4f, 7, -1), new Vector3(0, 7, 0), new Vector3(0, 0, 12), new Vector3(-1.2f, 0, 13) } });
            escalator.AddStopButtons(records);

            Assert.That(escalator.StopButtons, Is.EqualTo(2));
            Assert.That(escalator.HasStopButton, Is.True);
            var buttons = root.GetComponentsInChildren<EscalatorStopButton>();
            Assert.That(buttons.Length, Is.EqualTo(2));
            // 아래 승강장(벨트 끝 (0, 0, 12), 내리는 곳): 바깥은 +z, 오른쪽은 +x.
            var bottom = new Vector3(.6f, .4f, 12.3f);
            // 위 승강장(벨트 시작 (0, 7, 0), 타는 곳): 바깥은 -z, 왼쪽은 -x.
            var top = new Vector3(-.55f, 7.9f, -.2f);
            Assert.That(System.Array.Exists(buttons, b => (b.transform.position - bottom).sqrMagnitude < 1e-4f), "아래 승강장의 오른쪽 데크");
            Assert.That(System.Array.Exists(buttons, b => (b.transform.position - top).sqrMagnitude < 1e-4f), "위 승강장의 왼쪽 난간");
        }

        [Test]
        public void AnEscalatorWithoutRecordsHasNoStopButton()
        {
            root = new GameObject("시험 에스컬레이터");
            var escalator = root.AddComponent<Escalator>();
            escalator.Setup(new StationPoints.EscalatorEntry { id = "esc-unsurveyed", label = "시험", path = new[] { new Vector3(0, 0, 0), new Vector3(0, 7, 12) } });
            escalator.AddStopButtons(EscalatorStopSurvey.Load(Records("{\"escalator\":\"esc-other\",\"landing\":\"bottom\",\"side\":\"right\",\"height\":0.4,\"placementEvidence\":\"test fixture only\"}")));

            Assert.That(escalator.HasStopButton, Is.False);
            Assert.That(root.GetComponentsInChildren<EscalatorStopButton>(), Is.Empty);
        }
    }
}
