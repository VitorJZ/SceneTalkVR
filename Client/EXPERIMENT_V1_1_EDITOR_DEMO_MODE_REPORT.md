# Experiment v1.1 Editor Formal/Pilot Demonstration Mode

## Outcome

SceneTalkVR now supports two isolated Editor-only modes: `EditorDemoFormal` and `EditorDemoPilot`. Both reuse the existing assignment, condition/Pilot lifecycle, fixed Task Catalog, Goal Tracking, Feedback First, questionnaires, ranking/export, reset, and integrity code paths. No second simplified experiment lifecycle was introduced.

The formal protocol asset was not edited. Its 11 decisions remain Unconfirmed; `participantCollectionReady=false` and `releaseCandidateEligible=false`.

## Main implementation

- `EditorDemoModel.cs`, `ExperimentV11EditorDemoProtocol.cs`: Demo-only protocol types and `DemoApproved` decisions.
- `EditorDemoSessionCoordinator.cs`: start/resume/prepare/reset, demo-operator goal/questionnaire/ranking/interview actions, isolated persistence, and assignment identity.
- `SceneTalkTeamShowcaseWindow.cs`: `SceneTalkVR → Demo → Team Showcase Control` with Formal/Pilot start/resume, lifecycle, retry/invalid, ranking, export, audit, reset, and return controls.
- `EditorDemoPreflight.cs`: independent `DEMO_READY`, `DEMO_WARNING`, `DEMO_BLOCKED` results without collection-ready terminology.
- `EditorDemoBundleExporter.cs`: canonical event Bundle, checksums, and integrity report.
- `SceneTalkFlowUiController.cs`: persistent non-participant banner, status panel, and Demo-only final-ranking preview.
- `CorrectionFeedbackPresenter.cs`, `PilotEmbodimentPresenter.cs`: Pilot owns embodiment visibility; Orb does not leak into Voice Only/Humanoid or across resets.
- `ExperimentConditionManager.cs`: Editor Pilot resolves Pilot Task Catalog entries, preventing formal-task fallback.
- `Client/Analysis`: Editor Demo is denied by default and accepted only under explicit test-only configuration.

## Demo protocol

Version `1.1-editor-demo-v1`, purpose `EditorDemonstration`, `researchApproved=false`, `collectionEligible=false`. The 11 fixed demonstration values are listed in `EXPERIMENT_V1_1_EDITOR_DEMO_PROTOCOL_MANIFEST.json`. Each carries `DemoApproved`, the required evidence string, and “Not approved for research collection.”

## Resources

Formal Demo uses explicit current-catalog placeholders: Hotel `barista_humanoid_v1`, Furniture `teacher_humanoid_v1`, Gym `barista_male_humanoid_v1`, Tourist `teacher_female_humanoid_v1`. Pilot Humanoid uses `teacher_female_humanoid_v1`; Orb uses `generated_orb_v1`; Voice Only creates no visual. All are non-collection resources.

Voice catalog: Tencent voice `101050`, `en-US`, 24 kHz, profiles `editor_demo_dialogue_voice` and shared `editor_demo_feedback_voice`; both `approvedForEditorDemo=true`, `approvedForCollection=false`. Deployment: `EditorDemo`, UnityEditor loopback `127.0.0.1:8787`, `collectionAllowed=false`.

## Validation

- Compile: PASS, zero C# errors.
- Unity EditMode: 400/400 PASS.
- Unity PlayMode: 22/22 PASS.
- Python: 38/38 PASS.
- Minimal Play Mode: 10 seconds completed; final project Console 0 errors.
- Formal Demo Preflight: `DEMO_WARNING`, zero blockers.
- Pilot Demo Preflight: `DEMO_WARNING`, zero blockers.
- Actual Formal flow: four conditions/tasks, four questionnaires, ranking, interview, Bundle PASS.
- Actual Pilot flow: three restaurant tasks/embodiments, three questionnaires, ranking, Bundle PASS.
- Default analysis: Demo rejected; explicit test-only analysis succeeds without primary analysis; source hashes unchanged.

The Unity Skills server emitted a package-internal “thread was being aborted” log when a REST Play/Stop connection was torn down. It was isolated to the package server, cleared, and did not correspond to a SceneTalkVR runtime exception. The final Console check was clean.

## Known limits and external blockers

This is not PICO validation and not collection readiness. Formal semantic Avatar presets, collection voices/deployments, Pilot-approved Humanoid, three collection-grade panoramas, official decisions, LAN service, OpenXR/PICO profiles, and device validation remain external blockers. Current Demo resources remain visibly labeled “Not Collection Approved.”

## Git scope

Base: `9b5b04b22479d9042c478b1c44ca8f094afa2997` on `experiment-v1.1-integration`, pushed before development. The delivery commit uses `feat(demo): add editor formal and pilot showcase modes`. Unity Skills package files and regenerated Stage 8/9/Preflight evidence are intentionally excluded from that commit.
