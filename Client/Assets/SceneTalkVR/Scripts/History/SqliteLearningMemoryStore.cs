using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using SQLite;
using UnityEngine;

namespace SceneTalkVR.History
{
    public sealed class SqliteLearningMemoryStore : ILearningMemoryStore, IExperimentHistoryStore
    {
        private const int CurrentSchemaVersion = 2;

        private readonly string databasePath;
        private SQLiteConnection connection;

        public SqliteLearningMemoryStore(string databasePath)
        {
            this.databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public void Initialize()
        {
            if (connection != null)
            {
                return;
            }

            var folder = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            connection = new SQLiteConnection(databasePath);
            try
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                connection.Execute("PRAGMA auto_vacuum = INCREMENTAL");
                connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL");
                connection.Execute("PRAGMA synchronous = NORMAL");
                var schemaVersion = connection.ExecuteScalar<int>("PRAGMA user_version");
                if (schemaVersion > CurrentSchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"History database schema {schemaVersion} is newer than supported version {CurrentSchemaVersion}.");
                }

                if (schemaVersion < 1)
                {
                    MigrateToVersion1();
                }
                // Version 2 creation is intentionally idempotent so a database produced by an
                // interrupted migration can repair missing tables or linkage columns on reopen.
                MigrateToVersion2();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void MigrateToVersion1()
        {
            connection.RunInTransaction(() =>
            {
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS conversation_sessions ("
                    + "session_id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL, scenario_id TEXT NOT NULL, "
                    + "task_type TEXT NOT NULL, environment_type TEXT NOT NULL, correction_provider TEXT NOT NULL, "
                    + "correction_style TEXT NOT NULL, created_at_unix_ms INTEGER NOT NULL, updated_at_unix_ms INTEGER NOT NULL, "
                    + "turn_count INTEGER NOT NULL, correction_count INTEGER NOT NULL, settings_json TEXT NOT NULL, "
                    + "scene_payload_json TEXT NOT NULL)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS conversation_turns ("
                    + "id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, sequence_index INTEGER NOT NULL, "
                    + "is_opening INTEGER NOT NULL, created_at_unix_ms INTEGER NOT NULL, user_text TEXT NOT NULL, "
                    + "assistant_text TEXT NOT NULL, has_correction INTEGER NOT NULL, error_type TEXT NOT NULL, payload_json TEXT NOT NULL, "
                    + "FOREIGN KEY(session_id) REFERENCES conversation_sessions(session_id) ON DELETE CASCADE)");
                connection.Execute(
                    "CREATE UNIQUE INDEX IF NOT EXISTS idx_conversation_turn_sequence "
                    + "ON conversation_turns(session_id, sequence_index)");
                connection.Execute(
                    "CREATE INDEX IF NOT EXISTS idx_conversation_sessions_updated "
                    + "ON conversation_sessions(updated_at_unix_ms DESC)");
                connection.Execute("PRAGMA user_version = 1");
            });
        }

        private void MigrateToVersion2()
        {
            connection.RunInTransaction(() =>
            {
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS experiment_records ("
                    + "experiment_id TEXT PRIMARY KEY NOT NULL, participant_id TEXT NOT NULL, status INTEGER NOT NULL, "
                    + "pilot_status INTEGER NOT NULL, formal_status INTEGER NOT NULL, preferred_embodiment TEXT NOT NULL, "
                    + "created_at_unix_ms INTEGER NOT NULL, updated_at_unix_ms INTEGER NOT NULL)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS experiment_phases ("
                    + "experiment_id TEXT NOT NULL, phase INTEGER NOT NULL, session_id TEXT NOT NULL, status INTEGER NOT NULL, "
                    + "data_root_path TEXT NOT NULL, started_at_unix_ms INTEGER NOT NULL, completed_at_unix_ms INTEGER NOT NULL, "
                    + "updated_at_unix_ms INTEGER NOT NULL, PRIMARY KEY(experiment_id, phase), "
                    + "FOREIGN KEY(experiment_id) REFERENCES experiment_records(experiment_id) ON DELETE CASCADE)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS experiment_attempts ("
                    + "attempt_id TEXT PRIMARY KEY NOT NULL, experiment_id TEXT NOT NULL, phase INTEGER NOT NULL, "
                    + "condition_key TEXT NOT NULL, task_id TEXT NOT NULL, run_id TEXT NOT NULL, attempt_index INTEGER NOT NULL, "
                    + "status INTEGER NOT NULL, completion_reason TEXT NOT NULL, started_at_unix_ms INTEGER NOT NULL, "
                    + "ended_at_unix_ms INTEGER NOT NULL, FOREIGN KEY(experiment_id) REFERENCES experiment_records(experiment_id) ON DELETE CASCADE)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS questionnaire_sessions ("
                    + "questionnaire_session_key TEXT PRIMARY KEY NOT NULL, experiment_id TEXT NOT NULL, phase INTEGER NOT NULL, "
                    + "attempt_id TEXT, linkage_key TEXT NOT NULL, questionnaire_id TEXT NOT NULL, completion_status INTEGER NOT NULL, "
                    + "completion_rate REAL NOT NULL, has_missing INTEGER NOT NULL, session_json TEXT NOT NULL, prompts_json TEXT NOT NULL, "
                    + "updated_at_unix_ms INTEGER NOT NULL, FOREIGN KEY(experiment_id) REFERENCES experiment_records(experiment_id) ON DELETE CASCADE)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS questionnaire_responses ("
                    + "questionnaire_session_key TEXT NOT NULL, item_id TEXT NOT NULL, raw_value TEXT NOT NULL, "
                    + "scored_value REAL NOT NULL, has_scored_value INTEGER NOT NULL, response_json TEXT NOT NULL, "
                    + "PRIMARY KEY(questionnaire_session_key, item_id), "
                    + "FOREIGN KEY(questionnaire_session_key) REFERENCES questionnaire_sessions(questionnaire_session_key) ON DELETE CASCADE)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS questionnaire_scores ("
                    + "questionnaire_session_key TEXT NOT NULL, section_id TEXT NOT NULL, mean REAL NOT NULL, "
                    + "answered_count INTEGER NOT NULL, item_count INTEGER NOT NULL, has_missing INTEGER NOT NULL, "
                    + "PRIMARY KEY(questionnaire_session_key, section_id), "
                    + "FOREIGN KEY(questionnaire_session_key) REFERENCES questionnaire_sessions(questionnaire_session_key) ON DELETE CASCADE)");
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS experiment_rankings ("
                    + "experiment_id TEXT NOT NULL, phase INTEGER NOT NULL, response_json TEXT NOT NULL, "
                    + "updated_at_unix_ms INTEGER NOT NULL, PRIMARY KEY(experiment_id, phase), "
                    + "FOREIGN KEY(experiment_id) REFERENCES experiment_records(experiment_id) ON DELETE CASCADE)");

                if (!HasColumn("conversation_sessions", "experiment_id"))
                    connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_id TEXT");
                if (!HasColumn("conversation_sessions", "experiment_phase"))
                    connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_phase TEXT");
                if (!HasColumn("conversation_sessions", "experiment_attempt_id"))
                    connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_attempt_id TEXT");
                if (!HasColumn("conversation_sessions", "experiment_run_id"))
                    connection.Execute("ALTER TABLE conversation_sessions ADD COLUMN experiment_run_id TEXT");

                connection.Execute("CREATE INDEX IF NOT EXISTS idx_experiment_records_updated ON experiment_records(updated_at_unix_ms DESC)");
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_experiment_attempts_parent ON experiment_attempts(experiment_id, phase, attempt_index)");
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_questionnaire_sessions_parent ON questionnaire_sessions(experiment_id, phase)");
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_conversation_sessions_experiment ON conversation_sessions(experiment_id, updated_at_unix_ms DESC)");
                connection.Execute($"PRAGMA user_version = {CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)}");
            });
        }

        private bool HasColumn(string table, string column)
        {
            return connection.Query<TableInfoRow>($"PRAGMA table_info({table})")
                .Any(item => string.Equals(item.name, column, StringComparison.OrdinalIgnoreCase));
        }

        public int CountSessions()
        {
            EnsureInitialized();
            return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM conversation_sessions");
        }

        public IReadOnlyList<LearningSessionSummary> ListSessions(int offset, int limit)
        {
            EnsureInitialized();
            var safeOffset = Math.Max(0, offset);
            var safeLimit = Math.Max(1, limit);
            return connection.Query<ConversationSessionRow>(
                    "SELECT * FROM conversation_sessions "
                    + "ORDER BY updated_at_unix_ms DESC, session_id ASC LIMIT ? OFFSET ?",
                    safeLimit,
                    safeOffset)
                .Select(ToSummary)
                .ToArray();
        }

        public LearningSessionDetail GetSession(string sessionId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return null;
            }

            var session = connection.Find<ConversationSessionRow>(sessionId);
            if (session == null)
            {
                return null;
            }

            var turns = connection.Query<ConversationTurnRow>(
                "SELECT * FROM conversation_turns WHERE session_id = ? ORDER BY sequence_index ASC",
                sessionId);

            return new LearningSessionDetail
            {
                summary = ToSummary(session),
                settings = DeserializeOrDefault<ConversationSettingsSnapshot>(session.settings_json),
                sceneSnapshot = DeserializeOrDefault<SpringScenePayload>(session.scene_payload_json),
                turns = turns.Select(ToTurn).ToArray()
            };
        }

