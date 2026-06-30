# Edwin 组员任务规划：SceneTalk VR 语音交互与 Avatar 化身系统

## 任务定位

Edwin 负责 SceneTalk VR 中“听、说、显形”的模块：语音识别 STT、语音合成 TTS、Avatar 外观匹配与运行时加载。该模块的核心目标不是单独做一个语音或人物 demo，而是把用户语音、Spring 的 LLM/场景结果、Vitor 的 Unity/PICO 客户端主流程连接起来，让系统能够根据自然语言练习目标生成合适角色，并用语音完成沉浸式口语对话。

当前仓库采用“Unity 客户端 + 后端语音网关 + 本地 Avatar 预设库”的路线。Unity 客户端不直接暴露云厂商密钥，STT/TTS 统一经由 `Server/voice-gateway` 调用；Avatar 侧用本地 `AvatarCatalog.asset` 和 prefab 预设库实现稳定匹配，当前已接入 teacher、barista、police 三个 Humanoid 试点，后续可继续扩展 Addressables、Ready Player Me 或其他 Avatar 后端。

Edwin 的模块应保持可替换、可降级、可联调：即使真实云服务、真实模型或口型同步暂时不可用，主练习流程也应能通过 mock provider、占位 Avatar 和离线音频继续演示。

## 核心目标

1. 建立稳定的 STT/TTS 语音链路：Unity/PICO 录音、上传语音网关、返回 transcript、合成 Avatar 回复音频并播放。
2. 建立安全的后端语音网关：隐藏腾讯云等云厂商密钥，对 Unity 暴露统一协议，并保留 mock fallback。
3. 建立 Avatar 资源匹配与加载链路：消费 Spring 的 `SpringScenePayload.avatarRole` 和 `appearance` 字段，选择合适角色 prefab 并实例化到 `AvatarRoot`。
4. 保证 Avatar 资源缺失、语音服务失败、字段不完整时仍可回退到默认角色或 demo 语音，不阻断整体练习流程。
5. 为后续更多真实模型替换、PICO 真机麦克风验证、流式语音、口型同步和 LLM 对话记忆预留接口。

## 当前完成进度（2026-06-30）

### 已完成

- 已在 `documents/avatar-module-technical-plan.md` 中完成 Avatar 外观生成模块技术规划。
- 已在 `documents/speech-gateway-technical-plan.md` 中完成 STT/TTS 语音网关技术规划。
- 已在 `SceneTalkContracts.cs` 中扩展 `AvatarAppearanceData`，并挂到 `AvatarRoleData.appearance`。
- 已建立 Avatar 预设库核心脚本：`AvatarCatalog`、`AvatarPresetEntry`、`AvatarResolutionResult`、`AvatarPresetResolver`。
- 已建立 Avatar 加载接口：`IAvatarInstanceLoader` 与 `PrefabAvatarInstanceLoader`。
- 已建立组合呈现模块 `AvatarPresentationVoiceModule`，可完成 Avatar 解析、加载、替换、动画触发和语音播放。
- 已新增 `SceneTalkVR/Avatar/Generate Placeholder Avatars`，可生成本地占位 Avatar prefab 和 catalog。
- 已生成 `barista_default`、`teacher_default`、`police_default` 三个占位 Avatar，分别覆盖咖啡店、课堂、警务/机场等演示场景。
- 已建立 `AvatarCatalog.asset`，通过角色、环境、服装、配件、优先级和 fallback 规则匹配 Avatar。
- 已完成 Avatar P1 三个 Humanoid 角色试点：`teacher_humanoid_v1`、`barista_humanoid_v1`、`police_humanoid_v1` 均已制作 prefab 并登记进 `AvatarCatalog.asset`。
- 已记录真实模型来源与授权：`AvatarHumanoidP1SourceLog.md`，当前 Humanoid 角色主要使用 Quaternius / Poly Pizza 授权资源。
- 已建立共享 `AvatarCommonHumanoid.controller`，支持 `Idle` / `Think` / `Speak` / `Talk` 动作触发。
- 已更新 demo payload，可根据输入关键词生成 barista / teacher / police 三类角色，并命中对应 Avatar。
- 已完成 Avatar P0 稳定性验证：未知角色 fallback、旧 Avatar 清理、多轮替换、资源缺失时继续流程。
- 已新增后端语音网关 `Server/voice-gateway`，提供 `/health`、`/api/voice/stt`、`/api/voice/tts` 和音频下载接口。
- 语音网关已支持 `mock` provider：固定 transcript 与生成 WAV tone，用于离线演示。
- 语音网关已支持 `tencent` provider：调用腾讯云 ASR `SentenceRecognition` 与 TTS `TextToVoice`。
- 已支持 `VOICE_GATEWAY_PROVIDER=mock|tencent` provider 切换。
- 已支持 `TENCENT_FALLBACK_TO_MOCK=true`，腾讯云失败时自动回退 mock，保证 demo 不因云服务失败中断。
- 已新增 Unity 侧 `VoiceGatewayClient`、`MicrophoneRecorder`、`GatewaySpeechInputModule`。
- Unity 侧可录制默认麦克风音频，编码为 16-bit WAV base64 后上传给语音网关。
- Unity 侧可请求 TTS、下载返回 WAV，并转换为 `AudioClip` 播放。
- 已新增 `VoiceGatewaySettings.asset`，集中配置 `gatewayBaseUrl`，支持团队通过局域网共用一台语音网关主机。
- 已新增 `SceneTalkVR/Setup/Rebuild Demo Rig With Voice Gateway`，可在现有 demo rig 上切换到真实语音网关路径，不重建场景生成、UI 或 Avatar 配置。
- 已合入 Vitor 的连续回合框架；同一场景内可重复触发 `STT -> Brain -> TTS -> Avatar 回复`，但 LLM 对话历史仍由 Spring 的 Brain 层负责。
- 当前文档记录显示：腾讯云 ASR/TTS 已在后端 live smoke test 与 Unity Editor 闭环中完成验证。

