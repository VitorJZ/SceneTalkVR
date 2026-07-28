# SceneTalkVR

## 中文指南

SceneTalkVR 是一个面向 PICO/VR 的英语情景练习课程项目。当前仓库采用“Unity 客户端为主，AI/场景生成模块服务端解耦”的路线：`Client` 负责 VR 交互、UI、流程调度和 PICO 打包，`Holodeck` 与后续 LLM、STT、TTS、Avatar、场景生成能力通过轻量接口接入。

### 项目结构

- `Client`：Unity 6 客户端工程，当前使用 Unity `6000.3.16f1`。
- `Client/Assets/SceneTalkVR`：Vitor 负责的客户端主流程、Demo 模块、编辑器工具和说明文档。
- `Client/Packages/com.unity.xr.picoxr`：已随工程提交的 PICO Unity Integration SDK embedded package。
- `Holodeck`：独立的场景/生成相关模块，不直接塞进 Unity 客户端工程。
- `documents`：任务规划、会议记录和分工说明。

### 环境要求

- Unity `6000.3.16f1`
- Unity Hub 模块：`Android Build Support`、`Android SDK & NDK Tools`、`OpenJDK`
- 已接入包：Input System、Unity UI、XR Interaction Toolkit、OpenXR Plugin、PICO Unity Integration SDK
- PICO 真机测试：PICO 4 开启开发者模式和 USB 调试

### 快速运行 Unity Demo

1. 克隆仓库后，用 Unity `6000.3.16f1` 打开 `Client`。
2. 等待 Unity 完成 package resolve、脚本编译和资源导入。
3. 打开 `Assets/Scenes/SampleScene.unity`。
4. 在 Unity 顶部菜单运行 `SceneTalkVR/Setup/Apply Recommended Project Settings`。如果 Unity 触发重新编译，等待完成后再运行一次。
5. 运行 `SceneTalkVR/Setup/Rebuild Full Demo Rig (Voice Gateway)`。
6. 点击 Play，在初始界面点击 `Start`；初始界面显示纵向排列的 `Start`、`Settings` 和 `Quit`。
7. 运行 `SceneTalkVR/Diagnostics/Run Preflight Check` 查看当前环境报告：`Client/Assets/SceneTalkVR/Docs/VitorPreflightReport.md`。

### PICO 手柄操作

真机运行时，Demo 支持 PICO/OpenXR 通用手柄输入：

- 左右手柄会显示轻量 3D 手柄代理和射线；任一扳机仅在射线命中世界空间 UI 按钮时确认点击，录音、结束和重试均通过界面按钮操作。
- `A / X`：仅确认当前射线指向的 UI 按钮；射线未指向按钮时不触发操作。
- `B / Y` 或菜单键：返回初始界面。
- 握持键或摇杆按下：把世界空间面板重新居中到当前头显正前方。
- 初始界面的 `Quit` 按钮退出当前应用；非初始流程右上角的 `Exit` 按钮返回初始界面。

### Demo 流程 UI

当前 Demo UI 分为四个阶段：

1. 初始界面：显示 `Start`、`Settings` 和 `Quit`，没有 `Exit`。
2. 设置界面：点击 `Settings` 后进入；可设置字体大小、界面大小（50%-125%）和是否隐藏对话字幕；右上角 `Exit` 返回初始界面。
3. 场景/人物需求确认：点击 `Start` 后进入待录音状态，`Listen` 按钮可切换为 `End` 并手动结束录音；录音转写完成后同一按钮显示 `Retry`，并提供 `Confirm`；右上角 `Exit` 返回初始界面。
4. 加载界面：点击 `Confirm` 后显示场景和人物加载状态；右上角 `Exit` 返回初始界面。
5. 对话界面：加载完成后中间面板消失，底部显示玩家与 Avatar 的彩色字幕；如果在设置中隐藏字幕，底部区域会收缩为紧凑操作条，仅保留对话操作与状态；右上角 `Exit` 返回初始界面。

启动时客户端会等待头显位姿更新，然后把 Demo 面板放到当前视线前方。XR 相机带 `TrackedPoseDriver` 时不会再被脚本强制写入固定世界坐标，避免真机初始视角偏移。

