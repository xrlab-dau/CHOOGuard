using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using ChooGuard.Foundation.Simulation;
public class IncidentDirectorTests
{
    static void Check(bool ok, string message) { if(!ok) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Expected rejection"); }
    static IncidentSchedule Profile() => new IncidentSchedule { ProfileId = "synthetic-v1", FirstOnsetTick = 2, MinimumQuietTicks = 1, MaximumQuietTicks = 3,
        Choices = new[] { new IncidentChoice { Id = "fire-a", ResourceKey = "area-a" }, new IncidentChoice { Id = "power-a", ResourceKey = "area-a", Kind = FoundationIncidentKind.PowerLoss },
            new IncidentChoice { Id = "train-b", ResourceKey = "train-b", Kind = FoundationIncidentKind.TrainFault } } };
    [Test] public void BoundedContract()
    {
        var d = new IncidentDirector(Profile(), 12); Check(d.AdvanceOne() == null, "no countdown surprise before onset");
        Check(d.AdvanceOne() != null, "first onset");
        for (var i = 0; i < 30; i++) d.AdvanceOne();
        Check(d.Active.Count == 2 && d.Active.Select(e => Profile().Choices.Single(c => c.Id == e.ChoiceId).ResourceKey).Distinct().Count() == 2, "bounded resource conflict");
        var checkpoint = d.ExportCheckpoint(); var restored = new IncidentDirector(Profile(), 999); restored.Restore(checkpoint);
        Check(restored.ExportCheckpoint() == checkpoint, "exact replay state");
        var first = d.Active[0].Id; Reject(() => d.Complete(first)); Check(d.ExportCheckpoint() == checkpoint, "incomplete recovery atomic");
        d.Mitigate(first); restored.Mitigate(first); d.Complete(first); restored.Complete(first);
        for (var i = 0; i < 20; i++) { d.AdvanceOne(); restored.AdvanceOne(); }
        Check(d.ExportCheckpoint() == restored.ExportCheckpoint(), "identical continuation");
        Check(d.Active.All(e => e.Id != first), "never reuse episode identity");
        var bytes = Convert.FromBase64String(checkpoint.Substring(6)); bytes[9] ^= 1;
        var before = restored.ExportCheckpoint(); Reject(() => restored.Restore("CGID1:" + Convert.ToBase64String(bytes)));
        Check(before == restored.ExportCheckpoint(), "invalid restore leaves state untouched");
        var profile = Profile(); var copied = new IncidentDirector(profile, 12); profile.Choices[0].ResourceKey = "changed";
        copied.AdvanceOne(); copied.AdvanceOne(); Check(copied.Active.Count == 1, "definition copied");
        var mismatch = Profile(); mismatch.Choices[0].Kind = FoundationIncidentKind.EmergencyStop;
        Reject(() => new IncidentDirector(mismatch, 12).Restore(checkpoint));
        Reject(() => new IncidentDirector(Profile(), 0));
        string Rehash(byte[] payload)
        { using (var sha = SHA256.Create()) return "CGID1:" + Convert.ToBase64String(payload.Concat(sha.ComputeHash(payload)).ToArray()); }
        var plain = Convert.FromBase64String(checkpoint.Substring(6)); plain = plain.Take(plain.Length - 32).ToArray();
        var hugeLength = new byte[] {1,0,0,0,128,128,64}.Concat(plain.Skip(5)).ToArray();
        Reject(() => restored.Restore(Rehash(hugeLength)));
        using (var stream = new MemoryStream(plain)) using (var reader = new BinaryReader(stream))
        {
            reader.ReadInt32(); reader.ReadString(); reader.ReadUInt64();
            var tickOffset = (int)stream.Position;
            var fabricated = (byte[])plain.Clone(); Array.Copy(BitConverter.GetBytes((long)0), 0, fabricated, tickOffset, 8);
            Reject(() => restored.Restore(Rehash(fabricated)));
            fabricated = (byte[])plain.Clone(); Array.Copy(BitConverter.GetBytes(long.MaxValue), 0, fabricated, tickOffset + 8, 8);
            Reject(() => restored.Restore(Rehash(fabricated)));
        }
        Check(before == restored.ExportCheckpoint(), "valid digest negative fixtures are atomic");
        Console.WriteLine("incident-director-tests: 13 passed");
    }
}
