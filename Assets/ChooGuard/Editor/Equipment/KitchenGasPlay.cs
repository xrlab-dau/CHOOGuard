using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Play-mode proof of the kitchen and gas equipment through the real staff path, driven from the gateway
    /// (<c>edit_eval: KitchenGasPlay.Arm(configPath)</c>, <c>play_eval: KitchenGasPlay.Report()</c>). Every scenario starts a fresh
    /// shift with a fixed seed and JEV off, forces one cause, then does what a staff member does: walks to the piece of equipment,
    /// looks at it (the interaction ray must find it and show its prompt), presses the interaction, lifts the K-class
    /// extinguisher, pulls the pin and sprays, and afterwards sends every radio option and hands the scene over. Each scenario
    /// records named checks (expected / observed), console errors and the timeline it produced; screenshots are taken at the
    /// moments that matter; the scenarios named in the config's <c>finish</c> list go on to the radio and the handover (the others
    /// stop after the staff part: force_all covers the whole response of every kind). Results are written after every scenario.
    /// Config JSON (a file the edit snippet names): <c>out</c> (result JSON path, required), <c>shots</c> (screenshot folder),
    /// <c>seed</c>, <c>timescale</c> (default 4), <c>magnitude</c> (default 0.5), <c>scenarioGameSeconds</c> (default 500),
    /// <c>settle</c> (game seconds a new shift settles, default 9), <c>names</c> (scenario names to run, default all),
    /// <c>finish</c> (scenario names that go on to radio and handover), <c>catalog</c> (true: photograph every kind of equipment first).
    /// </summary>
    [InitializeOnLoad]
    public static class KitchenGasPlay
    {
        private const string ConfigKey = "ChooGuard.KitchenGasPlay.Config";
        private const string JevVariable = "TYPESAFE_API_KEY";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class Wait { public bool TimedOut; }
        private sealed class Scenario
        {
            public string Name, Kind, Summary;
            public float Magnitude;
            public Func<IEnumerator> Staff;
        }

        private static readonly Stack<IEnumerator> Frames = new Stack<IEnumerator>();
        private static readonly JArray Records = new JArray();
        private static JObject config, current;
        private static JArray currentErrors, currentChecks, currentShots;
        private static bool running, envSwitched;
        private static string originalKey, endedBecause, results, phase = "load";
        private static double startedAt;
        private static Scenario[] scenarios;

        static KitchenGasPlay()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static string Arm(string configPath)
        {
            var parsed = JObject.Parse(File.ReadAllText(configPath));
            if (parsed["out"] == null) return "config has no 'out'";
            parsed["armedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SessionState.SetString(ConfigKey, parsed.ToString(Formatting.None));
            return "armed → " + parsed["out"];
        }

        public static string Report()
        {
            if (running) Finish("the job ended before every scenario was run");
            if (results == null || !File.Exists(results)) return "no results";
            var report = JObject.Parse(File.ReadAllText(results));
            var lines = new List<string> { "ended: " + report["endedBecause"], "file: " + results };
            foreach (var record in (JArray)report["scenarios"])
            {
                var checks = (JArray)record["checks"];
                lines.Add(record["name"] + " (" + record["kind"] + "): " + record["result"] + " checks " + checks.Count(c => (bool)c["ok"]) + "/" + checks.Count + " errors=" + ((JArray)record["consoleErrors"]).Count);
                foreach (var check in checks.Where(c => !(bool)c["ok"])) lines.Add("   FAIL " + check["name"] + " · " + check["detail"]);
            }
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
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)armed["armedAt"] > 900) { SessionState.EraseString(ConfigKey); return; }
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
            Frames.Clear();
            endedBecause = null;
            startedAt = EditorApplication.timeSinceStartup;
            scenarios = Scenarios();
            if (armed["names"] is JArray names && names.Count > 0)
                scenarios = names.Select(n => scenarios.Single(s => s.Name == (string)n)).ToArray();
            running = true;
            Frames.Push(Run());
            Log("run started");
        }

        private static void Step()
        {
            while (running && Frames.Count > 0)
            {
                var top = Frames.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception exception)
                {
                    if (current == null || Frames.Count == 1) { Finish("harness exception: " + exception); return; }
                    current["result"] = "exception";
                    currentErrors.Add(exception.ToString());
                    while (Frames.Count > 1) Frames.Pop();
                    return;
                }
                if (!more) { Frames.Pop(); continue; }
                if (top.Current is IEnumerator inner) { Frames.Push(inner); continue; }
                return;
            }
            if (running) Finish("all scenarios run");
        }

        private static void Finish(string because)
        {
            if (!running) return;
            running = false;
            endedBecause = because;
            Frames.Clear();
            if (current != null) { if ((string)current["result"] == "running") current["result"] = "aborted"; Close(); }
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
                ["endedBecause"] = endedBecause ?? "running",
                ["seed"] = config["seed"],
                ["timescale"] = Value("timescale", 4f),
                ["jev"] = "off (TYPESAFE_API_KEY=off)",
                ["editorSeconds"] = Math.Round(EditorApplication.timeSinceStartup - startedAt),
                ["scenarios"] = Records,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(results)));
            File.WriteAllText(results, report.ToString(Formatting.Indented));
        }

        private static void Log(string line) => File.AppendAllText(results + ".progress.log", DateTime.Now.ToString("HH:mm:ss") + " " + line + "\n");
        private static float Value(string name, float fallback) => config[name] != null ? (float)config[name] : fallback;

        private static void KeepRunning()
        {
            var session = EmergencySession.Current;
            if (!Ready(session) || session.Incidents.Stage == IncidentDirector.Phase.Ended) return;
            if (!session.Player.ExternalInputMode) session.Player.SetExternalInputMode(true);
            if (session.Player.IsPaused) session.Player.Resume(false);
            float scale = Value("timescale", 4f);
            if (Time.timeScale != scale) Time.timeScale = scale;
        }

        private static bool Ready(EmergencySession session) =>
            session != null && session.Player != null && session.World != null && session.Crowd != null && session.Incidents != null && session.Log != null && session.Jev != null &&
            !SceneFlow.Loading && session.Crowd.People.Count > 0;

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
            originalKey = Environment.GetEnvironmentVariable(JevVariable);
            Environment.SetEnvironmentVariable(JevVariable, "off");
            envSwitched = true;
            yield return StartShift(wait);
            if (wait.TimedOut) { Finish("the first fresh shift did not become ready"); yield break; }
            bool fresh = true;
            if (config["catalog"] != null && (bool)config["catalog"])
            {
                yield return Tour();
                fresh = false;
            }
            foreach (var scenario in scenarios)
            {
                current = new JObject
                {
                    ["name"] = scenario.Name, ["kind"] = scenario.Kind, ["summary"] = scenario.Summary, ["result"] = "running",
                    ["checks"] = new JArray(), ["shots"] = new JArray(), ["consoleErrors"] = new JArray(), ["radioSent"] = new JArray(),
                };
                currentErrors = (JArray)current["consoleErrors"];
                currentChecks = (JArray)current["checks"];
                currentShots = (JArray)current["shots"];
                if (!fresh) yield return StartShift(wait);
                fresh = false;
                if (wait.TimedOut) { current["result"] = "shift_not_ready"; Close(); continue; }
                yield return RunScenario(scenario);
                Close();
            }
        }

        private static void Close()
        {
            Records.Add(current);
            Log(current["name"] + " → " + current["result"] + " (checks " + ((JArray)current["checks"]).Count(c => (bool)c["ok"]) + "/" + ((JArray)current["checks"]).Count + ", errors=" + ((JArray)current["consoleErrors"]).Count + ")");
            current = null;
            currentErrors = currentChecks = currentShots = null;
            Save();
        }

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
            yield return Sleep(Value("settle", 9f));
        }

        // ── 설비 도감 사진 ──────────────────────────────────────────────────

        /// <summary>One kind of kitchen or gas equipment, and the shop whose piece of it is photographed.</summary>
        private readonly struct Device
        {
            public readonly string Key, Kind, Shop, Role;
            /// <summary>False for a piece the concourse cannot see by design: it hangs under the ceiling just behind the fascia of the shop front.</summary>
            public readonly bool SeenFromConcourse;
            public Device(string key, string kind, string shop, string role = "", bool seenFromConcourse = true) { Key = key; Kind = kind; Shop = shop; Role = role; SeenFromConcourse = seenFromConcourse; }
        }

        private static readonly Device[] Catalog =
        {
            new Device("fryer", KitchenAppliancePoint.FryerKind, "반월당닭강정"),
            new Device("range", KitchenAppliancePoint.RangeKind, "청도할매김밥"),
            new Device("oven", KitchenAppliancePoint.OvenKind, "비엔씨제과"),
            new Device("hood", "exhaust_hood", "청도할매김밥"),
            new Device("auto_extinguisher", AutoExtinguisherPoint.Kind, "청도할매김밥"),
            new Device("gas_meter", "gas_meter", "청도할매김밥"),
            new Device("intermediate_valve", GasValvePoint.Kind, "청도할매김밥", "intermediate"),
            new Device("main_valve", GasValvePoint.Kind, "청도할매김밥", "main"),
            new Device("gas_hose", "gas_hose", "청도할매김밥"),
            new Device("fuse_cock", "fuse_cock", "청도할매김밥"),
            new Device("gas_pipe", "gas_pipe", "청도할매김밥"),
            new Device("gas_alarm", GasAlarmPoint.Kind, "청도할매김밥", seenFromConcourse: false),
            new Device("k_extinguisher", KitchenExtinguisherPoint.Kind, "청도할매김밥"),
            new Device("counter", "kitchen_counter", "청도할매김밥"),
            new Device("table", "kitchen_table", "청도할매김밥"),
        };

        private static Bounds BoundsOf(Component root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var box = renderers[0].bounds;
            foreach (var renderer in renderers) box.Encapsulate(renderer.bounds);
            return box;
        }

        /// <summary>Distance from <paramref name="from"/> along <paramref name="direction"/> to the first solid that is not part of <paramref name="piece"/> (or <paramref name="max"/>).</summary>
        private static float FreeAhead(Vector3 from, Vector3 direction, float max, Component piece)
        {
            float free = max;
            foreach (var hit in Physics.RaycastAll(from, direction, max, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(piece.transform) && hit.distance < free) free = hit.distance;
            return free;
        }

        /// <summary>
        /// The line of sight from <paramref name="eye"/> to <paramref name="target"/> is open: the shop's glass front (the twin's
        /// "Kit_Colliders", which blocks bodies but not eyes) and people do not count, walls, columns and furniture do.
        /// </summary>
        private static bool Sees(Vector3 eye, Bounds target, Component piece) =>
            Sees(eye, target.center, piece) || Sees(eye, target.center + Vector3.up * target.extents.y * .8f, piece) || Sees(eye, target.center - Vector3.up * target.extents.y * .8f, piece);

        private static bool Sees(Vector3 eye, Vector3 target, Component piece)
        {
            var toward = target - eye;
            float distance = toward.magnitude;
            foreach (var hit in Physics.RaycastAll(eye, toward / distance, distance - .05f, ~0, QueryTriggerInteraction.Ignore))
            {
                var collider = hit.collider;
                if (collider.transform.IsChildOf(piece.transform) || collider.name == "Kit_Colliders" || collider.GetComponentInParent<Passenger>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// One photograph of every kind of kitchen and gas equipment from inside its shop (the staff member stands in the kitchen in
        /// front of it, no closer to the opposite wall than fits) and from the concourse (outside the open front of the shop, on the
        /// far side of the room from the wall the piece hangs on, so the view runs diagonally through the glass; the first spot with
        /// an open line of sight is used, and the camera narrows its field of view so a hose or a leak alarm is legible at that
        /// distance), plus a wide view from the concourse per shop. A piece no spot on the concourse sees (the leak alarm under the
        /// ceiling behind the lintel of the glass front) is listed under <c>hiddenFromConcourse</c>. The shop's open-front direction
        /// is the <c>front</c> data of its K-class extinguisher (from the layout).
        /// </summary>
        private static IEnumerator Tour()
        {
            current = new JObject
            {
                ["name"] = "catalog", ["kind"] = "-", ["summary"] = "Every kind of kitchen and gas equipment photographed from inside its shop and from the concourse.", ["result"] = "running",
                ["checks"] = new JArray(), ["shots"] = new JArray(), ["consoleErrors"] = new JArray(), ["radioSent"] = new JArray(),
            };
            currentErrors = (JArray)current["consoleErrors"];
            currentChecks = (JArray)current["checks"];
            currentShots = (JArray)current["shots"];
            var hidden = new JArray();
            current["hiddenFromConcourse"] = hidden;
            phase = "catalog";
            var camera = Session.Player.PlayerCamera;
            float fov = camera.fieldOfView;
            var wide = new HashSet<string>();
            foreach (var device in Catalog)
            {
                var shop = Session.World.Points.Of(PointKind.Shop).FirstOrDefault(p => p.Label == device.Shop);
                var piece = shop == null ? null : EquipmentRegistry.OfKind(device.Kind).FirstOrDefault(e => e.Text("shop") == shop.Id && (device.Role.Length == 0 || e.Text("role") == device.Role));
                var anchor = shop == null ? null : EquipmentRegistry.OfKind(KitchenExtinguisherPoint.Kind).FirstOrDefault(e => e.Text("shop") == shop.Id);
                Check(device.Key + " stands in " + device.Shop, piece != null && anchor != null);
                if (piece == null || anchor == null) continue;
                var box = BoundsOf(piece);
                var inward = new Vector3(anchor.transform.forward.x, 0, anchor.transform.forward.z).normalized;
                var frontData = anchor.Text("front").Split(',');
                var front = new Vector3(float.Parse(frontData[0], CultureInfo.InvariantCulture), 0, float.Parse(frontData[1], CultureInfo.InvariantCulture)).normalized;
                Check(device.Key + ": the shop front direction is across the wall the kitchen stands on", Mathf.Abs(Vector3.Dot(front, inward)) < .05f, "front " + front + " inward " + inward);

                // 안에서: 설비 앞에 서서 가까이(맞은편 벽에 파묻히지 않게).
                float close = Mathf.Clamp(box.size.magnitude * 1.6f, 1.4f, 3.2f);
                close = Mathf.Min(close, Mathf.Max(1.1f, FreeAhead(box.center, inward, close + .6f, piece) - .5f));
                yield return Stand(box.center, inward, close);
                yield return Sleep(1.2f);
                yield return Shot("catalog_" + device.Key + "_inside");

                // 통로에서: 유리 앞면까지의 거리는 방 안 높은 곳에서 앞쪽으로 쏘아 잰다.
                var origin = box.center + inward * 1.4f;
                origin.y = shop.Position.y + 2.4f;
                float frontAt = Physics.Raycast(origin, front, out var frontHit, 16f, ~0, QueryTriggerInteraction.Ignore) ? frontHit.distance : 7f;
                Vector3? spot = null;
                // 지나가는 사람이 가릴 수 있어 몇 초 간격으로 세 번까지 다시 찾는다.
                for (int attempt = 0; attempt < 3 && spot == null && device.SeenFromConcourse; attempt++)
                {
                    if (attempt > 0) yield return Sleep(4f);
                    foreach (float beyond in new[] { 2.6f, 3.6f, 4.6f, 5.6f })
                    {
                        foreach (float lateral in new[] { 2.4f, 1.6f, 3.2f, .9f, 4.0f, .4f })
                        {
                            var at = new Vector3(box.center.x, 0, box.center.z) + inward * lateral + front * (frontAt + beyond);
                            var feet = Physics.Raycast(new Vector3(at.x, shop.Position.y + 1.5f, at.z), Vector3.down, out var floor, 4f, ~0, QueryTriggerInteraction.Ignore) ? floor.point : new Vector3(at.x, shop.Position.y, at.z);
                            if (!Sees(feet + Vector3.up * Session.Player.EyeHeight, box, piece)) continue;
                            spot = feet;
                            break;
                        }
                        if (spot != null) break;
                    }
                }
                if (spot == null) { hidden.Add(device.Key); }
                else
                {
                    var ground = spot.Value;
                    float distance = Vector3.Distance(ground + Vector3.up * Session.Player.EyeHeight, box.center);
                    camera.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(Mathf.Max(box.size.x, box.size.y, box.size.z) * 1.6f / 2f / distance) * Mathf.Rad2Deg, 8f, camera.fieldOfView);
                    yield return LookFrom(ground, box.center);
                    yield return Sleep(1.2f);
                    yield return Shot("catalog_" + device.Key + "_concourse");
                    camera.fieldOfView = fov;
                    if (wide.Add(device.Shop))
                    {
                        var line = EquipmentRegistry.All.Where(e => e.Text("shop") == shop.Id && (e.Kind == KitchenAppliancePoint.FryerKind || e.Kind == KitchenAppliancePoint.RangeKind || e.Kind == KitchenAppliancePoint.OvenKind)).ToList();
                        var centre = line.Aggregate(Vector3.zero, (sum, e) => sum + e.transform.position) / line.Count + Vector3.up;
                        yield return LookFrom(ground, centre);
                        yield return Sleep(1.2f);
                        yield return Shot("catalog_shop_" + device.Shop + "_concourse_wide");
                    }
                }
            }
            camera.fieldOfView = fov;
            current["result"] = "done";
            Close();
        }

        // ── 한 시나리오 ─────────────────────────────────────────────────────

        private static EmergencySession Session => EmergencySession.Current;

        private static IEnumerator RunScenario(Scenario scenario)
        {
            var session = Session;
            var director = session.Incidents;
            var wait = new Wait();
            phase = "offer";
            object chosen = null;
            var origins = typeof(IncidentDirector).GetMethod("Origins", Members);
            yield return Until(() =>
            {
                chosen = ((IEnumerable)origins.Invoke(director, null)).Cast<object>().FirstOrDefault(t => (string)t.GetType().GetField("Kind").GetValue(t) == scenario.Kind);
                return chosen != null;
            }, 150, 120, wait, .5f);
            if (chosen == null) { current["result"] = "not_offered"; yield break; }
            current["key"] = (string)chosen.GetType().GetField("Key").GetValue(chosen);
            current["description"] = (string)chosen.GetType().GetField("Description").GetValue(chosen);
            float magnitude = scenario.Magnitude > 0 ? scenario.Magnitude : Value("magnitude", .5f);
            current["magnitude"] = magnitude;
            int from = session.Log.Timeline.Count;
            float gameStart = Time.time;
            ExecuteTransition(chosen, magnitude);
            yield return Until(() => director.Stage != IncidentDirector.Phase.Calm, 10, 30, wait);
            if (director.Stage == IncidentDirector.Phase.Calm) { current["result"] = "not_started"; yield break; }
            phase = "staff";
            var know = typeof(IncidentDirector).GetMethod("Know", Members);
            foreach (var hazard in ((IList)typeof(IncidentDirector).GetField("all", Members).GetValue(director)).Cast<Hazard>().ToList()) know.Invoke(director, new object[] { hazard, "kitchen_play" });
            yield return scenario.Staff();

            if (config["finish"] is JArray finishing && finishing.All(n => (string)n != scenario.Name))
            {
                current["result"] = "staff_part_done";
                current["gameSeconds"] = Math.Round(Time.time - gameStart, 1);
                current["timeline"] = Timeline(session, from);
                yield break;
            }
            phase = "response";
            var sent = new HashSet<string>();
            float deadlineGame = Value("scenarioGameSeconds", 500f);
            float realStart = (float)EditorApplication.timeSinceStartup;
            bool handover = false;
            while (director.Stage == IncidentDirector.Phase.Incident && Time.time - gameStart < deadlineGame && EditorApplication.timeSinceStartup - realStart < 360)
            {
                SendNext(session, sent);
                foreach (var responder in Object.FindObjectsByType<Responder>(FindObjectsSortMode.None))
                {
                    if (!responder.Lead || !responder.OnScene || !director.CanHandOver(responder)) continue;
                    while (SendNext(session, sent)) { }
                    current["handoverTo"] = Responder.AgencyName(responder.Agency) + " · " + responder.DisplayName;
                    current["handoverAtGame"] = Math.Round(Time.time - gameStart, 1);
                    director.HandOver(responder);
                    handover = true;
                    break;
                }
                if (handover) break;
                yield return Sleep(1f);
            }
            if (handover) yield return Until(() => director.Stage == IncidentDirector.Phase.Ended, 20, 60, wait);
            bool ended = director.Stage == IncidentDirector.Phase.Ended;
            current["handover"] = handover;
            current["ended"] = ended;
            current["result"] = ended && handover ? "ended" : ended ? "ended_without_our_handover" : "timeout";
            current["gameSeconds"] = Math.Round(Time.time - gameStart, 1);
            current["timeline"] = Timeline(session, from);
        }

        /// <summary>Runs a transition the way the director does when it draws it (the harness stands in for JEV and the dice).</summary>
        private static void ExecuteTransition(object transition, float magnitude)
        {
            var execute = typeof(IncidentDirector).GetMethod("Execute", Members);
            execute.Invoke(Session.Incidents, execute.GetParameters().Select(p => p.ParameterType == typeof(float) ? magnitude : p.ParameterType == typeof(int) ? 1 : p.ParameterType == typeof(string) ? (object)"kitchen_play" : transition).ToArray());
        }

        private static bool SendNext(EmergencySession session, HashSet<string> sent)
        {
            try
            {
                foreach (var provider in session.RadioProviders.ToArray())
                    foreach (var option in provider())
                    {
                        if (!sent.Add(option.Label)) continue;
                        ((JArray)current["radioSent"]).Add(option.Label);
                        option.Send?.Invoke();
                        return true;
                    }
            }
            catch (Exception exception) { currentErrors.Add("radio: " + exception); }
            return false;
        }

        private static JArray Timeline(EmergencySession session, int from)
        {
            var entries = new List<JObject>();
            for (int i = from; i < session.Log.Timeline.Count; i++)
                entries.Add(new JObject { ["t"] = Math.Round(session.Log.Timeline[i].Seconds, 1), ["text"] = session.Log.Timeline[i].Text });
            return new JArray(entries);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (current == null || type == LogType.Log || type == LogType.Warning) return;
            var first = (stackTrace ?? "").Split('\n').FirstOrDefault(l => l.Length > 0);
            var entry = phase + " · " + type + ": " + condition.Split('\n')[0] + (first != null ? " | " + first.Trim() : "");
            if (currentErrors.Count < 12 && !currentErrors.Any(e => (string)e == entry)) currentErrors.Add(entry);
        }

        private static void Check(string name, bool ok, string detail = "") =>
            currentChecks.Add(new JObject { ["name"] = name, ["ok"] = ok, ["detail"] = detail });

        // ── 직원의 동작 ─────────────────────────────────────────────────────

        private static IEnumerable<GasLeakHazard> Leaks() => HazardRegistry.Active.OfType<GasLeakHazard>().ToList();
        private static IEnumerable<FireHazard> Fires() => HazardRegistry.Active.OfType<FireHazard>().ToList();

        private static T Nearest<T>(string kind, Vector3 to) where T : Component =>
            EquipmentRegistry.OfKind(kind).OrderBy(e => (e.transform.position - to).sqrMagnitude).Select(e => e.GetComponent<T>()).FirstOrDefault(c => c != null);

        /// <summary>The staff member stands <paramref name="distance"/> metres from <paramref name="focus"/> on the side of <paramref name="toward"/> and looks at it.</summary>
        private static IEnumerator Stand(Vector3 focus, Vector3 toward, float distance)
        {
            var player = Session.Player;
            var flat = new Vector3(toward.x, 0, toward.z).normalized;
            var position = focus + flat * distance;
            // 바닥은 걸을 수 있는 면(내비메시)에서: 아래로 쏘면 조리대·레인지 위에 서게 된다.
            position.y = NavMesh.SamplePosition(new Vector3(position.x, focus.y, position.z), out var walk, 4f, NavMesh.AllAreas)
                ? walk.position.y
                : Physics.Raycast(new Vector3(position.x, focus.y + .3f, position.z), Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : focus.y - 1f;
            yield return LookFrom(position, focus);
        }

        /// <summary>The staff member stands with the feet at <paramref name="feet"/> and looks at <paramref name="focus"/>.</summary>
        private static IEnumerator LookFrom(Vector3 feet, Vector3 focus)
        {
            var player = Session.Player;
            var to = focus - (feet + Vector3.up * player.EyeHeight);
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            player.RestorePhysicalPose(feet, yaw, pitch);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            player.RefreshInteraction();
        }

        /// <summary>
        /// Presses the interaction on what the staff member is looking at once its prompt shows (a passer-by in the way moves on, so
        /// the prompt is awaited for a few seconds like a person would); the expected prompt is checked, then the effect.
        /// </summary>
        private static IEnumerator Press(string what, string expectedPrompt, Component piece, Func<bool> effect = null, string effectName = null)
        {
            var player = Session.Player;
            float until = Time.time + 4f;
            while (true)
            {
                player.RefreshInteraction();
                if (player.CurrentPrompt == expectedPrompt || Time.time > until) break;
                yield return Sleep(.5f);
            }
            // 불꽃이 설비 앞에 서 있으면 그 충돌체가 시선을 막는다: 사람처럼 십자선을 설비의 다른 곳으로 옮겨 본다.
            if (player.CurrentPrompt != expectedPrompt) yield return AimAt(piece, expectedPrompt);
            string prompt = player.CurrentPrompt;
            var eye = player.PlayerCamera.transform;
            var first = Physics.RaycastAll(eye.position, eye.forward, 3f, ~0, QueryTriggerInteraction.Ignore).Where(h => !h.collider.transform.IsChildOf(player.transform)).OrderBy(h => h.distance).FirstOrDefault();
            string ray = first.collider != null ? first.collider.name + " under " + first.collider.transform.root.name + " at " + first.distance.ToString("0.00") + " m" : "nothing within 3 m";
            Check(what + ": the ray finds it and shows the prompt", prompt == expectedPrompt, "expected '" + expectedPrompt + "', saw '" + prompt + "'" + (prompt == expectedPrompt ? "" : "; the ray meets " + ray));
            bool did = player.TryInteract();
            Check(what + ": the interaction is performed", did, player.LastFeedback);
            if (effect != null) Check(what + ": " + effectName, effect());
        }

        /// <summary>Moves the crosshair over the piece (middle, low, high, to the sides) until its prompt shows; the feet stay where they are.</summary>
        private static IEnumerator AimAt(Component piece, string expectedPrompt)
        {
            var player = Session.Player;
            var feet = player.transform.position;
            var box = BoundsOf(piece);
            foreach (float up in new[] { .5f, .15f, .85f, .3f, .7f, .05f, .95f })
                foreach (float side in new[] { 0f, .3f, -.3f, .15f, -.15f, .45f, -.45f })
                {
                    var target = new Vector3(box.center.x, box.min.y + box.size.y * up, box.center.z) + piece.transform.right * side;
                    yield return LookFrom(feet, target);
                    if (player.CurrentPrompt == expectedPrompt) yield break;
                }
        }

        private static IEnumerator Shot(string name)
        {
            if (config["shots"] == null) yield break;
            string path = Path.Combine((string)config["shots"], name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            yield return null;
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            currentShots.Add(path);
        }

        private static void Trigger(bool down)
        {
            var field = typeof(EmergencySession).GetField(down ? "Primary" : "PrimaryReleased", Members);
            (field.GetValue(Session) as Action)?.Invoke();
        }

        /// <summary>Takes the extinguisher off its bracket by the real interaction (the staff member stands in front of it).</summary>
        private static IEnumerator TakeKitchenExtinguisher(FireHazard fire)
        {
            var point = Nearest<KitchenExtinguisherPoint>(KitchenExtinguisherPoint.Kind, fire.Position);
            Check("a K-class extinguisher hangs in the shop's kitchen", point != null);
            if (point == null) yield break;
            yield return Stand(point.transform.position + Vector3.up * .3f, point.transform.forward, 1.1f);
            yield return Press("K-class extinguisher", "E · 소화기 들기", point, () => Session.Hands.Held == point.Tool && point.Tool.Type == ExtinguishAgent.WetChemical, "the staff member holds a wet-chemical extinguisher");
        }

        /// <summary>Holds the trigger: first the pin (hold), then the spray, aimed at the base of the fire, until it is out or the agent is spent.</summary>
        private static IEnumerator Spray(FireHazard fire, float atMost, Vector3 from)
        {
            yield return Stand(fire.Position + Vector3.up * .3f, from, 2.3f);
            var tool = Session.Hands.Held;
            if (tool == null) yield break;
            Trigger(true);
            yield return Until(() => tool.PinPulled, 8, 20, new Wait());
            Trigger(false);
            Check("the pin comes out after the hold", tool.PinPulled);
            Trigger(true);
            float end = Time.time + atMost;
            float agent = tool.Agent;
            while (Time.time < end && !fire.Extinguished && tool.Agent > 0) yield return null;
            Trigger(false);
            current["agentUsed"] = Math.Round(agent - tool.Agent, 2);
        }

        private static FireHazard TheFire()
        {
            var fire = Fires().FirstOrDefault();
            Check("the fire exists at the appliance", fire != null);
            return fire;
        }

        private static Scenario[] Scenarios() => new[]
        {
            new Scenario
            {
                Name = "valve_stops_hose_leak", Kind = "gas_hose_off",
                Summary = "The hose slips off the range; the staff member finds the intermediate valve, closes it and the leak stops.",
                Staff = ValveStopsLeak,
            },
            new Scenario
            {
                Name = "fryer_power_then_k_extinguisher", Kind = "fryer_oil_fire",
                Summary = "Oil fire in the fryer: switch the fryer off (its feed), take the K-class extinguisher, pull the pin, spray; the oil is cooled and the fire is out.",
                Staff = FryerOffThenK,
            },
            new Scenario
            {
                Name = "powder_on_oil_fire", Kind = "fryer_oil_fire", Magnitude = .9f,
                Summary = "The same fire with a dry-powder ABC extinguisher of the station: the flames drop but the fire never goes out.",
                Staff = FryerOffThenPowder,
            },
            new Scenario
            {
                Name = "range_valve_then_extinguisher", Kind = "kitchen_fire",
                Summary = "Pan fire on the gas range: close the range's intermediate valve (its feed), then put the fire out with the K-class extinguisher.",
                Staff = RangeValveThenK,
            },
            new Scenario
            {
                Name = "oven_off", Kind = "oven_fire",
                Summary = "Fire in the electric oven: switch it off at its switch, then put it out.",
                Staff = OvenOffThenK,
            },
            new Scenario
            {
                Name = "meter_leak_alarm_main_valve", Kind = "gas_meter_leak",
                Summary = "Gas seeps at the meter: the wall alarm sounds, the intermediate valves cannot stop it, the main valve at the meter does.",
                Staff = MeterLeak,
            },
            new Scenario
            {
                Name = "gas_ignites_main_valve_then_k", Kind = "gas_meter_leak", Magnitude = .9f,
                Summary = "A strong leak at the meter ignites: the fire burns on the gas (the K-class extinguisher beats it down to embers only), the main valve cuts the gas, then the extinguisher puts it out.",
                Staff = GasIgnites,
            },
            new Scenario
            {
                Name = "cock_left_open", Kind = "gas_cock_open",
                Summary = "A burner cock was left open: the alarm sounds, the staff member closes the burner cock and the leak ends.",
                Staff = CockLeft,
            },
            new Scenario
            {
                Name = "auto_extinguisher", Kind = "fryer_oil_fire", Magnitude = .9f,
                Summary = "Oil fire left alone: the automatic extinguisher over the fryer discharges by itself.",
                Staff = AutoExtinguisher,
            },
        };

        private static IEnumerator ValveStopsLeak()
        {
            var gas = Leaks().FirstOrDefault();
            Check("a gas leak is a hazard", gas != null);
            if (gas == null) yield break;
            yield return Stand(gas.Position, Vector3.right, 2.2f);
            yield return Shot("valve_stops_hose_leak_1leak");
            var valve = EquipmentRegistry.All.Where(e => gas.StoppedBy.Contains(e.Id)).Select(e => e.GetComponent<GasValvePoint>()).FirstOrDefault(v => v != null && !v.Main);
            Check("the leak names an intermediate valve that stops it", valve != null, string.Join(",", gas.StoppedBy));
            if (valve == null) yield break;
            yield return Stand(valve.transform.position, valve.transform.forward, 1.2f);
            yield return Press("intermediate valve", "E · 가스 중간밸브 잠그기", valve, () => valve.Closed, "the valve is closed");
            yield return Sleep(1.5f);
            Check("closing it ends the leak", !gas.Active);
            yield return Shot("valve_stops_hose_leak_2closed");
        }

        private static IEnumerator SwitchOffFryer(FireHazard fire, KitchenAppliancePoint fryer)
        {
            yield return Stand(fryer.transform.position + Vector3.up * .3f, fryer.transform.forward, 1.3f);
            yield return Press("fryer", "E · 튀김기 전원 끄기", fryer, () => !fryer.On && fire.Feed == null, "the fryer is off and the fire's feed is cut");
        }

        private static IEnumerator FryerOffThenK()
        {
            var fire = TheFire();
            if (fire == null) yield break;
            var fryer = Nearest<KitchenAppliancePoint>(KitchenAppliancePoint.FryerKind, fire.Position);
            Check("the fire is burning oil", fire.Oil);
            yield return Stand(fire.Position + Vector3.up * .5f, fryer.transform.forward, 2.8f);
            yield return Shot("fryer_k_1burning");
            yield return SwitchOffFryer(fire, fryer);
            yield return TakeKitchenExtinguisher(fire);
            yield return Spray(fire, 30f, fryer.transform.forward);
            yield return Shot("fryer_k_2spray");
            Check("the wet chemical cools the oil", fire.Cooled);
            Check("the fire is out", fire.Extinguished, "intensity " + fire.Intensity.ToString("0.00"));
            Check("the extinguisher was not emptied to do it", Session.Hands.Held == null || Session.Hands.Held.Agent > 0, "agent left " + (Session.Hands.Held != null ? Session.Hands.Held.Agent.ToString("0.00") : "-"));
        }

        private static IEnumerator FryerOffThenPowder()
        {
            var fire = TheFire();
            if (fire == null) yield break;
            var fryer = Nearest<KitchenAppliancePoint>(KitchenAppliancePoint.FryerKind, fire.Position);
            yield return SwitchOffFryer(fire, fryer);
            var powder = Object.FindObjectsByType<Extinguisher>(FindObjectsSortMode.None).Where(e => e.Type == ExtinguishAgent.Powder && !e.Held && !e.Defective).OrderBy(e => (e.transform.position - fire.Position).sqrMagnitude).FirstOrDefault();
            Check("the station has a working ABC powder extinguisher", powder != null);
            if (powder == null) yield break;
            Session.Hands.PickUp(powder);
            Check("the staff member holds the powder extinguisher", Session.Hands.Held == powder);
            float before = fire.Intensity;
            yield return Spray(fire, 40f, fryer.transform.forward);
            float empty = fire.Intensity;
            Check("the powder knocks the flames down", empty < before, before.ToString("0.00") + " → " + empty.ToString("0.00"));
            Check("but the oil fire is not out", !fire.Extinguished, "intensity " + empty.ToString("0.00"));
            yield return Sleep(20f);
            Check("and it does not go out by itself", !fire.Extinguished, "intensity 20 s later " + fire.Intensity.ToString("0.00"));
            current["powderIntensity"] = new JArray(Math.Round(before, 2), Math.Round(empty, 2), Math.Round(fire.Intensity, 2));
        }

        private static IEnumerator RangeValveThenK()
        {
            var fire = TheFire();
            if (fire == null) yield break;
            var range = Nearest<KitchenAppliancePoint>(KitchenAppliancePoint.RangeKind, fire.Position);
            var valve = Nearest<GasValvePoint>(GasValvePoint.Kind, range.transform.position);
            valve = EquipmentRegistry.All.Where(e => e.Id == range.ValveId).Select(e => e.GetComponent<GasValvePoint>()).FirstOrDefault() ?? valve;
            yield return Stand(fire.Position + Vector3.up * .4f, range.transform.forward, 2.8f);
            yield return Shot("range_1burning");
            yield return Stand(valve.transform.position, valve.transform.forward, 1.2f);
            yield return Press("range's intermediate valve", "E · 가스 중간밸브 잠그기", valve, () => valve.Closed && fire.Feed == null, "the valve is closed and the fire's gas feed is cut");
            yield return TakeKitchenExtinguisher(fire);
            yield return Spray(fire, 30f, range.transform.forward);
            Check("the fire is out", fire.Extinguished, "intensity " + fire.Intensity.ToString("0.00"));
        }

        private static IEnumerator OvenOffThenK()
        {
            var fire = TheFire();
            if (fire == null) yield break;
            var oven = Nearest<KitchenAppliancePoint>(KitchenAppliancePoint.OvenKind, fire.Position);
            // 불꽃이 문 앞에 서 있으니(충돌체가 시선을 막는다) 오븐 아랫 앞면을 본다.
            yield return Stand(oven.transform.position + Vector3.up * .25f, oven.transform.forward, 1.3f);
            yield return Press("oven", "E · 오븐 전원 끄기", oven, () => !oven.On && fire.Feed == null, "the oven is off and the fire's feed is cut");
            yield return TakeKitchenExtinguisher(fire);
            yield return Spray(fire, 30f, oven.transform.forward);
            Check("the fire is out", fire.Extinguished, "intensity " + fire.Intensity.ToString("0.00"));
        }

        private static IEnumerator MeterLeak()
        {
            var gas = Leaks().FirstOrDefault();
            Check("a gas leak is a hazard", gas != null);
            if (gas == null) yield break;
            var alarm = Nearest<GasAlarmPoint>(GasAlarmPoint.Kind, gas.Position);
            yield return Until(() => alarm.Sounding, 90, 90, new Wait(), .5f);
            Check("the leak alarm of the shop sounds", alarm.Sounding);
            yield return Stand(alarm.transform.position, alarm.transform.forward, 1.6f);
            yield return Shot("meter_1alarm");
            var meter = Nearest<StationEquipment>("gas_meter", gas.Position);
            yield return Stand(meter.transform.position, meter.transform.forward, 1.3f);
            yield return Shot("meter_2meter");
            var main = EquipmentRegistry.All.Where(e => gas.StoppedBy.Contains(e.Id)).Select(e => e.GetComponent<GasValvePoint>()).First(v => v.Main);
            var intermediates = EquipmentRegistry.All.Where(e => e.Kind == GasValvePoint.Kind && e.Text("shop") == meter.Text("shop") && !e.GetComponent<GasValvePoint>().Main).ToList();
            Check("an intermediate valve does not appear among what stops a leak at the meter", intermediates.All(v => !gas.StoppedBy.Contains(v.Id)));
            yield return Stand(main.transform.position, main.transform.forward, 1.2f);
            yield return Press("main valve", "E · 가스 메인밸브 잠그기", main, () => main.Closed, "the main valve is closed");
            yield return Sleep(1.5f);
            Check("closing it ends the leak", !gas.Active);
        }

        private static IEnumerator CockLeft()
        {
            var gas = Leaks().FirstOrDefault();
            Check("a gas leak is a hazard", gas != null);
            if (gas == null) yield break;
            var alarm = Nearest<GasAlarmPoint>(GasAlarmPoint.Kind, gas.Position);
            yield return Until(() => alarm.Sounding, 90, 90, new Wait(), .5f);
            Check("the leak alarm sounds (a cock left open is what it is for)", alarm.Sounding);
            var range = Nearest<KitchenAppliancePoint>(KitchenAppliancePoint.RangeKind, gas.Position);
            yield return Stand(range.transform.position + Vector3.up * .6f, range.transform.forward, 1.3f);
            yield return Press("range", "E · 화구 코크 잠그기(불 끄기)", range, () => !range.On, "the burner cock is closed");
            yield return Sleep(1.5f);
            Check("closing the cock ends the leak", !gas.Active);
        }

        private static IEnumerator GasIgnites()
        {
            var gas = Leaks().FirstOrDefault();
            Check("a strong leak is a gas hazard of level 3 or more", gas != null && gas.Level >= 3, gas == null ? "no leak" : "level " + gas.Level);
            if (gas == null) yield break;
            var developments = ((IEnumerable)typeof(IncidentDirector).GetMethod("Developments", Members).Invoke(Session.Incidents, null)).Cast<object>();
            var ignition = developments.FirstOrDefault(t => (string)t.GetType().GetField("Kind").GetValue(t) == "gas_ignites");
            Check("the director offers the gas as something that can ignite", ignition != null);
            if (ignition == null) yield break;
            ExecuteTransition(ignition, .6f);
            yield return Sleep(1f);
            var fire = Fires().FirstOrDefault();
            Check("the leak has become a fire and is no longer a gas hazard", fire != null && !gas.Active);
            if (fire == null) yield break;
            Check("the fire burns on the gas feed", fire.Feed != null, fire.Feed);
            var kext = Nearest<KitchenExtinguisherPoint>(KitchenExtinguisherPoint.Kind, fire.Position);
            var inward = kext.transform.forward;
            yield return TakeKitchenExtinguisher(fire);
            yield return Spray(fire, 5f, inward);
            Check("while the gas feeds it the fire is beaten down to embers but is not out", !fire.Extinguished && fire.Intensity <= FireHazard.LiveEmbers + .02f, "intensity " + fire.Intensity.ToString("0.00"));
            var main = EquipmentRegistry.All.Where(e => gas.StoppedBy.Contains(e.Id)).Select(e => e.GetComponent<GasValvePoint>()).First(v => v.Main);
            yield return Stand(main.transform.position, main.transform.forward, 1.3f);
            yield return Press("main valve", "E · 가스 메인밸브 잠그기", main, () => main.Closed && fire.Feed == null, "the main valve is closed and the gas feed is cut");
            yield return Spray(fire, 12f, inward);
            Check("with the gas cut the extinguisher puts the fire out", fire.Extinguished, "intensity " + fire.Intensity.ToString("0.00"));
        }

        private static IEnumerator AutoExtinguisher()
        {
            var fire = TheFire();
            if (fire == null) yield break;
            var fryer = Nearest<KitchenAppliancePoint>(KitchenAppliancePoint.FryerKind, fire.Position);
            var unit = Nearest<AutoExtinguisherPoint>(AutoExtinguisherPoint.Kind, fire.Position);
            yield return Stand(fire.Position + Vector3.up * .7f, fryer.transform.forward, 3f);
            yield return Shot("auto_1before");
            yield return Until(() => unit.Discharged, 240, 120, new Wait(), .5f);
            Check("the automatic extinguisher over the fryer discharged by itself", unit.Discharged);
            float atDischarge = fire.Intensity;
            yield return Sleep(2f);
            yield return Shot("auto_2discharging");
            yield return Sleep(10f);
            Check("its agent knocks the fire down", fire.Intensity < atDischarge || fire.Extinguished, atDischarge.ToString("0.00") + " → " + fire.Intensity.ToString("0.00"));
            current["autoIntensity"] = new JArray(Math.Round(atDischarge, 2), Math.Round(fire.Intensity, 2));
        }
    }
}
