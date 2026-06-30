# SceneTalkVR 实机演示讲稿

本文档用于 5-10 分钟项目展示视频。建议录制方式为：先播放一条完整实机演示流程，在关键技术出现时暂停画面，并叠加对应技术说明。

## 展示目标

本次视频要讲清楚 SceneTalkVR 的完整技术链路：

```text
用户语音输入
-> STT 语音识别
-> LLM 意图解析与回复生成
-> 场景生成与混合渲染
-> Avatar 匹配与加载
-> TTS 语音合成与播放
-> VR 中完成英语口语练习
```

讲解时需要区分两件事：

- 当前可演示能力：Unity/PICO 客户端主流程、模块化接口、语音网关路径、Avatar 预设匹配、场景 JSON 消费与 prefab 呈现。
- 技术路线能力：LLM、Holodeck、360 全景图、STT/TTS、Avatar 资源库都通过解耦接口接入，可以逐步替换和增强。

## 开场 0:00-0:40

老师好，我们展示的是 SceneTalkVR，一个面向 PICO/VR 的生成式英语口语练习系统。用户不需要手动选择固定关卡，而是用一句自然语言描述想练习的场景，比如“我想练习和语速快的咖啡店员点单”。系统会把这句话转成场景、Avatar 角色、对话回复和语音输出，最终在 VR 中形成一个可练习的情境。

这个项目的核心思路不是把所有 AI 模型都塞进头显，而是采用“Unity 客户端 + 服务端 AI 能力解耦”的架构。Unity 负责 VR 交互、UI、场景呈现、Avatar 加载和音频播放；LLM、语音识别、语音合成、Holodeck 场景生成等重任务通过 HTTP 服务接入。

屏幕叠字：

```text
SceneTalkVR
生成式 VR 英语口语练习系统

核心路线：
Unity/PICO 客户端负责交互与呈现
AI / 场景 / 语音能力通过服务端接口解耦接入
```

## 暂停点 1：点击 Start 后 0:40-1:30

这里暂停一下。客户端架构的核心是 `SceneTalkOrchestrator`，它把整个流程拆成四个接口模块：

```text
ISceneTalkSpeechInput
-> ISceneTalkBrain
-> ISceneTalkScenePresenter
-> ISceneTalkAvatarVoice
```

也就是说，第一步采集用户语音，第二步让大模型生成结构化结果，第三步把场景显示出来，第四步让 Avatar 说话。这样的设计好处是模块可以替换：Demo 模块、真实 LLM、真实 STT/TTS、真实 Avatar 系统都可以通过同一套接口接入，不需要改 VR 底层交互代码。

客户端使用 Unity 6，配合 OpenXR、XR Interaction Toolkit 和 PICO Unity Integration SDK。PICO 手柄通过射线点击世界空间 UI，用户可以 Start、Retry、Confirm，也可以退出或重置面板位置。

屏幕叠字：

```text
客户端架构

SceneTalkOrchestrator 负责流程调度
四个接口模块串起完整练习流程：
Speech Input -> Brain -> Scene Presenter -> Avatar Voice

好处：
每个 AI / 语音 / Avatar 模块都可以独立替换
VR 交互底座保持稳定
```

## 暂停点 2：用户说话 / STT 转写出现 1:30-2:30

这里展示的是语音识别。Unity 端用 `MicrophoneRecorder` 从麦克风录制一段 WAV 音频，并转成 Base64。然后 `GatewaySpeechInputModule` 通过 `VoiceGatewayClient` 发送到本地语音网关：

```text
Unity/PICO -> Voice Gateway -> STT Provider
```

语音网关在 `Server/voice-gateway` 中实现，默认地址是 `http://127.0.0.1:8787`。它支持 mock 模式，也支持腾讯云 ASR/TTS。真实模式下，STT 使用腾讯云 ASR 的 `SentenceRecognition`，英文识别引擎是 `16k_en`。

