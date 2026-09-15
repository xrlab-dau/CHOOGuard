using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Straight, level rigid train frames over the authored connected geometry. Fixed boarding bridges belong to the station.</summary>
    public sealed class SimulatedSpatialWorld
    {
        public const float BoardingClearanceM = .035f;
        private readonly ConnectedWorldDefinition authored;
        public SimulatedSpatialWorld(ConnectedWorldDefinition authored)
        {
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            this.authored = authored.Copy();
            this.authored.Validate();
            if (this.authored.Portals.Any(p => this.authored.Region(p.From).FrameId != this.authored.Region(p.To).FrameId &&
                (!p.StaticBoarding || (this.authored.Region(p.From).FrameId != "world" && this.authored.Region(p.To).FrameId != "world"))))
                throw new ArgumentException("Cross-frame portals require a stationary-world boarding boundary.");
        }

        public ConnectedWorldDefinition At(SpatialFrame[] frames)
        {
            if (frames == null || frames.Any(f => f == null || !f.Origin.Finite) ||
                !frames.Select(f => f.FrameId).OrderBy(x => x).SequenceEqual(authored.Frames.Select(f => f.FrameId).OrderBy(x => x)) ||
                frames.Any(f => f.YawDegrees != authored.Frame(f.FrameId).YawDegrees || f.Origin.Y != authored.Frame(f.FrameId).Origin.Y) ||
                frames.Single(f => f.FrameId == "world").Origin.DistanceSquared(authored.Frame("world").Origin) != 0)
                throw new ArgumentException("Physical frames must preserve the fixed world and level straight-route orientation.");
            Point3 Move(Point3 p, string frame) => frames.Single(f => f.FrameId == frame).ToWorld(authored.Frame(frame).ToLocal(p));
            return new ConnectedWorldDefinition { SchemaVersion = authored.SchemaVersion, GeometrySchemaVersion = authored.GeometrySchemaVersion, ProfileId = authored.ProfileId,
                StartRegionId = authored.StartRegionId, Classification = authored.Classification, Limits = authored.Limits?.ToArray() ?? Array.Empty<string>(),
                Frames = frames.Select(f => f.Copy()).ToArray(), Portals = authored.Portals.Select(p => p.Copy()).ToArray(),
                Regions = authored.Regions.Select(r => new ConnectedRegionDefinition { Id = r.Id, Label = r.Label, FrameId = r.FrameId,
                    SceneName = r.SceneName, Template = r.Template, EquipmentAsset = r.EquipmentAsset, EquipmentLabel = r.EquipmentLabel,
                    ControlledPortalId = r.ControlledPortalId, GeometryStatus = r.GeometryStatus, SizeX = r.SizeX, SizeZ = r.SizeZ, Height = r.Height,
                    WallHeight = r.WallHeight, Ceiling = r.Ceiling,
                    Center = Move(r.Center, r.FrameId), Hub = Move(r.Hub, r.FrameId), EquipmentPosition = Move(r.EquipmentPosition, r.FrameId),
                    Exclusions = r.Exclusions.Select(e => new RegionExclusion { Center = Move(e.Center, r.FrameId), SizeX = e.SizeX, SizeZ = e.SizeZ }).ToArray() }).ToArray() };
        }

        public bool TryLocate(ConnectedWorldDefinition current, SpatialPose previous, Point3 point, float radius,
            ISet<string> closed, out SpatialPose result)
        {
            result = null;
            if (current == null || previous == null || !point.Finite || float.IsNaN(radius) ||
                float.IsInfinity(radius) || radius <= 0 || !current.Regions.Any(r => r.Id == previous.RegionId)) return false;
            var region = current.Region(previous.RegionId);
            foreach (var portal in current.Portals.Where(p => p.StaticBoarding && (p.From == region.Id || p.To == region.Id)))
            {
                var train = current.Region(current.Region(portal.From).FrameId == "world" ? portal.To : portal.From);
                var station = train.Id == portal.From ? portal.To : portal.From;
                var threshold = train.Id == portal.From ? portal.FromPoint : portal.ToPoint;
                var outer = train.Id == portal.From ? portal.ToPoint : portal.FromPoint;
                var dx = threshold.X - outer.X; var dz = threshold.Z - outer.Z;
                var length = Math.Sqrt((double)dx * dx + (double)dz * dz); dx /= (float)length; dz /= (float)length;
                var inward = (point.X - threshold.X) * dx + (point.Z - threshold.Z) * dz;
                var across = Math.Abs((point.X - threshold.X) * dz - (point.Z - threshold.Z) * dx);
                var isClosed = closed?.Contains(portal.Id) ?? false;
                var aligned = TrainAligned(current, portal, train.Id);
                var nearBoundary = inward >= -length - .01 && inward <= radius + BoardingClearanceM + .01 &&
                    across <= portal.ClearWidth / 2 - radius + .001 && Math.Abs(point.Y - threshold.Y) <= .3;
                if (previous.RegionId == train.Id)
                {
                    if (region.Contains(point) && !region.Exclusions.Any(e => e.Contains(point, radius)))
                    { result = current.Pose(train.Id, point, nearBoundary ? portal.Id : ""); return true; }
                    if (!nearBoundary || isClosed || !aligned) continue;
                    result = current.Pose(inward >= -radius - BoardingClearanceM ? train.Id : station, point, portal.Id);
                    return true;
                }
                var entering = train.Contains(point) && across <= portal.ClearWidth / 2 - radius + .001;
                if (!nearBoundary && !entering) continue;
                if (entering && (!aligned || isClosed)) return false;
                if (isClosed && portal.Contains(point, radius, out var along) && portal.Contains(previous.Position, 0, out var before) &&
                    (before < portal.ClosureAlong) != (along < portal.ClosureAlong)) return false;
                // Keep station coordinates while any of the footprint still spans the fixed bridge.
                result = inward > radius + BoardingClearanceM && aligned && !isClosed ? current.Pose(train.Id, point) : current.Pose(station, point, portal.Id);
                return true;
            }
            if (region.Contains(point) && !region.Exclusions.Any(e => e.Contains(point, radius)))
            { result = current.Pose(region.Id, point); return true; }
            // Ordinary ramps retain their historical midpoint region convention; every stationary region shares world frame.
            foreach (var portal in current.Portals.Where(p => !p.StaticBoarding && (p.From == region.Id || p.To == region.Id)))
            {
                if (!portal.Contains(point, radius, out var along)) continue;
                if (closed?.Contains(portal.Id) ?? false)
                {
                    if (!portal.Contains(previous.Position, 0, out var before)) before = region.Id == portal.From ? 0 : 1;
                    if ((before < portal.ClosureAlong) != (along < portal.ClosureAlong)) return false;
                }
                result = current.Pose(along < .5f ? portal.From : portal.To, point, portal.Id); return true;
            }
            return false;
        }

        private bool TrainAligned(ConnectedWorldDefinition current, ConnectedPortalDefinition portal, string train)
        {
            var frame = current.Frame(current.Region(train).FrameId); var original = authored.Frame(frame.FrameId);
            return frame.Origin.DistanceSquared(original.Origin) <= .0025 && frame.YawDegrees == original.YawDegrees;
        }

        public static bool OccupiesBoardingBridge(ConnectedPortalDefinition portal, Point3 feet, float radius, float height)
        {
            if (!portal.StaticBoarding || !feet.Finite || radius <= 0 || height <= 0) return false;
            var dx = portal.ToPoint.X - portal.FromPoint.X; var dz = portal.ToPoint.Z - portal.FromPoint.Z;
            var length = Math.Sqrt((double)dx * dx + (double)dz * dz);
            var x = feet.X - portal.FromPoint.X; var z = feet.Z - portal.FromPoint.Z;
            var along = (x * dx + z * dz) / length; var across = Math.Abs(x * dz - z * dx) / length;
            var floor = portal.FromPoint.Y + Math.Max(0, Math.Min(1, along / length)) * (portal.ToPoint.Y - portal.FromPoint.Y);
            return along >= -radius && along <= length + radius && across <= portal.ClearWidth / 2 + radius &&
                feet.Y < floor + portal.ClearHeight && feet.Y + height > floor;
        }
    }
}
