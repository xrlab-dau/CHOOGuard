using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    public sealed class ObservedFrontageRepairTests
    {
        [TestCase("Kit_Lightbox_Kit_Tex_artbox-2026-05-sign")]
        [TestCase("Kit_Frame_Canopy_Dark")]
        [TestCase("Kit_Clad_wall_painted_gypsum_fine")]
        [TestCase("Kit_Fascia_Kit_Paint_1E2226")]
        public void BarycentricCutPreservesUnrelatedSourceFacesAndInterpolatesOriginalUV(string leaf)
        {
            var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ChooGuard/Art/StationInterior/Kit/1F/" + leaf + ".asset");
            // Deterministic half-face fixture, not a renewed photograph/as-built verdict.
            // The independent cut polygon removes half the original unit triangle.
            var cuts = new[] { new ObservedFrontageRepair.FaceRemainder {
                Ordinal = 0, RemainingAreaFraction = .5f,
                CutPolygon = new[] { Vector2.zero, new Vector2(.5f, 0), Vector2.up, Vector2.zero },
                Triangles = new[] { new[] { new Vector3(.5f, .5f, 0), Vector3.up, Vector3.forward } }
            } };
            var sourceIndices = source.triangles; var sourceVertices = source.vertices; var sourceUV = source.uv;
            var result = ObservedFrontageRepair.KeepObservedRemainder(source, cuts);
            try
            {
                CollectionAssert.AreEqual(sourceVertices, result.vertices.Take(source.vertexCount));
                CollectionAssert.AreEqual(source.normals, result.normals.Take(source.vertexCount));
                CollectionAssert.AreEqual(source.tangents, result.tangents.Take(source.vertexCount));
                CollectionAssert.AreEqual(sourceUV, result.uv.Take(source.vertexCount));
                var byOrdinal = cuts.ToDictionary(c => c.Ordinal); int cursor = 0; int appended = source.vertexCount;
                var indices = result.triangles; var vertices = result.vertices; var uv = result.uv;
                for (int ordinal = 0; ordinal < sourceIndices.Length / 3; ordinal++)
                {
                    int a = sourceIndices[ordinal * 3], b = sourceIndices[ordinal * 3 + 1], c = sourceIndices[ordinal * 3 + 2];
                    if (!byOrdinal.TryGetValue(ordinal, out var cut))
                    {
                        CollectionAssert.AreEqual(new[] { a, b, c }, indices.Skip(cursor).Take(3)); cursor += 3; continue;
                    }
                    Assert.That(cut.RemainingAreaFraction, Is.InRange(.47f, .999f));
                    foreach (var triangle in cut.Triangles) foreach (var weight in triangle)
                    {
                        Assert.That(indices[cursor++], Is.EqualTo(appended));
                        var expected = sourceVertices[a] * weight.x + sourceVertices[b] * weight.y + sourceVertices[c] * weight.z;
                        Assert.That(Vector3.Distance(vertices[appended], expected), Is.LessThan(.000001f));
                        var expectedUV = sourceUV[a] * weight.x + sourceUV[b] * weight.y + sourceUV[c] * weight.z;
                        Assert.That(Vector2.Distance(uv[appended], expectedUV), Is.LessThan(.000001f)); appended++;
                    }
                }
                Assert.That(cursor, Is.EqualTo(indices.Length));
                Assert.That(appended, Is.EqualTo(result.vertexCount));
                CollectionAssert.AreEqual(sourceIndices, source.triangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(result); }
        }
        static Mesh Triangle() => new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
            normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward }, uv = new[] { Vector2.zero, Vector2.right, Vector2.up },
            tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) }, triangles = new[] { 0, 1, 2 } };
        [Test]
        public void EmptyRemainderCannotDeleteAnEntireArchitecturalFace()
        {
            var source = Triangle();
            try
            {
                Assert.Throws<InvalidDataException>(() => ObservedFrontageRepair.KeepObservedRemainder(source,
                    new[] { new ObservedFrontageRepair.FaceRemainder { Ordinal = 0, RemainingAreaFraction = 0, Triangles = Array.Empty<Vector3[]>() } }));
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, source.triangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test]
        public void OutOfFaceVertexCannotIntroduceAnInventedWallPatch()
        {
            var source = Triangle();
            try
            {
                Assert.Throws<InvalidDataException>(() => ObservedFrontageRepair.KeepObservedRemainder(source,
                    new[] { new ObservedFrontageRepair.FaceRemainder { Ordinal = 0, RemainingAreaFraction = .5f,
                        Triangles = new[] { new[] { Vector3.right, Vector3.up, new Vector3(-.1f, .5f, .6f) } } } }));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test]
        public void EqualAreaReplacementInsideTheObservedCutIsRejected()
        {
            var source = Triangle();
            try
            {
                var error = Assert.Throws<InvalidDataException>(() => ObservedFrontageRepair.KeepObservedRemainder(source,
                    new[] { new ObservedFrontageRepair.FaceRemainder { Ordinal = 0, RemainingAreaFraction = .5f,
                        CutPolygon = new[] { Vector2.zero, new Vector2(.5f, 0), Vector2.up, Vector2.zero },
                        Triangles = new[] { new[] { Vector3.right, new Vector3(.5f, .5f, 0), Vector3.forward } } } }));
                StringAssert.Contains("enters the observed cut", error.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test]
        public void NonFiniteAreaCannotBypassPreservationChecks()
        {
            var source = Triangle();
            try
            {
                Assert.Throws<InvalidDataException>(() => ObservedFrontageRepair.KeepObservedRemainder(source,
                    new[] { new ObservedFrontageRepair.FaceRemainder { Ordinal = 0, RemainingAreaFraction = float.NaN,
                        Triangles = new[] { new[] { Vector3.right, Vector3.up, Vector3.forward } } } }));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test]
        public void RepeatedPartialTrianglesCannotMasqueradeAsCompleteRemainder()
        {
            var source = Triangle();
            try
            {
                var triangle = new[] { new Vector3(.5f, .5f, 0), Vector3.up, new Vector3(0, .5f, .5f) };
                var error = Assert.Throws<InvalidDataException>(() => ObservedFrontageRepair.KeepObservedRemainder(source,
                    new[] { new ObservedFrontageRepair.FaceRemainder { Ordinal = 0, RemainingAreaFraction = .5f,
                        CutPolygon = new[] { Vector2.zero, new Vector2(.5f, 0), Vector2.up, Vector2.zero },
                        Triangles = new[] { triangle, triangle } } }));
                StringAssert.Contains("overlap", error.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
