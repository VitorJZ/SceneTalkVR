# SceneTalkVR 项目总纲与开发行动指南

## 第 1 部分：项目定位、研究主线、总体目标与设计原则

版本：v0.1
日期：2026 年 7 月
项目目标：面向 IEEE VR 2027 投稿的 VR 语言学习中具身化纠错反馈研究平台
团队成员：Vitor / Spring / Edwin

---

# 0. 文档用途

本文档是 SceneTalkVR 后续开发、实验设计、论文写作和团队协作的总纲。它不再把 SceneTalkVR 定义为一个单纯的“生成式 VR 英语口语练习产品”，而是把它重新定义为：

> **一个用于研究 VR 语言学习中具身化纠错反馈角色分配问题的可控实验平台。**

本文档应作为后续所有开发任务、实验任务、论文结构和答辩材料的统一参照。团队后续新增功能时，应优先判断该功能是否服务于本文档定义的研究问题、实验变量、评估指标和系统边界。

---

# 1. 项目背景与方向转变

## 1.1 原始项目定位

SceneTalkVR 最初定位为一个 **情境生成式 VR 英语口语练习系统**。用户可以用自然语言描述希望练习的英语场景，例如咖啡店点单、课堂问答、机场安检、酒店入住等；系统通过 STT 获取用户输入，用 LLM 解析学习任务、环境和角色设定，再通过全景图、Holodeck 布局服务、本地低模资产、Avatar catalog、TTS 和多轮对话模块构建沉浸式英语口语练习体验。

现有阶段性报告已经说明，系统具备从“用户说话”到“场景生成”再到“虚拟角色对话”的完整闭环，并包含 PICO/OpenXR 客户端框架、LLM 结构化 Payload、全景与近景实体混合渲染、语音网关、Avatar catalog/resolver、TTS、动画驱动和 fallback 机制等模块。

这一阶段的系统能力已经较完整，但如果继续将论文主线放在“生成式 VR 英语学习系统”本身，会面临几个问题：

1. **产品化评价边界不清**：需要覆盖多少场景、多少 Avatar、多少口音、多少任务才算足够？
2. **很难和商业产品竞争**：大型公司在语音、Avatar、实时生成、渲染质量上天然具有资源优势。
3. **论文贡献容易分散**：如果同时强调 LLM、场景生成、Avatar、TTS、PICO 适配、语音识别，主贡献会模糊。
4. **实验评价困难**：单纯证明“系统可以运行”不足以构成强研究贡献。

因此，项目需要从“生成式产品系统”转向“可控研究平台”。

---

## 1.2 新的研究定位

经过团队讨论和文献调研，SceneTalkVR 的最终研究定位调整为：

> **VR 语言学习中的具身化纠错反馈研究平台。**

更具体地说，SceneTalkVR 研究的问题是：

> 在 VR 语言学习中，用户一方面需要与虚拟角色进行自然、沉浸、符合情境的交流；另一方面又需要收到语言纠错反馈。
> 当同一个主对话 Avatar 同时承担“交流对象”和“语言教师 / 纠错者”两种角色时，是否会造成角色冲突、破坏对话自然性、增加用户压力或削弱沉浸感？
> 如果引入一个独立辅助 Agent 来承担纠错反馈，是否能改善角色清晰度、社交舒适度和对话连续性？
> 不同反馈方式，如 Explicit Correction 与 Recast，又如何调节这种影响？

这一路线的核心不再是“我们能生成多少场景”，而是：

> **我们发现并定义了 LLM + VR 语言学习中的一个新交互问题：具身化反馈角色分配。**

这使项目从产品工程转向 HCI / VR / 教育技术研究。

---

# 2. 文献调研带来的关键结论

## 2.1 相关研究不是泛 VR 语言学习，而是三类交叉文献

文献调研显示，最接近本项目问题的文献不是一般的 VR 语言学习，而是三类交叉研究：

1. **VR / Social VR 口语练习与外语焦虑研究**
   这些研究证明 VR、Social VR 和 embodied LLM agents 可以支持情境化口语练习、role-play、信心提升和焦虑缓解。

2. **SLA / CALL 中的 oral corrective feedback 研究**
   这些研究提供了纠错反馈类型、反馈时机、learner uptake、explicit correction、recast 等理论基础。

3. **Embodied pedagogical agent / virtual coach / XR conversational agent 研究**
   这些研究说明 feedback provider 的身份、外观、声音、空间位置和社会角色会改变用户对反馈的理解与接受度。

调研报告指出，前两类文献回答“怎么纠错、何时纠错”，第三类文献回答“由谁纠错、为什么反馈来源本身重要”。

这直接支持了我们将论文主线设定为 **Embodied Corrective Feedback Role Assignment**。

---

## 2.2 明确的 research gap

调研报告最重要的发现是：

> 现有研究支持 VR 能提升口语练习的情境性、信心与参与度，但几乎没有直接比较“主对话 Avatar 纠错”与“独立辅助 Agent 纠错”。

这意味着：

* ELLMA-T、Social VR LLM agent、AI tutor 等工作已经证明 embodied LLM agent 可用于语言学习；
* 但这些系统通常把 agent 同时作为 conversation partner 和 tutor / feedback provider；
* 它们没有把 **feedback provider** 作为主要实验变量；
* 也没有系统测量主对话角色承担纠错职责时是否会造成 role conflict、conversation disruption 或 evaluation anxiety。

因此，SceneTalkVR 的论文空白不是：

> VR 能不能帮助语言学习？

而是：

> 在 VR 语言学习中，纠错反馈应如何进行具身化角色分配？

---

## 2.3 核心假设具有间接但强有力的理论支撑

文献调研指出，SLA 文献表明即时口语纠错可能打断交流流、提升情绪负担；recast 自然但不一定被注意到；explicit correction 显著但更具评价意味。VR/HCI 文献则表明 social presence、social fidelity、latency、breaks in presence 和 psychological discomfort 都会影响 embodied interaction 体验。因此，“同一角色从 conversation partner 突然切换成 evaluator / corrector”很可能影响 conversational naturalness、role clarity、social comfort 和 immersion。

这说明我们的主假设是合理的，但需要谨慎表达。

不应预设：

> 辅助 Agent 一定比主 Avatar 好。

而应表述为：

> Feedback provider 的具身分配会改变反馈的社会意义、角色清晰度、对话连续性、社交舒适度和学习支持感。

这让论文即使得到复杂结果也仍然有价值。

例如，可能出现以下结果：

* 主 Avatar + Recast 最自然；
* 辅助 Agent + Explicit Correction 最清楚；
* 主 Avatar + Explicit Correction 最容易造成 role conflict；
* 辅助 Agent + Recast 可能被认为多余；
* 辅助 Agent 提高 learning support，但可能轻微分散注意力。

这些结果都可以转化为设计原则。

---

## 2.4 第一篇论文应聚焦 Provider × Style

文献调研建议，首篇论文不应做三因素或四因素的大设计，而应聚焦：

> **Feedback Provider × Feedback Style**

即：

| 因素                | 条件                           |
| ----------------- | ---------------------------- |
| Feedback Provider | 主对话 Avatar / 辅助 Agent        |
| Feedback Style    | Explicit Correction / Recast |

调研报告明确指出，这一 2 × 2 设计是最清晰、最可解释、也最贴近理论主张的起点；feedback timing 更适合作为控制变量固定在“自然停顿后 + 任务后总结”，rendering level 更适合作为系统 rationale 或独立 pilot，而不是首篇主实验变量。

因此，项目后续开发必须围绕这个主实验服务。

---

# 3. 论文主张与研究问题

## 3.1 论文核心主张

SceneTalkVR 的论文核心主张应表述为：

> **Feedback in VR is not content-neutral.
> The embodied source of corrective feedback changes its social meaning, its disruption to role-play, and its perceived learning value.**

中文表述：

> **VR 中的反馈不是中性的内容传递。反馈由哪个具身实体说出，会改变用户对反馈的社会意义、角色关系、对话连续性和学习价值的感知。**

这句话应成为论文 Introduction、System Design 和 Discussion 的核心。

---

## 3.2 研究问题

建议最终研究问题设定为：

### RQ1：Feedback Provider 主效应

> 在 VR 英语口语练习中，纠错反馈由主对话 Avatar 提供，还是由独立辅助 Agent 提供，会如何影响学习者的角色清晰度、对话自然性、对话连续性、社交舒适度、沉浸感和学习支持感？

### RQ2：Feedback Style 主效应

> Explicit Correction 与 Recast 两种反馈方式，会如何影响学习者对反馈清晰度、自然性、学习价值、压力和纠错采纳的感知？

### RQ3：Provider × Style 交互效应

> Feedback Style 是否调节 Feedback Provider 的影响？例如，Explicit Correction 是否在主对话 Avatar 条件下更容易造成 role conflict，而 Recast 是否在主 Avatar 条件下更自然？

### RQ4：行为指标与主观体验关系

> 不同反馈条件是否会影响 task completion、turns to completion、feedback count、uptake、repeated error rate 和 interruption recovery？这些行为指标与主观体验之间是否存在一致或冲突？

---

## 3.3 可检验假设

后续论文和实验预注册可以考虑以下假设，但不必全部写成强假设：

### H1：Provider 对 Role Clarity 的影响

辅助 Agent 条件相比主 Avatar 条件，会提高用户对“交流内容”和“语言反馈内容”的区分能力，即提高 role clarity。

### H2：Provider 对 Conversation Continuity 的影响

主 Avatar 直接 explicit correction 可能比辅助 Agent explicit correction 更容易破坏 conversation continuity。

### H3：Style 对 Feedback Noticeability 的影响

Explicit Correction 比 Recast 更容易被用户注意到，并带来更高 perceived learning / feedback usefulness。

### H4：Style 对 Conversational Naturalness 的影响

Recast 比 Explicit Correction 更自然，尤其是在主 Avatar provider 条件下。

### H5：Provider × Style 交互

主 Avatar + Explicit Correction 可能在 role conflict 和 social comfort 上表现最弱；主 Avatar + Recast 可能在 naturalness 上表现较好；辅助 Agent + Explicit Correction 可能在 learning support 上表现较好。

### H6：Uptake 行为指标

Explicit Correction 可能带来更高 immediate uptake，但也可能增加 interruption recovery time；Recast 可能更自然但 uptake 较低。

这些假设不应被写成“必须证明”的工程目标，而是实验分析的指导框架。

---

# 4. 项目总体设计原则

## 4.1 从开放生成转向可控实验

SceneTalkVR 未来开发必须遵循：

> **可控性优先于开放性。**

原始系统强调用户可以自由描述任意场景；最终研究平台应强调固定、可复现、可对比的任务型场景。

这意味着：

| 原始方向          | 最终方向                   |
| ------------- | ---------------------- |
| 任意自然语言生成场景    | 标准化 Scenario Grammar   |
| 尽可能丰富的 Avatar | 固定主 Avatar + 控制变量      |
| 开放式自由聊天       | Task-oriented dialogue |
| 场景生成质量展示      | 实验条件控制                 |
| 产品功能完整性       | 研究变量清晰性                |
| Demo 能跑通      | 数据可记录、可复现、可分析          |

这不是削弱系统，而是提升研究价值。

---

## 4.2 研究变量必须尽量少而清晰

首篇论文只把以下两个因素作为正式实验变量：

1. Feedback Provider
2. Feedback Style

其他因素都应固定或作为后续研究扩展：

