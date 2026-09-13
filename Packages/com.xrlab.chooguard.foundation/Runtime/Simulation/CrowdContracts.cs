using System;
using System.Linq;

namespace ChooGuard.Foundation.Simulation
{
    [Serializable]
    public struct CrowdVector
    {
        public double X, Y;
        public CrowdVector(double x, double y) { X = x; Y = y; }
        public double LengthSquared => X * X + Y * Y;
        public double Length => Math.Sqrt(LengthSquared);
        public static CrowdVector operator +(CrowdVector a, CrowdVector b) => new CrowdVector(a.X + b.X, a.Y + b.Y);
        public static CrowdVector operator -(CrowdVector a, CrowdVector b) => new CrowdVector(a.X - b.X, a.Y - b.Y);
        public static CrowdVector operator *(CrowdVector a, double b) => new CrowdVector(a.X * b, a.Y * b);
        public static CrowdVector operator /(CrowdVector a, double b) => new CrowdVector(a.X / b, a.Y / b);
        public static double Dot(CrowdVector a, CrowdVector b) => a.X * b.X + a.Y * b.Y;
        public static double Cross(CrowdVector a, CrowdVector b) => a.X * b.Y - a.Y * b.X;
    }

    [Serializable]
    public sealed class CrowdParameters
    {
        public double TimeGapSeconds = 1, PersonRepulsion = 5, PersonRepulsionRangeM = .1;
        public double WallRepulsion = 5, WallRepulsionRangeM = .02;
        public double MaxSubstepSeconds = .01, VelocityToleranceMS = 1e-8, ComplementarityTolerance = 1e-9, GeometryToleranceM = 1e-9;
        public int MaxAgents = 100, MaxProjectionIterations = 16384, MaxSubstepsPerTick = 4096, MaxActiveSetConstraints = 200;
        public CrowdParameters Copy() => (CrowdParameters)MemberwiseClone();
    }

    [Serializable]
    public sealed class CrowdSpace
    {
        // Same space means a shared metric contact plane. Region/frame labels alone never isolate contact.
        public string Id;
        // Optional numerical corridor fixture: X is periodic, Y unbounded, walls forbidden in this space.
        public double PeriodicLengthXM;
        public CrowdSpace Copy() => (CrowdSpace)MemberwiseClone();
    }

    [Serializable]
    public sealed class CrowdWall
    {
        public string Id, ContactSpaceId;
        public CrowdVector A, B;
        public bool Enabled = true, HasVerticalBounds;
        public double BottomElevationM, TopElevationM;
        public CrowdWall Copy() => (CrowdWall)MemberwiseClone();
    }

    [Serializable]
    public sealed class CrowdDefinition
    {
        public int SchemaVersion = 1;
        public string ProfileId;
        public CrowdParameters Parameters = new CrowdParameters();
        public CrowdSpace[] Spaces = Array.Empty<CrowdSpace>();
        public CrowdWall[] Walls = Array.Empty<CrowdWall>();
        public CrowdDefinition Copy() => new CrowdDefinition { SchemaVersion = SchemaVersion, ProfileId = ProfileId,
            Parameters = Parameters?.Copy(), Spaces = Spaces?.Select(s => s?.Copy()).ToArray(), Walls = Walls?.Select(w => w?.Copy()).ToArray() };
    }

    public enum CrowdIntentMode { Goal, DesiredVelocity }

    [Serializable]
    public sealed class CrowdMovementWindow
    {
        // Ephemeral constraint derived from the authoritative support geometry for one advance.
        // It constrains the body centre, not a wall or an altered physical body radius.
        public string AgentId;
        public double MinX, MaxX, MinY, MaxY;
    }

    [Serializable]
    public sealed class CrowdIntent
    {
        public string AgentId;
        public CrowdIntentMode Mode;
        public CrowdVector Goal, DesiredVelocity;
    }

