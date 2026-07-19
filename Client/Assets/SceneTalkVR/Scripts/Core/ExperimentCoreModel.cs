using System;
using UnityEngine;

namespace SceneTalkVR.Core
{
    // v1.1 runtime types. String condition IDs are intentionally confined to legacy adapters.
    public enum ExperimentPhase { Developer, Pilot, Formal }
    public enum FormalConditionCode { NE, NR, SE, SR }
    public enum FeedbackProvider { DialogueAvatar, AssistantAgent }
    public enum FeedbackStyle { Explicit, Recast }
    public enum EmbodimentCondition { VoiceOnly, FloatingOrb, HumanoidAgent }
    public enum ExperimentTechnicalValidity { Valid, Retry, FallbackUsed, TechnicalInvalid }

    [Serializable]
    public struct ExperimentTaskReference
    {
        public string taskId;
        public string scenarioId;
    }

    public enum AssignmentPolicy { Undefined, StrictWithoutReplacement, WithReplacement, Manual }
    public enum AssignmentStatus { Created, Active, Completed, Incompatible, Aborted }
    public enum ConditionRunStatus
    {
        Assigned, Preparing, Running, TaskCompleted, AwaitingQuestionnaire,
        QuestionnaireInProgress, QuestionnaireSubmitted, Completed, TechnicalInvalid, Aborted
    }

    [Serializable]
    public sealed class ExperimentParticipant
    {
        public string participantId;
        public string experimentSessionId;
    }

    [Serializable]
    public sealed class TaskAssignment
    {
        public string taskId;
        public string taskAssignmentId;
    }

    [Serializable]
    public sealed class ConditionAssignment
    {
        public int conditionPosition;
        public FormalConditionCode formalConditionCode;
        public string formalConditionLabel;
        public TaskAssignment task = new TaskAssignment();
        public ConditionRunStatus status = ConditionRunStatus.Assigned;
        public string latestConditionRunId;
        public int runAttempt;
    }

    [Serializable]
    public sealed class AssignmentSequence
    {
        public string sequenceId;
        public FormalConditionCode[] conditions = Array.Empty<FormalConditionCode>();
    }

    [Serializable]
    public sealed class ExperimentAssignment
    {
        // Legacy-compatible single-assignment view.
        public FormalConditionCode condition;
        public ExperimentTaskReference task;
        public string sequenceId;
        public int conditionOrderIndex;

        public string participantId;
        public string experimentSessionId;
        public string assignmentSeed;
        public string assignmentVersion;
        public string protocolVersion;
        public string taskCatalogVersion;
        public string createdAtUtc;
        public AssignmentPolicy policy;
        public AssignmentStatus status;
        public bool developerTestAssignment;
        public string dataOrigin;
        public bool collectionEligible;
        public ConditionAssignment[] conditions = Array.Empty<ConditionAssignment>();
    }

    [Serializable]
    public struct ExperimentRunContext
    {
        public string participantId;
        public string sessionId;
        public ExperimentPhase phase;
        public FormalConditionCode formalCondition;
        public ExperimentAssignment assignment;
    }

    public static class FormalConditionResolver
    {
        public static bool TryResolve(FormalConditionCode code, out FeedbackProvider provider, out FeedbackStyle style)
        {
            provider = code == FormalConditionCode.SE || code == FormalConditionCode.SR
                ? FeedbackProvider.AssistantAgent : FeedbackProvider.DialogueAvatar;
            style = code == FormalConditionCode.NR || code == FormalConditionCode.SR
                ? FeedbackStyle.Recast : FeedbackStyle.Explicit;
            return Enum.IsDefined(typeof(FormalConditionCode), code);
        }

        public static string ToLegacyProvider(FeedbackProvider value) => value == FeedbackProvider.AssistantAgent
            ? ExperimentConditionManager.AssistantAgentProvider : ExperimentConditionManager.DialogueAvatarProvider;
        public static string ToLegacyStyle(FeedbackStyle value) => value == FeedbackStyle.Recast
            ? ExperimentConditionManager.RecastStyle : ExperimentConditionManager.ExplicitStyle;
        public static string ToLegacyConditionId(FormalConditionCode code) => code switch
        {
            FormalConditionCode.NR => "dialogue_avatar_recast",
            FormalConditionCode.SE => "assistant_agent_explicit",
            FormalConditionCode.SR => "assistant_agent_recast",
            _ => "dialogue_avatar_explicit"
        };
    }
}
