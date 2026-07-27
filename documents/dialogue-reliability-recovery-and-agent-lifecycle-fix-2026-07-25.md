# 对话可靠性、失败恢复与 Agent 生命周期修复汇总

日期：2026-07-25

## 1. 背景与问题

现象是：部分情况下语音识别已经成功，界面也没有显示网络警告，但角色没有得到 LLM 回复，用户只能停留在无反馈状态。此外，纠错 Agent 在多轮对话中可能消失，纠错音频也可能因为音源尚未激活而没有真正播放。

本次工作先沿现有调用链进行了检查，再在既有架构内补齐可靠性和恢复逻辑。当前主链路为：

1. `SceneTalkOrchestrator` 完成语音录制和 ASR，取得 transcript。
2. `RealLLMService` 并行请求对话内容与纠错内容；对话请求优先使用 SSE 流式返回。
3. `AvatarPresentationVoiceModule` 接收完整或分句结果，准备 Avatar、纠错反馈和 TTS 播放。
4. 本地 `llm-gateway` 负责把 Unity 请求转发到上游 OpenAI 兼容接口。
5. 当前 turn 结束后继续等待下一次录音；只有 condition/session 结束时才应清理常驻 Agent。

## 2. 根因分析

### 2.1 HTTP 成功，但响应格式被网关改写

Unity 流式请求发送了 `Accept: text/event-stream`，旧网关却固定向上游发送 `Accept: application/json`，并且 curl 路径固定把响应标记为 `application/json`。因此，上游可能返回普通 OpenAI JSON envelope，而不是客户端期待的 SSE `data:` 事件。

旧 `StreamingDownloadHandler` 只处理以 `data:` 开头的行。普通 JSON 会被完整接收但全部忽略。由于 HTTP 状态仍是 200，`UnityWebRequest` 不会产生网络错误，于是出现“没有任何网络警告，但没有 LLM 回复”的表象。

### 2.2 空结果被当作可继续处理的 payload

旧流式请求在没有解析出任何 SSE 内容时仍返回空字符串。旧 `TryParseDialoguePayload` 随后会创建一个 `dialogueReply` 为空的 fallback payload，而 Orchestrator 只检查显式 error，没有检查 payload 和 `dialogueReply` 是否为空。结果是流程继续进入语音展示，但没有可播放文本，也不会进入统一失败恢复。

### 2.3 SSE 解析会静默吞错

旧解析器还存在以下边界问题：

- 每个传输块单独调用 `Encoding.UTF8.GetString`，多字节字符跨块时可能损坏；
- 最后一条 SSE 行没有换行符时不会被处理；
- JSON 事件解析异常被空 `catch` 静默吞掉；
- HTTP 200 但响应体为空时没有明确失败；
- turn 被停止时仅停 Unity coroutine，底层请求、流式队列和已准备音频不一定同步停止。

### 2.4 纠错 Agent 消失和音频不播放

Agent 是 condition/session 级展示对象，但流式 turn 清理过去会走到会话级隐藏逻辑，导致“无纠错 → 有纠错 → 无纠错”等多轮流程中 Agent 被提前隐藏。

同时，Pilot/Agent 的 `AudioSource` 位于可隐藏的视觉根节点之下。旧逻辑先准备并尝试 `AudioSource.Play()`，再在 `playbackStarted` 回调中显示 Agent；该回调已经晚于 `Play()`，因此隐藏状态下的 AudioSource 可能无法启动，却被流程误认为已完成。

## 3. 修复内容

### 3.1 LLM 请求可靠性

`RealLLMService` 增加统一的请求可靠性策略：

- 总请求预算 45 秒；
- 首次请求最多 30 秒；
- 对话请求允许 1 次瞬态重试；
- 连接错误、HTTP 408、429 和 5xx 视为可重试；
- 尊重上游 `Retry-After`，并限制重试等待时间；
- 流已经输出内容后禁止自动重试，避免用户听到重复的半段回复；
- 正式实验中的纠错请求允许瞬态重试，辅助请求不盲目重试；
- turn 重启、结束、退出和对象销毁时取消仍在执行的请求。

非流式和流式响应现在都使用增量 UTF-8 `Decoder`，可保留跨传输块的多字节字符。SSE 解析器会冲刷最后一行、记录无法解析的事件数量与最后错误，并保留受限长度的响应详情用于诊断。

