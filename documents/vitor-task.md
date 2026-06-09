# Vitor 组长任务规划：SceneTalk VR 系统架构与 VR 底层交互

## 任务定位

Vitor 作为组长，负责 SceneTalk VR 的系统架构、Unity/PICO 客户端底座、模块调度、VR 内交互 UI、设备联调与最终打包。当前仓库中 Unity 客户端位于 `Client`，使用 Unity `6000.3.16f1`；Holodeck 独立位于 `Holodeck`。因此 Vitor 的核心任务不是直接实现所有 AI 能力，而是搭建稳定的 VR 客户端容器，把 Spring 的 LLM/场景生成模块和 Edwin 的语音/Avatar 模块串成可演示的闭环。

默认技术路线采用“Unity 客户端为主，Holodeck/AI 生成模块服务端解耦”。客户端负责 VR 交互、状态流转、结果呈现与 PICO 打包；高风险的 Holodeck、LLM、TTS/STT 等模块通过接口接入。保底场景方案采用“360 全景图 + 少量本地 3D 预设物体”，避免在 PICO 4 上动态加载过重的 3D 资产。

## 核心目标

1. 保证 `Client` Unity 6 工程可运行，并能面向 Android/PICO 进行构建验证。
2. 补齐 PICO SDK、XR Interaction Toolkit、基础输入系统与 VR UI 交互能力。
3. 设计客户端主流程状态机，串联用户录音、STT、LLM、场景生成、Avatar/TTS 播放等步骤。
4. 定义 Unity 客户端与 Spring、Edwin 模块之间的轻量数据接口，支持先用假数据联调，再替换为真实模块。
5. 完成 PICO 设备联调、性能检查、演示流程打磨和答辩技术路线说明。

## 当前完成进度（2026-06-06）

### 已完成

- 已在 `Client/Assets/SceneTalkVR` 建立 Vitor 客户端侧工程骨架。
- 已实现客户端主流程状态机 `SceneTalkOrchestrator`，覆盖 `Idle`、`Listening`、`Processing`、`SceneReady`、`AvatarSpeaking`、`Finished`、`Error` 等状态。
- 已定义 Spring/Edwin 对接接口：`ISceneTalkSpeechInput`、`ISceneTalkBrain`、`ISceneTalkScenePresenter`、`ISceneTalkAvatarVoice`。
- 已实现假数据联调模块：模拟 STT 输入、LLM/场景生成输出、Avatar 语音回复。
- 已实现 `SceneTalkScenePresenter`，可消费 Spring 风格的场景数据，并限制动态生成物体数量与距离。
- 已实现 Unity 编辑器菜单 `SceneTalkVR/Setup/Rebuild Demo Rig`，可一键生成或重建 Demo Rig。
- 已修复 Demo Rig 重复生成问题：重建 Demo Rig 时会先清理旧 Rig，再重建唯一一套。
- 已修复 UI 太小、无法点击、左右镜像问题：已配置 Main Camera、World Space Canvas、EventSystem、InputSystem UI 输入模块，并修正 Canvas 朝向。
- 已新增 `SceneTalkVR/Advanced/Clear Generated Demo Rig`，用于清理生成物。
- 已新增 `SceneTalkVR/Setup/Apply Recommended Project Settings`，用于自动配置 Android/PICO 推荐默认项：包名、IL2CPP、ARM64、最低 SDK、OpenXR/PICO 功能与主场景 Build Settings。
- 已新增 `SceneTalkVR/Diagnostics/Run Preflight Check`，用于生成 Vitor 预检报告，检查 Demo Rig、EventSystem、Build Settings、Android 参数与关键包缺失情况。
- 已检查 `Configure Android Build Defaults` 运行结果：Android Build Support 已安装，Active Build Target 已切换为 Android，包名、IL2CPP、ARM64、最低 SDK 和主场景 Build Settings 均已通过预检。
- 已将 Android Min SDK 提升到 API 29，满足 PICO SDK 对 Android 10.0 的 Required Validation。
- 已安装 XR Interaction Toolkit 与 OpenXR Plugin，并为 Android OpenXR 启用 `Khronos Simple Controller Profile`，用于消除“至少需要一个 interaction profile”的验证警告。
- 已将 OpenXR 兜底交互 Profile 移至 `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`，仅在 PICO Profile 未生效时使用。
- 已导入 PICO Unity Integration SDK `3.4.0`，当前包路径为 `Client/Packages/com.unity.xr.picoxr`。
- 已将 PICO OpenXR 默认配置合并到 `SceneTalkVR/Setup/Apply Recommended Project Settings`；该步骤需要 Unity 重新编译后再运行一次菜单完成启用。
- 已完成 PICO OpenXR 基础验证：`PICO_OPENXR_SDK`、`OpenXRLoader`、`PICO XR Support`、`PICO OpenXR Features`、`PICO4 Touch Controller Profile` 均已通过预检。
- 已完成 PICO/OpenXR Project Validation 基础配置：Android 保持 OpenXR 单一路线，Min SDK API 29、ARM64、IL2CPP、OpenGLES3、Run In Background 与 URP Quality 配置已落盘；本地调试默认使用 Unity debug signing，keystore 私钥文件不提交 Git。
- 已接入 PICO/OpenXR 手柄交互：左右手柄显示轻量 3D 手柄代理和射线，射线命中世界空间 UI 时由扳机确认点击；`A/X` 保留为开始/重试快捷键，`B/Y` 或菜单结束，握持键或摇杆按下重置 UI 面板到头显正前方；UI 中新增 `Quit` 按钮用于退出应用。
- 已修正真机初始视角逻辑：带 `TrackedPoseDriver` 的 XR 相机不再被 Demo bootstrap 强制设置世界坐标，UI 面板改为启动后相对当前头显位置重居中。
- 当前 Unity Editor 内 Demo 已能正常显示、点击并运行假数据闭环。