    [Serializable]
    public sealed class CrowdAgent
    {
        public string Id, ContactSpaceId, RegionId, FrameId, SurfaceId;
        // Effective circular footprint. The Unity body/sweep adapter must use this exact radius.
        public double RadiusM = .15, PreferredSpeedMS = 1.2;
        public CrowdVector Position, Velocity, Goal, DesiredVelocity;
        // Upright cylinder envelope; SupportGradient gives d elevation / d horizontal contact-space coordinate.
        public double FootElevationM, HeightM = 1.8;
        public CrowdVector SupportGradient;
        public bool Pinned;
        public CrowdIntentMode IntentMode;
        // Opaque navigation/replay information: the motion solver does not invent routes or railway procedures.
        public string LeaderId = "";
        public string[] KnownClosedPortalIds = Array.Empty<string>();
        public int PathCursor;
        public CrowdAgent Copy()
        { var copy = (CrowdAgent)MemberwiseClone(); copy.KnownClosedPortalIds = KnownClosedPortalIds?.ToArray(); return copy; }
    }

    /// <summary>Detached mutable-body and clock read; deliberately has no geometry definition and is not a checkpoint.</summary>
    public sealed class CrowdReadState
    {
        public CrowdAgent[] Agents;
        public long Seed, Tick, AcceptedSubsteps, GeometryRevision;
        public double ElapsedSeconds;
    }

    [Serializable]
    public sealed class CrowdSnapshot
    {
        public int SchemaVersion = 2;
        public string ModelVersion = CrowdMotionModel.Version;
        public CrowdDefinition Definition;
        public CrowdAgent[] Agents;
        public long Seed, Tick, AcceptedSubsteps, GeometryRevision;
        public double ElapsedSeconds;
        public CrowdSnapshot Copy() => new CrowdSnapshot { SchemaVersion = SchemaVersion, ModelVersion = ModelVersion,
            Definition = Definition?.Copy(), Agents = Agents?.Select(a => a?.Copy()).ToArray(),
            Seed = Seed, Tick = Tick, AcceptedSubsteps = AcceptedSubsteps, GeometryRevision = GeometryRevision, ElapsedSeconds = ElapsedSeconds };
    }

    [Serializable]
    public sealed class CrowdBodyBinding
    {
        public string AgentId, ContactSpaceId, RegionId, FrameId, SurfaceId;
        public CrowdVector Position, Velocity, Goal, DesiredVelocity, SupportGradient;
        public double FootElevationM;
        public bool Pinned;
        public static CrowdBodyBinding FromAgent(CrowdAgent a) => new CrowdBodyBinding { AgentId = a.Id,
            ContactSpaceId = a.ContactSpaceId, RegionId = a.RegionId, FrameId = a.FrameId, SurfaceId = a.SurfaceId,
            Position = a.Position, Velocity = a.Velocity, Goal = a.Goal, DesiredVelocity = a.DesiredVelocity,
            SupportGradient = a.SupportGradient, FootElevationM = a.FootElevationM, Pinned = a.Pinned };
    }

    [Serializable]
    public sealed class CrowdRebind
    {
        // Trusted server geometry operation, distinct from state restore. Bodies must name the complete existing roster.
        public long ExpectedTick, ExpectedGeometryRevision;
        public CrowdSpace[] Spaces;
        public CrowdWall[] Walls;
        public CrowdBodyBinding[] Bodies;
    }

    [Serializable]
    public sealed class CrowdStepReport
    {
        public bool Committed;
        public string Failure = "";
        public int AttemptedSubsteps, MaximumProjectionIterations, MaximumConstraintCount, ActiveSetSolves;
        public double MinimumSweptGapM, MaximumConstraintViolationMS, MaximumComplementarityResidual, MaximumStationarityResidualMS;
        // Dual variables have speed units in this unweighted projection, not force, pressure, or injury units.
        public double MaximumDualMultiplierMS;
        public double MaximumGroundSpeedMS;
    }
}
