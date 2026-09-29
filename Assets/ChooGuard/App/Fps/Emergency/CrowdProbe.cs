using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Temporary measurement probe for w2-crowd (never committed): starts a real fire by the director's own path, rings the bell, announces, instructs, and records how the crowd reacted.</summary>
    public sealed class CrowdProbe : MonoBehaviour
    {
        private const string Flag = "/tmp/w2-probe/run.flag", Result = "/tmp/w2-probe/result.json";
        private static readonly BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!File.Exists(Flag) || FindAnyObjectByType<CrowdProbe>() != null) return;
            var go = new GameObject("CrowdProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CrowdProbe>();
        }

        private IEnumerator Start()
        {
            EmergencySession s = null;
            while (s == null || s.Crowd == null || s.Incidents == null || s.Crowd.People.Count == 0) { s = EmergencySession.Current; yield return null; }
            string shots = File.ReadAllText(Flag).Trim();
            File.Delete(Flag);
            var crowd = s.Crowd;
            var director = s.Incidents;
            float t0 = Time.time;
            var frames = new List<float>();
            var timeline = new JArray();
            var samples = new JArray();
            Passenger witness = null;
            int witnessNumber = 0;
            float fireGame = -1, fireReal = -1, nextSample = 0;
            string lastActivity = null;
            bool fired = false, belled = false, announced = false, instructed = false;
            FireHazard fire = null;
            var shotAt = new List<(float at, string name)> { (1.5f, "fire+1.5s"), (5f, "fire+5s"), (12f, "fire+12s"), (30f, "fire+30s") };
            var lastWitness = Vector3.zero;
            while (Time.time - t0 < 120)
            {
                float t = Time.time - t0;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                if (s.Player.IsPaused) s.Player.Resume(false);
                if (!fired && t > 20)
                {
                    fired = true;
                    int best = -1;
                    foreach (var p in crowd.People)
                    {
                        if (p.Aboard || p.Hostile || p.Hurt || !Passenger.Routine(p.Current) || p.Current == Passenger.Activity.InTrain || !p.Body.Visible) continue;
                        int near = crowd.CountNear(p.transform.position, 8, null);
                        if (near > best) { best = near; witness = p; }
                    }
                    fireGame = Time.time;
                    fireReal = Time.realtimeSinceStartup;
                    lastWitness = witness.transform.position;
                    witnessNumber = witness.Number;
                    typeof(IncidentDirector).GetMethod("StartPowerBankFire", Any).Invoke(director, new object[] { witness, .55f });
                    fire = director.Main as FireHazard;
                    if (fire != null)
                    {
                        var away = witness.transform.position - fire.Position; away.y = 0;
                        var at = StationWorld.OnNavMesh(fire.Position + away.normalized * 14f, 4f);
                        var look = fire.Position - at; look.y = 0;
                        s.Player.RestorePhysicalPose(at, Quaternion.LookRotation(look).eulerAngles.y, 6f);
                    }
                    timeline.Add(new JObject { ["t"] = 0, ["event"] = "fire started next to passenger " + witness.Number + " (" + best + " people within 8 m)", ["where"] = fire != null ? fire.Where : "?" });
                }
                if (fired && !belled && t > 50 && fire != null && fire.Active)
                {
                    belled = true;
                    typeof(IncidentDirector).GetMethod("TriggerAlarm", Any).Invoke(director, new object[] { fire });
                    timeline.Add(new JObject { ["t"] = Time.time - fireGame, ["event"] = "alarm bell" });
                }
                if (fired && !announced && t > 58 && director.Main != null)
                {
                    announced = true;
                    typeof(IncidentDirector).GetMethod("Announce", Any, null, System.Type.EmptyTypes, null).Invoke(director, null);
                    timeline.Add(new JObject { ["t"] = Time.time - fireGame, ["event"] = "public announcement" });
                }
                if (fired && !instructed && t > 66)
                {
                    instructed = true;
                    // 불에서 가장 가까운, 아직 나가지 않는 사람 곁에서 역무원이 대피 방향을 안내한다.
                    Passenger nearest = null;
                    float bestDistance = float.PositiveInfinity;
                    foreach (var p in crowd.People)
                    {
                        if (p == null || p.Hurt || p.Current == Passenger.Activity.Evacuate || p.Aboard) continue;
                        float d = fire != null ? Vector3.Distance(p.transform.position, fire.Position) : 0;
                        if (d < bestDistance) { bestDistance = d; nearest = p; }
                    }
                    int told = nearest != null ? crowd.InstructAround(nearest.transform.position, s.Player.transform.position) : 0;
                    timeline.Add(new JObject { ["t"] = Time.time - fireGame, ["event"] = "staff instructs " + told + " people around passenger " + (nearest != null ? nearest.Number : 0) });
                }
                if (fired && shotAt.Count > 0 && Time.time - fireGame >= shotAt[0].at)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(shots, "w2-crowd-" + shotAt[0].name + ".png"));
                    shotAt.RemoveAt(0);
                }
                if (witness != null) lastWitness = witness.transform.position;
                if (witness != null)
                {
                    string now = witness.Current + "/" + (witness.Slot.Urgent != null ? "judging" : "-");
                    if (now != lastActivity) { lastActivity = now; timeline.Add(new JObject { ["t"] = fired ? Time.time - fireGame : -1, ["witness"] = now }); }
                }
                if (t >= nextSample)
                {
                    nextSample = t + 2;
                    int waiting = crowd.People.Count(p => p.Slot.Waiting);
                    samples.Add(new JObject { ["t"] = Mathf.RoundToInt(t), ["people"] = crowd.People.Count, ["waiting"] = waiting, ["longest_wait"] = crowd.Mind.Metrics.LongestWait, ["max_queued"] = crowd.Mind.Metrics.MaxQueued });
                }
                yield return null;
            }
            frames.Sort();
            var result = new JObject
            {
                ["witness"] = witnessNumber,
                ["fire_start_game"] = fireGame,
                ["fire_start_real"] = fireReal,
                ["timeline"] = timeline,
                ["samples"] = samples,
                ["frame_ms"] = new JObject { ["n"] = frames.Count, ["p50"] = frames[frames.Count / 2], ["p95"] = frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * .95f))], ["max"] = frames[frames.Count - 1] },
                ["crowd"] = crowd.Mind.Metrics.Summary(s.Jev),
                ["jev_status"] = s.Jev.Status,
            };
            File.WriteAllText(Result, result.ToString());
            Destroy(gameObject);
        }
    }
}
