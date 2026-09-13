using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;

namespace ChooGuard.Foundation.Simulation
{
    [Serializable] public sealed class ConnectedThermalProfile
    {
        public double MaximumCellLengthM = 10, MaximumRampRisePerCellM = .5;
        public double MaximumSolverStepSeconds = .01;
        public double WallArealHeatCapacityJPerM2K = 20000, InsideHeatTransferWPerM2K = 5, OutsideHeatTransferWPerM2K = 3;
        // Total closed-envelope leakage width per region, distributed across virtual cells.
        public double VentilationMassKgPerSecondPerM3 = .0005, ClosedEnvelopeLeakWidthM = .005;
    }
    [Serializable] public sealed class ThermalCellBinding
    {
        public string CellId, RegionId, FrameId, PortalId = "";
        public Point3 LocalFloorCenter, Size;
        public float LocalYawDegrees;
    }
    [Serializable] public sealed class ThermalDoorBinding
    { public string DoorId, PortalId = ""; public bool Controlled, BoardingCoupling; public Point3 PlanePoint; }
    [Serializable] public sealed class ThermalFanBinding
    { public string FanId, RegionId; }

    /// <summary>Conservative gas network over authored spaces. Ramp cells are stepped horizontal prisms;
    /// outside regions use an ambient reservoir. This is not a surveyed CFD or train piston model.</summary>
    public sealed class ConnectedThermalDomain
    {
        private SpatialFrame stationaryFrame;
        public FireNetworkDefinition Network { get; private set; }
        public FireSolverOptions SolverOptions { get; private set; }
        public ThermalCellBinding[] Bindings { get; private set; }
        public ThermalDoorBinding[] DoorBindings { get; private set; }
        public ThermalFanBinding[] FanBindings { get; private set; }

