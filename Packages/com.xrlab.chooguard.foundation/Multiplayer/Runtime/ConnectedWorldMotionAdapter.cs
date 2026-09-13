using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using ChooGuard.Foundation.Simulation;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class WorldBodySpawn
    {
        public string BodyId;
        public SpatialPose Pose;
        public double RadiusM = WorldBodyPresentation.NpcRadiusM, HeightM = WorldBodyPresentation.NpcHeightM, PreferredSpeedMS = 1.2;
        public bool Pinned;
    }
    [Serializable] public sealed class WorldMotionCommand
    {
        public string BodyId;
        // Goal uses region/frame semantics; absent goal means horizontal world-space desired velocity.
        public SpatialPose Goal;
        public Point3 DesiredVelocity;
        public bool Pinned;
    }
    [Serializable] public sealed class WorldMotionSnapshot
    {
        public int SchemaVersion = 1;
        public string ProfileId, GeometrySignature, PathCheckpoint;
        public SpatialFrame[] Frames;
        public string[] ClosedPortalIds, BodyIds;
        public SpatialPose[] Poses;
        public CrowdSnapshot Crowd;
    }
    [Serializable] public sealed class WorldMotionReport
    {
        public bool Committed;
        public string Failure = "";
        public int CoupledSteps, SupportTransitions;
        public double MinimumSweptGapM = double.PositiveInfinity;
        public double TotalMs,SetupMs,InitialBindingsMs,WallBuildMs,RebindMs,IntentSupportMs,PathMs,CoreMs,TopologyMs,
            IntermediateValidationMs,FinalNormalizationMs,FinalValidationMs,ValidationBindingsMs,ValidationColliderMs,ValidationPhysXMs;
        public long PathRequests;
        public CrowdPhaseReport CorePhases;
    }

    /// <summary>Server-only transaction over one collective crowd, actual authored support/colliders,
    /// supplied straight/level train frames and all roster bodies, including offline/pinned bodies.
    /// Capture/TryRestore let an outer fire/train/authority transaction roll back this exact boundary.</summary>
    public sealed class ConnectedWorldMotionAdapter : IDisposable
    {
        [Flags] public enum ExecutionOptimization { None=0, ComposedClock=1, PreparedClearance=2, All=3 }
        public ExecutionOptimization Optimization { get; set; } = ExecutionOptimization.All;
        private readonly ConnectedWorldGeometry geometry;
        private readonly SimulatedSpatialWorld spatial;
        private readonly string rosterKey;
        private readonly bool ownsGeometry;
        private bool disposed;
        private CrowdMotionModel model;
        private CrowdWall[] wallCache;
        private SpatialFrame[] frames;
        private HashSet<string> closed = new HashSet<string>();
        private Dictionary<string, SpatialPose> poses;
        private readonly Dictionary<string, PathState> paths = new Dictionary<string, PathState>();
        private sealed class PathState { public string Key; public Point3[] Points; public int Cursor; }
        public ConnectedWorldMotionAdapter(ConnectedWorldDefinition world, ConnectedRegionView[] views, WorldBodySpawn[] bodies, long seed = 1)
            : this(ConnectedWorldGeometry.Capture(world, views), bodies, seed) { ownsGeometry = true; }
        public ConnectedWorldMotionAdapter(ConnectedWorldGeometry geometry, WorldBodySpawn[] bodies, long seed = 1)
        {
            this.geometry = geometry ?? throw new ArgumentNullException(nameof(geometry)); spatial = new SimulatedSpatialWorld(geometry.Definition);
            frames = geometry.Definition.Frames.Select(f => f.Copy()).ToArray();
            geometry.ApplyFrames(frames, closed);
            if (bodies == null || bodies.Length == 0 || bodies.Length > 120 || bodies.Any(b => b == null || b.Pose == null || string.IsNullOrEmpty(b.BodyId)) || bodies.Select(b => b.BodyId).Distinct().Count() != bodies.Length)
                throw new ArgumentException("Motion requires a complete unique roster of 1 to 120 bodies.");
            var current = spatial.At(frames); poses = new Dictionary<string, SpatialPose>();
            var agents = bodies.Select(b =>
            {
                if (!spatial.TryLocate(current, b.Pose, b.Pose.Position, (float)b.RadiusM, closed, out var pose)) throw new ArgumentException("Invalid initial region/frame pose " + b.BodyId);
                var p = b.Pose.Position; var surface = geometry.FindSupport(p.X, p.Z, p.Y, frames, new CrowdVector());
                if (surface == null) throw new ArgumentException("No authored support for " + b.BodyId);
                var delta = geometry.Delta(surface.FrameId, frames); var y = surface.Elevation(p.X - delta.X, p.Z - delta.Z);
                pose.Position.Y = (float)y; pose.LocalPosition = current.Frame(pose.FrameId).ToLocal(pose.Position); poses.Add(b.BodyId, pose);
                return new CrowdAgent { Id = b.BodyId, ContactSpaceId = "world", RegionId = pose.RegionId, FrameId = pose.FrameId, SurfaceId = surface.Id,
                    RadiusM = b.RadiusM, HeightM = b.HeightM, PreferredSpeedMS = b.PreferredSpeedMS, Position = new CrowdVector(p.X, p.Z),
                    FootElevationM = y, SupportGradient = surface.Gradient, Pinned = b.Pinned };
            }).ToArray();
            var definition = new CrowdDefinition { ProfileId = "world-motion/" + geometry.Definition.ProfileId,
                Parameters = new CrowdParameters { MaxAgents = 120 }, Spaces = frames.Select(f => new CrowdSpace { Id = f.FrameId }).ToArray(), Walls = geometry.Walls(frames, closed) };
            wallCache = definition.Walls;
            model = new CrowdMotionModel(definition, agents, seed); rosterKey = RosterKey(agents);
            if (!ValidateBodies(out var failure)) throw new ArgumentException(failure);
        }
        public IReadOnlyDictionary<string, SpatialPose> BodyPoses => poses.ToDictionary(p => p.Key, p => Copy(p.Value));
        public SpatialPose Pose(string bodyId) => Copy(poses[bodyId]);
        public CrowdSnapshot ExportCrowdSnapshot() => model.ExportSnapshot();
        public CrowdMotionModel.WallOptimization CrowdOptimization { get => model.Optimization; set => model.Optimization=value; }
        public bool ProfilePhases { get; set; }
        private long PhaseStamp() => ProfilePhases?System.Diagnostics.Stopwatch.GetTimestamp():0;
        private static double Since(long started) => (System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public string ExportCrowdCheckpoint() => model.ExportCheckpoint();
        public CrowdCheckpointProof ExportCrowdCheckpointProof() => model.ExportCheckpointProof();
        public WorldMotionSnapshot Capture() => new WorldMotionSnapshot { ProfileId = geometry.Definition.ProfileId, GeometrySignature = geometry.Signature, PathCheckpoint = EncodePaths(),
            Frames = frames.Select(f => f.Copy()).ToArray(), ClosedPortalIds = closed.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            BodyIds = poses.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(), Poses = poses.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Copy(p.Value)).ToArray(), Crowd = model.ExportSnapshot() };
        public string ExportCheckpoint() => WorldMotionCheckpoint.Encode(Capture(), model.ExportCheckpoint());
        public bool TryRestoreCheckpoint(string checkpoint, out string failure)
        {
            try { return TryRestore(WorldMotionCheckpoint.Decode(checkpoint), out failure); }
            catch (ArgumentException e) { failure = e.Message; return false; }
        }
        public bool TryRestore(WorldMotionSnapshot snapshot, out string failure)
        {
            failure = "";
            try
            {
                if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.ProfileId != geometry.Definition.ProfileId || snapshot.GeometrySignature != geometry.Signature || snapshot.Crowd == null ||
                    snapshot.Crowd.Seed != model.ReadState().Seed || RosterKey(snapshot.Crowd.Agents) != rosterKey || snapshot.Poses == null || snapshot.BodyIds == null || snapshot.Poses.Length != poses.Count ||
                    !snapshot.BodyIds.SequenceEqual(poses.Keys.OrderBy(x => x, StringComparer.Ordinal))) throw new ArgumentException("Foreign motion source, profile, seed, or roster.");
                if (!geometry.ValidateSourceGeometry(out failure)) return false;
                var restoredPaths = DecodePaths(snapshot.PathCheckpoint);
                var candidateFrames = ValidateFrames(snapshot.Frames); var candidateClosed = ValidateClosed(snapshot.ClosedPortalIds);
                var candidate = CrowdMotionModel.FromSnapshot(snapshot.Crowd);
                candidate.Optimization=model.Optimization;
                var live = model.ExportSnapshot();
                if (snapshot.Crowd.GeometryRevision == live.GeometryRevision && !CrowdMotionModel.FromSnapshot(live).TryRestore(snapshot.Crowd, out failure)) return false;
                var expected = geometry.Walls(candidateFrames, candidateClosed).OrderBy(w => w.Id, StringComparer.Ordinal).ToArray();
                var expectedState = candidate.ExportSnapshot();
                expectedState.Definition = new CrowdDefinition { ProfileId = live.Definition.ProfileId, Parameters = live.Definition.Parameters.Copy(),
                    Spaces = candidateFrames.Select(f => new CrowdSpace { Id = f.FrameId }).ToArray(), Walls = expected };
                if (CrowdMotionModel.FromSnapshot(expectedState).ExportCheckpoint() != candidate.ExportCheckpoint())
                    throw new ArgumentException("Checkpoint contact geometry differs from supplied authored frames.");
                var candidatePoses = snapshot.BodyIds.Select((id, i) => new { id, pose = Copy(snapshot.Poses[i]) }).ToDictionary(x => x.id, x => x.pose);
                geometry.ApplyFrames(candidateFrames, candidateClosed);
                if (!Validate(candidate.ReadState(), candidateFrames, candidateClosed, candidatePoses, null, out failure)) { geometry.ApplyFrames(frames, closed); return false; }
                wallCache = expected; model = candidate; frames = candidateFrames; closed = candidateClosed; poses = candidatePoses; paths.Clear(); foreach (var path in restoredPaths) paths.Add(path.Key, path.Value); return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NullReferenceException || e is FormatException || e is IOException || e is KeyNotFoundException)
            { geometry.ApplyFrames(frames, closed); failure = e.Message; return false; }
        }
        public bool TryApplyFrames(SpatialFrame[] targetFrames, string[] closedPortalIds, out string failure)
        {
            var ok = Transaction(0, Array.Empty<WorldMotionCommand>(), targetFrames, closedPortalIds, out var report); failure = report.Failure; return ok;
        }
        public bool TryAdvance(double seconds, WorldMotionCommand[] commands, out WorldMotionReport report) => TryAdvance(seconds, commands, frames, closed.ToArray(), out report);
        public bool TryAdvance(double seconds, WorldMotionCommand[] commands, SpatialFrame[] targetFrames, string[] closedPortalIds, out WorldMotionReport report)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 || seconds > 1) { report = new WorldMotionReport { Failure = "World motion tick must be in (0,1] seconds." }; return false; }
            return Transaction(seconds, commands, targetFrames, closedPortalIds, out report);
        }
        private bool Transaction(double seconds, WorldMotionCommand[] commands, SpatialFrame[] targetFrames, string[] closedPortalIds, out WorldMotionReport report)
        {
            report = new WorldMotionReport();
            var totalStarted=PhaseStamp();var phaseStarted=totalStarted;
            model.ProfilePhases=ProfilePhases;
            if(ProfilePhases)report.CorePhases=new CrowdPhaseReport();
            var previousPaths = paths.ToDictionary(p => p.Key, p => new PathState { Key = p.Value.Key, Cursor = p.Value.Cursor, Points = p.Value.Points.ToArray() });
            try
            {
                var nextFrames = ValidateFrames(targetFrames); var nextClosed = ValidateClosed(closedPortalIds);
                if (commands == null || commands.Any(c => c == null || !poses.ContainsKey(c.BodyId) || !c.DesiredVelocity.Finite || Math.Abs(c.DesiredVelocity.Y) > .001 ||
                    c.DesiredVelocity.DistanceSquared(new Point3()) > 400 || c.Goal != null && (!c.Goal.Position.Finite || !geometry.Definition.Regions.Any(r => r.Id == c.Goal.RegionId))) || commands.Select(c => c.BodyId).Distinct().Count() != commands.Length)
                    throw new ArgumentException("Invalid or duplicate world motion command.");
                foreach (var p in geometry.Definition.Portals)
                {
                    var changedToClosed = nextClosed.Contains(p.Id) && !closed.Contains(p.Id);
                    var frameChanged = p.StaticBoarding && new[] { p.From, p.To }.Select(id => geometry.Definition.Region(id).FrameId).Any(id => frames.Single(f => f.FrameId == id).Origin.DistanceSquared(nextFrames.Single(f => f.FrameId == id).Origin) > 0);
                    // Establishing a separated frame requires the whole bridge to be clear. Once the
                    // car is detached behind an existing closure, station-side bodies must still be
                    // able to move while it continues. The train controller checks each new departure.
                    var departing = frameChanged && (!closed.Contains(p.Id) || new[] { p.From, p.To }.Select(id => geometry.Definition.Region(id).FrameId)
                        .Where(id => id != "world").Any(id => frames.Single(f => f.FrameId == id).Origin.DistanceSquared(geometry.Definition.Frame(id).Origin) == 0));
                    if ((changedToClosed || departing) && !CanClosePortal(p.Id)) throw new ArgumentException("Occupied portal prevents closure/departure: " + p.Id);
                }
                var previous = model.ReadState(); var candidate = model.Fork();
                var composedClock=(Optimization & ExecutionOptimization.ComposedClock)!=0 && seconds>1e-10 ? candidate.BeginComposedTick() : null;
                var nextPoses = poses.ToDictionary(p => p.Key, p => Copy(p.Value));
                var commandMap = commands.ToDictionary(c => c.BodyId);
                geometry.ApplyFrames(nextFrames, nextClosed);
                if(ProfilePhases)report.SetupMs=Since(phaseStarted);phaseStarted=PhaseStamp();
                var state = candidate.ReadState(); var current = spatial.At(nextFrames);
                var bindings = state.Agents.Select(a =>
                {
                    var binding = CrowdBodyBinding.FromAgent(a); var wp = WorldPosition(a, frames);
                    if (a.FrameId != "world") { var before = frames.Single(f => f.FrameId == a.FrameId); var after = nextFrames.Single(f => f.FrameId == a.FrameId); wp.X += after.Origin.X - before.Origin.X; wp.Y += after.Origin.Z - before.Origin.Z; }
                    binding.ContactSpaceId = geometry.Space(a.FrameId, nextFrames, nextClosed); binding.Position = ConnectedWorldGeometry.ToContact(wp, binding.ContactSpaceId, nextFrames);
                    binding.FootElevationM = WorldY(a, frames) - (binding.ContactSpaceId == "world" ? 0 : nextFrames.Single(f => f.FrameId == binding.ContactSpaceId).Origin.Y);
                    var velocityWorld = DirectionToWorld(a.Velocity, a.ContactSpaceId, frames); binding.Velocity = DirectionToContact(velocityWorld, binding.ContactSpaceId, nextFrames);
                    binding.DesiredVelocity = new CrowdVector(); binding.Goal = binding.Position;
                    if (commandMap.TryGetValue(a.Id, out var command)) binding.Pinned = command.Pinned;
                    var surface = geometry.FindSupport(wp.X, wp.Y, WorldY(a, frames), nextFrames, new CrowdVector(), a.SurfaceId);
                    if (surface == null) throw new ArgumentException("Frame loses body support " + a.Id);
                    binding.SurfaceId = surface.Id; binding.SupportGradient = DirectionToContact(surface.Gradient, binding.ContactSpaceId, nextFrames);
                    var pose = Copy(nextPoses[a.Id]); pose.Position = new Point3((float)wp.X, (float)WorldY(a, frames), (float)wp.Y); pose.LocalPosition = current.Frame(pose.FrameId).ToLocal(pose.Position); nextPoses[a.Id] = pose;
                    return binding;
                }).ToArray();
                if(ProfilePhases)report.InitialBindingsMs=Since(phaseStarted);phaseStarted=PhaseStamp();
                var movedGeometry = !closed.SetEquals(nextClosed) || frames.Any(f => f.Origin.DistanceSquared(nextFrames.Single(n => n.FrameId == f.FrameId).Origin) > 0);
                var walls = movedGeometry ? geometry.Walls(nextFrames, nextClosed) : wallCache; var spaces = nextFrames.Select(f => new CrowdSpace { Id = f.FrameId }).ToArray();
                if(ProfilePhases)report.WallBuildMs=Since(phaseStarted);phaseStarted=PhaseStamp();
                var geometryChanged = movedGeometry ||
                    bindings.Where((b, i) => b.Pinned != state.Agents[i].Pinned || b.SurfaceId != state.Agents[i].SurfaceId || b.ContactSpaceId != state.Agents[i].ContactSpaceId).Any();
                var rebindFailure = "";
                if (geometryChanged && !candidate.TryRebind(new CrowdRebind { ExpectedTick = state.Tick, ExpectedGeometryRevision = state.GeometryRevision, Spaces = spaces, Walls = walls, Bodies = bindings }, out rebindFailure)) throw new ArgumentException(rebindFailure);
                if(ProfilePhases)report.RebindMs+=Since(phaseStarted);
                var remaining = seconds;
                for (var step = 0; remaining > 1e-10; step++)
                {
                    if (step >= 4096) throw new ArgumentException("Support transition budget exceeded.");
                    var h = remaining;
                    phaseStarted=PhaseStamp();
                    var before = candidate.ReadState(); var intents = new List<CrowdIntent>(); var windows = new List<CrowdMovementWindow>(); var changed = false;
                    bindings = before.Agents.Select(a => CrowdBodyBinding.FromAgent(a)).ToArray();
                    for (var i = 0; i < before.Agents.Length; i++)
                    {
                        var a = before.Agents[i]; var wp = WorldPosition(a, nextFrames); var wish = new CrowdVector();
                        if (commandMap.TryGetValue(a.Id, out var command) && !a.Pinned)
                        {
                            if (command.Goal != null)
                            {
                                var pathStarted=PhaseStamp();
                                if (NextGoal(a.Id, nextPoses[a.Id], command.Goal, a.RadiusM, a.HeightM, nextFrames, nextClosed, out var goal)) wish = new CrowdVector(goal.X - wp.X, goal.Z - wp.Y);
                                if(ProfilePhases){report.PathMs+=Since(pathStarted);report.PathRequests++;}
                                if (wish.Length > a.PreferredSpeedMS * h) wish = wish / wish.Length * a.PreferredSpeedMS; else wish /= h;
                            }
                            else wish = new CrowdVector(command.DesiredVelocity.X, command.DesiredVelocity.Z);
                        }
                        var surface = geometry.FindSupport(wp.X, wp.Y, WorldY(a, nextFrames), nextFrames, wish.Length > 0 ? wish : DirectionToWorld(a.Velocity, a.ContactSpaceId, nextFrames), a.SurfaceId);
                        if (surface == null) throw new ArgumentException("Unsupported body " + a.Id);
                        if (surface.Id != a.SurfaceId)
                        {
                            var delta = geometry.Delta(surface.FrameId, nextFrames);
                            if (Math.Abs(surface.Elevation(wp.X - delta.X, wp.Y - delta.Z) - WorldY(a, nextFrames)) > ConnectedWorldGeometry.GeometrySlackM)
                                throw new ArgumentException("Discontinuous support plane " + a.Id + " " + a.SurfaceId + " to " + surface.Id);
                            bindings[i].SurfaceId = surface.Id; bindings[i].SupportGradient = DirectionToContact(surface.Gradient, a.ContactSpaceId, nextFrames); changed = true; report.SupportTransitions++;
                        }
                        windows.Add(Window(a, surface, nextFrames));
                        intents.Add(new CrowdIntent { AgentId = a.Id, Mode = CrowdIntentMode.DesiredVelocity, DesiredVelocity = DirectionToContact(wish, a.ContactSpaceId, nextFrames) });
                    }
                    // Away from a hinge the core performs its own .01 s contact substeps without
                    // copying Unity geometry for every substep. Near any edge, a conservative bound
                    // on the whole projected velocity vector limits this affine-support interval.
                    var speedBound = Math.Sqrt(before.Agents.Where(a => !a.Pinned).Sum(a => a.PreferredSpeedMS * a.PreferredSpeedMS));
                    if (speedBound > 0)
                        for (var i = 0; i < before.Agents.Length; i++)
                        {
                            if (before.Agents[i].Pinned) continue;
                            var at = before.Agents[i].Position; var w = windows[i];
                            foreach (var distance in new[] { at.X - w.MinX, w.MaxX - at.X, at.Y - w.MinY, w.MaxY - at.Y })
                                if (distance > 1e-7) h = Math.Min(h, Math.Max(.0001, distance / speedBound));
                        }
                    if(ProfilePhases)report.IntentSupportMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    if (changed && !candidate.TryRebind(new CrowdRebind { ExpectedTick = before.Tick, ExpectedGeometryRevision = before.GeometryRevision, Spaces = spaces, Walls = walls, Bodies = bindings }, out rebindFailure)) throw new ArgumentException(rebindFailure);
                    if(ProfilePhases)report.RebindMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    if (!candidate.TryAdvance(h, intents.ToArray(), out var crowdReport, windows.ToArray())) throw new ArgumentException(crowdReport.Failure);
                    if(ProfilePhases){report.CoreMs+=Since(phaseStarted);report.CorePhases.Add(candidate.Phases);}phaseStarted=PhaseStamp();
                    remaining = Math.Max(0, remaining - h);
                    report.CoupledSteps++; report.MinimumSweptGapM = Math.Min(report.MinimumSweptGapM, crowdReport.MinimumSweptGapM);
                    var after = candidate.ReadState();
                    foreach (var a in after.Agents)
                    {
                        var wp = WorldPosition(a, nextFrames); var point = new Point3((float)wp.X, (float)WorldY(a, nextFrames), (float)wp.Y);
                        if (!spatial.TryLocate(current, nextPoses[a.Id], point, (float)a.RadiusM, nextClosed, out var pose)) throw new ArgumentException("Body leaves authored topology " + a.Id);
                        nextPoses[a.Id] = pose;
                    }
                    if(ProfilePhases)report.TopologyMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    if (!Validate(after, nextFrames, nextClosed, nextPoses, before, out var validationFailure,ProfilePhases?report:null)) throw new ArgumentException(validationFailure);
                    if(ProfilePhases)report.IntermediateValidationMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                    // Region and whole-body frame transitions preserve the same double physical point.
                    bindings = after.Agents.Select(a =>
                    {
                        var b = CrowdBodyBinding.FromAgent(a); var pose = nextPoses[a.Id]; b.RegionId = pose.RegionId; b.FrameId = pose.FrameId;
                        var space = geometry.Space(pose.FrameId, nextFrames, nextClosed);
                        if (space != b.ContactSpaceId) throw new ArgumentException("A closed/moving frame cannot be boarded.");
                        return b;
                    }).ToArray();
                    if (bindings.Where((b, i) => b.RegionId != after.Agents[i].RegionId || b.FrameId != after.Agents[i].FrameId).Any() &&
                        !candidate.TryRebind(new CrowdRebind { ExpectedTick = after.Tick, ExpectedGeometryRevision = after.GeometryRevision, Spaces = spaces, Walls = walls, Bodies = bindings }, out rebindFailure)) throw new ArgumentException(rebindFailure);
                    if(ProfilePhases)report.RebindMs+=Since(phaseStarted);
                }
                phaseStarted=PhaseStamp();
                if(composedClock!=null)composedClock.Complete();
                else if (candidate.ReadState().Tick != previous.Tick + (seconds > 0 ? 1 : 0))
                { var committed = candidate.ExportSnapshot(); committed.Tick = previous.Tick + (seconds > 0 ? 1 : 0); var optimization=candidate.Optimization;
                    candidate = CrowdMotionModel.FromSnapshot(committed); candidate.Optimization=optimization;candidate.ProfilePhases=ProfilePhases; }
                if(ProfilePhases)report.FinalNormalizationMs=Since(phaseStarted);phaseStarted=PhaseStamp();
                if (!Validate(candidate.ReadState(), nextFrames, nextClosed, nextPoses, null, out var finalFailure,ProfilePhases?report:null)) throw new ArgumentException(finalFailure);
                if(ProfilePhases)report.FinalValidationMs=Since(phaseStarted);
                wallCache = walls; model = candidate; frames = nextFrames; closed = nextClosed; poses = nextPoses;
                report.Committed = true;if(ProfilePhases)report.TotalMs=Since(totalStarted); if (double.IsPositiveInfinity(report.MinimumSweptGapM)) report.MinimumSweptGapM = 0; return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            { geometry.ApplyFrames(frames, closed); paths.Clear(); foreach (var p in previousPaths) paths.Add(p.Key, p.Value); report.Failure = e.Message; return false; }
        }
        private CrowdMovementWindow Window(CrowdAgent a, ConnectedWorldGeometry.Support s, SpatialFrame[] currentFrames)
        {
            var d = geometry.Delta(s.FrameId, currentFrames);
            var p = ConnectedWorldGeometry.ToContact(new CrowdVector(s.MinX + d.X, s.MinZ + d.Z), a.ContactSpaceId, currentFrames);
            var q = ConnectedWorldGeometry.ToContact(new CrowdVector(s.MaxX + d.X, s.MaxZ + d.Z), a.ContactSpaceId, currentFrames);
            return new CrowdMovementWindow { AgentId = a.Id, MinX = Math.Min(p.X, q.X), MaxX = Math.Max(p.X, q.X), MinY = Math.Min(p.Y, q.Y), MaxY = Math.Max(p.Y, q.Y) };
        }
        public bool BoardingOccupied(string portalId)
        {
            var p = geometry.Definition.Portals.Single(p => p.Id == portalId);
            return model.ReadState().Agents.Any(a => { var v = WorldPosition(a, frames); return SimulatedSpatialWorld.OccupiesBoardingBridge(p, new Point3((float)v.X, (float)WorldY(a, frames), (float)v.Y), (float)a.RadiusM + SimulatedSpatialWorld.BoardingClearanceM, (float)a.HeightM); });
        }
        public bool CanClosePortal(string portalId)
        {
            var p = geometry.Definition.Portals.Single(p => p.Id == portalId); if (p.StaticBoarding) return !BoardingOccupied(portalId);
            var d = new CrowdVector(p.ToPoint.X - p.FromPoint.X, p.ToPoint.Z - p.FromPoint.Z); d /= d.Length;
            return !model.ReadState().Agents.Any(a => { var v = WorldPosition(a, frames) - new CrowdVector(p.ClosurePoint.X, p.ClosurePoint.Z); var y = WorldY(a, frames);
                return Math.Abs(CrowdVector.Dot(v, d)) <= a.RadiusM + .095 && Math.Abs(CrowdVector.Cross(v, d)) <= p.ClearWidth / 2 + a.RadiusM && y < p.ClosurePoint.Y + p.ClosureHeight && y + a.HeightM > p.ClosurePoint.Y; });
        }
        public bool ValidateBodies(out string failure) => Validate(model.ReadState(), frames, closed, poses, null, out failure);
        private bool Validate(CrowdReadState state, SpatialFrame[] currentFrames, ISet<string> currentClosed, Dictionary<string, SpatialPose> currentPoses, CrowdReadState before, out string failure, WorldMotionReport timing=null)
        {
            failure = "";
            var world = spatial.At(currentFrames);
            var clearance=(Optimization & ExecutionOptimization.PreparedClearance)!=0?geometry.PrepareClearance(currentFrames,currentClosed):null;
            foreach (var a in state.Agents)
            {
                var phaseStarted=PhaseStamp();
                var wp = WorldPosition(a, currentFrames); var y = WorldY(a, currentFrames); var point = new Vector3((float)wp.X, (float)y, (float)wp.Y);
                var support = geometry.Supports.SingleOrDefault(s => s.Id == a.SurfaceId); var pose = currentPoses[a.Id];
                if (support == null) { failure = "Unknown body support " + a.Id; return false; }
                if (!world.Regions.Any(r => r.Id == pose.RegionId && r.FrameId == pose.FrameId) ||
                    !string.IsNullOrEmpty(pose.PortalId) && !world.Portals.Any(p => p.Id == pose.PortalId && (p.From == pose.RegionId || p.To == pose.RegionId)) ||
                    before == null && (pose.RegionId != a.RegionId || pose.FrameId != a.FrameId) ||
                    !spatial.TryLocate(world, pose, pose.Position, (float)a.RadiusM, currentClosed, out var located) || located.RegionId != pose.RegionId || located.FrameId != pose.FrameId)
                { failure = "Body pose topology mismatch " + a.Id; return false; }
                var d = geometry.Delta(support.FrameId, currentFrames); var gradient = DirectionToContact(support.Gradient, a.ContactSpaceId, currentFrames);
                if (!support.Contains(wp.X - d.X, wp.Y - d.Z) || Math.Abs(support.Elevation(wp.X - d.X, wp.Y - d.Z) - y) > ConnectedWorldGeometry.GeometrySlackM ||
                    (gradient - a.SupportGradient).Length > 1e-7 || a.ContactSpaceId != geometry.Space(pose.FrameId, currentFrames, currentClosed) ||
                    pose.Position.DistanceSquared(new Point3(point.x, point.y, point.z)) > 1e-8 || currentFrames.Single(f => f.FrameId == pose.FrameId).ToWorld(pose.LocalPosition).DistanceSquared(pose.Position) > 1e-7)
                { failure = "Body support/frame binding mismatch " + a.Id; return false; }
                if(timing!=null)timing.ValidationBindingsMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                if (!(clearance!=null?clearance.IsClear(wp.X,y,wp.Y,a.RadiusM,a.HeightM):geometry.IsClear(wp.X, y, wp.Y, a.RadiusM, a.HeightM, currentFrames, currentClosed))) { failure = "Body intersects authored collider " + a.Id; return false; }
                if(timing!=null)timing.ValidationColliderMs+=Since(phaseStarted);phaseStarted=PhaseStamp();
                var from = point;
                if (before != null) { var b = before.Agents.Single(b => b.Id == a.Id); var p = WorldPosition(b, currentFrames); from = new Vector3((float)p.X, (float)WorldY(b, currentFrames), (float)p.Y); }
                var offset = point - from; offset.y = 0;
                if (!GroundedWorldMotor.TryGroundedStep(from, offset, out var grounded, (float)a.RadiusM, (float)a.HeightM) || Math.Abs(grounded.y - point.y) > .001)
                { failure = "Grounded candidate rejected " + a.Id + ": " + GroundedWorldMotor.LastFailure + " at " + point; return false; }
                if(timing!=null)timing.ValidationPhysXMs+=Since(phaseStarted);
            }
            return true;
        }
        public bool TryPlanPath(string bodyId, SpatialPose goal, out Point3[] path)
        {
            var a = model.ReadState().Agents.Single(a => a.Id == bodyId);
            return Plan(poses[bodyId], goal, a.RadiusM, a.HeightM, frames, closed, out path);
        }
        private bool Plan(SpatialPose from, SpatialPose goal, double radius, double height, SpatialFrame[] currentFrames, ISet<string> currentClosed, out Point3[] result)
        {
            result = Array.Empty<Point3>(); var blocked = new HashSet<string>(currentClosed);
            foreach (var p in geometry.Definition.Portals.Where(p => p.StaticBoarding)) if (geometry.Space(geometry.Definition.Region(geometry.Definition.Region(p.From).FrameId == "world" ? p.To : p.From).FrameId, currentFrames, currentClosed) != "world") blocked.Add(p.Id);
            if (geometry.Definition.Route(from.RegionId, goal.RegionId, blocked).Length == 0) return false;
            Point3 Authored(SpatialPose p) { var delta = geometry.Delta(p.FrameId, currentFrames); return new Point3(p.Position.X - delta.X, p.Position.Y, p.Position.Z - delta.Z); }
            if (HorizontalDistance(from.Position, goal.Position) < .001) { result = new[] { from.Position, goal.Position }; return true; }
            if (!geometry.TryPath(Authored(from), Authored(goal), radius, height, out var authored)) return false;
            result = authored.Select(p =>
            {
                var frame = geometry.Definition.Regions.FirstOrDefault(r => r.FrameId != "world" && r.Contains(p))?.FrameId ?? "world";
                var delta = geometry.Delta(frame, currentFrames); return new Point3(p.X + delta.X, p.Y, p.Z + delta.Z);
            }).ToArray(); return true;
        }
        private bool NextGoal(string id, SpatialPose from, SpatialPose goal, double radius, double height, SpatialFrame[] currentFrames, ISet<string> currentClosed, out Point3 point)
        {
            var key = JsonUtility.ToJson(goal) + "|" + string.Join(",", currentClosed.OrderBy(x => x,StringComparer.Ordinal)) + "|" + string.Join(",", currentFrames
                .Where(f=>f.FrameId==from.FrameId || f.FrameId==goal.FrameId).OrderBy(f => f.FrameId, StringComparer.Ordinal)
                .Select(f => f.FrameId + ":" + f.Origin.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "/" + f.Origin.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            point = from.Position;
            if (!paths.TryGetValue(id, out var path) || path.Key != key)
            { if (!Plan(from, goal, radius, height, currentFrames, currentClosed, out var points)) return false; paths[id] = path = new PathState { Key = key, Points = points, Cursor = 1 }; }
            while (path.Cursor < path.Points.Length - 1 && HorizontalDistance(from.Position, path.Points[path.Cursor]) < .08) path.Cursor++;
            if (path.Points.Length == 0) return false;
            path.Cursor = Math.Max(0, Math.Min(path.Cursor, path.Points.Length - 1));
            point = path.Points[path.Cursor]; return true;
        }
        private string EncodePaths()
        {
            using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            writer.Write("CGMP1"); writer.Write(paths.Count);
            foreach (var entry in paths.OrderBy(p => p.Key, StringComparer.Ordinal))
            { writer.Write(entry.Key); writer.Write(entry.Value.Key); writer.Write(entry.Value.Cursor); writer.Write(entry.Value.Points.Length);
                foreach (var p in entry.Value.Points) { writer.Write(p.X); writer.Write(p.Y); writer.Write(p.Z); } }
            writer.Flush(); return Convert.ToBase64String(bytes.ToArray());
        }
        private Dictionary<string, PathState> DecodePaths(string checkpoint)
        {
            if (checkpoint == null || checkpoint.Length > 3 * 1024 * 1024) throw new ArgumentException("Invalid navigation checkpoint.");
            using var bytes = new MemoryStream(Convert.FromBase64String(checkpoint)); using var reader = new BinaryReader(bytes, Encoding.UTF8);
            if (ReadPathText(reader, 5) != "CGMP1") throw new ArgumentException("Unsupported navigation checkpoint.");
            var count = reader.ReadInt32(); if (count < 0 || count > poses.Count) throw new ArgumentException("Invalid path roster.");
            var result = new Dictionary<string, PathState>();
            for (var i = 0; i < count; i++)
            {
                var id = ReadPathText(reader, 512); var key = ReadPathText(reader, 65536); var cursor = reader.ReadInt32(); var length = reader.ReadInt32();
                if (!poses.ContainsKey(id) || result.ContainsKey(id) || key.Length > 16384 || length < 1 || length > 8192 || cursor < 0 || cursor >= length) throw new ArgumentException("Invalid path state.");
                var points = new Point3[length]; for (var j = 0; j < length; j++) { points[j] = new Point3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); if (!points[j].Finite) throw new ArgumentException("Nonfinite path."); }
                result.Add(id, new PathState { Key = key, Cursor = cursor, Points = points });
            }
            if (bytes.Position != bytes.Length) throw new ArgumentException("Trailing navigation checkpoint bytes."); return result;
        }
        private static string ReadPathText(BinaryReader reader, int maximumBytes)
        {
            uint length = 0; var shift = 0;
            for (var i = 0; i < 5; i++)
            {
                var next = reader.ReadByte(); if (i == 4 && next > 7) throw new ArgumentException("Invalid navigation string prefix.");
                length |= (uint)(next & 127) << shift;
                if (length > maximumBytes) throw new ArgumentException("Navigation string exceeds its byte bound.");
                if ((next & 128) == 0)
                {
                    if (i > 0 && next == 0 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new ArgumentException("Truncated/noncanonical navigation string.");
                    return new UTF8Encoding(false, true).GetString(reader.ReadBytes((int)length));
                }
                shift += 7;
            }
            throw new ArgumentException("Invalid navigation string prefix.");
        }
        private SpatialFrame[] ValidateFrames(SpatialFrame[] candidate) { spatial.At(candidate); return candidate.Select(f => f.Copy()).ToArray(); }
        private HashSet<string> ValidateClosed(string[] value)
        {
            if (value == null || value.Any(id => !geometry.Definition.Portals.Any(p => p.Id == id)) || value.Distinct().Count() != value.Length) throw new ArgumentException("Invalid closed portal set.");
            return new HashSet<string>(value);
        }
        private static string RosterKey(CrowdAgent[] bodies) => string.Join("|", bodies.OrderBy(a => a.Id, StringComparer.Ordinal).Select(a => a.Id + ":" + a.RadiusM.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + a.HeightM.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + a.PreferredSpeedMS.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        private static CrowdVector WorldPosition(CrowdAgent a, SpatialFrame[] currentFrames) => ConnectedWorldGeometry.ToWorld(a.Position, a.ContactSpaceId, currentFrames);
        private static double WorldY(CrowdAgent a, SpatialFrame[] currentFrames) => a.FootElevationM + (a.ContactSpaceId == "world" ? 0 : currentFrames.Single(f => f.FrameId == a.ContactSpaceId).Origin.Y);
        private static CrowdVector DirectionToWorld(CrowdVector v, string space, SpatialFrame[] currentFrames) => ConnectedWorldGeometry.ToWorld(v, space, currentFrames) - ConnectedWorldGeometry.ToWorld(new CrowdVector(), space, currentFrames);
        private static CrowdVector DirectionToContact(CrowdVector v, string space, SpatialFrame[] currentFrames) => ConnectedWorldGeometry.ToContact(v, space, currentFrames) - ConnectedWorldGeometry.ToContact(new CrowdVector(), space, currentFrames);
        private static SpatialPose Copy(SpatialPose p) => new SpatialPose { RegionId = p.RegionId, FrameId = p.FrameId, PortalId = p.PortalId, Position = p.Position, LocalPosition = p.LocalPosition };
        private static double HorizontalDistance(Point3 a, Point3 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
        public void Dispose()
        {
            if (disposed) return;
            try { geometry.RestoreSurvivingSceneViews(); }
            finally { if (ownsGeometry) geometry.Dispose(); paths.Clear(); disposed = true; }
        }
    }
}
