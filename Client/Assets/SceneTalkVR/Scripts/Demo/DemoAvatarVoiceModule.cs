using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Demo
{
    public sealed class DemoAvatarVoiceModule : MonoBehaviour, ISceneTalkAvatarVoice
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip demoReplyClip;
        [SerializeField] private Animator avatarAnimator;
        [SerializeField] private string thinkingTrigger = "Think";
        [SerializeField] private string speakingTrigger = "Speak";
        [SerializeField] private float fallbackSpeakingSeconds = 2f;

        public IEnumerator PresentReply(SpringScenePayload payload, Action onComplete, Action<string> onError)
        {
            if (payload == null)
            {
                onError?.Invoke("Avatar voice payload is null.");
                yield break;
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

        private void TriggerAnimation(string triggerName)
        {
            if (avatarAnimator == null || string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            avatarAnimator.SetTrigger(triggerName);
        }
    }
}
