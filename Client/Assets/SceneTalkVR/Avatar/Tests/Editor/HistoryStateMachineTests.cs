using System;
using System.IO;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Runtime;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests.Editor
{
    public sealed class HistoryStateMachineTests
    {
        private GameObject host;
        private string databasePath;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), $"scenetalk-state-{Guid.NewGuid():N}.sqlite3");
            host = new GameObject("History State Test Host");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                host.GetComponent<LearningMemoryService>()?.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
            }

            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }

        [Test]
        public void OrchestratorOwnsHistoryListDetailAndDeleteStates()
        {
            var manager = host.AddComponent<ExperimentConditionManager>();
            var memory = host.AddComponent<LearningMemoryService>();
            var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
            memory.ConfigureStoreForTests(new SqliteLearningMemoryStore(databasePath));

            var payload = CreatePayload();
            var settings = CreateSettings("history-session");
            memory.BeginSession(
                "history-session",
                payload,
                settings,
                "Restaurant Reservation",
                payload.dialogueReply);
            memory.EndActiveSession();

            orchestrator.OpenHistory();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.HistoryList));
            Assert.That(orchestrator.CurrentHistoryPage.totalCount, Is.EqualTo(1));

            orchestrator.SelectHistorySession("history-session");
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.HistoryDetail));
            Assert.That(orchestrator.SelectedHistorySession.summary.title, Is.EqualTo("Restaurant Reservation"));

            orchestrator.RequestDeleteSelectedHistory();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.HistoryDeleteConfirm));
            orchestrator.CancelDeleteSelectedHistory();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.HistoryDetail));

            orchestrator.RequestDeleteSelectedHistory();
            orchestrator.ConfirmDeleteSelectedHistory();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.HistoryList));
            Assert.That(orchestrator.CurrentHistoryPage.totalCount, Is.Zero);
            Assert.That(memory.GetSession("history-session"), Is.Null);
            Assert.That(manager.IsFormalExperiment, Is.False);
        }

        [Test]
        public void HistoryUiContainsPagedListDetailAndConfirmationControls()
        {
            host.AddComponent<ExperimentConditionManager>();
            var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
            var canvasObject = new GameObject("History Test Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var flowUi = host.AddComponent<SceneTalkFlowUiController>();
                flowUi.Configure(orchestrator, canvasObject.GetComponent<Canvas>(), null);

                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/InitialPanel/HistoryButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryListPanel/PreviousButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryListPanel/NextButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryListPanel/BackButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryDetailPanel/ConversationViewport"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryDetailPanel/ContinueButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryDetailPanel/BackButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryDeletePanel/ConfirmDeleteButton"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("SceneTalkVR Flow UI/HistoryErrorPanel/BackButton"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void FormalExperimentDisablesHistoryEntry()
        {
            var manager = host.AddComponent<ExperimentConditionManager>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("formalExperiment").boolValue = true;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            manager.RefreshCondition(false);

            var memory = host.AddComponent<LearningMemoryService>();
            var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
            var storeField = typeof(LearningMemoryService).GetField(
                "store",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            orchestrator.OpenHistory();

            Assert.That(orchestrator.IsHistoryAvailable, Is.False);
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Idle));
            Assert.That(memory, Is.Not.Null);
            Assert.That(storeField, Is.Not.Null);
            Assert.That(storeField.GetValue(memory), Is.Null);
        }

        private static SpringScenePayload CreatePayload()
        {
            return new SpringScenePayload
            {
                taskType = "restaurant_reservation",
                environmentType = "restaurant",
                dialogueReply = "Good evening. What time would you like to reserve?",
                avatarRole = new AvatarRoleData
                {
                    role = "barista",
                    appearance = new AvatarAppearanceData { genderPresentation = "female" }
                },
                scene = new ScenePayload { mode = "skybox", skyboxUrl = "demo://restaurant-360" },
                correctionFeedback = new CorrectionFeedbackData
                {
                    provider = "assistant_agent",
                    style = "explicit"
                }
            };
        }

        private static ConversationSettingsSnapshot CreateSettings(string sessionId)
        {
            return new ConversationSettingsSnapshot
            {
                brainMode = "DirectRealLlm",
                feedbackSensitivity = "moderate",
                condition = new CorrectionExperimentCondition
                {
                    participantId = "test",
                    sessionId = sessionId,
                    scenarioId = "restaurant_reservation",
                    conditionId = "assistant_agent_explicit",
                    provider = "assistant_agent",
                    style = "explicit",
                    task = new SceneTalkExperimentTask { scenarioId = "restaurant_reservation" }
                }
            };
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
