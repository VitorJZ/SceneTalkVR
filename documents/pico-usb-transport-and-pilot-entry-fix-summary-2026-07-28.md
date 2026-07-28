# PICO USB 数据通道与预实验入口修复汇总

日期：2026-07-28

## 工作范围

本次未提交改动实现了 PICO—电脑之间的 USB 优先数据通道，并统一管理 STT、LLM、TTS 的 Voice/LLM 网关路由。同时结合 PICO 真机调试，修复了 USB 后台异常退出以及预实验无法进入对话的问题。

USB 通道使用 ADB reverse 复用现有 HTTP、JSON、SSE 与 WAV 协议：

```text
PICO 127.0.0.1:8787 ──USB/ADB──> PC Voice Gateway
PICO 127.0.0.1:8788 ──USB/ADB──> PC LLM Gateway
```

USB 只替代 PICO 到电脑的局域网链路。电脑仍需联网访问腾讯云与 LLM 上游。

## Unity 传输状态机

新增 `GatewayTransportRouter`，使用以下配置和运行状态：

- 偏好：`LanOnly`、`UsbPreferred`、`UsbOnly`。
- 状态：`Uninitialized`、`ProbingUsb`、`UsbReady`、`ProbingLan`、`LanReady`、`Unavailable`。
- 路由快照同时包含 Voice 与 LLM 地址，只有两个 `/health` 端点都兼容时才选择该路由。
- `UsbPreferred` 先探测 USB，失败后才探测 LAN；单个逻辑请求只允许一次跨路由切换。
- 旧 Runtime Config 没有传输配置版本时保持 `LanOnly`，避免旧资源静默改变行为。
- PICO 普通 LAN 配置仍禁止 loopback；只有显式 USB 策略允许 `127.0.0.1`。
- 正式实验、Pilot 和 Rehearsal 在要求真实服务且路由未就绪时禁止开始 attempt，不会回退到 mock。

设置界面增加只读“数据通道”状态，显示“USB 数据线”“局域网备用”“正在连接”或“不可用”，参与者不能修改传输策略。

传输选择、切换、失败阶段与受限错误原因写入：

```text
Application.persistentDataPath/SceneTalkVR/TransportAudit/transport-events.jsonl
```

审计记录不包含音频、对话文本或密钥。

## STT、LLM 与 TTS 行为

### STT

- 每个新语音回合重新探测 USB 优先路由。
- WAV 已编码到请求 JSON 后可以在备用路由重试，不要求参与者重新录音。
- HTTP 4xx、provider 或业务错误不触发换路由。

### LLM

- 普通与流式请求的地址均来自统一路由快照。
- 保留单路由原有瞬时重试，再执行最多一次跨路由恢复。
- 只有连接失败且未收到任何响应字节时允许跨路由重发。
- 流式请求收到任意 SSE 字节后禁止自动重发，避免重复回复以及后续半段语音。

### TTS

- TTS POST 与 WAV 下载固定使用同一路由。
- 音频 URL 会固定到所选路由主机，避免 USB 请求返回 LAN 绝对地址后绕回无线链路。
- 连接失败时在备用路由从 TTS POST 开始完整重试。
- 播放前继续验证文本字符数、provider、WAV 格式、采样率和 AudioClip 可播放性。

## 电脑端统一启动器

新增 `Server/gateway-launcher/scenetalk_gateway_launcher.py`：

- 定位 ADB，选择唯一已授权 PICO，或通过 `--serial` 指定设备。
- 启动或复用 `8787` Voice Gateway 与 `8788` LLM Gateway。
- 校验两个 `/health` 端点后建立并验证 ADB reverse。
- 检测拔线、重连和映射丢失并自动恢复。
- 端口冲突时明确停止，不覆盖其他映射。
- 退出时只清理启动器自己创建的映射和子进程。
- 日志错误经过长度限制和密钥脱敏。

