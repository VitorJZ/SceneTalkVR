using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SceneTalkVR.Runtime;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum StudyEventType
    {
        AssignmentCreated, AssignmentLoaded, ConditionPrepared, ConditionStarted, TaskLoaded,
        GoalCandidateSubmitted, GoalConfirmed, GoalRejected, TaskCompleted,
        ConditionAwaitingQuestionnaire, QuestionnaireStarted, QuestionnairePageCompleted,
        QuestionnaireSubmitted, QuestionnaireReopened, FinalRankingStarted, FinalRankingSubmitted,
        InterviewStarted, InterviewCompleted, ConditionCompleted, ConditionTechnicalInvalid,
        ConditionAborted, ExperimentCompleted
    }

    [Serializable]
    public sealed class StudyEventRecord
    {
        public string schemaVersion = "1.0";
        public string timestampUtc;
        public string eventType;
        public string participantId;
        public string sessionId;
        public string conditionRunId;
        public string questionnaireLinkageKey;
        public string sequenceId;
        public int conditionPosition;
        public string formalConditionCode;
        public string taskId;
        public string taskAssignmentId;
        public string goalId;
        public string turnId;
        public string actor;
        public string reason;
        public string technicalValidity;
        public int completedGoalCount;
        public int totalGoalCount;
        public float taskCompletionRate;
        public int turnsToCompletion;
        public long conditionDurationMs;
        public string completionReason;
    }

    [DisallowMultipleComponent]
    public sealed class ExperimentLifecycleCoordinator : MonoBehaviour, ISceneTalkSessionReset
    {
        [SerializeField] private ExperimentConditionManager conditionManager;
        [SerializeField] private SceneTalkOrchestrator orchestrator;
        [SerializeField] private int maxTurns;
        [SerializeField] private float maxDurationMinutes;
        private readonly GoalProgressTracker goalTracker = new GoalProgressTracker();
        private ExperimentAssignment assignment;
        private ConditionAssignment currentCondition;
        private DateTime conditionStartedUtc;
        private int conditionStartTurn;
        private bool goalEventsSubscribed;

        public ExperimentAssignment Assignment => assignment;
        public ConditionAssignment CurrentConditionAssignment => currentCondition;
        public GoalProgressTracker GoalTracker => goalTracker;
        public bool IsDeveloperManualSession => assignment != null
            && assignment.developerTestAssignment
            && string.Equals(assignment.dataOrigin, "developer_manual", StringComparison.Ordinal);
        public string ConditionRunId { get; private set; }
        public string QuestionnaireLinkageKey { get; private set; }
        public string CompletionReason { get; private set; }
        public ExperimentTechnicalValidity TechnicalValidity { get; private set; } = ExperimentTechnicalValidity.Valid;
        public long ConditionDurationMs => conditionStartedUtc == default ? 0 : (long)(DateTime.UtcNow - conditionStartedUtc).TotalMilliseconds;
        public int TurnsToCompletion => conditionManager == null ? 0 : Mathf.Max(0, conditionManager.CurrentTurnIndex - conditionStartTurn);

        private void Awake()
        {
            if (conditionManager == null) conditionManager = GetComponent<ExperimentConditionManager>();
            if (orchestrator == null) orchestrator = GetComponent<SceneTalkOrchestrator>();
            EnsureGoalSubscription();
        }

        private void OnDestroy()
        {
            if (goalEventsSubscribed) goalTracker.GoalChanged -= OnGoalChanged;
        }

        public void Configure(ExperimentConditionManager manager, SceneTalkOrchestrator targetOrchestrator = null)
        {
            conditionManager = manager;
            if (targetOrchestrator != null) orchestrator = targetOrchestrator;
            EnsureGoalSubscription();
        }

        private void EnsureGoalSubscription()
        {
            if (goalEventsSubscribed) return;
            goalTracker.GoalChanged += OnGoalChanged;
            goalEventsSubscribed = true;
        }

        public bool LoadAssignment(ExperimentAssignment value, out string error)
        {
            if (value == null) { error = "assignment_missing"; return false; }
            if (conditionManager == null) { error = "condition_manager_missing"; return false; }
            if (conditionManager.IsFormalExperiment && value.developerTestAssignment) { error = "formal_mode_rejects_developer_assignment"; return false; }
            if (conditionManager.IsFormalExperiment && !string.IsNullOrWhiteSpace(value.dataOrigin) && !value.collectionEligible) { error = "formal_mode_rejects_collection_ineligible_assignment"; return false; }
            if (conditionManager.IsFormalExperiment && !conditionManager.ValidateFormalProtocol(out error)) return false;
            if (!ExperimentAssignmentAllocator.IsCompatible(value,
                conditionManager.ExperimentProtocol?.ProtocolVersion ?? string.Empty,
                conditionManager.TaskCatalog?.CatalogVersion ?? string.Empty, out error))
            {
                value.status = AssignmentStatus.Incompatible;
                ExperimentAssignmentAllocator.Save(value,
                    ExperimentAssignmentAllocator.DefaultPath(value.participantId, value.experimentSessionId));
                return false;
            }
            if (!ExperimentAssignmentAllocator.ValidateAssignment(value, conditionManager.TaskCatalog, out error)) return false;
            assignment = value;
            WriteEvent(StudyEventType.AssignmentLoaded, actor: "system");
            return true;
        }

        public bool CreateOrLoadFormalAssignment(string participantId, string sessionId, AssignmentPolicy policy, out string error)
        {
            error = string.Empty;
            if (conditionManager == null) { error = "condition_manager_missing"; return false; }
            var path = ExperimentAssignmentAllocator.DefaultPath(participantId, sessionId);
            var stored = ExperimentAssignmentAllocator.Load(path);
            if (stored != null)
            {
                if (!LoadAssignment(stored, out error)) return false;
                return true;
            }
            var allocator = new ExperimentAssignmentAllocator();
            if (!allocator.TryCreateFormal(participantId, sessionId, conditionManager.ExperimentProtocol,
                conditionManager.TaskCatalog, policy, out var created, out error)) return false;
            created.developerTestAssignment = false;
            created.dataOrigin = "participant_collection";
            created.collectionEligible = true;
            assignment = created;
            ExperimentAssignmentAllocator.Save(assignment, path);
            WriteEvent(StudyEventType.AssignmentCreated, actor: "system");
            return true;
        }

        public bool PrepareCondition(int position, bool allowTechnicalRetry, out string error)
        {
            error = string.Empty;
            if (assignment?.conditions == null || position < 0 || position >= assignment.conditions.Length) { error = "condition_assignment_missing"; return false; }
            var next = assignment.conditions[position];
            if (next.status == ConditionRunStatus.Completed || next.status == ConditionRunStatus.AwaitingQuestionnaire)
            { error = "condition_already_completed"; return false; }
            if (next.status == ConditionRunStatus.TechnicalInvalid && !allowTechnicalRetry)
            { error = "technical_retry_requires_explicit_authorization"; return false; }

            conditionManager.ResetConditionSessionBoundary();
            currentCondition = next;
            assignment.status = AssignmentStatus.Active;
            currentCondition.runAttempt++;
            ConditionRunId = $"cr-{assignment.assignmentSeed}-{position}-{currentCondition.runAttempt}-{Guid.NewGuid():N}";
            QuestionnaireLinkageKey = $"ql-{ConditionRunId}";
            currentCondition.latestConditionRunId = ConditionRunId;
            currentCondition.status = ConditionRunStatus.Preparing;
            CompletionReason = string.Empty;
            TechnicalValidity = ExperimentTechnicalValidity.Valid;
            WriteEvent(StudyEventType.ConditionPrepared, actor: "system");
            if (!conditionManager.ApplyFormalAssignment(currentCondition.formalConditionCode, currentCondition.task.taskId, out error,
                assignment.participantId, assignment.experimentSessionId))
            {
                currentCondition.status = ConditionRunStatus.TechnicalInvalid;
                WriteEvent(StudyEventType.ConditionTechnicalInvalid, reason: error, actor: "system", validity: ExperimentTechnicalValidity.TechnicalInvalid);
                return false;
            }
            var task = conditionManager.TaskCatalog.Find(currentCondition.task.taskId);
            goalTracker.ResetGoals(task);
            WriteEvent(StudyEventType.TaskLoaded, actor: "system");
            currentCondition.status = ConditionRunStatus.Running;
            conditionStartedUtc = DateTime.UtcNow;
            conditionStartTurn = conditionManager.CurrentTurnIndex;
            WriteEvent(StudyEventType.ConditionStarted, actor: "system");
            orchestrator?.LoadAssignedTask(currentCondition.task.taskId);
            return true;
        }

        public bool PrepareDeveloperTaskSession(string taskId, out string error)
        {
            error = string.Empty;
            if (conditionManager == null) { error = "condition_manager_missing"; return false; }
            if (conditionManager.IsFormalExperiment) { error = "formal_mode_rejects_developer_manual_session"; return false; }
            var task = conditionManager.TaskCatalog?.Find(taskId);
            if (task == null) { error = $"task_catalog_missing:{taskId}"; return false; }
            if (task.phase != ExperimentTaskPhase.Formal) { error = $"developer_manual_requires_formal_task:{taskId}"; return false; }

            if (IsDeveloperManualSession && currentCondition?.status == ConditionRunStatus.Running)
                Abort("developer_task_switched");
            conditionManager.ResetConditionSessionBoundary();

            var token = Guid.NewGuid().ToString("N");
            var participantId = "developer_manual";
            var sessionId = $"developer-manual-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{token.Substring(0, 8)}";
            var conditionCode = conditionManager.CurrentFormalCondition;
            var taskAssignment = new TaskAssignment
            {
                taskId = task.taskId,
                taskAssignmentId = $"developer-task-{token}"
            };
            currentCondition = new ConditionAssignment
            {
                conditionPosition = 0,
                formalConditionCode = conditionCode,
                formalConditionLabel = conditionCode.ToString(),
                task = taskAssignment,
                status = ConditionRunStatus.Preparing,
                runAttempt = 1
            };
            assignment = new ExperimentAssignment
            {
                condition = conditionCode,
                task = new ExperimentTaskReference { taskId = task.taskId, scenarioId = task.scenarioId },
                sequenceId = "developer-manual",
                conditionOrderIndex = 0,
                participantId = participantId,
                experimentSessionId = sessionId,
                assignmentSeed = token,
                assignmentVersion = ExperimentAssignmentAllocator.AssignmentVersion,
                protocolVersion = conditionManager.ExperimentProtocol?.ProtocolVersion ?? string.Empty,
                taskCatalogVersion = conditionManager.TaskCatalog?.CatalogVersion ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                policy = AssignmentPolicy.Manual,
                status = AssignmentStatus.Active,
                developerTestAssignment = true,
                dataOrigin = "developer_manual",
                collectionEligible = false,
                conditions = new[] { currentCondition }
            };
            ConditionRunId = $"developer-run-{token}";
            QuestionnaireLinkageKey = $"developer-link-{token}";
            currentCondition.latestConditionRunId = ConditionRunId;
            CompletionReason = string.Empty;
            TechnicalValidity = ExperimentTechnicalValidity.Valid;
            WriteEvent(StudyEventType.AssignmentCreated, actor: "developer");
            WriteEvent(StudyEventType.ConditionPrepared, actor: "developer");

            if (!conditionManager.ApplyFormalAssignment(conditionCode, task.taskId, out error, participantId, sessionId))
            {
                currentCondition.status = ConditionRunStatus.TechnicalInvalid;
                TechnicalValidity = ExperimentTechnicalValidity.TechnicalInvalid;
                WriteEvent(StudyEventType.ConditionTechnicalInvalid, actor: "developer", reason: error,
                    validity: ExperimentTechnicalValidity.TechnicalInvalid);
                return false;
            }

            goalTracker.ResetGoals(task);
            WriteEvent(StudyEventType.TaskLoaded, actor: "developer");
            currentCondition.status = ConditionRunStatus.Running;
            conditionStartedUtc = DateTime.UtcNow;
            conditionStartTurn = conditionManager.CurrentTurnIndex;
            WriteEvent(StudyEventType.ConditionStarted, actor: "developer");
            return true;
        }

        public bool SubmitGoalCandidate(string goalId, string source, string turnId, string transcript, out string error) =>
            goalTracker.SubmitGoalCandidate(goalId, source, new GoalEvidence { turnId = turnId, transcript = transcript }, out error);

        public bool ConfirmGoalByExperimenter(string goalId, string experimenterId, string note, out string error) =>
            goalTracker.ConfirmGoal(goalId, experimenterId, note, out error);

        public bool RejectGoalByExperimenter(string goalId, string experimenterId, string reason, out string error) =>
            goalTracker.RejectGoal(goalId, experimenterId, reason, out error);

        public bool ShouldEndForLimit(out string reason)
        {
            if (maxTurns > 0 && TurnsToCompletion >= maxTurns) { reason = "max_turns"; return true; }
            if (maxDurationMinutes > 0f && ConditionDurationMs >= maxDurationMinutes * 60000f) { reason = "max_duration"; return true; }
            reason = string.Empty;
            return false;
        }

        public void CompleteTask(string reason, string actor = "experimenter")
        {
            if (currentCondition == null) return;
            CompletionReason = string.IsNullOrWhiteSpace(reason) ? (goalTracker.AreAllConfirmed ? "all_goals_confirmed" : "experimenter_ended") : reason;
            currentCondition.status = ConditionRunStatus.TaskCompleted;
            WriteEvent(StudyEventType.TaskCompleted, reason: CompletionReason, actor: actor);
            currentCondition.status = ConditionRunStatus.AwaitingQuestionnaire;
            WriteEvent(StudyEventType.ConditionAwaitingQuestionnaire, reason: CompletionReason, actor: actor);
        }

        public bool BeginQuestionnaire(string conditionRunId, string linkageKey, out string error)
        {
            if (currentCondition == null || currentCondition.status != ConditionRunStatus.AwaitingQuestionnaire) { error = "condition_not_awaiting_questionnaire"; return false; }
            if (conditionRunId != ConditionRunId || linkageKey != QuestionnaireLinkageKey) { error = "questionnaire_linkage_mismatch"; return false; }
            if (TechnicalValidity == ExperimentTechnicalValidity.TechnicalInvalid) { error = "technical_invalid_condition"; return false; }
            currentCondition.status = ConditionRunStatus.QuestionnaireInProgress;
            WriteEvent(StudyEventType.QuestionnaireStarted, actor: "participant");
            error = string.Empty; return true;
        }

        public bool CompleteQuestionnaireSubmission(string conditionRunId, string linkageKey, out string error, string actor = "participant")
        {
            if (!ValidateQuestionnaireSubmission(conditionRunId, linkageKey, out error)) return false;
            currentCondition.status = ConditionRunStatus.QuestionnaireSubmitted;
            WriteEvent(StudyEventType.QuestionnaireSubmitted, actor: actor);
            currentCondition.status = ConditionRunStatus.Completed;
            WriteEvent(StudyEventType.ConditionCompleted, reason: CompletionReason, actor: actor);
            if (AllConditionsCompleted()) { assignment.status = AssignmentStatus.Completed; WriteEvent(StudyEventType.ExperimentCompleted, actor: actor); }
            error = string.Empty; return true;
        }

        public bool ValidateQuestionnaireSubmission(string conditionRunId, string linkageKey, out string error)
        {
            if (currentCondition == null || currentCondition.status != ConditionRunStatus.QuestionnaireInProgress) { error = "questionnaire_not_in_progress"; return false; }
            if (conditionRunId != ConditionRunId || linkageKey != QuestionnaireLinkageKey) { error = "questionnaire_linkage_mismatch"; return false; }
            if (TechnicalValidity == ExperimentTechnicalValidity.TechnicalInvalid) { error = "technical_invalid_condition"; return false; }
            error = string.Empty; return true;
        }

        [Obsolete("Questionnaires must submit through CompleteQuestionnaireSubmission with linkage validation.")]
        public void CompleteQuestionnaireBoundary(string actor = "experimenter")
        {
            if (conditionManager != null && conditionManager.IsFormalExperiment) return;
            if (currentCondition == null || currentCondition.status != ConditionRunStatus.AwaitingQuestionnaire) return;
            currentCondition.status = ConditionRunStatus.Completed;
            WriteEvent(StudyEventType.ConditionCompleted, reason: "developer_legacy_questionnaire_boundary", actor: actor);
        }

        public void RecordStudyEvent(StudyEventType type, string actor = "system", string reason = "") => WriteEvent(type, actor: actor, reason: reason);

        public void MarkTechnicalInvalid(string reason)
        {
            if (currentCondition == null) return;
            currentCondition.status = ConditionRunStatus.TechnicalInvalid;
            CompletionReason = reason ?? "technical_failure";
            TechnicalValidity = ExperimentTechnicalValidity.TechnicalInvalid;
            WriteEvent(StudyEventType.ConditionTechnicalInvalid, reason: CompletionReason, actor: "experimenter", validity: ExperimentTechnicalValidity.TechnicalInvalid);
        }

        public void Abort(string reason)
        {
            if (currentCondition == null) return;
            currentCondition.status = ConditionRunStatus.Aborted;
            CompletionReason = reason ?? "aborted";
            WriteEvent(StudyEventType.ConditionAborted, reason: CompletionReason, actor: "experimenter");
        }

        public void ResetSession()
        {
            goalTracker.ResetGoals(null);
            conditionStartedUtc = default;
            conditionStartTurn = 0;
            CompletionReason = string.Empty;
            TechnicalValidity = ExperimentTechnicalValidity.Valid;
            if (!IsDeveloperManualSession) return;
            assignment = null;
            currentCondition = null;
            ConditionRunId = string.Empty;
            QuestionnaireLinkageKey = string.Empty;
        }

        private void OnGoalChanged(GoalProgressRecord goal, string action)
        {
            var type = action == "confirmed" ? StudyEventType.GoalConfirmed : action == "rejected" ? StudyEventType.GoalRejected : StudyEventType.GoalCandidateSubmitted;
            WriteEvent(type, goal.goalId, goal.evidenceTurnId, action == "candidate" ? goal.candidateSource : goal.confirmedBy, goal.rejectionReason);
            if (goalTracker.AreAllConfirmed && currentCondition?.status == ConditionRunStatus.Running) CompleteTask("all_goals_confirmed", "system");
        }

        private bool AllConditionsCompleted()
        {
            if (assignment?.conditions == null) return false;
            foreach (var item in assignment.conditions) if (item.status != ConditionRunStatus.Completed) return false;
            return true;
        }

        private void WriteEvent(StudyEventType type, string goalId = "", string turnId = "", string actor = "", string reason = "", ExperimentTechnicalValidity validity = ExperimentTechnicalValidity.Valid)
        {
            if (assignment == null) return;
            var record = new StudyEventRecord
            {
                timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), eventType = type.ToString(), participantId = assignment.participantId,
                sessionId = assignment.experimentSessionId, conditionRunId = ConditionRunId ?? string.Empty,
                questionnaireLinkageKey = QuestionnaireLinkageKey ?? string.Empty, sequenceId = assignment.sequenceId,
                conditionPosition = currentCondition?.conditionPosition ?? -1, formalConditionCode = currentCondition?.formalConditionCode.ToString() ?? string.Empty,
                taskId = currentCondition?.task?.taskId ?? string.Empty, taskAssignmentId = currentCondition?.task?.taskAssignmentId ?? string.Empty,
                goalId = goalId ?? string.Empty, turnId = turnId ?? string.Empty, actor = actor ?? string.Empty,
                reason = reason ?? string.Empty, technicalValidity = validity.ToString(),
                completedGoalCount = goalTracker.ConfirmedCount, totalGoalCount = goalTracker.Goals.Count,
                taskCompletionRate = goalTracker.GetCompletionRate(), turnsToCompletion = TurnsToCompletion,
                conditionDurationMs = ConditionDurationMs, completionReason = CompletionReason ?? string.Empty
            };
            try
            {
                var folder = Path.Combine(Application.persistentDataPath, "SceneTalkVR", "ExperimentLogs");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, $"{assignment.participantId}_{assignment.experimentSessionId}_study_events_v1.jsonl"),
                    JsonUtility.ToJson(record) + Environment.NewLine, Encoding.UTF8);
                ExperimentAssignmentAllocator.Save(assignment,
                    ExperimentAssignmentAllocator.DefaultPath(assignment.participantId, assignment.experimentSessionId));
            }
            catch (Exception ex) { Debug.LogWarning($"[Experiment] Study event write failed: {ex.Message}", this); }
        }
    }
}
