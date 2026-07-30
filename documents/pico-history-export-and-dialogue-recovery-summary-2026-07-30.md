# PICO 历史导出、任务推进与语音恢复工作汇总

日期：2026-07-30
提交前基线：`3a64706`（`main`）

## 1. 本次汇总范围

本次提交汇总当前工作区内尚未提交的两组完整改动：

1. PICO 通过 USB 数据线向电脑导出实验历史，并在电脑端生成 JSON 与 Excel 文件。
2. 任务目标立即推进、语音识别失败恢复、纠错/角色语音播放重试及成功回合计数修复。

同时包含与上述功能配套的协议资源、运行时配置、中文界面、场景序列化、编辑器预检和自动化测试更新。

## 2. PICO 实验历史 USB 导出

### 2.1 PICO 端

- 首页新增“导出历史数据”按钮。
- 导出协调器采用明确状态：`Idle`、`ProbingUsb`、`BuildingSnapshot`、`Uploading`、`Succeeded`、`Failed`。
- 只允许使用 `127.0.0.1:8789` USB loopback 地址，不向局域网发送实验历史。
- 导出前检查电脑端 `/health`，确认服务类型和 schema 版本均匹配。
- 从实验历史库读取全部实验，按创建时间升序生成稳定快照。
- 快照包含实验摘要、attempt、完整对话回合、场景问卷、最终排序及缺失对话警告。
- 对话、问卷响应、section score 和最终排序均按时间或稳定 ID 排序。
- 导出不修改或删除 PICO 本地 SQLite 数据。
- 中文界面显示检查数据线、整理数据、上传、成功和失败状态，并提供可执行的错误提示。

新增运行时配置：

- `usbHistoryExportBaseUrl = http://127.0.0.1:8789`
- `historyExportTimeoutSeconds = 120`

### 2.2 电脑端

- 新增 `history-export-receiver`，默认只监听电脑的 `127.0.0.1:8789`。
- 统一网关启动器同时管理 Voice 8787、LLM 8788 和历史导出 8789 三条 ADB reverse 映射。
- 后台支持通过 `--export-dir` 或 `SCENETALK_EXPORT_DIR` 指定保存目录。
- 默认导出目录为 `Documents/SceneTalkVRExports`。
- 每次导出先写临时目录，JSON 和 Excel 全部成功后才原子发布最终目录。
- 相同 `exportId` 与相同内容可幂等返回；相同 ID、不同内容会明确拒绝。
- 服务限制请求体大小、校验 schema、导出 ID、时间和 JSON 编码。
- 返回文件名、记录数量、警告数量及 SHA-256，日志不输出密钥。

### 2.3 导出文件

每次成功导出生成：

```text
yyyyMMddTHHmmssZ_<exportId>/
├── experiment_history.json
└── questionnaire_records.xlsx
```

`experiment_history.json` 使用 schema `1.0`，按时间从早到晚保存完整实验历史。

`questionnaire_records.xlsx` 包含五个工作表：

| 工作表 | 内容 |
| --- | --- |
| `Questionnaires` | 问卷会话和完成状态 |
| `Responses` | 每道题的原始值、得分及题目中英文 |
| `Scores` | section 聚合分数 |
| `FormalSceneStats` | 正式场景问卷统计 |
| `FormalRankingStats` | 正式实验最终排序统计 |

`FormalSceneStats` 的列顺序为：

1. `participantId`
2. 完成时间（北京时间，Excel 原生日期格式 `dd/mm/yyyy hh:mm:ss`）
3. `taskId`
4. 按问卷目录顺序排列的各题得分

缺失或没有得分的题目写入数值 `-1`。已提交和已跳过的正式场景问卷都会保留，记录按完成时间升序排列。

`FormalRankingStats` 的列顺序为：

1. `participantId`
2. 完成时间
3. NE/NR/SE/SR 与正式任务 `taskId` 的映射
4. 各反馈条件的排名；缺失排名写入 `-1`
5. 首选条件、首选任务和理由

同一参与者存在多份最终排序时，只保留完成时间最新的一份。统计表只读取 `ExperimentKind=Formal` 的实验记录。

## 3. 任务目标立即推进

### 3.1 新状态策略

新增 `SequentialAfterConfirmationWithFinalReplyCompletion` 策略，并保持旧枚举值不变：

