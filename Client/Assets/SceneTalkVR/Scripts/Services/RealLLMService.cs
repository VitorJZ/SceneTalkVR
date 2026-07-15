using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneTalkVR.Runtime.Services
{
    /// <summary>
    /// Real implementation of LLM service using SJTU Local API (OpenAI compatible).
    /// </summary>
    public sealed class RealLLMService : MonoBehaviour, ISceneTalkBrain, ISceneTalkStreamingBrain, ILLMService, ISceneTalkSessionReset, ISceneTalkExperimentContextReceiver, ISceneTalkExperimentLockReceiver
    {
        [Header("API Configuration")]
        [SerializeField] private string apiUrl = "https://models.sjtu.edu.cn/api/v1/chat/completions";
        [SerializeField] private string apiKey = ""; 
        [SerializeField] private string modelName = "deepseek-chat";
        
        [Header("Feedback Strategy")]
        [Tooltip("Feedback strictness: conservative (only severe errors), moderate (general errors), active (almost all errors)")]
        [SerializeField] private string feedbackSensitivity = "moderate"; // conservative | moderate | active

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
                                                      "  \"scene\": { \"mode\": \"skybox\", \"skyboxUrl\": \"\" },\n" +
                                                      "  \"correctionFeedback\": {\n" +
                                                      "    \"hasFeedback\": false,\n" +
                                                      "    \"provider\": \"dialogue_avatar|assistant_agent\",\n" +
                                                      "    \"style\": \"explicit|recast\",\n" +
                                                      "    \"errorType\": \"grammar|unnatural|vocabulary|incomplete|unknown\",\n" +
                                                      "    \"originalText\": \"string\",\n" +
                                                      "    \"correctedText\": \"string\",\n" +
                                                      "    \"feedbackText\": \"string\",\n" +
                                                      "    \"targetSpan\": \"string\",\n" +
                                                      "    \"confidence\": 1.0\n" +
                                                      "  }\n" +
                                                      "}\n" +
                                                      "Ensure the output is ONLY the JSON object, no markdown, no conversational filler. " +
                                                      "Normalize avatarRole.role to barista, teacher, or police when the request matches a waiter/service worker, teacher, or police/security officer. " +
                                                      "Respect explicit gender requests such as male teacher, female teacher, male waiter, female waiter, male police, or female police by setting avatarRole.appearance.genderPresentation to male or female; otherwise use unknown. " +
                                                      "The 'dialogueReply' should be in character based on the 'environmentType' and 'avatarRole.role'.";

        private readonly List<OpenAiMessage> chatHistory = new List<OpenAiMessage>();
        private readonly List<string> sessionErrorHistory = new List<string>();
        private SceneTalkOrchestrator cachedOrchestrator;
        private CorrectionExperimentCondition currentCondition;
        public CorrectionExperimentCondition CurrentCondition => currentCondition;

        private float lastSttConfidence = 1.0f;
        private float lastRecordingDurationMs = 0f;
        private string lastRecordingStopReason = "unknown";

        public void ConfigureApi(string runtimeApiUrl, string runtimeModelName)
        {
            if (!string.IsNullOrWhiteSpace(runtimeApiUrl))
            {
                apiUrl = runtimeApiUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(runtimeModelName))
            {
                modelName = runtimeModelName.Trim();
            }
        }

        public string GetSessionErrorSummary()
        {
            if (sessionErrorHistory.Count == 0) return "No errors detected in this session.";
            var counts = new Dictionary<string, int>();
            foreach (var err in sessionErrorHistory)
            {
                if (string.IsNullOrEmpty(err)) continue;
                counts[err] = counts.TryGetValue(err, out int c) ? c + 1 : 1;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Total feedback triggered: {sessionErrorHistory.Count}");
            foreach (var kvp in counts)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value} times");
            }
            return sb.ToString();
        }

        public void SetExperimentCondition(CorrectionExperimentCondition condition)
        {
            currentCondition = ExperimentConditionManager.CloneCondition(condition);
        }

        public IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            Debug.Log($"[RealLLMService] Generating scene and reply for: {userText}");
            
            // Retrieve latest STT metadata from GatewaySpeechInputModule if available (skip if running in test runner)
            bool isTestRunner = currentCondition != null && currentCondition.participantId == "test_runner";
            if (!isTestRunner)
            {
                lastSttConfidence = 1.0f;
                lastRecordingDurationMs = 0f;
                lastRecordingStopReason = "unknown";
                
                var speechModule = FindObjectOfType<GatewaySpeechInputModule>();
                if (speechModule != null)
                {
                    lastRecordingDurationMs = speechModule.LastRecordingDurationMs;
                    lastRecordingStopReason = speechModule.LastRecordingStopReason;
                    if (speechModule.LastSttResponse != null)
                    {
                        lastSttConfidence = speechModule.LastSttResponse.confidence;
                    }
                    Debug.Log($"[RealLLMService] STT Metadata - Duration: {lastRecordingDurationMs}ms, StopReason: {lastRecordingStopReason}, Confidence: {lastSttConfidence}");
                }
            }

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
            if (ShouldSuppressCorrectionByStt(out var suppressionReason))
            {
                var payload = new SpringScenePayload
                {
                    dialogueReply = "Sorry, I didn't catch that clearly. Could you say it again?",
                    taskType = currentCondition == null ? string.Empty : currentCondition.scenarioId,
                    environmentType = currentCondition?.task == null ? string.Empty : currentCondition.task.fallbackEnvironmentType,
                    avatarRole = new AvatarRoleData(),
                    scene = new ScenePayload(),
                    correctionFeedback = new CorrectionFeedbackData
                    {
                        hasFeedback = false,
                        provider = currentCondition?.provider ?? "dialogue_avatar",
                        style = currentCondition?.style ?? "explicit",
                        errorType = "none",
                        originalText = "",
                        correctedText = "",
                        feedbackText = "",
                        targetSpan = "",
                        confidence = 1f,
                        rationaleTag = suppressionReason
                    }
                };
                return payload;
            }

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
            if (ShouldSuppressCorrectionByStt(out var suppressionReason))
            {
                var payload = new SpringScenePayload
                {
                    dialogueReply = "Sorry, I didn't catch that clearly. Could you say it again?",
                    taskType = currentCondition == null ? string.Empty : currentCondition.scenarioId,
                    environmentType = currentCondition?.task == null ? string.Empty : currentCondition.task.fallbackEnvironmentType,
                    avatarRole = new AvatarRoleData(),
                    scene = new ScenePayload(),
                    correctionFeedback = new CorrectionFeedbackData
                    {
                        hasFeedback = false,
                        provider = currentCondition?.provider ?? "dialogue_avatar",
                        style = currentCondition?.style ?? "explicit",
                        errorType = "none",
                        originalText = "",
                        correctedText = "",
                        feedbackText = "",
                        targetSpan = "",
                        confidence = 1f,
                        rationaleTag = suppressionReason
                    }
                };
                return payload;
            }

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
            var builder = new StringBuilder();
            
            if (currentCondition == null)
            {
                builder.AppendLine("When analyzing the user's speech, you must also detect language errors and include a correctionFeedback object in your JSON response.");
                builder.AppendLine("JSON structure for correctionFeedback:");
                builder.AppendLine("  \"correctionFeedback\": {");
                builder.AppendLine("    \"hasFeedback\": false,");
                builder.AppendLine("    \"provider\": \"dialogue_avatar\",");
                builder.AppendLine("    \"style\": \"explicit\",");
                builder.AppendLine("    \"errorType\": \"no_feedback\",");
                builder.AppendLine("    \"originalText\": \"\",");
                builder.AppendLine("    \"correctedText\": \"\",");
                builder.AppendLine("    \"feedbackText\": \"\",");
                builder.AppendLine("    \"targetSpan\": \"\",");
                builder.AppendLine("    \"confidence\": 1.0");
                builder.AppendLine("  }");
                builder.AppendLine("If no clear error, set hasFeedback=false.");
                return builder.ToString();
            }

            var task = currentCondition.task;
            var goals = task == null || task.goals == null || task.goals.Length == 0
                ? string.Empty
                : string.Join("; ", task.goals);

            bool locked = IsExperimentLocked();
            string effectiveSensitivity = locked ? "moderate" : feedbackSensitivity;
            string historyCsv = (!locked && sessionErrorHistory.Count > 0) ? string.Join(", ", sessionErrorHistory) : "none";

            builder.AppendLine("=== EXPERIMENT & TASK CONTEXT ===");
            builder.AppendLine("The experiment condition is FIXED by the client. Do NOT change provider or style in the JSON output.");
            builder.AppendLine($"- scenarioId: {currentCondition.scenarioId}");
            builder.AppendLine($"- feedbackProvider: {currentCondition.provider} (dialogue_avatar means you, the roleplay character; assistant_agent means a separate AI assistant helper)");
            builder.AppendLine($"- feedbackStyle: {currentCondition.style} (explicit means direct correction; recast means natural conversational reformulation)");
            builder.AppendLine($"- feedbackSensitivity: {effectiveSensitivity} (conservative means correct only severe errors that block understanding; moderate means correct clear grammar/vocab errors; active means correct even minor unnatural expressions/repetitions)");
            if (!locked)
            {
                builder.AppendLine($"- correctedErrorsInSession: [{historyCsv}] (types of errors already corrected in this session)");
            }
            
            if (task != null)
            {
                if (!string.IsNullOrWhiteSpace(task.context))
                {
                    builder.AppendLine($"- taskContext: {task.context}");
                }
                if (!string.IsNullOrWhiteSpace(goals))
                {
                    builder.AppendLine($"- taskGoals: {goals}");
                }
                if (!string.IsNullOrWhiteSpace(task.initialQuestion))
                {
                    builder.AppendLine($"- openingQuestion: {task.initialQuestion}");
                }
            }

            builder.AppendLine("\n=== SCENARIO-SPECIFIC GUIDANCE ===");
            switch (currentCondition.scenarioId)
            {
                case "restaurant_reservation":
                    builder.AppendLine("Acceptable short task phrases: 'Table for two, please.', 'For tomorrow at seven.', 'Do you have a table by the window?'. Do NOT over-correct these. Only correct clear grammar/vocab errors (e.g., missing articles like 'have table by window' -> 'have a table by the window', missing auxiliaries like 'I want reserve a table' -> 'I'd like to reserve a table') or impolite/abrupt phrasing.");
                    builder.AppendLine("If feedbackProvider is assistant_agent: Your dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.");
                    break;
                case "furniture_shopping":
                    builder.AppendLine("Acceptable short task phrases: 'I'm looking for a wooden desk.', 'Do you deliver?', 'How much is this chair?'. Only correct clear errors like adverb-verb order (e.g. 'I very like this desk' -> 'I really like this desk'), wrong size/material vocabulary, or incorrect structures like 'I want make my room fitting' -> 'I want it to fit my room'.");
                    builder.AppendLine("If feedbackProvider is assistant_agent: Your dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.");
                    break;
                case "gym_membership":
                    builder.AppendLine("Acceptable short task phrases: 'Do you have a monthly plan?', 'Is there a swimming pool?', 'Can I try one class?'. Only correct clear errors like question structure (e.g. 'How much cost the plan?' -> 'How much does the plan cost?') or verb patterns (e.g. 'I want make muscle' -> 'I want to build muscle').");
                    builder.AppendLine("If feedbackProvider is assistant_agent: Your dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.");
                    break;
                case "hotel_check_in":
                    builder.AppendLine("Acceptable short task phrases: 'I have a reservation under Johnson.', 'When is check-out?', 'Could I get a quiet room?'. Only correct clear errors like missing articles (e.g. 'I have reservation' -> 'I have a reservation'), time questions (e.g. 'What time I must leave?' -> 'What time do I need to check out?'), or abrupt demands (e.g. 'Give me key' -> 'Could I get my key, please?').");
                    builder.AppendLine("If feedbackProvider is assistant_agent: Your dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.");
                    break;
            }

            builder.AppendLine("\n=== SPEECH CAPTURE METADATA ===");
            builder.AppendLine($"- recordingDurationMs: {lastRecordingDurationMs} ms");
            builder.AppendLine($"- recordingStopReason: {lastRecordingStopReason}");
            builder.AppendLine($"- sttConfidence: {lastSttConfidence}");
            if (lastSttConfidence < 0.5f)
            {
                builder.AppendLine("CRITICAL: STT/ASR confidence is extremely low. Do NOT perform any grammar correction (set hasFeedback = false) because the errors are likely STT recognition failures. Respond politely asking the user to repeat.");
            }
            if (lastRecordingDurationMs > 0 && lastRecordingDurationMs < 500f)
            {
                builder.AppendLine("CRITICAL: The user recording was too short (under 500ms), probably a misclick or accidental cancel. Do NOT perform grammar correction (set hasFeedback = false). Respond politely asking the user to repeat.");
            }

            builder.AppendLine("\n=== LANGUAGE CORRECTION INSTRUCTIONS ===");
            builder.AppendLine("1. Detect at most ONE major error per turn (grammar, unnatural expression, vocabulary misuse, or incomplete sentence).");
            builder.AppendLine("2. Respect the 'feedbackSensitivity' level:");
            builder.AppendLine("   - If 'conservative': Only correct severe grammar/vocab errors that clearly hinder understanding. Ignore minor unnaturalness.");
            builder.AppendLine("   - If 'moderate': Correct clear grammar (including missing articles like 'a'/'the'), unnatural expressions, and vocabulary misuse. Ignore minor self-corrections or normal pauses. Do NOT skip missing articles even when provider is dialogue_avatar and style is explicit.");
            builder.AppendLine("   - If 'active': Be highly strict. Correct even minor slips, awkward phrasing, and slang/informal style.");
            if (!locked)
            {
                builder.AppendLine("3. Manage repetitive errors: Consult 'correctedErrorsInSession'. If the same errorType was corrected recently, try to be more tolerant (set hasFeedback=false) or prefer the softer 'recast' feedbackStyle to avoid annoying the user.");
                builder.AppendLine("4. If no error is detected or it is skipped based on sensitivity/history, set hasFeedback = false and leave originalText/correctedText/feedbackText/targetSpan empty.");
            }
            else
            {
                builder.AppendLine("3. If no error is detected or it is skipped based on sensitivity, set hasFeedback = false and leave originalText/correctedText/feedbackText/targetSpan empty.");
            }
            builder.AppendLine("4. If feedbackProvider = assistant_agent: Your dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English. Do NOT say: 'you should say', 'a better way', 'correct', 'wrong', 'mistake', 'grammar', 'instead', 'try saying'.");
            builder.AppendLine("5. Customize feedbackText based on feedbackStyle and feedbackProvider:");
            builder.AppendLine("   - If style is 'explicit':");
            builder.AppendLine("     * If provider is 'dialogue_avatar': Keep it brief and character-appropriate. Example: 'You can say: I really like this topic.'");
            builder.AppendLine("     * If provider is 'assistant_agent': Act as an instructor helper. Example: 'Grammar tip: Remember to say: I really like this topic, not I very like this topic.'");
            builder.AppendLine("   - If style is 'recast':");
            builder.AppendLine("     * Never use direct correction words like 'say', 'correct', 'instead', or 'not'. Formulate a natural conversational reformulation.");
            builder.AppendLine("     * If provider is 'dialogue_avatar': The feedbackText should sound like the character natural confirmation or continuation of the talk. Example: 'Oh, you really like this topic?'");
            builder.AppendLine("     * If provider is 'assistant_agent': The feedbackText should be a helpful recast hint. Example: 'You mean you really like this topic?'");
            builder.AppendLine("6. Limit feedbackText to 1 or 2 short sentences suitable for spoken TTS in VR.");

            builder.AppendLine("\n=== JSON OUTPUT FORMAT ===");
            if (includeScenePayload)
            {
                builder.AppendLine("Return a complete JSON containing taskType, environmentType, dialogueReply, avatarRole, scene, and correctionFeedback.");
            }
            else
            {
                builder.AppendLine("Return ONLY a JSON object with: dialogueReply (string) and correctionFeedback (object). Do not include scene, avatarRole, etc. Do not include markdown code block syntax.");
            }

            builder.AppendLine("Ensure the JSON has the exact schema for correctionFeedback:");
            builder.AppendLine("{");
            builder.AppendLine("  \"dialogueReply\": \"character's reply text\",");
            builder.AppendLine("  \"correctionFeedback\": {");
            builder.AppendLine("    \"hasFeedback\": true/false,");
            builder.AppendLine("    \"provider\": \"dialogue_avatar|assistant_agent\" (MATCH input provider exactly),");
            builder.AppendLine("    \"style\": \"explicit|recast\" (MATCH input style exactly),");
            builder.AppendLine("    \"errorType\": \"grammar|unnatural|vocabulary|incomplete|unknown\",");
            builder.AppendLine("    \"originalText\": \"user's incorrect sentence\",");
            builder.AppendLine("    \"correctedText\": \"the corrected sentence\",");
            builder.AppendLine("    \"feedbackText\": \"the feedback text to be spoken via TTS\",");
            builder.AppendLine("    \"targetSpan\": \"the specific wrong phrase or word that was corrected\",");
            builder.AppendLine("    \"confidence\": 0.0-1.0,");
            builder.AppendLine("    \"rationaleTag\": \"short internal tag explaining the correction choice (e.g., subject_verb_agreement, active_sensitivity_filter, repeated_error_skipped, ok)\"");
            builder.AppendLine("  }");
            builder.AppendLine("}");

            return builder.ToString();
        }

        private static readonly System.Collections.Generic.HashSet<string> ValidErrorTypes = new System.Collections.Generic.HashSet<string>
        {
            "grammar",
            "unnatural",
            "vocabulary",
            "incomplete",
            "none",
            "unknown"
        };

        private bool isLocked;

        public void SetExperimentLocked(bool locked)
        {
            isLocked = locked;
        }

        private bool IsExperimentLocked()
        {
            return isLocked || (currentCondition != null && currentCondition.formalExperiment);
        }

        private bool ShouldSuppressCorrectionByStt(out string sttSuppressionReason)
        {
            sttSuppressionReason = string.Empty;

            if (lastRecordingDurationMs > 0 && lastRecordingDurationMs < 500)
            {
                sttSuppressionReason = "short_recording_suppressed";
                return true;
            }

            if (lastSttConfidence >= 0 && lastSttConfidence < 0.5f)
            {
                sttSuppressionReason = "low_confidence_suppressed";
                return true;
            }

            return false;
        }

        private string BuildSafeTaskContinuation(SpringScenePayload payload)
        {
            var scenario = currentCondition?.scenarioId ?? payload.taskType ?? string.Empty;
            switch (scenario)
            {
                case "restaurant_reservation":
                    return "Sure. What time would you like to come in?";
                case "furniture_shopping":
                    return "Got it. What size or style are you looking for?";
                case "gym_membership":
                    return "Okay. Are you interested in a monthly plan or a trial visit?";
                case "hotel_check_in":
                    return "Thank you. May I confirm the name on your reservation?";
                default:
                    return "I see. Could you tell me a little more?";
            }
        }

        private string BuildMinimalRecast(string correctedText)
        {
            if (!string.IsNullOrWhiteSpace(correctedText))
            {
                return correctedText.Trim().TrimEnd('.') + ".";
            }
            return string.Empty;
        }

        private static string AppendRationale(string current, string next)
        {
            if (string.IsNullOrWhiteSpace(current)) return next;
            if (current.Contains(next)) return current;
            return $"{current};{next}";
        }

        private void ApplyExperimentConditionToPayload(SpringScenePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            EnsurePayloadDefaults(payload);

            var feedback = payload.correctionFeedback;
            if (feedback == null)
            {
                feedback = new CorrectionFeedbackData
                {
                    hasFeedback = false
                };
                payload.correctionFeedback = feedback;
            }

            // 1. Enum Validation & Normalization
            if (!feedback.hasFeedback)
            {
                feedback.errorType = "none";
                feedback.originalText = "";
                feedback.correctedText = "";
                feedback.feedbackText = "";
                feedback.targetSpan = "";
            }
            else
            {
                if (string.IsNullOrEmpty(feedback.errorType) || !ValidErrorTypes.Contains(feedback.errorType.ToLowerInvariant()))
                {
                    feedback.errorType = "unknown";
                    feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "invalid_error_type_repaired");
                }
            }

            // Log detected error type to session history for post-analysis and repetitive error management
            if (feedback.hasFeedback && !string.IsNullOrEmpty(feedback.errorType) && feedback.errorType != "none")
            {
                sessionErrorHistory.Add(feedback.errorType);
                Debug.Log($"[RealLLMService] Recorded session error: {feedback.errorType}. Total count: {sessionErrorHistory.Count}");
            }

            if (currentCondition == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.taskType))
            {
                payload.taskType = currentCondition.scenarioId;
            }

            if (string.IsNullOrWhiteSpace(payload.environmentType) && currentCondition.task != null)
            {
                payload.environmentType = currentCondition.task.fallbackEnvironmentType;
            }

            // Force override provider and style to match the experiment condition exactly
            feedback.provider = currentCondition.provider == "assistant_agent" ? "assistant_agent" : "dialogue_avatar";
            feedback.style = currentCondition.style == "recast" ? "recast" : "explicit";

            // 2. Dialogue Reply Leakage Guard
            if (string.Equals(feedback.provider, "assistant_agent", StringComparison.OrdinalIgnoreCase))
            {
                if (CorrectionTextGuards.LooksLikeCorrection(payload.dialogueReply))
                {
                    Debug.LogWarning($"[RealLLMService] Correction leakage detected in dialogueReply under assistant_agent: {payload.dialogueReply}");
                    if (IsExperimentLocked())
                    {
                        payload.dialogueReply = BuildSafeTaskContinuation(payload);
                        feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "dialogue_reply_leakage_suppressed");
                    }
                }
            }

            // 3. Recast Purity Guard
            if (string.Equals(feedback.style, "recast", StringComparison.OrdinalIgnoreCase) && feedback.hasFeedback)
            {
                if (CorrectionTextGuards.ViolatesRecastPurity(feedback.feedbackText))
                {
                    Debug.LogWarning($"[RealLLMService] Recast purity violation in feedbackText: {feedback.feedbackText}");
                    if (IsExperimentLocked())
                    {
                        feedback.feedbackText = BuildMinimalRecast(feedback.correctedText);
                        feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "recast_purity_repaired");
                    }
                }
            }
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
                    if (chatHistory.Count > 0 || sessionErrorHistory.Count > 0)
                    {
                        Debug.Log("[RealLLMService] Orchestrator is Idle/Finished. Clearing chat history and session error history.");
                        chatHistory.Clear();
                        sessionErrorHistory.Clear();
                    }
                }
            }
        }

        public void ResetSession()
        {
            if (chatHistory != null)
            {
                chatHistory.Clear();
            }
            if (sessionErrorHistory != null)
            {
                sessionErrorHistory.Clear();
            }
            Debug.Log("[RealLLMService] Chat history and session error history cleared on explicit session reset.");
        }

        #endregion

        #region Send API Requests

        private async Task<string> SendChatRequest(OpenAiMessage[] messages, bool useJsonObject)
        {
            var requiresClientApiKey = RequiresClientApiKey(apiUrl);
            string effectiveKey = string.IsNullOrEmpty(apiKey)
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                : apiKey;

            if (requiresClientApiKey && string.IsNullOrEmpty(effectiveKey))
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
            if (requiresClientApiKey)
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {effectiveKey}");
            }

            webRequest.timeout = 45;
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

        private static bool RequiresClientApiKey(string requestUrl)
        {
            if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
            {
                return true;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !IsLoopbackOrPrivateHost(uri.Host);
        }

        private static bool IsLoopbackOrPrivateHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!System.Net.IPAddress.TryParse(host, out var address))
            {
                return false;
            }

            if (System.Net.IPAddress.IsLoopback(address))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            return bytes.Length == 4
                && (bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254));
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

        #region Streaming Implementation & Helpers

        public IEnumerator GenerateSceneAndReplyStreaming(string userText, Action<string> onSentenceComplete, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            Debug.Log($"[RealLLMService] Generating streaming scene and reply for: {userText}");
            
            // Retrieve latest STT metadata from GatewaySpeechInputModule if available (skip if running in test runner)
            bool isTestRunner = currentCondition != null && currentCondition.participantId == "test_runner";
            if (!isTestRunner)
            {
                lastSttConfidence = 1.0f;
                lastRecordingDurationMs = 0f;
                lastRecordingStopReason = "unknown";
                
                var speechModule = FindObjectOfType<GatewaySpeechInputModule>();
                if (speechModule != null)
                {
                    lastRecordingDurationMs = speechModule.LastRecordingDurationMs;
                    lastRecordingStopReason = speechModule.LastRecordingStopReason;
                    if (speechModule.LastSttResponse != null)
                    {
                        lastSttConfidence = speechModule.LastSttResponse.confidence;
                    }
                    Debug.Log($"[RealLLMService] STT Metadata (Streaming) - Duration: {lastRecordingDurationMs}ms, StopReason: {lastRecordingStopReason}, Confidence: {lastSttConfidence}");
                }
            }

            CheckAndResetSession();

            Task<SpringScenePayload> task;
            if (chatHistory.Count == 0)
            {
                task = ParseIntentStreamingAsync(userText, onSentenceComplete);
            }
            else
            {
                task = GenerateDialogueTurnStreamingAsync(userText, onSentenceComplete);
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

        private async Task<SpringScenePayload> ParseIntentStreamingAsync(string userInput, Action<string> onSentenceComplete)
        {
            if (ShouldSuppressCorrectionByStt(out var suppressionReason))
            {
                var suppressedPayload = BuildSuppressedPayload(suppressionReason);
                onSentenceComplete?.Invoke(suppressedPayload.dialogueReply);
                return suppressedPayload;
            }

            string systemPrompt = BuildSceneSystemPrompt();
            var messages = new[]
            {
                new OpenAiMessage { role = "system", content = systemPrompt },
                new OpenAiMessage { role = "user", content = userInput }
            };

            var parser = new IncrementalJsonParser();
            string fullResponse = await SendChatRequestStreaming(messages, chunk =>
            {
                var sentences = parser.Feed(chunk);
                foreach (var s in sentences)
                {
                    onSentenceComplete?.Invoke(s);
                }
            });

            fullResponse = CleanJsonString(fullResponse);
            var payload = JsonUtility.FromJson<SpringScenePayload>(fullResponse);
            EnsureDialogueReplyPresent(payload);
            ApplyExperimentConditionToPayload(payload);

            chatHistory.Clear();
            string rpSysPrompt = BuildRoleplaySystemPrompt(payload);
            chatHistory.Add(new OpenAiMessage { role = "system", content = rpSysPrompt });
            chatHistory.Add(new OpenAiMessage { role = "user", content = userInput });
            chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });
            return payload;
        }

        private async Task<SpringScenePayload> GenerateDialogueTurnStreamingAsync(string userInput, Action<string> onSentenceComplete)
        {
            if (ShouldSuppressCorrectionByStt(out var suppressionReason))
            {
                var suppressedPayload = BuildSuppressedPayload(suppressionReason);
                onSentenceComplete?.Invoke(suppressedPayload.dialogueReply);
                return suppressedPayload;
            }

            chatHistory.Add(new OpenAiMessage { role = "user", content = userInput });

            var parser = new IncrementalJsonParser();
            string fullResponse = await SendChatRequestStreaming(chatHistory.ToArray(), chunk =>
            {
                var sentences = parser.Feed(chunk);
                foreach (var s in sentences)
                {
                    onSentenceComplete?.Invoke(s);
                }
            });

            fullResponse = CleanJsonString(fullResponse);
            var payload = TryParseDialoguePayload(fullResponse);
            ApplyExperimentConditionToPayload(payload);
            chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });
            return payload;
        }

        private async Task<string> SendChatRequestStreaming(OpenAiMessage[] messages, Action<string> onChunkReceived)
        {
            string effectiveKey = string.IsNullOrEmpty(apiKey)
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                : apiKey;

            if (RequiresClientApiKey(apiUrl) && string.IsNullOrEmpty(effectiveKey))
            {
                throw new Exception("API Key is not set.");
            }

            string jsonBody;
            var requestBody = new OpenAiRequest
            {
                model = modelName,
                messages = messages,
                response_format = new ResponseFormat { type = "json_object" },
                stream = true
            };
            jsonBody = JsonUtility.ToJson(requestBody);

            using var webRequest = new UnityWebRequest(apiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);

            var fullResponseBuilder = new StringBuilder();
            webRequest.downloadHandler = new StreamingDownloadHandler(chunk =>
            {
                fullResponseBuilder.Append(chunk);
                onChunkReceived?.Invoke(chunk);
            });

            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "text/event-stream");
            webRequest.SetRequestHeader("Cache-Control", "no-cache");
            if (RequiresClientApiKey(apiUrl))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {effectiveKey}");
            }

            webRequest.timeout = 45;
            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"API Request Failed: {webRequest.error}";
                throw new Exception(errorMsg);
            }

            return fullResponseBuilder.ToString();
        }

        private SpringScenePayload BuildSuppressedPayload(string suppressionReason)
        {
            return new SpringScenePayload
            {
                dialogueReply = "Sorry, I didn't catch that clearly. Could you say it again?",
                taskType = currentCondition == null ? string.Empty : currentCondition.scenarioId,
                environmentType = currentCondition?.task == null ? string.Empty : currentCondition.task.fallbackEnvironmentType,
                avatarRole = new AvatarRoleData(),
                scene = new ScenePayload(),
                correctionFeedback = new CorrectionFeedbackData
                {
                    hasFeedback = false,
                    provider = currentCondition?.provider ?? "dialogue_avatar",
                    style = currentCondition?.style ?? "explicit",
                    errorType = "none",
                    originalText = "",
                    correctedText = "",
                    feedbackText = "",
                    targetSpan = "",
                    confidence = 1f,
                    rationaleTag = suppressionReason
                }
            };
        }

        private class StreamingDownloadHandler : DownloadHandlerScript
        {
            private Action<string> onChunkReceived;
            private StringBuilder buffer = new StringBuilder();

            public StreamingDownloadHandler(Action<string> onChunkReceived) : base(new byte[16384])
            {
                this.onChunkReceived = onChunkReceived;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength == 0)
                {
                    return false;
                }

                string text = Encoding.UTF8.GetString(data, 0, dataLength);
                buffer.Append(text);

                string fullText = buffer.ToString();
                int lineEnd;
                int lastIndex = 0;

                while ((lineEnd = fullText.IndexOf('\n', lastIndex)) != -1)
                {
                    string line = fullText.Substring(lastIndex, lineEnd - lastIndex).Trim();
                    lastIndex = lineEnd + 1;

                    if (line.StartsWith("data:"))
                    {
                        string dataVal = line.Substring(5).Trim();
                        if (dataVal == "[DONE]")
                        {
                            break;
                        }

                        if (!string.IsNullOrEmpty(dataVal))
                        {
                            try
                            {
                                var chunkJson = JsonUtility.FromJson<OpenAiChunk>(dataVal);
                                if (chunkJson != null && chunkJson.choices != null && chunkJson.choices.Length > 0)
                                {
                                    var content = chunkJson.choices[0].delta.content;
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        onChunkReceived?.Invoke(content);
                                    }
                                }
                            }
                            catch
                            {
                                // Incomplete chunks can fail JsonUtility parsing silently
                            }
                        }
                    }
                }

                if (lastIndex > 0)
                {
                    buffer.Remove(0, lastIndex);
                }

                return true;
            }
        }

        private class IncrementalJsonParser
        {
            private string buffer = "";
            private bool inDialogueReply = false;
            private string dialogueReplyBuffer = "";
            private HashSet<string> sentencesYielded = new HashSet<string>();

            public List<string> Feed(string chunk)
            {
                buffer += chunk;
                var newSentences = new List<string>();

                if (!inDialogueReply)
                {
                    int keyIndex = buffer.IndexOf("\"dialogueReply\"");
                    if (keyIndex != -1)
                    {
                        int colonIndex = buffer.IndexOf(':', keyIndex + 15);
                        if (colonIndex != -1)
                        {
                            int quoteIndex = buffer.IndexOf('"', colonIndex + 1);
                            if (quoteIndex != -1)
                            {
                                inDialogueReply = true;
                                dialogueReplyBuffer = buffer.Substring(quoteIndex + 1);
                                buffer = "";
                            }
                        }
                    }
                }
                else
                {
                    dialogueReplyBuffer += chunk;
                }

                if (inDialogueReply)
                {
                    string cleanText = "";
                    bool escape = false;
                    int endIndex = -1;

                    for (int i = 0; i < dialogueReplyBuffer.Length; i++)
                    {
                        char c = dialogueReplyBuffer[i];
                        if (escape)
                        {
                            cleanText += c;
                            escape = false;
                        }
                        else if (c == '\\')
                        {
                            escape = true;
                        }
                        else if (c == '"')
                        {
                            endIndex = i;
                            break;
                        }
                        else
                        {
                            cleanText += c;
                        }
                    }

                    int lastBoundary = 0;
                    for (int i = 0; i < cleanText.Length; i++)
                    {
                        char c = cleanText[i];
                        if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？')
                        {
                            bool isBoundary = false;
                            if (i == cleanText.Length - 1)
                            {
                                isBoundary = (endIndex != -1);
                            }
                            else
                            {
                                char next = cleanText[i + 1];
                                isBoundary = char.IsWhiteSpace(next) || next == '"';
                            }

                            if (isBoundary)
                            {
                                string s = cleanText.Substring(lastBoundary, (i + 1) - lastBoundary).Trim();
                                if (!string.IsNullOrEmpty(s) && !sentencesYielded.Contains(s))
                                {
                                    newSentences.Add(s);
                                    sentencesYielded.Add(s);
                                }
                                lastBoundary = i + 1;
                            }
                        }
                    }

                    if (endIndex != -1)
                    {
                        inDialogueReply = false;
                        string remaining = cleanText.Substring(lastBoundary).Trim();
                        if (!string.IsNullOrEmpty(remaining) && !sentencesYielded.Contains(remaining))
                        {
                            newSentences.Add(remaining);
                            sentencesYielded.Add(remaining);
                        }
                        dialogueReplyBuffer = dialogueReplyBuffer.Substring(endIndex + 1);
                    }
                }

                return newSentences;
            }
        }

        #endregion

        #region API Data Schemas
        [Serializable]
        private class OpenAiRequest
        {
            public string model;
            public OpenAiMessage[] messages;
            public ResponseFormat response_format;
            public bool stream;
        }

        [Serializable]
        private class OpenAiTextRequest
        {
            public string model;
            public OpenAiMessage[] messages;
            public bool stream;
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

        [Serializable]
        private class OpenAiChunk
        {
            public OpenAiChunkChoice[] choices;
        }

        [Serializable]
        private class OpenAiChunkChoice
        {
            public OpenAiChunkDelta delta;
        }

        [Serializable]
        private class OpenAiChunkDelta
        {
            public string content;
        }
        #endregion
    }
}
