#if UNITY_INCLUDE_TESTS
using System.Collections;
using ChooGuard.App.Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class GameplayVisualLifecycleTests
    {
        private GameObject world;

        [TearDown]
        public void TearDown()
        {
            if (world != null) Object.DestroyImmediate(world);
        }

        [UnityTest]
        public IEnumerator SuspendedWorldAndRemovedActorDoNotBreakVisibleIdentity()
        {
            world = new GameObject("Visual lifecycle world");
            var poolObject = new GameObject("Cosmetic pool"); poolObject.transform.SetParent(world.transform);
            var pool = poolObject.AddComponent<ActorVisualPool>();
            var template = new GameObject("Visual-only template"); template.transform.SetParent(world.transform);
            template.SetActive(false); pool.Templates = new[] { template };
            var actor = CreateActor("persistent-citizen");
            Assert.That(pool.RegisterActor(actor, "시민", "시민", "이동 목표 유지", out var reason), Is.True, reason);
            Assert.That(pool.RefreshVisibility(Vector3.zero, out reason), Is.True, reason);
            Assert.That(pool.TryGetVisual(actor.ActorId, out var visual), Is.True);
            Assert.That(visual.activeInHierarchy, Is.True);

            world.SetActive(false);
            yield return null;
            Assert.That(visual.activeInHierarchy, Is.False);
            world.SetActive(true);
            Assert.That(pool.RefreshVisibility(Vector3.zero, out reason), Is.True, reason);
            Assert.That(actor.ActorId, Is.EqualTo("persistent-citizen"));
            Assert.That(pool.TryGetVisual(actor.ActorId, out visual), Is.True);
            Assert.That(visual.activeInHierarchy, Is.True);
            Assert.That(visual.transform.parent, Is.EqualTo(actor.transform));

            // Removing a culled actor may also destroy an inactive cosmetic root cached beneath it.
            pool.SetCulled(actor.ActorId, true);
            pool.UnregisterActor(actor.ActorId);
            Object.Destroy(actor.gameObject);
            yield return null;
            var next = CreateActor("new-citizen");
            Assert.That(pool.RegisterActor(next, "새 시민", "시민", "다른 목표", out reason), Is.True, reason);
            Assert.That(pool.RefreshVisibility(Vector3.zero, out reason), Is.True, reason);
            Assert.That(pool.TryGetVisual(next.ActorId, out visual), Is.True);
            Assert.That(visual.activeInHierarchy, Is.True);
            Assert.That(visual.transform.parent, Is.EqualTo(next.transform));
        }

        private ActorNavigationBinding CreateActor(string id)
        {
            var instance = new GameObject(id); instance.transform.SetParent(world.transform);
            var actor = instance.AddComponent<ActorNavigationBinding>(); actor.ActorId = id;
            return actor;
        }
    }
}
#endif
