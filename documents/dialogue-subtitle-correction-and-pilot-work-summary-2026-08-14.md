# 对话字幕、错误恢复与预实验临时配置工作汇总

日期：2026-08-14

## 本次范围

本次汇总覆盖当前工作区内全部未提交修改，主要包括：

- 纠错语音与角色回复字幕同步显示。
- 新增辅助角色纠错字幕，同时保留独立的 `CorrectionFeedback`。
- 长字幕自动换行、动态扩展与完整显示。
- 可恢复 LLM 错误的重试流程与编辑器日志处理。
- 纠错播报文本的统一解析和字幕触发时机。
- 预实验临时统一使用“无预约到店”任务。
- 任务面板旋转、场景视野、字体图集和编辑器设置调整。
- 对应 EditMode、PlayMode 回归测试。

## 问题与根因

### 纠错字幕晚于角色回复出现

流式生成过程中，角色回复会在句子到达时立即写入原有字幕控件；纠错内容则需要等待纠错方案解析、TTS 准备完成并真正开始播放后才确定。两条链路此前没有统一的显示屏障，因此会先看到角色回复，再看到后补的纠错文本。

### 纠错播报与字幕内容可能不一致

纠错文本在不同调用点分别处理 `recastText`、`feedbackText`、`correctedText` 和 `Grammar tip` 前缀，容易造成实际播报内容与字幕采用不同的回退顺序。若仅在计划生成时显示字幕，还会在音频未真正开始或播放失败时提前展示纠错内容。

### 长文本显示不完整

原字幕区域使用固定高度，并受到自动字号、固定行高和遮罩区域限制。较长的英文或连续中文文本即使换行，也可能超出文本矩形或面板边界而被裁切。

### PC 编辑器中的可恢复错误无法操作重试

可恢复的 LLM 返回格式错误或请求失败使用 `Debug.LogError`。启用 Unity Error Pause 时，编辑器会在错误日志产生后立即暂停，导致错误状态和“重试”按钮还未来得及完成刷新，看起来像是直接退出流程。

## 实现内容

### 字幕同步状态

`SceneTalkOrchestrator` 增加本轮字幕同步状态，分别记录：

- 角色回复文本是否已经到达。
- 纠错方案是否已经解析完成。
- 本轮是否需要播放纠错。
- 纠错字幕内容是否已经在音频播放开始时就绪。

只有角色回复已到达，并且纠错方案及必要的纠错字幕都已就绪时，`AreTurnSubtitlesReady` 才允许 UI 同时展示本轮字幕。无纠错回合在方案明确为无纠错后直接放行；新回合会清空上轮缓存，防止旧字幕串入。

同步时序如下：

```text
开始新回合
  -> 缓存流式角色回复
  -> 等待纠错方案解析
     -> 无纠错：一次刷新显示角色回复
     -> 有纠错：等待纠错音频真正开始
        -> 一次刷新显示纠错字幕和角色回复
```

### 纠错字幕归属

- 对话角色执行纠错时，将实际播报的纠错文本放在 `AvatarSubtitle` 中，并位于角色回复前方，顺序与语音播放一致。
- 辅助角色执行纠错时，新增 `AgentSubtitle` 展示辅助角色的实际播报文本；`AvatarSubtitle` 继续展示对话角色回复。
- 原有独立 `CorrectionFeedback` 继续显示结构化纠错结果，未被替换或移除。
- 历史回合恢复时，会从已保存的纠错载荷重建已播报字幕信息。

### 纠错文本和播放事件

`CorrectionTextGuards.ResolveSpokenFeedbackText` 统一处理纠错播报文本：

- Recast 优先使用 `recastText`，再回退到 `feedbackText` 和 `correctedText`。
- Explicit 优先使用 `feedbackText`，再回退到 `correctedText`。
- `Grammar tip` 前缀支持空白和中英文标点分隔，并在播报和字幕中统一移除。

`CorrectionFeedbackPresenter` 仅在音频播放真正开始时发出 `CorrectionSubtitleStarted`。播放未能开始时不产生字幕提示，避免出现“有纠错文字但没有对应语音”的假象。

### 长字幕完整显示

参与者字幕、辅助角色字幕、对话角色字幕、独立纠错内容和状态文本统一采用正常换行，并关闭自动缩小与截断。UI 每次刷新时根据 TMP `preferredHeight` 计算实际高度，按以下顺序重新排列：

```text
调试信息（如有）
参与者字幕
辅助角色字幕（如有）
对话角色字幕
独立纠错内容（如有）
纠错状态
对话状态
```

字幕面板固定底边位置，高度不足时只向上扩展；发言/重试按钮保持在底部固定区域。这样可以完整显示长英文、连续中文以及放大字体后的内容，同时避免面板向中心下方漂移。

### LLM 错误恢复

