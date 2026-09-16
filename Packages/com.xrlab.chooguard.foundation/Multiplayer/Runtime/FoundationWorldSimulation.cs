using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Simulation;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class ServerMovementInput
    {
        public string ParticipantId;
        public Point3 Velocity;
        public bool Enabled;
        public string[] LoadedRegions = Array.Empty<string>();
    }
    [Serializable] public sealed class SimulationTickReport
    {
        public string Failure = "";
        public long Tick;
        public int ActiveIncidents, NpcCount;
        public double TotalMilliseconds, MotionMilliseconds, FireMilliseconds, CheckpointMilliseconds;
        public double ReadMilliseconds, BeforeMotionMilliseconds, OpticsMilliseconds, AuthorityMilliseconds;
        public double MotionCaptureMilliseconds, CrowdEncodeMilliseconds, MotionEncodeMilliseconds;
        public int PhysicalCheckpointCharacters;
        public PhysicalCheckpointTiming EncodeTiming, DecodeTiming;
        public WorldMotionReport Motion;
        public FireStepReport Fire;
    }

    /// <summary>Server-only coupled world. Nothing in this class is a client projection or an accepted staff procedure.</summary>
    public sealed class FoundationWorldSimulation : IDisposable
    {
        public const double TickSeconds = .05;
        [Serializable] private sealed class NpcGoal
        { public string BodyId, LeaderId = ""; public SpatialPose Goal; public long RefreshTick; public int RoutineIndex; }
        [Serializable] private sealed class Auxiliary
        { public int Schema = 1; public string IncidentCheckpoint, MotionCheckpoint; public NpcGoal[] Goals; }
        private readonly ConnectedWorldDefinition authored;
        private readonly FoundationSimulationProfile profile;
        private readonly ConnectedThermalDomain thermal;
        private readonly ConnectedWorldGeometry geometry;
        private readonly ConnectedWorldMotionAdapter motion;
        private readonly WorldState initial;
        private readonly WorldBodySpawn[] spawns;
        private readonly Dictionary<string, SpatialPose[]> routines = new Dictionary<string, SpatialPose[]>();
        private ZoneFireModel fire;
        private IncidentDirector director;
        private TrainOperatingState[] trains;
        private NpcGoal[] goals;
        private HashSet<string> actualClosed = new HashSet<string>();
        private ObservedSmokeCell[] smokeCells = Array.Empty<ObservedSmokeCell>();
        private SmokeOpticalField opticalField;
        private long tick;
        public string DefinitionHash { get; }
        public long Tick => tick;
        public FoundationTrainingMode TrainingMode => profile.TrainingMode;
        /// <summary>Documented blocked-reason contract for <see cref="TryTransitionMode"/>; FMP-11b-D01.
        /// The same text is recorded in docs/context/projections/FMP-11b-transition-decision.json, which is an
        /// unapproved proposal and not a PM policy decision.</summary>
        public const string MidShiftTransitionBlockedReason = "Mid-shift mode transition is blocked without an accepted PM policy decision (FMP-11b-D01). Active shift trajectory and scoring fairness require a clean shift restart.";
        /// <summary>Mid-shift mode transition guard. A request for the mode the shift is already running is an
        /// idempotent no-op and reports success; any other mode is refused while the shift is live, and the
        /// refusal never restarts, clears or otherwise mutates the running simulation.</summary>
        public bool TryTransitionMode(FoundationTrainingMode newMode, out string blockedReason)
        {
            if (newMode == TrainingMode)
            {
                blockedReason = "";
                return true;
            }
            blockedReason = MidShiftTransitionBlockedReason;
            return false;
        }
        public WorldState InitialState => initial.Copy();
        public int ActiveIncidentCount => director.Active.Count;
        public int NpcCount => profile.NpcCount;
        public bool ProfilePhases { get; set; }
        public ConnectedWorldMotionAdapter.ExecutionOptimization MotionOptimization { get=>motion.Optimization; set=>motion.Optimization=value; }

        public FoundationWorldSimulation(ConnectedWorldDefinition world, ConnectedRegionView[] views, FoundationSimulationProfile definition, WorldState seed)
        {
            if (seed == null || seed.SchemaVersion != 2 || seed.Sequence != 0 || seed.Participants == null || seed.Participants.Length < 1 || seed.Participants.Length > 20 ||
                seed.Participants.Any(p => p == null) || seed.Participants.Select(p => p.ParticipantId).Distinct().Count() != seed.Participants.Length)
                throw new ArgumentException("Simulation requires a fresh public connected-world seed and complete admission roster.");
            authored = JsonUtility.FromJson<ConnectedWorldDefinition>(JsonUtility.ToJson(world));
            profile = JsonUtility.FromJson<FoundationSimulationProfile>(JsonUtility.ToJson(definition)); profile.Validate(authored);
            thermal = ConnectedThermalDomain.Create(authored, profile.Thermal);
            geometry = ConnectedWorldGeometry.Capture(authored, views);
            try
            {
                var bodies = new List<WorldBodySpawn>(); initial = seed.Copy();
                foreach (var p in initial.Participants)
                {
                    if (!geometry.TryFindStandingPoint(p.RegionId, p.Position, WorldBodyPresentation.PlayerRadiusM, 1.8, bodies.ToArray(), out var pose))
                        throw new ArgumentException("No supported participant spawn: " + p.ParticipantId);
                    Put(p, pose); p.InputEnabled = false;
                    bodies.Add(new WorldBodySpawn { BodyId = p.ParticipantId, Pose = pose, RadiusM = WorldBodyPresentation.PlayerRadiusM, HeightM = 1.8, PreferredSpeedMS = 3, Pinned = true });
                }
                var entities = authored.Regions.Select(r => authored.RegionEquipment(r.Id)).ToList();
                for (var i = 0; i < profile.NpcCount; i++)
                {
                    var id = "npc-" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture); var region = authored.Region(profile.NpcSpawnRegions[i % profile.NpcSpawnRegions.Length]);
                    if (bodies.Any(b => b.BodyId == id) || !geometry.TryFindStandingPoint(region.Id, region.Hub, WorldBodyPresentation.NpcRadiusM,
                        WorldBodyPresentation.NpcHeightM, bodies.ToArray(), out var pose)) throw new ArgumentException("No supported unique NPC spawn: " + id);
                    bodies.Add(new WorldBodySpawn { BodyId = id, Pose = pose });
                    entities.Add(new EntityState { EntityId = id, Kind = EntityKind.Evacuee, RegionId = pose.RegionId, FrameId = pose.FrameId, Position = pose.Position, LocalPosition = pose.LocalPosition });
                    var near = pose.Position; near.X += i % 2 == 0 ? 2 : -2;
                    if (!geometry.TryFindStandingPoint(region.Id, near, WorldBodyPresentation.NpcRadiusM, WorldBodyPresentation.NpcHeightM, Array.Empty<WorldBodySpawn>(), out var destination)) destination = pose;
                    routines[id] = new[] { Copy(pose), Copy(destination) };
                }
                foreach (var route in profile.Trains)
                {
                    var region = authored.Region(route.RegionId); var pose = authored.Pose(region.Id, region.Hub);
                    entities.Add(new EntityState { EntityId = route.Operation.TrainId, Kind = EntityKind.Train, RegionId = region.Id, FrameId = region.FrameId, Position = pose.Position, LocalPosition = pose.LocalPosition });
                }
                initial.SchemaVersion = 3; initial.Entities = entities.ToArray(); initial.Frames = authored.Frames.Select(f => f.Copy()).ToArray();
                spawns = bodies.ToArray(); motion = new ConnectedWorldMotionAdapter(geometry, spawns, profile.Seed);
                director = new IncidentDirector(profile.Schedule, (ulong)profile.Seed); fire = new ZoneFireModel(thermal.Network, options: thermal.SolverOptions);
                trains = profile.Trains.Select(t => TrainOperation.Create(t.Operation)).ToArray();
                goals = routines.Select(r => new NpcGoal { BodyId = r.Key, Goal = Copy(r.Value[1]), RoutineIndex = 1, RefreshTick = 400 }).ToArray();
                DefinitionHash = SessionAdmission.Digest("foundation-world-kernel-2\n" + JsonUtility.ToJson(authored) + "\n" + JsonUtility.ToJson(profile) + "\n" + geometry.Signature + "\n" + JsonUtility.ToJson(seed));
                initial.SimulationDefinitionHash = DefinitionHash; initial.SimulationCheckpoint = CapturePhysical();
                RefreshOptics(initial.Frames);
            }
            catch { motion?.Dispose(); geometry.Dispose(); throw; }
        }

        public bool CanOperate(EntityState target)
        {
            if (target.Kind != EntityKind.Equipment || !target.Active) return true;
            return authored.Portals.Where(p => p.LinkedDoorEntityIds.Contains(target.EntityId)).All(p => motion.CanClosePortal(p.Id));
        }

        public bool TryAdvance(AuthoritativeShift authority, ServerMovementInput[] inputs, out SimulationTickReport report)
        {
            report = new SimulationTickReport { Tick = tick, NpcCount = profile.NpcCount, ActiveIncidents = director.Active.Count };
            var timer = Stopwatch.StartNew(); var current = authority.ReadSimulation();
            report.ReadMilliseconds = timer.Elapsed.TotalMilliseconds;
            if (ProfilePhases) { report.EncodeTiming = new PhysicalCheckpointTiming(); report.DecodeTiming = new PhysicalCheckpointTiming(); }
            if (current.Paused) return true;
            var previousCheckpoint = current.Checkpoint;
            try
            {
                if (current.Tick != tick || current.DefinitionHash != DefinitionHash || inputs == null || inputs.Any(i => i == null || !i.Velocity.Finite ||
                    Math.Abs(i.Velocity.Y) > .001 || i.Velocity.DistanceSquared(new Point3()) > 9.00001 || i.LoadedRegions == null || i.LoadedRegions.Length > 13 ||
                    i.LoadedRegions.Any(id => !authored.Regions.Any(r => r.Id == id)) || !current.Participants.Any(p => p.ParticipantId == i.ParticipantId)) ||
                    inputs.Select(i => i.ParticipantId).Distinct().Count() != inputs.Length)
                    throw new ArgumentException("Invalid current server movement input/boundary.");
                var onset = director.AdvanceOne();
                var nextTrains = trains.Select(t => t.Copy()).ToArray();
                if (onset != null)
                {
                    var binding = profile.Incidents.Single(i => i.ChoiceId == onset.ChoiceId);
                    var kind = profile.Schedule.Choices.Single(c => c.Id == onset.ChoiceId).Kind;
                    if (kind == FoundationIncidentKind.TrainFault || kind == FoundationIncidentKind.EmergencyStop)
                    {
                        var route = profile.Trains.Single(t => t.Operation.TrainId == binding.TrainId);
                        TrainOperation.EmergencyStop(nextTrains.Single(t => t.TrainId == binding.TrainId), route.Operation, onset.Id);
                    }
                }
                foreach (var route in profile.Trains)
                {
                    var train = nextTrains.Single(t => t.TrainId == route.Operation.TrainId);
                    var portal = authored.Portals.Single(p => p.Id == route.PortalId);
                    var clear = motion.CanClosePortal(portal.Id);
                    TrainOperation.Advance(train, route.Operation, TickSeconds, new TrainOperatingInput { BoardingAreaClear = clear, AllowDeparture = clear,
                        DoorRequestedOpen = portal.LinkedDoorEntityIds.All(id => current.Entities.Single(e => e.EntityId == id).Active) });
                }
                var frames = Frames(nextTrains); var closed = Closures(current.Entities, nextTrains);
                var commands = new List<WorldMotionCommand>();
                foreach (var p in current.Participants)
                {
                    var input = inputs.SingleOrDefault(i => i.ParticipantId == p.ParticipantId);
                    var enabled = input != null && input.Enabled && p.InputEnabled && authored.RequiredRegions(p.RegionId).All(id => input.LoadedRegions.Contains(id));
                    commands.Add(new WorldMotionCommand { BodyId = p.ParticipantId, Pinned = !enabled, DesiredVelocity = enabled ? input.Velocity : new Point3() });
                }
                UpdateGoals(current, frames);
                foreach (var goal in goals) commands.Add(new WorldMotionCommand { BodyId = goal.BodyId, Goal = CurrentGoal(goal.Goal, frames) });
                var phase = Stopwatch.StartNew();
                report.BeforeMotionMilliseconds = timer.Elapsed.TotalMilliseconds-report.ReadMilliseconds;
                motion.ProfilePhases=ProfilePhases;
                if (!motion.TryAdvance(TickSeconds, commands.ToArray(), frames, closed.ToArray(), out var movement)) throw new InvalidOperationException(movement.Failure);
                report.Motion = movement; report.MotionMilliseconds = phase.Elapsed.TotalMilliseconds;
                phase.Restart();
                var forcing = thermal.Forcing(FireSources(), id => !closed.Contains(id), id => BoardingAligned(id, nextTrains),
                    id => ServicesRequestedOn(id,current.Entities) && !Affected(id, FoundationIncidentKind.PowerLoss));
                if (!fire.TryAdvance(TickSeconds, forcing, out var heat)) throw new InvalidOperationException(heat.Failure);
                report.Fire = heat; report.FireMilliseconds = phase.Elapsed.TotalMilliseconds;
                trains = nextTrains; tick++;
                var entities = current.Entities.Select(e => e.Copy()).ToList();
                if (onset != null)
                {
                    var binding = profile.Incidents.Single(i => i.ChoiceId == onset.ChoiceId); var region = authored.Region(binding.RegionId);
                    entities.Add(new EntityState { EntityId = onset.Id, Kind = EntityKind.Incident, RegionId = region.Id, FrameId = region.FrameId, LocalPosition = binding.LocalPosition });
                }
                var activeIds = new HashSet<string>(director.Active.Select(i => i.Id));
                foreach (var entity in entities)
                {
                    if (entity.Kind == EntityKind.Evacuee) Put(entity, motion.Pose(entity.EntityId));
                    else entity.Position = frames.Single(f => f.FrameId == entity.FrameId).ToWorld(entity.LocalPosition);
                    var active = entity.Active;
                    if (entity.Kind == EntityKind.Incident) active = activeIds.Contains(entity.EntityId);
                    if (active != entity.Active) { entity.Active = active; entity.Revision++; }
                }
                phase.Restart(); RefreshOptics(frames); report.OpticsMilliseconds = phase.Elapsed.TotalMilliseconds;
                phase.Restart(); var prepared = CapturePreparedPhysical(report); var checkpoint = prepared.Encoded; report.CheckpointMilliseconds = phase.Elapsed.TotalMilliseconds;
                report.PhysicalCheckpointCharacters = checkpoint.Length; phase.Restart();
                authority.ApplyPreparedServerSimulation(new ServerSimulationUpdate { Tick = tick, DefinitionHash = DefinitionHash, Checkpoint = checkpoint, Frames = frames,
                    Actors = current.Participants.Select(p => new ServerActorPose { ParticipantId = p.ParticipantId, Pose = motion.Pose(p.ParticipantId) }).ToArray(), Entities = entities.ToArray() }, prepared, report.DecodeTiming);
                report.AuthorityMilliseconds = phase.Elapsed.TotalMilliseconds;
                actualClosed = closed;
                report.Tick = tick; report.ActiveIncidents = director.Active.Count; report.TotalMilliseconds = timer.Elapsed.TotalMilliseconds; return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is InvalidDataException)
            {
                // Roll back every subsystem to the last published boundary; accepted actions remain owned by the authority.
                Restore(current, previousCheckpoint);
                report.Failure = exception.Message; report.TotalMilliseconds = timer.Elapsed.TotalMilliseconds; return false;
            }
        }

        public ObservedPortalState[] ProjectPortals(ParticipantState actor, Func<ConnectedPortalDefinition, bool> visible) => authored.Portals.Where(p =>
            (p.From == actor.RegionId || p.To == actor.RegionId) && p.ClosurePoint.DistanceSquared(actor.Position) <= 625 && visible(p))
            .Select(p => new ObservedPortalState { PortalId = p.Id, Open = !actualClosed.Contains(p.Id) }).ToArray();
        public WorldPhysicalView Project(ServerSimulationView current, ParticipantState actor, Func<Point3, bool> visible, IEnumerable<EntityState> observedEntities=null)
        {
            var frames = current.Frames;
            var visibleRegions = authored.Regions.Where(r =>
            {
                var point = ClosestPoint(r, frames, actor.Position);
                return point.DistanceSquared(actor.Position) <= 625 && visible(point);
            }).ToArray();
            var frameIds = new HashSet<string>(visibleRegions.Select(r => r.FrameId)) { actor.FrameId };
            foreach (var region in authored.Regions.Where(r => r.FrameId != "world"))
            {
                var exterior = ClosestPoint(region, frames, actor.Position, -.2f);
                if (exterior.DistanceSquared(actor.Position) <= 625 && visible(exterior)) frameIds.Add(region.FrameId);
            }
            var bodies=current.Participants.Where(p=>p.ParticipantId!=actor.ParticipantId && p.Position.DistanceSquared(actor.Position)<=625 &&
                visible(new Point3(p.Position.X,p.Position.Y+1,p.Position.Z))).Select(p=>new ObservedBody {ParticipantId=p.ParticipantId,TeamId=p.TeamId,RoleId=p.RoleId,
                    RegionId=p.RegionId,FrameId=p.FrameId,Position=p.Position,LocalPosition=p.LocalPosition}).ToArray();
            foreach (var body in bodies) frameIds.Add(body.FrameId);
            if (observedEntities!=null) foreach (var entity in observedEntities) frameIds.Add(entity.FrameId);
            return new WorldPhysicalView { Frames = frames.Where(f => frameIds.Contains(f.FrameId)).Select(f => f.Copy()).ToArray(),
                Bodies = bodies,
                Trains = profile.Trains.Where(t => visibleRegions.Any(r => r.Id == t.RegionId)).Select(t =>
                { var state = trains.Single(s => s.TrainId == t.Operation.TrainId); return new ObservedTrain { EntityId = state.TrainId, FrameId = t.FrameId, SignedSpeedMS = state.Motion.SignedSpeedMS, DoorOpen = state.DoorOpen }; }).ToArray(),
                Smoke = smokeCells.Where(c => c.Cell.UpperExtinctionPerM > 0 || c.Cell.LowerExtinctionPerM > 0).Where(c =>
                {
                    var cell = c.Cell; var p = ClosestCellPoint(cell, actor.Position);
                    return p.DistanceSquared(actor.Position) <= 625 && visible(p);
                }).Select(c => new ObservedSmokeCell { RegionId = c.RegionId, Cell = c.Cell.Copy(), UpperTemperatureK = c.UpperTemperatureK, LowerTemperatureK = c.LowerTemperatureK }).ToArray(),
                Regions = visibleRegions.Select(r => new ObservedRegionEffect { RegionId = r.Id,
                    LightingOn = ServicesRequestedOn(r.Id,current.Entities) && !Affected(r.Id, FoundationIncidentKind.PowerLoss),
                    PublicAddressAvailable = !Affected(r.Id, FoundationIncidentKind.PublicAddressFailure) && !Affected(r.Id, FoundationIncidentKind.PowerLoss) }).ToArray() };
        }
        private bool ServicesRequestedOn(string regionId, EntityState[] entities) => !string.IsNullOrEmpty(authored.Region(regionId).ControlledPortalId) ||
            entities.Single(e=>e.EntityId=="equipment."+regionId).Active;
        public double OpticalDepth(Point3 from, Point3 to) => opticalField.Trace(new DoubleVector3(from.X, from.Y, from.Z), new DoubleVector3(to.X, to.Y, to.Z)).OpticalDepth;
        public bool OpticallyVisible(Point3 from, Point3 to) => OpticalDepth(from, to) <= profile.ObservationOpticalDepthLimit;
        private void RefreshOptics(SpatialFrame[] frames)
        {
            var state = fire.ExportState();
            smokeCells = thermal.Bindings.Select(b =>
            {
                var d = thermal.Network.Cells.Single(c => c.Id == b.CellId); var c = state.Cells.Single(s => s.CellId == b.CellId); var m = fire.ReadCell(b.CellId);
                var frame = frames.Single(f => f.FrameId == b.FrameId); var center = frame.ToWorld(b.LocalFloorCenter);
                var gradient = 0.0;
                if (d.FloorRiseM > 0)
                { var portal = authored.Portals.Single(p => p.Id == b.PortalId); gradient = Math.Sign(portal.ToPoint.Y - portal.FromPoint.Y) * d.FloorRiseM / d.WidthM; }
                return new ObservedSmokeCell { RegionId = b.RegionId, UpperTemperatureK = m.UpperTemperatureK, LowerTemperatureK = m.LowerTemperatureK,
                    Cell = new SmokeCell { Id = b.CellId, CenterX = center.X, CenterY = center.Y, CenterZ = center.Z, YawDegrees = b.LocalYawDegrees + frame.YawDegrees,
                        WidthM = d.WidthM, DepthM = d.DepthM, BoundingHeightM = d.HeightM, FloorRiseM = d.FloorRiseM, SignedFloorGradientX = gradient,
                        InterfaceHeightAboveMinFloorM = m.InterfaceHeightM, UpperExtinctionPerM = profile.SmokeMassExtinctionM2PerKg * c.UpperSmokeKg / m.UpperVolumeM3,
                        LowerExtinctionPerM = profile.SmokeMassExtinctionM2PerKg * c.LowerSmokeKg / m.LowerVolumeM3 } };
            }).ToArray();
            opticalField = new SmokeOpticalField(smokeCells.Where(c => c.Cell.UpperExtinctionPerM > 0 || c.Cell.LowerExtinctionPerM > 0).Select(c => c.Cell).ToArray());
        }
        private static Point3 ClosestCellPoint(SmokeCell c, Point3 point)
        {
            var angle = c.YawDegrees * Math.PI / 180; var cos = Math.Cos(angle); var sin = Math.Sin(angle);
            var dx = point.X - c.CenterX; var dz = point.Z - c.CenterZ;
            var x = Math.Max(-c.WidthM / 2 + .01, Math.Min(c.WidthM / 2 - .01, cos * dx - sin * dz));
            var z = Math.Max(-c.DepthM / 2 + .01, Math.Min(c.DepthM / 2 - .01, sin * dx + cos * dz));
            var floor = c.CenterY + c.FloorRiseM / 2 + c.SignedFloorGradientX * x;
            var y = Math.Max(floor + .01, Math.Min(floor + c.BoundingHeightM - c.FloorRiseM - .01, point.Y + 1.6));
            return new Point3((float)(c.CenterX + cos * x + sin * z), (float)y, (float)(c.CenterZ - sin * x + cos * z));
        }
        private Point3 ClosestPoint(ConnectedRegionDefinition region, SpatialFrame[] frames, Point3 position, float inset = .05f)
        {
            var frame = frames.Single(f => f.FrameId == region.FrameId); var local = frame.ToLocal(position);
            var center = authored.Frame(region.FrameId).ToLocal(region.Center);
            var point = new Point3(Mathf.Clamp(local.X, center.X - region.SizeX / 2 + inset, center.X + region.SizeX / 2 - inset),
                center.Y + 1.3f, Mathf.Clamp(local.Z, center.Z - region.SizeZ / 2 + inset, center.Z + region.SizeZ / 2 - inset));
            return frame.ToWorld(point);
        }

        private void UpdateGoals(ServerSimulationView current, SpatialFrame[] frames)
        {
            foreach (var goal in goals)
            {
                var npc = current.Entities.Single(e => e.EntityId == goal.BodyId);
                if (!string.IsNullOrEmpty(npc.LeaderId))
                {
                    if (goal.LeaderId != npc.LeaderId || tick >= goal.RefreshTick)
                    {
                        var leader = current.Participants.Single(p => p.ParticipantId == npc.LeaderId);
                        goal.LeaderId = npc.LeaderId; goal.Goal = new SpatialPose { RegionId = leader.RegionId, FrameId = leader.FrameId, PortalId = leader.PortalId,
                            Position = leader.Position, LocalPosition = leader.LocalPosition }; goal.RefreshTick = tick + 10;
                    }
                }
                else if (goal.LeaderId != "" || tick >= goal.RefreshTick)
                { goal.LeaderId = ""; goal.RoutineIndex = 1 - goal.RoutineIndex; goal.Goal = Copy(routines[goal.BodyId][goal.RoutineIndex]); goal.RefreshTick = tick + 400; }
            }
        }
        private SpatialPose CurrentGoal(SpatialPose pose, SpatialFrame[] frames)
        { var result = Copy(pose); result.Position = frames.Single(f => f.FrameId == pose.FrameId).ToWorld(pose.LocalPosition); return result; }
        private FireSourcePower[] FireSources() => director.Active.Where(e => !e.Mitigated &&
            profile.Schedule.Choices.Single(c => c.Id == e.ChoiceId).Kind == FoundationIncidentKind.Fire).Select(e =>
            {
                var d = profile.Incidents.Single(i => i.ChoiceId == e.ChoiceId);
                return new FireSourcePower { CellId = thermal.CellAt(d.RegionId, d.LocalPosition), HeatReleaseW = d.HeatReleaseW,
                    FuelMassKgPerSecond = d.FuelMassKgPerSecond, SmokeMassKgPerSecond = d.SmokeMassKgPerSecond, RadiationFraction = d.RadiationFraction };
            }).ToArray();
        private bool Affected(string regionId, FoundationIncidentKind kind) => director.Active.Any(e => !e.Mitigated &&
            profile.Schedule.Choices.Single(c => c.Id == e.ChoiceId).Kind == kind && profile.Incidents.Single(i => i.ChoiceId == e.ChoiceId).RegionId == regionId);
        private SpatialFrame[] Frames(TrainOperatingState[] states) => authored.Frames.Select(f =>
        {
            var copy = f.Copy(); var route = profile.Trains.SingleOrDefault(t => t.FrameId == f.FrameId); if (route == null) return copy;
            var displacement = states.Single(t => t.TrainId == route.Operation.TrainId).Motion.ReferenceDistanceM - route.StationReferenceM;
            copy.Origin.X += (float)(route.DirectionWorld.X * displacement); copy.Origin.Z += (float)(route.DirectionWorld.Z * displacement); return copy;
        }).ToArray();
        private bool BoardingAligned(string portalId, TrainOperatingState[] states)
        {
            var route = profile.Trains.SingleOrDefault(t => t.PortalId == portalId); if (route == null) return true;
            var train = states.Single(t => t.TrainId == route.Operation.TrainId);
            return train.Motion.SignedSpeedMS == 0 && Math.Abs(train.Motion.ReferenceDistanceM - route.StationReferenceM) <= route.Operation.DockToleranceM && train.DoorOpen;
        }
        private HashSet<string> Closures(EntityState[] entities, TrainOperatingState[] states)
        {
            var result = new HashSet<string>(authored.Portals.Where(p => p.LinkedDoorEntityIds.Any(id => !entities.Single(e => e.EntityId == id).Active) || !BoardingAligned(p.Id, states)).Select(p => p.Id));
            foreach (var episode in director.Active.Where(e => !e.Mitigated && profile.Schedule.Choices.Single(c => c.Id == e.ChoiceId).Kind == FoundationIncidentKind.RouteRestriction))
            { var portal = profile.Incidents.Single(i => i.ChoiceId == episode.ChoiceId).PortalId; if (motion.CanClosePortal(portal)) result.Add(portal); }
            return result;
        }
        private string CapturePhysical() => CapturePreparedPhysical().Encoded;
        private PreparedPhysicalCheckpoint CapturePreparedPhysical(SimulationTickReport report = null)
        {
            var timer = Stopwatch.StartNew(); var snapshot = motion.Capture();
            if (report != null) report.MotionCaptureMilliseconds = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); var proof = motion.ExportCrowdCheckpointProof(); var crowd = proof.Encoded;
            if (report != null) report.CrowdEncodeMilliseconds = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            var auxiliary = new Auxiliary { IncidentCheckpoint = director.ExportCheckpoint(), MotionCheckpoint = WorldMotionCheckpoint.Encode(snapshot, crowd), Goals = goals };
            if (report != null) report.MotionEncodeMilliseconds = timer.Elapsed.TotalMilliseconds;
            return PhysicalCheckpoint.Prepare(new PhysicalWorldState { DefinitionHash = DefinitionHash, SimulationTick = tick, RandomState = (ulong)profile.Seed,
                Fire = fire.ExportState(), Trains = trains, CrowdCheckpoint = crowd, DirectorState = JsonUtility.ToJson(auxiliary) }, proof, report?.EncodeTiming);
        }
        public void Restore(WorldState state) => Restore(new ServerSimulationView { Tick = state.SimulationTick, DefinitionHash = state.SimulationDefinitionHash,
            Frames = state.Frames, Participants = state.Participants, Entities = state.Entities }, state.SimulationCheckpoint);
        private void Restore(ServerSimulationView current, string checkpoint)
        {
            var physical = PhysicalCheckpoint.Decode(checkpoint, DefinitionHash);
            if (physical.SimulationTick != current.Tick || physical.RandomState != (ulong)profile.Seed || Math.Abs(physical.Fire.SimulatedSeconds - current.Tick * TickSeconds) > .0001 ||
                physical.Trains.Length != profile.Trains.Length || physical.Trains.Any(t => !profile.Trains.Any(r => r.Operation.TrainId == t.TrainId)))
                throw new InvalidDataException("Physical state does not match the server boundary.");
            foreach (var route in profile.Trains) TrainOperation.Validate(physical.Trains.Single(t => t.TrainId == route.Operation.TrainId), route.Operation);
            if (physical.Trains.Any(t => Math.Abs(t.ElapsedSeconds - current.Tick * TickSeconds) > .0001) ||
                !SameFrames(Frames(physical.Trains), current.Frames)) throw new InvalidDataException("Train clock/route position differs from the physical frame boundary.");
            var auxiliary = JsonUtility.FromJson<Auxiliary>(physical.DirectorState);
            if (auxiliary == null || auxiliary.Schema != 1 || auxiliary.Goals == null || auxiliary.Goals.Length != profile.NpcCount ||
                auxiliary.Goals.Any(g => g == null || !routines.ContainsKey(g.BodyId) || g.Goal == null || g.RefreshTick < 0 || g.RoutineIndex < 0 || g.RoutineIndex > 1 ||
                    !g.Goal.Position.Finite || !g.Goal.LocalPosition.Finite || !authored.Regions.Any(r => r.Id == g.Goal.RegionId && r.FrameId == g.Goal.FrameId) ||
                    g.LeaderId != "" && !current.Participants.Any(p => p.ParticipantId == g.LeaderId)) || auxiliary.Goals.Select(g => g.BodyId).Distinct().Count() != auxiliary.Goals.Length)
                throw new InvalidDataException("Invalid private NPC/director checkpoint.");
            var candidateDirector = new IncidentDirector(profile.Schedule, (ulong)profile.Seed); candidateDirector.Restore(auxiliary.IncidentCheckpoint);
            if (candidateDirector.Tick != current.Tick) throw new InvalidDataException("Incident clock differs from physical clock.");
            if (!candidateDirector.Active.Select(e => e.Id).OrderBy(x => x).SequenceEqual(current.Entities.Where(e => e.Kind == EntityKind.Incident && e.Active).Select(e => e.EntityId).OrderBy(x => x)))
                throw new InvalidDataException("Active incident identity differs from its realized server state.");
            foreach (var active in candidateDirector.Active)
            {
                var binding = profile.Incidents.Single(i => i.ChoiceId == active.ChoiceId); var entity = current.Entities.Single(e => e.EntityId == active.Id); var region = authored.Region(binding.RegionId);
                if (entity.RegionId != region.Id || entity.FrameId != region.FrameId || entity.LocalPosition.DistanceSquared(binding.LocalPosition) != 0 ||
                    current.Frames.Single(f => f.FrameId == entity.FrameId).ToWorld(binding.LocalPosition).DistanceSquared(entity.Position) > 1e-8)
                    throw new InvalidDataException("Active incident binding differs from its realized location.");
            }
            var candidateFire = new ZoneFireModel(thermal.Network, physical.Fire, thermal.SolverOptions);
            var snapshot = WorldMotionCheckpoint.Decode(auxiliary.MotionCheckpoint);
            if (CrowdMotionModel.FromSnapshot(snapshot.Crowd).ExportCheckpoint() != physical.CrowdCheckpoint || snapshot.Crowd.Tick != current.Tick ||
                Math.Abs(snapshot.Crowd.ElapsedSeconds - current.Tick * TickSeconds) > .0001 || !SameFrames(snapshot.Frames, current.Frames) ||
                snapshot.BodyIds.Length != spawns.Length) throw new InvalidDataException("Motion checkpoint differs from its published boundary.");
            if (profile.Trains.Any(t => !BoardingAligned(t.PortalId, physical.Trains) && !snapshot.ClosedPortalIds.Contains(t.PortalId)))
                throw new InvalidDataException("An unavailable train boarding portal is physically open.");
            for (var i = 0; i < snapshot.BodyIds.Length; i++)
            {
                var id = snapshot.BodyIds[i]; var p = current.Participants.SingleOrDefault(a => a.ParticipantId == id); var e = current.Entities.SingleOrDefault(a => a.EntityId == id && a.Kind == EntityKind.Evacuee);
                if (p == null && e == null || snapshot.Poses[i].Position.DistanceSquared(p?.Position ?? e.Position) != 0 || snapshot.Poses[i].LocalPosition.DistanceSquared(p?.LocalPosition ?? e.LocalPosition) != 0 ||
                    snapshot.Poses[i].RegionId != (p?.RegionId ?? e.RegionId) || snapshot.Poses[i].FrameId != (p?.FrameId ?? e.FrameId)) throw new InvalidDataException("Body pose differs from server ownership/position.");
            }
            if (!motion.TryRestore(snapshot, out var failure)) throw new InvalidDataException(failure);
            tick = physical.SimulationTick; fire = candidateFire; director = candidateDirector; trains = physical.Trains; goals = auxiliary.Goals;
            actualClosed = new HashSet<string>(snapshot.ClosedPortalIds);
            RefreshOptics(current.Frames);
        }
        private static bool SameFrames(SpatialFrame[] a, SpatialFrame[] b) => a != null && b != null && a.Length == b.Length &&
            a.All(f => b.Count(g => g.FrameId == f.FrameId && f.Origin.DistanceSquared(g.Origin) == 0 && f.YawDegrees == g.YawDegrees) == 1);
        private static SpatialPose Copy(SpatialPose p) => new SpatialPose { RegionId = p.RegionId, FrameId = p.FrameId, PortalId = p.PortalId, Position = p.Position, LocalPosition = p.LocalPosition };
        private static void Put(ParticipantState p, SpatialPose pose)
        { p.RegionId = pose.RegionId; p.FrameId = pose.FrameId; p.PortalId = pose.PortalId; p.Position = pose.Position; p.LocalPosition = pose.LocalPosition; }
        private static void Put(EntityState e, SpatialPose pose)
        { e.RegionId = pose.RegionId; e.FrameId = pose.FrameId; e.Position = pose.Position; e.LocalPosition = pose.LocalPosition; }
        public void Dispose() { motion.Dispose(); geometry.Dispose(); }
    }
}
