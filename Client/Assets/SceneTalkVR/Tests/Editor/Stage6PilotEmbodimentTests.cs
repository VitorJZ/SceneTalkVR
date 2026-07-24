using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class Stage6PilotEmbodimentTests
    {
        private const string FormalCorrectionAssistantPrefabPath =
            "Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/correction_assistant_woman.prefab";
        private ExperimentV11ProtocolConfig protocol;
        private ExperimentTaskCatalog tasks;
        private PilotPresentationCatalog presentations;
        private QuestionnaireCatalog questionnaires;

        [SetUp] public void SetUp()
        {
            protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
            tasks = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
            presentations = AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>("Assets/SceneTalkVR/ExperimentProtocol/PilotPresentationCatalog.asset");
            questionnaires = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset");
            Assert.That(protocol, Is.Not.Null); Assert.That(tasks, Is.Not.Null); Assert.That(presentations, Is.Not.Null); Assert.That(questionnaires, Is.Not.Null);
        }

        [Test] public void EmbodimentIds_AreUniqueStringLabels()
        {
            var labels = Enum.GetValues(typeof(PilotEmbodimentCondition)).Cast<PilotEmbodimentCondition>().Select(PilotProtocolValues.Label).ToArray();
            CollectionAssert.AreEquivalent(new[] { "voice_only", "floating_orb", "humanoid_agent" }, labels);
            Assert.That(labels.Distinct().Count(), Is.EqualTo(3));
        }

        [Test] public void OfficialLockedPilot_UsesConfirmedDecisionsAndApprovedPresentations()
        {
            Assert.That(protocol.TryResolvePilotDecisions(out _, out _, out var decisionError), Is.True, decisionError);
            Assert.That(presentations.ValidateLocked(protocol, out var presentationError), Is.True, presentationError);
            Assert.That(new PilotAssignmentAllocator().TryCreateLocked("p", "s", protocol, tasks, presentations, out var assignment, out var error), Is.True, error);
            Assert.That(assignment, Is.Not.Null);
        }

        [Test] public void PresentationProfiles_ShareVoiceAndControlledParameters()
        {
            var profiles = presentations.Profiles.ToArray(); Assert.That(profiles.Length, Is.EqualTo(3));
            Assert.That(profiles.Select(x => x.voiceProfileKey).Distinct().Count(), Is.EqualTo(1));
            Assert.That(profiles.Select(x => x.speakingSpeed).Distinct().Single(), Is.EqualTo(1));
            Assert.That(profiles.Select(x => x.volume).Distinct().Single(), Is.EqualTo(1));
            Assert.That(profiles.Select(x => x.subtitlePolicy).Distinct().Single(), Is.EqualTo("feedback_only"));
            Assert.That(profiles.All(x => x.appearanceDelayMs == 0), Is.True);
            Assert.That(
                profiles.Where(x => x.embodimentCondition != PilotEmbodimentCondition.VoiceOnly)
                    .All(x => x.minDistance == 3.2f && x.maxDistance == 8f),
                Is.True);
        }

        [TestCase(PilotAudioSourcePolicy.SpatialFixedSource)]
        [TestCase(PilotAudioSourcePolicy.NonSpatialHeadLocked)]
        public void VoiceOnly_UsesFormalAudioOnlyMode(PilotAudioSourcePolicy policy)
        {
            var go = new GameObject("voice-only-test"); try
            {
                var presenter = go.AddComponent<PilotEmbodimentPresenter>();
                Assert.That(presenter.Configure(presentations.Find(PilotEmbodimentCondition.VoiceOnly), policy, false, out var error), Is.True, error);
                presenter.BeginFeedback(); Assert.That(presenter.HasVisualEntity, Is.False); Assert.That(presenter.VisualEntityType, Is.EqualTo("none"));
                Assert.That(go.GetComponent<CorrectionAgentPresenter>().CurrentVisualMode, Is.EqualTo(CorrectionAgentPresenter.VisualMode.AudioOnly));
                Assert.That(presenter.AudioSource.spatialBlend, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void FloatingOrb_UsesFormalGeneratedOrbUntilReset()
        {
            var go = new GameObject("orb-test"); try
            {
                var presenter = go.AddComponent<PilotEmbodimentPresenter>();
                Assert.That(presenter.Configure(presentations.Find(PilotEmbodimentCondition.FloatingOrb), PilotAudioSourcePolicy.SpatialFixedSource, false, out var error), Is.True, error);
                var agent = go.GetComponent<CorrectionAgentPresenter>();
                Assert.That(agent, Is.Not.Null); Assert.That(agent.CurrentVisualMode, Is.EqualTo(CorrectionAgentPresenter.VisualMode.GeneratedAgent)); Assert.That(presenter.HasVisualEntity, Is.True);
                Assert.That(presenter.AudioSource.minDistance, Is.EqualTo(3.2f).Within(0.0001f));
                Assert.That(presenter.AudioSource.maxDistance, Is.EqualTo(8f).Within(0.0001f));
                presenter.BeginFeedback(); Assert.That(presenter.HasVisualEntity, Is.True);
                presenter.EndFeedback(); Assert.That(presenter.HasVisualEntity, Is.True);
                presenter.ResetSession(); Assert.That(presenter.HasVisualEntity, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void Humanoid_UsesFormalPresenterPrefab_AndRejectsMissingFormalPrefab()
        {
            var go = new GameObject("human-test");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormalCorrectionAssistantPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            try
            {
                var agent = go.AddComponent<CorrectionAgentPresenter>();
                var agentSerialized = new SerializedObject(agent);
                agentSerialized.FindProperty("humanoidPrefab").objectReferenceValue = prefab;
                agentSerialized.ApplyModifiedPropertiesWithoutUndo();
                var presenter = go.AddComponent<PilotEmbodimentPresenter>();
                var good = Profile(PilotEmbodimentCondition.HumanoidAgent, PilotVisualMode.Humanoid);
                Assert.That(presenter.Configure(good, PilotAudioSourcePolicy.SpatialFixedSource, true, out var error), Is.True, error);
                presenter.BeginFeedback(); Assert.That(presenter.HasVisualEntity, Is.True);
                Assert.That(agent.CurrentVisualMode, Is.EqualTo(CorrectionAgentPresenter.VisualMode.HumanoidAvatar));
                Assert.That(go.transform.Find("Correction Assistant Agent/Assistant Humanoid"), Is.Not.Null);
                Assert.That(go.transform.Find("Pilot Humanoid Feedback Agent"), Is.Null);

                agentSerialized.FindProperty("humanoidPrefab").objectReferenceValue = null;
                agentSerialized.ApplyModifiedPropertiesWithoutUndo();
                var missing = Profile(PilotEmbodimentCondition.HumanoidAgent, PilotVisualMode.Humanoid);
                Assert.That(presenter.Configure(missing, PilotAudioSourcePolicy.SpatialFixedSource, true, out error), Is.False);
                Assert.That(error, Is.EqualTo("formal_humanoid_prefab_missing"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void PlannerContext_ContainsStyleButNeverEmbodiment()
        {
            var context = PilotWorkflowCoordinator.BuildCorrectionPlannerContext(PilotFeedbackStyleChoice.Explicit);
            StringAssert.Contains("feedback_style=explicit", context); StringAssert.DoesNotContain("voice_only", context); StringAssert.DoesNotContain("floating_orb", context); StringAssert.DoesNotContain("humanoid", context);
        }

        [Test] public void SameInput_ProducesSameFeedbackHashAcrossEmbodiments()
        {
            var hashes = presentations.Profiles.Select(_ => ExperimentEventTimeline.HashText("Use: I went to the restaurant.")).ToArray();
            Assert.That(hashes.Distinct().Count(), Is.EqualTo(1));
        }

        [Test] public void PilotTasks_AreThreeUniqueCompleteRestaurantTasks()
        {
            var pilot = tasks.GetTasks(ExperimentTaskPhase.Pilot).ToArray();
            Assert.That(ExperimentTaskCatalog.ValidatePilotTasks(pilot, out var error), Is.True, error);
            CollectionAssert.AreEquivalent(new[] { "pilot_restaurant_walk_in", "pilot_restaurant_ordering", "pilot_restaurant_wrong_dish" }, pilot.Select(x => x.taskId));
            Assert.That(pilot.All(x => x.goals.Length == 4 && !string.IsNullOrWhiteSpace(x.initialQuestion) && x.panoramaResourceKey == "SceneTalkVR/Textures/restaurant-360"), Is.True);
        }

        [Test] public void TestAllocator_AssignsEachEmbodimentAndTaskOnce_AndIsStable()
        {
            var allocator = new PilotAssignmentAllocator(); var sequences = TestSequences(); var ids = PilotTaskIds();
            Assert.That(allocator.TryCreateForTesting("participant-42", "session", protocol.ProtocolVersion, tasks.CatalogVersion, sequences, ids, PilotFeedbackStyleChoice.Explicit, PilotAudioSourcePolicy.NonSpatialHeadLocked, true, out var first, out var error), Is.True, error);
            allocator.TryCreateForTesting("participant-42", "session", protocol.ProtocolVersion, tasks.CatalogVersion, sequences, ids, PilotFeedbackStyleChoice.Explicit, PilotAudioSourcePolicy.NonSpatialHeadLocked, true, out var second, out _);
            Assert.That(first.sequenceId, Is.EqualTo(second.sequenceId));
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(PilotEmbodimentCondition)), first.conditions.Select(x => x.embodimentCondition));
            CollectionAssert.AreEquivalent(ids, first.conditions.Select(x => x.task.taskId));
        }

        [Test] public void TestAllocator_IsApproximatelyBalancedAcrossParticipants()
        {
            var counts = new int[3, 3]; var allocator = new PilotAssignmentAllocator(); var sequences = TestSequences(); var ids = PilotTaskIds();
            for (var p = 0; p <  ninety(); p++)
            {
                allocator.TryCreateForTesting("balanced-" + p, "s", protocol.ProtocolVersion, tasks.CatalogVersion, sequences, ids, PilotFeedbackStyleChoice.Recast, PilotAudioSourcePolicy.SpatialFixedSource, true, out var assignment, out _);
                foreach (var c in assignment.conditions) counts[(int)c.embodimentCondition, Array.IndexOf(ids, c.task.taskId)]++;
            }
            var flat = counts.Cast<int>().ToArray(); Assert.That(flat.Max() - flat.Min(), Is.LessThanOrEqualTo(12));
        }

        [Test] public void Assignment_SaveRestoreAndVersionValidation()
        {
            var allocator = new PilotAssignmentAllocator(); allocator.TryCreateForTesting("save", "s", protocol.ProtocolVersion, tasks.CatalogVersion, TestSequences(), PilotTaskIds(), PilotFeedbackStyleChoice.Explicit, PilotAudioSourcePolicy.SpatialFixedSource, true, out var assignment, out _);
            var path = System.IO.Path.GetTempFileName(); try { PilotAssignmentAllocator.Save(assignment, path); var json=System.IO.File.ReadAllText(path);StringAssert.Contains("\"feedbackStyleLabel\": \"explicit\"",json);StringAssert.Contains("\"embodimentConditionLabel\": \"voice_only\"",json);var restored = PilotAssignmentAllocator.Load(path); Assert.That(PilotAssignmentAllocator.IsCompatible(restored, protocol.ProtocolVersion, tasks.CatalogVersion, out var error), Is.True, error); restored.pilotAssignmentVersion = "old"; Assert.That(PilotAssignmentAllocator.IsCompatible(restored, protocol.ProtocolVersion, tasks.CatalogVersion, out _), Is.False); } finally { System.IO.File.Delete(path); }
        }

        [Test] public void PilotQuestionnaireDefinitionsAndRankingLabels_AreStage5CatalogData()
        {
            Assert.That(questionnaires.Find("pilot_condition_v1"), Is.Not.Null); Assert.That(questionnaires.Find("pilot_final_v1"), Is.Not.Null);
            var ranking = new PreferenceRankingResponse { rankings = new[] { "voice_only", "floating_orb", "humanoid_agent" }.Select((x, i) => new PreferenceRankEntry { rank = i + 1, embodimentCondition = x }).ToArray() };
            Assert.That(ranking.ValidateUnique(new[] { "voice_only", "floating_orb", "humanoid_agent" }, out var error), Is.True, error);
            ranking.rankings[2].embodimentCondition = "floating_orb"; Assert.That(ranking.ValidateUnique(new[] { "voice_only", "floating_orb", "humanoid_agent" }, out _), Is.False);
        }

        [Test] public void QuestionnaireSubmission_HasUnambiguousSubmittedAndCompletedSemantics()
        {
            var service = new QuestionnaireSessionService(); service.Configure(questionnaires, protocol);
            var context = new QuestionnaireSession { participantId="p",sessionId="s",sequenceId="pilot-a",conditionRunId="run",questionnaireLinkageKey="link",embodimentCondition="voice_only",taskId=PilotTaskIds()[0],taskAssignmentId="pta",technicalValidity=ExperimentTechnicalValidity.Valid,protocolVersion=protocol.ProtocolVersion,conditionStatus=ConditionRunStatus.QuestionnaireInProgress };
            Assert.That(service.Begin(questionnaires.Find("pilot_condition_v1"), context, out var error), Is.True, error);
            foreach(var item in questionnaires.GetEnabledItems("pilot_condition_v1", protocol).Where(x=>x.required)) service.SetResponse(item.itemId, item.itemType==QuestionnaireItemType.Likert?"4":"answer", out _);
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "stage6-questionnaire-"+Guid.NewGuid().ToString("N"));
            try { Assert.That(service.Submit(folder, out error), Is.True, error); Assert.That(service.ActiveSession.responses.All(x=>x.questionnaireStatus==QuestionnaireCompletionStatus.Submitted.ToString() && x.conditionStatus==ConditionRunStatus.Completed.ToString() && !string.IsNullOrWhiteSpace(x.responseCapturedAtUtc) && !string.IsNullOrWhiteSpace(x.questionnaireSubmittedAtUtc) && !string.IsNullOrWhiteSpace(x.conditionCompletedAtUtc)), Is.True); }
            finally { if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true); }
        }

        private static int ninety()=>90;
        private static string[] PilotTaskIds()=>new[]{"pilot_restaurant_walk_in","pilot_restaurant_ordering","pilot_restaurant_wrong_dish"};
        private static PilotSequenceDefinition[] TestSequences()=>new[]{
            new PilotSequenceDefinition{sequenceId="a-b-c",confirmed=true,conditions=new[]{PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent}},
            new PilotSequenceDefinition{sequenceId="b-c-a",confirmed=true,conditions=new[]{PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent,PilotEmbodimentCondition.VoiceOnly}},
            new PilotSequenceDefinition{sequenceId="c-a-b",confirmed=true,conditions=new[]{PilotEmbodimentCondition.HumanoidAgent,PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb}}};
        private static PilotPresentationProfile Profile(PilotEmbodimentCondition condition,PilotVisualMode visual)=>new PilotPresentationProfile{embodimentCondition=condition,visualMode=visual,feedbackActor=PilotProtocolValues.Label(condition),voiceProfileKey="shared",audioSourcePolicy=PilotAudioSourcePolicy.SpatialFixedSource,spatialBlend=1,minDistance=.2f,maxDistance=4,volume=1,speakingSpeed=1,subtitlePolicy="feedback_only"};
    }
}
