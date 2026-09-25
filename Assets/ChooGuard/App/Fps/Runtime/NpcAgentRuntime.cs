using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    public sealed class NpcSpeech
    {
        public StableId ActorId { get; }
        public string Text { get; }
        public string Source { get; }
        public string GoalStatus { get; }
        public NpcSpeech(StableId actor, string text, string source, string goalStatus)
        { ActorId = actor; Text = text; Source = source; GoalStatus = goalStatus; }
    }

    /// <summary>Logical agents outlive visual leases. Host explicitly ticks; navigation alone owns actor poses.</summary>
    public sealed class NpcAgentRuntime : MonoBehaviour
    {
        private sealed class Agent
        {
            internal StableId Id;
            internal NpcPlanner Planner;
            internal ActorNavigationBinding Motion;
            internal Task<InferenceSelection> Flight;
            internal CancellationTokenSource Cancellation;
            internal Task<DialogueSelection> Dialogue;
            internal CancellationTokenSource DialogueCancellation;
            internal long DialogueSequence, DialogueKnowledge;
            internal ActorState DecisionBasis;
            internal List<GroundedCandidate> Candidates;
            internal ActorActionIntent Action;
            internal FpsEntityBinding Target, Tool, Held;
            internal FpsEntityBinding.WorkPoint Point;
            internal long Generation, DecisionExpiresTick, LastWorkTick = -1;
            internal float NextSense, ActionElapsed, ContactElapsed, LastProgress, StrokePhase;
            internal Vector3 PreviousHead;
            internal bool PreviousContact, Reached;
            internal string Status = "관측 준비", LastSpeech;
        }
        [Min(1)] public int SenseAgentsPerTick = 4;
        [Min(.1f)] public float SenseInterval = .5f;
        [Min(1)] public float SightDistance = 10;
        [Range(30, 180)] public float FieldOfViewDegrees = 140;
        [Min(1)] public float HearingDistance = 5;
        [Min(.1f)] public float NpcHandReach = .8f;
        [Min(.01f)] public float DecisionSpacing = .1f;
        [Min(1)] public float MaximumGripForce = 100;
        [Min(1)] public float MaximumGripTorque = 10;
        private WorldSession session;
        private GameplayInferenceClient inference;
        private GameplayNavigation navigation;
        private readonly Dictionary<StableId, Agent> registry = new Dictionary<StableId, Agent>();
        private readonly List<Agent> agents = new List<Agent>();
        private readonly Dictionary<string, FpsEntityBinding> physical = new Dictionary<string, FpsEntityBinding>(StringComparer.Ordinal);
        private readonly Dictionary<Vector3Int, List<StableId>> cells = new Dictionary<Vector3Int, List<StableId>>();
        private readonly List<List<StableId>> cellLists = new List<List<StableId>>();
        private readonly List<StableId> entityIds = new List<StableId>();
        private readonly HashSet<StableId> entitySet = new HashSet<StableId>();
        private readonly Dictionary<StableId, Transform> humanObservers = new Dictionary<StableId, Transform>();
        private readonly Queue<WorldMutation> socialEvents = new Queue<WorldMutation>();
        private readonly Queue<WorldMutation> resourceEvents = new Queue<WorldMutation>();
        private readonly FpsPhysicalQueries queries = new FpsPhysicalQueries();
        private readonly RaycastHit[] hearingHits = new RaycastHit[32];
        private long generation = -1;
        private float elapsed, nextSpatial, nextDecision;
        private int senseCursor, decisionCursor;
        private bool initialized;
        public event Action<NpcSpeech> Speech;
        public int RegisteredActorCount => registry.Count;

        public void Initialize(WorldSession world, GameplayInferenceClient client, GameplayNavigation routes, IEnumerable<ActorNavigationBinding> bindings)
        {
            Shutdown();
            session = world ?? throw new ArgumentNullException(nameof(world)); inference = client ?? throw new ArgumentNullException(nameof(client));
            navigation = routes;
            generation = session.Generation;
            var basis = session.Snapshot();
            foreach (var entity in basis.Entities.Values) { entityIds.Add(entity.Id); entitySet.Add(entity.Id); }
            foreach (var actor in basis.Actors.Values) if (!actor.IsHuman) AddAgent(actor);
            if (bindings != null) foreach (var binding in bindings) RegisterBinding(binding);
            session.Committed += OnCommitted;
            initialized = true; RebuildSpatial();
        }

        public void RegisterBinding(ActorNavigationBinding binding)
        {
            if (binding == null || string.IsNullOrEmpty(binding.ActorId)) return;
            var id = new StableId(binding.ActorId);
            if (!registry.TryGetValue(id, out var agent)) return;
            if (agent.Motion != null && agent.Motion != binding && agent.Motion.isActiveAndEnabled)
                throw new InvalidOperationException("Two NPC movement owners for one identity.");
            agent.Motion = binding;
            binding.Navigation = navigation;
        }

        public string GetStatus(StableId actorId) => registry.TryGetValue(actorId, out var agent) ? agent.Status : "등록되지 않은 NPC";

        public void RegisterHumanObserver(StableId actorId, Transform actorRoot)
        {
            if (actorRoot == null || !session.TryGetActor(actorId, out var actor) || !actor.IsHuman)
                throw new ArgumentException("A registered human observer and physical root are required.");
            humanObservers[actorId] = actorRoot;
        }

        public bool RequestConversation(StableId actorId, string message)
        {
            if (!initialized || !registry.TryGetValue(actorId, out var agent) || agent.Dialogue != null ||
                !session.TryGetActor(actorId, out var actor)) return false;
            if (!inference.HasDialogueProvider)
            {
                Say(agent, "자유 대화 제공자가 구성되지 않았습니다. 실제 관측과 작업 상태 안내만 제공할 수 있습니다.");
                return false;
            }
            agent.DialogueCancellation?.Dispose(); agent.DialogueCancellation = new CancellationTokenSource();
            agent.DialogueKnowledge = actor.KnowledgeRevision;
            agent.Dialogue = inference.GenerateDialogueAsync(session.Snapshot(), actor, message,
                checked(++agent.DialogueSequence), agent.DialogueCancellation.Token);
            return true;
        }

        private void PumpDialogue(Agent agent)
        {
            if (agent.Dialogue == null || !agent.Dialogue.IsCompleted) return;
            var task = agent.Dialogue; agent.Dialogue = null;
            if (task.IsFaulted) { _ = task.Exception; Say(agent, "자유 대화 응답을 적용할 수 없습니다."); return; }
            if (task.IsCanceled || agent.DialogueCancellation.IsCancellationRequested || !session.TryGetActor(agent.Id, out var actor) ||
                actor.KnowledgeRevision != agent.DialogueKnowledge) return;
            var result = task.GetAwaiter().GetResult();
            if (result.Status == "ok") Say(agent, result.Text, "generative");
            else Say(agent, "자유 대화 불가 · " + result.Reason);
            // Deliberately no action, memory or fact mutation from free text.
        }

        private void AddAgent(ActorState actor)
        {
            if (registry.ContainsKey(actor.Id)) return;
            var agent = new Agent { Id = actor.Id, Planner = new NpcPlanner(actor.Id), Generation = generation };
            registry.Add(actor.Id, agent); agents.Add(agent);
            // A schedule tells its owner where they intend to go, not hidden target facts.
            if (actor.Plan != null && session.TryGetEntity(actor.Plan.TargetId, out var assigned))
            {
                var location = new WorldEntity(assigned.Id, assigned.Label, assigned.Kind, assigned.Revision, assigned.Position,
                    assigned.ZoneId, definitionId: assigned.DefinitionId);
                var updated = agent.Planner.Observe(actor, location, session.Tick, ObservationSource.Assignment);
                if (!ReferenceEquals(actor, updated)) session.UpdateActor(updated, "owner_schedule_location");
            }
        }

        public void TickAgents(float deltaSeconds)
        {
            if (!initialized || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0) return;
            float dt = Mathf.Min(deltaSeconds, .1f); elapsed += dt;
            if (session.Generation != generation) ResetGeneration();
            if (elapsed >= nextSpatial) { RebuildSpatial(); nextSpatial = elapsed + .5f; }
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent.Motion != null && agent.Motion.isActiveAndEnabled)
                    session.SetActorPose(agent.Id, Point(agent.Motion.transform.position));
                Pump(agent);
                PumpDialogue(agent);
                if (agent.Action != null) Advance(agent, dt);
                else FollowAgreedEscort(agent);
                Carry(agent);
            }
            while (socialEvents.Count > 0)
            {
                var mutation = socialEvents.Dequeue();
                if (mutation.Generation == generation && session.TryGetEntity(mutation.Intent.TargetId, out var target))
                    DeliverSocial(mutation.Intent.ActorId, mutation.Intent, target);
            }
            while (resourceEvents.Count > 0) ObserveReleasedWork(resourceEvents.Dequeue());
            int sensed = 0, scanned = 0;
            while (agents.Count > 0 && sensed < Mathf.Max(1, SenseAgentsPerTick) && scanned++ < agents.Count)
            {
                var agent = agents[senseCursor++ % agents.Count];
                if (agent.NextSense > elapsed) continue;
                agent.NextSense = elapsed + SenseInterval; Sense(agent); sensed++;
            }
            if (elapsed < nextDecision || agents.Count == 0) return;
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[decisionCursor++ % agents.Count];
                if (TryDecide(agent)) { nextDecision = elapsed + DecisionSpacing; break; }
            }
        }

        private void ResetGeneration()
        {
            foreach (var agent in agents)
            {
                agent.Cancellation?.Cancel(); agent.Cancellation?.Dispose();
                agent.DialogueCancellation?.Cancel(); agent.DialogueCancellation?.Dispose();
                agent.Motion?.ClearDestination(); agent.Target?.StopConstraint();
            }
            socialEvents.Clear(); resourceEvents.Clear();
            var bindings = new List<ActorNavigationBinding>();
            foreach (var agent in agents) if (agent.Motion != null) bindings.Add(agent.Motion);
            registry.Clear(); agents.Clear(); entityIds.Clear(); entitySet.Clear();
            generation = session.Generation;
            var snapshot = session.Snapshot();
            foreach (var entity in snapshot.Entities.Values) { entityIds.Add(entity.Id); entitySet.Add(entity.Id); }
            foreach (var actor in snapshot.Actors.Values) if (!actor.IsHuman) AddAgent(actor);
            foreach (var binding in bindings) RegisterBinding(binding);
            senseCursor = decisionCursor = 0; nextDecision = elapsed; RebuildSpatial();
        }

        private void OnCommitted(WorldMutation mutation)
        {
            // Reconcile grips from committed custody only; private observation commits happen outside this callback.
            foreach (var entity in mutation.Entities)
            {
                if (entitySet.Add(entity.Id)) entityIds.Add(entity.Id);
                if (mutation.Generation == generation) ReconcileCustody(entity);
            }
            if (mutation.Intent != null && mutation.Receipt != null && mutation.Receipt.Phase == ActionPhase.Completed &&
                (mutation.Intent.Verb == ActionVerb.RequestHelp || mutation.Intent.Verb == ActionVerb.Consent ||
                 mutation.Intent.Verb == ActionVerb.Report || mutation.Intent.Verb == ActionVerb.Handoff))
                socialEvents.Enqueue(mutation);
            if (mutation.Intent != null && mutation.Receipt != null && ActionRules.NeedsReservation(mutation.Intent.Verb) &&
                (mutation.Receipt.Phase == ActionPhase.Completed || mutation.Receipt.Phase == ActionPhase.Cancelled || mutation.Receipt.Phase == ActionPhase.Blocked))
                resourceEvents.Enqueue(mutation);
        }

        private void ReconcileCustody(WorldEntity item)
        {
            if (item.Kind == EntityKind.Actor || !physical.TryGetValue(item.Id.Value, out var binding)) return;
            Agent receiver = null;
            if (item.CustodianId.HasValue) registry.TryGetValue(item.CustodianId.Value, out receiver);
            if (receiver != null && receiver.Held == binding) return;
            foreach (var agent in agents)
                if (agent.Held == binding && agent != receiver) agent.Held = null;
            if (receiver != null && binding.CanDriveBody(out _) && receiver.Held == null) receiver.Held = binding;
        }

        private void ObserveReleasedWork(WorldMutation mutation)
        {
            if (mutation.Generation != generation || !session.TryGetEntity(mutation.Intent.TargetId, out var target) ||
                !session.TryGetEntity(mutation.Intent.ActorId, out var performer)) return;
            foreach (var observer in agents)
            {
                if (observer.Id.Equals(performer.Id) || observer.Motion == null || !session.TryGetActor(observer.Id, out var state) ||
                    state.Plan == null || state.Plan.BlockedReason == null || !observer.Planner.GetKnownEntities(state).ContainsKey(target.Id) ||
                    !Visible(observer, performer, out _) || !Visible(observer, target, out _)) continue;
                if ((observer.Motion.transform.position - Vector(target.Position)).sqrMagnitude > SightDistance * SightDistance) continue;
                var observed = observer.Planner.ObserveResourceActivity(state, target, session.Tick);
                if (!ReferenceEquals(state, observed)) session.UpdateActor(observed, "local_resource_work_stopped_not_availability_assertion");
            }
        }

        private void RebuildSpatial()
        {
            physical.Clear();
            foreach (var binding in FpsEntityBinding.Live)
                if (binding != null && !string.IsNullOrEmpty(binding.EntityId)) physical[binding.EntityId] = binding;
            foreach (var agent in agents)
                if (agent.Held != null && (!physical.TryGetValue(agent.Held.EntityId, out var live) || live != agent.Held ||
                    !session.TryGetEntity(new StableId(agent.Held.EntityId), out var held) || held.Kind == EntityKind.Actor ||
                    !held.CustodianId.HasValue || !held.CustodianId.Value.Equals(agent.Id))) agent.Held = null;
            foreach (var binding in physical.Values)
                if (session.TryGetEntity(new StableId(binding.EntityId), out var item)) ReconcileCustody(item);
            foreach (var list in cellLists) list.Clear();
            cells.Clear(); int used = 0;
            foreach (var id in entityIds)
            {
                if (!session.TryGetEntity(id, out var entity)) continue;
                var cell = Cell(Vector(entity.Position));
                if (!cells.TryGetValue(cell, out var list))
                {
                    if (used == cellLists.Count) cellLists.Add(new List<StableId>(16));
                    list = cellLists[used++]; cells.Add(cell, list);
                }
                list.Add(id);
            }
        }

        private void Sense(Agent agent)
        {
            if (agent.Motion == null || !agent.Motion.isActiveAndEnabled || !session.TryGetActor(agent.Id, out var actor)) return;
            var original = actor;
            if (session.TryGetEntity(agent.Id, out var self)) actor = agent.Planner.Observe(actor, self, session.Tick, ObservationSource.Sight);
            Vector3 position = agent.Motion.transform.position;
            var centre = Cell(position); int radius = Mathf.CeilToInt(SightDistance / 8f);
            for (int x = -radius; x <= radius; x++) for (int y = -1; y <= 1; y++) for (int z = -radius; z <= radius; z++)
            {
                if (!cells.TryGetValue(centre + new Vector3Int(x, y, z), out var ids)) continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i].Equals(agent.Id) || !session.TryGetEntity(ids[i], out var entity)) continue;
                    if (!TrySight(agent, entity, out var observedPosition)) continue;
                    actor = ObserveSight(agent, actor, entity, observedPosition);
                }
            }
            actor = agent.Planner.FormGoal(actor);
            if (!ReferenceEquals(original, actor)) session.UpdateActor(actor, "local_los_and_goal");
            if (actor.Plan != null) agent.Status = actor.Plan.BlockedReason == null ? actor.Plan.Reason : "목표 유지 · " + actor.Plan.BlockedReason;
        }

        private bool TryDecide(Agent agent)
        {
            if (agent.Flight != null || agent.Action != null || agent.Motion == null || !agent.Motion.isActiveAndEnabled ||
                !session.TryGetActor(agent.Id, out var actor) || actor.Plan == null || !agent.Planner.CanReplan(actor, RouteRevision)) return false;
            actor = agent.Planner.Resume(actor);
            var candidates = agent.Planner.BuildCandidates(actor);
            if (candidates.Count < 2) { Block(agent, actor, "알고 있는 대상 또는 실행 후보가 부족합니다"); return false; }
            actor = actor.With(decisionSequence: checked(actor.DecisionSequence + 1));
            session.UpdateActor(actor, "autonomous_decision_requested");
            agent.DecisionBasis = actor; agent.Candidates = candidates;
            agent.DecisionExpiresTick = checked(session.Tick.Microseconds + 5000000);
            agent.Cancellation?.Dispose(); agent.Cancellation = new CancellationTokenSource();
            agent.Generation = generation; agent.Status = "자기 목표의 다음 행동을 판단 중";
            agent.Flight = inference.SelectNpcAsync(session.Snapshot(), actor, candidates, session.GetActiveIntent(agent.Id), agent.Cancellation.Token);
            return true;
        }

        private void Pump(Agent agent)
        {
            if (agent.Flight == null || !agent.Flight.IsCompleted) return;
            var flight = agent.Flight; agent.Flight = null;
            if (!session.TryGetActor(agent.Id, out var actor) || agent.Generation != generation) return;
            if (flight.IsCanceled || flight.IsFaulted)
            {
                if (flight.IsFaulted) _ = flight.Exception;
                Block(agent, actor, "판단 서비스 응답을 적용할 수 없습니다"); return;
            }
            var result = flight.GetAwaiter().GetResult();
            if (result.Status != "ok") { Block(agent, actor, "JEV 판단 불가 · " + result.Reason); return; }
            if (actor.DecisionSequence != agent.DecisionBasis.DecisionSequence || actor.KnowledgeRevision != agent.DecisionBasis.KnowledgeRevision ||
                actor.RoleRevision != agent.DecisionBasis.RoleRevision || agent.Cancellation.IsCancellationRequested || session.Tick.Microseconds >= agent.DecisionExpiresTick)
            { agent.Status = "새 관측으로 이전 판단을 폐기했습니다"; return; }
            GroundedCandidate selected = null;
            foreach (var candidate in agent.Candidates) if (candidate.CandidateId == result.SelectedCandidateId) { selected = candidate; break; }
            if (selected == null) { Block(agent, actor, "응답 후보가 현재 후보 집합에 없습니다"); return; }
            foreach (var read in selected.ReadSet)
                if (!session.TryGetEntity(read.EntityId, out var entity) || entity.Revision != read.Revision)
                { agent.Status = "관련 대상이 바뀌어 다시 관측합니다"; return; }
            Begin(agent, actor, selected);
        }

        private void Begin(Agent agent, ActorState actor, GroundedCandidate candidate)
        {
            if (!candidate.Verb.HasValue || !session.TryGetEntity(candidate.TargetId, out var target)) return;
            physical.TryGetValue(target.Id.Value, out agent.Target);
            agent.Tool = candidate.ToolId.HasValue && physical.TryGetValue(candidate.ToolId.Value.Value, out var tool) ? tool : null;
            agent.Point = agent.Target == null ? null : agent.Target.FindWorkPoint(agent.Target.WorkPointId);
            if (agent.Point == null && agent.Target != null && agent.Target.WorkPoints.Length > 0) agent.Point = agent.Target.WorkPoints[0];
            if (candidate.Verb == ActionVerb.Clean && agent.Target != null)
                foreach (var point in agent.Target.WorkPoints)
                    if (point != null && target.Measurement("soil:" + point.Id).HasValue && target.Measurement("soil:" + point.Id).Value.Value > 0)
                    { agent.Point = point; break; }
            var intent = session.CreateIntent(agent.Id, candidate.Verb.Value, target.Id, candidate.ToolId, candidate.RecipientId, agent.Point?.Id);
            var receipt = session.RequestAction(intent);
            // RequestAction advanced ActionSequence; never write the older actor snapshot back.
            session.TryGetActor(agent.Id, out actor);
            if (receipt.Phase == ActionPhase.Completed)
            { Block(agent, actor, "선택한 대기 상태 · 관련 변화가 생기면 재계획합니다"); return; }
            if (Terminal(receipt.Phase)) { Block(agent, actor, "실행 보류 · " + receipt.Reason); return; }
            agent.Action = intent; agent.ActionElapsed = agent.LastProgress = agent.StrokePhase = 0;
            agent.LastWorkTick = -1; agent.PreviousContact = false; agent.Reached = false; agent.ContactElapsed = 0;
            agent.PreviousHead = ToolHead(agent);
            agent.Status = "선택한 행동 접근 중 · " + candidate.Verb;
            if (!SetApproach(agent, target)) return;
            Say(agent, "제가 " + target.Label + " 쪽으로 가서 " + VerbText(candidate.Verb.Value) + " 보겠습니다.");
        }

        private bool SetApproach(Agent agent, WorldEntity target)
        {
            if (agent.Motion == null) return false;
            var position = agent.Motion.transform.position;
            var targetPosition = Vector(target.Position);
            bool handedToActor = agent.Action.Verb == ActionVerb.Handoff && agent.Action.RecipientId.HasValue &&
                session.TryGetEntity(agent.Action.RecipientId.Value, out _);
            Vector3 approach;
            if (!handedToActor && target.Kind != EntityKind.Actor && target.Kind != EntityKind.Exit && target.Kind != EntityKind.Zone)
            {
                if (agent.Target == null || agent.Target.ApproachPoint == null)
                { StopBlocked(agent, "저작된 지상 접근 위치가 없습니다"); return false; }
                approach = agent.Target.ApproachPoint.position;
            }
            else
            {
                if (handedToActor && session.TryGetEntity(agent.Action.RecipientId.Value, out var recipient)) targetPosition = Vector(recipient.Position);
                Vector3 direction = targetPosition - position; direction.y = 0;
                float range = agent.Action.Verb == ActionVerb.MoveTo ? .7f : Mathf.Min(.65f, NpcHandReach * .8f);
                approach = targetPosition - direction.normalized * range;
            }
            if ((approach - position).sqrMagnitude <= .01f) { agent.Motion.ClearDestination(); return true; }
            if (!agent.Motion.SetDestination(approach, out var reason)) { StopBlocked(agent, "경로 차단 · " + reason); return false; }
            return true;
        }

        private void Advance(Agent agent, float dt)
        {
            var action = agent.Action;
            if (agent.Motion == null || !agent.Motion.isActiveAndEnabled) { StopBlocked(agent, "물리 이동 연결 대기 · 책임 유지"); return; }
            if (!session.TryGetActor(agent.Id, out var actor) || actor.RoleRevision != action.RoleRevision) { StopBlocked(agent, "역할 변경으로 행동 중단"); return; }
            if (!session.TryGetEntity(action.TargetId, out var target)) { StopBlocked(agent, "대상 연결 상실"); return; }
            if (agent.Motion.IsBlocked) { StopBlocked(agent, "경로 차단 · " + agent.Motion.BlockedReason); return; }
            if (agent.Motion.HasDestination && !agent.Motion.HasArrived) return;
            if (!agent.Reached) { agent.Reached = true; agent.ActionElapsed = agent.LastProgress = agent.ContactElapsed = 0; }
            agent.ActionElapsed += dt;
            if (session.Tick.Microseconds == agent.LastWorkTick) return;
            var position = agent.Motion.transform.position;
            bool visible = Visible(agent, target, out var contact);
            if (!visible) { StopBlocked(agent, "직접 시야를 확보할 수 없습니다"); return; }
            if (agent.Target != null && agent.Target.ContactSurface != null)
            {
                contact = agent.Point != null && agent.Point.Surface != null
                    ? agent.Point.Surface.ClosestPoint(Hand(agent)) : agent.Target.ContactSurface.ClosestPoint(Hand(agent));
                visible = queries.Visible(Eye(agent), contact, agent.Motion.transform, agent.Target, agent.Tool);
                if (!visible) { StopBlocked(agent, "작업 접촉점이 가려져 있습니다"); return; }
            }
            bool touching = Contact(agent, target, contact);
            agent.ContactElapsed = touching ? agent.ContactElapsed + dt : 0;
            bool supported = false, aligned = false;
            double work = 0;
            if (action.Verb == ActionVerb.MoveTo)
            { work = 0; aligned = false; }
            else if (Conversational(action.Verb))
            {
                if (action.RecipientId.HasValue && !Audible(agent.Id, action.RecipientId.Value)) { StopBlocked(agent, "상대가 실제 전달 범위에 없습니다"); return; }
                if (agent.ActionElapsed < .5f) return;
                work = 1;
            }
            else if (action.Verb == ActionVerb.Clean)
            {
                if (!PrepareTool(agent, actor, out var reason)) { StopBlocked(agent, reason); return; }
                var head = ToolHead(agent); var point = agent.Point;
                contact = point.Surface.ClosestPoint(head);
                touching = touching && Vector3.Distance(head, contact) <= point.ContactRadius + agent.Tool.ToolHeadRadius &&
                    queries.ClearPath(Hand(agent), head, .015f, agent.Motion.transform, agent.Target, agent.Tool);
                aligned = point.Frame != null && Vector3.Angle(agent.Tool.ToolHead.forward, point.Frame.TransformDirection(point.Axis)) <= point.AlignmentDegrees;
                if (touching && aligned && agent.PreviousContact) work = Mathf.Clamp01(Vector3.Distance(head, agent.PreviousHead) / point.StrokeMetres);
                agent.PreviousHead = head; agent.PreviousContact = touching && aligned;
                agent.StrokePhase += dt * 3;
                Vector3 goal = point.Frame.position + point.Frame.right * Mathf.Sin(agent.StrokePhase) * Mathf.Min(.08f, point.StrokeMetres * .25f);
                DriveBody(agent.Tool, goal, agent.Tool.ToolHead.position, agent.Motion.transform, agent.Target);
                Vector3 axis = point.Frame.TransformDirection(point.Axis);
                Quaternion error = Quaternion.FromToRotation(agent.Tool.ToolHead.forward, axis);
                error.ToAngleAxis(out var angle, out var turnAxis);
                if (angle > 180) angle -= 360;
                if (!float.IsNaN(turnAxis.x) && !float.IsInfinity(turnAxis.x))
                    agent.Tool.Body.AddTorque(Vector3.ClampMagnitude(turnAxis * (angle * Mathf.Deg2Rad * 30) -
                        agent.Tool.Body.angularVelocity * 5, MaximumGripTorque), ForceMode.Force);
            }
            else if (action.Verb == ActionVerb.Open || action.Verb == ActionVerb.Close)
            {
                if (agent.Target == null || agent.Target.Hinge == null && agent.Target.Slider == null) { StopBlocked(agent, "저작된 실제 문 구동부가 없습니다"); return; }
                var endpoint = action.Verb == ActionVerb.Open ? agent.Target.OpenCoordinate : agent.Target.ClosedCoordinate;
                var range = Mathf.Abs(agent.Target.OpenCoordinate - agent.Target.ClosedCoordinate);
                if (range <= .0001f) { StopBlocked(agent, "문 이동 범위 근거가 없습니다"); return; }
                aligned = Mathf.Abs(agent.Target.ConstraintCoordinate - endpoint) <= range * .01f;
                if (touching) agent.Target.DriveConstraint(Mathf.Sign(endpoint - agent.Target.ConstraintCoordinate), MaximumGripForce);
                work = aligned ? 1 : 0;
            }
            else if (action.Verb == ActionVerb.PickUp)
            {
                if (agent.Target == null || !agent.Target.CanDriveBody(out _)) { StopBlocked(agent, "접촉 운반 형상이 없습니다"); return; }
                supported = touching; aligned = touching; work = agent.ContactElapsed >= .5f ? 1 : 0;
            }
            else if (action.Verb == ActionVerb.Handoff || action.Verb == ActionVerb.Escort || action.Verb == ActionVerb.Consent || action.Verb == ActionVerb.Verify)
            {
                supported = target.Kind == EntityKind.Actor || agent.Held == agent.Target;
                aligned = touching; work = agent.ContactElapsed >= .5f ? 1 : 0;
                if (action.RecipientId.HasValue && !Audible(agent.Id, action.RecipientId.Value)) { StopBlocked(agent, "인계 상대 미도착 · 책임 유지"); return; }
            }
            else { StopBlocked(agent, "현재 작업에는 실제 NPC 조작 연결이 없습니다"); return; }
            if (action.Verb != ActionVerb.MoveTo && work <= 0)
            {
                if (agent.ActionElapsed - agent.LastProgress > 5) StopBlocked(agent, "실제 접촉 진행 없음 · 부분 결과와 책임 유지");
                return;
            }
            agent.LastWorkTick = session.Tick.Microseconds;
            var receipt = session.AdvanceAction(agent.Id, action.IntentId, new ActionEvidence(Point(position), Point(contact), visible,
                touching, supported, aligned, work, target.Revision));
            if (receipt.ChangedWorld) agent.LastProgress = agent.ActionElapsed;
            if (receipt.Phase == ActionPhase.Completed) Complete(agent, receipt);
            else if (Terminal(receipt.Phase)) StopBlocked(agent, "실행 보류 · " + receipt.Reason, false);
            else agent.Status = "실제 작업 진행 · " + receipt.Reason;
        }

        private bool PrepareTool(Agent agent, ActorState actor, out string reason)
        {
            reason = "호환 공구와 저작된 접촉 작업점이 필요합니다";
            var p = agent.Point;
            if (agent.Tool == null || p == null || p.Surface == null || p.Frame == null || agent.Tool.ToolHead == null || p.StrokeMetres <= 0 ||
                p.ContactRadius <= 0 || agent.Tool.ToolHeadRadius <= 0 || p.AlignmentDegrees <= 0 || !agent.Tool.CanDriveBody(out reason)) return false;
            if (!session.TryGetEntity(new StableId(agent.Tool.EntityId), out var tool) || !tool.CustodianId.Equals(actor.Id) || p.CompatibleToolDefinition != tool.DefinitionId) return false;
            agent.Held = agent.Tool; reason = null; return true;
        }

        private void Complete(Agent agent, ActionReceipt receipt)
        {
            var action = agent.Action;
            agent.Action = null; agent.Motion.ClearDestination(); agent.Target?.StopConstraint();
            if (!session.TryGetActor(agent.Id, out var actor) || !session.TryGetEntity(action.TargetId, out var target)) return;
            actor = agent.Planner.ObserveOwnEffect(actor, target, action.Verb, session.Tick);
            actor = agent.Planner.CompleteAction(actor, action);
            session.UpdateActor(actor, "own_action_result");
            ReconcileCustody(target);
            agent.Status = actor.Plan == null ? "관측된 목표 달성" : "실행 결과 확인 · 목표 유지";
            Say(agent, CompletionText(action.Verb, receipt.Reason));
        }

        private void DeliverSocial(StableId speaker, ActorActionIntent action, WorldEntity target)
        {
            if (action.Verb != ActionVerb.RequestHelp && action.Verb != ActionVerb.Consent && action.Verb != ActionVerb.Report && action.Verb != ActionVerb.Handoff) return;
            foreach (var listener in agents)
            {
                if (listener.Id.Equals(speaker) || action.RecipientId.HasValue && !listener.Id.Equals(action.RecipientId.Value) || !Audible(speaker, listener.Id) ||
                    !session.TryGetActor(listener.Id, out var actor)) continue;
                string predicate = action.Verb == ActionVerb.RequestHelp ? "assistance.request" : action.Verb == ActionVerb.Consent ? "consent.report" : action.Verb == ActionVerb.Handoff ? "handoff.report" : "hazard.report";
                var heard = listener.Planner.Hear(actor, speaker, target.Id, target.Revision, predicate, RuleTruth.TRUE, session.Tick);
                // Local directed speech can draw attention to its visible speaker, not to distant reported subjects.
                if (listener.Motion != null && session.TryGetEntity(speaker, out var speakerEntity) && Visible(listener, speakerEntity, out _))
                    heard = listener.Planner.Observe(heard, speakerEntity, session.Tick, ObservationSource.Sight);
                if (target.Id.Equals(listener.Id))
                    heard = listener.Planner.Observe(heard, target, session.Tick, ObservationSource.Sight);
                // The typed consent action is a witnessed owner action, not arbitrary generated speech.
                if (action.Verb == ActionVerb.Consent)
                {
                    // A directed grant reveals this recipient's permission, not the speaker's other private consents.
                    string predicateForRecipient = "consent:" + listener.Id.Value;
                    var grant = new WorldEntity(target.Id, target.Label, target.Kind, target.Revision, target.Position, target.ZoneId,
                        definitionId: target.DefinitionId, facts: new Dictionary<string, RuleTruth> { [predicateForRecipient] = target.Fact(predicateForRecipient) });
                    heard = listener.Planner.ObserveOwnEffect(heard, grant, ActionVerb.Consent, session.Tick);
                }
                if (action.Verb == ActionVerb.RequestHelp && heard.Plan == null)
                {
                    var known = listener.Planner.GetKnownEntities(heard);
                    StableId? goalTarget = known.ContainsKey(target.Id) ? target.Id : known.ContainsKey(speaker) ? speaker : (StableId?)null;
                    if (goalTarget.HasValue)
                        heard = heard.With(plan: new GoalPlan(new StableId("goal:" + Guid.NewGuid().ToString("N")), GoalKind.HonourPromise,
                            goalTarget.Value, 0, "직접 받은 지원 요청을 확인하려고 합니다. 아직 책임 인계가 끝난 것은 아닙니다.", "responsibility.handed.off",
                            new[] { ActionVerb.MoveTo, ActionVerb.Consent, ActionVerb.Escort, ActionVerb.Handoff }), replacePlan: true);
                }
                if (!ReferenceEquals(actor, heard)) session.UpdateActor(heard, "audible_typed_social_action");
                if (action.Verb == ActionVerb.RequestHelp && !ReferenceEquals(actor, heard))
                    Say(listener, "지원 요청을 들었습니다. 제 상황과 동의 여부를 판단하겠습니다. 아직 인계된 것은 아닙니다.");
            }
        }

        private void FollowAgreedEscort(Agent agent)
        {
            if (agent.Motion == null || !agent.Motion.isActiveAndEnabled || !session.TryGetActor(agent.Id, out var actor) ||
                !session.TryGetEntity(agent.Id, out var self) || !self.ParentId.HasValue ||
                !session.TryGetActor(self.ParentId.Value, out _) || !session.TryGetEntity(self.ParentId.Value, out var leader)) return;
            if (self.Fact("consent:" + leader.Id.Value) != RuleTruth.TRUE) return;
            if (!TrySight(agent, leader, out var observedPosition))
            {
                agent.Motion.ClearDestination();
                agent.Status = "동행 상대 시야 상실 · 정지 후 직접 재관측 대기";
                return;
            }
            var observed = ObserveSight(agent, actor, leader, observedPosition);
            if (!ReferenceEquals(actor, observed)) session.UpdateActor(observed, "escort_leader_directly_observed");
            // Immediate steering uses this frame's verified sight, not the quantized long-term memory position.
            Vector3 observedLeader = Vector(observedPosition);
            Vector3 direction = observedLeader - agent.Motion.transform.position;
            direction.y = 0;
            Vector3 desired = observedLeader - direction.normalized * .8f;
            if ((desired - agent.Motion.transform.position).sqrMagnitude > 2.25f &&
                (!agent.Motion.HasDestination || (agent.Motion.Destination - desired).sqrMagnitude > .5f))
            {
                if (!agent.Motion.SetDestination(desired, out var reason)) agent.Status = "동행 경로 차단 · " + reason;
                else agent.Status = "동의한 동행 이동 중 · 도착은 아직 미확인";
            }
            else if ((desired - agent.Motion.transform.position).sqrMagnitude <= 2.25f) agent.Motion.ClearDestination();
        }

        private void Carry(Agent agent)
        {
            if (agent.Held == null || agent.Motion == null || !agent.Motion.isActiveAndEnabled) return;
            if (!session.TryGetEntity(new StableId(agent.Held.EntityId), out var entity) || entity.Kind == EntityKind.Actor ||
                !entity.CustodianId.HasValue || !entity.CustodianId.Value.Equals(agent.Id)) { agent.Held = null; return; }
            if (agent.Action != null && agent.Action.Verb == ActionVerb.Clean) return;
            Vector3 grip = agent.Held.GripPoint == null ? agent.Held.transform.position : agent.Held.GripPoint.position;
            DriveBody(agent.Held, Hand(agent) + agent.Motion.transform.forward * .25f, grip, agent.Motion.transform, null);
        }

        private void DriveBody(FpsEntityBinding item, Vector3 desired, Vector3 actual, Transform actor, FpsEntityBinding target)
        {
            if (item != null) session.SetPhysicalPose(new StableId(item.EntityId), Point(item.transform.position));
            if (item == null || !item.CanDriveBody(out _) || !queries.ClearMotion(actual, desired, .015f, actor, item, target)) return;
            var force = (desired - actual) * 180f - item.Body.GetPointVelocity(actual) * 25f;
            item.Body.AddForceAtPosition(Vector3.ClampMagnitude(force, MaximumGripForce), actual, ForceMode.Force);
        }

        private void StopBlocked(Agent agent, string reason, bool cancel = true)
        {
            if (cancel && agent.Action != null) session.CancelAction(agent.Id, agent.Action.IntentId, reason);
            agent.Action = null; agent.Motion?.ClearDestination(); agent.Target?.StopConstraint();
            if (session.TryGetActor(agent.Id, out var actor)) Block(agent, actor, reason);
        }
        private void Block(Agent agent, ActorState actor, string reason)
        {
            var blocked = agent.Planner.Block(actor, reason, RouteRevision, session.Tick, agent.Candidates);
            if (!ReferenceEquals(actor, blocked)) session.UpdateActor(blocked, "goal_blocked_not_abandoned");
            agent.Status = "목표 유지 · " + reason;
            Say(agent, "아직 끝나지 않았습니다. " + reason + ". 맡은 목표는 유지하겠습니다.");
        }
        private ActorState ObserveSight(Agent agent, ActorState actor, WorldEntity entity, WorldPoint position)
        {
            if (agent.Planner.GetKnownEntities(actor).TryGetValue(entity.Id, out var known) &&
                known.Revision == entity.Revision && known.Position.DistanceSquared(position) == 0) return actor;
            var observed = entity.Position.DistanceSquared(position) == 0 ? entity : entity.With(entity.Revision, position: position);
            return agent.Planner.Observe(actor, observed, session.Tick, ObservationSource.Sight);
        }
        private Transform ActorRoot(StableId id)
        {
            if (registry.TryGetValue(id, out var agent) && agent.Motion != null && agent.Motion.isActiveAndEnabled) return agent.Motion.transform;
            return humanObservers.TryGetValue(id, out var human) && human != null && human.gameObject.activeInHierarchy ? human : null;
        }
        private bool TrySight(Agent agent, WorldEntity entity, out WorldPoint observedPosition)
        {
            observedPosition = default;
            Vector3 location;
            if (physical.TryGetValue(entity.Id.Value, out var binding) && binding != null && binding.isActiveAndEnabled)
                location = binding.transform.position;
            else
            {
                var root = entity.Kind == EntityKind.Actor ? ActorRoot(entity.Id) : null;
                if (root == null) return false;
                location = root.position;
            }
            Vector3 delta = location - agent.Motion.transform.position;
            if (delta.sqrMagnitude > SightDistance * SightDistance ||
                delta.sqrMagnitude > 1 && Vector3.Angle(agent.Motion.transform.forward, delta) > FieldOfViewDegrees * .5f ||
                !Visible(agent, entity, out _)) return false;
            observedPosition = Point(location);
            return true;
        }
        private bool Visible(Agent agent, WorldEntity entity, out Vector3 contact)
        {
            contact = Vector(entity.Position);
            if (physical.TryGetValue(entity.Id.Value, out var binding) && binding.ContactSurface != null)
            { contact = binding.ContactSurface.ClosestPoint(Eye(agent)); return queries.Visible(Eye(agent), contact, agent.Motion.transform, binding, agent.Held); }
            if (entity.Kind == EntityKind.Actor)
            {
                var root = ActorRoot(entity.Id);
                if (root == null) return false;
                contact = root.position + Vector3.up;
                return ClearLine(Eye(agent), contact, agent.Motion.transform, entity.Id);
            }
            return false; // A semantic coordinate alone is not visible geometry.
        }
        private bool Contact(Agent agent, WorldEntity target, Vector3 contact)
        {
            float reach = agent.Target != null && agent.Target.HandReach > 0 ? Mathf.Min(NpcHandReach, agent.Target.HandReach) : NpcHandReach;
            if ((contact - Hand(agent)).sqrMagnitude > reach * reach) return false;
            if (agent.Target != null) return queries.ClearPath(Hand(agent), contact, .015f, agent.Motion.transform, agent.Target, agent.Tool);
            return target.Kind == EntityKind.Actor && ClearLine(Hand(agent), contact, agent.Motion.transform, target.Id);
        }
        private bool Audible(StableId from, StableId to)
        {
            if (!session.TryGetEntity(from, out var a) || !session.TryGetEntity(to, out var b) || a.Position.DistanceSquared(b.Position) > HearingDistance * HearingDistance) return false;
            Transform speaker = registry.TryGetValue(from, out var owner) && owner.Motion != null ? owner.Motion.transform : null;
            if (speaker == null) humanObservers.TryGetValue(from, out speaker);
            return ClearLine(Vector(a.Position) + Vector3.up * 1.4f, Vector(b.Position) + Vector3.up * 1.4f, speaker, to);
        }
        private bool ClearLine(Vector3 from, Vector3 to, Transform observer, StableId target)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return true;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, hearingHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hearingHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = hearingHits[i].collider;
                if (collider == null || observer != null && collider.transform.IsChildOf(observer)) continue;
                if (humanObservers.TryGetValue(target, out var human) && human != null && collider.transform.IsChildOf(human)) continue;
                var actor = collider.GetComponentInParent<ActorNavigationBinding>();
                if (actor != null && actor.ActorId == target.Value) continue;
                var binding = collider.GetComponentInParent<FpsEntityBinding>();
                if (binding != null && binding.EntityId == target.Value) continue;
                return false;
            }
            return true;
        }
        private void Say(Agent agent, string text, string source = "template")
        {
            if (agent.LastSpeech == text) return;
            agent.LastSpeech = text; Speech?.Invoke(new NpcSpeech(agent.Id, text, source, agent.Status));
        }
        private static bool Terminal(ActionPhase phase) => phase == ActionPhase.Blocked || phase == ActionPhase.Cancelled || phase == ActionPhase.Failed;
        private static bool Conversational(ActionVerb verb) => verb == ActionVerb.Observe || verb == ActionVerb.Report || verb == ActionVerb.RequestHelp || verb == ActionVerb.Consent;
        private int RouteRevision => navigation == null ? -1 : navigation.RouteRevision;
        private static Vector3 Eye(Agent agent) => agent.Motion.transform.position + Vector3.up * 1.5f;
        private static Vector3 Hand(Agent agent) => agent.Motion.transform.position + Vector3.up * 1.1f;
        private static Vector3 ToolHead(Agent agent) => agent.Tool != null && agent.Tool.ToolHead != null ? agent.Tool.ToolHead.position : Vector3.zero;
        private static Vector3 Vector(WorldPoint point) => new Vector3(point.X, point.Y, point.Z);
        private static WorldPoint Point(Vector3 point) => new WorldPoint(point.x, point.y, point.z);
        private static Vector3Int Cell(Vector3 point) => new Vector3Int(Mathf.FloorToInt(point.x / 8f), Mathf.FloorToInt(point.y / 8f), Mathf.FloorToInt(point.z / 8f));
        private static string VerbText(ActionVerb verb)
        {
            switch (verb) { case ActionVerb.Clean: return "실제 오염을 닦아"; case ActionVerb.RequestHelp: return "지원을 요청해"; case ActionVerb.Escort: return "동의를 확인하고 동행해"; case ActionVerb.Handoff: return "실제로 인계해"; default: return "현재 상태를 확인해"; }
        }
        private static string CompletionText(ActionVerb verb, string reason)
        {
            switch (verb)
            {
                case ActionVerb.Clean: return "확인된 접촉 구역의 잔여 오염 결과가 반영되었습니다. 건조나 살균 완료를 뜻하지는 않습니다.";
                case ActionVerb.Escort: return "동행에 합의했습니다. 목적지 도착이나 안전 확인은 아직 아닙니다.";
                case ActionVerb.Consent: return "제 의사로 지원에 동의합니다.";
                case ActionVerb.RequestHelp: return "도움을 요청했습니다. 상대의 수락과 실제 도착은 따로 확인해야 합니다.";
                case ActionVerb.Handoff: return "실행기가 현장 인계와 책임 이전을 확인했습니다.";
                case ActionVerb.Verify: return "실습 점검 조건을 확인했습니다. 철도 설비의 안전 인증은 아닙니다.";
                default: return "행동 결과를 확인했습니다: " + reason;
            }
        }
        private void Shutdown()
        {
            if (session != null) session.Committed -= OnCommitted;
            foreach (var agent in agents)
            {
                agent.Cancellation?.Cancel(); agent.Cancellation?.Dispose();
                agent.DialogueCancellation?.Cancel(); agent.DialogueCancellation?.Dispose();
                agent.Target?.StopConstraint(); agent.Motion?.ClearDestination();
            }
            agents.Clear(); registry.Clear(); physical.Clear(); cells.Clear(); cellLists.Clear(); entityIds.Clear(); entitySet.Clear();
            humanObservers.Clear(); socialEvents.Clear(); resourceEvents.Clear();
            initialized = false;
        }
        private void OnDestroy() => Shutdown();
    }
}
