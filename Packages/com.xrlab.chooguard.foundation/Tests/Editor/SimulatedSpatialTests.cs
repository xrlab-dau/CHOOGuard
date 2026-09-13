using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class SimulatedSpatialTests
    {
        private static ConnectedWorldDefinition World() => JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText("foundation/world/connected-world-profile.json"));
        [Test]
        public void BoardingBridgeStaysInStationFrameUntilTheBodyThreshold()
        {
            var authored = World(); var spatial = new SimulatedSpatialWorld(authored); var world = spatial.At(authored.Frames);
            var portal = world.Portals.Single(p => p.From == "rolling_stock_mainline");
            var previous = world.Pose("rail_platforms_mainline", new Point3(-50, 2, 8), portal.Id);
            Assert.That(spatial.TryLocate(world, previous, new Point3(-50, 2, 12.4f), .3f, null, out var bridge), Is.True);
            Assert.That(bridge.FrameId, Is.EqualTo("world")); Assert.That(bridge.RegionId, Is.EqualTo("rail_platforms_mainline"));
            Assert.That(spatial.TryLocate(world, bridge, new Point3(-50, 2, 12.9f), .3f, null, out var car), Is.True);
            Assert.That(spatial.TryLocate(world, bridge, new Point3(-50, 2, 12.6f), .3f, null, out var straddling), Is.True);
            Assert.That(straddling.FrameId, Is.EqualTo("world"), "The complete disk must cross before frame ownership changes.");
            Assert.That(spatial.TryLocate(world, car, new Point3(-50, 2, 12.4f), .3f, null, out var returning), Is.True);
            Assert.That(returning.FrameId, Is.EqualTo("train-mainline"), "Retain car ownership while stepping back across the threshold.");
            Assert.That(car.FrameId, Is.EqualTo("train-mainline")); Assert.That(car.PortalId, Is.Empty);
            Assert.That(spatial.TryLocate(world, car, new Point3(-50, 2, 12.1f), .3f, null, out var back), Is.True);
            Assert.That(back.FrameId, Is.EqualTo("world"));
            Assert.That(spatial.TryLocate(world, bridge, new Point3(-50, 2, 12.6f), .3f,
                new System.Collections.Generic.HashSet<string> { portal.Id }, out _), Is.False);
        }

        [Test]
        public void CrossFramePortalCannotUseTheOrdinaryRampOwnershipConvention()
        {
            var world = World(); world.Portals.Single(p => p.From == "rolling_stock_mainline").StaticBoarding = false;
            Assert.Throws<ArgumentException>(() => new SimulatedSpatialWorld(world));
        }

        [Test]
        public void MovingFrameCarriesLocalBodiesWhileBridgeAndStationRemainFixed()
        {
            var authored = World(); var spatial = new SimulatedSpatialWorld(authored);
            var frames = authored.Frames.Select(f => f.Copy()).ToArray(); frames.Single(f => f.FrameId == "train-mainline").Origin.X += 100;
            var current = spatial.At(frames);
            Assert.That(current.Region("rolling_stock_mainline").Center.X, Is.EqualTo(50));
            Assert.That(current.Portals.Single(p => p.From == "rolling_stock_mainline").FromPoint.X, Is.EqualTo(-50));
            Assert.That(current.Region("rail_platforms_mainline").Center.X, Is.EqualTo(-50));
            var pose = current.Pose("rolling_stock_mainline", new Point3(51, 2, 15));
            Assert.That(pose.LocalPosition.X, Is.EqualTo(1));
            Assert.That(spatial.TryLocate(current, pose, new Point3(51.1f, 2, 15), .3f, null, out _), Is.True);
            Assert.That(authored.Region("rolling_stock_mainline").Center.X, Is.EqualTo(-50));
        }

        [Test]
        public void WholeBridgeAndStraddlingFootprintsReserveDeparture()
        {
            var world = World(); var portal = world.Portals.Single(p => p.From == "rolling_stock_mainline");
            Assert.That(SimulatedSpatialWorld.OccupiesBoardingBridge(portal, new Point3(-50, 2, 5.2f), .3f, 1.8f), Is.True);
            Assert.That(SimulatedSpatialWorld.OccupiesBoardingBridge(portal, new Point3(-50, 2, 12.65f), .3f, 1.8f), Is.True);
            Assert.That(SimulatedSpatialWorld.OccupiesBoardingBridge(portal, new Point3(-50, 2, 12.9f), .3f, 1.8f), Is.False);
            Assert.That(SimulatedSpatialWorld.OccupiesBoardingBridge(portal, new Point3(-50, -8, 8), .3f, 1.8f), Is.False);
        }
    }
}
