using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public enum PointKind { Exit, Counter, Shop, Wait, Office, Seat, Chair, Toilet, PlatformWait, Meet }

    /// <summary>
    /// Navmesh-validated places, zones, vertical links, platforms and the KTX interior of the Busan twin, generated at
    /// edit time (station-points.json, version 2). Train positions are world positions at the set's stop pose.
    /// </summary>
    public sealed class StationPoints
    {
        [Serializable] public sealed class ZoneEntry { public string id, label; public Vector3 min, max; public int priority; }
        [Serializable] public sealed class PointEntry { public string id, kind, label, zone, slot; public Vector3 position, anchor; public float yaw; }
        [Serializable] public sealed class EscalatorEntry { public string id, label, from, to; public bool up, stairs; public Vector3[] path; public Vector3 stairsTop, stairsBottom; }
        [Serializable] public sealed class ElevatorStop { public string floor, zone; public Vector3 door; public float yaw; }
        [Serializable] public sealed class ElevatorEntry { public string id, label; public ElevatorStop[] stops; }
        [Serializable] public sealed class PlatformEntry { public string id, label; public Vector3 a, b; public float halfWidth; }
        [Serializable] public sealed class DoorEntry { public string[] leaves; public Vector3 openDelta; public string lift; public Vector3 liftDelta, outside, inside; }
        [Serializable] public sealed class SeatEntry { public string id; public int row; public Vector3 anchor, front, aisle; public float yaw; }
        [Serializable] public sealed class CarEntry { public string name, label; public int number; public float floor; public bool reachable; public DoorEntry door; public Vector3[] aisle; public SeatEntry[] seats; public Vector3 min, max; }
        [Serializable] public sealed class TrainEntry { public string root, platform, track; public Vector3 axis; public CarEntry[] cars; }
        [Serializable] private sealed class File { public int version; public string note; public ZoneEntry[] zones; public PointEntry[] points; public EscalatorEntry[] escalators; public ElevatorEntry[] elevators; public PlatformEntry[] platforms; public TrainEntry train; }

        public sealed class Point
        {
            public string Id, Label, Zone;
            public PointKind Kind;
            /// <summary>Walkable navmesh position where a person stands for this place.</summary>
            public Vector3 Position;
            /// <summary>Facing on arrival (degrees). For seats and chairs: the direction the seated person faces.</summary>
            public float Yaw;
            /// <summary>Seats and chairs: floor point under the seated hips.</summary>
            public Vector3 Anchor;
            /// <summary>Seats: both sides of one bench slot share this key; one person per slot.</summary>
            public string Slot;
        }

        private readonly List<Point> all = new List<Point>();
        private readonly Dictionary<PointKind, List<Point>> byKind = new Dictionary<PointKind, List<Point>>();

        public IReadOnlyList<Point> All => all;
        public IReadOnlyList<ZoneEntry> Zones { get; private set; } = Array.Empty<ZoneEntry>();
        public IReadOnlyList<EscalatorEntry> Escalators { get; private set; } = Array.Empty<EscalatorEntry>();
        public IReadOnlyList<ElevatorEntry> Elevators { get; private set; } = Array.Empty<ElevatorEntry>();
        public IReadOnlyList<PlatformEntry> Platforms { get; private set; } = Array.Empty<PlatformEntry>();
        public TrainEntry Train { get; private set; }

        public static StationPoints Load(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var file = JsonUtility.FromJson<File>(asset.text);
            if (file == null || file.version != 2 || file.points == null || file.points.Length == 0)
                throw new InvalidOperationException("station-points.json 구조 오류 (version 2 필요)");
            var points = new StationPoints
            {
                Zones = file.zones ?? Array.Empty<ZoneEntry>(),
                Escalators = file.escalators ?? Array.Empty<EscalatorEntry>(),
                Elevators = file.elevators ?? Array.Empty<ElevatorEntry>(),
                Platforms = file.platforms ?? Array.Empty<PlatformEntry>(),
                Train = file.train != null && file.train.cars != null && file.train.cars.Length > 0 ? file.train : null,
            };
            foreach (var entry in file.points)
            {
                if (!Enum.TryParse(entry.kind, out PointKind kind)) throw new InvalidOperationException("알 수 없는 지점 종류: " + entry.kind);
                var point = new Point { Id = entry.id, Label = entry.label, Zone = entry.zone ?? "", Kind = kind, Position = entry.position, Yaw = entry.yaw, Anchor = entry.anchor, Slot = string.IsNullOrEmpty(entry.slot) ? entry.id : entry.slot };
                points.all.Add(point);
                if (!points.byKind.TryGetValue(kind, out var list)) points.byKind[kind] = list = new List<Point>();
                list.Add(point);
            }
            if (points.Of(PointKind.Exit).Count == 0) throw new InvalidOperationException("출입 지점이 없습니다.");
            return points;
        }

        public IReadOnlyList<Point> Of(PointKind kind) => byKind.TryGetValue(kind, out var list) ? list : (IReadOnlyList<Point>)Array.Empty<Point>();

        public Point Nearest(PointKind kind, Vector3 from, Func<Point, bool> filter = null)
        {
            Point best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var point in Of(kind))
            {
                if (filter != null && !filter(point)) continue;
                float d = (point.Position - from).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = point; }
            }
            return best;
        }

        /// <summary>The zone containing <paramref name="position"/> (highest priority wins), or null outside all zones.</summary>
        public ZoneEntry ZoneAt(Vector3 position)
        {
            ZoneEntry best = null;
            foreach (var zone in Zones)
            {
                if (position.x < zone.min.x || position.y < zone.min.y || position.z < zone.min.z) continue;
                if (position.x > zone.max.x || position.y > zone.max.y || position.z > zone.max.z) continue;
                if (best == null || zone.priority > best.priority) best = zone;
            }
            return best;
        }

        public ZoneEntry Zone(string id)
        {
            foreach (var zone in Zones) if (zone.id == id) return zone;
            return null;
        }

        /// <summary>Platform whose strip contains <paramref name="position"/> (ground level only).</summary>
        public PlatformEntry PlatformAt(Vector3 position, float margin = .3f)
        {
            if (position.y > 2.5f || position.y < -.6f) return null;
            foreach (var platform in Platforms)
            {
                var ab = platform.b - platform.a; ab.y = 0;
                var ap = position - platform.a; ap.y = 0;
                float t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
                if (t < -.02f || t > 1.02f) continue;
                var closest = platform.a + ab * Mathf.Clamp01(t);
                closest.y = position.y;
                if (Vector3.Distance(closest, position) <= platform.halfWidth + margin) return platform;
            }
            return null;
        }
    }
}
