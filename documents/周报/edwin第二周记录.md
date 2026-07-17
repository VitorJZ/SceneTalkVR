# SceneTalkVR 项目周报（2026-07-13 至 2026-07-17）

## 一、本周概览

本周工作主要围绕四条主线展开：Avatar 动画体系稳定化、对话延迟优化、固定实验场景适配，以及纠错 Assistant 的角色化升级。根据 Codex 本地会话记录，本周共检索到 16 个与 SceneTalkVR 直接相关的任务，分布为：7 月 13 日 1 个、7 月 14 日 6 个、7 月 15 日 2 个、7 月 16 日 7 个；7 月 17 日主要进行本周记录整理。

本周已经形成并落地的主要成果包括：

- 重构并稳定 Avatar 的 Idle、Thinking、Talk 动画状态与 Humanoid 重定向流程。
- 完成纠错语音与 Avatar 回复两段 TTS 的并行预取，缩短回合间等待。
- 将六角色关键词匹配改为四个固定场景与固定人物、音色映射。
- 将纠错 Assistant 从程序化小球升级为可切换的 Sparrow 小鸟 Prefab，并完善朝向、动画和常驻显示逻辑。
- 解决并合并 Spring 分支冲突，将 `edwin-dev` 同步到最新主线。
- 完成“ASR 自动纠错影响语法纠错证据”的技术调研与验证计划，但尚未进入 P0 数据基准测试。

## 二、本周完成工作

### 1. Avatar 动画架构与重定向稳定化

首先梳理了现有 Avatar 结构。当前六个人物并不是真正共享同一副 Skeleton，而是各自保留模型骨架和 Unity Avatar，通过 Humanoid/Mecanim 映射到统一的人形语义骨骼，并共享 Animator 状态机及部分上半身动画。这一结构能够实现动画复用，但跨体型动画仍需要严格控制 Avatar T-Pose、Avatar Mask 和原生骨骼辅助曲线。

本周对动画状态重新定义为：

- 开场回复：`Idle -> SpeakWave -> TalkLoop -> Idle`
- 后续回复：`Idle -> ThinkingEnter -> ThinkingHold -> TalkLoop -> Idle`
- `SpeakWave` 只承担一次性开场手势。
- `ThinkingHold` 和 `TalkLoop` 按状态持续循环，并由真实音频播放起止事件驱动。
- Idle、髋部和腿部持续由每个角色自己的原生动画控制；共享动画只覆盖必要的上半身范围。

针对实际测试中出现的手臂穿模、脚部拉长、Idle 不循环、状态切换时腿部回弹等问题，完成了多轮修复：

- 使用 Unity 生成的有效 Humanoid T-Pose，避免直接使用不合法的 FBX bind pose。
- 屏蔽 Mixamo 手指曲线，避免 Quaternius 手型被错误重定向。
- 将 Animator 状态统一设置为 `Write Defaults = false`，移除会导致参考姿势闪回的 Idle 自转场。
- 排除 Unity 自动生成的 `__preview__` 非循环 Clip。
- 从每个角色视觉正确的 FBX 预览 Idle 生成独立循环 `.anim`，保留 `Foot.L/R`、`PT.L/R` 等辅助 Transform 曲线。
- 六个最终 Idle 均保留 250 条曲线，避免正式 Humanoid Clip 只有 130 条曲线时发生脚掌拉长。
- 将 Talk 动画替换为 `Thoughtful Head Nod 70AS`，并重建 Override Controller，消除失效 Mecanim Clip 映射。

相关验证包括：

- Avatar 动画 EditMode 测试最高一次达到 `24/24` 通过。
- Thinking 头部过渡专项修改完成后，相关 Unity 测试 `42/42` 通过。
- 六个角色的 Idle 均确认 `humanMotion=True`、`looping=True`，并包含完整脚部辅助曲线。
- 编译和资源重建通过；控制台剩余错误主要为工程原有 XR Manager 初始化问题。

注意：自动化和静态验证已覆盖主要回归点，但不同角色在真实 VR 对话流程中的视觉观感仍应继续人工回归，尤其是 `Idle -> Thinking -> Talk -> Idle` 的连续切换。

### 2. Thinking 动画连续性优化

