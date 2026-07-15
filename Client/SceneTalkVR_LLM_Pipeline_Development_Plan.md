# SceneTalkVR LLM Pipeline 与具身化纠错反馈实验变量强化开发方案

**目标目录（用户本地）**：`/mnt/e/UnityProjects/SceneTalkVR/Client`  
**建议保存文件名**：`SCENETALKVR_LLM_PIPELINE_DEV_PLAN.md`  
**适用阶段**：第二周开发 / Manipulation Validity Sprint  
**核心目标**：让 2×2 实验条件在“用户实际听到与看到的体验层面”被可靠地区分，而不仅仅是在 payload 字段层面被区分。

---

## 0. 背景与问题判断

当前系统已经完成了具身化纠错反馈的主要工程骨架：

- `ExperimentConditionManager` 已管理 `participantId`、`sessionId`、`conditionId`、`scenarioId`、`provider`、`style`、`turnIndex`、`conditionOrder`。
- 已支持四种实验条件：
  - `dialogue_avatar + explicit`
  - `dialogue_avatar + recast`
  - `assistant_agent + explicit`
  - `assistant_agent + recast`
- `RealLLMService` 和 `SceneTalkOrchestrator` 已对 `correctionFeedback.provider/style` 做强制回写。
- `CorrectionFeedbackPresenter` 已能根据 provider 将纠错反馈路由到主 Avatar 或辅助 Agent 小球。
- 日志已能记录 condition、scenario、turn、provider/style、hasFeedback、errorType、correctionOutcome、userAction、retryCount、recordingDurationMs、moduleFallback、timestamp。

但是现有测试反馈显示：四种实验配置的体验区分不明显。审计报告显示根因不是播放链路没有完成，而是：

1. `provider/style` 字段被锁定，但 `dialogueReply` 和 `feedbackText` 的语言内容没有被可靠锁定。
2. `assistant_agent` 条件下，主 Avatar 的 `dialogueReply` 仍可能包含纠错内容，导致主 Avatar 越界纠错。
3. `recast` 条件下，LLM 可能生成伪 recast，例如带有 “You mean...”、“You should say...”、“A better way is...” 等显式纠错痕迹。
4. 当前 Prompt 同时承担场景解析、对话生成、纠错判定和实验条件执行，职责过载。
5. `feedbackSensitivity` 和 `sessionErrorHistory` 是产品化功能，但会污染正式 2×2 实验中的 feedback frequency 和 style 可比性。
6. STT 低置信度和超短录音目前主要依赖 Prompt 软约束，不是代码层硬过滤。
7. 日志缺少 `transcript`、`dialogueReply`、`feedbackText`、`originalText`、`correctedText`、`rationaleTag`、`sttConfidence` 等论文分析必需字段。

因此本轮开发的核心不是继续增加功能，而是做一次 **Manipulation Validity Sprint**：确保四种实验条件在语言内容、播放来源、日志记录和回归测试中都能稳定地区分。

---

## 1. 总体开发目标

### 1.1 研究目标

本系统服务于论文主实验：

> Feedback Provider × Feedback Style 对 VR 英语口语练习中 role clarity、conversation continuity、social comfort、perceived learning、task completion 和 correction uptake 的影响。

因此系统必须保证：

- Provider 是真正的 provider 差异：
  - `dialogue_avatar`：纠错由主对话 Avatar 提供。
  - `assistant_agent`：纠错由独立辅助 Agent 提供，主 Avatar 不纠错。

- Style 是真正的 style 差异：
  - `explicit`：明确指出错误或给出更好表达。
  - `recast`：自然重述正确表达，不使用显式纠错语言。

- 所有正式实验条件下：
  - 不允许 LLM 擅自改变 provider/style。
  - 不允许自适应退让改变 style 或 feedback frequency。
  - 不允许 STT fallback transcript 进入正式实验数据。
  - 不允许 debug force feedback 污染正式实验。

### 1.2 工程目标

开发完成后，应满足：

| 类别 | 目标 |
|---|---|
| Provider manipulation | `assistant_agent` 条件下主 Avatar 纠错泄露率为 0%。 |
| Style manipulation | `recast` 条件下显式纠错词违规率低于 5%，目标为 0%。 |
| JSON 稳定性 | LLM JSON parse 成功率 ≥ 99%。 |
| STT 防误判 | 低置信度 / 超短录音纠错抑制率 100%。 |
| 日志完整性 | P0 日志字段完整率 100%。 |
| 正式实验稳定性 | `formalExperiment = true` 时禁用所有会污染实验变量的 debug/fallback/adaptive 行为。 |
| 回归测试 | 40 条 Prompt 测试集通过率 ≥ 90%。 |

---

## 2. 开发优先级总览

## P0 必须完成

1. 引入 `Experiment Locked Mode`。
2. 防止 `dialogueReply` 纠错泄露。
3. 拆分或至少逻辑分离 dialogue generation 与 correction generation。
4. 强化 recast 定义、禁用词和后验校验。
5. STT 低置信度和超短录音代码层硬过滤。
6. 正式实验禁用 fallback transcript。
7. 正式实验禁用 debugForceFeedback。
8. 补齐 P0/P1 日志字段。
9. 增加 enum 白名单校验。
10. 增加 40 条 Prompt/LLM pipeline 测试集。

## P1 应该完成