        public static ConnectedThermalDomain Create(ConnectedWorldDefinition world, ConnectedThermalProfile profile)
        {
            world.Validate();
            if (profile == null || !Positive(profile.MaximumCellLengthM) || profile.MaximumCellLengthM < 1 ||
                !Positive(profile.MaximumRampRisePerCellM) || profile.MaximumRampRisePerCellM > 1 ||
                !Positive(profile.MaximumSolverStepSeconds) || profile.MaximumSolverStepSeconds > .05 || profile.MaximumSolverStepSeconds < .0001 ||
                !Positive(profile.WallArealHeatCapacityJPerM2K) || !Nonnegative(profile.InsideHeatTransferWPerM2K) ||
                !Nonnegative(profile.OutsideHeatTransferWPerM2K) || !Nonnegative(profile.VentilationMassKgPerSecondPerM3) ||
                !Positive(profile.ClosedEnvelopeLeakWidthM)) throw new ArgumentException("Invalid connected thermal profile.");
            var cells = new List<FireCellDefinition>(); var bindings = new List<ThermalCellBinding>();
            var doors = new List<FireDoorDefinition>(); var doorBindings = new List<ThermalDoorBinding>();
            var fans = new List<FireFanDefinition>(); var fanBindings = new List<ThermalFanBinding>();
            var roomCells = new Dictionary<string, FireCellDefinition[]>();
            var roomBounds = new Dictionary<string, (double left, double right)>();
            void Door(string id, FireCellDefinition a, FireCellDefinition b, double width, string portal = "", bool controlled = false,
                bool boarding = false, Point3 plane = default, double? requestedBottom = null, double? requestedTop = null)
            {
                var bottom = b == null ? a.FloorElevationM : Math.Max(a.FloorElevationM, b.FloorElevationM);
                var top = b == null ? a.FloorElevationM + a.HeightM : Math.Min(a.FloorElevationM + a.HeightM, b.FloorElevationM + b.HeightM);
                if (requestedBottom.HasValue) bottom = Math.Max(bottom, requestedBottom.Value);
                if (requestedTop.HasValue) top = Math.Min(top, requestedTop.Value);
                if (top <= bottom || width <= 0) return;
                doors.Add(new FireDoorDefinition { Id = id, FromCellId = a.Id, ToCellId = b?.Id ?? "", WidthM = width,
                    BottomElevationM = bottom, HeightM = top - bottom });
                doorBindings.Add(new ThermalDoorBinding { DoorId = id, PortalId = portal, Controlled = controlled,
                    BoardingCoupling = boarding, PlanePoint = plane });
            }
            FireCellDefinition Cell(string id, double width, double depth, double height, double floor, double perimeter,
                double wallHeight, bool ceiling, Point3 center, string region, string frame, string portal = "", float yaw = 0,
                FireWallOpening[] apertures = null, double surfaceAreaScale = 1, double floorRise = 0)
            {
                var c = new FireCellDefinition { Id = id, WidthM = width, DepthM = depth, HeightM = height, FloorElevationM = floor,
                    FloorRiseM = floorRise,
                    UseExplicitWallAreas = true, FloorWallAreaM2 = width * depth * surfaceAreaScale, CeilingWallAreaM2 = ceiling ? width * depth * surfaceAreaScale : 0,
                    VerticalWallLengthM = wallHeight > 0 ? perimeter : 0, VerticalWallHeightM = wallHeight,
                    WallOpenings = wallHeight > 0 ? apertures ?? Array.Empty<FireWallOpening>() : Array.Empty<FireWallOpening>(),
                    InsideHeatTransferWPerM2K = profile.InsideHeatTransferWPerM2K, OutsideHeatTransferWPerM2K = profile.OutsideHeatTransferWPerM2K };
                c.WallHeatCapacityJPerK = c.SurfaceAreaM2 * profile.WallArealHeatCapacityJPerM2K;
                cells.Add(c); bindings.Add(new ThermalCellBinding { CellId = id, RegionId = region, FrameId = frame, PortalId = portal,
                    LocalFloorCenter = world.Frame(frame).ToLocal(center), Size = new Point3((float)width, (float)height, (float)depth), LocalYawDegrees = yaw });
                return c;
            }
            void AmbientSideGaps(FireCellDefinition cell, double perimeter, double wallHeight, string prefix, FireWallOpening[] apertures = null)
            {
                var clearance = cell.HeightM - cell.FloorRiseM;
                if (wallHeight >= clearance) return;
                apertures ??= Array.Empty<FireWallOpening>();
                var cuts = new[] { wallHeight, clearance }.Concat(apertures.SelectMany(o => new[] { o.BottomM, o.BottomM + o.HeightM }))
                    .Where(h => h >= wallHeight && h <= clearance).Distinct().OrderBy(h => h).ToArray();
                for (var i = 0; i + 1 < cuts.Length; i++)
                {
                    var middle = (cuts[i] + cuts[i + 1]) / 2;
                    var exposed = perimeter - apertures.Where(o => middle > o.BottomM && middle < o.BottomM + o.HeightM).Sum(o => o.WidthM);
                    // Open ramp side slots retain their exact area at the median floor elevation;
                    // lateral/wind-resolved flow is outside this reduced model.
                    Door(prefix + "-" + i, cell, null, Math.Max(0, exposed), requestedBottom: cell.FloorElevationM + cell.FloorRiseM / 2 + cuts[i],
                        requestedTop: cell.FloorElevationM + cell.FloorRiseM / 2 + cuts[i + 1]);
                }
            }
            foreach (var r in world.Regions)
            {
                var axisCuts = RoomCuts(world, r, profile.MaximumCellLengthM); var count = axisCuts.Length - 1;
                var room = new FireCellDefinition[count]; roomCells.Add(r.Id, room);
                for (var i = 0; i < count; i++)
                {
                    var width = axisCuts[i + 1] - axisCuts[i]; var x = (axisCuts[i] + axisCuts[i + 1]) / 2;
                    var perimeter = 2 * width + (i == 0 ? r.SizeZ : 0) + (i == count - 1 ? r.SizeZ : 0);
                    var apertures = new List<FireWallOpening>();
                    foreach (var portal in world.Portals.Where(p => p.From == r.Id || p.To == r.Id))
                    {
                        var point = portal.From == r.Id ? portal.FromPoint : portal.ToPoint;
                        double apertureWidth;
                        if (Math.Abs(Math.Abs(point.X - r.Center.X) - r.SizeX / 2) < .05)
                            apertureWidth = (point.X < r.Center.X ? i == 0 : i == count - 1) ? portal.ClearWidth : 0;
                        else apertureWidth = Math.Max(0, Math.Min(x + width / 2, point.X + portal.ClearWidth / 2.0) - Math.Max(x - width / 2, point.X - portal.ClearWidth / 2.0));
                        if (apertureWidth > 0) apertures.Add(new FireWallOpening { Id = portal.Id, WidthM = apertureWidth, HeightM = Math.Min(r.Height, portal.ClearHeight) });
                    }
                    room[i] = Cell("cell-" + r.Id + "-" + i, width, r.SizeZ, r.Height, r.Center.Y, perimeter, r.WallHeight, r.Ceiling,
                        new Point3((float)x, r.Center.Y, r.Center.Z), r.Id, r.FrameId, apertures: apertures.ToArray());
                    roomBounds.Add(room[i].Id, (axisCuts[i], axisCuts[i + 1]));
                    Door("leak-" + r.Id + "-" + i, room[i], null, profile.ClosedEnvelopeLeakWidthM * width / r.SizeX);
                    AmbientSideGaps(room[i], perimeter, r.WallHeight, "ambient-" + r.Id + "-" + i, apertures.ToArray());
                    var flow = room[i].VolumeM3 * profile.VentilationMassKgPerSecondPerM3;
                    var intake = "intake-" + r.Id + "-" + i; var exhaust = "exhaust-" + r.Id + "-" + i;
                    fans.Add(new FireFanDefinition { Id = intake, ToCellId = room[i].Id, MassFlowKgPerSecond = flow });
                    fans.Add(new FireFanDefinition { Id = exhaust, FromCellId = room[i].Id, FromUpper = true, MassFlowKgPerSecond = flow });
                    fanBindings.Add(new ThermalFanBinding { FanId = intake, RegionId = r.Id }); fanBindings.Add(new ThermalFanBinding { FanId = exhaust, RegionId = r.Id });
                    if (i > 0) Door("internal-" + r.Id + "-" + i, room[i - 1], room[i], r.SizeZ);
                }
            }
            FireCellDefinition Endpoint(string region, Point3 point)
            {
                // Public geometry is stored as floats; use the same centimetre edge tolerance as Region.Contains.
                return roomCells[region].First(c => point.X >= roomBounds[c.Id].left - .01 && point.X <= roomBounds[c.Id].right + .01);
            }
            foreach (var p in world.Portals)
            {
                var dx = p.ToPoint.X - p.FromPoint.X; var dz = p.ToPoint.Z - p.FromPoint.Z;
                var dy = p.ToPoint.Y - p.FromPoint.Y; var horizontal = Math.Sqrt((double)dx * dx + (double)dz * dz);
                var count = Math.Max(1, Math.Max((int)Math.Ceiling(horizontal / profile.MaximumCellLengthM), (int)Math.Ceiling(Math.Abs(dy) / profile.MaximumRampRisePerCellM)));
                var fractions = Enumerable.Range(0, count + 1).Select(i => (double)i / count).ToList();
                if (p.LinkedDoorEntityIds.Length > 0) fractions.Add(p.ClosureAlong);
                var cuts = fractions.Distinct().OrderBy(x => x).ToArray();
                var previous = Endpoint(p.From, p.FromPoint);
                var owner = p.StaticBoarding ? (world.Region(p.From).FrameId == "world" ? p.From : p.To) : p.From;
                for (var i = 0; i < cuts.Length; i++)
                {
                    FireCellDefinition next;
                    if (i == cuts.Length - 1) next = Endpoint(p.To, p.ToPoint);
                    else
                    {
                        var t = (cuts[i] + cuts[i + 1]) / 2; var width = horizontal * (cuts[i + 1] - cuts[i]);
                        var floorA = p.FromPoint.Y + dy * cuts[i]; var floorB = p.FromPoint.Y + dy * cuts[i + 1]; var rise = Math.Abs(floorB - floorA);
                        var center = new Point3((float)(p.FromPoint.X + dx * t), (float)Math.Min(floorA, floorB), (float)(p.FromPoint.Z + dz * t));
                        next = Cell("passage-" + p.Id + "-" + i, width, p.ClearWidth, p.ClearHeight + rise, Math.Min(floorA, floorB), 2 * width,
                            p.WallHeight, p.Ceiling, center, owner, "world", p.Id, (float)(Math.Atan2(-dz, dx) * 180 / Math.PI),
                            surfaceAreaScale: Math.Sqrt(horizontal * horizontal + dy * dy) / horizontal, floorRise: rise);
                        AmbientSideGaps(next, 2 * width, p.WallHeight, "ambient-passage-" + p.Id + "-" + i);
                    }
                    var plane = new Point3((float)(p.FromPoint.X + dx * cuts[i]), (float)(p.FromPoint.Y + dy * cuts[i]), (float)(p.FromPoint.Z + dz * cuts[i]));
                    var boarding = p.StaticBoarding && i == (world.Region(p.From).FrameId == "world" ? cuts.Length - 1 : 0);
                    var id = "link-" + p.Id + "-" + i;
                    var openingBottom = p.FromPoint.Y + dy * cuts[i]; var openingTop = openingBottom + p.ClearHeight;
                    if (p.LinkedDoorEntityIds.Length > 0 && Math.Abs(cuts[i] - p.ClosureAlong) < 1e-9)
                    {
                        var sill = plane.Y + p.ClosureBottom; var lintel = sill + p.ClosureHeight;
                        Door(id, previous, next, p.ClearWidth, p.Id, true, boarding, plane, Math.Max(openingBottom, sill), Math.Min(openingTop, lintel));
                        Door(id + "-below", previous, next, p.ClearWidth, p.Id, false, boarding, plane, openingBottom, Math.Min(openingTop, sill));
                        Door(id + "-above", previous, next, p.ClearWidth, p.Id, false, boarding, plane, Math.Max(openingBottom, lintel), openingTop);
                    }
                    else Door(id, previous, next, p.ClearWidth, p.Id, false, boarding, plane, openingBottom, openingTop);
                    previous = next;
                }
            }
            var network = new FireNetworkDefinition { Cells = cells.ToArray(), Doors = doors.ToArray(), Fans = fans.ToArray() };
            new ZoneFireModel(network); // Reject over-budget domains and invalid vertical openings at authoring/startup.
            return new ConnectedThermalDomain { stationaryFrame = world.Frame("world").Copy(), Network = network,
                SolverOptions = new FireSolverOptions { MaxStepSeconds = profile.MaximumSolverStepSeconds },
                Bindings = bindings.ToArray(), DoorBindings = doorBindings.ToArray(), FanBindings = fanBindings.ToArray() };
        }

