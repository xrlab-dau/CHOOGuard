using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Fire family: what can catch fire right now and how a fire develops. Ignition sources are things that are really
    /// there: a power bank in a passenger's bag (hall, platform, KTX car), the litter bins and recycling stations placed at the
    /// entrances and in the halls, the running gear under the KTX set at the platform. Fires in electrical equipment (distribution
    /// boards, vending machines, charging kiosks) come from IncidentDirector.ElectricPlaza.cs and the kitchen fires of the food
    /// shops from their real appliances (IncidentDirector.KitchenGas.cs); they are the same fire. Detectors, the bell, smoke,
    /// rekindling and the office's own 119 call follow common rules.
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

        private IEnumerable<Transition> FireOrigins(Roster roster) => Chain(
            Passengers(roster, p => p.CarriesPowerBank && Settled(p), Overheat),
            // 쓰레기통은 실제로 놓인 통이다(IncidentDirector.ElectricPlaza 의 배치): 출입구 곁 휴지통은 담배꽁초, 사람이 오가는 분리수거함은 버려진 배터리.
            Pieces("litter_bin", NearAnExit, bin => BinFire(bin, false)),
            Pieces("recycling_bin", bin => PeopleNear(bin.transform.position, 10f) > 0, bin => BinFire(bin, true)),
            UnderfloorOrigins());

        /// <summary>Running gear under a KTX car smokes: any car of the set standing at the platform that people can reach, once the doors have opened for the arrivals.</summary>
        private IEnumerable<Transition> UnderfloorOrigins()
        {
            if (Train == null) yield break;
            foreach (var smoke in Each(Train.Cars, car => Train.AtPlatform && Train.Stage != TrainService.Phase.Opening && car.Entry.reachable ? Underfloor(car) : null)) yield return smoke;
        }

        private static readonly List<string> OverheatLevels = new List<string> { "only a faint burning smell and a wisp of white smoke", "white smoke pouring out of the bag", "the bag bursts into small flames", "flames reach the seat or things around it", "a fierce fire with thick black smoke within seconds" };

        private Transition Overheat(Passenger owner) => new Transition
        {
            Key = "overheat_" + owner.Number, Kind = "overheat", Origin = true,
            Description = "The power bank in the bag of " + Profile(owner) + ", " + owner.Doing + " at " + Place(owner.transform.position) + ", starts to overheat.",
            Levels = OverheatLevels,
            Apply = m => StartPowerBankFire(owner, m),
        };

        /// <summary>A place where people step out to smoke and come back: a bin within 14 m of an entrance on its floor.</summary>
        private bool NearAnExit(StationEquipment bin)
        {
            var p = bin.transform.position;
            foreach (var exit in world.Points.Of(PointKind.Exit))
            {
                var d = exit.Position - p;
                if (Mathf.Abs(d.y) < 3f && new Vector2(d.x, d.z).magnitude < 14f) return true;
            }
            return false;
        }

        private static readonly List<string> BinBatteryLevels = new List<string> { "a hiss and a wisp of white smoke from the bin", "white smoke and a sharp chemical smell pouring out of the bin", "the battery vents a jet of flame that lights the paper and cups", "flames leap out of the bin, cans and bottles burst", "the bin burns fiercely, thick toxic smoke rolls along the ceiling" };
        private static readonly List<string> BinCigaretteLevels = new List<string> { "a thin wisp of smoke from the bin", "thick smoke from the bin", "the rubbish in the bin bursts into flames", "flames leap out of the bin", "the bin burns fiercely and melts, black smoke drifts through the entrance" };

        private Transition BinFire(StationEquipment bin, bool battery)
        {
            var exit = world.Points.Nearest(PointKind.Exit, bin.transform.position);
            string where = PlaceOf(bin);
            return new Transition
            {
                Key = (battery ? "bin_battery_" : "bin_") + bin.Id, Kind = battery ? "bin_battery_fire" : "bin_fire", Origin = true,
                Description = battery
                    ? "A worn-out power bank thrown away with drink cans and paper into recycling station " + bin.Id + " (" + where + ") swells and goes into thermal runaway. " + CrowdNote(bin.transform.position)
                    : "A cigarette butt, still lit, dropped into litter bin " + bin.Id + " (" + where + ")" + (exit != null ? " by someone coming back in from outside through the entrance '" + exit.Label + "'" : "") + " starts the rubbish smouldering. " + CrowdNote(bin.transform.position),
                Levels = battery ? BinBatteryLevels : BinCigaretteLevels,
                Apply = m =>
                {
                    if (!Still(StillIgnitable(bin), bin.Label + " " + bin.Id)) return;
                    StartEquipmentFire(bin, m, battery ? "버려진 보조배터리 열폭주" : "담배꽁초", battery
                        ? " 배터리가 섞인 불입니다. 꺼진 뒤에도 안에서 다시 타오를 수 있으니 물로 충분히 식히고 손대지 마십시오."
                        : null);
                },
            };
        }

        private static readonly List<string> UnderfloorLevels = new List<string> { "a burning smell and light smoke from under the car", "white smoke pours out from under the car", "sparks and small flames under the car", "flames lick up the side of the car", "a fierce fire under the car with thick black smoke" };

        private Transition Underfloor(TrainService.Car car) => new Transition
        {
            Key = "underfloor_" + car.Number, Kind = "underfloor_smoke", Origin = true,
            Description = "Smoke rises from under KTX " + car.Label + " standing at platform 5·6 (a brake or electrical fault in the running gear).",
            Levels = UnderfloorLevels,
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
