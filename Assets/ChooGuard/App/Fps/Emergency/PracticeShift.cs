using System;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A practice shift chosen from the title's tutorial list (2026-10-31): one incident of one family in the retained
    /// emergency session, nothing else composed. The runtime counterpart of the editor's PracticeDrill: it keeps the station's
    /// provider runtime from waking (the same deferral seam the PlayMode fixtures and the editor drill use), switches the
    /// provider root off when the station scene loads, makes sure a first-person staff member exists, and hands the family
    /// to <see cref="IncidentDirector"/> when the session begins. Leaving to the title releases everything.
    /// </summary>
    public static class PracticeShift
    {
        /// <summary>The tutorial catalog category that starts the cardiac-arrest practice.</summary>
        public const string CardiacCategory = "cardiac-arrest-practice";
        public const string CardiacFamily = "심정지";
        private const string ProviderRootName = "CHOOGuard Provider Runtime";
        private const string ProviderTypeName = "ChooGuard.Seeded.Unity.PlatformSeededSimulator, ChooGuard.Seeded";
        private const string DeferralProperty = "DeferPreparationForLegacyTests";

        /// <summary>The family armed for the next session (null when none); <see cref="Take"/> consumes it.</summary>
        public static string Family { get; private set; }
        public static bool Armed => Family != null;

        private static bool installed, ownsDeferral;
        private static PropertyInfo deferral;

        /// <summary>The practice family a tutorial category stands for, or null when the category is an ordinary tutorial scene.</summary>
        public static string FamilyOf(string categoryId) => categoryId == CardiacCategory ? CardiacFamily : null;

        /// <summary>Arms a practice of <paramref name="family"/> for the shift about to load. False when the provider seam cannot be taken.</summary>
        public static bool Arm(string family)
        {
            if (string.IsNullOrEmpty(family)) return false;
            if (!Install(out var problem)) { Debug.LogError("[연습] " + problem); return false; }
            Family = family;
            Debug.Log("[연습] 무장 → " + family + " (튜토리얼 목록에서)");
            return true;
        }

        /// <summary>The armed family, consumed by the session that begins now.</summary>
        public static string Take()
        {
            var family = Family;
            Family = null;
            return family;
        }

        private static bool Install(out string problem)
        {
            problem = null;
            if (installed) return true;
            var type = Type.GetType(ProviderTypeName, false);
            if (type != null)
            {
                var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
                if (station.isLoaded && ProviderRoot(station) is GameObject awake && awake.activeInHierarchy)
                { problem = "역 씬이 이미 provider 런타임으로 떠 있습니다."; return false; }
                deferral = type.GetProperty(DeferralProperty, BindingFlags.NonPublic | BindingFlags.Static);
                if (deferral == null || deferral.PropertyType != typeof(bool)) { problem = "provider 준비 유예 seam 이 없습니다."; return false; }
                ownsDeferral = !(bool)deferral.GetValue(null);
                if (ownsDeferral) deferral.SetValue(null, true);
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            installed = true;
            return true;
        }

        private static GameObject ProviderRoot(Scene scene) => scene.GetRootGameObjects().FirstOrDefault(root => root.name == ProviderRootName);

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneFlow.TitleScene) { Release(); return; }
            if (scene.name != SceneFlow.StationScene) return;
            var root = ProviderRoot(scene);
            if (root != null && root.activeSelf)
            {
                // 선택 루트는 제어 객체여야 한다. 형상이 붙어 있으면 끄지 않는다(원본 형상을 지우지 않는다).
                if (root.GetComponentsInChildren<Renderer>(true).Length > 0 || root.GetComponentsInChildren<Collider>(true).Length > 0)
                    Debug.LogError("[연습] provider 루트에 형상이 붙어 있어 끄지 않습니다.");
                else { root.SetActive(false); Debug.Log("[연습] 보존 근무 선택 · provider 런타임 비활성화"); }
            }
            EnsureResponder(scene);
        }

        private static void EnsureResponder(Scene scene)
        {
            bool present = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FirstPersonResponder>(true)).Any(player => player.gameObject.activeInHierarchy);
            if (present) return;
            var player = new GameObject("연습용 역무원");
            player.SetActive(false);
            SceneManager.MoveGameObjectToScene(player, scene);
            player.layer = 2;
            player.AddComponent<CharacterController>();
            var responder = player.AddComponent<FirstPersonResponder>();
            var cameraObject = new GameObject("FirstPersonCamera", typeof(Camera));
            cameraObject.transform.SetParent(player.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .05f; camera.farClipPlane = 1500; camera.fieldOfView = 70;
            responder.PlayerCamera = camera; responder.BodyHeight = 1.72f; responder.EyeHeight = 1.60f; responder.EnableJump = false;
            player.SetActive(true);
            Debug.Log("[연습] 역무원이 없어 연습용 플레이어를 두었습니다.");
        }

        private static void Release()
        {
            if (!installed) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            try { if (ownsDeferral) deferral?.SetValue(null, false); }
            catch (Exception problem) { Debug.LogWarning("[연습] provider 준비 유예를 되돌리지 못했습니다: " + problem.Message); }
            installed = false; ownsDeferral = false; deferral = null; Family = null;
        }
    }
}
