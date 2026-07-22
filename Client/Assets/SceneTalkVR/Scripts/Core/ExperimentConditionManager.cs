using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        public const string NoAssistantEmbodiment = "none";
        public const string AudioOnlyAssistantEmbodiment = "audio_only";
        public const string OrbAssistantEmbodiment = "orb";
        public const string HumanoidAssistantEmbodiment = "humanoid";

        public enum AssistantEmbodimentPreset
        {
            [InspectorName("Voice Only | God Voice")]
            AudioOnly,
            [InspectorName("Little Orb")]
            SmallObject,
            [InspectorName("Third Person | Humanoid")]
            ThirdPerson
        }

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
        [SerializeField] private ExperimentV11ProtocolConfig experimentProtocol;
        [SerializeField] private ExperimentBuildInfo experimentBuildInfo;
        [SerializeField] private ExperimentTaskCatalog taskCatalog;
        [SerializeField] private QuestionnaireCatalog questionnaireCatalog;
        [SerializeField] private PilotPresentationCatalog pilotPresentationCatalog;
        [SerializeField] private ExperimentVoiceProfileCatalog voiceProfileCatalog;
        [SerializeField] private ExperimentDeploymentCatalog deploymentCatalog;
        [SerializeField] private ExperimentDeploymentProfileId deploymentProfile = ExperimentDeploymentProfileId.DevelopmentEditor;

        [Header("Condition")]
        [SerializeField] private bool useConditionOrder;
        [SerializeField] private ExperimentConditionPreset manualCondition = ExperimentConditionPreset.AssistantAgentExplicit;
        [SerializeField] private AssistantEmbodimentPreset manualAssistantEmbodiment = AssistantEmbodimentPreset.SmallObject;
        [SerializeField] private FormalConditionCode formalCondition = FormalConditionCode.NE;
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
        private SceneTalkExperimentTask restoredTaskOverride;
        private string restoredConditionIdOverride;
        private bool assignmentConditionActive;
        private readonly ExperimentEventTimeline eventTimeline = new ExperimentEventTimeline();
        private long eventTurnStartedTicks;

        public IReadOnlyList<ExperimentTimingEvent> ActiveTimingEvents => eventTimeline.Events;

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
        public string CurrentAssistantEmbodiment => CurrentCondition?.assistantEmbodiment ?? NoAssistantEmbodiment;
        public string ConfiguredAssistantEmbodiment => GetAssistantEmbodimentId(manualAssistantEmbodiment);
        public bool CanUseManualRuntimeCondition => !formalExperiment
            && !useConditionOrder
            && !HasActiveTurn
            && !HasPendingTurnReview;
        public bool CanUseManualAssistantEmbodiment => CanUseManualRuntimeCondition
            && string.Equals(
                CurrentFeedbackProvider,
                AssistantAgentProvider,
                StringComparison.OrdinalIgnoreCase);
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
        public ExperimentV11ProtocolConfig ExperimentProtocol => experimentProtocol;
        public ExperimentBuildInfo ExperimentBuildInfo => experimentBuildInfo;
        public ExperimentTaskCatalog TaskCatalog => taskCatalog;
        public QuestionnaireCatalog QuestionnaireCatalog => questionnaireCatalog;
        public PilotPresentationCatalog PilotPresentationCatalog => pilotPresentationCatalog;
        public ExperimentVoiceProfileCatalog VoiceProfileCatalog => voiceProfileCatalog;
        public ExperimentDeploymentCatalog DeploymentCatalog => deploymentCatalog;
        public ExperimentDeploymentProfileId DeploymentProfile => deploymentProfile;
        public FormalConditionCode CurrentFormalCondition => formalExperiment ? formalCondition : LegacyToFormal(CurrentCondition?.conditionId);
        public int CurrentTurnIndex => turnIndex;
        public ExperimentLifecycleCoordinator LifecycleCoordinator => GetComponent<ExperimentLifecycleCoordinator>();

        public void EnterEditorCollectionMode(ExperimentV11ProtocolConfig protocol, ExperimentTaskCatalog tasks,
            QuestionnaireCatalog questionnaires, ExperimentVoiceProfileCatalog voices,
            ExperimentDeploymentCatalog deployments)
        {
            experimentProtocol = protocol;
            taskCatalog = tasks;
            questionnaireCatalog = questionnaires;
            voiceProfileCatalog = voices;
            deploymentCatalog = deployments;
            deploymentProfile = ExperimentDeploymentProfileId.EditorCollection;
            formalExperiment = true;
            debugMode = false;
            showDebugLabel = false;
            assignmentConditionActive = false;
            RefreshCondition(false);
        }

        public bool ValidateFormalProtocol(out string error)
        {
            if (!formalExperiment)
            {
                error = string.Empty;
                return true;
            }

            if (experimentProtocol == null)
            {
                error = "Formal Mode requires an ExperimentV11ProtocolConfig asset.";
                return false;
            }

            if (experimentBuildInfo == null)
            {
                error = "Formal Mode requires an ExperimentBuildInfo asset.";
                return false;
            }
            if (taskCatalog == null) { error = "Formal Mode requires an ExperimentTaskCatalog asset."; return false; }
            if (!taskCatalog.ValidateFormal(experimentProtocol, out error)) return false;
            if (questionnaireCatalog == null) { error = "Formal Mode requires a QuestionnaireCatalog asset."; return false; }
            if (!questionnaireCatalog.ValidateFormal(experimentProtocol, out error)) return false;
            if (voiceProfileCatalog == null) { error = "Formal Mode requires an ExperimentVoiceProfileCatalog asset."; return false; }
            var dialogueVoiceKeys = new List<string>(); foreach (var task in taskCatalog.GetTasks(ExperimentTaskPhase.Formal)) dialogueVoiceKeys.Add(task.voiceProfileKey);
            if (!voiceProfileCatalog.ValidateForLockedCollection(dialogueVoiceKeys, out error)) return false;
            if (deploymentCatalog == null) { error = "Formal Mode requires an ExperimentDeploymentCatalog asset."; return false; }
            if (!deploymentCatalog.ValidateForCollection(deploymentProfile, out error)) return false;
            return experimentProtocol.ValidateForFormalMode(out error);
        }

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
                    : $"{condition.conditionId} | {condition.assistantEmbodiment} | {condition.scenarioId} | turn {condition.turnIndex}";
            }
        }

        private void Awake()
        {
            if (GetComponent<StructuredLlmGoalEvaluationFallback>() == null)
                gameObject.AddComponent<StructuredLlmGoalEvaluationFallback>();
            if (GetComponent<ExperimentLifecycleCoordinator>() == null)
            {
                gameObject.AddComponent<ExperimentLifecycleCoordinator>();
            }
            if (GetComponent<QuestionnaireRuntimeController>() == null)
            {
                gameObject.AddComponent<QuestionnaireRuntimeController>();
            }
            if (GetComponent<QuestionnaireVrPanel>() == null)
            {
                gameObject.AddComponent<QuestionnaireVrPanel>();
            }
            if (GetComponent<FormalRankingVrPanel>() == null)
            {
                gameObject.AddComponent<FormalRankingVrPanel>();
            }
            if (GetComponent<EditorCollectionSessionCoordinator>() == null)
            {
                gameObject.AddComponent<EditorCollectionSessionCoordinator>();
            }
            if (GetComponent<PilotWorkflowCoordinator>() == null)
            {
                gameObject.AddComponent<PilotWorkflowCoordinator>();
            }
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
            eventTimeline.Reset();
            eventTurnStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            RecordTimingEvent(ExperimentTimingEventType.DialogueGateClosed);
            return CloneCondition(CurrentCondition);
        }

        public void StartConversation(string conversationSessionId, string taskId)
        {
            ResetConversationRuntimeState();
            sessionId = string.IsNullOrWhiteSpace(conversationSessionId)
                ? $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}"
                : conversationSessionId.Trim();
            restoredTaskOverride = null;
            restoredConditionIdOverride = string.Empty;
            SetScenario(taskId);
            RefreshCondition(false);
            NotifyConditionChanged();
        }

        public bool RestoreConversation(CorrectionExperimentCondition condition, int completedTurnCount)
        {
            if (formalExperiment || condition == null || string.IsNullOrWhiteSpace(condition.sessionId))
            {
                return false;
            }

            ResetConversationRuntimeState();
            sessionId = condition.sessionId.Trim();
            participantId = string.IsNullOrWhiteSpace(condition.participantId)
                ? participantId
                : condition.participantId.Trim();
            turnIndex = Mathf.Max(0, completedTurnCount);
            manualCondition = ResolvePreset(condition.provider, condition.style);
            restoredTaskOverride = CloneTask(condition.task);
            restoredConditionIdOverride = string.IsNullOrWhiteSpace(condition.conditionId)
                ? string.Empty
                : NormalizeConditionId(condition.conditionId);
            SetScenario(condition.scenarioId);
            RefreshCondition(false);
            NotifyConditionChanged();
            return true;
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

            var conditionId = formalExperiment || assignmentConditionActive ? FormalConditionResolver.ToLegacyConditionId(formalCondition) : ResolveCurrentConditionId();
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
                assistantEmbodiment = string.Equals(
                    provider,
                    AssistantAgentProvider,
                    StringComparison.OrdinalIgnoreCase)
                    ? ConfiguredAssistantEmbodiment
                    : NoAssistantEmbodiment,
                turnIndex = includeCurrentTurn ? turnIndex : Mathf.Max(0, turnIndex),
                conditionOrder = CopyConditionOrder(),
                task = CloneTask(restoredTaskOverride != null
                    && string.Equals(restoredTaskOverride.scenarioId, resolvedScenarioId, StringComparison.OrdinalIgnoreCase)
                    ? restoredTaskOverride
                    : FindTask(resolvedScenarioId))
            };

            return CloneCondition(currentCondition);
        }

        public void AdvanceCondition()
        {
            if (formalExperiment) { Debug.LogWarning("[Experiment] Formal Mode rejects inspector/debug condition changes.", this); return; }
            if (useConditionOrder)
            {
                restoredConditionIdOverride = string.Empty;
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

        public bool TrySetManualAssistantEmbodiment(string embodiment)
        {
            if (!CanUseManualAssistantEmbodiment
                || !TryNormalizeAssistantEmbodiment(embodiment, out var normalizedEmbodiment))
            {
                return false;
            }

            var nextPreset = ResolveAssistantEmbodimentPreset(normalizedEmbodiment);
            var changed = manualAssistantEmbodiment != nextPreset;
            manualAssistantEmbodiment = nextPreset;
            RefreshCondition(false);

            if (changed)
            {
                Debug.Log(
                    $"[ExperimentConditionManager] Runtime correction assistant appearance changed: "
                    + $"assistantEmbodiment={CurrentAssistantEmbodiment}, applies=next_turn",
                    this);
                NotifyConditionChanged();
            }

            return true;
        }

        public void AdvanceScenario()
        {
            if (formalExperiment) { Debug.LogWarning("[Experiment] Formal Mode rejects scene/task changes.", this); return; }
            if (taskCatalog != null)
            {
                var rehearsalPilot = RehearsalSessionCoordinator.Active != null && RehearsalSessionCoordinator.Active.IsPilot;
                var editorPilotDemo = EditorDemoSessionCoordinator.Active != null && EditorDemoSessionCoordinator.Active.IsPilotDemo;
                var pilotCollection = PilotCollectionSessionCoordinator.Active != null && PilotCollectionSessionCoordinator.Active.IsArmed;
                var phase = rehearsalPilot || editorPilotDemo || pilotCollection || experimentProtocol != null && experimentProtocol.ExperimentPhase == ExperimentPhase.Pilot
                    ? ExperimentTaskPhase.Pilot
                    : ExperimentTaskPhase.Formal;
                var tasks = taskCatalog.GetTasks(phase);
                if (tasks.Count == 0) return;
                var currentIndex = 0;
                for (var i = 0; i < tasks.Count; i++)
                {
                    if (string.Equals(tasks[i].taskId, scenarioId, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = i;
                        break;
                    }
                }
                scenarioId = tasks[(currentIndex + 1) % tasks.Count].taskId;
                RefreshCondition(false);
                NotifyConditionChanged();
                return;
            }

            EnsureDefaultTaskDefinitions();
            if (taskDefinitions.Length == 0)
            {
                return;
            }

            restoredTaskOverride = null;
            scenarioIndex = (scenarioIndex + 1) % taskDefinitions.Length;
            scenarioId = taskDefinitions[scenarioIndex].scenarioId;
            RefreshCondition(false);
            NotifyConditionChanged();
        }

        public void SelectTask(string taskId)
        {
            if (formalExperiment)
            {
                Debug.LogWarning("[Experiment] Formal Mode rejects scene/task changes outside an assignment.", this);
                return;
            }

            restoredTaskOverride = null;
            if (taskCatalog != null)
            {
                var definition = taskCatalog.Find(taskId);
                if (definition == null)
                {
                    Debug.LogError($"[Experiment] Task Catalog does not contain '{taskId}'.", this);
                    return;
                }
                scenarioId = definition.taskId;
                RefreshCondition(false);
                NotifyConditionChanged();
                return;
            }

            SetScenario(taskId);
            RefreshCondition(false);
            NotifyConditionChanged();
        }

        private void SetScenario(string taskId)
        {
            EnsureDefaultTaskDefinitions();
            for (int i = 0; i < taskDefinitions.Length; i++)
            {
                if (string.Equals(taskDefinitions[i].scenarioId, taskId, StringComparison.OrdinalIgnoreCase))
                {
                    scenarioIndex = i;
                    scenarioId = taskDefinitions[i].scenarioId;
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(taskId))
            {
                scenarioId = taskId.Trim();
            }
        }

        private void ResetConversationRuntimeState()
        {
            FlushActiveTurn("skip");
            FlushPendingTurn("continue");
            recordingActive = false;
            queuedRetryCount = 0;
            turnIndex = 0;
            activeTurnLog = null;
            pendingTurnLog = null;
        }

        public bool LoadAssignedTask(string taskId, out string error)
        {
            return LoadAssignedTask(taskId, ExperimentTaskPhase.Formal, formalExperiment, out error);
        }

        private bool LoadAssignedTask(string taskId, ExperimentTaskPhase expectedPhase, bool enforceExpectedPhase, out string error)
        {
            error = string.Empty;
            if (taskCatalog == null)
            {
                error = "Experiment Task Catalog is not bound.";
                return false;
            }

            var definition = taskCatalog.Find(taskId);
            if (definition == null)
            {
                error = $"Task Catalog does not contain assigned task '{taskId}'.";
                return false;
            }

            if (enforceExpectedPhase && definition.phase != expectedPhase)
            {
                error = $"{expectedPhase} flow rejects task '{taskId}' with phase '{definition.phase}'.";
                return false;
            }

            scenarioId = definition.taskId;
            RefreshCondition(false);
            NotifyConditionChanged();
            return true;
        }

        public bool ApplyFormalAssignment(FormalConditionCode code, string taskId, out string error, string assignedParticipantId = null, string assignedSessionId = null)
        {
            error = string.Empty;
            if (!Enum.IsDefined(typeof(FormalConditionCode), code)) { error = "Invalid formal condition code."; return false; }
            if (formalExperiment && !ValidateFormalProtocol(out error)) return false;
            if (!LoadAssignedTask(taskId, out error)) return false;
            if (!string.IsNullOrWhiteSpace(assignedParticipantId)) participantId = assignedParticipantId.Trim();
            if (!string.IsNullOrWhiteSpace(assignedSessionId)) sessionId = assignedSessionId.Trim();
            formalCondition = code;
            assignmentConditionActive = true;
            RefreshCondition(false);
            NotifyConditionChanged();
            return true;
        }

        public bool ApplyPilotAssignment(PilotFeedbackStyleChoice style, string taskId, string assignedParticipantId, string assignedSessionId, out string error)
        {
            if (style == PilotFeedbackStyleChoice.Undefined) { error = "pilot_feedback_style_unconfirmed"; return false; }
            if (!LoadAssignedTask(taskId, ExperimentTaskPhase.Pilot, true, out error)) return false;
            formalCondition = style == PilotFeedbackStyleChoice.Recast ? FormalConditionCode.SR : FormalConditionCode.SE;
            assignmentConditionActive = true;
            if (!string.IsNullOrWhiteSpace(assignedParticipantId)) participantId = assignedParticipantId.Trim();
            if (!string.IsNullOrWhiteSpace(assignedSessionId)) sessionId = assignedSessionId.Trim();
            RefreshCondition(false); NotifyConditionChanged(); error = string.Empty; return true;
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

        public void ApplyAssistantEmbodimentTo(MonoBehaviour avatarVoiceModule)
        {
            if (!string.Equals(
                    CurrentFeedbackProvider,
                    AssistantAgentProvider,
                    StringComparison.OrdinalIgnoreCase)
                || avatarVoiceModule == null)
            {
                return;
            }

            if (avatarVoiceModule is ISceneTalkCorrectionAssistantEmbodimentReceiver embodimentReceiver)
            {
                embodimentReceiver.SetCorrectionAssistantEmbodiment(ConfiguredAssistantEmbodiment);
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

        public void RecordAvatarResolution(string resolvedKey, string fallbackLevel)
        {
            var log = ResolveWritableTurnLog();
            if (log == null) return;
            log.resolvedAvatarPresetKey = NullToEmpty(resolvedKey);
            log.avatarFallbackLevel = NullToEmpty(fallbackLevel);
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

            RecordTimingEvent(ExperimentTimingEventType.TurnCompleted);
            var timing = eventTimeline.CalculateSummary();
            activeTurnLog.userEndToFeedbackAudioMs = timing.userEndToFeedbackAudioMs;
            activeTurnLog.userEndToDialogueAudioMs = timing.userEndToDialogueAudioMs;
            activeTurnLog.feedbackToDialogueGapMs = timing.feedbackToDialogueGapMs;
            var lifecycle = LifecycleCoordinator;
            activeTurnLog.completedGoalCount = lifecycle?.GoalTracker?.ConfirmedCount ?? 0;
            activeTurnLog.totalGoalCount = lifecycle?.GoalTracker?.Goals?.Count ?? 0;
            activeTurnLog.taskCompletionRate = lifecycle?.GoalTracker?.GetCompletionRate() ?? 0f;
            activeTurnLog.turnsToCompletion = lifecycle?.TurnsToCompletion ?? 0;
            activeTurnLog.completionReason = lifecycle?.CompletionReason ?? string.Empty;

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
            var lifecycle = LifecycleCoordinator;
            var studyAssignment = lifecycle?.Assignment;
            var conditionAssignment = lifecycle?.CurrentConditionAssignment;
            var editorDemo = EditorDemoSessionCoordinator.Active;
            var rehearsal = RehearsalSessionCoordinator.Active;
            var collection = EditorCollectionSessionCoordinator.Active;

            return new ExperimentTurnLogRecord
            {
                protocolVersion = collection != null && collection.IsArmed ? collection.Protocol.ProtocolVersion : rehearsal != null && rehearsal.IsActive ? rehearsal.Protocol.ProtocolVersion : editorDemo != null && editorDemo.IsDemoMode ? editorDemo.DemoProtocol.DemoProtocolVersion : experimentProtocol == null ? string.Empty : experimentProtocol.ProtocolVersion,
                buildVersion = experimentBuildInfo == null ? (experimentProtocol == null ? string.Empty : experimentProtocol.BuildVersion) : experimentBuildInfo.BuildVersion,
                gitCommit = experimentBuildInfo == null ? string.Empty : experimentBuildInfo.GitCommit,
                activeBranch = experimentBuildInfo == null ? string.Empty : experimentBuildInfo.ActiveBranch,
                experimentPhase = experimentProtocol == null ? string.Empty : experimentProtocol.ExperimentPhase.ToString(),
                formalModeLocked = experimentProtocol != null && experimentProtocol.FormalModeLocked,
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
                taskCatalogVersion = taskCatalog == null ? string.Empty : taskCatalog.CatalogVersion,
                taskId = condition.task != null ? condition.task.taskId : condition.scenarioId,
                taskPhase = condition.task != null ? condition.task.taskPhase : string.Empty,
                taskName = condition.task != null ? condition.task.displayName : condition.scenarioId,
                taskContext = condition.task != null ? condition.task.context : string.Empty,
                taskGoals = (condition.task != null && condition.task.goals != null) ? string.Join(";", condition.task.goals) : string.Empty,
                initialQuestion = condition.task != null ? condition.task.initialQuestion : string.Empty,
                sceneMode = isFixed ? "fixed_panorama" : "generative",
                whetherHolodeckCalled = isFixed ? false : (config != null && config.UseHolodeckBackend),
                panoramaSource = isFixed ? "local" : (config != null && config.ForceFallbackPanorama ? "fallback" : "generated_once"),
                panoramaResourceKey = condition.task != null ? condition.task.panoramaResourceKey : string.Empty,
                avatarPresetKey = collection != null && collection.IsArmed ? collection.ResolveFormalAvatarKey(condition.task?.taskId) : editorDemo != null && editorDemo.IsFormalDemo ? editorDemo.ResolveFormalAvatarKey(condition.task?.taskId) : condition.task != null ? condition.task.avatarPresetKey : string.Empty,
                resolvedAvatarPresetKey = string.Empty,
                avatarFallbackLevel = condition.task != null && condition.task.developerPlaceholderAvatar ? "developer_placeholder_pending" : string.Empty,
                voiceProfileKey = collection != null && collection.IsArmed ? "editor_collection_dialogue_voice" : rehearsal != null && rehearsal.IsActive
                    ? "rehearsal_dialogue_voice"
                    : condition.task != null ? condition.task.voiceProfileKey : string.Empty,
                whetherImageGenerationCalled = !isFixed,
                experimentProvider = condition.provider,
                experimentStyle = condition.style,
                assistantEmbodiment = condition.assistantEmbodiment,
                sequenceId = studyAssignment?.sequenceId ?? string.Empty,
                conditionRunId = lifecycle?.ConditionRunId ?? string.Empty,
                taskAssignmentId = conditionAssignment?.task?.taskAssignmentId ?? string.Empty,
                assignmentVersion = studyAssignment?.assignmentVersion ?? string.Empty,
                questionnaireLinkageKey = lifecycle?.QuestionnaireLinkageKey ?? string.Empty,
                completedGoalCount = lifecycle?.GoalTracker?.ConfirmedCount ?? 0,
                totalGoalCount = lifecycle?.GoalTracker?.Goals?.Count ?? 0,
                taskCompletionRate = lifecycle?.GoalTracker?.GetCompletionRate() ?? 0f,
                turnsToCompletion = lifecycle?.TurnsToCompletion ?? 0,
                completionReason = lifecycle?.CompletionReason ?? string.Empty,
                runtimeMode = collection != null && collection.IsArmed ? ExperimentRuntimeMode.EditorCollectionFormal.ToString() : editorDemo != null && editorDemo.IsDemoMode ? editorDemo.RuntimeMode.ToString() : formalExperiment ? ExperimentRuntimeMode.LockedFormalCollection.ToString() : ExperimentRuntimeMode.DeveloperManual.ToString(),
                dataOrigin = rehearsal != null && rehearsal.IsActive ? "rehearsal" : editorDemo != null && editorDemo.IsDemoMode ? "editor_demo" : studyAssignment?.dataOrigin ?? string.Empty,
                collectionEligible = rehearsal != null && rehearsal.IsActive ? false : editorDemo != null && editorDemo.IsDemoMode ? false : studyAssignment?.collectionEligible ?? false,
                developerTestAssignment = rehearsal != null && rehearsal.IsActive ? false : editorDemo != null && editorDemo.IsDemoMode || (studyAssignment?.developerTestAssignment ?? false),
                demoMode = editorDemo != null && editorDemo.IsDemoMode,
                demoProtocolVersion = editorDemo?.DemoProtocol?.DemoProtocolVersion ?? string.Empty,
                flowMode = rehearsal?.RuntimeContext?.flowMode.ToString() ?? studyAssignment?.flowMode.ToString() ?? string.Empty,
                runQualification = rehearsal?.RuntimeContext?.qualification.ToString() ?? studyAssignment?.runQualification.ToString() ?? string.Empty,
                protocolSnapshotId = rehearsal?.RuntimeContext?.protocolSnapshotId ?? studyAssignment?.protocolSnapshotId ?? string.Empty,
                resourceSnapshotId = rehearsal?.RuntimeContext?.resourceSnapshotId ?? studyAssignment?.resourceSnapshotId ?? string.Empty
            };
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
            if (PilotCollectionSessionCoordinator.Active != null && PilotCollectionSessionCoordinator.Active.IsArmed)
                return PilotCollectionSessionCoordinator.Active.CurrentDataFolder;
            if (EditorCollectionSessionCoordinator.Active != null && EditorCollectionSessionCoordinator.Active.IsArmed)
                return EditorCollectionSessionCoordinator.Active.CurrentDataFolder;
            if (RehearsalSessionCoordinator.Active != null && RehearsalSessionCoordinator.Active.IsActive)
                return RehearsalSessionCoordinator.Active.CurrentDataFolder;
            if (EditorDemoSessionCoordinator.Active != null && EditorDemoSessionCoordinator.Active.IsDemoMode)
                return EditorDemoSessionCoordinator.Active.CurrentDataFolder;
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
            if (!string.IsNullOrWhiteSpace(restoredConditionIdOverride))
            {
                return restoredConditionIdOverride;
            }

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
            if (taskCatalog != null)
            {
                var rehearsalPilot = RehearsalSessionCoordinator.Active != null && RehearsalSessionCoordinator.Active.IsPilot;
                var editorPilotDemo = EditorDemoSessionCoordinator.Active != null && EditorDemoSessionCoordinator.Active.IsPilotDemo;
                var pilotCollection = PilotCollectionSessionCoordinator.Active != null && PilotCollectionSessionCoordinator.Active.IsArmed;
                var phase = rehearsalPilot || editorPilotDemo || pilotCollection || experimentProtocol != null && experimentProtocol.ExperimentPhase == ExperimentPhase.Pilot
                    ? ExperimentTaskPhase.Pilot
                    : ExperimentTaskPhase.Formal;
                var requested = string.IsNullOrWhiteSpace(scenarioId) ? null : taskCatalog.Find(scenarioId.Trim());
                if (requested != null && requested.phase == phase) return requested.taskId;
                var tasks = taskCatalog.GetTasks(phase);
                return tasks.Count > 0 ? tasks[0].taskId : string.Empty;
            }

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
            var catalogTask = taskCatalog == null ? null : taskCatalog.Find(id);
            if (catalogTask != null)
            {
                return CreateRuntimeTask(catalogTask);
            }
            if (taskCatalog != null) return null;
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

        private static SceneTalkExperimentTask CreateRuntimeTask(ExperimentTaskDefinition definition)
        {
            var goals = definition.goals == null
                ? Array.Empty<string>()
                : Array.ConvertAll(definition.goals, goal => goal == null ? string.Empty : goal.text);
            var separator = definition.panoramaResourceKey == null ? -1 : definition.panoramaResourceKey.LastIndexOf('/');
            var panoramaName = string.IsNullOrWhiteSpace(definition.panoramaResourceKey)
                ? string.Empty
                : definition.panoramaResourceKey.Substring(separator + 1);

            return new SceneTalkExperimentTask
            {
                taskId = definition.taskId,
                scenarioId = definition.scenarioId,
                displayName = definition.displayName,
                taskPhase = definition.phase.ToString(),
                context = definition.context,
                goals = goals,
                initialQuestion = definition.initialQuestion,
                fallbackEnvironmentType = definition.environmentType,
                fallbackAvatarRole = definition.avatarRole,
                fallbackAvatarGenderPresentation = "unknown",
                fallbackAvatarAttitude = "helpful",
                fallbackSkyboxUrl = string.IsNullOrWhiteSpace(panoramaName) ? string.Empty : "demo://" + panoramaName,
                panoramaResourceKey = definition.panoramaResourceKey,
                avatarPresetKey = definition.avatarPresetKey,
                voiceProfileKey = definition.voiceProfileKey,
                roleplayPrompt = definition.roleplayPrompt,
                spawnPosition = definition.spawnPosition,
                spawnRotation = definition.spawnRotation,
                developerPlaceholderAvatar = definition.developerPlaceholderAvatar,
                fallbackLayoutObjects = Array.Empty<LayoutObjectData>()
            };
        }

        private void EnsureDefaultTaskDefinitions()
        {
            if (taskCatalog != null)
            {
                taskDefinitions = Array.Empty<SceneTalkExperimentTask>();
                return;
            }
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
            var previousConditionId = ResolveCurrentConditionId();
            manualCondition = preset;
            restoredConditionIdOverride = string.Empty;
            RefreshCondition(false);
            var changed = !string.Equals(
                previousConditionId,
                CurrentConditionId,
                StringComparison.OrdinalIgnoreCase);

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

        private static bool TryNormalizeAssistantEmbodiment(
            string embodiment,
            out string normalizedEmbodiment)
        {
            if (string.Equals(embodiment, AudioOnlyAssistantEmbodiment, StringComparison.OrdinalIgnoreCase))
            {
                normalizedEmbodiment = AudioOnlyAssistantEmbodiment;
                return true;
            }

            if (string.Equals(embodiment, OrbAssistantEmbodiment, StringComparison.OrdinalIgnoreCase))
            {
                normalizedEmbodiment = OrbAssistantEmbodiment;
                return true;
            }

            if (string.Equals(embodiment, HumanoidAssistantEmbodiment, StringComparison.OrdinalIgnoreCase))
            {
                normalizedEmbodiment = HumanoidAssistantEmbodiment;
                return true;
            }

            normalizedEmbodiment = string.Empty;
            return false;
        }

        private static string GetAssistantEmbodimentId(AssistantEmbodimentPreset preset)
        {
            return preset switch
            {
                AssistantEmbodimentPreset.AudioOnly => AudioOnlyAssistantEmbodiment,
                AssistantEmbodimentPreset.ThirdPerson => HumanoidAssistantEmbodiment,
                _ => OrbAssistantEmbodiment
            };
        }

        private static AssistantEmbodimentPreset ResolveAssistantEmbodimentPreset(string embodiment)
        {
            if (string.Equals(embodiment, AudioOnlyAssistantEmbodiment, StringComparison.OrdinalIgnoreCase))
            {
                return AssistantEmbodimentPreset.AudioOnly;
            }

            return string.Equals(embodiment, HumanoidAssistantEmbodiment, StringComparison.OrdinalIgnoreCase)
                ? AssistantEmbodimentPreset.ThirdPerson
                : AssistantEmbodimentPreset.SmallObject;
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

        private static FormalConditionCode LegacyToFormal(string conditionId)
        {
            var id = NormalizeConditionId(conditionId);
            return id == "dialogue_avatar_recast" ? FormalConditionCode.NR
                : id == "assistant_agent_explicit" ? FormalConditionCode.SE
                : id == "assistant_agent_recast" ? FormalConditionCode.SR : FormalConditionCode.NE;
        }

        /// <summary>Boundary reset for a future allocator. It deliberately never chooses a new assignment.</summary>
        public void ResetConditionSessionBoundary()
        {
            FlushActiveTurn("reset");
            FlushPendingTurn("reset");
            recordingActive = false;
            recordingStartedAt = 0f;
            turnIndex = 0;
            queuedRetryCount = 0;
            activeTurnLog = null;
            pendingTurnLog = null;
            currentCondition = null;
            assignmentConditionActive = false;
            scenarioId = string.Empty;
            eventTimeline.Reset();
            eventTurnStartedTicks = 0;
            foreach (var module in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (module is ISceneTalkSessionReset reset) reset.ResetSession();
            RefreshCondition(false);
            NotifyConditionChanged();
        }

        public ExperimentTimingEvent RecordTimingEvent(
            ExperimentTimingEventType eventType,
            string reason = "",
            string failureStage = "",
            ExperimentTechnicalValidity validity = ExperimentTechnicalValidity.Valid,
            string actualPlaybackActor = "",
            string voiceProfile = "",
            string speakingSpeed = "",
            float volume = 1f,
            string subtitlePolicy = "dialogue_only",
            string feedbackText = "",
            string fallback = "")
        {
            if (activeTurnLog == null) return null;
            if (eventTurnStartedTicks == 0) eventTurnStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - eventTurnStartedTicks;
            var elapsedMs = (long)(elapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency);
            var record = eventTimeline.Add(new ExperimentTimingEvent
            {
                timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                monotonicElapsedMs = elapsedMs,
                participantId = activeTurnLog.participantId,
                sessionId = activeTurnLog.sessionId,
                turnId = activeTurnLog.turnId,
                turnIndex = activeTurnLog.turnIndex,
                condition = CurrentFormalCondition.ToString(),
                provider = activeTurnLog.provider,
                style = activeTurnLog.style,
                taskId = activeTurnLog.taskId,
                eventType = eventType.ToString(),
                technicalValidity = validity.ToString(),
                failureStage = failureStage ?? string.Empty,
                reason = reason ?? string.Empty,
                actualPlaybackActor = actualPlaybackActor ?? string.Empty,
                voiceProfile = voiceProfile ?? string.Empty,
                speakingSpeed = speakingSpeed ?? string.Empty,
                volume = volume,
                subtitlePolicy = subtitlePolicy ?? string.Empty,
                feedbackTextHash = string.IsNullOrEmpty(feedbackText) ? string.Empty : ExperimentEventTimeline.HashText(feedbackText),
                fallback = fallback ?? string.Empty
            });
            var pilot = PilotWorkflowCoordinator.Active;
            if (pilot != null && pilot.HasActivePilotRun)
            {
                record.embodimentCondition = PilotProtocolValues.Label(pilot.CurrentEmbodiment);
                record.pilotRunId = pilot.PilotRunId;
                pilot.ObserveTimingEvent(record);
            }
            WriteTimingEvent(record);
            return record;
        }

        public void MarkTurnTechnicalInvalid(string failureStage, string reason)
        {
            var log = ResolveWritableTurnLog();
            if (log != null)
            {
                log.failureReason = reason ?? string.Empty;
                log.timeoutReason = (reason ?? string.Empty).IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ? reason : string.Empty;
            }
            RecordTimingEvent(ExperimentTimingEventType.TurnTechnicalInvalid, reason, failureStage, ExperimentTechnicalValidity.TechnicalInvalid);
            LifecycleCoordinator?.MarkTechnicalInvalid(reason);
        }

        private void WriteTimingEvent(ExperimentTimingEvent record)
        {
            if (!enableLogging || !writeJsonLines || record == null) return;
            try
            {
                var folder = ResolveLogFolder();
                Directory.CreateDirectory(folder);
                var filePrefix = PilotCollectionSessionCoordinator.Active?.IsArmed == true ? "pilot_timing" : $"{SanitizeFileToken(record.participantId)}_{SanitizeFileToken(record.sessionId)}";
                File.AppendAllText(Path.Combine(folder, $"{filePrefix}_events_v1.jsonl"), JsonUtility.ToJson(record) + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneTalkVR] Failed to write timing event: {ex.Message}", this);
            }
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
                assistantEmbodiment = source.assistantEmbodiment,
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
                taskId = source.taskId,
                scenarioId = source.scenarioId,
                displayName = source.displayName,
                taskPhase = source.taskPhase,
                context = source.context,
                goals = CopyStringArray(source.goals),
                initialQuestion = source.initialQuestion,
                fallbackEnvironmentType = source.fallbackEnvironmentType,
                fallbackAvatarRole = source.fallbackAvatarRole,
                fallbackAvatarGenderPresentation = source.fallbackAvatarGenderPresentation,
                fallbackAvatarAttitude = source.fallbackAvatarAttitude,
                fallbackSkyboxUrl = source.fallbackSkyboxUrl,
                panoramaResourceKey = source.panoramaResourceKey,
                avatarPresetKey = source.avatarPresetKey,
                voiceProfileKey = source.voiceProfileKey,
                roleplayPrompt = source.roleplayPrompt,
                spawnPosition = source.spawnPosition,
                spawnRotation = source.spawnRotation,
                developerPlaceholderAvatar = source.developerPlaceholderAvatar,
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
            public string protocolVersion;
            public string buildVersion;
            public string gitCommit;
            public string activeBranch;
            public string experimentPhase;
            public bool formalModeLocked;
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
            public string taskCatalogVersion;
            public string taskId;
            public string taskPhase;
            public string taskName;
            public string taskContext;
            public string taskGoals;
            public string initialQuestion;
            public string sceneMode;
            public bool whetherHolodeckCalled;
            public string panoramaSource;
            public string panoramaResourceKey;
            public string avatarPresetKey;
            public string resolvedAvatarPresetKey;
            public string avatarFallbackLevel;
            public string voiceProfileKey;
            public bool whetherImageGenerationCalled;
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
            public string sequenceId;
            public string conditionRunId;
            public string taskAssignmentId;
            public string assignmentVersion;
            public string questionnaireLinkageKey;
            public int completedGoalCount;
            public int totalGoalCount;
            public float taskCompletionRate;
            public int turnsToCompletion;
            public string completionReason;
            public string runtimeMode;
            public string dataOrigin;
            public bool collectionEligible;
            public bool developerTestAssignment;
            public bool demoMode;
            public string demoProtocolVersion;
            public string flowMode;
            public string runQualification;
            public string protocolSnapshotId;
            public string resourceSnapshotId;

            public const string CsvHeader =
                "protocolVersion,buildVersion,gitCommit,activeBranch,experimentPhase,formalModeLocked,participantId,sessionId,conditionId,scenarioId,turnId,turnIndex,provider,style,hasFeedback,errorType,correctionOutcome,correctionErrorCode,userAction,retryCount,recordingDurationMs,moduleFallback,timestampUtc,timestampUnixMs,completedAtUtc,transcript,dialogueReply,feedbackText,originalText,correctedText,rationaleTag,sttConfidence,sttProvider,sttFallbackLevel,sttSuppressionReason,conditionOrderPosition,validationWarnings,selectedTaskId,taskCatalogVersion,taskId,taskPhase,taskName,taskContext,taskGoals,initialQuestion,sceneMode,whetherHolodeckCalled,whetherImageGenerationCalled,panoramaResourceKey,panoramaSource,avatarPresetKey,resolvedAvatarPresetKey,avatarFallbackLevel,voiceProfileKey,experimentProvider,experimentStyle,dialogueContinuation,recastText,correctionRequestStartTime,dialogueRequestStartTime,firstTokenTime,firstSentenceTime,ttsReadyTime,correctionPlayStartTime,correctionPlayEndTime,dialoguePlayStartTime,dialoguePlayEndTime,playbackOrder,userEndToFeedbackAudioMs,userEndToDialogueAudioMs,feedbackToDialogueGapMs,correctionVoiceId,actualPlaybackSubject,timeoutReason,fallbackReason,failureReason,assistantEmbodiment,sequenceId,conditionRunId,taskAssignmentId,assignmentVersion,questionnaireLinkageKey,completedGoalCount,totalGoalCount,taskCompletionRate,turnsToCompletion,completionReason,runtimeMode,dataOrigin,collectionEligible,developerTestAssignment,demoMode,demoProtocolVersion,flowMode,runQualification,protocolSnapshotId,resourceSnapshotId";

            public string ToCsvLine()
            {
                return string.Join(
                    ",",
                    Csv(protocolVersion),
                    Csv(buildVersion),
                    Csv(gitCommit),
                    Csv(activeBranch),
                    Csv(experimentPhase),
                    formalModeLocked ? "true" : "false",
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
                    Csv(taskCatalogVersion),
                    Csv(taskId),
                    Csv(taskPhase),
                    Csv(taskName),
                    Csv(taskContext),
                    Csv(taskGoals),
                    Csv(initialQuestion),
                    Csv(sceneMode),
                    whetherHolodeckCalled ? "true" : "false",
                    whetherImageGenerationCalled ? "true" : "false",
                    Csv(panoramaResourceKey),
                    Csv(panoramaSource),
                    Csv(avatarPresetKey),
                    Csv(resolvedAvatarPresetKey),
                    Csv(avatarFallbackLevel),
                    Csv(voiceProfileKey),
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
                    Csv(assistantEmbodiment),
                    Csv(sequenceId), Csv(conditionRunId), Csv(taskAssignmentId), Csv(assignmentVersion), Csv(questionnaireLinkageKey),
                    completedGoalCount.ToString(CultureInfo.InvariantCulture), totalGoalCount.ToString(CultureInfo.InvariantCulture),
                    taskCompletionRate.ToString("F4", CultureInfo.InvariantCulture), turnsToCompletion.ToString(CultureInfo.InvariantCulture), Csv(completionReason),
                    Csv(runtimeMode), Csv(dataOrigin), collectionEligible ? "true" : "false",
                    developerTestAssignment ? "true" : "false", demoMode ? "true" : "false", Csv(demoProtocolVersion),
                    Csv(flowMode), Csv(runQualification), Csv(protocolSnapshotId), Csv(resourceSnapshotId));
            }

            private static string Csv(string value)
            {
                value ??= string.Empty;
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
        }
    }
}
