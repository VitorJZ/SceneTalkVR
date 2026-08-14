# 对话状态显示设置优化汇总

日期：2026-08-14

## 目标

在设置界面增加“状态显示”开关，让参与者可以显示或隐藏对话框下方的两项运行状态：

- `SubtitlePanel/CorrectionStatus`，例如 “No corrective feedback this turn.”
- `SubtitlePanel/DialogueStatus`，例如 “You can speak again.”

本功能只控制这两个状态对象的可见性，不隐藏参与者字幕、角色字幕、实际纠错内容、任务目标或发言按钮，也不改变对话面板的位置和尺寸。

## 现有机制与接入方式

项目已经通过 `SceneTalkState.Settings` 管理设置界面的进入和退出，并使用 `SceneTalkUserSettingsStore` 统一保存用户设置。因此本次没有增加平行的流程状态或独立设置系统，而是复用现有链路：

```text
Settings 状态中的按钮点击
→ SceneTalkUserSettingsStore.SetHideDialogueStatuses
→ PlayerPrefs 持久化并触发 Changed
→ SceneTalkFlowUiController.OnUserSettingsChanged
→ ApplyUserSettings + Refresh
→ 更新 CorrectionStatus / DialogueStatus 可见性
```

正式实验和预实验共用这一套设置状态与 UI 控制器，因此无需分别维护两份状态。

## 实现内容

### 设置模型与持久化

`SceneTalkUserSettings` 新增 `hideDialogueStatuses`：

- 默认值为 `false`，即默认显示状态。
- `Clone` 会保留该值。
- `SetHideDialogueStatuses` 使用现有“克隆、规范化、保存、通知”流程更新设置。
- 设置继续保存在 `SceneTalkVR.UserSettings.v1`，无需迁移 PlayerPrefs 键。
- 旧版本 JSON 不含该字段时，Unity JSON 反序列化会得到默认值 `false`，保持旧行为。
- `ResetAll` 会恢复默认显示。

### 设置界面

设置页新增以下运行时 UI：

- `StatusDisplayLabel`：中文“状态显示”，英文“Status display”。
- `StatusDisplayValue`：显示“显示/隐藏”或 “Shown/Hidden”。
- `StatusDisplayChangeButton`：沿用现有“切换/Switch”按钮行为。

新增行位于“对话字幕”下方。后续纠错来源、辅助角色外观、纠错方式和数据通道依次下移，仍保持在现有设置面板范围内。

### 对话面板可见性

刷新对话界面时分别读取：

- `hideDialogueSubtitles`：只控制字幕文本容器。
- `hideDialogueStatuses`：只控制 `CorrectionStatus` 和 `DialogueStatus`。

两个设置彼此独立。即使状态被隐藏，内部状态文本仍按现有流程更新；重新开启时会直接显示最新状态，不会影响对话状态机、纠错判断或下一轮录音。

### 双语文案

在 `SceneTalkUiText` 中增加“状态显示”到 “Status display” 的静态映射。状态值使用现有双语选择方法生成，因此切换系统语言后不需要重建设置数据。

## 修改文件

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkUserSettings.cs`
  - 新增设置字段、克隆支持和统一设置 API。
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
  - 创建设置行、绑定按钮、刷新双语状态值，并控制两个状态对象的显示。
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkUiText.cs`
  - 增加中英文静态文案映射。
- `Client/Assets/SceneTalkVR/Tests/Editor/LanguageSystemTests.cs`
  - 覆盖默认值、旧配置兼容、重置、持久化和 `Changed` 事件。
- `Client/Assets/SceneTalkVR/Tests/PlayMode/EditorCollectionParticipantFlowPlayModeTests.cs`
  - 覆盖正式实验中的状态开关行为。
- `Client/Assets/SceneTalkVR/Tests/PlayMode/PilotCollectionParticipantFlowPlayModeTests.cs`
  - 覆盖预实验设置页双语显示、状态开关及布局不变性。

## 行为矩阵

| 对话字幕 | 状态显示 | 字幕文本 | CorrectionStatus / DialogueStatus |
|---|---|---|---|
| 显示 | 显示 | 显示 | 显示 |
| 隐藏 | 显示 | 隐藏 | 显示 |
| 显示 | 隐藏 | 显示 | 隐藏 |
| 隐藏 | 隐藏 | 隐藏 | 隐藏 |

所有组合均保留 `CorrectionFeedback`、`DialogueListenButton` 和面板布局的原有行为。

## 审查与验证

代码审查结论：未发现阻断性问题。设置写入、事件通知、界面刷新和正式/预实验入口均复用现有机制，没有新增场景依赖或流程分支。

已完成验证：

- 完整解决方案编译：0 错误。
- 目标测试项目编译：0 警告、0 错误。
- EditMode：5/5 通过。
- PlayMode：3/3 通过。
- PlayMode 覆盖中英文切换、四种字幕/状态组合、正式实验、预实验，以及面板和发言按钮布局保持不变。
- `git diff --check`：通过。

测试过程中动态写入的 TMP 字体图集和 `EditorSettings.asset` PlayMode 选项已经清理，不纳入提交。

## 本次提交边界

本次提交只包含状态显示设置、对应测试及本文档。以下现有未提交内容明确保留在工作区，不纳入本次提交：

- `SceneTalkVR Task Goal UI` 的 Y Rotation 从 `-60.255` 调整为 `0`，以及相应布局测试修改。
- 预实验任务临时统一、任务分配断言及相关测试修改。
- `ExperimentBuildInfo.asset` 和 `SampleScene.unity` 等与状态显示无关的修改。

本次仅创建本地 Git 提交，不推送远端。
