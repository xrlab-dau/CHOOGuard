using System;
using System.Collections.Generic;

namespace ChooGuard.Foundation.Simulation
{
    [Serializable]
    public struct DoubleVector3
    {
        public double X, Y, Z;
        public DoubleVector3(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    /// <summary>
    /// SI units. CenterY is the minimum floor elevation, not the room's vertical midpoint.
    /// Local X in world space is (cos(yaw),0,-sin(yaw)); positive gradient rises along local X.
    /// floor(x)=CenterY+FloorRiseM/2+SignedFloorGradientX*x; roof=floor+BoundingHeightM-FloorRiseM.
    /// The interface is horizontal at CenterY+InterfaceHeightAboveMinFloorM.
    /// Extinction coefficients are explicit inputs; no smoke-mass conversion policy lives here.
    /// </summary>
    [Serializable]
    public sealed class SmokeCell
    {
        public string Id;
        public double CenterX, CenterY, CenterZ, YawDegrees;
        public double WidthM, DepthM, BoundingHeightM, FloorRiseM, SignedFloorGradientX;
        public double InterfaceHeightAboveMinFloorM, UpperExtinctionPerM, LowerExtinctionPerM;
        public SmokeCell Copy() => (SmokeCell)MemberwiseClone();
    }

    public readonly struct OpticalDepthResult
    {
        public readonly double SegmentLengthM, LowerLengthM, UpperLengthM, OpticalDepth;
        public double InsideLengthM => LowerLengthM + UpperLengthM;
        internal OpticalDepthResult(double length, double lower, double upper, double depth)
        { SegmentLengthM = length; LowerLengthM = lower; UpperLengthM = upper; OpticalDepth = depth; }
    }

    /// <summary>
    /// Immutable prepared projection. Construction copies and validates every DTO, and caches rotations.
    /// The caller validates the world partition: positive-volume overlaps are summed, never merged here.
    /// X/Z min faces own coplanar rays; max faces do not. This also defines outer max-face rays as outside.
    /// Rebuild after changing a cell coefficient, layer height, yaw, or translated moving frame.
    /// </summary>
    public sealed class SmokeOpticalField
    {
        private readonly PreparedSmokeCell[] cells;
        public int CellCount => cells.Length;
        public SmokeOpticalField(SmokeCell[] definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            cells = new PreparedSmokeCell[definition.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < definition.Length; i++)
            {
                cells[i] = new PreparedSmokeCell(definition[i]);
                if (!ids.Add(definition[i].Id)) throw new ArgumentException("Duplicate smoke cell identifier.");
            }
        }

        public OpticalDepthResult Trace(DoubleVector3 from, DoubleVector3 to)
        {
            var segment = new OpticalSegment(from, to);
            var lower = new CompensatedSum(); var upper = new CompensatedSum(); var depth = new CompensatedSum();
            foreach (var cell in cells)
            {
                var result = cell.Trace(segment);
                lower.Add(result.LowerLengthM); upper.Add(result.UpperLengthM); depth.Add(result.OpticalDepth);
            }
            return new OpticalDepthResult(segment.Length, lower.Value, upper.Value, depth.Value);
        }
    }

    public static class SmokeOptics
    {
        /// <summary>One-cell convenience path. Use SmokeOpticalField for repeated rays.</summary>
        public static OpticalDepthResult TraceCell(SmokeCell cell, DoubleVector3 from, DoubleVector3 to)
        { return new PreparedSmokeCell(cell).Trace(new OpticalSegment(from, to)); }
    }

    internal struct CompensatedSum
    {
        private double sum, compensation;
        public double Value => sum;
        public void Add(double value)
        {
            var adjusted = value - compensation; var next = sum + adjusted;
            if (!OpticalMath.Finite(next)) throw new ArgumentException("Optical sum exceeds finite double range.");
            compensation = (next - sum) - adjusted; sum = next;
        }
    }

    internal readonly struct OpticalSegment
    {
        public readonly DoubleVector3 From, To;
        public readonly double Length;
        public OpticalSegment(DoubleVector3 from, DoubleVector3 to)
        {
            if (!OpticalMath.Finite(from.X) || !OpticalMath.Finite(from.Y) || !OpticalMath.Finite(from.Z) ||
                !OpticalMath.Finite(to.X) || !OpticalMath.Finite(to.Y) || !OpticalMath.Finite(to.Z))
                throw new ArgumentException("Smoke ray endpoints must be finite.");
            From = from; To = to;
            var x = to.X - from.X; var y = to.Y - from.Y; var z = to.Z - from.Z;
            var scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
            if (!OpticalMath.Finite(scale)) throw new ArgumentException("Smoke segment difference exceeds finite double range.");
            if (scale == 0) { Length = 0; return; }
            x /= scale; y /= scale; z /= scale;
            Length = scale * Math.Sqrt(x * x + y * y + z * z);
            if (!OpticalMath.Finite(Length)) throw new ArgumentException("Smoke segment length exceeds finite double range.");
        }
    }

    internal static class OpticalMath
    {
        internal const double RoundoffScale = 8 * 2.2204460492503131e-16;
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool Positive(double value) => Finite(value) && value > 0;
        internal static bool Nonnegative(double value) => Finite(value) && value >= 0;
        internal static double Roundoff(double a, double b) => RoundoffScale * Math.Abs(a) + RoundoffScale * Math.Abs(b);
    }

    internal readonly struct OpticalLocalPoint
    {
        public readonly double X, Y, Z, ErrorX, ErrorY, ErrorZ;
        public OpticalLocalPoint(DoubleVector3 point, double cx, double cy, double cz, double cosine, double sine)
        {
            var dx = point.X - cx; var dz = point.Z - cz;
            X = cosine * dx - sine * dz; Y = point.Y - cy; Z = sine * dx + cosine * dz;
            ErrorX = Math.Abs(cosine) * OpticalMath.Roundoff(point.X, cx) + Math.Abs(sine) * OpticalMath.Roundoff(point.Z, cz) +
                OpticalMath.Roundoff(cosine * dx, sine * dz);
            ErrorY = OpticalMath.Roundoff(point.Y, cy);
            ErrorZ = Math.Abs(sine) * OpticalMath.Roundoff(point.X, cx) + Math.Abs(cosine) * OpticalMath.Roundoff(point.Z, cz) +
                OpticalMath.Roundoff(sine * dx, cosine * dz);
            if (!OpticalMath.Finite(X) || !OpticalMath.Finite(Y) || !OpticalMath.Finite(Z) ||
                !OpticalMath.Finite(ErrorX) || !OpticalMath.Finite(ErrorY) || !OpticalMath.Finite(ErrorZ))
                throw new ArgumentException("Smoke local coordinates exceed finite double range.");
        }
    }

    internal sealed class PreparedSmokeCell
    {
        private readonly double cx, cy, cz, cosine, sine, halfWidth, halfDepth, height, interfaceHeight;
        private readonly double floorNx, floorNy, floorBound, roofBound, lowerExtinction, upperExtinction;
        internal PreparedSmokeCell(SmokeCell c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || !OpticalMath.Finite(c.CenterX) || !OpticalMath.Finite(c.CenterY) || !OpticalMath.Finite(c.CenterZ) ||
                !OpticalMath.Finite(c.YawDegrees) || !OpticalMath.Positive(c.WidthM) || !OpticalMath.Positive(c.DepthM) || !OpticalMath.Positive(c.BoundingHeightM) ||
                !OpticalMath.Nonnegative(c.FloorRiseM) || c.FloorRiseM > c.BoundingHeightM / 2 || !OpticalMath.Finite(c.SignedFloorGradientX) ||
                !OpticalMath.Nonnegative(c.InterfaceHeightAboveMinFloorM) || c.InterfaceHeightAboveMinFloorM > c.BoundingHeightM ||
                !OpticalMath.Nonnegative(c.LowerExtinctionPerM) || !OpticalMath.Nonnegative(c.UpperExtinctionPerM))
                throw new ArgumentException("Invalid smoke cell geometry or extinction coefficient.");
            var impliedRise = Math.Abs(c.SignedFloorGradientX) * c.WidthM;
            if (!OpticalMath.Finite(impliedRise) || c.FloorRiseM == 0 && c.SignedFloorGradientX != 0 ||
                c.FloorRiseM > 0 && (impliedRise == 0 || Math.Abs(impliedRise - c.FloorRiseM) > 1e-10 * Math.Max(impliedRise, c.FloorRiseM)))
                throw new ArgumentException("Smoke floor gradient and elevation range disagree.");
            cx = c.CenterX; cy = c.CenterY; cz = c.CenterZ;
            halfWidth = c.WidthM / 2; halfDepth = c.DepthM / 2; height = c.BoundingHeightM;
            if (halfWidth == 0 || halfDepth == 0) throw new ArgumentException("Smoke cell half extents underflow double precision.");
            interfaceHeight = c.InterfaceHeightAboveMinFloorM; lowerExtinction = c.LowerExtinctionPerM; upperExtinction = c.UpperExtinctionPerM;
            var yaw = c.YawDegrees % 360; if (yaw < 0) yaw += 360;
            // Exact cardinal axes avoid introducing an artificial component at a right-angle rotation.
            if (yaw == 0) { cosine = 1; sine = 0; }
            else if (yaw == 90) { cosine = 0; sine = 1; }
            else if (yaw == 180) { cosine = -1; sine = 0; }
            else if (yaw == 270) { cosine = 0; sine = -1; }
            else { var radians = yaw * Math.PI / 180; cosine = Math.Cos(radians); sine = Math.Sin(radians); }
            // Normalize the sloped halfspaces without squaring an arbitrarily large gradient.
            var scale = Math.Max(1, Math.Abs(c.SignedFloorGradientX));
            var nx = c.SignedFloorGradientX / scale; var ny = -1 / scale; var norm = Math.Sqrt(nx * nx + ny * ny);
            floorNx = nx / norm; floorNy = ny / norm;
            floorBound = -(c.FloorRiseM / 2 / scale) / norm;
            roofBound = ((height - c.FloorRiseM / 2) / scale) / norm;
        }

        internal OpticalDepthResult Trace(OpticalSegment segment)
        {
            if (segment.Length == 0) return new OpticalDepthResult(0, 0, 0, 0);
            var a = new OpticalLocalPoint(segment.From, cx, cy, cz, cosine, sine);
            var b = new OpticalLocalPoint(segment.To, cx, cy, cz, cosine, sine);
            double entry = 0, exit = segment.Length;
            if (!Clip(a, b, 1, 0, 0, halfWidth, segment.Length, ref entry, ref exit, true) ||
                !Clip(a, b, -1, 0, 0, halfWidth, segment.Length, ref entry, ref exit) ||
                !Clip(a, b, 0, 0, 1, halfDepth, segment.Length, ref entry, ref exit, true) ||
                !Clip(a, b, 0, 0, -1, halfDepth, segment.Length, ref entry, ref exit) ||
                !Clip(a, b, floorNx, floorNy, 0, floorBound, segment.Length, ref entry, ref exit) ||
                !Clip(a, b, -floorNx, -floorNy, 0, roofBound, segment.Length, ref entry, ref exit) || exit <= entry)
                return new OpticalDepthResult(segment.Length, 0, 0, 0);
            var lower = 0.0; var upper = 0.0;
            if (interfaceHeight == 0) upper = exit - entry;
            else if (interfaceHeight == height) lower = exit - entry;
            else if (Excess(a, 0, 1, 0, interfaceHeight) == 0 && Excess(b, 0, 1, 0, interfaceHeight) == 0)
                upper = exit - entry; // One owner for a segment lying in the shared horizontal interface.
            else
            {
                var lo = entry; var hi = exit;
                if (Clip(a, b, 0, 1, 0, interfaceHeight, segment.Length, ref lo, ref hi)) lower = Math.Max(0, hi - lo);
                lo = entry; hi = exit;
                if (Clip(a, b, 0, -1, 0, -interfaceHeight, segment.Length, ref lo, ref hi)) upper = Math.Max(0, hi - lo);
            }
            var depth = lower * lowerExtinction + upper * upperExtinction;
            if (!OpticalMath.Finite(depth)) throw new ArgumentException("Optical depth exceeds finite double range.");
            return new OpticalDepthResult(segment.Length, lower, upper, depth);
        }

        // A halfspace is n dot localPoint <= bound. Clipped coordinates are distances in [0,fullLength].
        // Endpoint signs avoid an arbitrary near-parallel direction epsilon; no ray marching is used.
        private static bool Clip(OpticalLocalPoint a, OpticalLocalPoint b, double nx, double ny, double nz, double bound,
            double fullLength, ref double entry, ref double exit, bool excludeCoplanar = false)
        {
            var start = Excess(a, nx, ny, nz, bound); var end = Excess(b, nx, ny, nz, bound);
            if (excludeCoplanar && start == 0 && end == 0) return false;
            if (start <= 0 && end <= 0) return entry <= exit;
            if (start > 0 && end > 0) return false;
            // Opposite signs: scaled absolute distances avoid overflow in start-end.
            var scale = Math.Max(Math.Abs(start), Math.Abs(end));
            var first = Math.Abs(start) / scale; var last = Math.Abs(end) / scale;
            var crossing = fullLength * (first / (first + last));
            if (start > 0) entry = Math.Max(entry, crossing); else exit = Math.Min(exit, crossing);
            return entry <= exit;
        }

        private static double Excess(OpticalLocalPoint p, double nx, double ny, double nz, double bound)
        {
            var x = nx * p.X; var y = ny * p.Y; var z = nz * p.Z;
            var value = x + y + z - bound;
            var tolerance = Math.Abs(nx) * p.ErrorX + Math.Abs(ny) * p.ErrorY + Math.Abs(nz) * p.ErrorZ +
                OpticalMath.Roundoff(x, y) + OpticalMath.Roundoff(z, bound);
            if (!OpticalMath.Finite(value) || !OpticalMath.Finite(tolerance)) throw new ArgumentException("Smoke plane distance exceeds finite double range.");
            return Math.Abs(value) <= tolerance ? 0 : value;
        }
    }
}
