# SceneTalkVR × Holodeck 阶段性集成开发方案

## 0. 项目目标

将已经跑通的 Holodeck 场景生成能力逐步集成到 SceneTalkVR 项目中，使用户可以通过自然语言指令生成适合英语口语练习的 VR 场景，并最终在 Unity/PICO 客户端中显示与交互。

本方案不要求短期内把 Holodeck 的完整 AI2-THOR 场景原样搬进 Unity。推荐采用“后端生成语义场景，前端轻量渲染”的解耦路线：

用户语音或文本输入
→ Spring 后端理解任务和场景需求
→ Holodeck 生成或辅助生成场景布局
→ 后端转换成 SceneTalkVR 统一 JSON
→ Unity/PICO 客户端根据 JSON 加载本地 prefab、skybox、Avatar 与对话流程

## 1. 当前已完成前提

Spring 本地已经完成以下工作：

1. Holodeck Python 环境已安装完成。
2. AI2-THOR 已能启动，并且图形界面可以在 WSL 中显示。
3. Holodeck 能根据自然语言请求生成 coffee shop 场景。
4. Objathor/Holodeck 数据包已下载完成。
5. assets、annotations、features 已经可被原脚本识别。

当前关键本地路径如下：

```text
Holodeck base data:
~/.objathor-assets/holodeck/2023_09_23

Objathor assets:
~/.objathor-assets/2023_09_23/assets

Annotations:
~/.objathor-assets/2023_09_23/annotations.json.gz

Features:
~/.objathor-assets/2023_09_23/features

AI2-THOR runtime processed models:
~/.ai2thor/releases/thor-Linux64-3213d486cd09bcbafce33561997355983bdf8d1a/processed_models
```

在 Spring 当前机器上，绝对路径为：

```text
/home/spring5/.objathor-assets/holodeck/2023_09_23
/home/spring5/.objathor-assets/2023_09_23/assets
/home/spring5/.objathor-assets/2023_09_23/annotations.json.gz
/home/spring5/.objathor-assets/2023_09_23/features
/home/spring5/.ai2thor/releases/thor-Linux64-3213d486cd09bcbafce33561997355983bdf8d1a/processed_models
```

注意：这些资产路径主要用于后端调试、资产筛选和初步 demo 资产挑选。不要假设这些路径在其他开发者机器或 Unity 项目中天然存在。最终应该通过配置项、文档或导入流程管理。

---

# 总体开发路线

本项目采用三阶段推进：

## 阶段 1：轻量 JSON 场景集成

目标：让 Unity/PICO 客户端通过后端返回的 JSON 显示一个动态生成的轻量场景。

核心策略：

* Holodeck 运行在 Python 后端。
* Unity 不直接运行 Holodeck。
* 后端返回 SceneTalkVR 统一场景 JSON。
* Unity 根据 prefabKey 加载本地低模 prefab。
* 先做 coffee shop、airport、restaurant、office 等少数场景。
* 优先保证“可演示、可联调、低延迟、稳定”。

## 阶段 2：360 Skybox + 本地 prefab 混合显示

目标：增强沉浸感，但保持 PICO 端性能稳定。

核心策略：

* 后端返回 skyboxUrl 或本地 skybox 资源 key。
* Unity 设置 360 背景或天空盒。
* 近处仍然使用本地 prefab 作为可交互物体。
* 形成“远景沉浸 + 近景交互”的混合渲染方案。

## 阶段 3：Holodeck/Objathor 资产精选导入 Unity

目标：从已下载的 3D 资产中挑选少量质量好、适合 demo 的对象，转换或导入 Unity 项目，作为本地 prefab 库的一部分。

核心策略：

* 不批量导入所有 Objathor 资产。
* 只精选少量 demo 需要的资产。
* 优先导入桌子、椅子、柜台、沙发、植物、货架、咖啡杯、机场标识等高频物体。
* 所有导入资产都必须经过性能检查、材质检查、碰撞体处理和 prefabKey 绑定。

---

# 阶段 1：轻量 JSON 场景集成

## 1.1 目标

完成一个最小可用的 Spring 后端服务，使 Unity 客户端能够发送用户输入，并收到标准化场景响应。