1. 为四个 scenario 增加 scenario-specific correction guidance。
2. 处理 `dialogue_avatar + recast` 的反馈与对话边界。
3. 增加 manipulation check 调试面板或日志字段。
4. 增加 LLM 原始输出与清洗后输出的可选调试保存。
5. 增加 task goal completion 的 P0 轻量字段。

## P2 可选增强

1. correction uptake 初版检测。
2. JSON repair pass。
3. Post-task summary prompt。
4. Adaptive Product Mode UI。
5. Live Mode 双工语音，不进入本轮开发。

---

## 3. 文件与模块影响范围

Agent 开发时应重点检查并修改以下文件。路径以 `Client` 为根目录。

### 3.1 Core / Contracts

- `Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`
  - 扩展 `CorrectionFeedbackData`。
  - 扩展 `ExperimentConditionData` 或相关 condition 结构。
  - 必要时新增 `SceneTalkSttMetadata`、`CorrectionValidationResult`、`ExperimentMode` 等轻量结构。

- `Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
  - 在 payload 生成后增加 provider/style 强校验。
  - 增加 `dialogueReply` 纠错泄露 guard 调用。
  - 在日志中写入 transcript、dialogueReply、feedbackText 等字段。
  - 处理低置信度/超短录音时的自然重录引导。

### 3.2 Experiment

- `Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs`
  - 增加 `IsExperimentLocked`。
  - `formalExperiment = true` 时锁定配置。
  - 增加 condition switch 时清理 session/correction history。
  - 增加日志字段。
  - 增加 condition order position。

### 3.3 LLM

- `Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs`
  - 拆分 Prompt。
  - 修改 JSON schema。
  - 增加 STT 硬过滤入口。
  - 增加 `Experiment Locked Mode` 支持。
  - 禁用自适应退让进入正式实验。
  - 增加 scenario-specific blocks。
  - 增加 recast validation / repair 或失败降级。
  - 统一首轮与后续轮 schema。

### 3.4 Voice / STT

- `Assets/SceneTalkVR/Voice/Scripts/GatewaySpeechInputModule.cs`
  - formalExperiment 下禁用 `fallbackTranscript`。
  - 暴露 `LastSttResponse.provider`、`confidence`、`latencyMs`、`fallbackLevel` 给日志。
  - 增加 transcript source / fallback source 字段，便于日志判断。

### 3.5 Correction / Avatar

- `Assets/SceneTalkVR/Avatar/Scripts/CorrectionFeedbackPresenter.cs`
  - formalExperiment 下禁用 `debugForceFeedback`。
  - 增加 recast 禁用词后验检查。
  - 增加 provider/style/content mismatch warning。
  - 可选：当 recast TTS 失败时记录 `recast_audio_failed`。

- `Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs`
  - 确认播放顺序：纠错反馈 → 普通回复。
  - 若 `dialogue_avatar + recast` 采用特殊合并策略，在这里或 Orchestrator 中处理避免重复播放。

### 3.6 Demo / Tests

- `Assets/SceneTalkVR/Scripts/Demo/DemoBrainModule.cs`
  - 与新 schema 对齐。
  - 为四个条件生成明显不同的 Demo 输出。

- 建议新增：
  - `Assets/SceneTalkVR/Scripts/Editor/LLMPipelineTestRunner.cs`
  - `Assets/SceneTalkVR/Docs/LLMPipelineTestCases.md`
  - `Assets/SceneTalkVR/Docs/LLMPipelineManipulationCheckReport.md`

---

## 4. P0-1：引入 Experiment Locked Mode

### 4.1 目标

正式实验中，系统必须锁定所有会污染 2×2 条件可比性的行为。

### 4.2 需要锁定的行为

当 `formalExperiment = true` 或 `IsExperimentLocked = true` 时：

1. `feedbackSensitivity` 固定为 `moderate`。
2. 不注入 `sessionErrorHistory` 到 Prompt。
3. 不使用重复错误自适应退让指令。
4. condition 切换时清空 `sessionErrorHistory`。
5. 禁用 `fallbackTranscript`。
6. 禁用 `debugForceFeedback`。
7. 禁止用户通过语音改变 scenario/provider/style/avatar。
8. 只允许当前 `ExperimentConditionManager` 控制 provider/style/scenario。
9. 所有低置信度和超短录音必须代码层抑制纠错。

### 4.3 建议接口

在 `ExperimentConditionManager` 中增加：

```csharp
public bool IsExperimentLocked => formalExperiment;
public string LockedFeedbackSensitivity => IsExperimentLocked ? "moderate" : feedbackSensitivity;

public event Action ExperimentConditionChanged;

