using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class Stage7ReleaseReadinessTests
    {
        [Test] public void FormalMapping_DerivesOnlyTheFourApprovedCyclicSequences()
        {
            var protocol=ProtocolWith(new ExperimentProtocolDecision{decisionId="condition_letter_mapping",status=ProtocolDecisionStatus.Confirmed,confirmedValue="a=NE,b=NR,c=SE,d=SR"});
            Assert.That(protocol.TryResolveFormalSequences(out var sequences,out var error),Is.True,error);
            CollectionAssert.AreEqual(new[]{"a-b-c-d","b-c-d-a","c-d-a-b","d-a-b-c"},sequences.Select(x=>x.sequenceId));
            CollectionAssert.AreEqual(new[]{FormalConditionCode.NE,FormalConditionCode.NR,FormalConditionCode.SE,FormalConditionCode.SR},sequences[0].conditions);
            UnityEngine.Object.DestroyImmediate(protocol);
        }

        [TestCase("a=NE,b=NR,c=SE,d=SE")][TestCase("a=NE,b=NR,c=SE")][TestCase("a=NE,b=NR,c=SE,x=SR")]
        public void FormalMapping_RejectsIllegalOrIncompleteValues(string value)
        {
            var protocol=ProtocolWith(new ExperimentProtocolDecision{decisionId="condition_letter_mapping",status=ProtocolDecisionStatus.Confirmed,confirmedValue=value});
            Assert.That(protocol.TryResolveFormalSequences(out _,out _),Is.False);UnityEngine.Object.DestroyImmediate(protocol);
        }

        [Test] public void ConfirmedDecision_WithoutEvidence_FailsFormalValidation()
        {
            var protocol=ProtocolWith(new ExperimentProtocolDecision{decisionId="x",status=ProtocolDecisionStatus.Confirmed,confirmedValue="yes"});
            Assert.That(protocol.ValidateForFormalMode(out var error),Is.False);StringAssert.Contains("lacks provenance",error);UnityEngine.Object.DestroyImmediate(protocol);
        }

        [Test] public void ProtocolVersionChange_InvalidatesExistingAssignment()
        {
            var assignment=new ExperimentAssignment{assignmentVersion=ExperimentAssignmentAllocator.AssignmentVersion,protocolVersion="old",taskCatalogVersion="tasks"};
            Assert.That(ExperimentAssignmentAllocator.IsCompatible(assignment,"new","tasks",out var reason),Is.False);Assert.That(reason,Is.EqualTo("protocol_version_changed"));
        }

        [Test] public void FormalAvatar_RequiresExactSemanticApprovedPreset()
        {
            var catalog=ScriptableObject.CreateInstance<AvatarCatalog>();catalog.presets=new[]{new AvatarPresetEntry{key="teacher",semanticRole="teacher",mobileReady=true}};
            Assert.That(catalog.ValidateExactFormalPreset("hotel_receptionist_v1","hotel receptionist",out var error),Is.False);StringAssert.Contains("missing",error);UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test] public void PicoDeployment_RejectsLoopbackAndMockProviders()
        {
            var catalog=ScriptableObject.CreateInstance<ExperimentDeploymentCatalog>();catalog.EditorSet("test",new[]{new ExperimentDeploymentProfile{profileId=ExperimentDeploymentProfileId.PicoLab,voiceGatewayBaseUrl="http://127.0.0.1:8765",networkRequired=true,approvedForCollection=true,evidenceReference="approval",sttProvider="mock",ttsProvider="mock"}});
            Assert.That(catalog.ValidateForCollection(ExperimentDeploymentProfileId.PicoLab,out var error),Is.False);StringAssert.Contains("loopback",error);StringAssert.Contains("mock_provider",error);UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test] public void DeploymentProfiles_HaveNoSecretFieldsOrQuerySecrets()
        {
            Assert.That(typeof(ExperimentDeploymentProfile).GetFields().Any(x=>x.Name.IndexOf("key",StringComparison.OrdinalIgnoreCase)>=0||x.Name.IndexOf("secret",StringComparison.OrdinalIgnoreCase)>=0||x.Name.IndexOf("token",StringComparison.OrdinalIgnoreCase)>=0),Is.False);
            Assert.That(ExperimentDeploymentCatalog.ContainsSecretMaterial("https://server/voice?token=secret"),Is.True);
        }

        [Test] public void VoiceCatalog_RejectsUnconfirmedCentralKeys()
        {
            var catalog=ScriptableObject.CreateInstance<ExperimentVoiceProfileCatalog>();catalog.EditorSet("test","","","",Array.Empty<ExperimentVoiceProfile>());
            Assert.That(catalog.ValidateForLockedCollection(Array.Empty<string>(),out var error),Is.False);StringAssert.Contains("approved_voice_profiles_missing",error);UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test] public void IntegrityAuditor_DetectsDialogueBeforeFeedback_AndDoesNotModifyInput()
        {
            var folder=Path.Combine(Path.GetTempPath(),"SceneTalkVR_stage7_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder,"p_s_assignment.json"),"{}");var timing=Path.Combine(folder,"p_s_timing.jsonl");
                File.WriteAllLines(timing,new[]{"{\"participantId\":\"p\",\"sessionId\":\"s\",\"turnId\":\"t1\",\"eventType\":\"DialoguePlaybackStarted\",\"monotonicElapsedMs\":10}","{\"participantId\":\"p\",\"sessionId\":\"s\",\"turnId\":\"t1\",\"eventType\":\"CorrectionPlaybackStarted\",\"monotonicElapsedMs\":20}"});
                var before=File.ReadAllText(timing);var report=SessionDataIntegrityAuditor.Audit(folder,"p","s");Assert.That(report.result,Is.EqualTo(DataIntegritySeverity.Fail));Assert.That(report.findings.Any(x=>x.checkId=="feedback_first"),Is.True);Assert.That(File.ReadAllText(timing),Is.EqualTo(before));
            }
            finally{Directory.Delete(folder,true);}
        }

        private static ExperimentV11ProtocolConfig ProtocolWith(params ExperimentProtocolDecision[] decisions)
        {
            var protocol=ScriptableObject.CreateInstance<ExperimentV11ProtocolConfig>();typeof(ExperimentV11ProtocolConfig).GetField("requiredDecisions",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(protocol,decisions);return protocol;
        }
    }
}