输入示例：

```json
{
  "userText": "I want to practice ordering coffee in a coffee shop.",
  "language": "en",
  "targetScene": "coffee_shop"
}
```

输出示例：

```json
{
  "taskType": "ordering_coffee",
  "environmentType": "coffee_shop",
  "dialogueReply": "Welcome to the coffee shop. What would you like to order?",
  "avatarRole": "barista",
  "scene": {
    "mode": "layout",
    "skyboxUrl": "",
    "layoutObjects": [
      {
        "prefabKey": "coffee_counter",
        "position": { "x": 0.0, "y": 0.0, "z": 3.0 },
        "rotationY": 0
      },
      {
        "prefabKey": "cafe_table",
        "position": { "x": 1.5, "y": 0.0, "z": 1.2 },
        "rotationY": 45
      },
      {
        "prefabKey": "chair",
        "position": { "x": 2.0, "y": 0.0, "z": 1.2 },
        "rotationY": 180
      }
    ]
  }
}
```

## 1.2 需要完成的任务

### 任务 A：定义统一场景协议

Agent 需要输出一份清晰的 SceneTalkVR 场景 JSON 协议文档，至少包括：

1. 请求字段：

   * userText
   * language
   * targetScene
   * optional constraints

2. 响应字段：

   * taskType
   * environmentType
   * dialogueReply
   * avatarRole
   * scene.mode
   * scene.skyboxUrl
   * scene.layoutObjects
   * error 或 fallback 字段

3. layoutObjects 字段：

   * prefabKey
   * position.x
   * position.y
   * position.z
   * rotationY
   * optional scale
   * optional interactable
   * optional label

4. 坐标约定：

   * 后端输出单位统一为米。
   * Unity 中地面平面使用 x/z。
   * y 表示高度，地面物体默认 y = 0。
   * rotationY 表示绕 Unity Y 轴旋转角度。

### 任务 B：定义 prefabKey 白名单

Agent 需要建立第一版 prefabKey 白名单。建议至少包含：

```text
coffee_counter
cafe_table
chair
sofa
plant
wall_shelf
menu_board
cash_register
coffee_mug
lamp
bookshelf
airport_counter
airport_sign
queue_barrier
office_desk
whiteboard
restaurant_table
bed
cabinet
generic_table
generic_chair
generic_decor
```

要求：

1. 后端只能返回白名单中的 prefabKey。
2. 如果 Holodeck 输出未知物体名，必须映射到 generic_xxx。
3. prefabKey 命名需要和 Unity 端资源命名规范保持一致。
4. 每个 prefabKey 需要有中文说明、英文说明和用途说明。

### 任务 C：实现 Spring 后端 mock 服务

Agent 需要提供一个后端服务方案，至少包含：

```text
GET /api/health
POST /api/generate_scene
```

其中：

* `/api/health` 用于 Unity 检查后端是否可用。
* `/api/generate_scene` 用于返回场景 JSON。
* 第一版可以不真实调用 Holodeck，先返回固定 coffee_shop、airport、restaurant 模板。
* 保证 Unity 可以先完成联调。

### 任务 D：接入 Unity 客户端显示

Agent 需要在 Unity 端接入后端返回结果，推荐方式：

1. 新增或改造 SpringHttpBrainModule。
2. 由该模块请求 Python 后端。
3. 将返回 JSON 转成 SceneTalkVR 内部 payload。
4. 交给 SceneTalkScenePresenter 或等价模块显示。
5. Unity 端根据 prefabKey 查本地 prefab 并实例化。

不要改 XR/PICO 底层脚本。不要让 Unity 直接依赖 Holodeck Python 代码。

### 任务 E：完成第一版本地 prefab demo

Agent 需要从 Unity 项目现有资源或简单低模资源中准备第一版 demo prefab。若需要从 Holodeck/Objathor 资产中挑选，可以参考以下路径：

```text
/home/spring5/.objathor-assets/2023_09_23/assets
/home/spring5/.ai2thor/releases/thor-Linux64-3213d486cd09bcbafce33561997355983bdf8d1a/processed_models
```

要求：

