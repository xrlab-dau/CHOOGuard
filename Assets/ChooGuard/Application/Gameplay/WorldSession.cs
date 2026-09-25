using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;

namespace ChooGuard.Application.Gameplay
{
    public sealed class WorldSession : IDisposable
    {
        private sealed class RunningAction
        {
            public ActorActionIntent Intent;
            public string Fingerprint;
            public ActionReceipt Receipt;
            public long LastWorkTick = -1;
            public readonly List<EntityReadVersion> Reads = new List<EntityReadVersion>();
            public readonly List<StableId> Resources = new List<StableId>();
        }
        private readonly OperationsSession writer;
        private readonly IGameplayCommitSink sink;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<StableId, WorldEntity> entities;
        private readonly Dictionary<StableId, ActorState> actors;
        private readonly Dictionary<StableId, WorldPoint> poses = new Dictionary<StableId, WorldPoint>();
        private readonly Dictionary<StableId, WorldEntity> poseBindings = new Dictionary<StableId, WorldEntity>();
        private readonly PoseView poseView;
        private readonly Dictionary<StableId, RunningAction> active = new Dictionary<StableId, RunningAction>();
        private readonly Dictionary<StableId, StableId> reservations = new Dictionary<StableId, StableId>();
        private readonly Dictionary<string, RunningAction> receipts = new Dictionary<string, RunningAction>(StringComparer.Ordinal);
        private readonly Queue<string> receiptOrder = new Queue<string>();
        private readonly HashSet<string> environmentReceipts = new HashSet<string>(StringComparer.Ordinal);
        private bool disposed;
        public StableId RunId { get; }
        public long Generation { get; private set; }
        public long Revision { get; private set; }
        public SimTick Tick { get; private set; }
        public GameplayMode Mode { get; }
        public string RulesetId { get; }
        public long RulesetRevision { get; }
        public event Action<WorldMutation> Committed;
        public event Action<Exception> ObserverFailed;

