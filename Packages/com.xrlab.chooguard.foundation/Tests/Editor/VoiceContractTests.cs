using System;
using System.Text;
using NUnit.Framework;
using ChooGuard.Foundation.Multiplayer;

namespace ChooGuard.Foundation.Tests
{
    public sealed class VoiceContractTests
    {
        [Test]
        public void ReleaseInvalidatesLateCaptureOrPublishCompletion()
        {
            var gate = new PushToTalkGate(); gate.SetAvailability(true, true, true, true);
            var started = gate.Press(); Assert.That(gate.CanTransmit(started), Is.True);
            gate.Release(); Assert.That(gate.CanTransmit(started), Is.False);
            Assert.That(gate.Held, Is.False);
        }

        [Test]
        public void FocusLossDisconnectAndInputPauseNeverResumeTransmissionAutomatically()
        {
            var gate = new PushToTalkGate(); gate.SetAvailability(true, true, true, true);
            var started = gate.Press(); gate.SetAvailability(true, true, false, true);
            Assert.That(gate.CanTransmit(started), Is.False);
            gate.SetAvailability(true, true, true, true); Assert.That(gate.Held, Is.False);
            started = gate.Press(); gate.SetAvailability(true, false, true, true);
            gate.SetAvailability(true, true, true, true); Assert.That(gate.CanTransmit(started), Is.False);
            started = gate.Press(); gate.SetAvailability(true, true, true, false);
            Assert.That(gate.CanTransmit(started), Is.False);
        }

        [Test]
        public void ChannelSwitchInvalidatesEarlierAsyncStart()
        {
            var gate = new PushToTalkGate(); gate.SetAvailability(true, true, true, true);
            var old = gate.Press(); var current = gate.Press();
            Assert.That(gate.CanTransmit(old), Is.False); Assert.That(gate.CanTransmit(current), Is.True);
        }

        [Test]
        public void CaptureStopStillRunsWhenNativeMuteThrows()
        {
            var stopped = false;
            Assert.Throws<InvalidOperationException>(() => PushToTalkGate.Stop(() => { throw new InvalidOperationException("fixture mute failure"); }, () => stopped = true));
            Assert.That(stopped, Is.True);
        }

        private static string Payload(string token)
        {
            var part = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            return Encoding.UTF8.GetString(Convert.FromBase64String(part.PadRight((part.Length + 3) / 4 * 4, '=')));
        }

        [Test]
        public void VoiceGrantBindsIdentityAndRoomWithoutAdminOrDataPermission()
        {
            var issuer = new VoiceTokenIssuer("key", new string('s', 40), () => 1000);
            var p = new ParticipantState { ParticipantId = "alice", TeamId = "red", RoleId = "role-01" };
            var grant = issuer.Join("world", "shift", "epoch", p, "team", "ws://127.0.0.1:7880");
            var payload = Payload(grant.Token);
            Assert.That(payload, Does.Contain("\"sub\":\"alice\""));
            Assert.That(payload, Does.Contain("\"room\":\"" + grant.Room + "\""));
            Assert.That(payload, Does.Contain("\"canPublishSources\":[\"microphone\"]"));
            Assert.That(payload, Does.Contain("\"canPublishData\":false"));
            Assert.That(payload, Does.Not.Contain("roomAdmin")); Assert.That(payload, Does.Not.Contain("roomCreate"));
            Assert.That(grant.ExpiresAt, Is.EqualTo(1090));
            Assert.That(grant.Token, Does.Not.Contain(new string('s', 40)));
        }

        [Test]
        public void CommandChannelPublicationRequiresInstructorAndEpochChangesRoom()
        {
            var issuer = new VoiceTokenIssuer("key", new string('s', 40), () => 1000);
            var p = new ParticipantState { ParticipantId = "alice", TeamId = "red", RoleId = "role-01" };
            var listen = issuer.Join("w", "s", "old", p, "command", "ws://127.0.0.1:7880");
            Assert.That(listen.CanPublish, Is.False);
            p.IsInstructor = true;
            var speak = issuer.Join("w", "s", "new", p, "command", "ws://127.0.0.1:7880");
            Assert.That(speak.CanPublish, Is.True); Assert.That(speak.Room, Is.Not.EqualTo(listen.Room));
            Assert.Throws<ArgumentException>(() => issuer.Join("w", "s", "new", p, "arbitrary-room", "ws://127.0.0.1:7880"));
            p.IsInstructor = false; p.TeamId = "command";
            var team = issuer.Join("w", "s", "new", p, "team", "ws://127.0.0.1:7880");
            Assert.That(team.Room, Is.Not.EqualTo(speak.Room), "A team name must never alias the command permission domain");
        }

        [Test]
        public void MaximumLengthTeamIdentityFitsTheInternalRoomNamespace()
        {
            var issuer = new VoiceTokenIssuer("key", new string('s', 40), () => 1000);
            var participant = new ParticipantState { ParticipantId = "p", TeamId = new string('a', 128), RoleId = "role-01" };
            var grant = issuer.Join("w", "s", "epoch", participant, "team", "ws://127.0.0.1:17880");
            Assert.That(grant.Room.Length, Is.LessThan(200));
        }
    }
}
