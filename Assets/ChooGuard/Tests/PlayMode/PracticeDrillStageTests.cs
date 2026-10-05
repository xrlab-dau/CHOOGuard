#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// 연습 하네스(<c>ChooGuard.Editor.PracticeDrill</c>, #270)가 돌 **무대만** 띄운다: 역 씬과 근무를
    /// 올리고, 디렉터가 준비된 뒤 정해진 실시간만큼 재생을 붙잡아 둔다.
    /// </summary>
    /// <remarks>
    /// 왜 <see cref="ShiftSampleTests"/> 를 쓰지 않는가: 그 시험은 JEV 키가 있어야 통과한다 — 돌릴 때마다
    /// 유료 호출이 일어난다. 연습은 사건을 **강제로** 일으키므로 JEV 가 필요 없다. 그래서 JEV 를 끈
    /// 무대를 따로 둔다(<see cref="Issue256NavigationSmokeTests"/> 와 같은 방식). 끄면 디렉터가 아무것도
    /// 합성하지 않으므로 근무에서 일어나는 일은 연습이 강제한 그 한 건뿐이다 — 무엇을 봤는지 흐려지지 않는다.
    ///
    /// 이 시험은 연습 도구를 **참조하지 않는다.** 도구는 에디터 어셈블리에서 <c>EditorApplication.update</c>
    /// 에 붙어 재생 중 스스로 돈다. 이쪽은 어셈블리 경계를 넘지 않는다.
    ///
    /// <c>CG_PRACTICE_EXPECT=1</c> 이면 연습 결과 파일이 실제로 늘었는지까지 단정한다. 켜지 않으면 무대
    /// 사실만 본다 — 무장하지 않고 돌렸을 때도 통과하는 검사를 단정으로 두지 않으려는 구분이다.
    /// </remarks>
    public sealed class PracticeDrillStageTests
    {
        private const string DrillFolder = ".planning/2026-10-05-practice-drill";

        private string savedKey;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20260930;
        }

        // 시험이 실패해도 전역을 되돌리고, 근무 씬을 먼저 내린 한 프레임 뒤 역 씬을 내려 다음 시험에 씬을 남기지 않는다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
                EmergencySession.NextSeed = 0;
                Time.captureFramerate = 0;
                HazardRegistry.Clear();
            }
            finally
            {
                Time.timeScale = 1;
            }
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (!emergency.isLoaded && !station.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("PracticeDrillScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        [UnityTest, Explicit("연습 하네스를 먼저 무장해야 한다: -executeMethod ChooGuard.Editor.PracticeDrill.Arm"), Timeout(int.MaxValue)]
        public IEnumerator 근무를_띄워_연습_하네스가_돌_무대를_만든다()
        {
            int before = Drills();

            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_PRACTICE_BOOT", 120f);
            // 역무원이 생기는 대로 근무를 풀어 준다. 멈춘 채로 두면 시계가 0 에 머물러 연습 기록의
            // 시각이 모두 14:00:00 으로 찍힌다 — 첫 실행에서 실제로 그렇게 나왔다.
            while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null
                    || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }

            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Assert.That(session.Incidents, Is.Not.Null, "디렉터(IncidentDirector)가 부팅 안에 준비되지 않았습니다.");
            Assert.That(session.Jev.Available, Is.False,
                "TYPESAFE_API_KEY=off 이면 JEV 는 쓸 수 없어야 합니다: 이 무대에서는 아무것도 합성되지 않아야 합니다.");
            // 질환 원인은 승객에게 붙는다. 사람이 없으면 후보가 오르지 않으므로 무대 조건으로 단정한다.
            Assert.That(session.Crowd.People.Count, Is.GreaterThan(0), "승객이 없으면 질환 계열 후보가 오르지 않습니다.");

            Resume(session);

            // 무대를 붙잡아 둔다. 연습 하네스는 이 사이에 후보를 기다리고 한 건을 일으킨다.
            float hold = Seconds("CG_PRACTICE_WAIT", 150f);
            float until = Time.realtimeSinceStartup + hold;
            while (Time.realtimeSinceStartup < until) yield return null;

            int after = Drills();
            Debug.Log("CG_PRACTICE 무대 " + hold + "초 · 근무 " + session.ShiftSeconds.ToString("0.0")
                      + "게임초 · 승객 " + session.Crowd.People.Count + "명 · 연습 결과 파일 " + before + " → " + after);

            Assert.That(session.ShiftSeconds, Is.GreaterThan(0f),
                "근무 시계가 흐르지 않았습니다. 멈춘 무대에서는 연습 기록의 시각이 모두 같아집니다.");

            if (Environment.GetEnvironmentVariable("CG_PRACTICE_EXPECT") == "1")
                Assert.That(after, Is.GreaterThan(before),
                    "연습 결과 파일이 늘지 않았습니다. 무장(-executeMethod ChooGuard.Editor.PracticeDrill.Arm)이 됐는지, "
                    + "그리고 계열 후보가 올랐는지 확인하십시오.");
        }

        // SetExternalInputMode(true) 는 Pause() 를 부른다 — Resume(false) 로 풀지 않으면 역무원이 얼어붙는다.
        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        private static float Seconds(string variable, float fallback) =>
            float.TryParse(Environment.GetEnvironmentVariable(variable), out var parsed) && parsed > 0 ? parsed : fallback;

        private static int Drills() =>
            Directory.Exists(DrillFolder) ? Directory.GetFiles(DrillFolder, "drill-*.json").Length : 0;
    }
}
#endif
