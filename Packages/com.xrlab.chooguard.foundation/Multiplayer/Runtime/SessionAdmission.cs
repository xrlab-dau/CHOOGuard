using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class AdmissionTicket { public string ParticipantId, SecretSha256; }
    [Serializable] public sealed class JoinCredential { public string WorldId, ShiftId, ParticipantId, Secret; }
    [Serializable] public sealed class ServerSessionConfig
    {
        public WorldState World;
        public AdmissionTicket[] Tickets;
    }

    public sealed class SessionAdmission
    {
        private readonly ServerSessionConfig config;
        public SessionAdmission(ServerSessionConfig config)
        {
            if (config?.World?.Participants == null || config.Tickets == null ||
                config.World.Participants.Length < 1 || config.World.Participants.Length > 20 ||
                config.Tickets.Length != config.World.Participants.Length ||
                config.Tickets.Any(t => t == null || t.SecretSha256 == null || t.SecretSha256.Length != 64 ||
                    !config.World.Participants.Any(p => p.ParticipantId == t.ParticipantId)) ||
                config.Tickets.Select(t => t.ParticipantId).Distinct().Count() != config.Tickets.Length)
                throw new ArgumentException("Invalid server admission roster.");
            this.config = config;
        }

        public bool Allows(JoinCredential credential)
        {
            if (credential == null || credential.WorldId != config.World.WorldId || credential.ShiftId != config.World.ShiftId ||
                string.IsNullOrEmpty(credential.Secret) || credential.Secret.Length > 256) return false;
            var ticket = config.Tickets.SingleOrDefault(t => t.ParticipantId == credential.ParticipantId);
            if (ticket == null) return false;
            var actual = Digest(credential.Secret);
            var difference = 0;
            for (var i = 0; i < actual.Length; i++) difference |= actual[i] ^ ticket.SecretSha256[i];
            return difference == 0;
        }

        public static string Digest(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
