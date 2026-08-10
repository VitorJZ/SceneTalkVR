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
        {var walk=tasks.Find("pilot_restaurant_walk_in");var ordering=tasks.Find("pilot_restaurant_ordering");var wrong=tasks.Find("pilot_restaurant_wrong_dish");Assert.That(tasks.CatalogVersion,Is.EqualTo("1.5.0-formal-six-pilot-four"));Assert.That(walk.initialQuestion,Is.EqualTo("Good evening! Welcome to Riverside Restaurant. Do you have a reservation?"));Assert.That(ordering.initialQuestion,Is.EqualTo("Here is the menu. Are you ready to order, or would you like a recommendation?"));Assert.That(wrong.initialQuestion,Is.EqualTo("Excuse me, is everything all right with your meal?"));CollectionAssert.AreEqual(new[]{"no_reservation","party_size","window_table_availability","menu_request"},walk.goals.Select(x=>x.goalId));CollectionAssert.AreEqual(new[]{"recommendation","main_course","dish_price","drink"},ordering.goals.Select(x=>x.goalId));CollectionAssert.AreEqual(new[]{"wrong_dish","dietary_restriction","extra_charge","replacement_preparation_time"},wrong.goals.Select(x=>x.goalId));}
        [TestCase("pilot_restaurant_walk_in","No, I don't have a reservation.","no_reservation")]
        [TestCase("pilot_restaurant_walk_in","There are four of us.","party_size")]
        [TestCase("pilot_restaurant_walk_in","Do you have any tables by the window?","window_table_availability")]
        [TestCase("pilot_restaurant_walk_in","May I have a menu, please?","menu_request")]
        [TestCase("pilot_restaurant_ordering","What do you recommend?","recommendation")]
        [TestCase("pilot_restaurant_ordering","How much does the grilled chicken cost?","dish_price")]
        [TestCase("pilot_restaurant_wrong_dish","This isn't what I ordered.","wrong_dish")]
        [TestCase("pilot_restaurant_wrong_dish","I am allergic to peanuts.","dietary_restriction")]
        [TestCase("pilot_restaurant_wrong_dish","Will I be charged extra?","extra_charge")]
        [TestCase("pilot_restaurant_wrong_dish","How long will the new dish take to prepare?","replacement_preparation_time")]
        public void ApprovedParticipantPhrases_AreDetected(string taskId,string phrase,string goalId)
        {var task=tasks.Find(taskId);var result=new GoalAchievementEvaluator().Evaluate(new GoalEvaluationRequest{participantId="p",sessionId="s",conditionRunId="r",taskId=taskId,turnId="t",userTranscript=phrase,currentGoalDefinitions=task.goals});Assert.That(result.evaluations.Single(x=>x.goalId==goalId).achieved,Is.True);}
        [Test]public void RedesignedCatalogVersion_InvalidatesOldPilotAndFormalAssignments()
        {var pilot=new PilotAssignment{pilotProtocolVersion=protocol.ProtocolVersion,pilotAssignmentVersion=PilotAssignmentAllocator.Version,taskCatalogVersion="1.2.1-pilot-collection"};Assert.That(PilotAssignmentAllocator.IsCompatible(pilot,protocol.ProtocolVersion,tasks.CatalogVersion,out var pilotError),Is.False);Assert.That(pilotError,Is.EqualTo("task_catalog_version_changed"));var formal=new ExperimentAssignment{protocolVersion=protocol.ProtocolVersion,assignmentVersion=ExperimentAssignmentAllocator.AssignmentVersion,taskCatalogVersion="1.2.1-pilot-collection"};Assert.That(ExperimentAssignmentAllocator.IsCompatible(formal,protocol.ProtocolVersion,tasks.CatalogVersion,out var formalError),Is.False);Assert.That(formalError,Is.EqualTo("task_catalog_version_changed"));}
        [Test]public void RedesignedPilotGoals_HaveChineseParticipantLabels()
        {var type=Type.GetType("SceneTalkVR.Runtime.SceneTalkUiText, Assembly-CSharp");var method=type?.GetMethod("Goal");Assert.That(method,Is.Not.Null);string Label(string id)=>(string)method.Invoke(null,new object[]{id,""});Assert.That(Label("window_table_availability"),Is.EqualTo("询问是否有靠窗的空桌。"));Assert.That(Label("menu_request"),Is.EqualTo("要一份菜单。"));Assert.That(Label("dish_price"),Is.EqualTo("询问菜品价格。"));Assert.That(Label("dietary_restriction"),Is.EqualTo("声明自己的忌口或过敏原。"));Assert.That(Label("extra_charge"),Is.EqualTo("询问是否有额外收费。"));Assert.That(Label("replacement_preparation_time"),Is.EqualTo("询问重新制作餐品所需时间。"));}
        [Test]public void Presentations_ShareVoiceSpeedVolumeAndSubtitlePolicy()
        {var values=presentations.Profiles.ToArray();Assert.That(values.Select(x=>x.voiceProfileKey).Distinct().Count(),Is.EqualTo(1));Assert.That(values.Select(x=>x.speakingSpeed).Distinct().Count(),Is.EqualTo(1));Assert.That(values.Select(x=>x.volume).Distinct().Count(),Is.EqualTo(1));Assert.That(values.Select(x=>x.subtitlePolicy).Distinct().Count(),Is.EqualTo(1));Assert.That(presentations.Find(PilotEmbodimentCondition.VoiceOnly).visualMode,Is.EqualTo(PilotVisualMode.None));Assert.That(presentations.Find(PilotEmbodimentCondition.VoiceOnly).spatialBlend,Is.Zero);}
    }
}
