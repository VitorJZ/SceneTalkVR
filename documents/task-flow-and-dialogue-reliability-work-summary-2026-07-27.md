# 任务顺序机制与对话可靠性工作汇总

日期：2026-07-27

适用项目：SceneTalkVR

协议版本：`1.4.0-participant-turn-gated-goals`

构建标识：`editor-collection-20260727`

## 1. 工作背景

本轮工作解决了两类直接影响实验有效性和稳定性的问题：

1. 参与者可以在一次长录音中同时说出多个任务目标，旧机制会让多个目标同时参与判断，无法保证预期的多轮交互。
2. LLM 瞬态重试、Pilot Agent 生命周期和 Formal 对话文本守卫存在回归问题，分别表现为测试中的 Unity `Scripting object is not properly attached`、开始新一轮时 Agent 被隐藏，以及普通否定句被误判为纠错内容泄漏。

本次实现沿用项目现有的 `GoalProgressTracker`、实验生命周期协调器、Orchestrator 和协议资产体系，没有建立第二套并行任务逻辑。

## 2. 顺序任务状态机

### 2.1 状态与主流程

目标顺序由 `GoalProgressTracker` 统一管理，正式状态流为：

```text
ActiveGoal
  -> AwaitingParticipantTurn
  -> AwaitingAvatarReply
  -> ActiveGoal（下一目标）
```

最后一个目标完成证据判断后，不再要求额外的参与者发言，而是等待该证据轮对应的 Avatar 回复结束，再进入 `Completed`。没有目标时使用 `Inactive`。

状态机同时记录：

- 当前目标索引和目标 ID；
- 状态机修订号；
- 正在等待完成的对话 `turnId`；
- 已观察的参与者轮次和已完成的 Avatar 对话轮次；
- 顺序策略 `SequentialAfterParticipantTurnAndAvatarReply`。

### 2.2 显示与判断规则

- 初始只激活并显示第一个目标。
- 已确认目标继续保留在任务面板，并显示完成标记。
- 尚未解锁的未来目标不显示，也不参与确定性或结构化 LLM 判断。
- 评估器每次只接收当前激活目标；单次录音即使包含所有答案，也最多确认当前目标。
- 当前目标确认后仍保留为状态机的当前记录，下一目标不会立即出现。

### 2.3 “至少再发言一轮”的落实方式

中间目标确认后，必须满足以下两个条件才会解锁下一目标：

1. 参与者提交一轮不同于目标证据轮的新发言；
2. 相同 `turnId` 的 Avatar 回复完整结束。

仅完成目标证据轮的 Avatar 回复不能解锁下一目标；隐藏目标也不会使用这轮新发言作为证据。下一目标显示后，参与者需要再次作答，目标才会参与判断。

`SceneTalkOrchestrator` 在 ASR 最终文本进入处理时通知“参与者轮次已提交”，在 Avatar 对话轮进入 Review 时通知“对话轮已完成”。状态机使用同一个 `turnId` 关联两者，防止无关回复、过期回复或错误轮次解锁目标。

若某轮 Avatar 生成失败，参与者之后可以重新发言；新的有效轮次会替换待完成轮次，失败轮不会误解锁下一目标。

### 2.4 完成、撤销和恢复

- `OnAllGoalsConfirmed` 只在顺序状态真正进入 `Completed` 后触发，不会在最后一个目标刚被确认时提前触发问卷。
- 撤销某个已完成目标时，会回滚该目标及其后的目标记录，并把被撤销目标重新设为当前目标，避免绕过顺序。
- 目标快照升级到 `3.0`，同时保存目标记录和顺序状态。
- 恢复时若快照停在 `AwaitingAvatarReply`，由于进程重启后原播放不能继续，系统会安全回退到 `AwaitingParticipantTurn`，要求新的参与者轮次。
- 旧记录和 `2.0` 快照仍可读取；缺少新顺序信息时，会根据已确认目标推导安全状态。

## 3. UI、协议与审计同步

任务面板现在只渲染“已完成目标 + 当前目标”，并显示以下等待提示：

- 目标完成后等待参与者再次发言；
- 等待 Avatar 回复结束；
- 全部目标完成。

Formal、Pilot、Editor Demo、Rehearsal 和 QA 自动流程均改为使用同一顺序状态机。Demo/Rehearsal/QA 的自动完成逻辑会显式模拟必要的轮次事件，不再直接遍历并确认所有目标。

实验事件和 Pilot 事件增加了顺序策略、顺序状态、当前目标索引、修订号和解锁轮次等字段，并记录目标激活、等待参与者、等待 Avatar、顺序推进和顺序完成事件。

正式协议资产已同步为：

