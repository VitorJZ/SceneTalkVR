using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [CreateAssetMenu(fileName = "ExperimentV11RehearsalResources", menuName = "SceneTalkVR/Experiment/Rehearsal Resources")]
    public sealed class ExperimentV11RehearsalResourceCatalog : ScriptableObject
    {
        [SerializeField] private string resourceSnapshotId = "v1.1-rehearsal-1-resources";
        [SerializeField] private RehearsalAvatarMapping[] formalAvatarMappings = Array.Empty<RehearsalAvatarMapping>();
        [SerializeField] private GameObject pilotHumanoidPrefab;
        [SerializeField] private string pilotHumanoidPresetKey = "teacher_female_humanoid_v1";
        [SerializeField] private RehearsalPanoramaApproval[] panoramas = Array.Empty<RehearsalPanoramaApproval>();
        public string ResourceSnapshotId => resourceSnapshotId?.Trim() ?? string.Empty;
        public IReadOnlyList<RehearsalAvatarMapping> FormalAvatarMappings => formalAvatarMappings;
        public GameObject PilotHumanoidPrefab => pilotHumanoidPrefab;
        public string PilotHumanoidPresetKey => pilotHumanoidPresetKey?.Trim() ?? string.Empty;
        public IReadOnlyList<RehearsalPanoramaApproval> Panoramas => panoramas;
        public RehearsalAvatarMapping FindAvatar(string taskId) => formalAvatarMappings?.FirstOrDefault(x => x != null && string.Equals(x.taskId, taskId, StringComparison.OrdinalIgnoreCase));
#if UNITY_EDITOR
        public void EditorSet(string snapshotId,RehearsalAvatarMapping[] mappings,GameObject humanoid,string humanoidKey,RehearsalPanoramaApproval[] panoramaValues){resourceSnapshotId=snapshotId;formalAvatarMappings=mappings??Array.Empty<RehearsalAvatarMapping>();pilotHumanoidPrefab=humanoid;pilotHumanoidPresetKey=humanoidKey;panoramas=panoramaValues??Array.Empty<RehearsalPanoramaApproval>();}
#endif
    }
}
