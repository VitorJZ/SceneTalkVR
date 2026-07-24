using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Runtime.Services;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class PilotSceneMappingTests
    {
        [TestCase("pilot_restaurant_walk_in", "PilotEnvironmentVariants/WalkInScene")]
        [TestCase("pilot_restaurant_ordering", "PilotEnvironmentVariants/OrderingScene")]
        [TestCase("pilot_restaurant_wrong_dish", "PilotEnvironmentVariants/WrongDishScene")]
        public void PilotTask_ResolvesToDedicatedAuthoredScene(string taskId, string expectedPath)
        {
            var method = typeof(HybridScenePresenter).GetMethod(
                "ResolveStaticSceneName",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { "restaurant", taskId }), Is.EqualTo(expectedPath));
        }

        [Test]
        public void NestedPilotSceneActivation_IsExclusiveAndPreservesModelContents()
        {
            var rootObject = new GameObject("SceneContentRoot");
            var formalScene = Child(rootObject.transform, "HotelLobbyScene");
            var variants = Child(rootObject.transform, "PilotEnvironmentVariants");
            var walkIn = Child(variants, "WalkInScene");
            var ordering = Child(variants, "OrderingScene");
            var wrongDish = Child(variants, "WrongDishScene");
            var model = Child(ordering, "RestaurantModel");

            try
            {
                formalScene.gameObject.SetActive(true);
                walkIn.gameObject.SetActive(true);
                ordering.gameObject.SetActive(false);
                wrongDish.gameObject.SetActive(true);

                var method = typeof(HybridScenePresenter).GetMethod(
                    "ActivateExclusiveStaticScenePath",
                    BindingFlags.NonPublic | BindingFlags.Static);

                Assert.That(method, Is.Not.Null);
                method.Invoke(null, new object[] { rootObject.transform, ordering });

                Assert.That(formalScene.gameObject.activeSelf, Is.False);
                Assert.That(variants.gameObject.activeSelf, Is.True);
                Assert.That(walkIn.gameObject.activeSelf, Is.False);
                Assert.That(ordering.gameObject.activeSelf, Is.True);
                Assert.That(wrongDish.gameObject.activeSelf, Is.False);
                Assert.That(model.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
