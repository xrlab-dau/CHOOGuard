#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps.Runtime;
using ChooGuard.Application.Gameplay.Content;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class GameplayPracticeLaboratoryTests
    {
        private GameObject world;

        [TearDown]
        public void TearDown()
        {
            if (world != null) Object.DestroyImmediate(world);
        }

        [UnityTest]
        public IEnumerator FiveHundredthAuthoredPersonHasPhysicalFloorAndConnectedRoute()
        {
            var seed = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Gameplay/practice-seed.json");
            var rules = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Gameplay/atomic-transitions.json");
            var content = GameplayContentLoader.Load(seed.text, rules.text);
            var snapshot = GameplayInitialWorld.Create(content.Seed, 500, GameplayMode.RandomOperationsLab);
            world = new GameObject("Population footprint fixture");
            var navigation = world.AddComponent<GameplayPracticeLaboratory>().Build(snapshot, null);
            var first = snapshot.Entities[GameplayInitialWorld.HumanId].Position;
            var last = snapshot.Entities[new StableId("npc-0499")].Position;
            var source = new Vector3(first.X, first.Y, first.Z);
            var destination = new Vector3(last.X, last.Y, last.Z);
            var route = new List<Vector3>();
            Assert.That(navigation.TryRoute(source, destination, route, out var reason), Is.True, reason);
            Assert.That(Vector3.Distance(route[route.Count - 1], destination), Is.LessThan(.1f));
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(destination + Vector3.up, Vector3.down, out var hit, 1.1f), Is.True);
            Assert.That(hit.point.y, Is.EqualTo(last.Y).Within(.01f));
            yield return null;
        }
    }
}
#endif
