using System;
using System.Linq;
using NUnit.Framework;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class AdmissionBoundaryTests
    {
        private static ServerSessionConfig Config(int count = 2) => new ServerSessionConfig {
            World = new WorldState { WorldId = "world", ShiftId = "shift",
                Participants = Enumerable.Range(0, count).Select(i => new ParticipantState {
                    ParticipantId = "p" + i, TeamId = "team", RoleId = "role-01", RegionId = "hall"
                }).ToArray() },
            Tickets = Enumerable.Range(0, count).Select(i => new AdmissionTicket {
                ParticipantId = "p" + i, SecretSha256 = SessionAdmission.Digest("fixture-secret-" + i)
            }).ToArray()
        };
        private static JoinCredential Credential(int i = 0) => new JoinCredential {
            WorldId = "world", ShiftId = "shift", ParticipantId = "p" + i, Secret = "fixture-secret-" + i
        };

        [TestCase("world")]
        [TestCase("shift")]
        [TestCase("hash")]
        [TestCase("ticket-id")]
        [TestCase("tickets-array")]
        [TestCase("null-ticket")]
        public void ValidatedAdmissionIsIndependentOfCallerMutation(string field)
        {
            var config = Config(); var admission = new SessionAdmission(config);
            switch (field) {
                case "world": config.World.WorldId = "different"; break;
                case "shift": config.World.ShiftId = "different"; break;
                case "hash": config.Tickets[0].SecretSha256 = SessionAdmission.Digest("replacement"); break;
                case "ticket-id": config.Tickets[0].ParticipantId = "other"; break;
                case "tickets-array": config.Tickets = Array.Empty<AdmissionTicket>(); break;
                case "null-ticket": config.Tickets[0] = null; break;
            }
            Assert.That(admission.Allows(Credential()), Is.True);
            var changed = Credential(); changed.Secret = "replacement";
            Assert.That(admission.Allows(changed), Is.False);
        }

        [TestCase("world-null")]
        [TestCase("world-empty")]
        [TestCase("shift-empty")]
        [TestCase("null-participant")]
        [TestCase("participant-empty")]
        [TestCase("participant-duplicate")]
        [TestCase("participant-invalid")]
        [TestCase("participant-too-long")]
        [TestCase("nonhex-hash")]
        [TestCase("short-hash")]
        [TestCase("null-hash")]
        [TestCase("duplicate-ticket")]
        [TestCase("unknown-ticket")]
        public void MalformedRosterFailsAtConstructionWithArgumentError(string defect)
        {
            var config = Config();
            switch (defect) {
                case "world-null": config.World.WorldId = null; break;
                case "world-empty": config.World.WorldId = ""; break;
                case "shift-empty": config.World.ShiftId = ""; break;
                case "null-participant": config.World.Participants[0] = null; break;
                case "participant-empty": config.World.Participants[0].ParticipantId = config.Tickets[0].ParticipantId = ""; break;
                case "participant-duplicate": config.World.Participants[1].ParticipantId = "p0"; break;
                case "participant-invalid": config.World.Participants[0].ParticipantId = config.Tickets[0].ParticipantId = "p 0"; break;
                case "participant-too-long": config.World.Participants[0].ParticipantId = config.Tickets[0].ParticipantId = new string('p', 129); break;
                case "nonhex-hash": config.Tickets[0].SecretSha256 = new string('x', 64); break;
                case "short-hash": config.Tickets[0].SecretSha256 = "a"; break;
                case "null-hash": config.Tickets[0].SecretSha256 = null; break;
                case "duplicate-ticket": config.Tickets[1].ParticipantId = "p0"; break;
                case "unknown-ticket": config.Tickets[1].ParticipantId = "unknown"; break;
            }
            Assert.Throws<ArgumentException>(() => new SessionAdmission(config));
        }

        [TestCase(1)] [TestCase(20)]
        public void WholeSupportedRosterAuthenticatesWithoutGrantingOtherIdentities(int count)
        {
            var admission = new SessionAdmission(Config(count));
            for (var i = 0; i < count; i++) Assert.That(admission.Allows(Credential(i)), Is.True);
            Assert.That(admission.Allows(Credential(count)), Is.False);
            Assert.That(admission.Allows(null), Is.False);
        }
        [TestCase(0)] [TestCase(21)]
        public void UnsupportedRosterSizeIsRejected(int count) =>
            Assert.Throws<ArgumentException>(() => new SessionAdmission(Config(count)));

        [Test]
        public void TicketOrderDoesNotChangeIdentityBinding()
        {
            var config = Config(20); Array.Reverse(config.Tickets);
            var admission = new SessionAdmission(config);
            for (var i = 0; i < 20; i++) Assert.That(admission.Allows(Credential(i)), Is.True);
            var wrong = Credential(); wrong.Secret = "fixture-secret-1";
            Assert.That(admission.Allows(wrong), Is.False);
        }
        [TestCase("")] [TestCase(null)]
        public void EmptySecretIsRejected(string secret)
        {
            var credential = Credential(); credential.Secret = secret;
            Assert.That(new SessionAdmission(Config()).Allows(credential), Is.False);
        }
        [Test]
        public void UppercaseHexDigestDescribesTheSameBytes()
        {
            var config = Config(); config.Tickets[0].SecretSha256 = config.Tickets[0].SecretSha256.ToUpperInvariant();
            Assert.That(new SessionAdmission(config).Allows(Credential()), Is.True);
        }
    }
}
