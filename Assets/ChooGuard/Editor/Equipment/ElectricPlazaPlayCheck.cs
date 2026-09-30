using System;
using System.Collections;
using System.Collections.Generic;
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
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Plays the staff member's electrical work in a running shift, through the same interaction path a player uses (aim,
    /// then the interact input of the responder's verification adapter), and records what happened. Driven from the play-mode
    /// gateway: an edit-mode eval calls <see cref="Arm"/> with a JSON config, the shift plays, a play-mode eval calls
    /// <see cref="Report"/>. JEV is off for every shift (TYPESAFE_API_KEY=off) so only what the scenarios force happens.
    /// Scenarios: board (open a board, read the breaker label, switch a machine's breaker off and on, walk away and the door
    /// closes), plug (pull a vending machine's plug), kiosk (throw a kiosk's power switch), fire (a vending machine burns:
    /// live fire cannot be put out, water shocks, the plug comes out, the fire goes out and the machine stays dead), boardfire
    /// (a board burns: too hot to open, the main breaker, the board burns out), developments (shock, spread, protection trip, wet
    /// short, forced one by one), bin and binbattery (a real litter bin / recycling station burns and spreads), electrician (nobody
    /// cuts it: report, the electrician cuts and inspects, the fire brigade puts it out, handover), perf (what the causes cost).
    /// Config: out (report path), seed, timescale, shots (folder for screenshots), scenarios (list, default all).
    /// </summary>
    [InitializeOnLoad]
    public static class ElectricPlazaPlayCheck
    {
        private const string ConfigKey = "ChooGuard.ElectricPlazaCheck.Config";
        private const string JevVariable = "TYPESAFE_API_KEY";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class Wait { public bool TimedOut; }

        private static readonly Stack<IEnumerator> Frames = new Stack<IEnumerator>();
        private static readonly JArray Checks = new JArray();
        private static readonly JArray Errors = new JArray();
        private static JObject config;
        private static bool running, envSwitched;
        private static string originalKey, endedBecause, results, scenario = "load";

        static ElectricPlazaPlayCheck()
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
            foreach (var check in (JArray)report["checks"]) lines.Add(((bool)check["ok"] ? "PASS " : "FAIL ") + check["scenario"] + " · " + check["name"] + (((string)check["detail"]).Length > 0 ? " — " + check["detail"] : ""));
            lines.Add("console errors: " + ((JArray)report["errors"]).Count);
            return string.Join("\n", lines);
        }

        // ── 구동 ──

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
                config = armed;
                results = (string)armed["out"];
                Checks.Clear();
                Errors.Clear();
                Frames.Clear();
                endedBecause = null;
                running = true;
                Frames.Push(Run());
                Log("armed run started");
            }
            KeepRunning();
            Step();
        }

        private static void Step()
        {
            while (running && Frames.Count > 0)
            {
                var top = Frames.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception exception) { Errors.Add("harness: " + exception); Finish("harness exception: " + exception.Message); return; }
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
            Save();
            if (envSwitched) Environment.SetEnvironmentVariable(JevVariable, originalKey);
            envSwitched = false;
            Time.timeScale = 1f;
            SessionState.EraseString(ConfigKey);
            Log("run ended · " + because);
        }

        private static void Save()
        {
            var report = new JObject { ["endedBecause"] = endedBecause ?? "running", ["seed"] = config["seed"], ["checks"] = Checks, ["errors"] = Errors };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(results)));
            File.WriteAllText(results, report.ToString(Formatting.Indented));
        }

        private static void Log(string line) => File.AppendAllText(results + ".progress.log", DateTime.Now.ToString("HH:mm:ss") + " " + line + "\n");

        private static void Check(string name, bool ok, string detail = "")
        {
            detail = detail ?? "";
            Checks.Add(new JObject { ["scenario"] = scenario, ["name"] = name, ["ok"] = ok, ["detail"] = detail });
            Log((ok ? "PASS " : "FAIL ") + scenario + " · " + name + (detail.Length > 0 ? " — " + detail : ""));
            Save();
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!running || type == LogType.Log || type == LogType.Warning) return;
            var entry = scenario + " · " + type + ": " + condition.Split('\n')[0];
            if (Errors.Count < 20 && !Errors.Any(e => (string)e == entry)) Errors.Add(entry);
        }

        private static float Value(string name, float fallback) => config[name] != null ? (float)config[name] : fallback;

        private static void KeepRunning()
        {
            var session = EmergencySession.Current;
            if (!Ready(session) || session.Incidents.Stage == IncidentDirector.Phase.Ended) return;
            if (!session.Player.ExternalInputMode) session.Player.SetExternalInputMode(true);
            if (session.Player.IsPaused) session.Player.Resume(false);
            float scale = Value("timescale", 2f);
            if (Time.timeScale != scale) Time.timeScale = scale;
        }

        private static bool Ready(EmergencySession session) =>
            session != null && session.Player != null && session.World != null && session.Crowd != null && session.Incidents != null && session.Log != null && session.Jev != null &&
            !SceneFlow.Loading && session.Crowd.People.Count > 0 && EquipmentRegistry.All.Count > 0;

        private static IEnumerator Until(Func<bool> condition, float gameSeconds, float realSeconds, Wait outcome)
        {
            float game = Time.time;
            double real = EditorApplication.timeSinceStartup;
            while (!condition())
            {
                if ((gameSeconds > 0 && Time.time - game > gameSeconds) || (realSeconds > 0 && EditorApplication.timeSinceStartup - real > realSeconds)) { outcome.TimedOut = true; yield break; }
                yield return null;
            }
        }

        private static IEnumerator Sleep(float gameSeconds)
        {
            float end = Time.time + gameSeconds;
            while (Time.time < end) yield return null;
        }

        // ── 구조 ──

        private static IEnumerator Run()
        {
            var wait = new Wait();
            yield return Until(() => Ready(EmergencySession.Current), 0, 300, wait);
            if (wait.TimedOut) { Finish("no ready shift in play mode after 300 s"); yield break; }
            originalKey = Environment.GetEnvironmentVariable(JevVariable);
            Environment.SetEnvironmentVariable(JevVariable, "off");
            envSwitched = true;
            var names = config["scenarios"] is JArray list ? list.Select(s => (string)s).ToList() : new List<string> { "board", "plug", "kiosk", "fire", "boardfire", "developments", "bin", "binbattery", "stale", "electrician", "perf" };
            foreach (var name in names)
            {
                scenario = name;
                yield return StartShift(wait);
                if (wait.TimedOut) { Check("fresh shift ready", false, "timed out"); continue; }
                switch (name)
                {
                    case "board": yield return Board(); break;
                    case "plug": yield return Plug(); break;
                    case "kiosk": yield return Kiosk(); break;
                    case "fire": yield return Fire(); break;
                    case "boardfire": yield return BoardFire(); break;
                    case "electrician": yield return Electrician(); break;
                    case "developments": yield return Developments(); break;
                    case "bin": yield return Bin(); break;
                    case "binbattery": yield return BinBattery(); break;
                    case "stale": yield return Stale(); break;
                    case "perf": yield return Perf(); break;
                    default: Check("known scenario", false, name); break;
                }
            }
        }

        private static IEnumerator StartShift(Wait outcome)
        {
            outcome.TimedOut = false;
            var old = EmergencySession.Current;
            var font = old != null ? old.KoreanFont : null;
            if (font == null) { outcome.TimedOut = true; yield break; }
            EmergencySession.NextSeed = (int)config["seed"];
            SceneFlow.StartShift(font, null);
            yield return Until(() => !SceneFlow.Loading && EmergencySession.Current != old && Ready(EmergencySession.Current), 0, 240, outcome);
            if (outcome.TimedOut) yield break;
            yield return Sleep(2f);
        }

        // ── 조작 ──

        private static EmergencySession Session => EmergencySession.Current;

        /// <summary>Stands the staff member <paramref name="from"/> (eye position) looking at <paramref name="target"/>.</summary>
        private static void LookAt(Vector3 from, Vector3 target)
        {
            var player = Session.Player;
            var direction = target - from;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(direction.normalized.y) * Mathf.Rad2Deg;
            player.RestorePhysicalPose(from - Vector3.up * player.PlayerCamera.transform.localPosition.y, yaw, pitch);
        }

        /// <summary>What the crosshair names and offers now: the HUD's name line and prompt.</summary>
        private static (string name, string prompt) Aimed()
        {
            var player = Session.Player;
            player.RefreshInteraction();
            string name = "";
            if (player.CurrentTargetCollider != null)
                foreach (var behaviour in player.CurrentTargetCollider.GetComponentsInParent<MonoBehaviour>(false))
                    if (behaviour is IFpsInteraction) { name = (behaviour as IFpsNamed)?.DisplayName ?? ""; break; }
            return (name, player.CurrentPrompt ?? "");
        }

        /// <summary>One press of E through the verification adapter (released again a frame later).</summary>
        private static IEnumerator Press()
        {
            var player = Session.Player;
            // 배속을 올려도 한 걸음의 시간은 짧게 준다(응답자가 긴 프레임은 받지 않는다).
            float step = Mathf.Min(Time.deltaTime, .03f);
            player.StepInput(Vector2.zero, Vector2.zero, false, false, false, step);
            yield return null;
            player.StepInput(Vector2.zero, Vector2.zero, false, true, false, step);
            yield return null;
            player.StepInput(Vector2.zero, Vector2.zero, false, false, false, step);
            yield return null;
        }

        private static IEnumerator Shot(string name)
        {
            string folder = config["shots"] != null ? (string)config["shots"] : Path.Combine(Path.GetDirectoryName(results), "shots");
            Directory.CreateDirectory(folder);
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "w4-check-" + name + ".png"));
            yield return null;
            yield return null;
        }

        private static StationEquipment Machine(string id) => EquipmentRegistry.Find(id) ?? throw new InvalidOperationException("no equipment " + id);

        private static readonly BindingFlags All = Members;

        private static T Field<T>(string name) => (T)typeof(IncidentDirector).GetField(name, All).GetValue(Session.Incidents);

        private static List<FireHazard> Fires() => Field<List<FireHazard>>("fires");

        private static object Offered(string kind)
        {
            var list = (IEnumerable)typeof(IncidentDirector).GetMethod("Origins", All).Invoke(Session.Incidents, null);
            foreach (var item in list) if ((string)item.GetType().GetField("Kind").GetValue(item) == kind) return item;
            return null;
        }

        private static string Describe(object transition) => (string)transition.GetType().GetField("Description").GetValue(transition);

        private static void Execute(object transition, float magnitude)
        {
            var execute = typeof(IncidentDirector).GetMethod("Execute", All);
            var type = transition.GetType();
            var arguments = execute.GetParameters().Select(p => p.ParameterType == typeof(float) ? magnitude : p.ParameterType == typeof(int) ? 1 : p.ParameterType == typeof(string) ? (object)"electric-check" : p.ParameterType == type ? transition : null).ToArray();
            execute.Invoke(Session.Incidents, arguments);
        }

        private static IEnumerator Force(string kind, float magnitude, Wait outcome, Action<object> chosenOut)
        {
            object chosen = null;
            yield return Until(() => (chosen = Offered(kind)) != null, 60, 120, outcome);
            chosenOut(chosen);
            if (chosen != null) Execute(chosen, magnitude);
        }

        /// <summary>The development of that kind the director offers right now, or null.</summary>
        private static object Development(string kind)
        {
            var list = (IEnumerable)typeof(IncidentDirector).GetMethod("Developments", All).Invoke(Session.Incidents, null);
            foreach (var item in list) if ((string)item.GetType().GetField("Kind").GetValue(item) == kind) return item;
            return null;
        }

        private static void KnowAll()
        {
            var know = typeof(IncidentDirector).GetMethod("Know", All);
            foreach (var hazard in Field<List<Hazard>>("all").ToList()) know.Invoke(Session.Incidents, new object[] { hazard, "electric-check" });
        }

        private static List<string> Radio()
        {
            var labels = new List<string>();
            foreach (var provider in Session.RadioProviders.ToArray()) foreach (var option in provider()) labels.Add(option.Label);
            return labels;
        }

        private static bool SendRadio(string contains)
        {
            foreach (var provider in Session.RadioProviders.ToArray())
                foreach (var option in provider())
                    if (option.Label.Contains(contains)) { option.Send?.Invoke(); return true; }
            return false;
        }

        // ── 시나리오 ──

        private static IEnumerator Board()
        {
            var machine = EquipmentRegistry.OfKind("vending_machine").OrderBy(e => e.Id, StringComparer.Ordinal).First(e => ElectricNetwork.CircuitOf(e) != null);
            var circuit = ElectricNetwork.CircuitOf(machine);
            var board = circuit.Board;
            Check("network built", ElectricNetwork.Boards.Count == EquipmentRegistry.OfKind("distribution_board").Count, ElectricNetwork.Boards.Count + " boards, " + ElectricNetwork.Boards.Sum(b => b.Branches.Count(c => c.Load != null)) + " machines hung on branches");
            var unit = board.Equipment.GetComponent<ElectricBoardUnit>();
            var t = board.Equipment.transform;
            var doorCentre = t.position + Vector3.up * (BreakerDeckLayout.BoardBottom + .4f);
            LookAt(doorCentre + t.forward * 1.2f, doorCentre);
            yield return null;
            var aim = Aimed();
            Check("closed board offers its door", aim.prompt.Contains("분전반 열기") && aim.name.Contains(board.Name), "name='" + aim.name + "' prompt='" + aim.prompt + "'");
            yield return Press();
            yield return Sleep(1.2f);
            Check("door open after E", unit.Door.IsOpen);

            var breaker = unit.Switches[circuit.Number];
            var target = breaker.GetComponent<Collider>().bounds.center;
            LookAt(target + t.forward * .8f, target);
            yield return null;
            aim = Aimed();
            Check("breaker named after its machine", aim.name.Contains(ElectricNetwork.Tag(machine)) && aim.name.Contains(board.Code) && aim.prompt.Contains("차단기 내리기"), "name='" + aim.name + "' prompt='" + aim.prompt + "'");
            Check("machine sticker names the breaker", machine.GetComponent<ElectricLoad>().DisplayName.Contains(circuit.Label), machine.GetComponent<ElectricLoad>().DisplayName);
            yield return Shot("board-breaker-on");
            yield return Press();
            yield return Sleep(.6f);
            Check("breaker off cuts the machine", !circuit.On && !ElectricNetwork.Powered(machine) && machine.State == "전원 차단", "state=" + machine.State);
            yield return Shot("board-breaker-off");
            aim = Aimed();
            Check("breaker shows off and offers to switch on", aim.name.Contains("꺼짐") && aim.prompt.Contains("올리기"), "name='" + aim.name + "' prompt='" + aim.prompt + "'");

            // 조명 회로는 손대지 않는다.
            var light = board.Branches.First(c => c.Load == null);
            var lightTarget = unit.Switches[light.Number].GetComponent<Collider>().bounds.center;
            LookAt(lightTarget + t.forward * .8f, lightTarget);
            yield return null;
            aim = Aimed();
            Check("lighting circuit is the electrician's", !aim.prompt.StartsWith("E ·") && aim.prompt.Contains("전기 담당"), "prompt='" + aim.prompt + "'");

            LookAt(target + t.forward * .8f, target);
            yield return null;
            yield return Press();
            yield return Sleep(.6f);
            Check("breaker on again powers the machine", circuit.On && ElectricNetwork.Powered(machine) && machine.State == "정상", "state=" + machine.State);

            // 멀리 가면 문이 닫힌다.
            LookAt(t.position + t.forward * 14f + Vector3.up * 1.6f, t.position + Vector3.up * 1.6f);
            yield return Sleep(7f);
            Check("door closes when the staff member walks away", !unit.Door.IsOpen);
        }

        private static IEnumerator Plug()
        {
            var machine = EquipmentRegistry.OfKind("vending_machine").OrderBy(e => e.Id, StringComparer.Ordinal).Last();
            var load = machine.GetComponent<ElectricLoad>();
            var t = machine.transform;
            var centre = t.position + Vector3.up * 1f;
            LookAt(centre + t.forward * 1.6f + Vector3.up * .6f, centre);
            yield return null;
            var aim = Aimed();
            Check("machine offers its plug", aim.prompt.Contains("전원 코드 뽑기") && aim.name.Contains(ElectricNetwork.Tag(machine)), "name='" + aim.name + "' prompt='" + aim.prompt + "'");
            yield return Shot("plug-in");
            yield return Press();
            Check("plug out cuts the machine", load.LocalOff && !ElectricNetwork.Powered(machine) && machine.State == "전원 차단", "state=" + machine.State);
            var dark = machine.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null && m.name.Contains("꺼짐")).ToList();
            Check("the machine's lit panels were swapped for dark ones", dark.Count > 0 && dark.All(m => m.GetColor("_EmissionColor").maxColorComponent < .01f), string.Join(", ", dark.Select(m => m.name)));
            yield return Shot("plug-out");
            yield return Press();
            Check("plug in powers it again", !load.LocalOff && ElectricNetwork.Powered(machine) && machine.State == "정상", "state=" + machine.State);
        }

        private static IEnumerator Kiosk()
        {
            var kiosk = EquipmentRegistry.OfKind("charging_kiosk").OrderBy(e => e.Id, StringComparer.Ordinal).First();
            var load = kiosk.GetComponent<ElectricLoad>();
            var t = kiosk.transform;
            var centre = t.position + Vector3.up * 1.2f;
            LookAt(centre + t.forward * 1.6f + Vector3.up * .3f, centre);
            yield return null;
            var aim = Aimed();
            Check("kiosk offers its power switch", aim.prompt.Contains("전원 스위치 끄기") && aim.name.Contains(ElectricNetwork.Tag(kiosk)), "name='" + aim.name + "' prompt='" + aim.prompt + "'");
            yield return Press();
            Check("switch off darkens the kiosk", load.LocalOff && !ElectricNetwork.Powered(kiosk) && kiosk.State == "전원 차단", "state=" + kiosk.State);
            var materials = kiosk.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList();
            Check("the kiosk's lit panel material was swapped for a dark one", materials.Any(m => m.name.Contains("꺼짐")) && materials.Where(m => m.name.Contains("꺼짐")).All(m => m.GetColor("_EmissionColor").maxColorComponent < .01f),
                string.Join(", ", materials.Select(m => m.name + (m.IsKeywordEnabled("_EMISSION") ? "[E " + m.GetColor("_EmissionColor").maxColorComponent.ToString("0.0") + "]" : ""))));
            yield return Shot("kiosk-off");
            yield return Press();
            Check("switch on lights it again", !load.LocalOff && ElectricNetwork.Powered(kiosk), "state=" + kiosk.State);
        }

        private static IEnumerator Fire()
        {
            var wait = new Wait();
            object chosen = null;
            yield return Force("vending_fire", .5f, wait, c => chosen = c);
            if (chosen == null) { Check("vending_fire offered", false, "not offered"); yield break; }
            Check("vending_fire offered with a real machine in its description", Describe(chosen).Contains("VM-"), Describe(chosen));
            var fire = Fires().LastOrDefault(f => f.Installation != null);
            if (fire == null) { Check("fire started", false); yield break; }
            KnowAll();
            var machine = fire.Installation;
            var load = machine.GetComponent<ElectricLoad>();
            float apart = Vector3.Distance(fire.View.transform.position, machine.transform.position + Vector3.up * .4f);
            Check("fire is fed by the machine's circuit and burns at the machine", fire.Feed == "전원" && apart < 1.1f && fire.Where.Contains("자동판매기"), "where='" + fire.Where + "' apart=" + apart.ToString("0.00") + " m");
            Check("machine reads on fire", machine.State == "화재" && load.OnFire, machine.State);
            var t = machine.transform;
            LookAt(t.position + t.forward * 2.8f + Vector3.up * 1.6f, t.position + Vector3.up * .5f);
            yield return Sleep(1.5f);
            yield return Shot("fire-vending-origin");

            fire.Suppress(1f, 30f);
            Check("live fire cannot be put out", !fire.Extinguished && fire.Feed != null && fire.Intensity > 0 && fire.Intensity <= FireHazard.LiveEmbers + .001f, "intensity=" + fire.Intensity.ToString("0.000"));
            bool wet = false;
            fire.WetWhileLive += _ => wet = true;
            fire.Suppress(1f, .05f, true);
            Check("water on a live fire raises the shock signal and drops the hose", wet && Session.Hands.Hose == null);

            var centre = t.position + Vector3.up * 1f;
            LookAt(centre + t.forward * 1.6f + Vector3.up * .6f, centre);
            yield return null;
            var aim = Aimed();
            Check("burning machine still offers its plug", aim.prompt.Contains("전원 코드 뽑기"), "prompt='" + aim.prompt + "'");
            Check("no cut report before the cut", !Radio().Any(l => l.Contains("전원 차단 보고")));
            yield return Press();
            yield return null;
            Check("plug out cuts the feed", fire.Feed == null && fire.CutBy == ElectricNetwork.StaffName, "cutBy=" + fire.CutBy);
            Check("cut report offered", Radio().Any(l => l.Contains("전원 차단 보고")), string.Join(" | ", Radio()));
            Check("cut report sent", SendRadio("전원 차단 보고"));
            var called = Field<IDictionary>("calledBy");
            Check("electrician called by the report", called.Contains(Agency.Facility));

            fire.Suppress(1f, 30f);
            Check("cut fire goes out", fire.Extinguished);
            yield return Sleep(1f);
            Check("machine stays dead after the fire", load.Burnt && !ElectricNetwork.Powered(machine) && machine.State == "소손", machine.State);
        }

        private static IEnumerator BoardFire()
        {
            var wait = new Wait();
            object chosen = null;
            yield return Force("board_fire", .5f, wait, c => chosen = c);
            if (chosen == null) { Check("board_fire offered", false, "not offered"); yield break; }
            var fire = Fires().LastOrDefault(f => f.Installation != null);
            if (fire == null) { Check("fire started", false); yield break; }
            KnowAll();
            var equipment = fire.Installation;
            var board = ElectricNetwork.BoardOf(equipment);
            var unit = equipment.GetComponent<ElectricBoardUnit>();
            float apart = Vector3.Distance(fire.View.transform.position, equipment.transform.position + Vector3.up * (BreakerDeckLayout.BoardBottom + .4f));
            Check("board fire burns at the board", fire.Feed == "전원" && apart < .8f, "apart=" + apart.ToString("0.00") + " m, where='" + fire.Where + "'");
            var t = equipment.transform;
            var doorCentre = t.position + Vector3.up * (BreakerDeckLayout.BoardBottom + .4f);
            LookAt(doorCentre + t.forward * 2.6f, doorCentre);
            yield return Sleep(1.5f);
            yield return Shot("fire-board-origin");

            fire.Grow(.35f);
            LookAt(doorCentre + t.forward * 1.2f, doorCentre);
            yield return null;
            var aim = Aimed();
            Check("a burning board cannot be opened", aim.prompt.Contains("열 수 없습니다"), "prompt='" + aim.prompt + "'");
            fire.Suppress(1f, 60f);
            Check("powder beats it down to the embers", fire.Intensity <= FireHazard.LiveEmbers + .001f && !fire.Extinguished, "intensity=" + fire.Intensity.ToString("0.000"));
            LookAt(doorCentre + t.forward * 1.2f, doorCentre);
            yield return null;
            aim = Aimed();
            Check("at the embers the board can be opened", aim.prompt.Contains("분전반 열기"), "prompt='" + aim.prompt + "'");
            yield return Press();
            yield return Sleep(1.2f);
            Check("door open", unit.Door.IsOpen);
            var target = unit.Switches[0].GetComponent<Collider>().bounds.center;
            LookAt(target + t.forward * .8f, target);
            yield return null;
            aim = Aimed();
            Check("main breaker named", aim.name.Contains("메인 차단기") && aim.prompt.Contains("메인 차단기 내리기"), "name='" + aim.name + "' prompt='" + aim.prompt + "'");
            yield return Press();
            yield return Sleep(.6f);
            Check("main off cuts the board, the fire feed and every machine on it", !board.Main.On && fire.Feed == null && board.Branches.Where(c => c.Load != null).All(c => !ElectricNetwork.Powered(c.Load)), "feed=" + fire.Feed);
            Check("lock-out tag hung by the staff member", board.Main.Tag != null && board.Main.Tag.Contains("작동금지") && board.Main.TagBy == ElectricNetwork.StaffName, board.Main.Tag);
            yield return Shot("fire-board-main-off");
            fire.Suppress(1f, 60f);
            Check("cut fire goes out", fire.Extinguished);
            yield return Sleep(1f);
            Check("burnt board stays dead and cannot be switched on", board.Damaged && !board.Main.On && !ElectricNetwork.Switch(board.Main, true, "시험"));
        }

        private static IEnumerator Electrician()
        {
            var wait = new Wait();
            object chosen = null;
            yield return Force("vending_fire", .5f, wait, c => chosen = c);
            if (chosen == null) { Check("vending_fire offered", false, "not offered"); yield break; }
            var fire = Fires().LastOrDefault(f => f.Installation != null);
            if (fire == null) { Check("fire started", false); yield break; }
            KnowAll();
            var director = Session.Incidents;
            Check("report option offered", SendRadio("화재 보고"), string.Join(" | ", Radio()));
            var called = Field<IDictionary>("calledBy");
            Check("fire report calls the fire service and the electrician", called.Contains(Agency.Fire) && called.Contains(Agency.Facility), string.Join(", ", called.Keys.Cast<object>()));
            float start = Time.time;
            yield return Until(() => fire.Feed == null, 240, 240, wait);
            Check("the electrician cuts the feed by hand", fire.Feed == null && fire.CutBy == "전기 담당", "cutBy=" + fire.CutBy + " after " + (Time.time - start).ToString("0") + " game s");
            var responders = Object.FindObjectsByType<Responder>(FindObjectsSortMode.None).Where(r => r.Agency == Agency.Facility).ToList();
            Check("an electrician team walked in", responders.Any(r => r.Team == Team.Electric), string.Join(", ", responders.Select(r => r.DisplayName + "/" + r.Team)));
            var circuit = ElectricNetwork.CircuitOf(fire.Installation);
            Check("the breaker went down with it and carries the electrician's tag", circuit != null && !circuit.On && circuit.TagBy == "전기 담당", circuit?.Tag);
            wait.TimedOut = false;
            yield return Until(() => fire.Extinguished, 300, 240, wait);
            Check("the fire brigade puts the cut fire out", fire.Extinguished && !wait.TimedOut);
            wait.TimedOut = false;
            var sent = new HashSet<string>();
            while (director.Stage == IncidentDirector.Phase.Incident && !wait.TimedOut)
            {
                foreach (var provider in Session.RadioProviders.ToArray())
                    foreach (var option in provider())
                        if (sent.Add(option.Label)) option.Send?.Invoke();
                foreach (var responder in Object.FindObjectsByType<Responder>(FindObjectsSortMode.None))
                    if (responder.Lead && responder.OnScene && director.CanHandOver(responder)) { director.HandOver(responder); goto handed; }
                yield return Sleep(1f);
                if (Time.time - start > 600) wait.TimedOut = true;
            }
            handed:
            yield return Until(() => director.Stage == IncidentDirector.Phase.Ended, 20, 60, wait);
            Check("handover ends the shift", director.Stage == IncidentDirector.Phase.Ended, "stage=" + director.Stage + " after " + (Time.time - start).ToString("0") + " game s");
        }

        /// <summary>The developments of an equipment fire, forced one by one: the shock, the spread to the neighbour, the protection tripping, and a burst pipe's water reaching a machine.</summary>
        private static IEnumerator Developments()
        {
            var director = Session.Incidents;
            var all = EquipmentRegistry.OfKind("vending_machine").Concat(EquipmentRegistry.OfKind("charging_kiosk")).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
            var machine = all.FirstOrDefault(e => all.Any(o => o != e && Mathf.Abs(o.transform.position.y - e.transform.position.y) < 1f && Vector3.Distance(o.transform.position, e.transform.position) < 1.8f));
            if (machine == null) { Check("a machine with a neighbour exists", false); yield break; }
            var start = typeof(IncidentDirector).GetMethod("StartEquipmentFire", All);
            var fire = (FireHazard)start.Invoke(director, new object[] { machine, .5f, null, null });
            KnowAll();
            Check("fire fed by the circuit", fire.Feed == "전원" && fire.Installation == machine, machine.Id);

            // 감전: 통전된 불 옆에 선 사람.
            var person = Session.Crowd.People.First(p => !p.Hurt && !p.Aboard && !p.Hostile);
            person.Body.Agent.Warp(StationWorld.OnNavMesh(machine.transform.position + machine.transform.forward * 1.1f, 1.5f));
            yield return null; // 바로 묻는다: 걷는 승객은 1초 뒤 1.8 m 밖이다.
            var shock = Development("electric_shock");
            Check("shock offered next to a live burning machine", shock != null, shock != null ? Describe(shock) : "not offered");
            if (shock != null)
            {
                Execute(shock, .5f);
                Check("the person who touched it is down", person.Hurt);
            }

            var spread = Development("equipment_fire_spreads");
            Check("spread to the neighbour offered", spread != null, spread != null ? Describe(spread) : "not offered");
            int before = Fires().Count;
            if (spread != null)
            {
                Execute(spread, .5f);
                var next = Fires().Skip(before).FirstOrDefault(f => f.Installation != null);
                Check("the neighbour burns and is fed too", next != null && next.Installation != machine && next.Feed == "전원", next != null ? next.Where : "no new fire");
            }

            var trip = Development("protection_trips");
            Check("protection trip offered", trip != null, trip != null ? Describe(trip) : "not offered");
            if (trip != null)
            {
                Execute(trip, .5f);
                Check("the protection cut the first fire's feed", fire.Feed == null && fire.CutBy == "보호장치", "cutBy=" + fire.CutBy);
                var circuit = ElectricNetwork.CircuitOf(machine);
                Check("its breaker went down without a tag", circuit == null || !circuit.On && circuit.Tag == null, circuit?.Tag);
            }

            // 누수: 터진 배관의 물이 다른 기계 밑동에 닿는다.
            var dry = all.First(e => !Fires().Any(f => f.Installation == e) && Vector3.Distance(e.transform.position, machine.transform.position) > 12f && ElectricNetwork.Powered(e));
            typeof(IncidentDirector).GetMethod("StartLeak", All).Invoke(director, new object[] { dry.transform.position + dry.transform.forward * .5f, .5f });
            var wet = Development("wet_equipment_short");
            Check("the water reaches the machine and a short is offered", wet != null, wet != null ? Describe(wet) : "not offered near " + dry.Id);
            if (wet != null)
            {
                int count = Fires().Count;
                Execute(wet, .5f);
                var shorted = Fires().Skip(count).FirstOrDefault(f => f.Installation == dry);
                Check("the wet machine burns", shorted != null && shorted.Source.Contains("누수"), shorted != null ? shorted.Source : "no fire");
            }
            yield return Sleep(1f);
        }

        /// <summary>A cigarette butt in a real litter bin: the fire burns at the bin's mouth, the primitive stand-in is gone, a plain extinguisher puts it out (nothing feeds it), the bin reads burnt afterwards.</summary>
        private static IEnumerator Bin()
        {
            var wait = new Wait();
            object chosen = null;
            yield return Force("bin_fire", .5f, wait, c => chosen = c);
            if (chosen == null) { Check("bin_fire offered", false, "not offered"); yield break; }
            Check("description names a placed litter bin", Describe(chosen).Contains("litter bin bin-"), Describe(chosen));
            var fire = Fires().LastOrDefault(f => f.Installation != null);
            if (fire == null) { Check("fire started", false); yield break; }
            KnowAll();
            var bin = fire.Installation;
            var mouth = bin.transform.position + bin.transform.forward * .22f + Vector3.up * .85f;
            float apart = Vector3.Distance(fire.View.transform.position, mouth);
            Check("burns at the mouth of the placed bin", bin.Kind == "litter_bin" && apart < .05f, bin.Id + " apart=" + apart.ToString("0.000") + " m, where='" + fire.Where + "'");
            Check("no runtime primitive under the fire", fire.View.GetComponentsInChildren<Transform>(true).All(t => t.name != "휴지통"));
            Check("nothing feeds it and it is no electrical fire", fire.Feed == null && !fire.Electric && !fire.Involves(Agency.Facility));
            Check("bin reads on fire", bin.State == "화재", bin.State);
            var t = bin.transform;
            LookAt(t.position + t.forward * 2.4f + Vector3.up * 1.6f, mouth);
            yield return Sleep(1.5f);
            yield return Shot("fire-bin-origin");
            fire.Suppress(1f, 30f);
            Check("a plain extinguisher puts it out", fire.Extinguished);
            yield return Sleep(1f);
            Check("bin reads burnt afterwards", bin.State == "소손", bin.State);
        }

        /// <summary>A battery in a recycling station: the fire burns at its mouth and spreads to the litter bin beside it.</summary>
        private static IEnumerator BinBattery()
        {
            var wait = new Wait();
            object chosen = null;
            yield return Force("bin_battery_fire", .5f, wait, c => chosen = c);
            if (chosen == null) { Check("bin_battery_fire offered", false, "not offered"); yield break; }
            Check("description names a placed recycling station", Describe(chosen).Contains("recycling station "), Describe(chosen));
            var fire = Fires().LastOrDefault(f => f.Installation != null);
            if (fire == null) { Check("fire started", false); yield break; }
            KnowAll();
            var station = fire.Installation;
            Check("burns at the recycling station", station.Kind == "recycling_bin" && Vector3.Distance(fire.View.transform.position, station.transform.position + Vector3.up * 1.05f) < .05f, station.Id + " where='" + fire.Where + "'");
            var spread = Development("bin_fire_spreads");
            Check("spread to the litter bin beside it offered", spread != null, spread != null ? Describe(spread) : "not offered");
            if (spread != null)
            {
                int before = Fires().Count;
                Execute(spread, .5f);
                var next = Fires().Skip(before).FirstOrDefault(f => f.Installation != null);
                Check("the neighbouring bin burns too", next != null && next.Installation.Kind == "litter_bin", next != null ? next.Where : "no new fire");
            }
            fire.Suppress(1f, 30f);
            Check("the battery fire goes out with the extinguisher", fire.Extinguished);
        }

        /// <summary>JEV answers a moment after the candidates were listed: an answer that names a machine which lost its power meanwhile, or which is already burning, starts nothing.</summary>
        private static IEnumerator Stale()
        {
            yield return Sleep(3f);
            var listed = Offered("vending_fire");
            if (listed == null) { Check("vending_fire offered", false, "not offered"); yield break; }
            var key = (string)listed.GetType().GetField("Key").GetValue(listed);
            var machine = EquipmentRegistry.OfKind("vending_machine").FirstOrDefault(e => key == "vending_fire_" + e.Id);
            if (machine == null) { Check("the offered machine is a placed one", false, key); yield break; }
            ElectricNetwork.Switch(ElectricNetwork.CircuitOf(machine), false, "electric-check");
            int before = Fires().Count;
            Execute(listed, .5f);
            Check("an answer for a machine that lost its power meanwhile starts nothing", Fires().Count == before && machine.State != "화재", machine.Id + " state=" + machine.State + " fires " + before + " -> " + Fires().Count);
            ElectricNetwork.Switch(ElectricNetwork.CircuitOf(machine), true, "electric-check");
            var again = Offered("vending_fire");
            if (again == null) { Check("vending_fire offered again", false, "not offered"); yield break; }
            var againKey = (string)again.GetType().GetField("Key").GetValue(again);
            Execute(again, .5f);
            int burning = Fires().Count;
            Execute(again, .5f);
            Check("a second answer for a machine that already burns starts no second fire", burning == before + 1 && Fires().Count == burning, againKey + " fires " + before + " -> " + burning + " -> " + Fires().Count);
        }

        /// <summary>
        /// What the equipment costs while the shift runs: the time to list the causes of each family (the electric and bin causes are
        /// the ElectricPlaza and Fire families' part of it) and of the electric tick. Each is the best of many runs (the editor shares the
        /// machine with other work, so a mean drifts by a factor of two between runs), and each run starts without the crowd snapshot
        /// of the frame, as the first listing of a frame does.
        /// </summary>
        private static IEnumerator Perf()
        {
            yield return Sleep(3f);
            var director = Session.Incidents;
            var snapshot = typeof(IncidentDirector).GetField("peopleSnapshotFrame", All);
            var poolsType = typeof(IncidentDirector).GetNestedType("Pools", All);
            var pools = Activator.CreateInstance(poolsType, All, null, new object[] { director }, null);
            var arguments = new object[] { pools };
            var whole = typeof(IncidentDirector).GetMethod("Origins", All);
            var parts = new List<string>();
            double Best(int runs, Action run)
            {
                double best = double.MaxValue;
                for (int i = -1; i < runs; i++)
                {
                    snapshot.SetValue(director, -1);
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    run();
                    if (i >= 0) best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
                }
                return best;
            }
            void Family(string name)
            {
                var method = typeof(IncidentDirector).GetMethod(name, All);
                double ms = Best(30, () =>
                {
                    var result = method.Invoke(director, arguments);
                    if (result is IEnumerable list) foreach (var _ in list) { }
                });
                parts.Add(name.Replace("Origins", "") + " " + ms.ToString("0.00"));
            }
            Family("FireOrigins"); Family("CasualtyOrigins"); Family("SecurityOrigins"); Family("TrainOrigins"); Family("FacilityOrigins");
            var candidates = typeof(IncidentDirector).GetMethod("Candidates", All);
            var binFire = typeof(IncidentDirector).GetMethod("BinFire", All);
            var nearAnExit = (Func<StationEquipment, bool>)Delegate.CreateDelegate(typeof(Func<StationEquipment, bool>), director, typeof(IncidentDirector).GetMethod("NearAnExit", All));
            var peopleNear = typeof(IncidentDirector).GetMethod("PeopleNear", All);
            Func<StationEquipment, bool> peopleAtTheStation = e => (int)peopleNear.Invoke(director, new object[] { e.transform.position, 10f }) > 0;
            double bins = Best(30, () =>
            {
                foreach (var bin in (IEnumerable<StationEquipment>)candidates.Invoke(director, new object[] { "litter_bin", 2, nearAnExit })) binFire.Invoke(director, new object[] { bin, false });
                foreach (var bin in (IEnumerable<StationEquipment>)candidates.Invoke(director, new object[] { "recycling_bin", 1, peopleAtTheStation })) binFire.Invoke(director, new object[] { bin, true });
            });
            parts.Add("bins " + bins.ToString("0.00"));
            var electricMethod = typeof(IncidentDirector).GetMethod("ElectricPlazaOrigins", All);
            var transitions = typeof(List<>).MakeGenericType(typeof(IncidentDirector).GetNestedType("Transition", All));
            double electric = Best(30, () => electricMethod.Invoke(director, new object[] { pools, Activator.CreateInstance(transitions) }));
            parts.Add("ElectricPlaza " + electric.ToString("0.00"));
            parts.Add("Pools " + Best(30, () => Activator.CreateInstance(poolsType, All, null, new object[] { director }, null)).ToString("0.00"));
            int count = 0;
            double listing = Best(30, () => count = ((ICollection)whole.Invoke(director, null)).Count);
            var tick = typeof(IncidentDirector).GetMethod("ElectricPlazaTick", All);
            double perFrame = Best(300, () => tick.Invoke(director, new object[] { .016f }));
            int renderers = EquipmentRegistry.All.Sum(e => e.GetComponentsInChildren<Renderer>(true).Length);
            string summary = "best ms per call [" + string.Join(", ", parts) + "]; whole Origins() " + listing.ToString("0.00") + " ms (" + count + " candidates offered); electric tick " + (perFrame * 1000).ToString("0.0") + " µs per frame; " + EquipmentRegistry.All.Count + " placed pieces, " + renderers + " renderers";
            Check("the electric plaza causes are cheap to list", electric < 1, summary);
            Check("the litter bin and recycling station causes are cheap to list", bins < 1, summary);
        }
    }
}
