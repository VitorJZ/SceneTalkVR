# Experiment v1.1 Stage 7 — Release Readiness Report

Overall status: `BLOCKED_PENDING_RESEARCH_INPUT`, `BLOCKED_PENDING_ASSET`, `BLOCKED_DEVICE_UNAVAILABLE`

Release Candidate status: **not eligible**. No `experiment-v1.1-rc1` tag was created and no claim is made that Pilot or Formal collection may begin.

## Baseline and scope

- Branch: `experiment-v1.1-integration`
- Required base and starting HEAD: `eae67af6b0f2fd2dbf55ce1e9cc08222fe9f11ec`
- Unity: 6000.3.16f1; the already-open Editor was used through UnitySkills. No second Editor was launched.
- Excluded local UnitySkills changes remain unstaged: `Packages/manifest.json`, `Packages/packages-lock.json`, `.agents/skills/`.
- No research decision, official Avatar/Humanoid, official voice profile, LAN address, secret, replacement panorama, PICO result or real-service result was inferred.

## Stage 6 mandatory regression

The current base compiled successfully before Stage 7. The full EditMode run contained 178 tests: 176 passed and two UnitySkills package tests failed (`UnitySkills.Tests.Core.NewCapabilitiesTests.PlayCapture_DurationOutsideRange_IsRejected(0/301)`). These are tooling-package failures, not SceneTalkVR test failures, but the required all-green full regression condition is not satisfied. After domain reload the server reverted to Auto, where `test_run` is forbidden; project-only EditMode, PlayMode, Stage 3–6 directed suites, minimum Play Mode and final Preflight therefore remain pending. Previous Stage 6 results are not substituted for the required new run.

Stage 7 C# compilation was actually rerun. One first-pass compiler error in the new mapping parser was detected and fixed; the final compilation completed at `2026-07-19T03:48:53Z` with 0 errors and 10 existing warnings.

UnitySkills read-only diagnostics also returned 0 Console errors and 0 loaded-scene missing-script/missing-reference issues.

## Research decision mechanism

`ExperimentProtocolDecision` now stores `decisionId`, `status`, `confirmedValue`, `confirmedBy`, `confirmedAtUtc`, `evidenceReference`, and `notes`. `ExperimentProtocolChange` supplies a Git-visible protocol change log. Formal validation rejects a Confirmed decision without value and provenance.

The protocol schema recognizes exactly these required IDs: `condition_letter_mapping`, `formal_task_no_replacement`, `formal_social_comfort`, `pilot_feedback_style`, `voice_only_spatial_audio`, `pilot_sequence_mapping`, `formal_max_turns`, `formal_max_duration`, `pilot_max_turns`, `pilot_max_duration`, and `questionnaire_scale_anchors`. All remain Unconfirmed.

Formal sequences are derived only from an approved complete mapping value such as the syntax `a=...,b=...,c=...,d=...`; the parser requires a one-to-one set of NE/NR/SE/SR and then produces only `a-b-c-d`, `b-c-d-a`, `c-d-a-b`, `d-a-b-c`. Pilot mapping similarly requires a one-to-one mapping of `voice_only`, `floating_orb`, `humanoid_agent` and produces only the three prescribed cyclic sequences. Missing, duplicate, incomplete and illegal mappings are rejected. The allocators no longer consume independently serialized sequence arrays as authority.

Protocol version is advanced to `1.1.0-stage7` for the schema/decision-boundary change. Existing `ExperimentAssignmentAllocator.IsCompatible` and `PilotAssignmentAllocator.IsCompatible` reject snapshots whose protocol version differs; neither allocator silently regenerates an existing Assignment.

## Formal Avatar and Pilot Humanoid boundary

`AvatarPresetEntry` now has collection metadata for semantic role, voice key/id, Animator, idle/thinking/speaking states, spawn transform, mobile readiness, asset version, approval and evidence. `AvatarCatalog.ValidateExactFormalPreset` requires the requested key and semantic role to match exactly and rejects incomplete/unapproved assets. Existing teacher/barista catalog entries are not accepted as Hotel/Furniture/Gym/Tourist substitutes. All four Task Catalog preset keys are currently empty, so Formal stays blocked.

`PilotPresentationProfile` now records Animator, idle/speaking controls, transform, AudioSource requirement, mobile readiness, asset version, approval and evidence. Locked Pilot validation rejects a missing/unapproved Humanoid. The presenter uses catalog rotation/scale/Animator metadata and never changes Humanoid failure into an Orb run. Reset still stops audio, hides presentation state and destroys the Humanoid instance.

## Voice variable control

`ExperimentVoiceProfileCatalog` is the intended sole collection source for provider, real voice ID, language, speed, volume, pitch, sample rate and subtitle policy. It requires explicit collection approval and evidence. The catalog intentionally contains no fabricated approved profile, so collection is blocked until the team supplies the real parameters.

| Variable | Voice Only | Orb | Humanoid | Same now? |
|---|---|---|---|---|
| Feedback text hash | Stage 6 common correction payload | same payload | same payload | software path yes; real run pending |
| Voice ID | unconfirmed catalog | unconfirmed catalog | unconfirmed catalog | blocked |
| Speed | unconfirmed catalog | unconfirmed catalog | unconfirmed catalog | blocked |
| Volume | unconfirmed catalog | unconfirmed catalog | unconfirmed catalog | blocked |
| Audio policy | research decision pending | catalog profile pending | catalog profile pending | intentionally may differ only as approved embodiment variable |
| Subtitle policy | unconfirmed catalog | unconfirmed catalog | unconfirmed catalog | blocked |
| Gate gap | Stage 3 Feedback First gate | same gate | same gate | automated prior behavior; real/PICO run pending |

