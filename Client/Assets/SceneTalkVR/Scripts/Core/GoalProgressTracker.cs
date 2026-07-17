using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneTalkVR.Core
{
    public enum GoalProgressState { NotStarted, Candidate, Confirmed, Rejected }

    [Serializable]
    public sealed class GoalEvidence
    {
        public string turnId;
        public string transcript;
    }

    [Serializable]
    public sealed class GoalProgressRecord
    {
        public string goalId;
        public string goalText;
        public GoalProgressState state;
        public string candidateSource;
        public string evidenceTurnId;
        public string evidenceTranscript;
        public string candidateAtUtc;
        public string confirmedAtUtc;
        public string confirmedBy;
        public string rejectionReason;
    }

    public sealed class GoalProgressTracker
    {
        private readonly List<GoalProgressRecord> goals = new List<GoalProgressRecord>();
        public IReadOnlyList<GoalProgressRecord> Goals => goals;
        public event Action<GoalProgressRecord, string> GoalChanged;

        public void ResetGoals(ExperimentTaskDefinition task)
        {
            goals.Clear();
            if (task?.goals == null) return;
            for (var i = 0; i < task.goals.Length; i++)
                goals.Add(new GoalProgressRecord
                {
                    goalId = $"{task.taskId}.goal.{i + 1}",
                    goalText = task.goals[i]?.text ?? string.Empty,
                    state = GoalProgressState.NotStarted
                });
        }

        public bool SubmitGoalCandidate(string goalId, string source, GoalEvidence evidence, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (goal.state == GoalProgressState.Confirmed) { error = "confirmed_goal_is_immutable"; return false; }
            goal.state = GoalProgressState.Candidate;
            goal.candidateSource = string.IsNullOrWhiteSpace(source) ? "system" : source;
            goal.evidenceTurnId = evidence?.turnId ?? string.Empty;
            goal.evidenceTranscript = evidence?.transcript ?? string.Empty;
            goal.candidateAtUtc = DateTime.UtcNow.ToString("o");
            goal.rejectionReason = string.Empty;
            GoalChanged?.Invoke(goal, "candidate");
            error = string.Empty;
            return true;
        }

        public bool ConfirmGoal(string goalId, string experimenterId, string note, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (goal.state != GoalProgressState.Candidate) { error = "only_candidate_can_be_confirmed"; return false; }
            goal.state = GoalProgressState.Confirmed;
            goal.confirmedAtUtc = DateTime.UtcNow.ToString("o");
            goal.confirmedBy = experimenterId.Trim();
            if (!string.IsNullOrWhiteSpace(note)) goal.rejectionReason = "note:" + note.Trim();
            GoalChanged?.Invoke(goal, "confirmed");
            error = string.Empty;
            return true;
        }

        public bool RejectGoal(string goalId, string experimenterId, string reason, out string error)
        {
            var goal = Find(goalId);
            if (goal == null) { error = "goal_not_found"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            if (goal.state != GoalProgressState.Candidate) { error = "only_candidate_can_be_rejected"; return false; }
            goal.state = GoalProgressState.Rejected;
            goal.confirmedBy = experimenterId.Trim();
            goal.rejectionReason = reason ?? string.Empty;
            GoalChanged?.Invoke(goal, "rejected");
            error = string.Empty;
            return true;
        }

        public float GetCompletionRate() => goals.Count == 0 ? 0f : goals.Count(g => g.state == GoalProgressState.Confirmed) / (float)goals.Count;
        public int ConfirmedCount => goals.Count(g => g.state == GoalProgressState.Confirmed);
        public bool AreAllConfirmed => goals.Count > 0 && ConfirmedCount == goals.Count;
        private GoalProgressRecord Find(string id) => goals.FirstOrDefault(g => string.Equals(g.goalId, id, StringComparison.Ordinal));
    }
}
