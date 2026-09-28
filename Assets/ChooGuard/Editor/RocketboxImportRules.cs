using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Import rules for Microsoft Rocketbox (MIT) avatars and animations under ThirdParty/Models/Rocketbox.
    /// Avatars become Humanoid (auto-mapped Biped), animations copy the reference avatar of their gender so clips
    /// retarget in muscle space. Materials are authored for URP by <see cref="CrowdAssetBuilder"/>.
    /// Adapted from Rocketbox Assets/Editor/FixRocketboxMaxImport.cs (MIT, Microsoft 2020).
    /// </summary>
    public sealed class RocketboxImportRules : AssetPostprocessor
    {
        public const string Root = "Assets/ChooGuard/ThirdParty/Models/Rocketbox/";
        public const string AvatarRoot = Root + "Avatars/";
        public const string AnimationRoot = Root + "Animations/";
        public const string MaleReference = AvatarRoot + "Male_Adult_02/Male_Adult_02.fbx";
        public const string FemaleReference = AvatarRoot + "Female_Adult_01/Female_Adult_01.fbx";

        private static readonly string[] LoopingClips =
        {
            "walk_neutral", "walk_fast", "run_neutral", "run_fast", "idle_neutral", "idle_look_around", "idle_nervous",
            "idle_waiting", "cell_phone_talk", "crouch_idle", "idle_cough", "sit_chair_idle", "take_picture",
        };

        /// <summary>
        /// Clips whose source travels forward over the cycle (the Rocketbox walk and run move about 1.2 m per loop). That
        /// travel must not be baked into the pose: with the agent moving the person, a baked stride carries the body ahead
        /// of its position all cycle and snaps it back at every loop (measured 2026-09-27: 212 snaps of 0.95–1.43 m in
        /// 20 s of crowd). Left as root motion, it is dropped (Animator.applyRootMotion is off) and the body walks in place
        /// over the agent.
        /// </summary>
        private static readonly string[] TravellingClips = { "walk_", "run_" };

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.animationType = ModelImporterAnimationType.Human;
            // 아바타는 슬롯 이름(<코드>_body/_head/_opacity/_helmet)을 얻으려고 재질 설명을 가져온다.
            // 실제 URP 재질은 CrowdAssetBuilder 가 슬롯 이름으로 다시 만든다.
            importer.materialImportMode = assetPath.StartsWith(AvatarRoot, StringComparison.Ordinal)
                ? ModelImporterMaterialImportMode.ImportViaMaterialDescription
                : ModelImporterMaterialImportMode.None;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            if (assetPath.StartsWith(AvatarRoot, StringComparison.Ordinal))
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                return;
            }
            if (!assetPath.StartsWith(AnimationRoot, StringComparison.Ordinal)) return;
            var file = System.IO.Path.GetFileName(assetPath);
            var reference = AssetDatabase.LoadAllAssetsAtPath(file.StartsWith("f_", StringComparison.Ordinal) ? FemaleReference : MaleReference)
                .OfType<Avatar>().FirstOrDefault();
            if (reference == null)
            {
                Debug.LogError("[RocketboxImportRules] 기준 아바타를 먼저 가져와야 합니다: " + assetPath, AssetDatabase.LoadMainAssetAtPath(assetPath));
                return;
            }
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = reference;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationRoot, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            var file = System.IO.Path.GetFileNameWithoutExtension(assetPath).Replace(".max", "");
            bool loop = LoopingClips.Any(file.Contains);
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = file;
                clips[i].loopTime = loop;
                // 이동은 NavMeshAgent 가 한다. 회전·높이는 포즈에 굽고, 걷기·뛰기의 수평 전진은 굽지 않고 뿌리 이동으로 떼어
                // 버린다(위 TravellingClips). 앉기·일어서기처럼 제자리 원점을 옮기는 동작은 수평 이동도 포즈에 둔다(PersonBody 가 원점을 옮긴다).
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = !TravellingClips.Any(file.Contains);
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            var lower = assetPath.ToLowerInvariant();
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            if (lower.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = lower.Contains("opacity");
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            if (root.transform.Find("Bip02") != null) Rename(root.transform);
        }

        private static void Rename(Transform bone)
        {
            bone.name = bone.name.Replace("Bip02", "Bip01");
            for (int i = 0; i < bone.childCount; i++) Rename(bone.GetChild(i));
        }
    }
}
