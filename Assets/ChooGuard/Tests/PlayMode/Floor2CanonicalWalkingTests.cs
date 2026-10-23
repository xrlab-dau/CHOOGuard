#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Floor2CanonicalWalkingTests
    {
        // The maintained runtime Resource is the canonical route input.
        // NativeWalkingRoutePlayTests separately exercises production nav dispatch,
        // curved physical walks, failure and cleanup on isolated approved fixtures.
        // Resource validity alone does not certify its historical station placement.
        [Test]
        public void CanonicalRouteEndpointsDirectionsAndLinkCostsUseTheProductionResource()
        {
            var routes = StationWalkingRoutes.Load();
            Assert.That(routes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(routes.Select(route => route.id).Distinct().Count(), Is.EqualTo(routes.Length));
            foreach (var route in routes)
            {
                Assert.DoesNotThrow(() => route.Validate());
                var original = (Vector3[])route.path.Clone();
                var forward = route.Direction(false);
                var backward = route.Direction(true);
                Assert.That(forward, Is.EqualTo(original));
                Assert.That(backward, Is.EqualTo(original.Reverse().ToArray()));
                Assert.That(ReferenceEquals(forward, route.path), Is.False);
                Assert.That(ReferenceEquals(backward, route.path), Is.False);
                Assert.That(route.Matches(route.Start, route.End, out bool reverse), Is.True);
                Assert.That(reverse, Is.False);
                Assert.That(route.Matches(route.End, route.Start, out reverse), Is.True);
                Assert.That(reverse, Is.True);
                Assert.That(route.Matches(route.Start + Vector3.up, route.End, out _), Is.False);
                float measured = 0;
                for (int i = 1; i < original.Length; i++) measured += Vector3.Distance(original[i - 1], original[i]);
                var link = route.LinkData();
                Assert.That(route.Length, Is.EqualTo(measured).Within(.00001f));
                Assert.That(link.startPosition, Is.EqualTo(route.Start));
                Assert.That(link.endPosition, Is.EqualTo(route.End));
                Assert.That(link.bidirectional, Is.True);
                Assert.That(link.costModifier, Is.EqualTo(Mathf.Max(1, measured / Vector3.Distance(route.Start, route.End))).Within(.00001f));
                forward[0] += Vector3.one;
                backward[0] += Vector3.one;
                Assert.That(route.path, Is.EqualTo(original), "Consumers must not mutate the canonical runtime Resource points");
            }
        }
    }
}
#endif
