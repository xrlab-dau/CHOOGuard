using System;
using System.IO;
using System.Text;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Bounded UTF8 projection using a deterministic LZ token stream with exact input consumption.
    /// Literal token 0..127 stores 1..128 bytes; match token 128..255 stores 3..130 bytes plus a ushort backward distance.
    /// Never pass a private WorldState to this boundary.</summary>
    public static class SnapshotPayload
    {
        public const int MaximumPlainBytes = 256 * 1024;
        private const uint Magic = 0x334C4743;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static byte[] Encode(string observedJson)
        {
            if (observedJson == null || observedJson.Length > MaximumPlainBytes || Utf8.GetByteCount(observedJson) > MaximumPlainBytes)
                throw new ArgumentException("Observed projection exceeds its plain payload bound.");
            var plain = Utf8.GetBytes(observedJson);
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Utf8, true))
                {
                    writer.Write(Magic); writer.Write(plain.Length);
                    var previous = new int[8192]; for (var h = 0; h < previous.Length; h++) previous[h] = -1;
                    var literal = 0; var position = 0;
                    void Flush(int end)
                    {
                        while (literal < end)
                        { var n = Math.Min(128, end - literal); writer.Write((byte)(n - 1)); writer.Write(plain, literal, n); literal += n; }
                    }
                    while (position < plain.Length)
                    {
                        var count = 0; var candidate = -1;
                        if (position + 2 < plain.Length)
                        {
                            var hash = Hash(plain, position); candidate = previous[hash]; previous[hash] = position;
                            if (candidate >= 0 && position - candidate <= 32768)
                                while (count < 130 && position + count < plain.Length && plain[candidate + count] == plain[position + count]) count++;
                        }
                        if (count >= 4)
                        {
                            Flush(position); writer.Write((byte)(128 + count - 3)); writer.Write((ushort)(position - candidate));
                            for (var p = position + 1; p < position + count && p + 2 < plain.Length; p++) previous[Hash(plain, p)] = p;
                            position += count; literal = position;
                        }
                        else { position++; if (position - literal == 128) Flush(position); }
                        if (output.Length > SnapshotFragments.MaximumPayloadBytes) throw new ArgumentException("Observed projection exceeds its compressed bound.");
                    }
                    Flush(position); writer.Flush();
                }
                if (output.Length > SnapshotFragments.MaximumPayloadBytes) throw new ArgumentException("Observed projection exceeds its compressed bound.");
                return output.ToArray();
            }
        }
        public static string Decode(byte[] payload)
        {
            try
            {
                if (payload == null || payload.Length < 8 || payload.Length > SnapshotFragments.MaximumPayloadBytes)
                    throw new ArgumentException("Invalid compressed projection size.");
                using (var input = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(input, Utf8, true))
                {
                    if (reader.ReadUInt32() != Magic) throw new ArgumentException("Unknown projection encoding.");
                    var length = reader.ReadInt32();
                    if (length < 0 || length > MaximumPlainBytes) throw new ArgumentException("Invalid projection expansion bound.");
                    var output = new byte[length]; var position = 0;
                    while (input.Position < input.Length)
                    {
                        var token = reader.ReadByte();
                        if (token < 128)
                        {
                            var count = token + 1;
                            if (count > length - position || count > input.Length - input.Position) throw new ArgumentException("Invalid projection literal.");
                            if (reader.Read(output, position, count) != count) throw new ArgumentException("Truncated projection literal.");
                            position += count;
                        }
                        else
                        {
                            var count = token - 128 + 3; var distance = reader.ReadUInt16();
                            if (distance == 0 || distance > 32768 || distance > position || count > length - position)
                                throw new ArgumentException("Invalid projection match.");
                            for (var i = 0; i < count; i++) { output[position] = output[position - distance]; position++; }
                        }
                    }
                    if (position != length) throw new ArgumentException("Truncated projection.");
                    return Utf8.GetString(output);
                }
            }
            catch (Exception e) when (e is IOException || e is DecoderFallbackException)
            { throw new ArgumentException("Malformed compressed projection.", e); }
        }
        private static int Hash(byte[] value, int position) => ((value[position] * 251 + value[position + 1]) * 251 + value[position + 2]) & 8191;
    }
}
