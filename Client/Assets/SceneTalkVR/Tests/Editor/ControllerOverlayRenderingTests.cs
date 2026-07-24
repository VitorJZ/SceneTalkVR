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
            }
            finally
            {
                Object.DestroyImmediate(visualMaterial);
                Object.DestroyImmediate(rayMaterial);
                Object.DestroyImmediate(gameObject);
            }
        }

        private static Material InvokeMaterialResolver(
            SceneTalkInteractionBootstrap bootstrap,
            string methodName)
        {
            var method = typeof(SceneTalkInteractionBootstrap).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Material)method.Invoke(bootstrap, null);
        }
    }
}
