# 对话面板精简与状态隐藏布局优化汇总

日期：2026-08-14

## 修改目标

本次修改针对对话面板完成三项精简：

- 不再创建或显示独立的 `CorrectionFeedback` 字幕行。
- 状态显示关闭时，根据实际可见内容缩短面板，去除底部留白。
- 对话角色和辅助角色在中英文界面中统一使用 `A:`、`B:` 标识。

## 原因分析

### 独立纠错字幕重复

纠错语音文本已经按照实际播报者显示在 `AvatarSubtitle` 或 `AgentSubtitle` 中，独立的 `CorrectionFeedback` 再次展示结构化纠错结果会形成内容重复。本次只移除该视觉对象及专用派生文本，不移除纠错生成、纠错语音、提供者判断、实验审计或字幕同步机制。

### 状态隐藏后仍有留白

原动态布局即使已经隐藏 `CorrectionStatus` 和 `DialogueStatus`，仍会无条件测量这两行的高度，并把高度计入 `statusCursor` 和面板总高度。因此对象不可见，但其空间仍然保留。

## 实现内容

### 移除独立 CorrectionFeedback 对象

运行时 UI 不再：

- 声明 `correctionFeedbackText` 字段。
- 创建名为 `CorrectionFeedback` 的 TMP 对象。
- 刷新独立纠错显示文本。
- 将独立纠错行纳入动态高度和垂直排列。

`SceneTalkOrchestrator` 同步删除只服务于该对象的 `LastCorrectionDisplayText` 和文本解析方法。以下机制保持不变：

- `CorrectionFeedbackData` 数据模型。
- `CorrectionFeedbackPresenter` 纠错播报组件。
- 角色或助手纠错提供者判断。
- 纠错语音与角色回复的同步显示屏障。
- `CorrectionStatus`、错误恢复、实验记录与审计。

### 统一 A/B 说话者标签

- `AvatarSubtitle` 固定使用 `A: `。
- `AgentSubtitle` 固定使用 `B: `。
- 标签不随中英文设置变化。
- 参与者字幕继续使用现有“你：/You: ”双语标签。

对话角色执行纠错时显示顺序仍为：

```text
A: 纠错播报文本
角色回复文本
```

辅助角色执行纠错时仍分别显示：

```text
B: 辅助角色纠错播报文本
A: 对话角色回复文本
```

### 状态隐藏时动态缩短面板

`ApplySubtitleLayout` 现在同时接收字幕和状态隐藏设置，并只为当前激活对象计算高度：

- `CorrectionStatus` 隐藏时高度为零。
- `DialogueStatus` 隐藏时高度为零。
- 两行之间的间距只在两行实际显示时加入。
- 状态隐藏时，最小高度按顶部边距、按钮区域和实际字幕内容计算。
- 字幕与状态同时隐藏时，面板缩至仅容纳发言/重试按钮及边距。
- 面板宽度、底部锚点和按钮位置保持不变；长字幕仍向上扩展并完整换行。

## 显示行为

| 字幕设置 | 状态设置 | 面板内容与尺寸 |
|---|---|---|
| 显示 | 显示 | 显示字幕、状态和按钮，保留现有最小高度 |
| 显示 | 隐藏 | 显示字幕和按钮，移除状态占用高度 |
| 隐藏 | 显示 | 显示状态和按钮，使用状态模式最小高度 |
| 隐藏 | 隐藏 | 仅显示按钮，缩至按钮所需高度 |

## 修改文件

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
  - 删除独立纠错字幕专用派生状态。
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
  - 删除 `CorrectionFeedback` UI 创建与布局链路，增加 A/B 标签和按激活行计算的紧凑布局。
- `Client/Assets/SceneTalkVR/Tests/PlayMode/EditorCollectionParticipantFlowPlayModeTests.cs`
  - 正式实验覆盖对象不存在、状态隐藏后缩短、底边和按钮位置不变。
- `Client/Assets/SceneTalkVR/Tests/PlayMode/PilotCollectionParticipantFlowPlayModeTests.cs`
  - 预实验覆盖四种显示组合、A/B 双语稳定性、纠错提供者显示和长字幕完整性。

未修改场景文件、预制体、纠错协议或实验数据结构。

## 审查与验证

- 已逐项检查全部未提交差异，未发现无关修改、密钥或导出数据。
- `dotnet build Client/Client.sln --no-restore`：0 错误，4 个项目既有警告。
- 字幕同步相关 EditMode：2/2 通过。
- 正式与预实验目标 PlayMode：4/4 通过。
- PlayMode 覆盖：
  - 运行时不存在 `CorrectionFeedback` GameObject。
  - 中文和英文均保持 `A:`、`B:` 标签。
  - 状态隐藏后面板高度减小。
  - 字幕和状态同时隐藏时进一步缩短。
  - 面板底边和发言按钮位置不变。
  - 长英文、连续中文和放大字体均完整换行显示。
- Unity Console：目标验证后 0 Error。
- `git diff --check`：通过。

测试期间动态写入的 TMP 字体图集已恢复，没有纳入本次修改。本次只创建本地 Git 提交，不推送远端。
