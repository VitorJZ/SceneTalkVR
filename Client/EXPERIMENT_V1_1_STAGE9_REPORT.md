# SceneTalkVR Experiment v1.1 Stage 9 Report

Stage: Final-SHA Regression Binding, Experiment Matrix Runner, and Research Data Analysis Pipeline
Branch: `experiment-v1.1-integration`
Evidence commit: `060889b3a2deede6654f95adbdf3d77a8d06bec3`
Unity: `6000.3.16f1`, Android active build target
Protocol: `1.1.0-stage7`; Task Catalog: `1.1.0-stage2`; Questionnaire Catalog: `1.1-stage5.1`

## Outcome

Stage 9 binds fresh project regression and Synthetic bundle evidence to the required Stage 8 final SHA, adds deterministic Formal/Pilot matrix execution with isolated evidence, and adds a read-only Python analysis pipeline. It does not approve collection resources or research decisions.

`participantCollectionReady=false`
`releaseCandidateEligible=false`

## Final SHA regression

`ExperimentBuildInfo.asset` was refreshed through the existing Unity Editor before the run. It records commit `060889b3a2deede6654f95adbdf3d77a8d06bec3`, branch `experiment-v1.1-integration`, Unity `6000.3.16f1`, protocol `1.1.0-stage7`, and a real UTC build timestamp. `Stage9FinalShaEvidenceBuilder` refuses stale Unity test results and copies fresh raw/summary evidence only when it matches current Git and BuildInfo.

The SceneTalkVR project test runner excludes UnitySkills package tests. The bound project evidence reports 146 total: EditMode 139/139 and PlayMode 7/7, with zero failures or skips. The Stage 9 files are regenerated from the project runner; no Stage 8 result was merely relabelled. A final rerun result is recorded in `EXPERIMENT_V1_1_STAGE9_VALIDATION.json`.

UnitySkills package tests are intentionally outside the project regression set and are neither counted nor claimed as project tests.

## Matrix implementation

Core types are in `Assets/SceneTalkVR/Scripts/Core/ExperimentMatrix.cs`. Editor menus and export are in `Assets/SceneTalkVR/Scripts/Editor/ExperimentMatrixRunner.cs`. Final-SHA binding is in `Stage9FinalShaEvidenceBuilder.cs`.

The runner supports strong types `Synthetic`, `DeveloperPlaceholder`, and `LockedCollection`. Formal enumeration is exactly 4 conditions × 4 tasks; Pilot enumeration is exactly 3 embodiments × 3 tasks. Every case receives isolated participant/session/run/assignment/questionnaire IDs and its own short physical evidence path, avoiding Windows path-length ambiguity while retaining the full logical case ID.

- Formal Synthetic: 16/16 PASS.
- Pilot Synthetic: 9/9 PASS.
- Locked Formal: 16/16 BLOCKED, zero FAIL/PASS.
- Locked Pilot: 9/9 BLOCKED, zero FAIL/PASS.

Synthetic uses the Stage 8 fake services and deterministic seed. All matrix manifests record `dataOrigin=synthetic_matrix`, `collectionEligible=false`, and `developerTestAssignment=true`. Developer Placeholder records placeholders and remains collection-ineligible. Locked Collection cannot consume Synthetic assignments.

Formal condition interpretation is centralized through `FormalConditionResolver`: NE/NR are Dialogue Avatar with Explicit/Recast; SE/SR are Assistant Agent with Explicit/Recast. Pilot evidence verifies Voice Only creates no visual entity, Floating Orb lifecycle/audio policy, and Humanoid is explicitly a placeholder. Matrix JSON is authoritative; CSV is review-only.

## Locked collection blockers

Formal remains blocked by all 11 Unconfirmed protocol decisions, missing approved formal Avatar presets, approved Voice Profiles, approved deployment profile, and incomplete collection-grade panorama approval. Pilot reports the Pilot-relevant decision IDs plus missing approved Humanoid, Voice Profiles and deployment profile. The validators were not disabled and no default research values were introduced.