### 仍未完成

- PICO 4 真机上的麦克风权限、录音设备、采样率、上传、识别和播放尚需回归验证。
- 录音结束策略仍偏 P0，后续需要手动结束录音、基础静音检测或 VAD。
- STT/TTS 目前以 turn-based 整段上传和整段播放为主，尚未实现流式 STT、TTS 分段播放和 barge-in 打断。
- 语音日志、错误码、延迟统计、成本统计和日志脱敏还需补齐。
- Avatar 真实模型已有 teacher、barista、police 三个 Humanoid 试点；后续若需要更多角色、LOD、Addressables 或更高质量资产，还需继续扩展。
- 真实口型同步尚未实现；当前主要是播放 TTS 音频并触发基础 speaking 动画。
- Addressables、远程 Avatar 加载、Avatar 缓存和 LOD 尚未实现。
- 当前连续回合框架已经可重复调用 Edwin 的语音和 Avatar 播放链路；完整多轮智能仍需要 Spring 的多轮 LLM Brain 维护历史、Prompt 和上下文策略。

### 需要人工或外部条件的步骤

- 在语音网关主机上配置腾讯云 ASR/TTS 账号与环境变量。
- 团队联调时将 `VoiceGatewaySettings.asset` 中的 `gatewayBaseUrl` 改为网关主机局域网 IP。
- PICO 真机测试时确认 Android 麦克风权限、网络访问权限和与网关主机的同网段连通性。
- 为新增真实 Avatar 模型确认来源、授权、模型格式、面数、贴图尺寸和课程/论文/demo 展示许可。
- 若接入口型同步插件，需要确认插件授权、Unity 6 兼容性、PICO/Android 兼容性和模型 BlendShape/骨骼支持情况。

## 阶段计划

### 第 1 阶段：准备期 - 模块边界、假数据闭环和 Avatar 预设库

- [x] 明确 Edwin 分工边界：负责 STT/TTS、语音网关、Avatar 匹配与加载，不负责 LLM、场景生成、PICO 打包和主流程 UI。
- [x] 复用 Vitor 的 `SceneTalkOrchestrator`、`ISceneTalkSpeechInput`、`ISceneTalkAvatarVoice` 接口。
- [x] 扩展 `AvatarRoleData.appearance`，让 Spring 输出的结构化 JSON 能承载 Avatar 外观需求。
- [x] 建立本地 Avatar 预设库目录和 `AvatarCatalog.asset`。
- [x] 使用 barista / teacher / police 三个占位 Avatar 跑通角色切换。
- [x] 建立 Avatar fallback 规则，保证未知角色也能加载默认角色。
- [x] 建立离线 demo 能力，不依赖真实云服务也能演示流程。

