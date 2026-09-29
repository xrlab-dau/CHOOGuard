using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Work;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the ElectricPlaza equipment goes: the standards' spacing rules applied to the twin's measured wall spots
    /// (<see cref="WallSpots"/>), deterministically (same twin, same file). A rule keeps clear of doors, exits, escalators,
    /// elevators, fire fittings and the places people stand and sit, leaves the 1.2 m walking width in front that the
    /// accessibility rules ask for, and puts each kind where it is used: boards at the cores (toilets, office, elevators, staff
    /// doors), vending machines and kiosks where people wait, bins at exits, shops and seating. Nothing is drawn at random.
    /// </summary>
    internal sealed class ElectricPlazaPlacement
    {
        /// <summary>Walking width kept free beside any fitting (m): access routes are at least 1.2 m wide (accessibility act, enforcement rule appendix 1).</summary>
        private const float Passage = 1.2f;

        private readonly StationPoints points;
        private readonly WallSpots.Survey survey;
        private readonly Vector3[] publicDoors, staffDoors, tenantDoors, elevatorDoors, exits, toilets, cores, seats, shops, escalatorEnds, fittings, people, doors, entrances;
        private readonly List<Placed> placed = new List<Placed>();
        private readonly Dictionary<string, int> serial = new Dictionary<string, int>(), boards = new Dictionary<string, int>();

        private struct Placed { public Vector3 Centre; public float Radius; public string Kind; }

        /// <summary>One kind of fitting and the rules for it.</summary>
        private sealed class Rule
        {
            public string Kind, Prefab, Label, IdPrefix;
            /// <summary>Footprint on the floor: width along the wall, depth out from it (m).</summary>
            public float Width, Depth;
            /// <summary>Free floor needed in front of the wall (m).</summary>
            public float MinFree;
            /// <summary>Least distance between two of this kind (m).</summary>
            public float Spacing;
            /// <summary>Clearances from doors (public, staff), tenant shop-front doors, exits, escalator ends, elevator doors, fire fittings, people's places (m).</summary>
            public float Door, TenantDoor, Exit, Escalator, Elevator, Fitting, Person;
            public Func<WallSpots.Spot, float> Score;
            public Func<string, int> Quota;
            /// <summary>The prefab pivot is the bottom centre (not the back face): stand it half its depth off the wall.</summary>
            public bool CentredPivot;
            /// <summary>A second fitting stands beside the first (a bank of vending machines, a litter bin by the recycling station).</summary>
            public Rule Beside;
        }

        public ElectricPlazaPlacement(StationPoints stationPoints, WallSpots.Survey wallSurvey)
        {
            points = stationPoints;
            survey = wallSurvey;
            Vector3[] Doors(Func<StationDoor, bool> filter) =>
                UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsSortMode.None).Where(filter).Select(d => d.transform.TransformPoint(d.Centre)).OrderBy(p => p.x).ThenBy(p => p.z).ToArray();
            publicDoors = Doors(d => d.Use == StationDoor.DoorUse.Public && d.Kind != StationDoor.DoorKind.Elevator);
            staffDoors = Doors(d => d.Use == StationDoor.DoorUse.Staff);
            tenantDoors = Doors(d => d.Use == StationDoor.DoorUse.Tenant);
            elevatorDoors = points.Elevators.SelectMany(e => e.stops).Select(s => s.door).ToArray();
            Vector3[] Of(params PointKind[] kinds) => kinds.SelectMany(k => points.Of(k)).Select(p => p.Position).ToArray();
            exits = Of(PointKind.Exit);
            toilets = Of(PointKind.Toilet);
            shops = Of(PointKind.Shop, PointKind.Counter);
            seats = Of(PointKind.Seat, PointKind.Chair, PointKind.Wait, PointKind.PlatformWait);
            people = Of(PointKind.Seat, PointKind.Chair, PointKind.Wait, PointKind.PlatformWait, PointKind.Shop, PointKind.Counter, PointKind.Toilet, PointKind.Meet, PointKind.Office);
            cores = toilets.Concat(Of(PointKind.Office)).Concat(elevatorDoors).Concat(staffDoors).ToArray();
            escalatorEnds = points.Escalators.SelectMany(e => new[] { e.path[0], e.path[e.path.Length - 1], e.stairsTop, e.stairsBottom }).Where(p => p != Vector3.zero).ToArray();
            doors = publicDoors.Concat(staffDoors).ToArray();
            entrances = publicDoors.Concat(exits).ToArray();
            // 소화기·옥내소화전·발신기·AED 함: 앞을 가리지 않는다.
            fittings = UnityEngine.Object.FindObjectsByType<StationFixture>(FindObjectsSortMode.None).Select(f => f.transform.position)
                .Concat(UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None).Select(f => f.transform.position)).ToArray();
        }

        private static float Near(Vector3 p, Vector3[] set)
        {
            float best = float.MaxValue;
            foreach (var q in set)
            {
                if (Mathf.Abs(q.y - p.y) > 3f) continue;
                float dx = q.x - p.x, dz = q.z - p.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best) best = d;
            }
            return best;
        }

        private static int Within(Vector3 p, Vector3[] set, float radius)
        {
            int count = 0;
            foreach (var q in set)
            {
                if (Mathf.Abs(q.y - p.y) > 3f) continue;
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz <= radius * radius) count++;
            }
            return count;
        }

        /// <summary>Walkable floor of a zone in m² (the navmesh), zero when it has none.</summary>
        private float Area(string zone) => survey.Area.TryGetValue(zone, out float area) ? area : 0;

        private static bool Indoor(string zone) => zone != "plaza" && zone != "skyplaza";

        // ── 규칙 ──

        private Rule[] Rules() => new[] { BoardRule(), DrinkBank(), KioskRule(), RecyclingStation() };

        /// <summary>
        /// Distribution boards: on every floor, in the core (beside the toilets, the office, the elevators or a staff door) where
        /// the electrical shaft stands, one per 1,500 m² of floor (design guidance: one board per about 1,000 m² of office floor and
        /// branch circuits up to 30 m; a hall has fewer outlets than offices), never in a doorway, with the door's swing free.
        /// </summary>
        private Rule BoardRule() => new Rule
        {
            Kind = "distribution_board", Prefab = ElectricPlazaPrefabs.Board, Label = "분전반", IdPrefix = "board",
            Width = .56f, Depth = .37f, MinFree = 1.5f, Spacing = 26f,
            Door = 1.8f, TenantDoor = 2.5f, Exit = 4f, Escalator = 4f, Elevator = 2.5f, Fitting = 1.5f, Person = 1f,
            Quota = zone => Indoor(zone) ? Mathf.Clamp(Mathf.CeilToInt(Area(zone) / 1500f), 1, 12) : 0,
            Score = s =>
            {
                float core = Mathf.Min(Near(s.Wall, cores), 99f);
                return core <= 14f ? 1f + (14f - core) / 14f * 2f : .1f;
            },
        };

        /// <summary>
        /// Vending machines stand in banks of two (a drink and a snack machine side by side) on walls where people wait: near seats and
        /// shops, well away from exits, escalators and shop fronts, with a clear walking width in front.
        /// </summary>
        private Rule DrinkBank()
        {
            var snack = new Rule { Kind = "vending_machine", Prefab = ElectricPlazaPrefabs.VendingSnack, Label = "스낵 자동판매기", IdPrefix = "vending", Width = .93f, Depth = .95f };
            return new Rule
            {
                Kind = "vending_machine", Prefab = ElectricPlazaPrefabs.VendingDrink, Label = "음료 자동판매기", IdPrefix = "vending", Width = .96f, Depth = .54f,
                MinFree = .95f + Passage + .3f, Spacing = 22f, Door = 2f, TenantDoor = 3f, Exit = 6f, Escalator = 5f, Elevator = 4f, Fitting = 2f, Person = 1.2f,
                Beside = snack,
                Quota = zone => zone == "tracks" ? 2 : Indoor(zone) ? Mathf.Clamp(Mathf.CeilToInt(Area(zone) / 3500f), Area(zone) > 1000f ? 1 : 0, 4) : 0,
                Score = s =>
                {
                    int crowd = Within(s.Wall, seats, 14f);
                    if (crowd < 4) return 0;
                    return .5f + Mathf.Min(crowd, 40) / 40f + (Near(s.Wall, shops) < 10f ? .3f : 0);
                },
            };
        }

        /// <summary>Phone-charging kiosks: one per about 5,000 m² next to the seating, where people wait with a phone in hand.</summary>
        private Rule KioskRule() => new Rule
        {
            Kind = "charging_kiosk", Prefab = ElectricPlazaPrefabs.ChargingKiosk, Label = "휴대폰 충전 키오스크", IdPrefix = "kiosk",
            Width = .76f, Depth = .25f, MinFree = Passage + .9f, Spacing = 28f, Door = 2f, TenantDoor = 3f, Exit = 6f, Escalator = 5f, Elevator = 4f, Fitting = 2f, Person = 1.2f,
            Quota = zone => Indoor(zone) && zone != "tracks" ? Mathf.Clamp(Mathf.CeilToInt(Area(zone) / 3500f), Area(zone) > 1500f ? 1 : 0, 4) : 0,
            Score = s =>
            {
                int crowd = Within(s.Wall, seats, 12f);
                return crowd < 8 ? 0 : .5f + Mathf.Min(crowd, 60) / 60f;
            },
        };

        /// <summary>
        /// Recycling stations with a litter bin beside them: at the entrances and exits, near the toilets and the shops and among the
        /// seating, one per about 3,000 m², on platforms none (the platform has plain litter bins).
        /// </summary>
        private Rule RecyclingStation()
        {
            var bin = new Rule { Kind = "litter_bin", Prefab = ElectricPlazaPrefabs.LitterBin, Label = "휴지통", IdPrefix = "bin", Width = .43f, Depth = .43f };
            return new Rule
            {
                Kind = "recycling_bin", Prefab = ElectricPlazaPrefabs.RecyclingBin, Label = "분리수거함", IdPrefix = "recycling", Width = .94f, Depth = .38f, CentredPivot = true,
                MinFree = .4f + Passage, Spacing = 24f, Door = 1.5f, TenantDoor = 2f, Exit = 2.5f, Escalator = 4f, Elevator = 3f, Fitting = 1.5f, Person = 1f,
                Beside = bin,
                Quota = zone => zone != "tracks" && Indoor(zone) ? Mathf.Clamp(Mathf.FloorToInt(Area(zone) / 3000f), Area(zone) > 800f ? 1 : 0, 6) : 0,
                Score = s =>
                {
                    float exit = Near(s.Wall, entrances), toilet = Near(s.Wall, toilets), shop = Near(s.Wall, shops);
                    float use = Mathf.Max(exit < 12f ? (12f - exit) / 12f : 0, toilet < 8f ? (8f - toilet) / 8f : 0, shop < 8f ? (8f - shop) / 8f * .8f : 0);
                    int crowd = Within(s.Wall, seats, 12f);
                    return use + Mathf.Min(crowd, 30) / 60f > .25f ? .3f + use + Mathf.Min(crowd, 30) / 60f : 0;
                },
            };
        }

        // ── 선택 ──

        /// <summary>Places every rule and returns the placement entries.</summary>
        public List<EquipmentPlacement> Place()
        {
            var items = new List<EquipmentPlacement>();
            foreach (var rule in Rules()) PlaceRule(rule, items);
            PlaceLitterBins(items);
            PlaceOutdoors(items);
            return items;
        }

        private bool Passes(Rule rule, WallSpots.Spot s)
        {
            if (s.FlatPlus < rule.Width * .5f + .05f || s.FlatMinus < rule.Width * .5f + .05f || s.Free < rule.MinFree) return false;
            if (Near(s.Wall, doors) < rule.Door || Near(s.Wall, tenantDoors) < rule.TenantDoor) return false;
            if (Near(s.Wall, exits) < rule.Exit || Near(s.Wall, escalatorEnds) < rule.Escalator || Near(s.Wall, elevatorDoors) < rule.Elevator) return false;
            if (Near(s.Wall, fittings) < rule.Fitting || Near(s.Wall + s.Normal * (rule.Depth * .5f + .5f), people) < rule.Person) return false;
            return true;
        }

        private static Vector3 Centre(Rule rule, WallSpots.Spot s, float along) =>
            s.Wall + s.Tangent * along + s.Normal * (rule.CentredPivot ? rule.Depth * .5f + .02f : .01f);

        private bool Free(Vector3 centre, float radius)
        {
            foreach (var p in placed)
                if (Mathf.Abs(p.Centre.y - centre.y) < 3f && Vector2.Distance(new Vector2(p.Centre.x, p.Centre.z), new Vector2(centre.x, centre.z)) < p.Radius + radius + .05f) return false;
            return true;
        }

        private void PlaceRule(Rule rule, List<EquipmentPlacement> items)
        {
            var count = new Dictionary<string, int>();
            var candidates = survey.Spots.Where(s => Passes(rule, s)).Select(s => (spot: s, score: rule.Score(s))).Where(c => c.score > 0)
                .OrderByDescending(c => c.score).ThenBy(c => c.spot.Wall.x).ThenBy(c => c.spot.Wall.z).ToList();
            foreach (var (spot, _) in candidates)
            {
                count.TryGetValue(spot.Zone, out int have);
                if (have >= rule.Quota(spot.Zone)) continue;
                var centre = Centre(rule, spot, 0);
                float radius = Mathf.Max(rule.Width, rule.Depth) * .5f;
                if (placed.Any(p => p.Kind == rule.Kind && Mathf.Abs(p.Centre.y - centre.y) < 3f && Vector2.Distance(new Vector2(p.Centre.x, p.Centre.z), new Vector2(centre.x, centre.z)) < rule.Spacing)) continue;
                if (!Free(centre, radius)) continue;
                // 옆에 하나 더(자판기 두 대·수거함과 휴지통): 벽이 그만큼 이어져야 한다.
                float offset = 0;
                if (rule.Beside != null)
                {
                    float need = rule.Width * .5f + .03f + rule.Beside.Width;
                    if (spot.FlatPlus >= need + .05f && spot.FlatMinus >= rule.Width * .5f + .05f) offset = rule.Width * .5f + .03f + rule.Beside.Width * .5f;
                    else if (spot.FlatMinus >= need + .05f && spot.FlatPlus >= rule.Width * .5f + .05f) offset = -(rule.Width * .5f + .03f + rule.Beside.Width * .5f);
                    else continue;
                    if (!Free(Centre(rule.Beside, spot, offset), Mathf.Max(rule.Beside.Width, rule.Beside.Depth) * .5f)) continue;
                }
                count[spot.Zone] = have + 1;
                Add(rule, spot, 0, items);
                if (rule.Beside != null) Add(rule.Beside, spot, offset, items);
            }
        }

        private void Add(Rule rule, WallSpots.Spot spot, float along, List<EquipmentPlacement> items)
        {
            var centre = Centre(rule, spot, along);
            placed.Add(new Placed { Centre = centre, Radius = Mathf.Max(rule.Width, rule.Depth) * .5f, Kind = rule.Kind });
            string key = rule.IdPrefix + "-" + spot.Zone;
            serial[key] = serial.TryGetValue(key, out int n) ? n + 1 : 1;
            string label = rule.Kind == "distribution_board" ? BoardLabel(spot.Zone) : rule.Label;
            items.Add(new EquipmentPlacement
            {
                id = key + "-" + serial[key].ToString("00"), kind = rule.Kind, label = label, zone = spot.Zone, prefab = rule.Prefab,
                position = centre, rotation = new Vector3(0, Mathf.Atan2(spot.Normal.x, spot.Normal.z) * Mathf.Rad2Deg, 0),
            });
        }

        /// <summary>Korean panel designations: LP = lighting and outlet panel, then the floor and a running number over the whole floor (LP-2F-03). Platforms: LP-P-nn.</summary>
        private string BoardLabel(string zone)
        {
            string floor = zone == "ground1f" ? "1F" : zone == "upper3f" ? "3F" : zone == "tracks" ? "P" : "2F";
            boards[floor] = boards.TryGetValue(floor, out int n) ? n + 1 : 1;
            return "분전반 LP-" + floor + "-" + boards[floor].ToString("00");
        }

        /// <summary>
        /// Plain litter bins stand by themselves too (one per about 700 m²): along walls at the exits, the toilets, the shops and the
        /// seating, and on the platforms.
        /// </summary>
        private void PlaceLitterBins(List<EquipmentPlacement> items)
        {
            var rule = new Rule
            {
                Kind = "litter_bin", Prefab = ElectricPlazaPrefabs.LitterBin, Label = "휴지통", IdPrefix = "bin", Width = .43f, Depth = .43f, MinFree = .4f + Passage, Spacing = 9f,
                Door = 1.2f, TenantDoor = 1.5f, Exit = 2f, Escalator = 3f, Elevator = 2.5f, Fitting = 1.2f, Person = .8f,
                Quota = zone => Indoor(zone) ? Mathf.Clamp(Mathf.FloorToInt(Area(zone) / 700f), Area(zone) > 300f ? 1 : 0, 24) : 0,
                Score = s =>
                {
                    float exit = Near(s.Wall, entrances), toilet = Near(s.Wall, toilets), shop = Near(s.Wall, shops);
                    float use = Mathf.Max(exit < 10f ? (10f - exit) / 10f : 0, toilet < 6f ? (6f - toilet) / 6f : 0, shop < 6f ? (6f - shop) / 6f : 0);
                    return .2f + use + Mathf.Min(Within(s.Wall, seats, 10f), 20) / 40f;
                },
            };
            PlaceRule(rule, items);
        }

        /// <summary>
        /// The plaza and the sky plaza have no walls to stand against: the fittings stand on the paving inside each exit, facing the
        /// people who come out (a recycling station with a litter bin), and the smoking area of the plaza — the station has no indoor
        /// smoking room; the designated place is outdoors at the side of the exit toward Choryang, by the taxi stand — gets two cigarette-butt
        /// bins and a litter bin. Positions come from the navmesh under the exit and are only kept where nothing solid stands.
        /// </summary>
        private void PlaceOutdoors(List<EquipmentPlacement> items)
        {
            foreach (var exit in points.Of(PointKind.Exit).Where(e => !Indoor(e.Zone)).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var into = points.Nearest(PointKind.Wait, exit.Position);
                var inward = into != null ? new Vector3(into.Position.x - exit.Position.x, 0, into.Position.z - exit.Position.z).normalized : Vector3.forward;
                var side = Vector3.Cross(Vector3.up, inward);
                bool smoking = exit.Id == "exit-west-north";
                var stations = new List<(Rule rule, Vector3 at, float yaw)>();
                var recycling = RecyclingStation();
                var bin = recycling.Beside;
                var ash = new Rule { Kind = "ash_bin", Prefab = ElectricPlazaPrefabs.AshBin, Label = "담배꽁초 수거함", IdPrefix = "ash", Width = .37f, Depth = .36f, CentredPivot = true };
                float back = Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg;
                // 출구 안쪽 4 m 에서 사람들을 마주 보게.
                stations.Add((recycling, exit.Position + inward * 4f + side * 1.6f, back));
                stations.Add((bin, exit.Position + inward * 4f + side * 2.6f, back));
                if (smoking)
                {
                    stations.Add((ash, exit.Position + inward * 6.5f - side * 3.2f, back));
                    stations.Add((ash, exit.Position + inward * 6.5f - side * 4.0f, back));
                    stations.Add((bin, exit.Position + inward * 6.5f - side * 4.9f, back));
                }
                foreach (var (rule, at, yaw) in stations)
                {
                    if (!NavMesh.SamplePosition(at, out var hit, .6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - exit.Position.y) > 1.2f) continue;
                    var centre = hit.position;
                    if (Physics.CheckSphere(centre + Vector3.up * .6f, .4f, ~0, QueryTriggerInteraction.Ignore) || !Free(centre, Mathf.Max(rule.Width, rule.Depth) * .5f)) continue;
                    placed.Add(new Placed { Centre = centre, Radius = Mathf.Max(rule.Width, rule.Depth) * .5f, Kind = rule.Kind });
                    string key = rule.IdPrefix + "-" + exit.Zone + "-" + exit.Id;
                    serial[key] = serial.TryGetValue(key, out int n) ? n + 1 : 1;
                    items.Add(new EquipmentPlacement
                    {
                        id = key + "-" + serial[key].ToString("00"), kind = rule.Kind, label = rule.Label, zone = exit.Zone, prefab = rule.Prefab,
                        position = centre, rotation = new Vector3(0, yaw, 0),
                    });
                }
            }
        }
    }
}