- 协议：`1.4.0-participant-turn-gated-goals`；
- 构建：`editor-collection-20260727`；
- Formal 最大轮次：9；
- Pilot 最大轮次：8；
- Formal/Pilot 最大时长仍分别为 10/8 分钟。

### 最大轮次的实际行为

最大轮次用于标记实验轮次预算，而不是替代目标完成条件。Formal 达到上限且目标未完成时，会记录 `TaskLimitReachedWithoutCompletion` 并发出通知，但不会自动确认目标，也不会把条件伪装成正常完成；当前状态仍需由既有实验流程处理。Pilot 同样保存了 8 轮配置和上限检查能力，当前代码没有让上限自动确认目标或自动解锁隐藏目标。

## 4. 对话可靠性与 Agent 回归修复

### 4.1 LLM 瞬态重试测试

`RealLLMService` 将重试循环拆为不依赖 Unity `Scripting Object` 生命周期的静态核心，并注入重试日志和延时函数：

- 生产路径仍保留 Dialogue/正式纠错请求的瞬态重试策略；
- 429、502 等重试行为和预算计算保持不变；
- 纯重试单元测试不再创建或销毁 `GameObject`，也不再等待真实的 1～5 秒退避；
- 消除了测试结束后异步日志访问失效 Unity 对象导致的 `Scripting object is not properly attached`。

### 4.2 Floating Orb / Humanoid 生命周期

当 `PilotEmbodimentPresenter` 已持有有效 Profile 时，Pilot condition 对共享 Agent 的可见性拥有控制权。通用的 `CorrectionFeedbackPresenter` 不再在开始新 turn 或流式清理时覆盖该可见性，因此 Floating Orb 和 Humanoid Agent 会保留到真正的 condition/session reset。

### 4.3 Formal correction leakage 误报

`CorrectionTextGuards` 原先把过宽的 `"not "` 当作纠错泄漏特征，导致以下正常角色对话被拒绝：

> You're welcome to take photos inside the museum, but flash photography is not allowed to protect the exhibits.

现已移除该通用否定模式，保留 `you should say`、`try saying`、`grammar`、`instead of` 等明确纠错特征。普通的 `not allowed`、`not included` 和 `not available` 可以作为合法 Avatar 对话，同时显式纠错文本仍会被拦截。

## 5. 主要代码范围

任务机制与接线：

- `GoalProgressTracker.cs`
- `GoalAchievementEvaluator.cs`
- `SceneTalkOrchestrator.cs`
- `ExperimentStudyLifecycle.cs`
- `SceneTalkFlowUiController.cs`
- Formal/Pilot/Demo/Rehearsal/QA 各协调器

协议与持久化：

- `ExperimentV11ProtocolConfig.cs`
- `EditorCollectionAssetBuilder.cs`
- 正式、Demo、Rehearsal 协议资产和 `ExperimentBuildInfo.asset`

回归修复：

- `RealLLMService.cs`
- `CorrectionFeedbackPresenter.cs`
- `CorrectionTextGuards.cs`

测试：

- 新增 `SequentialGoalStateMachineTests.cs`
- 更新目标敏感度、Formal/Pilot 流程、LLM 可靠性、纠错守卫及 PlayMode 测试

## 6. 验证结果

所有 Unity 自动化测试均在隔离副本 `tmp/SequentialGoalTestProject` 中通过，测试产物位于 Git 忽略的 `tmp` 目录，不纳入提交。

- 最新全量 EditMode：449/449 通过；
- 全量 PlayMode：48/48 通过；
- correction leakage 定向 EditMode：34/34 通过；
- 502 重试与 Floating Orb/Humanoid 回归集合：4/4 通过；
- `Assembly-CSharp.csproj`、`Assembly-CSharp-Editor.csproj` 和 `SceneTalkVR.Stage2.PlayModeTests.csproj` 编译通过，0 error；
- `git diff --check` 通过，仅显示仓库行尾策略产生的 LF/CRLF 转换提示。

覆盖的关键场景包括：

- 单次录音包含全部答案时只确认当前目标；
- 新的参与者发言和匹配 Avatar 回复共同解锁下一目标；
- 证据轮、错误轮次和失败 Avatar 回复不能误解锁；
- 最终 Avatar 回复结束前不启动问卷；
- 快照恢复和旧数据迁移；
- 429/502 重试无未处理 Unity 日志；
- Floating Orb/Humanoid 在 turn 清理后保持可见；
- 普通否定句不再触发 correction leakage。

## 7. 提交审查结论

- 已审查当前全部未提交业务代码、协议资产和测试，没有发现冲突标记、临时调试文件或凭据内容。
- API Key 仍为空或从环境变量读取，没有把本地密钥纳入版本控制。
- `tmp` 中的隔离测试工程和结果文件未进入 Git 状态。
- 本次工作只创建本地提交，不推送到远端。
