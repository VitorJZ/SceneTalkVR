# PICO—电脑 USB 数据通道实施说明

日期：2026-07-28

## 实施结果

项目现在使用统一传输状态机管理 STT、LLM 和 TTS。默认 PICO 运行策略为 `UsbPreferred`：先同时验证 USB 路由下的 Voice Gateway 与 LLM Gateway；两个端点都健康才选择 USB，失败后再验证并选择 LAN。旧配置没有传输版本或新枚举字段时保持 `LanOnly`，不会静默改变已有部署行为。

```text
PICO 127.0.0.1:8787 ──ADB reverse / USB──> PC Voice Gateway
PICO 127.0.0.1:8788 ──ADB reverse / USB──> PC LLM Gateway
```

USB 仅替代 PICO 到电脑的无线链路，电脑仍需联网访问腾讯云与 LLM 上游。

## 电脑端启动

1. 在 PICO 开启开发者模式和 USB 调试，并使用数据线连接电脑。
2. 在仓库根目录运行：

```bash
python Server/gateway-launcher/scenetalk_gateway_launcher.py
```

多台已授权 Android 设备同时连接时必须指定 PICO：

```bash
python Server/gateway-launcher/scenetalk_gateway_launcher.py --serial <adb-serial>
```

启动器会定位 ADB、启动或复用两个兼容网关、验证 `/health`、建立 `8787/8788` 反向映射，并在拔插数据线或 ADB 重启后恢复映射。退出时只清理它自己创建的映射和子进程。日志不会输出密钥。

## Unity 状态与重试

传输状态为：`Uninitialized → ProbingUsb/ProbingLan → UsbReady/LanReady`，两个路径都不可用时进入 `Unavailable`。设置页只读显示：

- `USB 数据线`
- `局域网备用`
- `正在连接`
- `不可用`

正式实验和 Pilot 在路由未就绪时不能开始 attempt，且不会切换到 mock。每个新 attempt 会要求 USB 优先路由重新探测；每个 STT 回合边界也会重试 USB。对话 Error 页的 Retry 会触发重新探测。

跨路由切换只由连接失败或连接超时触发。HTTP 4xx、provider 错误与模型错误保留原错误，不通过换路由掩盖。STT 复用已缓存的 WAV JSON；TTS 在备用路由重新执行 POST 与完整 WAV 下载，并验证完整文本确认、格式、采样率和可播放性；LLM 只在零响应字节时允许换路由重发，一旦收到任何 SSE 字节就禁止自动重发，避免重复回复或半段语音。

传输选择和切换会写入 `Application.persistentDataPath/SceneTalkVR/TransportAudit/transport-events.jsonl`，仅包含时间、传输类型、请求阶段和受限失败原因，不包含音频、对话文本或密钥。

## 自动验证

```bash
python -m unittest discover -s Server/gateway-launcher/tests -v
```

当前自动检查覆盖 ADB 定位、授权与多设备选择、映射冲突、健康检查、断线恢复、进程/映射所有权、密钥脱敏、USB/LAN 状态迁移、旧配置兼容、loopback 策略、中文状态、TTS 音频元数据与 LLM 部分 SSE 禁止换路重发。

## 真机验收清单

以下项目必须在 PICO 与现场网络上执行，不能由编辑器单元测试替代：

- 关闭 PICO Wi-Fi 后完成 STT→LLM→TTS。
- 连续 30 个完整对话回合无 PICO—电脑连接错误。
- 拔线后下一请求回退 LAN，重新插线后下一回合恢复 USB。
- LLM 流式返回与 TTS 下载阶段分别拔线，确认没有重复回复或不完整语音。
- 重启 ADB 后 5 秒内恢复两个反向映射。

PICO 设备验证仍属于 rehearsal，不能因此获得正式采集批准。
