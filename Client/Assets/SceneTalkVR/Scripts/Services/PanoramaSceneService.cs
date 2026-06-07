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
    /// Service for generating 360 panorama scenes using Skybox AI (Blockade Labs).
    /// </summary>
    public sealed class PanoramaSceneService : MonoBehaviour
    {
        private const string BaseUrl = "https://backend.blockadelabs.com/api/v1/skybox";
        
        [Header("API Configuration")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private int checkIntervalMs = 2000;
        [SerializeField] private int timeoutSeconds = 60;

        public async Task<Texture2D> GenerateSkyboxAsync(string environmentDescription)
        {
            string effectiveKey = string.IsNullOrEmpty(apiKey) 
                ? Environment.GetEnvironmentVariable("SKYBOX_API_KEY") 
                : apiKey;

            if (string.IsNullOrEmpty(effectiveKey))
            {
                throw new Exception("Skybox AI API Key is not set.");
            }

            // 1. Request generation
            string skyboxId = await RequestGeneration(effectiveKey, environmentDescription);
            Debug.Log($"[PanoramaSceneService] Skybox generation requested. ID: {skyboxId}");

            // 2. Poll for status
            string imageUrl = await PollForImageUrl(effectiveKey, skyboxId);
            Debug.Log($"[PanoramaSceneService] Skybox ready. URL: {imageUrl}");

            // 3. Download texture
            return await DownloadTexture(imageUrl);
        }

        private async Task<string> RequestGeneration(string key, string prompt)
        {
            WWWForm form = new WWWForm();
            form.AddField("api_key", key);
            form.AddField("prompt", prompt);

            using var webRequest = UnityWebRequest.Post(BaseUrl, form);
            await SendRequestAsync(webRequest);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Skybox request failed: {webRequest.error}\n{webRequest.downloadHandler.text}");
            }

            var response = JsonUtility.FromJson<SkyboxRequestResponse>(webRequest.downloadHandler.text);
            return response.id.ToString();
        }

        private async Task<string> PollForImageUrl(string key, string id)
        {
            string statusUrl = $"{BaseUrl}/status/{id}?api_key={key}";
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                using var webRequest = UnityWebRequest.Get(statusUrl);
                await SendRequestAsync(webRequest);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Skybox status check failed: {webRequest.error}");
                }

                var response = JsonUtility.FromJson<SkyboxStatusResponse>(webRequest.downloadHandler.text);
                
                if (string.Equals(response.status, "complete", StringComparison.OrdinalIgnoreCase))
                {
                    return response.file_url;
                }

                if (string.Equals(response.status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Skybox generation failed on the server.");
                }

                await Task.Delay(checkIntervalMs);
            }

            throw new Exception("Skybox generation timed out.");
        }

        private async Task<Texture2D> DownloadTexture(string url)
        {
            using var webRequest = UnityWebRequestTexture.GetTexture(url);
            await SendRequestAsync(webRequest);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Failed to download skybox texture: {webRequest.error}");
            }

            return DownloadHandlerTexture.GetContent(webRequest);
        }

        private async Task SendRequestAsync(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }

        [Serializable]
        private class SkyboxRequestResponse
        {
            public int id;
        }

        [Serializable]
        private class SkyboxStatusResponse
        {
            public string status;
            public string file_url;
        }
    

        public void ApplySkybox(Texture2D texture)
        {
            if (texture == null) return;

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogError("[PanoramaSceneService] Shader 'Skybox/Panoramic' not found.");
                return;
            }

            var material = new Material(shader);
            material.SetTexture("_MainTex", texture);
            
            RenderSettings.skybox = material;
            DynamicGI.UpdateEnvironment();
            
            Debug.Log("[PanoramaSceneService] Skybox updated successfully.");
        }
}
}