public void NotifyConditionChanged()
{
    ExperimentConditionChanged?.Invoke();
}
```

如果已有 `AdvanceCondition()`，在其中调用：

```csharp
public void AdvanceCondition()
{
    // existing logic
    NotifyConditionChanged();
}
```

在 `RealLLMService` 中增加：

```csharp
private bool IsExperimentLocked()
{
    return currentCondition != null && currentCondition.formalExperiment;
}
```

如果 condition 中没有 `formalExperiment`，则通过查找 `ExperimentConditionManager` 获取。

### 4.4 RealLLMService 行为

当 locked：

```csharp
var effectiveSensitivity = IsExperimentLocked() ? "moderate" : feedbackSensitivity;
var includeAdaptiveHistory = !IsExperimentLocked();
```

Prompt 中不允许出现：

```text
If the same errorType was corrected recently, try to be more tolerant...
prefer the softer recast...
```

正式实验下绝不能让 LLM 根据历史改 style。

### 4.5 Condition 切换清理

在 condition 切换时调用：

```csharp
public void ResetCorrectionHistoryForNewCondition()
{
    sessionErrorHistory.Clear();
}
```

如果当前 `ResetSession()` 已清理 chatHistory，也应同步清理 sessionErrorHistory。

### 4.6 验收标准

- `formalExperiment = true` 时 Inspector 中 `feedbackSensitivity` 修改不影响 Prompt。
- condition 切换后 `sessionErrorHistory.Count == 0`。
- Prompt 中不包含 adaptive retreat 指令。
- debugForceFeedback 自动关闭。
- STT fallback transcript 不会被用作用户 transcript。

---

## 5. P0-2：防止 dialogueReply 纠错泄露

### 5.1 问题

当前 `correctionFeedback.provider` 虽被强制回写，但 `dialogueReply` 始终由主 Avatar 播放。如果 `provider = assistant_agent` 时 `dialogueReply` 含有纠错内容，则主 Avatar 会越界纠错，破坏 provider 条件。

### 5.2 目标

`assistant_agent` 条件下：

- `dialogueReply` 必须只推进任务对话。
- `dialogueReply` 不能包含语法提示、替代表达、错误评价、纠错词。
- 所有纠错内容必须只存在于 `correctionFeedback.feedbackText`。

### 5.3 Prompt 硬约束

在 dialogue generation prompt 中加入：

```text
If feedbackProvider = assistant_agent:
- dialogueReply MUST NOT contain correction, grammar tips, alternative phrasing, or comments about the user's English.
- Do NOT say: "you should say", "a better way", "correct", "wrong", "mistake", "grammar", "instead", "try saying".
- The separate assistant agent will handle all feedback in correctionFeedback.
- Your dialogueReply should only respond as the scenario character and advance the task.
```

在 `dialogue_avatar` 条件中加入：

```text
If feedbackProvider = dialogue_avatar:
- If hasFeedback=true, correction should appear only in correctionFeedback.feedbackText, not inside dialogueReply, unless the condition is dialogue_avatar+recast and the system explicitly uses the recast-then-task-continuation pattern.
```

### 5.4 代码层 Guard

新增工具类，例如：

`Assets/SceneTalkVR/Scripts/Core/CorrectionTextGuards.cs`

```csharp
public static class CorrectionTextGuards
{
    private static readonly string[] CorrectionLeakagePatterns =
    {
        "you should say",
        "should say",
        "you can say",
        "try saying",
        "a better way",
        "better way",
        "correct sentence",
        "correct expression",
        "grammar",
        "grammatical",
        "mistake",
        "wrong",
        "incorrect",
        "instead of",
        "not ",
        "the right way",
        "proper way",
        "actually, you",
        "more natural"
    };

    public static bool LooksLikeCorrection(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        foreach (var pattern in CorrectionLeakagePatterns)
        {
            if (lower.Contains(pattern)) return true;
        }
        return false;
    }
}
```

在 `ApplyExperimentConditionToPayload` 后调用：

```csharp
private void GuardDialogueReplyAgainstCorrectionLeakage(SpringScenePayload payload)
{
    var feedback = payload?.correctionFeedback;
    if (payload == null || feedback == null) return;

    bool assistantProvider = string.Equals(feedback.provider, "assistant_agent", StringComparison.OrdinalIgnoreCase);
    if (!assistantProvider) return;

    if (CorrectionTextGuards.LooksLikeCorrection(payload.dialogueReply))
    {
        Debug.LogWarning($"[SceneTalkVR] Correction leakage detected in dialogueReply under assistant_agent: {payload.dialogueReply}");

        // Formal experiment: replace with safe neutral task continuation.
        if (IsExperimentLocked())
        {
            payload.dialogueReply = BuildSafeTaskContinuation(payload);
            feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "dialogue_reply_leakage_suppressed");
        }
    }
}
```

### 5.5 Safe task continuation 示例

按 scenario 返回短句：

```csharp
private string BuildSafeTaskContinuation(SpringScenePayload payload)
{
    var scenario = currentCondition?.scenarioId ?? payload.taskType ?? string.Empty;
    switch (scenario)
    {
        case "restaurant_reservation":
            return "Sure. What time would you like to come in?";
        case "furniture_shopping":
            return "Got it. What size or style are you looking for?";
        case "gym_membership":
            return "Okay. Are you interested in a monthly plan or a trial visit?";
        case "hotel_check_in":
            return "Thank you. May I confirm the name on your reservation?";
        default:
            return "I see. Could you tell me a little more?";
    }
}
```

### 5.6 验收标准

- `assistant_agent` 条件下，40 条测试集中的 `dialogueReply` 纠错泄露率为 0%。
- 所有泄露事件写入日志：`rationaleTag` 包含 `dialogue_reply_leakage_suppressed`。
- 主 Avatar 不播放任何显式纠错语句。

---

## 6. P0-3：拆分 Prompt / LLM 调用职责

### 6.1 推荐架构

建议将当前单一 LLM 生成改为最少两个逻辑阶段。

```text
用户 transcript
  ├── Dialogue Generator → dialogueReply
  └── Correction Feedback Generator → correctionFeedback
