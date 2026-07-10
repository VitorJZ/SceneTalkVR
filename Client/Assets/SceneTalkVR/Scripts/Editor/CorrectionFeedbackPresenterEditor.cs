using SceneTalkVR.AvatarSystem;
using UnityEditor;

namespace SceneTalkVR.EditorTools
{
    [CustomEditor(typeof(CorrectionFeedbackPresenter))]
    [CanEditMultipleObjects]
    public sealed class CorrectionFeedbackPresenterEditor : Editor
    {
        private SerializedProperty script;
        private SerializedProperty playCorrectionFeedback;
        private SerializedProperty correctionAgentPresenter;
        private SerializedProperty createCorrectionAgentIfMissing;
        private SerializedProperty assistantAgentVoiceType;
        private SerializedProperty debugForceFeedback;
        private SerializedProperty debugFeedbackText;

        private void OnEnable()
        {
            script = serializedObject.FindProperty("m_Script");
            playCorrectionFeedback = serializedObject.FindProperty("playCorrectionFeedback");
            correctionAgentPresenter = serializedObject.FindProperty("correctionAgentPresenter");
            createCorrectionAgentIfMissing = serializedObject.FindProperty("createCorrectionAgentIfMissing");
            assistantAgentVoiceType = serializedObject.FindProperty("assistantAgentVoiceType");
            debugForceFeedback = serializedObject.FindProperty("debugForceFeedback");
            debugFeedbackText = serializedObject.FindProperty("debugFeedbackText");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                DrawProperty(script, "m_Script");
            }

            EditorGUILayout.Space();
            DrawProperty(playCorrectionFeedback, "playCorrectionFeedback");
            DrawProperty(correctionAgentPresenter, "correctionAgentPresenter");
            DrawProperty(createCorrectionAgentIfMissing, "createCorrectionAgentIfMissing");
            DrawProperty(assistantAgentVoiceType, "assistantAgentVoiceType");

            EditorGUILayout.Space();
            DrawProperty(debugForceFeedback, "debugForceFeedback");

            if (debugForceFeedback != null
                && (debugForceFeedback.hasMultipleDifferentValues || debugForceFeedback.boolValue))
            {
                EditorGUI.indentLevel++;
                DrawProperty(debugFeedbackText, "debugFeedbackText");
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawProperty(SerializedProperty property, string propertyName)
        {
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Missing serialized property '{propertyName}'.",
                MessageType.Error);
        }
    }
}
