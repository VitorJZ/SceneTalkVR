# SceneTalkVR 纠错证据 ASR 技术规划

更新日期：2026-07-15

## 1. 目标与结论

当前腾讯云 `SentenceRecognition` 适合生成自然对话 transcript，但它只返回一个最终 `Result`。如果识别器把用户说的 `I are hungry` 平滑成 `I'm hungry`，纠错层就失去了用户原始错误的证据。

本规划的目标不是立刻增加第二套正式 STT，而是先验证哪一种识别结果能同时满足：

- 保留学习者实际说出的语法错误。
- 对正确表达不制造错误候选。
- 在 PICO/VR 对话中维持可接受的等待时间。
- 能向纠错层提供候选、置信度或音素证据，而不只是单一显示文本。

当前推荐路线：

1. 保留腾讯云作为现有基线和 TTS provider。
2. 首先用 Azure Speech 短音频 REST `format=detailed` 做离线 A/B 基准。
3. Azure 只有通过错误保留率和延迟门槛后，才进入 voice gateway。
4. 如果 Azure 单路表现足够好，直接替换 STT，避免双路延迟。
5. 如果 Azure 只适合纠错证据，则与腾讯并行，设置硬性等待上限。
6. 如果云端 N-best 仍然普遍把错误句改正确，则停止堆叠云 ASR，转向本地弱语言模型 ASR 或针对已知语法点的候选/音素评分。

## 2. 当前链路与问题边界

当前链路为：

```text
Unity/PICO 录音
  -> POST /api/voice/stt
  -> Tencent SentenceRecognition
  -> transcript
  -> RealLLMService
  -> correctionFeedback + dialogueReply
```

当前接口的限制：

- 腾讯一句话识别输出 `Result`、音频时长和可选词时间戳。
- 官方输出参数没有 N-best、候选级置信度或词级置信度。
- `FilterDirty`、`FilterModal`、`FilterPunc` 等开关主要注明支持中文普通话，不能关闭英文解码器的语言模型偏置。
- `WordInfo` 只能增加时间戳，不能恢复已经被改写的单词。
- 当前 Unity Brain 只消费一个 `string userText`，无法表达“对话文本”和“纠错证据”是两种不同结果。

因此，不能通过修改 LLM prompt 从单一最终 transcript 反推出用户原话。修复必须发生在 ASR 输出契约或音频证据层。

## 3. 候选方案调研

| 方案 | N-best / 候选 | 原始形式 | 置信度 | 实时能力 | 当前判断 |
| --- | --- | --- | --- | --- | --- |
| 腾讯 `SentenceRecognition` | 无，单一 `Result` | 无独立 lexical/raw | 无真实置信度 | 当前项目为整段请求 | 继续作为基线，不承担纠错证据 |
| Azure Speech detailed | 返回 `NBest` 列表 | `Lexical`、`ITN`、`Display` 分离 | 候选级 `Confidence` | REST 支持短音频最终结果；SDK 支持中间结果 | 第一优先验证 |
| Azure Pronunciation Assessment | 音素级 `NBestPhonemes` | 可用参考文本做发音/插入/遗漏比较 | 音素和发音评分 | 支持 streaming | 固定语法点的第二阶段候选评分 |
| AWS Transcribe alternatives | 最多 10 个 segment alternatives | 分段 transcript | 候选和词级置信度 | 官方明确 alternatives 只支持 batch | 不进入 VR 实时链路 |
| Deepgram Nova streaming | 响应含 `alternatives` 容器 | word / punctuated word | transcript 和词级 confidence | WebSocket streaming | 未找到可配置 N-best 数量，先不作为纠错主证据 |
| Google Cloud STT | API 支持 alternatives 和 word confidence | transcript | 候选/词级 confidence | 支持 streaming | 当前开发网络无法稳定打开官方文档，后续仅作备选网络测试 |
| sherpa-onnx + 本地模型 | 取决于模型和解码器 | 可选 CTC/Transducer/Whisper 等模型 | 取决于模型 | 本地、WebSocket、C#/Android 均可接入 | 云端候选失败后的可控路线 |

注意：`Lexical` 仍然是 ASR 选出的词，不等于未经语言模型处理的声学真值。Azure 进入下一阶段的依据只能是实测错误保留率，而不是字段名称。

