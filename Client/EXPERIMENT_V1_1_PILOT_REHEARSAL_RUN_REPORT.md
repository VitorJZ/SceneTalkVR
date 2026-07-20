# Experiment v1.1 Pilot Rehearsal Run Report

## Result

`BLOCKED_EXTERNAL_SERVICE` — Pilot 的共享 allocator/workflow、三种 presentation 配置和自动回归已通过；真实 Voice Gateway `127.0.0.1:8787` 不可达，未声称完成真实三条件语音会话或真实 Bundle audit。

## Verified in the open Unity Editor

- Pilot Assignment 为三个 embodiment、三个餐厅任务各一次。
- Pilot style 固定 Explicit；Voice Only 为 `NonSpatialHeadLocked` 且无视觉实体。
- Floating Orb 使用 `generated_orb_v1`；Humanoid 使用 `teacher_female_humanoid_v1`，不允许回退为 Orb。
- 三条件共享 `rehearsal_feedback_voice`，运行时解析到 Tencent voiceId `101050`。
- `PilotWorkflowCoordinator` 的 Prepare、Questionnaire、TechnicalInvalid、Retry、Reset 和 ranking 路径通过项目 PlayMode 回归。

## Not yet verified manually

- 真实 Voice Only/Orb/Humanoid 播放、动画与空间听感。
- 三次实际问卷与最终排序。
- 同一反馈输入的真实 feedback hash、语音参数与无跨条件泄漏。
- 真实 Bundle checksum/integrity PASS。
- 要求的 Voice Only/Orb/Humanoid/ranking 截图。

必须先启动电脑端真实 STT/TTS/LLM 服务，再按 Operator Guide 完成标准运行；QA 工具只能用于后续重复回归。
