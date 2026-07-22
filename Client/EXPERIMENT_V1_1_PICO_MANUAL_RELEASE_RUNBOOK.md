# Experiment v1.1 PICO Manual Release Runbook

Chinese version: `EXPERIMENT_V1_1_PICO_MANUAL_RELEASE_RUNBOOK_ZH.md`

Use this runbook only after the automated gates are green. It deliberately leaves network
addresses, approval references, device identity, and human observations blank; those values must
come from the actual lab/portable deployment and must not be invented.

Current status at generation time: **NO-GO for formal participant collection**.

## 1. Candidate and operator record

Create an evidence directory outside the repository and record:

- Candidate Git SHA: `<git rev-parse main>`
- APK path and SHA-256: `<Get-FileHash -Algorithm SHA256 ...>`
- Date/time and timezone: `<actual>`
- Operator: `<actual>`
- PICO model, serial, OS/build: `<actual>`
- Deployment profile: `PicoLab` or `PicoPortable`
- Research-owner approval reference: `<actual immutable reference>`
- Voice gateway host and port: `<actual approved LAN endpoint>`
- LLM gateway host and port: `<actual approved LAN endpoint>`

Do not start participant collection if any value is blank. The current validation APK is Android
Debug signed and debuggable; it is an engineering artifact, not evidence of signed release
approval.

## 2. Credential incident closure

Two credentials were previously committed in shared branch history. Before any real-service run:

1. Revoke both exposed credentials at their providers.
2. Issue replacement credentials through the approved secret channel.
3. Configure replacements only in server-side environment/local configuration; never in Unity
   assets, scenes, Markdown, command history, screenshots, or session bundles.
4. Decide with the repository owner whether to rewrite Git history. Current-head redaction does not
   erase credentials from old commits, clones, or remote refs.
5. Run from the repository root:

   ```powershell
   py -3.11 scripts/verify_no_tracked_secrets.py
   ```

Expected result: `PASS`. A history rewrite, if approved, must be planned separately with clone and
remote coordination; do not perform it during a participant session.

## 3. Approve real PICO deployment profiles

In `Assets/SceneTalkVR/ExperimentProtocol/ExperimentDeploymentCatalog.asset`, create both profiles
only from approved real values. Each profile must satisfy:

| Field | Required value |
| --- | --- |
| `profileId` | `PicoLab` or `PicoPortable` |
| `voiceGatewayBaseUrl` | `http://<approved-host>:8787`; non-loopback, no query token |
| `requestTimeoutSeconds` | Positive approved timeout, normally 30 |
| `sttProvider` / `ttsProvider` | Actual non-empty, non-mock provider identifiers |
| `microphonePolicy` | Approved policy, normally `runtime_permission_required` |
| `networkRequired` | `true` |
| `approvedForCollection` / `collectionAllowed` | `true` only after research approval |
| `target` | `Pico` |
| `picoRequired` | `true` |
| `loopbackAllowed` | `false` |
| Editor/demo/rehearsal approvals | `false` unless separately justified |
| `evidenceReference` | Real immutable approval/ticket/protocol reference |

Set the same approved service host in:

- `Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset`
  - `voiceGatewayBaseUrl=http://<approved-host>:8787`
  - `directLlmApiUrl=http://<approved-host>:8788/api/llm/chat/completions`
- `Assets/SceneTalkVR/Voice/VoiceGatewaySettings.asset`
  - `gatewayBaseUrl=http://<approved-host>:8787`

Do not substitute the audit-time WLAN address without checking the real PICO network. From the
PICO, confirm both `/health` endpoints return the expected JSON. If a browser is unavailable, use
the connected-device shell as an availability check:

```powershell
& $adb shell ping -c 3 <approved-host>
& $adb shell "toybox nc -z -w 3 <approved-host> 8787"
& $adb shell "toybox nc -z -w 3 <approved-host> 8788"
```

