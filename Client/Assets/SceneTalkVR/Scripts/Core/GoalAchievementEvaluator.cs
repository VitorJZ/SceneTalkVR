using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using SceneTalkVR.Runtime;
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

    public enum GoalEvaluatorSource { Deterministic, StructuredLlm }

    public sealed class GoalEvaluationAudit
    {
        public string eventType;
        public GoalEvaluatorSource source;
        public long latencyMs;
        public string goalId;
        public bool achieved;
        public float confidence;
        public string evidence;
        public string reason;
        public string evaluatorVersion;
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
        public const string EvaluatorVersion = "goal_evaluator_v1.2.1";
        public const float SemanticFallbackMinimumConfidence = 0.75f;
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
            var matched = (MatchesAuthoredPattern(goal, text) || MatchesIntent(goal.evaluationIntent, text))
                && !ShouldDeferToSemanticFallback(goal, text);
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
                if (!string.IsNullOrWhiteSpace(pattern) && ContainsPhrase(text, Normalize(pattern))) return true;
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

        public static string NormalizeForEvaluation(string value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant()
                .Replace('\u2018', '\'').Replace('\u2019', '\'').Replace('\u02bc', '\'')
                .Replace('\u201c', '"').Replace('\u201d', '"');
            var contractions = new Dictionary<string, string>
            {
                ["don't"]="do not", ["doesn't"]="does not", ["didn't"]="did not",
                ["can't"]="cannot", ["couldn't"]="could not", ["won't"]="will not",
                ["wouldn't"]="would not", ["isn't"]="is not", ["aren't"]="are not",
                ["wasn't"]="was not", ["weren't"]="were not", ["i'd"]="i would",
                ["i'll"]="i will", ["i'm"]="i am", ["we're"]="we are",
                ["we've"]="we have", ["i've"]="i have", ["that's"]="that is"
            };
            foreach (var pair in contractions)
                text = Regex.Replace(text, @"\b" + Regex.Escape(pair.Key) + @"\b", pair.Value, RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"[\p{P}\p{S}]", " ");
            text = Regex.Replace(text, @"\b(uh|um|er|ah)\b", " ", RegexOptions.CultureInvariant);
            var numbers = new Dictionary<string, string>
            {
                ["one"]="1", ["two"]="2", ["three"]="3", ["four"]="4", ["five"]="5"
            };
            foreach (var pair in numbers)
                text = Regex.Replace(text, @"\b" + pair.Key + @"\b", pair.Value, RegexOptions.CultureInvariant);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
        private static string Normalize(string value) => NormalizeForEvaluation(value);
        private static bool ContainsPhrase(string text, string phrase) => !string.IsNullOrWhiteSpace(phrase)
            && (" " + text + " ").IndexOf(" " + phrase + " ", StringComparison.Ordinal) >= 0;
        private static bool Has(string text, string token) => ContainsPhrase(text, Normalize(token));
        private static bool Any(string text, params string[] values) => values.Any(value => Has(text, value));

        private static bool ShouldDeferToSemanticFallback(ExperimentTaskGoal goal, string text)
        {
            var intent = (goal?.evaluationIntent ?? string.Empty).Trim().ToLowerInvariant();
            if (intent == "wrong_dish" && Any(text, "not the wrong dish", "is not wrong", "correct dish")) return true;
            if (intent == "no_reservation" && Any(text, "do not need a reservation", "do not want a reservation")) return true;
            if (intent == "dietary_restriction" && Any(text, "no dietary restriction", "do not have an allergy")) return true;

            var legitimateNegativeIntent = intent == "no_reservation" || intent == "wrong_dish" || intent == "dietary_restriction";
            var rejection = Any(text, "do not need", "do not want", "not interested", "no thank", "decline");
            var unrelatedPast = Any(text, "last year", "yesterday", "previously", "already used", "used to", "in the past");
            var quoted = Any(text, "he said", "she said", "they said", "my friend said", "told me that", "according to");
            var hypothetical = Any(text, "if i", "if we", "would have", "could have", "hypothetically", "suppose i");
            if (rejection || unrelatedPast || quoted || hypothetical) return true;
            if (!legitimateNegativeIntent && Regex.IsMatch(text, @"\b(no|not|never|cannot|do not|does not|did not)\b")) return true;

            if (intent == "table_availability" && !LooksLikeQuestionOrRequest(text)) return true;
            if (intent == "recommendation" && !Any(text, "recommend", "suggest", "what is good", "what would you", "what do you")) return true;
            if (intent == "delivery" && !Any(text, "do you", "can you", "could you", "is delivery", "delivery available", "deliver", "bring", "send")) return true;
            if (intent == "trial" && !Any(text, "is there", "do you", "can i", "could i", "available", "offer", "try the gym", "test the gym", "trial session")) return true;
            return false;
        }

        private static bool LooksLikeQuestionOrRequest(string text) => Any(text,
            "do you", "does", "is there", "are there", "have you", "can you", "could you",
            "would you", "will you", "may i", "can i", "could i", "any table", "table for");
    }

    public static class GoalEvaluationOrchestrator
    {
        public static IStructuredGoalEvaluationFallback StructuredFallback { get; set; }
        public static IAsyncStructuredGoalEvaluationFallback AsyncStructuredFallback { get; set; }
        private static readonly HashSet<string> StartedTurns = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>> RecentTurnsByRun = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public static bool StartActiveTaskGoalEvaluation(MonoBehaviour coroutineHost,
            ExperimentLifecycleCoordinator lifecycle, PilotWorkflowCoordinator pilot, string turnId,
            string transcript, string speaker = "participant")
        {
            if (coroutineHost == null || !string.Equals(speaker, "participant", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(turnId) || string.IsNullOrWhiteSpace(transcript)) return false;

            if (TryBuildFormalExecution(lifecycle, turnId, transcript, out var formal))
            {
                if (!TryRegisterEvaluationTurn(formal.dedupeKey)) return false;
                formal.request.recentUserTurns = TrackRecent(formal.runKey, transcript);
                lifecycle.RecordStudyEvent(StudyEventType.UserTranscriptFinalized, "participant", "user_speech_only=true");
                coroutineHost.StartCoroutine(EvaluateActiveTaskGoalsAsync(formal.request, lifecycle.GoalTracker,
                    formal.isCurrent, () => IsPlaybackStillRunning(coroutineHost), formal.audit));
                return true;
            }

            if (TryBuildPilotExecution(pilot, turnId, transcript, out var pilotExecution))
            {
                if (!TryRegisterEvaluationTurn(pilotExecution.dedupeKey)) return false;
                pilotExecution.request.recentUserTurns = TrackRecent(pilotExecution.runKey, transcript);
                coroutineHost.StartCoroutine(EvaluateActiveTaskGoalsAsync(pilotExecution.request, pilot.Goals,
                    pilotExecution.isCurrent, () => IsPlaybackStillRunning(coroutineHost), pilotExecution.audit));
                return true;
            }
            return false;
        }

        public static IEnumerator EvaluateActiveTaskGoalsAsync(GoalEvaluationRequest request,
            GoalProgressTracker tracker, Func<bool> identityIsCurrent, Func<bool> playbackStillRunning,
            Action<GoalEvaluationAudit> audit)
        {
            if (request == null || tracker == null || identityIsCurrent == null || !identityIsCurrent()) yield break;
            var pendingDefinitions = (request.currentGoalDefinitions ?? Array.Empty<ExperimentTaskGoal>())
                .Where(x => x != null && tracker.Goals.Any(g => g.goalId == x.goalId && g.state != GoalProgressState.Confirmed))
                .ToArray();
            if (pendingDefinitions.Length == 0) yield break;

            request.currentGoalDefinitions = pendingDefinitions;
            audit?.Invoke(new GoalEvaluationAudit { eventType = "GoalEvaluationStarted", source = GoalEvaluatorSource.Deterministic,
                evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion });
            var deterministicClock = Stopwatch.StartNew();
            GoalEvaluationResult deterministic = null;
            string deterministicError = null;
            try { deterministic = new GoalAchievementEvaluator().Evaluate(request); }
            catch (Exception ex) { deterministicError = "deterministic_goal_evaluation_failed:" + ex.Message; }
            deterministicClock.Stop();
            if (!string.IsNullOrWhiteSpace(deterministicError) || deterministic == null)
            {
                audit?.Invoke(new GoalEvaluationAudit { eventType = "GoalEvaluationFailed", source = GoalEvaluatorSource.Deterministic,
                    latencyMs = deterministicClock.ElapsedMilliseconds, evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion,
                    error = deterministicError ?? "deterministic_goal_result_missing", reason = deterministicError ?? "deterministic_goal_result_missing" });
                yield break;
            }
            foreach (var item in deterministic.evaluations ?? Array.Empty<GoalEvaluationItem>())
                audit?.Invoke(ToAudit("GoalEvaluationCompleted", GoalEvaluatorSource.Deterministic, deterministicClock.ElapsedMilliseconds, item));
            yield return ApplyEvaluations(request, tracker, deterministic, false, identityIsCurrent, playbackStillRunning);
            if (!identityIsCurrent()) yield break;

            var unresolved = pendingDefinitions
                .Where(def => tracker.Goals.Any(g => g.goalId == def.goalId && g.state != GoalProgressState.Confirmed))
                .ToArray();
            if (unresolved.Length == 0) yield break;

            var semanticRequest = CopyRequest(request, unresolved);
            audit?.Invoke(new GoalEvaluationAudit { eventType = "GoalEvaluationStarted", source = GoalEvaluatorSource.StructuredLlm,
                evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion + "+structured_llm" });
            var semanticClock = Stopwatch.StartNew();
            GoalEvaluationResult semanticResult = null;
            string semanticError = null;
            if (AsyncStructuredFallback != null)
            {
                yield return AsyncStructuredFallback.Evaluate(semanticRequest, value => semanticResult = value, value => semanticError = value);
            }
            else if (StructuredFallback != null)
            {
                if (!StructuredFallback.TryEvaluate(semanticRequest, out semanticResult, out semanticError) && string.IsNullOrWhiteSpace(semanticError))
                    semanticError = "structured_goal_fallback_failed";
            }
            else semanticError = "structured_goal_fallback_missing";
            semanticClock.Stop();

            if (!identityIsCurrent()) yield break;
            if (!string.IsNullOrWhiteSpace(semanticError) || semanticResult == null)
            {
                audit?.Invoke(new GoalEvaluationAudit { eventType = "GoalEvaluationFailed", source = GoalEvaluatorSource.StructuredLlm,
                    latencyMs = semanticClock.ElapsedMilliseconds, evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion + "+structured_llm",
                    error = string.IsNullOrWhiteSpace(semanticError) ? "structured_goal_result_missing" : semanticError,
                    reason = string.IsNullOrWhiteSpace(semanticError) ? "structured_goal_result_missing" : semanticError });
                yield break;
            }
            if (!string.Equals(semanticResult.taskId, request.taskId, StringComparison.Ordinal)
                || !string.Equals(semanticResult.turnId, request.turnId, StringComparison.Ordinal))
            {
                audit?.Invoke(new GoalEvaluationAudit { eventType = "GoalEvaluationFailed", source = GoalEvaluatorSource.StructuredLlm,
                    latencyMs = semanticClock.ElapsedMilliseconds, evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion + "+structured_llm",
                    error = "structured_goal_identity_mismatch", reason = "structured_goal_identity_mismatch" });
                yield break;
            }
            foreach (var item in semanticResult.evaluations ?? Array.Empty<GoalEvaluationItem>())
                audit?.Invoke(ToAudit("GoalEvaluationCompleted", GoalEvaluatorSource.StructuredLlm, semanticClock.ElapsedMilliseconds, item));
            yield return ApplyEvaluations(request, tracker, semanticResult, true, identityIsCurrent, playbackStillRunning);
        }

        private sealed class ActiveExecution
        {
            public GoalEvaluationRequest request;
            public string runKey;
            public string dedupeKey;
            public Func<bool> isCurrent;
            public Action<GoalEvaluationAudit> audit;
        }

        private static bool TryBuildFormalExecution(ExperimentLifecycleCoordinator lifecycle, string turnId,
            string transcript, out ActiveExecution execution)
        {
            execution = null;
            var assignment = lifecycle?.Assignment;
            var condition = lifecycle?.CurrentConditionAssignment;
            if (assignment == null || condition == null || assignment.flowMode != ExperimentFlowMode.Formal
                || (assignment.runQualification != ExperimentRunQualification.Rehearsal
                    && assignment.runQualification != ExperimentRunQualification.Collection)
                || condition.status != ConditionRunStatus.Running
                || lifecycle.TechnicalValidity == ExperimentTechnicalValidity.TechnicalInvalid) return false;
            var manager = lifecycle.GetComponent<ExperimentConditionManager>();
            var task = manager?.TaskCatalog?.Find(condition.task?.taskId);
            if (task == null) return false;
            var participant = assignment.participantId ?? string.Empty;
            var session = assignment.experimentSessionId ?? string.Empty;
            var run = lifecycle.ConditionRunId ?? string.Empty;
            var taskId = task.taskId ?? string.Empty;
            var runKey = BuildRunKey("formal", participant, session, run, taskId);
            execution = new ActiveExecution
            {
                runKey = runKey,
                dedupeKey = runKey + "|" + turnId,
                request = new GoalEvaluationRequest
                {
                    participantId = participant, sessionId = session, conditionRunId = run,
                    taskId = taskId, turnId = turnId, userTranscript = transcript,
                    currentGoalDefinitions = IncompleteDefinitions(task, lifecycle.GoalTracker),
                    evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion
                },
                isCurrent = () => lifecycle != null && lifecycle.Assignment != null
                    && lifecycle.CurrentConditionAssignment != null
                    && lifecycle.CurrentConditionAssignment.status == ConditionRunStatus.Running
                    && lifecycle.TechnicalValidity != ExperimentTechnicalValidity.TechnicalInvalid
                    && string.Equals(lifecycle.Assignment.participantId, participant, StringComparison.Ordinal)
                    && string.Equals(lifecycle.Assignment.experimentSessionId, session, StringComparison.Ordinal)
                    && string.Equals(lifecycle.ConditionRunId, run, StringComparison.Ordinal)
                    && string.Equals(lifecycle.CurrentConditionAssignment.task?.taskId, taskId, StringComparison.Ordinal),
                audit = value => RecordFormalAudit(lifecycle, turnId, value)
            };
            return execution.request.currentGoalDefinitions.Length > 0;
        }

        private static bool TryBuildPilotExecution(PilotWorkflowCoordinator pilot, string turnId,
            string transcript, out ActiveExecution execution)
        {
            execution = null;
            var assignment = pilot?.Assignment;
            var condition = pilot?.Current;
            if (assignment == null || condition == null || !pilot.HasActivePilotRun
                || condition.status != PilotRunStatus.Running) return false;
            var manager = pilot.GetComponent<ExperimentConditionManager>();
            var task = manager?.TaskCatalog?.Find(condition.task?.taskId);
            if (task == null) return false;
            var participant = assignment.participantId ?? string.Empty;
            var session = assignment.sessionId ?? string.Empty;
            var run = pilot.PilotRunId ?? string.Empty;
            var taskId = task.taskId ?? string.Empty;
            var runKey = BuildRunKey("pilot", participant, session, run, taskId);
            execution = new ActiveExecution
            {
                runKey = runKey,
                dedupeKey = runKey + "|" + turnId,
                request = new GoalEvaluationRequest
                {
                    participantId = participant, sessionId = session, conditionRunId = run,
                    taskId = taskId, turnId = turnId, userTranscript = transcript,
                    currentGoalDefinitions = IncompleteDefinitions(task, pilot.Goals),
                    evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion
                },
                isCurrent = () => pilot != null && pilot.Assignment != null && pilot.Current != null
                    && pilot.Current.status == PilotRunStatus.Running
                    && string.Equals(pilot.Assignment.participantId, participant, StringComparison.Ordinal)
                    && string.Equals(pilot.Assignment.sessionId, session, StringComparison.Ordinal)
                    && string.Equals(pilot.PilotRunId, run, StringComparison.Ordinal)
                    && string.Equals(pilot.Current.task?.taskId, taskId, StringComparison.Ordinal),
                audit = value => pilot?.RecordGoalEvaluationAudit(turnId, value)
            };
            return execution.request.currentGoalDefinitions.Length > 0;
        }

        private static ExperimentTaskGoal[] IncompleteDefinitions(ExperimentTaskDefinition task, GoalProgressTracker tracker) =>
            (task?.goals ?? Array.Empty<ExperimentTaskGoal>())
                .Where(def => def != null && tracker.Goals.Any(g => g.goalId == def.goalId && g.state != GoalProgressState.Confirmed))
                .ToArray();

        private static string BuildRunKey(string flow, string participant, string session, string run, string task) =>
            string.Join("|", flow, participant, session, run, task);

        public static bool TryRegisterEvaluationTurn(string evaluationIdentity)
        {
            if (string.IsNullOrWhiteSpace(evaluationIdentity)) return false;
            if (StartedTurns.Count > 2048) StartedTurns.Clear();
            return StartedTurns.Add(evaluationIdentity);
        }

        private static string[] TrackRecent(string runKey, string transcript)
        {
            if (!RecentTurnsByRun.TryGetValue(runKey, out var values)) RecentTurnsByRun[runKey] = values = new List<string>();
            values.Add(transcript.Trim());
            while (values.Count > 4) values.RemoveAt(0);
            if (RecentTurnsByRun.Count > 32)
            {
                var active = new HashSet<string>(StartedTurns.Select(x => x.Substring(0, x.LastIndexOf('|'))), StringComparer.Ordinal);
                foreach (var key in RecentTurnsByRun.Keys.Where(x => !active.Contains(x)).Take(RecentTurnsByRun.Count - 32).ToArray())
                    RecentTurnsByRun.Remove(key);
            }
            return values.ToArray();
        }

        private static GoalEvaluationRequest CopyRequest(GoalEvaluationRequest source, ExperimentTaskGoal[] definitions) =>
            new GoalEvaluationRequest
            {
                participantId = source.participantId, sessionId = source.sessionId,
                conditionRunId = source.conditionRunId, taskId = source.taskId, turnId = source.turnId,
                userTranscript = source.userTranscript, recentUserTurns = source.recentUserTurns?.Take(4).ToArray() ?? Array.Empty<string>(),
                currentGoalDefinitions = definitions, evaluatorVersion = GoalAchievementEvaluator.EvaluatorVersion + "+structured_llm"
            };

        private static IEnumerator ApplyEvaluations(GoalEvaluationRequest request, GoalProgressTracker tracker,
            GoalEvaluationResult result, bool semantic, Func<bool> identityIsCurrent, Func<bool> playbackStillRunning)
        {
            foreach (var evaluation in result?.evaluations ?? Array.Empty<GoalEvaluationItem>())
            {
                if (!identityIsCurrent()) yield break;
                if (evaluation == null || !evaluation.achieved || string.IsNullOrWhiteSpace(evaluation.goalId)) continue;
                var definition = request.currentGoalDefinitions.FirstOrDefault(x => x.goalId == evaluation.goalId);
                var existing = tracker.Goals.FirstOrDefault(x => x.goalId == evaluation.goalId);
                if (definition == null || existing == null || existing.state == GoalProgressState.Confirmed) continue;
                var threshold = semantic ? GoalAchievementEvaluator.SemanticFallbackMinimumConfidence : definition.minimumConfidence;
                if (evaluation.confidence < threshold || (semantic && string.IsNullOrWhiteSpace(evaluation.evidence))) continue;
                if (tracker.ConfirmedCount == tracker.Goals.Count - 1 && playbackStillRunning != null)
                    while (identityIsCurrent() && playbackStillRunning()) yield return null;
                if (!identityIsCurrent()) yield break;
                tracker.SubmitGoalCandidate(evaluation.goalId,
                    string.IsNullOrWhiteSpace(evaluation.evaluatorVersion)
                        ? GoalAchievementEvaluator.EvaluatorVersion + (semantic ? "+structured_llm" : string.Empty)
                        : evaluation.evaluatorVersion,
                    new GoalEvidence { turnId = request.turnId, transcript = semantic ? evaluation.evidence : request.userTranscript,
                        confidence = evaluation.confidence, evaluatorVersion = evaluation.evaluatorVersion,
                        evaluationReason = evaluation.reason }, out _);
            }
        }

        private static GoalEvaluationAudit ToAudit(string eventType, GoalEvaluatorSource source, long latencyMs, GoalEvaluationItem item) =>
            new GoalEvaluationAudit
            {
                eventType = eventType, source = source, latencyMs = latencyMs,
                goalId = item?.goalId ?? string.Empty, achieved = item?.achieved == true,
                confidence = item?.confidence ?? 0f, evidence = item?.evidence ?? string.Empty,
                reason = item?.reason ?? string.Empty, evaluatorVersion = item?.evaluatorVersion ?? string.Empty
            };

        private static void RecordFormalAudit(ExperimentLifecycleCoordinator lifecycle, string turnId, GoalEvaluationAudit value)
        {
            if (lifecycle == null || value == null) return;
            var type = value.eventType == "GoalEvaluationStarted" ? StudyEventType.GoalEvaluationStarted
                : value.eventType == "GoalEvaluationFailed" ? StudyEventType.GoalEvaluationFailed
                : StudyEventType.GoalEvaluationCompleted;
            lifecycle.RecordGoalEvaluationEvent(type, turnId, value.goalId, value.evidence, value.confidence,
                value.evaluatorVersion, value.reason, SourceLabel(value.source), value.latencyMs);
        }

        private static bool IsPlaybackStillRunning(MonoBehaviour host) => host is SceneTalkOrchestrator orchestrator && orchestrator.IsTurnRunning;
        public static string SourceLabel(GoalEvaluatorSource source) => source == GoalEvaluatorSource.StructuredLlm ? "structured_llm" : "deterministic";

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