### 第 2 阶段：原型期 - 真实语音网关与 Unity 语音 adapter

- [x] 新建 `Server/voice-gateway` 后端网关。
- [x] 建立 mock provider，支持离线 STT/TTS。
- [x] 建立 Tencent provider，支持腾讯云 ASR/TTS。
- [x] Unity 侧新增 `MicrophoneRecorder`，完成麦克风录音和 WAV base64 编码。
- [x] Unity 侧新增 `VoiceGatewayClient`，统一访问 STT/TTS API。
- [x] Unity 侧新增 `GatewaySpeechInputModule`，替换假 STT 输入。
- [x] 扩展 `AvatarPresentationVoiceModule`，支持优先播放语音网关 TTS 音频。
- [x] 提供 `Rebuild Demo Rig With Voice Gateway`，方便一键切换到真实语音路径，且只修改语音组件和引用。
- [x] 在 Unity Editor 中完成真实语音闭环验证。

### 第 3 阶段：联调期 - PICO 真机、真实 Avatar 和语音体验增强

- [ ] 在 PICO 4 上验证麦克风录音、上传、腾讯云 ASR、TTS 下载和播放。
- [ ] 增加手动结束录音和基础静音检测，避免固定录音时长影响体验。
- [ ] 补齐语音网关结构化日志：provider、耗时、错误码、fallback level、音频时长和缓存状态。
- [ ] 补齐日志脱敏策略：默认不保存原始音频和完整 transcript。
- [x] 完成至少 3 个真实或半真实 Humanoid Avatar：teacher、barista、police。
- [x] 为真实 Avatar 统一基本 idle / thinking / speaking / follow-up talking 动画触发。
- [ ] 继续保留 placeholder prefab 作为 fallback，避免真实模型导入失败影响演示。
- [ ] 与 Spring 的真实 LLM/场景 payload 联调，确认 `avatarRole` 与 `appearance` 字段稳定。
- [x] 与 Vitor 的 Orchestrator 连续回合框架联调，确认 Avatar 语音播放结束后可由用户触发下一轮交互。

### 第 4 阶段：答辩打磨期 - 口型同步、延迟优化和稳定演示

- 准备固定演示路径，例如“咖啡店点单”或“课堂问答”，确保语音、Avatar 和场景输出一致。
- 准备离线兜底：mock transcript、固定 TTS 音频、占位 Avatar、固定 payload。
- 评估 TTS 分段合成和播放队列，降低 Avatar 开口等待时间。
- 评估口型同步方案：优先考虑音频驱动的基础 mouth/jaw 动作，时间允许再接入插件级 viseme。
- 汇总语音链路延迟和失败 fallback 数据，用于 PPT 风险控制说明。
- 配合 Vitor、Spring 统一答辩术语：Edwin 负责语音交互与化身系统，Vitor 负责客户端容器与调度，Spring 负责 LLM 大脑与场景生成。

## 具体任务清单

### 1. STT 语音识别

- Unity 侧采集用户麦克风输入。
- 将音频转换为后端可识别格式，目前采用 16-bit WAV base64。
- 经由 `VoiceGatewayClient` 调用 `/api/voice/stt`。
- 将返回 transcript 交给 `SceneTalkOrchestrator`，再由 Spring Brain 生成场景和回复。
- 保留 mock transcript，云服务失败时可继续演示。
- 后续补齐 PICO 真机权限、录音结束、静音检测和错误提示。

### 2. TTS 语音合成

- 从 `SpringScenePayload.dialogueReply` 读取 Avatar 要说的话。
- 从 `avatarRole` 读取语速、口音、态度、角色等语音画像。
- 经由 `VoiceGatewayClient` 调用 `/api/voice/tts`。
- 下载网关返回的 WAV，转成 `AudioClip` 后由 Unity `AudioSource` 播放。
- TTS 失败时回退 demo 音频或 fallback speaking 等待。
- 后续补齐分段合成、音频缓存、播放队列和打断能力。

