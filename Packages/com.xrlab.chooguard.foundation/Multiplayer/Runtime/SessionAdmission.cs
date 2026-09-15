using System;
using System.Linq;
using System.Collections.Generic;
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
        // Admission is a validated snapshot. Mutating a deserialized configuration
        // must not silently rotate credentials or rebind a running world's identity.
        private readonly string worldId, shiftId;
        private readonly Dictionary<string, string> ticketDigests;

        public SessionAdmission(ServerSessionConfig config)
        {
            if (config?.World?.Participants == null || config.Tickets == null ||
                !Identifier(config.World.WorldId) || !Identifier(config.World.ShiftId) ||
                config.World.Participants.Length < 1 || config.World.Participants.Length > 20 ||
                config.Tickets.Length != config.World.Participants.Length ||
                config.World.Participants.Any(p => p == null || !Identifier(p.ParticipantId)))
                throw new ArgumentException("Invalid server admission roster.", nameof(config));

            var roster = new HashSet<string>(config.World.Participants.Select(p => p.ParticipantId), StringComparer.Ordinal);
            if (roster.Count != config.World.Participants.Length ||
                config.Tickets.Any(t => t == null || !Identifier(t.ParticipantId) ||
                    !roster.Contains(t.ParticipantId) || !HexDigest(t.SecretSha256)) ||
                config.Tickets.Select(t => t.ParticipantId).Distinct(StringComparer.Ordinal).Count() != config.Tickets.Length)
                throw new ArgumentException("Invalid server admission roster.", nameof(config));

            worldId = config.World.WorldId;
            shiftId = config.World.ShiftId;
            ticketDigests = config.Tickets.ToDictionary(t => t.ParticipantId,
                t => t.SecretSha256.ToLowerInvariant(), StringComparer.Ordinal);
        }

        public bool Allows(JoinCredential credential)
        {
            if (credential == null || credential.WorldId != worldId || credential.ShiftId != shiftId ||
                !Identifier(credential.ParticipantId) || string.IsNullOrEmpty(credential.Secret) ||
                credential.Secret.Length > 256 || !ticketDigests.TryGetValue(credential.ParticipantId, out var expected))
                return false;
            var actual = Digest(credential.Secret);
            var difference = 0;
            for (var i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
            return difference == 0;
        }

        // Same bounded wire identity alphabet as AuthoritativeShift; display names
        // are not participant/world/shift protocol identifiers.
        private static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || "_.:-".IndexOf(c) >= 0);
        private static bool HexDigest(string value) => value != null && value.Length == 64 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');

        public static string Digest(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