1. 只挑选少量资产。
2. 优先挑选 coffee shop 场景需要的资产。
3. 优先选择外观明确、面数较低、材质正常的物体。
4. 初步 demo 中，Unity 可以先用自制低模或占位 prefab，不强制使用 Objathor 原资产。
5. 每个 prefab 都要绑定到 prefabKey。

## 1.3 阶段 1 验收标准

完成以下验证即视为阶段 1 通过：

1. 启动 Python 后端后，Unity 能访问 `/api/health` 并得到正常响应。
2. Unity 发送 coffee shop 请求后，能收到合法 JSON。
3. Unity 场景中能根据 JSON 生成至少 5 个对象。
4. 对象位置、旋转、缩放基本正确。
5. 用户能在 PICO 或 Unity Play Mode 中看到动态布置的 coffee shop 场景。
6. 场景加载失败时有 fallback，不会导致 Unity 卡死或崩溃。
7. 后端接口文档、prefabKey 白名单、测试样例 JSON 已提交到项目文档目录。

---

# 阶段 2：接入 Holodeck Adapter

## 2.1 目标

将 Holodeck 真实生成结果接入 Spring 后端，但 Unity 端仍然只消费统一 JSON。

Holodeck 的职责是：

* 理解自然语言场景需求。
* 生成房间布局。
* 生成墙高、门窗、物体选择和物体摆放。
* 提供可参考的空间语义和物体清单。

Spring 后端的职责是：

* 调用 Holodeck。
* 解析 Holodeck 输出。
* 过滤不适合 Unity/PICO 的复杂数据。
* 转换成 SceneTalkVR 统一 JSON。
* 做缓存和降级。

## 2.2 需要完成的任务

### 任务 A：整理 Holodeck 输出格式

Agent 需要分析 Holodeck 当前运行日志和中间输出，整理出可稳定提取的信息：

1. room/floor plan
2. wall height
3. door/window plan
4. object selection plan
5. object placement
6. wall object placement
7. selected asset IDs
8. AI2-THOR runtime asset 信息

要求：

* 明确哪些字段短期需要给 Unity。
* 明确哪些字段只用于后端调试。
* 明确哪些字段暂时丢弃。

### 任务 B：实现 Holodeck → SceneTalk Payload 映射

Agent 需要建立映射规则：

1. room name → environmentType
2. Holodeck object_name → prefabKey
3. Holodeck position → Unity position
4. Holodeck rotation → Unity rotationY
5. Holodeck room size → Unity floor bounds 或 debug info
6. door/window 信息 → 可选显示对象

示例映射：

```text
communal_table -> cafe_table
sofa -> sofa
wall_shelf -> wall_shelf
small potted succulent -> plant
coffee mug -> coffee_mug
coffee bean jar -> generic_decor
```

要求：

* 所有未知物体都必须映射到 generic_xxx。
* 不允许把无限制的 Holodeck 物体名直接暴露给 Unity。
* 单个场景物体数量需要限制，建议初期不超过 20 个。
* 移动端 PICO 模式建议默认不超过 10–15 个主要物体。

### 任务 C：实现缓存机制

Agent 需要给后端设计缓存策略：

1. 对常见场景缓存：

   * coffee_shop
   * airport
   * restaurant
   * office
   * hotel
   * classroom

2. 缓存内容：

   * 原始用户输入
   * Holodeck 原始输出摘要
   * SceneTalkVR JSON
   * 生成时间
   * 使用的模型和参数
   * 是否人工修正过

3. 缓存命中策略：

   * 用户请求接近已有场景时，优先返回缓存。
   * 缓存没有命中时，才调用 Holodeck。
   * Holodeck 调用失败时，返回默认模板。

### 任务 D：设计降级策略

Agent 需要实现清晰的 fallback 规则：

1. Holodeck 不可用 → 返回固定模板场景。
2. LLM 输出格式错误 → 使用最近一次可用缓存。
3. 物体映射失败 → 使用 generic prefab。
4. 场景过大 → 自动裁剪物体数量。
5. 后端超时 → 返回简化场景和简短 dialogueReply。
6. Unity 加载 prefab 失败 → 跳过该物体并记录日志。