        public IReadOnlyCollection<string> ListSessionIds()
        {
            EnsureInitialized();
            return connection.Query<SessionIdProjection>("SELECT session_id FROM conversation_sessions")
                .Select(item => item.session_id)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        public void CreateSession(LearningSessionDetail detail)
        {
            EnsureInitialized();
            if (detail == null || detail.summary == null || string.IsNullOrWhiteSpace(detail.summary.sessionId))
            {
                throw new ArgumentException("A history session requires a non-empty session ID.", nameof(detail));
            }

            var session = ToRow(detail);
            var turns = detail.turns ?? Array.Empty<DialogueTurnRecord>();
            connection.RunInTransaction(() =>
            {
                connection.Insert(session);
                foreach (var turn in turns)
                {
                    connection.Insert(ToRow(session.session_id, turn));
                }
            });
        }

        public void AppendTurn(string sessionId, DialogueTurnRecord turn)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("A history session ID is required.", nameof(sessionId));
            }

            if (turn == null)
            {
                throw new ArgumentNullException(nameof(turn));
            }

            connection.RunInTransaction(() =>
            {
                var session = connection.Find<ConversationSessionRow>(sessionId)
                    ?? throw new InvalidOperationException($"History session '{sessionId}' was not found.");
                connection.Insert(ToRow(sessionId, turn));
                session.updated_at_unix_ms = Math.Max(session.updated_at_unix_ms, turn.createdAtUnixMs);
                if (!turn.isOpening)
                {
                    session.turn_count++;
                    if (turn.HasCorrection)
                    {
                        session.correction_count++;
                    }
                }

                connection.Update(session);
            });
        }

