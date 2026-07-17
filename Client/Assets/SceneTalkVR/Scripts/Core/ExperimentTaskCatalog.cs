using System;
using System.Collections.Generic;
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
        public Vector3 spawnPosition;
        public Vector3 spawnRotation;
        public bool developerPlaceholderAvatar;
    }

    [CreateAssetMenu(fileName = "ExperimentTaskCatalog", menuName = "SceneTalkVR/Experiment Task Catalog")]
    public sealed class ExperimentTaskCatalog : ScriptableObject
    {
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

        private static void ValidateFormalTask(ExperimentTaskDefinition task, List<string> issues)
        {
            if (string.Equals(task.taskId, "restaurant_reservation", StringComparison.OrdinalIgnoreCase)) issues.Add("Restaurant must be Pilot only");
            if (string.IsNullOrWhiteSpace(task.scenarioId)) issues.Add($"{task.taskId}: scenarioId missing");
            if (string.IsNullOrWhiteSpace(task.context)) issues.Add($"{task.taskId}: context missing");
            if (task.goals == null || task.goals.Length != 4)
            {
                issues.Add($"{task.taskId}: exactly four goals are required");
            }
            else
            {
                foreach (var goal in task.goals)
                {
                    if (goal == null || string.IsNullOrWhiteSpace(goal.text)) issues.Add($"{task.taskId}: goals must be non-empty");
                }
            }
            if (string.IsNullOrWhiteSpace(task.initialQuestion)) issues.Add($"{task.taskId}: initialQuestion missing");
            if (string.IsNullOrWhiteSpace(task.panoramaResourceKey) || Resources.Load<Texture2D>(task.panoramaResourceKey) == null) issues.Add($"{task.taskId}: local panorama missing");
            if (string.IsNullOrWhiteSpace(task.avatarRole)) issues.Add($"{task.taskId}: avatarRole missing");
            if (string.IsNullOrWhiteSpace(task.voiceProfileKey)) issues.Add($"{task.taskId}: voiceProfileKey missing");
            if (string.IsNullOrWhiteSpace(task.roleplayPrompt)) issues.Add($"{task.taskId}: roleplayPrompt missing");
            if (string.IsNullOrWhiteSpace(task.avatarPresetKey) || task.developerPlaceholderAvatar) issues.Add($"{task.taskId}: formal avatar preset is unavailable or placeholder");
        }
    }
}