`ping` may be blocked and some PICO builds may omit `nc`; a successful application health request
is the authoritative check. Do not use `adb reverse` as collection evidence because it bypasses the
real LAN deployment path.

After configuration, rerun Preflight from a terminal:

```powershell
& 'E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath '<repo>\Client' `
  -executeMethod SceneTalkVR.EditorTools.SceneTalkPreflightMenu.RunPreflightCheck `
  -logFile '<evidence>\preflight.log'
```

The regenerated report must show no missing scripts, both PICO profiles approved, and the intended
Formal/Pilot readiness state.

## 4. Connect, identify, and install on PICO

Use the Unity-bundled ADB:

```powershell
$adb = 'E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
$apk = '<approved candidate APK>'
& $adb start-server
& $adb devices -l
```

Stop if the list is empty or says `unauthorized`. Enable developer/USB debugging on the headset,
accept the headset authorization prompt, then repeat until one authorized device is listed.

Capture device identity before installation:

```powershell
& $adb shell getprop ro.product.manufacturer
& $adb shell getprop ro.product.model
& $adb shell getprop ro.serialno
& $adb shell getprop ro.build.version.release
& $adb shell getprop ro.build.fingerprint
& $adb install -r $apk
& $adb shell dumpsys package com.scenetalkvr.demo | Select-String 'versionCode|versionName|RECORD_AUDIO'
```

For a clean rehearsal only, `adb shell pm clear com.scenetalkvr.demo` may be used before launch.
Never clear application data after a participant session until its bundle is exported, hashed, and
backed up.

Start log capture in a second terminal, then launch the exact activity from the APK manifest:

```powershell
& $adb logcat -c
& $adb logcat -v threadtime > '<evidence>\pico-logcat.txt'
& $adb shell am start -n com.scenetalkvr.demo/com.unity3d.player.UnityPlayerActivity
```

On first launch, exercise the real runtime microphone permission prompt. Accept or deny through the
headset UI as required by the test case; do not pre-grant permission for the primary permission-flow
test. Confirm the granted state afterward with `dumpsys package`.

## 5. Mandatory visual inspection

Inspect inside the headset, not only a desktop mirror. For every view capture a screenshot or video
plus operator notes for: readable font size, no clipping/overlap, correct world-space depth,
reachable controls, selected/disabled button state, and controller ray interaction.

Required views:

1. Developer mode: source/style/embodiment controls and History.
2. Formal participant start and assignment/task screen; manual correction controls and History must
   be hidden.
3. Formal goal panel, condition questionnaire, final ranking, and interview boundary.
4. Pilot participant start, task/goal panel, condition questionnaire, and final ranking.
5. Error/technical-invalid state and retry path.
6. Microphone permission denied and subsequently granted behavior.

Reliable screenshot capture:

```powershell
& $adb shell screencap -p /sdcard/scenetalk-check.png
& $adb pull /sdcard/scenetalk-check.png '<evidence>\screenshots\<view-id>.png'
```

The operator must also look through the headset; a screenshot alone cannot prove binocular comfort,
depth, controller reachability, or readable placement.

## 6. Formal 16-cell rehearsal

Use a clearly marked rehearsal participant and the locked assignment flow. Do not manually override
condition ordering. Complete and record all combinations:

| Conditions | Tasks |
| --- | --- |
| `NE`, `NR`, `SE`, `SR` | `hotel_check_in`, `furniture_shopping`, `gym_membership`, `tourist_assistance` |

For each of the 16 cells verify the assigned fixed panorama, exact task avatar, task goals, one
feedback turn, questionnaire linkage, completion boundary, and saved condition-run/task-assignment
IDs. After all four conditions verify final ranking and interview linkage.

## 7. Pilot 9-cell rehearsal

Complete and record all combinations:

| Embodiments | Tasks |
| --- | --- |
| `voice_only`, `floating_orb`, `humanoid_agent` | `pilot_restaurant_walk_in`, `pilot_restaurant_ordering`, `pilot_restaurant_wrong_dish` |

