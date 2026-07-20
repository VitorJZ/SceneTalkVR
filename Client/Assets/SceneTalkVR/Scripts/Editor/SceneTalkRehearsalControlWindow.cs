using System;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class SceneTalkRehearsalControlWindow : EditorWindow
    {
        private const string PendingKey = "SceneTalkVR.Rehearsal.PendingAction";
        private ExperimentFlowMode flow = ExperimentFlowMode.Formal;
        private string participantId = "REHEARSAL-001";
        private string sessionId = "SESSION-001";
        private string interviewNote = string.Empty;
        private string message = string.Empty;
        private bool advancedQa;
        private Vector2 scroll;

        [MenuItem("SceneTalkVR/Experiment/Rehearsal Control")]
        public static void Open() => GetWindow<SceneTalkRehearsalControlWindow>("Rehearsal Control");

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.HasKey(PendingKey)) return;
            var action = EditorPrefs.GetString(PendingKey); EditorPrefs.DeleteKey(PendingKey); StartOrLoad(action == "load");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("SceneTalkVR Experiment v1.1", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Collection-equivalent operational rehearsal. Data are isolated and never collection eligible.", MessageType.Info);
            flow = (ExperimentFlowMode)EditorGUILayout.EnumPopup("Flow", flow == ExperimentFlowMode.Pilot ? ExperimentFlowMode.Pilot : ExperimentFlowMode.Formal);
            participantId = EditorGUILayout.TextField("Participant ID", participantId);
            sessionId = EditorGUILayout.TextField("Session ID", sessionId);
            var coordinator = RehearsalSessionCoordinator.Active;
            EditorGUILayout.LabelField("Qualification", "Rehearsal");
            EditorGUILayout.LabelField("Protocol version", coordinator?.Protocol?.ProtocolVersion ?? "1.1-rehearsal-2");
            EditorGUILayout.LabelField("Voice profile", "rehearsal_feedback_voice / rehearsal_dialogue_voice");
            EditorGUILayout.LabelField("Deployment", "rehearsal_editor (127.0.0.1:8787)");
            if (GUILayout.Button("Create Session")) StartOrEnterPlay("create");
            if (GUILayout.Button("Load Session")) StartOrEnterPlay("load");
            EditorGUILayout.Space();
            DrawState(coordinator);
            RuntimeButton("Prepare Current Condition", x => x.PrepareCurrentCondition(out message));
            RuntimeButton("Complete Task", x => x.CompleteTask(out message));
            RuntimeButton("Mark TechnicalInvalid", x => { x.MarkTechnicalInvalid("rehearsal_operator_marked"); return true; });
            RuntimeButton("Retry", x => x.Retry(out message));
            RuntimeButton("Open Questionnaire", x => x.OpenQuestionnaire(out message));
            RuntimeButton("Complete Questionnaire Boundary", x => x.SubmitQuestionnaire(out message));
            RuntimeButton("Prepare Next Condition", x => x.PrepareCurrentCondition(out message));
            RuntimeButton("Open Final Ranking", x => x.OpenFinalRanking(out message));
            interviewNote = EditorGUILayout.TextField("Interview note", interviewNote);
            RuntimeButton("Save Interview", x => x.SaveInterview(interviewNote, out message));
            RuntimeButton("Export Bundle", x => x.ExportBundle(out message));
            RuntimeButton("Run Integrity Audit", x => x.AuditBundle(out message));
            RuntimeButton("End Session", x => { x.ResetSession(); message = "Session ended."; return true; });
            advancedQa = EditorGUILayout.Foldout(advancedQa, "Advanced QA Tools", true);
            if (advancedQa)
            {
                EditorGUILayout.HelpBox("Every action is logged with actor=rehearsal_qa_operator and qaAutomationUsed=true.", MessageType.Warning);
                RuntimeButton("Auto-complete Goals", x => x.CompleteCurrentGoalsForQa(out message));
                RuntimeButton("Auto-fill Questionnaire", x => x.AutoFillQuestionnaireForQa(out message));
                RuntimeButton("Auto-fill Ranking", x => x.AutoFillRankingForQa(out message));
            }
            EditorGUILayout.HelpBox(message, MessageType.None); EditorGUILayout.EndScrollView();
            if (EditorApplication.isPlaying) Repaint();
        }

        private void DrawState(RehearsalSessionCoordinator x)
        {
            EditorGUILayout.LabelField("Runtime status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Flow mode", x?.RuntimeContext?.flowMode.ToString() ?? "not assigned");
            EditorGUILayout.LabelField("Participant / Session", x == null ? "-" : x.ParticipantId + " / " + x.SessionId);
            EditorGUILayout.LabelField("Sequence", x?.IsFormal == true ? x.FormalAssignment?.sequenceId ?? "-" : x?.PilotAssignment?.sequenceId ?? "-");
            EditorGUILayout.LabelField("Condition position", x == null ? "-" : $"{Mathf.Max(0, x.CurrentPosition + 1)}/{x.TotalConditions}");
            EditorGUILayout.LabelField("Task", x?.CurrentTaskId ?? "-");
            EditorGUILayout.LabelField("Run ID", x?.CurrentRunId ?? "-");
            EditorGUILayout.LabelField("Data path", x?.CurrentDataFolder ?? RehearsalSessionCoordinator.RehearsalRoot);
            EditorGUILayout.LabelField("Bundle", x?.LastBundlePath ?? "not exported");
            if (x?.IsFormal == true && x.FormalAssignment?.conditions != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Participant-choice assignment", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Selection order", x.FormalAssignment.participantSelectionOrder == null
                    ? "-" : string.Join(" → ", x.FormalAssignment.participantSelectionOrder.Select(v => v.ToString())));
                foreach (var item in x.FormalAssignment.conditions)
                {
                    var evidence = x.FormalAssignment == null || x.CurrentPosition != item.conditionPosition
                        ? string.Empty : string.Join(", ", x.GetComponent<ExperimentLifecycleCoordinator>().GoalTracker.Goals
                            .Where(g => !string.IsNullOrWhiteSpace(g.candidateEvidence)).Select(g => g.goalId + ": " + g.candidateEvidence));
                    EditorGUILayout.LabelField($"{item.formalConditionCode} → {item.task.taskId}",
                        $"{item.status}; selected #{(item.participantSelectionPosition < 0 ? "-" : (item.participantSelectionPosition + 1).ToString())}");
                    if (!string.IsNullOrWhiteSpace(evidence)) EditorGUILayout.HelpBox(evidence, MessageType.None);
                }
            }
        }

        private void StartOrEnterPlay(string action)
        {
            if (!EditorApplication.isPlaying) { EditorPrefs.SetString(PendingKey, action); EditorApplication.isPlaying = true; message = "Entering Play Mode..."; }
            else StartOrLoad(action == "load");
        }
        private void StartOrLoad(bool load)
        {
            var coordinator = EnsureCoordinator(); if (coordinator == null) { message = "Open SampleScene; required bindings were not found."; return; }
            var ok = load ? coordinator.LoadSession(flow, participantId, sessionId, out message)
                : coordinator.CreateSession(flow, participantId, sessionId, out message);
            if (ok) message = (load ? "Loaded" : "Created") + " rehearsal session. Prepare the current condition when the participant is ready.";
        }
        public static RehearsalSessionCoordinator EnsureCoordinator()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<RehearsalSessionCoordinator>(); if (existing != null) return existing;
            var manager = UnityEngine.Object.FindFirstObjectByType<ExperimentConditionManager>(); if (manager == null) return null;
            var value = manager.gameObject.AddComponent<RehearsalSessionCoordinator>();
            value.Configure(AssetDatabase.LoadAssetAtPath<ExperimentV11RehearsalProtocol>(RehearsalAssetBuilder.ProtocolPath),
                AssetDatabase.LoadAssetAtPath<ExperimentV11RehearsalResourceCatalog>(RehearsalAssetBuilder.ResourcePath),
                AssetDatabase.LoadAssetAtPath<ExperimentVoiceProfileCatalog>(RehearsalAssetBuilder.VoicePath),
                AssetDatabase.LoadAssetAtPath<ExperimentDeploymentCatalog>(RehearsalAssetBuilder.DeploymentPath));
            return value;
        }
        private void RuntimeButton(string label, Func<RehearsalSessionCoordinator, bool> action)
        {
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || RehearsalSessionCoordinator.Active == null))
                if (GUILayout.Button(label)) { message = string.Empty; if (action(RehearsalSessionCoordinator.Active) && string.IsNullOrWhiteSpace(message)) message = label + " completed."; }
        }
    }
}