| 因素                         | 当前处理                           |
| -------------------------- | ------------------------------ |
| Feedback Timing            | 固定为自然停顿后反馈 + 任务后统一总结           |
| Scene Rendering Level      | 固定为全景 + 少量关键 3D props          |
| Avatar Appearance          | 每个任务固定主 Avatar                 |
| Assistant Agent Appearance | 固定 floating feedback assistant |
| Task Type                  | 四个标准化任务，顺序 counterbalance      |
| Voice / Accent             | 尽量固定或按角色固定，不作为变量               |
| Difficulty                 | 每个任务目标数量和语言难度尽量平衡              |

任何新增功能如果会引入额外不可控变量，应谨慎放入主实验。

---

## 4.3 反馈不是对话附属物，而是核心交互事件

系统中每一次纠错反馈都必须被视为一个正式的 interaction event，而不是一条普通回复。

每个 feedback event 必须记录：

* feedbackId
* sessionId
* turnId
* provider
* style
* timing
* original user utterance
* detected error
* error type
* corrected expression
* feedback text
* feedback display modality
* timestamp
* whether followed by uptake
* whether task goal progressed after feedback

这为后续统计和论文分析提供基础。

---

## 4.4 任务型对话优先于自由聊天

为了支持 task completion 和 benchmark-style evaluation，系统必须从自由聊天转向任务型对话。

每个场景必须包含：

* task description
* user goals
* main Avatar role
* initial question
* required slots / goals
* acceptable user expressions
* possible system nudges
* feedback opportunities
* completion criteria

用户可以自然表达，但系统内部必须知道该任务应该完成什么。

---

## 4.5 Learning Memory 是实验基础设施

存档系统不应只被视为游戏式“继续上次对话”，而应设计为：

> **Learning Memory + Experiment Log**

它既服务用户体验，也服务实验数据记录。

Learning Memory 应支持：

* 保存对话历史；
* 保存纠错历史；
* 保存 task goal completion；
* 保存错误类型统计；
* 保存用户 uptake；
* 生成任务后 learning summary；
* 支持继续上次场景；
* 支持导出实验日志；
* 支持后续人工标注和数据分析。

---

# 5. 系统总体架构

## 5.1 当前基础架构

现有 SceneTalkVR 已经具备良好的模块化基础。Vitor 的客户端框架采用“主流程编排器 + 可替换模块接口”的结构，`SceneTalkOrchestrator` 通过 `ISceneTalkSpeechInput`、`ISceneTalkBrain`、`ISceneTalkScenePresenter`、`ISceneTalkAvatarVoice` 等接口调度 STT、LLM、场景呈现、Avatar/TTS 模块。

Spring 的现有模块负责真实 LLM、结构化 Payload、多轮上下文、全景图、Holodeck 后端、HybridScenePresenter、AssetCatalog、fallback 等链路。

Edwin 的现有模块负责 STT/TTS、语音网关、Avatar catalog/resolver、Humanoid prefab、动画、TTS 播放和多轮 Avatar 复用。

这些现有架构不需要推倒重来，而是要在其上新增实验平台层。

---

## 5.2 最终系统分层

最终 SceneTalkVR 应分为六层：

```text
用户 / PICO VR / 语音输入
        ↓
1. Device & Interaction Layer
        ↓
2. Dialogue Orchestration Layer
        ↓
3. Scenario & Task Layer
        ↓
4. Feedback Intelligence Layer
        ↓
5. Embodied Presentation Layer
        ↓
6. Logging / Learning Memory / Evaluation Layer
```

---

## 5.3 Layer 1：Device & Interaction Layer

负责人主要为 Vitor，Edwin 协作语音输入，Spring 协作实验 UI。

职责：

* PICO/OpenXR 支持；
* World Space UI；
* 控制器射线交互；
* 头显前方 UI 重定位；
* Start / Speak / Exit / Confirm / Continue 操作；
* 实验条件选择入口；
* 问卷或任务后评分入口；
* PICO 端性能与输入稳定性。

要求：

* 所有实验条件下交互入口一致；
* 不能让某个条件因为 UI 位置或视觉元素不同而获得额外优势；
* 需要支持实验员快速切换 condition；
* 需要支持自动记录 participantId、condition、taskId；
* 需要提供 debug overlay，但正式实验中可隐藏。

---

## 5.4 Layer 2：Dialogue Orchestration Layer

主要基于 Vitor 的 `SceneTalkOrchestrator` 扩展。

现有状态机已支持 Idle、Listening、Processing、SceneReady、AvatarSpeaking、Finished、Error，并支持首轮练习、后续对话、退出、清理和 reset。

需要新增或扩展：

* `ExperimentSessionState`
* `CurrentTask`
* `CurrentCondition`
* `CurrentTurnIndex`
* `CurrentFeedbackEvent`
* `TaskCompletionStatus`
* `FeedbackPending`
* `PostTaskSummary`

建议在不破坏原 Orchestrator 的基础上，新增一个上层控制器：

```csharp
SceneTalkExperimentController
```

它负责：

* 初始化实验 session；
* 加载任务模板；
* 设置 provider/style 条件；
* 管理任务顺序；
* 触发 Orchestrator 对话轮；
* 调用 feedback pipeline；
* 记录日志；
* 判断任务完成；
* 进入任务后 summary；
* 切换到下一任务。

Orchestrator 继续负责底层对话状态，不应塞入过多实验逻辑。

---

## 5.5 Layer 3：Scenario & Task Layer

这是新的核心模块之一。

建议新增：

```text
ScenarioTemplate
TaskGoal
ScenarioGrammarService
TaskProgressTracker
ScenarioRuntimeState
```

### ScenarioTemplate 字段

```json
{
  "taskId": "hotel_checkin",
  "displayName": "Hotel Check-in",
  "environmentType": "hotel_lobby",
  "mainAvatarRole": "hotel_receptionist",
  "initialQuestion": "Good afternoon! Welcome to the hotel. How may I help you today?",
  "userRole": "hotel_guest",
  "taskDescription": "Check in at a hotel and confirm room details.",
  "goals": [
    {
      "goalId": "confirm_reservation",
      "description": "Confirm your reservation.",
      "required": true
    },
    {
      "goalId": "ask_breakfast",
      "description": "Ask whether breakfast is included.",
      "required": true
    },
    {
      "goalId": "ask_quiet_room",
      "description": "Ask whether the room is quiet.",
      "required": true
    },
    {
      "goalId": "ask_checkout_time",
      "description": "Ask about check-out time.",
      "required": true
    }
  ],
  "props": [
    "front_desk",
    "room_key_card",
    "luggage",
    "hotel_sign"
  ],
  "interactionZone": {
    "avatarPosition": [0, 0, 2.2],
    "userFacingDirection": "forward"
  }
}
```

### 必须支持的四个标准任务

第一版建议固定为：

1. Restaurant Reservation
2. Furniture Shopping
3. Gym Membership
4. Hotel Check-in

这四类任务已在早期具身纠错设计文档中出现，适合作为 2 × 2 被试内实验中的四个任务。

---

## 5.6 Layer 4：Feedback Intelligence Layer

这是论文核心模块。

建议拆成以下组件：

```text
ErrorDetectionService
FeedbackPolicyService
FeedbackGenerationService
FeedbackEventBuilder
UptakeTracker
```

### ErrorDetectionService

输入：

* user transcript
* current task
* dialogue context
* recent feedback history

输出：

```json
{
  "hasError": true,
  "errorType": "grammar",
  "severity": "minor",
  "originalText": "I want reserve a table for five.",
  "correctedText": "I would like to reserve a table for five.",
  "explanation": "Missing 'to' after 'want' and a more polite restaurant reservation expression is preferred.",
  "targetForm": "would like to reserve"
}
```

错误类型至少包括：

* grammar
* vocabulary
* unnatural expression
* incomplete sentence
* pragmatic / politeness issue
* pronunciation / recognition issue，若后续支持

### FeedbackPolicyService

输入：

* condition.provider
* condition.style
* error object
* task state
* dialogue state

输出：

```json
{
  "shouldFeedback": true,
  "provider": "assistant_agent",
  "style": "explicit",
  "timing": "after_natural_pause",
  "modality": "voice_and_caption"
}
```

### FeedbackGenerationService

根据 condition 生成反馈文本。

Explicit Correction 示例：

```text
You should say: “I would like to reserve a table for five,” instead of “I want reserve a table for five.”
```

Recast 示例：

```text
Sure, you would like to reserve a table for five.
```

主 Avatar 条件下，recast 应尽可能保持 in-character。辅助 Agent 条件下，explicit 可以更像学习提示。

---

## 5.7 Layer 5：Embodied Presentation Layer

该层由 Edwin 的 Avatar 系统、Vitor 的 UI/交互、Spring 的反馈策略共同支撑。

包含两个反馈提供者：

### 主对话 Avatar

主 Avatar 是任务世界中的社会角色，例如：

* restaurant staff
* furniture salesperson
* gym receptionist
* hotel receptionist

它负责：

* 任务对话；
* 情境推进；
* 在主 Avatar provider 条件下承担纠错反馈；
* 在非纠错条件下保持任务角色。

### 辅助 Agent

辅助 Agent 是独立 feedback provider。

建议设计为：

* 非人形 floating AI assistant；
* 小型 orb / holographic panel；
* 固定在用户侧前方；
* 不抢占主 Avatar 空间；
* 反馈时短暂亮起；
* 可显示字幕并播放短音频；
* 始终保持中性、简洁、低压迫感。

辅助 Agent 的目标不是做第二个角色，而是提供清晰的反馈角色分离。

---

## 5.8 Layer 6：Logging / Learning Memory / Evaluation Layer

这是后续实验和论文分析的基础。

建议新增：

```text
ExperimentLogger
LearningMemoryService
SessionSaveManager
QuestionnaireManager
LogExporter
```

必须记录：

* participantId
* sessionId
* taskId
* condition
* turnId
* user transcript
* Avatar reply
* error detection result
* feedback event
* task goal progress
* uptake result
* timestamps
* user ratings
* post-task summary
* system errors / fallback events

日志格式建议使用 JSONL，每行一个事件，便于后续 Python 分析。

事件类型包括：

```text
session_start
task_start
user_utterance
avatar_reply
error_detected
feedback_shown
uptake_detected
goal_completed
task_end
questionnaire_submitted
summary_generated
session_end
system_error
```

---

# 6. 团队分工总览

## 6.1 Vitor：VR 客户端、状态机、实验流程与 PICO 稳定性

核心职责：

* 保持 Orchestrator 稳定；
* 支持实验任务流；
* 实现实验 UI；
* 管理任务切换；
* 管理 condition 切换；
* 支持 PICO/OpenXR；
* 支持辅助 Agent 的空间 UI / 交互呈现；
* 支持任务后问卷界面；
* 保证 Demo Rig / Rebuild / Preflight 可用；
* 支持实验员操作流程。

新增开发重点：

```text
SceneTalkExperimentController
ExperimentConditionSelector
TaskFlowController
ExperimentWorldUiController
FeedbackAgentAnchor / FeedbackOrbPresenter
QuestionnairePanel
PICO runtime validation
```

---

## 6.2 Spring：Scenario Grammar、LLM、纠错反馈、任务追踪与日志

核心职责：

* 设计 ScenarioTemplate；
* 实现 ScenarioGrammarService；
* 实现 ErrorDetectionService；
* 实现 FeedbackGenerationService；
* 实现 TaskProgressTracker；
* 实现 UptakeTracker；
* 实现 LearningMemoryService；
* 设计实验日志 schema；
* 设计 prompt；
* 管理 task-oriented dialogue；
* 控制 LLM 输出稳定性。

新增开发重点：