For every cell confirm identical feedback text hash, shared voice profile, speaking speed `1`, volume
`1`, and `feedback_only` subtitle policy. Condition-specific checks:

- `voice_only`: no visual entity; head-locked/non-spatial audio (`spatialBlend=0`).
- `floating_orb`: only the orb is visible; world-positioned/spatial audio (`spatialBlend=1`).
- `humanoid_agent`: only the approved humanoid is visible, with Idle/Talking behavior and
  world-positioned/spatial audio (`spatialBlend=1`).

Verify the Pilot final ranking only becomes available after all three conditions.

## 8. Real voice and Feedback First evidence

Run at least one feedback turn and one no-feedback turn using a human microphone utterance. The
chain must use the real services:

`microphone -> STT -> LLM correction/dialogue -> TTS -> playback -> dialogue`

For a feedback turn, timing evidence must show:

- `DialogueGateClosed` before dialogue playback.
- `CorrectionPlaybackStarted` then `CorrectionPlaybackEnded` before `DialogueGateOpened` and
  `DialoguePlaybackStarted`.
- `DialoguePlaybackEnded` then `TurnCompleted`.
- Monotonic timestamps and non-negative derived latency fields.

For a no-feedback turn, no correction playback event may exist; the gate must open before dialogue
playback. Force one controlled service failure during rehearsal. Locked Formal/Pilot must emit
`TurnTechnicalInvalid`, keep the dialogue gate closed, and never disguise fallback output as a valid
participant turn.

Listen for intelligibility, clipping, double playback, spatial direction, avatar lip/body animation,
and whether the visual entity disappears/changes at the correct boundary. Record observations; log
ordering alone is not sufficient audio evidence.

## 9. Bundle export and non-mutating audit

Before analysis, hash every source-bundle file:

```powershell
Get-ChildItem '<bundle>' -Recurse -File | Sort-Object FullName |
  Get-FileHash -Algorithm SHA256 |
  Export-Csv '<evidence>\bundle-hashes-before.csv' -NoTypeInformation
```

Run the documented source-layout pipeline (ordinary wheel installation is not the supported layout):

```powershell
Set-Location '<repo>\Client\Analysis'
$env:PYTHONPATH = (Resolve-Path '.\src').Path
py -3.11 -m scenetalkvr_analysis validate-bundle '<bundle>'
py -3.11 -m scenetalkvr_analysis analyze-bundle '<bundle>' --config '<approved analysis config>'
```

Hash the source bundle again and compare:

```powershell
Get-ChildItem '<bundle>' -Recurse -File | Sort-Object FullName |
  Get-FileHash -Algorithm SHA256 |
  Export-Csv '<evidence>\bundle-hashes-after.csv' -NoTypeInformation
Compare-Object (Import-Csv '<evidence>\bundle-hashes-before.csv') `
               (Import-Csv '<evidence>\bundle-hashes-after.csv') `
               -Property Path,Hash
```

Expected comparison output: empty. The audit must report valid checksums, Feedback First ordering,
complete questionnaire/ranking/interview linkage, and retained technical-invalid attempts.

## 10. Final go/no-go signature

Formal collection is GO only when all are attached to the same candidate SHA and APK hash:

- credential revocation/rotation evidence;
- approved `PicoLab` and `PicoPortable` profiles;
- green Preflight, EditMode, PlayMode, Python, Formal 16, and Pilot 9 results;
- authorized PICO identity/install evidence;
- headset visual checklist and screenshots;
- real voice/no-feedback/failure timing evidence;
- immutable bundle hashes and integrity report;
- build signing decision and research-owner approval.

Sign-off fields:

- Operator/date: `<actual>`
- Technical reviewer/date: `<actual>`
- Research owner/date: `<actual>`
- Decision: `<GO | NO-GO>`
- Evidence root/checksum: `<actual>`
