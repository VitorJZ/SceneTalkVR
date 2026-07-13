# PICO 真机网络与双 Gateway 运行记录（2026-07-13）

本文记录当前 PICO 真机运行所需的 PC 端网络、语音网关和 LLM 网关配置。它用于交接和复现，不替代 Unity 内的 `PicoRealRunGuide.md`，后者仍是通用真机运行指南。

## 当前验证结论

- PC hotspot gateway IP：`192.168.137.1`
- PICO IP：本次网络切换后未在 ARP 表中确认；请以 PICO 当前网络详情页为准。
- PICO 需要与 PC 在同一可互通网段，并能访问 PC 的 `8787` / `8788` 端口。
- `voice-gateway` 已监听 `0.0.0.0:8787`，健康检查通过。
- `llm-gateway` 已监听 `0.0.0.0:8788`，健康检查通过。
- PC 直连上游 LLM 返回 HTTP 200。
- PC 通过 `llm-gateway` 转发 LLM 请求返回 HTTP 200。
- Unity 当前配置指向 PC 局域网地址：
  - `voiceGatewayBaseUrl`: `http://192.168.137.1:8787`
  - `directLlmApiUrl`: `http://192.168.137.1:8788/api/llm/chat/completions`

## Clash / VPN 状态

关闭 Clash 后，当前检查结果如下：

- `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 未设置。
- WinHTTP 为 `Direct access`。
- WinINET `ProxyEnable=0`。
- `7890`、`7897`、`7899` 未监听。
- `clash-core-service` / `clash-verge-service` 进程仍可能存在。
- DNS 仍可能把部分域名解析成 `198.18.2.x` fake-ip，但当前 LLM 直连和 gateway 转发都已成功，所以这不是当前阻塞点。

如果后续网络再次失败，优先检查：

```powershell
Get-ChildItem Env:HTTP_PROXY,Env:HTTPS_PROXY,Env:ALL_PROXY,Env:NO_PROXY -ErrorAction SilentlyContinue
netsh winhttp show proxy
Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings | Select-Object ProxyEnable,ProxyServer,AutoConfigURL
Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -in 8787,8788,7890,7897,7899 }
Resolve-DnsName models.sjtu.edu.cn
```

如果 DNS 仍是 fake-ip 但请求可用，可以继续验证。只有当请求失败、超时或 TLS 异常时，再处理 Clash core、VPN 或 DNS。

## 启动 voice-gateway

PICO 访问 PC 服务时不能使用 `127.0.0.1` 或 `localhost`。即使 `Server/voice-gateway/voice-gateway.local.json` 里写的是 `127.0.0.1`，也可以在启动时用环境变量覆盖为 `0.0.0.0`。

PowerShell：

```powershell
cd E:\Project\Unity\SceneTalkVR\Server\voice-gateway
$env:VOICE_GATEWAY_HOST="0.0.0.0"
$env:VOICE_GATEWAY_PORT="8787"
$env:VOICE_GATEWAY_PROVIDER="tencent"
python -m src.voice_gateway.main
```

健康检查：

```powershell
Invoke-RestMethod http://192.168.137.1:8787/health | ConvertTo-Json -Compress
```

期望结果：

```json
{"status":"ok","provider":"tencent"}
```

## 启动 llm-gateway

`llm-gateway` 用于把 PICO/Unity 的 LLM 请求转发到 OpenAI-compatible Chat Completions API，并把 API Key 留在 PC 端。它会从环境变量、本地 JSON 或仓库根目录 `.env` 读取 `OPENAI_API_KEY`。

PowerShell：

```powershell
cd E:\Project\Unity\SceneTalkVR\Server\llm-gateway
$env:LLM_GATEWAY_HOST="0.0.0.0"
$env:LLM_GATEWAY_PORT="8788"
python -m src.llm_gateway.main
```

健康检查：

```powershell
Invoke-RestMethod http://192.168.137.1:8788/health | ConvertTo-Json -Compress
```

期望结果中应包含：

```json
{"status":"ok","hasApiKey":true}
```

LLM 转发 smoke test：

```powershell
$body='{"model":"minimax-m2.7","messages":[{"role":"user","content":"Return only ok."}],"max_tokens":8}'
$body | curl.exe -sS -m 90 -o NUL -w "llm_gateway_http=%{http_code} elapsed=%{time_total} remote=%{remote_ip}`n" -H "Content-Type: application/json" --data-binary "@-" http://192.168.137.1:8788/api/llm/chat/completions
```

期望结果：

```text
llm_gateway_http=200
```

## Unity 配置检查

当前真机配置应至少满足：

- `Client/Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset`
  - `voiceGatewayBaseUrl`: `http://192.168.137.1:8787`
  - `directLlmApiUrl`: `http://192.168.137.1:8788/api/llm/chat/completions`
