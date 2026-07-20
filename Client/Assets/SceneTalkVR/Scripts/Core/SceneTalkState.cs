namespace SceneTalkVR.Core
{
    public enum SceneTalkState
    {
        Idle,
        Settings,
        HistoryLoading,
        HistoryList,
        HistoryDetail,
        HistoryDeleteConfirm,
        HistoryRestoring,
        HistoryError,
        Listening,
        Recording,
        Transcribing,
        Processing,
        SceneReady,
        AvatarSpeaking,
        CorrectionFeedbackSpeaking,
        DialogueSpeaking,
        TurnReview,
        Questionnaire,
        Finished,
        Error
    }
}
