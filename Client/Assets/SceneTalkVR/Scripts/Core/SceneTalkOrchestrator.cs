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

        private Coroutine currentTurn;
        private bool finishRequested;

        private ISceneTalkSpeechInput SpeechInput => speechInputModule as ISceneTalkSpeechInput;
        private ISceneTalkBrain Brain => brainModule as ISceneTalkBrain;
        private ISceneTalkScenePresenter ScenePresenter => scenePresenterModule as ISceneTalkScenePresenter;
        private ISceneTalkAvatarVoice AvatarVoice => avatarVoiceModule as ISceneTalkAvatarVoice;

        private void Awake()
        {
            RefreshUi();
        }

        public void StartPractice()
        {
            finishRequested = false;
            StartPracticeTurn();
        }

        public void StartPracticeTurn()
        {
            if (currentTurn != null)
            {
                StopCoroutine(currentTurn);
            }

            currentTurn = StartCoroutine(RunPracticeTurn());
        }

        public void FinishPractice()
        {
            finishRequested = true;

            if (currentTurn != null)
            {
                StopCoroutine(currentTurn);
                currentTurn = null;
            }

            SetState(SceneTalkState.Finished);
        }

        public void RetryAfterError()
        {
            if (CurrentState == SceneTalkState.Error)
            {
                StartPracticeTurn();
            }
        }

        private IEnumerator RunPracticeTurn()
        {
            LastTranscript = string.Empty;
            LastScenePayload = null;
            LastError = string.Empty;
            RefreshUi();

            if (!ValidateModules())
            {
                currentTurn = null;
                yield break;
            }

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

            SetState(SceneTalkState.AvatarSpeaking);

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
            SetState(finishRequested ? SceneTalkState.Finished : SceneTalkState.Listening);
        }

        private bool ValidateModules()
        {
            if (SpeechInput == null)
            {
                EnterError("Speech input module is missing or does not implement ISceneTalkSpeechInput.");
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
