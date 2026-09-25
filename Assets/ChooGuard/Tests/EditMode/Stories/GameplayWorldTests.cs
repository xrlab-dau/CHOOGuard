using System;
using System.Collections.Generic;
using System.IO;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class GameplayWorldTests
    {
        private sealed class Journal : IGameplayCommitSink
        {
            public bool Fail;
            public readonly List<WorldMutation> Entries = new List<WorldMutation>();
            public void Commit(WorldMutation mutation) { if (Fail) throw new IOException("disk_full"); Entries.Add(mutation); }
        }
        private static StableId Id(string value) => new StableId(value);
        private static readonly StableId Player = Id("player"), Other = Id("other"), Zone = Id("zone"), Tool = Id("cloth"), Surface = Id("surface"), Equipment = Id("equipment");
        private static WorldSnapshot Initial(GameplayMode mode = GameplayMode.RandomOperationsLab, ActorRole role = ActorRole.Maintenance)
        {
            var entities = new Dictionary<StableId, WorldEntity>();
            entities.Add(Zone, new WorldEntity(Zone, "연습 구역", EntityKind.Zone, 0, new WorldPoint(0, 0, 0), Zone));
            entities.Add(Player, new WorldEntity(Player, "사람", EntityKind.Actor, 0, new WorldPoint(0, 0, 0), Zone));
            entities.Add(Other, new WorldEntity(Other, "동료", EntityKind.Actor, 0, new WorldPoint(.2f, 0, 0), Zone));
            entities.Add(Tool, new WorldEntity(Tool, "청소 도구", EntityKind.Tool, 0, new WorldPoint(0, 0, .5f), Zone,
                facts: new Dictionary<string, RuleTruth> { ["portable"] = RuleTruth.TRUE, ["installed"] = RuleTruth.FALSE, ["hazardous"] = RuleTruth.FALSE,
                    ["tool.cleaning"] = RuleTruth.TRUE, ["contaminated"] = RuleTruth.FALSE },
                measurements: new Dictionary<string, SiValue> { ["supply"] = new SiValue(.001, SiUnit.CubicMetre), ["consumption.per-work"] = new SiValue(.0001, SiUnit.CubicMetre) }));
            entities.Add(Surface, new WorldEntity(Surface, "접촉 표면", EntityKind.Surface, 0, new WorldPoint(0, 0, .8f), Zone,
                facts: new Dictionary<string, RuleTruth> { ["safe.material.known"] = RuleTruth.TRUE, ["dirty"] = RuleTruth.TRUE, ["wet"] = RuleTruth.FALSE },
                measurements: new Dictionary<string, SiValue> { ["soil:main"] = new SiValue(1, SiUnit.Dimensionless) }));
            entities.Add(Equipment, new WorldEntity(Equipment, "명시적 연습 전원", EntityKind.Equipment, 0, new WorldPoint(.6f, 0, .4f), Zone,
                facts: new Dictionary<string, RuleTruth> { ["power.on"] = RuleTruth.TRUE, ["fault"] = RuleTruth.TRUE, ["ignited"] = RuleTruth.FALSE,
                    ["isolated"] = RuleTruth.FALSE, ["isolation.procedure.known"] = RuleTruth.TRUE, ["isolation.permitted"] = RuleTruth.TRUE }));
            var actors = new Dictionary<StableId, ActorState>
            {
                [Player] = new ActorState(Player, role, 0, 0, 0, true, ""),
                [Other] = new ActorState(Other, role, 0, 0, 0, false, "")
            };
            return new WorldSnapshot(Id("world-" + Guid.NewGuid().ToString("N")), 0, 0, new SimTick(0), mode, "test-authored-rules", 1, entities, actors);
        }
        private static ActionReceipt Execute(WorldSession world, StableId actor, ActionVerb verb, StableId target, StableId? tool = null,
            double work = 1, StableId? recipient = null, StableId? supportedBy = null)
        {
            var intent = world.CreateIntent(actor, verb, target, tool, recipient);
            var accepted = world.RequestAction(intent);
            if (accepted.Phase != ActionPhase.Reserved) return accepted;
            world.TryGetEntity(actor, out var person); world.TryGetEntity(target, out var entity);
            world.AdvanceTime(new SimTick(world.Tick.Microseconds + 100000));
            return world.AdvanceAction(actor, intent.IntentId, new ActionEvidence(person.Position, entity.Position, true, true, true, true, work, entity.Revision, supportedBy));
        }
        private static TransitionKernel Kernel()
        {
            return new TransitionKernel(new[] { new TransitionDefinition("fault-ignition", "authored-lab", "연습 발화", EntityKind.Equipment,
                EntityKind.Equipment, TransitionRelation.Self, new Dictionary<string, RuleTruth> { ["power.on"] = RuleTruth.TRUE, ["fault"] = RuleTruth.TRUE },
                new Dictionary<string, RuleTruth> { ["ignited"] = RuleTruth.FALSE }, new Dictionary<string, RuleTruth> { ["ignited"] = RuleTruth.TRUE }) });
        }
        private static WorldSnapshot InitialWithSigns()
        {
            var basis = Initial();
            var entities = new Dictionary<StableId, WorldEntity>(basis.Entities);
            foreach (var id in new[] { Id("sign-a"), Id("sign-b") })
                entities.Add(id, new WorldEntity(id, id.Value, EntityKind.Sign, 0, new WorldPoint(.2f, 0, .4f), Zone,
                    facts: new Dictionary<string, RuleTruth>
                    { ["portable"] = RuleTruth.TRUE, ["installed"] = RuleTruth.FALSE, ["hazardous"] = RuleTruth.FALSE }));
            return new WorldSnapshot(basis.RunId, basis.Generation, basis.Revision, basis.Tick, basis.Mode,
                basis.RulesetId, basis.RulesetRevision, entities, new Dictionary<StableId, ActorState>(basis.Actors));
        }

        [Test]
        public void CompetingPickupAndDuplicateDeliveryCannotCreateTwoCustodians()
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                var a = world.CreateIntent(Player, ActionVerb.PickUp, Tool);
                var b = world.CreateIntent(Other, ActionVerb.PickUp, Tool);
                Assert.That(world.RequestAction(a).Phase, Is.EqualTo(ActionPhase.Reserved));
                Assert.That(world.RequestAction(b).Reason, Is.EqualTo("resource_reserved"));
                world.AdvanceTime(new SimTick(1)); world.TryGetEntity(Tool, out var tool);
                var done = world.AdvanceAction(Player, a.IntentId, new ActionEvidence(new WorldPoint(0, 0, 0), tool.Position, true, true, true, true, 1, tool.Revision));
                Assert.That(done.Phase, Is.EqualTo(ActionPhase.Completed));
                Assert.That(world.RequestAction(a), Is.SameAs(done));
                Assert.That(world.RequestAction(b).Phase, Is.EqualTo(ActionPhase.Blocked));
                world.TryGetEntity(Tool, out tool); Assert.That(tool.CustodianId, Is.EqualTo(Player));
            }
        }
        [TestCase("surface", null, false)]
        [TestCase("surface", "zone", false)]
        [TestCase("surface", "surface", true)]
        [TestCase(null, null, true)]
        public void PlacementCreditsOnlyThePhysicallySupportedRecipient(string recipient, string supportedBy, bool completed)
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, Tool).Phase);
                StableId? destination = recipient == null ? (StableId?)null : Id(recipient);
                var receipt = Execute(world, Player, ActionVerb.PutDown, Tool, recipient: destination,
                    supportedBy: supportedBy == null ? (StableId?)null : Id(supportedBy));
                Assert.AreEqual(completed ? ActionPhase.Completed : ActionPhase.Blocked, receipt.Phase);
                world.TryGetEntity(Tool, out var item);
                Assert.AreEqual(completed ? (StableId?)null : Player, item.CustodianId);
                Assert.AreEqual(completed ? destination : null, item.ParentId);
            }
        }
        [Test]
        public void PickingUpFromAContainerClearsContainmentBeforeHandoff()
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Other, ActionVerb.Consent, Other, recipient: Player).Phase);
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, Tool).Phase);
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PutDown, Tool, recipient: Surface, supportedBy: Surface).Phase);
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, Tool).Phase);
                world.TryGetEntity(Tool, out var lifted);
                Assert.IsNull(lifted.ParentId);
                Assert.AreEqual(Player, lifted.CustodianId);
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.Handoff, Tool, recipient: Other).Phase);
                world.TryGetEntity(Tool, out var transferred);
                Assert.IsNull(transferred.ParentId);
                Assert.AreEqual(Other, transferred.CustodianId);
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void CordonLosesOnlyTheRemovedSignsEvidence(bool moveFirstSign)
        {
            using (var world = new WorldSession(InitialWithSigns(), new Journal()))
            {
                var first = Id("sign-a"); var second = Id("sign-b");
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, first).Phase);
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.Cordon, Surface, first).Phase);
                world.TryGetEntity(first, out var placed);
                Assert.AreEqual(Surface, placed.ParentId);
                Assert.AreEqual(.4f, placed.Position.Z, "Cordon must not replace the measured sign pose with the target centre.");
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, second).Phase);
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.Cordon, Surface, second).Phase);
                if (moveFirstSign) world.SetPhysicalPose(first, new WorldPoint(ActionRules.CordonPlacementDistance + 1, 0, .4f));
                else
                {
                    Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, first).Phase);
                    Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PutDown, first).Phase);
                }
                world.TryGetEntity(Surface, out var protectedSurface);
                Assert.AreEqual(RuleTruth.TRUE, protectedSurface.Fact("cordoned"));
                Assert.AreEqual(RuleTruth.FALSE, protectedSurface.Fact("cordon.sign:" + first.Value));
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, second).Phase);
                world.TryGetEntity(Surface, out var unprotectedSurface);
                Assert.AreEqual(RuleTruth.FALSE, unprotectedSurface.Fact("cordoned"));
                world.SetPhysicalPose(first, placed.Position);
                world.TryGetEntity(Surface, out var movedBack);
                Assert.AreEqual(RuleTruth.FALSE, movedBack.Fact("cordoned"), "A pose alone cannot recreate revoked placement evidence.");
            }
        }
        [Test]
        public void CordonRejectsSignOutsideTheDeclaredPlacementRadius()
        {
            using (var world = new WorldSession(InitialWithSigns(), new Journal()))
            {
                var sign = Id("sign-a");
                Assert.AreEqual(ActionPhase.Completed, Execute(world, Player, ActionVerb.PickUp, sign).Phase);
                world.SetPhysicalPose(sign, new WorldPoint(ActionRules.CordonPlacementDistance + 1, 0, .4f));
                Assert.AreEqual(ActionPhase.Blocked, Execute(world, Player, ActionVerb.Cordon, Surface, sign).Phase);
                world.TryGetEntity(Surface, out var surface);
                world.TryGetEntity(sign, out var held);
                Assert.AreNotEqual(RuleTruth.TRUE, surface.Fact("cordoned"));
                Assert.AreEqual(Player, held.CustodianId);
                Assert.IsNull(held.ParentId);
            }
        }
        [Test]
        public void ConflictingIntentBodyIsRejectedWithoutChangingOriginalReservation()
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                var original = world.CreateIntent(Player, ActionVerb.PickUp, Tool); world.RequestAction(original);
                var conflicting = new ActorActionIntent(original.RunId, original.Generation, original.ActorId, original.IntentId, original.IntentSequence,
                    ActionVerb.Observe, original.TargetId, original.RoleRevision, original.ExpiresAt, original.ReadSet);
                Assert.That(world.RequestAction(conflicting).Reason, Is.EqualTo("intent_fingerprint_conflict"));
                Assert.That(world.GetActiveIntent(Player), Is.SameAs(original));
            }
        }
        [Test]
        public void PersistenceFailureLeavesNoReservationOrSequenceAdvance()
        {
            var journal = new Journal { Fail = true };
            using (var world = new WorldSession(Initial(), journal))
            {
                Assert.Throws<IOException>(() => world.RequestAction(world.CreateIntent(Player, ActionVerb.PickUp, Tool)));
                Assert.That(world.GetActiveIntent(Player), Is.Null);
                world.TryGetActor(Player, out var player); Assert.That(player.ActionSequence, Is.EqualTo(0));
                journal.Fail = false;
                Assert.That(Execute(world, Other, ActionVerb.PickUp, Tool).Phase, Is.EqualTo(ActionPhase.Completed));
                world.TryGetEntity(Tool, out var tool); Assert.That(tool.CustodianId, Is.EqualTo(Other));
            }
        }
        [Test]
        public void CancelPreservesActualContactResidueConsumptionAndHeldTool()
        {
            using (var world = new WorldSession(Initial(role: ActorRole.Cleaning), new Journal()))
            {
                Execute(world, Player, ActionVerb.PickUp, Tool);
                var receipt = Execute(world, Player, ActionVerb.Clean, Surface, Tool, .25);
                Assert.That(receipt.Phase, Is.EqualTo(ActionPhase.Executing));
                world.CancelAction(Player, receipt.IntentId, "focus_lost");
                world.TryGetEntity(Surface, out var surface); world.TryGetEntity(Tool, out var tool);
                Assert.That(surface.Measurement("soil:main").Value.Value, Is.EqualTo(.75));
                Assert.That(tool.Measurement("supply").Value.Value, Is.EqualTo(.000975).Within(1e-12));
                Assert.That(tool.CustodianId, Is.EqualTo(Player));
                Assert.That(surface.Fact("wet"), Is.EqualTo(RuleTruth.TRUE));
                Assert.That(surface.Fact("verified"), Is.EqualTo(RuleTruth.UNKNOWN));
            }
        }
        [Test]
        public void RewindRejectsOldGenerationBeforeReturningItsCachedReceipt()
        {
            var initial = Initial(GameplayMode.Tutorial);
            using (var world = new WorldSession(initial, new Journal()))
            {
                var intent = world.CreateIntent(Player, ActionVerb.PickUp, Tool); world.RequestAction(intent);
                world.Restore(initial);
                Assert.That(world.RequestAction(intent).Reason, Is.EqualTo("stale_generation"));
                Assert.That(world.Generation, Is.EqualTo(1));
                world.TryGetEntity(Tool, out var tool); Assert.That(tool.CustodianId, Is.Null);
                Assert.That(Execute(world, Other, ActionVerb.PickUp, Tool).Phase, Is.EqualTo(ActionPhase.Completed));
            }
        }
        [Test]
        public void SwitchingRoleRevokesOldIntentButDoesNotDestroyCarriedEquipment()
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                Execute(world, Player, ActionVerb.PickUp, Tool);
                var intent = world.CreateIntent(Player, ActionVerb.Isolate, Equipment); world.RequestAction(intent);
                world.ChangeRole(Player, ActorRole.PassengerService);
                world.TryGetEntity(Tool, out var tool); Assert.That(tool.CustodianId, Is.EqualTo(Player));
                Assert.That(world.GetActiveIntent(Player), Is.Null);
                Assert.That(world.RequestAction(intent).Phase, Is.EqualTo(ActionPhase.Cancelled));
                Assert.That(Execute(world, Player, ActionVerb.Isolate, Equipment).Reason, Is.EqualTo("role_not_authorized"));
            }
        }
        [Test]
        public void ForecastDoesNotWriteLiveMemoryInventorySequenceOrWorld()
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                var before = world.Snapshot(); var composer = new FutureComposer(Kernel());
                var branch = composer.Begin(before, "forecast-test"); var choices = composer.Candidates(branch);
                Assert.That(composer.ApplySelection(branch, choices, choices[0].CandidateId, new string('a', 64), "jev-1.13.0", out _), Is.True);
                Assert.That(branch.State.Entities[Equipment].Fact("ignited"), Is.EqualTo(RuleTruth.TRUE));
                Assert.That(world.Revision, Is.EqualTo(before.Revision));
                world.TryGetEntity(Equipment, out var actual); Assert.That(actual.Fact("ignited"), Is.EqualTo(RuleTruth.FALSE));
                world.TryGetActor(Player, out var actor); Assert.That(actor.ActionSequence, Is.EqualTo(0)); Assert.That(actor.Memories.Count, Is.EqualTo(0));
            }
        }
        [Test]
        public void PlayerIsolationRemovesCauseAndRejectsPreviouslyPredictedIgnition()
        {
            using (var world = new WorldSession(Initial(), new Journal()))
            {
                var kernel = Kernel(); var basis = world.Snapshot(); var prediction = kernel.BuildEnvironmentCandidates(basis)[0];
                Assert.That(Execute(world, Player, ActionVerb.Isolate, Equipment).Phase, Is.EqualTo(ActionPhase.Completed));
                Assert.That(world.CommitEnvironment(kernel, basis, prediction, "environment-1", new string('b', 64), out _), Is.False);
                Assert.That(kernel.BuildEnvironmentCandidates(world.Snapshot()).Count, Is.EqualTo(1));
                world.TryGetEntity(Equipment, out var target); Assert.That(target.Fact("ignited"), Is.EqualTo(RuleTruth.FALSE));
                Assert.That(target.Measurement("voltage"), Is.Null);
            }
        }
        [Test]
        public void RecordedMutationsReplayWithoutReinferenceOrDoubleConsumption()
        {
            var initial = Initial(role: ActorRole.Cleaning); var journal = new Journal(); double supply;
            using (var world = new WorldSession(initial, journal))
            {
                Execute(world, Player, ActionVerb.PickUp, Tool); Execute(world, Player, ActionVerb.Clean, Surface, Tool, .5);
                world.TryGetEntity(Tool, out var tool); supply = tool.Measurement("supply").Value.Value;
            }
            using (var replay = new WorldSession(initial, new Journal()))
            {
                foreach (var entry in journal.Entries) replay.ReplayCommitted(GameplayCodec.DeserializeMutation(GameplayCodec.SerializeMutation(entry)));
                replay.TryGetEntity(Tool, out var tool); Assert.That(tool.Measurement("supply").Value.Value, Is.EqualTo(supply));
                Assert.That(tool.CustodianId, Is.EqualTo(Player)); Assert.That(replay.GetActiveIntent(Player).Verb, Is.EqualTo(ActionVerb.Clean));
                replay.AdvanceGeneration("broker_restart");
                Assert.That(replay.GetActiveIntent(Player), Is.Null); replay.TryGetEntity(Tool, out tool); Assert.That(tool.CustodianId, Is.EqualTo(Player));
            }
        }
        [Test]
        public void SnapshotCodecPreservesInt64IdentityAndUnknownMeasurements()
        {
            var initial = Initial(); var json = GameplayCodec.SerializeSnapshot(initial); var restored = GameplayCodec.DeserializeSnapshot(json);
            Assert.That(restored.RunId, Is.EqualTo(initial.RunId)); Assert.That(restored.Entities[Equipment].Measurement("voltage"), Is.Null);
            Assert.That(restored.Entities[Equipment].Fact("verified"), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(restored.Actors[Player].Role, Is.EqualTo(ActorRole.Maintenance));
        }
    }
}
