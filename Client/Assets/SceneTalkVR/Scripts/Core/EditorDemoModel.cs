using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum EditorDemoDecisionStatus { DemoApproved }
    public enum EditorDemoProtocolPurpose { EditorDemonstration }
    public enum EditorDemoPreflightStatus { DEMO_READY, DEMO_WARNING, DEMO_BLOCKED }
    public enum EditorDemoVoiceMode { EditorDemoLiveVoice, EditorDemoOfflineVoice }

    [Serializable]
    public sealed class EditorDemoDecision
    {
        public string decisionId;
        [TextArea] public string value;
        public EditorDemoDecisionStatus status = EditorDemoDecisionStatus.DemoApproved;
        public string confirmedBy = "OpenAI project design decision for editor demonstration";
        public string evidenceReference = "editor-demo-protocol-v1";
        [TextArea] public string notes = "Not approved for research collection";
    }

    [Serializable]
    public sealed class EditorDemoAvatarMapping
    {
        public string taskId;
        public string demoAvatarKey;
        public bool demoPlaceholder = true;
        public bool semanticRoleApproved;
    }

    [Serializable]
    public sealed class EditorDemoPanoramaStatus
    {
        public string taskId;
        public string panoramaResourceKey;
        public bool demoAccepted = true;
        public bool collectionApproved;
        public string knownRisk;
    }

    public abstract class ExperimentV11EditorDemoProtocolBase : ScriptableObject
    {
        [SerializeField] private string demoProtocolVersion = "1.1-editor-demo-v1";
        [SerializeField] private EditorDemoProtocolPurpose protocolPurpose = EditorDemoProtocolPurpose.EditorDemonstration;
        [SerializeField] private bool researchApproved;
        [SerializeField] private bool collectionEligible;
        [SerializeField] private EditorDemoDecision[] decisions = CreateDecisions();

        public string DemoProtocolVersion => demoProtocolVersion;
        public EditorDemoProtocolPurpose ProtocolPurpose => protocolPurpose;
        public bool ResearchApproved => researchApproved;
        public bool CollectionEligible => collectionEligible;
        public IReadOnlyList<EditorDemoDecision> Decisions => decisions;
        public AssignmentPolicy FormalAssignmentPolicy => AssignmentPolicy.StrictWithoutReplacement;
        public PilotFeedbackStyleChoice PilotFeedbackStyle => PilotFeedbackStyleChoice.Explicit;
        public PilotAudioSourcePolicy VoiceOnlyAudioPolicy => PilotAudioSourcePolicy.NonSpatialHeadLocked;
        public int FormalMaxTurns => 6;
        public float FormalMaxDurationMinutes => 10f;
        public int PilotMaxTurns => 5;
        public float PilotMaxDurationMinutes => 8f;

        public AssignmentSequence[] FormalSequences => new[]
        {
            Sequence("a-b-c-d", FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR),
            Sequence("b-c-d-a", FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR, FormalConditionCode.NE),
            Sequence("c-d-a-b", FormalConditionCode.SE, FormalConditionCode.SR, FormalConditionCode.NE, FormalConditionCode.NR),
            Sequence("d-a-b-c", FormalConditionCode.SR, FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE)
        };

        public PilotSequenceDefinition[] PilotSequences => new[]
        {
            PilotSequence("a-b-c", PilotEmbodimentCondition.VoiceOnly, PilotEmbodimentCondition.FloatingOrb, PilotEmbodimentCondition.HumanoidAgent),
            PilotSequence("b-c-a", PilotEmbodimentCondition.FloatingOrb, PilotEmbodimentCondition.HumanoidAgent, PilotEmbodimentCondition.VoiceOnly),
            PilotSequence("c-a-b", PilotEmbodimentCondition.HumanoidAgent, PilotEmbodimentCondition.VoiceOnly, PilotEmbodimentCondition.FloatingOrb)
        };

        public bool Validate(out string error)
        {
            var required = RequiredValues();
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(demoProtocolVersion)) issues.Add("demo_protocol_version_missing");
            if (researchApproved || collectionEligible) issues.Add("demo_protocol_collection_isolation_invalid");
            if (decisions == null || decisions.Length != required.Count) issues.Add("demo_requires_eleven_decisions");
            foreach (var pair in required)
            {
                var item = decisions?.FirstOrDefault(x => x != null && x.decisionId == pair.Key);
                if (item == null || item.status != EditorDemoDecisionStatus.DemoApproved || item.value != pair.Value)
                    issues.Add("demo_decision_invalid:" + pair.Key);
                else if (item.confirmedBy != "OpenAI project design decision for editor demonstration"
                    || item.evidenceReference != "editor-demo-protocol-v1"
                    || item.notes != "Not approved for research collection") issues.Add("demo_decision_provenance_invalid:" + pair.Key);
            }
            error = string.Join(";", issues); return issues.Count == 0;
        }

        public static IReadOnlyDictionary<string, string> RequiredValues() => new Dictionary<string, string>
        {
            ["condition_letter_mapping"] = "a=NE,b=NR,c=SE,d=SR",
            ["formal_task_no_replacement"] = "strict_without_replacement",
            ["formal_social_comfort"] = "excluded",
            ["pilot_feedback_style"] = "explicit",
            ["voice_only_spatial_audio"] = "non_spatial_head_locked",
            ["pilot_sequence_mapping"] = "a=voice_only,b=floating_orb,c=humanoid_agent",
            ["formal_max_turns"] = "6",
            ["formal_max_duration"] = "10 minutes",
            ["pilot_max_turns"] = "5",
            ["pilot_max_duration"] = "8 minutes",
            ["questionnaire_scale_anchors"] = "1 = Strongly disagree / 非常不同意; 7 = Strongly agree / 非常同意"
        };

        private static EditorDemoDecision[] CreateDecisions() => RequiredValues().Select(x => new EditorDemoDecision { decisionId = x.Key, value = x.Value }).ToArray();
        private static AssignmentSequence Sequence(string id, params FormalConditionCode[] values) => new AssignmentSequence { sequenceId = id, conditions = values };
        private static PilotSequenceDefinition PilotSequence(string id, params PilotEmbodimentCondition[] values) => new PilotSequenceDefinition { sequenceId = id, conditions = values, confirmed = true };
    }

    public abstract class EditorDemoAvatarMappingCatalogBase : ScriptableObject
    {
        [SerializeField] private string mappingVersion = "1.0";
        [SerializeField] private EditorDemoAvatarMapping[] formalMappings = Array.Empty<EditorDemoAvatarMapping>();
        [SerializeField] private GameObject pilotHumanoidPrefab;
        [SerializeField] private string pilotHumanoidPrefabKey = "teacher_female_humanoid_v1";
        [SerializeField] private bool pilotHumanoidDemoPlaceholder = true;
        [SerializeField] private EditorDemoPanoramaStatus[] panoramaStatuses = Array.Empty<EditorDemoPanoramaStatus>();
        public string MappingVersion => mappingVersion;
        public IReadOnlyList<EditorDemoAvatarMapping> FormalMappings => formalMappings;
        public GameObject PilotHumanoidPrefab => pilotHumanoidPrefab;
        public string PilotHumanoidPrefabKey => pilotHumanoidPrefabKey;
        public bool PilotHumanoidDemoPlaceholder => pilotHumanoidDemoPlaceholder;
        public IReadOnlyList<EditorDemoPanoramaStatus> PanoramaStatuses => panoramaStatuses;
        public EditorDemoAvatarMapping Find(string taskId) => formalMappings?.FirstOrDefault(x => x != null && string.Equals(x.taskId, taskId, StringComparison.OrdinalIgnoreCase));
#if UNITY_EDITOR
        public void EditorSet(string version, EditorDemoAvatarMapping[] mappings, GameObject humanoid, string humanoidKey, EditorDemoPanoramaStatus[] panoramas)
        { mappingVersion = version; formalMappings = mappings ?? Array.Empty<EditorDemoAvatarMapping>(); pilotHumanoidPrefab = humanoid; pilotHumanoidPrefabKey = humanoidKey; pilotHumanoidDemoPlaceholder = true; panoramaStatuses = panoramas ?? Array.Empty<EditorDemoPanoramaStatus>(); }
#endif
    }

    [Serializable]
    public sealed class EditorDemoPreflightResult
    {
        public EditorDemoPreflightStatus status;
        public string mode;
        public string[] checks = Array.Empty<string>();
        public string[] warnings = Array.Empty<string>();
        public string[] blockers = Array.Empty<string>();
    }
}
