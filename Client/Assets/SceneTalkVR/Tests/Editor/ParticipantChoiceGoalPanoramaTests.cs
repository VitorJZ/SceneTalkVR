using System;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.EditorTools;
using UnityEditor;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class ParticipantChoiceGoalPanoramaTests
    {
        private ExperimentV11RehearsalProtocol protocol;
        private ExperimentTaskCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11RehearsalProtocol>(RehearsalAssetBuilder.ProtocolPath);
            catalog = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(RehearsalAssetBuilder.Root + "ExperimentTaskCatalog.asset");
        }

        [Test] public void ProtocolV2_UsesParticipantChoicePolicies()
        {
            Assert.That(protocol.ProtocolVersion, Is.EqualTo("1.1-rehearsal-2"));
            Assert.That(protocol.FormalConditionOrderPolicy, Is.EqualTo(FormalConditionOrderPolicy.ParticipantChoice));
            Assert.That(protocol.GoalConfirmationPolicy, Is.EqualTo(GoalConfirmationPolicy.AutomaticOnValidatedDetection));
            Assert.That(protocol.QuestionnaireReturnPolicy, Is.EqualTo(QuestionnaireReturnPolicy.ReturnToModeSelection));
        }

        [Test] public void Assignment_IsStableRandomBijection()
        {
            var allocator = new ExperimentAssignmentAllocator();
            Assert.That(allocator.TryCreateRehearsal("P-STABLE", "S1", protocol, catalog, "resources", out var first, out var error), Is.True, error);
            Assert.That(allocator.TryCreateRehearsal("P-STABLE", "S2", protocol, catalog, "resources", out var second, out error), Is.True, error);
            Assert.That(first.conditions.Select(x => x.task.taskId), Is.EqualTo(second.conditions.Select(x => x.task.taskId)));
            Assert.That(first.conditions.Select(x => x.task.taskId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(first.conditions.Select(x => x.formalConditionCode), Is.EqualTo(new[] { FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR }));
            Assert.That(first.formalConditionOrderPolicy, Is.EqualTo("participant_choice"));
            Assert.That(first.participantSelectionOrder, Is.Empty);
        }

        [Test] public void Assignment_ResumeCompatibilityDoesNotReinterpretV1()
        {
            var old = new ExperimentAssignment
            {
                assignmentVersion = "1.0",
                protocolVersion = "1.1-rehearsal-2",
                taskCatalogVersion = catalog.CatalogVersion,
                formalConditionOrderPolicy = "participant_choice"
            };
            Assert.That(ExperimentAssignmentAllocator.IsCompatible(old, "1.1-rehearsal-2", catalog.CatalogVersion, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("assignment_version_changed"));
        }

        [Test] public void GoalReset_CarriesRunAndAssignmentIdentity()
        {
            var tracker = Tracker(GoalConfirmationPolicy.ExperimenterReview);
            Assert.That(tracker.Goals.All(x => x.conditionRunId == "run-1" && x.taskAssignmentId == "assignment-1"), Is.True);
        }

        [Test] public void GoalReset_RaisesCollectionAndProgressEvents()
        {
            var tracker = new GoalProgressTracker(); var resets = 0; var progress = 0;
            tracker.OnGoalCollectionReset += _ => resets++; tracker.OnGoalProgressChanged += _ => progress++;
            tracker.ResetGoals(Task(), Context(GoalConfirmationPolicy.ExperimenterReview));
            Assert.That(resets, Is.EqualTo(1)); Assert.That(progress, Is.EqualTo(1));
        }

        [Test] public void AutomaticPolicy_RequiresTurnAndTranscript()
        {
            var tracker = Tracker(GoalConfirmationPolicy.AutomaticOnValidatedDetection);
            Assert.That(tracker.SubmitGoalCandidate(tracker.Goals[0].goalId, "detector", new GoalEvidence(), out var error), Is.False);
            Assert.That(error, Is.EqualTo("validated_goal_evidence_required"));
        }

        [Test] public void AutomaticPolicy_ConfirmsValidatedCandidate()
        {
            var tracker = Tracker(GoalConfirmationPolicy.AutomaticOnValidatedDetection);
            Assert.That(tracker.SubmitGoalCandidate(tracker.Goals[0].goalId, "detector", Evidence("turn-1"), out var error), Is.True, error);
            Assert.That(tracker.Goals[0].state, Is.EqualTo(GoalProgressState.Confirmed));
            Assert.That(tracker.Goals[0].confirmedBy, Is.EqualTo(GoalProgressTracker.AutomaticConfirmationActor));
            Assert.That(tracker.SequenceState, Is.EqualTo(GoalSequenceState.ActiveGoal));
            Assert.That(tracker.ActiveGoalIndex, Is.EqualTo(1));
        }

        [Test] public void ManualPolicy_RemainsCandidateUntilExperimenterReview()
        {
            var tracker = Tracker(GoalConfirmationPolicy.ExperimenterReview);
            tracker.SubmitGoalCandidate(tracker.Goals[0].goalId, "detector", Evidence("turn-1"), out _);
            Assert.That(tracker.Goals[0].state, Is.EqualTo(GoalProgressState.Candidate));
        }

        [Test] public void DuplicateEvidence_IsRejected()
        {
            var tracker = Tracker(GoalConfirmationPolicy.ExperimenterReview); var goal = tracker.Goals[0].goalId;
            tracker.SubmitGoalCandidate(goal, "detector", Evidence("turn-1"), out _);
            Assert.That(tracker.SubmitGoalCandidate(goal, "detector", Evidence("turn-1"), out var error), Is.False);
            Assert.That(error, Is.EqualTo("duplicate_goal_evidence"));
        }

        [Test] public void AllConfirmed_RaisesExactlyOnceAfterEveryAvatarReply()
        {
            var tracker = Tracker(GoalConfirmationPolicy.AutomaticOnValidatedDetection); var count = 0;
            tracker.OnAllGoalsConfirmed += _ => count++;
            for (var i = 0; i < tracker.Goals.Count; i++)
            {
                var turnId = "turn-" + i;
                tracker.SubmitGoalCandidate(tracker.ActiveGoal.goalId, "detector", Evidence(turnId), out _);
                Assert.That(count, Is.Zero);
                AdvanceAfterConfirmedGoal(tracker, turnId);
            }
            Assert.That(count, Is.EqualTo(1)); Assert.That(tracker.AreAllConfirmed, Is.True);
            Assert.That(tracker.IsSequenceCompleted, Is.True);
        }

        [Test] public void Reset_ClearsCompletionAndRevisions()
        {
            var tracker = Tracker(GoalConfirmationPolicy.AutomaticOnValidatedDetection);
            tracker.SubmitGoalCandidate(tracker.Goals[0].goalId, "detector", Evidence("turn-1"), out _);
            tracker.ResetGoals(Task(), Context(GoalConfirmationPolicy.AutomaticOnValidatedDetection));
            Assert.That(tracker.Goals.All(x => x.state == GoalProgressState.NotStarted && x.revision == 0), Is.True);
        }

        [Test] public void PanoramaContract_HasExactlyFiveStableKeys() => Assert.That(PanoramaAssetValidator.RequiredResourceKeys, Has.Length.EqualTo(5));

        [Test] public void PanoramaValidation_ReportsCurrentAssetTruthfully()
        {
            var report = PanoramaAssetValidator.ValidateAll();
            Assert.That(report.panoramas, Has.Length.EqualTo(5));
            Assert.That(report.generatorCapability, Is.EqualTo(PanoramaAssetValidator.GeneratorCapability));
            Assert.That(report.result, Is.EqualTo("FAIL"), "The retained Pilot restaurant panorama is still square and must remain explicit.");
            Assert.That(report.panoramas.Count(x => !x.dimensionValid), Is.EqualTo(1));
            Assert.That(report.panoramas.Single(x => !x.dimensionValid).resourceKey, Does.Contain("restaurant"));
        }

        [Test] public void PanoramaMemoryEstimate_UsesAstcBlocksAndMipmaps()
        {
            Assert.That(PanoramaAssetValidator.EstimateAstc6x6Bytes(4096, 2048, true), Is.GreaterThan(0));
            Assert.That(PanoramaAssetValidator.EstimateAstc6x6Bytes(4096, 2048, true), Is.LessThan(8 * 1024 * 1024));
        }

        [TestCase("hotel_check_in", "My reservation is under Li. Is breakfast included? Could I have a high floor? I need a quiet room. Does the hotel have Wi-Fi? What is the checkout time?")]
        [TestCase("furniture_shopping", "The desk dimensions are 120 centimeters. What materials are available? My budget is 500 dollars. What colors are available? Is home delivery available? Is assembly included?")]
        [TestCase("gym_membership", "My goal is to build muscle. What workout plan do you recommend? What are your opening hours? How much is the monthly membership? Is there a student discount? Is there a free trial?")]
        [TestCase("tourist_assistance", "How can I get to the museum? What are the museum opening hours? Do I need a ticket? Can I take photos inside? How long does it take to visit the museum? Can you recommend another nearby attraction?")]
        public void GoalDetector_RecognizesAllSixTaskGoals(string taskId, string transcript)
        {
            Assert.That(ValidatedRehearsalGoalDetector.Match(taskId, transcript), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [Test] public void GoalDetector_DoesNotInferFromGenericConversation()
        {
            Assert.That(ValidatedRehearsalGoalDetector.Match("gym_membership", "Hello, that sounds good, thank you."), Is.Empty);
        }

        private static GoalProgressTracker Tracker(GoalConfirmationPolicy policy)
        { var value = new GoalProgressTracker(); value.ResetGoals(Task(), Context(policy)); return value; }
        private static void AdvanceAfterConfirmedGoal(GoalProgressTracker tracker, string evidenceTurnId)
        {
            if (tracker.SequenceState == GoalSequenceState.ActiveGoal
                || tracker.SequenceState == GoalSequenceState.Completed)
                return;
            if (tracker.SequenceState == GoalSequenceState.AwaitingParticipantTurn)
            {
                var unlockTurnId = evidenceTurnId + "-unlock";
                Assert.That(tracker.NotifyParticipantTurnSubmitted(unlockTurnId), Is.True);
                Assert.That(tracker.NotifyDialogueTurnCompleted(unlockTurnId), Is.True);
                return;
            }
            Assert.That(tracker.NotifyDialogueTurnCompleted(evidenceTurnId), Is.True);
        }
        private static GoalTrackingContext Context(GoalConfirmationPolicy policy) => new GoalTrackingContext
        { participantId = "P", sessionId = "S", conditionRunId = "run-1", taskAssignmentId = "assignment-1", confirmationPolicy = policy,
            sequencePolicy = GoalSequencePolicy.SequentialAfterConfirmationWithFinalReplyCompletion };
        private static GoalEvidence Evidence(string turn) => new GoalEvidence { turnId = turn, transcript = "validated transcript", confidence = .9f };
        private static ExperimentTaskDefinition Task() => new ExperimentTaskDefinition
        {
            taskId = "test_task", goals = new[]
            {
                new ExperimentTaskGoal { text = "one" }, new ExperimentTaskGoal { text = "two" },
                new ExperimentTaskGoal { text = "three" }, new ExperimentTaskGoal { text = "four" }
            }
        };
    }
}
