# Edwin 具身化纠错反馈模块技术规划

## 1. 文档目标

本文档用于规划 Edwin 在“VR 英语口语练习中的具身化纠错反馈设计”中的负责部分。该研究功能的核心不是单纯给用户纠错，而是比较不同纠错来源和纠错方式对 VR 口语练习体验的影响：

```text
Feedback Provider: 对话 Avatar / 辅助 Agent
Feedback Style: Explicit Correction / Recast
```

Edwin 的工作重点是让“反馈来源”真正具身化，并保证用户语音、纠错反馈语音、主 Avatar、辅助 Agent 和 Unity 播放链路稳定工作。错误检测规则、纠错文本生成、实验条件分配和 VR 主流程仍分别由 Spring 与 Vitor 负责。

本文档参考资料：

- `documents/VR 英语口语练习中的具身化纠错反馈设计.pdf`
- `documents/avatar-module-technical-plan.md`
- `documents/speech-gateway-technical-plan.md`
- `documents/edwin-task.md`
- `documents/trigger-recording-button-state-implementation.md`

## 2. 功能定位

现有 SceneTalkVR 链路为：

```text
用户语音 -> STT -> LLM Brain -> SpringScenePayload -> 场景呈现 -> Avatar/TTS 回复
```

加入具身化纠错反馈后，Edwin 侧需要支持：

```text
用户语音
  -> STT transcript 与语音质量信息
  -> Spring 生成对话回复与纠错反馈 payload
  -> Edwin 根据 provider/style 路由到主 Avatar 或辅助 Agent
  -> 对应角色播放 TTS、动画和反馈表现
```

因此 Edwin 模块不是判断“这句话错没错”的核心大脑，而是负责“谁把反馈说出来、怎么自然地说出来、语音与 Avatar 表现是否稳定”。

## 3. Edwin 分工边界

### 3.1 Edwin 负责

- STT 输入链路：录音、上传语音网关、接收 transcript，并尽量提供置信度、低置信度提示或音频质量信息。
- 手动录音控制：实现并维护 `ISceneTalkManualSpeechInput`，让 Vitor 的按钮和 PICO/OpenXR 空指向扳机可以停止或取消当前录音。
- TTS 输出链路：根据纠错反馈文本生成语音，并区分主对话 Avatar 与辅助 Agent 的音色和播放入口。
- 反馈来源路由：根据 `provider=dialogue_avatar|assistant_agent` 决定由主 Avatar 还是辅助 Agent 播放反馈。
- 辅助 Agent 表现：实现漂浮式 AI 小助手的 prefab、出现/隐藏、说话动画、音频播放和 fallback。
- 主 Avatar 纠错表现：当 provider 是对话 Avatar 时，复用现有 `AvatarPresentationVoiceModule` 播放反馈，但避免重建 Avatar。
- 反馈风格的表达差异：Explicit Correction 更清晰、教学化；Recast 更自然、短促、尽量像继续对话。
- 语音与 Avatar fallback：TTS 失败、辅助 Agent 缺失、字段缺失时不阻塞主练习流程。
- 反馈日志：记录反馈是否播放、provider、style、音频时长、播放耗时、fallback 等，不默认保存原始音频。

### 3.2 Edwin 不负责

- 错误检测 Prompt、错误类型判断和纠错文本生成。这属于 Spring 的 LLM Brain。
- Explicit Correction 与 Recast 的语言学策略设计。这属于 Spring 的反馈策略，但 Edwin 可以提供语音表现建议。
- 2x2 实验条件随机化、顺序平衡、问卷 UI 和任务流程。这属于 Vitor 的 VR 实验流程。
- VR 按钮、字幕 UI、问卷面板和实验数据总表。这属于 Vitor 的客户端交互与实验管理。
- 场景生成、360 skybox、Holodeck 或场景物体布局。这属于 Spring/Vitor 现有分工。

## 4. 推荐接口设计

当前 `SpringScenePayload` 还没有纠错反馈字段。为了让 Edwin 侧能稳定消费反馈，建议后续由 Spring/Vitor 扩展一个结构化字段，例如：

```csharp
[Serializable]
public sealed class CorrectionFeedbackData
{
    public bool hasFeedback;
    public string provider;      // dialogue_avatar | assistant_agent
    public string style;         // explicit | recast
    public string errorType;     // grammar | unnatural | vocabulary | incomplete | unknown
    public string originalText;
    public string correctedText;
    public string feedbackText;
    public string targetSpan;
    public float confidence;
}
```

