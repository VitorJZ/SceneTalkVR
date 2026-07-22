# Experiment v1.1 Formal Rehearsal Run Report

## Result

`BLOCKED_EXTERNAL_SERVICE` — Editor 编译、资源、生命周期与 UI 自动回归通过，但当前真实 Voice Gateway `http://127.0.0.1:8787` 拒绝连接，因此未声称完成真实 microphone → STT → correction/dialogue → TTS 的四条件实跑，也未生成冒充真实会话的 Bundle。

## Verified in the open Unity Editor

- Unity `6000.3.16f1`，单一已打开 Editor。
- Formal Rehearsal allocator 生成四条件、四任务无放回 Assignment；自动测试通过。
- `ExperimentLifecycleCoordinator` 的 Prepare/Reset/Goal/Questionnaire/TechnicalInvalid/Retry/Resume 路径通过 EditMode/PlayMode。
- Hotel、Furniture、Gym、Tourist 本地 panorama 与四个 rehearsal avatar mapping 可解析。
- Rehearsal dialogue/feedback profile 均解析为 Tencent voiceId `101050`。
- 10 秒最小 Play Mode：0 runtime error；主菜单截图为 `Assets/Screenshots/rehearsal-participant-main-menu.png`。

## Not yet verified manually

- NE/NR/SE/SR 各一次的真实语音播放与 Feedback First 听感。
- 四次参与者实际问卷、最终排序、访谈。
- 真实原始事件构成的 Bundle checksum/integrity PASS。
- 要求的 formal welcome/task/feedback/questionnaire/ranking 截图。

启动 Voice Gateway 后，按 `EXPERIMENT_V1_1_REHEARSAL_OPERATOR_GUIDE.md` 完成一次四条件运行即可解除该阻断；不得使用 QA Auto-Fill 代替标准运行。
