using System;
using System.Collections.Generic;
using System.Linq;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class EditorDemoPreflight
    {
        [MenuItem("SceneTalkVR/Demo/Run Formal Demo Preflight")]
        public static void RunFormalMenu() => Log(Run(true));
        [MenuItem("SceneTalkVR/Demo/Run Pilot Demo Preflight")]
        public static void RunPilotMenu() => Log(Run(false));

        public static EditorDemoPreflightResult Run(bool formal)
        {
            var checks = new List<string>(); var warnings = new List<string>(); var blockers = new List<string>();
            var protocol = Load<ExperimentV11EditorDemoProtocol>(EditorDemoAssetBuilder.ProtocolPath);
            if (protocol != null && protocol.Validate(out _)) checks.Add("demo_protocol_11_decisions_valid"); else blockers.Add("demo_protocol_missing_or_invalid");
            var official = Load<ExperimentV11ProtocolConfig>(EditorDemoAssetBuilder.Root + "ExperimentV11Protocol.asset");
            if (official != null && official.RequiredDecisions.Count == 11 && official.RequiredDecisions.All(x => x.status == ProtocolDecisionStatus.Unconfirmed)) checks.Add("official_protocol_11_unconfirmed"); else blockers.Add("official_protocol_decisions_changed");
            var tasks = Load<ExperimentTaskCatalog>(EditorDemoAssetBuilder.Root + "ExperimentTaskCatalog.asset");
            var phase = formal ? ExperimentTaskPhase.Formal : ExperimentTaskPhase.Pilot;
            var expected = formal ? 4 : 3;
            var selected = tasks?.GetTasks(phase) ?? new List<ExperimentTaskDefinition>();
            if (selected.Count == expected) checks.Add($"{phase.ToString().ToLowerInvariant()}_tasks_{expected}"); else blockers.Add($"{phase.ToString().ToLowerInvariant()}_task_count");
            foreach (var task in selected)
                if (!string.IsNullOrWhiteSpace(task.panoramaResourceKey) && Resources.Load<Texture2D>(task.panoramaResourceKey) != null) checks.Add("panorama:" + task.taskId); else blockers.Add("panorama_missing:" + task.taskId);
            var voices = Load<ExperimentVoiceProfileCatalog>(EditorDemoAssetBuilder.VoicePath);
            if (voices != null && voices.Profiles.Count >= 2 && voices.Profiles.All(x => x.approvedForEditorDemo && !x.approvedForCollection)) checks.Add("demo_voice_isolated"); else blockers.Add("demo_voice_invalid");
            var deployment = Load<ExperimentDeploymentCatalog>(EditorDemoAssetBuilder.DeploymentPath);
            if (deployment != null && deployment.TryGet(ExperimentDeploymentProfileId.EditorDemo, out var dp) && dp.approvedForEditorDemo && !dp.approvedForCollection && !dp.collectionAllowed) checks.Add("demo_deployment_isolated"); else blockers.Add("demo_deployment_invalid");
            var mapping = Load<EditorDemoAvatarMappingCatalog>(EditorDemoAssetBuilder.MappingPath);
            if (formal)
            {
                var avatars = Load<AvatarCatalog>("Assets/SceneTalkVR/Avatar/Catalogs/AvatarCatalog.asset");
                foreach (var task in selected) { var map = mapping?.Find(task.taskId); if (map != null && avatars?.FindByKey(map.demoAvatarKey)?.IsUsable == true) checks.Add("demo_avatar:" + task.taskId); else blockers.Add("demo_avatar_missing:" + task.taskId); }
                warnings.Add("hotel_furniture_gym_panoramas_non_standard_1_to_1");
                warnings.Add("demo_avatars_are_semantic_placeholders");
            }
            else
            {
                if (protocol?.PilotFeedbackStyle == PilotFeedbackStyleChoice.Explicit) checks.Add("pilot_explicit"); else blockers.Add("pilot_style");
                if (protocol?.VoiceOnlyAudioPolicy == PilotAudioSourcePolicy.NonSpatialHeadLocked) checks.Add("voice_only_head_locked"); else blockers.Add("voice_only_audio");
                if (mapping?.PilotHumanoidPrefab != null) checks.Add("pilot_humanoid_demo_prefab"); else blockers.Add("pilot_humanoid_prefab_missing");
                checks.Add("floating_orb_generated_profile"); warnings.Add("pilot_humanoid_is_demo_placeholder");
            }
            if (Type.GetType("SceneTalkVR.Core.FeedbackFirstPlaybackGate, Assembly-CSharp") != null) checks.Add("feedback_first_gate_present"); else blockers.Add("feedback_first_gate_missing");
            var status = blockers.Count > 0 ? EditorDemoPreflightStatus.DEMO_BLOCKED : warnings.Count > 0 ? EditorDemoPreflightStatus.DEMO_WARNING : EditorDemoPreflightStatus.DEMO_READY;
            return new EditorDemoPreflightResult { status = status, mode = formal ? "editor_demo_formal" : "editor_demo_pilot", checks = checks.ToArray(), warnings = warnings.ToArray(), blockers = blockers.ToArray() };
        }

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static void Log(EditorDemoPreflightResult value) => Debug.Log("[SceneTalkVR Demo Preflight] " + JsonUtility.ToJson(value, true));
    }
}
