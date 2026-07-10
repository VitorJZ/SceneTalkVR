using System;
using SceneTalkVR.AvatarSystem;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    [CustomEditor(typeof(CorrectionFeedbackPresenter))]
    [CanEditMultipleObjects]
    public sealed class CorrectionFeedbackPresenterEditor : Editor
    {
        private static readonly GUIContent FeedbackProviderLabel = new GUIContent(
            "Feedback Provider",
            "Fallback provider used when the payload does not specify a supported provider.");

        private static readonly string[] FeedbackProviderLabels =
        {
            "Assistant Agent",
            "Dialogue Avatar"
        };

        private static readonly string[] FeedbackProviderValues =
        {
            "assistant_agent",
            "dialogue_avatar"
        };

        private SerializedProperty script;
        private SerializedProperty playCorrectionFeedback;
        private SerializedProperty correctionAgentPresenter;
        private SerializedProperty createCorrectionAgentIfMissing;
        private SerializedProperty feedbackProvider;
        private SerializedProperty assistantAgentFallbackVoiceId;
        private SerializedProperty debugEnableCorrectionOverrides;
        private SerializedProperty debugForceFeedback;
        private SerializedProperty debugProvider;
        private SerializedProperty debugStyle;
        private SerializedProperty debugFeedbackText;

        private void OnEnable()
        {
            script = serializedObject.FindProperty("m_Script");
            playCorrectionFeedback = serializedObject.FindProperty("playCorrectionFeedback");
            correctionAgentPresenter = serializedObject.FindProperty("correctionAgentPresenter");
            createCorrectionAgentIfMissing = serializedObject.FindProperty("createCorrectionAgentIfMissing");
            feedbackProvider = serializedObject.FindProperty("feedbackProvider");
            assistantAgentFallbackVoiceId = serializedObject.FindProperty("assistantAgentFallbackVoiceId");
            debugEnableCorrectionOverrides = serializedObject.FindProperty("debugEnableCorrectionOverrides");
            debugForceFeedback = serializedObject.FindProperty("debugForceFeedback");
            debugProvider = serializedObject.FindProperty("debugProvider");
            debugStyle = serializedObject.FindProperty("debugStyle");
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
            DrawFeedbackProvider();
            EditorGUILayout.PropertyField(assistantAgentFallbackVoiceId);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Correction Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(debugEnableCorrectionOverrides);

            if (debugEnableCorrectionOverrides.hasMultipleDifferentValues
                || debugEnableCorrectionOverrides.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(debugProvider);
                EditorGUILayout.PropertyField(debugForceFeedback);
                EditorGUILayout.PropertyField(debugStyle);

                if (debugForceFeedback.hasMultipleDifferentValues || debugForceFeedback.boolValue)
                {
                    EditorGUILayout.PropertyField(debugFeedbackText);
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFeedbackProvider()
        {
            var currentIndex = Array.FindIndex(
                FeedbackProviderValues,
                value => string.Equals(value, feedbackProvider.stringValue, StringComparison.OrdinalIgnoreCase));
            currentIndex = Mathf.Max(0, currentIndex);

            EditorGUI.showMixedValue = feedbackProvider.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selectedIndex = EditorGUILayout.Popup(
                FeedbackProviderLabel,
                currentIndex,
                FeedbackProviderLabels);
            if (EditorGUI.EndChangeCheck())
            {
                feedbackProvider.stringValue = FeedbackProviderValues[selectedIndex];
            }

            EditorGUI.showMixedValue = false;
        }
    }
}
