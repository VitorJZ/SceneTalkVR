# 对话语音中断与完整播放修复汇总

日期：2026-07-27

适用项目：SceneTalkVR

## 1. 结论

本次问题不是单一的网络失败，而是对话流式 TTS、音频播放完成判断和降级策略之间存在多个可叠加的缺口：

1. 流式句子从待处理队列取出、仍在合成时，两个队列会短暂同时为空；旧逻辑把“队列为空”误判为整轮语音已经结束。
2. LLM 的最终 `dialogueReply` 可能包含尚未触发分句回调的尾句，旧接口只发送“流结束”信号，没有把最终文本与已排队文本核对，因此会漏播尾句。
3. TTS 没有得到可播放音频时，旧的 `silent_wait` 会以静默等待代替声音；在部分运行模式中，该路径没有作为失败上报。
4. `AudioSource.isPlaying` 变为 `false` 就被视为播放完成，无法区分自然播完、被停止、组件被禁用或 Clip 被替换，导致截断音频仍可能推进对话状态。
5. live 配置过去可能接受 mock STT/TTS 或固定 transcript 回退。Tencent 调用失败时，服务端也默认允许回退到 mock，因此客户端可能没有网络警告，却收到并接受非真实语音结果。

修复后，一轮 Avatar 回复只有在“最终文本已确定、所有分段均真实播放完成、已播放文本与最终 `dialogueReply` 一致”时才完成。任何 TTS 或播放错误都会进入现有 `SceneTalkState.Error`，保留最终回复供重试，不会提前进入 `TurnReview`，也不会通知任务系统该轮已完成。

## 2. 现有对话机制与故障位置

当前真实对话链路为：

```text
SceneTalkOrchestrator
  -> 录音与 Gateway STT
  -> RealLLMService 流式生成
  -> 分句回调 EnqueueSentence
  -> AvatarPresentationVoiceModule 逐句准备 TTS
  -> AvatarSpeechPlayer 播放 AudioClip
  -> PresentReply 完成
  -> EnterTurnReviewState
  -> CompleteActiveTurn / NotifyDialogueTurnCompleted
```

旧流式实现分别维护待合成队列、已合成队列以及若干布尔值。典型竞态如下：

```text
句子出队 -> TTS 合成中 -> 待合成队列为空
                         -> 已合成队列仍为空
LLM 此时结束 -> SignalStreamingComplete
播放协程看到队列均为空 -> 提前退出
PresentReply 看到播放标记为 false -> 提前完成
稍后 TTS 返回，但原播放协程已经结束
```

因此可以同时观察到：ASR 成功、LLM 已有最终文本、无明显网络警告，但没有语音；如果前面的分句已经播放，则表现为语音只有前半段。

## 3. 状态机方案

### 3.1 流式语音回合

新增 `StreamingAvatarSpeechTurn`，由它统一决定语音回合是否完成，不再以队列是否为空作为完成依据。

回合状态：

```text
Idle -> Receiving -> Draining -> Completed
                     |
                     +-------> Failed
任意活动状态 ----------------> Aborted
```

每个分段独立记录：

```text
Queued -> Synthesizing -> Ready -> Playing -> Played
                                   \--------> Failed
```

完成条件同时要求：

- LLM 已给出非空的最终 `dialogueReply`；
- 回合已从 `Receiving` 进入 `Draining`；
- 所有登记过的分段均到达 `Played`；
- 规范化空白后，全部分段文本与最终 `dialogueReply` 完全一致。

分句已出队但仍处于 `Synthesizing` 时，状态机仍持有该分段，因此不会因为队列暂空而提前结束。每个回合还有单独的 `turnId`，旧回合或已移除分段的异步回调不能污染新回合。

### 3.2 最终文本对账

原来的 `SignalStreamingComplete()` 改为 `CompleteStreaming(expectedDialogueText)`，必须同时传入最终 LLM 回复。

- 已排队文本是最终文本的完整前缀：自动把缺失尾部作为新分段合成和播放。
- 播放开始前发现文本分歧：释放旧的未播放结果，按最终回复重建语音。
- 播放开始后发现文本分歧：当前回合失败，禁止把不一致语音标记为完成。
- 最终回复为空：明确失败，不生成空 payload 或静默成功。

这保证了流式分句优化不会牺牲最终语音的完整性。

### 3.3 音频播放完成判定

