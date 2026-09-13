using System;
using NUnit.Framework;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class AdmissionTests
    {
        [Test]
        public void TicketIsBoundToWorldShiftAndRosterIdentity()
        {
            var admission = new SessionAdmission(new ServerSessionConfig { World = new WorldState {
                WorldId = "w", ShiftId = "s", Participants = new[] { new ParticipantState {
                    ParticipantId = "p", TeamId = "team", RoleId = "role-01", RegionId = "hall" } } },
                Tickets = new[] { new AdmissionTicket { ParticipantId = "p", SecretSha256 = SessionAdmission.Digest("a private fixture secret") } } });
            var ticket = new JoinCredential { WorldId = "w", ShiftId = "s", ParticipantId = "p", Secret = "a private fixture secret" };
            Assert.That(admission.Allows(ticket), Is.True);
            ticket.ShiftId = "old"; Assert.That(admission.Allows(ticket), Is.False);
            ticket.ShiftId = "s"; ticket.ParticipantId = "instructor"; Assert.That(admission.Allows(ticket), Is.False);
            ticket.ParticipantId = "p"; ticket.Secret = "wrong"; Assert.That(admission.Allows(ticket), Is.False);
        }

        [Test]
        public void InvalidOrDuplicateRosterCannotStartAdmission()
        {
            Assert.Throws<ArgumentException>(() => new SessionAdmission(new ServerSessionConfig()));
        }
    }
}
