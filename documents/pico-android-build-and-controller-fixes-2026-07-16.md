# PICO Android 打包与手柄修复记录（2026-07-16）

本文记录 SceneTalkVR 在 Unity `6000.3.16f1`、PICO Unity Integration SDK `3.4.0`、OpenXR Plugin `1.16.1` 和 PICO 4 Enterprise 真机上的 Android 打包、OpenXR 手柄输入及手柄位置修复。内容用于后续复现、回归测试和团队交接。

## 最终结果

- Android Gradle 构建成功，PICO 自定义 OpenXR Loader 与 Unity 默认 Loader 不再发生重复打包冲突。
- APK 包含 PICO 所需的 `PxrPlatform.aar` 和 VR Manifest 元数据。
- PICO 能将应用识别为 VR 应用。
- Android OpenXR 已注册 33 个可用 Feature，实际启用 4 个必要 PICO Feature。
- 真机日志由 `Features requested to be enabled: (0)` 修复为 `(4)`。
- 真机日志由 `No action sets registered` 修复为 `Action Sets (2)`，两套控制器 Action Set 各包含 18 个动作。
- 左右 PICO 4 控制器均能被运行时识别。
- 手柄 Tracking Space 局部姿态会固定转换为世界姿态，避免手柄模型离 XR 相机过远。
- Voice Gateway 与 LLM Gateway 在验证时均返回 HTTP 200。

最终验证 APK：

```text
E:\Project\Unity\SceneTalkVR\Builds\SceneTalkVR-controller-pose-fix-debug.apk
Size: 175508059 bytes
SHA256: 6E6CF33C3731B8091A3E1AD3F95D082632450BE7417FB2880A2D16D4396477D6
```

## 1. Android Gradle 构建失败

### 1.1 现象

Unity 调用 Gradle `assembleDebug` 时失败。导出的 Gradle 工程同时包含：

- Unity OpenXR 默认 Loader：`openxr_loader.aar`
- PICO 自定义 Loader：`LoaderForUnitySDK_*.aar`

两者都会向 APK 写入 OpenXR Loader 原生库，造成重复的 `libopenxr_loader.so` 打包冲突。项目选择的是 PICO 自定义 OpenXR Loader，因此不能同时保留 Unity 默认 Loader 依赖。

后续检查还发现 PICO 自定义 Loader 路线需要 `PxrPlatform.aar`，并且最终 Manifest 必须包含 PICO VR 应用元数据。

### 1.2 持久化修复

修复位于：

```text
Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkAndroidBuildPostprocessor.cs
```

`SceneTalkAndroidBuildPostprocessor` 在 Unity 生成 Gradle 工程后执行以下操作：

1. 检测 `LoaderForUnitySDK*.aar` 和 `openxr_loader.aar` 是否同时存在。
2. PICO 自定义 Loader 存在时，从 `unityLibrary/build.gradle` 删除 Unity 默认 `openxr_loader` 的 AAR dependency。
3. 从下列位置复制 PICO 平台库：

   ```text
   Client/Packages/com.unity.xr.picoxr/Runtime/Android/PxrPlatform.aar
   ```

4. 将 `PxrPlatform.aar` 放入生成的 `unityLibrary/libs`，并加入：

   ```gradle
   implementation(name: 'PxrPlatform', ext:'aar')
   ```

5. 向 Android Manifest 补充 PICO VR 元数据：

   ```text
   pvr.app.type=vr
   use.pxr.sdk=2
   pxr.sdk.version_code=5150
   pvr.sdk.version=Unity OpenXR 3.4.0
   controller=1
   ```

6. 保留局域网 HTTP 调试所需设置：

   ```text
   android:usesCleartextTraffic=true
   android:networkSecurityConfig=@xml/scenetalk_network_security_config
   ```

此修复在每次 Unity 重新生成 Gradle 工程时自动执行，不需要手工编辑 `Library/Bee/Android/.../Gradle` 中的临时文件。

## 2. PICO 手柄不显示且不能操作

### 2.1 真机证据

修复前 OpenXR 运行日志：

```text
OpenXR session -> FOCUSED
Features requested to be enabled: (0)
Action Sets: No action sets registered
```

这说明 HMD 和 OpenXR Session 已正常启动，但没有控制器 Interaction Profile，因此 Unity 无法创建左右手柄 `InputDevice`。`SceneTalkInteractionBootstrap` 每帧查询不到 Controller 后，会隐藏手柄代理和射线。

### 2.2 根因

