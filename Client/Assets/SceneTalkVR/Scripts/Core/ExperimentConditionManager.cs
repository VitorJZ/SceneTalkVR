using System;
using System.Globalization;
using System.IO;
using System.Text;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [DisallowMultipleComponent]
    public sealed class ExperimentConditionManager : MonoBehaviour
    {
        public const string DialogueAvatarProvider = "dialogue_avatar";
        public const string AssistantAgentProvider = "assistant_agent";
        public const string ExplicitStyle = "explicit";
        public const string RecastStyle = "recast";

        public enum ExperimentConditionPreset
        {
            DialogueAvatarExplicit,
            DialogueAvatarRecast,
            AssistantAgentExplicit,
            AssistantAgentRecast
        }

        [Header("Session")]
        [SerializeField] private string participantId = "participant_demo";
        [SerializeField] private string sessionId = "";
        [SerializeField] private bool formalExperiment;
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool showDebugLabel = true;

        [Header("Condition")]
        [SerializeField] private bool useConditionOrder;
        [SerializeField] private ExperimentConditionPreset manualCondition = ExperimentConditionPreset.AssistantAgentExplicit;
        [SerializeField] private string[] conditionOrder =
        {
            "dialogue_avatar_explicit",
            "dialogue_avatar_recast",
            "assistant_agent_explicit",
            "assistant_agent_recast"
        };
        [SerializeField] private int conditionOrderIndex;

        [Header("Scenario")]
        [SerializeField] private string scenarioId = "restaurant_reservation";
        [SerializeField] private int scenarioIndex;
        [SerializeField] private SceneTalkExperimentTask[] taskDefinitions = CreateDefaultTasks();

        [Header("Logging")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private bool writeJsonLines = true;
        [SerializeField] private bool writeCsv = true;
        [SerializeField] private string logFolderName = "SceneTalkVR/ExperimentLogs";

        private CorrectionExperimentCondition currentCondition;
        private ExperimentTurnLogRecord activeTurnLog;
        private ExperimentTurnLogRecord pendingTurnLog;
        private float recordingStartedAt;
        private bool recordingActive;
        private int turnIndex;
        private int queuedRetryCount;

        public CorrectionExperimentCondition CurrentCondition
        {
            get
            {
                if (currentCondition == null)
                {
                    RefreshCondition(false);
                }

                return currentCondition;
            }
        }

        public SceneTalkExperimentTask CurrentTask => CurrentCondition?.task;

        public bool HasActiveTurn => activeTurnLog != null;
        public bool HasPendingTurnReview => pendingTurnLog != null;
        public bool IsExperimentLocked => formalExperiment;
        public string LockedFeedbackSensitivity => IsExperimentLocked ? "moderate" : "moderate";
        public string CurrentConditionId => CurrentCondition?.conditionId ?? string.Empty;
        public string CurrentFeedbackProvider => CurrentCondition?.provider ?? DialogueAvatarProvider;
        public string CurrentFeedbackStyle => CurrentCondition?.style ?? ExplicitStyle;
        public bool CanUseManualRuntimeCondition => !formalExperiment
            && !useConditionOrder
            && !HasActiveTurn
            && !HasPendingTurnReview;
        public string ManualRuntimeConditionLockReason
        {
            get
            {
                if (formalExperiment)
                {
                    return "Locked by formal experiment.";
                }

                if (useConditionOrder)
                {
                    return "Locked by condition order.";
                }

                if (HasActiveTurn || HasPendingTurnReview)
                {
                    return "Available after the current turn.";
                }

                return string.Empty;
            }
        }

        public event Action ExperimentConditionChanged;

        public void NotifyConditionChanged()
        {
            ExperimentConditionChanged?.Invoke();
        }

        public bool IsFormalExperiment => formalExperiment;
        public bool DebugMode => debugMode;
        public bool ShowDebugLabel => debugMode && showDebugLabel && !formalExperiment;

        public string CurrentTurnId
        {
            get
            {
                if (activeTurnLog != null)
                {
                    return activeTurnLog.turnId;
                }

                if (pendingTurnLog != null)
                {
                    return pendingTurnLog.turnId;
                }

                return BuildTurnId(turnIndex);
            }
        }

        public string CurrentDebugLabel
        {
            get
            {
                var condition = CurrentCondition;
                return condition == null
                    ? string.Empty
                    : $"{condition.conditionId} | {ResolveAssistantEmbodiment(condition)} | {condition.scenarioId} | turn {condition.turnIndex}";
            }
        }

        private void Awake()
        {
            EnsureSessionId();
            EnsureDefaultTaskDefinitions();
            RefreshCondition(false);
        }

        private void OnValidate()
        {
            conditionOrderIndex = Mathf.Max(0, conditionOrderIndex);
            scenarioIndex = Mathf.Max(0, scenarioIndex);
            EnsureDefaultTaskDefinitions();
            RefreshCondition(false);
        }

        private void OnDisable()
        {
            RecordUserAction("exit");
        }

        public CorrectionExperimentCondition BeginTurn()
        {
            FlushActiveTurn("skip");
            FlushPendingTurn("continue");

            EnsureSessionId();
            turnIndex++;
            RefreshCondition(true);
            activeTurnLog = CreateTurnLog(CurrentCondition);
            return CloneCondition(CurrentCondition);
        }

        public CorrectionExperimentCondition EnsureActiveTurn()
        {
            if (activeTurnLog == null)
            {
                return BeginTurn();
            }

            RefreshCondition(true);
            return CloneCondition(CurrentCondition);
        }

        public CorrectionExperimentCondition RefreshCondition(bool includeCurrentTurn)
        {
            EnsureDefaultTaskDefinitions();
            EnsureSessionId();

            var conditionId = ResolveCurrentConditionId();
            ResolveCondition(conditionId, out var provider, out var style);
            var resolvedScenarioId = ResolveScenarioId();

            currentCondition = new CorrectionExperimentCondition
            {
                participantId = string.IsNullOrWhiteSpace(participantId) ? "participant_demo" : participantId.Trim(),
                sessionId = sessionId,
                formalExperiment = formalExperiment,
                conditionId = conditionId,
                scenarioId = resolvedScenarioId,
                provider = provider,
                style = style,
                turnIndex = includeCurrentTurn ? turnIndex : Mathf.Max(0, turnIndex),
                conditionOrder = CopyConditionOrder(),
                task = CloneTask(FindTask(resolvedScenarioId))
            };

            return CloneCondition(currentCondition);
        }

        public void AdvanceCondition()
        {
            if (useConditionOrder)
            {
                var order = GetEffectiveConditionOrder();
                conditionOrderIndex = order.Length == 0 ? 0 : (conditionOrderIndex + 1) % order.Length;
                RefreshCondition(false);
                NotifyConditionChanged();
                return;
            }

            var nextCondition = (ExperimentConditionPreset)(((int)manualCondition + 1)
                % Enum.GetValues(typeof(ExperimentConditionPreset)).Length);
            SetManualCondition(nextCondition);
        }

        public bool TrySetManualFeedbackProvider(string provider)
        {
            if (!CanUseManualRuntimeCondition || !TryNormalizeProvider(provider, out var normalizedProvider))
            {
                return false;
            }

            var preset = ResolvePreset(normalizedProvider, CurrentFeedbackStyle);
            return SetManualCondition(preset);
        }

        public bool TrySetManualFeedbackStyle(string style)
        {
            if (!CanUseManualRuntimeCondition || !TryNormalizeStyle(style, out var normalizedStyle))
            {
                return false;
            }

            var preset = ResolvePreset(CurrentFeedbackProvider, normalizedStyle);
            return SetManualCondition(preset);
        }

        public void AdvanceScenario()
        {
            EnsureDefaultTaskDefinitions();
            if (taskDefinitions.Length == 0)
            {
                return;
            }

            scenarioIndex = (scenarioIndex + 1) % taskDefinitions.Length;
            scenarioId = taskDefinitions[scenarioIndex].scenarioId;
            RefreshCondition(false);
            NotifyConditionChanged();
        }

        public void SelectTask(string taskId)
        {
            EnsureDefaultTaskDefinitions();
            for (int i = 0; i < taskDefinitions.Length; i++)
            {
                if (string.Equals(taskDefinitions[i].scenarioId, taskId, StringComparison.OrdinalIgnoreCase))
                {
                    scenarioIndex = i;
                    scenarioId = taskDefinitions[i].scenarioId;
                    break;
                }
            }
            RefreshCondition(false);
            NotifyConditionChanged();
        }

        public void ApplyProviderTo(MonoBehaviour avatarVoiceModule)
        {
            var condition = CurrentCondition;
            if (condition == null || avatarVoiceModule == null)
            {
                return;
            }

            if (avatarVoiceModule is ISceneTalkCorrectionFeedbackProviderReceiver providerReceiver)
            {
                providerReceiver.SetCorrectionFeedbackProvider(condition.provider);
            }
        }

        public void InjectInto(MonoBehaviour brainModule)
        {
            var condition = CurrentCondition;
            if (condition == null || brainModule == null)
            {
                return;
            }

            if (brainModule is ISceneTalkExperimentContextReceiver receiver)
            {
                receiver.SetExperimentCondition(CloneCondition(condition));
            }
        }

        public void BeginRecording()
        {
            if (activeTurnLog == null)
            {
                BeginTurn();
            }

            recordingStartedAt = Time.realtimeSinceStartup;
            recordingActive = true;
        }

        public void CompleteRecording()
        {
            if (!recordingActive)
            {
                return;
            }

            recordingActive = false;
            if (activeTurnLog != null)
            {
                activeTurnLog.recordingDurationMs = Mathf.Max(
                    0,
                    Mathf.RoundToInt((Time.realtimeSinceStartup - recordingStartedAt) * 1000f));
            }
        }

        public void RecordCorrectionPayload(SpringScenePayload payload)
        {
            var log = ResolveWritableTurnLog();
            if (log == null)
            {
                return;
            }

            var condition = CurrentCondition;
            var feedback = payload?.correctionFeedback;
            log.provider = ResolveNonEmpty(feedback?.provider, condition?.provider);
            log.style = ResolveNonEmpty(feedback?.style, condition?.style);
            log.hasFeedback = feedback != null && feedback.hasFeedback;
            log.errorType = feedback == null ? string.Empty : NullToEmpty(feedback.errorType);
            if (!log.hasFeedback)
            {
                log.correctionOutcome = "none";
                log.correctionErrorCode = string.Empty;
            }

            if (payload != null)
            {
                log.dialogueReply = NullToEmpty(payload.dialogueReply);
                log.dialogueContinuation = NullToEmpty(payload.dialogueContinuation);
                if (feedback != null)
                {
                    log.feedbackText = NullToEmpty(feedback.feedbackText);
                    log.recastText = NullToEmpty(feedback.recastText);
                    log.originalText = NullToEmpty(feedback.originalText);
                    log.correctedText = NullToEmpty(feedback.correctedText);
                    log.rationaleTag = NullToEmpty(feedback.rationaleTag);

                    if (!string.IsNullOrEmpty(feedback.rationaleTag))
                    {
                        if (feedback.rationaleTag.Contains("low_confidence_suppressed") || feedback.rationaleTag.Contains("short_recording_suppressed"))
                        {
                            log.sttSuppressionReason = feedback.rationaleTag;
                        }
                    }

                    // Compute validation warnings
                    var warnings = new System.Collections.Generic.List<string>();
                    if (string.Equals(feedback.provider, "assistant_agent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (CorrectionTextGuards.LooksLikeCorrection(payload.dialogueReply))
                        {
                            warnings.Add("dialogue_reply_leakage_detected");
                        }
                    }
                    if (string.Equals(feedback.style, "recast", StringComparison.OrdinalIgnoreCase) && feedback.hasFeedback)
                    {
                        if (CorrectionTextGuards.ViolatesRecastPurity(feedback.feedbackText))
                        {
                            warnings.Add("recast_purity_violated");
                        }
                    }
                    log.validationWarnings = warnings.Count > 0 ? string.Join(";", warnings) : "none";
                }
            }
        }

        public void RecordSpeechMetadata(string transcript, float confidence, string provider, string fallbackLevel, string suppressionReason)
        {
            var log = ResolveWritableTurnLog();
            if (log != null)
            {
                log.transcript = NullToEmpty(transcript);
                log.sttConfidence = confidence;
                log.sttProvider = NullToEmpty(provider);
                log.sttFallbackLevel = NullToEmpty(fallbackLevel);
                if (!string.IsNullOrEmpty(suppressionReason))
                {
                    log.sttSuppressionReason = suppressionReason;
                }
                else if (string.IsNullOrEmpty(log.sttSuppressionReason))
                {
                    log.sttSuppressionReason = "none";
                }
            }
        }

        public void RecordCorrectionPlayback(string provider, string outcome, string errorCode)
        {
            var log = ResolveWritableTurnLog();
            if (log == null)
            {
                return;
            }

            log.provider = ResolveNonEmpty(provider, log.provider);
            log.correctionOutcome = string.IsNullOrWhiteSpace(outcome) ? "unknown" : outcome;
            log.correctionErrorCode = NullToEmpty(errorCode);

            if (log.correctionOutcome.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0
                || !string.IsNullOrWhiteSpace(log.correctionErrorCode))
            {
                log.moduleFallback = ResolveNonEmpty(log.moduleFallback, log.correctionOutcome);
            }
        }

        public void RecordModuleFallback(string moduleFallback)
        {
            if (string.IsNullOrWhiteSpace(moduleFallback))
            {
                return;
            }

            var log = ResolveWritableTurnLog();
            if (log != null)
            {
                log.moduleFallback = AppendToken(log.moduleFallback, moduleFallback);
            }
        }

        public void RecordDetailMetrics(
            string dialogueContinuation,
            string recastText,
            string correctionRequestStartTime,
            string dialogueRequestStartTime,
            string firstTokenTime,
            string firstSentenceTime,
            string ttsReadyTime,
            string correctionPlayStartTime,
            string correctionPlayEndTime,
            string dialoguePlayStartTime,
            string dialoguePlayEndTime,
            string playbackOrder,
            float userEndToFeedbackAudioMs,
            float userEndToDialogueAudioMs,
            float feedbackToDialogueGapMs,
            string correctionVoiceId,
            string actualPlaybackSubject,
            string timeoutReason,
            string fallbackReason,
            string failureReason)
        {
            var log = ResolveWritableTurnLog();
            if (log == null) return;

            log.dialogueContinuation = dialogueContinuation;
            log.recastText = recastText;
            log.correctionRequestStartTime = correctionRequestStartTime;
            log.dialogueRequestStartTime = dialogueRequestStartTime;
            log.firstTokenTime = firstTokenTime;
            log.firstSentenceTime = firstSentenceTime;
            log.ttsReadyTime = ttsReadyTime;
            log.correctionPlayStartTime = correctionPlayStartTime;
            log.correctionPlayEndTime = correctionPlayEndTime;
            log.dialoguePlayStartTime = dialoguePlayStartTime;
            log.dialoguePlayEndTime = dialoguePlayEndTime;
            log.playbackOrder = playbackOrder;
            log.userEndToFeedbackAudioMs = userEndToFeedbackAudioMs;
            log.userEndToDialogueAudioMs = userEndToDialogueAudioMs;
            log.feedbackToDialogueGapMs = feedbackToDialogueGapMs;
            log.correctionVoiceId = correctionVoiceId;
            log.actualPlaybackSubject = actualPlaybackSubject;
            log.timeoutReason = timeoutReason;
            log.fallbackReason = fallbackReason;
            log.failureReason = failureReason;
        }

        public void CompleteActiveTurn()
        {
            if (activeTurnLog == null)
            {
                return;
            }

            activeTurnLog.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(activeTurnLog.correctionOutcome))
            {
                activeTurnLog.correctionOutcome = activeTurnLog.hasFeedback ? "unknown" : "none";
            }

            pendingTurnLog = activeTurnLog;
            activeTurnLog = null;
        }

        public void RecordUserAction(string action)
        {
            var normalizedAction = NormalizeUserAction(action);

            if (pendingTurnLog != null)
            {
                var retryBase = pendingTurnLog.retryCount;
                pendingTurnLog.userAction = normalizedAction;
                WriteTurnLog(pendingTurnLog);

                if (string.Equals(normalizedAction, "try_again", StringComparison.OrdinalIgnoreCase))
                {
                    queuedRetryCount = retryBase + 1;
                }

                pendingTurnLog = null;
                return;
            }

            if (activeTurnLog != null)
            {
                var retryBase = activeTurnLog.retryCount;
                activeTurnLog.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                activeTurnLog.userAction = normalizedAction;
                WriteTurnLog(activeTurnLog);

                if (string.Equals(normalizedAction, "try_again", StringComparison.OrdinalIgnoreCase))
                {
                    queuedRetryCount = retryBase + 1;
                }

                activeTurnLog = null;
            }
        }

        [ContextMenu("Experiment/Next Condition")]
        private void ContextAdvanceCondition()
        {
            AdvanceCondition();
        }

        [ContextMenu("Experiment/Next Scenario")]
        private void ContextAdvanceScenario()
        {
            AdvanceScenario();
        }

        private void FlushPendingTurn(string defaultAction)
        {
            if (pendingTurnLog == null)
            {
                return;
            }

            pendingTurnLog.userAction = NormalizeUserAction(defaultAction);
            WriteTurnLog(pendingTurnLog);
            pendingTurnLog = null;
        }

        private void FlushActiveTurn(string defaultAction)
        {
            if (activeTurnLog == null)
            {
                return;
            }

            activeTurnLog.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            activeTurnLog.userAction = NormalizeUserAction(defaultAction);
            WriteTurnLog(activeTurnLog);
            activeTurnLog = null;
        }

        private ExperimentTurnLogRecord ResolveWritableTurnLog()
        {
            return activeTurnLog ?? pendingTurnLog;
        }

        private ExperimentTurnLogRecord CreateTurnLog(CorrectionExperimentCondition condition)
        {
            var now = DateTime.UtcNow;
            var retryCount = queuedRetryCount;
            queuedRetryCount = 0;

            var config = FindFirstObjectByType<SceneTalkRuntimeConfigApplier>()?.Config;
            bool isFixed = config != null ? config.UseFixedExperimentMode : true;

            return new ExperimentTurnLogRecord
            {
                participantId = condition.participantId,
                sessionId = condition.sessionId,
                conditionId = condition.conditionId,
                scenarioId = condition.scenarioId,
                turnId = BuildTurnId(condition.turnIndex),
                turnIndex = condition.turnIndex,
                provider = condition.provider,
                style = condition.style,
                hasFeedback = false,
                errorType = string.Empty,
                correctionOutcome = "none",
                correctionErrorCode = string.Empty,
                userAction = string.Empty,
                retryCount = retryCount,
                recordingDurationMs = 0,
                moduleFallback = string.Empty,
                timestampUtc = now.ToString("o", CultureInfo.InvariantCulture),
                timestampUnixMs = new DateTimeOffset(now).ToUnixTimeMilliseconds(),
                completedAtUtc = string.Empty,

                // Initialize new fields
                transcript = string.Empty,
                dialogueReply = string.Empty,
                feedbackText = string.Empty,
                originalText = string.Empty,
                correctedText = string.Empty,
                rationaleTag = string.Empty,
                sttConfidence = 1.0f,
                sttProvider = string.Empty,
                sttFallbackLevel = string.Empty,
                sttSuppressionReason = string.Empty,
                conditionOrderPosition = conditionOrderIndex,
                validationWarnings = string.Empty,

                // Fixed Experiment Scenario Mode fields
                selectedTaskId = condition.scenarioId,
                taskName = condition.task != null ? condition.task.scenarioId : condition.scenarioId,
                taskContext = condition.task != null ? condition.task.context : string.Empty,
                taskGoals = (condition.task != null && condition.task.goals != null) ? string.Join(";", condition.task.goals) : string.Empty,
                initialQuestion = condition.task != null ? condition.task.initialQuestion : string.Empty,
                sceneMode = isFixed ? "fixed_panorama" : "generative",
                whetherHolodeckCalled = isFixed ? false : (config != null && config.UseHolodeckBackend),
                panoramaSource = isFixed ? "local" : (config != null && config.ForceFallbackPanorama ? "fallback" : "generated_once"),
                experimentProvider = condition.provider,
                experimentStyle = condition.style,
                assistantEmbodiment = ResolveAssistantEmbodiment(condition)
            };
        }

        private static string ResolveAssistantEmbodiment(CorrectionExperimentCondition condition)
        {
            if (condition == null
                || !string.Equals(
                    condition.provider,
                    AssistantAgentProvider,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "none";
            }

            var presenter = FindFirstObjectByType<CorrectionAgentPresenter>(FindObjectsInactive.Include);
            return presenter != null ? presenter.AppearanceId : "missing";
        }

        private void WriteTurnLog(ExperimentTurnLogRecord record)
        {
            if (!enableLogging || record == null)
            {
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(record.completedAtUtc))
                {
                    record.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                }

                var folder = ResolveLogFolder();
                Directory.CreateDirectory(folder);
                var filePrefix = $"{SanitizeFileToken(record.participantId)}_{SanitizeFileToken(record.sessionId)}";

                if (writeJsonLines)
                {
                    var path = Path.Combine(folder, $"{filePrefix}.jsonl");
                    File.AppendAllText(path, JsonUtility.ToJson(record) + Environment.NewLine, Encoding.UTF8);
                }

                if (writeCsv)
                {
                    var path = Path.Combine(folder, $"{filePrefix}.csv");
                    var shouldWriteHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                    using var writer = new StreamWriter(path, true, Encoding.UTF8);
                    if (shouldWriteHeader)
                    {
                        writer.WriteLine(ExperimentTurnLogRecord.CsvHeader);
                    }

                    writer.WriteLine(record.ToCsvLine());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneTalkVR] Failed to write experiment log: {ex.Message}", this);
            }
        }

        private string ResolveLogFolder()
        {
            var safeFolderName = string.IsNullOrWhiteSpace(logFolderName)
                ? "SceneTalkVR/ExperimentLogs"
                : logFolderName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(Application.persistentDataPath, safeFolderName);
        }

        private string BuildTurnId(int index)
        {
            EnsureSessionId();
            return $"{sessionId}_turn_{Mathf.Max(0, index):000}";
        }

        private void EnsureSessionId()
        {
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = sessionId.Trim();
                return;
            }

            sessionId = $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        private string ResolveCurrentConditionId()
        {
            if (!useConditionOrder)
            {
                return GetConditionId(manualCondition);
            }

            var order = GetEffectiveConditionOrder();
            if (order.Length == 0)
            {
                return GetConditionId(manualCondition);
            }

            var index = Mathf.Clamp(conditionOrderIndex, 0, order.Length - 1);
            conditionOrderIndex = index;
            return NormalizeConditionId(order[index]);
        }

        private string ResolveScenarioId()
        {
            EnsureDefaultTaskDefinitions();

            if (!string.IsNullOrWhiteSpace(scenarioId))
            {
                return scenarioId.Trim();
            }

            if (taskDefinitions.Length == 0)
            {
                return "restaurant_reservation";
            }

            scenarioIndex = Mathf.Clamp(scenarioIndex, 0, taskDefinitions.Length - 1);
            return taskDefinitions[scenarioIndex].scenarioId;
        }

        private SceneTalkExperimentTask FindTask(string id)
        {
            EnsureDefaultTaskDefinitions();

            for (var i = 0; i < taskDefinitions.Length; i++)
            {
                var task = taskDefinitions[i];
                if (task != null && string.Equals(task.scenarioId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return task;
                }
            }

            return taskDefinitions.Length > 0 ? taskDefinitions[0] : CreateDefaultTasks()[0];
        }

        private void EnsureDefaultTaskDefinitions()
        {
            if (taskDefinitions != null && taskDefinitions.Length > 0)
            {
                return;
            }

            taskDefinitions = CreateDefaultTasks();
        }

        private string[] GetEffectiveConditionOrder()
        {
            if (conditionOrder == null || conditionOrder.Length == 0)
            {
                return new[]
                {
                    "dialogue_avatar_explicit",
                    "dialogue_avatar_recast",
                    "assistant_agent_explicit",
                    "assistant_agent_recast"
                };
            }

            return conditionOrder;
        }

        private string[] CopyConditionOrder()
        {
            var order = GetEffectiveConditionOrder();
            var copy = new string[order.Length];
            for (var i = 0; i < order.Length; i++)
            {
                copy[i] = NormalizeConditionId(order[i]);
            }

            return copy;
        }

        private static string GetConditionId(ExperimentConditionPreset preset)
        {
            return preset switch
            {
                ExperimentConditionPreset.DialogueAvatarRecast => "dialogue_avatar_recast",
                ExperimentConditionPreset.AssistantAgentExplicit => "assistant_agent_explicit",
                ExperimentConditionPreset.AssistantAgentRecast => "assistant_agent_recast",
                _ => "dialogue_avatar_explicit"
            };
        }

        private bool SetManualCondition(ExperimentConditionPreset preset)
        {
            var changed = manualCondition != preset;
            manualCondition = preset;
            RefreshCondition(false);

            if (changed)
            {
                Debug.Log(
                    $"[ExperimentConditionManager] Runtime correction condition changed: "
                    + $"conditionId={CurrentConditionId}, provider={CurrentFeedbackProvider}, "
                    + $"style={CurrentFeedbackStyle}, applies=next_turn",
                    this);
                NotifyConditionChanged();
            }

            return true;
        }

        private static ExperimentConditionPreset ResolvePreset(string provider, string style)
        {
            var useAssistant = string.Equals(
                provider,
                AssistantAgentProvider,
                StringComparison.OrdinalIgnoreCase);
            var useRecast = string.Equals(style, RecastStyle, StringComparison.OrdinalIgnoreCase);

            if (useAssistant)
            {
                return useRecast
                    ? ExperimentConditionPreset.AssistantAgentRecast
                    : ExperimentConditionPreset.AssistantAgentExplicit;
            }

            return useRecast
                ? ExperimentConditionPreset.DialogueAvatarRecast
                : ExperimentConditionPreset.DialogueAvatarExplicit;
        }

        private static bool TryNormalizeProvider(string provider, out string normalizedProvider)
        {
            if (string.Equals(provider, DialogueAvatarProvider, StringComparison.OrdinalIgnoreCase))
            {
                normalizedProvider = DialogueAvatarProvider;
                return true;
            }

            if (string.Equals(provider, AssistantAgentProvider, StringComparison.OrdinalIgnoreCase))
            {
                normalizedProvider = AssistantAgentProvider;
                return true;
            }

            normalizedProvider = string.Empty;
            return false;
        }

        private static bool TryNormalizeStyle(string style, out string normalizedStyle)
        {
            if (string.Equals(style, ExplicitStyle, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStyle = ExplicitStyle;
                return true;
            }

            if (string.Equals(style, RecastStyle, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStyle = RecastStyle;
                return true;
            }

            normalizedStyle = string.Empty;
            return false;
        }

        private static string NormalizeConditionId(string conditionId)
        {
            var value = string.IsNullOrWhiteSpace(conditionId)
                ? "dialogue_avatar_explicit"
                : conditionId.Trim().ToLowerInvariant()
                    .Replace(" ", "_")
                    .Replace("+", "_")
                    .Replace("-", "_");

            if (value.Contains("assistant") && value.Contains("recast"))
            {
                return "assistant_agent_recast";
            }

            if (value.Contains("assistant"))
            {
                return "assistant_agent_explicit";
            }

            if (value.Contains("recast"))
            {
                return "dialogue_avatar_recast";
            }

            return "dialogue_avatar_explicit";
        }

        private static void ResolveCondition(string conditionId, out string provider, out string style)
        {
            var normalized = NormalizeConditionId(conditionId);
            provider = normalized.StartsWith("assistant_agent", StringComparison.OrdinalIgnoreCase)
                ? AssistantAgentProvider
                : DialogueAvatarProvider;
            style = normalized.EndsWith("recast", StringComparison.OrdinalIgnoreCase)
                ? RecastStyle
                : ExplicitStyle;
        }

        public static CorrectionExperimentCondition CloneCondition(CorrectionExperimentCondition source)
        {
            if (source == null)
            {
                return null;
            }

            return new CorrectionExperimentCondition
            {
                participantId = source.participantId,
                sessionId = source.sessionId,
                formalExperiment = source.formalExperiment,
                conditionId = source.conditionId,
                scenarioId = source.scenarioId,
                provider = source.provider,
                style = source.style,
                turnIndex = source.turnIndex,
                conditionOrder = CopyStringArray(source.conditionOrder),
                task = CloneTask(source.task)
            };
        }

        public static SceneTalkExperimentTask CloneTask(SceneTalkExperimentTask source)
        {
            if (source == null)
            {
                return null;
            }

            return new SceneTalkExperimentTask
            {
                scenarioId = source.scenarioId,
                context = source.context,
                goals = CopyStringArray(source.goals),
                initialQuestion = source.initialQuestion,
                fallbackEnvironmentType = source.fallbackEnvironmentType,
                fallbackAvatarRole = source.fallbackAvatarRole,
                fallbackAvatarGenderPresentation = source.fallbackAvatarGenderPresentation,
                fallbackAvatarAttitude = source.fallbackAvatarAttitude,
                fallbackSkyboxUrl = source.fallbackSkyboxUrl,
                fallbackLayoutObjects = CopyLayoutObjects(source.fallbackLayoutObjects)
            };
        }

        private static string[] CopyStringArray(string[] values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static LayoutObjectData[] CopyLayoutObjects(LayoutObjectData[] values)
        {
            if (values == null)
            {
                return Array.Empty<LayoutObjectData>();
            }

            var copy = new LayoutObjectData[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var item = values[i];
                copy[i] = item == null
                    ? null
                    : new LayoutObjectData
                    {
                        prefabKey = item.prefabKey,
                        position = item.position,
                        rotationY = item.rotationY
                    };
            }

            return copy;
        }

        private static SceneTalkExperimentTask[] CreateDefaultTasks()
        {
            return new[]
            {
                CreateTask(
                    "restaurant_reservation",
                    "Calling an Italian restaurant to reserve a table for a small celebration.",
                    new[] { "Reserve a table for 5 people", "Ask if a quiet corner table is available", "Ask whether you can bring a small cake", "Ask about parking nearby" },
                    "Hello! Thank you for calling. How can I help you today?",
                    "restaurant",
                    "barista",
                    "demo://restaurant-360",
                    new[]
                    {
                        Layout("generic_table", new Vector3(0.55f, 0f, 1.35f), 18f),
                        Layout("generic_chair", new Vector3(-0.45f, 0f, 1.2f), -12f)
                    }),
                CreateTask(
                    "furniture_shopping",
                    "Speaking with a salesperson at a furniture store to buy a desk.",
                    new[] { "Describe the desk size or style you want", "Ask about available colors", "Ask whether delivery is available this week", "Ask about discounts or promotions" },
                    "Hi! Welcome to HomeSpace. What kind of furniture are you looking for today?",
                    "furniture_store",
                    "clerk",
                    "demo://furniture-store-360",
                    new[]
                    {
                        Layout("generic_table", new Vector3(0.65f, 0f, 1.25f), 8f),
                        Layout("generic_chair", new Vector3(-0.55f, 0f, 1.25f), -16f)
                    }),
                CreateTask(
                    "gym_membership",
                    "Visiting a gym and asking about membership options.",
                    new[] { "Ask about the monthly membership price", "Ask whether there is a student discount", "Ask about opening hours", "Ask if you can try one class first" },
                    "Hi! Welcome to FitZone. What would you like to know about our gym?",
                    "gym",
                    "instructor",
                    "demo://gym-360",
                    new[]
                    {
                        Layout("generic_table", new Vector3(0.6f, 0f, 1.3f), 12f),
                        Layout("plant", new Vector3(-0.65f, 0f, 1.45f), -8f)
                    }),
                CreateTask(
                    "hotel_check_in",
                    "Checking in at a hotel and confirming room details.",
                    new[] { "Confirm your reservation", "Ask whether breakfast is included", "Ask if the room is quiet", "Ask about check-out time" },
                    "Good afternoon! Welcome to the hotel. How may I help you today?",
                    "hotel_lobby",
                    "clerk",
                    "demo://hotel-lobby-360",
                    new[]
                    {
                        Layout("generic_table", new Vector3(0.7f, 0f, 1.28f), 4f),
                        Layout("generic_chair", new Vector3(-0.55f, 0f, 1.35f), -10f)
                    })
            };
        }

        private static SceneTalkExperimentTask CreateTask(
            string id,
            string context,
            string[] goals,
            string initialQuestion,
            string environmentType,
            string fallbackRole,
            string skyboxUrl,
            LayoutObjectData[] layoutObjects)
        {
            return new SceneTalkExperimentTask
            {
                scenarioId = id,
                context = context,
                goals = goals,
                initialQuestion = initialQuestion,
                fallbackEnvironmentType = environmentType,
                fallbackAvatarRole = fallbackRole,
                fallbackAvatarGenderPresentation = "unknown",
                fallbackAvatarAttitude = "helpful",
                fallbackSkyboxUrl = skyboxUrl,
                fallbackLayoutObjects = layoutObjects
            };
        }

        private static LayoutObjectData Layout(string prefabKey, Vector3 position, float rotationY)
        {
            return new LayoutObjectData
            {
                prefabKey = prefabKey,
                position = position,
                rotationY = rotationY
            };
        }

        private static string ResolveNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? NullToEmpty(fallback) : value;
        }

        private static string NullToEmpty(string value)
        {
            return value ?? string.Empty;
        }

        private static string AppendToken(string current, string next)
        {
            if (string.IsNullOrWhiteSpace(next))
            {
                return current ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(current) ? next : $"{current}+{next}";
        }

        private static string NormalizeUserAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return "continue";
            }

            var normalized = action.Trim().ToLowerInvariant();
            return normalized switch
            {
                "tryagain" => "try_again",
                "retry" => "try_again",
                "continue" => "continue",
                "skip" => "skip",
                "exit" => "exit",
                _ => normalized
            };
        }

        private static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return builder.ToString();
        }

        [Serializable]
        private sealed class ExperimentTurnLogRecord
        {
            public string participantId;
            public string sessionId;
            public string conditionId;
            public string scenarioId;
            public string turnId;
            public int turnIndex;
            public string provider;
            public string style;
            public bool hasFeedback;
            public string errorType;
            public string correctionOutcome;
            public string correctionErrorCode;
            public string userAction;
            public int retryCount;
            public int recordingDurationMs;
            public string moduleFallback;
            public string timestampUtc;
            public long timestampUnixMs;
            public string completedAtUtc;

            // New fields
            public string transcript;
            public string dialogueReply;
            public string feedbackText;
            public string originalText;
            public string correctedText;
            public string rationaleTag;
            public float sttConfidence;
            public string sttProvider;
            public string sttFallbackLevel;
            public string sttSuppressionReason;
            public int conditionOrderPosition;
            public string validationWarnings;

            // Fixed Experiment Scenario Mode fields
            public string selectedTaskId;
            public string taskName;
            public string taskContext;
            public string taskGoals;
            public string initialQuestion;
            public string sceneMode;
            public bool whetherHolodeckCalled;
            public string panoramaSource;
            public string experimentProvider;
            public string experimentStyle;
            public string assistantEmbodiment;

            // New design requirements fields
            public string dialogueContinuation;
            public string recastText;
            public string correctionRequestStartTime;
            public string dialogueRequestStartTime;
            public string firstTokenTime;
            public string firstSentenceTime;
            public string ttsReadyTime;
            public string correctionPlayStartTime;
            public string correctionPlayEndTime;
            public string dialoguePlayStartTime;
            public string dialoguePlayEndTime;
            public string playbackOrder;
            public float userEndToFeedbackAudioMs;
            public float userEndToDialogueAudioMs;
            public float feedbackToDialogueGapMs;
            public string correctionVoiceId;
            public string actualPlaybackSubject;
            public string timeoutReason;
            public string fallbackReason;
            public string failureReason;

            public const string CsvHeader =
                "participantId,sessionId,conditionId,scenarioId,turnId,turnIndex,provider,style,hasFeedback,errorType,correctionOutcome,correctionErrorCode,userAction,retryCount,recordingDurationMs,moduleFallback,timestampUtc,timestampUnixMs,completedAtUtc,transcript,dialogueReply,feedbackText,originalText,correctedText,rationaleTag,sttConfidence,sttProvider,sttFallbackLevel,sttSuppressionReason,conditionOrderPosition,validationWarnings,selectedTaskId,taskName,taskContext,taskGoals,initialQuestion,sceneMode,whetherHolodeckCalled,panoramaSource,experimentProvider,experimentStyle,dialogueContinuation,recastText,correctionRequestStartTime,dialogueRequestStartTime,firstTokenTime,firstSentenceTime,ttsReadyTime,correctionPlayStartTime,correctionPlayEndTime,dialoguePlayStartTime,dialoguePlayEndTime,playbackOrder,userEndToFeedbackAudioMs,userEndToDialogueAudioMs,feedbackToDialogueGapMs,correctionVoiceId,actualPlaybackSubject,timeoutReason,fallbackReason,failureReason,assistantEmbodiment";

            public string ToCsvLine()
            {
                return string.Join(
                    ",",
                    Csv(participantId),
                    Csv(sessionId),
                    Csv(conditionId),
                    Csv(scenarioId),
                    Csv(turnId),
                    turnIndex.ToString(CultureInfo.InvariantCulture),
                    Csv(provider),
                    Csv(style),
                    hasFeedback ? "true" : "false",
                    Csv(errorType),
                    Csv(correctionOutcome),
                    Csv(correctionErrorCode),
                    Csv(userAction),
                    retryCount.ToString(CultureInfo.InvariantCulture),
                    recordingDurationMs.ToString(CultureInfo.InvariantCulture),
                    Csv(moduleFallback),
                    Csv(timestampUtc),
                    timestampUnixMs.ToString(CultureInfo.InvariantCulture),
                    Csv(completedAtUtc),
                    Csv(transcript),
                    Csv(dialogueReply),
                    Csv(feedbackText),
                    Csv(originalText),
                    Csv(correctedText),
                    Csv(rationaleTag),
                    sttConfidence.ToString("F4", CultureInfo.InvariantCulture),
                    Csv(sttProvider),
                    Csv(sttFallbackLevel),
                    Csv(sttSuppressionReason),
                    conditionOrderPosition.ToString(CultureInfo.InvariantCulture),
                    Csv(validationWarnings),
                    Csv(selectedTaskId),
                    Csv(taskName),
                    Csv(taskContext),
                    Csv(taskGoals),
                    Csv(initialQuestion),
                    Csv(sceneMode),
                    whetherHolodeckCalled ? "true" : "false",
                    Csv(panoramaSource),
                    Csv(experimentProvider),
                    Csv(experimentStyle),
                    Csv(dialogueContinuation),
                    Csv(recastText),
                    Csv(correctionRequestStartTime),
                    Csv(dialogueRequestStartTime),
                    Csv(firstTokenTime),
                    Csv(firstSentenceTime),
                    Csv(ttsReadyTime),
                    Csv(correctionPlayStartTime),
                    Csv(correctionPlayEndTime),
                    Csv(dialoguePlayStartTime),
                    Csv(dialoguePlayEndTime),
                    Csv(playbackOrder),
                    userEndToFeedbackAudioMs.ToString("F2", CultureInfo.InvariantCulture),
                    userEndToDialogueAudioMs.ToString("F2", CultureInfo.InvariantCulture),
                    feedbackToDialogueGapMs.ToString("F2", CultureInfo.InvariantCulture),
                    Csv(correctionVoiceId),
                    Csv(actualPlaybackSubject),
                    Csv(timeoutReason),
                    Csv(fallbackReason),
                    Csv(failureReason),
                    Csv(assistantEmbodiment));
            }

            private static string Csv(string value)
            {
                value ??= string.Empty;
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
        }
    }
}
