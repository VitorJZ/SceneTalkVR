# SceneTalkVR LLM Gateway

Local PC-side gateway for SceneTalkVR real-device LLM calls.

Current PICO real-device network notes live in `../../documents/pico-real-device-gateway-runbook-2026-07-13.md`.

```text
PICO/Unity -> LLM Gateway -> OpenAI-compatible Chat Completions API
```

The gateway keeps the real `OPENAI_API_KEY` on the PC/server. PICO builds call the LAN gateway URL and do not need to embed the LLM key in the APK.

## Run

```bash
cd Server/llm-gateway
python -m src.llm_gateway.main
```

Default URL:

```text
http://127.0.0.1:8788
```

For PICO, bind to LAN:

```powershell
$env:LLM_GATEWAY_HOST="0.0.0.0"
$env:LLM_GATEWAY_PORT="8788"
python -m src.llm_gateway.main
```

Then point Unity to:

```text
http://<pc-lan-ip>:8788/api/llm/chat/completions
```

Current project smoke-test example:

```text
http://172.20.10.4:8788/api/llm/chat/completions
```

The concrete LAN IP changes when the PC changes Wi-Fi/hotspot/VPN environment. After a network change, update Unity runtime config and rebuild/reinstall the PICO APK.

## Configuration

The gateway reads credentials from environment variables, `llm-gateway.local.json`, or the repository root `.env`.

Supported environment variables:

```bash
LLM_GATEWAY_HOST=127.0.0.1
LLM_GATEWAY_PORT=8788
LLM_GATEWAY_UPSTREAM_URL=https://models.sjtu.edu.cn/api/v1/chat/completions
LLM_GATEWAY_API_KEY="..."
LLM_GATEWAY_TIMEOUT_SECONDS=60
LLM_GATEWAY_TRANSPORT=auto
LLM_GATEWAY_CURL_PATH=curl.exe
LLM_GATEWAY_CURL_SSL_NO_REVOKE=true
```

If `LLM_GATEWAY_API_KEY` is empty, the gateway falls back to `OPENAI_API_KEY`.

`LLM_GATEWAY_TRANSPORT=auto` first tries Python's standard HTTPS client and falls back to `curl` if the local VPN/proxy stack resets that connection. On Windows with Clash/VPN, keep `LLM_GATEWAY_CURL_SSL_NO_REVOKE=true` if `curl` otherwise fails with a certificate revocation check error.

Optional local config file:

```bash
cd Server/llm-gateway
cp llm-gateway.local.example.json llm-gateway.local.json
```

`llm-gateway.local.json` is ignored by Git and should stay local.

## Endpoints

Health:

```bash
curl http://127.0.0.1:8788/health
```

Chat completions:

```bash
curl http://127.0.0.1:8788/api/llm/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "minimax-m2.7",
    "messages": [
      {"role": "user", "content": "Return only ok."}
    ]
  }'
```

The response body is the upstream OpenAI-compatible response.
