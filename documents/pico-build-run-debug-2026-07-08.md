# PICO Build & Run Debug 记录

日期：2026-07-08

## 问题现象

PC Editor 中游戏可以运行；Build & Run 到 PICO 真机后，先后遇到两类问题：

- 早期包无法正常进入 VR 游戏。
- 启动问题修复后，真机画面能运行，但 HMD 视角固定，没有 3DOF/6DOF；左右手柄位置也不正确。

## 启动问题结论

这部分主要不是性能问题，而是 XR 初始化、Android 权限和本地网络配置叠加。

已修复：

- `Client/Assets/XR/XRGeneralSettingsPerBuildTarget.asset`
  - Android XR loader 改为 OpenXR loader。
  - `m_AutomaticLoading` 改为 `1`。
  - `m_AutomaticRunning` 改为 `1`。
- `Client/Assets/SceneTalkVR/Voice/Scripts/MicrophoneRecorder.cs`
  - Android 真机录音前请求 `Permission.Microphone`。
  - 未授权时返回明确错误。
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkAndroidBuildPostprocessor.cs`
  - Gradle 工程生成后写入 `usesCleartextTraffic=true`。
  - 生成 network security config，允许本地 HTTP 调试。
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkPreflightMenu.cs`
  - 增加 Android XR automatic loading/running 检查。

本地网关注意点：

- PICO 真机访问 `127.0.0.1` 时访问的是头显自身，不是 PC。
- 如果语音或场景网关跑在 PC 上，应使用 PC 的局域网 IP，并确保服务实际监听该地址和端口。

## Tracking 问题结论

当前 Build Settings 中真正启用的是：

- `Assets/first_save.unity`

而 `Assets/Scenes/SampleScene.unity` 是 disabled。`SampleScene` 中有 `TrackedPoseDriver`，但实际打包的 `first_save.unity` 里 Main Camera 没有 HMD pose driver，所以真机上相机会保持固定变换。

已修复：

- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs`
  - `ConfigureCamera` 会在 Android 真机运行时确保 Main Camera 有 Input System `TrackedPoseDriver`。
  - 绑定 `<XRHMD>/centerEyePosition`、`<XRHMD>/centerEyeRotation`、`<XRHMD>/trackingState`。
  - 保留 handheld AR position/rotation fallback。
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkDemoSetupMenu.cs`
  - 重建 Demo Rig 时也会补齐同样的 `TrackedPoseDriver`。
- `Client/Assets/first_save.unity`
- `Client/Assets/Scenes/SampleScene.unity`
  - `transformControllerPoseFromTrackingSpace` 从 `1` 改为 `0`，避免控制器 pose 默认被二次转换。

## 当前已改文件清单

- `Client/Assets/XR/XRGeneralSettingsPerBuildTarget.asset`
- `Client/Assets/SceneTalkVR/Voice/Scripts/MicrophoneRecorder.cs`
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkAndroidBuildPostprocessor.cs`
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkPreflightMenu.cs`
- `Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs`
- `Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkDemoSetupMenu.cs`
- `Client/Assets/first_save.unity`
- `Client/Assets/Scenes/SampleScene.unity`

## 真机复测清单

1. 重新 Build & Run 到 PICO。
2. 戴上头显左右转头，确认画面跟随 HMD rotation。
3. 前后/左右移动头显，确认画面有 6DOF position 变化。
4. 检查左右手柄模型或射线是否靠近真实手柄位置。
5. 用任一扳机点击世界空间 UI，确认射线点击仍可用。
6. 触发录音流程，确认 Android 麦克风授权弹窗和授权后录音流程正常。
7. 如果使用 PC 本地服务，确认 PICO 配置的网关地址不是 `127.0.0.1`。

## 当前判断

真机固定视角的直接原因是实际打包场景缺少 HMD `TrackedPoseDriver`。手柄错位主要与相机没有跟随 HMD、以及控制器 pose 默认二次转换有关。修复后，PICO 侧应由 OpenXR 启动 XR，Main Camera 由 HMD pose 驱动，控制器射线直接使用 Unity XR 返回的设备 pose。
