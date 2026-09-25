using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using NUnit.Framework;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class RoleTrainingProgramTests
    {
        private sealed class Journal : IGameplayCommitSink
        {
            public readonly List<WorldMutation> Entries = new List<WorldMutation>();
            public void Commit(WorldMutation mutation) => Entries.Add(mutation);
        }
        private sealed class Practice : IDisposable
        {
            public readonly RoleTrainingFixture Fixture;
            public readonly WorldSession World;
            public readonly RoleTrainingProgram Training = new RoleTrainingProgram();
            public Practice(ActorRole role)
            {
                Fixture = RoleTrainingFixtures.Create(new StableId("tutorial-test"), role);
                World = new WorldSession(Fixture.Snapshot, new Journal());
                Training.Initialize(World, role, Fixture.Bindings, null);
            }
            public StableId Id(string key) => Fixture.Bindings[key];
            public WorldEntity Entity(string key) { World.TryGetEntity(Id(key), out var entity); return entity; }
            public ActionReceipt Act(string target, ActionVerb verb, string point = null, string tool = null,
                string recipient = null, string actor = "player", double work = 1, bool complete = true, string supportedBy = null)
            {
                // Headless contact adapter for explicit synthetic fixtures, not production physics evidence.
                // Place both actor and held object at the authored contact point before submitting a sample.
                var position = Entity(target).Position;
                if (verb == ActionVerb.PutDown || verb == ActionVerb.DisposeWaste)
                {
                    if (recipient != null) position = Entity(recipient).Position;
                    World.SetPhysicalPose(Id(target), position);
                }
                World.SetActorPose(Id(actor), position);
                if (tool != null && Entity(tool).CustodianId.Equals(Id(actor))) World.SetPhysicalPose(Id(tool), position);
                World.AdvanceTime(new SimTick(World.Tick.Microseconds + 100000));
                var intent = World.CreateIntent(Id(actor), verb, Id(target), tool == null ? (StableId?)null : Id(tool),
                    recipient == null ? (StableId?)null : Id(recipient), point);
                var request = World.RequestAction(intent);
                Assert.AreNotEqual(ActionPhase.Blocked, request.Phase, request.Reason);
                var result = World.AdvanceAction(Id(actor), intent.IntentId,
                    new ActionEvidence(position, position, true, true, true, true, work, Entity(target).Revision,
                        supportedBy == null ? (StableId?)null : Id(supportedBy)));
                if (complete) Assert.AreEqual(ActionPhase.Completed, result.Phase, verb + " " + target + ": " + result.Reason);
                Training.Tick();
                return result;
            }
            public void Place(string target, string recipient = null)
            { Act(target, ActionVerb.PickUp); Act(target, ActionVerb.PutDown, recipient: recipient, supportedBy: recipient); }
            public void Cancel(string actor = "player")
            {
                var action = World.GetActiveIntent(Id(actor));
                if (action != null) World.CancelAction(Id(actor), action.IntentId, "test-interruption");
                Training.Tick();
            }
            public void Consent(string actor, string to)
            { Act(actor, ActionVerb.Consent, recipient: to, actor: actor); }
            public void Meet(string actor)
            { World.SetActorPose(Id(actor), Entity("destination").Position); }
            public void Dispose() { Training.Dispose(); World.Dispose(); }
        }

        [TestCase(RuleTruth.UNKNOWN)]
        [TestCase(RuleTruth.CONFLICTED)]
        public void UnknownApplicabilityNeverSkipsRequiredEvidence(RuleTruth truth)
        {
            var runner = new ProcedureRunner();
            var applies = RuleExpression.Fact(new StableId("applicable"));
            runner.LoadGameplay("scope", "scope", "explicit-test-source", 1, ActorRole.Maintenance,
                new[] { new GameplayProcedureStep("conditional", "conditional", "explicit-test-source", 1, ActorRole.Maintenance,
                    Array.Empty<string>(), applies, RuleExpression.Constant(RuleTruth.TRUE), RuleExpression.Constant(RuleTruth.TRUE), "manual missing") });
            runner.EvaluateGameplay(ProcedureRunner.FactsFrom(new Dictionary<string, RuleTruth> { ["applicable"] = truth }));
            Assert.IsFalse(runner.Completed);
            Assert.IsFalse(runner.GameplayStates[0].NotApplicable);
            Assert.IsFalse(runner.GameplayStates[0].Available);
            runner.EvaluateGameplay(ProcedureRunner.FactsFrom(new Dictionary<string, RuleTruth> { ["applicable"] = RuleTruth.FALSE }));
            Assert.IsTrue(runner.Completed, "Only explicitly FALSE may be skipped.");
        }

        [TestCase(RuleTruth.FALSE)]
        [TestCase(RuleTruth.UNKNOWN)]
        [TestCase(RuleTruth.CONFLICTED)]
        public void NonPracticeOrUnknownScopeCannotBecomeCompletedBySkippingEveryNode(RuleTruth truth)
        {
            var fixture = RoleTrainingFixtures.Create(new StableId("scope-test"), ActorRole.Maintenance);
            var entities = new Dictionary<StableId, WorldEntity>(fixture.Snapshot.Entities);
            var zone = entities[fixture.Bindings["zone"]];
            var scope = new Dictionary<string, RuleTruth>(zone.Facts) { ["practice.fixture"] = truth };
            entities[zone.Id] = zone.With(zone.Revision, facts: scope);
            var snapshot = new WorldSnapshot(fixture.Snapshot.RunId, 0, 0, new SimTick(0), GameplayMode.Tutorial,
                fixture.Snapshot.RulesetId, fixture.Snapshot.RulesetRevision, entities,
                new Dictionary<StableId, ActorState>(fixture.Snapshot.Actors));
            using (var world = new WorldSession(snapshot, new Journal()))
            using (var training = new RoleTrainingProgram())
            {
                training.Initialize(world, ActorRole.Maintenance, fixture.Bindings, null);
                Assert.IsFalse(training.PracticeCompleted);
                Assert.AreEqual(truth, training.PracticeScopeTruth);
                Assert.AreEqual(RuleTruth.UNKNOWN, training.CertificationStatus);
            }
        }

        [Test]
        public void RewindRestoresConsumptionCustodyObservationsAndGoalsAndRejectsOldGeneration()
        {
            using (var p = new Practice(ActorRole.Cleaning))
            {
                p.Act("cleaning-tool", ActionVerb.PickUp);
                p.Act("surface", ActionVerb.Observe);
                p.World.TryGetActor(p.Id("passenger"), out var passenger);
                var plan = new GoalPlan(new StableId("arrival-goal"), GoalKind.KeepSchedule, p.Id("destination"), 0,
                    "autonomous practice schedule", "destination.reached", new[] { ActionVerb.MoveTo });
                var memory = new ActorObservation(passenger.Id, p.Id("route"), p.Entity("route").Revision, p.World.Tick,
                    "passage.clear", RuleTruth.TRUE, ObservationSource.Sight);
                p.World.UpdateActor(passenger.With(plan: plan, replacePlan: true, knowledgeRevision: 1, memories: new[] { memory }), "fixture-observation");
                var checkpoint = p.Training.CaptureCheckpoint();
                double supply = p.Entity("cleaning-tool").Measurements["supply"].Value;
                var partial = p.Act("surface", ActionVerb.Clean, "main", "cleaning-tool", work: .5, complete: false);
                Assert.AreEqual(ActionPhase.Executing, partial.Phase);
                Assert.Less(p.Entity("cleaning-tool").Measurements["supply"].Value, supply);
                var abandonedIntent = p.World.GetActiveIntent(p.Id("player"));
                p.Cancel();
                p.Act("cleaning-tool", ActionVerb.PutDown, recipient: "laundry-bin", supportedBy: "laundry-bin");
                p.World.TryGetActor(passenger.Id, out passenger);
                p.World.UpdateActor(passenger.With(plan: null, replacePlan: true, memories: Array.Empty<ActorObservation>(), knowledgeRevision: 2), "changed-plan");
                p.Training.RestoreCheckpoint(checkpoint);
                Assert.AreEqual(checkpoint.World.Generation + 1, p.World.Generation);
                Assert.AreEqual(supply, p.Entity("cleaning-tool").Measurements["supply"].Value);
                Assert.AreEqual(1, p.Entity("surface").Measurements["soil:main"].Value);
                Assert.AreEqual(p.Id("player"), p.Entity("cleaning-tool").CustodianId);
                Assert.IsNull(p.Entity("cleaning-tool").ParentId);
                p.World.TryGetActor(passenger.Id, out passenger);
                Assert.AreEqual(plan.Id, passenger.Plan.Id);
                Assert.AreEqual("passage.clear", passenger.Memories[0].FactId);
                Assert.AreEqual(checkpoint.Observations.Count, p.Training.CompletionObservations.Count);
                Assert.AreEqual("stale_generation", p.World.RequestAction(abandonedIntent).Reason);
                Assert.IsNull(p.World.GetActiveIntent(p.Id("player")));
            }
        }

        [Test]
        public void StableCheckpointRejectsLivePartialWorkInsteadOfLosingTheAction()
        {
            using (var p = new Practice(ActorRole.Cleaning))
            {
                p.Act("cleaning-tool", ActionVerb.PickUp);
                p.Act("surface", ActionVerb.Clean, "main", "cleaning-tool", work: .25, complete: false);
                Assert.Throws<InvalidOperationException>(() => p.Training.CaptureCheckpoint());
                p.Cancel();
                var checkpoint = p.Training.CaptureCheckpoint();
                Assert.AreEqual(.75, checkpoint.World.Entities[p.Id("surface")].Measurements["soil:main"].Value);
            }
        }

        [Test]
        public void MaintenanceCompletesActualReplacementAndReopeningInvalidatesVerification()
        {
            using (var p = new Practice(ActorRole.Maintenance))
            {
                CompleteMaintenance(p);
                Assert.IsTrue(p.Training.PracticeCompleted, p.Training.BlockedReason);
                Assert.AreEqual(RuleTruth.UNKNOWN, p.Training.CertificationStatus);
                p.Act("driver", ActionVerb.PickUp);
                p.Act("new-part", ActionVerb.Unfasten, "a", "driver", work: .25, complete: false);
                p.Cancel();
                Assert.IsFalse(p.Training.PracticeCompleted);
                Assert.IsFalse(p.Training.Runner.Find("verify").Done);
                Assert.AreEqual(p.Id("socket"), p.Entity("new-part").ParentId, "Partial unfastening must not remove the installed part.");
                Assert.AreEqual(p.Id("maintainer"), p.Entity("work-order").CustodianId, "Past report handoff is preserved, not rewritten into current safety.");
            }
        }

        [Test]
        public void ReversingPartialFastenerWorkRequiresUndoingTheActualReleasedTravel()
        {
            using (var p = new Practice(ActorRole.Maintenance))
            {
                CompleteMaintenance(p);
                p.Act("driver", ActionVerb.PickUp);
                Assert.AreEqual(ActionPhase.Executing, p.Act("new-part", ActionVerb.Unfasten, "a", "driver", work: .5, complete: false).Phase);
                p.Cancel();
                Assert.AreEqual(ActionPhase.Executing, p.Act("new-part", ActionVerb.Fasten, "a", "driver", work: .01, complete: false).Phase);
                p.Cancel();
                var partial = p.Entity("new-part");
                Assert.AreEqual(.51, partial.Measurement("progress:fasten:a").Value.Value, 1e-9);
                Assert.AreEqual(.49, partial.Measurement("progress:unfasten:a").Value.Value, 1e-9);
                Assert.AreEqual(RuleTruth.FALSE, partial.Fact("fastened:a"));
                Assert.AreEqual(RuleTruth.FALSE, partial.Fact("practice.verified"));
                Assert.AreEqual(ActionPhase.Blocked, p.Act("new-part", ActionVerb.Verify, complete: false).Phase);
                p.Act("new-part", ActionVerb.Fasten, "a", "driver", work: .49);
                p.Act("new-part", ActionVerb.Verify);
                Assert.AreEqual(RuleTruth.TRUE, p.Entity("new-part").Fact("fastened:a"));
            }
        }

        [Test]
        public void CleaningCompletesContainedPracticeButNeverInventsDryingOrDisinfection()
        {
            using (var p = new Practice(ActorRole.Cleaning))
            {
                CompleteCleaning(p);
                Assert.IsTrue(p.Training.PracticeCompleted, p.Training.BlockedReason);
                Assert.AreEqual(RuleTruth.TRUE, p.Entity("surface").Fact("wet"));
                Assert.AreEqual(RuleTruth.UNKNOWN, p.Entity("surface").Fact("disinfected"));
                Assert.AreEqual(RuleTruth.UNKNOWN, p.Training.CertificationStatus);
                Assert.AreEqual(p.Id("bin"), p.Entity("waste").ParentId);
                Assert.IsNull(p.Entity("lost-item").ParentId);
                p.Act("sign", ActionVerb.PickUp);
                Assert.IsFalse(p.Training.PracticeCompleted, "Removing wet-area protection invalidates current completion.");
            }
        }

        [Test]
        public void ServiceRequiresRealRecipientAndPassengerArrivalAndCurrentSupplyCustody()
        {
            using (var p = new Practice(ActorRole.PassengerService))
            {
                p.Act("work-order", ActionVerb.Observe); p.Act("radio", ActionVerb.Observe); p.Act("radio", ActionVerb.PickUp);
                p.Act("service-supply", ActionVerb.Observe); p.Act("radio", ActionVerb.PutDown, recipient: "return-bin", supportedBy: "return-bin");
                p.Act("route", ActionVerb.Observe); p.Act("passenger", ActionVerb.RequestHelp); p.Consent("passenger", "player");
                p.Act("service-supply", ActionVerb.PickUp);
                p.World.SetPhysicalPose(p.Id("service-supply"), p.Entity("passenger").Position);
                p.Act("service-supply", ActionVerb.Handoff, recipient: "passenger");
                p.Act("passenger", ActionVerb.Observe); p.Act("passenger", ActionVerb.Report); p.Act("destination", ActionVerb.Observe);
                p.Act("passenger", ActionVerb.Escort);
                p.Act("facility", ActionVerb.Observe); p.Act("facility", ActionVerb.Report); p.Act("facility", ActionVerb.RequestHelp);
                p.Meet("passenger"); p.Consent("passenger", "colleague"); p.Consent("colleague", "player");
                var notArrived = p.Act("passenger", ActionVerb.Handoff, recipient: "colleague", complete: false);
                Assert.AreEqual(ActionPhase.Blocked, notArrived.Phase);
                Assert.IsFalse(p.Training.PracticeCompleted);
                p.Meet("colleague"); p.Act("passenger", ActionVerb.Handoff, recipient: "colleague");
                p.Act("work-order", ActionVerb.Report);
                Assert.IsTrue(p.Training.PracticeCompleted, p.Training.BlockedReason);
                p.World.SetPhysicalPose(p.Id("service-supply"), p.Entity("destination").Position);
                p.Act("service-supply", ActionVerb.PutDown, actor: "passenger");
                Assert.IsFalse(p.Training.PracticeCompleted, "A historical handoff does not override the wrong current custody.");
            }
        }

        [Test]
        public void StationRecordsWrongVerdictWithoutRepairingRealityAndCompletesPhysicalHandback()
        {
            using (var p = new Practice(ActorRole.StationStaff))
            {
                foreach (var point in new[] { "serial", "spec", "gauge", "body" }) p.Act("facility", ActionVerb.Observe, point);
                p.Act("facility", ActionVerb.Report, "fit"); p.Act("facility", ActionVerb.RequestHelp);
                p.Act("tag", ActionVerb.PickUp); p.Act("tag-socket", ActionVerb.Install, tool: "tag");
                p.Act("terminal", ActionVerb.Observe); p.Act("facility", ActionVerb.Report); p.Act("facility", ActionVerb.Observe, "body");
                p.Act("lost-item", ActionVerb.Observe); p.Act("lost-item", ActionVerb.Report); p.Act("lost-item", ActionVerb.RequestHelp);
                p.Place("cart"); p.Act("route", ActionVerb.Observe); p.Act("destination", ActionVerb.Observe);
                p.Act("sign", ActionVerb.PickUp); p.Act("cordon-zone", ActionVerb.Cordon, tool: "sign");
                p.Act("passenger", ActionVerb.RequestHelp); p.Consent("passenger", "player"); p.Act("passenger", ActionVerb.Escort);
                p.Meet("passenger"); p.Consent("passenger", "colleague"); p.Consent("colleague", "player"); p.Meet("colleague");
                p.Act("passenger", ActionVerb.Handoff, recipient: "colleague"); p.Act("terminal", ActionVerb.Report);
                Assert.IsTrue(p.Training.PracticeCompleted, p.Training.BlockedReason);
                Assert.IsTrue(p.Training.HasMisreportedFacility);
                Assert.AreEqual(RuleTruth.TRUE, p.Entity("facility").Fact("corroded"));
                Assert.AreEqual(RuleTruth.UNKNOWN, p.Training.CertificationStatus);
                Assert.AreEqual(p.Id("tag-socket"), p.Entity("tag").ParentId);
            }
        }

        private static void CompleteMaintenance(Practice p)
        {
            p.Act("work-order", ActionVerb.Observe); p.Act("old-part", ActionVerb.Observe); p.Act("new-part", ActionVerb.Observe);
            p.Act("socket", ActionVerb.Isolate); p.Act("driver", ActionVerb.Observe); p.Act("meter", ActionVerb.Observe); p.Place("tray");
            p.Act("driver", ActionVerb.PickUp); p.Act("old-part", ActionVerb.Unfasten, "a", "driver"); p.Act("old-part", ActionVerb.Unfasten, "b", "driver");
            p.Act("driver", ActionVerb.PutDown, recipient: "tray", supportedBy: "tray"); p.Act("old-part", ActionVerb.Remove); p.Act("old-part", ActionVerb.PutDown, recipient: "tray", supportedBy: "tray");
            p.Act("old-part", ActionVerb.Observe, "contact"); p.Act("meter", ActionVerb.PickUp); p.Act("old-part", ActionVerb.Measure, "main", "meter");
            p.Act("meter", ActionVerb.PutDown, recipient: "tray", supportedBy: "tray"); p.Act("new-part", ActionVerb.PickUp); p.Act("socket", ActionVerb.Install, tool: "new-part");
            p.Act("driver", ActionVerb.PickUp); p.Act("new-part", ActionVerb.Fasten, "a", "driver"); p.Act("new-part", ActionVerb.Fasten, "b", "driver");
            p.Act("new-part", ActionVerb.Verify); p.Act("driver", ActionVerb.PutDown, recipient: "tray", supportedBy: "tray"); p.Act("work-order", ActionVerb.Report);
            p.Consent("maintainer", "player"); p.Act("work-order", ActionVerb.PickUp);
            p.World.SetPhysicalPose(p.Id("work-order"), p.Entity("maintainer").Position); p.Act("work-order", ActionVerb.Handoff, recipient: "maintainer");
        }
        private static void CompleteCleaning(Practice p)
        {
            p.Act("zone", ActionVerb.Observe); p.Act("lost-item", ActionVerb.Observe); p.Act("lost-item", ActionVerb.Report); p.Act("lost-item", ActionVerb.RequestHelp);
            p.Place("cart"); p.Place("sign", "surface"); p.Act("route", ActionVerb.Observe);
            p.Act("waste", ActionVerb.PickUp); p.Act("waste", ActionVerb.DisposeWaste, recipient: "bin");
            p.Act("cleaning-tool", ActionVerb.PickUp); p.Act("surface", ActionVerb.Clean, "main", "cleaning-tool", complete: false); p.Cancel();
            p.Act("surface", ActionVerb.Clean, "edge", "cleaning-tool"); p.Act("cleaning-tool", ActionVerb.PutDown, recipient: "laundry-bin", supportedBy: "laundry-bin");
            p.Act("supply", ActionVerb.PickUp); p.Act("dispenser", ActionVerb.Refill, tool: "supply"); p.Act("supply", ActionVerb.PutDown);
            p.Act("surface", ActionVerb.Observe); p.Act("surface", ActionVerb.Report); p.Act("surface", ActionVerb.RequestHelp);
            p.Act("bin", ActionVerb.Report); p.Act("laundry-bin", ActionVerb.Report);
            p.Act("work-order", ActionVerb.Report); p.Consent("colleague", "player"); p.Act("work-order", ActionVerb.PickUp);
            p.World.SetPhysicalPose(p.Id("work-order"), p.Entity("colleague").Position);
            p.Act("work-order", ActionVerb.Handoff, recipient: "colleague");
        }
    }
}
