# Experiment v1.1 阶段 6：Pilot 三种反馈具身形态与预实验流程

## 结论

阶段 6 的代码、Catalog、三个餐厅任务、测试专用分配器、生命周期、Stage 5 问卷接入、实验员入口与日志关联已经建立。Voice Only、Floating Orb、Humanoid Agent 是三个显式条件；它们复用 Stage 3 Feedback First Gate 和同一 Correction Planner，Pilot style 属于 Assignment 而不属于 Embodiment，呈现层只改变反馈主体的视觉/音频通道。

Pilot Locked Run 仍按设计阻断，不能开展正式预实验采集。阻断原因是：`pilot_feedback_style` 未确认、`voice_only_spatial_audio` 未确认、a/b/c 映射未确认、Humanoid 正式 prefab 未交付。代码未修改这些正式决策，也没有用测试映射确认正式资产。

基线：`experiment-v1.1-integration`，`673589ccd8ebf27b6386e34ec78b5dcf5f43d434`。最终交付提交为包含本报告的 HEAD；Git commit 无法自包含自身 SHA，准确 SHA 以 `git rev-parse HEAD` 和交付消息为准。提交信息固定为 `feat(experiment): add pilot embodiment conditions and workflow`。

## 起始检查与 Stage 5 语义修复

- 起始本地 HEAD 与远端一致，实验代码工作区干净。
- `Client/Packages/manifest.json`、`Client/Packages/packages-lock.json`、`Client/.agents/skills/` 是 Unity Skills 本地安装内容，未纳入提交。
- 起始全量 EditMode：327/327 通过。
- Stage 5 样例问题已修复：问卷提交时不再让导出行保持 `QuestionnaireInProgress`。新增并真实填写 `responseCapturedAtUtc`、`questionnaireStatus`、`questionnaireSubmittedAtUtc`、`conditionCompletedAtUtc`，并将提交后的导出 `conditionStatus` 设为 `Completed`。Pilot CSV 另加入 `embodimentCondition`。

## 实现文件与调用链

- `Scripts/Core/PilotExperimentModel.cs`：强类型条件、Assignment、sequence、run context、presentation profile、稳定分配和版本校验。
- `Scripts/Core/PilotPresentationCatalog.cs` 与 `ExperimentProtocol/PilotPresentationCatalog.asset`：三形态呈现的运行时权威来源。
- `Avatar/Scripts/PilotEmbodimentPresenter.cs`：视觉、音源、动画与 Reset。
- `Scripts/Core/PilotWorkflowCoordinator.cs`：Pilot lifecycle、任务、问卷、重跑与 Pilot JSONL。
- `Avatar/Scripts/CorrectionFeedbackPresenter.cs`：在原有 Gate 内把同一 feedback payload 路由到 Pilot presenter；没有建立第二条绕过 Gate 的播放路径。
- `Scripts/Editor/PilotExperimenterWindow.cs`：仅 Unity Editor 可见的实验员控制窗口，不进入 VR participant UI。
- `Scripts/Editor/SceneTalkPreflightMenu.cs`：Pilot Catalog、任务、问卷、决策、sequence、placeholder 与 Humanoid 校验。

运行链：`PilotExperimenterWindow → PilotWorkflowCoordinator.Prepare → ResetConditionSessionBoundary → Task Catalog task → ApplyPilotAssignment(style, task) → CorrectionFeedbackPresenter → PilotEmbodimentPresenter → Stage 3 Gate → Avatar dialogue`。Questionnaire 链为 `CompleteTask → AwaitingPilotQuestionnaire → Stage 5 pilot_condition_v1 → Submit → Completed`；三项均有效完成后才允许 `pilot_final_v1` 排序。

## 三种 Embodiment

