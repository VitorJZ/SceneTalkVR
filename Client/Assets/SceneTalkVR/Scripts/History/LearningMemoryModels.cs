using System;
using SceneTalkVR.Core;

namespace SceneTalkVR.History
{
    [Serializable]
    public sealed class ConversationSettingsSnapshot
    {
        public string brainMode;
        public string feedbackSensitivity;
        public CorrectionExperimentCondition condition;
    }

    [Serializable]
    public sealed class DialogueTurnRecord
    {
        public int sequenceIndex;
        public bool isOpening;
        public long createdAtUnixMs;
        public string userText;
        public string assistantText;
        public SpringScenePayload payload;

        public bool HasCorrection => payload != null
            && payload.correctionFeedback != null
            && payload.correctionFeedback.hasFeedback;
    }

    [Serializable]
    public sealed class LearningSessionSummary
    {
        public string sessionId;
        public string title;
        public string scenarioId;
        public string taskType;
        public string environmentType;
        public string correctionProvider;
        public string correctionStyle;
        public long createdAtUnixMs;
        public long updatedAtUnixMs;
        public int turnCount;
        public int correctionCount;
    }

    [Serializable]
    public sealed class LearningSessionDetail
    {
        public LearningSessionSummary summary = new LearningSessionSummary();
        public ConversationSettingsSnapshot settings = new ConversationSettingsSnapshot();
        public SpringScenePayload sceneSnapshot = new SpringScenePayload();
        public DialogueTurnRecord[] turns = Array.Empty<DialogueTurnRecord>();
    }

    [Serializable]
    public sealed class LearningSessionPage
    {
        public LearningSessionSummary[] items = Array.Empty<LearningSessionSummary>();
        public int pageIndex;
        public int pageSize;
        public int totalCount;

        public int TotalPages => totalCount <= 0 || pageSize <= 0
            ? 1
            : (totalCount + pageSize - 1) / pageSize;
    }
}
