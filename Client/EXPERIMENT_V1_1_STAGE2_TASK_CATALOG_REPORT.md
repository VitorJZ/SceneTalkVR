# Experiment v1.1 阶段 2：正式任务目录与固定场景资源报告

## 结论

阶段 2 的代码、资源、场景绑定和自动测试已经完成。运行时任务权威来源已统一为 `ExperimentTaskCatalog.asset`；Formal Catalog 恰好包含 Hotel、Furniture、Gym、Tourist，Restaurant 仅为 Pilot；四任务均使用本地 panorama，固定启动路径不调用 scene-intent LLM、在线生图、Holodeck 或近景 layout objects。

当前仍不能开展 Formal Mode：协议层五项研究决策继续按要求阻断，且四个正式 Avatar preset 按团队分工保持空值并被 Formal validation 明确阻断。Developer Mode 可使用现有 scenario fallback，并在日志中标记为 placeholder；这不被视为正式 Avatar 验收成功。

- 分支：`experiment-v1.1-integration`
- 阶段 1 基线：`aabecbdd3fe500e94096e2d45195762371acdbdf`
- 阶段 2 提交：由本报告所在、提交信息为 `feat(experiment): establish formal task catalog and fixed scene resources` 的 Git commit 确定；其 SHA 在最终交付消息和 `git log` 中记录。Git commit 无法在自身被哈希前可靠嵌入自身 SHA。
- Unity：`6000.3.16f1`
- Task Catalog 版本：`1.1.0-stage2`

## Task Catalog 与运行时调用链

新增类型位于 `Assets/SceneTalkVR/Scripts/Core/ExperimentTaskCatalog.cs`：

- `ExperimentTaskCatalog`：唯一运行时任务目录；
- `ExperimentTaskDefinition`：任务、场景、Avatar、voice 和 prompt 元数据；
- `ExperimentTaskGoal`：结构化目标；
- `ExperimentTaskPhase`：`Pilot` / `Formal`。

资产为 `Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset`。`SampleScene.unity` 的 `ExperimentConditionManager.taskCatalog` 已绑定该资产，旧 `taskDefinitions` 已清空。

真实调用链：

```text
SceneTalkFlowUiController.BuildTaskButtons
  -> ExperimentTaskCatalog.GetTasks(phase)
  -> SceneTalkOrchestrator.LoadAssignedTask(taskId)
  -> ExperimentConditionManager.LoadAssignedTask(taskId)
  -> ExperimentConditionManager.CreateRuntimeTask(definition)
  -> local demo:// panorama payload + fixed initialQuestion + fixed Avatar metadata
  -> PanoramaSceneService.Resources.Load
  -> AvatarPresetResolver exact presetKey（若为空，Developer Mode 才允许 scenario fallback）
  -> RealLLMService 接收 taskId/context/goals/avatarRole/roleplayPrompt
```

Formal assignment 可以调用同一 `LoadAssignedTask(taskId)` 边界；参与者 allocator 不在本阶段实现。

## 重复任务源处理

| 来源 | 当前状态 | 运行时优先级 | 漂移风险 |
|---|---|---:|---|
| `ExperimentTaskCatalog.asset` | 唯一权威来源 | 最高且唯一 | 低 |
| `SampleScene.unity.taskDefinitions` | 已清空为 `[]` | 无 | 已消除 |
| `SceneTalkFlowUiController` 四按钮文本 | 已删除，改为 Catalog 动态生成 | 无 | 已消除 |
| `ExperimentV11Protocol.asset.formalTaskIds` | 仅保存 ID 并校验集合一致性 | 协议约束，不保存内容 | 低 |
| `ExperimentConditionManager.CreateDefaultTasks` | 仅在未绑定 Catalog 的旧场景兼容路径使用 | Catalog 绑定时禁用 | 中；阶段 3 可删除兼容代码 |

## 四个正式任务

### Hotel Check-In

- `taskId/scenarioId`: `hotel_check_in`
- Context: Checking in at a hotel and confirming accommodation details.
- Goals: reservation name；breakfast included；high-floor room；check-out time。
- Initial question: “Good afternoon! Welcome to City Hotel. How can I help you today?”
- Environment/panorama: `hotel_lobby` / `SceneTalkVR/Textures/hotel-lobby-360`
- Avatar: role `hotel receptionist`；preset 待补；voice `hotel_receptionist_en`

