# Experiment v1.2 Goal Sensitivity Improvement Report

## Baseline and scope

- Branch: `experiment-v1.1-integration`
- Starting commit: `4a28f1273b481cc5e5b9cb6555a52e796adaf7ce`
- Scope was limited to Goal evaluation, a small deterministic-pattern expansion, structured-LLM prompt, minimal evaluation logging, integration call timing, and focused tests.
- Assignment, experiment flow, questionnaire, `GoalProgressTracker`, Goal Panel, Feedback First Gate, Bundle file layout, analysis pipeline, Avatar, Voice, and Panorama were not changed.

## Root causes before the change

1. Formal used deterministic evaluation plus asynchronous structured fallback, while Pilot only used deterministic matching in production.
2. Goal evaluation was invoked after correction/dialogue generation and Avatar playback.
3. TTS or Avatar playback failure could exit the turn before Goal evaluation.
4. Matching was mostly normalized `Contains`, with weak punctuation, contraction, filler, number, word-boundary, negation, and speech-act handling.
5. Formal and Pilot recent-turn and evaluation logging behavior differed.

## Unified Formal and Pilot semantic fallback

`GoalEvaluationOrchestrator.StartActiveTaskGoalEvaluation` now resolves the active Formal or Pilot run and builds the same `GoalEvaluationRequest` pipeline. `EvaluateActiveTaskGoalsAsync`:

1. selects only currently incomplete Goal definitions;
2. runs deterministic evaluation immediately;
3. applies valid deterministic results;
4. sends only still-unresolved Goals to `StructuredLlmGoalEvaluationFallback` / `RealLLMService`;
5. applies structured results only after participant/session/run/task/result-turn identity validation.

Both flows use the same JSON schema, evidence, evaluator version, confidence handling, failure path, and structured service. The only Formal/Pilot difference is the active task's Goal definitions.

## Earlier evaluation timing and stale-result protection

The production call moved to immediately after successful STT final transcript capture and before correction/dialogue generation. The old post-playback calls were removed.

Goal evaluation is a separate coroutine, so correction/dialogue and Feedback First continue without waiting. Playback failure does not cancel an already-started evaluation. A per-run/task/turn key prevents duplicate starts. Before applying an asynchronous result, participant ID, session ID, condition/pilot run ID, task ID, and returned task/turn identity are checked. Reset, Retry, Exit, or condition changes therefore cause stale results to be ignored.

To avoid the last Goal interrupting an in-progress Feedback First turn through the existing automatic questionnaire transition, evaluation may compute immediately but defers only the final state-changing confirmation until the current playback turn has ended.

Formal and Pilot now retain at most four participant-only turns, keyed by flow/participant/session/run/task. History does not cross task or run boundaries.

## Normalize and deterministic matching

`goal_evaluator_v1.2.1` adds:

- Unicode curly quote normalization;
- ordinary Unicode punctuation/symbol removal;
- common contraction expansion;
- removal of `uh`, `um`, `er`, and `ah`;
- one-to-five word/number normalization;
- whitespace normalization;
- token/phrase-boundary matching rather than arbitrary substring matching.

The Task Catalog received only the approved Formal and Pilot pattern additions. `EditorCollectionAssetBuilder` was updated for the Formal patterns so regenerating official assets does not discard them.

## Negation and ambiguity guard

Deterministic auto-confirm now defers rejection, unrelated past-event, quoted-speech, hypothetical/counterfactual, and unsupported keyword-only cases to structured evaluation. Goal-specific handling preserves legitimate negative meanings for `no_reservation`, `wrong_dish`, and `dietary_restriction`.

Focused negative cases no longer auto-confirm:

- `I do not need home delivery.`
- `I already used my free trial last year.`
- `There is no table available.`
- `I do not want a recommendation.`
- `This is not the wrong dish.`

## Structured LLM prompt and threshold

Formal and Pilot share a rewritten strict communication-goal prompt. It accepts paraphrases, synonyms, short answers, different names/numbers, harmless language errors, fillers, STT punctuation errors, and evidence distributed across recent participant turns. It explicitly distinguishes providing, asking, requesting, reporting, restriction, and preference speech acts, and rejects keyword-only, negated, historical, quoted, hypothetical, ambiguous, or contradictory evidence.

Structured auto-confirm uses an evaluator-local `SemanticFallbackMinimumConfidence = 0.75`. `achieved=false`, confidence below 0.75, or empty evidence cannot confirm. Catalog `minimumConfidence` values were not broadly lowered; deterministic matches retain their high confidence.

## Minimal logging

Formal study events and the existing Pilot event JSONL now record `GoalEvaluationStarted`, `GoalEvaluationCompleted`, and `GoalEvaluationFailed`, with evaluator source (`deterministic` or `structured_llm`), latency, Goal ID, achieved, confidence, version, evidence/reason, and error where applicable. No new log file or Bundle file type was introduced.

## Focused validation

- Unity C# compile: PASS, 0 compilation errors.
- New focused EditMode tests: 19/19 PASS, job `aaad050ba041475eaea2be03eef23750`.
- Focused plus existing Formal/Pilot evaluator tests: 70/70 PASS, job `ef96f51952c6493db5aa7b2e7be5b44f`.
- Required deterministic positive/negative Unity probe: 11/11 PASS.
- Formal manual probe: `The reservation should be in Zhang's name.` -> achieved, confidence 0.98.
- Pilot manual probe: `I don't, uh, have a reservation.` -> achieved, confidence 0.98.

Focused tests cover structured fallback for Pilot, incomplete-Goal filtering, semantic threshold, empty evidence, immediate started audit, duplicate-turn prevention, playback failure independence, and stale-result rejection after reset.

Per request, Formal 16/Pilot 9 matrices, full participant session, Bundle audit, Python suite, and PICO validation were not run.

## Modified files

- `Assets/SceneTalkVR/Scripts/Core/GoalAchievementEvaluator.cs`
- `Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Assets/SceneTalkVR/Scripts/Core/ExperimentStudyLifecycle.cs`
- `Assets/SceneTalkVR/Scripts/Core/PilotWorkflowCoordinator.cs`
- `Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs`
- `Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset`
- `Assets/SceneTalkVR/Scripts/Editor/EditorCollectionAssetBuilder.cs`
- `Assets/SceneTalkVR/Tests/Editor/GoalSensitivityFocusedTests.cs`
- `EXPERIMENT_V1_2_GOAL_SENSITIVITY_IMPROVEMENT_REPORT.md`

## Commit

Commit message: `fix(goals): improve formal and pilot semantic goal detection`.

The immutable final commit SHA is recorded by `git rev-parse HEAD` after this report is committed and is included in the delivery response; a Git commit cannot contain its own SHA because changing this file changes that SHA.
