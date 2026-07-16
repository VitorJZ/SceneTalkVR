# 🎓 SceneTalk VR 具身纠错实验开发情况与配置指南

本报告旨在梳理和总结 SceneTalk VR 项目中与“具身纠错实验（Embodied Correction Experiment）”相关的系统架构、核心配置项、内置场景定义以及数据沉淀机制，以便于开发者和实验设计人员快速理解与调试。

---

## 1. 具身纠错实验设计理念

本实验的核心目标是探索在虚拟现实（VR）具身英语口语学习环境中，不同的**反馈主体（Provider）**与**反馈风格（Style）**对学习者口语纠错效果、认知负荷及学习体验的影响。

### 1.1 实验自变量 (2 × 2 设计)

系统支持以下两种核心维度的交叉配置：

| 反馈主体 (Provider) \ 反馈风格 (Style) | 显性反馈 (Explicit) | 隐性反馈 (Recast) |
| :--- | :--- | :--- |
| **场景角色 (Dialogue Avatar)** | 场景 NPC 直接以口头和字幕形式明确指出用户的语法错误，并给出正确示范。 | 场景 NPC 不直接指出错误，而是以正确的句式和内容自然地顺应对话逻辑进行回复。 |
| **辅助助理 (Assistant Agent)** | 悬浮在半空中的独立 AI 助理（发光体）开口并以字幕形式明确指出用户的错误，Avatar 保持安静。 | 辅助助理在空中以正确的句式自然重复用户的意图，Avatar 随后继续正常对话。 |

---

## 2. 核心配置详解

### 2.1 反馈主体 (Feedback Providers)

在 C# 代码中定义为常量（[ExperimentConditionManager.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs#L12-L13)）：

*   **`dialogue_avatar` (场景角色)**：
    *   **表现形式**：由对话中的 NPC（如咖啡师、前台接待员）直接对用户的口语进行纠错或重塑。
    *   **播放时序**：在流式对话结束后，由 NPC 自行发声播报。
*   **`assistant_agent` (辅助助理)**：
    *   **表现形式**：场景中有一个独立的虚拟助理（在 Demo 预制体中表现为一颗发光的 3D 浮空光球，挂载了 `CorrectionAgentPresenter`）。
    *   **播放时序**：在 Avatar 说完流式台词并停顿 0.5s 后，光球亮起并由专属音频源播放纠错信息，实现主次分明。

### 2.2 反馈风格 (Feedback Styles)

*   **`explicit` (显性纠错)**：
    *   大模型在 `correctionFeedback` 中生成明确的错误说明（如 `"Remember to say: I really like this topic, not I very like this topic."`）。
    *   客户端会唤醒**纠错展示面板**（UI 弹出红色/橙色的语法反馈），并由指定主体播放纠错音频。
*   **`recast` (隐性重塑)**：
    *   大模型在 `correctionFeedback` 中不进行说教，而是自然重塑用户的正确表达方式（如 `"You mean you'd like a latte?"`）。
    *   客户端仅展现弱化的重塑提示语，不打断学习者的心理连贯性。

### 2.3 实验条件预设 (Condition Presets)

系统内置了 4 种实验状态预设，对应一个四步循环，可通过 `ExperimentConditionManager` 自动或手动轮转：

1.  **`DialogueAvatarExplicit`** (`dialogue_avatar_explicit`): 场景角色 + 显性纠错
2.  **`DialogueAvatarRecast`** (`dialogue_avatar_recast`): 场景角色 + 隐性重塑
3.  **`AssistantAgentExplicit`** (`assistant_agent_explicit`): 辅助助理 + 显性纠错
4.  **`AssistantAgentRecast`** (`assistant_agent_recast`): 辅助助理 + 隐性重塑

---

## 3. 内置固定场景与任务定义

