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
        private SerializedProperty assistantAgentFallbackVoiceId;
        private SerializedProperty debugForceFeedback;
        private SerializedProperty debugFeedbackText;

        private void OnEnable()
        {
            script = serializedObject.FindProperty("m_Script");
            playCorrectionFeedback = serializedObject.FindProperty("playCorrectionFeedback");
            correctionAgentPresenter = serializedObject.FindProperty("correctionAgentPresenter");
            createCorrectionAgentIfMissing = serializedObject.FindProperty("createCorrectionAgentIfMissing");
            assistantAgentFallbackVoiceId = serializedObject.FindProperty("assistantAgentFallbackVoiceId");
            debugForceFeedback = serializedObject.FindProperty("debugForceFeedback");
            debugFeedbackText = serializedObject.FindProperty("debugFeedbackText");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(script);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Correction Feedback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playCorrectionFeedback);
            EditorGUILayout.PropertyField(correctionAgentPresenter);
            EditorGUILayout.PropertyField(createCorrectionAgentIfMissing);
            EditorGUILayout.PropertyField(assistantAgentFallbackVoiceId);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Correction Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(debugForceFeedback);

            if (debugForceFeedback.hasMultipleDifferentValues || debugForceFeedback.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(debugFeedbackText);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
