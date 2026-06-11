using System;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public sealed class AvatarAttachmentSockets : MonoBehaviour
    {
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform hipsOverride;
        [SerializeField] private Transform chestOverride;
        [SerializeField] private Transform headOverride;
        [SerializeField] private Transform leftHandOverride;
        [SerializeField] private Transform rightHandOverride;

        private Animator animator;

        private Animator Animator
        {
            get
            {
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>();
                }

                return animator;
            }
        }

        public Transform Resolve(AvatarPropSocket socket)
        {
            switch (socket)
            {
                case AvatarPropSocket.Hips:
                    return hipsOverride != null ? hipsOverride : ResolveHumanoidBone(HumanBodyBones.Hips);
                case AvatarPropSocket.Chest:
                    return chestOverride != null ? chestOverride : ResolveHumanoidBone(HumanBodyBones.Chest);
                case AvatarPropSocket.Head:
                    return headOverride != null ? headOverride : ResolveHumanoidBone(HumanBodyBones.Head);
                case AvatarPropSocket.LeftHand:
                    return leftHandOverride != null ? leftHandOverride : ResolveHumanoidBone(HumanBodyBones.LeftHand);
                case AvatarPropSocket.RightHand:
                    return rightHandOverride != null ? rightHandOverride : ResolveHumanoidBone(HumanBodyBones.RightHand);
                case AvatarPropSocket.World:
                case AvatarPropSocket.AvatarRoot:
                default:
                    return avatarRoot != null ? avatarRoot : transform;
            }
        }

        private Transform ResolveHumanoidBone(HumanBodyBones bone)
        {
            var currentAnimator = Animator;
            if (currentAnimator != null && currentAnimator.avatar != null && currentAnimator.avatar.isHuman)
            {
                try
                {
                    var boneTransform = currentAnimator.GetBoneTransform(bone);
                    if (boneTransform != null)
                    {
                        return boneTransform;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Some imported assets report an Animator but not a usable humanoid map.
                }
            }

            return avatarRoot != null ? avatarRoot : transform;
        }
    }
}
