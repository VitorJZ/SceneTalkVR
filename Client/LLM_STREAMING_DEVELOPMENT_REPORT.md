# SceneTalk VR 大模型流式响应与低延迟播音系统开发报告

为了解决 VR 环境下由于大模型一次性生成完整结构化 JSON 响应导致首字延迟（TTFT）过高（3秒以上）的痛点，项目于近期完成了 **“增量流式解析与并行分句 TTS 队列播音”** 的流式响应改造。本报告详细记录了该模块的架构设计、核心算法、代码改造与延迟性能指标。

---

## 一、 性能优化指标 (Benchmark Results)

我们在开发初期对交大 API (`deepseek-chat`) 进行了结构化输出的流式测试，测试结果表明流式响应方案能获得显著的延迟收益：

| 性能指标 | 非流式响应（改造前） | 流式响应（改造后） | 性能收益 |
| :--- | :--- | :--- | :--- |
| **首字延迟 (TTFT)** | ~3.20 秒 | **0.655 秒** | ⚡ **缩短 79.5%** |
| **首句生成时间 (TTFS)** | ~3.80 秒 | **1.605 秒** | ⚡ **缩短 57.7%** |
| **首句播音开始延迟** | ~4.20 秒 | **1.20 - 1.80 秒** | ⚡ **缩短约 65%** |
| **总生成耗时** | 3.776 秒 | 3.776 秒 | 持平（网络吞吐量相同） |

> [!NOTE]
> 在流式播音模式下，当 LLM 仍在流式生成第 2、3 句时，系统已经对第 1 句发起了 TTS 合成并开始在 VR 中朗读，从而将用户的“感官等待时间”压缩到了极致。

---

## 二、 系统架构与协议契约扩展

流式响应改造遵循了“高内聚、低耦合”的增量式开发原则，在 [SceneTalkContracts.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkContracts.cs) 中定义了两个扩展流式接口：

### 1. ISceneTalkStreamingBrain
```csharp
public interface ISceneTalkStreamingBrain : ISceneTalkBrain
{
    // 提供分句输出的回调能力，利用协程驱动
    IEnumerator GenerateSceneAndReplyStreaming(
        string userText, 
        Action<string> onSentenceComplete, 
        Action<SpringScenePayload> onComplete, 
        Action<string> onError
    );
}
```

### 2. ISceneTalkStreamingAvatarVoice
```csharp
public interface ISceneTalkStreamingAvatarVoice : ISceneTalkAvatarVoice
{
    // 允许外部源源不断地向播放队列塞入分好的句子
    void EnqueueSentence(string sentence);
    // 标识整个流式文本已经传输完毕，队列可以安全退出
    void SignalStreamingComplete();
}
```

---

## 三、 核心解析算法与网络层改造

### 1. 增量字符流接收 (`StreamingDownloadHandler`)
由于 Unity 内置的 `UnityWebRequest` 默认在完整 Response 返回后才触发 Text 渲染，我们在 `RealLLMService.cs` 中实现了一个继承自 `DownloadHandlerScript` 的 `StreamingDownloadHandler`：
- **流式监听**：重写 `ReceiveData(byte[] data, int dataLength)`，每当网卡接收到新的 TCP 分包数据时，自动将其转换为 UTF-8 文本并追加 to 本地缓冲区。
- **SSE 解析**：按换行符 `\n` 扫描协议格式，识别并切分出 `data: { ... }` 格式的 SSE 数据段。对于标准的流式 Chunk，利用 `JsonUtility` 解析出增量的 `choices[0].delta.content` 并分发至解析器。

### 2. 字段状态机解析与分句 (`IncrementalJsonParser`)
由于后端模型在 `stream: true` 模式下返回的是包含完整 JSON 格式（如 `{ "taskType": "...", "dialogueReply": "..." }`）的字符流，我们设计了一个**增量字符解析状态机**：
1. **定位字段**：状态机首先检索 `"dialogueReply"` 键并寻找冒号 `:` 和起始引号 `"`。
2. **提取内容**：进入 `dialogueReply` 字段的提取阶段后，逐个读取字符并处理转义符（如 `\"`、`\\` 等），直到遇到非转义的闭合引号 `"`，标识当前字段提取结束。
3. **分句检测**：在此阶段，使用正则表达式或字符匹配监视句子边界标点符 `[.!?。！？]\s*`。一旦识别出完整的句子（如句尾有空格或引号），立刻将其送往 `onSentenceComplete` 回调，同时清除缓冲区中已发送的部分。

