# 纠错模式热切换与 PICO UI 调试记录（2026-07-17）

## 1. 文档范围

本文记录 2026-07-17 完成的以下工作：

- 设定页增加纠错来源与纠错方式的运行时热切换。
- 修复热切换测试中暴露的条件未重新注入问题。
- 修复测试通过 `SendMessage("OnEnable")` 触发生命周期断言的问题。
- 清理项目自有代码中的 Unity 6 废弃 API warning 和未使用变量 warning。
- 分析 Unity/XRI/PICO 包产生的两类非阻塞 warning。
- 修复 PICO 实机中所有世界空间 UI 面板在特定观察角度整体消失的问题。
- 记录本地 Voice Gateway 与 LLM Gateway 的联调状态。

本文以当前代码为准，不依赖已过时的 2026-07-16 纠错模式设定方案文档。

## 2. 纠错模式热切换

### 2.1 目标

原有纠错模式由来源和方式组合为四种条件，但只能在运行前设定。此次在 `SceneTalkState.Settings` 中增加两个独立切换项：

- `Correction Source`：`Dialogue Avatar` / `Assistant Agent`
- `Correction Style`：`Explicit` / `Recast`

切换只影响下一回合，不清空对话历史，也不修改正在生成、播放或等待结算的回合。

### 2.2 条件真源与四种映射

`ExperimentConditionManager` 仍是纠错条件的唯一真源。没有在 `SceneTalkUserSettings` 中复制 provider/style 状态。

| Provider | Style | Condition ID |
| --- | --- | --- |
| `dialogue_avatar` | `explicit` | `dialogue_avatar_explicit` |
| `dialogue_avatar` | `recast` | `dialogue_avatar_recast` |
| `assistant_agent` | `explicit` | `assistant_agent_explicit` |
| `assistant_agent` | `recast` | `assistant_agent_recast` |

新增的只读状态与切换接口包括：

- `CurrentConditionId`
- `CurrentFeedbackProvider`
- `CurrentFeedbackStyle`
- `CanUseManualRuntimeCondition`
- `ManualRuntimeConditionLockReason`
- `TrySetManualFeedbackProvider(...)`
- `TrySetManualFeedbackStyle(...)`

单轴切换会保留另一轴，再统一解析为 `ExperimentConditionPreset`、刷新 `CurrentCondition`，最后发送既有的 `ExperimentConditionChanged` 事件。

### 2.3 状态机约束

UI 不直接操作 `ExperimentConditionManager`，而是通过 `SceneTalkOrchestrator` 命令进入现有状态机链路。

手动切换必须同时满足：

- 当前状态为 `Settings`。
- 没有运行中的协程回合或录音。
- 没有活动回合或待结算回合。
- `formalExperiment == false`。
- `useConditionOrder == false`。

不满足条件时仍显示当前值，但按钮显示 `Locked`，并呈现对应原因：正式实验、条件顺序或当前回合尚未结束。

`OpenSettings()` 只允许从 `Idle` 或 `Finished` 进入，退出仍通过 `CloseSettings()` 返回状态机，不由 UI 自行改状态。

### 2.4 真实重新注入链路

热切换不是只改 UI 文本。条件事件由 `SceneTalkOrchestrator` 订阅，安全切换后重新执行现有注入链路：

```text
Settings button
  -> SceneTalkOrchestrator.ChangeCorrection*Setting()
  -> ExperimentConditionManager.TrySetManual*()
  -> ExperimentConditionChanged
  -> SceneTalkOrchestrator.OnExperimentConditionChanged()
  -> ApplyExperimentConditionToModules()
       -> RealLLMService.SetExperimentCondition()
       -> AvatarPresentationVoiceModule.SetCorrectionFeedbackProvider()
  -> next BeginTurn() captures the new condition
```

因此下一次生成使用新的 style，实际播报主体使用新的 provider，下一回合日志也使用相同的 condition/provider/style。原有 payload 强制校准逻辑继续防止模型返回旧条件。

### 2.5 设定页改动

`SceneTalkFlowUiController` 的设定页扩大为 `820 x 500`，页面标题改为 `Display & Correction`，新增：

- 当前纠错来源显示。
- 当前纠错方式显示。
- 两个独立的大型 `Change` 按钮。
- 下一回合生效提示或锁定原因。

## 3. 热切换测试调试记录