并挂到：

```csharp
public sealed class SpringScenePayload
{
    public string taskType;
    public string environmentType;
    public string dialogueReply;
    public AvatarRoleData avatarRole;
    public ScenePayload scene;
    public CorrectionFeedbackData correctionFeedback;
}
```

Edwin 只消费其中这些字段：

- `hasFeedback`
- `provider`
- `style`
- `feedbackText`
- `correctedText`
- `confidence`

如果字段缺失，Edwin 侧应按“无纠错反馈”处理，继续播放普通 Avatar 回复。

## 5. Edwin 侧模块设计

### 5.1 STT 语音证据层

现有 `GatewaySpeechInputModule` 已能完成录音、手动停止、上传和 transcript 返回；`MicrophoneRecorder` 已支持外部停止信号，`GatewaySpeechInputModule` 与 `DemoSpeechInputModule` 已通过 `ISceneTalkManualSpeechInput` 接收停止/取消请求。纠错场景下需要在这个基础上继续增强为“尽量给 Spring 提供更可靠的用户输入”。

P0 保持：

- 返回 transcript。
- 支持 `Listen/End/Retry` 与 `Speak/End` 两条 UI 路径触发的手动结束录音。
- 支持 PICO/OpenXR 空指向扳机按住录音、松开同一只手柄结束录音。
- STT 失败时走 mock transcript 或错误回调。
- 不改变现有 `ISceneTalkSpeechInput.CaptureSpeech(...)` 主接口；停止/取消通过 `ISceneTalkManualSpeechInput.RequestStopCapture()` 和 `CancelCapture()` 进入。

P1 增强：

- 在语音网关 STT 响应中加入 `confidence`。
- 标记可能误识别的低置信度片段。
- 记录音频时长、停止来源、静音比例、上传耗时、ASR 耗时。
- 对噪声过大或录音过短的输入返回 `low_audio_quality`，避免 Spring 把 ASR 错误误判为用户语言错误。

P2 增强：

- 支持 word-level timestamp。
- 支持 partial transcript 或更细粒度的语音分析。
- 可选接入 Azure Pronunciation Assessment 或其他发音评分服务，但不作为 P0 必需项。

### 5.2 反馈语音路由层

新增一个反馈路由概念，建议命名为 `CorrectionFeedbackVoiceRouter` 或并入 `AvatarPresentationVoiceModule` 的下游组合模块。

职责：

- 读取 `payload.correctionFeedback`。
- 如果 `hasFeedback=false`，只播放普通 `dialogueReply`。
- 如果 `provider=dialogue_avatar`，由当前主 Avatar 播放 `feedbackText`。
- 如果 `provider=assistant_agent`，由辅助 Agent 播放 `feedbackText`。
- 如果 provider 缺失或非法，回退为 `assistant_agent` 或项目默认 provider。
- 播放完成后通知 Vitor 的 Orchestrator 继续下一步。

项目统一播放策略：

```text
纠错反馈 -> 普通对话回复 -> onComplete
```

无纠错时直接播放普通回复；有纠错时必须先完成纠错反馈，再播放普通回复。该顺序不再作为实验条件变化。

### 5.3 主 Avatar 反馈表现

当 provider 是对话 Avatar 时，反馈应由当前场景的主交流对象给出。Edwin 侧应复用现有主 Avatar，不要重新实例化角色。

需要支持：

- 保持当前 `currentAvatarKey`，不销毁和重建。
- 播放反馈 TTS 时触发 `Talk` 或专用 `Correct` 动画。
- Explicit Correction 与 Recast 默认使用相同 voiceId、语速、音量和 TTS 参数。
- 两种 style 的差异只来自 Spring 生成的反馈文本，避免语音参数成为额外实验变量。
- 如果 TTS 失败，至少保留短暂停顿和动画，不阻塞后续流程。

注意：对话 Avatar 同时承担“交流对象”和“教师”角色，可能造成 PDF 中提到的角色冲突。因此 Edwin 侧要尽量让主 Avatar 的纠错表现短、自然、不过度夸张。

### 5.4 辅助 Agent 反馈表现

当 provider 是辅助 Agent 时，纠错反馈由一个独立的漂浮式 AI 小助手给出，与主对话 Avatar 分离。

建议新增：

- `CorrectionAgentPresenter`
- `CorrectionAgentCatalog` 或简单 prefab 引用
- `CorrectionAgentVoiceProfile`

