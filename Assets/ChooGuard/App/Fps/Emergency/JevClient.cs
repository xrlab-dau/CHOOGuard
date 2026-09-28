using System;
using System.Collections;
using System.Collections.Generic;
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
        /// <summary>Score questions: the level index JEV chose (0 = lowest).</summary>
        public float Score;
        public float Confidence;
        public Dictionary<string, float> Probabilities;
    }

    /// <summary>
    /// Direct TypeSafe System One client (POST /v1/systemone) with explicit budgets. JEV only ranks candidates the game
    /// built; control flow and world writes stay in code. When unreachable, callers fall back to deterministic rules.
    /// The key is read from TYPESAFE_API_KEY or ~/.chooguard/typesafe.key and is never logged or written.
    /// </summary>
    public sealed class JevClient
    {
        public const string Endpoint = "https://api.typesafe.ai/v1/systemone";
        public const string Model = "jev-latest";
        public int MaxRequestsPerRun = 1500;
        public int MaxRequestsPerMinute = 90;
        public int MaxInFlight = 3;
        public float TimeoutSeconds = 8f;

        public bool Available => key != null && !disabled;
        public string Status { get; private set; }
        public int Requests { get; private set; }
        public int Failures { get; private set; }
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
            key = LoadKey();
            logPath = runLogPath;
            Status = key == null ? "JEV 키 없음 · 로컬 규칙으로 진행" : "JEV 연결 준비";
        }

        private static string LoadKey()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) return Valid(fromEnvironment.Trim());
            try
            {
                var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".chooguard", "typesafe.key");
                if (File.Exists(file)) return Valid(File.ReadAllText(file).Trim());
            }
            catch (Exception) { }
            return null;
        }

        private static string Valid(string value) => value.Length >= 20 && value.IndexOfAny(new[] { '\r', '\n', ' ' }) < 0 ? value : null;

        /// <summary>True when a request may be sent now under the per-minute, per-run and in-flight caps.</summary>
        public bool CanSend()
        {
            if (!Available || inFlight >= MaxInFlight || Requests >= MaxRequestsPerRun) return false;
            float now = Time.realtimeSinceStartup;
            while (recent.Count > 0 && now - recent.Peek() > 60f) recent.Dequeue();
            return recent.Count < MaxRequestsPerMinute;
        }

        /// <summary>
        /// Sends one request holding several independent questions over the same state. The callback always runs on
        /// the main thread; answers is null when the request failed, timed out or was refused by the caps.
        /// </summary>
        public IEnumerator Ask(string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done)
        {
            if (!CanSend() || questions.Count == 0) { done(null); yield break; }
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
                if (answers == null)
                {
                    Failures++;
                    Status = request.responseCode == 401 || request.responseCode == 403 ? "JEV 인증 거부 · 로컬 규칙으로 진행" :
                        request.responseCode == 429 ? "JEV 요청 한도 · 잠시 로컬 규칙" : "JEV 응답 없음 · 로컬 규칙";
                    if (request.responseCode == 401 || request.responseCode == 403) disabled = true;
                }
                else Status = "JEV 연결됨 · " + LastModel;
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
                if (question.IsScore)
                {
                    var score = (float?)answer?["score"];
                    // 척도 밖의 값은 받아들이지 않는다.
                    if (score == null || score < 0 || score > question.Levels.Count - 1) continue;
                    result[question.Id] = new JevAnswer { Score = score.Value, Confidence = (float?)answer["confidence"] ?? 0 };
                    continue;
                }
                var choice = (string)answer?["choice"];
                // 후보 밖의 답은 받아들이지 않는다. 게임이 만든 후보만 실행된다.
                if (choice == null || !question.Criteria.ContainsKey(choice)) continue;
                var probabilities = new Dictionary<string, float>();
                if (answer["probabilities"] is JObject p) foreach (var pair in p) probabilities[pair.Key] = (float?)pair.Value ?? 0;
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