`OpenXRPackageSettings.asset` 中已经存在并标记为启用的 PICO Feature 子资产，但它们最初没有注册到 Android `OpenXRSettings.features` 数组，属于“启用但未注册”的孤立 Feature：

| Feature | Feature ID |
| --- | --- |
| PICO XR Support | `com.unity.openxr.feature.pico` |
| PICO OpenXR Features | `com.unity.openxr.pico.features` |
| PICO4 Touch Controller Profile | `com.unity.openxr.feature.input.PICO4touch` |
| PICO4 Ultra Touch Controller Profile | `com.unity.openxr.feature.input.PICO4Ultratouch` |

此外，PICO 控制器源码使用 `#if PICO_OPENXR_SDK`。如果首次设置宏后立即调用 `FeatureHelpers.RefreshFeatures(Android)`，Unity 还没有完成重新编译，刷新过程看不到 PICO 类型，反而会再次移除 PICO Feature 引用。

### 2.3 修复

涉及文件：

```text
Client/ProjectSettings/ProjectSettings.asset
Client/Assets/XR/Settings/OpenXRPackageSettings.asset
Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkPreflightMenu.cs
```

修复内容：

- Android scripting define 加入 `PICO_OPENXR_SDK`。
- `OpenXRPackageSettings` 和相关 XR Settings 加入 `preloadedAssets`。
- Android OpenXR Settings 使用 PICO 自定义 Loader：

  ```text
  customLoaderName: LoaderForUnitySDK_1_1_0
  ```

- Android `features` 数组注册 PICO Feature；当前共注册 33 个 Feature，其中上述 4 个必要 Feature 为启用状态。
- `Apply Recommended Project Settings` 改为两阶段执行：

  1. 如果本次刚加入 `PICO_OPENXR_SDK`，先保存并返回，等待 Unity 重新编译。
  2. 重新编译完成后再次运行菜单，调用官方 `FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android)`，再启用 4 个 PICO Feature。

- 预检增加“必要 PICO Feature 已注册到 Android OpenXR Settings”的独立检查，避免只检查子资产的 `enabled` 状态。

正确操作顺序：

1. 在 Unity 中运行 `SceneTalkVR/Setup/Apply Recommended Project Settings`。
2. 如果 Console 提示刚加入 `PICO_OPENXR_SDK`，等待脚本编译完成。
3. 再运行一次同一菜单。
4. 运行 `SceneTalkVR/Diagnostics/Run Preflight Check`。
5. 确认 OpenXR 和 PICO 项全部通过后再构建 APK。

### 2.4 修复后日志

```text
OpenXRSession::HandleSessionStateChangedEvent: ... -> XR_SESSION_STATE_FOCUSED
Features requested to be enabled: (4)
PICO XR Support
PICO OpenXR Features
PICO4 Touch Controller Profile
PICO4 Ultra Touch Controller Profile
Action Sets (2):
  pico4touchcontroller: ActionCount=18
  pico4ultracontroller: ActionCount=18
```

PICO 4 Enterprise 运行时还会报告当前交互 Profile：

```text
/interaction_profiles/bytedance/pico4_controller
```

PICO 4 Enterprise 不支持 Ultra 对应的 `pico4s_controller` 路径时，可能出现一次非阻塞的 `XR_ERROR_PATH_UNSUPPORTED`。普通 PICO 4 Profile、Action Set 和左右手柄输入仍然正常。

## 3. 手柄模型离相机过远

### 3.1 场景坐标结构

真机使用的 XR 相机层级为：

```text
XR Origin (VR)              localPosition = (0, 0, 0)
└─ Camera Offset            localPosition = (0, 1.6, 0)
   └─ Main Camera           localPosition = (0, 0, 0)
```

`SceneTalkInteractionBootstrap.interactionCamera` 指向该 XR Main Camera。运行时生成的手柄代理和射线对象位于场景根节点，使用世界坐标。

PICO/OpenXR 的：

```csharp
CommonUsages.devicePosition
CommonUsages.deviceRotation
```

返回 Tracking Space 局部姿态，不能直接作为根节点对象的世界姿态。

### 3.2 原有问题

旧逻辑同时计算原始姿态和转换后姿态，再用“哪个位置离相机更近”猜测应该采用哪一个。这个启发式判断在 XR Origin、Camera Offset 或相机追踪状态变化时可能误选原始局部坐标，导致手柄看起来距离用户很远。

如果关闭 `transformControllerPoseFromTrackingSpace`，问题会更加明确：Tracking Space 局部坐标被直接写入世界坐标。

### 3.3 修复

修复文件：

