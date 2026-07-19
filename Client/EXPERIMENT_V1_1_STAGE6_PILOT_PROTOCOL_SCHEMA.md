# Experiment v1.1 Stage 6 Pilot Protocol Schema

## Authority and lock rules

Runtime authority is `ExperimentV11Protocol.asset` + `PilotPresentationCatalog.asset` + `ExperimentTaskCatalog.asset` + the Stage 5 `ExperimentQuestionnaireCatalog.asset`. Production Pilot creation uses `PilotAssignmentAllocator.TryCreateLocked`; test mappings are accepted only by `TryCreateForTesting` and are marked `developerTestAssignment`.

Locked Pilot is intentionally blocked until all of the following are supplied by the research team:

- `pilot_feedback_style`: exactly `explicit` or `recast`.
- `voice_only_spatial_audio`: exactly `spatial_fixed_source` or `non_spatial_head_locked`.
- Three confirmed `PilotSequenceDefinition` entries describing `a → b → c`, `b → c → a`, and `c → a → b`, with the research-approved a/b/c-to-embodiment mapping.
- A non-placeholder Humanoid Agent prefab.

No default is assigned for these decisions.

## Strong types and wire labels

| Type | Values / wire labels |
|---|---|
| `PilotEmbodimentCondition` | `VoiceOnly` / `voice_only`; `FloatingOrb` / `floating_orb`; `HumanoidAgent` / `humanoid_agent` |
| `PilotVisualMode` | `None`, `FloatingOrb`, `Humanoid` |
| `PilotFeedbackStyleChoice` | `Undefined`, `Explicit` / `explicit`, `Recast` / `recast` |
| `PilotAudioSourcePolicy` | `Undefined`, `SpatialFixedSource` / `spatial_fixed_source`, `NonSpatialHeadLocked` / `non_spatial_head_locked` |
| `PilotRunStatus` | `Assigned`, `Preparing`, `Running`, `TaskCompleted`, `AwaitingPilotQuestionnaire`, `PilotQuestionnaireInProgress`, `PilotQuestionnaireSubmitted`, `Completed`, `TechnicalInvalid`, `Aborted` |

JSON logs and saved assignments use the string labels alongside the serialized Unity enum fields. Analysis must use the labels.

## Assignment

`PilotAssignment` contains `pilotProtocolVersion`, `pilotAssignmentVersion`, `taskCatalogVersion`, participant/session IDs, `sequenceId`, deterministic `assignmentSeed`, `feedbackStyle`, `voiceOnlyAudioPolicy`, and three `PilotConditionAssignment` records. Each condition contains one unique embodiment, one unique `PilotTaskAssignment`, position, status, run attempt, and latest run ID. A TechnicalInvalid retry increments `runAttempt` and creates a fresh `pilotRunId`.

## Presentation profile

Each `PilotPresentationProfile` contains embodiment, visual mode, actor, shared voice profile, audio policy, source position, spatial blend, distances, volume, speed, subtitle policy, appearance/disappearance delays, prefab key/reference, and a `developerPlaceholder` flag.

The Pilot feedback style belongs to the assignment, not the presentation profile. `BuildCorrectionPlannerContext` includes only the fixed style and shared planner instruction; embodiment is deliberately absent.

## Lifecycle and linkage

`PilotWorkflowCoordinator` owns the lifecycle. Before every condition it calls `ExperimentConditionManager.ResetConditionSessionBoundary`, then clears Pilot presenter/audio, Goal state, questionnaire draft state, timing accumulators, run/linkage IDs, and transient condition state. Questionnaire linkage is `questionnaireLinkageKey = pql-{pilotRunId}` and also includes embodiment, task and taskAssignmentId.

Stage 3 JSONL remains the timing authority. Its compatible extension adds `embodimentCondition` and `pilotRunId`; Pilot JSONL links the same run and derives its latency fields from the observed monotonic Stage 3 events without changing Stage 3 metric definitions.