### Unity 菜单说明

为了减少混淆，`SceneTalkVR` 菜单只保留三组入口：

- `SceneTalkVR/Setup/Rebuild Full Demo Rig (Voice Gateway)`：重建可运行 Demo，自动清理旧 Rig，并配置 Main Camera、World Space Canvas、EventSystem 和输入模块。
- `SceneTalkVR/Setup/Apply Recommended Project Settings`：应用 Android/OpenXR/PICO 推荐设置，包括包名、IL2CPP、ARM64、Min SDK、PICO OpenXR features 和 Build Settings。
- `SceneTalkVR/Diagnostics/Run Preflight Check`：生成环境预检报告，不修改主要项目设置。
- `SceneTalkVR/Advanced/Clear Generated Demo Rig`：只清理生成的 Demo Rig 和 World UI。
- `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`：仅在 OpenXR Validation 仍提示缺少 interaction profile 时使用；PICO Profile 正常时通常不需要。

### PICO / Android 打包路线

当前默认只走 `OpenXRLoader + PICO OpenXR Features`。不要同时启用 OpenXR Loader 和 PICO 原生 Loader，否则容易出现 XR provider 冲突。

建议打包前检查：

1. `SceneTalkVR/Setup/Apply Recommended Project Settings` 已运行并通过预检。
2. `Project Settings > XR Plug-in Management > Android` 中使用 OpenXR，并启用 PICO 相关 features/controller profile。
3. Android Graphics APIs 只保留 `OpenGLES3`，不要把 Vulkan 放在首位。
4. Android Scripting Backend 是 IL2CPP，Target Architecture 是 ARM64，Min SDK 不低于 API 29。
5. PICO 4 已开启开发者模式和 USB 调试。
6. 本地调试构建保持关闭 custom keystore，使用 Unity debug signing。
7. Console 右上角 error 为 `0`。PICO SDK 在 Unity 6 / XRI 3.x 下可能有较多 warning，只要没有 error，当前阶段不阻塞 Demo 打包。

### Git 与 keystore

- 不要提交 `Client/UserKeystore.keystore`、`*.keystore` 或 `*.jks`。
- `.gitignore` 已忽略 keystore 和根目录的 `PICO Unity Integration SDK-*` 下载/解压缓存。
- `Client/Packages/com.unity.xr.picoxr` 是项目实际使用的 embedded PICO SDK，需要随工程提交。
- 本地 PICO 调试默认使用 Unity debug signing，不启用 custom keystore。
- release 包才需要在 Unity 的 `Player > Publishing Settings` 中启用私有 keystore；签名文件和密码只通过私密渠道共享。

### GitHub 上传清单

当前应上传：

- `README.md`、`documents/`：项目说明、分工规划和任务记录。
- `Client/Assets/SceneTalkVR/`：主流程、Demo 模块、编辑器工具、PICO 手柄交互和说明文档。
- `Client/Assets/Scenes/SampleScene.unity`：当前可运行 Demo 场景。
- `Client/Assets/Settings/`、`Client/Assets/XR/Settings/`：URP、移动端和 OpenXR 配置。
- `Client/Assets/Resources/PXR_PlatformSetting.asset` 及 `.meta`：PICO SDK 生成的项目配置资产，当前没有 appID 或密钥，可以提交。
- `Client/Packages/manifest.json`、`Client/Packages/packages-lock.json`、`Client/Packages/com.unity.xr.picoxr/`：Unity 包依赖和 embedded PICO SDK。
- `Client/ProjectSettings/`：Unity 项目设置，包括 Android/OpenXR/PICO 打包配置。

不要上传：

- `Client/Library/`、`Client/Temp/`、`Client/Logs/`、`Client/UserSettings/`、`Client/build/`。
- `Client/*.csproj`、`Client/*.sln`、`.vs/`、`.vscode/`、`.idea/`。
- `Client/UserKeystore.keystore`、任何 `*.keystore` 或 `*.jks`。
- 根目录 `PICO Unity Integration SDK-*` 下载/解压缓存。
- PDB 调试符号，例如 `Client/Packages/com.unity.xr.picoxr/Runtime/windows/x86_64/applogrs.pdb`。

