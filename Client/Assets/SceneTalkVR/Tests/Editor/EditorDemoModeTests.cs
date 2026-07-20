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
    public sealed class EditorDemoModeTests
    {
        private ExperimentV11EditorDemoProtocol protocol;
        private EditorDemoAvatarMappingCatalog mapping;
        private ExperimentVoiceProfileCatalog voices;
        private ExperimentDeploymentCatalog deployment;
        private ExperimentTaskCatalog tasks;

        [SetUp] public void SetUp() { EditorDemoAssetBuilder.CreateOrUpdate(); protocol = Load<ExperimentV11EditorDemoProtocol>(EditorDemoAssetBuilder.ProtocolPath); mapping = Load<EditorDemoAvatarMappingCatalog>(EditorDemoAssetBuilder.MappingPath); voices = Load<ExperimentVoiceProfileCatalog>(EditorDemoAssetBuilder.VoicePath); deployment = Load<ExperimentDeploymentCatalog>(EditorDemoAssetBuilder.DeploymentPath); tasks = Load<ExperimentTaskCatalog>(EditorDemoAssetBuilder.Root + "ExperimentTaskCatalog.asset"); }
        [Test] public void T01_ProtocolHasElevenDemoApprovedDecisions() { Assert.That(protocol.Decisions.Count, Is.EqualTo(11)); Assert.That(protocol.Validate(out var e), Is.True, e); }
        [Test] public void T02_OfficialProtocolRemainsElevenUnconfirmed() { var p = Load<ExperimentV11ProtocolConfig>(EditorDemoAssetBuilder.Root + "ExperimentV11Protocol.asset"); Assert.That(p.RequiredDecisions.Count, Is.EqualTo(11)); Assert.That(p.RequiredDecisions.All(x => x.status == ProtocolDecisionStatus.Unconfirmed), Is.True); }
        [Test] public void T03_FormalLetterMappingIsFixed() { CollectionAssert.AreEqual(new[] { FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR }, protocol.FormalSequences[0].conditions); }
        [Test] public void T04_PilotMappingIsFixed() { CollectionAssert.AreEqual(new[] { PilotEmbodimentCondition.VoiceOnly, PilotEmbodimentCondition.FloatingOrb, PilotEmbodimentCondition.HumanoidAgent }, protocol.PilotSequences[0].conditions); }
        [Test] public void T05_FormalSequenceDefinitionsAreFourUniqueRotations() { Assert.That(protocol.FormalSequences.Length, Is.EqualTo(4)); Assert.That(protocol.FormalSequences.Select(x => x.sequenceId).Distinct().Count(), Is.EqualTo(4)); }
        [Test] public void T06_PilotSequenceDefinitionsAreThreeUniqueRotations() { Assert.That(protocol.PilotSequences.Length, Is.EqualTo(3)); Assert.That(protocol.PilotSequences.Select(x => x.sequenceId).Distinct().Count(), Is.EqualTo(3)); }
        [Test] public void T07_FormalCatalogHasFourTasks() => Assert.That(tasks.GetTasks(ExperimentTaskPhase.Formal).Count, Is.EqualTo(4));
        [Test] public void T08_PilotCatalogHasThreeTasks() => Assert.That(tasks.GetTasks(ExperimentTaskPhase.Pilot).Count, Is.EqualTo(3));
        [Test] public void T09_FormalAssignmentHasEachConditionAndTaskOnce() { var a = Formal(); Assert.That(a.conditions.Select(x => x.formalConditionCode).Distinct().Count(), Is.EqualTo(4)); Assert.That(a.conditions.Select(x => x.task.taskId).Distinct().Count(), Is.EqualTo(4)); }
        [Test] public void T10_PilotAssignmentHasEachConditionAndTaskOnce() { var a = Pilot(); Assert.That(a.conditions.Select(x => x.embodimentCondition).Distinct().Count(), Is.EqualTo(3)); Assert.That(a.conditions.Select(x => x.task.taskId).Distinct().Count(), Is.EqualTo(3)); }
        [Test] public void T11_DemoAssignmentIsolationFlagsAreRequired() { var a = Formal(); a.runtimeMode = ExperimentRuntimeMode.EditorDemoFormal; a.demoMode = true; a.dataOrigin = "editor_demo"; a.collectionEligible = false; a.developerTestAssignment = true; Assert.That(a.collectionEligible, Is.False); Assert.That(a.developerTestAssignment, Is.True); Assert.That(a.demoMode, Is.True); }
        [Test] public void T12_DemoVoiceProfilesAreNeverCollectionApproved() => Assert.That(voices.Profiles.All(x => x.approvedForEditorDemo && !x.approvedForCollection), Is.True);
        [Test] public void T13_DemoDeploymentIsNeverCollectionApproved() { Assert.That(deployment.TryGet(ExperimentDeploymentProfileId.EditorDemo, out var p), Is.True); Assert.That(p.approvedForEditorDemo, Is.True); Assert.That(p.approvedForCollection, Is.False); Assert.That(p.collectionAllowed, Is.False); }
        [Test] public void T14_AllFormalTasksHaveExplicitDemoAvatarMappings() => Assert.That(tasks.GetTasks(ExperimentTaskPhase.Formal).All(x => !string.IsNullOrWhiteSpace(mapping.Find(x.taskId)?.demoAvatarKey)), Is.True);
        [Test] public void T15_DemoAvatarsAreSemanticPlaceholders() => Assert.That(mapping.FormalMappings.All(x => x.demoPlaceholder && !x.semanticRoleApproved), Is.True);
        [Test] public void T16_DemoHumanoidIsExplicitAndNotOrbFallback() { Assert.That(mapping.PilotHumanoidPrefab, Is.Not.Null); Assert.That(mapping.PilotHumanoidPrefabKey, Is.Not.EqualTo("generated_orb_v1")); }
        [Test] public void T17_VoiceOnlyPolicyIsHeadLocked() => Assert.That(protocol.VoiceOnlyAudioPolicy, Is.EqualTo(PilotAudioSourcePolicy.NonSpatialHeadLocked));
        [Test] public void T18_PilotUsesSharedExplicitFeedbackVoice() { Assert.That(protocol.PilotFeedbackStyle, Is.EqualTo(PilotFeedbackStyleChoice.Explicit)); Assert.That(voices.PilotSharedFeedbackProfileKey, Is.EqualTo("editor_demo_feedback_voice")); }
        [Test] public void T19_FormalAndPilotPreflightDoNotClaimCollectionReady() { Assert.That(EditorDemoPreflight.Run(true).status, Is.Not.EqualTo((EditorDemoPreflightStatus)99)); Assert.That(EditorDemoPreflight.Run(false).status, Is.Not.EqualTo((EditorDemoPreflightStatus)99)); }
        [Test] public void T20_OfficialRcGateStillBlockedByUnconfirmedDecisions() { var p = Load<ExperimentV11ProtocolConfig>(EditorDemoAssetBuilder.Root + "ExperimentV11Protocol.asset"); Assert.That(p.RequiredDecisions.Any(x => x.status != ProtocolDecisionStatus.Confirmed), Is.True); }
        [Test] public void T21_FormalDemoBundlePassesIntegrityWithIsolationMetadata() { Bundle(true); }
        [Test] public void T22_PilotDemoBundlePassesIntegrityWithIsolationMetadata() { Bundle(false); }

        private ExperimentAssignment Formal() { var ok = new ExperimentAssignmentAllocator().TryCreateForTesting("DEMO-FORMAL-TEST", "s", protocol.DemoProtocolVersion, tasks.CatalogVersion, protocol.FormalSequences, tasks.GetTasks(ExperimentTaskPhase.Formal).Select(x => x.taskId).ToArray(), AssignmentPolicy.StrictWithoutReplacement, out var a, out var e); Assert.That(ok, Is.True, e); return a; }
        private PilotAssignment Pilot() { var ok = new PilotAssignmentAllocator().TryCreateForTesting("DEMO-PILOT-TEST", "s", protocol.DemoProtocolVersion, tasks.CatalogVersion, protocol.PilotSequences, tasks.GetTasks(ExperimentTaskPhase.Pilot).Select(x => x.taskId).ToArray(), PilotFeedbackStyleChoice.Explicit, PilotAudioSourcePolicy.NonSpatialHeadLocked, true, out var a, out var e); Assert.That(ok, Is.True, e); return a; }
        private void Bundle(bool formal)
        {
            var root = Path.Combine(Path.GetTempPath(), "scenetalk-demo-bundle-" + Guid.NewGuid().ToString("N"));
            try
            {
                ExperimentAssignment fa = null; PilotAssignment pa = null;
                if (formal) { fa = Formal(); fa.runtimeMode = ExperimentRuntimeMode.EditorDemoFormal; fa.demoMode = true; fa.demoProtocolVersion = protocol.DemoProtocolVersion; fa.dataOrigin = "editor_demo"; fa.collectionEligible = false; fa.developerTestAssignment = true; foreach (var c in fa.conditions) { c.status = ConditionRunStatus.Completed; c.latestConditionRunId = "run-" + c.conditionPosition; } }
                else { pa = Pilot(); pa.runtimeMode = ExperimentRuntimeMode.EditorDemoPilot; pa.demoMode = true; pa.demoProtocolVersion = protocol.DemoProtocolVersion; pa.dataOrigin = "editor_demo"; pa.collectionEligible = false; pa.developerTestAssignment = true; foreach (var c in pa.conditions) { c.status = PilotRunStatus.Completed; c.latestPilotRunId = "run-" + c.conditionPosition; } }
                Assert.That(EditorDemoBundleExporter.Export(root, fa, pa, protocol.DemoProtocolVersion, "1.1.0-stage7", true, formal, out var path, out var error), Is.True, error);
                Assert.That(SessionDataIntegrityAuditor.Audit(path, formal ? fa.participantId : pa.participantId, "s").result, Is.EqualTo(DataIntegritySeverity.Pass));
                var manifest = JsonUtility.FromJson<SessionBundleManifest>(File.ReadAllText(Path.Combine(path, "manifest.json"))); Assert.That(manifest.dataOrigin, Is.EqualTo("editor_demo")); Assert.That(manifest.collectionEligible, Is.False);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
