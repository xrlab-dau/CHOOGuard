#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
    public sealed class PracticeDrillStageTests : RetainedEmergencySessionFixture
    {
        private const string DrillFolder = ".planning/2026-10-30-cardiac-arrest-solo-scenario/drills";

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
            var known = new HashSet<string>(DrillFiles());

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
            // CG_PRACTICE_FILM=<폴더> 면 역무원 시점을 ShiftSampleTests 의 촬영기로 찍는다(복제하지 않는다).
            // -nographics 로 돌리면 빈 화면이 나오므로 촬영할 때는 그 옵션을 빼야 한다.
            // 사건이 자리를 잡으면 현장이 보이는 자리에 역무원을 세우고(걸어간 것이 아니다 — 하네스가 세운 것이다),
            // 구급대장이 현장에 도착해 인계를 받을 수 있게 되면 하네스가 인계해 근무를 끝낸다 — 영상이 시작부터 끝까지 이어지게.
            float hold = Seconds("CG_PRACTICE_WAIT", 150f);
            float until = Time.realtimeSinceStartup + hold;
            var film = Environment.GetEnvironmentVariable("CG_PRACTICE_FILM");
            var recorder = string.IsNullOrEmpty(film) ? null : new ShiftSampleTests.Recorder(film, Seconds("CG_PRACTICE_FILM_EVERY_MS", 200f) / 1000f);
            bool autoHandover = Environment.GetEnvironmentVariable("CG_PRACTICE_HANDOVER") != "0";
            Hazard filmed = null;
            float endAt = float.PositiveInfinity;
            try
            {
                while (Time.realtimeSinceStartup < until && Time.realtimeSinceStartup < endAt)
                {
                    var main = session.Incidents.Main;
                    if (main != null && main != filmed && main.Localized)
                    {
                        filmed = main;
                        Face(session, main);
                    }
                    if (autoHandover && main != null && float.IsPositiveInfinity(endAt))
                    {
                        foreach (var responder in UnityEngine.Object.FindObjectsByType<Responder>(FindObjectsSortMode.None))
                        {
                            if (!session.Incidents.CanHandOver(responder)) continue;
                            // 제품은 인계 전에 Q 업무·인계의 네 항목 검토를 요구한다. 첫 호출이 검토를 열고, 네 항목은 그 무전
                            // 묶음의 선택지다 — 하네스가 전부 보내고 다시 인계한다(역무원이 누른 것이 아니다). 3초 뒤 근무가 끝난다.
                            var first = session.Incidents.HandOver(responder);
                            Debug.Log("CG_PRACTICE 하네스가 " + responder.name + " 에게 현장 인계 시도 · " + first);
                            int reviewed = 0;
                            foreach (var provider in session.RadioGroupProviders.ToArray())
                                foreach (var group in provider() ?? Array.Empty<EmergencySession.RadioGroup>())
                                {
                                    if (group.Label != "업무·인계" || group.Options == null) continue;
                                    foreach (var option in group.Options) { option.Send?.Invoke(); reviewed++; }
                                }
                            var second = session.Incidents.HandOver(responder);
                            Debug.Log("CG_PRACTICE 하네스가 업무·인계 항목 " + reviewed + "개 검토 후 다시 인계 · " + second);
                            endAt = Time.realtimeSinceStartup + 10f;   // 인계 문장과 근무 종료(3초 뒤)를 찍는다.
                            break;
                        }
                    }
                    if (recorder != null) recorder.Step(session, Time.deltaTime);
                    yield return null;
                }
            }
            finally
            {
                if (recorder != null)
                {
                    Debug.Log("CG_PRACTICE_FILM 프레임 " + recorder.Frames + "장 → " + film);
                    recorder.Close();
                }
            }

            int after = Drills();
            Debug.Log("CG_PRACTICE 무대 " + hold + "초 · 근무 " + session.ShiftSeconds.ToString("0.0")
                      + "게임초 · 승객 " + session.Crowd.People.Count + "명 · 연습 결과 파일 " + before + " → " + after);

            Assert.That(session.ShiftSeconds, Is.GreaterThan(0f),
                "근무 시계가 흐르지 않았습니다. 멈춘 무대에서는 연습 기록의 시각이 모두 같아집니다.");

            if (Environment.GetEnvironmentVariable("CG_PRACTICE_EXPECT") == "1")
            {
                Assert.That(after, Is.GreaterThan(before),
                    "연습 결과 파일이 늘지 않았습니다. 무장(-executeMethod ChooGuard.Editor.PracticeDrill.Arm)이 됐는지, "
                    + "그리고 계열 후보가 올랐는지 확인하십시오.");
                // 심정지 계열(CG_PRACTICE_FAMILY=심정지)이면 기록으로 단정한다: 가슴 통증 한 건, 시작 단계는 즉시형 4 / 악화형 3,
                // 끝 단계는 4(호흡 비정상 = 심정지). 도구를 참조하지 않고 결과 파일만 읽는다 — 어셈블리 경계는 그대로다.
                if (Environment.GetEnvironmentVariable("CG_PRACTICE_FAMILY") == "심정지")
                {
                    var record = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(NewDrill(known)));
                    Assert.That((string)record["family"], Is.EqualTo("심정지"));
                    Assert.That((string)record["result"], Is.EqualTo("reported"), "심정지 한 건이 보고되지 않았습니다: " + record["result"] + " " + record["error"]);
                    Assert.That((string)record["kind"], Is.EqualTo("chest_pain"), "심정지 계열은 가슴 통증 조건만 고른다.");
                    bool worsen = (string)record["onset"] == "worsen";
                    Assert.That((int)record["startLevel"], Is.EqualTo(worsen ? 3 : 4), "시작 단계: 즉시형 4, 악화형 3.");
                    // 악화형은 역무원이 AED 를 못 가져온 채 시간이 지나면 4단계가 된다. 그 전에 구급대가 처치했으면
                    // 올리지 않는 것이 맞으므로(도구의 규칙), 그 경우만 3단계 그대로를 허용하고 기록(treated)으로 이유를 단정한다.
                    bool treatedFirst = worsen && record["worsenedAt"].Type == Newtonsoft.Json.Linq.JTokenType.Null && (bool?)record["treated"] == true;
                    if (treatedFirst)
                        Debug.Log("CG_PRACTICE 악화 전에 구급대가 처치 · 3단계 그대로 (" + record["handover"] + ")");
                    else
                    {
                        Assert.That((int)record["level"], Is.EqualTo(4), "끝 단계는 4(의식 없음·호흡 비정상)여야 심정지다.");
                        Assert.That((bool)record["notBreathingNormally"], Is.True);
                        if (worsen) Assert.That(record["worsenedAt"].Type, Is.Not.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null),
                            "악화형은 악화 시각이 기록돼야 한다(처치가 먼저였으면 treated 가 true 여야 한다).");
                    }
                    var guidance = (Newtonsoft.Json.Linq.JArray)record["guidance"];
                    Assert.That(guidance.Count, Is.EqualTo(1), "위험은 그 한 건뿐이어야 한다.");
                    if (!worsen)
                        Assert.That((string)guidance[0]["officeReply"], Does.Contain("자동심장충격기"),
                            "4단계로 시작했으면 역무실 지시에 AED 문장이 붙어야 한다.");
                }
            }
        }

        private static string[] DrillFiles() =>
            Directory.Exists(DrillFolder) ? Directory.GetFiles(DrillFolder, "drill-*.json") : Array.Empty<string>();

        /// <summary>
        /// 이번 실행이 새로 쓴 기록 하나. 이름(초 단위 시각)으로 "최신"을 집으면 하네스가 아직 저장하지 못한 채
        /// 무대가 끝났을 때 이전 실행의 파일을 읽게 되므로, 시작 전 집합과의 차이로 고른다.
        /// </summary>
        private static string NewDrill(HashSet<string> known)
        {
            var fresh = DrillFiles().Where(file => !known.Contains(file)).ToArray();
            Assert.That(fresh.Length, Is.EqualTo(1), "이번 실행이 쓴 연습 기록이 정확히 하나여야 합니다: " + fresh.Length + "개");
            return fresh[0];
        }

        /// <summary>
        /// 역무원을 현장이 보이는 자리에 세우고 시선을 맞춘다. 카메라가 역무원 시점이라, 현장을 보고 있지 않으면
        /// 빈 복도만 찍힌다. 걸어간 것이 아니다 — 하네스가 세운 것이다(#270 촬영 방식 그대로). 세운 자리는 현장이
        /// 막는 땅 밖(ApproachRadius)이고 navmesh 위로 스냅하며, 실제로 보이는지 Raycast 로 잰다.
        /// </summary>
        private static void Face(EmergencySession session, Hazard hazard)
        {
            var player = session.Player;
            float ring = Mathf.Max(3f, hazard.ApproachRadius);
            var eye = Vector3.up * player.EyeHeight;
            // 바닥에 누운 환자 위 한 뼘을 본다. 바닥이나 환자 자신의 콜라이더에 가로막히지 않게.
            var look = hazard.Position + Vector3.up * .6f;
            var here = player.transform.position;
            float fromAngle = Mathf.Atan2(here.x - hazard.Position.x, here.z - hazard.Position.z) * Mathf.Rad2Deg;
            Vector3 stand = Vector3.zero;
            bool found = false;
            int walkable = 0;
            // 지금 역무원이 선 쪽부터 좌우로 번갈아 넓혀 간다 — 자연스러운 접근 방향을 먼저 쓴다.
            for (int step = 0; step < 16 && !found; step++)
            {
                float angle = fromAngle + (step % 2 == 0 ? 1 : -1) * (step / 2) * 22.5f;
                var guess = hazard.Position + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad)) * ring;
                if (!NavMesh.SamplePosition(guess, out var hit, 2f, NavMesh.AllAreas)) continue;
                walkable++;
                // 환자 바로 앞까지만 잰다. 끝점까지 재면 환자 자신의 몸이 장애물로 잡힌다(쓰러진 승객의 콜라이더가 선 키 그대로다).
                var line = look - (hit.position + eye);
                float reach = Mathf.Max(.5f, line.magnitude - 1.5f);
                if (Physics.Raycast(hit.position + eye, line.normalized, out var blocker, reach, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (walkable <= 3)
                        Debug.Log("CG_PRACTICE 시야 막힘 진단 · " + blocker.collider.name + " · " + blocker.distance.ToString("0.0") + "m / " + reach.ToString("0.0") + "m");
                    continue;
                }
                stand = hit.position;
                found = true;
            }
            if (!found)
            {
                Debug.LogWarning("CG_PRACTICE 현장이 보이는 자리를 찾지 못했습니다 (걷는 땅 " + walkable + "곳 모두 시야가 막힘). 역무원을 옮기지 않습니다 · " + hazard.Where);
                return;
            }
            var toward = look - (stand + eye);
            float yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(toward.y, new Vector2(toward.x, toward.z).magnitude) * Mathf.Rad2Deg;   // 양수면 아래를 본다.
            player.RestorePhysicalPose(stand, yaw, pitch);
            Debug.Log("CG_PRACTICE 하네스가 역무원을 현장 " + Vector3.Distance(stand, hazard.Position).ToString("0.0")
                      + "m 앞, 시야가 트인 자리에 세움 (걸어간 것이 아니다) · " + hazard.Label + " · " + hazard.Where);
        }

        // SetExternalInputMode(true) 는 Pause() 를 부른다 — Resume(false) 로 풀지 않으면 역무원이 얼어붙는다.
        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        private static float Seconds(string variable, float fallback) =>
            float.TryParse(Environment.GetEnvironmentVariable(variable), out var parsed) && parsed > 0 ? parsed : fallback;

        private static int Drills() => DrillFiles().Length;
    }
}
#endif
