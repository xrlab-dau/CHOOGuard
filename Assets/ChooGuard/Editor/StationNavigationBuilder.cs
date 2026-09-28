using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Bakes the whole-station passenger navmesh (concourse, 3F, 1F, plaza, platforms, sky plaza) from the twin's
    /// physics colliders, with escalator belts blocked so they are only used as one-way rides, and writes the station
    /// data (zones, places, escalators, elevators, platforms, KTX doors and seats). FpsStation.unity is not modified;
    /// the session adds the navmesh and links at runtime.
    /// </summary>
    public static class StationNavigationBuilder
    {
        public const string NavMeshPath = EmergencySceneBuilder.ArtRoot + "/StationWorld.navmesh.asset";
        public const string PointsPath = EmergencySceneBuilder.ArtRoot + "/station-points.json";
        public const string TrainPatchRoot = EmergencySceneBuilder.ArtRoot + "/TrainDoors";
        public static readonly Bounds WorldBounds = new Bounds(new Vector3(30, 7.5f, -5), new Vector3(270, 19, 270));
        // 맞이방 한가운데(대합실 좌석 남쪽). 모든 지점은 여기서 걸어서(링크 포함) 닿아야 채택한다.
        public static readonly Vector3 HallCentre = new Vector3(64, 7.0f, -2);
        public const int EscalatorArea = 3, ElevatorArea = 4;

        public static NavMeshBuildSettings Settings()
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .28f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = .35f;
            settings.agentSlope = 36;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .12f;
            settings.overrideTileSize = true;
            settings.tileSize = 256;
            settings.minRegionArea = 6;
            return settings;
        }

        private static void RequireStation()
        {
            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            if (!station.IsValid() || !station.isLoaded) throw new InvalidOperationException("FpsStation 씬을 연 상태에서 실행하세요.");
        }

        [MenuItem("ChooGuard/Emergency/Bake world navmesh")]
        public static void Bake()
        {
            RequireStation();
            var log = new List<string>();
            var lanes = StationSurvey.Escalators(log);
            var ktx = GameObject.Find(StationSurvey.KtxPath).transform;
            var namedEscalators = GameObject.Find("FPSWorld/맞이방 · 에스컬레이터")?.transform;
            var player = UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Fps.FirstPersonResponder>();
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(WorldBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            int before = sources.Count;
            sources.RemoveAll(s =>
            {
                var component = s.component;
                if (component == null) return false;
                // 열차는 움직인다. 에스컬레이터 벨트는 걷는 길이 아니라 탑승 링크로만 쓴다.
                if (component.transform.IsChildOf(ktx)) return true;
                if (namedEscalators != null && component.transform.IsChildOf(namedEscalators)) return true;
                if (player != null && component.transform.IsChildOf(player.transform)) return true;
                // 사람이 지나가는 문(자동 미닫이·공용 여닫이)은 길을 막지 않는다. 관계자 문·승강장 문·못 쓰는 문은 막고, 잠근 자동문은 실행 중에 길을 깎는다.
                var door = component.GetComponentInParent<ChooGuard.App.Fps.Facilities.StationDoor>();
                if (door != null && door.PassableByPeople) return true;
                var collider = component as Collider;
                return collider != null && collider.isTrigger;
            });
            foreach (var lane in lanes)
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(lane.BoxCentre, lane.BoxRotation, Vector3.one),
                    size = lane.BoxSize,
                    area = 1,
                });
                // 에스컬레이터 옆 계단은 따로 구역을 둬서 사람마다 계단·에스컬레이터·엘리베이터를 고를 수 있게 한다.
                if (lane.StairsSize != Vector3.zero)
                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.ModifierBox,
                        transform = Matrix4x4.TRS(lane.StairsCentre, lane.BoxRotation, Vector3.one),
                        size = lane.StairsSize,
                        area = StationWorld.StairsArea,
                    });
            }
            var data = NavMeshBuilder.BuildNavMeshData(Settings(), sources, WorldBounds, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("navmesh bake produced no data");
            Directory.CreateDirectory(Path.GetDirectoryName(NavMeshPath));
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(data, NavMeshPath);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_WORLD_NAVMESH sources=" + before + "->" + sources.Count + " escalators=" + lanes.Count + " stairs=" + lanes.Count(l => l.StairsSize != Vector3.zero) + " path=" + NavMeshPath + "\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Replaces only the elevators of station-points.json with the current survey (kit landing doors with a working car),
        /// keeping every other station datum as it is. Run after elevator landings change; a full Build station points also does it.
        /// </summary>
        [MenuItem("ChooGuard/Emergency/Rebuild elevator stops")]
        public static void RebuildElevatorStops()
        {
            RequireStation();
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) ?? throw new FileNotFoundException("먼저 navmesh 를 굽습니다: " + NavMeshPath);
            var instance = NavMesh.AddNavMeshData(data);
            var log = new List<string>();
            try
            {
                Vector3? OnNav(Vector3 p, float r) => NavMesh.SamplePosition(p, out var h, r, NavMesh.AllAreas) ? h.position : (Vector3?)null;
                var elevators = StationSurvey.Elevators(OnNav, log);
                var file = JsonUtility.FromJson<StationFile>(File.ReadAllText(PointsPath));
                file.elevators = elevators.ToArray();
                File.WriteAllText(PointsPath, JsonUtility.ToJson(file, true));
                AssetDatabase.ImportAsset(PointsPath);
                Debug.Log("CG_ELEVATOR_STOPS elevators=" + elevators.Count + " " + string.Join(" | ", elevators.Select(e => e.id + " " + e.label + " " + string.Join(" / ", e.stops.Select(s => s.floor + " " + s.door.ToString("F1") + " yaw " + s.yaw.ToString("F0"))))) + "\n" + string.Join("\n", log));
            }
            finally { instance.Remove(); }
        }

        // ── 장소·구역·링크·열차 ───────────────────────────────────────────────

        private static readonly StationPoints.ZoneEntry[] Zones =
        {
            Zone("hall2f", "2층 맞이방", new Vector3(18, 5.5f, -30), new Vector3(112, 10.5f, 64), 1),
            Zone("southgate", "2층 남측 게이트(타는 곳)", new Vector3(-6, 5.5f, -70), new Vector3(95, 10.5f, -30), 2),
            Zone("northdeck", "2층 북측 데크(나가는 곳)", new Vector3(25, 5.5f, 60), new Vector3(145, 10.5f, 105), 2),
            Zone("main2f", "2층 본관", new Vector3(-62, 5.5f, -100), new Vector3(18, 10.5f, 90), 1),
            Zone("eastexit", "2층 동측 출구", new Vector3(88, 5.5f, -25), new Vector3(112, 10.5f, 30), 3),
            Zone("skyplaza", "하늘광장(부산항 방면)", new Vector3(112, 4, -95), new Vector3(235, 11, 55), 1),
            Zone("upper3f", "3층", new Vector3(0, 10.5f, -55), new Vector3(115, 17, 60), 2),
            Zone("ground1f", "1층", new Vector3(-70, -.6f, -95), new Vector3(16, 5.5f, 90), 1),
            Zone("plaza", "역 광장·중앙대로", new Vector3(-140, -2, -150), new Vector3(-58, 10.5f, 140), 0),
            Zone("tracks", "승강장 구역", new Vector3(-40, -1.5f, -160), new Vector3(160, 5.4f, 120), 0),
        };

        private static StationPoints.ZoneEntry Zone(string id, string label, Vector3 min, Vector3 max, int priority) =>
            new StationPoints.ZoneEntry { id = id, label = label, min = min, max = max, priority = priority };

        // 도시 쪽 출입(사람이 들고 나는 곳). 트윈 가장자리의 걷는 면에서 고른다.
        // 도시 쪽 출입(사람이 들고 나는 곳). 트윈 가장자리에서 맞이방까지 걸어서 닿는 곳을 고른다(2026-09-26 도달성 격자).
        private static readonly (string id, string label, Vector3 near)[] CityExits =
        {
            ("exit-west-north", "중앙대로(초량 방면)", new Vector3(-104, 1, 62)),
            ("exit-west-south", "중앙대로(중앙동 방면)", new Vector3(-100, 5, -30)),
            ("exit-northwest", "초량 방면 도로", new Vector3(-102, 5.5f, 112)),
            ("exit-port", "부산항 방면(하늘광장 끝)", new Vector3(163, 7, -52)),
        };

        [MenuItem("ChooGuard/Emergency/Build station points")]
        public static void BuildPoints()
        {
            RequireStation();
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) ?? throw new FileNotFoundException("먼저 navmesh 를 굽습니다: " + NavMeshPath);
            var instance = NavMesh.AddNavMeshData(data);
            var links = new List<NavMeshLinkInstance>();
            var log = new List<string>();
            try
            {
                Vector3? OnNav(Vector3 p, float r) => NavMesh.SamplePosition(p, out var h, r, NavMesh.AllAreas) ? h.position : (Vector3?)null;
                // 에스컬레이터·엘리베이터를 링크로 넣은 상태에서 도달 가능성을 본다(3층·1층은 링크로만 닿는 곳이 있다).
                var escalators = new List<StationPoints.EscalatorEntry>();
                foreach (var lane in StationSurvey.Escalators(log))
                {
                    // 벨트 끝 바로 앞은 난간·벽에 붙어 있어 걷는 면이 옆으로 1~2 m 비켜 있기도 하다.
                    var from = OnNav(lane.Path[0], 2.5f);
                    var to = OnNav(lane.Path[lane.Path.Count - 1], 2.5f);
                    if (from == null || to == null) { log.Add(lane.Id + ": 계단참이 navmesh 밖 " + (from == null ? "타는 쪽 " + lane.Path[0].ToString("F1") : "") + (to == null ? " 내리는 쪽 " + lane.Path[lane.Path.Count - 1].ToString("F1") : "")); continue; }
                    lane.Path[0] = from.Value;
                    lane.Path[lane.Path.Count - 1] = to.Value;
                    links.Add(NavMesh.AddLink(new NavMeshLinkData { startPosition = from.Value, endPosition = to.Value, width = 0, bidirectional = false, area = EscalatorArea, costModifier = -1 }));
                    var entry = new StationPoints.EscalatorEntry { id = lane.Id, label = lane.Label, up = lane.Up, from = lane.From, to = lane.To, path = lane.Path.ToArray() };
                    if (lane.StairsSize != Vector3.zero)
                    {
                        // 옆 계단의 위·아래 계단참(긴 승강장 경로를 두 구간으로 나눌 때 쓰는 경유점).
                        var upAxis = lane.BoxRotation * Vector3.forward;
                        float half = lane.StairsSize.z * .5f + .6f;
                        var top = OnNav(new Vector3(lane.StairsCentre.x, StationSurvey.Concourse, lane.StairsCentre.z) + upAxis * half, 2.5f);
                        var bottom = OnNav(new Vector3(lane.StairsCentre.x, StationSurvey.Ground, lane.StairsCentre.z) - upAxis * half, 2.5f);
                        if (top != null && bottom != null && Mathf.Abs(top.Value.y - StationSurvey.Concourse) < .6f && Mathf.Abs(bottom.Value.y) < .6f)
                        {
                            entry.stairs = true;
                            entry.stairsTop = Round(top.Value);
                            entry.stairsBottom = Round(bottom.Value);
                        }
                    }
                    escalators.Add(entry);
                }
                var elevators = StationSurvey.Elevators(OnNav, log);
                foreach (var elevator in elevators)
                    for (int i = 0; i < elevator.stops.Length; i++)
                        for (int j = i + 1; j < elevator.stops.Length; j++)
                            links.Add(NavMesh.AddLink(new NavMeshLinkData { startPosition = elevator.stops[i].door, endPosition = elevator.stops[j].door, width = 0, bidirectional = true, area = ElevatorArea, costModifier = -1 }));

                if (!NavMesh.SamplePosition(HallCentre, out var centreHit, 2f, NavMesh.AllAreas)) throw new InvalidOperationException("맞이방 중심이 navmesh 위에 없습니다.");
                var path = new NavMeshPath();
                bool Reachable(Vector3 p) => NavMesh.CalculatePath(centreHit.position, p, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                var zoneIndex = new StationPointsZones(Zones);
                var points = new List<StationPoints.PointEntry>();
                var rejected = new List<string>();
                void Add(string id, string kind, string label, Vector3 near, float radius, float yaw = 0, Vector3? anchor = null, string slot = null)
                {
                    var hit = OnNav(near, radius);
                    if (hit == null || !Reachable(hit.Value)) { rejected.Add(id); return; }
                    points.Add(new StationPoints.PointEntry { id = id, kind = kind, label = label, zone = zoneIndex.At(hit.Value), position = Round(hit.Value), yaw = Mathf.Round(yaw), anchor = anchor.HasValue ? Round(anchor.Value) : Vector3.zero, slot = slot });
                }

                // 출구는 가장 가까운 navmesh 가 떨어진 조각일 수 있어 주변 12 m 안에서 맞이방과 이어진 점을 찾는다.
                foreach (var (id, label, near) in CityExits)
                {
                    Vector3? best = null; float bestDistance = float.PositiveInfinity;
                    for (float dx = -12; dx <= 12; dx += 2)
                        for (float dz = -12; dz <= 12; dz += 2)
                            foreach (float dy in new[] { -2f, 0f, 2f })
                            {
                                var probe = near + new Vector3(dx, dy, dz);
                                var hit = OnNav(probe, 1.5f);
                                if (hit == null || !Reachable(hit.Value)) continue;
                                float d = Vector3.Distance(hit.Value, near);
                                if (d < bestDistance) { bestDistance = d; best = hit; }
                            }
                    if (best == null) { rejected.Add(id); continue; }
                    points.Add(new StationPoints.PointEntry { id = id, kind = "Exit", label = label, zone = zoneIndex.At(best.Value), position = Round(best.Value) });
                }

                // 매표창구 앞: 창구 줄에서 맞이방 쪽으로 1.4 m.
                for (int i = 0; i < 9; i++)
                {
                    var window = GameObject.Find("맞이방 · 원본 정합/매표창구 window " + i);
                    if (window == null) { rejected.Add("counter-" + i); continue; }
                    var bounds = window.GetComponentInChildren<Renderer>().bounds;
                    var toward = FaceOpen(bounds.center, 7f);
                    Add("counter-" + i, "Counter", "매표창구 " + (i + 1) + "번", new Vector3(bounds.center.x, 7, bounds.center.z) + toward * 1.4f, 1f, Mathf.Atan2(-toward.x, -toward.z) * Mathf.Rad2Deg);
                }

                AddTenants(points, rejected, Add, zoneIndex, Reachable);
                AddToilets(Add);
                AddWaits(points, zoneIndex, Reachable);
                AddSeats(points, rejected, Reachable, zoneIndex);
                Add("office-door", "Office", "역무실", new Vector3(27, 7, -9.5f), 3f, 0);
                // 마중: 북측 나가는 곳 앞 맞이방(도착 안내판 아래).
                Add("meet-north", "Meet", "나가는 곳 앞", new Vector3(81, 7, 39), 3f, 20);
                Add("meet-south", "Meet", "남측 게이트 앞", new Vector3(52, 7, -26), 3f, 200);

                var platforms = StationSurvey.Platforms(log);
                var train = StationSurvey.Train(platforms.First(p => p.id == "p56"), log);
                // 문 앞 승강장이 맞이방에서 걸어서 닿지 않는 객차(남측 게이트 쪽 승강장이 트윈에서 끊겨 있음)는 타고 내리는 흐름에 쓰지 않는다.
                foreach (var car in train.cars)
                {
                    var front = OnNav(car.door.outside, 1.0f);
                    car.reachable = front != null && Reachable(front.Value);
                    if (!car.reachable) log.Add(car.label + ": 문 앞 승강장에 닿지 않음");
                }
                // 승강장 대기: 5·6 타는 곳 각 객차 문 양옆(문 앞은 내리는 사람 길로 비워 둔다).
                foreach (var car in train.cars)
                {
                    if (!car.reachable) continue;
                    var outward = car.door.outside - car.door.inside; outward.y = 0; outward.Normalize();
                    var along = train.axis;
                    int made = 0;
                    for (int i = 0; i < 10 && made < 5; i++)
                    {
                        float side = (i % 2 == 0 ? 1 : -1) * (1.1f + (i / 2) * .65f);
                        float back = 1.0f + (i % 3) * .5f;
                        var near = car.door.outside + outward * back + along * side;
                        var hit = OnNav(near, 1.0f);
                        if (hit == null || Mathf.Abs(hit.Value.y) > .3f || !Reachable(hit.Value)) continue;
                        points.Add(new StationPoints.PointEntry { id = "pw-" + car.number + "-" + made++, kind = "PlatformWait", label = car.label + " 타는 문 앞", zone = zoneIndex.At(hit.Value), position = Round(hit.Value), yaw = Mathf.Round(Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg) });
                    }
                    if (made == 0) rejected.Add("pw-" + car.number);
                }

                var file = new StationFile
                {
                    note = "Generated by StationNavigationBuilder.BuildPoints from FpsStation + StationWorld navmesh with escalator/elevator links. Places are reachable from the hall centre (64, 7, -2). Train positions are at the stop pose.",
                    zones = Zones,
                    points = points.ToArray(),
                    escalators = escalators.ToArray(),
                    elevators = elevators.ToArray(),
                    platforms = platforms.ToArray(),
                    train = train,
                };
                File.WriteAllText(PointsPath, JsonUtility.ToJson(file, true));
                AssetDatabase.ImportAsset(PointsPath);
                var counts = points.GroupBy(p => p.kind).Select(g => g.Key + "=" + g.Count());
                Debug.Log("CG_STATION_POINTS " + string.Join(" ", counts) + " escalators=" + escalators.Count + " elevators=" + elevators.Count + " platforms=" + platforms.Count +
                          " cars=" + train.cars.Length + " seats=" + train.cars.Sum(c => c.seats.Length) + " rejected=" + rejected.Count + " [" + string.Join(",", rejected) + "]\n" + string.Join("\n", log));
            }
            finally
            {
                foreach (var link in links) NavMesh.RemoveLink(link);
                NavMesh.RemoveNavMeshData(instance);
            }
        }

        [Serializable] private sealed class StationFile { public int version = 2; public string note; public StationPoints.ZoneEntry[] zones; public StationPoints.PointEntry[] points; public StationPoints.EscalatorEntry[] escalators; public StationPoints.ElevatorEntry[] elevators; public StationPoints.PlatformEntry[] platforms; public StationPoints.TrainEntry train; }

        private sealed class StationPointsZones
        {
            private readonly StationPoints.ZoneEntry[] zones;
            public StationPointsZones(StationPoints.ZoneEntry[] zones) { this.zones = zones; }
            public string At(Vector3 p)
            {
                StationPoints.ZoneEntry best = null;
                foreach (var z in zones)
                {
                    if (p.x < z.min.x || p.y < z.min.y || p.z < z.min.z || p.x > z.max.x || p.y > z.max.y || p.z > z.max.z) continue;
                    if (best == null || z.priority > best.priority) best = z;
                }
                return best?.id ?? "";
            }
        }

        private static Vector3 Round(Vector3 v) => new Vector3(Mathf.Round(v.x * 100) / 100, Mathf.Round(v.y * 100) / 100, Mathf.Round(v.z * 100) / 100);

        /// <summary>Horizontal direction from <paramref name="centre"/> toward the most open walkable side at floor height.</summary>
        private static Vector3 FaceOpen(Vector3 centre, float floorY)
        {
            Vector3 best = Vector3.forward; float bestDistance = -1;
            for (int a = 0; a < 360; a += 30)
            {
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                var probe = new Vector3(centre.x, floorY, centre.z) + dir * 1.5f;
                if (!NavMesh.SamplePosition(probe, out var hit, .5f, NavMesh.AllAreas)) continue;
                float free = Physics.Raycast(hit.position + Vector3.up * 1.2f, dir, out var h, 6f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 6f;
                if (free > bestDistance) { bestDistance = free; best = dir; }
            }
            return best;
        }

        /// <summary>
        /// Shops and cafe chairs: hall tenants (children of 맞이방 · 원본 정합 named "&lt;name&gt; fixture/display/table/chair")
        /// and store interiors (children of 점포 내부 * named "SHOP-&lt;floor&gt;-&lt;name&gt; counter/cabinet/tableSet N table/chair L|R").
        /// A chair faces its table; the seated person's hips sit over the chair centre.
        /// </summary>
        private static void AddTenants(List<StationPoints.PointEntry> points, List<string> rejected, Action<string, string, string, Vector3, float, float, Vector3?, string> add, StationPointsZones zones, Func<Vector3, bool> reachable)
        {
            var items = new List<(string shop, string type, Bounds bounds)>();
            var hall = GameObject.Find("맞이방 · 원본 정합");
            if (hall != null)
                foreach (Transform child in hall.transform)
                {
                    var name = child.name;
                    int cut = new[] { " fixture", " display", " table", " chair" }.Select(s => name.IndexOf(s, StringComparison.Ordinal)).Where(i => i > 0).DefaultIfEmpty(-1).Max();
                    if (cut <= 0 || !TryBounds(child, out var b)) continue;
                    items.Add((name.Substring(0, cut), name.Substring(cut + 1), b));
                }
            var finish = GameObject.Find("실내 트윈 마감");
            if (finish != null)
                foreach (Transform group in finish.transform)
                {
                    if (!group.name.StartsWith("점포 내부", StringComparison.Ordinal)) continue;
                    foreach (Transform child in group)
                    {
                        if (!child.name.StartsWith("SHOP-", StringComparison.Ordinal) || !TryBounds(child, out var b)) continue;
                        var name = child.name;
                        int cut = new[] { " counter", " cabinet", " tableSet", " plant", " light" }.Select(s => name.IndexOf(s, StringComparison.Ordinal)).Where(i => i > 0).DefaultIfEmpty(-1).Min();
                        if (cut <= 0) continue;
                        var type = name.Contains(" chair") ? "chair" : name.Contains(" table") ? "table" : name.Contains(" counter") ? "counter" : name.Contains(" cabinet") ? "cabinet" : "";
                        if (type.Length == 0) continue;
                        items.Add((ShopLabel(name.Substring(0, cut)), type, b));
                    }
                }
            foreach (var shop in items.Where(i => i.type != "table" && i.type != "chair").GroupBy(i => i.shop).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var centre = shop.Aggregate(Vector3.zero, (a, b) => a + b.bounds.center) / shop.Count();
                float floorY = shop.Min(i => i.bounds.min.y);
                var toward = FaceOpen(centre, floorY);
                add("shop-" + shop.Key.Replace(' ', '_'), "Shop", shop.Key, new Vector3(centre.x, floorY, centre.z) + toward * 1.2f, 2.5f, Mathf.Atan2(-toward.x, -toward.z) * Mathf.Rad2Deg, null, null);
            }
            var tables = items.Where(i => i.type == "table").ToList();
            int chairIndex = 0;
            foreach (var chair in items.Where(i => i.type == "chair"))
            {
                string id = "chair-" + chairIndex++;
                var table = tables.Where(t => t.shop == chair.shop).OrderBy(t => Vector3.Distance(t.bounds.center, chair.bounds.center)).FirstOrDefault();
                if (table.shop == null || Vector3.Distance(table.bounds.center, chair.bounds.center) > 1.6f) { rejected.Add(id); continue; }
                var facing = table.bounds.center - chair.bounds.center; facing.y = 0;
                if (facing.sqrMagnitude < .01f) { rejected.Add(id); continue; }
                facing.Normalize();
                var anchor = new Vector3(chair.bounds.center.x, chair.bounds.min.y, chair.bounds.center.z);
                // 의자 앞(탁자 쪽)에 서서 돌아 앉는다. 탁자에 막히면 의자 옆에서 앉는다.
                if (!NavMesh.SamplePosition(anchor + facing * .46f, out var stand, .3f, NavMesh.AllAreas))
                {
                    var sideways = new Vector3(facing.z, 0, -facing.x);
                    if (!NavMesh.SamplePosition(anchor + sideways * .55f, out stand, .35f, NavMesh.AllAreas) && !NavMesh.SamplePosition(anchor - sideways * .55f, out stand, .35f, NavMesh.AllAreas)) { rejected.Add(id); continue; }
                }
                if (!reachable(stand.position)) { rejected.Add(id); continue; }
                points.Add(new StationPoints.PointEntry
                {
                    id = id, kind = "Chair", label = chair.shop + " 의자", zone = zones.At(stand.position),
                    position = Round(stand.position), anchor = Round(anchor), yaw = Mathf.Round(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg), slot = id,
                });
            }
        }

        private static bool TryBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return true;
        }

        /// <summary>SHOP-2F-box-카카오프렌즈 → 카카오프렌즈, SHOP-2F-main-편의점-44_-37 → 편의점, SHOP-1F-03 → 1층 매장 03.</summary>
        private static string ShopLabel(string raw)
        {
            var rest = raw.Substring("SHOP-".Length);
            int dash = rest.IndexOf('-');
            var floor = dash > 0 ? rest.Substring(0, dash) : "";
            rest = dash > 0 ? rest.Substring(dash + 1) : rest;
            if (rest.StartsWith("main-", StringComparison.Ordinal)) rest = rest.Substring(5);
            if (rest.StartsWith("box-", StringComparison.Ordinal)) rest = rest.Substring(4);
            rest = System.Text.RegularExpressions.Regex.Replace(rest, @"-+-?\d+_-?\d+$", "");
            var floorName = floor == "1F" ? "1층" : floor == "2F" ? "2층" : floor == "3F" ? "3층" : floor;
            return rest.All(char.IsDigit) ? floorName + " 매장 " + rest : rest.Replace('-', ' ');
        }

        private static void AddToilets(Action<string, string, string, Vector3, float, float, Vector3?, string> add)
        {
            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            var signs = station.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
                .Where(r => r.name.StartsWith("Sign · ", StringComparison.Ordinal) && r.name.Contains("화장실") && !r.name.Contains("m")).ToList();
            int index = 0;
            foreach (var sign in signs)
            {
                var b = sign.bounds;
                float floor = new[] { StationSurvey.Ground, StationSurvey.Concourse, StationSurvey.Upper }.Where(f => b.center.y - f > .6f && b.center.y - f < 3.6f).DefaultIfEmpty(float.NaN).Max();
                if (float.IsNaN(floor)) continue;
                var label = (sign.name.Contains("남자") ? "남자 화장실" : sign.name.Contains("여자") ? "여자 화장실" : sign.name.Contains("장애인") ? "장애인 화장실" : "화장실") + " · " + StationSurvey.FloorName(floor);
                add("toilet-" + index++, "Toilet", label, new Vector3(b.center.x, floor, b.center.z), 2.5f, 0, null, null);
            }
        }

        /// <summary>Standing spots: between the hall benches, and a sparse grid on the other public floors.</summary>
        private static void AddWaits(List<StationPoints.PointEntry> points, StationPointsZones zones, Func<Vector3, bool> reachable)
        {
            int wait = 0;
            void Grid(float x0, float x1, float z0, float z1, float y, float step, string label)
            {
                for (float x = x0; x <= x1; x += step)
                    for (float z = z0; z <= z1; z += step)
                    {
                        if (!NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, .6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - y) > .6f) continue;
                        // 벽·기둥에 바짝 붙은 자리는 쓰지 않는다.
                        if (!NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas) || edge.distance < .8f) continue;
                        if (!reachable(hit.position)) continue;
                        points.Add(new StationPoints.PointEntry { id = "wait-" + wait++, kind = "Wait", label = label, zone = zones.At(hit.position), position = Round(hit.position), yaw = (wait * 137.5f) % 360 });
                    }
            }
            Grid(52, 86, 4, 34, 7, 3.4f, "대합실 좌석 구역");
            Grid(20, 110, -30, 60, 7, 9f, "맞이방");
            Grid(0, 112, -52, 56, 12.2f, 8f, "3층");
            Grid(-65, 14, -90, 88, 0, 10f, "1층");
            Grid(-60, 18, -95, 88, 7, 12f, "2층 본관");
            Grid(115, 200, -80, 40, 7, 14f, "하늘광장");
        }

        /// <summary>
        /// 대합실 의자(Box_Majibang_Bench_Wood, 한 개의 결합 메시)를 위에서 0.1m 격자로 찍어 의자 하나하나를 분리하고,
        /// 팔걸이로 나뉜 칸마다 양쪽 방향의 착석 지점을 만든다. 등받이가 없는 의자라 어느 쪽으로도 앉을 수 있다.
        /// </summary>
        private static void AddSeats(List<StationPoints.PointEntry> points, List<string> rejected, Func<Vector3, bool> reachable, StationPointsZones zones)
        {
            var wood = GameObject.Find("Box_Majibang_Bench_Wood")?.GetComponent<Collider>();
            if (wood == null) { rejected.Add("seats:no-bench-collider"); return; }
            const float step = .1f;
            var box = wood.bounds;
            int nx = Mathf.CeilToInt(box.size.x / step) + 1, nz = Mathf.CeilToInt(box.size.z / step) + 1;
            var hit = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    var origin = new Vector3(box.min.x + i * step, box.max.y + .5f, box.min.z + k * step);
                    hit[i, k] = wood.Raycast(new Ray(origin, Vector3.down), out _, 2f);
                }
            var seen = new bool[nx, nz];
            var stack = new Stack<Vector2Int>();
            var cells = new List<Vector2>();
            int bench = 0;
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    if (!hit[i, k] || seen[i, k]) continue;
                    cells.Clear();
                    stack.Push(new Vector2Int(i, k));
                    seen[i, k] = true;
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        cells.Add(new Vector2(box.min.x + c.x * step, box.min.z + c.y * step));
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                int x = c.x + dx, z = c.y + dz;
                                if (x < 0 || z < 0 || x >= nx || z >= nz || seen[x, z] || !hit[x, z]) continue;
                                seen[x, z] = true;
                                stack.Push(new Vector2Int(x, z));
                            }
                    }
                    if (cells.Count < 40) continue;
                    var mean = Vector2.zero;
                    foreach (var c in cells) mean += c;
                    mean /= cells.Count;
                    float sxx = 0, sxz = 0, szz = 0;
                    foreach (var c in cells) { var d = c - mean; sxx += d.x * d.x; sxz += d.x * d.y; szz += d.y * d.y; }
                    float theta = .5f * Mathf.Atan2(2 * sxz, sxx - szz);
                    var axis = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var c in cells) { float t = Vector2.Dot(c - mean, axis); lo = Mathf.Min(lo, t); hi = Mathf.Max(hi, t); }
                    float length = hi - lo + step;
                    int slots = Mathf.Max(1, Mathf.RoundToInt(length / .7f));
                    var perp = new Vector3(-axis.y, 0, axis.x);
                    var along = new Vector3(axis.x, 0, axis.y);
                    var centre = new Vector3(mean.x, 0, mean.y) + along * ((lo + hi) * .5f);
                    for (int s = 0; s < slots; s++)
                    {
                        var seat = centre + along * ((s - (slots - 1) * .5f) * length / slots);
                        foreach (var (side, facing) in new[] { ("a", perp), ("b", -perp) })
                        {
                            var id = "seat-" + bench.ToString("000") + "-" + s + side;
                            var approach = new Vector3(seat.x, 7, seat.z) + facing * .75f;
                            if (!NavMesh.SamplePosition(approach, out var standHit, .35f, NavMesh.AllAreas) || !reachable(standHit.position)) { rejected.Add(id); continue; }
                            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                            points.Add(new StationPoints.PointEntry
                            {
                                id = id, kind = "Seat", label = "대합실 의자", zone = zones.At(standHit.position),
                                position = Round(standHit.position), yaw = Mathf.Round(yaw), anchor = Round(new Vector3(seat.x, 7, seat.z)),
                                slot = "seat-" + bench.ToString("000") + "-" + s,
                            });
                        }
                    }
                    bench++;
                }
        }

        // ── 객실 안쪽 자동문 ─────────────────────────────────────────────────

        /// <summary>
        /// Splits each passenger car's interior meshes into a fixed part and the vestibule-to-saloon sliding leaf, so
        /// the leaf can open as people pass. The closed leaf region and slide come from car 2, whose leaf the twin
        /// already carries as a separate open patch (Coach02InnerDoorStaticOpenPatch).
        /// </summary>
        [MenuItem("ChooGuard/Emergency/Build train inner doors")]
        public static EmergencyArt.TrainDoorPatch[] BuildTrainDoors()
        {
            RequireStation();
            var ktx = GameObject.Find(StationSurvey.KtxPath).transform;
            var car2 = ktx.Cast<Transform>().First(c => c.name.StartsWith("Car02", StringComparison.Ordinal));
            var patch = ktx.Cast<Transform>().First(c => c.name == "Coach02InnerDoorStaticOpenPatch");
            // 열린 문짝 꼭짓점(차량 좌표).
            var leafOpen = new List<Vector3>();
            foreach (var filter in patch.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.Contains("InnerLeaf")))
                foreach (var v in filter.sharedMesh.vertices) leafOpen.Add(car2.InverseTransformPoint(filter.transform.TransformPoint(v)));
            // 원본(비활성) 객실 메시의 꼭짓점과 가장 잘 겹치는 가로 이동량이 문이 열린 거리다.
            var original = new HashSet<Vector3Int>();
            foreach (var filter in car2.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.Contains("_in_") && !f.gameObject.activeSelf))
                foreach (var v in filter.sharedMesh.vertices) original.Add(Key(car2.InverseTransformPoint(filter.transform.TransformPoint(v))));
            float bestShift = 0; int bestHits = -1;
            for (int step = -140; step <= 140; step++)
            {
                float shift = step * .0001f;
                int hits = leafOpen.Count(v => original.Contains(Key(v - new Vector3(shift, 0, 0))));
                if (hits > bestHits) { bestHits = hits; bestShift = shift; }
            }
            var slide = new Vector3(bestShift, 0, 0);
            var closedMin = new Vector3(leafOpen.Min(v => v.x), leafOpen.Min(v => v.y), leafOpen.Min(v => v.z)) - slide;
            var closedMax = new Vector3(leafOpen.Max(v => v.x), leafOpen.Max(v => v.y), leafOpen.Max(v => v.z)) - slide;
            var pad = new Vector3(.0003f, .0003f, .0003f);
            closedMin -= pad; closedMax += pad;
            Directory.CreateDirectory(TrainPatchRoot);
            var patches = new List<EmergencyArt.TrainDoorPatch>();
            foreach (Transform car in ktx)
            {
                if (!car.name.StartsWith("Car", StringComparison.Ordinal)) continue;
                foreach (var filter in car.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.Contains("_in_")))
                {
                    // 2호차의 원본 메시는 비활성, 정적 열림 패치가 대신한다. 모든 차량을 원본에서 같은 규칙으로 나눈다.
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var toCar = car.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var vertices = mesh.vertices;
                    var carVerts = vertices.Select(v => toCar.MultiplyPoint3x4(v)).ToArray();
                    bool Inside(Vector3 p) => p.x >= closedMin.x && p.x <= closedMax.x && p.y >= closedMin.y && p.y <= closedMax.y && p.z >= closedMin.z && p.z <= closedMax.z;
                    var fixedSub = new List<int[]>();
                    var leafSub = new List<int[]>();
                    int leafTriangles = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var tris = mesh.GetTriangles(s);
                        var f = new List<int>(); var l = new List<int>();
                        for (int t = 0; t < tris.Length; t += 3)
                        {
                            var c = (carVerts[tris[t]] + carVerts[tris[t + 1]] + carVerts[tris[t + 2]]) / 3f;
                            var dest = Inside(c) && Inside(carVerts[tris[t]]) && Inside(carVerts[tris[t + 1]]) && Inside(carVerts[tris[t + 2]]) ? l : f;
                            dest.Add(tris[t]); dest.Add(tris[t + 1]); dest.Add(tris[t + 2]);
                        }
                        leafTriangles += l.Count / 3;
                        fixedSub.Add(f.ToArray()); leafSub.Add(l.ToArray());
                    }
                    if (leafTriangles < 4) continue;
                    var baseName = car.name.Substring(0, 5) + "_" + filter.name.Substring(filter.name.IndexOf("_in_", StringComparison.Ordinal) + 1);
                    var fixedMesh = Split(mesh, fixedSub, baseName + "_fixed");
                    var leafMesh = Split(mesh, leafSub, baseName + "_leaf");
                    patches.Add(new EmergencyArt.TrainDoorPatch { Car = car.name, Source = filter.name, Fixed = fixedMesh, Leaf = leafMesh, Slide = slide });
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CG_TRAIN_DOORS slide=" + (bestShift * 100).ToString("F2") + "m overlap=" + bestHits + "/" + leafOpen.Count + " patches=" + patches.Count + " cars=" + string.Join(",", patches.Select(p => p.Car.Substring(0, 5)).Distinct()));
            return patches.ToArray();
        }

        private static Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 20000), Mathf.RoundToInt(v.y * 20000), Mathf.RoundToInt(v.z * 20000));

        private static Mesh Split(Mesh source, List<int[]> submeshes, string name)
        {
            var path = TrainPatchRoot + "/" + name + ".asset";
            var mesh = new Mesh { name = name, indexFormat = source.indexFormat };
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.tangents = source.tangents;
            mesh.uv = source.uv;
            if (source.uv2 != null && source.uv2.Length == source.vertexCount) mesh.uv2 = source.uv2;
            if (source.colors != null && source.colors.Length == source.vertexCount) mesh.colors = source.colors;
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s, false);
            mesh.RecalculateBounds();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
