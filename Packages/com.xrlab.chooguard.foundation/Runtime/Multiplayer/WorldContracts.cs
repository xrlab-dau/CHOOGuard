using System;
using System.Linq;

namespace ChooGuard.Foundation.Multiplayer
{
    public enum EntityKind { Equipment, Evacuee, Incident, Train }
    public enum CommandKind { Operate, Discover, Report, AcknowledgeReport, ClaimEvacuee, HandOffEvacuee, PauseShift, ResumeShift }
    public enum CommandCode { Accepted, InvalidCommand, IdentityMismatch, WrongWorld, CommandIdConflict,
        RoleDenied, UnknownTarget, StaleTarget, OutOfReach, Occluded, NotObserved, AlreadyClaimed,
        InputPaused, ShiftPaused, PersistenceUnavailable, TargetBlocked }

    [Serializable]
    public struct Point3
    {
        public float X, Y, Z;
        public Point3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool Finite => !(float.IsNaN(X) || float.IsInfinity(X) || float.IsNaN(Y) ||
            float.IsInfinity(Y) || float.IsNaN(Z) || float.IsInfinity(Z));
        public double DistanceSquared(Point3 other)
        { double x = X - other.X, y = Y - other.Y, z = Z - other.Z; return x * x + y * y + z * z; }
    }

    // IDs are explicit, bounded protocol values. Human-facing names belong in separate data.
    [Serializable]
    public sealed class WorldCommand
    {
        public string WorldId, ShiftId, ParticipantId, TeamId, CommandId, TargetId = "", Argument = "";
        public CommandKind Kind;
        public long ExpectedRevision;
    }

    [Serializable]
    public sealed class CommandReceipt
    {
        public string WorldId, ShiftId, ParticipantId, CommandId, Fingerprint;
        public CommandCode Code;
        public long Sequence;
        public CommandReceipt Copy() => (CommandReceipt)MemberwiseClone();
    }

    [Serializable]
    public sealed class ParticipantState
    {
        public string ParticipantId, TeamId, RoleId, RegionId;
        public string FrameId = "world", PortalId = "";
        public bool IsInstructor, InputEnabled = true;
        public Point3 Position, LocalPosition;
        public string[] ObservedIds = Array.Empty<string>();
        public ParticipantState Copy()
        { var p = (ParticipantState)MemberwiseClone(); p.ObservedIds = ObservedIds.ToArray(); return p; }
    }

    [Serializable]
    public sealed class EntityState
    {
        public string EntityId, RegionId, RequiredRoleId = "", LeaderId = "", FrameId = "world";
        public EntityKind Kind;
        public Point3 Position, LocalPosition;
        public long Revision;
        public bool Active = true;
        public EntityState Copy() => (EntityState)MemberwiseClone();
    }

    [Serializable]
    public sealed class TeamReport
    {
        public string ReportId, FromParticipantId, ToTeamId, EntityId, RegionId, FrameId = "world";
        public Point3 Position, LocalPosition;
        public long ObservedRevision, Sequence;
        public long ObservedSimulationTick;
        public SpatialFrame ObservedFrame;
        public string[] AcknowledgedBy = Array.Empty<string>();
        public TeamReport Copy()
        { var r = (TeamReport)MemberwiseClone(); r.AcknowledgedBy = AcknowledgedBy.ToArray(); r.ObservedFrame = ObservedFrame?.Copy(); return r; }
    }

