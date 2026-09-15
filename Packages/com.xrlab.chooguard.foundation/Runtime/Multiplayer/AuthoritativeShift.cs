using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Single server-thread command authority, independent of Unity and its transport.</summary>
    public sealed class AuthoritativeShift
    {
        public const double InteractionRadius = 2.5;
        public const double InterestRadius = 25;
        private WorldState state;
        private readonly ICommitSink sink;
        private readonly Func<ParticipantState, EntityState, bool> visible;
        private readonly Func<EntityState, bool> canOperate;
        private bool persistenceFaulted;
        private bool processingCommand;
        private readonly Dictionary<string, CommandReceipt> receipts = new Dictionary<string, CommandReceipt>(StringComparer.Ordinal);

        public AuthoritativeShift(WorldState initial, ICommitSink sink, Func<ParticipantState, EntityState, bool> visible,
            Func<EntityState, bool> canOperate = null)
        {
            Validate(initial);
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.visible = visible ?? throw new ArgumentNullException(nameof(visible));
            this.canOperate = canOperate ?? (_ => true);
            state = initial.Copy();
            foreach (var receipt in state.Receipts) receipts.Add(Key(receipt.ParticipantId, receipt.CommandId), receipt);
        }

        public static AuthoritativeShift Restore(WorldState checkpoint, ICommitSink sink,
            Func<ParticipantState, EntityState, bool> visible, Func<EntityState, bool> canOperate = null)
        {
            var restored = new AuthoritativeShift(checkpoint, sink, visible, canOperate);
            restored.state.Paused = true;
            return restored;
        }

        public WorldState ExportCheckpoint() => state.Copy();
        public bool Paused => state.Paused;
        public ParticipantState Participant(string participantId) => FindParticipant(participantId).Copy();
        public ServerSimulationView ReadSimulation() => new ServerSimulationView { Tick = state.SimulationTick, Sequence = state.Sequence, Paused = state.Paused,
            DefinitionHash = state.SimulationDefinitionHash, Checkpoint = state.SimulationCheckpoint,
            Frames = state.Frames.Select(f => f.Copy()).ToArray(), Participants = state.Participants.Select(p => p.Copy()).ToArray(),
            Entities = state.Entities.Select(e => e.Copy()).ToArray() };

        public void ApplyServerSimulation(ServerSimulationUpdate update, PhysicalCheckpointTiming checkpointTiming = null)
            => ApplyServerSimulationCore(update, null, checkpointTiming);

        public void ApplyPreparedServerSimulation(ServerSimulationUpdate update, PreparedPhysicalCheckpoint prepared, PhysicalCheckpointTiming checkpointTiming = null)
        {
            if (prepared == null) throw new ArgumentException("Missing locally prepared physical boundary.");
            ApplyServerSimulationCore(update, prepared, checkpointTiming);
        }

        private void ApplyServerSimulationCore(ServerSimulationUpdate update, PreparedPhysicalCheckpoint prepared, PhysicalCheckpointTiming checkpointTiming)
        {
            RequireIdleMutation();
            if (state.SchemaVersion != 3 || state.Paused || persistenceFaulted)
                throw new InvalidOperationException("Physical simulation is unavailable or paused.");
            if (update == null || update.DefinitionHash != state.SimulationDefinitionHash || update.Tick != state.SimulationTick + 1 ||
                update.Frames == null || update.Actors == null || update.Entities == null || update.Actors.Length != state.Participants.Length ||
                update.Actors.Any(a => a == null || a.Pose == null || !state.Participants.Any(p => p.ParticipantId == a.ParticipantId)) ||
                update.Actors.Select(a => a.ParticipantId).Distinct().Count() != update.Actors.Length ||
                update.Frames.Any(f => f == null) || !update.Frames.Select(f => f.FrameId).OrderBy(x => x).SequenceEqual(state.Frames.Select(f => f.FrameId).OrderBy(x => x)) ||
                update.Entities.Length > 4096 || update.Entities.Any(e => e == null || !Identifier(e.EntityId)) ||
                state.Entities.Any(e => !update.Entities.Any(n => n.EntityId == e.EntityId)))
                throw new ArgumentException("Invalid server physical publication.");
            var physical = prepared == null ? PhysicalCheckpoint.Decode(update.Checkpoint, update.DefinitionHash, checkpointTiming) :
                prepared.Read(update.Checkpoint, update.DefinitionHash);
            if (physical.SimulationTick != update.Tick) throw new ArgumentException("Physical checkpoint tick differs from publication.");
            foreach (var e in update.Entities)
            {
                var previous = state.Entities.SingleOrDefault(x => x.EntityId == e.EntityId);
                if (previous == null)
                { if (e.Kind != EntityKind.Incident || !e.EntityId.StartsWith("incident-", StringComparison.Ordinal) || e.Revision != 0 || !string.IsNullOrEmpty(e.LeaderId)) throw new ArgumentException("Only new server incident identities may be introduced."); }
                else if (e.Kind != previous.Kind || e.RequiredRoleId != previous.RequiredRoleId || e.LeaderId != previous.LeaderId ||
                    previous.Kind == EntityKind.Incident && !previous.Active && e.Active ||
                    e.Revision != previous.Revision + (e.Active != previous.Active ? 1 : 0))
                    throw new ArgumentException("Physical publication cannot change role/ownership or invent action revisions.");
            }
            var next = state.CopyCurrent(); next.SimulationTick = update.Tick; next.SimulationCheckpoint = update.Checkpoint;
            next.Frames = state.Frames.Select(f => update.Frames.Single(n => n.FrameId == f.FrameId).Copy()).ToArray();
            var oldWorld = state.Frames.SingleOrDefault(f => f.FrameId == "world"); var newWorld = next.Frames.SingleOrDefault(f => f.FrameId == "world");
            if (oldWorld != null && (newWorld.Origin.DistanceSquared(oldWorld.Origin) != 0 || newWorld.YawDegrees != oldWorld.YawDegrees))
                throw new ArgumentException("The stationary world reference frame cannot move.");
            foreach (var pose in update.Actors)
            {
                var actor = next.Participants.Single(p => p.ParticipantId == pose.ParticipantId);
                actor.RegionId = pose.Pose.RegionId; actor.FrameId = pose.Pose.FrameId; actor.PortalId = pose.Pose.PortalId;
                actor.Position = pose.Pose.Position; actor.LocalPosition = pose.Pose.LocalPosition;
            }
            next.Entities = update.Entities.Select(e => e.Copy()).ToArray();
            Validate(next, false, false); state = next;
        }
        public void FaultPersistence()
        {
            // This emergency stop is allowed even while an adapter is being called.
            persistenceFaulted = true; state.Paused = true;
            foreach (var actor in state.Participants) actor.InputEnabled = false;
        }

        // Only the authenticated server movement/input adapter may call these, never a position RPC.
        public void SetServerPosition(string participantId, string regionId, Point3 position)
        {
            RequireIdleMutation();
            if (state.SchemaVersion != 1) throw new InvalidOperationException("Spatial worlds require a frame-bound server pose.");
            if (!position.Finite || !Identifier(regionId)) throw new ArgumentException("Invalid server position.");
            var actor = FindParticipant(participantId);
            actor.RegionId = regionId; actor.Position = position;
        }
        public void SetServerPose(string participantId, SpatialPose pose)
        {
            RequireIdleMutation();
            if ((state.SchemaVersion != 2 && state.SchemaVersion != 3) || pose == null || !Identifier(pose.RegionId) ||
                (pose.PortalId != "" && !PortalIdentifier(pose.PortalId)) || !ValidFramePose(state, pose.FrameId, pose.Position, pose.LocalPosition))
                throw new ArgumentException("Invalid frame-bound server position.");
            var actor = FindParticipant(participantId);
            actor.RegionId = pose.RegionId; actor.FrameId = pose.FrameId; actor.PortalId = pose.PortalId;
            actor.Position = pose.Position; actor.LocalPosition = pose.LocalPosition;
        }
        public void SetInputEnabled(string participantId, bool enabled)
        {
            RequireIdleMutation();
            FindParticipant(participantId).InputEnabled = enabled;
        }

        public ObservedState Observe(string participantId)
        {
            var actor = FindParticipant(participantId);
            return new ObservedState { WorldId = state.WorldId, ShiftId = state.ShiftId,
                ParticipantId = actor.ParticipantId, Sequence = state.Sequence, SimulationTick = state.SimulationTick, Paused = state.Paused,
                Entities = state.Entities.Where(x => (state.SchemaVersion == 3 || x.RegionId == actor.RegionId) &&
                    x.Position.DistanceSquared(actor.Position) <= InterestRadius * InterestRadius &&
                    (x.Kind != EntityKind.Incident || x.Active && actor.ObservedIds.Contains(x.EntityId)) && CanSee(actor, x))
                    .Select(x => x.Copy()).ToArray(),
                TotalReports = state.Reports.Count(x => x.ToTeamId == actor.TeamId),
                Reports = state.Reports.Where(x => x.ToTeamId == actor.TeamId).OrderByDescending(x => x.Sequence)
                    .Take(16).Reverse().Select(x => x.Copy()).ToArray() };
        }

        public int DiscoverNearby(string participantId, Func<Point3, bool> inView)
        {
            RequireIdleMutation();
            var actor = FindParticipant(participantId);
            if (state.Paused || !actor.InputEnabled) return 0;
            var count = 0;
            foreach (var target in state.Entities.Where(x => x.Kind == EntityKind.Incident && x.Active &&
                !actor.ObservedIds.Contains(x.EntityId) && (state.SchemaVersion == 3 ? x.Position.DistanceSquared(actor.Position) <= InterestRadius * InterestRadius : InReach(actor, x)) && CanSee(actor, x) && inView(x.Position)).ToArray())
            {
                var receipt = Submit(participantId, new WorldCommand { WorldId = state.WorldId, ShiftId = state.ShiftId,
                    ParticipantId = participantId, TeamId = actor.TeamId, CommandId = Guid.NewGuid().ToString("N"),
                    Kind = CommandKind.Discover, TargetId = target.EntityId });
                if (receipt.Code == CommandCode.Accepted) count++;
            }
            return count;
        }

        public CommandReceipt Submit(string authenticatedParticipantId, WorldCommand command)
        {
            // Single-threaded adapters may still call back synchronously. A nested command
            // must not reserve the same sequence or replace the state prepared by its caller.
            if (processingCommand) return Reject(command, CommandCode.AuthorityBusy);
            processingCommand = true;
            try { return SubmitCore(authenticatedParticipantId, command?.Copy()); }
            finally { processingCommand = false; }
        }

        private CommandReceipt SubmitCore(string authenticatedParticipantId, WorldCommand command)
        {
            if (!WellFormed(command)) return Reject(command, CommandCode.InvalidCommand);
            var actor = state.Participants.SingleOrDefault(x => x.ParticipantId == authenticatedParticipantId);
            if (actor == null || actor.ParticipantId != command.ParticipantId || actor.TeamId != command.TeamId)
                return Reject(command, CommandCode.IdentityMismatch);
            if (command.WorldId != state.WorldId || command.ShiftId != state.ShiftId)
                return Reject(command, CommandCode.WrongWorld);
            var fingerprint = Fingerprint(command);
            if (receipts.TryGetValue(Key(actor.ParticipantId, command.CommandId), out var original))
                return original.Fingerprint == fingerprint ? original.Copy() : Reject(command, CommandCode.CommandIdConflict);
            if (persistenceFaulted) return Reject(command, CommandCode.PersistenceUnavailable);
            if (!actor.InputEnabled && !(actor.IsInstructor && (command.Kind == CommandKind.PauseShift || command.Kind == CommandKind.ResumeShift)))
                return Reject(command, CommandCode.InputPaused);
            var commit = new ShiftCommit { Paused = state.Paused, WorldSchemaVersion = state.SchemaVersion,
                SpatialProfileId = state.SpatialProfileId,
                SimulationDefinitionHash = state.SimulationDefinitionHash, SimulationCheckpoint = state.SimulationCheckpoint,
                SimulationTick = state.SimulationTick, Frames = state.SchemaVersion == 3 ? state.Frames.Select(f => f.Copy()).ToArray() : Array.Empty<SpatialFrame>(),
                Participants = state.Participants.Select(x => x.Copy()).ToArray(), Receipt = new CommandReceipt {
                WorldId = state.WorldId, ShiftId = state.ShiftId, ParticipantId = actor.ParticipantId,
                CommandId = command.CommandId, Fingerprint = fingerprint, Code = CommandCode.Accepted,
                Sequence = checked(state.Sequence + 1) } };
            var result = Prepare(actor, command, commit);
            if (persistenceFaulted) return Reject(command, CommandCode.PersistenceUnavailable);
            if (result != CommandCode.Accepted) return Reject(command, result);
            if (state.SchemaVersion == 3)
                commit.Entities = state.Entities.Select(e => (commit.Entities.SingleOrDefault(c => c.EntityId == e.EntityId) ?? e).Copy()).ToArray();
            var next = PrepareNextState(commit);
            try { sink.Append(commit.Copy()); }
            catch (Exception)
            {
                // Append can fail after writing. Any sink failure is an ambiguous durable
                // boundary: stop this authority and recover from the checked journal, not retry.
                FaultPersistence();
                return Reject(command, CommandCode.PersistenceUnavailable);
            }
            // A synchronous adapter can report a fault without throwing. Do not overwrite
            // its pause/input stop with the state prepared before the callback.
            if (persistenceFaulted) return Reject(command, CommandCode.PersistenceUnavailable);
            state = next;
            receipts.Add(Key(commit.Receipt.ParticipantId, commit.Receipt.CommandId), commit.Receipt.Copy());
            return commit.Receipt.Copy();
        }

        private CommandCode Prepare(ParticipantState actor, WorldCommand c, ShiftCommit commit)
        {
            if (c.Kind == CommandKind.PauseShift || c.Kind == CommandKind.ResumeShift)
            {
                if (!actor.IsInstructor) return CommandCode.RoleDenied;
                commit.Paused = c.Kind == CommandKind.PauseShift;
                return CommandCode.Accepted;
            }
            if (state.Paused) return CommandCode.ShiftPaused;
            if (c.Kind == CommandKind.AcknowledgeReport)
            {
                var report = state.Reports.SingleOrDefault(x => x.ReportId == c.TargetId && x.ToTeamId == actor.TeamId);
                if (report == null) return CommandCode.UnknownTarget;
                var changed = report.Copy();
                changed.AcknowledgedBy = changed.AcknowledgedBy.Concat(new[] { actor.ParticipantId }).Distinct().ToArray();
                commit.Reports = new[] { changed };
                return CommandCode.Accepted;
            }
            var target = state.Entities.SingleOrDefault(x => x.EntityId == c.TargetId);
            if (target == null) return CommandCode.UnknownTarget;
            if (c.Kind == CommandKind.Report)
            {
                if (!actor.ObservedIds.Contains(target.EntityId) || target.Kind == EntityKind.Incident && !target.Active) return CommandCode.NotObserved;
                // Report is a captured observation, not permission to stream the incident remotely.
                if (!(state.SchemaVersion == 3 ? target.Position.DistanceSquared(actor.Position) <= InterestRadius * InterestRadius : InReach(actor, target)) || !CanSee(actor, target)) return CommandCode.OutOfReach;
                var recipient = string.IsNullOrEmpty(c.Argument) ? actor.TeamId : c.Argument;
                if (!state.Participants.Any(x => x.TeamId == recipient)) return CommandCode.UnknownTarget;
                commit.Reports = new[] { new TeamReport { ReportId = "report-" + commit.Receipt.Sequence,
                    FromParticipantId = actor.ParticipantId, ToTeamId = recipient, EntityId = target.EntityId,
                    RegionId = target.RegionId, Position = target.Position, FrameId = target.FrameId,
                    LocalPosition = target.LocalPosition, ObservedRevision = target.Revision,
                    ObservedSimulationTick = state.SimulationTick, ObservedFrame = state.SchemaVersion == 3 ? state.Frames.Single(f => f.FrameId == target.FrameId).Copy() : null,
                    Sequence = commit.Receipt.Sequence } };
                return CommandCode.Accepted;
            }
            if (c.Kind == CommandKind.Discover)
            {
                if (target.Kind != EntityKind.Incident || !target.Active) return CommandCode.UnknownTarget;
                if (!(state.SchemaVersion == 3 ? target.Position.DistanceSquared(actor.Position) <= InterestRadius * InterestRadius : InReach(actor, target)))
                    return CommandCode.OutOfReach;
                if (!CanSee(actor, target)) return CommandCode.Occluded;
                var observed = actor.Copy();
                observed.ObservedIds = observed.ObservedIds.Concat(new[] { target.EntityId }).Distinct().ToArray();
                commit.Participants[Array.FindIndex(commit.Participants, p => p.ParticipantId == actor.ParticipantId)] = observed;
                return CommandCode.Accepted;
            }
            if (!InReach(actor, target)) return CommandCode.OutOfReach;
            if (!CanSee(actor, target)) return CommandCode.Occluded;
            if (!string.IsNullOrEmpty(target.RequiredRoleId) && actor.RoleId != target.RequiredRoleId)
                return CommandCode.RoleDenied;
            if (target.Revision != c.ExpectedRevision) return CommandCode.StaleTarget;
            var changedTarget = target.Copy();
            switch (c.Kind)
            {
                case CommandKind.Operate:
                    if (target.Kind != EntityKind.Equipment) return CommandCode.UnknownTarget;
                    if (!canOperate(target.Copy())) return CommandCode.TargetBlocked;
                    changedTarget.Active = !target.Active;
                    break;
                case CommandKind.ClaimEvacuee:
                    if (target.Kind != EntityKind.Evacuee) return CommandCode.UnknownTarget;
                    if (!target.Active) return CommandCode.TargetBlocked;
                    if (!string.IsNullOrEmpty(target.LeaderId)) return CommandCode.AlreadyClaimed;
                    changedTarget.LeaderId = actor.ParticipantId;
                    break;
                case CommandKind.HandOffEvacuee:
                    if (target.Kind != EntityKind.Evacuee || target.LeaderId != actor.ParticipantId) return CommandCode.RoleDenied;
                    if (!target.Active) return CommandCode.TargetBlocked;
                    var next = state.Participants.SingleOrDefault(x => x.ParticipantId == c.Argument);
                    if (next == null || !next.InputEnabled || !InReach(next, target) || !CanSee(next, target)) return CommandCode.OutOfReach;
                    if (!string.IsNullOrEmpty(target.RequiredRoleId) && next.RoleId != target.RequiredRoleId) return CommandCode.RoleDenied;
                    changedTarget.LeaderId = next.ParticipantId;
                    break;
                default: return CommandCode.InvalidCommand;
            }
            changedTarget.Revision = checked(target.Revision + 1);
            commit.Entities = new[] { changedTarget };
            return CommandCode.Accepted;
        }

        // Replay only commits from the integrity-checked server journal; never from a client message.
        public void Replay(ShiftCommit commit)
        {
            RequireIdleMutation();
            if (commit == null || commit.Receipt == null || commit.Receipt.Code != CommandCode.Accepted ||
                commit.Receipt.WorldId != state.WorldId || commit.Receipt.ShiftId != state.ShiftId ||
                commit.Receipt.Sequence != state.Sequence + 1 ||
                receipts.ContainsKey(Key(commit.Receipt.ParticipantId, commit.Receipt.CommandId)))
                throw new InvalidDataException("Journal does not continue this checkpoint.");
            state = PrepareNextState(commit);
            receipts.Add(Key(commit.Receipt.ParticipantId, commit.Receipt.CommandId), commit.Receipt.Copy());
            state.Paused = true;
        }

        private void RequireIdleMutation()
        {
            if (processingCommand)
                throw new InvalidOperationException("Cannot mutate authority during command preparation or persistence.");
        }

        private WorldState PrepareNextState(ShiftCommit commit)
        {
            // Zero is accepted only for pre-spatial JSON records, where the new field was absent.
            if (!((state.SchemaVersion == 1 && (commit.WorldSchemaVersion == 0 || commit.WorldSchemaVersion == 1)) ||
                    commit.WorldSchemaVersion == state.SchemaVersion && commit.SpatialProfileId == state.SpatialProfileId) ||
                commit.Participants == null || commit.Entities == null || commit.Reports == null ||
                commit.Participants.Any(x => x == null || !state.Participants.Any(p => p.ParticipantId == x.ParticipantId)) ||
                commit.Entities.Any(x => x == null || !state.Entities.Any(e => e.EntityId == x.EntityId) &&
                    !(state.SchemaVersion == 3 && x.Kind == EntityKind.Incident && x.EntityId.StartsWith("incident-", StringComparison.Ordinal))) ||
                commit.Reports.Any(x => x == null) ||
                commit.Participants.Select(x => x.ParticipantId).Distinct().Count() != commit.Participants.Length ||
                commit.Entities.Select(x => x.EntityId).Distinct().Count() != commit.Entities.Length ||
                commit.Reports.Select(x => x.ReportId).Distinct().Count() != commit.Reports.Length)
                throw new InvalidDataException("Invalid journal delta identities.");
            var nextState = state.Copy();
            if (state.SchemaVersion == 3)
            {
                if (commit.SimulationDefinitionHash != state.SimulationDefinitionHash || commit.SimulationTick < state.SimulationTick || commit.Frames == null ||
                    !commit.Frames.Select(f => f.FrameId).OrderBy(x => x).SequenceEqual(state.Frames.Select(f => f.FrameId).OrderBy(x => x)))
                    throw new InvalidDataException("Physical journal context differs from this world.");
                nextState.SimulationTick = commit.SimulationTick; nextState.SimulationCheckpoint = commit.SimulationCheckpoint;
                nextState.Frames = commit.Frames.Select(f => f.Copy()).ToArray();
            }
            foreach (var x in commit.Participants)
            {
                if (x.ObservedIds == null) throw new InvalidDataException("Invalid observed state.");
                nextState.Participants[Array.FindIndex(nextState.Participants, p => p.ParticipantId == x.ParticipantId)] = x.Copy();
            }
            foreach (var x in commit.Entities)
            { var at = Array.FindIndex(nextState.Entities, e => e.EntityId == x.EntityId);
                if (at < 0) nextState.Entities = nextState.Entities.Concat(new[] { x.Copy() }).ToArray(); else nextState.Entities[at] = x.Copy(); }
            foreach (var x in commit.Reports)
            {
                if (x.AcknowledgedBy == null) throw new InvalidDataException("Invalid report acknowledgments.");
                var index = Array.FindIndex(nextState.Reports, r => r.ReportId == x.ReportId);
                if (index < 0) nextState.Reports = nextState.Reports.Concat(new[] { x.Copy() }).ToArray();
                else nextState.Reports[index] = x.Copy();
            }
            nextState.Paused = commit.Paused; nextState.Sequence = commit.Receipt.Sequence;
            nextState.Receipts = nextState.Receipts.Concat(new[] { commit.Receipt.Copy() }).ToArray();
            Validate(nextState);
            return nextState;
        }

        private static void Validate(WorldState initial, bool history = true, bool physical = true)
        {
            if (initial == null || (initial.SchemaVersion != 1 && initial.SchemaVersion != 2 && initial.SchemaVersion != 3) || !Identifier(initial.WorldId) || !Identifier(initial.ShiftId) ||
                initial.Sequence < 0 || initial.Participants == null || initial.Entities == null || initial.Reports == null || initial.Receipts == null)
                throw new InvalidDataException("Unsupported or incomplete world checkpoint.");
            if (initial.SchemaVersion == 3)
            {
                if (initial.SimulationTick < 0 || initial.SimulationDefinitionHash == null || initial.SimulationCheckpoint == null)
                    throw new InvalidDataException("Missing physical world context.");
                if (physical && PhysicalCheckpoint.Decode(initial.SimulationCheckpoint, initial.SimulationDefinitionHash).SimulationTick != initial.SimulationTick)
                    throw new InvalidDataException("Physical world tick differs from checkpoint.");
            }
            if (initial.Participants.Any(p => p == null || !Identifier(p.ParticipantId) || !Identifier(p.TeamId) ||
                !Identifier(p.RoleId) || !Identifier(p.RegionId) || !p.Position.Finite || p.ObservedIds == null) ||
                initial.Entities.Any(e => e == null || !Identifier(e.EntityId) || !Identifier(e.RegionId) || !e.Position.Finite ||
                e.Revision < 0 || !Enum.IsDefined(typeof(EntityKind), e.Kind)) ||
                initial.Participants.Select(p => p.ParticipantId).Distinct().Count() != initial.Participants.Length ||
                initial.Entities.Select(e => e.EntityId).Distinct().Count() != initial.Entities.Length)
                throw new InvalidDataException("Invalid world identities or geometry.");
            if (initial.SchemaVersion >= 2 && (!Identifier(initial.SpatialProfileId) || initial.Frames == null || initial.Frames.Length == 0 ||
                initial.Frames.Any(f => f == null || !Identifier(f.FrameId) || !f.Origin.Finite || float.IsNaN(f.YawDegrees) || float.IsInfinity(f.YawDegrees)) ||
                initial.Frames.Select(f => f.FrameId).Distinct().Count() != initial.Frames.Length ||
                initial.Participants.Any(p => !ValidFramePose(initial, p.FrameId, p.Position, p.LocalPosition) ||
                    (p.PortalId != "" && !PortalIdentifier(p.PortalId))) ||
                initial.Entities.Any(e => !ValidFramePose(initial, e.FrameId, e.Position, e.LocalPosition)) ||
                history && initial.Reports.Any(r => r == null || !ValidReportPose(initial, r))))
                throw new InvalidDataException("Spatial checkpoint has an invalid profile or frame pose.");
            if (!history) return;
            if (initial.Participants.Any(p => p.ObservedIds.Any(id => !initial.Entities.Any(e => e.EntityId == id && e.Kind == EntityKind.Incident))) ||
                initial.Entities.Any(e => !string.IsNullOrEmpty(e.LeaderId) && !initial.Participants.Any(p => p.ParticipantId == e.LeaderId)) ||
                initial.Reports.Any(r => r == null || !Identifier(r.ReportId) || !r.Position.Finite || r.AcknowledgedBy == null ||
                    r.Sequence < 1 || r.Sequence > initial.Sequence || r.ObservedRevision < 0 ||
                    !initial.Entities.Any(e => e.EntityId == r.EntityId) || !initial.Participants.Any(p => p.ParticipantId == r.FromParticipantId) ||
                    !initial.Participants.Any(p => p.TeamId == r.ToTeamId) ||
                    r.AcknowledgedBy.Any(id => !initial.Participants.Any(p => p.ParticipantId == id && p.TeamId == r.ToTeamId))) ||
                initial.Reports.Select(r => r.ReportId).Distinct().Count() != initial.Reports.Length)
                throw new InvalidDataException("Invalid world references.");
            if (initial.Receipts.Any(r => r == null || r.WorldId != initial.WorldId || r.ShiftId != initial.ShiftId ||
                r.Sequence < 1 || r.Sequence > initial.Sequence || r.Code != CommandCode.Accepted ||
                !Identifier(r.ParticipantId) || !initial.Participants.Any(p => p.ParticipantId == r.ParticipantId) ||
                !Identifier(r.CommandId) || r.Fingerprint == null || r.Fingerprint.Length != 64 ||
                r.Fingerprint.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))) ||
                initial.Receipts.Select(r => Key(r.ParticipantId, r.CommandId)).Distinct().Count() != initial.Receipts.Length ||
                initial.Sequence != initial.Receipts.LongLength ||
                initial.Receipts.OrderBy(r => r.Sequence).Where((r, index) => r.Sequence != index + 1L).Any())
                throw new InvalidDataException("Invalid command ledger.");
        }

        private ParticipantState FindParticipant(string id) => state.Participants.SingleOrDefault(x => x.ParticipantId == id)
            ?? throw new ArgumentException("Unknown authenticated participant.");
        private bool CanSee(ParticipantState actor, EntityState target) => visible(actor.Copy(), target.Copy());
        private bool InReach(ParticipantState actor, EntityState target) => (state.SchemaVersion == 3 || actor.RegionId == target.RegionId) &&
            actor.Position.DistanceSquared(target.Position) <= InteractionRadius * InteractionRadius;
        private static string Key(string participant, string command) => participant + "/" + command;
        private static bool ValidFramePose(WorldState world, string frameId, Point3 position, Point3 local)
        {
            var frame = world.Frames?.SingleOrDefault(f => f.FrameId == frameId);
            return frame != null && position.Finite && local.Finite && frame.ToWorld(local).DistanceSquared(position) < .0001;
        }
        private static bool ValidReportPose(WorldState world, TeamReport report)
        {
            if (world.SchemaVersion != 3) return ValidFramePose(world, report.FrameId, report.Position, report.LocalPosition);
            var frame = report.ObservedFrame;
            return frame != null && frame.FrameId == report.FrameId && frame.Origin.Finite && !float.IsNaN(frame.YawDegrees) && !float.IsInfinity(frame.YawDegrees) &&
                report.ObservedSimulationTick >= 0 && report.ObservedSimulationTick <= world.SimulationTick && report.Position.Finite && report.LocalPosition.Finite &&
                frame.ToWorld(report.LocalPosition).DistanceSquared(report.Position) < .0001;
        }
        private static bool PortalIdentifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 256 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-');
        private static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || "_.:-".IndexOf(c) >= 0);
        private static bool WellFormed(WorldCommand c) => c != null && Identifier(c.WorldId) && Identifier(c.ShiftId) &&
            Identifier(c.ParticipantId) && Identifier(c.TeamId) && Identifier(c.CommandId) && c.TargetId != null &&
            (c.TargetId == "" || Identifier(c.TargetId)) && c.Argument != null && c.Argument.Length <= 512 && c.ExpectedRevision >= 0 &&
            Enum.IsDefined(typeof(CommandKind), c.Kind);
        private CommandReceipt Reject(WorldCommand c, CommandCode code) => new CommandReceipt { WorldId = state.WorldId,
            ShiftId = state.ShiftId, ParticipantId = c?.ParticipantId ?? "", CommandId = c?.CommandId ?? "", Code = code, Sequence = state.Sequence };
        private static string Fingerprint(WorldCommand c)
        {
            var parts = new[] { c.WorldId, c.ShiftId, c.ParticipantId, c.TeamId, c.CommandId, c.TargetId, c.Argument,
                ((int)c.Kind).ToString(CultureInfo.InvariantCulture), c.ExpectedRevision.ToString(CultureInfo.InvariantCulture) };
            var text = string.Concat(parts.Select(x => x.Length.ToString(CultureInfo.InvariantCulture) + ":" + x));
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
