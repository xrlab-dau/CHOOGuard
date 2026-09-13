using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.Foundation.Multiplayer;

public class RuntimeMetricAccumulatorTests
{
    private static readonly DateTime StartUtc = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);
    private static readonly double[] Bounds = { 1, 2, 4, 8, 12, 16, 20, 25, 33.3, 40, 50, 66.7, 100, 150, 200, 250, 333, 500, 750, 1000, 2000 };
    private static int passed, failed;
    private static RuntimeMetricAccumulator New(string role = "server", long simulation = 0, long sent = 0, long received = 0) =>
        new RuntimeMetricAccumulator(role, 0, StartUtc, simulation, sent, received, true, true);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (OverflowException) { return; } throw new Exception("Expected rejection."); }
    private static void Equivalent(object a, object b)
    {
        if (a == null || b == null) { Require(a == b, "Null mismatch"); return; }
        var type = a.GetType(); Require(type == b.GetType(), "Type mismatch");
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) { Require(a.Equals(b), "Value mismatch"); return; }
        if (a is Array aa && b is Array bb)
        { Require(aa.Length == bb.Length, "Array length mismatch"); for (var i = 0; i < aa.Length; i++) Equivalent(aa.GetValue(i), bb.GetValue(i)); return; }
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Equivalent(field.GetValue(a), field.GetValue(b));
    }
    private static void Check(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.GetType().Name + " " + e.Message); }
    }

    private static void IntervalAggregates()
    {
        var metric = New(simulation: 100, sent: 10, received: 20);
        metric.Record(.25, 50, 14, 25, 4, 100, 1, 101, false, .02);
        metric.Record(.5, 1, 20, 35, 5, 99, 2, 105, true, .2);
        var row = metric.Close(1, StartUtc.AddSeconds(1));
        Require(row.Schema == 1 && row.Kind == "interval" && row.Role == "server", "Envelope");
        Require(row.DurationSeconds == 1 && row.TickCount == 2, "Duration/call count");
        Require(row.SentPayloadBytes == 10 && row.ReceivedPayloadBytes == 15, "Interval byte deltas");
        Require(row.SimulationTickStart == 100 && row.SimulationTickEnd == 105, "Simulation range differs from call count");
        Require(row.PausedAny && row.MaximumBacklogSeconds == .2, "Pause/backlog");
        Require(row.ConnectedClients == 5 && row.ConnectedClientsMin == 4 && row.ConnectedClientsMax == 5 && row.ConnectedClientsMean == 4.5, "Client population");
        Require(row.NpcCount == 99 && row.NpcCountMin == 99 && row.NpcCountMax == 100 && row.NpcCountMean == 99.5, "NPC population");
        Require(row.ActiveIncidents == 2 && row.ActiveIncidentsMin == 1 && row.ActiveIncidentsMax == 2 && row.ActiveIncidentsMean == 1.5, "Incident population");
        Require(row.TickMilliseconds.Counts[0] == 1 && row.TickMilliseconds.Counts[10] == 1, "Inclusive buckets");
        Require(row.Batch && row.NullGraphics, "Runtime mode flags");
    }
    private static void EveryHistogramBoundary()
    {
        var metric = New(); long calls = 0;
        Action<double> record = value => { calls++; metric.Record(calls * .01, value, calls, calls, 20, 100, 2, calls, false, 0); };
        record(0);
        foreach (var boundary in Bounds) { record(boundary); record(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(boundary) + 1)); }
        var row = metric.Close(1, StartUtc.AddSeconds(1));
        Equivalent(row.TickMilliseconds.Bounds, Bounds);
        Require(row.TickMilliseconds.Counts.Length == Bounds.Length + 1 && row.TickCount == 43, "Histogram shape");
        long sum = 0; for (var i = 0; i < row.TickMilliseconds.Counts.Length; i++) { var expected = i == Bounds.Length ? 1 : 2; Require(row.TickMilliseconds.Counts[i] == expected, "Boundary bin " + i); sum += row.TickMilliseconds.Counts[i]; }
        Require(sum == row.TickCount, "Histogram total");
    }
    private static void CounterAndTickCarryAcrossClose()
    {
        var metric = New(simulation: 50, sent: 100, received: 200);
        metric.Record(.5, 10, 150, 225, 20, 100, 2, 60, true, .25);
        metric.Close(1, StartUtc.AddSeconds(1));
        metric.Record(1.5, 20, 180, 260, 18, 90, 1, 65, false, .1);
        var row = metric.Close(2, StartUtc.AddSeconds(2));
        Require(row.DurationSeconds == 1 && row.SentPayloadBytes == 30 && row.ReceivedPayloadBytes == 35, "Carry deltas");
        Require(row.SimulationTickStart == 60 && row.SimulationTickEnd == 65 && row.TickCount == 1, "Carry simulation");
        Require(!row.PausedAny && row.MaximumBacklogSeconds == .1 && row.ConnectedClientsMin == 18, "Reset per-interval extrema");
        Require(row.UtcStart == StartUtc.AddSeconds(1).ToString("O"), "UTC carry");
    }
    private static void EmptyIntervalsDoNotInventCallsBytesOrPhysics()
    {
        var metric = New(simulation: 7, sent: 10, received: 20);
        var first = metric.Close(1, StartUtc.AddSeconds(1));
        Require(first.TickCount == 0 && first.SentPayloadBytes == 0 && first.SimulationTickStart == 7 && first.SimulationTickEnd == 7, "Empty initial interval");
        Require(first.ConnectedClients == 0 && first.NpcCountMin == 0, "No initial population assumption");
        metric.Record(1.5, 0, 15, 21, 20, 100, 2, 8, false, 0);
        metric.Close(2, StartUtc.AddSeconds(2));
        var next = metric.Close(3, StartUtc.AddSeconds(3));
        Require(next.TickCount == 0 && next.SentPayloadBytes == 0 && next.ReceivedPayloadBytes == 0, "Empty carry bytes");
        Require(next.SimulationTickStart == 8 && next.SimulationTickEnd == 8, "Empty carry physics");
        Require(next.ConnectedClients == 20 && next.ConnectedClientsMin == 20 && next.ConnectedClientsMean == 20 && next.ConnectedClientsMax == 20, "Empty last population hold");
    }
    private static void UtcJumpDoesNotChangeMonotonicDuration()
    {
        var metric = New(); metric.Record(.5, 1, 1, 1, 20, 100, 2, 1, false, 0);
        var row = metric.Close(1, StartUtc.AddSeconds(-60));
        Require(row.DurationSeconds == 1 && row.UtcDurationSeconds == -60 && row.UtcMinusMonotonicSeconds == -61 && row.UtcWentBackwards, "Backward clock diagnostic");
        var forward = metric.Close(2, StartUtc.AddSeconds(60));
        Require(forward.DurationSeconds == 1 && forward.UtcDurationSeconds == 120 && forward.UtcMinusMonotonicSeconds == 119, "Forward clock diagnostic/carry");
    }
    private static void ReturnedDtosNeverAliasAccumulatorOrEachOther()
    {
        var a = New(); a.Record(.5, 1, 1, 1, 20, 100, 2, 1, false, 0); var first = a.Close(1, StartUtc.AddSeconds(1));
        first.TickMilliseconds.Bounds[0] = 999; first.TickMilliseconds.Counts[0] = 999; first.ConnectedClients = -1;
        a.Record(1.5, 1, 2, 2, 20, 100, 2, 2, false, 0); var next = a.Close(2, StartUtc.AddSeconds(2));
        Require(next.TickMilliseconds.Bounds[0] == 1 && next.TickMilliseconds.Counts[0] == 1 && next.ConnectedClients == 20, "Returned object changed accumulator");
        var other = New().Close(1, StartUtc.AddSeconds(1));
        Require(!ReferenceEquals(next.TickMilliseconds.Bounds, other.TickMilliseconds.Bounds) && !ReferenceEquals(next.TickMilliseconds.Counts, other.TickMilliseconds.Counts), "Shared output arrays");
    }
    private static void EveryInvalidRecordIsAtomic()
    {
        var invalid = new Action<RuntimeMetricAccumulator>[] {
            m => m.Record(.2, 1, 14, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(double.NaN, 1, 14, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(double.PositiveInfinity, 1, 14, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(.3, -1, 14, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(.3, double.NaN, 14, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(.3, double.PositiveInfinity, 14, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(.3, 1, 13, 25, 4, 100, 1, 101, false, 0),
            m => m.Record(.3, 1, 14, 24, 4, 100, 1, 101, false, 0),
            m => m.Record(.3, 1, 14, 25, -1, 100, 1, 101, false, 0),
            m => m.Record(.3, 1, 14, 25, 4, -1, 1, 101, false, 0),
            m => m.Record(.3, 1, 14, 25, 4, 100, -1, 101, false, 0),
            m => m.Record(.3, 1, 14, 25, 4, 100, 1, 100, false, 0),
            m => m.Record(.3, 1, 14, 25, 4, 100, 1, 101, false, -1),
            m => m.Record(.3, 1, 14, 25, 4, 100, 1, 101, false, double.NaN),
            m => m.Record(.3, 1, 14, 25, 4, 100, 1, 101, false, double.PositiveInfinity)
        };
        foreach (var bad in invalid)
        {
            var a = New(simulation: 100, sent: 10, received: 20); var b = New(simulation: 100, sent: 10, received: 20);
            foreach (var m in new[] { a, b }) m.Record(.25, 50, 14, 25, 4, 100, 1, 101, false, .02);
            Reject(() => bad(a));
            foreach (var m in new[] { a, b }) m.Record(.5, 1, 20, 35, 5, 99, 2, 105, true, .2);
            Equivalent(a.Close(1, StartUtc.AddSeconds(1)), b.Close(1, StartUtc.AddSeconds(1)));
        }
    }
    private static void EveryInvalidCloseIsAtomic()
    {
        var invalid = new Action<RuntimeMetricAccumulator>[] {
            m => m.Close(.2, StartUtc), m => m.Close(double.NaN, StartUtc), m => m.Close(double.PositiveInfinity, StartUtc),
            m => m.Close(1, DateTime.SpecifyKind(StartUtc, DateTimeKind.Unspecified)),
            m => m.Close(1, DateTime.SpecifyKind(StartUtc, DateTimeKind.Local))
        };
        foreach (var bad in invalid)
        {
            var a = New(); var b = New();
            foreach (var m in new[] { a, b }) m.Record(.25, 1, 2, 3, 20, 100, 2, 1, false, .1);
            Reject(() => bad(a));
            Equivalent(a.Close(1, StartUtc.AddSeconds(1)), b.Close(1, StartUtc.AddSeconds(1)));
        }
        var empty = New(); Reject(() => empty.Close(0, StartUtc)); Require(empty.Close(1, StartUtc.AddSeconds(1)).DurationSeconds == 1, "Zero duration close mutated boundary");
    }
    private static void ConstructorRejectsAmbiguousOrInvalidInitialState()
    {
        Reject(() => New("unknown")); Reject(() => New(null)); Reject(() => New(simulation: -1)); Reject(() => New(sent: -1)); Reject(() => New(received: -1));
        Reject(() => new RuntimeMetricAccumulator("server", double.NaN, StartUtc, 0, 0, 0, false, false));
        Reject(() => new RuntimeMetricAccumulator("server", -1, StartUtc, 0, 0, 0, false, false));
        Reject(() => new RuntimeMetricAccumulator("server", 0, DateTime.SpecifyKind(StartUtc, DateTimeKind.Local), 0, 0, 0, false, false));
    }
    private static void CumulativeLimitsDoNotWrapAndOverloadsAreObserved()
    {
        var a = New(simulation: long.MaxValue - 1, sent: long.MaxValue - 1, received: long.MaxValue - 1);
        a.Record(.5, double.MaxValue, long.MaxValue, long.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, long.MaxValue, true, double.MaxValue);
        var row = a.Close(1, StartUtc.AddSeconds(1));
        Require(row.SentPayloadBytes == 1 && row.ReceivedPayloadBytes == 1 && row.TickMilliseconds.Counts[Bounds.Length] == 1, "Cumulative/duration range");
        Require(row.ConnectedClientsMax == int.MaxValue && row.NpcCount == int.MaxValue && row.ActiveIncidentsMean == int.MaxValue, "Producer must record over-target counts, not cap them");
    }
    private static void StatisticalBoundsDoNotInventMeanP95Ordering()
    {
        var metric = New();
        var durations = new double[100];
        durations[durations.Length - 1] = 10000;
        for (var i = 0; i < durations.Length; i++)
            metric.Record((i + 1) * .005, durations[i], i + 1, i + 1, 20, 100, 2, i + 1, false, 0);
        var row = metric.Close(1, StartUtc.AddSeconds(1));
        var sorted = durations.OrderBy(value => value).ToArray();
        var mean = durations.Average();
        var maximum = durations.Max();
        var p95 = sorted[(int)Math.Ceiling(.95 * sorted.Length) - 1];

        Require(mean == 100 && p95 == 0 && maximum == 10000 && mean > p95,
            "Counterexample must prevent a mean <= p95 invariant.");
        Require(mean >= 0 && mean <= maximum && p95 >= 0 && p95 <= maximum,
            "Exact summaries must be finite nonnegative values bounded by the observed maximum.");
        Require(row.TickMilliseconds.Counts.Sum() == row.TickCount && row.TickMilliseconds.Counts[0] == 99 &&
            row.TickMilliseconds.Counts[Bounds.Length] == 1, "Histogram must retain the counterexample samples.");
        Require(p95 >= 0 && p95 <= row.TickMilliseconds.Bounds[0],
            "Nearest-rank p95 must lie in the selected first bin, whose zero endpoint is inclusive.");
    }
    private static void ClientRuntimeModeAndCallPhysicsSeparation()
    {
        var a = new RuntimeMetricAccumulator("client", 0, StartUtc, 10, 0, 0, true, false);
        for (var i = 1; i <= 5; i++) a.Record(i * .1, 16, i, i, 1, 9, 1, 10, false, 0);
        var row = a.Close(1, StartUtc.AddSeconds(1));
        Require(row.Role == "client" && row.Batch && !row.NullGraphics && row.TickCount == 5 && row.SimulationTickStart == row.SimulationTickEnd, "Do not equate frame calls and physics/GPU");
    }
    [Test] public void IntervalContracts()
    {
        passed=failed=0;
        Check(nameof(IntervalAggregates), IntervalAggregates);
        Check(nameof(EveryHistogramBoundary), EveryHistogramBoundary);
        Check(nameof(CounterAndTickCarryAcrossClose), CounterAndTickCarryAcrossClose);
        Check(nameof(EmptyIntervalsDoNotInventCallsBytesOrPhysics), EmptyIntervalsDoNotInventCallsBytesOrPhysics);
        Check(nameof(UtcJumpDoesNotChangeMonotonicDuration), UtcJumpDoesNotChangeMonotonicDuration);
        Check(nameof(ReturnedDtosNeverAliasAccumulatorOrEachOther), ReturnedDtosNeverAliasAccumulatorOrEachOther);
        Check(nameof(EveryInvalidRecordIsAtomic), EveryInvalidRecordIsAtomic);
        Check(nameof(EveryInvalidCloseIsAtomic), EveryInvalidCloseIsAtomic);
        Check(nameof(ConstructorRejectsAmbiguousOrInvalidInitialState), ConstructorRejectsAmbiguousOrInvalidInitialState);
        Check(nameof(CumulativeLimitsDoNotWrapAndOverloadsAreObserved), CumulativeLimitsDoNotWrapAndOverloadsAreObserved);
        Check(nameof(StatisticalBoundsDoNotInventMeanP95Ordering), StatisticalBoundsDoNotInventMeanP95Ordering);
        Check(nameof(ClientRuntimeModeAndCallPhysicsSeparation), ClientRuntimeModeAndCallPhysicsSeparation);
        Console.WriteLine("{\"Scope\":\"unity_editor_metric_contract\",\"Passed\":" + passed + ",\"Failed\":" + failed + "}");
        Assert.That(failed,Is.Zero);
    }
}
