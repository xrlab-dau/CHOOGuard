using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.Domain.Gameplay
{
    public enum TransitionRelation { Self, ContainingZone, SameZone }

    /// <summary>Authored atomic causal rule. Model responses never supply definitions or effects.</summary>
    public sealed class TransitionDefinition
    {
        public string Id { get; }
        public string ParameterSetId { get; }
        public string Label { get; }
        public EntityKind SourceKind { get; }
        public EntityKind TargetKind { get; }
        public TransitionRelation Relation { get; }
        public IReadOnlyDictionary<string, RuleTruth> SourceConditions { get; }
        public IReadOnlyDictionary<string, RuleTruth> TargetConditions { get; }
        public IReadOnlyDictionary<string, RuleTruth> Effects { get; }
        public TransitionDefinition(string id, string parameterSetId, string label, EntityKind sourceKind, EntityKind targetKind,
            TransitionRelation relation, IDictionary<string, RuleTruth> sourceConditions, IDictionary<string, RuleTruth> targetConditions,
            IDictionary<string, RuleTruth> effects)
        {
            new StableId(id); new StableId(parameterSetId);
            if (id == "hold" || sourceConditions == null || sourceConditions.Count == 0 || effects == null || effects.Count == 0)
                throw new ArgumentException("원인과 실제 효과가 있는 원자 전이가 필요합니다.");
            Id = id; ParameterSetId = parameterSetId; Label = label ?? id; SourceKind = sourceKind; TargetKind = targetKind; Relation = relation;
            SourceConditions = new ReadOnlyDictionary<string, RuleTruth>(new Dictionary<string, RuleTruth>(sourceConditions));
            TargetConditions = new ReadOnlyDictionary<string, RuleTruth>(new Dictionary<string, RuleTruth>(targetConditions ?? new Dictionary<string, RuleTruth>()));
            Effects = new ReadOnlyDictionary<string, RuleTruth>(new Dictionary<string, RuleTruth>(effects));
            foreach (var pair in Effects) if (pair.Value != RuleTruth.TRUE && pair.Value != RuleTruth.FALSE) throw new ArgumentException("전이 효과는 확인 가능한 명시적 사실이어야 합니다.");
        }
    }

    public sealed class TransitionKernel
    {
        private readonly Dictionary<string, TransitionDefinition> definitions = new Dictionary<string, TransitionDefinition>(StringComparer.Ordinal);
        public TransitionKernel(IEnumerable<TransitionDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (var definition in definitions) this.definitions.Add(definition.Id, definition);
        }
        public IReadOnlyList<GroundedCandidate> BuildEnvironmentCandidates(WorldSnapshot state, int maximum = 32)
        {
            if (maximum < 2 || maximum > 32) throw new ArgumentOutOfRangeException(nameof(maximum));
            var result = new List<GroundedCandidate>();
            WorldEntity holdTarget = null;
            foreach (var source in state.Entities.Values)
            {
                if (holdTarget == null || StringComparer.Ordinal.Compare(source.Id.Value, holdTarget.Id.Value) < 0) holdTarget = source;
                foreach (var rule in definitions.Values)
                {
                    if (source.Kind != rule.SourceKind || !Matches(source, rule.SourceConditions)) continue;
                    foreach (var target in state.Entities.Values)
                    {
                        if (!Applicable(rule, source, target) || !Changes(rule, target)) continue;
                        var reads = new List<EntityReadVersion> { new EntityReadVersion(source.Id, source.Revision) };
                        if (!source.Id.Equals(target.Id)) reads.Add(new EntityReadVersion(target.Id, target.Revision));
                        var causes = new List<string>();
                        foreach (var fact in rule.SourceConditions) causes.Add(FactId(source.Id, fact.Key));
                        foreach (var fact in rule.TargetConditions) causes.Add(FactId(target.Id, fact.Key));
                        result.Add(new GroundedCandidate(CandidateId(rule.Id, source.Id, target.Id), rule.Id, rule.ParameterSetId,
                            null, target.Id, rule.Label + " · " + source.Label + " → " + target.Label, reads, causes, sourceId: source.Id));
                        if (result.Count == maximum - 1) break;
                    }
                    if (result.Count == maximum - 1) break;
                }
                if (result.Count == maximum - 1) break;
            }
            // Waiting is absence of a new effect, not a guessed alternative emergency.
            if (holdTarget != null) result.Add(new GroundedCandidate("env-hold", "hold", "none", null, holdTarget.Id,
                "현재 원인에서 새 환경 전이를 확정하지 않음", new[] { new EntityReadVersion(holdTarget.Id, holdTarget.Revision) },
                Array.Empty<string>(), sourceId: holdTarget.Id));
            return result.AsReadOnly();
        }
        public static bool IsEnvironmentHold(GroundedCandidate candidate)
        {
            return candidate != null && candidate.CandidateId == "env-hold" && candidate.TransitionId == "hold" &&
                candidate.ParameterSetId == "none" && !candidate.ActorId.HasValue && !candidate.Verb.HasValue &&
                !candidate.ToolId.HasValue && !candidate.RecipientId.HasValue && candidate.SourceId.Equals(candidate.TargetId) &&
                candidate.CauseFactIds.Count == 0 && candidate.ReadSet.Count == 1 && candidate.ReadSet[0].EntityId.Equals(candidate.SourceId);
        }
        public bool TryApply(IReadOnlyDictionary<StableId, WorldEntity> entities, GroundedCandidate candidate,
            out WorldEntity changed, out string reason)
        {
            changed = null;
            if (candidate == null || candidate.ActorId.HasValue || candidate.Verb.HasValue) { reason = "not_environment_transition"; return false; }
            if (candidate.TransitionId == "hold" || candidate.CandidateId == "env-hold")
            {
                if (!IsEnvironmentHold(candidate) || !ReadSetCurrent(entities, candidate.ReadSet))
                { reason = "invalid_hold"; return false; }
                reason = "no_new_effect"; return true;
            }
            if (!definitions.TryGetValue(candidate.TransitionId, out var rule) || rule.ParameterSetId != candidate.ParameterSetId)
            { reason = "unregistered_transition"; return false; }
            if (!entities.TryGetValue(candidate.SourceId, out var source) || !entities.TryGetValue(candidate.TargetId, out var target))
            { reason = "binding_missing"; return false; }
            if (!ReadSetCurrent(entities, candidate.ReadSet) || !Covers(candidate.ReadSet, source) || !Covers(candidate.ReadSet, target))
            { reason = "stale_read_set"; return false; }
            if (!Applicable(rule, source, target) || !Changes(rule, target)) { reason = "cause_no_longer_true"; return false; }
            var facts = Copy(target.Facts);
            foreach (var effect in rule.Effects) facts[effect.Key] = effect.Value;
            changed = target.With(checked(target.Revision + 1), facts: facts);
            reason = "applied_authored_gameplay_rule";
            return true;
        }
        public WorldSnapshot ApplyHypothetical(WorldSnapshot state, GroundedCandidate candidate, out string reason)
        {
            if (!TryApply(state.Entities, candidate, out var changed, out reason)) return null;
            if (changed == null) return state;
            var entities = Copy(state.Entities); entities[changed.Id] = changed;
            return new WorldSnapshot(state.RunId, state.Generation, checked(state.Revision + 1), state.Tick, state.Mode,
                state.RulesetId, state.RulesetRevision, entities, Copy(state.Actors));
        }
        public static bool ReadSetCurrent(IReadOnlyDictionary<StableId, WorldEntity> entities, IReadOnlyList<EntityReadVersion> reads)
        {
            var seen = new HashSet<StableId>();
            foreach (var read in reads)
                if (!seen.Add(read.EntityId) || !entities.TryGetValue(read.EntityId, out var entity) || entity.Revision != read.Revision) return false;
            return true;
        }
        private static bool Covers(IReadOnlyList<EntityReadVersion> reads, WorldEntity entity)
        { foreach (var read in reads) if (read.EntityId.Equals(entity.Id) && read.Revision == entity.Revision) return true; return false; }
        private static bool Applicable(TransitionDefinition rule, WorldEntity source, WorldEntity target)
        {
            if (source.Kind != rule.SourceKind || target.Kind != rule.TargetKind || !Matches(source, rule.SourceConditions) || !Matches(target, rule.TargetConditions)) return false;
            switch (rule.Relation)
            {
                case TransitionRelation.Self: return source.Id.Equals(target.Id);
                case TransitionRelation.ContainingZone: return source.ZoneId.Equals(target.Id);
                case TransitionRelation.SameZone: return source.ZoneId.Equals(target.ZoneId) && !source.Id.Equals(target.Id);
                default: return false;
            }
        }
        private static bool Matches(WorldEntity entity, IReadOnlyDictionary<string, RuleTruth> conditions)
        { foreach (var condition in conditions) if (entity.Fact(condition.Key) != condition.Value) return false; return true; }
        private static bool Changes(TransitionDefinition rule, WorldEntity entity)
        { foreach (var effect in rule.Effects) if (entity.Fact(effect.Key) != effect.Value) return true; return false; }
        public static Dictionary<TKey, TValue> Copy<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source)
        { var result = new Dictionary<TKey, TValue>(source.Count); foreach (var item in source) result.Add(item.Key, item.Value); return result; }
        public static string FactId(StableId entityId, string fact) => CandidateId("fact", entityId, new StableId(fact.Replace('/', ':')));
        private static string CandidateId(string rule, StableId source, StableId target)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(rule + "\n" + source.Value + "\n" + target.Value));
                var text = new StringBuilder(36); text.Append(rule == "fact" ? "fact-" : "env-");
                for (int i = 0; i < 12; i++) text.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }
    }
}
