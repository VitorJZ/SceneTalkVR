# Spring 具身化纠错反馈 LLM 策略技术规划

## 1. 文档目标

本文档用于规划 Spring 在“VR 英语口语练习中的具身化纠错反馈设计”中的负责部分。该研究功能采用 2x2 实验设计：

```text
Feedback Provider: 对话 Avatar / 辅助 Agent
Feedback Style: Explicit Correction / Recast
```

Spring 的核心职责是让 LLM Brain 能够识别用户英语表达中的可反馈问题，并根据实验条件生成结构化、可控、短小、适合 TTS 和 VR 呈现的纠错反馈。Spring 不负责 VR 实验流程和问卷 UI，也不负责 STT/TTS、Avatar 或辅助 Agent 的播放实现。

本文档参考资料：

- `documents/VR 英语口语练习中的具身化纠错反馈设计.pdf`
- `Client/PROJECT_DOCUMENTATION.md`
- `documents/conversation.md`
- `documents/edwin-embodied-corrective-feedback-plan.md`
- `documents/vitor-embodied-corrective-feedback-plan.md`
- `documents/trigger-recording-button-state-implementation.md`

## 2. 功能定位

现有 Spring 侧职责包括：

```text
用户 transcript -> RealLLMService -> SpringScenePayload
```

其中 `SpringScenePayload` 已包含：

- `taskType`
- `environmentType`
- `dialogueReply`
- `avatarRole`
- `scene`

加入纠错反馈后，Spring 侧需要在同一轮 LLM 输出中额外生成：

```text
CorrectionFeedbackData
  -> 是否需要反馈
  -> 错误类型
  -> 原始表达
  -> 推荐表达
  -> Explicit 或 Recast 风格文本
  -> provider/style 标签回传
```

Spring 的关键任务是保证纠错既有教学价值，又不破坏 VR 口语练习中的对话自然性。

远端最新实现已把语音输入从固定时长改为手动结束录音：Vitor 侧有 `Recording` / `Transcribing` 状态，Edwin 侧通过 `ISceneTalkManualSpeechInput` 接收停止和取消信号。因此 Spring 的纠错判断应考虑录音边界：用户可能主动结束一句完整表达，也可能录得过短、噪声过大或中途取消。低质量输入不应被强行解释成语言错误。

## 3. Spring 分工边界

### 3.1 Spring 负责

- 设计 LLM Prompt，使模型识别用户表达中的语言问题。
- 识别 PDF 中列出的主要错误类型：
  - 语法错误
  - 不自然表达
  - 词汇使用错误
  - 不完整句式
- 根据 Vitor 给定的 `style=explicit|recast` 生成不同反馈文本。
- 根据 Vitor 给定的 `provider=dialogue_avatar|assistant_agent` 调整反馈措辞，避免角色口吻冲突。
- 输出结构化 `CorrectionFeedbackData`，供 Vitor 记录和 Edwin 播放。
- 控制反馈长度，适合 VR 中即时播放。
- 对低置信度 STT 或疑似 ASR 误识别保持保守，避免误纠错。
- 消费 Edwin/Vitor 提供的录音上下文，例如录音时长、停止原因、音频质量标记，用于判断是否需要请求用户重说。
- 维护多轮对话上下文，使纠错不破坏当前场景任务。
- 为正式实验保持四种条件的一致性和可复现性。

### 3.2 Spring 不负责

- 实验条件随机化、被试顺序平衡和问卷流程。这属于 Vitor。
- VR UI 展示、按钮、状态机、日志文件写入和 PICO 打包。这属于 Vitor。
- STT/TTS、语音网关、音色、Avatar 播放和辅助 Agent 表现。这属于 Edwin。
- 判断用户真实发音是否准确的声学评分。若后续接入发音评分，音频证据由 Edwin 提供，Spring 只消费结果。
- Avatar prefab、场景 prefab、全景球渲染细节和 Unity 资源加载。

## 4. 推荐数据结构

