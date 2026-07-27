using System;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class EditorDemoAssetBuilder
    {
        private const string UnifiedDialogueAvatarKey = "barista_male_humanoid_v1";
        public const string Root = "Assets/SceneTalkVR/ExperimentProtocol/";
        public const string ProtocolPath = Root + "ExperimentV11EditorDemoProtocol.asset";
        public const string MappingPath = Root + "EditorDemoAvatarMapping.asset";
        public const string VoicePath = Root + "ExperimentEditorDemoVoiceProfileCatalog.asset";
        public const string DeploymentPath = Root + "ExperimentEditorDemoDeploymentCatalog.asset";

        [MenuItem("SceneTalkVR/Demo/Create or Update Editor Demo Assets")]
        public static void CreateOrUpdate()
        {
            EnsureFolder(Root.TrimEnd('/'));
            GetOrCreate<ExperimentV11EditorDemoProtocol>(ProtocolPath);
            var mapping = GetOrCreate<EditorDemoAvatarMappingCatalog>(MappingPath);
            var tasks = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(Root + "ExperimentTaskCatalog.asset");
            var formal = new[]
            {
                Map("hotel_check_in", UnifiedDialogueAvatarKey),
                Map("furniture_shopping", UnifiedDialogueAvatarKey),
                Map("gym_membership", UnifiedDialogueAvatarKey),
                Map("tourist_assistance", UnifiedDialogueAvatarKey)
            };
            var humanoid = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_female_humanoid_v1.prefab");
            var panoramas = formal.Select(x =>
            {
                var task = tasks?.Find(x.taskId);
                return new EditorDemoPanoramaStatus
                {
                    taskId = x.taskId,
                    panoramaResourceKey = task?.panoramaResourceKey ?? string.Empty,
                    demoAccepted = true,
                    collectionApproved = false,
                    knownRisk = x.taskId == "tourist_assistance" ? "Current 2:1 local panorama; demo-only quality status." : "Non-standard 1:1 panorama; Editor Demo only."
                };
            }).ToArray();
            mapping.EditorSet("1.0-editor-demo", formal, humanoid, "teacher_female_humanoid_v1", panoramas);

            var voices = GetOrCreate<ExperimentVoiceProfileCatalog>(VoicePath);
            var dialogue = Voice("editor_demo_dialogue_voice", "101050", VoiceSubtitlePolicy.AllSpeech);
            var feedback = Voice("editor_demo_feedback_voice", "default_female_en", VoiceSubtitlePolicy.FeedbackOnly);
            voices.EditorSet("1.1-editor-demo-v1", feedback.voiceProfileKey, feedback.voiceProfileKey, feedback.voiceProfileKey, new[] { dialogue, feedback });

            var deployments = GetOrCreate<ExperimentDeploymentCatalog>(DeploymentPath);
            deployments.EditorSet("1.1-editor-demo-v1", new[] { new ExperimentDeploymentProfile
            {
                profileId = ExperimentDeploymentProfileId.EditorDemo,
                voiceGatewayBaseUrl = "http://127.0.0.1:8787",
                requestTimeoutSeconds = 30,
                sttProvider = "actual_editor_gateway",
                ttsProvider = "tencent",
                microphonePolicy = "UnityEditor default microphone",
                networkRequired = true,
                approvedForCollection = false,
                approvedForEditorDemo = true,
                loopbackAllowedForEditorDemo = true,
                collectionAllowed = false,
                evidenceReference = "editor-demo-protocol-v1"
            }});
            EditorUtility.SetDirty(mapping); EditorUtility.SetDirty(voices); EditorUtility.SetDirty(deployments);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[SceneTalkVR] Editor Demo assets created/updated. These assets are not collection approved.");
        }

        private static EditorDemoAvatarMapping Map(string task, string avatar) => new EditorDemoAvatarMapping { taskId = task, demoAvatarKey = avatar, demoPlaceholder = true, semanticRoleApproved = false };
        private static ExperimentVoiceProfile Voice(string key, string voiceId, VoiceSubtitlePolicy subtitle) => new ExperimentVoiceProfile
        {
            voiceProfileKey = key, provider = "tencent", voiceId = voiceId, language = "en-US", speakingSpeed = 1f,
            volume = 1f, pitch = 1f, sampleRate = 24000, subtitlePolicy = subtitle,
            approvedForCollection = false, approvedForEditorDemo = true, offlineDemoVoice = false,
            approvedBy = "Editor demonstration configuration", evidenceReference = "editor-demo-protocol-v1", assetVersion = "editor-demo-v1"
        };

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var item = AssetDatabase.LoadAssetAtPath<T>(path);
            if (item != null) return item;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(Path.GetFullPath(path))) AssetDatabase.DeleteAsset(path);
            item = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(item, path); return item;
        }
        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var part in path.Split('/').Skip(1)) { var next = current + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part); current = next; }
        }
    }
}
