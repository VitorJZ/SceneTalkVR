# SceneTalkVR 历史记录系统实施说明（2026-07-20）

## 1. 数据边界

- 历史数据库位于 `Application.persistentDataPath/SceneTalkVR/History/scenetalk_history.sqlite3`。
- 动态全景图缓存位于同目录的 `Assets/<sessionId>/`。
- 旧 `ExperimentLogs/*.jsonl|csv` 仍是独立实验日志，不会迁移到历史列表，也不会随历史删除而改写。
- `formalExperiment=true` 时首页不显示 History 入口，也不初始化数据库、不写入会话或回合；实验日志仍按原流程记录。

## 2. 运行时架构

`SceneTalkOrchestrator` 是唯一的历史流程入口。UI 只发送打开、分页、选择、继续和删除命令，不直接读写数据库。

```text
Idle
  -> HistoryLoading -> HistoryList
  -> HistoryLoading -> HistoryDetail
  -> HistoryDeleteConfirm -> HistoryLoading -> HistoryList
  -> HistoryRestoring -> TurnReview
```

`LearningMemoryService` 管理当前活动会话并延迟初始化数据库；`SqliteLearningMemoryStore` 负责 SQLite schema 迁移、分页、事务、级联删除和增量空间回收。Brain 通过 `ISceneTalkConversationContextReceiver` 恢复 system/opening/user/assistant 消息及已纠错错误类型，Avatar 通过 `ISceneTalkAvatarSessionPrepare` 静默加载。继续历史时还会恢复原 Brain 模式；所需模块不存在时进入 `HistoryError`，返回详情后可再次尝试。

每次重新选择任务都会生成新 sessionId。固定任务的开场白保存为 opening，但不计入用户对话轮次；后续每个成功生成的用户回合均以事务追加。

## 3. UI

- 首页：`Start / Settings / History / Quit`。
- 历史列表：每页 5 条，使用 Previous/Next，按更新时间倒序，并提供明确的 Back。
- 详情页：显示任务、环境、Avatar、纠错来源/风格/灵敏度、轮次、纠错次数及完整对话和纠错文本，提供 Continue/Delete/Back。
- 长文本：使用 ScrollRect，并提供 Up/Down 按钮供 PICO 射线点击。
- 删除：必须进入确认状态，确认后删除会话、回合和场景缓存。

## 4. 手动验证

1. 在 Unity 执行 `Assets > Refresh`，等待 Package Manager 完成 `com.gilzoide.sqlite-net` 解析和脚本编译。
2. 确认 Console 中项目代码为 0 error。
3. 打开 `Window > General > Test Runner > EditMode`，运行：
   - `LearningMemoryStoreTests`
   - `HistoryStateMachineTests`
   - `ConversationContextRestoreTests`
   - 原有 `ExperimentConditionRuntimeSwitchTests`
4. Play Mode 中完成至少两次相同场景练习，每次退出后重新选择，确认 History 中出现两条独立记录。
5. 打开详情，检查对话、纠错和统计；Continue 后确认场景与纠错方式恢复，且新回合追加到原记录。
6. 删除其中一条并重启 Play Mode，确认删除结果持久化且另一条不受影响。
7. 将 `formalExperiment` 临时设为 true，确认首页 History 入口消失；验证后恢复原值。
8. 执行 Android ARM64 IL2CPP Build & Run，在 PICO 上验证分页、Up/Down、Continue 和 Delete 射线点击。

Windows Editor 数据目录通常为：

```text
C:\Users\<User>\AppData\LocalLow\SceneTalkVR\SceneTalkVR\SceneTalkVR\History
```
