# Experiment History 与统一实验流程实施说明（2026-07-24）

## 1. 目标与范围

本次实现把 Pilot 和 Formal 串成一条可持久化、可恢复、可审计的完整实验记录，同时保留原有 Conversation History：

- 首页固定入口为 `New Experiment`、`Experiment History`、`History`、`Settings`、`Quit`。
- `New Experiment` 创建一条主实验记录并进入 Pilot/Formal 二级菜单。
- Pilot 必须先完成；完成后不可重入。Formal 仅在 Pilot 完成且产生有效偏好后解锁，完成后不可重入。
- `Experiment History` 支持分页、继续未完成实验、查看阶段/attempt/对话/问卷/排名以及删除完整实验。
- 实验对话仍出现在普通 `History` 中，但只能查看，不能单独 Continue/Delete。
- 中途退出保留历史数据；再次进入同一阶段时创建递增的新 attempt，并从当前 task/condition 的介绍页重新开始。
- Pilot 与 Formal 完成页继续使用现有完成面板，并增加 `Continue` 返回实验二级菜单。

原有 JSON、CSV、bundle 与 raw 数据继续作为研究输出；SQLite 是 UI 状态与历史聚合的权威来源，不扫描导出文件推断实验状态。

## 2. 数据模型与 schema v2

统一数据库位于：

```text
Application.persistentDataPath/SceneTalkVR/History/scenetalk_history.sqlite3
```

`SqliteLearningMemoryStore` 从 schema v1 升级到 v2。迁移在事务内执行，并保留旧 Conversation History。新增表：

| 表 | 用途 |
| --- | --- |
| `experiment_records` | 主实验 ID、参与者、总体状态、Pilot/Formal 状态、Pilot 最终偏好与时间 |
| `experiment_phases` | Pilot/Formal 唯一阶段、确定性 session ID、状态、数据目录与阶段时间 |
| `experiment_attempts` | condition/task/run、attempt 序号、运行状态、完成/中断原因 |
| `questionnaire_sessions` | 问卷会话、题目快照、完成率和原始 JSON |
| `questionnaire_responses` | 每道题原始值、计分值与回答快照 |
| `questionnaire_scores` | section mean、回答数、题目数及缺失标志 |
| `experiment_rankings` | Pilot/Formal 排名、最终偏好与原因 |

`conversation_sessions` 新增可空关联字段：

- `experiment_id`
- `experiment_phase`
- `experiment_attempt_id`
- `experiment_run_id`

旧数据库记录的这些字段保持空值，因此仍是普通对话，不会被伪造成实验记录。问卷使用 `experiment + phase + linkage + attempt` 作为记录键，避免不同 attempt 复用 linkage 时互相覆盖。

## 3. 运行时协调与状态机

`ExperimentSessionCoordinator` 是统一实验流程入口，负责：

- 创建、激活和继续主实验记录；
- Pilot/Formal 阶段门控；
- attempt 开始、完成、技术无效与挂起；
- 问卷变更和最终排名的持久化；
- Pilot 偏好到 Formal agent 形象的临时覆盖；
- Experiment History 分页、选择、详情、删除和错误导航；
- 阶段退出与实验退出的不同语义。

顶层 `SceneTalkState` 新增：

```text
ExperimentMenu
ExperimentPhase
ExperimentPhaseCompleted
ExperimentExitConfirm
ExperimentHistoryLoading
ExperimentHistoryList
ExperimentHistoryActions
ExperimentHistoryRecord
ExperimentHistoryConversationDetail
ExperimentHistoryQuestionnaireDetail
ExperimentHistoryDeleteConfirm
ExperimentHistoryError
```

UI 仅按状态渲染，不使用分散的页面显示布尔值。Pilot/Formal 内部协议状态仍由原协调器负责。

## 4. Pilot → Formal 门控与形象映射

二级菜单规则：

- Pilot：`NotStarted/InProgress/Suspended` 可进入，`Completed` 禁用。
- Formal：仅当 Pilot 为 `Completed` 且偏好有效时可进入；Formal 完成后禁用。

Pilot 排名映射：

| Pilot 偏好 | Formal assistant embodiment |
| --- | --- |
| `voice_only` | `audio_only` |
| `floating_orb` | `orb` |
| `humanoid_agent` | `humanoid` |

覆盖通过 `ExperimentConditionManager` 的运行时 override 应用，只影响需要 assistant embodiment 的 Formal 条件；离开实验后清除，不修改用户手动设置。

## 5. Exit、恢复与 attempt 语义

