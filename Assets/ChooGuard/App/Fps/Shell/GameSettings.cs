using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>Player-facing options persisted in PlayerPrefs. The title and pause menus edit the same values.</summary>
    public static class GameSettings
    {
        private const string SensitivityKey = "chooguard.mouseSensitivity";
        private const string VolumeKey = "chooguard.masterVolume";
        public const float BaseLookDegreesPerPixel = .09f;

        public static event Action Changed;

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

        public static void Apply(FirstPersonResponder responder)
        {
            if (responder != null) responder.LookDegreesPerPixel = BaseLookDegreesPerPixel * MouseSensitivity;
            AudioListener.volume = MasterVolume;
        }
    }
}