```text
ScenarioTemplate.cs / .json
ScenarioGrammarService.cs
ErrorDetectionService.cs
FeedbackPolicyService.cs
FeedbackGenerationService.cs
TaskProgressTracker.cs
UptakeTracker.cs
LearningMemoryService.cs
ExperimentLogger.cs
LogExporter.cs
```

---

## 6.3 Edwin：语音、Avatar、TTS、反馈语音与动作呈现

核心职责：

* 保持 STT/TTS 网关稳定；
* 支持主 Avatar 对话与纠错语音；
* 支持辅助 Agent 的 TTS；
* 控制语音差异不要污染实验；
* 保持主 Avatar 在多轮中稳定复用；
* 支持 feedback event 的音频播放；
* 支持基础动画：Listen / Think / Speak / Feedback / Idle；
* 支持 fallback，避免实验中断。

新增开发重点：

```text
FeedbackVoiceProfileResolver
AssistantAgentVoiceModule
AvatarFeedbackPresentationMode
TTS cache / pre-generation if needed
Audio latency logging
Avatar feedback animation trigger
Assistant feedback sound / visual cue
```

---

# 7. 本部分结论

第 1 部分定义了 SceneTalkVR 的最终定位：

> SceneTalkVR 是一个用于研究 VR 语言学习中具身化纠错反馈角色分配的可控实验平台。

它的核心论文问题是：

> 当 VR 中的虚拟角色既承担任务互动，又可能承担纠错反馈时，反馈应由主对话 Avatar 还是独立辅助 Agent 提供？不同反馈方式如何影响角色清晰度、对话连续性、社交舒适度、沉浸感、学习支持和纠错采纳？

它的开发总原则是：

* 可控性优先于开放性；
* 研究变量少而清晰；
* 任务型对话优先于自由聊天；
* 反馈事件必须结构化记录；
* Learning Memory 是实验基础设施；
* 主实验聚焦 Provider × Style；
* 其他功能服务于实验而非喧宾夺主。

# SceneTalkVR 项目总纲与开发行动指南

## 第 2 部分：系统模块详细设计与代码开发规范

版本：v0.1
适用范围：Unity 客户端、LLM/反馈服务、Scenario Grammar、Learning Memory、实验日志、Avatar/TTS、PICO 实验运行

---

# 8. 第二阶段开发目标总览

第二阶段开发的目标不是继续扩展“生成式 VR 英语口语练习系统”的产品边界，而是把现有 SceneTalkVR 改造成一个可以稳定支撑 **Provider × Style 具身化纠错实验** 的研究平台。

第二阶段开发必须完成以下能力：

1. 支持标准化实验任务，而不是任意场景自由生成。
2. 支持四个实验条件的稳定切换。
3. 支持主对话 Avatar 与辅助 Agent 两种反馈提供者。
4. 支持 Explicit Correction 与 Recast 两种反馈方式。
5. 支持任务目标追踪、错误检测、反馈生成、uptake 检测。
6. 支持完整 Learning Memory / Experiment Logging。
7. 支持任务后 summary、问卷入口和日志导出。
8. 支持 PICO/Unity Editor 双环境运行。
9. 支持 fallback，但 fallback 必须被记录，不能悄悄影响实验数据。
10. 保持代码模块边界清晰，避免把实验逻辑塞进底层 Orchestrator、Avatar 或 Voice 模块中。

第二阶段完成后，系统应支持以下实验流程：

```text
实验员选择 participantId 与 condition order
    ↓
系统加载 Task 1 + Condition 1
    ↓
用户进入标准化 VR 场景
    ↓
主 Avatar 发起任务对话
    ↓
用户说话
    ↓
STT 得到 transcript
    ↓
LLM / 规则检测用户错误与任务目标进展
    ↓
系统按当前 condition 生成反馈
    ↓
反馈由主 Avatar 或辅助 Agent 呈现
    ↓
系统记录 feedback event、task goal、uptake
    ↓
任务结束后生成 learning summary
    ↓
用户填写短问卷
    ↓
进入下一个任务 / 条件
    ↓
实验结束后导出完整日志
```

---

# 9. 代码架构总原则

## 9.1 不推翻现有架构

现有 SceneTalkVR 已经形成了良好的接口分层：

```text
ISceneTalkSpeechInput
ISceneTalkBrain
ISceneTalkScenePresenter
ISceneTalkAvatarVoice
ISceneTalkSessionReset
ISceneTalkAvatarSessionReset
```

第二阶段开发不应推翻这套架构，而应在它上方增加实验控制层和反馈智能层。

推荐架构：

```text
SceneTalkOrchestrator               // 保持主流程状态机职责
SceneTalkExperimentController       // 新增：实验流程控制
ScenarioGrammarService              // 新增：标准化任务与场景模板
TaskProgressTracker                 // 新增：任务目标追踪
ErrorDetectionService               // 新增：错误检测
FeedbackPolicyService               // 新增：根据条件决定反馈策略
FeedbackGenerationService           // 新增：生成 explicit / recast 文本
FeedbackPresentationRouter          // 新增：把反馈路由给主 Avatar 或辅助 Agent
ExperimentLogger                    // 新增：结构化日志
LearningMemoryService               // 新增：存档与学习档案
QuestionnaireManager                // 新增：任务后问卷
```

现有模块定位：

```text
Vitor Orchestrator / UI / PICO      // 底层交互与流程框架
Spring RealLLM / Scene / Context    // 语言理解、任务、反馈、场景逻辑
Edwin Voice / Avatar / TTS          // 语音、角色、反馈呈现
```

---

## 9.2 实验逻辑不得污染底层模块

底层模块应保持可复用。

不推荐：

```csharp
SceneTalkOrchestrator.cs 里硬编码四个实验条件
AvatarPresentationVoiceModule.cs 里硬编码 explicit/recast 文本
HybridScenePresenter.cs 里硬编码 hotel_checkin 任务
VoiceGatewayClient.cs 里硬编码 participantId
```

推荐：

```csharp
SceneTalkExperimentController 负责 condition/task/session
FeedbackPolicyService 负责 provider/style/timing
FeedbackPresentationRouter 负责分发
Avatar/TTS 模块只负责播放和呈现
Logger 统一记录 condition/task/feedback
```

这样做有三个好处：

1. 不破坏现有 demo 能力。
2. 方便后续扩展 timing、rendering level、自适应反馈。
3. 方便论文中描述模块化系统。

---

## 9.3 所有实验事件必须可追踪

任何会影响实验结果的系统行为都必须记录。

必须记录的事件包括：

* 用户发言；
* STT 结果；
* LLM 输出；
* 检测到的错误；
* 生成的反馈；
* 反馈由谁提供；
* 反馈以什么 style 呈现；
* 用户是否 uptake；
* 任务目标是否完成；
* TTS 是否 fallback；
* LLM 是否 fallback；
* STT 是否 fallback；
* 场景是否 fallback；
* Avatar 是否 fallback；
* 系统延迟；
* 用户问卷。

不能出现：

```text
系统内部 fallback 了，但日志没有记录。
LLM 检测到错误，但没有生成 feedback event。
用户完成了任务 goal，但无法追溯是在哪一轮完成的。
某条件因为 TTS 失败而静默切换到文本反馈，但日志中仍显示 voice feedback。
```

---

# 10. 命名规范与目录建议

## 10.1 Unity 目录结构建议

建议在 `Client/Assets/SceneTalkVR/` 下新增以下目录：

```text
Client/Assets/SceneTalkVR/
  Experiment/
    Scripts/
      Core/
      Scenario/
      Feedback/
      Logging/
      Memory/
      UI/
      Analysis/
    Data/
      Scenarios/
      Conditions/
      Questionnaires/
    Prefabs/
      FeedbackAgent/
      ExperimentUI/
    Docs/
      ExperimentProtocol.md
      LogSchema.md
      ScenarioGrammar.md
      FeedbackDesign.md
```

也可以根据现有项目风格放在 `Scripts/Experiment/` 下，但建议保持实验相关代码与底层 Runtime/Services 分开。

---

## 10.2 C# 命名规范

类名使用 PascalCase：

```csharp
SceneTalkExperimentController
ScenarioGrammarService
FeedbackPolicyService
ExperimentLogger
LearningMemoryService
```

接口名使用 `I` 前缀：

```csharp
IScenarioProvider
IErrorDetectionService
IFeedbackGenerationService
IExperimentLogger
ILearningMemoryStore
```

数据类使用清晰后缀：

```csharp
ExperimentSessionData
ScenarioTemplateData
TaskGoalData
FeedbackEventData
CorrectionResultData
UptakeResultData
QuestionnaireResponseData
```

枚举使用明确语义：

```csharp
FeedbackProviderType
FeedbackStyleType
FeedbackTimingType
FeedbackModalityType
TaskGoalStatus
ExperimentEventType
```

---

## 10.3 JSON 文件命名规范

Scenario 文件：

```text
restaurant_reservation.json
furniture_shopping.json
gym_membership.json
hotel_checkin.json
```

Condition 文件：

```text
provider_avatar_style_explicit.json
provider_avatar_style_recast.json
provider_assistant_style_explicit.json
provider_assistant_style_recast.json
```

Log 文件：

```text
P001_session_20260710_143012.jsonl
P001_summary_20260710_143012.json
```

Learning Memory 文件：

```text
save_P001_hotel_checkin_20260710_143012.json
```

---

# 11. 核心数据结构设计

## 11.1 ExperimentCondition

实验条件必须结构化，不能用字符串散落在代码中。

```csharp
public enum FeedbackProviderType
{
    MainAvatar,
    AssistantAgent
}

public enum FeedbackStyleType
{
    ExplicitCorrection,
    Recast
}

public enum FeedbackTimingType
{
    AfterNaturalPause,
    EndOfTaskSummary,
    ImmediateAfterUserTurn
}

public enum FeedbackModalityType
{
    VoiceOnly,
    CaptionOnly,
    VoiceAndCaption
}

[Serializable]
public sealed class ExperimentCondition
{
    public string conditionId;
    public FeedbackProviderType provider;
    public FeedbackStyleType style;
    public FeedbackTimingType timing;
    public FeedbackModalityType modality;
    public string displayName;
}
```

首篇主实验固定四个 condition：

```text
C1: MainAvatar + ExplicitCorrection
C2: MainAvatar + Recast
C3: AssistantAgent + ExplicitCorrection
C4: AssistantAgent + Recast
```

Timing 第一版统一为：

```text
AfterNaturalPause
```

所有任务结束后统一生成：

```text
EndOfTaskSummary
```

---

## 11.2 ScenarioTemplate

每个实验场景应由模板定义。

```csharp
[Serializable]
public sealed class ScenarioTemplate
{
    public string taskId;
    public string displayName;
    public string environmentType;
    public string taskDescription;
    public string userRole;
    public string mainAvatarRole;
    public string mainAvatarPresetKey;
    public string initialQuestion;

    public TaskGoalData[] goals;
    public ScenarioPropData[] props;
    public ScenarioDialogueHint[] dialogueHints;
    public ScenarioFeedbackTarget[] feedbackTargets;

    public string panoramaKey;
    public string sceneLayoutKey;
}
```

---

## 11.3 TaskGoalData

```csharp
[Serializable]
public sealed class TaskGoalData
{
    public string goalId;
    public string description;
    public bool required;
    public string[] semanticKeywords;
    public string[] acceptableExpressions;
    public TaskGoalStatus status;
}

public enum TaskGoalStatus
{
    NotStarted,
    Mentioned,
    Completed,
    Failed,
    Skipped
}
```

示例：

