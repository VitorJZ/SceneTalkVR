using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [DisallowMultipleComponent]
    public sealed class CorrectionFeedbackPresenter : MonoBehaviour
    {
        private const string DialogueAvatarProvider = "dialogue_avatar";
        private const string AssistantAgentProvider = "assistant_agent";
        private const string ExplicitStyle = "explicit";
        private const string RecastStyle = "recast";

        [SerializeField] private bool playCorrectionFeedback = true;
        [SerializeField] private CorrectionAgentPresenter correctionAgentPresenter;
        [SerializeField] private bool createCorrectionAgentIfMissing = true;
        [SerializeField] private string assistantAgentFallbackVoiceId = "default_female_en";

        [SerializeField] private bool debugForceFeedback;
        [SerializeField] private string debugFeedbackText = "Try saying: I'd like a latte, please.";

        private AvatarSpeechPlayer speechPlayer;
        private bool isSessionActive;
        private string currentFeedbackProvider = AssistantAgentProvider;

        private AvatarSpeechPlayer SpeechPlayer => speechPlayer ??= new AvatarSpeechPlayer();

        public string CurrentFeedbackProvider => NormalizeProvider(currentFeedbackProvider);

        private void Start()
        {
            isSessionActive = false;
            ApplyAssistantVisibility();
        }

        private void Update()
        {
            ApplyAssistantVisibility();
        }

        private void OnDisable()
        {
            ResolveCorrectionAgentPresenter(false)?.HideImmediate();
        }

        public void SetFeedbackProvider(string provider)
        {
            currentFeedbackProvider = NormalizeProvider(provider);
            ApplyAssistantVisibility();
        }

        public void SetPresentationActive(bool active)
        {
            isSessionActive = active;
            ApplyAssistantVisibility();
        }

        internal IEnumerator Present(
            SpringScenePayload payload,
            AvatarSpeechPlaybackContext playbackContext,
            Action triggerDialogueAvatarSpeaking,
            Action<CorrectionPlaybackResult> onComplete)
        {
            var feedback = payload != null ? payload.correctionFeedback : null;
            var provider = ResolveConfiguredProvider(feedback);
            currentFeedbackProvider = provider;
            ApplyAssistantVisibility();

            if (!ShouldPlayCorrectionFeedback(feedback))
            {
                if (feedback != null && feedback.hasFeedback && !playCorrectionFeedback)
                {
                    onComplete?.Invoke(Result(provider, "failed", "playback_disabled"));
                }

                yield break;
            }

            var style = ResolveEffectiveStyle(feedback);
            var text = ResolveEffectiveFeedbackText(feedback);
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[SceneTalkVR] Correction feedback text is empty.", this);
                onComplete?.Invoke(new CorrectionPlaybackResult
                {
                    provider = provider,
                    outcome = "failed",
                    errorCode = "empty_text"
                });
                yield break;
            }

            var sourceProvider = feedback != null ? feedback.provider : string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceProvider)
                && !IsSupportedProvider(sourceProvider))
            {
                Debug.LogWarning(
                    $"[SceneTalkVR] Unknown correction feedback provider '{sourceProvider}', falling back to {provider}.",
                    this);
            }

            var useDialogueAvatar = string.Equals(
                provider,
                DialogueAvatarProvider,
                StringComparison.OrdinalIgnoreCase);
            var playbackRequest = new AvatarSpeechPlaybackRequest
            {
                text = text,
                logLabel = useDialogueAvatar
                    ? "Correction feedback"
                    : "Assistant correction feedback",
                voiceIdOverride = useDialogueAvatar
                    ? null
                    : ResolveAssistantAgentFallbackVoiceId()
            };

            AvatarSpeechPlaybackResult playbackResult = null;
            if (useDialogueAvatar)
            {
                triggerDialogueAvatarSpeaking?.Invoke();
                yield return SpeechPlayer.Play(
                    playbackContext,
                    payload,
                    playbackRequest,
                    value => playbackResult = value);
            }
            else
            {
                var correctionAgent = ResolveCorrectionAgentPresenter(createCorrectionAgentIfMissing);
                if (correctionAgent != null)
                {
                    correctionAgent.ShowImmediate();
                    correctionAgent.BeginSpeaking();
                    playbackRequest.audioSourceOverride = correctionAgent.AudioSource;
                    yield return SpeechPlayer.Play(
                        playbackContext,
                        payload,
                        playbackRequest,
                        value => playbackResult = value);
                    correctionAgent.EndSpeaking();
                    ApplyAssistantVisibility();
                }
                else
                {
                    Debug.LogWarning(
                        "[SceneTalkVR] CorrectionAgentPresenter is missing; using assistant audio-only fallback.",
                        this);
                    yield return SpeechPlayer.Play(
                        playbackContext,
                        payload,
                        playbackRequest,
                        value => playbackResult = value);
                    if (playbackResult != null)
                    {
                        playbackResult.fallbackLevel = AppendFallback(
                            playbackResult.fallbackLevel,
                            "missing_agent");
                    }
                }
            }

            playbackResult ??= new AvatarSpeechPlaybackResult
            {
                fallbackLevel = "playback_result_missing",
                error = "Correction feedback playback returned no result."
            };

            if (!string.IsNullOrWhiteSpace(playbackResult.error))
            {
                Debug.LogWarning(
                    $"[SceneTalkVR] Correction feedback TTS failed without blocking the turn: {playbackResult.error}",
                    this);
            }

            Debug.Log(
                $"[SceneTalkVR] Correction feedback log: provider={provider}, style={style}, "
                + $"playbackCompleted={playbackResult.playbackCompleted}, "
                + $"ttsProvider={playbackResult.ttsProvider}, ttsLatencyMs={playbackResult.ttsLatencyMs}, "
                + $"audioDurationMs={playbackResult.audioDurationMs}, textChars={text.Length}, "
                + $"fallback={playbackResult.fallbackLevel}",
                this);

            onComplete?.Invoke(CreateCompactResult(provider, playbackResult));
        }

        public void ResetPresentation()
        {
            isSessionActive = false;
            currentFeedbackProvider = AssistantAgentProvider;
            ResolveCorrectionAgentPresenter(false)?.HideImmediate();
        }

        private bool ShouldPlayCorrectionFeedback(CorrectionFeedbackData feedback)
        {
            if (!playCorrectionFeedback)
            {
                return false;
            }

            return IsDebugForceFeedbackEnabled()
                || feedback != null && feedback.hasFeedback;
        }

        private bool IsDebugForceFeedbackEnabled()
        {
            return debugForceFeedback;
        }

        private bool ShouldKeepAssistantVisible()
        {
            return isSessionActive
                && string.Equals(
                    CurrentFeedbackProvider,
                    AssistantAgentProvider,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyAssistantVisibility()
        {
            var shouldShow = ShouldKeepAssistantVisible();
            var correctionAgent = ResolveCorrectionAgentPresenter(
                shouldShow && createCorrectionAgentIfMissing);
            if (correctionAgent == null || correctionAgent.IsVisible == shouldShow)
            {
                return;
            }

            if (shouldShow)
            {
                correctionAgent.ShowImmediate();
            }
            else
            {
                correctionAgent.HideImmediate();
            }
        }

        private string ResolveConfiguredProvider(CorrectionFeedbackData feedback)
        {
            var payloadProvider = feedback != null ? feedback.provider : string.Empty;
            return IsSupportedProvider(payloadProvider)
                ? NormalizeProvider(payloadProvider)
                : CurrentFeedbackProvider;
        }

        private string ResolveEffectiveStyle(CorrectionFeedbackData feedback)
        {
            return NormalizeStyle(feedback != null ? feedback.style : string.Empty);
        }

        private string ResolveEffectiveFeedbackText(CorrectionFeedbackData feedback)
        {
            return IsDebugForceFeedbackEnabled() && !string.IsNullOrWhiteSpace(debugFeedbackText)
                ? debugFeedbackText
                : ResolveFeedbackText(feedback);
        }

        private string ResolveAssistantAgentFallbackVoiceId()
        {
            return string.IsNullOrWhiteSpace(assistantAgentFallbackVoiceId)
                ? "default_female_en"
                : assistantAgentFallbackVoiceId;
        }

        private CorrectionAgentPresenter ResolveCorrectionAgentPresenter(bool createIfMissing)
        {
            if (correctionAgentPresenter == null)
            {
                correctionAgentPresenter = GetComponent<CorrectionAgentPresenter>();
            }

            if (correctionAgentPresenter == null && createIfMissing)
            {
                correctionAgentPresenter = gameObject.AddComponent<CorrectionAgentPresenter>();
            }

            return correctionAgentPresenter;
        }

        private static CorrectionPlaybackResult CreateCompactResult(
            string provider,
            AvatarSpeechPlaybackResult playbackResult)
        {
            var fallback = playbackResult.fallbackLevel ?? string.Empty;
            var missingAgent = fallback.IndexOf("missing_agent", StringComparison.OrdinalIgnoreCase) >= 0;
            var silentFallback = fallback.IndexOf("silent_wait", StringComparison.OrdinalIgnoreCase) >= 0;
            var demoFallback = fallback.IndexOf("demo_clip", StringComparison.OrdinalIgnoreCase) >= 0;

            if (missingAgent)
            {
                return Result(provider, "failed", "missing_agent");
            }

            if (!string.IsNullOrWhiteSpace(playbackResult.error))
            {
                return Result(provider, "failed", "playback_error");
            }

            if (silentFallback)
            {
                return Result(provider, "silent_fallback", "audio_unavailable");
            }

            if (demoFallback)
            {
                return Result(provider, "demo_fallback", string.Empty);
            }

            return playbackResult.playbackCompleted
                ? Result(provider, "played", string.Empty)
                : Result(provider, "failed", "playback_incomplete");
        }

        private static CorrectionPlaybackResult Result(
            string provider,
            string outcome,
            string errorCode)
        {
            return new CorrectionPlaybackResult
            {
                provider = provider,
                outcome = outcome,
                errorCode = errorCode
            };
        }

        private static string ResolveFeedbackText(CorrectionFeedbackData feedback)
        {
            if (feedback == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(feedback.feedbackText)
                ? feedback.feedbackText
                : feedback.correctedText;
        }

        private static bool IsSupportedProvider(string provider)
        {
            return string.Equals(provider, DialogueAvatarProvider, StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, AssistantAgentProvider, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeProvider(string provider)
        {
            return string.Equals(provider, DialogueAvatarProvider, StringComparison.OrdinalIgnoreCase)
                ? DialogueAvatarProvider
                : AssistantAgentProvider;
        }

        private static string NormalizeStyle(string style)
        {
            return string.Equals(style, RecastStyle, StringComparison.OrdinalIgnoreCase)
                ? RecastStyle
                : ExplicitStyle;
        }

        private static string AppendFallback(string current, string next)
        {
            return string.IsNullOrWhiteSpace(current)
                || string.Equals(current, "none", StringComparison.OrdinalIgnoreCase)
                ? next
                : $"{current}+{next}";
        }
    }
}
