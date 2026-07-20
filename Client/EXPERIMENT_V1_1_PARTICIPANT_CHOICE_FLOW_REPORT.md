# Participant-choice Formal Rehearsal report

Baseline branch/HEAD: `experiment-v1.1-integration` at `5344fc12af03a624112f36d98b36cffe433957c7`.

## Result

Formal Rehearsal now presents four feedback-mode cards and never presents task-selection buttons. The session allocator fixes a random, without-replacement condition-to-task bijection at creation. Participant choice determines only execution order. Mapping and selection history persist in `formal_assignment.json`.

Friendly cards map to NE (direct feedback from conversation partner), NR (rephrased feedback from conversation partner), SE (direct feedback from support agent), and SR (rephrased feedback from support agent). Status and double-click guards are derived from the lifecycle assignment.

Questionnaire opening is event-driven from all-goals-confirmed. Submission returns to remaining mode cards; the fourth completion opens final ranking. TechnicalInvalid can retry the same mapped task with a new run identity. Old `1.1-rehearsal-1` assignments fail compatibility instead of being reinterpreted.

## Call chain

`FormalModeSelectionPanel button` → `RehearsalSessionCoordinator.SelectFormalCondition` → `ExperimentLifecycleCoordinator.PrepareCondition` → condition boundary reset → task/goals initialization → `SceneTalkOrchestrator.LoadAssignedTask`.

Dialogue turns call `ValidatedRehearsalGoalDetector.Evaluate` → `GoalProgressTracker.SubmitGoalCandidate` → automatic validated confirmation → `OnAllGoalsConfirmed` → `CompleteTask` → `PauseForQuestionnaireBoundary` → `QuestionnaireRequested` → `QuestionnaireRuntimeController.StartCurrentConditionQuestionnaire`.

## Data isolation

Rehearsal remains `dataOrigin=rehearsal`, `collectionEligible=false`, `developerTestAssignment=false`, and `runQualification=Rehearsal`. The locked Formal/Pilot protocol validation and eleven approved rehearsal decisions are unchanged except for the new rehearsal flow protocol version.

## Known blockers

Four legacy panorama files are square and all five lack native equirectangular provenance sidecars. Panorama collection readiness is therefore blocked. No PICO claim is made. Screenshots and live-cloud dialogue evidence require the operator/gateway environment and are not substituted with synthetic claims.

## Validation evidence

- Unity compile: PASS, zero Console errors.
- Full EditMode: 444/444 PASS.
- Full PlayMode: 28/28 PASS.
- Python analysis: 38/38 PASS.
- Formal Rehearsal Preflight: no blockers; three expected warnings (placeholder avatars, resources not collection-approved, PICO not validated).
- Pilot Rehearsal Preflight: no blockers; two expected warnings (resources not collection-approved, PICO not validated).
- Black-box captures: `QA/RehearsalFlowScreenshots/`.
- Five resource previews: `QA/PanoramaPreviews/`. These are evidence of current rendering only, not proof that the legacy images are valid equirectangular panoramas.
