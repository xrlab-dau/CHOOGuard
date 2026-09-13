using System;
using System.Globalization;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable]
    public sealed class RuntimeMetricHistogram
    {
        // Inclusive upper bounds plus one final overflow bucket.
        public double[] Bounds;
        public long[] Counts;
    }

    [Serializable]
    public sealed class RuntimeMetricInterval
    {
        public int Schema = 1;
        public string Kind = "interval", Role, UtcStart, UtcEnd;
        public double DurationSeconds, UtcDurationSeconds, UtcMinusMonotonicSeconds;
        public bool UtcWentBackwards, Batch, NullGraphics;
        // Record invocation count; deliberately independent of actual physics progress.
        public long TickCount, SimulationTickStart, SimulationTickEnd;
        public RuntimeMetricHistogram TickMilliseconds;
        public long SentPayloadBytes, ReceivedPayloadBytes;
        public int ConnectedClients, ConnectedClientsMin, ConnectedClientsMax;
        public double ConnectedClientsMean;
        public int NpcCount, NpcCountMin, NpcCountMax;
        public double NpcCountMean;
        public int ActiveIncidents, ActiveIncidentsMin, ActiveIncidentsMax;
        public double ActiveIncidentsMean;
        public bool PausedAny;
        public double MaximumBacklogSeconds;
    }

    /// <summary>Single-writer, engine/file-free interval metrics. Inputs are observations, not
    /// load acceptance. Close returns a detached Serializable snapshot and carries the exact
    /// last observed cumulative-counter/simulation boundaries into the next interval.</summary>
    public sealed class RuntimeMetricAccumulator
    {
        private static readonly double[] BucketBounds = { 1, 2, 4, 8, 12, 16, 20, 25, 33.3, 40, 50,
            66.7, 100, 150, 200, 250, 333, 500, 750, 1000, 2000 };
        private readonly string role;
        private readonly bool batch, nullGraphics;
        private readonly long[] counts = new long[BucketBounds.Length + 1];
        private double startMonotonic, lastMonotonic;
        private DateTime utcStart;
        private long startSent, startReceived, lastSent, lastReceived, startSimulationTick, lastSimulationTick;
        private long samples, connectedSum, npcSum, activeSum;
        private int lastConnected, lastNpc, lastActive, minimumConnected, maximumConnected,
            minimumNpc, maximumNpc, minimumActive, maximumActive;
        private bool pausedAny, lastPaused;
        private double maximumBacklog, lastBacklog;

        public RuntimeMetricAccumulator(string role, double startMonotonic, DateTime utcStart,
            long initialSimulationTick, long initialCumulativeSent, long initialCumulativeReceived,
            bool batch, bool nullGraphics)
        {
            if (role != "server" && role != "client") throw new ArgumentException("Metrics role must be server or client.");
            Nonnegative(startMonotonic, "Invalid initial monotonic clock.");
            Utc(utcStart);
            if (initialSimulationTick < 0 || initialCumulativeSent < 0 || initialCumulativeReceived < 0)
                throw new ArgumentException("Initial metric counters must be nonnegative.");
            this.role = role; this.batch = batch; this.nullGraphics = nullGraphics;
            this.startMonotonic = lastMonotonic = startMonotonic; this.utcStart = utcStart;
            startSimulationTick = lastSimulationTick = initialSimulationTick;
            startSent = lastSent = initialCumulativeSent; startReceived = lastReceived = initialCumulativeReceived;
        }

        public void Record(double nowMonotonic, double durationMs, long cumulativeSent, long cumulativeReceived,
            int currentConnected, int currentNpc, int currentActive, long simulationTick, bool paused, double backlog)
        {
            Clock(nowMonotonic);
            Nonnegative(durationMs, "Duration must be finite and nonnegative.");
            Nonnegative(backlog, "Backlog must be finite and nonnegative.");
            if (cumulativeSent < lastSent || cumulativeReceived < lastReceived || simulationTick < lastSimulationTick)
                throw new ArgumentException("Metric cumulative counters and simulation ticks cannot decrease.");
            if (currentConnected < 0 || currentNpc < 0 || currentActive < 0)
                throw new ArgumentException("Observed populations must be nonnegative.");
            var bucket = 0;
            while (bucket < BucketBounds.Length && durationMs > BucketBounds[bucket]) bucket++;
            long nextSamples, nextCount, nextConnectedSum, nextNpcSum, nextActiveSum;
            checked
            {
                nextSamples = samples + 1; nextCount = counts[bucket] + 1;
                nextConnectedSum = connectedSum + currentConnected;
                nextNpcSum = npcSum + currentNpc; nextActiveSum = activeSum + currentActive;
            }
            // Validate/compute the entire candidate before changing any live field.
            var minConnected = samples == 0 ? currentConnected : Math.Min(minimumConnected, currentConnected);
            var maxConnected = samples == 0 ? currentConnected : Math.Max(maximumConnected, currentConnected);
            var minNpc = samples == 0 ? currentNpc : Math.Min(minimumNpc, currentNpc);
            var maxNpc = samples == 0 ? currentNpc : Math.Max(maximumNpc, currentNpc);
            var minActive = samples == 0 ? currentActive : Math.Min(minimumActive, currentActive);
            var maxActive = samples == 0 ? currentActive : Math.Max(maximumActive, currentActive);
            lastMonotonic = nowMonotonic; lastSent = cumulativeSent; lastReceived = cumulativeReceived;
            lastSimulationTick = simulationTick; counts[bucket] = nextCount;
            connectedSum = nextConnectedSum; npcSum = nextNpcSum; activeSum = nextActiveSum;
            samples = nextSamples; lastConnected = currentConnected; lastNpc = currentNpc; lastActive = currentActive;
            minimumConnected = minConnected; maximumConnected = maxConnected;
            minimumNpc = minNpc; maximumNpc = maxNpc; minimumActive = minActive; maximumActive = maxActive;
            lastPaused = paused; pausedAny |= paused; lastBacklog = backlog; maximumBacklog = Math.Max(maximumBacklog, backlog);
        }

        public RuntimeMetricInterval Close(double nowMonotonic, DateTime utcEnd)
        {
            Clock(nowMonotonic); Utc(utcEnd);
            var seconds = nowMonotonic - startMonotonic;
            if (!(seconds > 0) || double.IsInfinity(seconds)) throw new ArgumentException("Metric intervals require positive finite monotonic duration.");
            var utcSeconds = (utcEnd - utcStart).TotalSeconds;
            var result = new RuntimeMetricInterval
            {
                Role = role, Batch = batch, NullGraphics = nullGraphics,
                UtcStart = utcStart.ToString("O", CultureInfo.InvariantCulture), UtcEnd = utcEnd.ToString("O", CultureInfo.InvariantCulture),
                DurationSeconds = seconds, UtcDurationSeconds = utcSeconds,
                UtcMinusMonotonicSeconds = utcSeconds - seconds, UtcWentBackwards = utcEnd < utcStart,
                TickCount = samples, SimulationTickStart = startSimulationTick, SimulationTickEnd = lastSimulationTick,
                TickMilliseconds = new RuntimeMetricHistogram { Bounds = (double[])BucketBounds.Clone(), Counts = (long[])counts.Clone() },
                SentPayloadBytes = lastSent - startSent, ReceivedPayloadBytes = lastReceived - startReceived,
                ConnectedClients = lastConnected, ConnectedClientsMin = samples == 0 ? lastConnected : minimumConnected,
                ConnectedClientsMax = samples == 0 ? lastConnected : maximumConnected,
                ConnectedClientsMean = samples == 0 ? lastConnected : (double)connectedSum / samples,
                NpcCount = lastNpc, NpcCountMin = samples == 0 ? lastNpc : minimumNpc,
                NpcCountMax = samples == 0 ? lastNpc : maximumNpc,
                NpcCountMean = samples == 0 ? lastNpc : (double)npcSum / samples,
                ActiveIncidents = lastActive, ActiveIncidentsMin = samples == 0 ? lastActive : minimumActive,
                ActiveIncidentsMax = samples == 0 ? lastActive : maximumActive,
                ActiveIncidentsMean = samples == 0 ? lastActive : (double)activeSum / samples,
                PausedAny = samples == 0 ? lastPaused : pausedAny,
                MaximumBacklogSeconds = samples == 0 ? lastBacklog : maximumBacklog
            };
            // Allocation/formatting and all validation have succeeded. Returned arrays are private copies.
            startMonotonic = lastMonotonic = nowMonotonic; utcStart = utcEnd;
            startSent = lastSent; startReceived = lastReceived; startSimulationTick = lastSimulationTick;
            samples = connectedSum = npcSum = activeSum = 0;
            minimumConnected = maximumConnected = minimumNpc = maximumNpc = minimumActive = maximumActive = 0;
            pausedAny = false; maximumBacklog = 0; Array.Clear(counts, 0, counts.Length);
            return result;
        }

        private void Clock(double value)
        {
            Nonnegative(value, "Monotonic clock must be finite and nonnegative.");
            if (value < lastMonotonic) throw new ArgumentException("Monotonic clock cannot decrease.");
        }
        private static void Nonnegative(double value, string message)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentException(message); }
        private static void Utc(DateTime value)
        { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC metric endpoints must have DateTimeKind.Utc."); }
    }
}