```

首轮还可保留 scene intent parsing，但在正式实验中 scenario 已固定，可跳过自由场景解析。

### 6.2 Prompt 分类

| Prompt | 时机 | 职责 | 输出 |
|---|---|---|---|
| Scene Intent Parsing Prompt | 非正式自由练习首轮 | 解析任务/环境/角色 | `taskType`, `environmentType`, `avatarRole`, `scene` |
| Dialogue Turn Prompt | 每轮 | 只生成主 Avatar 的任务对话 | `dialogueReply` |
| Correction Feedback Prompt | 每轮 | 只判断语言错误并生成反馈 | `correctionFeedback` |
| Post-task Summary Prompt | 任务结束 | 复盘任务完成与错误 | 后续实现 |

### 6.3 P0 可接受实现

如果两次 LLM 调用延迟过高，可以先做“单次调用、双段强约束 JSON”。但建议代码结构上分出方法：

```csharp
private Task<string> GenerateDialogueReplyAsync(string userText, ScenarioContext scenario, ExperimentCondition condition, SttMetadata stt);
private Task<CorrectionFeedbackData> GenerateCorrectionFeedbackAsync(string userText, string dialogueReply, ScenarioContext scenario, ExperimentCondition condition, SttMetadata stt);
```

即使内部暂时共用一次 API，也要让 Prompt 和后处理以这两个职责为单位组织。

### 6.4 推荐输出 Schema

后续轮不要输出 `avatarRole`、`scene` 等字段。

```json
{
  "dialogueReply": "short in-character task reply; must not contain correction if provider=assistant_agent",
  "correctionFeedback": {
    "hasFeedback": true,
    "errorType": "grammar",
    "originalText": "I very like this desk",
    "correctedText": "I really like this desk",
    "feedbackText": "Oh, you really like this desk.",
    "rationaleTag": "adverb_verb_order"
  }
}
```

注意：

- `provider` 和 `style` 可继续由客户端强制回写，不必让 LLM 输出。
- 如果保留 `provider/style` 字段，也必须继续强制回写。
- `targetSpan` 和 LLM 自评 `confidence` 不作为正式实验 P0 字段。

### 6.5 Dialogue Prompt 模板

```text
You are the main scenario character in a VR English speaking practice task.

SCENARIO:
- scenarioId: {scenarioId}
- environment: {environmentType}
- role: {role}
- attitude: {attitude}
- task goals: {taskGoals}

IMMUTABLE EXPERIMENT CONDITION:
- feedbackProvider: {provider}
- feedbackStyle: {style}

TASK:
Generate ONLY the main avatar's dialogueReply.

HARD RULES:
1. The dialogueReply must stay in character and advance the task.
2. The dialogueReply must be 1-2 short sentences.
3. Do NOT change the scene, role, task, provider, or style.
4. If feedbackProvider is assistant_agent, dialogueReply MUST NOT contain any language correction, grammar tip, alternative phrasing, or comment about the user's English.
5. Do NOT include words/phrases such as: "you should say", "a better way", "correct", "wrong", "mistake", "grammar", "instead", "try saying", "more natural".
6. If the user asks to change scene/avatar/condition, politely keep the current task.
7. Output JSON only:
{
  "dialogueReply": "..."
}
```

### 6.6 Correction Prompt 模板

```text
You are an English corrective feedback generator for a VR speaking practice experiment.
You do NOT advance the scenario dialogue. You ONLY decide whether to provide corrective feedback on the user's last utterance.

SCENARIO:
- scenarioId: {scenarioId}
- task context: {taskContext}
- task goals: {taskGoals}

IMMUTABLE EXPERIMENT CONDITION:
- feedbackProvider: {provider}
- feedbackStyle: {style}
- feedbackSensitivity: moderate

USER TRANSCRIPT:
"{userText}"

STT METADATA:
- sttConfidence: {sttConfidence}
- recordingDurationMs: {recordingDurationMs}
- stopReason: {stopReason}

GENERAL POLICY:
1. Detect at most ONE important error per turn.
2. In formal experiment mode, do not adapt based on previous errors.
3. If the utterance is understandable and natural enough for the scenario, set hasFeedback=false.
4. Do not correct fluent, acceptable short task phrases such as "Table for two, please" or "When is check-out?".
5. Do not correct ASR noise or disfluency unless confidence is high and the intended phrase is clear.

STYLE RULES:
- explicit: direct, brief, 1 sentence if possible. It may use "You can say..." or "A better way is...".
- recast: natural reformulation only. Do NOT use correction words. Do NOT use "you should", "mistake", "wrong", "correct", "grammar", "better way", "instead", "not", "try saying", "you mean".

PROVIDER RULES:
- dialogue_avatar: feedbackText should sound like the main character can say it naturally.
- assistant_agent: feedbackText should sound like a short helper cue, but still obey style rules.

