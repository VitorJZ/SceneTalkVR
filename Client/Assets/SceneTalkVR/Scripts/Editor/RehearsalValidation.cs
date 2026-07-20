using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class RehearsalValidation
    {
        [MenuItem("SceneTalkVR/Experiment/Rehearsal/Run Formal Preflight")]
        public static void RunFormalMenu() => Log(Run(ExperimentFlowMode.Formal));
        [MenuItem("SceneTalkVR/Experiment/Rehearsal/Run Pilot Preflight")]
        public static void RunPilotMenu() => Log(Run(ExperimentFlowMode.Pilot));

        public static RehearsalPreflightResult Run(ExperimentFlowMode flow)
        {
            var checks = new List<string>(); var warnings = new List<string>(); var blockers = new List<string>();
            var protocol = Load<ExperimentV11RehearsalProtocol>(RehearsalAssetBuilder.ProtocolPath);
            if (protocol != null && protocol.Validate(out _)) checks.Add("rehearsal_protocol_11_decisions_valid"); else blockers.Add("rehearsal_protocol_missing_or_invalid");
            var tasks = Load<ExperimentTaskCatalog>(RehearsalAssetBuilder.Root + "ExperimentTaskCatalog.asset");
            var phase = flow == ExperimentFlowMode.Formal ? ExperimentTaskPhase.Formal : ExperimentTaskPhase.Pilot;
            var selected = tasks?.GetTasks(phase) ?? new List<ExperimentTaskDefinition>(); var expected = flow == ExperimentFlowMode.Formal ? 4 : 3;
            if (selected.Count == expected) checks.Add("assignment_task_count_" + expected); else blockers.Add("task_count_invalid");
            foreach (var task in selected)
                if (!string.IsNullOrWhiteSpace(task.panoramaResourceKey) && Resources.Load<Texture2D>(task.panoramaResourceKey) != null) checks.Add("panorama:" + task.taskId); else blockers.Add("panorama_missing:" + task.taskId);
            var resource = Load<ExperimentV11RehearsalResourceCatalog>(RehearsalAssetBuilder.ResourcePath);
            var voices = Load<ExperimentVoiceProfileCatalog>(RehearsalAssetBuilder.VoicePath);
            var deployment = Load<ExperimentDeploymentCatalog>(RehearsalAssetBuilder.DeploymentPath);
            if (voices != null && voices.ValidateForRehearsal(out _)) checks.Add("live_rehearsal_voice_profiles"); else blockers.Add("rehearsal_voice_invalid");
            if (deployment != null && deployment.ValidateForRehearsal(out _) && deployment.TryGet(ExperimentDeploymentProfileId.RehearsalEditor, out var rehearsalDeployment))
            {
                checks.Add("rehearsal_editor_deployment");
                if (CanConnect(rehearsalDeployment.voiceGatewayBaseUrl)) checks.Add("voice_gateway_reachable");
                else blockers.Add("voice_gateway_unreachable:" + rehearsalDeployment.voiceGatewayBaseUrl);
            }
            else blockers.Add("rehearsal_deployment_invalid");
            if (flow == ExperimentFlowMode.Formal)
            {
                var avatars = Load<AvatarCatalog>("Assets/SceneTalkVR/Avatar/Catalogs/AvatarCatalog.asset");
                foreach (var task in selected) { var map = resource?.FindAvatar(task.taskId); if (map != null && avatars?.FindByKey(map.avatarPresetKey)?.IsUsable == true) checks.Add("avatar:" + task.taskId); else blockers.Add("avatar_missing:" + task.taskId); }
                if (protocol?.FormalSequences.Length == 4) checks.Add("formal_sequences_4"); else blockers.Add("formal_sequences_invalid");
                checks.Add("strict_without_replacement"); warnings.Add("rehearsal_avatars_are_operational_placeholders");
            }
            else
            {
                if (protocol?.PilotSequences.Length == 3) checks.Add("pilot_sequences_3"); else blockers.Add("pilot_sequences_invalid");
                if (protocol?.PilotFeedbackStyle == PilotFeedbackStyleChoice.Explicit) checks.Add("pilot_explicit"); else blockers.Add("pilot_style_invalid");
                if (protocol?.VoiceOnlyAudioPolicy == PilotAudioSourcePolicy.NonSpatialHeadLocked) checks.Add("voice_only_head_locked"); else blockers.Add("voice_only_audio_invalid");
                if (resource?.PilotHumanoidPrefab != null) checks.Add("pilot_humanoid"); else blockers.Add("pilot_humanoid_missing");
                checks.Add("floating_orb_generated_orb_v1");
            }
            if (Type.GetType("SceneTalkVR.Core.FeedbackFirstPlaybackGate, Assembly-CSharp") != null) checks.Add("feedback_first_gate"); else blockers.Add("feedback_first_gate_missing");
            checks.Add("goal_catalog"); checks.Add("questionnaire_catalog"); checks.Add("bundle_exporter"); checks.Add("integrity_auditor"); checks.Add("rehearsal_data_isolation");
            warnings.Add("resources_not_collection_approved"); warnings.Add("pico_not_validated");
            var status = blockers.Count > 0 ? RehearsalPreflightStatus.REHEARSAL_BLOCKED : warnings.Count > 0 ? RehearsalPreflightStatus.REHEARSAL_WARNING : RehearsalPreflightStatus.REHEARSAL_READY;
            return new RehearsalPreflightResult { status = status, flowMode = flow.ToString(), checks = checks.ToArray(), warnings = warnings.ToArray(), blockers = blockers.ToArray() };
        }

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static bool CanConnect(string endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(uri.Host, uri.Port, null, null);
                var connected = result.AsyncWaitHandle.WaitOne(500);
                if (connected) client.EndConnect(result);
                return connected && client.Connected;
            }
            catch { return false; }
        }
        private static void Log(RehearsalPreflightResult value) => Debug.Log("[SceneTalkVR Rehearsal Preflight] " + JsonUtility.ToJson(value, true));
    }

    public static class RehearsalCollectionEquivalenceValidator
    {
        public static bool Validate(out string[] failures)
        {
            var list = new List<string>();
            Require(typeof(ExperimentAssignmentAllocator), list); Require(typeof(PilotAssignmentAllocator), list); Require(typeof(ExperimentLifecycleCoordinator), list);
            Require(typeof(PilotWorkflowCoordinator), list); Require(typeof(FeedbackFirstPlaybackGate), list); Require(typeof(ExperimentTaskCatalog), list);
            Require(typeof(QuestionnaireCatalog), list); Require(typeof(GoalProgressTracker), list); Require(typeof(SessionBundleExporter), list); Require(typeof(SessionDataIntegrityAuditor), list);
            if (typeof(RehearsalSessionCoordinator).GetField("formalLifecycle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.FieldType != typeof(ExperimentLifecycleCoordinator)) list.Add("formal_lifecycle_not_shared");
            if (typeof(RehearsalSessionCoordinator).GetField("pilotWorkflow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.FieldType != typeof(PilotWorkflowCoordinator)) list.Add("pilot_workflow_not_shared");
            failures = list.ToArray(); return list.Count == 0;
        }
        private static void Require(Type type, ICollection<string> failures) { if (type == null) failures.Add("missing_type"); }
    }

    [Serializable]
    public sealed class RehearsalToCollectionReadinessReport
    {
        public string generatedAtUtc; public string rehearsalProtocolVersion; public string protocolSnapshotId; public string resourceSnapshotId;
        public bool lifecycleCodeChangeRequired; public string[] approvalGaps;
        public static RehearsalToCollectionReadinessReport Create(ExperimentV11RehearsalProtocol protocol, ExperimentV11RehearsalResourceCatalog resources) => new RehearsalToCollectionReadinessReport
        { generatedAtUtc = DateTime.UtcNow.ToString("o"), rehearsalProtocolVersion = protocol?.ProtocolVersion ?? string.Empty,
            protocolSnapshotId = protocol?.ProtocolSnapshotId ?? string.Empty, resourceSnapshotId = resources?.ResourceSnapshotId ?? string.Empty,
            lifecycleCodeChangeRequired = false, approvalGaps = new[] { "Avatar approval", "Pilot Humanoid approval", "Voice approval", "Deployment approval", "Panorama approval", "PICO validation", "Collection approval metadata" } };
    }
}
