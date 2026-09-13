using NUnit.Framework;
using System;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;

public class SnapshotFragmentTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static byte[] Data(int n) { var data = new byte[n]; new Random(42).NextBytes(data); return data; }
    [Test] public void BoundedContract()
    {
        var count = 0;
        var original = Data(65536);
        var parts = SnapshotFragments.Split(1, original);
        Check(parts.All(p => p.Length <= 952), "packet bound");
        var assembly = new SnapshotAssembler();
        byte[] output = null; long serial = 0;
        foreach (var part in parts.Reverse())
            assembly.Accept(part, 0, out output, out serial);
        Check(serial == 1 && original.SequenceEqual(output), "full binary reorder");
        Check(assembly.Accept(parts[0], 1, out _, out _) == FragmentResult.Ignored, "old duplicate");
        count++;
        assembly = new SnapshotAssembler();
        var old = SnapshotFragments.Split(1, Data(2000));
        assembly.Accept(old[0], 0, out _, out _);
        var newer = SnapshotFragments.Split(2, Data(10))[0];
        Check(assembly.Accept(newer, 1, out output, out serial) == FragmentResult.Complete && serial == 2, "new snapshot independent of missing old fragment");
        Check(assembly.Accept(old[1], 2, out _, out _) == FragmentResult.Ignored, "late old fragment");
        count++;
        assembly = new SnapshotAssembler();
        var corrupted = SnapshotFragments.Split(3, Data(10))[0];
        corrupted[corrupted.Length - 1] ^= 1;
        Check(assembly.Accept(corrupted, 0, out _, out _) == FragmentResult.Rejected, "digest");
        var malformed = SnapshotFragments.Split(4, Data(10))[0];
        malformed[12] = 255; malformed[13] = 255; malformed[14] = 255; malformed[15] = 127;
        Check(assembly.Accept(malformed, 1, out _, out _) == FragmentResult.Rejected, "oversized declared payload");
        count++;
        assembly = new SnapshotAssembler(maxInflight: 3, timeoutMilliseconds: 100);
        for (var i = 1; i <= 10; i++) assembly.Accept(SnapshotFragments.Split(i, Data(2000))[0], i, out _, out _);
        Check(assembly.InflightCount == 3 && assembly.BufferedBytes <= 3 * 65536, "bounded pending snapshots");
        assembly.Expire(200);
        Check(assembly.InflightCount == 0 && assembly.BufferedBytes == 0, "timeout releases memory");
        count++;
        assembly = new SnapshotAssembler();
        parts = SnapshotFragments.Split(5, Data(2000));
        assembly.Accept(parts[0], 0, out _, out _);
        var badDuplicate = (byte[])parts[0].Clone(); badDuplicate[badDuplicate.Length-1] ^= 1;
        Check(assembly.Accept(badDuplicate, 1, out _, out _) == FragmentResult.Rejected && assembly.InflightCount == 0, "conflicting duplicate");
        count++;
        original = Data(10); parts = SnapshotFragments.Split(6, original); original[0] ^= 1;
        assembly = new SnapshotAssembler(); assembly.Accept(parts[0], 0, out output, out _);
        Check(output[0] != original[0], "no source alias");
        Console.WriteLine("snapshot-fragment-tests: " + (++count) + " passed");
    }
}

