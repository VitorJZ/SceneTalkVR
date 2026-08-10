using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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

        [UnityTest]
        public IEnumerator EnglishLanguage_RendersFormalQuestionnaireAndRankingWithoutSystemChinese()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene") SceneManager.LoadScene("SampleScene");
            yield return null;

            var managerType = RuntimeType("SceneTalkVR.Core.ExperimentConditionManager");
            var controllerType = RuntimeType("SceneTalkVR.Runtime.QuestionnaireRuntimeController");
            var sessionServiceType = RuntimeType("SceneTalkVR.Core.QuestionnaireSessionService");
            var sessionType = RuntimeType("SceneTalkVR.Core.QuestionnaireSession");
            var conditionCodeType = RuntimeType("SceneTalkVR.Core.FormalConditionCode");
            var conditionStatusType = RuntimeType("SceneTalkVR.Core.ConditionRunStatus");
            var validityType = RuntimeType("SceneTalkVR.Core.ExperimentTechnicalValidity");
            var rankingPanelType = RuntimeType("SceneTalkVR.Runtime.FormalRankingVrPanel");
            var manager = (Component)Resources.FindObjectsOfTypeAll(managerType)
                .Single(x => ((Component)x).gameObject.scene.IsValid());
            var controller = manager.GetComponent(controllerType);
            var service = Get(controller, "Service");
            var linkageKey = "ql-language-ui-" + Guid.NewGuid().ToString("N");
            var defaultFolder = (string)sessionServiceType
                .GetProperty("DefaultFolder", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            var draftPath = (string)sessionServiceType
                .GetMethod("ResolveDraftPath", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { defaultFolder, linkageKey });

            try
            {
                SetLanguage("English");
                yield return null;
                yield return null;

                var protocol = Get(manager, "ExperimentProtocol");
                var catalog = Get(manager, "QuestionnaireCatalog");
                var context = Activator.CreateInstance(sessionType);
                SetFields(context, new Dictionary<string, object>
                {
                    ["participantId"] = "language-ui-participant",
                    ["sessionId"] = "language-ui-session",
                    ["sequenceId"] = "language-ui-sequence",
                    ["conditionRunId"] = "language-ui-run",
                    ["questionnaireLinkageKey"] = linkageKey,
                    ["conditionPosition"] = 0,
                    ["formalCondition"] = Enum.Parse(conditionCodeType, "NE"),
                    ["conditionStatus"] = Enum.Parse(conditionStatusType, "QuestionnaireInProgress"),
                    ["taskId"] = "hotel_check_in",
                    ["taskAssignmentId"] = "language-ui-task",
                    ["technicalValidity"] = Enum.Parse(validityType, "Valid"),
                    ["protocolVersion"] = Get(protocol, "ProtocolVersion")
                });
                var definition = catalog.GetType().GetMethod("Find")
                    ?.Invoke(catalog, new object[] { "formal_condition_v1" });
                var beginArguments = new[] { definition, context, null };
                var began = (bool)service.GetType().GetMethod("Begin")
                    ?.Invoke(service, beginArguments);
                Assert.That(began, Is.True, beginArguments[2] as string);
                yield return null;

                var questionnaire = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Single(x => x.scene.IsValid() && x.name == "QuestionnairePanel");
                var questionnaireTexts = questionnaire.GetComponentsInChildren<TMP_Text>(true)
                    .Select(x => x.text).ToArray();
                var enabledItems = (IEnumerable)catalog.GetType().GetMethod("GetEnabledItems")
                    ?.Invoke(catalog, new[] { "formal_condition_v1", protocol });
                var firstItem = enabledItems.Cast<object>().First();
                var firstPrompt = (string)firstItem.GetType().GetField("promptEnglish")?.GetValue(firstItem);
                Assert.That(questionnaireTexts, Does.Contain(firstPrompt));
                AssertNoChinese(questionnaire, "formal questionnaire");

                service.GetType().GetMethod("Reset")?.Invoke(service, null);
                var rankingPanel = manager.GetComponent(rankingPanelType);
                rankingPanelType
                    .GetMethod("EnsureBuilt", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(rankingPanel, null);
                yield return null;

                var ranking = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Single(x => x.scene.IsValid() && x.name == "FormalFinalRankingPanel");
                Assert.That(
                    ranking.GetComponentsInChildren<TMP_Text>(true).Select(x => x.text),
                    Does.Contain("Final ranking"));
                AssertNoChinese(ranking, "formal ranking");
            }
            finally
            {
                service?.GetType().GetMethod("Reset")?.Invoke(service, null);
                SetLanguage("Chinese");
                if (!string.IsNullOrWhiteSpace(draftPath) && File.Exists(draftPath)) File.Delete(draftPath);
            }
        }

        private static Type RuntimeType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static object Get(object target, string memberName)
        {
            Assert.That(target, Is.Not.Null, memberName);
            var type = target.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) return property.GetValue(target);
            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, type.FullName + "." + memberName);
            return field.GetValue(target);
        }

        private static void SetFields(object target, IReadOnlyDictionary<string, object> values)
        {
            foreach (var pair in values)
            {
                var field = target.GetType().GetField(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + pair.Key);
                field.SetValue(target, pair.Value);
            }
        }

        private static void SetLanguage(string languageName)
        {
            var storeType = RuntimeType("SceneTalkVR.Core.SceneTalkUserSettingsStore");
            var languageType = RuntimeType("SceneTalkVR.Core.SceneTalkLanguage");
            storeType.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new[] { Enum.Parse(languageType, languageName) });
        }

        private static void AssertNoChinese(GameObject root, string context)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(text.text, Does.Not.Match("[\\u3400-\\u9fff]"), context + ": " + text.name);
                Assert.That(text.text, Is.Not.EqualTo("Translation unavailable"), context + ": " + text.name);
            }
        }
    }
}
