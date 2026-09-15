using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;

namespace ChooGuard.NonUnity.Tests
{
    public sealed class SpatialBoundaryTests
    {
        private static ConnectedPortalDefinition Portal() => new ConnectedPortalDefinition {
            FromPoint = new Point3(0, 0, 0), ToPoint = new Point3(10, 0, 0), ClearWidth = 1, ClearHeight = 3
        };

        private static ConnectedWorldDefinition Definition()
        {
            var text = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "connected-world-profile.json"));
            var result = JsonSerializer.Deserialize<ConnectedWorldDefinition>(text, new JsonSerializerOptions { IncludeFields = true });
            result.Validate();
            return result;
        }

        [TestCase(.51f)]
        [TestCase(1f)]
        [TestCase(100f)]
        public void BodyWiderThanDoorCannotPassItsCentre(float radius)
        {
            Assert.That(Portal().Contains(new Point3(5, 0, 0), radius, out _), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.NaN)]
        public void InvalidRadiusCannotBecomePassableClearance(float radius)
        {
            Assert.That(Portal().Contains(new Point3(5, 0, 0), radius, out _), Is.False);
        }

        [Test]
        public void ExactFitAndPointProbeKeepTheirOriginalMeaning()
        {
            Assert.That(Portal().Contains(new Point3(5, 0, 0), .5f, out var along), Is.True);
            Assert.That(along, Is.EqualTo(.5f));
            Assert.That(Portal().Contains(new Point3(5, 0, .001f), .5f, out _), Is.False);
            Assert.That(Portal().Contains(new Point3(5, 0, .5f), 0, out _), Is.True);
            Assert.That(Portal().Contains(new Point3(5, .3f, 0), .25f, out _), Is.True);
        }

        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void InvalidRadiusCannotPassRegionShortcut(float radius)
        {
            var d = Definition(); var region = d.Region(d.StartRegionId); var pose = d.Pose(region.Id, region.Hub);
            Assert.That(d.TryLocate(pose, region.Hub, radius, null, out _), Is.False);
            Assert.That(new SimulatedSpatialWorld(d).TryLocate(d, pose, region.Hub, radius, null, out _), Is.False);
        }

        [Test]
        public void MissingPreviousPoseIsARejectedMoveNotAnUnhandledException()
        {
            var d = Definition();
            Assert.That(d.TryLocate(null, d.Region(d.StartRegionId).Hub, .25f, null, out _), Is.False);
        }

        [Test]
        public void PhysicalWorldOwnsItsValidatedAuthoredDefinition()
        {
            var authored = Definition(); var frames = authored.Frames.Select(f => f.Copy()).ToArray();
            var world = new SimulatedSpatialWorld(authored); var expected = world.At(frames);
            var expectedX = expected.Regions[0].Hub.X; var expectedWidth = expected.Portals[0].ClearWidth;
            var expectedLimit = expected.Limits[0];
            authored.Regions[0].Hub = new Point3(999, 999, 999);
            authored.Portals[0].ClearWidth = 999;
            authored.Limits[0] = "mutated";
            var current = world.At(frames);
            Assert.That(current.Regions[0].Hub.X, Is.EqualTo(expectedX));
            Assert.That(current.Portals[0].ClearWidth, Is.EqualTo(expectedWidth));
            Assert.That(current.Limits[0], Is.EqualTo(expectedLimit));
        }

        [Test]
        public void PublishedTopologyCannotMutateTheNextSnapshotOrItsSourceArrays()
        {
            var d = Definition();
            var index = Array.FindIndex(d.Portals, p => p.LinkedDoorEntityIds.Length > 0);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Canonical profile must include a linked door.");
            var originalWidth = d.Portals[index].ClearWidth;
            var originalDoor = d.Portals[index].LinkedDoorEntityIds[0];
            var world = new SimulatedSpatialWorld(d);
            var snapshot = world.At(d.Frames);
            snapshot.Portals[index].ClearWidth = 999;
            snapshot.Portals[index].LinkedDoorEntityIds[0] = "mutated";
            var next = world.At(d.Frames);
            Assert.That(next.Portals[index].ClearWidth, Is.EqualTo(originalWidth));
            Assert.That(next.Portals[index].LinkedDoorEntityIds[0], Is.EqualTo(originalDoor));
            Assert.That(d.Portals[index].ClearWidth, Is.EqualTo(originalWidth));
        }

        [Test]
        public void InvalidExclusionsAreRejectedAtDefinitionBoundary()
        {
            foreach (var exclusion in new RegionExclusion[] { null, new RegionExclusion { SizeX = -1, SizeZ = 1 },
                new RegionExclusion { SizeX = 1, SizeZ = 1, Center = new Point3(float.NaN, 0, 0) } })
            {
                var d = Definition(); d.Regions[0].Exclusions = new[] { exclusion };
                Assert.Throws<InvalidDataException>(() => d.Validate());
            }
        }

        [Test]
        public void NullFrameIsRejectedAtDefinitionBoundary()
        {
            var d = Definition(); d.Frames[0] = null;
            Assert.Throws<InvalidDataException>(() => d.Validate());
        }

        [Test]
        public void AllCanonicalPortalCentresRespectTheAuthoredWidths()
        {
            var d = Definition();
            foreach (var p in d.Portals)
            {
                var centre = new Point3((p.FromPoint.X + p.ToPoint.X) / 2, (p.FromPoint.Y + p.ToPoint.Y) / 2,
                    (p.FromPoint.Z + p.ToPoint.Z) / 2);
                Assert.That(p.Contains(centre, p.ClearWidth / 2 + .01f, out _), Is.False, p.Id);
                Assert.That(p.Contains(centre, .2f, out _), Is.True, p.Id);
            }
        }
    }
}
