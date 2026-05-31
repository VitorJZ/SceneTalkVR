# Vitor 客户端落地说明

本目录是 Vitor 组长任务在 Unity 客户端侧的落地骨架。它先用可运行的假数据打通流程，后续 Spring 和 Edwin 只需要替换对应模块实现即可。

## 已实现内容

- 客户端主流程状态机：`SceneTalkOrchestrator`。
- Spring/Edwin 联调接口：
  - `ISceneTalkSpeechInput`
  - `ISceneTalkBrain`
  - `ISceneTalkScenePresenter`
  - `ISceneTalkAvatarVoice`
- 假数据模块：模拟 STT、LLM/场景生成和 Avatar 语音回复。
- 场景呈现器：消费 Spring 风格的场景数据，并安全实例化本地预制体。
- Unity Demo 菜单入口：`SceneTalkVR/Setup/Rebuild Demo Rig`。
- 推荐项目配置入口：`SceneTalkVR/Setup/Apply Recommended Project Settings`。
- 构建预检入口：`SceneTalkVR/Diagnostics/Run Preflight Check`。
- 高级清理入口：`SceneTalkVR/Advanced/Clear Generated Demo Rig`。
- OpenXR 兜底控制器入口：`SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`。

## Unity 使用步骤

1. 使用 Unity `6000.3.16f1` 打开 `Client`。
2. 打开 `Assets/Scenes/SampleScene.unity`。
3. 在 Unity 顶部菜单运行 `SceneTalkVR/Setup/Apply Recommended Project Settings`；如果 Unity 重新编译，完成后再运行一次。
4. 运行 `SceneTalkVR/Setup/Rebuild Demo Rig`。
5. 点击 Play，在世界空间面板里点击 `Start Practice`。

当前 Demo 先使用假数据，目的是保证 Unity 客户端底座稳定。Spring 和 Edwin 后续可以把假模块替换为真实的 LLM、STT、TTS、Avatar 和 Holodeck/全景图服务。

如果已经创建过旧版 Demo Rig，但界面太小、不能点击，或者场景里出现多个 `SceneTalkVR Demo Rig`，运行 `SceneTalkVR/Setup/Rebuild Demo Rig`。它会先清理旧的 SceneTalkVR Demo Rig 和 World UI，再重建唯一一套可点击面板，并把 Main Camera、Canvas 交互相机、`EventSystem + InputSystemUIInputModule` 一起配置好。

如果只想清空旧生成物，不立刻重建，运行 `SceneTalkVR/Advanced/Clear Generated Demo Rig`。

## Vitor 预检与打包准备

运行 `SceneTalkVR/Diagnostics/Run Preflight Check` 会生成：

`Assets/SceneTalkVR/Docs/VitorPreflightReport.md`

报告会检查当前场景、Demo Rig、EventSystem、Canvas、Input System、Build Settings、Android 参数，以及 XR Interaction Toolkit / OpenXR / PICO SDK 的缺失状态。

运行 `SceneTalkVR/Setup/Apply Recommended Project Settings` 会自动完成以下可自动化配置：

- 如果当前 Unity 安装包含 Android Build Support，则切换构建目标到 Android。
- 设置包名为 `com.scenetalkvr.demo`。
- 设置 Android Scripting Backend 为 IL2CPP。
- 设置 Android Target Architecture 为 ARM64。
- 设置 Android Min SDK 为 29，以满足 PICO SDK 的 Android 10.0 要求。
- 将 `Assets/Scenes/SampleScene.unity` 加入 Build Settings。

如果预检报告显示 Android Build Support 缺失，需要先在 Unity Hub 为 Unity `6000.3.16f1` 安装 `Android Build Support`、`Android SDK & NDK Tools` 和 `OpenJDK`，再重新运行该菜单。缺失 Android 模块时，脚本会保留已写入的 Android 默认参数，但不会强行切换 Build Target。

XR Interaction Toolkit、OpenXR Plugin 和 PICO Unity Integration SDK 已接入。当前默认路线是 `OpenXRLoader + PICO OpenXR Features`，而不是同时启用 OpenXR 和 PICO 原生 Loader。PICO 原生 Loader 与 OpenXR Loader 是互斥路线，二者不要同时启用。

如果 OpenXR Validation 报告 `At least one interaction profile must be added`，先运行 `SceneTalkVR/Setup/Apply Recommended Project Settings`。如果仍未消失，再运行 `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`，或在 Android OpenXR 页手动添加 `Khronos Simple Controller Profile`。

导入 PICO SDK 后，如果 Project Validation 或预检报告显示 PICO OpenXR 功能缺失，运行 `SceneTalkVR/Setup/Apply Recommended Project Settings`。该菜单会设置 Android 的 `PICO_OPENXR_SDK` 宏；Unity 重新编译后，再运行一次同一菜单，用于启用 `PICO XR Support`、`PICO OpenXR Features` 和 `PICO4 Touch Controller Profile`。

PICO SDK 在 Unity 6 / XR Interaction Toolkit 3.x 下会输出较多 `CS0618`、`CS0660`、`CS0108` 等 warning，这些来自 SDK 包内部的过时 API 或类型声明提示。只要 Console 右上角保持 `0` 个 error，它们不阻塞当前 Demo 运行和下一步打包验证。

## 联调规则

- `Client` 是唯一 Unity/PICO 客户端工程。
- `Holodeck` 保持在 Unity 客户端之外，通过 JSON、图片 URL 或本地资源 key 接入。
- STT、LLM、场景生成和 TTS 等耗时任务不能阻塞 Unity 主线程。
- 360 全景图始终保留为场景保底方案。
- 为了 PICO 性能，动态生成物体必须限制数量和距离。

## 包管理说明

当前项目已经接入 XR Interaction Toolkit、OpenXR Plugin 和 PICO Unity Integration SDK。PICO SDK 使用 embedded package 方式随工程提交，路径为 `Client/Packages/com.unity.xr.picoxr`，`manifest.json` 中对应依赖为 `file:com.unity.xr.picoxr`。不要提交根目录下手动解压出来的 `PICO Unity Integration SDK-*` 缓存目录。

## Git 与 keystore

- `Client/UserKeystore.keystore` 是 Android/PICO 签名私钥文件，只保留在本机，不提交 Git。
- `.gitignore` 已忽略 `*.keystore` 和 `*.jks`，避免误传签名文件。
- `ProjectSettings.asset` 中只保留 keystore 文件名和 alias 名，不保存 keystore 密码。
- 队友拉取工程后，如果需要本地打包，需要在 Unity 的 `Player > Publishing Settings` 中重新生成自己的 keystore，或通过私密渠道获取同一份 keystore。
