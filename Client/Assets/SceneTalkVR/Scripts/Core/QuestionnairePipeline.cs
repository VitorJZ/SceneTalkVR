using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class QuestionnaireResponse
    {
        public string schemaVersion = "1.0";
        public string protocolVersion;
        public string questionnaireCatalogVersion;
        public string participantId;
        public string sessionId;
        public string sequenceId;
        public string conditionRunId;
        public string questionnaireLinkageKey;
        public int conditionPosition;
        public int assignmentPolicyEnumValue;
        public string assignmentPolicy;
        public int formalConditionEnumValue;
        public string formalConditionCode;
        public int conditionStatusEnumValue;
        public string conditionStatus;
        public string taskId;
        public string taskAssignmentId;
        public string questionnaireId;
        public string sectionId;
        public string itemId;
        public string itemVersion;
        public string rawValue;
        public float scoredValue;
        public bool hasScoredValue;
        public bool reverseScored;
        public int scaleMin;
        public int scaleMax;
        public bool missing;
        public int revision;
        public string submittedAtUtc;
        public int technicalValidityEnumValue;
        public string technicalValidity;
    }

    [Serializable]
    public sealed class QuestionnaireScoreResult
    {
        public string sectionId;
        public float mean;
        public int answeredCount;
        public int itemCount;
        public bool hasMissing;
    }

    [Serializable]
    public sealed class QuestionnaireSession
    {
        public string schemaVersion = "1.0";
        public string protocolVersion;
        public string questionnaireCatalogVersion;
        public string questionnaireId;
        public string questionnaireVersion;
        public string participantId;
        public string sessionId;
        public string sequenceId;
        public string conditionRunId;
        public string questionnaireLinkageKey;
        public int conditionPosition;
        public AssignmentPolicy assignmentPolicy;
        public FormalConditionCode formalCondition;
        public ConditionRunStatus conditionStatus;
        public string taskId;
        public string taskAssignmentId;
        public ExperimentTechnicalValidity technicalValidity;
        public QuestionnaireCompletionStatus completionStatus;
        public int revision = 1;
        public string previousRevisionId;
        public string startedAtUtc;
        public string submittedAtUtc;
        public int currentPage;
        public QuestionnaireResponse[] responses = Array.Empty<QuestionnaireResponse>();
        public QuestionnaireScoreResult[] sectionScores = Array.Empty<QuestionnaireScoreResult>();
        public float completionRate;
        public bool hasMissing;
    }

    [Serializable]
    public sealed class PreferenceRankEntry
    {
        public int rank;
        public string conditionCode;
        public string embodimentCondition;
    }

    [Serializable]
    public sealed class PreferenceRankingResponse
    {
        public string schemaVersion = "1.0";
        public string protocolVersion;
        public string questionnaireCatalogVersion;
        public string participantId;
        public string sessionId;
        public string sequenceId;
        public string questionnaireId;
        public PreferenceRankEntry[] rankings = Array.Empty<PreferenceRankEntry>();
        public string reason;
        public string submittedAtUtc;

        public bool ValidateUnique(string[] expectedLabels, out string error)
        {
            if (expectedLabels == null || rankings == null || rankings.Length != expectedLabels.Length)
            { error = "ranking_count_invalid"; return false; }
            var ranks = new HashSet<int>(); var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in rankings)
            {
                var label = string.IsNullOrWhiteSpace(entry.conditionCode) ? entry.embodimentCondition : entry.conditionCode;
                if (entry.rank < 1 || entry.rank > expectedLabels.Length || !ranks.Add(entry.rank)) { error = "ranking_rank_duplicate_or_invalid"; return false; }
                if (string.IsNullOrWhiteSpace(label) || !labels.Add(label)) { error = "ranking_label_duplicate_or_missing"; return false; }
            }
            if (!labels.SetEquals(expectedLabels)) { error = "ranking_labels_invalid"; return false; }
            error = string.Empty; return true;
        }
    }

    [Serializable]
    public sealed class InterviewNote
    {
        public string schemaVersion = "1.0";
        public string protocolVersion;
        public string questionnaireCatalogVersion;
        public string participantId;
        public string sessionId;
        public string sequenceId;
        public string interviewerId;
        public string interviewStartedAtUtc;
        public string interviewCompletedAtUtc;
        public string questionId;
        public string responseText;
        public string notes;
    }

    public sealed class QuestionnaireSessionService
    {
        private QuestionnaireCatalog catalog;
        private ExperimentV11ProtocolConfig protocol;
        public QuestionnaireSession ActiveSession { get; private set; }
        public QuestionnaireDefinition Definition { get; private set; }
        public event Action<QuestionnaireSession> SessionChanged;

        public void Configure(QuestionnaireCatalog source, ExperimentV11ProtocolConfig protocolSource)
        { catalog = source; protocol = protocolSource; }

        public bool Begin(QuestionnaireDefinition definition, QuestionnaireSession context, out string error)
        {
            if (catalog == null || definition == null || context == null) { error = "questionnaire_configuration_missing"; return false; }
            if (context.technicalValidity == ExperimentTechnicalValidity.TechnicalInvalid) { error = "technical_invalid_condition"; return false; }
            if (string.IsNullOrWhiteSpace(context.conditionRunId) || string.IsNullOrWhiteSpace(context.questionnaireLinkageKey)) { error = "questionnaire_linkage_missing"; return false; }
            Definition = definition;
            ActiveSession = context;
            ActiveSession.questionnaireId = definition.questionnaireId;
            ActiveSession.questionnaireVersion = definition.questionnaireVersion;
            ActiveSession.questionnaireCatalogVersion = catalog.CatalogVersion;
            ActiveSession.protocolVersion = protocol?.ProtocolVersion ?? context.protocolVersion;
            ActiveSession.startedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            ActiveSession.completionStatus = QuestionnaireCompletionStatus.InProgress;
            ActiveSession.responses ??= Array.Empty<QuestionnaireResponse>();
            SaveDraft(); SessionChanged?.Invoke(ActiveSession); error = string.Empty; return true;
        }

        public bool SetResponse(string itemId, string rawValue, out string error)
        {
            if (ActiveSession == null || Definition == null || ActiveSession.completionStatus == QuestionnaireCompletionStatus.Submitted)
            { error = "questionnaire_not_editable"; return false; }
            var item = catalog.GetEnabledItems(Definition.questionnaireId, protocol).FirstOrDefault(x => x.itemId == itemId);
            if (item == null) { error = "questionnaire_item_not_enabled"; return false; }
            if (item.itemType == QuestionnaireItemType.Likert)
            {
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) || numeric < item.scaleMin || numeric > item.scaleMax)
                { error = "likert_value_out_of_range"; return false; }
            }
            var responses = ActiveSession.responses.ToList();
            var response = responses.FirstOrDefault(x => x.itemId == item.itemId);
            if (response == null) { response = CreateResponse(item); responses.Add(response); }
            response.rawValue = rawValue ?? string.Empty;
            response.missing = string.IsNullOrWhiteSpace(response.rawValue);
            Score(item, response);
            ActiveSession.responses = responses.ToArray();
            RefreshSummary(); SaveDraft(); SessionChanged?.Invoke(ActiveSession); error = string.Empty; return true;
        }

        public bool CanSubmit(out string error)
        {
            if (ActiveSession == null || Definition == null) { error = "questionnaire_not_started"; return false; }
            if (ActiveSession.completionStatus == QuestionnaireCompletionStatus.Submitted) { error = "questionnaire_already_submitted"; return false; }
            foreach (var item in catalog.GetEnabledItems(Definition.questionnaireId, protocol).Where(x => x.required))
            {
                var answer = ActiveSession.responses?.FirstOrDefault(x => x.itemId == item.itemId);
                if (answer == null || string.IsNullOrWhiteSpace(answer.rawValue)) { error = "required_item_missing:" + item.itemId; return false; }
            }
            error = string.Empty; return true;
        }

        public bool Submit(string folder, out string error)
        {
            if (!CanSubmit(out error)) return false;
            ActiveSession.submittedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            ActiveSession.completionStatus = QuestionnaireCompletionStatus.Submitted;
            foreach (var response in ActiveSession.responses) response.submittedAtUtc = ActiveSession.submittedAtUtc;
            RefreshSummary(); QuestionnaireResearchExporter.AppendResponses(folder, ActiveSession);
            SaveDraft(); SessionChanged?.Invoke(ActiveSession); return true;
        }

        public bool Restore(string path, string expectedLinkageKey, string expectedProtocolVersion, string expectedCatalogVersion, out string error)
        {
            if (!File.Exists(path)) { error = "questionnaire_draft_missing"; return false; }
            var restored = JsonUtility.FromJson<QuestionnaireSession>(File.ReadAllText(path));
            if (restored == null || restored.questionnaireLinkageKey != expectedLinkageKey) { error = "questionnaire_linkage_mismatch"; return false; }
            if (restored.protocolVersion != expectedProtocolVersion || restored.questionnaireCatalogVersion != expectedCatalogVersion)
            { restored.completionStatus = QuestionnaireCompletionStatus.Incompatible; error = "questionnaire_version_incompatible"; return false; }
            var definition = catalog?.Find(restored.questionnaireId);
            if (definition == null) { error = "questionnaire_definition_missing"; return false; }
            Definition = definition; ActiveSession = restored; SessionChanged?.Invoke(ActiveSession); error = string.Empty; return true;
        }

        public bool Reopen(string experimenterId, out string error)
        {
            if (ActiveSession == null || ActiveSession.completionStatus != QuestionnaireCompletionStatus.Submitted) { error = "submitted_questionnaire_required"; return false; }
            if (string.IsNullOrWhiteSpace(experimenterId)) { error = "experimenter_identity_required"; return false; }
            var oldRevision = ActiveSession.revision;
            ActiveSession.previousRevisionId = ActiveSession.questionnaireLinkageKey + "-r" + oldRevision;
            ActiveSession.revision = oldRevision + 1;
            ActiveSession.completionStatus = QuestionnaireCompletionStatus.Reopened;
            ActiveSession.submittedAtUtc = string.Empty;
            foreach (var response in ActiveSession.responses) { response.revision = ActiveSession.revision; response.submittedAtUtc = string.Empty; }
            SaveDraft(); SessionChanged?.Invoke(ActiveSession); error = string.Empty; return true;
        }

        public string DraftPath => ActiveSession == null ? string.Empty : Path.Combine(DefaultFolder,
            $"{Safe(ActiveSession.participantId)}_{Safe(ActiveSession.sessionId)}_{Safe(ActiveSession.questionnaireLinkageKey)}_questionnaire_draft.json");
        public static string DefaultFolder => Path.Combine(Application.persistentDataPath, "SceneTalkVR", "ExperimentLogs");

        private QuestionnaireResponse CreateResponse(QuestionnaireItem item) => new QuestionnaireResponse
        {
            protocolVersion = ActiveSession.protocolVersion, questionnaireCatalogVersion = ActiveSession.questionnaireCatalogVersion,
            participantId = ActiveSession.participantId, sessionId = ActiveSession.sessionId, sequenceId = ActiveSession.sequenceId,
            conditionRunId = ActiveSession.conditionRunId, questionnaireLinkageKey = ActiveSession.questionnaireLinkageKey,
            conditionPosition = ActiveSession.conditionPosition, formalConditionEnumValue = (int)ActiveSession.formalCondition,
            assignmentPolicyEnumValue = (int)ActiveSession.assignmentPolicy, assignmentPolicy = ActiveSession.assignmentPolicy.ToString(),
            formalConditionCode = ActiveSession.formalCondition.ToString(), conditionStatusEnumValue = (int)ActiveSession.conditionStatus,
            conditionStatus = ActiveSession.conditionStatus.ToString(), taskId = ActiveSession.taskId,
            taskAssignmentId = ActiveSession.taskAssignmentId, questionnaireId = ActiveSession.questionnaireId,
            sectionId = item.sectionId, itemId = item.itemId, itemVersion = item.itemVersion,
            reverseScored = item.reverseScored, scaleMin = item.scaleMin, scaleMax = item.scaleMax,
            missing = true, revision = ActiveSession.revision, technicalValidityEnumValue = (int)ActiveSession.technicalValidity,
            technicalValidity = ActiveSession.technicalValidity.ToString()
        };

        private static void Score(QuestionnaireItem item, QuestionnaireResponse response)
        {
            response.hasScoredValue = false;
            if (item.itemType != QuestionnaireItemType.Likert || !int.TryParse(response.rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)) return;
            response.scoredValue = item.reverseScored ? item.scaleMax + item.scaleMin - raw : raw;
            response.hasScoredValue = true;
        }

        private void RefreshSummary()
        {
            var enabled = catalog.GetEnabledItems(Definition.questionnaireId, protocol);
            var answered = ActiveSession.responses?.Count(x => !string.IsNullOrWhiteSpace(x.rawValue)) ?? 0;
            ActiveSession.completionRate = enabled.Count == 0 ? 0f : (float)answered / enabled.Count;
            ActiveSession.hasMissing = answered < enabled.Count;
            ActiveSession.sectionScores = enabled.GroupBy(x => x.sectionId).Select(group =>
            {
                var values = ActiveSession.responses?.Where(r => r.sectionId == group.Key && r.hasScoredValue).Select(r => r.scoredValue).ToArray() ?? Array.Empty<float>();
                return new QuestionnaireScoreResult { sectionId = group.Key, mean = values.Length == 0 ? 0f : values.Average(), answeredCount = values.Length, itemCount = group.Count(), hasMissing = values.Length < group.Count() };
            }).ToArray();
        }

        private void SaveDraft()
        {
            try { Directory.CreateDirectory(DefaultFolder); File.WriteAllText(DraftPath, JsonUtility.ToJson(ActiveSession, true), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("[Questionnaire] Draft save failed: " + ex.Message); }
        }
        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }

    public static class QuestionnaireResearchExporter
    {
        public const string CsvHeader = "schemaVersion,protocolVersion,questionnaireCatalogVersion,participantId,sessionId,sequenceId,conditionRunId,questionnaireLinkageKey,conditionPosition,assignmentPolicyEnumValue,assignmentPolicy,formalConditionEnumValue,formalConditionCode,conditionStatusEnumValue,conditionStatus,taskId,taskAssignmentId,questionnaireId,sectionId,itemId,itemVersion,rawValue,scoredValue,reverseScored,scaleMin,scaleMax,missing,revision,submittedAtUtc,technicalValidityEnumValue,technicalValidity";
        public static void AppendResponses(string folder, QuestionnaireSession session)
        {
            Directory.CreateDirectory(folder);
            var stem = $"{session.participantId}_{session.sessionId}_questionnaire_responses_v1";
            var json = Path.Combine(folder, stem + ".jsonl"); var csv = Path.Combine(folder, stem + ".csv");
            if (!File.Exists(csv)) File.WriteAllText(csv, CsvHeader + Environment.NewLine, Encoding.UTF8);
            foreach (var r in session.responses ?? Array.Empty<QuestionnaireResponse>())
            {
                File.AppendAllText(json, JsonUtility.ToJson(r) + Environment.NewLine, Encoding.UTF8);
                File.AppendAllText(csv, string.Join(",", new[] { r.schemaVersion,r.protocolVersion,r.questionnaireCatalogVersion,r.participantId,r.sessionId,r.sequenceId,r.conditionRunId,r.questionnaireLinkageKey,r.conditionPosition.ToString(),r.assignmentPolicyEnumValue.ToString(),r.assignmentPolicy,r.formalConditionEnumValue.ToString(),r.formalConditionCode,r.conditionStatusEnumValue.ToString(),r.conditionStatus,r.taskId,r.taskAssignmentId,r.questionnaireId,r.sectionId,r.itemId,r.itemVersion,r.rawValue,r.hasScoredValue?r.scoredValue.ToString(CultureInfo.InvariantCulture):"",r.reverseScored.ToString(),r.scaleMin.ToString(),r.scaleMax.ToString(),r.missing.ToString(),r.revision.ToString(),r.submittedAtUtc,r.technicalValidityEnumValue.ToString(),r.technicalValidity }.Select(Csv)) + Environment.NewLine, Encoding.UTF8);
            }
        }
        public static void AppendRanking(string folder, PreferenceRankingResponse response)
        { Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, $"{response.participantId}_{response.sessionId}_ranking_v1.jsonl"), JsonUtility.ToJson(response) + Environment.NewLine, Encoding.UTF8); }
        public static void AppendInterview(string folder, InterviewNote note)
        { Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, $"{note.participantId}_{note.sessionId}_interview_v1.jsonl"), JsonUtility.ToJson(note) + Environment.NewLine, Encoding.UTF8); }
        private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