The Stage 7 RC gate remains false. No RC tag was created and `main` was not merged.

## Synthetic bundle evidence

Final-SHA Formal and Pilot sessions were regenerated with Session Bundle exports, file checksums and integrity audits. Both integrity audits are PASS and both manifests record `060889b3a2deede6654f95adbdf3d77a8d06bec3`. Evidence is stored beneath `Client/Library/SceneTalkVR/Stage9Evidence/`, outside participant data.

## Analysis pipeline

`Client/Analysis/` is a Python 3.11+ package with a CLI, versioned configuration, exclusion and scale definitions, parsers, independent timing/score derivation, QC, exports, report generation and tests. It requires no database or notebook. Runtime code uses the standard library; pytest is an optional development dependency.

The pipeline reads a bundle manifest first, validates SHA-256/integrity, records input hashes, and confirms source hashes are unchanged after processing. It never writes into a source Bundle. Outputs include sessions, assignments, attempts, turns, condition summaries, goals, questionnaire items, scale scores, rankings, interviews and exclusions. The data dictionary contains 125 fields.

All seven latency fields are recomputed from monotonic Stage 3 events; missing events remain missing. Summary mismatches, non-monotonic events and Feedback First violations become evidence-backed exclusion/QC rows. Questionnaire reverse scores are independently validated against `scale_definitions_v1.json`; exported reverse flags are not trusted blindly.

TechnicalInvalid and Retry attempts remain in `all_attempts.csv`; no source record is replaced. The production template retains `primaryAttemptPolicy=UNCONFIRMED`. Therefore real collection data can produce attempt/QC output but cannot produce a primary analysis dataset until the team approves a policy.

Synthetic data is rejected by default. Only the explicit test configuration enables it, and provenance/eligibility flags remain in output. Transcript text and aggregate interview text are excluded by default; no secrets or device identifiers are exported.

Python regression: 36/36 pytest cases pass. Explicit final-SHA Synthetic validation and analysis succeed for Formal and Pilot. Repeated analysis produces stable content hashes after excluding runtime metadata. These outputs are software fixtures only; no p-values or scientific claims were generated.

## Validation categories

| Category | Result | Meaning |
|---|---|---|
| Final SHA regression | PASS | Evidence bound to `060889b3...` |
| SceneTalkVR project EditMode | PASS | Project tests only |
| SceneTalkVR project PlayMode | PASS | Project tests only |
| UnitySkills package tests | EXCLUDED | Not claimed as project regression |
| Synthetic matrix | PASS | Formal 16/16, Pilot 9/9; ineligible for collection |
| Developer placeholder | PASS as test behavior | Explicitly placeholder and ineligible |
| Locked collection | BLOCKED | Required external approvals/resources absent |
| Python analysis tests | PASS | 36/36 |
| Synthetic analysis | PASS as fixture | No scientific conclusion |
| Real participant analysis | NOT RUN / BLOCKED | No collection-grade data or attempt policy |
| Real service validation | NOT RUN / BLOCKED | Voice Gateway/profile approvals absent |
| PICO validation | NOT RUN / BLOCKED | No build or device claim |

## Files added

- Unity matrix core, Editor runner, SHA evidence builder, EditMode and PlayMode tests.
- Complete `Client/Analysis/` package, configuration, tests and documentation.
- Formal/Pilot Synthetic and Locked matrix outputs, final-SHA test and bundle evidence.
- Matrix/analysis schemas, QC report, data dictionary, analysis model template and sample manifest.

## Known risks and next inputs

The formal Avatar and Pilot Humanoid remain placeholders or absent; Voice Profiles/Gateway and deployment evidence remain unapproved; no PICO build was performed. Eleven protocol decisions remain Unconfirmed, including the primary study mappings and duration/turn limits. Analysis still needs an approved primary-attempt policy and final statistical analysis plan. These are deliberate blockers, not Stage 9 software failures.

The Stage 9 commit containing this report is recorded by Git after submission and is intentionally not written back into the committed report to avoid a self-referential commit loop.
