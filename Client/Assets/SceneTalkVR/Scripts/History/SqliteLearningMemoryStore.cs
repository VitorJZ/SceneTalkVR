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
    public sealed class SqliteLearningMemoryStore : ILearningMemoryStore
    {
        private const int CurrentSchemaVersion = 1;

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
                connection.Execute($"PRAGMA user_version = {CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)}");
            });
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
                scene_payload_json = JsonUtility.ToJson(detail.sceneSnapshot ?? new SpringScenePayload())
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
                correctionCount = row.correction_count
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
    }
}
