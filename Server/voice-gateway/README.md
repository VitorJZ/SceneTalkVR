# SceneTalkVR Voice Gateway

Minimal P0 voice gateway for SceneTalkVR.

Current PICO real-device network notes live in `../../documents/pico-real-device-gateway-runbook-2026-07-13.md`.

The gateway hides cloud-provider credentials from Unity/PICO clients and exposes a stable local protocol:

```text
Unity/PICO -> Voice Gateway -> STT provider
Unity/PICO -> Voice Gateway -> TTS provider -> audio playback
```

This implementation supports:

- `mock`: deterministic transcript + generated WAV tone.
- `tencent`: Tencent Cloud ASR `SentenceRecognition` + TTS `TextToVoice`.

When `VOICE_GATEWAY_PROVIDER=tencent`, `TENCENT_FALLBACK_TO_MOCK=true` keeps the local demo usable if credentials or cloud calls fail.

## Run

```bash
cd Server/voice-gateway
python3 -m src.voice_gateway.main
```

Default URL:

```text
http://127.0.0.1:8787
```

For teammates on the same LAN, use the host computer's LAN IP from Unity:

```text
http://<gateway-host-lan-ip>:8787
```

Example:

```text
http://192.168.1.20:8787
```

Current project smoke-test example:

```text
http://172.20.10.4:8787
```

PICO and other teammates cannot use `127.0.0.1` to reach your computer. On those devices, `127.0.0.1` points to themselves.

For PICO, start the gateway on all interfaces:

```powershell
$env:VOICE_GATEWAY_HOST="0.0.0.0"
$env:VOICE_GATEWAY_PORT="8787"
$env:VOICE_GATEWAY_PROVIDER="tencent"
python -m src.voice_gateway.main
```

Optional environment variables:

```bash
VOICE_GATEWAY_HOST=127.0.0.1
VOICE_GATEWAY_PORT=8787
VOICE_GATEWAY_PROVIDER=mock
VOICE_GATEWAY_MOCK_TRANSCRIPT="I want to practice ordering coffee."
```

Tencent provider variables:

```bash
VOICE_GATEWAY_PROVIDER=tencent
TENCENT_SECRET_ID="..."
TENCENT_SECRET_KEY="..."
TENCENT_REGION=ap-guangzhou
TENCENT_ASR_ENGINE=16k_en
TENCENT_TTS_VOICE_TYPE=1051
TENCENT_FALLBACK_TO_MOCK=true
```

Optional local config file, for teammates who do not want to export variables every run:

```bash
cd Server/voice-gateway
cp voice-gateway.local.example.json voice-gateway.local.json
```

Then edit `voice-gateway.local.json` with the local Tencent key. This file is ignored by Git and should stay local to each teammate's machine.

Defaults:

- ASR endpoint: `asr.tencentcloudapi.com`
- TTS endpoint: `tts.tencentcloudapi.com`
- ASR action/version: `SentenceRecognition` / `2019-06-14`
- TTS action/version: `TextToVoice` / `2019-08-23`
- English ASR engine: `16k_en`
- English TTS voice: `1051` (`WeRose`), with `1050` (`WeJack`) also available.

## Tencent Cloud Setup

1. Create or log in to a Tencent Cloud account.
2. Open and activate ASR: https://cloud.tencent.com/product/asr
3. Open and activate TTS: https://cloud.tencent.com/product/tts
4. Create an API key in CAM: https://console.cloud.tencent.com/cam/capi
5. Put the key in either your local terminal environment or `voice-gateway.local.json`. Do not write real keys into tracked Git files, Unity `.meta` files, or screenshots.

Example local run:

```bash
cd Server/voice-gateway
export VOICE_GATEWAY_PROVIDER=tencent
export TENCENT_SECRET_ID="AKID..."
export TENCENT_SECRET_KEY="..."
export TENCENT_REGION=ap-guangzhou
python3 -m src.voice_gateway.main
```

Example local-file run:

```bash
cd Server/voice-gateway
cp voice-gateway.local.example.json voice-gateway.local.json
# Edit voice-gateway.local.json with your local Tencent key.
python3 -m src.voice_gateway.main
```

## Endpoints

### Health

```bash
curl http://127.0.0.1:8787/health
```

### STT

```bash
curl -s http://127.0.0.1:8787/api/voice/stt \
  -H 'Content-Type: application/json' \
  -d '{
    "sessionId": "demo-session",
    "sampleRate": 16000,
    "channels": 1,
    "format": "wav",
    "language": "en-US",
    "sceneType": "ordering_coffee",
    "audioBase64": ""
  }'
```

### TTS

```bash
curl -s http://127.0.0.1:8787/api/voice/tts \
  -H 'Content-Type: application/json' \
  -d '{
    "sessionId": "demo-session",
    "turnId": "turn-001",
    "text": "Good morning! What can I get for you today?",
    "language": "en-US",
    "voiceProfile": {
      "provider": "tencent",
      "role": "barista",
      "speakingSpeed": "fast",
      "accent": "american",
      "attitude": "friendly"
    },
    "output": {
      "format": "wav",
      "sampleRate": 24000
    }
  }'
```

The response contains an `audioUrl`. Download it from the same gateway host.

## Current Scope

- Mock STT/TTS are available for offline demos.
- Tencent ASR/TTS are available through `VOICE_GATEWAY_PROVIDER=tencent`.
- Tencent cloud keys are read from environment variables or local `voice-gateway.local.json`.
- No user audio or transcript is persisted by the gateway.
- P0 live smoke testing has been completed with Tencent ASR/TTS and Unity Editor.

Next step: validate the same gateway path on PICO 4 and add richer runtime logging, VAD/manual stop, and production error handling.
