# SceneTalkVR Experiment v1.1 Rehearsal Refactor Report

## Baseline and outcome

- Branch: `experiment-v1.1-integration`
- Starting commit: `f31d832225f0026b0cb94e0384658503046c01e4`
- Unity: `6000.3.16f1`
- Protocol: `1.1-rehearsal-1`
- Implementation commit: this report is part of commit `refactor(experiment): promote editor demo to collection-equivalent rehearsal`; use repository HEAD after delivery for the immutable SHA.

完成了 Editor Demo → Collection-equivalent Rehearsal 的代码重构。`ExperimentFlowMode`（DeveloperManual/Formal/Pilot/Synthetic）与 `ExperimentRunQualification`（Development/Rehearsal/Collection）成为正交维度。Formal/Pilot Rehearsal 不再是 Developer 子模式，也不使用简化 Assignment、生命周期、问卷或事件 schema。

## Core implementation

- `ExperimentCoreModel.cs`: runtime context、flow/qualification、snapshot identity。
- `ExperimentV11RehearsalProtocol.cs` + `.asset`: 11 项 ApprovedForRehearsal 决策、Formal/Pilot sequence、回合/时长限制。
- `ExperimentV11RehearsalResourceCatalog.cs` + `.asset`: avatar/panorama/humanoid snapshot。
- `ExperimentVoiceProfileCatalog` / `ExperimentDeploymentCatalog`: Rehearsal 审批和 validation。
- `RehearsalSessionCoordinator.cs`: operator facade；内部复用 `ExperimentLifecycleCoordinator`、`PilotWorkflowCoordinator`、`QuestionnaireRuntimeController` 和 `SceneTalkOrchestrator`。
- `SceneTalkRehearsalControlWindow.cs`: 独立实验员控制台。
- `RehearsalBundleExporter.cs`: 使用 `SessionBundleExporter` 与 `SessionDataIntegrityAuditor`，schema 为 `1.1-collection-equivalent`。
- `RehearsalValidation.cs`: Formal/Pilot Preflight、equivalence validator、collection readiness report。

## Answers required by the task

1. Rehearsal 使用真实 Formal/Pilot 生命周期：是；分别复用 `ExperimentLifecycleCoordinator` 与 `PilotWorkflowCoordinator`。
2. 与 Collection 的代码差异：只有 qualification、protocol/resource snapshot、审批、deployment、数据根目录和 collection eligibility；核心流程相同。
3. 参与者端 Demo/Developer UI：Rehearsal 激活时隐藏 Demo banner/status、内部 debug、手动任务列表和 operator controls。
4. 真实 STT/TTS/LLM pipeline：配置为 `RehearsalEditor`/`127.0.0.1:8787`，禁止 mock；本次服务离线，未完成真实实跑。
5. Formal 四条件完整实跑：自动生命周期路径通过；真实麦克风完整实跑因 Gateway 离线阻断。
6. Pilot 三形态完整实跑：自动生命周期路径通过；真实音频实跑因 Gateway 离线阻断。
7. Assignment：Formal/Pilot 均由现有 allocator 自动生成并无放回。
8. Resume/Retry：支持；Retry 通过共享 workflow 生成新 run ID，旧 Demo assignment 不自动迁移。
9. Bundle schema：与 Collection 共用 exporter/auditor，Rehearsal manifest 增加 qualification/snapshot metadata。
10. 当前资源：四个 Formal avatar、Pilot humanoid、Orb、五类本地 panorama、Tencent `101050`。
11. 替换资源：通过 resource/voice/deployment catalog，无需修改流程。
12. 提升 Collection：补齐资源/部署/PICO/Collection 审批；`RehearsalToCollectionReadinessReport.lifecycleCodeChangeRequired=false`。
13. 旧 Demo：禁止创建新 session，历史数据仍可只读/Resume，旧窗口入口重定向到 Rehearsal Control。
14. 测试：Rehearsal EditMode `47/47`、Rehearsal PlayMode `17/17`、项目 EditMode `187/187`、项目 PlayMode `25/25`、Python pytest `38/38`。
15. 最终 SHA：见交付后的分支 HEAD（报告无法自引用尚未创建的 commit hash）。

## Voice and timing correction

审计发现旧路径只记录 `voiceProfileKey`，`AvatarSpeechPlayer` 仍可能按 gender 使用默认 voiceId，Pilot 还可能把 profile key 当 voiceId。现已在共享播放边界解析 Rehearsal catalog：dialogue 使用 `rehearsal_dialogue_voice`，feedback 使用 `rehearsal_feedback_voice`，两者实际 voiceId 均为 `101050`；非 Rehearsal 行为不变。

## Validation evidence

- Compile: PASS, 0 errors（10 个既有 warning）。
- Missing Script/Reference: 0/0；scene validator 仅报告 3 条 info。
- Minimum Play Mode: 10 秒、0 runtime error。
- Formal/Pilot Preflight: 当前 `REHEARSAL_BLOCKED`，唯一运行阻断为 Voice Gateway 不可达；资源非 Collection-approved、无 PICO 仅为 warning。
- 未启动第二个 Unity Editor；Unity Skills server 为 Bypass。

## Known blockers and follow-up

P0：启动 `127.0.0.1:8787` 的真实 Voice Gateway，完成 Formal 四条件与 Pilot 三条件标准实跑、实际问卷/排序/访谈、真实 Bundle audit 和全部规定截图。P1：完成 Collection avatar/panorama/voice/deployment approvals 与 PICO 验证。当前代码可作为长期保留的 Editor Rehearsal fallback，但不能因为自动测试通过就宣称真人语音 Rehearsal 已完成。
