using System;
using ChooGuard.App.Fps.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>Invoked only by the explicit user menu. Merely importing/compiling never changes the open scene.</summary>
    public static class GameplayLaunchMenu
    {
        [MenuItem("CHOOGuard/Gameplay/독립 FPS 진입 장면 열기")]
        public static void OpenEntry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생을 종료한 뒤 명시적으로 실행하세요.");
            var seed = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Gameplay/practice-seed.json");
            var rules = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Gameplay/atomic-transitions.json");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset");
            if (seed == null || rules == null || font == null) throw new InvalidOperationException("초기 세계/원자 전이/한국어 폰트 원본이 필요합니다.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("명시적 FPS 게임 진입");
            var entry = root.AddComponent<GameplayBootstrap>(); entry.SeedJson = seed; entry.TransitionJson = rules; entry.KoreanFont = font;
            Selection.activeGameObject = root;
            // Unsaved dedicated entry: the user chooses whether/where to save. Modelling assets are never overwritten.
        }
    }
}
