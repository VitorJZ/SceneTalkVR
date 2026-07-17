# Experiment v1.1 Stage 3 — Feedback First 与事件时序报告

## 结论

阶段 3 在 `experiment-v1.1-integration`、基线 `65746985599236751b7a8dbfe82310816ca92df7` 上完成。实际播放入口已收敛为一个 `FeedbackFirstPlaybackGate`：任何有反馈回合只能执行 feedback/recast → dialogue；无反馈回合在 Planner 返回后立即开放 Gate。事件型 JSONL 成为时序分析权威来源，旧 JSONL/CSV只保留兼容汇总。

Formal Mode 的五项研究决策阻断与四项 Avatar preset 阻断均未解除。本阶段未进行 PICO 验证。

## 修改文件与核心类型

- `Assets/SceneTalkVR/Scripts/Core/FeedbackFirstTurnModel.cs`
  - `FeedbackFirstTurnState`
  - `FeedbackFirstPlaybackGate`
  - `ExperimentTimingEventType`
  - `ExperimentTimingEvent`
  - `ExperimentEventTimeline`
  - `ExperimentTurnTimingSummary`
- `AvatarPresentationVoiceModule.cs`：唯一 Gate、流式预合成、纠错先播、Formal 失败终止、reset。
- `CorrectionFeedbackPresenter.cs`：纠错 TTS/播放真实回调、actor/voice/speed/volume/hash。
- `AvatarSpeechPlayer.cs`：新增 TTS preparation started/ready 回调。
- `RealLLMService.cs`：Planner 与 Dialogue 并行请求；首响应字节/首流 token/首句/文本完成即时事件；Formal Agent dialogue 泄露终止。
- `ExperimentConditionManager.cs`：每回合 event timeline、即时 JSONL、技术无效原因与原始事件重算汇总。
- `SceneTalkOrchestrator.cs`：`UserSpeechEnded` 真实事件；移除伪造 request timestamp 与硬编码 voiceId。
- `FeedbackFirstTurnTests.cs`、`Stage3FeedbackFirstPlayModeTests.cs`：可控 fake planner/stream/TTS/gate 测试。

## 改造前后的真实时序

改造前存在 `AvatarPresentationVoiceModule.PresentReply` 兼容分支：若 `isDialogueGateOpen` 且流式播放已开始，会先等待 dialogue 结束，再调用 `CorrectionFeedbackPresenter.Present`，形成 `dialogue → feedback`。旧 `RecordTurnMetrics` 还会在回合结束时用当前 UTC 伪造两个 request start，并写入固定 voiceId 与 `"none"` failure 字段。

改造后：

```mermaid
sequenceDiagram
    participant U as User/STT
    participant O as Orchestrator
    participant C as Correction Planner
    participant D as Dialogue Stream
    participant T as Dialogue TTS Queue
    participant G as Playback Gate
    participant F as Feedback Actor
    participant A as Dialogue Avatar
    U->>O: UserSpeechEnded
    O->>G: DialogueGateClosed
    par parallel
        O->>C: CorrectionRequestStarted
        O->>D: DialogueRequestStarted
        D-->>T: first sentence; prepare TTS
    end
    C-->>O: CorrectionTextReady
    alt has feedback
        O->>F: feedback TTS + playback
        F-->>G: CorrectionPlaybackEnded
        G->>G: open once
    else no feedback
        O->>G: open immediately after planner
    end
    G->>A: play prepared dialogue
    A-->>O: DialoguePlaybackEnded / TurnCompleted
```

## Gate 与状态机调用链

`Planning → FeedbackPending → FeedbackSpeaking → DialogueReady → DialogueSpeaking → Completed`；无反馈为 `Planning → DialogueReady → DialogueSpeaking → Completed`。任何 Planner、Dialogue、TTS、AudioSource、Agent/Avatar 播放错误可进入 `TechnicalInvalid` 并关闭 Gate。

`PrepareStreaming` 清空句子队列、已合成队列、Gate 与一次性标记。`EnqueueSentence` 只允许 `AvatarSpeechPlayer.Prepare`，不会开放 Gate。`PresentReply` 是 Planner 结果进入 Gate 的唯一入口。`FeedbackEnded` 才开放有反馈回合的 Gate；无反馈由 `PlannerResolved(false)` 开放。`DialogueStarted`、`FeedbackStarted` 都拒绝重复调用。`ResetConditionSessionBoundary` 继续调用所有 `ISceneTalkSessionReset`，并额外清空 event timeline。

并行协调由 `ISceneTalkFeedbackFirstStreamingBrain.GenerateFeedbackFirstStreaming` 将 Planner 结果立即回调到 `ISceneTalkFeedbackFirstStreamingAvatarVoice.ResolveCorrectionPlan`：因此 Dialogue 仍在生成时即可播放反馈；无反馈时无需等待 Dialogue request 完成即可开放 Gate。最终 payload 到达 `PresentReply` 时只等待早期反馈（如仍在播放），不会二次播放。

## 删除或修复的逆序/绕过路径

