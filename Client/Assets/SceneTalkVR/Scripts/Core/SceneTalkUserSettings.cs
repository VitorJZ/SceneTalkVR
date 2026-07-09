using System;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class SceneTalkUserSettings
    {
        public const float MinFontScale = 0.8f;
        public const float MaxFontScale = 1.4f;
        public const float FontScaleStep = 0.1f;
        public const float MinUiScale = 0.5f;
        public const float MaxUiScale = 1.25f;
        public const float UiScaleStep = 0.05f;

        public float fontScale = 1f;
        public float uiScale = 1f;
        public bool hideDialogueSubtitles;

        public static SceneTalkUserSettings CreateDefault()
        {
            return new SceneTalkUserSettings();
        }

        public SceneTalkUserSettings Clone()
        {
            return new SceneTalkUserSettings
            {
                fontScale = fontScale,
                uiScale = uiScale,
                hideDialogueSubtitles = hideDialogueSubtitles
            };
        }

        public void Normalize()
        {
            fontScale = ClampToStep(fontScale, MinFontScale, MaxFontScale, FontScaleStep);
            uiScale = ClampToStep(uiScale, MinUiScale, MaxUiScale, UiScaleStep);
        }

        private static float ClampToStep(float value, float min, float max, float step)
        {
            var clamped = Mathf.Clamp(value, min, max);
            return Mathf.Round(clamped / step) * step;
        }
    }

    public static class SceneTalkUserSettingsStore
    {
        private const string PlayerPrefsKey = "SceneTalkVR.UserSettings.v1";

        private static SceneTalkUserSettings cachedSettings;

        public static event Action<SceneTalkUserSettings> Changed;

        public static SceneTalkUserSettings Current
        {
            get
            {
                if (cachedSettings == null)
                {
                    cachedSettings = Load();
                }

                return cachedSettings;
            }
        }

        public static void SetFontScale(float value)
        {
            var settings = Current.Clone();
            settings.fontScale = value;
            settings.Normalize();
            Save(settings);
        }

        public static void AdjustFontScale(float delta)
        {
            SetFontScale(Current.fontScale + delta);
        }

        public static void SetUiScale(float value)
        {
            var settings = Current.Clone();
            settings.uiScale = value;
            settings.Normalize();
            Save(settings);
        }

        public static void AdjustUiScale(float delta)
        {
            SetUiScale(Current.uiScale + delta);
        }

        public static void SetHideDialogueSubtitles(bool hidden)
        {
            var settings = Current.Clone();
            settings.hideDialogueSubtitles = hidden;
            settings.Normalize();
            Save(settings);
        }

        public static void ResetAll()
        {
            Save(SceneTalkUserSettings.CreateDefault());
        }

        private static SceneTalkUserSettings Load()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                var defaultSettings = SceneTalkUserSettings.CreateDefault();
                defaultSettings.Normalize();
                return defaultSettings;
            }

            try
            {
                var settings = JsonUtility.FromJson<SceneTalkUserSettings>(PlayerPrefs.GetString(PlayerPrefsKey));
                if (settings == null)
                {
                    settings = SceneTalkUserSettings.CreateDefault();
                }

                settings.Normalize();
                return settings;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SceneTalkVR] Failed to load user settings. Defaults will be used. {exception.Message}");
                var defaultSettings = SceneTalkUserSettings.CreateDefault();
                defaultSettings.Normalize();
                return defaultSettings;
            }
        }

        private static void Save(SceneTalkUserSettings settings)
        {
            settings.Normalize();
            cachedSettings = settings;
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
            Changed?.Invoke(cachedSettings);
        }
    }
}