- 阶段内部 Exit：安全结束当前运行，未完成 attempt 记录为 `Suspended`，阶段记录为 `Suspended`，返回实验二级菜单。
- 未完成实验在二级菜单 Exit：先进入确认状态；确认后保留数据库和研究输出，再回首页。
- Pilot 与 Formal 均完成：从二级菜单退出不弹警告，也不会把完成记录错误改写为 `Suspended`。
- 再次继续：旧 attempt 保持可查看，新运行创建递增 attempt，从 task/condition 介绍页开始。
- participant Exit 不再把整个 Formal assignment 标为 `Aborted`；`Aborted` 仅保留给明确放弃或实验员操作。

## 6. Experiment History UI

- 每页 5 条，按 `updated_at_unix_ms DESC` 排序；空列表显示无记录提示。
- 记录操作页提供 Continue、View Record、Delete。
- View Record 按 Pilot/Formal 展示阶段状态、attempt、任务、偏好、排名与原因。
- 对话列表可进入完整历史对话详情。
- 问卷详情显示题目文本快照、原始回答、计分值、完成率、section mean 和缺失状态。
- 删除必须确认，且存在活动运行时拒绝删除。

普通 Conversation History 继续展示所有会话。实验会话显示所属实验/阶段标签，但 `Continue` 和 `Delete` 禁用，只能通过 Experiment History 管理。

## 7. 删除与路径安全

数据库删除在单个事务内清理：

- 实验主记录、阶段、attempt；
- 问卷 session/response/score；
- 排名；
- 关联对话与 turn。

数据库删除成功后才清理文件。文件删除必须满足：

1. 候选路径位于显式允许根目录之下；
2. 候选路径不能等于允许根目录本身；
3. 外部手动复制的导出不在允许根目录内，因此不会被删除。

允许根目录包括 Pilot collection、Formal collection、Rehearsal 与内部 History assets。删除后执行 WAL checkpoint 与 incremental vacuum 释放空间。

## 8. 主要代码位置

- `Client/Assets/SceneTalkVR/Scripts/History/ExperimentHistoryModels.cs`
- `Client/Assets/SceneTalkVR/Scripts/History/IExperimentHistoryStore.cs`
- `Client/Assets/SceneTalkVR/Scripts/History/ExperimentHistoryService.cs`
- `Client/Assets/SceneTalkVR/Scripts/History/SqliteLearningMemoryStore.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentSessionCoordinator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkState.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`

Pilot、Formal、Rehearsal、问卷与完成面板通过原协调器的事件和状态接入，不替换原有协议执行逻辑。

## 9. 验证记录

本次提交前完成：

- `dotnet build Assembly-CSharp.csproj --no-restore`：0 errors。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`：0 errors。
- `dotnet build SceneTalkVR.Stage2.PlayModeTests.csproj --no-restore`：0 errors。
- Android Development Build：Succeeded，0 errors。
- 最新 APK：`Builds/SceneTalkVR-PICO-latest.apk`。
- APK IL2CPP metadata 包含 `New Experiment`、`Experiment History` 与新版退出警告文本。
- PICO 安装包与本地 APK 的 SHA-256 完全一致：

```text
24ED27B1EBF44FE7E887D30DB8897123FED0E0D69B287B4EF4BB6049E87F6B29
```

相关 EditMode 测试覆盖：

- v1 → v2 迁移与旧对话保留；
- 分页、问卷快照/统计与级联删除；
- 同 linkage 多 attempt 隔离；
- 实验对话权限；
- 删除路径安全；
- Pilot/Formal 门控与偏好映射；
- 未完成/已完成实验的 Exit 警告规则；
- 状态机与 UI 控件结构。

相关 PlayMode 测试覆盖新首页、Pilot→Formal 门控、完成页 Continue、退出保留、resume 新 attempt、PICO device-validation 与原有 participant flow 回归。

说明：当前 Unity Editor 在最终审计时未自动导入一次性 Test Runner 触发脚本，因此本说明不把未产生结果文件的运行计为“测试通过”。发布前仍应在 Unity Test Runner 中执行 `ExperimentHistoryStoreTests`、`HistoryStateMachineTests` 及相关 PlayMode fixtures。

## 10. 本次提交明确不包含

以下工作树内容与本功能提交分离，保持未暂存：

- `SampleScene.unity`、Recovery scene；
- XR、URP、ProjectSettings；
- ExperimentProtocol、Avatar catalog、字体等资源资产；
- 本地 embedded SQLite package 与 PICO package 生成文件；
- APK 构建产物。

仓库 HEAD 已通过 Git URL 声明 `com.gilzoide.sqlite-net#1.3.2`，因此 Experiment History 提交不依赖本地 embedded package 改动。