- 删除“流式 dialogue 已播放后再播 feedback”的完整兼容分支。
- `onSentenceComplete` 仅入 TTS preparation queue，不能播放。
- `OpenDialogueGate` 现在受状态机约束，Planner/feedback 未完成会抛出顺序错误。
- Streaming 与 non-streaming 共用同一 Gate。
- Presenter missing、Formal correction silent/error、Dialogue TTS/playback error 不再静默继续。
- Recast 使用独立 `recastText`；Presenter 只解析单一反馈单元，不从 dialogue 切割。

## 四条件控制与文本一致性

强类型映射仍由 `FormalConditionResolver` 唯一提供：NE=Avatar+Explicit，NR=Avatar+Recast，SE=Agent+Explicit，SR=Agent+Recast。Correction Planner prompt 不接收 Provider，只接收 Style；Provider 只在 `CorrectionFeedbackPresenter` 决定 actor。相同 Style 的文本 SHA-256 写入 `feedbackTextHash`，自动测试验证 NE/SE 与 NR/SR 的 hash 相同。

Dialogue Generator 不接收 Provider，并明确禁止纠错。Agent 条件下 `CorrectionTextGuards.LooksLikeCorrection` 命中时：Developer Mode 可记录并替换为安全 continuation；Formal Mode 设置 `formalDialogueLeakageDetected`、写入 `TurnTechnicalInvalid` 并停止播放。

## 失败和超时策略

- Correction Planner/Dialogue task fault：记录真实 exception、failure stage，立即 `TechnicalInvalid`；绝不先播 dialogue。
- Dialogue 较慢：反馈可先完成；Avatar 保持 Thinking，Gate 已开但队列等待真正 dialogue TTS。
- Formal correction TTS/Agent/Audio fallback failure：终止回合并记录真实 error code。
- Dialogue TTS/playback failure：停止 queue、关闭有效路径并记录 `DialoguePlayback` failure。
- Developer fallback 仍可启用，但其实际 `fallbackLevel/outcome/errorCode` 写入兼容日志和事件，不再永久填 `"none"`。

## 事件日志

Schema 见 `EXPERIMENT_V1_1_STAGE3_EVENT_SCHEMA.md`，样例见 `EXPERIMENT_V1_1_STAGE3_SAMPLE_EVENTS.jsonl`。文件位置为 `Application.persistentDataPath/SceneTalkVR/ExperimentLogs/<participant>_<session>_events_v1.jsonl`。每个事件即时 `AppendAllText`；UTC 用于关联，`Stopwatch` 单调时间用于顺序和延迟。回合汇总由 `ExperimentEventTimeline.CalculateSummary()` 从原始事件重算，缺失值为 `-1`。

样例回合重算：feedback latency 730 ms；dialogue latency 2500 ms；feedback→dialogue gap 500 ms；correction generation 548 ms；dialogue first sentence 257 ms；correction TTS 169 ms；dialogue first TTS 159 ms。

## 自动测试与 Unity 实际验证

- Unity 6000.3.16f1，复用当前 Editor，未启动第二进程。
- C# compile：通过；最终 Console error：0。
- EditMode：286/286 passed，job `89df073ce523463c8e85f52533d2af97`。
- Stage 3 focused EditMode：19/19 passed，job `e4ffe1412d3745cc9128b8b1b2deb0c6`。
- PlayMode：2/2 passed，job `8b6f4e1b15834b949d513db0cff22577`。
- 四条件、无反馈、提前 TTS/Gate、单次开放、去重、文本 hash、Agent 泄露、Planner/Dialogue timeout、两类 TTS failure、reset、单调事件、summary 重算均由 deterministic fake 覆盖，不依赖云服务。
- 阶段 2 四任务离线启动 PlayMode 回归通过。
- 最小 Play Mode：进入/退出成功，Console 0 error，主场景 RuntimeConfig 正常应用。
- Preflight：Stage 3 无新增失败；仍按设计报告五项研究决策、四项 Avatar preset、PICO/OpenXR/LAN 等既有阻断。
- 未把编译/Editor 结果表述为 PICO 通过。

## 已知风险

- 自动测试刻意不依赖真实云服务；真实网络 jitter、供应商 TTS 首包与音频设备故障仍需后续受控运行采样。
- Correction non-stream API 的 `CorrectionFirstToken` 定义为 HTTP download handler 的首个响应字节回调；服务端若整包返回，它是首包而非模型内部 token 时间。
- Formal Avatar presets 仍为空，Formal 全链路真实声音播放继续被阶段 2 validation 阻断。
- Formal 五项研究决策仍未确认，不允许启动正式实验。

## 阶段 4 输入条件

1. 保持本阶段 Gate/Event schema 不被 allocator 或 UI 绕过。
2. 明确五项研究决策后再解锁 Formal Mode。
3. 合入语义匹配的四任务 Avatar preset 与 voice profile。
4. 在固定 LAN、真实 STT/TTS 与目标音频设备上采集事件 JSONL，验证可重算延迟。
5. 后续 allocator 必须只调用统一 condition reset API。

最终提交由提交信息 `feat(experiment): enforce feedback-first playback and event timing logs` 标识；精确 SHA 在提交后由交付回复记录，避免报告自引用导致提交哈希失效。
