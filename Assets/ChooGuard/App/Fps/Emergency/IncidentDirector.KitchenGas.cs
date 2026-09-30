using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Kitchen and gas equipment of the food shops: every cause here starts from a real object the kitchen builder placed
    /// (a fryer, a range, an oven, the hose behind a range, the gas meter), never from a shop name. A fryer's oil ignites when its
    /// thermostat fails, a pan burns on a lit burner, food burns in an oven; a hose slips off a range (the fuse cock stops the
    /// flow, or does not), a burner cock is left open, the meter's union leaks. What stops each is a real fitting too: the range's
    /// intermediate valve, the main valve at the meter, the burner cock, the fryer's power switch. The shop's leak alarm sounds when
    /// the gas reaches it, the automatic diffusion extinguisher under a hood discharges when the flame reaches its bulb, and only
    /// a K-class extinguisher puts out a cooking-oil fire (FireHazard.Oil). The staff member closes valves and switches by hand.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        /// <summary>The kitchen of one shop: the pieces of equipment the rules look up by shop.</summary>
        private sealed class Kitchen
        {
            public string ShopId = "", Label = "";
            public Vector3 Centre;
            public GasValvePoint Main;
            public GasAlarmPoint Alarm;
            public StationEquipment Meter;
        }

        /// <summary>A fire something keeps feeding (gas, power) and what stops the feed: the range's cock, the valves.</summary>
        private sealed class Feeding
        {
            public FireHazard Fire;
            public KitchenAppliancePoint Appliance;
            public string[] Valves = Array.Empty<string>();
        }

        /// <summary>Where a gas leak comes from and what closes it.</summary>
        private sealed class GasLeakSource
        {
            public Kitchen Kitchen;
            public GasHosePoint Hose;
            public KitchenAppliancePoint Range;
            public GasValvePoint[] Valves;
            public AudioSource Hiss;
        }

        private const float AutoExtinguisherReach = 1.2f, GasLingerSeconds = 30f;

        private readonly Dictionary<string, Kitchen> kitchens = new Dictionary<string, Kitchen>();
        private readonly List<KitchenAppliancePoint> appliances = new List<KitchenAppliancePoint>();
        private readonly List<GasHosePoint> hoses = new List<GasHosePoint>();
        private readonly List<AutoExtinguisherPoint> autoExtinguishers = new List<AutoExtinguisherPoint>();
        private readonly Dictionary<string, GasValvePoint> gasValves = new Dictionary<string, GasValvePoint>();
        private readonly List<GasLeakHazard> gasLeaks = new List<GasLeakHazard>();
        private readonly Dictionary<GasLeakHazard, GasLeakSource> leakSources = new Dictionary<GasLeakHazard, GasLeakSource>();
        private readonly List<Feeding> feedings = new List<Feeding>();
        private readonly HashSet<string> spentSources = new HashSet<string>();
        private readonly Dictionary<string, float> gasLingersUntil = new Dictionary<string, float>();
        private readonly HashSet<FireHazard> hoodFires = new HashSet<FireHazard>();
        private readonly HashSet<GasLeakHazard> shutByStaff = new HashSet<GasLeakHazard>(), shutReported = new HashSet<GasLeakHazard>();
        private readonly HashSet<FireHazard> cutByStaff = new HashSet<FireHazard>(), gasCutReported = new HashSet<FireHazard>();

        /// <summary>
        /// Where something in a shop kitchen is, in the words staff use: the floor and the shop's own name ("2층 본관 환공어묵 주방").
        /// The station's nearest-landmark description would name the neighbour whose customer spot happens to be closer (the tenants
        /// stand wall to wall), so it is not used inside a shop.
        /// </summary>
        private string KitchenWhere(Kitchen k, Vector3 at) => (world.Points.ZoneAt(at)?.label ?? "역 구내") + " " + k.Label + " 주방";

        private string KitchenPlace(Kitchen k, Vector3 at) => world.Area(at) + ", " + KitchenWhere(k, at);

        private Kitchen KitchenAt(string shopId)
        {
            if (!kitchens.TryGetValue(shopId, out var kitchen))
                kitchens[shopId] = kitchen = new Kitchen { ShopId = shopId, Label = world.Points.All.First(p => p.Id == shopId).Label };
            return kitchen;
        }

        // ── 시작: 놓인 설비를 점포별로 묶고 손 닿는 것에 반응을 잇는다 ──

        partial void BeginKitchenGas()
        {
            foreach (var kind in new[] { KitchenAppliancePoint.FryerKind, KitchenAppliancePoint.RangeKind, KitchenAppliancePoint.OvenKind })
                foreach (var equipment in EquipmentRegistry.OfKind(kind))
                {
                    var appliance = equipment.GetComponent<KitchenAppliancePoint>();
                    appliance.Bind();
                    appliance.SwitchedOff += OnApplianceOff;
                    appliances.Add(appliance);
                    KitchenAt(appliance.Shop);
                }
            foreach (var equipment in EquipmentRegistry.OfKind(GasValvePoint.Kind))
            {
                var valve = equipment.GetComponent<GasValvePoint>();
                valve.Bind();
                valve.Closing += OnValveClosing;
                gasValves[equipment.Id] = valve;
                if (valve.Main) KitchenAt(valve.Shop).Main = valve;
            }
            foreach (var equipment in EquipmentRegistry.OfKind(GasHosePoint.Kind))
            {
                var hose = equipment.GetComponent<GasHosePoint>();
                hose.Bind();
                hoses.Add(hose);
            }
            foreach (var equipment in EquipmentRegistry.OfKind(GasAlarmPoint.Kind))
            {
                var alarm = equipment.GetComponent<GasAlarmPoint>();
                alarm.Bind();
                alarm.GasPresent = GasNearAlarm;
                KitchenAt(alarm.Shop).Alarm = alarm;
            }
            foreach (var equipment in EquipmentRegistry.OfKind("gas_meter")) KitchenAt(equipment.Text("shop")).Meter = equipment;
            foreach (var equipment in EquipmentRegistry.OfKind(AutoExtinguisherPoint.Kind))
            {
                var unit = equipment.GetComponent<AutoExtinguisherPoint>();
                unit.Bind();
                autoExtinguishers.Add(unit);
            }
            foreach (var equipment in EquipmentRegistry.OfKind(KitchenExtinguisherPoint.Kind)) equipment.GetComponent<KitchenExtinguisherPoint>().Bind(session.Hands);
            foreach (var kitchen in kitchens.Values)
            {
                var mine = appliances.Where(a => a.Shop == kitchen.ShopId).ToList();
                kitchen.Centre = mine.Count == 0 ? kitchen.Meter.transform.position : mine.Aggregate(Vector3.zero, (sum, a) => sum + a.transform.position) / mine.Count;
            }
        }

        // ── 원인: 지금 있는 설비에서만 ──

        private bool KitchenFree(string shopId) => !world.IsClosed(kitchens[shopId].Centre, 2);

        private bool ValveOpen(string id) => id.Length == 0 || !gasValves[id].Closed;

        private bool GasOpen(KitchenAppliancePoint range) => ValveOpen(range.ValveId) && (kitchens[range.Shop].Main == null || !kitchens[range.Shop].Main.Closed);

        /// <summary>Up to two of <paramref name="candidates"/> by this shift's rank: a cause is offered for a couple of its real objects at a time, not for all of them.</summary>
        private IEnumerable<T> Two<T>(IEnumerable<T> candidates, Func<T, string> id) => candidates.OrderBy(c => Rank(id(c))).Take(2);

        partial void KitchenGasOrigins(Pools pools, List<Transition> list)
        {
            bool Usable(KitchenAppliancePoint a) => a.On && !spentSources.Contains(a.Equipment.Id) && KitchenFree(a.Shop);
            foreach (var a in Two(appliances.Where(a => a.Equipment.Kind == KitchenAppliancePoint.FryerKind && Usable(a)), a => a.Equipment.Id)) list.Add(FryerFire(a));
            foreach (var a in Two(appliances.Where(a => a.Equipment.Kind == KitchenAppliancePoint.RangeKind && Usable(a) && GasOpen(a)), a => a.Equipment.Id)) list.Add(PanFire(a));
            foreach (var a in Two(appliances.Where(a => a.Equipment.Kind == KitchenAppliancePoint.OvenKind && Usable(a)), a => a.Equipment.Id)) list.Add(OvenFire(a));
            foreach (var hose in Two(hoses.Where(h => !h.Detached && !spentSources.Contains(h.Equipment.Id) && KitchenFree(h.Shop) && GasOpen(RangeById(h.RangeId))), h => h.Equipment.Id)) list.Add(HoseOff(hose));
            foreach (var a in Two(appliances.Where(a => a.Equipment.Kind == KitchenAppliancePoint.RangeKind && a.On && !spentSources.Contains(a.Equipment.Id + "#cock") && KitchenFree(a.Shop) && GasOpen(a)), a => a.Equipment.Id))
                list.Add(CockOpen(a));
            foreach (var k in Two(kitchens.Values.Where(k => k.Meter != null && k.Main != null && !k.Main.Closed && !spentSources.Contains(k.Meter.Id) && KitchenFree(k.ShopId)), k => k.Meter.Id))
                list.Add(MeterLeak(k));
        }

        private KitchenAppliancePoint RangeById(string id) => appliances.First(a => a.Equipment.Id == id);

        private Transition FryerFire(KitchenAppliancePoint fryer)
        {
            var k = kitchens[fryer.Shop];
            return new Transition
            {
                Key = "fryer_oil_" + fryer.Equipment.Id, Kind = "fryer_oil_fire", Origin = true,
                Description = "The thermostat of the electric deep fryer in the food shop '" + k.Label + "' (" + KitchenPlace(k, fryer.transform.position) + ") fails: the oil keeps heating past its ignition point and catches fire while staff are busy at the counter.",
                Levels = new List<string> { "the oil smokes heavily before anyone notices", "the oil in the fryer tank catches fire", "flames climb to the extractor hood", "burning oil spits and flames fill the hood", "a fierce oil fire, thick black smoke pours out of the shop" },
                Apply = m => IgniteAppliance(k, fryer, m, k.Label + " 튀김기 기름", k.Label + " 튀김기", true, "전원", " 식용유 화재에는 물을 쓰지 말고 K급 소화기를 쓰십시오. 튀김기 전원부터 끄게 하십시오."),
            };
        }

        private Transition PanFire(KitchenAppliancePoint range)
        {
            var k = kitchens[range.Shop];
            return new Transition
            {
                Key = "range_fire_" + range.Equipment.Id, Kind = "kitchen_fire", Origin = true,
                Description = "A pan left on the lit burner of the gas range in the food shop '" + k.Label + "' (" + KitchenPlace(k, range.transform.position) + ") boils dry and its grease catches fire while staff are busy at the counter.",
                Levels = new List<string> { "burnt food smokes on the burner", "a small flame in the pan", "flames reach the extractor hood", "the fire spreads along the kitchen counter", "a fierce kitchen fire, thick black smoke pours out of the shop" },
                Apply = m => IgniteAppliance(k, range, m, k.Label + " 주방 가스레인지 팬", k.Label + " 주방 가스레인지", false, "가스", " 가스 중간밸브부터 잠그게 하고 물러서서 초기 진화를 시도하십시오."),
            };
        }

        private Transition OvenFire(KitchenAppliancePoint oven)
        {
            var k = kitchens[oven.Shop];
            return new Transition
            {
                Key = "oven_fire_" + oven.Equipment.Id, Kind = "oven_fire", Origin = true,
                Description = "Food and grease burn inside the electric oven of the food shop '" + k.Label + "' (" + KitchenPlace(k, oven.transform.position) + "); smoke leaks out around the oven door.",
                Levels = new List<string> { "smoke leaks around the oven door", "smoke pours from the oven and a glow shows behind the door", "flames lick out of the oven door", "the fire spreads to the counter beside the oven", "a fierce kitchen fire, thick black smoke pours out of the shop" },
                Apply = m => IgniteAppliance(k, oven, m, k.Label + " 오븐", k.Label + " 오븐", false, "전원", " 오븐 전원을 끄고 문은 열지 마십시오(열면 불이 커집니다)."),
            };
        }

        private Transition HoseOff(GasHosePoint hose)
        {
            var k = kitchens[hose.Shop];
            return new Transition
            {
                Key = "hose_off_" + hose.Equipment.Id, Kind = "gas_hose_off", Origin = true,
                Description = "The flexible gas hose behind the gas range of the food shop '" + k.Label + "' (" + KitchenPlace(k, hose.transform.position) + ") slips off the range's inlet; the fuse cock (퓨즈콕) on the wall may or may not stop the flow.",
                Levels = new List<string> { "the fuse cock shuts the flow within seconds, only a brief whiff of gas", "gas leaks slowly from the loose hose", "gas keeps hissing from the loose hose", "a strong hiss, the smell spreads through the shop", "a strong hiss, the smell spreads into the passage and people nearby get headaches" },
                Apply = m => { hose.Detach(); StartLeak(k, GasSource.Hose, m, hose.Leak.position, hose.Equipment.Id, hose, RangeById(hose.RangeId), hose.ValveId); },
            };
        }

        private Transition CockOpen(KitchenAppliancePoint range)
        {
            var k = kitchens[range.Shop];
            return new Transition
            {
                Key = "gas_cock_" + range.Equipment.Id, Kind = "gas_cock_open", Origin = true,
                Description = "A burner cock of the gas range in the food shop '" + k.Label + "' (" + KitchenPlace(k, range.transform.position) + ") was left slightly open after the flame blew out: unlit gas seeps out, and the shop's leak alarm will notice it before anyone smells it.",
                Levels = new List<string> { "a trace of gas that only the alarm notices", "a faint smell of gas near the range", "a clear smell of gas in the kitchen", "the smell spreads into the passage in front of the shop", "a strong smell, people nearby get headaches" },
                Apply = m => StartLeak(k, GasSource.Cock, m, range.Fire.position, range.Equipment.Id + "#cock", null, range, range.ValveId),
            };
        }

        private Transition MeterLeak(Kitchen k)
        {
            return new Transition
            {
                Key = "gas_meter_" + k.Meter.Id, Kind = "gas_meter_leak", Origin = true,
                Description = "The union where the pipe meets the gas meter of the food shop '" + k.Label + "' (" + KitchenPlace(k, k.Meter.transform.position) + ") works loose and gas seeps out; it is upstream of every burner valve, only the main valve at the meter stops it.",
                Levels = new List<string> { "a faint smell of gas near the meter", "a clear smell of gas in the shop", "the smell spreads into the passage in front of the shop", "a strong smell, people nearby get headaches", "a hissing leak, the smell spreads across the floor" },
                Apply = m => StartLeak(k, GasSource.Meter, m, k.Meter.transform.Find("Leak").position, k.Meter.Id, null, null, null),
            };
        }

        // ── 적용 ──

        private FireHazard IgniteAppliance(Kitchen k, KitchenAppliancePoint appliance, float magnitude, string source, string subject, bool oil, string feed, string advice)
        {
            spentSources.Add(appliance.Equipment.Id);
            var fire = Ignite(appliance.Fire.position, source, subject, magnitude, advice, where: KitchenWhere(k, appliance.Fire.position));
            fire.Oil = oil;
            fire.Footprint = appliance.Equipment.Kind == KitchenAppliancePoint.FryerKind ? .13f : appliance.Equipment.Kind == KitchenAppliancePoint.RangeKind ? .22f : .35f;
            // 불을 먹이는 것: 전원과 가스. 끊기 전에는 불씨까지만 꺼진다. 소방대도 끊어 준다.
            fire.SetFeed(feed, Agency.Fire, 20f);
            var valveIds = appliance.Gas ? new[] { appliance.ValveId, k.Main != null ? k.Main.Equipment.Id : "" } : Array.Empty<string>();
            feedings.Add(new Feeding { Fire = fire, Appliance = appliance, Valves = valveIds });
            if (oil) fire.Splashed += OilSplashed;
            return fire;
        }

        private void StartLeak(Kitchen k, GasSource source, float magnitude, Vector3 at, string sourceId, GasHosePoint hose, KitchenAppliancePoint range, string valveId)
        {
            spentSources.Add(sourceId);
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var stoppers = new List<GasValvePoint>();
            if (valveId != null) stoppers.Add(gasValves[valveId]);
            if (k.Main != null) stoppers.Add(k.Main);
            var stoppedBy = stoppers.Select(v => v.Equipment.Id).ToList();
            if (source == GasSource.Cock) stoppedBy.Add(range.Equipment.Id);
            var gas = new GasLeakHazard("gas-" + ++serial, at, k.Label, level, source, stoppedBy) { Where = KitchenWhere(k, at) };
            gasLeaks.Add(gas);
            var hiss = new GameObject("가스 새는 소리").AddComponent<AudioSource>();
            hiss.transform.SetParent(root, false);
            hiss.transform.position = at;
            hiss.clip = KitchenSound.Hiss;
            hiss.loop = true;
            hiss.spatialBlend = 1f;
            hiss.minDistance = 1f;
            hiss.maxDistance = 14f;
            hiss.rolloffMode = AudioRolloffMode.Linear;
            hiss.volume = .12f + .1f * level;
            if (level > 0) hiss.Play();
            leakSources[gas] = new GasLeakSource { Kitchen = k, Hose = hose, Range = range, Valves = stoppers.ToArray(), Hiss = hiss };
            Register(gas);
            log.Add("가스 누출 · " + gas.Where + " — " + gas.Visible);
        }

        /// <summary>A leak ends: something closed off what fed it. The smell lingers a little for the alarm.</summary>
        private void StopLeak(GasLeakHazard gas, string by, string what)
        {
            var src = leakSources[gas];
            gas.ShutOff();
            HazardRegistry.Remove(gas);
            if (src.Hiss != null) Destroy(src.Hiss.gameObject);
            gasLingersUntil[src.Kitchen.ShopId] = Time.time + GasLingerSeconds;
            if (by.StartsWith("역무원", StringComparison.Ordinal)) shutByStaff.Add(gas);
            log.Add("가스 누출이 멈춤 · " + gas.Where + " (" + by + " · " + what + ")");
        }

        private void CutFeeding(Feeding feeding, string by, string what)
        {
            if (feeding.Fire.Extinguished || !feeding.Fire.CutFeed(by + " · " + what)) return;
            if (by.StartsWith("역무원", StringComparison.Ordinal)) cutByStaff.Add(feeding.Fire);
            log.Add("불을 먹이는 것을 끊음 · " + feeding.Fire.Where + " (" + by + " · " + what + ")");
        }

        private void OnValveClosing(GasValvePoint valve, string by)
        {
            log.Add(valve.Equipment.Label + " 잠금 (" + by + ")");
            foreach (var gas in gasLeaks.Where(g => g.Active && g.StoppedBy.Contains(valve.Equipment.Id)).ToList()) StopLeak(gas, by, valve.Main ? "메인밸브" : "중간밸브");
            foreach (var feeding in feedings.Where(f => f.Valves.Contains(valve.Equipment.Id) && f.Fire.Feed != null).ToList()) CutFeeding(feeding, by, valve.Main ? "가스 메인밸브" : "가스 중간밸브");
        }

        private void OnApplianceOff(KitchenAppliancePoint appliance, string by)
        {
            log.Add(appliance.Equipment.Label + " " + (appliance.Gas ? "코크 잠금" : "전원 끔") + " (" + by + ")");
            foreach (var feeding in feedings.Where(f => f.Appliance == appliance && f.Fire.Feed != null).ToList()) CutFeeding(feeding, by, appliance.Equipment.Label + (appliance.Gas ? " 코크" : " 전원"));
            foreach (var gas in gasLeaks.Where(g => g.Active && g.Source == GasSource.Cock && leakSources[g].Range == appliance).ToList()) StopLeak(gas, by, "화구 코크");
        }

        private void OilSplashed(FireHazard fire)
        {
            log.Add("끓는 기름에 물을 뿌려 기름이 튀고 불길이 솟았다 · " + fire.Where);
            session.Hud.Toast("끓는 기름에 물을 뿌리면 기름이 튀고 불이 커집니다 · K급 소화기를 쓰세요", 6f);
            NearestPerson(fire.Position, 3, p => !p.Hurt && !p.Hostile)?.Injure("끓는 기름이 튀어 화상을 입음");
        }

        private bool GasNearAlarm(GasAlarmPoint alarm) =>
            gasLeaks.Any(g => g.Active && leakSources[g].Kitchen.ShopId == alarm.Shop) || gasLingersUntil.TryGetValue(alarm.Shop, out var until) && Time.time < until;

        /// <summary>Gas that catches fire burns where it leaks, and the leak keeps feeding the flame until its valve is shut.</summary>
        private void GasIgnites(GasLeakHazard gas, float magnitude)
        {
            var src = leakSources[gas];
            gas.ShutOff();
            HazardRegistry.Remove(gas);
            if (src.Hiss != null) Destroy(src.Hiss.gameObject);
            log.Add("가스에 불이 붙음 · " + gas.Where);
            if (magnitude < .2f) { log.Add("순간 불꽃이 일었다 꺼짐"); return; }
            var fire = Ignite(gas.Position, gas.Shop + " 주방 가스", gas.Shop + " 주방", magnitude, " 가스 밸브를 잠그게 하고 불씨가 번지지 않게 주변을 비우십시오.", where: gas.Where);
            fire.Footprint = .35f;
            fire.SetFeed("가스", Agency.Fire, 20f);
            feedings.Add(new Feeding { Fire = fire, Appliance = gas.Source == GasSource.Cock ? src.Range : null, Valves = gas.StoppedBy.Where(gasValves.ContainsKey).ToArray() });
            if (magnitude >= .5f) NearestPerson(gas.Position, 4, p => !p.Hurt && !p.Hostile)?.Injure("가스 불꽃에 화상을 입음");
            crowd.Alert(fire.Position, 25, fire, null, "there was a bang and a flash of fire");
        }

        // ── 규칙: 경보기·퓨즈콕·자동확산소화기 ──

        partial void KitchenGasTick(float dt)
        {
            foreach (var gas in gasLeaks.ToArray())
            {
                if (!gas.Active) continue;
                var src = leakSources[gas];
                float age = Time.time - gas.StartedAt;
                // 퓨즈콕: 호스가 빠져 흐름이 갑자기 늘면 몇 초 안에 스스로 닫힌다(약한 누출만; 고장 난 것은 계속 샌다).
                if (gas.Source == GasSource.Hose && gas.Level == 0 && age > 4) { StopLeak(gas, "퓨즈콕", "과류 차단"); continue; }
                // 경보기: 가스가 천장까지 올라가면(새는 세기에 따라 수 초~수십 초) 울린다.
                var alarm = src.Kitchen.Alarm;
                if (alarm != null && !alarm.Sounding && age > (gas.Level == 0 ? 25 : 8)) { alarm.Sound(); log.Add("가스누설경보기 울림 · " + gas.Where); }
                if (age > 60 && CountAware(gas) >= 2) CitizenCall(null, gas);
            }
            if (fires.Count == 0) return;
            foreach (var unit in autoExtinguishers)
            {
                bool discharge = !unit.Discharged, spraying = unit.Discharging;
                if (!discharge && !spraying) continue;
                foreach (var fire in fires)
                {
                    if (fire.Extinguished || !UnderNozzle(unit, fire)) continue;
                    if (discharge && fire.Intensity >= .5f) { unit.Discharge(art.Smoke); log.Add("자동확산소화기 방출 · " + fire.Where); }
                    else if (spraying)
                    {
                        fire.SuppressWith(ExtinguishAgent.WetChemical, .8f, dt);
                        if (fire.Extinguished) OnFireOut(fire, "자동확산소화기");
                    }
                }
            }
        }

        private static bool UnderNozzle(AutoExtinguisherPoint unit, FireHazard fire)
        {
            var d = fire.Position - unit.Nozzle;
            return new Vector2(d.x, d.z).magnitude < AutoExtinguisherReach && d.y < 0 && d.y > -1.6f;
        }

        // ── 전개 ──

        partial void KitchenGasDevelopments(List<Transition> list)
        {
            foreach (var leak in gasLeaks)
            {
                if (!leak.Active) continue;
                var g = leak;
                var src = leakSources[g];
                string what = g.Source == GasSource.Hose ? "the loose gas hose" : g.Source == GasSource.Cock ? "the open burner cock" : "the union of the gas meter";
                if (g.Level < 4 && Ready("gas_worse_" + g.Id))
                    list.Add(new Transition { Key = "gas_worse_" + g.Id, Kind = "gas_spreads", Description = "The gas leaking from " + what + " in '" + g.Shop + "' grows stronger and spreads", Apply = _ => { g.Worsen(); log.Add("가스 냄새가 짙어짐 · " + g.Where); } });
                var valve = src.Valves.FirstOrDefault(v => !v.Closed);
                if (valve != null && Ready("gas_valve_" + g.Id))
                    list.Add(new Transition { Key = "gas_valve_" + g.Id, Kind = "gas_valve_shut", Description = "A worker at '" + g.Shop + "' finds the leak and closes the " + (valve.Main ? "main valve at the meter" : "intermediate valve of the range"), Apply = _ => valve.Close("점포 직원") });
                var dizzy = NearestPerson(g.Position, g.DangerRadius + 2, p => !p.Hurt && !p.Hostile);
                if (dizzy != null && g.Level >= 2 && Ready("gas_dizzy"))
                    list.Add(new Transition { Key = "gas_dizzy_" + dizzy.Number, Kind = "gas_dizzy", Description = Profile(dizzy) + " near '" + g.Shop + "' feels dizzy from the gas and sits down", Apply = _ => dizzy.Injure("가스 냄새를 맡고 어지러워 주저앉음") });
                if (g.Level >= 3 && Ready("gas_ignites"))
                    list.Add(new Transition
                    {
                        Key = "gas_ignites_" + g.Id, Kind = "gas_ignites", Description = "Someone in '" + g.Shop + "' switches on an appliance near " + what + " and the gas ignites with a flash",
                        Levels = new List<string> { "a brief flash that goes out", "a flash and a small fire in the kitchen", "a fireball in the kitchen, a worker is burnt", "a fireball that blows out the shop front", "a fierce fire after the flash" },
                        Apply = m => GasIgnites(g, m),
                    });
            }
            foreach (var feeding in feedings)
            {
                var fire = feeding.Fire;
                var appliance = feeding.Appliance;
                if (fire.Extinguished) continue;
                if (appliance != null && appliance.HoodId.Length > 0 && fire.Intensity >= .35f && fire.Intensity < 1f && !hoodFires.Contains(fire) && Ready("hood_" + fire.Id))
                    list.Add(new Transition
                    {
                        Key = "hood_" + fire.Id, Kind = "hood_fire", Description = "The flames of the burning " + fire.Subject + " at " + fire.Where + " reach the grease-coated filters of the hood over it and the hood catches fire",
                        Apply = _ => { hoodFires.Add(fire); fire.Grow(.25f); fire.SpreadSmoke(6); log.Add("후드 기름때에 불이 붙음 · " + fire.Where); },
                    });
                if (appliance != null && appliance.On && fire.Intensity < .6f && Ready("switch_off_" + fire.Id))
                    list.Add(new Transition
                    {
                        Key = "switch_off_" + fire.Id, Kind = "cook_switches_off", Description = "A cook of '" + KitchenName(appliance) + "' switches the burning " + appliance.Equipment.Label + " off and backs away",
                        Apply = _ => appliance.SwitchOff("점포 직원"),
                    });
            }
        }

        private string KitchenName(KitchenAppliancePoint appliance) => kitchens[appliance.Shop].Label;

        // ── 역무원이 아는 것과 무전 ──

        partial void LookAroundKitchenGas(Vector3 eye, Vector3 forward)
        {
            // 경보기가 울리는 것은 가게 밖에서도 들린다.
            foreach (var gas in gasLeaks)
            {
                if (!gas.Active || known.Contains(gas)) continue;
                var alarm = leakSources[gas].Kitchen.Alarm;
                if (alarm != null && alarm.Sounding && Vector3.Distance(eye, alarm.transform.position) < 18f) Know(gas, "가스누설경보기 경보음");
            }
        }

        partial void KitchenGasRadio(List<EmergencySession.RadioOption> options)
        {
            foreach (var gas in gasLeaks.Where(g => shutByStaff.Contains(g) && !shutReported.Contains(g)).Take(1))
            {
                var g = gas;
                options.Add(Option("역무실 · 가스 밸브 잠금·환기 보고", () =>
                {
                    shutReported.Add(g);
                    Say("역무실, " + g.Where + " " + g.Shop + " 가스 밸브 잠갔습니다. 누출은 멈췄고 환기 중입니다.");
                    Office("역무실 수신. 도시가스 안전점검원 보내겠습니다. 점검 끝날 때까지 그 매장은 가스를 쓰지 않게 하십시오.");
                    log.Add("역무실에 가스 밸브 잠금 보고 · " + g.Where);
                    Call(Agency.Facility, "역무원 가스 밸브 잠금 보고");
                }));
            }
            foreach (var fire in cutByStaff.Where(f => !gasCutReported.Contains(f)).Take(1))
            {
                var f = fire;
                options.Add(Option("역무실 · 조리 열원 차단 보고", () =>
                {
                    gasCutReported.Add(f);
                    Say("역무실, " + f.Where + " " + f.FeedName + " 차단했습니다. " + (f.Extinguished ? "불은 꺼졌습니다." : "불은 아직 탑니다."));
                    Office("역무실 수신. 소방대에 열원 차단을 전하겠습니다.");
                    log.Add("역무실에 열원 차단 보고 · " + f.Where);
                }));
            }
        }

        partial void KitchenGasResolved(Hazard hazard)
        {
            // 도시가스 안전점검원·소방대가 조치를 끝내면 실제 밸브를 잠그고 빠진 호스는 다시 물린다.
            if (!(hazard is GasLeakHazard gas) || !leakSources.TryGetValue(gas, out var src)) return;
            foreach (var valve in src.Valves) valve.Close("도시가스 안전점검원");
            if (src.Hose != null && src.Hose.Detached) src.Hose.Reattach();
        }
    }
}
