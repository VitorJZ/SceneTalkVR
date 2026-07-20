using System;
using System.Collections;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using SceneTalkVR.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPresentationVoiceModule : MonoBehaviour, ISceneTalkStreamingAvatarVoice, ISceneTalkAvatarReplyContext, ISceneTalkAvatarThinkingState, ISceneTalkAvatarSessionReset, ISceneTalkAvatarSessionPrepare, ISceneTalkCorrectionFeedbackProviderReceiver, ISceneTalkCorrectionAssistantEmbodimentReceiver
    {
        [Header("Avatar Resolution")]
        [SerializeField] private AvatarPresetResolver resolver;
        [SerializeField] private MonoBehaviour loaderModule;
        [SerializeField] private Transform avatarRoot;
        [FormerlySerializedAs("continueWithoutAvatar")]
        [SerializeField, Tooltip("Continue correction and reply audio when avatar resolution or loading fails.")]
        private bool allowVoiceFallbackOnAvatarFailure = true;
        [SerializeField] private bool attachProps;
        [SerializeField] private AvatarPropPresenter propPresenter;
        [SerializeField] private AvatarPropCatalog propCatalog;

        [Header("Avatar Placement")]
        [SerializeField, Tooltip("World-space UI used to place a newly generated avatar on the same viewing axis.")]
        private Transform placementAnchor;
        [SerializeField, Min(0f), Tooltip("Distance behind the placement anchor, away from the user.")]
        private float placementDepthFromAnchor = 1f;
        [SerializeField] private bool constrainPlacementToGround = true;
        [SerializeField] private float placementGroundY;

        [Header("User Facing")]
        [SerializeField] private Transform userFacingTarget;
        [SerializeField] private bool faceUserOnSpawn = true;
        [SerializeField] private float visualForwardYawOffset = 180f;
        [SerializeField] private bool useHumanoidLookAt = true;
        [SerializeField, Range(0f, 1f)] private float lookAtWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] private float lookAtBodyWeight = 0.05f;
        [SerializeField, Range(0f, 1f)] private float lookAtHeadWeight = 0.65f;
        [SerializeField, Range(0f, 1f)] private float lookAtEyesWeight = 0.15f;
        [SerializeField, Range(0f, 1f)] private float lookAtClampWeight = 0.7f;

        [Header("Demo Voice")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip demoReplyClip;
        [SerializeField] private float fallbackSpeakingSeconds = 2f;

        [Header("Voice Gateway")]
        [SerializeField] private bool useVoiceGatewayTts;
        [SerializeField] private VoiceGatewayClient voiceGatewayClient;
        [SerializeField] private string sessionId = "scenetalk-demo-session";
        [SerializeField] private string language = "en-US";
        [SerializeField] private string defaultVoiceId = "default_female_en";
        [SerializeField] private int ttsSampleRate = 24000;
        [SerializeField] private bool fallbackToDemoVoiceOnGatewayError = true;

        [Header("Correction Feedback")]
        [SerializeField] private CorrectionFeedbackPresenter correctionFeedbackPresenter;
        [SerializeField] private bool createCorrectionFeedbackPresenterIfMissing = true;

        [Header("Animation")]
        [SerializeField] private AvatarAnimationDriver animationDriver;
        [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
        [SerializeField] private Animator fallbackAnimator;

        private GameObject currentAvatar;
        private Animator currentAnimator;
        private string currentAvatarKey;
        private string currentAvatarGenderPresentation;
        private bool isOpeningReply = true;
        private AvatarSpeechPlayer speechPlayer;
        private bool isAvatarLoadingFinished;

        private IAvatarInstanceLoader Loader => loaderModule as IAvatarInstanceLoader;
        private AvatarSpeechPlayer SpeechPlayer => speechPlayer ??= new AvatarSpeechPlayer();

        public event Action<CorrectionPlaybackResult> CorrectionPlaybackCompleted;
        public CorrectionPlaybackResult LastCorrectionPlaybackResult { get; private set; }

        public void ConfigureVoiceGateway(bool enabled, VoiceGatewayClient client)
        {
            useVoiceGatewayTts = enabled;
            if (client != null)
            {
                voiceGatewayClient = client;
            }
        }

        public void SetCorrectionFeedbackProvider(string provider)
        {
            ResolveCorrectionFeedbackPresenter(createCorrectionFeedbackPresenterIfMissing)
                ?.SetFeedbackProvider(provider);
        }

        public void SetCorrectionAssistantEmbodiment(string embodiment)
        {
            ResolveCorrectionFeedbackPresenter(createCorrectionFeedbackPresenterIfMissing)
                ?.SetAssistantEmbodiment(embodiment);
        }

        public void SetReplyContext(bool isOpeningReply)
        {
            this.isOpeningReply = isOpeningReply;
        }

        public void SetThinking(bool isThinking)
        {
            EnsureAnimatorController(currentAnimator);
            ResolveAnimationDriver()?.SetThinking(isThinking);
        }

        public void ClearAvatar()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            ResolveCorrectionFeedbackPresenter(false)?.ResetPresentation();

            var props = ResolvePropPresenter(false);
            if (props != null)
            {
                props.ClearProps();
            }

            var avatarToDestroy = currentAvatar;
            currentAvatar = null;
            currentAnimator = null;
            currentAvatarKey = string.Empty;
            currentAvatarGenderPresentation = string.Empty;
            isOpeningReply = true;
            LastCorrectionPlaybackResult = null;

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.ResetState();
                driver.BindAnimator(null);
            }

            if (avatarToDestroy != null)
            {
                DestroyAvatarObject(avatarToDestroy);
            }
        }

        public IEnumerator PresentReply(SpringScenePayload payload, Action onComplete, Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Avatar voice payload is null.");
                yield break;
            }

            // The assistant is a mode-level companion. Activate its visual before
            // loading the dialogue avatar so both appear when the scene opens.
            var correctionPresenter = ResolveCorrectionFeedbackPresenter(
                createCorrectionFeedbackPresenterIfMissing);
            if (correctionPresenter != null)
            {
                var payloadProvider = payload.correctionFeedback != null
                    ? payload.correctionFeedback.provider
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(payloadProvider))
                {
                    correctionPresenter.SetFeedbackProvider(payloadProvider);
                }

                correctionPresenter.SetPresentationActive(true);
            }

            // If streaming was already used to play dialogue in real-time, just present correction and wait
            if (isDialogueGateOpen && (isStreamingPlaying || (isStreamingFinished && streamingBasePayload != null)))
            {
                // Ensure avatar is loaded first if it is the first turn and wasn't loaded in PrepareStreaming
                if (currentAvatar == null)
                {
                    string avatarLoadError = string.Empty;
                    yield return EnsureAvatar(payload, msg => avatarLoadError = msg);
                    isAvatarLoadingFinished = true;
                    if (!string.IsNullOrEmpty(avatarLoadError) && !allowVoiceFallbackOnAvatarFailure)
                    {
                        onError?.Invoke(avatarLoadError);
                        yield break;
                    }
                }
                else
                {
                    isAvatarLoadingFinished = true;
                }

                // Wait for the streaming dialogue audio to finish speaking completely
                while (isStreamingPlaying)
                {
                    yield return new WaitForSeconds(0.1f);
                }

                // Add a small natural pause between Avatar speech and Assistant Agent feedback
                yield return new WaitForSeconds(0.5f);

                var strPresenter = correctionPresenter
                    ?? ResolveCorrectionFeedbackPresenter(createCorrectionFeedbackPresenterIfMissing);
                if (strPresenter != null && payload.correctionFeedback != null && payload.correctionFeedback.hasFeedback)
                {
                    strPresenter.SetPresentationActive(true);
                    yield return strPresenter.Present(
                        payload,
                        BuildSpeechPlaybackContext(),
                        () => { }, // Do not trigger speaking animation
                        () => { },
                        value => LastCorrectionPlaybackResult = value);
                }

                onComplete?.Invoke();
                yield break;
            }

            var avatarError = string.Empty;
            if (isOpeningReply || currentAvatar == null)
            {
                yield return EnsureAvatar(payload, message => avatarError = message);
                isAvatarLoadingFinished = true;
            }
            else
            {
                isAvatarLoadingFinished = true;
            }

            if (!string.IsNullOrWhiteSpace(avatarError) && !allowVoiceFallbackOnAvatarFailure)
            {
                onError?.Invoke(avatarError);
                yield break;
            }

            // 1. Play Correction Feedback first (if any)
            LastCorrectionPlaybackResult = null;
            bool hasCorrection = payload.correctionFeedback != null && payload.correctionFeedback.hasFeedback;

            if (hasCorrection && correctionPresenter != null)
            {
                LastCorrectionPlayStart = Time.realtimeSinceStartup;
                correctionPresenter.SetPresentationActive(true);
                yield return correctionPresenter.Present(
                    payload,
                    BuildSpeechPlaybackContext(),
                    () => BeginSpeechAnimation(false),
                    EndSpeechAnimation,
                    value => LastCorrectionPlaybackResult = value);
                LastCorrectionPlayEnd = Time.realtimeSinceStartup;

                // Add a small natural pause (0.5 seconds) between Correction and Dialogue
                yield return new WaitForSeconds(0.5f);
            }
            else if (hasCorrection)
            {
                LastCorrectionPlaybackResult = new CorrectionPlaybackResult
                {
                    provider = payload.correctionFeedback.provider,
                    outcome = "failed",
                    errorCode = "presenter_missing"
                };
            }

            if (LastCorrectionPlaybackResult != null)
            {
                CorrectionPlaybackCompleted?.Invoke(LastCorrectionPlaybackResult);
            }

            // 2. Play Dialogue continuation (Streaming or Non-streaming)
            bool wasStreamingUsed = wasAnySentenceEnqueued;
            if (wasStreamingUsed)
            {
                // Open Dialogue Gate so that streaming sentences can start playing
                OpenDialogueGate();

                // Wait for the streaming/caching dialogue to finish playing completely
                while (isStreamingPlaying || streamingPreparedQueue.Count > 0 || streamingSentenceQueue.Count > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
            else
            {
                // Non-streaming fallback: Play the dialogueReply directly
                SetThinking(true);
                yield return null;

                Debug.Log($"[SceneTalkVR] Non-streaming avatar reply: {payload.dialogueReply}", this);

                AvatarSpeechPlaybackResult replyResult = null;
                yield return SpeechPlayer.Play(
                    BuildSpeechPlaybackContext(),
                    payload,
                    new AvatarSpeechPlaybackRequest
                    {
                        text = payload.dialogueReply,
                        logLabel = "Avatar reply",
                        playbackStarted = () => BeginSpeechAnimation(isOpeningReply),
                        playbackEnded = EndSpeechAnimation
                    },
                    value => replyResult = value);

                SetThinking(false);
                EndSpeechAnimation();

                if (replyResult != null && !string.IsNullOrWhiteSpace(replyResult.error))
                {
                    onError?.Invoke(replyResult.error);
                    yield break;
                }
            }

            onComplete?.Invoke();
        }

        public IEnumerator PrepareSession(
            SpringScenePayload payload,
            Action onComplete,
            Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Avatar session payload is null.");
                yield break;
            }

            var correctionPresenter = ResolveCorrectionFeedbackPresenter(
                createCorrectionFeedbackPresenterIfMissing);
            if (correctionPresenter != null)
            {
                var provider = payload.correctionFeedback?.provider;
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    correctionPresenter.SetFeedbackProvider(provider);
                }

                correctionPresenter.SetPresentationActive(true);
            }

            var avatarError = string.Empty;
            yield return EnsureAvatar(payload, message => avatarError = message);
            isAvatarLoadingFinished = true;
            if (!string.IsNullOrWhiteSpace(avatarError) && !allowVoiceFallbackOnAvatarFailure)
            {
                onError?.Invoke(avatarError);
                yield break;
            }

            onComplete?.Invoke();
        }

        private IEnumerator EnsureAvatar(SpringScenePayload payload, Action<string> onError)
        {
            if (resolver == null)
            {
                onError?.Invoke("Avatar resolver is not assigned.");
                yield break;
            }

            var loader = Loader;
            if (loader == null)
            {
                onError?.Invoke("Avatar loader module is missing or does not implement IAvatarInstanceLoader.");
                yield break;
            }

            var resolution = resolver.Resolve(payload);
            if (resolution == null || !resolution.HasPreset)
            {
                onError?.Invoke(resolution == null ? "Avatar resolver returned null." : resolution.fallbackReason);
                yield break;
            }

            currentAvatarGenderPresentation = ResolvePresetGenderPresentation(resolution.preset);

            if (currentAvatar != null
                && string.Equals(currentAvatarKey, resolution.avatarKey, StringComparison.OrdinalIgnoreCase))
            {
                RefreshProps(payload, currentAvatar);
                yield break;
            }

            GameObject loadedAvatar = null;
            string loadError = null;
            var parent = avatarRoot != null ? avatarRoot : transform;

            AlignAvatarRootToPlacementAnchor();

            yield return loader.LoadAvatar(
                resolution,
                parent,
                instance => loadedAvatar = instance,
                message => loadError = message);

            if (!string.IsNullOrWhiteSpace(loadError))
            {
                onError?.Invoke(loadError);
                yield break;
            }

            if (loadedAvatar == null)
            {
                onError?.Invoke($"Avatar loader completed without an instance for '{resolution.avatarKey}'.");
                yield break;
            }

            ReplaceCurrentAvatar(loadedAvatar, payload, resolution.avatarKey);
            Debug.Log(
                $"[SceneTalkVR] Avatar resolved: key={resolution.avatarKey}, score={resolution.score}, fallback={resolution.fallbackLevel}",
                this);
        }

        private AvatarSpeechPlaybackContext BuildSpeechPlaybackContext()
        {
            return new AvatarSpeechPlaybackContext
            {
                logContext = this,
                gatewayClient = useVoiceGatewayTts ? ResolveVoiceGatewayClient() : voiceGatewayClient,
                defaultAudioSource = audioSource,
                demoReplyClip = demoReplyClip,
                useVoiceGatewayTts = useVoiceGatewayTts,
                fallbackToDemoVoiceOnGatewayError = fallbackToDemoVoiceOnGatewayError,
                sessionId = sessionId,
                language = language,
                defaultVoiceId = defaultVoiceId,
                currentAvatarGenderPresentation = currentAvatarGenderPresentation,
                ttsSampleRate = ttsSampleRate,
                fallbackSpeakingSeconds = fallbackSpeakingSeconds
            };
        }

        private VoiceGatewayClient ResolveVoiceGatewayClient()
        {
            if (voiceGatewayClient != null)
            {
                return voiceGatewayClient;
            }

            voiceGatewayClient = GetComponent<VoiceGatewayClient>();
            if (voiceGatewayClient != null)
            {
                return voiceGatewayClient;
            }

            voiceGatewayClient = gameObject.AddComponent<VoiceGatewayClient>();
            return voiceGatewayClient;
        }

        private void ReplaceCurrentAvatar(GameObject loadedAvatar, SpringScenePayload payload, string avatarKey)
        {
            if (currentAvatar != null && currentAvatar != loadedAvatar)
            {
                Destroy(currentAvatar);
            }

            currentAvatar = loadedAvatar;
            currentAvatarKey = string.IsNullOrWhiteSpace(avatarKey) ? string.Empty : avatarKey;
            currentAnimator = currentAvatar.GetComponentInChildren<Animator>();
            EnsureAnimatorController(currentAnimator);
            ConfigureUserFacing(currentAvatar, currentAnimator);

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.BindAnimator(currentAnimator);
                driver.PlayIdle();
            }

            RefreshProps(payload, currentAvatar);
        }

        private void ConfigureUserFacing(GameObject avatar, Animator animator)
        {
            if (avatar == null || (!faceUserOnSpawn && !useHumanoidLookAt))
            {
                return;
            }

            var host = animator != null ? animator.gameObject : avatar;
            var facingController = host.GetComponent<AvatarUserFacingController>();
            if (facingController == null)
            {
                facingController = host.AddComponent<AvatarUserFacingController>();
            }

            var target = ResolveUserFacingTarget();

            facingController.Configure(
                animator,
                avatar.transform,
                target,
                faceUserOnSpawn,
                visualForwardYawOffset,
                useHumanoidLookAt,
                lookAtWeight,
                lookAtBodyWeight,
                lookAtHeadWeight,
                lookAtEyesWeight,
                lookAtClampWeight);
        }

        internal void AlignAvatarRootToPlacementAnchor()
        {
            placementAnchor = ResolvePlacementAnchor();
            if (avatarRoot == null || placementAnchor == null)
            {
                return;
            }

            var target = ResolveUserFacingTarget();
            var awayFromUser = target != null
                ? placementAnchor.position - target.position
                : placementAnchor.forward;
            awayFromUser = Vector3.ProjectOnPlane(awayFromUser, Vector3.up);

            if (awayFromUser.sqrMagnitude <= 0.0001f)
            {
                awayFromUser = Vector3.ProjectOnPlane(placementAnchor.forward, Vector3.up);
            }

            if (awayFromUser.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var position = placementAnchor.position
                + awayFromUser.normalized * Mathf.Max(0f, placementDepthFromAnchor);
            if (constrainPlacementToGround)
            {
                position.y = placementGroundY;
            }

            avatarRoot.position = position;
        }

        private Transform ResolvePlacementAnchor()
        {
            if (placementAnchor != null)
            {
                return placementAnchor;
            }

            var worldUi = GameObject.Find("SceneTalkVR World UI");
            return worldUi != null ? worldUi.transform : null;
        }

        private Transform ResolveUserFacingTarget()
        {
            if (userFacingTarget != null)
            {
                return userFacingTarget;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private static string ResolvePresetGenderPresentation(AvatarPresetEntry preset)
        {
            if (preset == null || preset.genderPresentations == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < preset.genderPresentations.Length; i++)
            {
                var value = preset.genderPresentations[i];
                if (IsGender(value, "male") || IsGender(value, "female"))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool IsGender(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void DestroyAvatarObject(GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(avatar);
            }
            else
            {
                DestroyImmediate(avatar);
            }
        }

        private void RefreshProps(SpringScenePayload payload, GameObject avatar)
        {
            var props = ResolvePropPresenter(attachProps);
            if (props == null)
            {
                return;
            }

            props.ClearProps();
            if (!attachProps)
            {
                return;
            }

            if (propCatalog != null)
            {
                props.SetCatalog(propCatalog);
            }

            if (!props.HasCatalog)
            {
                Debug.LogWarning("[SceneTalkVR] Avatar props are enabled, but no AvatarPropCatalog is assigned.", this);
                return;
            }

            props.PresentProps(payload, avatar);
        }

        private bool EnsureAnimatorController(Animator animator)
        {
            if (animator == null)
            {
                return false;
            }

            if (animator.runtimeAnimatorController != null && animator.layerCount > 0)
            {
                return true;
            }

            if (defaultAnimatorController == null)
            {
                Debug.LogWarning("[SceneTalkVR] Avatar Animator has no controller and no default controller is assigned.", this);
                return false;
            }

            animator.runtimeAnimatorController = defaultAnimatorController;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            return animator.runtimeAnimatorController != null && animator.layerCount > 0;
        }

        private AvatarPropPresenter ResolvePropPresenter(bool createIfMissing)
        {
            if (propPresenter == null)
            {
                propPresenter = GetComponent<AvatarPropPresenter>();
            }

            if (propPresenter == null && createIfMissing)
            {
                propPresenter = gameObject.AddComponent<AvatarPropPresenter>();
            }

            return propPresenter;
        }

        private CorrectionFeedbackPresenter ResolveCorrectionFeedbackPresenter(bool createIfMissing)
        {
            if (correctionFeedbackPresenter == null)
            {
                correctionFeedbackPresenter = GetComponent<CorrectionFeedbackPresenter>();
            }

            if (correctionFeedbackPresenter == null && createIfMissing)
            {
                correctionFeedbackPresenter = gameObject.AddComponent<CorrectionFeedbackPresenter>();
            }

            return correctionFeedbackPresenter;
        }

        private void BeginSpeechAnimation(bool openingReply)
        {
            EnsureAnimatorController(currentAnimator);

            var driver = ResolveAnimationDriver();
            if (driver == null)
            {
                return;
            }

            driver.SetThinking(false);
            if (openingReply)
            {
                driver.BeginOpeningSpeech();
                return;
            }

            driver.BeginTalking();
        }

        private void EndSpeechAnimation()
        {
            ResolveAnimationDriver()?.EndTalking();
        }

        private AvatarAnimationDriver ResolveAnimationDriver()
        {
            if (animationDriver == null)
            {
                animationDriver = GetComponent<AvatarAnimationDriver>();
            }

            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<AvatarAnimationDriver>();
            }

            animationDriver.SetFallbackAnimator(fallbackAnimator);
            return animationDriver;
        }

        #region Streaming Playback Implementation

        private System.Collections.Generic.Queue<string> streamingSentenceQueue = new System.Collections.Generic.Queue<string>();
        private System.Collections.Generic.Queue<PreparedAvatarSpeech> streamingPreparedQueue = new System.Collections.Generic.Queue<PreparedAvatarSpeech>();
        private bool isStreamingFinished = false;
        private bool isStreamingPlaying = false;
        private string streamingError = null;
        private SpringScenePayload streamingBasePayload;
        private bool isDialogueGateOpen = false;
        private bool isPreparingStream = false;
        private bool wasAnySentenceEnqueued = false;

        public float LastTtsReadyLatencyMs { get; private set; } = -1f;
        public float LastCorrectionPlayStart { get; private set; } = -1f;
        public float LastCorrectionPlayEnd { get; private set; } = -1f;
        public float LastDialoguePlayStart { get; private set; } = -1f;
        public float LastDialoguePlayEnd { get; private set; } = -1f;

        public void PrepareStreaming(SpringScenePayload basePayload)
        {
            streamingBasePayload = basePayload;
            streamingSentenceQueue.Clear();
            streamingPreparedQueue.Clear();
            isStreamingFinished = false;
            isStreamingPlaying = false;
            isDialogueGateOpen = false;
            isPreparingStream = false;
            wasAnySentenceEnqueued = false;
            streamingError = null;
            isAvatarLoadingFinished = false;

            LastTtsReadyLatencyMs = -1f;
            LastCorrectionPlayStart = -1f;
            LastCorrectionPlayEnd = -1f;
            LastDialoguePlayStart = -1f;
            LastDialoguePlayEnd = -1f;

            if (basePayload != null)
            {
                var correctionPresenter = ResolveCorrectionFeedbackPresenter(
                    createCorrectionFeedbackPresenterIfMissing);
                if (correctionPresenter != null)
                {
                    var payloadProvider = basePayload.correctionFeedback != null
                        ? basePayload.correctionFeedback.provider
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(payloadProvider))
                    {
                        correctionPresenter.SetFeedbackProvider(payloadProvider);
                    }

                    correctionPresenter.SetPresentationActive(true);
                }

                if (isOpeningReply || currentAvatar == null)
                {
                    StartCoroutine(EnsureAvatarCoroutine(basePayload));
                }
            }
        }

        public void EnqueueSentence(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return;

            wasAnySentenceEnqueued = true;
            streamingSentenceQueue.Enqueue(sentence);

            if (!isPreparingStream)
            {
                StartCoroutine(PrepareStreamingQueueCoroutine());
            }

            if (!isStreamingPlaying)
            {
                StartCoroutine(PlayStreamingQueueCoroutine());
            }
        }

        public void SignalStreamingComplete()
        {
            isStreamingFinished = true;
        }

        public void OpenDialogueGate()
        {
            isDialogueGateOpen = true;
        }

        private IEnumerator EnsureAvatarCoroutine(SpringScenePayload payload)
        {
            string avatarError = null;
            yield return EnsureAvatar(payload, msg => avatarError = msg);
            isAvatarLoadingFinished = true;
            if (!string.IsNullOrEmpty(avatarError))
            {
                Debug.LogWarning($"[AvatarPresentationVoiceModule] EnsureAvatar failed during streaming: {avatarError}", this);
            }
        }

        private IEnumerator PrepareStreamingQueueCoroutine()
        {
            isPreparingStream = true;
            while (!isStreamingFinished || streamingSentenceQueue.Count > 0)
            {
                if (streamingSentenceQueue.Count == 0)
                {
                    yield return new WaitForSeconds(0.05f);
                    continue;
                }

                string sentence = streamingSentenceQueue.Dequeue();
                PreparedAvatarSpeech prepared = null;
                yield return SpeechPlayer.Prepare(
                    BuildSpeechPlaybackContext(),
                    streamingBasePayload,
                    new AvatarSpeechPlaybackRequest
                    {
                        text = sentence,
                        logLabel = $"Streaming sentence: {sentence}"
                    },
                    value => prepared = value);

                if (prepared != null)
                {
                    if (LastTtsReadyLatencyMs < 0f)
                    {
                        var orchestrator = FindFirstObjectByType<SceneTalkOrchestrator>();
                        float captureEnd = orchestrator != null ? orchestrator.LastSpeechCaptureEndTime : Time.realtimeSinceStartup;
                        LastTtsReadyLatencyMs = (Time.realtimeSinceStartup - captureEnd) * 1000f;
                    }
                    streamingPreparedQueue.Enqueue(prepared);
                }
            }
            isPreparingStream = false;
        }

        private IEnumerator PlayStreamingQueueCoroutine()
        {
            isStreamingPlaying = true;
            SetThinking(true);

            while (!isStreamingFinished || streamingPreparedQueue.Count > 0 || streamingSentenceQueue.Count > 0 || !isDialogueGateOpen)
            {
                if (!isDialogueGateOpen || streamingPreparedQueue.Count == 0)
                {
                    yield return new WaitForSeconds(0.05f);
                    continue;
                }

                while (isOpeningReply && currentAvatar == null && !isAvatarLoadingFinished)
                {
                    yield return new WaitForSeconds(0.05f);
                }

                if (LastDialoguePlayStart < 0f)
                {
                    LastDialoguePlayStart = Time.realtimeSinceStartup;
                }

                BeginSpeechAnimation(isOpeningReply);

                var prepared = streamingPreparedQueue.Dequeue();
                AvatarSpeechPlaybackResult replyResult = null;
                yield return SpeechPlayer.PlayPrepared(
                    BuildSpeechPlaybackContext(),
                    new AvatarSpeechPlaybackRequest
                    {
                        text = string.Empty,
                        logLabel = "Streaming dialogue reply"
                    },
                    prepared,
                    value => replyResult = value);

                if (replyResult != null && !string.IsNullOrEmpty(replyResult.error))
                {
                    streamingError = replyResult.error;
                    break;
                }
            }

            LastDialoguePlayEnd = Time.realtimeSinceStartup;
            isStreamingPlaying = false;
            SetThinking(false);
            EndSpeechAnimation();
        }

        #endregion
    }
}
