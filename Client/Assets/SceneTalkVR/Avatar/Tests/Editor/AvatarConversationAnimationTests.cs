using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class AvatarConversationAnimationTests
    {
        private const string ControllerPath =
            "Assets/SceneTalkVR/Avatar/Animations/Common/AvatarCommonHumanoid.controller";

        [TestCase("teacher_humanoid_v1")]
        [TestCase("barista_humanoid_v1")]
        [TestCase("police_humanoid_v1")]
        [TestCase("barista_male_humanoid_v1")]
        [TestCase("teacher_female_humanoid_v1")]
        [TestCase("police_female_humanoid_v1")]
        public void HumanoidPrefab_UsesLoopingNativeIdleWithFootCurves(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/{prefabName}.prefab");
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>() : null;

            Assert.That(prefab, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);

            var idleClips = animator.runtimeAnimatorController.animationClips
                .Where(clip => clip != null
                    && clip.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .ToArray();

            Assert.That(idleClips, Has.Length.EqualTo(1));
            var idleClip = idleClips[0];
            Assert.That(idleClip.name, Does.Not.StartWith("__preview__"));
            Assert.That(idleClip.humanMotion, Is.True);
            Assert.That(idleClip.isLooping, Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(idleClip),
                Does.StartWith("Assets/SceneTalkVR/Avatar/Animations/Common/NativeIdle/"));

            var idleBindings = AnimationUtility.GetCurveBindings(idleClip);
            Assert.That(idleBindings, Has.Length.GreaterThan(130));
            var footBindingPaths = idleBindings
                .Where(binding => binding.type == typeof(Transform))
                .Select(binding => binding.path)
                .Distinct()
                .ToArray();
            Assert.That(footBindingPaths, Contains.Item("CharacterArmature/Root/Foot.L"));
            Assert.That(footBindingPaths, Contains.Item("CharacterArmature/Root/Foot.R"));
            Assert.That(footBindingPaths, Contains.Item("CharacterArmature/Root/PT.L"));
            Assert.That(footBindingPaths, Contains.Item("CharacterArmature/Root/PT.R"));
        }

        [TestCase("teacher_humanoid_v1", "IsThinking")]
        [TestCase("teacher_humanoid_v1", "IsTalking")]
        [TestCase("barista_humanoid_v1", "IsThinking")]
        [TestCase("barista_humanoid_v1", "IsTalking")]
        [TestCase("police_humanoid_v1", "IsThinking")]
        [TestCase("police_humanoid_v1", "IsTalking")]
        [TestCase("barista_male_humanoid_v1", "IsThinking")]
        [TestCase("barista_male_humanoid_v1", "IsTalking")]
        [TestCase("teacher_female_humanoid_v1", "IsThinking")]
        [TestCase("teacher_female_humanoid_v1", "IsTalking")]
        [TestCase("police_female_humanoid_v1", "IsThinking")]
        [TestCase("police_female_humanoid_v1", "IsTalking")]
        public void ConversationLayer_PreservesNativeLowerBody(
            string prefabName,
            string conversationParameter)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/{prefabName}.prefab");
            Assert.That(prefab, Is.Not.Null);

            var idleInstance = Object.Instantiate(prefab);
            var conversationInstance = Object.Instantiate(prefab);
            try
            {
                var idleAnimator = idleInstance.GetComponentInChildren<Animator>();
                var conversationAnimator = conversationInstance.GetComponentInChildren<Animator>();
                PrepareAnimator(idleAnimator);
                PrepareAnimator(conversationAnimator);
                conversationAnimator.SetBool(conversationParameter, true);

                AdvanceAnimators(idleAnimator, conversationAnimator, 0.5f);

                var lowerBodyBones = new[]
                {
                    HumanBodyBones.Hips,
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot,
                    HumanBodyBones.RightUpperLeg,
                    HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot
                };
                foreach (var bone in lowerBodyBones)
                {
                    var idleBone = idleAnimator.GetBoneTransform(bone);
                    var conversationBone = conversationAnimator.GetBoneTransform(bone);
                    Assert.That(idleBone, Is.Not.Null, $"Idle is missing {bone}.");
                    Assert.That(conversationBone, Is.Not.Null, $"Conversation is missing {bone}.");
                    Assert.That(
                        Vector3.Distance(idleBone.position, conversationBone.position),
                        Is.LessThan(0.0001f),
                        $"{prefabName}/{conversationParameter} moved {bone}.");
                    Assert.That(
                        Quaternion.Angle(idleBone.rotation, conversationBone.rotation),
                        Is.LessThan(0.01f),
                        $"{prefabName}/{conversationParameter} rotated {bone}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(idleInstance);
                Object.DestroyImmediate(conversationInstance);
            }
        }

        [TestCase("teacher_humanoid_v1")]
        [TestCase("barista_humanoid_v1")]
        [TestCase("police_humanoid_v1")]
        [TestCase("barista_male_humanoid_v1")]
        [TestCase("teacher_female_humanoid_v1")]
        [TestCase("police_female_humanoid_v1")]
        public void ThinkingHeadStabilization_PreservesNativeHeadPose(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/{prefabName}.prefab");
            Assert.That(prefab, Is.Not.Null);

            var idleInstance = Object.Instantiate(prefab);
            var thinkingInstance = Object.Instantiate(prefab);
            try
            {
                var idleAnimator = idleInstance.GetComponentInChildren<Animator>();
                var thinkingAnimator = thinkingInstance.GetComponentInChildren<Animator>();
                PrepareAnimator(idleAnimator);
                PrepareAnimator(thinkingAnimator);
                var driver = thinkingInstance.AddComponent<AvatarAnimationDriver>();
                driver.BindAnimator(thinkingAnimator);
                Assert.That(driver.SetThinking(true), Is.True);

                AdvanceAnimators(idleAnimator, thinkingAnimator, 3f);

                Assert.That(
                    thinkingAnimator.GetCurrentAnimatorStateInfo(2).IsName("ThinkingHeadIdle"),
                    Is.True);
                var idleHead = idleAnimator.GetBoneTransform(HumanBodyBones.Head);
                var thinkingHead = thinkingAnimator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(idleHead, Is.Not.Null);
                Assert.That(thinkingHead, Is.Not.Null);
                Assert.That(
                    Quaternion.Angle(idleHead.rotation, thinkingHead.rotation),
                    Is.LessThan(10f),
                    $"{prefabName} retained too much of the Thinking clip's head tilt.");
            }
            finally
            {
                Object.DestroyImmediate(idleInstance);
                Object.DestroyImmediate(thinkingInstance);
            }
        }

        [TestCase(
            "Assets/SceneTalkVR/Avatar/Animations/Mixamo/Thoughtful Head Nod 70AS.fbx",
            "TalkLoop")]
        public void MixamoAnimation_IsLoopingHumanoid(string assetPath, string clipName)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var animator = model != null ? model.GetComponentInChildren<Animator>() : null;
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);

            var clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == clipName);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.isLooping, Is.True);
        }

        [Test]
        public void ThinkingAnimation_UsesNonLoopingEntryAndLoopingHold()
        {
            const string assetPath = "Assets/SceneTalkVR/Avatar/Animations/Mixamo/Thinking.fbx";
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));

            var enterSettings = importer.clipAnimations.Single(clip => clip.name == "ThinkingEnter");
            Assert.That(enterSettings.firstFrame, Is.EqualTo(0f));
            Assert.That(enterSettings.lastFrame, Is.EqualTo(46f));
            Assert.That(enterSettings.loopTime, Is.False);

            var holdSettings = importer.clipAnimations.Single(clip => clip.name == "ThinkingHold");
            Assert.That(holdSettings.firstFrame, Is.EqualTo(46f));
            Assert.That(holdSettings.lastFrame, Is.EqualTo(70f));
            Assert.That(holdSettings.loopTime, Is.True);
            Assert.That(holdSettings.loopPose, Is.True);

            var clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .ToArray();
            Assert.That(clips.Single(clip => clip.name == "ThinkingEnter").isLooping, Is.False);
            Assert.That(clips.Single(clip => clip.name == "ThinkingHold").isLooping, Is.True);
        }

        [Test]
        public void QuaterniusAvatar_UsesValidHumanoidAvatar()
        {
            const string assetPath =
                "Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusAnimatedWoman/barista_animated_woman.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            var animator = model != null ? model.GetComponentInChildren<Animator>() : null;

            Assert.That(model, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(importer.humanDescription.skeleton, Is.Not.Empty);
        }

        [Test]
        public void SharedController_UsesConversationStateProtocol()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);

            AssertParameter(controller, "Speak", AnimatorControllerParameterType.Trigger);
            AssertParameter(controller, "IsThinking", AnimatorControllerParameterType.Bool);
            AssertParameter(controller, "IsTalking", AnimatorControllerParameterType.Bool);

            var baseLayer = controller.layers.Single(layer => layer.name == "Base Layer");
            Assert.That(baseLayer.stateMachine.states.Select(state => state.state.name), Contains.Item("Idle"));

            var conversationLayer = controller.layers.Single(
                layer => layer.name == "Upper Body Conversation");
            var states = conversationLayer.stateMachine.states.Select(state => state.state).ToArray();
            Assert.That(states.Select(state => state.name), Is.EquivalentTo(new[]
            {
                "ConversationIdle",
                "ThinkingEnter",
                "ThinkingHold",
                "SpeakWave",
                "TalkLoop"
            }));
            var thinkingEnter = states.Single(state => state.name == "ThinkingEnter");
            var thinkingHold = states.Single(state => state.name == "ThinkingHold");
            Assert.That(thinkingEnter.motion.name, Is.EqualTo("ThinkingEnter"));
            Assert.That(thinkingHold.motion.name, Is.EqualTo("ThinkingHold"));
            var enterToHold = thinkingEnter.transitions.Single(
                transition => transition.destinationState == thinkingHold);
            Assert.That(enterToHold.hasExitTime, Is.True);
            Assert.That(enterToHold.exitTime, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(
                enterToHold.conditions.Single().parameter,
                Is.EqualTo("IsThinking"));
            Assert.That(states.Single(state => state.name == "TalkLoop").motion.name, Is.EqualTo("TalkLoop"));
            Assert.That(
                states.Single(state => state.name == "ConversationIdle").motion.name,
                Does.Contain("idle").IgnoreCase);

            Assert.That(conversationLayer.avatarMask, Is.Not.Null);
            Assert.That(
                conversationLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body),
                Is.False);
            Assert.That(
                conversationLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg),
                Is.False);
            Assert.That(
                conversationLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg),
                Is.False);
            Assert.That(
                conversationLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers),
                Is.False);
            Assert.That(
                conversationLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers),
                Is.False);

            var headLayer = controller.layers.Single(
                layer => layer.name == "Thinking Head Stabilization");
            var headStates = headLayer.stateMachine.states.Select(state => state.state).ToArray();
            Assert.That(headStates.Select(state => state.name), Is.EquivalentTo(new[]
            {
                "HeadIdle",
                "ThinkingHeadIdle",
                "HeadSpeakWave",
                "HeadTalkLoop"
            }));
            Assert.That(headLayer.defaultWeight, Is.EqualTo(1f));
            Assert.That(
                headStates.Single(state => state.name == "HeadIdle").motion.name,
                Does.Contain("idle").IgnoreCase);
            Assert.That(
                headStates.Single(state => state.name == "ThinkingHeadIdle").motion.name,
                Does.Contain("idle").IgnoreCase);
            Assert.That(
                headStates.Single(state => state.name == "HeadSpeakWave").motion.name,
                Does.EndWith("Wave"));
            Assert.That(
                headStates.Single(state => state.name == "HeadTalkLoop").motion.name,
                Is.EqualTo("TalkLoop"));
            Assert.That(headLayer.avatarMask, Is.Not.Null);
            Assert.That(
                headLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head),
                Is.True);
            Assert.That(
                headLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body),
                Is.False);
            Assert.That(
                headLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm),
                Is.False);
            Assert.That(
                headLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm),
                Is.False);
        }

        [Test]
        public void AnimationDriver_ControlsThinkingAndTalkingBools()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_humanoid_v1.prefab");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var driver = instance.AddComponent<AvatarAnimationDriver>();
                driver.BindAnimator(animator);

                Assert.That(driver.SetThinking(true), Is.True);
                Assert.That(animator.GetBool("IsThinking"), Is.True);

                Assert.That(driver.BeginTalking(), Is.True);
                Assert.That(animator.GetBool("IsThinking"), Is.False);
                Assert.That(animator.GetBool("IsTalking"), Is.True);

                Assert.That(driver.EndTalking(), Is.True);
                Assert.That(animator.GetBool("IsTalking"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SharedController_FollowsOpeningAndFollowUpStateFlow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_humanoid_v1.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var driver = instance.AddComponent<AvatarAnimationDriver>();
                driver.BindAnimator(animator);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();

                animator.SetBool("IsTalking", true);
                animator.SetTrigger("Speak");
                AdvanceAnimator(animator, 0.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("SpeakWave"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("HeadSpeakWave"), Is.True);

                var waveLength = animator.runtimeAnimatorController.animationClips
                    .First(clip => clip.name.EndsWith("Wave"))
                    .length;
                AdvanceAnimator(animator, waveLength + 0.5f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("TalkLoop"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("HeadTalkLoop"), Is.True);

                animator.SetBool("IsTalking", false);
                AdvanceAnimator(animator, 0.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("ConversationIdle"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("HeadIdle"), Is.True);

                Assert.That(driver.SetThinking(true), Is.True);
                AdvanceAnimator(animator, 0.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("ThinkingEnter"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("ThinkingHeadIdle"), Is.True);

                var thinkingEnterLength = animator.runtimeAnimatorController.animationClips
                    .Single(clip => clip.name == "ThinkingEnter")
                    .length;
                AdvanceAnimator(animator, thinkingEnterLength + 0.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("ThinkingHold"), Is.True);

                var thinkingHoldLength = animator.runtimeAnimatorController.animationClips
                    .Single(clip => clip.name == "ThinkingHold")
                    .length;
                AdvanceAnimator(animator, thinkingHoldLength * 2f + 0.2f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("ThinkingHold"), Is.True);

                Assert.That(driver.BeginTalking(), Is.True);
                AdvanceAnimator(animator, 0.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("TalkLoop"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("HeadTalkLoop"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("teacher_humanoid_v1")]
        [TestCase("barista_humanoid_v1")]
        [TestCase("police_humanoid_v1")]
        [TestCase("barista_male_humanoid_v1")]
        [TestCase("teacher_female_humanoid_v1")]
        [TestCase("police_female_humanoid_v1")]
        public void TalkLoop_PreservesHeadMotionAfterThinking(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/{prefabName}.prefab");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                PrepareAnimator(animator);
                var driver = instance.AddComponent<AvatarAnimationDriver>();
                driver.BindAnimator(animator);
                Assert.That(driver.SetThinking(true), Is.True);
                AdvanceAnimator(animator, 0.5f);
                Assert.That(driver.BeginTalking(), Is.True);
                AdvanceAnimator(animator, 0.5f);

                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("TalkLoop"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("HeadTalkLoop"), Is.True);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(head, Is.Not.Null);
                var startRotation = head.localRotation;
                var maxHeadDelta = 0f;
                for (var i = 0; i < 40; i++)
                {
                    animator.Update(0.1f);
                    maxHeadDelta = Mathf.Max(
                        maxHeadDelta,
                        Quaternion.Angle(startRotation, head.localRotation));
                }

                Assert.That(
                    maxHeadDelta,
                    Is.GreaterThan(5f),
                    $"{prefabName} lost the TalkLoop head motion.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("teacher_humanoid_v1")]
        [TestCase("barista_humanoid_v1")]
        [TestCase("police_humanoid_v1")]
        [TestCase("barista_male_humanoid_v1")]
        [TestCase("teacher_female_humanoid_v1")]
        [TestCase("police_female_humanoid_v1")]
        public void ThinkingToIdle_DoesNotExposeSourceHeadTilt(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/{prefabName}.prefab");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                PrepareAnimator(animator);
                var driver = instance.AddComponent<AvatarAnimationDriver>();
                driver.BindAnimator(animator);
                Assert.That(driver.SetThinking(true), Is.True);
                AdvanceAnimator(animator, 3f);

                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(head, Is.Not.Null);
                var previousRotation = head.localRotation;
                var maxSingleFrameDelta = 0f;
                Assert.That(driver.SetThinking(false), Is.True);
                for (var i = 0; i < 30; i++)
                {
                    animator.Update(0.01f);
                    var currentRotation = head.localRotation;
                    maxSingleFrameDelta = Mathf.Max(
                        maxSingleFrameDelta,
                        Quaternion.Angle(previousRotation, currentRotation));
                    previousRotation = currentRotation;
                }

                Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("ConversationIdle"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("HeadIdle"), Is.True);
                Assert.That(
                    maxSingleFrameDelta,
                    Is.LessThan(10f),
                    $"{prefabName} exposed the source Thinking head tilt while returning to idle.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AdvanceAnimator(Animator animator, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                const float step = 0.1f;
                animator.Update(step);
                elapsed += step;
            }
        }

        private static void PrepareAnimator(Animator animator)
        {
            Assert.That(animator, Is.Not.Null);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
        }

        private static void AdvanceAnimators(Animator first, Animator second, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                const float step = 0.05f;
                first.Update(step);
                second.Update(step);
                elapsed += step;
            }
        }

        private static void AssertParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            var parameter = controller.parameters.Single(candidate => candidate.name == name);
            Assert.That(parameter.type, Is.EqualTo(type));
        }
    }
}
