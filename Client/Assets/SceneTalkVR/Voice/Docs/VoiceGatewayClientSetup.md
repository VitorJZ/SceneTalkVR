# Voice Gateway Client Setup

This client connects Unity to the local P0 voice gateway.

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
3. In Unity, open `Assets/SceneTalkVR/Voice/VoiceGatewaySettings.asset`.
4. Set `gatewayBaseUrl` to:

```text
http://192.168.1.20:8787
```

Do not use `127.0.0.1` on PICO or another teammate's computer. `127.0.0.1` always points to the current device itself.

## Unity Setup

For the P0 STT step:

1. Open or create `Assets/SceneTalkVR/Voice/VoiceGatewaySettings.asset`.
2. Set `gatewayBaseUrl`.
3. Add `VoiceGatewayClient` to the `SceneTalkVR Demo Rig`.
4. Add `MicrophoneRecorder` to the same object.
5. Add `GatewaySpeechInputModule` to the same object.
6. Assign `GatewaySpeechInputModule` to `SceneTalkOrchestrator.speechInputModule`.

You can use `SceneTalkVR/Setup/Rebuild Demo Rig With Voice Gateway` to rebuild the demo rig with:

- `VoiceGatewayClient`
- `MicrophoneRecorder`
- `GatewaySpeechInputModule`
- `AvatarPresentationVoiceModule.useVoiceGatewayTts` enabled
- `VoiceGatewaySettings.asset` assigned to `VoiceGatewayClient`

## P0 Status

`GatewaySpeechInputModule` records a short WAV clip from Unity's default microphone and sends it as base64 to the gateway. `AvatarPresentationVoiceModule` can request TTS audio from the same gateway and play the downloaded WAV.

P0 has been verified in Unity Editor with `VOICE_GATEWAY_PROVIDER=tencent`:

- STT returns a Tencent transcript from microphone audio.
- The transcript enters the existing `SceneTalkOrchestrator` flow.
- Avatar replies are synthesized through Tencent TTS and played as downloaded WAV audio.

Mock mode remains available for offline demos. PICO 4 microphone/playback validation, manual stop/VAD, streaming, interruption, and richer production logging are follow-up work.
