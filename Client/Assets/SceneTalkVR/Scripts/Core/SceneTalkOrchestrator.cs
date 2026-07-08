using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkOrchestrator : MonoBehaviour
    {
        [Header("Module adapters")]
        [SerializeField] private MonoBehaviour speechInputModule;
        [SerializeField] private MonoBehaviour brainModule;
        [SerializeField] private MonoBehaviour scenePresenterModule;
        [SerializeField] private MonoBehaviour avatarVoiceModule;

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
        public bool IsTurnRunning => currentTurn != null;
        public bool IsDialogueActive { get; private set; }

        private Coroutine currentTurn;
        private bool finishRequested;

        private ISceneTalkSpeechInput SpeechInput => speechInputModule as ISceneTalkSpeechInput;
        private ISceneTalkBrain Brain => brainModule as ISceneTalkBrain;
        private ISceneTalkScenePresenter ScenePresenter => scenePresenterModule as ISceneTalkScenePresenter;
        private ISceneTalkAvatarVoice AvatarVoice => avatarVoiceModule as ISceneTalkAvatarVoice;
        private ISceneTalkAvatarReplyContext AvatarReplyContext => avatarVoiceModule as ISceneTalkAvatarReplyContext;
        private ISceneTalkAvatarSessionReset AvatarSessionReset => avatarVoiceModule as ISceneTalkAvatarSessionReset;

        private void Awake()
        {
            RefreshUi();
        }

        public void StartPractice()
        {
            finishRequested = false;
            StartListeningTurn();
        }

        public void StartPracticeTurn()
        {
            StartListeningTurn();
        }

        public void RetryListening()
        {
            if (IsDialogueActive)
            {
                StartDialogueTurn();
                return;
            }

            StartListeningTurn();
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
            if (currentTurn != null)
            {
                return;
            }

            if (!IsDialogueActive)
            {
                StartListeningTurn();
                return;
            }

            if (!ValidateSpeechModule() || !ValidateDialogueModules())
            {
                return;
            }

            finishRequested = false;
            currentTurn = StartCoroutine(RunDialogueTurn());
        }

        public void FinishPractice()
        {
            ReturnToInitialMenu();
        }

        public void ReturnToInitialMenu()
        {
            finishRequested = true;

            if (currentTurn != null)
            {
                StopCoroutine(currentTurn);
                currentTurn = null;
            }

            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;
            IsDialogueActive = false;
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

        private void StartListeningTurn()
        {
            if (currentTurn != null)
            {
                StopCoroutine(currentTurn);
            }

            if (!ValidateSpeechModule())
            {
                currentTurn = null;
                return;
            }

            finishRequested = false;
            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;
            IsDialogueActive = false;
            currentTurn = StartCoroutine(RunSpeechCaptureTurn());
        }

        private IEnumerator RunSpeechCaptureTurn()
        {
            LastTranscript = string.Empty;
            LastError = string.Empty;
            RefreshUi();

            SetState(SceneTalkState.Listening);

            string transcript = null;
            string error = null;
            yield return SpeechInput.CaptureSpeech(
                value => transcript = value,
                message => error = message);

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
            SetState(SceneTalkState.Processing);

            var transcript = LastTranscript;
            string error = null;
            SpringScenePayload payload = null;
            yield return Brain.GenerateSceneAndReply(
                transcript,
                value => payload = value,
                message => error = message);

            if (HandleErrorOrFinish(error, "LLM/scene generation failed."))
            {
                currentTurn = null;
                yield break;
            }

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
            SetState(SceneTalkState.AvatarSpeaking);

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
            SetState(SceneTalkState.AvatarSpeaking);
        }

        private IEnumerator RunDialogueTurn()
        {
            LastError = string.Empty;
            RefreshUi();

            SetState(SceneTalkState.Listening);

            string transcript = null;
            string error = null;
            yield return SpeechInput.CaptureSpeech(
                value => transcript = value,
                message => error = message);

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
            yield return Brain.GenerateSceneAndReply(
                transcript,
                value => payload = value,
                message => error = message);

            if (HandleErrorOrFinish(error, "Dialogue reply generation failed."))
            {
                currentTurn = null;
                yield break;
            }

            LastScenePayload = payload;
            RefreshUi();
            SetState(SceneTalkState.AvatarSpeaking);

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
            SetState(SceneTalkState.AvatarSpeaking);
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
                SetState(SceneTalkState.Finished);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                EnterError(string.IsNullOrWhiteSpace(fallbackMessage) ? error : $"{fallbackMessage} {error}");
                return true;
            }

            return false;
        }

        private void EnterError(string message)
        {
            LastError = message;
            SetState(SceneTalkState.Error);
            Debug.LogError($"[SceneTalkVR] {message}", this);
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
                stateLabel.text = $"State: {CurrentState}";
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
