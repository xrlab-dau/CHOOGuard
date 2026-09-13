using NUnit.Framework;
using System;
using ChooGuard.Foundation.Simulation;

public class SmokeOpticsTests
{
    private static int passed;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); }
    private static void Near(double actual, double expected, string name, double tolerance = 2e-11)
    {
        if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance * Math.Max(Math.Abs(expected), 1e-100))
            throw new Exception(name + ": actual=" + actual.ToString("R") + " expected=" + expected.ToString("R"));
    }
    private static void Reject(Action action, string name)
    { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection: " + name); }
    private static DoubleVector3 P(double x, double y, double z = 0) => new DoubleVector3(x, y, z);
    private static SmokeCell Flat() => new SmokeCell { Id = "flat", WidthM = 10, DepthM = 4, BoundingHeightM = 4,
        InterfaceHeightAboveMinFloorM = 2, LowerExtinctionPerM = 2, UpperExtinctionPerM = 3 };
    private static SmokeCell Ramp(double sign = 1)
    { var cell = Flat(); cell.Id = "ramp"; cell.BoundingHeightM = 6; cell.FloorRiseM = 2; cell.SignedFloorGradientX = .2 * sign; cell.InterfaceHeightAboveMinFloorM = 3; return cell; }
    private static DoubleVector3 World(SmokeCell cell, DoubleVector3 point)
    {
        var angle = cell.YawDegrees * Math.PI / 180; var c = Math.Cos(angle); var s = Math.Sin(angle);
        return P(cell.CenterX + c * point.X + s * point.Z, cell.CenterY + point.Y, cell.CenterZ - s * point.X + c * point.Z);
    }
    private static OpticalDepthResult Trace(SmokeCell cell, DoubleVector3 a, DoubleVector3 b) => SmokeOptics.TraceCell(cell, a, b);
    private static void Lengths(OpticalDepthResult result, double lower, double upper, double depth, string name)
    { Near(result.LowerLengthM, lower, name + " lower"); Near(result.UpperLengthM, upper, name + " upper"); Near(result.OpticalDepth, depth, name + " depth"); }
    private static void Case(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }

    [Test] public void AnalyticGeometryCases()
    {
        Case("flat lower and upper horizontal chords", () => {
            Lengths(Trace(Flat(), P(-20, 1), P(20, 1)), 10, 0, 20, "lower");
            Lengths(Trace(Flat(), P(-20, 3), P(20, 3)), 0, 10, 30, "upper");
        });
        Case("vertical two-layer and oblique analytic chords", () => {
            Lengths(Trace(Flat(), P(0, -1), P(0, 5)), 2, 2, 10, "vertical");
            var length = Math.Sqrt(32);
            Lengths(Trace(Flat(), P(-2, 0), P(2, 4)), length / 2, length / 2, 2.5 * length, "diagonal");
        });
        Case("finite endpoints and zero segment", () => {
            Lengths(Trace(Flat(), P(-6, 1), P(0, 1)), 5, 0, 10, "finite entry");
            Lengths(Trace(Flat(), P(0, 1), P(.5, 1)), .5, 0, 1, "inside endpoints");
            Lengths(Trace(Flat(), P(0, 1), P(0, 1)), 0, 0, 0, "zero");
            Near(Trace(Flat(), P(-6, 1), P(0, 1)).SegmentLengthM, 6, "full finite distance");
        });
        Case("outside and point-only contacts", () => {
            foreach (var y in new[] { -1.0, 5.0 }) Lengths(Trace(Flat(), P(-20, y), P(20, y)), 0, 0, 0, "outside height");
            Lengths(Trace(Flat(), P(-6, 1), P(-5, 1)), 0, 0, 0, "point contact");
            Lengths(Trace(Flat(), P(-20, 1, 3), P(20, 1, 3)), 0, 0, 0, "outside depth");
        });
        Case("closed floor roof and single interface ownership", () => {
            Lengths(Trace(Flat(), P(-5, 0), P(5, 0)), 10, 0, 20, "floor");
            Lengths(Trace(Flat(), P(-5, 4), P(5, 4)), 0, 10, 30, "roof");
            Lengths(Trace(Flat(), P(-5, 2), P(5, 2)), 0, 10, 30, "interface upper owner");
        });
        Case("empty upper or lower layers", () => {
            var c = Flat(); c.InterfaceHeightAboveMinFloorM = 0;
            Lengths(Trace(c, P(-5, 0), P(5, 0)), 0, 10, 30, "empty lower");
            c.InterfaceHeightAboveMinFloorM = 4;
            Lengths(Trace(c, P(-5, 4), P(5, 4)), 10, 0, 20, "empty upper");
        });
        Case("positive and negative slope floor roof clipping", () => {
            foreach (var sign in new[] { -1.0, 1.0 }) {
                var c = Ramp(sign);
                Lengths(Trace(c, P(-10, 1), P(10, 1)), 5, 0, 10, "ramp floor clip");
                Lengths(Trace(c, P(-10, 5), P(10, 5)), 0, 5, 15, "ramp roof clip");
                Lengths(Trace(c, P(0, -1), P(0, 7)), 2, 2, 10, "median floor");
            }
        });
        Case("slope two-layer oblique chord and reversed ray", () => {
            foreach (var sign in new[] { -1.0, 1.0 }) {
                var c = Ramp(sign); c.InterfaceHeightAboveMinFloorM = 1.5;
                var a = P(-5, 1.5 - sign); var b = P(5, 1.5 + sign); var length = Math.Sqrt(104);
                Lengths(Trace(c, a, b), length / 2, length / 2, 2.5 * length, "ramp diagonal");
                Lengths(Trace(c, b, a), length / 2, length / 2, 2.5 * length, "ramp reversed");
            }
        });
        Case("slope cross-section at fixed x", () => {
            Lengths(Trace(Ramp(), P(-4, -1), P(-4, 7)), 2.8, 1.2, 9.2, "low side");
            Lengths(Trace(Ramp(), P(4, -1), P(4, 7)), 1.2, 2.8, 10.8, "high side");
            var c = Ramp(); c.InterfaceHeightAboveMinFloorM = .5;
            Lengths(Trace(c, P(4, -1), P(4, 7)), 0, 4, 12, "locally empty lower");
            c.InterfaceHeightAboveMinFloorM = 5.5;
            Lengths(Trace(c, P(-4, -1), P(-4, 7)), 4, 0, 8, "locally empty upper");
        });
        Case("Unity yaw and translated moving-frame covariance", () => {
            foreach (var yaw in new[] { 0.0, 90.0, -90.0, 37.0, 123.0, 450.0 }) {
                var c = Ramp(); c.YawDegrees = yaw; c.CenterX = 1234; c.CenterY = -5.25; c.CenterZ = -987;
                c.InterfaceHeightAboveMinFloorM = 1.5;
                var a = World(c, P(-5, .5, -1)); var b = World(c, P(5, 2.5, 1)); var length = Math.Sqrt(108);
                Lengths(Trace(c, a, b), length / 2, length / 2, 2.5 * length, "rigid frame " + yaw);
                Lengths(Trace(c, b, a), length / 2, length / 2, 2.5 * length, "reverse rigid frame " + yaw);
            }
        });
        Case("translated coplanar interface retains upper ownership", () => {
            var c = Flat(); c.CenterY = 1000000; c.InterfaceHeightAboveMinFloorM = .3;
            Lengths(Trace(c, P(-5, c.CenterY + .3), P(5, c.CenterY + .3)), 0, 10, 30, "translated boundary");
        });
        Case("X and Z max faces exclude coplanar rays, min faces include", () => {
            Lengths(Trace(Flat(), P(5, 1, -2), P(5, 1, 2)), 0, 0, 0, "max X");
            Lengths(Trace(Flat(), P(-5, 1, -2), P(-5, 1, 2)), 4, 0, 8, "min X");
            Lengths(Trace(Flat(), P(-5, 1, 2), P(5, 1, 2)), 0, 0, 0, "max Z");
            Lengths(Trace(Flat(), P(-5, 1, -2), P(5, 1, -2)), 10, 0, 20, "min Z");
        });
        Case("same-yaw adjacent X and Z faces contribute once", () => {
            var a = Flat(); a.Id = "a"; a.WidthM = 2; a.DepthM = 2; a.CenterX = -1;
            var b = Flat(); b.Id = "b"; b.WidthM = 2; b.DepthM = 2; b.CenterX = 1; b.LowerExtinctionPerM = 3;
            Lengths(new SmokeOpticalField(new[] { a, b }).Trace(P(0, 1, -1), P(0, 1, 1)), 2, 0, 6, "shared X");
            a.CenterX = b.CenterX = 0; a.CenterZ = -1; b.CenterZ = 1;
            Lengths(new SmokeOpticalField(new[] { a, b }).Trace(P(-1, 1), P(1, 1)), 2, 0, 6, "shared Z");
        });
        Case("rotated translated adjacent face ownership", () => {
            foreach (var yaw in new[] { 90.0, 37.0, -57.0 }) {
                var frame = Flat(); frame.YawDegrees = yaw; frame.CenterX = 101; frame.CenterY = -4; frame.CenterZ = 77;
                var a = Flat(); a.Id = "a"; a.WidthM = a.DepthM = 2; a.YawDegrees = yaw;
                var b = Flat(); b.Id = "b"; b.WidthM = b.DepthM = 2; b.YawDegrees = yaw; b.LowerExtinctionPerM = 3;
                var ca = World(frame, P(-1, 0)); var cb = World(frame, P(1, 0));
                a.CenterX = ca.X; a.CenterY = ca.Y; a.CenterZ = ca.Z;
                b.CenterX = cb.X; b.CenterY = cb.Y; b.CenterZ = cb.Z;
                var from = World(frame, P(0, 1, -.9)); var to = World(frame, P(0, 1, .9));
                Lengths(new SmokeOpticalField(new[] { a, b }).Trace(from, to), 1.8, 0, 5.4, "rotated shared face " + yaw);
            }
        });
        Case("independent partitions sum optical depth and clean air is empty", () => {
            var a = Flat(); a.Id = "a"; a.CenterX = -5;
            var b = Flat(); b.Id = "b"; b.CenterX = 5; b.LowerExtinctionPerM = 3;
            Lengths(new SmokeOpticalField(new[] { a, b }).Trace(P(-15, 1), P(15, 1)), 20, 0, 50, "sum");
            Lengths(new SmokeOpticalField(new SmokeCell[0]).Trace(P(0, 0), P(3, 4)), 0, 0, 0, "clean air");
        });
        Case("prepared field copies caller definition", () => {
            var c = Flat(); var cells = new[] { c }; var field = new SmokeOpticalField(cells);
            c.WidthM = 1; c.LowerExtinctionPerM = 100; cells[0] = null;
            Lengths(field.Trace(P(-20, 1), P(20, 1)), 10, 0, 20, "immutable prepared field");
        });
        Case("double range and small geometry preserve intensity", () => {
            var c = Flat(); c.WidthM = 1e200; c.LowerExtinctionPerM = 2e-200;
            var result = Trace(c, P(-1e200, 1), P(1e200, 1));
            Near(result.SegmentLengthM, 2e200, "large stable norm"); Near(result.LowerLengthM, 1e200, "large chord"); Near(result.OpticalDepth, 2, "large scaled intensity");
            c = Flat(); c.WidthM = c.DepthM = 1e-9; c.BoundingHeightM = 2e-9; c.InterfaceHeightAboveMinFloorM = 1e-9; c.LowerExtinctionPerM = 2e9;
            Lengths(Trace(c, P(-1e-9, .5e-9), P(1e-9, .5e-9)), 1e-9, 0, 2, "tiny chord");
        });
        Case("geometry coefficients and identifiers reject malformed data", () => {
            Action<SmokeCell>[] mutations = {
                c => c.WidthM = -1, c => c.DepthM = 0, c => c.BoundingHeightM = 0,
                c => c.FloorRiseM = -1, c => c.SignedFloorGradientX = .1,
                c => { c.FloorRiseM = 3; c.SignedFloorGradientX = .3; },
                c => c.InterfaceHeightAboveMinFloorM = -1, c => c.InterfaceHeightAboveMinFloorM = 5,
                c => c.LowerExtinctionPerM = -1, c => c.UpperExtinctionPerM = double.NaN,
                c => c.LowerExtinctionPerM = double.PositiveInfinity, c => c.CenterX = double.NaN,
                c => c.CenterY = double.PositiveInfinity, c => c.CenterZ = double.NaN,
                c => c.YawDegrees = double.NaN, c => c.SignedFloorGradientX = double.NaN,
                c => c.FloorRiseM = double.PositiveInfinity, c => c.WidthM = double.PositiveInfinity,
                c => c.Id = null, c => c.Id = " "
            };
            foreach (var mutation in mutations) { var c = Flat(); mutation(c); Reject(() => new SmokeOpticalField(new[] { c }), "invalid cell"); }
            var ramp = Ramp(); ramp.SignedFloorGradientX = .21; Reject(() => new SmokeOpticalField(new[] { ramp }), "rise mismatch");
            Reject(() => new SmokeOpticalField(new[] { Flat(), Flat() }), "duplicate cell");
            Reject(() => new SmokeOpticalField(new SmokeCell[] { null }), "null cell");
        });
        Case("invalid rays and unrepresentable results reject explicitly", () => {
            Reject(() => Trace(Flat(), P(double.NaN, 1), P(0, 1)), "NaN ray");
            Reject(() => Trace(Flat(), P(0, 1), P(double.PositiveInfinity, 1)), "infinite ray");
            Reject(() => Trace(Flat(), P(-double.MaxValue, 1), P(double.MaxValue, 1)), "overflowed distance");
            var c = Flat(); c.LowerExtinctionPerM = double.MaxValue;
            Reject(() => Trace(c, P(-5, 1), P(5, 1)), "optical-depth overflow");
        });
        Console.WriteLine("smoke-optics-tests: " + passed + " analytic groups passed");
    }
}
