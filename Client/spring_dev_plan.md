# SceneTalk VR 协同开发指南 (Spring 专属)

**核心协同模式：** 
*   **人类（Spring）：** 负责大模型 Prompt 设计、外部 API Key 申请、Python 后端（Holodeck / FastAPI）的本地环境搭建与测试。
*   **AI (Agent)：** 负责通过 Unity-MCP 抓取现有客户端代码接口、编写 C# 网络请求代码、将外部 API 接入 Unity，并在 Unity Editor 中自动挂载和联调。

---

## 阶段一：打通真实 LLM 意图解析与对话中枢

目前客户端在运行 `SceneTalkVR/Setup/Rebuild Demo Rig` 后点击 Start Practice 会走假数据。本阶段要接入 GPT-4o（或同等大模型）进行真实的意图解析。

### 👩‍💻 人类任务 (Spring)
1.  **准备 API Key：** 获取 OpenAI (或你选用的 LLM) 的 API Key。
2.  **编写核心 Prompt：** 准备两套 System Prompt。
    *   *解析 Prompt：* “你是一个 VR 场景调度员，根据用户的指令，输出 JSON 格式，包含 `task`, `environment`, `avatar_trait` 三个字段。”
    *   *对话 Prompt：* “你现在是 `environment` 里的 `avatar_trait`，你需要和用户进行口语对话练习...”
3.  **环境变量配置：** 在你的电脑本地设置好环境变量（如 `OPENAI_API_KEY`），以便 Agent 写的代码可以安全读取，避免将 Key 硬编码到代码中。

### 🤖 交给 Agent 的指令 (Prompt for Agent)

> **@Agent，请使用 Unity-MCP 协助我完成【阶段一：真实 LLM 服务接入】。**
> 
> **任务 1：分析现有接口**
> 请通过 Unity-MCP 读取 `Client/Assets/SceneTalkVR` 目录下的代码，找到目前处理 LLM 的假模块（如 `FakeLLMService` 或相应的 Interface，可能是 `ILLMService`）。告诉我它的输入和输出数据结构是什么。
> 
> **任务 2：开发 RealLLMService**
> 1. 在 `Client/Assets/SceneTalkVR/Scripts/Services`（如果没有则创建）下，编写一个新的 C# 脚本 `RealLLMService.cs`，实现上述的接口。
> 2. 使用 `UnityWebRequest` 或现有的 C# HTTP 库，编写调用外部大模型 API（如 OpenAI Chat Completions）的代码。
> 3. **要求：** 
>    - API Key 必须通过 `Environment.GetEnvironmentVariable` 读取，绝对不能硬编码。
>    - 包含两个核心方法：一个是 `ParseIntentAsync(string userInput)`，返回包含 task/environment/avatar 特征的 JSON；另一个是 `GenerateReplyAsync(string chatHistory)`，返回流式或完整的对话字符串。
> 4. 使用 Unity-MCP 将 `RealLLMService` 替换掉当前主状态机（或 Service Locator / DI 容器）中挂载的假服务。
> 
> **任务 3：Editor 内测试**
> 请在 Unity Editor 中模拟传入一条用户语句（例如 "I want to order a coffee"），运行并验证解析出的 JSON 是否正确打印在 Console 中。

### ✅ 本阶段验收标准
*   Agent 编写的代码不破坏 Vitor 原有的主状态机流程。
*   在 Editor 中运行 Demo，点击测试按钮后，能在 Console 看到真实的 LLM 返回并成功反序列化为 C# 对象。

---

## 阶段二：打通 360 全景图场景生成 (降级/保底方案优先)

为了快速验证场景生成的可行性，我们先实现 PPT 里面提到的降级方案：**外部 API 生成 360 场景图替换天空盒**。

### 👩‍💻 人类任务 (Spring)
1.  **确定全景图 API：** 注册 Skybox AI（Blockade Labs）或类似的 360 图生成 API，并获取 API Key。
2.  **Prompt 翻译逻辑：** 设计如何将阶段一 LLM 解析出的 `environment`（如 "A busy coffee shop"）转化为 Skybox API 需要的英文 Prompt。

### 🤖 交给 Agent 的指令 (Prompt for Agent)

