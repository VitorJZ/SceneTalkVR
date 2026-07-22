using System.IO;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public sealed class PilotExperimenterWindow : EditorWindow
    {
        private string participantId = "pilot-participant";
        private string sessionId = "pilot-session";
        private string assignmentPath = "";
        private string lastMessage = "Pilot Locked creation remains blocked until protocol decisions and Humanoid prefab are confirmed.";

        [MenuItem("SceneTalkVR/Experiment/Pilot Experimenter Control", false, 46)]
        public static void Open() => GetWindow<PilotExperimenterWindow>("Pilot Experimenter");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Experimenter-only Editor control. This window is not included in participant VR UI.", MessageType.Info);
            participantId = EditorGUILayout.TextField("Participant", participantId);
            sessionId = EditorGUILayout.TextField("Session", sessionId);
            assignmentPath = EditorGUILayout.TextField("Assignment JSON", assignmentPath);
            var workflow = PilotWorkflowCoordinator.Active ?? FindFirstObjectByType<PilotWorkflowCoordinator>();
            using (new EditorGUI.DisabledScope(workflow == null))
            {
                if (GUILayout.Button("Create Locked Assignment"))
                    lastMessage = workflow.CreateLocked(participantId, sessionId, out var error) ? "Assignment created." : error;
                if (GUILayout.Button("Load Assignment JSON"))
                {
                    var value = PilotAssignmentAllocator.Load(assignmentPath);
                    lastMessage = workflow.LoadAssignment(value, out var error) ? "Assignment loaded." : error;
                }
                if (GUILayout.Button("Save Current Assignment"))
                {
                    if (workflow.Assignment == null) lastMessage = "pilot_assignment_missing";
                    else { PilotAssignmentAllocator.Save(workflow.Assignment, assignmentPath); lastMessage = "Assignment saved."; }
                }
                if (GUILayout.Button("Prepare Next Condition")) lastMessage = workflow.PrepareNext(out var error) ? "Condition prepared and running." : error;
                if (GUILayout.Button("Complete Task")) { workflow.CompleteTask(); lastMessage = "Task marked complete."; }
                if (GUILayout.Button("Begin Pilot Questionnaire")) lastMessage = workflow.BeginQuestionnaire(out var error) ? "Questionnaire started." : error;
                if (GUILayout.Button("Mark Technical Invalid")) { workflow.MarkTechnicalInvalid("Experimenter", "experimenter_marked_invalid"); lastMessage = "TechnicalInvalid recorded."; }
                if (GUILayout.Button("Retry Current Condition")) lastMessage = workflow.RetryCurrent(out var error) ? "Retry prepared with a new pilotRunId." : error;
            }

            EditorGUILayout.Space(); EditorGUILayout.LabelField("Current", EditorStyles.boldLabel);
            if (workflow?.Assignment == null) EditorGUILayout.LabelField("No assignment loaded");
            else
            {
                var a = workflow.Assignment; var c = workflow.Current;
                EditorGUILayout.LabelField("Participant", a.participantId);
                EditorGUILayout.LabelField("Pilot sequence", a.sequenceId);
                EditorGUILayout.LabelField("Current position", c == null ? "not prepared" : (c.conditionPosition + 1).ToString());
                EditorGUILayout.LabelField("Embodiment", c == null ? "" : PilotProtocolValues.Label(c.embodimentCondition));
                EditorGUILayout.LabelField("Task", c?.task?.taskId ?? "");
                EditorGUILayout.LabelField("Feedback style", PilotProtocolValues.Label(a.feedbackStyle));
                EditorGUILayout.LabelField("Audio policy", PilotProtocolValues.Label(a.voiceOnlyAudioPolicy));
                EditorGUILayout.LabelField("Pilot run", workflow.PilotRunId ?? "");
            }
            EditorGUILayout.HelpBox(lastMessage, MessageType.None);
        }
    }
}
