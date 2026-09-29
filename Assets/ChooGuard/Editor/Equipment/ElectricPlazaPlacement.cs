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

        /// <summary>Gap kept between two different fittings (m), so a bank of machines, a kiosk and a bin never crowd each other.</summary>
        private const float Clearance = .8f;

        /// <summary>Wall that must continue beside a fitting on each side (m), so nothing stands wedged into a corner, a glass wall or a door frame.</summary>
        private const float Side = .3f;

        /// <summary>Glass (a shop front, a window wall) must be at least this far from the edge of a fitting (m), so a bin never stands half behind a pane.</summary>
        private const float GlassClearance = 1f;

        private readonly StationPoints points;
        private readonly WallSpots.Survey survey;
        private readonly Vector3[] publicDoors, staffDoors, tenantDoors, elevatorDoors, exits, toilets, cores, seats, shops, escalatorEnds, fittings, people, doors, entrances, carDoors;
        private readonly List<Placed> placed = new List<Placed>();
        private readonly Dictionary<string, int> serial = new Dictionary<string, int>(), boards = new Dictionary<string, int>();
        private readonly List<Bounds> tactile = new List<Bounds>();
        /// <summary>How many candidate spots (or accepted-then-dropped candidates) each rule removed, by "kind: rule": the builder logs it.</summary>
        public readonly SortedDictionary<string, int> Rejected = new SortedDictionary<string, int>(StringComparer.Ordinal);
        /// <summary>One row per placed item with its measured clearances (m): the builder writes them next to the log for the views tool and the review.</summary>
        public readonly List<string> Clearances = new List<string>();
        /// <summary>One line per (kind, zone) whose quota was not met, with the placed count and the three rules that removed the most candidates there: the builder logs them.</summary>
        public readonly List<string> Unmet = new List<string>();

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
            /// <summary>Most of the nine fan rays in front that may hit something (<see cref="WallSpots.Spot.Enclosed"/>): public fittings stand in the open, not in alcoves or shops.</summary>
            public int MaxEnclosed = 9;
            public Func<WallSpots.Spot, float> Score;
            public Func<string, int> Quota;
            /// <summary>The prefab pivot is the bottom centre (not the back face): stand it half its depth off the wall.</summary>
            public bool CentredPivot;
            /// <summary>A second fitting stands beside the first (a bank of vending machines, a litter bin by the recycling station).</summary>
            public Rule Beside;
            /// <summary>Real height of the model and how far above the floor it hangs (a distribution board), for the collision box.</summary>
            public float Height, Elevation;
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
            carDoors = points.Train != null ? points.Train.cars.Where(c => c.reachable).Select(c => c.door.outside).ToArray() : Array.Empty<Vector3>();
            doors = publicDoors.Concat(staffDoors).ToArray();
            entrances = publicDoors.Concat(exits).ToArray();
            // 소화기·옥내소화전·발신기·AED 함: 앞을 가리지 않는다.
            fittings = UnityEngine.Object.FindObjectsByType<StationFixture>(FindObjectsSortMode.None).Select(f => f.transform.position)
                .Concat(UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None).Select(f => f.transform.position)).ToArray();
            // 점자블록(노란 촉각 포장)은 걷는 길이다: 재질 이름으로 찾는다.
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name.IndexOf("tactile", StringComparison.OrdinalIgnoreCase) >= 0) { tactile.Add(renderer.bounds); break; }
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

        // ── 규칙과 개수 ──
        // 개수는 근거를 적는다. 공식은 면적 기준(설계 지침), 표는 설계 가정(공개 자료 없음)이다. 빌더 로그의 quota 표가 zone 별 결과를 보인다.

        private Rule[] Rules() => new[] { BoardRule(), DrinkBank(), KioskRule(), RecyclingStation() };

        /// <summary>Design assumption (no public plan): vending banks (a drink and a snack machine) per waiting area: the main hall 2, 1F arcade 2, platforms 2, every other floor zone 1.</summary>
        private static readonly Dictionary<string, int> Banks = new Dictionary<string, int> { { "hall2f", 2 }, { "main2f", 1 }, { "ground1f", 2 }, { "upper3f", 1 }, { "eastexit", 1 }, { "southgate", 1 }, { "northdeck", 1 }, { "tracks", 2 } };
        /// <summary>Design assumption: phone-charging kiosks beside seating: 2 in the main hall, 1 in each other seated zone.</summary>
        private static readonly Dictionary<string, int> Kiosks = new Dictionary<string, int> { { "hall2f", 2 }, { "main2f", 1 }, { "ground1f", 1 }, { "upper3f", 1 }, { "eastexit", 1 } };
        /// <summary>Recycling stations with a litter bin (KORAIL put drink-sorting bins in the Seoul Station hall, 2022; Busan is an assumption): main halls 2, 1F 3, others 1.</summary>
        private static readonly Dictionary<string, int> Stations = new Dictionary<string, int> { { "hall2f", 2 }, { "main2f", 2 }, { "ground1f", 3 }, { "upper3f", 1 }, { "eastexit", 1 }, { "southgate", 1 } };

        private static int Table(Dictionary<string, int> table, string zone) => table.TryGetValue(zone, out int n) ? n : 0;

        /// <summary>
        /// Distribution boards live in the cores (beside the toilets, the office, the elevators or a staff door: the electrical shaft),
        /// never on the open public floor, so a spot must be within 10 m of a core. Count: one per 4,000 m² of floor, at most 3 per
        /// zone (design guidance: a board on each floor in the core or shaft, about one per 1,000 m² of office floor; a concourse has
        /// only lighting, vending machines, kiosks and cleaning sockets to feed, so its density is lower — a stated assumption).
        /// </summary>
        private Rule BoardRule() => new Rule
        {
            Kind = "distribution_board", Prefab = ElectricPlazaPrefabs.Board, Label = "분전반", IdPrefix = "board",
            Width = .56f, Depth = .37f, Height = .8f, Elevation = ElectricPlazaPrefabs.BoardBottom, MinFree = 1.5f, Spacing = 26f,
            Door = 1.8f, TenantDoor = 2.5f, Exit = 4f, Escalator = 4f, Elevator = 3f, Fitting = 1.5f, Person = 1f,
            Quota = zone => Indoor(zone) ? Mathf.Clamp(Mathf.CeilToInt(Area(zone) / 4000f), 1, 3) : 0,
            Score = s =>
            {
                float core = Near(s.Wall, cores);
                return core <= 10f ? 1f + (10f - core) / 10f * 2f : 0;
            },
        };

        /// <summary>Vending machines stand in banks of two on walls where people wait: near seats and shops, away from exits, landings and shop fronts.</summary>
        private Rule DrinkBank()
        {
            var snack = new Rule { Kind = "vending_machine", Prefab = ElectricPlazaPrefabs.VendingSnack, Label = "스낵 자동판매기", IdPrefix = "vending", Width = .93f, Depth = .95f, Height = 1.85f };
            return new Rule
            {
                Kind = "vending_machine", Prefab = ElectricPlazaPrefabs.VendingDrink, Label = "음료 자동판매기", IdPrefix = "vending", Width = .96f, Depth = .54f, Height = 1.83f,
                MinFree = .95f + Passage + .3f, Spacing = 22f, Door = 2f, TenantDoor = 3f, Exit = 6f, Escalator = 5f, Elevator = 4f, Fitting = 2f, Person = 1.2f, MaxEnclosed = 6,
                Beside = snack,
                Quota = zone => Table(Banks, zone),
                Score = s =>
                {
                    int crowd = Within(s.Wall, seats, 14f);
                    if (crowd < 4) return 0;
                    return .5f + Mathf.Min(crowd, 40) / 40f + (Near(s.Wall, shops) < 10f ? .3f : 0);
                },
            };
        }

        /// <summary>Phone-charging kiosks: beside the seating, where people wait with a phone in hand.</summary>
        private Rule KioskRule() => new Rule
        {
            Kind = "charging_kiosk", Prefab = ElectricPlazaPrefabs.ChargingKiosk, Label = "휴대폰 충전 키오스크", IdPrefix = "kiosk",
            Width = .76f, Depth = .25f, Height = 1.8f, MinFree = Passage + .9f, Spacing = 28f, Door = 2f, TenantDoor = 3f, Exit = 6f, Escalator = 5f, Elevator = 4f, Fitting = 2f, Person = 1.2f, MaxEnclosed = 6,
            Quota = zone => Table(Kiosks, zone),
            Score = s =>
            {
                int crowd = Within(s.Wall, seats, 12f);
                return crowd < 8 ? 0 : .5f + Mathf.Min(crowd, 60) / 60f;
            },
        };

        /// <summary>Recycling stations with a litter bin beside them: at the entrances, near the toilets and the shops and among the seating; on platforms none.</summary>
        private Rule RecyclingStation()
        {
            var bin = new Rule { Kind = "litter_bin", Prefab = ElectricPlazaPrefabs.LitterBin, Label = "휴지통", IdPrefix = "bin", Width = .43f, Depth = .43f, Height = .85f };
            return new Rule
            {
                Kind = "recycling_bin", Prefab = ElectricPlazaPrefabs.RecyclingBin, Label = "분리수거함", IdPrefix = "recycling", Width = .94f, Depth = .38f, Height = 1.05f, CentredPivot = true,
                MinFree = .4f + Passage, Spacing = 24f, Door = 1.5f, TenantDoor = 2f, Exit = 2.5f, Escalator = 4f, Elevator = 3f, Fitting = 1.5f, Person = 1f, MaxEnclosed = 6,
                Beside = bin,
                Quota = zone => Table(Stations, zone),
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

        /// <summary>Why a spot cannot take this kind, or null when it can. Every rule that removes a spot is counted in <see cref="Rejected"/>.</summary>
        private string Reject(Rule rule, WallSpots.Spot s)
        {
            // 벽은 양옆으로 0.3 m 씩 더 이어져야 한다(폭 + 0.6 m): 모서리·유리벽·문틀에 붙어 낀 자리는 뺀다.
            // 승강장 비품은 기둥 곁에 자유롭게 서므로 기둥 면의 폭은 따지지 않는다(기둥은 비품보다 좁다).
            float half = rule.Width * .5f + Side;
            if (s.Zone != "tracks" && (s.FlatPlus < half || s.FlatMinus < half)) return "wall narrower than item + 0.6 m";
            if (s.Free < rule.MinFree) return "less than " + rule.MinFree.ToString("0.0") + " m free in front";
            if (s.Enclosed > rule.MaxEnclosed) return "alcove, shop interior or corridor";
            // 가게 안쪽 벽(계산대·손님 자리를 마주 보는 벽)은 가게 것이다: 역 비품이 서지 않는다.
            if (FacesShopFront(s)) return "wall faces a shop counter within 5 m";
            // 가게 유리는 비품(짝이 있으면 짝까지) 가장자리에서 1 m 안에 있으면 안 된다: 역 쓰레기통·자판기는 기둥·코어 곁에 둔다.
            float glassNeed = rule.Width * .5f + (rule.Beside != null ? rule.Beside.Width : 0f) + GlassClearance;
            if (s.GlassNearest < glassNeed) return "shop glass within 1 m of the item's edge";
            var platform = s.Zone == "tracks" ? PlatformReason(s) : null;
            if (platform != null) return platform;
            if (Near(s.Wall, doors) < rule.Door || Near(s.Wall, tenantDoors) < rule.TenantDoor) return "door clearance";
            if (Near(s.Wall, exits) < rule.Exit) return "exit clearance";
            if (Near(s.Wall, escalatorEnds) < (s.Zone == "tracks" ? Mathf.Max(rule.Escalator, 5f) : rule.Escalator)) return "stair/escalator landing clearance";
            if (Near(s.Wall, elevatorDoors) < rule.Elevator) return "elevator landing clearance";
            if (Near(s.Wall, fittings) < rule.Fitting) return "fire fitting clearance";
            if (Near(s.Wall + s.Normal * (rule.Depth * .5f + .5f), people) < rule.Person) return "people's places";
            if (OnTactile(Centre(rule, s, 0), Mathf.Max(rule.Width, rule.Depth) * .5f)) return "tactile paving within 0.6 m";
            if (!SeenFromFront(rule, s)) return "no clear line of sight from 2.8 m in front";
            return null;
        }

        /// <summary>Gap kept back from the edge of a platform (m): the tactile warning blocks lie 0.3–0.9 m from the edge (accessibility act, enforcement rule appendix 1); another 0.6 m clear of them and a metre for the queue of people boarding make 2.5 m (a design assumption built on that figure).</summary>
        private const float PlatformEdge = 2.5f;

        /// <summary>Why a platform pillar face cannot take a fitting: too near the edge, facing the edge, or in the stream at a car door.</summary>
        private string PlatformReason(WallSpots.Spot s)
        {
            var platform = points.PlatformAt(s.Wall);
            if (platform == null) return "not on a platform strip";
            var a = new Vector2(platform.a.x, platform.a.z);
            var ab = new Vector2(platform.b.x, platform.b.z) - a;
            var p = new Vector2(s.Wall.x, s.Wall.z);
            float edge = platform.halfWidth - Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude));
            if (edge < PlatformEdge) return "within 2.5 m of the platform edge";
            if (Mathf.Abs(Vector2.Dot(new Vector2(s.Normal.x, s.Normal.z), ab.normalized)) < .7f) return "faces across the platform toward an edge";
            if (Near(s.Wall, carDoors) < 4f) return "boarding stream at a car door";
            return null;
        }

        /// <summary>A person standing 2.8 m in front (straight, or 25° to either side) at eye height must see the item's centre without a wall, column or glass in between.</summary>
        private static bool SeenFromFront(Rule rule, WallSpots.Spot s)
        {
            var target = s.Wall + s.Normal * (rule.CentredPivot ? rule.Depth * .5f : .1f) + Vector3.up * (rule.Elevation + rule.Height * .5f);
            foreach (float yaw in new[] { 0f, 25f, -25f })
            {
                var eye = s.Wall + Quaternion.Euler(0, yaw, 0) * s.Normal * 2.8f + Vector3.up * 1.6f;
                var direction = target - eye;
                if (!WallSpots.LineBlocked(eye, direction.normalized, direction.magnitude - .5f)) return true;
            }
            return false;
        }

        private void Reject(Rule rule, string reason)
        {
            string key = rule.Kind + ": " + reason;
            Rejected[key] = Rejected.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        /// <summary>The paving that guides the blind (yellow tactile blocks) is a walking line: nothing stands on it or within 0.6 m of it.</summary>
        private bool OnTactile(Vector3 centre, float radius)
        {
            float reach = radius + .6f;
            foreach (var b in tactile) if (b.SqrDistance(centre) < reach * reach) return true;
            return false;
        }

        /// <summary>The item's real box (a few cm inset, above the floor) overlaps a collider of the twin: it would clip into a wall, a column or a fitting.</summary>
        private static bool Collides(Rule rule, Vector3 centre, float yaw)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            const float inset = .03f;
            var local = new Vector3(0, rule.Elevation + .03f + (rule.Height - .06f) * .5f, rule.CentredPivot ? 0 : rule.Depth * .5f);
            var half = new Vector3(rule.Width * .5f - inset, (rule.Height - .06f) * .5f, rule.Depth * .5f - inset);
            return Physics.CheckBox(centre + rotation * local, half, rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>A shop's customer place (or ticket counter) lies within 5 m straight in front of the wall: that wall is the inside of the shop (or the counter's own wall).</summary>
        private bool FacesShopFront(WallSpots.Spot s)
        {
            foreach (var q in shops)
            {
                if (Mathf.Abs(q.y - s.Wall.y) > 3f) continue;
                var v = new Vector3(q.x - s.Wall.x, 0, q.z - s.Wall.z);
                float d = v.magnitude;
                if (d < 5f && d > .01f && Vector3.Dot(v / d, s.Normal) > .7f) return true;
            }
            return false;
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
            var byZone = new Dictionary<string, Dictionary<string, int>>();
            void Fail(string zone, string reason)
            {
                if (zone == "tracks") reason = "[platform] " + reason;
                Reject(rule, reason);
                if (!byZone.TryGetValue(zone, out var reasons)) byZone[zone] = reasons = new Dictionary<string, int>();
                reasons[reason] = reasons.TryGetValue(reason, out int n) ? n + 1 : 1;
            }
            var candidates = new List<(WallSpots.Spot spot, float score)>();
            foreach (var spot in survey.Spots)
            {
                if (rule.Quota(spot.Zone) <= 0) continue;
                var reason = Reject(rule, spot);
                if (reason != null) { Fail(spot.Zone, reason); continue; }
                float score = rule.Score(spot);
                if (score > 0) candidates.Add((spot, score)); else Fail(spot.Zone, "not where it is used (score 0)");
            }
            candidates = candidates.OrderByDescending(c => c.score).ThenBy(c => c.spot.Wall.x).ThenBy(c => c.spot.Wall.z).ToList();
            foreach (var (spot, _) in candidates)
            {
                count.TryGetValue(spot.Zone, out int have);
                if (have >= rule.Quota(spot.Zone)) continue;
                var centre = Centre(rule, spot, 0);
                float radius = Mathf.Max(rule.Width, rule.Depth) * .5f;
                float yaw = Mathf.Atan2(spot.Normal.x, spot.Normal.z) * Mathf.Rad2Deg;
                if (placed.Any(p => p.Kind == rule.Kind && Mathf.Abs(p.Centre.y - centre.y) < 3f && Vector2.Distance(new Vector2(p.Centre.x, p.Centre.z), new Vector2(centre.x, centre.z)) < rule.Spacing)) { Fail(spot.Zone, "closer than " + rule.Spacing + " m to one of its kind"); continue; }
                if (!Free(centre, radius)) { Fail(spot.Zone, "overlaps another placed fitting"); continue; }
                if (Collides(rule, centre, yaw)) { Fail(spot.Zone, "collides with twin geometry"); continue; }
                // 옆에 하나 더(자판기 두 대·수거함과 휴지통): 벽이 그만큼 이어져야 한다.
                float offset = 0;
                if (rule.Beside != null && spot.Zone != "tracks")
                {
                    float need = rule.Width * .5f + .03f + rule.Beside.Width + Side;
                    if (spot.FlatPlus >= need && spot.FlatMinus >= rule.Width * .5f + Side) offset = rule.Width * .5f + .03f + rule.Beside.Width * .5f;
                    else if (spot.FlatMinus >= need && spot.FlatPlus >= rule.Width * .5f + Side) offset = -(rule.Width * .5f + .03f + rule.Beside.Width * .5f);
                    else { Fail(spot.Zone, "no wall beside for the second fitting"); continue; }
                    var second = Centre(rule.Beside, spot, offset);
                    if (!Free(second, Mathf.Max(rule.Beside.Width, rule.Beside.Depth) * .5f) || Collides(rule.Beside, second, yaw)) { Fail(spot.Zone, "second fitting overlaps geometry"); continue; }
                }
                count[spot.Zone] = have + 1;
                Add(rule, spot, 0, items);
                if (rule.Beside != null && spot.Zone != "tracks") Add(rule.Beside, spot, offset, items);
            }
            foreach (var zone in byZone.Keys.OrderBy(z => z, StringComparer.Ordinal))
            {
                count.TryGetValue(zone, out int have);
                int quota = rule.Quota(zone);
                if (have >= quota) continue;
                Unmet.Add(rule.Kind + " " + zone + " " + have + "/" + quota + ": " + string.Join("; ", byZone[zone].OrderByDescending(r => r.Value).ThenBy(r => r.Key, StringComparer.Ordinal).Take(3).Select(r => r.Value + "x " + r.Key)));
            }
        }

        private void Add(Rule rule, WallSpots.Spot spot, float along, List<EquipmentPlacement> items)
        {
            var centre = Centre(rule, spot, along);
            // 반지름에 0.8 m 를 더해 두어, 뒤에 놓이는 다른 비품은 이 비품에서 0.8 m 이상 떨어진다(붙어 서서 빽빽해 보이는 것을 막는다). 짝(옆에 하나 더)은 이미 함께 검사했다.
            placed.Add(new Placed { Centre = centre, Radius = Mathf.Max(rule.Width, rule.Depth) * .5f + Clearance, Kind = rule.Kind });
            string key = rule.IdPrefix + "-" + spot.Zone;
            serial[key] = serial.TryGetValue(key, out int n) ? n + 1 : 1;
            string label = rule.Kind == "distribution_board" ? BoardLabel(spot.Zone) : rule.Label;
            string id = key + "-" + serial[key].ToString("00");
            items.Add(new EquipmentPlacement
            {
                id = id, kind = rule.Kind, label = label, zone = spot.Zone, prefab = rule.Prefab,
                position = centre, rotation = new Vector3(0, Mathf.Atan2(spot.Normal.x, spot.Normal.z) * Mathf.Rad2Deg, 0),
            });
            // 요구 조건보다 얼마나 여유가 있는지(작을수록 빠듯하다).
            float tight = Mathf.Min(spot.Free - rule.MinFree, Mathf.Min(spot.FlatPlus, spot.FlatMinus) - (rule.Width * .5f + Side), Near(spot.Wall, doors) - rule.Door, Near(spot.Wall, escalatorEnds) - rule.Escalator, Near(spot.Wall, elevatorDoors) - rule.Elevator, Near(spot.Wall, people) - rule.Person);
            Clearances.Add(string.Join(",", id, rule.Kind, spot.Zone, spot.Free.ToString("0.00"), Mathf.Min(spot.FlatPlus, spot.FlatMinus).ToString("0.00"), spot.Enclosed, Near(spot.Wall, doors).ToString("0.0"), Near(spot.Wall, escalatorEnds).ToString("0.0"),
                Near(spot.Wall, elevatorDoors).ToString("0.0"), Near(spot.Wall, people).ToString("0.0"), tight.ToString("0.00")));
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
                Kind = "litter_bin", Prefab = ElectricPlazaPrefabs.LitterBin, Label = "휴지통", IdPrefix = "bin", Width = .43f, Depth = .43f, Height = .85f, MinFree = .4f + Passage, Spacing = 9f,
                Door = 1.2f, TenantDoor = 1.5f, Exit = 2f, Escalator = 3f, Elevator = 3f, Fitting = 1.2f, Person = .8f, MaxEnclosed = 6,
                // 근거: 역사 쓰레기통은 출입구·화장실·점포·좌석 곁에 둔다. 1,200 m² 당 1개(설계 가정), 구역당 12개까지.
                // 승강장 쓰레기통은 기둥 곁에 6개까지(설계 가정: 승강장 다섯 곳에 한둘씩).
                Quota = zone => zone == "tracks" ? 6 : Indoor(zone) ? Mathf.Clamp(Mathf.FloorToInt(Area(zone) / 1200f), Area(zone) > 300f ? 1 : 0, 12) : 0,
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
        /// people who come out (a recycling station with a litter bin), well beside the walking line. The designated smoking area of the
        /// real station (outdoors at exit 5, toward the Asti Hotel, by the taxi stand) is not modelled in the twin (no taxi stand, hotel or
        /// numbered exit 5 exists there), so no cigarette-butt bin is placed. A site is the nearest walkable, empty floor to the wanted
        /// spot (a search ring, deterministic).
        /// </summary>
        private void PlaceOutdoors(List<EquipmentPlacement> items)
        {
            var recycling = RecyclingStation();
            var bin = recycling.Beside;
            foreach (var exit in points.Of(PointKind.Exit).Where(e => !Indoor(e.Zone)).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var into = points.Nearest(PointKind.Wait, exit.Position);
                var inward = into != null ? new Vector3(into.Position.x - exit.Position.x, 0, into.Position.z - exit.Position.z).normalized : Vector3.forward;
                var side = Vector3.Cross(Vector3.up, inward);
                // 나오는 사람들을 마주 본다.
                float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
                // 출구에서 안쪽으로 걷는 길(가운데 줄)에서 3.5 m 이상 옆으로 비켜 세운다.
                var wanted = new List<(Rule rule, Vector3 at)> { (recycling, exit.Position + inward * 5f + side * 3.5f), (bin, exit.Position + inward * 5f + side * 4.6f) };
                // 흡연구역은 트윈에 없다(택시 승강장·호텔·5번 출구가 모델에 없다): 꽁초 수거함은 두지 않는다.
                foreach (var (rule, at) in wanted)
                {
                    float radius = Mathf.Max(rule.Width, rule.Depth) * .5f;
                    var centre = FloorNear(at, exit.Position.y, radius);
                    if (centre == null) continue;
                    placed.Add(new Placed { Centre = centre.Value, Radius = radius + Clearance, Kind = rule.Kind });
                    string key = rule.IdPrefix + "-" + exit.Zone + "-" + exit.Id;
                    serial[key] = serial.TryGetValue(key, out int n) ? n + 1 : 1;
                    items.Add(new EquipmentPlacement
                    {
                        id = key + "-" + serial[key].ToString("00"), kind = rule.Kind, label = rule.Label, zone = exit.Zone, prefab = rule.Prefab,
                        position = centre.Value, rotation = new Vector3(0, yaw, 0),
                    });
                }
            }
        }

        private Vector3? FloorNear(Vector3 wanted, float floorY, float radius)
        {
            for (float ring = 0; ring <= 6f; ring += .5f)
                for (int step = 0; step < (ring == 0 ? 1 : 12); step++)
                {
                    var at = wanted + Quaternion.Euler(0, step * 30f, 0) * Vector3.forward * ring;
                    if (!NavMesh.SamplePosition(at, out var hit, .3f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floorY) > 1.2f) continue;
                    if (Physics.CheckSphere(hit.position + Vector3.up * .6f, .4f, ~0, QueryTriggerInteraction.Ignore) || !Free(hit.position, radius) || OnTactile(hit.position, radius) || !FlatGround(hit.position, radius)) continue;
                    return hit.position;
                }
            return null;
        }

        /// <summary>The paving under the item is level (no stair edge or ramp): four rays at its corners and one at the centre stay within 4 cm of the site's height.</summary>
        private static bool FlatGround(Vector3 site, float radius)
        {
            foreach (var offset in new[] { Vector3.zero, new Vector3(radius, 0, radius), new Vector3(-radius, 0, radius), new Vector3(radius, 0, -radius), new Vector3(-radius, 0, -radius) })
            {
                if (!Physics.Raycast(site + offset + Vector3.up * .5f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(hit.point.y - site.y) > .06f || hit.normal.y < .97f) return false;
            }
            return true;
        }
    }
}