原 `Thinking.fbx` 将完整的抬手、保持和放手动作整体循环，导致角色反复从起始姿势抬手。现在将其拆分为：

- `ThinkingEnter`：进入思考姿势，不循环。
- `ThinkingHold`：保持手托下巴姿势，循环播放。
- 从两个 Thinking 状态均可立即切换到 Talk 或 Idle，避免回复较快时被完整进入动画阻塞。

随后进一步增加独立的头部层和 Head Mask，降低原动作因身体未同步倾斜而显得歪头过度的问题，并稳定 Thinking 与 Talk 之间的头部过渡。相关修复已提交并合入主线。

### 3. 对话延迟与并行两段 TTS

对当前回合制链路进行了拆解：录音上传、ASR、LLM、纠错反馈、Avatar 回复 TTS 和音频播放串行执行，是回合割裂感的主要来源。在不改变整体架构的前提下，确定了三类优化方向：

- 录音端裁剪静音并预热麦克风。
- 纠错音频播放期间并行预取 Avatar 回复 TTS。
- 复用 HTTP 连接，减少重复握手开销。

本周优先落地了第二项：当纠错 Assistant 播放反馈时，Avatar 回复音频已在后台生成；纠错结束后直接复用预取结果，避免再次等待完整 TTS。对应 `AvatarSpeechPlayer` 增加了预取、缓存和并发控制，`AvatarPresentationVoiceModule` 负责纠错与回复的顺序播放。

结果：

- 并行 TTS 专项测试 `2/2` 通过。
- 相关代码已合入提交 `0ab7775`，并在同步后进入当前分支。
- 后续发现 Spring PR #22 修改了相同的 Avatar 语音、LLM 和腾讯 TTS 文件，已完成冲突分析、合并和主线同步。

仍需在 Pico 真机上记录端到端时间，比较优化前后的首音延迟、纠错结束到 Avatar 开口的间隔，以及缓存未命中时的退化表现。

### 4. 固定四场景的人物与音色绑定

项目从关键词匹配角色调整为固定实验场景后，人物解析逻辑同步收敛为四个精确映射：

| 固定场景 | 人物 Prefab | 主对话音色 |
|---|---|---|
| 餐厅服务员 | `barista_humanoid_v1` | 女声 |
| 家具销售 | `teacher_humanoid_v1` | 男声 |
| 健身顾问 | `barista_male_humanoid_v1` | 男声 |
| 酒店前台 | `teacher_female_humanoid_v1` | 女声 |

具体改动包括：

- `AvatarPresetResolver` 改为根据 `taskType/scenarioId` 精确选择，不再进行角色、环境和性别关键词评分。
- `AvatarCatalog` 缩减为四个运行时人物；两个 Police 角色保留为历史资产，但不再进入 Catalog。
- 语音优先级改为“固定 Prefab 性别 > LLM payload 性别 > 默认 voiceId”，防止固定女角色被错误 payload 切换为男声。
- 新增固定场景解析和语音性别优先级回归测试。

Unity EditMode 测试 `6/6` 通过，并在 `SampleScene` Play Mode 中确认四个任务均解析到指定人物。功能与测试已一并提交并推送到 `edwin-dev`，对应 `6384089`。

### 5. Avatar 生成位置与朝向

VR 测试中 Avatar 有时生成在玩家左侧、有时在右侧，原因是原逻辑依据玩家当前朝向和动态偏移计算位置。现在改为以 World UI/对话框的固定位置作为锚点：

- Avatar 根据对话框位置生成。
- 生成后位置固定，不再随玩家移动。
- 身体在生成时朝向玩家一次，之后保持固定方向。
- 头部和眼睛继续通过 Humanoid IK 跟随玩家，并保留转头角度限制。

同时新增了位置回归测试。相关逻辑与 Assistant 常驻显示等修改一并进入提交 `60c5328`。

### 6. 纠错 Assistant 外观升级

本周先对原程序化小球进行视觉重设计，增加能量核心、玻璃外壳、语音波形、双色轨道、音量响应、柔和显隐和无缝脉冲动画。随后调研了免费的 Low Poly 角色资源，最终选择 Quirky Series 中的 Sparrow 小鸟作为具身纠错 Assistant。

完成内容包括：

