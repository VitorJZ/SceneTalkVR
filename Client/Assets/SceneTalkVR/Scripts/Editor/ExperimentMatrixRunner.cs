using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class ExperimentMatrixRunnerWindow : EditorWindow
    {
        private ExperimentMatrixExecutionMode mode = ExperimentMatrixExecutionMode.Synthetic;
        private string status = "Not run";
        private Vector2 scroll;

        [MenuItem("SceneTalkVR/Experiment/Matrix Runner", false, 91)]
        private static void Open() => GetWindow<ExperimentMatrixRunnerWindow>("Matrix Runner");

        [MenuItem("SceneTalkVR/Experiment/Run Formal Matrix", false, 92)]
        public static void RunFormalSynthetic() => RunAndWrite(ExperimentMatrixType.Formal, ExperimentMatrixExecutionMode.Synthetic);

        [MenuItem("SceneTalkVR/Experiment/Run Pilot Matrix", false, 93)]
        public static void RunPilotSynthetic() => RunAndWrite(ExperimentMatrixType.Pilot, ExperimentMatrixExecutionMode.Synthetic);

        [MenuItem("SceneTalkVR/Experiment/Validate Locked Formal Matrix", false, 94)]
        public static void ValidateLockedFormal() => RunAndWrite(ExperimentMatrixType.Formal, ExperimentMatrixExecutionMode.LockedCollection);

        [MenuItem("SceneTalkVR/Experiment/Validate Locked Pilot Matrix", false, 95)]
        public static void ValidateLockedPilot() => RunAndWrite(ExperimentMatrixType.Pilot, ExperimentMatrixExecutionMode.LockedCollection);

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Matrix Runner is Editor-only. Synthetic and Developer Placeholder records are never participant data.", MessageType.Warning);
            mode = (ExperimentMatrixExecutionMode)EditorGUILayout.EnumPopup("Execution mode", mode);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Formal 16")) status = Describe(RunAndWrite(ExperimentMatrixType.Formal, mode));
                if (GUILayout.Button("Run Pilot 9")) status = Describe(RunAndWrite(ExperimentMatrixType.Pilot, mode));
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static ExperimentMatrixRunManifest RunAndWrite(ExperimentMatrixType type, ExperimentMatrixExecutionMode mode)
        {
            var client = Directory.GetParent(Application.dataPath).FullName;
            var evidenceRoot = Path.Combine(client, "Library", "SceneTalkVR", "Stage9Evidence");
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
            var tasks = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
            var questionnaires = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset");
            var presentations = AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>("Assets/SceneTalkVR/ExperimentProtocol/PilotPresentationCatalog.asset");
            var definition = type == ExperimentMatrixType.Formal ? ExperimentMatrixDefinition.Formal() : ExperimentMatrixDefinition.Pilot();
            var run = ExperimentMatrixRunnerService.Run(definition, mode, evidenceRoot, GitCommit(client), protocol, tasks, questionnaires?.CatalogVersion ?? string.Empty, presentations);
            var runRoot = Path.Combine(evidenceRoot, run.matrixRunId);
            var typeRoot = Path.Combine(runRoot, type.ToString().ToLowerInvariant());
            Directory.CreateDirectory(typeRoot);
            File.WriteAllText(Path.Combine(typeRoot, "matrix-results.json"), JsonUtility.ToJson(run, true), Encoding.UTF8);
            File.WriteAllText(Path.Combine(typeRoot, "matrix-results.csv"), ExperimentMatrixRunnerService.ToCsv(run), Encoding.UTF8);
            File.WriteAllText(Path.Combine(runRoot, "manifest.json"), JsonUtility.ToJson(run, true), Encoding.UTF8);
            if (mode == ExperimentMatrixExecutionMode.Synthetic)
            {
                var prefix = type == ExperimentMatrixType.Formal ? "FORMAL" : "PILOT";
                File.WriteAllText(Path.Combine(client, $"EXPERIMENT_V1_1_STAGE9_{prefix}_MATRIX_RESULTS.json"), JsonUtility.ToJson(run, true), Encoding.UTF8);
                File.WriteAllText(Path.Combine(client, $"EXPERIMENT_V1_1_STAGE9_{prefix}_MATRIX_RESULTS.csv"), ExperimentMatrixRunnerService.ToCsv(run), Encoding.UTF8);
            }
            else if (mode == ExperimentMatrixExecutionMode.LockedCollection)
            {
                File.WriteAllText(Path.Combine(client, $"EXPERIMENT_V1_1_STAGE9_LOCKED_{type.ToString().ToUpperInvariant()}_MATRIX.json"), JsonUtility.ToJson(run, true), Encoding.UTF8);
            }
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"Stage 9 {type} {mode}: PASS={run.passed}, FAIL={run.failed}, BLOCKED={run.blocked}, root={runRoot}");
            return run;
        }

        private static string Describe(ExperimentMatrixRunManifest run) => JsonUtility.ToJson(run, true);

        private static string GitCommit(string client)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
                {
                    WorkingDirectory = Directory.GetParent(client).FullName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                return process == null ? "unknown" : process.StandardOutput.ReadToEnd().Trim();
            }
            catch { return "unknown"; }
        }
    }
}
