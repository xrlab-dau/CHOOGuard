using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// How much the game tells the player during a shift. It changes only what is shown and said (route marks, the
    /// office's advice, which radio lines are offered, the action names in prompts) and how handles are worked; what a
    /// station staff member is allowed to do is the same at every level.
    /// </summary>
    public enum GuidanceLevel
    {
        /// <summary>견학: the guided shift — route marks, advice in the office's answers, only the radio lines that fit now, action names in prompts, one key press for handles.</summary>
        Guided,
        /// <summary>표준: the office acknowledges without telling what to do next, the radio offers every report and request, prompts show what a thing is and its state, handles are worked by hand.</summary>
        Standard,
        /// <summary>실전: as 표준, and the compass keeps only the player's own pings.</summary>
        Expert,
    }

    /// <summary>Player-facing options persisted in PlayerPrefs. The title and pause menus edit the same values.</summary>
    public static class GameSettings
    {
        private const string SensitivityKey = "chooguard.mouseSensitivity";
        private const string VolumeKey = "chooguard.masterVolume";
        private const string GuidanceKey = "chooguard.guidance";
        // 안내 수준을 바꾸면 그 수준의 기본값으로 돌아가는 개별 설정(바꾸기 전까지는 수준을 따른다).
        private const string RouteKey = "chooguard.showRoute.v2";
        private const string SimpleKey = "chooguard.simpleControls";
        public const float BaseLookDegreesPerPixel = .09f;

        public static event Action Changed;
        public static event Action RouteChanged;

        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), .2f, 3f);
            set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, .2f, 3f)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, .8f));
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        /// <summary>The guidance preset (표준 by default). Setting it puts the route marks and simple controls back to the preset's own values.</summary>
        public static GuidanceLevel Guidance
        {
            get
            {
                int stored = PlayerPrefs.GetInt(GuidanceKey, (int)GuidanceLevel.Standard);
                return Enum.IsDefined(typeof(GuidanceLevel), stored) ? (GuidanceLevel)stored : GuidanceLevel.Standard;
            }
            set
            {
                if (Guidance == value && PlayerPrefs.HasKey(GuidanceKey)) return;
                bool routeBefore = ShowRoute;
                PlayerPrefs.SetInt(GuidanceKey, (int)value);
                PlayerPrefs.DeleteKey(RouteKey);
                PlayerPrefs.DeleteKey(SimpleKey);
                PlayerPrefs.Save();
                Changed?.Invoke();
                if (ShowRoute != routeBefore) RouteChanged?.Invoke();
            }
        }

        /// <summary>The guided shift (견학): the game says what fits now.</summary>
        public static bool Guided => Guidance == GuidanceLevel.Guided;

        public static string Label(GuidanceLevel level) => level == GuidanceLevel.Guided ? "견학" : level == GuidanceLevel.Expert ? "실전" : "표준";

        /// <summary>What a level hides or shows, for the settings screen.</summary>
        public static string Describe(GuidanceLevel level)
        {
            switch (level)
            {
                case GuidanceLevel.Guided:
                    return "길 안내 표시 · 역무실이 다음 조치를 알려 줌 · 무전은 지금 맞는 문장만 · 조작 이름 표시 · 손잡이는 E 한 번";
                case GuidanceLevel.Expert:
                    return "길 안내 없음 · 역무실은 접수만 · 무전은 모든 보고·요청 · 대상 이름과 상태만 표시 · 나침반에는 내가 찍은 표시만";
                default:
                    return "길 안내 없음 · 역무실은 접수만 · 무전은 모든 보고·요청 · 대상 이름과 상태만 표시 · 손잡이는 E 누른 채 마우스로";
            }
        }

        /// <summary>Route marks to a known incident (map line, floor marks, compass). Follows the preset (on only for 견학) until set.</summary>
        public static bool ShowRoute
        {
            get => PlayerPrefs.HasKey(RouteKey) ? PlayerPrefs.GetInt(RouteKey) != 0 : Guidance == GuidanceLevel.Guided;
            set
            {
                if (ShowRoute == value && PlayerPrefs.HasKey(RouteKey)) return;
                bool before = ShowRoute;
                PlayerPrefs.SetInt(RouteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (before != value) RouteChanged?.Invoke();
            }
        }

        /// <summary>One E press works a handle (valve, breaker, door, cover button) instead of holding E and moving the mouse. Follows the preset (on only for 견학) until set.</summary>
        public static bool SimpleControls
        {
            get => PlayerPrefs.HasKey(SimpleKey) ? PlayerPrefs.GetInt(SimpleKey) != 0 : Guidance == GuidanceLevel.Guided;
            set
            {
                if (SimpleControls == value && PlayerPrefs.HasKey(SimpleKey)) return;
                PlayerPrefs.SetInt(SimpleKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void Apply(FirstPersonResponder responder)
        {
            if (responder != null)
            {
                responder.LookDegreesPerPixel = BaseLookDegreesPerPixel * MouseSensitivity;
                responder.SimpleControls = SimpleControls;
            }
            AudioListener.volume = MasterVolume;
        }
    }
}
