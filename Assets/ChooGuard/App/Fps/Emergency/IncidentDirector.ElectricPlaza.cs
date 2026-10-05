using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Electrical equipment and concourse fittings as causes and as things the staff member works on. A distribution board,
    /// a vending machine and a phone-charging kiosk can each start a fire that burns where the object really stands
    /// (smoke and flames come out of it), on a circuit whose breaker sits in a real board: while it is live the fire cannot be
    /// put out (<see cref="FireHazard.Feed"/>), so the staff member opens the board and switches the breaker off, pulls the
    /// plug or throws the kiosk's switch, or waits for the electrician. Water on live equipment shocks whoever holds the
    /// hose. A burnt-out machine stays dead, a burnt-out board takes every machine it feeds with it. The cut, the report to
    /// the office and what the office does next (the electrician, the fire brigade's request to cut the power, a warning
    /// not to switch back on) are here; the fire itself stays a <see cref="FireHazard"/> and follows its common rules.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private static readonly string[] BoardLevels =
        {
            "a faint hot-plastic smell and a wisp of white smoke from the door seams", "grey smoke pouring from the door seams and vents",
            "sparks and a small flame glow through the vents, the smell of burning insulation", "flames lick out of the board and reach the cable duct above it",
            "a fierce fire in and around the board with thick toxic smoke along the ceiling",
        };

        private static readonly string[] VendingLevels =
        {
            "a burning-plastic smell and a wisp of smoke from the vent grille", "grey smoke from the bottom of the machine, its lights flicker",
            "sparks and small flames at the wiring at the base", "flames spread up inside the machine, the plastic front and the packaging catch",
            "the whole machine burns, thick black smoke fills the concourse",
        };

        private static readonly string[] KioskLevels =
        {
            "a hot-electronics smell and a wisp of smoke from one locker slot", "white smoke and a hiss from a locker: the battery is swelling",
            "a phone battery vents in a jet of sparks and flame", "the batteries in the neighbouring lockers catch, flames and acrid smoke",
            "the kiosk is engulfed, more batteries burst, thick toxic smoke",
        };

        private static readonly List<string> BoardLevelTexts = new List<string>(BoardLevels), VendingLevelTexts = new List<string>(VendingLevels), KioskLevelTexts = new List<string>(KioskLevels);

        /// <summary>Who cut a feed by hand, as <see cref="FireHazard.CutBy"/> records it.</summary>
        private const string StaffCut = ElectricNetwork.StaffName;

        private readonly HashSet<FireHazard> cutReported = new HashSet<FireHazard>(), embersHinted = new HashSet<FireHazard>(), burntNoted = new HashSet<FireHazard>();
        private readonly List<(float at, string text)> officeLines = new List<(float, string)>();
        private bool syncingFeeds, brigadeAskedForCut;
        private readonly Dictionary<StationEquipment, string> equipmentPlaces = new Dictionary<StationEquipment, string>();

        // ── 시작과 끝 ──

        partial void BeginElectricPlaza()
        {
            ElectricNetwork.Build();
            ElectricNetwork.Switched += OnBreaker;
            foreach (var board in ElectricNetwork.Boards)
            {
                var b = board;
                if (b.Unit != null) b.Unit.Blocked = () => BoardBlocked(b);
            }
            foreach (var machine in Loads())
            {
                var m = machine;
                var load = m.GetComponent<ElectricLoad>();
                if (load == null) continue;
                load.Blocked = () => LoadBlocked(m);
                load.HandSwitched += OnHandSwitched;
            }
            session.BoardProviders.Add(ElectricColumn);
        }

        partial void EndElectricPlaza()
        {
            ElectricNetwork.Switched -= OnBreaker;
            session?.BoardProviders.Remove(ElectricColumn);
            ElectricNetwork.Clear();
        }

        private static IEnumerable<StationEquipment> Loads() => EquipmentRegistry.OfKind("vending_machine").Concat(EquipmentRegistry.OfKind("charging_kiosk"));

        /// <summary>The radio name of where a placed piece stands. Naming a place scans the station's points, and the candidate lists are built again on every round, so it is asked once per piece; only a platform piece is named again each time, because its name depends on where the train stands.</summary>
        private string PlaceOf(StationEquipment e)
        {
            if (e.Zone == "tracks") return Place(e.transform.position);
            if (!equipmentPlaces.TryGetValue(e, out var place)) equipmentPlaces[e] = place = Place(e.transform.position);
            return place;
        }

        // ── 원인 ──

        private IEnumerable<Transition> ElectricPlazaOrigins() => Chain(
            Pieces("distribution_board", e => ElectricNetwork.BoardOf(e) is ElectricNetwork.Board b && b.Main.On && !b.Damaged, e => BoardFire(ElectricNetwork.BoardOf(e))),
            Pieces("vending_machine", LoadUsable, VendingFire),
            Pieces("charging_kiosk", e => LoadUsable(e) && PeopleNear(e.transform.position, 8f) > 0, KioskFire));

        /// <summary>A machine that can start a fault now: powered, not burning and not burnt out.</summary>
        private bool LoadUsable(StationEquipment machine) => ElectricNetwork.Powered(machine) && !BurningIn(machine);

        private bool BurningIn(StationEquipment installation) => fires.Exists(f => f.Installation == installation) || installation.GetComponent<ElectricLoad>() is ElectricLoad load && load.Burnt;

        /// <summary>
        /// JEV answers a moment after the candidates were listed (and again for the magnitude), and the piece may have been
        /// switched off, burnt out or lit by then: an answer is applied only if what it names is still a candidate.
        /// </summary>
        private bool StillIgnitable(StationEquipment e)
        {
            if (e == null || BurningIn(e) || world.IsClosed(e.transform.position, 2)) return false;
            if (e.Kind == "distribution_board") return ElectricNetwork.BoardOf(e) is ElectricNetwork.Board board && board.Main.On && !board.Damaged;
            return e.Kind == "vending_machine" || e.Kind == "charging_kiosk" ? LoadUsable(e) : true;
        }

        /// <summary>Records that an answer named something the world no longer offers, so the shift log does not show a composed event that never happened.</summary>
        private bool Still(bool valid, string what)
        {
            if (!valid) log.Add("장면이 바뀌어 적용하지 않음 · " + what);
            return valid;
        }

        private int peopleSnapshotFrame = -1;
        private readonly List<Vector3> peopleAt = new List<Vector3>();

        /// <summary>
        /// How many people in the station (not aboard a train) stand within <paramref name="radius"/> of <paramref name="position"/>
        /// on about the same floor. One listing of causes asks about a dozen places, and reading every person's transform for each
        /// asked cost more than the rest of the listing together, so the crowd's positions are read once per frame.
        /// </summary>
        private int PeopleNear(Vector3 position, float radius)
        {
            if (peopleSnapshotFrame != Time.frameCount)
            {
                peopleSnapshotFrame = Time.frameCount;
                peopleAt.Clear();
                foreach (var person in crowd.People) if (!person.Aboard) peopleAt.Add(person.transform.position);
            }
            int near = 0;
            foreach (var at in peopleAt) if (Mathf.Abs(at.y - position.y) < 3f && (at - position).sqrMagnitude < radius * radius) near++;
            return near;
        }

        private string CrowdNote(Vector3 position)
        {
            int near = PeopleNear(position, 7f);
            return near == 0 ? "Nobody is within 7 m of it." : near + (near == 1 ? " passenger is" : " passengers are") + " within 7 m of it.";
        }

        private Transition BoardFire(ElectricNetwork.Board board)
        {
            var e = board.Equipment;
            int machines = board.Branches.Count(c => c.Load != null);
            return new Transition
            {
                Key = "board_fire_" + e.Id, Kind = "board_fire", Origin = true,
                Description = "A loose terminal on one of the breakers inside distribution board " + e.Label + " (" + PlaceOf(e) + ") overheats and starts to melt its insulation. The panel stays live: it feeds " +
                    (machines > 0 ? machines + (machines == 1 ? " vending machine or kiosk plus " : " vending machines and kiosks plus ") : "") + "the lighting and sockets of the area, and smoke would first leak from its door seams. " + CrowdNote(e.transform.position),
                Levels = BoardLevelTexts,
                Apply = m => { if (Still(StillIgnitable(e), e.Label + " " + e.Id)) StartEquipmentFire(e, m, null); },
            };
        }

        private Transition VendingFire(StationEquipment machine)
        {
            bool drink = machine.Label.Contains("음료");
            var circuit = ElectricNetwork.CircuitOf(machine);
            return new Transition
            {
                Key = "vending_fire_" + machine.Id, Kind = "vending_fire", Origin = true,
                Description = (drink
                    ? "The compressor or the condenser fan motor of the drink vending machine " + ElectricNetwork.Tag(machine) + " (" + PlaceOf(machine) + ") overheats: dust in the condenser, a sticking relay, a worn power cord. "
                    : "The wiring or the power supply of the snack vending machine " + ElectricNetwork.Tag(machine) + " (" + PlaceOf(machine) + ") shorts (worn cord insulation, dust and damp, an overloaded coil motor). ")
                    + "It starts to smoke. It is fed from " + (circuit != null ? "breaker " + circuit.Label : "the floor's sockets") + ". " + CrowdNote(machine.transform.position),
                Levels = VendingLevelTexts,
                Apply = m => { if (Still(StillIgnitable(machine), machine.Label + " " + machine.Id)) StartEquipmentFire(machine, m, null); },
            };
        }

        private Transition KioskFire(StationEquipment kiosk)
        {
            var circuit = ElectricNetwork.CircuitOf(kiosk);
            int phones = Mathf.Clamp(1 + PeopleNear(kiosk.transform.position, 8f) / 2, 1, 8);
            return new Transition
            {
                Key = "kiosk_fire_" + kiosk.Id, Kind = "kiosk_fire", Origin = true,
                // 이미 망가진 배터리가 들어 있다고 전제하지 않는다: 그런 상태는 세계에 없으므로 JEV 가 '구체적 근거'로 읽어 다른 원인보다 몇 배 높게 판단한다(2026-09-30 측정).
                Description = "The charger electronics of the phone-charging kiosk " + ElectricNetwork.Tag(kiosk) + " (" + PlaceOf(kiosk) + ") or a phone charging in one of its lockers overheats: a failing charger module, a worn cable, a faulty battery. It starts to smoke. " +
                    phones + " of its 8 lockers hold a charging phone. It is fed from " + (circuit != null ? "breaker " + circuit.Label : "the floor's sockets") + ". " + CrowdNote(kiosk.transform.position),
                Levels = KioskLevelTexts,
                Apply = m => { if (Still(StillIgnitable(kiosk), kiosk.Label + " " + kiosk.Id)) StartEquipmentFire(kiosk, m, null); },
            };
        }

        // ── 적용 ──

        private static bool Electrical(StationEquipment e) => e.Kind == "distribution_board" || e.Kind == "vending_machine" || e.Kind == "charging_kiosk";

        /// <summary>What a fault in an installation looks like: what people call it, how high the flames come out of its front, how wide they can spread, how far in front of the pivot, the office's advice.</summary>
        private static (string subject, float height, float footprint, float forward, string advice) FaultOf(StationEquipment e)
        {
            switch (e.Kind)
            {
                case "distribution_board":
                    return ("분전반", BreakerDeckLayout.BoardBottom + .42f, .3f, .2f, " 전기 화재입니다. 분전반은 열지 말고 분말 소화기로 끄되 물은 쓰지 마십시오. 전기 담당도 부르겠습니다.");
                case "charging_kiosk":
                    return (e.Label, .95f, .35f, .18f, " 휴대폰 배터리 화재입니다. 충전 전원을 끊고 배터리에는 손대지 마십시오. 꺼진 뒤에도 다시 불이 붙을 수 있습니다. 물은 쓰지 마십시오.");
                // 휴지통은 원점이 뒤판(벽)에 있어 통 가운데는 깊이의 반만큼 앞이고, 분리수거함은 원점이 바닥 가운데다. 불은 통 입구에서 오른다.
                case "litter_bin":
                    return ("휴지통", .85f, .25f, .22f, " 작은 쓰레기통 불입니다. 분말 소화기로 끄고, 꺼진 뒤에도 잔불이 없는지 확인하십시오.");
                case "recycling_bin":
                    return ("분리수거함", 1.05f, .35f, 0f, " 분리수거함 불입니다. 분말 소화기로 끄고, 안에 배터리가 섞였으면 꺼진 뒤에도 다시 타오를 수 있으니 물로 식혀 주십시오.");
                default:
                    return (e.Label, .38f, .5f, .3f, " 전기 화재입니다. 전원 코드를 뽑거나 분전반에서 그 차단기를 내리고 물은 쓰지 마십시오. 전기 담당도 부르겠습니다.");
            }
        }

        private static string[] LevelsOf(StationEquipment e) => e.Kind == "distribution_board" ? BoardLevels : e.Kind == "charging_kiosk" ? KioskLevels : VendingLevels;

        private static string HowKo(StationEquipment e) =>
            e.Kind == "distribution_board" ? "단자 접촉불량 과열" : e.Kind == "charging_kiosk" ? "휴대폰 배터리 열폭주" : e.Label.Contains("음료") ? "압축기·전원 계통 합선" : "배선·전원부 합선";

        /// <summary>
        /// Starts the fire in an installation: it burns where the object stands (the flames and smoke come out of it), no wider
        /// than the object. An electrical installation (board, machine, kiosk) is fed while it is live; a bin is not. <paramref name="how"/>
        /// names the cause in Korean (null: the usual fault of that kind), <paramref name="advice"/> replaces the office's usual advice.
        /// </summary>
        private FireHazard StartEquipmentFire(StationEquipment installation, float magnitude, string how, string advice = null)
        {
            var fault = FaultOf(installation);
            var t = installation.transform;
            var origin = t.position + t.forward * fault.forward + Vector3.up * fault.height;
            bool electrical = Electrical(installation);
            string name = installation.Kind == "distribution_board" ? "분전반 " + ElectricNetwork.BoardOf(installation).Code : electrical ? installation.Label + " " + ElectricNetwork.Tag(installation) : installation.Label + " " + installation.Id;
            var fire = Ignite(origin, name + " " + (how ?? HowKo(installation)), fault.subject, magnitude, advice ?? fault.advice, where: world.Describe(t.position) + " " + fault.subject);
            fire.Footprint = fault.footprint;
            // 불은 설비 안에서 오른다. 불 표지의 충돌체가 분전반 문이나 자판기 앞을 막아 조준을 가로채지 않게 끈다.
            foreach (var collider in fire.View.GetComponents<Collider>()) collider.enabled = false;
            fire.Installation = installation;
            if (!electrical) { installation.State = "화재"; return fire; }
            fire.Electric = true;
            fire.WaterIsDangerous = true;
            if (LiveInstallation(installation)) fire.SetFeed("전원", Agency.Facility, 25f);
            fire.WetWhileLive += OnWetWhileLive;
            fire.FeedCut += OnFeedCut;
            var load = installation.GetComponent<ElectricLoad>();
            if (load != null) { load.OnFire = true; load.Refresh(); }
            else installation.State = "화재";
            return fire;
        }

        private static bool LiveInstallation(StationEquipment installation)
        {
            if (installation.Kind != "distribution_board") return ElectricNetwork.Powered(installation);
            var board = ElectricNetwork.BoardOf(installation);
            return board != null && board.Main.On;
        }

        // ── 차단 ──

        private void OnBreaker(ElectricNetwork.Circuit circuit, bool on, string by)
        {
            log.Add("차단기 " + (on ? "올림" : "내림") + " · " + circuit.Label + " (" + circuit.Name + ") — " + by);
            // 결함을 가르려고 내린 차단기에는 작동금지 표지를 건다: 건 사람이 뗀다(KOSHA 잠금·표지).
            if (!on && by != "보호장치" && fires.Exists(f => f.Electric && f.Installation != null && !f.Extinguished && (circuit.Number == 0 ? ElectricNetwork.BoardOf(f.Installation) == circuit.Board : f.Installation == circuit.Load)))
            {
                circuit.Tag = "작동금지 · " + by + " " + session.Clock(session.ShiftSeconds);
                circuit.TagBy = by;
            }
            else if (on) { circuit.Tag = null; circuit.TagBy = null; }
            SyncFeeds(by);
        }

        private void OnHandSwitched(ElectricLoad load, bool off)
        {
            log.Add((load.Kiosk ? "전원 스위치 " : "전원 코드 ") + (off ? (load.Kiosk ? "끔" : "뽑음") : (load.Kiosk ? "켬" : "꽂음")) + " · " + load.Equipment.Label + " " + ElectricNetwork.Tag(load.Equipment));
            SyncFeeds(StaffCut);
        }

        /// <summary>Whether a fire's installation is live now decides whether it is fed: cut when the power goes, fed again when it returns.</summary>
        private void SyncFeeds(string by)
        {
            syncingFeeds = true;
            foreach (var fire in fires)
            {
                if (!fire.Electric || fire.Extinguished || fire.Installation == null) continue;
                bool live = LiveInstallation(fire.Installation);
                if (!live && fire.Feed != null) fire.CutFeed(by);
                else if (live && fire.Feed == null && fire.Restore())
                {
                    log.Add("불이 다시 먹이를 얻음 · 차단했던 전원을 다시 넣음 · " + fire.Where);
                    if (Guided) Office("역무실입니다. 점검도 끝나기 전에 전원을 다시 넣으면 불이 되살아납니다! 차단기를 내려 주십시오.");
                }
            }
            syncingFeeds = false;
        }

        /// <summary>The electrician (or the fire service) cut a feed by hand at the scene: the breaker goes down with it.</summary>
        private void OnFeedCut(FireHazard fire)
        {
            if (syncingFeeds || fire.Installation == null) return;
            if (fire.Installation.Kind == "distribution_board")
            {
                var board = ElectricNetwork.BoardOf(fire.Installation);
                if (board != null) ElectricNetwork.Switch(board.Main, false, fire.CutBy);
                return;
            }
            var circuit = ElectricNetwork.CircuitOf(fire.Installation);
            if (circuit != null) ElectricNetwork.Switch(circuit, false, fire.CutBy);
            else fire.Installation.GetComponent<ElectricLoad>()?.Refresh();
        }

        private string BoardBlocked(ElectricNetwork.Board board) =>
            fires.Exists(f => f.Installation == board.Equipment && !f.Extinguished && f.Intensity > .3f) ? (Guided ? "불꽃과 연기가 새어 나와 열 수 없습니다 · 아크 위험, 소화기로 먼저 끄십시오" : "불꽃과 연기가 새어 나와 열 수 없습니다") : null;

        private string LoadBlocked(StationEquipment machine) =>
            fires.Exists(f => f.Installation == machine && !f.Extinguished && f.Intensity > .45f) ? (Guided ? "불길이 커서 가까이 갈 수 없습니다 · 분전반에서 차단기를 내리십시오" : "불길이 커서 가까이 갈 수 없습니다") : null;

        /// <summary>Water reached live equipment: the stream conducts back to the hand that holds the nozzle.</summary>
        private void OnWetWhileLive(FireHazard fire)
        {
            session.Hud.Toast("감전! 전기가 통하는 " + fire.Subject + "에 물을 뿌렸습니다 · 관창을 놓쳤습니다", 5f);
            session.Hands.Drop();
            log.Add("감전 위험 · 통전된 " + fire.Subject + "에 방수해 관창을 놓침 · " + fire.Where);
            if (Guided) Office("역무실입니다. 전기가 살아 있는 곳에 물을 쓰면 안 됩니다! 전원 차단이 먼저입니다.");
        }

        // ── 전개 ──

        partial void ElectricPlazaDevelopments(List<Transition> list)
        {
            foreach (var fire in fires)
            {
                if (fire.Extinguished || fire.Installation == null) continue;
                var f = fire;
                if (!f.Electric)
                {
                    // 쓰레기통 불은 바로 옆의 다른 통(휴지통 옆 분리수거함)으로 번질 수 있다.
                    var next = BinNeighbour(f);
                    if (next != null && Ready("bin_spreads_" + f.Id))
                        list.Add(new Transition
                        {
                            Key = "bin_spreads_" + f.Id, Kind = "bin_fire_spreads",
                            Description = "The fire in the " + f.Subject + " at " + f.Where + " reaches the " + next.Label + " " + next.Id + " standing right beside it (paper, cups and plastic bottles carry it across the gap)",
                            Apply = _ =>
                            {
                                if (Still(!f.Extinguished && next.State != "소손" && !FireBurning(next), next.Label + " " + next.Id)) log.Add("쓰레기통 불이 옆 통으로 옮겨붙음 · " + StartEquipmentFire(next, Mathf.Clamp01(f.Intensity), "옆 통에서 불이 옮겨붙음").Where);
                            },
                        });
                    continue;
                }
                if (f.Feed != null && Ready("trip_" + f.Id))
                    list.Add(new Transition
                    {
                        Key = "trip_" + f.Id, Kind = "protection_trips",
                        Description = "The arcing fault in the burning " + f.Subject + " at " + f.Where + " draws enough current for the protection upstream to trip and cut its power by itself (a loose-contact fault often does not trip anything)",
                        Apply = _ => { if (Still(!f.Extinguished && f.Feed != null, f.Subject + " " + f.Where)) ProtectionTrips(f); },
                    });
                var neighbour = f.Feed != null ? NeighbourOf(f) : null;
                if (neighbour != null && Ready("spreads_" + f.Id))
                    list.Add(new Transition
                    {
                        Key = "spreads_" + f.Id, Kind = "equipment_fire_spreads",
                        Description = "The fire in the " + f.Subject + " at " + f.Where + " reaches the " + neighbour.Label + " " + ElectricNetwork.Tag(neighbour) + " standing right beside it (plastic panels and packaging carry it across the gap)",
                        Apply = _ => { if (Still(!f.Extinguished && !FireBurning(neighbour) && !(neighbour.GetComponent<ElectricLoad>() is ElectricLoad load && load.Burnt), neighbour.Label + " " + neighbour.Id)) SpreadTo(neighbour, f); },
                    });
                var close = f.Feed != null ? NearestPerson(f.Position, 1.8f, p => p.Current != Passenger.Activity.Evacuate && !p.Hurt && !p.Hostile) : null;
                if (close != null && Ready("shock"))
                    list.Add(new Transition
                    {
                        Key = "shock_" + close.Number, Kind = "electric_shock",
                        Description = Profile(close) + ", standing next to the live burning " + f.Subject + " at " + f.Where + ", touches its casing and gets an electric shock",
                        Apply = _ => { if (Still(!f.Extinguished && f.Feed != null && !close.Hurt && close.Current != Passenger.Activity.Evacuate && Flat(close.transform.position - f.Position).magnitude < 2.2f, "감전 후보 승객 #" + close.Number)) Shock(close, f); },
                    });
            }
            // 터진 배관의 물이 바닥에 번져 설비 밑동에 닿으면 배선이 젖어 합선한다(분전반은 벽에 높이 달려 닿지 않는다).
            foreach (var leak in leaks)
            {
                if (leak.Stopped || !leak.Active || leak.Level < 1) continue;
                var l = leak;
                var wet = Loads().Where(e => !BurningIn(e) && ElectricNetwork.Powered(e) && Mathf.Abs(e.transform.position.y - l.Position.y) < 2f && Flat(e.transform.position - l.Position).magnitude < l.Radius + .4f)
                    .OrderBy(e => e.Id, System.StringComparer.Ordinal).FirstOrDefault();
                if (wet == null || !Ready("wet_" + wet.Id)) continue;
                list.Add(new Transition
                {
                    Key = "wet_" + wet.Id, Kind = "wet_equipment_short", Levels = LevelsOf(wet).ToList(),
                    Description = "Water from the burst pipe at " + l.Where + " has spread across the floor to the base of the " + wet.Label + " " + ElectricNetwork.Tag(wet) + " and gets into its wiring: it shorts",
                    Apply = m => { if (Still(!BurningIn(wet) && ElectricNetwork.Powered(wet), wet.Label + " " + wet.Id)) StartEquipmentFire(wet, m, "누수로 젖어 합선"); },
                });
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        private void ProtectionTrips(FireHazard fire)
        {
            if (fire.Installation.Kind == "distribution_board")
            {
                var board = ElectricNetwork.BoardOf(fire.Installation);
                if (board != null) ElectricNetwork.Switch(board.Main, false, "보호장치");
            }
            else
            {
                var circuit = ElectricNetwork.CircuitOf(fire.Installation);
                if (circuit != null) ElectricNetwork.Switch(circuit, false, "보호장치");
                else if (fire.Installation.GetComponent<ElectricLoad>() is ElectricLoad load) { fire.CutFeed("보호장치"); load.Refresh(); }
            }
            if (fire.Feed != null) fire.CutFeed("보호장치");
            log.Add("보호장치가 동작해 전원이 차단됨 · " + fire.Where);
        }

        /// <summary>The machine (or kiosk) right beside a burning one that could catch from it.</summary>
        private static StationEquipment NeighbourOf(FireHazard fire)
        {
            if (fire.Installation.Kind == "distribution_board") return null;
            var near = new List<StationEquipment>();
            EquipmentRegistry.Within("vending_machine", fire.Installation.transform.position, 1.8f, near);
            EquipmentRegistry.Within("charging_kiosk", fire.Installation.transform.position, 1.8f, near);
            return near.Where(e => e != fire.Installation && Mathf.Abs(e.transform.position.y - fire.Installation.transform.position.y) < 1f && !(e.GetComponent<ElectricLoad>() is ElectricLoad load && load.Burnt))
                .OrderBy(e => (e.transform.position - fire.Installation.transform.position).sqrMagnitude).ThenBy(e => e.Id, System.StringComparer.Ordinal).FirstOrDefault(e => !FireBurning(e));
        }

        private static bool FireBurning(StationEquipment installation)
        {
            foreach (var hazard in HazardRegistry.Active) if (hazard is FireHazard fire && fire.Installation == installation && !fire.Extinguished) return true;
            return false;
        }

        private void SpreadTo(StationEquipment neighbour, FireHazard from)
        {
            var next = StartEquipmentFire(neighbour, Mathf.Clamp01(from.Intensity), "옆 설비에서 불이 옮겨붙음");
            log.Add("설비 화재가 옆 설비로 옮겨붙음 · " + next.Where);
        }

        private void Shock(Passenger person, FireHazard fire)
        {
            person.Injure("감전 (통전된 " + fire.Subject + "에 접촉)", true);
            log.Add("감전 · 통전된 " + fire.Subject + "에 손을 댄 승객 1명 쓰러짐 · " + fire.Where);
        }

        // ── 규칙 ──

        partial void ElectricPlazaTick(float dt)
        {
            for (int i = officeLines.Count - 1; i >= 0; i--)
            {
                if (Time.time < officeLines[i].at) continue;
                Office(officeLines[i].text);
                officeLines.RemoveAt(i);
            }
            foreach (var fire in fires)
            {
                if (fire.Installation == null) continue;
                if (fire.Extinguished)
                {
                    if (burntNoted.Add(fire)) BurntOut(fire);
                    continue;
                }
                if (!fire.Electric) continue;
                // 불씨까지만 죽고 더는 안 죽으면 전원이 살아 있는 것이다: 역무실이 한 번 알려 준다.
                if (Guided && Stage == Phase.Incident && fire.Feed != null && fire.Intensity <= FireHazard.LiveEmbers + .01f && fire.SuppressedSeconds > 0 && embersHinted.Add(fire))
                    Office("역무실입니다. 불씨가 계속 살아나면 전기가 살아 있는 겁니다. 분말로 끄는 것보다 차단기를 내리거나 전원 코드를 뽑는 게 먼저입니다.");
            }
            // 소방대는 통전 화재의 전원 차단을 요청한다: 역무원이 아직 전기 담당을 부르지 않았어도 온다.
            if (Stage == Phase.Incident && !brigadeAskedForCut && arrived.Contains(Agency.Fire) && !calledBy.ContainsKey(Agency.Facility) && fires.Exists(f => f.Electric && f.Feed != null && !f.Extinguished))
            {
                brigadeAskedForCut = true;
                Office("역무실입니다. 소방대가 전원 차단을 요청했습니다. 전기 담당을 현장으로 보내겠습니다.");
                // 표준·실전: 소방대가 말한 그 불로 전기 담당이 간다.
                var live = fires.Find(f => f.Electric && f.Feed != null && !f.Extinguished);
                Call(Agency.Facility, "소방대 전원 차단 요청", Guided ? null : live, Guided ? (Team?)null : Team.Electric);
            }
        }

        /// <summary>The fire is out: a machine that burnt stays dead, a board that burnt takes everything it feeds with it.</summary>
        private void BurntOut(FireHazard fire)
        {
            var installation = fire.Installation;
            var load = installation.GetComponent<ElectricLoad>();
            if (load != null)
            {
                load.OnFire = false;
                load.Burn();
            }
            else
            {
                installation.State = "소손";
                var board = ElectricNetwork.BoardOf(installation);
                if (board != null)
                {
                    board.Damaged = true;
                    ElectricNetwork.Switch(board.Main, false, "화재로 소손");
                    log.Add("분전반 소손 · " + board.Name + " 이(가) 붙은 설비가 모두 꺼짐");
                }
            }
            if (Guided && Electrical(installation)) officeLines.Add((Time.time + 6f, "역무실입니다. 전기 담당이 점검하기 전에는 그 차단기를 다시 올리지 마십시오."));
        }

        /// <summary>The bin (or recycling station) right beside a burning bin that could catch from it.</summary>
        private static StationEquipment BinNeighbour(FireHazard fire)
        {
            var at = fire.Installation.transform.position;
            var near = new List<StationEquipment>();
            EquipmentRegistry.Within("litter_bin", at, 1.2f, near);
            EquipmentRegistry.Within("recycling_bin", at, 1.2f, near);
            return near.Where(e => e != fire.Installation && Mathf.Abs(e.transform.position.y - at.y) < 1f && e.State != "소손" && !FireBurning(e))
                .OrderBy(e => (e.transform.position - at).sqrMagnitude).ThenBy(e => e.Id, System.StringComparer.Ordinal).FirstOrDefault();
        }

        // ── 무전과 화면 ──

        partial void ElectricPlazaRadio(List<EmergencySession.RadioOption> options)
        {
            foreach (var fire in fires.Where(f => f.Electric && f.Installation != null && f.CutBy == StaffCut && !cutReported.Contains(f)).Take(1))
            {
                var f = fire;
                options.Add(Option("역무실 · " + f.Subject + " 전원 차단 보고", () => ReportCut(f)));
            }
        }

        private void ReportCut(FireHazard fire)
        {
            cutReported.Add(fire);
            Say("역무실, " + fire.Where + " " + HowCut(fire) + " 전원 차단했습니다.");
            Office("역무실 수신. 전기 담당이 점검하러 갑니다. 점검이 끝날 때까지 차단기는 올리지 마십시오.");
            log.Add("역무실에 전원 차단 보고 · " + fire.Where);
            Call(Agency.Facility, "역무원 전원 차단 보고");
        }

        private static string HowCut(FireHazard fire)
        {
            var installation = fire.Installation;
            if (installation.Kind == "distribution_board")
            {
                var board = ElectricNetwork.BoardOf(installation);
                return board != null ? "분전반 " + board.Code + " 메인 차단기를 내려" : "분전반 차단기를 내려";
            }
            var load = installation.GetComponent<ElectricLoad>();
            var circuit = ElectricNetwork.CircuitOf(installation);
            if (load != null && load.LocalOff) return load.Kiosk ? "전원 스위치를 꺼서" : "전원 코드를 뽑아";
            return circuit != null ? "분전반 " + circuit.Label + " 차단기를 내려" : "전원을 끊어";
        }

        private BoardOverlay.Column ElectricColumn()
        {
            var burning = fires.Where(f => f.Electric && !f.Extinguished).ToList();
            var cut = ElectricNetwork.Boards.SelectMany(b => b.All).Where(c => !c.On).ToList();
            if (burning.Count == 0 && cut.Count == 0) return null;
            var column = new BoardOverlay.Column { Title = "전기" };
            foreach (var f in burning) column.Lines.Add((f.Feed == null ? "● " : "○ ") + "전원 차단 · " + f.Subject + (f.Feed == null ? " (" + f.CutBy + ")" : " 통전 중"));
            foreach (var c in cut.Take(6)) column.Lines.Add("꺼짐 · " + c.Label + " " + c.Name);
            return column;
        }
    }
}