### 任务 E：生成开发调试报告

每次调用 Holodeck 后，后端应保存 debug 信息，便于 Spring 调试：

```text
request.json
holodeck_raw_output.txt
adapter_output.json
final_response.json
errors.log
```

建议目录：

```text
Holodeck/runtime_outputs/
```

或：

```text
Server/runtime_outputs/
```

具体位置由 Agent 根据项目结构决定，但必须写入文档。

## 2.3 阶段 2 验收标准

完成以下验证即视为阶段 2 通过：

1. 后端能真实调用 Holodeck 生成至少一个 coffee_shop 场景。
2. 后端能把 Holodeck 输出转换成 SceneTalkVR JSON。
3. Unity 不需要知道 Holodeck 内部细节，只消费统一 payload。
4. 同一个 coffee_shop 请求第二次能命中缓存，明显减少等待时间。
5. Holodeck 失败时，Unity 仍能得到 fallback 场景。
6. 至少完成 3 类场景的 Holodeck 生成与 JSON 转换：

   * coffee_shop
   * airport
   * restaurant 或 office
7. 每次生成都有可追踪 debug 输出。
8. 后端响应时间、缓存命中率、失败原因能被日志记录。

---

# 阶段 3：360 Skybox + 本地 prefab 混合显示

## 3.1 目标

在轻量布局场景基础上增强沉浸感。不要让 PICO 端承担完整高复杂度 3D 生成，而是采用：

```text
远景：360 skybox 或 panoramic background
中景：静态低模 prefab
近景：少量可交互物体
Avatar：Unity 本地角色
```

## 3.2 需要完成的任务

### 任务 A：扩展 scene.mode

Agent 需要扩展 scene.mode，至少支持：

```text
layout
skybox
hybrid
```

含义：

* layout：只使用本地 prefab 生成空间。
* skybox：主要显示 360 背景。
* hybrid：skybox + 本地 prefab + Avatar。

### 任务 B：定义 skybox 字段

响应 JSON 中应支持：

```json
{
  "scene": {
    "mode": "hybrid",
    "skyboxUrl": "http://backend/static/skyboxes/coffee_shop_001.jpg",
    "skyboxKey": "coffee_shop_001",
    "layoutObjects": []
  }
}
```

字段说明：

* skyboxUrl：后端提供的可下载图片地址。
* skyboxKey：Unity 本地已有 skybox 的 key。
* 如果两者都存在，Unity 优先使用本地 skyboxKey，避免网络延迟。
* 如果本地没有，再尝试 skyboxUrl。

### 任务 C：准备第一批 skybox 素材

Agent 需要准备或接入第一批 skybox 素材：

```text
coffee_shop
airport
restaurant
office
classroom
hotel_lobby
```

要求：

1. 每个 skybox 都要有 key。
2. 分辨率不要过高，优先保证 PICO 性能。
3. 素材要放到 Unity 可管理目录。
4. 后端 JSON 可以引用这些 key。
5. 如果使用外部生成服务或外部图片，需要记录来源和授权情况。

### 任务 D：实现 Unity 端 hybrid 显示

Unity 端需要：

1. 加载 skybox。
2. 清理旧场景对象。
3. 生成少量近景 prefab。
4. 放置 Avatar。
5. 保证用户初始位置和视角合理。
6. 保证 VR 中不会出现物体离用户过近或遮挡 UI。

### 任务 E：性能检查

Agent 需要在 Unity/PICO 端进行基础性能检查：

1. 场景加载时间。
2. 平均帧率。
3. 单场景物体数量。
4. 材质数量。
5. 纹理大小。
6. 内存占用。
7. 是否有明显卡顿或发热。

## 3.3 阶段 3 验收标准

完成以下验证即视为阶段 3 通过：

1. Unity 能显示 coffee_shop skybox。
2. Unity 能在 skybox 中摆放至少 5 个本地 prefab。
3. Avatar 能出现在合理位置。
4. 用户能在 VR 中看到明显区别于普通空场景的沉浸式环境。
5. 至少 3 个场景支持 hybrid 模式。
6. PICO 真机或目标运行环境中没有严重掉帧、黑屏或卡死。
7. 后端可以通过 JSON 控制 layout、skybox、hybrid 三种模式。