Formal same-style feedback profiles are represented by separate Explicit/Recast central keys; Provider does not select a different voice. Dialogue task voice keys must also resolve to approved catalog entries. `ExperimentConditionManager` is bound to the central Voice and Deployment catalogs in `SampleScene` and locked Formal validation consumes them. Runtime requests/logs cannot be considered collection-valid until approved entries exist; Developer Mode retains its legacy debug path.

## Deployment and secret isolation

`ExperimentDeploymentCatalog` models `DevelopmentEditor`, `PicoLab`, `PicoPortable`, and `MockOffline` without key/secret fields. Locked collection rejects missing/unapproved endpoints, loopback PICO endpoints, empty/mock providers and URL query secret material. Logs should record only `profileId` and `EndpointHost`; service credentials remain server-side. The committed templates contain no machine LAN IP and both PICO profiles remain unapproved/empty.

## Panorama quality

Hotel, Furniture and Gym remain 1024×1024 (1:1). Formal Preflight now requires a loadable texture with exact 2:1 aspect and at least 2048×1024. These three tasks are `BLOCKED_PENDING_ASSET`; no AI substitute was generated because source, licence, semantics and team approval were not supplied. Tourist remains the existing 2048×1024 asset and was not changed. Task Catalog version remains `1.1.0-stage2`; it must be increased only when approved replacements are actually imported, which will invalidate old Assignment snapshots by catalog-version compatibility.

## Preflight and data integrity

Preflight was extended to print every decision’s value, confirmer and evidence; validate the central voice/deployment catalogs; exact Formal Avatar semantic bindings; and collection-grade panorama dimensions. Expected blockers are explicit failures rather than fallbacks.

The actual Stage 7 Preflight run produced 65 passed checks and 34 failed checks. The failures explicitly include all eleven research decisions, all four Formal Avatar keys, approved voice/deployment profiles, Pilot Humanoid, the three 1:1 panoramas, and existing Android/PICO prerequisites. Tourist panorama passed the new 2048×1024 rule. The generated evidence is `Assets/SceneTalkVR/Docs/VitorPreflightReport.md`.

The new read-only `SessionDataIntegrityAuditor` and Editor window scan Assignment and JSON/JSONL exports and return PASS/WARNING/FAIL findings without modifying inputs. Controlled tests cover dialogue-before-feedback detection and byte-preserving audit behavior. A genuine completed Formal/Pilot dataset is still needed for the full cross-file, goal/ranking/closure acceptance audit.

## Automated test additions

`Stage7ReleaseReadinessTests` covers approved mapping derivation, illegal mapping rejection, missing decision evidence, assignment invalidation after protocol-version change, exact Formal Avatar mismatch, PICO loopback/mock rejection, absence of secret fields, central voice blockers, and read-only timing integrity detection. Tests compile but their execution is pending Bypass because UnitySkills currently reports Auto.

Formal 16 and Pilot 9 matrices are delivered as explicit `NOT_RUN` rows with blocker evidence. They are not marked as passed controlled tests.

## PICO and real services

No device was available: `BLOCKED_DEVICE_UNAVAILABLE`. Android build/install, XR, controller, microphone, Avatar/Agent/UI, logging, APK hash and PICO latency were not run. No fixed-LAN real STT/TTS samples were available, so no real-service latency or raw-event reconciliation is claimed. The separate PICO report contains the operator procedure and required metadata.

## Release Candidate gate

The RC gate is false because research decisions, four Formal Avatar presets, Pilot Humanoid, approved Voice Profiles, three replacement panoramas, all-green fresh regression, Formal 16, Pilot 9, continuous sessions, real services, PICO and real-session integrity PASS are missing. The release manifest records this and no tag/APK/hash.

## Files added or changed

- Protocol/mapping: `ExperimentV11ProtocolConfig.cs`, `ExperimentAssignmentAllocator.cs`, `PilotExperimentModel.cs`
- Asset contracts: `AvatarPresetEntry.cs`, `AvatarCatalog.cs`, `AvatarPresetResolver.cs`, `PilotPresentationCatalog.cs`, `PilotEmbodimentPresenter.cs`
- Central catalogs: `ExperimentVoiceProfileCatalog.cs`, `ExperimentDeploymentCatalog.cs`
- Diagnostics: `SceneTalkPreflightMenu.cs`, `SessionDataIntegrityAuditor.cs`, `SessionDataIntegrityAuditWindow.cs`
- Asset initializer: `Stage7ReleaseReadinessAssetBuilder.cs`
- Tests: `Stage7ReleaseReadinessTests.cs`
- Nine Stage 7 deliverables listed in the request.

## Required next inputs

1. Research-team signed values and evidence for all eleven decisions.
2. Four semantically correct, approved Formal Avatar presets and one approved Pilot Humanoid with full metadata.
3. Approved Formal/Pilot voice request parameters and provider evidence.
4. Approved/licensed 2:1 Hotel/Furniture/Gym panoramas and import/memory review.
5. Approved PICO deployment endpoints (no secrets in Unity assets).
6. UnitySkills Bypass for the mandatory final test/Play Mode/Preflight run.
7. Connected PICO, actual LAN services, and completed-session datasets.

Until all inputs and validation evidence exist, the correct conclusion is: **neither Pilot nor Formal experiment collection is ready to start**.
