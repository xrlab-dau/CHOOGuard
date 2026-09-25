using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.App.Fps.Runtime;
using ChooGuard.App.Fps.Work;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.App.Fps.Tutorial
{
    public sealed class TrainingActionObservation
    {
        public StableId RunId { get; }
        public long Generation { get; }
        public ActorActionIntent Intent { get; }
        public ActionReceipt Receipt { get; }
        public SimTick Tick { get; }
        public long TargetRevision { get; }
        public string SourceRef { get; }
        public long SourceRevision { get; }
        internal TrainingActionObservation(WorldMutation mutation, long targetRevision, RoleTrainingDefinition definition)
        {
            RunId = mutation.RunId; Generation = mutation.Generation; Intent = mutation.Intent; Receipt = mutation.Receipt;
            Tick = mutation.Tick; TargetRevision = targetRevision; SourceRef = definition.SourceRef; SourceRevision = definition.Revision;
        }
    }

    public sealed class RoleTrainingCheckpoint
    {
        public WorldSnapshot World { get; }
        public string DefinitionId { get; }
        public long DefinitionRevision { get; }
        public IReadOnlyList<TrainingActionObservation> Observations { get; }
        public bool IncludesPhysicalState => Physical != null;
        internal string BindingSignature { get; }
        internal FpsPhysicalCheckpoint Physical { get; }
        internal RoleTrainingCheckpoint(WorldSnapshot world, RoleTrainingDefinition definition, string signature,
            IEnumerable<TrainingActionObservation> observations, FpsPhysicalCheckpoint physical)
        {
            World = world; DefinitionId = definition.Id; DefinitionRevision = definition.Revision; BindingSignature = signature;
            Observations = new List<TrainingActionObservation>(observations).AsReadOnly(); Physical = physical;
        }
    }

    /// <summary>
    /// Tutorial guidance only. WorldSession owns all changes and NpcAgentRuntime owns all NPC decisions.
    /// No interaction-count completion, fact mutation, generated effects, or main-world reference exists here.
    /// </summary>
    public sealed class RoleTrainingProgram : IDisposable
    {
        private WorldSession session;
        private DetailedInteractionController controller;
        private IReadOnlyDictionary<string, StableId> bindings;
        private readonly List<TrainingActionObservation> observations = new List<TrainingActionObservation>();
        private readonly Dictionary<string, int> observationIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private IReadOnlyList<TrainingActionObservation> observationView;
        private readonly List<KeyValuePair<StableId, RuleTruth?>> facts = new List<KeyValuePair<StableId, RuleTruth?>>();
        private long evaluatedRevision = -1, evaluatedGeneration = -1, evaluatedTick = -1;
        private string bindingSignature;
        public ProcedureRunner Runner { get; } = new ProcedureRunner();
        public RoleTrainingDefinition Definition { get; private set; }
        public GameplayStepState CurrentTask { get; private set; }
        public string CurrentLabel => CurrentTask == null ? PracticeCompleted ? "범위가 명시된 실습 완료" : "현재 수행 가능한 작업 없음" : CurrentTask.Step.Label;
        public string BlockedReason { get; private set; } = "실습 초기화 전";
        public RuleTruth PracticeScopeTruth { get; private set; } = RuleTruth.UNKNOWN;
        public bool PracticeCompleted => Initialized && PracticeScopeTruth == RuleTruth.TRUE && Runner.Completed;
        public RuleTruth CertificationStatus => RuleTruth.UNKNOWN;
        public string CertificationReason => "합성 무하중 조작 연습입니다. 승인된 차종/역별 매뉴얼·토크·전기 격리·검사 한도·SDS·건조/소독 근거가 없어 실제 철도 안전/운행 합격은 부여하지 않습니다.";
        public IReadOnlyList<TrainingActionObservation> CompletionObservations => observationView ?? (observationView = observations.AsReadOnly());
        public ActionReceipt LastActionReceipt { get; private set; }
        public bool HasMisreportedFacility { get; private set; }
        public bool Initialized => session != null;

        public void Initialize(WorldSession isolatedTutorial, ActorRole role, IReadOnlyDictionary<string, StableId> bindings,
            DetailedInteractionController controller)
        {
            if (isolatedTutorial == null) throw new ArgumentNullException(nameof(isolatedTutorial));
            if (isolatedTutorial.Mode != GameplayMode.Tutorial) throw new InvalidOperationException("본게임 세션은 실습에 연결할 수 없습니다.");
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var definition = RoleTrainingDefinition.Create(role);
            var copy = new Dictionary<string, StableId>(StringComparer.Ordinal);
            foreach (var binding in bindings) copy.Add(binding.Key, binding.Value);
            foreach (var key in definition.RequiredBindings)
                if (!copy.TryGetValue(key, out var id) || !isolatedTutorial.TryGetEntity(id, out _)) throw new ArgumentException("실물 entity binding이 없습니다: " + key);
            if (!isolatedTutorial.TryGetActor(copy["player"], out var actor) || !actor.IsHuman || actor.Role != role)
                throw new ArgumentException("실습에 명시적으로 배정된 사람 역할이 필요합니다.");
            if (controller != null && (!ReferenceEquals(controller.Session, isolatedTutorial) || !controller.ActorId.Equals(actor.Id)))
                throw new ArgumentException("물리 조작기는 같은 격리 세션/실습자에 먼저 Bind해야 합니다.");
            Dispose();
            session = isolatedTutorial; this.controller = controller; this.bindings = new ReadOnlyDictionary<string, StableId>(copy);
            Definition = definition; bindingSignature = Signature(copy); observations.Clear(); observationIndex.Clear(); LastActionReceipt = null;
            Runner.LoadGameplay(definition.Id, definition.Title, definition.SourceRef, definition.Revision, role, definition.Steps);
            session.Committed += OnCommitted;
            evaluatedRevision = evaluatedGeneration = evaluatedTick = -1;
            Tick();
        }

        public void Tick()
        {
            if (session == null) return;
            // Pose-only changes are checked at simulation cadence, not by rebuilding every render frame.
            if (session.Revision == evaluatedRevision && session.Generation == evaluatedGeneration &&
                session.Tick.Microseconds >= evaluatedTick && session.Tick.Microseconds - evaluatedTick < 100000) return;
            facts.Clear();
            foreach (var condition in Definition.Conditions)
                facts.Add(new KeyValuePair<StableId, RuleTruth?>(condition.Id, Evaluate(condition)));
            Runner.EvaluateGameplay(new RuleFacts(facts));
            PracticeScopeTruth = session.TryGetEntity(bindings["zone"], out var zone) ? zone.Fact("practice.fixture") : RuleTruth.UNKNOWN;
            CurrentTask = null;
            foreach (var state in Runner.GameplayStates)
                if (!state.Step.Done && !state.NotApplicable && state.Available) { CurrentTask = state; break; }
            if (CurrentTask == null)
                foreach (var state in Runner.GameplayStates)
                    if (!state.Step.Done && !state.NotApplicable) { CurrentTask = state; break; }
            BlockedReason = PracticeScopeTruth != RuleTruth.TRUE
                ? "명시적으로 확인된 합성 연습 지그가 아닙니다 · 실제 설비의 승인 매뉴얼/검사 한도 미확보로 작업 합격 보류"
                : PracticeCompleted ? "실습 범위 완료 · " + CertificationReason : CurrentTask?.Reason ?? "증거 판정 보류";
            if (LastActionReceipt != null && (LastActionReceipt.Phase == ActionPhase.Blocked || LastActionReceipt.Phase == ActionPhase.Cancelled || LastActionReceipt.Phase == ActionPhase.Failed))
                BlockedReason += " · 최근 중단: " + LastActionReceipt.Reason + " (이미 반영된 부분 작업/소비/보유 상태는 유지)";
            HasMisreportedFacility = bindings.TryGetValue("facility", out var facilityId) && session.TryGetEntity(facilityId, out var facility) &&
                facility.Fact("corroded") == RuleTruth.TRUE && facility.Fact("report:" + bindings["player"].Value + ":fit") == RuleTruth.TRUE;
            evaluatedRevision = session.Revision; evaluatedGeneration = session.Generation; evaluatedTick = session.Tick.Microseconds;
        }

        private RuleTruth Evaluate(TrainingCondition condition)
        {
            if (!bindings.TryGetValue(condition.Target, out var id) || !session.TryGetEntity(id, out var entity)) return RuleTruth.UNKNOWN;
            switch (condition.Kind)
            {
                case TrainingConditionKind.Fact:
                    var truth = entity.Fact(condition.Key);
                    return truth == RuleTruth.UNKNOWN || truth == RuleTruth.CONFLICTED ? truth : truth == condition.Expected ? RuleTruth.TRUE : RuleTruth.FALSE;
                case TrainingConditionKind.Role:
                    return session.TryGetActor(id, out var actor) ? Known(actor.Role == Definition.Role) : RuleTruth.UNKNOWN;
                case TrainingConditionKind.Custody:
                    return Known(entity.CustodianId.HasValue && entity.CustodianId.Value.Equals(bindings[condition.Other]));
                case TrainingConditionKind.Parent:
                    return Known(entity.ParentId.HasValue && entity.ParentId.Value.Equals(bindings[condition.Other]));
                case TrainingConditionKind.Unheld: return Known(!entity.CustodianId.HasValue);
                case TrainingConditionKind.Near:
                    return session.TryGetEntity(bindings[condition.Other], out var other)
                        ? Known(entity.Position.DistanceSquared(other.Position) <= condition.Number * condition.Number) : RuleTruth.UNKNOWN;
                case TrainingConditionKind.MeasurementAtMost:
                case TrainingConditionKind.MeasurementAtLeast:
                    if (!entity.Measurements.TryGetValue(condition.Key, out var value) || value.Unit != condition.Unit) return RuleTruth.UNKNOWN;
                    return Known(condition.Kind == TrainingConditionKind.MeasurementAtMost ? value.Value <= condition.Number : value.Value >= condition.Number);
                case TrainingConditionKind.Action:
                    long afterSequence = -1;
                    if (condition.AfterTarget != null)
                    {
                        foreach (var prior in observations)
                            if (prior.Intent.TargetId.Equals(bindings[condition.AfterTarget]) && prior.Intent.Verb == condition.AfterVerb &&
                                prior.Intent.ActorId.Equals(bindings[condition.Actor]))
                                afterSequence = Math.Max(afterSequence, prior.Receipt.Sequence);
                        if (afterSequence < 0) return RuleTruth.FALSE;
                    }
                    foreach (var observation in observations)
                    {
                        var intent = observation.Intent;
                        if (intent.Verb != condition.Verb || !intent.TargetId.Equals(id) || !intent.ActorId.Equals(bindings[condition.Actor])) continue;
                        if (condition.Key != null && (intent.WorkPointId ?? "main") != condition.Key) continue;
                        if (condition.Tool != null && (!intent.ToolId.HasValue || !intent.ToolId.Value.Equals(bindings[condition.Tool]))) continue;
                        if (condition.Recipient != null && (!intent.RecipientId.HasValue || !intent.RecipientId.Value.Equals(bindings[condition.Recipient]))) continue;
                        if (observation.Receipt.Sequence <= afterSequence) continue;
                        return RuleTruth.TRUE;
                    }
                    return RuleTruth.FALSE;
                default: throw new InvalidOperationException("정의되지 않은 실습 증거 종류입니다.");
            }
        }
        private static RuleTruth Known(bool value) => value ? RuleTruth.TRUE : RuleTruth.FALSE;

        private void OnCommitted(WorldMutation mutation)
        {
            evaluatedRevision = -1;
            if (mutation.Kind == "tutorial.restore")
            {
                observations.Clear(); observationIndex.Clear(); LastActionReceipt = null;
                return;
            }
            if (mutation.Intent == null || mutation.Receipt == null || mutation.Generation != session.Generation) return;
            LastActionReceipt = mutation.Receipt;
            if (mutation.Receipt.Phase != ActionPhase.Completed || mutation.Kind != "action.effect") return;
            if (!session.TryGetEntity(mutation.Intent.TargetId, out var target)) return;
            var observation = new TrainingActionObservation(mutation, target.Revision, Definition);
            string key = ObservationKey(observation);
            if (observationIndex.TryGetValue(key, out int index)) observations[index] = observation;
            else { observationIndex.Add(key, observations.Count); observations.Add(observation); }
        }

        public RoleTrainingCheckpoint CaptureCheckpoint()
        {
            EnsureInitialized();
            var world = session.Snapshot();
            foreach (var actor in world.Actors.Values)
                if (session.GetActiveIntent(actor.Id) != null) throw new InvalidOperationException("모든 실습 인물의 접촉 작업을 완료/취소한 안정 경계에서 저장하세요. 부분 결과는 보존됩니다.");
            var physical = controller == null ? null : controller.CapturePhysicalCheckpoint();
            return new RoleTrainingCheckpoint(world, Definition, bindingSignature, observations, physical);
        }

        public void RestoreCheckpoint(RoleTrainingCheckpoint checkpoint)
        {
            EnsureInitialized();
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.DefinitionId != Definition.Id || checkpoint.DefinitionRevision != Definition.Revision || checkpoint.BindingSignature != bindingSignature ||
                !checkpoint.World.RunId.Equals(session.RunId) || checkpoint.World.RulesetId != session.RulesetId || checkpoint.World.RulesetRevision != session.RulesetRevision)
                throw new InvalidOperationException("다른 실습/정의/규칙/실물 binding의 checkpoint는 복원할 수 없습니다.");
            if ((checkpoint.Physical != null) != (controller != null)) throw new InvalidOperationException("물리 checkpoint 짝이 달라졌습니다.");
            if (controller != null) controller.ValidatePhysicalCheckpoint(checkpoint.Physical);
            // The shared NPC host generation fence cancels pending inference before pumping its next response.
            // Old intents/receipts/reservations are discarded by WorldSession before this event is published.
            session.Restore(checkpoint.World);
            if (controller != null) controller.RestorePhysicalCheckpoint(checkpoint.Physical);
            observations.Clear(); observationIndex.Clear();
            foreach (var observation in checkpoint.Observations)
            { observationIndex.Add(ObservationKey(observation), observations.Count); observations.Add(observation); }
            LastActionReceipt = null; evaluatedRevision = evaluatedGeneration = evaluatedTick = -1;
            Tick();
        }

        private static string ObservationKey(TrainingActionObservation observation)
        {
            var intent = observation.Intent;
            return intent.ActorId.Value + "|" + intent.Verb + "|" + intent.TargetId.Value + "|" + intent.ToolId?.Value + "|" + intent.RecipientId?.Value + "|" + intent.WorkPointId;
        }
        private static string Signature(Dictionary<string, StableId> source)
        {
            var keys = new List<string>(source.Keys); keys.Sort(StringComparer.Ordinal);
            var parts = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++) parts[i] = keys[i] + "=" + source[keys[i]].Value;
            return string.Join(";", parts);
        }
        private void EnsureInitialized() { if (session == null) throw new InvalidOperationException("실습을 먼저 초기화하세요."); }
        public void Dispose()
        {
            if (session != null) session.Committed -= OnCommitted;
            session = null; controller = null;
        }
    }
}