- 可恢复的 LLM 失败继续进入现有 `Error/Retry` 状态，并显示“重试”按钮。
- 可恢复失败改记为 Warning，避免 Unity Editor 的 Error Pause 抢先暂停界面刷新。
- 正式实验已被判定为技术无效的失败仍保留 Error 日志，不降低审计严重性。
- 恢复提示文本同步写入角色字幕，并记录可恢复失败的模块和技术原因。
- 重试会重新进入录音流程，不把失败回合误判为正常完成。

### 预实验临时任务分配

预实验分配版本升级为 `2.1-temporary-walk-in-only`。三种呈现条件暂时全部使用：

```text
pilot_restaurant_walk_in
```

呈现条件的三种 embodiment 仍各出现一次，每个条件继续生成唯一的 `taskAssignmentId`。旧的 `2.0-participant-choice` 未完成分配会因版本变化而被拒绝，需要重新创建会话。正式任务目录和另外两个预实验任务资源没有被删除。

### 场景、字体与编辑器设置

- `SceneTalkVR Task Goal UI` 本地旋转固定为 identity，不再在运行时持续朝向相机，避免运行值覆盖场景中的 Y Rotation = 0。
- `SampleScene` 中任务画布旋转归零，主相机 FOV 从 60 调整为 100，两处 Canvas Scaler 标记为 World 预设。
- `NotoSansSC-VF SDF.asset` 生成 2048×2048 中文 TMP 图集，补齐中文界面所需字形。
- `EditorSettings.asset` 的 Enter Play Mode 选项调整为值 1。
- `ExperimentBuildInfo.asset` 同步更新构建提交标识和构建时间。

## 修改文件

### 运行时代码

- `Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/CorrectionFeedbackPresenter.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/CorrectionTextGuards.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/PilotExperimentModel.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`

### 测试

- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/AvatarSpeechPlayerTests.cs`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/CorrectionPolicyTests.cs`
- `Client/Assets/SceneTalkVR/Tests/Editor/DialogueRecoveryTests.cs`
- `Client/Assets/SceneTalkVR/Tests/Editor/EditorDemoModeTests.cs`
- `Client/Assets/SceneTalkVR/Tests/Editor/PilotCollectionFlowTests.cs`
- `Client/Assets/SceneTalkVR/Tests/Editor/PilotSceneMappingTests.cs`
- `Client/Assets/SceneTalkVR/Tests/Editor/Stage6PilotEmbodimentTests.cs`
- `Client/Assets/SceneTalkVR/Tests/PlayMode/EditorCollectionParticipantFlowPlayModeTests.cs`
- `Client/Assets/SceneTalkVR/Tests/PlayMode/PilotCollectionParticipantFlowPlayModeTests.cs`

### 资源与设置

- `Client/Assets/SceneTalkVR/ExperimentProtocol/ExperimentBuildInfo.asset`
- `Client/Assets/SceneTalkVR/Fonts/NotoSansSC-VF SDF.asset`
- `Client/Assets/Scenes/SampleScene.unity`
- `Client/ProjectSettings/EditorSettings.asset`

## 测试覆盖

新增或更新的回归覆盖包括：

- 纠错音频开始时才产生字幕 cue，播放无法开始时不产生 cue。
- Recast/Explicit 播报文本选择与 `Grammar tip` 前缀移除。
- 纠错先到、回复先到和无纠错三种同步顺序。
- 纠错播放完成后不回退到错误的纠错播放状态。
- 可恢复 LLM 失败保留对话面板和重试入口，重试后重新进入录音。
- 辅助角色与对话角色两种纠错字幕归属，并确认独立 `CorrectionFeedback` 仍存在。
- 长英文、连续中文和字体放大后的换行、动态高度、排列顺序及面板边界。
- 任务面板运行时本地旋转保持为零。
- 三种预实验呈现条件统一映射到无预约到店任务，且旧分配版本失效。

## 审查与验证

- 已逐文件审查全部未提交差异，未发现密钥、导出数据或临时测试产物。
- `git diff --check`：通过。
- `dotnet build Client/Client.sln --no-restore`：0 错误；保留 2 个项目既有依赖冲突警告。
- `DialogueRecoveryTests`：EditMode 8/8 通过。
- 字幕同步 PlayMode：1/1 通过。
- 长字幕与纠错字幕 PlayMode：2/2 通过。
- Unity Console：0 Error。

## 已知事项

- 预实验统一使用无预约到店任务属于明确的临时实验配置；恢复三任务轮换时需要再次升级分配版本并同步更新测试。
- `ExperimentBuildInfo.asset` 中记录的是生成该资源时的构建提交，不是本次尚未创建的最终提交哈希。
- 编译中的 2 个警告为当前项目已有的 Unity/MCP 依赖版本冲突警告，本次修改未新增编译错误。

本次只创建本地 Git 提交，不推送远端。
