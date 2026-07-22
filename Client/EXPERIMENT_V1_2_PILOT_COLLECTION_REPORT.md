# SceneTalkVR Experiment v1.2 Pilot Collection 实现报告

## 基线与范围

- 分支：`experiment-v1.1-integration`
- 起始提交：`1b0fa91c05a8c2d1d0875f9c63b3885500b5e5c0`
- Unity：`6000.3.16f1`
- 运行目标：`editor_collection`
- 本轮未修改 Formal 的研究决策锁，不要求 PICO。

## 结果

主菜单现为 `Formal Experiment / Pilot Experiment / Settings / Quit`。Pilot 入口直接创建 `PilotCollectionSessionCoordinator` 和 `PilotCollectionParticipantUi`，进入 Session Setup，不再经过 Team Showcase、Developer Task Selection 或 Synthetic Assignment。Team Showcase 仅保留为 QA 工具。

Pilot collection 上下文固定为 `flowMode=pilot`、`runQualification=collection`、`dataOrigin=participant_collection`、`collectionEligible=true`、`developerTestAssignment=false`、`synthetic=false`、`demoMode=false`。

## 旧调用链与新调用链

旧路径：`Main Menu Start -> prepared-session check -> Team Showcase Create Session -> condition selection`。因此未先操作 Team Showcase 时会显示 `Experiment Session Not Prepared`。

新路径：`SceneTalkFlowUiController.ShowMainMenu -> Pilot Experiment -> PilotCollectionParticipantUi.ShowSetup -> PilotCollectionSessionCoordinator.CreateSession/ResumeSession -> Instructions -> Task Introduction -> PreparePilotCondition -> Dialogue -> Questionnaire -> Transition/Ranking -> Completion`。

## 主要修改

- `PilotCollectionSessionCoordinator.cs`：Session、Assignment、Resume、Retry、阶段转换、持久化、Bundle。
- `PilotCollectionParticipantUi.cs`：Setup、Instructions、Task Intro、Questionnaire、Transition、Ranking、Completion。
- `PilotCollectionBundleExporter.cs`：Pilot collection Bundle、checksums、integrity。
- `PilotExperimentModel.cs`：稳定三组循环分配算法。
- `PilotWorkflowCoordinator.cs`：collection 隔离、condition boundary reset、状态事件。
- `GoalAchievementEvaluator.cs`：Pilot final transcript 自动 Goal 判定与证据。
- `SceneTalkFlowUiController.cs`：Formal/Pilot 独立入口。
- `ExperimentConditionManager.cs`、`SceneTalkOrchestrator.cs`：Pilot task 保持、日志路径、正式 opening payload。
- `CorrectionFeedbackPresenter.cs`、Voice Profile Catalog、Avatar Catalog：统一反馈 voice，并修正 Avatar 性别 voice 映射。
- `PilotEditorCollectionPreflight.cs`、`SceneTalkOperatorControlWindow.cs`：Collection Preflight 与 Operator Control。
- `Client/Analysis`：Pilot Bundle、attempt、goal、questionnaire、ranking 解析。

## 协议、任务与 Assignment

协议快照为 `1.2.0-editor-collection`：Explicit、Voice Only 非空间 head-locked、最多 5 turns/8 分钟、primary attempt=`latest_valid_completed_attempt`。任务目录版本为 `1.2.1-pilot-collection`。

三个任务的最终 Opening 与 Goals：

1. `pilot_restaurant_walk_in`：Opening=`Good evening! Welcome to Riverside Restaurant. Do you have a reservation?`；Goals=`no_reservation, party_size, table_availability, wait_time`。
2. `pilot_restaurant_ordering`：Opening=`Here is the menu. Are you ready to order, or would you like a recommendation?`；Goals=`recommendation, main_course, dietary_restriction, drink`。
3. `pilot_restaurant_wrong_dish`：Opening=`Here is your meal. Is everything all right with your order?`；Goals=`wrong_dish, original_order, replacement_request, replacement_wait_time`。

`PilotCollectionAssignmentAllocator` 使用 `stableHash(participantId + protocolVersion) % 3` 选 A/B/C。每组同时循环 condition order 和 task pairing，保证每个 Session 三种 embodiment、三个 task 各一次。Assignment 在 Session 创建时持久化；Resume 读取原 snapshot，Retry 只增加 attempt/pilotRunId，不重新分配。

## Embodiment 与受控变量

- Voice Only：无视觉对象；head-locked AudioSource；`spatialBlend=0`。
- Floating Orb：`generated_orb_v1`，只在 feedback speaking 生命周期可见。
- Humanoid：`teacher_female_humanoid_v1`，只播放 feedback；不回退 Orb。
- 三条件共用 Explicit planner、feedback text/hash、Editor Collection feedback voice profile、voiceId、speed、volume、subtitle 和 Stage 3 Feedback First Gate。Provider/embodiment 不进入 correction planner prompt。
- 主对话 Avatar 显式映射为同一 `pilot_restaurant_dialogue_avatar`，不随 embodiment 改变。

`ResetPilotConditionBoundary` 清除视觉实体、反馈/对话音频、STT/TTS、Gate、Goal、问卷 runtime、旧 task/panorama/subtitle 和 run 级订阅，避免 Orb/Humanoid 或 Goal 泄漏。

