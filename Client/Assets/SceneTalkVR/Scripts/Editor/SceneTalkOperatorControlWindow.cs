using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    [InitializeOnLoad]
    public sealed class SceneTalkOperatorControlWindow : EditorWindow
    {
        private const string ParticipantKey = "SceneTalkVR.EditorCollection.ParticipantId";
        private const string SessionKey = "SceneTalkVR.EditorCollection.SessionId";
        private const string PendingKey = "SceneTalkVR.EditorCollection.Pending";
        private const string ResumeKey = "SceneTalkVR.EditorCollection.Resume";
        private string participantId;
        private string sessionId;
        private string operatorId = "experiment_operator";
        private string goalId;
        private string note;
        private string technicalReason;
        private string status;
        private bool showQaRecovery;

        static SceneTalkOperatorControlWindow()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("SceneTalkVR/Experiment/Operator Control")]
        public static void Open() => GetWindow<SceneTalkOperatorControlWindow>("Operator Control");

        private void OnEnable()
        {
            participantId = EditorPrefs.GetString(ParticipantKey, string.Empty);
            sessionId = EditorPrefs.GetString(SessionKey, string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Formal Editor Collection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Operator-only control. Arm a real participant session here, then use the participant UI in Game view. Demo/Rehearsal and QA recovery never qualify as collection data.", MessageType.Info);
            participantId = EditorGUILayout.TextField("Participant ID", participantId);
            sessionId = EditorGUILayout.TextField("Session ID", sessionId);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Arm New Session")) PrepareArm(false);
                if (GUILayout.Button("Resume Session")) PrepareArm(true);
            }
            if (!EditorApplication.isPlaying && EditorPrefs.GetBool(PendingKey, false))
                EditorGUILayout.HelpBox("Session is armed. Enter Play Mode; the official assets and stored identity will be bound automatically.", MessageType.Warning);

            var active = EditorApplication.isPlaying ? EditorCollectionSessionCoordinator.Active : null;
            if (active != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Armed", active.IsArmed.ToString());
                EditorGUILayout.LabelField("Participant / Session", active.ParticipantId + " / " + active.SessionId);
                EditorGUILayout.LabelField("Task / Run", active.CurrentTaskId + " / " + active.CurrentRunId);
                if (GUILayout.Button("Mark Current Condition Technical Invalid"))
                {
                    active.MarkTechnicalInvalid(string.IsNullOrWhiteSpace(technicalReason) ? "operator_marked_technical_invalid" : technicalReason.Trim());
                    status = "Condition marked TechnicalInvalid.";
                }
                technicalReason = EditorGUILayout.TextField("Technical reason", technicalReason);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Experimenter Goal Review", EditorStyles.boldLabel);
                operatorId = EditorGUILayout.TextField("Experimenter ID", operatorId);
                goalId = EditorGUILayout.TextField("Goal ID", goalId);
                note = EditorGUILayout.TextField("Evidence / reason", note);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Confirm")) RunGoalAction(active.ConfirmGoalByExperimenter);
                    if (GUILayout.Button("Reject")) RunGoalAction(active.RejectGoalByExperimenter);
                    if (GUILayout.Button("Undo")) RunGoalAction(active.UndoGoalByExperimenter);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Export Completed Bundle")) status = active.ExportBundle(out var error) ? active.LastBundlePath : error;
                    if (GUILayout.Button("Audit Last Bundle")) status = active.AuditBundle(out var error) ? "Bundle audit PASS" : error;
                }
                if (GUILayout.Button("Disarm Runtime Session")) { active.EndRuntimeSession(); status = "Runtime session disarmed."; }

                showQaRecovery = EditorGUILayout.Foldout(showQaRecovery, "QA / recovery tools (invalidates collection eligibility)", true);
                if (showQaRecovery && GUILayout.Button("Mark QA automation used"))
                {
                    active.MarkQaAutomationUsed("operator_control_manual_qa_recovery");
                    status = "QA use recorded; collectionEligible=false.";
                }
            }
            if (!string.IsNullOrWhiteSpace(status)) EditorGUILayout.HelpBox(status, MessageType.None);
            if (Event.current.type == EventType.Repaint) Repaint();
        }

        private delegate bool GoalAction(string goal, string actor, string reason, out string error);
        private void RunGoalAction(GoalAction action)
        {
            if (string.IsNullOrWhiteSpace(goalId) || string.IsNullOrWhiteSpace(operatorId)) { status = "Goal ID and Experimenter ID are required."; return; }
            status = action(goalId.Trim(), operatorId.Trim(), note?.Trim() ?? string.Empty, out var error) ? "Goal action recorded." : error;
        }

        private void PrepareArm(bool resume)
        {
            if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(sessionId)) { status = "Participant ID and Session ID are required."; return; }
            EditorPrefs.SetString(ParticipantKey, participantId.Trim());
            EditorPrefs.SetString(SessionKey, sessionId.Trim());
            EditorPrefs.SetBool(ResumeKey, resume);
            EditorPrefs.SetBool(PendingKey, true);
            status = EditorApplication.isPlaying ? ConfigureAndArm() : "Armed; enter Play Mode.";
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PendingKey, false))
                EditorApplication.delayCall += () => Debug.Log("[EditorCollection] " + ConfigureAndArm());
        }

        private static string ConfigureAndArm()
        {
            var coordinator = Object.FindFirstObjectByType<EditorCollectionSessionCoordinator>();
            if (coordinator == null) return "EditorCollectionSessionCoordinator missing from SampleScene runtime.";
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>(EditorCollectionAssetBuilder.ProtocolPath);
            var tasks = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(EditorCollectionAssetBuilder.TaskPath);
            var questionnaire = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>(EditorCollectionAssetBuilder.QuestionnairePath);
            var voices = AssetDatabase.LoadAssetAtPath<ExperimentVoiceProfileCatalog>(EditorCollectionAssetBuilder.VoicePath);
            var deployments = AssetDatabase.LoadAssetAtPath<ExperimentDeploymentCatalog>(EditorCollectionAssetBuilder.DeploymentPath);
            var resources = AssetDatabase.LoadAssetAtPath<EditorCollectionResourceCatalog>(EditorCollectionAssetBuilder.ResourcePath);
            coordinator.Configure(protocol, resources, voices, deployments, questionnaire, tasks);
            var ok = coordinator.ArmParticipantSession(EditorPrefs.GetString(ParticipantKey), EditorPrefs.GetString(SessionKey),
                EditorPrefs.GetBool(ResumeKey, false), out var error);
            if (ok) EditorPrefs.SetBool(PendingKey, false);
            return ok ? "Participant session armed for collection. Press Start in Game view." : "Arm failed: " + error;
        }
    }
}
