using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.App.Fps.Work
{
    public sealed class GameplayProcedureStep
    {
        public string Id { get; }
        public string Label { get; }
        public string SourceRef { get; }
        public long SourceRevision { get; }
        public ActorRole Role { get; }
        public IReadOnlyList<string> Requires { get; }
        public RuleExpression Applicability { get; }
        public RuleExpression StartGuard { get; }
        public RuleExpression CompletionEvidence { get; }
        public string BlockedReason { get; }
        public GameplayProcedureStep(string id, string label, string sourceRef, long sourceRevision, ActorRole role,
            IEnumerable<string> requires, RuleExpression applicability, RuleExpression startGuard,
            RuleExpression completionEvidence, string blockedReason)
        {
            Id = id; Label = label; SourceRef = sourceRef; SourceRevision = sourceRevision; Role = role;
            Requires = new List<string>(requires ?? Array.Empty<string>()).AsReadOnly();
            Applicability = applicability ?? throw new ArgumentNullException(nameof(applicability));
            StartGuard = startGuard ?? throw new ArgumentNullException(nameof(startGuard));
            CompletionEvidence = completionEvidence ?? throw new ArgumentNullException(nameof(completionEvidence));
            BlockedReason = blockedReason ?? throw new ArgumentNullException(nameof(blockedReason));
        }
    }

    public sealed class GameplayStepState
    {
        public ProcedureRunner.Step Step { get; internal set; }
        public RuleTruth Applicability { get; internal set; }
        public RuleTruth StartGuard { get; internal set; }
        public RuleTruth CompletionEvidence { get; internal set; }
        public bool Available { get; internal set; }
        public bool NotApplicable => Applicability == RuleTruth.FALSE;
        public string Reason { get; internal set; }
    }

    public sealed partial class ProcedureRunner
    {
        private readonly List<GameplayProcedureStep> gameplayDefinitions = new List<GameplayProcedureStep>();
        private readonly List<GameplayStepState> gameplayStates = new List<GameplayStepState>();
        private readonly Dictionary<string, GameplayStepState> gameplayById = new Dictionary<string, GameplayStepState>(StringComparer.Ordinal);
        public IReadOnlyList<GameplayStepState> GameplayStates => gameplayStates;

        // Trusted, typed authoring only. No effect source strings or arbitrary executable predicates.
        public void LoadGameplay(string id, string title, string sourceRef, long sourceRevision, ActorRole role,
            IReadOnlyList<GameplayProcedureStep> definitions)
        {
            Ready = false; Completed = false; Revision++;
            steps.Clear(); byId.Clear(); guards.Clear(); gameplayDefinitions.Clear(); gameplayStates.Clear(); gameplayById.Clear();
            ValidateId(id);
            if (string.IsNullOrWhiteSpace(sourceRef) || sourceRevision < 1 || definitions == null || definitions.Count < 1 || definitions.Count > 64)
                throw new ArgumentException("근거와 판본을 가진 1~64개 절차가 필요합니다.");
            Id = id; Title = title; Basis = sourceRef;
            var fingerprint = new StringBuilder().Append(id).Append('|').Append(sourceRef).Append('|')
                .Append(sourceRevision.ToString(CultureInfo.InvariantCulture)).Append('|').Append((int)role);
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("빈 단계입니다.");
                ValidateId(definition.Id);
                if (byId.ContainsKey(definition.Id) || string.IsNullOrWhiteSpace(definition.Label) ||
                    string.IsNullOrWhiteSpace(definition.SourceRef) || definition.SourceRevision < 1 || definition.Role != role)
                    throw new ArgumentException("단계 식별자/근거/역할이 올바르지 않습니다.");
                foreach (var required in definition.Requires)
                    if (!byId.ContainsKey(required)) throw new ArgumentException("선행 단계는 먼저 정의해야 합니다: " + required);
                var step = new Step { Id = definition.Id, Label = definition.Label, Basis = definition.SourceRef,
                    Requires = definition.Requires, Effect = "none", Observe = "현재 실행/관찰 증거", FailReason = definition.BlockedReason,
                    Index = steps.Count };
                var state = new GameplayStepState { Step = step };
                steps.Add(step); byId.Add(step.Id, step); guards.Add(step.Id, definition.StartGuard);
                gameplayDefinitions.Add(definition); gameplayStates.Add(state); gameplayById.Add(step.Id, state);
                fingerprint.Append('|').Append(definition.Id).Append('|').Append(definition.SourceRef).Append('|')
                    .Append(definition.SourceRevision.ToString(CultureInfo.InvariantCulture));
                foreach (var required in definition.Requires) fingerprint.Append('|').Append(required);
                AppendGameplayExpression(fingerprint, definition.Applicability);
                AppendGameplayExpression(fingerprint, definition.StartGuard);
                AppendGameplayExpression(fingerprint, definition.CompletionEvidence);
            }
            using (var hash = SHA256.Create())
                DefinitionHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToString()))).Replace("-", "").ToLowerInvariant();
            Ready = true; StatusReason = "근거 기반 실습 준비됨 · " + Title;
        }

        private static void AppendGameplayExpression(StringBuilder text, RuleExpression expression)
        {
            text.Append('[').Append((int)expression.Kind).Append(':').Append(expression.FactId?.Value)
                .Append(':').Append(expression.ConstantValue.HasValue ? (int)expression.ConstantValue.Value : -1);
            foreach (var child in expression.Children) AppendGameplayExpression(text, child);
            text.Append(']');
        }

        public void EvaluateGameplay(RuleFacts facts)
        {
            if (!Ready || gameplayDefinitions.Count == 0) throw new InvalidOperationException("다중 대상 절차가 로드되지 않았습니다.");
            bool completed = true, changed = false;
            for (int i = 0; i < gameplayDefinitions.Count; i++)
            {
                var definition = gameplayDefinitions[i]; var state = gameplayStates[i];
                var applicability = definition.Applicability.Evaluate(facts);
                var guard = definition.StartGuard.Evaluate(facts);
                var evidence = definition.CompletionEvidence.Evaluate(facts);
                bool prerequisites = true;
                foreach (var required in definition.Requires)
                {
                    var prior = gameplayById[required];
                    if (!prior.NotApplicable && !prior.Step.Done) { prerequisites = false; break; }
                }
                // Start conditions authorize future work; consumed resources need not remain held forever.
                // Current completion invariants are explicit separate evidence, rechecked after every mutation.
                bool done = applicability == RuleTruth.TRUE && prerequisites && evidence == RuleTruth.TRUE;
                changed |= state.Step.Done != done || state.Applicability != applicability || state.StartGuard != guard || state.CompletionEvidence != evidence;
                state.Step.Done = done; state.Applicability = applicability; state.StartGuard = guard; state.CompletionEvidence = evidence;
                state.Available = !done && applicability == RuleTruth.TRUE && prerequisites && guard == RuleTruth.TRUE;
                state.Reason = done ? "현재 완료 증거 충족" : applicability == RuleTruth.FALSE ? "확인된 비해당" :
                    applicability == RuleTruth.UNKNOWN ? "적용 근거 미확인 · 승인 매뉴얼/연습 대상 구분 필요" :
                    applicability == RuleTruth.CONFLICTED ? "적용 근거 충돌 · 판정 보류" :
                    !prerequisites ? "선행 작업의 현재 증거가 충족되지 않았습니다" : definition.BlockedReason;
                if (!done && applicability != RuleTruth.FALSE) completed = false;
            }
            if (changed || completed != Completed) Revision++;
            Completed = completed;
        }

        private Decision GameplayNext(RuleFacts facts)
        {
            EvaluateGameplay(facts);
            GameplayStepState firstBlocked = null;
            foreach (var state in gameplayStates)
            {
                if (state.Step.Done || state.NotApplicable) continue;
                if (state.Available)
                    return new Decision { Step = state.Step, Allowed = true, Guard = state.StartGuard,
                        Reason = state.Step.Label, Facts = facts, Revision = Revision };
                if (firstBlocked == null) firstBlocked = state;
            }
            return new Decision { Step = firstBlocked?.Step, Allowed = false,
                Guard = firstBlocked == null ? RuleTruth.UNKNOWN : firstBlocked.StartGuard,
                Reason = firstBlocked?.Reason ?? (Completed ? "실습 증거 충족" : "판정 보류"), Facts = facts, Revision = Revision };
        }
    }
}