```json
{
  "goalId": "ask_breakfast",
  "description": "Ask whether breakfast is included.",
  "required": true,
  "semanticKeywords": ["breakfast", "included", "morning meal"],
  "acceptableExpressions": [
    "Is breakfast included?",
    "Does the room include breakfast?",
    "Could you tell me if breakfast is included?"
  ]
}
```

---

## 11.4 CorrectionResultData

错误检测输出结构：

```csharp
public enum ErrorType
{
    None,
    Grammar,
    Vocabulary,
    UnnaturalExpression,
    IncompleteSentence,
    PragmaticsPoliteness,
    PronunciationOrRecognition,
    TaskRelevance
}

public enum ErrorSeverity
{
    None,
    Minor,
    Moderate,
    Severe
}

[Serializable]
public sealed class CorrectionResultData
{
    public bool hasError;
    public ErrorType errorType;
    public ErrorSeverity severity;

    public string originalText;
    public string correctedText;
    public string explanation;
    public string targetForm;

    public float confidence;
    public bool shouldTriggerFeedback;
}
```

第一版应避免检测过多错误。建议每轮最多触发一个反馈，优先级为：

```text
任务推进相关错误 > 明显语法错误 > 不自然表达 > 礼貌/语用优化
```

这样可以避免用户每句话被多个反馈打断。

---

## 11.5 FeedbackEventData

```csharp
[Serializable]
public sealed class FeedbackEventData
{
    public string feedbackId;
    public string sessionId;
    public string taskId;
    public string turnId;

    public FeedbackProviderType provider;
    public FeedbackStyleType style;
    public FeedbackTimingType timing;
    public FeedbackModalityType modality;

    public ErrorType errorType;
    public ErrorSeverity severity;

    public string originalUtterance;
    public string correctedText;
    public string feedbackText;
    public string targetForm;

    public double createdAtUnixMs;
    public double presentedAtUnixMs;

    public bool presentedSuccessfully;
    public string presentationFallbackLevel;
}
```

每一个 feedback event 都必须写入日志，即使最终没有成功播放。

---

## 11.6 UptakeResultData

```csharp
public enum UptakeStatus
{
    NotEvaluated,
    NoUptake,
    PartialUptake,
    SuccessfulRepair,
    NeedsRepair,
    Unclear
}

[Serializable]
public sealed class UptakeResultData
{
    public string feedbackId;
    public string sessionId;
    public string taskId;
    public string turnIdAfterFeedback;

    public UptakeStatus status;
    public string evidenceText;
    public string targetForm;
    public float confidence;
}
```

第一版 uptake 检测范围：

```text
只检查 feedback 后的 1–2 个 learner turns。
只检测目标表达是否被采纳或错误是否被修复。
不声称测量长期学习效果。
```

---

## 11.7 ExperimentSessionData

```csharp
[Serializable]
public sealed class ExperimentSessionData
{
    public string participantId;
    public string sessionId;
    public string experimentVersion;
    public string conditionOrderId;

    public string[] taskOrder;
    public ExperimentCondition[] conditionOrder;

    public double startTimeUnixMs;
    public double endTimeUnixMs;

    public string deviceType;       // Editor / PICO
    public string buildVersion;
    public string notes;
}
```

---

# 12. Scenario Grammar 详细设计

## 12.1 Scenario Grammar 的定位

Scenario Grammar 是替代开放式 Holodeck 生成的核心实验基础设施。

它的目标不是生成任意复杂场景，而是：

1. 提供固定、可复现的任务型 VR 语言练习场景。
2. 平衡四个任务的复杂度。
3. 为 task completion 提供明确目标。
4. 为 feedback detection 提供上下文。
5. 为 Avatar、场景、道具、初始问题提供统一配置。
6. 降低 PICO 渲染压力。
7. 避免场景生成随机性污染实验变量。

---

## 12.2 第一版四个标准任务

### Task 1：Restaurant Reservation

用户角色：customer
主 Avatar：restaurant staff
场景：restaurant reception / phone reservation counter
目标：

1. Reserve a table for five.
2. Ask for a quiet corner table.
3. Ask whether bringing a small birthday cake is allowed.
4. Ask about nearby parking.

初始问题：

```text
Hello! Thank you for calling Bella Restaurant. How can I help you today?
```

---

### Task 2：Furniture Shopping

用户角色：customer
主 Avatar：furniture salesperson
场景：furniture store
目标：

1. Describe the desk size or style you want.
2. Ask about available colors.
3. Ask whether delivery is available this week.
4. Ask about discounts or promotions.

初始问题：

```text
Hi! Welcome to HomeSpace Furniture. What kind of furniture are you looking for today?
```

---

### Task 3：Gym Membership

用户角色：visitor / potential member
主 Avatar：gym receptionist
场景：gym front desk
目标：

1. Ask about monthly membership price.
2. Ask whether there is a student discount.
3. Ask about opening hours.
4. Ask whether you can try one class first.

初始问题：

```text
Hi! Welcome to FitZone. Would you like to know about our gym membership plans?
```

---

### Task 4：Hotel Check-in

用户角色：hotel guest
主 Avatar：hotel receptionist
场景：hotel lobby / front desk
目标：

1. Confirm your reservation.
2. Ask whether breakfast is included.
3. Ask whether the room is quiet.
4. Ask about check-out time.

初始问题：

```text
Good afternoon! Welcome to the hotel. How may I help you today?
```

---

## 12.3 任务平衡要求

四个任务应尽量保持：

* goal 数量一致；
* 任务难度相近；
* 语言复杂度相近；
* 场景熟悉度相近；
* 主 Avatar 社会距离相近；
* 反馈机会数量相近。

避免：

* 一个任务天然更容易出错；
* 一个任务比其他任务更长；
* 一个任务主 Avatar 更像老师；
* 一个任务场景过于严肃导致 anxiety 偏高；
* 一个任务环境过于复杂导致 immersion 偏高。

---

## 12.4 ScenarioTemplate JSON 示例

```json
{
  "taskId": "hotel_checkin",
  "displayName": "Hotel Check-in",
  "environmentType": "hotel_lobby",
  "taskDescription": "You are checking in at a hotel and confirming room details.",
  "userRole": "hotel_guest",
  "mainAvatarRole": "hotel_receptionist",
  "mainAvatarPresetKey": "hotel_receptionist_default",
  "initialQuestion": "Good afternoon! Welcome to the hotel. How may I help you today?",
  "panoramaKey": "hotel_lobby_panorama",
  "sceneLayoutKey": "hotel_frontdesk_low",

  "goals": [
    {
      "goalId": "confirm_reservation",
      "description": "Confirm your reservation.",
      "required": true,
      "semanticKeywords": ["reservation", "booking", "check in", "name"],
      "acceptableExpressions": [
        "I have a reservation.",
        "I would like to check in.",
        "My reservation is under the name..."
      ]
    },
    {
      "goalId": "ask_breakfast",
      "description": "Ask whether breakfast is included.",
      "required": true,
      "semanticKeywords": ["breakfast", "included"],
      "acceptableExpressions": [
        "Is breakfast included?",
        "Does my room include breakfast?"
      ]
    }
  ]
}
```

---

# 13. Feedback Pipeline 详细设计

## 13.1 Pipeline 总览

每轮用户说话后，系统执行：

```text
User transcript
    ↓
TaskProgressTracker
    ↓
ErrorDetectionService
    ↓
FeedbackPolicyService
    ↓
FeedbackGenerationService
    ↓
FeedbackEventBuilder
    ↓
FeedbackPresentationRouter
    ↓
ExperimentLogger
    ↓
UptakeTracker in later turns
```

---

## 13.2 ErrorDetectionService

### 输入

```csharp
public sealed class ErrorDetectionInput
{
    public string userText;
    public ScenarioTemplate scenario;
    public DialogueTurnData[] recentTurns;
    public TaskGoalData[] currentGoals;
    public FeedbackEventData[] recentFeedback;
}
```

### 输出

```csharp
CorrectionResultData
```

### 实现建议

第一版使用 LLM JSON 输出，但必须严格约束 schema。

Prompt 应要求：

1. 不要纠正所有小问题；
2. 每轮最多选择一个最值得反馈的问题；
3. 只在错误明显或表达明显不自然时反馈；
4. 避免因为 ASR 小错误过度纠错；
5. 输出 JSON；
6. 给出 targetForm 以支持 uptake 检测。

示例输出：

```json
{
  "hasError": true,
  "errorType": "Grammar",
  "severity": "Moderate",
  "originalText": "I want reserve a table for five.",
  "correctedText": "I would like to reserve a table for five.",
  "explanation": "The phrase needs 'to' after 'want', and 'would like to' is more polite in a reservation.",
  "targetForm": "would like to reserve",
  "confidence": 0.86,
  "shouldTriggerFeedback": true
}
```

---

## 13.3 FeedbackPolicyService

该模块根据实验条件决定反馈方式。

```csharp
public sealed class FeedbackPolicyInput
{
    public ExperimentCondition condition;
    public CorrectionResultData correction;
    public ScenarioRuntimeState scenarioState;
    public DialogueRuntimeState dialogueState;
}

public sealed class FeedbackPolicyDecision
{
    public bool shouldPresent;
    public FeedbackProviderType provider;
    public FeedbackStyleType style;
    public FeedbackTimingType timing;
    public FeedbackModalityType modality;
}
```

第一版规则：

```text
if correction.hasError == false:
    no feedback

if correction.shouldTriggerFeedback == false:
    no feedback

if alreadyPresentedFeedbackThisTurn:
    no feedback

provider = currentCondition.provider
style = currentCondition.style
timing = AfterNaturalPause
modality = VoiceAndCaption
```

---

## 13.4 FeedbackGenerationService

### Explicit Correction 生成规则

Explicit Correction 应清楚指出错误和推荐表达，但必须简短。

模板：

```text
You can say: “{correctedText}” instead of “{originalText}.”
```

或者：

```text
A better expression is: “{correctedText}.”
```

避免：

```text
Your grammar is wrong because...
You made a mistake.
This is incorrect.
```

原因：过度负面语言会增加 evaluation anxiety，污染 provider/style 效应。

---

### Recast 生成规则

Recast 应保持自然对话推进，不显式指出错误。

模板：

```text
{In-character acknowledgement}, {correctedText}.
```

示例：

用户：

```text
I want reserve a table for five.
```

主 Avatar recast：

```text
Of course, you would like to reserve a table for five.
```

辅助 Agent recast：

```text
You would like to reserve a table for five.
```

注意：辅助 Agent 的 recast 可能显得奇怪，因为它不是对话对象。这个结果本身值得研究，但生成时仍需保持一致。

---

## 13.5 主 Avatar vs 辅助 Agent 的文本差异控制

为保证变量清晰，Provider 改变的是“谁说”，不是反馈内容复杂度。

因此同一 style 下文本长度、清晰度、礼貌程度应尽量一致。

例如 Explicit：

主 Avatar：

```text
You can say: “I would like to reserve a table for five.”
```

辅助 Agent：

```text
You can say: “I would like to reserve a table for five.”
```

Recast：

主 Avatar：

```text
Of course, you would like to reserve a table for five.
```

辅助 Agent：

```text
You would like to reserve a table for five.
```

差异仅保留必要的角色语境。

---

## 13.6 FeedbackPresentationRouter

职责：

```text
根据 FeedbackEventData.provider
把 feedback 路由给 MainAvatar 或 AssistantAgent
```

接口建议：

```csharp
public interface IFeedbackPresenter
{
    IEnumerator PresentFeedback(
        FeedbackEventData feedback,
        Action onComplete,
        Action<string> onError);
}
```

实现：

```text
MainAvatarFeedbackPresenter
AssistantAgentFeedbackPresenter
```

Router：