建议在 `SpringScenePayload` 中扩展：

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
    public string severity;      // light | medium | blocking
    public string rationaleTag;  // short internal label, not shown to user by default
}
```

字段含义：

- `hasFeedback`：是否触发纠错。没有明显问题时为 false。
- `provider`：必须回传 Vitor 指定条件，不由 LLM 随机改变。
- `style`：必须回传 Vitor 指定条件。
- `errorType`：错误类型，用于统计，不一定展示。
- `originalText`：用户原句或相关片段。
- `correctedText`：推荐表达。
- `feedbackText`：直接给 Edwin 做 TTS 的文本。
- `targetSpan`：错误片段，给 Vitor UI 高亮使用。
- `confidence`：LLM 对纠错判断的置信度。
- `severity`：是否影响交流。
- `rationaleTag`：内部短标签，帮助调试，不给用户展示长解释。

P0 可先只实现：

- `hasFeedback`
- `provider`
- `style`
- `errorType`
- `correctedText`
- `feedbackText`

若 Vitor/Edwin 后续提供录音上下文，建议额外作为 LLM 输入而不是直接展示：

```csharp
[Serializable]
public sealed class SpeechCaptureContext
{
    public string captureMode;         // request | dialogue
    public float recordingDurationMs;
    public string recordingStopReason; // button_end | trigger_release | timeout | cancel | unknown
    public float sttConfidence;
    public string audioQualityFlag;    // ok | too_short | too_noisy | low_confidence | unknown
}
```

## 5. 错误检测策略

### 5.1 只纠正“值得纠正”的问题

VR 口语练习中的用户会有停顿、重复、改口和自然口语现象。Spring 不应把所有不流利都当作错误。

建议判定优先级：

1. 影响理解的问题优先反馈。
2. 明显语法或搭配错误可以反馈。
3. 只是自然停顿、轻微重复、不影响理解的自我修正，不必每次反馈。
4. STT 置信度低时，不做强纠错。
5. 录音过短、取消或音频质量差时，优先请求用户重说，而不是输出语法纠错。
6. 每一轮最多反馈一个核心问题，避免让用户压力过大。

### 5.2 错误类型

建议枚举：

```text
grammar
unnatural
vocabulary
incomplete
pronunciation_uncertain
no_feedback
unknown
```

其中 `pronunciation_uncertain` 不代表 Spring 独立判断发音错误，而是消费 Edwin 侧传来的低置信度或发音评分信号。

### 5.3 严重程度

建议枚举：

```text
light
medium
blocking
```

- `light`：不影响理解，可不反馈或用 Recast。
- `medium`：值得反馈，但不要中断太久。
- `blocking`：影响交流，可建议用户重说。

## 6. 反馈风格生成

### 6.1 Explicit Correction

Explicit Correction 需要直接指出问题并提供正确表达。

原则：

- 简短明确。
- 不羞辱用户。
- 不讲长语法课。
- 最多解释一个点。
- 适合 TTS 直接朗读。

示例：

```text
You can say, "I really like this topic," not "I very like this topic."
```

或：

```text
Try this: "Could I have a latte, please?"
```

### 6.2 Recast

Recast 需要用更自然的对话方式重述正确表达，尽量保持对话连续性。

原则：

- 不显式说 “You are wrong”。
- 不出现太多教学元语言。
- 像自然确认或回应。
- 可以把 corrected expression 融进下一句对话。

示例：

```text
Oh, you really like this topic?
```

或：

```text
Sure, you would like a latte, please.
```

### 6.3 Provider 对文本的影响

Provider 由 Vitor 指定，但 Spring 需要让文本口吻适配来源。

当 `provider=dialogue_avatar`：

- 文本应更像当前角色自然回应。
- 尽量避免 “As your teacher...”。
- Recast 条件尤其要像继续对话。

当 `provider=assistant_agent`：

- 可以更明确地说这是一个提示。
- Explicit 条件可以更教学化。
- 仍然保持短句，避免打断太久。

## 7. Prompt 设计建议

Spring 应在 `RealLLMService` 的系统提示词中加入纠错任务，但保持 JSON 输出稳定。

推荐 Prompt 约束：

```text
You are generating a VR English speaking practice turn.
Return strict JSON only.
The experiment condition is:
- feedbackProvider: {provider}
- feedbackStyle: {style}

