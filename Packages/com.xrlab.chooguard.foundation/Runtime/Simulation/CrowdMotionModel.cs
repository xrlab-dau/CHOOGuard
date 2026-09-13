using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ChooGuard.Foundation.Simulation
{
    /// <summary>First-order CSM walking plus a separate Euclidean admissible-velocity projection.
    /// Metric planar circles/finite static wall segments; no Newtonian force or railway procedure model.
    /// Server tick budget accounting: Total fixed server tick budget is 50ms (20Hz).
    /// Motion simulation sub-budget target is <= 20ms per tick (CoreMs <= 10ms).
    /// Non-motion coupled subsystems (Fire ~73ms, Checkpoints ~34ms, non-Core validation ~51ms)
    /// must be scheduled or optimized under separate tickets to satisfy the global 50ms server budget.</summary>
    public sealed class CrowdMotionModel
    {
        // Standalone candidate switch. This is deliberately not persisted in physical state.
        [Flags] public enum WallOptimization { None = 0, Constraints = 1, Distances = 2, All = 3 }
        public sealed class WallWorkReport
        {
            public long ConstraintCandidates, ConstraintRowsPruned, StartCandidates, StartDistancesPruned;
            public long SweptCandidates, SweptDistancesPruned, NonzeroWindowFallbacks, ProjectionFallbacks;
        }
        public WallOptimization Optimization { get; set; } = WallOptimization.All;
        public WallWorkReport WallWork { get; private set; } = new WallWorkReport();
        public bool ProfilePhases { get; set; }
        public CrowdPhaseReport Phases { get; private set; }
        private long PhaseStamp() => ProfilePhases ? Stopwatch.GetTimestamp() : 0;
        private static double Since(long stamp) => (Stopwatch.GetTimestamp()-stamp)*1000.0/Stopwatch.Frequency;
        // Extra conservatism for arithmetic in the already bounded +/-1e7 m coordinate domain.
        // This margin changes only which exact work is performed; it never relaxes a acceptance tolerance.
        private const double PruningRoundoffMarginM = 1e-6;
        public const string Version = "csm-admissible-contact-2";
        public const string LegacyVersion = "csm-admissible-contact-1";
        private CrowdSnapshot state;
        private string definitionKey;
        private Dictionary<string, double> lengths;
        private ComposedTick composedTick;
        public sealed class ComposedTick
        {
            internal readonly CrowdMotionModel Owner;
            internal readonly long StartTick;
            internal int AcceptedIntervals;
            internal ComposedTick(CrowdMotionModel owner) { Owner=owner;StartTick=owner.state.Tick; }
            public void Complete() => Owner.CompleteComposedTick(this);
        }
        /// <summary>Owns only the logical counter of accepted intervals on this exact model.
        /// Motion, elapsed time, geometry revision and accepted contact substeps are never rewritten.</summary>
        public ComposedTick BeginComposedTick()
        {
            if(composedTick!=null)throw new InvalidOperationException("A composed tick is already active.");
            return composedTick=new ComposedTick(this);
        }
        private void CompleteComposedTick(ComposedTick token)
        {
            if(!ReferenceEquals(composedTick,token)||token.AcceptedIntervals<1||state.Tick!=checked(token.StartTick+token.AcceptedIntervals))
                throw new InvalidOperationException("Missing, stale or already completed composed tick.");
            state.Tick=checked(token.StartTick+1);composedTick=null;
        }

        private struct Constraint
        {
            public int I, J;
            public CrowdVector Normal;
            public double Bound, Lambda;
            public bool MovableI, MovableJ;
            public int Diagonal => (MovableI ? 1 : 0) + (MovableJ ? 1 : 0);
        }

        private sealed class VerticalEligibility
        {
            public bool[,] Pairs, Walls;
        }

        private sealed class SpatialWallGrid
        {
            public const double CellSize = 4.0;
            private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();

            public SpatialWallGrid(CrowdWall[] walls)
            {
                for (var i = 0; i < walls.Length; i++)
                {
                    var w = walls[i];
                    if (!w.Enabled) continue;
                    RasterizeWall(w.A, w.B, i);
                }
            }

            private void AddCell(int cx, int cy, int wallIndex)
            {
                var key = ((long)(uint)cx << 32) | (uint)cy;
                if (!cells.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    cells[key] = list;
                }
                if (list.Count == 0 || list[list.Count - 1] != wallIndex)
                {
                    list.Add(wallIndex);
                }
            }

            private void RasterizeWall(CrowdVector a, CrowdVector b, int wallIndex)
            {
                var cx0 = (int)Math.Floor(a.X / CellSize);
                var cy0 = (int)Math.Floor(a.Y / CellSize);
                var cx1 = (int)Math.Floor(b.X / CellSize);
                var cy1 = (int)Math.Floor(b.Y / CellSize);

                if (cx0 == cx1 && cy0 == cy1)
                {
                    AddCell(cx0, cy0, wallIndex);
                    return;
                }

                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
                var stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);

                var tDeltaX = dx != 0 ? Math.Abs(CellSize / dx) : double.PositiveInfinity;
                var tDeltaY = dy != 0 ? Math.Abs(CellSize / dy) : double.PositiveInfinity;

                double tMaxX, tMaxY;
                if (stepX > 0) tMaxX = ((cx0 + 1) * CellSize - a.X) / dx;
                else if (stepX < 0) tMaxX = (cx0 * CellSize - a.X) / dx;
                else tMaxX = double.PositiveInfinity;

                if (stepY > 0) tMaxY = ((cy0 + 1) * CellSize - a.Y) / dy;
                else if (stepY < 0) tMaxY = (cy0 * CellSize - a.Y) / dy;
                else tMaxY = double.PositiveInfinity;

                var cx = cx0;
                var cy = cy0;
                AddCell(cx, cy, wallIndex);

                var maxSteps = Math.Abs(cx1 - cx0) + Math.Abs(cy1 - cy0) + 2;
                for (var step = 0; step < maxSteps && (cx != cx1 || cy != cy1); step++)
                {
                    if (Math.Abs(tMaxX - tMaxY) < 1e-12)
                    {
                        AddCell(cx + stepX, cy, wallIndex);
                        AddCell(cx, cy + stepY, wallIndex);
                        cx += stepX;
                        cy += stepY;
                        tMaxX += tDeltaX;
                        tMaxY += tDeltaY;
                    }
                    else if (tMaxX < tMaxY)
                    {
                        cx += stepX;
                        tMaxX += tDeltaX;
                    }
                    else
                    {
                        cy += stepY;
                        tMaxY += tDeltaY;
                    }
                    AddCell(cx, cy, wallIndex);
                }
            }

            public void Query(double minX, double maxX, double minY, double maxY, List<int> candidates, int[] stampBuffer, int stamp)
            {
                var cMinX = (int)Math.Floor(minX / CellSize);
                var cMaxX = (int)Math.Floor(maxX / CellSize);
                var cMinY = (int)Math.Floor(minY / CellSize);
                var cMaxY = (int)Math.Floor(maxY / CellSize);

                for (var cx = cMinX; cx <= cMaxX; cx++)
                for (var cy = cMinY; cy <= cMaxY; cy++)
                {
                    var key = ((long)(uint)cx << 32) | (uint)cy;
                    if (cells.TryGetValue(key, out var list))
                    {
                        for (var i = 0; i < list.Count; i++)
                        {
                            var wallIdx = list[i];
                            if (stampBuffer[wallIdx] != stamp)
                            {
                                stampBuffer[wallIdx] = stamp;
                                candidates.Add(wallIdx);
                            }
                        }
                    }
                }
            }
        }

        private SpatialWallGrid wallGrid;
        [ThreadStatic] private static List<int> candidateWallBuffer;
        [ThreadStatic] private static int[] wallStampBuffer;
        [ThreadStatic] private static int wallStamp;

        private static void EnsureQueryBuffers(int wallCount)
        {
            if (candidateWallBuffer == null) candidateWallBuffer = new List<int>(128);
            if (wallStampBuffer == null || wallStampBuffer.Length < wallCount)
            {
                wallStampBuffer = new int[Math.Max(wallCount, 128)];
                wallStamp = 0;
            }
        }

        public CrowdMotionModel(CrowdDefinition definition, CrowdAgent[] agents, long seed = 1)
            : this(new CrowdSnapshot { Definition = definition, Agents = agents, Seed = seed }) { }

        private CrowdMotionModel(CrowdSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentException("Missing crowd snapshot.");
            var candidate = snapshot.Copy(); Validate(candidate);
            Canonicalize(candidate);
            state = candidate; definitionKey = DefinitionKey(candidate.Definition);
            lengths = candidate.Definition.Spaces.ToDictionary(s => s.Id, s => s.PeriodicLengthXM, StringComparer.Ordinal);
            wallGrid = new SpatialWallGrid(candidate.Definition.Walls);
            if (MinimumGap(candidate.Agents, candidate.Definition, null, 0) < -candidate.Definition.Parameters.GeometryToleranceM)
                throw new ArgumentException("Crowd initial or restored geometry penetrates a body or wall.");
        }

        // A validated model never mutates its owned definition. Rebind/restore replace it with
        // a newly validated private definition; a fork can therefore share only that immutable state.
        private CrowdMotionModel(CrowdMotionModel source)
        { state = CopyMutable(source.state); definitionKey = source.definitionKey; lengths = source.lengths; Optimization = source.Optimization; ProfilePhases=source.ProfilePhases; wallGrid = source.wallGrid; }
        public CrowdMotionModel Fork() => new CrowdMotionModel(this);
        public CrowdReadState ReadState() => new CrowdReadState { Agents = state.Agents.Select(a => a.Copy()).ToArray(),
            Seed = state.Seed, Tick = state.Tick, AcceptedSubsteps = state.AcceptedSubsteps,
            GeometryRevision = state.GeometryRevision, ElapsedSeconds = state.ElapsedSeconds };
        private static CrowdSnapshot CopyMutable(CrowdSnapshot source) => new CrowdSnapshot { SchemaVersion = source.SchemaVersion,
            ModelVersion = source.ModelVersion, Definition = source.Definition, Agents = source.Agents.Select(a => a.Copy()).ToArray(),
            Seed = source.Seed, Tick = source.Tick, AcceptedSubsteps = source.AcceptedSubsteps,
            GeometryRevision = source.GeometryRevision, ElapsedSeconds = source.ElapsedSeconds };

        public static CrowdMotionModel FromSnapshot(CrowdSnapshot snapshot) => new CrowdMotionModel(snapshot);
        public CrowdSnapshot ExportSnapshot() => state.Copy();
        public string ExportCheckpoint() => CrowdCheckpoint.Encode(state);
        public CrowdCheckpointProof ExportCheckpointProof() => new CrowdCheckpointProof(CrowdCheckpoint.Encode(state));
        public static CrowdMotionModel FromCheckpoint(string checkpoint) => FromSnapshot(CrowdCheckpoint.Decode(checkpoint));
        public bool TryRestoreCheckpoint(string checkpoint, out string failure)
        {
            try { return TryRestore(CrowdCheckpoint.Decode(checkpoint), out failure); }
            catch (ArgumentException e) { failure = e.Message; return false; }
        }

        public bool TryRestore(CrowdSnapshot snapshot, out string failure)
        {
            failure = "";
            try
            {
                var candidate = FromSnapshot(snapshot);
                if (candidate.definitionKey != definitionKey || candidate.state.Seed != state.Seed || candidate.state.GeometryRevision != state.GeometryRevision ||
                    candidate.state.Agents.Length != state.Agents.Length)
                    throw new ArgumentException("Restored crowd profile, seed, or roster differs from this model.");
                for (var i = 0; i < state.Agents.Length; i++)
                {
                    var a = state.Agents[i]; var b = candidate.state.Agents[i];
                    if (a.Id != b.Id || a.RadiusM != b.RadiusM || a.PreferredSpeedMS != b.PreferredSpeedMS || a.HeightM != b.HeightM)
                        throw new ArgumentException("Restored crowd identity/body profile differs from this model.");
                    if (a.Pinned != b.Pinned || a.ContactSpaceId != b.ContactSpaceId || a.FrameId != b.FrameId || a.RegionId != b.RegionId ||
                        a.SurfaceId != b.SurfaceId || a.SupportGradient.X != b.SupportGradient.X || a.SupportGradient.Y != b.SupportGradient.Y ||
                        Math.Abs((a.FootElevationM - b.FootElevationM) - CrowdVector.Dot(a.SupportGradient, a.Position - b.Position)) > state.Definition.Parameters.GeometryToleranceM)
                        throw new ArgumentException("Restored body changed its support binding without a geometry rebind.");
                }
                state = candidate.state; composedTick=null; return true;
            }
            catch (ArgumentException e) { failure = e.Message; return false; }
        }

        public bool TryRebind(CrowdRebind request, out string failure)
        {
            failure = "";
            try
            {
                if (request == null || request.ExpectedTick != state.Tick || request.ExpectedGeometryRevision != state.GeometryRevision ||
                    state.GeometryRevision == long.MaxValue || request.Spaces == null || request.Walls == null || request.Bodies == null ||
                    request.Spaces.Length > 1000 || request.Walls.Length > 10000 || request.Bodies.Length != state.Agents.Length ||
                    request.Bodies.Any(b => b == null || !Id(b.AgentId) || !VectorFinite(b.Position) || !VectorFinite(b.Velocity) ||
                        !VectorFinite(b.Goal) || !VectorFinite(b.DesiredVelocity) || !VectorFinite(b.SupportGradient) || !Finite(b.FootElevationM)) ||
                    request.Bodies.Select(b => b.AgentId).Distinct(StringComparer.Ordinal).Count() != request.Bodies.Length)
                    throw new ArgumentException("Invalid, stale, or unbounded crowd geometry rebind.");
                var candidate = state.Copy();
                candidate.Definition.Spaces = request.Spaces.Select(s => s?.Copy()).ToArray();
                candidate.Definition.Walls = request.Walls.Select(w => w?.Copy()).ToArray();
                foreach (var a in candidate.Agents)
                {
                    var binding = request.Bodies.SingleOrDefault(b => b.AgentId == a.Id);
                    if (binding == null) throw new ArgumentException("Crowd rebind cannot change the roster.");
                    a.ContactSpaceId = binding.ContactSpaceId; a.RegionId = binding.RegionId; a.FrameId = binding.FrameId; a.SurfaceId = binding.SurfaceId;
                    a.Position = binding.Position; a.Velocity = binding.Pinned ? new CrowdVector() : binding.Velocity;
                    a.Goal = binding.Goal; a.DesiredVelocity = binding.DesiredVelocity; a.SupportGradient = binding.SupportGradient;
                    a.FootElevationM = binding.FootElevationM; a.Pinned = binding.Pinned;
                }
                candidate.GeometryRevision++;
                // Construction checks finite/profile/vertical/periodic geometry before any live reference is replaced.
                var validated = FromSnapshot(candidate);
                state = validated.state; definitionKey = validated.definitionKey; lengths = validated.lengths; wallGrid = validated.wallGrid;
                return true;
            }
            catch (ArgumentException e) { failure = e.Message; return false; }
        }

        public bool TryAdvance(double seconds, CrowdIntent[] intents, out CrowdStepReport report, CrowdMovementWindow[] movementWindows = null)
        {
            report = new CrowdStepReport { MinimumSweptGapM = double.PositiveInfinity };
            WallWork = new WallWorkReport();
            Phases=ProfilePhases?new CrowdPhaseReport():null; var totalStarted=PhaseStamp(); var phaseStarted=totalStarted;
            try
            {
                if (!Finite(seconds) || seconds <= 0 || seconds > 60) throw new ArgumentException("Crowd tick must be in (0,60] seconds.");
                if (state.Tick == long.MaxValue) throw new ArgumentException("Crowd tick counter exhausted.");
                if(composedTick!=null && composedTick.AcceptedIntervals==int.MaxValue)throw new ArgumentException("Composed tick interval limit exceeded.");
                var next = CopyMutable(state); var p = next.Definition.Parameters;
                ApplyIntents(next.Agents, intents);
                var windows = MovementWindows(next.Agents, movementWindows, p.GeometryToleranceM);
                // Equal substeps avoid a tiny floating-point tail; reject the entire requested tick before commit.
                var count = (int)Math.Ceiling(seconds / p.MaxSubstepSeconds - 1e-12);
                if (count > p.MaxSubstepsPerTick || count < 1 || next.AcceptedSubsteps > long.MaxValue - count)
                    return Fail(report, "Crowd substep budget exceeded.");
                var h = seconds / count;
                if(Phases!=null)Phases.CopyIntentWindowMs=Since(phaseStarted);
                for (var step = 0; step < count; step++)
                {
                    phaseStarted=PhaseStamp();
                    report.AttemptedSubsteps++;
                    var eligibility = Eligibility(next.Agents, next.Definition, h);
                    if(Phases!=null){Phases.EligibilityMs+=Since(phaseStarted);Phases.Substeps++;} phaseStarted=PhaseStamp();
                    if (!ValidateProjectedStart(next.Agents, next.Definition, eligibility))
                        return Fail(report, "Crowd vertical reach overlaps an initially intersecting horizontal footprint; reduce the coupled step or rebind support.");
                    if(Phases!=null)Phases.StartGeometryMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    var desired = DesiredVelocities(next.Agents, next.Definition, h, eligibility);
                    if(Phases!=null)Phases.DesiredMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    var speedBound = ConstraintSpeedBound(next.Agents, desired, windows);
                    var prunedBefore = WallWork.ConstraintRowsPruned;
                    var constraints = Constraints(next.Agents, next.Definition, h, eligibility, speedBound);
                    AppendMovementConstraints(constraints, next.Agents, windows, h);
                    report.MaximumConstraintCount = Math.Max(report.MaximumConstraintCount, constraints.Count);
                    var usedPruning = WallWork.ConstraintRowsPruned != prunedBefore;
                    var projectionReport = usedPruning ? new CrowdStepReport() : report;
                    if(Phases!=null)Phases.ConstraintsMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    var projected = Project(desired, constraints, p, projectionReport, out var velocity);
                    // A posteriori certificate covers numerical solver error. If the candidate is
                    // outside the pruning ball (or fails), rerun the unchanged complete problem.
                    if (usedPruning && (!projected || VelocityNorm(velocity) > speedBound))
                    {
                        WallWork.ProjectionFallbacks++;
                        constraints = Constraints(next.Agents, next.Definition, h, eligibility, double.PositiveInfinity);
                        AppendMovementConstraints(constraints, next.Agents, windows, h);
                        report.MaximumConstraintCount = Math.Max(report.MaximumConstraintCount, constraints.Count);
                        projectionReport = new CrowdStepReport();
                        projected = Project(desired, constraints, p, projectionReport, out velocity);
                    }
                    if (usedPruning) MergeProjectionReport(report, projectionReport);
                    if(Phases!=null)Phases.ProjectionMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    if (!projected) return Fail(report, "Crowd contact projection did not converge within its residual/iteration budget.");
                    for (var i = 0; i < velocity.Length; i++)
                    {
                        if (!VectorFinite(velocity[i])) return Fail(report, "Crowd projection produced nonfinite velocity.");
                        var length = lengths[next.Agents[i].ContactSpaceId];
                        if (length > 0 && velocity[i].Length * h >= length / 4)
                            return Fail(report, "Crowd periodic displacement exceeds its swept image coverage; reduce substep.");
                    }
                    var gap = MinimumGap(next.Agents, next.Definition, velocity, h, (Optimization & WallOptimization.Distances) != 0, WallWork);
                    if(Phases!=null)Phases.SweptGeometryMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    report.MinimumSweptGapM = Math.Min(report.MinimumSweptGapM, gap);
                    if (gap < -p.GeometryToleranceM) return Fail(report, "Crowd full swept geometry residual exceeds tolerance.");
                    for (var i = 0; i < next.Agents.Length; i++)
                    {
                        var a = next.Agents[i]; var verticalSpeed = CrowdVector.Dot(a.SupportGradient, velocity[i]);
                        a.Position += velocity[i] * h; a.FootElevationM += verticalSpeed * h; a.Velocity = velocity[i];
                        report.MaximumGroundSpeedMS = Math.Max(report.MaximumGroundSpeedMS, Math.Sqrt(velocity[i].LengthSquared + verticalSpeed * verticalSpeed));
                        var length = lengths[a.ContactSpaceId];
                        if (length > 0) a.Position.X = Wrap(a.Position.X, length);
                        if (!VectorFinite(a.Position) || !Finite(a.FootElevationM) || Math.Abs(a.FootElevationM) > 1e7 || Math.Abs(a.Position.X) > 1e7 || Math.Abs(a.Position.Y) > 1e7)
                            return Fail(report, "Crowd position exceeds the supported metric range.");
                        var window = windows[i];
                        if (window != null && (a.Position.X < window.MinX - p.GeometryToleranceM || a.Position.X > window.MaxX + p.GeometryToleranceM ||
                            a.Position.Y < window.MinY - p.GeometryToleranceM || a.Position.Y > window.MaxY + p.GeometryToleranceM))
                            return Fail(report, "Crowd support-window residual exceeds tolerance.");
                    }
                    if(Phases!=null)Phases.CommitMs+=Since(phaseStarted);
                }
                next.Tick++; next.AcceptedSubsteps += count; next.ElapsedSeconds += seconds;
                if (!Finite(next.ElapsedSeconds)) return Fail(report, "Crowd elapsed time overflow.");
                state = next; report.Committed = true;
                if(composedTick!=null)composedTick.AcceptedIntervals++;
                if (double.IsPositiveInfinity(report.MinimumSweptGapM)) report.MinimumSweptGapM = 0;
                if(Phases!=null)Phases.TotalMilliseconds=Since(totalStarted);
                return true;
            }
            catch (ArgumentException e) { return Fail(report, e.Message); }
        }

        private static bool Fail(CrowdStepReport report, string reason)
        { report.Failure = reason; if (double.IsPositiveInfinity(report.MinimumSweptGapM)) report.MinimumSweptGapM = 0; return false; }

        private static CrowdMovementWindow[] MovementWindows(CrowdAgent[] bodies, CrowdMovementWindow[] requested, double tolerance)
        {
            var result = new CrowdMovementWindow[bodies.Length]; if (requested == null) return result;
            if (requested.Length > bodies.Length) throw new ArgumentException("Too many crowd movement windows.");
            foreach (var w in requested)
            {
                if (w == null || !Id(w.AgentId) || !VectorFinite(new CrowdVector(w.MinX, w.MinY)) || !VectorFinite(new CrowdVector(w.MaxX, w.MaxY)) ||
                    w.MinX > w.MaxX || w.MinY > w.MaxY) throw new ArgumentException("Invalid support movement window.");
                var index = Array.FindIndex(bodies, a => a.Id == w.AgentId);
                if (index < 0 || result[index] != null) throw new ArgumentException("Unknown or duplicate support movement body.");
                var p = bodies[index].Position;
                if (p.X < w.MinX - tolerance || p.X > w.MaxX + tolerance || p.Y < w.MinY - tolerance || p.Y > w.MaxY + tolerance)
                    throw new ArgumentException("Initial body is outside its support window.");
                result[index] = new CrowdMovementWindow { AgentId = w.AgentId, MinX = w.MinX, MaxX = w.MaxX, MinY = w.MinY, MaxY = w.MaxY };
            }
            return result;
        }

        private static void ApplyIntents(CrowdAgent[] agents, CrowdIntent[] intents)
        {
            if (intents == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var intent in intents)
            {
                if (intent == null || !Id(intent.AgentId) || !ids.Add(intent.AgentId) ||
                    !Enum.IsDefined(typeof(CrowdIntentMode), intent.Mode) || !VectorFinite(intent.Goal) ||
                    !VectorFinite(intent.DesiredVelocity) || intent.DesiredVelocity.Length > 20)
                    throw new ArgumentException("Invalid, duplicate, or nonfinite crowd intent.");
                var a = agents.FirstOrDefault(body => body.Id == intent.AgentId);
                if (a == null) throw new ArgumentException("Crowd intent names an unknown agent.");
                a.IntentMode = intent.Mode; a.Goal = intent.Goal; a.DesiredVelocity = intent.DesiredVelocity;
            }
        }

        private static CrowdVector GroundWish(CrowdVector horizontal, CrowdVector gradient)
        {
            var length = horizontal.Length;
            if (length == 0 || gradient.LengthSquared == 0) return horizontal;
            var slope = CrowdVector.Dot(gradient, horizontal) / length;
            return horizontal / Math.Sqrt(1 + slope * slope);
        }

        private static VerticalEligibility Eligibility(CrowdAgent[] agents, CrowdDefinition d, double h)
        {
            // Projection onto a convex set containing zero cannot increase the full velocity-vector norm.
            // Conservative vertical reach therefore includes every possible projected velocity, not only its wish.
            var squared = 0.0;
            foreach (var a in agents)
                if (!a.Pinned) squared += a.IntentMode == CrowdIntentMode.DesiredVelocity ? a.DesiredVelocity.LengthSquared : a.PreferredSpeedMS * a.PreferredSpeedMS;
            var reach = h * (Math.Sqrt(squared) + 1e-6);
            var low = new double[agents.Length]; var high = new double[agents.Length];
            var result = new VerticalEligibility { Pairs = new bool[agents.Length, agents.Length], Walls = new bool[agents.Length, d.Walls.Length] };
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i]; var extent = a.Pinned ? 0 : reach * a.SupportGradient.Length;
                low[i] = a.FootElevationM - extent; high[i] = a.FootElevationM + a.HeightM + extent;
            }
            var tolerance = d.Parameters.GeometryToleranceM;
            for (var i = 0; i < agents.Length; i++)
            {
                for (var j = 0; j < agents.Length; j++) result.Pairs[i, j] = agents[i].ContactSpaceId == agents[j].ContactSpaceId &&
                    high[i] > low[j] + tolerance && high[j] > low[i] + tolerance;
                for (var j = 0; j < d.Walls.Length; j++)
                {
                    var w = d.Walls[j]; result.Walls[i, j] = w.Enabled && w.ContactSpaceId == agents[i].ContactSpaceId &&
                        (!w.HasVerticalBounds || high[i] > w.BottomElevationM + tolerance && w.TopElevationM > low[i] + tolerance);
                }
            }
            return result;
        }

        private bool ValidateProjectedStart(CrowdAgent[] agents, CrowdDefinition d, VerticalEligibility eligible)
        {
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i]; var length = lengths[a.ContactSpaceId];
                for (var j = i + 1; j < agents.Length; j++)
                {
                    if (!eligible.Pairs[i, j]) continue;
                    var delta = a.Position - agents[j].Position;
                    if (length > 0) delta.X -= Math.Floor(delta.X / length + .5) * length;
                    if (delta.Length < a.RadiusM + agents[j].RadiusM - d.Parameters.GeometryToleranceM) return false;
                }
                for (var j = 0; j < d.Walls.Length; j++)
                {
                    if (!eligible.Walls[i, j]) continue;
                    WallWork.StartCandidates++;
                    var wall = d.Walls[j];
                    if ((Optimization & WallOptimization.Distances) != 0 && BoxSeparation(a.Position, a.Position, wall.A, wall.B) > a.RadiusM + PruningRoundoffMarginM)
                    { WallWork.StartDistancesPruned++; continue; }
                    if ((a.Position - Closest(a.Position, wall.A, wall.B)).Length < a.RadiusM - d.Parameters.GeometryToleranceM) return false;
                }
            }
            return true;
        }

        private CrowdVector[] DesiredVelocities(CrowdAgent[] agents, CrowdDefinition d, double h, VerticalEligibility eligible)
        {
            var result = new CrowdVector[agents.Length]; var p = d.Parameters;
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i];
                if (a.Pinned) continue;
                if (a.IntentMode == CrowdIntentMode.DesiredVelocity) { if(Phases!=null)Phases.DirectVelocityBodies++;result[i] = GroundWish(a.DesiredVelocity, a.SupportGradient); continue; }
                if(Phases!=null)Phases.CsmBodies++;
                var goal = a.Goal - a.Position; var goalDistance = goal.Length;
                if (goalDistance < 1e-12) continue;
                var direction = goal / goalDistance; var length = lengths[a.ContactSpaceId];
                var phaseStarted=PhaseStamp();
                for (var j = 0; j < agents.Length; j++)
                {
                    var b = agents[j]; if (i == j || !eligible.Pairs[i, j]) continue;
                    var delta = a.Position - b.Position;
                    if (length > 0) delta.X -= Math.Floor(delta.X / length + .5) * length;
                    var distance = delta.Length;
                    if (Occluded(a.Position, b.Position, i, j, d.Walls, eligible)) continue;
                    // Periodic fixture extension uses the minimum image, averaging equally near images at a half-period tie.
                    // Fixed truncated image sums would introduce a seam-dependent direction, especially for long ranges.
                    if (length > 0 && Math.Abs(Math.Abs(delta.X) - length / 2) <= 1e-12 * Math.Max(1, length)) delta.X = 0;
                    direction += delta * (p.PersonRepulsion * Math.Exp(Math.Min(0, (a.RadiusM + b.RadiusM - distance) / p.PersonRepulsionRangeM)) / distance);
                }
                if(Phases!=null)Phases.PersonRepulsionMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                for (var wallIndex = 0; wallIndex < d.Walls.Length; wallIndex++)
                {
                    if (!eligible.Walls[i, wallIndex]) continue;
                    var wall = d.Walls[wallIndex];
                    var delta = a.Position - Closest(a.Position, wall.A, wall.B); var distance = delta.Length;
                    direction += delta * (p.WallRepulsion * Math.Exp(Math.Min(0, (a.RadiusM - distance) / p.WallRepulsionRangeM)) / distance);
                }
                if(Phases!=null)Phases.WallRepulsionMs+=Since(phaseStarted);
                var norm = direction.Length;
                // Exact cancellation is a deterministic stop, not an invented symmetry-breaking behavioural rule.
                if (norm < 1e-12) continue;
                direction /= norm;
                var speed = a.SupportGradient.LengthSquared == 0 ? Math.Min(a.PreferredSpeedMS, goalDistance / h) : a.PreferredSpeedMS;
                for (var j = 0; j < agents.Length; j++)
                {
                    var b = agents[j]; if (!eligible.Pairs[i, j]) continue;
                    for (var image = length > 0 ? -1 : 0; image <= (length > 0 ? 1 : 0); image++)
                    {
                        if (i == j && image == 0) continue;
                        var bp = b.Position + new CrowdVector(image * length, 0); var delta = bp - a.Position;
                        var combined = a.RadiusM + b.RadiusM;
                        // CSM eq.2-3 use Euclidean centre distance, not forward projection; body widths add.
                        if (CrowdVector.Dot(direction, delta) >= 0 && Math.Abs(CrowdVector.Cross(direction, delta)) <= combined &&
                            !Occluded(a.Position, bp, i, j, d.Walls, eligible))
                            speed = Math.Min(speed, Math.Max(0, (delta.Length - combined) / p.TimeGapSeconds));
                    }
                }
                result[i] = GroundWish(direction * speed, a.SupportGradient);
                if (a.SupportGradient.LengthSquared != 0 && result[i].Length > goalDistance / h)
                    result[i] = direction * (goalDistance / h);
            }
            return result;
        }

        private List<Constraint> Constraints(CrowdAgent[] agents, CrowdDefinition d, double h, VerticalEligibility eligible, double speedBound)
        {
            var rows = new List<Constraint>();
            EnsureQueryBuffers(d.Walls.Length);
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i]; var length = lengths[a.ContactSpaceId];
                for (var j = i + 1; j < agents.Length; j++)
                {
                    var b = agents[j]; if (!eligible.Pairs[i, j] || a.Pinned && b.Pinned) continue;
                    for (var image = length > 0 ? -1 : 0; image <= (length > 0 ? 1 : 0); image++)
                    {
                        var offset = new CrowdVector(image * length, 0);
                        var bPos = b.Position + offset;
                        if (Finite(speedBound) && BoxSeparation(a.Position, a.Position, bPos, bPos) >
                            a.RadiusM + b.RadiusM + 2 * h * speedBound + PruningRoundoffMarginM)
                        {
                            WallWork.ConstraintRowsPruned++;
                            continue;
                        }
                        var delta = a.Position - bPos; var distance = delta.Length;
                        rows.Add(new Constraint { I = i, J = j, Normal = delta / distance, MovableI = !a.Pinned, MovableJ = !b.Pinned,
                            // Numerically tolerated initial gaps do not remove zero from the feasible velocity set.
                            Bound = -Math.Max(0, distance - a.RadiusM - b.RadiusM) / h });
                    }
                }
                if (a.Pinned) continue;
                if (wallGrid != null && Finite(speedBound))
                {
                    var dilation = a.RadiusM + h * speedBound + PruningRoundoffMarginM;
                    candidateWallBuffer.Clear();
                    wallStamp++;
                    if (wallStamp == int.MaxValue) { Array.Clear(wallStampBuffer, 0, wallStampBuffer.Length); wallStamp = 1; }
                    wallGrid.Query(a.Position.X - dilation, a.Position.X + dilation,
                                   a.Position.Y - dilation, a.Position.Y + dilation,
                                   candidateWallBuffer, wallStampBuffer, wallStamp);
                    candidateWallBuffer.Sort();
                    for (var ci = 0; ci < candidateWallBuffer.Count; ci++)
                    {
                        var wallIndex = candidateWallBuffer[ci];
                        if (!eligible.Walls[i, wallIndex]) continue;
                        var wall = d.Walls[wallIndex]; WallWork.ConstraintCandidates++;
                        if (BoxSeparation(a.Position, a.Position, wall.A, wall.B) > a.RadiusM + h * speedBound + PruningRoundoffMarginM)
                        { WallWork.ConstraintRowsPruned++; continue; }
                        var delta = a.Position - Closest(a.Position, wall.A, wall.B); var distance = delta.Length;
                        // Distance to a convex finite segment has this supporting plane, including its endpoints.
                        rows.Add(new Constraint { I = i, J = -1, Normal = delta / distance, MovableI = true, Bound = -Math.Max(0, distance - a.RadiusM) / h });
                    }
                }
                else
                {
                    for (var wallIndex = 0; wallIndex < d.Walls.Length; wallIndex++)
                    {
                        if (!eligible.Walls[i, wallIndex]) continue;
                        var wall = d.Walls[wallIndex]; WallWork.ConstraintCandidates++;
                        if (Finite(speedBound) && BoxSeparation(a.Position, a.Position, wall.A, wall.B) > a.RadiusM + h * speedBound + PruningRoundoffMarginM)
                        { WallWork.ConstraintRowsPruned++; continue; }
                        var delta = a.Position - Closest(a.Position, wall.A, wall.B); var distance = delta.Length;
                        // Distance to a convex finite segment has this supporting plane, including its endpoints.
                        rows.Add(new Constraint { I = i, J = -1, Normal = delta / distance, MovableI = true, Bound = -Math.Max(0, distance - a.RadiusM) / h });
                    }
                }
            }
            return rows;
        }

        private static void MergeProjectionReport(CrowdStepReport target, CrowdStepReport source)
        {
            target.ActiveSetSolves += source.ActiveSetSolves;
            target.MaximumProjectionIterations = Math.Max(target.MaximumProjectionIterations, source.MaximumProjectionIterations);
            target.MaximumDualMultiplierMS = Math.Max(target.MaximumDualMultiplierMS, source.MaximumDualMultiplierMS);
            target.MaximumConstraintViolationMS = Math.Max(target.MaximumConstraintViolationMS, source.MaximumConstraintViolationMS);
            target.MaximumComplementarityResidual = Math.Max(target.MaximumComplementarityResidual, source.MaximumComplementarityResidual);
            target.MaximumStationarityResidualMS = Math.Max(target.MaximumStationarityResidualMS, source.MaximumStationarityResidualMS);
        }
        private double ConstraintSpeedBound(CrowdAgent[] agents, CrowdVector[] desired, CrowdMovementWindow[] windows)
        {
            if ((Optimization & WallOptimization.Constraints) == 0) return double.PositiveInfinity;
            for (var i = 0; i < agents.Length; i++)
            {
                var w = windows[i]; var a = agents[i]; if (w == null || a.Pinned) continue;
                // Window validation permits a tolerance-sized initial overhang. Such rows can
                // exclude zero: fall back instead of using the zero-feasible projection theorem.
                if (a.Position.X < w.MinX || a.Position.X > w.MaxX || a.Position.Y < w.MinY || a.Position.Y > w.MaxY)
                { WallWork.NonzeroWindowFallbacks++; return double.PositiveInfinity; }
            }
            return VelocityNorm(desired) + 1e-6;
        }
        private static double VelocityNorm(CrowdVector[] values)
        { var squared = 0.0; foreach (var v in values) squared += v.LengthSquared; return Math.Sqrt(squared); }
        private static void AppendMovementConstraints(List<Constraint> rows, CrowdAgent[] agents, CrowdMovementWindow[] windows, double h)
        {
            for (var i = 0; i < agents.Length; i++)
            {
                var window = windows[i]; var body = agents[i]; if (window == null || body.Pinned) continue;
                rows.Add(new Constraint { I = i, J = -1, MovableI = true, Normal = new CrowdVector(1, 0), Bound = (window.MinX - body.Position.X) / h });
                rows.Add(new Constraint { I = i, J = -1, MovableI = true, Normal = new CrowdVector(-1, 0), Bound = (body.Position.X - window.MaxX) / h });
                rows.Add(new Constraint { I = i, J = -1, MovableI = true, Normal = new CrowdVector(0, 1), Bound = (window.MinY - body.Position.Y) / h });
                rows.Add(new Constraint { I = i, J = -1, MovableI = true, Normal = new CrowdVector(0, -1), Bound = (body.Position.Y - window.MaxY) / h });
            }
        }
        // L-infinity distance between axis-aligned boxes is a lower bound on the
        // Euclidean distance of the enclosed segments, including degenerate points.
        private static double BoxSeparation(CrowdVector a, CrowdVector b, CrowdVector c, CrowdVector d)
        {
            var x = Math.Max(Math.Min(a.X, b.X) - Math.Max(c.X, d.X), Math.Min(c.X, d.X) - Math.Max(a.X, b.X));
            var y = Math.Max(Math.Min(a.Y, b.Y) - Math.Max(c.Y, d.Y), Math.Min(c.Y, d.Y) - Math.Max(a.Y, b.Y));
            return Math.Max(0, Math.Max(x, y));
        }

        private static bool Project(CrowdVector[] desired, List<Constraint> rows, CrowdParameters p, CrowdStepReport report, out CrowdVector[] velocity)
        {
            velocity = desired.ToArray();
            if (TryActiveProjection(desired, rows, p, out var factored)) { velocity = factored; report.ActiveSetSolves++; }
            var constraintResidual = 0.0; var complementarity = 0.0;
            for (var iteration = 1; iteration <= p.MaxProjectionIterations; iteration++)
            {
                for (var k = 0; k < rows.Count; k++)
                {
                    var row = rows[k]; var value = DotRow(row, velocity); var normSquared = row.Diagonal;
                    var lambda = Math.Max(0, row.Lambda + (row.Bound - value) / normSquared);
                    var change = row.Normal * (lambda - row.Lambda);
                    if (row.MovableI) velocity[row.I] += change; if (row.MovableJ) velocity[row.J] -= change;
                    row.Lambda = lambda; rows[k] = row;
                }
                constraintResidual = 0; complementarity = 0;
                for (var k = 0; k < rows.Count; k++)
                {
                    var row = rows[k]; var slack = DotRow(row, velocity) - row.Bound;
                    constraintResidual = Math.Max(constraintResidual, -slack);
                    complementarity = Math.Max(complementarity, Math.Abs(row.Lambda * slack));
                }
                report.MaximumProjectionIterations = Math.Max(report.MaximumProjectionIterations, iteration);
                if (constraintResidual > p.VelocityToleranceMS || complementarity > p.ComplementarityTolerance) continue;
                var reconstructed = desired.ToArray();
                foreach (var row in rows)
                {
                    var change = row.Normal * row.Lambda;
                    if (row.MovableI) reconstructed[row.I] += change; if (row.MovableJ) reconstructed[row.J] -= change;
                    report.MaximumDualMultiplierMS = Math.Max(report.MaximumDualMultiplierMS, row.Lambda);
                }
                var stationarity = 0.0;
                for (var i = 0; i < velocity.Length; i++) stationarity = Math.Max(stationarity, (reconstructed[i] - velocity[i]).Length);
                report.MaximumConstraintViolationMS = Math.Max(report.MaximumConstraintViolationMS, constraintResidual);
                report.MaximumComplementarityResidual = Math.Max(report.MaximumComplementarityResidual, complementarity);
                report.MaximumStationarityResidualMS = Math.Max(report.MaximumStationarityResidualMS, stationarity);
                return stationarity <= p.VelocityToleranceMS;
            }
            report.MaximumConstraintViolationMS = Math.Max(report.MaximumConstraintViolationMS, constraintResidual);
            report.MaximumComplementarityResidual = Math.Max(report.MaximumComplementarityResidual, complementarity);
            return false;
        }

        private static bool TryActiveProjection(CrowdVector[] desired, List<Constraint> rows, CrowdParameters p, out CrowdVector[] velocity)
        {
            velocity = null;
            if (p.MaxActiveSetConstraints == 0) return false;
            var active = new List<int>();
            for (var k = 0; k < rows.Count; k++)
                if (DotRow(rows[k], desired) - rows[k].Bound <= 10 * p.VelocityToleranceMS) active.Add(k);
            var count = active.Count;
            if (count == 0 || count > p.MaxActiveSetConstraints || count > 2 * desired.Length) return false;
            // Small independent active systems (e.g. a100-person queue) avoid the O(N^2) conditioning of coordinate descent.
            // This is only a candidate: dependent rows, negative multipliers, or any failed global KKT check fall back to DCD.
            var lower = new double[count, count]; var rhs = new double[count]; var lambda = new double[count];
            for (var i = 0; i < count; i++)
            {
                var a = rows[active[i]]; rhs[i] = a.Bound - DotRow(a, desired);
                for (var j = 0; j <= i; j++)
                {
                    var b = rows[active[j]]; var incidence = (a.MovableI && b.MovableI && a.I == b.I ? 1 : 0) + (a.MovableJ && b.MovableJ && a.J == b.J ? 1 : 0)
                        - (a.MovableI && b.MovableJ && a.I == b.J ? 1 : 0) - (a.MovableJ && b.MovableI && a.J == b.I ? 1 : 0);
                    var value = incidence * CrowdVector.Dot(a.Normal, b.Normal);
                    for (var k = 0; k < j; k++) value -= lower[i, k] * lower[j, k];
                    if (i == j)
                    { if (!Finite(value) || value <= 1e-12) return false; lower[i, j] = Math.Sqrt(value); }
                    else lower[i, j] = value / lower[j, j];
                }
                for (var j = 0; j < i; j++) rhs[i] -= lower[i, j] * rhs[j];
                rhs[i] /= lower[i, i];
            }
            for (var i = count - 1; i >= 0; i--)
            {
                var value = rhs[i]; for (var j = i + 1; j < count; j++) value -= lower[j, i] * lambda[j];
                lambda[i] = value / lower[i, i]; if (!Finite(lambda[i]) || lambda[i] < 0) return false;
            }
            var candidate = desired.ToArray();
            for (var i = 0; i < count; i++)
            {
                var row = rows[active[i]]; var change = row.Normal * lambda[i];
                if (row.MovableI) candidate[row.I] += change; if (row.MovableJ) candidate[row.J] -= change;
            }
            for (var k = 0; k < rows.Count; k++)
            {
                var slack = DotRow(rows[k], candidate) - rows[k].Bound;
                if (!Finite(slack) || slack < -p.VelocityToleranceMS) return false;
            }
            for (var i = 0; i < count; i++)
                if (Math.Abs(lambda[i] * (DotRow(rows[active[i]], candidate) - rows[active[i]].Bound)) > p.ComplementarityTolerance) return false;
            for (var i = 0; i < count; i++) { var row = rows[active[i]]; row.Lambda = lambda[i]; rows[active[i]] = row; }
            velocity = candidate; return true;
        }

        private static double DotRow(Constraint row, CrowdVector[] velocity) => CrowdVector.Dot(row.Normal,
            (row.MovableI ? velocity[row.I] : new CrowdVector()) - (row.MovableJ ? velocity[row.J] : new CrowdVector()));

        private double MinimumGap(CrowdAgent[] agents, CrowdDefinition d, CrowdVector[] velocity, double h, bool prune = false, WallWorkReport work = null)
        {
            var minimum = double.PositiveInfinity;
            // Phase 1: Agent-agent pairs and periodic self-gaps
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i]; var moveA = velocity == null ? new CrowdVector() : velocity[i] * h;
                var length = lengths != null && lengths.TryGetValue(a.ContactSpaceId, out var l) ? l : d.Spaces.First(s => s.Id == a.ContactSpaceId).PeriodicLengthXM;
                if (length > 0) minimum = Math.Min(minimum, length - 2 * a.RadiusM);
                for (var j = i + 1; j < agents.Length; j++)
                {
                    var b = agents[j]; if (a.ContactSpaceId != b.ContactSpaceId) continue;
                    var moveB = velocity == null ? new CrowdVector() : velocity[j] * h;
                    if (!OverlapWindow(a.FootElevationM - b.FootElevationM,
                        CrowdVector.Dot(a.SupportGradient, moveA) - CrowdVector.Dot(b.SupportGradient, moveB),
                        -a.HeightM, b.HeightM, d.Parameters.GeometryToleranceM, out var low, out var high)) continue;
                    var relativeMove = moveA - moveB;
                    for (var image = length > 0 ? -1 : 0; image <= (length > 0 ? 1 : 0); image++)
                    {
                        var relative = a.Position - b.Position - new CrowdVector(image * length, 0);
                        var t = relativeMove.LengthSquared > 0 ? Math.Max(low, Math.Min(high, -CrowdVector.Dot(relative, relativeMove) / relativeMove.LengthSquared)) : low;
                        minimum = Math.Min(minimum, (relative + relativeMove * t).Length - a.RadiusM - b.RadiusM);
                    }
                }
            }

            // Phase 2: Static walls
            EnsureQueryBuffers(d.Walls.Length);
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i]; var moveA = velocity == null ? new CrowdVector() : velocity[i] * h;
                if (wallGrid != null && prune && Finite(minimum) && minimum < 50.0)
                {
                    var dilation = a.RadiusM + minimum + PruningRoundoffMarginM;
                    var minX = Math.Min(a.Position.X, a.Position.X + moveA.X) - dilation;
                    var maxX = Math.Max(a.Position.X, a.Position.X + moveA.X) + dilation;
                    var minY = Math.Min(a.Position.Y, a.Position.Y + moveA.Y) - dilation;
                    var maxY = Math.Max(a.Position.Y, a.Position.Y + moveA.Y) + dilation;
                    candidateWallBuffer.Clear();
                    wallStamp++;
                    if (wallStamp == int.MaxValue) { Array.Clear(wallStampBuffer, 0, wallStampBuffer.Length); wallStamp = 1; }
                    wallGrid.Query(minX, maxX, minY, maxY, candidateWallBuffer, wallStampBuffer, wallStamp);
                    candidateWallBuffer.Sort();
                    for (var ci = 0; ci < candidateWallBuffer.Count; ci++)
                    {
                        var wallIndex = candidateWallBuffer[ci];
                        var wall = d.Walls[wallIndex];
                        if (!wall.Enabled || wall.ContactSpaceId != a.ContactSpaceId) continue;
                        var low = 0.0; var high = 1.0;
                        if (wall.HasVerticalBounds && !OverlapWindow(a.FootElevationM, CrowdVector.Dot(a.SupportGradient, moveA),
                            wall.BottomElevationM - a.HeightM, wall.TopElevationM, d.Parameters.GeometryToleranceM, out low, out high)) continue;
                        if (work != null) work.SweptCandidates++;
                        var start = a.Position + moveA * low; var end = a.Position + moveA * high;
                        if (BoxSeparation(start, end, wall.A, wall.B) - a.RadiusM > minimum + PruningRoundoffMarginM)
                        { if (work != null) work.SweptDistancesPruned++; continue; }
                        minimum = Math.Min(minimum, SegmentDistance(start, end, wall.A, wall.B) - a.RadiusM);
                    }
                }
                else
                {
                    for (var wallIndex = 0; wallIndex < d.Walls.Length; wallIndex++)
                    {
                        var wall = d.Walls[wallIndex];
                        if (!wall.Enabled || wall.ContactSpaceId != a.ContactSpaceId) continue;
                        var low = 0.0; var high = 1.0;
                        if (wall.HasVerticalBounds && !OverlapWindow(a.FootElevationM, CrowdVector.Dot(a.SupportGradient, moveA),
                            wall.BottomElevationM - a.HeightM, wall.TopElevationM, d.Parameters.GeometryToleranceM, out low, out high)) continue;
                        if (work != null) work.SweptCandidates++;
                        var start = a.Position + moveA * low; var end = a.Position + moveA * high;
                        if (prune && BoxSeparation(start, end, wall.A, wall.B) - a.RadiusM > minimum + PruningRoundoffMarginM)
                        { if (work != null) work.SweptDistancesPruned++; continue; }
                        minimum = Math.Min(minimum, SegmentDistance(start, end, wall.A, wall.B) - a.RadiusM);
                    }
                }
            }
            return minimum;
        }

        private static bool OverlapWindow(double start, double change, double bottom, double top, double tolerance, out double low, out double high)
        {
            low = 0; high = 1;
            if (change == 0) return start > bottom + tolerance && start < top - tolerance;
            var a = (bottom + tolerance - start) / change; var b = (top - tolerance - start) / change;
            low = Math.Max(0, Math.Min(a, b)); high = Math.Min(1, Math.Max(a, b));
            return low < high;
        }

        private static bool Occluded(CrowdVector a, CrowdVector b, int i, int j, CrowdWall[] walls, VerticalEligibility eligible)
        {
            for (var k = 0; k < walls.Length; k++)
                if (eligible.Walls[i, k] && eligible.Walls[j, k] && SegmentsIntersect(a, b, walls[k].A, walls[k].B)) return true;
            return false;
        }
        private static CrowdVector Closest(CrowdVector p, CrowdVector a, CrowdVector b)
        { var delta = b - a; return a + delta * (delta.LengthSquared > 0 ? Clamp01(CrowdVector.Dot(p - a, delta) / delta.LengthSquared) : 0); }
        private static double SegmentDistance(CrowdVector a, CrowdVector b, CrowdVector c, CrowdVector d)
        {
            if (SegmentsIntersect(a, b, c, d)) return 0;
            return Math.Min(Math.Min((a - Closest(a, c, d)).Length, (b - Closest(b, c, d)).Length),
                Math.Min((c - Closest(c, a, b)).Length, (d - Closest(d, a, b)).Length));
        }
        private static bool SegmentsIntersect(CrowdVector a, CrowdVector b, CrowdVector c, CrowdVector d)
        {
            var ab = b - a; var cd = d - c; var cross = CrowdVector.Cross(ab, cd);
            if (Math.Abs(cross) < 1e-18)
                return (a - Closest(a, c, d)).LengthSquared < 1e-24 || (b - Closest(b, c, d)).LengthSquared < 1e-24 ||
                    (c - Closest(c, a, b)).LengthSquared < 1e-24 || (d - Closest(d, a, b)).LengthSquared < 1e-24;
            var t = CrowdVector.Cross(c - a, cd) / cross; var u = CrowdVector.Cross(c - a, ab) / cross;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        private static void Validate(CrowdSnapshot s)
        {
            var d = s.Definition;
            if (s.SchemaVersion != 2 || s.ModelVersion != Version || d == null || d.SchemaVersion != 1 || !Id(d.ProfileId) ||
                d.Parameters == null || d.Spaces == null || d.Walls == null || s.Agents == null ||
                s.Tick < 0 || s.AcceptedSubsteps < s.Tick || s.GeometryRevision < 0 || !Finite(s.ElapsedSeconds) || s.ElapsedSeconds < 0)
                throw new ArgumentException("Invalid versioned crowd snapshot.");
            var p = d.Parameters;
            if (!Positive(p.TimeGapSeconds) || !Nonnegative(p.PersonRepulsion) || !Positive(p.PersonRepulsionRangeM) ||
                !Nonnegative(p.WallRepulsion) || !Positive(p.WallRepulsionRangeM) || p.PersonRepulsion > 1000 || p.WallRepulsion > 1000 ||
                !Positive(p.MaxSubstepSeconds) || p.MaxSubstepSeconds > 1 || !Positive(p.VelocityToleranceMS) || p.VelocityToleranceMS > 1e-5 ||
                !Positive(p.ComplementarityTolerance) || p.ComplementarityTolerance > 1e-5 || !Positive(p.GeometryToleranceM) || p.GeometryToleranceM > 1e-7 ||
                p.MaxAgents < 1 || p.MaxAgents > 1000 || p.MaxProjectionIterations < 1 || p.MaxProjectionIterations > 1000000 ||
                p.MaxSubstepsPerTick < 1 || p.MaxSubstepsPerTick > 1000000 || p.MaxActiveSetConstraints < 0 || p.MaxActiveSetConstraints > 512 || s.Agents.Length > p.MaxAgents)
                throw new ArgumentException("Invalid crowd numerical/body profile.");
            if (d.Spaces.Length == 0 || d.Spaces.Length > 1000 || d.Walls.Length > 10000 ||
                d.Spaces.Any(space => space == null || !Id(space.Id) || !Nonnegative(space.PeriodicLengthXM) || space.PeriodicLengthXM > 1e6) ||
                d.Spaces.Select(space => space.Id).Distinct(StringComparer.Ordinal).Count() != d.Spaces.Length)
                throw new ArgumentException("Invalid crowd contact spaces.");
            var spaces = d.Spaces.ToDictionary(space => space.Id, StringComparer.Ordinal);
            if (d.Walls.Any(w => w == null || !Id(w.Id) || !Id(w.ContactSpaceId) || !spaces.ContainsKey(w.ContactSpaceId) ||
                spaces[w.ContactSpaceId].PeriodicLengthXM > 0 || !VectorFinite(w.A) || !VectorFinite(w.B) || (w.A - w.B).Length < 1e-8 ||
                !Finite(w.BottomElevationM) || !Finite(w.TopElevationM) || Math.Abs(w.BottomElevationM) > 1e7 || Math.Abs(w.TopElevationM) > 1e7 ||
                w.HasVerticalBounds && w.TopElevationM <= w.BottomElevationM) ||
                d.Walls.Select(w => w.Id).Distinct(StringComparer.Ordinal).Count() != d.Walls.Length)
                throw new ArgumentException("Invalid finite wall segments; periodic fixtures cannot contain walls.");
            if (s.Agents.Any(a => a == null || !Id(a.Id) || !Id(a.ContactSpaceId) || !spaces.ContainsKey(a.ContactSpaceId) ||
                !Id(a.RegionId) || !Id(a.FrameId) || !Id(a.SurfaceId) || !Positive(a.RadiusM) || a.RadiusM < .01 || a.RadiusM > 2 ||
                !Nonnegative(a.PreferredSpeedMS) || a.PreferredSpeedMS > 20 || !VectorFinite(a.Position) || !VectorFinite(a.Velocity) ||
                !Finite(a.FootElevationM) || Math.Abs(a.FootElevationM) > 1e7 || !Positive(a.HeightM) || a.HeightM < 2 * a.RadiusM || a.HeightM > 10 ||
                !VectorFinite(a.SupportGradient) || a.SupportGradient.Length > 1 || a.Pinned && a.Velocity.LengthSquared != 0 ||
                !VectorFinite(a.Goal) || !VectorFinite(a.DesiredVelocity) || a.DesiredVelocity.Length > 20 || a.PathCursor < 0 ||
                !Enum.IsDefined(typeof(CrowdIntentMode), a.IntentMode) || a.LeaderId == null || a.LeaderId.Length > 128 ||
                a.KnownClosedPortalIds == null || a.KnownClosedPortalIds.Length > 256 || a.KnownClosedPortalIds.Any(id => !Id(id)) ||
                a.KnownClosedPortalIds.Distinct(StringComparer.Ordinal).Count() != a.KnownClosedPortalIds.Length ||
                spaces[a.ContactSpaceId].PeriodicLengthXM > 0 && (a.SupportGradient.LengthSquared != 0 || a.RadiusM * 4 >= spaces[a.ContactSpaceId].PeriodicLengthXM ||
                    a.Position.X < 0 || a.Position.X >= spaces[a.ContactSpaceId].PeriodicLengthXM)) ||
                s.Agents.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != s.Agents.Length)
                throw new ArgumentException("Invalid crowd agent identity, metric pose, body, intent, or navigation state.");
        }

        private static void Canonicalize(CrowdSnapshot s)
        {
            s.Agents = s.Agents.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
            s.Definition.Spaces = s.Definition.Spaces.OrderBy(space => space.Id, StringComparer.Ordinal).ToArray();
            s.Definition.Walls = s.Definition.Walls.OrderBy(w => w.Id, StringComparer.Ordinal).ToArray();
            foreach (var w in s.Definition.Walls)
                if (w.A.X > w.B.X || w.A.X == w.B.X && w.A.Y > w.B.Y) { var a = w.A; w.A = w.B; w.B = a; }
            foreach (var a in s.Agents) Array.Sort(a.KnownClosedPortalIds, StringComparer.Ordinal);
        }

        private static string DefinitionKey(CrowdDefinition d)
        {
            // Length-prefixed strings and invariant round-trip scalars avoid delimiter/culture collisions.
            var key = new StringBuilder(); void Add(string value) { key.Append(value.Length).Append(':').Append(value); }
            void Number(double value) { Add(value.ToString("R", CultureInfo.InvariantCulture)); }
            Add(d.ProfileId); Number(d.SchemaVersion); var p = d.Parameters;
            foreach (var value in new[] { p.TimeGapSeconds, p.PersonRepulsion, p.PersonRepulsionRangeM, p.WallRepulsion, p.WallRepulsionRangeM,
                p.MaxSubstepSeconds, p.VelocityToleranceMS, p.ComplementarityTolerance, p.GeometryToleranceM,
                p.MaxAgents, p.MaxProjectionIterations, p.MaxSubstepsPerTick, p.MaxActiveSetConstraints }) Number(value);
            Number(d.Spaces.Length); foreach (var space in d.Spaces) { Add(space.Id); Number(space.PeriodicLengthXM); }
            Number(d.Walls.Length); foreach (var wall in d.Walls)
            { Add(wall.Id); Add(wall.ContactSpaceId); Number(wall.A.X); Number(wall.A.Y); Number(wall.B.X); Number(wall.B.Y);
                Number(wall.Enabled ? 1 : 0); Number(wall.HasVerticalBounds ? 1 : 0); Number(wall.BottomElevationM); Number(wall.TopElevationM); }
            return key.ToString();
        }
        private static double Wrap(double x, double length) => x - Math.Floor(x / length) * length;
        private static double Clamp01(double x) => Math.Max(0, Math.Min(1, x));
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool VectorFinite(CrowdVector v) => Finite(v.X) && Finite(v.Y) && Math.Abs(v.X) <= 1e7 && Math.Abs(v.Y) <= 1e7;
        private static bool Positive(double x) => Finite(x) && x > 0;
        private static bool Nonnegative(double x) => Finite(x) && x >= 0;
        private static bool Id(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;
    }
}