| 条件 | 视觉 | 音频 | 生命周期 | Locked 行为 |
|---|---|---|---|---|
| `voice_only` | 不创建 Agent 视觉实体 | 由协议严格选择 fixed spatial 或 head-locked 2D | 音频仍遵守完整反馈播放时长和 Gate | audio 决策缺失即阻断 |
| `floating_orb` | 复用 `CorrectionAgentPresenter`，固定 profile，出现/说话/隐藏 | 共享 voice，profile 固定空间参数 | 同步 `BeginFeedback` / `EndFeedback`，Reset 强制隐藏 | placeholder 标记即阻断 |
| `humanoid_agent` | 独立 prefab、固定位置/旋转/缩放，Idle/Speaking bool | 共享 voice 和参数 | 只在纠错期间显示，结束隐藏，Reset 销毁实例 | prefab 缺失或 placeholder 即阻断，不退回 Orb |

当前 Humanoid asset 仅有明确的 `humanoid_feedback_agent_pending` placeholder 元数据，没有正式 prefab。Developer test 可验证接口；Locked 验证失败是正确结果，不能宣称 Humanoid 资源验收完成。

## Voice Only audio policy

合法协议值只有：

- `spatial_fixed_source`：`spatialBlend=1`，固定并记录 `sourcePosition`、min/max distance。
- `non_spatial_head_locked`：`spatialBlend=0`，AudioSource 挂到主 Camera，local position 为零。

正式协议资产没有被赋默认值。测试分别验证了两种策略。

## 混杂变量控制

三 profile 使用同一 `voiceProfileKey=101050`、`speakingSpeed=1`、`volume=1`、`subtitlePolicy=feedback_only`、零 appearance delay。Correction Planner prompt 只包含协议固定 style，不含 embodiment。反馈文本由原 Stage 3 payload 单次生成，`feedbackTextHash` 从同一文本计算；presentation visual preparation 在真实 playback-start callback 内同步发生，不提前开 Gate，也不增加人为等待。主 Avatar dialogue 逻辑没有改变。

不可避免的当前差异是 Voice Only 的声场待研究决策、Orb 的视觉动画以及 Humanoid 资源缺失；它们在 profile、Preflight、Pilot JSONL 和本报告中显式记录。

## 三个餐厅任务

Formal Catalog 未改变。Pilot Catalog 现在是三个独立 taskId：

- `pilot_restaurant_walk_in`：无预约到店询桌。
- `pilot_restaurant_ordering`：查看菜单并点餐。
- `pilot_restaurant_wrong_dish`：礼貌处理送错菜。

每项都有独立 context、四个 goals、initialQuestion 和 roleplayPrompt，共享固定本地 `SceneTalkVR/Textures/restaurant-360`。旧 `restaurant_reservation` 不再是运行时 Pilot taskId。完整冻结值见 `EXPERIMENT_V1_1_STAGE6_PILOT_TASK_MANIFEST.json`。本阶段未获得比用户提示更晚的老师文案文件，因此按阶段 6 指定文案固化并标记为团队复核项。

## Pilot allocator

`TryCreateLocked` 只读取协议内 confirmed Pilot sequences；目前正确阻断。`TryCreateForTesting` 接受内存测试 sequence，Participant ID 经 SHA-256 稳定映射 sequence 与 task rotation。每个 Assignment 恰好包含三种 embodiment 各一次、三个 task 各一次；跨多个参与者测试验证 embodiment × task 近似平衡。保存/加载包含 protocol、assignment、task catalog 三版本校验。

TechnicalInvalid 记录 failure stage/reason；实验员授权 `RetryCurrent` 后增加 run attempt 并创建新 `pilotRunId`，旧 JSONL 不覆盖。

## 生命周期、Reset 与问卷

生命周期完整覆盖 `Assigned → Preparing → Running → TaskCompleted → AwaitingPilotQuestionnaire → PilotQuestionnaireInProgress → PilotQuestionnaireSubmitted → Completed`，以及 `TechnicalInvalid` / `Aborted` 类型。

每个条件前统一 Reset LLM/Stage 3 gate/TTS/audio/Avatar/Agent 的既有 Stage 1 API，并额外清理 Pilot visual、Goal tracker、questionnaire session、timing accumulators、run/linkage ID 与 current condition transient state。Pilot coordinator 自身实现 `ISceneTalkSessionReset`，可被未来 allocator 安全调用。

