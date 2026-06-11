using System;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [CreateAssetMenu(fileName = "AvatarPropCatalog", menuName = "SceneTalkVR/Avatar Prop Catalog")]
    public sealed class AvatarPropCatalog : ScriptableObject
    {
        public AvatarPropEntry[] props = Array.Empty<AvatarPropEntry>();

        public AvatarPropEntry FindByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || props == null)
            {
                return null;
            }

            for (var i = 0; i < props.Length; i++)
            {
                var candidate = props[i];
                if (candidate != null && string.Equals(candidate.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
