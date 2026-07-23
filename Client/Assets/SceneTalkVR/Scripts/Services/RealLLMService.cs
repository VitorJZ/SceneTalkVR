using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Voice;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneTalkVR.Runtime.Services
{
    /// <summary>
    /// Real implementation of LLM service using SJTU Local API (OpenAI compatible).
    /// </summary>
    public sealed class RealLLMService : MonoBehaviour,
        ISceneTalkFeedbackFirstStreamingBrain,
        ILLMService,
        ISceneTalkSessionReset,
        ISceneTalkExperimentContextReceiver,
        ISceneTalkExperimentLockReceiver,
        ISceneTalkConversationContextReceiver
    {
        [Header("API Configuration")]
        [SerializeField] private string apiUrl = "https://models.sjtu.edu.cn/api/v1/chat/completions";
        [SerializeField] private string apiKey = ""; 
        [SerializeField] private string modelName = "deepseek-chat";
        
        [Header("Feedback Strategy")]
        [Tooltip("Feedback strictness: conservative (only severe errors), moderate (general errors), active (almost all errors)")]
        [SerializeField] private string feedbackSensitivity = "moderate"; // conservative | moderate | active

        [Header("Avatar Dialogue Pacing")]
        [Range(0f, 1f)] [SerializeField] private float temperature = 0.7f;
        [Min(0)] [SerializeField] private int maxNonGoalQuestionsPerTask = 3;

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
        private readonly HashSet<string> askedNonGoalQuestionIds = new HashSet<string>(StringComparer.Ordinal);
        private int nonGoalQuestionsAsked;
        private SceneTalkOrchestrator cachedOrchestrator;
        private CorrectionExperimentCondition currentCondition;
        public CorrectionExperimentCondition CurrentCondition => currentCondition;
        public string FeedbackSensitivity => string.IsNullOrWhiteSpace(feedbackSensitivity)
            ? "moderate"
            : feedbackSensitivity.Trim().ToLowerInvariant();
        public float DialoguePacingTemperature => Mathf.Clamp01(temperature);
        public int MaxNonGoalQuestionsPerTask => Mathf.Max(0, maxNonGoalQuestionsPerTask);
        public int NonGoalQuestionsAsked => nonGoalQuestionsAsked;

        private sealed class AvatarDialoguePacingDecision
        {
            public AvatarDialoguePacingData data = new AvatarDialoguePacingData();
            public NonGoalQuestionDefinition question;
        }

        private float lastSttConfidence = 1.0f;
        private bool lastSttConfidenceAvailable;
        private float lastRecordingDurationMs = 0f;
        private string lastRecordingStopReason = "unknown";

        public float LastFirstTokenLatencyMs { get; private set; } = -1f;
        public float LastFirstSentenceLatencyMs { get; private set; } = -1f;
        private bool formalDialogueLeakageDetected;
        private float streamStartTime;

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

        public void ConfigureDialoguePacing(float pacingTemperature, int maximumQuestionsPerTask)
        {
            temperature = Mathf.Clamp01(pacingTemperature);
            maxNonGoalQuestionsPerTask = Mathf.Max(0, maximumQuestionsPerTask);
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
            if (HasPacingScopeChanged(currentCondition, condition))
            {
                nonGoalQuestionsAsked = 0;
                askedNonGoalQuestionIds.Clear();
            }
            currentCondition = ExperimentConditionManager.CloneCondition(condition);
        }

        private static bool HasPacingScopeChanged(
            CorrectionExperimentCondition previous,
            CorrectionExperimentCondition next)
        {
            if (previous == null || next == null)
            {
                return previous != next;
            }

            return !string.Equals(previous.sessionId, next.sessionId, StringComparison.Ordinal)
                   || !string.Equals(previous.task?.taskId, next.task?.taskId, StringComparison.Ordinal);
        }

        public void RestoreConversationContext(LearningSessionDetail session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            currentCondition = ExperimentConditionManager.CloneCondition(session.settings?.condition);
            if (!IsExperimentLocked() && !string.IsNullOrWhiteSpace(session.settings?.feedbackSensitivity))
            {
                feedbackSensitivity = NormalizeFeedbackSensitivity(session.settings.feedbackSensitivity);
            }

            chatHistory.Clear();
            sessionErrorHistory.Clear();
            nonGoalQuestionsAsked = 0;
            askedNonGoalQuestionIds.Clear();

            var sceneContext = LearningMemoryService.ClonePayload(session.sceneSnapshot);
            chatHistory.Add(new OpenAiMessage
            {
                role = "system",
                content = BuildRoleplaySystemPrompt(sceneContext)
            });

            var turns = session.turns ?? Array.Empty<DialogueTurnRecord>();
            foreach (var turn in turns)
            {
                if (turn == null)
                {
                    continue;
                }

                if (!turn.isOpening && !string.IsNullOrWhiteSpace(turn.userText))
                {
                    chatHistory.Add(new OpenAiMessage { role = "user", content = turn.userText });
                }

                if (!string.IsNullOrWhiteSpace(turn.assistantText))
                {
                    chatHistory.Add(new OpenAiMessage { role = "assistant", content = turn.assistantText });
                }

                if (turn.payload?.dialoguePacing?.triggered == true)
                {
                    nonGoalQuestionsAsked++;
                    var restoredQuestionId = turn.payload.dialoguePacing.questionId;
                    if (!string.IsNullOrWhiteSpace(restoredQuestionId))
                    {
                        askedNonGoalQuestionIds.Add(restoredQuestionId.Trim());
                    }
                }

                var feedback = turn.payload?.correctionFeedback;
                if (feedback != null
                    && feedback.hasFeedback
                    && !string.IsNullOrWhiteSpace(feedback.errorType)
                    && !string.Equals(feedback.errorType, "none", StringComparison.OrdinalIgnoreCase))
                {
                    sessionErrorHistory.Add(feedback.errorType);
                }
            }

            Debug.Log(
                $"[RealLLMService] Restored history session {session.summary?.sessionId} with {turns.Length} stored turn(s).",
                this);
        }

        private static string NormalizeFeedbackSensitivity(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "moderate"
                : value.Trim().ToLowerInvariant();
            return normalized == "conservative" || normalized == "active"
                ? normalized
                : "moderate";
        }

        public IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            Debug.Log($"[RealLLMService] Generating scene and reply for: {userText}");

            RefreshSttMetadata(isStreaming: false);

            CheckAndResetSession();
            formalDialogueLeakageDetected = false;
            var pacingDecision = CreateAvatarDialoguePacingDecision(userText);

            var timing = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionRequestStarted);
            var correctionTask = ParseCorrectionFeedbackAsync(userText);
            timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueRequestStarted);
            var dialogueTask = ParseDialogueContinuationNonStreamingAsync(userText, pacingDecision);
            var correctionReadyLogged = false;
            var dialogueReadyLogged = false;

            while (!correctionTask.IsCompleted || !dialogueTask.IsCompleted)
            {
                if (!correctionReadyLogged && correctionTask.Status == TaskStatus.RanToCompletion)
                {
                    var ready = correctionTask.Result;
                    timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionTextReady, feedbackText: ready?.feedbackText ?? ready?.recastText);
                    correctionReadyLogged = true;
                }
                if (!dialogueReadyLogged && dialogueTask.Status == TaskStatus.RanToCompletion)
                {
                    timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueFirstToken);
                    timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueFirstSentenceReady);
                    dialogueReadyLogged = true;
                }
                yield return null;
            }

            var dialogueFailed = dialogueTask.IsFaulted || dialogueTask.IsCanceled;
            var correctionFailed = correctionTask.IsFaulted || correctionTask.IsCanceled;
            if (dialogueFailed || correctionFailed && IsExperimentLocked())
            {
                var ex = correctionFailed
                    ? correctionTask.Exception?.InnerException ?? correctionTask.Exception
                    : dialogueTask.Exception?.InnerException ?? dialogueTask.Exception;
                timing?.MarkTurnTechnicalInvalid(
                    correctionFailed ? "CorrectionPlanner" : "DialogueGenerator",
                    ex?.Message ?? "parallel_request_failed");
                onError?.Invoke(ex?.Message ?? "Parallel LLM tasks faulted.");
                yield break;
            }

            var payload = dialogueTask.Result;
            var feedback = correctionFailed
                ? BuildCorrectionFallback("correction_generation_failed", correctionTask.Exception)
                : correctionTask.Result;
            if (!correctionReadyLogged)
            {
                timing?.RecordTimingEvent(
                    ExperimentTimingEventType.CorrectionTextReady,
                    feedbackText: feedback?.feedbackText ?? feedback?.recastText);
            }
            if (!dialogueReadyLogged)
            {
                timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueFirstToken);
                timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueFirstSentenceReady);
            }
            payload.correctionFeedback = feedback;
            CommitAvatarDialoguePacing(payload, pacingDecision);
            ApplyExperimentConditionToPayload(payload);
            if (formalDialogueLeakageDetected)
            {
                timing?.MarkTurnTechnicalInvalid("DialogueLeakageGuard", "correction_leakage_in_avatar_dialogue");
                onError?.Invoke("Formal turn invalid: correction leakage detected in Avatar dialogue.");
                yield break;
            }

            // Update chat history
            if (chatHistory.Count == 0)
            {
                chatHistory.Clear();
                string rpSysPrompt = BuildRoleplaySystemPrompt(payload);
                chatHistory.Add(new OpenAiMessage { role = "system", content = rpSysPrompt });
                chatHistory.Add(new OpenAiMessage { role = "user", content = userText });
                chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });
            }
            else
            {
                chatHistory.Add(new OpenAiMessage { role = "user", content = userText });
                chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });
            }

            onComplete?.Invoke(payload);
        }

        #region ILLMService Implementation

        public async Task<SpringScenePayload> ParseIntentAsync(string userInput)
        {
            var pacingDecision = CreateAvatarDialoguePacingDecision(userInput);
            var feedback = await ParseCorrectionFeedbackAsync(userInput);
            var payload = await ParseDialogueContinuationNonStreamingAsync(userInput, pacingDecision);
            payload.correctionFeedback = feedback;
            CommitAvatarDialoguePacing(payload, pacingDecision);
            ApplyExperimentConditionToPayload(payload);
            return payload;
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

        public async Task<string> GenerateStructuredGoalEvaluationAsync(string requestJson)
        {
            const string systemPrompt =
                "You are a strict communication-goal evaluator for an English speaking task. " +
                "Determine whether the PARTICIPANT has actually completed each specified communication goal.\n\n" +
                "Rules:\n" +
                "1. Evaluate only participant speech. Never use dialogue-avatar or feedback-agent speech as evidence.\n" +
                "2. Accept natural paraphrases, synonyms, short answers, different numbers or names, conversational ellipsis, harmless grammar errors, filler words, and common speech-to-text punctuation errors.\n" +
                "3. Do not require exact wording or keywords from the goal definition.\n" +
                "4. Distinguish the required speech act: providing information, asking a question, making a request, reporting a problem, or expressing a restriction or preference.\n" +
                "5. A keyword mention alone is not enough.\n" +
                "6. Do not mark a goal achieved when the participant negates or rejects the goal, says they do not need or want it, describes an unrelated past event, quotes another person, uses a hypothetical or counterfactual statement, or merely repeats the goal wording without performing the required speech act.\n" +
                "7. Goals such as no reservation, wrong dish, or dietary restriction may legitimately use negative language. Judge intended meaning, not the word not.\n" +
                "8. Use the latest participant utterance and recent participant utterances from the same task when a goal is expressed across multiple turns.\n" +
                "9. Evidence must quote or closely reproduce participant words supporting the decision.\n" +
                "10. If evidence is ambiguous, incomplete, contradictory, or unrelated, return achieved=false.\n" +
                "11. Return strict JSON only, with no markdown or text outside JSON.\n\n" +
                "Output schema: {\"taskId\":\"<task id>\",\"turnId\":\"<turn id>\",\"evaluations\":[{\"goalId\":\"<goal id>\",\"achieved\":true,\"confidence\":0.0,\"evidence\":\"<participant evidence or empty string>\",\"reason\":\"<brief semantic reason>\",\"evaluatorVersion\":\"goal_evaluator_v1.2.1+structured_llm\"}]}.\n" +
                "Confidence: 0.90-1.00 direct and unambiguous; 0.75-0.89 clear paraphrase or completion distributed across recent participant turns; 0.50-0.74 plausible but incomplete or ambiguous; below 0.50 unsupported or contradictory.";
            var responseJson = await SendChatRequest(systemPrompt, requestJson, true);
            var response = JsonUtility.FromJson<OpenAiResponse>(responseJson);
            if (response?.choices == null || response.choices.Length == 0 || string.IsNullOrWhiteSpace(response.choices[0].message?.content))
                throw new InvalidOperationException("Structured goal evaluation response was empty.");
            return response.choices[0].message.content;
        }

        #endregion

        #region Dialogue Multi-Turn Helpers

        private AvatarDialoguePacingDecision CreateAvatarDialoguePacingDecision(string userInput)
        {
            var decision = new AvatarDialoguePacingDecision
            {
                data = new AvatarDialoguePacingData
                {
                    triggered = false,
                    questionId = string.Empty,
                    temperature = DialoguePacingTemperature,
                    randomSample = -1f
                }
            };
            var task = currentCondition?.task;
            if (MaxNonGoalQuestionsPerTask <= 0
                || task?.nonGoalQuestions == null
                || task.nonGoalQuestions.Length == 0
                || string.IsNullOrWhiteSpace(userInput))
            {
                return decision;
            }

            var candidates = new List<NonGoalQuestionDefinition>();
            var uniqueQuestionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var question in task.nonGoalQuestions)
            {
                if (question != null && !string.IsNullOrWhiteSpace(question.questionId)
                    && !string.IsNullOrWhiteSpace(question.text))
                {
                    var questionId = question.questionId.Trim();
                    if (uniqueQuestionIds.Add(questionId)
                        && !askedNonGoalQuestionIds.Contains(questionId))
                    {
                        candidates.Add(question);
                    }
                }
            }
            var effectiveQuestionLimit = Mathf.Min(MaxNonGoalQuestionsPerTask, uniqueQuestionIds.Count);
            if (nonGoalQuestionsAsked >= effectiveQuestionLimit || candidates.Count == 0)
            {
                return decision;
            }

            var key = string.Join("|",
                currentCondition?.sessionId ?? string.Empty,
                task.taskId ?? string.Empty,
                currentCondition?.turnIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                userInput.Trim());
            decision.data.randomSample = StableUnitSample(key + "|trigger");
            if (decision.data.randomSample >= DialoguePacingTemperature)
            {
                return decision;
            }

            var questionIndex = (int)(StableHash(key + "|question") % (uint)candidates.Count);
            decision.question = candidates[questionIndex];
            decision.data.triggered = true;
            decision.data.questionId = decision.question.questionId.Trim();
            return decision;
        }

        private string BuildAvatarDialogueSystemPrompt(
            AvatarDialoguePacingDecision pacingDecision,
            SpringScenePayload rolePayload = null)
        {
            var task = currentCondition?.task;
            var role = rolePayload?.avatarRole?.role;
            var speed = rolePayload?.avatarRole?.speakingSpeed;
            var accent = rolePayload?.avatarRole?.accent;
            var attitude = rolePayload?.avatarRole?.attitude;
            var environment = rolePayload?.environmentType;
            role = string.IsNullOrWhiteSpace(role) ? task?.fallbackAvatarRole ?? "tutor" : role;
            speed = string.IsNullOrWhiteSpace(speed) ? "medium" : speed;
            accent = string.IsNullOrWhiteSpace(accent) ? "american" : accent;
            attitude = string.IsNullOrWhiteSpace(attitude) ? task?.fallbackAvatarAttitude ?? "friendly" : attitude;
            environment = string.IsNullOrWhiteSpace(environment) ? task?.fallbackEnvironmentType ?? "classroom" : environment;

            var triggered = pacingDecision?.data?.triggered == true
                && pacingDecision.question != null
                && !string.IsNullOrWhiteSpace(pacingDecision.question.text);
            var builder = new StringBuilder();
            builder.AppendLine("You are the in-scene character for an English oral-practice conversation.");
            builder.AppendLine($"You are playing the role of a {role} in a {environment} environment.");
            builder.AppendLine($"Your accent is {accent}, your attitude is {attitude}, and you speak at a {speed} speed.");
            if (!string.IsNullOrWhiteSpace(task?.context))
                builder.AppendLine($"Scene context: {task.context}");
            if (!string.IsNullOrWhiteSpace(task?.roleplayPrompt))
                builder.AppendLine($"Roleplay boundary: {task.roleplayPrompt}");

            builder.AppendLine();
            builder.AppendLine("The participant's private task goals are not part of your context.");
            builder.AppendLine("Do not infer, enumerate, hint at, or proactively introduce a checklist, transaction step, requirement, or completion topic that the participant has not raised.");
            builder.AppendLine("Never invent, assume, or confirm any specific task detail that the participant has not explicitly stated.");
            builder.AppendLine("Respond naturally to the participant's latest statement in 1-3 concise sentences. Stay in role and in the current scene.");
            builder.AppendLine("Do not provide language corrections, alternative phrasing, grammar tips, or comments on the participant's English.");
            builder.AppendLine();
            builder.AppendLine("PACING DIRECTIVE");
            builder.AppendLine($"- pacingTriggered: {(triggered ? "true" : "false")}");
            if (triggered)
            {
                builder.AppendLine("- Respond to the participant first, then ask exactly this question as the final sentence:");
                builder.AppendLine($"  {pacingDecision.question.text.Trim()}");
                builder.AppendLine("- Ask it once, verbatim, without explaining why.");
                builder.AppendLine("- Do not add another question.");
            }
            else
            {
                builder.AppendLine("- Do not ask any question in this turn.");
                builder.AppendLine("- Do not request, confirm, or solicit any information from the participant.");
                builder.AppendLine("- Respond only to what the participant has already said, and end with a declarative sentence.");
                builder.AppendLine("- Do not claim that any transaction or task step has occurred.");
            }
            builder.AppendLine();
            builder.AppendLine("Return ONLY a valid JSON object with this schema:");
            builder.AppendLine("{\"dialogueContinuation\":\"character's reply text\"}");
            return builder.ToString();
        }

        private void CommitAvatarDialoguePacing(
            SpringScenePayload payload,
            AvatarDialoguePacingDecision decision)
        {
            if (payload == null) return;
            var source = decision?.data;
            payload.dialoguePacing = source == null
                ? new AvatarDialoguePacingData
                {
                    temperature = DialoguePacingTemperature,
                    randomSample = -1f
                }
                : new AvatarDialoguePacingData
                {
                    triggered = source.triggered,
                    questionId = source.questionId ?? string.Empty,
                    temperature = source.temperature,
                    randomSample = source.randomSample
                };
            if (source?.triggered == true)
            {
                var questionId = source.questionId?.Trim();
                if (!string.IsNullOrWhiteSpace(questionId) && askedNonGoalQuestionIds.Add(questionId))
                {
                    nonGoalQuestionsAsked++;
                }
            }
        }

        private static bool EnsureSelectedQuestionAtEnd(
            SpringScenePayload payload,
            AvatarDialoguePacingDecision decision)
        {
            if (payload == null || decision?.data?.triggered != true
                || string.IsNullOrWhiteSpace(decision.question?.text))
            {
                return false;
            }

            var question = decision.question.text.Trim();
            var reply = (payload.dialogueReply ?? payload.dialogueContinuation ?? string.Empty).Trim();
            var selectedQuestionWasPresent = reply.IndexOf(question, StringComparison.Ordinal) >= 0;
            var withoutSelectedQuestion = reply.Replace(question, string.Empty).Trim();
            var statements = RemoveQuestionSentences(withoutSelectedQuestion);
            if (string.IsNullOrWhiteSpace(statements))
            {
                statements = "I see.";
            }

            var combined = statements.Trim() + " " + question;
            payload.dialogueReply = combined;
            payload.dialogueContinuation = combined;
            return !selectedQuestionWasPresent;
        }

        private static string RemoveQuestionSentences(string value)
        {
            var kept = new StringBuilder();
            var keptCount = 0;
            foreach (var sentence in SplitDialogueSentences(value))
            {
                if (sentence.TrimEnd().EndsWith("?", StringComparison.Ordinal))
                {
                    continue;
                }

                if (kept.Length > 0)
                {
                    kept.Append(' ');
                }
                kept.Append(sentence.Trim());
                keptCount++;
                if (keptCount >= 2)
                {
                    break;
                }
            }
            return kept.ToString();
        }

        private static List<string> SplitDialogueSentences(string value)
        {
            var sentences = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return sentences;
            }

            var start = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character != '.' && character != '!' && character != '?')
                {
                    continue;
                }

                var sentence = value.Substring(start, i - start + 1).Trim();
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    sentences.Add(sentence);
                }
                start = i + 1;
            }

            if (start < value.Length)
            {
                var trailing = value.Substring(start).Trim();
                if (!string.IsNullOrWhiteSpace(trailing))
                {
                    sentences.Add(trailing);
                }
            }
            return sentences;
        }

        private static float StableUnitSample(string value) =>
            (StableHash(value) & 0x00FFFFFFu) / 16777216f;

        private static uint StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private string BuildCorrectionSystemPrompt()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are an English language tutor analyzing the user's speech in a VR oral practice context.");
            builder.AppendLine("Your ONLY job is to analyze the user's input for grammar, vocabulary, pronunciation, or expression errors.");
            builder.AppendLine("Do NOT continue the dialogue, do NOT act as the roleplay character, and do NOT generate any conversational replies.");

            if (currentCondition != null)
            {
                builder.AppendLine("\n=== EXPERIMENT & TASK CONTEXT ===");
                builder.AppendLine($"- scenarioId: {currentCondition.scenarioId}");
                builder.AppendLine($"- feedbackStyle: {currentCondition.style}");
                builder.AppendLine($"- feedbackSensitivity: {feedbackSensitivity}");
                if (!string.IsNullOrWhiteSpace(currentCondition.task?.context))
                {
                    builder.AppendLine($"- taskContext: {currentCondition.task.context}");
                }
                builder.AppendLine("Use this context only to understand the scene. Private task goals, goal IDs, completion rules, and target phrases are intentionally excluded.");
            }

            var isRecast = currentCondition != null
                           && string.Equals(currentCondition.style, "recast", StringComparison.OrdinalIgnoreCase);

            builder.AppendLine("\n=== LANGUAGE CORRECTION INSTRUCTIONS ===");
            builder.AppendLine("1. Detect at most ONE major error in the user's speech. If no clear error, set hasFeedback = false.");
            builder.AppendLine("2. Respect the feedback sensitivity level. Ignore minor repetitions or normal self-corrections.");

            if (isRecast)
            {
                builder.AppendLine("3. Generate unified feedback text under 'recast' style:");
                builder.AppendLine("   * Both Avatar and Agent MUST use the exact same recastText.");
                builder.AppendLine("   * Recast text must be a natural confirmation or model utterance suitable for BOTH the main character and helper agent.");
                builder.AppendLine("   * Recast text MUST use the SECOND person ('you', 'your', 'you'd like') from the speaker's perspective to confirm or recast what the user said. NEVER use the first person ('I', 'my', 'I'd like').");
                builder.AppendLine("   * Recast text MUST NOT contain any explicit correction words (forbidden: 'you mean', 'should', 'should say', 'correct', 'wrong', 'mistake', 'instead', 'better way', 'remember to', 'grammar tip').");
                builder.AppendLine("   * Few-Shot Examples under 'recast' style:");
                builder.AppendLine("     - User: 'I is hungry' -> recastText: 'Ah, you are hungry now.' (CORRECT) | 'I am hungry.' (INCORRECT)");
                builder.AppendLine("     - User: 'I want join the gym' -> recastText: 'So you want to join the gym.' (CORRECT) | 'I want to join the gym.' (INCORRECT)");
                builder.AppendLine("     - User: 'I like reserve tomorrow' -> recastText: 'You'd like to reserve for tomorrow.' (CORRECT) | 'I'd like to reserve tomorrow.' (INCORRECT)");
                builder.AppendLine("   * You MUST set feedbackText = \"\".");
            }
            else
            {
                builder.AppendLine("3. Generate unified feedback text under 'explicit' style:");
                builder.AppendLine("   * Both Avatar and Agent MUST use the exact same explicit feedbackText.");
                builder.AppendLine("   * You MUST use this exact format: 'Grammar tip: [one short rule]. Try: \"[correct expression]\".'");
                builder.AppendLine("   * Example: 'Grammar tip: Use \"really\" before a verb, not \"very.\" Try: \"I really like this furniture.\"'");
                builder.AppendLine("   * You MUST set recastText = \"\".");
            }

            builder.AppendLine("4. Keep the text brief and natural for VR spoken TTS.");
            builder.AppendLine("5. Output ONLY a valid JSON object matching this schema:");
            builder.AppendLine("{");
            builder.AppendLine("  \"hasFeedback\": true/false,");
            builder.AppendLine("  \"errorType\": \"grammar|unnatural|vocabulary|incomplete|unknown\",");
            builder.AppendLine("  \"originalText\": \"user's incorrect sentence\",");
            builder.AppendLine("  \"correctedText\": \"corrected sentence\",");
            if (isRecast)
            {
                builder.AppendLine("  \"feedbackText\": \"\",");
                builder.AppendLine("  \"recastText\": \"natural confirmation/model utterance (recast style)\",");
            }
            else
            {
                builder.AppendLine("  \"feedbackText\": \"Grammar tip: [rule]. Try: \\\"[correct expression]\\\" (explicit style)\",");
                builder.AppendLine("  \"recastText\": \"\",");
            }
            builder.AppendLine("  \"targetSpan\": \"wrong phrase/word\",");
            builder.AppendLine("  \"confidence\": 1.0,");
            builder.AppendLine("  \"rationaleTag\": \"short tag explanation\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private async Task<CorrectionFeedbackData> ParseCorrectionFeedbackAsync(string userInput)
        {
            if (ShouldSuppressCorrectionByStt(out var suppressionReason))
            {
                return BuildCorrectionFallback(suppressionReason);
            }

            string systemPrompt = BuildCorrectionSystemPrompt();
            string responseText = await SendChatRequest(
                systemPrompt,
                userInput,
                true,
                () => FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include)
                    ?.RecordTimingEvent(ExperimentTimingEventType.CorrectionFirstToken));

            // responseText is the complete OpenAI response envelope. Do not run
            // content cleanup on it: reasoning_content can contain </think>, and
            // trimming at that marker corrupts the outer JSON object.
            var response = JsonUtility.FromJson<OpenAiResponse>(responseText);
            if (response == null || response.choices == null || response.choices.Length == 0)
            {
                throw new Exception("Correction Planner returned invalid or empty response structure.");
            }

            string content = response.choices[0].message.content;
            content = CleanJsonString(content);
            Debug.Log($"[RealLLMService] Correction Planner content: {content}");

            var feedback = JsonUtility.FromJson<CorrectionFeedbackData>(content);
            if (feedback == null)
            {
                throw new Exception("Correction Planner returned malformed JSON content.");
            }

            if (feedback.hasFeedback && currentCondition != null && string.Equals(currentCondition.style, "recast", StringComparison.OrdinalIgnoreCase))
            {
                feedback.recastText = string.IsNullOrEmpty(feedback.recastText) ? feedback.feedbackText : feedback.recastText;
                feedback.feedbackText = feedback.recastText;
            }

            return feedback;
        }

        private async Task<SpringScenePayload> ParseDialogueContinuationNonStreamingAsync(
            string userInput,
            AvatarDialoguePacingDecision pacingDecision)
        {
            string systemPrompt = BuildAvatarDialogueSystemPrompt(pacingDecision);

            var messagesList = new System.Collections.Generic.List<OpenAiMessage>();
            if (chatHistory.Count == 0)
            {
                messagesList.Add(new OpenAiMessage { role = "system", content = systemPrompt });
                messagesList.Add(new OpenAiMessage { role = "user", content = userInput });
            }
            else
            {
                messagesList.Add(new OpenAiMessage { role = "system", content = systemPrompt });
                for (int i = 1; i < chatHistory.Count; i++)
                {
                    messagesList.Add(chatHistory[i]);
                }
                messagesList.Add(new OpenAiMessage { role = "user", content = userInput });
            }

            string responseJson = await SendChatRequest(messagesList.ToArray(), true);
            try
            {
                var response = JsonUtility.FromJson<OpenAiResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Length > 0)
                {
                    var content = response.choices[0].message.content;
                    content = CleanJsonString(content);
                    
                    var payload = TryParseDialoguePayload(content);
                    if (payload != null)
                    {
                        if (string.IsNullOrEmpty(payload.dialogueReply) && !string.IsNullOrEmpty(payload.dialogueContinuation))
                        {
                            payload.dialogueReply = payload.dialogueContinuation;
                        }
                        EnsureSelectedQuestionAtEnd(payload, pacingDecision);
                    }
                    return payload;
                }
                throw new Exception("Dialogue Continuation Generator returned invalid response structure.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealLLMService] Dialogue continuation error: {ex.Message}");
                throw;
            }
        }

        private string BuildSceneSystemPrompt()
        {
            return systemPrompt + "\n\n" + BuildExperimentPromptInstructions(true);
        }

        private SpringScenePayload TryParseDialoguePayload(string content)
        {
            var fallbackContent = content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var payload = JsonUtility.FromJson<SpringScenePayload>(content);
                    if (payload != null)
                    {
                        if (string.IsNullOrEmpty(payload.dialogueReply) && !string.IsNullOrEmpty(payload.dialogueContinuation))
                        {
                            payload.dialogueReply = payload.dialogueContinuation;
                        }
                        EnsureDialogueReplyPresent(payload);
                        EnsurePayloadDefaults(payload);
                        return payload;
                    }
                }
                catch (FormatException ex)
                {
                    Debug.LogWarning($"[RealLLMService] Dialogue JSON parse fallback: {ex.Message}");
                    if (content.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        fallbackContent = "Sorry, I couldn't format that reply correctly. Could you say that again?";
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RealLLMService] Dialogue JSON parse fallback: {ex.Message}");
                }
            }

            return new SpringScenePayload
            {
                dialogueReply = fallbackContent,
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
            return BuildAvatarDialogueSystemPrompt(null, initialPayload);
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
                builder.AppendLine($"- currentTaskId: {task.taskId}");
                builder.AppendLine($"- avatarRole: {task.fallbackAvatarRole}");
                if (!string.IsNullOrWhiteSpace(task.roleplayPrompt))
                {
                    builder.AppendLine($"- roleplayPrompt: {task.roleplayPrompt}");
                }
                builder.AppendLine("The task, scene, panorama, avatar identity, provider, and style are immutable. Continue only the in-task dialogue; never parse a new scene intent or output replacement scene/layout/avatar data.");
            }

            builder.AppendLine("\n=== SCENARIO GUIDANCE ===");
            builder.AppendLine("Use the task context only to understand the scene. Do not infer or introduce private participant goals.");
            builder.AppendLine("If feedbackProvider is assistant_agent: dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.");

            builder.AppendLine("\n=== SPEECH CAPTURE METADATA ===");
            builder.AppendLine($"- recordingDurationMs: {lastRecordingDurationMs} ms");
            builder.AppendLine($"- recordingStopReason: {lastRecordingStopReason}");
            builder.AppendLine(lastSttConfidenceAvailable
                ? $"- sttConfidence: {lastSttConfidence}"
                : "- sttConfidence: unavailable");
            if (lastSttConfidenceAvailable && lastSttConfidence < 0.5f)
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
            builder.AppendLine("4. If feedbackProvider = assistant_agent: Your dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.");
            builder.AppendLine("5. Customize feedbackText based on feedbackStyle and feedbackProvider:");
            builder.AppendLine("   - If style is 'explicit':");
            builder.AppendLine("     * If provider is 'dialogue_avatar': Keep it brief, conversational, and character-appropriate. You MUST use this exact format: 'Small correction: you can say, \"[correct expression]\".' Example: 'Small correction: you can say, \"I really like this furniture.\"' Do NOT include any grammar rules or explanations.");
            builder.AppendLine("     * If provider is 'assistant_agent': Act as an instructor helper. You MUST use this exact format: 'Grammar tip: [one short rule]. Try: \"[correct expression]\".' Example: 'Grammar tip: Use \"really\" before a verb, not \"very.\" Try: \"I really like this furniture.\"' Limit the rule explanation to one short, simple sentence (at most 2 sentences total including the recommendation).");
            builder.AppendLine("   - If style is 'recast':");
            builder.AppendLine("     * Strict Recast Rule: NEVER use direct correction words. You are FORBIDDEN from using any of these terms in feedbackText: 'you mean', 'should', 'should say', 'say', 'correct', 'wrong', 'mistake', 'instead', 'instead of', 'grammar tip', 'better way', 'remember to'.");
            builder.AppendLine("     * If provider is 'dialogue_avatar': The feedbackText should sound like the character natural confirmation or continuation of the talk. Example: 'Oh, you really like this furniture?'");
            builder.AppendLine("     * If provider is 'assistant_agent': Provide only a clean model utterance in the corrected form. Do not address the learner directly. Do not explain. Do not use 'you mean'. The feedbackText should sound like a quiet language model example, not a correction. Example: 'I really like this furniture.'");
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

            if (lastSttConfidenceAvailable
                && lastSttConfidence >= 0
                && lastSttConfidence < 0.5f)
            {
                sttSuppressionReason = "low_confidence_suppressed";
                return true;
            }

            return false;
        }

        private void RefreshSttMetadata(bool isStreaming)
        {
            var isTestRunner = currentCondition != null
                && currentCondition.participantId == "test_runner";
            if (isTestRunner)
            {
                // Editor tests inject confidence directly and expect it to participate in suppression.
                lastSttConfidenceAvailable = true;
                return;
            }

            lastSttConfidence = 1.0f;
            lastSttConfidenceAvailable = false;
            lastRecordingDurationMs = 0f;
            lastRecordingStopReason = "unknown";

            var speechModule = FindFirstObjectByType<GatewaySpeechInputModule>();
            if (speechModule == null)
            {
                return;
            }

            lastRecordingDurationMs = speechModule.LastRecordingDurationMs;
            lastRecordingStopReason = speechModule.LastRecordingStopReason;
            if (speechModule.LastSttResponse != null)
            {
                lastSttConfidence = speechModule.LastSttResponse.confidence;
                lastSttConfidenceAvailable = speechModule.LastSttResponse.confidenceAvailable;
            }

            var confidenceLog = lastSttConfidenceAvailable
                ? lastSttConfidence.ToString()
                : "unavailable";
            var streamLabel = isStreaming ? " (Streaming)" : string.Empty;
            Debug.Log(
                $"[RealLLMService] STT Metadata{streamLabel} - "
                + $"Duration: {lastRecordingDurationMs}ms, "
                + $"StopReason: {lastRecordingStopReason}, "
                + $"Confidence: {confidenceLog}");
        }

        private string BuildSafeTaskContinuation(SpringScenePayload payload)
        {
            return "I see. Could you tell me a little more about that?";
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

            // 2. Dialogue Reply Leakage Guard (Unconditional)
            if (string.Equals(feedback.provider, "assistant_agent", StringComparison.OrdinalIgnoreCase))
            {
                if (CorrectionTextGuards.LooksLikeCorrection(payload.dialogueReply))
                {
                    Debug.LogWarning($"[RealLLMService] Correction leakage detected in dialogueReply under assistant_agent: {payload.dialogueReply}");
                    if (currentCondition.formalExperiment)
                    {
                        formalDialogueLeakageDetected = true;
                        return;
                    }
                    payload.dialogueReply = BuildSafeTaskContinuation(payload);
                    feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "dialogue_reply_leakage_suppressed");
                }
            }

            // 3. Recast Purity Guard (Unconditional)
            if (string.Equals(feedback.style, "recast", StringComparison.OrdinalIgnoreCase) && feedback.hasFeedback)
            {
                if (string.IsNullOrEmpty(feedback.recastText))
                {
                    feedback.recastText = feedback.feedbackText;
                }
                else if (string.IsNullOrEmpty(feedback.feedbackText))
                {
                    feedback.feedbackText = feedback.recastText;
                }

                if (CorrectionTextGuards.ViolatesRecastPurity(feedback.recastText))
                {
                    Debug.LogWarning($"[RealLLMService] Recast purity violation in recastText: {feedback.recastText}");
                    feedback.recastText = BuildMinimalRecast(feedback.correctedText);
                    feedback.feedbackText = feedback.recastText;
                    feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "recast_purity_repaired");
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
                cachedOrchestrator = FindFirstObjectByType<SceneTalkOrchestrator>();
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
                        nonGoalQuestionsAsked = 0;
                        askedNonGoalQuestionIds.Clear();
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
            nonGoalQuestionsAsked = 0;
            askedNonGoalQuestionIds.Clear();
            Debug.Log("[RealLLMService] Chat history and session error history cleared on explicit session reset.");
        }

        #endregion

        #region Send API Requests

        private async Task<string> SendChatRequest(OpenAiMessage[] messages, bool useJsonObject, Action onFirstResponseBytes = null)
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
            var responseHandler = new FirstResponseBytesDownloadHandler(onFirstResponseBytes);
            webRequest.downloadHandler = responseHandler;
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
                    errorMsg += $"\n{responseHandler.Text}";
                }
                throw new Exception(errorMsg);
            }

            return responseHandler.Text;
        }

        private async Task<string> SendChatRequest(string sysPrompt, string userPrompt, bool useJsonObject, Action onFirstResponseBytes = null)
        {
            var messages = new[]
            {
                new OpenAiMessage { role = "system", content = sysPrompt },
                new OpenAiMessage { role = "user", content = userPrompt }
            };
            return await SendChatRequest(messages, useJsonObject, onFirstResponseBytes);
        }

        private sealed class FirstResponseBytesDownloadHandler : DownloadHandlerScript
        {
            private readonly StringBuilder text = new StringBuilder();
            private readonly Action onFirstBytes;
            private bool firstBytesReceived;

            public FirstResponseBytesDownloadHandler(Action onFirstBytes) : base(new byte[16384])
            {
                this.onFirstBytes = onFirstBytes;
            }

            public string Text => text.ToString();

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0) return true;
                if (!firstBytesReceived)
                {
                    firstBytesReceived = true;
                    onFirstBytes?.Invoke();
                }
                text.Append(Encoding.UTF8.GetString(data, 0, dataLength));
                return true;
            }
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
            return GenerateFeedbackFirstStreaming(userText, null, onSentenceComplete, onComplete, onError);
        }

        public IEnumerator GenerateFeedbackFirstStreaming(
            string userText,
            Action<CorrectionFeedbackData> onCorrectionReady,
            Action<string> onSentenceComplete,
            Action<SpringScenePayload> onComplete,
            Action<string> onError)
        {
            Debug.Log($"[RealLLMService] Generating streaming scene and reply for: {userText}");
            LastFirstTokenLatencyMs = -1f;
            LastFirstSentenceLatencyMs = -1f;
            streamStartTime = Time.realtimeSinceStartup;
            RefreshSttMetadata(isStreaming: true);

            CheckAndResetSession();
            var pacingDecision = CreateAvatarDialoguePacingDecision(userText);

            var timing = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            formalDialogueLeakageDetected = false;
            timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionRequestStarted);
            var correctionTask = ParseCorrectionFeedbackAsync(userText);
            timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueRequestStarted);
            var dialogueTask = ParseDialogueContinuationStreamingAsync(userText, pacingDecision, onSentenceComplete);
            var correctionReadyLogged = false;

            while (!correctionTask.IsCompleted || !dialogueTask.IsCompleted)
            {
                if (!correctionReadyLogged && correctionTask.Status == TaskStatus.RanToCompletion)
                {
                    var ready = correctionTask.Result;
                    timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionTextReady, feedbackText: ready?.feedbackText ?? ready?.recastText);
                    onCorrectionReady?.Invoke(ready);
                    correctionReadyLogged = true;
                }
                yield return null;
            }

            var dialogueFailed = dialogueTask.IsFaulted || dialogueTask.IsCanceled;
            var correctionFailed = correctionTask.IsFaulted || correctionTask.IsCanceled;
            if (dialogueFailed || correctionFailed && IsExperimentLocked())
            {
                var ex = correctionFailed
                    ? correctionTask.Exception?.InnerException ?? correctionTask.Exception
                    : dialogueTask.Exception?.InnerException ?? dialogueTask.Exception;
                timing?.MarkTurnTechnicalInvalid(
                    correctionFailed ? "CorrectionPlanner" : "DialogueGenerator",
                    ex?.Message ?? "parallel_stream_failed");
                onError?.Invoke(ex?.Message ?? "Parallel LLM streaming tasks faulted.");
                yield break;
            }

            var payload = dialogueTask.Result;
            var feedback = correctionFailed
                ? BuildCorrectionFallback("correction_generation_failed", correctionTask.Exception)
                : correctionTask.Result;
            if (!correctionReadyLogged)
            {
                timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionTextReady, feedbackText: feedback?.feedbackText ?? feedback?.recastText);
                onCorrectionReady?.Invoke(feedback);
            }
            payload.correctionFeedback = feedback;
            CommitAvatarDialoguePacing(payload, pacingDecision);
            ApplyExperimentConditionToPayload(payload);
            if (formalDialogueLeakageDetected)
            {
                timing?.MarkTurnTechnicalInvalid("DialogueLeakageGuard", "correction_leakage_in_avatar_dialogue");
                onError?.Invoke("Formal turn invalid: correction leakage detected in Avatar dialogue.");
                yield break;
            }

            // Update chat history
            if (chatHistory.Count == 0)
            {
                chatHistory.Clear();
                string rpSysPrompt = BuildRoleplaySystemPrompt(payload);
                chatHistory.Add(new OpenAiMessage { role = "system", content = rpSysPrompt });
                chatHistory.Add(new OpenAiMessage { role = "user", content = userText });
                chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });
            }
            else
            {
                chatHistory.Add(new OpenAiMessage { role = "user", content = userText });
                chatHistory.Add(new OpenAiMessage { role = "assistant", content = payload.dialogueReply });
            }

            onComplete?.Invoke(payload);
        }

        private async Task<SpringScenePayload> ParseDialogueContinuationStreamingAsync(
            string userInput,
            AvatarDialoguePacingDecision pacingDecision,
            Action<string> onSentenceComplete)
        {
            string systemPrompt = BuildAvatarDialogueSystemPrompt(pacingDecision);

            var messagesList = new System.Collections.Generic.List<OpenAiMessage>();
            if (chatHistory.Count == 0)
            {
                messagesList.Add(new OpenAiMessage { role = "system", content = systemPrompt });
                messagesList.Add(new OpenAiMessage { role = "user", content = userInput });
            }
            else
            {
                messagesList.Add(new OpenAiMessage { role = "system", content = systemPrompt });
                for (int i = 1; i < chatHistory.Count; i++)
                {
                    messagesList.Add(chatHistory[i]);
                }
                messagesList.Add(new OpenAiMessage { role = "user", content = userInput });
            }

            var parser = new IncrementalJsonParser();
            bool firstSentence = false;
            var holdForPacingSanitization = pacingDecision?.data?.triggered == true;
            string fullResponse = await SendChatRequestStreaming(messagesList.ToArray(), chunk =>
            {
                var sentences = parser.Feed(chunk);
                foreach (var s in sentences)
                {
                    if (!firstSentence)
                    {
                        firstSentence = true;
                        LastFirstSentenceLatencyMs = (Time.realtimeSinceStartup - streamStartTime) * 1000f;
                        FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include)
                            ?.RecordTimingEvent(ExperimentTimingEventType.DialogueFirstSentenceReady);
                    }
                    if (!holdForPacingSanitization)
                    {
                        onSentenceComplete?.Invoke(s);
                    }
                }
            });

            fullResponse = CleanJsonString(fullResponse);
            var payload = TryParseDialoguePayload(fullResponse);
            if (payload != null)
            {
                if (string.IsNullOrEmpty(payload.dialogueReply) && !string.IsNullOrEmpty(payload.dialogueContinuation))
                {
                    payload.dialogueReply = payload.dialogueContinuation;
                }
                EnsureSelectedQuestionAtEnd(payload, pacingDecision);
                if (holdForPacingSanitization)
                {
                    foreach (var sentence in SplitDialogueSentences(payload.dialogueReply))
                    {
                        onSentenceComplete?.Invoke(sentence);
                    }
                }
            }
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
            bool firstChunkReceived = false;
            webRequest.downloadHandler = new StreamingDownloadHandler(chunk =>
            {
                if (!firstChunkReceived)
                {
                    firstChunkReceived = true;
                    LastFirstTokenLatencyMs = (Time.realtimeSinceStartup - streamStartTime) * 1000f;
                    FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include)
                        ?.RecordTimingEvent(ExperimentTimingEventType.DialogueFirstToken);
                }
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

        private CorrectionFeedbackData BuildCorrectionFallback(string rationaleTag, Exception exception = null)
        {
            if (exception != null)
            {
                var root = exception.InnerException ?? exception;
                Debug.LogWarning($"[RealLLMService] Correction generation fallback: {root.Message}");
            }

            return new CorrectionFeedbackData
            {
                hasFeedback = false,
                provider = currentCondition?.provider ?? "dialogue_avatar",
                style = currentCondition?.style ?? "explicit",
                errorType = "none",
                originalText = "",
                correctedText = "",
                feedbackText = "",
                recastText = "",
                targetSpan = "",
                confidence = 1f,
                rationaleTag = string.IsNullOrWhiteSpace(rationaleTag)
                    ? "correction_unavailable"
                    : rationaleTag
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
                    int keyLen = 15;
                    if (keyIndex == -1)
                    {
                        keyIndex = buffer.IndexOf("\"dialogueContinuation\"");
                        keyLen = 22;
                    }

                    if (keyIndex != -1)
                    {
                        int colonIndex = buffer.IndexOf(':', keyIndex + keyLen);
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
