using System;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class RehearsalAssetBuilder
    {
        public const string Root = "Assets/SceneTalkVR/ExperimentProtocol/";
        public const string ProtocolPath = Root + "ExperimentV11RehearsalProtocol.asset";
        public const string ResourcePath = Root + "ExperimentV11RehearsalResources.asset";
        public const string VoicePath = Root + "ExperimentV11RehearsalVoiceProfileCatalog.asset";
        public const string DeploymentPath = Root + "ExperimentV11RehearsalDeploymentCatalog.asset";

        [MenuItem("SceneTalkVR/Experiment/Rehearsal/Create or Update Assets")]
        public static void CreateOrUpdate()
        {
            var protocol = GetOrCreate<ExperimentV11RehearsalProtocol>(ProtocolPath);
            var resource = GetOrCreate<ExperimentV11RehearsalResourceCatalog>(ResourcePath);
            var tasks = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(Root + "ExperimentTaskCatalog.asset");
            var mappings = new[]
            {
                Map("hotel_check_in", "hotel receptionist / front desk clerk", "barista_humanoid_v1"),
                Map("furniture_shopping", "furniture salesperson", "teacher_humanoid_v1"),
                Map("gym_membership", "gym membership consultant / trainer", "barista_male_humanoid_v1"),
                Map("tourist_assistance", "tourist information officer", "teacher_female_humanoid_v1")
            };
            var formalTasks = tasks != null ? tasks.GetTasks(ExperimentTaskPhase.Formal) : new System.Collections.Generic.List<ExperimentTaskDefinition>();
            var pilotTasks = tasks != null ? tasks.GetTasks(ExperimentTaskPhase.Pilot) : new System.Collections.Generic.List<ExperimentTaskDefinition>();
            var panoramas = formalTasks.Concat(pilotTasks)
                .Select(x => new RehearsalPanoramaApproval { taskId = x.taskId, panoramaResourceKey = x.panoramaResourceKey,
                    approvedForRehearsal = true, replaceableAsset = true,
                    knownRisk = x.taskId == "tourist_assistance" ? "Current local 2:1 panorama; replaceable before collection." : "Current local rehearsal panorama; visual quality does not block rehearsal." }).ToArray();
            var humanoid = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_female_humanoid_v1.prefab");
            resource.EditorSet("v1.1-rehearsal-1-resources", mappings, humanoid, "teacher_female_humanoid_v1", panoramas);

            var voices = GetOrCreate<ExperimentVoiceProfileCatalog>(VoicePath);
            var dialogue = Voice("rehearsal_dialogue_voice", "101050", VoiceSubtitlePolicy.AllSpeech);
            var feedback = Voice("rehearsal_feedback_voice", "default_female_en", VoiceSubtitlePolicy.FeedbackOnly);
            voices.EditorSet("1.1-rehearsal-1", feedback.voiceProfileKey, feedback.voiceProfileKey, feedback.voiceProfileKey, new[] { dialogue, feedback });

            var deployments = GetOrCreate<ExperimentDeploymentCatalog>(DeploymentPath);
            deployments.EditorSet("1.1-rehearsal-1", new[] { new ExperimentDeploymentProfile
            {
                profileId = ExperimentDeploymentProfileId.RehearsalEditor, voiceGatewayBaseUrl = "http://127.0.0.1:8787",
                requestTimeoutSeconds = 30, sttProvider = "voice_gateway_live_stt", ttsProvider = "tencent",
                microphonePolicy = "UnityEditor default microphone", networkRequired = true,
                approvedForRehearsal = true, loopbackAllowedForRehearsal = true,
                approvedForCollection = false, collectionAllowed = false,
                evidenceReference = "scenetalkvr-rehearsal-baseline-v1"
            }});
            EditorUtility.SetDirty(protocol); EditorUtility.SetDirty(resource); EditorUtility.SetDirty(voices); EditorUtility.SetDirty(deployments);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[SceneTalkVR] Rehearsal protocol and resource assets updated. Collection approval remains false.");
        }

        private static RehearsalAvatarMapping Map(string task, string role, string avatar) => new RehearsalAvatarMapping
        { taskId = task, taskRole = role, avatarPresetKey = avatar, approvedForRehearsal = true, approvedForCollection = false };
        private static ExperimentVoiceProfile Voice(string key, string voiceId, VoiceSubtitlePolicy subtitles) => new ExperimentVoiceProfile
        {
            voiceProfileKey = key, provider = "tencent", voiceId = voiceId, language = "en-US", speakingSpeed = 1f,
            volume = 1f, pitch = 1f, sampleRate = 24000, subtitlePolicy = subtitles,
            approvedForRehearsal = true, approvedForCollection = false, approvedForEditorDemo = false,
            approvedBy = "Project Lead Approval", evidenceReference = "scenetalkvr-rehearsal-baseline-v1", assetVersion = "rehearsal-1"
        };
        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var item = AssetDatabase.LoadAssetAtPath<T>(path);
            if (item != null && MonoScript.FromScriptableObject(item) != null) return item;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(Path.GetFullPath(path))) AssetDatabase.DeleteAsset(path);
            item = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(item, path); return item;
        }
    }
}
