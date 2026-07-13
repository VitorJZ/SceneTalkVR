# PICO Real Run Guide

This guide is for running the real SceneTalkVR path on a PICO device after the demo path already launches.

For the current validated Windows + PICO network runbook, see `../../../../documents/pico-real-device-gateway-runbook-2026-07-13.md`.

## Target Runtime Shape

```text
PICO APK
  -> LAN PC/server voice gateway
  -> LAN PC/server LLM gateway
  -> LAN PC/server Holodeck backend or local mock layout fallback
  -> Unity local Avatar catalog and low-poly prefab catalog
```

Do not point PICO builds at `127.0.0.1` or `localhost` unless the service is actually running inside the headset. On PICO those addresses refer to the headset itself, not the development PC.

## 1. Start LAN Services

Voice gateway:

```bash
cd Server/voice-gateway
VOICE_GATEWAY_HOST=0.0.0.0 VOICE_GATEWAY_PROVIDER=tencent python3 -m src.voice_gateway.main
```

Windows PowerShell:

```powershell
cd E:\Project\Unity\SceneTalkVR\Server\voice-gateway
$env:VOICE_GATEWAY_HOST="0.0.0.0"
$env:VOICE_GATEWAY_PORT="8787"
$env:VOICE_GATEWAY_PROVIDER="tencent"
python -m src.voice_gateway.main
```

For a safer first device smoke test, use `VOICE_GATEWAY_PROVIDER=mock`.

LLM gateway:

```bash
cd Server/llm-gateway
LLM_GATEWAY_HOST=0.0.0.0 python3 -m src.llm_gateway.main
```

Windows PowerShell:

```powershell
cd E:\Project\Unity\SceneTalkVR\Server\llm-gateway
$env:LLM_GATEWAY_HOST="0.0.0.0"
$env:LLM_GATEWAY_PORT="8788"
python -m src.llm_gateway.main
```

The LLM gateway reads `OPENAI_API_KEY` from the PC environment or the repository root `.env`. PICO builds should point `directLlmApiUrl` to the PC LAN URL, for example:

```text
http://192.168.1.20:8788/api/llm/chat/completions
```

Holodeck backend, if enabled:

```bash
cd Holodeck
python -m uvicorn app:app --host 0.0.0.0 --port 8080
```

If Holodeck runs in WSL, make sure the Windows LAN IP forwards to the WSL port. Also allow Windows Firewall inbound traffic on `8787`, `8788`, and `8080`.

## 2. Configure Unity Runtime Profile

1. Open `Assets/Scenes/SampleScene.unity`.
2. Run `SceneTalkVR/Setup/Configure PICO Real Run Profile`.
3. Open `Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset`.
4. Set `voiceGatewayBaseUrl` to the gateway host LAN address, for example:

```text
http://192.168.1.20:8787
```

5. Set `directLlmApiUrl` to the LLM gateway URL:

```text
http://192.168.1.20:8788/api/llm/chat/completions
```

6. Keep the Unity-side LLM API key fields empty when using the PC LLM gateway. The key should stay in the PC environment, local JSON config, or repository root `.env`, not in Unity scene/assets.

7. If using Holodeck, enable `useHolodeckBackend` and set:

```text
http://192.168.1.20:8080/generate_scene
```

For the most stable first PICO run, keep `useHolodeckBackend` disabled. The Unity client will use the local mock layout while still exercising real STT/TTS, real Avatar loading, and the scene presenter.

## 3. Build Preflight

Run:

```text
SceneTalkVR/Setup/Apply Recommended Project Settings
SceneTalkVR/Diagnostics/Run Preflight Check
```

The report is written to:

```text
Assets/SceneTalkVR/Docs/VitorPreflightReport.md
```

For real PICO runs, the `PICO Real Service Routing` section should show:

- runtime config exists
- runtime config applier exists in scene
- voice gateway URL is configured
- voice gateway URL is not localhost
- Brain module/profile uses a real LLM path
- LLM gateway URL is not localhost for PICO
- Holodeck URL is not localhost if Holodeck is enabled

## 4. Device Smoke Test Order

1. Build & Run to PICO.
2. Confirm HMD 6DOF tracking and controller rays.
3. Tap `Start`.
4. Record a short request and grant microphone permission when prompted.
5. Confirm the STT transcript is not the demo fallback.
6. Confirm the scene enters loading and Avatar appears.
7. Confirm TTS audio plays through the voice gateway.
8. Try one follow-up dialogue turn.

If a cloud or LAN service fails, first verify the PICO can reach the PC/server IP. Then switch the voice gateway to mock or disable Holodeck backend to isolate the failing layer.

## Notes

- Keep cloud keys on the PC/server side when possible. The current direct `RealLLMService` can be used for local demos, but a production path should proxy LLM and panorama calls through a server gateway.
- Keep `maxSpawnCount` low on PICO. The default runtime config uses `2`.
- Keep `enableSpatialClipping` enabled to prevent generated near-field props from colliding with panorama content.
