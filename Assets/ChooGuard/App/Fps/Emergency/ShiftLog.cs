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
        /// <summary>
        /// Every composition round JEV decided (transition, magnitude, the probability it had). Saved, not shown on the end
        /// screen. Nothing is composed without JEV: rounds it did not answer are only counted (<see cref="DeferredRounds"/>).
        /// </summary>
        public readonly JArray Compositions = new JArray();
        public int JevCompositions { get; private set; }
        /// <summary>Composition rounds put off because JEV did not answer (asked again a few seconds later).</summary>
        public int DeferredRounds { get; private set; }

        private readonly EmergencySession session;
        private readonly HashSet<Extinguisher> used = new HashSet<Extinguisher>();
        private readonly HashSet<string> once = new HashSet<string>();

        public ShiftLog(EmergencySession owner) { session = owner; }

        public void Add(string text) => Timeline.Add(new Entry(session.ShiftSeconds, text));

        /// <summary>Adds the fact only the first time <paramref name="key"/> is seen.</summary>
        public void Once(string key, string text) { if (once.Add(key)) Add(text); }

        public void Composed(string kind, string key, float magnitude, int candidates, string detail)
        {
            Compositions.Add(new JObject { ["t"] = Math.Round(session.ShiftSeconds, 1), ["kind"] = kind, ["key"] = key, ["magnitude"] = Math.Round(magnitude, 2), ["candidates"] = candidates, ["by"] = "jev", ["detail"] = detail });
            JevCompositions++;
        }

        public void Deferred() => DeferredRounds++;

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
                ["deferred_rounds"] = DeferredRounds,
                ["compositions"] = Compositions,
                ["jev"] = new JObject
                {
                    ["status"] = session.Jev.Status, ["requests"] = session.Jev.Requests, ["failures"] = session.Jev.Failures,
                    ["input_tokens"] = session.Jev.InputTokens, ["output_tokens"] = session.Jev.OutputTokens,
                },
            };
        }

        public string Save(string ending)
        {
            try
            {
                var folder = Path.Combine(UnityEngine.Application.persistentDataPath, "shift-logs");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "shift-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
                File.WriteAllText(path, ToJson(ending).ToString(Formatting.Indented));
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ShiftLog] 기록 저장 실패: " + exception.Message);
                return null;
            }
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
                "<color=#ffffff88>상황 전개 판단  JEV " + log.JevCompositions + (log.DeferredRounds > 0 ? " · 응답 없어 미룸 " + log.DeferredRounds : "") + "\n" +
                "승객 판단  JEV " + log.JevDecisions + " · 지연 시 규칙 " + log.LocalDecisions + "\n" + session.Jev.Status + "</color>";

            FpsUiFactory.Button(panel, font, "다시 근무", new Vector2(0, 0), new Vector2(0, 0), new Vector2(300, 56), () => SceneFlow.StartShift(font, null), 24);
            FpsUiFactory.Button(panel, font, "타이틀로", new Vector2(0, 0), new Vector2(320, 0), new Vector2(300, 56), SceneFlow.ToTitle, 24);
            var path = log.Save(ending);
            if (path != null) Debug.Log("CG_SHIFT_LOG " + path);
            return result;
        }
    }
}
