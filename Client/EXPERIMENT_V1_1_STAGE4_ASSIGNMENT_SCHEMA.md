# Experiment v1.1 Stage 4 Assignment Schema

Schema implementation: `ExperimentCoreModel.cs` and `ExperimentAssignmentAllocator.cs`. Persistence uses Unity JSON at `Application.persistentDataPath/SceneTalkVR/Assignments/<participant>_<session>_assignment_v1.json`.

## Root `ExperimentAssignment`

| Field | Meaning |
|---|---|
| `participantId`, `experimentSessionId` | Stable participant/session identity |
| `sequenceId` | Protocol sequence selected by stable hash |
| `assignmentSeed` | SHA-256-derived reproducibility token |
| `assignmentVersion` | Allocator schema/algorithm version (`1.0`) |
| `protocolVersion`, `taskCatalogVersion` | Compatibility lock |
| `createdAtUtc` | First creation time; not regenerated on load |
| `policy` | `Undefined`, `StrictWithoutReplacement`, `WithReplacement`, `Manual` |
| `status` | `Created`, `Active`, `Completed`, `Incompatible`, `Aborted` |
| `developerTestAssignment` | Explicit marker; Formal Mode rejects `true` |
| `conditions` | Exactly four `ConditionAssignment` records |

## `ConditionAssignment`

`conditionPosition` (0–3), `formalConditionCode` (NE/NR/SE/SR), `task`, `status`, `latestConditionRunId`, and `runAttempt`. `TaskAssignment` contains `taskId` and immutable `taskAssignmentId`.

Lifecycle values are `Assigned`, `Preparing`, `Running`, `TaskCompleted`, `AwaitingQuestionnaire`, `Completed`, `TechnicalInvalid`, and `Aborted`.

## Determinism and balance

The allocator hashes `participantId|protocolVersion|assignmentVersion`. The sequence and task rotation are derived independently from that stable value. Under the test-only `StrictWithoutReplacement` policy, each task appears exactly once. The cyclic sequence plus participant task rotation balances condition × task pairings over participant cohorts.

## Formal validation

Formal creation requires confirmed `condition_letter_mapping`, confirmed and parseable `formal_task_no_replacement`, exactly four confirmed sequences, a caller policy matching the confirmed protocol policy, and a valid formal Task Catalog. No policy is inferred. Load rejects developer assignments, duplicate/missing conditions, invalid task IDs, StrictWithoutReplacement duplicates, and protocol/catalog/assignment-version drift.

The four cyclic sequences used by automated tests are constructed in test code only; they are never serialized to `ExperimentV11Protocol.asset`.
