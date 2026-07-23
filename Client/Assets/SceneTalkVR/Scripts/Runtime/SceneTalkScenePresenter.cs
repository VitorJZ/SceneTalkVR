using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkScenePresenter : MonoBehaviour, ISceneTalkScenePresenter, ISceneTalkSceneSnapshotProvider, ISceneTalkPresentedSceneClearer
    {
        [Serializable]
        private sealed class PrefabBinding
        {
            public string key;
            public GameObject prefab;
        }

        [SerializeField] private Transform sceneRoot;
        [SerializeField] private Material fallbackSkybox;
        [SerializeField] private PrefabBinding[] prefabCatalog = Array.Empty<PrefabBinding>();
        [SerializeField] private int maxSpawnedObjects = 8;
        [SerializeField] private float maxSpawnDistance = 3f;

        public IEnumerator PresentScene(SpringScenePayload payload, Action onComplete, Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Spring scene payload is null.");
                yield break;
            }

            ClearSceneRoot();
            ApplySkybox(payload.scene);
            SpawnLayoutObjects(payload.scene);

            onComplete?.Invoke();
            yield break;
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

            onComplete?.Invoke(SceneTalkVR.History.LearningMemoryService.ClonePayload(payload));
            yield break;
        }

        public void ClearPresentedScene()
        {
            ClearSceneRoot();
        }

        private void ClearSceneRoot()
        {
            if (sceneRoot == null)
            {
                return;
            }

            for (var i = sceneRoot.childCount - 1; i >= 0; i--)
            {
                var child = sceneRoot.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void ApplySkybox(ScenePayload scene)
        {
            if (scene == null)
            {
                return;
            }

            if (string.Equals(scene.mode, "skybox", StringComparison.OrdinalIgnoreCase) && fallbackSkybox != null)
            {
                RenderSettings.skybox = fallbackSkybox;
                DynamicGI.UpdateEnvironment();
            }
        }

        private void SpawnLayoutObjects(ScenePayload scene)
        {
            if (sceneRoot == null || scene == null || scene.layoutObjects == null)
            {
                return;
            }

            var spawned = 0;

            foreach (var layoutObject in scene.layoutObjects)
            {
                if (layoutObject == null || spawned >= maxSpawnedObjects)
                {
                    continue;
                }

                var prefab = FindPrefab(layoutObject.prefabKey);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SceneTalkVR] No prefab binding found for key '{layoutObject.prefabKey}'.", this);
                    continue;
                }

                var position = Vector3.ClampMagnitude(layoutObject.position, maxSpawnDistance);
                var rotation = Quaternion.Euler(0f, layoutObject.rotationY, 0f);
                Instantiate(prefab, position, rotation, sceneRoot);
                spawned++;
            }
        }

        private GameObject FindPrefab(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            foreach (var binding in prefabCatalog)
            {
                if (binding != null && string.Equals(binding.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return binding.prefab;
                }
            }

            return null;
        }
    }
}
