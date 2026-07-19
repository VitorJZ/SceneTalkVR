# Experiment v1.1 Stage 8 — Desktop Dry Run Report

## Baseline and scope

- Branch: `experiment-v1.1-integration`
- Required/base commit and validation commit: `230eb65a2d72106cac4b1cebd50a4517f8eda19a`
- Remote state at start: local HEAD matched `origin/experiment-v1.1-integration`.
- Unity: `6000.3.16f1`; active build target: Android.
- Final Stage 8 commit: the commit containing this report, with subject `feat(experiment): add desktop dry-run and protocol decision intake` (the immutable SHA is also recorded in the delivery response because a commit cannot contain its own SHA).
- Excluded local Unity Skills material: `Client/Packages/manifest.json`, `Client/Packages/packages-lock.json`, and `Client/.agents/skills/`.

Stage 8 does not provide or approve Formal Avatar prefabs, Pilot Humanoid prefab, final Voice Profiles, real Voice Gateway parameters, replacement panoramas, an Android/PICO build, an RC tag, or a `main` merge.

## Baseline verification

Unity MCP/Unity Skills was connected to the already-open Editor; no second Editor was launched. Compilation completed with zero errors and the Console contained zero errors after the Stage 8 scripts were imported. `SampleScene` is the only enabled Build Settings scene. Scene validation found zero missing scripts and zero missing component references. A package-wide prefab scan separately found one missing component in each PICO XR SDK hand prefab (`leftHand.prefab` and `rightHand.prefab`); these are package assets, not objects or references in `SampleScene`.

The actual Preflight report confirms that the runtime config, protocol, Task Catalog, Questionnaire Catalog, Pilot Presentation Catalog, and ExperimentConditionManager bindings are present. It also confirms that Formal remains blocked by all 11 unconfirmed research decisions and unavailable/placeholder Avatar presets. The Stage 7 release manifest remains `releaseCandidateEligible=false`; no RC tag was created.

## Project-only test runner

`SceneTalkVRProjectTestRunner` uses Unity Test Framework APIs and an Editor-only callback. EditMode is filtered to the `SceneTalkVR.Tests.*` namespace. PlayMode is filtered to the `SceneTalkVR.Stage2.PlayModeTests` assembly and `SceneTalkVR.Tests.PlayMode.*` namespace. UnitySkills package tests are neither disabled nor changed; they are outside these filters, so failures in those package-owned tests are not SceneTalkVR project failures.

The runner provides these menu entries:

- `SceneTalkVR/Tests/Run Project EditMode`
- `SceneTalkVR/Tests/Run Project PlayMode`

It persists active run metadata through `SessionState`, saves raw Unity Test Runner XML below `Client/Library/SceneTalkVR/ProjectTestResults/`, and writes the combined deliverables `EXPERIMENT_V1_1_STAGE8_PROJECT_TEST_RESULTS.json` and `.xml`. Each run records assembly, leaf tests, counts, duration, commit, Unity version, and protocol version.

Actual project EditMode result: **132/132 passed, 0 failed, 0 skipped**. Directed suite counts were Task Catalog 25, Feedback First 19, core condition resolution 4, Stage 4 Assignment/Goals 22, Stage 5 Questionnaire 19, Stage 6 Pilot 15, Stage 7 Release Readiness 11, and Stage 8 Desktop Dry Run 17.

After the operator switched UnitySkills to Bypass, project PlayMode ran through the project-only menu: **6/6 passed, 0 failed, 0 skipped** from `SceneTalkVR.Stage2.PlayModeTests.dll`. The combined project result is therefore **138/138 passed**. The six tests cover offline task/menu startup, Feedback First gate/reset, Stage 4 lifecycle/goal panel, Stage 5 questionnaire binding, and Stage 6 Pilot catalog/workflow including Voice Only invisibility.

A separate 10-second `editor_play_capture` entered and exited minimum Play Mode, observed the runtime for 10.024 seconds, and returned `healthy=true`, `errorCount=0`. The project PlayMode test `DeveloperMode_MainMenuAndFourCatalogTasksStartOffline` confirms that the Developer Mode main menu and four Catalog tasks start offline. A second six-second run also had zero runtime errors. Its screenshot arrived after the capture job's initial file-wait timeout and was visually inspected: the SceneTalkVR main menu was visible with `Start`, `Settings`, and `Quit`. The temporary screenshot asset was then removed and is not part of the experiment commit.

## Protocol decision intake