```csharp
public sealed class FeedbackPresentationRouter : MonoBehaviour
{
    [SerializeField] private MainAvatarFeedbackPresenter mainAvatarPresenter;
    [SerializeField] private AssistantAgentFeedbackPresenter assistantAgentPresenter;

    public IEnumerator PresentFeedback(FeedbackEventData feedback, Action onComplete, Action<string> onError)
    {
        switch (feedback.provider)
        {
            case FeedbackProviderType.MainAvatar:
                yield return mainAvatarPresenter.PresentFeedback(feedback, onComplete, onError);
                break;

            case FeedbackProviderType.AssistantAgent:
                yield return assistantAgentPresenter.PresentFeedback(feedback, onComplete, onError);
                break;
        }
    }
}
```

---

# 14. Assistant Agent 详细设计

## 14.1 形态定位

辅助 Agent 是独立 feedback provider，不是第二对话角色。

推荐形态：

```text
Floating Feedback Orb / Holographic Coach
```

不推荐第一版做：

* 完整人形教师；
* 第二个 Avatar；
* 高度人格化角色；
* 表情复杂角色；
* 会移动到主 Avatar 附近的角色。

原因：

1. 避免引入性别、外貌、权威感等 confound。
2. 保持反馈角色清晰。
3. 降低 PICO 渲染压力。
4. 降低动画和 TTS 同步成本。
5. 便于被试理解“这是辅助反馈者”。

---

## 14.2 空间位置

默认位置：

```text
用户右前方或左前方 30–45 度
距离用户 1.2–1.8 米
高度略低于视线或与视线平齐
不遮挡主 Avatar
不遮挡字幕
不遮挡任务场景关键物体
```

推荐坐标：

```text
local position relative to XR camera:
x = 0.7
y = -0.1
z = 1.4
```

或作为 world-space anchor 相对 UI panel 固定。

---

## 14.3 呈现状态

Assistant Agent 至少有四种状态：

```csharp
public enum AssistantAgentState
{
    Idle,
    Listening,
    FeedbackReady,
    Speaking
}
```

状态表现：

| 状态            | 表现                 |
| ------------- | ------------------ |
| Idle          | 低亮度、半透明、不播放声音      |
| Listening     | 轻微脉冲，表示系统在听        |
| FeedbackReady | 短暂亮起，准备给反馈         |
| Speaking      | 显示字幕气泡，播放 TTS 或提示音 |

---

## 14.4 文本与语音

第一版建议 Assistant Agent 使用：

```text
VoiceAndCaption
```

理由：

* 纯语音可能被漏听；
* 纯字幕可能不够具身；
* voice + caption 最容易确保实验可理解。

但必须记录：

```text
voice playback success / failure
caption shown / hidden
fallback
latency
```

---

# 15. Task Progress Tracking

## 15.1 任务目标追踪原则

任务完成度是客观指标之一。每个任务有 4 个 required goals。系统每轮对用户发言进行 goal matching。

### 输入

```csharp
public sealed class TaskProgressInput
{
    public string userText;
    public ScenarioTemplate scenario;
    public TaskGoalData[] currentGoals;
    public DialogueTurnData[] recentTurns;
}
```

### 输出

```csharp
public sealed class TaskProgressResult
{
    public string[] completedGoalIds;
    public string[] newlyCompletedGoalIds;
    public string[] mentionedGoalIds;
    public string nextSuggestedGoalId;
    public bool taskCompleted;
    public float confidence;
}
```

---

## 15.2 实现方式

第一版可以使用 hybrid：

```text
规则关键词匹配 + LLM semantic check
```

流程：

1. 用关键词快速判断可能 goal。
2. 若关键词命中，调用 LLM 判断是否真正完成。
3. 若 LLM 不可用，规则结果作为 fallback。
4. 所有判断写入日志。

---

## 15.3 Task Completion 指标

每个任务结束时记录：

```text
completedGoalsCount
requiredGoalsCount
taskCompletionRate
turnsToCompletion
timeToCompletion
nudgeCount
feedbackCount
errorCount
```

任务完成条件：

```text
所有 required goals 完成
或达到最大轮数 / 最大时间
```

建议第一版设置：

```text
每个任务最大 8 轮用户发言
每个任务最大 5 分钟
至少完成 3/4 goals 算基本完成
4/4 goals 算完全完成
```

---

# 16. Uptake Tracking

## 16.1 Uptake 定义

本项目中的 uptake 定义为：

> 在系统给出纠错反馈后的 1–2 个用户发言轮次内，用户是否采纳了反馈中的目标表达，或是否修复了被指出的错误。

不测长期学习，只测 immediate uptake。

---

## 16.2 Uptake 检测输入

```csharp
public sealed class UptakeDetectionInput
{
    public FeedbackEventData feedback;
    public string userTextAfterFeedback;
    public DialogueTurnData[] recentTurns;
}
```

---

## 16.3 Uptake 判定

输出：

```csharp
UptakeResultData
```

规则：

```text
SuccessfulRepair:
  用户明确使用 correctedText 或 targetForm，且原错误消失。

PartialUptake:
  用户部分使用目标表达，但仍有小错误。

NeedsRepair:
  用户尝试修正但仍存在原错误。

NoUptake:
  用户继续使用原错误，或完全忽略目标表达。

Unclear:
  用户后续话题转移，无法判断。
```

---

## 16.4 实现建议

第一版：

```text
LLM 判定 + 规则辅助 + 人工抽样复核
```

LLM 输出 JSON：

```json
{
  "status": "SuccessfulRepair",
  "evidenceText": "I would like to reserve a table for five.",
  "confidence": 0.91
}
```

---

# 17. Learning Memory 与存档系统

## 17.1 定位

Learning Memory 同时服务：

1. 用户体验：继续练习、查看学习记录。
2. 实验数据：完整记录所有条件和行为事件。
3. 论文分析：导出 task completion、feedback、uptake 等指标。

它不是单纯游戏存档。

---

## 17.2 Session Save 结构

```csharp
[Serializable]
public sealed class LearningSessionSave
{
    public string saveId;
    public string participantId;
    public string sessionId;
    public string title;

    public string taskId;
    public ExperimentCondition condition;

    public ScenarioTemplate scenario;
    public DialogueTurnData[] dialogueHistory;
    public FeedbackEventData[] feedbackHistory;
    public CorrectionResultData[] correctionHistory;
    public UptakeResultData[] uptakeHistory;
    public TaskGoalData[] goalStates;

    public LearningSummaryData summary;

    public double createdAtUnixMs;
    public double updatedAtUnixMs;
}
```

---

## 17.3 Save Management

必须支持：

```text
CreateSave
LoadSave
RenameSave
DeleteSave
ListSaves
ExportSave
```

第一版 UI 不必复杂，但代码层应支持。

---

## 17.4 Learning Summary

任务结束后生成：

```csharp
public sealed class LearningSummaryData
{
    public string sessionId;
    public string taskId;

    public int completedGoals;
    public int totalGoals;

    public string[] strengths;
    public string[] repeatedErrors;
    public string[] suggestedExpressions;
    public string[] nextPracticeGoals;

    public FeedbackEventData[] keyFeedbackEvents;
}
```

示例：

```text
You completed 3 out of 4 goals. You successfully asked about breakfast and check-out time. 
You may practice a more polite reservation phrase: “I would like to...”
Next time, try asking for a quiet room more clearly.
```

---

# 18. Experiment Logger 详细设计

## 18.1 日志格式

建议使用 JSONL：

```text
每一行是一个独立 JSON event
```

优点：

* 易追加；
* 易恢复；
* 易导入 Python / R；
* 单个事件结构清晰；
* 崩溃时不会损坏整个文件。

---

## 18.2 基础事件字段

所有事件必须包含：

```json
{
  "eventType": "user_utterance",
  "timestampUnixMs": 1783680000000,
  "sessionId": "S001",
  "participantId": "P001",
  "taskId": "hotel_checkin",
  "conditionId": "C3",
  "turnId": "T004"
}
```

---

## 18.3 事件类型

必须支持：

```text
session_start
session_end
task_start
task_end
condition_start
condition_end
user_utterance
avatar_reply
error_detected
feedback_created
feedback_presented
feedback_failed
uptake_detected
goal_completed
task_progress_updated
summary_generated
questionnaire_started
questionnaire_submitted
system_fallback
system_error
latency_recorded
```

---

## 18.4 示例：user_utterance

```json
{
  "eventType": "user_utterance",
  "timestampUnixMs": 1783680001000,
  "sessionId": "S001",
  "participantId": "P001",
  "taskId": "restaurant_reservation",
  "conditionId": "C1",
  "turnId": "T002",
  "text": "I want reserve a table for five.",
  "sttProvider": "tencent",
  "sttFallbackLevel": "none",
  "audioDurationMs": 3400,
  "sttLatencyMs": 820
}
```

---

## 18.5 示例：feedback_presented

```json
{
  "eventType": "feedback_presented",
  "timestampUnixMs": 1783680004300,
  "sessionId": "S001",
  "participantId": "P001",
  "taskId": "restaurant_reservation",
  "conditionId": "C1",
  "turnId": "T002",
  "feedbackId": "F002",
  "provider": "MainAvatar",
  "style": "ExplicitCorrection",
  "timing": "AfterNaturalPause",
  "modality": "VoiceAndCaption",
  "feedbackText": "You can say: “I would like to reserve a table for five.”",
  "presentationLatencyMs": 610,
  "ttsProvider": "tencent",
  "ttsFallbackLevel": "none"
}
```

---

# 19. LLM Prompt 与输出控制规范

## 19.1 所有关键 LLM 输出必须 JSON 化

以下模块的 LLM 输出必须是 JSON：

* error detection
* task progress tracking
* uptake detection
* learning summary generation

不允许普通自然语言输出后再靠字符串解析。

---

## 19.2 Prompt 统一要求

所有 JSON prompt 必须包含：

```text
Return only a valid JSON object.
Do not include markdown.
Do not include code fences.
Do not include explanations outside JSON.
If uncertain, use confidence < 0.5 and conservative output.
```

Spring 现有 RealLLMService 已经处理 JSON mode、`<think>` 清洗、JSON/text schema 隔离，这些经验应继续复用。

---

## 19.3 保守策略

纠错检测必须保守，避免过度纠错。

规则：

```text
如果用户表达可以被主 Avatar 理解，且错误很小，可以不触发反馈。
如果一句话有多个错误，只选择最影响交流或最适合教学的一个。
如果 STT 结果明显不可靠，不触发语法反馈。
如果错误和任务目标无关，优先不打断。
```

原因：过度反馈会污染 social comfort 和 anxiety 指标。

---

# 20. GitHub 工作流与 AI Agent 编程规范

## 20.1 分支策略

建议：

```text
main                    // 稳定可演示版本
develop                 // 集成开发版本
feature/experiment-core
feature/scenario-grammar
feature/feedback-pipeline
feature/learning-memory
feature/assistant-agent
feature/task-tracking
feature/pico-stability
```

每个 feature 分支应有明确 owner。

---

## 20.2 Pull Request 要求

每个 PR 必须包含：

```text
1. 修改目的
2. 涉及模块
3. 是否影响实验变量
4. 是否影响日志 schema
5. 测试方式
6. fallback 行为
7. 截图或录屏，如涉及 UI/VR 呈现
```

---

## 20.3 AI Agent 编程要求

由于团队使用 Codex、Antigravity CLI 等 AI Agent 工具，必须增加以下约束：

