using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Runtime;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class ControllerOverlayRenderingTests
    {
        private const string ControllerShaderPath =
            "Assets/SceneTalkVR/Shaders/ControllerAlwaysOnTop.shader";

        [Test]
        public void ControllerMaterials_RenderAfterWorldUiWithoutDepthTesting()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ControllerShaderPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("SceneTalkVR/Controller/Always On Top"));

            var gameObject = new GameObject("Controller overlay rendering test");
            var bootstrap = gameObject.AddComponent<SceneTalkInteractionBootstrap>();
            Material visualMaterial = null;
            Material rayMaterial = null;
            Material visualInstanceMaterial = null;

            try
            {
                visualMaterial = InvokeMaterialResolver(bootstrap, "ResolveControllerVisualMaterial");
                rayMaterial = InvokeMaterialResolver(bootstrap, "ResolveControllerRayMaterial");

                Assert.That(visualMaterial.shader, Is.EqualTo(shader));
                Assert.That(rayMaterial.shader, Is.EqualTo(shader));
                Assert.That(visualMaterial.renderQueue, Is.EqualTo(4990));
                Assert.That(rayMaterial.renderQueue, Is.EqualTo(5000));
                Assert.That(visualMaterial.GetFloat("_UseVertexColor"), Is.Zero);
                Assert.That(rayMaterial.GetFloat("_UseVertexColor"), Is.EqualTo(1f));
                Assert.That(rayMaterial.color, Is.EqualTo(Color.white));
                Assert.That(
                    visualMaterial.GetInt("_SrcBlend"),
                    Is.EqualTo((int)UnityEngine.Rendering.BlendMode.One));
                Assert.That(
                    visualMaterial.GetInt("_DstBlend"),
                    Is.EqualTo((int)UnityEngine.Rendering.BlendMode.Zero));
                Assert.That(
                    rayMaterial.GetInt("_SrcBlend"),
                    Is.EqualTo((int)UnityEngine.Rendering.BlendMode.SrcAlpha));
                Assert.That(
                    rayMaterial.GetInt("_DstBlend"),
                    Is.EqualTo((int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));

                var rayObject = new GameObject("Controller ray sorting test");
                rayObject.transform.SetParent(gameObject.transform, false);
                var lineRenderer = rayObject.AddComponent<LineRenderer>();
                InvokePrivate(bootstrap, "ConfigureControllerRayLine", lineRenderer);
                Assert.That(lineRenderer.sortingOrder, Is.EqualTo(2001));

                var visual = (Transform)InvokePrivate(
                    bootstrap,
                    "CreateControllerPrimitive",
                    PrimitiveType.Cube,
                    "Controller visual sorting test",
                    gameObject.transform,
                    new Color(1f, 1f, 1f, 0.25f));
                var visualRenderer = visual.GetComponent<Renderer>();
                visualInstanceMaterial = visualRenderer.sharedMaterial;
                Assert.That(visualRenderer.sortingOrder, Is.EqualTo(2000));
                Assert.That(visualInstanceMaterial.color.a, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(visualInstanceMaterial);
                Object.DestroyImmediate(visualMaterial);
                Object.DestroyImmediate(rayMaterial);
                Object.DestroyImmediate(gameObject);
            }
        }

        private static Material InvokeMaterialResolver(
            SceneTalkInteractionBootstrap bootstrap,
            string methodName)
        {
            return (Material)InvokePrivate(bootstrap, methodName);
        }

        private static object InvokePrivate(
            SceneTalkInteractionBootstrap bootstrap,
            string methodName,
            params object[] arguments)
        {
            var method = typeof(SceneTalkInteractionBootstrap).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(bootstrap, arguments);
        }
    }
}
