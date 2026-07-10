using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarUserFacingController : MonoBehaviour
    {
        private Animator animator;
        private Transform avatarRoot;
        private Transform target;
        private bool alignBody;
        private float visualForwardYawOffset;
        private bool useHumanoidLookAt;
        private float lookAtWeight;
        private float bodyWeight;
        private float headWeight;
        private float eyesWeight;
        private float clampWeight;
        private bool bodyAligned;

        public void Configure(
            Animator animator,
            Transform avatarRoot,
            Transform target,
            bool alignBody,
            float visualForwardYawOffset,
            bool useHumanoidLookAt,
            float lookAtWeight,
            float bodyWeight,
            float headWeight,
            float eyesWeight,
            float clampWeight)
        {
            this.animator = animator;
            this.avatarRoot = avatarRoot;
            this.target = target;
            this.alignBody = alignBody;
            this.visualForwardYawOffset = visualForwardYawOffset;
            this.useHumanoidLookAt = useHumanoidLookAt;
            this.lookAtWeight = Mathf.Clamp01(lookAtWeight);
            this.bodyWeight = Mathf.Clamp01(bodyWeight);
            this.headWeight = Mathf.Clamp01(headWeight);
            this.eyesWeight = Mathf.Clamp01(eyesWeight);
            this.clampWeight = Mathf.Clamp01(clampWeight);
            bodyAligned = false;
            TryAlignBody();
        }

        private void LateUpdate()
        {
            if (!bodyAligned)
            {
                TryAlignBody();
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0
                || !useHumanoidLookAt
                || animator == null
                || !animator.isHuman
                || !TryResolveTarget())
            {
                return;
            }

            animator.SetLookAtWeight(
                lookAtWeight,
                bodyWeight,
                headWeight,
                eyesWeight,
                clampWeight);
            animator.SetLookAtPosition(target.position);
        }

        private void TryAlignBody()
        {
            if (!alignBody || avatarRoot == null || !TryResolveTarget())
            {
                return;
            }

            var direction = target.position - avatarRoot.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            avatarRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up)
                * Quaternion.Euler(0f, visualForwardYawOffset, 0f);
            bodyAligned = true;
        }

        private bool TryResolveTarget()
        {
            if (target != null)
            {
                return true;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            target = mainCamera.transform;
            return true;
        }
    }
}
