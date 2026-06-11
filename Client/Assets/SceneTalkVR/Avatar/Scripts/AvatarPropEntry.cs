using System;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [Serializable]
    public sealed class AvatarPropEntry
    {
        [Header("Identity")]
        public string key;
        public string displayName;
        public int priority;

        [Header("Runtime Asset")]
        public GameObject prefab;
        public string addressableKey;

        [Header("Matching Tags")]
        public string[] defaultForRoles = Array.Empty<string>();
        public string[] accessoryTags = Array.Empty<string>();
        public string[] environmentTags = Array.Empty<string>();

        [Header("Attachment")]
        public AvatarPropSocket socket = AvatarPropSocket.AvatarRoot;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;

        [Header("Constraints")]
        public bool mobileReady = true;

        public bool HasPrefab => prefab != null;
        public bool HasAddressableKey => !string.IsNullOrWhiteSpace(addressableKey);
        public bool IsUsable => !string.IsNullOrWhiteSpace(key) && (HasPrefab || HasAddressableKey);
    }
}