`AvatarSpeechPlayer` 移除了 `silent_wait`。没有可播放的 TTS、本地恢复音频或明确允许的 demo 音频时，直接返回错误。

播放阶段现在会：

- 确认 `AudioSource.Play()` 后确实进入播放状态；
- 记录 `AudioSettings.dspTime`；
- 根据 Clip 长度和 pitch 计算预期播放时长；
- 检查播放期间 `AudioSource` 没有被销毁或禁用；
- 检查 Clip 没有被替换；
- 仅在实际时长达到预期时接受完成，容差为一个音频采样。

被提前停止的 Clip 不再触发正常的 `playbackEnded`，也不会推进对话回合。

## 4. TTS/STT 正确性与配置策略

### 4.1 客户端校验

`VoiceGatewayClient` 增加运行时策略：

- 请求超时；
- 期望的 TTS provider；
- 是否允许 mock provider。

TTS 响应必须满足：

- provider 与当前 profile 要求一致；
- live profile 中不是 mock 或 `mock_after_*` 回退；
- `textCharacters` 与本次请求的完整文本 Unicode 字符数一致；
- 下载结果能解码为有效且非空的 `AudioClip`。

STT 侧同样拒绝 live profile 中的 mock provider。固定 transcript 只在显式 `MockOffline` 策略允许 mock 时可用。

### 4.2 配置优先级与状态切换

语音设置继续使用项目现有的 RuntimeConfig、DeploymentProfile 和实验/排练状态管理体系，没有建立平行设置系统。有效配置优先级为：

```text
活动 DeploymentProfile
  > SceneTalkRuntimeConfig
  > VoiceGatewaySettings / 场景序列化默认值
```

live 配置默认值为：

- 请求超时：30 秒；
- 期望 TTS provider：`tencent`；
- 允许 mock：`false`。

只有显式 `MockOffline` profile 可以允许 mock。进入或退出 Editor Collection、创建/加载/结束 Rehearsal、设备验证状态变化时，会刷新 Voice Gateway 的有效配置，避免上一个运行模式的设置残留。

`SampleScene`、`SceneTalkRuntimeConfig.asset` 和 `VoiceGatewaySettings.asset` 已序列化以上 live 默认值；场景中的 `fallbackToDemoVoiceOnGatewayError` 已关闭。

### 4.3 服务端策略

Voice Gateway 的 Tencent 默认配置和示例配置已改为：

```json
"tencent_fallback_to_mock": false
```

Tencent 失败时应把错误返回客户端，由状态机进入可恢复错误，不应伪装为正常语音。mock provider 仅供明确的离线/mock profile 使用。

本机私有文件 `Server/voice-gateway/voice-gateway.local.json` 当前仍是 Tencent provider 且 `tencent_fallback_to_mock=true`。该文件被 `.gitignore` 排除，未读取、记录或提交任何密钥。正式运行前必须手工改为 `false` 并重启 Voice Gateway；即使未修改，客户端 live 策略也会拒绝 mock 回退，但服务端仍会产生一次无效的 mock 结果。

## 5. 错误恢复与任务系统一致性

Avatar 语音失败时：

1. `SceneTalkOrchestrator` 记录 `DialoguePlayback` 技术无效原因；
2. 保存最终 `SpringScenePayload` 和 opening/dialogue 上下文；
3. 中止旧的流式语音回合；
4. 进入现有 `SceneTalkState.Error`；
5. UI 的主按钮显示 `Retry`；UI、手柄主操作和语音触发入口都调用统一的 `RetryAfterError()`；
6. 重试只使用缓存的最终 `dialogueReply` 重新执行 TTS/播放，不重跑 ASR 或 LLM，也不重复播放 correction feedback；
7. 只有重播完整成功后才进入 `TurnReview`。

失败时不会调用 `CompleteActiveTurn()` 或 `GoalEvaluationOrchestrator.NotifyDialogueTurnCompleted()`，因此项目现有的顺序任务状态机不会把无声或半段语音当作完整 Avatar 回合，也不会提前解锁下一任务或问卷。

## 6. 代码范围

核心状态机与播放：

- `Client/Assets/SceneTalkVR/Avatar/Scripts/StreamingAvatarSpeechTurn.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarSpeechPlayer.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/CorrectionFeedbackPresenter.cs`

