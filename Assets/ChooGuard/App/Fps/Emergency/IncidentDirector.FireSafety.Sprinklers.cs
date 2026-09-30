using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The sprinkler system of the twin as real objects: closed heads that burst when a fire heats them and put the fire under control, alarm valves whose flow signal
    /// rings the station bell, and the pipes that can spring a leak. Heads act on the fires in <see cref="fires"/> (a fire under a head heats its bulb; the water
    /// takes the intensity down but a big fire outgrows it), and a valve that is shut puts its zone out of service. Times and rates are game tuning like
    /// <see cref="FireHazard.Growth"/>; the placement and the sizes follow NFTC 103 (SprinklerLayout).
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<SprinklerHeadPoint> sprinklerHeads = new List<SprinklerHeadPoint>();
        private readonly Dictionary<(int x, int z), List<SprinklerHeadPoint>> headGrid = new Dictionary<(int, int), List<SprinklerHeadPoint>>();
        private readonly Dictionary<string, List<SprinklerHeadPoint>> headsByValve = new Dictionary<string, List<SprinklerHeadPoint>>();
        private readonly Dictionary<string, SprinklerValvePoint> valves = new Dictionary<string, SprinklerValvePoint>();
        private readonly List<SprinklerPipeLine> pipes = new List<SprinklerPipeLine>();
        private readonly Dictionary<(FireHazard fire, SprinklerHeadPoint head), float> headSensing = new Dictionary<(FireHazard, SprinklerHeadPoint), float>();
        private readonly List<SprinklerHeadPoint> discharging = new List<SprinklerHeadPoint>();
        /// <summary>Valves whose flow signal is showing at the receiver.</summary>
        private readonly HashSet<string> flowSignals = new HashSet<string>();
        private float lastSprinklerCheck;
        // 사람이 서는 자리 곁인지는 근무 내내 그대로다(고정된 배관·헤드와 대기 자리): 한 번만 골라 이 근무의 순위순으로 두고, 박동마다는
        // 밸브·작동·통제선만 본다. 매 박동 헤드·배관 수천 개에 가장 가까운 대기 자리를 찾으면 1초마다 80 ms 가 멈췄다(2026-09-30 측정).
        private List<(SprinklerPipeLine pipe, Vector3 floor)> publicPipes;
        private List<SprinklerHeadPoint> publicHeads;

        private const float HeadBucket = 4f;

        /// <summary>Water is running in some zone: the receiver cannot be reset until the facility team shuts that valve.</summary>
        private bool SprinklerFlowing => flowSignals.Count > 0;

        private void BindSprinklers()
        {
            foreach (var equipment in EquipmentRegistry.OfKind(SprinklerValvePoint.ValveKind).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var valve = equipment.GetComponent<SprinklerValvePoint>();
                valve.Bind();
                valves[valve.Key] = valve;
            }
            foreach (var equipment in EquipmentRegistry.OfKind(SprinklerHeadPoint.HeadKind).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var head = equipment.GetComponent<SprinklerHeadPoint>();
                head.Bind();
                sprinklerHeads.Add(head);
                var bucket = (Mathf.FloorToInt(equipment.transform.position.x / HeadBucket), Mathf.FloorToInt(equipment.transform.position.z / HeadBucket));
                if (!headGrid.TryGetValue(bucket, out var cell)) headGrid[bucket] = cell = new List<SprinklerHeadPoint>();
                cell.Add(head);
                if (!headsByValve.TryGetValue(head.Valve, out var zone)) headsByValve[head.Valve] = zone = new List<SprinklerHeadPoint>();
                zone.Add(head);
            }
            foreach (var equipment in EquipmentRegistry.OfKind(SprinklerPipeLine.PipeKind).OrderBy(e => e.Id, StringComparer.Ordinal)) pipes.Add(equipment.GetComponent<SprinklerPipeLine>());
            RankPublicSprinklerSpots();
        }

        private SprinklerValvePoint ValveOf(SprinklerHeadPoint head) => valves.TryGetValue(head.Valve, out var valve) ? valve : null;

        // ── 화재에 대한 작동 ──

        /// <summary>A head's bulb reaches its rating when a fire of some size burns within reach under it; the higher the head, the bigger the fire must be.</summary>
        private static bool HeadSenses(FireHazard fire, SprinklerHeadPoint head, float reach) =>
            fire.Intensity >= .22f + .025f * head.MountHeight && reach <= Mathf.Min(4f, .9f + 3.6f * fire.Intensity);

        /// <summary>Seconds the bulb needs to burst once it is hot enough (response time index of a standard-response head).</summary>
        private static float HeadDelay(SprinklerHeadPoint head) => 6f + head.MountHeight;

        private void SprinklerFireTick()
        {
            float dt = Mathf.Max(.05f, Time.time - lastSprinklerCheck);
            lastSprinklerCheck = Time.time;
            foreach (var fire in fires)
            {
                // 열차 안의 불은 승강장 위 헤드에 닿지 않는다.
                if (fire.Extinguished || fire.Aboard && !fire.Beneath) continue;
                int bx = Mathf.FloorToInt(fire.Position.x / HeadBucket), bz = Mathf.FloorToInt(fire.Position.z / HeadBucket);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!headGrid.TryGetValue((bx + dx, bz + dz), out var cell)) continue;
                        foreach (var head in cell)
                        {
                            if (head.Activated) continue;
                            var valve = ValveOf(head);
                            if (valve == null || valve.Closed) continue;
                            if (Mathf.Abs(head.FloorY - fire.Position.y) > 1.5f || head.transform.position.y - fire.Position.y < 1f) continue;
                            var flat = head.transform.position - fire.Position;
                            flat.y = 0;
                            var key = (fire, head);
                            if (!HeadSenses(fire, head, flat.magnitude)) { headSensing.Remove(key); continue; }
                            if (!headSensing.TryGetValue(key, out float since)) { headSensing[key] = Time.time; continue; }
                            if (Time.time - since >= HeadDelay(head)) ActivateHead(head, fire);
                        }
                    }
            }
            // 살수 중인 헤드 아래 3.2 m 안의 불은 물을 맞는다. 소화기 수준의 효과(초기 단계에서만 듣는다).
            foreach (var head in discharging)
            {
                if (!head.Discharging) continue;
                foreach (var fire in fires)
                {
                    if (fire.Extinguished || Mathf.Abs(head.FloorY - fire.Position.y) > 1.5f) continue;
                    var flat = head.transform.position - fire.Position;
                    flat.y = 0;
                    if (flat.magnitude <= 3.2f) fire.Suppress(.22f, dt);
                }
            }
            discharging.RemoveAll(h => !h.Discharging);
        }

        private void ActivateHead(SprinklerHeadPoint head, FireHazard fire)
        {
            head.Activate(art.Smoke, true);
            discharging.Add(head);
            var valve = ValveOf(head);
            log.Add("스프링클러 헤드 동작 · " + head.Equipment.Label + " (" + (valve != null ? valve.ZoneName : "") + ")");
            if (valve != null) { valve.SetFlowing(true); RaiseFlowAlarm(valve, fire); }
        }

        /// <summary>
        /// Water flows through an alarm valve: its flow switch shows the zone at the receiver and the sprinkler bell rings (NFTC 103 2.6), which starts the station alarm
        /// even when no detector tripped. The signal stays until the facility team shuts the zone's valve; only then can the receiver be reset.
        /// </summary>
        private void RaiseFlowAlarm(SprinklerValvePoint valve, Hazard cause)
        {
            if (!flowSignals.Add(valve.Key)) return;
            log.Add("스프링클러 유수검지장치 동작 · " + valve.ZoneName);
            if (alarm) return;
            alarm = true;
            Facilities.StationSignals.FireAlarm = true;
            Office("역무실입니다. 수신기 " + valve.ZoneName + " 스프링클러 유수검지장치 동작, " + cause.Where + "입니다. 현장 확인 바랍니다.");
            Know(cause, "스프링클러 동작 무전");
            crowd.Alert(cause.Position, 400, cause, null, "the fire alarm bell is ringing across the station");
            officeFollowUp = Time.time + 40;
        }

        /// <summary>The facility team (or the fire brigade) shuts the zone's control valve: no water, the tamper switch shows the zone out of service.</summary>
        private void CloseValve(SprinklerValvePoint valve, string by)
        {
            if (valve.Closed) return;
            valve.Close();
            if (headsByValve.TryGetValue(valve.Key, out var zone)) foreach (var head in zone) head.Stop();
            flowSignals.Remove(valve.Key);
            log.Add("스프링클러 급수 밸브 폐쇄 · " + valve.ZoneName + " (" + by + ")");
            if (alarm) Office("역무실입니다. 수신기에 " + valve.ZoneName + " 밸브 폐쇄(탬퍼) 표시가 들어왔습니다. 그 구역은 스프링클러가 작동하지 않습니다.");
        }

        // ── 원인: 배관 파열과 헤드 오작동 ──

        private Vector3 FloorUnder(Vector3 point, float floorY) => new Vector3(point.x, floorY, point.z);

        /// <summary>A spot people stand near: a waiting place within 7 m on the same floor (fixed for the whole shift).</summary>
        private bool NearWaitingPlace(Vector3 floor) =>
            world.Points.Nearest(PointKind.Wait, floor, w => Mathf.Abs(w.Position.y - floor.y) < 1.5f && Vector3.Distance(w.Position, floor) < 7f) != null;

        /// <summary>The pipes and low heads over places people stand, in this shift's rank order.</summary>
        private void RankPublicSprinklerSpots()
        {
            publicPipes = pipes.Select(p => (pipe: p, floor: FloorUnder(p.PointAt(.5f), p.FloorY))).Where(x => NearWaitingPlace(x.floor))
                .OrderBy(x => Rank(x.pipe.Equipment.Id)).ToList();
            publicHeads = sprinklerHeads.Where(h => h.MountHeight <= 5.2f && NearWaitingPlace(h.FloorPoint)).OrderBy(h => Rank(h.Equipment.Id)).ToList();
        }

        private void SprinklerOrigins(List<Transition> list)
        {
            if (publicPipes == null) RankPublicSprinklerSpots();
            if (leaks.Count == 0)
            {
                // 배관 이음이 터진다: 사람 있는 자리 위의 배관 가운데 이 근무의 순위가 앞선 셋(밸브가 닫혔거나 통제선 안인 곳은 뺀다).
                int taken = 0;
                foreach (var (pipe, floor) in publicPipes)
                {
                    if (taken == 3) break;
                    if (valves.TryGetValue(pipe.Valve, out var v) && v.Closed || world.IsClosed(floor, 3)) continue;
                    taken++;
                    var joint = pipe.JointNear(.2f + .6f * (Rank(pipe.Equipment.Id + "#joint") % 1000) / 1000f);
                    var p = pipe;
                    list.Add(new Transition
                    {
                        Key = "pipe_" + pipe.Equipment.Id, Kind = "water_leak", Origin = true,
                        Description = (pipe.Exposed ? "A grooved coupling of the exposed sprinkler pipe under the ceiling" : "A joint of a sprinkler pipe above the ceiling") + " at " + Place(FloorUnder(joint, pipe.FloorY)) + " (" + pipe.Equipment.Label + ") fails and water pours out.",
                        Levels = new List<string> { "water drips through a ceiling panel", "a steady stream of water pours from the ceiling", "water pours down and spreads across the floor", "the floor around floods", "the pipe gushes and water flows along the concourse" },
                        Apply = m => StartLeak(p, joint, m),
                    });
                }
            }
            // 헤드 오작동: 사람이 오가는 곳의 낮은 헤드가 부딪혀 깨지거나 유리관이 제풀에 터진다(이미 작동했거나 밸브가 닫힌 헤드, 통제선 안은 뺀다).
            int heads = 0;
            foreach (var head in publicHeads)
            {
                if (heads == 2) break;
                if (head.Activated || !valves.TryGetValue(head.Valve, out var v) || v.Closed || world.IsClosed(head.FloorPoint, 3)) continue;
                heads++;
                var h = head;
                bool knocked = Rank(head.Equipment.Id + "#cause") % 2 == 0;
                string cause = knocked ? "a worker's ladder or a passenger's luggage knocks the head" : "the glass bulb bursts by itself (a flaw or fatigue)";
                list.Add(new Transition
                {
                    Key = "sprinkler_" + head.Equipment.Id, Kind = "sprinkler_discharge", Origin = true,
                    Description = "The sprinkler head '" + head.Equipment.Label + "' at " + Place(head.FloorPoint) + " lets go although nothing is burning (" + cause + ") and sprays water; the flow switch starts the station alarm.",
                    Levels = new List<string> { "a dribble runs from the head", "the head sprays a few square metres", "a full spray soaks the floor and whoever stands there", "a strong spray covers the whole passage and the floor floods", "the head is torn off and gushes" },
                    Apply = m => StartDischarge(h, m, knocked ? "사다리·짐에 부딪혀 헤드 파손" : "유리관 자체 파열"),
                });
            }
        }

        private void StartLeak(SprinklerPipeLine pipe, Vector3 joint, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var floor = Floor(StationWorld.OnNavMesh(FloorUnder(joint, pipe.FloorY), 2f));
            // 노출 배관은 이음에서, 반자 안 배관은 반자 틈으로 새어 나온다.
            float ceiling = pipe.Exposed ? joint.y : Mathf.Min(joint.y, pipe.Equipment.Number("ceil", joint.y - .35f));
            var leak = new WaterLeakHazard("leak-" + ++serial, floor, ceiling, level, art, root, LeakSource.Pipe) { Where = world.Describe(floor), ValveKey = pipe.Valve, Component = pipe.Equipment.Label };
            leaks.Add(leak);
            CordonMarker(leak, leak.View, "천장 누수");
            Register(leak);
            // 물이 번진 바닥은 사람들이 피해서 자리를 잡는다.
            if (level >= 2) world.Closed.Add((floor, 1f + 1.6f * level, "누수"));
            log.Add("누수 · " + leak.Where + " — " + leak.Visible + " (" + pipe.Equipment.Label + ")");
        }

        private void StartDischarge(SprinklerHeadPoint head, float magnitude, string cause)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var floor = Floor(head.FloorPoint + Vector3.up * .2f);
            var valve = ValveOf(head);
            head.Activate(art.Smoke, false);
            discharging.Add(head);
            var leak = new WaterLeakHazard("leak-" + ++serial, floor, head.transform.position.y, level, art, root, LeakSource.Head) { Where = world.Describe(floor), ValveKey = head.Valve, Component = head.Equipment.Label };
            leaks.Add(leak);
            CordonMarker(leak, leak.View, "스프링클러 살수");
            Register(leak);
            if (level >= 2) world.Closed.Add((floor, 1f + 1.6f * level, "누수"));
            log.Add("스프링클러 오작동 살수 · " + leak.Where + " — " + leak.Visible + " (" + cause + ")");
            if (valve != null) { valve.SetFlowing(true); RaiseFlowAlarm(valve, leak); }
        }

        // ── 전개 ──

        private void SprinklerDevelopments(List<Transition> list)
        {
            foreach (var valve in valves.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
            {
                if (!valve.Flowing || valve.Closed) continue;
                var v = valve;
                // 물 때문에 난 사건(누수 위험물)은 자기 팀이 밸브를 잠근다. 불 때문에 나온 물은 불이 꺼지면 잠근다.
                if (leaks.Exists(l => l.Active && l.ValveKey == valve.Key)) continue;
                if (fires.Exists(f => !f.Extinguished && Mathf.Abs(f.Position.y - valve.transform.position.y) < 6f && Vector3.Distance(f.Position, valve.transform.position) < 60f)) continue;
                if (!arrived.Contains(Agency.Fire) && !arrived.Contains(Agency.Facility)) continue;
                if (!Ready("sprinkler_valve_" + valve.Key)) continue;
                string by = arrived.Contains(Agency.Facility) ? "시설 담당" : "소방대";
                list.Add(new Transition
                {
                    Key = "sprinkler_valve_" + valve.Key, Kind = "sprinkler_valve_shut",
                    Description = "The " + (by == "시설 담당" ? "facility team" : "fire brigade") + " shuts the control valve of " + valve.ZoneName + " to stop the water now that the fire is out",
                    Apply = _ => CloseValve(v, by),
                });
            }
        }

        private void SprinklerResolved(Hazard hazard)
        {
            // 시설 담당이 누수를 잡으면 그 구역의 급수 밸브가 잠겨 있다.
            if (hazard is WaterLeakHazard leak && !leak.Active && leak.ValveKey.Length > 0 && valves.TryGetValue(leak.ValveKey, out var valve)) CloseValve(valve, "시설 담당");
        }
    }
}