## 4. 推荐的验证顺序

### 4.1 第一优先：Azure 短音频 REST detailed

选择理由：

- 当前项目录音是 16 kHz mono WAV，符合 Azure 短音频 REST 输入格式。
- 当前单轮录音远低于 60 秒限制。
- 可以直接使用 HTTP，不必先给 Python 网关引入完整 Speech SDK。
- `format=detailed` 返回 `NBest`、`Confidence`、`Lexical`、`ITN` 和 `Display`。
- 能在不改 Unity 的情况下，用现有 WAV 样本先完成验证。

建议请求参数：

```text
language=en-US
format=detailed
profanity=raw
Content-Type: audio/wav; codecs=audio/pcm; samplerate=16000
```

第一阶段只做整段请求。只有整段 detailed 结果通过质量门槛，才评估 Speech SDK/WebSocket streaming，避免过早增加协议复杂度。

### 4.2 第二优先：Azure 音素候选或参考文本评分

如果普通 N-best 仍然没有保留目标错误，但实验任务的语法点是预先定义的，可以生成最小对比候选：

```text
I are hungry.
I'm hungry.
```

再使用 Pronunciation Assessment 或其他 forced-alignment 工具比较两个候选与音频的匹配程度。这个方案适合固定教学任务，不适合作为开放对话的通用转写器。

### 4.3 第三优先：本地 sherpa-onnx

如果云端模型都倾向于语法平滑，使用本地 CTC 或弱语言模型解码器进行实验。优点是没有第二次公网往返，并且可以控制解码策略；缺点是需要自行选择模型、部署运行时和验证非母语英语质量。

本地模型不直接部署到 PICO。优先放在现有 voice gateway 主机，通过内部 provider 调用，避免增加 Android 包体和设备负载。

## 5. 基准数据集

正式接入前建立一个小而可复现的最小对比数据集。

最低规模：

- 10 类可听辨语法错误。
- 每类包含错误句和对应正确句。
- 3 名说话者，优先包含中文母语英语学习者。
- 共 60 条干净录音；再增加同样 60 条 PICO 麦克风或轻度环境噪声版本。

建议语法对：

| 错误句 | 正确句 | 错误类型 |
| --- | --- | --- |
| I are hungry. | I'm hungry. | 主谓一致/系动词 |
| She go to school every day. | She goes to school every day. | 第三人称单数 |
| Yesterday I go there. | Yesterday I went there. | 过去时 |
| She don't like coffee. | She doesn't like coffee. | 助动词一致 |
| There is two cups. | There are two cups. | 单复数一致 |
| I very like this topic. | I really like this topic. | 副词搭配 |
| I have went home. | I have gone home. | 过去分词 |
| I am agree with you. | I agree with you. | 多余系动词 |
| I want buy a ticket. | I want to buy a ticket. | 不定式缺失 |
| One of my friend is here. | One of my friends is here. | 名词复数 |

录音标签至少包含：

```json
{
  "audioPath": "...",
  "speakerId": "speaker-01",
  "condition": "incorrect",
  "verbatimText": "I are hungry.",
  "correctText": "I'm hungry.",
  "targetSpan": "are",
  "errorType": "subject_verb_agreement",
  "captureDevice": "pico4"
}
```

## 6. 评价指标与准入门槛

普通 WER 不是本项目的主指标，因为把错误句自动改正确反而可能降低“面向正确英文参考”的 WER。必须增加以下指标：

### 6.1 质量指标

- `incorrect_top1_preservation`：错误句的目标错误是否出现在 top-1 lexical 结果中。
- `incorrect_topn_recall`：目标错误是否出现在任一 N-best 候选中。
- `correct_false_error_rate`：用户说正确句时，候选证据是否错误支持了错误句。
- `pair_discrimination_accuracy`：错误/正确最小对比中，声学证据是否选择了真实说法。
- `end_to_end_correction_precision`：最终触发的纠错中有多少确实是用户说出的错误。
- `end_to_end_correction_recall`：人工标注错误中有多少最终触发了纠错。

首轮建议门槛：

