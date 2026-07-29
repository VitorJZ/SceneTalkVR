using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public static class EditorCollectionBundleExporter
    {
        public const string BundleSchemaVersion = "1.2-editor-collection";

        public static bool Export(string root, ExperimentAssignment assignment,
            ExperimentV11ProtocolConfig protocol, EditorCollectionResourceCatalog resources,
            bool rankingSubmitted, out string bundle, out string error)
        {
            bundle = string.Empty;
            error = string.Empty;
            if (assignment == null || protocol == null || resources == null)
            { error = "collection_bundle_context_missing"; return false; }
            if (!rankingSubmitted || assignment.status != AssignmentStatus.Completed
                || assignment.conditions == null || assignment.conditions.Any(x => x.status != ConditionRunStatus.Completed))
            { error = "collection_bundle_session_incomplete"; return false; }
            if (assignment.dataOrigin != "participant_collection" || !assignment.collectionEligible
                || assignment.developerTestAssignment || assignment.demoMode || assignment.synthetic
                || assignment.runQualification != ExperimentRunQualification.Collection)
            { error = "collection_bundle_identity_invalid"; return false; }

            var raw = Path.Combine(root, "raw");
            var source = Path.Combine(root, "bundle-source");
            bundle = Path.Combine(root, "bundle");
            if (Directory.Exists(source)) Directory.Delete(source, true);
            if (Directory.Exists(bundle)) Directory.Delete(bundle, true);
            foreach (var folder in new[] { "assignment", "timing", "study", "questionnaire", "ranking", "interview", "integrity" })
                Directory.CreateDirectory(Path.Combine(source, folder));
            File.WriteAllText(Path.Combine(source, "assignment", "assignment.json"), JsonUtility.ToJson(assignment, true), Encoding.UTF8);
            CopyRaw(raw, source);
            var buildInfo = UnityEngine.Object.FindFirstObjectByType<ExperimentConditionManager>()?.ExperimentBuildInfo;
            var manifest = new SessionBundleManifest
            {
                bundleSchemaVersion = BundleSchemaVersion, dataOrigin = "participant_collection",
                collectionEligible = true, developerTestAssignment = false, demoMode = false,
                synthetic = false, qaAutomationUsed = assignment.qaAutomationUsed,
                runtimeMode = assignment.runtimeMode.ToString(),
                flowMode = ExperimentFlowMode.Formal.ToString(),
                runQualification = ExperimentRunQualification.Collection.ToString(), sessionMode = "formal",
                participantId = assignment.participantId, sessionId = assignment.experimentSessionId,
                gitCommit = buildInfo?.GitCommit ?? string.Empty,
                protocolVersion = protocol.ProtocolVersion, officialProtocolVersion = protocol.ProtocolVersion,
                protocolSnapshotId = protocol.ProtocolSnapshotId, resourceSnapshotId = resources.ResourceSnapshotId,
                taskCatalogVersion = assignment.taskCatalogVersion, questionnaireCatalogVersion = "1.2-editor-collection",
                assignmentVersion = assignment.assignmentVersion,
                formalConditionOrderPolicy = assignment.formalConditionOrderPolicy,
                taskAssignmentPolicy = assignment.taskAssignmentPolicy,
                goalConfirmationPolicy = assignment.goalConfirmationPolicy,
                questionnaireReturnPolicy = assignment.questionnaireReturnPolicy,
                assignmentAlgorithmVersion = assignment.assignmentAlgorithmVersion,
                randomSeedHash = assignment.randomSeedHash,
                deploymentProfile = assignment.deploymentProfile, primaryAttemptPolicy = protocol.PrimaryAttemptPolicy,
                assistantEmbodimentSnapshot = assignment.assistantEmbodimentSnapshot,
                conditionToTaskMapping = assignment.conditions.Select(x => x.formalConditionCode + "=" + x.task.taskId).ToArray(),
                conditionSelectionOrder = assignment.participantSelectionOrder.Select(x => x.ToString()).ToArray(),
                conditionRunIds = assignment.conditions.Select(x => x.latestConditionRunId ?? string.Empty).ToArray(),
                createdAtUtc = DateTime.UtcNow.ToString("o")
            };
            if (!SessionBundleExporter.Export(source, bundle, manifest, out error)) return false;
            var audit = SessionDataIntegrityAuditor.Audit(bundle, assignment.participantId, assignment.experimentSessionId);
            manifest.integrityStatus = audit.result.ToString().ToUpperInvariant();
            SessionBundleExporter.UpdateIntegrity(bundle, manifest, audit);
            return audit.result != DataIntegritySeverity.Fail;
        }

        private static void CopyRaw(string raw, string source)
        {
            if (!Directory.Exists(raw)) return;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(raw, "*.jsonl", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                var lower = name.ToLowerInvariant();
                var folder = lower.Contains("questionnaire") ? "questionnaire" : lower.Contains("ranking") ? "ranking"
                    : lower.Contains("interview") ? "interview" : lower.Contains("study") || lower.Contains("operator") ? "study" : "timing";
                var targetName = name;
                var suffix = 1;
                while (!used.Add(folder + "/" + targetName)) targetName = Path.GetFileNameWithoutExtension(name) + "-" + suffix++ + ".jsonl";
                File.Copy(file, Path.Combine(source, folder, targetName), true);
            }
            EnsureNonEmpty(Path.Combine(source, "timing"), "timing-empty.jsonl", "{\"eventType\":\"NoTimingEvents\"}");
            EnsureNonEmpty(Path.Combine(source, "questionnaire"), "questionnaire-empty.jsonl", "{\"eventType\":\"NoQuestionnaireEvents\"}");
            EnsureNonEmpty(Path.Combine(source, "ranking"), "ranking-empty.jsonl", "{\"eventType\":\"NoRankingEvents\"}");
        }

        private static void EnsureNonEmpty(string folder, string name, string line)
        {
            if (Directory.GetFiles(folder).Length == 0) File.WriteAllText(Path.Combine(folder, name), line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
