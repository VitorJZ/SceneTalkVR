# 🔬 SceneTalk VR: Explicit 与 Recast 纠错机制实现原理及提示词差异报告

为了支持被试实验的严谨性，项目在底层对 **Explicit（显性纠错）** 与 **Recast（隐性重塑）** 进行了不同的机制定义。本报告将详细拆解它们在系统中的实现路径、动态提示词（Prompt）控制条件、纯净度过滤机制（Purity Guard）以及为什么在初次测试时会感觉二者区别不大。

---

## 1. 核心概念与教学法设计差异

在第二语言习得（SLA）学术界，显性纠错与隐性重塑是两种对立的反馈机制：

*   **Explicit (显性纠错)**：
    *   **学术定义**：直接、明确地指出学习者口语中的语法/词汇错误，并给出纠正说明。
    *   **设计目标**：引发学习者的“显性注意”（Notice），强化元认知（Metacognition）。
*   **Recast (隐性重塑)**：
    *   **学术定义**：在不中断对话流畅度的前提下，以正确的语言形式自然地重复或延伸学习者的表达意图。
    *   **设计目标**：减少学习者的挫败感，通过暗示和交际流引导其进行自主修正（Uptake）。

---

## 2. 提示词 (System Prompt) 实现机制

在 SceneTalk VR 中，这两种机制并非在客户端使用两套静态文本模板，而是**通过动态构建 LLM 系统提示词**（System Prompt）驱动大模型生成高度符合语境的个性化反馈文本（`feedbackText`）。