## Goals、问卷与 Ranking

`UserTranscriptFinalized -> GoalAchievementEvaluator.EvaluatePilotUserTranscript -> GoalProgressTracker automatic confirmation -> panel refresh`。证据包含 system evaluator、turn、transcript、confidence、version 和 UTC。4/4 时只触发一次 task completion 和 `AwaitingQuestionnaire`；TechnicalInvalid 不作为有效完成。

条件问卷使用 `pilot_condition_v1`，1–7 必填校验，回答与 `pilotRunId`/linkage 关联。前两轮提交后进入 neutral transition，第三轮提交后进入 embodiment ranking。Ranking 强制三项唯一 rank，并保存 preferred embodiment、可读名称和 reason。

## TechnicalInvalid、Retry 与 Resume

Operator Control 可将当前 attempt 标为 TechnicalInvalid；原日志保留且不计有效完成。Retry 建立新 `pilotRunId`、attempt+1，同时保持 task、embodiment、position。Resume 从磁盘载入 assignment、completed conditions、questionnaire/ranking marker，不重新计算 group。

## 数据与分析

Bundle 包含 manifest、assignment、timing、study、goals、questionnaires、ranking、integrity、checksums。`SessionDataIntegrityAuditor` 对 Pilot ID/event/ranking 作兼容规范化。Python 分析可生成 assignments、attempts、condition summaries、goals、condition questionnaires、embodiment ranking 和 invalid/retry flags。

## Unity 与自动测试结果

- C# 编译：PASS，Console error=0。
- SampleScene Missing Script/Reference：PASS，0 issue。
- Project EditMode：318/318 PASS，job `070b23ad3662411abf1611217aff44bb`。
- Project PlayMode：41/41 PASS，job `c64bb9996c764170be7d318857b2245f`。
- Python pytest：41/41 PASS。
- Pilot Editor Collection Preflight：READY；protocol、sequence、tasks、embodiments、panorama、voice gateway binding、data directory、exporter、auditor 全部 READY。
- Formal 主菜单、四模式、Formal tests 均通过，没有解除协议决策锁。

## 人工黑盒实跑

指定参与者 `PILOT-COLLECTION-VALIDATION-001` 的首次运行发现并帮助定位了两个真实缺陷：BeginTurn 把 Pilot task 重置为 Hotel，以及 coordinator 订阅晚于 Arm。修复后使用 `PILOT-COLLECTION-VALIDATION-004 / PILOT-VALIDATION-004` 从真实 Unity 主菜单/UI/Session 生命周期完整重跑 Sequence A：Walk-in/Voice Only、Ordering/Orb、Wrong Dish/Humanoid，三轮均 4/4、问卷、Transition、Ranking、Completion、Export 和 Integrity PASS。

本次 Goal 输入采用可控 final-transcript 注入，以保证可重复验证 UI、evaluator 和 lifecycle；未把它描述为现场麦克风、真实 STT/TTS/LLM 网络实跑。因此 Bundle 的本次人工样本没有真实 turn timing 行，语音服务仅验证了绑定与 Preflight。现场参与者收集前仍需做一次真实麦克风/云端链路冒烟测试。

最终 Bundle：`PILOT-COLLECTION-VALIDATION-004_PILOT-VALIDATION-004`，13 files，checksum PASS，integrity PASS。分析输出：1 session、3 assignments、3 attempts、3 condition summaries、12 goals、9 questionnaire items、3 ranking rows、0 exclusions。

## 18 项结论

1. 以前只能从 Team Showcase 进入，是因为 Start 仅检查已准备 Session，没有 collection 创建入口。
2. 新入口调用链见“旧调用链与新调用链”。
3. 标准 Pilot 完全不依赖 Team Showcase。
4. 三种 embodiment 配置见“Embodiment 与受控变量”。
5. 三任务最终内容见“协议、任务与 Assignment”。
6. A/B/C 稳定哈希循环实现三条件三任务平衡。
7. Resume 从持久化 snapshot 读取，不重新分配。
8. 三条件共享 planner、Explicit、voice profile/ID/speed/volume，并测试 hash 等价。
9. Voice Only 不实例化视觉实体且强制 blend=0。
10. boundary reset 与 mutually-exclusive activation 防止 Orb/Humanoid 泄漏。
11. Goal 由 final transcript 自动 evaluator 确认并写证据。
12. Questionnaire 提交关闭面板，前两轮转 Transition，第三轮转 Ranking。
13. 第三轮问卷事件直接进入 Final Ranking。
14. invalid attempt 保留；Retry 新 run；Resume 保持 assignment。
15. Bundle schema、checksum、integrity 和分析均通过，可按 collection policy 收集；真实语音冒烟仍是操作前置项。
16. 完整人工实跑结果见上，最终重跑 PASS。
17. Unity/Python 全量结果见上。
18. 最终提交 SHA 在提交完成后由 `git rev-parse HEAD` 记录并在最终交付消息中报告。

## 已知限制

- 本轮不是 PICO 验收。
- 人工可重复验证未使用现场麦克风/云端生成，不能据此声称实际网络延迟或语音质量通过。
- Avatar/Orb 外观可后续替换，但当前批准资源不阻断 Editor Collection。
