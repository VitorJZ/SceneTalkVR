# SceneTalkVR 系统界面中文化实施汇总

日期：2026-07-27

基线提交：`d5b10af 修复对话语音中断与不完整播放`

## 1. 工作目标

本次工作将 SceneTalkVR 面向参与者、实验人员和编辑器演示人员的系统界面与按钮文案统一改为中文，同时保持英语口语练习内容、内部协议字段、状态机和实验数据结构不变。

中文化不是业务流程重构。所有界面仍由项目现有状态机和协调器决定显示、隐藏、可交互与跳转关系，展示层只根据当前状态输出中文文案。

## 2. 中文化范围

### 2.1 主流程与通用界面

- 主菜单：预实验、正式实验、实验历史、对话历史、设置和退出。
- 场景与角色需求：录音、结束、重试、确认、识别结果及错误提示。
- 对话过程：录音、识别、思考、纠错反馈播放、角色发言和就绪状态。
- 对话字幕：参与者与角色前缀改为“你”和“角色”。
- 通用导航：返回、继续、上一页、下一页、删除、取消、退出等。
- 加载和恢复：场景、角色、对话历史、实验历史及上下文恢复状态。
- 任务显示：任务名称、任务情境、沟通目标和目标完成进度。

### 2.2 设置与纠错界面

- 字体大小、界面大小、字幕显示、纠错来源、纠错方式和辅助角色外观。
- 可切换、已锁定、不适用等设置状态。
- 对话角色、辅助角色、直接纠错、重述反馈、仅语音、悬浮球和第三人称角色等展示名称。
- 正式实验或条件顺序锁定时的说明文字。

### 2.3 历史记录

- 对话历史列表、详情、删除确认、错误页和分页信息。
- 实验历史列表、实验记录、对话与问卷详情、尝试记录及最终排序。
- 实验类型、实验状态、尝试状态、问卷状态和常见枚举值的中文显示。
- 对话轮次、纠错次数、创建时间、更新时间、环境和角色信息。

### 2.4 预实验、正式实验与问卷

- 预实验会话设置、外观选择、任务介绍、问卷、最终排序和完成页。
- 正式实验反馈模式选择、状态提示、最终排序和完成页。
- 问卷标题、题目、量表说明、分页、提交确认及校验提示。
- PICO 设备验证和编辑器演示的操作员提示。

### 2.5 场景序列化文本

`SampleScene.unity` 中已经序列化的 TextMeshPro 可见文案与运行时生成文案同步中文化，避免首次加载场景时短暂显示旧英文，或因运行时对象未重建而保留英文。

## 3. 文案设计与代码结构

新增 `SceneTalkChineseUiText` 作为运行时统一中文展示映射，集中处理以下内容：

- 任务编号到中文任务名称、情境和目标的映射。
- 实验类型、实验状态、尝试状态和问卷状态的中文名称。
- 常见场景、外观、语速、态度、错误类型等值的中文名称。
- 业务错误码和已有英文错误消息到用户可读中文提示的映射。
- 纠错状态到中文状态提示的映射。

该类只返回展示文本，不修改业务对象，也不将中文文案回写到任务、历史、问卷或实验记录中。内部对象名称、资源键、任务编号、状态枚举、数据库字段和协议值继续使用原有值。

## 4. 状态机兼容方式

本次保留项目原有状态机职责边界：

1. `SceneTalkOrchestrator`、实验协调器、预实验协调器和问卷控制器继续产生状态与错误。
2. 各 UI 控制器继续按现有状态决定面板激活、按钮可用性和跳转行为。
3. UI 刷新阶段使用 `SceneTalkChineseUiText` 或本地中文常量生成展示文案。
4. NE、NR、SE、SR 等正式实验条件编码保持不变，仅补充中文解释。

因此，中文化不会影响状态迁移、会话恢复、条件顺序、采集资格、问卷提交或最终排序逻辑。

## 5. 英语练习内容保留原则

系统界面使用中文，但以下内容刻意保留英文：

- 参与者语音识别得到的英语转写。
- LLM 生成的角色英语回复。
- 对话历史中的原始参与者与角色发言。
- 任务要求中用于开始英语练习的英语开场白。
- 纠错记录中的英语原句、修改后句子和反馈正文。

这样可以避免界面中文化误改实验材料或英语练习内容。

## 6. 涉及文件

### 6.1 运行时代码

- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkChineseUiText.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/QuestionnaireVrPanel.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/FormalRankingVrPanel.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/PilotCollectionParticipantUi.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs`

### 6.2 场景与测试

- `Client/Assets/Scenes/SampleScene.unity`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/ExperimentConditionRuntimeSwitchTests.cs`
- `Client/Assets/SceneTalkVR/Tests/PlayMode/EditorCollectionParticipantFlowPlayModeTests.cs`
- `Client/Assets/SceneTalkVR/Tests/PlayMode/PilotCollectionParticipantFlowPlayModeTests.cs`

测试断言已同步为中文按钮、设置值和锁定提示，避免本次展示层变更导致旧英文断言失效。

## 7. 审查与验证

已完成以下检查：

- 逐项审查运行时代码、测试和 `SampleScene.unity` 的未提交差异。
- 检查场景中带拉丁字母的 `m_text`，仅保留品牌名 `SceneTalkVR` 和实验条件编码 NE、NR、SE、SR。
- `dotnet build Client/Assembly-CSharp.csproj --no-restore`：通过，0 个错误。
- `dotnet build Client/Assembly-CSharp-Editor.csproj --no-restore`：通过，0 个错误。
- `git diff --check`：通过。

构建只出现项目原有警告。本次环境没有可用的 Unity MCP 资源，因此未能从 Unity Test Runner 执行 EditMode/PlayMode 测试，也未进行编辑器或设备截图验证。提交前已再次执行程序集构建和 Git 差异校验，结果保持通过。

## 8. 本次提交明确排除的既有改动

工作区中以下改动在本次中文化工作开始前已经存在，或属于 Unity/用户生成内容，不纳入本次提交：

- `Client/Assets/SceneTalkVR/ExperimentProtocol/ExperimentBuildInfo.asset`
- `Client/Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset`
- `Client/Assets/SceneTalkVR/Fonts/NotoSansSC-VF SDF.asset`
- `Client/ProjectSettings/ProjectSettings.asset`

其中字体资产的纹理数据从约 4 MiB 缩减为 1 字节，属于高风险异常变化，必须继续保留在工作区但不得混入中文化提交。

Git 状态还显示若干只有时间戳或状态缓存变化、实际内容与索引一致的文件；这些文件同样不暂存。提交将使用明确文件列表，不使用 `git add -A`。

## 9. 后续建议

- 在 Unity 编辑器中运行相关 EditMode 和 PlayMode 测试。
- 在 PICO 真机检查中文字体字形、自动换行、按钮宽度和远距离可读性。
- 覆盖预实验、正式实验、历史恢复、问卷和最终排序的完整状态迁移。
- 单独排查并恢复 `NotoSansSC-VF SDF.asset` 的异常纹理数据，避免中文字符在设备上缺失。
