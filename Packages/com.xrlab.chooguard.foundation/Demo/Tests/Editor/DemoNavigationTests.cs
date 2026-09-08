using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoNavigationTests
    {
        [TestCase(0, 0, 10, 0)]
        [TestCase(0, 10, 0, 90)]
        [TestCase(0, -10, 0, -90)]
        [TestCase(0, 0, -10, 180)]
        [TestCase(90, 0, 10, -90)]
        public void BearingPreservesTargetsOutsideTheView(float yaw, float x, float z, float expected)
        {
            Assert.That(DemoNavigation.Bearing(Quaternion.Euler(0, yaw, 0), Vector3.zero,
                new Vector3(x, 7, z)), Is.EqualTo(expected).Within(.01f));
        }

        [Test]
        public void CoincidentTargetHasNoSpuriousHeading()
        {
            Assert.That(DemoNavigation.Bearing(Quaternion.identity, Vector3.one, Vector3.one), Is.Zero);
        }
    }
}