OUTPUT JSON ONLY:
{
  "correctionFeedback": {
    "hasFeedback": true/false,
    "errorType": "grammar|unnatural|vocabulary|incomplete|none",
    "originalText": "",
    "correctedText": "",
    "feedbackText": "",
    "rationaleTag": ""
  }
}
```

### 6.7 验收标准

- `dialogueReply` 单独测试时不出现纠错泄露。
- `correctionFeedback` 单独测试时不推进任务对话。
- JSON parse 成功率 ≥ 99%。
- 两个 Prompt 能独立运行和独立日志记录。

---

## 7. P0-4：Recast 强约束与后验校验

### 7.1 Recast 定义

正式实验中，recast 定义为：

> A natural reformulation of the learner's intended meaning using the corrected form, without explicitly saying that the learner made an error.

也就是说，recast 不是解释，不是建议，不是 “You mean...”，不是 “You should say...”。

### 7.2 禁用词列表

在 recast 条件下，`feedbackText` 不应包含：

```text
wrong
mistake
incorrect
correct
grammar
grammatical
should
should say
you should
you can say
try saying
better way
a more natural way
instead
not
rather than
you mean
I mean
the right way
properly
proper way
actually, you
```

### 7.3 Recast 示例

同一输入：`I very like this desk.`

| 条件 | feedbackText |
|---|---|
| dialogue_avatar + explicit | `You can say, "I really like this desk."` |
| dialogue_avatar + recast | `Oh, you really like this desk.` |
| assistant_agent + explicit | `Try: "I really like this desk."` |
| assistant_agent + recast | `I really like this desk.` |

更多示例：

| 用户输入 | Recast |
|---|---|
| `I want reserve a table.` | `You'd like to reserve a table.` |
| `How much cost the monthly plan?` | `How much does the monthly plan cost?` |
| `I have reservation under Johnson.` | `You have a reservation under Johnson.` |
| `What facilities you have?` | `What facilities do you have?` |

### 7.4 代码层校验

新增：

```csharp
public static bool ViolatesRecastPurity(string feedbackText)
{
    if (string.IsNullOrWhiteSpace(feedbackText)) return false;
    var lower = feedbackText.ToLowerInvariant();
    foreach (var term in RecastForbiddenTerms)
    {
        if (lower.Contains(term)) return true;
    }
    return false;
}
```

在 payload 校验阶段：

```csharp
if (IsRecast(feedback.style) && feedback.hasFeedback && CorrectionTextGuards.ViolatesRecastPurity(feedback.feedbackText))
{
    Debug.LogWarning($"[SceneTalkVR] Recast purity violation: {feedback.feedbackText}");

    if (IsExperimentLocked())
    {
        feedback.feedbackText = BuildMinimalRecast(feedback.correctedText, payload);
        feedback.rationaleTag = AppendRationale(feedback.rationaleTag, "recast_purity_repaired");
    }
}
```

`BuildMinimalRecast`：

```csharp
private string BuildMinimalRecast(string correctedText, SpringScenePayload payload)
{
    if (!string.IsNullOrWhiteSpace(correctedText))
    {
        return correctedText.Trim().TrimEnd('.') + ".";
    }
    return string.Empty;
}
```

### 7.5 验收标准

- 40 条测试集中 recast 禁用词违规率 ≤ 5%，目标 0%。
- 所有违规事件写入日志。
- 若发生 repair，日志 `rationaleTag` 包含 `recast_purity_repaired`。

---

## 8. P0-5：STT 低置信度和超短录音硬过滤

### 8.1 问题

当前 STT metadata 只注入 Prompt，LLM 可能忽视：

- `sttConfidence < 0.5`
- `recordingDurationMs < 500`

### 8.2 目标

代码层保证：

- 低置信度不纠错。
- 超短录音不纠错。
- 不调用 correction feedback LLM 或强制覆盖 `hasFeedback=false`。
- Avatar 自然引导用户重录。
- 日志记录过滤原因。

### 8.3 建议实现

在 `RealLLMService.GenerateSceneAndReply()` 或 Orchestrator 调用 LLM 前：

```csharp
private bool ShouldSuppressCorrectionByStt(out string rationaleTag)
{
    rationaleTag = string.Empty;

    if (lastRecordingDurationMs > 0 && lastRecordingDurationMs < 500)
    {
        rationaleTag = "short_recording_suppressed";
        return true;
    }

    if (lastSttConfidence >= 0 && lastSttConfidence < 0.5f)
    {
        rationaleTag = "low_confidence_suppressed";
        return true;
    }

    return false;
}
```

如果抑制：

```csharp
payload.correctionFeedback.hasFeedback = false;
payload.correctionFeedback.errorType = "none";
payload.correctionFeedback.feedbackText = string.Empty;
payload.correctionFeedback.rationaleTag = rationaleTag;
```

`dialogueReply` 可设置为：

```text
Sorry, I didn't catch that clearly. Could you say it again?
```

但注意：这不应被视为纠错反馈，而是录音质量引导。

### 8.4 日志

增加字段：

```csharp
public float sttConfidence;
public string sttProvider;
public int sttLatencyMs;
public string sttFallbackLevel;
public string sttSuppressionReason;
```

### 8.5 验收标准

- confidence 0.3 的输入，`hasFeedback=false`。
- 300ms 录音，`hasFeedback=false`。
- 日志包含 `low_confidence_suppressed` 或 `short_recording_suppressed`。
- 不把 ASR 噪声当作语法错误。

---

## 9. P0-6：正式实验禁用 fallback transcript

### 9.1 问题

`GatewaySpeechInputModule` 在 STT 错误时可能使用硬编码 `fallbackTranscript`，这对 demo 有用，但正式实验中会污染用户真实数据。

### 9.2 实现要求

当 `formalExperiment = true`：

```csharp
useFallbackTranscriptOnError = false;
```

如果 STT 失败：

- 返回明确错误。
- UI 引导用户重录。
- 日志记录 `stt_failed_no_fallback`。
- 不调用 LLM。

### 9.3 建议接口

`GatewaySpeechInputModule` 增加：

```csharp
public void SetExperimentLocked(bool locked)
{
    experimentLocked = locked;
}
```

或者实现接口：

```csharp
public interface ISceneTalkExperimentLockReceiver
{
    void SetExperimentLocked(bool isLocked);
}
```

