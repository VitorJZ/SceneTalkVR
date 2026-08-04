# 正式问卷界面与场景统计导出工作汇总

日期：2026-08-04
提交前基线：`fd80140`（`main`）

## 1. 本次工作范围

本次修改完成两项工作：

1. 放大正式实验问卷面板、题目文字和操作区域，提高 PICO 真机中的可读性与点击稳定性。
2. 完善 Excel 的 `FormalSceneStats` 工作表，新增正式条件、对话轮次和纠错次数，并补充数据关联及旧记录兼容逻辑。

## 2. 正式问卷界面调整

- 问卷面板由 `920 × 560` 放大为 `1120 × 720`。
- 标题、题目、进度、校验提示和按钮文字同步放大。
- 重新调整分页按钮、跳过按钮和提交按钮的位置及尺寸。
- Likert 选项点击区扩大为 `50 × 52`，横向间距调整为 `56`，避免相邻选项重叠。
- 动态生成题目时直接应用用户字体缩放设置，修复页面重建后新题目未正确缩放的问题。
- PlayMode 测试新增面板尺寸、题目字号、Likert 点击区尺寸和选项间距断言。

## 3. `FormalSceneStats` 导出调整

### 3.1 列结构

工作表列顺序调整为：

1. `participantId`
2. `完成时间`
3. `taskId`
4. `formalCondition`
5. `对话轮次`
6. `纠错次数`
7. 按问卷目录顺序排列的各题得分

`formalCondition` 与 `Questionnaires` 工作表使用相同映射：

| 枚举值 | 导出值 |
| --- | --- |
| `0` | `NE` |
| `1` | `NR` |
| `2` | `SE` |
| `3` | `SR` |

前六列设为冻结列，便于横向查看题目得分时保留场景上下文。

### 3.2 对话统计关联规则

- 优先使用问卷会话的 `conditionRunId` 和问卷记录的 `attemptId` 关联对应对话。
- 同时校验 `taskId`，避免把同一参与者的其他场景计入当前问卷。
- 对话摘要存在 `turnCount`、`correctionCount` 时直接使用摘要值。
- 旧数据没有摘要计数时，从对话回合列表回退计算轮次数和带纠错反馈的回合数。
- 同一对话 `sessionId` 只统计一次，防止重复记录造成重复累计。
- 无法建立可靠关联时写入 `-1`，不使用不确定数据伪造统计值。

## 4. 此前 Excel 缺少新列的原因

检查用户提供的 `questionnaire_records.xlsx` 时，电脑端 `8789` History Export Receiver 仍是旧进程。统一启动器检测到健康端口后复用了该进程，因此新代码虽然已经存在于工作区，实际导出仍由旧版本处理，`FormalSceneStats` 中不会出现 `formalCondition`、`对话轮次` 和 `纠错次数`。

该问题不是 PICO 历史数据缺失，也不是 Excel 查看软件隐藏列。部署本次修改后必须重启 History Export Receiver（或统一网关启动器），使 `8789` 加载新版代码，然后从 PICO 重新导出文件；已经生成的旧 Excel 不会自动补列。

## 5. 验证结果

| 验证项目 | 结果 |
| --- | --- |
| History Export Receiver Python 测试 | 9 / 9 通过 |
| Python `py_compile` | 通过 |
| Unity 相关 PlayMode 测试 | 3 / 3 通过 |
| `Assembly-CSharp.csproj` 编译 | 0 错误 |
| PlayMode 测试程序集编译 | 0 错误 |
| Unity Console | 0 error |
| 真实历史 JSON 临时导出验证 | 20 条正式场景记录，NE/NR/SE/SR 映射正确 |
| 工作表交叉核对 | `FormalSceneStats` 与 `Questionnaires.formalCondition` 一致 |
| Excel 可视检查 | 通过，无公式错误 |

自动化测试覆盖：正式/彩排记录过滤、提交与跳过问卷、条件映射、摘要计数、旧回合数据回退、错误关联排除、列顺序和冻结列设置。

## 6. 部署与使用说明

1. 重启电脑端统一网关，确认 Voice `8787`、LLM `8788`、History Export `8789` 的健康检查均通过。
2. 确认 PICO 的 ADB 状态为 `device`，且 `adb reverse --list` 包含 `8787`、`8788`、`8789`。
3. 在 PICO 首页重新执行历史导出。
4. 打开新生成目录中的 `questionnaire_records.xlsx`，检查 `FormalSceneStats` 的第 4 至第 6 列。

本次修改不迁移或重写 PICO 本地历史数据，不修改既有 Excel 文件，也不向远端仓库推送提交。
