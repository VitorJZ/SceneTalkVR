using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Demo;
using SceneTalkVR.Runtime;
using SceneTalkVR.Runtime.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class ExperimentConditionRuntimeSwitchTests
    {
        private GameObject host;
        private ExperimentConditionManager manager;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject(nameof(ExperimentConditionRuntimeSwitchTests));
            manager = host.AddComponent<ExperimentConditionManager>();
            SetBoolean(manager, "enableLogging", false);
            manager.RefreshCondition(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(
            ExperimentConditionManager.DialogueAvatarProvider,
            ExperimentConditionManager.ExplicitStyle,
            "dialogue_avatar_explicit")]
        [TestCase(
            ExperimentConditionManager.DialogueAvatarProvider,
            ExperimentConditionManager.RecastStyle,
            "dialogue_avatar_recast")]
        [TestCase(
            ExperimentConditionManager.AssistantAgentProvider,
            ExperimentConditionManager.ExplicitStyle,
            "assistant_agent_explicit")]
        [TestCase(
            ExperimentConditionManager.AssistantAgentProvider,
            ExperimentConditionManager.RecastStyle,
            "assistant_agent_recast")]
        public void ManualAxesResolveToExpectedCondition(
            string provider,
            string style,
            string expectedConditionId)
        {
            Assert.That(manager.TrySetManualFeedbackProvider(provider), Is.True);
            Assert.That(manager.TrySetManualFeedbackStyle(style), Is.True);

            Assert.That(manager.CurrentConditionId, Is.EqualTo(expectedConditionId));
            Assert.That(manager.CurrentFeedbackProvider, Is.EqualTo(provider));
            Assert.That(manager.CurrentFeedbackStyle, Is.EqualTo(style));
        }

        [Test]
        public void ChangingOneAxisPreservesTheOtherAxis()
        {
            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.DialogueAvatarProvider),
                Is.True);
            Assert.That(
                manager.TrySetManualFeedbackStyle(ExperimentConditionManager.RecastStyle),
                Is.True);

            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.AssistantAgentProvider),
                Is.True);
            Assert.That(manager.CurrentFeedbackStyle, Is.EqualTo(ExperimentConditionManager.RecastStyle));
            Assert.That(manager.CurrentConditionId, Is.EqualTo("assistant_agent_recast"));

            Assert.That(
                manager.TrySetManualFeedbackStyle(ExperimentConditionManager.ExplicitStyle),
                Is.True);
            Assert.That(
                manager.CurrentFeedbackProvider,
                Is.EqualTo(ExperimentConditionManager.AssistantAgentProvider));
            Assert.That(manager.CurrentConditionId, Is.EqualTo("assistant_agent_explicit"));
        }

        [TestCase(
            ExperimentConditionManager.DialogueAvatarProvider,
            ExperimentConditionManager.ExplicitStyle)]
        [TestCase(
            ExperimentConditionManager.DialogueAvatarProvider,
            ExperimentConditionManager.RecastStyle)]
        [TestCase(
            ExperimentConditionManager.AssistantAgentProvider,
            ExperimentConditionManager.ExplicitStyle)]
        [TestCase(
            ExperimentConditionManager.AssistantAgentProvider,
            ExperimentConditionManager.RecastStyle)]
        public void DemoBrainPayloadUsesTheSelectedAxes(string provider, string style)
        {
            var demoBrain = host.AddComponent<DemoBrainModule>();
            SetFloat(demoBrain, "simulatedProcessingSeconds", 0f);
            Assert.That(manager.TrySetManualFeedbackProvider(provider), Is.True);
            Assert.That(manager.TrySetManualFeedbackStyle(style), Is.True);
            manager.InjectInto(demoBrain);

            SpringScenePayload payload = null;
            string error = null;
            var routine = demoBrain.GenerateSceneAndReply(
                "I very like this topic.",
                value => payload = value,
                value => error = value);
            while (routine.MoveNext())
            {
            }

            Assert.That(error, Is.Null);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.correctionFeedback, Is.Not.Null);
            Assert.That(payload.correctionFeedback.hasFeedback, Is.True);
            Assert.That(payload.correctionFeedback.provider, Is.EqualTo(provider));
            Assert.That(payload.correctionFeedback.style, Is.EqualTo(style));
        }

        [TestCase("formalExperiment", "Locked by formal experiment.")]
        [TestCase("useConditionOrder", "Locked by condition order.")]
        public void ExperimentControlledModesRejectManualChanges(
            string serializedFlag,
            string expectedReason)
        {
            SetBoolean(manager, serializedFlag, true);
            var originalConditionId = manager.CurrentConditionId;

            Assert.That(manager.CanUseManualRuntimeCondition, Is.False);
            Assert.That(manager.ManualRuntimeConditionLockReason, Is.EqualTo(expectedReason));
            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.DialogueAvatarProvider),
                Is.False);
            Assert.That(manager.CurrentConditionId, Is.EqualTo(originalConditionId));
        }

        [Test]
        public void ActiveAndPendingTurnsRejectManualChanges()
        {
            var originalConditionId = manager.CurrentConditionId;
            manager.BeginTurn();

            Assert.That(manager.CanUseManualRuntimeCondition, Is.False);
            Assert.That(
                manager.TrySetManualFeedbackStyle(ExperimentConditionManager.RecastStyle),
                Is.False);
            Assert.That(manager.CurrentConditionId, Is.EqualTo(originalConditionId));

            manager.CompleteActiveTurn();
            Assert.That(manager.HasPendingTurnReview, Is.True);
            Assert.That(manager.CanUseManualRuntimeCondition, Is.False);
            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.DialogueAvatarProvider),
                Is.False);
            Assert.That(manager.CurrentConditionId, Is.EqualTo(originalConditionId));
        }

        [Test]
        public void OrchestratorReinjectsChangedAxesIntoGenerationAndPresentationModules()
        {
            var llm = host.AddComponent<RealLLMService>();
            var avatarVoice = host.AddComponent<AvatarPresentationVoiceModule>();
            var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
            orchestrator.ConfigureModules(brain: llm, avatarVoice: avatarVoice);
            orchestrator.OpenSettings();

            var initialProvider = manager.CurrentFeedbackProvider;
            var initialStyle = manager.CurrentFeedbackStyle;
            orchestrator.ChangeCorrectionProviderSetting();
            orchestrator.ChangeCorrectionStyleSetting();

            var expectedProvider = initialProvider == ExperimentConditionManager.DialogueAvatarProvider
                ? ExperimentConditionManager.AssistantAgentProvider
                : ExperimentConditionManager.DialogueAvatarProvider;
            var expectedStyle = initialStyle == ExperimentConditionManager.ExplicitStyle
                ? ExperimentConditionManager.RecastStyle
                : ExperimentConditionManager.ExplicitStyle;
            var correctionPresenter = host.GetComponent<CorrectionFeedbackPresenter>();

            Assert.That(llm.CurrentCondition, Is.Not.Null);
            Assert.That(llm.CurrentCondition.provider, Is.EqualTo(expectedProvider));
            Assert.That(llm.CurrentCondition.style, Is.EqualTo(expectedStyle));
            Assert.That(correctionPresenter, Is.Not.Null);
            Assert.That(correctionPresenter.CurrentFeedbackProvider, Is.EqualTo(expectedProvider));

            var nextTurn = manager.BeginTurn();
            Assert.That(nextTurn.provider, Is.EqualTo(expectedProvider));
            Assert.That(nextTurn.style, Is.EqualTo(expectedStyle));
        }

        [Test]
        public void SettingsUiBuildsIndependentSourceAndStyleButtons()
        {
            var canvasObject = new GameObject("Settings Test Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var flowUi = host.AddComponent<SceneTalkFlowUiController>();
                flowUi.Configure(orchestrator, canvasObject.GetComponent<Canvas>(), null);
                orchestrator.OpenSettings();

                var sourceButton = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionSourceChangeButton")
                    ?.GetComponent<Button>();
                var styleButton = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionStyleChangeButton")
                    ?.GetComponent<Button>();

                Assert.That(sourceButton, Is.Not.Null);
                Assert.That(styleButton, Is.Not.Null);
                Assert.That(sourceButton.interactable, Is.True);
                Assert.That(styleButton.interactable, Is.True);

                var initialProvider = manager.CurrentFeedbackProvider;
                var initialStyle = manager.CurrentFeedbackStyle;
                sourceButton.onClick.Invoke();
                Assert.That(manager.CurrentFeedbackProvider, Is.Not.EqualTo(initialProvider));
                Assert.That(manager.CurrentFeedbackStyle, Is.EqualTo(initialStyle));

                styleButton.onClick.Invoke();
                Assert.That(manager.CurrentFeedbackStyle, Is.Not.EqualTo(initialStyle));
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void SettingsUiDisablesCorrectionButtonsWhenConditionOrderOwnsTheMode()
        {
            SetBoolean(manager, "useConditionOrder", true);
            var canvasObject = new GameObject("Locked Settings Test Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var flowUi = host.AddComponent<SceneTalkFlowUiController>();
                flowUi.Configure(orchestrator, canvasObject.GetComponent<Canvas>(), null);
                orchestrator.OpenSettings();

                var sourceButton = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionSourceChangeButton")
                    ?.GetComponent<Button>();
                var styleButton = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionStyleChangeButton")
                    ?.GetComponent<Button>();
                var statusText = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionSettingsStatus")
                    ?.GetComponent<Text>();

                Assert.That(sourceButton, Is.Not.Null);
                Assert.That(styleButton, Is.Not.Null);
                Assert.That(sourceButton.interactable, Is.False);
                Assert.That(styleButton.interactable, Is.False);
                Assert.That(sourceButton.GetComponentInChildren<Text>().text, Is.EqualTo("Locked"));
                Assert.That(styleButton.GetComponentInChildren<Text>().text, Is.EqualTo("Locked"));
                Assert.That(statusText, Is.Not.Null);
                Assert.That(statusText.text, Is.EqualTo("Locked by condition order."));
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static void SetBoolean(Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
