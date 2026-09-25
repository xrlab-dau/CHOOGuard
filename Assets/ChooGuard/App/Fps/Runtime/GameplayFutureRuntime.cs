using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Runs remote inference off the frame loop; all state publication happens in TickFuture on the world owner.</summary>
    public sealed class GameplayFutureRuntime : MonoBehaviour
    {
        private sealed class StepResult
        {
            public long Epoch;
            public FutureBranch Branch;
            public IReadOnlyList<GroundedCandidate> Candidates;
            public InferenceSelection Selection;
            public string Failure;
        }
        private WorldSession session;
        private GameplayInferenceClient inference;
        private TransitionKernel kernel;
        private FutureComposer composer;
        private CancellationTokenSource cancellation;
        private Task<StepResult> pending;
        private readonly Dictionary<StableId, long> watched = new Dictionary<StableId, long>();
        private readonly List<KeyValuePair<ActorActionIntent, ActionEvidence>> hypotheses = new List<KeyValuePair<ActorActionIntent, ActionEvidence>>();
        private WorldSnapshot basis;
        private long epoch;
        private string forecastId, lastAttemptSignature;
        private bool registered, applied, dirty = true;
        public string Status { get; private set; } = "미래 추론 연결 전";
        public IReadOnlyList<FutureBranch> Branches => composer == null ? Array.Empty<FutureBranch>() : composer.Branches;
        public event Action<string> StatusChanged;
        public void Initialize(WorldSession world, TransitionKernel transitionKernel, GameplayInferenceClient client)
        {
            Unbind(); session = world ?? throw new ArgumentNullException(nameof(world)); kernel = transitionKernel ?? throw new ArgumentNullException(nameof(transitionKernel));
            inference = client ?? throw new ArgumentNullException(nameof(client)); composer = new FutureComposer(kernel);
            cancellation = new CancellationTokenSource(); session.Committed += OnCommitted; dirty = true;
            SetStatus(session.Mode == GameplayMode.Tutorial ? "튜토리얼 격리 · 본게임 비상 전개와 연결하지 않음" : "현재 원인 관측 대기");
        }
        public bool PreviewIntervention(ActorActionIntent intent, ActionEvidence hypotheticalEvidence, out string reason)
        {
            if (session == null || session.Mode != GameplayMode.RandomOperationsLab) { reason = "main_world_required"; return false; }
            if (intent == null || hypotheticalEvidence == null || hypotheses.Count >= 3) { reason = "hypothesis_capacity"; return false; }
            hypotheses.Add(new KeyValuePair<ActorActionIntent, ActionEvidence>(intent, hypotheticalEvidence));
            Invalidate("가정한 개입 변경 · 실제 수행하지 않음"); lastAttemptSignature = null; reason = "counterfactual_queued"; return true;
        }
        public void TickFuture()
        {
            if (session == null || session.Mode != GameplayMode.RandomOperationsLab) return;
            if (pending != null)
            {
                if (!pending.IsCompleted) return;
                var task = pending; pending = null;
                StepResult result;
                try { result = task.GetAwaiter().GetResult(); }
                catch (Exception) { SetStatus("미래 추론 불가 · 원격 서비스 오류"); return; }
                if (result.Epoch != epoch || result.Branch.Status != FutureStatus.Pending) return;
                if (result.Failure != null || result.Selection == null || result.Selection.Status != "ok")
                {
                    string reason = result.Failure ?? result.Selection?.Reason ?? "inference_unavailable";
                    result.Branch.Stop(FutureStatus.Unavailable, reason); SetStatus("미래 추론 불가 · " + reason);
                }
                else if (!composer.ApplySelection(result.Branch, result.Candidates, result.Selection.SelectedCandidateId,
                    result.Selection.RequestSha256, result.Selection.Model, out var reason)) SetStatus("미래 가지 보류 · " + reason);
                else SetStatus(result.Branch.Status == FutureStatus.Truncated ? "조건부 미래 · 깊이 한도 3에서 중단" : "변한 가상 상태에서 다음 인과 추론");
            }
            if (dirty)
            {
                var snapshot = session.Snapshot();
                var candidates = kernel.BuildEnvironmentCandidates(snapshot);
                string signature = Signature(snapshot, candidates);
                dirty = false;
                if (signature == lastAttemptSignature) return;
                lastAttemptSignature = signature;
                if (candidates.Count < 2) { SetStatus("현재 확인된 원인으로 새 환경 전이 후보 없음"); return; }
                Begin(snapshot, candidates);
            }
            if (basis == null || applied) return;
            foreach (var branch in composer.Branches)
            {
                if (branch.Status != FutureStatus.Pending) continue;
                var candidates = composer.Candidates(branch);
                if (candidates.Count < 2) { branch.Stop(FutureStatus.Complete, "no_further_grounded_effect"); continue; }
                // A scope never silently grows beyond the registered root causal read set.
                bool covered = true;
                foreach (var candidate in candidates) foreach (var read in candidate.ReadSet) if (!watched.ContainsKey(read.EntityId)) covered = false;
                if (!covered) { branch.Stop(FutureStatus.Incomplete, "causal_scope_requires_new_basis"); SetStatus("미래 불완전 · 새 원인 범위 재등록 필요"); continue; }
                int depth = 0; foreach (var step in branch.Steps) if (step.Depth >= 0) depth++;
                var scope = CollectScopeCandidates();
                pending = InferStep(branch, candidates, scope, depth, epoch, cancellation.Token);
                return;
            }
            applied = true;
            var root = composer.Branches[0];
            if (root.Steps.Count > 0 && root.Status != FutureStatus.Stale && root.Status != FutureStatus.Unavailable)
            {
                var first = root.Steps[0];
                if (first.Depth == 0 && first.Candidate.ActorId == null)
                {
                    bool committed = session.CommitEnvironment(kernel, basis, first.Candidate,
                        forecastId + "-environment", first.RequestSha256, out var reason);
                    SetStatus(committed ? "현재 원인 재검사 후 첫 환경 효과만 반영 · 후속 미래는 가정" : "실제 환경 반영 보류 · " + reason);
                }
            }
            _ = CloseScope(inference, basis, forecastId);
        }
        private void Begin(WorldSnapshot snapshot, IReadOnlyList<GroundedCandidate> candidates)
        {
            cancellation.Cancel(); cancellation.Dispose(); cancellation = new CancellationTokenSource(); epoch++;
            basis = snapshot; forecastId = "forecast-" + Guid.NewGuid().ToString("N");
            composer.Begin(snapshot, forecastId); registered = false; applied = false; watched.Clear();
            foreach (var hypothesis in hypotheses) composer.AddIntervention(hypothesis.Key, hypothesis.Value, out _);
            hypotheses.Clear();
            foreach (var candidate in CollectScopeCandidates()) foreach (var read in candidate.ReadSet)
                if (snapshot.Entities.TryGetValue(read.EntityId, out var entity)) watched[entity.Id] = entity.Revision;
            if (watched.Count > 64)
            {
                foreach (var branch in composer.Branches) branch.Stop(FutureStatus.Incomplete, "causal_readset_capacity");
                applied = true; SetStatus("미래 불완전 · 인과 범위 64개 한도");
            }
            else SetStatus("JEV 현재 상태 기반 미래 합성 중");
        }
        private IReadOnlyList<GroundedCandidate> CollectScopeCandidates()
        {
            var result = new List<GroundedCandidate>();
            foreach (var branch in composer.Branches) result.AddRange(kernel.BuildEnvironmentCandidates(branch.State));
            return result;
        }
        private async Task<StepResult> InferStep(FutureBranch branch, IReadOnlyList<GroundedCandidate> candidates,
            IReadOnlyList<GroundedCandidate> scopeCandidates, int depth, long requestedEpoch, CancellationToken token)
        {
            var result = new StepResult { Epoch = requestedEpoch, Branch = branch, Candidates = candidates };
            try
            {
                if (!registered)
                {
                    if (!await inference.RegisterOrUpdateSessionAsync(basis, token)) { result.Failure = inference.LastLifecycleError; return result; }
                    var branchIds = new List<string>(); foreach (var item in composer.Branches) branchIds.Add(item.BranchId);
                    if (!await inference.RegisterFutureAsync(basis, forecastId, branchIds, scopeCandidates, token)) { result.Failure = inference.LastLifecycleError; return result; }
                    if (requestedEpoch != epoch) { result.Failure = "stale_epoch"; return result; }
                    registered = true;
                }
                result.Selection = await inference.SelectFutureAsync(basis, branch.State, forecastId, branch.BranchId, depth,
                    candidates, branch.BranchId == "branch-0" && depth == 0, token);
            }
            catch (OperationCanceledException) { result.Failure = "cancelled"; }
            catch (Exception) { result.Failure = "future_transport_unavailable"; }
            return result;
        }
        private void OnCommitted(WorldMutation mutation)
        {
            if (basis != null && mutation.Generation != basis.Generation) { Invalidate("세대 변경 · 이전 미래 폐기"); return; }
            foreach (var entity in mutation.Entities)
                if (watched.TryGetValue(entity.Id, out var revision) && entity.Revision != revision) { Invalidate("플레이어/NPC/세계 개입 · 관련 원인 변경"); return; }
            // Unwatched entities can introduce a new cause. The candidate signature still prevents retries
            // for unrelated commits; existing watched changes cancel stale work immediately above.
            if (basis == null || watched.Count == 0 || mutation.Entities.Count > 0) dirty = true;
        }
        private void Invalidate(string reason)
        {
            cancellation?.Cancel(); epoch++; composer?.Invalidate(reason); dirty = true; applied = false;
            if (basis != null && forecastId != null) _ = CloseScope(inference, basis, forecastId);
            SetStatus(reason);
        }
        private static string Signature(WorldSnapshot snapshot, IReadOnlyList<GroundedCandidate> candidates)
        {
            var parts = new List<string>();
            foreach (var candidate in candidates)
            { parts.Add(candidate.CandidateId); foreach (var read in candidate.ReadSet) parts.Add(read.EntityId.Value + ":" + read.Revision.ToString(CultureInfo.InvariantCulture)); }
            parts.Sort(StringComparer.Ordinal);
            return snapshot.Generation.ToString(CultureInfo.InvariantCulture) + "|" + snapshot.RulesetRevision.ToString(CultureInfo.InvariantCulture) + "|" + string.Join("|", parts);
        }
        private static async Task CloseScope(GameplayInferenceClient client, WorldSnapshot snapshot, string id)
        { try { await client.CloseFutureAsync(snapshot, id, CancellationToken.None); } catch (Exception) { /* No world change is inferred from a remote close failure. */ } }
        private void SetStatus(string status) { Status = status; StatusChanged?.Invoke(status); }
        private void Unbind()
        {
            if (session != null) session.Committed -= OnCommitted;
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; epoch++; pending = null;
        }
        private void OnDestroy() { Unbind(); }
    }
}