```text
Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs
Client/Assets/Scenes/SampleScene.unity
```

场景保持：

```text
transformControllerPoseFromTrackingSpace: 1
```

手柄姿态固定通过 Camera Offset 转换，不再使用距离猜测：

```csharp
position = trackingSpace.TransformPoint(position);
rotation = trackingSpace.rotation * rotation;
```

这样输入姿态与 XR 相机处于同一世界坐标系，手柄代理会跟随真实手柄出现在用户附近。

## 4. 真机 URL 与 Key

当前 PICO 通过 PC 热点访问两个 Gateway：

```text
Voice Gateway: http://192.168.137.1:8787
LLM Gateway:   http://192.168.137.1:8788/api/llm/chat/completions
```

相关 Unity 配置：

```text
Client/Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset
Client/Assets/Scenes/SampleScene.unity
```

注意：

- PICO 真机不能使用 `127.0.0.1` 或 `localhost` 访问 PC 后台。
- Unity Scene 和 RuntimeConfig 中的 LLM/API Key 必须保持为空。
- 云服务 Key 只保存在 PC Gateway 的环境变量或本地配置中，不得写入 Scene、Asset、`.meta` 或 APK。
- 如果 PC 热点 IP 改变，需要同步修改 Unity URL 并重新构建 APK。

健康检查：

```powershell
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:8787/health
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:8788/health
```

本次验证结果均为 HTTP 200。

## 5. 构建、安装与日志验证

### 5.1 构建前检查

```powershell
rg -n "PICO_OPENXR_SDK|customLoaderName|PICO4ControllerProfile|PICOFeature" `
  Client/ProjectSettings/ProjectSettings.asset `
  Client/Assets/XR/Settings/OpenXRPackageSettings.asset
```

Unity 预检报告应确认：

- Android XR Loader 使用 OpenXR。
- XR 自动初始化和自动运行已启用。
- 必要 PICO Feature 已注册。
- PICO XR Support、PICO OpenXR Features 和 PICO 4 Controller Profile 已启用。
- Android 使用 ARM64、IL2CPP、最低 API 29 和 OpenGLES3。

### 5.2 安装和启动

```powershell
$adb = "E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"

& $adb devices -l
& $adb install -r "E:\Project\Unity\SceneTalkVR\Builds\SceneTalkVR-controller-pose-fix-debug.apk"
& $adb shell am force-stop com.scenetalkvr.demo
& $adb shell am start -n com.scenetalkvr.demo/com.unity3d.player.UnityPlayerActivity
```

### 5.3 OpenXR 和控制器日志

```powershell
$appPid = (& $adb shell pidof com.scenetalkvr.demo).Trim()

& $adb logcat -d -v time --pid=$appPid |
  Select-String "Features requested to be enabled|Action Sets|PICO4 Touch Controller Profile|XR_SESSION_STATE_FOCUSED"
```

期望至少看到：

```text
Features requested to be enabled: (4)
Action Sets (2)
pico4touchcontroller: ActionCount=18
XR_SESSION_STATE_FOCUSED
```

如果应用被 PICO 透视界面覆盖，先戴上头显并唤醒手柄，再重新启动应用。未佩戴头显时，控制器因 `hmd_sleep` 断开属于设备正常省电行为。

## 6. 回归测试清单

- [ ] APK 可以成功安装并作为 VR 应用启动。
- [ ] 真机日志不再出现 `Features requested to be enabled: (0)`。
- [ ] 真机日志不再出现 `No action sets registered`。
- [ ] 左右手柄代理均显示在真实手柄附近。
- [ ] 左右射线方向与真实手柄朝向一致。
- [ ] 左右扳机均能点击世界空间 UI。
- [ ] 射线未命中按钮时，按下或松开扳机不会改变录音状态；指向录音按钮时可正常点击。
- [ ] 握持键或摇杆按下可以重置 UI 面板。
- [ ] Voice Gateway 和 LLM Gateway 均可从 PICO 所在局域网访问。
- [ ] APK 中没有 Unity 客户端 API Key。

## 7. 关键文件

```text
Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkAndroidBuildPostprocessor.cs
Client/Assets/SceneTalkVR/Scripts/Editor/SceneTalkPreflightMenu.cs
Client/Assets/SceneTalkVR/Scripts/Runtime/SceneTalkInteractionBootstrap.cs
Client/Assets/XR/Settings/OpenXRPackageSettings.asset
Client/ProjectSettings/ProjectSettings.asset
Client/Assets/Scenes/SampleScene.unity
Client/Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset
```

