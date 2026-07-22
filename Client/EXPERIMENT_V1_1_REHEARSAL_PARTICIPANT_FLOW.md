# SceneTalkVR Rehearsal Participant Flow

```text
Operator creates assignment
  → Participant waiting/welcome
  → Fixed task scene + opening question + read-only goals
  → Real microphone turn(s)
  → Feedback First correction (condition-controlled)
  → Avatar dialogue continuation
  → Task completion
  → Per-condition questionnaire
  → Boundary reset
  → Next assigned condition
  → Final ranking
  → Completion
```

参与者 Game View 不显示 Demo banner、runtime mode、内部 ID、Auto-Fill 或实验员按钮。任务选择由 allocator 完成；参与者不能改变 task、condition、provider 或 style。Goal panel 只读，Experimenter Confirm/Reject 仍通过 coordinator API。

Formal 顺序包含 NE/NR/SE/SR 与四个正式任务各一次；Pilot 包含 Voice Only、Floating Orb、Humanoid 及三个餐厅任务各一次。Voice Only 为 non-spatial head-locked。条件边界会清除 LLM、播放 Gate、音频、Avatar/Agent、Goal 和 Questionnaire 状态。
