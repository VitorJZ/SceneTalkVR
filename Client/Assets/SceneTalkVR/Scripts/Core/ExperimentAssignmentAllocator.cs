using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public sealed class ExperimentAssignmentAllocator
    {
        public const string AssignmentVersion = "1.0";

        public bool TryCreateFormal(
            string participantId,
            string sessionId,
            ExperimentV11ProtocolConfig protocol,
            ExperimentTaskCatalog catalog,
            AssignmentPolicy policy,
            out ExperimentAssignment assignment,
            out string error)
        {
            assignment = null;
            var issues = new List<string>();
            if (protocol == null) issues.Add("protocol_missing");
            if (catalog == null) issues.Add("task_catalog_missing");
            if (string.IsNullOrWhiteSpace(participantId)) issues.Add("participant_id_missing");
            if (policy == AssignmentPolicy.Undefined) issues.Add("task_assignment_policy_unconfirmed");
            if (protocol != null)
            {
                if (!IsDecisionConfirmed(protocol, "condition_letter_mapping")) issues.Add("condition_mapping_unconfirmed");
                if (!IsDecisionConfirmed(protocol, "formal_task_no_replacement")) issues.Add("formal_task_no_replacement_unconfirmed");
                else if (!TryResolveConfirmedPolicy(protocol, out var confirmedPolicy)) issues.Add("formal_task_policy_value_invalid");
                else if (policy != confirmedPolicy) issues.Add("requested_policy_does_not_match_protocol");
            }
            var sequences = Array.Empty<AssignmentSequence>();
            if (protocol != null && !protocol.TryResolveFormalSequences(out sequences, out var sequenceError)) issues.Add(sequenceError);
            if (sequences.Length != 4) issues.Add("four_confirmed_condition_sequences_required");
            if (issues.Count > 0)
            {
                error = string.Join(";", issues);
                return false;
            }
            return TryCreateForTesting(participantId, sessionId, protocol.ProtocolVersion, catalog.CatalogVersion,
                sequences, protocol.FormalTaskIds.ToArray(), policy, out assignment, out error);
        }

        public bool TryCreateForTesting(
            string participantId,
            string sessionId,
            string protocolVersion,
            string catalogVersion,
            AssignmentSequence[] sequences,
            string[] taskIds,
            AssignmentPolicy policy,
            out ExperimentAssignment assignment,
            out string error)
        {
            assignment = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(sessionId)) { error = "participant/session missing"; return false; }
            if (!ValidateSequences(sequences, out error)) return false;
            if (taskIds == null || taskIds.Length == 0) { error = "task list missing"; return false; }
            if (policy == AssignmentPolicy.Undefined) { error = "assignment policy undefined"; return false; }
            if (policy == AssignmentPolicy.Manual) { error = "manual policy requires a pre-authored assignment"; return false; }

            var seed = StableHash($"{participantId}|{protocolVersion}|{AssignmentVersion}");
            var sequenceIndex = (int)(seed % (uint)sequences.Length);
            var sequence = sequences[sequenceIndex];
            var participantRotation = (int)((seed / (uint)sequences.Length) % (uint)taskIds.Length);
            var conditions = new ConditionAssignment[4];
            for (var position = 0; position < conditions.Length; position++)
            {
                var taskIndex = policy == AssignmentPolicy.StrictWithoutReplacement
                    ? (participantRotation + position) % taskIds.Length
                    : (int)(StableHash($"{seed}|{position}") % (uint)taskIds.Length);
                conditions[position] = new ConditionAssignment
                {
                    conditionPosition = position,
                    formalConditionCode = sequence.conditions[position],
                    task = new TaskAssignment
                    {
                        taskId = taskIds[taskIndex],
                        taskAssignmentId = $"ta-{seed:x8}-{position}"
                    },
                    status = ConditionRunStatus.Assigned
                };
            }
            assignment = new ExperimentAssignment
            {
                participantId = participantId.Trim(),
                experimentSessionId = sessionId.Trim(),
                sequenceId = sequence.sequenceId,
                assignmentSeed = seed.ToString("x8"),
                assignmentVersion = AssignmentVersion,
                protocolVersion = protocolVersion ?? string.Empty,
                taskCatalogVersion = catalogVersion ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("o"),
                policy = policy,
                status = AssignmentStatus.Created,
                developerTestAssignment = true,
                conditions = conditions
            };
            return true;
        }

        public static bool IsCompatible(ExperimentAssignment assignment, string protocolVersion, string catalogVersion, out string reason)
        {
            if (assignment == null) { reason = "assignment_missing"; return false; }
            if (!string.Equals(assignment.assignmentVersion, AssignmentVersion, StringComparison.Ordinal)) { reason = "assignment_version_changed"; return false; }
            if (!string.Equals(assignment.protocolVersion, protocolVersion, StringComparison.Ordinal)) { reason = "protocol_version_changed"; return false; }
            if (!string.Equals(assignment.taskCatalogVersion, catalogVersion, StringComparison.Ordinal)) { reason = "task_catalog_version_changed"; return false; }
            reason = string.Empty;
            return true;
        }

        public static bool ValidateAssignment(ExperimentAssignment assignment, ExperimentTaskCatalog catalog, out string error)
        {
            if (assignment?.conditions == null || assignment.conditions.Length != 4) { error = "assignment_requires_four_conditions"; return false; }
            var codes = new HashSet<FormalConditionCode>();
            var tasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < assignment.conditions.Length; i++)
            {
                var item = assignment.conditions[i];
                if (item == null || item.conditionPosition != i || !codes.Add(item.formalConditionCode)) { error = "invalid_condition_assignment"; return false; }
                if (item.task == null || string.IsNullOrWhiteSpace(item.task.taskAssignmentId)
                    || catalog == null || !catalog.TryGetFormal(item.task.taskId, out _)) { error = "invalid_task_assignment"; return false; }
                tasks.Add(item.task.taskId);
            }
            if (assignment.policy == AssignmentPolicy.StrictWithoutReplacement && tasks.Count != 4)
            { error = "strict_without_replacement_task_duplicate"; return false; }
            error = string.Empty;
            return true;
        }

        public static void Save(ExperimentAssignment assignment, string path)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(assignment, true), Encoding.UTF8);
        }

        public static ExperimentAssignment Load(string path) => File.Exists(path)
            ? JsonUtility.FromJson<ExperimentAssignment>(File.ReadAllText(path, Encoding.UTF8)) : null;

        public static string DefaultPath(string participantId, string sessionId) => Path.Combine(
            Application.persistentDataPath, "SceneTalkVR", "Assignments",
            $"{Sanitize(participantId)}_{Sanitize(sessionId)}_assignment_v1.json");


        private static bool IsDecisionConfirmed(ExperimentV11ProtocolConfig protocol, string id)
        {
            foreach (var decision in protocol.RequiredDecisions)
                if (decision != null && string.Equals(decision.decisionId, id, StringComparison.OrdinalIgnoreCase))
                    return decision.status == ProtocolDecisionStatus.Confirmed && !string.IsNullOrWhiteSpace(decision.confirmedValue);
            return false;
        }

        private static bool TryResolveConfirmedPolicy(ExperimentV11ProtocolConfig protocol, out AssignmentPolicy policy)
        {
            policy = AssignmentPolicy.Undefined;
            foreach (var decision in protocol.RequiredDecisions)
            {
                if (decision == null || !string.Equals(decision.decisionId, "formal_task_no_replacement", StringComparison.OrdinalIgnoreCase)) continue;
                var value = (decision.confirmedValue ?? string.Empty).Trim().Replace("-", "_").Replace(" ", "_");
                if (string.Equals(value, "strict_without_replacement", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    policy = AssignmentPolicy.StrictWithoutReplacement;
                else if (string.Equals(value, "with_replacement", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                    policy = AssignmentPolicy.WithReplacement;
                else if (string.Equals(value, "manual", StringComparison.OrdinalIgnoreCase)) policy = AssignmentPolicy.Manual;
                return policy != AssignmentPolicy.Undefined;
            }
            return false;
        }

        private static bool ValidateSequences(AssignmentSequence[] sequences, out string error)
        {
            if (sequences == null || sequences.Length != 4) { error = "exactly four sequences required"; return false; }
            foreach (var sequence in sequences)
            {
                if (sequence == null || string.IsNullOrWhiteSpace(sequence.sequenceId) || sequence.conditions == null || sequence.conditions.Length != 4)
                { error = "invalid sequence"; return false; }
                if (new HashSet<FormalConditionCode>(sequence.conditions).Count != 4)
                { error = "each sequence must contain NE/NR/SE/SR once"; return false; }
            }
            error = string.Empty;
            return true;
        }

        private static uint StableHash(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder();
            foreach (var c in value ?? string.Empty) builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return builder.ToString();
        }
    }
}
