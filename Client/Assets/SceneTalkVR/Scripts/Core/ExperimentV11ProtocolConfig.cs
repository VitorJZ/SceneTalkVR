using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum ProtocolDecisionStatus
    {
        Unconfirmed,
        Confirmed,
        NotApplicable
    }

    [Serializable]
    public sealed class ExperimentProtocolDecision
    {
        public string decisionId;
        [TextArea] public string question;
        public ProtocolDecisionStatus status = ProtocolDecisionStatus.Unconfirmed;
        [TextArea] public string confirmedValue;
    }

    [Serializable]
    public sealed class ExperimentConditionSequenceDefinition
    {
        public string sequenceId;
        public string[] conditionCodes = Array.Empty<string>();
        public bool confirmed;
    }

    [CreateAssetMenu(fileName = "ExperimentV11Protocol", menuName = "SceneTalkVR/Experiment v1.1 Protocol")]
    public sealed class ExperimentV11ProtocolConfig : ScriptableObject
    {
        [Header("Immutable Baseline Metadata")]
        [SerializeField] private string protocolVersion = "1.1.0-stage1";
        [SerializeField] private string buildVersion = "stage1-20260717";
        [SerializeField] private ExperimentPhase experimentPhase = ExperimentPhase.Formal;
        [SerializeField] private bool formalModeLocked = true;

        [Header("Formal Conditions")]
        [SerializeField] private string[] formalConditionCodes = { "NE", "NR", "SE", "SR" };
        [SerializeField] private ExperimentConditionSequenceDefinition[] conditionSequenceDefinitions = Array.Empty<ExperimentConditionSequenceDefinition>();

        [Header("Tasks and Pilot Options")]
        [SerializeField] private string[] formalTaskIds = { "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance" };
        [SerializeField] private string[] pilotTaskIds = { "restaurant_reservation" };
        [SerializeField] private string[] pilotEmbodimentOptions = { "voice_only", "floating_orb", "humanoid_agent" };

        [Header("Feedback Timing")]
        [TextArea]
        [SerializeField] private string feedbackTimingPolicy = "feedback_first_then_dialogue; dialogue TTS may be prepared before the playback gate opens";

        [Header("Required Research Decisions")]
        [SerializeField] private ExperimentProtocolDecision[] requiredDecisions =
        {
            new ExperimentProtocolDecision { decisionId = "condition_letter_mapping", question = "Map a/b/c/d to NE/NR/SE/SR.", status = ProtocolDecisionStatus.Unconfirmed },
            new ExperimentProtocolDecision { decisionId = "pilot_feedback_style", question = "Fix pilot feedback style: Explicit, Recast, or another approved design.", status = ProtocolDecisionStatus.Unconfirmed },
            new ExperimentProtocolDecision { decisionId = "voice_only_spatial_audio", question = "Define whether Voice Only is spatial, non-spatial, and its source position.", status = ProtocolDecisionStatus.Unconfirmed },
            new ExperimentProtocolDecision { decisionId = "formal_social_comfort", question = "Decide whether Social Comfort is included in the formal questionnaire.", status = ProtocolDecisionStatus.Unconfirmed },
            new ExperimentProtocolDecision { decisionId = "formal_task_no_replacement", question = "Decide whether formal task assignment is strictly without replacement.", status = ProtocolDecisionStatus.Unconfirmed }
        };

        public string ProtocolVersion => protocolVersion?.Trim() ?? string.Empty;
        public string BuildVersion => buildVersion?.Trim() ?? string.Empty;
        public ExperimentPhase ExperimentPhase => experimentPhase;
        public bool FormalModeLocked => formalModeLocked;
        public IReadOnlyList<string> FormalConditionCodes => formalConditionCodes;
        public IReadOnlyList<ExperimentConditionSequenceDefinition> ConditionSequenceDefinitions => conditionSequenceDefinitions;
        public IReadOnlyList<string> PilotEmbodimentOptions => pilotEmbodimentOptions;
        public IReadOnlyList<string> FormalTaskIds => formalTaskIds;
        public IReadOnlyList<string> PilotTaskIds => pilotTaskIds;
        public string FeedbackTimingPolicy => feedbackTimingPolicy?.Trim() ?? string.Empty;
        public IReadOnlyList<ExperimentProtocolDecision> RequiredDecisions => requiredDecisions;

        public bool ValidateForFormalMode(out string error)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(ProtocolVersion)) issues.Add("protocolVersion is empty");
            if (string.IsNullOrWhiteSpace(BuildVersion)) issues.Add("buildVersion is empty");
            if (!FormalModeLocked) issues.Add("formalModeLocked is false");
            if (!HasExactlyFormalConditions()) issues.Add("formalConditionCodes must contain NE, NR, SE, SR exactly once");
            if (requiredDecisions == null || requiredDecisions.Length == 0)
            {
                issues.Add("required decision list is missing");
            }
            else
            {
                foreach (var decision in requiredDecisions)
                {
                    if (decision == null || decision.status != ProtocolDecisionStatus.Confirmed)
                    {
                        issues.Add($"unconfirmed protocol decision: {decision?.decisionId ?? "<null>"}");
                    }
                }
            }

            error = issues.Count == 0 ? string.Empty : string.Join("; ", issues);
            return issues.Count == 0;
        }

        private bool HasExactlyFormalConditions()
        {
            if (formalConditionCodes == null || formalConditionCodes.Length != 4) return false;
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NE", "NR", "SE", "SR" };
            foreach (var code in formalConditionCodes)
            {
                if (string.IsNullOrWhiteSpace(code) || !expected.Remove(code.Trim())) return false;
            }
            return expected.Count == 0;
        }
    }
}
