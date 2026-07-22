using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class AvatarFixedScenarioResolverTests
    {
        private GameObject host;
        private GameObject restaurantPrefab;
        private GameObject furniturePrefab;
        private AvatarCatalog catalog;
        private AvatarPresetResolver resolver;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("AvatarFixedScenarioResolverTests");
            restaurantPrefab = new GameObject("RestaurantPrefab");
            furniturePrefab = new GameObject("FurniturePrefab");
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            catalog.defaultAvatarKey = "restaurant";
            catalog.presets = new[]
            {
                CreateEntry("restaurant", "restaurant_reservation", restaurantPrefab),
                CreateEntry("furniture", "furniture_shopping", furniturePrefab)
            };

            resolver = host.AddComponent<AvatarPresetResolver>();
            var serializedResolver = new SerializedObject(resolver);
            serializedResolver.FindProperty("catalog").objectReferenceValue = catalog;
            serializedResolver.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(restaurantPrefab);
            Object.DestroyImmediate(furniturePrefab);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Resolve_UsesTaskTypeInsteadOfRoleOrEnvironmentKeywords()
        {
            var payload = new SpringScenePayload
            {
                taskType = "furniture_shopping",
                environmentType = "restaurant",
                avatarRole = new AvatarRoleData { role = "barista" }
            };

            var result = resolver.Resolve(payload);

            Assert.That(result.avatarKey, Is.EqualTo("furniture"));
            Assert.That(result.preset.prefab, Is.SameAs(furniturePrefab));
            Assert.That(result.fallbackLevel, Is.EqualTo("fixed_scenario"));
        }

        [Test]
        public void Resolve_UnmappedScenarioUsesCatalogDefault()
        {
            var result = resolver.Resolve(new SpringScenePayload { taskType = "unknown_scenario" });

            Assert.That(result.avatarKey, Is.EqualTo("restaurant"));
            Assert.That(result.fallbackLevel, Is.EqualTo("global"));
            Assert.That(result.fallbackReason, Does.Contain("unknown_scenario"));
        }

        [TestCase("hotel_check_in", "barista_humanoid_v1")]
        [TestCase("furniture_shopping", "teacher_humanoid_v1")]
        [TestCase("gym_membership", "barista_male_humanoid_v1")]
        [TestCase("tourist_assistance", "teacher_female_humanoid_v1")]
        public void ProductionCatalog_MapsFixedScenarioToRequestedPrefab(string scenarioId, string expectedKey)
        {
            var productionCatalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(
                "Assets/SceneTalkVR/Avatar/Catalogs/AvatarCatalog.asset");

            Assert.That(productionCatalog, Is.Not.Null);
            Assert.That(productionCatalog.presets, Has.Length.EqualTo(4));
            var entry = productionCatalog.FindByScenarioId(scenarioId);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.key, Is.EqualTo(expectedKey));
            Assert.That(entry.prefab, Is.Not.Null);
            Assert.That(entry.prefab.name, Is.EqualTo(expectedKey));
        }

        private static AvatarPresetEntry CreateEntry(string key, string scenarioId, GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = key,
                prefab = prefab,
                scenarioIds = new[] { scenarioId }
            };
        }
    }
}
