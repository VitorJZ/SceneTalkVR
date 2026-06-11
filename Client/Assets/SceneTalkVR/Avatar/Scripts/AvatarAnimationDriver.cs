using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator fallbackAnimator;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string thinkingTrigger = "Think";
        [SerializeField] private string speakingTrigger = "Speak";
        [SerializeField] private bool useFallbackAnimator = true;

        private Animator currentAnimator;

        public Animator CurrentAnimator => currentAnimator;

        public void BindAvatar(GameObject avatar)
        {
            currentAnimator = avatar != null ? avatar.GetComponentInChildren<Animator>() : null;
        }

        public void BindAnimator(Animator animator)
        {
            currentAnimator = animator;
        }

        public void SetFallbackAnimator(Animator animator)
        {
            fallbackAnimator = animator;
        }

        public bool PlayIdle()
        {
            return TryPlayState(idleStateName);
        }

        public bool PlayThinking()
        {
            return TryPlayTrigger(thinkingTrigger);
        }

        public bool PlaySpeaking()
        {
            return TryPlayTrigger(speakingTrigger);
        }

        public bool TryPlayTrigger(string triggerName)
        {
            if (TrySetTrigger(currentAnimator, triggerName))
            {
                return true;
            }

            return useFallbackAnimator
                && fallbackAnimator != currentAnimator
                && TrySetTrigger(fallbackAnimator, triggerName);
        }

        private bool TryPlayState(string stateName)
        {
            if (TryCrossFade(currentAnimator, stateName))
            {
                return true;
            }

            return useFallbackAnimator
                && fallbackAnimator != currentAnimator
                && TryCrossFade(fallbackAnimator, stateName);
        }

        private static bool TrySetTrigger(Animator animator, string triggerName)
        {
            if (!CanUse(animator) || string.IsNullOrWhiteSpace(triggerName) || !HasTrigger(animator, triggerName))
            {
                return false;
            }

            animator.SetTrigger(triggerName);
            return true;
        }

        private static bool TryCrossFade(Animator animator, string stateName)
        {
            if (!CanUse(animator) || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            var stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
            {
                return false;
            }

            animator.CrossFadeInFixedTime(stateHash, 0.1f);
            return true;
        }

        private static bool CanUse(Animator animator)
        {
            return animator != null
                && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController != null;
        }

        private static bool HasTrigger(Animator animator, string triggerName)
        {
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