### 3.1 模块没有收到新 provider

失败测试：

```text
OrchestratorReinjectsChangedAxesIntoGenerationAndPresentationModules
Expected: dialogue_avatar
But was:  assistant_agent
```

日志显示 `ExperimentConditionManager` 已正确切换条件，但 Avatar 表现模块仍保留旧 provider。这证明条件真源已经变化，问题发生在通知后的模块重新注入阶段。

根因是测试通过 `ConfigureModules()` 动态组装模块时，Orchestrator 只执行了初次条件注入，没有确保订阅 `ExperimentConditionChanged`。后续条件变化无法沿同一条链路重新注入。

修复方式：

- `ConfigureModules()` 中调用 `SubscribeExperimentConditionChanges()`。
- `OnEnable()` / `OnDisable()` 继续负责正常生命周期的订阅和退订。
- 订阅函数会先检查当前 manager，防止重复订阅。

### 3.2 `ShouldRunBehaviour()` 断言

第二次失败来自测试代码主动执行：

```csharp
component.SendMessage("OnEnable");
```

在 EditMode 中手工发送 Unity 生命周期函数会绕过正常 Behaviour 生命周期，导致 Unity 内部触发：

```text
Assertion failed on expression: 'ShouldRunBehaviour()'
```

修复方式是删除测试中的 `SendMessage("OnEnable")`，由正常的组件创建和 `ConfigureModules()` 完成订阅。生产代码不再依赖测试模拟生命周期。

### 3.3 覆盖范围

`ExperimentConditionRuntimeSwitchTests` 覆盖：

- 四种 provider/style 组合到 condition ID 的映射。
- 单轴切换不影响另一轴。
- DemoBrain payload 使用当前两轴。
- 正式实验、条件顺序、活动回合和待结算回合拒绝切换。
- Orchestrator 将新条件重新注入 LLM 与 Avatar 表现模块。
- 下一回合日志使用新条件。
- 设定页生成两个独立按钮。
- 条件顺序模式下按钮锁定并显示原因。

## 4. Warning 清理与分类

### 4.1 已修复的项目代码 warning

Unity 6 已废弃 `FindObjectOfType<T>()`。项目自有代码中的五处调用已替换为保持原有活动对象查找语义的 `FindFirstObjectByType<T>()`：

- `SceneTalkOrchestrator`
- `ExperimentConditionManager`
- `RealLLMService` 两处
- `AvatarPresentationVoiceModule`

同时删除 `SceneTalkOrchestrator.GenerateSceneAndReplyWithStreamingSupport()` 中未被读取的 `isDone` 变量及其两个赋值。该协程本身已经 `yield return` 等待生成过程结束，不需要额外完成标志。

项目自有 `SceneTalkVR` C# 代码中已无 `FindObjectOfType<T>()` 调用。

### 4.2 未修改的包 warning

以下 warning 不属于项目业务代码，因此没有修改 `Library/PackageCache` 或嵌入的第三方包：

1. `XRManagerSettings.OnDisable` 在 manager 未初始化时调用 `StopSubsystems`：属于 XR Management 在 Editor 生命周期或 domain reload 中的提示。
2. `Could not find Samples directory (Assets\\Samples)`：来自 XR Interaction Toolkit 3.5.0 的样例缓存扫描。项目没有导入 Samples 时会输出 `Debug.LogWarning` 并返回，不会中止构建。若需要消除，可在 Unity 项目中创建空的 `Assets/Samples` 目录。

如果构建失败，应继续查找 Console 中真正的红色 Error，不能把上述 warning 当作失败原因。

## 5. PICO 世界空间面板角度消失

### 5.1 现象与共同点

初始、设定、请求、加载和对话界面并不是独立 Canvas，而是 `SceneTalkFlowUiController` 在同一个 `SceneTalkVR World UI` 世界空间 Canvas 下切换的多个面板。

因此多个界面在相同角度一起消失，说明问题位于共享 Canvas 或 XR 相机，而不是各面板的状态逻辑。

### 5.2 代码层根因

检查发现两个共同风险：

1. Canvas 只在启动后和手柄手动重居中时设置一次朝向。头显发生位置变化后，Canvas 会逐渐形成侧视或背视角，整个平面会一起进入极端观察角。
2. 最近加入的普通相机 FOV 功能在每个 `Update()` 中写入 XR 相机的 `fieldOfView`。PICO 双眼投影应由 XR Runtime 管理，持续写入普通相机 FOV 存在投影和剔除状态不一致的风险。

