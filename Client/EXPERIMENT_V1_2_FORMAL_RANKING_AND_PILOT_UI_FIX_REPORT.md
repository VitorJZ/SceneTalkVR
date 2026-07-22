# Experiment v1.2 Formal Ranking 与 Pilot UI 快速修复报告

## 范围与基线

- 分支：`experiment-v1.1-integration`
- 起始 HEAD：`7fa38ebe508a2278942423720fbedd6bef47ccad`
- 本轮未修改实验设计、Assignment 算法、Feedback First、Goal evaluator、问卷题目或数据 schema。
- 最终提交：包含本报告的 `fix(ui): complete formal ranking and pilot participant flow` 提交；精确 SHA 见交付消息与 Git 历史。

## Formal Final Ranking

原问题有两层：Rehearsal UI 仍把静态的 NE/NR/SE/SR 文本命名为参与者 Ranking Panel；Collection 的 `FormalRankingVrPanel` 虽已有 Rank 按钮，但总体偏好只能隐式取 Rank 1，不能独立选择。

修复后 `FormalRankingVrPanel` 显示四个人类可读名称，为每个条件提供 Rank 1–4 和独立 Preferred 控件。提交前校验四个 rank 完整且唯一、总体偏好已选择、理由非空；成功后沿用 `EditorCollectionSessionCoordinator.SubmitFinalRanking` 保存、关闭面板并进入 Completion，且本地 `submitted` 防止重复提交。Rehearsal 的静态页面已改为明确的 operator notice，不再伪装成正式参与者 Ranking。

## Pilot 匿名 ID 与入口

主菜单 `Pilot Experiment` 现在调用 `OpenAutomaticParticipantFlow`。若当前已有 armed session，直接恢复其当前阶段；否则优先使用 `PlayerPrefs` 中持久化的最后一组 ID 恢复磁盘 assignment；没有可恢复 session 时生成：

- `PILOT-P-<yyyyMMddTHHmmssZ>-<6位随机后缀>`
- `PILOT-S-<yyyyMMddTHHmmssZ>-<6位随机后缀>`

创建成功后 ID 写入 `PlayerPrefs`，assignment 仍由原有 `PilotAssignmentAllocator.Save` 写盘，因此 Play Mode 重启后的 Resume 不重新生成或重新分配。Participant ID 前缀不参与数据资格判断。`PilotSessionSetupPanel` 和其公开 `OpenSetup` 入口仍保留给 Operator/Showcase/QA，但主菜单参与者路径不再显示该页面。

## Orb / Humanoid 生命周期与朝向

- Voice Only：仍不创建视觉实体。
- Floating Orb：条件 `Configure` 时出现并进入 Idle；`BeginFeedback` 进入 Speaking；`EndFeedback` 只结束 Speaking、保持可见；条件 Reset 才隐藏。
- Humanoid：条件 `Configure` 时实例化并进入 Idle；纠错期间 Speaking；结束后回 Idle；条件 Reset 才销毁。
- Humanoid 初始化时只计算一次面向 `Camera.main` 的水平 yaw，pitch/roll 固定为 0。当前 prefab 使用 Catalog 的显式 `spawnRotation.y = 180°` 作为 forward-axis yaw offset。
- 视觉实体仍使用 Catalog 的 `sourcePosition`（当前反馈 Agent 位于主对话 Avatar 侧面）；每次 `Configure` 先执行 `ResetSession`，保证 Orb/Humanoid/Voice Only 之间不残留。
- Formal Split Agent 的 `CorrectionFeedbackPresenter.SetPresentationActive` / `ApplyAssistantVisibility` 已经是条件期持续可见、发言时切 Speaking 的策略，本轮未重复修改；Non-Split 仍不创建额外 Agent。

## Team Showcase Pilot QA 快速操作

`SceneTalkVR/Demo/Team Showcase Control` 现在打开原窗口，而不再强制跳转 Rehearsal Control。新增真实 Pilot collection lifecycle 的快捷操作：创建/准备条件、完成当前四个 Goal、打开/自动填写/提交问卷、推进条件、TechnicalInvalid、Retry、打开/自动填写/提交 Ranking、导出、完整性审计、Reset。

这些操作直接调用 `PilotCollectionSessionCoordinator`，不会误用 Formal lifecycle。所有自动操作在既有 `PilotCollectionOperatorEvent` 中写入 `qaAutomationUsed=true`、`actor=qa_operator`；未改变 Bundle schema。标准参与者流程不依赖 Team Showcase。

## 修改文件

- `Assets/SceneTalkVR/Scripts/Runtime/FormalRankingVrPanel.cs`
- `Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
- `Assets/SceneTalkVR/Scripts/Runtime/PilotCollectionParticipantUi.cs`
- `Assets/SceneTalkVR/Scripts/Core/PilotCollectionSessionCoordinator.cs`
- `Assets/SceneTalkVR/Avatar/Scripts/PilotEmbodimentPresenter.cs`
- `Assets/SceneTalkVR/Scripts/Editor/SceneTalkTeamShowcaseWindow.cs`
- `Assets/SceneTalkVR/Tests/Editor/Stage6PilotEmbodimentTests.cs`
- `Assets/SceneTalkVR/Tests/PlayMode/PilotCollectionParticipantFlowPlayModeTests.cs`

## 最小验证结果

- Unity 版本：`6000.3.16f1`
- C# 编译：PASS，Console error = 0。
- EditMode（Stage 5 Ranking model + Stage 6 embodiment）：34/34 PASS。
- PlayMode（Pilot collection participant flow）：5/5 PASS，包含主菜单自动进入 Instructions、自动 ID、三条件完整受控流程、Pilot Ranking 与 Completion。
- 实际 Play Mode Pilot 探针：`setup=false`、`instructions=true`；生成的 participant/session ID 分别以 `PILOT-P-` / `PILOT-S-` 开头。
- 视觉生命周期探针：Orb `Idle visible → Speaking visible → Idle visible → Reset hidden`；Humanoid `Idle visible → Speaking visible → Idle visible`，Reset 后销毁；Humanoid pitch/roll 均为 0，yawOffset=180°。
- Formal 唯一排名校验沿用并通过 Stage 5 model tests；正式 UI 的 Preferred、reason、重复提交保护已接入同一提交链。
- 本轮未运行大规模矩阵、完整参与者实跑或 PICO 验收，未作相关声明。

## 工作区说明

提交仅包含上述代码、两处定向测试和本报告。既有本地资产/协议报告改动、Unity Skills 的 `Packages/manifest.json`、`packages-lock.json`、`.agents/skills/` 以及测试生成文件均未纳入提交。
