using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Foundation.Simulation
{
    /// <summary>Version2 little-endian IEEE754/UTF8/SHA256/Base64, with explicit legacy version1 migration.
    /// A checkpoint can be embedded as a JSON string without lossy decimal-number conversion.</summary>
    internal static class CrowdCheckpoint
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private const int MaximumBytes = 8 * 1024 * 1024;

        public static string Encode(CrowdSnapshot s)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream, Utf8, true))
            {
                w.Write(2); w.Write(s.SchemaVersion); Text(w, s.ModelVersion);
                w.Write(s.Seed); w.Write(s.Tick); w.Write(s.AcceptedSubsteps); w.Write(s.ElapsedSeconds); w.Write(s.GeometryRevision);
                var d = s.Definition; w.Write(d.SchemaVersion); Text(w, d.ProfileId); var p = d.Parameters;
                foreach (var v in new[] { p.TimeGapSeconds, p.PersonRepulsion, p.PersonRepulsionRangeM, p.WallRepulsion, p.WallRepulsionRangeM,
                    p.MaxSubstepSeconds, p.VelocityToleranceMS, p.ComplementarityTolerance, p.GeometryToleranceM }) w.Write(v);
                w.Write(p.MaxAgents); w.Write(p.MaxProjectionIterations); w.Write(p.MaxSubstepsPerTick); w.Write(p.MaxActiveSetConstraints);
                w.Write(d.Spaces.Length); foreach (var space in d.Spaces) { Text(w, space.Id); w.Write(space.PeriodicLengthXM); }
                w.Write(d.Walls.Length); foreach (var wall in d.Walls)
                { Text(w, wall.Id); Text(w, wall.ContactSpaceId); Vector(w, wall.A); Vector(w, wall.B);
                    w.Write(wall.Enabled); w.Write(wall.HasVerticalBounds); w.Write(wall.BottomElevationM); w.Write(wall.TopElevationM); }
                w.Write(s.Agents.Length); foreach (var a in s.Agents)
                {
                    Text(w, a.Id); Text(w, a.ContactSpaceId); Text(w, a.RegionId); Text(w, a.FrameId); Text(w, a.SurfaceId);
                    w.Write(a.RadiusM); w.Write(a.PreferredSpeedMS); Vector(w, a.Position); Vector(w, a.Velocity);
                    Vector(w, a.Goal); Vector(w, a.DesiredVelocity); w.Write((int)a.IntentMode);
                    Text(w, a.LeaderId); w.Write(a.PathCursor); w.Write(a.KnownClosedPortalIds.Length);
                    foreach (var id in a.KnownClosedPortalIds) Text(w, id);
                    w.Write(a.FootElevationM); w.Write(a.HeightM); Vector(w, a.SupportGradient); w.Write(a.Pinned);
                }
                w.Flush(); if (stream.Length > MaximumBytes - 32) throw new ArgumentException("Crowd checkpoint exceeds the encoded size budget.");
                using (var sha = SHA256.Create()) w.Write(sha.ComputeHash(stream.ToArray()));
                w.Flush(); return "CGC2:" + Convert.ToBase64String(stream.ToArray());
            }
        }

        public static CrowdSnapshot Decode(string checkpoint)
        {
            try
            {
                if (checkpoint == null || !(checkpoint.StartsWith("CGC1:", StringComparison.Ordinal) || checkpoint.StartsWith("CGC2:", StringComparison.Ordinal)) || checkpoint.Length > MaximumBytes * 4 / 3 + 8)
                    throw new ArgumentException("Unknown or oversized crowd checkpoint envelope.");
                var bytes = Convert.FromBase64String(checkpoint.Substring(5));
                if (bytes.Length > MaximumBytes || bytes.Length < 32) throw new ArgumentException("Invalid crowd checkpoint payload size.");
                var payloadLength = bytes.Length - 32;
                using (var sha = SHA256.Create())
                {
                    var digest = sha.ComputeHash(bytes, 0, payloadLength); var mismatch = 0;
                    for (var i = 0; i < 32; i++) mismatch |= digest[i] ^ bytes[payloadLength + i];
                    if (mismatch != 0) throw new ArgumentException("Crowd checkpoint integrity digest mismatch.");
                }
                using (var stream = new MemoryStream(bytes, 0, payloadLength, false))
                using (var r = new BinaryReader(stream, Utf8, true))
                {
                    var version = r.ReadInt32();
                    if (version != 1 && version != 2 || checkpoint[3] - '0' != version) throw new ArgumentException("Unknown or mismatched crowd checkpoint wire version.");
                    var s = new CrowdSnapshot { SchemaVersion = r.ReadInt32(), ModelVersion = Text(r), Seed = r.ReadInt64(),
                        Tick = r.ReadInt64(), AcceptedSubsteps = r.ReadInt64(), ElapsedSeconds = r.ReadDouble() };
                    if (version == 1)
                    {
                        if (s.SchemaVersion != 1 || s.ModelVersion != CrowdMotionModel.LegacyVersion) throw new ArgumentException("Invalid legacy crowd checkpoint.");
                        s.SchemaVersion = 2; s.ModelVersion = CrowdMotionModel.Version;
                    }
                    else s.GeometryRevision = r.ReadInt64();
                    var d = new CrowdDefinition { SchemaVersion = r.ReadInt32(), ProfileId = Text(r) }; s.Definition = d;
                    d.Parameters = new CrowdParameters { TimeGapSeconds = r.ReadDouble(), PersonRepulsion = r.ReadDouble(), PersonRepulsionRangeM = r.ReadDouble(),
                        WallRepulsion = r.ReadDouble(), WallRepulsionRangeM = r.ReadDouble(), MaxSubstepSeconds = r.ReadDouble(),
                        VelocityToleranceMS = r.ReadDouble(), ComplementarityTolerance = r.ReadDouble(), GeometryToleranceM = r.ReadDouble(),
                        MaxAgents = r.ReadInt32(), MaxProjectionIterations = r.ReadInt32(), MaxSubstepsPerTick = r.ReadInt32(), MaxActiveSetConstraints = r.ReadInt32() };
                    d.Spaces = new CrowdSpace[Count(r, 1000)]; for (var i = 0; i < d.Spaces.Length; i++)
                        d.Spaces[i] = new CrowdSpace { Id = Text(r), PeriodicLengthXM = r.ReadDouble() };
                    d.Walls = new CrowdWall[Count(r, 10000)]; for (var i = 0; i < d.Walls.Length; i++)
                    {
                        var wall = new CrowdWall { Id = Text(r), ContactSpaceId = Text(r), A = Vector(r), B = Vector(r) };
                        if (version == 2) { wall.Enabled = Boolean(r); wall.HasVerticalBounds = Boolean(r); wall.BottomElevationM = r.ReadDouble(); wall.TopElevationM = r.ReadDouble(); }
                        d.Walls[i] = wall;
                    }
                    s.Agents = new CrowdAgent[Count(r, 1000)]; for (var i = 0; i < s.Agents.Length; i++)
                    {
                        var a = new CrowdAgent { Id = Text(r), ContactSpaceId = Text(r), RegionId = Text(r), FrameId = Text(r), SurfaceId = Text(r),
                            RadiusM = r.ReadDouble(), PreferredSpeedMS = r.ReadDouble(), Position = Vector(r), Velocity = Vector(r),
                            Goal = Vector(r), DesiredVelocity = Vector(r), IntentMode = (CrowdIntentMode)r.ReadInt32(),
                            LeaderId = Text(r), PathCursor = r.ReadInt32() };
                        a.KnownClosedPortalIds = new string[Count(r, 256)]; for (var j = 0; j < a.KnownClosedPortalIds.Length; j++) a.KnownClosedPortalIds[j] = Text(r);
                        if (version == 2) { a.FootElevationM = r.ReadDouble(); a.HeightM = r.ReadDouble(); a.SupportGradient = Vector(r); a.Pinned = Boolean(r); }
                        else a.HeightM = Math.Max(1.8, 2 * a.RadiusM);
                        s.Agents[i] = a;
                    }
                    if (stream.Position != stream.Length) throw new ArgumentException("Trailing bytes in crowd checkpoint.");
                    return s;
                }
            }
            catch (Exception e) when (e is FormatException || e is IOException || e is DecoderFallbackException)
            { throw new ArgumentException("Malformed or truncated crowd checkpoint.", e); }
        }
        private static int Count(BinaryReader r, int maximum)
        { var count = r.ReadInt32(); if (count < 0 || count > maximum) throw new ArgumentException("Crowd checkpoint collection exceeds its schema limit."); return count; }
        private static void Text(BinaryWriter w, string text)
        { var bytes = Utf8.GetBytes(text); w.Write(bytes.Length); w.Write(bytes); }
        private static string Text(BinaryReader r)
        { var n = Count(r, 1024); var bytes = r.ReadBytes(n); if (bytes.Length != n) throw new EndOfStreamException(); return Utf8.GetString(bytes); }
        private static void Vector(BinaryWriter w, CrowdVector v) { w.Write(v.X); w.Write(v.Y); }
        private static bool Boolean(BinaryReader r) { var value = r.ReadByte(); if (value > 1) throw new ArgumentException("Noncanonical crowd checkpoint boolean."); return value != 0; }
        private static CrowdVector Vector(BinaryReader r) => new CrowdVector(r.ReadDouble(), r.ReadDouble());
    }
}
