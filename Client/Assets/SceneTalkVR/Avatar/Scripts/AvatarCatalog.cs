using System;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [CreateAssetMenu(fileName = "AvatarCatalog", menuName = "SceneTalkVR/Avatar Catalog")]
    public sealed class AvatarCatalog : ScriptableObject
    {
        public string defaultAvatarKey = "global_default";
        public AvatarPresetEntry[] presets = Array.Empty<AvatarPresetEntry>();

        public bool TryFindByKey(string key, out AvatarPresetEntry entry)
        {
            entry = FindByKey(key);
            return entry != null;
        }

        public AvatarPresetEntry FindByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || presets == null)
            {
                return null;
            }

            for (var i = 0; i < presets.Length; i++)
            {
                var candidate = presets[i];
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        public AvatarPresetEntry FindByScenarioId(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) || presets == null)
            {
                return null;
            }

            for (var i = 0; i < presets.Length; i++)
            {
                var candidate = presets[i];
                if (candidate == null || !candidate.IsUsable || candidate.scenarioIds == null)
                {
                    continue;
                }

                for (var scenarioIndex = 0; scenarioIndex < candidate.scenarioIds.Length; scenarioIndex++)
                {
                    if (string.Equals(candidate.scenarioIds[scenarioIndex], scenarioId, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public AvatarPresetEntry GetDefault()
        {
            if (TryFindByKey(defaultAvatarKey, out var defaultEntry))
            {
                return defaultEntry;
            }

            if (presets == null)
            {
                return null;
            }

            for (var i = 0; i < presets.Length; i++)
            {
                var candidate = presets[i];
                if (candidate != null && candidate.IsUsable)
                {
                    return candidate;
                }
            }

            return null;
        }

        public bool ValidateExactFormalPreset(string expectedKey, string expectedRole, out string error)
        {
            if (string.IsNullOrWhiteSpace(expectedKey)) { error = "formal_avatar_preset_key_unconfirmed"; return false; }
            var entry = FindByKey(expectedKey);
            if (entry == null) { error = $"formal_avatar_preset_missing:{expectedKey}"; return false; }
            return entry.ValidateForFormal(expectedKey, expectedRole, out error);
        }

        public bool ValidateEditorCollectionPreset(string expectedKey, out string error)
        {
            if (string.IsNullOrWhiteSpace(expectedKey)) { error = "editor_collection_avatar_key_missing"; return false; }
            var entry = FindByKey(expectedKey);
            if (entry == null) { error = $"editor_collection_avatar_missing:{expectedKey}"; return false; }
            return entry.ValidateForEditorCollection(expectedKey, out error);
        }
    }
}