### Furniture Shopping

- `taskId/scenarioId`: `furniture_shopping`
- Context: Speaking with a furniture salesperson to purchase a desk.
- Goals: desk size；materials；maximum budget；home delivery。
- Initial question: “Hello! Is there anything in particular you're looking for today?”
- Environment/panorama: `furniture_store` / `SceneTalkVR/Textures/furniture-store-360`
- Avatar: role `furniture salesperson`；preset 待补；voice `furniture_salesperson_en`

### Gym Membership

- `taskId/scenarioId`: `gym_membership`
- Context: Asking at a gym about membership and a training plan.
- Goals: fitness goal；monthly price；workout plan；free trial。
- Initial question: “Hi! Welcome to Active Gym. How can I help you today?”
- Environment/panorama: `gym` / `SceneTalkVR/Textures/gym-360`
- Avatar: role `gym membership consultant`；preset 待补；voice `gym_consultant_en`

### Tourist Assistance

- `taskId/scenarioId`: `tourist_assistance`
- Context: Asking staff at a tourist information point for city-visit information.
- Goals: museum directions；ticket；indoor photography；another nearby attraction。
- Initial question: “Hello! Welcome to the tourist information center. How can I help you today?”
- Environment/panorama: `tourist_information_point` / `SceneTalkVR/Textures/tourist-information-360`
- Avatar: role `tourist information officer`；preset 待补；voice `tourist_information_officer_en`

以上 initial question 与 goals 已由自动测试逐字冻结。

## Restaurant Pilot 迁移

`restaurant_reservation` 保留在同一目录中但 `phase=Pilot`，继续使用本地 `restaurant-360` 和现有 barista preset。Formal UI、协议集合校验和 Formal task 日志校验均不会将其视为正式任务；餐厅 panorama 和旧资源未删除。

## 固定 panorama

| 任务 | 本地文件 | 像素 | 比例 | 说明 |
|---|---|---:|---:|---|
| Hotel | `hotel-lobby-360.png` | 1024×1024 | 1:1 | 语义匹配；不是标准 2:1 equirectangular，存在球面拉伸风险 |
| Furniture | `furniture-store-360.png` | 1024×1024 | 1:1 | 语义匹配；存在同上风险 |
| Gym | `gym-360.png` | 1024×1024 | 1:1 | 语义匹配；存在同上风险 |
| Tourist | `tourist-information-360.png` | 2048×1024 | 2:1 | 通过 SiliconFlow `Tongyi-MAI/Z-Image` 在 Developer Mode 一次性生成并保存 |

Tourist importer：Texture2D、sRGB、mipmap 开启、不可读、max size 2048、Android 使用平台默认压缩、quality 50。源 PNG 约 3.67 MB；Android 运行时压缩后的实际占用取决于 Unity 最终选择的 GPU 格式。前三张既有 1:1 图仍是 P1 视觉质量风险，建议阶段 3 前替换为 2:1 并做 PICO 内存/接缝检查。

`TouristPanoramaGenerator` 仅暴露 Developer-only 菜单；读取 `.env` 中的 key，但不保存或打印 key。Formal Mode 下 `PanoramaSceneService` 对缺失本地资源、远程 URL和 fallback 均抛出配置错误。

## Avatar 元数据与待补资源

Catalog 已提供 `avatarPresetKey`、`avatarRole`、`voiceProfileKey`、`spawnPosition`、`spawnRotation`。固定 payload 将这些字段传给 resolver 和 presentation；非空 preset key 只允许精确匹配，找不到时不会回退到教师/咖啡师。

按用户确认，四正式任务 `avatarPresetKey` 当前为空且 `developerPlaceholderAvatar=true`：

- Developer Mode：可使用旧 scenario fallback，日志写入 `avatarFallbackLevel`；
- Formal Mode：`ValidateFormal` 失败，不会把不匹配 fallback 当作成功；
- 队友交付 preset 后，需要为四任务填写 key，并保证对应 `AvatarCatalog` entry 可用、语义匹配且包含最终 prefab/Animator；随后重新运行 Formal validation。

## LLM、场景和条件硬边界