大模型的大脑处理类 [RealLLMService.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs#L427) 在每一次对话轮次中，都会在 System Prompt 的头部的 **`=== EXPERIMENT & TASK CONTEXT ===`** 中注入当前的自变量参数：
```
- feedbackStyle: {currentCondition.style} (explicit means direct correction; recast means natural conversational reformulation)
```

随后，在 **`=== LANGUAGE CORRECTION INSTRUCTIONS ===`** 这一核心指令区，系统向大模型下达了以下极其严格的对比约束指令（[RealLLMService.cs:L500-L508](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs#L500-L508)）：

### 📝 提示词差异对比表

| 反馈风格 (Style) | 大模型核心控制指令 (Prompt Instructions) | 提示词具体条目 |
| :--- | :--- | :--- |
| **Explicit (显性)** | 1. 允许使用明确的教学提示性词汇（如 grammar tip、remember to say、you should say）。<br>2. 可以进行新旧对比（not X, but Y）。 | `- If style is 'explicit':`<br>&nbsp;&nbsp;`* If provider is 'dialogue_avatar': Keep it brief and character-appropriate. Example: 'You can say: I really like this topic.'`<br>&nbsp;&nbsp;`* If provider is 'assistant_agent': Act as an instructor helper. Example: 'Grammar tip: Remember to say: I really like this topic, not I very like this topic.'` |
| **Recast (隐性)** | 1. **严禁**出现说教/纠错性词汇（禁止使用 say, correct, instead, not, mistake, grammar 等）。<br>2. 必须模拟人类正常交流中的自然确认或顺承。 | `- If style is 'recast':`<br>&nbsp;&nbsp;`* Never use direct correction words like 'say', 'correct', 'instead', or 'not'. Formulate a natural conversational reformulation.`<br>&nbsp;&nbsp;`* If provider is 'dialogue_avatar': The feedbackText should sound like the character natural confirmation or continuation of the talk. Example: 'Oh, you really like this topic?'`<br>&nbsp;&nbsp;`* If provider is 'assistant_agent': The feedbackText should be a helpful recast hint. Example: 'You mean you really like this topic?'` |

---

## 3. 不同自变量交叉组合下的生成效果对比

为了便于直观感受，当用户说出带有语病的错误句子：`"I very like this topic."`（语法错误：副词 very 错误修饰动词 like）时，4 种交叉条件下的 `feedbackText` 预期生成对比如下：

```
                    【 用户口语错误："I very like this topic." 】
                                        │
                 ┌──────────────────────┴──────────────────────┐
                 ▼                                             ▼
       【 Provider: Dialogue Avatar 】                【 Provider: Assistant Agent 】
                 │                                             │
      ┌──────────┴──────────┐                       ┌──────────┴──────────┐
      ▼                     ▼                       ▼                     ▼
[Style: Explicit]     [Style: Recast]        [Style: Explicit]     [Style: Recast]
   (场景NPC显性)         (场景NPC隐性)           (独立助理显性)         (独立助理隐性)
      │                     │                       │                     │
  "You can say:          "Oh, you               "Grammar tip:          "You mean
  I really like       really like this         Remember to say:       you really like
   this topic."           topic?"               I really like this     this topic?"
                                                 topic, not I very
                                                 like this topic."
```

---

## 4. 为什么初次测试时会感觉区别不大？

如果您在测试时发现 Explicit 与 Recast 在感官上区别不够强，主要由以下三个技术层面的原因导致：

### 原因 1：大模型越界导致的“重塑纯净度守卫（Recast Purity Guard）”强行重置
*   **现象**：有时在大模型直接生成的 JSON 中，即便当前是 `recast` 模式，它也可能会吐出带有显性意味的句子（例如：*"You mean you should say: I really like this topic?"*，混入了 `should say` 显性词）。
*   **拦截过滤**：客户端在接收到大模型回复后，会使用 [CorrectionTextGuards.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Core/CorrectionTextGuards.cs#L30) 中的 `RecastForbiddenTerms` 列表进行严格的词汇扫描。
*   **退化为 Minimal Recast**：一旦判定大模型的 Recast 话术越界（即包含 *should, say, correct, instead, not, actually* 等词），代码会启动**强行修复**（[RealLLMService.cs:L700](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Services/RealLLMService.cs#L700)）：
    ```csharp
    feedback.feedbackText = BuildMinimalRecast(feedback.correctedText);
    // 最终直接降级为原始的正确整句："I really like this topic."
    ```
*   **感官对比**：
    *   **Explicit 语音**：*"You can say: I really like this topic."* (10字左右)
    *   **Recast 语音 (修复后)**：*"I really like this topic."* (6字左右)
    在短句测试下，两者的发音字数和主干极其类似，听上去就像是只多/少了一个前缀词，因而差异感会被拉低。

### 原因 2：未连通大模型，处于本地“假数据模式 (Demo Mode)”
*   如果您是在没有配置好 `.env` 或者断网的情况下进行的 Demo 测试，系统会使用 [DemoBrainModule.cs](file:///mnt/e/UnityProjects/SceneTalkVR/Client/Assets/SceneTalkVR/Scripts/Demo/DemoBrainModule.cs#L398) 返回硬编码的假数据。
*   假数据里的句子是极度模板化的（仅仅在前缀上多了 `Remember to say` 或是 `You mean`），在快速点击测试时，句型结构极度趋同，因此听觉差异很小。

### 原因 3：没有真正触发语法错误判定（`hasFeedback` 为 `false`）
*   当被试用户说话非常流利且没有明显语法错误时（或者大模型因为 Sensitivty 设为 conservative 过滤了轻微错误），`hasFeedback` 判定为 `false`。
*   此时系统会完全**关闭任何形式的纠错语音播报**，不论当前处于 Explicit 还是 Recast 组，Avatar 都只会正常回答对话（如：*"Sure, here is your table."*），此时两者在体验上是完全一样的。

---

## 5. 如何在后续进一步拉大两者的体验差异？

为了使实验中两组的自变量对比更加鲜明，我们可以在后续开发中采取以下优化方案：

### 💡 优化方案 A：对 Explicit 实施“强教学化扩展”（Make Explicit Harder）
在 Explicit 提示词下，要求大模型必须详细给出**错误类别分析与元认知提示**，使它听上去更像一个“严肃的英语老师”。
*   *当前 (Explicit)*：*"Remember to say: I really like this topic, not I very like this topic."*
*   *可优化为*：*"Grammar error detected: 'very' is an adverb and cannot modify a verb. You should say: I really like this topic."*（拉长显性说明的音频长度，使其结构与 Recast 产生天壤之别）。

### 💡 优化方案 B：对 Recast 实施“弱提示化融入”（Make Recast Softer）
在 Recast 下，直接限制其纠错音频只读正确词组或是弱化语气词，甚至对于 `dialogue_avatar` 而言，要求其在回答中直接将纠错内容融入第一句台词中，不进行任何独立发声确认，从而将其对比降到最低（即纯粹隐形）。