由 `ExperimentConditionManager` 或 Orchestrator 注入。

### 9.4 验收标准

- formal mode 下断开 STT 网关，不产生 fallback transcript。
- 日志显示 STT failure。
- 不进入 LLM 纠错流程。

---

## 10. P0-7：正式实验禁用 debugForceFeedback

### 10.1 问题

`CorrectionFeedbackPresenter.debugForceFeedback` 如果忘记关闭，会绕过真实 LLM 输出，污染实验。

### 10.2 实现要求

当 `formalExperiment = true`：

```csharp
debugForceFeedback = false;
```

并且 Inspector 中 debug controls 应隐藏或 disabled。

### 10.3 代码建议

在 `CorrectionFeedbackPresenter`：

```csharp
public void SetExperimentLocked(bool locked)
{
    experimentLocked = locked;
    if (experimentLocked)
    {
        debugForceFeedback = false;
    }
}
```

在 `Present()` 开头：

```csharp
if (experimentLocked && debugForceFeedback)
{
    Debug.LogWarning("[CorrectionFeedbackPresenter] debugForceFeedback disabled in formal experiment.");
    debugForceFeedback = false;
}
```

### 10.4 验收标准

- formal mode 下即使 Inspector 曾经勾选 debugForceFeedback，也不会播放 debug 文本。
- 日志无 debug feedback outcome。

---

## 11. P0-8：补齐实验日志字段

### 11.1 当前问题

当前日志能记录条件、轮次和基础 outcome，但不能复核语言内容，因此无法分析：

- recast 是否纯净。
- explicit 是否简洁。
- false positive rate。
- repeated error rate。
- dialogueReply 是否泄露纠错。
- 真实用户 utterance 与 feedbackText 的关系。

### 11.2 P0 必须增加字段

在 `ExperimentTurnLogRecord` 中增加：

```csharp
public string transcript;
public string dialogueReply;
public string feedbackText;
public string originalText;
public string correctedText;
public string rationaleTag;
public float sttConfidence;
public string sttProvider;
public string sttFallbackLevel;
public string sttSuppressionReason;
public int conditionOrderPosition;
public string validationWarnings;
```

### 11.3 P1 建议字段

```csharp
public int sttLatencyMs;
public string rawLlmOutputPath;
public bool dialogueReplyLeakageDetected;
public bool recastPurityViolationDetected;
public bool recastPurityRepaired;
public string taskGoalCompletion;
public string uptakeCandidate;
```

### 11.4 写入位置

- transcript：Orchestrator 获得 transcript 后立即写入 activeTurnLog。
- dialogueReply：LLM payload 返回后写入。
- feedbackText/originalText/correctedText/rationaleTag：`RecordCorrectionPayload()` 写入。
- sttConfidence/provider/fallbackLevel：从 `GatewaySpeechInputModule.LastSttResponse` 写入。
- validationWarnings：后处理 guard 收集。

### 11.5 CSV 注意事项

CSV 中字符串需要转义逗号、换行和双引号。

建议实现：

```csharp
private static string CsvEscape(string value)
{
    if (value == null) return "";
    bool mustQuote = value.Contains(",") || value.Contains("\n") || value.Contains("\r") || value.Contains("\"");
    value = value.Replace("\"", "\"\"");
    return mustQuote ? $"\"{value}\"" : value;
}
```

### 11.6 验收标准

- 每条 turn 日志都包含 transcript、dialogueReply、feedbackText、originalText、correctedText、rationaleTag。
- 无反馈时字段为空字符串，不为 null。
- JSONL 与 CSV 字段一致。
- 能从日志中人工复核一轮完整交互。

---

## 12. P0-9：Enum 白名单校验

### 12.1 目标

确保 LLM 输出不会在 errorType 等字段中生成不可统计的值。

### 12.2 provider/style

provider/style 已有强制回写，但建议仍做 normalize：

```csharp
private static string NormalizeProvider(string value)
{
    return value == "dialogue_avatar" || value == "assistant_agent" ? value : "dialogue_avatar";
}

private static string NormalizeStyle(string value)
{
    return value == "explicit" || value == "recast" ? value : "explicit";
}
```

正式 payload 中仍以 condition 为准。

### 12.3 errorType

白名单：

```csharp
private static readonly HashSet<string> ValidErrorTypes = new HashSet<string>
{
    "grammar",
    "unnatural",
    "vocabulary",
    "incomplete",
    "none",
    "unknown"
};
```

如果 `hasFeedback=false`，强制：

```csharp
errorType = "none";
originalText = "";
correctedText = "";
feedbackText = "";
```

如果 `hasFeedback=true` 但 errorType 非法：

```csharp
errorType = "unknown";
rationaleTag += ";invalid_error_type_repaired";
```

### 12.4 验收标准

- 日志中 errorType 只出现白名单值。
- 无反馈时 errorType 永远为 `none`。

---

## 13. P1-1：Scenario-specific correction guidance

### 13.1 目标

不同场景中“可接受表达”和“值得纠错表达”不同。应避免通用 Prompt 在某些场景中过度纠错。

### 13.2 推荐结构

在 `ExperimentConditionManager` 的 scenario definition 中增加：

```csharp
public string correctionGuidance;
public string acceptableExpressions;
public string commonErrors;
```

或者在 `RealLLMService` 中按 scenario 生成 prompt block。

### 13.3 四个场景建议

#### restaurant_reservation

