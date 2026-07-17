using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum QuestionnaireItemType { Likert, SingleChoice, Ranking, ShortText, LongText, ExperimenterNote }
    public enum QuestionnaireCompletionStatus { NotStarted, InProgress, Submitted, Reopened, Incompatible, Rejected }
    public enum QuestionnaireAudience { FormalCondition, PilotCondition, FormalFinal, PilotFinal, Interview }

    [Serializable]
    public sealed class QuestionnaireItem
    {
        public string questionnaireId;
        public string sectionId;
        public string itemId;
        public string itemVersion = "1.0";
        public int displayOrder;
        [TextArea] public string promptEnglish;
        [TextArea] public string promptChinese;
        public QuestionnaireItemType itemType;
        public bool required = true;
        public bool reverseScored;
        public int scaleMin;
        public int scaleMax;
        public bool enabled = true;
        public string protocolDecisionDependency;
        public string[] choiceValues = Array.Empty<string>();
    }

    [Serializable]
    public sealed class QuestionnaireSection
    {
        public string sectionId;
        public string displayNameEnglish;
        public string displayNameChinese;
        public int displayOrder;
        public QuestionnaireItem[] items = Array.Empty<QuestionnaireItem>();
    }

    [Serializable]
    public sealed class QuestionnaireDefinition
    {
        public string questionnaireId;
        public string questionnaireVersion = "1.0";
        public QuestionnaireAudience audience;
        public bool enabled = true;
        public QuestionnaireSection[] sections = Array.Empty<QuestionnaireSection>();

        public IEnumerable<QuestionnaireItem> Items => (sections ?? Array.Empty<QuestionnaireSection>())
            .Where(x => x != null).SelectMany(x => x.items ?? Array.Empty<QuestionnaireItem>())
            .Where(x => x != null).OrderBy(x => x.displayOrder);
    }

    [CreateAssetMenu(fileName = "ExperimentQuestionnaireCatalog", menuName = "SceneTalkVR/Experiment v1.1 Questionnaire Catalog")]
    public sealed class QuestionnaireCatalog : ScriptableObject
    {
        [SerializeField] private string catalogVersion = "1.1-stage5.1";
        [SerializeField] private QuestionnaireDefinition[] questionnaires = Array.Empty<QuestionnaireDefinition>();

        public string CatalogVersion => catalogVersion?.Trim() ?? string.Empty;
        public IReadOnlyList<QuestionnaireDefinition> Questionnaires => questionnaires;
        public QuestionnaireDefinition Find(string questionnaireId) => questionnaires?.FirstOrDefault(x => x != null &&
            string.Equals(x.questionnaireId, questionnaireId, StringComparison.Ordinal));

        public IReadOnlyList<QuestionnaireItem> GetEnabledItems(string questionnaireId, ExperimentV11ProtocolConfig protocol)
        {
            var definition = Find(questionnaireId);
            if (definition == null || !definition.enabled) return Array.Empty<QuestionnaireItem>();
            return definition.Items.Where(item => IsEnabledByProtocol(item, protocol)).ToArray();
        }

        public bool IsEnabledByProtocol(QuestionnaireItem item, ExperimentV11ProtocolConfig protocol)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(item.protocolDecisionDependency)) return item.enabled;
            return protocol != null && protocol.TryGetConfirmedDecision(item.protocolDecisionDependency, out var value)
                && IsAffirmative(value);
        }

        public bool ValidateFormal(ExperimentV11ProtocolConfig protocol, out string error)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(CatalogVersion)) issues.Add("questionnaire_catalog_version_missing");
            var formal = Find("formal_condition_v1");
            if (formal == null || !formal.enabled) issues.Add("formal_questionnaire_missing");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in questionnaires ?? Array.Empty<QuestionnaireDefinition>())
            foreach (var item in definition?.Items ?? Array.Empty<QuestionnaireItem>())
            {
                if (string.IsNullOrWhiteSpace(item.itemId) || !ids.Add(item.itemId)) issues.Add("item_id_missing_or_duplicate:" + item.itemId);
                if (string.IsNullOrWhiteSpace(item.itemVersion)) issues.Add("item_version_missing:" + item.itemId);
                if (string.IsNullOrWhiteSpace(item.promptEnglish) || string.IsNullOrWhiteSpace(item.promptChinese)) issues.Add("bilingual_prompt_missing:" + item.itemId);
                if (item.itemType == QuestionnaireItemType.Likert && (item.scaleMin >= item.scaleMax || item.scaleMin == 0)) issues.Add("scale_range_invalid:" + item.itemId);
                if (item.reverseScored && item.itemType != QuestionnaireItemType.Likert) issues.Add("reverse_score_non_likert:" + item.itemId);
                if (item.required && !item.enabled && string.IsNullOrWhiteSpace(item.protocolDecisionDependency)) issues.Add("required_item_disabled:" + item.itemId);
            }
            var formalItems = GetEnabledItems("formal_condition_v1", protocol);
            if (formalItems.Count(x => x.sectionId == "role_clarity") != 2) issues.Add("formal_role_clarity_count");
            if (formalItems.Count(x => x.sectionId == "conversation_continuity") != 3) issues.Add("formal_conversation_continuity_count");
            if (formalItems.Count(x => x.sectionId == "interest_enjoyment") != 5) issues.Add("formal_interest_enjoyment_count");
            if (formalItems.Count(x => x.sectionId == "pressure_tension") != 2) issues.Add("formal_pressure_tension_count");
            if (formalItems.Count(x => x.sectionId == "learning_support") != 4) issues.Add("formal_learning_support_count");
            if (protocol == null || !protocol.TryGetConfirmedDecision("formal_social_comfort", out var socialValue))
            {
                if (formalItems.Any(x => x.sectionId == "social_comfort")) issues.Add("social_comfort_enabled_without_decision");
            }
            else if (IsAffirmative(socialValue) && !formal.Items.Any(x => x.sectionId == "social_comfort")) issues.Add("social_comfort_items_missing");
            ValidateRanking(Find("formal_final_v1"), new[] { "NE", "NR", "SE", "SR" }, "formal_ranking", issues);
            ValidateRanking(Find("pilot_final_v1"), new[] { "voice_only", "floating_orb", "humanoid_agent" }, "pilot_ranking", issues);
            error = string.Join("; ", issues.Distinct());
            return issues.Count == 0;
        }

        private static void ValidateRanking(QuestionnaireDefinition definition, string[] expected, string label, List<string> issues)
        {
            var ranking = definition?.Items.FirstOrDefault(x => x.itemType == QuestionnaireItemType.Ranking);
            if (ranking == null || ranking.choiceValues == null || !new HashSet<string>(ranking.choiceValues).SetEquals(expected)) issues.Add(label + "_invalid");
        }

        private static bool IsAffirmative(string value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "include", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "enabled", StringComparison.OrdinalIgnoreCase);

#if UNITY_EDITOR
        public void EditorSet(string version, QuestionnaireDefinition[] values)
        {
            catalogVersion = version;
            questionnaires = values ?? Array.Empty<QuestionnaireDefinition>();
        }
#endif
    }
}