提交前建议运行：

```powershell
git status --short
git status --ignored --short
```

只把非 ignored 的项目源码、Unity 资产、Package 和 ProjectSettings 提交；ignored 项一般不需要手动处理。

### 队友接口接入指南

核心接口文件：

`Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`

总调度脚本：

`Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`

`SceneTalkOrchestrator` 会按以下顺序调用模块：

```text
ISceneTalkSpeechInput
  -> ISceneTalkBrain
  -> ISceneTalkScenePresenter
  -> ISceneTalkAvatarVoice
```

Edwin 负责语音对话：

- STT 输入接口：实现 `ISceneTalkSpeechInput.CaptureSpeech(...)`，把用户语音转成文本后调用 `onComplete(transcript)`。
- TTS/Avatar 输出接口：实现 `ISceneTalkAvatarVoice.PresentReply(...)`，读取 `SpringScenePayload.dialogueReply` 和 `avatarRole`，播放 TTS 音频并驱动 Avatar。
- 参考脚本：`Client/Assets/SceneTalkVR/Scripts/Demo/DemoSpeechInputModule.cs` 和 `Client/Assets/SceneTalkVR/Scripts/Demo/DemoAvatarVoiceModule.cs`。
- 接入方式：新脚本必须是 `MonoBehaviour` 并实现对应接口，然后挂到场景中的 `SceneTalkVR Demo Rig`，再拖到 `SceneTalkOrchestrator` 的 `speechInputModule` 和 `avatarVoiceModule` 字段。

Spring 负责场景生成：

- LLM/场景接口：实现 `ISceneTalkBrain.GenerateSceneAndReply(...)`，输入是 Edwin 的转写文本 `userText`，输出是 `SpringScenePayload`。
- 输出字段包括 `taskType`、`environmentType`、`dialogueReply`、`avatarRole`、`scene.mode`、`scene.skyboxUrl`、`scene.layoutObjects[]`。
- `layoutObjects[]` 中每个对象使用 `prefabKey`、`position`、`rotationY` 描述；Unity 客户端会按 `prefabKey` 查本地预制体。
- 参考脚本：`Client/Assets/SceneTalkVR/Scripts/Demo/DemoBrainModule.cs`。
- 如果只输出 JSON/数据，继续使用现有 `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkScenePresenter.cs` 呈现场景；如果要自定义呈现逻辑，实现 `ISceneTalkScenePresenter.PresentScene(...)` 并替换 `scenePresenterModule`。
- 接入方式：新脚本必须是 `MonoBehaviour` 并实现接口，然后拖到 `SceneTalkOrchestrator` 的 `brainModule` 字段；自定义场景呈现器则拖到 `scenePresenterModule` 字段。

实现规则：

- 所有耗时任务用 `IEnumerator` 协程返回，不能阻塞 Unity 主线程。
- 成功时只调用 `onComplete(...)`，失败时调用 `onError(message)`。
- 不要直接改 Vitor 的 XR/PICO 底层脚本；模块之间通过 `SceneTalkContracts.cs` 的接口交接。

### 当前开发状态

- Unity Editor 内 Demo 已能显示、点击并跑通假数据闭环。
- PICO/OpenXR 手柄交互已接入：手柄射线 + 扳机确认点击；录音、结束和重试仅通过界面按钮操作，并保留退出与面板重居中的快捷操作。
- Android/OpenXR/PICO 基础配置已完成，PICO 4 调试默认使用 OpenGLES3 规避 Vulkan 启动崩溃。
- PICO 4 真机已能启动 Demo，仍需继续验证手柄操作、UI 面板位置和完整演示路径。
- Spring 的真实 LLM/场景生成模块、Edwin 的真实 STT/TTS/Avatar 模块尚未替换当前 Demo 假模块。
- Holodeck/360 全景图后端仍保持解耦接入，Unity 客户端只消费 JSON、资源 key、图片路径或 URL。

## English Guide

