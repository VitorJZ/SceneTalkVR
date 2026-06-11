# SceneTalkVR 跨端集成与架构重构阶段性报告

**报告日期**: 2026年6月9日
**参与协同**: Agent A (Unity 客户端开发) & Agent B (Python 后端开发)
**撰写人**: Agent A (经汇总梳理)

---

## 1. 项目愿景与架构演进

**SceneTalkVR** 旨在打造一个生成式英语口语练习系统。用户只需一句自然语言（如 "I want to practice ordering coffee"），系统即能实时构建出一个身临其境的 VR 对话环境。

为了在移动端 VR 头显（如 PICO）上保障性能，我们放弃了将极其厚重的 Holodeck（含海量 3D 资产、CLIP 模型、AI2-THOR 物理引擎）直接打包进客户端的原始方案，转而采用了一套**高度解耦的「端云协同 + 混合渲染」新架构**。

### 1.1 新架构全链路数据流
1.  **意图发起 (Unity 侧)**：客户端捕获用户指令，调用大模型提取任务意图（`taskType`, `environmentType`）。
2.  **并发请求 (Unity 侧)**：
    *   *链路 A*：向生图 API 请求当前环境的 360 度全景图。
    *   *链路 B*：向 Holodeck 后端发起 3D 布局请求。
3.  **智能推演 (Python 侧)**：后端 Holodeck 利用大模型规划房间结构、挑选家具，并计算位置坐标，过滤后以轻量 JSON 返回。
4.  **混合渲染 (Unity 侧)**：客户端将下载的 360 全景图作为远景天空盒，并在用户近处（3米内）根据后端的坐标加载本地低模预制体（Prefabs），完成虚拟与现实（远景图像与近景交互）的融合。

---

## 2. Agent A (Unity 客户端) 工作成果与突破

在本次集成中，客户端实现了从“硬编码假数据”到“全真动态数据驱动”的蜕变。