### 仍未完成

- PICO 4 真机已能启动 Demo；仍需回归验证手柄射线方向、扳机点击命中率和完整演示路径。
- PICO SDK 产生的 100+ 条 warning 目前来自 SDK 包内部的 Unity 6 / XRI 3.x 兼容性提示，只要 Console 没有 error，暂不阻塞后续真机打包。
- Spring 的真实 LLM/场景生成模块尚未替换假数据模块。
- Edwin 的真实 STT/TTS/Avatar/口型同步模块尚未替换假数据模块。
- 360 全景图 API 或 Holodeck 后端尚未接入，当前只保留了客户端接口与保底场景承载结构。

### 需要人工或下载的步骤

- 将 PICO 4 开启开发者模式和 USB 调试，连接电脑后执行第一次 Android Build & Run。
- 在 `Project Settings > XR Plug-in Management` 中启用 PICO/OpenXR 对应 provider。
- 将 PICO 4 开启开发者模式并连接电脑，完成 Android Build & Run。
- 将 Spring/Edwin 的真实模块挂到 `SceneTalkOrchestrator` 对应字段上，替换 Demo 模块。

## 阶段计划

### 第 1 阶段：准备期 - 客户端环境和 XR/PICO 基础验证

- [x] 确认 `Client` 工程使用 Unity `6000.3.16f1` 打开并可运行 Demo。
- [x] 安装 Unity Android Build Support、Android SDK & NDK Tools 和 OpenJDK，确认 AndroidPlayer 可用。
- [x] 安装 XR Interaction Toolkit 与 OpenXR Plugin，启用基础 Android OpenXR 控制器 Profile。
- [x] 接入 PICO Unity Integration SDK。
- [x] 验证 PICO OpenXR 功能集和 Android XR 设置。
- [x] 验证 PICO/OpenXR Project Validation 基础 Required 项。
- [x] 验证 PICO 真机权限、USB 调试和设备运行要求。
- [ ] 完成 XR Rig、左右手控制器、射线交互和 PICO 真机 UI 点击验证。
- [x] 建立最小 VR/桌面可运行场景：玩家视角、基础灯光、测试 UI 面板、场景容器。
- [x] 提供预检工具输出环境验证记录：Unity 版本、Package 状态、Build Settings、Android 参数、缺失项。

