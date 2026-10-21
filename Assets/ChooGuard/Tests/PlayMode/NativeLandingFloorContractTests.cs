#if UNITY_INCLUDE_TESTS
using System;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    // Isolated engine fixtures for the production floor-approval policy.
    // These do not certify any station location or replace current-Scene walking tests.
    public sealed class NativeLandingFloorContractTests
    {
        GameObject root, actor;
        BoxCollider floor;
        StationWalkingRouteEntry route;
        readonly Vector3 origin = new Vector3(12000, 0, 12000);

        BoxCollider Box(string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = centre;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        [SetUp]
        public void Setup()
        {
            root = new GameObject("NativeLandingFloorContractFixture");
            root.transform.position = origin;
            actor = new GameObject("Actor");
            actor.transform.SetParent(root.transform, false);
            floor = Box("ApprovedFloor", Vector3.down * .1f, new Vector3(10, .2f, 10));
            route = new StationWalkingRouteEntry
            {
                id = "isolated-floor-policy", label = "Isolated algorithm fixture",
                proof = "fixture:approved-exposed-footprint", physicalVerified = true,
                path = new[] { origin + Vector3.left, origin, origin + Vector3.right },
                floorOwners = new[] { NativeLandingWalker.Path(floor.transform) }
            };
            route.Validate();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            Physics.SyncTransforms();
        }

        bool Supported(out float height)
        {
            Physics.SyncTransforms();
            return NativeLandingWalker.Floor(origin, route, actor, out height);
        }

        [Test]
        public void ExposedFloorRequiresItsExactOwnerAndEveryFootprintRay()
        {
            Assert.That(Supported(out float height), Is.True);
            Assert.That(height, Is.EqualTo(0).Within(.0001f));
            route.floorOwners = new[] { "different-owner" };
            Assert.That(Supported(out _), Is.False, "A floor at the correct height cannot substitute for approved ownership");
            route.floorOwners = new[] { NativeLandingWalker.Path(floor.transform) };
            floor.size = new Vector3(.2f, .2f, .2f);
            Assert.That(Supported(out _), Is.False, "Centre support alone cannot certify the outer capsule footprint");
        }

        [Test]
        public void UnownedCoverCannotCertifyTheBuriedFloorAndTriggersCannotGrantSupport()
        {
            var cover = Box("UnapprovedCover", Vector3.up * .05f, new Vector3(2, .1f, 2));
            Assert.That(Supported(out _), Is.False);
            cover.isTrigger = true;
            Assert.That(Supported(out _), Is.True, "A trigger is ignored; the approved exposed floor still supports the footprint");
            floor.enabled = false;
            Assert.That(Supported(out _), Is.False, "An ignored trigger cannot replace missing physical support");
        }

        [TestCase(.34f, true)]
        [TestCase(.36f, false)]
        public void StagedHeightMustRegisterToItsActualExposedFloor(float shift, bool expected)
        {
            floor.transform.localPosition += Vector3.up * shift;
            Assert.That(Supported(out _), Is.EqualTo(expected));
        }

        [Test]
        public void ApprovedStepSeamRejectsExcessiveFootprintHeightSpread()
        {
            floor.enabled = false;
            var left = Box("LowerApprovedFloor", new Vector3(-2.5f, -.25f, 0), new Vector3(5, .2f, 10));
            var right = Box("UpperApprovedFloor", new Vector3(2.5f, .05f, 0), new Vector3(5, .2f, 10));
            route.floorOwners = new[] { NativeLandingWalker.Path(left.transform), NativeLandingWalker.Path(right.transform) };
            Assert.That(Supported(out _), Is.True, "All exposed footprint rays have approved support with a .30m spread");
            right.transform.localPosition += Vector3.up * .08f;
            Assert.That(Supported(out _), Is.False, ".38m spread exceeds the .36m source-step contract even though the upper floor registers within .35m");
        }

        [Test]
        public void UnverifiedNonFiniteAndDifferentStoreyRoutesCannotEnterTheWalkingContract()
        {
            route.physicalVerified = false;
            Assert.Throws<InvalidOperationException>(() => route.Validate());
            route.physicalVerified = true;
            route.floorOwners[0] = " ";
            Assert.Throws<InvalidOperationException>(() => route.Validate());
            route.floorOwners[0] = NativeLandingWalker.Path(floor.transform);
            route.path[1] = new Vector3(float.NaN, 0, 0);
            Assert.Throws<InvalidOperationException>(() => route.Validate());
            route.path[1] = origin + Vector3.up;
            Assert.Throws<InvalidOperationException>(() => route.Validate());
            route.path[1] = origin;
            Assert.DoesNotThrow(() => route.Validate());
        }
    }
}
#endif
