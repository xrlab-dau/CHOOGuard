using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// One typed question sent to JEV: Choice over candidates the game built from the current state, or Score on an
    /// ordered scale (<see cref="Levels"/>, lowest first) the game wrote. A question and its level list must not be
    /// changed once asked: they are serialised on a pool thread.
    /// </summary>
    public sealed class JevChoice
    {
        public string Id;
        public string Instructions;
        public readonly Dictionary<string, string> Criteria = new Dictionary<string, string>();
        /// <summary>Score questions only: level descriptions from lowest to highest (2–10 levels).</summary>
        public List<string> Levels;
        public bool IsScore => Levels != null;
        /// <summary>Local log context only; never included in the gateway request. Identifies the causal kind and rating scale without parsing an entity key.</summary>
        public string Kind, Scale;
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
    /// remainder) and each has its own in-flight limit. Building the body, reading the answer and writing the run log
    /// happen on pool threads; the main thread only starts the request and hands over the answer.
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
        /// <summary>What each Ask took from the main thread (waiting for the pool threads and the network is not counted).</summary>
        public IReadOnlyList<Cost> MainThread => mainThread;
        /// <summary>The session's own run-log file, not the credential file.</summary>
        public string LogFile => logPath;
        /// <summary>Admitted requests awaiting response/log finalization, including failed or capped records.</summary>
        public int PendingRequests => Volatile.Read(ref pendingRequests);

        private readonly string key, authorization;
        private readonly JevBudget budget = new JevBudget();
        private readonly List<Cost> mainThread = new List<Cost>();
        private bool disabled;
        private readonly string logPath;
        private readonly object logLock = new object();
        private long logBytes;
        private int pendingRequests;

        public JevClient(string runLogPath)
        {
            key = JevKey.Load(out var source);
            authorization = key == null ? null : "Bearer " + key;
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

        /// <summary>
        /// Warms every path a request takes while the shift loads, so the first requests do not meet cold code (~20 ms
        /// each on the main thread): builds one representative body (Newtonsoft's reflection and JIT), reads a canned
        /// reply, builds a log line, creates and drops an unsent POST (upload and download handlers, headers), and makes
        /// one cheap authenticated call (GET /v1/models, no model run) so DNS, TLS and the connection are ready before
        /// the first burst of requests.
        /// </summary>
        public IEnumerator Warm()
        {
            if (!Available) yield break;
            var state = new { place = "warm-up", people = new List<object> { new { what = "x", where = "y", now = true } }, tags = new[] { "a", "b" }, counts = new Dictionary<string, int> { ["a"] = 1 }, korean = "부산역" };
            var score = new JevChoice { Id = "score", Instructions = "x", Levels = new List<string> { "a", "b" } };
            var choice = new JevChoice { Id = "choice", Instructions = "x" };
            choice.Criteria["a"] = "x";
            var questions = new[] { score, choice };
            var text = Body(state, questions);
            var bytes = Encoding.UTF8.GetBytes(text);
            var reply = Read(Encoding.UTF8.GetBytes("{\"model\":\"warm-up\",\"usage\":{\"input_tokens\":1,\"output_tokens\":1},\"answers\":{\"score\":{\"score\":1,\"confidence\":0.5,\"probabilities\":{\"0\":0.4,\"1\":0.6}},\"choice\":{\"choice\":\"a\",\"confidence\":1,\"probabilities\":{\"a\":1}}}}"), questions);
            LogLine(DateTime.UtcNow.ToString("o"), "warm-up", JevLane.Director, text, questions, reply?.Answers, 200, 0.1f, 1, reply?.Model);
            using (var unsent = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                unsent.uploadHandler = new UploadHandlerRaw(bytes) { contentType = "application/json" };
                unsent.downloadHandler = new DownloadHandlerBuffer();
                unsent.SetRequestHeader("Authorization", authorization);
                unsent.SetRequestHeader("Accept", "application/json");
                unsent.timeout = Mathf.CeilToInt(TimeoutSeconds);
            }
            using (var request = UnityWebRequest.Get(JevKey.ModelsEndpoint))
            {
                request.SetRequestHeader("Authorization", authorization);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = JevKey.TimeoutSeconds;
                yield return request.SendWebRequest();
            }
        }

        /// <summary>
        /// Sends one request holding several independent questions over the same state. The body is built on a pool thread
        /// from <paramref name="state"/> and <paramref name="questions"/>, so the caller must not change them (or the level
        /// lists) after asking; the request itself is created on the main thread, the answer is read and the run log
        /// written on pool threads. The callback always runs on the main thread; answers is null when the request failed,
        /// timed out or was refused by the budget.
        /// </summary>
        public IEnumerator Ask(string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done, JevLane lane)
        {
            var span = DirectorSpan.Begin();
            Cost main = default;
            if (questions.Count == 0 || !CanSend(lane)) { done(null); yield break; }
            Interlocked.Increment(ref pendingRequests);
            bool recordingQueued = false;
            try
            {
                var preparing = Task.Run(() => { var text = Body(state, questions); return new Prepared(text, Encoding.UTF8.GetBytes(text)); });
                main += span.Stop();
                while (!preparing.IsCompleted) yield return null;
                span = DirectorSpan.Begin();
                if (preparing.IsFaulted) { mainThread.Add(main + span.Stop()); done(null); yield break; }
                var prepared = preparing.Result;
                long estimate = EstimateInputTokens(prepared.Bytes.Length);
                if (!budget.CanSend(lane, Time.realtimeSinceStartup, estimate)) { mainThread.Add(main + span.Stop()); done(null); yield break; }
                var ticket = budget.Begin(lane, Time.realtimeSinceStartup, estimate);
                float started = Time.realtimeSinceStartup;
                byte[] answerBytes = null;
                long code;
                using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(prepared.Bytes) { contentType = "application/json" };
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", authorization);
                    request.SetRequestHeader("Accept", "application/json");
                    request.timeout = Mathf.CeilToInt(TimeoutSeconds);
                    var sending = request.SendWebRequest();
                    main += span.Stop();
                    yield return sending;
                    span = DirectorSpan.Begin();
                    code = request.responseCode;
                    if (request.result == UnityWebRequest.Result.Success) answerBytes = request.downloadHandler.data;
                }
                Reply reply = null;
                if (answerBytes != null)
                {
                    var parsing = Task.Run(() => Read(answerBytes, questions));
                    main += span.Stop();
                    while (!parsing.IsCompleted) yield return null;
                    span = DirectorSpan.Begin();
                    reply = parsing.IsFaulted ? null : parsing.Result;
                }
                var answers = reply?.Answers;
                budget.End(ticket, reply?.InputTokens, reply?.OutputTokens ?? 0, answers == null);
                if (reply != null) LastModel = reply.Model;
                bool refused = code == 401 || code == 403;
                if (answers == null)
                {
                    FailuresInARow++;
                    Status = refused ? "JEV 키 거부 · 타이틀의 JEV 연결에서 다시 입력" :
                        code == 429 ? "JEV 요청 한도 · 잠시 뒤 다시 물음" : "JEV 응답 없음 · 다시 묻는 중";
                    if (refused) { disabled = true; JevKey.Observed(key, JevKeyCheck.Rejected); }
                }
                else
                {
                    FailuresInARow = 0;
                    Status = "JEV 연결됨 · " + LastModel;
                    JevKey.Observed(key, JevKeyCheck.Accepted);
                }
                WriteLog(purpose, lane, prepared.Text, questions, answers, code, Time.realtimeSinceStartup - started, reply?.InputTokens, LastModel);
                recordingQueued = true;
                mainThread.Add(main + span.Stop());
                done(answers);
            }
            finally
            {
                if (!recordingQueued) Interlocked.Decrement(ref pendingRequests);
            }
        }

        private sealed class Prepared
        {
            public readonly string Text;
            public readonly byte[] Bytes;

            public Prepared(string text, byte[] bytes)
            {
                Text = text;
                Bytes = bytes;
            }
        }

        private sealed class Reply
        {
            public string Model;
            public long? InputTokens;
            public long OutputTokens;
            public Dictionary<string, JevAnswer> Answers;
        }

        // 같은 수준 문구 목록은 요청마다 다시 직렬화하지 않는다(문구 목록은 물은 뒤 바꾸지 않는다).
        private static readonly ConditionalWeakTable<List<string>, string> LevelsJson = new ConditionalWeakTable<List<string>, string>();

        /// <summary>
        /// The request body: the model, the state and one question per <see cref="JevChoice"/> (a Score carries its level
        /// texts, a Choice its options). Written straight to text; the state may be any object Newtonsoft can serialise.
        /// </summary>
        public static string Body(object state, IReadOnlyList<JevChoice> questions)
        {
            var text = new StringBuilder(2048 + questions.Count * 700);
            using (var writer = new JsonTextWriter(new StringWriter(text, CultureInfo.InvariantCulture)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("model");
                writer.WriteValue(Model);
                writer.WritePropertyName("state");
                writer.WriteRawValue(state is JToken token ? token.ToString(Formatting.None) : JsonConvert.SerializeObject(state));
                writer.WritePropertyName("questions");
                writer.WriteStartObject();
                foreach (var question in questions)
                {
                    writer.WritePropertyName(question.Id);
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue(question.IsScore ? "score" : "choice");
                    writer.WritePropertyName("instructions");
                    writer.WriteValue(question.Instructions);
                    writer.WritePropertyName("criteria");
                    if (question.IsScore) writer.WriteRawValue(LevelsJson.GetValue(question.Levels, levels => JsonConvert.SerializeObject(levels)));
                    else
                    {
                        writer.WriteStartObject();
                        foreach (var pair in question.Criteria)
                        {
                            writer.WritePropertyName(pair.Key);
                            writer.WriteValue(pair.Value);
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            return text.ToString();
        }

        /// <summary>
        /// Input tokens of a request body before JEV counts them (measured on this project's requests: 1.75–2.5 bytes of
        /// UTF-8 JSON per token, the Korean-heavy small ones densest). The estimate is replaced by JEV's count on the answer.
        /// </summary>
        private static long EstimateInputTokens(int bodyBytes) => (long)(bodyBytes / 1.75f) + 1;

        /// <summary>Reads JEV's reply (UTF-8 JSON: model, token usage, one answer per question) — meant for a pool thread; null when it is not a usable reply.</summary>
        private static Reply Read(byte[] body, IReadOnlyList<JevChoice> questions)
        {
            try
            {
                var root = JObject.Parse(Encoding.UTF8.GetString(body));
                var reply = new Reply { Model = (string)root["model"] ?? "" };
                var usage = root["usage"];
                if (usage != null) { reply.InputTokens = (long?)usage["input_tokens"]; reply.OutputTokens = (long?)usage["output_tokens"] ?? 0; }
                reply.Answers = Parse(root, questions);
                return reply.Answers == null ? null : reply;
            }
            catch (Exception) { return null; }
        }

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

        /// <summary>One log entry as a line of text (built on a pool thread: serialising the request text and answers stays off the main thread).</summary>
        private static string LogLine(string at, string purpose, JevLane lane, string requestText, IReadOnlyList<JevChoice> questions, Dictionary<string, JevAnswer> answers, long status, float seconds, long? inputTokens, string model)
        {
            var entry = new JObject
            {
                ["at"] = at,
                ["purpose"] = purpose,
                ["lane"] = lane.ToString(),
                ["http"] = status,
                ["seconds"] = Math.Round(seconds, 3),
                ["model"] = model,
                ["input_tokens"] = inputTokens,
                ["request"] = new JRaw(requestText),
                ["answers"] = answers == null ? null : JToken.FromObject(answers),
            };
            JObject metadata = null;
            for (int i = 0; i < questions.Count; i++)
            {
                var question = questions[i];
                if (question.Kind == null) continue;
                if (metadata == null) metadata = new JObject();
                metadata[question.Id] = new JObject { ["kind"] = question.Kind, ["scale"] = question.Scale };
            }
            if (metadata != null) entry["question_metadata"] = metadata;
            return entry.ToString(Formatting.None) + "\n";
        }

        /// <summary>Appends one entry to the run log on a pool thread (building the line and writing the file stay off the main thread).</summary>
        private void WriteLog(string purpose, JevLane lane, string requestText, IReadOnlyList<JevChoice> questions, Dictionary<string, JevAnswer> answers, long status, float seconds, long? inputTokens, string model)
        {
            if (string.IsNullOrEmpty(logPath)) { Interlocked.Decrement(ref pendingRequests); return; }
            var at = DateTime.UtcNow.ToString("o");
            Task.Run(() =>
            {
                try
                {
                    var line = LogLine(at, purpose, lane, requestText, questions, answers, status, seconds, inputTokens, model);
                    lock (logLock)
                    {
                        if (logBytes >= MaxRunLogBytes) return;
                        File.AppendAllText(logPath, line);
                        logBytes += line.Length;
                    }
                }
                catch (Exception) { }
                finally { Interlocked.Decrement(ref pendingRequests); }
            });
        }
    }
}
