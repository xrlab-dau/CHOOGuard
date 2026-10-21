using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    public sealed class InteriorEnvelopeRepairTests
    {
        [Serializable] sealed class MeshReceipt { public string asset; public string complete_channels_sha256; }
        [Serializable] sealed class PartitionReceipt
        {
            public MeshReceipt kit_mesh;
            public int[] duplicate_kit_triangle_ordinals;
            public int[] preserve_remaining_triangle_ordinals;
        }
        [Test]
        public void PinnedPartitionPreservesEveryUnrelatedFixtureTriangleAndCompleteChannels()
        {
            var receipt = JsonUtility.FromJson<PartitionReceipt>(File.ReadAllText("content/fixtures/architecture/retained-shop-collision-partition.json"));
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(receipt.kit_mesh.asset);
            Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(source), Is.EqualTo(receipt.kit_mesh.complete_channels_sha256));
            var original = source.triangles;
            var result = InteriorEnvelopeRepair.KeepOtherTriangles(source, receipt.duplicate_kit_triangle_ordinals);
            try
            {
                var ordinals = receipt.preserve_remaining_triangle_ordinals;
                Assert.That(ordinals.Length, Is.EqualTo(132));
                CollectionAssert.AreEqual(ordinals.SelectMany(i => original.Skip(i * 3).Take(3)), result.triangles);
                CollectionAssert.AreEqual(source.vertices, result.vertices);
                CollectionAssert.AreEqual(source.normals, result.normals);
                CollectionAssert.AreEqual(source.uv, result.uv);
                CollectionAssert.AreEqual(source.tangents, result.tangents);
                CollectionAssert.AreEqual(original, source.triangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(result); }
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidSourceTriangleOrdinalCannotRemoveGeometry(int ordinal)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward },
                triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            try { Assert.Throws<InvalidDataException>(() => InteriorEnvelopeRepair.KeepOtherTriangles(mesh, new[] { ordinal })); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void RepeatedSourceOrdinalsCannotMasqueradeAsTwoRemovedTriangles()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            try { Assert.Throws<InvalidDataException>(() => InteriorEnvelopeRepair.KeepOtherTriangles(mesh, new[] { 0, 0 })); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
