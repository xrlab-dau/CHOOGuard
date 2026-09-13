using NUnit.Framework;
using System;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
public class SnapshotPayloadTests
{
    static void Check(bool ok, string message) { if(!ok) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Expected rejection"); }
    [Test] public void BoundedContract()
    {
        var text = "{\"label\":\"팀 보고 · 미확인\",\"members\":[1,2,3]}";
        Check(SnapshotPayload.Decode(SnapshotPayload.Encode(text)) == text, "unicode roundtrip");
        var value = new string('x', 200000); var encoded = SnapshotPayload.Encode(value);
        Check(encoded.Length < value.Length / 10 && SnapshotPayload.Decode(encoded) == value, "bounded large projection");
        var bad = (byte[])encoded.Clone(); bad[4]=1; bad[5]=0; bad[6]=0; bad[7]=0;
        Reject(() => SnapshotPayload.Decode(bad));
        Reject(() => SnapshotPayload.Encode(new string('x', SnapshotPayload.MaximumPlainBytes+1)));
        Reject(() => SnapshotPayload.Decode(new byte[SnapshotFragments.MaximumPayloadBytes+1]));
        var corrupted = SnapshotPayload.Encode(text); corrupted[0]^=1;
        Reject(() => SnapshotPayload.Decode(corrupted));
        var original = SnapshotPayload.Encode(text);
        for (var n = 0; n < original.Length; n++) Reject(() => SnapshotPayload.Decode(original.Take(n).ToArray()));
        Reject(() => SnapshotPayload.Decode(original.Concat(new byte[] { 0 }).ToArray()));
        var invalidUtf8 = new byte[] { 67,71,76,51,1,0,0,0,0,255 };
        Reject(() => SnapshotPayload.Decode(invalidUtf8));
        Reject(() => SnapshotPayload.Decode(new byte[] {67,71,76,51,3,0,0,0,128,0,0}));
        Check(SnapshotPayload.Decode(SnapshotPayload.Encode("")) == "", "empty projection");
        var random = new Random(15);
        for (var run = 0; run < 100; run++)
        {
            var chars = new char[random.Next(10000)];
            for (var i = 0; i < chars.Length; i++) chars[i] = (char)random.Next(32, 127);
            var sample = new string(chars); Check(SnapshotPayload.Decode(SnapshotPayload.Encode(sample)) == sample, "random roundtrip " + run);
        }
        Console.WriteLine("snapshot-payload-tests: 12 passed (including 100 generated roundtrips and every truncation)");
    }
}
