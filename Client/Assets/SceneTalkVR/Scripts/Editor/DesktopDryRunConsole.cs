using System;
using System.IO;
using System.Linq;
using System.Text;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class DesktopDryRunConsole : EditorWindow
    {
        [MenuItem("SceneTalkVR/Experiment/Generate Stage 8 Synthetic Evidence", false, 90)]
        private static void GenerateStage8SyntheticEvidence()
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            var root = Path.Combine(project, "Library", "SceneTalkVR", "Stage8Evidence", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var commit = ReadGitCommit(project);
            var formal = SyntheticDryRunEngine.RunFormal(root, "synthetic-stage8", "formal-complete", commit);
            var pilot = SyntheticDryRunEngine.RunPilot(root, "synthetic-stage8", "pilot-complete", commit);
            if (!formal.success || !pilot.success)
                throw new InvalidOperationException($"Synthetic evidence failed. Formal={formal.error}; Pilot={pilot.error}");

            File.Copy(Path.Combine(formal.bundleDirectory, "manifest.json"), Path.Combine(project, "EXPERIMENT_V1_1_STAGE8_SYNTHETIC_FORMAL_MANIFEST.json"), true);
            File.Copy(Path.Combine(pilot.bundleDirectory, "manifest.json"), Path.Combine(project, "EXPERIMENT_V1_1_STAGE8_SYNTHETIC_PILOT_MANIFEST.json"), true);
            var formalAudit = SessionDataIntegrityAuditor.Audit(formal.bundleDirectory, "synthetic-stage8", "formal-complete");
            var pilotAudit = SessionDataIntegrityAuditor.Audit(pilot.bundleDirectory, "synthetic-stage8", "pilot-complete");
            var report = new StringBuilder()
                .AppendLine("# Experiment v1.1 Stage 8 — Synthetic Integrity Report").AppendLine()
                .AppendLine($"Generated: {DateTime.UtcNow:o}")
                .AppendLine($"Git commit at run: `{commit}`").AppendLine()
                .AppendLine($"- Formal bundle: `{formal.bundleDirectory}`")
                .AppendLine($"- Formal integrity: `{formalAudit.result.ToString().ToUpperInvariant()}`")
                .AppendLine($"- Pilot bundle: `{pilot.bundleDirectory}`")
                .AppendLine($"- Pilot integrity: `{pilotAudit.result.ToString().ToUpperInvariant()}`").AppendLine()
                .AppendLine("Both bundles have `dataOrigin = synthetic_dry_run`, `collectionEligible = false`, and developer-test Assignments. These are software rehearsal records, not participant data or Locked Mode acceptance.").AppendLine()
                .AppendLine("## Formal findings")
                .AppendLine(string.Join(Environment.NewLine, formalAudit.findings.Select(x => $"- {x.severity}: `{x.checkId}` — {x.message}"))).AppendLine()
                .AppendLine("## Pilot findings")
                .AppendLine(string.Join(Environment.NewLine, pilotAudit.findings.Select(x => $"- {x.severity}: `{x.checkId}` — {x.message}")));
            File.WriteAllText(Path.Combine(project, "EXPERIMENT_V1_1_STAGE8_SYNTHETIC_INTEGRITY_REPORT.md"), report.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"Stage 8 synthetic evidence generated. Formal/Pilot integrity: {formalAudit.result}/{pilotAudit.result}");
        }

        private static string ReadGitCommit(string project)
        {
            try
            {
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                {
                    WorkingDirectory = Directory.GetParent(project).FullName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                var value = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);
                return value;
            }
            catch { return "unknown"; }
        }
        private string participant="synthetic-operator";private string session="";private string root="";private string currentCondition="not prepared";private string currentTask="not assigned";private string currentRunId="none";private string goalProgress="0/0";private string questionnaireState="not started";private string technicalValidity="not evaluated";private SyntheticDryRunResult last;private SessionDataIntegrityReport audit;private Vector2 scroll;
        [MenuItem("SceneTalkVR/Experiment/Desktop Dry Run Console",false,85)]private static void Open()=>GetWindow<DesktopDryRunConsole>("Desktop Dry Run");
        private void OnEnable(){if(string.IsNullOrWhiteSpace(root))root=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Library","SceneTalkVR","DesktopDryRun");if(string.IsNullOrWhiteSpace(session))session="synthetic-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");}
        private void OnGUI()
        {
            EditorGUILayout.HelpBox(SyntheticDryRunEngine.Banner,MessageType.Error);EditorGUILayout.LabelField("Mode",last?.mode??"Not started");participant=EditorGUILayout.TextField("Participant",participant);session=EditorGUILayout.TextField("Session",session);root=EditorGUILayout.TextField("Synthetic data root",root);
            var protocol=AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");EditorGUILayout.LabelField("Formal protocol",protocol?.ProtocolVersion??"missing");EditorGUILayout.LabelField("Collection eligible","FALSE");EditorGUILayout.LabelField("Assignment","developerTestAssignment = true");EditorGUILayout.LabelField("Data path",last?.bundleDirectory??"not created");EditorGUILayout.LabelField("Integrity",audit?.result.ToString()??last?.integrityStatus??"not run");
            EditorGUILayout.LabelField("Current condition",currentCondition);EditorGUILayout.LabelField("Current task",currentTask);EditorGUILayout.LabelField("Current run ID",currentRunId);EditorGUILayout.LabelField("Goal progress",goalProgress);EditorGUILayout.LabelField("Questionnaire state",questionnaireState);EditorGUILayout.LabelField("Technical validity",technicalValidity);
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("Run Complete Synthetic Formal"))Run(true);if(GUILayout.Button("Run Complete Synthetic Pilot"))Run(false);}
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("Resume Existing Bundle"))Resume();if(GUILayout.Button("Run Integrity Audit"))Audit();}
            EditorGUILayout.LabelField("Operator rehearsal actions (synthetic log only)");var actions=new[]{"Prepare condition","Feedback turn","No-feedback turn","Goal candidate","Confirm goal","Reject goal","Complete task","Submit questionnaire","TechnicalInvalid + Retry","Submit ranking","Save interview","Export bundle"};
            for(var i=0;i<actions.Length;i+=3)using(new EditorGUILayout.HorizontalScope())for(var j=i;j<Math.Min(i+3,actions.Length);j++){var action=actions[j];if(GUILayout.Button(action))RecordAction(action);}
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.TextArea(last==null?SyntheticDryRunEngine.Banner:JsonUtility.ToJson(last,true),GUILayout.ExpandHeight(true));EditorGUILayout.EndScrollView();
        }
        private void Run(bool formal){last=formal?SyntheticDryRunEngine.RunFormal(root,participant,session,GitCommit()):SyntheticDryRunEngine.RunPilot(root,participant,session,GitCommit());if(last.success){audit=SessionDataIntegrityAuditor.Audit(last.bundleDirectory,participant,session);currentCondition=formal?"NE/NR/SE/SR completed":"voice/orb/humanoid-placeholder completed";currentTask=formal?"4 formal tasks completed":"3 pilot tasks completed";currentRunId="see immutable study log";goalProgress=formal?"candidate/confirm/reject verified":"not applicable";questionnaireState="submitted + ranked";technicalValidity="synthetic PASS (not collection eligible)";}Repaint();}
        private void Resume(){var candidate=Directory.Exists(Path.Combine(root,"SyntheticBundles"))?Directory.GetDirectories(Path.Combine(root,"SyntheticBundles")).OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault():null;if(candidate==null)return;var manifest=JsonUtility.FromJson<SessionBundleManifest>(File.ReadAllText(Path.Combine(candidate,"manifest.json")));participant=manifest.participantId;session=manifest.sessionId;last=new SyntheticDryRunResult{success=true,mode=manifest.sessionMode,participantId=participant,sessionId=session,bundleDirectory=candidate,integrityStatus=manifest.integrityStatus};currentRunId="resumed: see immutable study log";technicalValidity="synthetic resumed";Audit();}
        private void Audit(){if(last==null||!Directory.Exists(last.bundleDirectory))return;audit=SessionDataIntegrityAuditor.Audit(last.bundleDirectory,participant,session);}
        private void RecordAction(string action){var folder=Path.Combine(root,"OperatorConsoleLogs");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"operator-actions.jsonl"),JsonUtility.ToJson(new OperatorAction{timestampUtc=DateTime.UtcNow.ToString("o"),dataOrigin="synthetic_dry_run",collectionEligible=false,participantId=participant,sessionId=session,action=action})+Environment.NewLine,Encoding.UTF8);technicalValidity=action=="TechnicalInvalid + Retry"?"TechnicalInvalid; retry retained":"synthetic action: "+action;Repaint();}
        [Serializable]private sealed class OperatorAction{public string timestampUtc;public string dataOrigin;public bool collectionEligible;public string participantId;public string sessionId;public string action;}
        private static string GitCommit(){var info=AssetDatabase.LoadAssetAtPath<ExperimentBuildInfo>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentBuildInfo.asset");return info?.GitCommit??"editor-working-copy";}
    }

    public static class PanoramaCandidateValidator
    {
        [MenuItem("SceneTalkVR/Experiment/Validate Selected Panorama Candidate",false,86)]
        public static void ValidateSelected()
        {
            var texture=Selection.activeObject as Texture2D;if(texture==null){Debug.LogError("Select a candidate Texture2D first.");return;}var path=AssetDatabase.GetAssetPath(texture);var importer=AssetImporter.GetAtPath(path) as TextureImporter;var android=importer?.GetPlatformTextureSettings("Android");var estimated=(long)texture.width*texture.height*4;
            var report=new StringBuilder().AppendLine("# Panorama Candidate Report").AppendLine().AppendLine("Status: `CANDIDATE_NOT_APPROVED`").AppendLine($"- Asset: `{path}`").AppendLine($"- Dimensions: {texture.width} × {texture.height}").AppendLine($"- Exact 2:1: {texture.width==texture.height*2}").AppendLine($"- Seam risk: MANUAL_REVIEW_REQUIRED").AppendLine($"- Texture type: {importer?.textureType}").AppendLine($"- Max size: {importer?.maxTextureSize}").AppendLine($"- Mipmap: {importer?.mipmapEnabled}").AppendLine($"- Android format: {android?.format}").AppendLine($"- Estimated RGBA32 memory: {estimated/1048576f:F2} MiB").AppendLine("- Source/licence metadata: OPERATOR_INPUT_REQUIRED").AppendLine("- Approval: false").AppendLine().AppendLine("This report does not bind or modify the Formal Task Catalog.");
            var folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Library","SceneTalkVR","PanoramaCandidates");Directory.CreateDirectory(folder);var output=Path.Combine(folder,Path.GetFileNameWithoutExtension(path)+"_candidate_report.md");File.WriteAllText(output,report.ToString(),Encoding.UTF8);Debug.Log($"Panorama candidate report written: {output}");
        }
    }
}
