using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChooGuard.Contracts.Gameplay
{
    public enum GameplayMode { Tutorial, RandomOperationsLab }
    public enum ActorRole { Citizen, Maintenance, Cleaning, PassengerService, StationStaff }
    public enum EntityKind { Actor, Zone, Exit, Door, Equipment, Tool, Part, Consumable, Surface, Container, PersonalItem, SuspiciousItem, Socket, Fastener, Sign }
    public enum ActionVerb { Observe, Wait, MoveTo, PickUp, PutDown, Open, Close, Isolate, Install, Remove, Fasten, Unfasten, Verify, Measure, Clean, Refill, DisposeWaste, Report, RequestHelp, Consent, Escort, Handoff, Cordon, Cancel }
    public enum ActionPhase { Requested, Reserved, Approaching, Executing, Verifying, Completed, Blocked, Cancelled, Failed }
    public enum ObservationSource { Sight, Instrument, Heard, OwnAction, Assignment }
    public enum GoalKind { KeepSchedule, InspectEquipment, RestoreCleanliness, AssistPerson, MaintainAccess, SeekSafety, HonourPromise }
    public enum FutureStatus { Pending, Complete, Truncated, Stale, Unavailable, Incomplete }

    public readonly struct WorldPoint
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public WorldPoint(float x, float y, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || float.IsNaN(z) || float.IsInfinity(z))
                throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y; Z = z;
        }
        public float DistanceSquared(WorldPoint other)
        { float x = X - other.X, y = Y - other.Y, z = Z - other.Z; return x * x + y * y + z * z; }
    }

    public readonly struct EntityReadVersion
    {
        public StableId EntityId { get; }
        public long Revision { get; }
        public EntityReadVersion(StableId entityId, long revision)
        { EntityId = GameplayGuard.Id(entityId); Revision = GameplayGuard.Nonnegative(revision); }
    }

    /// <summary>Immutable semantic entity. Missing measurement/fact remains unknown, never zero/normal.</summary>
    public sealed class WorldEntity
    {
        public StableId Id { get; }
        public string Label { get; }
        public EntityKind Kind { get; }
        public long Revision { get; }
        public WorldPoint Position { get; }
        public StableId ZoneId { get; }
        public StableId? CustodianId { get; }
        public StableId? ParentId { get; }
        public string DefinitionId { get; }
        public IReadOnlyDictionary<string, RuleTruth> Facts { get; }
        public IReadOnlyDictionary<string, SiValue> Measurements { get; }
        public WorldEntity(StableId id, string label, EntityKind kind, long revision, WorldPoint position,
            StableId zoneId, StableId? custodianId = null, StableId? parentId = null, string definitionId = null,
            IDictionary<string, RuleTruth> facts = null, IDictionary<string, SiValue> measurements = null)
        {
            Id = GameplayGuard.Id(id); Label = label ?? throw new ArgumentNullException(nameof(label));
            Kind = kind; Revision = GameplayGuard.Nonnegative(revision); Position = position; ZoneId = GameplayGuard.Id(zoneId);
            if (custodianId.HasValue) GameplayGuard.Id(custodianId.Value);
            if (parentId.HasValue) GameplayGuard.Id(parentId.Value);
            CustodianId = custodianId; ParentId = parentId; DefinitionId = definitionId ?? kind.ToString();
            Facts = new ReadOnlyDictionary<string, RuleTruth>(facts == null ? new Dictionary<string, RuleTruth>(StringComparer.Ordinal) : new Dictionary<string, RuleTruth>(facts, StringComparer.Ordinal));
            Measurements = new ReadOnlyDictionary<string, SiValue>(measurements == null ? new Dictionary<string, SiValue>(StringComparer.Ordinal) : new Dictionary<string, SiValue>(measurements, StringComparer.Ordinal));
        }
        private WorldEntity(WorldEntity source, long revision, WorldPoint position, StableId? custodian, StableId? parent,
            IDictionary<string, RuleTruth> facts, IDictionary<string, SiValue> measurements)
        {
            Id = source.Id; Label = source.Label; Kind = source.Kind; Revision = GameplayGuard.Nonnegative(revision);
            Position = position; ZoneId = source.ZoneId; CustodianId = custodian; ParentId = parent; DefinitionId = source.DefinitionId;
            Facts = facts == null ? source.Facts : new ReadOnlyDictionary<string, RuleTruth>(new Dictionary<string, RuleTruth>(facts, StringComparer.Ordinal));
            Measurements = measurements == null ? source.Measurements : new ReadOnlyDictionary<string, SiValue>(new Dictionary<string, SiValue>(measurements, StringComparer.Ordinal));
        }
        public RuleTruth Fact(string key) => Facts.TryGetValue(key, out var value) ? value : RuleTruth.UNKNOWN;
        public SiValue? Measurement(string key) => Measurements.TryGetValue(key, out var value) ? value : (SiValue?)null;
        public WorldEntity With(long revision, WorldPoint? position = null, IDictionary<string, RuleTruth> facts = null,
            IDictionary<string, SiValue> measurements = null, StableId? custodianId = null, bool replaceCustodian = false,
            StableId? parentId = null, bool replaceParent = false)
        {
            return new WorldEntity(this, revision, position ?? Position,
                replaceCustodian ? custodianId : CustodianId, replaceParent ? parentId : ParentId, facts, measurements);
        }
    }

    public sealed class ActorObservation
    {
        public StableId OwnerId { get; }
        public StableId EntityId { get; }
        public long EntityRevision { get; }
        public SimTick Tick { get; }
        public string FactId { get; }
        public RuleTruth Value { get; }
        public ObservationSource Source { get; }
        public StableId? InformantId { get; }
        public ActorObservation(StableId ownerId, StableId entityId, long entityRevision, SimTick tick,
            string factId, RuleTruth value, ObservationSource source, StableId? informantId = null)
        {
            OwnerId = GameplayGuard.Id(ownerId); EntityId = GameplayGuard.Id(entityId);
            EntityRevision = GameplayGuard.Nonnegative(entityRevision); Tick = tick;
            FactId = factId ?? throw new ArgumentNullException(nameof(factId)); Value = value; Source = source; InformantId = informantId;
        }
    }

    public sealed class GoalPlan
    {
        public StableId Id { get; }
        public GoalKind Kind { get; }
        public StableId TargetId { get; }
        public long Revision { get; }
        public string Reason { get; }
        public string CompletionFact { get; }
        public string BlockedReason { get; }
        public long ResumeAfterKnowledgeRevision { get; }
        public IReadOnlyList<ActionVerb> RemainingSteps { get; }
        public GoalPlan(StableId id, GoalKind kind, StableId targetId, long revision, string reason,
            string completionFact, IEnumerable<ActionVerb> remainingSteps, string blockedReason = null, long resumeAfterKnowledgeRevision = -1)
        {
            Id = GameplayGuard.Id(id); Kind = kind; TargetId = GameplayGuard.Id(targetId); Revision = GameplayGuard.Nonnegative(revision);
            Reason = reason ?? throw new ArgumentNullException(nameof(reason)); CompletionFact = completionFact;
            BlockedReason = blockedReason; ResumeAfterKnowledgeRevision = resumeAfterKnowledgeRevision;
            RemainingSteps = new List<ActionVerb>(remainingSteps ?? Array.Empty<ActionVerb>()).AsReadOnly();
        }
    }

    public sealed class ActorState
    {
        public StableId Id { get; }
        public ActorRole Role { get; }
        public long RoleRevision { get; }
        public long KnowledgeRevision { get; }
        public long DecisionSequence { get; }
        public long ActionSequence { get; }
        public bool IsHuman { get; }
        public string Persona { get; }
        public GoalPlan Plan { get; }
        public IReadOnlyList<ActorObservation> Memories { get; }
        public IReadOnlyDictionary<GoalKind, double> Drives { get; }
        public IReadOnlyList<WorldEntity> KnownEntities { get; }
        public ActorState(StableId id, ActorRole role, long roleRevision, long knowledgeRevision, long decisionSequence,
            bool isHuman, string persona, GoalPlan plan = null, IEnumerable<ActorObservation> memories = null,
            IDictionary<GoalKind, double> drives = null, long actionSequence = 0, IEnumerable<WorldEntity> knownEntities = null)
            : this(id, role, roleRevision, knowledgeRevision, decisionSequence, isHuman, persona, plan,
                CopyMemories(id, memories), CopyDrives(drives), actionSequence, CopyKnowledge(knownEntities), true) { }
        private ActorState(StableId id, ActorRole role, long roleRevision, long knowledgeRevision, long decisionSequence,
            bool isHuman, string persona, GoalPlan plan, IReadOnlyList<ActorObservation> memories,
            IReadOnlyDictionary<GoalKind, double> drives, long actionSequence, IReadOnlyList<WorldEntity> knownEntities, bool owned)
        {
            Id = GameplayGuard.Id(id); Role = role; RoleRevision = GameplayGuard.Nonnegative(roleRevision);
            KnowledgeRevision = GameplayGuard.Nonnegative(knowledgeRevision); DecisionSequence = GameplayGuard.Nonnegative(decisionSequence);
            ActionSequence = GameplayGuard.Nonnegative(actionSequence);
            IsHuman = isHuman; Persona = persona ?? string.Empty; Plan = plan;
            Memories = memories; Drives = drives; KnownEntities = knownEntities;
        }
        private static IReadOnlyList<ActorObservation> CopyMemories(StableId id, IEnumerable<ActorObservation> memories)
        {
            var memory = new List<ActorObservation>(memories ?? Array.Empty<ActorObservation>());
            if (memory.Count > 64) throw new ArgumentException("최근 기억 cache 한도는 64개입니다.", nameof(memories));
            foreach (var observation in memory) if (observation == null || !observation.OwnerId.Equals(id)) throw new ArgumentException("다른 인물의 비공개 기억입니다.", nameof(memories));
            return memory.AsReadOnly();
        }
        private static IReadOnlyDictionary<GoalKind, double> CopyDrives(IDictionary<GoalKind, double> drives)
        {
            var needs = drives == null ? new Dictionary<GoalKind, double>() : new Dictionary<GoalKind, double>(drives);
            foreach (var pair in needs) if (double.IsNaN(pair.Value) || double.IsInfinity(pair.Value) || pair.Value < 0 || pair.Value > 1) throw new ArgumentOutOfRangeException(nameof(drives));
            return new ReadOnlyDictionary<GoalKind, double>(needs);
        }
        private static IReadOnlyList<WorldEntity> CopyKnowledge(IEnumerable<WorldEntity> knownEntities)
        {
            var knowledge = new List<WorldEntity>(knownEntities ?? Array.Empty<WorldEntity>());
            if (knowledge.Count > 64) throw new ArgumentException("개인 관측 투영 한도는 64개입니다.", nameof(knownEntities));
            var ids = new HashSet<StableId>();
            foreach (var entity in knowledge)
                if (entity == null || !ids.Add(entity.Id)) throw new ArgumentException("중복되거나 없는 개인 관측 대상입니다.", nameof(knownEntities));
            return knowledge.AsReadOnly();
        }
        public ActorState With(ActorRole? role = null, long? roleRevision = null, long? knowledgeRevision = null,
            long? decisionSequence = null, GoalPlan plan = null, bool replacePlan = false,
            IEnumerable<ActorObservation> memories = null, IDictionary<GoalKind, double> drives = null, long? actionSequence = null,
            IEnumerable<WorldEntity> knownEntities = null)
        {
            return new ActorState(Id, role ?? Role, roleRevision ?? RoleRevision, knowledgeRevision ?? KnowledgeRevision,
                decisionSequence ?? DecisionSequence, IsHuman, Persona, replacePlan ? plan : Plan,
                memories == null ? Memories : CopyMemories(Id, memories), drives == null ? Drives : CopyDrives(drives),
                actionSequence ?? ActionSequence, knownEntities == null ? KnownEntities : CopyKnowledge(knownEntities), true);
        }
    }

    public sealed class ActorActionIntent
    {
        public StableId RunId { get; }
        public long Generation { get; }
        public StableId ActorId { get; }
        public StableId IntentId { get; }
        public long IntentSequence { get; }
        public ActionVerb Verb { get; }
        public StableId TargetId { get; }
        public StableId? ToolId { get; }
        public StableId? RecipientId { get; }
        public long RoleRevision { get; }
        public SimTick ExpiresAt { get; }
        public IReadOnlyList<EntityReadVersion> ReadSet { get; }
        public string WorkPointId { get; }
        public ActorActionIntent(StableId runId, long generation, StableId actorId, StableId intentId, long intentSequence,
            ActionVerb verb, StableId targetId, long roleRevision, SimTick expiresAt,
            IEnumerable<EntityReadVersion> readSet, StableId? toolId = null, StableId? recipientId = null, string workPointId = null)
        {
            RunId = GameplayGuard.Id(runId); Generation = GameplayGuard.Nonnegative(generation); ActorId = GameplayGuard.Id(actorId);
            IntentSequence = GameplayGuard.Nonnegative(intentSequence);
            IntentId = GameplayGuard.Id(intentId); Verb = verb; TargetId = GameplayGuard.Id(targetId);
            RoleRevision = GameplayGuard.Nonnegative(roleRevision); ExpiresAt = expiresAt; ToolId = toolId; RecipientId = recipientId;
            WorkPointId = workPointId; ReadSet = new List<EntityReadVersion>(readSet ?? throw new ArgumentNullException(nameof(readSet))).AsReadOnly();
        }
    }

    /// <summary>Evidence is submitted by the designated motion/contact adapter, never by model text.</summary>
    public sealed class ActionEvidence
    {
        public WorldPoint ActorPosition { get; }
        public WorldPoint ContactPosition { get; }
        public bool Visible { get; }
        public bool Contact { get; }
        public bool Supported { get; }
        public bool Aligned { get; }
        public double Work { get; }
        public long TargetRevision { get; }
        /// <summary>Actual supporting collider's entity; null for unbound floor, never inferred from proximity.</summary>
        public StableId? SupportId { get; }
        public ActionEvidence(WorldPoint actorPosition, WorldPoint contactPosition, bool visible, bool contact,
            bool supported, bool aligned, double work, long targetRevision, StableId? supportId = null)
        {
            if (double.IsNaN(work) || double.IsInfinity(work) || work < 0 || work > 1) throw new ArgumentOutOfRangeException(nameof(work));
            ActorPosition = actorPosition; ContactPosition = contactPosition; Visible = visible; Contact = contact;
            Supported = supported; Aligned = aligned; Work = work; TargetRevision = GameplayGuard.Nonnegative(targetRevision);
            SupportId = supportId.HasValue ? GameplayGuard.Id(supportId.Value) : (StableId?)null;
        }
    }

    public sealed class ActionReceipt
    {
        public StableId ActorId { get; }
        public StableId IntentId { get; }
        public long Generation { get; }
        public long Sequence { get; }
        public ActionPhase Phase { get; }
        public string Reason { get; }
        public bool ChangedWorld { get; }
        public ActionReceipt(StableId actorId, StableId intentId, long generation, long sequence, ActionPhase phase, string reason, bool changedWorld)
        { ActorId = actorId; IntentId = intentId; Generation = generation; Sequence = sequence; Phase = phase; Reason = reason; ChangedWorld = changedWorld; }
    }

    public sealed class WorldSnapshot
    {
        public StableId RunId { get; }
        public long Generation { get; }
        public long Revision { get; }
        public SimTick Tick { get; }
        public GameplayMode Mode { get; }
        public string RulesetId { get; }
        public long RulesetRevision { get; }
        public string SnapshotId { get; }
        public IReadOnlyDictionary<StableId, WorldEntity> Entities { get; }
        public IReadOnlyDictionary<StableId, ActorState> Actors { get; }
        public WorldSnapshot(StableId runId, long generation, long revision, SimTick tick, GameplayMode mode,
            string rulesetId, long rulesetRevision, IDictionary<StableId, WorldEntity> entities, IDictionary<StableId, ActorState> actors)
        {
            RunId = GameplayGuard.Id(runId); Generation = GameplayGuard.Nonnegative(generation); Revision = GameplayGuard.Nonnegative(revision);
            Tick = tick; Mode = mode; RulesetId = rulesetId ?? throw new ArgumentNullException(nameof(rulesetId)); RulesetRevision = GameplayGuard.Nonnegative(rulesetRevision);
            SnapshotId = "snapshot-" + Guid.NewGuid().ToString("N");
            Entities = new ReadOnlyDictionary<StableId, WorldEntity>(new Dictionary<StableId, WorldEntity>(entities));
            Actors = new ReadOnlyDictionary<StableId, ActorState>(new Dictionary<StableId, ActorState>(actors));
        }
    }

    public sealed class GroundedCandidate
    {
        public string CandidateId { get; }
        public string TransitionId { get; }
        public string ParameterSetId { get; }
        public StableId? ActorId { get; }
        public StableId TargetId { get; }
        public StableId SourceId { get; }
        public StableId? ToolId { get; }
        public StableId? RecipientId { get; }
        public ActionVerb? Verb { get; }
        public string Summary { get; }
        public IReadOnlyList<EntityReadVersion> ReadSet { get; }
        public IReadOnlyList<string> CauseFactIds { get; }
        public GroundedCandidate(string candidateId, string transitionId, string parameterSetId, StableId? actorId,
            StableId targetId, string summary, IEnumerable<EntityReadVersion> readSet, IEnumerable<string> causeFactIds,
            ActionVerb? verb = null, StableId? toolId = null, StableId? recipientId = null, StableId? sourceId = null)
        {
            CandidateId = candidateId ?? throw new ArgumentNullException(nameof(candidateId)); TransitionId = transitionId ?? throw new ArgumentNullException(nameof(transitionId));
            ParameterSetId = parameterSetId ?? throw new ArgumentNullException(nameof(parameterSetId)); ActorId = actorId; TargetId = GameplayGuard.Id(targetId);
            SourceId = sourceId ?? targetId;
            Summary = summary ?? string.Empty; ReadSet = new List<EntityReadVersion>(readSet ?? Array.Empty<EntityReadVersion>()).AsReadOnly();
            CauseFactIds = new List<string>(causeFactIds ?? Array.Empty<string>()).AsReadOnly(); Verb = verb; ToolId = toolId; RecipientId = recipientId;
        }
    }

    public sealed class FutureStepRecord
    {
        public int Depth { get; }
        public GroundedCandidate Candidate { get; }
        public string RequestSha256 { get; }
        public string Model { get; }
        public FutureStepRecord(int depth, GroundedCandidate candidate, string requestSha256, string model)
        { Depth = depth; Candidate = candidate; RequestSha256 = requestSha256; Model = model; }
    }

    public sealed class FutureBranch
    {
        public string ForecastId { get; }
        public string BranchId { get; }
        public WorldSnapshot Basis { get; }
        public WorldSnapshot State { get; internal set; }
        public FutureStatus Status { get; internal set; }
        public string Reason { get; internal set; }
        public SimTick HorizonEnd { get; }
        public IReadOnlyList<FutureStepRecord> Steps => steps;
        private readonly List<FutureStepRecord> steps = new List<FutureStepRecord>();
        public FutureBranch(string forecastId, string branchId, WorldSnapshot basis, SimTick horizonEnd)
        { ForecastId = forecastId; BranchId = branchId; Basis = basis; State = basis; HorizonEnd = horizonEnd; Status = FutureStatus.Pending; }
        public void Record(FutureStepRecord step, WorldSnapshot state, FutureStatus status, string reason)
        { steps.Add(step); State = state; Status = status; Reason = reason; }
        public void Stop(FutureStatus status, string reason) { Status = status; Reason = reason; }
    }

    public sealed class WorldMutation
    {
        public StableId RunId { get; }
        public long Generation { get; }
        public long PreviousRevision { get; }
        public long Revision { get; }
        public SimTick Tick { get; }
        public string Kind { get; }
        public StableId? ActorId { get; }
        public string Detail { get; }
        public IReadOnlyList<WorldEntity> Entities { get; }
        public IReadOnlyList<ActorState> Actors { get; }
        public ActorActionIntent Intent { get; }
        public ActionReceipt Receipt { get; }
        public WorldMutation(StableId runId, long generation, long previousRevision, SimTick tick, string kind,
            StableId? actorId, string detail, IEnumerable<WorldEntity> entities, IEnumerable<ActorState> actors,
            ActorActionIntent intent = null, ActionReceipt receipt = null)
        {
            RunId = runId; Generation = generation; PreviousRevision = previousRevision; Revision = checked(previousRevision + 1);
            Tick = tick; Kind = kind; ActorId = actorId; Detail = detail;
            Intent = intent; Receipt = receipt;
            Entities = new List<WorldEntity>(entities ?? Array.Empty<WorldEntity>()).AsReadOnly();
            Actors = new List<ActorState>(actors ?? Array.Empty<ActorState>()).AsReadOnly();
        }
    }

    public interface IGameplayCommitSink
    {
        // Throw before return on failure. Publication follows durable commit; only changed records are copied.
        void Commit(WorldMutation mutation);
    }

    internal static class GameplayGuard
    {
        internal static StableId Id(StableId id) { if (!id.IsValid) throw new ArgumentException("초기화된 ID가 필요합니다."); return id; }
        internal static long Nonnegative(long value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    }
}