系统在 [ExperimentConditionManager.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs#L815) 中内置了 **4 个标准化实验场景（Tasks）**。每个任务都包含了具身环境所需的全部元数据：

### 3.1 餐厅订位 (`restaurant_reservation`)
*   **背景上下文**：学习者需要通过与服务人员交谈，在餐厅预订一张桌子。
*   **初始引导语**：*"Good evening. What date, time, and party size would you like to reserve?"*
*   **通关目标 (Goals)**：
    1.  说出预订的日期和时间 (`state date and time`)
    2.  说出就餐人数 (`state party size`)
    3.  询问是否有空位 (`ask about availability`)
*   **具身摆设**：餐厅全景图（`demo://restaurant-360`） + 摆放在面前的一张桌子和一把椅子。
*   **Fallback 角色**：咖啡师 (`barista`)

### 3.2 选购家具 (`furniture_shopping`)
*   **背景上下文**：学习者在家具店购物，需要向店员描述自己的家具偏好、预算和配送需求。
*   **初始引导语**：*"Welcome in. What kind of furniture are you looking for today?"*
*   **通关目标 (Goals)**：
    1.  描述所需要的家具种类 (`describe the item needed`)
    2.  询问价格 (`ask about price`)
    3.  询问物流配送 (`ask about delivery`)
*   **具身摆设**：家具店全景图（`demo://furniture-store-360`） + 面前的一张陈列桌与椅子。
*   **Fallback 角色**：店员 (`clerk`)

### 3.3 健身房咨询 (`gym_membership`)
*   **背景上下文**：学习者向健身房教练咨询会员计划、场馆设施及试用课程。
*   **初始引导语**：*"Hi. Are you interested in a monthly plan, a yearly plan, or a trial visit?"*
*   **通关目标 (Goals)**：
    1.  询问会员资费套餐 (`ask about membership plans`)
    2.  询问健身房器械和设施 (`ask about facilities`)
    3.  咨询免费试用体验 (`ask about a trial visit`)
*   **具身摆设**：健身房全景图（`demo://gym-360`） + 旁边摆放的绿色盆栽和桌子。
*   **Fallback 角色**：教练 (`instructor`)

### 3.4 酒店入住 (`hotel_check_in`)
*   **背景上下文**：学习者在酒店前台办理入住登记，确认房间和退房时间。
*   **初始引导语**：*"Welcome to the hotel. May I have the name on your reservation?"*
*   **通关目标 (Goals)**：
    1.  报上预约人姓名 (`give booking name`)
    2.  询问房间详情/朝向等 (`ask about room details`)
    3.  询问最晚退房时间 (`ask about check-out time`)
*   **具身摆设**：酒店大堂全景图（`demo://hotel-lobby-360`） + 面前的前台接待桌与椅。
*   **Fallback 角色**：前台接待员 (`clerk`)

---

## 4. 实验数据沉淀与日志记录机制

为了保障实验数据的严谨性与后期学术分析的便利性，系统具备多维度的自动化日志沉淀功能。

### 4.1 日志输出格式与路径

*   **路径**：`Application.persistentDataPath` 目录下的 `SceneTalkVR/ExperimentLogs/`（在 Windows 编辑器中通常位于 `C:\Users\<Username>\AppData\LocalLow\VitorJZ\SceneTalkVR\SceneTalkVR\ExperimentLogs`）。
*   **格式**：
    *   **`.jsonl` (JSON Lines)**：记录每一轮对话的极详尽原始数据，适合进行大规模数据回放。
    *   **`.csv` (逗号分隔符文件)**：导出为格式化的表格文件，可直接使用 Excel、SPSS 或 Python Pandas 进行统计学分析。

### 4.2 核心日志字段说明

导出的 CSV/JSONL 文件头（[ExperimentConditionManager.cs:L1002](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs#L1002)）包含以下 30 个核心统计指标：

*   **基本标识**：`participantId` (被试编号)、`sessionId` (实验会话 ID)、`turnId` (回合 ID)、`turnIndex` (本会话回合计数)。
*   **自变量记录**：`conditionId` (实验条件，如 `assistant_agent_explicit`)、`scenarioId` (场景 ID)、`provider` (反馈主体)、`style` (反馈风格)。
*   **口语指标**：`transcript` (用户的语音转写文字)、`sttConfidence` (STT 识别置信度)、`recordingDurationMs` (用户录音时长，用于分析反应时)。
*   **大模型响应**：`dialogueReply` (Avatar 的台词回复)、`feedbackText` (纠错音频读词)、`originalText` (大模型识别到的错误句子片段)、`correctedText` (大模型给出的正确表达推荐)。
*   **容错与回退**：`moduleFallback` (各模块降级情况，如语音网关超时回退)、`sttSuppressionReason` (静音或超短音频的拦截过滤原因)。

---

## 5. 当前流式响应架构与时序整合现状

项目在近期成功引入了 **结构化 JSON 增量解析** 与 **流式分句播放** 机制，已完成了对实验的深度整合：

```
                    【 用户发音输入 】
                            │
                            ▼
           【 触发 大模型流式请求 (stream: true) 】
                            │
          ┌─────────────────┴─────────────────┐
          ▼                                   ▼
【实时 SSE 文本解包】                 【最终 Payload 解析】
          │                                   │
【增量扫描 "dialogueReply"】          【流式彻底结束，获取全量 JSON】
          │                                   │
【按标点符号断句并发射】                      │
          │                                   ▼
          ▼                     【触发 PresentScene 呈现 3D 布局】
【语音系统 (AudioQueue) 串行播放】            │
          │                                   ▼
          │                     【UI Subtitle 全量更新 (RefreshUi)】
          │                                   │
          └─────────────────┬─────────────────┘
                            ▼
                 【 等待 Avatar 说话完全结束 】
                            │
                            ▼
                 【 停顿 0.5 秒 (自然过渡) 】
                            │
                            ▼
          【 触发 Assistant Agent / Avatar 纠错播报 】
```

### 5.1 本地测试快捷键
在编辑器运行态下，如果在配置文件中勾选了 `Use Developer Text Console`，屏幕底部会浮现 **Developer Text Prompt Console**，允许直接打字输入来替代麦克风录音，同时完美保留流式 TTS 播放，能极大加快纠错时序、UI 字幕在不同实验组下的测试速度。

---

## 6. 讨论与后续优化建议

在当前的实验脚手架基础之上，若要进一步提高实验的严谨性，建议在后续关注以下几点：
1.  **被试反应时（Reaction Time）的精确度**：目前 `recordingDurationMs` 采用的是从松开录音键到按下录音键的物理时间差，这包含了用户的思考时间。后续若需精细化分析，可以通过麦克风实际分贝值，计算从 NPC 语音播放完毕到用户开始说话之间的“静音延迟”。
2.  **纠错触发率的控制**：目前的 `RealLLMService` 在 Prompt 中要求对用户的错误进行检测。由于大模型的温度值（Temperature）和幻觉，纠错的判定标准（Sensitivity）可能存在轻微浮动。在正式实验中，建议使用固定的一套“含语法错误的用户录音”作为基准测试，来校准大模型的 Sensitivity。
