#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using ChooGuard.Contracts;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSPACK0101Tests
    {
        [TestCase("")][TestCase(" ")][TestCase("기관")][TestCase("-a")][TestCase("a\n")][TestCase("a b")]
        public void InvalidAsciiIdIsRejected(string id) => Assert.Throws<ArgumentException>(() => new StableId(id));

        [Test]
        public void IdBoundsAndAlphabetArePreserved()
        {
            Assert.That(new StableId("A0._:-").Value, Is.EqualTo("A0._:-"));
            Assert.That(new StableId(new string('a', 128)).Value.Length, Is.EqualTo(128));
            Assert.Throws<ArgumentException>(() => new StableId(new string('a', 129)));
            Assert.Throws<ArgumentException>(() => new StableId(null));
        }

        [Test]
        public void SameKoreanNameDoesNotMergeAgencyIdentity()
        {
            var a = new NamedIdentity(new StableId("agency.a"), "서울역");
            var b = new NamedIdentity(new StableId("agency.b"), "서울역");
            Assert.That(a.DisplayName, Is.EqualTo(b.DisplayName));
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(new StableId("agency.a"), Is.EqualTo(a.Id));
        }

        [Test]
        public void DefaultIdCannotCrossDtoBoundary()
        {
            Assert.Throws<ArgumentException>(() => new NamedIdentity(default(StableId), "기관"));
            Assert.Throws<InvalidOperationException>(() => { var unused = default(StableId).Value; });
        }

        [TestCase(double.NaN)][TestCase(double.PositiveInfinity)][TestCase(double.NegativeInfinity)]
        public void NonFiniteSiQuantityIsRejected(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new SiValue(value, SiUnit.Metre));

        [Test]
        public void SiUnitAndTimeAxesRemainExplicit()
        {
            Assert.That(new SiValue(-2, SiUnit.Metre).Unit, Is.EqualTo(SiUnit.Metre));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SiValue(1, (SiUnit)999));
            Assert.That(new SimTick(long.MaxValue).Microseconds, Is.EqualTo(long.MaxValue));
            Assert.That(new Sequence(8).Value, Is.EqualTo(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimTick(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sequence(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonotonicTimestamp(-1));
            Assert.That(new MonotonicTimestamp(long.MaxValue).Microseconds, Is.EqualTo(long.MaxValue));
            foreach (var type in new[] { typeof(SimTick), typeof(Sequence), typeof(UtcTimestamp), typeof(MonotonicTimestamp) })
                Assert.That(type.GetMethods().Any(m => m.Name == "op_Implicit"), Is.False);
        }

        [Test]
        public void UtcClockRejectsOffsetAndPreservesSeparateObservationTimes()
        {
            var observed = new UtcTimestamp(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var received = new UtcTimestamp(observed.Value.AddSeconds(5));
            Assert.That(received.Value, Is.GreaterThan(observed.Value));
            Assert.Throws<ArgumentException>(() => new UtcTimestamp(observed.Value.ToOffset(TimeSpan.FromHours(9))));
            Assert.Throws<ArgumentException>(() => new UtcTimestamp(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)));
        }
    }
}
#endif