    // Server-only persistence DTO. Never serialize this type onto a client channel.
    [Serializable]
    public sealed class WorldState
    {
        public int SchemaVersion = 1;
        public string WorldId, ShiftId;
        public string SpatialProfileId = "";
        public string SimulationDefinitionHash = "", SimulationCheckpoint = "";
        public long SimulationTick;
        public SpatialFrame[] Frames = Array.Empty<SpatialFrame>();
        public long Sequence;
        public bool Paused;
        public ParticipantState[] Participants = Array.Empty<ParticipantState>();
        public EntityState[] Entities = Array.Empty<EntityState>();
        public TeamReport[] Reports = Array.Empty<TeamReport>();
        public CommandReceipt[] Receipts = Array.Empty<CommandReceipt>();
        public WorldState Copy() => new WorldState { SchemaVersion = SchemaVersion, WorldId = WorldId,
            ShiftId = ShiftId, Sequence = Sequence, Paused = Paused, SpatialProfileId = SpatialProfileId,
            SimulationDefinitionHash = SimulationDefinitionHash, SimulationCheckpoint = SimulationCheckpoint, SimulationTick = SimulationTick,
            Frames = (Frames ?? Array.Empty<SpatialFrame>()).Select(f => f.Copy()).ToArray(),
            Participants = Participants.Select(x => x.Copy()).ToArray(), Entities = Entities.Select(x => x.Copy()).ToArray(),
            Reports = Reports.Select(x => x.Copy()).ToArray(), Receipts = Receipts.Select(x => x.Copy()).ToArray() };
        internal WorldState CopyCurrent() => new WorldState { SchemaVersion = SchemaVersion, WorldId = WorldId, ShiftId = ShiftId,
            Sequence = Sequence, Paused = Paused, SpatialProfileId = SpatialProfileId, SimulationDefinitionHash = SimulationDefinitionHash,
            SimulationCheckpoint = SimulationCheckpoint, SimulationTick = SimulationTick,
            Frames = Frames.Select(f => f.Copy()).ToArray(), Participants = Participants.Select(p => p.Copy()).ToArray(),
            Entities = Entities.Select(e => e.Copy()).ToArray(), Reports = Reports, Receipts = Receipts };
    }

    // Deliberately no seed, future incidents, solution steps, raw ledger or server checkpoint.
    [Serializable]
    public sealed class ObservedState
    {
        public string WorldId, ShiftId, ParticipantId;
        public long Sequence;
        public long SimulationTick;
        public bool Paused;
        public int TotalReports;
        public EntityState[] Entities = Array.Empty<EntityState>();
        public TeamReport[] Reports = Array.Empty<TeamReport>();
    }

    [Serializable]
    public sealed class ShiftCommit
    {
        public int WorldSchemaVersion = 1;
        public string SpatialProfileId = "";
        public string SimulationDefinitionHash = "", SimulationCheckpoint = "";
        public long SimulationTick;
        public SpatialFrame[] Frames = Array.Empty<SpatialFrame>();
        public CommandReceipt Receipt;
        public bool Paused;
        public ParticipantState[] Participants = Array.Empty<ParticipantState>();
        public EntityState[] Entities = Array.Empty<EntityState>();
        public TeamReport[] Reports = Array.Empty<TeamReport>();
        public ShiftCommit Copy() => new ShiftCommit { Receipt = Receipt.Copy(), Paused = Paused,
            WorldSchemaVersion = WorldSchemaVersion, SpatialProfileId = SpatialProfileId,
            SimulationDefinitionHash = SimulationDefinitionHash, SimulationCheckpoint = SimulationCheckpoint, SimulationTick = SimulationTick,
            Frames = (Frames ?? Array.Empty<SpatialFrame>()).Select(f => f.Copy()).ToArray(),
            Participants = Participants.Select(x => x.Copy()).ToArray(), Entities = Entities.Select(x => x.Copy()).ToArray(),
            Reports = Reports.Select(x => x.Copy()).ToArray() };
    }

    [Serializable] public sealed class ServerActorPose { public string ParticipantId; public SpatialPose Pose; }
    [Serializable]
    public sealed class ServerSimulationUpdate
    {
        public long Tick;
        public string DefinitionHash, Checkpoint;
        public SpatialFrame[] Frames;
        public ServerActorPose[] Actors;
        public EntityState[] Entities;
    }
    // Bounded current state, deliberately omitting historical receipt/report arrays.
    public sealed class ServerSimulationView
    {
        public long Tick, Sequence; public bool Paused; public string DefinitionHash, Checkpoint;
        public SpatialFrame[] Frames; public ParticipantState[] Participants; public EntityState[] Entities;
    }

    public interface ICommitSink
    {
        // Must durably flush before returning. Failure throws; caller must not publish acceptance.
        void Append(ShiftCommit commit);
    }
}
