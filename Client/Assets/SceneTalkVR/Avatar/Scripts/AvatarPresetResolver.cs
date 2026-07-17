using System;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPresetResolver : MonoBehaviour
    {
        [SerializeField] private AvatarCatalog catalog;
        public AvatarResolutionResult Resolve(SpringScenePayload payload)
        {
            if (payload == null)
            {
                return AvatarResolutionResult.Empty("Payload is null.");
            }

            if (catalog == null)
            {
                return AvatarResolutionResult.Empty("Avatar catalog is not assigned.");
            }

            var requestedKey = payload.avatarRole == null ? string.Empty : payload.avatarRole.presetKey;
            if (!string.IsNullOrWhiteSpace(requestedKey))
            {
                var exact = catalog.FindByKey(requestedKey);
                if (exact != null && exact.IsUsable) return CreateResult(exact, "exact_task_key", string.Empty);
                if (payload.avatarRole != null && !string.IsNullOrWhiteSpace(payload.avatarRole.presetKey))
                    return AvatarResolutionResult.Empty($"Task requires unavailable avatar preset '{requestedKey}'.");
            }

            var scenarioEntry = catalog.FindByScenarioId(payload.taskType);
            if (scenarioEntry != null)
            {
                return CreateResult(scenarioEntry, "fixed_scenario", string.Empty);
            }

            var fallback = catalog.GetDefault();
            if (fallback != null)
            {
                return CreateResult(
                    fallback,
                    "global",
                    $"No avatar is mapped to fixed scenario '{payload.taskType}'. Falling back to the catalog default.");
            }

            return AvatarResolutionResult.Empty($"No usable avatar is configured for fixed scenario '{payload.taskType}'.");
        }

        private static AvatarResolutionResult CreateResult(
            AvatarPresetEntry preset,
            string fallbackLevel,
            string fallbackReason)
        {
            return new AvatarResolutionResult
            {
                avatarKey = preset == null ? string.Empty : preset.key,
                fallbackLevel = fallbackLevel,
                fallbackReason = fallbackReason,
                score = 0,
                preset = preset
            };
        }
    }
}
