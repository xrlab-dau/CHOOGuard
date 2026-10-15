using System;
using System.Linq;
using ChooGuard.Editor.Twin;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class GroundEscalatorSplitTests
    {
        private static string Folder(string bank) => "Assets/ChooGuard/Art/StationInterior/Kit/" + bank + "/";

        [TestCase("1F-2F_core", "Kit_EscStep_source-abf1f31198576978dc15.asset")]
        [TestCase("1F_public_escalator", "Kit_EscStep_source-abf1f31198576978dc15.asset")]
        [TestCase("1F-2F_core", "Kit_Rubber.asset")]
        [TestCase("1F_public_escalator", "Kit_Rubber.asset")]
        public void ActualBuilderFlightsPreserveEveryTrianglePositionNormalTangentAndUv(string bank, string asset)
        {
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(Folder(bank) + asset);
            Assert.That(source, Is.Not.Null);
            var originalIndices = source.triangles; var originalVertices = source.vertices;
            var originalNormals = source.normals; var originalTangents = source.tangents; var originalUv = source.uv;
            var splits = ExistingGroundEscalatorSetup.SplitBuilderFlights(source);
            try
            {
                for (int flight = 0; flight < 2; flight++)
                {
                    int first = flight * (source.vertexCount / 2), count = source.vertexCount / 2;
                    CollectionAssert.AreEqual(originalVertices.Skip(first).Take(count), splits[flight].vertices);
                    CollectionAssert.AreEqual(originalNormals.Skip(first).Take(count), splits[flight].normals);
                    CollectionAssert.AreEqual(originalTangents.Skip(first).Take(count), splits[flight].tangents);
                    CollectionAssert.AreEqual(originalUv.Skip(first).Take(count), splits[flight].uv);
                    CollectionAssert.AreEqual(originalIndices.Skip(flight * originalIndices.Length / 2).Take(originalIndices.Length / 2).Select(index => index - first), splits[flight].triangles);
                }
                Assert.That(splits.Sum(mesh => mesh.triangles.Length), Is.EqualTo(originalIndices.Length));
                CollectionAssert.AreEqual(originalIndices, source.triangles);
                CollectionAssert.AreEqual(originalVertices, source.vertices);
            }
            finally { foreach (var mesh in splits) UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [TestCase("1F-2F_core", 2, .9f)]
        [TestCase("1F_public_escalator", 3, .65f)]
        public void EveryDerivedRideRampCornerAlreadyExistsInTheActualColliderMesh(string bank, int flats, float runout)
        {
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(Folder(bank) + "Kit_EscStep_source-abf1f31198576978dc15.asset");
            var collider = AssetDatabase.LoadAssetAtPath<Mesh>(Folder(bank) + "Kit_Colliders.asset");
            var frame = new GameObject("Existing collider frame test");
            var splits = ExistingGroundEscalatorSetup.SplitBuilderFlights(source);
            try
            {
                foreach (var mesh in splits)
                {
                    var geometry = ExistingGroundEscalatorSetup.DescribeFlight(mesh, frame.transform, flats, runout);
                    foreach (var centre in geometry.Ramp)
                        foreach (float side in new[] { -.5f, .5f })
                        {
                            var corner = centre + geometry.Side * geometry.Width * side;
                            float error = collider.vertices.Min(v => Vector3.Distance(v, corner));
                            Assert.That(error, Is.LessThan(.0001f), "Ride geometry must be an existing collider corner");
                        }
                }
            }
            finally { foreach (var mesh in splits) UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(frame); }
        }

        [Test]
        public void APartialSurfaceWithMissingNativeComponentsIsRepairedAndReusedWithoutDuplicates()
        {
            var part = new GameObject("기존 디딤판");
            var mesh = new Mesh();
            try
            {
                ExistingGroundEscalatorSetup.EnsureSurfaceComponents(part, out var filter, out var renderer);
                Assert.That(filter == null, Is.False); Assert.That(renderer == null, Is.False);
                filter.sharedMesh = mesh;
                ExistingGroundEscalatorSetup.EnsureSurfaceComponents(part, out var againFilter, out var againRenderer);
                Assert.That(againFilter, Is.SameAs(filter)); Assert.That(againRenderer, Is.SameAs(renderer));
                Assert.That(part.GetComponents<MeshFilter>().Length, Is.EqualTo(1));
                Assert.That(part.GetComponents<MeshRenderer>().Length, Is.EqualTo(1));
                Assert.That(againFilter.sharedMesh, Is.SameAs(mesh));
            }
            finally { UnityEngine.Object.DestroyImmediate(part); UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CrossingTheBuilderFlightIndexBoundaryIsRejected()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one,
                Vector3.forward, Vector3.right + Vector3.forward, Vector3.up + Vector3.forward, Vector3.one + Vector3.forward },
                triangles = new[] { 0, 4, 1, 4, 5, 6 } };
            try { Assert.Throws<InvalidOperationException>(() => ExistingGroundEscalatorSetup.SplitBuilderFlights(mesh)); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
