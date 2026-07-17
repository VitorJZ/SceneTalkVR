using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage2TaskCatalogPlayModeTests
    {
        private static readonly string[] FormalTaskIds =
        {
            "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance"
        };

        [UnityTest]
        public IEnumerator DeveloperMode_MainMenuAndFourCatalogTasksStartOffline()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, "SampleScene", StringComparison.Ordinal))
            {
                SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            }

            yield return null;
            yield return null;

            var managerType = Type.GetType("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            Assert.That(managerType, Is.Not.Null);
            var manager = FindSceneComponent(managerType);
            Assert.That(manager, Is.Not.Null, "ExperimentConditionManager must exist in SampleScene.");

            var isFormal = (bool)managerType.GetProperty("IsFormalExperiment")!.GetValue(manager);
            Assert.That(isFormal, Is.False, "Stage 2 validation uses Developer Mode; Formal decisions remain blocked.");

            var catalog = managerType.GetProperty("TaskCatalog")!.GetValue(manager);
            Assert.That(catalog, Is.Not.Null, "Experiment Task Catalog must be bound.");

            var loadAssignedTask = managerType.GetMethod("LoadAssignedTask", BindingFlags.Instance | BindingFlags.Public);
            var currentTaskProperty = managerType.GetProperty("CurrentTask", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(loadAssignedTask, Is.Not.Null);
            Assert.That(currentTaskProperty, Is.Not.Null);

            foreach (var taskId in FormalTaskIds)
            {
                var arguments = new object[] { taskId, null };
                Assert.That((bool)loadAssignedTask!.Invoke(manager, arguments), Is.True, arguments[1] as string);
                var task = currentTaskProperty!.GetValue(manager);
                Assert.That(task, Is.Not.Null, taskId);
                var taskType = task.GetType();
                Assert.That((string)taskType.GetField("taskId")!.GetValue(task), Is.EqualTo(taskId));
                Assert.That((string)taskType.GetField("initialQuestion")!.GetValue(task), Is.Not.Empty);
                Assert.That((Array)taskType.GetField("fallbackLayoutObjects")!.GetValue(task), Is.Empty);
                var panoramaKey = (string)taskType.GetField("panoramaResourceKey")!.GetValue(task);
                Assert.That(Resources.Load<Texture2D>(panoramaKey), Is.Not.Null, taskId);
                yield return null;
            }

            var uiType = Type.GetType("SceneTalkVR.Runtime.SceneTalkFlowUiController, Assembly-CSharp");
            Assert.That(uiType, Is.Not.Null);
            var ui = FindSceneComponent(uiType);
            Assert.That(ui, Is.Not.Null, "Runtime task-selection UI must be present.");
            var taskOptions = uiType.GetProperty("CurrentTaskOptions")!.GetValue(ui) as IEnumerable;
            Assert.That(taskOptions, Is.Not.Null);
            Assert.That(taskOptions!.Cast<object>().Count(), Is.EqualTo(4));

            var initialPanel = GameObject.Find("InitialPanel");
            Assert.That(initialPanel, Is.Not.Null, "Developer Mode main menu must be created.");
        }

        private static Component FindSceneComponent(Type componentType)
        {
            return Resources.FindObjectsOfTypeAll(componentType)
                .OfType<Component>()
                .FirstOrDefault(component => component.gameObject.scene.IsValid());
        }
    }
}