### 第 2 阶段：原型期 - 主状态机、UI 和假数据联调

- [x] 实现客户端主流程状态机，至少包含 `Idle`、`Listening`、`Processing`、`SceneReady`、`AvatarSpeaking`、`Finished`、`Error` 状态。
- [x] 搭建基础 UI：开始练习、重试、结束练习、状态文本、转写文本、Avatar 回复、错误提示。
- [x] 用假数据模拟 Spring 模块输出，驱动场景承载逻辑和演示回复。
- [x] 用假数据模拟 Edwin 模块输出，驱动 Avatar 语音回复流程。
- [x] 为每个模块接入点保留异步接口，避免真实网络请求接入后阻塞 Unity 主线程。

### 第 3 阶段：联调期 - 真模块接入、PICO 打包和性能优化

- [ ] 接入 Spring 的 LLM/场景生成结果，支持从自然语言指令生成环境类型、任务类型、Avatar 人设和场景资源引用。
- [ ] 接入 Edwin 的 STT/TTS/Avatar 输出，支持从用户语音转文本，再播放 Avatar 回复音频。
- [ ] 完成 Unity 客户端与 Holodeck 或全景图服务的解耦联调：客户端只消费 JSON 布局、图片路径或资源 URL。
- [ ] 在 PICO 设备上做端到端 Demo 测试，记录启动时间、关键交互延迟、帧率和异常情况。
- [x] 对移动端性能做保底约束：限制动态物体数量、限制生成距离、保留 360 全景图保底策略。

### 第 4 阶段：答辩打磨期 - 演示稳定性和技术表达

- 准备一条稳定演示路径，例如“咖啡店点单”或“机场问路”，保证现场可重复演示。
- 准备离线兜底数据：固定场景图、固定 Avatar、固定音频、固定 LLM 回复，防止网络波动影响答辩。
- 梳理系统架构图，突出 Vitor 负责的客户端底座、模块调度和前后端解耦。
- 总结风险控制矩阵：版本冲突、Holodeck 性能、网络延迟、PICO 打包问题及对应 B 计划。
- 配合 Spring、Edwin 完成 PPT 分工页和技术路线页，保证答辩叙述口径一致。

## 具体任务清单

### 1. Unity/PICO 客户端底座

- 使用 `Client` 作为唯一 Unity 客户端工程，不将 Holodeck 强行塞入同一个 Unity 工程。
- 配置 Android/PICO 构建参数，包括包名、最低 SDK、目标架构、权限和 XR 设置。
- 接入 PICO Unity Integration SDK，验证设备识别、控制器输入、头显追踪和基础运行。
- 接入 XR Interaction Toolkit，完成射线选择、UI 点击、按钮交互和基础手柄输入。
- 建立可复用的 VR 场景根节点结构，例如 `XRRig`、`UIRoot`、`SceneRoot`、`AvatarRoot`、`SystemManager`。

### 2. 主流程状态机

- 建立客户端总控脚本，统一管理练习流程和 UI 状态。
- 推荐状态流：

```text
Idle
  -> Listening
  -> Processing
  -> SceneReady
  -> AvatarSpeaking
  -> Listening
  -> Finished
```

- 每个状态必须有明确进入条件、退出条件和错误处理。
- 网络请求、音频加载、场景替换等耗时操作必须异步执行，并在 UI 中显示等待状态。
- 当 STT、LLM、TTS 或场景生成失败时，进入 `Error` 状态，并允许用户重试或使用离线兜底数据。

### 3. VR UI 与交互

