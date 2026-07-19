using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum DataIntegritySeverity { Pass, Warning, Fail }

    [Serializable]
    public sealed class DataIntegrityFinding
    {
        public DataIntegritySeverity severity;
        public string checkId;
        public string message;
        public string sourceFile;
        public int sourceLine;
    }

    [Serializable]
    public sealed class SessionDataIntegrityReport
    {
        public string schemaVersion = "1.0";
        public string generatedAtUtc;
        public string participantId;
        public string sessionId;
        public DataIntegritySeverity result;
        public DataIntegrityFinding[] findings = Array.Empty<DataIntegrityFinding>();
    }

    public static class SessionDataIntegrityAuditor
    {
        [Serializable] private sealed class Envelope
        {
            public string participantId; public string sessionId; public string eventType; public string conditionRunId;
            public string questionnaireLinkageKey; public string taskAssignmentId; public string technicalValidity;
            public string conditionStatus; public string questionnaireStatus; public string turnId; public long monotonicElapsedMs;
            public string condition; public string formalConditionCode; public string taskId; public string itemId;
        }

        public static SessionDataIntegrityReport Audit(string directory, string participantId, string sessionId)
        {
            var findings = new List<DataIntegrityFinding>();
            if (!Directory.Exists(directory)) return Build(participantId, sessionId, findings, DataIntegritySeverity.Fail, "session_directory_missing", directory);
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(x => x.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)).ToArray();
            var assignment = files.FirstOrDefault(x => x.IndexOf("assignment", StringComparison.OrdinalIgnoreCase) >= 0);
            Add(findings, assignment == null ? DataIntegritySeverity.Fail : DataIntegritySeverity.Pass, "assignment_exists", assignment == null ? "Assignment snapshot not found." : "Assignment snapshot found.", assignment);
            var runIds = new HashSet<string>(StringComparer.Ordinal);
            var linkageToTask = new Dictionary<string,string>(StringComparer.Ordinal);
            var validSubmissions = new HashSet<string>(StringComparer.Ordinal);
            var eventByTurn = new Dictionary<string,List<Envelope>>(StringComparer.Ordinal);
            foreach (var file in files.Where(x => x.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)))
            {
                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++; if (string.IsNullOrWhiteSpace(line)) continue;
                    Envelope item; try { item = JsonUtility.FromJson<Envelope>(line); } catch { Add(findings, DataIntegritySeverity.Fail, "json_parse", "Invalid JSONL record.", file, lineNumber); continue; }
                    if (item == null) continue;
                    if ((!string.IsNullOrWhiteSpace(item.participantId) && item.participantId != participantId) || (!string.IsNullOrWhiteSpace(item.sessionId) && item.sessionId != sessionId)) Add(findings, DataIntegritySeverity.Fail, "participant_session_consistency", "Record belongs to another participant/session.", file, lineNumber);
                    if (item.eventType == "ConditionStarted" && !string.IsNullOrWhiteSpace(item.conditionRunId) && !runIds.Add(item.conditionRunId)) Add(findings, DataIntegritySeverity.Fail, "condition_run_unique", "Duplicate conditionRunId start.", file, lineNumber);
                    if (!string.IsNullOrWhiteSpace(item.questionnaireLinkageKey) && !string.IsNullOrWhiteSpace(item.taskAssignmentId))
                    {
                        if (linkageToTask.TryGetValue(item.questionnaireLinkageKey, out var task) && task != item.taskAssignmentId) Add(findings, DataIntegritySeverity.Fail, "linkage_task_match", "A linkage key maps to multiple taskAssignmentIds.", file, lineNumber);
                        else linkageToTask[item.questionnaireLinkageKey] = item.taskAssignmentId;
                    }
                    if (item.eventType == "QuestionnaireSubmitted" && string.Equals(item.technicalValidity, "Valid", StringComparison.OrdinalIgnoreCase) && !validSubmissions.Add(item.questionnaireLinkageKey)) Add(findings, DataIntegritySeverity.Fail, "questionnaire_unique_valid_submit", "Duplicate valid questionnaire submission.", file, lineNumber);
                    if (item.eventType == "ConditionCompleted" && string.Equals(item.technicalValidity, "TechnicalInvalid", StringComparison.OrdinalIgnoreCase)) Add(findings, DataIntegritySeverity.Fail, "invalid_not_completed", "TechnicalInvalid condition was recorded completed.", file, lineNumber);
                    if (!string.IsNullOrWhiteSpace(item.turnId)) { if (!eventByTurn.TryGetValue(item.turnId, out var list)) eventByTurn[item.turnId] = list = new List<Envelope>(); list.Add(item); }
                }
            }
            foreach (var pair in eventByTurn)
            {
                for (var i = 1; i < pair.Value.Count; i++) if (pair.Value[i].monotonicElapsedMs < pair.Value[i-1].monotonicElapsedMs) Add(findings, DataIntegritySeverity.Fail, "timing_monotonic", $"Turn {pair.Key} timing regressed.");
                var ordered = pair.Value.OrderBy(x => x.monotonicElapsedMs).ToArray();
                var feedback = Array.FindIndex(ordered, x => x.eventType == "CorrectionPlaybackStarted");
                var dialogue = Array.FindIndex(ordered, x => x.eventType == "DialoguePlaybackStarted");
                if (feedback >= 0 && dialogue >= 0 && feedback > dialogue) Add(findings, DataIntegritySeverity.Fail, "feedback_first", $"Turn {pair.Key} played dialogue before feedback.");
            }
            if (files.Length == 0) Add(findings, DataIntegritySeverity.Warning, "session_files", "No session data files matched; no raw data was modified.", directory);
            if (!findings.Any(x => x.severity == DataIntegritySeverity.Fail)) Add(findings, DataIntegritySeverity.Pass, "read_only", "Audit completed without modifying source data.");
            var result = findings.Any(x => x.severity == DataIntegritySeverity.Fail) ? DataIntegritySeverity.Fail : findings.Any(x => x.severity == DataIntegritySeverity.Warning) ? DataIntegritySeverity.Warning : DataIntegritySeverity.Pass;
            return new SessionDataIntegrityReport { generatedAtUtc = DateTime.UtcNow.ToString("o"), participantId = participantId, sessionId = sessionId, result = result, findings = findings.ToArray() };
        }

        public static void WriteReport(SessionDataIntegrityReport report, string outputPath)
        {
            var parent = Path.GetDirectoryName(outputPath); if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true), Encoding.UTF8);
        }
        private static SessionDataIntegrityReport Build(string participant, string session, List<DataIntegrityFinding> list, DataIntegritySeverity severity, string id, string source) { Add(list,severity,id,id,source); return new SessionDataIntegrityReport{generatedAtUtc=DateTime.UtcNow.ToString("o"),participantId=participant,sessionId=session,result=severity,findings=list.ToArray()}; }
        private static void Add(List<DataIntegrityFinding> list, DataIntegritySeverity severity, string id, string message, string source = "", int line = 0) => list.Add(new DataIntegrityFinding { severity=severity,checkId=id,message=message,sourceFile=source??string.Empty,sourceLine=line });
    }
}
