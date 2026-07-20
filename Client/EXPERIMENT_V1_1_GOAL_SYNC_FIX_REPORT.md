# Goal synchronization fix report

Baseline HEAD: `5344fc12af03a624112f36d98b36cffe433957c7`.

## Root cause

`GoalProgressTracker` previously exposed only `GoalChanged(record, action)`, without participant/session/run/task-assignment identity. `SceneTalkFlowUiController.Update()` rebuilt goal text by polling every frame. No normal participant dialogue path submitted goal candidates, and lifecycle completion was coupled to the legacy generic event. This made stale-run filtering impossible and meant automatic completion did not exist outside QA helpers.

## New authority and data flow

`GoalProgressTracker` is the sole state authority. Each `GoalProgressRecord` now carries `goalId`, `goalText`, `state`, `candidateEvidence`, `candidateAt`, `confirmedAt`, `confirmedBy`, `conditionRunId`, `taskAssignmentId`, and `revision`. Compatibility fields remain export-only.

`ResetGoals(task, GoalTrackingContext)` creates one collection identity and publishes `OnGoalCollectionReset`/`OnGoalProgressChanged`. Candidate, confirm, and reject transitions publish `OnGoalStateChanged` and `OnGoalProgressChanged`; the first transition to all-confirmed publishes `OnAllGoalsConfirmed` once.

`SceneTalkFlowUiController` subscribes and unsubscribes to tracker events. It rejects events from an old `conditionRunId` and renders progress as `confirmed / total`; it contains no participant confirmation controls.

For Formal Rehearsal, `ValidatedRehearsalGoalDetector` evaluates the actual current turn transcript using conservative task-specific phrase contracts. It submits confidence `0.9` evidence with the live turn ID. Automatic confirmation uses actor `system_rehearsal_goal_detector` and policy `automatic_validated_detection`. Generic conversation produces no match. Collection and Developer modes remain `ExperimenterReview`.

The lifecycle observes `OnAllGoalsConfirmed` once, rejects stale or technically invalid events, pauses recording/current coroutine, writes the completion snapshot, enters `AwaitingQuestionnaire`, and requests the questionnaire automatically. Reaching a turn or duration limit without all goals writes `TaskLimitReachedWithoutCompletion`; it no longer fabricates task completion.

## Changed files

- `Assets/SceneTalkVR/Scripts/Core/GoalProgressTracker.cs`
- `Assets/SceneTalkVR/Scripts/Core/ValidatedRehearsalGoalDetector.cs`
- `Assets/SceneTalkVR/Scripts/Core/ExperimentStudyLifecycle.cs`
- `Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`

No collection eligibility or locked Formal/Pilot decision is changed.
