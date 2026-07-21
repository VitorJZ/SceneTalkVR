using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class ExperimentTaskCatalogTests
    {
        private const string Path = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset";
        private static readonly string[] FormalIds =
        {
            "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance"
        };

        private ExperimentTaskCatalog Catalog => AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(Path);

        [Test] public void FormalCatalog_HasExactlyFourAndThreeRestaurantsArePilot()
        {
            var catalog = Catalog;
            Assert.That(catalog,Is.Not.Null);
            Assert.That(catalog.Tasks.Count(t=>t.phase==ExperimentTaskPhase.Formal),Is.EqualTo(4));
            CollectionAssert.AreEquivalent(FormalIds, catalog.GetTasks(ExperimentTaskPhase.Formal).Select(t => t.taskId));
            CollectionAssert.AreEquivalent(new[] { "pilot_restaurant_walk_in", "pilot_restaurant_ordering", "pilot_restaurant_wrong_dish" },
                catalog.GetTasks(ExperimentTaskPhase.Pilot).Select(t => t.taskId));
            Assert.That(catalog.Find("restaurant_reservation"), Is.Null);
        }

        [Test] public void Catalog_TaskIdsAreUnique()
        {
            var ids = Catalog.Tasks.Select(task => task.taskId).ToArray();
            Assert.That(ids.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(ids.Length));
        }

        [TestCase("hotel_check_in",4)] [TestCase("furniture_shopping",4)] [TestCase("gym_membership",4)] [TestCase("tourist_assistance",4)]
        public void FormalTasks_HaveFourGoals(string id,int count)
        {
            var task = Catalog.Find(id);
            Assert.That(task.initialQuestion,Is.Not.Empty); Assert.That(task.goals.Length,Is.EqualTo(count));
        }

        [TestCase("hotel_check_in", "Provide the reservation name.|Ask whether breakfast is included.|Ask whether a high-floor room can be arranged.|Ask about the check-out time.")]
        [TestCase("furniture_shopping", "Describe the desk size needed.|Ask about available materials.|State or ask about the maximum budget.|Ask whether home delivery is available.")]
        [TestCase("gym_membership", "Explain a fitness goal.|Ask about the monthly membership price.|Ask about a suitable workout plan.|Ask whether a free trial is available.")]
        [TestCase("tourist_assistance", "Ask how to reach the city museum.|Ask whether a ticket is required.|Ask whether indoor photography is allowed.|Ask for another nearby attraction recommendation.")]
        public void FormalTasks_UseFrozenGoals(string id, string expected)
        {
            Assert.That(string.Join("|", Catalog.Find(id).goals.Select(goal => goal.text)), Is.EqualTo(expected));
        }

        [TestCase("hotel_check_in", "Good afternoon! Welcome to City Hotel. How can I help you today?")]
        [TestCase("furniture_shopping", "Hello! Is there anything in particular you're looking for today?")]
        [TestCase("gym_membership", "Hi! Welcome to Active Gym. How can I help you today?")]
        [TestCase("tourist_assistance", "Hello! Welcome to the tourist information center. How can I help you today?")]
        public void FormalTasks_UseFrozenInitialQuestion(string id, string expected)
        {
            Assert.That(Catalog.Find(id).initialQuestion, Is.EqualTo(expected));
        }

        [Test] public void FormalPanoramas_AreLocalAndLoadable()
        {
            foreach (var task in Catalog.GetTasks(ExperimentTaskPhase.Formal))
            {
                Assert.That(task.panoramaResourceKey, Does.StartWith("SceneTalkVR/Textures/"));
                Assert.That(Resources.Load<Texture2D>(task.panoramaResourceKey), Is.Not.Null, task.taskId);
            }
        }

        [Test] public void TouristAssistance_HasApprovedEditorCollectionAvatarPreset()
        {
            var task = Catalog.Find("tourist_assistance");
            Assert.That(task, Is.Not.Null);
            Assert.That(task.phase, Is.EqualTo(ExperimentTaskPhase.Formal));
            Assert.That(task.scenarioId, Is.EqualTo("tourist_assistance"));
            Assert.That(task.avatarRole, Is.EqualTo("tourist information officer"));
            Assert.That(task.voiceProfileKey, Is.Not.Empty);
            Assert.That(task.developerPlaceholderAvatar, Is.False);
            Assert.That(task.avatarPresetKey, Is.EqualTo("teacher_female_humanoid_v1"));
        }

        [Test] public void FormalValidation_AcceptsApprovedEditorCollectionTaskResources()
        {
            Assert.That(Catalog.ValidateFormal(null, out var error), Is.True, error);
            Assert.That(error, Is.Empty);
        }

        [Test] public void FormalValidation_FailsWhenLocalPanoramaIsMissing()
        {
            var temporary = ScriptableObject.CreateInstance<ExperimentTaskCatalog>();
            try
            {
                var definitions = Catalog.Tasks.Select(CloneDefinition).ToArray();
                definitions[0].panoramaResourceKey = "SceneTalkVR/Textures/does-not-exist";
                typeof(ExperimentTaskCatalog).GetField("tasks", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(temporary, definitions);
                Assert.That(temporary.ValidateFormal(null, out var error), Is.False);
                Assert.That(error, Does.Contain("hotel_check_in: local panorama missing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        [Test] public void FormalMode_RemotePanoramaAndHolodeckAreHardBlocked()
        {
            var go = new GameObject("Stage2 Formal Service Lock Test");
            try
            {
                var panorama = go.AddComponent<PanoramaSceneService>();
                panorama.ConfigureFormalModeLock(true);
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await panorama.GenerateSkyboxAsync("remote scene", "https://example.invalid/panorama.png"));

                var holodeck = go.AddComponent<HolodeckSceneService>();
                holodeck.ConfigureFormalModeLock(true);
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await holodeck.GenerateLayoutAsync("remote layout"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test] public void AvatarResolver_UsesExactTaskKeyAndRejectsUnavailableExactKey()
        {
            var avatarCatalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>("Assets/SceneTalkVR/Avatar/Catalogs/AvatarCatalog.asset");
            var go = new GameObject("Stage2 Avatar Resolver Test");
            try
            {
                var resolver = go.AddComponent<AvatarPresetResolver>();
                var serialized = new SerializedObject(resolver);
                serialized.FindProperty("catalog").objectReferenceValue = avatarCatalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var exact = resolver.Resolve(new SpringScenePayload
                {
                    taskType = "furniture_shopping",
                    avatarRole = new AvatarRoleData { presetKey = "barista_humanoid_v1" }
                });
                Assert.That(exact.avatarKey, Is.EqualTo("barista_humanoid_v1"));
                Assert.That(exact.fallbackLevel, Is.EqualTo("exact_task_key"));

                var missing = resolver.Resolve(new SpringScenePayload
                {
                    taskType = "furniture_shopping",
                    avatarRole = new AvatarRoleData { presetKey = "missing_formal_preset" }
                });
                Assert.That(missing.HasPreset, Is.False);
                Assert.That(missing.fallbackReason, Does.Contain("missing_formal_preset"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase("hotel_check_in")]
        [TestCase("furniture_shopping")]
        [TestCase("gym_membership")]
        [TestCase("tourist_assistance")]
        public void AssignedTaskPayload_UsesCatalogDefinition(string taskId)
        {
            var go = new GameObject("Stage2 Task Test");
            try
            {
                var manager = go.AddComponent<ExperimentConditionManager>();
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("taskCatalog").objectReferenceValue = Catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(manager.LoadAssignedTask(taskId, out var error), Is.True, error);
                var expected = Catalog.Find(taskId);
                Assert.That(manager.CurrentTask.taskId, Is.EqualTo(expected.taskId));
                Assert.That(manager.CurrentTask.initialQuestion, Is.EqualTo(expected.initialQuestion));
                Assert.That(manager.CurrentTask.panoramaResourceKey, Is.EqualTo(expected.panoramaResourceKey));
                Assert.That(manager.CurrentTask.avatarPresetKey, Is.EqualTo(expected.avatarPresetKey));
                Assert.That(manager.CurrentTask.fallbackLayoutObjects, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator DeveloperMode_FourAssignedTasksAreOfflineReady()
        {
            var go = new GameObject("Stage2 PlayMode Task Test");
            var manager = go.AddComponent<ExperimentConditionManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("taskCatalog").objectReferenceValue = Catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var taskId in FormalIds)
            {
                Assert.That(manager.LoadAssignedTask(taskId, out var error), Is.True, error);
                Assert.That(manager.CurrentTask.taskId, Is.EqualTo(taskId));
                Assert.That(Resources.Load<Texture2D>(manager.CurrentTask.panoramaResourceKey), Is.Not.Null, taskId);
                Assert.That(manager.CurrentTask.fallbackLayoutObjects, Is.Empty, taskId);
                yield return null;
            }

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static ExperimentTaskDefinition CloneDefinition(ExperimentTaskDefinition source)
        {
            return new ExperimentTaskDefinition
            {
                taskId = source.taskId,
                scenarioId = source.scenarioId,
                displayName = source.displayName,
                phase = source.phase,
                context = source.context,
                goals = source.goals.Select(goal => new ExperimentTaskGoal { text = goal.text }).ToArray(),
                initialQuestion = source.initialQuestion,
                environmentType = source.environmentType,
                panoramaResourceKey = source.panoramaResourceKey,
                avatarPresetKey = source.avatarPresetKey,
                avatarRole = source.avatarRole,
                voiceProfileKey = source.voiceProfileKey,
                roleplayPrompt = source.roleplayPrompt,
                spawnPosition = source.spawnPosition,
                spawnRotation = source.spawnRotation,
                developerPlaceholderAvatar = source.developerPlaceholderAvatar
            };
        }
    }
}
