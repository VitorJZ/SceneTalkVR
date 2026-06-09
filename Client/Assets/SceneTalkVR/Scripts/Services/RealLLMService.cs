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
    /// Real implementation of LLM service using SJTU Local API (OpenAI compatible).
    /// </summary>
    public sealed class RealLLMService : MonoBehaviour, ISceneTalkBrain, ILLMService
    {
        [Header("API Configuration")]
        [SerializeField] private string apiUrl = "https://models.sjtu.edu.cn/api/v1/chat/completions";
        [SerializeField] private string apiKey = "sk-AGepmiQRplsPpiBqfA5uUw"; 
        [SerializeField] private string modelName = "minimax-m2.7";
        
        [Header("Prompts")]
        [TextArea(10, 20)]
        [SerializeField] private string systemPrompt = "You are a VR scene dispatcher and an English tutor. Based on the user's input, generate a JSON response that matches the following structure:\n" +
                                                      "{\n" +
                                                      "  \"taskType\": \"string\",\n" +
                                                      "  \"environmentType\": \"string\",\n" +
                                                      "  \"dialogueReply\": \"string\",\n" +
                                                      "  \"avatarRole\": { \"role\": \"string\", \"speakingSpeed\": \"string\", \"accent\": \"string\", \"attitude\": \"string\" },\n" +
                                                      "  \"scene\": { \"mode\": \"skybox\", \"skyboxUrl\": \"\" }\n" +
                                                      "}\n" +
                                                      "Ensure the output is ONLY the JSON object, no markdown, no conversational filler. " +
                                                      "The 'dialogueReply' should be in character based on the 'environmentType' and 'avatarRole.role'.";

        public IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            Debug.Log($"[RealLLMService] Generating scene and reply for: {userText}");
            
            var task = ParseIntentAsync(userText);
            
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var ex = task.Exception?.InnerException ?? task.Exception;
                onError?.Invoke(ex?.Message ?? "Task faulted during LLM request.");
            }
            else if (task.IsCompletedSuccessfully)
            {
                onComplete?.Invoke(task.Result);
            }
            else
            {
                onError?.Invoke("LLM request was cancelled or failed.");
            }
        }

        #region ILLMService Implementation

        public async Task<SpringScenePayload> ParseIntentAsync(string userInput)
        {
            string responseJson = await SendChatRequest(systemPrompt, userInput, true);
            
            try
            {
                var response = JsonUtility.FromJson<OpenAiResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Length > 0)
                {
                    var content = response.choices[0].message.content;
                    Debug.Log($"[RealLLMService] Intent Parse Result: {content}");
                    
                    content = CleanJsonString(content);
                    return JsonUtility.FromJson<SpringScenePayload>(content);
                }
                throw new Exception("API response structure is invalid or empty.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealLLMService] Parse error: {ex.Message}\nRaw Response: {responseJson}");
                throw;
            }
        }

        public async Task<string> GenerateReplyAsync(string chatHistory)
        {
            string chatSystemPrompt = "You are the character in the scene. Reply naturally to the user's input.";
            string responseJson = await SendChatRequest(chatSystemPrompt, chatHistory, false);

            try
            {
                var response = JsonUtility.FromJson<OpenAiResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Length > 0)
                {
                    return response.choices[0].message.content;
                }
                throw new Exception("API response structure is invalid or empty.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealLLMService] Reply generation error: {ex.Message}");
                throw;
            }
        }

        #endregion

        private async Task<string> SendChatRequest(string sysPrompt, string userPrompt, bool useJsonObject)
        {
            string effectiveKey = string.IsNullOrEmpty(apiKey) 
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                : apiKey;

            if (string.IsNullOrEmpty(effectiveKey))
            {
                throw new Exception("API Key is not set.");
            }

            var requestBody = new OpenAiRequest
            {
                model = modelName,
                messages = new[]
                {
                    new OpenAiMessage { role = "system", content = sysPrompt },
                    new OpenAiMessage { role = "user", content = userPrompt }
                }
            };

            if (useJsonObject)
            {
                requestBody.response_format = new ResponseFormat { type = "json_object" };
            }

            string jsonBody = JsonUtility.ToJson(requestBody);
            
            using var webRequest = new UnityWebRequest(apiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {effectiveKey}");

            var operation = webRequest.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"API Request Failed: {webRequest.error}";
                if (webRequest.downloadHandler != null)
                {
                    errorMsg += $"\n{webRequest.downloadHandler.text}";
                }
                throw new Exception(errorMsg);
            }

            return webRequest.downloadHandler.text;
        }

        private string CleanJsonString(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            
            if (json.StartsWith("```json")) json = json.Substring(7);
            else if (json.StartsWith("```")) json = json.Substring(3);
            
            if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
            
            return json.Trim();
        }

        #region API Data Schemas
        [Serializable]
        private class OpenAiRequest
        {
            public string model;
            public OpenAiMessage[] messages;
            public ResponseFormat response_format;
        }

        [Serializable]
        private class OpenAiMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class ResponseFormat
        {
            public string type;
        }

        [Serializable]
        private class OpenAiResponse
        {
            public OpenAiChoice[] choices;
        }

        [Serializable]
        private class OpenAiChoice
        {
            public OpenAiResponseMessage message;
        }

        [Serializable]
        private class OpenAiResponseMessage
        {
            public string content;
        }
        #endregion
    }
}