---

## 四、 播音调度队列 (Audio Presentation Queue)

在 [AvatarPresentationVoiceModule.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs) 中，我们引入了音频播放缓冲队列：

- **并发合成**：当 `EnqueueSentence` 被调用时，句子被加入 `streamingSentenceQueue`，并立即向 `/api/voice/tts` 发起异步 TTS 请求。
- **串行播放协程 (`PlayStreamingQueueCoroutine`)**：
  - **思考状态挂起**：在播放第 1 句之前，自动触发 Avatar 的 Thinking（思考）行为，增强真实感。
  - **连续无缝朗读**：当前一句的 `SpeechPlayer.Play` 协程执行结束时，如果下一句的 `AudioClip` 已经在内存中缓存就绪，则立刻执行无缝拼接播放；若未就绪，则进入极短的轮询等待。
  - **流式结束判定**：直到 `isStreamingFinished` 被标记为 true，且队列中所有缓存句子全部朗读完成后，协程退出，Avatar 回归 Idle 状态。

---

## 五、 对话状态机融合 (`SceneTalkOrchestrator`)

在调度中枢 [SceneTalkOrchestrator.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/SceneTalkOrchestrator.cs) 中，流式与普通播音分支并存。
当检测到 `Brain` 和 `AvatarVoice` 模块支持流式接口时，自动切换为流式生命周期：
```csharp
yield return streamingBrain.GenerateSceneAndReplyStreaming(
    transcript,
    sentence => {
        // 增量塞入播音队列并实时渲染字幕面板
        streamingVoice.EnqueueSentence(sentence);
        accumulatedSubtitle += (string.IsNullOrEmpty(accumulatedSubtitle) ? "" : " ") + sentence;
        if (replyLabel != null) replyLabel.text = $"Avatar: {accumulatedSubtitle}";
    },
    payload => {
        finalPayload = payload;
        isDone = true;
    },
    err => {
        brainError = err;
        isDone = true;
    }
);
```
在流式全部结束后，脑模块会完成最终的 Payload 解析（包括 Skybox 全景图 URL、3D 空间坐标荷载等信息），将对象传回以用于渲染，实现“音频先行，重装场景在后”的低延迟交互闭环。

---

## 六、 端到端语音流与双主体播音调度流程

SceneTalk VR 的端到端对话流程包含“用户语音录制”、“流式增量生成”、“并行语音合成”以及“双角色（主 Avatar 与 纠错 Agent）串行播报”四个步骤，其完整工作流如下：

### 1. 语音采集与 ASR (Speech-to-Text) 阶段
1. **录音捕获**：用户在 VR 场景中按住手柄触发键，利用头显物理麦克风录制语音输入。
2. **网关转译**：录音数据通过 WebRequest 以 PCM 字节流实时发送至 LAN 语音网关的 `/api/voice/stt` 接口，调用腾讯 ASR 引擎完成实时语音转文字。
3. **元数据返回**：网关返回识别的 `transcript` 及 `confidence`（腾讯 ASR 默认补充 1.0 置信度以防止低置信度误判拦截），客户端将 transcript 暂存到 `LastTranscript` 中。

