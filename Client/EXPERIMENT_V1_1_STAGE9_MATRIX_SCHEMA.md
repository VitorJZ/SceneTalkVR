# Experiment v1.1 Stage 9 Matrix Schema

Schema version: `1.0`

JSON is authoritative; CSV is a flattened review view. Matrix evidence is written only below `Client/Library/SceneTalkVR/Stage9Evidence/`, never to participant collection storage.

## Definitions

- `matrixType`: `Formal` or `Pilot`.
- `executionMode`: `Synthetic`, `DeveloperPlaceholder`, or `LockedCollection`.
- `status`: `PASS`, `FAIL`, `BLOCKED`, or `NOT_RUN` only.
- `PASS`: all software assertions passed.
- `FAIL`: executed software behaviour violated an assertion.
- `BLOCKED`: collection-grade external input or approval is absent; this is not a software failure.
- `NOT_RUN`: the case was not executed.

Formal cases are the Cartesian product of `NE|NR|SE|SR` and the four formal tasks. Pilot cases are the Cartesian product of `voice_only|floating_orb|humanoid_agent` and the three pilot restaurant tasks. `ExperimentMatrixDefinition` is the single enumerator and creates stable, unique case IDs.

## Run manifest

`ExperimentMatrixRunManifest` contains `matrixSchemaVersion`, `matrixRunId`, `matrixType`, `executionMode`, build/catalog versions, UTC start/end, data origin, eligibility, deterministic seed, result counts, and `results`.

Each `ExperimentMatrixCaseResult` contains the required identity and trace fields: `caseId`, `gitCommit`, catalog versions, condition/embodiment/task, status, timestamps, duration, assertion counts, blockers, failures, evidence files, bundle path and integrity status. Each case has independent participant, session, run, assignment and questionnaire-linkage IDs.

## Evidence

`ExperimentMatrixEvidence` records resolved condition/provider/style, task/scenario/panorama, requested and resolved avatar keys, fallback level, feedback hash, actual actor, playback timestamps, feedback-first and no-feedback assertions, goals, questionnaire linkage, reset, timing/study events, validity, and Pilot visual/audio parameters.

Synthetic outputs are always `dataOrigin=synthetic_matrix`, `collectionEligible=false`, and `developerTestAssignment=true`. Developer Placeholder additionally records `placeholderUsed=true`. Locked Collection never consumes a synthetic assignment and reports exact blocker IDs without disabling validators.

## Current authoritative outputs

- `EXPERIMENT_V1_1_STAGE9_FORMAL_MATRIX_RESULTS.json` — 16 Synthetic cases.
- `EXPERIMENT_V1_1_STAGE9_PILOT_MATRIX_RESULTS.json` — 9 Synthetic cases.
- `EXPERIMENT_V1_1_STAGE9_LOCKED_FORMAL_MATRIX.json` — collection blockers.
- `EXPERIMENT_V1_1_STAGE9_LOCKED_PILOT_MATRIX.json` — collection blockers.