当流式请求得到普通 OpenAI JSON envelope 时，客户端会兼容提取 `choices[0].message.content`。如果最终仍没有可解码内容，则明确抛出失败，不再制造空 payload。

### 3.2 网关协议透传

`Server/llm-gateway` 现在：

- 根据客户端请求透传 `Accept: text/event-stream` 或 `application/json`；
- 保留上游 `Content-Type`；
- 向 Unity 透传 `Retry-After`；
- urllib 和 curl 两条传输路径行为一致；
- 默认上游超时从 60 秒调整为 28 秒，确保首次失败先由网关返回，Unity 仍有时间在 45 秒总预算内重试。

注意：当前网关仍通过 `response.read()` 或 `subprocess.run()` 缓冲完整上游响应。本次保证的是响应类型和内容不会被错误改写；若后续需要真正逐 token 端到端转发，应另行把网关改为分块写回。

### 3.3 空回复检测和失败语音

Orchestrator 现在统一把以下情况视为 LLM 生成失败：

- 请求返回 error；
- payload 为 null；
- `dialogueReply` 为空或只有空白。

失败时会：

1. 取消底层 LLM 请求并终止流式音频队列；
2. 关闭 thinking 和残留说话动画；
3. 播放恢复提示：`Sorry, I didn't catch that. Could you say it again?`；
4. 将当前 turn 清理完成，状态切到可重试的 `Error`；
5. 对用户仅显示 `Please try again.`，技术错误保留在日志；
6. 不启动该失败 turn 的目标评估，避免无效数据进入实验记录。

恢复语音的播放优先级为：现有 Voice Gateway TTS → 专用本地 `recoveryPromptClip`。恢复流程不会误用普通 demo reply，也不会用静默等待伪装成功。如果两种语音都不可用，会记录明确的播放失败，但仍释放 turn，使用户能够重新录音。

### 3.4 流式 turn 清理

新增 `ISceneTalkCancelableBrain`、`ISceneTalkAvatarRecoveryVoice` 和 `ISceneTalkStreamingAvatarVoice.AbortStreaming()`。Avatar 模块跟踪并停止：

- 流式准备 coroutine；
- 流式播放 coroutine；
- 提前纠错 coroutine；
- Avatar 加载 coroutine；
- 当前准备或播放中的临时音频资源。

流式完成信号放入 `finally`，即使 LLM 异常也能结束等待队列。最终 payload 还会再次验证，防止“任务完成但没有回复”进入展示阶段。

### 3.5 Agent 生命周期和音频播放

turn 级取消改为 `StopPlaybackPreservingPresentation()`：只停止纠错音频和 speaking 状态，然后按当前 condition 重新应用可见性，不再把 Agent 当成 session 资源隐藏。只有 `ClearAvatar()` / `ResetSession()` 等真正的 condition/session 边界才移除 Agent。

播放纠错前使用 `ShowImmediate()` 同步激活视觉根节点和 AudioSource；Pilot 模式通过 `PrepareFeedbackAudioSource()` 做同样处理。`AvatarSpeechPlayer` 还会检测 AudioSource 在播放前是否禁用、`Play()` 是否实际启动、播放期间是否被禁用或销毁，失败时返回错误而不是上报假成功。

`PilotEmbodimentPresenter.OnDestroy()` 会清除静态 `Active`，避免已销毁的 Unity 对象残留为全局引用。

### 3.6 纠错策略补充

本次未提交代码还包含纠错提示词和结果修复：

- 明确 conservative / moderate / active 的判定边界；
- moderate 模式要求识别明确但仍可理解的不自然请求和疑问句语序；
- 加入餐厅场景中的正反例，避免把“能理解”误判为“不需要纠错”；
- 保持对自然省略、标点、大小写和可能的 ASR 同音误差不过度纠正；
- 当 LLM 判断 `hasFeedback=true` 却漏掉可朗读反馈文本时，按 explicit/recast 风格补齐，避免有纠错标记却无语音。

## 4. 场景和配置

`Client/Assets/Scenes/SampleScene.unity` 序列化了本次新增字段：

- `llmFailurePrompt`：英文失败重试提示；
- `totalRequestBudgetSeconds: 45`；
- `firstAttemptTimeoutSeconds: 30`；
- `transientRetryCount: 1`；
- `recoveryPromptClip: {fileID: 0}`。

