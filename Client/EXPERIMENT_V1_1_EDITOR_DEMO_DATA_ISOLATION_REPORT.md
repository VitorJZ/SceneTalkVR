# Editor Demo data isolation

## Enforced boundaries

`ExperimentRuntimeMode` is the single runtime discriminator. Demo assignments must be `EditorDemoFormal` or `EditorDemoPilot` and carry all four isolation fields: `dataOrigin=editor_demo`, `collectionEligible=false`, `developerTestAssignment=true`, `demoMode=true`. `ExperimentStudyLifecycle` and `PilotWorkflowCoordinator` reject mismatched Demo assignment metadata.

Demo logs, questionnaires, ranking, interview, assignments, and Bundles route to `Client/Library/SceneTalkVR/EditorDemoSessions`, not collection directories. `SessionBundleManifest` records `sessionMode=editor_demo_formal|editor_demo_pilot`, both protocol versions, and the isolation flags. The integrity auditor rejects a Demo bundle that claims collection eligibility or lacks Demo identity.

Demo voice, deployment, avatar, and Humanoid resources are separately flagged as not collection-approved. The `EditorDemo` deployment allows loopback only for Editor demonstration and has `collectionAllowed=false`.

## Analysis boundary

The Stage 9 Python configuration introduces `includeDemoForTesting`, default `false`. Normal analysis rejects Editor Demo input with `editor_demo_input_requires_includeDemoForTesting`. Even with the flag explicitly enabled and `requireCollectionEligible=false`, Demo runs never generate primary collection analysis (`primaryAnalysisGenerated=false`). Python tests: 38/38 PASS.

The tested Formal and Pilot source bundle hashes were identical before and after analysis, proving read-only consumption.

## Collection and release state

- Official `ExperimentV11Protocol.asset`: 11/11 decisions remain `Unconfirmed`.
- `participantCollectionReady=false`.
- `releaseCandidateEligible=false`.
- Locked Formal and Locked Pilot validation were not modified or bypassed.
- Demo assets do not satisfy collection avatar, voice, deployment, panorama, PICO, or approval gates.

External blockers remain: official research decisions, exact formal Avatar presets, approved collection voices/deployments, approved Pilot Humanoid, three 1:1 formal panoramas, LAN/PICO/OpenXR readiness, and PICO device validation.
