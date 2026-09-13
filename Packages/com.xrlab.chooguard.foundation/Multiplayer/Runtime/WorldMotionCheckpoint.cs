using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>CGWM1 server-only little-endian binary envelope. All crowd doubles remain in CGC2;
    /// frame/pose floats and CGMP1 navigation bytes retain their exact representations.</summary>
    internal static class WorldMotionCheckpoint
    {
        private const int MaximumBytes = 16 * 1024 * 1024;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        public static string Encode(WorldMotionSnapshot state, string crowdCheckpoint)
        {
            if (state.PathCheckpoint == null || state.PathCheckpoint.Length > 3 * 1024 * 1024 || crowdCheckpoint == null || crowdCheckpoint.Length > 12 * 1024 * 1024)
                throw new ArgumentException("World motion checkpoint field exceeds size limit.");
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(1); Text(writer, state.ProfileId); Text(writer, state.GeometrySignature);
            writer.Write(state.Frames.Length);
            foreach (var frame in state.Frames) { Text(writer, frame.FrameId); Point(writer, frame.Origin); writer.Write(frame.YawDegrees); }
            writer.Write(state.ClosedPortalIds.Length); foreach (var id in state.ClosedPortalIds) Text(writer, id);
            writer.Write(state.BodyIds.Length);
            for (var i = 0; i < state.BodyIds.Length; i++)
            {
                var p = state.Poses[i]; Text(writer, state.BodyIds[i]); Text(writer, p.RegionId); Text(writer, p.FrameId); Text(writer, p.PortalId);
                Point(writer, p.Position); Point(writer, p.LocalPosition);
            }
            Text(writer, crowdCheckpoint); Text(writer, state.PathCheckpoint); writer.Flush();
            if (stream.Length > MaximumBytes - 32) throw new ArgumentException("World motion checkpoint exceeds size limit.");
            using var sha = SHA256.Create(); writer.Write(sha.ComputeHash(stream.ToArray())); writer.Flush();
            return "CGWM1:" + Convert.ToBase64String(stream.ToArray());
        }
        public static WorldMotionSnapshot Decode(string checkpoint)
        {
            try
            {
                if (checkpoint == null || !checkpoint.StartsWith("CGWM1:", StringComparison.Ordinal) || checkpoint.Length > MaximumBytes * 4 / 3 + 10)
                    throw new ArgumentException("Unknown or oversized world motion checkpoint.");
                var bytes = Convert.FromBase64String(checkpoint.Substring(6));
                if (bytes.Length < 36 || bytes.Length > MaximumBytes) throw new ArgumentException("Invalid world motion payload length.");
                var length = bytes.Length - 32;
                using (var sha = SHA256.Create())
                {
                    var digest = sha.ComputeHash(bytes, 0, length); var mismatch = 0;
                    for (var i = 0; i < digest.Length; i++) mismatch |= digest[i] ^ bytes[length + i];
                    if (mismatch != 0) throw new ArgumentException("World motion checkpoint digest mismatch.");
                }
                using var stream = new MemoryStream(bytes, 0, length, false); using var reader = new BinaryReader(stream, Utf8, true);
                if (reader.ReadInt32() != 1) throw new ArgumentException("Unsupported world motion checkpoint version.");
                var state = new WorldMotionSnapshot { ProfileId = Text(reader, 128), GeometrySignature = Text(reader, 64) };
                Id(state.ProfileId);
                if (state.GeometrySignature.Length != 64 || state.GeometrySignature.Any(c => c < '0' || c > '9' && c < 'a' || c > 'f')) throw new ArgumentException("Invalid geometry signature.");
                state.Frames = new SpatialFrame[Count(reader, 16, 1)]; var frameIds = new HashSet<string>();
                for (var i = 0; i < state.Frames.Length; i++)
                {
                    var frame = new SpatialFrame { FrameId = Text(reader, 128), Origin = Point(reader), YawDegrees = reader.ReadSingle() };
                    Id(frame.FrameId); if (!frameIds.Add(frame.FrameId) || float.IsNaN(frame.YawDegrees) || float.IsInfinity(frame.YawDegrees)) throw new ArgumentException("Invalid or duplicate frame.");
                    state.Frames[i] = frame;
                }
                state.ClosedPortalIds = new string[Count(reader, 12)]; var closed = new HashSet<string>();
                for (var i = 0; i < state.ClosedPortalIds.Length; i++)
                { var id = Text(reader, 128); Id(id); if (!closed.Add(id)) throw new ArgumentException("Duplicate portal."); state.ClosedPortalIds[i] = id; }
                var bodies = Count(reader, 120, 1); state.BodyIds = new string[bodies]; state.Poses = new SpatialPose[bodies]; var ids = new HashSet<string>();
                for (var i = 0; i < bodies; i++)
                {
                    var id = Text(reader, 128); Id(id); if (!ids.Add(id)) throw new ArgumentException("Duplicate body."); state.BodyIds[i] = id;
                    var pose = new SpatialPose { RegionId = Text(reader, 128), FrameId = Text(reader, 128), PortalId = Text(reader, 128), Position = Point(reader), LocalPosition = Point(reader) };
                    Id(pose.RegionId); Id(pose.FrameId); if (!frameIds.Contains(pose.FrameId)) throw new ArgumentException("Missing pose frame."); state.Poses[i] = pose;
                }
                var crowd = Text(reader, 12 * 1024 * 1024);
                if (!crowd.StartsWith("CGC2:", StringComparison.Ordinal)) throw new ArgumentException("World motion requires an exact CGC2 checkpoint.");
                state.Crowd = CrowdMotionModel.FromCheckpoint(crowd).ExportSnapshot();
                state.PathCheckpoint = Text(reader, 3 * 1024 * 1024);
                if (stream.Position != stream.Length) throw new ArgumentException("Trailing world motion checkpoint bytes.");
                return state;
            }
            catch (Exception e) when (e is FormatException || e is IOException || e is DecoderFallbackException)
            { throw new ArgumentException("Malformed or truncated world motion checkpoint.", e); }
        }
        private static int Count(BinaryReader reader, int maximum, int minimum = 0)
        { var n = reader.ReadInt32(); if (n < minimum || n > maximum) throw new ArgumentException("Checkpoint length/count limit exceeded."); return n; }
        private static void Text(BinaryWriter writer, string text)
        { var bytes = Utf8.GetBytes(text ?? ""); writer.Write(bytes.Length); writer.Write(bytes); }
        private static string Text(BinaryReader reader, int maximum)
        { var n = Count(reader, maximum); var bytes = reader.ReadBytes(n); if (bytes.Length != n) throw new EndOfStreamException(); return Utf8.GetString(bytes); }
        private static void Point(BinaryWriter writer, Point3 p) { writer.Write(p.X); writer.Write(p.Y); writer.Write(p.Z); }
        private static Point3 Point(BinaryReader reader)
        { var p = new Point3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); if (!p.Finite) throw new ArgumentException("Nonfinite checkpoint point."); return p; }
        private static void Id(string id) { if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentException("Invalid checkpoint identity."); }
    }
}
