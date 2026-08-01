# 未完成实验对话恢复选择机制实现汇总

## 背景与问题

实验被中断后，原流程只能按条件状态继续，无法让参与者明确选择是否沿用旧对话。正式实验可能重新进入任务但没有恢复原对话上下文；Pilot Collection 还会把未完成条件转为技术无效并创建重试。这样会造成历史对话、任务目标进度、条件运行记录和当前 attempt 之间的语义不一致。

本次实现为未完成的正式实验和 Pilot Collection 增加统一恢复选择：

- **继续旧对话**：恢复原会话、完整历史回合、LLM 上下文、原 run/attempt、场景与 Avatar，并保留任务目标进度。
- **开启新对话**：保留旧会话和已挂起的 attempt，创建新的 run、attempt 和会话，按原实验条件重新开始当前任务，并重置该任务目标进度。

## 状态机设计

在 `SceneTalkState` 末尾追加 `ExperimentConversationResumeChoice`，避免改变既有枚举项的序号。恢复未完成实验时，运行时先加载正式或 Pilot 条件及目标快照，但不会立即启动场景对话；随后由 `ExperimentSessionCoordinator` 判断是否存在待处理的运行中条件：

```text
恢复实验记录
  -> 恢复条件与目标快照（不启动对话）
  -> 查找并校验可恢复的历史对话
  -> ExperimentConversationResumeChoice
       -> 继续旧对话 -> TurnReview / 原对话继续
       -> 开启新对话 -> 新 run / 新 attempt / 新对话
```

不存在待恢复对话条件时，仍按原状态进入条件选择、问卷、最终排序或完成页面。

## 历史记录与关联校验

恢复候选最多展示最近五条，并要求以下关联同时成立：

- `experimentId` 与当前实验一致。
- `experimentKind` 与正式实验或 Pilot 类型一致。
- `experimentRunId` 与当前未完成条件的 run ID 一致。
- `taskType` 或 `scenarioId` 与当前任务一致。
- `experimentAttemptId` 必须对应当前实验内同 run、同任务且状态为 `Running` 或 `Suspended` 的 attempt。
- 加载完整会话后，再校验会话 summary 与 settings 中的实验、类型、run、attempt 和任务关联。
- 恢复运行时还会校验保存的任务、反馈 provider/style 与当前实验条件一致。

`ExperimentHistoryService.ResumeAttempt` 仅允许恢复严格匹配的挂起或运行中 attempt，并重新建立 `CurrentConversationLink`。异步恢复失败时，attempt 会重新标记为 `Suspended`，界面返回恢复选择状态，不会静默创建错误关联。

## 正式实验流程

- `BeginParticipantFlow` 恢复当前条件和目标序列，但延迟 `LoadAssignedTask`。
- 选择继续旧对话后，恢复原 `LearningSession`、历史回合、最后用户文本、LLM 对话上下文、场景状态、Avatar 会话和原条件 run。
- 选择开启新对话后，原 attempt 保持挂起；当前条件创建新的 condition run 和 attempt，目标追踪器按任务定义重置，再加载相同任务。
- 操作日志分别写入 `FormalConversationResumed` 或 `FormalConversationRestarted`。

## Pilot Collection 流程

- 恢复时不再自动把未完成条件改为 `TechnicalInvalid`。
- `PilotWorkflowCoordinator.Resume` 恢复原 Pilot run、呈现配置、任务分配和目标序列。
- 继续旧对话时恢复原会话和目标进度；开启新对话时创建新的 Pilot run、attempt 和会话，并重置任务目标。
- 操作日志分别写入 `PilotConversationResumed` 或 `PilotConversationRestarted`。
- Pilot 问卷进行中时，优先从当前 Collection 数据目录恢复草稿，并兼容旧的 `ExperimentLogs` 草稿位置。

## 中文界面行为

新增运行时中文面板“继续场景对话”，包含：

- 当前任务名称。
- 最多五条合法历史对话，显示更新时间、回合数和任务名称。
- 当前选择高亮。
- “继续所选对话”按钮。
- “开启新对话”按钮。
- 无安全候选或恢复失败时的中文提示。

界面文案明确说明：继续旧对话会保留上下文和任务进度，开启新对话会重置当前任务进度。

## 主要修改文件

### 状态、运行时与界面

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkState.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`

### 正式实验、Pilot 与问卷协调

- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentSessionCoordinator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/EditorCollectionSessionCoordinator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentStudyLifecycle.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/PilotCollectionSessionCoordinator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/PilotWorkflowCoordinator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/QuestionnairePipeline.cs`

### 历史服务与测试

- `Client/Assets/SceneTalkVR/Scripts/History/ExperimentHistoryService.cs`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/ExperimentHistoryStoreTests.cs`
- `Client/Assets/SceneTalkVR/Tests/PlayMode/EditorCollectionParticipantFlowPlayModeTests.cs`
- `Client/Assets/SceneTalkVR/Tests/PlayMode/PilotCollectionParticipantFlowPlayModeTests.cs`

## 验证结果

- `Assembly-CSharp.csproj`：编译通过，0 错误。
- `Assembly-CSharp-Editor.csproj`：编译通过，0 错误。
- `SceneTalkVR.Stage2.PlayModeTests.csproj`：编译通过，0 错误。
- 实验历史 EditMode：12/12 通过。
- 正式与 Pilot 完整参与者流程 PlayMode：28/28 通过。
- 增强恢复用例：2/2 通过，覆盖两条历史回合、最后用户文本、原 run/attempt、目标进度和新会话目标重置。
- Unity Console：0 错误。
- `git diff --check`：通过。

## 范围与兼容说明

- 本次覆盖正式 Collection 与 Pilot Collection，包括其 PICO Collection 运行路径。
- Rehearsal 和 PICO 设备验证流程未扩展到该对话选择机制，继续保持原有行为。
- 已完成的实验、问卷终态、最终排序和既有普通历史继续功能不改变。
- `ExperimentBuildInfo.asset` 是审计时已存在的未提交构建信息更新，本次按“提交全部未提交改动”的要求保留；它不改变恢复机制业务逻辑。
- Unity 测试运行造成的字体动态缓存状态和 `ProjectSettings.asset` 自动写回不属于功能改动，已从提交范围排除。
