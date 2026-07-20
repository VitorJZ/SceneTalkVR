# Experiment v1.1 Rehearsal / Collection Equivalence

结论：Rehearsal 复用 Collection 的 Assignment、生命周期、Feedback First、Goal、Questionnaire、Ranking、事件日志、Bundle exporter 和 integrity auditor。`RehearsalSessionCoordinator` 仅负责建立 runtime context、选择 Rehearsal 审批资产、驱动共享 coordinator，并不实现一套简化实验。

| 领域 | Formal Rehearsal | Formal Collection | Pilot Rehearsal | Pilot Collection |
|---|---|---|---|---|
| Assignment | `ExperimentAssignmentAllocator` | 同一类型/allocator | `PilotAssignmentAllocator` | 同一类型/allocator |
| Lifecycle | `ExperimentLifecycleCoordinator` | 同一 coordinator | `PilotWorkflowCoordinator` | 同一 coordinator |
| Task/Goal | `ExperimentTaskCatalog` / `GoalProgressTracker` | 相同 | 相同 | 相同 |
| Questionnaire | `QuestionnaireSessionService` | 相同 | 相同 | 相同 |
| Timing | `FeedbackFirstPlaybackGate` / event JSONL | 相同 | 相同 | 相同 |
| Bundle | `SessionBundleExporter` + `SessionDataIntegrityAuditor` | 相同 schema/auditor | 相同 | 相同 |

允许差异仅为：`ExperimentRunQualification`、协议/资源 snapshot、资源审批、`RehearsalEditor` deployment、`RehearsalSessions` 数据根目录，以及 `collectionEligible=false`。

`RehearsalCollectionEquivalenceValidator` 自动检查共享类型引用；切换到 Collection 不应修改 allocator、lifecycle、Task、Feedback First、Questionnaire、Ranking 或日志代码，只需完成资源、部署、PICO 与 Collection 审批。