1. AI Agent 生成代码前，必须给出模块边界说明。
2. 不允许 AI Agent 大范围重构核心接口，除非团队确认。
3. 每次生成代码后必须运行 Unity 编译或至少静态检查。
4. 不允许把 API key 写入源码。
5. 不允许绕过 ExperimentLogger。
6. 不允许为了临时跑通而静默吞掉错误。
7. 不允许在实验代码中硬编码 participantId 或 condition。
8. 所有 prompt、schema、condition、scenario 文件都要入库。
9. 自动生成代码必须经过人工 review 后 merge。
10. 任何影响实验变量的改动必须记录在 `ExperimentProtocol.md` 中。

---

## 20.4 Commit Message 建议

格式：

```text
feat(experiment): add scenario grammar service
feat(feedback): implement explicit correction generation
fix(logger): record tts fallback events
chore(data): add hotel check-in scenario template
docs(protocol): update provider x style experiment design
```

---

# 21. 第二部分结论

第二部分定义了 SceneTalkVR 后续系统开发的具体代码架构：

* 不推翻现有模块，而是在其上新增实验平台层；
* 使用 Scenario Grammar 替代开放式随机场景生成；
* 使用 Feedback Pipeline 支撑 Provider × Style 实验；
* 使用 TaskProgressTracker 和 UptakeTracker 提供客观指标；
* 使用 Learning Memory 和 JSONL ExperimentLogger 支撑实验分析；
* 使用 Assistant Agent 作为低人格化、低干扰的独立 feedback provider；
* 用明确 GitHub 工作流和 AI Agent 编程规范保证高速开发下的代码质量。

# SceneTalkVR 项目总纲与开发行动指南

## 第 3 部分：具身化纠错实验设计与评估体系

版本：v0.1
适用范围：实验设计、任务流程、被试招募、实验条件控制、问卷设计、行为指标、统计分析、pilot 计划、风险控制、论文结果呈现

---

# 22. 第三阶段目标：把系统能力转化为可发表的实验证据

第一、第二部分定义了项目定位与系统实现。第三部分的核心目标是：

> 把 SceneTalkVR 的系统能力转化为一个严谨、可复现、可分析、可写入 IEEE VR 2027 论文的用户实验。

本阶段不是简单“找人试用系统”，而是要围绕明确研究问题，设计可控实验条件、标准化任务、主观量表、行为日志和统计分析方案。

本实验最终应回答：

1. 纠错反馈由主对话 Avatar 提供还是由辅助 Agent 提供，会不会改变用户体验？
2. Explicit Correction 与 Recast 在 VR 任务型口语对话中各自有什么优势和代价？
3. Provider 与 Style 是否存在交互效应？
4. 不同反馈条件是否影响 task completion、turn count、uptake、interruption recovery 等行为指标？
5. 这些结果能否支持更一般的 VR training feedback role assignment 设计原则？

---

# 23. 实验核心设计

## 23.1 主实验设计

主实验采用：

> **2 × 2 within-subjects design**

两个自变量：

| 自变量               | 条件 1                       | 条件 2                     |
| ----------------- | -------------------------- | ------------------------ |
| Feedback Provider | Main Conversational Avatar | Assistant Feedback Agent |
| Feedback Style    | Explicit Correction        | Recast                   |

四个实验条件：

| 条件编号 | Provider        | Style               | 简写                 |
| ---- | --------------- | ------------------- | ------------------ |
| C1   | Main Avatar     | Explicit Correction | Avatar-Explicit    |
| C2   | Main Avatar     | Recast              | Avatar-Recast      |
| C3   | Assistant Agent | Explicit Correction | Assistant-Explicit |
| C4   | Assistant Agent | Recast              | Assistant-Recast   |

每名被试完成四个任务，每个任务对应一个条件。

---

## 23.2 为什么采用被试内设计

采用 within-subjects design 的理由：

1. 可以减少个体英语水平、VR 熟悉度、外语焦虑基线、口语自信等个体差异的影响。
2. 每个被试都能比较四种反馈条件，适合收集 preference ranking 和访谈材料。
3. 样本量需求相对较低，适合团队在有限时间内完成。
4. Provider × Style 交互效应更容易在同一用户体验差异中被观察到。
5. 任务时间可控制在 45–60 分钟。

潜在问题：

1. 顺序效应；
2. 疲劳效应；
3. 学习效应；
4. 任务难度差异；
5. 被试猜测实验目的；
6. 反馈风格之间的 carryover。

应对方式：

* 使用 counterbalancing；
* 四个任务顺序与四个条件顺序分离设计；
* 每个任务控制在 4–5 分钟；
* 任务后问卷保持短；
* 任务之间短暂休息；
* 访谈中询问是否察觉差异；
* 分析中记录 order 作为协变量或检查项。

---

# 24. 实验任务设计

## 24.1 四个标准化任务

主实验使用四个服务类英语口语任务：

1. Restaurant Reservation
2. Furniture Shopping
3. Gym Membership
4. Hotel Check-in

选择理由：

* 都是常见真实生活英语交流场景；
* 都适合 VR 中第一人称 role-play；
* 都可以用一个主 Avatar 完成；
* 都包含明确 task goals；
* 都容易诱发礼貌表达、语法、不自然表达、句式完整性等反馈机会；
* 任务难度可以相对平衡；
* 与现有项目场景生成、Avatar、TTS 模块兼容。

---

## 24.2 每个任务的目标结构

每个任务包含四个 required goals。原则是：

```text
4 tasks × 4 goals = 每个被试最多 16 个 task goals
```

这样便于计算：

* task completion rate；
* goal completion count；
* turns to completion；
* missing goals；
* task progress after feedback。

---

## 24.3 Restaurant Reservation

### 场景设定

用户正在给餐厅打电话或在餐厅前台预约座位。主 Avatar 是 restaurant staff。

### 用户任务

```text
You are calling a restaurant to reserve a table for a small celebration.
```

### 初始问题

```text
Hello! Thank you for calling Bella Restaurant. How can I help you today?
```

### Goals

| Goal ID       | Goal                                             |
| ------------- | ------------------------------------------------ |
| reserve_table | Reserve a table for five people.                 |
| quiet_corner  | Ask whether a quiet corner table is available.   |
| birthday_cake | Ask whether you can bring a small birthday cake. |
| parking       | Ask about nearby parking.                        |

### 可能触发的语言问题

* “I want reserve a table.”
* “We are five people.”
* “Can we take cake?”
* “Is there parking near?”
* 缺少 polite request，如 “I want...” 过于直接。

---

## 24.4 Furniture Shopping

### 场景设定

用户在家具店购买书桌。主 Avatar 是 furniture salesperson。

### 用户任务

```text
You are speaking with a salesperson at a furniture store to buy a desk.
```

### 初始问题

```text
Hi! Welcome to HomeSpace Furniture. What kind of furniture are you looking for today?
```

### Goals

| Goal ID    | Goal                                         |
| ---------- | -------------------------------------------- |
| desk_style | Describe the desk size or style you want.    |
| colors     | Ask about available colors.                  |
| delivery   | Ask whether delivery is available this week. |
| discount   | Ask about discounts or promotions.           |

### 可能触发的语言问题

* “I want buy a desk.”
* “Do you have other color?”
* “Can delivery this week?”
* “Have discount?”
* 物品描述不完整。

---

## 24.5 Gym Membership

### 场景设定

用户到健身房前台咨询会员。主 Avatar 是 gym receptionist。

### 用户任务

```text
You are visiting a gym and asking about membership options.
```

### 初始问题

```text
Hi! Welcome to FitZone. Would you like to know about our gym membership plans?
```

### Goals

| Goal ID          | Goal                                     |
| ---------------- | ---------------------------------------- |
| monthly_price    | Ask about the monthly membership price.  |
| student_discount | Ask whether there is a student discount. |
| opening_hours    | Ask about opening hours.                 |
| trial_class      | Ask whether you can try one class first. |

### 可能触发的语言问题

* “How much one month?”
* “Have student discount?”
* “When you open?”
* “Can I try class?”
* 缺少助动词或冠词。

---

## 24.6 Hotel Check-in

### 场景设定

用户在酒店前台办理入住。主 Avatar 是 hotel receptionist。

### 用户任务

```text
You are checking in at a hotel and confirming room details.
```

### 初始问题

```text
Good afternoon! Welcome to the hotel. How may I help you today?
```

### Goals

| Goal ID             | Goal                               |
| ------------------- | ---------------------------------- |
| confirm_reservation | Confirm your reservation.          |
| breakfast           | Ask whether breakfast is included. |
| quiet_room          | Ask whether the room is quiet.     |
| checkout_time       | Ask about check-out time.          |

### 可能触发的语言问题

* “I have booking.”
* “Breakfast include?”
* “Room is quiet?”
* “What time check out?”
* 礼貌表达不足。

---

# 25. 条件与任务的 Counterbalancing

## 25.1 基本要求

因为每个被试完成 4 个任务和 4 个条件，所以必须避免：

* 某个条件总是出现在第一个任务；
* 某个条件总是配某个具体任务；
* 某个任务总是出现在最后；
* 顺序效应被误认为条件效应。

---

## 25.2 推荐方案：Latin Square

使用 4 × 4 Latin Square 安排条件顺序：

| Order ID | Task 1 | Task 2 | Task 3 | Task 4 |
| -------- | ------ | ------ | ------ | ------ |
| O1       | C1     | C2     | C3     | C4     |
| O2       | C2     | C4     | C1     | C3     |
| O3       | C3     | C1     | C4     | C2     |
| O4       | C4     | C3     | C2     | C1     |

任务顺序也应 counterbalance。例如：

| Task Order ID | 1          | 2          | 3          | 4          |
| ------------- | ---------- | ---------- | ---------- | ---------- |
| T1            | Restaurant | Furniture  | Gym        | Hotel      |
| T2            | Furniture  | Hotel      | Restaurant | Gym        |
| T3            | Gym        | Restaurant | Hotel      | Furniture  |
| T4            | Hotel      | Gym        | Furniture  | Restaurant |

实验系统中应为每名被试自动分配：

```text
conditionOrderId
taskOrderId
```

并写入日志。

---

## 25.3 样本量与分配

建议目标样本量：

```text
N = 24–32
```

最低可接受：

```text
N = 20
```

理由：

* within-subjects 设计对中等效应较敏感；
* 4 个 Latin Square order 可均衡分配；
* N=24 时每个 order 约 6 人；
* N=32 时每个 order 约 8 人，更稳健。

如果时间紧张，pilot 可先做：

```text
N = 6–8
```

用于验证流程、任务时长、问卷条目、日志完整性，不作为正式结果。

---

# 26. 实验流程

## 26.1 总时长控制

每名被试总时长建议控制在：

```text
45–60 分钟
```

建议分配：

| 环节      | 时间       |
| ------- | -------- |
| 知情同意与说明 | 5 分钟     |
| 背景问卷    | 3–5 分钟   |
| VR 操作训练 | 3–5 分钟   |
| 四个实验任务  | 20–25 分钟 |
| 每任务后短问卷 | 8–12 分钟  |
| 总排序与访谈  | 10–15 分钟 |

---

## 26.2 实验前准备

实验员需要完成：

1. 确认 PICO/Unity 运行正常；
2. 确认语音网关可用；
3. 确认 LLM API 可用；
4. 确认 TTS 可用；
5. 确认 fallback 不会被频繁触发；
6. 创建 participantId；
7. 分配 condition order 和 task order；
8. 清空旧 session；
9. 开启日志记录；
10. 确认录音/日志/问卷路径。

---

## 26.3 被试说明

被试应被告知：

