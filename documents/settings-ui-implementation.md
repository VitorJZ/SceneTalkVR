# SceneTalkVR 设置界面实施记录

日期：2026-07-09

## 目标

在初始世界空间 UI 中增加 `Settings` 入口，用于支持玩家个性化显示设置：

- 字体大小。
- 界面大小，范围为 50%-125%。
- 是否隐藏对话字幕，以提升沉浸感。
- 设置页右上角提供 `Exit`，用于回到初始界面。

按键绑定功能已移除。PICO/OpenXR 手柄交互采用固定规则：任一扳机在指向 UI 按钮时确认点击；未指向按钮时不会开始或结束录音，录音仅由界面按钮控制。

## 架构范围

设置功能只属于 Unity 客户端运行时显示配置，不改变服务端、LLM、STT、TTS、Avatar 或 Holodeck 的接口协议。

- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkState.cs`
  - 新增 `SceneTalkState.Settings`。
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
  - 新增 `OpenSettings()` 和 `CloseSettings()`。
  - 设置页打开时禁止直接开始练习。
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkUserSettings.cs`
  - 保存字体大小、界面大小和对话字幕显示状态。
  - 使用 `PlayerPrefs` 保存本机偏好。
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
  - 初始界面新增 `Settings`。
  - 沿用现有运行时 uGUI 生成方式构建设置页。
  - 隐藏字幕时把底部对话区域收缩为紧凑操作条。
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs`
  - 保持固定扳机交互：指向按钮时点击，空指向时不触发操作。

## 状态机

客户端主流程状态机新增 `Settings`：

```text
Idle
Settings
Listening
Recording
Transcribing
Processing
SceneReady
AvatarSpeaking
Finished
Error
```

设置页不再需要单独的按键绑定状态机，只负责显示配置。

## UI 布局

设置页使用一个紧凑面板：

- `Font Size`：调整运行时 UI 文本大小。
- `Interface Size`：调整世界空间 Canvas 整体大小，范围为 50%-125%。
- `Dialogue Subtitles`：显示或隐藏对话字幕。

隐藏字幕时，底部字幕框会收缩为紧凑操作条，只保留状态文字和 `Speak/End` 按钮，避免保留大面积空白。

## 验证清单

- 初始界面显示 `Start`、`Settings` 和 `Quit`。
- 点击 `Settings` 进入设置页。
- 右上角 `Exit` 返回初始界面。
- 字体大小调整后，运行时生成的 UI 文本即时变化。
- 界面大小调整后，世界空间 Canvas 即时缩放，最小可到 50%。
- 隐藏字幕会隐藏 `You:` 和 `Avatar:` 两行对话文本，并把底部字幕框收缩为紧凑操作条，不隐藏操作按钮和状态信息。
- 左右扳机都能在指向 UI 按钮时点击按钮。
- 左右扳机空指向时均不会改变录音状态，指向录音按钮时可正常点击。
- PICO 真机仍需回归验证左右手柄扳机输入。
