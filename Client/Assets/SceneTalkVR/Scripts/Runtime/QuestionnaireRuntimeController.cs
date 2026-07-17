using System;
using System.Collections.Generic;
using System.IO;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Runtime
{
    [DisallowMultipleComponent]
    public sealed class QuestionnaireRuntimeController : MonoBehaviour
    {
        [SerializeField] private ExperimentConditionManager conditionManager;
        [SerializeField] private ExperimentLifecycleCoordinator lifecycle;
        private readonly QuestionnaireSessionService service = new QuestionnaireSessionService();
        public QuestionnaireSessionService Service => service;
        public QuestionnaireSession ActiveSession => service.ActiveSession;
        public event Action<QuestionnaireSession> QuestionnaireChanged;

        private void Awake()
        {
            if (conditionManager == null) conditionManager = GetComponent<ExperimentConditionManager>();
            if (lifecycle == null) lifecycle = GetComponent<ExperimentLifecycleCoordinator>();
            Configure(conditionManager, lifecycle);
        }

        private void OnDestroy() => service.SessionChanged -= ForwardChanged;

        public void Configure(ExperimentConditionManager manager, ExperimentLifecycleCoordinator coordinator)
        {
            service.SessionChanged -= ForwardChanged;
            conditionManager = manager; lifecycle = coordinator;
            service.Configure(manager?.QuestionnaireCatalog, manager?.ExperimentProtocol);
            service.SessionChanged += ForwardChanged;
        }

        public bool StartCurrentConditionQuestionnaire(out string error)
        {
            if (conditionManager?.QuestionnaireCatalog == null || lifecycle?.Assignment == null || lifecycle.CurrentConditionAssignment == null)
            { error = "questionnaire_runtime_not_configured"; return false; }
            if (lifecycle.CurrentConditionAssignment.status != ConditionRunStatus.AwaitingQuestionnaire)
            { error = "condition_not_awaiting_questionnaire"; return false; }
            var definition = conditionManager.QuestionnaireCatalog.Find("formal_condition_v1");
            var assignment = lifecycle.Assignment; var condition = lifecycle.CurrentConditionAssignment;
            var context = new QuestionnaireSession
            {
                participantId = assignment.participantId, sessionId = assignment.experimentSessionId, sequenceId = assignment.sequenceId,
                conditionRunId = lifecycle.ConditionRunId, questionnaireLinkageKey = lifecycle.QuestionnaireLinkageKey,
                conditionPosition = condition.conditionPosition, assignmentPolicy = assignment.policy, formalCondition = condition.formalConditionCode,
                conditionStatus = ConditionRunStatus.QuestionnaireInProgress,
                taskId = condition.task.taskId, taskAssignmentId = condition.task.taskAssignmentId,
                technicalValidity = lifecycle.TechnicalValidity, protocolVersion = assignment.protocolVersion
            };
            if (!service.Begin(definition, context, out error)) return false;
            if (!lifecycle.BeginQuestionnaire(context.conditionRunId, context.questionnaireLinkageKey, out error)) return false;
            return true;
        }

        public bool SetResponse(string itemId, string rawValue, out string error) => service.SetResponse(itemId, rawValue, out error);

        public bool CompletePage(int pageIndex, out string error)
        {
            if (ActiveSession == null || ActiveSession.completionStatus == QuestionnaireCompletionStatus.Submitted)
            { error = "questionnaire_not_editable"; return false; }
            ActiveSession.currentPage = Mathf.Max(0, pageIndex);
            lifecycle.RecordStudyEvent(StudyEventType.QuestionnairePageCompleted, "participant", "page:" + pageIndex);
            error = string.Empty; return true;
        }

        public bool Submit(out string error)
        {
            if (!service.CanSubmit(out error)) return false;
            if (!lifecycle.ValidateQuestionnaireSubmission(ActiveSession.conditionRunId, ActiveSession.questionnaireLinkageKey, out error)) return false;
            if (!service.Submit(QuestionnaireSessionService.DefaultFolder, out error)) return false;
            return lifecycle.CompleteQuestionnaireSubmission(ActiveSession.conditionRunId, ActiveSession.questionnaireLinkageKey, out error);
        }

        public bool RestoreCurrentDraft(out string error)
        {
            if (lifecycle?.Assignment == null) { error = "assignment_missing"; return false; }
            var assignment = lifecycle.Assignment;
            var path = Path.Combine(QuestionnaireSessionService.DefaultFolder,
                $"{assignment.participantId}_{assignment.experimentSessionId}_{lifecycle.QuestionnaireLinkageKey}_questionnaire_draft.json");
            return service.Restore(path, lifecycle.QuestionnaireLinkageKey, assignment.protocolVersion,
                conditionManager.QuestionnaireCatalog.CatalogVersion, out error);
        }

        public bool ReopenByExperimenter(string experimenterId, out string error)
        {
            if (!service.Reopen(experimenterId, out error)) return false;
            lifecycle.RecordStudyEvent(StudyEventType.QuestionnaireReopened, experimenterId, "revision:" + ActiveSession.revision);
            return true;
        }

        public bool SubmitFormalRanking(PreferenceRankingResponse response, out string error)
        {
            if (lifecycle?.Assignment == null || lifecycle.Assignment.status != AssignmentStatus.Completed)
            { error = "formal_ranking_requires_four_completed_conditions"; return false; }
            lifecycle.RecordStudyEvent(StudyEventType.FinalRankingStarted, "experimenter");
            if (!response.ValidateUnique(new[] { "NE", "NR", "SE", "SR" }, out error)) return false;
            QuestionnaireResearchExporter.AppendRanking(QuestionnaireSessionService.DefaultFolder, response);
            lifecycle.RecordStudyEvent(StudyEventType.FinalRankingSubmitted, "experimenter"); return true;
        }

        public bool SubmitPilotRanking(PreferenceRankingResponse response, out string error)
        {
            if (!response.ValidateUnique(new[] { "voice_only", "floating_orb", "humanoid_agent" }, out error)) return false;
            QuestionnaireResearchExporter.AppendRanking(QuestionnaireSessionService.DefaultFolder, response); return true;
        }

        public bool SaveInterview(InterviewNote note, out string error)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.interviewerId)) { error = "interviewer_identity_required"; return false; }
            lifecycle.RecordStudyEvent(StudyEventType.InterviewStarted, note.interviewerId);
            QuestionnaireResearchExporter.AppendInterview(QuestionnaireSessionService.DefaultFolder, note);
            lifecycle.RecordStudyEvent(StudyEventType.InterviewCompleted, note.interviewerId); error = string.Empty; return true;
        }

        private void ForwardChanged(QuestionnaireSession session) => QuestionnaireChanged?.Invoke(session);
    }
}
