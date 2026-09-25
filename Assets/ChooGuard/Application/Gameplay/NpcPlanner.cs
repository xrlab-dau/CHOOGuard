using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.Application.Gameplay
{
    /// <summary>One owner's private projection. Never accepts a world snapshot as knowledge.</summary>
    public sealed class NpcPlanner
    {
        private readonly StableId owner;
        private readonly Dictionary<StableId, WorldEntity> known = new Dictionary<StableId, WorldEntity>();
        private readonly Dictionary<StableId, long> blockedReads = new Dictionary<StableId, long>();
        private int blockedRouteRevision = -1;
        private long blockedRoleRevision = -1;
        private readonly IReadOnlyDictionary<StableId, WorldEntity> knownView;
        private IReadOnlyList<WorldEntity> projection;
        public IReadOnlyDictionary<StableId, WorldEntity> GetKnownEntities(ActorState actor)
        { RequireOwner(actor); return knownView; }

        public NpcPlanner(StableId ownerId)
        { owner = ownerId; knownView = new ReadOnlyDictionary<StableId, WorldEntity>(known); }

        public static bool IsVisibleFact(string predicate)
        {
            return predicate == "open" || predicate == "dirty" || predicate == "wet" || predicate == "hazardous" ||
                predicate == "portable" || predicate == "installed" || predicate == "cordoned" || predicate == "suspicious" ||
                predicate == "assistance.needed" || predicate == "practice.fixture" ||
                predicate == "tool.cleaning" || predicate == "tool.fastening" || predicate == "tool.measurement";
        }

        /// <summary>The sensor, not the planner, must establish LOS or own-action provenance first.</summary>
        public ActorState Observe(ActorState actor, WorldEntity entity, SimTick tick, ObservationSource source)
        {
            RequireOwner(actor);
            if (source == ObservationSource.Heard) throw new ArgumentException("Use Hear for reports without world truth.", nameof(source));
            known.TryGetValue(entity.Id, out var previous);
            var facts = previous == null ? new Dictionary<string, RuleTruth>(StringComparer.Ordinal) : new Dictionary<string, RuleTruth>(previous.Facts);
            foreach (var fact in entity.Facts)
                if (source == ObservationSource.OwnAction || source == ObservationSource.Instrument ||
                    source == ObservationSource.Assignment || IsVisibleFact(fact.Key)) facts[fact.Key] = fact.Value;
            var visible = new WorldEntity(entity.Id, entity.Label, entity.Kind, entity.Revision, entity.Position, entity.ZoneId,
                entity.CustodianId, entity.ParentId, entity.DefinitionId, facts);
            bool changed = previous == null || previous.Revision != visible.Revision || !SameFacts(previous, visible) ||
                !previous.CustodianId.Equals(visible.CustodianId) || !previous.ParentId.Equals(visible.ParentId) ||
                previous.Position.DistanceSquared(visible.Position) > 1f;
            if (!changed) return actor;
            // Stage knowledge in the immutable actor, never in a side cache before the world commits it.
            var knowledge = new List<WorldEntity>(actor.KnownEntities);
            for (int i = knowledge.Count - 1; i >= 0; i--) if (knowledge[i].Id.Equals(entity.Id)) knowledge.RemoveAt(i);
            if (knowledge.Count == 64)
            {
                int discard = knowledge.FindIndex(item => !item.Id.Equals(owner) && !item.CustodianId.Equals(owner) &&
                    (actor.Plan == null || !item.Id.Equals(actor.Plan.TargetId)));
                if (discard < 0) return actor;
                knowledge.RemoveAt(discard);
            }
            knowledge.Add(visible);
            var memories = new List<ActorObservation>(actor.Memories);
            AddMemory(memories, new ActorObservation(owner, entity.Id, entity.Revision, tick, "observed", RuleTruth.TRUE, source));
            foreach (var fact in entity.Facts)
                if (source == ObservationSource.OwnAction || source == ObservationSource.Instrument ||
                    source == ObservationSource.Assignment || IsVisibleFact(fact.Key))
                    AddMemory(memories, new ActorObservation(owner, entity.Id, entity.Revision, tick, fact.Key, fact.Value, source));
            return actor.With(knowledgeRevision: checked(actor.KnowledgeRevision + 1), memories: memories, knownEntities: knowledge);
        }

        public ActorState ObserveOwnEffect(ActorState actor, WorldEntity entity, ActionVerb verb, SimTick tick)
        {
            // Observing the effect of a button or completed action is not an instrument for every hidden fact.
            var facts = new Dictionary<string, RuleTruth>(StringComparer.Ordinal);
            foreach (var fact in entity.Facts)
                if (IsVisibleFact(fact.Key) || verb == ActionVerb.Observe && fact.Key == "inspected" ||
                    verb == ActionVerb.Verify && fact.Key == "practice.verified" ||
                    verb == ActionVerb.Consent && fact.Key.StartsWith("consent:", StringComparison.Ordinal) ||
                    verb == ActionVerb.RequestHelp && fact.Key == "assistance.requested" ||
                    verb == ActionVerb.Report && fact.Key == "reported") facts.Add(fact.Key, fact.Value);
            return Observe(actor, new WorldEntity(entity.Id, entity.Label, entity.Kind, entity.Revision, entity.Position,
                entity.ZoneId, entity.CustodianId, entity.ParentId, entity.DefinitionId, facts), tick, ObservationSource.OwnAction);
        }

        public ActorState Hear(ActorState actor, StableId speaker, StableId subject, long revision, string predicate, RuleTruth claim, SimTick tick)
        {
            RequireOwner(actor);
            var memories = new List<ActorObservation>(actor.Memories);
            for (int i = memories.Count - 1; i >= 0; i--)
            {
                var old = memories[i];
                if (old.Source == ObservationSource.Heard && old.EntityId.Equals(subject) && old.FactId == predicate &&
                    old.InformantId.Equals(speaker) && old.EntityRevision == revision && old.Value == claim) return actor;
            }
            AddMemory(memories, new ActorObservation(owner, subject, revision, tick, predicate, claim, ObservationSource.Heard, speaker));
            // No entity or fact is revealed by a report. It can motivate observation/help, not authorize work.
            return actor.With(knowledgeRevision: checked(actor.KnowledgeRevision + 1), memories: memories);
        }

        public ActorState ObserveResourceActivity(ActorState actor, WorldEntity target, SimTick tick)
        {
            RequireOwner(actor);
            if (!known.ContainsKey(target.Id)) return actor;
            var memories = new List<ActorObservation>(actor.Memories);
            AddMemory(memories, new ActorObservation(owner, target.Id, target.Revision, tick,
                "resource.activity", RuleTruth.UNKNOWN, ObservationSource.Sight));
            return actor.With(knowledgeRevision: checked(actor.KnowledgeRevision + 1), memories: memories);
        }

        public RuleTruth KnownFact(ActorState actor, StableId entity, string predicate)
        {
            RequireOwner(actor);
            return known.TryGetValue(entity, out var value) ? value.Fact(predicate) : RuleTruth.UNKNOWN;
        }

        public ActorState FormGoal(ActorState actor)
        {
            RequireOwner(actor);
            if (actor.Plan != null) return actor; // Responsibilities survive blocked paths and visual culling.
            WorldEntity best = null;
            GoalKind kind = GoalKind.KeepSchedule;
            double bestPriority = -1;
            foreach (var entity in known.Values)
            {
                if (entity.Id.Equals(owner)) continue;
                GoalKind proposed;
                double priority;
                if (entity.Fact("hazardous") == RuleTruth.TRUE || entity.Fact("suspicious") == RuleTruth.TRUE)
                { proposed = GoalKind.SeekSafety; priority = 2; }
                else if (entity.Kind == EntityKind.Actor && entity.Fact("assistance.needed") == RuleTruth.TRUE && actor.Role != ActorRole.Citizen)
                { proposed = GoalKind.AssistPerson; priority = 1.5; }
                else if (actor.Role == ActorRole.Cleaning && entity.Fact("dirty") == RuleTruth.TRUE)
                { proposed = GoalKind.RestoreCleanliness; priority = .7; }
                else if (actor.Role == ActorRole.Maintenance && entity.Kind == EntityKind.Equipment && entity.Fact("inspected") != RuleTruth.TRUE)
                { proposed = GoalKind.InspectEquipment; priority = .6; }
                else if ((actor.Role == ActorRole.StationStaff || actor.Role == ActorRole.PassengerService) && entity.Kind == EntityKind.Door && entity.Fact("open") == RuleTruth.FALSE)
                { proposed = GoalKind.MaintainAccess; priority = .6; }
                else if (entity.Kind == EntityKind.Exit || entity.Kind == EntityKind.Zone)
                { proposed = GoalKind.KeepSchedule; priority = .2; }
                else continue;
                bool fulfilled = false;
                foreach (var memory in actor.Memories)
                    if (memory.EntityId.Equals(entity.Id) && memory.FactId == "goal.completed:" + proposed &&
                        memory.Source == ObservationSource.OwnAction && memory.EntityRevision == entity.Revision) { fulfilled = true; break; }
                if (fulfilled) continue;
                if (actor.Drives.TryGetValue(proposed, out var drive)) priority += drive;
                if (priority > bestPriority || (priority == bestPriority && string.CompareOrdinal(entity.Id.Value, best.Id.Value) < 0))
                { best = entity; kind = proposed; bestPriority = priority; }
            }
            if (best == null) return actor;
            var steps = Steps(kind);
            var plan = new GoalPlan(new StableId("goal:" + Guid.NewGuid().ToString("N")), kind, best.Id, 0,
                Reason(kind, best.Label), Completion(kind), steps);
            var drives = new Dictionary<GoalKind, double>(actor.Drives);
            if (!drives.ContainsKey(kind)) drives[kind] = .5;
            return actor.With(plan: plan, replacePlan: true, drives: drives);
        }

        public bool CanReplan(ActorState actor, int routeRevision)
        {
            RequireOwner(actor);
            if (actor.Plan == null || actor.Plan.BlockedReason == null) return true;
            if (actor.RoleRevision != blockedRoleRevision || routeRevision != blockedRouteRevision) return true;
            foreach (var read in blockedReads)
                if (known.TryGetValue(read.Key, out var entity) && entity.Revision != read.Value) return true;
            // A new relevant report may offer help; unrelated crowd movement does not wake a blocked plan.
            for (int i = actor.Memories.Count - 1; i >= 0; i--)
            {
                var memory = actor.Memories[i];
                if ((memory.EntityId.Equals(actor.Plan.TargetId) && memory.Source == ObservationSource.Heard ||
                     memory.FactId == "resource.activity" && blockedReads.ContainsKey(memory.EntityId)) &&
                    memory.Tick.Microseconds > blockedTick) return true;
                if (memory.Tick.Microseconds > blockedTick && memory.Source != ObservationSource.Heard && memory.Value == RuleTruth.TRUE &&
                    (memory.FactId == "hazardous" || memory.FactId == "suspicious" ||
                     actor.Plan.Kind == GoalKind.RestoreCleanliness && memory.FactId == "tool.cleaning")) return true;
                if (memory.Tick.Microseconds > blockedTick && memory.Source == ObservationSource.Heard && memory.FactId == "assistance.request") return true;
            }
            return false;
        }
        private long blockedTick;

        public ActorState Block(ActorState actor, string reason, int routeRevision, SimTick tick, IReadOnlyList<GroundedCandidate> candidates = null)
        {
            RequireOwner(actor);
            if (actor.Plan == null) return actor;
            blockedReads.Clear();
            if (known.TryGetValue(actor.Plan.TargetId, out var target)) blockedReads[target.Id] = target.Revision;
            if (candidates != null)
                foreach (var candidate in candidates) foreach (var read in candidate.ReadSet) blockedReads[read.EntityId] = read.Revision;
            blockedRouteRevision = routeRevision; blockedRoleRevision = actor.RoleRevision; blockedTick = tick.Microseconds;
            var p = actor.Plan;
            return actor.With(plan: new GoalPlan(p.Id, p.Kind, p.TargetId, checked(p.Revision + 1), p.Reason, p.CompletionFact,
                p.RemainingSteps, reason, actor.KnowledgeRevision), replacePlan: true);
        }

        public ActorState Resume(ActorState actor)
        {
            RequireOwner(actor);
            var p = actor.Plan;
            if (p == null || p.BlockedReason == null) return actor;
            return actor.With(plan: new GoalPlan(p.Id, p.Kind, p.TargetId, checked(p.Revision + 1), p.Reason,
                p.CompletionFact, p.RemainingSteps), replacePlan: true);
        }

        public ActorState CompleteAction(ActorState actor, ActorActionIntent action)
        {
            RequireOwner(actor);
            var p = actor.Plan;
            if (p == null) return actor;
            bool completed = action.TargetId.Equals(p.TargetId) &&
                ((p.Kind == GoalKind.KeepSchedule && action.Verb == ActionVerb.MoveTo) ||
                 (p.Kind == GoalKind.RestoreCleanliness && action.Verb == ActionVerb.Clean && KnownFact(actor, p.TargetId, "dirty") == RuleTruth.FALSE) ||
                 (p.Kind == GoalKind.InspectEquipment && action.Verb == ActionVerb.Observe && KnownFact(actor, p.TargetId, "inspected") == RuleTruth.TRUE) ||
                 (p.Kind == GoalKind.MaintainAccess && action.Verb == ActionVerb.Open && KnownFact(actor, p.TargetId, "open") == RuleTruth.TRUE) ||
                 ((p.Kind == GoalKind.AssistPerson || p.Kind == GoalKind.HonourPromise) && action.Verb == ActionVerb.Handoff));
            if (completed)
            {
                var drives = new Dictionary<GoalKind, double>(actor.Drives); drives[p.Kind] = 0;
                var memories = new List<ActorObservation>(actor.Memories);
                if (known.TryGetValue(p.TargetId, out var completedTarget))
                    AddMemory(memories, new ActorObservation(owner, p.TargetId, completedTarget.Revision,
                        LastObservedTick(actor, p.TargetId), "goal.completed:" + p.Kind, RuleTruth.TRUE, ObservationSource.OwnAction));
                return actor.With(plan: null, replacePlan: true, drives: drives, memories: memories);
            }
            var steps = new List<ActionVerb>(p.RemainingSteps);
            if (steps.Count > 0 && steps[0] == action.Verb) steps.RemoveAt(0);
            return actor.With(plan: new GoalPlan(p.Id, p.Kind, p.TargetId, checked(p.Revision + 1), p.Reason,
                p.CompletionFact, steps), replacePlan: true);
        }

        public List<GroundedCandidate> BuildCandidates(ActorState actor)
        {
            RequireOwner(actor);
            var result = new List<GroundedCandidate>(12);
            if (actor.Plan == null || !known.TryGetValue(actor.Plan.TargetId, out var target)) return result;
            if (target.Fact("inspected") != RuleTruth.TRUE) Add(result, actor, ActionVerb.Observe, target, "직접 확인합니다");
            Add(result, actor, ActionVerb.Wait, target, "목표를 유지하며 기다립니다");
            if (target.Kind == EntityKind.Actor || target.Kind == EntityKind.Zone || target.Kind == EntityKind.Exit)
                Add(result, actor, ActionVerb.MoveTo, target, "목표의 접근 가능한 위치로 이동합니다");
            // A new local hazard can interrupt routine work without deleting the original responsibility.
            foreach (var hazard in known.Values)
                if ((hazard.Fact("hazardous") == RuleTruth.TRUE || hazard.Fact("suspicious") == RuleTruth.TRUE) && hazard.Fact("reported") != RuleTruth.TRUE)
                    Add(result, actor, ActionVerb.Report, hazard, "직접 관측한 위험을 우선 보고하되 기존 책임을 유지합니다");
            switch (actor.Plan.Kind)
            {
                case GoalKind.RestoreCleanliness:
                    if (target.Fact("dirty") == RuleTruth.TRUE)
                    {
                        WorldEntity tool = FindOwnedTool("clean");
                        if (tool != null) Add(result, actor, ActionVerb.Clean, target, "보유한 청소 도구로 실제 오염을 닦습니다", tool.Id);
                        else AddToolAcquisition(result, actor, "clean");
                    }
                    break;
                case GoalKind.InspectEquipment:
                    Add(result, actor, ActionVerb.Verify, target, "확인 가능한 점검 근거를 검수합니다");
                    break;
                case GoalKind.MaintainAccess:
                    if (target.Fact("open") == RuleTruth.FALSE) Add(result, actor, ActionVerb.Open, target, "통행문 개방을 시도합니다");
                    break;
                case GoalKind.SeekSafety:
                    Add(result, actor, ActionVerb.Report, target, "직접 관측한 위험을 보고합니다");
                    break;
                case GoalKind.AssistPerson:
                case GoalKind.HonourPromise:
                    if (target.Kind == EntityKind.Actor && target.Fact("consent:" + owner.Value) != RuleTruth.TRUE)
                        Add(result, actor, ActionVerb.RequestHelp, target, "상대에게 지원 의사와 동의를 묻습니다", recipient: target.Id);
                    if (target.Fact("consent:" + owner.Value) == RuleTruth.TRUE && !target.CustodianId.Equals(owner))
                        Add(result, actor, ActionVerb.Escort, target, "동의한 상대에게 접근하여 동행을 시작합니다");
                    if (target.CustodianId.Equals(owner))
                        foreach (var destination in known.Values)
                            if (destination.Kind == EntityKind.Exit || destination.Kind == EntityKind.Zone)
                                Add(result, actor, ActionVerb.MoveTo, destination, "지원 책임을 유지하며 동행할 위치로 이동합니다");
                    foreach (var colleague in known.Values)
                        if (colleague.Kind == EntityKind.Actor && !colleague.Id.Equals(owner) && !colleague.Id.Equals(target.Id))
                        {
                            Add(result, actor, ActionVerb.RequestHelp, target, "주변 동료에게 실제 지원을 요청합니다", recipient: colleague.Id);
                            if (target.CustodianId.Equals(owner)) Add(result, actor, ActionVerb.Handoff, target, "현장 동료에게 책임 인계를 요청합니다", recipient: colleague.Id);
                            if (result.Count >= 24) break;
                        }
                    break;
            }
            // Consent belongs to the subject's own choice, never to the requesting speaker's prose.
            if (known.TryGetValue(owner, out var self))
                foreach (var memory in actor.Memories)
                    if (memory.Source == ObservationSource.Heard && memory.FactId == "assistance.request" && memory.InformantId.HasValue &&
                        known.TryGetValue(memory.InformantId.Value, out var requester) && self.Fact("consent:" + requester.Id.Value) != RuleTruth.TRUE)
                    {
                        Add(result, actor, ActionVerb.Consent, self, "본인의 판단으로 지원에 동의합니다", recipient: requester.Id);
                        Add(result, actor, ActionVerb.MoveTo, requester, "받은 요청을 확인하기 위해 상대에게 접근합니다");
                    }
            return result;
        }

        private static SimTick LastObservedTick(ActorState actor, StableId entity)
        {
            for (int i = actor.Memories.Count - 1; i >= 0; i--)
                if (actor.Memories[i].EntityId.Equals(entity)) return actor.Memories[i].Tick;
            return new SimTick(0);
        }

        private void AddToolAcquisition(List<GroundedCandidate> output, ActorState actor, string purpose)
        {
            foreach (var entity in known.Values)
                if (entity.Kind == EntityKind.Tool && !entity.CustodianId.HasValue && entity.Fact("portable") == RuleTruth.TRUE &&
                    entity.Fact(ToolCapability(purpose)) == RuleTruth.TRUE)
                { Add(output, actor, ActionVerb.PickUp, entity, "보이는 사용 가능한 도구를 집습니다"); break; }
        }
        private WorldEntity FindOwnedTool(string purpose)
        {
            foreach (var entity in known.Values)
                if (entity.Kind == EntityKind.Tool && entity.CustodianId.Equals(owner) &&
                    entity.Fact(ToolCapability(purpose)) == RuleTruth.TRUE) return entity;
            return null;
        }
        private static string ToolCapability(string purpose) => purpose == "clean" ? "tool.cleaning" : purpose == "fasten" ? "tool.fastening" : "tool.measurement";
        private void Add(List<GroundedCandidate> output, ActorState actor, ActionVerb verb, WorldEntity target, string text, StableId? tool = null, StableId? recipient = null)
        {
            if (output.Count >= 32) return;
            var reads = new List<EntityReadVersion> { new EntityReadVersion(target.Id, target.Revision) };
            if (tool.HasValue && known.TryGetValue(tool.Value, out var t)) reads.Add(new EntityReadVersion(t.Id, t.Revision));
            if (recipient.HasValue && known.TryGetValue(recipient.Value, out var r) && !r.Id.Equals(target.Id)) reads.Add(new EntityReadVersion(r.Id, r.Revision));
            string id = "c:" + output.Count + ":" + verb.ToString().ToLowerInvariant();
            output.Add(new GroundedCandidate(id, "action:" + verb.ToString().ToLowerInvariant(), "gameplay.v1", owner, target.Id,
                target.Label + " — " + text, reads, Array.Empty<string>(), verb, tool, recipient));
        }
        private void RequireOwner(ActorState actor)
        {
            if (actor == null || !actor.Id.Equals(owner)) throw new ArgumentException("Private planner owner mismatch.", nameof(actor));
            if (ReferenceEquals(projection, actor.KnownEntities)) return;
            known.Clear();
            foreach (var entity in actor.KnownEntities) known.Add(entity.Id, entity);
            projection = actor.KnownEntities;
        }
        private static bool SameFacts(WorldEntity a, WorldEntity b)
        {
            if (a.Facts.Count != b.Facts.Count) return false;
            foreach (var fact in a.Facts) if (b.Fact(fact.Key) != fact.Value) return false;
            return true;
        }
        private static void AddMemory(List<ActorObservation> memories, ActorObservation value)
        {
            for (int i = memories.Count - 1; i >= 0; i--)
                if (memories[i].EntityId.Equals(value.EntityId) && memories[i].FactId == value.FactId && memories[i].Source == value.Source &&
                    memories[i].InformantId.Equals(value.InformantId)) memories.RemoveAt(i);
            if (memories.Count == 64) memories.RemoveAt(0);
            memories.Add(value);
        }
        private static ActionVerb[] Steps(GoalKind kind)
        {
            switch (kind)
            {
                case GoalKind.RestoreCleanliness: return new[] { ActionVerb.MoveTo, ActionVerb.Clean, ActionVerb.Verify };
                case GoalKind.InspectEquipment: return new[] { ActionVerb.MoveTo, ActionVerb.Observe, ActionVerb.Verify };
                case GoalKind.AssistPerson: case GoalKind.HonourPromise: return new[] { ActionVerb.RequestHelp, ActionVerb.Escort, ActionVerb.Handoff };
                case GoalKind.MaintainAccess: return new[] { ActionVerb.MoveTo, ActionVerb.Open };
                case GoalKind.SeekSafety: return new[] { ActionVerb.Report, ActionVerb.RequestHelp };
                default: return new[] { ActionVerb.MoveTo };
            }
        }
        private static string Completion(GoalKind kind)
        {
            switch (kind)
            {
                case GoalKind.RestoreCleanliness: return "dirty:FALSE";
                case GoalKind.InspectEquipment: return "inspected";
                case GoalKind.AssistPerson: case GoalKind.HonourPromise: return "responsibility.handed.off";
                case GoalKind.MaintainAccess: return "open";
                case GoalKind.SeekSafety: return "safe.location.observed";
                default: return "destination.reached";
            }
        }
        private static string Reason(GoalKind kind, string label)
        {
            switch (kind)
            {
                case GoalKind.InspectEquipment: return label + "의 관측 가능한 상태를 점검하려고 합니다.";
                case GoalKind.RestoreCleanliness: return label + "의 보이는 오염을 제거하려고 합니다.";
                case GoalKind.AssistPerson: return label + "의 의사를 확인하고 지원하려고 합니다.";
                case GoalKind.HonourPromise: return label + "에 관해 맡은 약속을 이행하려고 합니다.";
                case GoalKind.MaintainAccess: return label + "의 통행 가능 조건을 확인하려고 합니다.";
                case GoalKind.SeekSafety: return label + "에서 관측한 위험을 알리고 안전한 이동을 확인하려고 합니다.";
                default: return label + " 목적지로 이동하려고 합니다.";
            }
        }
    }
}