辅助 Agent 第一阶段可以是低成本表现：

- 一个漂浮在用户视野侧前方的小型实体。
- 在 assistant_agent 条件下应作为常驻反馈来源出现，不随单句是否出错突然出现或消失。
- 非纠错时保持 idle 浮动，不说话。
- 反馈播放时轻微发光、点头、浮动或脉冲。
- 反馈结束后回到待机表现。

辅助 Agent 应与主 Avatar 明显区分：

- 位置不同：不要站在主 Avatar 正前方遮挡对话对象。
- 声音不同：`CorrectionFeedbackPresenter > Assistant Agent Voice Type` 提供腾讯云基础语音合成接口支持英文或中英双语的音色下拉选项，默认使用当前账号已验证可用的英文男声 `WeJack (101050)`，与主 Avatar 的默认音色独立配置。
- 行为不同：只负责纠错，不承担场景角色扮演。

P0 可以使用简单 prefab 或 primitive 组合，不要求复杂模型。重点是让实验参与者能清楚感知“这是另一个反馈来源”。

### 5.5 Explicit 与 Recast 的语音表现

Edwin 不负责生成两种文本，也不通过 voiceId、语速或音量人为扩大两种 style 的差异。

两种 style 使用同一套 TTS 参数：

```json
{
  "voiceId": "101050",
  "speakingRate": "medium",
  "volume": "default"
}
```

`style` 仍进入 Edwin 内部日志，但不映射为额外的 voiceId、语速、音量或 attitude 覆盖。Explicit 与 Recast 的措辞、长度和停顿文本由 Spring 生成。

### 5.6 日志与实验数据

Edwin 侧需要向实验记录提供反馈播放相关数据。建议输出或回调以下字段：

- `turnId`
- `captureMode`
- `recordingDurationMs`
- `recordingStopReason`
- `provider`
- `style`
- `feedbackTextLength`
- `ttsProvider`
- `ttsLatencyMs`
- `audioDurationMs`
- `playbackCompleted`
- `fallbackLevel`
- `sttLatencyMs`
- `sttConfidence`
- `audioQualityFlag`

默认不保存：

- 原始音频。
- 完整用户 transcript。
- 完整纠错文本。

如果研究需要保存 transcript，应由团队统一确认隐私说明和数据脱敏策略。

## 6. 阶段计划

### P0：最小可演示闭环

目标：不用复杂发音评分，先能跑通 2x2 条件下的具身化反馈演示。

- [x] 确认 `CorrectionFeedbackData` 最小字段。
- [x] 支持从 demo payload 读取 provider/style/feedbackText。
- [x] 复用现有手动录音能力，保证纠错反馈流程不需要重新设计 STT 录音入口。
- [x] 主 Avatar 可播放纠错反馈音频。
- [x] 新增一个简单辅助 Agent prefab。
- [x] 辅助 Agent 可常驻、播放 TTS、回到 idle。
- [x] Explicit/Recast 使用相同 TTS 参数，只消费 Spring 提供的不同反馈文本。
- [x] TTS 失败时回退 mock audio 或静默完成。
- [x] 输出基础播放日志：provider、style、是否播放成功。

2026-07-09 第一轮实现记录：

- Unity 契约层已新增 `CorrectionFeedbackData`，并挂到 `SpringScenePayload.correctionFeedback`。
- `DemoBrainModule` 已可通过 demo 输入关键词生成纠错反馈：包含 `correction/feedback/explicit/recast` 时启用反馈，包含 `assistant/agent` 时选择 `assistant_agent`，包含 `recast` 时选择 Recast，否则默认 `dialogue_avatar + explicit`。
- `AvatarPresentationVoiceModule` 已支持先播放纠错反馈、再播放普通回复。`provider=dialogue_avatar` 时驱动主 Avatar 说出反馈；`provider=assistant_agent` 使用辅助 Agent 的独立 AudioSource，不驱动主 Avatar 动画。
- Explicit 与 Recast 不再覆盖 voiceId、speakingSpeed、音量或 attitude，只消费 Spring 提供的不同反馈文本；若网关 TTS 不可用，则回退 demo clip 或静默等待，不阻塞当前回合。

2026-07-09 第二轮实现记录：

