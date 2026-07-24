using System;
using SceneTalkVR.Core;

namespace SceneTalkVR.History
{
    public enum ExperimentRecordStatus
    {
        InProgress,
        Completed
    }

    public enum ExperimentPhaseKind
    {
        Pilot,
        Formal
    }

    public enum ExperimentPhaseStatus
    {
        NotStarted,
        InProgress,
        Suspended,
        Completed
    }

    public enum ExperimentAttemptStatus
    {
        Running,
        Suspended,
        Completed,
        TechnicalInvalid,
        Aborted
    }

    [Serializable]
    public sealed class ExperimentConversationLink
    {
        public string experimentId;
        public ExperimentPhaseKind phase;
        public string attemptId;
        public string runId;

        public bool IsValid => !string.IsNullOrWhiteSpace(experimentId)
            && !string.IsNullOrWhiteSpace(attemptId);
    }

    [Serializable]
    public sealed class ExperimentRecordSummary
    {
        public string experimentId;
        public string participantId;
        public ExperimentRecordStatus status;
        public ExperimentPhaseStatus pilotStatus;
        public ExperimentPhaseStatus formalStatus;
        public string preferredEmbodiment;
        public long createdAtUnixMs;
        public long updatedAtUnixMs;

        public bool CanContinue => status != ExperimentRecordStatus.Completed;
    }

    [Serializable]
    public sealed class ExperimentPhaseRecord
    {
        public string experimentId;
        public ExperimentPhaseKind phase;
        public string sessionId;
        public ExperimentPhaseStatus status;
        public string dataRootPath;
        public long startedAtUnixMs;
        public long completedAtUnixMs;
        public long updatedAtUnixMs;
    }

    [Serializable]
    public sealed class ExperimentAttemptRecord
    {
        public string attemptId;
        public string experimentId;
        public ExperimentPhaseKind phase;
        public string conditionKey;
        public string taskId;
        public string runId;
        public int attemptIndex;
        public ExperimentAttemptStatus status;
        public string completionReason;
        public long startedAtUnixMs;
        public long endedAtUnixMs;
    }

    [Serializable]
    public sealed class QuestionnairePromptSnapshot
    {
        public string itemId;
        public string sectionId;
        public string promptEnglish;
        public string promptChinese;
        public int scaleMin;
        public int scaleMax;
    }

    [Serializable]
    public sealed class ExperimentQuestionnaireRecord
    {
        public string questionnaireRecordId;
        public string experimentId;
        public ExperimentPhaseKind phase;
        public string attemptId;
        public QuestionnaireSession session = new QuestionnaireSession();
        public QuestionnairePromptSnapshot[] prompts = Array.Empty<QuestionnairePromptSnapshot>();
    }

    [Serializable]
    public sealed class ExperimentRankingRecord
    {
        public string experimentId;
        public ExperimentPhaseKind phase;
        public PreferenceRankingResponse response = new PreferenceRankingResponse();
    }

    [Serializable]
    public sealed class ExperimentRecordDetail
    {
        public ExperimentRecordSummary summary = new ExperimentRecordSummary();
        public ExperimentPhaseRecord[] phases = Array.Empty<ExperimentPhaseRecord>();
        public ExperimentAttemptRecord[] attempts = Array.Empty<ExperimentAttemptRecord>();
        public LearningSessionSummary[] conversations = Array.Empty<LearningSessionSummary>();
        public ExperimentQuestionnaireRecord[] questionnaires = Array.Empty<ExperimentQuestionnaireRecord>();
        public ExperimentRankingRecord[] rankings = Array.Empty<ExperimentRankingRecord>();
    }

    [Serializable]
    public sealed class ExperimentRecordPage
    {
        public ExperimentRecordSummary[] items = Array.Empty<ExperimentRecordSummary>();
        public int pageIndex;
        public int pageSize;
        public int totalCount;

        public int TotalPages => totalCount <= 0 || pageSize <= 0
            ? 1
            : (totalCount + pageSize - 1) / pageSize;
    }
}
