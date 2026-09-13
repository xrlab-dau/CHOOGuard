using System;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class SlopedFireCellTests
    {
        private static FireCellDefinition Cell(double rise) => new FireCellDefinition { Id = "ramp", WidthM = 1, DepthM = 1,
            HeightM = 2 + rise, FloorRiseM = rise, UseExplicitWallAreas = true, FloorWallAreaM2 = Math.Sqrt(1 + rise * rise),
            CeilingWallAreaM2 = Math.Sqrt(1 + rise * rise), VerticalWallLengthM = 2, VerticalWallHeightM = 2 };

        [Test]
        public void LinearRampVolumeAndHorizontalLayerInverseMatchAnalyticValues()
        {
            var cell = Cell(.5); Assert.That(cell.VolumeM3, Is.EqualTo(2));
            var q = new[] { 0, .0625, .25, 1, 1.75, 1.9375, 2 }; var h = new[] { 0, .25, .5, 1.25, 2, 2.25, 2.5 };
            for (var i = 0; i < q.Length; i++)
            {
                Assert.That(cell.HeightForLowerVolume(q[i], 2 - q[i]), Is.EqualTo(h[i]).Within(1e-12));
                Assert.That(cell.FilledHeight(h[i], 2), Is.EqualTo(q[i]).Within(1e-12));
            }
        }

        [Test]
        public void FlatAndNearlyFlatLimitsAreContinuousAndAllThermalAreaIsAssigned()
        {
            foreach (var rise in new[] { 0.0, 1e-12, .5, 2 })
            {
                var cell = Cell(rise); var prior = -1.0;
                for (var i = 0; i <= 100; i++)
                {
                    var volume = i / 50.0; var h = cell.HeightForLowerVolume(volume, 2 - volume);
                    Assert.That(h, Is.GreaterThanOrEqualTo(prior)); prior = h;
                    Assert.That(cell.FilledHeight(h, 2), Is.EqualTo(volume).Within(1e-11));
                    Assert.That(cell.FilledHeight(h, 2) + cell.FilledHeight(cell.HeightM - h, 2), Is.EqualTo(2).Within(1e-11));
                    Assert.That(cell.WallAreaBetween(0, h) + cell.WallAreaBetween(h, cell.HeightM), Is.EqualTo(4).Within(1e-11));
                    Assert.That(cell.LayerSurfaceArea(h, true) + cell.LayerSurfaceArea(h, false), Is.EqualTo(cell.SurfaceAreaM2).Within(1e-11));
                }
            }
            Assert.Throws<ArgumentException>(() => Cell(.5).HeightForLowerVolume(-.1, 2.1));
            Assert.That(Cell(.5).FilledHeight(1, 0), Is.Zero);
        }
    }
}
