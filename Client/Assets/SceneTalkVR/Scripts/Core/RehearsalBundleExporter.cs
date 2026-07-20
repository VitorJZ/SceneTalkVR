using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public static class RehearsalBundleExporter
    {
        public const string BundleSchemaVersion = "1.1-collection-equivalent";

        public static bool Export(string root, ExperimentAssignment formal, PilotAssignment pilot,
            ExperimentV11RehearsalProtocol protocol, ExperimentV11RehearsalResourceCatalog resources,
            bool rankingSubmitted, bool interviewSaved, out string bundle, out string error)
        {
            bundle = string.Empty; error = string.Empty; var isFormal = formal != null;
            var participant = isFormal ? formal.participantId : pilot?.participantId;
            var session = isFormal ? formal.experimentSessionId : pilot?.sessionId;
            if (protocol == null || resources == null || string.IsNullOrWhiteSpace(participant) || string.IsNullOrWhiteSpace(session)) { error = "rehearsal_bundle_context_missing"; return false; }
            if (!rankingSubmitted || isFormal && !interviewSaved) { error = "rehearsal_final_ranking_or_interview_incomplete"; return false; }
            if (isFormal && !Valid(formal)) { error = "formal_rehearsal_incomplete_or_not_isolated"; return false; }
            if (!isFormal && !Valid(pilot)) { error = "pilot_rehearsal_incomplete_or_not_isolated"; return false; }

            var raw = Path.Combine(root, "raw"); var source = Path.Combine(root, "bundle-source"); bundle = Path.Combine(root, "bundle");
            if (Directory.Exists(source)) Directory.Delete(source, true); if (Directory.Exists(bundle)) Directory.Delete(bundle, true);
            foreach (var folder in new[] { "assignment", "timing", "study", "questionnaire", "ranking", "interview", "integrity" }) Directory.CreateDirectory(Path.Combine(source, folder));
            File.WriteAllText(Path.Combine(source, "assignment", "assignment.json"), isFormal ? JsonUtility.ToJson(formal, true) : JsonUtility.ToJson(pilot, true), Encoding.UTF8);
            CopyRaw(raw, source);
            var manifest = new SessionBundleManifest
            {
                bundleSchemaVersion = BundleSchemaVersion, dataOrigin = "rehearsal", collectionEligible = false,
                developerTestAssignment = false, demoMode = false, runtimeMode = string.Empty,
                flowMode = isFormal ? ExperimentFlowMode.Formal.ToString() : ExperimentFlowMode.Pilot.ToString(),
                runQualification = ExperimentRunQualification.Rehearsal.ToString(), sessionMode = isFormal ? "formal" : "pilot",
                participantId = participant, sessionId = session, gitCommit = Resources.Load<ExperimentBuildInfo>("Experiment/ExperimentBuildInfo")?.GitCommit ?? string.Empty,
                protocolVersion = protocol.ProtocolVersion, protocolSnapshotId = protocol.ProtocolSnapshotId,
                resourceSnapshotId = resources.ResourceSnapshotId,
                taskCatalogVersion = isFormal ? formal.taskCatalogVersion : pilot.taskCatalogVersion,
                questionnaireCatalogVersion = "1.1-stage5.1", assignmentVersion = isFormal ? formal.assignmentVersion : pilot.pilotAssignmentVersion,
                formalConditionOrderPolicy = isFormal ? formal.formalConditionOrderPolicy : string.Empty,
                taskAssignmentPolicy = isFormal ? formal.taskAssignmentPolicy : string.Empty,
                goalConfirmationPolicy = isFormal ? formal.goalConfirmationPolicy : string.Empty,
                questionnaireReturnPolicy = isFormal ? formal.questionnaireReturnPolicy : string.Empty,
                assignmentAlgorithmVersion = isFormal ? formal.assignmentAlgorithmVersion : string.Empty,
                randomSeedHash = isFormal ? formal.randomSeedHash : string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("o")
            };
            if (!SessionBundleExporter.Export(source, bundle, manifest, out error)) return false;
            var audit = SessionDataIntegrityAuditor.Audit(bundle, participant, session);
            manifest.integrityStatus = audit.result.ToString().ToUpperInvariant(); SessionBundleExporter.UpdateIntegrity(bundle, manifest, audit);
            return audit.result != DataIntegritySeverity.Fail;
        }

        private static bool Valid(ExperimentAssignment value) => value != null && value.dataOrigin == "rehearsal" && !value.collectionEligible
            && !value.developerTestAssignment && !value.demoMode && value.flowMode == ExperimentFlowMode.Formal
            && value.runQualification == ExperimentRunQualification.Rehearsal && value.conditions.All(x => x.status == ConditionRunStatus.Completed);
        private static bool Valid(PilotAssignment value) => value != null && value.dataOrigin == "rehearsal" && !value.collectionEligible
            && !value.developerTestAssignment && !value.demoMode && value.flowMode == ExperimentFlowMode.Pilot
            && value.runQualification == ExperimentRunQualification.Rehearsal && value.conditions.All(x => x.status == PilotRunStatus.Completed);

        private static void CopyRaw(string raw, string source)
        {
            if (!Directory.Exists(raw)) return;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(raw, "*.jsonl", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file); var lower = name.ToLowerInvariant();
                var folder = lower.Contains("questionnaire") ? "questionnaire" : lower.Contains("ranking") ? "ranking"
                    : lower.Contains("interview") ? "interview" : lower.Contains("study") || lower.Contains("pilot") || lower.Contains("operator") ? "study" : "timing";
                var targetName = name; var suffix = 1;
                while (!used.Add(folder + "/" + targetName)) targetName = Path.GetFileNameWithoutExtension(name) + "-" + suffix++ + ".jsonl";
                File.Copy(file, Path.Combine(source, folder, targetName), true);
            }
        }
    }
}
