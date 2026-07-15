# SceneTalk VR - Spring 模块开发与技术总结报告

**文档状态**: 最终总结版本  
**负责模块**: LLM 意图解析、场景生成 (Holodeck 后端 & Unity 混合渲染)、多轮上下文管理、UI 与 Editor 支持  
**分支**: `spring-dev`

---

## 1. 概览 (Overview)

SceneTalk VR 采用“Unity 客户端为主，AI/场景生成模块服务端解耦”的路线架构。在本学期的开发中，**Spring** 主要负责“LLM 大脑与场景生成”这一核心模块的开发。

本项目通过将大语言模型 (LLM) 和 AI2-THOR/Holodeck 结合，实现了动态根据用户意图生成 3D VR 语言练习场景的能力。Spring 成功将实验性的 Holodeck 生成链路转化为能在 Unity VR 中稳定渲染的“混合渲染 (Hybrid Rendering)”方案，并主导了相关的前后端对接、空间裁剪、白名单过滤以及 LLM 上下文清理等核心工程难点。这些工作不仅使得项目顺利达到了实机视频演示的标准，也为后续 CCF-A 级别会议的投稿奠定了坚实的系统基础。

---

## 2. 核心技术点与实现细节 (Key Technologies)

### 2.1 基于 Holodeck 的 3D 场景生成与混合渲染

这是本项目中最具学术创新和工程挑战的部分。为了避免将庞大繁杂的 3D 生成栈塞入 Unity 导致崩溃或卡顿，我们采用了后端 JSON 下发 + 客户端轻量级渲染的架构。

*   **Holodeck FastAPI 独立后端搭建 (`Holodeck/app.py`)**:
    *   搭建了基于 Python FastAPI 的服务端，提供 `/generate_scene` 接口。
    *   **核心逻辑**: 接收用户的自然语言描述，调用 Holodeck 接口获取包含室内布局坐标的场景字典。
    *   **数据清洗**: 在 Python 端完成数据清洗，包括自动识别单位 (将超过 50 的 CM 坐标转化为 M)、根据原点过滤出距离小于 15 米的有效物体，并清洗出纯净的 `name`, `position` 和 `rotation` 数据供 Unity 客户端读取。
*   **客户端空间裁剪 (Spatial Clipper / Bounds Clamping)**:
    *   由于全景图和 3D 模型同时存在，AI 生成的位置可能与 VR 玩家(Avatar)发生重叠。
    *   在 `HybridScenePresenter.cs` 中实现了空间裁剪机制，通过限定安全坐标范围（如 $x \in [-1.2, -0.7]$, $z \in [1.5, 2.0]$），强制生成的家具只出现在视野左前方的安全区域，杜绝了穿模问题。
*   **白名单过滤机制 (Whitelist Filter)**:
    *   AI2-THOR 经常生成过多的小物件（如叉子、盘子、纸）导致 VR 画面杂乱。
    *   在渲染层实现了白名单系统（`table`, `chair`, `desk`），直接剔除无关的杂物，保证“极简风格低模（Low Poly）”与高清全景图结合时的视觉和谐。
*   **资产映射与关键字模糊匹配 (Fuzzy Keyword Mapping)**:
    *   AI 生成的物体名带有后缀（如 `DiningTable_1`），为与 Unity 资产名解耦，我们在 `HybridScenePresenter.cs` 内实现 `MapToPrefabKey`，通过字符串包含判断提取主名称（如包含 `table` 则映射为 `table`）。
    *   这种设计极大地提高了 `SceneTalkAssetCatalog` (资产目录) 的灵活性，后续成功将低模家具回退稳定为 `Dining Set` 包中的预制体。

### 2.2 360° 全景图降级方案与天空盒管理 (Panorama Integration)

*   配合 Skybox AI 等全景图 API，负责了场景环境的降级保障方案。
*   **材质与天空球动态替换**: 在客户端读取并动态生成 Skybox Material 覆盖当前场景，支持了快速的环境无缝切换。
*   **纯全景模式 (onlyUsePanorama)**: 为了方便在 Inspector 调试或特殊场景需求下关闭 3D 模型，在 `HybridScenePresenter.cs` 中暴露了 `onlyUsePanorama` Toggle 选项，使得系统在不需要实体桌椅时能灵活工作。

### 2.3 LLM 意图解析与对话生命周期管理

*   **API 接入 (`RealLLMService.cs`)**: 使用 UnityWebRequest 将用户的自然语言（或 STT 识别结果）发送至 OpenAI 兼容 API。
*   **指令解析 JSON 化**: 要求大语言模型不仅回复对话，还必须提取环境要求，并按照严格格式返回 JSON，以供场景模块和 Avatar 模块消费。
*   **多轮上下文防泄漏 (Context Leak Prevention)**:
    *   在多轮测试中发现重新启动对话会导致前一场的场景和设定污染当前生成（即上下文泄露）。
    *   Spring 主导实现了 `ISceneTalkSessionReset` 机制，在用户点击 Exit 或系统触发 Session Reset 时，通过 `fix(llm): clear chat history on exit/session reset` 彻底清空对话历史。强制要求下次启动时必须基于全新的 Prompt 进行环境再生成，保证了系统的鲁棒性。

### 2.4 Editor 扩展与动态 UI 适配

*   **Editor 自动化构建修复 (`SceneTalkRebuild.cs`)**:
    *   支持在 Unity Editor 中进行一键式状态配置。
    *   修复了 Namespace 和类引用的编译错误。实现 Rebuild 时强制重新实例化 `ISceneGenerationService` 和 `ILLMService` 等底层服务，同步最新的 Prompt 和组件，保证切换到实机演示（如真实 Voice Gateway）时的稳定。
    *   保留了团队其他成员（如 Edwin 的性别特征逻辑）的设计结构，未产生覆盖。
*   **VR 动态 UI 多行字幕优化**:
    *   基于 `VerticalLayoutGroup` 和 `ContentSizeFitter` 开发了 VR 内的字幕自适应组件。
    *   解决了长文本溢出屏幕的严重问题，通过 `fix(ui)` 相关提交，确保 AI 生成的长篇回复能够在 UI 面板中良好地自动折行显示，大幅提升 VR 观感体验。

---

## 3. Git 协作与工程设计规范

本模块严格遵循了现代软件开发的流程与代码规范：
1. **原子化提交 (Atomic Commits)**: 所有的开发均限定在 `spring-dev` 分支，按照 `feat`, `fix`, `docs` 等规范提交（例如：`feat(scene): implement safety clipper bounds and prefab whitelist filters...`），保障了历史清晰、可回溯。
2. **面向接口编程 (Interface-Oriented Design)**: 在整合 Holodeck 与 LLM 时，不直接修改底层业务，而是实现相应的 Service 接口，这极大地方便了使用 Mock Data（假数据）和 Real API 之间的热插拔切换，符合项目最初的“多防线保底”策略。
3. **敏捷迭代与废弃第二防线**: 在最后的测试阶段，通过空间裁剪的完美表现，Spring 果断建议并移除了需要手动干预坐标的“第二防线”，直接依靠真实 Holodeck 接口完成渲染，最终成功支持了全流程录屏演示。

---

## 4. 总结

Spring 在 SceneTalk VR 中完成了从“大语言模型语义解析”到“3D 空间计算与物体实例化”的关键衔接。所开发的 **Holodeck 混合渲染架构** 和 **空间白名单裁剪系统** 兼顾了生成灵活性和 VR 展现的严谨性，达到了实机演示的预期目标。这份成果也是后续项目向国际学术会议推进时的核心技术依据。
