using System;
using System.Collections;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class PrefabAvatarInstanceLoader : MonoBehaviour, IAvatarInstanceLoader
    {
        [SerializeField] private bool resetLocalTransform = true;

        public IEnumerator LoadAvatar(
            AvatarResolutionResult resolution,
            Transform parent,
            Action<GameObject> onComplete,
            Action<string> onError)
        {
            if (resolution == null)
            {
                onError?.Invoke("Avatar resolution result is null.");
                yield break;
            }

            if (resolution.preset == null)
            {
                onError?.Invoke(string.IsNullOrWhiteSpace(resolution.fallbackReason)
                    ? "Avatar resolution does not contain a preset."
                    : resolution.fallbackReason);
                yield break;
            }

            if (resolution.preset.prefab == null)
            {
                onError?.Invoke($"Avatar preset '{resolution.avatarKey}' does not have a prefab assigned.");
                yield break;
            }

            var instance = Instantiate(resolution.preset.prefab, parent);
            instance.name = string.IsNullOrWhiteSpace(resolution.avatarKey)
                ? resolution.preset.prefab.name
                : $"Avatar_{resolution.avatarKey}";

            if (resetLocalTransform)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
            }

            yield return null;
            onComplete?.Invoke(instance);
        }
    }
}
