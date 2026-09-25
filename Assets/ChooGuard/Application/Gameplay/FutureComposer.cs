using System;
using System.Collections.Generic;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;

namespace ChooGuard.Application.Gameplay
{
    /// <summary>Hypothetical state only. No session, journal, actor sequence, custody reservation or private memory writer.</summary>
    public sealed class FutureComposer
    {
        private readonly TransitionKernel kernel;
        private readonly List<FutureBranch> branches = new List<FutureBranch>();
        private readonly IReadOnlyList<FutureBranch> branchView;
        public int MaximumBranches { get; }
        public int MaximumDepth { get; }
        public long HorizonMicroseconds { get; }
        public IReadOnlyList<FutureBranch> Branches => branchView;
        public FutureComposer(TransitionKernel kernel, int maximumBranches = 4, int maximumDepth = 3, long horizonMicroseconds = 30000000)
        {
            this.kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            branchView = branches.AsReadOnly();
            if (maximumBranches < 1 || maximumBranches > 4 || maximumDepth < 1 || maximumDepth > 3 || horizonMicroseconds < 1 || horizonMicroseconds > 30000000)
                throw new ArgumentOutOfRangeException(nameof(maximumBranches));
            MaximumBranches = maximumBranches; MaximumDepth = maximumDepth; HorizonMicroseconds = horizonMicroseconds;
        }
        public FutureBranch Begin(WorldSnapshot basis, string forecastId)
        {
            if (basis == null) throw new ArgumentNullException(nameof(basis));
            new StableId(forecastId);
            branches.Clear();
            var branch = new FutureBranch(forecastId, "branch-0", basis, new SimTick(checked(basis.Tick.Microseconds + HorizonMicroseconds)));
            branches.Add(branch); return branch;
        }
        public FutureBranch AddIntervention(ActorActionIntent hypothesis, ActionEvidence assumedEvidence, out string reason)
        {
            if (branches.Count == 0 || branches.Count >= MaximumBranches) { reason = "branch_capacity"; return null; }
            var basis = branches[0].Basis;
            if (hypothesis == null || hypothesis.Generation != basis.Generation || !hypothesis.RunId.Equals(basis.RunId) ||
                !basis.Actors.TryGetValue(hypothesis.ActorId, out var actor) || actor.RoleRevision != hypothesis.RoleRevision ||
                !TransitionKernel.ReadSetCurrent(basis.Entities, hypothesis.ReadSet)) { reason = "hypothesis_basis_mismatch"; return null; }
            var effect = ActionRules.Apply(basis.Entities, actor, hypothesis, assumedEvidence);
            if (!effect.Allowed || !effect.Complete) { reason = effect.Reason; return null; }
            var entities = TransitionKernel.Copy(basis.Entities);
            foreach (var entity in effect.Entities) entities[entity.Id] = entity;
            var state = new WorldSnapshot(basis.RunId, basis.Generation, checked(basis.Revision + 1), basis.Tick, basis.Mode,
                basis.RulesetId, basis.RulesetRevision, entities, TransitionKernel.Copy(basis.Actors));
            var branch = new FutureBranch(branches[0].ForecastId, "branch-" + branches.Count, basis, branches[0].HorizonEnd);
            var candidate = new GroundedCandidate("assumption-" + branches.Count, "actor-" + hypothesis.Verb, "authored-action", hypothesis.ActorId,
                hypothesis.TargetId, "가정한 개입 · 실제 수행 아님", hypothesis.ReadSet, Array.Empty<string>(), hypothesis.Verb, hypothesis.ToolId, hypothesis.RecipientId);
            branch.Record(new FutureStepRecord(-1, candidate, null, null), state, FutureStatus.Pending, "explicit_counterfactual_assumption_not_executed");
            branches.Add(branch); reason = "counterfactual_only"; return branch;
        }
        public IReadOnlyList<GroundedCandidate> Candidates(FutureBranch branch)
        {
            if (!branches.Contains(branch) || branch.Status != FutureStatus.Pending) return Array.Empty<GroundedCandidate>();
            return kernel.BuildEnvironmentCandidates(branch.State);
        }
        public bool ApplySelection(FutureBranch branch, IReadOnlyList<GroundedCandidate> offered, string selectedCandidateId,
            string requestSha256, string model, out string reason)
        {
            if (!branches.Contains(branch) || branch.Status != FutureStatus.Pending) { reason = "branch_not_pending"; return false; }
            if (string.IsNullOrEmpty(requestSha256) || string.IsNullOrEmpty(model)) { reason = "recorded_inference_required"; return false; }
            GroundedCandidate selected = null;
            foreach (var candidate in offered) if (candidate.CandidateId == selectedCandidateId) { selected = candidate; break; }
            if (selected == null) { reason = "candidate_not_offered"; return false; }
            var next = kernel.ApplyHypothetical(branch.State, selected, out reason);
            if (next == null) { branch.Stop(FutureStatus.Stale, reason); return false; }
            int depth = 0; foreach (var step in branch.Steps) if (step.Depth >= 0) depth++;
            var status = selected.TransitionId == "hold" ? FutureStatus.Complete : depth + 1 >= MaximumDepth ? FutureStatus.Truncated : FutureStatus.Pending;
            branch.Record(new FutureStepRecord(depth, selected, requestSha256, model), next, status,
                status == FutureStatus.Truncated ? "depth_bound_not_complete_future" : reason);
            return true;
        }
        public void Invalidate(string reason)
        { foreach (var branch in branches) if (branch.Status != FutureStatus.Stale) branch.Stop(FutureStatus.Stale, reason); }
    }
}