```text
Acceptable short task phrases:
- "Table for two, please."
- "For tomorrow at seven."
- "Do you have a table by the window?"

Worth correcting:
- "I want reserve a table" -> "I'd like to reserve a table"
- missing auxiliary in questions
- impolite requests when clearly abrupt

Do not over-correct:
- short service phrases that are natural in restaurants
- minor article omissions if the phrase is common and understandable
```

#### furniture_shopping

```text
Acceptable:
- "I'm looking for a wooden desk."
- "Do you deliver?"
- "How much is this chair?"

Worth correcting:
- "I very like this desk" -> "I really like this desk"
- "I want make my room fitting" -> "I want it to fit my room"
- wrong vocabulary for size/material/delivery
```

#### gym_membership

```text
Acceptable:
- "Do you have a monthly plan?"
- "Is there a swimming pool?"
- "Can I try one class?"

Worth correcting:
- "How much cost the plan?" -> "How much does the plan cost?"
- "I want make muscle" -> "I want to build muscle"
```

#### hotel_check_in

```text
Acceptable:
- "I have a reservation under Johnson."
- "When is check-out?"
- "Could I get a quiet room?"

Worth correcting:
- "I have reservation" -> "I have a reservation"
- "What time I must leave?" -> "What time do I need to check out?"
- abrupt requests such as "Give me key" -> "Could I get my key, please?"
```

### 13.4 验收标准

- `Table for two, please.` 不触发纠错。
- `When is check-out?` 不触发纠错。
- 明显错误仍触发。
- false positive rate 低于 5%。

---

## 14. P1-2：处理 dialogue_avatar + recast 的边界

### 14.1 问题

`dialogue_avatar + recast` 中，recast 本身就是自然对话，容易与 `dialogueReply` 混淆，导致：

- recast 被写入 dialogueReply，日志漏记。
- feedbackText 和 dialogueReply 重复播放。
- 用户感知不到纠错事件。

### 14.2 推荐策略：Recast-then-Task-Continuation

统一规定：

- `feedbackText`：只放 recast，一句。
- `dialogueReply`：放任务推进，一句。

示例：

用户：`I very like this desk.`

```json
{
  "dialogueReply": "What size are you looking for?",
  "correctionFeedback": {
    "hasFeedback": true,
    "errorType": "grammar",
    "originalText": "I very like this desk",
    "correctedText": "I really like this desk",
    "feedbackText": "Oh, you really like this desk.",
    "rationaleTag": "adverb_verb_order"
  }
}
```

播放体验：

```text
主 Avatar: Oh, you really like this desk. What size are you looking for?
```

系统内部仍记录为 feedback + dialogue 两部分，但用户听起来是自然连续的。

### 14.3 实现位置

可以在 Prompt 层规定，也可以在播放层合并：

- Preferred：Prompt 层生成两句，播放层按现有顺序播放。
- Optional：若 provider=dialogue_avatar+recast，可在 `AvatarPresentationVoiceModule` 中减少两段播放间隔。

### 14.4 验收标准

- `dialogue_avatar + recast` 有 `hasFeedback=true` 且有 feedbackText。
- feedbackText 不等于 dialogueReply。
- 用户听到的是自然连续句，不是重复两次。

---

## 15. P1-3：Manipulation Check 工具

### 15.1 目标

在正式实验前，团队需要证明四条件被正确操控。

### 15.2 建议新增 Editor 工具

新增菜单：

```text
SceneTalkVR/Diagnostics/Run LLM Manipulation Check
```

功能：

- 读取固定测试集。
- 对四个 provider/style 条件分别调用 LLM pipeline。
- 记录每条输入的：
  - hasFeedback
  - errorType
  - feedbackText
  - dialogueReply
  - recast forbidden term violation
  - dialogueReply correction leakage
  - JSON parse result
- 输出 Markdown 报告到：

```text
Assets/SceneTalkVR/Docs/LLMPipelineManipulationCheckReport.md
```

### 15.3 报告指标

```text
Total cases
JSON parse success rate
assistant_agent dialogue leakage count
recast purity violation count
explicit length violation count
false positive count
low confidence suppression pass count
short recording suppression pass count
hasFeedback distribution per condition
```

### 15.4 验收标准

- 测试集通过率 ≥ 90%。
- JSON parse 成功率 ≥ 99%。
- assistant_agent dialogue leakage = 0。
- recast purity violation ≤ 5%。

---

## 16. Prompt 测试集要求

### 16.1 覆盖范围

至少 40 条输入，覆盖：

- 明显语法错误。
- 轻微语法错误。
- 不自然但可理解表达。
- 词汇误用。
- 礼貌程度不足。
- 无错误表达。
- ASR 噪声。
- 超短输入。
- 中英混杂。
- 重复同类错误。
- 用户偏离任务。
- 用户要求系统不要纠错。
- 用户要求改变场景。
- 用户要求换 Avatar。
- 用户说出与当前实验条件冲突的指令。

### 16.2 最小测试样例

Agent 应创建测试用例文件，例如：

`Assets/SceneTalkVR/Docs/LLMPipelineTestCases.md`

每条包含：

```yaml
- id: T001
  scenarioId: furniture_shopping
  input: "I very like this desk."
  sttConfidence: 0.95
  recordingDurationMs: 2200
  expectedHasFeedback: true
  expectedErrorType: grammar
  expectedExplicitContains: "I really like this desk"
  expectedRecastContains: "I really like this desk"
  recastForbiddenTermsAllowed: false
  dialogueReplyMayContainCorrectionWhenAssistantAgent: false
```