这样做的一个重要原因是安全性：云服务密钥不放在 Unity/PICO 客户端里，而是由本机或局域网中的 gateway 统一管理。PICO 真机或队友电脑访问时不能使用 `127.0.0.1`，需要改成运行 gateway 那台电脑的局域网 IP。

当前已验证的是 Unity Editor 内的腾讯云单轮语音闭环：用户语音进入 STT，得到 transcript，再进入后续 Brain 流程。它是回合式流程，不是连续实时多轮语音对话。

屏幕叠字：

```text
语音识别 STT

Unity 录音 -> WAV -> Base64
Voice Gateway -> 腾讯云 ASR / Mock fallback

为什么要有 Gateway：
1. 云密钥不进入 Unity 客户端
2. Unity 只依赖稳定 HTTP 协议
3. 可在离线 mock 与真实云服务间切换
```

## 暂停点 3：Confirm 后进入加载 2:30-3:50

现在进入 LLM 接入。`RealLLMService` 通过 OpenAI-compatible Chat Completions API 调用交大模型代理，当前代码里的模型名是 `minimax-m2.7`。它的任务不是直接生成 Unity 对象，而是把用户自然语言转成一个统一 JSON。

这个 JSON 包括：

- `taskType`：练习任务类型，例如点咖啡、机场问询。
- `environmentType`：场景类型，例如 coffee shop、airport。
- `dialogueReply`：Avatar 的开场回复。
- `avatarRole`：Avatar 的角色、语速、口音、态度。
- `scene`：场景模式、天空盒地址和近景物体列表。

例如咖啡店点单会被解析成：任务是 ordering coffee，环境是 coffee shop，Avatar 角色是 barista，语速、口音、态度也会写入 `avatarRole`。

这里的关键点是：LLM 在系统里承担“意图解析 + 教学角色生成 + 首句对话生成”的作用。它输出的是稳定的数据协议，而不是直接操作 Unity 场景。Unity 只消费这个 JSON，所以大模型即使更换，也不会破坏客户端主流程。

屏幕叠字：

```text
LLM 接入

用户自然语言 -> 结构化 SpringScenePayload

LLM 输出：
taskType
environmentType
dialogueReply
avatarRole
scene

Unity 不直接依赖某一个模型
只依赖统一 JSON 协议
```

## 暂停点 4：场景开始生成 / 天空盒变化 3:50-5:10

场景生成这里分成两层。

第一层是远景，也就是 360 度全景背景。`PanoramaSceneService` 会把环境描述加上 `360 degree equirectangular panorama` 和 `seamless` 之类的 prompt，调用 SiliconFlow 的图片生成接口，下载生成图，再用 Unity 的 `Skybox/Panoramic` 材质更新天空盒。

第二层是近景 3D 物体。`HolodeckSceneService` 调用 Python 后端的 `/generate_scene` 接口，请 Holodeck 根据环境描述生成家具和物体布局。Holodeck 后端使用 FastAPI 包装，内部依赖 ai2holodeck、CLIP / SentenceTransformer 检索和 LLM 规划能力。为了适配本项目，后端只返回轻量 JSON，比如物体名称、位置 `[x, y, z]` 和 Y 轴旋转角。

这里的关键技术取舍是“混合渲染”：远处用 360 全景图保证沉浸感，近处只加载 3 米范围内的本地低模 prefab。这样既能体现生成式场景，又避免 PICO 一体机直接加载完整 Holodeck 大场景导致性能压力。

屏幕叠字：

```text
场景生成：混合渲染

远景：
360 全景图 -> Skybox/Panoramic

近景：
Holodeck / Python FastAPI -> 轻量布局 JSON
Unity 本地 prefab -> 3 米内交互物体

目的：
兼顾沉浸感、性能和移动 VR 稳定性
```

## 暂停点 5：近景物体出现 5:10-6:20

现在看到的近景物体不是后端随便给什么 Unity 就加载什么。项目定义了 `PrefabKey` 白名单，例如：

