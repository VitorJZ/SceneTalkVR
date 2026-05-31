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
5. 运行 `SceneTalkVR/Setup/Rebuild Demo Rig`。
6. 点击 Play，在世界空间 UI 面板上点击 `Start Practice`。
7. 运行 `SceneTalkVR/Diagnostics/Run Preflight Check` 查看当前环境报告：`Client/Assets/SceneTalkVR/Docs/VitorPreflightReport.md`。

### Unity 菜单说明

为了减少混淆，`SceneTalkVR` 菜单只保留三组入口：

- `SceneTalkVR/Setup/Rebuild Demo Rig`：重建可运行 Demo，自动清理旧 Rig，并配置 Main Camera、World Space Canvas、EventSystem 和输入模块。
- `SceneTalkVR/Setup/Apply Recommended Project Settings`：应用 Android/OpenXR/PICO 推荐设置，包括包名、IL2CPP、ARM64、Min SDK、PICO OpenXR features 和 Build Settings。
- `SceneTalkVR/Diagnostics/Run Preflight Check`：生成环境预检报告，不修改主要项目设置。
- `SceneTalkVR/Advanced/Clear Generated Demo Rig`：只清理生成的 Demo Rig 和 World UI。
- `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`：仅在 OpenXR Validation 仍提示缺少 interaction profile 时使用；PICO Profile 正常时通常不需要。

### PICO / Android 打包路线

当前默认只走 `OpenXRLoader + PICO OpenXR Features`。不要同时启用 OpenXR Loader 和 PICO 原生 Loader，否则容易出现 XR provider 冲突。

建议打包前检查：

1. `SceneTalkVR/Setup/Apply Recommended Project Settings` 已运行并通过预检。
2. `Project Settings > XR Plug-in Management > Android` 中使用 OpenXR，并启用 PICO 相关 features/controller profile。
3. Android Scripting Backend 是 IL2CPP，Target Architecture 是 ARM64，Min SDK 不低于 API 29。
4. PICO 4 已开启开发者模式和 USB 调试。
5. Console 右上角 error 为 `0`。PICO SDK 在 Unity 6 / XRI 3.x 下可能有较多 warning，只要没有 error，当前阶段不阻塞 Demo 打包。

### Git 与 keystore

- 不要提交 `Client/UserKeystore.keystore`、`*.keystore` 或 `*.jks`。
- `.gitignore` 已忽略 keystore 和根目录的 `PICO Unity Integration SDK-*` 下载/解压缓存。
- `Client/Packages/com.unity.xr.picoxr` 是项目实际使用的 embedded PICO SDK，需要随工程提交。
- `ProjectSettings.asset` 中只保留 keystore 文件名和 alias 名，不保存 keystore 密码。
- 队友如果需要本地打包，应在 Unity 的 `Player > Publishing Settings` 中生成自己的 keystore，或通过私密渠道获取同一份签名文件。

### 当前开发状态

- Unity Editor 内 Demo 已能显示、点击并跑通假数据闭环。
- Android/OpenXR/PICO 基础配置已完成。
- 仍需完成 PICO 4 真机 Build & Run。
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
5. Run `SceneTalkVR/Setup/Rebuild Demo Rig`.
6. Press Play and click `Start Practice` on the world-space UI panel.
7. Run `SceneTalkVR/Diagnostics/Run Preflight Check` to generate the environment report at `Client/Assets/SceneTalkVR/Docs/VitorPreflightReport.md`.

### Unity Menu

The `SceneTalkVR` menu is grouped into three areas to avoid confusing one-off setup commands:

- `SceneTalkVR/Setup/Rebuild Demo Rig`: Rebuilds the runnable demo, clears old rigs, and configures Main Camera, World Space Canvas, EventSystem, and input.
- `SceneTalkVR/Setup/Apply Recommended Project Settings`: Applies Android/OpenXR/PICO defaults, including package id, IL2CPP, ARM64, Min SDK, PICO OpenXR features, and Build Settings.
- `SceneTalkVR/Diagnostics/Run Preflight Check`: Generates the current environment report without changing the main project setup.
- `SceneTalkVR/Advanced/Clear Generated Demo Rig`: Clears only the generated demo rig and world UI.
- `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`: Use only if OpenXR Validation still reports a missing interaction profile. It is usually unnecessary once the PICO profiles are enabled.

### PICO / Android Build Path

The default path is `OpenXRLoader + PICO OpenXR Features`. Do not enable both OpenXR Loader and the PICO native Loader at the same time, because that can create XR provider conflicts.

Before building, check:

1. `SceneTalkVR/Setup/Apply Recommended Project Settings` has run and the preflight report passes the critical checks.
2. `Project Settings > XR Plug-in Management > Android` uses OpenXR with PICO features/controller profiles enabled.
3. Android Scripting Backend is IL2CPP, Target Architecture is ARM64, and Min SDK is API 29 or higher.
4. PICO 4 Developer Mode and USB debugging are enabled.
5. The Console shows `0` errors. The PICO SDK may produce many warnings under Unity 6 / XRI 3.x; warnings alone do not block the current demo build.

### Git And Keystore

- Do not commit `Client/UserKeystore.keystore`, `*.keystore`, or `*.jks`.
- `.gitignore` already ignores keystores and the root-level `PICO Unity Integration SDK-*` download/extract cache.
- `Client/Packages/com.unity.xr.picoxr` is the embedded PICO SDK actually used by the project and should be committed.
- `ProjectSettings.asset` keeps only the keystore filename and alias name. It does not store keystore passwords.
- Teammates who need local builds should generate their own keystore in Unity under `Player > Publishing Settings`, or receive the shared signing file through a private channel.

### Current Status

- The Unity Editor demo can display, receive clicks, and complete the fake-data loop.
- Android/OpenXR/PICO baseline settings are in place.
- PICO 4 device Build & Run is still pending.
- Spring's real LLM/scene-generation module and Edwin's real STT/TTS/Avatar module still need to replace the demo adapters.
- Holodeck/360 panorama integration remains decoupled. The Unity client should consume JSON, resource keys, image paths, or URLs instead of embedding the full generation stack.
