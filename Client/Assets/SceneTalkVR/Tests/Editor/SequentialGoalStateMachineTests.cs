using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class SequentialGoalStateMachineTests
    {
        [Test]
        public void Reset_ActivatesOnlyFirstGoal()
        {
            var tracker = CreateTracker();

            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.ActiveGoal));
            Assert.That(tracker.ActiveGoalIndex, Is.Zero);
            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_1"));
            Assert.That(tracker.Goals.Skip(1).All(goal => !tracker.IsGoalActive(goal.goalId)), Is.True);
        }

        [Test]
        public void LockedGoal_CannotParticipateInJudgment()
        {
            var tracker = CreateTracker();

            Assert.That(tracker.SubmitGoalCandidate("goal_2", "detector", Evidence("turn-1"), out var error), Is.False);
            Assert.That(error, Is.EqualTo("goal_is_not_active"));
            Assert.That(tracker.Goals[1].state, Is.EqualTo(GoalProgressState.NotStarted));
        }

        [Test]
        public void LockedGoal_CannotBeUsedToBypassSequenceThroughUndo()
        {
            var tracker = CreateTracker();

            Assert.That(tracker.UndoGoal("goal_3", "operator", "invalid jump", out var error), Is.False);
            Assert.That(error, Is.EqualTo("only_confirmed_goal_can_be_undone"));
            Assert.That(tracker.ActiveGoalIndex, Is.Zero);
            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_1"));
        }

        [Test]
        public void ConfirmedGoal_RequiresNewParticipantTurnAndMatchingAvatarReply()
        {
            var tracker = CreateTracker();

            Assert.That(tracker.NotifyParticipantTurnSubmitted("turn-1"), Is.False);
            Assert.That(tracker.SubmitGoalCandidate("goal_1", "detector", Evidence("turn-1"), out var error), Is.True, error);
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingParticipantTurn));
            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_1"));
            Assert.That(tracker.IsGoalActive("goal_2"), Is.False);

            Assert.That(tracker.NotifyDialogueTurnCompleted("turn-1"), Is.False,
                "The goal evidence turn must not unlock the next goal.");
            Assert.That(tracker.NotifyParticipantTurnSubmitted("turn-2"), Is.True);
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingAvatarReply));
            Assert.That(tracker.NotifyDialogueTurnCompleted("wrong-turn"), Is.False);
            Assert.That(tracker.NotifyDialogueTurnCompleted("turn-2"), Is.True);
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.ActiveGoal));
            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_2"));
        }

        [Test]
        public void LateEvaluation_DoesNotTreatCompletedEvidenceTurnAsUnlockDialogue()
        {
            var tracker = CreateTracker();

            Assert.That(tracker.NotifyParticipantTurnSubmitted("turn-1"), Is.False);
            Assert.That(tracker.NotifyDialogueTurnCompleted("turn-1"), Is.False);
            Assert.That(tracker.SubmitGoalCandidate("goal_1", "detector", Evidence("turn-1"), out var error), Is.True, error);

            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_1"));
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingParticipantTurn));
        }

        [UnityTest]
        public IEnumerator OneRecordingContainingEveryAnswer_ConfirmsOnlyActiveGoal()
        {
            var task = Task();
            var tracker = CreateTracker(task);
            tracker.NotifyParticipantTurnSubmitted("turn-all");
            var request = new GoalEvaluationRequest
            {
                taskId = task.taskId,
                turnId = "turn-all",
                userTranscript = "alpha bravo charlie delta",
                recentUserTurns = new[] { "alpha bravo charlie delta" },
                currentGoalDefinitions = task.goals
            };

            yield return GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(
                request,
                tracker,
                () => true,
                () => false,
                _ => { });

            Assert.That(tracker.ConfirmedCount, Is.EqualTo(1));
            Assert.That(tracker.Goals[0].state, Is.EqualTo(GoalProgressState.Confirmed));
            Assert.That(tracker.Goals.Skip(1).All(goal => goal.state == GoalProgressState.NotStarted), Is.True);
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingParticipantTurn));
        }

        [Test]
        public void AwaitingAvatarSnapshot_RestoresAsFreshParticipantGate()
        {
            var task = Task();
            var tracker = CreateTracker(task);
            tracker.SubmitGoalCandidate("goal_1", "detector", Evidence("turn-failed"), out _);
            tracker.NotifyParticipantTurnSubmitted("turn-interrupted");
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingAvatarReply));
            var records = tracker.Goals.ToArray();
            var snapshot = tracker.CaptureSequenceSnapshot();

            var restored = CreateTracker(task);
            restored.RestoreGoals(task, Context(), records, snapshot);

            Assert.That(restored.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingParticipantTurn));
            Assert.That(restored.ActiveGoal.goalId, Is.EqualTo("goal_1"));
            Assert.That(restored.NotifyDialogueTurnCompleted("stale-turn"), Is.False);
            Assert.That(restored.NotifyParticipantTurnSubmitted("turn-retry"), Is.True);
            Assert.That(restored.NotifyDialogueTurnCompleted("turn-retry"), Is.True);
            Assert.That(restored.ActiveGoal.goalId, Is.EqualTo("goal_2"));
        }

        [Test]
        public void LegacyRecords_RequireFreshDialogueBeforeFirstUnconfirmedGoal()
        {
            var task = Task();
            var source = CreateTracker(task);
            source.SubmitGoalCandidate("goal_1", "detector", Evidence("turn-1"), out _);

            var restored = CreateTracker(task);
            restored.RestoreGoals(task, Context(), source.Goals.ToArray());

            Assert.That(restored.SequenceState, Is.EqualTo(GoalSequenceState.AwaitingParticipantTurn));
            Assert.That(restored.ActiveGoal.goalId, Is.EqualTo("goal_1"));
            Assert.That(restored.NotifyParticipantTurnSubmitted("legacy-unlock"), Is.True);
            Assert.That(restored.NotifyDialogueTurnCompleted("legacy-unlock"), Is.True);
            Assert.That(restored.ActiveGoal.goalId, Is.EqualTo("goal_2"));
        }

        [Test]
        public void FailedAvatarReply_AllowsLaterParticipantTurnToReplacePendingTurn()
        {
            var tracker = CreateTracker();
            tracker.SubmitGoalCandidate("goal_1", "detector", Evidence("turn-1"), out _);

            Assert.That(tracker.NotifyParticipantTurnSubmitted("unlock-failed"), Is.True);
            Assert.That(tracker.NotifyParticipantTurnSubmitted("unlock-retry"), Is.True);
            Assert.That(tracker.PendingCompletionTurnId, Is.EqualTo("unlock-retry"));
            Assert.That(tracker.NotifyDialogueTurnCompleted("unlock-failed"), Is.False);
            Assert.That(tracker.NotifyDialogueTurnCompleted("unlock-retry"), Is.True);
            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_2"));
        }

        [Test]
        public void UndoEarlierGoal_RelocksAndClearsEveryLaterGoal()
        {
            var tracker = CreateTracker();
            CompleteActiveGoal(tracker, "turn-1");
            CompleteActiveGoal(tracker, "turn-2");

            Assert.That(tracker.UndoGoal("goal_1", "operator", "review", out var error), Is.True, error);
            Assert.That(tracker.ActiveGoal.goalId, Is.EqualTo("goal_1"));
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.ActiveGoal));
            Assert.That(tracker.Goals.All(goal => goal.state == GoalProgressState.NotStarted), Is.True);
        }

        [Test]
        public void FinalGoal_FiresCompletionOnlyAfterFinalAvatarReply()
        {
            var tracker = CreateTracker();
            var completedEvents = 0;
            tracker.OnAllGoalsConfirmed += _ => completedEvents++;
            for (var i = 0; i < 3; i++) CompleteActiveGoal(tracker, "turn-" + i);

            tracker.NotifyParticipantTurnSubmitted("turn-final");
            tracker.SubmitGoalCandidate("goal_4", "detector", Evidence("turn-final"), out _);
            Assert.That(tracker.AreAllConfirmed, Is.True);
            Assert.That(tracker.IsSequenceCompleted, Is.False);
            Assert.That(completedEvents, Is.Zero);

            tracker.NotifyDialogueTurnCompleted("turn-final");
            Assert.That(tracker.IsSequenceCompleted, Is.True);
            Assert.That(completedEvents, Is.EqualTo(1));
            Assert.That(tracker.NotifyDialogueTurnCompleted("turn-final"), Is.False);
            Assert.That(completedEvents, Is.EqualTo(1));
        }

        private static void CompleteActiveGoal(GoalProgressTracker tracker, string turnId)
        {
            tracker.NotifyParticipantTurnSubmitted(turnId);
            Assert.That(tracker.SubmitGoalCandidate(tracker.ActiveGoal.goalId, "detector", Evidence(turnId), out var error), Is.True, error);
            if (tracker.SequenceState == GoalSequenceState.AwaitingParticipantTurn)
            {
                Assert.That(tracker.NotifyDialogueTurnCompleted(turnId), Is.False);
                var unlockTurnId = turnId + "-unlock";
                Assert.That(tracker.NotifyParticipantTurnSubmitted(unlockTurnId), Is.True);
                Assert.That(tracker.NotifyDialogueTurnCompleted(unlockTurnId), Is.True);
            }
            else
            {
                Assert.That(tracker.NotifyDialogueTurnCompleted(turnId), Is.True);
            }
        }

        private static GoalProgressTracker CreateTracker(ExperimentTaskDefinition task = null)
        {
            var tracker = new GoalProgressTracker();
            tracker.ResetGoals(task ?? Task(), Context());
            return tracker;
        }

        private static GoalTrackingContext Context() => new GoalTrackingContext
        {
            participantId = "participant",
            sessionId = "session",
            conditionRunId = "run",
            taskAssignmentId = "assignment",
            confirmationPolicy = GoalConfirmationPolicy.AutomaticOnValidatedDetection,
            sequencePolicy = GoalSequencePolicy.SequentialAfterParticipantTurnAndAvatarReply
        };

        private static GoalEvidence Evidence(string turnId) => new GoalEvidence
        {
            turnId = turnId,
            transcript = "validated participant speech",
            confidence = .98f,
            evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion
        };

        private static ExperimentTaskDefinition Task() => new ExperimentTaskDefinition
        {
            taskId = "sequential_test",
            goals = new[]
            {
                Goal("goal_1", "alpha"),
                Goal("goal_2", "bravo"),
                Goal("goal_3", "charlie"),
                Goal("goal_4", "delta")
            }
        };

        private static ExperimentTaskGoal Goal(string id, string pattern) => new ExperimentTaskGoal
        {
            goalId = id,
            text = id,
            evaluationIntent = id,
            deterministicPatterns = new[] { pattern },
            minimumConfidence = .85f
        };
    }
}
