using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.History
{
    [DisallowMultipleComponent]
    public sealed class LearningMemoryService : MonoBehaviour, IDisposable
    {
        public const int DefaultPageSize = 5;
        private ILearningMemoryStore store;
        private int activeNextSequenceIndex = 1;
        private string historyRootOverride;

        public string ActiveSessionId { get; private set; }
        public SpringScenePayload ActiveSceneSnapshot { get; private set; }
        public ConversationSettingsSnapshot ActiveSettings { get; private set; }
        public bool HasActiveSession => !string.IsNullOrWhiteSpace(ActiveSessionId);
        public string LastError { get; private set; }

        public string HistoryRootPath => string.IsNullOrWhiteSpace(historyRootOverride)
            ? HistoryStoragePaths.RootPath
            : historyRootOverride;

        private string DatabasePath => Path.Combine(HistoryRootPath, HistoryStoragePaths.DatabaseFileName);
        private string AssetsRootPath => Path.Combine(HistoryRootPath, "Assets");

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            store?.Dispose();
            store = null;
            EndActiveSession();
        }

        public void ConfigureStoreForTests(
            ILearningMemoryStore customStore,
            string customHistoryRootPath = null)
        {
            store?.Dispose();
            historyRootOverride = string.IsNullOrWhiteSpace(customHistoryRootPath)
                ? string.Empty
                : Path.GetFullPath(customHistoryRootPath);
            store = customStore;
            store?.Initialize();
            if (store != null && !string.IsNullOrWhiteSpace(historyRootOverride))
            {
                CleanupOrphanedAssets();
            }
        }

        public LearningSessionPage GetPage(int pageIndex, int pageSize = DefaultPageSize)
        {
            EnsureInitialized();
            var safePageSize = Mathf.Max(1, pageSize);
            var total = store.CountSessions();
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(total / (float)safePageSize));
            var safePageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            return new LearningSessionPage
            {
                pageIndex = safePageIndex,
                pageSize = safePageSize,
                totalCount = total,
                items = store.ListSessions(safePageIndex * safePageSize, safePageSize).ToArray()
            };
        }

        public LearningSessionDetail GetSession(string sessionId)
        {
            EnsureInitialized();
            return store.GetSession(sessionId);
        }

        public string BeginSession(
            string sessionId,
            SpringScenePayload sceneSnapshot,
            ConversationSettingsSnapshot settings,
            string title,
            string openingAssistantText,
            string initialUserText = null)
        {
            EnsureInitialized();
            sessionId = string.IsNullOrWhiteSpace(sessionId)
                ? Guid.NewGuid().ToString("N")
                : sessionId.Trim();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var payload = ClonePayload(sceneSnapshot);
            var hasInitialUserTurn = !string.IsNullOrWhiteSpace(initialUserText);
            var firstTurn = new DialogueTurnRecord
            {
                sequenceIndex = hasInitialUserTurn ? 1 : 0,
                isOpening = !hasInitialUserTurn,
                createdAtUnixMs = now,
                userText = initialUserText ?? string.Empty,
                assistantText = openingAssistantText ?? payload.dialogueReply ?? string.Empty,
                payload = ClonePayload(payload)
            };

            var feedback = payload.correctionFeedback;
            var detail = new LearningSessionDetail
            {
                summary = new LearningSessionSummary
                {
                    sessionId = sessionId,
                    title = string.IsNullOrWhiteSpace(title) ? ResolveTitle(payload) : title.Trim(),
                    scenarioId = settings?.condition?.scenarioId ?? payload.taskType ?? string.Empty,
                    taskType = payload.taskType ?? string.Empty,
                    environmentType = payload.environmentType ?? string.Empty,
                    correctionProvider = settings?.condition?.provider ?? feedback?.provider ?? string.Empty,
                    correctionStyle = settings?.condition?.style ?? feedback?.style ?? string.Empty,
                    createdAtUnixMs = now,
                    updatedAtUnixMs = now,
                    turnCount = hasInitialUserTurn ? 1 : 0,
                    correctionCount = hasInitialUserTurn && firstTurn.HasCorrection ? 1 : 0,
                    experimentId = settings?.experimentId ?? string.Empty,
                    experimentKind = settings?.experimentKind ?? string.Empty,
                    experimentAttemptId = settings?.experimentAttemptId ?? string.Empty,
                    experimentRunId = settings?.experimentRunId ?? string.Empty
                },
                settings = CloneSettings(settings),
                sceneSnapshot = payload,
                turns = new[] { firstTurn }
            };

            try
            {
                store.CreateSession(detail);
            }
            catch
            {
                try
                {
                    if (store.GetSession(sessionId) == null)
                    {
                        DeleteSessionAssets(sessionId);
                    }
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        $"[SceneTalkVR] Failed to clean up an incomplete history session: {cleanupException.Message}");
                }

                throw;
            }

            Activate(detail);
            return sessionId;
        }

        public string BeginSession(
            SpringScenePayload sceneSnapshot,
            ConversationSettingsSnapshot settings,
            string title,
            string openingAssistantText,
            string initialUserText = null)
        {
            return BeginSession(
                Guid.NewGuid().ToString("N"),
                sceneSnapshot,
                settings,
                title,
                openingAssistantText,
                initialUserText);
        }

        public DialogueTurnRecord AppendTurn(string userText, SpringScenePayload payload)
        {
            EnsureInitialized();
            if (!HasActiveSession)
            {
                throw new InvalidOperationException("No active history session is available for the new turn.");
            }

            var turn = new DialogueTurnRecord
            {
                sequenceIndex = activeNextSequenceIndex,
                isOpening = false,
                createdAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                userText = userText ?? string.Empty,
                assistantText = payload?.dialogueReply ?? string.Empty,
                payload = ClonePayload(payload)
            };
            store.AppendTurn(ActiveSessionId, turn);
            activeNextSequenceIndex++;
            return turn;
        }

        public void UpdateSession(LearningSessionDetail detail)
        {
            EnsureInitialized();
            store.UpdateSession(detail);
            if (detail?.summary != null
                && string.Equals(detail.summary.sessionId, ActiveSessionId, StringComparison.Ordinal))
            {
                ActiveSceneSnapshot = ClonePayload(detail.sceneSnapshot);
                ActiveSettings = CloneSettings(detail.settings);
            }
        }

        public void Activate(LearningSessionDetail detail)
        {
            if (detail?.summary == null || string.IsNullOrWhiteSpace(detail.summary.sessionId))
            {
                throw new ArgumentException("A valid history detail is required.", nameof(detail));
            }

            ActiveSessionId = detail.summary.sessionId;
            ActiveSceneSnapshot = ClonePayload(detail.sceneSnapshot);
            ActiveSettings = CloneSettings(detail.settings);
            activeNextSequenceIndex = detail.turns == null || detail.turns.Length == 0
                ? 0
                : detail.turns.Max(turn => turn.sequenceIndex) + 1;
        }

        public void EndActiveSession()
        {
            ActiveSessionId = string.Empty;
            ActiveSceneSnapshot = null;
            ActiveSettings = null;
            activeNextSequenceIndex = 1;
        }

        public bool DeleteSession(string sessionId)
        {
            EnsureInitialized();
            if (string.Equals(sessionId, ActiveSessionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The active history session cannot be deleted.");
            }

            var existing = store.GetSession(sessionId);
            if (existing?.summary?.IsExperimentConversation == true)
            {
                throw new InvalidOperationException(
                    "Experiment conversations can only be deleted through Experiment History.");
            }

            var deleted = store.DeleteSession(sessionId);
            if (deleted)
            {
                DeleteSessionAssets(sessionId);
            }

            return deleted;
        }

        public string GetSessionAssetsPath(string sessionId)
        {
            if (!IsSafeSessionId(sessionId))
            {
                throw new ArgumentException("A safe session ID is required.", nameof(sessionId));
            }

            return Path.Combine(AssetsRootPath, sessionId.Trim());
        }

        private void EnsureInitialized()
        {
            if (store != null)
            {
                return;
            }

            try
            {
                store = new SqliteLearningMemoryStore(DatabasePath);
                store.Initialize();
                CleanupOrphanedAssets();
                LastError = string.Empty;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                store?.Dispose();
                store = null;
                Debug.LogError($"[SceneTalkVR] Failed to initialize conversation history: {exception}", this);
                throw;
            }
        }

        private void CleanupOrphanedAssets()
        {
            if (!Directory.Exists(AssetsRootPath))
            {
                return;
            }

            var knownIds = new HashSet<string>(store.ListSessionIds(), StringComparer.Ordinal);
            foreach (var directory in Directory.GetDirectories(AssetsRootPath))
            {
                var id = Path.GetFileName(directory);
                if (!knownIds.Contains(id))
                {
                    TryDeleteDirectory(directory);
                }
            }
        }

        private void DeleteSessionAssets(string sessionId)
        {
            if (!IsSafeSessionId(sessionId))
            {
                Debug.LogWarning("[SceneTalkVR] Skipped deleting history assets because the session ID is unsafe.");
                return;
            }

            TryDeleteDirectory(GetSessionAssetsPath(sessionId));
        }

        private static bool IsSafeSessionId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            var value = sessionId.Trim();
            return value != "."
                && value != ".."
                && value.IndexOf('/') < 0
                && value.IndexOf('\\') < 0
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SceneTalkVR] Failed to remove history assets at '{path}': {exception.Message}");
            }
        }

        private static string ResolveTitle(SpringScenePayload payload)
        {
            var raw = payload?.taskType;
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = payload?.environmentType;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Conversation";
            }

            var words = raw.Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }

        public static SpringScenePayload ClonePayload(SpringScenePayload source)
        {
            return source == null
                ? new SpringScenePayload()
                : JsonUtility.FromJson<SpringScenePayload>(JsonUtility.ToJson(source)) ?? new SpringScenePayload();
        }

        public static ConversationSettingsSnapshot CloneSettings(ConversationSettingsSnapshot source)
        {
            return source == null
                ? new ConversationSettingsSnapshot()
                : JsonUtility.FromJson<ConversationSettingsSnapshot>(JsonUtility.ToJson(source))
                  ?? new ConversationSettingsSnapshot();
        }
    }
}
