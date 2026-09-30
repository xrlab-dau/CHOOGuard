using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Regression harness for the emergency layer, driven from the play-mode gateway by /Users/um-yunsang/cg-orch/tools/force_all.py.
    /// While a shift plays it lists every cause <c>IncidentDirector.Origins()</c> offers, and for each kind — in a fresh shift with
    /// the same seed and JEV off, so the director composes nothing itself — forces one instance (<c>Execute</c> with a fixed
    /// magnitude), makes the staff member know it, sends every radio option once, hands the scene over to the commanding agency's
    /// lead as soon as it stands on scene, and records whether the incident started, the handover happened, the shift ended or
    /// the kind timed out, with the console errors and the timeline it produced. Results are written after every kind so a
    /// job that ends early leaves partial results. The director's private members are reached by reflection so the harness
    /// keeps compiling while the emergency layer is rewritten; a member it cannot find is reported, not guessed.
    /// </summary>
    [InitializeOnLoad]
    public static class EmergencyForceAll
    {
        private const string ConfigKey = "ChooGuard.ForceAll.Config";
        private const string JevVariable = "TYPESAFE_API_KEY";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class Wait { public bool TimedOut; }

        /// <summary>The game's own code threw while the harness called it (a finding about the game, not about the harness).</summary>
        private sealed class GameFailure : Exception
        {
            public GameFailure(string message) : base(message) { }
        }

        private static readonly Stack<IEnumerator> Frames = new Stack<IEnumerator>();
        private static readonly HashSet<string> Offered = new HashSet<string>();
        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>();
        private static readonly JArray Records = new JArray();
        private static JObject config;
        private static JObject current;
        private static JArray currentErrors;
        private static bool running, envSwitched;
        private static string originalKey, endedBecause;
        private static double startedAt;
        private static string results, phase = "load";

        static EmergencyForceAll()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        /// <summary>
        /// Arms the harness with the JSON config at <paramref name="configPath"/> (see force_all.py for the fields). The run
        /// begins by itself once play mode has a ready shift; call it from an edit-mode eval before the job's play phase.
        /// </summary>
        public static string Arm(string configPath)
        {
            var text = File.ReadAllText(configPath);
            var parsed = JObject.Parse(text);
            if (parsed["out"] == null) return "config has no 'out'";
            parsed["armedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SessionState.SetString(ConfigKey, parsed.ToString(Formatting.None));
            return "armed → " + parsed["out"];
        }

        /// <summary>Stops the run if it is still going (marking the kinds it never reached), disarms, and returns a one-screen summary.</summary>
        public static string Report()
        {
            if (running) Finish("the job ended before every kind was run");
            var path = results;
            if (path == null || !File.Exists(path)) return "no results (" + (SessionState.GetString(ConfigKey, "").Length > 0 ? "armed but never started" : "not armed") + ")";
            var report = JObject.Parse(File.ReadAllText(path));
            var lines = new List<string> { "ended: " + report["endedBecause"], "file: " + path };
            foreach (var record in (JArray)report["kinds"])
                lines.Add(record["kind"] + ": " + record["result"] + " started=" + record["started"] + " handover=" + record["handover"] + " ended=" + record["ended"] + " errors=" + ((JArray)record["consoleErrors"]).Count);
            return string.Join("\n", lines);
        }

        // ── 구동 ────────────────────────────────────────────────────────────

        private static void Tick()
        {
            var json = SessionState.GetString(ConfigKey, "");
            if (json.Length == 0) return;
            if (!EditorApplication.isPlaying)
            {
                if (running) Finish("play mode ended during the run");
                return;
            }
            if (!running)
            {
                var armed = JObject.Parse(json);
                // 무장만 하고 재생이 시작되지 않으면 다른 작업의 재생 세션에서 저절로 돌지 않게 유효 시간을 둔다.
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)armed["armedAt"] > Value(armed, "armTtlSeconds", 900f)) { SessionState.EraseString(ConfigKey); return; }
                Begin(armed);
            }
            KeepRunning();
            Step();
        }

        private static void Begin(JObject armed)
        {
            config = armed;
            results = (string)armed["out"];
            Records.Clear();
            Offered.Clear();
            Descriptions.Clear();
            Frames.Clear();
            endedBecause = null;
            startedAt = EditorApplication.timeSinceStartup;
            running = true;
            Frames.Push(Run());
            Log("armed run started · label=" + armed["label"]);
        }

        /// <summary>Advances the nested routines: a finished one hands control back to its parent within the same tick.</summary>
        private static void Step()
        {
            while (running && Frames.Count > 0)
            {
                var top = Frames.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception exception)
                {
                    // 종류 하나의 실패는 그 종류의 결과로 남기고 다음 종류로 넘어간다. 맨 아래 루틴(Run)이 던지면 실행을 끝낸다.
                    if (current == null || Frames.Count == 1) { Finish("harness exception: " + exception); return; }
                    current["result"] = exception is GameFailure || exception is TargetInvocationException ? "game_exception" : "harness_exception";
                    currentErrors.Add(exception.ToString());
                    while (Frames.Count > 1) Frames.Pop();
                    return;
                }
                if (!more) { Frames.Pop(); continue; }
                if (top.Current is IEnumerator inner) { Frames.Push(inner); continue; }
                return;
            }
            if (running) Finish("all kinds run");
        }

        private static void Finish(string because)
        {
            if (!running) return;
            running = false;
            endedBecause = because;
            Frames.Clear();
            if (current != null) { if ((string)current["result"] == "running") current["result"] = "aborted"; Close(); }
            var wanted = config["kinds"] is JArray list ? list.Select(k => (string)k).ToList() : Offered.OrderBy(k => k).ToList();
            var reached = Records.Select(r => (string)r["kind"]).ToHashSet();
            foreach (var kind in wanted.Where(k => !reached.Contains(k)))
                Records.Add(NewRecord(kind, "not_run"));
            Save();
            if (envSwitched) Environment.SetEnvironmentVariable(JevVariable, originalKey);
            envSwitched = false;
            Time.timeScale = 1f;
            SessionState.EraseString(ConfigKey);
            Log("run ended · " + because);
        }

        private static void Save()
        {
            var report = new JObject
            {
                ["label"] = config["label"],
                ["endedBecause"] = endedBecause ?? "running",
                ["seed"] = config["seed"],
                ["magnitude"] = Value(config, "magnitude", .5f),
                ["timescale"] = Value(config, "timescale", 4f),
                ["jev"] = "off (TYPESAFE_API_KEY=off for every shift of the run)",
                ["editorSeconds"] = Math.Round(EditorApplication.timeSinceStartup - startedAt),
                ["offeredKinds"] = new JArray(Offered.OrderBy(k => k)),
                ["descriptions"] = JObject.FromObject(Descriptions),
                ["kinds"] = Records,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(results)));
            File.WriteAllText(results, report.ToString(Formatting.Indented));
        }

        private static void Log(string line) =>
            File.AppendAllText(results + ".progress.log", DateTime.Now.ToString("HH:mm:ss") + " " + line + "\n");

        private static float Value(JObject source, string name, float fallback) => source[name] != null ? (float)source[name] : fallback;

        /// <summary>The gateway pauses nothing, but every new shift opens on its pause menu and a finished one pauses the player.</summary>
        private static void KeepRunning()
        {
            var session = EmergencySession.Current;
            if (!Ready(session) || session.Incidents.Stage == IncidentDirector.Phase.Ended) return;
            if (!session.Player.ExternalInputMode) session.Player.SetExternalInputMode(true);
            if (session.Player.IsPaused) session.Player.Resume(false);
            float scale = Value(config, "timescale", 4f);
            if (Time.timeScale != scale) Time.timeScale = scale;
        }

        private static bool Ready(EmergencySession session) =>
            session != null && session.Player != null && session.World != null && session.Crowd != null && session.Incidents != null && session.Log != null && session.Jev != null &&
            !SceneFlow.Loading && session.Crowd.People.Count > 0;

        // ── 기다림 ──────────────────────────────────────────────────────────

        /// <summary>Waits until <paramref name="condition"/> holds; gives up after game or real seconds (0 = no limit of that kind).</summary>
        private static IEnumerator Until(Func<bool> condition, float gameSeconds, float realSeconds, Wait outcome, float interval = 0f)
        {
            float game = Time.time;
            double real = EditorApplication.timeSinceStartup;
            float next = 0;
            while (true)
            {
                if (Time.time >= next)
                {
                    if (condition()) yield break;
                    next = Time.time + interval;
                }
                if ((gameSeconds > 0 && Time.time - game > gameSeconds) || (realSeconds > 0 && EditorApplication.timeSinceStartup - real > realSeconds))
                {
                    outcome.TimedOut = true;
                    yield break;
                }
                yield return null;
            }
        }

        private static IEnumerator Sleep(float gameSeconds)
        {
            float end = Time.time + gameSeconds;
            while (Time.time < end) yield return null;
        }

        // ── 실행 ────────────────────────────────────────────────────────────

        private static IEnumerator Run()
        {
            var wait = new Wait();
            yield return Until(() => Ready(EmergencySession.Current), 0, 300, wait);
            if (wait.TimedOut) { Finish("no ready shift in play mode after 300 s"); yield break; }
            // 사건은 이 도구가 만든다. 근무를 JEV 없이 시작하면 디렉터가 스스로 판단을 요청하지 않아 강제한 것만 일어난다.
            originalKey = Environment.GetEnvironmentVariable(JevVariable);
            Environment.SetEnvironmentVariable(JevVariable, "off");
            envSwitched = true;
            var probe = typeof(IncidentDirector).GetMethod("Origins", Members);
            var transition = typeof(IncidentDirector).GetNestedType("Transition", Members);
            var execute = typeof(IncidentDirector).GetMethod("Execute", Members);
            if (probe == null || transition == null || execute == null)
            {
                Finish("incompatible: IncidentDirector.Origins/Transition/Execute not found (" + (probe != null) + "/" + (transition != null) + "/" + (execute != null) + ")");
                yield break;
            }
            yield return StartShift(wait);
            if (wait.TimedOut) { Finish("the first fresh shift did not become ready"); yield break; }
            var kinds = config["kinds"] is JArray fixedKinds ? fixedKinds.Select(k => (string)k).ToList() : null;
            if (kinds == null)
            {
                float until = Time.time + Value(config, "discoverySeconds", 60f);
                while (Time.time < until) { Collect(EmergencySession.Current.Incidents); yield return Sleep(1f); }
                kinds = Offered.OrderBy(k => k).ToList();
                var skip = config["skip"] is JArray skipped ? skipped.Select(k => (string)k).ToList() : new List<string>();
                kinds = kinds.Where(k => !skip.Contains(k)).Skip((int)Value(config, "offset", 0)).ToList();
                if (config["limit"] != null) kinds = kinds.Take((int)Value(config, "limit", 0)).ToList();
                config["kinds"] = new JArray(kinds);
            }
            Log("kinds (" + kinds.Count + "): " + string.Join(", ", kinds));
            Save();
            bool fresh = true;
            foreach (var kind in kinds)
            {
                current = NewRecord(kind, "running");
                currentErrors = (JArray)current["consoleErrors"];
                if (!fresh) yield return StartShift(wait);
                fresh = false;
                if (wait.TimedOut) { current["result"] = "shift_not_ready"; Close(); continue; }
                yield return RunKind(kind, current);
                Close();
            }
        }

        private static JObject NewRecord(string kind, string result) => new JObject
        {
            ["kind"] = kind, ["result"] = result, ["started"] = false, ["handover"] = false, ["ended"] = false, ["timeout"] = false,
            ["consoleErrors"] = new JArray(), ["radioSent"] = new JArray(), ["radioUnsentAtEnd"] = new JArray(),
        };

        private static void Close()
        {
            Records.Add(current);
            Log(current["kind"] + " → " + current["result"] + " (handover=" + current["handover"] + ", errors=" + ((JArray)current["consoleErrors"]).Count + ")");
            current = null;
            currentErrors = null;
            Save();
        }

        /// <summary>A new shift with the run's seed, opened past its pause menu.</summary>
        private static IEnumerator StartShift(Wait outcome)
        {
            outcome.TimedOut = false;
            phase = "load";
            var old = EmergencySession.Current;
            var font = old != null ? old.KoreanFont : null;
            if (font == null) { outcome.TimedOut = true; yield break; }
            EmergencySession.NextSeed = (int)config["seed"];
            SceneFlow.StartShift(font, null);
            yield return Until(() => !SceneFlow.Loading && EmergencySession.Current != old && Ready(EmergencySession.Current), 0, 240, outcome);
            if (outcome.TimedOut) yield break;
            yield return Sleep(2f);
        }

        private static List<object> Origins(IncidentDirector director)
        {
            IEnumerable list;
            try { list = (IEnumerable)typeof(IncidentDirector).GetMethod("Origins", Members).Invoke(director, null); }
            catch (TargetInvocationException exception) { throw new GameFailure("IncidentDirector.Origins threw: " + exception.InnerException); }
            var result = new List<object>();
            foreach (var item in list) result.Add(item);
            return result;
        }

        private static string KindOf(object transition) => (string)transition.GetType().GetField("Kind").GetValue(transition);

        /// <summary>Notes every kind offered right now (and one description per kind for the report).</summary>
        private static void Collect(IncidentDirector director)
        {
            foreach (var item in Origins(director))
            {
                var kind = KindOf(item);
                Offered.Add(kind);
                if (!Descriptions.ContainsKey(kind)) Descriptions[kind] = (string)item.GetType().GetField("Description").GetValue(item);
            }
        }

        private static IEnumerator RunKind(string kind, JObject record)
        {
            double realStart = EditorApplication.timeSinceStartup;
            var session = EmergencySession.Current;
            var director = session.Incidents;
            record["seed"] = session.World.Seed;
            record["people"] = session.Crowd.People.Count;
            record["jev"] = session.Jev.Status;
            record["jevAvailable"] = session.Jev.Available;
            phase = "offer";

            // 종류가 지금 후보에 있을 때까지(열차가 서거나 사람이 자리를 잡을 때까지) 기다린다.
            object chosen = null;
            int offeredCount = 0;
            var wait = new Wait();
            yield return Until(() =>
            {
                var list = Origins(director);
                foreach (var item in list) { Offered.Add(KindOf(item)); }
                offeredCount = list.Count;
                chosen = list.FirstOrDefault(t => KindOf(t) == kind);
                return chosen != null;
            }, Value(config, "offerWaitSeconds", 150f), 120, wait, .5f);
            if (chosen == null) { record["result"] = "not_offered"; record["timeout"] = wait.TimedOut; yield break; }
            var type = chosen.GetType();
            record["key"] = (string)type.GetField("Key").GetValue(chosen);
            record["description"] = (string)type.GetField("Description").GetValue(chosen);
            float magnitude = Value(config, "magnitude", .5f);
            record["magnitude"] = magnitude;
            record["offeredCount"] = offeredCount;

            int timelineFrom = session.Log.Timeline.Count;
            float gameStart = Time.time;
            try
            {
                var execute = typeof(IncidentDirector).GetMethod("Execute", Members);
                var arguments = execute.GetParameters().Select(p => p.ParameterType == typeof(float) ? magnitude : p.ParameterType == typeof(int) ? offeredCount : p.ParameterType == typeof(string) ? (object)"force_all" : p.ParameterType == type ? chosen : null).ToArray();
                execute.Invoke(director, arguments);
            }
            catch (TargetInvocationException exception)
            {
                record["result"] = "execute_exception";
                currentErrors.Add(exception.InnerException?.ToString());
                yield break;
            }

            phase = "response";
            yield return Until(() => director.Stage != IncidentDirector.Phase.Calm, 10, 30, wait);
            if (director.Stage == IncidentDirector.Phase.Calm) { record["result"] = "not_started"; record["timeline"] = Timeline(session, timelineFrom); yield break; }
            record["started"] = true;
            record["startedAtGame"] = Math.Round(Time.time - gameStart, 1);
            KnowAll(director);

            var sent = new HashSet<string>();
            // 예산은 종류마다 다르다(불난 뒤 소방대가 조치하고 시설 담당이 따로 걸어오는 두 단계 대응은 한 단계 대응보다 오래 걸린다). 실제 시간 한도는 배속을 따른다.
            float deadlineGame = config["kindBudgets"]?[kind] != null ? (float)config["kindBudgets"][kind] : Value(config, "kindGameSeconds", 600f);
            float deadlineReal = Mathf.Max(Value(config, "kindRealSeconds", 300f), deadlineGame / Value(config, "timescale", 4f) * 2f + 60f);
            record["budgetGameSeconds"] = deadlineGame;
            var refusals = new HashSet<string>();
            while (director.Stage == IncidentDirector.Phase.Incident)
            {
                if (Time.time - gameStart > deadlineGame || EditorApplication.timeSinceStartup - realStart > deadlineReal) break;
                SendNext(session, sent, record);
                foreach (var responder in Object.FindObjectsByType<Responder>(FindObjectsSortMode.None))
                {
                    if (!responder.Lead || !responder.OnScene) continue;
                    if (!director.CanHandOver(responder)) { var refusal = director.HandOverRefusal(responder); if (!string.IsNullOrEmpty(refusal)) refusals.Add(refusal); continue; }
                    // 남은 무전 선택지를 모두 보낸 뒤에 인계한다.
                    while (SendNext(session, sent, record)) { }
                    record["handoverAtGame"] = Math.Round(Time.time - gameStart, 1);
                    record["handoverTo"] = Responder.AgencyName(responder.Agency) + " · " + responder.DisplayName;
                    try { director.HandOver(responder); }
                    catch (Exception exception) { throw new GameFailure("IncidentDirector.HandOver threw: " + exception); }
                    record["handover"] = true;
                    break;
                }
                if ((bool)record["handover"]) break;
                yield return Sleep(1f);
            }
            if ((bool)record["handover"]) yield return Until(() => director.Stage == IncidentDirector.Phase.Ended, 20, 60, wait);
            record["ended"] = director.Stage == IncidentDirector.Phase.Ended;
            record["timeout"] = !(bool)record["ended"];
            record["result"] = (bool)record["ended"] ? ((bool)record["handover"] ? "ended" : "ended_without_our_handover") : "timeout";
            record["gameSeconds"] = Math.Round(Time.time - gameStart, 1);
            record["realSeconds"] = Math.Round(EditorApplication.timeSinceStartup - realStart, 1);
            record["radioUnsentAtEnd"] = new JArray(UnsentLabels(session, sent));
            record["handoverRefusals"] = new JArray(refusals);
            record["state"] = Describe(director);
            record["timeline"] = Timeline(session, timelineFrom);
        }

        /// <summary>The staff member learns of every hazard the transition created (the harness stands in for seeing, hearing or being told).</summary>
        private static void KnowAll(IncidentDirector director)
        {
            var know = typeof(IncidentDirector).GetMethod("Know", Members);
            var all = typeof(IncidentDirector).GetField("all", Members)?.GetValue(director) as IList;
            if (know == null || all == null) { currentErrors.Add("harness: IncidentDirector.Know or the hazard list is missing"); return; }
            foreach (var hazard in all.Cast<Hazard>().ToList()) know.Invoke(director, new object[] { hazard, "force_all" });
        }

        /// <summary>Sends the first radio option of the wheel that was not sent yet; false when there is none.</summary>
        private static bool SendNext(EmergencySession session, HashSet<string> sent, JObject record)
        {
            try
            {
                foreach (var provider in session.RadioProviders.ToArray())
                    foreach (var option in provider())
                    {
                        if (!sent.Add(option.Label)) continue;
                        ((JArray)record["radioSent"]).Add(option.Label);
                        option.Send?.Invoke();
                        return true;
                    }
            }
            catch (Exception exception)
            {
                currentErrors.Add("radio: " + exception);
            }
            return false;
        }

        private static IEnumerable<string> UnsentLabels(EmergencySession session, HashSet<string> sent)
        {
            var labels = new List<string>();
            try
            {
                foreach (var provider in session.RadioProviders.ToArray())
                    foreach (var option in provider())
                        if (!sent.Contains(option.Label)) labels.Add(option.Label);
            }
            catch (Exception) { }
            return labels;
        }

        private static JArray Timeline(EmergencySession session, int from)
        {
            var entries = new List<JObject>();
            for (int i = from; i < session.Log.Timeline.Count; i++)
                entries.Add(new JObject { ["t"] = Math.Round(session.Log.Timeline[i].Seconds, 1), ["text"] = session.Log.Timeline[i].Text });
            if (entries.Count > 26)
            {
                var head = entries.Take(10).ToList();
                head.Add(new JObject { ["text"] = "… " + (entries.Count - 22) + " entries …" });
                head.AddRange(entries.Skip(entries.Count - 12));
                entries = head;
            }
            return new JArray(entries);
        }

        /// <summary>Where the response stands (hazards, agencies called and arrived, responders) for a kind that did not finish.</summary>
        private static JObject Describe(IncidentDirector director)
        {
            var described = new JObject { ["stage"] = director.Stage.ToString(), ["commander"] = director.Commander.ToString(), ["playerKnows"] = director.PlayerKnowsIncident };
            if (typeof(IncidentDirector).GetField("all", Members)?.GetValue(director) is IList hazards)
                described["hazards"] = new JArray(hazards.Cast<Hazard>().Select(h => h.Label + " @ " + h.Where + " · active=" + h.Active + " · command=" + h.Command));
            if (typeof(IncidentDirector).GetField("calledBy", Members)?.GetValue(director) is IDictionary called)
                described["called"] = new JArray(called.Keys.Cast<object>().Select(k => k + " (" + called[k] + ")"));
            if (typeof(IncidentDirector).GetField("arriveAt", Members)?.GetValue(director) is IDictionary arriving)
                described["arriveInGame"] = new JArray(arriving.Keys.Cast<object>().Select(k => k + " " + Math.Round((float)arriving[k] - Time.time, 1) + " s"));
            described["responders"] = new JArray(Object.FindObjectsByType<Responder>(FindObjectsSortMode.None).Select(r => r.DisplayName + " (" + r.Agency + ") lead=" + r.Lead + " onScene=" + r.OnScene));
            return described;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (current == null || type == LogType.Log || type == LogType.Warning) return;
            var first = (stackTrace ?? "").Split('\n').FirstOrDefault(l => l.Length > 0);
            var entry = phase + " · " + type + ": " + condition.Split('\n')[0] + (first != null ? " | " + first.Trim() : "");
            if (currentErrors.Count < 12 && !currentErrors.Any(e => (string)e == entry)) currentErrors.Add(entry);
        }
    }
}
