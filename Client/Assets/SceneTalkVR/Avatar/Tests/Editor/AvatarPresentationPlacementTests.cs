using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class AvatarPresentationPlacementTests
    {
        [Test]
        public void AlignAvatarRootToPlacementAnchor_PlacesAvatarBehindUiAndLocksGroundHeight()
        {
            var host = new GameObject("AvatarPresentationPlacementTests");
            var avatarRoot = new GameObject("AvatarRoot");
            var placementAnchor = new GameObject("WorldUi");
            var user = new GameObject("UserHead");

            try
            {
                placementAnchor.transform.position = new Vector3(2f, 1.55f, 3f);
                user.transform.position = new Vector3(0f, 1.6f, 1f);

                var presenter = host.AddComponent<AvatarPresentationVoiceModule>();
                var serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("avatarRoot").objectReferenceValue = avatarRoot.transform;
                serializedPresenter.FindProperty("placementAnchor").objectReferenceValue = placementAnchor.transform;
                serializedPresenter.FindProperty("placementDepthFromAnchor").floatValue = 1f;
                serializedPresenter.FindProperty("constrainPlacementToGround").boolValue = true;
                serializedPresenter.FindProperty("placementGroundY").floatValue = 0.25f;
                serializedPresenter.FindProperty("userFacingTarget").objectReferenceValue = user.transform;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

                presenter.AlignAvatarRootToPlacementAnchor();

                var horizontalDirection = new Vector3(2f, 0f, 2f).normalized;
                var expected = placementAnchor.transform.position + horizontalDirection;
                expected.y = 0.25f;
                Assert.That(Vector3.Distance(avatarRoot.transform.position, expected), Is.LessThan(0.0001f));

                placementAnchor.transform.position = new Vector3(-4f, 2f, -5f);
                Assert.That(Vector3.Distance(avatarRoot.transform.position, expected), Is.LessThan(0.0001f),
                    "The avatar should remain fixed after its one-time placement.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(avatarRoot);
                Object.DestroyImmediate(placementAnchor);
                Object.DestroyImmediate(user);
            }
        }

        [Test]
        public void ClearAvatar_ResetsStreamingStateForNextOpeningReply()
        {
            var host = new GameObject("Avatar Streaming Reset Tests");

            try
            {
                var presenter = host.AddComponent<AvatarPresentationVoiceModule>();
                var previousPayload = new SpringScenePayload
                {
                    dialogueReply = "Previous streamed reply."
                };

                presenter.PrepareStreaming(previousPayload);
                presenter.OpenDialogueGate();
                var turn = GetPrivateField<object>(presenter, "streamingTurn");
                var segment = turn.GetType().GetMethod("Enqueue")
                    ?.Invoke(turn, new object[] { "stale sentence" });
                EnqueuePrivateValue(presenter, "streamingSentenceQueue", segment);
                SetPrivateField(presenter, "isAvatarLoadingFinished", true);

                presenter.ClearAvatar();

                Assert.That(turn.GetType().GetProperty("State")?.GetValue(turn)?.ToString(), Is.EqualTo("Idle"));
                Assert.That(GetPrivateField<bool>(presenter, "isDialogueGateOpen"), Is.False);
                Assert.That(GetPrivateField<bool>(presenter, "isAvatarLoadingFinished"), Is.False);
                Assert.That(GetPrivateField<object>(presenter, "streamingBasePayload"), Is.Null);
                Assert.That(GetPrivateCollectionCount(presenter, "streamingSentenceQueue"), Is.Zero);
                Assert.That(GetPrivateCollectionCount(presenter, "streamingPreparedQueue"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void EnqueuePrivateValue(object target, string fieldName, object value)
        {
            var queue = GetPrivateField<object>(target, fieldName);
            queue.GetType().GetMethod("Enqueue")?.Invoke(queue, new[] { value });
        }

        private static int GetPrivateCollectionCount(object target, string fieldName)
        {
            var collection = GetPrivateField<object>(target, fieldName);
            return (int)collection.GetType().GetProperty("Count")?.GetValue(collection);
        }
    }
}
