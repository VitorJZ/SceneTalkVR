using System.Collections;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage5QuestionnairePlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_HasBoundQuestionnaireRuntimeAndCatalog()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene") SceneManager.LoadScene("SampleScene");
            yield return null;
            var managerType = Type.GetType("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            var controllerType = Type.GetType("SceneTalkVR.Runtime.QuestionnaireRuntimeController, Assembly-CSharp");
            var panelType = Type.GetType("SceneTalkVR.Runtime.QuestionnaireVrPanel, Assembly-CSharp");
            Assert.That(managerType, Is.Not.Null); Assert.That(controllerType, Is.Not.Null); Assert.That(panelType, Is.Not.Null);
            var managers = Resources.FindObjectsOfTypeAll(managerType); Assert.That(managers.Length, Is.EqualTo(1));
            var manager = (Component)managers[0];
            Assert.That(managerType.GetProperty("QuestionnaireCatalog").GetValue(manager), Is.Not.Null);
            Assert.That(manager.GetComponent(controllerType), Is.Not.Null); Assert.That(manager.GetComponent(panelType), Is.Not.Null);
        }
    }
}