---

# 阶段 4：精选 Objathor/Holodeck 资产导入 Unity

## 4.1 目标

从 Spring 已下载的 3D 资产中挑选一小部分适合 demo 的对象，导入 Unity 作为本地 prefab，增强场景真实感。

不要全量导入 assets。只挑选 demo 必需资产。

## 4.2 资产来源路径

Spring 本地资产路径：

```text
/home/spring5/.objathor-assets/2023_09_23/assets
```

每个 asset 通常是一个 UUID 目录，例如：

```text
/home/spring5/.objathor-assets/2023_09_23/assets/34cf668cb2c1478c9de5d7cb5f4d0e56
/home/spring5/.objathor-assets/2023_09_23/assets/2a9fadae585f4bbfa030772e54e64ace
/home/spring5/.objathor-assets/2023_09_23/assets/0f6be8c9cd9d4e1aba2173cabad5ec09
```

AI2-THOR 运行时处理后的模型路径：

```text
/home/spring5/.ai2thor/releases/thor-Linux64-3213d486cd09bcbafce33561997355983bdf8d1a/processed_models
```

Agent 可以检查这些目录中的模型格式、材质文件、贴图文件，并挑选适合导入 Unity 的资产。

## 4.3 需要优先挑选的资产类型

第一批建议挑选：

### coffee shop

```text
coffee_counter
cafe_table
chair
sofa
coffee_mug
plant
wall_shelf
menu_board
lamp
cash_register
```

### airport

```text
airport_counter
airport_sign
chair
queue_barrier
suitcase
information_board
security_gate
```

### office

```text
office_desk
office_chair
whiteboard
bookshelf
computer_monitor
plant
cabinet
```

### restaurant

```text
restaurant_table
chair
counter
plate
cup
plant
lamp
```

## 4.4 资产筛选标准

Agent 需要逐个检查候选资产：

1. 模型能否正常导入 Unity。
2. 材质和贴图是否正常。
3. 模型尺寸是否合理。
4. 面数是否适合 PICO。
5. 是否需要简化模型。
6. 是否需要手动添加 collider。
7. 是否适合 VR 近距离观看。
8. 是否有明显版权或来源风险。
9. 是否能稳定保存成 Unity prefab。
10. 是否能绑定到 prefabKey 白名单。

## 4.5 导入后的 Unity 处理要求

每个导入资产需要处理：

1. 命名规范：

   * `PF_CoffeeCounter`
   * `PF_CafeTable`
   * `PF_Chair`
   * `PF_Plant`

2. prefabKey 映射：

   * coffee_counter → PF_CoffeeCounter
   * cafe_table → PF_CafeTable

3. Collider：

   * 静态装饰物可以用简单 BoxCollider。
   * 可交互物体需要合适碰撞体。
   * 不要默认使用复杂 MeshCollider，除非必要。

4. LOD 或简化：

   * 面数高的模型需要降级或只用于 PC 端。
   * PICO 端优先使用低模版本。

5. 材质：

   * 尽量合并材质。
   * 避免过多高分辨率贴图。
   * 移动端优先使用轻量 shader。

## 4.6 阶段 4 验收标准

完成以下验证即视为阶段 4 通过：

1. 至少 10 个精选资产成功导入 Unity。
2. 每个资产都有 prefabKey 映射。
3. coffee_shop 场景至少使用 5 个精选资产。
4. Unity Play Mode 中所有资产显示正常。
5. PICO 或目标平台中没有明显性能问题。
6. Agent 提交资产清单文档，包含：

   * prefabKey
   * Unity prefab 路径
   * 原始 asset 路径
   * 模型类型
   * 是否可交互
   * 性能备注

---

# 阶段 5：对话系统与场景系统联调

## 5.1 目标

把场景生成从单独 demo 接入完整 SceneTalkVR 体验：

用户语音输入
→ STT 转写
→ Spring 后端解析意图
→ 生成场景与 Avatar 角色
→ Unity 显示场景
→ TTS 播放 Avatar 回复
→ 用户继续对话

