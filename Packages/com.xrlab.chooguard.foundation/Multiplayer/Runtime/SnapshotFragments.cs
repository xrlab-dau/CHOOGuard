using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ChooGuard.Foundation.Multiplayer
{
    public enum FragmentResult { Incomplete, Complete, Ignored, Rejected }

    /// <summary>Opaque versioned snapshot framing. Authentication belongs to the underlying game connection.</summary>
    public static class SnapshotFragments
    {
        public const int MaximumPayloadBytes = 65536, FragmentPayloadBytes = 900, HeaderBytes = 52;
        internal const uint Magic = 0x33474353;
        public static byte[][] Split(long serial, byte[] payload)
        {
            if (serial <= 0 || payload == null || payload.Length < 1 || payload.Length > MaximumPayloadBytes)
                throw new ArgumentException("Invalid snapshot serial or payload size.");
            var count = (payload.Length + FragmentPayloadBytes - 1) / FragmentPayloadBytes;
            byte[] hash; using (var sha = SHA256.Create()) hash = sha.ComputeHash(payload);
            var packets = new byte[count][];
            for (var index = 0; index < count; index++)
            {
                var offset = index * FragmentPayloadBytes; var size = Math.Min(FragmentPayloadBytes, payload.Length - offset);
                using (var stream = new MemoryStream(HeaderBytes + size))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic); writer.Write(serial); writer.Write(payload.Length);
                    writer.Write((ushort)index); writer.Write((ushort)count); writer.Write(hash);
                    writer.Write(payload, offset, size); writer.Flush(); packets[index] = stream.ToArray();
                }
            }
            return packets;
        }
    }

    public sealed class SnapshotAssembler
    {
        private sealed class Assembly
        {
            public long Serial, Started;
            public byte[] Data, Digest;
            public bool[] Received;
            public int Count;
        }
        private readonly int maxInflight;
        private readonly long timeout;
        private readonly Dictionary<long, Assembly> pending = new Dictionary<long, Assembly>();
        private long lastComplete, lastTime;
        public int InflightCount => pending.Count;
        public int BufferedBytes => pending.Values.Sum(a => a.Data.Length);

        public SnapshotAssembler(int maxInflight = 3, long timeoutMilliseconds = 300)
        {
            if (maxInflight < 1 || maxInflight > 8 || timeoutMilliseconds < 1 || timeoutMilliseconds > 10000)
                throw new ArgumentException("Invalid snapshot reassembly budget.");
            this.maxInflight = maxInflight; timeout = timeoutMilliseconds;
        }

        public void Expire(long nowMilliseconds)
        {
            if (nowMilliseconds < lastTime || nowMilliseconds < 0) throw new ArgumentException("Snapshot clock must be monotonic.");
            lastTime = nowMilliseconds;
            foreach (var key in pending.Where(p => nowMilliseconds - p.Value.Started >= timeout).Select(p => p.Key).ToArray())
                pending.Remove(key);
        }

        public FragmentResult Accept(byte[] packet, long nowMilliseconds, out byte[] payload, out long serial)
        {
            payload = null; serial = 0; Expire(nowMilliseconds);
            if (packet == null || packet.Length < SnapshotFragments.HeaderBytes || packet.Length > SnapshotFragments.HeaderBytes + SnapshotFragments.FragmentPayloadBytes)
                return FragmentResult.Rejected;
            using (var stream = new MemoryStream(packet, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != SnapshotFragments.Magic) return FragmentResult.Rejected;
                var id = reader.ReadInt64(); var length = reader.ReadInt32(); var index = reader.ReadUInt16(); var count = reader.ReadUInt16();
                if (id <= 0 || length < 1 || length > SnapshotFragments.MaximumPayloadBytes ||
                    count != (length + SnapshotFragments.FragmentPayloadBytes - 1) / SnapshotFragments.FragmentPayloadBytes || index >= count)
                    return FragmentResult.Rejected;
                var size = Math.Min(SnapshotFragments.FragmentPayloadBytes, length - index * SnapshotFragments.FragmentPayloadBytes);
                if (packet.Length != SnapshotFragments.HeaderBytes + size) return FragmentResult.Rejected;
                if (id <= lastComplete) return FragmentResult.Ignored;
                var digest = reader.ReadBytes(32);
                if (!pending.TryGetValue(id, out var assembly))
                {
                    if (pending.Count == maxInflight)
                    {
                        var oldest = pending.Keys.Min();
                        if (id <= oldest) return FragmentResult.Ignored;
                        pending.Remove(oldest);
                    }
                    assembly = new Assembly { Serial = id, Started = nowMilliseconds, Data = new byte[length],
                        Digest = digest, Received = new bool[count] };
                    pending.Add(id, assembly);
                }
                else if (assembly.Data.Length != length || assembly.Received.Length != count || !Equal(assembly.Digest, digest))
                { pending.Remove(id); return FragmentResult.Rejected; }
                var offset = index * SnapshotFragments.FragmentPayloadBytes;
                if (assembly.Received[index])
                {
                    for (var i = 0; i < size; i++)
                        if (assembly.Data[offset + i] != packet[SnapshotFragments.HeaderBytes + i])
                        { pending.Remove(id); return FragmentResult.Rejected; }
                    return FragmentResult.Incomplete;
                }
                Buffer.BlockCopy(packet, SnapshotFragments.HeaderBytes, assembly.Data, offset, size);
                assembly.Received[index] = true; assembly.Count++;
                if (assembly.Count != count) return FragmentResult.Incomplete;
                pending.Remove(id);
                byte[] actual; using (var sha = SHA256.Create()) actual = sha.ComputeHash(assembly.Data);
                if (!Equal(actual, assembly.Digest)) return FragmentResult.Rejected;
                lastComplete = id; payload = assembly.Data; serial = id;
                foreach (var key in pending.Keys.Where(k => k <= lastComplete).ToArray()) pending.Remove(key);
                return FragmentResult.Complete;
            }
        }
        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            var difference = 0; for (var i = 0; i < a.Length; i++) difference |= a[i] ^ b[i]; return difference == 0;
        }
    }
}

