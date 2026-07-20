using NUnit.Framework;
using SceneTalkVR.Runtime;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class WorldCanvasVisibilityTests
    {
        private sealed class TestTrackedPoseDriver : MonoBehaviour
        {
        }

        [Test]
        public void WorldCanvasFacesHeadsetWithoutChangingItsPosition()
        {
            var host = new GameObject("World Canvas Visibility Test");
            var cameraObject = new GameObject("Headset Camera", typeof(Camera));
            var canvasObject = new GameObject("World Canvas", typeof(RectTransform), typeof(Canvas));

            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(1.2f, 1.7f, -0.4f);
                canvasObject.transform.position = new Vector3(-0.3f, 1.5f, 1.8f);
                canvasObject.transform.rotation = Quaternion.Euler(0f, 110f, 0f);

                var bootstrap = host.AddComponent<SceneTalkInteractionBootstrap>();
                var serializedBootstrap = new SerializedObject(bootstrap);
                serializedBootstrap.FindProperty("interactionCamera").objectReferenceValue = camera;
                serializedBootstrap.FindProperty("worldCanvas").objectReferenceValue = canvasObject.GetComponent<Canvas>();
                serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

                var originalPosition = canvasObject.transform.position;
                Assert.That(bootstrap.KeepWorldCanvasFacingHeadset(), Is.True);

                var expectedForward = Vector3.ProjectOnPlane(
                    originalPosition - camera.transform.position,
                    Vector3.up).normalized;
                Assert.That(
                    Vector3.Dot(canvasObject.transform.forward, expectedForward),
                    Is.GreaterThan(0.9999f));
                Assert.That(canvasObject.transform.position, Is.EqualTo(originalPosition));
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TrackedCameraKeepsItsRuntimeControlledFieldOfView()
        {
            var cameraObject = new GameObject(
                "Tracked Headset Camera",
                typeof(Camera),
                typeof(TestTrackedPoseDriver));

            try
            {
                Assert.That(
                    SceneTalkInteractionBootstrap.ShouldControlCameraFieldOfView(
                        cameraObject.GetComponent<Camera>()),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void DesktopCameraStillUsesConfiguredFieldOfView()
        {
            var cameraObject = new GameObject("Desktop Camera", typeof(Camera));

            try
            {
                Assert.That(
                    SceneTalkInteractionBootstrap.ShouldControlCameraFieldOfView(
                        cameraObject.GetComponent<Camera>()),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