固定启动 payload 使用 Catalog 的 `initialQuestion`、空 `layoutObjects`、本地 `demo://` panorama 与固定 Avatar 元数据。后续 LLM prompt 包含：

- `currentTaskId`；
- task context / goals；
- avatar role；
- roleplay prompt；
- task、scene、panorama、Avatar identity、provider、style 不可变约束。

Formal Mode 的 panorama 在线生成和 fallback、Holodeck 调用均在服务层硬阻断。阶段 1 的 scene-intent、调试和条件覆盖硬锁保持不变。

## UI 与日志

`SceneTalkFlowUiController` 根据 Catalog 和 protocol phase 动态生成按钮，显示 display name、context 和 opening question。Formal phase 显示四正式任务，Pilot phase 只显示 Pilot 任务；UI 不再硬编码 Restaurant/Furniture/Gym/Hotel。

JSONL 与 CSV 新增并真实初始化/更新：

- `taskCatalogVersion`, `taskId`, `taskPhase`, `taskName`, `taskContext`, `taskGoals`, `initialQuestion`；
- `panoramaResourceKey`, `panoramaSource`, `sceneMode`；
- `avatarPresetKey`, `resolvedAvatarPresetKey`, `avatarFallbackLevel`, `voiceProfileKey`；
- `whetherHolodeckCalled`, `whetherImageGenerationCalled`。

Avatar resolver 完成后通过 `RecordAvatarResolution` 更新实际 resolved key 和 fallback level。固定场景初始化为 local / no Holodeck / no image generation。

## Formal validation

新增检查：

- Formal task 恰好四个且必须是 Hotel、Furniture、Gym、Tourist；
- taskId 唯一，Restaurant 只能为 Pilot；
- scenario/context/goals/initialQuestion/role/voice/prompt 完整；
- 每任务恰好四个非空 goals；
- local panorama 可由 `Resources.Load` 加载；
- protocol formalTaskIds 与 Catalog 双向一致；
- Avatar preset 非空且不得是 Developer placeholder。

当前 Preflight 对 Task Catalog 的唯一失败是四个 Avatar preset 待补；协议五项研究决策仍按要求失败。现有 PICO/OpenXR 和 LAN voice 配置问题属于后续真机准备，不在阶段 2 范围内。

## Unity MCP / Unity Skills 验证结果

- C# 编译：通过；0 errors；存在 6 条既有 obsolete/unused warnings。
- Console 最终检查：0 errors/exceptions。
- Scene health：0 errors、0 warnings；仅重复名称/空容器 info。
- Missing references：0。
- Preflight：主场景、Build Settings、Recovery 排除、协议/Catalog/RuntimeConfig 绑定均通过；预期阻断见上文。
- EditMode：29/29 passed。
- PlayMode 自动测试：`DeveloperMode_MainMenuAndFourCatalogTasksStartOffline` 在 Unity `TestResults.xml` 中为 Passed（1.704 s）。Unity Skills 的异步 wrapper 在域重载后错误报告 “cannot recover”，但 Unity Test Runner 原始结果文件明确记录用例通过。
- 最终最小 Play Mode：6.002 s，healthy=true，0 runtime errors。
- 四任务离线路径：PlayMode 用例依次通过 `LoadAssignedTask` 加载四个 task，验证本地 texture、固定 initial question、空 layout 和动态 UI；未发送网络请求。
- 物理断开网卡：未执行，以免影响用户机器；自动测试验证的是不依赖网络的固定资源路径。
- PICO 真机：未执行，不能据此声称通过。

## 已知风险与阶段 3 输入条件

1. 队友交付四个语义匹配、mobile-ready 的 Avatar preset，并完成 key/voice/Animator/位置验收。
2. 五项研究决策仍需团队确认；本阶段没有赋默认值或绕过 Formal 锁。
3. Hotel/Furniture/Gym panorama 建议替换为标准 2:1，并进行接缝、压缩和 PICO 内存验证。
4. PICO OpenXR controller profile、define、LAN Voice Gateway 仍需后续配置和真机验证。
5. 阶段 3 allocator 应只调用 `LoadAssignedTask(taskId)`，不得重新引入 Scene YAML 或 UI 硬编码任务。

阶段 2 未实现 participant allocator、condition sequence、无放回分配、Goal Tracking、问卷、Pilot Agent、uptake 或 PICO 正式验收。