        public void UpdateSession(LearningSessionDetail detail)
        {
            EnsureInitialized();
            if (detail?.summary == null || string.IsNullOrWhiteSpace(detail.summary.sessionId))
            {
                throw new ArgumentException("A history session requires a non-empty session ID.", nameof(detail));
            }

            connection.RunInTransaction(() =>
            {
                if (connection.Update(ToRow(detail)) == 0)
                {
                    throw new InvalidOperationException(
                        $"History session '{detail.summary.sessionId}' was not found.");
                }
            });
        }

        public bool DeleteSession(string sessionId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            var deleted = connection.Execute(
                "DELETE FROM conversation_sessions WHERE session_id = ?",
                sessionId) > 0;
            if (deleted)
            {
                connection.ExecuteScalar<int>("PRAGMA wal_checkpoint(TRUNCATE)");
                connection.Execute("PRAGMA incremental_vacuum");
            }

            return deleted;
        }

        public int CountExperiments()
        {
            EnsureInitialized();
            return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM experiment_records");
        }

        public IReadOnlyList<ExperimentRecordSummary> ListExperiments(int offset, int limit)
        {
            EnsureInitialized();
            return connection.Query<ExperimentRecordRow>(
                    "SELECT * FROM experiment_records ORDER BY updated_at_unix_ms DESC, experiment_id ASC LIMIT ? OFFSET ?",
                    Math.Max(1, limit),
                    Math.Max(0, offset))
                .Select(ToExperimentSummary)
                .ToArray();
        }

        public ExperimentRecordDetail GetExperiment(string experimentId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(experimentId)) return null;
            var record = connection.Find<ExperimentRecordRow>(experimentId);
            if (record == null) return null;

