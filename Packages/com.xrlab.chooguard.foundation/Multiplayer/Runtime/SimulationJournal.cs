using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Simulation;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>V3 streaming, compressed journal. Log ordinal includes physical boundaries; action sequence counts approvals only.</summary>
    public sealed class SimulationJournal : ICommitSink, IDisposable
    {
        private const string EmptyHash = "0000000000000000000000000000000000000000000000000000000000000000";
        private const int MaxCompressed = 16 * 1024 * 1024, MaxRecordPlain = 32 * 1024 * 1024, MaxCheckpointPlain = 64 * 1024 * 1024;
        private const long MaxJournalBytes = 4L * 1024 * 1024 * 1024;
        private readonly string directory;
        private readonly FileStream lease, log;
        private readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        private readonly List<string> receipts = new List<string>();
        private string worldId, shiftId, spatialId, definitionHash, lastHash = EmptyHash;
        private string[] roster;
        private long ordinal, sequence, lastTick;
        private bool opened, faulted;
        private Boundary durableBoundary;

        [Serializable] private sealed class Envelope
        {
            public int Schema = 3, PlainBytes;
            public long Ordinal, Sequence, Tick;
            public string Kind, WorldId, ShiftId, DefinitionHash, PreviousHash, Payload, Hash;
        }
        [Serializable] private sealed class Boundary
        {
            public long Sequence;
            public string Checkpoint;
            public bool Paused;
            public SpatialFrame[] Frames;
            public ParticipantState[] Participants;
            public EntityState[] Entities;
        }
        private sealed class Line { public string Text; public long End; }

        public SimulationJournal(string directory)
        {
            this.directory = Path.GetFullPath(directory); Directory.CreateDirectory(this.directory);
            lease = new FileStream(Path.Combine(this.directory, "writer-v3.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try { log = new FileStream(Path.Combine(this.directory, "actions-v3.jsonl"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); }
            catch { lease.Dispose(); throw; }
        }

        public AuthoritativeShift Open(WorldState initial, Func<ParticipantState, EntityState, bool> visible, Func<EntityState, bool> canOperate = null)
        {
            if (opened || faulted || initial == null || initial.SchemaVersion != 3) throw new InvalidOperationException("Open a fresh v3 journal instance.");
            new AuthoritativeShift(initial, this, visible, canOperate);
            worldId = initial.WorldId; shiftId = initial.ShiftId; spatialId = initial.SpatialProfileId; definitionHash = initial.SimulationDefinitionHash;
            roster = Roster(initial.Participants);
            var path = Path.Combine(directory, "checkpoint-v3.json"); var restoring = File.Exists(path);
            Envelope saved;
            WorldState checkpoint;
            if (restoring)
            {
                if (new FileInfo(path).Length > MaxCompressed * 4L / 3 + 4096) throw new InvalidDataException("Oversized checkpoint envelope.");
                saved = DecodeEnvelope(File.ReadAllText(path, utf8), "checkpoint");
                checkpoint = ReadJson<WorldState>(Inflate(saved, MaxCheckpointPlain));
                if (checkpoint == null || checkpoint.SchemaVersion != 3 || checkpoint.WorldId != worldId || checkpoint.ShiftId != shiftId ||
                    checkpoint.SpatialProfileId != spatialId || checkpoint.SimulationDefinitionHash != definitionHash || checkpoint.Sequence != saved.Sequence ||
                    checkpoint.SimulationTick != saved.Tick || !Roster(checkpoint.Participants).SequenceEqual(roster))
                    throw new InvalidDataException("Checkpoint world, roster or physical definition differs.");
            }
            else
            {
                if (log.Length != 0 || initial.Sequence != 0) throw new InvalidDataException("V3 log requires its initial checkpoint.");
                checkpoint = initial.Copy(); saved = Encode("checkpoint", 0, 0, checkpoint.SimulationTick, EmptyHash, JsonUtility.ToJson(checkpoint), MaxCheckpointPlain);
                WriteCheckpoint(saved);
            }
            var shift = restoring ? AuthoritativeShift.Restore(checkpoint, this, visible, canOperate) : new AuthoritativeShift(checkpoint, this, visible, canOperate);
            if (log.Length > MaxJournalBytes) throw new InvalidDataException("V3 journal exceeds its streaming recovery budget.");
            var readOrdinal = 0L; var readSequence = 0L; var readTick = 0L; var readHash = EmptyHash; var durableEnd = 0L;
            var matched = saved.Ordinal == 0 && saved.PreviousHash == EmptyHash;
            foreach (var line in Lines())
            {
                var envelope = DecodeEnvelope(line.Text, null);
                if (envelope.Ordinal != readOrdinal + 1 || envelope.PreviousHash != readHash || envelope.Tick < readTick ||
                    envelope.Kind != "command" && envelope.Kind != "boundary") throw new InvalidDataException("Broken v3 journal order/hash/tick.");
                if (envelope.Ordinal > saved.Ordinal && envelope.Tick < checkpoint.SimulationTick)
                    throw new InvalidDataException("Journal tail predates its checkpoint.");
                var payload = Inflate(envelope, MaxRecordPlain);
                if (envelope.Kind == "command")
                {
                    var commit = ReadJson<ShiftCommit>(payload);
                    if (commit?.Receipt == null || commit.WorldSchemaVersion != 3 || commit.Receipt.Code != CommandCode.Accepted ||
                        commit.Receipt.WorldId != worldId || commit.Receipt.ShiftId != shiftId || commit.Receipt.Sequence != readSequence + 1 ||
                        envelope.Sequence != commit.Receipt.Sequence || commit.SimulationTick != envelope.Tick || commit.SimulationDefinitionHash != definitionHash)
                        throw new InvalidDataException("Invalid approved command record.");
                    readSequence++; var receipt = JsonUtility.ToJson(commit.Receipt); receipts.Add(receipt);
                    if (readSequence <= checkpoint.Sequence && JsonUtility.ToJson(checkpoint.Receipts.Single(r => r.Sequence == readSequence)) != receipt)
                        throw new InvalidDataException("Checkpoint approval ledger differs from its log prefix.");
                    if (envelope.Ordinal > saved.Ordinal) shift.Replay(commit);
                }
                else
                {
                    if (envelope.Sequence != readSequence) throw new InvalidDataException("Physical boundary cannot invent an action sequence.");
                    if (envelope.Ordinal > saved.Ordinal)
                    {
                        var boundary = ReadJson<Boundary>(payload); var next = shift.ExportCheckpoint();
                        if (boundary == null || boundary.Sequence != envelope.Sequence) throw new InvalidDataException("Boundary approval context differs from its envelope.");
                        ApplyBoundary(next, boundary, envelope.Tick);
                        shift = AuthoritativeShift.Restore(next, this, visible, canOperate);
                    }
                }
                readOrdinal++; readTick = envelope.Tick; readHash = envelope.Hash; durableEnd = line.End;
                if (readOrdinal == saved.Ordinal) matched = readHash == saved.PreviousHash && readSequence == checkpoint.Sequence;
            }
            if (!matched || readOrdinal < saved.Ordinal || readSequence < checkpoint.Sequence)
                throw new InvalidDataException("Checkpoint is not tied to a complete v3 log prefix.");
            // A missing final newline was never acknowledged by Append/RecordBoundary.
            if (durableEnd != log.Length) { log.SetLength(durableEnd); log.Flush(true); }
            ordinal = readOrdinal; sequence = readSequence; lastHash = readHash;
            lastTick = Math.Max(readTick, checkpoint.SimulationTick); log.Position = log.Length; opened = true;
            durableBoundary = Capture(shift.ReadSimulation());
            return shift;
        }

        public void Append(ShiftCommit commit)
        {
            if (!opened || faulted || commit?.Receipt == null || commit.WorldSchemaVersion != 3 || commit.Receipt.WorldId != worldId ||
                commit.Receipt.ShiftId != shiftId || commit.Receipt.Code != CommandCode.Accepted || commit.Receipt.Sequence != sequence + 1 ||
                commit.SimulationDefinitionHash != definitionHash || commit.SimulationTick < lastTick)
                throw new IOException("V3 approval does not continue the durable world.");
            var envelope = Encode("command", checked(ordinal + 1), commit.Receipt.Sequence, commit.SimulationTick, lastHash, JsonUtility.ToJson(commit), MaxRecordPlain);
            AppendEnvelope(envelope); sequence = commit.Receipt.Sequence; receipts.Add(JsonUtility.ToJson(commit.Receipt));
            durableBoundary = new Boundary { Sequence = sequence, Checkpoint = commit.SimulationCheckpoint, Paused = commit.Paused,
                Frames = commit.Frames.Select(f => f.Copy()).ToArray(), Participants = commit.Participants.Select(p => p.Copy()).ToArray(),
                Entities = commit.Entities.Select(e => e.Copy()).ToArray() };
        }

        public void RecordBoundary(ServerSimulationView current)
        {
            if (!opened || faulted || current == null || current.Tick < lastTick || current.Sequence != sequence || current.DefinitionHash != definitionHash)
                throw new InvalidDataException("Invalid current physical boundary.");
            var boundary = Capture(current);
            ValidateBoundary(boundary, current.Tick);
            ValidateTransition(durableBoundary, boundary);
            AppendEnvelope(Encode("boundary", checked(ordinal + 1), sequence, current.Tick, lastHash, JsonUtility.ToJson(boundary), MaxRecordPlain));
            durableBoundary = boundary;
        }

        public void Checkpoint(WorldState current)
        {
            if (!opened || faulted) throw new IOException("V3 persistence requires recovery.");
            if (current == null || current.SchemaVersion != 3 || current.WorldId != worldId || current.ShiftId != shiftId ||
                current.SpatialProfileId != spatialId || current.SimulationDefinitionHash != definitionHash || current.Sequence != sequence ||
                current.SimulationTick < lastTick || !Roster(current.Participants).SequenceEqual(roster) ||
                !current.Receipts.OrderBy(r => r.Sequence).Select(r => JsonUtility.ToJson(r)).SequenceEqual(receipts))
                throw new InvalidDataException("Checkpoint is behind or outside its durable v3 boundary.");
            new AuthoritativeShift(current, this, (a, e) => true);
            var boundary = new Boundary { Sequence = current.Sequence, Checkpoint = current.SimulationCheckpoint, Paused = current.Paused,
                Frames = current.Frames, Participants = current.Participants, Entities = current.Entities };
            ValidateTransition(durableBoundary, boundary);
            WriteCheckpoint(Encode("checkpoint", ordinal, sequence, current.SimulationTick, lastHash, JsonUtility.ToJson(current), MaxCheckpointPlain));
            lastTick = current.SimulationTick;
            durableBoundary = Capture(new AuthoritativeShift(current, this, (a, e) => true).ReadSimulation());
        }

        private void ApplyBoundary(WorldState next, Boundary boundary, long tick)
        {
            ValidateBoundary(boundary, tick);
            ValidateTransition(new Boundary { Frames = next.Frames, Participants = next.Participants, Entities = next.Entities }, boundary);
            next.SimulationTick = tick; next.SimulationCheckpoint = boundary.Checkpoint; next.Frames = boundary.Frames;
            next.Participants = boundary.Participants; next.Entities = boundary.Entities; next.Paused = true;
        }

        private static void ValidateTransition(Boundary previous, Boundary boundary)
        {
            if (!previous.Frames.Select(f => f.FrameId).OrderBy(x => x).SequenceEqual(boundary.Frames.Select(f => f.FrameId).OrderBy(x => x)))
                throw new InvalidDataException("Physical boundary changed registered frames.");
            var oldWorld = previous.Frames.SingleOrDefault(f => f.FrameId == "world");
            var newWorld = boundary.Frames.SingleOrDefault(f => f.FrameId == "world");
            if (oldWorld != null && (newWorld.Origin.DistanceSquared(oldWorld.Origin) != 0 || newWorld.YawDegrees != oldWorld.YawDegrees))
                throw new InvalidDataException("Physical boundary moved the stationary world frame.");
            foreach (var actor in previous.Participants)
                if (!actor.ObservedIds.OrderBy(x => x).SequenceEqual(boundary.Participants.Single(p => p.ParticipantId == actor.ParticipantId).ObservedIds.OrderBy(x => x)))
                    throw new InvalidDataException("Physical boundary changed an approved observation.");
            foreach (var old in previous.Entities)
            {
                var current = boundary.Entities.SingleOrDefault(e => e.EntityId == old.EntityId);
                if (current == null || current.Kind != old.Kind || current.LeaderId != old.LeaderId || current.RequiredRoleId != old.RequiredRoleId ||
                    current.Revision < old.Revision || current.Active != old.Active && current.Revision == old.Revision ||
                    old.Kind == EntityKind.Incident && !old.Active && current.Active)
                    throw new InvalidDataException("Physical boundary changed ownership or removed known entities.");
            }
            if (boundary.Entities.Any(e => !previous.Entities.Any(old => old.EntityId == e.EntityId) &&
                (e.Kind != EntityKind.Incident || !e.EntityId.StartsWith("incident-", StringComparison.Ordinal))))
                throw new InvalidDataException("Unregistered physical entity in boundary.");
        }

        private static Boundary Capture(ServerSimulationView view) => new Boundary { Sequence = view.Sequence, Checkpoint = view.Checkpoint,
            Paused = view.Paused, Frames = view.Frames.Select(f => f.Copy()).ToArray(),
            Participants = view.Participants.Select(p => p.Copy()).ToArray(), Entities = view.Entities.Select(e => e.Copy()).ToArray() };

        private void ValidateBoundary(Boundary b, long tick)
        {
            if (b == null || b.Frames == null || b.Participants == null || b.Entities == null || !Roster(b.Participants).SequenceEqual(roster))
                throw new InvalidDataException("Malformed physical boundary roster.");
            var state = new WorldState { SchemaVersion = 3, WorldId = worldId, ShiftId = shiftId, SpatialProfileId = spatialId,
                SimulationDefinitionHash = definitionHash, SimulationTick = tick, SimulationCheckpoint = b.Checkpoint,
                Frames = b.Frames, Participants = b.Participants, Entities = b.Entities };
            new AuthoritativeShift(state, this, (a, e) => true);
        }

        private void AppendEnvelope(Envelope envelope)
        {
            var bytes = utf8.GetBytes(JsonUtility.ToJson(envelope) + "\n");
            if (log.Length + bytes.Length > MaxJournalBytes) throw new IOException("V3 journal storage budget reached; preserve records for rotation.");
            try
            {
                log.Write(bytes, 0, bytes.Length); log.Flush(true);
                ordinal = envelope.Ordinal; lastHash = envelope.Hash; lastTick = envelope.Tick;
            }
            catch { faulted = true; throw; }
        }
        private void WriteCheckpoint(Envelope envelope)
        {
            var path = Path.Combine(directory, "checkpoint-v3.json"); var temporary = path + ".tmp";
            var bytes = utf8.GetBytes(JsonUtility.ToJson(envelope));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        }

        private IEnumerable<Line> Lines()
        {
            log.Position = 0; var buffer = new byte[65536]; var offset = 0L;
            using (var line = new MemoryStream())
            {
                int count;
                while ((count = log.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var start = 0;
                    for (var i = 0; i < count; i++)
                    {
                        if (buffer[i] != (byte)'\n') continue;
                        line.Write(buffer, start, i - start); CheckLineSize(line.Length);
                        yield return new Line { Text = utf8.GetString(line.ToArray()), End = offset + i + 1 };
                        line.SetLength(0); start = i + 1;
                    }
                    line.Write(buffer, start, count - start); CheckLineSize(line.Length); offset += count;
                }
            }
        }
        private static void CheckLineSize(long size)
        { if (size > MaxCompressed * 4L / 3 + 4096) throw new InvalidDataException("Oversized v3 log line."); }

        private Envelope Encode(string kind, long position, long actionSequence, long tick, string previous, string payload, int maximum)
        {
            var raw = utf8.GetBytes(payload); if (raw.Length > maximum) throw new IOException("V3 uncompressed record exceeds its bound.");
            byte[] compressed;
            using (var stream = new MemoryStream())
            { using (var compressor = new DeflateStream(stream, System.IO.Compression.CompressionLevel.Fastest, true)) compressor.Write(raw, 0, raw.Length); compressed = stream.ToArray(); }
            if (compressed.Length > MaxCompressed) throw new IOException("V3 compressed record exceeds its bound.");
            var envelope = new Envelope { Kind = kind, Ordinal = position, Sequence = actionSequence, Tick = tick, WorldId = worldId,
                ShiftId = shiftId, DefinitionHash = definitionHash, PreviousHash = previous, PlainBytes = raw.Length, Payload = Convert.ToBase64String(compressed) };
            envelope.Hash = Digest(envelope); return envelope;
        }
        private Envelope DecodeEnvelope(string text, string kind)
        {
            var e = ReadJson<Envelope>(text);
            if (e == null || e.Schema != 3 || e.Ordinal < 0 || e.Sequence < 0 || e.Tick < 0 || e.WorldId != worldId || e.ShiftId != shiftId ||
                e.DefinitionHash != definitionHash || e.PreviousHash == null || e.PreviousHash.Length != 64 || e.Payload == null ||
                e.Payload.Length > MaxCompressed * 4L / 3 + 4 || e.PlainBytes < 0 || e.PlainBytes > MaxCheckpointPlain ||
                kind != null && e.Kind != kind || e.Hash != Digest(e)) throw new InvalidDataException("V3 envelope identity/version/hash mismatch.");
            return e;
        }
        private string Inflate(Envelope e, int maximum)
        {
            try
            {
                if (e.PlainBytes > maximum) throw new InvalidDataException("V3 decompression bound exceeded.");
                var bytes = Convert.FromBase64String(e.Payload); if (bytes.Length > MaxCompressed) throw new InvalidDataException("V3 compressed bound exceeded.");
                using (var input = new MemoryStream(bytes, false))
                using (var decoder = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[65536]; int count;
                    while ((count = decoder.Read(buffer, 0, buffer.Length)) > 0)
                    { if (output.Length + count > e.PlainBytes) throw new InvalidDataException("V3 expanded data exceeds declared length."); output.Write(buffer, 0, count); }
                    if (output.Length != e.PlainBytes) throw new InvalidDataException("V3 expanded length differs.");
                    return utf8.GetString(output.ToArray());
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is DecoderFallbackException)
            { throw new InvalidDataException("Malformed compressed v3 record.", ex); }
        }
        private static T ReadJson<T>(string text)
        { try { return JsonUtility.FromJson<T>(text); } catch (ArgumentException e) { throw new InvalidDataException("Malformed v3 record JSON.", e); } }
        private static string[] Roster(ParticipantState[] people)
        {
            if (people == null || people.Any(p => p == null)) throw new InvalidDataException("Missing v3 roster.");
            return people.Select(p => p.ParticipantId + "|" + p.TeamId + "|" + p.RoleId + "|" + p.IsInstructor).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
        private static string Digest(Envelope e)
        {
            var text = string.Join("\n", e.Schema.ToString(CultureInfo.InvariantCulture), e.Kind, e.WorldId, e.ShiftId, e.DefinitionHash,
                e.Ordinal.ToString(CultureInfo.InvariantCulture), e.Sequence.ToString(CultureInfo.InvariantCulture), e.Tick.ToString(CultureInfo.InvariantCulture),
                e.PlainBytes.ToString(CultureInfo.InvariantCulture), e.PreviousHash, e.Payload);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public void Dispose() { log.Dispose(); lease.Dispose(); }
    }
}