- 新增 `CorrectionAgentPresenter`，P0 暂时使用运行时生成的青蓝发光小球作为辅助 Agent，不依赖外部模型 prefab。
- provider/style 统一由 Vitor 的 `ExperimentConditionManager` 注入；首次进入回复呈现后辅助 Agent 在 `assistant_agent` 条件的整个 session 中常驻，非纠错回合保持 idle，且不依赖主 Avatar 是否加载成功，session reset 时隐藏。
- 辅助 Agent 默认出现在用户右前方，idle 状态轻微上下浮动；播放反馈时做膨胀/收缩脉冲，说完后回到 idle，不按单句纠错结果隐藏。
- `AvatarPresentationVoiceModule` 已将 `provider=assistant_agent` 路由到 `CorrectionAgentPresenter` 的独立 `AudioSource`，主 Avatar 在辅助 Agent 反馈期间不触发说话动画。
- Demo rig 构建和“启用 Voice Gateway”菜单会自动挂载并绑定 `CorrectionAgentPresenter`；若组件缺失，仍保留 audio-only fallback，保证流程不卡死。

2026-07-09 调试开关更新：

- `CorrectionFeedbackPresenter` 提供 `Correction Debug` Inspector 区域，可用 `debugForceFeedback` 和 `debugFeedbackText` 强制播放一段测试反馈，不依赖 Spring 真实纠错结果。
- provider/style 与辅助 Agent 显隐仍服从 `ExperimentConditionManager` 的当前实验条件；本地调试不再覆盖 provider、style 或可见性。
- 这些字段只用于本地 demo/debug；正式实验由 Vitor 提供条件分配，由 Spring 提供 `hasFeedback` 与 `feedbackText`。

2026-07-10 顺序、常驻和结果接口更新：

- 播放顺序统一为 `纠错反馈 -> 普通回复 -> onComplete`。
- Edwin 通过 `AvatarPresentationVoiceModule.SetCorrectionFeedbackProvider(string)` 接收 Vitor 的 provider，同时消费管理器写入 payload 的 provider/style；`CorrectionFeedbackPresenter` 不再保留独立模式选择。
- `AvatarPresentationVoiceModule` 保留 `ISceneTalkAvatarVoice`，并通过 `CorrectionPlaybackCompleted` 与 `LastCorrectionPlaybackResult` 返回精简的 `provider/outcome/errorCode` 结果。
- Edwin 内部日志继续记录 style、TTS provider、TTS latency、音频时长、文本长度与 fallback，Vitor 不需要消费这些内部细节。
- Avatar 加载失败时可通过 `allowVoiceFallbackOnAvatarFailure` 继续语音流程；该字段由旧 `continueWithoutAvatar` 平滑迁移，避免现有场景序列化配置丢失。
- 当 payload 要求纠错但 Inspector 关闭纠错播放时，Edwin 返回 `failed/playback_disabled`，确保 Vitor 能结束纠错状态而不是等待不存在的播放事件。

P0 验收标准：

- 四种条件都能在 Unity Editor 中触发。
- 主 Avatar 条件下不会重复销毁 Avatar。
- 辅助 Agent 条件下用户能明显看出反馈来自另一个角色。
- TTS 或辅助 Agent 缺失不会卡死主流程。

### P1：真实语音与 PICO 可用性

目标：让功能在真实语音输入和 PICO 设备上稳定工作。