### 3. 后端语音网关

- 不让 Unity/PICO 直接持有腾讯云、OpenAI、Azure 等密钥。
- 网关统一暴露 STT/TTS JSON 协议，对内切换 provider。
- 当前 provider 包括 `mock` 和 `tencent`。
- 腾讯云密钥只通过环境变量配置，不写入 Unity 工程、Git、`.meta` 文件或截图。
- 支持局域网共享：PICO 和队友机器访问网关主机 IP，而不是 `127.0.0.1`。
- 后续补齐日志脱敏、超时、成本统计、缓存和更细错误码。

### 4. Avatar 匹配与加载

- 消费 Spring 输出的 `SpringScenePayload.avatarRole` 和 `avatarRole.appearance`。
- 使用 `AvatarPresetResolver` 根据 role、environment、outfit、accessory、must-have 等字段打分匹配。
- 使用 `AvatarCatalog.asset` 管理 prefab、资源标签、优先级和默认角色。
- 使用 `PrefabAvatarInstanceLoader` 实例化当前匹配到的 Avatar。
- 切换角色时清理上一轮 Avatar，避免场景中堆积旧实例。
- 真实模型失败或字段缺失时按顺序 fallback：精确匹配、角色默认、环境默认、全局默认、占位 Avatar。

### 5. 真实 Avatar 资源治理

- 真实模型必须记录来源、作者、授权、下载链接、格式和基础体量。
- 导入 Unity 后优先配置为 Humanoid Rig。
- 真实模型应制作正式 prefab，再登记进 `AvatarCatalog.asset`，不在运行时代码里硬编码路径。
- 面向 PICO 4 时优先低到中等复杂度模型，避免过高面数、过多材质和过大贴图。
- 每个真实角色都保留 placeholder fallback。
- 当前试点为 `teacher_humanoid_v1`、`barista_humanoid_v1` 和 `police_humanoid_v1`；下一步建议做移动端预算检查、LOD/Addressables 规划和更多角色扩展。

### 6. Avatar 语音表现与口型同步

- 当前已能在 Avatar 说话前触发 thinking/speaking 动画。
- 当前 P0/P1 重点是“能说、能换角色、能稳定 fallback”，口型同步不阻塞主链路。
- 后续可以先实现低成本音量驱动 mouth/jaw 动作，再评估 Oculus LipSync、SALSA 或其他插件。
- 口型同步应挂在音频播放入口之后，不应反向修改 STT/TTS 或 LLM 逻辑。
- 若模型没有 BlendShape，则优先采用下颌骨或简单 speaking 动画作为保底。

## 与组员接口

### 与 Vitor 的接口：客户端调度与 VR 运行环境

Vitor 负责 Unity/PICO 客户端底座、主状态机、VR UI、设备输入、PICO 打包和最终演示流程。Edwin 模块通过既有接口接入，不直接改动主流程调度。

Edwin 向 Vitor 提供：

```json
{
  "sttText": "I want to practice ordering coffee with a fast-speaking barista.",
  "ttsAudioPath": "gateway:/api/voice/audio/turn-001.wav",
  "avatarPrefabKey": "barista_default",
  "avatarStatus": "ready"
}
```

Vitor 侧需要保证：

- 在 `Listening` 状态调用 Edwin 的 STT adapter。
- 在 `Processing` 状态把 transcript 交给 Spring Brain。
- 在 `AvatarSpeaking` 状态调用 Edwin 的 Avatar/TTS 模块播放回复。
- 在 TTS 播放完成后允许进入下一轮或结束练习。
- 在 PICO 真机上验证麦克风权限、音频播放、网络访问和 UI 状态提示。

### 与 Spring 的接口：LLM 大脑、场景生成与角色配置

Spring 负责用户意图解析、LLM 回复、场景生成和 payload 输出。Edwin 不负责 prompt 和 JSON 生成，只消费稳定结构化结果。

推荐 Spring 输出给 Edwin 的 Avatar 字段：