Analyze the learner's latest utterance.
Detect at most one important issue among grammar, unnatural expression, vocabulary misuse, or incomplete sentence.
Do not correct normal pauses, fillers, harmless repetition, or self-repair unless they block understanding.
If ASR confidence is low, avoid strong correction.
If recording duration is too short, cancelled, or audio quality is poor, ask the learner to repeat instead of correcting grammar.

If feedbackStyle is explicit, provide a short direct correction.
If feedbackStyle is recast, provide a natural conversational reformulation.
The feedbackText must be short and suitable for spoken TTS in VR.
Do not change feedbackProvider or feedbackStyle.
```

需要注意：

- 严格 JSON，避免多余解释。
- 每轮最多一个反馈点。
- `feedbackText` 不宜超过 1-2 句。
- `dialogueReply` 与 `feedbackText` 不要重复太多。

## 8. 多轮对话与学习记忆

Spring 现有 `RealLLMService` 已维护对话历史。纠错功能加入后，应区分：

- 当前场景对话上下文。
- 当前用户刚说的话。
- 当前实验条件。
- 历史高频错误。

P0：

- 只对当前轮生成反馈。
- 不做长期学习画像。

P1：

- 在 session 内记录用户常见 errorType。
- 避免连续多轮纠正同一轻微问题。
- 如果用户重说后改正，可在 `dialogueReply` 中自然鼓励。

P2：

- 生成 session summary。
- 统计错误类型分布。
- 为 Learning Support 维度提供更一致的反馈策略。

## 9. 场景与任务语境

纠错反馈不能脱离当前口语任务。Spring 需要继续维护场景任务和角色设定：

- coffee_shop：点单、修改订单、询问推荐。
- information_desk：问路、咨询信息、确认时间。
- classroom：表达观点、请求解释、回答问题。
- airport_or_hotel：办理手续、询问规则、处理异常。

每个场景的 LLM 输出仍应包含：

- `environmentType`
- `avatarRole`
- `dialogueReply`
- `scene`
- `correctionFeedback`

如果只是后续多轮对话，不需要每一轮重新生成完整场景资产，但仍需要保持 avatar role 和 task context 一致。

## 10. 阶段计划

### P0：结构化反馈最小闭环

目标：在 demo/Editor 中能根据固定 provider/style 输出稳定纠错反馈。

- [ ] 定义 `CorrectionFeedbackData` 最小字段。
- [ ] 更新 demo payload，覆盖四种条件。
- [ ] 更新 `RealLLMService` prompt，加入 error detection 与 style 控制。
- [ ] 保证 provider/style 原样回传，不由 LLM 改写。
- [ ] 每轮最多输出一个反馈点。
- [ ] 无明显错误时输出 `hasFeedback=false`。
- [ ] 反馈文本限制在 1-2 句。

P0 验收标准：

- 对 “I very like this topic” 能输出 explicit/recast 两种不同反馈。
- 对自然停顿或无明显错误的句子不强行纠错。
- 输出 JSON 可被 Unity `JsonUtility` 或现有解析链路消费。

### P1：真实对话与实验一致性

目标：让真实 STT transcript 和正式实验条件下的反馈稳定可用。

- [ ] 与 Vitor 联调 condition 注入方式。
- [ ] 与 Edwin 联调 STT confidence/audioQualityFlag。
- [ ] 与 Vitor/Edwin 联调手动录音上下文，包括 captureMode、recordingDurationMs 和 recordingStopReason。
- [ ] 在低置信度时减少强纠错。
- [ ] 为四个场景准备标准任务 prompt。
- [ ] 控制每个 style 的文本长度和语气一致性。
- [ ] 输出 errorType/severity/targetSpan，支持 Vitor 统计和 UI。
- [ ] 在多轮对话中保持角色和任务连续。

P1 验收标准：

- 四种条件下的反馈差异稳定。
- 同一用户句子在同一 style 下多次输出格式一致。
- JSON 字段缺失率低，解析失败时有 fallback。

### P2：研究数据质量与学习支持

目标：提升正式实验和论文分析的数据质量。

- [ ] 记录 session 内错误类型摘要。
- [ ] 支持反馈策略调参：保守/中等/积极。
- [ ] 支持按场景控制任务难度。
- [ ] 输出不展示给用户的 `rationaleTag`，帮助后期分析。
- [ ] 为访谈和问卷分析提供条件级摘要。
- [ ] 评估是否加入 pronunciation assessment 结果。

P2 验收标准：

- 输出能支持 Immersion、Conversation Naturalness、Social Comfort、Learning Support 等指标解释。
- 反馈既稳定又不会频繁破坏对话。
- 数据字段足够用于后续论文统计和质性分析。

## 11. 与 Vitor 的接口

Vitor 需要提供：

- `provider`
- `style`
- `scenarioId`
- `turnId`
- 当前是否正式实验模式
- 用户是否重说或跳过上一条反馈

Spring 需要返回：

- `SpringScenePayload.dialogueReply`
- `SpringScenePayload.correctionFeedback`
- `hasFeedback`
- `errorType`
- `targetSpan`
- `correctedText`
- `feedbackText`

接口原则：

- 实验条件由 Vitor 决定。
- Spring 不擅自改变 provider/style。
- Spring 负责保证输出短、稳、可解析。

## 12. 与 Edwin 的接口

Edwin 需要提供：

- STT transcript。
- 可选 `sttConfidence`。
- 可选 `audioQualityFlag`。
- 可选 `captureMode`、`recordingDurationMs`、`recordingStopReason`。
- TTS 支持的 voice/style 能力边界。
- 反馈文本长度建议。

Spring 需要提供给 Edwin：

- 可直接朗读的 `feedbackText`。
- `provider` 和 `style`。
- `correctedText` 供 UI 或 TTS 备用。
- `severity`，用于决定是否建议重说。

接口原则：

- Spring 不生成 SSML 或厂商专属 TTS 参数，除非 Edwin 明确支持。
- Spring 不关心具体 Avatar prefab。
- Edwin 不需要理解 Prompt，只消费结构化字段。

## 13. 风险与 fallback

### 风险 1：LLM 过度纠错

应对：

- Prompt 中明确“每轮最多一个问题”。
- 对轻微口语现象输出 `hasFeedback=false`。
- 增加 severity，仅中高严重度触发显式反馈。

### 风险 2：Recast 被生成成显式教学

应对：

- 在 prompt 中禁止 Recast 使用 “You should say...”。
- 加入少量 few-shot 示例。
- 用测试集固定检查四种条件输出。

### 风险 3：ASR 错误被当成用户错误

应对：

- 消费 Edwin 的 confidence/audioQualityFlag。
- 低置信度时生成请求重说，而不是纠错。
- 在 `rationaleTag` 中标记 `asr_uncertain`。

### 风险 4：JSON 解析失败

应对：

- 强制 strict JSON。
- 保留 fallback payload。
- 字段枚举固定为小写 snake_case。
- Vitor/Edwin 对缺失 feedback 字段按无反馈处理。

## 14. 最小交付清单

P0 完成时，Spring 侧应交付：

- `CorrectionFeedbackData` 字段定义建议。
- 一版支持 provider/style 的 LLM prompt。
- 四种条件 demo 输出样例。
- 错误类型枚举。
- Explicit 和 Recast 的 few-shot 示例。
- JSON fallback 策略。

可用于答辩的表述：

```text
Spring 负责具身化纠错反馈中的语言智能层：系统会分析用户刚才的英文表达，判断是否存在语法、不自然表达、词汇或不完整句式问题，并根据实验条件生成 Explicit Correction 或 Recast 两类反馈。输出以结构化 JSON 交给 Vitor 调度和 Edwin 播放，从而保证纠错策略可控、可比较、可复现实验。
```