### 16.3 强制测试条件

每条输入应在四种 condition 下测试：

```text
dialogue_avatar_explicit
dialogue_avatar_recast
assistant_agent_explicit
assistant_agent_recast
```

---

## 17. 推荐开发顺序

### Day 1：实验锁定与安全保护

1. 实现 `Experiment Locked Mode`。
2. formal mode 禁用 fallback transcript。
3. formal mode 禁用 debugForceFeedback。
4. 低置信度/超短录音硬过滤。
5. enum 白名单校验。

### Day 2：Prompt 重构

1. 拆出 dialogue prompt 和 correction prompt。
2. 重写 recast 规则。
3. 增加 four-condition few-shot examples。
4. 增加 provider-specific constraints。
5. 增加 scenario-specific correction guidance。

### Day 3：内容后验校验与日志补齐

1. dialogueReply leakage guard。
2. recast purity guard。
3. 补齐日志字段。
4. 日志记录 validation warnings。
5. DemoBrainModule 与新 schema 对齐。

### Day 4：测试集与 manipulation check

1. 创建 40 条测试集。
2. 创建 Editor 测试 runner 或至少命令式测试文档。
3. 运行四条件回归。
4. 输出 manipulation check report。
5. 修复失败 case。

### Day 5：端到端回归

1. Unity Editor 中跑四条件完整流程。
2. PICO 真机跑至少一个 scenario 的四条件流程。
3. 检查 JSONL/CSV 日志。
4. 检查 TTS fallback、recast 无字幕、assistant 小球状态。
5. 形成第二周周报。

---

## 18. 最终验收标准

### 18.1 自动/半自动测试

| 指标 | 目标 |
|---|---|
| 40 条测试集通过率 | ≥ 90% |
| JSON parse 成功率 | ≥ 99% |
| provider/style 字段遵守率 | 100% |
| assistant_agent dialogueReply 纠错泄露率 | 0% |
| recast 禁用词违规率 | ≤ 5%，目标 0% |
| explicit 反馈长度 | 90% 以上 ≤ 2 句 |
| 无错误输入 false positive rate | ≤ 5% |
| low confidence suppression | 100% |
| short recording suppression | 100% |
| P0 日志字段完整率 | 100% |

### 18.2 人工体验验收

用同一输入 `I very like this desk.` 观察四条件：

1. `dialogue_avatar + explicit`
   - 主 Avatar 明确给出更好表达。
   - 辅助小球不说话。

2. `dialogue_avatar + recast`
   - 主 Avatar 自然重述：`Oh, you really like this desk.`
   - 不出现 “mistake / should / correct / better way”。
   - 辅助小球不说话。

3. `assistant_agent + explicit`
   - 主 Avatar 只推进家具购物任务。
   - 辅助小球明确给出更好表达。

4. `assistant_agent + recast`
   - 主 Avatar 只推进任务。
   - 辅助小球只轻量重述正确表达。
   - 不出现显式纠错词。

如果用户听不出四条件差异，本轮开发未通过。

---

## 19. Agent 开发约束

1. 不要破坏现有 `ISceneTalkSpeechInput`、`ISceneTalkBrain`、`ISceneTalkScenePresenter`、`ISceneTalkAvatarVoice` 对外接口，除非必须，并说明兼容方案。
2. 不要移除 demo/fallback 能力，但必须用 `formalExperiment` 将其与正式实验隔离。
3. 不要把自适应纠错删除；应保留为 Adaptive Product Mode，但正式实验下禁用。
4. 不要让 Prompt 变成超长不可维护文本；应拆成函数/模板块。
5. 不要在正式实验中依赖 LLM 自觉遵守关键安全规则；关键规则必须代码层 guard。
6. 所有新增字段必须兼容 Unity `JsonUtility`，避免 nullable、Dictionary、复杂嵌套泛型。
7. 所有日志字段必须 JSONL/CSV 双路径一致。
8. 所有修改应通过 Unity 编译。
9. 所有功能应支持 Editor 和 PICO，formal mode 下不得依赖 Editor-only API。
10. 开发完成后必须提交一份 `LLMPipelineManipulationCheckReport.md`。

---

## 20. Agent 最终交付物

请 Agent 完成开发后输出：

1. 修改文件列表。
2. 每个 P0/P1/P2 任务完成状态。
3. 新增或修改的 public fields / serialized fields。
4. 新增日志字段说明。
5. Prompt 最终版本摘要。
6. 40 条测试集结果。
7. 四条件 manipulation check 结果。
8. 是否仍存在未解决风险。
9. 是否需要人工在 Unity Inspector 中调整任何配置。
10. 是否需要重新运行 Rebuild Demo Rig。

---

## 21. 最重要的开发结论

本轮开发不要追求“让 LLM 更聪明”，而要追求“让实验条件更可控”。

当前项目已经有完整的 P0 客户端和播放链路。真正阻碍正式实验的是：

```text
provider/style 在字段层面正确，但在语言内容和用户体验层面不够可靠。
```

因此本方案的核心是：

```text
Experiment Locked Mode + Prompt 拆分 + dialogueReply 泄露防护 + recast 后验校验 + 完整日志 + manipulation check
```

只有这些完成后，SceneTalkVR 才能从“能跑的 VR 纠错系统”进入“可用于 IEEE VR 论文实验的可控研究平台”。
