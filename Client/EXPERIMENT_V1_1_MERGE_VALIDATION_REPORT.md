# experiment-v1.1-integration Merge Validation Report

Generated: 2026-07-22 16:18 (Asia/Shanghai)

## Git provenance

- Main input: `6959f351fa557001540b419700126e98ebea6995`
- Experiment input: `5cea2a9dea2bca05eced88f96b3a0f629710f70f`
- Common baseline: `26217dfee11b4cfad263a3618f78f934c505edfd`
- Merge commit: `5455f7700c7bd96407a4e0a066dca829137f0236`
- Merge parents: main input first, experiment input second
- Final tested runtime/UI commit: `f1b82020855f26e26297c4447b562166f55a1429`
- Integration branch: `codex/integrate-experiment-v1.1`
- Remote push: not performed

The merge preserved the experiment branch's tracked code, resources, reports, evidence,
and screenshots. The original experiment worktree remained isolated with its pre-existing
three modified files and one untracked PICO metadata file.

## Integrated behavior

- Main's correction hot switching, history restore, three assistant appearances, streaming
  cleanup, and opening-state fixes remain present.
- Feedback First planning, readiness callbacks, monotonic timing events, and strict Formal/Pilot
  failure handling are integrated. Developer mode retains safe degradation; Formal mode records
  technical-invalid turns and keeps the dialogue gate closed on correction failures.
- Contracts, state, timing, assignment, task, goal, questionnaire, ranking, history, avatar session,
  and assistant embodiment fields are unified.
- The main TMP UI is retained. Formal ranking, questionnaire, Pilot participant flow, and Pilot
  ranking controls were migrated from legacy `Text`/`InputField` to TMP controls.
- Home keeps its original `Quit` control and does not show the global `Exit`. All non-Home flow
  screens use the same top-right `Exit` control. Experiment dialogue screens place task goals in
  the upper-left and a full-width dialogue panel below instead of using a narrow two-column layout.
- `SampleScene.unity` retains the main scene as its base and contains the experiment lifecycle,
  questionnaire, Pilot, protocol, task, and ranking bindings. Preflight reports no missing scripts.
- The LLM response envelope remains intact until content extraction; structured goal evaluation and
  experiment timing are added without cleaning the outer OpenAI response package prematurely.

## Automated verification

| Gate | Result | Evidence |
| --- | --- | --- |
| Unity version | PASS | `6000.3.16f1` |
| Full Android import and compilation | PASS | Unity batch process returned 0 |
| EditMode | PASS | 376 / 376, 0 failed/skipped at `f1b8202` |
| PlayMode | PASS | 45 / 45, 0 failed/skipped at `f1b8202` |
| Python analysis pipeline | PASS | 41 / 41, 0 failed |
| Formal synthetic matrix | PASS | Fresh 16 / 16 at `f1b8202` with independent bundle/checksum evidence |
| Pilot synthetic matrix | PASS | Fresh 9 / 9 at `f1b8202` with embodiment, voice, hash, speed, and volume invariants |
| Locked matrix guards | PASS | Formal 16 and Pilot 9 are all `BLOCKED`; zero unexpected PASS/FAIL |
| Synthetic Formal bundle integrity | PASS | checksum and non-mutating audit tests passed |
| Synthetic Pilot bundle integrity | PASS | checksum, retry, and non-mutating audit tests passed |
| Git conflict markers | PASS | none found |
| `git diff --check` | PASS | no whitespace errors |
| Scene missing scripts | PASS | regenerated Preflight report |
| Editor Formal Collection | READY | regenerated Preflight report |
| Pilot Collection | READY | regenerated Preflight report |
| Android OpenXR/PICO configuration | PASS (configuration only) | loader, features, controller profile, ARM64, IL2CPP, SDK and OpenGLES3 checks passed |
| Android validation APK | PASS | 0 errors, 44 warnings; `D383A6C2...D1607`; installed hash matches on authorized PICO A8110 |

The regenerated Preflight report is at
`Assets/SceneTalkVR/Docs/VitorPreflightReport.md`. Its remaining failed checks are the absent
`PicoLab` and `PicoPortable` approved deployment profiles; these are release blockers, not Editor
collection blockers.

## Manual release gates not completed

This merge is accepted by the automated merge gates, but it is **not approved for formal
participant collection release** until all of the following are completed:

1. Visually inspect the merged TMP Developer, Formal, Pilot, questionnaire, ranking, history,
   and goal views in the Unity Game view and on the PICO world-space canvas. Existing tracked
   screenshots were preserved, but they do not prove the final merged TMP rendering.
2. Run the real microphone -> STT -> LLM -> correction planning -> TTS -> playback -> dialogue
   chain and verify Feedback First timing and failure handling with live services.
3. Create and approve non-loopback `PicoLab` and `PicoPortable` deployment profiles with evidence.
4. Wake the PICO and perform the in-headset human check; the current automated screenshot is black
   because the headset was asleep. Verify Home Quit/no Exit, top-right Exit elsewhere, upper-left
   task goals, full-width lower dialogue panel, Chinese glyphs, controller interaction, spatial/
   non-spatial audio policy, avatar/orb visibility, fixed panoramas, and all Formal/Pilot boundaries.
5. Replace Android debug signing with the approved formal signing configuration, complete the
   credential rotation/history-closure process, and obtain technical/research-owner GO approval.

Until these gates pass, the merge must be described as suitable for automated integration and
Editor collection validation only, **not for formal collection release on PICO**.