现场调试发现启动器的一次 `adb devices -l` 超过 10 秒后抛出 `subprocess.TimeoutExpired`。原实现只捕获 `LauncherError`，因此监控进程退出，退出清理又删除了 `8787/8788` 映射，最终在 PICO 侧表现为：

```text
gateway_transport_not_ready:ProbingLan
```

本次审查已将 ADB 超时转换为可恢复的 `LauncherError`。监控循环会记录警告并继续运行，不再因单次 ADB 卡顿退出和删除映射，并增加对应 Python 回归测试。

## 预实验入口故障与修复

真机日志中的错误为：

```text
[PilotCollection] gateway_transport_reprobe_in_progress
```

当时 USB 路由实际健康，并且每次错误后约几十毫秒都能重新选择 `Usb`。根因是 PICO 设备验证路径在同一次“开始任务”操作中连续调用两次 `CanStartLiveAttempt`：

1. `PilotCollectionSessionCoordinator.BeginCurrentTask` 首次授权当前路由。
2. 随后调用 `RehearsalSessionCoordinator.PreparePilotCondition`。
3. `PreparePilotCondition` 再次授权，同一路由被误判为需要重新探测，导致预实验永久停留在任务介绍页。

修复后每条路径只有一个授权责任方：

- 普通 Pilot：由 `PilotCollectionSessionCoordinator` 校验。
- PICO 设备验证：由 `RehearsalSessionCoordinator` 校验。
- 正式采集与正式 Rehearsal：各入口校验一次。
- Rehearsal 的自动准备和技术重试入口也补充了路由就绪校验。

新增 PlayMode 回归用例构造一个已就绪 USB 路由，验证 PICO 设备验证一次点击即可进入 `Dialogue`。测试结束时恢复原 `GatewayTransportRouter.Active`，避免污染后续测试。

## 配置与资源

Runtime Config 新增：

- 传输配置版本。
- USB Voice loopback 地址。
- USB LLM loopback 地址。
- 传输偏好。
- 健康探测超时。

Deployment Profile 新增 LLM 地址和传输偏好。PICO 设备验证使用 `UsbPreferred`，LAN 地址继续保留为自动备用；编辑器资源保持 `LanOnly`。

网关 README、预检说明和资源生成器已同步更新。详细运行说明另见 `documents/pico-usb-data-channel-implementation-2026-07-28.md`。

## 自动验证结果

已完成：

- `python -m unittest discover -s Server/gateway-launcher/tests -v`：10 项通过。
- `python -m py_compile Server/gateway-launcher/scenetalk_gateway_launcher.py`：通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：0 错误。
- `dotnet build SceneTalkVR.Stage2.PlayModeTests.csproj --no-restore --nologo`：0 错误。
- Unity 编辑器最新日志未发现新增 C# 编译错误。
- `git diff --check`：通过。

当前桌面会话没有暴露 Unity MCP 测试资源，且 Unity 编辑器已打开，因此没有另外启动第二个 Unity 实例执行 Test Runner。新增测试已经完成编译。

## 真机状态与后续验收

调试期间已确认 PICO A8110 通过 ADB 授权连接，电脑端 Voice/LLM `/health` 正常，并成功建立 `8787/8788` USB 映射。源代码修改需要重新构建并安装 APK 后才会在 PICO 上生效。

发布前仍应完成以下真机验收：

- 关闭 PICO Wi-Fi，完成完整 STT → LLM → TTS。
- 连续完成至少 30 个完整对话回合。
- 拔线后确认下一请求回退 LAN，重新插线后在下一回合恢复 USB。
- 在 LLM 流式返回和 TTS 下载阶段分别模拟拔线，确认没有重复回复或不完整语音。
- 重启 ADB 后确认启动器持续运行并在目标时间内恢复双端口映射。
- 在新 APK 中分别进入正式实验和预实验，确认不会再出现重复 `gateway_transport_reprobe_in_progress`。
