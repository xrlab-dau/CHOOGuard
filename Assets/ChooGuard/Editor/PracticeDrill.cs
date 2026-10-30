using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// 계열별 비상 대응 연습의 프로토타입 (#270). 한 계열의 원인 하나를 강제로 일으키고, 역무원이
    /// 그 건만 인지하게 하고, 무전으로 보고해 **역무실이 돌려주는 지시를 그대로 받아 적는다.**
    /// </summary>
    /// <remarks>
    /// 왜 에디터 전용인가: #270 의 설계 판단(경계 리트머스를 바꿀지)이 아직 없다. 그래서
    /// <c>Tutorial</c> 네임스페이스를 참조하지 않고 제품 코드도 고치지 않는다 — 리트머스
    /// ("이 네임스페이스는 Emergency 심볼을 참조하지 않는다. 그 반대도 같다")를 깨지 않는다.
    /// 승인이 나면 리플렉션 세 곳을 공개 호출로 바꿔 승격하면 되므로 버릴 것이 적다.
    ///
    /// 왜 시나리오를 쓰지 않는가: 하드룰이 '비상상황 목록·시나리오 ID 테이블' 을 금지한다. 그리고
    /// **쓸 필요가 없다.** 연습의 정답이 이미 제품 코드에 있다 — <see cref="Hazard.OfficeReply"/> 가
    /// 17종 전부에 있고, 역할 경계까지 문구에 박혀 있다("직접 제지하지 말고", "문을 억지로 열지 말고",
    /// "선로로 내려가지 말고", "무리하지 마십시오"). 이 도구는 그 문구를 **읽어 보일 뿐 새로 쓰지 않는다.**
    /// <see cref="Families"/> 는 사건 목록이 아니라 '지금 세계가 내놓은 후보 중 어느 것을 고를지' 의
    /// 필터다 — 없는 사건을 만들지 않는다.
    ///
    /// <c>EmergencyForceAll</c> 과 다른 점 세 가지:
    /// (1) 전부가 아니라 **한 건**만 일으킨다,
    /// (2) 전부를 인지시키지 않고 <c>Execute</c> 전후 위험 목록의 **차이만** 인지시킨다 — 연습이므로
    ///     이미 벌어져 있던 다른 건까지 알게 되면 무엇을 배웠는지 흐려진다,
    /// (3) 무전을 전부 보내지 않고 **그 건의 보고 선택지만** 보낸다. 수리는 시도하지 않는다(역할 경계).
    ///
    /// 디렉터의 private 멤버만 리플렉션으로 닿는다 — <c>EmergencyForceAll</c> 과 같은 이유로,
    /// 비상 레이어가 고쳐 쓰이는 동안에도 컴파일이 유지되게. 못 찾은 멤버는 추측하지 않고 보고한다.
    /// <see cref="Hazard"/> 쪽은 전부 public 이므로 그냥 부른다.
    ///
    /// 심정지 계열(2026-10-30): 코드에 '심정지' 종류는 없다. <c>chest_pain</c> 의 4단계(의식 없음·호흡
    /// 비정상, <see cref="CollapseHazard.NotBreathingNormally"/>)가 심정지다. 즉시형은 4단계로 시작하고,
    /// 악화형(<c>CG_PRACTICE_ONSET=worsen</c>)은 3단계로 시작해 역무원이 AED 를 곁에 두지 못한 채
    /// <c>CG_PRACTICE_WORSEN_AFTER</c>(기본 60) 초가 지나면 한 단계 올린다 — 늦으면 악화한다는 것을 가르친다.
    ///
    /// 보존 근무 선택(#301 이후): 역 씬은 provider 런타임이 소유하고, 그 루트가 있으면 <c>EmergencySession</c>
    /// 이 스스로 꺼진다. 이 도구는 <c>IncidentDirector</c> 위에서 돌므로 재생 중 역 씬이 로드되기 전에
    /// provider 준비를 미루고(PlayMode 시험 픽스처 <c>RetainedEmergencySessionFixture</c> 와 같은 seam),
    /// 로드되면 provider 루트를 끄고 역무원이 없으면 연습용 플레이어를 둔다(<see cref="RetainedSession"/>).
    /// </remarks>
    [InitializeOnLoad]
    public static class PracticeDrill
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string OutFolder = ".planning/2026-10-30-cardiac-arrest-solo-scenario/drills";
        private const string TitleScenePath = "Assets/ChooGuard/Scenes/Bootstrap.unity";
        private const float OfferWaitSeconds = 60f;
        private const string ArmKey = "ChooGuard.PracticeDrill.Armed";
        private const long ArmTtlSeconds = 900;
        private const string CardiacFamily = "심정지";
        private static string lastFile;

        /// <summary>
        /// 계열 = 그 계열에 드는 원인 <c>Transition.Kind</c> 들. 계열 구성은
        /// <c>workers/learning/README.md</c> 의 집계(원인 33종 / 11계열)와 같다.
        /// 심정지는 질환 계열의 부분집합이다: 가슴 통증 조건만 고르고, 크기를 4단계로 올린다.
        /// </summary>
        private static readonly Dictionary<string, string[]> Families = new Dictionary<string, string[]>
        {
            ["질환"] = new[] { "collapse", "seizure", "chest_pain", "breathing", "low_blood_sugar" },
            [CardiacFamily] = new[] { "chest_pain" },
        };

        /// <summary>심정지 발생 방식: 즉시형(4단계로 시작) 또는 악화형(3단계로 시작해 뒤에 한 단계 올림).</summary>
        private static string Onset =>
            string.Equals(Environment.GetEnvironmentVariable("CG_PRACTICE_ONSET"), "worsen", StringComparison.OrdinalIgnoreCase) ? "worsen" : "immediate";

        static PracticeDrill() => EditorApplication.update += Tick;

        [MenuItem("ChooGuard/Practice/질환 계열 연습 한 건 (#270 프로토타입)")]
        public static void Illness() => Run("질환");

        [MenuItem("ChooGuard/Practice/심정지 환자 한 건")]
        public static void CardiacArrest() => Run(CardiacFamily);

        /// <summary>
        /// 배치 실행용 무장. 편집 모드에서 <c>-executeMethod</c> 로 부르고, 이어지는 재생(예: PlayMode
        /// 테스트)에서 근무가 준비되는 순간 <see cref="Tick"/> 이 한 번 돌린다. 도메인 리로드를 넘겨야
        /// 하므로 정적 필드가 아니라 <see cref="SessionState"/> 에 둔다 — <c>EmergencyForceAll</c> 과 같다.
        /// 계열은 환경변수 <c>CG_PRACTICE_FAMILY</c> (없으면 질환).
        /// </summary>
        public static void Arm()
        {
            var family = Environment.GetEnvironmentVariable("CG_PRACTICE_FAMILY");
            if (string.IsNullOrEmpty(family)) family = "질환";
            if (!Families.ContainsKey(family))
            {
                Debug.LogError("[연습] 모르는 계열입니다: " + family + " (아는 것: " + string.Join(", ", Families.Keys) + ")");
                return;
            }
            SessionState.SetString(ArmKey, family + "|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Debug.Log("[연습] 무장 → " + family);
        }

        /// <summary>마지막으로 쓴 결과를 한 화면으로. 배치에서 <c>-executeMethod</c> 로 부른다.</summary>
        public static void Report()
        {
            var path = lastFile;
            if (path == null && Directory.Exists(OutFolder))
                path = Directory.GetFiles(OutFolder, "drill-*.json").OrderBy(f => f).LastOrDefault();
            if (path == null) { Debug.Log("[연습] 결과가 없습니다 (" + (SessionState.GetString(ArmKey, "").Length > 0 ? "무장했지만 돌지 않음" : "무장 안 됨") + ")"); return; }
            var record = JObject.Parse(File.ReadAllText(path));
            var lines = new List<string>
            {
                "파일: " + path,
                "계열: " + record["family"] + " · 종류: " + record["kind"] + " · 결과: " + record["result"],
                "보낸 무전: " + string.Join(", ", (record["radioSent"] as JArray ?? new JArray()).Select(t => (string)t)),
            };
            foreach (var item in record["guidance"] as JArray ?? new JArray())
                lines.Add("지시 [" + item["label"] + " · " + item["where"] + "] " + item["officeReply"]);
            foreach (var item in record["timeline"] as JArray ?? new JArray())
                lines.Add(item["clock"] + " " + item["text"]);
            Debug.Log("[연습]\n" + string.Join("\n", lines));
        }

        /// <summary>무장돼 있고 재생 중이며 근무가 준비됐으면 한 번만 돌린다.</summary>
        private static void Tick()
        {
            var armed = SessionState.GetString(ArmKey, "");
            if (armed.Length == 0 || !EditorApplication.isPlaying) return;
            var parts = armed.Split('|');
            // 무장만 해 두고 재생이 없으면 다른 작업의 재생 세션에서 저절로 돌지 않게 유효 시간을 둔다.
            if (parts.Length != 2 || !long.TryParse(parts[1], out var at)
                || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - at > ArmTtlSeconds)
            { SessionState.EraseString(ArmKey); return; }
            // 역 씬이 뜨기 전에 보존 근무를 고른다. 이미 provider 로 떠 버렸으면 돌 수 없다.
            if (!RetainedSession.EnsureInstalled(out var problem))
            {
                Debug.LogError("[연습] " + problem);
                SessionState.EraseString(ArmKey);
                return;
            }
            var session = UnityEngine.Object.FindFirstObjectByType<EmergencySession>();
            if (session == null || session.Incidents == null) return;
            SessionState.EraseString(ArmKey);
            EditorCoroutine.Start(Drill(session, parts[0]));
        }

        /// <summary>
        /// 메뉴 진입. 재생 중이면 지금 근무에 바로 돌린다. 편집 모드면 무장하고 Bootstrap 에서 재생을 시작한다 —
        /// 역 씬을 직접 재생하면 provider 런타임이 먼저 깨어나 보존 근무를 고를 수 없기 때문이다. 타이틀에서
        /// 플레이 시작 → 랜덤 비상상황 대응을 고르면 <see cref="Tick"/> 이 근무가 준비되는 순간 돌린다.
        /// </summary>
        private static void Run(string family)
        {
            if (!EditorApplication.isPlaying)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (SceneManager.GetActiveScene().name != SceneFlow.TitleScene
                    && !EditorSceneManager.OpenScene(TitleScenePath).IsValid())
                {
                    Debug.LogError("[연습] 타이틀 씬을 열지 못했습니다: " + TitleScenePath + " · 무장하지 않습니다.");
                    return;
                }
                SessionState.SetString(ArmKey, family + "|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Debug.Log("[연습] 무장 → " + family + " · Bootstrap 에서 재생을 시작합니다. 타이틀에서 플레이 시작 → 랜덤 비상상황 대응을 고르십시오.");
                EditorApplication.EnterPlaymode();
                return;
            }
            var session = UnityEngine.Object.FindFirstObjectByType<EmergencySession>();
            if (session == null || session.Incidents == null)
            {
                Debug.LogError("[연습] 근무(EmergencySession)를 찾지 못했습니다. 역 씬이 provider 런타임으로 떠 있으면 재생을 멈추고 메뉴를 다시 누르십시오(Bootstrap 에서 시작).");
                return;
            }
            EditorCoroutine.Start(Drill(session, family));
        }

        private static IEnumerator Drill(EmergencySession session, string family)
        {
            var director = session.Incidents;
            var record = new JObject
            {
                ["at"] = DateTime.UtcNow.ToString("o"),
                ["family"] = family,
                ["shiftSeconds"] = Math.Round(session.ShiftSeconds, 1),
            };
            var wanted = Families[family];

            // 근무가 자리를 잡을 때까지 기다린다. 부팅 직후에는 승객이 아직 들어오는 중이라(첫 측정에서
            // 110 → 147명) 그 순간 오른 후보는 대표성이 떨어지고, 기록의 시각이 전부 t=0 으로 찍힌다.
            float settle = Seconds("CG_PRACTICE_SETTLE", 30f);
            while (session.ShiftSeconds < settle) yield return null;
            record["shiftSeconds"] = Math.Round(session.ShiftSeconds, 1);
            int timelineFrom = session.Log != null ? session.Log.Timeline.Count : 0;

            var execute = typeof(IncidentDirector).GetMethod("Execute", Members);
            var know = typeof(IncidentDirector).GetMethod("Know", Members);
            var hazardField = typeof(IncidentDirector).GetField("all", Members);
            if (execute == null || know == null || hazardField == null)
            {
                Fail(record, "IncidentDirector 의 Execute/Know/위험 목록을 찾지 못했습니다 ("
                             + (execute != null) + "/" + (know != null) + "/" + (hazardField != null) + ")");
                yield break;
            }

            // ① 이 계열의 원인이 후보에 오를 때까지 기다린다. 사람이 자리를 잡아야 생기는 후보가 있다.
            //
            // 실내로 제한한다(#270 촬영에서 배운 것): 거르지 않으면 하늘광장 같은 실외 후보가 먼저 잡힌다.
            // 판정은 세계가 쓰는 것을 그대로 쓴다(StationWorld.IsOutdoorZone). 위치를 알 수 없는 후보는
            // 추측하지 않고 거르되 몇 건을 걸렀는지 기록에 남긴다. CG_PRACTICE_INDOOR=0 이면 제한을 끈다.
            bool indoorOnly = Environment.GetEnvironmentVariable("CG_PRACTICE_INDOOR") != "0";
            // 구역 허용 목록(선택): CG_PRACTICE_ZONES=hall2f,main2f 처럼 적으면 그 구역(접두 일치)의 후보만 고른다.
            // 2층 남측 게이트처럼 보행 영역이 분리된 곳(#242)에서는 구급대가 닿지 못하므로, 촬영·훈련은 대합실로 좁힌다.
            var zonesWanted = (Environment.GetEnvironmentVariable("CG_PRACTICE_ZONES") ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(z => z.Trim()).Where(z => z.Length > 0).ToArray();
            var inFamily = new HashSet<string>();
            var outdoors = new HashSet<string>();
            var unplaced = new HashSet<string>();
            var otherZone = new HashSet<string>();
            object chosen = null;
            float deadline = Time.realtimeSinceStartup + OfferWaitSeconds;
            while (chosen == null && Time.realtimeSinceStartup < deadline)
            {
                var offered = Origins(director);
                if (offered == null) { Fail(record, "IncidentDirector.Origins 를 부를 수 없습니다"); yield break; }
                foreach (var candidate in offered)
                {
                    if (Array.IndexOf(wanted, Text(candidate, "Kind")) < 0) continue;
                    var key = Text(candidate, "Key") ?? "";
                    inFamily.Add(key);
                    if (!indoorOnly) { chosen = candidate; break; }
                    var subject = candidate.GetType().GetField("Subject")?.GetValue(candidate) as Component;
                    if (subject == null) { unplaced.Add(key); continue; }
                    var zone = session.World.ZoneId(subject.transform.position);
                    if (StationWorld.IsOutdoorZone(zone)) { outdoors.Add(key); continue; }
                    if (zonesWanted.Length > 0 && !zonesWanted.Any(z => zone.StartsWith(z, StringComparison.Ordinal))) { otherZone.Add(key); continue; }
                    chosen = candidate;
                    record["zone"] = zone;
                    break;
                }
                if (chosen == null) yield return null;
            }
            record["indoorOnly"] = indoorOnly;
            record["zones"] = zonesWanted.Length > 0 ? (JToken)new JArray(zonesWanted) : JValue.CreateNull();
            record["candidates"] = new JObject { ["family"] = inFamily.Count, ["outdoor"] = outdoors.Count, ["unknownPosition"] = unplaced.Count, ["otherZone"] = otherZone.Count };
            if (chosen == null)
            {
                record["result"] = "not_offered";
                Debug.LogWarning("[연습] " + OfferWaitSeconds + "초 안에 쓸 수 있는 " + family + " 계열 후보가 없었습니다. 계열 후보 "
                                 + inFamily.Count + "건 중 실외 " + outdoors.Count + "건, 위치 불명 " + unplaced.Count + "건을 걸렀습니다"
                                 + (indoorOnly ? "" : " (실내 제한 꺼짐)") + ".");
                Save(record);
                yield break;
            }

            record["kind"] = Text(chosen, "Kind");
            record["key"] = Text(chosen, "Key");
            record["description"] = Text(chosen, "Description");

            // 크기: 질환 계열은 **가운데**(연습이므로 최악도 최선도 피한다). 심정지는 4단계가 곧 심정지이므로
            // 즉시형 1.0(→4단계), 악화형 .75(→3단계, 뒤에 한 단계 올린다). 단계가 없는 전이는 크기를
            // 쓰지 않으므로 0 을 넘긴다(Levels 가 null 인 경우). Execute 는 round(m×4) 로 단계를 정한다.
            var levels = chosen.GetType().GetField("Levels")?.GetValue(chosen) as List<string>;
            var onset = Onset;   // 환경변수는 여기서 한 번만 읽는다 — 기록·크기·악화 분기가 같은 값을 본다.
            float magnitude = Magnitude(family, levels, onset);
            record["levels"] = levels == null ? (JToken)JValue.CreateNull() : new JArray(levels);
            record["magnitude"] = magnitude;
            record["onset"] = family == CardiacFamily ? onset : "n/a";

            // ② 한 건을 일으킨다. 무엇이 새로 생겼는지 보려면 **전후를 비교**해야 한다.
            var before = new HashSet<Hazard>(Hazards(hazardField, director));
            try { execute.Invoke(director, new object[] { chosen, magnitude, 1, "practice" }); }
            catch (TargetInvocationException problem)
            {
                Fail(record, "Execute 가 던졌습니다: " + problem.InnerException?.Message);
                yield break;
            }
            yield return null;

            var fresh = Hazards(hazardField, director).Where(h => !before.Contains(h)).ToList();
            if (fresh.Count == 0)
            {
                record["result"] = "nothing_created";
                Debug.LogWarning("[연습] Execute 는 돌았지만 새 위험이 생기지 않았습니다. 후보가 사이에 사라졌을 수 있습니다.");
                Save(record);
                yield break;
            }

            // ③ 역무원이 **그 건만** 인지한다. 실제 플레이에서는 보거나 듣거나 신고를 받아야 하는 자리를,
            //    프로토타입에서는 하네스가 대신한다. 손 떼기(CG_PRACTICE_HANDS_OFF=1)면 인지·무전을 역무원
            //    (사람 또는 시험의 오토파일럿)에게 남긴다 — 제품의 E·Q 경로로 직접 확인하고 보고하게.
            bool handsOff = Environment.GetEnvironmentVariable("CG_PRACTICE_HANDS_OFF") == "1";
            record["handsOff"] = handsOff;
            if (!handsOff) foreach (var hazard in fresh) know.Invoke(director, new object[] { hazard, "practice" });
            yield return null;

            // ④ **대응 지시를 받아 적는다.** 이것이 연습의 정답이고, 이미 제품 코드에 있다.
            var guidance = new JArray();
            foreach (var hazard in fresh)
                guidance.Add(new JObject
                {
                    ["label"] = hazard.Label,
                    ["where"] = hazard.Where,
                    ["visible"] = hazard.Visible,
                    ["reportable"] = hazard.Reportable,
                    ["reportOption"] = hazard.ReportOption,
                    ["reportLine"] = hazard.ReportLine,
                    ["officeReply"] = hazard.OfficeReply,
                });
            record["guidance"] = guidance;

            // ⑤ 무전은 **그 건의 보고 선택지만** 보낸다. 전부 보내지 않고도 그 건을 고를 수 있다 —
            //    무전 라벨이 "역무실 · " + Hazard.ReportOption 으로 만들어지고(IncidentDirector 의 보고
            //    선택지 한 곳), ReportOption 에는 장소가 들어 있어 건을 특정한다. 접두사를 이쪽에
            //    적어 두면 제품이 말투를 바꿀 때 조용히 빗나가므로 **접미 일치**로 맞춘다.
            //    (완전일치로 재다 첫 실행에서 radioSent 가 빈 채로 나왔다 — 그래서 고친 자리다.)
            var open = new List<EmergencySession.RadioOption>();
            foreach (var provider in session.RadioProviders.ToArray())
            {
                var options = provider();
                if (options != null) open.AddRange(options);
            }
            record["radioOpen"] = new JArray(open.Select(o => o.Label));
            var sent = new JArray();
            if (!handsOff)
                foreach (var hazard in fresh)
                {
                    if (!hazard.Reportable) continue;
                    int at = open.FindIndex(o => o.Label != null && o.Label.EndsWith(hazard.ReportOption, StringComparison.Ordinal));
                    if (at < 0) continue;
                    open[at].Send?.Invoke();
                    sent.Add(open[at].Label);
                }
            record["radioSent"] = sent;

            // ⑥ 역할 경계: 이 연습은 수리를 시도하지 않는다. 지시문이 그렇게 말한다 —
            //    "직접 제지하지 말고", "문을 억지로 열지 말고", "선로로 내려가지 말고".
            record["roleBoundary"] = new JObject { ["repairAttempted"] = false };

            // ⑦ 환자 상태. 심정지 악화형은 역무원이 AED 를 곁에 두지 못한 채 시간이 지나면 한 단계 올린다 —
            //    3단계(의식 없음·숨은 쉼)가 4단계(호흡 비정상 = 심정지)가 되고, 그 뒤 보고의 역무실 지시에
            //    "가까운 자동심장충격기를 가져와" 가 붙는다. 처치됐거나 AED 가 곁에 있으면 올리지 않는다.
            var casualty = fresh.OfType<CollapseHazard>().FirstOrDefault();
            record["startLevel"] = casualty != null ? (JToken)casualty.Level : JValue.CreateNull();
            record["worsenedAt"] = JValue.CreateNull();
            if (family == CardiacFamily && onset == "worsen" && casualty != null)
            {
                var change = typeof(IncidentDirector).GetMethod("ChangeCondition", Members);
                if (change == null) Debug.LogWarning("[연습] IncidentDirector.ChangeCondition 을 찾지 못해 악화를 건너뜁니다.");
                else
                {
                    float due = session.ShiftSeconds + Seconds("CG_PRACTICE_WORSEN_AFTER", 60f);
                    while (session.ShiftSeconds < due && casualty.Active && !casualty.Treated && casualty.AedAt < 0) yield return null;
                    if (casualty.Active && !casualty.Treated && casualty.AedAt < 0 && casualty.Level < 4)
                    {
                        try { change.Invoke(director, new object[] { casualty, 1 }); record["worsenedAt"] = Math.Round(session.ShiftSeconds, 1); }
                        catch (TargetInvocationException problem) { Debug.LogWarning("[연습] 악화 적용이 던졌습니다: " + problem.InnerException?.Message); }
                    }
                }
            }

            // ⑧ 손 떼기면 역무원의 대응이 끝날 때까지 기록을 미룬다: 구급대가 처치했거나, 역무원 쪽이 끝났다고
            //    알리거나(CG_PRACTICE_PLAYER_DONE=1, 같은 프로세스의 시험이 둔다), 상한(CG_PRACTICE_RECORD_AFTER)이 지나면.
            if (handsOff && casualty != null)
            {
                float cap = session.ShiftSeconds + Seconds("CG_PRACTICE_RECORD_AFTER", 600f);
                while (session.ShiftSeconds < cap && casualty.Active && !casualty.Treated
                       && Environment.GetEnvironmentVariable("CG_PRACTICE_PLAYER_DONE") != "1") yield return null;
            }
            yield return null;
            if (casualty != null)
            {
                record["level"] = casualty.Level;
                record["notBreathingNormally"] = casualty.NotBreathingNormally;
                record["aedAt"] = casualty.AedAt >= 0 ? (JToken)Math.Round(casualty.AedAt - casualty.StartedAt, 1) : JValue.CreateNull();
                record["handover"] = casualty.Handover;
                // 악화형에서 악화가 안 일어난 이유를 가리기 위해: 구급대가 먼저 처치했으면 올리지 않는 것이 맞다.
                record["treated"] = casualty.Treated;
            }
            var timeline = Timeline(session, timelineFrom);
            record["timeline"] = timeline;
            // 손 떼기에서는 역무원이 무전했는지를 근무 타임라인("역무실에 … 보고")으로 안다 — 하네스가 보낸 것이 아니다.
            bool reported = sent.Count > 0
                            || (handsOff && timeline.Any(entry => ((string)entry["text"] ?? "").StartsWith("역무실에 ", StringComparison.Ordinal)
                                                                   && ((string)entry["text"]).Contains("보고")));
            record["result"] = reported ? "reported" : "known_not_reported";
            Save(record);
        }

        private static float Magnitude(string family, List<string> levels, string onset)
        {
            if (levels == null || levels.Count == 0) return 0f;
            if (family == CardiacFamily) return onset == "worsen" ? .75f : 1f;
            return .5f;
        }

        private static IEnumerable<Hazard> Hazards(FieldInfo field, IncidentDirector director) =>
            (field.GetValue(director) as IList ?? new List<Hazard>()).Cast<Hazard>().ToList();

        /// <summary>지금 세계가 내놓는 원인 후보. 못 부르면 null — 추측하지 않는다.</summary>
        private static List<object> Origins(IncidentDirector director)
        {
            var method = typeof(IncidentDirector).GetMethod("Origins", Members);
            if (method == null) { Debug.LogError("[연습] IncidentDirector.Origins 를 찾지 못했습니다"); return null; }
            try
            {
                var list = new List<object>();
                foreach (var item in (IEnumerable)method.Invoke(director, null)) list.Add(item);
                return list;
            }
            catch (TargetInvocationException problem)
            {
                Debug.LogError("[연습] Origins 가 던졌습니다: " + problem.InnerException?.Message);
                return null;
            }
        }

        private static float Seconds(string variable, float fallback) =>
            float.TryParse(Environment.GetEnvironmentVariable(variable), out var parsed) && parsed >= 0 ? parsed : fallback;

        private static string Text(object transition, string field) =>
            transition?.GetType().GetField(field)?.GetValue(transition) as string;

        private static JArray Timeline(EmergencySession session, int from)
        {
            var entries = new JArray();
            if (session.Log == null) return entries;
            for (int i = from; i < session.Log.Timeline.Count; i++)
                entries.Add(new JObject
                {
                    ["t"] = Math.Round(session.Log.Timeline[i].Seconds, 1),
                    ["clock"] = session.Log.Clock(session.Log.Timeline[i].Seconds),
                    ["text"] = session.Log.Timeline[i].Text,
                });
            return entries;
        }

        private static void Fail(JObject record, string why)
        {
            record["result"] = "harness_error";
            record["error"] = why;
            Debug.LogError("[연습] " + why);
            Save(record);
        }

        private static void Save(JObject record)
        {
            Directory.CreateDirectory(OutFolder);
            var path = Path.Combine(OutFolder,
                "drill-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, record.ToString(), new UTF8Encoding(false));
            lastFile = path;
            Debug.Log("[연습] " + record["family"] + " · " + (record["kind"] ?? "(없음)")
                      + " · " + record["result"] + " · " + path);
        }

        /// <summary>
        /// 보존 근무 선택. 역 씬이 로드되기 전에 provider 준비를 미루고(<c>PlatformSeededSimulator.DeferPreparationForLegacyTests</c>,
        /// 시험 픽스처와 같은 private seam 을 리플렉션으로), 로드되면 provider 루트를 끄고 역무원이 없으면 연습용
        /// 플레이어를 둔다. 재생이 끝나면 되돌린다. 픽스처가 먼저 seam 을 잡았으면(PlayMode 시험) 건드리지 않는다.
        /// </summary>
        private static class RetainedSession
        {
            private const string ProviderRootName = "CHOOGuard Provider Runtime";
            private const string ProviderTypeName = "ChooGuard.Seeded.Unity.PlatformSeededSimulator, ChooGuard.Seeded";
            private static bool installed, ownsDeferral;
            private static PropertyInfo deferral;

            public static bool EnsureInstalled(out string problem)
            {
                problem = null;
                if (installed) return true;
                // 테스트 러너 아래서는 시험 픽스처(RetainedEmergencySessionFixture)가 같은 seam 을 잡는다. 먼저 잡으면 픽스처가
                // "leaked" 로 거부하므로 여기서는 물러난다 — 배치는 -runTests, 창 러너는 InitTestScene 활성 씬으로 안다.
                if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "-runTests", StringComparison.OrdinalIgnoreCase))
                    || SceneManager.GetActiveScene().name.StartsWith("InitTestScene", StringComparison.Ordinal))
                { installed = true; return true; }
                var type = Type.GetType(ProviderTypeName, false);
                if (type == null) { installed = true; return true; }   // provider 런타임이 없는 프로젝트: 할 일이 없다.
                var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
                if (station.isLoaded && ProviderRoot(station) is GameObject awake && awake.activeInHierarchy)
                {
                    problem = "역 씬이 이미 provider 런타임으로 떠 있습니다. Bootstrap 에서 재생을 시작하거나 PlayMode 시험으로 돌리십시오.";
                    return false;
                }
                deferral = type.GetProperty("DeferPreparationForLegacyTests", BindingFlags.NonPublic | BindingFlags.Static);
                if (deferral == null || deferral.PropertyType != typeof(bool)) { problem = "provider 준비 유예 seam 이 없습니다."; return false; }
                ownsDeferral = !(bool)deferral.GetValue(null);
                if (ownsDeferral) deferral.SetValue(null, true);
                SceneManager.sceneLoaded += OnSceneLoaded;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                installed = true;
                return true;
            }

            private static GameObject ProviderRoot(Scene scene) =>
                scene.GetRootGameObjects().FirstOrDefault(root => root.name == ProviderRootName);

            private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != SceneFlow.StationScene) return;
                var root = ProviderRoot(scene);
                if (root != null && root.activeSelf)
                {
                    // 선택 루트는 제어 객체여야 한다. 형상이 붙어 있으면 끄지 않는다(원본 형상을 지우지 않는다).
                    if (root.GetComponentsInChildren<Renderer>(true).Length > 0 || root.GetComponentsInChildren<Collider>(true).Length > 0)
                    { Debug.LogError("[연습] provider 루트에 형상이 붙어 있어 끄지 않습니다. 보존 근무를 고를 수 없습니다."); return; }
                    root.SetActive(false);
                    Debug.Log("[연습] 보존 근무 선택 · provider 런타임 비활성화");
                }
                EnsureResponder(scene);
            }

            private static void EnsureResponder(Scene scene)
            {
                bool present = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<FirstPersonResponder>(true))
                    .Any(player => player.gameObject.activeInHierarchy);
                if (present) return;
                var player = new GameObject("연습용 역무원"); player.SetActive(false);
                SceneManager.MoveGameObjectToScene(player, scene); player.layer = 2;
                player.AddComponent<CharacterController>();
                var responder = player.AddComponent<FirstPersonResponder>();
                var cameraObject = new GameObject("FirstPersonCamera", typeof(Camera)); cameraObject.transform.SetParent(player.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.nearClipPlane = .05f; camera.farClipPlane = 1500; camera.fieldOfView = 70;
                responder.PlayerCamera = camera; responder.BodyHeight = 1.72f; responder.EyeHeight = 1.60f; responder.EnableJump = false;
                player.SetActive(true);
                Debug.Log("[연습] 역무원이 없어 연습용 플레이어를 두었습니다.");
            }

            private static void OnPlayModeChanged(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode) Release();
            }

            private static void Release()
            {
                if (!installed) return;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                try { if (ownsDeferral) deferral?.SetValue(null, false); }
                catch (Exception problem) { Debug.LogWarning("[연습] provider 준비 유예를 되돌리지 못했습니다: " + problem.Message); }
                installed = false; ownsDeferral = false; deferral = null;
            }
        }

        /// <summary>재생 중 에디터에서 코루틴을 돌린다. 재생이 끝나면 스스로 떨어진다.</summary>
        private static class EditorCoroutine
        {
            public static void Start(IEnumerator routine)
            {
                void Step()
                {
                    bool more;
                    try { more = EditorApplication.isPlaying && routine.MoveNext(); }
                    catch (Exception problem) { Debug.LogError("[연습] " + problem); more = false; }
                    if (!more) EditorApplication.update -= Step;
                }
                EditorApplication.update += Step;
            }
        }
    }
}
