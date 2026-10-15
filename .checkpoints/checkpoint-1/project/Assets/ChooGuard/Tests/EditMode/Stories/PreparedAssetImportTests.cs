#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using ChooGuard.Editor.Assets;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Tests.EditMode.Stories
{
    // 통합 테스트: 준비 데이터가 없으면 명확히 실패한다. 이번 작성 단계에서는 실행하지 않는다.
    public sealed class PreparedAssetImportTests
    {
        [OneTimeSetUp]
        public void ConfigurePreparedAssets() => PreparedAssetSetup.Configure();

        [Test]
        public void SevenSetsHaveRealImportedTypesAndLicenseEvidence()
        {
            PreparedAssetSetup.ValidatePreparedInputs();
            var root = PreparedAssetSetup.SourceRoot;
            Assert.That(AssetDatabase.LoadAssetAtPath<Font>(root + "/Fonts/NotoSansCJKkr-Regular.otf"), Is.Not.Null);
            Assert.That(Files(PreparedAssetSetup.IconRoot, ".png").Length, Is.EqualTo(20));
            foreach (var path in Files(PreparedAssetSetup.IconRoot, ".png"))
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture && importer.alphaIsTransparency, Is.True, path);
            }
            foreach (var path in Files(root + "/Audio/Interface", ".ogg"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.samples, Is.GreaterThan(0), path);
                Assert.That(clip.frequency, Is.GreaterThan(0), path);
                Assert.That(clip.channels, Is.InRange(1, 2), path);
            }
        }

        [TestCase("TrainKit", 7)]
        [TestCase("MiniCharacters", 5)]
        public void ModelsHaveMeshesAndOwnedColormapRemaps(string set, int count)
        {
            var files = Files(PreparedAssetSetup.SourceRoot + "/Models/" + set, ".fbx");
            Assert.That(files.Length, Is.EqualTo(count));
            foreach (var path in files)
            {
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Any(m => m.vertexCount > 0), Is.True, path);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.animationType, Is.Not.EqualTo(ModelImporterAnimationType.Human), path);
                Assert.That(importer.useFileScale && importer.useFileUnits && importer.bakeAxisConversion, Is.True, path);
                Assert.That(importer.GetExternalObjectMap().Count, Is.GreaterThan(0), path);
                foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, path);
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(PreparedAssetSetup.OutputRoot + "/Materials/"));
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                    Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
                }
            }
        }

        [TestCase("Concrete031")]
        [TestCase("Metal032")]
        public void PbrMapsUseLinearDataAndActualPackedPixels(string set)
        {
            foreach (var path in Files(PreparedAssetSetup.SourceRoot + "/Textures/" + set, ".jpg"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.EqualTo(path.EndsWith("_Color.jpg", StringComparison.Ordinal)));
                if (path.EndsWith("_NormalGL.jpg", StringComparison.Ordinal))
                    Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PreparedAssetSetup.OutputRoot + "/Materials/" + set + ".mat");
            Assert.That(mat.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(mat.GetTexture("_BumpMap"), Is.Not.Null);
            var packed = mat.GetTexture("_MetallicGlossMap") as Texture2D;
            Assert.That(packed, Is.Not.Null);
            var rough = Decode(Files(PreparedAssetSetup.SourceRoot + "/Textures/" + set, ".jpg").Single(p => p.EndsWith("_Roughness.jpg", StringComparison.Ordinal)));
            Texture2D metal = null;
            try
            {
                if (set == "Metal032") metal = Decode(Files(PreparedAssetSetup.SourceRoot + "/Textures/" + set, ".jpg").Single(p => p.EndsWith("_Metalness.jpg", StringComparison.Ordinal)));
                var pixels = packed.GetPixels32();
                var r = rough.GetPixels32();
                var m = metal == null ? null : metal.GetPixels32();
                Assert.That(pixels.Length, Is.EqualTo(r.Length));
                for (var i = 0; i < pixels.Length; i += 997)
                {
                    Assert.That(pixels[i].a, Is.EqualTo(255 - r[i].r));
                    Assert.That(pixels[i].r, Is.EqualTo(m == null ? 0 : m[i].r));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(rough); if (metal != null) UnityEngine.Object.DestroyImmediate(metal); }
        }

        [Test]
        public void KoreanFontHasSourceDynamicAtlasAndSeedWithoutReplacingBootstrap()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PreparedAssetSetup.FontPath);
            Assert.That(font.sourceFontFile, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Font>(PreparedAssetSetup.SourceRoot + "/Fonts/NotoSansCJKkr-Regular.otf")));
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(font.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(font.HasCharacters(PreparedAssetSetup.KoreanSeed), Is.True);
            Assert.That(font.material.mainTexture, Is.Not.Null);
            Assert.That(font.characterTable.Count, Is.LessThan(11172));
        }

        [Test]
        public void RepeatedConfigurePreservesGuidsAndReferences()
        {
            var paths = AssetDatabase.FindAssets("", new[] { PreparedAssetSetup.OutputRoot }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var bootstrapPath = "Assets/ChooGuard/Settings/TMP/Fonts/ChooGuard Bootstrap SDF.asset";
            var bootstrap = File.ReadAllBytes(bootstrapPath);
            PreparedAssetSetup.Configure();
            Assert.That(paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), Is.EqualTo(guids));
            Assert.That(File.ReadAllBytes(bootstrapPath), Is.EqualTo(bootstrap));
            foreach (var path in paths.Where(p => !AssetDatabase.IsValidFolder(p)))
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
            KoreanFontHasSourceDynamicAtlasAndSeedWithoutReplacingBootstrap();
            ModelsHaveMeshesAndOwnedColormapRemaps("TrainKit", 7);
            ModelsHaveMeshesAndOwnedColormapRemaps("MiniCharacters", 5);
        }

        private static string[] Files(string root, string extension) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).Select(p => p.Replace('\\', '/')).ToArray();
        private static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }
    }
}
#endif
