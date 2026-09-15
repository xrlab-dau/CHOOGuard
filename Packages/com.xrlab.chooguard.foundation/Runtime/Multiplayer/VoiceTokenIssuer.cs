using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable]
    public sealed class VoiceGrant
    {
        public string Url, Channel, Room, Token;
        public bool CanPublish;
        public long ExpiresAt;
    }

    /// <summary>Only the authenticated game server owns this issuer and its API secret.</summary>
    public sealed class VoiceTokenIssuer
    {
        private readonly string key, secret;
        private readonly Func<long> clock;
        public VoiceTokenIssuer(string key, string secret, Func<long> clock)
        {
            RequireId(key);
            if (string.IsNullOrEmpty(secret) || secret.Length < 32) throw new ArgumentException("Voice API secret is too short.");
            this.key = key; this.secret = secret; this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public VoiceGrant Join(string world, string shift, string epoch, ParticipantState participant, string channel, string url)
        {
            if (channel != "team" && channel != "command") throw new ArgumentException("Unknown voice channel.");
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            RequireId(participant.ParticipantId); RequireId(participant.TeamId);
            var room = Room(world, shift, epoch, channel == "team" ? "team." + participant.TeamId : "command");
            var publish = channel == "team" || participant.IsInstructor;
            var now = clock();
            var video = "\"room\":\"" + room + "\",\"roomJoin\":true,\"canPublish\":" + Bool(publish) +
                ",\"canSubscribe\":true,\"canPublishData\":false,\"canUpdateOwnMetadata\":false,\"canPublishSources\":[\"microphone\"]";
            return new VoiceGrant { Url = url, Channel = channel, Room = room, CanPublish = publish, ExpiresAt = now + 90,
                Token = Sign(participant.ParticipantId, video, now, 90) };
        }

        public string RoomAdministrationToken() => Sign("voice-service", "\"roomCreate\":true", clock(), 30);
        public string ParticipantAdministrationToken(string room)
        { RequireId(room, 256); return Sign("voice-service", "\"roomAdmin\":true,\"room\":\"" + room + "\"", clock(), 30); }

        public static string Room(string world, string shift, string epoch, string team)
        {
            RequireId(world); RequireId(shift); RequireId(epoch); RequireId(team, 133);
            using (var hash = SHA256.Create())
            {
                var prefix = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(world + "/" + shift))).Replace("-", "").ToLowerInvariant().Substring(0, 16);
                var channel = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(team))).Replace("-", "").ToLowerInvariant().Substring(0, 16);
                return "cg." + prefix + "." + channel + "." + epoch;
            }
        }

        private string Sign(string subject, string video, long now, int lifetime)
        {
            // A malformed clock must fail before producing a signed token. Keep
            // both expiry arithmetic and the five-second not-before skew bounded.
            if (now < 0 || now > long.MaxValue - lifetime)
                throw new ArgumentOutOfRangeException(nameof(now), "Voice token clock is outside its representable lifetime.");
            var header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var payload = "{\"iss\":\"" + key + "\",\"sub\":\"" + subject + "\",\"iat\":" + now.ToString(CultureInfo.InvariantCulture) +
                ",\"nbf\":" + (now - 5).ToString(CultureInfo.InvariantCulture) + ",\"exp\":" + (now + lifetime).ToString(CultureInfo.InvariantCulture) + ",\"video\":{" + video + "}}";
            var unsigned = header + "." + Base64(Encoding.UTF8.GetBytes(payload));
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                return unsigned + "." + Base64(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned)));
        }
        private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static string Bool(bool value) => value ? "true" : "false";
        private static void RequireId(string id, int limit = 128)
        {
            if (string.IsNullOrEmpty(id) || id.Length > limit || id.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' ||
                c >= '0' && c <= '9' || "_.:-".IndexOf(c) >= 0)))
                throw new ArgumentException("Invalid voice identity.");
        }
    }
}
