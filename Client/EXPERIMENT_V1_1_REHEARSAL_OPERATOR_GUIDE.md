# SceneTalkVR Rehearsal Operator Guide

1. 启动 Unity，打开 `SampleScene`，确认电脑端 Voice Gateway 已在 `127.0.0.1:8787` 运行。
2. 执行 `SceneTalkVR > Experiment > Rehearsal > Run Formal Preflight` 或 Pilot Preflight；只有 `REHEARSAL_READY/WARNING` 才开始。
3. 打开 `SceneTalkVR > Experiment > Rehearsal Control`。
4. 选择 Formal 或 Pilot，输入 participantId/sessionId，点击 Create Session。参与者端不会出现手动任务选择。
5. 点击 Prepare Next Condition；参与者完成真实语音任务后，由实验员处理 Goal candidate 的 Confirm/Reject。
6. 点击 Complete Task，打开并由参与者填写每条件问卷，再提交边界。
7. 技术失败时点击 Mark Technical Invalid，记录原因；修复后 Retry 会创建新的 run ID。
8. 完成全部条件后填写最终 Ranking；Formal 还需保存 Interview。
9. Export Bundle，再执行 Integrity Audit。只有 PASS/WARN 且无 FAIL 才可保留为有效 rehearsal evidence。

Advanced QA 的 Auto-Fill/Auto-Complete 只用于重复回归，事件 actor 为 `rehearsal_qa_operator` 且 `qaAutomationUsed=true`；不得代替标准 Rehearsal 实跑。

旧 `SceneTalk Team Showcase` 菜单只用于历史 Demo 数据读取，创建新 Demo Session 已被代码拒绝并重定向到 Rehearsal Control。