SceneTalkVR is a PICO/VR English scenario practice project. The current architecture keeps the Unity client as the main VR runtime while AI and scene-generation modules stay decoupled on the service side. `Client` owns VR interaction, UI, orchestration, and PICO packaging. `Holodeck`, LLM, STT, TTS, Avatar, and scene-generation modules are connected through lightweight interfaces.

### Repository Layout

- `Client`: Unity 6 client project, currently using Unity `6000.3.16f1`.
- `Client/Assets/SceneTalkVR`: Vitor's client workflow, demo modules, editor tools, and local documentation.
- `Client/Packages/com.unity.xr.picoxr`: PICO Unity Integration SDK committed as an embedded package.
- `Holodeck`: Independent scene/generation module, not embedded directly into the Unity client.
- `documents`: Planning notes, meeting notes, and task breakdowns.

### Requirements

- Unity `6000.3.16f1`
- Unity Hub modules: `Android Build Support`, `Android SDK & NDK Tools`, `OpenJDK`
- Packages already wired in: Input System, Unity UI, XR Interaction Toolkit, OpenXR Plugin, PICO Unity Integration SDK
- For device testing: PICO 4 with Developer Mode and USB debugging enabled

### Quick Start

1. Clone the repository and open `Client` with Unity `6000.3.16f1`.
2. Wait for Unity to finish package resolve, script compilation, and asset import.
3. Open `Assets/Scenes/SampleScene.unity`.
4. Run `SceneTalkVR/Setup/Apply Recommended Project Settings`. If Unity recompiles, wait until it finishes and run the same menu once more.
5. Run `SceneTalkVR/Setup/Rebuild Full Demo Rig (Voice Gateway)`.
6. Press Play and click `Start` on the initial panel. The initial panel shows `Start`, `Settings`, and `Quit`.
7. Run `SceneTalkVR/Diagnostics/Run Preflight Check` to generate the environment report at `Client/Assets/SceneTalkVR/Docs/VitorPreflightReport.md`.

### PICO Controller Input

On device, the demo supports generic PICO/OpenXR controller input:

- Both controllers show lightweight 3D controller proxies and UI rays. Either trigger clicks a world-space UI button when the ray is over it. Recording, stopping, and retrying are controlled only through UI buttons; a trigger does nothing when no button is targeted.
- `A / X`: confirms the UI button currently targeted by the controller ray; it does nothing when no button is targeted.
- `B / Y` or menu: returns to the initial panel.
- Grip or thumbstick click: recenter the world-space panel in front of the current headset pose.
- The initial `Quit` button exits the application; the top-right `Exit` button during the flow returns to the initial panel.

### Demo Flow UI

The current demo UI has four stages:

1. Initial panel: shows `Start`, `Settings`, and `Quit`; no `Exit` button.
2. Settings panel: opened from the initial panel. It controls font size, interface size (50%-125%), and dialogue subtitle visibility. The top-right `Exit` returns to the initial panel.
3. Scene/avatar request confirmation: after `Start`, the client waits for manual recording. `Listen` switches to `End` while recording, then to `Retry` after STT completes, with `Confirm` available for the transcript; top-right `Exit` returns to the initial panel.
4. Loading panel: after `Confirm`, the client shows scene and avatar loading state; top-right `Exit` returns to the initial panel.
5. Dialogue panel: after loading, the central panel disappears and colored subtitles appear at the bottom for player and Avatar lines. If subtitles are hidden in Settings, the bottom area collapses into a compact control bar with only dialogue status and controls; top-right `Exit` returns to the initial panel.

At startup, the client waits for headset tracking and places the demo panel in front of the current view. If the camera has a `TrackedPoseDriver`, the bootstrap no longer writes a fixed world position into the camera transform, which avoids incorrect initial view offsets on device.

### Unity Menu

The `SceneTalkVR` menu is grouped into three areas to avoid confusing one-off setup commands:

