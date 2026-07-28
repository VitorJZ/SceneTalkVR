# Vitor 具身化纠错反馈实验流程技术规划

## 1. 文档目标

本文档用于规划 Vitor 在“VR 英语口语练习中的具身化纠错反馈设计”中的负责部分。该研究功能采用 2x2 实验设计：

```text
Feedback Provider: 对话 Avatar / 辅助 Agent
Feedback Style: Explicit Correction / Recast
```

Vitor 的核心职责是把这套实验设计稳定落到 VR 客户端流程中：控制实验条件、管理回合状态、呈现反馈 UI、记录实验数据，并保证 PICO/Unity 中的体验顺畅。Vitor 不负责 LLM 的纠错文本生成，也不负责 STT/TTS 或 Avatar 资源实现，但需要为 Spring 和 Edwin 提供清晰的调度入口。

本文档参考资料：

- `documents/VR 英语口语练习中的具身化纠错反馈设计.pdf`
- `documents/vitor-task.md`
- `documents/trigger-recording-button-state-implementation.md`
- `documents/edwin-embodied-corrective-feedback-plan.md`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`

## 2. 功能定位

现有 Vitor 侧主流程是：

```text
Idle -> Listening -> Recording -> Transcribing -> Processing -> SceneReady -> AvatarSpeaking -> Listening
```

加入具身化纠错反馈后，需要扩展为实验回合流程：

```text
Idle
  -> ExperimentConditionReady
  -> ScenarioReady
  -> Listening
  -> Recording
  -> Transcribing
  -> ProcessingSpeechAndFeedback
  -> PresentingCorrectionFeedback
  -> PresentingDialogueReply
  -> TurnComplete
  -> Listening / Questionnaire / Finished
```

其中：

- Spring 负责检测错误并生成 `CorrectionFeedbackData`。
- Edwin 负责让主 Avatar 或辅助 Agent 说出反馈。
- Vitor 负责决定何时触发、显示什么 UI、如何继续下一轮、如何记录数据。

## 3. Vitor 分工边界

### 3.1 Vitor 负责

- 实验条件控制：管理 provider/style/scenario 的当前条件。
- VR 主状态机：在现有 `SceneTalkOrchestrator` 基础上加入纠错反馈状态。
- 手动录音流程：复用现有 `Listen -> End -> Retry` 与 `Speak -> End -> Speak` 按钮状态；PICO/OpenXR 扳机仅用于点击射线所指向的界面按钮。
- VR UI 呈现：显示用户原句、反馈状态、继续/重说/跳过等按钮。
- 反馈时序控制：固定先播放纠错反馈、再播放普通对话回复，并决定是否允许跳过。
- 实验任务流程：管理四个标准化场景、每个场景的任务说明和回合数量。
- 问卷和量表入口：在 VR 内或实验结束后呈现 Role Clarity、Conversation Continuity、Social Comfort、Learning Support 等指标。
- 数据记录：记录 turnId、conditionId、provider、style、反馈次数、用户操作、时间戳和模块返回状态。
- PICO 真机体验：确保辅助 Agent、主 Avatar、UI 面板不会遮挡、不晕、不难点。
- 离线兜底：在真实 LLM/STT/TTS 不稳定时，仍能用 demo payload 跑通 2x2 条件。

### 3.2 Vitor 不负责

- 语法错误、不自然表达、词汇错误和不完整句式的判断规则。这属于 Spring。
- Explicit Correction / Recast 的文本生成。这属于 Spring。
- STT 录音、ASR、TTS、音色、主 Avatar 或辅助 Agent 的实际播放。这属于 Edwin。
- Avatar prefab、辅助 Agent prefab、口型同步和语音网关。这属于 Edwin。
- 360 全景图生成、Holodeck 后端或场景语义规划。这仍属于 Spring 现有模块。

## 4. 推荐接口设计

Vitor 侧建议维护一个实验条件对象，供 Spring 和 Edwin 消费：

```csharp
[Serializable]
public sealed class CorrectionExperimentCondition
{
    public string participantId;
    public string sessionId;
    public string conditionId;
    public string scenarioId;
    public string provider;    // dialogue_avatar | assistant_agent
    public string style;       // explicit | recast
    public int turnIndex;
    public int conditionOrder;
}
```

该对象由 Vitor 控制，不由 Spring 或 Edwin 随机决定。推荐数据流：

```text
Vitor ConditionManager
  -> Spring Brain: provider/style/scenario/task context
  -> SpringScenePayload.correctionFeedback
  -> Edwin Avatar/Agent feedback playback
  -> Vitor Logger: playback result and user action
