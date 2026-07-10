# SceneTalk VR - 具身化纠错反馈智能层开发总结报告 (P0 - P2)

本报告详细总结了 **Spring** 在 **SceneTalk VR** 具身化纠错反馈系统（基于 2x2 实验设计）中所主导的智能判定、元数据联调与学术分析支持等开发工作（覆盖 P0 至 P2 阶段），并在末尾附带了针对实验条件的配置和调整指南。

---

## 1. 阶段开发内容总结

### 1.1 P0 阶段：结构化反馈最小闭环
*   **契约接口定义**：在 [SceneTalkContracts.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs) 中与 Vitor、Edwin 明确并定义了 `CorrectionFeedbackData` 最小字段，确保被 `JsonUtility` 兼容。
*   **离线调试脑实现**：重构了 [DemoBrainModule.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Demo/DemoBrainModule.cs)，针对特定测试用例（如包含语法毛病的关键词 `"very like"`）提供高拟真的 4 种纠错组合模拟输出，实现了离线状态下的完整流程跑通。
*   **System Prompt 规范**：升级了 [RealLLMService.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs) 中的主 System Prompt 格式声明，添加了 `correctionFeedback` 的嵌套 JSON Schema 描述，确保大模型强制以 Strict JSON 格式输出，杜绝解析崩溃。

### 1.2 P1 阶段：真实对话与语音网关联调
*   **网关元数据暴露**：在语音网关层 [GatewaySpeechInputModule.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Voice/Scripts/GatewaySpeechInputModule.cs) 中新增并暴露了 `LastSttResponse`、`LastRecordingDurationMs` 和 `LastRecordingStopReason`，使得系统能够感知用户的语音质量和操作行为。
*   **STT 动态感知 prompt**：在 `RealLLMService` 发起网络请求前，动态拉取网关最新元数据，以 `=== SPEECH CAPTURE METADATA ===` 块的形式注入到 LLM 系统上下文中。
*   **低置信度防误纠错逻辑**：
    *   **ASR 乱码过滤**：当 STT 置信度低于 **0.5** 时，大模型将强行把 `hasFeedback` 设为 `false`，停止任何语法和词汇纠错，避免将语音误识别当作用户语法错误。
    *   **超短录音/误触规避**：当录音时长少于 **500ms** 时自动避开纠偏，并引导模型生成礼貌的“请求重说”回复。

### 1.3 P2 阶段：学术数据质量与学习支持深度挖掘
*   **科研数据标记 (`rationaleTag`)**：在 `CorrectionFeedbackData` 结构中新增 `rationaleTag` 内部字段。在 LLM 发生纠偏或规避时输出具体原因标识（例如 `subject_verb_agreement`、`repetition_ignored` 等），为实验后期统计 and 论文量化/质性分析提供底层数据。
*   **灵敏度策略调参 (`feedbackSensitivity`)**：
    *   在 Inspector 引入可调节参数（`conservative` 保守、`moderate` 中等、`active` 积极）。
    *   在提示词中映射三种处理规则：保守级仅纠正严重阻碍交流的错误；积极级连口语搭配瑕疵和句式不全也强制纠正。
*   **防疲劳去重设计 (`sessionErrorHistory`)**：
    *   服务内部设计了 `sessionErrorHistory` 对话内错误记录器，并在每次解析到错误时入库。
    *   在 System Prompt 中实时暴露错误历史 `correctedErrorsInSession`。要求大模型在遇到重复错误时提高容忍度（将其忽略，或改用温和的 `recast` 风格进行语义重述），保护被试在 VR 沉浸式口语练习中的流畅度与自信心。
*   **数据总结与生命周期托管**：
    *   暴露了 `GetSessionErrorSummary()` 公开 API，可生成当前 Session 各错误类型的触发频率统计报表。
    *   将历史记录器与 `ResetSession()` 及 `CheckAndResetSession()` 重置生命周期绑定，在系统回到 Idle/Finished 时进行强制自动清理，防止下一场实验发生跨会话上下文数据泄露。

---

## 2. 实验条件调整与测试操作方法

在 Unity Editor 中进行联调和正式实验时，您可以通过以下方式调整系统环境：

### 2.1 切换“离线 Mock 测试”与“在线真实大模型”
在场景层级树（Hierarchy）中选中 **`SceneTalkVR Demo Rig`** 节点，查看 **`SceneTalkOrchestrator`** 组件：
*   **使用假大脑（离线）**：将 `Brain Module` 拖入引用为 **`DemoBrainModule`**。
    *   *测试方法*：点击 Play，并在 UI 中输入 `"very like"`，即可快速验证 2x2 状态切换。
*   **使用真实大脑（在线）**：将 `Brain Module` 拖入引用为 **`RealLLMService`**。
    *   *测试方法*：确保 `.env` 配置文件中的 API 秘钥加载成功，使用真实麦克风说话，测试 LLM 的自适应判定能力。

### 2.2 调整 2x2 实验条件 (Provider & Style)
在 Hierarchy 中选中 **`SceneTalkVR Demo Rig`**，找到并查看 **`ExperimentConditionManager`** 组件：
*   **手动测试指定条件**：
    *   组件中的 `Use Condition Order` **取消勾选**。
    *   修改 `Manual Condition` 整数值（例如：`0` 代表 `dialogue_avatar | explicit`，`2` 代表 `assistant_agent | explicit` 等，具体的映射可在其组件脚本中查看或在 Inspector 显示的 Debug Label 中观察）。
*   **按标准被试条件顺序运行**：
    *   勾选 `Use Condition Order`。
    *   系统会自动按 `Condition Order` 数组中的定义，每一轮自动滑移到下一实验条件，以排除顺序效应。

### 2.3 调整纠错反馈的严格程度 (Sensitivity)
在 Hierarchy 中选中 **`SceneTalkVR Demo Rig`**，查看 **`RealLLMService`** 组件：
*   定位到 **`Feedback Strategy`** 下的 **`Feedback Sensitivity`** 字符串字段。
*   根据实验需要填入：
    *   `conservative` （保守策略 - 追求流畅度，极少打断）
    *   `moderate` （平衡策略 - 仅纠正典型错词与语病）
    *   `active` （积极策略 - 追求精确度，高频打断纠错）

### 2.4 手动强制测试特定文本与条件 (Presenter UI 侧)
在 Hierarchy 中选中 **`SceneTalkVR Demo Rig`**，查看 **`CorrectionFeedbackPresenter`** 组件：
*   如果您希望绕过大模型直接强行在 VR 中测试某段 TTS 文本的播放和 Orb 球渲染：
    *   勾选 **`Debug Enable Correction Overrides`**。
    *   勾选 **`Debug Force Feedback`**。
    *   调整 `Debug Provider` 下拉菜单（`DialogueAvatar` / `AssistantAgent`）与 `Debug Style`。
    *   在 `Debug Feedback Text` 中自由编辑要朗读的内容，在 Play 模式下可直接执行测试。
