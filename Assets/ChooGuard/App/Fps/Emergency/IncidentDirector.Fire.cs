using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Fire family: what can catch fire right now and how a fire develops. Ignition sources are things that are really
    /// there: a power bank in a passenger's bag (hall, platform, KTX car), a litter bin at a city entrance, the electrics of
    /// a ticket window or a shop fridge, the running gear under the KTX set at the platform (the kitchen fires of the food
    /// shops start from their real appliances: IncidentDirector.KitchenGas.cs). Detectors, the bell, smoke, rekindling and
    /// the office's own 119 call follow common rules.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<FireHazard> fires = new List<FireHazard>();
        private float fireOutAt = -1;

        /// <summary>
        /// The kitchen of a food shop of the twin (what each one cooks with is <see cref="StationKitchens"/>): its main appliance in
        /// English and Korean and whether it fries in oil; null for a shop without one.
        /// </summary>
        public static (string en, string ko, bool oil)? KitchenOf(StationPoints.Point shop)
        {
            var spec = StationKitchens.Of(shop.Label);
            return spec == null ? ((string, string, bool)?)null : (spec.English, spec.Korean, spec.Oil);
        }

        // ── 원인 ──

        private IEnumerable<Transition> FireOrigins(Pools pools)
        {
            foreach (var p in pools.Spread(p => p.CarriesPowerBank && Settled(p), 2)) yield return Overheat(p);
            var exit = world.Points.Of(PointKind.Exit).Where(e => e.Zone == "plaza" && !world.IsClosed(e.Position, 3)).OrderBy(_ => world.Random.Next()).FirstOrDefault();
            if (exit != null) yield return BinFire(exit);
            var counter = world.Points.Of(PointKind.Counter).OrderBy(_ => world.Random.Next()).FirstOrDefault();
            if (counter != null) yield return ElectricalFire(counter, "the ticket office equipment behind " + counter.Label, "매표창구 안 전기 설비");
            var store = world.Points.Of(PointKind.Shop).Where(s => KitchenOf(s) == null && !world.IsClosed(s.Position, 2)).OrderBy(_ => world.Random.Next()).FirstOrDefault();
            if (store != null) yield return ElectricalFire(store, "the refrigerated display case of " + store.Label, "매장 냉장 진열대 배선");
            if (Train != null && Train.AtPlatform && Train.Stage != TrainService.Phase.Opening)
            {
                var car = Train.Cars.Where(c => c.Entry.reachable).OrderBy(_ => world.Random.Next()).FirstOrDefault();
                if (car != null) yield return Underfloor(car);
            }
        }

        private Transition Overheat(Passenger owner) => new Transition
        {
            Key = "overheat_" + owner.Number, Kind = "overheat", Origin = true,
            Description = "The power bank in the bag of " + Profile(owner) + ", " + owner.Doing + " at " + Place(owner.transform.position) + ", starts to overheat.",
            Levels = new List<string> { "only a faint burning smell and a wisp of white smoke", "white smoke pouring out of the bag", "the bag bursts into small flames", "flames reach the seat or things around it", "a fierce fire with thick black smoke within seconds" },
            Apply = m => StartPowerBankFire(owner, m),
        };

        private Transition BinFire(StationPoints.Point exit) => new Transition
        {
            Key = "bin_" + exit.Id, Kind = "bin_fire", Origin = true,
            Description = "A cigarette butt thrown into the litter bin by the station entrance '" + exit.Label + "' (" + Place(exit.Position) + ") starts smouldering.",
            Levels = new List<string> { "a thin wisp of smoke from the bin", "thick smoke from the bin", "the rubbish in the bin bursts into flames", "flames leap out of the bin", "the bin burns fiercely and melts, black smoke drifts into the entrance" },
            Apply = m =>
            {
                var floor = Floor(StationWorld.OnNavMesh(exit.Position + (world.Points.Nearest(PointKind.Wait, exit.Position)?.Position - exit.Position ?? Vector3.forward).normalized * 3f, 2f));
                // 불은 휴지통 위(0.72 m)에서 오른다.
                var fire = Ignite(floor + Vector3.up * .72f, "입구 휴지통 담배꽁초", "휴지통", m, "");
                Props.LitterBin(fire.View.transform, floor, art);
            },
        };

        private Transition ElectricalFire(StationPoints.Point at, string what, string ko) => new Transition
        {
            Key = "electric_" + at.Id, Kind = "electrical_fire", Origin = true,
            Description = "An electrical fault in " + what + " (" + Place(at.Position) + ") starts to smoke.",
            Levels = new List<string> { "a smell of burning plastic", "grey smoke seeping out", "sparks and a small flame", "flames and acrid black smoke", "the wiring burns fiercely with thick toxic smoke" },
            // 사람들이 서는 자리보다 창구·진열대 쪽(바라보는 방향)으로 조금 들어간 곳이다.
            Apply = m => Ignite(Floor(at.Position + Quaternion.Euler(0, at.Yaw, 0) * Vector3.forward * .9f), ko + " 합선", ko, m, " 전기 화재입니다. 전원 차단을 요청하고 물을 쓰지 마십시오."),
        };

        private Transition Underfloor(TrainService.Car car) => new Transition
        {
            Key = "underfloor_" + car.Number, Kind = "underfloor_smoke", Origin = true,
            Description = "Smoke rises from under KTX " + car.Label + " standing at platform 5·6 (a brake or electrical fault in the running gear).",
            Levels = new List<string> { "a burning smell and light smoke from under the car", "white smoke pours out from under the car", "sparks and small flames under the car", "flames lick up the side of the car", "a fierce fire under the car with thick black smoke" },
            Apply = m => StartUnderfloorFire(car, m),
        };

        // ── 적용 ──

        private FireHazard Ignite(Vector3 spot, string source, string subject, float magnitude, string advice, Transform parent = null, bool aboard = false, string where = null, bool beneath = false)
        {
            var fire = new FireHazard("fire-" + ++serial, spot, source, subject, .05f + .35f * magnitude * magnitude, art, parent != null ? parent : root) { Where = where ?? world.Describe(spot), Advice = advice, Aboard = aboard, Beneath = beneath };
            fires.Add(fire);
            if (aboard) Train.Holds.Add("차내 화재");
            Register(fire);
            log.Add("화재 · " + fire.Where + " — " + source + " (" + fire.Visible + ")");
            return fire;
        }

        private void StartPowerBankFire(Passenger owner, float magnitude)
        {
            bool aboard = owner.Current == Passenger.Activity.InTrain && Train != null;
            Vector3 spot;
            if (aboard)
            {
                spot = owner.transform.position + owner.transform.right * .45f + owner.transform.forward * .35f;
                var car = Train.CarAt(owner.transform.position);
                spot.y = (car != null ? car.Entry.floor : .45f) + Train.Carrier.position.y;
            }
            else
            {
                var side = owner.transform.right * (world.Chance(.5f) ? .5f : -.5f) + owner.transform.forward * .3f;
                spot = Floor(StationWorld.OnNavMesh(owner.transform.position + side, 1f));
            }
            var fire = Ignite(spot, "승객 가방 속 보조배터리", "가방", magnitude, "", aboard ? Train.Carrier : null, aboard);
            owner.Notice(fire, false);
        }

        private void StartUnderfloorFire(TrainService.Car car, float magnitude)
        {
            var door = Train.World(car.DoorOutside);
            var inside = Train.World(car.DoorInside);
            var inward = inside - door;
            inward.y = 0;
            // 문 발판 바로 옆 차체 아래, 승강장 면보다 낮은 곳(대차·전기 장치 자리).
            var spot = door + inward.normalized * .9f + Vector3.down * .7f;
            // 차 안이 아니라 차 아래다: 이름은 승강장·차 기준으로, 불은 승강장 쪽에서 끈다.
            Ignite(spot, "KTX " + car.Label + " 차량 아래 대차·전기 장치", "KTX " + car.Label + " 차량 아래", magnitude, " 열차팀장에게 전원 차단과 승객 하차를 요청하겠습니다.", Train.Carrier, true,
                Train.Platform + " KTX " + car.Label + " 아래", true);
        }

        private void EmptyCar(TrainService.Car car, FireHazard fire)
        {
            int count = 0;
            foreach (var seat in car.Seats)
                if (seat.Taken != null) { seat.Taken.Notice(fire, true, "smoke is filling the car"); seat.Taken.BeginAlight(true); count++; }
            log.Add("KTX " + car.Label + " 승객 " + count + "명이 연기를 보고 승강장으로 내림");
        }

        /// <summary>
        /// A manual call point (발신기) was pressed: the receiver rings the station bell (the same alarm the detectors raise) and
        /// the office asks for the place to be checked. People react to the bell when a fire is actually burning nearby.
        /// </summary>
        private void CallPoint(Vector3 at, string label)
        {
            var where = world.Describe(at);
            log.Add("발신기 동작 · " + where);
            if (alarm) { Office("역무실입니다. " + where + " 발신기도 동작했습니다."); return; }
            alarm = true;
            Office("역무실입니다. " + where + " 발신기 동작. 화재 여부 현장 확인 바랍니다.");
            FireHazard nearest = null;
            float best = float.PositiveInfinity;
            foreach (var fire in fires)
            {
                if (fire.Extinguished) continue;
                float d = Vector3.Distance(fire.Position, at);
                if (d < best) { best = d; nearest = fire; }
            }
            if (nearest != null) crowd.Alert(nearest.Position, 400, nearest, null, "the fire alarm bell is ringing across the station");
        }

        private void TriggerAlarm(FireHazard fire)
        {
            if (alarm) return;
            alarm = true;
            // 수신기의 화재 신호: 피난 경로의 자동문과 개집표기가 연동해 열린다(자료 D1·G1, JEV 010).
            Facilities.StationSignals.FireAlarm = true;
            log.Add("자동화재탐지설비 동작 · 비상벨 · " + fire.Where);
            Office("역무실입니다. " + fire.Where + " 화재감지기 동작. 현장 확인 바랍니다.");
            Know(fire, "화재감지기 동작 무전");
            crowd.Alert(fire.Position, 400, fire, null, "the fire alarm bell is ringing across the station");
            if (!calledBy.ContainsKey(Agency.Fire)) officeFollowUp = Time.time + 30;
        }

        private void Rekindle(FireHazard old)
        {
            var fire = new FireHazard("fire-" + ++serial, old.Position, "꺼진 줄 알았던 " + old.Subject, old.Subject, .1f, art, old.Aboard ? Train.Carrier : root) { Where = old.Where, Advice = old.Advice, Aboard = old.Aboard };
            fires.Add(fire);
            HazardRegistry.Add(fire);
            all.Add(fire);
            fireOutAt = -1;
            if (fire.Aboard) Train.Holds.Add("차내 화재");
            log.Add("꺼졌던 " + old.Subject + "에서 다시 불꽃이 일었다 · " + fire.Where);
        }

        public void OnFireOut(FireHazard hazard, string by)
        {
            if (!hazard.Extinguished) return;
            HazardRegistry.Remove(hazard);
            if (!fires.Exists(f => !f.Extinguished))
            {
                fireOutAt = Time.time;
                if (Train != null && !fires.Any(f => f.Aboard && !f.Extinguished)) Train.Holds.Remove("차내 화재");
            }
            log.Add("불이 꺼졌다 (" + by + ") · " + hazard.Where);
        }

        // ── 전개 ──

        private IEnumerable<Transition> FireDevelopments()
        {
            foreach (var fire in fires)
            {
                var f = fire;
                if (!f.Extinguished)
                {
                    if (Ready("grow_" + f.Id) && f.Intensity < 1.2f)
                        yield return new Transition { Key = "grow_" + f.Id, Kind = "fire_grows", Description = "At " + f.Where + " the fire catches more of what is around it and the flames grow", Apply = _ => { f.Grow(.08f); log.Add("불이 커졌다 · " + f.Where); } };
                    if (Ready("smoke_" + f.Id) && f.SmokeRadius < 30)
                        yield return new Transition { Key = "smoke_" + f.Id, Kind = "smoke_spreads", Description = "Air movement pushes the smoke from " + f.Where + " further out", Apply = _ => { f.SpreadSmoke(5); log.Add("연기가 퍼졌다 · " + f.Where); } };
                    var close = NearestPerson(f.Position, 7, p => p.Current != Passenger.Activity.Evacuate && !p.Hurt && !p.Hostile);
                    if (close != null && Ready("overcome"))
                        yield return new Transition { Key = "overcome_" + close.Number, Kind = "overcome", Description = Profile(close) + " who stayed close to the fire at " + f.Where + " is overcome by the smoke", Apply = _ => close.Injure("화재 연기를 가까이서 들이마셨다", true) };
                    if (!alarm && !f.Aboard && f.Intensity > .25f)
                        yield return new Transition { Key = "alarm_" + f.Id, Kind = "detector_alarm", Description = "The automatic fire detector above " + f.Where + " triggers the alarm bell", Apply = _ => TriggerAlarm(f) };
                    if (f.Aboard && Ready("car_empties"))
                    {
                        var car = Train.CarAt(f.Position);
                        // 아직 자리에 앉아 있는 사람이 있을 때만(이미 비었으면 일어날 일이 없다).
                        if (car != null && car.Seats.Exists(seat => seat.Taken != null && !seat.Taken.Hurt))
                            yield return new Transition { Key = "car_empties_" + car.Number, Kind = "car_empties", Description = "Passengers in KTX " + car.Label + " see the smoke and hurry out onto platform 5·6", Apply = _ => EmptyCar(car, f) };
                    }
                }
                else if (fireOutAt > 0 && Time.time - fireOutAt < 40 && !arrived.Contains(Agency.Fire) && Ready("rekindle"))
                    yield return new Transition { Key = "rekindle_" + f.Id, Kind = "rekindle", Description = "Embers in what burnt at " + f.Where + " rekindle into small flames", Apply = _ => Rekindle(f) };
            }
        }

        // ── 규칙 ──

        private void FireTick(float dt)
        {
            // 꺼진 불은 등록에서 빠지지만 남은 연기는 계속 옅어져야 한다.
            foreach (var fire in fires) if (fire.Extinguished) fire.Tick(dt);
            if (Stage != Phase.Incident) return;
            foreach (var fire in fires)
            {
                if (fire.Extinguished) continue;
                if (!alarm && !fire.Aboard && fire.Intensity > .55f) TriggerAlarm(fire);
                // 아무도 신고하지 않으면 결국 승객이 직접 신고한다.
                if (Time.time - fire.StartedAt > 75 && CountAware(fire) >= 4) CitizenCall(null, fire);
            }
        }

        private float FireDanger(Vector3 eye)
        {
            float danger = 0;
            foreach (var fire in fires)
            {
                if (fire.Extinguished) continue;
                if (fire.InSmoke(eye)) danger = Mathf.Max(danger, .7f);
                if (Vector3.Distance(eye, fire.Position) < fire.DangerRadius + .8f) danger = 1;
            }
            return danger;
        }

        private IEnumerable<EmergencySession.RadioOption> FireRadio()
        {
            foreach (var fire in fires.Where(f => f.Extinguished && reported.Contains(f) && !reportedDone.Contains(f)).Take(1))
            {
                var f = fire;
                yield return Option("역무실 · 초기 진화 완료 보고", () =>
                {
                    reportedDone.Add(f);
                    Say("역무실, " + f.Where + " 화재 초기 진화했습니다. 연기 남아 있습니다.");
                    Office("역무실 수신. 소방대 도착하면 현장 인계 바랍니다.");
                    log.Add("역무실에 초기 진화 보고");
                });
            }
        }

        private void FireActions(BoardOverlay.Column column)
        {
            if (fires.Count > 0) column.Lines.Add((fires.TrueForAll(f => f.Extinguished) ? "● " : "○ ") + "초기 진화" + (log.SprayedSeconds > 0 ? " (분사 " + log.SprayedSeconds.ToString("0") + "초)" : ""));
        }
    }
}