```

如果当前接口暂时不能直接传入 `CorrectionExperimentCondition`，P0 可先通过 Inspector 选择：

- `feedbackProvider`
- `feedbackStyle`
- `scenarioId`

再由 demo payload 或 Spring Brain 读取。

## 5. Vitor 侧模块设计

### 5.1 ExperimentConditionManager

建议新增或在 Orchestrator 周边实现 `ExperimentConditionManager`。

职责：

- 保存当前 participant/session/condition。
- 支持手动切换四种条件，方便调试和录屏。
- 支持正式实验时按预设顺序运行条件。
- 输出 provider/style 给 Spring 和 Edwin。
- 记录每一轮 turnId 和当前 conditionId。

P0 可以只做 Inspector 手动选择；P1 再做顺序平衡和被试内实验流程。

### 5.2 Orchestrator 状态扩展

现有 `SceneTalkOrchestrator` 已能串联 STT、Brain、ScenePresenter 和 AvatarVoice。纠错实验需要增加对“反馈阶段”的感知。

推荐新增逻辑状态：

```text
Listening
Recording
Transcribing
Processing
CorrectionFeedbackSpeaking
DialogueSpeaking
TurnReview
Questionnaire
```

关键要求：

- 如果 `correctionFeedback.hasFeedback=false`，跳过纠错反馈阶段。
- 如果反馈播放失败，记录 fallback 并继续流程，不卡死。
- 如果用户点击跳过，停止或忽略后续反馈播放，并记录 `skipped=true`。
- 如果用户选择重说，进入新一轮 `Listening -> Recording -> Transcribing`，并记录 retry。
- 如果用户在录音中点击 `End`，调用 Edwin 侧 `ISceneTalkManualSpeechInput.RequestStopCapture()`；松开手柄扳机不会改变录音状态。
- 如果用户退出或实验员中断当前回合，调用 `ISceneTalkManualSpeechInput.CancelCapture()`，避免录音协程悬挂。

### 5.3 VR 反馈 UI

反馈 UI 不应抢走所有沉浸感。建议分层：

P0 简洁文本提示：

- 当前条件标签只在调试模式显示，正式实验隐藏。
- 显示用户刚才说的话。
- 显示一个短反馈文本或“Assistant is giving feedback...”状态。
- 复用现有录音按钮语义：需求阶段 `Listen/End/Retry`，对话阶段 `Speak/End`。
- 提供 Continue / Try Again；Try Again 应回到可录音状态，而不是重新生成场景。

P1 实验 UI：

- 支持每轮结束后的短评分。
- 支持每个条件结束后的问卷。
- 支持反馈来源提示，但不要破坏盲测或实验意图。

P2 打磨：

- 对 Recast 条件减少显式 UI 文本，更多依赖自然对话。
- 对 Explicit 条件显示更清楚的 corrected expression。
- UI 位置跟随头显但不贴脸，避免遮挡主 Avatar 和辅助 Agent。

### 5.4 四个标准化场景

PDF 中提到系统提供标准化 VR 英语练习场景，建议 Vitor 侧负责将场景作为实验任务容器固定下来。

推荐四个场景：

- coffee_shop：点咖啡、询问推荐、修改订单。
- information_desk：问路、咨询开放时间、询问信息。
- classroom：课堂问答、表达观点、请求解释。
- airport_or_hotel：办理手续、询问规则、处理小问题。

Vitor 负责：

- 场景入口与任务说明。
- 任务切换顺序。
- 每个场景的最小可运行 prefab/skybox fallback。
- 保证各场景交互流程一致，避免场景差异干扰实验条件。

Spring 负责生成场景文本、任务语境和对话内容；Edwin 负责 Avatar/语音表现。

### 5.5 问卷与实验指标

PDF 中列出的指标应由 Vitor 侧在实验流程中落地。

建议问卷维度：

- Role Clarity：用户是否清楚反馈者的作用。
- Conversation Continuity：纠错后是否仍能顺畅继续对话。
- Social Comfort：用户是否担心被随时纠错、是否紧张。
- Learning Support：反馈是否有帮助、是否能理解要改进什么。
- Preference / Ranking：四种条件排序。

P0 可以不做完整问卷，只保留条件切换和基础日志。

P1 建议用 VR 内按钮或实验后外部问卷记录。

P2 再做完整量表界面、访谈提示和数据导出。

### 5.6 实验日志

Vitor 侧应统一收集跨模块日志。建议一轮记录：

```json
{
  "participantId": "p001",
  "sessionId": "s001",
  "conditionId": "assistant_explicit",
  "scenarioId": "coffee_shop",
  "turnId": "s001_t03",
  "provider": "assistant_agent",
  "style": "explicit",
  "hasFeedback": true,
  "feedbackStartedAt": 123.4,
  "feedbackEndedAt": 127.2,
  "userAction": "continue",
  "retryCount": 0,
  "moduleFallback": "none"
}
```

隐私建议：

- 默认不保存原始音频。
- 默认不保存完整 transcript，除非研究伦理和数据说明允许。
- 可保存 errorType、文本长度、反馈次数和操作行为。

## 6. 阶段计划

### P0：2x2 条件可跑通

目标：在 Unity Editor 中能切换四种实验条件，并完成一轮口语 -> 反馈 -> 继续流程。

- [x] 新增实验条件配置：provider/style/scenario。
- [x] 在 Orchestrator 中加入纠错反馈阶段，并与现有 `Recording` / `Transcribing` 状态衔接。
- [x] 支持无反馈时跳过反馈阶段。
- [x] 支持主 Avatar 与辅助 Agent 两种 provider 的调用入口。
- [x] 支持 Explicit/Recast 两种 style 传给 Spring/Edwin。
- [x] 增加 Continue / Try Again 基础按钮，并保留现有 `Listen/End/Retry`、`Speak/End` 录音按钮状态。
- [x] 输出基础日志：conditionId、turnId、provider、style、hasFeedback。
- [x] 准备四种条件的 demo payload。

P0 验收标准：

- 四种条件在 Editor 中可以手动触发。
- 反馈播放完成后能进入下一轮 `Listening`，并可通过界面按钮开始下一次录音。
- 任一模块失败时可回到 Error 或 fallback，不造成死循环。

### P1：PICO 真机与正式实验流程

目标：让 2x2 条件可以在 PICO 中面向被试稳定运行。

- [ ] PICO 真机验证反馈 UI 可读、可点、不遮挡。
- [ ] 验证辅助 Agent 位置在不同身高/视角下舒适。
- [x] 支持 condition 顺序配置和 session 记录。
- [x] 支持每个条件多轮任务。
- [ ] 支持条件结束后的问卷入口。
- [x] 支持导出实验日志 CSV/JSON。
- [x] 与 Edwin 联调反馈播放完成回调及失败状态。
- [ ] 与 Spring 联调真实 feedback payload。

P1 验收标准：

- PICO 中四种条件可以连续跑完整 session。
- 数据能记录到本地文件或指定日志入口。
- 用户可以在反馈后继续、重说或结束。

### P2：实验打磨与数据质量

目标：降低实验噪声，提升正式研究可用性。

- [ ] 支持被试内条件顺序平衡。
- [ ] 支持场景与条件的对应关系配置。
- [ ] 支持实验员控制面板或快捷键。
- [ ] 支持问卷结果与 turn 日志合并。
- [ ] 支持异常中断恢复或标记无效回合。
- [ ] 优化 Recast 条件下的 UI 干预，避免显式提示破坏自然性。
- [ ] 优化 Explicit 条件下的 corrected text 呈现，确保用户能理解反馈。

P2 验收标准：

- 正式实验可以按 participant/session 导出完整数据。
- 实验员能快速定位模块失败和无效数据。
- UI、Agent、Avatar 不产生明显遮挡或晕动问题。

## 7. 与 Spring 的接口

Vitor 需要向 Spring 提供：

- 当前 `provider`。
- 当前 `style`。
- 当前 `scenarioId` 和任务目标。
- 当前 `turnId`。
- 是否处于正式实验模式。

Spring 需要向 Vitor 返回：

- `dialogueReply`。
- `correctionFeedback.hasFeedback`。
- `correctionFeedback.provider`。
- `correctionFeedback.style`。
- `correctionFeedback.errorType`。
- 适合 UI 展示的短文本字段，如 `correctedText` 或 `feedbackText`。

接口原则：

- Vitor 决定实验条件，Spring 不随机改 provider/style。
- Spring 可以在无明显错误时返回 `hasFeedback=false`。
- Spring 的反馈文本应短，避免 VR 中长时间等待。
- Vitor 客户端拒绝缺少 `dialogueReply` 的结构化 payload，并进入现有错误/重试流程，避免跳过普通回复或把原始 JSON 当作语音播放。

## 8. 与 Edwin 的接口

Vitor 需要向 Edwin 提供：

- 当前反馈应由谁播放：主 Avatar 或辅助 Agent。
- 当前反馈 style。
- 播放顺序固定为 `纠错反馈 -> 普通回复 -> onComplete`。
- 是否允许用户跳过或重说。
- Agent 的空间限制和 UI 安全区域。
- 录音停止/取消时机：按钮 `End`、同手柄扳机松开、Exit、实验员中断。

Edwin 需要向 Vitor 返回：

- 精简纠错结果：`provider`、`outcome=played|demo_fallback|silent_fallback|failed`、`errorCode`。
- Edwin 内部保留 TTS provider、latency、音频时长和 fallback 明细，Vitor 只记录精简结果。
- 条件管理器就绪后，可调用具体门面 `AvatarPresentationVoiceModule.SetCorrectionFeedbackProvider(provider)`；无需修改共享 `ISceneTalkAvatarVoice`。

接口原则：

- Vitor 不直接操作 TTS provider 或 Avatar prefab。
- Edwin 不直接决定实验流程跳转。
- 播放失败必须回调 Vitor，不能静默卡住。
- 录音停止和取消必须通过 `ISceneTalkManualSpeechInput` 统一进入 Edwin 模块，Vitor 不直接操作 `MicrophoneRecorder`。

## 9. 风险与 fallback

### 风险 1：反馈流程打断沉浸感

应对：

- Recast 条件尽量少弹显式 UI。
- Explicit 条件只显示短句，不显示大段说明。
- 反馈结束后快速回到 Listening 或继续对话。

### 风险 2：四种条件难以稳定复现

应对：

- P0 提供 demo payload。
- P1 固定 scenario 和任务顺序。
- 记录 conditionId 和 turnId，方便复查。

### 风险 3：辅助 Agent 遮挡或引起不适

应对：

- Vitor 负责设定空间安全区。
- Agent 默认在侧前方，不贴近用户视野中心。
- UI 面板和 Agent 不能同时占据主 Avatar 前方。

### 风险 4：多模块回调复杂

应对：

- 所有耗时任务仍走 `IEnumerator` 协程。
- 播放完成、失败、跳过都必须有统一回调。
- 日志先记录最小字段，再逐步扩展。

## 10. 最小交付清单

P0 完成时，Vitor 侧应交付：

- 一个可配置 provider/style 的实验条件入口。
- 一个支持纠错反馈阶段的 Orchestrator 流程。
- 一个基础 VR 反馈 UI。
- Continue / Try Again 基础交互，并与现有手动录音按钮状态兼容。
- 四种条件 demo 路径。
- 基础日志输出。

可用于答辩的表述：

```text
Vitor 负责把 2x2 具身化纠错反馈实验落到 VR 客户端流程中：控制反馈来源和反馈方式的实验条件，调度 STT、LLM、Avatar 和辅助 Agent 的播放时机，并记录沉浸感、对话连续性和用户操作相关的数据，保证整个实验能在 PICO 中稳定运行。
```
