using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.EditorTools;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class Stage8DesktopDryRunTests
    {
        [Test] public void ProjectRunner_ExcludesUnitySkillsPackageTests()
        {
            Assert.That(SceneTalkVRProjectTestRunner.IsProjectTestName("SceneTalkVR.Tests.Editor.Stage7ReleaseReadinessTests"),Is.True);
            Assert.That(SceneTalkVRProjectTestRunner.IsProjectTestName("UnitySkills.Tests.Core.NewCapabilitiesTests"),Is.False);
            var edit=SceneTalkVRProjectTestRunner.BuildProjectFilter(TestMode.EditMode);CollectionAssert.AreEqual(new[]{"^SceneTalkVR\\.Tests\\."},edit.groupNames);
            var play=SceneTalkVRProjectTestRunner.BuildProjectFilter(TestMode.PlayMode);CollectionAssert.AreEqual(new[]{"SceneTalkVR.Stage2.PlayModeTests"},play.assemblyNames);
        }

        [Test] public void ProjectRunner_ProducesJsonAndAggregateXmlSchema()
        {
            var summary=new SceneTalkVRProjectTestSummary{testMode="EditMode",gitCommit="abc",unityVersion="6000",protocolVersion="p",testCount=2,passed=2,unitySkillsPackageTestsExcluded=true};
            StringAssert.Contains("\"testCount\": 2",JsonUtility.ToJson(summary,true));var xml=SceneTalkVRProjectTestRunner.BuildAggregateXml(summary,"raw.xml");StringAssert.Contains("project-test-results",xml);StringAssert.Contains("unityskills-package-tests-excluded=\"true\"",xml);
        }

        [Test] public void DecisionTemplateSchema_IsCompleteAndValidDraft()
        {
            var doc=ApprovedDocument(false);Assert.That(ProtocolDecisionIntakeValidator.Validate(doc,false,out var errors),Is.True,string.Join(";",errors));CollectionAssert.AreEquivalent(ProtocolDecisionIntakeValidator.RequiredDecisionIds,doc.decisions.Select(x=>x.decisionId));
        }

        [Test] public void DecisionWithoutEvidence_IsRejected()
        {var doc=ApprovedDocument(true);doc.decisions[0].evidenceReference="";Assert.That(ProtocolDecisionIntakeValidator.Validate(doc,true,out var errors),Is.False);Assert.That(errors.Any(x=>x.Contains("provenance_required")),Is.True);}

        [TestCase("a=NE,b=NR,c=SE,d=SE","condition_letter_mapping")][TestCase("a=voice_only,b=floating_orb,c=floating_orb","pilot_sequence_mapping")][TestCase("random","formal_task_no_replacement")][TestCase("0","formal_max_turns")][TestCase("1-7","questionnaire_scale_anchors")]
        public void IllegalDecisionValue_IsRejected(string value,string id){Assert.That(ProtocolDecisionIntakeValidator.ValidateValue(id,value,out _),Is.False);}

        [Test] public void DecisionPreview_DoesNotModifyProtocol()
        {var protocol=ScriptableObject.CreateInstance<ExperimentV11ProtocolConfig>();var before=UnityEditor.EditorJsonUtility.ToJson(protocol);var json=JsonUtility.ToJson(ApprovedDocument(true));var preview=ProtocolDecisionImportWindow.BuildPreview(json,protocol);Assert.That(preview.valid,Is.True,string.Join(";",preview.errors));Assert.That(UnityEditor.EditorJsonUtility.ToJson(protocol),Is.EqualTo(before));UnityEngine.Object.DestroyImmediate(protocol);}

        [Test] public void DecisionTransactionFailure_RestoresWholeProtocol()
        {
            var protocol=ScriptableObject.CreateInstance<ExperimentV11ProtocolConfig>();var field=typeof(ExperimentV11ProtocolConfig).GetField("requiredDecisions",BindingFlags.Instance|BindingFlags.NonPublic);var original=(ExperimentProtocolDecision[])field.GetValue(protocol);field.SetValue(protocol,original.Take(10).ToArray());var before=UnityEditor.EditorJsonUtility.ToJson(protocol,true);
            Assert.That(ProtocolDecisionImportWindow.ApplyTransaction(protocol,ApprovedDocument(true),JsonUtility.ToJson(ApprovedDocument(true)),"APPLY_APPROVED_PROTOCOL_DECISIONS",out _),Is.False);Assert.That(UnityEditor.EditorJsonUtility.ToJson(protocol,true),Is.EqualTo(before));UnityEngine.Object.DestroyImmediate(protocol);
        }

        [Test] public void SyntheticAssignment_CannotEnterFormalCollection()
        {
            var go=new GameObject("stage8-formal-isolation");try{var manager=go.AddComponent<ExperimentConditionManager>();typeof(ExperimentConditionManager).GetField("formalExperiment",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,true);var lifecycle=go.GetComponent<ExperimentLifecycleCoordinator>()??go.AddComponent<ExperimentLifecycleCoordinator>();lifecycle.Configure(manager);var assignment=new ExperimentAssignment{participantId="p",experimentSessionId="s",developerTestAssignment=true,dataOrigin="synthetic_dry_run",collectionEligible=false};Assert.That(lifecycle.LoadAssignment(assignment,out var error),Is.False);StringAssert.Contains("developer_assignment",error);}finally{UnityEngine.Object.DestroyImmediate(go);}
        }

        [Test] public void CompleteSyntheticFormalBundle_AuditsPass()
        {WithTemp(root=>{var result=SyntheticDryRunEngine.RunFormal(root,"synthetic-p","formal-s");Assert.That(result.success,Is.True,result.error);Assert.That(result.integrityStatus,Is.EqualTo("PASS"));AssertManifest(result.bundleDirectory,"formal",4);});}

        [Test] public void CompleteSyntheticPilotBundle_AuditsPassAndRetainsRetry()
        {WithTemp(root=>{var result=SyntheticDryRunEngine.RunPilot(root,"synthetic-p","pilot-s");Assert.That(result.success,Is.True,result.error);AssertManifest(result.bundleDirectory,"pilot",3);var study=File.ReadAllText(Path.Combine(result.bundleDirectory,"study","study.jsonl"));StringAssert.Contains("pilot-run-invalid",study);StringAssert.Contains("pilot-run-1",study);StringAssert.Contains("RetryAuthorized",study);});}

        [Test] public void BundleChecksums_AreCorrect()
        {WithTemp(root=>{var result=SyntheticDryRunEngine.RunFormal(root,"p","checksums");foreach(var line in File.ReadAllLines(Path.Combine(result.bundleDirectory,"checksums.sha256"))){var pair=line.Split(new[]{"  "},2,StringSplitOptions.None);Assert.That(SessionBundleExporter.Sha256File(Path.Combine(result.bundleDirectory,pair[1].Replace('/',Path.DirectorySeparatorChar))),Is.EqualTo(pair[0]));}});}

        [Test] public void Auditor_FailsDialogueBeforeFeedback_AndDoesNotModifyInputs()
        {WithTemp(root=>{var result=SyntheticDryRunEngine.RunFormal(root,"p","bad-order");var path=Path.Combine(result.bundleDirectory,"timing","timing.jsonl");var lines=File.ReadAllLines(path);var correction=Array.FindIndex(lines,x=>x.Contains("CorrectionPlaybackStarted"));var dialogue=Array.FindIndex(lines,x=>x.Contains("DialoguePlaybackStarted"));(lines[correction],lines[dialogue])=(lines[dialogue],lines[correction]);File.WriteAllLines(path,lines);var before=File.ReadAllText(path);var audit=SessionDataIntegrityAuditor.Audit(result.bundleDirectory,"p","bad-order");Assert.That(audit.result,Is.EqualTo(DataIntegritySeverity.Fail));Assert.That(File.ReadAllText(path),Is.EqualTo(before));});}

        [Test] public void Auditor_FailsWhenQuestionnaireMissing()
        {WithTemp(root=>{var result=SyntheticDryRunEngine.RunFormal(root,"p","missing-q");File.Delete(Path.Combine(result.bundleDirectory,"questionnaire","questionnaire.jsonl"));var audit=SessionDataIntegrityAuditor.Audit(result.bundleDirectory,"p","missing-q");Assert.That(audit.result,Is.EqualTo(DataIntegritySeverity.Fail));Assert.That(audit.findings.Any(x=>x.checkId.Contains("questionnaire")||x.checkId.Contains("bundle")),Is.True);});}

        [Test] public void ProtocolVersionChange_StillInvalidatesOldAssignment()
        {var a=new ExperimentAssignment{assignmentVersion=ExperimentAssignmentAllocator.AssignmentVersion,protocolVersion="old",taskCatalogVersion="tasks"};Assert.That(ExperimentAssignmentAllocator.IsCompatible(a,"new","tasks",out var reason),Is.False);Assert.That(reason,Is.EqualTo("protocol_version_changed"));}

        private static ProtocolDecisionIntakeDocument ApprovedDocument(bool approved)
        {
            string Value(string id)=>id switch{"condition_letter_mapping"=>"a=NE,b=NR,c=SE,d=SR","formal_task_no_replacement"=>"strict_without_replacement","formal_social_comfort"=>"included","pilot_feedback_style"=>"explicit","voice_only_spatial_audio"=>"non_spatial_head_locked","pilot_sequence_mapping"=>"a=voice_only,b=floating_orb,c=humanoid_agent","formal_max_turns"=>"12","formal_max_duration"=>"20","pilot_max_turns"=>"10","pilot_max_duration"=>"15",_=>"1 = Strongly disagree / 非常不同意; 7 = Strongly agree / 非常同意"};
            return new ProtocolDecisionIntakeDocument{decisions=ProtocolDecisionIntakeValidator.RequiredDecisionIds.Select(id=>new ProtocolDecisionIntakeItem{decisionId=id,proposedValue=Value(id),allowedValues=Array.Empty<string>(),confirmedBy=approved?"researcher":"",confirmedAtUtc=approved?"2026-07-19T00:00:00Z":"",evidenceReference=approved?"approval/minutes":"",approvalStatus=approved?"Approved":"Draft"}).ToArray()};
        }
        private static void AssertManifest(string bundle,string mode,int expected){var manifest=JsonUtility.FromJson<SessionBundleManifest>(File.ReadAllText(Path.Combine(bundle,"manifest.json")));Assert.That(manifest.dataOrigin,Is.EqualTo("synthetic_dry_run"));Assert.That(manifest.collectionEligible,Is.False);Assert.That(manifest.sessionMode,Is.EqualTo(mode));var assignment=File.ReadAllText(Path.Combine(bundle,"assignment","assignment.json"));StringAssert.Contains("\"developerTestAssignment\": true",assignment);StringAssert.Contains("\"collectionEligible\": false",assignment);if(mode=="formal")StringAssert.Contains("\"formalConditionLabel\": \"NE\"",assignment);else StringAssert.Contains("\"embodimentConditionLabel\": \"voice_only\"",assignment);Assert.That(expected,Is.GreaterThan(0));}
        private static void WithTemp(Action<string> body){var path=Path.Combine(Path.GetTempPath(),"SceneTalkVR-stage8-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);try{body(path);}finally{if(Directory.Exists(path))Directory.Delete(path,true);}}
    }
}
