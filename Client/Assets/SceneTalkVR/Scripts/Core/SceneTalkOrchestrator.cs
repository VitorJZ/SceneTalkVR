using System;
using System.Collections;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkOrchestrator : MonoBehaviour
    {
        private const string TechnicalInvalidAttemptError =
            "This task attempt is no longer valid. Please retry the task.";

        private enum SpeechCaptureMode
        {
            None,
            Request,
            Dialogue
        }

        private enum RetryKind
        {
            None,
            AvatarDialoguePlayback,
            AvatarFullReplyPlayback
        }

        [Header("Module adapters")]
        [SerializeField] private MonoBehaviour speechInputModule;
        [SerializeField] private MonoBehaviour brainModule;
        [SerializeField] private MonoBehaviour scenePresenterModule;
        [SerializeField] private MonoBehaviour avatarVoiceModule;

        [Header("Experiment")]
        [SerializeField] private ExperimentConditionManager experimentConditionManager;

        [Header("Learning Memory")]
        [SerializeField] private LearningMemoryService learningMemoryService;

        [Header("Optional UI")]
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text transcriptLabel;
        [SerializeField] private TMP_Text replyLabel;
        [SerializeField] private TMP_Text errorLabel;

        [Header("Recoverable Dialogue Failure")]
        [SerializeField, TextArea]
        private string llmFailurePrompt = "Sorry, I didn't catch that. Could you say it again?";

        [Header("Speech Input")]
        [SerializeField, Min(0f), Tooltip("Seconds after Speak starts during which another Speak click is ignored.")]
        private float speakButtonStopDebounceSeconds = 0.35f;

        [Header("Events")]
        public UnityEvent<SceneTalkState> stateChanged = new UnityEvent<SceneTalkState>();

        public SceneTalkState CurrentState { get; private set; } = SceneTalkState.Idle;
        public SceneTalkRuntimeConfig RuntimeConfig
        {
            get
            {
                var applier = FindFirstObjectByType<SceneTalkRuntimeConfigApplier>();
                return applier != null ? applier.Config : null;
            }
        }
        public string LastTranscript { get; private set; }
        public float LastSpeechCaptureEndTime { get; private set; }
        public SpringScenePayload LastScenePayload { get; private set; }
        public string LastError { get; private set; }
        public string LastCorrectionStatus { get; private set; }
        public string LastCorrectionDisplayText { get; private set; }
        public string LastCorrectionProvider { get; private set; }
        public string LastCorrectionStyle { get; private set; }
        public bool LastCorrectionHasFeedback { get; private set; }
        public bool IsTurnRunning => currentTurn != null;
        public bool IsTaskAttemptTechnicalInvalid
        {
            get
            {
                var lifecycle = ResolveExperimentConditionManager(false)?.LifecycleCoordinator;
                var formalInvalid = lifecycle != null
                                    && (lifecycle.TechnicalValidity == ExperimentTechnicalValidity.TechnicalInvalid
                                        || lifecycle.CurrentConditionAssignment?.status == ConditionRunStatus.TechnicalInvalid);
                var pilotInvalid = PilotWorkflowCoordinator.Active?.Current?.status == PilotRunStatus.TechnicalInvalid;
                return formalInvalid || pilotInvalid;
            }
        }
        public bool IsDialogueActive { get; private set; }
        public bool IsSpeechRecording { get; private set; }
        public bool IsAwaitingTurnReviewAction { get; private set; }
        public LearningSessionPage CurrentHistoryPage { get; private set; }
        public LearningSessionDetail SelectedHistorySession { get; private set; }
        public string HistoryErrorMessage { get; private set; }
        public bool IsHistoryAvailable => true;
        public bool IsHistoryRecordingEnabled => true;
        public bool CanContinueSelectedHistory => SelectedHistorySession?.summary?.CanContinue == true;
        public bool CanDeleteSelectedHistory => SelectedHistorySession?.summary?.CanDelete == true;
        public SceneTalkBrainRuntimeMode CurrentBrainMode
        {
            get
            {
                if (brainModule is SceneTalkVR.Runtime.Services.RealLLMService)
                {
                    return SceneTalkBrainRuntimeMode.DirectRealLlm;
                }

                if (brainModule is SceneTalkVR.Demo.DemoBrainModule)
                {
                    return SceneTalkBrainRuntimeMode.DemoBrain;
                }

                return SceneTalkBrainRuntimeMode.KeepCurrent;
            }
        }
        public bool ShouldShowExperimentDebug
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                return manager != null && manager.ShowDebugLabel;
            }
        }

        public string ExperimentDebugLabel
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                return manager == null ? string.Empty : manager.CurrentDebugLabel;
            }
        }

        public string CorrectionProviderSetting
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                return manager == null
                    ? ExperimentConditionManager.DialogueAvatarProvider
                    : manager.CurrentFeedbackProvider;
            }
        }

        public string CorrectionStyleSetting
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                return manager == null
                    ? ExperimentConditionManager.ExplicitStyle
                    : manager.CurrentFeedbackStyle;
            }
        }

        public string CorrectionAssistantEmbodimentSetting
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                return manager == null
                    ? ExperimentConditionManager.OrbAssistantEmbodiment
                    : manager.ConfiguredAssistantEmbodiment;
            }
        }

        public bool CanChangeCorrectionSetting
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                return CurrentState == SceneTalkState.Settings
                    && currentTurn == null
                    && !IsSpeechRecording
                    && manager != null
                    && manager.CanUseManualRuntimeCondition;
            }
        }

        public bool CanChangeCorrectionAssistantEmbodimentSetting => CanChangeCorrectionSetting
            && ResolveExperimentConditionManager(false)?.CanUseManualAssistantEmbodiment == true;

        public string CorrectionSettingLockReason
        {
            get
            {
                var manager = ResolveExperimentConditionManager(false);
                if (manager == null)
                {
                    return "Correction condition manager is unavailable.";
                }

                if (CurrentState != SceneTalkState.Settings)
                {
                    return "Open Settings to change the correction condition.";
                }

                if (currentTurn != null || IsSpeechRecording)
                {
                    return "Available after the current turn.";
                }

                return manager.ManualRuntimeConditionLockReason;
            }
        }

        private Coroutine currentTurn;
        private bool finishRequested;
        private AvatarPresentationVoiceModule subscribedAvatarVoiceModule;
        private ExperimentConditionManager subscribedExperimentConditionManager;
        private SpeechCaptureMode activeSpeechCaptureMode = SpeechCaptureMode.None;
        private string pendingHistorySessionId;
        private bool experimentExitConfirmationActive;
        private SceneTalkState? deferredStateDuringExperimentExit;
        private float speechCaptureStartedAt;
        private RetryKind pendingRetryKind;
        private SpringScenePayload pendingAvatarReplyPayload;
        private bool pendingAvatarReplyIsOpening;
        private AvatarReplyPlaybackFailureStage pendingAvatarFailureStage;

        private ISceneTalkSpeechInput SpeechInput => speechInputModule as ISceneTalkSpeechInput;
        private ISceneTalkManualSpeechInput ManualSpeechInput => speechInputModule as ISceneTalkManualSpeechInput;
        private ISceneTalkBrain Brain => brainModule as ISceneTalkBrain;
        private ISceneTalkCancelableBrain CancelableBrain => brainModule as ISceneTalkCancelableBrain;
        private ISceneTalkScenePresenter ScenePresenter => scenePresenterModule as ISceneTalkScenePresenter;
        private ISceneTalkAvatarVoice AvatarVoice => avatarVoiceModule as ISceneTalkAvatarVoice;
        private ISceneTalkAvatarReplyContext AvatarReplyContext => avatarVoiceModule as ISceneTalkAvatarReplyContext;
        private ISceneTalkAvatarThinkingState AvatarThinkingState => avatarVoiceModule as ISceneTalkAvatarThinkingState;
        private ISceneTalkAvatarRecoveryVoice AvatarRecoveryVoice => avatarVoiceModule as ISceneTalkAvatarRecoveryVoice;
        private ISceneTalkAvatarSessionReset AvatarSessionReset => avatarVoiceModule as ISceneTalkAvatarSessionReset;
        private ISceneTalkAvatarSessionPrepare AvatarSessionPrepare => avatarVoiceModule as ISceneTalkAvatarSessionPrepare;
        private ISceneTalkConversationContextReceiver ConversationContextReceiver => brainModule as ISceneTalkConversationContextReceiver;
        private ISceneTalkSceneSnapshotProvider SceneSnapshotProvider => scenePresenterModule as ISceneTalkSceneSnapshotProvider;

        public void ConfigureModules(
            MonoBehaviour speechInput = null,
            MonoBehaviour brain = null,
            MonoBehaviour scenePresenter = null,
            MonoBehaviour avatarVoice = null)
        {
            if (speechInput != null)
            {
                speechInputModule = speechInput;
            }

            if (brain != null)
            {
                brainModule = brain;
            }

            if (scenePresenter != null)
            {
                scenePresenterModule = scenePresenter;
            }

            if (avatarVoice != null)
            {
                avatarVoiceModule = avatarVoice;
                SubscribeAvatarCorrectionPlayback();
            }

            SubscribeExperimentConditionChanges();
            ApplyExperimentConditionToModules();
            RefreshUi();
        }

        private void Awake()
        {
            var manager = ResolveExperimentConditionManager(true);
            manager?.TrySetManualAssistantEmbodiment(SceneTalkUserSettingsStore.Current.assistantEmbodiment);
            ResolveLearningMemoryService(true);
            RefreshUi();
        }

        private void OnEnable()
        {
            SubscribeAvatarCorrectionPlayback();
            SubscribeExperimentConditionChanges();
        }

        private void OnDisable()
        {
            UnsubscribeAvatarCorrectionPlayback();
            UnsubscribeExperimentConditionChanges();
        }

        public void OpenSettings()
        {
            if ((CurrentState != SceneTalkState.Idle && CurrentState != SceneTalkState.Finished)
                || currentTurn != null
                || IsSpeechRecording)
            {
                return;
            }

            LastError = string.Empty;
            SetState(SceneTalkState.Settings);
        }

        internal void SetExperimentNavigationState(SceneTalkState state)
        {
            if (state == SceneTalkState.ExperimentExitConfirm)
            {
                experimentExitConfirmationActive = true;
                deferredStateDuringExperimentExit = null;
                SetState(state);
                return;
            }

            switch (state)
            {
                case SceneTalkState.ExperimentSelection:
                case SceneTalkState.ExperimentPhase:
                case SceneTalkState.ExperimentRanking:
                case SceneTalkState.ExperimentCompleted:
                case SceneTalkState.ExperimentHistoryLoading:
                case SceneTalkState.ExperimentHistoryList:
                case SceneTalkState.ExperimentHistoryActions:
                case SceneTalkState.ExperimentHistoryRecord:
                case SceneTalkState.ExperimentHistoryConversationDetail:
                case SceneTalkState.ExperimentHistoryQuestionnaireDetail:
                case SceneTalkState.ExperimentHistoryDeleteConfirm:
                case SceneTalkState.ExperimentHistoryError:
                    SetState(state);
                    break;
            }
        }

        internal void RestoreAfterExperimentExit(SceneTalkState state)
        {
            if (CurrentState != SceneTalkState.ExperimentExitConfirm) return;
            var restoreState = deferredStateDuringExperimentExit ?? (state == SceneTalkState.ExperimentExitConfirm
                ? SceneTalkState.ExperimentSelection
                : state);
            ClearExperimentExitConfirmation();
            SetState(restoreState);
        }

        public void CloseSettings()
        {
            if (CurrentState == SceneTalkState.Settings)
            {
                SetState(SceneTalkState.Idle);
            }
        }

        public void OpenHistory()
        {
            if (!IsHistoryAvailable
                || (CurrentState != SceneTalkState.Idle && CurrentState != SceneTalkState.Finished)
                || currentTurn != null
                || IsSpeechRecording)
            {
                return;
            }

            LoadHistoryPage(0);
        }

        public void LoadHistoryPage(int pageIndex)
        {
            if (!IsHistoryAvailable
                || (CurrentState != SceneTalkState.Idle
                    && CurrentState != SceneTalkState.Finished
                    && CurrentState != SceneTalkState.HistoryList
                    && CurrentState != SceneTalkState.HistoryDetail
                    && CurrentState != SceneTalkState.HistoryDeleteConfirm
                    && CurrentState != SceneTalkState.HistoryError))
            {
                return;
            }

            SetState(SceneTalkState.HistoryLoading);
            try
            {
                var memory = ResolveLearningMemoryService(true);
                CurrentHistoryPage = memory.GetPage(pageIndex);
                SelectedHistorySession = null;
                HistoryErrorMessage = string.Empty;
                SetState(SceneTalkState.HistoryList);
            }
            catch (Exception exception)
            {
                EnterHistoryError($"Failed to load history. {exception.Message}");
            }
        }

        public void OpenPreviousHistoryPage()
        {
            LoadHistoryPage((CurrentHistoryPage?.pageIndex ?? 0) - 1);
        }

        public void OpenNextHistoryPage()
        {
            LoadHistoryPage((CurrentHistoryPage?.pageIndex ?? 0) + 1);
        }

        public void SelectHistorySession(string sessionId)
        {
            if (CurrentState != SceneTalkState.HistoryList || string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            SetState(SceneTalkState.HistoryLoading);
            try
            {
                SelectedHistorySession = ResolveLearningMemoryService(true).GetSession(sessionId);
                if (SelectedHistorySession == null)
                {
                    throw new InvalidOperationException("The selected history record no longer exists.");
                }

                HistoryErrorMessage = string.Empty;
                SetState(SceneTalkState.HistoryDetail);
            }
            catch (Exception exception)
            {
                EnterHistoryError($"Failed to open history. {exception.Message}");
            }
        }

        public void BackFromHistory()
        {
            if (CurrentState == SceneTalkState.HistoryDeleteConfirm)
            {
                SetState(SceneTalkState.HistoryDetail);
                return;
            }

            if (CurrentState == SceneTalkState.HistoryDetail)
            {
                SelectedHistorySession = null;
                SetState(SceneTalkState.HistoryList);
                return;
            }

            if (CurrentState == SceneTalkState.HistoryError)
            {
                HistoryErrorMessage = string.Empty;
                LastError = string.Empty;
                if (SelectedHistorySession != null)
                {
                    SetState(SceneTalkState.HistoryDetail);
                }
                else if (CurrentHistoryPage != null)
                {
                    SetState(SceneTalkState.HistoryList);
                }
                else
                {
                    SetState(SceneTalkState.Idle);
                }

                return;
            }

            if (CurrentState == SceneTalkState.HistoryList
                || CurrentState == SceneTalkState.HistoryLoading)
            {
                SelectedHistorySession = null;
                CurrentHistoryPage = null;
                HistoryErrorMessage = string.Empty;
                SetState(SceneTalkState.Idle);
            }
        }

        public void RequestDeleteSelectedHistory()
        {
            if (CurrentState == SceneTalkState.HistoryDetail && CanDeleteSelectedHistory)
            {
                SetState(SceneTalkState.HistoryDeleteConfirm);
            }
        }

        public void CancelDeleteSelectedHistory()
        {
            if (CurrentState == SceneTalkState.HistoryDeleteConfirm)
            {
                SetState(SceneTalkState.HistoryDetail);
            }
        }

        public void ConfirmDeleteSelectedHistory()
        {
            if (CurrentState != SceneTalkState.HistoryDeleteConfirm
                || SelectedHistorySession?.summary == null
                || !CanDeleteSelectedHistory)
            {
                return;
            }

            var pageIndex = CurrentHistoryPage?.pageIndex ?? 0;
            SetState(SceneTalkState.HistoryLoading);
            try
            {
                ResolveLearningMemoryService(true).DeleteSession(SelectedHistorySession.summary.sessionId);
                SelectedHistorySession = null;
                CurrentHistoryPage = ResolveLearningMemoryService(true).GetPage(pageIndex);
                SetState(SceneTalkState.HistoryList);
            }
            catch (Exception exception)
            {
                EnterHistoryError($"Failed to delete history. {exception.Message}");
            }
        }

        public void ContinueSelectedHistory()
        {
            if (CurrentState != SceneTalkState.HistoryDetail
                || SelectedHistorySession == null
                || currentTurn != null
                || !CanContinueSelectedHistory)
            {
                return;
            }

            currentTurn = StartCoroutine(RestoreHistorySession(SelectedHistorySession));
        }

        public void ChangeCorrectionProviderSetting()
        {
            if (!CanChangeCorrectionSetting)
            {
                return;
            }

            var manager = ResolveExperimentConditionManager(false);
            if (manager == null)
            {
                return;
            }

            var nextProvider = string.Equals(
                manager.CurrentFeedbackProvider,
                ExperimentConditionManager.DialogueAvatarProvider,
                StringComparison.OrdinalIgnoreCase)
                ? ExperimentConditionManager.AssistantAgentProvider
                : ExperimentConditionManager.DialogueAvatarProvider;
            manager.TrySetManualFeedbackProvider(nextProvider);
        }

        public void ChangeCorrectionStyleSetting()
        {
            if (!CanChangeCorrectionSetting)
            {
                return;
            }

            var manager = ResolveExperimentConditionManager(false);
            if (manager == null)
            {
                return;
            }

            var nextStyle = string.Equals(
                manager.CurrentFeedbackStyle,
                ExperimentConditionManager.ExplicitStyle,
                StringComparison.OrdinalIgnoreCase)
                ? ExperimentConditionManager.RecastStyle
                : ExperimentConditionManager.ExplicitStyle;
            manager.TrySetManualFeedbackStyle(nextStyle);
        }

        public void ChangeCorrectionAssistantEmbodimentSetting()
        {
            if (!CanChangeCorrectionAssistantEmbodimentSetting)
            {
                return;
            }

            var manager = ResolveExperimentConditionManager(false);
            if (manager == null)
            {
                return;
            }

            var current = manager.ConfiguredAssistantEmbodiment;
            var next = string.Equals(
                current,
                ExperimentConditionManager.AudioOnlyAssistantEmbodiment,
                StringComparison.OrdinalIgnoreCase)
                ? ExperimentConditionManager.OrbAssistantEmbodiment
                : string.Equals(
                    current,
                    ExperimentConditionManager.OrbAssistantEmbodiment,
                    StringComparison.OrdinalIgnoreCase)
                    ? ExperimentConditionManager.HumanoidAssistantEmbodiment
                    : ExperimentConditionManager.AudioOnlyAssistantEmbodiment;
            manager.TrySetManualAssistantEmbodiment(next);
            SceneTalkUserSettingsStore.SetAssistantEmbodiment(next);
        }

        public void StartPractice()
        {
            if (CurrentState == SceneTalkState.Settings)
            {
                return;
            }

            finishRequested = false;
            ApplyExperimentConditionToModules();
            EnterRequestReadyState(true);
        }

        public void StartPracticeTurn()
        {
            BeginRequestSpeechCapture();
        }

        public void RetryListening()
        {
            ClearPendingRetry();
            if (IsDialogueActive)
            {
                BeginDialogueSpeechCapture();
                return;
            }

            BeginRequestSpeechCapture();
        }

        public void ConfirmPracticeRequest()
        {
            if (currentTurn != null || string.IsNullOrWhiteSpace(LastTranscript))
            {
                return;
            }

            if (!ValidateGenerationModules())
            {
                return;
            }

            finishRequested = false;
            currentTurn = StartCoroutine(RunConfirmedPracticeTurn());
        }

        public void ConfirmFixedTaskSelection(string taskId)
        {
            if (currentTurn != null)
            {
                return;
            }

            if (!ValidateGenerationModules())
            {
                return;
            }

            finishRequested = false;
            currentTurn = StartCoroutine(RunFixedTaskStartup(taskId));
        }

        public void LoadAssignedTask(string taskId) => ConfirmFixedTaskSelection(taskId);

        private IEnumerator RunFixedTaskStartup(string taskId)
        {
            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;

            var manager = ResolveExperimentConditionManager(true);
            pendingHistorySessionId = Guid.NewGuid().ToString("N");
            if (manager != null)
            {
                if (!manager.IsFormalExperiment)
                {
                    manager.StartConversation(pendingHistorySessionId, taskId);
                }

                var definition = manager.TaskCatalog?.Find(taskId);
                var lifecycle = manager.LifecycleCoordinator;
                var rehearsalPrepared = RehearsalSessionCoordinator.Active != null
                    && RehearsalSessionCoordinator.Active.IsTaskPrepared(taskId);
                var collectionPrepared = EditorCollectionSessionCoordinator.Active != null
                    && EditorCollectionSessionCoordinator.Active.IsTaskPrepared(taskId);
                var pilotCollectionPrepared = PilotCollectionSessionCoordinator.Active != null
                    && PilotCollectionSessionCoordinator.Active.IsTaskPrepared(taskId);
                var demoPrepared = EditorDemoSessionCoordinator.Active != null
                    && EditorDemoSessionCoordinator.Active.IsTaskPrepared(taskId);
                var prepareDeveloperSession = !manager.IsFormalExperiment
                    && !rehearsalPrepared
                    && !collectionPrepared
                    && !demoPrepared
                    && !pilotCollectionPrepared
                    && definition != null
                    && definition.phase == ExperimentTaskPhase.Formal
                    && lifecycle != null;
                var assignmentError = string.Empty;
                var prepared = collectionPrepared || rehearsalPrepared || demoPrepared || pilotCollectionPrepared || (prepareDeveloperSession
                    ? lifecycle.PrepareDeveloperTaskSession(taskId, out assignmentError)
                    : manager.LoadAssignedTask(taskId, out assignmentError));
                if (collectionPrepared || rehearsalPrepared || demoPrepared || pilotCollectionPrepared) assignmentError = string.Empty;
                if (!prepared)
                {
                    LastError = assignmentError;
                    SetState(SceneTalkState.Error);
                    currentTurn = null;
                    yield break;
                }
            }
            if (brainModule is ISceneTalkSessionReset brainReset)
            {
                brainReset.ResetSession();
            }
            SetState(SceneTalkState.Processing);

            ApplyExperimentConditionToModules();

            var condition = manager != null ? manager.CurrentCondition : null;
            var task = condition != null ? condition.task : null;
            if (task == null)
            {
                LastError = "Failed to load default task definition.";
                SetState(SceneTalkState.Error);
                currentTurn = null;
                yield break;
            }

            var fallbackRole = string.IsNullOrWhiteSpace(task.fallbackAvatarRole) ? "barista" : task.fallbackAvatarRole;
            var gender = string.IsNullOrWhiteSpace(task.fallbackAvatarGenderPresentation) ? "female" : task.fallbackAvatarGenderPresentation;
            
            var roleFamily = "clerk";
            if (fallbackRole.Contains("barista")) roleFamily = "barista";
            else if (fallbackRole.Contains("instructor")) roleFamily = "instructor";
            else if (fallbackRole.Contains("police")) roleFamily = "police";
            else if (fallbackRole.Contains("teacher")) roleFamily = "teacher";

            var initialPayload = new SpringScenePayload
            {
                taskType = taskId,
                environmentType = string.IsNullOrWhiteSpace(task.fallbackEnvironmentType) ? taskId : task.fallbackEnvironmentType,
                dialogueReply = task.initialQuestion,
                avatarRole = new AvatarRoleData
                {
                    presetKey = task.avatarPresetKey,
                    voiceProfileKey = task.voiceProfileKey,
                    spawnPosition = task.spawnPosition,
                    spawnRotation = task.spawnRotation,
                    role = fallbackRole,
                    speakingSpeed = "medium",
                    accent = "american",
                    attitude = string.IsNullOrWhiteSpace(task.fallbackAvatarAttitude) ? "helpful" : task.fallbackAvatarAttitude,
                    appearance = new AvatarAppearanceData
                    {
                        styleId = "semi_realistic_v1",
                        genderPresentation = gender,
                        ageBucket = "adult",
                        bodyBuild = "average",
                        outfitRole = roleFamily,
                        outfitColor = "blue",
                        seed = 42345 + Mathf.Abs(taskId.GetHashCode() % 1000)
                    }
                },
                scene = new ScenePayload
                {
                    mode = "skybox",
                    skyboxUrl = string.IsNullOrWhiteSpace(task.fallbackSkyboxUrl) ? $"demo://{taskId}" : task.fallbackSkyboxUrl,
                    layoutObjects = task.fallbackLayoutObjects ?? Array.Empty<LayoutObjectData>()
                }
            };

            if (EditorDemoSessionCoordinator.Active != null && EditorDemoSessionCoordinator.Active.IsFormalDemo)
            {
                var demoAvatarKey = EditorDemoSessionCoordinator.Active.ResolveFormalAvatarKey(taskId);
                if (!string.IsNullOrWhiteSpace(demoAvatarKey)) initialPayload.avatarRole.presetKey = demoAvatarKey;
            }
            if (RehearsalSessionCoordinator.Active != null && RehearsalSessionCoordinator.Active.IsFormal)
            {
                var rehearsalAvatarKey = RehearsalSessionCoordinator.Active.ResolveFormalAvatarKey(taskId);
                if (!string.IsNullOrWhiteSpace(rehearsalAvatarKey)) initialPayload.avatarRole.presetKey = rehearsalAvatarKey;
                initialPayload.avatarRole.voiceProfileKey = "rehearsal_dialogue_voice";
            }
            if (EditorCollectionSessionCoordinator.Active != null && EditorCollectionSessionCoordinator.Active.IsArmed)
            {
                var collectionAvatarKey = EditorCollectionSessionCoordinator.Active.ResolveFormalAvatarKey(taskId);
                if (!string.IsNullOrWhiteSpace(collectionAvatarKey)) initialPayload.avatarRole.presetKey = collectionAvatarKey;
                initialPayload.avatarRole.voiceProfileKey = "editor_collection_dialogue_voice";
            }

            ApplyExperimentConditionToPayload(initialPayload);
            LastScenePayload = initialPayload;
            RefreshUi();
            SetState(SceneTalkState.SceneReady);

            string error = null;
            yield return ScenePresenter.PresentScene(
                initialPayload,
                () => { },
                message => error = message);

            if (HandleErrorOrFinish(error, "Scene presentation failed."))
            {
                currentTurn = null;
                yield break;
            }

            SpringScenePayload historySnapshot = null;
            yield return StartNewHistorySession(
                pendingHistorySessionId,
                initialPayload,
                null,
                value => historySnapshot = value,
                message => error = message);
            if (HandleErrorOrFinish(error, "History persistence failed."))
            {
                currentTurn = null;
                yield break;
            }

            if (historySnapshot != null)
            {
                initialPayload = historySnapshot;
                LastScenePayload = historySnapshot;
            }

            IsDialogueActive = true;
            PrepareCorrectionReview(initialPayload);
            SubscribeAvatarCorrectionPlayback();
            
            SetState(LastCorrectionHasFeedback
                ? SceneTalkState.CorrectionFeedbackSpeaking
                : SceneTalkState.DialogueSpeaking);

            AvatarReplyContext?.SetReplyContext(true);
            yield return AvatarVoice.PresentReply(
                initialPayload,
                () => { },
                message => error = message);

            if (HandleAvatarVoiceErrorOrFinish(error, initialPayload, true))
            {
                currentTurn = null;
                yield break;
            }

            currentTurn = null;
            EnterTurnReviewState();
        }

        public void StartDialogueTurn()
        {
            BeginDialogueSpeechCapture();
        }

        public void ToggleRequestSpeechCapture()
        {
            if (IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.Request)
            {
                if (!CanStopSpeechCaptureFromSpeakButton())
                {
                    return;
                }

                RequestStopSpeechCapture();
                return;
            }

            BeginRequestSpeechCapture();
        }

        public void ToggleDialogueSpeechCapture()
        {
            if (IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.Dialogue)
            {
                if (!CanStopSpeechCaptureFromSpeakButton())
                {
                    return;
                }

                RequestStopSpeechCapture();
                return;
            }

            if (CurrentState == SceneTalkState.Error)
            {
                RetryAfterError();
                return;
            }

            BeginDialogueSpeechCapture();
        }

        public void ContinueAfterFeedback()
        {
            if (!IsDialogueActive || currentTurn != null)
            {
                return;
            }

            RecordContinueIfReviewPending();
            LastCorrectionStatus = "Ready for your next line.";
            SetState(SceneTalkState.TurnReview);
        }

        public void TryAgainAfterFeedback()
        {
            if (!IsDialogueActive || currentTurn != null)
            {
                return;
            }

            IsAwaitingTurnReviewAction = false;
            ResolveExperimentConditionManager(false)?.RecordUserAction("try_again");
            BeginDialogueSpeechCapture(false);
        }

        public void FinishPractice()
        {
            ReturnToInitialMenu();
        }

        public void PauseForQuestionnaireBoundary()
        {
            finishRequested = true;
            CancelActiveSpeechCapture();
            if (currentTurn != null)
            {
                CancelableBrain?.CancelActiveGeneration();
                (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
                StopCoroutine(currentTurn);
                currentTurn = null;
            }
            IsSpeechRecording = false;
            IsDialogueActive = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            IsAwaitingTurnReviewAction = false;
            ClearCorrectionReviewState();
            foreach (var module in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (module == this || module is ExperimentLifecycleCoordinator || module is QuestionnaireRuntimeController
                    || module is PilotWorkflowCoordinator) continue;
                if (module is ISceneTalkSessionReset reset) reset.ResetSession();
            }
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (source != null && source.isPlaying) source.Stop();
            ResolveLearningMemoryService(false)?.EndActiveSession();
            pendingHistorySessionId = string.Empty;
            SetState(SceneTalkState.TurnReview);
        }

        public void ReturnToInitialMenu()
        {
            ClearExperimentExitConfirmation();
            finishRequested = true;
            var manager = ResolveExperimentConditionManager(false);
            manager?.RecordUserAction("exit");
            var lifecycle = manager?.LifecycleCoordinator;
            if (lifecycle?.CurrentConditionAssignment?.status == ConditionRunStatus.Running)
            {
                lifecycle.Abort("participant_exit");
            }
            CancelActiveSpeechCapture();

            if (currentTurn != null)
            {
                CancelableBrain?.CancelActiveGeneration();
                (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
                StopCoroutine(currentTurn);
                currentTurn = null;
            }

            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;
            ClearCorrectionReviewState();
            IsDialogueActive = false;
            IsSpeechRecording = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            ClearPresentedSceneIfSupported();
            AvatarSessionReset?.ClearAvatar();
            
            // Clear Brain history if it supports session reset
            if (brainModule is ISceneTalkSessionReset brainReset)
            {
                brainReset.ResetSession();
            }

            ResolveLearningMemoryService(false)?.EndActiveSession();
            pendingHistorySessionId = string.Empty;
            SelectedHistorySession = null;
            CurrentHistoryPage = null;
            HistoryErrorMessage = string.Empty;

            manager?.ResetConditionSessionBoundary();

            SetState(SceneTalkState.Idle);
        }

        public void ResetForConditionSelection()
        {
            ClearExperimentExitConfirmation();
            finishRequested = true;
            CancelActiveSpeechCapture();
            if (currentTurn != null)
            {
                CancelableBrain?.CancelActiveGeneration();
                (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
                StopCoroutine(currentTurn);
                currentTurn = null;
            }
            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;
            ClearCorrectionReviewState();
            IsDialogueActive = false;
            IsSpeechRecording = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            ClearPresentedSceneIfSupported();
            AvatarSessionReset?.ClearAvatar();
            if (brainModule is ISceneTalkSessionReset brainReset) brainReset.ResetSession();
            ResolveLearningMemoryService(false)?.EndActiveSession();
            pendingHistorySessionId = string.Empty;
            ResolveExperimentConditionManager(false)?.ResetConditionSessionBoundary();
            SetState(SceneTalkState.Idle);
        }

        public void RetryAfterError()
        {
            if (CurrentState != SceneTalkState.Error || currentTurn != null)
            {
                return;
            }

            GatewayTransportRouter.Active?.RequestBoundaryProbe(GatewayRequestStage.Retry);

            if ((pendingRetryKind == RetryKind.AvatarDialoguePlayback
                    || pendingRetryKind == RetryKind.AvatarFullReplyPlayback)
                && pendingAvatarReplyPayload != null)
            {
                currentTurn = StartCoroutine(RetryAvatarReplyPlayback());
                return;
            }

            RetryListening();
        }

        private void EnterRequestReadyState(bool clearTranscript)
        {
            if (currentTurn != null)
            {
                CancelActiveSpeechCapture();
                CancelableBrain?.CancelActiveGeneration();
                (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
                StopCoroutine(currentTurn);
                currentTurn = null;
            }

            IsSpeechRecording = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            LastError = string.Empty;
            IsDialogueActive = false;
            ClearCorrectionReviewState();
            ApplyExperimentConditionToModules();

            if (clearTranscript)
            {
                LastTranscript = string.Empty;
                LastScenePayload = null;
            }

            SetState(SceneTalkState.Listening);
        }

        private bool BeginRequestSpeechCapture()
        {
            if (BlockSpeechCaptureForInvalidAttempt())
            {
                return false;
            }

            if (currentTurn != null)
            {
                if (IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.Request)
                {
                    RequestStopSpeechCapture();
                    return true;
                }

                CancelableBrain?.CancelActiveGeneration();
                (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
                StopCoroutine(currentTurn);
            }

            if (!ValidateSpeechModule())
            {
                currentTurn = null;
                return false;
            }

            finishRequested = false;
            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;
            ClearCorrectionReviewState();
            IsDialogueActive = false;
            currentTurn = StartCoroutine(RunRequestSpeechCaptureTurn());
            return true;
        }

        private bool BeginDialogueSpeechCapture(bool recordContinueAction = true)
        {
            if (BlockSpeechCaptureForInvalidAttempt())
            {
                return false;
            }

            if (currentTurn != null)
            {
                if (IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.Dialogue)
                {
                    RequestStopSpeechCapture();
                    return true;
                }

                return false;
            }

            if (!IsDialogueActive)
            {
                return BeginRequestSpeechCapture();
            }

            if (!ValidateSpeechModule() || !ValidateDialogueModules())
            {
                return false;
            }

            if (recordContinueAction)
            {
                RecordContinueIfReviewPending();
            }

            IsAwaitingTurnReviewAction = false;
            finishRequested = false;
            currentTurn = StartCoroutine(RunDialogueTurn());
            return true;
        }

        private bool BlockSpeechCaptureForInvalidAttempt()
        {
            if (!IsTaskAttemptTechnicalInvalid)
            {
                return false;
            }

            LastError = TechnicalInvalidAttemptError;
            IsAwaitingTurnReviewAction = false;
            SetState(SceneTalkState.Error);
            return true;
        }

        private void RequestStopSpeechCapture()
        {
            if (!IsSpeechRecording)
            {
                return;
            }

            IsSpeechRecording = false;
            ManualSpeechInput?.RequestStopCapture();
            SetState(SceneTalkState.Transcribing);
        }

        private void CancelActiveSpeechCapture()
        {
            if (!IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.None)
            {
                return;
            }

            IsSpeechRecording = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            ManualSpeechInput?.CancelCapture();
        }

        private void BeginSpeechCaptureState(SpeechCaptureMode mode)
        {
            activeSpeechCaptureMode = mode;
            IsSpeechRecording = true;
            speechCaptureStartedAt = Time.realtimeSinceStartup;
            SetState(SceneTalkState.Recording);
        }

        private bool CanStopSpeechCaptureFromSpeakButton()
        {
            return Time.realtimeSinceStartup - speechCaptureStartedAt >= speakButtonStopDebounceSeconds;
        }

        private void CompleteSpeechCaptureState()
        {
            IsSpeechRecording = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            LastSpeechCaptureEndTime = Time.realtimeSinceStartup;
        }

        private IEnumerator RunRequestSpeechCaptureTurn()
        {
            LastTranscript = string.Empty;
            LastError = string.Empty;
            RefreshUi();

            BeginExperimentTurnForRecording();
            BeginSpeechCaptureState(SpeechCaptureMode.Request);

            string transcript = null;
            string error = null;
            yield return SpeechInput.CaptureSpeech(
                value => transcript = value,
                message => error = message);

            CompleteSpeechCaptureState();
            ResolveExperimentConditionManager(false)?.CompleteRecording();
            ResolveExperimentConditionManager(false)?.RecordTimingEvent(ExperimentTimingEventType.UserSpeechEnded);
            RecordSpeechMetadataHelper(transcript);

            if (HandleErrorOrFinish(error, "Speech input failed."))
            {
                currentTurn = null;
                yield break;
            }

            LastTranscript = transcript;
            RefreshUi();
            currentTurn = null;
            SetState(SceneTalkState.Listening);
        }

        private IEnumerator RunConfirmedPracticeTurn()
        {
            LastScenePayload = null;
            LastError = string.Empty;
            if (!ResolveLearningMemoryService(true).HasActiveSession)
            {
                pendingHistorySessionId = Guid.NewGuid().ToString("N");
                var manager = ResolveExperimentConditionManager(true);
                manager?.StartConversation(pendingHistorySessionId, manager.CurrentCondition?.scenarioId);
                if (brainModule is ISceneTalkSessionReset brainReset)
                {
                    brainReset.ResetSession();
                }
            }
            EnsureExperimentTurnStarted();
            SetState(SceneTalkState.Processing);

            var transcript = LastTranscript;
            string error = null;
            SpringScenePayload payload = null;
            ApplyExperimentConditionToModules();
            yield return GenerateSceneAndReplyWithStreamingSupport(
                transcript,
                value => payload = value,
                message => error = message);

            if (HasGenerationFailure(error, payload))
            {
                yield return RecoverFromLlmFailure(error, "LLM/scene generation failed.");
                yield break;
            }

            ApplyExperimentConditionToPayload(payload);
            LastScenePayload = payload;
            RefreshUi();
            SetState(SceneTalkState.SceneReady);

            yield return ScenePresenter.PresentScene(
                payload,
                () => { },
                message => error = message);

            if (HandleErrorOrFinish(error, "Scene presentation failed."))
            {
                currentTurn = null;
                yield break;
            }

            SpringScenePayload historySnapshot = null;
            yield return StartNewHistorySession(
                pendingHistorySessionId,
                payload,
                transcript,
                value => historySnapshot = value,
                message => error = message);
            if (HandleErrorOrFinish(error, "History persistence failed."))
            {
                currentTurn = null;
                yield break;
            }

            if (historySnapshot != null)
            {
                payload = historySnapshot;
                LastScenePayload = historySnapshot;
            }

            IsDialogueActive = true;
            PrepareCorrectionReview(payload);
            SubscribeAvatarCorrectionPlayback();
            SetState(LastCorrectionHasFeedback
                ? SceneTalkState.CorrectionFeedbackSpeaking
                : SceneTalkState.DialogueSpeaking);

            AvatarReplyContext?.SetReplyContext(true);
            yield return AvatarVoice.PresentReply(
                payload,
                () => { },
                message => error = message);

            RecordTurnMetrics(payload);

            if (HandleAvatarVoiceErrorOrFinish(error, payload, true))
            {
                currentTurn = null;
                yield break;
            }

            currentTurn = null;
            EnterTurnReviewState();
        }

        private IEnumerator RunDialogueTurn()
        {
            LastError = string.Empty;
            RefreshUi();

            BeginExperimentTurnForRecording();
            BeginSpeechCaptureState(SpeechCaptureMode.Dialogue);

            string transcript = null;
            string error = null;
            yield return SpeechInput.CaptureSpeech(
                value => transcript = value,
                message => error = message);

            CompleteSpeechCaptureState();
            ResolveExperimentConditionManager(false)?.CompleteRecording();
            ResolveExperimentConditionManager(false)?.RecordTimingEvent(ExperimentTimingEventType.UserSpeechEnded);
            RecordSpeechMetadataHelper(transcript);

            if (HandleErrorOrFinish(error, "Speech input failed."))
            {
                currentTurn = null;
                yield break;
            }

            var goalManager = ResolveExperimentConditionManager(false);
            var participantTurnId = goalManager?.CurrentTurnId;
            GoalEvaluationOrchestrator.NotifyParticipantTurnSubmitted(
                goalManager?.LifecycleCoordinator,
                PilotWorkflowCoordinator.Active,
                participantTurnId,
                transcript);
            LastTranscript = transcript;
            RefreshUi();
            SetState(SceneTalkState.Processing);
            AvatarReplyContext?.SetReplyContext(false);
            AvatarThinkingState?.SetThinking(true);

            SpringScenePayload payload = null;
            error = null;
            ApplyExperimentConditionToModules();
            yield return GenerateSceneAndReplyWithStreamingSupport(
                transcript,
                value => payload = value,
                message => error = message);
            AvatarThinkingState?.SetThinking(false);

            if (HasGenerationFailure(error, payload))
            {
                yield return RecoverFromLlmFailure(error, "Dialogue reply generation failed.");
                yield break;
            }

            ApplyExperimentConditionToPayload(payload);
            LastScenePayload = payload;
            RefreshUi();
            GoalEvaluationOrchestrator.StartActiveTaskGoalEvaluation(this,
                goalManager?.LifecycleCoordinator, PilotWorkflowCoordinator.Active,
                participantTurnId, transcript);
            try
            {
                if (IsHistoryRecordingEnabled)
                {
                    ResolveLearningMemoryService(true).AppendTurn(transcript, payload);
                }
            }
            catch (Exception exception)
            {
                EnterError($"Failed to save the dialogue turn. {exception.Message}");
                currentTurn = null;
                yield break;
            }
            PrepareCorrectionReview(payload);
            SubscribeAvatarCorrectionPlayback();
            SetState(LastCorrectionHasFeedback
                ? SceneTalkState.CorrectionFeedbackSpeaking
                : SceneTalkState.DialogueSpeaking);

            error = null;
            yield return AvatarVoice.PresentReply(
                payload,
                () => { },
                message => error = message);

            RecordTurnMetrics(payload);

            if (HandleAvatarVoiceErrorOrFinish(error, payload, false))
            {
                currentTurn = null;
                yield break;
            }

            currentTurn = null;
            EnterTurnReviewState();
        }

        private IEnumerator StartNewHistorySession(
            string sessionId,
            SpringScenePayload payload,
            string initialUserText,
            Action<SpringScenePayload> onComplete,
            Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Cannot save a null scene payload.");
                yield break;
            }

            var snapshot = LearningMemoryService.ClonePayload(payload);
            string captureError = null;
            if (SceneSnapshotProvider != null)
            {
                yield return SceneSnapshotProvider.CaptureSceneSnapshot(
                    sessionId,
                    payload,
                    value => snapshot = value,
                    message => captureError = message);
            }

            if (!string.IsNullOrWhiteSpace(captureError))
            {
                onError?.Invoke(captureError);
                yield break;
            }

            LearningMemoryService memory = null;
            var sessionCreated = false;
            try
            {
                var settings = BuildHistorySettingsSnapshot(sessionId);
                memory = ResolveLearningMemoryService(true);
                memory.BeginSession(
                    sessionId,
                    snapshot,
                    settings,
                    ResolveHistoryTitle(snapshot),
                    snapshot.dialogueReply,
                    initialUserText);
                sessionCreated = true;
                var storedSession = memory.GetSession(sessionId);
                ConversationContextReceiver?.RestoreConversationContext(storedSession);
                pendingHistorySessionId = string.Empty;
                onComplete?.Invoke(snapshot);
            }
            catch (Exception exception)
            {
                if (sessionCreated && memory != null)
                {
                    try
                    {
                        memory.EndActiveSession();
                        memory.DeleteSession(sessionId);
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogWarning(
                            $"[SceneTalkVR] Failed to roll back incomplete history session '{sessionId}': "
                            + cleanupException.Message,
                            this);
                    }
                }

                onError?.Invoke(exception.Message);
            }
        }

        private LearningSessionDetail BuildTransientConversationContext(
            string sessionId,
            SpringScenePayload payload,
            string initialUserText)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var hasInitialUserTurn = !string.IsNullOrWhiteSpace(initialUserText);
            return new LearningSessionDetail
            {
                summary = new LearningSessionSummary
                {
                    sessionId = sessionId ?? string.Empty,
                    turnCount = hasInitialUserTurn ? 1 : 0
                },
                settings = BuildHistorySettingsSnapshot(sessionId),
                sceneSnapshot = LearningMemoryService.ClonePayload(payload),
                turns = new[]
                {
                    new DialogueTurnRecord
                    {
                        sequenceIndex = hasInitialUserTurn ? 1 : 0,
                        isOpening = !hasInitialUserTurn,
                        createdAtUnixMs = now,
                        userText = initialUserText ?? string.Empty,
                        assistantText = payload?.dialogueReply ?? string.Empty,
                        payload = LearningMemoryService.ClonePayload(payload)
                    }
                }
            };
        }

        private IEnumerator RestoreHistorySession(LearningSessionDetail session)
        {
            SetState(SceneTalkState.HistoryRestoring);
            LastError = string.Empty;
            HistoryErrorMessage = string.Empty;
            finishRequested = false;

            string error = null;
            if (!TryRestoreHistoryBrainMode(session.settings?.brainMode, out error))
            {
                error ??= "The stored Brain mode could not be restored.";
            }

            var manager = ResolveExperimentConditionManager(true);
            var restoredCondition = ExperimentConditionManager.CloneCondition(session.settings?.condition);
            if (string.IsNullOrWhiteSpace(error) && restoredCondition == null)
            {
                error = "The stored correction condition is missing.";
            }
            else if (string.IsNullOrWhiteSpace(error))
            {
                restoredCondition.sessionId = session.summary.sessionId;
                if (!manager.RestoreConversation(restoredCondition, session.summary.turnCount))
                {
                    error = "History cannot be resumed while formal experiment mode is active.";
                }
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                ApplyExperimentConditionToModules();
                try
                {
                    ConversationContextReceiver?.RestoreConversationContext(session);
                }
                catch (Exception exception)
                {
                    error = $"Failed to restore the LLM context. {exception.Message}";
                }
            }

            AvatarSessionReset?.ClearAvatar();
            ClearPresentedSceneIfSupported();
            if (string.IsNullOrWhiteSpace(error))
            {
                if (ScenePresenter == null)
                {
                    error = "Scene presenter is unavailable.";
                }
                else
                {
                    yield return ScenePresenter.PresentScene(
                        session.sceneSnapshot,
                        () => { },
                        message => error = message);
                }
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                if (AvatarSessionPrepare == null)
                {
                    error = "Avatar module does not support silent history restoration.";
                }
                else
                {
                    yield return AvatarSessionPrepare.PrepareSession(
                        session.sceneSnapshot,
                        () => { },
                        message => error = message);
                }
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                ResolveLearningMemoryService(false)?.EndActiveSession();
                currentTurn = null;
                EnterHistoryError($"Failed to continue history. {error}");
                yield break;
            }

            ResolveLearningMemoryService(true).Activate(session);
            var turns = session.turns ?? Array.Empty<DialogueTurnRecord>();
            var lastTurn = turns.Length == 0 ? null : turns[turns.Length - 1];
            var displayPayload = LearningMemoryService.ClonePayload(session.sceneSnapshot);
            if (lastTurn != null)
            {
                LastTranscript = lastTurn.isOpening ? string.Empty : lastTurn.userText;
                displayPayload.dialogueReply = lastTurn.assistantText;
                var lastPayload = LearningMemoryService.ClonePayload(lastTurn.payload);
                displayPayload.dialogueContinuation = lastPayload.dialogueContinuation;
                displayPayload.correctionFeedback = lastPayload.correctionFeedback;
            }
            else
            {
                LastTranscript = string.Empty;
            }

            LastScenePayload = displayPayload;
            IsDialogueActive = true;
            IsSpeechRecording = false;
            activeSpeechCaptureMode = SpeechCaptureMode.None;
            AvatarReplyContext?.SetReplyContext(false);
            PrepareCorrectionReview(displayPayload);
            SelectedHistorySession = null;
            CurrentHistoryPage = null;
            currentTurn = null;
            SetState(SceneTalkState.TurnReview);
        }

        private ConversationSettingsSnapshot BuildHistorySettingsSnapshot(string sessionId)
        {
            var condition = ExperimentConditionManager.CloneCondition(
                ResolveExperimentConditionManager(true).CurrentCondition);
            if (condition != null)
            {
                condition.sessionId = sessionId;
            }

            var realLlm = Brain as SceneTalkVR.Runtime.Services.RealLLMService;
            var experimentLink = ExperimentHistoryService.Active?.CurrentConversationLink;
            return new ConversationSettingsSnapshot
            {
                brainMode = CurrentBrainMode.ToString(),
                feedbackSensitivity = realLlm == null ? "moderate" : realLlm.FeedbackSensitivity,
                condition = condition,
                experimentId = experimentLink?.experimentId ?? string.Empty,
                experimentKind = experimentLink?.kind.ToString() ?? string.Empty,
                experimentAttemptId = experimentLink?.attemptId ?? string.Empty,
                experimentRunId = experimentLink?.runId ?? string.Empty
            };
        }

        private bool TryRestoreHistoryBrainMode(string storedMode, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(storedMode))
            {
                return true;
            }

            if (!Enum.TryParse(storedMode, true, out SceneTalkBrainRuntimeMode expectedMode))
            {
                error = $"Stored Brain mode '{storedMode}' is not supported.";
                return false;
            }

            if (expectedMode == SceneTalkBrainRuntimeMode.KeepCurrent
                || expectedMode == CurrentBrainMode)
            {
                return true;
            }

            var applier = FindFirstObjectByType<SceneTalkRuntimeConfigApplier>(FindObjectsInactive.Include);
            if (applier == null)
            {
                error = $"Brain mode '{expectedMode}' is unavailable because the runtime config applier is missing.";
                return false;
            }

            if (!applier.TryConfigureHistoryBrainMode(expectedMode, out error))
            {
                return false;
            }

            if (CurrentBrainMode != expectedMode)
            {
                error = $"Brain mode '{expectedMode}' could not be activated.";
                return false;
            }

            return true;
        }

        private static string ResolveHistoryTitle(SpringScenePayload payload)
        {
            var raw = string.IsNullOrWhiteSpace(payload?.taskType)
                ? payload?.environmentType
                : payload.taskType;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Conversation";
            }

            var words = raw.Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }

            return string.Join(" ", words);
        }

        private void EnterHistoryError(string message)
        {
            HistoryErrorMessage = message ?? "History operation failed.";
            LastError = HistoryErrorMessage;
            SetState(SceneTalkState.HistoryError);
            Debug.LogError($"[SceneTalkVR] {HistoryErrorMessage}", this);
        }

        private bool ValidateSpeechModule()
        {
            if (SpeechInput == null)
            {
                EnterError("Speech input module is missing or does not implement ISceneTalkSpeechInput.");
                return false;
            }

            return true;
        }

        private bool ValidateGenerationModules()
        {
            var experimentManager = ResolveExperimentConditionManager(false);
            if (experimentManager != null && !experimentManager.ValidateFormalProtocol(out var protocolError))
            {
                EnterError($"Formal experiment protocol is invalid: {protocolError}");
                return false;
            }

            if (Brain == null)
            {
                EnterError("Brain module is missing or does not implement ISceneTalkBrain.");
                return false;
            }

            if (ScenePresenter == null)
            {
                EnterError("Scene presenter module is missing or does not implement ISceneTalkScenePresenter.");
                return false;
            }

            if (AvatarVoice == null)
            {
                EnterError("Avatar voice module is missing or does not implement ISceneTalkAvatarVoice.");
                return false;
            }

            return true;
        }

        private bool ValidateDialogueModules()
        {
            if (Brain == null)
            {
                EnterError("Brain module is missing or does not implement ISceneTalkBrain.");
                return false;
            }

            if (AvatarVoice == null)
            {
                EnterError("Avatar voice module is missing or does not implement ISceneTalkAvatarVoice.");
                return false;
            }

            return true;
        }

        private void ClearPresentedSceneIfSupported()
        {
            if (scenePresenterModule is ISceneTalkPresentedSceneClearer presenter)
            {
                presenter.ClearPresentedScene();
            }
        }

        private bool HandleErrorOrFinish(string error, string fallbackMessage)
        {
            if (finishRequested)
            {
                ResolveExperimentConditionManager(false)?.RecordUserAction("exit");
                SetState(SceneTalkState.Finished);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var manager = ResolveExperimentConditionManager(false);
                manager?.RecordModuleFallback(fallbackMessage);
                var speechRecognitionFailure = string.Equals(
                    fallbackMessage,
                    "Speech input failed.",
                    StringComparison.Ordinal);
                if (speechRecognitionFailure)
                    manager?.RecordRecoverableTurnFailure("SpeechRecognition", error);
                manager?.RecordUserAction("skip");
                EnterError(speechRecognitionFailure
                    ? "Speech recognition failed. Please retry recording."
                    : string.IsNullOrWhiteSpace(fallbackMessage) ? error : $"{fallbackMessage} {error}");
                return true;
            }

            return false;
        }

        private bool HandleAvatarVoiceErrorOrFinish(
            string error,
            SpringScenePayload payload,
            bool isOpeningReply)
        {
            if (finishRequested)
            {
                ResolveExperimentConditionManager(false)?.RecordUserAction("exit");
                SetState(SceneTalkState.Finished);
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            var manager = ResolveExperimentConditionManager(false);
            manager?.RecordModuleFallback("Avatar voice playback failed.");
            CaptureAvatarPlaybackRetry(payload, isOpeningReply, error);
            Debug.LogError($"[SceneTalkVR] Avatar voice playback failed: {error}", this);
            EnterError(PlaybackRetryMessage(pendingAvatarFailureStage));
            return true;
        }

        private IEnumerator RetryAvatarReplyPlayback()
        {
            var sourcePayload = pendingAvatarReplyPayload;
            var retryKind = pendingRetryKind;
            var payload = retryKind == RetryKind.AvatarFullReplyPlayback
                ? sourcePayload
                : BuildDialogueOnlyRetryPayload(sourcePayload);
            var isOpeningReply = pendingAvatarReplyIsOpening;
            LastError = string.Empty;
            finishRequested = false;
            (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
            AvatarReplyContext?.SetReplyContext(isOpeningReply);
            SetState(SceneTalkState.DialogueSpeaking);

            string error = null;
            yield return AvatarVoice.PresentReply(
                payload,
                () => { },
                message => error = message);

            if (!string.IsNullOrWhiteSpace(error))
            {
                currentTurn = null;
                CaptureAvatarPlaybackRetry(sourcePayload, isOpeningReply, error);
                Debug.LogError($"[SceneTalkVR] Avatar reply retry failed: {error}", this);
                EnterError(PlaybackRetryMessage(pendingAvatarFailureStage));
                yield break;
            }

            currentTurn = null;
            ClearPendingRetry();
            EnterTurnReviewState();
        }

        private void CaptureAvatarPlaybackRetry(
            SpringScenePayload payload,
            bool isOpeningReply,
            string error)
        {
            var stage = avatarVoiceModule is ISceneTalkAvatarPlaybackDiagnostics diagnostics
                ? diagnostics.LastFailureStage
                : AvatarReplyPlaybackFailureStage.DialogueReply;
            pendingRetryKind = stage == AvatarReplyPlaybackFailureStage.Setup
                || stage == AvatarReplyPlaybackFailureStage.CorrectionFeedback
                    ? RetryKind.AvatarFullReplyPlayback
                    : RetryKind.AvatarDialoguePlayback;
            pendingAvatarReplyPayload = payload;
            pendingAvatarReplyIsOpening = isOpeningReply;
            pendingAvatarFailureStage = stage;
            ResolveExperimentConditionManager(false)?.RecordRecoverableTurnFailure(
                stage == AvatarReplyPlaybackFailureStage.CorrectionFeedback
                    ? "CorrectionPlayback"
                    : stage == AvatarReplyPlaybackFailureStage.Setup
                        ? "AvatarSetup"
                        : "DialoguePlayback",
                error);
            (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
        }

        private static string PlaybackRetryMessage(AvatarReplyPlaybackFailureStage stage) =>
            stage == AvatarReplyPlaybackFailureStage.CorrectionFeedback
                ? "Correction voice playback failed. Please retry."
                : "Avatar voice playback failed. Please retry.";

        private static SpringScenePayload BuildDialogueOnlyRetryPayload(SpringScenePayload source)
        {
            if (source == null)
            {
                return null;
            }

            return new SpringScenePayload
            {
                taskType = source.taskType,
                environmentType = source.environmentType,
                dialogueReply = source.dialogueReply,
                dialogueContinuation = source.dialogueContinuation,
                avatarRole = source.avatarRole,
                scene = source.scene,
                dialoguePacing = source.dialoguePacing,
                correctionFeedback = new CorrectionFeedbackData { hasFeedback = false }
            };
        }

        private void ClearPendingRetry()
        {
            pendingRetryKind = RetryKind.None;
            pendingAvatarReplyPayload = null;
            pendingAvatarReplyIsOpening = false;
            pendingAvatarFailureStage = AvatarReplyPlaybackFailureStage.None;
        }

        private static bool HasGenerationFailure(string error, SpringScenePayload payload)
        {
            return !string.IsNullOrWhiteSpace(error)
                || payload == null
                || string.IsNullOrWhiteSpace(payload.dialogueReply);
        }

        private IEnumerator RecoverFromLlmFailure(string error, string fallbackMessage)
        {
            CancelableBrain?.CancelActiveGeneration();
            (AvatarVoice as ISceneTalkStreamingAvatarVoice)?.AbortStreaming();
            AvatarThinkingState?.SetThinking(false);

            if (finishRequested)
            {
                currentTurn = null;
                SetState(SceneTalkState.Finished);
                yield break;
            }

            var technicalMessage = string.IsNullOrWhiteSpace(error)
                ? "LLM returned an empty dialogue payload."
                : error.Trim();
            var manager = ResolveExperimentConditionManager(false);
            manager?.RecordModuleFallback(fallbackMessage);

            LastError = string.Empty;
            SetState(SceneTalkState.AvatarSpeaking);
            var recoveryError = string.Empty;
            var prompt = string.IsNullOrWhiteSpace(llmFailurePrompt)
                ? "Sorry, I didn't catch that. Could you say it again?"
                : llmFailurePrompt.Trim();

            if (AvatarRecoveryVoice != null)
            {
                yield return AvatarRecoveryVoice.PresentRecoveryPrompt(
                    prompt,
                    () => { },
                    message => recoveryError = message);
            }
            else if (AvatarVoice != null)
            {
                var recoveryPayload = new SpringScenePayload
                {
                    dialogueReply = prompt,
                    avatarRole = LastScenePayload?.avatarRole ?? new AvatarRoleData(),
                    scene = LastScenePayload?.scene ?? new ScenePayload(),
                    correctionFeedback = new CorrectionFeedbackData { hasFeedback = false }
                };
                yield return AvatarVoice.PresentReply(
                    recoveryPayload,
                    () => { },
                    message => recoveryError = message);
            }
            else
            {
                recoveryError = "Avatar recovery voice is unavailable.";
            }

            if (!string.IsNullOrWhiteSpace(recoveryError))
            {
                Debug.LogWarning($"[SceneTalkVR] LLM recovery prompt playback failed: {recoveryError}", this);
            }

            Debug.LogError($"[SceneTalkVR] {fallbackMessage} {technicalMessage}", this);
            currentTurn = null;
            IsAwaitingTurnReviewAction = false;
            LastError = ResolveLlmFailureUiMessage();
            SetState(SceneTalkState.Error);
        }

        private string ResolveLlmFailureUiMessage()
        {
            return IsTaskAttemptTechnicalInvalid
                ? TechnicalInvalidAttemptError
                : "Please try again.";
        }

        private void EnterError(string message)
        {
            AvatarThinkingState?.SetThinking(false);
            LastError = message;
            IsAwaitingTurnReviewAction = false;
            SetState(SceneTalkState.Error);
            Debug.LogError($"[SceneTalkVR] {message}", this);
        }

        private void BeginExperimentTurnForRecording()
        {
            var manager = ResolveExperimentConditionManager(true);
            if (manager == null)
            {
                return;
            }

            manager.BeginTurn();
            ApplyExperimentConditionToModules();
            manager.BeginRecording();
        }

        private void EnsureExperimentTurnStarted()
        {
            var manager = ResolveExperimentConditionManager(true);
            if (manager == null)
            {
                return;
            }

            manager.EnsureActiveTurn();
            ApplyExperimentConditionToModules();
        }

        private void ApplyExperimentConditionToModules()
        {
            var manager = ResolveExperimentConditionManager(true);
            if (manager == null)
            {
                return;
            }

            manager.RefreshCondition(manager.HasActiveTurn);
            manager.ApplyProviderTo(avatarVoiceModule);
            manager.ApplyAssistantEmbodimentTo(avatarVoiceModule);
            manager.InjectInto(brainModule);
            PropagateExperimentLockState(manager.IsExperimentLocked);
        }

        private void PropagateExperimentLockState(bool locked)
        {
            #if UNITY_2023_1_OR_NEWER
            var lockReceivers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            #else
            var lockReceivers = FindObjectsOfType<MonoBehaviour>(true);
            #endif
            foreach (var mono in lockReceivers)
            {
                if (mono is ISceneTalkExperimentLockReceiver receiver)
                {
                    receiver.SetExperimentLocked(locked);
                }
            }
        }

        private void ApplyExperimentConditionToPayload(SpringScenePayload payload)
        {
            var manager = ResolveExperimentConditionManager(false);
            if (payload == null || manager == null)
            {
                return;
            }

            var condition = manager.CurrentCondition;
            if (condition == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.taskType))
            {
                payload.taskType = condition.scenarioId;
            }

            if (string.IsNullOrWhiteSpace(payload.environmentType) && condition.task != null)
            {
                payload.environmentType = condition.task.fallbackEnvironmentType;
            }

            if (payload.correctionFeedback == null)
            {
                payload.correctionFeedback = new CorrectionFeedbackData
                {
                    hasFeedback = false
                };
            }

            payload.correctionFeedback.provider = condition.provider;
            payload.correctionFeedback.style = condition.style;
        }

        private void PrepareCorrectionReview(SpringScenePayload payload)
        {
            var feedback = payload == null ? null : payload.correctionFeedback;
            LastCorrectionHasFeedback = feedback != null && feedback.hasFeedback;
            LastCorrectionProvider = ResolveNonEmpty(feedback == null ? null : feedback.provider, "none");
            LastCorrectionStyle = ResolveNonEmpty(feedback == null ? null : feedback.style, "none");
            LastCorrectionDisplayText = ResolveCorrectionDisplayText(feedback);
            LastCorrectionStatus = LastCorrectionHasFeedback
                ? $"Feedback: {LastCorrectionProvider} / {LastCorrectionStyle}"
                : "No correction feedback this turn.";
            IsAwaitingTurnReviewAction = false;

            ResolveExperimentConditionManager(false)?.RecordCorrectionPayload(payload);
        }

        private void RecordSpeechMetadataHelper(string transcript)
        {
            if (speechInputModule is GatewaySpeechInputModule gatewayStt)
            {
                var response = gatewayStt.LastSttResponse;
                float confidence = response != null ? response.confidence : 1.0f;
                string sttProv = response != null ? response.provider : "unknown";
                string fallbackLvl = response != null ? response.fallbackLevel : "none";
                string suppressionReason = "";
                ResolveExperimentConditionManager(false)?.RecordSpeechMetadata(
                    transcript,
                    confidence,
                    sttProv,
                    fallbackLvl,
                    suppressionReason);
            }
        }

        private void EnterTurnReviewState()
        {
            ClearPendingRetry();
            var manager = ResolveExperimentConditionManager(false);
            var completedTurnId = manager?.CurrentTurnId;
            manager?.CompleteActiveTurn();
            var lifecycle = manager?.LifecycleCoordinator;
            GoalEvaluationOrchestrator.NotifyDialogueTurnCompleted(
                lifecycle,
                PilotWorkflowCoordinator.Active,
                completedTurnId);
            if (lifecycle != null && lifecycle.ShouldEndForLimit(out var limitReason))
            {
                lifecycle.NotifyTaskLimitReached(limitReason);
            }
            IsAwaitingTurnReviewAction = false;
            if (string.IsNullOrWhiteSpace(LastCorrectionStatus))
            {
                LastCorrectionStatus = "Ready for your next line.";
            }

            SetState(SceneTalkState.TurnReview);
        }

        private void RecordContinueIfReviewPending()
        {
            if (!IsAwaitingTurnReviewAction)
            {
                return;
            }

            IsAwaitingTurnReviewAction = false;
            ResolveExperimentConditionManager(false)?.RecordUserAction("continue");
        }

        private ExperimentConditionManager ResolveExperimentConditionManager(bool createIfMissing)
        {
            if (experimentConditionManager == null)
            {
                experimentConditionManager = GetComponent<ExperimentConditionManager>();
            }

            if (experimentConditionManager == null)
            {
                experimentConditionManager = FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
            }

            if (experimentConditionManager == null && createIfMissing)
            {
                experimentConditionManager = gameObject.AddComponent<ExperimentConditionManager>();
            }

            return experimentConditionManager;
        }

        private LearningMemoryService ResolveLearningMemoryService(bool createIfMissing)
        {
            if (learningMemoryService == null)
            {
                learningMemoryService = GetComponent<LearningMemoryService>();
            }

            if (learningMemoryService == null)
            {
                learningMemoryService = FindFirstObjectByType<LearningMemoryService>(FindObjectsInactive.Include);
            }

            if (learningMemoryService == null && createIfMissing)
            {
                learningMemoryService = gameObject.AddComponent<LearningMemoryService>();
            }

            return learningMemoryService;
        }

        private void SubscribeExperimentConditionChanges()
        {
            var manager = ResolveExperimentConditionManager(false);
            if (manager == subscribedExperimentConditionManager)
            {
                return;
            }

            UnsubscribeExperimentConditionChanges();
            subscribedExperimentConditionManager = manager;
            if (subscribedExperimentConditionManager != null)
            {
                subscribedExperimentConditionManager.ExperimentConditionChanged += OnExperimentConditionChanged;
            }
        }

        private void UnsubscribeExperimentConditionChanges()
        {
            if (subscribedExperimentConditionManager != null)
            {
                subscribedExperimentConditionManager.ExperimentConditionChanged -= OnExperimentConditionChanged;
                subscribedExperimentConditionManager = null;
            }
        }

        private void OnExperimentConditionChanged()
        {
            var manager = ResolveExperimentConditionManager(false);
            if (manager == null
                || currentTurn != null
                || IsSpeechRecording
                || manager.HasActiveTurn
                || manager.HasPendingTurnReview)
            {
                RefreshUi();
                return;
            }

            ApplyExperimentConditionToModules();
            RefreshUi();
        }

        private void ClearCorrectionReviewState()
        {
            LastCorrectionStatus = string.Empty;
            LastCorrectionDisplayText = string.Empty;
            LastCorrectionProvider = string.Empty;
            LastCorrectionStyle = string.Empty;
            LastCorrectionHasFeedback = false;
            IsAwaitingTurnReviewAction = false;
        }

        private void SubscribeAvatarCorrectionPlayback()
        {
            var next = avatarVoiceModule as AvatarPresentationVoiceModule;
            if (subscribedAvatarVoiceModule == next)
            {
                return;
            }

            UnsubscribeAvatarCorrectionPlayback();
            subscribedAvatarVoiceModule = next;
            if (subscribedAvatarVoiceModule != null)
            {
                subscribedAvatarVoiceModule.CorrectionPlaybackCompleted += OnCorrectionPlaybackCompleted;
            }
        }

        private void UnsubscribeAvatarCorrectionPlayback()
        {
            if (subscribedAvatarVoiceModule != null)
            {
                subscribedAvatarVoiceModule.CorrectionPlaybackCompleted -= OnCorrectionPlaybackCompleted;
                subscribedAvatarVoiceModule = null;
            }
        }

        private void OnCorrectionPlaybackCompleted(CorrectionPlaybackResult result)
        {
            if (result == null)
            {
                return;
            }

            LastCorrectionProvider = ResolveNonEmpty(result.provider, LastCorrectionProvider);
            LastCorrectionStatus = string.IsNullOrWhiteSpace(result.errorCode)
                ? $"Feedback {result.outcome}: {LastCorrectionProvider}"
                : $"Feedback {result.outcome}: {result.errorCode}";

            ResolveExperimentConditionManager(false)?.RecordCorrectionPlayback(
                result.provider,
                result.outcome,
                result.errorCode);

            if (CurrentState == SceneTalkState.CorrectionFeedbackSpeaking)
            {
                SetState(SceneTalkState.DialogueSpeaking);
            }
        }

        private string ResolveCorrectionDisplayText(CorrectionFeedbackData feedback)
        {
            if (feedback == null || !feedback.hasFeedback)
            {
                return string.Empty;
            }

            var style = ResolveNonEmpty(feedback.style, LastCorrectionStyle);
            var manager = ResolveExperimentConditionManager(false);
            if (string.Equals(style, ExperimentConditionManager.RecastStyle, System.StringComparison.OrdinalIgnoreCase)
                && (manager == null || !manager.ShowDebugLabel))
            {
                return string.Empty;
            }

            return ResolveNonEmpty(feedback.correctedText, feedback.feedbackText);
        }

        private static string ResolveNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }

        private void SetState(SceneTalkState state)
        {
            if (experimentExitConfirmationActive
                && CurrentState == SceneTalkState.ExperimentExitConfirm
                && state != SceneTalkState.ExperimentExitConfirm)
            {
                deferredStateDuringExperimentExit = state;
                return;
            }

            CurrentState = state;
            stateChanged.Invoke(state);
            RefreshUi();
        }

        private void ClearExperimentExitConfirmation()
        {
            experimentExitConfirmationActive = false;
            deferredStateDuringExperimentExit = null;
        }

        private void RefreshUi()
        {
            if (stateLabel != null)
            {
                stateLabel.text = ShouldShowExperimentDebug
                    ? $"State: {CurrentState}\nCondition: {ExperimentDebugLabel}"
                    : $"State: {CurrentState}";
            }

            if (transcriptLabel != null)
            {
                transcriptLabel.text = string.IsNullOrWhiteSpace(LastTranscript)
                    ? "Transcript: -"
                    : $"Transcript: {LastTranscript}";
            }

            if (replyLabel != null)
            {
                var reply = LastScenePayload == null ? string.Empty : LastScenePayload.dialogueReply;
                replyLabel.text = string.IsNullOrWhiteSpace(reply)
                    ? "Avatar: -"
                    : $"Avatar: {reply}";
            }

            if (errorLabel != null)
            {
                errorLabel.text = string.IsNullOrWhiteSpace(LastError) ? string.Empty : $"Error: {LastError}";
            }
        }

        private IEnumerator GenerateSceneAndReplyWithStreamingSupport(
            string transcript,
            Action<SpringScenePayload> onCompleteCallback,
            Action<string> onErrorCallback)
        {
            var streamingBrain = Brain as ISceneTalkStreamingBrain;
            var streamingVoice = AvatarVoice as ISceneTalkStreamingAvatarVoice;

            if (streamingBrain != null && streamingVoice != null)
            {
                var basePayload = LastScenePayload;
                streamingVoice.PrepareStreaming(basePayload);

                SpringScenePayload finalPayload = null;
                string brainError = null;

                string accumulatedSubtitle = string.Empty;
                Action<string> sentenceReady = sentence => {
                        streamingVoice.EnqueueSentence(sentence);
                        accumulatedSubtitle += (string.IsNullOrEmpty(accumulatedSubtitle) ? "" : " ") + sentence;
                        if (replyLabel != null)
                        {
                            replyLabel.text = $"Avatar: {accumulatedSubtitle}";
                        }
                    };
                Action<SpringScenePayload> generationComplete = payload => {
                        finalPayload = payload;
                    };
                Action<string> generationError = err => {
                        brainError = err;
                    };

                try
                {
                    if (streamingBrain is ISceneTalkFeedbackFirstStreamingBrain feedbackFirstBrain
                        && streamingVoice is ISceneTalkFeedbackFirstStreamingAvatarVoice feedbackFirstVoice)
                    {
                        yield return feedbackFirstBrain.GenerateFeedbackFirstStreaming(
                            transcript,
                            feedbackFirstVoice.ResolveCorrectionPlan,
                            sentenceReady,
                            generationComplete,
                            generationError);
                    }
                    else
                    {
                        yield return streamingBrain.GenerateSceneAndReplyStreaming(
                            transcript,
                            sentenceReady,
                            generationComplete,
                            generationError);
                    }
                }
                finally
                {
                    if (string.IsNullOrWhiteSpace(brainError)
                        && finalPayload != null
                        && !string.IsNullOrWhiteSpace(finalPayload.dialogueReply))
                    {
                        streamingVoice.CompleteStreaming(finalPayload.dialogueReply);
                    }
                    else
                    {
                        streamingVoice.AbortStreaming();
                    }
                }

                if (!string.IsNullOrEmpty(brainError))
                {
                    onErrorCallback?.Invoke(brainError);
                    yield break;
                }

                if (finalPayload == null || string.IsNullOrWhiteSpace(finalPayload.dialogueReply))
                {
                    onErrorCallback?.Invoke("LLM generation completed without a dialogue reply.");
                    yield break;
                }

                onCompleteCallback?.Invoke(finalPayload);
            }
            else
            {
                yield return Brain.GenerateSceneAndReply(
                    transcript,
                    onCompleteCallback,
                    onErrorCallback
                );
            }
        }

        private void RecordTurnMetrics(SpringScenePayload payload)
        {
            var expManager = ResolveExperimentConditionManager(false);
            if (expManager != null && payload != null)
            {
                var realLLM = Brain as SceneTalkVR.Runtime.Services.RealLLMService;
                var voiceModule = AvatarVoice as SceneTalkVR.AvatarSystem.AvatarPresentationVoiceModule;

                string dialogueContinuation = payload.dialogueReply;
                string recastText = (payload.correctionFeedback != null) ? payload.correctionFeedback.recastText : string.Empty;
                
                // Compatibility columns remain empty; the event JSONL is the authoritative clock.
                string correctionRequestStartTime = string.Empty;
                string dialogueRequestStartTime = string.Empty;

                string firstTokenTime = (realLLM != null && realLLM.LastFirstTokenLatencyMs >= 0) 
                    ? realLLM.LastFirstTokenLatencyMs.ToString("F2") : "n/a";
                string firstSentenceTime = (realLLM != null && realLLM.LastFirstSentenceLatencyMs >= 0)
                    ? realLLM.LastFirstSentenceLatencyMs.ToString("F2") : "n/a";
                string ttsReadyTime = (voiceModule != null && voiceModule.LastTtsReadyLatencyMs >= 0)
                    ? voiceModule.LastTtsReadyLatencyMs.ToString("F2") : "n/a";

                string correctionPlayStartTime = (voiceModule != null && voiceModule.LastCorrectionPlayStart >= 0)
                    ? voiceModule.LastCorrectionPlayStart.ToString("F2") : "n/a";
                string correctionPlayEndTime = (voiceModule != null && voiceModule.LastCorrectionPlayEnd >= 0)
                    ? voiceModule.LastCorrectionPlayEnd.ToString("F2") : "n/a";
                string dialoguePlayStartTime = (voiceModule != null && voiceModule.LastDialoguePlayStart >= 0)
                    ? voiceModule.LastDialoguePlayStart.ToString("F2") : "n/a";
                string dialoguePlayEndTime = (voiceModule != null && voiceModule.LastDialoguePlayEnd >= 0)
                    ? voiceModule.LastDialoguePlayEnd.ToString("F2") : "n/a";

                bool hasCorrection = payload.correctionFeedback != null && payload.correctionFeedback.hasFeedback;
                string playbackOrder = hasCorrection ? "Correction -> Dialogue" : "Dialogue Only";

                float userEndToFeedbackAudioMs = 0f;
                float userEndToDialogueAudioMs = 0f;
                float feedbackToDialogueGapMs = 0f;

                if (voiceModule != null)
                {
                    if (voiceModule.LastCorrectionPlayStart >= 0)
                    {
                        userEndToFeedbackAudioMs = (voiceModule.LastCorrectionPlayStart - LastSpeechCaptureEndTime) * 1000f;
                    }
                    if (voiceModule.LastDialoguePlayStart >= 0)
                    {
                        userEndToDialogueAudioMs = (voiceModule.LastDialoguePlayStart - LastSpeechCaptureEndTime) * 1000f;
                    }
                    if (voiceModule.LastDialoguePlayStart >= 0 && voiceModule.LastCorrectionPlayEnd >= 0)
                    {
                        feedbackToDialogueGapMs = (voiceModule.LastDialoguePlayStart - voiceModule.LastCorrectionPlayEnd) * 1000f;
                    }
                }

                string correctionVoiceId = string.Empty;
                string actualPlaybackSubject = string.Empty;
                if (hasCorrection && payload.correctionFeedback != null)
                {
                    actualPlaybackSubject = (payload.correctionFeedback.provider == "assistant_agent") ? "Agent" : "Avatar";
                }

                string timeoutReason = string.Empty;
                string fallbackReason = voiceModule?.LastCorrectionPlaybackResult?.outcome ?? string.Empty;
                string failureReason = voiceModule?.LastCorrectionPlaybackResult?.errorCode ?? string.Empty;

                expManager.RecordDetailMetrics(
                    dialogueContinuation,
                    recastText,
                    correctionRequestStartTime,
                    dialogueRequestStartTime,
                    firstTokenTime,
                    firstSentenceTime,
                    ttsReadyTime,
                    correctionPlayStartTime,
                    correctionPlayEndTime,
                    dialoguePlayStartTime,
                    dialoguePlayEndTime,
                    playbackOrder,
                    userEndToFeedbackAudioMs,
                    userEndToDialogueAudioMs,
                    feedbackToDialogueGapMs,
                    correctionVoiceId,
                    actualPlaybackSubject,
                    timeoutReason,
                    fallbackReason,
                    failureReason);
            }
        }
    }
}