## 5.2 需要完成的任务

### 任务 A：统一任务类型

Agent 需要定义 taskType，例如：

```text
ordering_coffee
airport_checkin
hotel_checkin
restaurant_ordering
office_meeting
classroom_discussion
shopping
asking_directions
```

每个 taskType 需要绑定：

1. 推荐场景。
2. 推荐 Avatar role。
3. 推荐 opening dialogue。
4. 推荐 prefab 组合。
5. 推荐 skybox。

### 任务 B：上下文管理

后端需要维护简单上下文：

1. 当前用户正在练习什么任务。
2. 当前场景是什么。
3. 当前 Avatar 角色是什么。
4. 当前对话轮次。
5. 用户上一次回答。
6. 是否需要纠错、提示或继续问答。

### 任务 C：对话与场景解耦

场景不应该每轮都重新生成。建议：

1. 第一轮识别任务并生成场景。
2. 后续对话沿用同一场景。
3. 用户明确要求换场景时才重新生成。
4. 如果只是继续练习，不调用 Holodeck。

### 任务 D：延迟优化

Agent 需要优化整体响应：

1. 场景生成使用缓存。
2. TTS 使用流式或分段播放。
3. LLM 对话和场景生成分离。
4. 先显示加载状态。
5. 可先返回 dialogueReply，再异步加载场景。
6. 对常见场景预加载。

## 5.3 阶段 5 验收标准

完成以下验证即视为阶段 5 通过：

1. 用户说“我想练习在咖啡店点单”，系统能进入 coffee shop 场景。
2. Avatar 角色正确，例如 barista。
3. 场景加载后，Avatar 能开始第一句对话。
4. 用户继续说话时，不会重复生成场景。
5. 用户要求换成 airport 场景时，系统能切换场景。
6. 系统有加载状态、失败提示和重试机制。
7. 端到端 demo 可连续运行至少 3 分钟不崩溃。

---

# 阶段 6：WSL 本地渲染与开发体验优化

## 6.1 目标

优化 Spring 本地开发体验，使 Spring 能更方便地观察 Holodeck/AI2-THOR 生成效果。

这部分不是最终用户显示方案。最终用户显示仍然在 Unity/PICO。

## 6.2 需要完成的任务

### 任务 A：检查 WSL 图形加速

Agent 需要指导或检查：

```text
glxinfo -B
echo $DISPLAY
echo $WAYLAND_DISPLAY
```

重点确认：

1. 是否使用 WSLg。
2. OpenGL renderer 是否为硬件加速。
3. 是否错误地走 llvmpipe 软件渲染。

### 任务 B：Windows/WSL 更新建议

需要确保：

1. Windows WSL 已更新。
2. 显卡驱动已更新。
3. WSLg 可正常运行 GUI。
4. 必要时调整 `.wslgconfig` 改善 DPI 缩放。

### 任务 C：开发时显示策略

Agent 可以提供两种本地调试模式：

1. GUI 模式：

   * 用于 Spring 观察 Holodeck/AI2-THOR 生成结果。

2. Headless 模式：

   * 用于后台生成和批量测试。
   * 不依赖图形窗口。
   * 适合 CI 或服务端。

## 6.3 阶段 6 验收标准

完成以下验证即视为阶段 6 通过：

1. Spring 本地可以稳定启动 AI2-THOR GUI。
2. 如果 GUI 模糊或性能差，有明确优化文档。
3. 有 headless 或 xvfb 运行说明。
4. Holodeck 后端服务可以不依赖人工观察窗口完成生成流程。
5. WSL 渲染问题不会阻塞 Unity/PICO 集成。

---

# 阶段 7：质量、性能与答辩准备

## 7.1 目标

确保项目可展示、可解释、可降级、可复现。

## 7.2 需要完成的任务

### 任务 A：场景质量评估

Agent 需要为每个场景记录：

1. 场景语义是否匹配用户需求。
2. 物体是否合理。
3. 布局是否可理解。
4. 是否适合英语练习。
5. 是否有明显穿模、遮挡、过密问题。
6. 是否适合 VR 视角。

### 任务 B：性能评估

记录：