        public FireForcing Forcing(FireSourcePower[] sources, Func<string, bool> portalOpen, Func<string, bool> boardingAligned, Func<string, bool> fanEnabled)
        {
            if (sources == null || portalOpen == null || boardingAligned == null || fanEnabled == null) throw new ArgumentException("Missing thermal forcing adapter.");
            return new FireForcing { Sources = sources,
                Doors = DoorBindings.Select(b => new FireDoorSetting { DoorId = b.DoorId,
                    OpeningFraction = (!b.Controlled || portalOpen(b.PortalId)) && (!b.BoardingCoupling || boardingAligned(b.PortalId)) ? 1 : 0 }).ToArray(),
                Fans = FanBindings.Select(b => new FireFanSetting { FanId = b.FanId, Enabled = fanEnabled(b.RegionId) }).ToArray() };
        }
        private static double[] RoomCuts(ConnectedWorldDefinition world, ConnectedRegionDefinition room, double maximum)
        {
            var minimum = room.Center.X - room.SizeX / 2.0; var end = room.Center.X + room.SizeX / 2.0;
            var openings = world.Portals.Where(p => p.From == room.Id || p.To == room.Id).Select(p => new {
                Point = p.From == room.Id ? p.FromPoint : p.ToPoint, Width = p.ClearWidth })
                .Where(p => Math.Abs(Math.Abs(p.Point.X - room.Center.X) - room.SizeX / 2) >= .05)
                .Select(p => (left: Math.Max(minimum, p.Point.X - p.Width / 2.0), right: Math.Min(end, p.Point.X + p.Width / 2.0)))
                .OrderBy(p => p.left).ToArray();
            var merged = new List<(double left, double right)>();
            foreach (var opening in openings)
            {
                if (merged.Count > 0 && opening.left <= merged[merged.Count - 1].right)
                { var previous = merged[merged.Count - 1]; merged[merged.Count - 1] = (previous.left, Math.Max(previous.right, opening.right)); }
                else merged.Add(opening);
            }
            if (merged.Any(o => o.right - o.left > maximum + 1e-6))
                throw new ArgumentException("Requested axial cells would bisect a doorway; keep its aperture in one cell or provide a multidimensional domain.");
            var anchors = new[] { minimum, end }.Concat(merged.SelectMany(o => new[] { o.left, o.right })).Distinct().OrderBy(x => x).ToArray();
            var cuts = new List<double> { minimum };
            for (var i = 0; i + 1 < anchors.Length; i++)
            {
                var middle = (anchors[i] + anchors[i + 1]) / 2;
                var count = merged.Any(o => middle > o.left && middle < o.right) ? 1 : (int)Math.Ceiling((anchors[i + 1] - anchors[i]) / maximum);
                for (var j = 1; j <= count; j++) cuts.Add(anchors[i] + (anchors[i + 1] - anchors[i]) * j / count);
            }
            return cuts.ToArray();
        }
        public string CellAt(string regionId, Point3 localPosition) => FindCell(Bindings.Where(b => b.RegionId == regionId && b.PortalId == ""), _ => localPosition);
        public string CellAt(SpatialPose pose)
        {
            if (pose == null || !pose.Position.Finite || !pose.LocalPosition.Finite) throw new ArgumentException("Invalid thermal lookup pose.");
            var candidates = string.IsNullOrEmpty(pose.PortalId) ? Bindings.Where(b => b.RegionId == pose.RegionId && b.PortalId == "") :
                Bindings.Where(b => b.PortalId == pose.PortalId);
            return FindCell(candidates, b => b.FrameId == pose.FrameId ? pose.LocalPosition : b.FrameId == "world" ? stationaryFrame.ToLocal(pose.Position) :
                throw new ArgumentException("Thermal lookup is outside its frame binding."));
        }
        private static string FindCell(IEnumerable<ThermalCellBinding> candidates, Func<ThermalCellBinding, Point3> coordinate)
        {
            foreach (var b in candidates)
            {
                var point = coordinate(b); var x = point.X - b.LocalFloorCenter.X; var z = point.Z - b.LocalFloorCenter.Z;
                var a = b.LocalYawDegrees * Math.PI / 180; var localX = Math.Cos(a) * x - Math.Sin(a) * z; var localZ = Math.Sin(a) * x + Math.Cos(a) * z;
                // Foot poses select the horizontal prism; ramp cell floor elevation is a declared stepped approximation.
                if (Math.Abs(localX) <= b.Size.X / 2 + .001 && Math.Abs(localZ) <= b.Size.Z / 2 + .001) return b.CellId;
            }
            throw new ArgumentException("Pose is outside the represented thermal cells.");
        }
        private static bool Positive(double value) => Nonnegative(value) && value > 0;
        private static bool Nonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