问卷直接从 Stage 5 Catalog 解析 `pilot_condition_v1` 和 `pilot_final_v1`，没有复制题目。每条件 response 带 pilotRunId 对应的 conditionRunId、`questionnaireLinkageKey`、embodiment、taskId 与 taskAssignmentId。最终排序要求三种字符串标签唯一、三条件均有效完成、长期首选形态有效且开放理由非空；TechnicalInvalid 不可进入最终有效排序。

## 日志

Pilot JSONL `PilotEventRecord` 包含需求列出的协议/Assignment/run/participant/session/sequence/condition/embodiment/style/task/hash/actor/visual/voice/audio/spatial/timestamps/latencies/validity/failure/questionnaire linkage 字段。

Stage 3 timing JSONL 仍是音频时序权威，仅兼容增加 `embodimentCondition` 和 `pilotRunId`。Pilot coordinator 观察真实 `UserSpeechEnded`、`CorrectionPlaybackStarted/Ended`、`DialoguePlaybackStarted` monotonic 事件，写关联事件并计算 `userEndToFeedbackAudioMs` 与 `feedbackToDialogueGapMs`；未改变 Stage 3 指标定义。非 Pilot 回合只有在 `HasActivePilotRun` 时才写 Pilot 扩展，避免 Formal 日志被默认 `voice_only` 污染。

## 实验员控制

菜单 `SceneTalkVR/Experiment/Pilot Experimenter Control` 提供创建/加载/保存 Assignment、准备下一条件、完成任务、进入问卷、标记 TechnicalInvalid、重跑。窗口在开始前显示 Participant、sequence、position、embodiment、task、style、audio policy 和 run ID。它位于 Editor assembly，不是 participant VR 按钮。

## Unity 验证

- Unity：6000.3.16f1；使用已打开的 `Client@16ed13d125de1334`，未启动第二个 Editor。
- 编译：Console 0 C# error。
- 全量 EditMode：342/342 通过，job `55d2a827913545cc92654d4c79c0581c`。
- 全量 PlayMode：6/6 通过，job `35dcea0a22ce4db69e7c837d13a51afb`。
- 最小 Play Mode：进入 5 秒并退出，0 新 error。
- Preflight：64 pass / 13 blocked；Pilot 的四个 blocker 均被明确报告。其他 blocker 包括既有 Formal 决策、Formal Avatar 与 LAN/PICO 配置，不属于阶段 6。
- Stage 2–5 回归包含在上述全量运行中；Stage 3 Gate 测试通过。
- 未进行、也不声称 PICO 真机验证。

在上述全量 Unity MCP 运行后，收尾增加了兼容的 Pilot timing linkage、问卷 CSV embodiment 字段与 reset hardening。随后使用 Unity 6000.3 自带 Roslyn 和当前 Bee response files 编译 `Assembly-CSharp`、`Assembly-CSharp-Editor`、`SceneTalkVR.Stage2.PlayModeTests`，三者均为 0 error；没有启动第二个 Editor。既有 obsolete/member-hiding warning 不属于阶段 6 回归。

## 已知风险与阶段 7 输入

P0：研究团队确认 `pilot_feedback_style`、`voice_only_spatial_audio`、a/b/c 映射；交付并绑定 Humanoid 正式 prefab。四项完成前 Locked Pilot 必须继续阻断。

P1：研究团队复核三个 restaurant initialQuestion/roleplay 文案；确认 shared voice profile `101050` 是正式预实验音色；用正式 Humanoid 资源复跑三条件音频延迟比较与完整问卷闭环。

P2：PICO 上验证 spatial source、Orb/Humanoid 位置/遮挡/动画、内存与连续三条件 reset；本阶段没有相关结论。

阶段 7 的明确输入是：三项研究决策的 confirmed 协议值、Humanoid prefab 与 Animator 参数契约、正式 Pilot voice profile，以及研究团队签字确认的三个 Pilot 任务文案。
