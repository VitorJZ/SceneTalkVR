using System.Collections;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
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
        public string LastTranscript { get; private set; }
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

        private Coroutine currentTurn;
        private bool finishRequested;
        private AvatarPresentationVoiceModule subscribedAvatarVoiceModule;
        private SpeechCaptureMode activeSpeechCaptureMode = SpeechCaptureMode.None;

        private ISceneTalkSpeechInput SpeechInput => speechInputModule as ISceneTalkSpeechInput;
        private ISceneTalkManualSpeechInput ManualSpeechInput => speechInputModule as ISceneTalkManualSpeechInput;
        private ISceneTalkBrain Brain => brainModule as ISceneTalkBrain;
        private ISceneTalkScenePresenter ScenePresenter => scenePresenterModule as ISceneTalkScenePresenter;
        private ISceneTalkAvatarVoice AvatarVoice => avatarVoiceModule as ISceneTalkAvatarVoice;
        private ISceneTalkAvatarReplyContext AvatarReplyContext => avatarVoiceModule as ISceneTalkAvatarReplyContext;
        private ISceneTalkAvatarSessionReset AvatarSessionReset => avatarVoiceModule as ISceneTalkAvatarSessionReset;

        private void Awake()
        {
            ResolveExperimentConditionManager(true);
            RefreshUi();
        }

        private void OnEnable()
        {
            SubscribeAvatarCorrectionPlayback();
        }

        private void OnDisable()
        {
            UnsubscribeAvatarCorrectionPlayback();
        }

        public void OpenSettings()
        {
            if (currentTurn != null || IsSpeechRecording)
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
            yield return Brain.GenerateSceneAndReply(
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

            SpringScenePayload payload = null;
            error = null;
            ApplyExperimentConditionToModules();
            yield return Brain.GenerateSceneAndReply(
                transcript,
                value => payload = value,
                message => error = message);

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
            IsAwaitingTurnReviewAction = LastCorrectionHasFeedback;
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
    }
}
