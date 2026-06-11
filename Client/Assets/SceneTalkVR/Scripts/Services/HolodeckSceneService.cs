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
    /// Service for consuming 3D scene layout data from the Holodeck Python backend.
    /// </summary>
    public sealed class HolodeckSceneService : MonoBehaviour
    {
        [Header("Backend Configuration")]
        [Tooltip("If disabled, or if the backend is unreachable, the system will use a hardcoded fallback layout.")]
        [SerializeField] private bool useLocalBackend = true;
        [SerializeField] private string backendUrl = "http://localhost:8080/generate_scene";
        [SerializeField] private int timeoutSeconds = 300; // Holodeck can be very slow on first run

        public async Task<HolodeckResponse> GenerateLayoutAsync(string environmentDescription)
        {
            if (!useLocalBackend)
            {
                Debug.Log($"[HolodeckSceneService] Local backend disabled. Using mock layout for: {environmentDescription}");
                return GetMockResponse(environmentDescription);
            }

            Debug.Log($"[HolodeckSceneService] Requesting 3D layout for: {environmentDescription}");

            var requestBody = new HolodeckRequest { environment = environmentDescription };
            string jsonPayload = JsonUtility.ToJson(requestBody);

            using var webRequest = new UnityWebRequest(backendUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = timeoutSeconds;

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HolodeckSceneService] Holodeck request failed ({webRequest.error}). Falling back to mock data.");
                return GetMockResponse(environmentDescription);
            }

            string responseJson = webRequest.downloadHandler.text;
            Debug.Log($"[HolodeckSceneService] Layout received: {responseJson}");

            return JsonUtility.FromJson<HolodeckResponse>(responseJson);
        }

        private HolodeckResponse GetMockResponse(string environmentDescription)
        {
            return new HolodeckResponse
            {
                environment = environmentDescription,
                objects = new[]
                {
                    new HolodeckObject { name = "coffee_counter", position = new float[] { 0f, 0f, 2.5f }, rotation = 0 },
                    new HolodeckObject { name = "cafe_table", position = new float[] { 1.5f, 0f, 1.2f }, rotation = 45 },
                    new HolodeckObject { name = "chair", position = new float[] { 2.0f, 0f, 1.2f }, rotation = -90 },
                    new HolodeckObject { name = "plant", position = new float[] { -1.5f, 0f, 2.5f }, rotation = 0 }
                }
            };
        }

        [Serializable]
        public class HolodeckRequest
        {
            public string environment;
        }

        [Serializable]
        public class HolodeckResponse
        {
            public string environment;
            public HolodeckObject[] objects;
        }

        [Serializable]
        public class HolodeckObject
        {
            public string name;
            public float[] position; // [x, y, z]
            public float rotation;   // Y-axis rotation
        }
    }
}
