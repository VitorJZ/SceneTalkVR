# Experiment v1.2 Goal Evaluation Schema

`GoalProgressTracker` remains the single state authority. `GoalEvaluationOrchestrator.EvaluateUserTranscript` is called only at the final participant transcript boundary in `SceneTalkOrchestrator`.

## Request

```json
{
  "participantId": "P001",
  "sessionId": "S001",
  "conditionRunId": "cr-...",
  "taskId": "hotel_check_in",
  "turnId": "turn-...",
  "userTranscript": "My name is Harry Potter.",
  "recentUserTurns": [],
  "currentGoalDefinitions": [],
  "evaluatorVersion": "goal-evaluator-1.2"
}
```

## Result

```json
{
  "taskId": "hotel_check_in",
  "turnId": "turn-...",
  "evaluations": [{
    "goalId": "reservation_name",
    "achieved": true,
    "confidence": 0.99,
    "evidence": "My name is Harry Potter.",
    "reason": "The participant explicitly provided a name.",
    "evaluatorVersion": "goal-evaluator-1.2:deterministic"
  }]
}
```

The evaluator first applies task-scoped deterministic intent rules for all 16 formal goals. Only unmatched goals are offered to `IStructuredGoalEvaluationFallback`; the fallback must return typed `GoalEvaluationResult` data. Failure/timeout records an error and never confirms a goal. Avatar/Agent speaker labels, stale runs, technical-invalid runs, duplicate evidence, low confidence, and non-current tasks are rejected.

Formal IDs are: Hotel `reservation_name`, `breakfast`, `higher_floor`, `checkout_time`; Furniture `desk_size`, `material`, `budget`, `delivery`; Gym `fitness_goal`, `monthly_price`, `suitable_workout`, `trial`; Tourist `museum_route`, `ticket`, `photography`, `nearby_attraction`.
