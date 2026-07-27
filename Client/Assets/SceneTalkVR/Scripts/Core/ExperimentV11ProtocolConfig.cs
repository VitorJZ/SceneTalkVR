using System;
using System.Collections.Generic;
using System.Linq;
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
        public string confirmedBy;
        public string confirmedAtUtc;
        public string evidenceReference;
        [TextArea] public string notes;
    }

    [Serializable]
    public sealed class ExperimentProtocolChange
    {
        public string changedAtUtc;
        public string changedBy;
        public string previousProtocolVersion;
        public string newProtocolVersion;
        public string evidenceReference;
        [TextArea] public string summary;
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
        [SerializeField] private string protocolVersion = "1.1.0-stage7";
        [SerializeField] private string buildVersion = "stage7-20260719";
        [SerializeField] private ExperimentPhase experimentPhase = ExperimentPhase.Formal;
        [SerializeField] private bool formalModeLocked = true;
        [SerializeField] private string protocolPurpose = "ParticipantCollection";
        [SerializeField] private bool approvedForCollection;
        [SerializeField] private string approvalAuthority;
        [SerializeField] private string approvalEvidence;
        [SerializeField] private string protocolSnapshotId;

        [Header("Official Participant Flow")]
        [SerializeField] private FormalConditionOrderPolicy formalConditionOrderPolicy = FormalConditionOrderPolicy.Undefined;
        [SerializeField] private string formalTaskAssignmentPolicy;
        [SerializeField] private GoalConfirmationPolicy goalConfirmationPolicy = GoalConfirmationPolicy.ExperimenterReview;
        [SerializeField] private GoalSequencePolicy goalSequencePolicy = GoalSequencePolicy.Undefined;
        [SerializeField] private QuestionnaireReturnPolicy questionnaireTransitionPolicy = QuestionnaireReturnPolicy.Undefined;
        [SerializeField] private string finalTransitionPolicy;
        [SerializeField] private string primaryAttemptPolicy;

        [Header("Formal Conditions")]
        [SerializeField] private string[] formalConditionCodes = { "NE", "NR", "SE", "SR" };
        [SerializeField] private ExperimentConditionSequenceDefinition[] conditionSequenceDefinitions = Array.Empty<ExperimentConditionSequenceDefinition>();

        [Header("Tasks and Pilot Options")]
        [SerializeField] private string[] formalTaskIds = { "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance" };
        [SerializeField] private string[] pilotTaskIds = { "restaurant_reservation" };
        [SerializeField] private string[] pilotEmbodimentOptions = { "voice_only", "floating_orb", "humanoid_agent" };
        [SerializeField] private PilotSequenceDefinition[] pilotSequenceDefinitions = Array.Empty<PilotSequenceDefinition>();

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
            ,new ExperimentProtocolDecision { decisionId = "pilot_sequence_mapping", question = "Map pilot a/b/c to voice_only/floating_orb/humanoid_agent.", status = ProtocolDecisionStatus.Unconfirmed }
            ,new ExperimentProtocolDecision { decisionId = "formal_max_turns", question = "Confirm the maximum turns for each formal condition.", status = ProtocolDecisionStatus.Unconfirmed }
            ,new ExperimentProtocolDecision { decisionId = "formal_max_duration", question = "Confirm the maximum duration for each formal condition.", status = ProtocolDecisionStatus.Unconfirmed }
            ,new ExperimentProtocolDecision { decisionId = "pilot_max_turns", question = "Confirm the maximum turns for each pilot condition.", status = ProtocolDecisionStatus.Unconfirmed }
            ,new ExperimentProtocolDecision { decisionId = "pilot_max_duration", question = "Confirm the maximum duration for each pilot condition.", status = ProtocolDecisionStatus.Unconfirmed }
            ,new ExperimentProtocolDecision { decisionId = "questionnaire_scale_anchors", question = "Confirm the wording for all 1-7 questionnaire scale anchors.", status = ProtocolDecisionStatus.Unconfirmed }
        };

        [Header("Auditable Protocol Change Log")]
        [SerializeField] private ExperimentProtocolChange[] changeLog = Array.Empty<ExperimentProtocolChange>();

        public string ProtocolVersion => protocolVersion?.Trim() ?? string.Empty;
        public string BuildVersion => buildVersion?.Trim() ?? string.Empty;
        public ExperimentPhase ExperimentPhase => experimentPhase;
        public bool FormalModeLocked => formalModeLocked;
        public string ProtocolPurpose => protocolPurpose?.Trim() ?? string.Empty;
        public bool ApprovedForCollection => approvedForCollection;
        public string ApprovalAuthority => approvalAuthority?.Trim() ?? string.Empty;
        public string ApprovalEvidence => approvalEvidence?.Trim() ?? string.Empty;
        public string ProtocolSnapshotId => protocolSnapshotId?.Trim() ?? string.Empty;
        public FormalConditionOrderPolicy FormalConditionOrderPolicy => formalConditionOrderPolicy;
        public string FormalTaskAssignmentPolicy => formalTaskAssignmentPolicy?.Trim() ?? string.Empty;
        public GoalConfirmationPolicy GoalConfirmationPolicy => goalConfirmationPolicy;
        public GoalSequencePolicy GoalSequencePolicy => goalSequencePolicy;
        public QuestionnaireReturnPolicy QuestionnaireTransitionPolicy => questionnaireTransitionPolicy;
        public string FinalTransitionPolicy => finalTransitionPolicy?.Trim() ?? string.Empty;
        public string PrimaryAttemptPolicy => primaryAttemptPolicy?.Trim() ?? string.Empty;
        public IReadOnlyList<string> FormalConditionCodes => formalConditionCodes;
        public IReadOnlyList<ExperimentConditionSequenceDefinition> ConditionSequenceDefinitions => conditionSequenceDefinitions;
        public IReadOnlyList<string> PilotEmbodimentOptions => pilotEmbodimentOptions;
        public IReadOnlyList<PilotSequenceDefinition> PilotSequenceDefinitions => pilotSequenceDefinitions;
        public IReadOnlyList<string> FormalTaskIds => formalTaskIds;
        public IReadOnlyList<string> PilotTaskIds => pilotTaskIds;
        public string FeedbackTimingPolicy => feedbackTimingPolicy?.Trim() ?? string.Empty;
        public IReadOnlyList<ExperimentProtocolDecision> RequiredDecisions => requiredDecisions;
        public IReadOnlyList<ExperimentProtocolChange> ChangeLog => changeLog;

        public bool TryGetConfirmedDecision(string decisionId, out string confirmedValue)
        {
            confirmedValue = string.Empty;
            if (requiredDecisions == null || string.IsNullOrWhiteSpace(decisionId)) return false;
            foreach (var decision in requiredDecisions)
            {
                if (decision == null || !string.Equals(decision.decisionId, decisionId, StringComparison.OrdinalIgnoreCase)) continue;
                if (decision.status != ProtocolDecisionStatus.Confirmed) return false;
                confirmedValue = decision.confirmedValue?.Trim() ?? string.Empty;
                return true;
            }
            return false;
        }

        public bool TryResolvePilotDecisions(out PilotFeedbackStyleChoice style, out PilotAudioSourcePolicy audioPolicy, out string error)
        {
            style = PilotFeedbackStyleChoice.Undefined; audioPolicy = PilotAudioSourcePolicy.Undefined;
            var issues = new List<string>();
            if (!TryGetConfirmedDecision("pilot_feedback_style", out var styleValue)) issues.Add("pilot_feedback_style_unconfirmed");
            else if (!PilotProtocolValues.TryParseFeedbackStyle(styleValue, out style)) issues.Add("pilot_feedback_style_invalid");
            if (!TryGetConfirmedDecision("voice_only_spatial_audio", out var audioValue)) issues.Add("voice_only_spatial_audio_unconfirmed");
            else if (!PilotProtocolValues.TryParseAudioPolicy(audioValue, out audioPolicy)) issues.Add("voice_only_spatial_audio_invalid");
            error = string.Join("; ", issues); return issues.Count == 0;
        }

        public bool TryResolveFormalSequences(out AssignmentSequence[] sequences, out string error)
        {
            sequences = Array.Empty<AssignmentSequence>();
            if (!TryGetConfirmedDecision("condition_letter_mapping", out var value)) { error = "condition_letter_mapping_unconfirmed"; return false; }
            if (!TryParseMap(value, new[] { "a", "b", "c", "d" }, new[] { "NE", "NR", "SE", "SR" }, out var map, out error)) return false;
            var letters = new[] { new[] { "a", "b", "c", "d" }, new[] { "b", "c", "d", "a" }, new[] { "c", "d", "a", "b" }, new[] { "d", "a", "b", "c" } };
            sequences = new AssignmentSequence[letters.Length];
            for (var i = 0; i < letters.Length; i++)
            {
                var codes = new FormalConditionCode[4];
                for (var j = 0; j < 4; j++)
                    if (!Enum.TryParse(map[letters[i][j]], true, out codes[j])) { error = "condition_letter_mapping_invalid"; sequences = Array.Empty<AssignmentSequence>(); return false; }
                sequences[i] = new AssignmentSequence { sequenceId = string.Join("-", letters[i]), conditions = codes };
            }
            error = string.Empty; return true;
        }

        public bool TryResolvePilotSequences(out PilotSequenceDefinition[] sequences, out string error)
        {
            sequences = Array.Empty<PilotSequenceDefinition>();
            if (!TryGetConfirmedDecision("pilot_sequence_mapping", out var value)) { error = "pilot_sequence_mapping_unconfirmed"; return false; }
            if (!TryParseMap(value, new[] { "a", "b", "c" }, new[] { "voice_only", "floating_orb", "humanoid_agent" }, out var map, out error)) return false;
            var letters = new[] { new[] { "a", "b", "c" }, new[] { "b", "c", "a" }, new[] { "c", "a", "b" } };
            sequences = new PilotSequenceDefinition[3];
            for (var i = 0; i < letters.Length; i++)
            {
                var values = new PilotEmbodimentCondition[3];
                for (var j = 0; j < 3; j++)
                {
                    var mapped = map[letters[i][j]];
                    values[j] = string.Equals(mapped, "voice_only", StringComparison.OrdinalIgnoreCase) ? PilotEmbodimentCondition.VoiceOnly : string.Equals(mapped, "floating_orb", StringComparison.OrdinalIgnoreCase) ? PilotEmbodimentCondition.FloatingOrb : PilotEmbodimentCondition.HumanoidAgent;
                }
                sequences[i] = new PilotSequenceDefinition { sequenceId = string.Join("-", letters[i]), conditions = values, confirmed = true };
            }
            error = string.Empty; return true;
        }

        public bool ValidateForFormalMode(out string error)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(ProtocolVersion)) issues.Add("protocolVersion is empty");
            if (string.IsNullOrWhiteSpace(BuildVersion)) issues.Add("buildVersion is empty");
            if (!FormalModeLocked) issues.Add("formalModeLocked is false");
            if (!ApprovedForCollection || !string.Equals(ProtocolPurpose, "ParticipantCollection", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(ApprovalAuthority) || string.IsNullOrWhiteSpace(ApprovalEvidence)
                || string.IsNullOrWhiteSpace(ProtocolSnapshotId)) issues.Add("official collection approval metadata is incomplete");
            if (formalConditionOrderPolicy != FormalConditionOrderPolicy.ParticipantChoice
                || formalTaskAssignmentPolicy != "random_bijection_without_replacement"
                || goalConfirmationPolicy != GoalConfirmationPolicy.AutomaticOnValidatedDetection
                || goalSequencePolicy != GoalSequencePolicy.SequentialAfterParticipantTurnAndAvatarReply
                || questionnaireTransitionPolicy != QuestionnaireReturnPolicy.ReturnToModeSelection
                || finalTransitionPolicy != "open_final_ranking_after_four_conditions"
                || primaryAttemptPolicy != "latest_valid_completed_attempt")
                issues.Add("official participant-flow policy is incomplete");
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
                    else if (string.IsNullOrWhiteSpace(decision.confirmedValue) || string.IsNullOrWhiteSpace(decision.confirmedBy)
                        || string.IsNullOrWhiteSpace(decision.confirmedAtUtc) || string.IsNullOrWhiteSpace(decision.evidenceReference))
                        issues.Add($"confirmed protocol decision lacks provenance: {decision.decisionId}");
                }
            }

            error = issues.Count == 0 ? string.Empty : string.Join("; ", issues);
            return issues.Count == 0;
        }

