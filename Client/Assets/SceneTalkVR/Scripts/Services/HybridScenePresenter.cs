using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Runtime.Services
{
    /// <summary>
    /// Hybrid scene presenter that combines Panorama backgrounds and Holodeck 3D layouts.
    /// </summary>
    public sealed class HybridScenePresenter : MonoBehaviour, ISceneTalkScenePresenter, ISceneTalkSceneSnapshotProvider
    {
        [Header("Services")]
        [SerializeField] private PanoramaSceneService panoramaService;
        [SerializeField] private HolodeckSceneService holodeckService;
        
        [Header("Settings")]
        [SerializeField] private Transform sceneRoot;
        [SerializeField] private float spawnScale = 1.0f;
        [SerializeField] private bool autoCenterObjects = true;
        [SerializeField] private Vector3 sceneOffset = new Vector3(0, 0, 2.5f); // Move objects 2.5m forward
        
        [Header("Asset Configuration")]
        [SerializeField] private SceneTalkAssetCatalog assetCatalog;

        [Header("Render Mode")]
        [SerializeField] private bool onlyUsePanorama = true;

        [Header("Safe Spatial Bounds (Clipper)")]
        [SerializeField] private bool enableSpatialClipping = true;
        [SerializeField] private float minX = -1.2f;
        [SerializeField] private float maxX = 1.2f;
        [SerializeField] private float minZ = 1.0f;
        [SerializeField] private float maxZ = 2.5f;

        [Header("Asset Filters")]
        [SerializeField] private int maxSpawnCount = 2;
        [SerializeField] private List<string> prefabWhitelist = new List<string> { "cafe_table", "chair", "generic_table", "generic_chair" };
        private readonly List<LayoutObjectData> lastResolvedLayout = new List<LayoutObjectData>();

        public bool OnlyUsePanorama => onlyUsePanorama;
        public bool EnableSpatialClipping => enableSpatialClipping;
        public int MaxSpawnCount => maxSpawnCount;

        public void ConfigureRuntime(bool usePanoramaOnly, bool useSpatialClipping, int spawnLimit)
        {
            onlyUsePanorama = usePanoramaOnly;
            enableSpatialClipping = useSpatialClipping;
            maxSpawnCount = Mathf.Max(0, spawnLimit);
        }

        public IEnumerator PresentScene(SpringScenePayload payload, Action onComplete, Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Payload is null.");
                yield break;
            }

            ClearScene();
            lastResolvedLayout.Clear();

            var isHistorySnapshot = payload.scene != null
                && !string.IsNullOrWhiteSpace(payload.scene.skyboxUrl)
                && payload.scene.skyboxUrl.StartsWith("history://", StringComparison.OrdinalIgnoreCase);

            // 1. Generate Panorama Background
            var panoTask = panoramaService.GenerateSkyboxAsync(payload.environmentType, payload.scene?.skyboxUrl);
            
            // 2. Generate Holodeck 3D Layout (only if onlyUsePanorama is false)
            Task<HolodeckSceneService.HolodeckResponse> holodeckTask = null;
            if (!onlyUsePanorama && !isHistorySnapshot)
            {
                holodeckTask = holodeckService.GenerateLayoutAsync(payload.environmentType);
            }

            // Wait for tasks
            while (!panoTask.IsCompleted || (holodeckTask != null && !holodeckTask.IsCompleted))
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
                if (isHistorySnapshot)
                {
                    onError?.Invoke($"Failed to restore the saved panorama: {panoTask.Exception?.GetBaseException().Message}");
                    yield break;
                }
            }

            // Apply 3D Objects (only if onlyUsePanorama is false)
            if (!onlyUsePanorama && holodeckTask != null)
            {
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
            }
            else if (!onlyUsePanorama && isHistorySnapshot)
            {
                InstantiateSnapshotObjects(payload.scene?.layoutObjects);
            }

            onComplete?.Invoke();
        }

        public IEnumerator CaptureSceneSnapshot(
            string sessionId,
            SpringScenePayload payload,
            Action<SpringScenePayload> onComplete,
            Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Cannot capture a null scene payload.");
                yield break;
            }

            var snapshot = SceneTalkVR.History.LearningMemoryService.ClonePayload(payload);
            snapshot.scene ??= new ScenePayload();
            var sourceUrl = snapshot.scene.skyboxUrl ?? string.Empty;
            var requiresCachedPanorama = !sourceUrl.StartsWith("demo://", StringComparison.OrdinalIgnoreCase)
                && !sourceUrl.StartsWith("history://", StringComparison.OrdinalIgnoreCase);
            if (requiresCachedPanorama)
            {
                if (panoramaService == null
                    || !panoramaService.TrySaveHistoryTexture(sessionId, out var historyUri))
                {
                    onError?.Invoke("The generated panorama could not be cached for conversation history.");
                    yield break;
                }

                snapshot.scene.skyboxUrl = historyUri;
            }

            if (lastResolvedLayout.Count > 0)
            {
                snapshot.scene.layoutObjects = lastResolvedLayout.ToArray();
            }

            onComplete?.Invoke(snapshot);
            yield break;
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
            if (onlyUsePanorama)
            {
                Debug.Log("[HybridScenePresenter] onlyUsePanorama is enabled. Skipping 3D object instantiation.");
                return;
            }
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

            int spawnedCount = 0;

            foreach (var objData in response.objects)
            {
                // 1. Map Holodeck Name to PrefabKey Whitelist
                string mappedKey = MapToPrefabKey(objData.name);
                string resolvedKey = mappedKey;
                
                // Whitelist filter check
                if (prefabWhitelist != null && prefabWhitelist.Count > 0)
                {
                    if (!prefabWhitelist.Contains(mappedKey))
                    {
                        Debug.Log($"[HybridScenePresenter] Skipping '{mappedKey}' (Original: '{objData.name}') - not in whitelist.");
                        continue;
                    }
                }

                // 2. Find mapped prefab
                GameObject prefab = FindPrefab(mappedKey);
                if (prefab == null)
                {
                    string fallbackKey = "generic_decor";
                    if (mappedKey.Contains("table")) fallbackKey = "generic_table";
                    else if (mappedKey.Contains("chair") || mappedKey.Contains("sofa")) fallbackKey = "generic_chair";

                    prefab = FindPrefab(fallbackKey);
                    resolvedKey = fallbackKey;
                    
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[HybridScenePresenter] No prefab or fallback mapped for: '{mappedKey}' (Original: '{objData.name}'). Skipping instantiation.");
                        continue;
                    }
                    
                    Debug.Log($"[HybridScenePresenter] Mapped '{objData.name}' to fallback prefab '{fallbackKey}'");
                }

                // 3. Handle coordinate format from Python backend
                Vector3 pos = Vector3.zero;
                if (objData.position != null && objData.position.Length >= 3)
                {
                    pos = new Vector3(objData.position[0], objData.position[1], objData.position[2]);
                }
                
                pos -= centerOffset; // Move objects to be centered around user
                pos += sceneOffset; // Apply manual viewing offset

                // Apply spatial bounds clamping to prevent visual collisions with skybox furniture
                if (enableSpatialClipping)
                {
                    pos.x = Mathf.Clamp(pos.x, minX, maxX);
                    pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
                    pos.y = 0f; // Force flat ground level
                }

                Quaternion rot = Quaternion.Euler(0, objData.rotation, 0);

                var instance = Instantiate(prefab, pos, rot, sceneRoot);
                instance.name = $"{mappedKey}_{Guid.NewGuid().ToString().Substring(0, 4)}";
                instance.transform.localScale = Vector3.one * spawnScale;

                lastResolvedLayout.Add(new LayoutObjectData
                {
                    prefabKey = resolvedKey,
                    position = pos,
                    rotationY = objData.rotation
                });

                spawnedCount++;
                if (spawnedCount >= maxSpawnCount)
                {
                    Debug.Log($"[HybridScenePresenter] Reached max spawn limit of {maxSpawnCount}. Stopping instantiation.");
                    break;
                }
            }
        }

        private void InstantiateSnapshotObjects(LayoutObjectData[] layoutObjects)
        {
            if (layoutObjects == null || sceneRoot == null)
            {
                return;
            }

            var spawnedCount = 0;
            foreach (var item in layoutObjects)
            {
                if (item == null || spawnedCount >= maxSpawnCount)
                {
                    continue;
                }

                var prefab = FindPrefab(item.prefabKey);
                if (prefab == null)
                {
                    continue;
                }

                var position = item.position;
                if (enableSpatialClipping)
                {
                    position.x = Mathf.Clamp(position.x, minX, maxX);
                    position.z = Mathf.Clamp(position.z, minZ, maxZ);
                    position.y = 0f;
                }

                var instance = Instantiate(
                    prefab,
                    position,
                    Quaternion.Euler(0f, item.rotationY, 0f),
                    sceneRoot);
                instance.name = $"{item.prefabKey}_history";
                instance.transform.localScale = Vector3.one * spawnScale;
                spawnedCount++;
            }
        }

        private string MapToPrefabKey(string originalName)
        {
            if (string.IsNullOrEmpty(originalName)) return "generic_decor";
            
            string lowerName = originalName.ToLowerInvariant();
            
            // Fuzzy match table/desk/counter elements to generic_table
            if (lowerName.Contains("table") || 
                lowerName.Contains("desk") || 
                lowerName.Contains("counter") || 
                lowerName.Contains("communal") || 
                lowerName.Contains("bench") || 
                lowerName.Contains("bar"))
            {
                return "generic_table";
            }

            // Fuzzy match chairs/stools/sofas to generic_chair
            if (lowerName.Contains("chair") || 
                lowerName.Contains("stool") || 
                lowerName.Contains("sofa") || 
                lowerName.Contains("couch") || 
                lowerName.Contains("seat"))
            {
                return "generic_chair";
            }
            
            // Decor elements mapping
            if (lowerName.Contains("plant") || lowerName.Contains("succulent") || lowerName.Contains("flower")) return "plant";
            if (lowerName.Contains("shelf")) return "wall_shelf";
            if (lowerName.Contains("menu")) return "menu_board";
            if (lowerName.Contains("register") || lowerName.Contains("cash")) return "cash_register";
            if (lowerName.Contains("mug") || lowerName.Contains("cup")) return "coffee_mug";
            if (lowerName.Contains("lamp") || lowerName.Contains("light")) return "lamp";

            return "generic_decor";
        }

        private GameObject FindPrefab(string key)
        {
            if (assetCatalog == null)
            {
                Debug.LogWarning("[HybridScenePresenter] Asset Catalog is not assigned.");
                return null;
            }
            return assetCatalog.FindPrefab(key);
        }

    }
}
