using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SceneTalkVR.Runtime.Services;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [Serializable]
    public sealed class GoalEvaluationRequest
    {
        public string participantId;
        public string sessionId;
        public string conditionRunId;
        public string taskId;
        public string turnId;
        public string userTranscript;
        public string[] recentUserTurns = Array.Empty<string>();
        public ExperimentTaskGoal[] currentGoalDefinitions = Array.Empty<ExperimentTaskGoal>();
        public string evaluatorVersion;
    }

    [Serializable]
    public sealed class GoalEvaluationItem
    {
        public string goalId;
        public bool achieved;
        public float confidence;
        public string evidence;
        public string reason;
        public string evaluatorVersion;
    }

    [Serializable]
    public sealed class GoalEvaluationResult
    {
        public string taskId;
        public string turnId;
        public GoalEvaluationItem[] evaluations = Array.Empty<GoalEvaluationItem>();
        public bool fallbackRequested;
        public bool fallbackSucceeded;
        public string error;
    }

    public interface IStructuredGoalEvaluationFallback
    {
        bool TryEvaluate(GoalEvaluationRequest request, out GoalEvaluationResult result, out string error);
    }

    public interface IAsyncStructuredGoalEvaluationFallback
    {
        IEnumerator Evaluate(GoalEvaluationRequest request, Action<GoalEvaluationResult> onComplete, Action<string> onError);
    }

    [DisallowMultipleComponent]
    public sealed class StructuredLlmGoalEvaluationFallback : MonoBehaviour, IAsyncStructuredGoalEvaluationFallback
    {
        private void Awake() => GoalEvaluationOrchestrator.AsyncStructuredFallback = this;
        private void OnDestroy()
        {
            if (ReferenceEquals(GoalEvaluationOrchestrator.AsyncStructuredFallback, this))
                GoalEvaluationOrchestrator.AsyncStructuredFallback = null;
        }

        public IEnumerator Evaluate(GoalEvaluationRequest request, Action<GoalEvaluationResult> onComplete, Action<string> onError)
        {
            var service = FindFirstObjectByType<RealLLMService>();
            if (service == null) { onError?.Invoke("structured_goal_llm_service_missing"); yield break; }
            var task = service.GenerateStructuredGoalEvaluationAsync(JsonUtility.ToJson(request));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { onError?.Invoke("structured_goal_llm_failed:" + task.Exception?.GetBaseException().Message); yield break; }
            GoalEvaluationResult result;
            try { result = JsonUtility.FromJson<GoalEvaluationResult>(task.Result); }
            catch (Exception ex) { onError?.Invoke("structured_goal_json_invalid:" + ex.Message); yield break; }
            if (result == null || result.evaluations == null) { onError?.Invoke("structured_goal_schema_invalid"); yield break; }
            result.fallbackSucceeded = true;
            onComplete?.Invoke(result);
        }
    }

    public sealed class GoalAchievementEvaluator
    {
        public const string EvaluatorVersion = "goal_evaluator_v1.2.0";
        private readonly IStructuredGoalEvaluationFallback fallback;

        public GoalAchievementEvaluator(IStructuredGoalEvaluationFallback structuredFallback = null)
        {
            fallback = structuredFallback;
        }

        public GoalEvaluationResult Evaluate(GoalEvaluationRequest request)
        {
            var result = new GoalEvaluationResult
            {
                taskId = request?.taskId ?? string.Empty,
                turnId = request?.turnId ?? string.Empty
            };
            if (request == null || string.IsNullOrWhiteSpace(request.userTranscript))
            {
                result.error = "user_transcript_missing";
                return result;
            }

            var normalized = Normalize(request.userTranscript);
            result.evaluations = (request.currentGoalDefinitions ?? Array.Empty<ExperimentTaskGoal>())
                .Where(goal => goal != null)
                .Select(goal => EvaluateGoal(goal, normalized, request.userTranscript))
                .ToArray();
            result.fallbackRequested = result.evaluations.Any(x => !x.achieved);
            if (result.fallbackRequested && fallback != null)
            {
                if (fallback.TryEvaluate(request, out var fallbackResult, out var fallbackError) && fallbackResult != null)
                {
                    var byGoal = result.evaluations.ToDictionary(x => x.goalId, StringComparer.OrdinalIgnoreCase);
                    foreach (var item in fallbackResult.evaluations ?? Array.Empty<GoalEvaluationItem>())
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.goalId) || !byGoal.TryGetValue(item.goalId, out var existing)
                            || existing.achieved || !item.achieved) continue;
                        item.evaluatorVersion = string.IsNullOrWhiteSpace(item.evaluatorVersion) ? EvaluatorVersion + "+structured_fallback" : item.evaluatorVersion;
                        byGoal[item.goalId] = item;
                    }
                    result.evaluations = byGoal.Values.ToArray();
                    result.fallbackSucceeded = true;
                }
                else result.error = string.IsNullOrWhiteSpace(fallbackError) ? "structured_goal_fallback_failed" : fallbackError;
            }
            return result;
        }

        private static GoalEvaluationItem EvaluateGoal(ExperimentTaskGoal goal, string text, string evidence)
        {
            var matched = MatchesAuthoredPattern(goal, text) || MatchesIntent(goal.evaluationIntent, text);
            return new GoalEvaluationItem
            {
                goalId = goal.goalId ?? string.Empty,
                achieved = matched,
                confidence = matched ? 0.98f : 0f,
                evidence = matched ? evidence : string.Empty,
                reason = matched ? "High-confidence deterministic intent rule matched." : "No deterministic rule matched; structured fallback may evaluate this goal.",
                evaluatorVersion = EvaluatorVersion
            };
        }

        private static bool MatchesAuthoredPattern(ExperimentTaskGoal goal, string text)
        {
            foreach (var pattern in goal.deterministicPatterns ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(pattern) && text.Contains(Normalize(pattern))) return true;
            return false;
        }

        private static bool MatchesIntent(string intent, string text)
        {
            switch ((intent ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "reservation_name": return Any(text, "my name is", "booking is under", "booking under", "booked under", "reservation is under", "reservation under", "under the name", "find it under");
                case "breakfast": return Has(text, "breakfast") && Any(text, "included", "include", "serve", "time", "how much", "cost", "comes with");
                case "higher_floor": return Any(text, "higher floor", "high floor", "upper floor", "room upstairs", "upstairs room");
                case "checkout_time": return Any(text, "what time is checkout", "checkout time", "check out time", "when do i need to check out", "when is check out", "when is checkout");
                case "desk_size": return Any(text, "desk size", "dimensions", "centimeter", "centimetre", "meter wide", "metre wide", "inches wide", "feet wide");
                case "material": return Has(text, "material") || Any(text, "made of", "wood or", "metal or");
                case "budget": return Any(text, "my budget", "maximum budget", "max budget", "spend up to", "price limit", "afford up to");
                case "delivery": return Any(text, "home delivery", "deliver to my home", "deliver it", "delivery available", "do you deliver");
                case "fitness_goal": return Any(text, "my fitness goal", "my goal is", "want to lose weight", "want to build muscle", "get fitter", "improve my fitness", "increase strength");
                case "monthly_price": return Has(text, "month") && Any(text, "price", "cost", "membership", "how much", "fee");
                case "suitable_workout": return Any(text, "workout plan", "training plan", "exercise plan", "routine do you recommend", "plan do you recommend", "workout do you recommend");
                case "trial": return Has(text, "trial") && Any(text, "free", "complimentary", "no charge", "available", "offer");
                case "museum_route": return Has(text, "museum") && Any(text, "how do i get", "how can i get", "directions", "way to", "reach", "go to");
                case "ticket": return Has(text, "ticket") && Any(text, "need", "required", "buy", "admission", "have to");
                case "photography": return Any(text, "take photos", "take pictures", "photography", "photos allowed", "pictures allowed") && Any(text, "inside", "indoor", "allowed", "can i");
                case "nearby_attraction": return Any(text, "another attraction", "nearby attraction", "other place to visit", "recommend nearby", "else should i visit", "nearby place");
                default: return false;
            }
        }

        private static string Normalize(string value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", " ");
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text;
        }
        private static bool Has(string text, string token) => text.IndexOf(token, StringComparison.Ordinal) >= 0;
        private static bool Any(string text, params string[] values) => values.Any(value => Has(text, value));
    }

    public static class GoalEvaluationOrchestrator
    {
        public static IStructuredGoalEvaluationFallback StructuredFallback { get; set; }
        public static IAsyncStructuredGoalEvaluationFallback AsyncStructuredFallback { get; set; }

        public static int EvaluateUserTranscript(ExperimentLifecycleCoordinator lifecycle, string turnId,
            string transcript, string speaker = "participant")
        {
            if (lifecycle?.Assignment == null || lifecycle.CurrentConditionAssignment == null
                || lifecycle.Assignment.flowMode != ExperimentFlowMode.Formal
                || (lifecycle.Assignment.runQualification != ExperimentRunQualification.Rehearsal
                    && lifecycle.Assignment.runQualification != ExperimentRunQualification.Collection)
                || lifecycle.CurrentConditionAssignment.status != ConditionRunStatus.Running
                || lifecycle.TechnicalValidity == ExperimentTechnicalValidity.TechnicalInvalid
                || !string.Equals(speaker, "participant", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(turnId) || string.IsNullOrWhiteSpace(transcript)) return 0;

            var manager = lifecycle.GetComponent<ExperimentConditionManager>();
            var task = manager?.TaskCatalog?.Find(lifecycle.CurrentConditionAssignment.task?.taskId);
            if (task == null) return 0;
            lifecycle.RecordStudyEvent(StudyEventType.UserTranscriptFinalized, "participant", "user_speech_only=true");
            lifecycle.RecordStudyEvent(StudyEventType.GoalEvaluationStarted, "system_goal_evaluator", GoalAchievementEvaluator.EvaluatorVersion);
            var request = new GoalEvaluationRequest
            {
                participantId = lifecycle.Assignment.participantId,
                sessionId = lifecycle.Assignment.experimentSessionId,
                conditionRunId = lifecycle.ConditionRunId,
                taskId = task.taskId,
                turnId = turnId,
                userTranscript = transcript,
                recentUserTurns = lifecycle.RecordFinalUserTranscript(transcript),
                currentGoalDefinitions = task.goals,
                evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion
            };
            var result = new GoalAchievementEvaluator(StructuredFallback).Evaluate(request);
            var confirmed = ApplyResult(lifecycle, task, turnId, transcript, result);
            if (result.fallbackRequested && StructuredFallback == null && AsyncStructuredFallback != null)
            {
                var expectedRun = request.conditionRunId;
                lifecycle.StartCoroutine(AsyncStructuredFallback.Evaluate(request, fallbackResult =>
                {
                    if (lifecycle == null || lifecycle.ConditionRunId != expectedRun
                        || lifecycle.TechnicalValidity == ExperimentTechnicalValidity.TechnicalInvalid) return;
                    ApplyResult(lifecycle, task, turnId, transcript, fallbackResult);
                }, error => lifecycle?.RecordStudyEvent(StudyEventType.GoalEvaluationCompleted,
                    "system_goal_evaluator", "error=" + error)));
            }
            if (!string.IsNullOrWhiteSpace(result.error))
                lifecycle.RecordStudyEvent(StudyEventType.GoalEvaluationCompleted, "system_goal_evaluator", "error=" + result.error);
            return confirmed;
        }

        public static int EvaluatePilotUserTranscript(PilotWorkflowCoordinator pilot, string turnId,
            string transcript, string speaker = "participant")
        {
            if (pilot == null || !pilot.HasActivePilotRun || pilot.Current?.status != PilotRunStatus.Running
                || !string.Equals(speaker, "participant", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(turnId) || string.IsNullOrWhiteSpace(transcript)) return 0;
            var manager = pilot.GetComponent<ExperimentConditionManager>();
            var task = manager?.TaskCatalog?.Find(pilot.Current.task?.taskId);
            if (task == null) return 0;
            var request = new GoalEvaluationRequest
            {
                participantId = pilot.Assignment.participantId,
                sessionId = pilot.Assignment.sessionId,
                conditionRunId = pilot.PilotRunId,
                taskId = task.taskId,
                turnId = turnId,
                userTranscript = transcript,
                recentUserTurns = new[] { transcript },
                currentGoalDefinitions = task.goals,
                evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion
            };
            var result = new GoalAchievementEvaluator(StructuredFallback).Evaluate(request);
            var confirmed = 0;
            foreach (var evaluation in result?.evaluations ?? Array.Empty<GoalEvaluationItem>())
            {
                if (evaluation == null || !evaluation.achieved) continue;
                var definition = task.goals.FirstOrDefault(x => x.goalId == evaluation.goalId);
                var existing = pilot.Goals.Goals.FirstOrDefault(x => x.goalId == evaluation.goalId);
                if (definition == null || existing == null || existing.state == GoalProgressState.Confirmed
                    || evaluation.confidence < definition.minimumConfidence) continue;
                if (pilot.Goals.SubmitGoalCandidate(evaluation.goalId,
                    string.IsNullOrWhiteSpace(evaluation.evaluatorVersion) ? GoalAchievementEvaluator.EvaluatorVersion : evaluation.evaluatorVersion,
                    new GoalEvidence { turnId = turnId, transcript = transcript, confidence = evaluation.confidence,
                        evaluatorVersion = evaluation.evaluatorVersion, evaluationReason = evaluation.reason }, out _)) confirmed++;
            }
            return confirmed;
        }

        private static int ApplyResult(ExperimentLifecycleCoordinator lifecycle, ExperimentTaskDefinition task,
            string turnId, string transcript, GoalEvaluationResult result)
        {
            var confirmed = 0;
            foreach (var evaluation in result?.evaluations ?? Array.Empty<GoalEvaluationItem>())
            {
                if (evaluation == null) continue;
                lifecycle.RecordGoalEvaluationEvent(StudyEventType.GoalEvaluationCompleted, turnId,
                    evaluation.goalId, evaluation.evidence, evaluation.confidence, evaluation.evaluatorVersion, evaluation.reason);
                if (!evaluation.achieved) continue;
                var definition = task.goals.FirstOrDefault(x => x.goalId == evaluation.goalId);
                if (definition == null || evaluation.confidence < definition.minimumConfidence) continue;
                var existing = lifecycle.GoalTracker.Goals.FirstOrDefault(x => x.goalId == evaluation.goalId);
                if (existing == null || existing.state == GoalProgressState.Confirmed) continue;
                if (lifecycle.GoalTracker.SubmitGoalCandidate(evaluation.goalId,
                    string.IsNullOrWhiteSpace(evaluation.evaluatorVersion) ? GoalAchievementEvaluator.EvaluatorVersion + "+structured_llm" : evaluation.evaluatorVersion,
                    new GoalEvidence { turnId = turnId, transcript = transcript, confidence = evaluation.confidence,
                        evaluatorVersion = evaluation.evaluatorVersion, evaluationReason = evaluation.reason }, out _)) confirmed++;
            }
            return confirmed;
        }
    }
}