```text
coffee_counter
cafe_table
chair
menu_board
cash_register
generic_table
generic_chair
generic_decor
```

后端或 Unity 适配层会把 Holodeck 输出的复杂物体名映射成这些白名单 key。`HybridScenePresenter` 里也有 `MapToPrefabKey` 的兜底逻辑。找不到精确模型时，就降级成 `generic_table`、`generic_chair` 或 `generic_decor`，避免因为资产缺失导致演示崩溃。

这个设计把“AI 生成的不确定性”和“Unity 运行时的稳定性”隔离开了：AI 可以自由生成语义布局，但客户端只接受受控的资源 key。

屏幕叠字：

```text
PrefabKey 白名单

AI 输出复杂物体名
-> Adapter 映射到受控 key
-> Unity 加载本地 prefab

Fallback：
generic_table / generic_chair / generic_decor

避免资产缺失导致运行时崩溃
```

## 暂停点 6：Avatar 出现 6:20-7:30

Avatar 部分不是实时从零生成角色模型，而是“LLM 描述 + 本地预设库匹配”。`SpringScenePayload` 中的 `avatarRole` 会提供角色、语速、口音、态度，也可以提供外观字段。然后 `AvatarPresetResolver` 根据 role、environmentType 和 appearance 给本地 `AvatarCatalog` 打分，选择最匹配的 prefab。

当前 Avatar 库已经有 Humanoid 角色，例如 teacher、barista、police 等，也保留 placeholder fallback。加载由 `PrefabAvatarInstanceLoader` 完成，角色会被挂到 `AvatarRoot` 下，必要时绑定通用 Animator Controller，用 `Think` 和 `Speak` trigger 驱动基础动作。

所以这里可以解释为：Avatar 生成在当前阶段是“预设库检索与动态加载”，不是运行时 3D 模型生成。这个方案稳定、适合移动端 VR，也便于后续替换更高质量角色资产。

屏幕叠字：

```text
Avatar 生成

当前实现：
LLM 生成角色描述
AvatarPresetResolver 匹配本地 AvatarCatalog
PrefabAvatarInstanceLoader 动态加载角色 prefab

不是从零实时生成 3D 人物
而是预设库匹配 + 运行时替换 + fallback
```

## 暂停点 7：Avatar 开口说话 7:30-8:40

最后是语音生成。`AvatarPresentationVoiceModule` 会读取 LLM 返回的 `dialogueReply`，并把角色信息组成 voice profile，比如 barista、fast、american、friendly。然后它通过同一个 Voice Gateway 调用 TTS：

```text
Unity
-> Voice Gateway
-> Tencent TTS
-> WAV audioUrl
-> Unity 下载并播放
```

TTS 默认输出 WAV，采样率可以设为 24000。Unity 下载音频后转成 `AudioClip`，通过 `AudioSource` 播放，同时触发 Avatar 的说话动画。如果网关不可用，当前也保留 demo 音频或等待时间作为 fallback，保证演示不会直接中断。

这条链路的意义是：视觉角色、语言角色和声音角色来自同一个结构化 payload，所以 Avatar 看起来是谁、说什么、用什么语速和态度，都由同一轮用户意图驱动。

屏幕叠字：

```text
语音生成 TTS

dialogueReply + avatarRole
-> Voice Gateway
-> 腾讯云 TTS
-> WAV 音频
-> Unity AudioSource 播放

Avatar 外观、回复内容、声音参数
来自同一个结构化 payload
```

## 收尾 8:40-9:30

总结一下，SceneTalkVR 的完整技术链路是：

用户语音输入，经过 STT 转写成文本；LLM 把文本解析成任务、环境、角色和回复；场景模块用 360 天空盒加近景 prefab 生成 VR 情境；Avatar 模块从本地角色库中匹配并加载合适角色；最后 TTS 合成角色回复并在 VR 中播放。

项目当前最重要的工程特点是解耦：Unity 客户端只负责稳定的 VR 体验，AI 和生成能力通过服务端接口接入。这样既能在 PICO 这类移动 VR 设备上保持性能，又保留了后续替换模型、扩展场景、升级 Avatar 和改进语音对话的空间。

