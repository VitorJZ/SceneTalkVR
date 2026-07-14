using System;
using System.Collections;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;
using UnityEngine.Serialization;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPresentationVoiceModule : MonoBehaviour, ISceneTalkAvatarVoice, ISceneTalkAvatarReplyContext, ISceneTalkAvatarThinkingState, ISceneTalkAvatarSessionReset, ISceneTalkCorrectionFeedbackProviderReceiver
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

            var avatarError = string.Empty;
            if (isOpeningReply || currentAvatar == null)
            {
                yield return EnsureAvatar(payload, message => avatarError = message);
            }

            if (!string.IsNullOrWhiteSpace(avatarError))
            {
                if (!allowVoiceFallbackOnAvatarFailure)
                {
                    onError?.Invoke(avatarError);
                    yield break;
                }

                Debug.LogWarning($"[SceneTalkVR] Avatar presentation fallback: {avatarError}", this);
            }

            LastCorrectionPlaybackResult = null;
            SetThinking(false);
            var correctionPresenter = ResolveCorrectionFeedbackPresenter(
                createCorrectionFeedbackPresenterIfMissing);
            if (correctionPresenter != null)
            {
                correctionPresenter.SetPresentationActive(true);
                yield return correctionPresenter.Present(
                    payload,
                    BuildSpeechPlaybackContext(),
                    () => BeginSpeechAnimation(false),
                    EndSpeechAnimation,
                    value => LastCorrectionPlaybackResult = value);
            }
            else if (payload.correctionFeedback != null && payload.correctionFeedback.hasFeedback)
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

            Debug.Log($"[SceneTalkVR] Avatar reply: {payload.dialogueReply}", this);
            if (!isOpeningReply && !string.IsNullOrWhiteSpace(payload.dialogueReply))
            {
                SetThinking(true);
            }

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

            var target = userFacingTarget;
            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
            }

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

    }
}
