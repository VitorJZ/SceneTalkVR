using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum ExperimentTaskPhase
    {
        Pilot,
        Formal
    }

    [Serializable]
    public sealed class ExperimentTaskGoal
    {
        public string goalId;
        [TextArea] public string text;
        public string evaluationIntent;
        public string[] deterministicPatterns = Array.Empty<string>();
        [TextArea] public string llmCriteria;
        [Range(0f, 1f)] public float minimumConfidence = 0.85f;
    }

    [Serializable]
    public sealed class NonGoalQuestionDefinition
    {
        public string questionId;
        [TextArea] public string text;
    }

    [Serializable]
    public sealed class ExperimentTaskDefinition
    {
        public string taskId;
        public string scenarioId;
        public string displayName;
        public ExperimentTaskPhase phase;
        [TextArea] public string context;
        public ExperimentTaskGoal[] goals;
        [TextArea] public string initialQuestion;
        public string environmentType;
        public string panoramaResourceKey;
        public string avatarPresetKey;
        public string avatarRole;
        public string voiceProfileKey;
        [TextArea] public string roleplayPrompt;
        public NonGoalQuestionDefinition[] nonGoalQuestions = Array.Empty<NonGoalQuestionDefinition>();
        public Vector3 spawnPosition;
        public Vector3 spawnRotation;
        public bool developerPlaceholderAvatar;
    }

    [CreateAssetMenu(fileName = "ExperimentTaskCatalog", menuName = "SceneTalkVR/Experiment Task Catalog")]
    public sealed class ExperimentTaskCatalog : ScriptableObject
    {
        public const int FormalGoalsPerTask = 6;
        public const int PilotGoalsPerTask = 4;

        private static readonly string[] RequiredFormalTaskIds =
        {
            "hotel_check_in",
            "furniture_shopping",
            "gym_membership",
            "tourist_assistance"
        };

        [SerializeField] private string catalogVersion = "1.1.0-stage2";
        [SerializeField] private ExperimentTaskDefinition[] tasks = Array.Empty<ExperimentTaskDefinition>();

        public string CatalogVersion => catalogVersion ?? string.Empty;
        public IReadOnlyList<ExperimentTaskDefinition> Tasks => tasks;

        public List<ExperimentTaskDefinition> GetTasks(ExperimentTaskPhase phase)
        {
            var result = new List<ExperimentTaskDefinition>();
            foreach (var task in tasks)
            {
                if (task != null && task.phase == phase)
                {
                    result.Add(task);
                }
            }
            return result;
        }

        public ExperimentTaskDefinition Find(string taskId)
        {
            foreach (var task in tasks)
            {
                if (task != null && string.Equals(task.taskId, taskId, StringComparison.OrdinalIgnoreCase))
                {
                    return task;
                }
            }
            return null;
        }

        public bool TryGetFormal(string taskId, out ExperimentTaskDefinition task)
        {
            task = Find(taskId);
            return task != null && task.phase == ExperimentTaskPhase.Formal;
        }

        public bool ValidateFormal(ExperimentV11ProtocolConfig protocol, out string error)
        {
            var issues = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var formal = new List<ExperimentTaskDefinition>();

            foreach (var task in tasks)
            {
                if (task == null || string.IsNullOrWhiteSpace(task.taskId) || !ids.Add(task.taskId))
                {
                    issues.Add("taskId is missing or duplicated");
                    continue;
                }
                if (task.phase == ExperimentTaskPhase.Formal)
                {
                    formal.Add(task);
                }
            }

            if (formal.Count != RequiredFormalTaskIds.Length)
            {
                issues.Add("Formal catalog must contain exactly four tasks");
            }
            foreach (var requiredId in RequiredFormalTaskIds)
            {
                if (!formal.Exists(task => string.Equals(task.taskId, requiredId, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add($"Required formal task missing: {requiredId}");
                }
            }

            foreach (var task in formal)
            {
                ValidateFormalTask(task, issues);
            }

            var restaurant = Find("restaurant_reservation");
            if (restaurant != null && restaurant.phase != ExperimentTaskPhase.Pilot)
            {
                issues.Add("Restaurant must be Pilot only");
            }

            if (protocol != null)
            {
                var protocolIds = new HashSet<string>(protocol.FormalTaskIds, StringComparer.OrdinalIgnoreCase);
                foreach (var id in protocolIds)
                {
                    if (!TryGetFormal(id, out _)) issues.Add($"Protocol formal task missing: {id}");
                }
                foreach (var task in formal)
                {
                    if (!protocolIds.Contains(task.taskId)) issues.Add($"Catalog formal task missing from protocol: {task.taskId}");
                }
            }

            error = string.Join("; ", issues);
            return issues.Count == 0;
        }

        public static bool ValidatePilotTasks(IReadOnlyList<ExperimentTaskDefinition> pilot, out string error)
        {
            var issues = new List<string>(); var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (pilot == null || pilot.Count != 3) issues.Add("pilot_catalog_must_contain_three_tasks");
            foreach (var task in pilot ?? Array.Empty<ExperimentTaskDefinition>())
            {
                if (task == null || string.IsNullOrWhiteSpace(task.taskId) || !ids.Add(task.taskId)) issues.Add("pilot_task_id_missing_or_duplicate");
                if (task?.goals == null || task.goals.Length != PilotGoalsPerTask || task.goals.Any(x => x == null || string.IsNullOrWhiteSpace(x.text))) issues.Add((task?.taskId ?? "<null>")+":pilot_goals_invalid");
                if (task != null && (string.IsNullOrWhiteSpace(task.context) || string.IsNullOrWhiteSpace(task.initialQuestion) || string.IsNullOrWhiteSpace(task.roleplayPrompt))) issues.Add(task.taskId+":pilot_text_missing");
                ValidateNonGoalQuestions(task, issues);
                if (task != null && string.IsNullOrWhiteSpace(task.panoramaResourceKey)) issues.Add(task.taskId+":pilot_panorama_missing");
            }
            error=string.Join("; ",issues); return issues.Count==0;
        }

        private static void ValidateFormalTask(ExperimentTaskDefinition task, List<string> issues)
        {
            if (string.Equals(task.taskId, "restaurant_reservation", StringComparison.OrdinalIgnoreCase)) issues.Add("Restaurant must be Pilot only");
            if (string.IsNullOrWhiteSpace(task.scenarioId)) issues.Add($"{task.taskId}: scenarioId missing");
            if (string.IsNullOrWhiteSpace(task.context)) issues.Add($"{task.taskId}: context missing");
            if (task.goals == null || task.goals.Length != FormalGoalsPerTask)
            {
                issues.Add($"{task.taskId}: exactly {FormalGoalsPerTask} goals are required");
            }
            else
            {
                var goalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var goal in task.goals)
                {
                    if (goal == null || string.IsNullOrWhiteSpace(goal.text)) issues.Add($"{task.taskId}: goals must be non-empty");
                    else if (string.IsNullOrWhiteSpace(goal.goalId) || !goalIds.Add(goal.goalId)
                        || string.IsNullOrWhiteSpace(goal.evaluationIntent)
                        || goal.deterministicPatterns == null || goal.deterministicPatterns.Length == 0
                        || string.IsNullOrWhiteSpace(goal.llmCriteria) || goal.minimumConfidence <= 0f)
                        issues.Add($"{task.taskId}: goal evaluation metadata is incomplete");
                }
            }
            if (string.IsNullOrWhiteSpace(task.initialQuestion)) issues.Add($"{task.taskId}: initialQuestion missing");
            if (string.IsNullOrWhiteSpace(task.panoramaResourceKey) || Resources.Load<Texture2D>(task.panoramaResourceKey) == null) issues.Add($"{task.taskId}: local panorama missing");
            if (string.IsNullOrWhiteSpace(task.avatarRole)) issues.Add($"{task.taskId}: avatarRole missing");
            if (string.IsNullOrWhiteSpace(task.voiceProfileKey)) issues.Add($"{task.taskId}: voiceProfileKey missing");
            if (string.IsNullOrWhiteSpace(task.roleplayPrompt)) issues.Add($"{task.taskId}: roleplayPrompt missing");
            ValidateNonGoalQuestions(task, issues);
            if (string.IsNullOrWhiteSpace(task.avatarPresetKey) || task.developerPlaceholderAvatar) issues.Add($"{task.taskId}: formal avatar preset is unavailable or placeholder");
        }

        private static void ValidateNonGoalQuestions(ExperimentTaskDefinition task, List<string> issues)
        {
            if (task == null) return;
            if (task.nonGoalQuestions == null || task.nonGoalQuestions.Length == 0)
            {
                issues.Add($"{task.taskId}: non-goal question bank missing");
                return;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var question in task.nonGoalQuestions)
            {
                if (question == null || string.IsNullOrWhiteSpace(question.questionId)
                    || string.IsNullOrWhiteSpace(question.text) || !ids.Add(question.questionId))
                {
                    issues.Add($"{task.taskId}: non-goal questions must have unique non-empty ids and text");
                }
            }
        }

#if UNITY_EDITOR
        public void EditorSet(string version, ExperimentTaskDefinition[] values)
        {
            catalogVersion = version;
            tasks = values ?? Array.Empty<ExperimentTaskDefinition>();
        }
#endif
    }
}
