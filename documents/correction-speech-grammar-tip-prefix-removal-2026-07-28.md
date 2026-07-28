# 纠错语音固定 Grammar Tip 前缀移除实施汇总

日期：2026-07-28

基线提交：`c747b7a 中文化系统界面与交互文案`

## 1. 工作目标

研究 SceneTalkVR 当前纠错机制，移除显式纠错语音中固定朗读的 `Grammar tip:` / `Grammar tips:` 开头，使语音直接从实际纠错规则开始，同时保持纠错判定、实验条件、状态机和“先纠错、后角色回复”的播放顺序不变。

示例：

- 修改前：`Grammar tip: Use "am" with "I". Try: "I am ready."`
- 修改后：`Use "am" with "I". Try: "I am ready."`

## 2. 现有纠错机制

当前纠错流程由以下环节组成：

1. `RealLLMService` 通过独立纠错请求分析本轮 ASR 文本，生成 `CorrectionFeedbackData`。
2. `FinalizeCorrectionFeedback` 根据当前实验条件固定 `provider` 与 `style`，执行非语音差异过滤、重述纯度检查和缺失语音文本修复。
3. `SceneTalkOrchestrator` 调用 `PrepareCorrectionReview`，有纠错时进入 `SceneTalkState.CorrectionFeedbackSpeaking`，无纠错时直接进入 `DialogueSpeaking`。
4. `AvatarPresentationVoiceModule` 维持反馈优先门控，先调用 `CorrectionFeedbackPresenter` 播放纠错，再开放角色回复语音。
5. `CorrectionFeedbackPresenter.ResolveFeedbackText` 为显式纠错选择 `feedbackText`，随后通过 `AvatarSpeechPlayer` 和语音网关送入 TTS。

本次没有改动第 3、4 步的状态迁移和门控逻辑。

## 3. 根因

固定开头来自三个相互叠加的原因：

- 独立纠错提示词强制显式反馈使用 `Grammar tip: [rule]. Try: ...` 格式。
- 兼容的联合对话提示词仍为辅助角色保留相同固定格式。
- 本地显式纠错兜底文本也硬编码 `Grammar tip:`。

生成结果进入 `CorrectionFeedbackPresenter` 后，显式纠错的 `feedbackText` 会被原样发送给 TTS，因此该标签成为每次显式纠错语音的固定开头。原实现没有对旧模型输出或旧数据中的该前缀做归一化处理。

## 4. 修复设计

### 4.1 从生成源头取消固定标签

`RealLLMService` 的独立纠错提示词和兼容联合提示词均改为：

- 直接从一条简短纠错规则开始。
- 保留 `Try: "[correct expression]"` 推荐句结构。
- 明确禁止在规则前添加 `Grammar tip` 等标题或标签。

显式纠错仍然保持简短、适合 VR 语音播放，纠错内容本身没有被删除。

### 4.2 修正本地兜底文案

缺失模型反馈时的本地兜底由：

`Grammar tip: Use this form. Try: "...".`

改为：

`Use this form. Try: "...".`

缺少可用修改句时同样不再生成固定标签。

### 4.3 统一旧结果归一化

在 `CorrectionTextGuards` 新增 `RemoveGrammarTipPrefix`：

- 仅处理文本开头的 `Grammar tip` 和 `Grammar tips`，大小写不敏感。
- 支持英文/中文冒号、逗号、句号及连字符、短横线、长横线等标签分隔符。
- 不处理正文中间的相同词语。
- 不把 `Grammar tips are useful.` 之类正常句子误判为标签。

`RealLLMService` 在纠错结果最终化和实验条件归一化路径中统一调用该逻辑，覆盖新独立纠错路径及兼容路径。

### 4.4 TTS 边界增加最终防护

`CorrectionFeedbackPresenter.ResolveFeedbackText` 在显式纠错文本送入 TTS 前再次清理前缀。即使纠错数据来自旧缓存、测试数据或绕过当前生成逻辑的其他来源，实际语音也不会再朗读固定标签。

重述反馈继续优先使用 `recastText`，不受此修改影响。

## 5. 状态机与实验不变量

本次明确保持以下行为不变：

- `CorrectionFeedbackSpeaking`、`DialogueSpeaking` 等状态及转换条件不变。
- 反馈优先门控及“纠错语音完成后再播放角色回复”的顺序不变。
- `hasFeedback`、错误类型、纠错敏感度和非语音差异过滤规则不变。
- `dialogue_avatar` / `assistant_agent` 提供者和 `explicit` / `recast` 风格编码不变。
- NE、NR、SE、SR 正式实验条件不变。
- 语音角色、音色、空间化策略和实验事件记录流程不变。

## 6. 涉及文件

- `Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/CorrectionTextGuards.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/CorrectionFeedbackPresenter.cs`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/CorrectionPolicyTests.cs`

## 7. 回归测试

`CorrectionPolicyTests` 新增或加强以下覆盖：

- 显式纠错提示词要求直接从规则开始，不再包含旧固定格式。
- 单数、复数、大小写、中文冒号及多种标点形式的前缀清理。
- 非标签语句不会被误删。
- `RealLLMService` 最终化会清理旧模型返回的前缀。
- `CorrectionFeedbackPresenter` 在 TTS 取文边界会清理旧前缀。
- 缺失纠错文本的本地修复结果不再以 `Grammar tip` 开头。

## 8. 验证结果

- `dotnet build Client/Assembly-CSharp.csproj --no-restore`：通过，0 个错误。
- `dotnet build Client/Assembly-CSharp-Editor.csproj --no-restore`：通过，0 个错误。
- Unity 编辑器已导入本次三个运行时脚本及测试脚本，日志未出现相关编译错误。
- 运行时代码全文检查未发现仍会生成 `Grammar tip:` / `Grammar tips:` 固定开头的路径。
- `git diff --check`：通过。

构建仍有项目既有的程序集版本冲突、弃用 API 和未赋值字段警告。本次 Unity MCP 未提供可用资源，且项目已在 Unity 编辑器中打开，因此没有另启第二个 Unity 实例运行 Test Runner；新增 EditMode 测试已随 Editor 程序集成功编译。

## 9. 本次提交排除项

工作区还存在本次任务开始前已有的 Unity/用户生成改动，包括实验协议、构建信息、字体、场景和项目设置等。本次提交使用明确文件列表，仅包含上述四个代码/测试文件及本说明文档。

尤其需要继续排除：

- `Client/Assets/SceneTalkVR/ExperimentProtocol/ExperimentBuildInfo.asset`
- `Client/Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset`
- `Client/Assets/SceneTalkVR/Fonts/NotoSansSC-VF SDF.asset`
- `Client/Assets/Scenes/SampleScene.unity`
- `Client/ProjectSettings/ProjectSettings.asset`

字体资产仍存在纹理数据异常缩减风险，不应混入本次纠错语音提交。Git 状态中只有时间戳或状态缓存变化、没有实际内容差异的其他 Unity 文件同样不暂存。