其中 `recoveryPromptClip` 当前未配置。本地离线兜底要真正生效，需要在 Unity Inspector 中为 `AvatarPresentationVoiceModule` 绑定一段内容一致的 AudioClip。未绑定时仍会优先尝试现有 TTS，但若 TTS 也离线，则无法保证一定有声音。

该场景文件同时包含本次修复开始前已经存在的大量场景层级、餐厅布局和 UI 序列化改动，并非都由对话可靠性修复产生。审查确认这些是真实场景改动，因此本次提交保留它们，没有用脚本归一化或回退。`git diff --check` 报告的尾随空格也都位于该 Unity YAML 文件的空字符串字段中；其余代码和文档通过差异格式检查。

场景中的 LLM `apiKey` 仍为空，仓库根目录 `.env` 由 `.gitignore` 排除，没有把本地密钥纳入提交。

## 5. 涉及文件

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarSpeechPlayer.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/CorrectionFeedbackPresenter.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/PilotEmbodimentPresenter.cs`
- `Client/Assets/Scenes/SampleScene.unity`
- `Server/llm-gateway/src/llm_gateway/api/server.py`
- `Server/llm-gateway/src/llm_gateway/config.py`
- 网关示例配置、README 及相关 Editor/Python 回归测试。

## 6. 自动化验证

已完成：

- `dotnet build Client/Assembly-CSharp.csproj --no-restore`：0 error；
- `dotnet build Client/Assembly-CSharp-Editor.csproj --no-restore`：0 error；
- 网关 Python 单元测试：3/3 通过；
- 非场景文件 `git diff --check`：通过；
- 敏感配置检查：场景 API key 为空，`.env` 已忽略。

编译中仍有项目既存的 `System.Net.Http` / `System.IO.Compression` 版本警告和两个过时 API 警告，与本次修改无关。

Unity Editor Test Runner 尚未实际执行：检查时 Editor 已打开，但当前自动化环境无法识别其窗口。因此新增 Editor 测试已参与 Editor 工程编译，但仍应在提交后的 Unity 环境中补跑。

新增或扩展的回归覆盖包括：

- SSE 跨块 UTF-8、多事件边界和末行冲刷；
- 非流式响应跨块 UTF-8；
- malformed SSE 可观测性；
- 普通 JSON envelope 兼容；
- 空 dialogue payload 拒绝；
- 429 / 502 瞬态重试和终态失败；
- LLM 失败后中止流、播放恢复提示并恢复录音入口；
- 本地恢复音频优先级和缺失音频错误；
- 禁用 AudioSource 时不再上报假播放；
- Voice Only / Floating Orb / Humanoid 的 turn 与 session 生命周期；
- moderate 纠错检测和缺失反馈文本补齐。

## 7. PICO 实机验收建议

### 7.1 正常多轮与 Agent 生命周期

在 Floating Orb 和 Humanoid condition 各执行一次：

1. 第 1 轮说一个自然且无需纠错的句子，确认正常获得 Avatar 回复，Agent 保持 condition 预期的展示状态；
2. 第 2 轮说一个明确错误句，例如 `How long the replacement will be?`，确认纠错语音实际播放，Agent 不会到说话时才延迟出现；
3. 第 3 轮再次说无需纠错的句子，确认 Agent 不因 turn 清理而消失；
4. 结束当前 condition，确认此时 Agent 才被隐藏或清理。

即验收序列：**无纠错 → 有纠错 → 无纠错**。

### 7.2 LLM 失败恢复

1. 临时让网关返回 429、502、空 body、普通 JSON envelope 和 malformed SSE；
2. 确认可重试故障最多重试一次，总等待不超过预算；
3. 确认最终失败时不残留 thinking、半段语音或流式队列；
4. 确认听到恢复提示，并能立即重新录音；
5. 在断开 TTS 的情况下验证绑定后的 `recoveryPromptClip`；
6. 检查失败 turn 没有启动目标评估，也没有被记录为正常对话结果。

## 8. 已知限制与后续项

- 在 Inspector 为 `recoveryPromptClip` 绑定本地音频，否则 TTS 同时不可用时只有日志和重试状态，没有可靠的本地声音；
- 在 Unity Test Runner 中补跑全部 EditMode 测试；
- 如需降低网关场景下的首 token 延迟，把网关从完整响应缓冲改为真正的 chunked/SSE 流式转发；
- 后续若重新保存 `SampleScene.unity`，建议确认 Unity 版本与团队行尾策略一致，避免继续扩大 YAML 噪声 diff。
