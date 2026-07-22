# Experiment v1.1 Formal PICO Release Gate Status

Generated: 2026-07-22 11:37 (Asia/Shanghai)

Release decision: **BLOCKED — do not use this build for formal participant collection.**

The merged project and an Android validation APK pass the available automated checks. The
remaining blockers require a real PICO, an approved deployment/network definition, human visual
inspection, and a live end-to-end voice run. No PICO evidence or research approval is inferred
from desktop health checks or synthetic tests.

## Provenance

- Merge commit: `5455f7700c7bd96407a4e0a066dca829137f0236`
- Automated merge-validation commit: `7becf92a678f776ec25982409fbc7cab49f65f1b`
- APK source commit: `1ed80aa5fd39c7e15fff317d63745a7e160a002b`
- Branch: `codex/integrate-experiment-v1.1`
- Unity: `6000.3.16f1`
- Remote push: not performed

The APK was built after the reproducible Editor build entry point was committed. The generated
player build metadata therefore records source commit `1ed80aa`. Unity's build-time edits to
tracked assets were restored after the artifact was produced.

## Automated evidence

| Gate | Result | Evidence |
| --- | --- | --- |
| Merge regression suite | PASS at `7becf92` | EditMode 373/373, PlayMode 41/41, Python 41/41 |
| Synthetic experiment matrices | PASS at `7becf92` | Formal 16/16 and Pilot 9/9 |
| Scene and Editor preflight | PASS/READY at `7becf92` | No missing script; Formal Editor and Pilot Editor ready |
| Gateway offline contracts | PASS | Python compile; mock STT; mock TTS/WAV; OpenAI response-envelope passthrough; missing-key guard |
| Current service health only | PASS | `10.180.73.186:8787/health` reports Tencent; `10.180.73.186:8788/health` reports configured API key |
| Android Development APK | PASS | Unity build finished with 0 errors and 41 warnings in 31.16 s after cache warm-up |
| APK identity and SDK | PASS | `com.scenetalkvr.demo`, min SDK 29, target SDK 36 |
| APK permissions | PASS | `android.permission.INTERNET` and `android.permission.RECORD_AUDIO` present |
| APK ABI | PASS | `arm64-v8a` native code, including IL2CPP, Unity OpenXR, and PICO libraries |
| Configured PICO service URLs | BLOCKED | Both configured `192.168.137.1` health URLs time out from this workstation |
| PICO connection | BLOCKED | `adb devices -l` returns no device |
| Deployment profiles | BLOCKED | `PicoLab` and `PicoPortable` are absent; only `EditorCollection` is defined |
| Final TMP/world-space visual review | PENDING HUMAN | No claim made from preserved pre-merge screenshots |
| Live microphone-to-dialogue chain | PENDING HUMAN | No paid STT, TTS, or LLM generation request was sent during this audit |

The regression results are detailed in `EXPERIMENT_V1_1_MERGE_VALIDATION_REPORT.md`. The only
source change between that report and the APK source commit is the Editor-only Android validation
build entry point; the complete current project compiled during both Android builds.

## APK artifact

- Path: `E:\Temp\SceneTalkVR-integration-v1.1-validation.apk`
- Build log: `E:\Temp\SceneTalkVR-integration-v1.1-android-build-final.log`
- File size: `132915683` bytes
- SHA-256: `281C1CE0FCE81CC743D22AA4761A3DF3FB73FEB53CCBBBD03DE3940072263E9D`
- Build mode: Android Development APK; not installed, not device-run, and not a signed formal release

`aapt` and `apkanalyzer` independently confirmed the package ID, SDK levels, and required
permissions. ZIP inspection confirmed that all native libraries are under `lib/arm64-v8a`.

The 41 Unity warning records reduce to known categories in the build log: PICO platform `appID`
is not configured, diagnostics symbols are not set to Full/SymbolTable, one Windows-only PICO
library has Android importer metadata, and the PICO hand-outline shader reports GLES3
initialization warnings. These did not fail the validation build, but they remain items for release
owner triage rather than being silently treated as zero-warning evidence.

## Network and deployment blockers

The committed runtime configuration currently contains:

- Voice: `http://192.168.137.1:8787`
- LLM: `http://192.168.137.1:8788/api/llm/chat/completions`

At audit time the workstation WLAN address was `10.180.73.186`; there was no local
`192.168.137.1` interface or matching route. The two services listen on `0.0.0.0` ports 8787 and
8788 and their health endpoints are reachable through the WLAN address, but this does not prove
that a PICO can reach that address. The committed URLs were not changed because the actual PICO
network and approved deployment topology are unknown.

`ExperimentDeploymentCatalog.asset` has no `PicoLab` or `PicoPortable` record. A valid record must
use a real non-loopback voice gateway, positive timeout, non-mock STT/TTS providers, the correct
PICO target and policy flags, and a genuine approval evidence reference. Service credentials must
remain server-side and must not be placed in the URL or asset.

## Required human completion steps

1. Put the PICO and service host on the intended lab/portable network. Confirm the stable PC/server
   LAN address from the PICO by opening both `/health` endpoints. Do not assume the audit-time WLAN
   address is the approved permanent address.
2. Have the research owner define and approve `PicoLab` and `PicoPortable`, including the endpoint,
   providers, timeout, microphone/network policy, target flags, and a real evidence reference.
3. Enable PICO developer mode and USB debugging, connect and authorize the headset, then confirm
   that `adb devices -l` shows a device rather than an empty or `unauthorized` entry.
4. Install the validation APK and manually inspect Developer, Formal, Pilot, questionnaire,
   ranking, History, and goal views for TMP readability, button state, clipping, and world-space
   placement.
5. Run a real participant-like turn through microphone -> STT -> LLM -> correction planning ->
   TTS -> playback -> dialogue. Verify Feedback First ordering, technical-invalid failure handling,
   VoiceOnly/Orb/Humanoid visibility, and spatial/non-spatial audio policy.
6. Export the resulting Formal/Pilot bundle, validate its checksum and timing integrity without
   modifying the source bundle, attach device screenshots/logs, and obtain the final research-owner
   release decision.

Until all six steps have evidence, the project remains suitable for automated integration and
Editor validation only, **not formal collection release on PICO**.
