using System;
using System.Collections;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class PrefabAvatarInstanceLoader : MonoBehaviour, IAvatarInstanceLoader
    {
        private const float MinimumProbeDistance = 0.01f;

        [Header("Transform Reset")]
        [SerializeField] private bool resetLocalTransform = true;

        [Header("Ground Alignment")]
        [SerializeField] private bool alignToGround = true;
        [SerializeField, Tooltip("Physics layers that may provide a walkable ground surface.")]
        private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0f), Tooltip("Distance above the avatar bounds where the downward ground probe starts.")]
        private float groundProbeStartHeight = 2f;
        [SerializeField, Min(MinimumProbeDistance), Tooltip("Maximum downward distance used to find a ground collider.")]
        private float groundProbeDistance = 50f;
        [SerializeField, Range(0f, 89f), Tooltip("Steepest surface that can be treated as ground.")]
        private float maxGroundSlope = 60f;
        [SerializeField, Tooltip("World-space ground height used when no suitable collider is found.")]
        private float fallbackGroundY;
        [SerializeField, Tooltip("Small vertical clearance added above the resolved ground surface.")]
        private float groundClearance;
        [SerializeField] private bool logGroundAlignment = true;

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

            if (alignToGround)
            {
                AlignToGround(instance, resolution.avatarKey);
            }

            onComplete?.Invoke(instance);
        }

        private void AlignToGround(GameObject instance, string avatarKey)
        {
            if (!TryGetVisualBounds(instance, out var visualBounds))
            {
                Debug.LogWarning(
                    $"[SceneTalkVR] Avatar ground alignment skipped for '{avatarKey}': no active renderer bounds were found.",
                    this);
                return;
            }

            var groundY = fallbackGroundY;
            var groundSource = $"fallback world Y={fallbackGroundY:0.###}";

            if (TryFindGround(instance.transform, visualBounds, out var groundHit))
            {
                groundY = groundHit.point.y;
                groundSource = $"collider '{groundHit.collider.name}'";
            }

            var verticalAdjustment = groundY + groundClearance - visualBounds.min.y;
            instance.transform.position += Vector3.up * verticalAdjustment;
            Physics.SyncTransforms();

            if (logGroundAlignment)
            {
                Debug.Log(
                    $"[SceneTalkVR] Avatar ground aligned: key={avatarKey}, source={groundSource}, "
                    + $"groundY={groundY:0.###}, adjustment={verticalAdjustment:0.###}.",
                    this);
            }
        }

        private bool TryFindGround(Transform avatarTransform, Bounds visualBounds, out RaycastHit groundHit)
        {
            Physics.SyncTransforms();

            var probeStartY = Mathf.Max(visualBounds.max.y, fallbackGroundY)
                + Mathf.Max(0f, groundProbeStartHeight);
            var probeOrigin = new Vector3(visualBounds.center.x, probeStartY, visualBounds.center.z);
            var hits = Physics.RaycastAll(
                probeOrigin,
                Vector3.down,
                Mathf.Max(MinimumProbeDistance, groundProbeDistance),
                groundLayers,
                QueryTriggerInteraction.Ignore);
            var minimumGroundDot = Mathf.Cos(maxGroundSlope * Mathf.Deg2Rad);
            var nearestDistance = float.PositiveInfinity;
            groundHit = default;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null
                    || hit.transform == avatarTransform
                    || hit.transform.IsChildOf(avatarTransform)
                    || Vector3.Dot(hit.normal, Vector3.up) < minimumGroundDot
                    || hit.distance >= nearestDistance)
                {
                    continue;
                }

                groundHit = hit;
                nearestDistance = hit.distance;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        private static bool TryGetVisualBounds(GameObject instance, out Bounds visualBounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            visualBounds = default;
            var initialized = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!initialized)
                {
                    visualBounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    visualBounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }
    }
}
