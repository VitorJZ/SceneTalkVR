using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class RealLLMService : MonoBehaviour, ISceneTalkBrain, ILLMService, ISceneTalkSessionReset, ISceneTalkExperimentContextReceiver
    {
        [Header("API Configuration")]
        [SerializeField] private string apiUrl = "https://models.sjtu.edu.cn/api/v1/chat/completions";
        [SerializeField] private string apiKey = ""; 
        [SerializeField] private string modelName = "minimax-m2.7";
        
        [Header("Prompts")]
        [TextArea(10, 20)]
        [SerializeField] private string systemPrompt = "You are a VR scene dispatcher and an English tutor. Based on the user's input, generate a JSON response that matches the following structure:\n" +
                                                      "{\n" +
                                                      "  \"taskType\": \"string\",\n" +
                                                      "  \"environmentType\": \"string\",\n" +
                                                      "  \"dialogueReply\": \"string\",\n" +
                                                      "  \"avatarRole\": {\n" +
                                                      "    \"role\": \"string\",\n" +
                                                      "    \"speakingSpeed\": \"string\",\n" +
                                                      "    \"accent\": \"string\",\n" +
                                                      "    \"attitude\": \"string\",\n" +
                                                      "    \"appearance\": {\n" +
                                                      "      \"styleId\": \"semi_realistic_v1\",\n" +
                                                      "      \"genderPresentation\": \"male|female|unknown\",\n" +
                                                      "      \"ageBucket\": \"young_adult|adult|unknown\",\n" +
                                                      "      \"bodyBuild\": \"average|unknown\",\n" +
                                                      "      \"outfitRole\": \"barista|teacher|police|unknown\",\n" +
                                                      "      \"outfitColor\": \"string\"\n" +
                                                      "    }\n" +
                                                      "  },\n" +
                                                      "  \"scene\": { \"mode\": \"skybox\", \"skyboxUrl\": \"\" }\n" +
                                                      "}\n" +
                                                      "Ensure the output is ONLY the JSON object, no markdown, no conversational filler. " +
                                                      "Normalize avatarRole.role to barista, teacher, or police when the request matches a waiter/service worker, teacher, or police/security officer. " +
                                                      "Respect explicit gender requests such as male teacher, female teacher, male waiter, female waiter, male police, or female police by setting avatarRole.appearance.genderPresentation to male or female; otherwise use unknown. " +
                                                      "The 'dialogueReply' should be in character based on the 'environmentType' and 'avatarRole.role'.";

        private readonly List<OpenAiMessage> chatHistory = new List<OpenAiMessage>();
        private SceneTalkOrchestrator cachedOrchestrator;
        private CorrectionExperimentCondition currentCondition;

        public void SetExperimentCondition(CorrectionExperimentCondition condition)
        {
            currentCondition = ExperimentConditionManager.CloneCondition(condition);
        }

        public IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            Debug.Log($"[RealLLMService] Generating scene and reply for: {userText}");
            
            CheckAndResetSession();

            Task<SpringScenePayload> task;
            if (chatHistory.Count == 0)
            {
                task = ParseIntentAsync(userText);
            }
            else
            {
                task = GenerateDialogueTurnAsync(userText);
            }
            
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
            string responseJson = await SendChatRequest(BuildSceneSystemPrompt(), userInput, true);
            
            try
            {
                var response = JsonUtility.FromJson<OpenAiResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Length > 0)
                {
                    var content = response.choices[0].message.content;
                    Debug.Log($"[RealLLMService] Intent Parse Result: {content}");
                    
                    content = CleanJsonString(content);
                    var payload = JsonUtility.FromJson<SpringScenePayload>(content);

                    EnsureDialogueReplyPresent(payload);
                    ApplyExperimentConditionToPayload(payload);
                    chatHistory.Clear();
                    string rpSysPrompt = BuildRoleplaySystemPrompt(payload);
                    chatHistory.Add(new OpenAiMessage { role = "system", content = rpSysPrompt });
                    chatHistory.Add(new OpenAiMessage { role = "user", content = userInput });
                    chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });

                    return payload;
                }
                throw new Exception("API response structure is invalid or empty.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealLLMService] Parse error: {ex.Message}\nRaw Response: {responseJson}");
                throw;
            }
        }

        public async Task<string> GenerateReplyAsync(string chatHistoryJson)
        {
            string chatSystemPrompt = "You are the character in the scene. Reply naturally to the user's input.";
            string responseJson = await SendChatRequest(chatSystemPrompt, chatHistoryJson, false);

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

        #region Dialogue Multi-Turn Helpers

        private async Task<SpringScenePayload> GenerateDialogueTurnAsync(string userInput)
        {
            chatHistory.Add(new OpenAiMessage { role = "user", content = userInput });

            string responseJson = await SendChatRequest(chatHistory.ToArray(), true);

            try
            {
                var response = JsonUtility.FromJson<OpenAiResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Length > 0)
                {
                    var content = response.choices[0].message.content;
                    
                    // Clean content by stripping <think>...</think> reasoning blocks
                    content = CleanJsonString(content);
                    
                    Debug.Log($"[RealLLMService] Dialogue Turn Reply: {content}");

                    var payload = TryParseDialoguePayload(content);
                    ApplyExperimentConditionToPayload(payload);
                    chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });

                    return payload;
                }
                throw new Exception("API response structure is invalid or empty.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealLLMService] Dialogue turn error: {ex.Message}\nRaw Response: {responseJson}");
                throw;
            }
        }

        private string BuildSceneSystemPrompt()
        {
            return systemPrompt + "\n\n" + BuildExperimentPromptInstructions(true);
        }

        private SpringScenePayload TryParseDialoguePayload(string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var payload = JsonUtility.FromJson<SpringScenePayload>(content);
                    if (payload != null)
                    {
                        EnsureDialogueReplyPresent(payload);
                        EnsurePayloadDefaults(payload);
                        return payload;
                    }
                }
                catch (FormatException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RealLLMService] Dialogue JSON parse fallback: {ex.Message}");
                }
            }

            return new SpringScenePayload
            {
                dialogueReply = content,
                taskType = currentCondition == null ? string.Empty : currentCondition.scenarioId,
                environmentType = currentCondition?.task == null
                    ? string.Empty
                    : currentCondition.task.fallbackEnvironmentType,
                avatarRole = new AvatarRoleData(),
                scene = new ScenePayload(),
                correctionFeedback = currentCondition == null
                    ? null
                    : new CorrectionFeedbackData
                    {
                        hasFeedback = false,
                        provider = currentCondition.provider,
                        style = currentCondition.style
                    }
            };
        }

        private static void EnsureDialogueReplyPresent(SpringScenePayload payload)
        {
            if (payload == null)
            {
                throw new FormatException("LLM response did not contain a valid SceneTalk payload.");
            }

            if (string.IsNullOrWhiteSpace(payload.dialogueReply))
            {
                throw new FormatException("LLM payload is missing the required dialogueReply.");
            }
        }

        private string BuildRoleplaySystemPrompt(SpringScenePayload initialPayload)
        {
            string role = initialPayload.avatarRole?.role ?? "tutor";
            string speed = initialPayload.avatarRole?.speakingSpeed ?? "medium";
            string accent = initialPayload.avatarRole?.accent ?? "american";
            string attitude = initialPayload.avatarRole?.attitude ?? "friendly";
            string env = initialPayload.environmentType ?? "classroom";

            return $"You are playing the role of a {role} in a {env} environment for English oral practice. " +
                   $"Your accent is {accent}, your attitude is {attitude}, and you should speak at a {speed} speed. " +
                   $"Reply to the user's statements naturally and concisely (1-3 sentences). Keep the practice interactive and realistic.\n\n" +
                   BuildExperimentPromptInstructions(false);
        }

        private string BuildExperimentPromptInstructions(bool includeScenePayload)
        {
            if (currentCondition == null)
            {
                return includeScenePayload
                    ? "When relevant, include a correctionFeedback object with hasFeedback=false if there is no language error."
                    : "Return ONLY a JSON object with dialogueReply and correctionFeedback.";
            }

            var task = currentCondition.task;
            var goals = task == null || task.goals == null || task.goals.Length == 0
                ? string.Empty
                : string.Join("; ", task.goals);

            var builder = new StringBuilder();
            builder.AppendLine("Experiment condition is fixed by the client. Do not change it.");
            builder.AppendLine($"scenarioId: {currentCondition.scenarioId}");
            builder.AppendLine($"feedback provider: {currentCondition.provider}");
            builder.AppendLine($"feedback style: {currentCondition.style}");
            if (task != null)
            {
                if (!string.IsNullOrWhiteSpace(task.context))
                {
                    builder.AppendLine($"task context: {task.context}");
                }

                if (!string.IsNullOrWhiteSpace(goals))
                {
                    builder.AppendLine($"task goals: {goals}");
                }

                if (!string.IsNullOrWhiteSpace(task.initialQuestion))
                {
                    builder.AppendLine($"opening question: {task.initialQuestion}");
                }
            }

            if (includeScenePayload)
            {
                builder.AppendLine("Return the normal scene JSON plus a correctionFeedback object.");
            }
            else
            {
                builder.AppendLine("Return ONLY JSON with dialogueReply and correctionFeedback. Do not return plain text.");
            }

            builder.AppendLine("correctionFeedback must contain hasFeedback, provider, style, errorType, originalText, correctedText, feedbackText, targetSpan, confidence.");
            builder.AppendLine("If the learner made no clear grammar, vocabulary, naturalness, or incomplete-sentence error, set hasFeedback=false and keep provider/style fixed.");
            builder.AppendLine("For explicit style, feedbackText should briefly point out the correction and correctedText should be the corrected expression.");
            builder.AppendLine("For recast style, feedbackText should be a natural conversational reformulation, not a teacher-like explanation.");
            return builder.ToString();
        }

        private void ApplyExperimentConditionToPayload(SpringScenePayload payload)
        {
            if (payload == null || currentCondition == null)
            {
                return;
            }

            EnsurePayloadDefaults(payload);

            if (string.IsNullOrWhiteSpace(payload.taskType))
            {
                payload.taskType = currentCondition.scenarioId;
            }

            if (string.IsNullOrWhiteSpace(payload.environmentType) && currentCondition.task != null)
            {
                payload.environmentType = currentCondition.task.fallbackEnvironmentType;
            }

            if (payload.correctionFeedback == null)
            {
                payload.correctionFeedback = new CorrectionFeedbackData
                {
                    hasFeedback = false
                };
            }

            payload.correctionFeedback.provider = currentCondition.provider;
            payload.correctionFeedback.style = currentCondition.style;
        }

        private static void EnsurePayloadDefaults(SpringScenePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            payload.avatarRole ??= new AvatarRoleData();
            payload.scene ??= new ScenePayload();
        }

        private void CheckAndResetSession()
        {
            if (cachedOrchestrator == null)
            {
                cachedOrchestrator = FindObjectOfType<SceneTalkOrchestrator>();
            }

            if (cachedOrchestrator != null)
            {
                var state = cachedOrchestrator.CurrentState;
                if (state == SceneTalkState.Idle || state == SceneTalkState.Finished)
                {
                    if (chatHistory.Count > 0)
                    {
                        Debug.Log("[RealLLMService] Orchestrator is Idle/Finished. Clearing chat history.");
                        chatHistory.Clear();
                    }
                }
            }
        }

        public void ResetSession()
        {
            if (chatHistory != null)
            {
                chatHistory.Clear();
                Debug.Log("[RealLLMService] Chat history cleared on explicit session reset.");
            }
        }

        #endregion

        #region Send API Requests

        private async Task<string> SendChatRequest(OpenAiMessage[] messages, bool useJsonObject)
        {
            string effectiveKey = string.IsNullOrEmpty(apiKey) 
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                : apiKey;

            if (string.IsNullOrEmpty(effectiveKey))
            {
                throw new Exception("API Key is not set.");
            }

            string jsonBody;
            if (useJsonObject)
            {
                var requestBody = new OpenAiRequest
                {
                    model = modelName,
                    messages = messages,
                    response_format = new ResponseFormat { type = "json_object" }
                };
                jsonBody = JsonUtility.ToJson(requestBody);
            }
            else
            {
                var requestBody = new OpenAiTextRequest
                {
                    model = modelName,
                    messages = messages
                };
                jsonBody = JsonUtility.ToJson(requestBody);
            }
            
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

        private async Task<string> SendChatRequest(string sysPrompt, string userPrompt, bool useJsonObject)
        {
            var messages = new[]
            {
                new OpenAiMessage { role = "system", content = sysPrompt },
                new OpenAiMessage { role = "user", content = userPrompt }
            };
            return await SendChatRequest(messages, useJsonObject);
        }

        #endregion

        private string CleanJsonString(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            // Strip <think>...</think> reasoning blocks if present
            int thinkEndIdx = json.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            if (thinkEndIdx >= 0)
            {
                json = json.Substring(thinkEndIdx + 8);
            }
            
            json = json.Trim();
            
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
        private class OpenAiTextRequest
        {
            public string model;
            public OpenAiMessage[] messages;
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
