# 问卷跳过机制与当前未提交改动汇总（2026-07-28）

## 1. 本轮目标

本轮在不破坏现有实验状态机、问卷提交规则和历史数据兼容性的前提下，为正式实验与 Pilot 条件问卷增加“跳过”终态。问卷是否填写完成不再阻断条件流转，但最终排序和访谈仍沿用原有校验规则。

同时，本次提交纳入并复核了工作区中已有的场景相机、XR 预加载资源和构建信息修改。

## 2. 状态机与流程

### 2.1 兼容性处理

- `QuestionnaireCompletionStatus` 在末尾追加 `Skipped`。
- `ConditionRunStatus` 在末尾追加 `QuestionnaireSkipped`。
- `PilotRunStatus` 在末尾追加 `PilotQuestionnaireSkipped`。
- `StudyEventType` 在末尾追加 `QuestionnaireSkipped`。

所有新增枚举值均追加在原有成员之后，既有序列化数值不变，不需要数据库迁移。

### 2.2 正式实验

正式条件问卷保持现有状态机入口，新增跳过分支：

`QuestionnaireInProgress → QuestionnaireSkipped → Completed`

提交分支仍为：

`QuestionnaireInProgress → QuestionnaireSubmitted → Completed`

`ExperimentLifecycleCoordinator` 保留原提交 API 和提交事件，并增加带 `QuestionnaireCompletionStatus` 结果的统一完成事件。采集与彩排协调器通过统一事件完成 attempt、持久化 assignment、清理当前条件边界，并进入下一条件选择或最终排序。

### 2.3 Pilot、彩排与设备验证

Pilot 新增状态迁移：

`PilotQuestionnaireInProgress → PilotQuestionnaireSkipped → Completed`

`PilotWorkflowCoordinator`、`PilotCollectionSessionCoordinator`、`RehearsalSessionCoordinator` 和编辑器演示协调器均接入跳过 API。因此正常 Pilot 采集、彩排和 PICO 设备验证使用同一套流程，不建立旁路状态。

跳过后的 attempt 仍记为正常完成，完成原因为 `questionnaire_skipped`，技术有效性保持 `Valid`；流程继续进入下一条件或最终排序。

## 3. 问卷服务与数据记录

`QuestionnaireSessionService` 新增 `CanSkip` 和 `Skip`：

- 不执行必答题完整性校验。
- 仍校验问卷会话、当前状态、技术有效性、`conditionRunId` 和 `questionnaireLinkageKey`。
- 已提交或已跳过的问卷不能再次跳过，也不能继续修改答案。
- 跳过时间记录在 `skippedAtUtc`，完成原因记录为 `participant_skipped`。
- 已填写答案保留并标记为 `Skipped`；未回答题目不生成伪造响应。
- 提交继续记录 `submittedAtUtc`，并执行原有必答题校验。

新增 `QuestionnaireCompletionRecord` 和 `*_questionnaire_events_v1.jsonl`。提交与跳过都会写入一个独立终态记录，包含终态类型、时间、关联键、条件运行 ID、完成率、已答题数、总题数、缺失状态、技术有效性和运行上下文。原响应 JSONL/CSV 格式及 Bundle schema 版本保持不变。

## 4. 完整性审计与历史兼容

`SessionDataIntegrityAuditor` 现在要求每个正常完成的条件具有且仅具有一个问卷终态：

- `QuestionnaireSubmitted`；或
- `QuestionnaireSkipped`。

审计规则包括：

- 提交问卷必须存在答题记录。
- 跳过问卷允许零答题，部分答案仍按现有数值规则校验。
- 同时出现提交与跳过、重复终态、缺少关联键或缺少终态均判定失败。
- 旧数据没有独立完成记录时，仍可通过响应行中的 `Submitted` 状态识别原提交终态。
- Pilot 的 `PilotQuestionnaireSubmitted` 与 `PilotQuestionnaireSkipped` 会映射到统一审计事件。

实验历史继续复用现有 `questionnaire_sessions` 记录结构。`QuestionnaireSession` 的新增字段随原 JSON 一同保存，`Skipped` 状态和部分答案可直接往返读取，无数据库迁移。

## 5. 界面行为

### 5.1 正式问卷

- 最后一页增加“跳过”按钮，位于“提交”按钮左侧。
- 第一次点击显示“跳过将保留已填写内容并继续实验”，按钮变为“确认跳过”。
- 第二次点击才执行跳过。
- 翻页、修改答案、切换问卷或点击提交会取消待确认状态。
- `Submitted` 和 `Skipped` 都会关闭问卷面板。

### 5.2 Pilot 问卷

- 底部改为左侧“跳过”、右侧“提交”的并排按钮。
- 跳过同样需要二次点击确认，并在修改答案、提交或离开当前问卷阶段时取消确认状态。
- 提交仍要求完成必答题，跳过不受必答题缺失影响。

中文状态显示新增“已跳过”，并补充“已经跳过”和“当前问卷不能跳过”的错误提示。

## 6. 测试覆盖

本轮新增或扩展的自动化测试覆盖：

- 空问卷跳过、部分填写后跳过、重复跳过和跳过后禁止编辑。
- 正式生命周期跳过后条件完成且技术状态保持有效。
- 正式问卷跳过按钮位置、二次确认、面板关闭和返回条件选择。
- Pilot 正常采集与 PICO 设备验证中的提交/跳过混合流程，最后进入最终排序。
- `Skipped` 状态、时间、完成原因和部分答案的 SQLite 历史往返。
- 审计接受零答题跳过，拒绝无答题提交、双终态和空关联键。

提交前验证结果：

- `Assembly-CSharp.csproj` 编译通过。
- `Assembly-CSharp-Editor.csproj` 编译通过。
- `SceneTalkVR.Stage2.PlayModeTests.csproj` 编译通过。
- `git diff --check` 通过。

Unity Editor 当时正在占用项目，Unity MCP 不可用，因此没有在本轮通过 Unity Test Runner 实际执行 EditMode/PlayMode 测试；也未使用 Windows 图形界面自动化。

## 7. 一并纳入的现有配置修改

以下三项修改在本轮开始前已存在于工作区，经复核后一并提交：

- `SampleScene.unity`：主相机视场角由 `77.5°` 调整为 `100°`。
- `ProjectSettings.asset`：将 XR General Settings、OpenXR Package Settings 和 PXR Settings 加入 `preloadedAssets`，保证 XR/PICO 运行时设置随播放器预加载。
- `ExperimentBuildInfo.asset`：将构建基线更新到提交 `810cf5d09483c189754cfcac6756c3f8d965f877`，并同步构建时间。该值表示本轮未提交改动开始时的代码基线，不是当前提交自身的哈希。

## 8. 审查结论

当前改动保持了既有状态机边界、枚举持久化兼容性和 Bundle schema，未发现阻断提交的问题。跳过仅放宽条件问卷的必答限制，不会将跳过标记为技术失败，也不会放宽最终排序与访谈的原有规则。
