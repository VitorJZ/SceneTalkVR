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

        [Header("Experiment v1.1 Collection Metadata")]
        public string semanticRole;
        public string voiceProfileKey;
        public string voiceId;
        public RuntimeAnimatorController animatorController;
        public string idleState;
        public string thinkingState;
        public string speakingState;
        public Vector3 spawnPosition;
        public Vector3 spawnRotation;
        public Vector3 scale = Vector3.one;
        public string assetVersion;
        public bool approvedForCollection;
        public bool approvedForEditorCollection;
        public bool replaceableAsset;
        public string evidenceReference;

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

        public bool ValidateForFormal(string expectedKey, string expectedRole, out string error)
        {
            if (!string.Equals(key?.Trim(), expectedKey?.Trim(), StringComparison.OrdinalIgnoreCase)) { error = "formal_avatar_preset_key_mismatch"; return false; }
            if (!string.Equals(semanticRole?.Trim(), expectedRole?.Trim(), StringComparison.OrdinalIgnoreCase)) { error = "formal_avatar_semantic_role_mismatch"; return false; }
            if (!HasPrefab || animatorController == null || string.IsNullOrWhiteSpace(voiceProfileKey) || string.IsNullOrWhiteSpace(voiceId)
                || string.IsNullOrWhiteSpace(idleState) || string.IsNullOrWhiteSpace(thinkingState) || string.IsNullOrWhiteSpace(speakingState)
                || scale == Vector3.zero || !mobileReady || !approvedForCollection || string.IsNullOrWhiteSpace(assetVersion) || string.IsNullOrWhiteSpace(evidenceReference))
            { error = "formal_avatar_metadata_incomplete_or_unapproved"; return false; }
            error = string.Empty; return true;
        }

        public bool ValidateForEditorCollection(string expectedKey, out string error)
        {
            if (!string.Equals(key?.Trim(), expectedKey?.Trim(), StringComparison.OrdinalIgnoreCase))
            { error = "editor_collection_avatar_preset_key_mismatch"; return false; }
            if (!HasPrefab || animatorController == null || string.IsNullOrWhiteSpace(voiceProfileKey)
                || string.IsNullOrWhiteSpace(voiceId) || string.IsNullOrWhiteSpace(idleState)
                || string.IsNullOrWhiteSpace(thinkingState) || string.IsNullOrWhiteSpace(speakingState)
                || scale == Vector3.zero || !approvedForEditorCollection || !approvedForCollection
                || !replaceableAsset || string.IsNullOrWhiteSpace(assetVersion)
                || string.IsNullOrWhiteSpace(evidenceReference))
            { error = "editor_collection_avatar_metadata_incomplete_or_unapproved"; return false; }
            error = string.Empty;
            return true;
        }
    }
}
