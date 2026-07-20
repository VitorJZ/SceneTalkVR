using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class Stage9FinalShaEvidenceBuilder
    {
        [MenuItem("SceneTalkVR/Experiment/Generate Stage 9 Final SHA Evidence", false, 96)]
        public static void Generate()
        {
            var client = Directory.GetParent(Application.dataPath).FullName;
            var repository = Directory.GetParent(client).FullName;
            var commit = Git(repository, "rev-parse HEAD");
            var branch = Git(repository, "branch --show-current");
            var buildInfo = AssetDatabase.LoadAssetAtPath<ExperimentBuildInfo>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentBuildInfo.asset");
            if (buildInfo == null || buildInfo.GitCommit != commit || buildInfo.ActiveBranch != branch)
                throw new InvalidOperationException($"BuildInfo mismatch. Expected {commit}/{branch}, got {buildInfo?.GitCommit}/{buildInfo?.ActiveBranch}.");

            var sourceJson = Path.Combine(client, "EXPERIMENT_V1_1_STAGE8_PROJECT_TEST_RESULTS.json");
            var sourceXml = Path.Combine(client, "EXPERIMENT_V1_1_STAGE8_PROJECT_TEST_RESULTS.xml");
            if (!File.Exists(sourceJson) || !File.ReadAllText(sourceJson).Contains(commit) || !File.ReadAllText(sourceJson).Contains("\"testMode\": \"EditMode\"") || !File.ReadAllText(sourceJson).Contains("\"testMode\": \"PlayMode\""))
                throw new InvalidOperationException("Fresh project EditMode and PlayMode results bound to current commit are required.");
            File.Copy(sourceJson, Path.Combine(client, "EXPERIMENT_V1_1_STAGE9_FINAL_SHA_TEST_RESULTS.json"), true);
            File.Copy(sourceXml, Path.Combine(client, "EXPERIMENT_V1_1_STAGE9_FINAL_SHA_TEST_RESULTS.xml"), true);

            var root = Path.Combine(client, "Library", "SceneTalkVR", "Stage9Evidence", "final-sha", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var formal = SyntheticDryRunEngine.RunFormal(root, "stage9-final-sha", "formal", commit);
            var pilot = SyntheticDryRunEngine.RunPilot(root, "stage9-final-sha", "pilot", commit);
            if (!formal.success || !pilot.success) throw new InvalidOperationException($"Final SHA Synthetic failure: formal={formal.error}; pilot={pilot.error}");
            File.Copy(Path.Combine(formal.bundleDirectory, "manifest.json"), Path.Combine(client, "EXPERIMENT_V1_1_STAGE9_FINAL_SHA_SYNTHETIC_FORMAL_MANIFEST.json"), true);
            File.Copy(Path.Combine(pilot.bundleDirectory, "manifest.json"), Path.Combine(client, "EXPERIMENT_V1_1_STAGE9_FINAL_SHA_SYNTHETIC_PILOT_MANIFEST.json"), true);
            var formalAudit = SessionDataIntegrityAuditor.Audit(formal.bundleDirectory, "stage9-final-sha", "formal");
            var pilotAudit = SessionDataIntegrityAuditor.Audit(pilot.bundleDirectory, "stage9-final-sha", "pilot");
            var report = new StringBuilder()
                .AppendLine("# Stage 9 Final-SHA Synthetic Integrity")
                .AppendLine().AppendLine($"- Git commit: `{commit}`")
                .AppendLine($"- Active branch: `{branch}`")
                .AppendLine($"- BuildInfo timestamp: `{buildInfo.BuildTimestampUtc}`")
                .AppendLine($"- Formal bundle: `{formal.bundleDirectory}`")
                .AppendLine($"- Formal audit: `{formalAudit.result.ToString().ToUpperInvariant()}`")
                .AppendLine($"- Pilot bundle: `{pilot.bundleDirectory}`")
                .AppendLine($"- Pilot audit: `{pilotAudit.result.ToString().ToUpperInvariant()}`")
                .AppendLine().AppendLine("Synthetic evidence is collection-ineligible and is not a scientific result.");
            File.WriteAllText(Path.Combine(client, "EXPERIMENT_V1_1_STAGE9_FINAL_SHA_SYNTHETIC_INTEGRITY.md"), report.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"Stage 9 final-SHA regression evidence generated for {commit}: Formal={formalAudit.result}, Pilot={pilotAudit.result}");
        }

        private static string Git(string root, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            return process == null ? string.Empty : process.StandardOutput.ReadToEnd().Trim();
        }
    }
}