```json
{
  "taskType": "ordering_coffee",
  "environmentType": "coffee_shop",
  "dialogueReply": "Good morning! What can I get for you today?",
  "avatarRole": {
    "role": "barista",
    "speakingSpeed": "fast",
    "accent": "american",
    "attitude": "friendly",
    "appearance": {
      "styleId": "semi_realistic_v1",
      "ageBucket": "adult",
      "bodyBuild": "average",
      "outfitRole": "barista",
      "outfitColor": "green",
      "accessories": ["glasses"],
      "mustHave": ["green_apron"]
    }
  }
}
```

Edwin 侧可消费字段：

- `dialogueReply`：TTS 文本。
- `avatarRole.role`：角色职业，例如 barista、teacher、police。
- `avatarRole.speakingSpeed`：语速。
- `avatarRole.accent`：口音。
- `avatarRole.attitude`：语气/态度。
- `avatarRole.appearance`：外观匹配字段。
- `environmentType`：Avatar 环境 fallback 的辅助字段。

Spring 不需要传 Unity prefab 路径，也不需要知道 `AvatarCatalog.asset` 的内部引用。资源 key 与 prefab 选择由 Edwin 的 Avatar 模块在 Unity 侧完成。

## 风险与 B 计划

### 风险 1：STT/TTS 云服务失败或网络延迟过高

语音链路包含录音、上传、ASR、LLM、TTS、下载和播放，任何一步失败都会影响沉浸感。

B 计划：

- 保留 `mock` provider。
- 保留固定 transcript 和固定 demo 音频。
- 腾讯云失败时通过 `TENCENT_FALLBACK_TO_MOCK=true` 回退。
- UI 显示处理状态，Avatar 播放 thinking 动作掩盖等待。
- 答辩现场优先使用固定演示路径和可控网络环境。

### 风险 2：云密钥泄露

如果把云厂商密钥放入 Unity 工程或 Android 包，打包后很容易被反编译获取。

B 计划：

- Unity 只访问语音网关。
- 密钥只存在网关主机环境变量。
- Git 中只保留变量名和配置说明，不提交真实 secret。
- 日志和截图中避免出现密钥、完整 transcript 和原始音频。

### 风险 3：真实 Avatar 模型导入不稳定

免费模型可能存在授权不清、骨骼不标准、面数过高、材质 shader 不兼容或 Humanoid 配置失败。

B 计划：

- 每个模型都记录来源和授权。
- P1 只做单模型试点，再逐步扩到 3-5 个角色。
- 优先选 CC0 或明确可用于课程 demo 的低模资源。
- 保留 placeholder prefab fallback。
- 不把模型路径写死在运行时代码中，统一通过 catalog 管理。

### 风险 4：口型同步时间不足

真实口型同步需要模型 BlendShape、音频分析、插件兼容和移动端性能验证，容易拖慢主链路。

B 计划：

- P0/P1 先保证 TTS 音频播放和 speaking 动画。
- P2/P3 再实现音量驱动 mouth/jaw 或插件级 viseme。
- 没有 BlendShape 的模型使用基础 speaking 动画兜底。
- 答辩时将口型同步作为增强项，而不是主链路完成条件。

### 风险 5：三人模块接口不一致

如果 Spring 的 payload 字段、Vitor 的状态机调用时机、Edwin 的语音/Avatar 输入不一致，后期联调会被接口问题拖慢。

B 计划：

- 先以固定 JSON 样例联调。
- Edwin 侧对字段缺失做容错。
- Vitor 侧保持接口调用时机稳定。
- Spring 侧逐步补齐 `avatarRole` 和 `appearance` 字段，不一次性依赖复杂 schema。

## 答辩表达重点

Edwin 的部分可以概括为：

```text
用户说话 -> Unity 录音 -> 语音网关 STT -> Spring Brain -> 语音网关 TTS -> Unity 播放 -> Avatar 根据角色配置加载并说话
```

答辩时建议突出三点：

1. 语音网关保证安全和可替换：Unity 不暴露云密钥，后端 provider 可从 mock 切到腾讯云，未来可扩展 Azure / Whisper。
2. Avatar 采用预设库匹配而不是运行时生成 3D 人体：更适合 PICO 4、工程风险更低、可稳定演示。
3. 全链路有 fallback：云服务、Avatar 资源、字段缺失和网络异常都不会让主流程直接崩溃。
