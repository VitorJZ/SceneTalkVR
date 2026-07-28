# 对话按钮录音状态机实施记录

## 目标

本次改动把提出需求和与 Avatar 对话时的语音输入从固定录音时长改为手动结束录音。

- 手柄射线命中世界空间 UI 按钮时，任一扳机用于点击按钮。
- 手柄没有指向按钮时，扳机不会开始或结束录音。
- 需求阶段保留 `Listen` 按钮，并复用为 `Listen -> End -> Retry -> End`。
- 对话阶段保留 `Speak` 按钮，并复用为 `Speak -> End -> Speak`。
- 旧的独立 `Retry` 按钮已从运行时 UI builder 中去除。

## 实现范围

- `SceneTalkOrchestrator` 统一管理录音状态，新增 `Recording` 与 `Transcribing` 状态。
- `ISceneTalkManualSpeechInput` 为语音输入模块提供停止和取消录音信号。
- `MicrophoneRecorder` 支持外部停止信号，`maxRecordingSeconds` 作为循环录音缓冲长度，默认上限为 100 秒。
- `GatewaySpeechInputModule` 和 `DemoSpeechInputModule` 都支持手动停止，保留真实语音与离线 demo 两条路径。
- `SceneTalkFlowUiController` 根据状态切换 `Listen/End/Retry` 与 `Speak/End`。
- `SceneTalkInteractionBootstrap` 仅将扳机用于射线按钮点击，不再提供硬件录音快捷入口。

## 用户流程

### 提出需求

1. 点击 `Start` 进入需求阶段。
2. 点击 `Listen` 开始录音。
3. 录音中按钮显示 `End`。
4. 点击 `End` 结束录音。
5. STT 完成后按钮显示 `Retry`，`Confirm` 可用。
6. 点击 `Retry` 可重新录制需求。

### 与 Avatar 对话

1. Avatar 首轮回复完成后进入可对话状态。
2. 点击 `Speak` 开始录音。
3. 录音中按钮显示 `End`。
4. 点击 `End` 结束录音。
5. 系统执行 `STT -> Brain -> Avatar TTS`。
6. Avatar 回复完成后按钮回到 `Speak`。

## 回归重点

- Unity Editor 中通过 `Listen/End/Retry` 和 `Speak/End` 完成流程。
- PICO 上空指向按下或松开扳机均不会改变录音状态。
- PICO 上指向按钮时扳机只点击按钮。
- Android 麦克风权限、局域网 voice gateway、STT 上传和 TTS 播放仍需真机回归。
