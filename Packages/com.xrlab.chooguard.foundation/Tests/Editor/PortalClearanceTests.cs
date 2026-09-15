using System;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class PortalClearanceTests
    {
        private static ConnectedPortalDefinition Portal() => new ConnectedPortalDefinition {
            FromPoint = new Point3(0, 0, 0), ToPoint = new Point3(10, 2, 0), ClearWidth = 2, ClearHeight = 3
        };
        [TestCase(1.01f)] [TestCase(2f)] [TestCase(float.PositiveInfinity)]
        [TestCase(-.1f)] [TestCase(float.NegativeInfinity)] [TestCase(float.NaN)]
        public void OversizedOrInvalidRadiusNeverFits(float radius) =>
            Assert.That(Portal().Contains(new Point3(5, 1, 0), radius, out _), Is.False);
        [TestCase(0f)] [TestCase(-2f)] [TestCase(float.PositiveInfinity)] [TestCase(float.NaN)]
        public void InvalidWidthDoesNotBecomeClearanceBySquaring(float width)
        {
            var portal = Portal(); portal.ClearWidth = width;
            Assert.That(portal.Contains(new Point3(5, 1, 0), .3f, out _), Is.False);
        }
        [TestCase(0f, 1f, true)]
        [TestCase(.5f, .5f, true)]
        [TestCase(.501f, .5f, false)]
        [TestCase(-.501f, .5f, false)]
        public void SlopedCorridorHonorsActualRemainingHalfWidth(float lateral, float radius, bool fits)
        {
            Assert.That(Portal().Contains(new Point3(5, 1, lateral), radius, out var along), Is.EqualTo(fits));
            Assert.That(along, Is.EqualTo(.5f).Within(.00001));
        }
        [Test]
        public void ReversedSlopedCorridorHasIdenticalContainment()
        {
            var first = Portal(); var reverse = Portal();
            reverse.FromPoint = first.ToPoint; reverse.ToPoint = first.FromPoint;
            var point = new Point3(2.5f, .5f, .4f);
            Assert.That(first.Contains(point, .5f, out var a), Is.True);
            Assert.That(reverse.Contains(point, .5f, out var b), Is.True);
            Assert.That(a + b, Is.EqualTo(1).Within(.00001));
        }
        [Test]
        public void DegenerateCorridorIsRejectedWithoutNonfiniteOutput()
        {
            var portal = Portal(); portal.ToPoint = portal.FromPoint;
            Assert.That(portal.Contains(new Point3(), 0, out var along), Is.False);
            Assert.That(float.IsNaN(along) || float.IsInfinity(along), Is.False);
        }
        [Test]
        public void FiniteLargeCoordinatesDoNotOverflowIntermediateSegmentArithmetic()
        {
            var portal = Portal(); portal.FromPoint = new Point3(-1e30f, 0, 0); portal.ToPoint = new Point3(1e30f, 0, 0);
            Assert.That(portal.Contains(new Point3(0, 0, 0), .3f, out var along), Is.True);
            Assert.That(along, Is.EqualTo(.5f));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void NonfinitePointIsRejected(float x) =>
            Assert.That(Portal().Contains(new Point3(x, 1, 0), .3f, out _), Is.False);

        [TestCase(-.1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void TryLocateRejectsInvalidBodyBeforeCurrentRegionFastPath(float radius)
        {
            var world = World();
            Assert.That(world.TryLocate(world.Pose("hall", new Point3()), new Point3(), radius, null, out var result), Is.False);
            Assert.That(result, Is.Null);
        }
        [Test]
        public void TryLocateWithoutPreviousPoseIsDeniedRatherThanThrowing() =>
            Assert.That(World().TryLocate(null, new Point3(), .3f, null, out _), Is.False);
        private static ConnectedWorldDefinition World() => new ConnectedWorldDefinition {
            Frames = new[] { new SpatialFrame { FrameId = "world" } },
            Regions = new[] { new ConnectedRegionDefinition { Id = "hall", FrameId = "world", SizeX = 10, SizeZ = 10 } },
            Portals = Array.Empty<ConnectedPortalDefinition>()
        };
    }
}