`ProtocolDecisionIntake` defines the exact 11 decision IDs and validates their shape, allowed values, approval status, provenance, and dates. It rejects duplicate or incomplete Formal/Pilot mappings, zero/invalid limits, unsupported policies, and questionnaire anchors without complete bilingual `1 =` and `7 =` text.

`SceneTalkVR/Experiment/Protocol Decision Import` implements:

`Load JSON → validate schema/value/provenance → preview old/new diff → explicit confirmation phrase → backup → transactional write → append change log → increment version → Formal validation → Preflight`.

Preview is the default. Any invalid item blocks all writes. The old serialized protocol is restored on any exception, and a pre-write backup is kept under `Library/SceneTalkVR/ProtocolBackups`. An approved version change does not regenerate old Assignments; existing lifecycle compatibility checks reject their old protocol version.

No team-approved values were supplied in this stage. The intake template remains Draft, every official decision remains Unconfirmed, and `ExperimentV11Protocol.asset` is unchanged.

## SyntheticDryRun isolation and call chain

Every synthetic assignment and event has:

- `dataOrigin = synthetic_dry_run`
- `collectionEligible = false`
- `developerTestAssignment = true`

Formal/Pilot collection lifecycle checks reject developer-test or collection-ineligible assignments. Synthetic records use a test-only protocol version, deterministic Fake STT/Planner/TTS timing, and placeholder visuals. They are written only beneath `Library/SceneTalkVR/.../Synthetic*`, never the participant collection root, and never mutate the official protocol asset.

Formal chain:

`test-only assignment → four condition/task runs → feedback turn → no-feedback turn → goal candidate/confirm/reject → per-condition questionnaire/reverse score → completion/reset ×4 → final ranking → interview → export → read-only audit`.

Pilot chain:

`test-only assignment → TechnicalInvalid old run → retry with new run ID → Voice Only → Floating Orb → Humanoid developer placeholder → questionnaire ×3 → final ranking → export → read-only audit`.

The Editor-only `Desktop Dry Run Console` displays the synthetic warning, identity, protocol, assignment, current condition/task/run, goal/questionnaire/technical state, data path, and integrity result. It supports full Formal/Pilot generation, individual operator rehearsal actions, resume, bundle export through the run engine, and independent audit. It is not included in participant VR UI.

## Data integrity and bundles

`SessionDataIntegrityAuditor` reads exported files without mutation and checks:

- assignment identity/version/sequence/count/unique IDs and synthetic eligibility;
- monotonic timing, Gate closure, Feedback First/no-feedback semantics, and exact summary recomputation;
- condition closure, goal trace, TechnicalInvalid/retry identity, and reset boundaries;
- questionnaire linkage, submitted revisions, required/raw/scored/reverse values and status semantics;
- exact, unique Formal/Pilot rankings after condition completion and Formal interview linkage;
- consistent Pilot feedback hash;
- manifest/checksum integrity and sensitive-material exclusion.

Bundles contain `manifest.json`, `assignment/`, `timing/`, `study/`, `questionnaire/`, `ranking/`, `interview/`, `integrity/`, and `checksums.sha256`. Every exported payload plus `manifest.json` has a SHA-256 entry. Export copies from a separate source root, never overwrites an existing destination, and rejects secret-like material.

Actual synthetic results generated through the Unity menu:

- Formal bundle: integrity **PASS**, four conditions/tasks, four resets, questionnaire/ranking/interview linkage complete.
- Pilot bundle: integrity **PASS**, three embodiments/tasks, Humanoid explicitly a developer placeholder, TechnicalInvalid old log retained, retry uses a new run ID.
- Deliberately corrupted dialogue-before-feedback and missing-questionnaire fixtures produce **FAIL** in automated tests.

## Panorama candidate validation

`SceneTalkVR/Experiment/Validate Selected Panorama Candidate` produces an Editor-only report containing dimensions, exact 2:1 status, seam manual review, import type, max size, mipmaps, Android compression, estimated memory, source/licence placeholder, and `CANDIDATE_NOT_APPROVED`. It never updates the Formal Task Catalog.

## Readiness conclusion

The desktop synthetic software path and project EditMode baseline are ready for continued engineering rehearsal. The project is **not ready for participant collection** and is **not an RC**. Required blockers remain: 11 approved research decisions with evidence, four Formal Avatar presets, Pilot Humanoid, approved Voice Profiles/real gateway configuration, collection-grade panorama decisions, successful project PlayMode/minimum Play Mode after UnitySkills is placed in Bypass, and later Android/PICO/real-service validation.
