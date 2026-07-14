using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator fallbackAnimator;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string openingSpeechTrigger = "Speak";
        [SerializeField] private string thinkingParameter = "IsThinking";
        [SerializeField] private string talkingParameter = "IsTalking";
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

        public bool SetThinking(bool active)
        {
            return TrySetBool(thinkingParameter, active);
        }

        public bool BeginOpeningSpeech()
        {
            var talkingSet = TrySetBool(talkingParameter, true);
            var openingTriggered = TryPlayTrigger(openingSpeechTrigger);
            return talkingSet || openingTriggered;
        }

        public bool BeginTalking()
        {
            return TrySetBool(talkingParameter, true);
        }

        public bool EndTalking()
        {
            return TrySetBool(talkingParameter, false);
        }

        public void ResetState()
        {
            TrySetBool(thinkingParameter, false);
            TrySetBool(talkingParameter, false);
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

        private bool TrySetBool(string parameterName, bool value)
        {
            if (TrySetBool(currentAnimator, parameterName, value))
            {
                return true;
            }

            return useFallbackAnimator
                && fallbackAnimator != currentAnimator
                && TrySetBool(fallbackAnimator, parameterName, value);
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

        private static bool TrySetBool(Animator animator, string parameterName, bool value)
        {
            if (!CanUse(animator) || string.IsNullOrWhiteSpace(parameterName) || !HasParameter(
                    animator,
                    parameterName,
                    AnimatorControllerParameterType.Bool))
            {
                return false;
            }

            animator.SetBool(parameterName, value);
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
            return HasParameter(animator, triggerName, AnimatorControllerParameterType.Trigger);
        }

        private static bool HasParameter(
            Animator animator,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == parameterType && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
