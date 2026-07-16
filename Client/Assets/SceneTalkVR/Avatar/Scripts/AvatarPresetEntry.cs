using System;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [Serializable]
    public sealed class AvatarPresetEntry
    {
        [Header("Identity")]
        public string key;
        public string displayName;
        public int priority;

        [Header("Runtime Asset")]
        public GameObject prefab;
        public string addressableKey;

        [Header("Fixed Scenario Mapping")]
        public string[] scenarioIds = Array.Empty<string>();

        [Header("Matching Tags")]
        public string[] roles = Array.Empty<string>();
        public string[] environmentTags = Array.Empty<string>();
        public string[] styleIds = Array.Empty<string>();
        public string[] genderPresentations = Array.Empty<string>();
        public string[] ageBuckets = Array.Empty<string>();
        public string[] bodyBuilds = Array.Empty<string>();
        public string[] hairStyles = Array.Empty<string>();
        public string[] hairColors = Array.Empty<string>();
        public string[] outfitRoles = Array.Empty<string>();
        public string[] outfitColors = Array.Empty<string>();
        public string[] accessoryTags = Array.Empty<string>();
        public string[] mustHaveTags = Array.Empty<string>();

        [Header("Constraints")]
        public string qualityTier = "placeholder";
        public bool mobileReady = true;

        public bool HasPrefab => prefab != null;
        public bool HasAddressableKey => !string.IsNullOrWhiteSpace(addressableKey);
        public bool IsUsable => !string.IsNullOrWhiteSpace(key) && (HasPrefab || HasAddressableKey);
    }
}
