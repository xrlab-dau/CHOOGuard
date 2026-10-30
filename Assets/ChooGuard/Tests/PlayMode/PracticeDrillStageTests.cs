#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps;
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
            Environment.SetEnvironmentVariable("CG_PRACTICE_PLAYER_DONE", null);
            EmergencySession.NextSeed = 20260930;
        }

        // 시험이 실패해도 전역을 되돌리고, 근무 씬을 먼저 내린 한 프레임 뒤 역 씬을 내려 다음 시험에 씬을 남기지 않는다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
                Environment.SetEnvironmentVariable("CG_PRACTICE_PLAYER_DONE", null);
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
            // CG_PRACTICE_AUTOPILOT=1 이면 하네스가 자리를 잡아 주지 않는다. 오토파일럿이 제품의 입력 경로로 역무원을
            // 걷게 하고 E·Q 로 대응한다(확인 → 보고 → AED → 곁에 두기 → 인계). 끝나면 하네스에 알리고 10초 뒤 무대를 접는다.
            var pilot = Environment.GetEnvironmentVariable("CG_PRACTICE_AUTOPILOT") == "1" ? new Autopilot(session) : null;
            Hazard filmed = null;
            float endAt = float.PositiveInfinity;
            try
            {
                while (Time.realtimeSinceStartup < until && Time.realtimeSinceStartup < endAt)
                {
                    var main = session.Incidents.Main;
                    if (pilot != null)
                    {
                        pilot.Step(Time.deltaTime);
                        if (pilot.Done && float.IsPositiveInfinity(endAt))
                        {
                            Environment.SetEnvironmentVariable("CG_PRACTICE_PLAYER_DONE", "1");
                            endAt = Time.realtimeSinceStartup + 10f;
                        }
                    }
                    else if (main != null && main != filmed && main.Localized)
                    {
                        filmed = main;
                        Face(session, main);
                    }
                    if (pilot == null && autoHandover && main != null && float.IsPositiveInfinity(endAt))
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
                if (pilot != null)
                    Debug.Log("CG_AUTOPILOT 끝 · 단계 " + pilot.PhaseName + " · 걸은 거리 " + pilot.Walked.ToString("0") + "m · 대체 호출 "
                              + pilot.Fallbacks.Count + "건" + (pilot.Fallbacks.Count > 0 ? " · " + string.Join(" / ", pilot.Fallbacks) : ""));
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
                    if (pilot != null)
                    {
                        // 오토파일럿은 역무원 몫까지 한다: AED 를 환자 곁에 두고(쓰러진 지 N초가 기록된다) 구급대장에게 인계한다.
                        Assert.That(pilot.Done, Is.True, "오토파일럿이 끝까지 가지 못했습니다 · 단계 " + pilot.PhaseName);
                        Assert.That(record["aedAt"].Type, Is.Not.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null), "AED 가 환자 곁에 놓이지 않았습니다.");
                        Assert.That((string)record["handover"], Does.Contain("AED 곁에 둠"), "인계 문구에 AED 가 없습니다: " + record["handover"]);
                    }
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

        /// <summary>
        /// 역무원을 제품의 입력 경로로 움직이는 오토파일럿(CG_PRACTICE_AUTOPILOT=1). 걷기는 NavMesh 경로의 꺾임점을
        /// <see cref="FirstPersonResponder.StepInput"/> 으로 따라가고(순찰 시험 ShiftSampleTests 와 같은 방식), 상호작용은
        /// 시선을 맞춘 뒤 <see cref="FirstPersonResponder.TryInteract"/>(E) 로 한다. 사건 위치는 근무(Incidents.Main)에서
        /// 읽는다 — 역무실에서 신고를 전달받은 자리에 해당한다. 걷기나 E 가 정해진 시간 안에 안 되면 제품 API 를 직접
        /// 불러 대신하고 그 사실을 <see cref="Fallbacks"/> 와 로그에 남긴다 — 조용히 넘기지 않는다.
        /// </summary>
        private sealed class Autopilot
        {
            private enum Phase { Wait, GoPatient, Check, Report, GoAed, TakeAed, Return, Place, WaitEms, GoLead, HandOver, Done }

            private const float TurnDegreesPerSecond = 180f, FaceToWalkDegrees = 60f, CornerRadius = .7f, StuckSeconds = 3f;   // 꺾임점 반경이 크면 기둥을 질러 끼인다(take3 실측)
            private static readonly float[] AimHeights = { .9f, .5f, 1.3f };   // 쓰러진 몸의 콜라이더는 선 키 그대로다. 못 맞추면 높이를 바꿔 본다.

            private readonly EmergencySession session;
            private readonly FirstPersonResponder player;
            private Phase phase = Phase.Wait;
            private CollapseHazard casualty;
            private Passenger patient;
            private AedCabinet cabinet;
            private Responder lead;
            private Vector3[] corners = Array.Empty<Vector3>();
            private Vector3 planned, lastSeen;
            private int corner, replans, planFailures, tries, reviewedTries, sideSign = 1, approachSide;
            private float sincePhase, sinceProgress, sinceTry, sinceTrace, sidestep, direct, onSceneSince = -1f;

            public bool Done => phase == Phase.Done;
            public string PhaseName => phase.ToString();
            public float Walked { get; private set; }
            public readonly List<string> Fallbacks = new List<string>();

            public Autopilot(EmergencySession session)
            {
                this.session = session;
                player = session.Player;
                lastSeen = player.transform.position;
            }

            public void Step(float deltaSeconds)
            {
                // Simulate 는 deltaSeconds > 0.5 면 조용히 false 를 돌려준다. 여기서 자른다.
                float dt = Mathf.Min(deltaSeconds, .45f);
                if (dt <= 0 || player == null || phase == Phase.Done) return;
                sincePhase += dt;
                var before = player.transform.position;
                switch (phase)
                {
                    case Phase.Wait:
                        casualty = session.Incidents.Main as CollapseHazard;
                        if (casualty == null || casualty.Person == null) break;
                        patient = casualty.Person;
                        Enter(Phase.GoPatient, "환자 발생을 전달받음 · " + casualty.Where);
                        break;
                    case Phase.GoPatient:
                        if (WalkTo(StandSpot(patient.transform.position, patient.transform.forward, 1.3f), .9f, dt)) Enter(Phase.Check, "환자 곁 도착");
                        break;
                    case Phase.Check:
                        // 눈으로 보여 이미 알게 됐더라도 E 로 상태를 확인한다 — 완료 판정은 E 의 피드백(보이는 상태 문장)이다.
                        if (Interact(() => patient.transform.position + Vector3.up * AimHeights[tries % AimHeights.Length],
                                     () => player.LastFeedback == casualty.Visible, dt, 12f,
                                     () => player.ShowFeedback(session.Incidents.CheckInjured(patient)), "부상자 확인"))
                            Enter(Phase.Report, "환자 확인 · " + player.LastFeedback);
                        break;
                    case Phase.Report:
                        Aim(patient.transform.position + Vector3.up * .9f, dt);
                        if (SendReport()) Enter(Phase.GoAed, "역무실에 보고(Q)");
                        else if (sincePhase > 6f) { Fallback("보고 선택지가 열리지 않아 보고를 건너뜀"); Enter(Phase.GoAed, "보고 없이 AED 로"); }
                        break;
                    case Phase.GoAed:
                        if (cabinet == null) cabinet = UnityEngine.Object.FindFirstObjectByType<AedCabinet>();
                        if (cabinet == null) { Fallback("AED 보관함이 씬에 없음"); Enter(Phase.WaitEms, "AED 없이 구급대 대기"); break; }
                        if (WalkTo(cabinet.transform.position + cabinet.transform.forward * .9f, 1.1f, dt)) Enter(Phase.TakeAed, "AED 보관함 앞 도착");
                        break;
                    case Phase.TakeAed:
                        if (Interact(() => CabinetAim(), () => session.Hands.Aed != null, dt, 12f,
                                     () => session.Hands.TakeAed(cabinet.Unit), "AED 꺼내기"))
                            Enter(Phase.Return, "AED 를 들고 환자에게");
                        break;
                    case Phase.Return:
                        if (WalkTo(StandSpot(patient.transform.position, patient.transform.forward, 1.3f), .9f, dt)) Enter(Phase.Place, "환자 곁 복귀");
                        break;
                    case Phase.Place:
                        if (Interact(() => patient.transform.position + Vector3.up * AimHeights[tries % AimHeights.Length],
                                     () => casualty.AedAt >= 0, dt, 12f,
                                     () => session.Hands.PlaceAedBeside(patient), "AED 곁에 두기"))
                            Enter(Phase.WaitEms, "AED 를 환자 곁에 둠 · 쓰러진 지 " + Mathf.RoundToInt(casualty.AedAt - casualty.StartedAt) + "초 · 구급대 대기");
                        break;
                    case Phase.WaitEms:
                        Aim(patient.transform.position + Vector3.up * .6f, dt);
                        if (lead == null)
                        {
                            foreach (var responder in UnityEngine.Object.FindObjectsByType<Responder>(FindObjectsSortMode.None))
                                if (responder.Lead && responder.OnScene && session.Incidents.CanHandOver(responder)) { lead = responder; onSceneSince = sincePhase; break; }
                        }
                        // 구급대가 환자를 처치하는 것까지 보고 인계한다. 처치가 45초 넘게 안 오면 인계로 넘어간다.
                        if (lead != null && (casualty.Treated || !casualty.Active || sincePhase - onSceneSince > 45f))
                            Enter(Phase.GoLead, casualty.Treated ? "구급대 처치 확인 · 구급대장에게" : "처치 전이지만 구급대장에게");
                        else if (sincePhase > 420f) { Fallback("구급대장이 420초 안에 도착하지 않음"); Enter(Phase.Done, "인계 없이 종료"); }
                        break;
                    case Phase.GoLead:
                    {
                        // 환자 반대편에서 다가간다 — 환자 쪽에서 보면 E 가 환자(상태 확인)에 먼저 맞는다(take3 실측: 23회).
                        var away = lead.transform.position - patient.transform.position;
                        var side = Quaternion.Euler(0, new[] { 0f, 90f, -90f, 180f }[approachSide % 4], 0) * away;
                        if (WalkTo(StandSpot(lead.transform.position, side, 1.4f), .8f, dt)) Enter(Phase.HandOver, "구급대장 앞 (" + approachSide + "번 방향)");
                        break;
                    }
                    case Phase.HandOver:
                        // 첫 E 가 업무·인계 네 항목 검토를 요구한다. E 한 번마다 검토(Q)를 보내고 다시 E.
                        if (tries > reviewedTries) { reviewedTries = tries; SendReview(); }
                        if (tries >= 3 && !player.CurrentPrompt.Contains("인계") && approachSide < 3)
                        {
                            approachSide++;
                            Enter(Phase.GoLead, "E 가 '" + player.CurrentPrompt + "' 에 맞아 다른 방향에서 다시");
                            break;
                        }
                        if (Interact(() => LeadAim(), () => !session.Incidents.CanHandOver(lead), dt, 20f,
                                     () => { session.Incidents.HandOver(lead); SendReview(); session.Incidents.HandOver(lead); }, "현장 인계"))
                            Enter(Phase.Done, "현장 인계 · " + player.LastFeedback);
                        break;
                }
                Walked += Vector3.Distance(before, player.transform.position);
            }

            private void Enter(Phase next, string why)
            {
                Debug.Log("CG_AUTOPILOT t=" + session.ShiftSeconds.ToString("0.0") + "s " + phase + " → " + next + " · " + why);
                phase = next;
                sincePhase = 0; sinceProgress = 0; sinceTry = 0; sinceTrace = 0; sidestep = 0; direct = 0;
                tries = 0; reviewedTries = 0; replans = 0; planFailures = 0;
                corners = Array.Empty<Vector3>();
                lastSeen = player.transform.position;
            }

            private void Fallback(string what)
            {
                Fallbacks.Add(what);
                Debug.LogWarning("CG_AUTOPILOT 대체 · t=" + session.ShiftSeconds.ToString("0.0") + "s · " + phase + " · " + what);
            }

            private Vector3 LeadAim()
            {
                var collider = lead.GetComponentInChildren<Collider>();
                return collider != null ? collider.bounds.center : lead.transform.position + Vector3.up * 1.2f;
            }

            private Vector3 CabinetAim()
            {
                var collider = cabinet.GetComponent<Collider>() ?? cabinet.GetComponentInChildren<Collider>();
                return collider != null ? collider.bounds.center : cabinet.transform.position + Vector3.up * 1.2f;
            }

            /// <summary>NavMesh 경로의 꺾임점을 따라 걷는다. 도착하면 true. 막히면 경로를 다시 잡고, 세 번 막히면 텔레포트(대체 기록).</summary>
            /// <summary>
            /// 사람·물건 곁에 설 자리. <paramref name="preferred"/> 방향부터 좌우로 넓혀 가며 <paramref name="distance"/> 떨어진 걷는 땅을 찾고,
            /// 눈높이에서 대상이 보이는 곳을 먼저 고른다. 대상 위치 자체를 목표로 잡으면 벤치 뒤·돕는 승객 사이로 들어가려다
            /// 막힌다(take3 실측: 환자 곁 복귀에서 세 번 막힘).
            /// </summary>
            private Vector3 StandSpot(Vector3 anchor, Vector3 preferred, float distance)
            {
                preferred.y = 0;
                if (preferred.sqrMagnitude < 1e-4f) preferred = player.transform.position - anchor;
                preferred.y = 0;
                float baseAngle = Mathf.Atan2(preferred.x, preferred.z) * Mathf.Rad2Deg;
                Vector3? blockedFallback = null;
                for (int step = 0; step < 8; step++)
                {
                    float angle = baseAngle + (step % 2 == 0 ? 1 : -1) * (step / 2) * 45f;
                    var guess = anchor + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad)) * distance;
                    if (!NavMesh.SamplePosition(guess, out var hit, 1f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - anchor.y) > 1.5f) continue;
                    var eye = hit.position + Vector3.up * player.EyeHeight;
                    var line = anchor + Vector3.up * .6f - eye;
                    float reach = Mathf.Max(.3f, line.magnitude - .8f);
                    if (Physics.Raycast(eye, line.normalized, reach, ~0, QueryTriggerInteraction.Ignore)) { if (blockedFallback == null) blockedFallback = hit.position; continue; }
                    return hit.position;
                }
                return blockedFallback ?? anchor;
            }

            /// <summary>
            /// NavMesh 경로의 꺾임점을 따라 걷는다. 도착하면 true. 막히면 옆걸음 → 경로 재계산 → 목표로 직진 순으로 풀고,
            /// 그래도 안 되면 텔레포트(대체 기록). 부분 경로(끊긴 보행 영역, #242)는 끝까지 간 뒤 직진으로 잇는다 —
            /// 역무원은 NavMeshAgent 가 아니라 CharacterController 라 보행 영역 밖도 물리적으로 걸을 수 있다.
            /// </summary>
            private bool WalkTo(Vector3 target, float arrive, float dt)
            {
                var here = player.transform.position;
                float remaining = new Vector2(target.x - here.x, target.z - here.z).magnitude;
                if (remaining <= arrive && Mathf.Abs(target.y - here.y) < 2f) { corners = Array.Empty<Vector3>(); direct = 0; return true; }
                sinceTrace += dt;
                if (sinceTrace > 5f)
                {
                    sinceTrace = 0;
                    Debug.Log("CG_AUTOPILOT 걷기 t=" + session.ShiftSeconds.ToString("0") + "s 위치=" + Describe(here) + " 목표=" + Describe(target)
                              + " 남은=" + remaining.ToString("0.0") + "m 꺾임점 " + corner + "/" + corners.Length + (direct > 0 ? " 직진" : "") + (sidestep > 0 ? " 옆걸음" : ""));
                }

                // 옆걸음: 벽·기둥·사람에 붙어 밀고 있을 때 1.2초 비켜선다.
                if (sidestep > 0)
                {
                    sidestep -= dt;
                    player.StepInput(new Vector2(sideSign, .3f), Vector2.zero, false, false, false, dt);
                    if (sidestep <= 0) { lastSeen = player.transform.position; sinceProgress = 0; }
                    return false;
                }

                // 직진: 경로가 없거나 끊겼을 때 목표를 향해 그냥 걷는다(최대 12초).
                if (direct > 0)
                {
                    direct -= dt;
                    Steer(target, here, dt, remaining);
                    if (!Progressing(here, dt)) { sidestep = 1.2f; sideSign = -sideSign; }
                    if (direct <= 0)
                    {
                        if (++replans >= 4)
                        {
                            Fallback("경로·직진·옆걸음으로도 닿지 못해 목표 옆으로 텔레포트 (" + Describe(target) + ")");
                            Teleport(target); return true;
                        }
                        corners = Array.Empty<Vector3>();   // 자리가 바뀌었으니 다시 잡아 본다.
                    }
                    return false;
                }

                if (corners.Length == 0 || (target - planned).sqrMagnitude > 1f)
                {
                    if (!Plan(here, target))
                    {
                        if (++planFailures > 30) { planFailures = 0; direct = 12f; Debug.Log("CG_AUTOPILOT 길이 안 열려 직진 · " + Describe(here) + " → " + Describe(target)); }
                        return false;
                    }
                }
                while (corner < corners.Length)
                {
                    // 높이를 포함해 잰다 — 경사로의 꺾임점을 평면 거리로 '지났다' 고 하면 경사로를 타지 않는다(순찰 시험 실측).
                    float reach = corner == corners.Length - 1 ? arrive : CornerRadius;
                    if (Vector3.Distance(corners[corner], here) >= reach) break;
                    corner++;
                }
                if (corner >= corners.Length)
                {
                    corners = Array.Empty<Vector3>();
                    if (remaining <= arrive + .5f) return true;
                    direct = 12f;   // 부분 경로의 끝이다. 나머지는 직진으로.
                    Debug.Log("CG_AUTOPILOT 경로 끝, 남은 " + remaining.ToString("0.0") + "m 는 직진");
                    return false;
                }

                if (!Progressing(here, dt))
                {
                    Debug.Log("CG_AUTOPILOT 막힘 · 위치=" + Describe(here) + " 다음 꺾임점=" + Describe(corners[corner]) + " · 옆걸음 후 " + (replans + 1 >= 2 ? "직진" : "재계산"));
                    sidestep = 1.2f; sideSign = -sideSign;
                    if (++replans >= 2) direct = 12f;
                    corners = Array.Empty<Vector3>();
                    return false;
                }

                Steer(corners[corner], here, dt, Vector3.Distance(corners[corner], here));
                return false;
            }

            /// <summary>0.5m 넘게 움직였으면 진행 중. StuckSeconds 동안 못 움직였으면 false(한 번만).</summary>
            private bool Progressing(Vector3 here, float dt)
            {
                if ((here - lastSeen).sqrMagnitude > .25f) { lastSeen = here; sinceProgress = 0; return true; }
                sinceProgress += dt;
                if (sinceProgress <= StuckSeconds) return true;
                sinceProgress = 0;
                return false;
            }

            private void Steer(Vector3 waypoint, Vector3 here, float dt, float ahead)
            {
                var flat = new Vector3(waypoint.x - here.x, 0, waypoint.z - here.z);
                float wanted = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(player.transform.eulerAngles.y, wanted);
                float turn = Mathf.Clamp(delta, -TurnDegreesPerSecond * dt, TurnDegreesPerSecond * dt);
                float forward = Mathf.Abs(delta) < FaceToWalkDegrees ? 1f : 0f;
                // 걸으면서 시선은 수평으로 되돌린다(E 를 위해 숙였던 고개). 응급이므로 멀면 달리되, 꺾임점·목표 가까이선 걷는다(기둥에 끼지 않게).
                float level = Mathf.Clamp(player.PitchDegrees, -TurnDegreesPerSecond * dt, TurnDegreesPerSecond * dt);
                player.StepInput(new Vector2(0, forward), new Vector2(turn, level), ahead > 5f, false, false, dt);
            }

            private bool Plan(Vector3 here, Vector3 target)
            {
                var end = NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas) ? hit.position : target;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(here, end, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid || path.corners.Length == 0) return false;
                corners = path.corners;
                corner = corners.Length > 1 ? 1 : 0;   // 0번은 지금 선 자리다.
                planned = target; planFailures = 0;
                float length = 0;
                for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                Debug.Log("CG_AUTOPILOT 경로 " + path.status + " · 꺾임점 " + corners.Length + " · " + length.ToString("0") + "m · " + Describe(here) + " → " + Describe(end));
                return true;
            }

            private void Teleport(Vector3 target)
            {
                var spot = NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas) ? hit.position : target;
                var toward = target - spot;
                player.RestorePhysicalPose(spot, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, 0);
                corners = Array.Empty<Vector3>();
            }

            /// <summary>시선을 점에 맞춘다(yaw 는 더하고, pitch 는 look.y 를 빼는 제품 규칙). 맞으면 true.</summary>
            private bool Aim(Vector3 point, float dt)
            {
                var eye = player.PlayerCamera != null ? player.PlayerCamera.transform.position : player.transform.position + Vector3.up * player.EyeHeight;
                var dir = point - eye;
                float wantedYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float wantedPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;   // 양수 = 아래
                float dyaw = Mathf.DeltaAngle(player.transform.eulerAngles.y, wantedYaw);
                float dpitch = wantedPitch - player.PitchDegrees;
                float most = TurnDegreesPerSecond * dt;
                player.StepInput(Vector2.zero, new Vector2(Mathf.Clamp(dyaw, -most, most), -Mathf.Clamp(dpitch, -most, most)), false, false, false, dt);
                return Mathf.Abs(dyaw) < 2.5f && Mathf.Abs(dpitch) < 2.5f;
            }

            /// <summary>시선을 맞춘 뒤 0.6초마다 E 를 누른다. <paramref name="done"/> 이 참이 되면 true. 시간이 지나면 대체 호출.</summary>
            private bool Interact(Func<Vector3> aim, Func<bool> done, float dt, float timeout, Action fallback, string label)
            {
                if (done()) return true;
                if (!Aim(aim(), dt)) return false;
                sinceTry += dt;
                if (sinceTry >= .6f)
                {
                    sinceTry = 0; tries++;
                    bool performed = player.TryInteract();
                    Debug.Log("CG_AUTOPILOT E · " + label + " · " + (performed ? "수행" : "불가") + " · 프롬프트 '" + player.CurrentPrompt + "' · " + player.LastFeedback);
                }
                if (done()) return true;
                if (sincePhase > timeout)
                {
                    Fallback(label + " E " + tries + "회 실패 → 제품 API 직접 호출 (프롬프트 '" + player.CurrentPrompt + "')");
                    fallback();
                    return done();
                }
                return false;
            }

            /// <summary>그 건의 보고 선택지(라벨 끝이 Hazard.ReportOption)를 무전(Q)으로 보낸다.</summary>
            private bool SendReport()
            {
                if (!casualty.Reportable) return false;
                foreach (var provider in session.RadioProviders.ToArray())
                {
                    var options = provider();
                    if (options == null) continue;
                    foreach (var option in options)
                    {
                        if (option.Label == null || !option.Label.EndsWith(casualty.ReportOption, StringComparison.Ordinal)) continue;
                        option.Send?.Invoke();
                        Debug.Log("CG_AUTOPILOT Q · " + option.Label);
                        return true;
                    }
                }
                return false;
            }

            /// <summary>인계 전 검토: Q 업무·인계 묶음의 항목을 전부 보낸다(제품이 요구하는 네 항목).</summary>
            private void SendReview()
            {
                int reviewed = 0;
                foreach (var provider in session.RadioGroupProviders.ToArray())
                    foreach (var group in provider() ?? Array.Empty<EmergencySession.RadioGroup>())
                    {
                        if (group.Label != "업무·인계" || group.Options == null) continue;
                        foreach (var option in group.Options) { option.Send?.Invoke(); reviewed++; }
                    }
                if (reviewed > 0) Debug.Log("CG_AUTOPILOT Q · 업무·인계 항목 " + reviewed + "개 검토");
            }

            private string Describe(Vector3 at) => at.x.ToString("0.0") + "," + at.y.ToString("0.0") + "," + at.z.ToString("0.0");
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