- 实现基础菜单：开始练习、停止录音、重新生成、结束练习。
- 实现状态反馈：正在听、正在生成场景、Avatar 正在思考、Avatar 正在说话、网络/模块错误。
- UI 布局要适合 VR 近距离阅读，避免文字过小、按钮过密或需要精细点击。
- 对答辩 Demo 保留一键演示入口，减少现场操作步骤。

### 4. 场景承载与性能控制

- 优先支持 360 全景图作为 Skybox 或球形背景，作为稳定保底场景方案。
- 只在用户近处摆放少量本地 3D 预设物体，例如桌子、菜单、咖啡杯、机场指示牌。
- 对 Spring 输出的场景结果做客户端过滤：限制物体数量、限制加载范围、忽略未知资源。
- 保留 Holodeck 3D 布局接入口，但其结果应先转成轻量 JSON，再由 Unity 客户端实例化本地预制体。

### 5. 打包、联调与演示保障

- 定期在 PICO 设备上进行真机测试，避免只在 Unity Editor 中验证。
- 建立最小可演示包，优先保证一条完整链路稳定，而不是追求过多场景。
- 准备离线演示模式：不依赖实时 LLM/TTS/场景生成，也能展示系统闭环。
- 记录联调问题清单，包括模块接口不一致、音频格式不兼容、资源路径错误、Android 权限缺失等。

当前已提供的编辑器保障工具：

- `SceneTalkVR/Setup/Rebuild Demo Rig`：清理旧生成物后创建唯一 Demo Rig，并修复相机、Canvas、EventSystem 和输入。
- `SceneTalkVR/Setup/Apply Recommended Project Settings`：配置 Android/OpenXR/PICO 推荐默认项并运行预检。
- `SceneTalkVR/Diagnostics/Run Preflight Check`：生成 `Assets/SceneTalkVR/Docs/VitorPreflightReport.md`，用于记录当前缺失项和打包准备状态。
- `SceneTalkVR/Advanced/Clear Generated Demo Rig`：只清理 Demo Rig 和 World UI。
- `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile`：仅在 OpenXR Validation 仍缺少交互 Profile 时启用通用兜底控制器。

## 与组员接口

### 与 Spring 的接口：LLM 大脑与场景生成

Spring 负责把用户指令解析为任务、环境、人设和场景结果。Vitor 负责在客户端消费这些结果并驱动场景呈现。

推荐 Spring 输出 JSON：

```json
{
  "taskType": "ordering_coffee",
  "environmentType": "coffee_shop",
  "avatarRole": {
    "role": "barista",
    "speakingSpeed": "fast",
    "accent": "american",
    "attitude": "friendly"
  },
  "scene": {
    "mode": "skybox",
    "skyboxUrl": "https://example.com/coffee-shop-360.jpg",
    "layoutObjects": [
      {
        "prefabKey": "coffee_table",
        "position": [0.8, 0.0, 1.2],
        "rotationY": 20
      }
    ]
  }
}
```

Vitor 需要保证客户端具备以下消费能力：

- 根据 `environmentType` 或 `skyboxUrl` 替换场景背景。
- 根据 `layoutObjects` 在安全范围内实例化本地预制体。
- 根据 `avatarRole` 通知 Edwin 的 Avatar 模块加载对应角色。
- 当字段缺失或资源不可用时，自动切换到默认场景和默认角色。

### 与 Edwin 的接口：语音交互与 Avatar 系统

Edwin 负责 STT、TTS、Avatar 加载和口型同步。Vitor 负责调度调用时机，并把音频、Avatar、UI 状态串起来。

推荐 Edwin 输出或暴露的数据：

```json
{
  "sttText": "I want to practice ordering coffee with a fast-speaking barista.",
  "ttsAudioPath": "Assets/StreamingAssets/DemoAudio/barista_reply.wav",
  "avatarPrefabKey": "barista_default",
  "avatarStatus": "ready"
}
```

Vitor 需要保证客户端具备以下调度能力：

