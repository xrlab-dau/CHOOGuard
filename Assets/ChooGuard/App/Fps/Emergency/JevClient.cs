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

        /// <summary>
        /// JEV's probability per level of a Score question with <paramref name="levels"/> levels, normalised to sum to 1
        /// (all mass on the rounded score when JEV gave no distribution).
        /// </summary>
        public float[] LevelProbabilities(int levels)
        {
            var result = new float[levels];
            float total = 0;
            if (Probabilities != null)
                foreach (var pair in Probabilities)
                    if (int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) && level >= 0 && level < levels && pair.Value > 0)
                    {
                        result[level] = pair.Value;
                        total += pair.Value;
                    }
            if (total <= 0)
            {
                result[Mathf.Clamp(Mathf.RoundToInt(Score), 0, levels - 1)] = 1;
                return result;
            }
            for (int i = 0; i < levels; i++) result[i] /= total;
            return result;
        }
    }

    /// <summary>
    /// Direct TypeSafe System One client (POST /v1/systemone) with explicit budgets. JEV only ranks candidates the game
    /// built; control flow and world writes stay in code. The key comes from <see cref="JevKey"/> (this machine's user)
    /// and is never logged or written by the client. Every request belongs to a <see cref="JevLane"/>; the lanes share
    /// the budget of <see cref="JevBudget"/> (the director reserved first, urgent crowd decisions next, routine ones the
    /// remainder) and each has its own in-flight limit.
    /// </summary>
    public sealed class JevClient
    {
        public const string Endpoint = "https://api.typesafe.ai/v1/systemone";
        public const string Model = "jev-latest";
        /// <summary>Run logs kept on disk: the newest files up to these limits, oldest deleted first when a shift starts.</summary>
        public const int KeepRunLogs = 30;
        public const long KeepRunLogBytes = 400L * 1024 * 1024;
        /// <summary>One shift's log stops growing here (a shift of this size is far beyond a normal one).</summary>
        public const long MaxRunLogBytes = 64L * 1024 * 1024;
        public float TimeoutSeconds = 8f;

        public JevKeySource Source { get; }
        public bool Available => key != null && !disabled;
        /// <summary>The server refused the key (401/403): it stays off for this shift.</summary>
        public bool Rejected => disabled;
        public string Status { get; private set; }
        /// <summary>Failed requests since the last answer (0 while JEV answers).</summary>
        public int FailuresInARow { get; private set; }
        public string LastModel { get; private set; } = "";
        /// <summary>Requests, tokens and dollars of all lanes together: the last minute's rate and the shift's totals.</summary>
        public JevUsage Usage => budget.Usage(Time.realtimeSinceStartup);
        public JevUsage UsageOf(JevLane lane) => budget.Usage(lane, Time.realtimeSinceStartup);

        private readonly string key;
        private readonly JevBudget budget = new JevBudget();
        private bool disabled;
        private readonly string logPath;
        private long logBytes;

        public JevClient(string runLogPath)
        {
            key = JevKey.Load(out var source);
            Source = source;
            logPath = runLogPath;
            TrimRunLogs(runLogPath);
            Status = source == JevKeySource.Off ? "JEV 꺼짐(TYPESAFE_API_KEY=off) · 비상상황을 만들지 않음" :
                key == null ? "JEV 키 없음 · 비상상황을 만들 수 없음" :
                JevKey.KnownFor(key) == JevKeyCheck.Rejected ? "JEV 키 거부 · 타이틀의 JEV 연결에서 다시 입력" : "JEV 연결 준비";
            if (key != null && JevKey.KnownFor(key) == JevKeyCheck.Rejected) disabled = true;
        }

        /// <summary>True when a request of <paramref name="lane"/> may be sent now under the lane's in-flight limit and the shared budget.</summary>
        public bool CanSend(JevLane lane) => Available && budget.CanSend(lane, Time.realtimeSinceStartup);

        /// <summary>Old two-lane form (critical = urgent crowd lane, otherwise routine); it goes once the crowd uses lanes directly.</summary>
        public bool CanSend(bool critical = false) => CanSend(critical ? JevLane.CrowdUrgent : JevLane.CrowdRoutine);

        /// <summary>
        /// Warms the first request up while the shift loads: serialises one representative payload (Newtonsoft's reflection and
        /// JIT, which cost the first request of a new shape ~20 ms on the main thread) and makes one cheap authenticated call
        /// (GET /v1/models, no model run) so DNS, TLS and the connection are ready before the first burst of requests.
        /// </summary>
        public IEnumerator Warm()
        {
            if (!Available) yield break;
            var sample = new JObject
            {
                ["model"] = Model,
                ["state"] = JToken.FromObject(new { place = "warm-up", people = new List<object> { new { what = "x", where = "y", now = true } }, tags = new[] { "a", "b" }, counts = new Dictionary<string, int> { ["a"] = 1 }, korean = "부산역" }),
                ["questions"] = new JObject
                {
                    ["score"] = new JObject { ["type"] = "score", ["instructions"] = "x", ["criteria"] = new JArray("a", "b") },
                    ["choice"] = new JObject { ["type"] = "choice", ["instructions"] = "x", ["criteria"] = new JObject { ["a"] = "x" } },
                },
            };
            Encoding.UTF8.GetBytes(sample.ToString(Formatting.None));
            using (var request = UnityWebRequest.Get(JevKey.ModelsEndpoint))
            {
                request.SetRequestHeader("Authorization", "Bearer " + key);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = JevKey.TimeoutSeconds;
                yield return request.SendWebRequest();
            }
        }

        /// <summary>Old two-lane form of <see cref="Ask(string, object, IReadOnlyList{JevChoice}, Action{Dictionary{string, JevAnswer}}, JevLane)"/>.</summary>
        public IEnumerator Ask(string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done, bool critical = false) =>
            Ask(purpose, state, questions, done, critical ? JevLane.CrowdUrgent : JevLane.CrowdRoutine);

        /// <summary>
        /// Sends one request holding several independent questions over the same state. The callback always runs on
        /// the main thread; answers is null when the request failed, timed out or was refused by the budget.
        /// </summary>
        public IEnumerator Ask(string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done, JevLane lane)
        {
            if (questions.Count == 0 || !CanSend(lane)) { done(null); yield break; }
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
            long estimate = EstimateInputTokens(body.Length);
            if (!budget.CanSend(lane, Time.realtimeSinceStartup, estimate)) { done(null); yield break; }
            var ticket = budget.Begin(lane, Time.realtimeSinceStartup, estimate);
            float started = Time.realtimeSinceStartup;
            Dictionary<string, JevAnswer> answers = null;
            long? inputTokens = null;
            long outputTokens = 0;
            using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + key);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Mathf.CeilToInt(TimeoutSeconds);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var root = JObject.Parse(request.downloadHandler.text);
                        LastModel = (string)root["model"] ?? "";
                        var usage = root["usage"];
                        if (usage != null) { inputTokens = (long?)usage["input_tokens"]; outputTokens = (long?)usage["output_tokens"] ?? 0; }
                        answers = Parse(root, questions);
                    }
                    catch (Exception) { answers = null; }
                }
                budget.End(ticket, inputTokens, outputTokens, answers == null);
                bool refused = request.responseCode == 401 || request.responseCode == 403;
                if (answers == null)
                {
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
                Log(purpose, lane, payload, answers, request.responseCode, Time.realtimeSinceStartup - started, inputTokens);
            }
            done(answers);
        }

        /// <summary>
        /// Input tokens of a request body before JEV counts them (measured on this project's requests: 1.75–2.5 bytes of
        /// UTF-8 JSON per token, the Korean-heavy small ones densest). The estimate is replaced by JEV's count on the answer.
        /// </summary>
        private static long EstimateInputTokens(int bodyBytes) => (long)(bodyBytes / 1.75f) + 1;

        private static Dictionary<string, JevAnswer> Parse(JObject root, IReadOnlyList<JevChoice> questions)
        {
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

        /// <summary>
        /// Keeps the run-log folder bounded: the newest <see cref="KeepRunLogs"/> files and at most
        /// <see cref="KeepRunLogBytes"/> in all; the log of the shift starting now (<paramref name="current"/>) is never touched.
        /// </summary>
        private static void TrimRunLogs(string current)
        {
            if (string.IsNullOrEmpty(current)) return;
            try
            {
                var files = new DirectoryInfo(Path.GetDirectoryName(current)).GetFiles("jev-*.jsonl");
                Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                long bytes = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    bytes += files[i].Length;
                    if (files[i].FullName == Path.GetFullPath(current)) continue;
                    if (i >= KeepRunLogs || bytes > KeepRunLogBytes) files[i].Delete();
                }
            }
            catch (Exception) { }
        }

        private void Log(string purpose, JevLane lane, JObject payload, Dictionary<string, JevAnswer> answers, long status, float seconds, long? inputTokens)
        {
            if (string.IsNullOrEmpty(logPath)) return;
            try
            {
                var entry = new JObject
                {
                    ["at"] = DateTime.UtcNow.ToString("o"),
                    ["purpose"] = purpose,
                    ["lane"] = lane.ToString(),
                    ["http"] = status,
                    ["seconds"] = Math.Round(seconds, 3),
                    ["model"] = LastModel,
                    ["input_tokens"] = inputTokens,
                    ["request"] = payload,
                    ["answers"] = answers == null ? null : JToken.FromObject(answers),
                };
                if (logBytes >= MaxRunLogBytes) return;
                var line = entry.ToString(Formatting.None) + "\n";
                File.AppendAllText(logPath, line);
                logBytes += line.Length;
            }
            catch (Exception) { }
        }
    }
}
