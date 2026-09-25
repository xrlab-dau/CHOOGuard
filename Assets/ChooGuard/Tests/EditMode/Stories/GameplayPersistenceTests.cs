#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using ChooGuard.App.Fps.Runtime;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using ChooGuard.Persistence;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class GameplayPersistenceTests
    {
        [Test]
        public void ActorlessCommitsReplayWhilePrivateKnowledgeRemainsTheOwnersLastObservation()
        {
            string package = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../workers/runtime"));
            if (!File.Exists(Path.Combine(package, "runtime-manifest.json"))) Assert.Ignore("NOT_VERIFIED: materialized native runtime package required.");
            RuntimePackage.ConfigureRoot(package);
            string directory = Path.Combine(Path.GetTempPath(), "cg 게임 저장 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var human = new StableId("human"); var npc = new StableId("npc"); var surface = new StableId("surface"); var zone = new StableId("zone");
            var entities = new Dictionary<StableId, WorldEntity>
            {
                [human] = new WorldEntity(human, "사람", EntityKind.Actor, 0, new WorldPoint(0, 0, 0), zone),
                [npc] = new WorldEntity(npc, "동료", EntityKind.Actor, 0, new WorldPoint(0, 0, 1), zone),
                [surface] = new WorldEntity(surface, "관측 표면", EntityKind.Surface, 0, new WorldPoint(1, 0, 1), zone,
                    facts: new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE, ["wet"] = RuleTruth.FALSE })
            };
            var initial = new WorldSnapshot(new StableId("save-" + Guid.NewGuid().ToString("N")), 0, 0, new SimTick(0),
                GameplayMode.RandomOperationsLab, "persistence-test", 1, entities, new Dictionary<StableId, ActorState>
                {
                    [human] = new ActorState(human, ActorRole.Maintenance, 0, 0, 0, true, ""),
                    [npc] = new ActorState(npc, ActorRole.Cleaning, 0, 0, 0, false, "")
                });
            var kernel = new TransitionKernel(new[] { new TransitionDefinition("test-wet", "declared-test", "명시적 시험 변화",
                EntityKind.Surface, EntityKind.Surface, TransitionRelation.Self,
                new Dictionary<string, RuleTruth> { ["dirty"] = RuleTruth.TRUE },
                new Dictionary<string, RuleTruth> { ["wet"] = RuleTruth.FALSE },
                new Dictionary<string, RuleTruth> { ["wet"] = RuleTruth.TRUE }) });
            long generation;
            string database = Path.Combine(directory, "세계.sqlite");
            try
            {
                using (var store = new GameplayPersistenceStore(database))
                using (var world = store.Open(initial))
                {
                    generation = world.Generation;
                    world.ChangeRole(human, ActorRole.StationStaff);
                    world.TryGetActor(npc, out var actor);
                    world.UpdateActor(new NpcPlanner(npc).Observe(actor, entities[surface], world.Tick, ObservationSource.Sight), "local_observation");
                    var basis = world.Snapshot();
                    var candidate = kernel.BuildEnvironmentCandidates(basis)[0];
                    Assert.That(world.CommitEnvironment(kernel, basis, candidate, "test-transition", new string('a', 64), out var reason), Is.True, reason);
                    world.SetActorPose(human, new WorldPoint(2, 0, 3));
                    store.Save(world);
                }
                using (var store = new GameplayPersistenceStore(database))
                using (var world = store.Open(initial))
                {
                    Assert.That(world.Generation, Is.GreaterThan(generation));
                    world.TryGetActor(human, out var player); Assert.That(player.Role, Is.EqualTo(ActorRole.StationStaff));
                    world.TryGetEntity(human, out var body); Assert.That(body.Position.DistanceSquared(new WorldPoint(2, 0, 3)), Is.EqualTo(0));
                    world.TryGetEntity(surface, out var actual); Assert.That(actual.Fact("wet"), Is.EqualTo(RuleTruth.TRUE));
                    world.TryGetActor(npc, out var actor);
                    var restored = new NpcPlanner(npc);
                    Assert.That(restored.KnownFact(actor, surface, "wet"), Is.EqualTo(RuleTruth.FALSE));
                    Assert.That(restored.FormGoal(actor).Plan.Kind, Is.EqualTo(GoalKind.RestoreCleanliness));
                    store.Save(world);
                }
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
#endif
