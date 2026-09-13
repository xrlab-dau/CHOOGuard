using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable]
    public sealed class SpatialFrame
    {
        public string FrameId;
        public Point3 Origin;
        public float YawDegrees;
        public Point3 ToWorld(Point3 local)
        {
            var a = YawDegrees * Math.PI / 180; var c = (float)Math.Cos(a); var s = (float)Math.Sin(a);
            return new Point3(Origin.X + c * local.X + s * local.Z, Origin.Y + local.Y,
                Origin.Z - s * local.X + c * local.Z);
        }
        public Point3 ToLocal(Point3 world)
        {
            var x = world.X - Origin.X; var z = world.Z - Origin.Z;
            var a = YawDegrees * Math.PI / 180; var c = (float)Math.Cos(a); var s = (float)Math.Sin(a);
            return new Point3(c * x - s * z, world.Y - Origin.Y, s * x + c * z);
        }
        public SpatialFrame Copy() => (SpatialFrame)MemberwiseClone();
    }

    [Serializable]
    public sealed class SpatialPose
    {
        public string RegionId, FrameId, PortalId = "";
        public Point3 Position, LocalPosition;
    }

    [Serializable]
    public sealed class RegionExclusion
    {
        public Point3 Center;
        public float SizeX, SizeZ;
        public bool Contains(Point3 p, float margin) => Math.Abs(p.X - Center.X) < SizeX / 2 + margin &&
            Math.Abs(p.Z - Center.Z) < SizeZ / 2 + margin;
    }

    [Serializable]
    public sealed class ConnectedRegionDefinition
    {
        public string Id, Label, FrameId, SceneName, Template, EquipmentAsset, EquipmentLabel,
            ControlledPortalId = "", GeometryStatus;
        public Point3 Center, Hub, EquipmentPosition;
        public float SizeX, SizeZ, Height;
        public float WallHeight;
        public bool Ceiling;
        public RegionExclusion[] Exclusions = Array.Empty<RegionExclusion>();
        public bool Contains(Point3 p) => Math.Abs(p.X - Center.X) <= SizeX / 2 + .01f &&
            Math.Abs(p.Z - Center.Z) <= SizeZ / 2 + .01f && Math.Abs(p.Y - Center.Y) <= .3f;
        public Point3 LocalBoundsCenter(SpatialFrame frame) => frame.ToLocal(new Point3(Center.X, Center.Y + Height / 2, Center.Z));
        public Point3 LocalBoundsSize => new Point3(SizeX, Height, SizeZ);
    }

    [Serializable]
    public sealed class ConnectedPortalDefinition
    {
        public string Id, From, To, TopologyStatus, GeometryStatus;
        public Point3 FromPoint, ToPoint;
        public float ClearWidth, ClearHeight;
        public string[] LinkedDoorEntityIds = Array.Empty<string>(), SourceRefs = Array.Empty<string>();
        public bool StaticBoarding;
        public float WallHeight, ClosureAlong, ClosureBottom, ClosureHeight;
        public bool Ceiling;
        public Point3 ClosurePoint => new Point3(FromPoint.X + (ToPoint.X - FromPoint.X) * ClosureAlong,
            FromPoint.Y + (ToPoint.Y - FromPoint.Y) * ClosureAlong, FromPoint.Z + (ToPoint.Z - FromPoint.Z) * ClosureAlong);
        public bool Contains(Point3 p, float radius, out float along)
        {
            var dx = ToPoint.X - FromPoint.X; var dz = ToPoint.Z - FromPoint.Z;
            var length2 = dx * dx + dz * dz;
            along = ((p.X - FromPoint.X) * dx + (p.Z - FromPoint.Z) * dz) / length2;
            if (along < -.005f || along > 1.005f) return false;
            var x = FromPoint.X + along * dx; var z = FromPoint.Z + along * dz;
            var y = FromPoint.Y + along * (ToPoint.Y - FromPoint.Y);
            return (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) <= Math.Pow(ClearWidth / 2 - radius, 2) &&
                Math.Abs(p.Y - y) <= .3f;
        }
    }

    /// <summary>Public, provisional topology and authored game dimensions; no incident schedule or procedure hints.</summary>
    [Serializable]
    public sealed class ConnectedWorldDefinition
    {
        public int SchemaVersion = 1;
        public int GeometrySchemaVersion;
        public string ProfileId, StartRegionId, Classification;
        public SpatialFrame[] Frames;
        public ConnectedRegionDefinition[] Regions;
        public ConnectedPortalDefinition[] Portals;
        public string[] Limits;

        public ConnectedRegionDefinition Region(string id) => Regions.Single(r => r.Id == id);
        public SpatialFrame Frame(string id) => Frames.Single(f => f.FrameId == id);
        public string[] Neighbors(string id) => Portals.Where(p => p.From == id || p.To == id)
            .Select(p => p.From == id ? p.To : p.From).ToArray();
        public string[] RequiredRegions(string id) => new[] { id }.Concat(Neighbors(id)).Distinct().OrderBy(x => x).ToArray();

        public string[] Route(string from, string to, ISet<string> knownClosed = null)
        {
            if (!Regions.Any(r => r.Id == from) || !Regions.Any(r => r.Id == to)) return Array.Empty<string>();
            var queue = new Queue<string>(); queue.Enqueue(from);
            var parents = new Dictionary<string, string> { [from] = null };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); if (current == to) break;
                foreach (var portal in Portals.Where(p => (p.From == current || p.To == current) && !(knownClosed?.Contains(p.Id) ?? false)))
                {
                    var next = portal.From == current ? portal.To : portal.From;
                    if (parents.ContainsKey(next)) continue;
                    parents[next] = current; queue.Enqueue(next);
                }
            }
            if (!parents.ContainsKey(to)) return Array.Empty<string>();
            var path = new List<string>(); for (var p = to; p != null; p = parents[p]) path.Add(p);
            path.Reverse(); return path.ToArray();
        }

        public SpatialPose Pose(string regionId, Point3 world, string portalId = "")
        {
            var frame = Frame(Region(regionId).FrameId);
            return new SpatialPose { RegionId = regionId, FrameId = frame.FrameId, PortalId = portalId,
                Position = world, LocalPosition = frame.ToLocal(world) };
        }

        public bool TryLocate(SpatialPose previous, Point3 point, float radius, ISet<string> closed, out SpatialPose result)
        {
            result = null;
            if (!point.Finite || !Regions.Any(r => r.Id == previous.RegionId)) return false;
            // Only the actor's current region or an explicitly adjacent corridor can receive a small server step.
            var current = Region(previous.RegionId);
            if (current.Contains(point) && !current.Exclusions.Any(e => e.Contains(point, radius)))
            { result = Pose(current.Id, point); return true; }
            foreach (var portal in Portals.Where(p => p.From == current.Id || p.To == current.Id))
            {
                if (!portal.Contains(point, radius, out var along)) continue;
                if (closed?.Contains(portal.Id) ?? false)
                {
                    if (!portal.Contains(previous.Position, 0, out var before)) before = current.Id == portal.From ? 0 : 1;
                    var closure = GeometrySchemaVersion == 0 ? .5f : portal.ClosureAlong;
                    if ((before < closure) != (along < closure)) return false;
                }
                var region = along < .5f ? portal.From : portal.To;
                result = Pose(region, point, portal.Id); return true;
            }
            return false;
        }

        public EntityState RegionEquipment(string regionId)
        {
            var r = Region(regionId);
            return new EntityState { EntityId = "equipment." + r.Id, Kind = EntityKind.Equipment,
                RegionId = r.Id, FrameId = r.FrameId, Position = r.EquipmentPosition,
                LocalPosition = Frame(r.FrameId).ToLocal(r.EquipmentPosition) };
        }

        public Point3 EquipmentApproach(string regionId)
        {
            var r = Region(regionId);
            var x = r.Hub.X - r.EquipmentPosition.X; var z = r.Hub.Z - r.EquipmentPosition.Z;
            var length = Math.Sqrt(x * x + z * z);
            return new Point3(r.EquipmentPosition.X + (float)(x / length) * 1.5f,
                r.EquipmentPosition.Y, r.EquipmentPosition.Z + (float)(z / length) * 1.5f);
        }

        public WorldState CreateWorldState() => new WorldState { SchemaVersion = 2, WorldId = "synthetic-connected-station",
            ShiftId = "shift-001", SpatialProfileId = ProfileId, Frames = Frames.Select(f => f.Copy()).ToArray(),
            Entities = Regions.Select(r => RegionEquipment(r.Id)).ToArray() };

        public void Validate()
        {
            if (SchemaVersion != 1 || string.IsNullOrEmpty(ProfileId) || Regions == null || Portals == null || Frames == null ||
                Regions.Length != 13 || Portals.Length != 12 || Frames.Length < 1 ||
                Regions.Any(r => r == null || string.IsNullOrEmpty(r.Id) || !r.Center.Finite || !r.Hub.Finite || !r.EquipmentPosition.Finite ||
                    !Positive(r.SizeX) || !Positive(r.SizeZ) || !Positive(r.Height) || string.IsNullOrEmpty(r.Template) || r.Exclusions == null ||
                    !r.Contains(r.Hub) || !r.Contains(r.EquipmentPosition) || !Frames.Any(f => f.FrameId == r.FrameId)) ||
                Regions.Select(r => r.Id).Distinct().Count() != Regions.Length ||
                Regions.Select(r => r.SceneName).Distinct().Count() != Regions.Length ||
                Frames.Any(f => f == null || string.IsNullOrEmpty(f.FrameId) || !f.Origin.Finite || float.IsNaN(f.YawDegrees) || float.IsInfinity(f.YawDegrees)) ||
                Frames.Select(f => f.FrameId).Distinct().Count() != Frames.Length)
                throw new InvalidDataException("Invalid connected-world regions or static frames.");
            // Existing generated scenes used these exact builder constants. Migration is in memory;
            // explicit geometry profiles carry the corresponding authored fields from now on.
            if (GeometrySchemaVersion == 0)
            {
                foreach (var r in Regions)
                { r.WallHeight = r.Template == "forecourt" || r.Template == "track" || r.Template.Contains("platform") ? 1.15f : 3.1f;
                    r.Ceiling = r.Template != "forecourt" && r.Template != "track"; }
                foreach (var p in Portals.Where(p => p != null))
                { var dx = p.ToPoint.X - p.FromPoint.X; var dy = p.ToPoint.Y - p.FromPoint.Y; var dz = p.ToPoint.Z - p.FromPoint.Z;
                    var horizontal = Math.Sqrt((double)dx * dx + (double)dz * dz);
                    p.WallHeight = horizontal > 0 ? (float)(1.5 * Math.Sqrt(horizontal * horizontal + dy * dy) / horizontal) : 1.5f;
                    p.Ceiling = false; p.ClosureAlong = .5f; p.ClosureBottom = 0; p.ClosureHeight = 2.3f; }
                GeometrySchemaVersion = 1;
            }
            if (GeometrySchemaVersion != 1 || Regions.Any(r => !Nonnegative(r.WallHeight) || r.WallHeight > r.Height))
                throw new InvalidDataException("Invalid authored enclosure profile.");
            foreach (var p in Portals)
            {
                if (p == null || p.From == p.To || p.Id != p.From + "--" + p.To || !Regions.Any(r => r.Id == p.From) ||
                    !Regions.Any(r => r.Id == p.To) || !p.FromPoint.Finite || !p.ToPoint.Finite ||
                    !Positive(p.ClearWidth) || p.ClearWidth < 1 || !Positive(p.ClearHeight) || p.ClearHeight < 2 ||
                    !Region(p.From).Contains(p.FromPoint) || !Region(p.To).Contains(p.ToPoint) || p.LinkedDoorEntityIds == null ||
                    p.LinkedDoorEntityIds.Any(id => !Regions.Any(r => "equipment." + r.Id == id && r.ControlledPortalId == p.Id)))
                    throw new InvalidDataException("Invalid connected-world portal.");
                if (!Nonnegative(p.WallHeight) || p.WallHeight > p.ClearHeight || !Nonnegative(p.ClosureAlong) || p.ClosureAlong > 1 ||
                    !Nonnegative(p.ClosureBottom) || !Positive(p.ClosureHeight) || p.ClosureBottom + p.ClosureHeight > p.ClearHeight)
                    throw new InvalidDataException("Invalid passage enclosure or closure profile.");
                var dx = p.ToPoint.X - p.FromPoint.X; var dz = p.ToPoint.Z - p.FromPoint.Z;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance < 1 || Math.Abs(p.ToPoint.Y - p.FromPoint.Y) / distance > .6)
                    throw new InvalidDataException("Portal exceeds the reviewed synthetic walking slope.");
            }
            if (Portals.Select(p => p.Id).Distinct().Count() != Portals.Length ||
                Regions.Any(r => Route(StartRegionId, r.Id).Length == 0))
                throw new InvalidDataException("Connected world must be the complete coverage tree.");
        }
        private static bool Positive(float f) => f > 0 && !float.IsInfinity(f);
        private static bool Nonnegative(float f) => f >= 0 && !float.IsInfinity(f);
    }
}
