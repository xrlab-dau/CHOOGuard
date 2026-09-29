using System;
using System.Collections.Generic;
using System.IO;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// What actually happened during the shift, in order. The end screen shows only these facts (no scoring or
    /// quiz); the same record is written as JSON for verification.
    /// </summary>
    public sealed class ShiftLog
    {
        public readonly struct Entry
        {
            public readonly float Seconds;
            public readonly string Text;
            public Entry(float seconds, string text) { Seconds = seconds; Text = text; }
        }

        public readonly List<Entry> Timeline = new List<Entry>();
        public readonly Dictionary<string, int> Choices = new Dictionary<string, int>();
        public int Guided { get; private set; }
        public float SprayedSeconds { get; private set; }
        public int ExtinguishersUsed { get; private set; }
        public int JevDecisions { get; private set; }
        public int LocalDecisions { get; private set; }
        /// <summary>Every event the director made happen (transition, magnitude, the levels behind it). Saved, not shown on the end screen.</summary>
        public readonly JArray Compositions = new JArray();
        /// <summary>Events that happened: new emergencies and developments of existing ones.</summary>
        public int Events { get; private set; }
        /// <summary>Of <see cref="Events"/>, the new emergencies.</summary>
        public int NewEmergencies { get; private set; }
        /// <summary>The director's judging (rounds JEV answered, the hazard it produced, what a heartbeat costs), as opposed to the events.</summary>
        public readonly DirectorRecord Director;

        private const int KeepRecords = 50;
        private readonly EmergencySession session;
        private readonly HashSet<Extinguisher> used = new HashSet<Extinguisher>();
        private readonly HashSet<string> once = new HashSet<string>();

        public ShiftLog(EmergencySession owner) { session = owner; Director = new DirectorRecord(owner); }

        public void Add(string text) => Timeline.Add(new Entry(session.ShiftSeconds, text));

        /// <summary>Adds the fact only the first time <paramref name="key"/> is seen.</summary>
        public void Once(string key, string text) { if (once.Add(key)) Add(text); }

        public void Composed(string kind, string key, bool origin, float magnitude, int candidates, string detail)
        {
            Compositions.Add(new JObject { ["t"] = Math.Round(session.ShiftSeconds, 1), ["kind"] = kind, ["key"] = key, ["origin"] = origin, ["magnitude"] = Math.Round(magnitude, 2), ["candidates"] = candidates, ["by"] = "jev", ["detail"] = detail });
            Events++;
            if (origin) NewEmergencies++;
        }

        public void Decision(Passenger who, string kind, string choice, string source)
        {
            string key = kind + ":" + choice;
            Choices[key] = Choices.TryGetValue(key, out var n) ? n + 1 : 1;
            if (source == "JEV") JevDecisions++; else LocalDecisions++;
        }

        public void AddGuided(int count)
        {
            if (count <= 0) return;
            Guided += count;
            Once("guided", "역무원이 승객에게 대피 방향을 직접 안내하기 시작");
        }

        public void Picked(Extinguisher tool)
        {
            if (used.Add(tool)) ExtinguishersUsed++;
            Add("소화기 " + tool.Serial + " 을 들었다");
        }

        public void PinPulled(Extinguisher tool) => Add("소화기 " + tool.Serial + " 안전핀을 뽑았다");
        public void DefectiveUsed(Extinguisher tool) => Add("소화기 " + tool.Serial + " 는 압력이 없어 약제가 나오지 않았다");

        public void Sprayed(FireHazard fire, float quality, float deltaSeconds)
        {
            SprayedSeconds += deltaSeconds;
            Once("spray-" + fire.Id, "불을 향해 소화 약제를 뿌리기 시작");
        }

        public string Clock(float seconds) => session.Clock(seconds);

        public JObject ToJson(string ending)
        {
            var timeline = new JArray();
            foreach (var entry in Timeline) timeline.Add(new JObject { ["t"] = Math.Round(entry.Seconds, 1), ["clock"] = Clock(entry.Seconds), ["text"] = entry.Text });
            var crowd = session.Crowd;
            return new JObject
            {
                ["ending"] = ending,
                ["seed"] = session.World.Seed,
                ["shift_seconds"] = Math.Round(session.ShiftSeconds, 1),
                ["timeline"] = timeline,
                ["guided"] = Guided,
                ["sprayed_seconds"] = Math.Round(SprayedSeconds, 1),
                ["extinguishers_used"] = ExtinguishersUsed,
                ["crowd"] = new JObject
                {
                    ["spawned"] = crowd.Spawned, ["in_station"] = crowd.InStation, ["in_train"] = crowd.People.Count - crowd.InStation, ["evacuated"] = crowd.Evacuated,
                    ["left_normally"] = crowd.LeftNormally, ["injured"] = crowd.Injured.Count,
                },
                ["decisions"] = new JObject { ["jev"] = JevDecisions, ["local"] = LocalDecisions, ["choices"] = JObject.FromObject(Choices) },
                ["compositions"] = Compositions,
                ["director"] = Director.ToJson(Events, NewEmergencies),
                ["jev"] = new JObject
                {
                    ["status"] = session.Jev.Status,
                    ["usage"] = UsageJson(session.Jev.Usage),
                    ["lanes"] = new JObject
                    {
                        ["director"] = UsageJson(session.Jev.UsageOf(JevLane.Director)),
                        ["crowd_urgent"] = UsageJson(session.Jev.UsageOf(JevLane.CrowdUrgent)),
                        ["crowd_routine"] = UsageJson(session.Jev.UsageOf(JevLane.CrowdRoutine)),
                    },
                    ["budget"] = new JObject { ["requests_per_minute"] = JevBudget.MaxRequestsPerMinute, ["dollars_per_hour"] = JevBudget.MaxDollarsPerHour, ["dollars_per_million_input_tokens"] = JevBudget.DollarsPerMillionInputTokens },
                },
            };
        }

        /// <summary>A lane's spend: the shift's totals, the average hourly rate over the shift and the highest minute.</summary>
        private JObject UsageJson(JevUsage usage) => new JObject
        {
            ["requests"] = usage.Requests, ["failures"] = usage.Failures, ["input_tokens"] = usage.InputTokens, ["output_tokens"] = usage.OutputTokens,
            ["dollars"] = Math.Round(usage.Dollars, 5),
            ["average_dollars_per_hour"] = session.ShiftSeconds > 0 ? Math.Round(usage.Dollars * 3600 / session.ShiftSeconds, 4) : 0,
            ["peak_requests_per_minute"] = usage.PeakRequestsPerMinute, ["peak_dollars_per_hour"] = Math.Round(usage.PeakDollarsPerHour, 4),
        };

        public string Save(string ending)
        {
            try
            {
                var folder = Path.Combine(UnityEngine.Application.persistentDataPath, "shift-logs");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "shift-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
                File.WriteAllText(path, ToJson(ending).ToString(Formatting.Indented));
                Trim(folder, path);
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ShiftLog] 기록 저장 실패: " + exception.Message);
                return null;
            }
        }

        /// <summary>Keeps the folder of shift records bounded to the newest <see cref="KeepRecords"/> (the one just written stays).</summary>
        private static void Trim(string folder, string current)
        {
            try
            {
                var files = new DirectoryInfo(folder).GetFiles("shift-*.json");
                Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = KeepRecords; i < files.Length; i++)
                    if (files[i].FullName != current) files[i].Delete();
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// What the director's real-time judging did over the shift, separate from the events it caused: rounds JEV answered,
    /// how it rated candidates (levels per scale), the hazard the game drew against at every step (so the expected time to
    /// an event can be recomputed from the record), and what a heartbeat costs on the machine.
    /// </summary>
    public sealed class DirectorRecord
    {
        /// <summary>Requests in which JEV rated candidates or answered a magnitude, and requests it did not answer (nothing is drawn against a judgment that never came).</summary>
        public int Rounds { get; private set; }
        public int Unanswered { get; private set; }
        /// <summary>Candidate ratings JEV gave, over all rounds.</summary>
        public int Judged { get; private set; }
        /// <summary>Draws whose subject was no longer eligible (or had changed) when they were applied: nothing happened.</summary>
        public int Vanished { get; private set; }
        /// <summary>Multiplier on every hazard rate in this shift (1 in play; a calibration run sets it lower, see <see cref="IncidentDirector.RateScaleVariable"/>).</summary>
        public float RateScale { get; set; } = 1f;

        private readonly EmergencySession session;
        private readonly int[,] levels = new int[3, Imminence.LevelCount];
        private readonly JArray trajectory = new JArray();
        private readonly List<float> enumerationMs = new List<float>();
        private readonly List<float> requestMs = new List<float>();
        private readonly List<float> totalMs = new List<float>();
        private readonly List<float> sliceMs = new List<float>();
        private float firstNewEmergency = -1;

        public DirectorRecord(EmergencySession owner) { session = owner; }

        public void Round(bool answered) { if (answered) Rounds++; else Unanswered++; }

        /// <summary>A candidate JEV rated: counted at its most probable level of the scale.</summary>
        public void Rated(ImminenceScale scale, float[] probabilities)
        {
            int top = 0;
            for (int level = 1; level < probabilities.Length; level++) if (probabilities[level] > probabilities[top]) top = level;
            levels[(int)scale, top]++;
            Judged++;
        }

        public void Vanish() => Vanished++;

        public void NewEmergency() { if (firstNewEmergency < 0) firstNewEmergency = session.ShiftSeconds; }

        /// <summary>
        /// The hazard the draw used over the last <paramref name="dt"/> seconds of game time (events per second): how many
        /// candidates were listed, how many of them counted with their own rating and how many with the rating of the newest
        /// candidate of their kind (not rated yet), and the highest terms.
        /// </summary>
        public void Trace(float dt, float origin, float development, int candidates, int rated, int borrowed, string top)
        {
            trajectory.Add(new JArray(Math.Round(session.ShiftSeconds, 2), Math.Round(dt, 3), origin, development, candidates, rated, borrowed, top));
        }

        /// <summary>
        /// One heartbeat's cost in milliseconds: listing and reconciling candidates and building the state, then (when a
        /// request was needed, otherwise negative) building and starting it. The listing is spread over frames.
        /// </summary>
        public void Beat(float enumeration, float request)
        {
            enumerationMs.Add(enumeration);
            if (request >= 0) requestMs.Add(request);
            totalMs.Add(enumeration + Mathf.Max(0, request));
        }

        /// <summary>What the director took from one frame (a step of the listing, or the closing step that also asks and draws).</summary>
        public void Slice(float milliseconds) => sliceMs.Add(milliseconds);

        public JObject ToJson(int events, int newEmergencies)
        {
            return new JObject
            {
                ["rate_scale"] = RateScale, ["rounds"] = Rounds, ["unanswered_rounds"] = Unanswered, ["rated_candidates"] = Judged, ["vanished_draws"] = Vanished,
                ["events"] = events, ["new_emergencies"] = newEmergencies, ["first_new_emergency_at"] = firstNewEmergency < 0 ? null : (JToken)Math.Round(firstNewEmergency, 1),
                ["levels"] = new JObject { ["calm_origin"] = Counts(0), ["incident_origin"] = Counts(1), ["development"] = Counts(2) },
                ["heartbeat_ms"] = new JObject { ["heartbeats"] = totalMs.Count, ["requests_built"] = requestMs.Count, ["frame_slices"] = sliceMs.Count, ["per_frame"] = Percentiles(sliceMs), ["enumeration"] = Percentiles(enumerationMs), ["request"] = Percentiles(requestMs), ["total"] = Percentiles(totalMs) },
                ["hazard_columns"] = new JArray("t", "dt", "origin_per_second", "development_per_second", "candidates", "rated", "borrowed", "top_terms"),
                ["hazard"] = trajectory,
            };
        }

        private JArray Counts(int scale)
        {
            var counts = new JArray();
            for (int level = 0; level < Imminence.LevelCount; level++) counts.Add(levels[scale, level]);
            return counts;
        }

        private static JObject Percentiles(List<float> samples)
        {
            if (samples.Count == 0) return new JObject { ["p50"] = 0, ["p95"] = 0, ["max"] = 0 };
            var sorted = new List<float>(samples);
            sorted.Sort();
            float At(float fraction) => sorted[Mathf.Min(sorted.Count - 1, Mathf.FloorToInt(fraction * sorted.Count))];
            return new JObject { ["p50"] = Math.Round(At(.5f), 3), ["p95"] = Math.Round(At(.95f), 3), ["max"] = Math.Round(sorted[sorted.Count - 1], 3) };
        }
    }

    /// <summary>End-of-shift screen: the facts in order and two ways out. No score.</summary>
    public sealed class ShiftResult : MonoBehaviour
    {
        public static ShiftResult Show(EmergencySession session, string heading, string ending)
        {
            var canvas = FpsUiFactory.InteractiveCanvas("근무 결과", session.transform, 60);
            var result = canvas.AddComponent<ShiftResult>();
            var root = (RectTransform)canvas.transform;
            var font = session.KoreanFont;
            var shade = FpsUiFactory.Panel(root, "음영", Vector2.zero, Vector2.zero, new Color(.01f, .015f, .025f, .82f));
            FpsUiFactory.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            var panel = FpsUiFactory.Node(root, "결과");
            FpsUiFactory.Place(panel, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1100, 690));
            var title = FpsUiFactory.Text(panel, font, "제목", new Vector2(0, 1), new Vector2(0, 0), new Vector2(1100, 56), 38, TextAlignmentOptions.TopLeft);
            title.text = "근무 종료";
            title.fontStyle = FontStyles.Bold;
            var sub = FpsUiFactory.Text(panel, font, "부제", new Vector2(0, 1), new Vector2(0, -60), new Vector2(1100, 30), 20, TextAlignmentOptions.TopLeft);
            sub.text = heading;
            sub.color = FpsUiFactory.Accent;

            var log = session.Log;
            var lines = new System.Text.StringBuilder();
            int start = Mathf.Max(0, log.Timeline.Count - 16);
            if (start > 0) lines.Append("<color=#ffffff88>… 앞선 기록 ").Append(start).Append("건</color>\n");
            for (int i = start; i < log.Timeline.Count; i++)
                lines.Append("<color=#ffffff99>").Append(log.Clock(log.Timeline[i].Seconds)).Append("</color>  ").Append(log.Timeline[i].Text).Append('\n');
            var body = FpsUiFactory.Text(panel, font, "기록", new Vector2(0, 1), new Vector2(0, -108), new Vector2(700, 480), 16, TextAlignmentOptions.TopLeft);
            body.text = lines.ToString();

            var crowd = session.Crowd;
            var facts = FpsUiFactory.Text(panel, font, "수치", new Vector2(1, 1), new Vector2(0, -108), new Vector2(360, 480), 18, TextAlignmentOptions.TopLeft);
            facts.text =
                "직접 대피 안내  " + log.Guided + "명\n" +
                "역사 밖으로 대피  " + crowd.Evacuated + "명\n" +
                "역 안에 남은 승객  " + crowd.InStation + "명\n" +
                "부상  " + crowd.Injured.Count + "명\n" +
                "소화 약제 분사  " + log.SprayedSeconds.ToString("0") + "초 (" + log.ExtinguishersUsed + "대)\n\n" +
                "<color=#ffffff88>상황 판단  JEV " + log.Director.Rounds + "회 (후보 " + log.Director.Judged + "건 평가" + (log.Director.Unanswered > 0 ? " · 응답 없음 " + log.Director.Unanswered : "") + ")\n" +
                "일어난 사건  " + log.Events + "건 (새 비상상황 " + log.NewEmergencies + ")\n" +
                "승객 판단  JEV " + log.JevDecisions + " · 지연 시 규칙 " + log.LocalDecisions + "\n" +
                "JEV 사용  요청 " + session.Jev.Usage.Requests + "건 · 평균 $" + (session.ShiftSeconds > 0 ? session.Jev.Usage.Dollars * 3600 / session.ShiftSeconds : 0).ToString("0.00") + "/시간\n" + session.Jev.Status + "</color>";

            FpsUiFactory.Button(panel, font, "다시 근무", new Vector2(0, 0), new Vector2(0, 0), new Vector2(300, 56), () => SceneFlow.StartShift(font, null), 24);
            FpsUiFactory.Button(panel, font, "타이틀로", new Vector2(0, 0), new Vector2(320, 0), new Vector2(300, 56), SceneFlow.ToTitle, 24);
            var path = log.Save(ending);
            if (path != null) Debug.Log("CG_SHIFT_LOG " + path);
            return result;
        }
    }
}