            var phases = connection.Query<ExperimentPhaseRow>(
                    "SELECT * FROM experiment_phases WHERE experiment_id = ? ORDER BY phase ASC", experimentId)
                .Select(ToPhase).ToArray();
            var attempts = connection.Query<ExperimentAttemptRow>(
                    "SELECT * FROM experiment_attempts WHERE experiment_id = ? ORDER BY phase ASC, attempt_index ASC, started_at_unix_ms ASC",
                    experimentId)
                .Select(ToAttempt).ToArray();
            var conversations = connection.Query<ConversationSessionRow>(
                    "SELECT * FROM conversation_sessions WHERE experiment_id = ? ORDER BY updated_at_unix_ms DESC, session_id ASC",
                    experimentId)
                .Select(ToSummary).ToArray();
            var questionnaires = connection.Query<QuestionnaireSessionRow>(
                    "SELECT * FROM questionnaire_sessions WHERE experiment_id = ? ORDER BY phase ASC, updated_at_unix_ms ASC",
                    experimentId)
                .Select(ToQuestionnaire).ToArray();
            var rankings = connection.Query<ExperimentRankingRow>(
                    "SELECT * FROM experiment_rankings WHERE experiment_id = ? ORDER BY phase ASC", experimentId)
                .Select(ToRanking).ToArray();

