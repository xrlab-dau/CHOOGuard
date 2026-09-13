using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Foundation.Simulation
{
    public enum FoundationIncidentKind { Fire, PowerLoss, PublicAddressFailure, RouteRestriction, TrainFault, EmergencyStop }
    [Serializable] public sealed class IncidentSchedule
    {
        public string ProfileId;
        public int MaximumActive = 2;
        public long FirstOnsetTick = 1200, MinimumQuietTicks = 1200, MaximumQuietTicks = 3600;
        public IncidentChoice[] Choices = Array.Empty<IncidentChoice>();
    }
    [Serializable] public sealed class IncidentChoice
    {
        public string Id, ResourceKey;
        public FoundationIncidentKind Kind;
    }
    [Serializable] public sealed class IncidentEpisode
    {
        public string Id, ChoiceId;
        public long StartedTick;
        public bool Mitigated;
        public IncidentEpisode Copy() => (IncidentEpisode)MemberwiseClone();
    }
    /// <summary>Server-only deterministic scheduling of authored incident choices. It does not invent procedures or reset physics.</summary>
    public sealed class IncidentDirector
    {
        private readonly IncidentSchedule schedule;
        private readonly string signature;
        private ulong random;
        private long tick, nextOnset, issued, lastIssuedTick;
        private readonly List<IncidentEpisode> active = new List<IncidentEpisode>();
        public long Tick => tick;
        public IReadOnlyList<IncidentEpisode> Active => active.Select(e => e.Copy()).ToArray();
        public IncidentDirector(IncidentSchedule definition, ulong seed)
        {
            if (definition == null || !Id(definition.ProfileId) || seed == 0 || definition.MaximumActive < 1 || definition.MaximumActive > 2 ||
                definition.FirstOnsetTick < 1 || definition.FirstOnsetTick > 1728000 || definition.MinimumQuietTicks < 1 ||
                definition.MaximumQuietTicks < definition.MinimumQuietTicks || definition.MaximumQuietTicks > 1728000 ||
                definition.Choices == null || definition.Choices.Length < 1 || definition.Choices.Length > 128 ||
                definition.Choices.Any(c => c == null || !Id(c.Id) || !Id(c.ResourceKey) || !Enum.IsDefined(typeof(FoundationIncidentKind), c.Kind)) ||
                definition.Choices.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != definition.Choices.Length)
                throw new ArgumentException("Invalid authored incident schedule.");
            schedule = new IncidentSchedule { ProfileId = definition.ProfileId, MaximumActive = definition.MaximumActive,
                FirstOnsetTick = definition.FirstOnsetTick, MinimumQuietTicks = definition.MinimumQuietTicks, MaximumQuietTicks = definition.MaximumQuietTicks,
                Choices = definition.Choices.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => new IncidentChoice { Id = c.Id, Kind = c.Kind, ResourceKey = c.ResourceKey }).ToArray() };
            signature = DefinitionDigest(); random = seed; nextOnset = schedule.FirstOnsetTick;
        }
        public IncidentEpisode AdvanceOne(ISet<string> unavailableChoices = null)
        {
            if (tick == long.MaxValue) throw new InvalidOperationException("Incident clock exhausted.");
            var at = tick + 1;
            if (at < nextOnset || active.Count >= schedule.MaximumActive) { tick = at; return null; }
            var occupied = new HashSet<string>(active.Select(e => schedule.Choices.Single(c => c.Id == e.ChoiceId).ResourceKey), StringComparer.Ordinal);
            var choices = schedule.Choices.Where(c => !occupied.Contains(c.ResourceKey) && !(unavailableChoices?.Contains(c.Id) ?? false)).ToArray();
            if (choices.Length == 0) { tick = at; return null; }
            if (issued == long.MaxValue || at > long.MaxValue - schedule.MaximumQuietTicks) throw new InvalidOperationException("Incident identity/clock exhausted.");
            var choice = choices[(int)(Next() % (ulong)choices.Length)];
            var episode = new IncidentEpisode { Id = "incident-" + (++issued).ToString(System.Globalization.CultureInfo.InvariantCulture), ChoiceId = choice.Id, StartedTick = at };
            tick = lastIssuedTick = at;
            nextOnset = at + schedule.MinimumQuietTicks + (long)(Next() % (ulong)(schedule.MaximumQuietTicks - schedule.MinimumQuietTicks + 1));
            active.Add(episode); return episode.Copy();
        }
        // Only the authoritative response evaluator calls these after checking command/physical preconditions.
        public void Mitigate(string episodeId)
        { var e = active.SingleOrDefault(a => a.Id == episodeId); if (e == null) throw new ArgumentException("Unknown active incident."); e.Mitigated = true; }
        public void Complete(string episodeId)
        { var e = active.SingleOrDefault(a => a.Id == episodeId); if (e == null || !e.Mitigated) throw new ArgumentException("Incident has not reached recovery."); active.Remove(e); }
        public string ExportCheckpoint()
        {
            using (var output = new MemoryStream())
            using (var w = new BinaryWriter(output, Encoding.UTF8, true))
            {
                w.Write(1); w.Write(signature); w.Write(random); w.Write(tick); w.Write(nextOnset); w.Write(issued); w.Write(lastIssuedTick); w.Write(active.Count);
                foreach (var e in active) { w.Write(e.Id); w.Write(e.ChoiceId); w.Write(e.StartedTick); w.Write(e.Mitigated); }
                w.Flush(); using (var hash = SHA256.Create()) w.Write(hash.ComputeHash(output.ToArray()));
                w.Flush(); return "CGID1:" + Convert.ToBase64String(output.ToArray());
            }
        }
        public void Restore(string checkpoint)
        {
            try
            {
                if (checkpoint == null || !checkpoint.StartsWith("CGID1:", StringComparison.Ordinal) || checkpoint.Length > 4096) throw new ArgumentException("Invalid incident checkpoint.");
                var bytes = Convert.FromBase64String(checkpoint.Substring(6)); var length = bytes.Length - 32;
                if (length < 1) throw new ArgumentException("Truncated incident checkpoint.");
                using (var hash = SHA256.Create()) if (!hash.ComputeHash(bytes, 0, length).SequenceEqual(bytes.Skip(length))) throw new ArgumentException("Incident checkpoint digest mismatch.");
                using (var input = new MemoryStream(bytes, 0, length, false))
                using (var r = new BinaryReader(input, new UTF8Encoding(false, true), true))
                {
                    if (r.ReadInt32() != 1 || ReadText(r) != signature) throw new ArgumentException("Incident definition mismatch.");
                    var nextRandom = r.ReadUInt64(); var at = r.ReadInt64(); var onset = r.ReadInt64(); var countIssued = r.ReadInt64(); var last = r.ReadInt64(); var count = r.ReadInt32();
                    if (nextRandom == 0 || at < 0 || onset < 1 || countIssued < 0 || count < 0 || count > schedule.MaximumActive || countIssued < count)
                        throw new ArgumentException("Invalid incident checkpoint counters.");
                    if (countIssued == 0 ? count != 0 || last != 0 || onset != schedule.FirstOnsetTick :
                        last < schedule.FirstOnsetTick || last > at || countIssued - 1 > (last - schedule.FirstOnsetTick) / schedule.MinimumQuietTicks ||
                        last > long.MaxValue - schedule.MaximumQuietTicks || onset < last + schedule.MinimumQuietTicks || onset > last + schedule.MaximumQuietTicks)
                        throw new ArgumentException("Incident counters contradict the authored schedule.");
                    var restored = new List<IncidentEpisode>(); var resources = new HashSet<string>(StringComparer.Ordinal);
                    for (var i = 0; i < count; i++)
                    {
                        var id = ReadText(r); var choiceId = ReadText(r); var started = r.ReadInt64(); var flag = r.ReadByte();
                        var choice = schedule.Choices.SingleOrDefault(c => c.Id == choiceId);
                        if (!id.StartsWith("incident-", StringComparison.Ordinal) || !long.TryParse(id.Substring(9), System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out var serial) || serial < 1 || serial > countIssued ||
                            id != "incident-" + serial.ToString(System.Globalization.CultureInfo.InvariantCulture) || choice == null ||
                            started < schedule.FirstOnsetTick || started > last || serial - 1 > (started - schedule.FirstOnsetTick) / schedule.MinimumQuietTicks ||
                            countIssued - serial > (last - started) / schedule.MinimumQuietTicks || serial == countIssued && started != last ||
                            flag > 1 || !resources.Add(choice.ResourceKey) || restored.Any(e => e.Id == id))
                            throw new ArgumentException("Invalid incident episode.");
                        restored.Add(new IncidentEpisode { Id = id, ChoiceId = choiceId, StartedTick = started, Mitigated = flag == 1 });
                    }
                    if (input.Position != input.Length) throw new ArgumentException("Trailing incident checkpoint bytes.");
                    random = nextRandom; tick = at; nextOnset = onset; issued = countIssued; lastIssuedTick = last; active.Clear(); active.AddRange(restored);
                }
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is DecoderFallbackException)
            { throw new ArgumentException("Malformed incident checkpoint.", e); }
        }
        private ulong Next() { random ^= random << 13; random ^= random >> 7; random ^= random << 17; return random; }
        private string DefinitionDigest()
        {
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(schedule.ProfileId); w.Write(schedule.MaximumActive); w.Write(schedule.FirstOnsetTick); w.Write(schedule.MinimumQuietTicks); w.Write(schedule.MaximumQuietTicks);
                foreach (var c in schedule.Choices) { w.Write(c.Id); w.Write((int)c.Kind); w.Write(c.ResourceKey); }
                w.Flush(); using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        private static string ReadText(BinaryReader r)
        {
            var first = r.ReadByte(); var length = (int)first;
            if (first >= 128)
            { var second = r.ReadByte(); if (second != 1 || first != 128) throw new ArgumentException("Oversized/noncanonical incident identifier."); length = 128; }
            if (length > r.BaseStream.Length - r.BaseStream.Position) throw new ArgumentException("Truncated incident identifier.");
            var bytes = r.ReadBytes(length); var text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.Length > 128) throw new ArgumentException("Oversized incident identifier."); return text;
        }
        private static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(c => c >= 32 && c <= 126);
    }
}
