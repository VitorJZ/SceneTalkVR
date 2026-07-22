using System;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class PilotCollectionFlowTests
    {
        private ExperimentV11ProtocolConfig protocol;private ExperimentTaskCatalog tasks;private PilotPresentationCatalog presentations;
        [SetUp]public void SetUp(){protocol=AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");tasks=AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");presentations=AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>("Assets/SceneTalkVR/ExperimentProtocol/PilotPresentationCatalog.asset");}
        [Test]public void CollectionAssignment_IsStableAndUsesApprovedIdentity()
        {var allocator=new PilotAssignmentAllocator();Assert.That(allocator.TryCreateCollection("P-100","S-1",protocol,tasks,presentations,"resource",out var first,out var error),Is.True,error);Assert.That(allocator.TryCreateCollection("P-100","S-2",protocol,tasks,presentations,"resource",out var second,out error),Is.True,error);Assert.That(first.sequenceId,Is.EqualTo(second.sequenceId));Assert.That(first.dataOrigin,Is.EqualTo("participant_collection"));Assert.That(first.collectionEligible,Is.True);Assert.That(first.developerTestAssignment,Is.False);Assert.That(first.feedbackStyle,Is.EqualTo(PilotFeedbackStyleChoice.Explicit));Assert.That(first.voiceOnlyAudioPolicy,Is.EqualTo(PilotAudioSourcePolicy.NonSpatialHeadLocked));}
        [Test]public void CollectionAssignment_ContainsEachTaskAndEmbodimentExactlyOnce()
        {var allocator=new PilotAssignmentAllocator();allocator.TryCreateCollection("P-200","S",protocol,tasks,presentations,"resource",out var value,out _);CollectionAssert.AreEquivalent(new[]{"pilot_restaurant_walk_in","pilot_restaurant_ordering","pilot_restaurant_wrong_dish"},value.conditions.Select(x=>x.task.taskId));CollectionAssert.AreEquivalent(Enum.GetValues(typeof(PilotEmbodimentCondition)),value.conditions.Select(x=>x.embodimentCondition));Assert.That(value.conditions.Select(x=>x.task.taskId).Distinct().Count(),Is.EqualTo(3));}
        [Test]public void ApprovedPilotTasks_HaveExactOpeningsAndGoalIds()
        {var walk=tasks.Find("pilot_restaurant_walk_in");var ordering=tasks.Find("pilot_restaurant_ordering");var wrong=tasks.Find("pilot_restaurant_wrong_dish");Assert.That(walk.initialQuestion,Is.EqualTo("Good evening! Welcome to Riverside Restaurant. Do you have a reservation?"));Assert.That(ordering.initialQuestion,Is.EqualTo("Here is the menu. Are you ready to order, or would you like a recommendation?"));Assert.That(wrong.initialQuestion,Is.EqualTo("Here is your meal. Is everything all right with your order?"));CollectionAssert.AreEqual(new[]{"no_reservation","party_size","table_availability","wait_time"},walk.goals.Select(x=>x.goalId));CollectionAssert.AreEqual(new[]{"recommendation","main_course","dietary_restriction","drink"},ordering.goals.Select(x=>x.goalId));CollectionAssert.AreEqual(new[]{"wrong_dish","original_order","replacement_request","replacement_wait_time"},wrong.goals.Select(x=>x.goalId));}
        [TestCase("pilot_restaurant_walk_in","No, I don't have a reservation.","no_reservation")]
        [TestCase("pilot_restaurant_walk_in","There are four of us.","party_size")]
        [TestCase("pilot_restaurant_ordering","What do you recommend?","recommendation")]
        [TestCase("pilot_restaurant_ordering","I don't eat seafood.","dietary_restriction")]
        [TestCase("pilot_restaurant_wrong_dish","This isn't what I ordered.","wrong_dish")]
        [TestCase("pilot_restaurant_wrong_dish","Could you replace it, please?","replacement_request")]
        public void ApprovedParticipantPhrases_AreDetected(string taskId,string phrase,string goalId)
        {var task=tasks.Find(taskId);var result=new GoalAchievementEvaluator().Evaluate(new GoalEvaluationRequest{participantId="p",sessionId="s",conditionRunId="r",taskId=taskId,turnId="t",userTranscript=phrase,currentGoalDefinitions=task.goals});Assert.That(result.evaluations.Single(x=>x.goalId==goalId).achieved,Is.True);}
        [Test]public void Presentations_ShareVoiceSpeedVolumeAndSubtitlePolicy()
        {var values=presentations.Profiles.ToArray();Assert.That(values.Select(x=>x.voiceProfileKey).Distinct().Count(),Is.EqualTo(1));Assert.That(values.Select(x=>x.speakingSpeed).Distinct().Count(),Is.EqualTo(1));Assert.That(values.Select(x=>x.volume).Distinct().Count(),Is.EqualTo(1));Assert.That(values.Select(x=>x.subtitlePolicy).Distinct().Count(),Is.EqualTo(1));Assert.That(presentations.Find(PilotEmbodimentCondition.VoiceOnly).visualMode,Is.EqualTo(PilotVisualMode.None));Assert.That(presentations.Find(PilotEmbodimentCondition.VoiceOnly).spatialBlend,Is.Zero);}
    }
}
