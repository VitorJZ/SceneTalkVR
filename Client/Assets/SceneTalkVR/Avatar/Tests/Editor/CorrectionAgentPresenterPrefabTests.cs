using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class CorrectionAgentPresenterPrefabTests
    {
        private const string SparrowPrefabPath =
            "Assets/Quirky Series Ultimate/FREE/Prefabs/Sparrow.prefab";
        private const string SparrowMaterialPath =
            "Assets/Quirky Series Ultimate/FREE/Materials/M_Sparrow.mat";

        [Test]
        public void SparrowAssistant_UsesIdleAndTalkAnimations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SparrowPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Material>(SparrowMaterialPath).shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));

            var host = new GameObject("Correction Assistant Test");
            var player = new GameObject("Player Head");
            try
            {
                player.transform.position = new Vector3(4f, 3f, -2f);
                var presenter = host.AddComponent<CorrectionAgentPresenter>();
                var serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("visualMode").enumValueIndex =
                    (int)CorrectionAgentPresenter.VisualMode.PrefabAvatar;
                serializedPresenter.FindProperty("avatarPrefab").objectReferenceValue = prefab;
                serializedPresenter.FindProperty("avatarFacingYawOffset").floatValue = 12f;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
                typeof(CorrectionAgentPresenter)
                    .GetField("lookTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(presenter, player.transform);

                presenter.ShowImmediate();

                var avatar = host.transform.Find(
                    "Correction Assistant Agent/Assistant Avatar");
                Assert.That(avatar, Is.Not.Null);
                Assert.That(
                    host.transform.Find("Correction Assistant Agent/Assistant Visuals")
                        .gameObject.activeSelf,
                    Is.False,
                    "Generated visual should be disabled in PrefabAvatar mode.");

                var animator = avatar.GetComponentInChildren<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(
                    avatar.GetComponentsInChildren<Collider>(true),
                    Has.All.Matches<Collider>(collider => !collider.enabled));

                var agentRoot = avatar.parent;
                var basePosition = agentRoot.localPosition;
                typeof(CorrectionAgentPresenter)
                    .GetMethod("UpdateRootMotion", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, new object[] { 1f });
                Assert.That(
                    agentRoot.localPosition,
                    Is.EqualTo(basePosition),
                    "Prefab avatars should rely on their own animation instead of generated floating motion.");

                typeof(CorrectionAgentPresenter)
                    .GetMethod("UpdateFaceDirection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, new object[] { 100f });
                var playerDirection = Vector3.ProjectOnPlane(
                    player.transform.position - avatar.position,
                    agentRoot.up);
                var expectedRotation = Quaternion.LookRotation(playerDirection, agentRoot.up)
                    * Quaternion.AngleAxis(12f, Vector3.up)
                    * Quaternion.Euler(Vector3.zero);
                Assert.That(
                    Quaternion.Angle(avatar.rotation, expectedRotation),
                    Is.LessThan(0.01f),
                    "Prefab avatars should stay upright and keep the configured yaw offset while facing the player.");

                animator.Update(0.1f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Idle_A"),
                    Is.True,
                    "Sparrow should start in Idle_A, not the asset controller's Attack default.");

                presenter.BeginSpeaking();
                animator.Update(0.2f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Bounce"),
                    Is.True,
                    "BeginSpeaking should select Bounce.");

                presenter.EndSpeaking();
                animator.Update(0.2f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Idle_A"),
                    Is.True,
                    "EndSpeaking should return to Idle_A.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(player);
            }
        }
    }
}
