using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// One typed question sent to JEV: Choice over candidates the game built from the current state, or Score on an
    /// ordered scale (<see cref="Levels"/>, lowest first) the game wrote.
    /// </summary>
    public sealed class JevChoice
    {
        public string Id;
        public string Instructions;
        public readonly Dictionary<string, string> Criteria = new Dictionary<string, string>();
        /// <summary>Score questions only: level descriptions from lowest to highest (2–10 levels).</summary>
        public List<string> Levels;
        public bool IsScore => Levels != null;
    }

    public sealed class JevAnswer
    {
        public string Choice;
        /// <summary>Score questions: JEV's expected level (0 = lowest).</summary>
        public float Score;
        public float Confidence;
        /// <summary>Choice: probability per option key. Score: probability per level index ("0", "1", …).</summary>
        public Dictionary<string, float> Probabilities;

        /// <summary>
        /// One option drawn from JEV's probabilities, so what JEV judges plausible happens in proportion rather than its
        /// top pick every time. Falls back to the top pick when JEV gave no distribution.
        /// </summary>
        public string Draw(System.Random random)
        {
            if (Probabilities == null || Probabilities.Count == 0) return Choice;
            float total = 0;
            foreach (var pair in Probabilities) total += Mathf.Max(0, pair.Value);
            if (total <= 0) return Choice;
            float roll = (float)random.NextDouble() * total;
            string last = Choice;
            foreach (var pair in Probabilities)
            {
                if (pair.Value <= 0) continue;
                last = pair.Key;
                roll -= pair.Value;
                if (roll <= 0) return pair.Key;
            }
            return last;
        }

        /// <summary>One level (0 = lowest) drawn from JEV's per-level probabilities; the rounded score when it gave none.</summary>
        public int DrawLevel(System.Random random, int levels)
        {
            var drawn = Probabilities != null && Probabilities.Count > 0 ? Draw(random) : null;
            int level = drawn != null && int.TryParse(drawn, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : Mathf.RoundToInt(Score);
            return Mathf.Clamp(level, 0, Mathf.Max(0, levels - 1));
        }
    }

    /// <summary>
    /// Direct TypeSafe System One client (POST /v1/systemone) with explicit budgets. JEV only ranks candidates the game
    /// built; control flow and world writes stay in code. The key comes from <see cref="JevKey"/> (this machine's user)
    /// and is never logged or written by the client. Composition requests are critical: crowd batches leave them one
    /// in-flight slot and part of the per-minute budget.
    /// </summary>
    public sealed class JevClient
    {
        public const string Endpoint = "https://api.typesafe.ai/v1/systemone";
        public const string Model = "jev-latest";
        public int MaxRequestsPerRun = 1500;
        public int MaxRequestsPerMinute = 90;
        public int MaxInFlight = 3;
        /// <summary>Per-minute requests kept free of crowd batches for composition.</summary>
        public int ReservedPerMinute = 12;
        public float TimeoutSeconds = 8f;

        public JevKeySource Source { get; }
        public bool Available => key != null && !disabled;
        /// <summary>The server refused the key (401/403): it stays off for this shift.</summary>
        public bool Rejected => disabled;
        public string Status { get; private set; }
        public int Requests { get; private set; }
        public int Failures { get; private set; }
        /// <summary>Failed requests since the last answer (0 while JEV answers).</summary>
        public int FailuresInARow { get; private set; }
        public long InputTokens { get; private set; }
        public long OutputTokens { get; private set; }
        public string LastModel { get; private set; } = "";

        private readonly string key;
        private readonly Queue<float> recent = new Queue<float>();
        private int inFlight;
        private bool disabled;
        private readonly string logPath;

        public JevClient(string runLogPath)
        {
            key = JevKey.Load(out var source);
            Source = source;
            logPath = runLogPath;
            Status = source == JevKeySource.Off ? "JEV 꺼짐(TYPESAFE_API_KEY=off) · 비상상황을 만들지 않음" :
                key == null ? "JEV 키 없음 · 비상상황을 만들 수 없음" :
                JevKey.KnownFor(key) == JevKeyCheck.Rejected ? "JEV 키 거부 · 타이틀의 JEV 연결에서 다시 입력" : "JEV 연결 준비";
            if (key != null && JevKey.KnownFor(key) == JevKeyCheck.Rejected) disabled = true;
        }

        /// <summary>
        /// True when a request may be sent now under the per-minute, per-run and in-flight caps. Non-critical (crowd)
        /// requests leave one in-flight slot and <see cref="ReservedPerMinute"/> of the minute to critical ones.
        /// </summary>
        public bool CanSend(bool critical = false)
        {
            if (!Available || Requests >= MaxRequestsPerRun) return false;
            if (inFlight >= (critical ? MaxInFlight : MaxInFlight - 1)) return false;
            float now = Time.realtimeSinceStartup;
            while (recent.Count > 0 && now - recent.Peek() > 60f) recent.Dequeue();
            return recent.Count < (critical ? MaxRequestsPerMinute : MaxRequestsPerMinute - ReservedPerMinute);
        }

        /// <summary>
        /// Sends one request holding several independent questions over the same state. The callback always runs on
        /// the main thread; answers is null when the request failed, timed out or was refused by the caps.
        /// </summary>
        public IEnumerator Ask(string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done, bool critical = false)
        {
            if (!CanSend(critical) || questions.Count == 0) { done(null); yield break; }
            inFlight++;
            Requests++;
            recent.Enqueue(Time.realtimeSinceStartup);
            var questionObject = new JObject();
            foreach (var question in questions)
            {
                if (question.IsScore)
                {
                    questionObject[question.Id] = new JObject { ["type"] = "score", ["instructions"] = question.Instructions, ["criteria"] = new JArray(question.Levels) };
                    continue;
                }
                var criteria = new JObject();
                foreach (var pair in question.Criteria) criteria[pair.Key] = pair.Value;
                questionObject[question.Id] = new JObject { ["type"] = "choice", ["instructions"] = question.Instructions, ["criteria"] = criteria };
            }
            var payload = new JObject { ["model"] = Model, ["state"] = JToken.FromObject(state), ["questions"] = questionObject };
            var body = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            float started = Time.realtimeSinceStartup;
            Dictionary<string, JevAnswer> answers = null;
            using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + key);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Mathf.CeilToInt(TimeoutSeconds);
                yield return request.SendWebRequest();
                inFlight--;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { answers = Parse(request.downloadHandler.text, questions); }
                    catch (Exception) { answers = null; }
                }
                bool refused = request.responseCode == 401 || request.responseCode == 403;
                if (answers == null)
                {
                    Failures++;
                    FailuresInARow++;
                    Status = refused ? "JEV 키 거부 · 타이틀의 JEV 연결에서 다시 입력" :
                        request.responseCode == 429 ? "JEV 요청 한도 · 잠시 뒤 다시 물음" : "JEV 응답 없음 · 다시 묻는 중";
                    if (refused) { disabled = true; JevKey.Observed(key, JevKeyCheck.Rejected); }
                }
                else
                {
                    FailuresInARow = 0;
                    Status = "JEV 연결됨 · " + LastModel;
                    JevKey.Observed(key, JevKeyCheck.Accepted);
                }
                Log(purpose, payload, answers, request.responseCode, Time.realtimeSinceStartup - started);
            }
            done(answers);
        }

        private Dictionary<string, JevAnswer> Parse(string text, IReadOnlyList<JevChoice> questions)
        {
            var root = JObject.Parse(text);
            LastModel = (string)root["model"] ?? "";
            var usage = root["usage"];
            if (usage != null) { InputTokens += (long?)usage["input_tokens"] ?? 0; OutputTokens += (long?)usage["output_tokens"] ?? 0; }
            var answersToken = root["answers"] as JObject;
            if (answersToken == null) return null;
            var result = new Dictionary<string, JevAnswer>();
            foreach (var question in questions)
            {
                var answer = answersToken[question.Id] as JObject;
                if (answer == null) continue;
                var probabilities = new Dictionary<string, float>();
                if (question.IsScore)
                {
                    var score = (float?)answer["score"];
                    // 척도 밖의 값은 받아들이지 않는다.
                    if (score == null || score < 0 || score > question.Levels.Count - 1) continue;
                    if (answer["probabilities"] is JObject levels)
                        foreach (var pair in levels)
                            if (int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) && level >= 0 && level < question.Levels.Count)
                                probabilities[pair.Key] = (float?)pair.Value ?? 0;
                    result[question.Id] = new JevAnswer { Score = score.Value, Confidence = (float?)answer["confidence"] ?? 0, Probabilities = probabilities };
                    continue;
                }
                var choice = (string)answer["choice"];
                // 후보 밖의 답은 받아들이지 않는다. 게임이 만든 후보만 실행된다.
                if (choice == null || !question.Criteria.ContainsKey(choice)) continue;
                if (answer["probabilities"] is JObject p)
                    foreach (var pair in p)
                        if (question.Criteria.ContainsKey(pair.Key)) probabilities[pair.Key] = (float?)pair.Value ?? 0;
                result[question.Id] = new JevAnswer { Choice = choice, Confidence = (float?)answer["confidence"] ?? 0, Probabilities = probabilities };
            }
            return result;
        }

        private void Log(string purpose, JObject payload, Dictionary<string, JevAnswer> answers, long status, float seconds)
        {
            if (string.IsNullOrEmpty(logPath)) return;
            try
            {
                var entry = new JObject
                {
                    ["at"] = DateTime.UtcNow.ToString("o"),
                    ["purpose"] = purpose,
                    ["http"] = status,
                    ["seconds"] = Math.Round(seconds, 3),
                    ["model"] = LastModel,
                    ["request"] = payload,
                    ["answers"] = answers == null ? null : JToken.FromObject(answers),
                };
                File.AppendAllText(logPath, entry.ToString(Formatting.None) + "\n");
            }
            catch (Exception) { }
        }
    }
}
