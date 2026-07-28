using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
        ISceneTalkCancelableBrain,
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
        [SerializeField] private CorrectionPolicySettings correctionPolicy = new CorrectionPolicySettings();

        [Header("Avatar Dialogue Pacing")]
        [Range(0f, 1f)] [SerializeField] private float temperature = 0.7f;
        [Min(0)] [SerializeField] private int maxNonGoalQuestionsPerTask = 3;

        [Header("LLM Reliability")]
        [Min(5)] [SerializeField] private int totalRequestBudgetSeconds = 45;
        [Min(5)] [SerializeField] private int firstAttemptTimeoutSeconds = 30;
        [Min(0)] [SerializeField] private int transientRetryCount = 1;

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
        public CorrectionPolicySettings CorrectionPolicy =>
            CorrectionPolicySettings.CloneNormalized(correctionPolicy);
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
        private CancellationTokenSource activeGenerationCancellation;

        private enum LlmRequestPurpose
        {
            Dialogue,
            Correction,
            Auxiliary
        }

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

        public void ConfigureCorrectionPolicy(CorrectionPolicySettings runtimePolicy)
        {
            correctionPolicy = CorrectionPolicySettings.CloneNormalized(runtimePolicy);
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
            var generationCancellation = BeginGeneration();
            var cancellationToken = generationCancellation.Token;

            RefreshSttMetadata(isStreaming: false);

            CheckAndResetSession();
            formalDialogueLeakageDetected = false;
            var pacingDecision = CreateAvatarDialoguePacingDecision(userText);

            var timing = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionRequestStarted);
            var correctionTask = ParseCorrectionFeedbackAsync(userText, cancellationToken);
            timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueRequestStarted);
            var dialogueTask = ParseDialogueContinuationNonStreamingAsync(userText, pacingDecision, cancellationToken);
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
                CompleteGeneration(generationCancellation);
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
                CompleteGeneration(generationCancellation);
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
            CompleteGeneration(generationCancellation);
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
                "8. Use the latest participant utterance and only the supplied recent participant utterances from the CURRENT GOAL ACTIVATION window when a goal is expressed across multiple turns. Never reuse speech from before this goal became visible.\n" +
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
            var effectiveSensitivity = IsExperimentLocked()
                ? "moderate"
                : FeedbackSensitivity;
            builder.AppendLine("You are an English language tutor analyzing the user's speech in a VR oral practice context.");
            builder.AppendLine("Your ONLY job is to analyze the user's input for clear grammar, vocabulary, or expression errors that are audible in spoken language.");
            builder.AppendLine("Do NOT continue the dialogue, do NOT act as the roleplay character, and do NOT generate any conversational replies.");

            if (currentCondition != null)
            {
                builder.AppendLine("\n=== EXPERIMENT & TASK CONTEXT ===");
                builder.AppendLine($"- scenarioId: {currentCondition.scenarioId}");
                builder.AppendLine($"- feedbackStyle: {currentCondition.style}");
                if (!string.IsNullOrWhiteSpace(currentCondition.task?.context))
                {
                    builder.AppendLine($"- taskContext: {currentCondition.task.context}");
                }
                builder.AppendLine("Use this context only to understand the scene. Private task goals, goal IDs, completion rules, and target phrases are intentionally excluded.");
            }

            var isRecast = currentCondition != null
                           && string.Equals(currentCondition.style, "recast", StringComparison.OrdinalIgnoreCase);

            builder.AppendLine("\n=== FEEDBACK SENSITIVITY ===");
            builder.AppendLine($"- feedbackSensitivity: {effectiveSensitivity}");
            builder.AppendLine("- conservative: correct only severe grammar or vocabulary errors that hinder understanding.");
            builder.AppendLine("- moderate: correct clear grammar errors, vocabulary misuse, and clearly unnatural request or question constructions, even when the intended meaning is understandable.");
            builder.AppendLine("- active: also correct minor slips, awkward phrasing, and informal wording.");

            builder.AppendLine("\n=== LANGUAGE CORRECTION INSTRUCTIONS ===");
            builder.AppendLine("1. Detect at most ONE clear language error in the user's speech. If no clear error, set hasFeedback = false.");
            builder.AppendLine("2. Apply the feedback sensitivity definition above exactly. Under moderate, do not dismiss a clear error merely because the listener can understand the meaning. Ignore only minor repetitions or normal self-corrections.");
            builder.AppendLine("3. Treat the user input as an ASR transcript. Ignore capitalization, punctuation, whitespace, apostrophes, hyphens, quotation marks, and other writing-format differences because they are not audible language errors.");
            builder.AppendLine("4. Do NOT evaluate pronunciation from transcript text. If a possible issue could instead be ASR confusion involving a homophone, personal name, proper noun, or uncertain transcription, set hasFeedback = false.");
            builder.AppendLine("5. Correct only when the proposed correction changes the words actually spoken, a grammatical relationship, or meaning.");
            builder.AppendLine("6. Accept natural conversational ellipsis and concise service phrases. Do NOT expand a correct utterance merely to make it fuller or more formal. Phrases such as 'table for two please', 'I'd like a quiet room', 'don't you have a monthly plan', and 'when is check-out' must receive no feedback when their only differences are writing format or optional conversational wording.");
            builder.AppendLine("7. Detection examples for moderate sensitivity:");
            builder.AppendLine("   * 'I'm asking for you to replace my dish.' is a clear unnatural request construction: set hasFeedback=true and correct it to 'Could you replace my dish?'.");
            builder.AppendLine("   * 'Giving me some recommendations.' is an incorrect request form: set hasFeedback=true and correct it to 'Could you give me some recommendations?'.");
            builder.AppendLine("   * 'How long the replacement will be?' has incorrect direct-question word order: set hasFeedback=true and correct it to 'How long will the replacement be?'.");
            builder.AppendLine("   * 'Can you replace my dish?' is already natural: set hasFeedback=false.");
            builder.AppendLine("   * Do not correct a grammatically natural statement only because its factual content may not match a private task goal.");

            if (isRecast)
            {
                builder.AppendLine("8. Generate unified feedback text under 'recast' style:");
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
                builder.AppendLine("8. Generate unified feedback text under 'explicit' style:");
                builder.AppendLine("   * Both Avatar and Agent MUST use the exact same explicit feedbackText.");
                builder.AppendLine("   * Start directly with the correction rule. You MUST use this exact format: '[one short rule]. Try: \"[correct expression]\".'");
                builder.AppendLine("   * Do NOT add a heading or label such as 'Grammar tip' before the rule.");
                builder.AppendLine("   * Example: 'Use \"really\" before a verb, not \"very.\" Try: \"I really like this furniture.\"'");
                builder.AppendLine("   * You MUST set recastText = \"\".");
            }

            builder.AppendLine("9. Keep the text brief and natural for VR spoken TTS.");
            builder.AppendLine("10. Output ONLY a valid JSON object matching this schema:");
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
                builder.AppendLine("  \"feedbackText\": \"[rule]. Try: \\\"[correct expression]\\\" (explicit style, without a heading or label)\",");
                builder.AppendLine("  \"recastText\": \"\",");
            }
            builder.AppendLine("  \"targetSpan\": \"wrong phrase/word\",");
            builder.AppendLine("  \"confidence\": 1.0,");
            builder.AppendLine("  \"rationaleTag\": \"short tag explanation\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private async Task<CorrectionFeedbackData> ParseCorrectionFeedbackAsync(
            string userInput,
            CancellationToken cancellationToken = default)
        {
            if (ShouldSuppressCorrectionByStt(out var suppressionReason))
            {
                return FinalizeCorrectionFeedback(
                    userInput,
                    BuildCorrectionFallback(suppressionReason));
            }

            string systemPrompt = BuildCorrectionSystemPrompt();
            string responseText = await SendChatRequest(
                systemPrompt,
                userInput,
                true,
                () => FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include)
                    ?.RecordTimingEvent(ExperimentTimingEventType.CorrectionFirstToken),
                cancellationToken,
                LlmRequestPurpose.Correction);

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

            var plannerHasFeedback = feedback.hasFeedback;
            var finalizedFeedback = FinalizeCorrectionFeedback(userInput, feedback);
            var spokenText = string.Equals(
                    finalizedFeedback.style,
                    ExperimentConditionManager.RecastStyle,
                    StringComparison.OrdinalIgnoreCase)
                ? finalizedFeedback.recastText
                : finalizedFeedback.feedbackText;
            Debug.Log(
                $"[RealLLMService] Correction decision - "
                + $"plannerHasFeedback={plannerHasFeedback}, "
                + $"finalHasFeedback={finalizedFeedback.hasFeedback}, "
                + $"spokenTextPresent={!string.IsNullOrWhiteSpace(spokenText)}, "
                + $"rationale={finalizedFeedback.rationaleTag}",
                this);
            return finalizedFeedback;
        }

        private async Task<SpringScenePayload> ParseDialogueContinuationNonStreamingAsync(
            string userInput,
            AvatarDialoguePacingDecision pacingDecision,
            CancellationToken cancellationToken = default)
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

            string responseJson = await SendChatRequest(
                messagesList.ToArray(),
                true,
                null,
                cancellationToken,
                LlmRequestPurpose.Dialogue);
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
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new FormatException("LLM dialogue response was empty.");
            }

            try
            {
                var payload = JsonUtility.FromJson<SpringScenePayload>(content);
                if (payload != null)
                {
                    if (string.IsNullOrEmpty(payload.dialogueReply)
                        && !string.IsNullOrEmpty(payload.dialogueContinuation))
                    {
                        payload.dialogueReply = payload.dialogueContinuation;
                    }

                    EnsureDialogueReplyPresent(payload);
                    EnsurePayloadDefaults(payload);
                    return payload;
                }
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new FormatException(
                    $"LLM dialogue response was not valid SceneTalk JSON: {exception.Message}",
                    exception);
            }

            throw new FormatException("LLM response did not contain a valid SceneTalk payload.");
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
            var policy = CorrectionPolicy;
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
            if (lastSttConfidenceAvailable
                && lastSttConfidence < policy.LowSttConfidenceThreshold)
            {
                builder.AppendLine($"CRITICAL: STT/ASR confidence is below {policy.LowSttConfidenceThreshold:0.##}. Do NOT perform any grammar correction (set hasFeedback = false) because the errors are likely STT recognition failures. Respond politely asking the user to repeat.");
            }
            if (policy.ShortRecordingThresholdMs > 0
                && lastRecordingDurationMs > 0
                && lastRecordingDurationMs < policy.ShortRecordingThresholdMs)
            {
                builder.AppendLine($"CRITICAL: The user recording was too short (under {policy.ShortRecordingThresholdMs}ms), probably a misclick or accidental cancel. Do NOT perform grammar correction (set hasFeedback = false). Respond politely asking the user to repeat.");
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
            builder.AppendLine("     * If provider is 'assistant_agent': Act as an instructor helper. Start directly with the correction rule and do not add a heading or label. You MUST use this exact format: '[one short rule]. Try: \"[correct expression]\".' Example: 'Use \"really\" before a verb, not \"very.\" Try: \"I really like this furniture.\"' Limit the rule explanation to one short, simple sentence (at most 2 sentences total including the recommendation).");
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

        private CorrectionFeedbackData FinalizeCorrectionFeedback(
            string userInput,
            CorrectionFeedbackData feedback)
        {
            if (feedback == null)
            {
                feedback = BuildCorrectionFallback("correction_feedback_missing");
            }

            feedback.provider = currentCondition != null
                                && string.Equals(
                                    currentCondition.provider,
                                    ExperimentConditionManager.AssistantAgentProvider,
                                    StringComparison.OrdinalIgnoreCase)
                ? ExperimentConditionManager.AssistantAgentProvider
                : ExperimentConditionManager.DialogueAvatarProvider;
            feedback.style = currentCondition != null
                             && string.Equals(
                                 currentCondition.style,
                                 ExperimentConditionManager.RecastStyle,
                                 StringComparison.OrdinalIgnoreCase)
                ? ExperimentConditionManager.RecastStyle
                : ExperimentConditionManager.ExplicitStyle;

            if (ShouldSuppressCorrectionByStt(out var sttSuppressionReason))
            {
                ClearCorrectionContent(feedback);
                feedback.rationaleTag = AppendRationale(
                    feedback.rationaleTag,
                    sttSuppressionReason);
                return feedback;
            }

            if (!feedback.hasFeedback)
            {
                ClearCorrectionContent(feedback);
                return feedback;
            }

            var normalizedErrorType = string.IsNullOrWhiteSpace(feedback.errorType)
                ? string.Empty
                : feedback.errorType.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedErrorType) || !ValidErrorTypes.Contains(normalizedErrorType))
            {
                normalizedErrorType = "unknown";
                feedback.rationaleTag = AppendRationale(
                    feedback.rationaleTag,
                    "invalid_error_type_repaired");
            }
            feedback.errorType = normalizedErrorType;

            if (CorrectionPolicyEvaluator.ApplyNonAudibleDifferenceFilter(
                    correctionPolicy,
                    userInput,
                    feedback))
            {
                return feedback;
            }

            if (string.Equals(
                    feedback.style,
                    ExperimentConditionManager.RecastStyle,
                    StringComparison.OrdinalIgnoreCase))
            {
                feedback.recastText = string.IsNullOrWhiteSpace(feedback.recastText)
                    ? feedback.feedbackText
                    : feedback.recastText;
                if (string.IsNullOrWhiteSpace(feedback.recastText))
                {
                    feedback.recastText = BuildMinimalRecast(feedback.correctedText);
                    feedback.rationaleTag = AppendRationale(
                        feedback.rationaleTag,
                        "missing_spoken_feedback_repaired");
                }
                feedback.feedbackText = feedback.recastText;
                if (CorrectionTextGuards.ViolatesRecastPurity(feedback.recastText))
                {
                    feedback.recastText = BuildMinimalRecast(feedback.correctedText);
                    feedback.feedbackText = feedback.recastText;
                    feedback.rationaleTag = AppendRationale(
                        feedback.rationaleTag,
                        "recast_purity_repaired");
                }
            }
            else
            {
                feedback.recastText = string.Empty;
                NormalizeExplicitFeedback(feedback);
            }

            return feedback;
        }

        private static void ClearCorrectionContent(CorrectionFeedbackData feedback)
        {
            feedback.hasFeedback = false;
            feedback.errorType = "none";
            feedback.originalText = string.Empty;
            feedback.correctedText = string.Empty;
            feedback.feedbackText = string.Empty;
            feedback.recastText = string.Empty;
            feedback.targetSpan = string.Empty;
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
            if (isLocked)
            {
                feedbackSensitivity = "moderate";
                correctionPolicy = CorrectionPolicySettings.CloneNormalized(correctionPolicy);
            }
        }

        private bool IsExperimentLocked()
        {
            return isLocked || (currentCondition != null && currentCondition.formalExperiment);
        }

        private bool ShouldSuppressCorrectionByStt(out string sttSuppressionReason)
        {
            sttSuppressionReason = string.Empty;
            var policy = CorrectionPolicy;

            if (policy.ShortRecordingThresholdMs > 0
                && lastRecordingDurationMs > 0
                && lastRecordingDurationMs < policy.ShortRecordingThresholdMs)
            {
                sttSuppressionReason = "short_recording_suppressed";
                return true;
            }

            if (lastSttConfidenceAvailable
                && lastSttConfidence >= 0
                && lastSttConfidence < policy.LowSttConfidenceThreshold)
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
            return "Could you say that again in a complete sentence?";
        }

        private static string BuildMinimalExplicitCorrection(string correctedText)
        {
            if (!string.IsNullOrWhiteSpace(correctedText))
            {
                return $"Use this form. Try: \"{correctedText.Trim()}\".";
            }

            return "Please try that again using a complete, natural English sentence.";
        }

        private static void NormalizeExplicitFeedback(CorrectionFeedbackData feedback)
        {
            if (feedback == null
                || !feedback.hasFeedback
                || !string.Equals(
                    feedback.style,
                    ExperimentConditionManager.ExplicitStyle,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            feedback.feedbackText = CorrectionTextGuards.RemoveGrammarTipPrefix(
                feedback.feedbackText);
            if (!string.IsNullOrWhiteSpace(feedback.feedbackText))
            {
                return;
            }

            feedback.feedbackText = BuildMinimalExplicitCorrection(feedback.correctedText);
            feedback.rationaleTag = AppendRationale(
                feedback.rationaleTag,
                "missing_spoken_feedback_repaired");
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
                feedback.recastText = "";
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

            NormalizeExplicitFeedback(feedback);

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
            CancelActiveGeneration();
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

        public void CancelActiveGeneration()
        {
            var cancellation = activeGenerationCancellation;
            activeGenerationCancellation = null;
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation.Dispose();
        }

        private CancellationTokenSource BeginGeneration()
        {
            CancelActiveGeneration();
            activeGenerationCancellation = new CancellationTokenSource();
            return activeGenerationCancellation;
        }

        private void CompleteGeneration(CancellationTokenSource generationCancellation)
        {
            if (!ReferenceEquals(activeGenerationCancellation, generationCancellation))
            {
                return;
            }

            activeGenerationCancellation = null;
            generationCancellation.Dispose();
        }

        private void OnDestroy()
        {
            CancelActiveGeneration();
        }

        #endregion

        #region Send API Requests

        private async Task<string> SendChatRequest(
            OpenAiMessage[] messages,
            bool useJsonObject,
            Action onFirstResponseBytes = null,
            CancellationToken cancellationToken = default,
            LlmRequestPurpose purpose = LlmRequestPurpose.Auxiliary)
        {
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

            var firstBytesSignaled = 0;
            Action signalFirstBytes = () =>
            {
                if (Interlocked.Exchange(ref firstBytesSignaled, 1) == 0)
                {
                    onFirstResponseBytes?.Invoke();
                }
            };

            return await ExecuteWithRetry(
                timeoutSeconds => SendChatRequestAttempt(
                    jsonBody,
                    timeoutSeconds,
                    signalFirstBytes,
                    cancellationToken),
                purpose,
                cancellationToken);
        }

        private async Task<string> SendChatRequestAttempt(
            string jsonBody,
            int timeoutSeconds,
            Action onFirstResponseBytes,
            CancellationToken cancellationToken)
        {
            var requiresClientApiKey = RequiresClientApiKey(apiUrl);
            string effectiveKey = string.IsNullOrEmpty(apiKey)
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                : apiKey;

            if (requiresClientApiKey && string.IsNullOrEmpty(effectiveKey))
            {
                throw new LlmRequestException("API Key is not set.", 0, false, 0);
            }

            using var webRequest = new UnityWebRequest(apiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            var responseHandler = new FirstResponseBytesDownloadHandler(onFirstResponseBytes);
            webRequest.downloadHandler = responseHandler;
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            if (requiresClientApiKey)
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {effectiveKey}");
            }

            webRequest.timeout = Mathf.Max(1, timeoutSeconds);
            var operation = webRequest.SendWebRequest();
            try
            {
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                webRequest.Abort();
                throw;
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw BuildRequestException(webRequest, responseHandler.Text);
            }

            if (string.IsNullOrWhiteSpace(responseHandler.Text))
            {
                throw new LlmRequestException(
                    "LLM returned HTTP success with an empty response body.",
                    webRequest.responseCode,
                    true,
                    0);
            }

            return responseHandler.Text;
        }

        private async Task<string> SendChatRequest(
            string sysPrompt,
            string userPrompt,
            bool useJsonObject,
            Action onFirstResponseBytes = null,
            CancellationToken cancellationToken = default,
            LlmRequestPurpose purpose = LlmRequestPurpose.Auxiliary)
        {
            var messages = new[]
            {
                new OpenAiMessage { role = "system", content = sysPrompt },
                new OpenAiMessage { role = "user", content = userPrompt }
            };
            return await SendChatRequest(
                messages,
                useJsonObject,
                onFirstResponseBytes,
                cancellationToken,
                purpose);
        }

        private Task<T> ExecuteWithRetry<T>(
            Func<int, Task<T>> requestAttempt,
            LlmRequestPurpose purpose,
            CancellationToken cancellationToken)
        {
            var retryAllowed = purpose == LlmRequestPurpose.Dialogue
                || (purpose == LlmRequestPurpose.Correction && IsExperimentLocked());
            return ExecuteWithRetryCore(
                requestAttempt,
                purpose,
                cancellationToken,
                retryAllowed ? Mathf.Max(0, transientRetryCount) : 0,
                Mathf.Max(5, totalRequestBudgetSeconds),
                Mathf.Max(5, firstAttemptTimeoutSeconds),
                message => Debug.LogWarning(message),
                (delayMilliseconds, token) => Task.Delay(delayMilliseconds, token));
        }

        private static async Task<T> ExecuteWithRetryCore<T>(
            Func<int, Task<T>> requestAttempt,
            LlmRequestPurpose purpose,
            CancellationToken cancellationToken,
            int retryCount,
            int totalBudget,
            int firstAttemptTimeout,
            Action<string> onRetry,
            Func<int, CancellationToken, Task> delayAsync)
        {
            var maxAttempts = 1 + retryCount;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            LlmRequestException lastFailure = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remainingSeconds = totalBudget - (int)Math.Ceiling(timer.Elapsed.TotalSeconds);
                if (remainingSeconds <= 0)
                {
                    break;
                }

                var timeoutSeconds = attempt == 1
                    ? Math.Min(firstAttemptTimeout, remainingSeconds)
                    : remainingSeconds;
                try
                {
                    return await requestAttempt(timeoutSeconds);
                }
                catch (LlmRequestException exception)
                {
                    lastFailure = exception;
                    if (!exception.Retryable || attempt >= maxAttempts)
                    {
                        throw;
                    }

                    var delaySeconds = ResolveRetryDelaySeconds(exception, attempt);
                    var remainingAfterDelay = totalBudget - timer.Elapsed.TotalSeconds - delaySeconds;
                    if (remainingAfterDelay < 2d)
                    {
                        throw;
                    }

                    onRetry?.Invoke(
                        $"[RealLLMService] {purpose} request failed on attempt {attempt}/{maxAttempts}; "
                        + $"retrying in {delaySeconds:0.0}s. {exception.Message}");
                    var delayMilliseconds = Math.Max(
                        1,
                        (int)Math.Round(
                            delaySeconds * 1000f,
                            MidpointRounding.AwayFromZero));
                    await delayAsync(delayMilliseconds, cancellationToken);
                }
            }

            throw lastFailure ?? new LlmRequestException(
                $"LLM {purpose} request exceeded the {totalBudget}s total budget.",
                0,
                false,
                0);
        }

        private static float ResolveRetryDelaySeconds(LlmRequestException exception, int attempt)
        {
            if (exception.RetryAfterSeconds > 0)
            {
                return Math.Min(5f, Math.Max(0.25f, exception.RetryAfterSeconds));
            }

            if (exception.StatusCode == 429)
            {
                return 5f;
            }

            var jitter = Math.Abs(Environment.TickCount % 251) / 1000f;
            return Math.Min(5f, attempt) + jitter;
        }

        private static LlmRequestException BuildRequestException(
            UnityWebRequest webRequest,
            string responseBody)
        {
            var statusCode = webRequest == null ? 0 : webRequest.responseCode;
            var requestError = webRequest == null || string.IsNullOrWhiteSpace(webRequest.error)
                ? "unknown transport error"
                : webRequest.error;
            var retryable = webRequest != null
                && (webRequest.result == UnityWebRequest.Result.ConnectionError
                    || statusCode == 408
                    || statusCode == 429
                    || statusCode >= 500);
            var retryAfterSeconds = 0;
            if (webRequest != null)
            {
                int.TryParse(webRequest.GetResponseHeader("Retry-After"), out retryAfterSeconds);
            }

            var detail = BoundResponseDetail(responseBody);
            var message = $"API Request Failed: {requestError}";
            if (!string.IsNullOrWhiteSpace(detail))
            {
                message += $"\n{detail}";
            }

            return new LlmRequestException(
                message,
                statusCode,
                retryable,
                retryAfterSeconds);
        }

        private static string BoundResponseDetail(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            var normalized = responseBody.Trim();
            const int maxLength = 1024;
            return normalized.Length <= maxLength
                ? normalized
                : normalized.Substring(0, maxLength) + "...";
        }

        private sealed class LlmRequestException : Exception
        {
            public long StatusCode { get; }
            public bool Retryable { get; }
            public int RetryAfterSeconds { get; }

            public LlmRequestException(
                string message,
                long statusCode,
                bool retryable,
                int retryAfterSeconds)
                : base(message)
            {
                StatusCode = statusCode;
                Retryable = retryable;
                RetryAfterSeconds = retryAfterSeconds;
            }
        }

        private sealed class FirstResponseBytesDownloadHandler : DownloadHandlerScript
        {
            private readonly StringBuilder text = new StringBuilder();
            private readonly Action onFirstBytes;
            private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
            private readonly char[] characterBuffer = new char[16384];
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

                AppendDecodedText(data, dataLength, false);
                return true;
            }

            protected override void CompleteContent()
            {
                AppendDecodedText(Array.Empty<byte>(), 0, true);
            }

            private void AppendDecodedText(byte[] data, int dataLength, bool flush)
            {
                var characterCount = decoder.GetChars(
                    data,
                    0,
                    dataLength,
                    characterBuffer,
                    0,
                    flush);
                if (characterCount > 0)
                {
                    text.Append(characterBuffer, 0, characterCount);
                }
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
            var generationCancellation = BeginGeneration();
            var cancellationToken = generationCancellation.Token;
            LastFirstTokenLatencyMs = -1f;
            LastFirstSentenceLatencyMs = -1f;
            streamStartTime = Time.realtimeSinceStartup;
            RefreshSttMetadata(isStreaming: true);

            CheckAndResetSession();
            var pacingDecision = CreateAvatarDialoguePacingDecision(userText);

            var timing = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            formalDialogueLeakageDetected = false;
            timing?.RecordTimingEvent(ExperimentTimingEventType.CorrectionRequestStarted);
            var correctionTask = ParseCorrectionFeedbackAsync(userText, cancellationToken);
            timing?.RecordTimingEvent(ExperimentTimingEventType.DialogueRequestStarted);
            var dialogueTask = ParseDialogueContinuationStreamingAsync(
                userText,
                pacingDecision,
                onSentenceComplete,
                cancellationToken);
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
                CompleteGeneration(generationCancellation);
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
                CompleteGeneration(generationCancellation);
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
            CompleteGeneration(generationCancellation);
        }

        private async Task<SpringScenePayload> ParseDialogueContinuationStreamingAsync(
            string userInput,
            AvatarDialoguePacingDecision pacingDecision,
            Action<string> onSentenceComplete,
            CancellationToken cancellationToken = default)
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
            }, cancellationToken);

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

        private async Task<string> SendChatRequestStreaming(
            OpenAiMessage[] messages,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default)
        {
            var requestBody = new OpenAiRequest
            {
                model = modelName,
                messages = messages,
                response_format = new ResponseFormat { type = "json_object" },
                stream = true
            };
            var jsonBody = JsonUtility.ToJson(requestBody);
            return await ExecuteWithRetry(
                timeoutSeconds => SendChatRequestStreamingAttempt(
                    jsonBody,
                    timeoutSeconds,
                    onChunkReceived,
                    cancellationToken),
                LlmRequestPurpose.Dialogue,
                cancellationToken);
        }

        private async Task<string> SendChatRequestStreamingAttempt(
            string jsonBody,
            int timeoutSeconds,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken)
        {
            string effectiveKey = string.IsNullOrEmpty(apiKey)
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                : apiKey;

            if (RequiresClientApiKey(apiUrl) && string.IsNullOrEmpty(effectiveKey))
            {
                throw new LlmRequestException("API Key is not set.", 0, false, 0);
            }

            using var webRequest = new UnityWebRequest(apiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);

            var fullResponseBuilder = new StringBuilder();
            bool firstChunkReceived = false;
            Action<string> dispatchChunk = chunk =>
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
            };
            var responseHandler = new StreamingDownloadHandler(dispatchChunk);
            webRequest.downloadHandler = responseHandler;

            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "text/event-stream");
            webRequest.SetRequestHeader("Cache-Control", "no-cache");
            if (RequiresClientApiKey(apiUrl))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {effectiveKey}");
            }

            webRequest.timeout = Mathf.Max(1, timeoutSeconds);
            var operation = webRequest.SendWebRequest();
            try
            {
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                webRequest.Abort();
                throw;
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                var failure = BuildRequestException(webRequest, responseHandler.RawText);
                if (fullResponseBuilder.Length > 0)
                {
                    failure = new LlmRequestException(
                        failure.Message + "\nThe failed stream already emitted content; automatic retry was suppressed.",
                        failure.StatusCode,
                        false,
                        failure.RetryAfterSeconds);
                }
                throw failure;
            }

            if (responseHandler.ParseFailureCount > 0)
            {
                Debug.LogWarning(
                    $"[RealLLMService] Streaming response contained {responseHandler.ParseFailureCount} "
                    + $"unparseable SSE event(s). Last error: {responseHandler.LastParseFailure}",
                    this);
            }

            if (fullResponseBuilder.Length == 0)
            {
                var envelopeContent = TryExtractNonStreamingContent(responseHandler.RawText);
                if (!string.IsNullOrWhiteSpace(envelopeContent))
                {
                    Debug.LogWarning(
                        "[RealLLMService] Upstream returned a non-streaming envelope for a streaming request; "
                        + "using choices[0].message.content.",
                        this);
                    dispatchChunk(envelopeContent);
                }
            }

            if (fullResponseBuilder.Length == 0)
            {
                var detail = BoundResponseDetail(responseHandler.RawText);
                throw new LlmRequestException(
                    "LLM returned HTTP success without decodable dialogue content."
                    + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $"\n{detail}"),
                    webRequest.responseCode,
                    true,
                    0);
            }

            return fullResponseBuilder.ToString();
        }

        private static string TryExtractNonStreamingContent(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return string.Empty;
            }

            try
            {
                var response = JsonUtility.FromJson<OpenAiResponse>(rawResponse.Trim());
                return response?.choices != null && response.choices.Length > 0
                    ? response.choices[0].message?.content ?? string.Empty
                    : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
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

        internal sealed class StreamingDownloadHandler : DownloadHandlerScript
        {
            private readonly Action<string> onChunkReceived;
            private readonly StringBuilder lineBuffer = new StringBuilder();
            private readonly StringBuilder rawText = new StringBuilder();
            private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
            private readonly char[] characterBuffer = new char[16384];

            public string RawText => rawText.ToString();
            public int ParsedEventCount { get; private set; }
            public int ParseFailureCount { get; private set; }
            public string LastParseFailure { get; private set; } = string.Empty;

            public StreamingDownloadHandler(Action<string> onChunkReceived) : base(new byte[16384])
            {
                this.onChunkReceived = onChunkReceived;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                {
                    return true;
                }

                var characterCount = decoder.GetChars(
                    data,
                    0,
                    dataLength,
                    characterBuffer,
                    0,
                    false);
                ProcessDecodedText(new string(characterBuffer, 0, characterCount));
                return true;
            }

            protected override void CompleteContent()
            {
                var characterCount = decoder.GetChars(
                    Array.Empty<byte>(),
                    0,
                    0,
                    characterBuffer,
                    0,
                    true);
                if (characterCount > 0)
                {
                    ProcessDecodedText(new string(characterBuffer, 0, characterCount));
                }

                if (lineBuffer.Length > 0)
                {
                    ProcessLine(lineBuffer.ToString());
                    lineBuffer.Clear();
                }
            }

            private void ProcessDecodedText(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                rawText.Append(text);
                lineBuffer.Append(text);

                string fullText = lineBuffer.ToString();
                int lineEnd;
                int lastIndex = 0;

                while ((lineEnd = fullText.IndexOf('\n', lastIndex)) != -1)
                {
                    string line = fullText.Substring(lastIndex, lineEnd - lastIndex);
                    lastIndex = lineEnd + 1;
                    ProcessLine(line);
                }

                if (lastIndex > 0)
                {
                    lineBuffer.Remove(0, lastIndex);
                }
            }

            private void ProcessLine(string rawLine)
            {
                var line = string.IsNullOrEmpty(rawLine) ? string.Empty : rawLine.Trim();
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    return;
                }

                string dataVal = line.Substring(5).Trim();
                if (dataVal == "[DONE]" || string.IsNullOrEmpty(dataVal))
                {
                    return;
                }

                try
                {
                    var chunkJson = JsonUtility.FromJson<OpenAiChunk>(dataVal);
                    if (chunkJson?.choices == null || chunkJson.choices.Length == 0)
                    {
                        return;
                    }

                    ParsedEventCount++;
                    var content = chunkJson.choices[0].delta?.content;
                    if (!string.IsNullOrEmpty(content))
                    {
                        onChunkReceived?.Invoke(content);
                    }
                }
                catch (Exception exception)
                {
                    ParseFailureCount++;
                    LastParseFailure = exception.Message;
                }
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