1. 后端场景生成耗时。
2. 缓存命中耗时。
3. Unity 场景加载耗时。
4. PICO 端帧率。
5. 单场景物体数量。
6. skybox 纹理大小。
7. 失败率和 fallback 次数。

### 任务 C：演示脚本准备

至少准备 3 条演示输入：

```text
I want to practice ordering coffee in a coffee shop.
I want to practice checking in at the airport.
I want to practice booking a hotel room.
```

每条输入需要有：

1. 预期 taskType。
2. 预期 environmentType。
3. 预期 Avatar role。
4. 预期场景显示。
5. 预期 opening dialogue。
6. fallback 版本。

### 任务 D：答辩说明材料

Agent 需要生成或辅助整理：

1. 系统架构图。
2. 后端场景生成流程图。
3. Holodeck 集成说明。
4. Unity/PICO 显示策略。
5. 三阶段优化路线。
6. 风险与降级策略。
7. 当前 demo 截图或录屏清单。

## 7.3 阶段 7 验收标准

完成以下验证即视为阶段 7 通过：

1. 至少 3 个完整任务场景可稳定演示。
2. 每个场景都有缓存版本。
3. 后端失败时系统仍可演示 fallback。
4. Unity/PICO 端不会因后端失败而崩溃。
5. 有清晰的技术说明和风险应对材料。
6. 有可复现的运行步骤文档。

---

# 推荐开发顺序

Agent 应按以下顺序执行，不要跳阶段：

## 第一步：协议先行

先完成：

```text
SceneTalkVR JSON 协议
prefabKey 白名单
接口文档
mock response 示例
```

## 第二步：Unity 接 mock

先不调用 Holodeck，让 Unity 能显示后端固定 JSON。

## 第三步：Holodeck Adapter

将 Holodeck 输出转换成统一 JSON。

## 第四步：缓存与降级

让 demo 稳定，而不是每次都依赖实时生成。

## 第五步：skybox 混合显示

增强沉浸感，同时控制性能。

## 第六步：精选资产导入

从 Objathor/Holodeck 资产中挑一小部分做高质量 demo prefab。

## 第七步：端到端联调

接入 STT、TTS、Avatar 和对话上下文。

---

# Agent 交付物清单

每个阶段结束时，Agent 至少应提交：

1. 阶段开发说明。
2. 修改文件清单。
3. 运行步骤。
4. 测试步骤。
5. 验收结果。
6. 已知问题。
7. 下一阶段建议。

最终应提交：

```text
docs/SpringScenePayload.md
docs/PrefabKeyWhitelist.md
docs/HolodeckIntegrationPlan.md
docs/HolodeckAssetImportReport.md
docs/BackendApi.md
docs/DemoRunbook.md
```

建议最终目录结构由 Agent 根据仓库现状决定，但文档必须能让 Vitor、Spring、Edwin 三人分别知道自己如何联调。

---

# 重要工程原则

1. Holodeck 不直接塞进 Unity 客户端。
2. Unity/PICO 只消费轻量 JSON、skybox key、prefabKey。
3. PICO 端优先稳定帧率，不追求全量高精 3D 资产。
4. 后端必须有缓存和 fallback。
5. 不让每轮对话都重新生成场景。
6. 所有 prefabKey 必须白名单化。
7. 所有坐标单位统一为米。
8. Unity 地面平面使用 x/z，y 为高度。
9. Objathor 资产只精选导入，不全量导入。
10. 演示优先保证稳定、低延迟、可复现。

---

# 当前最小可行目标

Agent 的第一阶段目标应非常明确：

在 Unity/PICO 中展示一个由 Spring 后端返回 JSON 驱动的 coffee shop 场景。

验收画面应包括：

1. 一个咖啡店背景或简单房间。
2. 一个吧台。
3. 至少一张桌子。
4. 至少两把椅子。
5. 一个 Avatar，角色为 barista。
6. 一句开场英语对话。
7. 用户可以进入对话流程。
8. 后端断开时 Unity 不崩溃，而是显示默认 coffee shop 场景。

完成这个目标后，再逐步接入 Holodeck 真实生成、skybox、精选 3D 资产和完整口语训练闭环。

