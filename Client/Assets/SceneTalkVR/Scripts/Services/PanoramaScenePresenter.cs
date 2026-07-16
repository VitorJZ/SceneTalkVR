using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Runtime.Services
{
    /// <summary>
    /// Custom scene presenter that uses PanoramaSceneService to generate 360 backgrounds.
    /// </summary>
    public sealed class PanoramaScenePresenter : MonoBehaviour, ISceneTalkScenePresenter
    {
        [Header("Modules")]
        [SerializeField] private PanoramaSceneService panoramaService;
        [SerializeField] private SceneTalkScenePresenter fallbackPresenter;

        public IEnumerator PresentScene(SpringScenePayload payload, Action onComplete, Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Payload is null.");
                yield break;
            }

            Debug.Log($"[PanoramaScenePresenter] Presenting scene for environment: {payload.environmentType}");

            // 1. Generate and Apply Skybox
            if (panoramaService != null)
            {
                var task = panoramaService.GenerateSkyboxAsync(payload.environmentType, payload.scene?.skyboxUrl);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[PanoramaScenePresenter] Skybox generation failed: {task.Exception?.Message}. Using fallback.");
                }
                else if (task.IsCompletedSuccessfully)
                {
                    panoramaService.ApplySkybox(task.Result);
                }
            }

            // 2. Delegate to fallback for layout objects (3D props)
            if (fallbackPresenter != null)
            {
                yield return fallbackPresenter.PresentScene(payload, onComplete, onError);
            }
            else
            {
                onComplete?.Invoke();
            }
        }
    }
}
