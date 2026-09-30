using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Structure, utilities and natural hazards: an earthquake (hanging boards fall, lifts stop), a hanging sign coming
    /// loose on its own, an elevator stopping between floors, a power cut, a detector tripping without a fire, a burst
    /// water pipe, a gas smell from a kitchen. Water can reach shop electrics and a gas leak can ignite: those chains are
    /// offered to JEV as developments like any other (research.md for the public guidance behind staff actions).
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private EarthquakeHazard quake;
        private readonly List<FallingBoard> fallen = new List<FallingBoard>();
        private readonly List<EmergencyArt.HangingItem> hanging = new List<EmergencyArt.HangingItem>();
        private readonly HashSet<FallingBoard> fallenKnown = new HashSet<FallingBoard>();
        private readonly Dictionary<FallingBoard, FallingObjectHazard> boardHazards = new Dictionary<FallingBoard, FallingObjectHazard>();
        private readonly List<ElevatorTrapHazard> elevatorTraps = new List<ElevatorTrapHazard>();
        private readonly List<WaterLeakHazard> leaks = new List<WaterLeakHazard>();
        /// <summary>Shops whose electrics leak water already shorted: that fire then grows or rekindles, it does not start again.</summary>
        private readonly HashSet<string> shorted = new HashSet<string>();
        private readonly List<GasLeakHazard> gasLeaks = new List<GasLeakHazard>();
        private PowerOutageHazard outage;
        private FalseAlarmHazard falseAlarm;
        private float lastFall = -100;
        private bool escalatorsStopped, damageReported;

        private Vector3 cameraRest;
        private bool wasShaking;

        private bool Shaking => quake != null && quake.Shaking;
        private int FallenCount => fallen.Count;

        /// <summary>Why a detector tripped with no fire, in words for JEV and for the facility team.</summary>
        private static readonly (string en, string ko)[] DetectorCauses =
        {
            ("dust from ceiling work", "천장 작업 먼지"), ("cooking fumes drifting from a food shop", "매장 조리 연기"), ("steam from a cleaning machine", "청소 장비 수증기"), ("a faulty detector head", "감지기 고장"),
        };

        private void BeginFacility()
        {
            hanging.AddRange(art.Hanging);
            cameraRest = cameraTransform.localPosition;
        }

        private void EndFacility()
        {
            // 정전 조명은 근무가 끝나면 원래대로 돌린다(다음 근무가 어둡게 시작하지 않게).
            StationLighting.Restore();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;
            bool shake = Shaking && !session.Player.IsPaused;
            if (shake)
            {
                float t = Time.time * 13f, s = quake.Strength;
                cameraTransform.localPosition = cameraRest + new Vector3(Mathf.PerlinNoise(t, 0) - .5f, Mathf.PerlinNoise(0, t) - .5f, Mathf.PerlinNoise(t, t) - .5f) * .07f * s;
                // 시점 기울기는 매 프레임 조종기의 상하 각도에서 새로 만든다(누적되지 않게).
                cameraTransform.localRotation = Quaternion.Euler(session.Player.PitchDegrees, 0, (Mathf.PerlinNoise(t * .7f, 3) - .5f) * 2.4f * s);
            }
            else if (wasShaking)
            {
                cameraTransform.localPosition = cameraRest;
                cameraTransform.localRotation = Quaternion.Euler(session.Player.PitchDegrees, 0, 0);
            }
            wasShaking = shake;
        }

        // ── 원인 ──

        private IEnumerable<Transition> FacilityOrigins(Pools pools)
        {
            if (quake == null) yield return QuakeOrigin();
            if (hanging.Count > 0 && quake == null) yield return LooseSign();
            foreach (var elevator in world.Elevators)
            {
                elevator.Inside.RemoveAll(b => b == null);
                if (elevator.Running && elevator.Inside.Count > 0 && !elevatorTraps.Exists(t => t.Active && t.Elevator == elevator)) yield return ElevatorStops(elevator);
            }
            if (outage == null) yield return PowerCut();
            if (falseAlarm == null && !alarm)
            {
                var spot = world.Points.Of(PointKind.Wait).Where(w => w.Zone != "plaza" && w.Zone != "skyplaza" && w.Zone != "tracks").OrderBy(w => Rank(w.Id)).FirstOrDefault();
                if (spot != null) yield return DetectorTrips(spot);
            }
            var leakAt = world.Points.Of(PointKind.Wait).Where(w => w.Zone != "plaza" && w.Zone != "skyplaza" && !world.IsClosed(w.Position, 3)).OrderBy(w => Rank(w.Id)).FirstOrDefault();
            if (leakAt != null && leaks.Count == 0) yield return PipeBursts(leakAt);
            var kitchen = world.Points.Of(PointKind.Shop).Where(s => KitchenOf(s)?.ko == "가스레인지" && !gasLeaks.Exists(g => g.Shop == s.Label)).OrderBy(s => Rank(s.Id)).FirstOrDefault();
            if (kitchen != null) yield return GasSmell(kitchen);
        }

        private Transition QuakeOrigin() => new Transition
        {
            Key = "quake", Kind = "quake", Origin = true,
            Description = "An earthquake strikes; the whole station starts to shake.",
            Levels = new List<string> { "a light tremor", "moderate shaking", "strong shaking", "very strong shaking", "violent shaking" },
            Apply = StartQuake,
        };

        private Transition LooseSign() => new Transition
        {
            Key = "loose_sign", Kind = "falling_object", Origin = true,
            Description = "A hanging sign or ceiling panel above the concourse comes loose from its fixings and falls.",
            Levels = new List<string> { "it falls onto an empty spot", "it falls near one or two people", "it falls over a walkway", "it falls over a busy spot", "it falls onto the busiest spot below" },
            Apply = StartLooseSign,
        };

        private Transition ElevatorStops(Elevator elevator) => new Transition
        {
            Key = "elevator_" + elevator.Entry.id, Kind = "elevator_trap", Origin = true,
            Description = "The " + elevator.Label + " carrying " + elevator.Inside.Count + " people stops between floors with a fault.",
            Levels = new List<string> { "it stops briefly; the lights stay on", "it is stuck; the people inside are calm", "it is stuck; the people inside press the alarm again and again", "it is stuck; someone inside panics and bangs on the door", "it is stuck in a crowded car; someone inside feels faint" },
            Apply = m => StartElevatorTrap(elevator, m),
        };

        private Transition PowerCut() => new Transition
        {
            Key = "power_cut", Kind = "power_outage", Origin = true,
            Description = "The station loses mains power (a fault at the substation or in the grid).",
            Levels = new List<string> { "the lights flicker and come back within seconds", "part of the lighting goes out for under a minute", "the whole station goes dark on emergency lighting for a minute or two", "a long cut: lights out and escalators and elevators stop", "a long cut: lights out, lifts stop and people are trapped in an elevator" },
            Apply = StartOutage,
        };

        private Transition DetectorTrips(StationPoints.Point spot)
        {
            var cause = DetectorCauses[Rank(spot.Id) % DetectorCauses.Length];
            return new Transition
            {
                Key = "detector_" + spot.Id, Kind = "false_alarm", Origin = true,
                Description = "A smoke detector above " + Place(spot.Position) + " trips although nothing is burning (" + cause.en + ").",
                Apply = _ => StartFalseAlarm(spot.Position, cause.ko),
            };
        }

        private Transition PipeBursts(StationPoints.Point at) => new Transition
        {
            Key = "pipe_" + at.Id, Kind = "water_leak", Origin = true,
            Description = "A water pipe above the ceiling at " + Place(at.Position) + " bursts.",
            Levels = new List<string> { "water drips through a ceiling panel", "a steady stream of water pours from the ceiling", "water pours down and spreads across the floor", "the floor around floods", "the pipe gushes and water flows along the concourse" },
            Apply = m => StartLeak(at.Position, m),
        };

        private Transition GasSmell(StationPoints.Point shop) => new Transition
        {
            Key = "gas_" + shop.Id, Kind = "gas_leak", Origin = true,
            Description = "A gas hose in the kitchen of the food shop '" + shop.Label + "' (" + Place(shop.Position) + ") works loose and gas starts to leak.",
            Levels = new List<string> { "a faint smell of gas near the kitchen", "a clear smell of gas in the shop", "the smell spreads into the passage in front of the shop", "a strong smell; people nearby get headaches", "a hissing leak; the smell spreads across the floor" },
            Apply = m => StartGas(shop, m),
        };

        // ── 적용 ──

        private void StartQuake(float magnitude)
        {
            quake = new EarthquakeHazard("quake-" + ++serial, new Vector3(64, 7, -2), 10 + 18 * magnitude, .45f + .55f * magnitude) { Where = "역 전체" };
            Register(quake);
            log.Add("지진 · 역사 전체가 흔들리기 시작 (세기 " + quake.Strength.ToString("0.0") + ")");
            Know(quake, "흔들림을 직접 느낌");
            officeFollowUp = Time.time + 150;
            // 흔들리는 동안 열차는 출발하지 않는다.
            if (Train != null) Train.Holds.Add("지진");
        }

        private void DropBoard()
        {
            // 사람이 많은 곳 위의 안내판부터 흔들린다(무게·고정 상태는 알 수 없으므로 사람 밀도로만 고른다).
            EmergencyArt.HangingItem pick = null;
            int best = -1;
            foreach (var item in hanging)
            {
                int people = crowd.CountNear(new Vector3(item.Centre.x, 7, item.Centre.z), 8, null) + world.Random.Next(3);
                if (people > best) { best = people; pick = item; }
            }
            if (pick != null) Drop(pick);
        }

        private FallingBoard Drop(EmergencyArt.HangingItem item)
        {
            hanging.Remove(item);
            lastFall = Time.time;
            var below = item.Centre + Vector3.down * (item.Size.y * .5f + .3f);
            float floorY = Physics.Raycast(below, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y + .02f : 7.02f;
            var board = FallingBoard.Drop(item, root, floorY, world.Random);
            board.OnLanded += Landed;
            fallen.Add(board);
            return board;
        }

        /// <summary>A sign comes loose on its own: which one is JEV's magnitude — low over an empty spot, high over a crowd.</summary>
        private void StartLooseSign(float magnitude)
        {
            var sorted = hanging.OrderBy(item => crowd.CountNear(new Vector3(item.Centre.x, item.Centre.y - 4, item.Centre.z), 6, null)).ToList();
            if (sorted.Count == 0) return;
            var pick = sorted[Mathf.Clamp(Mathf.RoundToInt(magnitude * (sorted.Count - 1)), 0, sorted.Count - 1)];
            var board = Drop(pick);
            var hazard = new FallingObjectHazard("falling-" + ++serial, board) { Where = world.Describe(pick.Centre) };
            boardHazards[board] = hazard;
            Register(hazard);
            log.Add("천장 " + pick.Label + " 고정이 풀려 떨어짐 · " + hazard.Where);
        }

        private void Landed(FallingBoard board)
        {
            float radius = Mathf.Max(board.Item.Size.x, board.Item.Size.z) * .5f + .4f;
            string where = world.Describe(board.Impact);
            log.Add(board.Item.Label + " 낙하 · " + where);
            boardHazards.TryGetValue(board, out var hazard);
            if (hazard != null) { hazard.Landed(board.Impact); hazard.Where = where; HazardRegistry.Add(hazard); }
            foreach (var person in crowd.People.ToArray())
            {
                var d = person.transform.position - board.Impact;
                if (Mathf.Abs(d.y) > 3) continue;
                d.y = 0;
                if (d.magnitude < radius && !person.Hurt) { person.Injure(board.Item.Label + "에 맞았다"); if (hazard != null) hazard.Hit++; }
                else if (d.magnitude < radius + 6)
                {
                    if (hazard != null) person.Notice(hazard, false);
                    else if (quake != null) person.Notice(quake, true);
                }
            }
            world.Closed.Add((board.Impact, radius + .5f, board.Item.Label));
            if (hazard != null) { CordonMarker(hazard, board.gameObject, "떨어진 " + board.Item.Label); return; }
            var marker = board.gameObject.AddComponent<HazardMarker>();
            marker.Name = "떨어진 " + board.Item.Label;
            marker.Prompt = () => world.Closed.Exists(z => z.label == "통제선 · " + board.Item.Label) ? "" : "주변 접근 통제";
            marker.Act = responder => PlaceCordon(null, board.Impact, radius + 1.5f, board.Item.Label);
        }

        /// <summary>Escalators and elevators stop (a quake sensor, a power cut); anyone in a stopped elevator car is trapped.</summary>
        private void StopLifts(string cause)
        {
            escalatorsStopped = true;
            foreach (var escalator in world.Escalators) escalator.Stop(cause);
            int trapped = 0;
            foreach (var elevator in world.Elevators)
            {
                elevator.Stop();
                elevator.Inside.RemoveAll(b => b == null);
                if (elevator.Inside.Count == 0) continue;
                trapped += elevator.Inside.Count;
                if (!elevatorTraps.Exists(t => t.Active && t.Elevator == elevator)) Trap(elevator, 2, false);
            }
            log.Add("에스컬레이터·엘리베이터가 " + cause + KoreanText.Instrument(cause) + " 정지" + (trapped > 0 ? " · 엘리베이터 안에 " + trapped + "명" : ""));
            Office("역무실입니다. 에스컬레이터와 엘리베이터 전 대 " + cause + KoreanText.Instrument(cause) + " 정지했습니다." + (trapped > 0 ? " 엘리베이터 안에 승객이 갇혀 있습니다." : ""));
        }

        private void StartElevatorTrap(Elevator elevator, float magnitude)
        {
            elevator.Stop();
            Trap(elevator, Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4), true);
        }

        private ElevatorTrapHazard Trap(Elevator elevator, int level, bool call)
        {
            var landing = elevator.Door(0);
            var hazard = new ElevatorTrapHazard("elevator-" + ++serial, elevator, landing, Mathf.Max(1, elevator.Inside.Count), level) { Where = elevator.Label };
            elevatorTraps.Add(hazard);
            var view = new GameObject("엘리베이터 비상통화 · " + elevator.Label, typeof(SphereCollider));
            view.transform.SetParent(root, false);
            view.transform.position = landing + Vector3.up * 1.3f;
            view.GetComponent<SphereCollider>().radius = .45f;
            var marker = view.AddComponent<HazardMarker>();
            marker.Hazard = hazard;
            marker.Name = elevator.Label + " 호출 버튼 · 비상통화";
            marker.Prompt = () => hazard.Active && !hazard.Reassured ? "갇힌 승객 안심시키기" : "";
            marker.Act = responder =>
            {
                hazard.Reassured = true;
                hazard.Calm();
                log.Add("역무원이 갇힌 승객을 안심시킴 · " + elevator.Label);
                return "\"곧 구조됩니다. 문을 억지로 열지 말고 기다려 주세요.\" 갇힌 승객을 안심시켰습니다";
            };
            Register(hazard);
            if (call)
            {
                // 카 안의 비상통화는 역무실로 온다. 역무실이 역무원에게 알린다.
                Office("역무실입니다. " + elevator.Label + " 비상통화 호출입니다. 층 사이에 멈춰 " + hazard.Riders + "명 갇혔습니다. 현장 확인 바랍니다.");
                Know(hazard, "역무실 무전(비상통화)");
            }
            log.Add("엘리베이터 갇힘 · " + elevator.Label + " — " + hazard.Riders + "명");
            return hazard;
        }

        private void StartOutage(float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var office = world.Points.Of(PointKind.Office).FirstOrDefault();
            outage = new PowerOutageHazard("outage-" + ++serial, office != null ? office.Position : PlayerPosition, level);
            Register(outage);
            StationLighting.Dim(root, world.Points.Zones);
            if (outage.StopsLifts) StopLifts("정전");
            Office(level == 0 ? "역무실입니다. 방금 순간 정전이 있었습니다. 설비에 이상 없는지 확인 바랍니다." : "역무실입니다. 정전 발생했습니다. 비상조명 켜졌습니다. 엘리베이터 갇힘과 승객 안전 확인 바랍니다.");
            Know(outage, "정전을 직접 겪음");
            officeFollowUp = Time.time + 90;
            log.Add("정전 · " + outage.Visible);
        }

        private void RestorePower(string how)
        {
            if (outage == null) return;
            outage.Restore();
            HazardRegistry.Remove(outage);
            StationLighting.Restore();
            foreach (var elevator in world.Elevators) if (!elevatorTraps.Exists(t => t.Active && t.Elevator == elevator)) elevator.Resume();
            log.Add("전기가 다시 들어옴 (" + how + ")");
            if (escalatorsStopped && quake == null) StartCoroutine(RestartEscalators(20));
        }

        /// <summary>After a power cut the facility team restarts the escalators once they are checked (steps clear, nobody fallen).</summary>
        private IEnumerator RestartEscalators(float delay)
        {
            yield return new WaitForSeconds(delay);
            int restarted = 0;
            foreach (var escalator in world.Escalators)
                if (!escalator.Running && escalator.StoppedBy == "정전" && escalator.Restart(out _)) restarted++;
            if (restarted > 0) { escalatorsStopped = world.Escalators.Exists(e => !e.Running); log.Add("정전 뒤 점검한 에스컬레이터 " + restarted + "대 재가동"); }
        }

        private void StartFalseAlarm(Vector3 detector, string cause)
        {
            falseAlarm = new FalseAlarmHazard("detector-" + ++serial, detector, cause) { Where = world.Describe(detector) };
            Register(falseAlarm);
            alarm = true;
            // 수신기의 화재 신호: 불이 없어도 비상벨과 연동 문은 똑같이 동작한다.
            Facilities.StationSignals.FireAlarm = true;
            crowd.Alert(detector, 400, falseAlarm, null, "the fire alarm bell is ringing across the station");
            Office("역무실입니다. " + falseAlarm.Where + " 화재감지기 동작. 현장 확인 바랍니다.");
            Know(falseAlarm, "화재감지기 동작 무전");
            officeFollowUp = Time.time + 40;
            log.Add("자동화재탐지설비 동작 · 비상벨 · " + falseAlarm.Where);
        }

        /// <summary>The receiver is reset: the bell stops and the interlocked doors go back to normal.</summary>
        private void ResetReceiver(string how)
        {
            if (!alarm) return;
            alarm = false;
            Facilities.StationSignals.FireAlarm = false;
            log.Add("수신기 복구 · 비상벨 멈춤 (" + how + ")");
        }

        private void StartLeak(Vector3 at, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var floor = Floor(StationWorld.OnNavMesh(at, 2f));
            float ceiling = Physics.Raycast(floor + Vector3.up * 1.5f, Vector3.up, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : floor.y + 4.5f;
            var leak = new WaterLeakHazard("leak-" + ++serial, floor, ceiling, level, art, root) { Where = world.Describe(floor) };
            leaks.Add(leak);
            CordonMarker(leak, leak.View, "천장 누수");
            Register(leak);
            // 물이 번진 바닥은 사람들이 피해서 자리를 잡는다.
            if (level >= 2) world.Closed.Add((floor, 1f + 1.6f * level, "누수"));
            log.Add("누수 · " + leak.Where + " — " + leak.Visible);
        }

        private void StartGas(StationPoints.Point shop, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var gas = new GasLeakHazard("gas-" + ++serial, shop.Position, shop.Label, level) { Where = world.Describe(shop.Position) };
            gasLeaks.Add(gas);
            Register(gas);
            log.Add("가스 누출 · " + gas.Where + " — " + gas.Visible);
        }

        // ── 전개 ──

        private IEnumerable<Transition> FacilityDevelopments()
        {
            if (Shaking && hanging.Count > 0 && fallen.Count < 3 && Time.time - lastFall > 5)
                yield return new Transition { Key = "board_falls", Kind = "board_falls", Description = "A hanging board above a crowded part of the concourse comes loose and falls", Apply = _ => DropBoard() };
            if (quake != null && !quake.Shaking)
            {
                if (quake.Aftershocks == 0 && Time.time > quake.ShakeUntil + 40)
                    yield return new Transition
                    {
                        Key = "aftershock", Kind = "aftershock", Description = "An aftershock shakes the station briefly",
                        Apply = _ =>
                        {
                            quake.Aftershock(world.Range(6, 10), world.Range(.4f, .6f));
                            foreach (var person in crowd.People) person.Noticed.Remove(quake);
                            log.Add("여진");
                        },
                    };
                if (!escalatorsStopped)
                    yield return new Transition { Key = "escalators_stop", Kind = "escalators_stop", Description = "The escalators and elevators halt after the quake; people crowd at the landings and a few are left inside elevators", Apply = _ => StopLifts("지진 감지") };
            }
            foreach (var trap in elevatorTraps)
            {
                if (!trap.Active) continue;
                var t = trap;
                if (t.Level < 4 && Ready("elevator_worse_" + t.Id))
                    yield return new Transition { Key = "elevator_worse_" + t.Id, Kind = "elevator_panic", Description = "The people stuck in the " + t.Elevator.Label + " grow more distressed", Apply = _ => { t.Worsen(); log.Add("갇힌 승객이 더 불안해함 · " + t.Elevator.Label); } };
            }
            if (outage != null && outage.Active)
            {
                if (!outage.StopsLifts && !escalatorsStopped)
                    yield return new Transition { Key = "outage_lifts", Kind = "outage_lifts_stop", Description = "The power cut also stops the escalators and elevators", Apply = _ => StopLifts("정전") };
                if (Ready("outage_back"))
                    yield return new Transition { Key = "outage_back", Kind = "power_returns", Description = "Mains power comes back by itself", Apply = _ => RestorePower("저절로 복구") };
                var faller = NearestPerson(PlayerPosition, 40, p => !p.Hurt && !p.Hostile && p.Current == Passenger.Activity.Walk && OnStairs(p));
                if (faller != null && Ready("dark_fall"))
                    yield return new Transition { Key = "dark_fall_" + faller.Number, Kind = "stairs_fall", Description = Profile(faller) + " misses a step on the dark stairs and falls", Apply = m => StartStairsFall(faller, .5f) };
            }
            if (falseAlarm != null && falseAlarm.Active && !falseAlarm.Cleared && Ready("alarm_leaving"))
                yield return new Transition { Key = "alarm_leaving", Kind = "alarm_crowd_leaves", Description = "People near the ringing bell start leaving the station on their own", Apply = _ => { int n = crowd.Announce(falseAlarm.Position, 40, false); log.Add("비상벨을 듣고 스스로 나가는 승객 " + n + "명"); } };
            foreach (var leak in leaks)
            {
                if (!leak.Active) continue;
                var l = leak;
                if (l.Level < 4 && Ready("leak_worse_" + l.Id))
                    yield return new Transition { Key = "leak_worse_" + l.Id, Kind = "leak_worsens", Description = "The burst pipe at " + l.Where + " gushes harder", Apply = _ => { l.Worsen(); log.Add("누수가 심해짐 · " + l.Where); } };
                var walker = NearestPerson(l.Position, l.Radius + 2, p => !p.Hurt && !p.Hostile && p.Current != Passenger.Activity.InTrain);
                if (walker != null && Ready("slips"))
                    yield return new Transition { Key = "slip_" + walker.Number, Kind = "slips", Description = Profile(walker) + " slips on the wet floor at " + l.Where + " and falls", Apply = _ => Slip(walker, l) };
                var shop = world.Points.Nearest(PointKind.Shop, l.Position, s => Vector3.Distance(s.Position, l.Position) < 12);
                if (shop != null && l.Level >= 2 && !shorted.Contains(shop.Id) && Ready("water_electrics"))
                    yield return new Transition { Key = "water_electrics_" + shop.Id, Kind = "electrical_fire", Description = "Water from the burst pipe reaches the electrics of '" + shop.Label + "' and sparks fly", Levels = new List<string> { "sparks and a burning smell", "grey smoke from the socket", "small flames at the socket", "flames and acrid smoke", "the wiring burns fiercely" }, Apply = m => { shorted.Add(shop.Id); Ignite(Floor(shop.Position), shop.Label + " 전기 설비(누수)", "젖은 전기 설비", m, " 전기 화재입니다. 전원 차단을 요청하고 물을 쓰지 마십시오."); } };
            }
            foreach (var gas in gasLeaks)
            {
                if (!gas.Active) continue;
                var g = gas;
                if (g.Level < 4 && Ready("gas_worse_" + g.Id))
                    yield return new Transition { Key = "gas_worse_" + g.Id, Kind = "gas_spreads", Description = "The gas smell from '" + g.Shop + "' grows stronger and spreads", Apply = _ => { g.Worsen(); log.Add("가스 냄새가 짙어짐 · " + g.Where); } };
                if (Ready("gas_valve"))
                    yield return new Transition { Key = "gas_valve_" + g.Id, Kind = "gas_valve_shut", Description = "A worker at '" + g.Shop + "' finds the loose hose and shuts the gas valve", Apply = _ => { g.ShutOff(); HazardRegistry.Remove(g); log.Add(g.Shop + " 직원이 가스 밸브를 잠금"); } };
                var dizzy = NearestPerson(g.Position, g.DangerRadius + 2, p => !p.Hurt && !p.Hostile);
                if (dizzy != null && g.Level >= 2 && Ready("gas_dizzy"))
                    yield return new Transition { Key = "gas_dizzy_" + dizzy.Number, Kind = "gas_dizzy", Description = Profile(dizzy) + " near '" + g.Shop + "' feels dizzy from the gas and sits down", Apply = _ => dizzy.Injure("가스 냄새를 맡고 어지러워 주저앉음") };
                if (g.Level >= 3 && Ready("gas_ignites"))
                    yield return new Transition { Key = "gas_ignites_" + g.Id, Kind = "gas_ignites", Description = "Someone in '" + g.Shop + "' switches on an appliance and the gas ignites with a flash", Levels = new List<string> { "a brief flash that goes out", "a flash and a small fire in the kitchen", "a fireball in the kitchen; a worker is burnt", "a fireball that blows out the shop front", "a fierce fire after the flash" }, Apply = m => GasIgnites(g, m) };
            }
        }

        private void Slip(Passenger walker, WaterLeakHazard leak)
        {
            var casualty = AddCasualty(walker, "미끄러짐 부상", "젖은 바닥에 미끄러진", "slipped on the wet floor", "젖은 바닥에 미끄러져 넘어진 승객", -1);
            casualty.Told = "에서 사람이 물에 미끄러져 넘어졌어요!";
            walker.Injure("젖은 바닥에 미끄러져 넘어짐");
            Register(casualty);
            log.Add("젖은 바닥에서 승객이 미끄러짐 · " + leak.Where);
        }

        private void GasIgnites(GasLeakHazard gas, float magnitude)
        {
            gas.ShutOff();
            HazardRegistry.Remove(gas);
            log.Add("가스에 불이 붙음 · " + gas.Where);
            if (magnitude < .2f) { log.Add("순간 불꽃이 일었다 꺼짐"); return; }
            var fire = Ignite(Floor(gas.Position), gas.Shop + " 주방 가스", gas.Shop + " 주방", magnitude, " 가스 밸브를 잠그게 하고 불씨가 번지지 않게 주변을 비우십시오.");
            if (magnitude >= .5f)
            {
                var burnt = NearestPerson(gas.Position, 4, p => !p.Hurt && !p.Hostile);
                if (burnt != null) burnt.Injure("가스 불꽃에 화상을 입음");
            }
            crowd.Alert(fire.Position, 25, fire, null, "there was a bang and a flash of fire");
        }

        // ── 규칙 ──

        private void FacilityTick()
        {
            crowd.Shaking = Shaking;
            if (outage != null && outage.Active && Time.time > outage.Until) RestorePower("저절로 복구");
            if (Stage != Phase.Incident) return;
            foreach (var gas in gasLeaks)
                if (gas.Active && Time.time - gas.StartedAt > 60 && CountAware(gas) >= 2) CitizenCall(null, gas);
        }

        private void OfficeFollowUp()
        {
            if (fires.Exists(f => !f.Extinguished) && !calledBy.ContainsKey(Agency.Fire))
            {
                Call(Agency.Fire, "역무실(감지기 동작 확인)");
                Office("역무실입니다. 화재감지기 동작 확인되어 119 신고했습니다.");
                return;
            }
            if (falseAlarm != null && !falseAlarm.Cleared && !calledBy.ContainsKey(Agency.Fire))
            {
                Call(Agency.Fire, "역무실(감지기 동작 확인)");
                Office("역무실입니다. 감지기 동작 뒤 확인 보고가 없어 119에 신고했습니다.");
                return;
            }
            if ((quake != null || outage != null || falseAlarm != null) && !calledBy.ContainsKey(Agency.Facility)) Call(Agency.Facility, "역무실 자체 판단");
        }

        private void LookAroundFacility(Vector3 eye, Vector3 forward)
        {
            foreach (var board in fallen)
                if (board.Landed && !fallenKnown.Contains(board) && Vector3.Distance(eye, board.Impact) < 25 && Vector3.Angle(forward, board.Impact - eye) < 60)
                {
                    fallenKnown.Add(board);
                    if (quake != null) quake.FallenKnown = fallenKnown.Count;
                    log.Once("fallen-" + board.GetInstanceID(), "역무원이 떨어진 " + board.Item.Label + KoreanText.Object(board.Item.Label) + " 확인");
                    session.SetMarker("fallen-" + board.GetInstanceID(), board.Impact, MarkerKind.Incident, "낙하물");
                }
            // 감지기 자리에 가 보면 불이 없다는 것을 안다(비화재보).
            if (falseAlarm != null && !falseAlarm.Checked && Vector3.Distance(eye, falseAlarm.Position) < 7)
            {
                falseAlarm.Check();
                log.Add("역무원이 감지기 주변 확인 · 불이나 연기 없음 · " + falseAlarm.Where);
                session.Hud.Toast("불이나 연기가 없습니다 · 비화재보로 보입니다 · 역무실에 보고하세요", 6f);
            }
        }

        private bool FacilityScene(out Vector3 scene)
        {
            scene = default;
            var target = TargetOf(Agency.Facility);
            if (target is EarthquakeHazard) { if (fallen.Count == 0) return false; scene = fallen[0].Impact; return true; }
            if (target is CollapseHazard casualty && casualty.Escalator != null) { scene = casualty.Escalator.Middle; return true; }
            return false;
        }

        private float FacilityDanger(Vector3 eye)
        {
            float danger = Shaking ? .35f : 0;
            foreach (var gas in gasLeaks)
                if (gas.Active && Vector3.Distance(eye, gas.Position) < gas.DangerRadius + 1) danger = Mathf.Max(danger, .5f);
            return danger;
        }

        private void FacilityReported(Hazard hazard)
        {
            if (hazard is FalseAlarmHazard alarmHazard)
            {
                alarmHazard.Clear();
                ResetReceiver("역무원 비화재 확인");
            }
        }

        private void FacilityResolved(Hazard hazard)
        {
            switch (hazard)
            {
                case PowerOutageHazard _: RestorePower("시설 담당 복구"); break;
                case FalseAlarmHazard alarmHazard when alarmHazard.Cleared:
                    ResetReceiver("소방대 비화재 확인");
                    // 감지기 점검과 경보 종료는 시설 담당이 한다: 역무원 보고 없이 소방대가 확인한 경우에도 역무실이 부른다.
                    if (!calledBy.ContainsKey(Agency.Facility)) Call(Agency.Facility, "역무실(비화재 확인 뒤 감지기 점검)");
                    break;
                case ElevatorTrapHazard trap when trap.Level >= 4:
                {
                    // 가장 힘들어하던 사람은 내린 뒤 구급대 처치를 받는다.
                    var body = trap.Elevator.Inside.FirstOrDefault(b => b != null);
                    var person = body != null ? body.GetComponent<Passenger>() : null;
                    if (person != null) StartCoroutine(FaintAfter(person, 6));
                    break;
                }
            }
        }

        private IEnumerator FaintAfter(Passenger person, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (person == null || person.Hurt) yield break;
            var casualty = AddCasualty(person, "응급 환자", "엘리베이터에 갇혔다 쓰러진", "fainted after being trapped in the elevator", "엘리베이터에서 내린 뒤 어지러워 주저앉음", 1);
            person.Injure("엘리베이터에 갇혔다 내린 뒤 어지러워 주저앉음");
            Register(casualty);
        }

        private IEnumerable<EmergencySession.RadioOption> FacilityRadio()
        {
            if (quake != null && fallenKnown.Count > 0 && !damageReported)
                yield return Option("역무실 · 천장 낙하물 피해 보고", () =>
                {
                    damageReported = true;
                    Say("역무실, 천장 안내판 " + fallenKnown.Count + "곳 낙하했습니다.");
                    Office("역무실 수신. 시설 담당 보내겠습니다. 낙하 지점 접근 통제 바랍니다.");
                    log.Add("역무실에 낙하물 피해 보고");
                    Call(Agency.Facility, "역무원 보고");
                });
        }

        private void FacilityLines(BoardOverlay.Column column)
        {
            if (outage != null && outage.Active) column.Lines.Add("정전 · 비상조명");
            if (escalatorsStopped) column.Lines.Add("에스컬레이터·엘리베이터 정지" + (escalatorsClosed ? " · 이용 통제" : ""));
            if (alarm) column.Lines.Add("비상벨 울림");
        }

        private void FacilityActions(BoardOverlay.Column column)
        {
            if (falseAlarm != null) column.Lines.Add((falseAlarm.Cleared ? "● " : falseAlarm.Checked ? "◐ " : "○ ") + "화재 여부 확인·수신기 복구");
            foreach (var trap in elevatorTraps) column.Lines.Add((trap.Reassured ? "● " : "○ ") + "갇힌 승객 안심시키기 · " + trap.Elevator.Label);
        }
    }
}