- 增加 `GeneratedAgent / PrefabAvatar` 外观模式，可在 Inspector 中切换小球与小鸟。
- Sparrow 使用自带 `Idle_A` 作为待机、`Bounce` 作为说话动作。
- 材质转换为 URP/Lit。
- Prefab 模式不再叠加程序化上下浮动，避免与小鸟原生 Idle 动画冲突。
- 小鸟平滑朝向玩家，只绕竖直轴旋转，保持直立。
- 在完全正对玩家的基础上加入轻微水平偏角，使侧面轮廓更自然。
- 增加 Prefab 接线、Idle/Talk、朝向和无额外浮动的 Editor 测试。

Sparrow 功能和必要依赖已提交并推送为 `7a7c9af`。完整 Quirky 包仍有大量未使用资源留在本地未跟踪状态，需要后续清理。

### 7. Assistant 常驻显示逻辑

修复了 Assistant 仅在实际触发纠错时才出现的问题。根因是动态创建后虽然调用了激活接口，但 `CorrectionFeedbackPresenter.Start()` 又将会话状态重置为未激活。

当前行为为：

- `assistant_agent_explicit` 和 `assistant_agent_recast` 两种模式进入场景后，Assistant 与 Avatar 同时初始化并常驻显示。
- 没有具体纠错内容时，Assistant 仍保持可见。
- `dialogue_avatar_explicit` 和 `dialogue_avatar_recast` 不显示独立 Assistant，由主 Avatar 承担反馈。
- 切换模式、结束或重置会话时，Assistant 正确隐藏。
- Streaming 预加载路径也同步处理。

该修复已包含在 `60c5328` 中。

### 8. 团队分支冲突与主线同步

Spring PR #22 与本周并行 TTS 修改在以下核心文件发生交叉：

- `AvatarPresentationVoiceModule.cs`
- `RealLLMService.cs`
- `tencent.py`

完成冲突分析和解决后：

- PR #22 已成功合并到 `main`。
- 本地 `edwin-dev` 已 fast-forward 到最新主线。
- 5 处 stash 恢复冲突全部解决。
- `SampleScene` Smart Merge 检查为 `0 issues`。
- Presenter 脚本检查为 `0 errors`，Prefab Assistant EditMode 测试 `1/1` 通过。

截至本周记录整理时，`edwin-dev` 与 `origin/edwin-dev` 已同步，但工作区仍存在场景、RuntimeConfig、ProjectSettings、语音规划文档及完整 Quirky 包等未提交内容。

### 9. World UI 重复问题定位

确认场景中的两个 `World UI` 都是真实序列化对象，不是 Play Mode 临时生成：

- 一个是未被任何脚本引用的残留副本。
- 另一个才被 `SceneTalkFlowUiController` 和 `SceneTalkInteractionBootstrap` 使用。
- 根因是 Setup 脚本每次只删除找到的第一个 UI 再重新创建，场景一旦出现两个副本，重复运行也无法彻底清理。

本周只完成定位，没有在该任务中修改场景。后续应删除残留副本，并将 Setup 逻辑改为清理全部旧实例后再创建唯一 UI。

## 三、技术调研与方案记录

### 1. ASR 自动纠错对语法纠错实验的影响

当前腾讯语音识别会将类似 `I are hungry` 的错误表达自动修正为正确文本，导致后续纠错系统失去原始错误证据。本周形成了独立技术方案：`documents/asr-correction-evidence-technical-plan.md`。

主要结论：

- 不默认采用双路识别，先验证 Azure 单路能否同时满足纠错证据和对话体验。
- 腾讯一句话识别缺少 N-best 与真实置信度，不适合作为严格的纠错证据。
- Azure Detailed 结果可返回 `NBest`、`Lexical`、`ITN`、`Display` 和候选置信度，是首选验证对象。
- AWS 的多候选仅适用于批处理，不适合实时 VR。
- Deepgram 有词级置信度和流式能力，但多候选能力仍需验证。
- sherpa-onnx 可作为云端 ASR 仍会自动修正时的本地备用路线。

规划的 P0 准入指标包括：

- Top-5 错误保留率不低于 85%。
- 正确句误触发率不高于 5%。
- 纠错 Precision 不低于 90%。
- 纠错证据 p95 延迟不超过 900 ms。

