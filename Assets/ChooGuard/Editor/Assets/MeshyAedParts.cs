using System;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Assets
{
    /// <summary>
    /// Turns the prepared Meshy AED parts (Art/Emergency/Equipment/Models/aed_open, aed_pad, aed_shock_button: FBX in metres +
    /// BaseColor/Normal/MetallicSmoothness PNGs + json sidecar, made by prep_meshy2.py) into what the scene code uses: a URP Lit
    /// material per part, a prefab (the model under a box collider on its bounds, as aed_unit.prefab is laid out) and the three
    /// <see cref="EmergencyArt"/> slots (AedOpenModel, AedPadModel, AedShockButtonModel). Re-runnable: existing materials and
    /// prefabs are updated in place, so their GUIDs and the art asset's references stay.
    /// Batch: Unity.exe -batchmode -projectPath ... -executeMethod ChooGuard.Editor.Assets.MeshyAedParts.Build
    /// </summary>
    public static class MeshyAedParts
    {
        private const string Root = "Assets/ChooGuard/Art/Emergency/Equipment/Models";
        private static readonly (string folder, string slot)[] Parts =
        {
            ("aed_open", nameof(EmergencyArt.AedOpenModel)),
            ("aed_pad", nameof(EmergencyArt.AedPadModel)),
            ("aed_shock_button", nameof(EmergencyArt.AedShockButtonModel)),
        };

        [MenuItem("ChooGuard/Assets/Build Meshy AED Parts")]
        public static void Build()
        {
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(EmergencySceneBuilder.ArtAssetPath) ?? throw new FileNotFoundException(EmergencySceneBuilder.ArtAssetPath);
            foreach (var (folder, slot) in Parts)
            {
                var prefab = BuildPart(folder);
                var field = typeof(EmergencyArt).GetField(slot) ?? throw new MissingFieldException(slot);
                field.SetValue(art, prefab);
                Debug.Log("MeshyAedParts: " + folder + " → EmergencyArt." + slot);
            }
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildPart(string name)
        {
            string dir = Root + "/" + name;
            string fbx = dir + "/" + name + ".fbx";
            if (AssetImporter.GetAtPath(fbx) is not ModelImporter importer) throw new FileNotFoundException(fbx);
            bool dirty = importer.materialImportMode != ModelImporterMaterialImportMode.None || !importer.isReadable || importer.globalScale != 1 || !importer.useFileScale || !importer.useFileUnits;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;   // AedUnit reads the open case's vertices to tell which side the lid stands on
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.animationType = ModelImporterAnimationType.None;
            if (dirty) importer.SaveAndReimport();

            Texture(dir + "/" + name + "_BaseColor.png", srgb: true, normal: false);
            Texture(dir + "/" + name + "_Normal.png", srgb: false, normal: true);
            Texture(dir + "/" + name + "_MetallicSmoothness.png", srgb: false, normal: false);

            string matPath = dir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_BaseColor.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_Normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_MetallicSmoothness.png"));
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) ?? throw new FileNotFoundException(fbx);
            string prefabPath = dir + "/" + name + ".prefab";
            var root = new GameObject(name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = name;
                instance.transform.SetParent(root.transform, false);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                var bounds = Props.LocalBounds(root.transform);
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                if (!ok || prefab == null) throw new InvalidOperationException("prefab not saved: " + prefabPath);
                Debug.Log("MeshyAedParts: " + prefabPath + " · bounds " + bounds.size.ToString("F3") + " · centre " + bounds.center.ToString("F3"));
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Texture(string path, bool srgb, bool normal)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) throw new FileNotFoundException(path);
            bool dirty = importer.sRGBTexture != srgb || importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) || importer.maxTextureSize != 2048;
            importer.sRGBTexture = srgb;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            if (dirty) importer.SaveAndReimport();
        }
    }
}