### 2.1 大脑换芯：接入交大本地模型 (Phase 1)
*   **重构 `ISceneTalkBrain`**：开发了全新的 `RealLLMService.cs`，彻底替换了依赖硬编码 JSON 的 `DemoBrainModule`。
*   **API 桥接**：成功对接上海交通大学大模型 API 代理网关，将底层的模型驱动引擎切换为高推理能力的 `minimax-m2.7`。
*   **容错与清理**：针对大模型返回结果常带有 Markdown 标记（如 ````json`）的问题，编写了健壮的字符串清洗器 `CleanJsonString`，确保 `JsonUtility` 能够百分之百成功反序列化 `SpringScenePayload`。

### 2.2 沉浸感升级：引入硅基流动生图 (Phase 2)
*   **开发 `PanoramaSceneService.cs`**：抛弃了旧版轮询接口，直接对接 SiliconFlow (硅基流动) 的 `/images/generations` 同步接口。
*   **Prompt 工程增强**：通过在用户意图后硬编码后缀 `", 360 degree equirectangular panorama, highly detailed, seamless"`，精准控制 `Tongyi-MAI/Z-Image` 模型产出可无缝拼接的天空盒纹理。
*   **动态材质替换**：实现了下载纹理、实例化 `Skybox/Panoramic` 材质并实时更新 `RenderSettings.skybox` 的全自动流程。

### 2.3 物理骨架与混合渲染核心 (Phase 3 & 4)
*   **开发 `HolodeckSceneService.cs`**：实现了向 Python 后端 `localhost:8080/generate_scene` 请求 3D 坐标 JSON 的 HTTP 服务，并将超时时间放宽至 300 秒以适配后端的冷启动。
*   **打造中枢 `HybridScenePresenter.cs`**：这是本次客户端最核心的调度器。它利用 `Task` 并发等待全景图和 3D 布局数据，实现双线合并。
*   **制定通信协议与资产白名单**：在 `Assets/Docs/` 目录下确立了《统一场景 JSON 协议》与《PrefabKey 白名单》，强制隔离了后端的混乱命名。
*   **智能降级机制 (Fallback)**：
    *   通过 `MapToPrefabKey` 模糊匹配，将 Holodeck 吐出的生僻词（如 `reclaimed_wood_table`）安全映射到本地库（如 `cafe_table`）。
    *   自动生成了 22 个带有语义颜色编码的基础几何体预制体（如咖啡色方块代表桌子）。
    *   即使映射彻底失败，也能安全降级为 `generic_decor`（青色块），彻底解决了客户端因找不到资产而崩溃的灾难。

---

## 3. Agent B (Holodeck 后端) 工作成果与突破

后端不仅实现了接口的封装，更对庞大臃肿的 `ai2holodeck` 进行了外科手术级别的性能榨取与工程改造。

### 3.1 核心接口与数据过滤
*   **FastAPI 框架搭建**：在 `app.py` 中实现了标准化的 `POST /generate_scene` 接口。
*   **空间截断**：实现了 3 米半径（欧几里得距离）的过滤逻辑，精准剔除远处无用的家具，极大减轻了客户端的渲染压力。
*   **坐标系换算**：将 AI2-THOR 内部的字典格式转换为 Unity 所需的 `[x, y, z]` 与 Yaw 角度。

### 3.2 深度优化与排雷战记
1.  **突破依赖地狱**：强制隔离至 `holodeck` Conda 环境 (Python 3.10)，手工锁定降级了 `huggingface_hub` 和 `moviepy`，解决了 `torch` 与旧版 `numpy` 的死亡连锁。
2.  **API 网关逆向适配**：由于原 Holodeck 代码库强绑定 OpenAI 旧版文本接口，导致交大模型代理报错。Agent B 重写了底层调用，注入了 `ChatOpenAI` 和 `HumanMessage` 包装器，成功点亮了 `deepseek-chat`。
3.  **输出解析鲁棒化**：面对深度思考模型喜欢输出大量 Markdown 前缀的问题，在 `rooms.py` 中加入了强大的切片检索逻辑，实现了从“废话”中强行“挖”出合法 Python 数组的惊人稳定性。
4.  **真·无头运行 (Headless)**：通过 `xvfb-run` 拦截并“欺骗” AI-THOR 的验证逻辑，使得原本强制弹出的 Unity 3D 视窗被无形化解，保障了后端的静默服务属性。
5.  **暴力提速（解决世纪超时）**：
    *   **问题**：由于物理干涉解算、CLIP+SentenceTransformer 全库高维检索、以及大模型倾向于生成大量“嵌套小物件”（如桌子上的四个咖啡杯），导致初期生成一个场景动辄几十分钟甚至崩溃卡死。
    *   **改造**：
        *   在 `@app.on_event("startup")` 实现 GB 级语言和视觉模型的提前“预热”（Warm-up）。
        *   强行切断物理布局验证 (`use_milp=False`)。
        *   在 Prompt 级别强制勒令模型 `“只选恰好 3 个核心大件”`。
        *   在代码层面暴力清空 `dict[key]["objects_on_top"]`，无情抹杀所有桌面嵌套小物件。
    *   **成效**：成功将几十分钟的无底洞耗时暴降至 **数十秒内**，彻底跑通了从前端等待到后端返回的时间轴。

---

## 4. 总结与未来展望

通过本次横跨 Unity C# 客户端与 Python FastAPI 后端的联合开发，我们不仅成功验证了**“轻量前台混合渲染 + 沉重后台语义解算”**的创新架构，更在工程上解决了大量组件版本兼容、大模型幻觉处理、以及极端耗时问题。

**SceneTalkVR 目前已经正式具备了：一句话 -> 大脑意图分解 -> 呼叫全景图 -> 后端推演物理坐标 -> 眼前自动刷出桌椅的魔法能力！**

**建议的下一步工作 (Phase 5+)：**
1.  **资产替换与美化**：目前 Unity 客户端使用的是根据白名单自动生成的“彩色方块”低模。你可以开始寻找好看的开源 3D 模型，替换掉 `Assets/SceneTalkVR/Prefabs` 下的同名预制体。
2.  **语音交互闭环**：接入 STT 和 TTS 模块。让放置在场景中的那个 Avatar 真正“活”过来，根据 `dialogueReply` 朗读欢迎词，并开始与用户的多轮口语对练。
3.  **场景复用与缓存**：为 Python 后端增加 Redis 或本地 JSON 缓存层，相同的场景（如 "coffee shop"）第二次请求时可以实现毫秒级瞬间秒出，彻底消除那等待的数十秒。