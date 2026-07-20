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
    public enum ExperimentFlowMode { DeveloperManual, Formal, Pilot, Synthetic }
    public enum ExperimentRunQualification { Development, Rehearsal, Collection }
    public enum ExperimentRuntimeMode
    {
        DeveloperManual,
        EditorDemoFormal,
        EditorDemoPilot,
        SyntheticDryRun,
        LockedFormalCollection,
        LockedPilotCollection
    }

    [Serializable]
    public sealed class ExperimentRuntimeContext
    {
        public ExperimentFlowMode flowMode;
        public ExperimentRunQualification qualification;
        public string participantId;
        public string sessionId;
        public string protocolSnapshotId;
        public string resourceSnapshotId;
        public string dataOrigin;
        public bool collectionEligible;

        public bool IsValidCombination => IsAllowed(flowMode, qualification);
        public bool IsRehearsal => qualification == ExperimentRunQualification.Rehearsal;
        public bool IsCollection => qualification == ExperimentRunQualification.Collection;

        public static bool IsAllowed(ExperimentFlowMode flow, ExperimentRunQualification value) =>
            flow == ExperimentFlowMode.DeveloperManual && value == ExperimentRunQualification.Development
            || flow == ExperimentFlowMode.Formal && (value == ExperimentRunQualification.Rehearsal || value == ExperimentRunQualification.Collection)
            || flow == ExperimentFlowMode.Pilot && (value == ExperimentRunQualification.Rehearsal || value == ExperimentRunQualification.Collection)
            || flow == ExperimentFlowMode.Synthetic && value == ExperimentRunQualification.Development;

        public static ExperimentRuntimeContext CreateRehearsal(ExperimentFlowMode flow, string participantId,
            string sessionId, string protocolSnapshotId, string resourceSnapshotId)
        {
            if (flow != ExperimentFlowMode.Formal && flow != ExperimentFlowMode.Pilot)
                throw new ArgumentOutOfRangeException(nameof(flow), "Rehearsal requires Formal or Pilot flow.");
            return new ExperimentRuntimeContext
            {
                flowMode = flow,
                qualification = ExperimentRunQualification.Rehearsal,
                participantId = participantId?.Trim() ?? string.Empty,
                sessionId = sessionId?.Trim() ?? string.Empty,
                protocolSnapshotId = protocolSnapshotId?.Trim() ?? string.Empty,
                resourceSnapshotId = resourceSnapshotId?.Trim() ?? string.Empty,
                dataOrigin = "rehearsal",
                collectionEligible = false
            };
        }
    }

    [Serializable]
    public struct ExperimentTaskReference
    {
        public string taskId;
        public string scenarioId;
    }

    public enum AssignmentPolicy { Undefined, StrictWithoutReplacement, WithReplacement, Manual }
    public enum FormalConditionOrderPolicy { Undefined, CounterbalancedForcedOrder, ParticipantChoice }
    public enum QuestionnaireReturnPolicy { Undefined, ReturnToModeSelection, FinalRankingAfterLastCondition }
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
        public int participantSelectionPosition = -1;
        public string selectedAtUtc;
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
        public ExperimentRuntimeMode runtimeMode;
        public bool demoMode;
        public string demoProtocolVersion;
        public ExperimentFlowMode flowMode;
        public ExperimentRunQualification runQualification;
        public string protocolSnapshotId;
        public string resourceSnapshotId;
        public string formalConditionOrderPolicy;
        public string taskAssignmentPolicy;
        public string goalConfirmationPolicy;
        public string questionnaireReturnPolicy;
        public string assignmentAlgorithmVersion;
        public string randomSeedHash;
        public FormalConditionCode[] participantSelectionOrder = Array.Empty<FormalConditionCode>();
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
