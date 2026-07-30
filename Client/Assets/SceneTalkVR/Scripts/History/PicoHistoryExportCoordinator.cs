using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SceneTalkVR.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneTalkVR.History
{
    public enum PicoHistoryExportState
    {
        Idle,
        ProbingUsb,
        BuildingSnapshot,
        Uploading,
        Succeeded,
        Failed
    }

    [Serializable]
    public sealed class PicoHistoryExportWarning
    {
        public string code;
        public string experimentId;
        public string conversationSessionId;
        public string message;
    }

    [Serializable]
    public sealed class PicoHistoryExportQuestionnaireItemDefinition
    {
        public string questionnaireId;
        public string sectionId;
        public string itemId;
        public string itemVersion;
        public int displayOrder;
        public string promptEnglish;
        public string promptChinese;
        public int itemType;
        public string[] choiceValues = Array.Empty<string>();
    }

    [Serializable]
    public sealed class PicoHistoryExportQuestionnaireDefinition
    {
        public string questionnaireId;
        public string questionnaireVersion;
        public string questionnaireCatalogVersion;
        public PicoHistoryExportQuestionnaireItemDefinition[] items =
            Array.Empty<PicoHistoryExportQuestionnaireItemDefinition>();
    }

    [Serializable]
    public sealed class PicoHistoryExportExperiment
    {
        public ExperimentRecordSummary summary = new ExperimentRecordSummary();
        public string experimentKind;
        public string experimentStatus;
        public ExperimentAttemptRecord[] attempts = Array.Empty<ExperimentAttemptRecord>();
        public LearningSessionDetail[] conversations = Array.Empty<LearningSessionDetail>();
        public ExperimentQuestionnaireRecord[] questionnaires = Array.Empty<ExperimentQuestionnaireRecord>();
        public ExperimentRankingRecord[] rankings = Array.Empty<ExperimentRankingRecord>();
        public string[] missingConversationSessionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class PicoHistoryExportRequest
    {
        public string schemaVersion = "1.0";
        public string exportId;
        public string exportedAtUtc;
        public string sortOrder = "chronological_ascending";
        public string applicationVersion;
        public string unityVersion;
        public string sourcePlatform;
        public string deviceModel;
        public int experimentCount;
        public int questionnaireCount;
        public int conversationCount;
        public PicoHistoryExportQuestionnaireDefinition[] questionnaireDefinitions =
            Array.Empty<PicoHistoryExportQuestionnaireDefinition>();
        public PicoHistoryExportWarning[] warnings = Array.Empty<PicoHistoryExportWarning>();
        public PicoHistoryExportExperiment[] experiments = Array.Empty<PicoHistoryExportExperiment>();
    }

    [Serializable]
    public sealed class PicoHistoryExportResult
    {
        public bool success;
        public string status;
        public string exportId;
        public string exportDirectory;
        public string jsonFile;
        public string excelFile;
        public int experimentCount;
        public int questionnaireCount;
        public int responseCount;
        public int warningCount;
        public string jsonSha256;
        public string excelSha256;
        public string errorCode;
        public string message;
    }

    public static class PicoHistoryExportSnapshotBuilder
    {
        public static PicoHistoryExportRequest Build(
            IEnumerable<ExperimentRecordDetail> source,
            Func<string, LearningSessionDetail> conversationResolver,
            string exportId,
            string exportedAtUtc,
            string applicationVersion,
            string unityVersion,
            string sourcePlatform,
            string deviceModel,
            IEnumerable<PicoHistoryExportQuestionnaireDefinition> questionnaireDefinitions = null)
        {
            var warnings = new List<PicoHistoryExportWarning>();
            var experiments = (source ?? Array.Empty<ExperimentRecordDetail>())
                .Where(detail => detail?.summary != null)
                .OrderBy(detail => detail.summary.createdAtUnixMs)
                .ThenBy(detail => detail.summary.experimentId, StringComparer.Ordinal)
                .Select(detail => BuildExperiment(detail, conversationResolver, warnings))
                .ToArray();

            return new PicoHistoryExportRequest
            {
                exportId = exportId ?? string.Empty,
                exportedAtUtc = exportedAtUtc ?? string.Empty,
                applicationVersion = applicationVersion ?? string.Empty,
                unityVersion = unityVersion ?? string.Empty,
                sourcePlatform = sourcePlatform ?? string.Empty,
                deviceModel = deviceModel ?? string.Empty,
                experimentCount = experiments.Length,
                questionnaireCount = experiments.Sum(item => item.questionnaires?.Length ?? 0),
                conversationCount = experiments.Sum(item => item.conversations?.Length ?? 0),
                questionnaireDefinitions = (questionnaireDefinitions
                        ?? Array.Empty<PicoHistoryExportQuestionnaireDefinition>())
                    .Where(value => value != null && !string.IsNullOrWhiteSpace(value.questionnaireId))
                    .OrderBy(value => value.questionnaireId, StringComparer.Ordinal)
                    .Select(Clone)
                    .ToArray(),
                warnings = warnings.ToArray(),
                experiments = experiments
            };
        }

        public static PicoHistoryExportQuestionnaireDefinition[] BuildQuestionnaireDefinitions(
            QuestionnaireCatalog catalog,
            ExperimentV11ProtocolConfig protocol)
        {
            if (catalog == null) return Array.Empty<PicoHistoryExportQuestionnaireDefinition>();

            var snapshots = new List<PicoHistoryExportQuestionnaireDefinition>();
            foreach (var questionnaireId in new[] { "formal_condition_v1", "formal_final_v1" })
            {
                var definition = catalog.Find(questionnaireId);
                if (definition == null || !definition.enabled) continue;

                var items = new List<PicoHistoryExportQuestionnaireItemDefinition>();
                foreach (var section in (definition.sections ?? Array.Empty<QuestionnaireSection>())
                             .Where(value => value != null)
                             .OrderBy(value => value.displayOrder))
                {
                    foreach (var item in (section.items ?? Array.Empty<QuestionnaireItem>())
                                 .Where(value => value != null && catalog.IsEnabledByProtocol(value, protocol))
                                 .OrderBy(value => value.displayOrder))
                    {
                        items.Add(new PicoHistoryExportQuestionnaireItemDefinition
                        {
                            questionnaireId = definition.questionnaireId ?? string.Empty,
                            sectionId = item.sectionId ?? section.sectionId ?? string.Empty,
                            itemId = item.itemId ?? string.Empty,
                            itemVersion = item.itemVersion ?? string.Empty,
                            displayOrder = items.Count,
                            promptEnglish = item.promptEnglish ?? string.Empty,
                            promptChinese = item.promptChinese ?? string.Empty,
                            itemType = (int)item.itemType,
                            choiceValues = (item.choiceValues ?? Array.Empty<string>()).ToArray()
                        });
                    }
                }

                snapshots.Add(new PicoHistoryExportQuestionnaireDefinition
                {
                    questionnaireId = definition.questionnaireId ?? string.Empty,
                    questionnaireVersion = definition.questionnaireVersion ?? string.Empty,
                    questionnaireCatalogVersion = catalog.CatalogVersion,
                    items = items.ToArray()
                });
            }

            return snapshots.ToArray();
        }

        private static PicoHistoryExportExperiment BuildExperiment(
            ExperimentRecordDetail detail,
            Func<string, LearningSessionDetail> conversationResolver,
            ICollection<PicoHistoryExportWarning> warnings)
        {
            var conversations = new List<LearningSessionDetail>();
            var missing = new List<string>();
            foreach (var summary in (detail.conversations ?? Array.Empty<LearningSessionSummary>())
                         .Where(value => value != null && !string.IsNullOrWhiteSpace(value.sessionId))
                         .OrderBy(value => value.createdAtUnixMs)
                         .ThenBy(value => value.sessionId, StringComparer.Ordinal))
            {
                LearningSessionDetail resolved = null;
                try
                {
                    resolved = conversationResolver?.Invoke(summary.sessionId);
                }
                catch (Exception exception)
                {
                    warnings.Add(new PicoHistoryExportWarning
                    {
                        code = "conversation_read_failed",
                        experimentId = detail.summary.experimentId,
                        conversationSessionId = summary.sessionId,
                        message = exception.Message
                    });
                }

                if (resolved == null)
                {
                    missing.Add(summary.sessionId);
                    if (!warnings.Any(value => string.Equals(
                            value.conversationSessionId,
                            summary.sessionId,
                            StringComparison.Ordinal)
                        && string.Equals(value.experimentId, detail.summary.experimentId, StringComparison.Ordinal)))
                    {
                        warnings.Add(new PicoHistoryExportWarning
                        {
                            code = "conversation_detail_missing",
                            experimentId = detail.summary.experimentId,
                            conversationSessionId = summary.sessionId,
                            message = "The experiment references a conversation that is not available in history storage."
                        });
                    }
                    continue;
                }

                var clone = Clone(resolved);
                clone.turns = (clone.turns ?? Array.Empty<DialogueTurnRecord>())
                    .OrderBy(turn => turn.createdAtUnixMs)
                    .ThenBy(turn => turn.sequenceIndex)
                    .ToArray();
                conversations.Add(clone);
            }

            var questionnaires = (detail.questionnaires ?? Array.Empty<ExperimentQuestionnaireRecord>())
                .Where(value => value != null)
                .Select(CloneQuestionnaire)
                .OrderBy(value => value.session?.startedAtUtc ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.questionnaireRecordId, StringComparer.Ordinal)
                .ToArray();

            return new PicoHistoryExportExperiment
            {
                summary = Clone(detail.summary),
                experimentKind = detail.summary.kind.ToString(),
                experimentStatus = detail.summary.status.ToString(),
                attempts = (detail.attempts ?? Array.Empty<ExperimentAttemptRecord>())
                    .Where(value => value != null)
                    .OrderBy(value => value.startedAtUnixMs)
                    .ThenBy(value => value.attemptIndex)
                    .ThenBy(value => value.attemptId, StringComparer.Ordinal)
                    .Select(value => Clone(value))
                    .ToArray(),
                conversations = conversations
                    .OrderBy(value => value.summary?.createdAtUnixMs ?? 0L)
                    .ThenBy(value => value.summary?.sessionId, StringComparer.Ordinal)
                    .ToArray(),
                questionnaires = questionnaires,
                rankings = (detail.rankings ?? Array.Empty<ExperimentRankingRecord>())
                    .Where(value => value != null)
                    .OrderBy(value => value.response?.submittedAtUtc ?? string.Empty, StringComparer.Ordinal)
                    .Select(value => Clone(value))
                    .ToArray(),
                missingConversationSessionIds = missing.ToArray()
            };
        }

        private static ExperimentQuestionnaireRecord CloneQuestionnaire(ExperimentQuestionnaireRecord source)
        {
            var clone = Clone(source);
            clone.session ??= new QuestionnaireSession();
            clone.session.responses = (clone.session.responses ?? Array.Empty<QuestionnaireResponse>())
                .OrderBy(value => value.responseCapturedAtUtc ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.itemId, StringComparer.Ordinal)
                .ToArray();
            clone.session.sectionScores = (clone.session.sectionScores ?? Array.Empty<QuestionnaireScoreResult>())
                .OrderBy(value => value.sectionId, StringComparer.Ordinal)
                .ToArray();
            return clone;
        }

        private static T Clone<T>(T value) where T : class, new()
        {
            return value == null
                ? new T()
                : JsonUtility.FromJson<T>(JsonUtility.ToJson(value)) ?? new T();
        }
    }

    [DisallowMultipleComponent]
    public sealed class PicoHistoryExportCoordinator : MonoBehaviour
    {
        private const string DefaultBaseUrl = "http://127.0.0.1:8789";

        [Serializable]
        private sealed class HealthResponse
        {
            public string status;
            public string service;
            public string schemaVersion;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string errorCode;
            public string message;
        }

        private SceneTalkRuntimeConfig runtimeConfig;
        private ExperimentHistoryService experimentHistory;
        private LearningMemoryService learningMemory;
        private ExperimentConditionManager conditionManager;
        private Coroutine activeExport;
        private UnityWebRequest activeRequest;

        public PicoHistoryExportState State { get; private set; } = PicoHistoryExportState.Idle;
        public PicoHistoryExportResult LastResult { get; private set; }
        public bool IsBusy => State == PicoHistoryExportState.ProbingUsb
            || State == PicoHistoryExportState.BuildingSnapshot
            || State == PicoHistoryExportState.Uploading;

        public event Action<PicoHistoryExportState, PicoHistoryExportResult> StateChanged;

        public void Configure(
            SceneTalkRuntimeConfig config,
            ExperimentHistoryService historyService,
            LearningMemoryService memoryService)
        {
            runtimeConfig = config;
            experimentHistory = historyService;
            learningMemory = memoryService;
        }

        public bool TryStartExport(out string error)
        {
            if (IsBusy)
            {
                error = "history_export_in_progress";
                return false;
            }

            ResolveDependencies();
            if (experimentHistory == null || learningMemory == null)
            {
                error = "history_service_unavailable";
                SetFailure(error, "The history service is unavailable.");
                return false;
            }

            var baseUrl = ResolveBaseUrl();
            if (!SceneTalkRuntimeConfig.IsLoopbackUrl(baseUrl))
            {
                error = "history_export_usb_endpoint_invalid";
                SetFailure(error, "History export must use the USB loopback endpoint.");
                return false;
            }

            LastResult = null;
            activeExport = StartCoroutine(ExportRoutine(baseUrl));
            error = string.Empty;
            return true;
        }

        public void ClearCompletedStatus()
        {
            if (IsBusy || State == PicoHistoryExportState.Idle) return;
            LastResult = null;
            SetState(PicoHistoryExportState.Idle);
        }

        private IEnumerator ExportRoutine(string baseUrl)
        {
            SetState(PicoHistoryExportState.ProbingUsb);
            using (var probe = UnityWebRequest.Get(baseUrl + "/health"))
            {
                activeRequest = probe;
                probe.timeout = runtimeConfig == null ? 3 : runtimeConfig.GatewayProbeTimeoutSeconds;
                probe.SetRequestHeader("Accept", "application/json");
                yield return probe.SendWebRequest();
                activeRequest = null;
                if (probe.result != UnityWebRequest.Result.Success || probe.responseCode != 200)
                {
                    SetFailure(
                        "history_export_usb_unavailable",
                        "The PC history export service is not reachable through the USB cable.");
                    activeExport = null;
                    yield break;
                }

                HealthResponse health;
                try
                {
                    health = JsonUtility.FromJson<HealthResponse>(probe.downloadHandler.text);
                }
                catch (Exception)
                {
                    health = null;
                }
                if (health == null
                    || health.status != "ok"
                    || health.service != "history-export"
                    || health.schemaVersion != "1.0")
                {
                    SetFailure("history_export_service_incompatible", "The PC export service is incompatible.");
                    activeExport = null;
                    yield break;
                }
            }

            SetState(PicoHistoryExportState.BuildingSnapshot);
            yield return null;

            PicoHistoryExportRequest snapshot;
            try
            {
                var experiments = experimentHistory.GetAllExperimentsChronological();
                if (experiments.Length == 0)
                {
                    SetFailure("history_export_empty", "There is no experiment history to export.");
                    activeExport = null;
                    yield break;
                }

                snapshot = PicoHistoryExportSnapshotBuilder.Build(
                    experiments,
                    learningMemory.GetSession,
                    Guid.NewGuid().ToString("N"),
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    Application.version,
                    Application.unityVersion,
                    Application.platform.ToString(),
                    SystemInfo.deviceModel,
                    PicoHistoryExportSnapshotBuilder.BuildQuestionnaireDefinitions(
                        conditionManager?.QuestionnaireCatalog,
                        conditionManager?.ExperimentProtocol));
            }
            catch (Exception exception)
            {
                SetFailure("history_export_snapshot_failed", exception.Message);
                activeExport = null;
                yield break;
            }

            byte[] body;
            try
            {
                body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
            }
            catch (Exception exception)
            {
                SetFailure("history_export_serialization_failed", exception.Message);
                activeExport = null;
                yield break;
            }

            SetState(PicoHistoryExportState.Uploading);
            using (var upload = new UnityWebRequest(baseUrl + "/api/history/export", UnityWebRequest.kHttpVerbPOST))
            {
                activeRequest = upload;
                upload.uploadHandler = new UploadHandlerRaw(body);
                upload.downloadHandler = new DownloadHandlerBuffer();
                upload.timeout = runtimeConfig == null ? 120 : runtimeConfig.HistoryExportTimeoutSeconds;
                upload.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                upload.SetRequestHeader("Accept", "application/json");
                yield return upload.SendWebRequest();
                activeRequest = null;

                if (upload.result != UnityWebRequest.Result.Success || upload.responseCode < 200 || upload.responseCode >= 300)
                {
                    var serverError = ParseError(upload.downloadHandler?.text);
                    SetFailure(
                        string.IsNullOrWhiteSpace(serverError?.errorCode)
                            ? "history_export_upload_failed"
                            : serverError.errorCode,
                        string.IsNullOrWhiteSpace(serverError?.message)
                            ? upload.error ?? "The history export upload failed."
                            : serverError.message);
                    activeExport = null;
                    yield break;
                }

                PicoHistoryExportResult result;
                try
                {
                    result = JsonUtility.FromJson<PicoHistoryExportResult>(upload.downloadHandler.text);
                }
                catch (Exception)
                {
                    result = null;
                }
                if (result == null
                    || result.status != "ok"
                    || !string.Equals(result.exportId, snapshot.exportId, StringComparison.Ordinal))
                {
                    SetFailure("history_export_response_invalid", "The PC returned an invalid export result.");
                    activeExport = null;
                    yield break;
                }

                result.success = true;
                LastResult = result;
                SetState(PicoHistoryExportState.Succeeded);
            }

            activeExport = null;
        }

        private string ResolveBaseUrl()
        {
            var configured = runtimeConfig?.UsbHistoryExportBaseUrl;
            return string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : configured;
        }

        private void ResolveDependencies()
        {
            experimentHistory ??= GetComponent<ExperimentHistoryService>()
                ?? FindFirstObjectByType<ExperimentHistoryService>(FindObjectsInactive.Include);
            learningMemory ??= GetComponent<LearningMemoryService>()
                ?? FindFirstObjectByType<LearningMemoryService>(FindObjectsInactive.Include);
            conditionManager ??= GetComponent<ExperimentConditionManager>()
                ?? FindFirstObjectByType<ExperimentConditionManager>(FindObjectsInactive.Include);
        }

        private static ErrorResponse ParseError(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<ErrorResponse>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SetFailure(string code, string message)
        {
            LastResult = new PicoHistoryExportResult
            {
                success = false,
                status = "error",
                errorCode = code ?? "history_export_failed",
                message = string.IsNullOrWhiteSpace(message) ? "History export failed." : message
            };
            SetState(PicoHistoryExportState.Failed);
        }

        private void SetState(PicoHistoryExportState state)
        {
            State = state;
            StateChanged?.Invoke(State, LastResult);
        }

        private void OnDestroy()
        {
            if (activeRequest != null && !activeRequest.isDone)
            {
                activeRequest.Abort();
            }
            activeRequest = null;
            if (activeExport != null)
            {
                StopCoroutine(activeExport);
                activeExport = null;
            }
        }
    }
}