> **@Agent，请使用 Unity-MCP 协助我完成【阶段二：360 全景图场景动态加载】。**
> 
> **任务 1：开发 PanoramaSceneService**
> 1. 查阅场景生成接口（如 `ISceneGenerationService`）。目前客户端只消费 JSON、图片路径或 URL。
> 2. 编写 `PanoramaSceneService.cs`。逻辑为：接收 `environment` 字符串 -> 调用 360 API 生成请求 -> 轮询获取生成的图片 URL -> 使用 `UnityWebRequestTexture` 下载图片。
> 
> **任务 2：动态替换 Skybox**
> 1. 图片下载完成后，使用 Unity-MCP 编写代码，动态创建一个材质球 (Material)，Shader 设置为 `Skybox/Panoramic`。
> 2. 将下载的 Texture2D 赋值给该材质球。
> 3. 通过代码 `RenderSettings.skybox = newMaterial;` 动态替换当前场景的天空盒。
> 
> **任务 3：集成与挂载**
> 将 `PanoramaSceneService` 接入流程，确保它的触发时机是在 LLM 返回意图 JSON 之后。使用 Unity-MCP 检查并在 Editor 中运行，确保天空盒能成功被替换。

### ✅ 本阶段验收标准
*   输入环境指令后，Unity 场景的背景能动态变更为网络下载的 360 度全景图。
*   天空盒替换过程不造成主线程长达数秒的卡死（需要 Agent 使用 Async/Await 或 Coroutine）。

---

## 阶段三：Holodeck 3D 后端接入 (进阶核心方案)

这是本项目的难点。因为 Holodeck 需要隔离在独立的 Python 环境中，所以我们采用彻底的前后端解耦。

### 👩‍💻 人类任务 (Spring)
1.  **环境配置：** 在 `Holodeck` 目录下，搭建 Python 虚拟环境，安装相关依赖（注意资料提到需要 macOS/Ubuntu）。
2.  **编写 FastAPI 服务：** 写一个轻量级的 `server.py`。
    *   接口例如 `/generate_scene`。
    *   接收 JSON：`{"environment": "coffee shop"}`。
    *   内部调用 Holodeck 逻辑生成房间布局。
    *   返回 JSON 给 Unity：`{"objects": [{"name": "Table", "x": 0, "y": 0, "z": 1}, {"name": "CoffeeCup", "x": 0, "y": 0.8, "z": 1}]}`。
3.  **本地运行：** 确保 `localhost:8000/generate_scene` 可以正常跑通并返回 JSON 数据。

### 🤖 交给 Agent 的指令 (Prompt for Agent)

> **@Agent，请使用 Unity-MCP 协助我完成【阶段三：Holodeck 3D 后端数据消费】。**
> 
> **背景：** 我已经在本地启动了一个 Python 后端，它将负责调用 Holodeck 并在 `http://localhost:8000/generate_scene` 返回场景布局的 JSON 数据。客户端不需要打包复杂的生成逻辑，只需消费这些数据。
> 
> **任务 1：开发 HolodeckSceneService**
> 1. 编写 C# 脚本发送 HTTP POST 请求到本地 Python 服务器。
> 2. 解析返回的 JSON 数据（包含物体名称和三维坐标）。
> 
> **任务 2：动态加载预制体 (Prefab Instantiation)**
> 1. 根据 JSON 中的物体名称（如 "Table", "CoffeeCup"），通过 `Resources.Load` 或 Addressables 从本地预设库中加载对应的低多边形 3D 模型。
> 2. 在指定的 `(x, y, z)` 坐标上实例化这些物体。
> 
> **任务 3：构建混合场景**
> 使用 Unity-MCP 修改状态机逻辑，实现“混合渲染”方案：先调用阶段二的 API 铺设 360 图作为背景，然后在用户周围（近处坐标）实例化阶段三从 Holodeck 后端返回的 3D 交互物体。

### ✅ 本阶段验收标准
*   Spring 的 Python 服务台能收到来自 Unity 的 HTTP 请求。
*   Unity 端能正确解析 JSON，并且能在场景原点附近生成出带有 Collider 的 3D 物体（例如桌子）。

---
