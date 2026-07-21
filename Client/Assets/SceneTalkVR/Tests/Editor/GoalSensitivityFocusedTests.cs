using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class GoalSensitivityFocusedTests
    {
        private ExperimentTaskCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(
                "Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
            GoalEvaluationOrchestrator.StructuredFallback = null;
            GoalEvaluationOrchestrator.AsyncStructuredFallback = null;
        }

        [TearDown]
        public void TearDown()
        {
            GoalEvaluationOrchestrator.StructuredFallback = null;
            GoalEvaluationOrchestrator.AsyncStructuredFallback = null;
        }

        [TestCase("hotel_check_in", "reservation_name", "The reservation should be in Zhang's name.")]
        [TestCase("pilot_restaurant_walk_in", "no_reservation", "I don't, uh, have a reservation.")]
        [TestCase("pilot_restaurant_walk_in", "party_size", "We're a group of three.")]
        [TestCase("pilot_restaurant_ordering", "recommendation", "Could you suggest something popular?")]
        [TestCase("pilot_restaurant_ordering", "dietary_restriction", "I can't eat nuts.")]
        [TestCase("pilot_restaurant_wrong_dish", "wrong_dish", "You brought me somebody else's order.")]
        public void NaturalApprovedExpressions_AreDeterministicallyDetected(string taskId, string goalId, string transcript)
        {
            Assert.That(Evaluate(taskId, goalId, transcript).achieved, Is.True);
        }

        [TestCase("furniture_shopping", "delivery", "I do not need home delivery.")]
        [TestCase("gym_membership", "trial", "I already used my free trial last year.")]
        [TestCase("pilot_restaurant_walk_in", "table_availability", "There is no table available.")]
        [TestCase("pilot_restaurant_ordering", "recommendation", "I do not want a recommendation.")]
        [TestCase("pilot_restaurant_wrong_dish", "wrong_dish", "This is not the wrong dish.")]
        public void NegatedOrAmbiguousMentions_AreDeferredInsteadOfConfirmed(string taskId, string goalId, string transcript)
        {
            var result = Evaluate(taskId, goalId, transcript);
            Assert.That(result.achieved, Is.False);
        }

        [Test]
        public void Normalize_HandlesUnicodePunctuationFillersContractionsAndNumbers()
        {
            Assert.That(GoalAchievementEvaluator.NormalizeForEvaluation("We’re, um, a group of three!"),
                Is.EqualTo("we are a group of 3"));
            Assert.That(GoalAchievementEvaluator.NormalizeForEvaluation("I can’t—uh—eat nuts."),
                Is.EqualTo("i cannot eat nuts"));
        }

        [Test]
        public void SameTurnEvaluation_IsRegisteredOnlyOnce()
        {
            var identity = "focused|" + Guid.NewGuid().ToString("N");
            Assert.That(GoalEvaluationOrchestrator.TryRegisterEvaluationTurn(identity), Is.True);
            Assert.That(GoalEvaluationOrchestrator.TryRegisterEvaluationTurn(identity), Is.False);
        }

        [Test]
        public void EvaluationStartedAudit_IsRaisedBeforeFirstCoroutineYield()
        {
            var task = catalog.Find("pilot_restaurant_ordering");
            var definition = task.goals.Single(x => x.goalId == "main_course");
            var audits = new List<GoalEvaluationAudit>();
            var routine = GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(
                Request(task, new[] { definition }, "I want the salmon entrée.", "turn-immediate"), Tracker(task),
                () => true, () => true, audits.Add);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(audits.Any(x => x.eventType == "GoalEvaluationStarted" && x.source == GoalEvaluatorSource.Deterministic), Is.True);
        }

        [UnityTest]
        public IEnumerator PlaybackFailure_DoesNotCancelAlreadyStartedSemanticEvaluation()
        {
            var task = catalog.Find("pilot_restaurant_ordering");
            var definition = task.goals.Single(x => x.goalId == "main_course");
            var tracker = Tracker(task);
            GoalEvaluationOrchestrator.AsyncStructuredFallback = new FakeAsyncFallback(.90f, "I want the salmon entrée.");
            yield return GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(
                Request(task, new[] { definition }, "I want the salmon entrée.", "turn-audio-failed"), tracker,
                () => true, () => false, null);
            Assert.That(tracker.Goals.Single(x => x.goalId == "main_course").state, Is.EqualTo(GoalProgressState.Confirmed));
        }

        [UnityTest]
        public IEnumerator UnifiedPipeline_UsesStructuredFallbackForUnmatchedPilotGoal_AtSemanticThreshold()
        {
            var task = catalog.Find("pilot_restaurant_ordering");
            var definition = task.goals.Single(x => x.goalId == "main_course");
            var tracker = Tracker(task);
            var fallback = new FakeAsyncFallback(.80f, "I want the salmon entrée.");
            GoalEvaluationOrchestrator.AsyncStructuredFallback = fallback;
            var request = Request(task, new[] { definition }, "I want the salmon entrée.", "turn-semantic");

            yield return GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(request, tracker,
                () => true, () => false, null);

            Assert.That(fallback.Called, Is.True);
            Assert.That(fallback.Request.currentGoalDefinitions.Select(x => x.goalId), Is.EqualTo(new[] { "main_course" }));
            Assert.That(tracker.Goals.Single(x => x.goalId == "main_course").state, Is.EqualTo(GoalProgressState.Confirmed));
        }

        [UnityTest]
        public IEnumerator StructuredFallback_ReceivesOnlyCurrentlyIncompleteGoals()
        {
            var task = catalog.Find("pilot_restaurant_ordering");
            var recommendation = task.goals.Single(x => x.goalId == "recommendation");
            var main = task.goals.Single(x => x.goalId == "main_course");
            var tracker = Tracker(task);
            tracker.SubmitGoalCandidate(recommendation.goalId, "test",
                new GoalEvidence { turnId = "prior", transcript = "What do you recommend?", confidence = .98f }, out _);
            var fallback = new FakeAsyncFallback(.80f, "I want the salmon entrée.");
            GoalEvaluationOrchestrator.AsyncStructuredFallback = fallback;

            yield return GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(
                Request(task, new[] { recommendation, main }, "I want the salmon entrée.", "turn-incomplete"), tracker,
                () => true, () => false, null);

            Assert.That(fallback.Request.currentGoalDefinitions.Select(x => x.goalId), Is.EqualTo(new[] { "main_course" }));
        }

        [UnityTest]
        public IEnumerator StructuredFallback_BelowThresholdOrWithoutEvidence_DoesNotConfirm()
        {
            var task = catalog.Find("pilot_restaurant_ordering");
            var definition = task.goals.Single(x => x.goalId == "main_course");
            foreach (var fallback in new[] { new FakeAsyncFallback(.74f, "I want the salmon entrée."), new FakeAsyncFallback(.90f, "") })
            {
                var tracker = Tracker(task);
                GoalEvaluationOrchestrator.AsyncStructuredFallback = fallback;
                yield return GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(
                    Request(task, new[] { definition }, "I want the salmon entrée.", Guid.NewGuid().ToString("N")), tracker,
                    () => true, () => false, null);
                Assert.That(tracker.Goals.Single(x => x.goalId == "main_course").state, Is.EqualTo(GoalProgressState.NotStarted));
            }
        }

        [UnityTest]
        public IEnumerator StaleAsyncResult_AfterRunReset_IsIgnored()
        {
            var task = catalog.Find("pilot_restaurant_ordering");
            var definition = task.goals.Single(x => x.goalId == "main_course");
            var tracker = Tracker(task);
            var current = true;
            GoalEvaluationOrchestrator.AsyncStructuredFallback = new FakeAsyncFallback(.90f, "I want the salmon entrée.", () => current = false);

            yield return GoalEvaluationOrchestrator.EvaluateActiveTaskGoalsAsync(
                Request(task, new[] { definition }, "I want the salmon entrée.", "turn-stale"), tracker,
                () => current, () => false, null);

            Assert.That(tracker.Goals.Single(x => x.goalId == "main_course").state, Is.EqualTo(GoalProgressState.NotStarted));
        }

        private GoalEvaluationItem Evaluate(string taskId, string goalId, string transcript)
        {
            var task = catalog.Find(taskId);
            var definition = task.goals.Single(x => x.goalId == goalId);
            return new GoalAchievementEvaluator().Evaluate(Request(task, new[] { definition }, transcript, "turn")).evaluations.Single();
        }

        private static GoalEvaluationRequest Request(ExperimentTaskDefinition task, ExperimentTaskGoal[] goals,
            string transcript, string turnId) => new GoalEvaluationRequest
        {
            participantId = "p", sessionId = "s", conditionRunId = "run", taskId = task.taskId,
            turnId = turnId, userTranscript = transcript, recentUserTurns = new[] { transcript }, currentGoalDefinitions = goals
        };

        private static GoalProgressTracker Tracker(ExperimentTaskDefinition task)
        {
            var tracker = new GoalProgressTracker();
            tracker.ResetGoals(task, new GoalTrackingContext
            {
                participantId = "p", sessionId = "s", conditionRunId = "run", taskId = task.taskId,
                taskAssignmentId = "ta", confirmationPolicy = GoalConfirmationPolicy.AutomaticOnValidatedDetection
            });
            return tracker;
        }

        private sealed class FakeAsyncFallback : IAsyncStructuredGoalEvaluationFallback
        {
            private readonly float confidence;
            private readonly string evidence;
            private readonly Action beforeComplete;
            public bool Called { get; private set; }
            public GoalEvaluationRequest Request { get; private set; }

            public FakeAsyncFallback(float confidence, string evidence, Action beforeComplete = null)
            { this.confidence = confidence; this.evidence = evidence; this.beforeComplete = beforeComplete; }

            public IEnumerator Evaluate(GoalEvaluationRequest request, Action<GoalEvaluationResult> onComplete, Action<string> onError)
            {
                Called = true; Request = request; yield return null; beforeComplete?.Invoke();
                onComplete(new GoalEvaluationResult
                {
                    taskId = request.taskId, turnId = request.turnId,
                    evaluations = request.currentGoalDefinitions.Select(x => new GoalEvaluationItem
                    {
                        goalId = x.goalId, achieved = true, confidence = confidence, evidence = evidence,
                        reason = "semantic paraphrase", evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion + "+structured_llm"
                    }).ToArray()
                });
            }
        }
    }
}