* 本实验研究 VR 英语口语练习中的反馈体验；
* 需要完成四个不同服务场景的英语交流任务；
* 系统可能在对话中提供语言反馈；
* 反馈可能来自不同角色；
* 无需追求完美英语，只要尽力完成任务；
* 可以随时退出；
* 语音转写和系统日志会用于研究分析；
* 数据会匿名化处理。

不应提前强调：

```text
我们在比较主 Avatar 和辅助 Agent 谁更好
我们认为主 Avatar 纠错会破坏沉浸感
```

否则会引导被试。

---

## 26.4 VR 操作训练

训练任务应与正式任务不同，例如：

```text
Ask about opening hours at a library.
```

训练只用于让被试熟悉：

* 戴头显；
* 看主 Avatar；
* 按按钮开始说话；
* 等待系统回复；
* 看到 feedback；
* 看到字幕；
* 退出任务。

训练数据不进入正式分析。

---

## 26.5 单个任务流程

每个正式任务流程如下：

```text
1. 系统显示任务说明
2. 用户确认开始
3. 场景加载
4. 主 Avatar 发出 initial question
5. 用户开始对话
6. 每轮系统识别用户语音
7. TaskProgressTracker 更新任务目标
8. ErrorDetectionService 检测错误
9. 根据当前 condition 生成并呈现反馈
10. 主 Avatar 继续任务对话
11. 达成任务目标或达到时间/轮次上限
12. 系统显示 learning summary
13. 用户填写任务后问卷
14. 进入下一任务
```

---

## 26.6 任务终止条件

单个任务在以下条件之一满足时结束：

1. 所有 required goals 完成；
2. 用户完成至少 3/4 goals，且明确表示任务结束；
3. 达到最大轮数；
4. 达到最大时间；
5. 系统发生不可恢复错误；
6. 被试主动退出。

建议限制：

```text
最大用户发言轮次：8
最大任务时间：5 分钟
```

系统应提醒任务即将结束，但不要显得像考试。

---

# 27. Feedback 呈现规范

## 27.1 Feedback 时机

第一版固定为：

```text
AfterNaturalPause
```

实际执行方式：

1. 用户说话；
2. STT 完成；
3. 错误检测；
4. 主 Avatar 对任务内容作出简短回应；
5. 在自然停顿处呈现 feedback；
6. 对话继续。

或者：

1. 用户说话；
2. 错误检测；
3. feedback 呈现；
4. 主 Avatar 继续回应任务。

两种顺序需要在 pilot 中确定。建议优先使用：

```text
用户说话 → 主 Avatar 任务回应 → feedback → 下一轮
```

原因：

* 保持任务对话先被回应；
* feedback 不抢占主任务；
* 更符合“自然停顿后”原则；
* 有助于测量 feedback 是否打断后续对话。

但如果主 Avatar 条件下反馈本身就是 Avatar 说出，则可以设计为：

```text
用户说话 → 主 Avatar 先做任务回应，再补充纠错
```

示例：

```text
Sure, I can help you reserve a table for five. 
You can also say: “I would like to reserve a table for five.”
```

---

## 27.2 每轮最多一次反馈

每个用户 turn 最多触发一个 feedback event。

原因：

* 避免过度纠错；
* 控制反馈密度；
* 降低被试压力；
* 便于统计 uptake；
* 避免多个错误同时影响结果。

---

## 27.3 Feedback 内容长度控制

Explicit Correction：

```text
建议 1 句，最多 2 句。
```

Recast：

```text
建议 1 句。
```

禁止长篇语法解释。

原因：

* 不希望变成课堂教学；
* 不希望 provider 条件被文本长度污染；
* 不希望 TTS 时长差异过大。

---

## 27.4 Feedback 语气控制

所有反馈必须：

* 礼貌；
* 简洁；
* 中性；
* 不羞辱用户；
* 不使用 “wrong” 等强评价词；
* 不使用考试口吻；
* 不进行长篇教学。

推荐表达：

```text
You can say...
A more natural way is...
You could also say...
```

避免表达：

```text
You are wrong.
Your grammar is incorrect.
That sentence is bad.
You made a mistake.
```

---

# 28. 主观评估体系

## 28.1 评估层级

主观评估分为三层：

1. 标准量表短版；
2. 本研究自定义构念；
3. 总排序与访谈。

每个任务后填写短问卷，实验结束后做总体排序和访谈。

---

## 28.2 每任务后短问卷

每个任务后应测量：

| 构念                                  | 建议条目数 |
| ----------------------------------- | ----- |
| Role Clarity                        | 2     |
| Conversation Continuity             | 3     |
| Conversational Naturalness          | 2     |
| Social Comfort / Evaluation Anxiety | 3     |
| Feedback Usefulness                 | 3     |
| Perceived Learning                  | 2     |
| Presence / Immersion                | 2     |
| Cognitive Load                      | 1–2   |

总条目建议控制在：

```text
15–18 个 Likert items
```

每个条目使用 7 点 Likert：

```text
1 = Strongly disagree
7 = Strongly agree
```

---

## 28.3 Role Clarity 条目

目的：测量用户是否能区分“谁在交流，谁在教学”。

建议条目：

1. 我能清楚理解这个反馈角色的作用。
   `I could clearly understand the role of the feedback provider.`

2. 我能很容易分清哪些内容是在继续情境对话，哪些内容是在给我语言反馈。
   `I could easily distinguish between the ongoing conversation and the language feedback.`

3. 反馈提供者的角色与当前 VR 场景是协调的。
   `The feedback provider’s role felt appropriate for the VR scenario.`

---

## 28.4 Conversation Continuity 条目

目的：测量 feedback 是否打断对话流。

建议条目：

1. 纠正出现后，我仍然能顺畅地继续对话。
   `After receiving feedback, I could continue the conversation smoothly.`

2. 纠正出现后，我需要花时间重新回到对话中。
   `After receiving feedback, I needed time to get back into the conversation.`
   反向计分。

3. 反馈出现后，我仍然感觉自己处在当前的对话情境中。
   `After the feedback, I still felt situated in the ongoing conversation scenario.`

---

## 28.5 Conversational Naturalness 条目

目的：测量对话是否像真实交流。

建议条目：

1. 这次对话感觉自然，而不是像语言测试。
   `The conversation felt natural rather than like a language test.`

2. 反馈的出现方式符合当前对话情境。
   `The way feedback appeared fit the current conversation context.`

3. 当前角色的回应方式让我感觉像真实情境中的交流。
   `The avatar’s responses felt like a realistic interaction in this scenario.`

---

## 28.6 Social Comfort / Evaluation Anxiety 条目

目的：测量犯错压力和被评价感。

建议条目：

1. 在这次 VR 口语练习中，我不担心犯英语错误。
   `I did not worry about making English mistakes during this VR speaking practice.`

2. 我担心这个反馈者会随时纠正我犯的每一个错误。
   `I was afraid that the feedback provider was ready to correct every mistake I made.`
   反向计分。

3. 在这次任务中说英语时，我感到紧张。
   `I felt nervous when I had to speak English during this task.`
   反向计分。

4. 从这个反馈者那里收到反馈不会让我不舒服。
   `Receiving feedback from this provider did not make me uncomfortable.`

---

## 28.7 Feedback Usefulness 条目

目的：测量反馈是否有帮助、清楚、可用。

建议条目：

1. 我能理解这条反馈想让我改进什么。
   `I understood what the feedback wanted me to improve.`

2. 我能把这条反馈和自己刚才的表达联系起来。
   `I could relate the feedback to what I had just said.`

3. 我认为这条反馈有助于改进我的英语表达。
   `I found the feedback useful for improving my English expression.`

4. 这条反馈对我之后的英语对话具有实用价值。
   `The feedback was practical for improving my future English conversations.`

---

## 28.8 Perceived Learning 条目

目的：测量用户主观学习感。

建议条目：

1. 完成这个任务后，我感觉自己学到了一些更自然的英语表达。
   `After this task, I felt that I learned more natural English expressions.`

2. 这个反馈方式帮助我意识到自己的表达问题。
   `This feedback style helped me notice issues in my expression.`

---

## 28.9 Presence / Immersion 条目

使用短版即可，不宜过长。

建议条目：

1. 我感觉自己像是在这个 VR 场景中进行真实交流。
   `I felt like I was actually communicating in the VR scenario.`

2. 我在任务中保持了沉浸感。
   `I felt immersed during the task.`

3. 反馈出现时，我没有明显从情境中“跳出来”。
   `The feedback did not make me feel pulled out of the scenario.`

---

## 28.10 Cognitive Load 条目

建议使用简化 NASA-TLX 思路：

1. 这个任务让我感到精神负担很重。
   `This task was mentally demanding.`

2. 处理反馈让我觉得额外费力。
   `Processing the feedback required extra effort.`

---

# 29. 行为与客观指标

## 29.1 为什么需要行为指标

只靠问卷会使论文像 UX 偏好调查。行为指标可以证明系统不仅改变主观体验，也可能改变任务推进和学习行为。

文献调研明确建议结合 task completion、turns-to-completion、feedback count、uptake、repeated error rate、response latency、interruption recovery 等指标。

---

## 29.2 Task Completion Rate

定义：

```text
completed required goals / total required goals
```

每个任务 4 个 goals。

记录：

```json id="yx0pm5"
{
  "taskCompletionRate": 0.75,
  "completedGoals": 3,
  "totalGoals": 4
}
```

分析：

* 比较四个 condition 下 completion rate；
* 检查 feedback provider 是否影响任务完成；
* 检查 style 是否使用户更关注语言而忽视任务。

---

## 29.3 Turns to Completion

定义：

```text
完成任务所需用户发言轮数
```

如果任务未完成，则记录最大轮数并标记 censored。

意义：

* 反馈过于打断可能增加轮数；
* 有效反馈可能帮助用户更清晰表达目标，减少轮数；
* 可与 conversation continuity 对照。

---

## 29.4 Feedback Count / Feedback Density

定义：

```text
feedback count = 一个任务中呈现的反馈次数
feedback density = feedback count / user turns
```

需要控制：

* 不同条件下 feedback count 应尽量相近；
* 如果某条件 feedback count 明显更多，可能污染体验指标；
* 分析中应把 feedback count 作为协变量或报告描述统计。

---

## 29.5 Error Count

定义：

```text
系统检测到的语言错误数量
```

注意：

* error count 不等于真实语言能力；
* 会受 STT、LLM 检测阈值影响；
* 主要用于确认四个条件中错误机会是否平衡。

---

## 29.6 Correction Uptake

定义：

```text
feedback 后 1–2 个用户 turn 中是否采纳目标表达或修正错误
```

指标：

```text
uptake rate = successful uptake / feedback count
```

分类：

* SuccessfulRepair
* PartialUptake
* NeedsRepair
* NoUptake
* Unclear

分析：

* Explicit 是否比 Recast 更高 uptake；
* Assistant 是否比 Main Avatar 更利于 uptake；
* 主观 usefulness 是否与 uptake 对应；
* naturalness 与 uptake 是否存在 trade-off。

---

## 29.7 Repeated Error Rate

定义：

```text
同类错误在 feedback 后是否再次出现
```

示例：

用户被纠正 “I want reserve...” 后，后续仍说 “I want ask...” 或 “I want buy...” 可视为重复同类结构错误。

指标：

```text
repeated error count / relevant opportunities
```

第一版可作为 exploratory metric，不宜做强 claim。

---

## 29.8 Interruption Recovery

定义：

```text
反馈出现后，用户恢复任务对话所需时间或轮数
```

可操作定义：

1. feedback 结束到用户下一次开始说话的时间；
2. feedback 后第一个用户 turn 是否继续任务；
3. feedback 后是否需要主 Avatar 再次提示任务；
4. feedback 后是否出现“sorry?”、“what?”、“ok...”等停顿性回应。

