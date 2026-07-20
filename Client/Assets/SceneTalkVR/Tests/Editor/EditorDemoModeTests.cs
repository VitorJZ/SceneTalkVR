using System;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class RehearsalModeTests
    {
        private ExperimentV11RehearsalProtocol protocol;
        private ExperimentV11RehearsalResourceCatalog resources;
        private ExperimentTaskCatalog tasks;
        private ExperimentVoiceProfileCatalog voices;
        private ExperimentDeploymentCatalog deployments;

        [OneTimeSetUp] public void BuildAssets() => RehearsalAssetBuilder.CreateOrUpdate();
        [SetUp] public void SetUp()
        {
            protocol = Load<ExperimentV11RehearsalProtocol>(RehearsalAssetBuilder.ProtocolPath);
            resources = Load<ExperimentV11RehearsalResourceCatalog>(RehearsalAssetBuilder.ResourcePath);
            tasks = Load<ExperimentTaskCatalog>(RehearsalAssetBuilder.Root + "ExperimentTaskCatalog.asset");
            voices = Load<ExperimentVoiceProfileCatalog>(RehearsalAssetBuilder.VoicePath);
            deployments = Load<ExperimentDeploymentCatalog>(RehearsalAssetBuilder.DeploymentPath);
        }

        [Test] public void T01_ProtocolVersion() => Assert.That(protocol.ProtocolVersion, Is.EqualTo("1.1-rehearsal-1"));
        [Test] public void T02_ProtocolPurpose() => Assert.That(protocol.ProtocolPurpose, Is.EqualTo(RehearsalProtocolPurpose.OperationalRehearsal));
        [Test] public void T03_ApprovedOnlyForRehearsal() { Assert.That(protocol.ApprovedForRehearsal, Is.True); Assert.That(protocol.ApprovedForCollection, Is.False); }
        [Test] public void T04_ApprovalProvenance() { Assert.That(protocol.ApprovalAuthority, Is.EqualTo("Project Lead Approval")); Assert.That(protocol.EvidenceReference, Is.EqualTo("scenetalkvr-rehearsal-baseline-v1")); }
        [Test] public void T05_ElevenDecisions() { Assert.That(protocol.Decisions.Count, Is.EqualTo(11)); Assert.That(protocol.Validate(out var error), Is.True, error); }
        [TestCase("condition_letter_mapping","a=NE,b=NR,c=SE,d=SR")]
        [TestCase("formal_task_no_replacement","strict_without_replacement")]
        [TestCase("formal_social_comfort","excluded")]
        [TestCase("pilot_feedback_style","explicit")]
        [TestCase("voice_only_spatial_audio","non_spatial_head_locked")]
        [TestCase("pilot_sequence_mapping","a=voice_only,b=floating_orb,c=humanoid_agent")]
        [TestCase("formal_max_turns","6")]
        [TestCase("formal_max_duration","10 minutes")]
        [TestCase("pilot_max_turns","5")]
        [TestCase("pilot_max_duration","8 minutes")]
        [TestCase("questionnaire_scale_anchors","1 = Strongly disagree / 非常不同意; 7 = Strongly agree / 非常同意")]
        public void T06_T16_DecisionValue(string id,string expected) => Assert.That(protocol.Decisions.Single(x=>x.decisionId==id).confirmedValue,Is.EqualTo(expected));
        [Test] public void T17_FormalSequences() => Assert.That(protocol.FormalSequences.Select(x=>x.sequenceId),Is.EqualTo(new[]{"a-b-c-d","b-c-d-a","c-d-a-b","d-a-b-c"}));
        [Test] public void T18_PilotSequences() => Assert.That(protocol.PilotSequences.Select(x=>x.sequenceId),Is.EqualTo(new[]{"a-b-c","b-c-a","c-a-b"}));
        [Test] public void T19_FormalLimits() { Assert.That(protocol.FormalMaxTurns,Is.EqualTo(6)); Assert.That(protocol.FormalMaxDurationMinutes,Is.EqualTo(10)); }
        [Test] public void T20_PilotLimits() { Assert.That(protocol.PilotMaxTurns,Is.EqualTo(5)); Assert.That(protocol.PilotMaxDurationMinutes,Is.EqualTo(8)); }
        [TestCase(ExperimentFlowMode.DeveloperManual,ExperimentRunQualification.Development,true)]
        [TestCase(ExperimentFlowMode.Formal,ExperimentRunQualification.Rehearsal,true)]
        [TestCase(ExperimentFlowMode.Formal,ExperimentRunQualification.Collection,true)]
        [TestCase(ExperimentFlowMode.Pilot,ExperimentRunQualification.Rehearsal,true)]
        [TestCase(ExperimentFlowMode.Pilot,ExperimentRunQualification.Collection,true)]
        [TestCase(ExperimentFlowMode.Synthetic,ExperimentRunQualification.Development,true)]
        [TestCase(ExperimentFlowMode.DeveloperManual,ExperimentRunQualification.Collection,false)]
        [TestCase(ExperimentFlowMode.Synthetic,ExperimentRunQualification.Collection,false)]
        public void T21_T28_RuntimeCombinations(ExperimentFlowMode flow,ExperimentRunQualification q,bool expected)=>Assert.That(ExperimentRuntimeContext.IsAllowed(flow,q),Is.EqualTo(expected));
        [Test] public void T29_FormalAssignmentUsesRealAllocator()
        { var ok=new ExperimentAssignmentAllocator().TryCreateRehearsal("P01","S01",protocol,tasks,resources.ResourceSnapshotId,out var a,out var e);Assert.That(ok,Is.True,e);AssertIsolation(a);Assert.That(a.conditions.Select(x=>x.formalConditionCode).Distinct().Count(),Is.EqualTo(4));Assert.That(a.conditions.Select(x=>x.task.taskId).Distinct().Count(),Is.EqualTo(4)); }
        [Test] public void T30_FormalAssignmentStable()
        { var x=new ExperimentAssignmentAllocator();x.TryCreateRehearsal("P02","S",protocol,tasks,resources.ResourceSnapshotId,out var a,out _);x.TryCreateRehearsal("P02","S2",protocol,tasks,resources.ResourceSnapshotId,out var b,out _);Assert.That(a.sequenceId,Is.EqualTo(b.sequenceId));Assert.That(a.conditions.Select(v=>v.task.taskId),Is.EqualTo(b.conditions.Select(v=>v.task.taskId))); }
        [Test] public void T31_PilotAssignmentUsesRealAllocator()
        { var ok=new PilotAssignmentAllocator().TryCreateRehearsal("P03","S",protocol,tasks,resources.ResourceSnapshotId,out var a,out var e);Assert.That(ok,Is.True,e);AssertIsolation(a);Assert.That(a.conditions.Select(x=>x.embodimentCondition).Distinct().Count(),Is.EqualTo(3));Assert.That(a.conditions.Select(x=>x.task.taskId).Distinct().Count(),Is.EqualTo(3)); }
        [Test] public void T32_PilotExplicitAndHeadLocked() { Assert.That(protocol.PilotFeedbackStyle,Is.EqualTo(PilotFeedbackStyleChoice.Explicit));Assert.That(protocol.VoiceOnlyAudioPolicy,Is.EqualTo(PilotAudioSourcePolicy.NonSpatialHeadLocked)); }
        [TestCase("hotel_check_in","barista_humanoid_v1")]
        [TestCase("furniture_shopping","teacher_humanoid_v1")]
        [TestCase("gym_membership","barista_male_humanoid_v1")]
        [TestCase("tourist_assistance","teacher_female_humanoid_v1")]
        public void T33_T36_FormalAvatarMapping(string task,string key)=>Assert.That(resources.FindAvatar(task).avatarPresetKey,Is.EqualTo(key));
        [Test] public void T37_PilotHumanoidMapping() { Assert.That(resources.PilotHumanoidPresetKey,Is.EqualTo("teacher_female_humanoid_v1"));Assert.That(resources.PilotHumanoidPrefab,Is.Not.Null); }
        [Test] public void T38_FivePanoramaKeysLoad() { foreach(var key in new[]{"SceneTalkVR/Textures/hotel-lobby-360","SceneTalkVR/Textures/furniture-store-360","SceneTalkVR/Textures/gym-360","SceneTalkVR/Textures/tourist-information-360","SceneTalkVR/Textures/restaurant-360"})Assert.That(Resources.Load<Texture2D>(key),Is.Not.Null,key); }
        [Test] public void T39_VoicesResolve() { Assert.That(voices.ValidateForRehearsal(out var e),Is.True,e);Assert.That(voices.Profiles.All(x=>x.voiceId=="101050"&&x.sampleRate==24000),Is.True); }
        [Test] public void T40_DeploymentIsLiveLoopback() { Assert.That(deployments.ValidateForRehearsal(out var e),Is.True,e);deployments.TryGet(ExperimentDeploymentProfileId.RehearsalEditor,out var p);Assert.That(p.EndpointHost,Is.EqualTo("127.0.0.1")); }
        [Test] public void T41_FormalPreflightAuditsLiveGateway() { var r=RehearsalValidation.Run(ExperimentFlowMode.Formal);Assert.That(r.checks.Contains("voice_gateway_reachable")||r.blockers.Any(x=>x.StartsWith("voice_gateway_unreachable:")),Is.True); }
        [Test] public void T42_PilotPreflightAuditsLiveGateway() { var r=RehearsalValidation.Run(ExperimentFlowMode.Pilot);Assert.That(r.checks.Contains("voice_gateway_reachable")||r.blockers.Any(x=>x.StartsWith("voice_gateway_unreachable:")),Is.True); }
        [Test] public void T43_EquivalenceContract() { Assert.That(RehearsalCollectionEquivalenceValidator.Validate(out var failures),Is.True,string.Join(";",failures)); }
        [Test] public void T44_ReadinessRequiresNoLifecycleCodeChange() { var r=RehearsalToCollectionReadinessReport.Create(protocol,resources);Assert.That(r.lifecycleCodeChangeRequired,Is.False);Assert.That(r.approvalGaps,Has.Length.EqualTo(7)); }
        [Test] public void T45_LegacyRuntimeModesRemainReadableOnly() { Assert.That(Enum.GetNames(typeof(ExperimentRuntimeMode)),Does.Contain("EditorDemoFormal").And.Contain("EditorDemoPilot"));Assert.That(typeof(EditorDemoSessionCoordinator).GetMethod("StartFormalDemo").GetCustomAttributes(typeof(ObsoleteAttribute),false),Is.Not.Empty); }
        [Test] public void T46_RunLimitsAreInjectableIntoSharedLifecycles()
        {
            var go=new GameObject("rehearsal-limit-test");try{var formal=go.AddComponent<ExperimentLifecycleCoordinator>();var pilot=go.AddComponent<PilotWorkflowCoordinator>();formal.ConfigureRunLimits(protocol.FormalMaxTurns,protocol.FormalMaxDurationMinutes);pilot.ConfigureRunLimits(protocol.PilotMaxTurns,protocol.PilotMaxDurationMinutes);Assert.That((formal.MaximumTurns,formal.MaximumDurationMinutes),Is.EqualTo((6,10f)));Assert.That((pilot.MaximumTurns,pilot.MaximumDurationMinutes),Is.EqualTo((5,8f)));}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void T47_PilotEventCarriesRehearsalQualificationMetadata()
        {
            foreach(var field in new[]{"flowMode","runQualification","protocolSnapshotId","resourceSnapshotId"})Assert.That(typeof(PilotEventRecord).GetField(field),Is.Not.Null,field);
        }

        private static void AssertIsolation(ExperimentAssignment a){Assert.That(a.flowMode,Is.EqualTo(ExperimentFlowMode.Formal));Assert.That(a.runQualification,Is.EqualTo(ExperimentRunQualification.Rehearsal));Assert.That(a.dataOrigin,Is.EqualTo("rehearsal"));Assert.That(a.collectionEligible,Is.False);Assert.That(a.developerTestAssignment,Is.False);Assert.That(a.demoMode,Is.False);}
        private static void AssertIsolation(PilotAssignment a){Assert.That(a.flowMode,Is.EqualTo(ExperimentFlowMode.Pilot));Assert.That(a.runQualification,Is.EqualTo(ExperimentRunQualification.Rehearsal));Assert.That(a.dataOrigin,Is.EqualTo("rehearsal"));Assert.That(a.collectionEligible,Is.False);Assert.That(a.developerTestAssignment,Is.False);Assert.That(a.demoMode,Is.False);}
        private static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
