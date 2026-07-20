using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class SceneTalkTeamShowcaseWindow : EditorWindow
    {
        private const string PendingKey = "SceneTalkVR.EditorDemo.Pending";
        private string participantSuffix = "001";
        private string interviewNote = "Editor demonstration interview note.";
        private string message = "Create/update Demo assets, then run Preflight.";
        private Vector2 scroll;

        [MenuItem("SceneTalkVR/Demo/Team Showcase Control")]
        public static void Open() => GetWindow<SceneTalkTeamShowcaseWindow>("Team Showcase Control");

        private void OnEnable() { EditorApplication.playModeStateChanged += OnPlayMode; }
        private void OnDisable() { EditorApplication.playModeStateChanged -= OnPlayMode; }
        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.HasKey(PendingKey)) return;
            var action = EditorPrefs.GetString(PendingKey); EditorPrefs.DeleteKey(PendingKey);
            EditorApplication.delayCall += () => StartOrResume(action);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("EDITOR DEMONSTRATION — NOT PARTICIPANT DATA\nDemoApproved is not formal research approval.", MessageType.Warning);
            participantSuffix = EditorGUILayout.TextField("Participant suffix", participantSuffix);
            var demo = EditorDemoSessionCoordinator.Active;
            EditorGUILayout.LabelField("Current runtime mode", demo?.RuntimeMode.ToString() ?? "Not running");
            EditorGUILayout.LabelField("Demo protocol", demo?.DemoProtocol?.DemoProtocolVersion ?? "1.1-editor-demo-v1");
            EditorGUILayout.LabelField("Participant / Session", demo == null ? "-" : demo.ParticipantId + " / " + demo.SessionId);
            EditorGUILayout.LabelField("Condition position", demo == null ? "-" : $"{demo.CurrentPosition + 1}/{demo.TotalConditions}");
            EditorGUILayout.LabelField("Task / Run", demo == null ? "-" : demo.CurrentTaskId + " / " + demo.CurrentRunId);
            EditorGUILayout.LabelField("Voice profile", "editor_demo_feedback_voice (collection approved: No)");
            EditorGUILayout.LabelField("Deployment", "EditorDemo @ 127.0.0.1 (collection allowed: No)");
            EditorGUILayout.LabelField("Collection eligible", "No");
            EditorGUILayout.Space();
            if (GUILayout.Button("Create / Update Demo Assets")) { EditorDemoAssetBuilder.CreateOrUpdate(); message = "Demo assets updated."; }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Formal Demo Preflight")) message = Summarize(EditorDemoPreflight.Run(true));
            if (GUILayout.Button("Pilot Demo Preflight")) message = Summarize(EditorDemoPreflight.Run(false));
            EditorGUILayout.EndHorizontal();
            Button("Start Formal Demo", "start_formal"); Button("Resume Formal Demo", "resume_formal");
            Button("Start Pilot Demo", "start_pilot"); Button("Resume Pilot Demo", "resume_pilot");
            EditorGUILayout.Space();
            RuntimeButton("Prepare Current Condition", d => d.PrepareNextCondition(out message));
            RuntimeButton("Complete Current Goals", d => Confirm("Complete all current Goals as demo_operator?") && d.CompleteCurrentGoals(out message));
            RuntimeButton("Complete Current Task", d => d.CompleteCurrentTask(out message));
            RuntimeButton("Open Questionnaire", d => d.OpenQuestionnaire(out message));
            RuntimeButton("Demo Auto-Fill Questionnaire", d => Confirm("Auto-fill this Demo questionnaire? It is never participant data.") && d.AutoFillQuestionnaire(out message));
            RuntimeButton("Submit Questionnaire", d => d.SubmitQuestionnaire(out message));
            RuntimeButton("Prepare Next Condition", d => d.PrepareNextCondition(out message));
            RuntimeButton("Mark TechnicalInvalid", d => { d.MarkTechnicalInvalid("editor_demo_operator_injected"); return true; });
            RuntimeButton("Retry", d => d.Retry(out message));
            RuntimeButton("Open Final Ranking", d => d.OpenFinalRanking(out message));
            RuntimeButton("Demo Auto-Fill Ranking", d => Confirm("Auto-fill final Demo ranking?") && d.AutoFillFinalRanking(out message));
            RuntimeButton("Show Pilot Feedback Visual", d => d.ShowPilotFeedbackVisual(out message));
            RuntimeButton("Hide Pilot Feedback Visual", d => d.HidePilotFeedbackVisual(out message));
            interviewNote = EditorGUILayout.TextField("Interview note", interviewNote);
            RuntimeButton("Save Demo Interview Note", d => d.SaveDemoInterviewNote(interviewNote, out message));
            RuntimeButton("Export Session Bundle", d => d.ExportSessionBundle(out message));
            RuntimeButton("Run Integrity Audit", d => d.AuditLastBundle(out message));
            RuntimeButton("Reset Demo Session", d => { if (!Confirm("Reset and clear the current Demo session state?")) return false; d.ResetDemoSession(); message = "Demo session reset."; return true; });
            RuntimeButton("Return to Main Menu", d => { d.ResetDemoSession(); message = "Demo state cleared; use the existing Main Menu."; return true; });
            EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
            if (EditorApplication.isPlaying) Repaint();
        }

        private void Button(string label, string action)
        {
            if (!GUILayout.Button(label)) return;
            if (!EditorApplication.isPlaying) { EditorPrefs.SetString(PendingKey, action); EditorApplication.isPlaying = true; message = "Entering Play Mode…"; }
            else StartOrResume(action);
        }
        private void StartOrResume(string action)
        {
            var demo = EnsureCoordinator();
            if (demo == null) { message = "Scene bindings unavailable; open SampleScene."; return; }
            bool ok;
            if (action == "start_formal") ok = demo.StartFormalDemo(participantSuffix, out message);
            else if (action == "start_pilot") ok = demo.StartPilotDemo(participantSuffix, out message);
            else ok = demo.ResumeLatest(action == "resume_formal", out message);
            if (ok) message = action + " ready. Click Prepare Current Condition.";
        }
        public static EditorDemoSessionCoordinator EnsureCoordinator()
        {
            var existing = Object.FindFirstObjectByType<EditorDemoSessionCoordinator>();
            if (existing != null) return existing;
            var manager = Object.FindFirstObjectByType<ExperimentConditionManager>();
            if (manager == null) return null;
            var demo = manager.gameObject.AddComponent<EditorDemoSessionCoordinator>();
            demo.Configure(AssetDatabase.LoadAssetAtPath<ExperimentV11EditorDemoProtocol>(EditorDemoAssetBuilder.ProtocolPath),
                AssetDatabase.LoadAssetAtPath<EditorDemoAvatarMappingCatalog>(EditorDemoAssetBuilder.MappingPath),
                AssetDatabase.LoadAssetAtPath<ExperimentVoiceProfileCatalog>(EditorDemoAssetBuilder.VoicePath),
                AssetDatabase.LoadAssetAtPath<ExperimentDeploymentCatalog>(EditorDemoAssetBuilder.DeploymentPath));
            return demo;
        }
        private void RuntimeButton(string label, System.Func<EditorDemoSessionCoordinator, bool> run)
        {
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || EditorDemoSessionCoordinator.Active == null))
                if (GUILayout.Button(label)) { var ok = run(EditorDemoSessionCoordinator.Active); if (ok && string.IsNullOrWhiteSpace(message)) message = label + " completed."; }
        }
        private static bool Confirm(string text) => EditorUtility.DisplayDialog("Editor Demo only", text, "Continue", "Cancel");
        private static string Summarize(EditorDemoPreflightResult r) => r.status + $" — checks={r.checks.Length}, warnings={r.warnings.Length}, blockers={r.blockers.Length}" + (r.blockers.Length > 0 ? "\n" + string.Join("\n", r.blockers) : "");

        [MenuItem("SceneTalkVR/Demo/Validation/Start Formal Demo In Play Mode")]
        public static void ValidationStartFormal()
        {
            var demo = EnsureCoordinator(); var error = demo == null ? "scene bindings missing" : string.Empty; if (demo == null || !demo.StartFormalDemo("VALIDATION", out error)) Debug.LogError("[Editor Demo Validation] " + error); else Debug.Log("[Editor Demo Validation] Formal started; collectionEligible=false.");
        }
        [MenuItem("SceneTalkVR/Demo/Validation/Start Pilot Demo In Play Mode")]
        public static void ValidationStartPilot()
        {
            var demo = EnsureCoordinator(); var error = demo == null ? "scene bindings missing" : string.Empty; if (demo == null || !demo.StartPilotDemo("VALIDATION", out error)) Debug.LogError("[Editor Demo Validation] " + error); else Debug.Log("[Editor Demo Validation] Pilot started; collectionEligible=false.");
        }
        [MenuItem("SceneTalkVR/Demo/Validation/Prepare Next Condition")]
        public static void ValidationPrepareNext() { var demo = EnsureCoordinator(); var error = demo == null ? "scene bindings missing" : string.Empty; if (demo == null || !demo.PrepareNextCondition(out error)) Debug.LogError("[Editor Demo Validation] " + error); }
        [MenuItem("SceneTalkVR/Demo/Validation/Complete Current Goals")]
        public static void ValidationCompleteGoals() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.CompleteCurrentGoals(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Complete Current Task")]
        public static void ValidationCompleteTask() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.CompleteCurrentTask(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Open Questionnaire")]
        public static void ValidationOpenQuestionnaire() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.OpenQuestionnaire(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Auto Fill Questionnaire")]
        public static void ValidationFillQuestionnaire() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.AutoFillQuestionnaire(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Submit Questionnaire")]
        public static void ValidationSubmitQuestionnaire() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.SubmitQuestionnaire(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Auto Fill Final Ranking")]
        public static void ValidationFillRanking() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.AutoFillFinalRanking(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Open Final Ranking")]
        public static void ValidationOpenRanking() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.OpenFinalRanking(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Show Pilot Feedback Visual")]
        public static void ValidationShowPilotVisual() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.ShowPilotFeedbackVisual(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Hide Pilot Feedback Visual")]
        public static void ValidationHidePilotVisual() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.HidePilotFeedbackVisual(out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Save Interview Note")]
        public static void ValidationSaveInterview() => RunValidation((EditorDemoSessionCoordinator d, out string e) => d.SaveDemoInterviewNote("Automated Editor Demo validation note.", out e));
        [MenuItem("SceneTalkVR/Demo/Validation/Export And Audit Bundle")]
        public static void ValidationExportAudit()
        {
            var demo = EnsureCoordinator(); if (demo == null) { Debug.LogError("[Editor Demo Validation] scene bindings missing"); return; }
            if (!demo.ExportSessionBundle(out var error) || !demo.AuditLastBundle(out error)) Debug.LogError("[Editor Demo Validation] " + error); else Debug.Log("[Editor Demo Validation] Bundle integrity PASS: " + demo.LastBundlePath);
        }
        private delegate bool ValidationAction(EditorDemoSessionCoordinator demo, out string error);
        private static void RunValidation(ValidationAction action)
        {
            var demo = EnsureCoordinator(); var error = demo == null ? "scene bindings missing" : string.Empty;
            if (demo == null || !action(demo, out error)) Debug.LogError("[Editor Demo Validation] " + error);
        }
    }
}
