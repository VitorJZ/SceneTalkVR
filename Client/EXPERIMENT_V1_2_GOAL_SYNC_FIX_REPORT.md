# Experiment v1.2 Goal Synchronization Fix Report

The old failure was upstream of the checkbox: normal participant turns did not call a formal goal evaluator, while `ValidatedRehearsalGoalDetector` was restricted to rehearsal. Consequently `GoalProgressTracker` never changed, the read-only panel had no event to render, and all-goals completion never fired.

The fixed chain is:

```text
STT final participant transcript
 -> SceneTalkOrchestrator final-transcript handler
 -> GoalEvaluationOrchestrator
 -> deterministic rules / structured fallback
 -> GoalProgressTracker.SubmitGoalCandidate
 -> AutomaticOnValidatedDetection => Confirmed
 -> OnGoalStateChanged + OnGoalProgressChanged
 -> ReadOnlyTaskGoalPanel refresh
 -> OnAllGoalsConfirmed
 -> questionnaire lifecycle transition
```

`My name is Harry Potter.` matches the Hotel `reservation_name` name-provision intent without requiring the word “reservation”. The evidence stores turn ID, exact transcript, confidence, evaluator version, reason, confirmation policy, system actor and UTC confirmation time.

The panel subscribes once to `OnGoalStateChanged`, `OnGoalProgressChanged`, `OnGoalCollectionReset` and `OnAllGoalsConfirmed`, filters the current `conditionRunId`, and renders `0 / 4` through `4 / 4 completed`. Reset, Exit and task transitions clear the tracker and UI. The Game View evidence shows `reservation_name` at `1 / 4 completed` immediately after the final-transcript boundary.

Tests cover the requested positive phrases for all 16 intents, unrelated text, Avatar speech, duplicate evidence, fallback success/failure, resume and UI synchronization.
