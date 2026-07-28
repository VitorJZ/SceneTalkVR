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
        public void VersionOneMigratesDirectlyToSplitSchemaAndPreservesOrdinaryConversation()
        {
            CreateVersionOneDatabase();
            using (var store = new SqliteLearningMemoryStore(databasePath))
            {
                store.Initialize();
                var legacy = store.GetSession("legacy-session");
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.summary.title, Is.EqualTo("Legacy conversation"));
                Assert.That(legacy.turns.Single().assistantText, Is.EqualTo("Legacy reply"));
                Assert.That(legacy.summary.IsExperimentConversation, Is.False);
                Assert.That(store.CountExperiments(), Is.Zero);
            }

            using var connection = new SQLiteConnection(databasePath);
            Assert.That(connection.ExecuteScalar<int>("PRAGMA user_version"), Is.EqualTo(3));
            AssertTableExists(connection, "experiment_records");
            AssertTableExists(connection, "experiment_attempts");
            AssertTableExists(connection, "questionnaire_sessions");
            AssertTableExists(connection, "experiment_rankings");
            AssertTableMissing(connection, "experiment_phases");
            var conversationColumns = connection.Query<TableColumn>("PRAGMA table_info(conversation_sessions)");
            Assert.That(conversationColumns.Any(item => item.name == "experiment_kind"), Is.True);
            Assert.That(conversationColumns.Any(item => item.name == "experiment_phase"), Is.False);
        }

        [Test]
        public void VersionTwoMigrationDeletesCompositeExperimentsButKeepsOrdinaryConversations()
        {
            CreateVersionTwoDatabase();
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();

            Assert.That(store.CountExperiments(), Is.Zero);
            Assert.That(store.GetSession("legacy-session"), Is.Not.Null);
            Assert.That(store.GetSession("old-experiment-conversation"), Is.Null);
            Assert.That(store.ListSessionIds(), Is.EquivalentTo(new[] { "legacy-session" }));
        }

        [Test]
        public void IncompleteVersionThreeMigrationIsRepairedWithoutLosingOrdinaryConversation()
        {
            CreateVersionTwoDatabase();
            using (var connection = new SQLiteConnection(databasePath))
                connection.Execute("PRAGMA user_version = 3");

            using (var store = new SqliteLearningMemoryStore(databasePath))
            {
                store.Initialize();
                Assert.That(store.CountExperiments(), Is.Zero);
                Assert.That(store.GetSession("legacy-session")?.turns.Single().assistantText,
                    Is.EqualTo("Legacy reply"));
                Assert.That(store.GetSession("old-experiment-conversation"), Is.Null);
            }

            using var repaired = new SQLiteConnection(databasePath);
            var columns = repaired.Query<TableColumn>("PRAGMA table_info(conversation_sessions)");
            Assert.That(columns.Any(item => item.name == "experiment_kind"), Is.True);
            Assert.That(columns.Any(item => item.name == "experiment_phase"), Is.False);
            AssertTableMissing(repaired, "experiment_phases");
        }

        [Test]
        public void SplitExperimentPagingQuestionnaireSnapshotsAndCascadeDeleteRoundTrip()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            for (var i = 0; i < 6; i++)
                store.CreateExperiment(CreateExperiment("exp-" + i, 1000 + i, i % 2 == 0 ? ExperimentKind.Pilot : ExperimentKind.Formal));

            Assert.That(store.CountExperiments(), Is.EqualTo(6));
            Assert.That(store.ListExperiments(0, 5).Count, Is.EqualTo(5));
            Assert.That(store.ListExperiments(0, 1).Single().experimentId, Is.EqualTo("exp-5"));

            var attempt = new ExperimentAttemptRecord
            {
                attemptId = "attempt-1", experimentId = "exp-5", conditionKey = "NE",
                taskId = "formal-task", runId = "run-1", attemptIndex = 1,
                status = ExperimentAttemptStatus.Completed, completionReason = "questionnaire_submitted",
                startedAtUnixMs = 2000, endedAtUnixMs = 3000
            };
            store.UpsertAttempt(attempt);
            store.UpsertQuestionnaire(new ExperimentQuestionnaireRecord
            {
                experimentId = "exp-5",
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
                    questionnaireId = "formal_condition_v1", questionnaireLinkageKey = "ql-1",
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
            store.CreateSession(CreateConversation("conversation-1", "exp-5", attempt.attemptId, ExperimentKind.Formal));

            var restored = store.GetExperiment("exp-5");
            Assert.That(restored.summary.kind, Is.EqualTo(ExperimentKind.Formal));
            Assert.That(restored.summary.assistantEmbodimentSnapshot, Is.EqualTo(ExperimentConditionManager.OrbAssistantEmbodiment));
            Assert.That(restored.attempts.Single().status, Is.EqualTo(ExperimentAttemptStatus.Completed));
            Assert.That(restored.questionnaires.Single().session.responses.Single().rawValue, Is.EqualTo("6"));
            Assert.That(restored.questionnaires.Single().session.sectionScores.Single().mean, Is.EqualTo(6f));
            Assert.That(restored.questionnaires.Single().prompts.Single().promptChinese, Is.EqualTo("我有临场感。"));
            Assert.That(restored.questionnaires.Single().questionnaireRecordId, Is.Not.Empty);
            Assert.That(restored.conversations.Single().experimentKind, Is.EqualTo(ExperimentKind.Formal.ToString()));

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
            store.CreateExperiment(CreateExperiment("exp-questionnaire-attempts", 1000, ExperimentKind.Formal));

            foreach (var attemptId in new[] { "attempt-1", "attempt-2" })
            {
                store.UpsertQuestionnaire(new ExperimentQuestionnaireRecord
                {
                    experimentId = "exp-questionnaire-attempts",
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
        public void SkippedQuestionnaireStatusAndPartialResponsesRoundTripWithoutMigration()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            store.CreateExperiment(CreateExperiment("exp-skipped-questionnaire", 1000, ExperimentKind.Formal));
            store.UpsertQuestionnaire(new ExperimentQuestionnaireRecord
            {
                experimentId = "exp-skipped-questionnaire",
                attemptId = "attempt-skip",
                session = new QuestionnaireSession
                {
                    questionnaireId = "formal_condition_v1",
                    questionnaireLinkageKey = "skip-linkage",
                    completionStatus = QuestionnaireCompletionStatus.Skipped,
                    completionRate = .25f,
                    hasMissing = true,
                    skippedAtUtc = "2026-07-28T00:00:00Z",
                    completionReason = "participant_skipped",
                    responses = new[] { new QuestionnaireResponse { itemId = "q1", rawValue = "5", questionnaireStatus = "Skipped" } }
                }
            });

            var session = store.GetExperiment("exp-skipped-questionnaire").questionnaires.Single().session;
            Assert.That(session.completionStatus, Is.EqualTo(QuestionnaireCompletionStatus.Skipped));
            Assert.That(session.completionRate, Is.EqualTo(.25f));
            Assert.That(session.hasMissing, Is.True);
            Assert.That(session.skippedAtUtc, Is.EqualTo("2026-07-28T00:00:00Z"));
            Assert.That(session.completionReason, Is.EqualTo("participant_skipped"));
            Assert.That(session.responses.Single().questionnaireStatus, Is.EqualTo("Skipped"));
        }

        [Test]
        public void PilotAndFormalAreIndependentRecords()
        {
            using var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            var pilot = CreateExperiment("pilot", 1000, ExperimentKind.Pilot);
            pilot.summary.status = ExperimentRecordStatus.Completed;
            pilot.summary.completedAtUnixMs = 1500;
            var formal = CreateExperiment("formal", 2000, ExperimentKind.Formal);
            formal.summary.status = ExperimentRecordStatus.Suspended;
            formal.summary.assistantEmbodimentSnapshot = ExperimentConditionManager.HumanoidAssistantEmbodiment;
            store.CreateExperiment(pilot);
            store.CreateExperiment(formal);

            var restoredPilot = store.GetExperiment("pilot").summary;
            var restoredFormal = store.GetExperiment("formal").summary;
            Assert.That(restoredPilot.kind, Is.EqualTo(ExperimentKind.Pilot));
            Assert.That(restoredPilot.CanContinue, Is.False);
            Assert.That(restoredFormal.kind, Is.EqualTo(ExperimentKind.Formal));
            Assert.That(restoredFormal.CanContinue, Is.True);
            Assert.That(restoredFormal.assistantEmbodimentSnapshot,
                Is.EqualTo(ExperimentConditionManager.HumanoidAssistantEmbodiment));
        }

        [Test]
        public void ExperimentConversationCannotBeDeletedIndividually()
        {
            using (var store = new SqliteLearningMemoryStore(databasePath))
            {
                store.Initialize();
                store.CreateExperiment(CreateExperiment("exp-protected", 1000, ExperimentKind.Pilot));
                store.CreateSession(CreateConversation("protected-conversation", "exp-protected", "attempt", ExperimentKind.Pilot));
            }

            host = new GameObject("Experiment conversation permission test");
            var memory = host.AddComponent<LearningMemoryService>();
            memory.ConfigureStoreForTests(new SqliteLearningMemoryStore(databasePath), ownedRoot);
            var summary = memory.GetSession("protected-conversation").summary;
            Assert.That(summary.CanContinue, Is.False);
            Assert.That(summary.CanDelete, Is.False);
            Assert.Throws<InvalidOperationException>(() => memory.DeleteSession("protected-conversation"));
        }

        [Test]
        public void DeleteRemovesOnlyOwnedExperimentDirectoryWithinAllowedRoots()
        {
            var safeFolder = Path.Combine(ownedRoot, "exp-safe");
            Directory.CreateDirectory(safeFolder);
            Directory.CreateDirectory(outsideRoot);
            var rootSentinel = Path.Combine(ownedRoot, "keep-root.txt");
            File.WriteAllText(Path.Combine(safeFolder, "raw.json"), "safe");
            File.WriteAllText(rootSentinel, "keep");
            File.WriteAllText(Path.Combine(outsideRoot, "external-copy.json"), "keep");

            var safe = CreateExperiment("exp-safe", 1000, ExperimentKind.Pilot);
            safe.summary.dataRootPath = safeFolder;
            var outside = CreateExperiment("exp-outside", 1001, ExperimentKind.Formal);
            outside.summary.dataRootPath = outsideRoot;
            var rootGuard = CreateExperiment("exp-root-guard", 1002, ExperimentKind.Pilot);
            rootGuard.summary.dataRootPath = ownedRoot;
            var store = new SqliteLearningMemoryStore(databasePath);
            store.Initialize();
            store.CreateExperiment(safe);
            store.CreateExperiment(outside);
            store.CreateExperiment(rootGuard);

            host = new GameObject("Experiment path safety test");
            var service = host.AddComponent<ExperimentHistoryService>();
            service.ConfigureStoreForTests(store, databasePath);
            Assert.That(service.DeleteExperiment("exp-safe", new[] { ownedRoot }), Is.True);
            Assert.That(service.DeleteExperiment("exp-outside", new[] { ownedRoot }), Is.True);
            Assert.That(service.DeleteExperiment("exp-root-guard", new[] { ownedRoot }), Is.True);
            Assert.That(Directory.Exists(safeFolder), Is.False);
            Assert.That(File.Exists(rootSentinel), Is.True);
            Assert.That(File.Exists(Path.Combine(outsideRoot, "external-copy.json")), Is.True);
        }

        [Test]
        public void ExitWarnsOnlyWhileCurrentExperimentIsIncomplete()
        {
            host = new GameObject("Experiment exit test");
            host.SetActive(false);
            var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
            var coordinator = host.AddComponent<ExperimentSessionCoordinator>();
            ConfigureNavigationOnly(coordinator, orchestrator);

            var detail = CreateExperiment("exp-exit", 1000, ExperimentKind.Pilot);
            SetCurrentExperiment(coordinator, detail);
            coordinator.RequestLeaveExperiment();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.ExperimentExitConfirm));

            coordinator.CancelLeaveExperiment();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.ExperimentSelection));
            detail.summary.status = ExperimentRecordStatus.Completed;
            coordinator.RequestLeaveExperiment();
            Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Idle));
            Assert.That(coordinator.CurrentExperiment, Is.Null);
        }

        private void CreateVersionOneDatabase()
        {
            using var connection = new SQLiteConnection(databasePath);
            CreateVersionOneTables(connection);
            InsertConversation(connection, "legacy-session", "Legacy conversation", "Legacy reply");
            connection.Execute("PRAGMA user_version = 1");
        }

        private void CreateVersionTwoDatabase()
        {
            using var connection = new SQLiteConnection(databasePath);
            CreateVersionOneTables(connection);
            connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_id TEXT");
            connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_phase TEXT");
            connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_attempt_id TEXT");
            connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_run_id TEXT");
            InsertConversation(connection, "legacy-session", "Legacy conversation", "Legacy reply");
            InsertConversation(connection, "old-experiment-conversation", "Old experiment", "Old reply");
            connection.Execute("UPDATE conversation_sessions SET experiment_id='old-composite' WHERE session_id='old-experiment-conversation'");
            connection.Execute(
                "CREATE TABLE experiment_records (experiment_id TEXT PRIMARY KEY NOT NULL, participant_id TEXT NOT NULL, "
                + "status INTEGER NOT NULL, pilot_status INTEGER NOT NULL, formal_status INTEGER NOT NULL, "
                + "preferred_embodiment TEXT NOT NULL, created_at_unix_ms INTEGER NOT NULL, updated_at_unix_ms INTEGER NOT NULL)");
            connection.Execute("INSERT INTO experiment_records VALUES ('old-composite','P',0,0,0,'',1,1)");
            connection.Execute("PRAGMA user_version = 2");
        }

        private static void CreateVersionOneTables(SQLiteConnection connection)
        {
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
        }

        private static void InsertConversation(SQLiteConnection connection, string id, string title, string reply)
        {
            connection.Execute(
                "INSERT INTO conversation_sessions "
                + "(session_id,title,scenario_id,task_type,environment_type,correction_provider,correction_style,"
                + "created_at_unix_ms,updated_at_unix_ms,turn_count,correction_count,settings_json,scene_payload_json) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                id, title, "legacy", "legacy", "legacy", "dialogue_avatar", "explicit",
                1000L, 1000L, 0, 0, "{}", "{}");
            connection.Execute(
                "INSERT INTO conversation_turns "
                + "(session_id, sequence_index, is_opening, created_at_unix_ms, user_text, assistant_text, has_correction, error_type, payload_json) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                id, 0, 1, 1000L, string.Empty, reply, 0, string.Empty, "{}");
        }

        private static ExperimentRecordDetail CreateExperiment(string id, long timestamp, ExperimentKind kind)
        {
            return new ExperimentRecordDetail
            {
                summary = new ExperimentRecordSummary
                {
                    experimentId = id,
                    participantId = "P-" + id,
                    sessionId = "S-" + id,
                    kind = kind,
                    status = ExperimentRecordStatus.InProgress,
                    assistantEmbodimentSnapshot = kind == ExperimentKind.Formal
                        ? ExperimentConditionManager.OrbAssistantEmbodiment
                        : string.Empty,
                    createdAtUnixMs = timestamp,
                    startedAtUnixMs = timestamp,
                    updatedAtUnixMs = timestamp
                }
            };
        }

        private static LearningSessionDetail CreateConversation(
            string sessionId,
            string experimentId,
            string attemptId,
            ExperimentKind kind)
        {
            return new LearningSessionDetail
            {
                summary = new LearningSessionSummary
                {
                    sessionId = sessionId, title = "Experiment dialogue", scenarioId = "task", taskType = "task",
                    environmentType = "restaurant", correctionProvider = "assistant_agent", correctionStyle = "explicit",
                    createdAtUnixMs = 1000, updatedAtUnixMs = 1000, experimentId = experimentId,
                    experimentKind = kind.ToString(), experimentAttemptId = attemptId, experimentRunId = "run-1"
                },
                settings = new ConversationSettingsSnapshot
                {
                    experimentId = experimentId, experimentKind = kind.ToString(),
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
            type.GetMethod("Unsubscribe", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(coordinator, null);
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
            type.GetField("orchestrator", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(coordinator, orchestrator);
        }

        private static void AssertTableExists(SQLiteConnection connection, string table) => Assert.That(
            connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", table),
            Is.EqualTo(1), table);

        private static void AssertTableMissing(SQLiteConnection connection, string table) => Assert.That(
            connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", table),
            Is.Zero, table);

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
        }
    }
}
