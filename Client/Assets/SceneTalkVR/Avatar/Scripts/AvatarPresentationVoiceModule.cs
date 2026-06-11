using System;
using System.Collections;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarPresentationVoiceModule : MonoBehaviour, ISceneTalkAvatarVoice
    {
        [Header("Avatar Resolution")]
        [SerializeField] private AvatarPresetResolver resolver;
        [SerializeField] private MonoBehaviour loaderModule;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private bool continueWithoutAvatar = true;
        [SerializeField] private AvatarPropPresenter propPresenter;

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

        private GameObject currentAvatar;
        private Animator currentAnimator;

        private IAvatarInstanceLoader Loader => loaderModule as IAvatarInstanceLoader;

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
            TriggerSpeaking();

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

            ReplaceCurrentAvatar(loadedAvatar, payload);
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
                    voiceId = string.IsNullOrWhiteSpace(defaultVoiceId) ? "default_female_en" : defaultVoiceId,
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

        private void ReplaceCurrentAvatar(GameObject loadedAvatar, SpringScenePayload payload)
        {
            var props = ResolvePropPresenter();
            if (props != null)
            {
                props.ClearProps();
            }

            if (currentAvatar != null && currentAvatar != loadedAvatar)
            {
                Destroy(currentAvatar);
            }

            currentAvatar = loadedAvatar;
            currentAnimator = currentAvatar.GetComponentInChildren<Animator>();
            EnsureAnimatorController(currentAnimator);

            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.BindAnimator(currentAnimator);
            }

            if (props != null)
            {
                props.PresentProps(payload, currentAvatar);
            }
        }

        private void EnsureAnimatorController(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController != null || defaultAnimatorController == null)
            {
                return;
            }

            animator.runtimeAnimatorController = defaultAnimatorController;
            animator.applyRootMotion = false;
        }

        private AvatarPropPresenter ResolvePropPresenter()
        {
            if (propPresenter == null)
            {
                propPresenter = GetComponent<AvatarPropPresenter>();
            }

            return propPresenter;
        }

        private void TriggerThinking()
        {
            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.PlayThinking();
                return;
            }

            TriggerAnimationLegacy(thinkingTrigger);
        }

        private void TriggerSpeaking()
        {
            var driver = ResolveAnimationDriver();
            if (driver != null)
            {
                driver.PlaySpeaking();
                return;
            }

            TriggerAnimationLegacy(speakingTrigger);
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