- 在 `Listening` 状态启动录音或调用 Edwin 的 STT 模块。
- 在 `Processing` 状态把 `sttText` 交给 Spring 的 LLM/场景模块。
- 在 `AvatarSpeaking` 状态播放 `ttsAudioPath` 或 `AudioClip`，并通知 Avatar 进入说话动画。
- 当实时 TTS 未返回时，允许播放预设短语音或思考动作，降低用户等待感。

## 风险与 B 计划

### 风险 1：Unity、PICO SDK、Holodeck 版本冲突

Holodeck 对旧 Unity 环境和运行平台有特定要求，而当前客户端是 Unity 6。Vitor 不应尝试把 Holodeck 直接合进 `Client` 工程。

B 计划：

- 保持 `Client` 和 `Holodeck` 解耦。
- Holodeck 或场景生成逻辑独立为后端服务。
- Unity 客户端只消费 JSON、图片 URL 或本地资源 key。
- 如果 Holodeck 路线不稳定，直接切换为 360 全景图方案。

### 风险 2：PICO 4 性能不足以承载复杂动态 3D 场景

动态加载大量 3D 资产可能造成掉帧、内存溢出或闪退。

B 计划：

- 远景使用 360 全景图。
- 近处只加载少量低面数本地预制体。
- 对所有动态实例化结果设置数量上限。
- Demo 优先保证 72Hz 或设备目标帧率下稳定运行。

### 风险 3：STT -> LLM -> TTS 链路延迟过长

完整语音对话链路可能产生数秒等待，影响沉浸感。

B 计划：

- 客户端状态机支持异步等待和 UI 反馈。
- Avatar 在等待期间播放思考动作或短语音。
- 支持流式输出时逐句播放，不等待完整长回复。
- 准备离线兜底音频，保证答辩现场演示稳定。

### 风险 4：模块接口不一致导致联调延期

Spring、Edwin、Vitor 分别负责不同模块，如果字段和调用时机不统一，后期会产生大量联调成本。

B 计划：

- 先定义 JSON 字段和默认值。
- Vitor 先用假数据完成客户端闭环。
- 每次接入真实模块前先用固定样例 JSON 测试。
- 所有模块失败时必须返回可识别错误，而不是让客户端卡死。

## 验收标准

Vitor 的任务完成标准如下：

- `Client` Unity 工程能正常打开，并具备基础 XR/PICO 运行配置。
- 在 Unity Editor 中可完成最小闭环：开始练习、模拟输入、生成/切换场景、加载 Avatar、播放回复音频、结束练习。
- 在 PICO 设备上至少能运行一条稳定 Demo 流程。
- 客户端主状态机能清楚展示当前流程状态，并能处理模块失败或网络失败。
- 客户端能消费 Spring 提供的场景/人设 JSON，并能调度 Edwin 的语音/Avatar 输出。
- 答辩时能说明“Unity 客户端 + AI/Holodeck 服务端解耦”的架构优势和风险控制策略。
- 保底演示模式可用，即使实时网络 API 失败，也能展示 360 全景图、默认 Avatar 和预设对话音频。

## 答辩表达重点

Vitor 可以在答辩中突出以下表达：

> 我负责的是整个系统的 VR 客户端底座和模块调度。我们的架构不会把所有高风险模块都塞进一个 Unity 工程，而是让 Unity/PICO 客户端专注于交互、呈现和打包，让 LLM、Holodeck、语音和 Avatar 模块通过接口接入。这样既能降低版本冲突和性能风险，也能保证每位组员并行开发，最后通过清晰的数据接口联调成完整 Demo。

建议配合一张架构图说明：

```text
用户语音
  -> Edwin: STT
  -> Spring: LLM 指令解析 / 对话生成 / 场景生成
  -> Vitor: Unity 客户端状态机调度
  -> Vitor: 场景呈现 / VR UI / PICO 运行
  -> Edwin: TTS / Avatar / Lip Sync
  -> 用户继续对话
```
