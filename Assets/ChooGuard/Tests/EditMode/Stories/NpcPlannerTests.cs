#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class NpcPlannerTests
    {
        private static StableId Id(string value) => new StableId(value);
        private static ActorState Actor(string id, ActorRole role = ActorRole.Cleaning) =>
            new ActorState(Id(id), role, 0, 0, 0, false, "일정을 지키되 관측 가능한 상황을 확인합니다.");
        private static WorldEntity Entity(string id, EntityKind kind, long revision = 1, IDictionary<string, RuleTruth> facts = null,
            StableId? owner = null, string definition = null) => new WorldEntity(Id(id), id, kind, revision,
                new WorldPoint(1, 0, 1), Id("zone"), owner, definitionId: definition, facts: facts);

        [Test]
        public void AReportDoesNotTransferAnotherObserversPrivateTruth()
        {
            var first = new NpcPlanner(Id("first")); var second = new NpcPlanner(Id("second"));
            var hazard = Entity("door", EntityKind.Door, facts: new Dictionary<string, RuleTruth> { ["hazardous"] = RuleTruth.TRUE, ["power.on"] = RuleTruth.TRUE });
            var a = first.Observe(Actor("first"), hazard, new SimTick(1), ObservationSource.Sight);
            var b = second.Hear(Actor("second"), a.Id, hazard.Id, hazard.Revision, "hazardous", RuleTruth.FALSE, new SimTick(2));
            Assert.That(first.KnownFact(a, hazard.Id, "hazardous"), Is.EqualTo(RuleTruth.TRUE));
            Assert.That(first.KnownFact(a, hazard.Id, "power.on"), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(second.KnownFact(b, hazard.Id, "hazardous"), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(second.GetKnownEntities(b).ContainsKey(hazard.Id), Is.False);
            Assert.That(b.Memories[0].Source, Is.EqualTo(ObservationSource.Heard));
            Assert.That(b.Memories[0].InformantId, Is.EqualTo(a.Id));
            Assert.That(b.Memories[0].Value, Is.EqualTo(RuleTruth.FALSE));
        }

        [Test]
        public void OwnObservationCompletionDoesNotRevealElectricalOrMeasurementTruth()
        {
            var planner = new NpcPlanner(Id("npc"));
            var target = Entity("equipment", EntityKind.Equipment, facts: new Dictionary<string, RuleTruth> {
                ["inspected"] = RuleTruth.TRUE, ["power.on"] = RuleTruth.FALSE, ["isolated"] = RuleTruth.TRUE });
            var state = planner.ObserveOwnEffect(Actor("npc", ActorRole.Maintenance), target, ActionVerb.Observe, new SimTick(2));
            Assert.That(planner.KnownFact(state, target.Id, "inspected"), Is.EqualTo(RuleTruth.TRUE));
            Assert.That(planner.KnownFact(state, target.Id, "power.on"), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(planner.KnownFact(state, target.Id, "isolated"), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(state.Memories, Has.None.Matches<ActorObservation>(m => m.FactId == "isolated"));
        }

        [Test]
        public void CleaningRoleFormsItsOwnGoalWithoutPlayerRequest()
        {
            var planner = new NpcPlanner(Id("cleaner"));
            var surface = Entity("surface", EntityKind.Surface, facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE });
            var state = planner.FormGoal(planner.Observe(Actor("cleaner"), surface, new SimTick(1), ObservationSource.Sight));
            Assert.That(state.Plan.Kind, Is.EqualTo(GoalKind.RestoreCleanliness));
            Assert.That(state.Plan.TargetId, Is.EqualTo(surface.Id));
            Assert.That(state.Drives.ContainsKey(GoalKind.RestoreCleanliness), Is.True);
        }

        [Test]
        public void UnrelatedSightDoesNotRetryBlockedResponsibilityButTargetChangeDoes()
        {
            var planner = new NpcPlanner(Id("npc"));
            var target = Entity("surface", EntityKind.Surface, facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE });
            var state = planner.FormGoal(planner.Observe(Actor("npc"), target, new SimTick(1), ObservationSource.Sight));
            var goal = state.Plan;
            state = planner.Block(state, "resource_reserved", 8, new SimTick(2), planner.BuildCandidates(state));
            Assert.That(planner.CanReplan(state, 8), Is.False);
            state = planner.Observe(state, Entity("unrelated", EntityKind.Door), new SimTick(3), ObservationSource.Sight);
            Assert.That(planner.CanReplan(state, 8), Is.False);
            Assert.That(state.Plan.Id, Is.EqualTo(goal.Id));
            Assert.That(state.Plan.TargetId, Is.EqualTo(goal.TargetId));
            Assert.That(state.Plan.RemainingSteps, Is.EqualTo(goal.RemainingSteps));
            state = planner.Observe(state, target.With(2, facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.FALSE }), new SimTick(4), ObservationSource.Sight);
            Assert.That(planner.CanReplan(state, 8), Is.True);
            var resumed = planner.Resume(state);
            Assert.That(resumed.Plan.Id, Is.EqualTo(goal.Id));
            Assert.That(resumed.Plan.BlockedReason, Is.Null);
        }

        [Test]
        public void NewGeometryCanResumeWithoutErasingBlockedGoal()
        {
            var planner = new NpcPlanner(Id("npc"));
            var state = planner.FormGoal(planner.Observe(Actor("npc"), Entity("exit", EntityKind.Exit), new SimTick(1), ObservationSource.Sight));
            state = planner.Block(state, "no_route", 3, new SimTick(2));
            var repeated = planner.FormGoal(state);
            Assert.That(repeated.Plan, Is.SameAs(state.Plan));
            Assert.That(planner.CanReplan(repeated, 3), Is.False);
            Assert.That(planner.CanReplan(repeated, 4), Is.True);
        }

        [Test]
        public void ToolCapabilityNotDefinitionSubstringGroundsAcquisitionAndUse()
        {
            var planner = new NpcPlanner(Id("npc"));
            var surface = Entity("surface", EntityKind.Surface, facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE });
            var tool = Entity("tool", EntityKind.Tool, facts: new Dictionary<string, RuleTruth> { ["portable"] = RuleTruth.TRUE, ["tool.cleaning"] = RuleTruth.TRUE }, definition: "practice-cloth");
            var state = planner.Observe(Actor("npc"), surface, new SimTick(1), ObservationSource.Sight);
            state = planner.FormGoal(planner.Observe(state, tool, new SimTick(1), ObservationSource.Sight));
            Assert.That(planner.BuildCandidates(state).Exists(c => c.Verb == ActionVerb.PickUp && c.TargetId.Equals(tool.Id)), Is.True);
            Assert.That(planner.BuildCandidates(state).Exists(c => c.Verb == ActionVerb.Clean), Is.False);
            state = planner.ObserveOwnEffect(state, tool.With(2, custodianId: state.Id, replaceCustodian: true), ActionVerb.PickUp, new SimTick(2));
            Assert.That(planner.BuildCandidates(state).Exists(c => c.Verb == ActionVerb.Clean && c.ToolId.Equals(tool.Id)), Is.True);
        }

        [Test]
        public void ReachingScheduleDestinationDoesNotRegenerateTheSameUnchangedTrip()
        {
            var planner = new NpcPlanner(Id("npc")); var target = Entity("exit", EntityKind.Exit);
            var state = planner.FormGoal(planner.Observe(Actor("npc", ActorRole.Citizen), target, new SimTick(1), ObservationSource.Sight));
            var action = new ActorActionIntent(Id("run"), 0, state.Id, Id("intent"), 1, ActionVerb.MoveTo, target.Id,
                0, new SimTick(100), new[] { new EntityReadVersion(target.Id, target.Revision) });
            state = planner.CompleteAction(state, action);
            Assert.That(state.Plan, Is.Null);
            Assert.That(planner.FormGoal(state).Plan, Is.Null);
        }

        [Test]
        public void HeardHelpCanOfferConsentOnlyFromTheListenersOwnIdentity()
        {
            var planner = new NpcPlanner(Id("listener"));
            var state = planner.Observe(Actor("listener", ActorRole.Citizen), Entity("listener", EntityKind.Actor), new SimTick(1), ObservationSource.Sight);
            state = planner.Observe(state, Entity("requester", EntityKind.Actor), new SimTick(1), ObservationSource.Sight);
            state = planner.FormGoal(planner.Observe(state, Entity("exit", EntityKind.Exit), new SimTick(1), ObservationSource.Sight));
            state = planner.Hear(state, Id("requester"), state.Id, 1, "assistance.request", RuleTruth.TRUE, new SimTick(2));
            var consent = planner.BuildCandidates(state).Find(c => c.Verb == ActionVerb.Consent);
            Assert.That(consent.ActorId, Is.EqualTo(state.Id));
            Assert.That(consent.TargetId, Is.EqualTo(state.Id));
            Assert.That(consent.RecipientId, Is.EqualTo(Id("requester")));
            Assert.That(planner.KnownFact(state, state.Id, "consent:requester"), Is.EqualTo(RuleTruth.UNKNOWN));
        }

        private sealed class FailingCommit : IGameplayCommitSink
        {
            public bool Fail = true;
            public void Commit(WorldMutation mutation) { if (Fail) throw new System.IO.IOException("disk_full"); }
        }

        [Test]
        public void FailedObservationCommitCannotLeakKnowledgeOrSuppressTheNextObservation()
        {
            var actor = Actor("observer");
            var body = Entity("observer", EntityKind.Actor);
            var surface = Entity("dirty-surface", EntityKind.Surface, facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE });
            var initial = new WorldSnapshot(Id("knowledge-" + Guid.NewGuid().ToString("N")), 0, 0, new SimTick(0),
                GameplayMode.RandomOperationsLab, "test", 1,
                new Dictionary<StableId, WorldEntity> { [body.Id] = body, [surface.Id] = surface },
                new Dictionary<StableId, ActorState> { [actor.Id] = actor });
            var sink = new FailingCommit();
            using (var world = new WorldSession(initial, sink))
            {
                var planner = new NpcPlanner(actor.Id);
                var proposed = planner.FormGoal(planner.Observe(actor, surface, new SimTick(1), ObservationSource.Sight));
                Assert.That(proposed.Plan.Kind, Is.EqualTo(GoalKind.RestoreCleanliness));
                Assert.Throws<System.IO.IOException>(() => world.UpdateActor(proposed, "observation"));
                world.TryGetActor(actor.Id, out var actual);
                Assert.That(planner.KnownFact(actual, surface.Id, "dirty"), Is.EqualTo(RuleTruth.UNKNOWN));
                Assert.That(actual.KnowledgeRevision, Is.EqualTo(0));
                sink.Fail = false;
                world.UpdateActor(planner.Observe(actual, surface, new SimTick(2), ObservationSource.Sight), "observation");
                world.TryGetActor(actor.Id, out actual);
                Assert.That(new NpcPlanner(actor.Id).FormGoal(actual).Plan.Kind, Is.EqualTo(GoalKind.RestoreCleanliness));
                Assert.That(planner.KnownFact(actual, surface.Id, "dirty"), Is.EqualTo(RuleTruth.TRUE));
            }
        }
    }
}
#endif
