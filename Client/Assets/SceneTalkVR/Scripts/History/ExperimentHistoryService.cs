using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.History
{
    [DisallowMultipleComponent]
    public sealed class ExperimentHistoryService : MonoBehaviour, IDisposable
    {
        public const int DefaultPageSize = 5;
        public static ExperimentHistoryService Active { get; private set; }

        private IExperimentHistoryStore store;
        private string databasePathOverride;

        public ExperimentConversationLink CurrentConversationLink { get; private set; }
        public string ActiveExperimentId { get; private set; }
        public string LastError { get; private set; }

        private string DatabasePath => string.IsNullOrWhiteSpace(databasePathOverride)
            ? HistoryStoragePaths.DatabasePath
            : databasePathOverride;

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            Dispose();
            if (Active == this) Active = null;
        }

        public void Dispose()
        {
            store?.Dispose();
            store = null;
            ClearRuntimeContext();
        }

        public void ConfigureStoreForTests(IExperimentHistoryStore customStore, string customDatabasePath = null)
        {
            store?.Dispose();
            store = customStore;
            databasePathOverride = customDatabasePath;
            store?.Initialize();
        }

        public ExperimentRecordPage GetPage(int pageIndex, int pageSize = DefaultPageSize)
        {
            EnsureInitialized();
            var safeSize = Mathf.Max(1, pageSize);
            var total = store.CountExperiments();
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(total / (float)safeSize));
            var safeIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            return new ExperimentRecordPage
            {
                pageIndex = safeIndex,
                pageSize = safeSize,
                totalCount = total,
                items = store.ListExperiments(safeIndex * safeSize, safeSize).ToArray()
            };
        }

        public ExperimentRecordDetail GetExperiment(string experimentId)
        {
            EnsureInitialized();
            return store.GetExperiment(experimentId);
        }

        public ExperimentRecordDetail CreateExperiment(string participantId, string experimentId = null)
        {
            EnsureInitialized();
            var id = string.IsNullOrWhiteSpace(experimentId) ? Guid.NewGuid().ToString("N") : experimentId.Trim();
            var participant = string.IsNullOrWhiteSpace(participantId) ? "participant" : participantId.Trim();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var detail = new ExperimentRecordDetail
            {
                summary = new ExperimentRecordSummary
                {
                    experimentId = id,
                    participantId = participant,
                    status = ExperimentRecordStatus.InProgress,
                    pilotStatus = ExperimentPhaseStatus.NotStarted,
                    formalStatus = ExperimentPhaseStatus.NotStarted,
                    createdAtUnixMs = now,
                    updatedAtUnixMs = now
                },
                phases = new[]
                {
                    new ExperimentPhaseRecord
                    {
                        experimentId = id,
                        phase = ExperimentPhaseKind.Pilot,
                        sessionId = id + "-pilot",
                        status = ExperimentPhaseStatus.NotStarted,
                        updatedAtUnixMs = now
                    },
                    new ExperimentPhaseRecord
                    {
                        experimentId = id,
                        phase = ExperimentPhaseKind.Formal,
                        sessionId = id + "-formal",
                        status = ExperimentPhaseStatus.NotStarted,
                        updatedAtUnixMs = now
                    }
                }
            };
            store.CreateExperiment(detail);
            ActivateExperiment(id);
            return detail;
        }

        public void ActivateExperiment(string experimentId)
        {
            if (string.IsNullOrWhiteSpace(experimentId))
                throw new ArgumentException("A valid experiment ID is required.", nameof(experimentId));
            ActiveExperimentId = experimentId.Trim();
            CurrentConversationLink = null;
        }

        public ExperimentAttemptRecord BeginAttempt(
            ExperimentPhaseKind phase,
            string conditionKey,
            string taskId,
            string runId,
            int attemptIndex)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(ActiveExperimentId))
                throw new InvalidOperationException("No active experiment is available.");

            var attempt = new ExperimentAttemptRecord
            {
                attemptId = Guid.NewGuid().ToString("N"),
                experimentId = ActiveExperimentId,
                phase = phase,
                conditionKey = conditionKey ?? string.Empty,
                taskId = taskId ?? string.Empty,
                runId = runId ?? string.Empty,
                attemptIndex = Mathf.Max(1, attemptIndex),
                status = ExperimentAttemptStatus.Running,
                startedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            store.UpsertAttempt(attempt);
            SetPhaseStatus(phase, ExperimentPhaseStatus.InProgress);
            CurrentConversationLink = new ExperimentConversationLink
            {
                experimentId = ActiveExperimentId,
                phase = phase,
                attemptId = attempt.attemptId,
                runId = attempt.runId
            };
            return attempt;
        }

        public void CompleteAttempt(ExperimentAttemptStatus status, string reason)
        {
            if (CurrentConversationLink == null || !CurrentConversationLink.IsValid) return;
            EnsureInitialized();
            var detail = store.GetExperiment(CurrentConversationLink.experimentId);
            var attempt = detail?.attempts?.FirstOrDefault(x => x.attemptId == CurrentConversationLink.attemptId);
            if (attempt == null) return;
            attempt.status = status;
            attempt.completionReason = reason ?? string.Empty;
            attempt.endedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            store.UpsertAttempt(attempt);
            CurrentConversationLink = null;
            TouchExperiment(detail);
        }

        public void SuspendInterruptedRuntime(string reason = "resume_after_interruption")
        {
            if (string.IsNullOrWhiteSpace(ActiveExperimentId)) return;
            EnsureInitialized();
            var detail = store.GetExperiment(ActiveExperimentId);
            if (detail == null) return;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var attempt in detail.attempts ?? Array.Empty<ExperimentAttemptRecord>())
            {
                if (attempt.status != ExperimentAttemptStatus.Running) continue;
                attempt.status = ExperimentAttemptStatus.Suspended;
                attempt.completionReason = reason ?? string.Empty;
                attempt.endedAtUnixMs = now;
                store.UpsertAttempt(attempt);
            }
            foreach (var phase in detail.phases ?? Array.Empty<ExperimentPhaseRecord>())
            {
                if (phase.status != ExperimentPhaseStatus.InProgress) continue;
                phase.status = ExperimentPhaseStatus.Suspended;
                phase.updatedAtUnixMs = now;
                store.UpsertPhase(phase);
                if (phase.phase == ExperimentPhaseKind.Pilot) detail.summary.pilotStatus = phase.status;
                else detail.summary.formalStatus = phase.status;
            }
            detail.summary.updatedAtUnixMs = now;
            store.UpdateExperiment(detail.summary);
            CurrentConversationLink = null;
        }

        public void SetPhaseStatus(ExperimentPhaseKind phase, ExperimentPhaseStatus status, string dataRootPath = null)
        {
            EnsureInitialized();
            var detail = store.GetExperiment(ActiveExperimentId)
                ?? throw new InvalidOperationException("The active experiment was not found.");
            var item = detail.phases.First(x => x.phase == phase);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            item.status = status;
            item.updatedAtUnixMs = now;
            if (!string.IsNullOrWhiteSpace(dataRootPath)) item.dataRootPath = Path.GetFullPath(dataRootPath);
            if (status == ExperimentPhaseStatus.InProgress && item.startedAtUnixMs <= 0) item.startedAtUnixMs = now;
            if (status == ExperimentPhaseStatus.Completed) item.completedAtUnixMs = now;
            store.UpsertPhase(item);

            detail.summary.pilotStatus = phase == ExperimentPhaseKind.Pilot ? status : detail.summary.pilotStatus;
            detail.summary.formalStatus = phase == ExperimentPhaseKind.Formal ? status : detail.summary.formalStatus;
            detail.summary.status = detail.summary.pilotStatus == ExperimentPhaseStatus.Completed
                && detail.summary.formalStatus == ExperimentPhaseStatus.Completed
                ? ExperimentRecordStatus.Completed
                : ExperimentRecordStatus.InProgress;
            detail.summary.updatedAtUnixMs = now;
            store.UpdateExperiment(detail.summary);
        }

        public void RecordQuestionnaire(
            ExperimentPhaseKind phase,
            string attemptId,
            QuestionnaireSession session,
            QuestionnaireCatalog catalog,
            ExperimentV11ProtocolConfig protocol)
        {
            if (session == null || string.IsNullOrWhiteSpace(ActiveExperimentId)) return;
            EnsureInitialized();
            var definition = catalog?.Find(session.questionnaireId);
            var prompts = definition == null
                ? Array.Empty<QuestionnairePromptSnapshot>()
                : catalog.GetEnabledItems(definition.questionnaireId, protocol).Select(item => new QuestionnairePromptSnapshot
                {
                    itemId = item.itemId,
                    sectionId = item.sectionId,
                    promptEnglish = item.promptEnglish,
                    promptChinese = item.promptChinese,
                    scaleMin = item.scaleMin,
                    scaleMax = item.scaleMax
                }).ToArray();
            store.UpsertQuestionnaire(new ExperimentQuestionnaireRecord
            {
                experimentId = ActiveExperimentId,
                phase = phase,
                attemptId = attemptId ?? string.Empty,
                session = Clone(session),
                prompts = prompts
            });
            TouchExperiment();
        }

        public void RecordRanking(ExperimentPhaseKind phase, PreferenceRankingResponse response)
        {
            if (response == null || string.IsNullOrWhiteSpace(ActiveExperimentId)) return;
            EnsureInitialized();
            store.UpsertRanking(new ExperimentRankingRecord
            {
                experimentId = ActiveExperimentId,
                phase = phase,
                response = Clone(response)
            });
            if (phase != ExperimentPhaseKind.Pilot)
            {
                TouchExperiment();
                return;
            }
            var detail = store.GetExperiment(ActiveExperimentId);
            detail.summary.preferredEmbodiment = response.preferredEmbodimentCondition ?? string.Empty;
            detail.summary.updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            store.UpdateExperiment(detail.summary);
        }

        public bool DeleteExperiment(string experimentId, IEnumerable<string> allowedRoots)
        {
            EnsureInitialized();
            if (string.Equals(experimentId, ActiveExperimentId, StringComparison.Ordinal))
                throw new InvalidOperationException("The active experiment cannot be deleted.");
            var detail = store.GetExperiment(experimentId);
            if (detail == null) return false;
            if (!store.DeleteExperiment(experimentId)) return false;

            var roots = (allowedRoots ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Path.GetFullPath)
                .ToArray();
            foreach (var phase in detail.phases ?? Array.Empty<ExperimentPhaseRecord>())
                DeleteOwnedDirectory(phase.dataRootPath, roots);
            var historyAssetsRoot = Path.Combine(HistoryStoragePaths.RootPath, "Assets");
            var historyRoots = roots.Concat(new[] { Path.GetFullPath(historyAssetsRoot) }).ToArray();
            foreach (var conversation in detail.conversations ?? Array.Empty<LearningSessionSummary>())
                DeleteOwnedDirectory(Path.Combine(historyAssetsRoot, conversation.sessionId ?? string.Empty), historyRoots);
            return true;
        }

        public void ClearRuntimeContext()
        {
            ActiveExperimentId = string.Empty;
            CurrentConversationLink = null;
        }

        private void EnsureInitialized()
        {
            if (store != null) return;
            try
            {
                store = new SqliteLearningMemoryStore(DatabasePath);
                store.Initialize();
                LastError = string.Empty;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                store?.Dispose();
                store = null;
                Debug.LogError($"[SceneTalkVR] Failed to initialize experiment history: {exception}", this);
                throw;
            }
        }

        private void TouchExperiment(ExperimentRecordDetail detail = null)
        {
            if (string.IsNullOrWhiteSpace(ActiveExperimentId)) return;
            detail ??= store.GetExperiment(ActiveExperimentId);
            if (detail?.summary == null) return;
            detail.summary.updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            store.UpdateExperiment(detail.summary);
        }

        private static void DeleteOwnedDirectory(string candidate, IReadOnlyCollection<string> allowedRoots)
        {
            if (string.IsNullOrWhiteSpace(candidate) || allowedRoots.Count == 0) return;
            var full = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedRoots = allowedRoots
                .Select(root => root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .ToArray();
            var targetsAnAllowedRoot = normalizedRoots.Any(root => string.Equals(
                full,
                root,
                StringComparison.OrdinalIgnoreCase));
            var safe = !targetsAnAllowedRoot && normalizedRoots.Any(root => full.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
            if (!safe)
            {
                Debug.LogWarning($"[SceneTalkVR] Refused to delete experiment data outside an allowed root: {full}");
                return;
            }
            try { if (Directory.Exists(full)) Directory.Delete(full, true); }
            catch (Exception exception) { Debug.LogWarning($"[SceneTalkVR] Failed to delete '{full}': {exception.Message}"); }
        }

        private static T Clone<T>(T value) where T : class, new()
        {
            return value == null ? new T() : JsonUtility.FromJson<T>(JsonUtility.ToJson(value)) ?? new T();
        }
    }
}
