# Experiment v1.1 Stage 4 Study Event Schema

Authoritative workflow stream: `<participant>_<session>_study_events_v1.jsonl` under `Application.persistentDataPath/SceneTalkVR/ExperimentLogs`. This is independent of, and does not modify, Stage 3 timing events.

Schema version: `1.0`.

Events: `AssignmentCreated`, `AssignmentLoaded`, `ConditionPrepared`, `ConditionStarted`, `TaskLoaded`, `GoalCandidateSubmitted`, `GoalConfirmed`, `GoalRejected`, `TaskCompleted`, `ConditionAwaitingQuestionnaire`, `ConditionCompleted`, `ConditionTechnicalInvalid`, `ConditionAborted`, `ExperimentCompleted`.

Every line contains `schemaVersion`, `timestampUtc`, `eventType`, `participantId`, `sessionId`, `conditionRunId`, `questionnaireLinkageKey`, `sequenceId`, `conditionPosition`, `formalConditionCode`, `taskId`, `taskAssignmentId`, `goalId`, `turnId`, `actor`, `reason`, and `technicalValidity`.

Condition/task summaries are included at event time: `completedGoalCount`, `totalGoalCount`, `taskCompletionRate`, `turnsToCompletion`, `conditionDurationMs`, and `completionReason`.

Events append; retries generate a new `conditionRunId` and never replace earlier lines. The current assignment snapshot is saved after each event for crash recovery.