- `SceneTalkVR/Setup/Rebuild Full Demo Rig (Voice Gateway)`: Rebuilds the runnable demo, clears old rigs, and configures Main Camera, World Space Canvas, EventSystem, and input.
- `SceneTalkVR/Setup/Apply Recommended Project Settings`: Applies Android/OpenXR/PICO defaults, including package id, IL2CPP, ARM64, Min SDK, PICO OpenXR features, and Build Settings.
- `SceneTalkVR/Diagnostics/Run Preflight Check`: Generates the current environment report without changing the main project setup.
- `SceneTalkVR/Advanced/Clear Generated Demo Rig`: Clears only the generated demo rig and world UI.
- `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`: Use only if OpenXR Validation still reports a missing interaction profile. It is usually unnecessary once the PICO profiles are enabled.

### PICO / Android Build Path

The default path is `OpenXRLoader + PICO OpenXR Features`. Do not enable both OpenXR Loader and the PICO native Loader at the same time, because that can create XR provider conflicts.

Before building, check:

1. `SceneTalkVR/Setup/Apply Recommended Project Settings` has run and the preflight report passes the critical checks.
2. `Project Settings > XR Plug-in Management > Android` uses OpenXR with PICO features/controller profiles enabled.
3. Android Graphics APIs contains only `OpenGLES3`; do not place Vulkan first.
4. Android Scripting Backend is IL2CPP, Target Architecture is ARM64, and Min SDK is API 29 or higher.
5. PICO 4 Developer Mode and USB debugging are enabled.
6. Custom keystore is disabled for local debug builds, so Unity debug signing is used.
7. The Console shows `0` errors. The PICO SDK may produce many warnings under Unity 6 / XRI 3.x; warnings alone do not block the current demo build.

### Git And Keystore

- Do not commit `Client/UserKeystore.keystore`, `*.keystore`, or `*.jks`.
- `.gitignore` already ignores keystores and the root-level `PICO Unity Integration SDK-*` download/extract cache.
- `Client/Packages/com.unity.xr.picoxr` is the embedded PICO SDK actually used by the project and should be committed.
- Local PICO debug builds use Unity debug signing by default, with custom keystore disabled.
- Release builds should enable a private keystore under `Player > Publishing Settings`; signing files and passwords must be shared only through a private channel.

### GitHub Upload Checklist

Commit project source, Unity assets, Package files, and ProjectSettings. Do not commit generated Unity folders, local builds, IDE files, keystores, or SDK download caches. Keep `Client/Assets/Resources/PXR_PlatformSetting.asset` and `.meta` committed because they are PICO project assets and currently contain no app secret.

Before committing, run:

```powershell
git status --short
git status --ignored --short
```

### Teammate Interface Guide

Contracts live in `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs`. The coordinator is `Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs`.

Edwin should implement voice dialogue:

- `ISceneTalkSpeechInput` for STT.
- `ISceneTalkAvatarVoice` for TTS and Avatar playback.
- Use `DemoSpeechInputModule.cs` and `DemoAvatarVoiceModule.cs` as references.
- Attach the new MonoBehaviours to the scene and assign them to `speechInputModule` and `avatarVoiceModule` on `SceneTalkOrchestrator`.

Spring should implement scene generation:

- `ISceneTalkBrain` for LLM response and scene payload generation.
- Return `SpringScenePayload` with task, environment, reply, avatar role, skybox URL, and layout objects.
- Use `DemoBrainModule.cs` as the reference.
- Keep `SceneTalkScenePresenter.cs` if only data output is needed; replace `scenePresenterModule` only if custom scene presentation is required.

All long-running work must use coroutines and report success through `onComplete(...)` or failure through `onError(message)`.

### Current Status

- The Unity Editor demo can display, receive clicks, and complete the fake-data loop.
- PICO/OpenXR controller interaction is wired for controller rays and trigger-confirmed UI clicks; recording, stopping, and retrying use UI buttons, with finish and panel-recentering shortcuts retained.
- Android/OpenXR/PICO baseline settings are in place, and PICO 4 debug builds default to OpenGLES3 to avoid Vulkan startup crashes.
- PICO 4 can launch the demo; controller input, panel placement, and the full presentation path still need device verification.
- Spring's real LLM/scene-generation module and Edwin's real STT/TTS/Avatar module still need to replace the demo adapters.
- Holodeck/360 panorama integration remains decoupled. The Unity client should consume JSON, resource keys, image paths, or URLs instead of embedding the full generation stack.
