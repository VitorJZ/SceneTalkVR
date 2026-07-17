using System;
using System.Collections;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkOrchestrator : MonoBehaviour
    {
        private enum SpeechCaptureMode
        {
            None,
            Request,
            Dialogue
        }

        [Header("Module adapters")]
        [SerializeField] private MonoBehaviour speechInputModule;
        [SerializeField] private MonoBehaviour brainModule;
        [SerializeField] private MonoBehaviour scenePresenterModule;
        [SerializeField] private MonoBehaviour avatarVoiceModule;

        [Header("Experiment")]
        [SerializeField] private ExperimentConditionManager experimentConditionManager;

        [Header("Optional UI")]
        [SerializeField] private Text stateLabel;
        [SerializeField] private Text transcriptLabel;
        [SerializeField] private Text replyLabel;
        [SerializeField] private Text errorLabel;

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
        public bool IsDialogueActive { get; private set; }
        public bool IsSpeechRecording { get; private set; }
        public bool IsAwaitingTurnReviewAction { get; private set; }
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

        private ISceneTalkSpeechInput SpeechInput => speechInputModule as ISceneTalkSpeechInput;
        private ISceneTalkManualSpeechInput ManualSpeechInput => speechInputModule as ISceneTalkManualSpeechInput;
        private ISceneTalkBrain Brain => brainModule as ISceneTalkBrain;
        private ISceneTalkScenePresenter ScenePresenter => scenePresenterModule as ISceneTalkScenePresenter;
        private ISceneTalkAvatarVoice AvatarVoice => avatarVoiceModule as ISceneTalkAvatarVoice;
        private ISceneTalkAvatarReplyContext AvatarReplyContext => avatarVoiceModule as ISceneTalkAvatarReplyContext;
        private ISceneTalkAvatarThinkingState AvatarThinkingState => avatarVoiceModule as ISceneTalkAvatarThinkingState;
        private ISceneTalkAvatarSessionReset AvatarSessionReset => avatarVoiceModule as ISceneTalkAvatarSessionReset;

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
            ResolveExperimentConditionManager(true);
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

        public void CloseSettings()
        {
            if (CurrentState == SceneTalkState.Settings)
            {
                SetState(SceneTalkState.Idle);
            }
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

        private IEnumerator RunFixedTaskStartup(string taskId)
        {
            LastScenePayload = null;
            LastError = string.Empty;
            
            var manager = ResolveExperimentConditionManager(true);
            if (manager != null)
            {
                manager.SelectTask(taskId);
            }

            EnsureExperimentTurnStarted();
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

            if (HandleErrorOrFinish(error, "Avatar voice playback failed."))
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
                RequestStopSpeechCapture();
                return;
            }

            BeginRequestSpeechCapture();
        }

        public void ToggleDialogueSpeechCapture()
        {
            if (IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.Dialogue)
            {
                RequestStopSpeechCapture();
                return;
            }

            BeginDialogueSpeechCapture();
        }

        public bool TryBeginControllerSpeechCapture()
        {
            if (IsSpeechRecording || currentTurn != null)
            {
                return false;
            }

            if (IsDialogueActive)
            {
                return BeginDialogueSpeechCapture();
            }

            if (CurrentState == SceneTalkState.Listening || CurrentState == SceneTalkState.Error)
            {
                return BeginRequestSpeechCapture();
            }

            return false;
        }

        public bool TryEndControllerSpeechCapture()
        {
            if (!IsSpeechRecording)
            {
                return false;
            }

            RequestStopSpeechCapture();
            return true;
        }

        public bool CanUseControllerSpeechCapture()
        {
            if (IsSpeechRecording)
            {
                return true;
            }

            if (currentTurn != null)
            {
                return false;
            }

            if (IsDialogueActive)
            {
                return CurrentState == SceneTalkState.TurnReview
                    || CurrentState == SceneTalkState.AvatarSpeaking
                    || CurrentState == SceneTalkState.Error;
            }

            return CurrentState == SceneTalkState.Listening || CurrentState == SceneTalkState.Error;
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

        public void ReturnToInitialMenu()
        {
            finishRequested = true;
            ResolveExperimentConditionManager(false)?.RecordUserAction("exit");
            CancelActiveSpeechCapture();

            if (currentTurn != null)
            {
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

            SetState(SceneTalkState.Idle);
        }

        public void RetryAfterError()
        {
            if (CurrentState == SceneTalkState.Error)
            {
                RetryListening();
            }
        }

        private void EnterRequestReadyState(bool clearTranscript)
        {
            if (currentTurn != null)
            {
                CancelActiveSpeechCapture();
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
            if (currentTurn != null)
            {
                if (IsSpeechRecording && activeSpeechCaptureMode == SpeechCaptureMode.Request)
                {
                    RequestStopSpeechCapture();
                    return true;
                }

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
            SetState(SceneTalkState.Recording);
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

            if (HandleErrorOrFinish(error, "LLM/scene generation failed."))
            {
                currentTurn = null;
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

            if (HandleErrorOrFinish(error, "Avatar voice playback failed."))
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
            RecordSpeechMetadataHelper(transcript);

            if (HandleErrorOrFinish(error, "Speech input failed."))
            {
                currentTurn = null;
                yield break;
            }

            LastTranscript = transcript;
            RefreshUi();
            SetState(SceneTalkState.Processing);
            AvatarThinkingState?.SetThinking(true);

            SpringScenePayload payload = null;
            error = null;
            ApplyExperimentConditionToModules();
            yield return GenerateSceneAndReplyWithStreamingSupport(
                transcript,
                value => payload = value,
                message => error = message);
            AvatarThinkingState?.SetThinking(false);

            if (HandleErrorOrFinish(error, "Dialogue reply generation failed."))
            {
                currentTurn = null;
                yield break;
            }

            ApplyExperimentConditionToPayload(payload);
            LastScenePayload = payload;
            RefreshUi();
            PrepareCorrectionReview(payload);
            SubscribeAvatarCorrectionPlayback();
            SetState(LastCorrectionHasFeedback
                ? SceneTalkState.CorrectionFeedbackSpeaking
                : SceneTalkState.DialogueSpeaking);

            error = null;
            AvatarReplyContext?.SetReplyContext(false);
            yield return AvatarVoice.PresentReply(
                payload,
                () => { },
                message => error = message);

            RecordTurnMetrics(payload);

            if (HandleErrorOrFinish(error, "Avatar voice playback failed."))
            {
                currentTurn = null;
                yield break;
            }

            currentTurn = null;
            EnterTurnReviewState();
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
            if (scenePresenterModule is SceneTalkScenePresenter presenter)
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
                manager?.RecordUserAction("skip");
                EnterError(string.IsNullOrWhiteSpace(fallbackMessage) ? error : $"{fallbackMessage} {error}");
                return true;
            }

            return false;
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
            ResolveExperimentConditionManager(false)?.CompleteActiveTurn();
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
            CurrentState = state;
            stateChanged.Invoke(state);
            RefreshUi();
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
                yield return streamingBrain.GenerateSceneAndReplyStreaming(
                    transcript,
                    sentence => {
                        streamingVoice.EnqueueSentence(sentence);
                        accumulatedSubtitle += (string.IsNullOrEmpty(accumulatedSubtitle) ? "" : " ") + sentence;
                        if (replyLabel != null)
                        {
                            replyLabel.text = $"Avatar: {accumulatedSubtitle}";
                        }
                    },
                    payload => {
                        finalPayload = payload;
                    },
                    err => {
                        brainError = err;
                    }
                );

                streamingVoice.SignalStreamingComplete();

                if (!string.IsNullOrEmpty(brainError))
                {
                    onErrorCallback?.Invoke(brainError);
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
                
                string correctionRequestStartTime = System.DateTime.UtcNow.ToString("o");
                string dialogueRequestStartTime = System.DateTime.UtcNow.ToString("o");

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
                string actualPlaybackSubject = "Avatar";
                if (hasCorrection && payload.correctionFeedback != null)
                {
                    actualPlaybackSubject = (payload.correctionFeedback.provider == "assistant_agent") ? "Agent" : "Avatar";
                    correctionVoiceId = (payload.correctionFeedback.provider == "assistant_agent") ? "WeJames" : "TencentVoice";
                }

                string timeoutReason = "none";
                string fallbackReason = "none";
                string failureReason = "none";

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
