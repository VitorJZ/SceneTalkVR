# PICO 360 Panorama Real-Device Fix (2026-07-13)

This note records the investigation and fix for the issue where the 360 panorama appeared in the Unity Editor but did not appear on the PICO real device.

## Symptom

- Unity Editor could show the generated or fallback 360 panorama.
- PICO real-device build entered the scene flow, but the 360 background did not appear.
- Voice recognition and LLM routing were already moving through the PC-side gateways.

## Root Causes

1. The PICO device was on the Windows hotspot subnet:
   - PICO: `192.168.137.38/24`
   - PC hotspot gateway: `192.168.137.1`

   The Unity runtime config still pointed to the PC WLAN address `10.180.73.186`, which the PICO build could not reliably reach from its current route table.

2. `PanoramaSceneService` used `Shader.Find("Skybox/Panoramic")` when `useSkySphere` was disabled.

   This can work in the Editor, but the shader can be stripped from Android/PICO builds if it is not explicitly retained. When the shader is missing, the texture exists but cannot be displayed as the skybox.

3. The PICO APK does not inherit the PC environment variable `SILICONFLOW_API_KEY`.

   With an empty Unity-side panorama API key, PICO falls back to `FallbackPanorama.png`. That fallback texture is valid, but it still needs a render path that works on Android/PICO.

## Changes Made

- Updated PICO-reachable gateway URLs to the current hotspot gateway:
  - `http://192.168.137.1:8787`
  - `http://192.168.137.1:8788/api/llm/chat/completions`
- Set the scene panorama renderer to `useSkySphere: 1`.
- Changed `PanoramaSceneService` default `useSkySphere` to `true`.
- Added a fallback path: if `Skybox/Panoramic` is unavailable, render the panorama on an inverted sky sphere.
- Added unlit shader fallback checks for:
  - `Unlit/Texture`
  - `Universal Render Pipeline/Unlit`
  - `Sprites/Default`
- Updated setup tooling so rebuilt demo rigs also default to sky sphere rendering.

## Files Touched

- `Client/Assets/SceneTalkVR/Scripts/Services/PanoramaSceneService.cs`
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkDemoSetupMenu.cs`
- `Client/Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset`
- `Client/Assets/SceneTalkVR/Voice/VoiceGatewaySettings.asset`
- `Client/Assets/Scenes/SampleScene.unity`
- `Client/Assets/SceneTalkVR/Docs/VitorPreflightReport.md`
- `Server/llm-gateway/README.md`
- `Server/voice-gateway/README.md`
- `documents/pico-real-device-gateway-runbook-2026-07-13.md`
- `documents/speech-gateway-technical-plan.md`

## Verification

Current service checks:

```powershell
Invoke-RestMethod http://192.168.137.1:8787/health | ConvertTo-Json -Compress
Invoke-RestMethod http://192.168.137.1:8788/health | ConvertTo-Json -Compress
```

Expected:

```json
{"status":"ok","provider":"tencent"}
{"status":"ok","hasApiKey":true}
```

Current PICO network check:

```powershell
$adb='E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
& $adb shell "ip -4 addr show wlan0; ip route"
```

Expected PICO subnet:

```text
192.168.137.38/24
192.168.137.0/24 dev wlan0
```

C# compile check:

```powershell
dotnet build Client/Assembly-CSharp.csproj -v:minimal
```

Result observed: `0` errors. Existing warnings are from PICO SDK and pre-existing Unity project code.

## Next Real-Device Test

Rebuild and reinstall the APK after these changes. The already-installed PICO build will not pick up modified Unity assets or C# code.

During the PICO test, collect logs with:

```powershell
$adb='E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
& $adb logcat -c
# Run the scene generation flow on PICO.
& $adb logcat -d -v time Unity:I '*:S' |
  Select-String -Pattern 'SceneTalkVR|PanoramaSceneService|HybridScenePresenter|Shader|Skybox|fallback|192\.168\.137\.1|8787|8788'
```

Useful success indicators:

- `[SceneTalkVR] Runtime config applied`
- `[PanoramaSceneService] API Key missing. Using local fallback.`
- `[PanoramaSceneService] Shader 'Skybox/Panoramic' not found. Falling back to sky sphere.`
- `[PanoramaSceneService] Background applied successfully.`

## Longer-Term Note

This fix makes the fallback panorama display reliably on PICO. If PICO must display newly generated online panoramas, the safer next architecture is a PC-side panorama/image gateway, similar to `voice-gateway` and `llm-gateway`, so cloud image credentials stay on the PC/server side rather than inside the APK.