- top-5 错误保留率不低于 85%。
- 正确句错误证据率不高于 5%。
- 端到端纠错 precision 不低于 90%。
- 噪声版本相比干净版本的错误保留率下降不超过 15 个百分点。

门槛是试运行目标，不是供应商宣传指标。若数据量扩大后波动明显，应重新计算置信区间。

### 6.2 延迟指标

记录以下时间点：

```text
recordingStoppedAt
gatewayRequestReceivedAt
providerStartedAt
providerCompletedAt
assessmentReadyAt
brainStartedAt
avatarAudioStartedAt
```

首轮建议门槛：

- Azure 单路 p95 STT 完成时间不高于腾讯基线 p95 + 300 ms。
- 正式纠错证据在用户结束说话后 p95 不超过 900 ms。
- 双路模式下，评估结果最多允许比对话 STT 晚 400 ms。
- 达到硬超时后不继续阻塞 Avatar 回复。

## 7. 选型决策树

```text
Azure detailed 是否通过错误保留率门槛？
  ├─ 否 -> 云端 N-best 不进入正式链路
  │       -> 测试固定候选/音素评分
  │       -> 仍失败则测试本地 CTC/弱语言模型
  └─ 是 -> Azure top-1 对话质量和网络延迟是否也合格？
          ├─ 是 -> 单路 Azure STT，腾讯继续负责 TTS
          └─ 否 -> 腾讯生成 conversationTranscript
                  Azure 并行生成 correctionEvidence
                  使用硬超时和证据门控
```

单路 Azure 是首选生产结构，因为它没有第二路等待和双倍 STT 成本。双路只在实测证明“腾讯对话体验更好，但 Azure 更能保留错误”时采用。

## 8. 目标数据契约

为保持 Unity 兼容，现有 `transcript` 字段继续表示对话使用的最终文本。新增字段采用可选、向后兼容方式：

```json
{
  "requestId": "stt_...",
  "provider": "azure",
  "transcript": "I'm hungry.",
  "lexicalTranscript": "i are hungry",
  "confidence": 0.82,
  "confidenceAvailable": true,
  "alternatives": [
    {
      "lexical": "i are hungry",
      "display": "I are hungry.",
      "confidence": 0.82
    },
    {
      "lexical": "i'm hungry",
      "display": "I'm hungry.",
      "confidence": 0.76
    }
  ],
  "assessment": {
    "status": "supported",
    "verbatimCandidate": "i are hungry",
    "evidenceType": "nbest",
    "latencyMs": 430
  }
}
```

`assessment.status` 建议取值：

- `supported`：证据足够，可用于纠错。
- `ambiguous`：候选分差不足，不应强纠错。
- `timeout`：评估超过等待预算。
- `unavailable`：provider 不提供证据。
- `failed`：评估 provider 调用失败。

`unavailable`、`ambiguous` 和 `timeout` 都不等于“用户没有错误”。实验日志必须区分这些状态。

## 9. 分阶段实施计划

### P0：离线基准，不改 Unity

计划新增：

- `Server/voice-gateway/tools/asr_benchmark.py`
- `Server/voice-gateway/benchmarks/correction-asr/manifest.example.jsonl`
- Azure REST 调用的独立试验 adapter。
- 输出每条音频的 Tencent/Azure 结果、候选、目标错误命中和延迟。

完成条件：

- 至少 60 条干净最小对比录音完成两家 provider 测试。
- 生成 CSV/JSON 汇总，不保存密钥。
- 得出明确的“通过、拒绝或继续扩样”结论。

### P1：接入 Azure turn-based provider

只有 P0 通过后执行：

- 新增 `AzureSpeechProvider`，保持与腾讯 provider 相同的服务端边界。
- 增加 Azure endpoint、key、language 和 timeout 配置。
- 扩展 `SttResult` 支持 lexical transcript 和 alternatives。
- `/api/voice/stt` 保持现有字段不变，只追加可选字段。
- Unity `SttResponse` 增加对应可序列化结构。
- `RealLLMService` 只在 `assessment.status == supported` 时消费纠错证据。

P1 先保持录完上传，不同时引入 WebSocket、VAD 和 Brain 接口重构。

### P2：单路或并行生产模式

根据基准结论二选一：

单路模式：

```text
WAV -> Azure detailed -> transcript + correctionEvidence -> Brain
```

