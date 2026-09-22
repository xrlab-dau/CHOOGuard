using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor.Assets
{
    /// <summary>준비된 로컬 에셋만 설정한다. 제품 배포 승인이나 기존 Bootstrap 교체를 의미하지 않는다.</summary>
    public static class PreparedAssetSetup
    {
        public const string SourceRoot = "Assets/ChooGuard/ThirdParty";
        public const string OutputRoot = "Assets/ChooGuard/Settings/ImportedAssets";
        public const string FontPath = OutputRoot + "/Fonts/NotoSansCJKkr SDF.asset";
        public const string KoreanSeed = "추가드 대전역 열차 승강장 출입구 대합실 운행 안전 안내 경고 확인 취소 저장 불러오기 시작 일시정지 종료 설정 사람 경로 시간 시뮬레이션 0123456789 ABCxyz";
        private const string Owner = "ChooGuard.PreparedAssetSetup.v1";
        // 데이터 작업의 실제 Lucide 폴더와 예정된 Lucide20PNG 이름을 모두 지원하되 중복은 거절한다.
        public static string IconRoot
        {
            get
            {
                var preferred = SourceRoot + "/Icons/Lucide20PNG";
                var staged = SourceRoot + "/Icons/Lucide";
                Require(!(Directory.Exists(preferred) && Directory.Exists(staged)), "Ambiguous Lucide directories");
                return Directory.Exists(preferred) ? preferred : staged;
            }
        }

        [MenuItem("ChooGuard/Assets/Configure Prepared Assets")]
        public static void Configure()
        {
            // 누락 파일은 출력 생성 전에 차단한다. Refresh에는 ForceUpdate를 쓰지 않는다.
            ValidatePreparedInputs();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(Shader.Find("Universal Render Pipeline/Lit") != null, "URP/Lit shader missing");
            foreach (var folder in new[] { OutputRoot, OutputRoot + "/Materials", OutputRoot + "/Fonts", OutputRoot + "/Textures" })
                EnsureFolder(folder);
            foreach (var path in Files(IconRoot, ".png")) ConfigureTexture(path, true, true, false);
            foreach (var set in new[] { "Concrete031", "Metal032" }) ConfigureSurface(set);
            ConfigureModels("TrainKit");
            ConfigureModels("MiniCharacters");
            foreach (var path in Files(SourceRoot + "/Audio/Interface", ".ogg"))
            {
                var clip = Load<AudioClip>(path);
                Require(clip.samples > 0 && clip.frequency > 0 && clip.channels >= 1 && clip.channels <= 2, "Invalid AudioClip metadata: " + path);
                Require(AssetImporter.GetAtPath(path) is AudioImporter, "AudioImporter missing: " + path);
            }
            ConfigureFont();
            AssetDatabase.SaveAssets();
        }

        public static void ValidatePreparedInputs()
        {
            Require(File.Exists(SourceRoot + "/Fonts/NotoSansCJKkr-Regular.otf"), "Missing Korean source font");
            Require(Files(IconRoot, ".png").Length == 20, "Expected 20 Lucide PNGs");
            Require(Files(SourceRoot + "/Audio/Interface", ".ogg").Length == 4, "Expected four interface OGG clips");
            foreach (var set in new[] { "TrainKit", "MiniCharacters" })
            {
                var models = Files(SourceRoot + "/Models/" + set, ".fbx");
                var expected = set == "TrainKit"
                    ? new[] { "railroad-corner-large.fbx", "railroad-curve.fbx", "railroad-straight.fbx", "train-connector.fbx", "train-electric-subway-a.fbx", "train-electric-subway-b.fbx", "train-electric-subway-c.fbx" }
                    : new[] { "aid-cane-blind.fbx", "aid-defibrillator-green.fbx", "character-female-a.fbx", "character-male-a.fbx", "wheelchair.fbx" };
                Require(models.Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(n => n, StringComparer.Ordinal)), "Unexpected selected FBX set: " + set);
                Require(Files(SourceRoot + "/Models/" + set, ".png").Count(p => Path.GetFileName(p) == "colormap.png") == 1, "Missing/ambiguous colormap: " + set);
                foreach (var path in models) ValidateFbxMetadata(path);
            }
            foreach (var set in new[] { "Concrete031", "Metal032" })
            {
                Require(Files(SourceRoot + "/Textures/" + set, ".jpg").Length == 6, "Expected six source JPG maps: " + set);
                foreach (var map in new[] { "Color", "NormalGL", "NormalDX", "Displacement", "Roughness", set == "Metal032" ? "Metalness" : "AmbientOcclusion" })
                    SurfacePath(set, map);
                // Concrete031은 금속 맵 없는 유전체이므로 R=0. Metal032는 반드시 실제 Metalness를 사용한다.
                Require(set != "Concrete031" || !Files(SourceRoot + "/Textures/" + set, ".jpg").Any(p => p.Contains("Metalness")), "Unexpected concrete metal map; review packing contract");
            }
            var licenses = Files(SourceRoot + "/Licenses", ".txt");
            foreach (var name in new[] { "Noto-Copyright.txt", "Noto-OFL.txt", "Lucide-ISC-MIT.txt", "Kenney-TrainKit.txt", "Kenney-MiniCharacters.txt", "Kenney-InterfaceSounds.txt", "CC0-1.0.txt", "NOTICE.txt" })
                Require(licenses.Any(p => Path.GetFileName(p) == name), "Missing license file: " + name);
            var text = string.Join("\n", licenses.Select(File.ReadAllText));
            foreach (var marker in new[] { "SIL OPEN FONT LICENSE", "ISC", "MIT", "CC0" })
                Require(text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0, "Missing license evidence: " + marker);
            foreach (var file in licenses) Require(new FileInfo(file).Length > 0, "Empty license: " + file);
        }

        private static void ConfigureSurface(string set)
        {
            foreach (var path in Files(SourceRoot + "/Textures/" + set, ".jpg"))
                ConfigureTexture(path, path.EndsWith("_Color.jpg", StringComparison.Ordinal), false, path.EndsWith("_NormalGL.jpg", StringComparison.Ordinal));
            var packedPath = OutputRoot + "/Textures/" + set + "_MetallicSmoothness.png";
            CheckOwned(packedPath);
            var rough = Decode(SurfacePath(set, "Roughness"));
            Texture2D metal = null;
            Texture2D packed = null;
            try
            {
                if (set == "Metal032") metal = Decode(SurfacePath(set, "Metalness"));
                Require(metal == null || (metal.width == rough.width && metal.height == rough.height), "PBR source dimensions differ: " + set);
                var r = rough.GetPixels32();
                var m = metal == null ? null : metal.GetPixels32();
                var colors = new Color32[r.Length];
                for (var i = 0; i < colors.Length; i++) colors[i] = new Color32(m == null ? (byte)0 : m[i].r, 0, 0, (byte)(255 - r[i].r));
                packed = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(colors);
                packed.Apply();
                var bytes = ImageConversion.EncodeToPNG(packed);
                if (!File.Exists(packedPath) || !File.ReadAllBytes(packedPath).SequenceEqual(bytes))
                {
                    File.WriteAllBytes(packedPath, bytes);
                    AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
                }
                MarkOwned(packedPath);
                ConfigureTexture(packedPath, false, false, false, true);
            }
            finally
            {
                Object.DestroyImmediate(rough);
                if (metal != null) Object.DestroyImmediate(metal);
                if (packed != null) Object.DestroyImmediate(packed);
            }
            var material = OwnedMaterial(set);
            material.SetTexture("_BaseMap", Load<Texture2D>(SurfacePath(set, "Color")));
            material.SetTexture("_BumpMap", Load<Texture2D>(SurfacePath(set, "NormalGL")));
            material.SetTexture("_MetallicGlossMap", Load<Texture2D>(packedPath));
            material.SetFloat("_Smoothness", 1); // 원본 roughness의 역수를 감쇠 없이 사용한다.
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (set == "Concrete031")
            {
                material.SetTexture("_OcclusionMap", Load<Texture2D>(SurfacePath(set, "AmbientOcclusion")));
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            // NormalDX/Displacement는 보존하되 URP/Lit에 임의 용도로 연결하지 않는다.
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureModels(string set)
        {
            var colorPath = Files(SourceRoot + "/Models/" + set, ".png").Single(p => Path.GetFileName(p) == "colormap.png");
            ConfigureTexture(colorPath, true, false, false);
            var material = OwnedMaterial(set + "_Colormap");
            material.SetTexture("_BaseMap", Load<Texture2D>(colorPath));
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            foreach (var path in Files(SourceRoot + "/Models/" + set, ".fbx"))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Require(importer != null, "Missing ModelImporter: " + path);
                // 선택 세트 중 실제 LimbNode가 있는 캐릭터만 Generic; 비골격 모델은 None. Humanoid 강제 금지.
                var rig = Encoding.ASCII.GetString(File.ReadAllBytes(path)).Contains("LimbNode") ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                var dirty = !importer.useFileScale || !importer.useFileUnits || !importer.bakeAxisConversion || importer.globalScale != 1 || importer.animationType != rig || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard || importer.materialLocation != ModelImporterMaterialLocation.InPrefab;
                importer.useFileScale = true;
                importer.useFileUnits = true;
                importer.bakeAxisConversion = true;
                importer.globalScale = 1;
                importer.animationType = rig;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                if (dirty) importer.SaveAndReimport();
                Require(importer.fileScale > 0, "Invalid file scale: " + path);
                var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
                var map = importer.GetExternalObjectMap();
                var identifiers = embedded.Select(m => new AssetImporter.SourceAssetIdentifier(m)).Concat(map.Keys.Where(k => k.type == typeof(Material))).Distinct().ToArray();
                Require(identifiers.Length > 0, "No source materials to remap: " + path);
                var remapped = false;
                foreach (var id in identifiers)
                {
                    if (map.TryGetValue(id, out var current) && current == material) continue;
                    Require(current == null || AssetDatabase.GetAssetPath(current).StartsWith(OutputRoot + "/", StringComparison.Ordinal), "Refusing non-owned material remap: " + path);
                    importer.AddRemap(id, material);
                    remapped = true;
                }
                if (remapped) importer.SaveAndReimport();
                Require(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Any(m => m.vertexCount > 0), "No imported mesh: " + path);
                var prefab = Load<GameObject>(path);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var assigned in renderer.sharedMaterials) Require(assigned == material, "Material remap not applied: " + path);
            }
        }

        private static void ConfigureFont()
        {
            CheckOwned(FontPath);
            var source = Load<Font>(SourceRoot + "/Fonts/NotoSansCJKkr-Regular.otf");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                Require(font != null, "TMP Korean font creation failed");
                font.name = "NotoSansCJKkr SDF";
                AssetDatabase.CreateAsset(font, FontPath);
                MarkOwned(FontPath);
            }
            Require(font.sourceFontFile == source, "Owned TMP font has unexpected sourceFontFile");
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            if (!font.HasCharacters(KoreanSeed))
            {
                font.TryAddCharacters(KoreanSeed, out string missing);
                Require(string.IsNullOrEmpty(missing) && font.HasCharacters(KoreanSeed), "Korean seed glyphs missing: " + missing);
            }
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (var atlas in font.atlasTextures) if (atlas != null) EditorUtility.SetDirty(atlas);
        }

        private static void ConfigureTexture(string path, bool srgb, bool sprite, bool normal, bool readable = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Missing TextureImporter: " + path);
            var type = sprite ? TextureImporterType.Sprite : normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var dirty = importer.textureType != type || importer.sRGBTexture != srgb || importer.alphaIsTransparency != sprite || importer.isReadable != readable || importer.textureCompression != TextureImporterCompression.Uncompressed || (sprite && importer.spriteImportMode != SpriteImportMode.Single);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = sprite;
            importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (sprite) importer.spriteImportMode = SpriteImportMode.Single;
            if (dirty) importer.SaveAndReimport();
            Load<Texture2D>(path);
            if (sprite) Load<Sprite>(path);
        }

        private static Material OwnedMaterial(string name)
        {
            var path = OutputRoot + "/Materials/" + name + ".mat";
            CheckOwned(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
                MarkOwned(path);
            }
            Require(material.shader != null && material.shader.name == "Universal Render Pipeline/Lit", "Owned material shader changed: " + path);
            return material;
        }

        private static void CheckOwned(string path)
        {
            Require(path.StartsWith(OutputRoot + "/", StringComparison.Ordinal), "Output outside owned root");
            if (File.Exists(path)) Require(AssetImporter.GetAtPath(path)?.userData == Owner, "Refusing existing non-owned output: " + path);
        }
        private static void MarkOwned(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            Require(importer != null, "Output importer missing: " + path);
            if (importer.userData == Owner) return;
            importer.userData = Owner;
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))), "Could not create output folder: " + path);
        }
        private static string[] Files(string root, string extension)
        {
            Require(Directory.Exists(root), "Missing prepared directory: " + root);
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(p => p.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }
        private static string SurfacePath(string set, string map)
        {
            var paths = Files(SourceRoot + "/Textures/" + set, ".jpg").Where(p => p.EndsWith("_" + map + ".jpg", StringComparison.Ordinal)).ToArray();
            Require(paths.Length == 1, "Missing/ambiguous source map: " + set + "/" + map);
            return paths[0];
        }
        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, "Wrong/missing imported type " + typeof(T).Name + ": " + path);
            return asset;
        }
        private static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) return texture;
            Object.DestroyImmediate(texture);
            throw new InvalidOperationException("Invalid source image: " + path);
        }
        private static void ValidateFbxMetadata(string path)
        {
            // 배포된 두 Kenney FBX 바이너리의 GlobalSettings P 레코드를 확인한다. 다른 버전이면 추측하지 않고 실패한다.
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.GetEncoding(28591).GetString(bytes);
            Require(text.StartsWith("Kaydara FBX Binary", StringComparison.Ordinal), "Expected binary FBX: " + path);
            foreach (var item in new[] { ("UpAxis", 1), ("UpAxisSign", 1), ("FrontAxis", 2), ("FrontAxisSign", 1), ("CoordAxis", 0), ("CoordAxisSign", 1) })
            {
                var match = Regex.Match(text, Regex.Escape(item.Item1) + "S\\x03\\x00\\x00\\x00intS\\x07\\x00\\x00\\x00IntegerS\\x00\\x00\\x00\\x00I(.{4})", RegexOptions.Singleline);
                Require(match.Success && BitConverter.ToInt32(bytes, match.Groups[1].Index) == item.Item2, "Unexpected FBX axis " + item.Item1 + ": " + path);
            }
            var unit = Regex.Match(text, "UnitScaleFactorS\\x06\\x00\\x00\\x00doubleS\\x06\\x00\\x00\\x00NumberS\\x00\\x00\\x00\\x00D(.{8})", RegexOptions.Singleline);
            Require(unit.Success && BitConverter.ToDouble(bytes, unit.Groups[1].Index) == 1d, "Unexpected FBX unit scale: " + path);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Prepared assets: " + message);
        }
    }
}
