// Copy only into Assets/SchoolValidation in an isolated validation checkout.
// This instruments a separate player; it does not belong in the training build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Demo;
using UnityEngine;
using UnityEngine.Profiling;

[DefaultExecutionOrder(1000)]
public sealed class SchoolWindowsValidationDriver : MonoBehaviour
{
    [Serializable] public sealed class Result
    {
        public string status, unity, graphics, workload;
        public double requestedSeconds, elapsedSeconds;
        public int width, height, vSyncCount, targetFrameRate, shifts, incidentOnsets;
        public long frames, cameraRenderCallbacks;
        public bool batchMode, manualInputVerified, hmdVerified;
        public string[] observedCases, errors;
    }

    private static readonly int[] Seeds = { 10, 1, 2, 104, 2107, 6123 };
    private readonly List<double> frameTimes = new List<double>();
    private readonly HashSet<string> cases = new HashSet<string>();
    private readonly List<string> errors = new List<string>();
    private StationWorldController world;
    private StreamWriter samples;
    private string output, lastIncident;
    private double duration, start, previousFrame, sampleStart, shiftStart;
    private long frames;
    private long renderCallbacks;
    private int shifts, onsets;
    private bool initialized, finished;

    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, name);
        if (at < 0 || at + 1 == args.Length) throw new ArgumentException("Missing " + name);
        return args[at + 1];
    }

    private IEnumerator Start()
    {
        yield return null; // Existing scene controllers initialize themselves first.
        try
        {
            duration = double.Parse(Argument("--school-duration-seconds"), CultureInfo.InvariantCulture);
            if (double.IsNaN(duration) || duration < 15 || duration > 1800)
                throw new ArgumentException("Duration must be 15..1800 seconds.");
            output = Path.GetFullPath(Argument("--school-output"));
            if (Directory.Exists(output) || File.Exists(output))
                throw new IOException("Output must be a new directory.");
            Directory.CreateDirectory(output);
            samples = new StreamWriter(Path.Combine(output, "samples.csv"), false);
            samples.WriteLine("seconds,frames,fps,frame_p50_ms,frame_p95_ms,frame_max_ms,unity_allocated_bytes,managed_bytes,working_set_bytes,private_bytes,shift,phase,world_seconds");
            world = FindFirstObjectByType<StationWorldController>();
            if (world == null || !world.IsConfigured) throw new InvalidOperationException("Existing world is not configured.");
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            Camera.onPostRender += OnRendered;
            start = previousFrame = sampleStart = Time.realtimeSinceStartupAsDouble;
            BeginShift(start);
            initialized = true;
        }
        catch (Exception error)
        {
            errors.Add(error.GetType().Name + ": " + error.Message);
            Finish(false);
        }
    }

    private void BeginShift(double now)
    {
        world.StartShift(Seeds[shifts % Seeds.Length]);
        world.Player.SetControlEnabled(false);
        shiftStart = now;
        lastIncident = null;
        shifts++;
    }

    private void Update()
    {
        if (!initialized || finished) return;
        try
        {
            var now = Time.realtimeSinceStartupAsDouble;
            frameTimes.Add((now - previousFrame) * 1000);
            previousFrame = now;
            frames++;
            // Keep the original world Update/OnGUI path active in an unattended
            // window. This is an automated focus override, not an input test.
            world.Session.Paused = false;
            world.Player.SetControlEnabled(false);
            world.Player.ViewCamera.transform.rotation = Quaternion.Euler(0, (float)((now - shiftStart) * 8 % 360), 0);
            if (world.Session.Active != null && world.Session.Active.Id != lastIncident)
            {
                lastIncident = world.Session.Active.Id;
                onsets++;
                cases.Add(world.Session.Active.SiteId + "/" + world.Session.Active.Kind);
            }
            if (now - sampleStart >= 1) Sample(now);
            if (errors.Count > 0) { Finish(false); return; }
            if (now - start >= duration) { Finish(true); return; }
            if (now - shiftStart >= 120) BeginShift(now);
        }
        catch (Exception error)
        {
            errors.Add(error.GetType().Name + ": " + error.Message);
            Finish(false);
        }
    }

    private void Sample(double now)
    {
        if (frameTimes.Count == 0) return;
        frameTimes.Sort();
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
        {
            process.Refresh();
            samples.WriteLine(string.Join(",", new object[] {
                (now-start).ToString("F4", CultureInfo.InvariantCulture), frameTimes.Count,
                (frameTimes.Count/(now-sampleStart)).ToString("F3", CultureInfo.InvariantCulture),
                frameTimes[(int)((frameTimes.Count-1)*.50)].ToString("F3", CultureInfo.InvariantCulture),
                frameTimes[(int)((frameTimes.Count-1)*.95)].ToString("F3", CultureInfo.InvariantCulture),
                frameTimes[frameTimes.Count-1].ToString("F3", CultureInfo.InvariantCulture),
                Profiler.GetTotalAllocatedMemoryLong(), GC.GetTotalMemory(false),
                process.WorkingSet64, process.PrivateMemorySize64, shifts,
                world.Session.Phase, world.Session.Seconds.ToString("F3", CultureInfo.InvariantCulture)
            }));
        }
        samples.Flush();
        frameTimes.Clear();
        sampleStart = now;
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(type + ": " + message);
    }

    private void OnRendered(Camera camera)
    {
        if (world != null && camera == world.Player.ViewCamera) renderCallbacks++;
    }

    private void Finish(bool completed, bool quit = true)
    {
        if (finished) return;
        finished = true;
        Application.logMessageReceived -= OnLog;
        Camera.onPostRender -= OnRendered;
        try
        {
            if (initialized && frameTimes.Count > 0) Sample(Time.realtimeSinceStartupAsDouble);
            if (samples != null) samples.Dispose();
            var result = new Result {
                status = completed && errors.Count == 0 ? "completed" : "failed",
                unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(),
                workload = "Existing world Update and UI; fixed spawn, camera sweep; ordinary NPC/incident states; new shift every 120s. Automated focus override. No response completion or manual input.",
                requestedSeconds = duration, elapsedSeconds = initialized ? Time.realtimeSinceStartupAsDouble-start : 0,
                width = Screen.width, height = Screen.height, vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate, frames = frames,
                cameraRenderCallbacks = renderCallbacks, batchMode = Application.isBatchMode, shifts = shifts,
                incidentOnsets = onsets, observedCases = cases.OrderBy(x=>x).ToArray(), errors = errors.ToArray()
            };
            // Do not touch an existing output directory when initialization rejected it.
            if (samples != null) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("SCHOOL_VALIDATION_" + result.status.ToUpperInvariant());
        }
        catch (Exception error) { Debug.LogError("School receipt failure: " + error.GetType().Name); completed = false; }
        if (quit) Application.Quit(completed && errors.Count == 0 ? 0 : 1);
    }

    private void OnApplicationQuit() { if (!finished) Finish(false, false); }
}

#if UNITY_EDITOR
public static class SchoolWindowsValidationBuilder
{
    public static void Build()
    {
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, "--school-build-output");
        if (at < 0 || at+1 == args.Length) throw new ArgumentException("Missing build output.");
        var output = Path.GetFullPath(args[at+1]);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Build output must be new.");
        var scene = ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.Build();
        new GameObject("School validation instrumentation").AddComponent<SchoolWindowsValidationDriver>();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/SchoolValidation/StandaloneValidation.unity");
        // Keep validation records separate from the ordinary player's local records.
        UnityEditor.PlayerSettings.productName = "CHOOGuard School Validation";
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
            scenes = new[] { "Assets/SchoolValidation/StandaloneValidation.unity" },
            locationPathName = Path.Combine(output, "SchoolValidation.exe"),
            target = UnityEditor.BuildTarget.StandaloneWindows64,
            options = UnityEditor.BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("School validation build failed: " + report.summary.result);
        Debug.Log("SCHOOL_VALIDATION_BUILD_OK");
    }
}
#endif
