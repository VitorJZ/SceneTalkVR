using System;
using System.Collections.Generic;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPropPresenter : MonoBehaviour
    {
        [SerializeField] private AvatarPropCatalog catalog;
        [SerializeField] private Transform worldPropRoot;
        [SerializeField] private bool includeRoleDefaults = true;

        private readonly List<GameObject> currentProps = new List<GameObject>();

        public void PresentProps(SpringScenePayload payload, GameObject avatar)
        {
            ClearProps();

            if (catalog == null || catalog.props == null || avatar == null)
            {
                return;
            }

            var sockets = avatar.GetComponent<AvatarAttachmentSockets>();
            if (sockets == null)
            {
                sockets = avatar.AddComponent<AvatarAttachmentSockets>();
            }

            var attachedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < catalog.props.Length; i++)
            {
                var entry = catalog.props[i];
                if (entry == null || !entry.IsUsable || attachedKeys.Contains(entry.key))
                {
                    continue;
                }

                if (!ShouldAttach(entry, payload))
                {
                    continue;
                }

                var prop = Attach(entry, sockets, avatar.transform);
                if (prop != null)
                {
                    currentProps.Add(prop);
                    attachedKeys.Add(entry.key);
                }
            }
        }

        public void ClearProps()
        {
            for (var i = currentProps.Count - 1; i >= 0; i--)
            {
                var prop = currentProps[i];
                if (prop != null)
                {
                    Destroy(prop);
                }
            }

            currentProps.Clear();
        }

        private bool ShouldAttach(AvatarPropEntry entry, SpringScenePayload payload)
        {
            if (payload == null || payload.avatarRole == null)
            {
                return false;
            }

            var role = payload.avatarRole.role;
            var appearance = payload.avatarRole.appearance;

            if (includeRoleDefaults && Contains(entry.defaultForRoles, role))
            {
                return true;
            }

            if (appearance == null)
            {
                return false;
            }

            return ContainsAny(entry, appearance.accessories)
                || ContainsAny(entry, appearance.mustHave);
        }

        private static bool ContainsAny(AvatarPropEntry entry, string[] values)
        {
            if (values == null)
            {
                return false;
            }

            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (MatchesEntry(entry, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesEntry(AvatarPropEntry entry, string value)
        {
            return string.Equals(entry.key, value, StringComparison.OrdinalIgnoreCase)
                || Contains(entry.accessoryTags, value);
        }

        private GameObject Attach(AvatarPropEntry entry, AvatarAttachmentSockets sockets, Transform avatarRoot)
        {
            var parent = entry.socket == AvatarPropSocket.World
                ? worldPropRoot != null ? worldPropRoot : avatarRoot
                : sockets.Resolve(entry.socket);

            if (parent == null)
            {
                parent = avatarRoot;
            }

            var prop = Instantiate(entry.prefab, parent);
            prop.name = $"AvatarProp_{entry.key}";
            prop.transform.localPosition = CompensateForParentScale(entry.localPosition, parent);
            prop.transform.localRotation = Quaternion.Euler(entry.localEulerAngles);
            prop.transform.localScale = CompensateForParentScale(
                entry.localScale == Vector3.zero ? Vector3.one : entry.localScale,
                parent);
            return prop;
        }

        private static Vector3 CompensateForParentScale(Vector3 value, Transform parent)
        {
            if (parent == null)
            {
                return value;
            }

            var scale = parent.lossyScale;
            return new Vector3(
                DivideByScale(value.x, scale.x),
                DivideByScale(value.y, scale.y),
                DivideByScale(value.z, scale.z));
        }

        private static float DivideByScale(float value, float scale)
        {
            return Mathf.Abs(scale) > 0.0001f ? value / scale : value;
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
