# Experiment v1.1 Formal PICO Release Gate Status

Generated: 2026-07-22 16:18 (Asia/Shanghai)

Release decision: **BLOCKED - do not use this build for formal participant collection.**

The merged project, final-head regression suites, experiment matrices, Preflight, and an Android
validation APK pass the available automated checks. The remaining gates require credential-owner
action, approved real deployment profiles, human PICO visual/audio judgment, a live
voice chain, and research-owner sign-off. Desktop health and synthetic evidence do not prove those
gates.

## Provenance

- Merge commit: `5455f7700c7bd96407a4e0a066dca829137f0236`
- Original merge-validation commit: `7becf92a678f776ec25982409fbc7cab49f65f1b`
- Credential-redaction commit: `8a0759e51afa01cbf8c92a65bca7cabdb773fb3c`
- Batch-Preflight fix commit: `dd1c02f537234e8226b0dc7e9a2b042b77fd309c`
- Tested/APK source and stacked task/dialogue layout commit: `f1b82020855f26e26297c4447b562166f55a1429`
- Branch: `codex/integrate-experiment-v1.1`
- Unity: `6000.3.16f1`
- Remote push of the integration work: not performed

The current APK build metadata records `f1b8202`. Unity's build/test-time rewrites of tracked assets were
restored after evidence was collected.

## Automated evidence

| Gate | Result | Evidence |
| --- | --- | --- |
| Candidate-head Unity EditMode | PASS at `f1b8202` | 376/376, 0 failed/skipped |
| Candidate-head Unity PlayMode | PASS at `f1b8202` | 45/45, 0 failed/skipped; includes Formal/Pilot, Home Quit/Exit exclusivity, and shown/hidden-subtitle stacked-layout assertions |
| Candidate-head Python analysis | PASS at `f1b8202` | 41/41 using documented `PYTHONPATH=src` layout |
| Formal synthetic matrix | PASS | Fresh 16/16 at `f1b8202`; all cases unique, integrity `PASS` |
| Pilot synthetic matrix | PASS | Fresh 9/9 at `f1b8202`; all cases unique, integrity `PASS` |
| Locked matrix guards | PASS | Formal 16/16 and Pilot 9/9 remain `BLOCKED`, with zero unexpected PASS/FAIL |
| Scene and Editor Preflight | PASS/READY | 102 checks pass; the only two failed checks are the expected missing `PicoLab`/`PicoPortable` profiles; no missing script; Formal Editor and Pilot Editor ready |
| Tracked-worktree credential scan | PASS | 2,475 tracked text files scanned; no high-confidence finding |
| APK credential scan | PASS | 482 APK ZIP entries scanned; no high-confidence credential finding |
| Gateway offline contracts | PASS | Python compile, mock STT/TTS/WAV, response-envelope passthrough, missing-key guard |
| Workstation service health only | PASS | `192.168.137.1:8787/health` returns Tencent; `:8788/health` reports an API key; no generation request sent |
| Android Development APK | PASS | Unity: 0 errors, 44 warnings, 3 min 42 s build |
| APK identity/SDK | PASS | `com.scenetalkvr.demo`, min SDK 29, target SDK 36 |
| APK permissions | PASS | `INTERNET` and `RECORD_AUDIO` present; runtime permission code present |
| APK network policy | PASS | `usesCleartextTraffic=true`; referenced base config permits cleartext HTTP |
| APK ABI | PASS | ARM64-only IL2CPP, Unity OpenXR, and PICO native libraries |
| APK integrity | PASS for validation | ZIP alignment and APK Signature Scheme v2 verification pass |
| Formal release signing | BLOCKED | APK is debuggable and signed by the Android Debug certificate |
| Configured PICO service URLs | PENDING DEVICE REQUEST | PICO is `192.168.137.223/24` and sees the host over LAN/SSDP; ICMP is blocked and the headset has no `nc`; no application-level health or generation request is captured |
| PICO connection and APK installation | PASS (installation only) | Authorized PICO A8110 on Android 10; `adb install -r` succeeded; device APK SHA-256 matches the candidate |
| Deployment profiles | BLOCKED | `PicoLab` and `PicoPortable` remain absent |
| Exposed credential closure | BLOCKED HUMAN | Current head is redacted; old shared Git history still contains both values |
| Final TMP/world-space visual review | PENDING HUMAN | Preserved screenshots do not prove final headset rendering |
| Live microphone-to-dialogue chain | PENDING HUMAN | No paid real generation request was made during this audit |

Evidence files outside the repository:

- EditMode XML/log: `E:\Temp\SceneTalkVR-f1b8202-editmode.xml`, `.log`
- PlayMode XML/log: `E:\Temp\SceneTalkVR-f1b8202-playmode.xml`, `.log`
- Python JUnit XML: `E:\Temp\SceneTalkVR-f1b8202-python.xml`
- Android build log: `E:\Temp\SceneTalkVR-f1b8202-android-build.log`
- PICO screenshot/logcat: `E:\Temp\SceneTalkVR-f1b8202-pico-home.png`, `-pico-logcat.txt`

