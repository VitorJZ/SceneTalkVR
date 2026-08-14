# 正式对话纠错泄漏误判修复汇总

日期：2026-08-14

## 问题表现

正式实验生成角色回复后，流程可能报错并停止继续录音：

```text
[SceneTalkVR] Dialogue reply generation failed. Formal turn invalid: correction leakage detected in Avatar dialogue.
```

本次复现中的角色回复属于正常餐厅对话，包含以下表达：

- `this mistake is on us`
- `correcting this mistake`
- `the wrong dish`

这些内容是在说明餐品错误、道歉或处理换餐，不是在纠正参与者的英语。

## 根因

`CorrectionTextGuards.LooksLikeCorrection` 原先会将 `mistake`、`wrong`、`incorrect` 等单个判断词直接识别为“语言纠错泄漏”。这些词也广泛用于餐厅、酒店等正常角色对话，因此产生误报。

误报后，正式回合会被判定无效，并沿现有正式实验保护流程记录为技术无效状态，所以当前 attempt 无法继续录音。修复规则后，已经被持久化为技术无效的旧 attempt 不会被自动改写，需要重新进入或重新开始对应条件。

## 修复内容

修改文件：

- `Client/Assets/SceneTalkVR/Scripts/Core/CorrectionTextGuards.cs`
- `Client/Assets/SceneTalkVR/Tests/Editor/FeedbackFirstTurnTests.cs`

纠错泄漏识别调整为两类规则：

1. 明确的教学或纠错短语仍直接判定为纠错，例如 `you should say`、`try saying`、`grammar`、`a better way to say`、`instead of saying`。
2. `mistake`、`wrong`、`incorrect`、`correct`、`better` 等判断词，只有同时出现语言对象时才判定为纠错。语言对象包括 `sentence`、`phrase`、`expression`、`word choice`、`verb`、`tense`、`pronunciation`。

同时新增内部复用的 `ContainsAny` 辅助方法，避免重复遍历代码。

## 回归覆盖

新增测试确认以下正常角色回复不会再触发纠错泄漏：

- 餐厅承担餐品错误责任或说明免费更换。
- 道歉并更换错误餐品。
- 路线语境中的 `a better way`。
- 正常对话中的 `you can say hello`。

同时确认以下真实语言纠错仍会被拦截：

- `Your sentence is wrong.`
- `That expression is incorrect.`
- `There is a mistake in the verb tense.`
- `A better way to say it is ...`
- `instead of saying ...`

## 验证结果

- 纠错守卫正反例样例：10/10 通过。
- `Assembly-CSharp.csproj` 编译：0 错误。
- `Assembly-CSharp-Editor.csproj` 编译：0 错误。
- `git diff --check`：通过。
- 编译过程中保留的 Unity/MCP 依赖版本与旧 API 警告为既有警告，与本次修复无关。

## 提交范围

本次提交只包含纠错泄漏误判修复、对应回归测试和本文档。以下现有工作区改动不属于本次修复，保持未暂存且不纳入提交：

- `SceneTalkVR Task Goal UI` 的 Y Rotation 调整及相关场景/UI 流程测试。
- `ExperimentBuildInfo.asset` 构建信息变更。

本次只创建本地提交，不推送远端。
