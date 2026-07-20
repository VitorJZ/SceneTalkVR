using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneTalkVR.Core
{
    public enum GoalProgressState { NotStarted, Candidate, Confirmed, Rejected }
    public enum GoalConfirmationPolicy { ExperimenterReview, AutomaticOnValidatedDetection }

    [Serializable]
    public sealed class GoalEvidence
    {
        public string turnId;
        public string transcript;
        public float confidence = 1f;
    }

    [Serializable]
    public sealed class GoalTrackingContext
    {
        public string participantId;
        public string sessionId;
        public string conditionRunId;
        public string taskAssignmentId;
        public string taskId;
        public GoalConfirmationPolicy confirmationPolicy;
    }

    [Serializable]
    public sealed class GoalProgressRecord
    {
        public string goalId;
        public string goalText;
        public GoalProgressState state;
        public string candidateEvidence;
        public string candidateAt;
        public string confirmedAt;
        public string confirmedBy;
        public string conditionRunId;
        public string taskAssignmentId;
        public int revision;

        // Compatibility fields retained for existing exporters and operator tools.
        public string candidateSource;
        public string evidenceTurnId;
        public string evidenceTranscript;
        public string candidateAtUtc;
        public string confirmedAtUtc;
        public string rejectionReason;
    }

    [Serializable]
    public sealed class GoalProgressChangedEvent
    {
        public string participantId;
        public string sessionId;
        public string conditionRunId;
        public string taskAssignmentId;
        public string taskId;
        public string goalId;
        public GoalProgressState oldState;
        public GoalProgressState newState;
        public int confirmedCount;
        public int totalCount;
        public string timestampUtc;
        public string actor;
        public int revision;
    }

    public sealed class GoalProgressTracker
    {
        public const string AutomaticConfirmationActor = "system_rehearsal_goal_detector";
        private readonly List<GoalProgressRecord> goals = new List<GoalProgressRecord>();
        private GoalTrackingContext context = new GoalTrackingContext();
        private bool allConfirmedRaised;

        public IReadOnlyList<GoalProgressRecord> Goals => goals;
        public GoalTrackingContext Context => context;
        public event Action<GoalProgressRecord, string> GoalChanged;
        public event Action<GoalProgressChangedEvent> OnGoalStateChanged;
        public event Action<GoalProgressChangedEvent> OnGoalCollectionReset;
        public event Action<GoalProgressChangedEvent> OnGoalProgressChanged;
        public event Action<GoalProgressChangedEvent> OnAllGoalsConfirmed;

        public void ResetGoals(ExperimentTaskDefinition task) => ResetGoals(task, null);

        public void ResetGoals(ExperimentTaskDefinition task, GoalTrackingContext trackingContext)
        {
            goals.Clear();
            context = trackingContext ?? new GoalTrackingContext();
            allConfirmedRaised = false;
            if (task?.goals != null)
            {
                context.taskId = task.taskId;
                for (var i = 0; i < task.goals.Length; i++)
                    goals.Add(new GoalProgressRecord
                    {
                        goalId = $"{task.taskId}.goal.{i + 1}",
                        goalText = task.goals[i]?.text ?? string.Empty,
                        state = GoalProgressState.NotStarted,
                        conditionRunId = context.conditionRunId ?? string.Empty,
                        taskAssignmentId = context.taskAssignmentId ?? string.Empty,
                        revision = 0
                    });
            }
            var payload = CreatePayload(null, GoalProgressState.NotStarted, GoalProgressState.NotStarted, "system");
            OnGoalCollectionReset?.Invoke(payload);
            OnGoalProgressChanged?.Invoke(payload);
        }

        public bool SubmitGoalCandidate(string goalId, string source, GoalEvidence evidence, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (goal.state == GoalProgressState.Confirmed) { error = "confirmed_goal_is_immutable"; return false; }
            if (context.confirmationPolicy == GoalConfirmationPolicy.AutomaticOnValidatedDetection
                && (evidence == null || string.IsNullOrWhiteSpace(evidence.turnId) || string.IsNullOrWhiteSpace(evidence.transcript)))
            { error = "validated_goal_evidence_required"; return false; }
            evidence ??= new GoalEvidence();
            if (evidence.confidence < 0f || evidence.confidence > 1f) { error = "goal_evidence_confidence_invalid"; return false; }
            if (goal.state == GoalProgressState.Candidate
                && string.Equals(goal.evidenceTurnId, evidence.turnId, StringComparison.Ordinal)
                && string.Equals(goal.evidenceTranscript, evidence.transcript, StringComparison.Ordinal))
            { error = "duplicate_goal_evidence"; return false; }

            var oldState = goal.state;
            goal.state = GoalProgressState.Candidate;
            goal.candidateSource = string.IsNullOrWhiteSpace(source) ? "system" : source.Trim();
            goal.evidenceTurnId = evidence.turnId?.Trim() ?? string.Empty;
            goal.evidenceTranscript = evidence.transcript?.Trim() ?? string.Empty;
            goal.candidateEvidence = $"turnId={goal.evidenceTurnId};confidence={evidence.confidence:0.###};transcript={goal.evidenceTranscript}";
            goal.candidateAt = goal.candidateAtUtc = DateTime.UtcNow.ToString("o");
            goal.rejectionReason = string.Empty;
            goal.revision++;
            Publish(goal, oldState, GoalProgressState.Candidate, goal.candidateSource, "candidate");

            if (context.confirmationPolicy == GoalConfirmationPolicy.AutomaticOnValidatedDetection)
                return ConfirmGoal(goalId, AutomaticConfirmationActor, "policy=automatic_validated_detection", out error);
            error = string.Empty;
            return true;
        }

        public bool ConfirmGoal(string goalId, string experimenterId, string note, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (goal.state != GoalProgressState.Candidate) { error = "only_candidate_can_be_confirmed"; return false; }
            var oldState = goal.state;
            goal.state = GoalProgressState.Confirmed;
            goal.confirmedAt = goal.confirmedAtUtc = DateTime.UtcNow.ToString("o");
            goal.confirmedBy = experimenterId.Trim();
            if (!string.IsNullOrWhiteSpace(note)) goal.rejectionReason = "note:" + note.Trim();
            goal.revision++;
            Publish(goal, oldState, GoalProgressState.Confirmed, goal.confirmedBy, "confirmed");
            error = string.Empty;
            return true;
        }

        public bool RejectGoal(string goalId, string experimenterId, string reason, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (goal.state != GoalProgressState.Candidate) { error = "only_candidate_can_be_rejected"; return false; }
            var oldState = goal.state;
            goal.state = GoalProgressState.Rejected;
            goal.confirmedBy = experimenterId.Trim();
            goal.rejectionReason = reason ?? string.Empty;
            goal.revision++;
            Publish(goal, oldState, GoalProgressState.Rejected, goal.confirmedBy, "rejected");
            error = string.Empty;
            return true;
        }

        public float GetCompletionRate() => goals.Count == 0 ? 0f : ConfirmedCount / (float)goals.Count;
        public int ConfirmedCount => goals.Count(g => g.state == GoalProgressState.Confirmed);
        public bool AreAllConfirmed => goals.Count > 0 && ConfirmedCount == goals.Count;

        private void Publish(GoalProgressRecord goal, GoalProgressState oldState, GoalProgressState newState, string actor, string legacyAction)
        {
            GoalChanged?.Invoke(goal, legacyAction);
            var payload = CreatePayload(goal, oldState, newState, actor);
            OnGoalStateChanged?.Invoke(payload);
            OnGoalProgressChanged?.Invoke(payload);
            if (AreAllConfirmed && !allConfirmedRaised)
            {
                allConfirmedRaised = true;
                OnAllGoalsConfirmed?.Invoke(payload);
            }
        }

        private GoalProgressChangedEvent CreatePayload(GoalProgressRecord goal, GoalProgressState oldState, GoalProgressState newState, string actor) =>
            new GoalProgressChangedEvent
            {
                participantId = context.participantId ?? string.Empty,
                sessionId = context.sessionId ?? string.Empty,
                conditionRunId = context.conditionRunId ?? string.Empty,
                taskAssignmentId = context.taskAssignmentId ?? string.Empty,
                taskId = context.taskId ?? string.Empty,
                goalId = goal?.goalId ?? string.Empty,
                oldState = oldState,
                newState = newState,
                confirmedCount = ConfirmedCount,
                totalCount = goals.Count,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                actor = actor ?? string.Empty,
                revision = goal?.revision ?? 0
            };

        private GoalProgressRecord Find(string id) => goals.FirstOrDefault(g => string.Equals(g.goalId, id, StringComparison.Ordinal));
    }
}