该任务目前只完成调研和文档，没有修改运行代码。下一步是建立录音数据集，并实现腾讯/Azure REST benchmark 工具。

### 2. 腾讯免费英文音色选择

为区分主 Avatar 与纠错 Assistant，调研了腾讯英文音色和免费额度：

- 10 万字符“大模型音色”免费包中，支持英文的主要选择为 `501008 WeJames` 和 `501009 WeWinny`。
- 可使用默认 `WeJack / WeRose` 作为 Avatar 男/女声，再使用 `WeJames / WeWinny` 作为 Assistant 第三音色。
- `502xxx / 602xxx / 603xxx` 等超自然音色属于另一档 2 万字符额度，不应与 10 万字符包混用估算。

本周完成了选型结论，但尚未固定最终 Assistant 音色并完成听感测试。

## 四、本周主要提交

| Commit | 内容 | 状态 |
|---|---|---|
| `74390df` | 更新 Avatar 动画状态、原生 Idle、Humanoid 重定向及语音置信度 | 已进入主线 |
| `0ab7775` | 并行两段 TTS，纠错播放期间预取 Avatar 回复 | 已进入主线 |
| `4f9b31d` | 稳定 Thinking 头部过渡 | 已通过 PR #23 合入主线 |
| `6384089` | 固定四场景人物与语音性别映射 | 已推送至 `origin/edwin-dev` |
| `7a7c9af` | 增加 Sparrow 纠错 Assistant | 已推送至 `origin/edwin-dev` |
| `60c5328` | 优化 Avatar 位置、外观与 Assistant 常驻显示 | 已推送至 `origin/edwin-dev` |

## 五、遗留问题与风险

1. **真实 VR 全链路回归不足**
   Avatar 动画、并行 TTS、固定场景和 Assistant 分别通过了自动化或局部 Play Mode 验证，但仍需在 Pico 真机上完成一次连续多回合测试，重点观察动画过渡、首音延迟、空间位置、头部跟随和模式切换。

2. **Quirky 资产许可与仓库分发边界**
   Sparrow 来自 Unity Asset Store Standard EULA。仓库为公开仓库时，原始 FBX、动画、材质及可直接提取资源的再分发边界需要再次确认。正式发布或扩大协作范围前，应完成许可复核并记录结论。

3. **本地存在大量未使用 Quirky 资源**
   Sparrow 的必要资源已提交，但完整免费包中的其他动物、Demo、Shader 和动画仍为本地未跟踪文件。应删除未使用内容，避免误提交和仓库体积膨胀。

4. **ASR 纠错证据尚未实测**
   Azure 单路、多候选保留率和噪声环境表现目前只有方案，没有数据。纠错实验的有效性仍取决于 P0 benchmark 结果。

5. **World UI Setup 不具备幂等性**
   场景残留副本可手工删除，但生成工具仍可能再次制造重复 UI，需要从 Setup 脚本层修复。

6. **工作区仍有未提交配置漂移**
   `SampleScene`、RuntimeConfig、ProjectSettings、`packages-lock.json` 和语音规划文档仍有本地修改。后续提交前需按功能拆分，避免将 Unity 本机配置和无关资源混入功能 PR。

## 六、下周建议

1. 在 Pico 真机执行固定四场景的完整多回合回归，记录 ASR、LLM、纠错 TTS、Avatar TTS 各阶段耗时。
2. 完成 ASR P0 benchmark：录音数据集、腾讯/Azure 对比脚本、错误保留率和延迟报告。
3. 清理未使用 Quirky 资源，并确认 Sparrow 在公开仓库中的许可处理方式。
4. 修复 World UI Setup 的幂等性，确保重复执行不会生成残留对象。
5. 固定 Assistant 第三音色，完成 Avatar 与 Assistant 的听感区分测试。
6. 整理当前未提交工作区，按场景配置、语音方案和 Unity 本机设置分别处理。

## 七、记录口径

本文依据以下信息整理：

- 2026-07-13 至 2026-07-17 的本地 Codex 会话记录。
- 同期 SceneTalkVR Git 提交历史。
- 2026-07-17 整理时的当前分支与工作区状态。

仅讨论但未落地的内容已放入“技术调研”或“遗留问题”，未计入已完成功能。
