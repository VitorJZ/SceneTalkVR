using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Demo;
using SceneTalkVR.Runtime;
using SceneTalkVR.Runtime.Services;
using TMPro;
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
            Assert.That(
                manager.CurrentAssistantEmbodiment,
                Is.EqualTo(provider == ExperimentConditionManager.AssistantAgentProvider
                    ? ExperimentConditionManager.OrbAssistantEmbodiment
                    : ExperimentConditionManager.NoAssistantEmbodiment));
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

        [Test]
        public void AssistantEmbodimentIsUnavailableForDialogueAndRestoredForAssistant()
        {
            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.AssistantAgentProvider),
                Is.True);
            Assert.That(
                manager.TrySetManualAssistantEmbodiment(
                    ExperimentConditionManager.AudioOnlyAssistantEmbodiment),
                Is.True);
            Assert.That(
                manager.CurrentAssistantEmbodiment,
                Is.EqualTo(ExperimentConditionManager.AudioOnlyAssistantEmbodiment));

            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.DialogueAvatarProvider),
                Is.True);
            Assert.That(
                manager.CurrentAssistantEmbodiment,
                Is.EqualTo(ExperimentConditionManager.NoAssistantEmbodiment));
            Assert.That(
                manager.ConfiguredAssistantEmbodiment,
                Is.EqualTo(ExperimentConditionManager.AudioOnlyAssistantEmbodiment));
            Assert.That(
                manager.TrySetManualAssistantEmbodiment(
                    ExperimentConditionManager.HumanoidAssistantEmbodiment),
                Is.False);

            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.AssistantAgentProvider),
                Is.True);
            Assert.That(
                manager.CurrentAssistantEmbodiment,
                Is.EqualTo(ExperimentConditionManager.AudioOnlyAssistantEmbodiment));
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
        public void ExitingFormalCollectionModeUnlocksManualRuntimeChangesAndReentryRelocks()
        {
            manager.EnterEditorCollectionMode(null, null, null, null, null);
            Assert.That(manager.IsFormalExperiment, Is.True);
            Assert.That(manager.CanUseManualRuntimeCondition, Is.False);

            manager.ExitEditorCollectionMode();
            Assert.That(manager.IsFormalExperiment, Is.False);
            Assert.That(manager.CanUseManualRuntimeCondition, Is.True);
            Assert.That(
                manager.TrySetManualFeedbackStyle(ExperimentConditionManager.RecastStyle),
                Is.True);

            manager.EnterEditorCollectionMode(null, null, null, null, null);
            Assert.That(manager.IsFormalExperiment, Is.True);
            Assert.That(manager.CanUseManualRuntimeCondition, Is.False);
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
        public void ManualChangeAfterHistoryRestoreClearsTheRestoredConditionOverride()
        {
            var restored = new CorrectionExperimentCondition
            {
                participantId = "test",
                sessionId = "restored-session",
                scenarioId = "restaurant_reservation",
                conditionId = "assistant_agent_recast",
                provider = ExperimentConditionManager.AssistantAgentProvider,
                style = ExperimentConditionManager.RecastStyle,
                task = manager.CurrentTask
            };

            Assert.That(manager.RestoreConversation(restored, 2), Is.True);
            Assert.That(manager.CurrentConditionId, Is.EqualTo("assistant_agent_recast"));
            Assert.That(
                manager.TrySetManualFeedbackProvider(ExperimentConditionManager.DialogueAvatarProvider),
                Is.True);
            Assert.That(manager.CurrentConditionId, Is.EqualTo("dialogue_avatar_recast"));
            Assert.That(manager.CurrentFeedbackProvider, Is.EqualTo(ExperimentConditionManager.DialogueAvatarProvider));
            Assert.That(manager.CurrentFeedbackStyle, Is.EqualTo(ExperimentConditionManager.RecastStyle));
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
            Assert.That(
                correctionPresenter.CurrentAssistantEmbodiment,
                Is.EqualTo(ExperimentConditionManager.OrbAssistantEmbodiment));

            var nextTurn = manager.BeginTurn();
            Assert.That(nextTurn.provider, Is.EqualTo(expectedProvider));
            Assert.That(nextTurn.style, Is.EqualTo(expectedStyle));
        }

        [Test]
        public void SettingsUiBuildsIndependentSourceStyleAndAppearanceButtons()
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
                var appearanceButton = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionAppearanceChangeButton")
                    ?.GetComponent<Button>();
                var appearanceValue = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionAppearanceValue")
                    ?.GetComponent<TMP_Text>();

                Assert.That(sourceButton, Is.Not.Null);
                Assert.That(styleButton, Is.Not.Null);
                Assert.That(appearanceButton, Is.Not.Null);
                Assert.That(appearanceValue, Is.Not.Null);
                Assert.That(sourceButton.interactable, Is.True);
                Assert.That(styleButton.interactable, Is.True);
                Assert.That(appearanceButton.interactable, Is.True);
                Assert.That(appearanceValue.text, Is.EqualTo("悬浮球"));

                var initialProvider = manager.CurrentFeedbackProvider;
                var initialStyle = manager.CurrentFeedbackStyle;
                appearanceButton.onClick.Invoke();
                Assert.That(
                    manager.CurrentAssistantEmbodiment,
                    Is.EqualTo(ExperimentConditionManager.HumanoidAssistantEmbodiment));

                sourceButton.onClick.Invoke();
                Assert.That(manager.CurrentFeedbackProvider, Is.Not.EqualTo(initialProvider));
                Assert.That(manager.CurrentFeedbackStyle, Is.EqualTo(initialStyle));

                typeof(SceneTalkFlowUiController)
                    .GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(flowUi, null);
                Assert.That(appearanceButton.interactable, Is.False);
                Assert.That(appearanceButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("不适用"));
                Assert.That(appearanceValue.text, Is.EqualTo("不适用"));

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
                var appearanceButton = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionAppearanceChangeButton")
                    ?.GetComponent<Button>();
                var statusText = canvasObject.transform.Find(
                    "SceneTalkVR Flow UI/SettingsPanel/GeneralSettings/CorrectionSettingsStatus")
                    ?.GetComponent<TMP_Text>();

                Assert.That(sourceButton, Is.Not.Null);
                Assert.That(styleButton, Is.Not.Null);
                Assert.That(appearanceButton, Is.Not.Null);
                Assert.That(sourceButton.interactable, Is.False);
                Assert.That(styleButton.interactable, Is.False);
                Assert.That(appearanceButton.interactable, Is.False);
                Assert.That(sourceButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("已锁定"));
                Assert.That(styleButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("已锁定"));
                Assert.That(appearanceButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("已锁定"));
                Assert.That(statusText, Is.Not.Null);
                Assert.That(statusText.text, Is.EqualTo("已由实验条件顺序锁定。"));
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void AssistantAppearanceSettingPersistsAcrossStoreReload()
        {
            var cache = typeof(SceneTalkUserSettingsStore).GetField(
                "cachedSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cache, Is.Not.Null);

            try
            {
                SceneTalkUserSettingsStore.ResetAll();
                SceneTalkUserSettingsStore.SetAssistantEmbodiment(
                    ExperimentConditionManager.HumanoidAssistantEmbodiment);
                cache.SetValue(null, null);

                Assert.That(
                    SceneTalkUserSettingsStore.Current.assistantEmbodiment,
                    Is.EqualTo(ExperimentConditionManager.HumanoidAssistantEmbodiment));
            }
            finally
            {
                SceneTalkUserSettingsStore.ResetAll();
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
