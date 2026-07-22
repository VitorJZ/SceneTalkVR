using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class Stage9ExperimentMatrixTests
    {
        [Test]
        public void Definitions_HaveExactUniqueCases()
        {
            var formal = ExperimentMatrixDefinition.Formal();
            var pilot = ExperimentMatrixDefinition.Pilot();
            Assert.That(formal.cases.Length, Is.EqualTo(16));
            Assert.That(pilot.cases.Length, Is.EqualTo(9));
            Assert.That(formal.cases.Select(x => x.caseId).Distinct().Count(), Is.EqualTo(16));
            Assert.That(pilot.cases.Select(x => x.caseId).Distinct().Count(), Is.EqualTo(9));
        }

        [Test]
        public void LockedMatrices_AreBlockedNotFailed()
        {
            WithTemp(root =>
            {
                var formal = ExperimentMatrixRunnerService.Run(ExperimentMatrixDefinition.Formal(), ExperimentMatrixExecutionMode.LockedCollection, root, "sha", null, null, "q", null);
                var pilot = ExperimentMatrixRunnerService.Run(ExperimentMatrixDefinition.Pilot(), ExperimentMatrixExecutionMode.LockedCollection, root, "sha", null, null, "q", null);
                Assert.That(formal.blocked, Is.EqualTo(16)); Assert.That(formal.failed, Is.Zero); Assert.That(formal.passed, Is.Zero);
                Assert.That(pilot.blocked, Is.EqualTo(9)); Assert.That(pilot.failed, Is.Zero); Assert.That(pilot.passed, Is.Zero);
                Assert.That(formal.results.All(x => x.blockerIds.Contains("protocol_asset_missing")), Is.True);
                Assert.That(pilot.results.All(x => x.blockerIds.Contains("approved_pilot_humanoid_missing")), Is.True);
            });
        }

        [Test]
        public void SyntheticFormalMatrix_Passes16_WithIndependentEvidenceAndBundles()
        {
            WithTemp(root =>
            {
                var run = ExperimentMatrixRunnerService.Run(ExperimentMatrixDefinition.Formal(), ExperimentMatrixExecutionMode.Synthetic, root, "060889b", null, null, "q", null);
                Assert.That(run.totalCases, Is.EqualTo(16)); Assert.That(run.passed, Is.EqualTo(16)); Assert.That(run.failed, Is.Zero);
                Assert.That(run.collectionEligible, Is.False); Assert.That(run.dataOrigin, Is.EqualTo("synthetic_matrix"));
                Assert.That(run.results.Select(x => x.evidence.participantId).Distinct().Count(), Is.EqualTo(16));
                Assert.That(run.results.Select(x => x.evidence.conditionRunId).Distinct().Count(), Is.EqualTo(16));
                Assert.That(run.results.Select(x => x.evidence.taskAssignmentId).Distinct().Count(), Is.EqualTo(16));
                Assert.That(run.results.Select(x => x.evidence.questionnaireLinkageKey).Distinct().Count(), Is.EqualTo(16));
                Assert.That(run.results.All(ValidCase), Is.True);
                Assert.That(run.results.All(x => x.evidence.goalTraceValid && x.evidence.noFeedbackValid && x.evidence.feedbackFirstValid), Is.True);
            });
        }

        [Test]
        public void SyntheticPilotMatrix_Passes9_AndEnforcesEmbodimentPolicies()
        {
            WithTemp(root =>
            {
                var run = ExperimentMatrixRunnerService.Run(ExperimentMatrixDefinition.Pilot(), ExperimentMatrixExecutionMode.Synthetic, root, "060889b", null, null, "q", null);
                Assert.That(run.totalCases, Is.EqualTo(9)); Assert.That(run.passed, Is.EqualTo(9)); Assert.That(run.failed, Is.Zero);
                Assert.That(run.results.Where(x => x.embodimentCondition == "voice_only").All(x => !x.evidence.visualEntityCreated && x.evidence.visualEntityType == "none"), Is.True);
                Assert.That(run.results.Where(x => x.embodimentCondition == "floating_orb").All(x => x.evidence.visualEntityCreated && x.evidence.visualEntityType == "orb"), Is.True);
                Assert.That(run.results.Where(x => x.embodimentCondition == "humanoid_agent").All(x => x.evidence.placeholderUsed && x.evidence.visualEntityType == "humanoid_placeholder"), Is.True);
                Assert.That(run.results.Select(x => x.evidence.feedbackTextHash).Distinct().Count(), Is.EqualTo(1));
                Assert.That(run.results.Select(x => x.evidence.voiceProfileKey).Distinct().Count(), Is.EqualTo(1));
                Assert.That(run.results.Select(x => x.evidence.speakingSpeed).Distinct().Count(), Is.EqualTo(1));
                Assert.That(run.results.Select(x => x.evidence.volume).Distinct().Count(), Is.EqualTo(1));
                Assert.That(run.results.All(ValidCase), Is.True);
            });
        }

        [Test]
        public void DeveloperPlaceholder_IsExplicitAndCollectionIneligible()
        {
            WithTemp(root =>
            {
                var one = ExperimentMatrixDefinition.Formal(); one.cases = one.cases.Take(1).ToArray();
                var run = ExperimentMatrixRunnerService.Run(one, ExperimentMatrixExecutionMode.DeveloperPlaceholder, root, "sha", null, null, "q", null);
                Assert.That(run.passed, Is.EqualTo(1)); Assert.That(run.collectionEligible, Is.False);
                Assert.That(run.results[0].evidence.placeholderUsed, Is.True);
                Assert.That(run.results[0].evidence.dataOrigin, Is.EqualTo("developer_placeholder_matrix"));
            });
        }

        [Test]
        public void MatrixRun_DoesNotModifyOfficialProtocol()
        {
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
            Assert.That(protocol, Is.Not.Null); var before = EditorJsonUtility.ToJson(protocol, true);
            WithTemp(root =>
            {
                var one = ExperimentMatrixDefinition.Pilot(); one.cases = one.cases.Take(1).ToArray();
                ExperimentMatrixRunnerService.Run(one, ExperimentMatrixExecutionMode.Synthetic, root, "sha", protocol, null, "q", null);
            });
            Assert.That(EditorJsonUtility.ToJson(protocol, true), Is.EqualTo(before));
        }

        [Test]
        public void FormalProviderStyleMapping_IsCanonical()
        {
            WithTemp(root =>
            {
                var definition = ExperimentMatrixDefinition.Formal(); definition.cases = definition.cases.Where(x => x.taskId == "hotel_check_in").ToArray();
                var run = ExperimentMatrixRunnerService.Run(definition, ExperimentMatrixExecutionMode.Synthetic, root, "sha", null, null, "q", null);
                AssertEvidence(run, "NE", "Non-Split / Dialogue Avatar", "Explicit", "Avatar");
                AssertEvidence(run, "NR", "Non-Split / Dialogue Avatar", "Recast", "Avatar");
                AssertEvidence(run, "SE", "Split / Assistant Agent", "Explicit", "Agent");
                AssertEvidence(run, "SR", "Split / Assistant Agent", "Recast", "Agent");
            });
        }

        private static void AssertEvidence(ExperimentMatrixRunManifest run, string code, string provider, string style, string actor)
        {var item=run.results.Single(x=>x.conditionCode==code);Assert.That(item.evidence.provider,Is.EqualTo(provider));Assert.That(item.evidence.style,Is.EqualTo(style));Assert.That(item.evidence.actualPlaybackActor,Is.EqualTo(actor));}
        private static bool ValidCase(ExperimentMatrixCaseResult item)
        {
            if (item.status != "PASS" || item.integrityStatus != "PASS" || item.evidence.collectionEligible || !item.evidence.developerTestAssignment) return false;
            if (!File.Exists(Path.Combine(item.sessionBundlePath, "manifest.json")) || !File.Exists(Path.Combine(item.sessionBundlePath, "checksums.sha256"))) return false;
            foreach (var line in File.ReadAllLines(Path.Combine(item.sessionBundlePath, "checksums.sha256")))
            {var pair=line.Split(new[]{"  "},2,StringSplitOptions.None);if(pair.Length!=2||SessionBundleExporter.Sha256File(Path.Combine(item.sessionBundlePath,pair[1].Replace('/',Path.DirectorySeparatorChar)))!=pair[0])return false;}
            return true;
        }
        private static void WithTemp(Action<string> body){var root=Path.Combine(Path.GetTempPath(),"SceneTalkVR-stage9-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{body(root);}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
    }
}
