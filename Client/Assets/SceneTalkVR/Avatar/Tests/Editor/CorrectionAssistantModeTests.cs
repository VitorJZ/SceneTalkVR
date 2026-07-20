using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class CorrectionAssistantModeTests
    {
        private const string HumanoidPrefabPath =
            "Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/correction_assistant_woman.prefab";

        [Test]
        public void VisualModeValues_PreserveExistingSceneSerialization()
        {
            Assert.That((int)CorrectionAgentPresenter.VisualMode.GeneratedAgent, Is.EqualTo(0));
            Assert.That((int)CorrectionAgentPresenter.VisualMode.PrefabAvatar, Is.EqualTo(1));
            Assert.That((int)CorrectionAgentPresenter.VisualMode.AudioOnly, Is.EqualTo(2));
            Assert.That((int)CorrectionAgentPresenter.VisualMode.HumanoidAvatar, Is.EqualTo(3));
        }

        [Test]
        public void RuntimeAppearanceIdsExposeOnlyOfficialExperimentModes()
        {
            var host = new GameObject("Assistant Appearance Mapping Test");
            try
            {
                var presenter = host.AddComponent<CorrectionAgentPresenter>();

                Assert.That(
                    presenter.SetAppearanceId(ExperimentConditionManager.AudioOnlyAssistantEmbodiment),
                    Is.True);
                Assert.That(
                    presenter.CurrentVisualMode,
                    Is.EqualTo(CorrectionAgentPresenter.VisualMode.AudioOnly));

                Assert.That(
                    presenter.SetAppearanceId(ExperimentConditionManager.OrbAssistantEmbodiment),
                    Is.True);
                Assert.That(
                    presenter.CurrentVisualMode,
                    Is.EqualTo(CorrectionAgentPresenter.VisualMode.GeneratedAgent));

                Assert.That(
                    presenter.SetAppearanceId(ExperimentConditionManager.HumanoidAssistantEmbodiment),
                    Is.True);
                Assert.That(
                    presenter.CurrentVisualMode,
                    Is.EqualTo(CorrectionAgentPresenter.VisualMode.HumanoidAvatar));

                Assert.That(presenter.SetAppearanceId("bird"), Is.False);
                Assert.That(
                    presenter.CurrentVisualMode,
                    Is.EqualTo(CorrectionAgentPresenter.VisualMode.HumanoidAvatar));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LittleOrb_RepairsMissingRuntimeMaterialsWhenBindingSavedHierarchy()
        {
            var host = new GameObject("Little Orb Material Repair Test");
            try
            {
                var presenter = host.AddComponent<CorrectionAgentPresenter>();
                Assert.That(
                    presenter.SetAppearanceId(ExperimentConditionManager.OrbAssistantEmbodiment),
                    Is.True);
                presenter.ShowImmediate();

                var visualRoot = host.transform.Find(
                    "Correction Assistant Agent/Assistant Visuals");
                Assert.That(visualRoot, Is.Not.Null);
                var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty);

                var oldMaterials = new Material[renderers.Length];
                for (var index = 0; index < renderers.Length; index++)
                {
                    oldMaterials[index] = renderers[index].sharedMaterial;
                    renderers[index].sharedMaterial = null;
                }

                for (var index = 0; index < oldMaterials.Length; index++)
                {
                    if (oldMaterials[index] != null)
                    {
                        Object.DestroyImmediate(oldMaterials[index]);
                    }
                }

                typeof(CorrectionAgentPresenter)
                    .GetMethod("BindVisualHierarchy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, null);

                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.gameObject.name);
                    Assert.That(renderer.sharedMaterial.shader, Is.Not.Null, renderer.gameObject.name);
                    Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True, renderer.gameObject.name);
                    Assert.That(
                        renderer.sharedMaterial.shader.name,
                        Is.Not.EqualTo("Hidden/InternalErrorShader"),
                        renderer.gameObject.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AudioOnly_KeepsIndependentVoiceSourceWithoutVisuals()
        {
            var host = new GameObject("Audio Only Assistant Test");
            try
            {
                var presenter = host.AddComponent<CorrectionAgentPresenter>();
                SetMode(presenter, CorrectionAgentPresenter.VisualMode.AudioOnly);

                presenter.ShowImmediate();

                var root = host.transform.Find("Correction Assistant Agent");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.gameObject.activeSelf, Is.True);
                Assert.That(root.Find("Assistant Visuals").gameObject.activeSelf, Is.False);
                Assert.That(root.Find("Assistant Avatar"), Is.Null);
                Assert.That(root.Find("Assistant Humanoid"), Is.Null);

                var voice = root.Find("Assistant Voice")?.GetComponent<AudioSource>();
                Assert.That(voice, Is.Not.Null);
                Assert.That(voice.spatialBlend, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(root.GetComponent<Light>().enabled, Is.False);
                Assert.That(presenter.AppearanceId, Is.EqualTo("audio_only"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HumanoidAssistant_IsGroundedStableAndUsesTalkingParameter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run SceneTalkVR/Avatar/Build Correction Assistant Humanoid first.");

            var host = new GameObject("Humanoid Assistant Test");
            var anchor = new GameObject("AvatarRoot");
            var player = new GameObject("Player Head");
            try
            {
                anchor.transform.position = new Vector3(0f, 0f, 2.6f);
                player.transform.position = new Vector3(0f, 1.6f, 0f);

                var presenter = host.AddComponent<CorrectionAgentPresenter>();
                var serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("visualMode").enumValueIndex =
                    (int)CorrectionAgentPresenter.VisualMode.HumanoidAvatar;
                serializedPresenter.FindProperty("humanoidPrefab").objectReferenceValue = prefab;
                serializedPresenter.FindProperty("humanoidPlacementAnchor").objectReferenceValue = anchor.transform;
                serializedPresenter.FindProperty("humanoidLookTarget").objectReferenceValue = player.transform;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

                presenter.ShowImmediate();

                var root = host.transform.Find("Correction Assistant Agent");
                var humanoid = root.Find("Assistant Humanoid");
                Assert.That(humanoid, Is.Not.Null);
                Assert.That(humanoid.gameObject.activeSelf, Is.True);
                Assert.That(root.position.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(root.position.x, Is.EqualTo(1.15f).Within(0.001f));
                Assert.That(root.position.z, Is.EqualTo(2.72f).Within(0.001f));
                Assert.That(
                    humanoid.GetComponentsInChildren<Collider>(true),
                    Has.All.Matches<Collider>(collider => !collider.enabled));

                var animator = humanoid.GetComponentInChildren<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);

                presenter.BeginSpeaking();
                Assert.That(animator.GetBool("IsTalking"), Is.True);
                typeof(CorrectionAgentPresenter)
                    .GetMethod("UpdateRootMotion", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, new object[] { 1f });
                Assert.That(root.localScale, Is.EqualTo(Vector3.one));

                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var voice = humanoid.GetComponentInChildren<AudioSource>(true);
                Assert.That(voice, Is.Not.Null);
                Assert.That(voice.transform.parent, Is.EqualTo(head));
                Assert.That(voice.spatialBlend, Is.EqualTo(1f).Within(0.0001f));

                presenter.EndSpeaking();
                Assert.That(animator.GetBool("IsTalking"), Is.False);
                Assert.That(presenter.AppearanceId, Is.EqualTo("humanoid"));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(anchor);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void HumanoidAssistant_MatchesDialogueAvatarVisualHeight()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run SceneTalkVR/Avatar/Build Correction Assistant Humanoid first.");

            var host = new GameObject("Humanoid Assistant Scale Test");
            var anchor = new GameObject("AvatarRoot");
            var player = new GameObject("Player Head");
            var avatarVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                const float dialogueAvatarHeight = 2.1f;
                avatarVisual.name = "Dialogue Avatar Visual";
                avatarVisual.transform.SetParent(anchor.transform, false);
                avatarVisual.transform.localPosition = Vector3.up * dialogueAvatarHeight * 0.5f;
                avatarVisual.transform.localScale = new Vector3(0.5f, dialogueAvatarHeight, 0.4f);
                anchor.transform.position = new Vector3(0f, 0f, 2.6f);
                player.transform.position = new Vector3(0f, 1.6f, 0f);

                var presenter = host.AddComponent<CorrectionAgentPresenter>();
                var serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("visualMode").enumValueIndex =
                    (int)CorrectionAgentPresenter.VisualMode.HumanoidAvatar;
                serializedPresenter.FindProperty("humanoidPrefab").objectReferenceValue = prefab;
                serializedPresenter.FindProperty("humanoidPlacementAnchor").objectReferenceValue = anchor.transform;
                serializedPresenter.FindProperty("humanoidLookTarget").objectReferenceValue = player.transform;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

                presenter.ShowImmediate();

                var humanoid = host.transform.Find(
                    "Correction Assistant Agent/Assistant Humanoid");
                Assert.That(humanoid, Is.Not.Null);
                var assistantRenderers = humanoid.GetComponentsInChildren<Renderer>(true);
                var assistantBounds = assistantRenderers[0].bounds;
                for (var i = 1; i < assistantRenderers.Length; i++)
                {
                    assistantBounds.Encapsulate(assistantRenderers[i].bounds);
                }

                Assert.That(
                    assistantBounds.size.y,
                    Is.EqualTo(dialogueAvatarHeight).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(anchor);
                Object.DestroyImmediate(player);
            }
        }

        private static void SetMode(
            CorrectionAgentPresenter presenter,
            CorrectionAgentPresenter.VisualMode mode)
        {
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("visualMode").enumValueIndex = (int)mode;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