Orchestrator、UI 与任务完成门控：

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs`

运行时配置与实验状态：

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkRuntimeConfig.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkRuntimeConfigApplier.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/RehearsalSessionCoordinator.cs`
- `Client/Assets/SceneTalkVR/Voice/Scripts/GatewaySpeechInputModule.cs`
- `Client/Assets/SceneTalkVR/Voice/Scripts/VoiceGatewayClient.cs`
- `Client/Assets/SceneTalkVR/Voice/Scripts/VoiceGatewaySettings.cs`
- 对应 RuntimeConfig、VoiceGatewaySettings 和 SampleScene 序列化配置

服务端与说明：

- `Server/voice-gateway/src/voice_gateway/config.py`
- `Server/voice-gateway/voice-gateway.local.example.json`
- `Server/voice-gateway/README.md`

测试：

- `StreamingAvatarSpeechTurnTests.cs`
- `AvatarSpeechPlayerTests.cs`
- `AvatarPresentationPlacementTests.cs`
- `DialogueRecoveryTests.cs`
- `VoiceGatewayPolicyTests.cs`
- `Server/voice-gateway/tests/test_provider_policy.py`

## 7. 自动验证结果

提交前完成的自动检查：

- `Assembly-CSharp.csproj`：编译通过，0 error；
- `Assembly-CSharp-Editor.csproj`：编译通过，0 error；
- Unity Editor 自动重载日志：未发现 `error CS` 或编译失败；
- Voice Gateway Python unittest：3/3 通过；
- `git diff --check`：通过。

新增/扩展测试覆盖：

- 分段仍在合成时不得完成回合；
- 最终尾句缺失时返回并补合成 suffix；
- 播放前文本分歧可重建，过期回调无效；
- 播放后文本不一致不得完成；
- 缺少音频时不再静默成功；
- TTS 播放失败停留在 Error，缓存回复重播成功后才进入 TurnReview；
- UI/手柄入口重试缓存回复；
- live profile 拒绝 mock，MockOffline 显式允许 mock；
- TTS 完整文本字符确认支持 Unicode scalar；
- Tencent 服务端默认不构造 mock fallback provider。

当前 Unity MCP 没有暴露 Editor 资源或测试接口，因此本轮没有实际运行 Unity Test Runner。新增 Editor 测试已参与 `Assembly-CSharp-Editor.csproj` 编译，但仍需在可连接的 Unity Editor 中补跑。

## 8. 手工验收清单

### 8.1 Unity Editor

1. 在 Unity Test Runner 中运行全部 EditMode 测试。
2. 使用 live Tencent profile 连续完成多轮对话，确认每轮最终字幕与实际语音文本一致。
3. 使用包含多句且末尾不带换行的长回复，确认最后一句完整播放。
4. 在 TTS 合成中途让 LLM 完成，确认不会因队列短暂为空提前进入 TurnReview。
5. 播放中禁用 AudioSource、替换 Clip 或停止播放，确认进入 Error 而不是正常完成。
6. 点击 `Retry`，确认只重播同一条缓存回复，不重复录音、ASR、LLM 或 correction feedback。

### 8.2 PICO 实机

1. 将私有 Voice Gateway 配置的 `tencent_fallback_to_mock` 改为 `false` 并重启服务。
2. 验证 PicoDeviceValidation profile 使用局域网地址、30 秒超时、Tencent provider 且禁止 mock。
3. 连续执行短回复、长回复、中文/英文混合回复，确认没有无声回合、漏尾句或提前切换界面。
4. 临时断开 Tencent 服务，确认客户端显示可重试错误，不播放 mock 音调，也不推进任务状态。
5. 恢复网络后直接重试，确认完整播放缓存回复并正常进入 TurnReview。

## 9. 审查边界与已知限制

- 本次提交不包含工作区中无关的构建标识时间戳、实验协议确认时间、字体资产、渲染资产、GraphicsSettings 和 ProjectSettings 改动。
- `textCharacters` 能确认网关处理了完整请求文本，但不能从客户端证明云端音频的语义内容逐字正确；最终保证由 Tencent provider 身份校验、完整文本确认、有效 AudioClip 校验和完整时长播放共同构成。
- Unity Test Runner 与 PICO 实机验收尚未自动执行，不能用项目编译结果替代以上两项验证。
- 本次只创建本地 Git 提交，不推送远端。
