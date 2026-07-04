using System;
using System.Collections;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPresentationVoiceModule : MonoBehaviour, ISceneTalkAvatarVoice, ISceneTalkAvatarReplyContext, ISceneTalkAvatarSessionReset
    {
        private const string DefaultFollowUpSpeakingTrigger = "Talk";

        [Header("Avatar Resolution")]
        [SerializeField] private AvatarPresetResolver resolver;
        [SerializeField] private MonoBehaviour loaderModule;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private bool continueWithoutAvatar = true;
        [SerializeField] private bool attachProps;
        [SerializeField] private AvatarPropPresenter propPresenter;
        [SerializeField] private AvatarPropCatalog propCatalog;

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

        [Header("Animation")]
        [SerializeField] private AvatarAnimationDriver animationDriver;
        [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
        [SerializeField] private Animator fallbackAnimator;
        [SerializeField] private string thinkingTrigger = "Think";
        [SerializeField] private string speakingTrigger = "Speak";
        [SerializeField] private string followUpSpeakingTrigger = "Talk";

        private GameObject currentAvatar;
        private Animator currentAnimator;
        private string currentAvatarKey;
        private string currentAvatarGenderPresentation;
        private bool isOpeningReply = true;

        private IAvatarInstanceLoader Loader => loaderModule as IAvatarInstanceLoader;

        public void SetReplyContext(bool isOpeningReply)
        {
            this.isOpeningReply = isOpeningReply;
        }

        public void ClearAvatar()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

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

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
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
            yield return EnsureAvatar(payload, message => avatarError = message);

            if (!string.IsNullOrWhiteSpace(avatarError))
            {
                if (!continueWithoutAvatar)
                {
                    onError?.Invoke(avatarError);
                    yield break;
                }

                Debug.LogWarning($"[SceneTalkVR] Avatar presentation fallback: {avatarError}", this);
            }

            TriggerThinking();
            yield return null;

            Debug.Log($"[SceneTalkVR] Avatar reply: {payload.dialogueReply}", this);
            TriggerSpeaking(isOpeningReply);

            var playedAudio = false;
            if (useVoiceGatewayTts)
            {
                string voiceError = null;
                yield return PlayGatewayTts(payload, message => voiceError = message);
                playedAudio = string.IsNullOrWhiteSpace(voiceError);

                if (!playedAudio)
                {
                    if (!fallbackToDemoVoiceOnGatewayError)
                    {
                        onError?.Invoke(voiceError);
                        yield break;
                    }

                    Debug.LogWarning($"[SceneTalkVR] Voice gateway TTS fallback: {voiceError}", this);
                }
            }

            if (!playedAudio && audioSource != null && demoReplyClip != null)
            {
                audioSource.clip = demoReplyClip;
                audioSource.Play();
                yield return new WaitWhile(() => audioSource != null && audioSource.isPlaying);
                playedAudio = true;
            }

            if (!playedAudio)
            {
                yield return new WaitForSeconds(Mathf.Max(0.1f, fallbackSpeakingSeconds));
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

        private IEnumerator PlayGatewayTts(SpringScenePayload payload, Action<string> onError)
        {
            if (audioSource == null)
            {
                onError?.Invoke("AudioSource is not assigned.");
                yield break;
            }

            var client = ResolveVoiceGatewayClient();
            if (client == null)
            {
                onError?.Invoke("Voice gateway client is not assigned.");
                yield break;
            }

            var text = payload.dialogueReply;
            if (string.IsNullOrWhiteSpace(text))
            {
                onError?.Invoke("TTS text is empty.");
                yield break;
            }

            var role = payload.avatarRole;
            var request = new TtsRequest
            {
                sessionId = sessionId,
                turnId = $"turn-{Time.frameCount}",
                text = text,
                language = string.IsNullOrWhiteSpace(language) ? "en-US" : language,
                voiceProfile = new VoiceProfile
                {
                    provider = "tencent",
                    voiceId = ResolveVoiceId(payload),
                    speakingSpeed = role != null ? role.speakingSpeed : string.Empty,
                    accent = role != null ? role.accent : string.Empty,
                    attitude = role != null ? role.attitude : string.Empty,
                    role = role != null ? role.role : string.Empty
                },
                output = new TtsOutput
                {
                    format = "wav",
                    sampleRate = Mathf.Max(8000, ttsSampleRate)
                }
            };

            AudioClip clip = null;
            TtsResponse response = null;
            string requestError = null;
            yield return client.RequestTtsAudioClip(
                request,
                (value, audioClip) =>
                {
                    response = value;
                    clip = audioClip;
                },
                message => requestError = message);

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                onError?.Invoke(requestError);
                yield break;
            }

            if (clip == null)
            {
                onError?.Invoke("Voice gateway returned no playable TTS clip.");
                yield break;
            }

            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log(
                $"[SceneTalkVR] Voice gateway TTS audio ({response?.provider}, {response?.latencyMs} ms, cache={response?.cacheHit})",
                this);
            yield return new WaitWhile(() => audioSource != null && audioSource.isPlaying);
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

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.BindAnimator(currentAnimator);
                driver.PlayIdle();
            }

            RefreshProps(payload, currentAvatar);
        }

        private string ResolveVoiceId(SpringScenePayload payload)
        {
            var role = payload != null ? payload.avatarRole : null;
            var appearance = role != null ? role.appearance : null;
            var gender = appearance != null ? appearance.genderPresentation : string.Empty;

            if (IsGender(currentAvatarGenderPresentation, "male") || IsGender(gender, "male"))
            {
                return "default_male_en";
            }

            if (IsGender(currentAvatarGenderPresentation, "female") || IsGender(gender, "female"))
            {
                return "default_female_en";
            }

            return string.IsNullOrWhiteSpace(defaultVoiceId) ? "default_female_en" : defaultVoiceId;
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

        private void TriggerThinking()
        {
            EnsureAnimatorController(currentAnimator);

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.PlayThinking();
                return;
            }

            TriggerAnimationLegacy(thinkingTrigger);
        }

        private void TriggerSpeaking(bool openingReply)
        {
            EnsureAnimatorController(currentAnimator);

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                var played = openingReply
                    ? driver.PlaySpeaking()
                    : driver.PlayFollowUpSpeaking();

                if (played || openingReply)
                {
                    return;
                }

                if (driver.PlaySpeaking())
                {
                    return;
                }
            }

            var triggerName = openingReply
                ? speakingTrigger
                : ResolveFollowUpSpeakingTrigger();
            TriggerAnimationLegacy(triggerName);
        }

        private string ResolveFollowUpSpeakingTrigger()
        {
            return string.IsNullOrWhiteSpace(followUpSpeakingTrigger)
                ? DefaultFollowUpSpeakingTrigger
                : followUpSpeakingTrigger;
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

        private void TriggerAnimationLegacy(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            var animator = currentAnimator != null ? currentAnimator : fallbackAnimator;
            if (animator == null)
            {
                return;
            }

            animator.SetTrigger(triggerName);
        }
    }
}
