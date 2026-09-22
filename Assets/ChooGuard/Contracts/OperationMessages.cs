using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChooGuard.Contracts
{
    // K02 운영 포트용 immutable 메모리 DTO. JSON ingestion/직렬화 계층이 아니며,
    // 중복 JSON key·알 수 없는 필드·wire enum 표기는 향후 wire adapter가 검증해야 한다.
    public sealed class ReceiptKey : IEquatable<ReceiptKey>
    {
        public StableId RunId { get; }
        public StableId RequesterId { get; }
        public StableId IntentId { get; }
        public ReceiptKey(StableId runId, StableId requesterId, StableId intentId)
        {
            RunId = ContractGuard.Id(runId, nameof(runId));
            RequesterId = ContractGuard.Id(requesterId, nameof(requesterId));
            IntentId = ContractGuard.Id(intentId, nameof(intentId));
        }
        public bool Equals(ReceiptKey other) => other != null && RunId.Equals(other.RunId) && RequesterId.Equals(other.RequesterId) && IntentId.Equals(other.IntentId);
        public override bool Equals(object obj) => Equals(obj as ReceiptKey);
        public override int GetHashCode() { unchecked { return (RunId.GetHashCode() * 397 ^ RequesterId.GetHashCode()) * 397 ^ IntentId.GetHashCode(); } }
    }

    public sealed class ContentReference
    {
        public StableId Id { get; }
        public long Revision { get; }
        public string Sha256 { get; }
        public ContentReference(StableId id, long revision, string sha256)
        {
            Id = ContractGuard.Id(id, nameof(id));
            Revision = ContractGuard.Nonnegative(revision, nameof(revision));
            Sha256 = MessageGuard.Hash(sha256, nameof(sha256));
        }
    }

    public sealed class EntityRevision
    {
        public StableId EntityId { get; }
        public long Revision { get; }
        public EntityRevision(StableId entityId, long revision)
        {
            EntityId = ContractGuard.Id(entityId, nameof(entityId));
            Revision = ContractGuard.Nonnegative(revision, nameof(revision));
        }
    }

    public enum ReceiptStatus { ACCEPTED, REJECTED, CONFLICT }
    public enum TargetStatus { ACCEPTED, REJECTED, QUEUED }
    public enum ReasonAxis { lifecycle, authority, knowledge, resources, mobility, safety, evidence }
    public enum ViewScope { AUTHOR_ANALYSIS, AGENCY_KNOWLEDGE }

    public sealed class Reason
    {
        public StableId Code { get; }
        public ReasonAxis Axis { get; }
        public StableId SubjectId { get; }
        public IReadOnlyList<ContentReference> EvidenceRefs { get; }
        public string Message { get; }
        public Reason(StableId code, ReasonAxis axis, StableId subjectId, IEnumerable<ContentReference> evidenceRefs, string message)
        {
            Code = ContractGuard.Id(code, nameof(code));
            Axis = ContractGuard.Defined(axis, nameof(axis));
            SubjectId = ContractGuard.Id(subjectId, nameof(subjectId));
            EvidenceRefs = MessageGuard.Copy(evidenceRefs, 0, nameof(evidenceRefs));
            if (string.IsNullOrEmpty(message) || message.Length > 8192) throw new ArgumentException("이유 메시지는 1~8192자여야 합니다.", nameof(message));
            Message = message;
        }
    }

    public sealed class TargetResult
    {
        public StableId TargetId { get; }
        public TargetStatus Status { get; }
        public IReadOnlyList<Reason> Reasons { get; }
        public TargetResult(StableId targetId, TargetStatus status, IEnumerable<Reason> reasons)
        {
            TargetId = ContractGuard.Id(targetId, nameof(targetId));
            Status = ContractGuard.Defined(status, nameof(status));
            Reasons = MessageGuard.Copy(reasons, 0, nameof(reasons));
        }
    }

    public sealed class CommandIntent
    {
        public ReceiptKey Key { get; }
        public StableId ActingAgencyId { get; }
        public IReadOnlyList<StableId> ActingTeamIds { get; }
        public StableId ActionId { get; }
        public IReadOnlyList<StableId> TargetIds { get; }
        public IReadOnlyList<EntityRevision> ReadSet { get; }
        public ContentReference PayloadRef { get; }
        public UtcTimestamp AuthoredAt { get; }
        public CommandIntent(ReceiptKey key, StableId actingAgencyId, IEnumerable<StableId> actingTeamIds, StableId actionId,
            IEnumerable<StableId> targetIds, IEnumerable<EntityRevision> readSet, ContentReference payloadRef, UtcTimestamp authoredAt)
        {
            Key = ContractGuard.NotNull(key, nameof(key));
            ActingAgencyId = ContractGuard.Id(actingAgencyId, nameof(actingAgencyId));
            ActingTeamIds = MessageGuard.Ids(actingTeamIds, nameof(actingTeamIds));
            ActionId = ContractGuard.Id(actionId, nameof(actionId));
            TargetIds = MessageGuard.Ids(targetIds, nameof(targetIds));
            ReadSet = MessageGuard.Copy(readSet, 1, nameof(readSet));
            PayloadRef = ContractGuard.NotNull(payloadRef, nameof(payloadRef));
            AuthoredAt = authoredAt;
        }
    }

    public sealed class CommandReceipt
    {
        public ReceiptKey Key { get; }
        public string Fingerprint { get; }
        public ReceiptStatus Status { get; }
        public string CommitId { get; }
        public Sequence? Sequence { get; }
        public IReadOnlyList<TargetResult> TargetResults { get; }
        public CommandReceipt(ReceiptKey key, string fingerprint, ReceiptStatus status, string commitId, Sequence? sequence, IEnumerable<TargetResult> targetResults)
        {
            Key = ContractGuard.NotNull(key, nameof(key));
            Fingerprint = MessageGuard.Hash(fingerprint, nameof(fingerprint));
            Status = ContractGuard.Defined(status, nameof(status));
            if (status == ReceiptStatus.ACCEPTED && (string.IsNullOrEmpty(commitId) || !sequence.HasValue))
                throw new ArgumentException("ACCEPTED에는 commitId와 sequence가 필요합니다.");
            if (commitId != null) new StableId(commitId); // K02의 공통 ID 규칙은 nullable commitId에도 적용한다.
            CommitId = commitId;
            Sequence = sequence;
            TargetResults = MessageGuard.Copy(targetResults, 1, nameof(targetResults));
        }
    }

    public sealed class ReceiptLookup
    {
        public bool Found { get; }
        public CommandReceipt Receipt { get; }
        public ReceiptLookup(bool found, CommandReceipt receipt)
        {
            if (found != (receipt != null)) throw new ArgumentException("found와 receipt의 null 여부가 일치해야 합니다.");
            Found = found; Receipt = receipt;
        }
    }

    public sealed class PreviewResult
    {
        public ReceiptKey Key { get; }
        public IReadOnlyList<EntityRevision> ReadSet { get; }
        public IReadOnlyList<TargetResult> TargetResults { get; }
        public ContentReference ProposedEffectsRef { get; }
        public long PreviewExpiresAtRevision { get; }
        public PreviewResult(ReceiptKey key, IEnumerable<EntityRevision> readSet, IEnumerable<TargetResult> targetResults, ContentReference proposedEffectsRef, long previewExpiresAtRevision)
        {
            Key = ContractGuard.NotNull(key, nameof(key));
            ReadSet = MessageGuard.Copy(readSet, 1, nameof(readSet));
            TargetResults = MessageGuard.Copy(targetResults, 1, nameof(targetResults));
            ProposedEffectsRef = ContractGuard.NotNull(proposedEffectsRef, nameof(proposedEffectsRef));
            PreviewExpiresAtRevision = ContractGuard.Nonnegative(previewExpiresAtRevision, nameof(previewExpiresAtRevision));
        }
    }

    public sealed class ProjectionQuery
    {
        public StableId RunId { get; }
        public StableId RequesterId { get; }
        public ViewScope ViewScope { get; }
        public StableId? AgencyId { get; }
        public long AfterRevision { get; }
        public ProjectionQuery(StableId runId, StableId requesterId, ViewScope viewScope, StableId? agencyId, long afterRevision)
        {
            RunId = ContractGuard.Id(runId, nameof(runId));
            RequesterId = ContractGuard.Id(requesterId, nameof(requesterId));
            ViewScope = ContractGuard.Defined(viewScope, nameof(viewScope));
            AgencyId = MessageGuard.OptionalId(agencyId, nameof(agencyId));
            AfterRevision = ContractGuard.Nonnegative(afterRevision, nameof(afterRevision));
        }
    }

    public sealed class SessionProjection
    {
        public StableId RunId { get; }
        public long Revision { get; }
        public SimTick SimTick { get; }
        public ViewScope ViewScope { get; }
        public StableId? AgencyId { get; }
        public ContentReference EntitiesRef { get; }
        public ContentReference TasksRef { get; }
        public ContentReference ReasonsRef { get; }
        public SessionProjection(StableId runId, long revision, SimTick simTick, ViewScope viewScope, StableId? agencyId,
            ContentReference entitiesRef, ContentReference tasksRef, ContentReference reasonsRef)
        {
            RunId = ContractGuard.Id(runId, nameof(runId));
            Revision = ContractGuard.Nonnegative(revision, nameof(revision));
            SimTick = simTick;
            ViewScope = ContractGuard.Defined(viewScope, nameof(viewScope));
            AgencyId = MessageGuard.OptionalId(agencyId, nameof(agencyId));
            EntitiesRef = ContractGuard.NotNull(entitiesRef, nameof(entitiesRef));
            TasksRef = ContractGuard.NotNull(tasksRef, nameof(tasksRef));
            ReasonsRef = ContractGuard.NotNull(reasonsRef, nameof(reasonsRef));
        }
    }

    public enum ActivityCategory { SETUP, AUTHORING, REVIEW, EXPORT_REWORK, ATTENDED_WAIT, UNATTENDED_COMPUTE, EXTERNAL_REPLY_WAIT }
    public enum ActivityCompleteness { COMPLETE, CENSORED }
    public enum BranchKind { EXACT_FORK, REEXECUTE_FROM_ORIGIN }
    public enum CheckpointRestartMode { FULL_STATE, CHECKPOINT_REPLAY, FROM_ORIGIN }
    public enum WorkerRestartMode { FULL_STATE, CHECKPOINT_REPLAY, FROM_ORIGIN, NONE }
    public enum WorkerTimeMode { STEP, BATCH }
    public enum ComparisonStatus { COMPARABLE, PARTIALLY_COMPARABLE, INCOMPARABLE, INCONCLUSIVE }
    public enum EvidenceResult { NOT_RUN, FAILED, PASSED_WITH_SCOPE, INCONCLUSIVE }
    public enum ScriptClaimType { OBSERVED_IN_RUN, AUTHOR_ANNOTATION, HYPOTHESIS, MANUAL_SUPPORTED_PROPOSAL, REVIEWED_INSTRUCTION }
    public enum FieldStatus { VALID_WITHIN_PROFILE, UNSUPPORTED, FAILED }
    // In-memory vocabulary only: wire adapter must map m, m/s, K, kg/m3, Pa, 1, person explicitly.
    public enum FieldUnit { Metre, MetresPerSecond, Kelvin, KilogramsPerCubicMetre, Pascal, Dimensionless, Person }

    public sealed class ActivityInterval
    {
        public StableId ParticipantCode { get; }
        public StableId CaseId { get; }
        public StableId IntervalId { get; }
        public MonotonicTimestamp StartMonoUs { get; }
        public MonotonicTimestamp? EndMonoUs { get; }
        public ActivityCategory Category { get; }
        public bool Assistance { get; }
        public ContentReference ConsentRef { get; }
        public ActivityCompleteness Completeness { get; }
        public ActivityInterval(StableId participantCode, StableId caseId, StableId intervalId, MonotonicTimestamp startMonoUs,
            MonotonicTimestamp? endMonoUs, ActivityCategory category, bool assistance, ContentReference consentRef, ActivityCompleteness completeness)
        {
            ParticipantCode = ContractGuard.Id(participantCode, nameof(participantCode));
            CaseId = ContractGuard.Id(caseId, nameof(caseId));
            IntervalId = ContractGuard.Id(intervalId, nameof(intervalId));
            StartMonoUs = startMonoUs; EndMonoUs = endMonoUs;
            Category = ContractGuard.Defined(category, nameof(category));
            Assistance = assistance;
            ConsentRef = ContractGuard.NotNull(consentRef, nameof(consentRef));
            Completeness = ContractGuard.Defined(completeness, nameof(completeness));
            if (endMonoUs.HasValue && endMonoUs.Value.Microseconds < startMonoUs.Microseconds)
                throw new ArgumentException("Monotonic interval is reversed.");
            if (completeness == ActivityCompleteness.COMPLETE && !endMonoUs.HasValue)
                throw new ArgumentException("An open interval is censored, not complete.");
        }
    }

    public sealed class BranchRequest
    {
        public StableId IntentId { get; }
        public StableId ParentRunId { get; }
        public ContentReference CheckpointRef { get; }
        public StableId NewRunId { get; }
        public ContentReference NewPlanRef { get; }
        public BranchKind RequestedKind { get; }
        public BranchRequest(StableId intentId, StableId parentRunId, ContentReference checkpointRef, StableId newRunId,
            ContentReference newPlanRef, BranchKind requestedKind)
        {
            IntentId = ContractGuard.Id(intentId, nameof(intentId));
            ParentRunId = ContractGuard.Id(parentRunId, nameof(parentRunId));
            CheckpointRef = ContractGuard.NotNull(checkpointRef, nameof(checkpointRef));
            NewRunId = ContractGuard.Id(newRunId, nameof(newRunId));
            NewPlanRef = ContractGuard.NotNull(newPlanRef, nameof(newPlanRef));
            RequestedKind = ContractGuard.Defined(requestedKind, nameof(requestedKind));
            if (parentRunId.Equals(newRunId)) throw new ArgumentException("A branch requires a new run ID.");
        }
    }

    public sealed class CheckpointWorkerState
    {
        public StableId WorkerId { get; }
        public Sequence CutSequence { get; }
        public SimTick TickUs { get; }
        public string InputDigest { get; }
        public CheckpointRestartMode Mode { get; }
        public ContentReference StateOrRecipeRef { get; }
        public ContentReference ReplayEvidenceRef { get; }
        public CheckpointWorkerState(StableId workerId, Sequence cutSequence, SimTick tickUs, string inputDigest,
            CheckpointRestartMode mode, ContentReference stateOrRecipeRef, ContentReference replayEvidenceRef)
        {
            WorkerId = ContractGuard.Id(workerId, nameof(workerId));
            CutSequence = cutSequence; TickUs = tickUs;
            InputDigest = MessageGuard.Hash(inputDigest, nameof(inputDigest));
            Mode = ContractGuard.Defined(mode, nameof(mode));
            StateOrRecipeRef = ContractGuard.NotNull(stateOrRecipeRef, nameof(stateOrRecipeRef));
            ReplayEvidenceRef = ContractGuard.NotNull(replayEvidenceRef, nameof(replayEvidenceRef));
        }
    }

    public sealed class Checkpoint
    {
        public StableId CheckpointId { get; }
        public StableId RunId { get; }
        public Sequence CutSequence { get; }
        public SimTick TickUs { get; }
        public ContentReference CoreStateRef { get; }
        public ContentReference EventQueueRef { get; }
        public ContentReference RandomStreamsRef { get; }
        public ContentReference ContentLockRef { get; }
        public IReadOnlyList<StableId> RequiredWorkers { get; }
        public IReadOnlyList<CheckpointWorkerState> WorkerStates { get; }
        public Checkpoint(StableId checkpointId, StableId runId, Sequence cutSequence, SimTick tickUs,
            ContentReference coreStateRef, ContentReference eventQueueRef, ContentReference randomStreamsRef, ContentReference contentLockRef,
            IEnumerable<StableId> requiredWorkers, IEnumerable<CheckpointWorkerState> workerStates)
        {
            CheckpointId = ContractGuard.Id(checkpointId, nameof(checkpointId));
            RunId = ContractGuard.Id(runId, nameof(runId));
            CutSequence = cutSequence; TickUs = tickUs;
            CoreStateRef = ContractGuard.NotNull(coreStateRef, nameof(coreStateRef));
            EventQueueRef = ContractGuard.NotNull(eventQueueRef, nameof(eventQueueRef));
            RandomStreamsRef = ContractGuard.NotNull(randomStreamsRef, nameof(randomStreamsRef));
            ContentLockRef = ContractGuard.NotNull(contentLockRef, nameof(contentLockRef));
            RequiredWorkers = MessageGuard.Ids(requiredWorkers, nameof(requiredWorkers), 0);
            WorkerStates = MessageGuard.Copy(workerStates, 0, nameof(workerStates));
            var remaining = new HashSet<StableId>(RequiredWorkers);
            foreach (var state in WorkerStates)
                if (!remaining.Remove(state.WorkerId) || state.CutSequence.Value != cutSequence.Value || state.TickUs.Microseconds != tickUs.Microseconds)
                    throw new ArgumentException("Worker set, cut and tick must exactly match the checkpoint.");
            if (remaining.Count != 0) throw new ArgumentException("Required worker state is missing.");
            // Actual blob existence, expected input digest and replay fidelity require the run/backend context.
        }
    }

    public sealed class CommitBatch
    {
        public StableId RunId { get; }
        public long ExpectedRevision { get; }
        public ContentReference EventsRef { get; }
        public ContentReference ReservationsRef { get; }
        public CommandReceipt Receipt { get; }
        public ContentReference OutboxRef { get; }
        public ContentReference ProjectionRef { get; }
        public CommitBatch(StableId runId, long expectedRevision, ContentReference eventsRef, ContentReference reservationsRef,
            CommandReceipt receipt, ContentReference outboxRef, ContentReference projectionRef)
        {
            RunId = ContractGuard.Id(runId, nameof(runId));
            ExpectedRevision = ContractGuard.Nonnegative(expectedRevision, nameof(expectedRevision));
            EventsRef = ContractGuard.NotNull(eventsRef, nameof(eventsRef));
            ReservationsRef = ContractGuard.NotNull(reservationsRef, nameof(reservationsRef));
            Receipt = ContractGuard.NotNull(receipt, nameof(receipt));
            OutboxRef = ContractGuard.NotNull(outboxRef, nameof(outboxRef));
            ProjectionRef = ContractGuard.NotNull(projectionRef, nameof(projectionRef));
            if (!runId.Equals(receipt.Key.RunId)) throw new ArgumentException("Receipt belongs to a different run.");
        }
    }

    /// <summary>Delivery fencing identity only; not a simulation result or effect acceptance.</summary>
    public sealed class OutboxDeliveryAck
    {
        public StableId RunId { get; }
        public StableId JobId { get; }
        public StableId CommitId { get; }
        public long Generation { get; }
        public StableId AttemptId { get; }
        public StableId OwnerId { get; }
        public OutboxDeliveryAck(StableId runId, StableId jobId, StableId commitId, long generation, StableId attemptId, StableId ownerId)
        {
            RunId = ContractGuard.Id(runId, nameof(runId));
            JobId = ContractGuard.Id(jobId, nameof(jobId));
            CommitId = ContractGuard.Id(commitId, nameof(commitId));
            Generation = ContractGuard.Nonnegative(generation, nameof(generation));
            AttemptId = ContractGuard.Id(attemptId, nameof(attemptId));
            OwnerId = ContractGuard.Id(ownerId, nameof(ownerId));
        }
        public bool Matches(OutboxDeliveryAck other) => other != null && RunId.Equals(other.RunId) &&
            JobId.Equals(other.JobId) && CommitId.Equals(other.CommitId) && Generation == other.Generation &&
            AttemptId.Equals(other.AttemptId) && OwnerId.Equals(other.OwnerId);
    }

    public sealed class OutboxDelivery
    {
        public OutboxDeliveryAck Identity { get; }
        public ContentReference OutboxRef { get; }
        public DateTimeOffset LeaseExpiresAt { get; }
        public OutboxDelivery(OutboxDeliveryAck identity, ContentReference outboxRef, DateTimeOffset leaseExpiresAt)
        {
            Identity = ContractGuard.NotNull(identity, nameof(identity));
            OutboxRef = ContractGuard.NotNull(outboxRef, nameof(outboxRef));
            LeaseExpiresAt = leaseExpiresAt;
        }
    }

    public sealed class CommitReceipt
    {
        public StableId RunId { get; }
        public StableId CommitId { get; }
        public long Revision { get; }
        public CommandReceipt IntentReceipt { get; }
        public IReadOnlyList<StableId> DispatchableJobs { get; }
        public CommitReceipt(StableId runId, StableId commitId, long revision, CommandReceipt intentReceipt, IEnumerable<StableId> dispatchableJobs)
        {
            RunId = ContractGuard.Id(runId, nameof(runId));
            CommitId = ContractGuard.Id(commitId, nameof(commitId));
            Revision = ContractGuard.Nonnegative(revision, nameof(revision));
            IntentReceipt = ContractGuard.NotNull(intentReceipt, nameof(intentReceipt));
            DispatchableJobs = MessageGuard.Ids(dispatchableJobs, nameof(dispatchableJobs), 0);
            if (!runId.Equals(intentReceipt.Key.RunId) || !StringComparer.Ordinal.Equals(commitId.Value, intentReceipt.CommitId) || !intentReceipt.Sequence.HasValue)
                throw new ArgumentException("Receipt run, commit ID and committed sequence are required.");
            // Schema has no outer sequence. Do not equate event sequence with projection revision.
            // The store must compare IntentReceipt.Sequence against its actual committed event sequence.
        }
    }

    public sealed class ComparisonReport
    {
        public StableId ComparisonId { get; }
        public IReadOnlyList<ContentReference> RunRefs { get; }
        public ContentReference BasisRef { get; }
        public IReadOnlyList<StableId> CommonQoiIds { get; }
        public ComparisonStatus Status { get; }
        public ContentReference ViolationsRef { get; }
        public ContentReference MetricsRef { get; }
        public ContentReference UncertaintyRef { get; }
        public ContentReference SelectedPlanRef { get; }
        public string SelectionReason { get; }
        public ComparisonReport(StableId comparisonId, IEnumerable<ContentReference> runRefs, ContentReference basisRef,
            IEnumerable<StableId> commonQoiIds, ComparisonStatus status, ContentReference violationsRef, ContentReference metricsRef,
            ContentReference uncertaintyRef, ContentReference selectedPlanRef, string selectionReason)
        {
            ComparisonId = ContractGuard.Id(comparisonId, nameof(comparisonId));
            RunRefs = MessageGuard.Copy(runRefs, 2, nameof(runRefs));
            BasisRef = ContractGuard.NotNull(basisRef, nameof(basisRef));
            CommonQoiIds = MessageGuard.Ids(commonQoiIds, nameof(commonQoiIds), 0);
            Status = ContractGuard.Defined(status, nameof(status));
            ViolationsRef = ContractGuard.NotNull(violationsRef, nameof(violationsRef));
            MetricsRef = ContractGuard.NotNull(metricsRef, nameof(metricsRef));
            UncertaintyRef = ContractGuard.NotNull(uncertaintyRef, nameof(uncertaintyRef));
            SelectedPlanRef = selectedPlanRef; SelectionReason = selectionReason;
        }
    }

    public sealed class EvidenceRecord
    {
        public StableId EvidenceId { get; }
        public ContentReference SubjectRef { get; }
        public StableId TestId { get; }
        public string TestRevision { get; }
        public ContentReference RunEnvironmentRef { get; }
        public IReadOnlyList<ContentReference> InputRefs { get; }
        public IReadOnlyList<ContentReference> OutputRefs { get; }
        public EvidenceResult Result { get; }
        public UtcTimestamp? ExecutedAt { get; }
        public StableId ExecutorId { get; }
        public StableId? ReviewerId { get; }
        public string ClaimScope { get; }
        public string NotRunReason { get; }
        public EvidenceRecord(StableId evidenceId, ContentReference subjectRef, StableId testId, string testRevision,
            ContentReference runEnvironmentRef, IEnumerable<ContentReference> inputRefs, IEnumerable<ContentReference> outputRefs,
            EvidenceResult result, UtcTimestamp? executedAt, StableId executorId, StableId? reviewerId, string claimScope, string notRunReason)
        {
            EvidenceId = ContractGuard.Id(evidenceId, nameof(evidenceId));
            SubjectRef = ContractGuard.NotNull(subjectRef, nameof(subjectRef));
            TestId = ContractGuard.Id(testId, nameof(testId));
            TestRevision = MessageGuard.Hash(testRevision, nameof(testRevision));
            RunEnvironmentRef = ContractGuard.NotNull(runEnvironmentRef, nameof(runEnvironmentRef));
            InputRefs = MessageGuard.Copy(inputRefs, 1, nameof(inputRefs));
            OutputRefs = MessageGuard.Copy(outputRefs, 0, nameof(outputRefs));
            Result = ContractGuard.Defined(result, nameof(result));
            ExecutedAt = executedAt;
            ExecutorId = ContractGuard.Id(executorId, nameof(executorId));
            ReviewerId = MessageGuard.OptionalId(reviewerId, nameof(reviewerId));
            ClaimScope = MessageGuard.Text(claimScope, nameof(claimScope));
            NotRunReason = notRunReason;
            if (result == EvidenceResult.NOT_RUN && (executedAt.HasValue || string.IsNullOrWhiteSpace(notRunReason)))
                throw new ArgumentException("NOT_RUN requires no execution timestamp and a concrete reason.");
            if (result != EvidenceResult.NOT_RUN && !executedAt.HasValue)
                throw new ArgumentException("An executed result requires an execution timestamp.");
            if (result == EvidenceResult.PASSED_WITH_SCOPE && OutputRefs.Count == 0)
                throw new ArgumentException("A scoped pass requires output evidence references.");
            // References do not prove artifact accessibility, reviewer independence or claim validity.
        }
    }

    public sealed class FieldOutputContract
    {
        public StableId FieldId { get; }
        public StableId OwnerWorkerId { get; }
        public FieldUnit Unit { get; }
        public StableId FrameId { get; }
        public FieldOutputContract(StableId fieldId, StableId ownerWorkerId, FieldUnit unit, StableId frameId)
        {
            FieldId = ContractGuard.Id(fieldId, nameof(fieldId));
            OwnerWorkerId = ContractGuard.Id(ownerWorkerId, nameof(ownerWorkerId));
            Unit = ContractGuard.Defined(unit, nameof(unit));
            FrameId = ContractGuard.Id(frameId, nameof(frameId));
        }
    }

    public sealed class SimulationJob
    {
        public StableId JobId { get; }
        public StableId RunId { get; }
        public long Generation { get; }
        public StableId AttemptId { get; }
        public StableId WorkerId { get; }
        public ContentReference ModelRef { get; }
        public string InputDigest { get; }
        public long BoundaryRevision { get; }
        public SimTick StartTickUs { get; }
        public SimTick EndTickUs { get; }
        public ContentReference InputBlobRef { get; }
        public FieldOutputContract OutputContract { get; }
        public SimulationJob(StableId jobId, StableId runId, long generation, StableId attemptId, StableId workerId,
            ContentReference modelRef, string inputDigest, long boundaryRevision, SimTick startTickUs, SimTick endTickUs,
            ContentReference inputBlobRef, FieldOutputContract outputContract)
        {
            JobId = ContractGuard.Id(jobId, nameof(jobId));
            RunId = ContractGuard.Id(runId, nameof(runId));
            Generation = ContractGuard.Nonnegative(generation, nameof(generation));
            AttemptId = ContractGuard.Id(attemptId, nameof(attemptId));
            WorkerId = ContractGuard.Id(workerId, nameof(workerId));
            ModelRef = ContractGuard.NotNull(modelRef, nameof(modelRef));
            InputDigest = MessageGuard.Hash(inputDigest, nameof(inputDigest));
            BoundaryRevision = ContractGuard.Nonnegative(boundaryRevision, nameof(boundaryRevision));
            StartTickUs = startTickUs; EndTickUs = endTickUs;
            InputBlobRef = ContractGuard.NotNull(inputBlobRef, nameof(inputBlobRef));
            OutputContract = ContractGuard.NotNull(outputContract, nameof(outputContract));
            MessageGuard.Interval(startTickUs, endTickUs);
            if (!workerId.Equals(outputContract.OwnerWorkerId)) throw new ArgumentException("Output owner must be this job's worker.");
        }
    }

    public sealed class FieldBatch
    {
        public StableId JobId { get; }
        public StableId RunId { get; }
        public long Generation { get; }
        public StableId WorkerId { get; }
        public ContentReference ModelRef { get; }
        public string InputDigest { get; }
        public long BoundaryRevision { get; }
        public SimTick ValidFromTickUs { get; }
        public SimTick ValidToTickUs { get; }
        public StableId FieldOwner { get; }
        public FieldUnit Unit { get; }
        public StableId FrameId { get; }
        public ContentReference ResultRef { get; }
        public FieldStatus Status { get; }
        public StableId FieldId { get; }
        public FieldBatch(StableId jobId, StableId runId, long generation, StableId workerId, ContentReference modelRef,
            string inputDigest, long boundaryRevision, SimTick validFromTickUs, SimTick validToTickUs, StableId fieldOwner,
            FieldUnit unit, StableId frameId, ContentReference resultRef, FieldStatus status, StableId fieldId)
        {
            JobId = ContractGuard.Id(jobId, nameof(jobId));
            RunId = ContractGuard.Id(runId, nameof(runId));
            Generation = ContractGuard.Nonnegative(generation, nameof(generation));
            WorkerId = ContractGuard.Id(workerId, nameof(workerId));
            ModelRef = ContractGuard.NotNull(modelRef, nameof(modelRef));
            InputDigest = MessageGuard.Hash(inputDigest, nameof(inputDigest));
            BoundaryRevision = ContractGuard.Nonnegative(boundaryRevision, nameof(boundaryRevision));
            ValidFromTickUs = validFromTickUs; ValidToTickUs = validToTickUs;
            FieldOwner = ContractGuard.Id(fieldOwner, nameof(fieldOwner));
            Unit = ContractGuard.Defined(unit, nameof(unit));
            FrameId = ContractGuard.Id(frameId, nameof(frameId));
            ResultRef = ContractGuard.NotNull(resultRef, nameof(resultRef));
            Status = ContractGuard.Defined(status, nameof(status));
            FieldId = ContractGuard.Id(fieldId, nameof(fieldId));
            MessageGuard.Interval(validFromTickUs, validToTickUs);
            if (!workerId.Equals(fieldOwner)) throw new ArgumentException("Field owner must identify the producing worker.");
            // Matching this result to a live job/generation, units, frame and input digest is the coordinator's responsibility.
        }
    }

    public sealed class ScriptStep
    {
        public StableId StepId { get; }
        public StableId RoleId { get; }
        public ContentReference ConditionRef { get; }
        public string Text { get; }
        public ScriptClaimType ClaimType { get; }
        public IReadOnlyList<ContentReference> EvidenceRefs { get; }
        public ScriptStep(StableId stepId, StableId roleId, ContentReference conditionRef, string text,
            ScriptClaimType claimType, IEnumerable<ContentReference> evidenceRefs)
        {
            StepId = ContractGuard.Id(stepId, nameof(stepId));
            RoleId = ContractGuard.Id(roleId, nameof(roleId));
            ConditionRef = ContractGuard.NotNull(conditionRef, nameof(conditionRef));
            Text = MessageGuard.Text(text, nameof(text));
            ClaimType = ContractGuard.Defined(claimType, nameof(claimType));
            EvidenceRefs = MessageGuard.Copy(evidenceRefs, 0, nameof(evidenceRefs));
            if ((claimType == ScriptClaimType.OBSERVED_IN_RUN || claimType == ScriptClaimType.MANUAL_SUPPORTED_PROPOSAL ||
                 claimType == ScriptClaimType.REVIEWED_INSTRUCTION) && EvidenceRefs.Count == 0)
                throw new ArgumentException("This claim requires evidence references, whose scope must be verified separately.");
        }
    }

    public sealed class ScriptIR
    {
        public StableId ScriptId { get; }
        public long Revision { get; }
        public ContentReference PlanRef { get; }
        public ContentReference ScenarioRef { get; }
        public IReadOnlyList<ScriptStep> Steps { get; }
        public ContentReference ReviewRef { get; }
        public ScriptIR(StableId scriptId, long revision, ContentReference planRef, ContentReference scenarioRef,
            IEnumerable<ScriptStep> steps, ContentReference reviewRef)
        {
            ScriptId = ContractGuard.Id(scriptId, nameof(scriptId));
            Revision = ContractGuard.Nonnegative(revision, nameof(revision));
            PlanRef = ContractGuard.NotNull(planRef, nameof(planRef));
            ScenarioRef = ContractGuard.NotNull(scenarioRef, nameof(scenarioRef));
            Steps = MessageGuard.Copy(steps, 1, nameof(steps));
            ReviewRef = reviewRef;
            var ids = new HashSet<StableId>();
            foreach (var step in Steps)
            {
                if (!ids.Add(step.StepId)) throw new ArgumentException("Duplicate script step ID.");
                if (step.ClaimType == ScriptClaimType.REVIEWED_INSTRUCTION && reviewRef == null)
                    throw new ArgumentException("Reviewed instructions require a review reference.");
            }
        }
    }

    public sealed class WorkerCapability
    {
        public StableId WorkerId { get; }
        public ContentReference ModelRef { get; }
        public string ProtocolVersion { get; }
        public IReadOnlyList<StableId> Fields { get; }
        public WorkerTimeMode TimeMode { get; }
        public WorkerRestartMode RestartMode { get; }
        public ContentReference ValidityProfileRef { get; }
        public SimTick MaxStepUs { get; }
        public WorkerCapability(StableId workerId, ContentReference modelRef, string protocolVersion, IEnumerable<StableId> fields,
            WorkerTimeMode timeMode, WorkerRestartMode restartMode, ContentReference validityProfileRef, SimTick maxStepUs)
        {
            WorkerId = ContractGuard.Id(workerId, nameof(workerId));
            ModelRef = ContractGuard.NotNull(modelRef, nameof(modelRef));
            if (protocolVersion != "cg-worker/1") throw new ArgumentException("Unsupported worker protocol.", nameof(protocolVersion));
            ProtocolVersion = protocolVersion;
            Fields = MessageGuard.Ids(fields, nameof(fields));
            TimeMode = ContractGuard.Defined(timeMode, nameof(timeMode));
            RestartMode = ContractGuard.Defined(restartMode, nameof(restartMode));
            ValidityProfileRef = ContractGuard.NotNull(validityProfileRef, nameof(validityProfileRef));
            MaxStepUs = maxStepUs;
        }
    }

    internal static class MessageGuard
    {
        internal static void Interval(SimTick start, SimTick end)
        {
            if (end.Microseconds < start.Microseconds) throw new ArgumentException("Simulation interval is reversed.");
        }
        internal static string Text(string value, string name)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 8192) throw new ArgumentException("Text must contain 1–8192 characters.", name);
            return value;
        }
        internal static string Hash(string value, string name)
        {
            if (value == null || value.Length != 64) throw new ArgumentException("소문자 SHA-256 64자여야 합니다.", name);
            foreach (var c in value)
                if (!(c >= 'a' && c <= 'f') && !(c >= '0' && c <= '9')) throw new ArgumentException("소문자 SHA-256이어야 합니다.", name);
            return value;
        }
        internal static StableId? OptionalId(StableId? value, string name)
        {
            if (value.HasValue) ContractGuard.Id(value.Value, name);
            return value;
        }
        // 모든 원소 타입은 sealed immutable DTO 또는 값 타입이다. 배열 순서를 유지하고 소유권을 복사한다.
        internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, int minimum, string name) where T : class
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<T>();
            foreach (var value in values)
            {
                if (value == null) throw new ArgumentException("null 원소를 허용하지 않습니다.", name);
                if (copy.Count == 4096) throw new ArgumentException("4096개 제한을 초과했습니다.", name);
                copy.Add(value);
            }
            if (copy.Count < minimum) throw new ArgumentException("필수 원소가 없습니다.", name);
            return new ReadOnlyCollection<T>(copy);
        }
        internal static IReadOnlyList<StableId> Ids(IEnumerable<StableId> values, string name, int minimum = 1)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<StableId>();
            var unique = new HashSet<StableId>();
            foreach (var value in values)
            {
                ContractGuard.Id(value, name);
                if (copy.Count == 4096 || !unique.Add(value)) throw new ArgumentException("ID 중복 또는 4096개 제한 초과입니다.", name);
                copy.Add(value);
            }
            if (copy.Count < minimum) throw new ArgumentException("필수 ID가 없습니다.", name);
            return new ReadOnlyCollection<StableId>(copy);
        }
    }
}
