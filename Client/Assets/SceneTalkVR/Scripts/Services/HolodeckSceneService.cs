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
        [SerializeField] private string backendUrl = "http://localhost:8080/generate_scene";
        [SerializeField] private int timeoutSeconds = 120; // Holodeck can be slow

        public async Task<HolodeckResponse> GenerateLayoutAsync(string environmentDescription)
        {
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
                throw new Exception($"Holodeck request failed: {webRequest.error}\n{webRequest.downloadHandler?.text}");
            }

            string responseJson = webRequest.downloadHandler.text;
            Debug.Log($"[HolodeckSceneService] Layout received: {responseJson}");

            return JsonUtility.FromJson<HolodeckResponse>(responseJson);
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
