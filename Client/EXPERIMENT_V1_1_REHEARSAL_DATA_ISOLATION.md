# Experiment v1.1 Rehearsal 数据隔离

Rehearsal 数据固定写入 `Client/Library/SceneTalkVR/RehearsalSessions/<participant>_<session>/raw`，Bundle 位于同一 session 根目录。它不会写入 Collection 默认日志目录。

每个 Assignment、Pilot event、Study event、Operator event 与 Bundle manifest 使用：

- `runQualification=Rehearsal`
- `dataOrigin=rehearsal`
- `collectionEligible=false`
- `developerTestAssignment=false`
- `demoMode=false`
- `protocolSnapshotId=v1.1-rehearsal-1-protocol`
- `resourceSnapshotId=v1.1-rehearsal-1-resources`

`SessionDataIntegrityAuditor` 会将 rehearsal assignment 中任一 collection/developer/demo 标记冲突判为 FAIL。分析端默认不把 Rehearsal 当作 Collection；只有测试配置显式启用 `includeRehearsalForTesting` 时才能导入。

删除或更换 Session 时必须通过 `RehearsalSessionCoordinator.ResetSession`，它调用共享 condition boundary reset，清理 LLM、Gate、TTS、Avatar、Agent、Goal、Questionnaire 和运行引用。
