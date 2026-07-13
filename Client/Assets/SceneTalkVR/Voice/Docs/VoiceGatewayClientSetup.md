# Voice Gateway Client Setup

This client connects Unity to the local P0 voice gateway.

For the current PICO real-device network notes, see `../../../../../documents/pico-real-device-gateway-runbook-2026-07-13.md`.

## Start the Gateway

```bash
cd Server/voice-gateway
python3 -m src.voice_gateway.main
```

Default endpoint:

```text
http://127.0.0.1:8787
```

## Shared LAN Gateway

For team development, run `voice-gateway` on one teammate's computer and let everyone else connect to that machine.

1. On the gateway host computer, start `Server/voice-gateway`.
2. Find that computer's LAN IP, for example `192.168.1.20`.
3. In Unity, open `Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset`.
4. Set `voiceGatewayBaseUrl` to:

```text
http://192.168.1.20:8787
```

`VoiceGatewaySettings.asset` remains as a legacy/default fallback. PICO real-device builds should prefer the runtime config profile because it is also checked by the preflight report.

Do not use `127.0.0.1` on PICO or another teammate's computer. `127.0.0.1` always points to the current device itself.

For PICO, the gateway process must bind to `0.0.0.0`, not only `127.0.0.1`:

```powershell
$env:VOICE_GATEWAY_HOST="0.0.0.0"
$env:VOICE_GATEWAY_PORT="8787"
$env:VOICE_GATEWAY_PROVIDER="tencent"
python -m src.voice_gateway.main
```

## Unity Setup

For the P0 STT step:

1. Run `SceneTalkVR/Setup/Configure PICO Real Run Profile`.
2. Open `Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset` and set `voiceGatewayBaseUrl`.
3. Add `VoiceGatewayClient` to the `SceneTalkVR Demo Rig`.
4. Add `MicrophoneRecorder` to the same object.
5. Add `GatewaySpeechInputModule` to the same object.
6. Assign `GatewaySpeechInputModule` to `SceneTalkOrchestrator.speechInputModule`.

You can use `SceneTalkVR/Setup/Rebuild Demo Rig With Voice Gateway` to configure the existing demo rig with:

- `VoiceGatewayClient`
- `MicrophoneRecorder`
- `GatewaySpeechInputModule`
- `AvatarPresentationVoiceModule.useVoiceGatewayTts` enabled
- `VoiceGatewaySettings.asset` assigned to `VoiceGatewayClient`

This menu item only changes the voice path on the existing rig. It does not rebuild scene presentation, UI, avatar placement, or prefab bindings.

## P0 Status

`GatewaySpeechInputModule` records WAV audio from Unity's default microphone and sends it as base64 to the gateway. The current Unity flow supports manual stop: the request `Listen` button switches to `End` while recording, the dialogue `Speak` button switches to `End`, and PICO/OpenXR triggers can hold-to-record when the ray is not over a UI button. `AvatarPresentationVoiceModule` can request TTS audio from the same gateway and play the downloaded WAV.

P0 has been verified in Unity Editor with `VOICE_GATEWAY_PROVIDER=tencent`:

- STT returns a Tencent transcript from microphone audio.
- The transcript enters the existing `SceneTalkOrchestrator` flow.
- Avatar replies are synthesized through Tencent TTS and played as downloaded WAV audio.

Mock mode remains available for offline demos. PICO 4 microphone/playback validation, VAD, streaming, interruption, and richer production logging are follow-up work.