Fresh matrix artifacts and the regenerated Preflight report are committed in the repository. The
detailed human procedures are `EXPERIMENT_V1_1_PICO_MANUAL_RELEASE_RUNBOOK.md` (English) and
`EXPERIMENT_V1_1_PICO_MANUAL_RELEASE_RUNBOOK_ZH.md` (Chinese).

## APK artifact

- Path: `E:\Temp\SceneTalkVR-f1b8202-stacked-layout-validation.apk`
- File size: `155197600` bytes
- SHA-256: `D383A6C2E1330598DD3A94479048F39A39AFB1C55D4F9A72B7EE1D0CF88D1607`
- Build mode: Android Development APK
- Installation: completed on an authorized PICO A8110 at 2026-07-22 16:02 (Asia/Shanghai)
- Installed APK SHA-256: matches `D383A6C2E1330598DD3A94479048F39A39AFB1C55D4F9A72B7EE1D0CF88D1607` (rechecked from `/data/app/.../base.apk`)
- Install mode: `adb install -r`; existing application data was preserved and `RECORD_AUDIO` was
  already granted, so the first-run permission prompt remains untested
- Device process state: `UnityPlayerActivity` was explicitly launched. The automated screenshot is
  black because the headset was asleep and is not accepted as visual evidence. No application service
  request or human in-headset visual/audio judgment was performed.

`aapt` and `apkanalyzer` independently confirmed the package, SDK levels, permissions, application
flags, and network-security reference. `apksigner` and `zipalign` confirmed artifact integrity. The
debuggable/debug-certificate result is intentionally recorded as a formal-signing blocker rather
than being presented as a release signature.

The 44 Unity warning records reduce to known categories: PICO platform `appID` is not configured,
diagnostics symbols are not Full/SymbolTable, one Windows-only PICO library has Android importer
metadata, and the PICO hand-outline shader reports GLES3 initialization warnings. No application
code references PICO Platform services, so the empty `appID` did not block this validation build;
the release owner must still triage all four categories.

## Credential security blocker

Automated scanning found two high-confidence API credentials in tracked documents, a Recovery
scene, and a Holodeck fallback. Both `main@6959f35` and
`experiment-v1.1-integration@5cea2a9` already contained the material through shared history. Commit
`8a0759e` removes the values from current files, makes Holodeck fail closed when its environment key
is absent, and adds `scripts/verify_no_tracked_secrets.py`.

Current-head redaction does not revoke a credential or remove it from old commits, existing clones,
or remote refs. The credential owner must revoke/rotate both values before any real-service run.
Git history rewriting is a coordinated destructive operation and was not performed without explicit
authorization.

## Network and deployment blockers

The committed runtime configuration still contains:

- Voice: `http://192.168.137.1:8787`
- LLM: `http://192.168.137.1:8788/api/llm/chat/completions`

At the latest audit, the workstation hotspot interface is `192.168.137.1/24`, the PICO WLAN address
is `192.168.137.223/24`, and the two configured health URLs return HTTP 200 on the workstation.
The PICO receives SSDP traffic from `192.168.137.1`, which proves a LAN path exists, but its firmware
does not provide `nc`/`curl`/`wget` and ICMP receives no reply. Therefore TCP reachability and the
real STT/LLM/TTS path remain unproven until the application itself completes a logged request. Do
not use `adb reverse` as substitute evidence.

`ExperimentDeploymentCatalog.asset` has only `EditorCollection`. A valid PICO profile requires a
real non-loopback endpoint, positive timeout, non-mock providers, PICO target/policy flags, and a
genuine approval evidence reference. Credentials must stay server-side.

## Required human completion

Follow `EXPERIMENT_V1_1_PICO_MANUAL_RELEASE_RUNBOOK_ZH.md` (Chinese) or its English counterpart
exactly. The remaining decisions/actions are:

1. Revoke/rotate the exposed credentials and decide whether coordinated history rewriting is
   required.
2. Approve the real lab/portable network and create genuine `PicoLab`/`PicoPortable` profiles.
3. Launch the installed APK on the authorized PICO and capture device logs and runtime evidence.
4. Complete headset visual checks plus the Formal 16 and Pilot 9 rehearsal matrices.
5. Run human microphone feedback/no-feedback/failure turns and verify Feedback First and audio/
   embodiment behavior.
6. Export and non-mutatingly audit real bundles, attach hashes/evidence, decide signing policy, and
   obtain technical/research-owner GO approval.

Until every item has evidence tied to the same candidate SHA and APK hash, the project remains
suitable for automated integration and Editor validation only, **not formal collection release on
PICO**.
