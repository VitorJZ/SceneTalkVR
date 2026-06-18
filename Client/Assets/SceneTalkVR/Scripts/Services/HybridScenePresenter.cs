using System;
using System.Collections;
using System.Collections.Generic;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Runtime.Services
{
    /// <summary>
    /// Hybrid scene presenter that combines Panorama backgrounds and Holodeck 3D layouts.
    /// </summary>
    public sealed class HybridScenePresenter : MonoBehaviour, ISceneTalkScenePresenter
    {
        [Header("Services")]
        [SerializeField] private PanoramaSceneService panoramaService;
        [SerializeField] private HolodeckSceneService holodeckService;
        
        [Header("Settings")]
        [SerializeField] private Transform sceneRoot;
        [SerializeField] private float spawnScale = 1.0f;
        [SerializeField] private bool autoCenterObjects = true;
        
        [Serializable]
        private struct PrefabMapping
        {
            public string key;
            public GameObject prefab;
        }
        
        [SerializeField] private List<PrefabMapping> objectLibrary = new List<PrefabMapping>();

        public IEnumerator PresentScene(SpringScenePayload payload, Action onComplete, Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Payload is null.");
                yield break;
            }

            ClearScene();

            // 1. Generate Panorama Background
            var panoTask = panoramaService.GenerateSkyboxAsync(payload.environmentType);
            
            // 2. Generate Holodeck 3D Layout
            var holodeckTask = holodeckService.GenerateLayoutAsync(payload.environmentType);

            // Wait for both
            while (!panoTask.IsCompleted || !holodeckTask.IsCompleted)
            {
                yield return null;
            }

            // Apply Background
            if (panoTask.IsCompletedSuccessfully)
            {
                panoramaService.ApplySkybox(panoTask.Result);
            }
            else
            {
                Debug.LogWarning($"[HybridScenePresenter] Panorama failed: {panoTask.Exception?.Message}");
            }

            // Apply 3D Objects
            if (holodeckTask.IsCompletedSuccessfully)
            {
                InstantiateHolodeckObjects(holodeckTask.Result);
            }
            else
            {
                Debug.LogError($"[HybridScenePresenter] Holodeck failed: {holodeckTask.Exception?.Message}");
                onError?.Invoke($"Failed to generate 3D layout: {holodeckTask.Exception?.Message}");
                yield break;
            }

            onComplete?.Invoke();
        }

        private void ClearScene()
        {
            if (sceneRoot == null) return;
            foreach (Transform child in sceneRoot)
            {
                Destroy(child.gameObject);
            }
        }

        private void InstantiateHolodeckObjects(HolodeckSceneService.HolodeckResponse response)
        {
            if (response?.objects == null || sceneRoot == null) return;

            Debug.Log($"[HybridScenePresenter] Received {response.objects.Length} objects from backend.");

            Vector3 centerOffset = Vector3.zero;
            if (autoCenterObjects && response.objects.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var obj in response.objects)
                {
                    if (obj.position != null && obj.position.Length >= 3)
                    {
                        sum += new Vector3(obj.position[0], obj.position[1], obj.position[2]);
                        count++;
                    }
                }
                if (count > 0)
                {
                    centerOffset = sum / count;
                    centerOffset.y = 0; // Keep objects on ground
                    Debug.Log($"[HybridScenePresenter] Auto-centering scene. Applied offset: {-centerOffset}");
                }
            }

            foreach (var objData in response.objects)
            {
                // 1. Map Holodeck Name to PrefabKey Whitelist
                string mappedKey = MapToPrefabKey(objData.name);
                
                // 2. Find mapped prefab
                GameObject prefab = FindPrefab(mappedKey);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HybridScenePresenter] No prefab mapped for: '{mappedKey}' (Original: '{objData.name}'). Using generic fallback.");
                    prefab = GetGenericFallback(mappedKey);
                }

                // 3. Handle coordinate format from Python backend
                Vector3 pos = Vector3.zero;
                if (objData.position != null && objData.position.Length >= 3)
                {
                    pos = new Vector3(objData.position[0], objData.position[1], objData.position[2]);
                }
                
                pos -= centerOffset; // Move objects to be centered around user

                Quaternion rot = Quaternion.Euler(0, objData.rotation, 0);

                var instance = Instantiate(prefab, pos, rot, sceneRoot);
                instance.name = $"{mappedKey}_{Guid.NewGuid().ToString().Substring(0, 4)}";
                instance.transform.localScale = Vector3.one * spawnScale;
            }
        }

        private string MapToPrefabKey(string originalName)
        {
            if (string.IsNullOrEmpty(originalName)) return "generic_decor";
            
            string lowerName = originalName.ToLowerInvariant();
            
            // Strict mapping to docs/PrefabKeyWhitelist.md
            if (lowerName.Contains("counter")) return "coffee_counter";
            if (lowerName.Contains("cafe") && lowerName.Contains("table")) return "cafe_table";
            if (lowerName.Contains("sofa") || lowerName.Contains("couch")) return "sofa";
            if (lowerName.Contains("chair")) return "chair";
            if (lowerName.Contains("plant") || lowerName.Contains("succulent") || lowerName.Contains("flower")) return "plant";
            if (lowerName.Contains("shelf")) return "wall_shelf";
            if (lowerName.Contains("menu")) return "menu_board";
            if (lowerName.Contains("register") || lowerName.Contains("cash")) return "cash_register";
            if (lowerName.Contains("mug") || lowerName.Contains("cup")) return "coffee_mug";
            if (lowerName.Contains("lamp") || lowerName.Contains("light")) return "lamp";
            if (lowerName.Contains("table")) return "cafe_table";
            
            // Generic fallbacks
            if (lowerName.Contains("desk") || lowerName.Contains("table")) return "generic_table";
            if (lowerName.Contains("seat") || lowerName.Contains("stool")) return "generic_chair";

            return "generic_decor";
        }

        private GameObject FindPrefab(string key)
        {
            foreach (var mapping in objectLibrary)
            {
                if (string.Equals(key, mapping.key, StringComparison.OrdinalIgnoreCase))
                    return mapping.prefab;
            }
            return null;
        }

        private GameObject GetGenericFallback(string key)
        {
            PrimitiveType type = PrimitiveType.Cube;
            Vector3 scale = new Vector3(0.3f, 0.3f, 0.3f);
            
            if (key.Contains("table"))
            {
                type = PrimitiveType.Cube;
                scale = new Vector3(1.2f, 0.8f, 1.2f);
            }
            else if (key.Contains("chair") || key.Contains("sofa"))
            {
                type = PrimitiveType.Cylinder;
                scale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            else if (key.Contains("plant"))
            {
                type = PrimitiveType.Sphere;
                scale = new Vector3(0.4f, 0.6f, 0.4f);
            }

            var go = GameObject.CreatePrimitive(type);
            go.transform.localScale = scale;
            
            // Visual indicator for generic fallbacks
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.cyan;
            }
            
            return go;
        }
    }
}