指标：

```text
recoveryLatencyMs
recoveryTurns
needsNudgeAfterFeedback
```

这是衡量 conversation continuity 的重要行为指标。

---

## 29.9 Response Latency

记录：

* STT latency；
* LLM error detection latency；
* feedback generation latency；
* TTS latency；
* total turn latency。

必须分析 latency，因为 embodied LLM agent 的等待时间会影响 immersion / frustration。

如果某条件因辅助 Agent 多一步 TTS 导致明显更慢，必须报告。

---

# 30. 访谈设计

## 30.1 访谈目标

访谈用于解释定量结果，重点关注：

* 用户如何理解主 Avatar 和辅助 Agent 的角色；
* 哪种反馈更自然；
* 哪种反馈更有帮助；
* 哪种反馈压力更小；
* 用户是否感到主 Avatar “跳戏”；
* 用户是否觉得辅助 Agent 分散注意力；
* 用户如何权衡学习效果与对话沉浸。

---

## 30.2 总排序问题

实验结束后，让用户对四个条件排序：

```text
如果你之后长期使用这个 VR 英语口语练习系统，你最希望使用哪一种反馈方式？
请将四种条件从最想使用到最不想使用排序，并说明理由。
```

四种条件用中性名称展示，避免技术术语：

* 对话角色直接给出明确修改建议；
* 对话角色自然重述你的表达；
* 辅助小助手给出明确修改建议；
* 辅助小助手自然重述你的表达。

---

## 30.3 半结构化访谈问题

建议问题：

1. 在哪个条件下，你最容易理解谁在和你对话、谁在给你反馈？
2. 有没有某个条件让你感觉主对话角色突然不像场景中的角色了？
3. 哪种反馈最不打断你的对话？
4. 哪种反馈让你感觉最有学习帮助？
5. 哪种反馈让你最不紧张？
6. 辅助 Agent 是否帮助你区分交流和纠错？还是让你分心？
7. Recast 这种自然重述，你能意识到它是在纠错吗？
8. Explicit Correction 是否让你更容易记住正确表达？
9. 如果未来长期使用，你希望对话中即时反馈，还是任务结束后总结？
10. 你觉得这种反馈设计是否也适用于面试训练、医疗训练或其他 VR 训练？

---

# 31. Pilot 计划

## 31.1 Pilot 目标

正式实验前必须做 pilot。

Pilot 不是为了验证假设，而是验证：

1. 四个任务是否难度平衡；
2. 每个任务是否能在 5 分钟内完成；
3. 每个任务是否能自然产生反馈机会；
4. feedback 内容是否清楚；
5. 辅助 Agent 是否显眼但不干扰；
6. 问卷条目是否过长；
7. 日志是否完整；
8. TTS/STT/LLM latency 是否可接受；
9. PICO 运行是否稳定；
10. 被试是否理解任务和反馈。

---

## 31.2 Pilot 样本

建议：

```text
N = 6–8
```

至少包括：

* 2 名英语较弱用户；
* 2 名英语中等用户；
* 2 名 VR 不熟悉用户；
* 尽量包含团队外人员。

---

## 31.3 Pilot 后必须调整的内容

Pilot 后检查：

* 哪个任务太难或太简单；
* 哪个 goal 不容易自然表达；
* 哪个 feedback 过长；
* 哪个 condition latency 偏高；
* 问卷是否疲劳；
* 用户是否能分辨 Recast；
* 辅助 Agent 是否被忽视；
* 主 Avatar 纠错是否过于突兀；
* task completion tracker 是否误判；
* uptake tracker 是否可用。

Pilot 后冻结：

```text
正式实验 ScenarioTemplate
正式实验 Prompt
正式实验 Questionnaire
正式实验 Logging Schema
正式实验 Condition Order
```

冻结后不要随意改动，否则影响数据一致性。

---

# 32. 数据分析计划

## 32.1 数据结构

每个被试产生：

```text
4 tasks
4 conditions
每任务多个 turns
每任务一份 post-task questionnaire
实验后一份 ranking + interview
```

数据层级：

```text
participant-level
task-level
turn-level
feedback-event-level
goal-level
questionnaire-level
```

---

## 32.2 主观指标分析

对每个构念计算平均分：

```text
RoleClarityScore
ConversationContinuityScore
NaturalnessScore
SocialComfortScore
FeedbackUsefulnessScore
PerceivedLearningScore
PresenceScore
CognitiveLoadScore
```

统计方法建议：

### 优先方法

使用 linear mixed-effects model：

```text
Score ~ Provider * Style + TaskOrder + ConditionOrder + (1 | Participant)
```

如果样本量较小，可使用 repeated-measures ANOVA 或非参数替代。

### 需要报告

* Provider main effect；
* Style main effect；
* Provider × Style interaction；
* effect size；
* confidence interval；
* p-value；
* descriptive statistics。

---

## 32.3 行为指标分析

### Task Completion

```text
CompletionRate ~ Provider * Style + (1 | Participant)
```

可用：

* mixed-effects model；
* repeated-measures ANOVA；
* Friedman test，如分布不满足。

### Uptake

因为 uptake 是 event-level binary / categorical，可用：

```text
SuccessfulUptake ~ Provider * Style + (1 | Participant)
```

或报告：

* uptake rate by condition；
* descriptive + exploratory statistics。

### Turns to Completion

```text
Turns ~ Provider * Style + Task + (1 | Participant)
```

注意未完成任务可单独分析或视为最大轮数。

---

## 32.4 访谈分析

采用 thematic analysis。

初始 code 建议：

```text
role clarity
role conflict
conversation disruption
feedback helpfulness
feedback pressure
agent distraction
recast unnoticed
explicit useful but evaluative
preference for dual-layer feedback
desire for end-of-task summary
```

访谈结果应服务于解释定量结果，而不是单独发散。

---

# 33. 可能结果与论文解释策略

## 33.1 如果 Assistant-Explicit 最有学习帮助

解释：

* 角色分离让 explicit correction 更容易被接受；
* 辅助 Agent 提供 clear pedagogical frame；
* 主任务 Avatar 保持 conversation partner 身份；
* 支持 separated coach design。

设计原则：

```text
Use a separate feedback agent for explicit corrective feedback in immersive role-play.
```

---

## 33.2 如果 MainAvatar-Recast 最自然

解释：

* Recast 可以被主对话角色自然嵌入任务对话；
* 对话角色不显式评价用户；
* 纠错成为 conversation repair 而不是 teaching event。

设计原则：

```text
Use in-character recasts when naturalness and role-play continuity are prioritized.
```

---

## 33.3 如果 Recast uptake 很低

解释：

* Recast 虽自然，但 noticeability 不足；
* 用户可能没有意识到自己被纠正；
* 需要任务后 summary 或视觉 highlight 补充。

设计原则：

```text
Combine in-dialogue recasts with post-task explicit summaries.
```

---

## 33.4 如果 Assistant Agent 分散注意力

解释：

* 角色分离有代价；
* 额外 agent 需要空间设计和注意力管理；
* 辅助 Agent 应低频、低显著性、在自然停顿出现。

设计原则：

```text
Separate feedback roles carefully; spatially embodied feedback can help role clarity but may introduce attention switching costs.
```

---

## 33.5 如果没有显著差异

也不是失败。可以解释：

* 短时任务中 provider 效应可能较弱；
* 反馈内容与 timing 被控制后，style 可能比 provider 更重要；
* 个体差异或英语水平调节效果；
* 需要更高压力场景或长期使用才能显现；
* 行为指标和访谈可能揭示细微差异。

论文仍可贡献：

* 设计空间；
* 平台；
* null result；
* 测量框架；
* 对 provider effect 的边界条件。

---

# 34. 实验风险与控制

## 34.1 LLM 错误检测不稳定

风险：

* 错误检测过多或过少；
* 不同条件 feedback 数量不均；
* 检测结果误导用户。

控制：

* Pilot 调整 prompt；
* 每轮最多一个反馈；
* feedback count 作为日志指标；
* 低置信度不反馈；
* 关键任务可预设常见错误 pattern；
* 正式分析报告 feedback density。

---

## 34.2 STT 错误污染纠错

风险：

系统纠正的其实是 STT 错误，而不是用户错误。

控制：

* 显示 transcript 给用户确认，或实验员监控；
* STT confidence 低时不触发反馈；
* 允许用户重说；
* 日志记录 STT fallback；
* 访谈询问是否有识别错误影响体验。

---

## 34.3 TTS / LLM latency 影响沉浸

风险：

某个 condition 因 TTS 或反馈生成更慢，导致体验变差。

控制：

* 预生成固定 task initial prompts；
* 尽量缓存 TTS；
* 记录 latency；
* fallback 必须记录；
* pilot 检查条件间 latency 差异；
* 分析中报告 latency。

---

## 34.4 辅助 Agent 外观引入偏差

风险：

用户喜欢/讨厌辅助 Agent 外观，而不是 provider 机制。

控制：

* 使用非人形、中性、简洁设计；
* 不使用强人格化声音；
* 位置固定；
* 视觉效果低调；
* 访谈询问是否因外观影响偏好。

---

## 34.5 任务难度不平衡

风险：

某个任务更难，刚好匹配某个 condition。

控制：

* task order 与 condition order counterbalance；
* pilot 调整任务；
* 每任务 4 goals；
* 分析中加入 Task 作为因素；
* 不把某个任务固定绑定某个条件。

---

## 34.6 被试英语水平差异

风险：

英语水平影响错误数量、焦虑、uptake。

控制：

* 收集自评英语水平；
* 收集英语学习背景；
* 可做简短 speaking confidence baseline；
* within-subjects design；
* 分析中探索 proficiency 作为协变量。

---

## 34.7 被试猜测实验目的

风险：

用户迎合研究假设。

控制：

* 实验说明中不强调主 Avatar vs 辅助 Agent；
* 条件命名中性；
* 访谈最后询问是否猜到实验目的；
* 分析中记录。

---

# 35. 伦理与数据管理

## 35.1 知情同意

必须说明：

* 实验内容；
* 数据类型；
* 语音转写；
* 系统日志；
* 是否录屏/录音；
* 匿名化处理；
* 可随时退出；
* 数据仅用于研究。

---

## 35.2 数据匿名化

participantId 使用：

```text
P001, P002, ...
```

不得在日志中保存真实姓名、学号、手机号。

语音原始文件如保存，应单独加密或尽量不保存。优先保存 transcript 和系统日志。

---

## 35.3 数据存储

建议结构：

```text
ExperimentData/
  raw_logs/
  questionnaires/
  summaries/
  exports/
  anonymized/
  analysis_scripts/
```

正式分析使用 anonymized 数据。

---

# 36. 第三部分结论

第三部分定义了主实验与评估体系：

* 主实验采用 Provider × Style 的 2 × 2 被试内设计；
* 四个标准化任务分别是 Restaurant Reservation、Furniture Shopping、Gym Membership、Hotel Check-in；
* 每个任务包含四个明确 goals；
* 使用 Latin Square counterbalancing 控制顺序效应；
* 每个任务后进行短问卷，实验后进行排序与访谈；
* 主观指标包括 role clarity、conversation continuity、naturalness、social comfort、feedback usefulness、perceived learning、presence、cognitive load；
* 行为指标包括 task completion、turns to completion、feedback density、uptake、repeated error、interruption recovery、latency；
* Pilot 是正式实验前的必要步骤；
* 分析应结合 mixed-effects model、描述统计和 thematic analysis；
* 即使结果复杂或部分 null，也可以转化为 VR feedback role assignment 的设计原则。


