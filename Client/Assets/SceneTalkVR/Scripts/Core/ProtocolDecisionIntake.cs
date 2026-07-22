using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class ProtocolDecisionIntakeItem
    {
        public string decisionId;
        [TextArea] public string proposedValue;
        public string[] allowedValues = Array.Empty<string>();
        public string confirmedBy;
        public string confirmedAtUtc;
        public string evidenceReference;
        [TextArea] public string notes;
        public string approvalStatus = "Draft";
    }

    [Serializable]
    public sealed class ProtocolDecisionIntakeDocument
    {
        public string schemaVersion = "1.0";
        public string targetProtocolVersion = "1.1.0-stage7";
        public ProtocolDecisionIntakeItem[] decisions = Array.Empty<ProtocolDecisionIntakeItem>();
    }

    [Serializable]
    public sealed class ProtocolDecisionImportChange
    {
        public string decisionId;
        public string previousStatus;
        public string previousValue;
        public string proposedStatus;
        public string proposedValue;
    }

    [Serializable]
    public sealed class ProtocolDecisionImportPreview
    {
        public bool valid;
        public string sourceHash;
        public string targetProtocolVersion;
        public string proposedProtocolVersion;
        public string[] errors = Array.Empty<string>();
        public ProtocolDecisionImportChange[] changes = Array.Empty<ProtocolDecisionImportChange>();
    }

    public static class ProtocolDecisionIntakeValidator
    {
        public static readonly string[] RequiredDecisionIds =
        {
            "condition_letter_mapping", "formal_task_no_replacement", "formal_social_comfort",
            "pilot_feedback_style", "voice_only_spatial_audio", "pilot_sequence_mapping",
            "formal_max_turns", "formal_max_duration", "pilot_max_turns", "pilot_max_duration",
            "questionnaire_scale_anchors"
        };

        public static bool Validate(ProtocolDecisionIntakeDocument document, bool requireApprovedProvenance, out string[] errors)
        {
            var issues = new List<string>();
            if (document == null) { errors = new[] { "document_missing" }; return false; }
            if (document.schemaVersion != "1.0") issues.Add("schema_version_invalid");
            var items = document.decisions ?? Array.Empty<ProtocolDecisionIntakeItem>();
            var groups = items.Where(x => x != null).GroupBy(x => x.decisionId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var id in RequiredDecisionIds)
            {
                var group = groups.FirstOrDefault(x => string.Equals(x.Key, id, StringComparison.OrdinalIgnoreCase));
                if (group == null) { issues.Add($"decision_missing:{id}"); continue; }
                if (group.Count() != 1) { issues.Add($"decision_duplicate:{id}"); continue; }
                ValidateItem(group.First(), requireApprovedProvenance, issues);
            }
            foreach (var group in groups) if (!RequiredDecisionIds.Contains(group.Key, StringComparer.OrdinalIgnoreCase)) issues.Add($"decision_unknown:{group.Key}");
            errors = issues.Distinct().ToArray(); return errors.Length == 0;
        }

        public static bool ValidateValue(string decisionId, string value, out string error)
        {
            value = value?.Trim() ?? string.Empty;
            switch (decisionId)
            {
                case "condition_letter_mapping": return ValidateMapping(value, new[] { "a", "b", "c", "d" }, new[] { "NE", "NR", "SE", "SR" }, out error);
                case "pilot_sequence_mapping": return ValidateMapping(value, new[] { "a", "b", "c" }, new[] { "voice_only", "floating_orb", "humanoid_agent" }, out error);
                case "formal_task_no_replacement": return OneOf(value, new[] { "strict_without_replacement", "with_replacement", "manual" }, out error);
                case "formal_social_comfort": return OneOf(value, new[] { "included", "excluded" }, out error);
                case "pilot_feedback_style": return OneOf(value, new[] { "explicit", "recast" }, out error);
                case "voice_only_spatial_audio": return OneOf(value, new[] { "spatial_fixed_source", "non_spatial_head_locked" }, out error);
                case "formal_max_turns": case "formal_max_duration": case "pilot_max_turns": case "pilot_max_duration":
                    if (string.Equals(value, "unlimited", StringComparison.OrdinalIgnoreCase)) { error = string.Empty; return true; }
                    if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0) { error = string.Empty; return true; }
                    error = "positive_integer_or_unlimited_required"; return false;
                case "questionnaire_scale_anchors":
                    if (value.IndexOf("1 =", StringComparison.OrdinalIgnoreCase) >= 0 && value.IndexOf("7 =", StringComparison.OrdinalIgnoreCase) >= 0 && value.Any(c => c > 127)) { error = string.Empty; return true; }
                    error = "full_bilingual_anchor_text_required"; return false;
                default: error = "unknown_decision_id"; return false;
            }
        }

        private static void ValidateItem(ProtocolDecisionIntakeItem item, bool requireApproved, List<string> issues)
        {
            if (!ValidateValue(item.decisionId, item.proposedValue, out var valueError)) issues.Add($"{item.decisionId}:{valueError}");
            if (requireApproved && !string.Equals(item.approvalStatus, "Approved", StringComparison.OrdinalIgnoreCase)) issues.Add($"{item.decisionId}:approval_required");
            if (requireApproved && (string.IsNullOrWhiteSpace(item.confirmedBy) || string.IsNullOrWhiteSpace(item.confirmedAtUtc) || string.IsNullOrWhiteSpace(item.evidenceReference))) issues.Add($"{item.decisionId}:provenance_required");
            if (requireApproved && !DateTime.TryParse(item.confirmedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _)) issues.Add($"{item.decisionId}:confirmed_at_invalid");
        }

        private static bool ValidateMapping(string value, string[] keys, string[] values, out string error)
        {
            var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=', ':' }, 2);
                if (pair.Length != 2 || !map.TryAdd(pair[0].Trim(), pair[1].Trim())) { error = "mapping_syntax_or_duplicate_key"; return false; }
            }
            if (map.Count != keys.Length || keys.Any(x => !map.ContainsKey(x))) { error = "mapping_keys_invalid"; return false; }
            var actual = new HashSet<string>(map.Values, StringComparer.OrdinalIgnoreCase);
            if (actual.Count != values.Length || values.Any(x => !actual.Contains(x))) { error = "mapping_values_invalid"; return false; }
            error = string.Empty; return true;
        }
        private static bool OneOf(string value, string[] allowed, out string error) { if (allowed.Contains(value, StringComparer.OrdinalIgnoreCase)) { error = string.Empty; return true; } error = "value_not_allowed"; return false; }
    }
}
