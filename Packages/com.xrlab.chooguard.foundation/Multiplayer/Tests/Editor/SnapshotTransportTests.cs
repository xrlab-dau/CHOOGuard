using System.Reflection;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class SnapshotTransportTests
{
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    public void NamedMessageReaderConsumesOnlyRemainingFragmentBytes(int consumedHeaderBytes)
    {
        var packet = SnapshotFragments.Split(1, new byte[2000])[0];
        using var writer = new FastBufferWriter(consumedHeaderBytes + packet.Length, Allocator.Temp);
        writer.WriteBytesSafe(new byte[consumedHeaderBytes]);
        writer.WriteBytesSafe(packet);
        using var reader = new FastBufferReader(writer, Allocator.Temp);
        var header = new byte[consumedHeaderBytes];
        reader.ReadBytesSafe(ref header, header.Length);
        var owner = new GameObject("SnapshotTransportTest");
        try
        {
            var runtime = owner.AddComponent<NetworkFieldRuntime>();
            var receive = typeof(NetworkFieldRuntime).GetMethod("ReceiveSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            receive.Invoke(runtime, new object[] { NetworkManager.ServerClientId, reader });
            var assembler = (SnapshotAssembler)typeof(NetworkFieldRuntime)
                .GetField("snapshotAssembler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
            Assert.That(assembler.InflightCount, Is.EqualTo(1), "Complete first fragment must reach reassembly even with an NGO header prefix.");
            Assert.That((long)typeof(NetworkFieldRuntime).GetField("receivedBytes", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(runtime), Is.EqualTo(packet.Length), "Payload metrics exclude the already-consumed NGO header.");
        }
        finally { Object.DestroyImmediate(owner); }
    }
}