PICO 的 foveated rendering、subsample 和 Application SpaceWarp 当前均未启用，因此不是此次问题的首要来源。

### 5.3 修复

`SceneTalkInteractionBootstrap` 现在：

- 在 `LateUpdate()` 中让共享世界空间 Canvas 水平朝向头显。
- 只更新旋转，不改变 Canvas 的世界位置，因此仍保持世界空间面板和手柄手动重居中语义。
- 重居中后也复用同一朝向函数，避免两套旋转算法分叉。
- 对带 `TrackedPoseDriver` 或已经启用 stereo 的相机，不再写入 `fieldOfView`。
- 桌面非 XR 相机仍保留原有 FOV 配置能力。

`WorldCanvasVisibilityTests` 覆盖：

- Canvas 朝向头显时世界位置不变。
- 跟踪相机不由项目覆盖 FOV。
- 桌面相机继续接受项目 FOV。

## 6. Gateway 联调记录

本次调试期间确认两个后台均监听 `0.0.0.0`：

- Voice Gateway：`8787`
- LLM Gateway：`8788`

最小链路检查结果：

- LLM Gateway 实际请求成功并返回 `ok`。
- Voice Gateway 使用 Tencent TTS 成功返回音频 URL，`fallbackLevel=none`。

本记录不包含任何 API Key 或云服务凭据。本地 JSON 和仓库根目录 `.env` 不应加入 Git。

## 7. 验证状态

| 验证项 | 结果 |
| --- | --- |
| Unity Editor 运行时程序集编译 | 0 error |
| Editor 测试程序集（包含新增测试）编译 | 0 error |
| PICO/Android 编译参数验证 | 0 error |
| PICO/Android 编译现存 warning | `DemoSpeechInputModule.isCapturing` 未使用，非本次引入 |
| `git diff --check`（相关已跟踪脚本） | 通过 |

注意：11:33 安装到 PICO 的 APK 早于世界空间 Canvas 修复，不能用于验收该修复。需要重新 Build & Run。

## 8. 后续手动验收

### 8.1 Unity Test Framework

在 EditMode 中运行：

- `ExperimentConditionRuntimeSwitchTests`
- `WorldCanvasVisibilityTests`

确认没有 `ShouldRunBehaviour()` 断言和条件重新注入失败。

### 8.2 纠错模式

在 `formalExperiment=false`、`useConditionOrder=false` 时：

1. 进入 Settings。
2. 分别切换 Source 和 Style。
3. 确认另一轴保持不变。
4. 开始下一回合，确认 payload、实际播报主体和回合日志一致。
5. 分别验证四种组合。
6. 在正式实验、条件顺序和未结算回合中确认按钮锁定。

### 8.3 PICO 面板

重新 Build & Run 后分别检查初始、设定和对话界面：

1. 正面观察面板。
2. 左右侧移头部，从较大水平夹角观察。
3. 确认面板只旋转朝向用户，不随头显改变世界位置。
4. 确认三个界面都不会在相同角度突然整体消失。
5. 确认手柄射线、按钮点击以及握持键/摇杆重居中仍正常。

## 9. 本次建议提交范围

应提交：

- `Client/Assets/SceneTalkVR/Scripts/Core/ExperimentConditionManager.cs`
- `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkFlowUiController.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs`
- `Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs`
- `Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/ExperimentConditionRuntimeSwitchTests.cs`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/ExperimentConditionRuntimeSwitchTests.cs.meta`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/WorldCanvasVisibilityTests.cs`
- `Client/Assets/SceneTalkVR/Avatar/Tests/Editor/WorldCanvasVisibilityTests.cs.meta`
- `documents/runtime-correction-hot-switch-and-pico-ui-debug-2026-07-17.md`

当前工作区中以下文件存在其他改动，不应随本次提交一起暂存：

- `Client/Assets/Scenes/SampleScene.unity`
- `Client/Assets/Settings/Mobile_RPAsset.asset`
- `Client/Assets/_Recovery/0 (6).unity`
- `Client/ProjectSettings/ProjectSettings.asset`
- `Client/Packages/com.unity.xr.picoxr/Runtime/windows/x86_64/applogrs.pdb.meta`
