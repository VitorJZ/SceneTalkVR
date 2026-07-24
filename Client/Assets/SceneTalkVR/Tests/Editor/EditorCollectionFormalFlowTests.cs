using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class EditorCollectionFormalFlowTests
    {
        private ExperimentV11ProtocolConfig protocol;
        private ExperimentTaskCatalog tasks;
        private ExperimentVoiceProfileCatalog voices;
        private ExperimentDeploymentCatalog deployments;
        private EditorCollectionResourceCatalog resources;
        private QuestionnaireCatalog questionnaires;
        private PilotPresentationCatalog pilot;

        [OneTimeSetUp]
        public void BuildOfficialAssets() => EditorCollectionAssetBuilder.CreateOrUpdate();

        [SetUp]
        public void SetUp()
        {
            protocol = Load<ExperimentV11ProtocolConfig>(EditorCollectionAssetBuilder.ProtocolPath);
            tasks = Load<ExperimentTaskCatalog>(EditorCollectionAssetBuilder.TaskPath);
            voices = Load<ExperimentVoiceProfileCatalog>(EditorCollectionAssetBuilder.VoicePath);
            deployments = Load<ExperimentDeploymentCatalog>(EditorCollectionAssetBuilder.DeploymentPath);
            resources = Load<EditorCollectionResourceCatalog>(EditorCollectionAssetBuilder.ResourcePath);
            questionnaires = Load<QuestionnaireCatalog>(EditorCollectionAssetBuilder.QuestionnairePath);
            pilot = Load<PilotPresentationCatalog>(EditorCollectionAssetBuilder.PilotPresentationPath);
        }

        [Test] public void T01_OfficialProtocolIsCollectionApproved()
        { Assert.That(protocol.ProtocolVersion, Is.EqualTo("1.2.0-editor-collection")); Assert.That(protocol.ApprovedForCollection, Is.True); Assert.That(protocol.ValidateForFormalMode(out var error), Is.True, error); }

        [Test] public void T02_ElevenDecisionsAreConfirmedWithProvenance()
        { Assert.That(protocol.RequiredDecisions.Count, Is.EqualTo(11)); Assert.That(protocol.RequiredDecisions.All(x => x.status == ProtocolDecisionStatus.Confirmed && x.confirmedBy == "ProjectLead" && !string.IsNullOrWhiteSpace(x.confirmedAtUtc) && x.evidenceReference == "formal-editor-collection-directive-v1"), Is.True); }

        [TestCase("condition_letter_mapping", "a=NE,b=NR,c=SE,d=SR")]
        [TestCase("formal_task_no_replacement", "strict_without_replacement")]
        [TestCase("formal_social_comfort", "excluded")]
        [TestCase("pilot_feedback_style", "explicit")]
        [TestCase("voice_only_spatial_audio", "non_spatial_head_locked")]
        [TestCase("pilot_sequence_mapping", "a=voice_only,b=floating_orb,c=humanoid_agent")]
        [TestCase("formal_max_turns", "6")]
        [TestCase("formal_max_duration", "10 minutes")]
        [TestCase("pilot_max_turns", "5")]
        [TestCase("pilot_max_duration", "8 minutes")]
        public void T03_T12_OfficialDecisionValues(string id, string expected)
        { Assert.That(protocol.TryGetConfirmedDecision(id, out var value), Is.True); Assert.That(value, Is.EqualTo(expected)); }

        [Test] public void T13_ParticipantFlowPoliciesAreLocked()
        { Assert.That(protocol.FormalConditionOrderPolicy, Is.EqualTo(FormalConditionOrderPolicy.ParticipantChoice)); Assert.That(protocol.FormalTaskAssignmentPolicy, Is.EqualTo("random_bijection_without_replacement")); Assert.That(protocol.GoalConfirmationPolicy, Is.EqualTo(GoalConfirmationPolicy.AutomaticOnValidatedDetection)); Assert.That(protocol.QuestionnaireTransitionPolicy, Is.EqualTo(QuestionnaireReturnPolicy.ReturnToModeSelection)); Assert.That(protocol.PrimaryAttemptPolicy, Is.EqualTo("latest_valid_completed_attempt")); }

        [Test] public void T14_OfficialCatalogsValidate()
        { Assert.That(tasks.ValidateFormal(protocol, out var taskError), Is.True, taskError); Assert.That(questionnaires.ValidateFormal(protocol, out var qError), Is.True, qError); Assert.That(resources.Validate(tasks, voices, deployments, out var resourceError), Is.True, resourceError); }

        [Test] public void T15_EditorDeploymentIsCollectionApproved()
        { Assert.That(deployments.ValidateForCollection(ExperimentDeploymentProfileId.EditorCollection, out var error), Is.True, error); deployments.TryGet(ExperimentDeploymentProfileId.EditorCollection, out var profile); Assert.That(profile.target, Is.EqualTo(ExperimentDeploymentTarget.UnityEditor)); Assert.That(profile.EndpointHost, Is.EqualTo("127.0.0.1")); }

        [Test] public void T16_CurrentPilotResourcesRemainValid()
        { Assert.That(pilot.ValidateLocked(protocol, out var error), Is.True, error); Assert.That(pilot.Find(PilotEmbodimentCondition.VoiceOnly).visualMode, Is.EqualTo(PilotVisualMode.None)); Assert.That(pilot.Find(PilotEmbodimentCondition.FloatingOrb).visualPrefabKey, Is.EqualTo("generated_orb_v1")); Assert.That(pilot.Find(PilotEmbodimentCondition.HumanoidAgent).visualPrefabKey, Is.EqualTo("correction_assistant_woman")); }

        [Test] public void T17_FormalAssignmentIsRandomBijectionAndCollectionIdentity()
        {
            var ok = new ExperimentAssignmentAllocator().TryCreateEditorCollection("P-COLLECTION", "S-001", protocol, tasks, resources.ResourceSnapshotId, out var assignment, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(assignment.conditions.Select(x => x.formalConditionCode).Distinct().Count(), Is.EqualTo(4)); Assert.That(assignment.conditions.Select(x => x.task.taskId).Distinct().Count(), Is.EqualTo(4)); Assert.That(assignment.collectionEligible, Is.True); Assert.That(assignment.developerTestAssignment, Is.False); Assert.That(assignment.demoMode, Is.False); Assert.That(assignment.synthetic, Is.False); Assert.That(assignment.runQualification, Is.EqualTo(ExperimentRunQualification.Collection)); Assert.That(assignment.deploymentProfile, Is.EqualTo("editor_collection"));
        }

        [Test] public void T18_AssignmentIsStableAndPersistencePreservesMapping()
        {
            var allocator = new ExperimentAssignmentAllocator();
            Assert.That(allocator.TryCreateEditorCollection("P-STABLE", "S-STABLE", protocol, tasks, resources.ResourceSnapshotId, out var first, out var error), Is.True, error);
            Assert.That(allocator.TryCreateEditorCollection("P-STABLE", "S-STABLE", protocol, tasks, resources.ResourceSnapshotId, out var second, out error), Is.True, error);
            Assert.That(first.conditions.Select(x => x.task.taskId), Is.EqualTo(second.conditions.Select(x => x.task.taskId)));
            var path = Path.Combine(Application.temporaryCachePath, "editor-collection-assignment-test.json");
            try { ExperimentAssignmentAllocator.Save(first, path); var restored = ExperimentAssignmentAllocator.Load(path); Assert.That(restored.conditions.Select(x => x.task.taskId), Is.EqualTo(first.conditions.Select(x => x.task.taskId))); }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [TestCase("hotel_check_in", "reservation_name", "My name is Harry Potter.")]
        [TestCase("hotel_check_in", "reservation_name", "The booking is under Harry Potter.")]
        [TestCase("hotel_check_in", "breakfast", "Is breakfast included?")]
        [TestCase("hotel_check_in", "higher_floor", "Could I have a room on a higher floor?")]
        [TestCase("hotel_check_in", "checkout_time", "What time is checkout?")]
        [TestCase("furniture_shopping", "desk_size", "I need a desk 120 centimeters wide.")]
        [TestCase("furniture_shopping", "material", "What material is this desk made of?")]
        [TestCase("furniture_shopping", "budget", "My budget is 500 dollars.")]
        [TestCase("furniture_shopping", "delivery", "Do you offer home delivery?")]
        [TestCase("gym_membership", "fitness_goal", "My fitness goal is to build muscle.")]
        [TestCase("gym_membership", "monthly_price", "How much is the membership per month?")]
        [TestCase("gym_membership", "suitable_workout", "What workout plan do you recommend?")]
        [TestCase("gym_membership", "trial", "Is a free trial available?")]
        [TestCase("tourist_assistance", "museum_route", "How do I get to the museum?")]
        [TestCase("tourist_assistance", "ticket", "Do I need a ticket?")]
        [TestCase("tourist_assistance", "photography", "Can I take photos inside?")]
        [TestCase("tourist_assistance", "nearby_attraction", "Can you recommend another nearby attraction?")]
        public void T19_T35_DeterministicGoalIntentRules(string taskId, string goalId, string transcript)
        {
            var task = tasks.Find(taskId); var goal = task.goals.Single(x => x.goalId == goalId);
            var result = new GoalAchievementEvaluator().Evaluate(Request(task, transcript, goal));
            Assert.That(result.evaluations.Single().achieved, Is.True, taskId + ":" + goalId);
            Assert.That(result.evaluations.Single().confidence, Is.GreaterThanOrEqualTo(goal.minimumConfidence));
        }

        [Test] public void T36_UnrelatedSentenceDoesNotCompleteGoal()
        { var task = tasks.Find("hotel_check_in"); var goal = task.goals.Single(x => x.goalId == "reservation_name"); Assert.That(new GoalAchievementEvaluator().Evaluate(Request(task, "The weather is pleasant today.", goal)).evaluations.Single().achieved, Is.False); }

        [Test] public void T37_StructuredFallbackCanReturnTypedResult()
        {
            var task = tasks.Find("hotel_check_in"); var goal = task.goals.Single(x => x.goalId == "higher_floor");
            var result = new GoalAchievementEvaluator(new FakeFallback(goal.goalId)).Evaluate(Request(task, "I would like something a little farther up.", goal));
            Assert.That(result.fallbackSucceeded, Is.True); Assert.That(result.evaluations.Single().achieved, Is.True); Assert.That(result.evaluations.Single().evaluatorVersion, Does.Contain("structured"));
        }

        [Test] public void T38_FallbackFailureNeverFakesCompletion()
        { var task = tasks.Find("hotel_check_in"); var goal = task.goals.Single(x => x.goalId == "breakfast"); var result = new GoalAchievementEvaluator(new FailingFallback()).Evaluate(Request(task, "Tell me about food.", goal)); Assert.That(result.evaluations.Single().achieved, Is.False); Assert.That(result.error, Is.EqualTo("fake_timeout")); }

        [Test] public void T39_FourPanoramasAreTrueTwoToOne()
        { foreach (var task in tasks.GetTasks(ExperimentTaskPhase.Formal)) { var texture = Resources.Load<Texture2D>(task.panoramaResourceKey); Assert.That(texture, Is.Not.Null, task.taskId); Assert.That((texture.width, texture.height), Is.EqualTo((2048, 1024)), task.taskId); } }

        [Test] public void T40_FormalAvatarMappingsAreExplicit()
        { Assert.That(resources.FindAvatar("hotel_check_in").requestedPresetKey, Is.EqualTo("barista_humanoid_v1")); Assert.That(resources.FindAvatar("furniture_shopping").requestedPresetKey, Is.EqualTo("teacher_humanoid_v1")); Assert.That(resources.FindAvatar("gym_membership").requestedPresetKey, Is.EqualTo("barista_male_humanoid_v1")); Assert.That(resources.FindAvatar("tourist_assistance").requestedPresetKey, Is.EqualTo("teacher_female_humanoid_v1")); }

        [Test] public void T41_ProductionStructuredFallbackUsesDedicatedJsonLlmBoundary()
        {
            Assert.That(typeof(StructuredLlmGoalEvaluationFallback).GetInterfaces(), Does.Contain(typeof(IAsyncStructuredGoalEvaluationFallback)));
            var service = Type.GetType("SceneTalkVR.Runtime.Services.RealLLMService, Assembly-CSharp");
            Assert.That(service?.GetMethod("GenerateStructuredGoalEvaluationAsync"), Is.Not.Null);
        }

        [Test]
        public void T42_FormalAvatarVoiceMetadataMatchesPresentedGender()
        {
            var catalog = Load<AvatarCatalog>(EditorCollectionAssetBuilder.AvatarPath);
            AssertVoice(catalog, "barista_humanoid_v1", "female", "default_female_en");
            AssertVoice(catalog, "teacher_humanoid_v1", "male", "default_male_en");
            AssertVoice(catalog, "barista_male_humanoid_v1", "male", "default_male_en");
            AssertVoice(catalog, "teacher_female_humanoid_v1", "female", "default_female_en");
            Assert.That(voices.TryGet("editor_collection_feedback_voice", out var feedback), Is.True);
            Assert.That(feedback.voiceId, Is.EqualTo("default_female_en"), "The shared Pilot feedback voice must match its female Humanoid presentation.");
        }

        private static void AssertVoice(AvatarCatalog catalog, string key, string gender, string voiceId)
        {
            var preset = catalog.presets.Single(x => x.key == key);
            Assert.That(preset.genderPresentations.Any(x => string.Equals(x, gender, StringComparison.OrdinalIgnoreCase)), Is.True, key);
            Assert.That(preset.voiceId, Is.EqualTo(voiceId), key);
        }

        private static GoalEvaluationRequest Request(ExperimentTaskDefinition task, string transcript, ExperimentTaskGoal goal) => new GoalEvaluationRequest { taskId = task.taskId, turnId = "turn-1", userTranscript = transcript, currentGoalDefinitions = new[] { goal } };
        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private sealed class FakeFallback : IStructuredGoalEvaluationFallback
        {
            private readonly string goalId; public FakeFallback(string id) => goalId = id;
            public bool TryEvaluate(GoalEvaluationRequest request, out GoalEvaluationResult result, out string error)
            { result = new GoalEvaluationResult { taskId = request.taskId, turnId = request.turnId, evaluations = new[] { new GoalEvaluationItem { goalId = goalId, achieved = true, confidence = .91f, evidence = request.userTranscript, reason = "Structured schema matched paraphrased intent.", evaluatorVersion = "fake_structured_v1" } } }; error = string.Empty; return true; }
        }
        private sealed class FailingFallback : IStructuredGoalEvaluationFallback
        {
            public bool TryEvaluate(GoalEvaluationRequest request, out GoalEvaluationResult result, out string error) { result = null; error = "fake_timeout"; return false; }
        }
    }
}