            return new ExperimentRecordDetail
            {
                summary = ToExperimentSummary(record),
                phases = phases,
                attempts = attempts,
                conversations = conversations,
                questionnaires = questionnaires,
                rankings = rankings
            };
        }

        public void CreateExperiment(ExperimentRecordDetail detail)
        {
            EnsureInitialized();
            if (detail?.summary == null || string.IsNullOrWhiteSpace(detail.summary.experimentId))
                throw new ArgumentException("An experiment requires a non-empty experiment ID.", nameof(detail));
            connection.RunInTransaction(() =>
            {
                connection.Insert(ToRow(detail.summary));
                foreach (var phase in detail.phases ?? Array.Empty<ExperimentPhaseRecord>())
                    connection.Insert(ToRow(phase));
            });
        }

        public void UpdateExperiment(ExperimentRecordSummary summary)
        {
            EnsureInitialized();
            if (summary == null || string.IsNullOrWhiteSpace(summary.experimentId))
                throw new ArgumentException("A valid experiment summary is required.", nameof(summary));
            if (connection.Update(ToRow(summary)) == 0)
                throw new InvalidOperationException($"Experiment '{summary.experimentId}' was not found.");
        }

        public void UpsertPhase(ExperimentPhaseRecord phase)
        {
            EnsureInitialized();
            if (phase == null || string.IsNullOrWhiteSpace(phase.experimentId))
                throw new ArgumentException("A valid experiment phase is required.", nameof(phase));
            connection.InsertOrReplace(ToRow(phase));
        }

        public void UpsertAttempt(ExperimentAttemptRecord attempt)
        {
            EnsureInitialized();
            if (attempt == null || string.IsNullOrWhiteSpace(attempt.attemptId)
                || string.IsNullOrWhiteSpace(attempt.experimentId))
                throw new ArgumentException("A valid experiment attempt is required.", nameof(attempt));
            connection.InsertOrReplace(ToRow(attempt));
        }

        public void UpsertQuestionnaire(ExperimentQuestionnaireRecord questionnaire)
        {
            EnsureInitialized();
            if (questionnaire?.session == null || string.IsNullOrWhiteSpace(questionnaire.experimentId))
                throw new ArgumentException("A valid experiment questionnaire is required.", nameof(questionnaire));
            var linkage = questionnaire.session.questionnaireLinkageKey ?? string.Empty;
            var key = $"{questionnaire.experimentId}:{(int)questionnaire.phase}:{linkage}:{questionnaire.attemptId}";
            var row = new QuestionnaireSessionRow
            {
                questionnaire_session_key = key,
                experiment_id = questionnaire.experimentId,
                phase = (int)questionnaire.phase,
                attempt_id = questionnaire.attemptId ?? string.Empty,
                linkage_key = linkage,
                questionnaire_id = questionnaire.session.questionnaireId ?? string.Empty,
                completion_status = (int)questionnaire.session.completionStatus,
                completion_rate = questionnaire.session.completionRate,
                has_missing = questionnaire.session.hasMissing ? 1 : 0,
                session_json = JsonUtility.ToJson(questionnaire.session),
                prompts_json = JsonUtility.ToJson(new PromptSnapshotCollection { items = questionnaire.prompts ?? Array.Empty<QuestionnairePromptSnapshot>() }),
                updated_at_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            connection.RunInTransaction(() =>
            {
                connection.InsertOrReplace(row);
                connection.Execute("DELETE FROM questionnaire_responses WHERE questionnaire_session_key = ?", key);
                connection.Execute("DELETE FROM questionnaire_scores WHERE questionnaire_session_key = ?", key);
                foreach (var response in questionnaire.session.responses ?? Array.Empty<QuestionnaireResponse>())
                {
                    connection.Insert(new QuestionnaireResponseRow
                    {
                        questionnaire_session_key = key,
                        item_id = response.itemId ?? string.Empty,
                        raw_value = response.rawValue ?? string.Empty,
                        scored_value = response.scoredValue,
                        has_scored_value = response.hasScoredValue ? 1 : 0,
                        response_json = JsonUtility.ToJson(response)
                    });
                }
                foreach (var score in questionnaire.session.sectionScores ?? Array.Empty<QuestionnaireScoreResult>())
                {
                    connection.Insert(new QuestionnaireScoreRow
                    {
                        questionnaire_session_key = key,
                        section_id = score.sectionId ?? string.Empty,
                        mean = score.mean,
                        answered_count = score.answeredCount,
                        item_count = score.itemCount,
                        has_missing = score.hasMissing ? 1 : 0
                    });
                }
            });
        }

        public void UpsertRanking(ExperimentRankingRecord ranking)
        {
            EnsureInitialized();
            if (ranking?.response == null || string.IsNullOrWhiteSpace(ranking.experimentId))
                throw new ArgumentException("A valid experiment ranking is required.", nameof(ranking));
            connection.InsertOrReplace(new ExperimentRankingRow
            {
                experiment_id = ranking.experimentId,
                phase = (int)ranking.phase,
                response_json = JsonUtility.ToJson(ranking.response),
                updated_at_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        public bool DeleteExperiment(string experimentId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(experimentId)) return false;
            var deleted = false;
            connection.RunInTransaction(() =>
            {
                connection.Execute(
                    "DELETE FROM conversation_turns WHERE session_id IN (SELECT session_id FROM conversation_sessions WHERE experiment_id = ?)",
                    experimentId);
                connection.Execute("DELETE FROM conversation_sessions WHERE experiment_id = ?", experimentId);
                connection.Execute(
                    "DELETE FROM questionnaire_responses WHERE questionnaire_session_key IN "
                    + "(SELECT questionnaire_session_key FROM questionnaire_sessions WHERE experiment_id = ?)", experimentId);
                connection.Execute(
                    "DELETE FROM questionnaire_scores WHERE questionnaire_session_key IN "
                    + "(SELECT questionnaire_session_key FROM questionnaire_sessions WHERE experiment_id = ?)", experimentId);
                connection.Execute("DELETE FROM questionnaire_sessions WHERE experiment_id = ?", experimentId);
                connection.Execute("DELETE FROM experiment_rankings WHERE experiment_id = ?", experimentId);
                connection.Execute("DELETE FROM experiment_attempts WHERE experiment_id = ?", experimentId);
                connection.Execute("DELETE FROM experiment_phases WHERE experiment_id = ?", experimentId);
                deleted = connection.Execute("DELETE FROM experiment_records WHERE experiment_id = ?", experimentId) > 0;
            });
            if (deleted)
            {
                connection.ExecuteScalar<int>("PRAGMA wal_checkpoint(TRUNCATE)");
                connection.Execute("PRAGMA incremental_vacuum");
            }
            return deleted;
        }

        public void Dispose()
        {
            connection?.Close();
            connection?.Dispose();
            connection = null;
        }

        private void EnsureInitialized()
        {
            if (connection == null)
            {
                Initialize();
            }
        }

        private static ConversationSessionRow ToRow(LearningSessionDetail detail)
        {
            var summary = detail.summary;
            return new ConversationSessionRow
            {
                session_id = summary.sessionId,
                title = summary.title ?? string.Empty,
                scenario_id = summary.scenarioId ?? string.Empty,
                task_type = summary.taskType ?? string.Empty,
                environment_type = summary.environmentType ?? string.Empty,
                correction_provider = summary.correctionProvider ?? string.Empty,
                correction_style = summary.correctionStyle ?? string.Empty,
                created_at_unix_ms = summary.createdAtUnixMs,
                updated_at_unix_ms = summary.updatedAtUnixMs,
                turn_count = summary.turnCount,
                correction_count = summary.correctionCount,
                settings_json = JsonUtility.ToJson(detail.settings ?? new ConversationSettingsSnapshot()),
                scene_payload_json = JsonUtility.ToJson(detail.sceneSnapshot ?? new SpringScenePayload()),
                experiment_id = summary.experimentId ?? detail.settings?.experimentId ?? string.Empty,
                experiment_phase = summary.experimentPhase ?? detail.settings?.experimentPhase ?? string.Empty,
                experiment_attempt_id = summary.experimentAttemptId ?? detail.settings?.experimentAttemptId ?? string.Empty,
                experiment_run_id = summary.experimentRunId ?? detail.settings?.experimentRunId ?? string.Empty
            };
        }

        private static ConversationTurnRow ToRow(string sessionId, DialogueTurnRecord turn)
        {
            return new ConversationTurnRow
            {
                session_id = sessionId,
                sequence_index = turn.sequenceIndex,
                is_opening = turn.isOpening ? 1 : 0,
                created_at_unix_ms = turn.createdAtUnixMs,
                user_text = turn.userText ?? string.Empty,
                assistant_text = turn.assistantText ?? string.Empty,
                has_correction = turn.HasCorrection ? 1 : 0,
                error_type = turn.payload?.correctionFeedback?.errorType ?? string.Empty,
                payload_json = JsonUtility.ToJson(turn.payload ?? new SpringScenePayload())
            };
        }

        private static LearningSessionSummary ToSummary(ConversationSessionRow row)
        {
            return new LearningSessionSummary
            {
                sessionId = row.session_id,
                title = row.title,
                scenarioId = row.scenario_id,
                taskType = row.task_type,
                environmentType = row.environment_type,
                correctionProvider = row.correction_provider,
                correctionStyle = row.correction_style,
                createdAtUnixMs = row.created_at_unix_ms,
                updatedAtUnixMs = row.updated_at_unix_ms,
                turnCount = row.turn_count,
                correctionCount = row.correction_count,
                experimentId = row.experiment_id,
                experimentPhase = row.experiment_phase,
                experimentAttemptId = row.experiment_attempt_id,
                experimentRunId = row.experiment_run_id
            };
        }

        private static DialogueTurnRecord ToTurn(ConversationTurnRow row)
        {
            return new DialogueTurnRecord
            {
                sequenceIndex = row.sequence_index,
                isOpening = row.is_opening != 0,
                createdAtUnixMs = row.created_at_unix_ms,
                userText = row.user_text,
                assistantText = row.assistant_text,
                payload = DeserializeOrDefault<SpringScenePayload>(row.payload_json)
            };
        }

        private static ExperimentRecordRow ToRow(ExperimentRecordSummary value) => new ExperimentRecordRow
        {
            experiment_id = value.experimentId,
            participant_id = value.participantId ?? string.Empty,
            status = (int)value.status,
            pilot_status = (int)value.pilotStatus,
            formal_status = (int)value.formalStatus,
            preferred_embodiment = value.preferredEmbodiment ?? string.Empty,
            created_at_unix_ms = value.createdAtUnixMs,
            updated_at_unix_ms = value.updatedAtUnixMs
        };

        private static ExperimentRecordSummary ToExperimentSummary(ExperimentRecordRow value) => new ExperimentRecordSummary
        {
            experimentId = value.experiment_id,
            participantId = value.participant_id,
            status = (ExperimentRecordStatus)value.status,
            pilotStatus = (ExperimentPhaseStatus)value.pilot_status,
            formalStatus = (ExperimentPhaseStatus)value.formal_status,
            preferredEmbodiment = value.preferred_embodiment,
            createdAtUnixMs = value.created_at_unix_ms,
            updatedAtUnixMs = value.updated_at_unix_ms
        };

        private static ExperimentPhaseRow ToRow(ExperimentPhaseRecord value) => new ExperimentPhaseRow
        {
            experiment_id = value.experimentId,
            phase = (int)value.phase,
            session_id = value.sessionId ?? string.Empty,
            status = (int)value.status,
            data_root_path = value.dataRootPath ?? string.Empty,
            started_at_unix_ms = value.startedAtUnixMs,
            completed_at_unix_ms = value.completedAtUnixMs,
            updated_at_unix_ms = value.updatedAtUnixMs
        };

        private static ExperimentPhaseRecord ToPhase(ExperimentPhaseRow value) => new ExperimentPhaseRecord
        {
            experimentId = value.experiment_id,
            phase = (ExperimentPhaseKind)value.phase,
            sessionId = value.session_id,
            status = (ExperimentPhaseStatus)value.status,
            dataRootPath = value.data_root_path,
            startedAtUnixMs = value.started_at_unix_ms,
            completedAtUnixMs = value.completed_at_unix_ms,
            updatedAtUnixMs = value.updated_at_unix_ms
        };

        private static ExperimentAttemptRow ToRow(ExperimentAttemptRecord value) => new ExperimentAttemptRow
        {
            attempt_id = value.attemptId,
            experiment_id = value.experimentId,
            phase = (int)value.phase,
            condition_key = value.conditionKey ?? string.Empty,
            task_id = value.taskId ?? string.Empty,
            run_id = value.runId ?? string.Empty,
            attempt_index = value.attemptIndex,
            status = (int)value.status,
            completion_reason = value.completionReason ?? string.Empty,
            started_at_unix_ms = value.startedAtUnixMs,
            ended_at_unix_ms = value.endedAtUnixMs
        };

        private static ExperimentAttemptRecord ToAttempt(ExperimentAttemptRow value) => new ExperimentAttemptRecord
        {
            attemptId = value.attempt_id,
            experimentId = value.experiment_id,
            phase = (ExperimentPhaseKind)value.phase,
            conditionKey = value.condition_key,
            taskId = value.task_id,
            runId = value.run_id,
            attemptIndex = value.attempt_index,
            status = (ExperimentAttemptStatus)value.status,
            completionReason = value.completion_reason,
            startedAtUnixMs = value.started_at_unix_ms,
            endedAtUnixMs = value.ended_at_unix_ms
        };

        private ExperimentQuestionnaireRecord ToQuestionnaire(QuestionnaireSessionRow value)
        {
            var prompts = DeserializeOrDefault<PromptSnapshotCollection>(value.prompts_json);
            var session = DeserializeOrDefault<QuestionnaireSession>(value.session_json);
            session.responses = connection.Query<QuestionnaireResponseRow>(
                    "SELECT * FROM questionnaire_responses WHERE questionnaire_session_key = ? ORDER BY item_id ASC",
                    value.questionnaire_session_key)
                .Select(item => DeserializeOrDefault<QuestionnaireResponse>(item.response_json))
                .ToArray();
            session.sectionScores = connection.Query<QuestionnaireScoreRow>(
                    "SELECT * FROM questionnaire_scores WHERE questionnaire_session_key = ? ORDER BY section_id ASC",
                    value.questionnaire_session_key)
                .Select(item => new QuestionnaireScoreResult
                {
                    sectionId = item.section_id,
                    mean = item.mean,
                    answeredCount = item.answered_count,
                    itemCount = item.item_count,
                    hasMissing = item.has_missing != 0
                })
                .ToArray();
            session.completionStatus = (QuestionnaireCompletionStatus)value.completion_status;
            session.completionRate = value.completion_rate;
            session.hasMissing = value.has_missing != 0;
            return new ExperimentQuestionnaireRecord
            {
                questionnaireRecordId = value.questionnaire_session_key,
                experimentId = value.experiment_id,
                phase = (ExperimentPhaseKind)value.phase,
                attemptId = value.attempt_id,
                session = session,
                prompts = prompts.items ?? Array.Empty<QuestionnairePromptSnapshot>()
            };
        }

        private static ExperimentRankingRecord ToRanking(ExperimentRankingRow value) => new ExperimentRankingRecord
        {
            experimentId = value.experiment_id,
            phase = (ExperimentPhaseKind)value.phase,
            response = DeserializeOrDefault<PreferenceRankingResponse>(value.response_json)
        };

        private static T DeserializeOrDefault<T>(string json) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new T();
            }

            try
            {
                return JsonUtility.FromJson<T>(json) ?? new T();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SceneTalkVR] Failed to deserialize history data: {exception.Message}");
                return new T();
            }
        }

        [Table("conversation_sessions")]
        private sealed class ConversationSessionRow
        {
            public ConversationSessionRow()
            {
            }

            [PrimaryKey, Column("session_id")]
            public string session_id { get; set; }

            public string title { get; set; }
            public string scenario_id { get; set; }
            public string task_type { get; set; }
            public string environment_type { get; set; }
            public string correction_provider { get; set; }
            public string correction_style { get; set; }
            public long created_at_unix_ms { get; set; }
            public long updated_at_unix_ms { get; set; }
            public int turn_count { get; set; }
            public int correction_count { get; set; }
            public string settings_json { get; set; }
            public string scene_payload_json { get; set; }
            public string experiment_id { get; set; }
            public string experiment_phase { get; set; }
            public string experiment_attempt_id { get; set; }
            public string experiment_run_id { get; set; }
        }

        [Table("conversation_turns")]
        private sealed class ConversationTurnRow
        {
            public ConversationTurnRow()
            {
            }

            [PrimaryKey, AutoIncrement]
            public long id { get; set; }

            [Indexed, Column("session_id")]
            public string session_id { get; set; }

            public int sequence_index { get; set; }
            public int is_opening { get; set; }
            public long created_at_unix_ms { get; set; }
            public string user_text { get; set; }
            public string assistant_text { get; set; }
            public int has_correction { get; set; }
            public string error_type { get; set; }
            public string payload_json { get; set; }
        }

        private sealed class SessionIdProjection
        {
            public SessionIdProjection()
            {
            }

            public string session_id { get; set; }
        }

        private sealed class TableInfoRow
        {
            public string name { get; set; }
        }

        [Table("experiment_records")]
        private sealed class ExperimentRecordRow
        {
            [PrimaryKey] public string experiment_id { get; set; }
            public string participant_id { get; set; }
            public int status { get; set; }
            public int pilot_status { get; set; }
            public int formal_status { get; set; }
            public string preferred_embodiment { get; set; }
            public long created_at_unix_ms { get; set; }
            public long updated_at_unix_ms { get; set; }
        }

        [Table("experiment_phases")]
        private sealed class ExperimentPhaseRow
        {
            public string experiment_id { get; set; }
            public int phase { get; set; }
            public string session_id { get; set; }
            public int status { get; set; }
            public string data_root_path { get; set; }
            public long started_at_unix_ms { get; set; }
            public long completed_at_unix_ms { get; set; }
            public long updated_at_unix_ms { get; set; }
        }

        [Table("experiment_attempts")]
        private sealed class ExperimentAttemptRow
        {
            [PrimaryKey] public string attempt_id { get; set; }
            public string experiment_id { get; set; }
            public int phase { get; set; }
            public string condition_key { get; set; }
            public string task_id { get; set; }
            public string run_id { get; set; }
            public int attempt_index { get; set; }
            public int status { get; set; }
            public string completion_reason { get; set; }
            public long started_at_unix_ms { get; set; }
            public long ended_at_unix_ms { get; set; }
        }

        [Table("questionnaire_sessions")]
        private sealed class QuestionnaireSessionRow
        {
            [PrimaryKey] public string questionnaire_session_key { get; set; }
            public string experiment_id { get; set; }
            public int phase { get; set; }
            public string attempt_id { get; set; }
            public string linkage_key { get; set; }
            public string questionnaire_id { get; set; }
            public int completion_status { get; set; }
            public float completion_rate { get; set; }
            public int has_missing { get; set; }
            public string session_json { get; set; }
            public string prompts_json { get; set; }
            public long updated_at_unix_ms { get; set; }
        }

        [Table("questionnaire_responses")]
        private sealed class QuestionnaireResponseRow
        {
            public string questionnaire_session_key { get; set; }
            public string item_id { get; set; }
            public string raw_value { get; set; }
            public float scored_value { get; set; }
            public int has_scored_value { get; set; }
            public string response_json { get; set; }
        }

        [Table("questionnaire_scores")]
        private sealed class QuestionnaireScoreRow
        {
            public string questionnaire_session_key { get; set; }
            public string section_id { get; set; }
            public float mean { get; set; }
            public int answered_count { get; set; }
            public int item_count { get; set; }
            public int has_missing { get; set; }
        }

        [Table("experiment_rankings")]
        private sealed class ExperimentRankingRow
        {
            public string experiment_id { get; set; }
            public int phase { get; set; }
            public string response_json { get; set; }
            public long updated_at_unix_ms { get; set; }
        }

        [Serializable]
        private sealed class PromptSnapshotCollection
        {
            public QuestionnairePromptSnapshot[] items = Array.Empty<QuestionnairePromptSnapshot>();
        }
    }
}
