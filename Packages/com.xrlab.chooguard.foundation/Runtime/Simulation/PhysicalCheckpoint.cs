using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace ChooGuard.Foundation.Simulation
{
    [Serializable] public sealed class PhysicalCheckpointTiming
    { public double TotalMilliseconds, CrowdValidationMilliseconds; }
    /// <summary>Server-local proof for exactly the bytes produced by the checked encoder.
    /// Mutable input/output arrays are copied; never serialize this capability to a client.</summary>
    public sealed class PreparedPhysicalCheckpoint
    {
        private readonly PhysicalWorldState snapshot;
        public string Encoded { get; }
        internal PreparedPhysicalCheckpoint(string encoded, PhysicalWorldState owned)
        { Encoded = encoded; snapshot = owned; }
        internal PhysicalWorldState Read(string encoded, string definitionHash)
        {
            if (!string.Equals(encoded, Encoded, StringComparison.Ordinal) || definitionHash != snapshot.DefinitionHash)
                throw new ArgumentException("Prepared checkpoint differs from its exact local encoding.");
            return snapshot.Copy();
        }
    }
    [Serializable]
    public sealed class PhysicalWorldState
    {
        public int SchemaVersion = 1;
        public string DefinitionHash;
        public long SimulationTick;
        public ulong RandomState = 1;
        public FireState Fire;
        public TrainOperatingState[] Trains = Array.Empty<TrainOperatingState>();
        public string CrowdCheckpoint, DirectorState = "{}";
        public PhysicalWorldState Copy() => new PhysicalWorldState { SchemaVersion = SchemaVersion, DefinitionHash = DefinitionHash,
            SimulationTick = SimulationTick, RandomState = RandomState, Fire = Fire?.Copy(),
            Trains = Trains?.Select(t => t?.Copy()).ToArray(), CrowdCheckpoint = CrowdCheckpoint, DirectorState = DirectorState };
    }

    /// <summary>Exact physical state envelope. Only the authoritative server persists this type; it is not a client projection.</summary>
    public static class PhysicalCheckpoint
    {
        private const int MaximumBytes = 16 * 1024 * 1024;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Encode(PhysicalWorldState state, PhysicalCheckpointTiming timing = null)
            => EncodeCore(state, timing, null);

        public static PreparedPhysicalCheckpoint Prepare(PhysicalWorldState state, CrowdCheckpointProof crowd, PhysicalCheckpointTiming timing = null)
        {
            if (state == null || crowd == null || !string.Equals(state.CrowdCheckpoint, crowd.Encoded, StringComparison.Ordinal))
                throw new ArgumentException("Prepared physical state requires its exact validated crowd encoding.");
            var owned = state.Copy();
            return new PreparedPhysicalCheckpoint(EncodeCore(owned, timing, crowd), owned);
        }

        private static string EncodeCore(PhysicalWorldState state, PhysicalCheckpointTiming timing, CrowdCheckpointProof crowd)
        {
            var started = timing == null ? 0 : Stopwatch.GetTimestamp();
            ValidateShape(state);
            var crowdStarted = timing == null ? 0 : Stopwatch.GetTimestamp();
            if (crowd == null) CrowdMotionModel.FromCheckpoint(state.CrowdCheckpoint);
            if (timing != null) timing.CrowdValidationMilliseconds = Milliseconds(crowdStarted);
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream, Utf8, true))
            {
                w.Write(1); Text(w, state.DefinitionHash, 64); w.Write(state.SimulationTick); w.Write(state.RandomState);
                var f = state.Fire; w.Write(f.SchemaVersion); Text(w, f.ModelVersion, 512);
                w.Write(f.SimulatedSeconds); w.Write(f.ExternalMassKg); w.Write(f.ExternalEnergyJ); w.Write(f.ExternalSmokeKg);
                w.Write(f.ReleasedHeatJ); w.Write(f.ExternalEnthalpyJ); w.Write(f.RadiationEscapedJ); w.Write(f.WallLossJ);
                w.Write(f.AcceptedSubsteps); w.Write(f.ConservativeRepartitions); w.Write(f.Cells.Length);
                foreach (var c in f.Cells)
                {
                    Text(w, c.CellId, 512); w.Write(c.UpperMassKg); w.Write(c.LowerMassKg); w.Write(c.UpperEnergyJ); w.Write(c.LowerEnergyJ);
                    w.Write(c.UpperSmokeKg); w.Write(c.LowerSmokeKg); w.Write(c.WallEnergyJ);
                }
                w.Write(state.Trains.Length);
                foreach (var t in state.Trains)
                {
                    Text(w, t.TrainId, 512); var m = t.Motion;
                    w.Write(m.ReferenceDistanceM); w.Write(m.SignedSpeedMS); w.Write((int)m.Phase); Text(w, m.BrakeCommandId, 512); w.Write(m.BrakeDirection);
                    w.Write(m.DelayRemainingSeconds); w.Write(m.BuildUpRemainingSeconds); w.Write(m.DecelerationMS2); w.Write(m.TargetDecelerationMS2); w.Write(m.BrakeElapsedSeconds);
                    w.Write((int)t.Stage); w.Write(t.StopIndex); w.Write(t.CompletedStops); w.Write(t.DwellRemainingSeconds); w.Write(t.ClosingRemainingSeconds);
                    w.Write(t.ElapsedSeconds); w.Write(t.DoorOpen); w.Write(t.FaultLatched); w.Write(t.ContinueToTarget);
                }
                Text(w, state.CrowdCheckpoint, 12 * 1024 * 1024); Text(w, state.DirectorState, 1024 * 1024);
                w.Flush(); if (stream.Length > MaximumBytes - 32) throw new ArgumentException("Physical checkpoint exceeds its size bound.");
                using (var hash = SHA256.Create()) w.Write(hash.ComputeHash(stream.ToArray()));
                w.Flush(); var encoded = "CGP1:" + Convert.ToBase64String(stream.ToArray());
                if (timing != null) timing.TotalMilliseconds = Milliseconds(started);
                return encoded;
            }
        }

        public static PhysicalWorldState Decode(string encoded, string expectedDefinitionHash, PhysicalCheckpointTiming timing = null)
        {
            var started = timing == null ? 0 : Stopwatch.GetTimestamp();
            try
            {
                if (!Hash(expectedDefinitionHash) || encoded == null || !encoded.StartsWith("CGP1:", StringComparison.Ordinal) ||
                    encoded.Length > MaximumBytes * 4 / 3 + 8) throw new ArgumentException("Unknown, oversized or unbound physical checkpoint.");
                var bytes = Convert.FromBase64String(encoded.Substring(5));
                if (bytes.Length < 32 || bytes.Length > MaximumBytes) throw new ArgumentException("Invalid physical checkpoint length.");
                var length = bytes.Length - 32;
                using (var hash = SHA256.Create())
                {
                    var digest = hash.ComputeHash(bytes, 0, length); var difference = 0;
                    for (var i = 0; i < 32; i++) difference |= digest[i] ^ bytes[length + i];
                    if (difference != 0) throw new ArgumentException("Physical checkpoint digest mismatch.");
                }
                using (var stream = new MemoryStream(bytes, 0, length, false))
                using (var r = new BinaryReader(stream, Utf8, true))
                {
                    if (r.ReadInt32() != 1) throw new ArgumentException("Unknown physical state schema.");
                    var state = new PhysicalWorldState { DefinitionHash = Text(r, 64), SimulationTick = r.ReadInt64(), RandomState = r.ReadUInt64() };
                    if (state.DefinitionHash != expectedDefinitionHash) throw new ArgumentException("Physical state belongs to another simulation definition.");
                    var f = new FireState { SchemaVersion = r.ReadInt32(), ModelVersion = Text(r, 512), SimulatedSeconds = r.ReadDouble(),
                        ExternalMassKg = r.ReadDouble(), ExternalEnergyJ = r.ReadDouble(), ExternalSmokeKg = r.ReadDouble(), ReleasedHeatJ = r.ReadDouble(),
                        ExternalEnthalpyJ = r.ReadDouble(), RadiationEscapedJ = r.ReadDouble(), WallLossJ = r.ReadDouble(), AcceptedSubsteps = r.ReadInt64(), ConservativeRepartitions = r.ReadInt64() };
                    state.Fire = f; f.Cells = new FireCellState[Count(r, 256)];
                    for (var i = 0; i < f.Cells.Length; i++)
                        f.Cells[i] = new FireCellState { CellId = Text(r, 512), UpperMassKg = r.ReadDouble(), LowerMassKg = r.ReadDouble(),
                            UpperEnergyJ = r.ReadDouble(), LowerEnergyJ = r.ReadDouble(), UpperSmokeKg = r.ReadDouble(), LowerSmokeKg = r.ReadDouble(), WallEnergyJ = r.ReadDouble() };
                    state.Trains = new TrainOperatingState[Count(r, 16)];
                    for (var i = 0; i < state.Trains.Length; i++)
                    {
                        var t = new TrainOperatingState { TrainId = Text(r, 512) }; state.Trains[i] = t;
                        t.Motion = new TrainMotionState { ReferenceDistanceM = r.ReadDouble(), SignedSpeedMS = r.ReadDouble(), Phase = (TrainBrakePhase)r.ReadInt32(),
                            BrakeCommandId = Text(r, 512), BrakeDirection = r.ReadInt32(), DelayRemainingSeconds = r.ReadDouble(), BuildUpRemainingSeconds = r.ReadDouble(),
                            DecelerationMS2 = r.ReadDouble(), TargetDecelerationMS2 = r.ReadDouble(), BrakeElapsedSeconds = r.ReadDouble() };
                        t.Stage = (TrainOperatingStage)r.ReadInt32(); t.StopIndex = r.ReadInt32(); t.CompletedStops = r.ReadInt64();
                        t.DwellRemainingSeconds = r.ReadDouble(); t.ClosingRemainingSeconds = r.ReadDouble(); t.ElapsedSeconds = r.ReadDouble();
                        t.DoorOpen = r.ReadBoolean(); t.FaultLatched = r.ReadBoolean(); t.ContinueToTarget = r.ReadBoolean();
                    }
                    state.CrowdCheckpoint = Text(r, 12 * 1024 * 1024); state.DirectorState = Text(r, 1024 * 1024);
                    if (stream.Position != stream.Length) throw new ArgumentException("Trailing physical checkpoint data.");
                    ValidateShape(state);
                    var crowdStarted = timing == null ? 0 : Stopwatch.GetTimestamp();
                    CrowdMotionModel.FromCheckpoint(state.CrowdCheckpoint);
                    if (timing != null) { timing.CrowdValidationMilliseconds = Milliseconds(crowdStarted); timing.TotalMilliseconds = Milliseconds(started); }
                    return state;
                }
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is DecoderFallbackException)
            { throw new ArgumentException("Malformed or truncated physical checkpoint.", e); }
        }

        private static void ValidateShape(PhysicalWorldState s)
        {
            if (s == null || s.SchemaVersion != 1 || !Hash(s.DefinitionHash) || s.SimulationTick < 0 || s.RandomState == 0 || s.Fire == null ||
                s.Fire.SchemaVersion != 1 || !Id(s.Fire.ModelVersion) || s.Fire.Cells == null || s.Fire.Cells.Length < 1 || s.Fire.Cells.Length > 256 ||
                s.Trains == null || s.Trains.Length > 16 || s.CrowdCheckpoint == null || !(s.CrowdCheckpoint.StartsWith("CGC1:", StringComparison.Ordinal) || s.CrowdCheckpoint.StartsWith("CGC2:", StringComparison.Ordinal)) || s.DirectorState == null ||
                !Nonnegative(s.Fire.SimulatedSeconds) || s.Fire.AcceptedSubsteps < 0 || s.Fire.ConservativeRepartitions < 0 ||
                !new[] { s.Fire.ExternalMassKg, s.Fire.ExternalEnergyJ, s.Fire.ExternalSmokeKg, s.Fire.ReleasedHeatJ, s.Fire.ExternalEnthalpyJ, s.Fire.RadiationEscapedJ, s.Fire.WallLossJ }.All(Finite) ||
                s.Fire.Cells.Any(c => c == null || !Id(c.CellId) || !Positive(c.UpperMassKg) || !Positive(c.LowerMassKg) ||
                    !Positive(c.UpperEnergyJ) || !Positive(c.LowerEnergyJ) || !Nonnegative(c.UpperSmokeKg) || !Nonnegative(c.LowerSmokeKg) || !Nonnegative(c.WallEnergyJ) ||
                    c.UpperSmokeKg > c.UpperMassKg || c.LowerSmokeKg > c.LowerMassKg) ||
                s.Fire.Cells.Select(c => c.CellId).Distinct().Count() != s.Fire.Cells.Length)
                throw new ArgumentException("Invalid physical state shape or fire quantities.");
            foreach (var t in s.Trains)
            {
                var m = t?.Motion;
                if (t == null || !Id(t.TrainId) || m == null || !Enum.IsDefined(typeof(TrainOperatingStage), t.Stage) ||
                    !Enum.IsDefined(typeof(TrainBrakePhase), m.Phase) || m.BrakeCommandId == null || m.BrakeCommandId.Length > 128 || t.StopIndex < 0 || t.CompletedStops < 0 ||
                    !new[] { m.ReferenceDistanceM, m.SignedSpeedMS }.All(Finite) ||
                    !new[] { m.DelayRemainingSeconds, m.BuildUpRemainingSeconds, m.DecelerationMS2, m.TargetDecelerationMS2, m.BrakeElapsedSeconds,
                        t.DwellRemainingSeconds, t.ClosingRemainingSeconds, t.ElapsedSeconds }.All(Nonnegative))
                    throw new ArgumentException("Invalid physical train state shape.");
                TrainDynamics.ValidateState(m);
            }
            if (s.Trains.Select(t => t.TrainId).Distinct().Count() != s.Trains.Length) throw new ArgumentException("Duplicate physical train state.");
            // Full semantic frame/train/fire/director validation also requires the hash-bound world definition.
        }
        private static bool Hash(string s) => s != null && s.Length == 64 && s.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        private static double Milliseconds(long started) => (Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
        private static bool Id(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 128;
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Nonnegative(double x) => Finite(x) && x >= 0;
        private static bool Positive(double x) => Finite(x) && x > 0;
        private static int Count(BinaryReader r, int maximum)
        { var count = r.ReadInt32(); if (count < 0 || count > maximum) throw new ArgumentException("Physical collection/string exceeds schema bound."); return count; }
        private static void Text(BinaryWriter w, string text, int maximum)
        { var bytes = Utf8.GetBytes(text); if (bytes.Length > maximum) throw new ArgumentException("Physical string exceeds schema bound."); w.Write(bytes.Length); w.Write(bytes); }
        private static string Text(BinaryReader r, int maximum)
        { var length = Count(r, maximum); var bytes = r.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException(); return Utf8.GetString(bytes); }
    }
}
