using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneTalkVR.Core
{
    public enum GoalProgressState { NotStarted, Candidate, Confirmed, Rejected }
    public enum GoalConfirmationPolicy { ExperimenterReview, AutomaticOnValidatedDetection }
    public enum GoalSequencePolicy
    {
        Undefined = 0,
        SequentialAfterParticipantTurnAndAvatarReply = 1,
        SequentialAfterConfirmationWithFinalReplyCompletion = 2
    }

    public enum GoalSequenceState
    {
        Inactive = 0,
        ActiveGoal = 1,
        AwaitingAvatarReply = 2,
        Completed = 3,
        AwaitingParticipantTurn = 4
    }

    [Serializable]
    public sealed class GoalSequenceSnapshot
    {
        public const string CurrentSchemaVersion = "4.0";
        public string schemaVersion = CurrentSchemaVersion;
        public GoalSequenceState state;
        public int activeGoalIndex = -1;
        public int sequenceRevision;
        public string pendingCompletionTurnId;

        // Schema 2 compatibility. New snapshots leave this empty.
        public string pendingEvidenceTurnId;
    }

    [Serializable]
    public sealed class GoalEvidence
    {
        public string turnId;
        public string transcript;
        public float confidence = 1f;
        public string evaluatorVersion;
        public string evaluationReason;
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
        public GoalSequencePolicy sequencePolicy = GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion;
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
        public float confidence;
        public string evaluatorVersion;
        public string confirmationPolicy;
        public string evaluationReason;
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
        public GoalSequenceState sequenceState;
        public int activeGoalIndex;
        public string activeGoalId;
        public int sequenceRevision;
        public string unlockTurnId;
    }

    public sealed class GoalProgressTracker
    {
        public const string AutomaticConfirmationActor = "system_goal_evaluator";
        private readonly List<GoalProgressRecord> goals = new List<GoalProgressRecord>();
        private readonly HashSet<string> observedParticipantTurns = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedDialogueTurns = new HashSet<string>(StringComparer.Ordinal);
        private GoalTrackingContext context = new GoalTrackingContext();
        private bool allConfirmedRaised;
        private GoalSequenceState sequenceState = GoalSequenceState.Inactive;
        private int activeGoalIndex = -1;
        private int sequenceRevision;
        private string pendingCompletionTurnId = string.Empty;

        public IReadOnlyList<GoalProgressRecord> Goals => goals;
        public GoalTrackingContext Context => context;
        public GoalSequenceState SequenceState => sequenceState;
        public int ActiveGoalIndex => activeGoalIndex;
        public int SequenceRevision => sequenceRevision;
        public string PendingCompletionTurnId => pendingCompletionTurnId;
        public GoalProgressRecord ActiveGoal => activeGoalIndex >= 0 && activeGoalIndex < goals.Count
            ? goals[activeGoalIndex]
            : null;
        public bool IsSequenceCompleted => sequenceState == GoalSequenceState.Completed;
        public event Action<GoalProgressRecord, string> GoalChanged;
        public event Action<GoalProgressChangedEvent> OnGoalStateChanged;
        public event Action<GoalProgressChangedEvent> OnGoalCollectionReset;
        public event Action<GoalProgressChangedEvent> OnGoalProgressChanged;
        public event Action<GoalProgressChangedEvent> OnGoalSequenceStateChanged;
        public event Action<GoalProgressChangedEvent> OnAllGoalsConfirmed;

        public void ResetGoals(ExperimentTaskDefinition task) => ResetGoals(task, null);

        public void ResetGoals(ExperimentTaskDefinition task, GoalTrackingContext trackingContext)
        {
            goals.Clear();
            observedParticipantTurns.Clear();
            completedDialogueTurns.Clear();
            context = trackingContext ?? new GoalTrackingContext();
            allConfirmedRaised = false;
            pendingCompletionTurnId = string.Empty;
            if (task?.goals != null)
            {
                context.taskId = task.taskId;
                for (var i = 0; i < task.goals.Length; i++)
                    goals.Add(new GoalProgressRecord
                    {
                        goalId = string.IsNullOrWhiteSpace(task.goals[i]?.goalId) ? $"{task.taskId}.goal.{i + 1}" : task.goals[i].goalId,
                        goalText = task.goals[i]?.text ?? string.Empty,
                        state = GoalProgressState.NotStarted,
                        conditionRunId = context.conditionRunId ?? string.Empty,
                        taskAssignmentId = context.taskAssignmentId ?? string.Empty,
                        revision = 0
                    });
            }
            activeGoalIndex = goals.Count > 0 ? 0 : -1;
            sequenceState = goals.Count > 0 ? GoalSequenceState.ActiveGoal : GoalSequenceState.Inactive;
            sequenceRevision = goals.Count > 0 ? 1 : 0;
            var payload = CreatePayload(null, GoalProgressState.NotStarted, GoalProgressState.NotStarted, "system");
            OnGoalCollectionReset?.Invoke(payload);
            OnGoalSequenceStateChanged?.Invoke(payload);
            OnGoalProgressChanged?.Invoke(payload);
        }

        public bool SubmitGoalCandidate(string goalId, string source, GoalEvidence evidence, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (goal.state == GoalProgressState.Confirmed) { error = "confirmed_goal_is_immutable"; return false; }
            if (!IsActiveGoal(goal)) { error = "goal_is_not_active"; return false; }
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
            goal.confidence = evidence.confidence;
            goal.evaluatorVersion = string.IsNullOrWhiteSpace(evidence.evaluatorVersion) ? goal.candidateSource : evidence.evaluatorVersion.Trim();
            goal.evaluationReason = evidence.evaluationReason?.Trim() ?? string.Empty;
            goal.confirmationPolicy = context.confirmationPolicy == GoalConfirmationPolicy.AutomaticOnValidatedDetection
                ? "automatic_validated_detection" : "experimenter_review";
            goal.rejectionReason = string.Empty;
            goal.revision++;
            Publish(goal, oldState, GoalProgressState.Candidate, goal.candidateSource, "candidate");

            if (context.confirmationPolicy == GoalConfirmationPolicy.AutomaticOnValidatedDetection)
                return ConfirmGoal(goalId, AutomaticConfirmationActor, "policy=automatic_validated_detection", out error);
            error = string.Empty;
            return true;
        }

        public void RestoreGoals(ExperimentTaskDefinition task, GoalTrackingContext trackingContext,
            IEnumerable<GoalProgressRecord> restored) => RestoreGoals(task, trackingContext, restored, null);

        public void RestoreGoals(ExperimentTaskDefinition task, GoalTrackingContext trackingContext,
            IEnumerable<GoalProgressRecord> restored, GoalSequenceSnapshot restoredSequence)
        {
            ResetGoals(task, trackingContext);
            if (restored != null)
            {
                foreach (var source in restored)
                {
                    var target = Find(source?.goalId);
                    if (target == null || source == null) continue;
                    target.state = source.state;
                    target.candidateEvidence = source.candidateEvidence;
                    target.candidateAt = source.candidateAt;
                    target.confirmedAt = source.confirmedAt;
                    target.confirmedBy = source.confirmedBy;
                    target.candidateSource = source.candidateSource;
                    target.evidenceTurnId = source.evidenceTurnId;
                    target.evidenceTranscript = source.evidenceTranscript;
                    target.candidateAtUtc = source.candidateAtUtc;
                    target.confirmedAtUtc = source.confirmedAtUtc;
                    target.rejectionReason = source.rejectionReason;
                    target.confidence = source.confidence;
                    target.evaluatorVersion = source.evaluatorVersion;
                    target.confirmationPolicy = source.confirmationPolicy;
                    target.evaluationReason = source.evaluationReason;
                    target.revision = source.revision;
                }
            }
            RestoreSequence(restoredSequence);
            allConfirmedRaised = IsSequenceCompleted;
            var payload = CreatePayload(ActiveGoal, GoalProgressState.NotStarted, ActiveGoal?.state ?? GoalProgressState.NotStarted, "system_resume");
            OnGoalSequenceStateChanged?.Invoke(payload);
            OnGoalProgressChanged?.Invoke(payload);
        }

        public bool ConfirmGoal(string goalId, string experimenterId, string note, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (!IsActiveGoal(goal)) { error = "goal_is_not_active"; return false; }
            if (goal.state != GoalProgressState.Candidate) { error = "only_candidate_can_be_confirmed"; return false; }
            var oldState = goal.state;
            goal.state = GoalProgressState.Confirmed;
            goal.confirmedAt = goal.confirmedAtUtc = DateTime.UtcNow.ToString("o");
            goal.confirmedBy = experimenterId.Trim();
            if (!string.IsNullOrWhiteSpace(note)) goal.rejectionReason = "note:" + note.Trim();
            goal.revision++;
            var finalGoal = AreAllConfirmed;
            var evidenceTurnId = goal.evidenceTurnId?.Trim() ?? string.Empty;
            if (context.sequencePolicy == GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion)
            {
                pendingCompletionTurnId = string.Empty;
                if (!finalGoal)
                {
                    activeGoalIndex = FindNextUnconfirmedGoal(activeGoalIndex + 1);
                    if (activeGoalIndex < 0) activeGoalIndex = FindNextUnconfirmedGoal(0);
                    sequenceState = GoalSequenceState.ActiveGoal;
                    sequenceRevision++;
                    Publish(goal, oldState, GoalProgressState.Confirmed, goal.confirmedBy,
                        "confirmed", true, evidenceTurnId);
                    error = string.Empty;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(evidenceTurnId))
                {
                    sequenceState = GoalSequenceState.AwaitingAvatarReply;
                    pendingCompletionTurnId = evidenceTurnId;
                    sequenceRevision++;
                    Publish(goal, oldState, GoalProgressState.Confirmed, goal.confirmedBy,
                        "confirmed", true, evidenceTurnId);
                    if (completedDialogueTurns.Contains(pendingCompletionTurnId))
                        AdvanceAfterAvatarReply(pendingCompletionTurnId);
                }
                else
                {
                    activeGoalIndex = -1;
                    sequenceState = GoalSequenceState.Completed;
                    sequenceRevision++;
                    Publish(goal, oldState, GoalProgressState.Confirmed, goal.confirmedBy,
                        "confirmed", true);
                    RaiseAllGoalsConfirmed(CreatePayload(goal, oldState,
                        GoalProgressState.Confirmed, goal.confirmedBy));
                }
                error = string.Empty;
                return true;
            }

            var canFinishOnEvidenceReply = finalGoal && !string.IsNullOrWhiteSpace(evidenceTurnId);
            sequenceState = canFinishOnEvidenceReply
                ? GoalSequenceState.AwaitingAvatarReply
                : GoalSequenceState.AwaitingParticipantTurn;
            pendingCompletionTurnId = canFinishOnEvidenceReply ? evidenceTurnId : string.Empty;
            sequenceRevision++;
            Publish(goal, oldState, GoalProgressState.Confirmed, goal.confirmedBy, "confirmed", true);
            if (canFinishOnEvidenceReply
                && completedDialogueTurns.Contains(pendingCompletionTurnId))
                AdvanceAfterAvatarReply(pendingCompletionTurnId);
            error = string.Empty;
            return true;
        }

        public bool ConfirmGoalByExperimenter(string goalId, string experimenterId, string note, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (goal.state == GoalProgressState.Confirmed) { error = "goal_already_confirmed"; return false; }
            if (!IsActiveGoal(goal)) { error = "goal_is_not_active"; return false; }
            if (goal.state != GoalProgressState.Candidate)
            {
                var oldState = goal.state;
                goal.state = GoalProgressState.Candidate;
                goal.candidateSource = "experimenter_review";
                goal.candidateEvidence = note?.Trim() ?? string.Empty;
                goal.candidateAt = goal.candidateAtUtc = DateTime.UtcNow.ToString("o");
                goal.confirmationPolicy = "experimenter_review";
                goal.revision++;
                Publish(goal, oldState, GoalProgressState.Candidate, experimenterId.Trim(), "candidate");
            }
            return ConfirmGoal(goalId, experimenterId, note, out error);
        }

        public bool RejectGoal(string goalId, string experimenterId, string reason, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (!IsActiveGoal(goal)) { error = "goal_is_not_active"; return false; }
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

        public bool UndoGoal(string goalId, string experimenterId, string reason, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (goal.state != GoalProgressState.Confirmed) { error = "only_confirmed_goal_can_be_undone"; return false; }
            var rollbackIndex = goals.IndexOf(goal);
            var oldState = goal.state;
            for (var i = rollbackIndex; i < goals.Count; i++) ResetRecordForRollback(goals[i]);
            goal.confirmedBy = experimenterId.Trim();
            goal.rejectionReason = reason ?? string.Empty;
            activeGoalIndex = rollbackIndex;
            sequenceState = GoalSequenceState.ActiveGoal;
            pendingCompletionTurnId = string.Empty;
            sequenceRevision++;
            allConfirmedRaised = false;
            Publish(goal, oldState, GoalProgressState.NotStarted, goal.confirmedBy, "undo", true);
            error = string.Empty;
            return true;
        }

        public bool IsGoalActive(string goalId) => sequenceState == GoalSequenceState.ActiveGoal
            && ActiveGoal != null
            && string.Equals(ActiveGoal.goalId, goalId, StringComparison.Ordinal);

        public bool NotifyParticipantTurnSubmitted(string turnId)
        {
            var normalized = turnId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized) || !observedParticipantTurns.Add(normalized)) return false;
            if (sequenceState != GoalSequenceState.AwaitingParticipantTurn
                && sequenceState != GoalSequenceState.AwaitingAvatarReply) return false;
            if (ActiveGoal == null || ActiveGoal.state != GoalProgressState.Confirmed) return false;
            if (string.Equals(normalized, ActiveGoal.evidenceTurnId?.Trim(), StringComparison.Ordinal)) return false;

            pendingCompletionTurnId = normalized;
            sequenceState = GoalSequenceState.AwaitingAvatarReply;
            sequenceRevision++;
            PublishSequenceChange(ActiveGoal, "participant_turn_submitted", normalized);
            return true;
        }

        public bool NotifyDialogueTurnCompleted(string turnId)
        {
            var normalized = turnId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized) || !completedDialogueTurns.Add(normalized)) return false;
            if (sequenceState != GoalSequenceState.AwaitingAvatarReply) return false;
            return string.Equals(normalized, pendingCompletionTurnId, StringComparison.Ordinal)
                && AdvanceAfterAvatarReply(normalized);
        }

        public GoalSequenceSnapshot CaptureSequenceSnapshot() => new GoalSequenceSnapshot
        {
            state = sequenceState,
            activeGoalIndex = activeGoalIndex,
            sequenceRevision = sequenceRevision,
            pendingCompletionTurnId = pendingCompletionTurnId ?? string.Empty,
            pendingEvidenceTurnId = string.Empty
        };

        public float GetCompletionRate() => goals.Count == 0 ? 0f : ConfirmedCount / (float)goals.Count;
        public int ConfirmedCount => goals.Count(g => g.state == GoalProgressState.Confirmed);
        public bool AreAllConfirmed => goals.Count > 0 && ConfirmedCount == goals.Count;

        private void Publish(GoalProgressRecord goal, GoalProgressState oldState, GoalProgressState newState,
            string actor, string legacyAction, bool sequenceChanged = false, string unlockTurnId = "")
        {
            GoalChanged?.Invoke(goal, legacyAction);
            var payload = CreatePayload(goal, oldState, newState, actor, unlockTurnId);
            OnGoalStateChanged?.Invoke(payload);
            if (sequenceChanged) OnGoalSequenceStateChanged?.Invoke(payload);
            OnGoalProgressChanged?.Invoke(payload);
        }

        private GoalProgressChangedEvent CreatePayload(GoalProgressRecord goal, GoalProgressState oldState,
            GoalProgressState newState, string actor, string unlockTurnId = "") =>
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
                revision = goal?.revision ?? 0,
                sequenceState = sequenceState,
                activeGoalIndex = activeGoalIndex,
                activeGoalId = ActiveGoal?.goalId ?? string.Empty,
                sequenceRevision = sequenceRevision,
                unlockTurnId = unlockTurnId ?? string.Empty
            };

        private bool AdvanceAfterAvatarReply(string turnId)
        {
            if (sequenceState != GoalSequenceState.AwaitingAvatarReply || ActiveGoal == null) return false;
            var completedGoal = ActiveGoal;
            pendingCompletionTurnId = string.Empty;
            var nextIndex = FindNextUnconfirmedGoal(activeGoalIndex + 1);
            if (nextIndex < 0) nextIndex = FindNextUnconfirmedGoal(0);
            if (nextIndex >= 0)
            {
                activeGoalIndex = nextIndex;
                sequenceState = GoalSequenceState.ActiveGoal;
                sequenceRevision++;
                PublishSequenceChange(ActiveGoal, "system_turn_completed", turnId);
                return true;
            }

            if (!AreAllConfirmed) return false;
            activeGoalIndex = -1;
            sequenceState = GoalSequenceState.Completed;
            sequenceRevision++;
            var payload = PublishSequenceChange(completedGoal, "system_turn_completed", turnId);
            if (!allConfirmedRaised)
                RaiseAllGoalsConfirmed(payload);
            return true;
        }

        private void RaiseAllGoalsConfirmed(GoalProgressChangedEvent payload)
        {
            if (allConfirmedRaised) return;
            allConfirmedRaised = true;
            OnAllGoalsConfirmed?.Invoke(payload);
        }

        private GoalProgressChangedEvent PublishSequenceChange(GoalProgressRecord goal, string actor, string turnId)
        {
            var state = goal?.state ?? GoalProgressState.NotStarted;
            var payload = CreatePayload(goal, state, state, actor, turnId);
            OnGoalSequenceStateChanged?.Invoke(payload);
            OnGoalProgressChanged?.Invoke(payload);
            return payload;
        }

        private void RestoreSequence(GoalSequenceSnapshot snapshot)
        {
            observedParticipantTurns.Clear();
            completedDialogueTurns.Clear();
            if (goals.Count == 0)
            {
                activeGoalIndex = -1;
                sequenceState = GoalSequenceState.Inactive;
                sequenceRevision = 0;
                pendingCompletionTurnId = string.Empty;
                return;
            }

            if (snapshot != null
                && string.Equals(snapshot.schemaVersion, GoalSequenceSnapshot.CurrentSchemaVersion, StringComparison.Ordinal)
                && IsValidRestoredSequence(snapshot))
            {
                if (context.sequencePolicy == GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion)
                {
                    RestoreImmediateSequence(snapshot.sequenceRevision);
                    return;
                }
                activeGoalIndex = snapshot.activeGoalIndex;
                sequenceRevision = Math.Max(1, snapshot.sequenceRevision);
                if (snapshot.state == GoalSequenceState.AwaitingAvatarReply)
                {
                    // In-flight playback cannot survive a process restart. Legacy sessions
                    // require a fresh participant turn before continuing.
                    sequenceState = GoalSequenceState.AwaitingParticipantTurn;
                    pendingCompletionTurnId = string.Empty;
                }
                else
                {
                    sequenceState = snapshot.state;
                    pendingCompletionTurnId = string.Empty;
                }
                return;
            }

            if (snapshot != null
                && string.Equals(snapshot.schemaVersion, "3.0", StringComparison.Ordinal)
                && RestoreSchemaThreeSequence(snapshot))
            {
                return;
            }

            if (snapshot != null
                && string.Equals(snapshot.schemaVersion, "2.0", StringComparison.Ordinal)
                && RestoreSchemaTwoSequence(snapshot))
            {
                return;
            }

            var nextUnconfirmed = FindNextUnconfirmedGoal(0);
            if (nextUnconfirmed < 0)
            {
                activeGoalIndex = -1;
                sequenceState = GoalSequenceState.Completed;
            }
            else if (nextUnconfirmed == 0
                || context.sequencePolicy == GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion)
            {
                activeGoalIndex = nextUnconfirmed;
                sequenceState = GoalSequenceState.ActiveGoal;
            }
            else
            {
                activeGoalIndex = nextUnconfirmed - 1;
                sequenceState = GoalSequenceState.AwaitingParticipantTurn;
            }
            sequenceRevision = Math.Max(1, goals.Sum(goal => Math.Max(0, goal.revision)) + 1);
            pendingCompletionTurnId = string.Empty;
        }

        private bool RestoreSchemaThreeSequence(GoalSequenceSnapshot snapshot)
        {
            if (context.sequencePolicy == GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion)
            {
                RestoreImmediateSequence(snapshot.sequenceRevision);
                return true;
            }

            if (!IsValidRestoredSequence(snapshot)) return false;
            activeGoalIndex = snapshot.activeGoalIndex;
            sequenceRevision = Math.Max(1, snapshot.sequenceRevision);
            sequenceState = snapshot.state == GoalSequenceState.AwaitingAvatarReply
                ? GoalSequenceState.AwaitingParticipantTurn
                : snapshot.state;
            pendingCompletionTurnId = string.Empty;
            return true;
        }

        private bool IsValidRestoredSequence(GoalSequenceSnapshot snapshot)
        {
            if (snapshot.state == GoalSequenceState.Completed)
                return snapshot.activeGoalIndex == -1 && AreAllConfirmed;
            if (snapshot.state != GoalSequenceState.ActiveGoal
                && snapshot.state != GoalSequenceState.AwaitingParticipantTurn
                && snapshot.state != GoalSequenceState.AwaitingAvatarReply) return false;
            if (snapshot.activeGoalIndex < 0 || snapshot.activeGoalIndex >= goals.Count) return false;
            if (snapshot.state == GoalSequenceState.ActiveGoal)
                return goals[snapshot.activeGoalIndex].state != GoalProgressState.Confirmed;
            if (goals[snapshot.activeGoalIndex].state != GoalProgressState.Confirmed) return false;
            return snapshot.state != GoalSequenceState.AwaitingAvatarReply
                || !string.IsNullOrWhiteSpace(snapshot.pendingCompletionTurnId);
        }

        private bool RestoreSchemaTwoSequence(GoalSequenceSnapshot snapshot)
        {
            if (context.sequencePolicy == GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion)
            {
                RestoreImmediateSequence(snapshot.sequenceRevision);
                return true;
            }
            if (snapshot.state == GoalSequenceState.Completed)
            {
                if (snapshot.activeGoalIndex != -1 || !AreAllConfirmed) return false;
                activeGoalIndex = -1;
                sequenceState = GoalSequenceState.Completed;
            }
            else if (snapshot.state == GoalSequenceState.ActiveGoal)
            {
                if (snapshot.activeGoalIndex < 0 || snapshot.activeGoalIndex >= goals.Count
                    || goals[snapshot.activeGoalIndex].state == GoalProgressState.Confirmed) return false;
                activeGoalIndex = snapshot.activeGoalIndex;
                sequenceState = GoalSequenceState.ActiveGoal;
            }
            else if (snapshot.state == GoalSequenceState.AwaitingAvatarReply)
            {
                if (snapshot.activeGoalIndex < 0 || snapshot.activeGoalIndex >= goals.Count
                    || goals[snapshot.activeGoalIndex].state != GoalProgressState.Confirmed) return false;
                activeGoalIndex = snapshot.activeGoalIndex;
                sequenceState = GoalSequenceState.AwaitingParticipantTurn;
            }
            else
            {
                return false;
            }

            sequenceRevision = Math.Max(1, snapshot.sequenceRevision);
            pendingCompletionTurnId = string.Empty;
            return true;
        }

        private void RestoreImmediateSequence(int restoredRevision)
        {
            var nextUnconfirmed = FindNextUnconfirmedGoal(0);
            activeGoalIndex = nextUnconfirmed;
            sequenceState = nextUnconfirmed >= 0
                ? GoalSequenceState.ActiveGoal
                : GoalSequenceState.Completed;
            sequenceRevision = Math.Max(1, restoredRevision);
            pendingCompletionTurnId = string.Empty;
        }

        private int FindNextUnconfirmedGoal(int startIndex)
        {
            for (var i = Math.Max(0, startIndex); i < goals.Count; i++)
                if (goals[i].state != GoalProgressState.Confirmed) return i;
            return -1;
        }

        private bool IsActiveGoal(GoalProgressRecord goal) => sequenceState == GoalSequenceState.ActiveGoal
            && ReferenceEquals(goal, ActiveGoal);

        private static void ResetRecordForRollback(GoalProgressRecord goal)
        {
            goal.state = GoalProgressState.NotStarted;
            goal.candidateEvidence = string.Empty;
            goal.candidateAt = string.Empty;
            goal.confirmedAt = string.Empty;
            goal.candidateSource = string.Empty;
            goal.evidenceTurnId = string.Empty;
            goal.evidenceTranscript = string.Empty;
            goal.candidateAtUtc = string.Empty;
            goal.confirmedAtUtc = string.Empty;
            goal.confirmedBy = string.Empty;
            goal.rejectionReason = string.Empty;
            goal.confidence = 0f;
            goal.evaluatorVersion = string.Empty;
            goal.confirmationPolicy = string.Empty;
            goal.evaluationReason = string.Empty;
            goal.revision++;
        }

        private GoalProgressRecord Find(string id) => goals.FirstOrDefault(g => string.Equals(g.goalId, id, StringComparison.Ordinal));
    }
}