- `Client/Assets/Scenes/SampleScene.unity`
  - `VoiceGatewayClient.gatewayBaseUrl`: `http://192.168.137.1:8787`
  - `RealLLMService.apiUrl`: `http://192.168.137.1:8788/api/llm/chat/completions`
  - `RealLLMService.apiKey`: 为空
  - `PanoramaSceneService.apiKey`: 为空

不要把云 API Key 写入 Unity scene、Unity asset、`.meta` 或 APK。PICO 真机路径应通过 PC 端 gateway 访问云服务。

如果切换网络后 PC IP 改变，需要同步更新上述 Unity 地址，并重新构建或重新安装 APK。

## Holodeck 注意事项

当前 `SampleScene.unity` 中仍有 Holodeck 示例地址：

```text
http://localhost:8080/generate_scene
```

如果真机流程不启用 Holodeck，可以先保持不动。如果要让 PICO 直接访问 Holodeck 后端，必须改为 PC 局域网地址，例如：

```text
http://192.168.137.1:8080/generate_scene
```

并确保 Holodeck 监听 `0.0.0.0:8080`，Windows 防火墙放行 `8080`。

## 真机验证顺序

1. PC 启动 `voice-gateway`，确认 `8787/health` 通过。
2. PC 启动 `llm-gateway`，确认 `8788/health` 通过。
3. 确认 Unity 配置使用 PC hotspot gateway IP，而不是 `localhost`。
4. 重新构建并安装 PICO APK。
5. 在 PICO 中进入流程，点击 `Listen`，说一句英文，点击 `End`。
6. 确认 transcript 正确显示。
7. 点击 `Confirm`，确认场景生成或对话生成不再报 `API Key is not set`。
8. 确认 Avatar 出现，并且 TTS 能播放。

## 快速定位

检查两个 gateway 是否监听：

```powershell
Get-NetTCPConnection -LocalPort 8787,8788 -State Listen | Select-Object LocalAddress,LocalPort,OwningProcess
```

检查 PICO 是否仍在同一网段：

```powershell
arp -a | Select-String '192\.168\.137\.'
Test-NetConnection <pico-current-ip>
```

检查 Unity 配置是否仍指向当前 PC IP：

```powershell
rg -n "voiceGatewayBaseUrl|directLlmApiUrl|gatewayBaseUrl|apiUrl|8787|8788|localhost:8080" Client/Assets/SceneTalkVR/RuntimeConfig Client/Assets/Scenes/SampleScene.unity
```

检查 LLM gateway 日志：

```powershell
Get-Content E:\Project\Unity\SceneTalkVR\tmp\llm-gateway.log -Tail 80
```

检查 voice gateway 日志：

```powershell
Get-Content E:\Project\Unity\SceneTalkVR\tmp\voice-gateway.log -Tail 80
Get-Content E:\Project\Unity\SceneTalkVR\tmp\voice-gateway.err.log -Tail 80
```

## 关联记录

- `documents/pico-panorama-real-device-fix-2026-07-13.md`：记录 PC Editor 可显示 360 全景但 PICO 真机不显示的原因、修复点和重新打包后的验证步骤。
