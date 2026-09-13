using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Single-writer, hash-chained, flushed server records. Contains no join credentials.</summary>
    public sealed class ShiftJournal : ICommitSink, IDisposable
    {
        private const string EmptyHash = "0000000000000000000000000000000000000000000000000000000000000000";
        private const long MaximumJournalBytes = 256L * 1024 * 1024;
        private const int MaximumRecordBytes = 1024 * 1024;
        private readonly string directory;
        private readonly FileStream lease, journal;
        private readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        private string lastHash = EmptyHash;
        private long sequence;
        private bool opened, faulted;
        private string worldId, shiftId;
        private readonly List<string> durableReceipts = new List<string>();

        [Serializable] private sealed class Envelope
        {
            public int Schema = 1;
            public string PreviousHash, Hash, Payload;
        }

        public ShiftJournal(string directory)
        {
            this.directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(this.directory);
            lease = new FileStream(Path.Combine(this.directory, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try { journal = new FileStream(Path.Combine(this.directory, "actions-v1.jsonl"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); }
            catch { lease.Dispose(); throw; }
        }

        public AuthoritativeShift Open(WorldState initial, Func<ParticipantState, EntityState, bool> visible,
            Func<EntityState, bool> canOperate = null)
        {
            if (opened || faulted) throw new InvalidOperationException("Open a new journal instance for recovery.");
            var checkpointPath = Path.Combine(directory, "checkpoint-v1.json");
            worldId = initial.WorldId; shiftId = initial.ShiftId;
            var restoring = File.Exists(checkpointPath);
            var checkpointHash = EmptyHash;
            WorldState checkpoint;
            if (restoring)
            {
                var envelope = Decode(File.ReadAllText(checkpointPath));
                checkpoint = JsonUtility.FromJson<WorldState>(envelope.Payload);
                checkpointHash = envelope.PreviousHash;
                if (checkpoint == null || checkpoint.WorldId != initial.WorldId || checkpoint.ShiftId != initial.ShiftId ||
                    checkpoint.SchemaVersion != initial.SchemaVersion || (initial.SchemaVersion == 2 &&
                    (checkpoint.SpatialProfileId != initial.SpatialProfileId || JsonUtility.ToJson(new FrameList { Frames = checkpoint.Frames }) !=
                    JsonUtility.ToJson(new FrameList { Frames = initial.Frames }))))
                    throw new InvalidDataException("Checkpoint belongs to another world, shift, schema or spatial profile.");
            }
            else
            {
                if (journal.Length != 0) throw new InvalidDataException("Journal exists without its initial checkpoint.");
                checkpoint = initial.Copy();
                if (checkpoint.Sequence != 0) throw new InvalidDataException("New journal requires sequence zero.");
                // Validate before creating the initial durable record.
                new AuthoritativeShift(checkpoint, this, visible);
                WriteCheckpoint(checkpoint);
            }
            var shift = restoring ? AuthoritativeShift.Restore(checkpoint, this, visible, canOperate) : new AuthoritativeShift(checkpoint, this, visible, canOperate);
            if (journal.Length > MaximumJournalBytes) throw new InvalidDataException("Journal exceeds configured recovery limit.");
            journal.Position = 0;
            var bytes = new byte[(int)journal.Length];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = journal.Read(bytes, count, bytes.Length - count);
                if (read == 0) throw new EndOfStreamException("Journal changed while reading.");
                count += read;
            }
            int start = 0, durableEnd = 0;
            long readSequence = 0;
            var readHash = EmptyHash;
            var checkpointMatched = checkpoint.Sequence == 0 && checkpointHash == EmptyHash;
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != (byte)'\n') continue;
                if (i - start > MaximumRecordBytes) throw new InvalidDataException("Oversized journal record.");
                var envelope = Decode(utf8.GetString(bytes, start, i - start));
                if (envelope.PreviousHash != readHash) throw new InvalidDataException("Broken journal hash chain.");
                var commit = JsonUtility.FromJson<ShiftCommit>(envelope.Payload);
                if (commit?.Receipt == null || commit.Receipt.Sequence != readSequence + 1 ||
                    commit.Receipt.WorldId != initial.WorldId || commit.Receipt.ShiftId != initial.ShiftId)
                    throw new InvalidDataException("Broken journal sequence or identity.");
                readSequence++; readHash = envelope.Hash;
                var receiptJson = JsonUtility.ToJson(commit.Receipt);
                durableReceipts.Add(receiptJson);
                if (readSequence <= checkpoint.Sequence &&
                    JsonUtility.ToJson(checkpoint.Receipts.Single(r => r.Sequence == readSequence)) != receiptJson)
                    throw new InvalidDataException("Checkpoint ledger differs from its journal prefix.");
                if (readSequence == checkpoint.Sequence) checkpointMatched = readHash == checkpointHash;
                if (readSequence > checkpoint.Sequence) shift.Replay(commit);
                start = i + 1; durableEnd = start;
            }
            if (!checkpointMatched || readSequence < checkpoint.Sequence)
                throw new InvalidDataException("Checkpoint is not bound to this journal prefix.");
            // An unterminated last record was never acknowledged as flushed. Retain all complete records.
            if (durableEnd != bytes.Length)
            {
                journal.SetLength(durableEnd);
                journal.Flush(true);
            }
            sequence = readSequence; lastHash = readHash;
            journal.Position = journal.Length;
            opened = true;
            return shift;
        }

        public void Append(ShiftCommit commit)
        {
            if (!opened || faulted || commit?.Receipt == null || commit.Receipt.Sequence != sequence + 1)
                throw new IOException("Journal requires recovery or a contiguous commit.");
            var payload = JsonUtility.ToJson(commit);
            var envelope = Encode(payload, lastHash);
            var bytes = utf8.GetBytes(JsonUtility.ToJson(envelope) + "\n");
            if (bytes.Length > MaximumRecordBytes || journal.Length + bytes.Length > MaximumJournalBytes)
                throw new IOException("Journal storage limit reached; preserve and rotate through a reviewed workflow.");
            try
            {
                journal.Write(bytes, 0, bytes.Length);
                journal.Flush(true);
                sequence = commit.Receipt.Sequence; lastHash = envelope.Hash;
                durableReceipts.Add(JsonUtility.ToJson(commit.Receipt));
            }
            catch { faulted = true; throw; }
        }

        public void Checkpoint(WorldState state)
        {
            if (!opened || faulted || state.Sequence != sequence) throw new IOException("Checkpoint is not at the durable journal boundary.");
            new AuthoritativeShift(state, this, (a, e) => true);
            if (state.WorldId != worldId || state.ShiftId != shiftId ||
                !state.Receipts.OrderBy(r => r.Sequence).Select(r => JsonUtility.ToJson(r)).SequenceEqual(durableReceipts))
                throw new InvalidDataException("Checkpoint identity or ledger differs from durable records.");
            WriteCheckpoint(state);
        }

        private void WriteCheckpoint(WorldState state)
        {
            var path = Path.Combine(directory, "checkpoint-v1.json");
            var temporary = path + ".tmp";
            var bytes = utf8.GetBytes(JsonUtility.ToJson(Encode(JsonUtility.ToJson(state), lastHash)));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        private static Envelope Encode(string payload, string previous) => new Envelope {
            Payload = payload, PreviousHash = previous, Hash = Hash(previous + "\n" + payload) };

        [Serializable] private sealed class FrameList { public SpatialFrame[] Frames; }

        private static Envelope Decode(string json)
        {
            Envelope envelope;
            try { envelope = JsonUtility.FromJson<Envelope>(json); }
            catch (ArgumentException exception) { throw new InvalidDataException("Invalid record encoding.", exception); }
            if (envelope == null || envelope.Schema != 1 || envelope.Payload == null || envelope.PreviousHash == null ||
                envelope.PreviousHash.Length != 64 || envelope.Hash != Hash(envelope.PreviousHash + "\n" + envelope.Payload))
                throw new InvalidDataException("Record version or hash mismatch.");
            return envelope;
        }

        private static string Hash(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        public void Dispose() { journal.Dispose(); lease.Dispose(); }
    }
}