并行模式：

```text
                 -> Tencent -> conversationTranscript
WAV -> coordinator
                 -> Azure -> correctionEvidence
```

并行协调器要求：

- 两个 provider 同时开始，不能串行调用。
- 腾讯成功、Azure 超时时，继续自然对话并记录 `assessment_timeout`。
- Azure 成功、腾讯失败时，可以按配置降级到 Azure top-1。
- 两边都失败时再进入现有 mock fallback。
- 原始 WAV 只在内存中保留到两路完成或超时，默认不落盘。

### P3：流式与固定候选评分

当 turn-based 质量已经验证后再做：

- Unity 到 gateway 的 WebSocket/分片音频协议。
- Azure Speech SDK streaming 或其他实时候选接口。
- 已知语法任务的最小候选/音素评分。
- 本地 sherpa-onnx provider 与模型常驻预热。

P3 的目标是减少录音结束后的尾部等待，而不是改变 P0/P1 的评价标准。

## 10. 纠错证据门控

不能把 N-best 中出现的任意错误句都当成用户错误。建议先做确定性门控，再交给 LLM：

```text
1. assessment.status 必须为 supported
2. 错误候选必须包含可定位的 targetSpan
3. 错误候选与正确候选之间必须达到最小置信度差或声学评分差
4. 正确句测试集上的误触发率必须低于门槛
5. 音频过短、取消、严重噪声或超时时不触发纠错
6. LLM 只能解释已通过门控的错误，不能从低分候选中自行挑错
```

第一版阈值不写死在 prompt 中，放到网关或独立配置：

```text
ASR_ASSESSMENT_TIMEOUT_MS=900
ASR_ASSESSMENT_MAX_EXTRA_WAIT_MS=400
ASR_MIN_ALTERNATIVE_MARGIN=待基准标定
```

## 11. 实验与隐私要求

- 正式运行默认不保存原始音频和完整 transcript。
- 基准数据集必须获得说话者同意，并放在不提交 Git 的本地目录。
- 运行日志只记录 provider、耗时、状态、错误类型和脱敏后的命中结果。
- `assessment_timeout`、`assessment_ambiguous` 和 provider fallback 必须进入实验 turn log。
- 分析实验结果时，不能把“证据不可用”计为“用户没有错误”。
- 如果不同实验条件的超时率不同，需要作为潜在混杂变量报告。

## 12. 风险与停止条件

### 风险：N-best 仍然全部是正确句

处理：停止继续堆叠云识别器，转向音素/forced alignment 或本地弱语言模型。

### 风险：Lexical 被误认为绝对原话

处理：文档和代码统一称为 `lexicalTranscript` 或 `verbatimCandidate`，不使用 `rawAudioTruth` 等误导命名。

### 风险：双路导致延迟和成本增加

处理：优先单路 Azure；双路必须并行并受硬超时限制。未证明质量收益前不得默认开启。

### 风险：自动跳过纠错破坏实验一致性

处理：记录 assessment 状态；正式实验前根据预实验决定是跳过、请求重说，还是排除该 turn。

停止条件：任何方案若不能同时达到错误保留率、正确句误触发率和延迟门槛，不进入正式实验链路。

## 13. 官方参考

- 腾讯云一句话识别：<https://cloud.tencent.com/document/api/1093/35646>
- Azure 短音频 REST：<https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-speech-to-text-short>
- Azure 识别结果：<https://learn.microsoft.com/en-us/azure/ai-services/speech-service/get-speech-recognition-results>
- Azure Pronunciation Assessment：<https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-pronunciation-assessment>
- AWS Alternative Transcriptions：<https://docs.aws.amazon.com/transcribe/latest/dg/alternatives.html>
- Deepgram confidence：<https://developers.deepgram.com/docs/confidence>
- Deepgram streaming：<https://developers.deepgram.com/docs/live-streaming-audio>
- Google Speech-to-Text requests：<https://cloud.google.com/speech-to-text/docs/speech-to-text-requests>
- sherpa-onnx：<https://k2-fsa.github.io/sherpa/onnx/index.html>

以上能力核对日期为 2026-07-15。供应商行为最终以同一批 SceneTalkVR 学习者语音实测为准。
