#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static ChooGuard.App.Fps.Emergency.PracticeLabel;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// The order lessons hear the real game (docs/practice-lessons-plan.md "검증"): in a real practice shift the staff member
    /// presses E on the real target through <see cref="FirstPersonResponder.TryInteract"/>, and the radio entries the wheel
    /// would call (RequestTeam, Broadcast, ReportSlots) run as they are. The harness only stands the staff member where the
    /// target is in reach and aims; it does not walk. Each step the lesson ticks is one the player could really do.
    /// </summary>
    public sealed class PracticeLessonSceneTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private string savedKey;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PracticeVideoRecorder.End();
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator LowBloodSugarCheckAndCallTickTheirStepsInOrder()
        {
            EmergencySession session = null;
            yield return StartPractice("환자·저혈당", s => session = s);
            var lesson = (LowBloodSugarLesson)session.OrderLesson;

            CollapseHazard casualty = null;
            yield return WaitFor(() => (casualty = session.Incidents.Main as CollapseHazard) != null && casualty.Person != null, 300, "저혈당 환자가 생기지 않았습니다.");
            var patient = casualty.Person;
            yield return null;

            Assert.That(Aim(session.Player, patient.GetComponentsInChildren<Collider>(), CheckPatient), Is.True,
                "환자를 '" + CheckPatient + "'로 조준하지 못했습니다. 마지막 문구: " + session.Player.CurrentPrompt);
            Assert.That(session.Player.TryInteract(), Is.True, "E 상태 확인이 실행되지 않았습니다.");
            Assert.That(lesson.IsDone("3"), Is.True, "E 상태 확인이 3 · 의식 판단으로 잡히지 않았습니다.");

            Radio(session, "RequestTeam", 1);   // TeamRequests[1] = "119 구급"
            Assert.That(lesson.IsDone("4b"), Is.True, "Q 요청·기관 119 구급이 4b · 119 신고로 잡히지 않았습니다.");
            Assert.That(lesson.Mistakes, Is.Empty, "순서대로 했는데 감점됐습니다: " + string.Join(", ", lesson.Mistakes.Select(m => m.Key)));
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator FallingObjectCordonReportAndRadioTickTheirSteps()
        {
            EmergencySession session = null;
            yield return StartPractice("시설·낙하물", s => session = s);
            var lesson = (FallingObjectLesson)session.OrderLesson;

            FallingObjectHazard fallen = null;
            yield return WaitFor(() => (fallen = session.Incidents.Main as FallingObjectHazard) != null, 300, "낙하물이 생기지 않았습니다.");
            HazardMarker marker = null;
            yield return WaitFor(() => (marker = UnityEngine.Object.FindObjectsByType<HazardMarker>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.Hazard == fallen && m.InteractionPrompt == Cordon)) != null, 120, "떨어진 판재에 통제 표식이 없습니다.");

            Assert.That(Aim(session.Player, marker.GetComponentsInChildren<Collider>(true), Cordon), Is.True,
                "떨어진 판재를 '" + Cordon + "'으로 조준하지 못했습니다. 마지막 문구: " + session.Player.CurrentPrompt);
            Assert.That(session.Player.TryInteract(), Is.True, "E 주변 접근 통제가 실행되지 않았습니다.");
            Assert.That(lesson.IsDone("3"), Is.True, "E 주변 접근 통제가 3 · 접근 통제로 잡히지 않았습니다.");

            Radio(session, "ReportSlots", fallen);
            Assert.That(lesson.IsDone("4"), Is.True, "Q 보고가 4 · 역무실 보고로 잡히지 않았습니다.");
            Radio(session, "Broadcast", PaScope.ClearAround, true);
            Assert.That(lesson.IsDone("5"), Is.True, "Q 방송 주변 통로 비우기가 5 · 우회 방송으로 잡히지 않았습니다.");
            Radio(session, "RequestTeam", 4);   // TeamRequests[4] = "시설 담당"
            Assert.That(lesson.IsDone("6"), Is.True, "Q 요청·기관 시설 담당이 6 · 시설 담당 호출로 잡히지 않았습니다.");
            Assert.That(lesson.Mistakes, Is.Empty);
        }

        // ── 연습 전체 완주 ───────────────────────────────────────────────────

        /// <summary>Every practice scenario that can start in the real station and keeps an order lesson.</summary>
        public static IEnumerable<string> PlayableLessons() => PracticeCatalog.Groups.SelectMany(group => group.Scenarios)
            .Where(scenario => scenario.Available && scenario.OrderLesson != null).Select(scenario => scenario.Id);

        /// <summary>
        /// Walks the lesson's own step list in table order (docs/practice-lessons-plan.md "검증"): the staff member first notices
        /// the incident by standing near and looking at it, then does each step the real way — E/R on the target whose prompt the
        /// step listens for, or the radio entry the wheel would call — and finally hands over to the commanding team leader
        /// after reviewing the four handover items. Every step must tick and no order rule may deduct.
        /// </summary>
        [UnityTest, Timeout(2400000)]
        public IEnumerator WalkthroughTicksEveryStepWithoutDeductions([ValueSource(nameof(PlayableLessons))] string id)
        {
            EmergencySession session = null;
            yield return StartPractice(id, s => session = s);
            PracticeVideoRecorder.Begin(id, session);
            var lesson = session.OrderLesson;
            var director = session.Incidents;
            yield return WaitFor(() => director.Main != null, 420, id + " 사건이 생기지 않았습니다.");
            var main = director.Main;
            var report = new List<string> { id + " · 주 사건 " + main.Label + (main.Localized ? " @" + main.Position.ToString("F1") : " (위치 없음)") };

            // 알아채기: 볼 수 있는 사건은 가까이 서서 본다. 냄새·흔들림·전화처럼 안 보이는 사건은 알 때까지 기다린다.
            float until = Time.realtimeSinceStartup + 90;
            while (!director.PlayerKnowsIncident && Time.realtimeSinceStartup < until)
            {
                if (main.Localized) Look(session.Player, main.Position);
                yield return new WaitForSecondsRealtime(.25f);
            }
            report.Add("인지 " + (director.PlayerKnowsIncident ? "됨" : "안 됨"));
            PracticeVideoRecorder.Mark(report[0] + " · " + report[1]);

            var declared = lesson.DeclaredSteps.ToList();
            // 세상이 만들지 않은 대상(맞은 사람이 없는 낙하물, 아무것도 안 떨어진 지진)의 단계는 '해당 없음'으로 따로 적는다.
            var notApplicable = new List<string>();
            foreach (var no in declared.Select(step => step.No).Distinct().ToList())
            {
                var entries = declared.Where(step => step.No == no).OrderBy(step => step.Kind == PracticeSignal.Interact ? 0 : 1).ToList();
                if (entries.All(step => step.Kind == PracticeSignal.Handover)) continue;
                string how = null;
                PracticeVideoRecorder.Mark("단계 " + no + " " + entries[0].Name + " 시도");
                foreach (var entry in entries)
                {
                    if (lesson.IsDone(no)) break;
                    var attempt = new List<string>();
                    yield return DoStep(session, entry.Kind, entry.Labels, main, attempt);
                    yield return PracticeVideoRecorder.Beat();
                    how = entry.Kind + " " + string.Join("/", attempt);
                }
                bool absent = !lesson.IsDone(no) && entries.All(step => step.Kind == PracticeSignal.Interact) && how != null && how.Contains("대상 없음") && !how.Contains(" E '") && !how.Contains(" R '");
                if (absent) notApplicable.Add(no);
                report.Add(no + " " + entries[0].Name + ": " + (lesson.IsDone(no) ? "✓ " : absent ? "– 해당 없음 " : "✗ ") + how);
                PracticeVideoRecorder.Mark(report[report.Count - 1]);
            }

            // 인계: 지휘 기관을 아직 안 불렀으면 부른다(모든 단계 뒤라 순서 규칙에 걸리지 않는다).
            var calledBy = (IDictionary)typeof(IncidentDirector).GetField("calledBy", Private).GetValue(director);
            var commander = director.Commander;
            if (!calledBy.Contains(commander))
            {
                int index = commander == Agency.Fire ? 0 : commander == Agency.Medical ? 1 : commander == Agency.Police ? 3 : 4;
                Radio(session, "RequestTeam", index);
                report.Add("인계 전 지휘 기관 추가 요청 · " + commander);
            }
            Responder lead = null;
            until = Time.realtimeSinceStartup + 900;
            while ((lead = UnityEngine.Object.FindObjectsByType<Responder>(FindObjectsSortMode.None).FirstOrDefault(director.CanHandOver)) == null && Time.realtimeSinceStartup < until)
                yield return new WaitForSecondsRealtime(1f);
            PracticeVideoRecorder.Mark("인계 대기 끝 · " + commander + (lead == null ? " 팀장 없음" : " 팀장 " + lead.name));
            if (lead == null) report.Add("인계 ✗ " + commander + " 팀장이 도착하지 않음");
            else
            {
                bool first = Aim(session.Player, lead.GetComponentsInChildren<Collider>(), "현장 인계") && session.Player.TryInteract();
                yield return PracticeVideoRecorder.Beat();
                var options = (List<EmergencySession.RadioOption>)typeof(IncidentDirector).GetMethod("WorkflowOptions", Private).Invoke(director, null);
                int reviewed = 0;
                foreach (var option in options.Where(option => option.Label.StartsWith("인계 검토 · ")))
                {
                    PracticeVideoRecorder.Mark("무전 · " + option.Label);
                    option.Send(); reviewed++;
                    yield return PracticeVideoRecorder.Beat(1.5f);
                }
                bool second = Aim(session.Player, lead.GetComponentsInChildren<Collider>(), "현장 인계") && session.Player.TryInteract();
                report.Add("인계 " + (lesson.HandedOver ? "✓" : "✗") + " · " + commander + " 팀장 · 검토 " + reviewed + "항목 · E " + first + "/" + second);
            }
            report.Add("감점 " + (lesson.Mistakes.Count == 0 ? "없음" : string.Join(", ", lesson.Mistakes.Select(m => m.Key))) + " · 점수 " + lesson.OrderScore);
            Debug.Log("CG_PRACTICE_WALK " + string.Join(" | ", report));
            // 녹화 중이면 인계 뒤 화면(결과 화면 등)도 몇 초 남긴다.
            if (Environment.GetEnvironmentVariable(PracticeVideoRecorder.Variable) is string video && video.Length > 0)
                yield return new WaitForSecondsRealtime(10f);
            PracticeVideoRecorder.End();

            var missing = lesson.StepNumbers.Where(no => !lesson.IsDone(no) && !notApplicable.Contains(no)).ToList();
            Assert.That(missing, Is.Empty, "안 잡힌 단계: " + string.Join(", ", missing) + "\n" + string.Join("\n", report));
            Assert.That(lesson.Mistakes, Is.Empty, string.Join("\n", report));
        }

        private static IEnumerator DoStep(EmergencySession session, PracticeSignal kind, string[] labels, Hazard main, List<string> attempt)
        {
            switch (kind)
            {
                case PracticeSignal.Interact:
                    foreach (var label in labels)
                    {
                        // AED를 환자 곁에 두려면 먼저 보관함에서 꺼내 든다(그 자체는 단계가 아니다).
                        if (label == AedPlace && session.Hands.Aed == null)
                        {
                            var cabinets = Targets(AedTake, main);
                            foreach (var (cabinet, _) in cabinets.Take(4))
                                if (Aim(session.Player, cabinet.GetComponentsInChildren<Collider>(), AedTake) && session.Player.TryInteract()) { attempt.Add("E '" + AedTake + "' " + cabinet.name); break; }
                            if (session.Hands.Aed == null) { attempt.Add("'" + AedTake + "' 실패"); continue; }
                            yield return PracticeVideoRecorder.Beat();
                        }
                        // 대상이 나타날 때까지(예: 흔들림 뒤 떨어진 물건, 사건을 안 뒤의 '대피 안내') 잠시 기다린다.
                        List<(MonoBehaviour Target, bool Secondary)> found = null;
                        float until = Time.realtimeSinceStartup + 45;
                        while ((found = Targets(label, main)).Count == 0 && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(.5f);
                        if (found.Count == 0) { attempt.Add("'" + label + "' 대상 없음"); continue; }
                        foreach (var (target, secondary) in found.Take(4))
                        {
                            if (!Aim(session.Player, target.GetComponentsInChildren<Collider>(), label, secondary)) { attempt.Add("'" + label + "' 조준 실패 " + target.name); continue; }
                            bool done = secondary ? session.Player.TrySecondary() : session.Player.TryInteract();
                            attempt.Add((secondary ? "R '" : "E '") + label + "' " + target.name + (done ? "" : " 실행 안 됨"));
                            if (done) yield break;
                        }
                    }
                    break;
                case PracticeSignal.Report:
                    Radio(session, "ReportSlots", main);
                    attempt.Add("보고 " + main.Label);
                    break;
                case PracticeSignal.Request:
                {
                    string label = labels.Length > 0 ? labels[0] : "119 소방";
                    int index = Array.IndexOf(StaticField<string[]>("TeamRequests"), label);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0), label);
                    Radio(session, "RequestTeam", index);
                    attempt.Add(label);
                    break;
                }
                case PracticeSignal.Measure:
                {
                    int index = Array.IndexOf(StaticField<string[]>("MeasureRequests"), labels[0]);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0), labels[0]);
                    Radio(session, "RequestMeasure", index);
                    attempt.Add(labels[0]);
                    break;
                }
                case PracticeSignal.Broadcast:
                {
                    var scopeName = typeof(IncidentDirector).GetMethod("ScopeName", BindingFlags.NonPublic | BindingFlags.Static);
                    string label = labels.Length > 0 ? labels[0] : (string)scopeName.Invoke(null, new object[] { PaScope.ClearAround });
                    var scope = ((PaScope[])Enum.GetValues(typeof(PaScope))).First(s => (string)scopeName.Invoke(null, new object[] { s }) == label);
                    // 보고한 위치가 없으면 Q 방송의 '현 위치'를 고른다('보고한 위치'는 역무실이 거절한다).
                    bool atReported = typeof(IncidentDirector).GetField("lastReported", Private).GetValue(session.Incidents) != null;
                    Radio(session, "Broadcast", scope, atReported);
                    attempt.Add(label + (atReported ? " · 보고한 위치" : " · 현 위치"));
                    break;
                }
            }
            yield return null;
        }

        /// <summary>Behaviours whose E (or R) prompt reads <paramref name="label"/> right now, nearest the incident first.</summary>
        private static List<(MonoBehaviour Target, bool Secondary)> Targets(string label, Hazard main)
        {
            var found = new List<(MonoBehaviour, bool)>();
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour == null || !behaviour.isActiveAndEnabled) continue;
                if (behaviour is IFpsInteraction primary && Prompt(() => primary.InteractionPrompt) == label) found.Add((behaviour, false));
                else if (behaviour is IFpsSecondaryInteraction secondary && Prompt(() => secondary.SecondaryPrompt) == label) found.Add((behaviour, true));
            }
            var near = main.Localized ? main.Position : EmergencySession.Current.Player.transform.position;
            return found.OrderBy(item => Vector3.Distance(item.Item1.transform.position, near)).ToList();
        }

        private static string Prompt(Func<string> read)
        {
            try { return read(); } catch { return null; }
        }

        private static T StaticField<T>(string name) => (T)typeof(IncidentDirector).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        /// <summary>Stands a few metres from <paramref name="point"/> on the navmesh and looks at it (how a player notices it).</summary>
        private static void Look(FirstPersonResponder player, Vector3 point)
        {
            for (int step = 0; step < 8; step++)
            {
                float angle = step * 45f * Mathf.Deg2Rad;
                if (!NavMesh.SamplePosition(point + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 3f, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                var eye = hit.position + Vector3.up * player.EyeHeight;
                if (Physics.Linecast(eye, point + Vector3.up * .5f, out var blocked, ~0, QueryTriggerInteraction.Ignore) && blocked.distance < Vector3.Distance(eye, point) - 1.5f) continue;
                var toward = point - eye;
                player.RestorePhysicalPose(hit.position, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, -Mathf.Atan2(toward.y, new Vector2(toward.x, toward.z).magnitude) * Mathf.Rad2Deg);
                return;
            }
        }

        // ── 무대 ─────────────────────────────────────────────────────────────

        private static IEnumerator StartPractice(string id, Action<EmergencySession> started)
        {
            Assert.That(PracticeShift.Arm(id), Is.True, id + " 연습을 무장하지 못했습니다.");
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return WaitFor(() => EmergencySession.Current != null && EmergencySession.Current.Incidents != null && EmergencySession.Current.Incidents.Practicing, 600, "연습 세션이 생기지 않았습니다.");
            var session = EmergencySession.Current;
            Assert.That(session.Incidents.Practice?.Id, Is.EqualTo(id));
            Assert.That(session.OrderLesson, Is.Not.Null, id + " 연습에 순서 점수가 붙지 않았습니다.");
            session.Player.SetExternalInputMode(true);
            Assert.That(session.Player.Resume(false), Is.True);
            started(session);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string failure)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        private static void Radio(EmergencySession session, string method, params object[] arguments)
        {
            var found = typeof(IncidentDirector).GetMethods(Private).Where(m => m.Name == method && m.GetParameters().Length == arguments.Length).ToArray();
            Assert.That(found.Length, Is.EqualTo(1), "IncidentDirector." + method + " 을 찾지 못했습니다.");
            found[0].Invoke(session.Incidents, arguments);
        }

        /// <summary>
        /// Stands the staff member within reach of <paramref name="targets"/> and aims until the HUD prompt reads
        /// "E · <paramref name="prompt"/>". Positions are tried in rings around the target (on the navmesh first, then on the
        /// floor under the guess: a cordoned or closed spot can be off the passengers' navmesh but the staff member may still
        /// stand there), aim points across its bounds. On failure the last camera hit is logged.
        /// </summary>
        private static bool Aim(FirstPersonResponder player, Collider[] targets, string prompt, bool secondary = false)
        {
            var live = targets.Where(c => c != null && c.enabled).ToArray();
            if (live.Length == 0) { Debug.LogWarning("CG_PRACTICE_SCENE 조준할 콜라이더가 없습니다."); return false; }
            var bounds = live[0].bounds;
            foreach (var c in live) bounds.Encapsulate(c.bounds);
            var centre = bounds.center;
            var aims = new List<Vector3> { centre, centre + Vector3.up * bounds.extents.y * .5f, centre - Vector3.up * bounds.extents.y * .5f };
            aims.AddRange(live.Select(c => c.bounds.center));
            string wanted = (secondary ? "R · " : "E · ") + prompt, lastHit = "(없음)";
            foreach (bool onNavMesh in new[] { true, false })
                foreach (float distance in new[] { .6f, 1.1f, 1.7f, 2.3f })
                    for (int step = 0; step < 12; step++)
                    {
                        float angle = step * 30f * Mathf.Deg2Rad;
                        // 상자 표면에서 distance 만큼 떨어진 자리(가운데가 아니다: 떨어진 판재의 상자는 수 m 크기다).
                        var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                        float edge = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
                        var guess = new Vector3(centre.x, bounds.min.y, centre.z) + direction * (edge + distance);
                        Vector3 stand;
                        if (onNavMesh)
                        {
                            if (!NavMesh.SamplePosition(guess, out var hit, 1.2f, NavMesh.AllAreas)) continue;
                            stand = hit.position;
                        }
                        else
                        {
                            if (!Physics.Raycast(guess + Vector3.up * 1.5f, Vector3.down, out var floor, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                            stand = floor.point;
                        }
                        if (stand.y > bounds.max.y || stand.y < bounds.min.y - 2f) continue;
                        var eye = stand + Vector3.up * player.EyeHeight;
                        // 눈에서 가장 가까운 상자 위의 점을 먼저 본다. 손 닿는 거리(3 m) 안이어야 잡힌다.
                        var near = live.Where(c => c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mesh && mesh.convex))
                            .Select(c => c.ClosestPoint(eye)).ToList();
                        foreach (var aim in near.Concat(aims))
                        {
                            var toward = aim - eye;
                            float yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                            float pitch = -Mathf.Atan2(toward.y, new Vector2(toward.x, toward.z).magnitude) * Mathf.Rad2Deg;
                            player.RestorePhysicalPose(stand, yaw, pitch);
                            Physics.SyncTransforms();
                            player.RefreshInteraction();
                            if ((secondary ? player.CurrentSecondaryPrompt : player.CurrentPrompt) == wanted) return true;
                            var camera = player.PlayerCamera.transform;
                            if (Physics.Raycast(camera.position, camera.forward, out var seen, 3f, ~0, QueryTriggerInteraction.Ignore))
                                lastHit = seen.collider.name + " @" + seen.distance.ToString("0.00") + "m (문구 '" + player.CurrentPrompt + "')";
                        }
                    }
            Debug.LogWarning("CG_PRACTICE_SCENE '" + prompt + "' 조준 실패 · 대상 " + string.Join("/", live.Select(c => c.name + ":" + c.GetType().Name + (c.isTrigger ? "(trigger)" : "") + " layer " + c.gameObject.layer))
                             + " · 범위 " + bounds.center.ToString("F2") + " " + bounds.size.ToString("F2") + " · 마지막 시선 " + lastHit);
            return false;
        }
    }
}
#endif