#if UNITY_EDITOR
        public void EditorSetOfficialCollection(string version, string build, string snapshotId,
            ExperimentProtocolDecision[] decisions, ExperimentProtocolChange[] changes)
        {
            protocolVersion = version;
            buildVersion = build;
            experimentPhase = ExperimentPhase.Formal;
            formalModeLocked = true;
            protocolPurpose = "ParticipantCollection";
            approvedForCollection = true;
            approvalAuthority = "ProjectLead";
            approvalEvidence = "formal-editor-collection-directive-v1";
            protocolSnapshotId = snapshotId;
            formalConditionCodes = new[] { "NE", "NR", "SE", "SR" };
            formalTaskIds = new[] { "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance" };
            pilotTaskIds = new[] { "pilot_restaurant_walk_in", "pilot_restaurant_ordering", "pilot_restaurant_wrong_dish" };
            pilotEmbodimentOptions = new[] { "voice_only", "floating_orb", "humanoid_agent" };
            formalConditionOrderPolicy = FormalConditionOrderPolicy.ParticipantChoice;
            formalTaskAssignmentPolicy = "random_bijection_without_replacement";
            goalConfirmationPolicy = GoalConfirmationPolicy.AutomaticOnValidatedDetection;
            goalSequencePolicy = GoalSequencePolicy.SequentialAfterParticipantTurnAndAvatarReply;
            questionnaireTransitionPolicy = QuestionnaireReturnPolicy.ReturnToModeSelection;
            finalTransitionPolicy = "open_final_ranking_after_four_conditions";
            primaryAttemptPolicy = "latest_valid_completed_attempt";
            requiredDecisions = decisions ?? Array.Empty<ExperimentProtocolDecision>();
            changeLog = changes ?? Array.Empty<ExperimentProtocolChange>();
        }
#endif

        private static bool TryParseMap(string value, string[] requiredKeys, string[] allowedValues, out Dictionary<string,string> map, out string error)
        {
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (value ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=', ':' }, 2);
                if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) || string.IsNullOrWhiteSpace(pair[1])) { error = "mapping_syntax_invalid"; return false; }
                if (!map.TryAdd(pair[0].Trim(), pair[1].Trim())) { error = "mapping_key_duplicate"; return false; }
            }
            if (map.Count != requiredKeys.Length) { error = "mapping_keys_invalid"; return false; }
            foreach (var requiredKey in requiredKeys) if (!map.ContainsKey(requiredKey)) { error = "mapping_keys_invalid"; return false; }
            var actual = new HashSet<string>(map.Values, StringComparer.OrdinalIgnoreCase);
            if (actual.Count != allowedValues.Length || allowedValues.Any(valueName => !actual.Contains(valueName))) { error = "mapping_values_invalid"; return false; }
            error = string.Empty; return true;
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
