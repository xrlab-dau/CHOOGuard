using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Runtime view of the whole Busan twin: the baked world navmesh (added on open, removed on close), escalator and
    /// elevator links, places, zones and platforms from the station data, seat and place reservations, and closed-off
    /// areas. FpsStation.unity itself is never modified.
    /// </summary>
    public sealed class StationWorld : IDisposable
    {
        public const int EscalatorArea = 3, ElevatorArea = 4, StairsArea = 5;

        public StationPoints Points { get; }
        public System.Random Random { get; }
        public int Seed { get; }
        public TrainService Train { get; set; }
        public readonly List<Escalator> Escalators = new List<Escalator>();
        public readonly List<Elevator> Elevators = new List<Elevator>();

        /// <summary>Closed-off discs (cordons, fires, fallen objects). People do not choose places inside them.</summary>
        public readonly List<(Vector3 centre, float radius, string label)> Closed = new List<(Vector3, float, string)>();

        private NavMeshDataInstance navmesh;
        private readonly GameObject links;
        private readonly HashSet<string> takenSlots = new HashSet<string>();
        private readonly HashSet<StationPoints.Point> takenPlaces = new HashSet<StationPoints.Point>();
        private readonly NavMeshPath scratch = new NavMeshPath();
        private readonly Vector3[] corners = new Vector3[256];

        public StationWorld(EmergencyArt art, int seed, Transform parent = null)
        {
            if (art == null || art.WorldNavMesh == null || art.StationData == null) throw new ArgumentException("EmergencyArt 의 navmesh·역 자료가 없습니다.");
            Seed = seed;
            Random = new System.Random(seed);
            Points = StationPoints.Load(art.StationData);
            navmesh = NavMesh.AddNavMeshData(art.WorldNavMesh);
            // 방송 한 번에 역 안 백여 명이 동시에 출구까지 긴 길을 다시 찾는다. 기본 예산(프레임당 100회)으로는 수십 초 밀린다.
            NavMesh.pathfindingIterationsPerFrame = 1000;
            if (parent == null) return;
            links = new GameObject("승강 설비");
            links.transform.SetParent(parent, false);
            foreach (var entry in Points.Escalators)
            {
                var go = new GameObject(entry.label);
                go.transform.SetParent(links.transform, false);
                var escalator = go.AddComponent<Escalator>();
                escalator.Setup(entry);
                escalator.AddStopButtons();
                Escalators.Add(escalator);
            }
            foreach (var entry in Points.Elevators)
            {
                var go = new GameObject(entry.label);
                go.transform.SetParent(links.transform, false);
                var elevator = go.AddComponent<Elevator>();
                elevator.Setup(entry);
                Elevators.Add(elevator);
            }
        }

        public void Dispose()
        {
            if (links != null) UnityEngine.Object.Destroy(links);
            if (navmesh.valid) navmesh.Remove();
        }

        public float Range(float min, float max) => min + (float)Random.NextDouble() * (max - min);
        public bool Chance(float probability) => Random.NextDouble() < probability;
        public T Pick<T>(IReadOnlyList<T> list) => list.Count == 0 ? default : list[Random.Next(list.Count)];

        public bool IsClosed(Vector3 position, float margin = 0)
        {
            foreach (var zone in Closed)
            {
                var d = position - zone.centre;
                if (Mathf.Abs(d.y) > 3) continue;
                d.y = 0;
                if (d.sqrMagnitude < (zone.radius + margin) * (zone.radius + margin)) return true;
            }
            return false;
        }

        // ── 자리 예약 ──────────────────────────────────────────────────────

        /// <summary>A free bench side near <paramref name="near"/> (random among the closest few), reserved for the caller.</summary>
        public StationPoints.Point ReserveSeat(Vector3 near, float maxDistance = 1e4f) => ReserveSlot(PointKind.Seat, near, maxDistance, 18);

        /// <summary>A free cafe chair, preferring <paramref name="filter"/> (a shop, a zone), reserved for the caller.</summary>
        public StationPoints.Point ReserveChair(Vector3 near, float maxDistance, Func<StationPoints.Point, bool> filter = null) => ReserveSlot(PointKind.Chair, near, maxDistance, 6, filter);

        private StationPoints.Point ReserveSlot(PointKind kind, Vector3 near, float maxDistance, float jitter, Func<StationPoints.Point, bool> filter = null)
        {
            StationPoints.Point best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var seat in Points.Of(kind))
            {
                if (takenSlots.Contains(seat.Slot) || IsClosed(seat.Anchor, 1.5f)) continue;
                if (filter != null && !filter(seat)) continue;
                float distance = Vector3.Distance(seat.Position, near);
                if (distance > maxDistance) continue;
                float score = distance + Range(0, jitter);
                if (score < bestScore) { bestScore = score; best = seat; }
            }
            if (best != null) takenSlots.Add(best.Slot);
            return best;
        }

        public void ReleaseSeat(StationPoints.Point seat)
        {
            if (seat != null) takenSlots.Remove(seat.Slot);
        }

        /// <summary>A free place of the kind (counter, shop, standing spot), reserved for the caller. Shops take several people.</summary>
        public StationPoints.Point ReservePlace(PointKind kind, Func<StationPoints.Point, bool> filter = null)
        {
            var list = Points.Of(kind);
            if (list.Count == 0) return null;
            bool shared = kind == PointKind.Shop || kind == PointKind.Toilet || kind == PointKind.Exit || kind == PointKind.Meet;
            int start = Random.Next(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var point = list[(start + i) % list.Count];
                if (!shared && takenPlaces.Contains(point)) continue;
                if (filter != null && !filter(point)) continue;
                if (IsClosed(point.Position, 1.5f)) continue;
                if (!shared) takenPlaces.Add(point);
                return point;
            }
            return null;
        }

        public void ReleasePlace(StationPoints.Point point)
        {
            if (point != null) takenPlaces.Remove(point);
        }

        /// <summary>Random city exit (for routine arrivals and departures).</summary>
        public StationPoints.Point RandomExit(StationPoints.Point except = null)
        {
            var exits = Points.Of(PointKind.Exit);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var exit = exits[Random.Next(exits.Count)];
                if (exit != except) return exit;
            }
            return exits[0];
        }

        // ── 길 ─────────────────────────────────────────────────────────────

        /// <summary>Walking length (m) of the navmesh path, or +inf when there is none.</summary>
        public float PathLength(Vector3 from, Vector3 to)
        {
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, scratch) || scratch.status != NavMeshPathStatus.PathComplete) return float.PositiveInfinity;
            float length = 0;
            int count = scratch.GetCornersNonAlloc(corners);
            for (int i = 1; i < count; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        /// <summary>Walking length (m) of the route <see cref="Via"/> would take (platform trips split at a well), or +inf.</summary>
        public float RouteLength(Vector3 from, Vector3 to)
        {
            float total = 0;
            var at = from;
            foreach (var stop in Via(from, to, "stairs"))
            {
                total += PathLength(at, stop);
                at = stop;
            }
            return total + PathLength(at, to);
        }

        /// <summary>
        /// Stops on the way from <paramref name="from"/> to <paramref name="to"/> (the destination itself excluded): the platform
        /// wells of <see cref="Via"/>, a point before every change of storey (escalator, elevator, stairs) and about every 30 m.
        /// An agent plans each short leg in full; one long cross-storey plan can come back cut short and leave it standing.
        /// </summary>
        public List<Vector3> Route(Vector3 from, Vector3 to, string route)
        {
            var stops = new List<Vector3>();
            var at = from;
            foreach (var stop in Via(from, to, route))
            {
                AddWaypoints(stops, at, stop);
                stops.Add(stop);
                at = stop;
            }
            AddWaypoints(stops, at, to);
            return stops;
        }

        private void AddWaypoints(List<Vector3> stops, Vector3 from, Vector3 to)
        {
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, scratch) || scratch.status == NavMeshPathStatus.PathInvalid) return;
            int count = scratch.GetCornersNonAlloc(corners);
            float run = 0;
            for (int i = 1; i < count - 1; i++)
            {
                run += Vector3.Distance(corners[i - 1], corners[i]);
                bool storey = Mathf.Abs(corners[i + 1].y - corners[i].y) > 1.5f;
                if (storey || run > 30f) { stops.Add(corners[i]); run = 0; }
            }
        }

        /// <summary>
        /// Intermediate stops for a walk between the concourse and the KTX platform: the long route goes through the
        /// platform's north-deck well (stairs down; escalator or stairs up by <paramref name="route"/>). Splitting it keeps
        /// every navmesh path short enough to be found in full. Empty when no split is needed.
        /// </summary>
        public List<Vector3> Via(Vector3 from, Vector3 to, string route)
        {
            var stops = new List<Vector3>();
            var trainPlatform = Points.Train?.platform;
            if (trainPlatform == null) return stops;
            bool fromPlatform = Points.PlatformAt(from)?.id == trainPlatform, toPlatform = Points.PlatformAt(to)?.id == trainPlatform;
            if (fromPlatform == toPlatform) return stops;
            var well = BestWell(fromPlatform ? from : to);
            if (well == null) return stops;
            if (toPlatform)
            {
                // 내려갈 때: 위로 가는 에스컬레이터 옆 계단(없으면 내려가는 에스컬레이터 타는 곳).
                if (well.Entry.up && well.Entry.stairs) AddStairs(stops, well.Entry.stairsTop, well.Entry.stairsBottom);
                else if (!well.Entry.up) stops.Add(well.Start);
            }
            else
            {
                bool escalator = well.Entry.up && (route != "stairs" || !well.Entry.stairs);
                if (escalator) { stops.Add(well.Start); stops.Add(well.End); }
                else if (well.Entry.stairs) AddStairs(stops, well.Entry.stairsBottom, well.Entry.stairsTop);
            }
            return stops;
        }

        /// <summary>
        /// The two ends of a stair on a keep-right lane (stairs in Korea are walked on the right): both ends moved about 0.45 m
        /// to the walker's right, spread a little per person. With everyone sent to the one centre point at each end, the up and
        /// down streams met head-on there and the crowd solver shoved people apart a third of a metre at a time.
        /// </summary>
        private void AddStairs(List<Vector3> stops, Vector3 from, Vector3 to)
        {
            var along = to - from;
            along.y = 0;
            var right = along.sqrMagnitude > .01f ? Vector3.Cross(Vector3.up, along.normalized) * (.45f + Range(-.15f, .15f)) : Vector3.zero;
            stops.Add(Lane(from, right));
            stops.Add(Lane(to, right));
        }

        private static Vector3 Lane(Vector3 end, Vector3 shift) =>
            NavMesh.SamplePosition(end + shift, out var hit, .3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - end.y) < .3f ? hit.position : end;

        private readonly Dictionary<Escalator, bool> wellReach = new Dictionary<Escalator, bool>();

        private Escalator BestWell(Vector3 platformPoint)
        {
            Escalator best = null;
            float bestDistance = float.PositiveInfinity;
            var trainPlatform = Points.Train?.platform;
            foreach (var escalator in Escalators)
            {
                if (escalator.Closed || !escalator.Entry.id.StartsWith("esc-well", StringComparison.Ordinal)) continue;
                var bottom = escalator.Entry.up ? escalator.Start : escalator.End;
                if (Points.PlatformAt(bottom, 1.5f)?.id != trainPlatform) continue;
                // 맞이방에서 닿는 우물만(남측 게이트 쪽은 트윈에서 끊겨 있다): 윗 계단참이 맞이방과 이어진 곳.
                // 계단참에서 맞이방 쪽 길은 에스컬레이터를 타고 돌아가도 이어지므로, 맞이방에서 계단참으로 가는 길도 본다.
                if (!wellReach.TryGetValue(escalator, out bool connected))
                {
                    var top = escalator.Entry.up ? escalator.End : escalator.Start;
                    var hall = OnNavMesh(new Vector3(64, 7, -2));
                    wellReach[escalator] = connected = !float.IsPositiveInfinity(PathLength(top, hall)) && !float.IsPositiveInfinity(PathLength(hall, top));
                }
                if (!connected) continue;
                float d = Vector3.Distance(bottom, platformPoint);
                if (d < bestDistance) { bestDistance = d; best = escalator; }
            }
            return best;
        }

        /// <summary>
        /// City exit with the shortest walk from <paramref name="from"/> whose path keeps clear of <paramref name="avoid"/>.
        /// Falls back to the farthest exit from the danger when every path passes near it.
        /// </summary>
        public StationPoints.Point SafeExit(Vector3 from, Vector3? avoid, float clearance)
        {
            StationPoints.Point best = null, farthest = null;
            float bestLength = float.PositiveInfinity, farthestDistance = -1;
            foreach (var exit in Points.Of(PointKind.Exit))
            {
                if (avoid.HasValue)
                {
                    float away = Vector3.Distance(exit.Position, avoid.Value);
                    if (away > farthestDistance) { farthestDistance = away; farthest = exit; }
                }
                if (!NavMesh.CalculatePath(from, exit.Position, NavMesh.AllAreas, scratch) || scratch.status != NavMeshPathStatus.PathComplete) continue;
                float length = 0;
                bool clear = true;
                int count = scratch.GetCornersNonAlloc(corners);
                for (int i = 1; i < count; i++)
                {
                    length += Vector3.Distance(corners[i - 1], corners[i]);
                    if (avoid.HasValue && Mathf.Abs(corners[i].y - avoid.Value.y) < 3 && SegmentDistance(avoid.Value, corners[i - 1], corners[i]) < clearance) clear = false;
                }
                if (clear && length < bestLength) { bestLength = length; best = exit; }
            }
            return best ?? farthest ?? Points.Of(PointKind.Exit)[0];
        }

        public static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0;
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(point, a + ab * t);
        }

        /// <summary>Nearest walkable point, or the input when none is within <paramref name="radius"/>.</summary>
        public static Vector3 OnNavMesh(Vector3 position, float radius = 2f) =>
            NavMesh.SamplePosition(position, out var hit, radius, NavMesh.AllAreas) ? hit.position : position;

        /// <summary>A walkable point about <paramref name="distance"/> away from <paramref name="danger"/>, on the far side from it.</summary>
        public Vector3 AwayFrom(Vector3 from, Vector3 danger, float distance)
        {
            var away = from - danger; away.y = 0;
            if (away.sqrMagnitude < .01f) away = new Vector3(Range(-1, 1), 0, Range(-1, 1));
            away.Normalize();
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var direction = Quaternion.Euler(0, Range(-35, 35) * (attempt + 1) / 2f, 0) * away;
                var target = danger + direction * distance;
                target.y = from.y;
                if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - from.y) < 2.5f && !IsClosed(hit.position)) return hit.position;
            }
            return WalkableNear(from);
        }

        /// <summary>
        /// The walkable floor point nearest <paramref name="point"/> on the same storey. A spot on a table top or between
        /// seats is not on the navmesh, and a plain nearest-point query can return the floor below instead.
        /// </summary>
        public static Vector3 WalkableNear(Vector3 point, float radius = 6f)
        {
            if (NavMesh.SamplePosition(point, out var direct, 1f, NavMesh.AllAreas) && Mathf.Abs(direct.position.y - point.y) < 1.5f) return direct.position;
            Vector3 best = point;
            float bestDistance = float.PositiveInfinity;
            for (float ring = 1f; ring <= radius && float.IsPositiveInfinity(bestDistance); ring += 1f)
                for (int step = 0; step < 12; step++)
                {
                    var probe = point + Quaternion.Euler(0, step * 30f, 0) * Vector3.forward * ring;
                    if (!NavMesh.SamplePosition(probe, out var hit, .9f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - point.y) > 1.5f) continue;
                    float d = Vector3.Distance(hit.position, point);
                    if (d < bestDistance) { bestDistance = d; best = hit.position; }
                }
            return best;
        }

        /// <summary>Where a responder coming from <paramref name="from"/> stands, about <paramref name="standOff"/> m from <paramref name="scene"/> on its storey.</summary>
        public Vector3 Approach(Vector3 scene, Vector3 from, float standOff)
        {
            var floor = WalkableNear(scene);
            var toward = from - floor; toward.y = 0;
            if (toward.sqrMagnitude < .01f) toward = Vector3.forward;
            toward.Normalize();
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float turn = (attempt % 2 == 0 ? 1 : -1) * 25f * ((attempt + 1) / 2);
                var target = floor + Quaternion.Euler(0, turn, 0) * toward * standOff;
                if (NavMesh.SamplePosition(target, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - floor.y) < 1.5f && !IsClosed(hit.position)) return hit.position;
            }
            return floor;
        }

        // ── 장소 이름 ────────────────────────────────────────────────────────

        public string ZoneId(Vector3 position)
        {
            if (Train != null && Train.CarAt(position) != null) return "train";
            var platform = Points.PlatformAt(position);
            if (platform != null) return platform.id;
            return Points.ZoneAt(position)?.id ?? "";
        }

        /// <summary>The name staff would use on the radio for <paramref name="position"/>, anywhere in the station.</summary>
        public string Describe(Vector3 position)
        {
            if (Train != null)
            {
                var car = Train.CarAt(position);
                if (car != null) return "KTX " + car.Label + " 안";
            }
            foreach (var escalator in Escalators)
                if (escalator.DistanceTo(position) < 1.3f) return escalator.Label;
            // 승강장 계단은 '승강장 구역' 상자 안에 있어도 선로가 아니다: 옆 에스컬레이터로 부른다.
            var stairs = StairsAt(position);
            if (stairs != null) return stairs.label + " 옆 계단";
            var platform = Points.PlatformAt(position);
            if (platform != null)
            {
                if (Train != null && Train.AtPlatform && platform.id == Points.Train?.platform)
                {
                    TrainService.Car nearest = null;
                    float best = 9;
                    foreach (var car in Train.Cars)
                    {
                        float d = Vector3.Distance(Train.World(car.DoorOutside), position);
                        if (d < best) { best = d; nearest = car; }
                    }
                    if (nearest != null) return platform.label + " " + nearest.Label + " 문 앞";
                }
                return platform.label;
            }
            var zone = Points.ZoneAt(position);
            var zoneLabel = zone?.label ?? "역 구내";
            if (zone != null && zone.id == "tracks") return "선로 쪽";
            StationPoints.Point landmark = null;
            float bestDistance = 25;
            foreach (var kind in new[] { PointKind.Counter, PointKind.Shop, PointKind.Toilet, PointKind.Office, PointKind.Meet, PointKind.Exit })
                foreach (var point in Points.Of(kind))
                {
                    if (zone != null && point.Zone != zone.id) continue;
                    if (Mathf.Abs(point.Position.y - position.y) > 3) continue;
                    float d = Vector3.Distance(point.Position, position);
                    if (d < bestDistance) { bestDistance = d; landmark = point; }
                }
            var seat = Points.Nearest(PointKind.Seat, position);
            bool seats = seat != null && Vector3.Distance(seat.Anchor, position) < 4;
            if (landmark == null || bestDistance > 9 && seats) return seats ? zoneLabel + " 대합실 의자 구역" : zoneLabel;
            string name = landmark.Kind == PointKind.Counter ? "매표창구" : landmark.Kind == PointKind.Toilet ? "화장실" : landmark.Kind == PointKind.Office ? "역무실" : landmark.Label;
            // 9 m 안이면 '앞', 더 멀면 '근처'(같은 층에서 알아볼 만한 가장 가까운 곳).
            if (bestDistance > 9) return zoneLabel + " " + (name.EndsWith("앞", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1).TrimEnd() : name) + " 근처";
            return zoneLabel + " " + name + (name.EndsWith("앞", StringComparison.Ordinal) ? "" : " 앞");
        }

        /// <summary>Short label of the floor or area, for JEV context ("platform 5·6", "2F concourse", "KTX car 3").</summary>
        public string Area(Vector3 position)
        {
            if (Train != null) { var car = Train.CarAt(position); if (car != null) return "inside KTX car " + car.Number; }
            var platform = Points.PlatformAt(position);
            if (platform != null) return "on platform " + platform.label.Replace(" 타는 곳", "");
            var stairs = StairsAt(position);
            if (stairs != null) return "on the stairs between platform " + (Points.PlatformAt(stairs.stairsBottom)?.label.Replace(" 타는 곳", "") ?? "?") + " and the 2F concourse";
            switch (Points.ZoneAt(position)?.id)
            {
                case "hall2f": return "2F waiting hall";
                case "southgate": return "2F south boarding gate";
                case "northdeck": return "2F north deck (arrival exits)";
                case "main2f": return "2F main building";
                case "eastexit": return "2F east exit";
                case "skyplaza": return "sky plaza (port side)";
                case "upper3f": return "3F shops and restaurants";
                case "ground1f": return "1F";
                case "plaza": return "station square / street";
                case "tracks": return "track side";
                default: return "in the station";
            }
        }

        /// <summary>
        /// The flight of stairs beside an escalator well that <paramref name="position"/> stands on (navmesh stairs area),
        /// or null anywhere else.
        /// </summary>
        public StationPoints.EscalatorEntry StairsAt(Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out var hit, .4f, 1 << StairsArea) || Mathf.Abs(hit.position.y - position.y) > .3f) return null;
            StationPoints.EscalatorEntry nearest = null;
            float best = 6;
            foreach (var escalator in Escalators)
            {
                var entry = escalator.Entry;
                if (!entry.stairs) continue;
                var along = entry.stairsBottom - entry.stairsTop;
                float t = Mathf.Clamp01(Vector3.Dot(position - entry.stairsTop, along) / Mathf.Max(along.sqrMagnitude, 1e-4f));
                float d = Vector3.Distance(position, entry.stairsTop + along * t);
                if (d < best) { best = d; nearest = entry; }
            }
            return nearest;
        }
    }
}
