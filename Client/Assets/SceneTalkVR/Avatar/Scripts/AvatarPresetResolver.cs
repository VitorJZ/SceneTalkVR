using System;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPresetResolver : MonoBehaviour
    {
        [SerializeField] private AvatarCatalog catalog;
        [SerializeField] private int minimumExactScore = 45;

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

            var role = payload.avatarRole;
            var appearance = role == null ? null : role.appearance;
            var bestEntry = FindBestEntry(payload.environmentType, role, appearance, out var bestScore);

            if (bestEntry != null && bestScore >= minimumExactScore)
            {
                return CreateResult(bestEntry, "exact_or_close", string.Empty, bestScore);
            }

            var fallback = FindRoleFallback(role);
            if (fallback != null)
            {
                return CreateResult(fallback, "role", "No high-confidence match. Falling back to role default.", bestScore);
            }

            fallback = FindEnvironmentFallback(payload.environmentType);
            if (fallback != null)
            {
                return CreateResult(fallback, "environment", "No role match. Falling back to environment default.", bestScore);
            }

            fallback = catalog.GetDefault();
            if (fallback != null)
            {
                return CreateResult(fallback, "global", "No specific match. Falling back to global default.", bestScore);
            }

            return AvatarResolutionResult.Empty("No usable Avatar preset found.");
        }

        private AvatarPresetEntry FindBestEntry(
            string environmentType,
            AvatarRoleData role,
            AvatarAppearanceData appearance,
            out int bestScore)
        {
            bestScore = 0;
            AvatarPresetEntry bestEntry = null;

            if (catalog.presets == null)
            {
                return null;
            }

            for (var i = 0; i < catalog.presets.Length; i++)
            {
                var candidate = catalog.presets[i];
                if (candidate == null || !candidate.IsUsable)
                {
                    continue;
                }

                var score = ScoreCandidate(candidate, environmentType, role, appearance);
                if (bestEntry == null || score > bestScore || score == bestScore && candidate.priority > bestEntry.priority)
                {
                    bestEntry = candidate;
                    bestScore = score;
                }
            }

            return bestEntry;
        }

        private int ScoreCandidate(
            AvatarPresetEntry candidate,
            string environmentType,
            AvatarRoleData role,
            AvatarAppearanceData appearance)
        {
            var score = 0;

            if (role != null && Contains(candidate.roles, role.role))
            {
                score += 40;
            }

            if (Contains(candidate.environmentTags, environmentType))
            {
                score += 20;
            }

            if (appearance == null)
            {
                return score + candidate.priority;
            }

            if (Contains(candidate.styleIds, appearance.styleId))
            {
                score += 5;
            }

            if (Contains(candidate.genderPresentations, appearance.genderPresentation))
            {
                score += 5;
            }

            if (Contains(candidate.ageBuckets, appearance.ageBucket))
            {
                score += 5;
            }

            if (Contains(candidate.bodyBuilds, appearance.bodyBuild))
            {
                score += 5;
            }

            if (Contains(candidate.hairStyles, appearance.hairStyle))
            {
                score += 5;
            }

            if (Contains(candidate.hairColors, appearance.hairColor))
            {
                score += 5;
            }

            if (Contains(candidate.outfitRoles, appearance.outfitRole))
            {
                score += 20;
            }

            if (Contains(candidate.outfitColors, appearance.outfitColor))
            {
                score += 5;
            }

            score += CountMatches(candidate.accessoryTags, appearance.accessories) * 5;
            score += CountMatches(candidate.mustHaveTags, appearance.mustHave) * 10;

            if (candidate.mobileReady)
            {
                score += 5;
            }

            return score + candidate.priority;
        }

        private AvatarPresetEntry FindRoleFallback(AvatarRoleData role)
        {
            if (role == null || catalog.presets == null)
            {
                return null;
            }

            for (var i = 0; i < catalog.presets.Length; i++)
            {
                var candidate = catalog.presets[i];
                if (candidate != null && candidate.IsUsable && Contains(candidate.roles, role.role))
                {
                    return candidate;
                }
            }

            return null;
        }

        private AvatarPresetEntry FindEnvironmentFallback(string environmentType)
        {
            if (catalog.presets == null)
            {
                return null;
            }

            for (var i = 0; i < catalog.presets.Length; i++)
            {
                var candidate = catalog.presets[i];
                if (candidate != null && candidate.IsUsable && Contains(candidate.environmentTags, environmentType))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static AvatarResolutionResult CreateResult(
            AvatarPresetEntry preset,
            string fallbackLevel,
            string fallbackReason,
            int score)
        {
            return new AvatarResolutionResult
            {
                avatarKey = preset == null ? string.Empty : preset.key,
                fallbackLevel = fallbackLevel,
                fallbackReason = fallbackReason,
                score = score,
                preset = preset
            };
        }

        private static int CountMatches(string[] haystack, string[] needles)
        {
            if (haystack == null || needles == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < needles.Length; i++)
            {
                if (Contains(haystack, needles[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Contains(string[] values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
