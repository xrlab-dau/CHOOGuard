using System;
using NUnit.Framework;
using ChooGuard.Foundation.Multiplayer;

namespace ChooGuard.Foundation.Tests
{
    public sealed class VoiceExpiryBoundaryTests
    {
        private static readonly ParticipantState Participant = new ParticipantState { ParticipantId = "p", TeamId = "red" };
        private static VoiceTokenIssuer Issuer(long now) => new VoiceTokenIssuer("key", new string('s', 40), () => now);
        [TestCase(-1L)] [TestCase(long.MinValue)] [TestCase(long.MaxValue)] [TestCase(long.MaxValue - 89)]
        public void JoinCannotSignNegativeOrOverflowingLifetime(long now) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Issuer(now).Join("w", "s", "epoch", Participant, "team", "ws://127.0.0.1:7880"));
        [TestCase(-1L)] [TestCase(long.MinValue)] [TestCase(long.MaxValue)] [TestCase(long.MaxValue - 29)]
        public void RoomAdministrationCannotSignInvalidLifetime(long now) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Issuer(now).RoomAdministrationToken());
        [TestCase(-1L)] [TestCase(long.MinValue)] [TestCase(long.MaxValue)] [TestCase(long.MaxValue - 29)]
        public void ParticipantAdministrationCannotSignInvalidLifetime(long now) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Issuer(now).ParticipantAdministrationToken("room"));
        [TestCase(0L)] [TestCase(1000L)] [TestCase(long.MaxValue - 90)]
        public void ValidJoinBoundaryRetainsExactLifetime(long now)
        {
            var grant = Issuer(now).Join("w", "s", "epoch", Participant, "team", "ws://127.0.0.1:7880");
            Assert.That(grant.ExpiresAt, Is.EqualTo(checked(now + 90)));
        }
        [Test]
        public void EachGrantReadsClockOnceToKeepPayloadAndEnvelopeConsistent()
        {
            var calls = 0; var issuer = new VoiceTokenIssuer("key", new string('s', 40), () => 1000 + calls++);
            var grant = issuer.Join("w", "s", "epoch", Participant, "team", "ws://127.0.0.1:7880");
            Assert.That(calls, Is.EqualTo(1)); Assert.That(grant.ExpiresAt, Is.EqualTo(1090));
        }
    }
}