### 2. 流式大模型解析与分句 (LLM & Chunk Parsing) 阶段
1. **触发流式请求**：中枢状态机切换到 `Processing`，调用 `ISceneTalkStreamingBrain.GenerateSceneAndReplyStreaming` 向 LLM Gateway 发送聊天请求，大模型调用兼容 OpenAI 的 `stream = true` 模式和 `json_object` 强制结构化响应。
2. **网关 SSE 增量解码**：`StreamingDownloadHandler` 通过底层 TCP 分包回调实时解包 SSE 字符流，过滤 `data: ` 前缀，反序列化增量 Token。
3. **字段状态机提取**：增量 Token 注入 `IncrementalJsonParser`。解析器通过查找 `"dialogueReply"` 键定位文本起点，逐字符解析双引号内的内容，自动解义转义字符。
4. **标点边界切割**：当提取出的字符流中出现 `.`、`!`、`?` 或中文 `。`、`！`、`？` 等分句标点且其后有空白符或引号时，判定为一句完成，立刻触发 `onSentenceComplete` 回调吐出单句文本。

### 3. 主对话播报（主 Avatar 语音）阶段
1. **音频队列缓冲**：`onSentenceComplete(sentence)` 触发后，立刻塞入 `streamingSentenceQueue` 播放队列。
2. **Thinking 动画挂起**：为掩盖 TTS 生成首句音频的时间，Avatar 在接收到第一句时立即调用 `SetThinking(true)` 播放倾听/思考动画。
3. **TTS 异步合成**：播放队列协程 `PlayStreamingQueueCoroutine` 从队列中 pop 句子，并立刻通过网络向网关 `/api/voice/tts` 发起腾讯语音合成请求。
4. **口型口语驱动**：网关返回 WAV 音频后，客户端动态构建为 `AudioClip`，加载给主 Avatar 的 AudioSource 播放。同时，主 Avatar 调用 `BeginSpeaking`，触发面部口型（LIP-SYNC）和说话手势动画。当该句播音完毕后执行 `EndSpeaking`。
5. **并行重叠**：大模型生成第 2 句的同时，第 1 句正在由网关合成；第 1 句播放时，第 2 句正在通过网络接收，实现完美的流式时序重叠。

### 4. 纠错反馈播报（主 Avatar / 纠错 Agent 语音）阶段
1. **流式判定结束**：主 Avatar 播完队列中最后一句后，`isStreamingPlaying` 状态转为 `false`，大模型数据已全部接收完成并反序列化得到最终的 `SpringScenePayload`。
2. **强制时间停顿**：播音控制器在主 Avatar 话音刚落的瞬间，执行 `yield return new WaitForSeconds(0.5f);`，留出 **0.5秒的自然呼吸时间**，避免语音紧凑重叠。
3. **纠错主体与风格调度**：系统提取 payload 中的 `correctionFeedback`，判断 `hasFeedback` 是否为 true。若有纠错，调用 `CorrectionFeedbackPresenter.Present` 动态分配播报主体：
   * **Recast (顺势纠错风格)**：
     * **若 Provider 为 `dialogue_avatar`**：直接跳过音频播放！由于 Recast 形式的修正短语（如 *"Oh, you really like this furniture?"*）已经被自然融合在主 Avatar 的 `dialogueReply` 首句中并完成了流式朗读，此处不再重复朗读，以保证对话的连贯和自然。
     * **若 Provider 为 `assistant_agent`**：纠错小助手显形并用其专属 TTS 声音念出 Recast 确认。
   * **Explicit (显式纠错风格)**：
     * **若 Provider 为 `dialogue_avatar`**：主 Avatar 亲自纠错，朗读 `feedbackText`（如 *"Small correction: you can say..."*），音频由主 Avatar 播发并驱动其说话动画。
     * **若 Provider 为 `assistant_agent`**：
       - 激活场景中的 3D 浮空助手（纠错 Agent，`CorrectionAgentPresenter`）令其可见。
       - 将音频播报路由重定向到纠错 Agent 的 AudioSource。
       - 用纠错 Agent 的独立合成音色（如 WeJack 男音）播发语法 tip 提示与正确用法，并触发纠错小助手的说话和口型动画。
       - 纠错小助手播报完毕后，系统调用 `EndSpeaking` 并还原其隐藏状态。
4. **进入 Review 状态**：播报阶段完全结束，中枢状态机切入 `TurnReview`，提示用户点击 Continue 或者 Try Again，本次对话交互周期结束。
