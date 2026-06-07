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

            // 1. Generate Panorama Background (Parallel start if possible, but keep sequence for simplicity here)
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

            foreach (var objData in response.objects)
            {
                GameObject prefab = FindPrefab(objData.name);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HybridScenePresenter] No prefab mapping found for: {objData.name}. Creating placeholder.");
                    prefab = CreatePlaceholder(objData.name);
                }

                Vector3 pos = new Vector3(objData.position[0], objData.position[1], objData.position[2]);
                Quaternion rot = Quaternion.Euler(0, objData.rotation, 0);

                var instance = Instantiate(prefab, pos, rot, sceneRoot);
                instance.name = objData.name;
                instance.transform.localScale = Vector3.one * spawnScale;
            }
        }

        private GameObject FindPrefab(string name)
        {
            foreach (var mapping in objectLibrary)
            {
                if (name.Contains(mapping.key, StringComparison.OrdinalIgnoreCase))
                    return mapping.prefab;
            }
            return null;
        }

        private GameObject CreatePlaceholder(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            return go;
        }
    }
}
