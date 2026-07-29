using System;
using System.Collections.Generic;
using System.Linq;
using SceneTalkVR.AvatarSystem;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class EditorCollectionAvatarMapping
    {
        public string taskId;
        public string requestedPresetKey;
        public bool approvedForEditorCollection;
        public bool approvedForCollection;
        public bool replaceableAsset;
    }

    [Serializable]
    public sealed class EditorCollectionPanoramaApproval
    {
        public string taskId;
        public string panoramaResourceKey;
        public bool equirectangular;
        public bool approvedForEditorCollection;
        public bool approvedForCollection;
        public bool replaceableAsset;
    }

    [CreateAssetMenu(fileName = "ExperimentEditorCollectionResources", menuName = "SceneTalkVR/Experiment/Editor Collection Resources")]
    public sealed class EditorCollectionResourceCatalog : ScriptableObject
    {
        [SerializeField] private string resourceSnapshotId;
        [SerializeField] private AvatarCatalog avatarCatalog;
        [SerializeField] private EditorCollectionAvatarMapping[] formalAvatarMappings = Array.Empty<EditorCollectionAvatarMapping>();
        [SerializeField] private EditorCollectionPanoramaApproval[] panoramas = Array.Empty<EditorCollectionPanoramaApproval>();
        [SerializeField] private string splitFeedbackAgentKey = "correction_agent_presenter_v1";
        [SerializeField] private bool splitFeedbackAgentApprovedForCollection;
        [SerializeField] private string pilotOrbKey = "generated_orb_v1";
        [SerializeField] private string pilotHumanoidKey = "correction_assistant_woman";
        [SerializeField] private bool pilotResourcesApprovedForCollection;

        public string ResourceSnapshotId => resourceSnapshotId?.Trim() ?? string.Empty;
        public AvatarCatalog AvatarCatalog => avatarCatalog;
        public IReadOnlyList<EditorCollectionAvatarMapping> FormalAvatarMappings => formalAvatarMappings;
        public IReadOnlyList<EditorCollectionPanoramaApproval> Panoramas => panoramas;
        public string SplitFeedbackAgentKey => splitFeedbackAgentKey?.Trim() ?? string.Empty;
        public string PilotOrbKey => pilotOrbKey?.Trim() ?? string.Empty;
        public string PilotHumanoidKey => pilotHumanoidKey?.Trim() ?? string.Empty;

        public EditorCollectionAvatarMapping FindAvatar(string taskId) => formalAvatarMappings?
            .FirstOrDefault(x => x != null && string.Equals(x.taskId, taskId, StringComparison.OrdinalIgnoreCase));

        public bool Validate(ExperimentTaskCatalog tasks, ExperimentVoiceProfileCatalog voices,
            ExperimentDeploymentCatalog deployments, out string error)
            => Validate(tasks, voices, deployments, ExperimentDeploymentProfileId.EditorCollection, out error);

        public bool Validate(ExperimentTaskCatalog tasks, ExperimentVoiceProfileCatalog voices,
            ExperimentDeploymentCatalog deployments, ExperimentDeploymentProfileId deploymentProfile,
            out string error)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(ResourceSnapshotId)) issues.Add("editor_collection_resource_snapshot_missing");
            var formal = tasks?.GetTasks(ExperimentTaskPhase.Formal) ?? new List<ExperimentTaskDefinition>();
            foreach (var task in formal)
            {
                var mapping = FindAvatar(task.taskId);
                if (mapping == null || mapping.requestedPresetKey != task.avatarPresetKey
                    || !mapping.approvedForEditorCollection || !mapping.approvedForCollection || !mapping.replaceableAsset)
                    issues.Add("editor_collection_avatar_mapping_invalid:" + task.taskId);
                else
                {
                    var avatarError = "editor_collection_avatar_catalog_missing";
                    if (avatarCatalog == null || !avatarCatalog.ValidateEditorCollectionPreset(mapping.requestedPresetKey, out avatarError))
                        issues.Add(avatarError + ":" + task.taskId);
                }
                var pano = panoramas?.FirstOrDefault(x => x != null && x.taskId == task.taskId);
                var texture = string.IsNullOrWhiteSpace(task.panoramaResourceKey) ? null : Resources.Load<Texture2D>(task.panoramaResourceKey);
                if (pano == null || pano.panoramaResourceKey != task.panoramaResourceKey || !pano.equirectangular
                    || !pano.approvedForEditorCollection || !pano.approvedForCollection || !pano.replaceableAsset
                    || texture == null || texture.width < 2048 || texture.height < 1024 || texture.width != texture.height * 2)
                    issues.Add("editor_collection_panorama_invalid:" + task.taskId);
            }
            if (!splitFeedbackAgentApprovedForCollection || string.IsNullOrWhiteSpace(SplitFeedbackAgentKey)) issues.Add("split_feedback_agent_unapproved");
            if (!pilotResourcesApprovedForCollection || PilotOrbKey != "generated_orb_v1" || PilotHumanoidKey != "correction_assistant_woman") issues.Add("pilot_resources_unapproved");
            var voiceError = "editor_collection_voice_catalog_missing";
            if (voices == null || !voices.ValidateForLockedCollection(formal.Select(x => x.voiceProfileKey), out voiceError)) issues.Add(voiceError);
            var deploymentError = "editor_collection_deployment_catalog_missing";
            if (deployments == null || !deployments.ValidateForCollection(deploymentProfile, out deploymentError)) issues.Add(deploymentError);
            error = string.Join(";", issues.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
            return string.IsNullOrWhiteSpace(error);
        }

#if UNITY_EDITOR
        public void EditorSet(string snapshotId, AvatarCatalog catalog, EditorCollectionAvatarMapping[] avatars,
            EditorCollectionPanoramaApproval[] panoramaValues, string agentKey, string orbKey, string humanoidKey)
        {
            resourceSnapshotId = snapshotId;
            avatarCatalog = catalog;
            formalAvatarMappings = avatars ?? Array.Empty<EditorCollectionAvatarMapping>();
            panoramas = panoramaValues ?? Array.Empty<EditorCollectionPanoramaApproval>();
            splitFeedbackAgentKey = agentKey;
            splitFeedbackAgentApprovedForCollection = true;
            pilotOrbKey = orbKey;
            pilotHumanoidKey = humanoidKey;
            pilotResourcesApprovedForCollection = true;
        }
#endif
    }
}
