using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChooGuard.App.Fps.Emergency;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

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
    /// </remarks>
    [InitializeOnLoad]
    public static class PracticeDrill
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string OutFolder = ".planning/2026-10-05-practice-drill";
        private const float OfferWaitSeconds = 60f;
        private const string ArmKey = "ChooGuard.PracticeDrill.Armed";
        private const long ArmTtlSeconds = 900;
        private static string lastFile;

        /// <summary>
        /// 계열 = 그 계열에 드는 원인 <c>Transition.Kind</c> 들. 계열 구성은
        /// <c>workers/learning/README.md</c> 의 집계(원인 33종 / 11계열)와 같다.
        /// </summary>
        private static readonly Dictionary<string, string[]> Families = new Dictionary<string, string[]>
        {
            ["질환"] = new[] { "collapse", "seizure", "chest_pain", "breathing", "low_blood_sugar" },
        };

        static PracticeDrill() => EditorApplication.update += Tick;

        [MenuItem("ChooGuard/Practice/질환 계열 연습 한 건 (#270 프로토타입)")]
        public static void Illness() => Run("질환");

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
            var session = UnityEngine.Object.FindFirstObjectByType<EmergencySession>();
            if (session == null || session.Incidents == null) return;
            SessionState.EraseString(ArmKey);
            EditorCoroutine.Start(Drill(session, parts[0]));
        }

        private static void Run(string family)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[연습] 재생 중에만 돌 수 있습니다. 근무를 시작한 뒤 메뉴를 누르십시오.");
                return;
            }
            var session = UnityEngine.Object.FindFirstObjectByType<EmergencySession>();
            if (session == null || session.Incidents == null)
            {
                Debug.LogError("[연습] 근무(EmergencySession)를 찾지 못했습니다.");
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
            object chosen = null;
            float deadline = Time.realtimeSinceStartup + OfferWaitSeconds;
            while (chosen == null && Time.realtimeSinceStartup < deadline)
            {
                var offered = Origins(director);
                if (offered == null) { Fail(record, "IncidentDirector.Origins 를 부를 수 없습니다"); yield break; }
                chosen = offered.FirstOrDefault(t => Array.IndexOf(wanted, Text(t, "Kind")) >= 0);
                if (chosen == null) yield return null;
            }
            if (chosen == null)
            {
                record["result"] = "not_offered";
                Debug.LogWarning("[연습] " + OfferWaitSeconds + "초 안에 " + family
                                 + " 계열 후보가 오르지 않았습니다. 세계가 그 원인을 아직 가능하게 하지 않습니다.");
                Save(record);
                yield break;
            }

            record["kind"] = Text(chosen, "Kind");
            record["key"] = Text(chosen, "Key");
            record["description"] = Text(chosen, "Description");

            // 크기는 **가운데**로 고른다 — 연습이므로 최악도 최선도 피한다. 단계가 없는 전이는
            // 크기를 쓰지 않으므로 0 을 넘긴다(Levels 가 null 인 경우).
            var levels = chosen.GetType().GetField("Levels")?.GetValue(chosen) as List<string>;
            float magnitude = levels == null || levels.Count == 0 ? 0f : .5f;
            record["levels"] = levels == null ? (JToken)JValue.CreateNull() : new JArray(levels);
            record["magnitude"] = magnitude;

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
            //    프로토타입에서는 하네스가 대신한다.
            foreach (var hazard in fresh) know.Invoke(director, new object[] { hazard, "practice" });
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

            yield return null;
            record["timeline"] = Timeline(session, timelineFrom);
            record["result"] = sent.Count > 0 ? "reported" : "known_not_reported";
            Save(record);
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
