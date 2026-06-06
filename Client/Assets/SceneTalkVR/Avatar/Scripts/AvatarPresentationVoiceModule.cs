using System;
using System.Collections;
using SceneTalkVR.Core;
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

        [Header("Demo Voice")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip demoReplyClip;
        [SerializeField] private float fallbackSpeakingSeconds = 2f;

        [Header("Animation")]
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

            TriggerAnimation(thinkingTrigger);
            yield return null;

            Debug.Log($"[SceneTalkVR] Avatar reply: {payload.dialogueReply}", this);
            TriggerAnimation(speakingTrigger);

            if (audioSource != null && demoReplyClip != null)
            {
                audioSource.clip = demoReplyClip;
                audioSource.Play();
                yield return new WaitWhile(() => audioSource != null && audioSource.isPlaying);
            }
            else
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

            ReplaceCurrentAvatar(loadedAvatar);
            Debug.Log(
                $"[SceneTalkVR] Avatar resolved: key={resolution.avatarKey}, score={resolution.score}, fallback={resolution.fallbackLevel}",
                this);
        }

        private void ReplaceCurrentAvatar(GameObject loadedAvatar)
        {
            if (currentAvatar != null && currentAvatar != loadedAvatar)
            {
                Destroy(currentAvatar);
            }

            currentAvatar = loadedAvatar;
            currentAnimator = currentAvatar.GetComponentInChildren<Animator>();
        }

        private void TriggerAnimation(string triggerName)
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