- 中间目标确认后立即激活并显示下一目标，不再等待额外的参与者发言或角色回复回合。
- 最后一个目标确认后进入 `AwaitingAvatarReply`。
- 只有与该目标证据 `turnId` 匹配的角色语音完整播放完成，状态才进入 `Completed` 并打开问卷。
- 若目标判定晚于语音播放完成，已记录的完成 `turnId` 仍可立即完成最后目标。
- 正式实验、预实验、彩排和编辑器演示协调器统一使用新策略。

### 3.2 恢复兼容

- 目标序列快照升级到 `4.0`。
- 继续读取 `2.0` 和 `3.0` 快照。
- 旧快照恢复时从第一个未确认目标继续，不要求补做已经取消的额外解锁回合。
- 已全部确认的恢复会话直接进入完成边界，避免恢复后再次卡在对话界面。

### 3.3 协议资源

正式采集协议升级为：

```text
1.5.0-immediate-goal-advance
```

协议资源、资源生成器、正式模式预检和构建信息已同步更新，避免重新生成资源后恢复旧策略。

## 4. STT、纠错语音和角色语音恢复

### 4.1 语音识别失败

- STT 失败作为可恢复失败记录，不直接把条件标记为技术无效。
- 界面显示“语音识别失败，请点击‘重试’重新录音”。
- 点击重试启动一次全新的录音，不复用失败的识别结果。
- 失败发生在目标证据提交之前，因此不会误推进或锁住目标状态。

### 4.2 纠错语音失败

- Avatar 语音模块记录失败阶段：`Setup`、`CorrectionFeedback` 或 `DialogueReply`。
- 纠错语音或角色初始化失败时，重试完整的缓存回复，包括纠错和角色回答。
- 重试不会重新调用 STT、LLM 或目标判定，不会生成重复回复或重复目标证据。
- 再次失败后仍保留相同缓存数据，允许继续点击重试。

### 4.3 角色回答语音失败

- 仅角色回答阶段失败时，构造“仅角色回答”的重试 payload。
- 已成功播放的纠错语音不会重复播放。
- 完整播放成功后才进入回合完成状态并通知目标状态机。

### 4.4 成功回合计数

- 新增 `CompletedTurnCount`。
- 只有 `CompleteActiveTurn()`，即整轮语音完整结束时，才增加成功回合数。
- STT、纠错语音和角色语音的可恢复失败不会消耗最大回合数。
- 正式实验和预实验的 `TurnsToCompletion`、回合上限及恢复基线均改用成功回合计数。
- 可恢复失败使用独立的 `TurnRecoverableFailure` 时间事件记录，技术有效性保持 `Valid`。

## 5. 界面与场景

- 新增历史导出按钮及导出状态中文提示。
- 目标面板将旧的“等待额外发言”提示改为“正在准备下一目标”。
- 最终目标等待状态明确显示“正在等待本轮语音完整播放”。
- 纠错语音、角色语音和 STT 失败提示均明确要求点击“重试”。
- `SampleScene.unity` 保存了新增导出协调器、导出按钮以及此前尚未提交的正式实验界面整理结果。
- 场景由 Unity 重新分配大量本地对象 ID，因此文本 diff 较大；没有手工改写 Unity YAML 结构。

## 6. 代码审查结论

- USB 历史导出服务保持 loopback-only，实验数据不暴露到 LAN。
- 未在改动或新增文件中发现 API key、token、Authorization 或其他凭据字面量。
- 新增 C# 脚本和测试均包含 Unity `.meta` 文件。
- 没有把测试结果、缓存、数据库或临时导出文件加入提交范围。
- `git diff --check` 的非零项仅来自 Unity 场景重新序列化的空值字段尾随空格；C#、Python、配置和文档没有新增格式错误。

## 7. 验证结果

| 验证 | 结果 |
| --- | --- |
| Unity EditMode 相关回归 | 148 / 148 通过 |
| Unity PlayMode 正式/预实验流程 | 26 / 26 通过 |
| Gateway Launcher Python 单元测试 | 12 / 12 通过 |
| History Export Receiver Python 单元测试 | 9 / 9 通过 |
| Python `py_compile` | 通过 |
| Unity 主编辑器最近 1200 行日志 | 无 C# 编译错误 |

Unity 测试覆盖历史快照排序、问卷目录导出、目标立即切换、最终回复完成边界、STT 重新录音、纠错完整重试、角色回答重试、失败不消耗回合、正式流程和预实验流程。

电脑端测试覆盖 ADB 8789 映射、loopback 监听、路径配置、时间排序、两张正式统计表、缺失得分 `-1`、Excel 原生日期、五工作表结构、原子写入、幂等导出和 HTTP 接口。