        public WorldSession(WorldSnapshot initial, IGameplayCommitSink sink)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            RunId = initial.RunId; Generation = initial.Generation; Revision = initial.Revision; Tick = initial.Tick; Mode = initial.Mode;
            RulesetId = initial.RulesetId; RulesetRevision = initial.RulesetRevision;
            entities = TransitionKernel.Copy(initial.Entities); actors = TransitionKernel.Copy(initial.Actors);
            poseView = new PoseView(entities, poseBindings);
            foreach (var actor in actors.Values)
                if (!entities.TryGetValue(actor.Id, out var entity) || entity.Kind != EntityKind.Actor) throw new ArgumentException("인물의 실제 world entity가 없습니다.", nameof(initial));
            writer = new OperationsSession(RunId);
            writer.ExecuteWorld(Tick, () => { });
        }
        public bool TryGetEntity(StableId id, out WorldEntity entity)
        {
            CheckThread();
            if (!entities.TryGetValue(id, out entity)) return false;
            if (poses.TryGetValue(id, out var pose) && entity.Position.DistanceSquared(pose) > 0.000001f) entity = entity.With(entity.Revision, position: pose);
            return true;
        }
        public bool TryGetActor(StableId id, out ActorState actor) { CheckThread(); return actors.TryGetValue(id, out actor); }
        public WorldSnapshot Snapshot()
        {
            CheckThread(); var copy = new Dictionary<StableId, WorldEntity>(entities);
            foreach (var pose in poses) if (copy.TryGetValue(pose.Key, out var entity)) copy[pose.Key] = entity.With(entity.Revision, position: pose.Value);
            return new WorldSnapshot(RunId, Generation, Revision, Tick, Mode, RulesetId, RulesetRevision, copy, actors);
        }
        public void SetActorPose(StableId actorId, WorldPoint position)
        { CheckThread(); if (!actors.ContainsKey(actorId)) throw new ArgumentException("등록되지 않은 인물입니다.", nameof(actorId)); poses[actorId] = position; }
        public void SetPhysicalPose(StableId entityId, WorldPoint position)
        {
            CheckThread();
            if (!entities.TryGetValue(entityId, out var entity)) throw new ArgumentException("등록되지 않은 물체입니다.", nameof(entityId));
            poses[entityId] = position;
            if (entity.Kind != EntityKind.Sign || !entity.ParentId.HasValue ||
                !TryGetEntity(entity.ParentId.Value, out var target) || target.Fact("cordon.sign:" + entityId.Value) != RuleTruth.TRUE) return;
            if (!entity.CustodianId.HasValue && position.DistanceSquared(target.Position) <= ActionRules.CordonPlacementDistance * ActionRules.CordonPlacementDistance) return;
            poseBindings.Clear(); HydratePose(target.Id);
            foreach (var item in entities.Values) if (item.Kind == EntityKind.Sign) HydratePose(item.Id);
            var revoked = ActionRules.RevalidateCordons(poseView, entityId, position);
            if (revoked.Count == 0) return;
            var changed = new List<WorldEntity>(revoked) { entity.With(checked(entity.Revision + 1), position: position) };
            Publish(Mutation("world.cordon-placement", null, "physical-sign-placement-revoked", changed, null));
        }
        private void HydratePose(StableId id)
        {
            if (poses.TryGetValue(id, out var position))
            {
                var entity = entities[id];
                if (entity.Position.DistanceSquared(position) > 0.000001f)
                    poseBindings[id] = entity.With(entity.Revision, position: position);
            }
        }
        public void FlushPoses()
        {
            CheckThread(); var changed = new List<WorldEntity>();
            foreach (var pose in poses)
                if (entities.TryGetValue(pose.Key, out var entity) && entity.Position.DistanceSquared(pose.Value) > 0.000001f)
                    changed.Add(entity.With(checked(entity.Revision + 1), position: pose.Value));
            if (changed.Count > 0) Publish(Mutation("world.pose-checkpoint", null, "designated-motion-owners", changed, null));
        }
        public void SaveCheckpoint()
        {
            CheckThread(); FlushPoses();
            Publish(Mutation("world.save", null, "durable-semantic-and-pose-state", null, null));
        }
        public void AdvanceTime(SimTick tick)
        {
            CheckThread(); if (tick.Microseconds < Tick.Microseconds) throw new ArgumentOutOfRangeException(nameof(tick));
            if (tick.Microseconds == Tick.Microseconds) return;
            writer.ExecuteWorld(tick, () => Tick = tick);
        }
        public ActorActionIntent CreateIntent(StableId actorId, ActionVerb verb, StableId targetId,
            StableId? toolId = null, StableId? recipientId = null, string workPointId = null)
        {
            CheckThread();
            if (!actors.TryGetValue(actorId, out var actor) || !entities.ContainsKey(targetId)) throw new ArgumentException("등록된 인물과 대상이 필요합니다.");
            var reads = new List<EntityReadVersion>(); AddRead(reads, targetId);
            if (toolId.HasValue) AddRead(reads, toolId.Value);
            if (recipientId.HasValue) AddRead(reads, recipientId.Value);
            if (verb == ActionVerb.Remove && entities[targetId].ParentId.HasValue) AddRead(reads, entities[targetId].ParentId.Value);
            long sequence = checked(actor.ActionSequence + 1);
            return new ActorActionIntent(RunId, Generation, actorId, new StableId("action-" + Generation.ToString(CultureInfo.InvariantCulture) + "-" + sequence.ToString(CultureInfo.InvariantCulture)),
                sequence, verb, targetId, actor.RoleRevision, new SimTick(checked(Tick.Microseconds + 120000000)), reads, toolId, recipientId, workPointId);
        }
        public ActorActionIntent GetActiveIntent(StableId actorId)
        { CheckThread(); return active.TryGetValue(actorId, out var action) ? action.Intent : null; }
        public ActionReceipt RequestAction(ActorActionIntent intent)
        {
            CheckThread(); if (intent == null) throw new ArgumentNullException(nameof(intent));
            // Fence discarded generations before consulting any retained receipt.
            if (!RunId.Equals(intent.RunId)) return Rejected(intent, "wrong_run");
            if (intent.Generation != Generation) return Rejected(intent, "stale_generation");
            string key = Key(intent), fingerprint = Fingerprint(intent);
            if (receipts.TryGetValue(key, out var prior)) return prior.Fingerprint == fingerprint ? prior.Receipt : Rejected(intent, "intent_fingerprint_conflict");
            if (!actors.TryGetValue(intent.ActorId, out var actor)) return Rejected(intent, "actor_unknown");
            if (intent.IntentSequence <= actor.ActionSequence) return Rejected(intent, "intent_expired");
            if (active.ContainsKey(intent.ActorId)) return RecordDenied(intent, actor, fingerprint, "actor_busy");
            if (intent.RoleRevision != actor.RoleRevision) return RecordDenied(intent, actor, fingerprint, "role_revision_changed");
            if (Tick.Microseconds >= intent.ExpiresAt.Microseconds) return RecordDenied(intent, actor, fingerprint, "intent_deadline");
            if (!entities.TryGetValue(intent.TargetId, out var target)) return RecordDenied(intent, actor, fingerprint, "target_missing");
            if (!ActionRules.RoleAllows(actor.Role, intent.Verb, target)) return RecordDenied(intent, actor, fingerprint, "role_not_authorized");
            if (!TransitionKernel.ReadSetCurrent(entities, intent.ReadSet) || !ReadCovers(intent.ReadSet, target.Id) ||
                (intent.ToolId.HasValue && !ReadCovers(intent.ReadSet, intent.ToolId.Value)) ||
                (intent.RecipientId.HasValue && !ReadCovers(intent.ReadSet, intent.RecipientId.Value))) return RecordDenied(intent, actor, fingerprint, "stale_or_incomplete_read_set");
            var running = new RunningAction { Intent = intent, Fingerprint = fingerprint };
            running.Reads.AddRange(intent.ReadSet);
            if (ActionRules.NeedsReservation(intent.Verb))
            {
                foreach (var read in intent.ReadSet) running.Resources.Add(read.EntityId);
                running.Resources.Sort((a, b) => StringComparer.Ordinal.Compare(a.Value, b.Value));
                foreach (var resource in running.Resources)
                    if (reservations.TryGetValue(resource, out var holder) && !holder.Equals(intent.ActorId)) return RecordDenied(intent, actor, fingerprint, "resource_reserved");
            }
            var phase = intent.Verb == ActionVerb.Wait ? ActionPhase.Completed : ActionPhase.Reserved;
            running.Receipt = new ActionReceipt(intent.ActorId, intent.IntentId, Generation, checked(Revision + 1), phase,
                phase == ActionPhase.Completed ? "waiting_for_relevant_change" : "reserved_not_completed", false);
            var mutation = Mutation("action.request", intent.ActorId, fingerprint, null,
                new[] { actor.With(actionSequence: intent.IntentSequence) }, intent, running.Receipt);
            Publish(mutation, () =>
            {
                Remember(key, running);
                if (phase != ActionPhase.Completed)
                {
                    active.Add(intent.ActorId, running);
                    foreach (var resource in running.Resources) reservations.Add(resource, intent.ActorId);
                }
            });
            return running.Receipt;
        }
        public ActionReceipt AdvanceAction(StableId actorId, StableId intentId, ActionEvidence evidence)
        {
            CheckThread();
            if (!active.TryGetValue(actorId, out var running) || !running.Intent.IntentId.Equals(intentId)) return Missing(actorId, intentId);
            var intent = running.Intent;
            if (intent.Generation != Generation) return Finish(running, ActionPhase.Cancelled, "stale_generation");
            if (actors[actorId].RoleRevision != intent.RoleRevision) return Finish(running, ActionPhase.Cancelled, "role_revision_changed");
            if (Tick.Microseconds >= intent.ExpiresAt.Microseconds) return Finish(running, ActionPhase.Blocked, "intent_deadline");
            if (!TransitionKernel.ReadSetCurrent(entities, running.Reads)) return Finish(running, ActionPhase.Blocked, "related_state_changed");
            if (running.LastWorkTick == Tick.Microseconds) return running.Receipt;
            if (evidence == null) return Finish(running, ActionPhase.Blocked, "physical_evidence_missing");
            var actualPose = poses.TryGetValue(actorId, out var pose) ? pose : entities[actorId].Position;
            if (actualPose.DistanceSquared(evidence.ActorPosition) > 0.04f) return Finish(running, ActionPhase.Blocked, "motion_owner_evidence_mismatch");
            // Hydrate only pose-dependent bindings, not a full-world copy on every contact sample.
            poseBindings.Clear(); HydratePose(intent.TargetId);
            if (intent.ToolId.HasValue) HydratePose(intent.ToolId.Value);
            if (intent.RecipientId.HasValue) HydratePose(intent.RecipientId.Value);
            var effect = ActionRules.Apply(poseBindings.Count == 0 ? (IReadOnlyDictionary<StableId, WorldEntity>)entities : poseView,
                actors[actorId], intent, evidence);
            if (!effect.Allowed) return Finish(running, ActionPhase.Blocked, effect.Reason);
            if (!effect.Complete && effect.Entities.Count == 0) { running.LastWorkTick = Tick.Microseconds; return running.Receipt; }
            var changedEntities = new List<WorldEntity>(effect.Entities);
            if (effect.Complete && intent.Verb == ActionVerb.MoveTo)
                changedEntities.Add(entities[actorId].With(checked(entities[actorId].Revision + 1), position: evidence.ActorPosition));
            var receipt = new ActionReceipt(actorId, intentId, Generation, checked(Revision + 1),
                effect.Complete ? ActionPhase.Completed : ActionPhase.Executing, effect.Reason, changedEntities.Count > 0);
            var mutation = Mutation("action.effect", actorId, running.Fingerprint, changedEntities, null, intent, receipt);
            Publish(mutation, () =>
            {
                running.Receipt = receipt; running.LastWorkTick = Tick.Microseconds;
                for (int i = 0; i < running.Reads.Count; i++)
                {
                    var read = running.Reads[i];
                    foreach (var entity in changedEntities) if (entity.Id.Equals(read.EntityId)) running.Reads[i] = new EntityReadVersion(entity.Id, entity.Revision);
                }
                if (effect.Complete) Release(running);
            });
            return receipt;
        }
        public ActionReceipt CancelAction(StableId actorId, StableId intentId, string reason)
        {
            CheckThread(); return active.TryGetValue(actorId, out var action) && action.Intent.IntentId.Equals(intentId)
                ? Finish(action, ActionPhase.Cancelled, reason ?? "cancelled") : Missing(actorId, intentId);
        }
        public void ChangeRole(StableId actorId, ActorRole role)
        {
            CheckThread(); if (!Enum.IsDefined(typeof(ActorRole), role) || role == ActorRole.Citizen) throw new ArgumentOutOfRangeException(nameof(role));
            if (!actors.TryGetValue(actorId, out var actor) || !actor.IsHuman) throw new InvalidOperationException("사람 플레이어만 직무를 전환할 수 있습니다.");
            if (actor.Role == role) return;
            var replacement = actor.With(role: role, roleRevision: checked(actor.RoleRevision + 1), knowledgeRevision: checked(actor.KnowledgeRevision + 1), replacePlan: true);
            active.TryGetValue(actorId, out var running);
            var receipt = running == null ? null : new ActionReceipt(actorId, running.Intent.IntentId, Generation, checked(Revision + 1), ActionPhase.Cancelled, "role_revision_changed", false);
            Publish(Mutation("actor.role", actorId, role.ToString(), null, new[] { replacement }, running?.Intent, receipt), () =>
            { if (running != null) { running.Receipt = receipt; Release(running); } });
        }
        public void UpdateActor(ActorState actor, string reason)
        {
            CheckThread(); if (actor == null || !actors.TryGetValue(actor.Id, out var prior)) throw new ArgumentException("등록된 인물이 필요합니다.");
            if (actor.Role != prior.Role || actor.RoleRevision != prior.RoleRevision || actor.ActionSequence != prior.ActionSequence || actor.IsHuman != prior.IsHuman ||
                actor.KnowledgeRevision < prior.KnowledgeRevision || actor.DecisionSequence < prior.DecisionSequence) throw new InvalidOperationException("인물 권한/행동 이력은 전용 실행기만 변경합니다.");
            Publish(Mutation("actor.knowledge-plan", actor.Id, reason, null, new[] { actor }));
        }
        public bool CommitEnvironment(TransitionKernel kernel, WorldSnapshot basis, GroundedCandidate candidate,
            string transitionIntentId, string requestSha256, out string reason)
        {
            CheckThread();
            if (basis == null || !basis.RunId.Equals(RunId) || basis.Generation != Generation) { reason = "stale_generation"; return false; }
            if (basis.RulesetId != RulesetId || basis.RulesetRevision != RulesetRevision) { reason = "ruleset_changed"; return false; }
            if (basis.Tick.Microseconds > Tick.Microseconds || Tick.Microseconds - basis.Tick.Microseconds > 5000000) { reason = "environment_deadline"; return false; }
            if (string.IsNullOrEmpty(transitionIntentId) || string.IsNullOrEmpty(requestSha256)) { reason = "inference_record_required"; return false; }
            string key = Generation.ToString(CultureInfo.InvariantCulture) + ":" + transitionIntentId;
            if (environmentReceipts.Contains(key)) { reason = "duplicate_environment_intent"; return false; }
            // Stop admission rather than forgetting old environment identities and allowing reapplication.
            if (environmentReceipts.Count >= 8192) { reason = "environment_identity_capacity"; return false; }
            if (!kernel.TryApply(entities, candidate, out var changed, out reason)) return false;
            var mutation = Mutation("environment.transition", null, transitionIntentId + "|" + requestSha256 + "|" + candidate.TransitionId + "|" + candidate.SourceId.Value + "|" + candidate.TargetId.Value,
                changed == null ? null : new[] { changed }, null);
            Publish(mutation, () => environmentReceipts.Add(key));
            return true;
        }
        public void Restore(WorldSnapshot checkpoint)
        {
            CheckThread();
            if (Mode != GameplayMode.Tutorial || checkpoint == null || checkpoint.Mode != GameplayMode.Tutorial || !checkpoint.RunId.Equals(RunId))
                throw new InvalidOperationException("튜토리얼 전용 run의 checkpoint만 복원할 수 있습니다.");
            if (checkpoint.RulesetId != RulesetId || checkpoint.RulesetRevision != RulesetRevision) throw new InvalidOperationException("다른 규칙 판본의 checkpoint입니다.");
            long generation = checked(Generation + 1);
            var mutation = new WorldMutation(RunId, generation, Revision, checkpoint.Tick, "tutorial.restore", null,
                checkpoint.SnapshotId, checkpoint.Entities.Values, checkpoint.Actors.Values);
            writer.ExecuteWorld(checkpoint.Tick, () =>
            {
                sink.Commit(mutation);
                entities.Clear(); foreach (var entity in checkpoint.Entities) entities.Add(entity.Key, entity.Value);
                actors.Clear(); foreach (var actor in checkpoint.Actors) actors.Add(actor.Key, actor.Value);
                poses.Clear(); active.Clear(); reservations.Clear(); receipts.Clear(); receiptOrder.Clear(); environmentReceipts.Clear();
                Generation = generation; Revision = mutation.Revision; Tick = checkpoint.Tick;
            }, restoreClock: true);
            Notify(mutation);
        }
        public void AdvanceGeneration(string reason)
        {
            CheckThread(); FlushPoses();
            var mutation = new WorldMutation(RunId, checked(Generation + 1), Revision, Tick,
                "world.generation-fence", null, reason ?? "new-inference-lifetime", null, null);
            Publish(mutation, () =>
            {
                active.Clear(); reservations.Clear(); receipts.Clear(); receiptOrder.Clear(); environmentReceipts.Clear();
            });
        }
        public void ReplayCommitted(WorldMutation mutation)
        {
            CheckThread();
            if (!mutation.RunId.Equals(RunId) || mutation.PreviousRevision != Revision || mutation.Generation < Generation) throw new InvalidOperationException("기록의 run/순서/generation 불일치입니다.");
            bool restore = mutation.Kind == "tutorial.restore";
            bool fence = mutation.Kind == "world.generation-fence";
            if (mutation.Generation != Generation && !restore && !fence) throw new InvalidOperationException("명시적인 세대 fence 기록이 필요합니다.");
            if (mutation.Kind == "environment.transition" && (mutation.Detail == null || mutation.Detail.IndexOf('|') <= 0))
                throw new InvalidOperationException("환경 전이 identity 기록이 없습니다.");
            writer.ExecuteWorld(mutation.Tick, () =>
            {
                if (restore) { entities.Clear(); actors.Clear(); active.Clear(); reservations.Clear(); receipts.Clear(); receiptOrder.Clear(); environmentReceipts.Clear(); poses.Clear(); }
                if (fence) { active.Clear(); reservations.Clear(); receipts.Clear(); receiptOrder.Clear(); environmentReceipts.Clear(); }
                foreach (var entity in mutation.Entities) entities[entity.Id] = entity;
                foreach (var actor in mutation.Actors) actors[actor.Id] = actor;
                Generation = mutation.Generation; Revision = mutation.Revision; Tick = mutation.Tick;
                if (mutation.Intent != null && mutation.Receipt != null)
                {
                    var intent = mutation.Intent; string key = Key(intent);
                    if (!receipts.TryGetValue(key, out var action))
                    {
                        action = new RunningAction { Intent = intent, Fingerprint = Fingerprint(intent) };
                        if (mutation.Kind != "action.denied")
                        {
                            foreach (var read in intent.ReadSet) action.Reads.Add(new EntityReadVersion(read.EntityId, entities[read.EntityId].Revision));
                            if (ActionRules.NeedsReservation(intent.Verb)) foreach (var read in intent.ReadSet) action.Resources.Add(read.EntityId);
                        }
                        Remember(key, action);
                    }
                    action.Receipt = mutation.Receipt;
                    for (int i = 0; i < action.Reads.Count; i++) action.Reads[i] = new EntityReadVersion(action.Reads[i].EntityId, entities[action.Reads[i].EntityId].Revision);
                    if (mutation.Receipt.Phase == ActionPhase.Reserved || mutation.Receipt.Phase == ActionPhase.Executing)
                    { active[intent.ActorId] = action; foreach (var resource in action.Resources) reservations[resource] = intent.ActorId; }
                    else Release(action);
                }
                if (mutation.Kind == "environment.transition")
                { int split = mutation.Detail.IndexOf('|'); if (split <= 0) throw new InvalidOperationException("환경 전이 identity 기록이 없습니다."); environmentReceipts.Add(Generation.ToString(CultureInfo.InvariantCulture) + ":" + mutation.Detail.Substring(0, split)); }
            }, restore);
        }
        public void Dispose() { CheckThread(); writer.Dispose(); disposed = true; }
        private void AddRead(List<EntityReadVersion> reads, StableId id)
        {
            if (!entities.TryGetValue(id, out var entity)) throw new ArgumentException("참조 entity가 없습니다.");
            if (!ReadCovers(reads, id)) reads.Add(new EntityReadVersion(id, entity.Revision));
        }
        private static bool ReadCovers(IReadOnlyList<EntityReadVersion> reads, StableId id)
        { foreach (var read in reads) if (read.EntityId.Equals(id)) return true; return false; }
        private ActionReceipt RecordDenied(ActorActionIntent intent, ActorState actor, string fingerprint, string reason)
        {
            var action = new RunningAction { Intent = intent, Fingerprint = fingerprint,
                Receipt = new ActionReceipt(intent.ActorId, intent.IntentId, Generation, checked(Revision + 1), ActionPhase.Blocked, reason, false) };
            Publish(Mutation("action.denied", intent.ActorId, fingerprint, null,
                new[] { actor.With(actionSequence: intent.IntentSequence) }, intent, action.Receipt), () => Remember(Key(intent), action));
            return action.Receipt;
        }
        private ActionReceipt Finish(RunningAction action, ActionPhase phase, string reason)
        {
            var receipt = new ActionReceipt(action.Intent.ActorId, action.Intent.IntentId, Generation, checked(Revision + 1), phase, reason, false);
            Publish(Mutation("action.end", action.Intent.ActorId, action.Fingerprint, null, null, action.Intent, receipt), () => { action.Receipt = receipt; Release(action); });
            return receipt;
        }
        private ActionReceipt Rejected(ActorActionIntent intent, string reason) => new ActionReceipt(intent.ActorId, intent.IntentId, Generation, Revision, ActionPhase.Blocked, reason, false);
        private ActionReceipt Missing(StableId actor, StableId intent) => new ActionReceipt(actor, intent, Generation, Revision, ActionPhase.Blocked, "active_action_missing", false);
        private WorldMutation Mutation(string kind, StableId? actor, string detail, IEnumerable<WorldEntity> changed, IEnumerable<ActorState> changedActors,
            ActorActionIntent intent = null, ActionReceipt receipt = null) => new WorldMutation(RunId, Generation, Revision, Tick, kind, actor, detail, changed, changedActors, intent, receipt);
        private void Publish(WorldMutation mutation, Action after = null)
        {
            writer.ExecuteWorld(Tick, () =>
            {
                sink.Commit(mutation);
                foreach (var entity in mutation.Entities) entities[entity.Id] = entity;
                foreach (var actor in mutation.Actors) actors[actor.Id] = actor;
                Generation = mutation.Generation; Revision = mutation.Revision;
                after?.Invoke();
            });
            Notify(mutation);
        }
        private void Notify(WorldMutation mutation)
        {
            var listeners = Committed;
            if (listeners == null) return;
            foreach (Action<WorldMutation> listener in listeners.GetInvocationList())
            {
                try { listener(mutation); }
                catch (Exception error) { ObserverFailed?.Invoke(error); }
            }
        }
        private void Release(RunningAction action)
        {
            if (active.TryGetValue(action.Intent.ActorId, out var running) && ReferenceEquals(running, action)) active.Remove(action.Intent.ActorId);
            foreach (var resource in action.Resources)
                if (reservations.TryGetValue(resource, out var holder) && holder.Equals(action.Intent.ActorId)) reservations.Remove(resource);
        }
        private void Remember(string key, RunningAction action)
        {
            receipts.Add(key, action); receiptOrder.Enqueue(key);
            int budget = receiptOrder.Count;
            while (receipts.Count > 4096 && budget-- > 0)
            {
                var old = receiptOrder.Dequeue(); var prior = receipts[old];
                if (active.TryGetValue(prior.Intent.ActorId, out var running) && ReferenceEquals(running, prior)) { receiptOrder.Enqueue(old); continue; }
                receipts.Remove(old);
            }
        }
        private void CheckThread()
        {
            if (disposed) throw new ObjectDisposedException(nameof(WorldSession));
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("모든 세계 상태 접근은 지정된 writer thread에서 수행해야 합니다.");
        }
        private static string Key(ActorActionIntent intent) => intent.Generation.ToString(CultureInfo.InvariantCulture) + "|" + intent.ActorId.Value + "|" + intent.IntentId.Value;
        private static string Fingerprint(ActorActionIntent intent)
        {
            var text = new StringBuilder();
            text.Append(intent.RunId.Value).Append('|').Append(intent.Generation).Append('|').Append(intent.ActorId.Value).Append('|').Append(intent.IntentId.Value)
                .Append('|').Append(intent.IntentSequence).Append('|').Append((int)intent.Verb).Append('|').Append(intent.TargetId.Value)
                .Append('|').Append(intent.ToolId?.Value).Append('|').Append(intent.RecipientId?.Value).Append('|').Append(intent.RoleRevision)
                .Append('|').Append(intent.ExpiresAt.Microseconds).Append('|').Append(intent.WorkPointId);
            foreach (var read in intent.ReadSet) text.Append('|').Append(read.EntityId.Value).Append(':').Append(read.Revision);
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private sealed class PoseView : IReadOnlyDictionary<StableId, WorldEntity>
        {
            private readonly IReadOnlyDictionary<StableId, WorldEntity> source, poses;
            public PoseView(IReadOnlyDictionary<StableId, WorldEntity> source, IReadOnlyDictionary<StableId, WorldEntity> poses) { this.source = source; this.poses = poses; }
            public WorldEntity this[StableId key] => poses.TryGetValue(key, out var value) ? value : source[key];
            public IEnumerable<StableId> Keys => source.Keys;
            public IEnumerable<WorldEntity> Values { get { foreach (var key in source.Keys) yield return this[key]; } }
            public int Count => source.Count;
            public bool ContainsKey(StableId key) => source.ContainsKey(key);
            public bool TryGetValue(StableId key, out WorldEntity value) => poses.TryGetValue(key, out value) || source.TryGetValue(key, out value);
            public IEnumerator<KeyValuePair<StableId, WorldEntity>> GetEnumerator() { foreach (var key in source.Keys) yield return new KeyValuePair<StableId, WorldEntity>(key, this[key]); }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
