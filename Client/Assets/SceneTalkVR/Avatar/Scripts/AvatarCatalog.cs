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
    }
}
