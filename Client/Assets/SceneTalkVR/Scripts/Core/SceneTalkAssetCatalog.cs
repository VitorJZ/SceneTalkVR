using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [CreateAssetMenu(fileName = "SceneTalkAssetCatalog", menuName = "SceneTalkVR/Asset Catalog")]
    public sealed class SceneTalkAssetCatalog : ScriptableObject
    {
        [Serializable]
        public struct AssetMapping
        {
            public string key;
            public GameObject prefab;
        }

        [SerializeField] private List<AssetMapping> mappings = new List<AssetMapping>();

        public List<AssetMapping> Mappings => mappings;

        public GameObject FindPrefab(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            foreach (var mapping in mappings)
            {
                if (string.Equals(mapping.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return mapping.prefab;
                }
            }
            return null;
        }
    }
}
