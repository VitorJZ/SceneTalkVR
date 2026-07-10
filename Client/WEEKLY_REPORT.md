# SceneTalk VR - 开发周报

**汇报周期**：本周
**负责板块**：LLM 大脑与场景生成 (Spring 负责部分)
**汇报人员**：Spring

---

## 📢 本周工作进展总结

本周核心完成了 **“具身化纠错反馈机制”（Embodied Corrective Feedback）** 的全链路设计与集成开发（覆盖 P0 至 P2 阶段）。通过数据契约设计、语音网关元数据融合以及大模型 Prompt 的策略调优，构建了高科研强度的自适应口语纠错闭环，并成功解决了多项代码编译和联调兼容问题。

---

## 🛠️ 详细开发进展

### 1. P0 阶段：结构化纠错反馈骨架搭建
*   **接口契约化定义**：在 [SceneTalkContracts.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs) 中定义了 `CorrectionFeedbackData` 核心数据结构，包含纠错文本、类型、错字区间和置信度等字段，用以匹配 2x2 实验设计。
*   **离线调试脑实现**：重构了 [DemoBrainModule.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Demo/DemoBrainModule.cs)。在无网路或测试 API 时，输入固定句式（如带有 `"very like"`）能自动根据当前的实验条件，模拟产生出对应的 Orb 荧光球提示语（Explicit）或 Avatar 追问重述（Recast），打通了端到端逻辑。
*   **Prompt 格式规范化**：修改了 [RealLLMService.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs) 的 System Prompt，制定了严格的纠错 JSON Schema 约束，阻断大模型胡乱输出导致的解析错误。

### 2. P1 阶段：STT 网关元数据感知与防错保护
*   **元数据捕获集成**：在 [GatewaySpeechInputModule.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Voice/Scripts/GatewaySpeechInputModule.cs) 中暴露了 `LastSttResponse`、`LastRecordingDurationMs` 和 `LastRecordingStopReason` 变量。
*   **大模型环境感知**：使 `RealLLMService` 能在发出请求前动态读取上述录音元数据，注入至 System Prompt 中。
*   **ASR 低置信度屏蔽机制**：
    *   **低置信度防误判**：当 STT 平均置信度小于 **0.5** 时，强制重置 `hasFeedback = false`，防范语音识别软件产生的识别错误被大模型误判定为用户的语法语病。
    *   **超短录音静默**：若录音小于 **500ms**（属手抖或误触），则静默跳过纠错，只播放 Avatar 的重录引导音。

### 3. P2 阶段：学术数据归档与调参管理支持
*   **学术级分析标记 (`rationaleTag`)**：在纠错反馈数据中增加了内部字段 `rationaleTag`，由大模型在决定纠错或跳过时输出成因标识（如 `active_sensitivity_filter`、`subject_verb_agreement` 等），支持实验后期问卷比对。
*   **纠错严格度调参 (`feedbackSensitivity`)**：在 `RealLLMService` 引入了 `conservative`（保守）、`moderate`（默认，中等）、`active`（积极）参数，大模型将根据不同策略判定打断频次。
*   **多轮重复错误负荷规避**：引入 `sessionErrorHistory`。大模型可以通过 `correctedErrorsInSession` 获取当前会话已纠错历史。若用户短期内重复犯同一类型错误，大模型会执行自适应退让（不再喋喋不休纠正，或软化为 Recast 隐式流），以此维持 VR 练习流畅度。
*   **多轮数据聚合统计**：设计了 `GetSessionErrorSummary()` 报表接口，并在重置生命周期中强绑定清除逻辑，规避被试跨场次数据污染。

### 4. 主线合并与编译修复
*   修复了 `RealLLMService` 在 Rebuild 时发生的 `GatewaySpeechInputModule` 缺失命名空间编译报错（CS0246）。
*   完成了 `remotes/origin/main` 远端主线合并，同步了 Edwin 新增的腾讯云 TTS 纠错助手音色下拉选择（`TencentVoiceType`）和 Vitor 的实体交互优化。

---

## 📂 本周产出文档

为了方便后续的代码审查与答辩现场调整，本周已在客户端目录归档了以下报告：
1.  **具身化纠错详细技术与实验调整文档**：[CORRECTIVE_FEEDBACK_DEVELOPMENT_REPORT.md](file:///mnt/e/UnityProjects/SceneTalkVR/Client/CORRECTIVE_FEEDBACK_DEVELOPMENT_REPORT.md)
2.  **组件静态挂载与编译检查报告**：[static_check_report.md (Artifact 侧)](file:///home/spring5/.gemini/antigravity-cli/brain/d81b5353-1ef3-4886-9175-772eb0ac3653/static_check_report.md)

---

## 📅 下周工作计划

1.  **多模块联调**：与 Vitor、Edwin 合作开展基于 VR 头显的端到端对话实验联调，验证 2x2 纠错在头显中的物理播报效果。
2.  **实验数据测试**：配合被试流程，在 Unity 控制台中收集 `rationaleTag` 和 `GetSessionErrorSummary()` 的统计回显情况，确保数据归档逻辑稳定。
3.  **音色与语速微调**：测试 Edwin 新增的多种 Tencent Assistant 英文音色，配合被试反馈优化纠错小助手的朗读听感。