- [ ] PICO 4 真机验证麦克风录音、上传、STT、TTS 下载和播放。
- [ ] PICO 4 真机回归空指向扳机按住录音、松开同一只手柄结束录音，并确认纠错反馈播放后能继续下一轮 `Speak/End`。
- [ ] STT 响应加入 confidence 或 audioQualityFlag。
- [ ] 语音网关日志加入 STT/TTS 耗时和 fallbackLevel。
- [x] 为辅助 Agent 配置独立 voiceId；Inspector 可从腾讯英文/中英双语 `VoiceType` 下拉框选择，默认 `WeJack (101050)`。高级音色资源包耗尽时，网关会回退到配置的腾讯基础音色并记录 `voice_type_fallback`，不会播放 mock 提示音。音色来源：[腾讯云语音合成音色列表](https://cloud.tencent.com/document/product/1073/92668)，核对日期 2026-07-10。
- [x] 为主 Avatar 与辅助 Agent 分别配置 AudioSource，避免音频互相覆盖。
- [ ] 支持反馈播放时主 Avatar 保持 idle/listening，不与辅助 Agent 抢动作。
- [ ] 与 Vitor 联调反馈出现位置、遮挡和视线舒适度。
- [ ] 与 Spring 联调真实 LLM 产出的 feedback payload。

P1 验收标准：

- PICO 真机上四种条件可跑通。
- 反馈音频播放完成后 Orchestrator 能继续下一轮。
- 低置信度或音频质量差时能通过字段提示 Spring/Vitor 做保守处理。
- 日志足够支持统计反馈次数、播放成功率和大致延迟。

### P2：体验增强与研究打磨

目标：提升沉浸感、可解释性和实验稳定性。

- [ ] 为辅助 Agent 增加更明确的待机/出现/说话/消失动画。
- [ ] 加入基础口型同步或 jaw/mouth 动作。
- [ ] 加入 TTS 音频缓存，降低重复反馈等待时间。
- [ ] 支持 barge-in 或用户跳过反馈。
- [ ] 支持更细的 word-level timestamp 或发音评分。
- [ ] 支持按 provider/style 输出更完整的实验日志。
- [ ] 对 Explicit/Recast 的声音速度、停顿和音量做一致性校准。

P2 验收标准：

- 辅助 Agent 不突兀、不遮挡主对话、不显著破坏沉浸感。
- 主 Avatar 的纠错反馈自然短促，不让用户明显跳出角色扮演。
- 语音和动画表现能支撑正式实验录制和展示。

## 7. 与 Vitor 的接口

Vitor 需要提供或确认：

- 当前实验条件：provider 与 style。
- 固定采用 `纠错反馈 -> 普通回复 -> onComplete`。
- 辅助 Agent 在 VR 空间中的默认位置和安全区域。
- 用户是否可以跳过反馈、重说或继续。
- 实验日志的统一收集入口。

Edwin 需要提供给 Vitor：

- 精简纠错结果回调：`provider`、`outcome`、`errorCode`。
- 完整 TTS latency、音频时长和 fallback 由 Edwin 内部记录。
- 辅助 Agent prefab 与必要 Inspector 配置。

## 8. 与 Spring 的接口

Spring 需要提供或确认：

- `CorrectionFeedbackData` 字段结构。
- 错误类型枚举。
- provider/style 的字符串规范。
- `feedbackText` 是否已经适合直接 TTS。
- Recast 是否只放在 `feedbackText`，还是也需要更新 `dialogueReply`。
- 低置信度 STT 时是否减少纠错或改为提示用户重说。

Edwin 需要提供给 Spring：

- STT transcript。
- 可选 STT confidence 或 audioQualityFlag。
- TTS 支持的 voiceId、语速和风格参数。
- 反馈文本长度建议，避免过长反馈导致 VR 中等待过久。

## 9. 风险与 fallback

### 风险 1：ASR 误识别导致错误纠错

应对：

- P0 先保守处理，只基于 transcript。
- P1 增加 confidence/audioQualityFlag。
- 低置信度时建议 Spring 不做强纠错，改为 “Could you say that again?”。

### 风险 2：辅助 Agent 让场景变得拥挤

应对：

- P0 使用小体积漂浮 Agent。
- 默认放在用户右前方或左前方，不挡主 Avatar。
- 出现时间短，反馈后隐藏。

### 风险 3：TTS 延迟破坏对话连续性

应对：

- 使用 mock/fallback audio 保底。
- P1 记录 TTS latency。
- P2 做缓存和分段播放。

### 风险 4：主 Avatar 与教师身份冲突

应对：

- Recast 条件下让主 Avatar 像自然确认，而不是正式批改。
- Explicit 条件下也控制反馈长度。
- 将更教学化的语气优先交给辅助 Agent。

## 10. 最小交付清单

P0 完成时，Edwin 侧应交付：

- 一个可播放纠错反馈的主 Avatar 路径。
- 一个可播放纠错反馈的辅助 Agent 路径。
- 一个 demo payload 或 demo 开关，能触发四种 provider/style 组合。
- 一个基础 TTS voice routing 方案。
- 一个 fallback 方案，保证 TTS 或 Agent 缺失不阻塞主流程。
- 一份可用于答辩说明的 Edwin 负责范围总结。

可用于答辩的表述：

```text
Edwin 负责把纠错反馈具身化：系统根据实验条件决定由主对话 Avatar 还是独立辅助 Agent 说出反馈，并保持 Explicit Correction 与 Recast 使用相同 TTS 参数，使实验差异来自 Spring 生成的反馈文本，而不是额外的音色、语速或音量变量。
```
