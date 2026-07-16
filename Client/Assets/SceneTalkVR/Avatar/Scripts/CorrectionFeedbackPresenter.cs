using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    [DisallowMultipleComponent]
    public sealed class CorrectionFeedbackPresenter : MonoBehaviour, ISceneTalkExperimentLockReceiver
    {
        private enum TencentVoiceType
        {
            [InspectorName("WeJack | English male | 101050 | available")]
            WeJack = 101050,
            [InspectorName("WeRose | English female | 1051 | legacy available")]
            WeRoseLegacy = 1051,
            [InspectorName("WeJack | English male | 1050 | legacy available")]
            WeJackLegacy = 1050,
            [InspectorName("WeWinny | English female | 501009")]
            WeWinny = 501009,
            [InspectorName("WeJames | English male | 501008")]
            WeJames = 501008,
            [InspectorName("Zhi Xiao Min | Chat female | 502003")]
            ZhiXiaoMin = 502003,
            [InspectorName("Zhi Xiao Rou | Chat female | 502001")]
            ZhiXiaoRou = 502001,
            [InspectorName("Zhi Xiao Wu | Chat male | 502006")]
            ZhiXiaoWu = 502006,
            [InspectorName("Zhi Xiao Hu | Child | 502007")]
            ZhiXiaoHu = 502007,
            [InspectorName("Zhi Xiao Jie | Narration male | 502005")]
            ZhiXiaoJie = 502005,
            [InspectorName("Zhi Xiao Man | Marketing female | 502004")]
            ZhiXiaoMan = 502004,
            [InspectorName("Nuan Xin A Can | Chat male | 602004")]
            NuanXinACan = 602004,
            [InspectorName("Zhuan Ye Zi Xin | Chat female | 602005")]
            ZhuanYeZiXin = 602005,
            [InspectorName("Dong Shi Shao Nian | Character male | 603000")]
            DongShiShaoNian = 603000,
            [InspectorName("Xiao Xiang Mei Mei | Character female | 603001")]
            XiaoXiangMeiMei = 603001,
            [InspectorName("Ruan Meng Xin Xin | Boy | 603002")]
            RuanMengXinXin = 603002,
            [InspectorName("Sui He Lao Li | Chat male | 603003")]
            SuiHeLaoLi = 603003,
            [InspectorName("Wen Rou Xiao Ning | Chat female | 603004")]
            WenRouXiaoNing = 603004,
            [InspectorName("Zhi Xin Da Lin | Chat male | 603005")]
            ZhiXinDaLin = 603005,
            [InspectorName("Chen Wen Qing Shu | Chat male | 603006")]
            ChenWenQingShu = 603006,
            [InspectorName("Lin Jia Nv Hai | Chat female | 603007")]
            LinJiaNvHai = 603007,
            [InspectorName("Ai Xiao You | Chat female | 602003")]
            AiXiaoYou = 602003
        }

        private const string DialogueAvatarProvider = "dialogue_avatar";
        private const string AssistantAgentProvider = "assistant_agent";
        private const string ExplicitStyle = "explicit";
        private const string RecastStyle = "recast";
        private const TencentVoiceType DefaultAssistantAgentVoice = TencentVoiceType.WeJack;

        [Header("Correction Playback")]
        [SerializeField] private bool playCorrectionFeedback = true;
        [SerializeField] private CorrectionAgentPresenter correctionAgentPresenter;
        [SerializeField] private bool createCorrectionAgentIfMissing = true;

        [Header("Assistant Agent Voice")]
        [SerializeField]
        [Tooltip("Tencent VoiceType used only by the correction assistant. Options support English or bilingual speech in the basic TTS API.")]
        private TencentVoiceType assistantAgentVoiceType = DefaultAssistantAgentVoice;

        [Header("Correction Debug")]
        [SerializeField] private bool debugForceFeedback;
        [SerializeField] private string debugFeedbackText = "Try saying: I'd like a latte, please.";

        private AvatarSpeechPlayer speechPlayer;
        private bool isSessionActive;
        private string currentFeedbackProvider = AssistantAgentProvider;
        private bool experimentLocked;

        private AvatarSpeechPlayer SpeechPlayer => speechPlayer ??= new AvatarSpeechPlayer();

        public string CurrentFeedbackProvider => NormalizeProvider(currentFeedbackProvider);

        public void SetExperimentLocked(bool locked)
        {
            experimentLocked = locked;
            if (experimentLocked)
            {
                debugForceFeedback = false;
            }
        }

        private void Start()
        {
            // SetPresentationActive can be called immediately after this component
            // is dynamically created. Do not overwrite that mode-level state here.
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
            Action beginDialogueAvatarSpeaking,
            Action endDialogueAvatarSpeaking,
            Action<CorrectionPlaybackResult> onComplete)
        {
            if (experimentLocked && debugForceFeedback)
            {
                Debug.LogWarning("[CorrectionFeedbackPresenter] debugForceFeedback disabled in formal experiment.");
                debugForceFeedback = false;
            }

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
            var useDialogueAvatar = string.Equals(
                provider,
                DialogueAvatarProvider,
                StringComparison.OrdinalIgnoreCase);

            if (useDialogueAvatar && string.Equals(style, RecastStyle, StringComparison.OrdinalIgnoreCase))
            {
                // Dialogue avatar recast is already naturally spoken in the main dialogueReply.
                // Do not play it again to prevent redundant repetition.
                onComplete?.Invoke(Result(provider, "played", "skipped_audio_avatar_recast"));
                yield break;
            }

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

            useDialogueAvatar = string.Equals(
                provider,
                DialogueAvatarProvider,
                StringComparison.OrdinalIgnoreCase);
            var assistantAgentVoiceId = useDialogueAvatar
                ? null
                : ResolveAssistantAgentVoiceId();
            var playbackRequest = new AvatarSpeechPlaybackRequest
            {
                text = text,
                logLabel = useDialogueAvatar
                    ? "Correction feedback"
                    : "Assistant correction feedback",
                voiceIdOverride = assistantAgentVoiceId
            };

            AvatarSpeechPlaybackResult playbackResult = null;
            if (useDialogueAvatar)
            {
                playbackRequest.playbackStarted = beginDialogueAvatarSpeaking;
                playbackRequest.playbackEnded = endDialogueAvatarSpeaking;
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
                    correctionAgent.SetVisible(true);
                    playbackRequest.audioSourceOverride = correctionAgent.AudioSource;
                    playbackRequest.playbackStarted = correctionAgent.BeginSpeaking;
                    playbackRequest.playbackEnded = correctionAgent.EndSpeaking;
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
                + $"voiceId={assistantAgentVoiceId ?? "avatar_default"}, "
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
            return debugForceFeedback && !experimentLocked;
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
            if (correctionAgent == null || correctionAgent.TargetVisible == shouldShow)
            {
                return;
            }

            correctionAgent.SetVisible(shouldShow);
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

        private string ResolveAssistantAgentVoiceId()
        {
            var selectedVoice = Enum.IsDefined(typeof(TencentVoiceType), assistantAgentVoiceType)
                ? assistantAgentVoiceType
                : DefaultAssistantAgentVoice;
            return ((int)selectedVoice).ToString();
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
