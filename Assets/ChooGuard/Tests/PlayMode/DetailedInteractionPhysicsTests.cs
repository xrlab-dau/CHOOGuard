using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Runtime;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class DetailedInteractionPhysicsTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private WorldSession session;
        private FirstPersonResponder responder;
        private DetailedInteractionController controller;
        private NpcAgentRuntime npcRuntime;
        private GameplayInferenceClient inference;
        private readonly StableId actorId = new StableId("physical-player");
        private readonly StableId zoneId = new StableId("physical-zone");
        private sealed class Journal : IGameplayCommitSink
        {
            public readonly List<WorldMutation> Records = new List<WorldMutation>();
            public void Commit(WorldMutation mutation) => Records.Add(mutation);
        }
        private GameObject Cube(string name, Vector3 position, Vector3 size)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(item);
            item.name = name; item.transform.position = position; item.transform.localScale = size; return item;
        }
        private FpsEntityBinding Portable(string id, Vector3 position, Vector3 size)
        {
            var item = Cube(id, position, size);
            var body = item.AddComponent<Rigidbody>(); body.useGravity = false; body.mass = 1;
            var binding = item.AddComponent<FpsEntityBinding>();
            binding.Configure(id, item.GetComponent<Collider>(), body); binding.HandReach = 3;
            return binding;
        }
        private WorldEntity Entity(FpsEntityBinding binding, EntityKind kind, bool held = false,
            Dictionary<string, RuleTruth> facts = null, Dictionary<string, SiValue> values = null)
        {
            var p = binding.transform.position;
            return new WorldEntity(new StableId(binding.EntityId), binding.EntityId, kind, 0,
                new WorldPoint(p.x, p.y, p.z), zoneId, held ? actorId : (StableId?)null, definitionId: binding.EntityId,
                facts: facts, measurements: values);
        }
        private void StartWorld(params WorldEntity[] entities)
        {
            var player = new GameObject("Physical test player"); objects.Add(player);
            responder = player.AddComponent<FirstPersonResponder>();
            responder.SetExternalInputMode(true); responder.Resume(false);
            controller = player.AddComponent<DetailedInteractionController>();
            var world = new Dictionary<StableId, WorldEntity>
            {
                [actorId] = new WorldEntity(actorId, "Player", EntityKind.Actor, 0, new WorldPoint(0, 0, 0), zoneId),
                [zoneId] = new WorldEntity(zoneId, "Zone", EntityKind.Zone, 0, new WorldPoint(0, 0, 0), zoneId)
            };
            var actors = new Dictionary<StableId, ActorState>
            { [actorId] = new ActorState(actorId, ActorRole.Cleaning, 0, 0, 0, true, "test") };
            foreach (var entity in entities)
            {
                world.Add(entity.Id, entity);
                if (entity.Kind == EntityKind.Actor) actors.Add(entity.Id, new ActorState(entity.Id, ActorRole.Citizen, 0, 0, 0, false, "test"));
            }
            session = new WorldSession(new WorldSnapshot(new StableId("physical-test"), 0, 0, new SimTick(0), GameplayMode.Tutorial,
                "physical-test", 1, world, actors), new Journal());
            controller.Bind(session, actorId, responder);
        }
        private ActorNavigationBinding NpcBody(string id, Vector3 position)
        {
            var body = new GameObject(id); objects.Add(body); body.transform.position = position;
            var motion = body.AddComponent<ActorNavigationBinding>(); motion.ActorId = id;
            return motion;
        }
        private void StartNpcs(GameplayNavigation navigation, params ActorNavigationBinding[] actors)
        {
            var host = new GameObject("Physical NPC runtime"); objects.Add(host);
            npcRuntime = host.AddComponent<NpcAgentRuntime>();
            inference = new GameplayInferenceClient();
            npcRuntime.Initialize(session, inference, navigation, actors);
            npcRuntime.RegisterHumanObserver(actorId, responder.transform);
        }
        [TearDown]
        public void TearDown()
        {
            if (npcRuntime != null) Object.DestroyImmediate(npcRuntime.gameObject);
            inference?.Dispose(); inference = null;
            if (controller != null) { controller.CancelActive("test teardown"); controller.Session = null; }
            if (session != null) { session.Dispose(); session = null; }
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
        }

        [UnityTest]
        public IEnumerator CarryCannotPullBodyThroughWallAndCancelKeepsCustody()
        {
            var item = Portable("carried", new Vector3(0, 1.6f, .8f), Vector3.one * .2f);
            var wall = Cube("wall", new Vector3(0, 1.5f, 1.3f), new Vector3(4, 3, .15f));
            StartWorld(Entity(item, EntityKind.Tool, true));
            for (int i = 0; i < 100; i++)
            {
                controller.StepManipulation(Vector2.zero, .016f, 0, true, false, .02f);
                yield return new WaitForFixedUpdate();
            }
            Assert.LessOrEqual(item.ContactSurface.bounds.max.z, wall.GetComponent<Collider>().bounds.min.z + .025f);
            Assert.Greater(item.Body.position.z, .85f, "A stationary object is not proof of collision-respecting carry.");
            controller.CancelActive("cancel carry input");
            Assert.AreSame(item, controller.HeldEntity);
            Assert.IsTrue(session.TryGetEntity(new StableId(item.EntityId), out var state));
            Assert.AreEqual(actorId, state.CustodianId.Value);
        }

        [UnityTest]
        public IEnumerator PlacementRequiresSupportAndRejectsPenetration()
        {
            var item = Portable("placement", new Vector3(0, .2f, 0), Vector3.one * .2f);
            var floor = Cube("support", new Vector3(0, -.05f, 0), new Vector3(3, .1f, 3));
            var queries = new FpsPhysicalQueries();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(queries.CanPlace(item, null, out _, out _), "Floating above a surface is not supported placement.");
            item.Body.position = new Vector3(0, .1f, 0);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(queries.CanPlace(item, null, out var reason, out _), reason);
            Cube("obstacle", new Vector3(.08f, .1f, 0), Vector3.one * .15f);
            Physics.SyncTransforms();
            Assert.IsFalse(queries.CanPlace(item, null, out _, out _), "A support ray must not hide body overlap.");
            Assert.IsNotNull(floor);
        }

        [UnityTest]
        public IEnumerator NamedPlacementRejectsNearbyFloorUntilTheActualRecipientSupportsTheItem()
        {
            var item = Portable("returned-tool", new Vector3(0, 1.6f, .8f), Vector3.one * .2f);
            var floor = Cube("wrong-floor", new Vector3(0, 1.45f, .8f), new Vector3(.4f, .1f, .4f));
            var trayObject = Cube("return-tray", new Vector3(.6f, 1.45f, .8f), new Vector3(.4f, .1f, .4f));
            var tray = trayObject.AddComponent<FpsEntityBinding>();
            tray.Configure("return-tray", trayObject.GetComponent<Collider>());
            item.Verb = ActionVerb.PutDown; item.RecipientId = tray.EntityId;
            StartWorld(Entity(item, EntityKind.Tool, true), Entity(tray, EntityKind.Container));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(controller.Begin(item, out var reason), reason);
            controller.StepManipulation(Vector2.zero, 0, 0, false, false, .02f);
            session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
            controller.StepManipulation(Vector2.zero, 0, 0, false, true, .02f);
            yield return new WaitForFixedUpdate();
            session.TryGetEntity(new StableId(item.EntityId), out var wrongSupport);
            Assert.AreEqual(actorId, wrongSupport.CustodianId);
            Assert.IsNull(wrongSupport.ParentId, "A nearby tray cannot receive credit for floor support.");
            Assert.AreSame(item, controller.HeldEntity);

            floor.transform.position += Vector3.left;
            trayObject.transform.position = new Vector3(0, 1.45f, .8f);
            Physics.SyncTransforms();
            session.SetPhysicalPose(new StableId(tray.EntityId), new WorldPoint(0, 1.45f, .8f));
            controller.CancelActive("support geometry changed");
            Assert.IsTrue(controller.Begin(item, out reason), reason);
            controller.StepManipulation(Vector2.zero, 0, 0, false, false, .02f);
            session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
            controller.StepManipulation(Vector2.zero, 0, 0, false, true, .02f);
            yield return new WaitForFixedUpdate();
            session.TryGetEntity(new StableId(item.EntityId), out var returned);
            Assert.AreEqual(ActionPhase.Completed, controller.LastReceipt.Phase, controller.LastReceipt.Reason);
            Assert.IsNull(returned.CustodianId);
            Assert.AreEqual(new StableId(tray.EntityId), returned.ParentId);
            Assert.IsNull(controller.HeldEntity);
        }

        [UnityTest]
        public IEnumerator HandoffAndRestoredCustodyMoveTheItemWithOnlyTheReceivingNpc()
        {
            var receiverId = new StableId("receiving-npc");
            var receiver = NpcBody(receiverId.Value, new Vector3(0, 0, 1.4f));
            var item = Portable("handed-tool", new Vector3(0, 1.6f, .8f), Vector3.one * .2f);
            var dependent = Portable("escorted-person", new Vector3(.8f, 1.1f, -.8f), Vector3.one * .2f);
            Vector3 dependentPosition = dependent.Body.position;
            item.Verb = ActionVerb.Handoff; item.RecipientId = receiverId.Value;
            StartWorld(Entity(item, EntityKind.Tool, true),
                new WorldEntity(receiverId, "Receiver", EntityKind.Actor, 0, new WorldPoint(0, 0, 1.4f), zoneId,
                    facts: new Dictionary<string, RuleTruth> { ["consent:" + actorId.Value] = RuleTruth.TRUE }),
                Entity(dependent, EntityKind.Actor).With(0, custodianId: receiverId, replaceCustodian: true, parentId: receiverId, replaceParent: true));
            StartNpcs(null, receiver);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(controller.Begin(item, out var reason), reason);
            controller.StepManipulation(Vector2.zero, 0, 0, false, false, .02f);
            session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
            controller.StepManipulation(Vector2.zero, 0, 0, false, true, .02f);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(ActionPhase.Completed, controller.LastReceipt.Phase, controller.LastReceipt.Reason);
            Assert.IsNull(controller.HeldEntity, "The sender must release the committed transfer.");
            var checkpoint = session.Snapshot();

            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    session.Restore(checkpoint);
                    yield return new WaitForFixedUpdate();
                }
                Vector3 before = item.Body.position;
                for (int i = 0; i < 120; i++)
                {
                    receiver.GetComponent<CharacterController>().Move(Vector3.right * .01f);
                    session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
                    npcRuntime.TickAgents(.02f);
                    yield return new WaitForFixedUpdate();
                }
                Assert.Greater(item.Body.position.x, before.x + .8f, "Committed/restored NPC custody must drive the actual body.");
                Vector3 hand = receiver.transform.position + Vector3.up * 1.1f + receiver.transform.forward * .25f;
                Assert.Less(Vector3.Distance(item.Body.position, hand), .65f);
                Assert.Less(Vector3.Distance(dependent.Body.position, dependentPosition), .02f, "Escort responsibility must never grip a person's body.");
                session.TryGetEntity(new StableId(item.EntityId), out var carried);
                Assert.AreEqual(receiverId, carried.CustodianId);
                Assert.IsNull(controller.HeldEntity);
            }
        }

        [UnityTest]
        public IEnumerator HumanRecipientAdoptsCommittedItemAndCanPhysicallyCarryIt()
        {
            var senderId = new StableId("npc-sender");
            var sender = NpcBody(senderId.Value, new Vector3(.6f, 0, 0));
            var item = Portable("received-tool", new Vector3(.6f, 1.1f, .6f), Vector3.one * .15f);
            StartWorld(Entity(item, EntityKind.Tool).With(0, custodianId: senderId, replaceCustodian: true),
                new WorldEntity(senderId, "Sender", EntityKind.Actor, 0, new WorldPoint(.6f, 0, 0), zoneId));
            StartNpcs(null, sender);
            yield return new WaitForFixedUpdate();
            Assert.IsNull(controller.HeldEntity);

            // Typed headless social/contact fixture; the receiving grip and subsequent motion use real adapters/PhysX.
            session.TryGetEntity(actorId, out var human);
            var consent = session.CreateIntent(actorId, ActionVerb.Consent, actorId, recipientId: senderId);
            Assert.AreEqual(ActionPhase.Reserved, session.RequestAction(consent).Phase);
            session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
            Assert.AreEqual(ActionPhase.Completed, session.AdvanceAction(actorId, consent.IntentId,
                new ActionEvidence(human.Position, human.Position, true, true, false, true, 1, human.Revision)).Phase);
            session.TryGetEntity(senderId, out var npc);
            session.TryGetEntity(new StableId(item.EntityId), out var held);
            var handoff = session.CreateIntent(senderId, ActionVerb.Handoff, held.Id, recipientId: actorId);
            Assert.AreEqual(ActionPhase.Reserved, session.RequestAction(handoff).Phase);
            session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
            Assert.AreEqual(ActionPhase.Completed, session.AdvanceAction(senderId, handoff.IntentId,
                new ActionEvidence(npc.Position, held.Position, true, true, true, true, 1, held.Revision)).Phase);
            Assert.AreSame(item, controller.HeldEntity);

            float before = item.Body.position.z;
            controller.StepManipulation(Vector2.zero, 0, 0, false, false, .02f);
            for (int i = 0; i < 80; i++)
            {
                controller.StepManipulation(Vector2.zero, .016f, 0, true, false, .02f);
                session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
                npcRuntime.TickAgents(.02f);
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(item.Body.position.z, before + .6f, "Receipt alone is not proof that the human can carry the received body.");
            session.TryGetEntity(held.Id, out var carried);
            Assert.AreEqual(actorId, carried.CustodianId);
            Assert.AreSame(item, controller.HeldEntity);
        }

        [UnityTest]
        public IEnumerator EscortStopsWhenLeaderIsOccludedOrOutOfRangeAndReacquiresVisibleLeader()
        {
            var followerId = new StableId("escort-follower");
            var follower = NpcBody(followerId.Value, new Vector3(0, .05f, 0));
            var floor = Cube("escort-floor", new Vector3(0, -.1f, 3), new Vector3(20, .2f, 20));
            var navigationObject = new GameObject("Escort navigation"); objects.Add(navigationObject);
            var navigation = navigationObject.AddComponent<GameplayNavigation>();
            const string revision = "physical-escort-fixture";
            navigation.BeginGeometry(revision);
            var baked = GameplayNavigationBaker.BakeGeometry(null, new[] { floor.GetComponent<MeshFilter>() }, out _);
            Assert.IsTrue(navigation.RegisterFloor(new FloorNavigationBinding { FloorId = "escort-floor", GeometryRevision = revision },
                baked, out var reason), reason);
            StartWorld(new WorldEntity(followerId, "Follower", EntityKind.Actor, 0, new WorldPoint(0, .05f, 0), zoneId,
                custodianId: actorId, parentId: actorId,
                facts: new Dictionary<string, RuleTruth> { ["consent:" + actorId.Value] = RuleTruth.TRUE }));
            responder.RestorePhysicalPose(new Vector3(0, .05f, 6), 0, 0);
            StartNpcs(navigation, follower);
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++)
            {
                npcRuntime.TickAgents(.02f);
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(follower.HasDestination, follower.BlockedReason);
            Assert.Greater(follower.transform.position.z, .02f);
            Vector3 observedDestination = follower.Destination;

            var wall = Cube("escort-occluder", new Vector3(0, 1.5f, 3), new Vector3(4, 3, .2f));
            responder.RestorePhysicalPose(new Vector3(1, .05f, 6), 0, 0);
            Physics.SyncTransforms();
            npcRuntime.TickAgents(.02f);
            Assert.IsFalse(follower.HasDestination, "A hidden leader must not be tracked through the wall.");
            Assert.AreEqual(observedDestination, follower.Destination);
            Vector3 stopped = follower.transform.position;
            for (int i = 0; i < 5; i++) { npcRuntime.TickAgents(.02f); yield return new WaitForFixedUpdate(); }
            Assert.Less(Vector3.Distance(stopped, follower.transform.position), .01f);

            wall.SetActive(false); Physics.SyncTransforms();
            npcRuntime.TickAgents(.02f);
            Assert.IsTrue(follower.HasDestination, "Reacquiring actual sight must resume escort.");
            Assert.Greater(follower.Destination.x, .5f);
            npcRuntime.SightDistance = 3;
            npcRuntime.TickAgents(.02f);
            Assert.IsFalse(follower.HasDestination, "Unoccluded but distant leaders are not locally perceived.");
            npcRuntime.SightDistance = 10;
            npcRuntime.TickAgents(.02f);
            Assert.IsTrue(follower.HasDestination);
        }

        [UnityTest]
        public IEnumerator SaturatedRayAndOriginInsideWallFailClosed()
        {
            var targetObject = Cube("target", new Vector3(0, 1, 3), Vector3.one * .1f);
            var target = targetObject.AddComponent<FpsEntityBinding>(); target.Configure("target", targetObject.GetComponent<Collider>());
            for (int i = 0; i < 130; i++) Cube("occluder", new Vector3(0, 1, .2f + i * .02f), new Vector3(.1f, .1f, .008f));
            Physics.SyncTransforms();
            var queries = new FpsPhysicalQueries();
            Assert.IsFalse(queries.Visible(new Vector3(0, 1, 0), new Vector3(0, 1, 2.95f), null, target));
            Assert.IsTrue(queries.Saturated);
            Assert.IsFalse(queries.Visible(new Vector3(0, 1, .2f), new Vector3(0, 1, 2.95f), null, target));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RequestDoesNotInstantlyPickUpAndFocusCancellationDoesNotTransfer()
        {
            var item = Portable("pickup", new Vector3(0, 1.6f, .8f), Vector3.one * .2f);
            item.Verb = ActionVerb.PickUp;
            StartWorld(Entity(item, EntityKind.Tool, facts: new Dictionary<string, RuleTruth>
            { ["portable"] = RuleTruth.TRUE, ["installed"] = RuleTruth.FALSE, ["hazardous"] = RuleTruth.FALSE }));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(controller.Begin(item, out var reason), reason);
            Assert.AreEqual(ActionPhase.Reserved, controller.LastReceipt.Phase);
            Assert.IsTrue(session.TryGetEntity(new StableId(item.EntityId), out var requested));
            Assert.IsFalse(requested.CustodianId.HasValue);
            controller.InputConsumed = true;
            yield return new WaitForFixedUpdate();
            Assert.IsNull(controller.ActiveIntent);
            Assert.IsTrue(session.TryGetEntity(new StableId(item.EntityId), out var cancelled));
            Assert.IsFalse(cancelled.CustodianId.HasValue);
        }

        [UnityTest]
        public IEnumerator CancellingContactStrokePreservesOnlyWorkedRegionAndConsumption()
        {
            var cloth = Portable("cloth", new Vector3(-.2f, 1.085f, .9f), Vector3.one * .04f);
            cloth.ToolHead = cloth.transform; cloth.ToolHeadRadius = .04f;
            cloth.Body.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            var surfaceObject = Cube("clean-region", new Vector3(0, 1, .9f), new Vector3(.8f, .1f, .8f));
            var surface = surfaceObject.AddComponent<FpsEntityBinding>();
            surface.Configure("clean-region", surfaceObject.GetComponent<Collider>());
            surface.HandReach = 2; surface.Verb = ActionVerb.Clean; surface.ToolId = "cloth"; surface.WorkPointId = "left";
            surface.WorkPoints = new[]
            {
                new FpsEntityBinding.WorkPoint
                {
                    Id = "left", Surface = surface.ContactSurface, Frame = surface.transform,
                    CompatibleToolDefinition = "cloth", ContactRadius = .04f, Axis = Vector3.up,
                    AlignmentDegrees = 15, StrokeMetres = 5
                }
            };
            StartWorld(Entity(cloth, EntityKind.Tool, true,
                    new Dictionary<string, RuleTruth> { ["tool.cleaning"] = RuleTruth.TRUE, ["contaminated"] = RuleTruth.FALSE },
                    new Dictionary<string, SiValue> { ["supply"] = new SiValue(.001, SiUnit.CubicMetre), ["consumption.per-work"] = new SiValue(.00001, SiUnit.CubicMetre) }),
                Entity(surface, EntityKind.Surface, facts: new Dictionary<string, RuleTruth>
                    { ["dirty"] = RuleTruth.TRUE, ["safe.material.known"] = RuleTruth.TRUE },
                    values: new Dictionary<string, SiValue>
                    { ["soil:left"] = new SiValue(1, SiUnit.Dimensionless), ["soil:right"] = new SiValue(1, SiUnit.Dimensionless) }));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(controller.Begin(surface, out var reason), reason);
            controller.StepManipulation(Vector2.zero, 0, 0, false, false, .02f);
            for (int i = 0; i < 24; i++)
            {
                session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
                controller.StepManipulation(new Vector2(.005f, 0), 0, 0, true, false, .02f);
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(session.TryGetEntity(new StableId(surface.EntityId), out var worked));
            double residue = worked.Measurement("soil:left").Value.Value;
            Assert.That(residue, Is.GreaterThan(0).And.LessThan(1), controller.LastFeedback);
            Assert.AreEqual(1, worked.Measurement("soil:right").Value.Value);
            Assert.IsTrue(session.TryGetEntity(new StableId(cloth.EntityId), out var consumed));
            double supply = consumed.Measurement("supply").Value.Value;
            Assert.Less(supply, .001);
            controller.CancelActive("release partially cleaned region");
            session.AdvanceTime(new SimTick(session.Tick.Microseconds + 20000));
            yield return new WaitForFixedUpdate();
            session.TryGetEntity(new StableId(surface.EntityId), out var after);
            session.TryGetEntity(new StableId(cloth.EntityId), out var retained);
            Assert.AreEqual(residue, after.Measurement("soil:left").Value.Value);
            Assert.AreEqual(supply, retained.Measurement("supply").Value.Value);
            Assert.AreEqual(actorId, retained.CustodianId.Value);
            Assert.AreSame(cloth, controller.HeldEntity);
        }

        [UnityTest]
        public IEnumerator TutorialRestoreRestoresRigidPoseViewAndHeldCustody()
        {
            var item = Portable("checkpoint-item", new Vector3(0, 1.6f, .8f), Vector3.one * .2f);
            StartWorld(Entity(item, EntityKind.Tool, true));
            yield return new WaitForFixedUpdate();
            var world = session.Snapshot();
            var physical = controller.CapturePhysicalCheckpoint();
            Vector3 bodyPosition = item.Body.position;
            Quaternion bodyRotation = item.Body.rotation;
            Vector3 playerPosition = responder.transform.position;
            float yaw = responder.YawDegrees, pitch = responder.PitchDegrees;
            item.Body.position += Vector3.right;
            item.Body.rotation = Quaternion.Euler(20, 50, 70);
            responder.RestorePhysicalPose(new Vector3(2, 0, 1), 100, 30);
            session.Restore(world);
            controller.RestorePhysicalCheckpoint(physical);
            Assert.Less(Vector3.Distance(bodyPosition, item.Body.position), .0001f);
            Assert.Less(Quaternion.Angle(bodyRotation, item.Body.rotation), .001f);
            Assert.Less(Vector3.Distance(playerPosition, responder.transform.position), .0001f);
            Assert.AreEqual(yaw, responder.YawDegrees);
            Assert.AreEqual(pitch, responder.PitchDegrees);
            Assert.AreSame(item, controller.HeldEntity);
            Assert.IsNull(controller.ActiveIntent);
            session.TryGetEntity(new StableId(item.EntityId), out var retained);
            Assert.AreEqual(actorId, retained.CustodianId.Value);
        }
    }
}
