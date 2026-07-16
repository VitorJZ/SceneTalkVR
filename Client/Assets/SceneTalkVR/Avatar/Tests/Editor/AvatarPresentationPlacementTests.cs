using NUnit.Framework;
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
    }
}