屏幕叠字：

```text
项目总结

自然语言 -> STT -> LLM -> 场景 -> Avatar -> TTS

核心价值：
生成式情境
模块化架构
移动 VR 可运行
可替换、可扩展、可兜底
```

## 当前问题与后续解决方案 9:30-10:30

如果视频需要接近 10 分钟，或者答辩希望体现我们对项目边界和后续工作的理解，可以在收尾后增加这一段。

目前项目已经跑通了核心链路，但它仍然是课程 Demo 阶段，还存在几个需要继续优化的问题。

第一，近景 3D 资产还不够丰富。现在为了保证 PICO 端稳定性，近景物体采用白名单 prefab 和低模 fallback，优先保证“能生成、能加载、不卡顿”。后续会继续增加更多语义明确的低模模型，比如更真实的咖啡机、柜台、菜单牌、机场柜台、行李箱、餐桌和办公室道具，并保持同一套 `PrefabKey` 白名单协议。这样后端不需要改输出格式，Unity 只需要扩展本地资源库，就能让场景更细致。

第二，当前语音流程更接近单轮或回合式闭环，还不是连续多轮对话。后续会在 LLM 侧增加 conversation state，把用户历史发言、Avatar 历史回复、当前任务目标和纠错信息一起传入模型，让 Avatar 能围绕同一个情境持续追问、纠错和反馈。同时可以增加手动结束录音、基础 VAD、打断播放和下一轮开始信号，让 VR 里的口语练习更自然。

第三，场景生成和云服务调用仍然受网络和模型延迟影响。Holodeck、全景图生成、STT 和 TTS 都可能出现冷启动或网络波动。后续方案是增加缓存和分级 fallback：常见场景如 coffee shop、airport、restaurant 可以预生成或本地缓存；相同 `environmentType` 第二次进入时优先读取缓存；云服务失败时回退到本地 skybox、mock transcript、demo audio 或默认 Avatar，保证课堂展示不中断。

第四，Avatar 的表现力还可以继续提升。现在的 Avatar 生成主要是本地预设库匹配，优点是稳定、轻量、适合移动 VR。后续可以继续扩展更多职业角色和服装风格，并加入 lip sync、表情状态、更多 idle / speaking 动画，让“角色身份”和“口语交流”更一致。

第五，PICO 真机体验还需要继续打磨。后续重点包括真机麦克风权限、局域网 gateway 连接、手柄点击命中率、UI 距离和大小、帧率、加载提示与错误恢复。目标是在网络不稳定或某个 AI 服务失败时，用户依然能完成一条可展示的练习流程。

屏幕叠字：

```text
当前问题与后续方案

1. 低模资产不足
   -> 扩展 PrefabKey 资源库

2. 对话仍偏单轮
   -> 增加 conversation state 和多轮记忆

3. 云服务延迟与失败
   -> 场景缓存 + 分级 fallback

4. Avatar 表现力有限
   -> 更多角色、动画、lip sync

5. 真机体验待打磨
   -> PICO 录音、网络、UI、帧率回归测试
```

## 录制注意事项

- 展示 Unity Inspector 或代码窗口时，不要露出 API key。
- 讲 LLM 和生图服务时，可以用“OpenAI-compatible API / SiliconFlow / Tencent Cloud”说明，不需要展示真实密钥。
- 如果现场网络不稳定，优先使用 mock 或 demo fallback 跑通流程，再解释真实接口路径。
- 讲 Avatar 时不要说“实时生成 3D 人物模型”，当前准确说法是“预设库匹配与动态加载”。
- 讲语音时不要说“连续实时多轮对话”，当前准确说法是“已验证单轮或回合式语音闭环”。
- 讲 Holodeck 时强调它是服务端模块，Unity 客户端消费 JSON、资源 key、图片路径或 URL，不把完整生成栈打进 PICO。
