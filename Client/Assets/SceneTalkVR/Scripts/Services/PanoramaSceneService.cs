using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using SceneTalkVR.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneTalkVR.Runtime.Services
{
    /// <summary>
    /// Service for generating 360 panorama scenes using SiliconFlow API (Domestic Model).
    /// Optimized for Tongyi-MAI/Z-Image or Kwai-Kolors/Kolors.
    /// </summary>
    public sealed class PanoramaSceneService : MonoBehaviour
    {
        private const string SiliconFlowUrl = "https://api.siliconflow.cn/v1/images/generations";
        
        [Header("API Configuration")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private string modelName = "Tongyi-MAI/Z-Image";
        [SerializeField] private string imageSize = "1024x1024";

        [Header("Fallback Settings")]
        [SerializeField] private Texture2D fallbackTexture;
        [SerializeField] private string localFallbackPath = "SceneTalkVR/Textures/FallbackPanorama";

        [Header("Debug Controls")]
        [Tooltip("If enabled, bypasses SiliconFlow API and forces using local fallback panorama.")]
        [SerializeField] private bool forceUseFallback = false;

        [Header("Sky Sphere Settings")]
        [Tooltip("If enabled, renders background inside a 3D Sphere in the scene to allow scaling.")]
        [SerializeField] private bool useSkySphere = false;
        [SerializeField] private float skySphereScale = 20.0f;
        [Tooltip("Physical position offset of the Sky Sphere. Lowering Y (e.g. -1.6) aligns the panorama floor with physical ground.")]
        [SerializeField] private Vector3 skySpherePositionOffset = new Vector3(0f, -1.6f, 0f);
        [SerializeField] private Material skySphereMaterial;

        private GameObject skySphereInstance;

        private void Update()
        {
            if (useSkySphere && skySphereInstance != null)
            {
                skySphereInstance.transform.localScale = Vector3.one * skySphereScale;
                skySphereInstance.transform.position = skySpherePositionOffset;
            }
        }

        public async Task<Texture2D> GenerateSkyboxAsync(string environmentDescription)
        {
            if (forceUseFallback)
            {
                Debug.Log("[PanoramaSceneService] Force Use Fallback is enabled. Loading local fallback.");
                return LoadLocalFallback();
            }

            string effectiveKey = string.IsNullOrEmpty(apiKey) 
                ? Environment.GetEnvironmentVariable("SILICONFLOW_API_KEY") 
                : apiKey;

            if (string.IsNullOrEmpty(effectiveKey))
            {
                Debug.LogWarning("[PanoramaSceneService] API Key missing. Using local fallback.");
                return LoadLocalFallback();
            }

            // Enhance prompt for 360 panorama
            string enhancedPrompt = $"{environmentDescription}, 360 degree equirectangular panorama, highly detailed, high resolution, seamless";
            Debug.Log($"[PanoramaSceneService] Requesting SiliconFlow generation with prompt: {enhancedPrompt}");

            try
            {
                // 1. Request generation
                string imageUrl = await RequestGeneration(effectiveKey, enhancedPrompt);
                Debug.Log($"[PanoramaSceneService] Image URL received: {imageUrl}");

                // 2. Download texture
                return await DownloadTexture(imageUrl);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PanoramaSceneService] API request failed: {ex.Message}. Using local fallback.");
                return LoadLocalFallback();
            }
        }

        private Texture2D LoadLocalFallback()
        {
            if (fallbackTexture != null) return fallbackTexture;
            
            var loaded = Resources.Load<Texture2D>(localFallbackPath);
            if (loaded == null)
            {
                // Try direct asset load (only works in editor or if built in)
                #if UNITY_EDITOR
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/{localFallbackPath}.png");
                #endif
            }
            return loaded;
        }

        private async Task<string> RequestGeneration(string key, string prompt)
        {
            var requestBody = new SiliconFlowRequest
            {
                model = modelName,
                prompt = prompt,
                image_size = imageSize,
                batch_size = 1
            };

            string jsonBody = JsonUtility.ToJson(requestBody);
            
            using var webRequest = new UnityWebRequest(SiliconFlowUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {key}");

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"SiliconFlow request failed: {webRequest.error}\n{webRequest.downloadHandler.text}");
            }

            var response = JsonUtility.FromJson<SiliconFlowResponse>(webRequest.downloadHandler.text);
            if (response != null && response.images != null && response.images.Length > 0)
            {
                return response.images[0].url;
            }

            throw new Exception("SiliconFlow response did not contain any images.");
        }

        private async Task<Texture2D> DownloadTexture(string url)
        {
            using var webRequest = UnityWebRequestTexture.GetTexture(url);
            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Failed to download panorama texture: {webRequest.error}");
            }

            return DownloadHandlerTexture.GetContent(webRequest);
        }

        public void ApplySkybox(Texture2D texture)
        {
            if (texture == null) return;

            if (!useSkySphere)
            {
                if (skySphereInstance != null)
                {
                    skySphereInstance.SetActive(false);
                }

                var shader = Shader.Find("Skybox/Panoramic");
                if (shader == null)
                {
                    Debug.LogError("[PanoramaSceneService] Shader 'Skybox/Panoramic' not found.");
                    return;
                }

                var material = new Material(shader);
                material.SetTexture("_MainTex", texture);
                
                RenderSettings.skybox = material;
            }
            else
            {
                RenderSettings.skybox = null; // Disable global skybox

                if (skySphereInstance == null)
                {
                    skySphereInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    skySphereInstance.name = "SceneTalkVR_SkySphere";
                    
                    var col = skySphereInstance.GetComponent<Collider>();
                    if (col != null)
                    {
                        DestroyImmediate(col); // Prevent blocking VR raycasts
                    }

                    InvertMeshNormals(skySphereInstance);
                }

                skySphereInstance.SetActive(true);
                skySphereInstance.transform.position = skySpherePositionOffset;
                skySphereInstance.transform.localScale = Vector3.one * skySphereScale;

                var renderer = skySphereInstance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material matInstance = skySphereMaterial != null 
                        ? new Material(skySphereMaterial) 
                        : new Material(Shader.Find("Unlit/Texture"));
                    
                    matInstance.mainTexture = texture;
                    renderer.sharedMaterial = matInstance;
                }
            }

            DynamicGI.UpdateEnvironment();
            Debug.Log("[PanoramaSceneService] Background applied successfully.");
        }

        private void InvertMeshNormals(GameObject go)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter != null)
            {
                Mesh mesh = filter.mesh;
                Vector3[] normals = mesh.normals;
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = -normals[i];
                }
                mesh.normals = normals;

                for (int m = 0; m < mesh.subMeshCount; m++)
                {
                    int[] triangles = mesh.GetTriangles(m);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int temp = triangles[i + 0];
                        triangles[i + 0] = triangles[i + 1];
                        triangles[i + 1] = temp;
                    }
                    mesh.SetTriangles(triangles, m);
                }
            }
        }

        [Serializable]
        private class SiliconFlowRequest
        {
            public string model;
            public string prompt;
            public string image_size;
            public int batch_size;
        }

        [Serializable]
        private class SiliconFlowResponse
        {
            public SiliconFlowImage[] images;
        }

        [Serializable]
        private class SiliconFlowImage
        {
            public string url;
        }
    }
}
