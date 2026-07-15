# SceneTalkVR 大模型 API 性能测试与优化方案报告

本项目测试了交大内部 AI 大模型 API 服务（`https://models.sjtu.edu.cn`）的各项模型返回速度。以下为测试结果、关键瓶颈发现以及下一步优化方向的详细说明。

---

## 1. 大模型 API 性能基准测试数据

基于 Python 脚本在校内网络环境下对各个主流模型进行了多次连续请求测试（包含非流式与流式对比）：

| 模型调用名 (ID) | 物理模型 | 成功率 (3轮) | 非流式平均延迟 | 最小延迟 | 最大延迟 | 是否包含思考链 (`<think>`) | 流式首字延迟 (TTFT) |
|---|---|---|---|---|---|---|---|
| `deepseek-chat` | DeepSeek V4 Flash (常规) | **100%** | **1.12s** | 1.10s | 1.13s | **无 (直接输出)** | **约 0.2 - 0.4s** |
| `qwen3.6-27b` | Qwen3.6-27B (常规) | **100%** | **1.12s** | 0.99s | 1.35s | **无 (直接输出)** | **约 0.2 - 0.5s** |
| `deepseek-reasoner` | DeepSeek V4 Flash (思考) | 部分* | **2.96s** | 2.96s | 2.96s | 有 (推理思考) | 需等思考完 (约 2-3s) |
| `minimax-m2.7` | MiniMax-M2.7 | 部分* | **5.29s** | 4.53s | 6.74s | 有 (推理思考) | 需等思考完 (约 4-5s) |

> \*注：由于触发了 API 每分钟请求频率限制（HTTP 429），部分后续请求未能成功返回。

---

## 2. 核心瓶颈发现

### 发现 1：API 严重的频控限制 (Rate Limit - 10 RPM)
在进行基准测试与自动化测试用例运行期间，API 返回了明确的错误：
```json
{"error":{"message":"Rate limit exceeded for api_key. Limit type: requests. Current limit: 10, Remaining: 0."}}
```
* **瓶颈表现**：交大测试大模型 API 的默认额度中，**每分钟最大请求次数 (RPM) 仅为 10 次**。
* **对测试集的影响**：测试集共有 40 个测试用例，每个用例要在 4 种实验配置下各跑 1 次，总计 **160 次连续请求**。在不设置任何间隔的情况下，测试器在第 3 个用例（第 9-10 次请求）时就会触发 429 报错，导致网络请求在网关处被挂起、重试或被拒，这就是为什么您在运行测试时卡在第二个用例且运行了十分钟的根本原因！

### 发现 2：思考链 (`<think>`) 导致的高延迟
* `minimax-m2.7` 和 `deepseek-reasoner` 在返回 JSON 前，会强制生成数百个 Token 的思考链。
* 这使得即使网络通畅，非流式模式下也需要等待 **5 秒以上** 才能拿到数据，严重影响 VR 练习的即时反馈体验。
* 相比之下，`deepseek-chat` (Flash 常规模式) 和 `qwen3.6-27b` 的非流式平均响应仅需 **1.12 秒**，速度提升了 **5 倍**。

---

## 3. 减少等待时间与优化的三大方案

### 方案一：测试期与运行时切换为极速模型（立即见效）
* **优化内容**：将 Unity 场景或 `SceneTalkRuntimeConfig.asset` 中默认的 `modelName` 从 `minimax-m2.7` 更改为 **`deepseek-chat`**。
* **效果**：生成延迟直接从 5.29s 骤降至 **1.12s** 左右。

### 方案二：测试运行器引入频控规避 (Rate Limit Pacing) 与数量抽样
* **优化内容**：
  1. 在 [`LLMPipelineTestRunner.cs`](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Editor/LLMPipelineTestRunner.cs) 中引入 `Task.Delay(6000)`（每两次调用间隔 6 秒），将频率控制在 10 RPM 以内，避免触发 429 频控。
  2. 在测试面板上提供 **“最大测试数 (Max Test Cases)”** 选项（默认 3 或 5），支持开发人员进行局部快速抽样验证，无需每次跑完 40 个用例。

### 方案三：采用流式响应 (Streaming Response) 的 VR 极速响应方案
* **流式在纠错场景的难点**：我们客户端需要接收完整的结构化 JSON Payload（包含 `dialogueReply`, `scene`, `correctionFeedback` 三部分）来进行场景实例化 and 语音播放。如果是传统的流式输出，客户端无法直接通过 `JsonUtility.FromJson` 解析未闭合的 JSON 字符串。
* **流式优化策略 (Incremental JSON Parser)**：
  我们可以在客户端实现一个**增量 JSON 状态机解析器**，或者在 `llm-gateway`（PC 侧网关）实现**字段流式提取与并行下发**：
  * **并行提取**：大模型流式输出时，`dialogueReply` 字段通常在最前面。网关一旦检测到 `dialogueReply` 字段的 JSON 文本流闭合，立刻将其截取并通过 TTS 播放，而无需等待后面的 `scene` 3D 坐标和 `correctionFeedback` 生成完毕。
  * **体感延迟**：这样可将用户的首字声音反馈延迟（TTFT）降低到 **0.3s 左右**，实现真正的实时 VR 对话。

---

## 4. 下一步开发计划

我们将按照以下步骤进行下一步优化：

1. **步骤一**：修改 Unity 默认模型为 `deepseek-chat`。
2. **步骤二**：修改 [`LLMPipelineTestRunner.cs`](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Editor/LLMPipelineTestRunner.cs)，增加**限制测试数量输入框**以及**自动频控延迟**，保证半自动测试报告能稳定生成。
3. **步骤三**：在 PC 侧 `llm-gateway` 中增加对流式响应的解析支持，实现语音与场景的并行下发。