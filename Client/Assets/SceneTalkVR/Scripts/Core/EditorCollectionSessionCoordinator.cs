using System;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class EditorCollectionOperatorEvent
    {
        public string schemaVersion = "1.2";
        public string timestampUtc;
        public string eventType;
        public string participantId;
        public string sessionId;
        public string protocolVersion;
        public string protocolSnapshotId;
        public string resourceSnapshotId;
        public string flowMode = "formal";
        public string runQualification = "collection";
        public string dataOrigin = "participant_collection";
        public bool collectionEligible = true;
        public bool developerTestAssignment;
        public bool demoMode;
        public bool synthetic;
        public string deploymentProfile = "editor_collection";
        public bool qaAutomationUsed;
        public string actor = "experiment_operator";
        public string detail;
    }

    [Serializable]
    public sealed class EditorCollectionGoalSnapshot
    {
        public string schemaVersion = "1.0";
        public string participantId;
        public string sessionId;
        public string conditionRunId;
        public string taskId;
        public string savedAtUtc;
        public GoalProgressRecord[] goals = Array.Empty<GoalProgressRecord>();
    }

    [DisallowMultipleComponent]
    public sealed class EditorCollectionSessionCoordinator : MonoBehaviour
    {
        public static EditorCollectionSessionCoordinator Active { get; private set; }

        [SerializeField] private ExperimentV11ProtocolConfig protocol;
        [SerializeField] private EditorCollectionResourceCatalog resources;
        [SerializeField] private ExperimentVoiceProfileCatalog voiceCatalog;
        [SerializeField] private ExperimentDeploymentCatalog deploymentCatalog;
        [SerializeField] private QuestionnaireCatalog questionnaireCatalog;
        [SerializeField] private ExperimentTaskCatalog taskCatalog;

        private ExperimentConditionManager conditionManager;
        private ExperimentLifecycleCoordinator lifecycle;
        private QuestionnaireRuntimeController questionnaire;
        private SceneTalkOrchestrator orchestrator;
        private int currentPosition = -1;
        private bool participantStarted;
        private bool finalRankingVisible;
        private bool rankingSubmitted;
        private bool experimentCompleted;
        private bool subscribed;
        private string lastBundlePath;

        public ExperimentRuntimeContext RuntimeContext { get; private set; }
        public bool IsArmed => RuntimeContext != null && RuntimeContext.IsCollection && RuntimeContext.flowMode == ExperimentFlowMode.Formal;
        public bool ParticipantStarted => participantStarted;
        public bool AwaitingParticipantConditionChoice => IsArmed && participantStarted && currentPosition < 0
            && !finalRankingVisible && !experimentCompleted && Assignment?.status != AssignmentStatus.Completed;
        public bool FinalRankingVisible => finalRankingVisible;
        public bool ExperimentCompleted => experimentCompleted;
        public ExperimentAssignment Assignment => lifecycle?.Assignment;
        public ExperimentV11ProtocolConfig Protocol => protocol;
        public EditorCollectionResourceCatalog Resources => resources;
        public int CurrentPosition => currentPosition;
        public string CurrentTaskId => currentPosition < 0 ? string.Empty : lifecycle?.CurrentConditionAssignment?.task?.taskId ?? string.Empty;
        public string CurrentRunId => lifecycle?.ConditionRunId ?? string.Empty;
        public string ParticipantId => RuntimeContext?.participantId ?? string.Empty;
        public string SessionId => RuntimeContext?.sessionId ?? string.Empty;
        public string LastBundlePath => lastBundlePath ?? string.Empty;
        public static string CollectionRoot => Path.Combine(Application.persistentDataPath, "SceneTalkVR", "EditorCollectionSessions");
        public string CurrentDataFolder => string.IsNullOrWhiteSpace(ParticipantId) || string.IsNullOrWhiteSpace(SessionId)
            ? CollectionRoot : Path.Combine(CollectionRoot, Safe(ParticipantId) + "_" + Safe(SessionId), "raw");

        private void Awake()
        {
            Active = this;
            ResolveDependencies();
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Active == this) Active = null;
        }

        public void Configure(ExperimentV11ProtocolConfig officialProtocol,
            EditorCollectionResourceCatalog collectionResources, ExperimentVoiceProfileCatalog voices,
            ExperimentDeploymentCatalog deployments, QuestionnaireCatalog questionnaires,
            ExperimentTaskCatalog tasks)
        {
            protocol = officialProtocol;
            resources = collectionResources;
            voiceCatalog = voices;
            deploymentCatalog = deployments;
            questionnaireCatalog = questionnaires;
            taskCatalog = tasks;
            ResolveDependencies();
            Subscribe();
        }

        public bool ArmParticipantSession(string participantId, string sessionId, bool resume, out string error)
        {
            error = string.Empty;
            ResolveDependencies();
            if (!Application.isEditor) { error = "editor_collection_requires_unity_editor"; return false; }
            if (RehearsalSessionCoordinator.Active?.IsActive == true || EditorDemoSessionCoordinator.Active?.IsDemoMode == true)
            { error = "qa_session_must_be_closed_before_collection"; return false; }
            if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(sessionId))
            { error = "participant_and_session_required"; return false; }
            if (conditionManager == null || lifecycle == null || questionnaire == null || orchestrator == null)
            { error = "editor_collection_scene_bindings_missing"; return false; }
            conditionManager.EnterEditorCollectionMode(protocol, taskCatalog, questionnaireCatalog, voiceCatalog, deploymentCatalog);
            questionnaire.Configure(conditionManager, lifecycle);
            if (protocol == null || !protocol.ValidateForFormalMode(out error)) return false;
            if (resources == null || !resources.Validate(taskCatalog, voiceCatalog, deploymentCatalog, out error)) return false;
            if (!conditionManager.ValidateFormalProtocol(out error)) return false;
            if (!TryApplyConfirmedRunLimits(out error)) return false;

            ResetRuntimeOnly();
            RuntimeContext = ExperimentRuntimeContext.CreateEditorCollection(participantId, sessionId,
                protocol.ProtocolSnapshotId, resources.ResourceSnapshotId);
            Directory.CreateDirectory(CurrentDataFolder);
            var path = AssignmentPath;
            var stored = ExperimentAssignmentAllocator.Load(path);
            if (resume)
            {
                if (stored == null) { error = "editor_collection_assignment_missing"; return FailArm(); }
                if (!ValidateCollectionAssignment(stored, out error) || !lifecycle.LoadAssignment(stored, out error)) return FailArm();
                currentPosition = Array.FindIndex(stored.conditions, x => x.status == ConditionRunStatus.Running
                    || x.status == ConditionRunStatus.AwaitingQuestionnaire || x.status == ConditionRunStatus.QuestionnaireInProgress);
            }
            else
            {
                if (stored != null) { error = "session_already_exists_use_resume"; return FailArm(); }
                var allocator = new ExperimentAssignmentAllocator();
                if (!allocator.TryCreateEditorCollection(ParticipantId, SessionId, protocol, taskCatalog,
                    resources.ResourceSnapshotId, out var created, out error) || !lifecycle.LoadAssignment(created, out error)) return FailArm();
                currentPosition = -1;
                PersistAssignment();
            }
            participantStarted = false;
            finalRankingVisible = Assignment?.status == AssignmentStatus.Completed && !rankingSubmitted;
            experimentCompleted = false;
            WriteOperator(StudyEventType.ParticipantSessionArmed.ToString(), "resume=" + resume);
            lifecycle.RecordStudyEvent(StudyEventType.ParticipantSessionArmed, "experiment_operator", "deploymentProfile=editor_collection");
            RefreshUi();
            error = string.Empty;
            return true;
        }

        public bool BeginParticipantFlow(out string error)
        {
            if (!IsArmed) { error = "participant_session_not_armed"; return false; }
            participantStarted = true;
            if (currentPosition >= 0)
            {
                var goals = LoadGoalSnapshot(Assignment.conditions[currentPosition].latestConditionRunId)?.goals;
                if (!lifecycle.ResumeCondition(currentPosition, goals, out error)) return false;
                if (lifecycle.CurrentConditionAssignment.status == ConditionRunStatus.QuestionnaireInProgress)
                    questionnaire.RestoreCurrentDraft(out _);
            }
            else if (Assignment.status == AssignmentStatus.Completed)
            {
                ShowFinalRanking();
            }
            else
            {
                lifecycle.RecordStudyEvent(StudyEventType.FormalModeSelectionShown, "participant");
            }
            RefreshUi();
            error = string.Empty;
            return true;
        }

        public bool SelectFormalCondition(FormalConditionCode code, out string error)
        {
            error = string.Empty;
            if (!AwaitingParticipantConditionChoice) { error = "formal_mode_selection_not_available"; return false; }
            var items = Assignment?.conditions;
            var position = items == null ? -1 : Array.FindIndex(items, x => x.formalConditionCode == code);
            if (position < 0) { error = "formal_condition_not_assigned"; return false; }
            var selected = items[position];
            if (selected.status == ConditionRunStatus.Completed || selected.status == ConditionRunStatus.QuestionnaireSubmitted)
            { error = "formal_condition_already_completed"; return false; }
            if (selected.status != ConditionRunStatus.Assigned && selected.status != ConditionRunStatus.TechnicalInvalid)
            { error = "formal_condition_not_selectable:" + selected.status; return false; }
            var retry = selected.status == ConditionRunStatus.TechnicalInvalid;
            currentPosition = position;
            if (!lifecycle.PrepareCondition(position, retry, out error)) { currentPosition = -1; return false; }
            var order = Assignment.participantSelectionOrder?.ToList() ?? new System.Collections.Generic.List<FormalConditionCode>();
            if (!order.Contains(code)) order.Add(code);
            Assignment.participantSelectionOrder = order.ToArray();
            selected.participantSelectionPosition = order.IndexOf(code);
            selected.selectedAtUtc = DateTime.UtcNow.ToString("o");
            lifecycle.RecordStudyEvent(StudyEventType.FormalModeSelected, "participant", "code=" + code);
            lifecycle.RecordStudyEvent(StudyEventType.ConditionTaskResolved, "system", "taskId=" + selected.task.taskId);
            PersistAssignment();
            WriteOperator("FormalModeSelected", $"code={code};task={selected.task.taskId};retry={retry}");
            RefreshUi();
            return true;
        }

        public bool IsTaskPrepared(string taskId) => IsArmed
            && string.Equals(CurrentTaskId, taskId, StringComparison.OrdinalIgnoreCase)
            && lifecycle?.CurrentConditionAssignment?.status == ConditionRunStatus.Running;

        public string ResolveFormalAvatarKey(string taskId) => resources?.FindAvatar(taskId)?.requestedPresetKey ?? string.Empty;

        public void MarkTechnicalInvalid(string reason)
        {
            if (!IsArmed) return;
            lifecycle.MarkTechnicalInvalid(reason);
            PersistAssignment();
            WriteOperator("ConditionTechnicalInvalid", reason);
            RefreshUi();
        }

        public bool ConfirmGoalByExperimenter(string goalId, string experimenterId, string note, out string error)
        {
            error = "editor_collection_lifecycle_missing";
            var ok = lifecycle != null && lifecycle.ConfirmGoalByExperimenter(goalId, experimenterId, note, out error);
            if (ok) { PersistGoalSnapshot(); WriteOperator("GoalConfirmedByExperimenter", $"goalId={goalId};note={note}"); }
            return ok;
        }

        public bool RejectGoalByExperimenter(string goalId, string experimenterId, string reason, out string error)
        {
            error = "editor_collection_lifecycle_missing";
            var ok = lifecycle != null && lifecycle.RejectGoalByExperimenter(goalId, experimenterId, reason, out error);
            if (ok) { PersistGoalSnapshot(); WriteOperator("GoalRejectedByExperimenter", $"goalId={goalId};reason={reason}"); }
            return ok;
        }

        public bool UndoGoalByExperimenter(string goalId, string experimenterId, string reason, out string error)
        {
            error = "editor_collection_lifecycle_missing";
            var ok = lifecycle != null && lifecycle.UndoGoalByExperimenter(goalId, experimenterId, reason, out error);
            if (ok) { PersistGoalSnapshot(); WriteOperator("GoalUndoneByExperimenter", $"goalId={goalId};reason={reason}"); }
            return ok;
        }

        public void MarkQaAutomationUsed(string detail)
        {
            if (!IsArmed || Assignment == null) return;
            Assignment.qaAutomationUsed = true;
            Assignment.collectionEligible = false;
            PersistAssignment();
            WriteOperator("QaAutomationUsed", detail, true);
        }

        public bool SubmitFinalRanking(PreferenceRankingResponse response, out string error)
        {
            if (!finalRankingVisible || Assignment?.status != AssignmentStatus.Completed)
            { error = "final_ranking_not_available"; return false; }
            response.protocolVersion = protocol.ProtocolVersion;
            response.questionnaireCatalogVersion = questionnaireCatalog.CatalogVersion;
            response.participantId = ParticipantId;
            response.sessionId = SessionId;
            response.sequenceId = Assignment.sequenceId;
            response.questionnaireId = "formal_final_v1";
            response.submittedAtUtc = DateTime.UtcNow.ToString("o");
            if (!questionnaire.SubmitFormalRanking(response, out error)) return false;
            rankingSubmitted = true;
            finalRankingVisible = false;
            experimentCompleted = true;
            lifecycle.RecordStudyEvent(StudyEventType.ExperimentCompleted, "participant");
            WriteOperator("ExperimentCompleted");
            RefreshUi();
            return true;
        }

        public bool ExportBundle(out string error)
        {
            var ok = EditorCollectionBundleExporter.Export(Path.GetDirectoryName(CurrentDataFolder), Assignment,
                protocol, resources, rankingSubmitted, out lastBundlePath, out error);
            if (ok) WriteOperator("BundleExported", lastBundlePath);
            return ok;
        }

        public bool AuditBundle(out string error)
        {
            if (string.IsNullOrWhiteSpace(lastBundlePath) || !Directory.Exists(lastBundlePath))
            { error = "collection_bundle_missing"; return false; }
            var report = SessionDataIntegrityAuditor.Audit(lastBundlePath, ParticipantId, SessionId);
            SessionDataIntegrityAuditor.WriteReport(report, lastBundlePath + "-manual-audit.json");
            error = report.result.ToString().ToUpperInvariant();
            return report.result != DataIntegritySeverity.Fail;
        }

        public void EndRuntimeSession()
        {
            WriteOperator("ParticipantSessionDisarmed");
            ResetRuntimeOnly();
            RuntimeContext = null;
            RefreshUi();
        }

        private void OnQuestionnaireRequested()
        {
            if (!IsArmed) return;
            if (!questionnaire.StartCurrentConditionQuestionnaire(out var error))
            {
                Debug.LogError("[EditorCollection] Automatic questionnaire start failed: " + error, this);
                MarkTechnicalInvalid("questionnaire_start_failed:" + error);
                return;
            }
            lifecycle.RecordStudyEvent(StudyEventType.QuestionnaireOpened, "system");
            PersistAssignment();
            RefreshUi();
        }

        private void OnQuestionnaireSubmitted()
        {
            if (!IsArmed) return;
            PersistGoalSnapshot();
            PersistAssignment();
            orchestrator.ResetForConditionSelection();
            lifecycle.ClearCurrentConditionBoundary();
            currentPosition = -1;
            lifecycle.RecordStudyEvent(StudyEventType.ReturnedToModeSelection, "system");
            if (Assignment.status == AssignmentStatus.Completed) ShowFinalRanking();
            else lifecycle.RecordStudyEvent(StudyEventType.FormalModeSelectionShown, "system");
            RefreshUi();
        }

        private void ShowFinalRanking()
        {
            finalRankingVisible = true;
            lifecycle.RecordStudyEvent(StudyEventType.FinalRankingOpened, "system");
            RefreshUi();
        }

        private void ResolveDependencies()
        {
            conditionManager ??= GetComponent<ExperimentConditionManager>() ?? FindFirstObjectByType<ExperimentConditionManager>();
            lifecycle ??= GetComponent<ExperimentLifecycleCoordinator>() ?? FindFirstObjectByType<ExperimentLifecycleCoordinator>();
            questionnaire ??= GetComponent<QuestionnaireRuntimeController>() ?? FindFirstObjectByType<QuestionnaireRuntimeController>();
            orchestrator ??= GetComponent<SceneTalkOrchestrator>() ?? FindFirstObjectByType<SceneTalkOrchestrator>();
        }

        private void Subscribe()
        {
            if (subscribed || lifecycle == null) return;
            lifecycle.QuestionnaireRequested += OnQuestionnaireRequested;
            lifecycle.QuestionnaireSubmitted += OnQuestionnaireSubmitted;
            lifecycle.GoalTracker.OnGoalProgressChanged += OnGoalProgressChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || lifecycle == null) return;
            lifecycle.QuestionnaireRequested -= OnQuestionnaireRequested;
            lifecycle.QuestionnaireSubmitted -= OnQuestionnaireSubmitted;
            lifecycle.GoalTracker.OnGoalProgressChanged -= OnGoalProgressChanged;
            subscribed = false;
        }

        private void OnGoalProgressChanged(GoalProgressChangedEvent value)
        {
            if (!IsArmed || value == null || value.conditionRunId != CurrentRunId) return;
            PersistGoalSnapshot();
        }

        private string AssignmentPath => Path.Combine(CurrentDataFolder, "formal_assignment.json");
        private void PersistAssignment()
        {
            if (IsArmed && Assignment != null) ExperimentAssignmentAllocator.Save(Assignment, AssignmentPath);
        }

        private string GoalSnapshotPath(string runId) => Path.Combine(CurrentDataFolder, "goal_snapshot_" + Safe(runId) + ".json");
        private void PersistGoalSnapshot()
        {
            if (!IsArmed || string.IsNullOrWhiteSpace(CurrentRunId)) return;
            Directory.CreateDirectory(CurrentDataFolder);
            var value = new EditorCollectionGoalSnapshot
            {
                participantId = ParticipantId, sessionId = SessionId, conditionRunId = CurrentRunId,
                taskId = CurrentTaskId, savedAtUtc = DateTime.UtcNow.ToString("o"),
                goals = lifecycle.GoalTracker.Goals.ToArray()
            };
            File.WriteAllText(GoalSnapshotPath(CurrentRunId), JsonUtility.ToJson(value, true), Encoding.UTF8);
        }

        private EditorCollectionGoalSnapshot LoadGoalSnapshot(string runId)
        {
            var path = GoalSnapshotPath(runId);
            return File.Exists(path) ? JsonUtility.FromJson<EditorCollectionGoalSnapshot>(File.ReadAllText(path, Encoding.UTF8)) : null;
        }

        private void WriteOperator(string eventType, string detail = "", bool qa = false)
        {
            if (!IsArmed) return;
            Directory.CreateDirectory(CurrentDataFolder);
            var value = new EditorCollectionOperatorEvent
            {
                timestampUtc = DateTime.UtcNow.ToString("o"), eventType = eventType,
                participantId = ParticipantId, sessionId = SessionId,
                protocolVersion = protocol.ProtocolVersion, protocolSnapshotId = protocol.ProtocolSnapshotId,
                resourceSnapshotId = resources.ResourceSnapshotId, qaAutomationUsed = qa,
                actor = qa ? "qa_recovery_operator" : "experiment_operator", detail = detail ?? string.Empty
            };
            File.AppendAllText(Path.Combine(CurrentDataFolder, "editor_collection_operator_events.jsonl"),
                JsonUtility.ToJson(value) + Environment.NewLine, Encoding.UTF8);
        }

        private bool ValidateCollectionAssignment(ExperimentAssignment value, out string error)
        {
            if (value == null || value.flowMode != ExperimentFlowMode.Formal
                || value.runQualification != ExperimentRunQualification.Collection
                || value.dataOrigin != "participant_collection" || !value.collectionEligible
                || value.developerTestAssignment || value.demoMode || value.synthetic
                || value.deploymentProfile != "editor_collection")
            { error = "editor_collection_assignment_invalid"; return false; }
            return ExperimentAssignmentAllocator.ValidateAssignment(value, taskCatalog, out error);
        }

        private bool TryApplyConfirmedRunLimits(out string error)
        {
            if (!protocol.TryGetConfirmedDecision("formal_max_turns", out var turnsValue)
                || !int.TryParse(turnsValue, out var turns) || turns <= 0)
            { error = "formal_max_turns_invalid"; return false; }
            if (!protocol.TryGetConfirmedDecision("formal_max_duration", out var durationValue))
            { error = "formal_max_duration_invalid"; return false; }
            var token = new string(durationValue.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            if (!float.TryParse(token, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var minutes) || minutes <= 0f)
            { error = "formal_max_duration_invalid"; return false; }
            lifecycle.ConfigureRunLimits(turns, minutes);
            error = string.Empty;
            return true;
        }

        private bool FailArm() { RuntimeContext = null; return false; }
        private void ResetRuntimeOnly()
        {
            orchestrator?.ResetForConditionSelection();
            participantStarted = false;
            finalRankingVisible = false;
            rankingSubmitted = false;
            experimentCompleted = false;
            currentPosition = -1;
            questionnaire?.Service.Reset();
            lifecycle?.ClearAssignmentForRuntimeMode();
        }

        private void RefreshUi() => FindFirstObjectByType<SceneTalkFlowUiController>(FindObjectsInactive.Include)?.RefreshExternalState();
        private static string Safe(string value) => new string((value ?? string.Empty).Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
    }
}
