using System;
using NUnit.Framework;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class SnapshotFreshnessTests
    {
        [Test] public void InputNeedsACompletedCurrentSnapshotEvenIfTheConnectionIsAlive()
        {
            var freshness=new SnapshotFreshness();
            Assert.That(freshness.IsCurrent(0),Is.False);
            freshness.Received(1);
            Assert.That(freshness.IsCurrent(1.5),Is.True); Assert.That(freshness.IsCurrent(1.50001),Is.False);
            freshness.Received(2); Assert.That(freshness.IsCurrent(2),Is.True);
            Assert.Throws<ArgumentException>(()=>freshness.Received(1)); Assert.Throws<ArgumentException>(()=>freshness.IsCurrent(double.NaN));
        }
    }
}
