using System.IO;
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    public sealed class ArchitecturalCorrectionRegistryTests
    {
        [Serializable] sealed class Record { public string source_asset, replacement_asset, owner; }
        [Serializable] sealed class Records { public Record[] entries; }
        static Record Entry(string leaf)
        {
            var registry = JsonUtility.FromJson<Records>(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry));
            foreach (var entry in registry.entries)
                if (entry.source_asset == "Assets/ChooGuard/Art/StationInterior/Kit/1F/" + leaf + ".asset") return entry;
            Assert.Fail("Missing installed correction: " + leaf); return null;
        }
        static Matrix4x4 Frame(Record entry)
        {
            // Independent retained kit basis; EditMode test runner starts in an empty scene.
            return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 16.2f, 0), Vector3.one);
        }
        [TestCase("Kit_Lightbox_Kit_Tex_artbox-2026-05-sign")]
        [TestCase("Kit_Frame_Canopy_Dark")]
        [TestCase("Kit_Clad_wall_painted_gypsum_fine")]
        [TestCase("Kit_Fascia_Kit_Paint_1E2226")]
        public void IdenticalSourceReturnsTheSavedCorrectionWithoutChangingItsNativeMesh(string leaf)
        {
            var entry = Entry(leaf); string path = entry.source_asset;
            var original = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var generated = UnityEngine.Object.Instantiate(original); string originalDigest = ArchitecturalCorrectionRegistry.ChannelDigest(original);
            try
            {
                var actual = ArchitecturalCorrectionRegistry.ResolveForRebuild(generated, path, entry.owner, Frame(entry));
                Assert.That(actual, Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>(entry.replacement_asset)));
                Assert.That(ArchitecturalCorrectionRegistry.ChannelDigest(original), Is.EqualTo(originalDigest));
                Assert.That(actual.triangles, Is.Not.EqualTo(original.triangles));
            }
            finally { UnityEngine.Object.DestroyImmediate(generated); }
        }
        [Test]
        public void ChangedNativeUVCannotSilentlyReuseAPhotographicCut()
        {
            var entry = Entry("Kit_Frame_Canopy_Dark"); var mesh = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(entry.source_asset));
            try
            {
                var uv = mesh.uv; uv[0] += Vector2.one * .01f; mesh.uv = uv;
                Assert.Throws<InvalidDataException>(() => ArchitecturalCorrectionRegistry.ResolveForRebuild(mesh,
                    entry.source_asset, entry.owner, Frame(entry)));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test]
        public void ChangedFrameCannotApplyAnOldCutToAnotherStorefront()
        {
            var entry = Entry("Kit_Frame_Canopy_Dark"); var frame = Frame(entry); frame[0,3] += .01f;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(entry.source_asset);
            Assert.Throws<InvalidDataException>(() => ArchitecturalCorrectionRegistry.ResolveForRebuild(mesh,
                entry.source_asset, entry.owner, frame));
        }
    }
}
