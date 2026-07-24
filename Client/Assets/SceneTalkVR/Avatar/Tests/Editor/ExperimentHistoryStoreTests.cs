using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Runtime;
using SQLite;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests.Editor
{
    public sealed class ExperimentHistoryStoreTests
    {
        private string databasePath;
        private string ownedRoot;
        private string outsideRoot;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), $"scenetalk-experiments-{Guid.NewGuid():N}.sqlite3");
            ownedRoot = Path.Combine(Path.GetTempPath(), $"scenetalk-owned-{Guid.NewGuid():N}");
            outsideRoot = Path.Combine(Path.GetTempPath(), $"scenetalk-outside-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                host.GetComponent<LearningMemoryService>()?.Dispose();
                host.GetComponent<ExperimentHistoryService>()?.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
            }
            DeleteFile(databasePath);
            DeleteFile(databasePath + "-wal");
            DeleteFile(databasePath + "-shm");
            DeleteDirectory(ownedRoot);
            DeleteDirectory(outsideRoot);
        }

        [Test]
        public void VersionOneMigratesToVersionTwoAndPreservesLegacyConversation()
        {
            CreateVersionOneDatabase();
            using (var store = new SqliteLearningMemoryStore(databasePath))
            {
                store.Initialize();
                var legacy = store.GetSession("legacy-session");
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.summary.title, Is.EqualTo("Legacy conversation"));
                Assert.That(legacy.turns, Has.Length.EqualTo(1));
                Assert.That(legacy.turns[0].assistantText, Is.EqualTo("Legacy reply"));
                Assert.That(legacy.summary.IsExperimentConversation, Is.False);
                Assert.That(store.CountExperiments(), Is.Zero);
            }

            using var connection = new SQLiteConnection(databasePath);
            Assert.That(connection.ExecuteScalar<int>("PRAGMA user_version"), Is.EqualTo(2));
            foreach (var table in new[]
                     {
                         "experiment_records", "experiment_phases", "experiment_attempts",
                         "questionnaire_sessions", "questionnaire_responses", "questionnaire_scores", "experiment_rankings"
                     })
            {
                Assert.That(connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", table), Is.EqualTo(1), table);
            }
            var experimentIdColumn = connection.Query<TableColumn>("PRAGMA table_info(conversation_sessions)")
                .Single(item => item.name == "experiment_id");
            Assert.That(experimentIdColumn.notnull, Is.Zero);
        }

        [Test]
        public void PagingQuestionnaireSnapshotsAndCascadeDeleteRoundTrip()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            for (var i = 0; i < 6; i++) store.CreateExperiment(CreateExperiment("exp-" + i, 1000 + i));

            Assert.That(store.CountExperiments(), Is.EqualTo(6));
            Assert.That(store.ListExperiments(0, 5).Count, Is.EqualTo(5));
            Assert.That(store.ListExperiments(0, 1).Single().experimentId, Is.EqualTo("exp-5"));

            var attempt = new ExperimentAttemptRecord
            {
                attemptId = "attempt-1", experimentId = "exp-5", phase = ExperimentPhaseKind.Pilot,
                conditionKey = "voice_only", taskId = "pilot-task", runId = "run-1", attemptIndex = 1,
                status = ExperimentAttemptStatus.Completed, completionReason = "questionnaire_submitted",
                startedAtUnixMs = 2000, endedAtUnixMs = 3000
            };
            store.UpsertAttempt(attempt);
            store.UpsertQuestionnaire(new ExperimentQuestionnaireRecord
            {
                experimentId = "exp-5",
                phase = ExperimentPhaseKind.Pilot,
                attemptId = attempt.attemptId,
                prompts = new[]
                {
                    new QuestionnairePromptSnapshot
                    {
                        itemId = "q1", sectionId = "presence", promptEnglish = "I felt present.",
                        promptChinese = "我有临场感。", scaleMin = 1, scaleMax = 7
                    }
                },
                session = new QuestionnaireSession
                {
                    questionnaireId = "pilot_condition_v1", questionnaireLinkageKey = "ql-1",
                    completionStatus = QuestionnaireCompletionStatus.Submitted, completionRate = 1f,
                    responses = new[]
                    {
                        new QuestionnaireResponse
                        {
                            itemId = "q1", sectionId = "presence", rawValue = "6", scoredValue = 6f,
                            hasScoredValue = true
                        }
                    },
                    sectionScores = new[]
                    {
                        new QuestionnaireScoreResult
                        {
                            sectionId = "presence", mean = 6f, answeredCount = 1, itemCount = 1
                        }
                    }
                }
            });
            store.CreateSession(CreateConversation("conversation-1", "exp-5", attempt.attemptId));

            var restored = store.GetExperiment("exp-5");
            Assert.That(restored.attempts.Single().status, Is.EqualTo(ExperimentAttemptStatus.Completed));
            Assert.That(restored.questionnaires.Single().session.completionRate, Is.EqualTo(1f));
            Assert.That(restored.questionnaires.Single().session.responses.Single().rawValue, Is.EqualTo("6"));
            Assert.That(restored.questionnaires.Single().session.sectionScores.Single().mean, Is.EqualTo(6f));
            Assert.That(restored.questionnaires.Single().prompts.Single().promptChinese, Is.EqualTo("我有临场感。"));
            Assert.That(restored.questionnaires.Single().questionnaireRecordId, Is.Not.Empty);
            Assert.That(restored.conversations.Single().IsExperimentConversation, Is.True);

            Assert.That(store.DeleteExperiment("exp-5"), Is.True);
            Assert.That(store.GetExperiment("exp-5"), Is.Null);
            Assert.That(store.GetSession("conversation-1"), Is.Null);
            Assert.That(store.CountExperiments(), Is.EqualTo(5));
        }

        [Test]
        public void QuestionnaireRecordsWithSharedLinkageRemainDistinctAcrossAttempts()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            store.CreateExperiment(CreateExperiment("exp-questionnaire-attempts", 1000));

            foreach (var attemptId in new[] { "attempt-1", "attempt-2" })
            {
                store.UpsertQuestionnaire(new ExperimentQuestionnaireRecord
                {
                    experimentId = "exp-questionnaire-attempts",
                    phase = ExperimentPhaseKind.Formal,
                    attemptId = attemptId,
                    session = new QuestionnaireSession
                    {
                        questionnaireId = "formal_condition_v1",
                        questionnaireLinkageKey = "shared-linkage",
                        completionStatus = QuestionnaireCompletionStatus.InProgress
                    }
                });
            }

            var records = store.GetExperiment("exp-questionnaire-attempts").questionnaires;
            Assert.That(records, Has.Length.EqualTo(2));
            Assert.That(records.Select(item => item.questionnaireRecordId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(records.Select(item => item.attemptId), Is.EquivalentTo(new[] { "attempt-1", "attempt-2" }));
        }

        [Test]
        public void ExperimentConversationCannotBeDeletedIndividually()
        {
            using (var store = new SqliteLearningMemoryStore(databasePath))
            {
                store.Initialize();
                store.CreateExperiment(CreateExperiment("exp-protected", 1000));
                store.CreateSession(CreateConversation("protected-conversation", "exp-protected", "attempt"));
            }

            host = new GameObject("Experiment conversation permission test");
            var memory = host.AddComponent<LearningMemoryService>();
            memory.ConfigureStoreForTests(new SqliteLearningMemoryStore(databasePath), ownedRoot);
            var summary = memory.GetSession("protected-conversation").summary;
            Assert.That(summary.CanContinue, Is.False);
            Assert.That(summary.CanDelete, Is.False);
            Assert.Throws<InvalidOperationException>(() => memory.DeleteSession("protected-conversation"));
            Assert.That(memory.GetSession("protected-conversation"), Is.Not.Null);
        }

        [Test]
        public void DeleteRemovesOnlyOwnedDirectoriesWithinAllowedRoots()
        {
            var safeFolder = Path.Combine(ownedRoot, "exp-safe", "pilot");
            Directory.CreateDirectory(safeFolder);
            Directory.CreateDirectory(outsideRoot);
            var rootSentinel = Path.Combine(ownedRoot, "keep-root.txt");
            File.WriteAllText(Path.Combine(safeFolder, "raw.json"), "safe");
            File.WriteAllText(rootSentinel, "keep");
            File.WriteAllText(Path.Combine(outsideRoot, "external-copy.json"), "keep");

            var detail = CreateExperiment("exp-safe", 1000);
            detail.phases[0].dataRootPath = safeFolder;
            detail.phases[1].dataRootPath = outsideRoot;
            var broadRoot = CreateExperiment("exp-root-guard", 1001);
            broadRoot.phases[0].dataRootPath = ownedRoot;
            var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            store.CreateExperiment(detail);
            store.CreateExperiment(broadRoot);

            host = new GameObject("Experiment path safety test");
            var service = host.AddComponent<ExperimentHistoryService>();
            service.ConfigureStoreForTests(store, databasePath);
            Assert.That(service.DeleteExperiment("exp-safe", new[] { ownedRoot }), Is.True);
            Assert.That(service.DeleteExperiment("exp-root-guard", new[] { ownedRoot }), Is.True);
            Assert.That(Directory.Exists(safeFolder), Is.False);
            Assert.That(File.Exists(rootSentinel), Is.True);
            Assert.That(File.Exists(Path.Combine(outsideRoot, "external-copy.json")), Is.True);
        }

        [Test]
        public void PhaseGatesAndPilotEmbodimentMappingAreDeterministic()
        {
            host = new GameObject("Experiment gate test");
            var coordinator = host.AddComponent<ExperimentSessionCoordinator>();
            var detail = CreateExperiment("exp-gates", 1000);
            SetCurrentExperiment(coordinator, detail);

            Assert.That(coordinator.CanEnterPilot, Is.True);
            Assert.That(coordinator.CanEnterFormal, Is.False);
            detail.summary.pilotStatus = ExperimentPhaseStatus.Completed;
            Assert.That(coordinator.CanEnterPilot, Is.False);
            Assert.That(coordinator.CanEnterFormal, Is.False);
            detail.summary.preferredEmbodiment = "floating_orb";
            Assert.That(coordinator.CanEnterFormal, Is.True);
            detail.summary.formalStatus = ExperimentPhaseStatus.Completed;
            Assert.That(coordinator.CanEnterFormal, Is.False);

            Assert.That(ExperimentSessionCoordinator.ResolvePreferredAssistantEmbodiment("voice_only"),
                Is.EqualTo(ExperimentConditionManager.AudioOnlyAssistantEmbodiment));
            Assert.That(ExperimentSessionCoordinator.ResolvePreferredAssistantEmbodiment("floating_orb"),
                Is.EqualTo(ExperimentConditionManager.OrbAssistantEmbodiment));
            Assert.That(ExperimentSessionCoordinator.ResolvePreferredAssistantEmbodiment("humanoid_agent"),
                Is.EqualTo(ExperimentConditionManager.HumanoidAssistantEmbodiment));
            Assert.That(ExperimentSessionCoordinator.ResolvePreferredAssistantEmbodiment("unknown"), Is.Empty);
        }

        [Test]
        public void ExperimentMenuExitWarnsOnlyWhileExperimentIsIncomplete()
        {
            host = new GameObject("Experiment exit gate test");
            host.SetActive(false);
            var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
            var coordinator = host.AddComponent<ExperimentSessionCoordinator>();
            ConfigureNavigationOnly(coordinator, orchestrator);

            var detail = CreateExperiment("exp-exit-gate", 1000);
            SetCurrentExperiment(coordinator, detail);
            coordinator.RequestLeaveExperiment();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.ExperimentExitConfirm));

            coordinator.CancelLeaveExperiment();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.ExperimentMenu));
            detail.summary.pilotStatus = ExperimentPhaseStatus.Completed;
            detail.summary.formalStatus = ExperimentPhaseStatus.Completed;
            detail.summary.status = ExperimentRecordStatus.Completed;
            coordinator.RequestLeaveExperiment();

            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Idle));
            Assert.That(coordinator.CurrentExperiment, Is.Null);
        }

        private void CreateVersionOneDatabase()
        {
            using var connection = new SQLiteConnection(databasePath);
            connection.Execute(
                "CREATE TABLE conversation_sessions (session_id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL, "
                + "scenario_id TEXT NOT NULL, task_type TEXT NOT NULL, environment_type TEXT NOT NULL, "
                + "correction_provider TEXT NOT NULL, correction_style TEXT NOT NULL, created_at_unix_ms INTEGER NOT NULL, "
                + "updated_at_unix_ms INTEGER NOT NULL, turn_count INTEGER NOT NULL, correction_count INTEGER NOT NULL, "
                + "settings_json TEXT NOT NULL, scene_payload_json TEXT NOT NULL)");
            connection.Execute(
                "CREATE TABLE conversation_turns (id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, "
                + "sequence_index INTEGER NOT NULL, is_opening INTEGER NOT NULL, created_at_unix_ms INTEGER NOT NULL, "
                + "user_text TEXT NOT NULL, assistant_text TEXT NOT NULL, has_correction INTEGER NOT NULL, "
                + "error_type TEXT NOT NULL, payload_json TEXT NOT NULL)");
            connection.Execute(
                "INSERT INTO conversation_sessions VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                "legacy-session", "Legacy conversation", "legacy", "legacy", "legacy", "dialogue_avatar", "explicit",
                1000L, 1000L, 0, 0, "{}", "{}");
            connection.Execute(
                "INSERT INTO conversation_turns "
                + "(session_id, sequence_index, is_opening, created_at_unix_ms, user_text, assistant_text, has_correction, error_type, payload_json) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                "legacy-session", 0, 1, 1000L, string.Empty, "Legacy reply", 0, string.Empty, "{}");
            connection.Execute("PRAGMA user_version = 1");
        }

        private static ExperimentRecordDetail CreateExperiment(string id, long timestamp)
        {
            return new ExperimentRecordDetail
            {
                summary = new ExperimentRecordSummary
                {
                    experimentId = id, participantId = "P-" + id, status = ExperimentRecordStatus.InProgress,
                    pilotStatus = ExperimentPhaseStatus.NotStarted, formalStatus = ExperimentPhaseStatus.NotStarted,
                    createdAtUnixMs = timestamp, updatedAtUnixMs = timestamp
                },
                phases = new[]
                {
                    new ExperimentPhaseRecord
                    {
                        experimentId = id, phase = ExperimentPhaseKind.Pilot, sessionId = id + "-pilot",
                        status = ExperimentPhaseStatus.NotStarted, updatedAtUnixMs = timestamp
                    },
                    new ExperimentPhaseRecord
                    {
                        experimentId = id, phase = ExperimentPhaseKind.Formal, sessionId = id + "-formal",
                        status = ExperimentPhaseStatus.NotStarted, updatedAtUnixMs = timestamp
                    }
                }
            };
        }

        private static LearningSessionDetail CreateConversation(string sessionId, string experimentId, string attemptId)
        {
            return new LearningSessionDetail
            {
                summary = new LearningSessionSummary
                {
                    sessionId = sessionId, title = "Experiment dialogue", scenarioId = "task", taskType = "task",
                    environmentType = "restaurant", correctionProvider = "assistant_agent", correctionStyle = "explicit",
                    createdAtUnixMs = 1000, updatedAtUnixMs = 1000, experimentId = experimentId,
                    experimentPhase = ExperimentPhaseKind.Pilot.ToString(), experimentAttemptId = attemptId,
                    experimentRunId = "run-1"
                },
                settings = new ConversationSettingsSnapshot
                {
                    experimentId = experimentId, experimentPhase = ExperimentPhaseKind.Pilot.ToString(),
                    experimentAttemptId = attemptId, experimentRunId = "run-1"
                },
                sceneSnapshot = new SpringScenePayload { taskType = "task", dialogueReply = "Hello" },
                turns = new[]
                {
                    new DialogueTurnRecord
                    {
                        sequenceIndex = 0, isOpening = true, createdAtUnixMs = 1000,
                        assistantText = "Hello", payload = new SpringScenePayload { taskType = "task", dialogueReply = "Hello" }
                    }
                }
            };
        }

        private static void SetCurrentExperiment(ExperimentSessionCoordinator coordinator, ExperimentRecordDetail value)
        {
            typeof(ExperimentSessionCoordinator).GetProperty(
                    nameof(ExperimentSessionCoordinator.CurrentExperiment),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(coordinator, new object[] { value });
        }

        private static void ConfigureNavigationOnly(
            ExperimentSessionCoordinator coordinator,
            SceneTalkOrchestrator orchestrator)
        {
            var type = typeof(ExperimentSessionCoordinator);
            var unsubscribe = type.GetMethod(
                "Unsubscribe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(unsubscribe, Is.Not.Null);
            unsubscribe.Invoke(coordinator, null);

            foreach (var fieldName in new[]
                     {
                         "conditionManager", "learningMemory", "history", "pilot", "formalCollection",
                         "rehearsal", "formalQuestionnaire"
                     })
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(coordinator, null);
            }

            var orchestratorField = type.GetField(
                "orchestrator",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(orchestratorField, Is.Not.Null);
            orchestratorField.SetValue(coordinator, orchestrator);
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private sealed class TableColumn
        {
            public string name { get; set; }
            public int notnull { get; set; }
        }
    }
}
