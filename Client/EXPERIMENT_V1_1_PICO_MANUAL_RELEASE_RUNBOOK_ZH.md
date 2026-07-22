# Experiment v1.1 PICO 正式采集人工发布 Runbook

英文原版：`EXPERIMENT_V1_1_PICO_MANUAL_RELEASE_RUNBOOK.md`

仅当所有自动化门槛均已通过后，才使用本 Runbook。本文有意将网络地址、批准引用、
设备身份和人工观察结果留空；这些信息必须来自真实的实验室或便携部署环境，不得编造。

本文生成时的状态：**正式参与者采集 NO-GO（禁止开始）**。

## 1. 候选版本与操作人员记录

在仓库外创建证据目录，并记录以下信息：

- 候选 Git SHA：`<git rev-parse main>`
- APK 路径和 SHA-256：`<Get-FileHash -Algorithm SHA256 ...>`
- 日期、时间和时区：`<实际值>`
- 操作人员：`<实际值>`
- PICO 型号、序列号、系统及构建版本：`<实际值>`
- 部署 Profile：`PicoLab` 或 `PicoPortable`
- 研究负责人批准引用：`<真实且不可变的引用>`
- 语音网关主机和端口：`<实际获批的局域网端点>`
- LLM 网关主机和端口：`<实际获批的局域网端点>`

任一字段为空时，不得开始参与者采集。当前验证 APK 使用 Android Debug 证书签名，且
`debuggable=true`；它只是工程验证产物，不能作为正式签名发布已经获批的证据。

## 2. 凭据泄露事件闭环

共享分支历史中曾提交过两组凭据。在调用任何真实服务前必须完成：

1. 在对应服务提供方撤销两组已暴露凭据。
2. 通过获批的秘密传递渠道签发替代凭据。
3. 替代凭据只能配置在服务端环境变量或本地私密配置中；不得写入 Unity 资源、场景、
   Markdown、命令历史、截图或 Session Bundle。
4. 与仓库负责人共同决定是否重写 Git 历史。当前头部的脱敏不会清除旧提交、已有克隆
   或远端引用中的凭据。
5. 在仓库根目录执行：

   ```powershell
   py -3.11 scripts/verify_no_tracked_secrets.py
   ```

预期结果：`PASS`。如果批准重写历史，必须另行制定包含所有克隆和远端协同的方案；
不得在参与者采集期间执行历史重写。

## 3. 批准真实 PICO 部署 Profile

在 `Assets/SceneTalkVR/ExperimentProtocol/ExperimentDeploymentCatalog.asset` 中，仅使用
真实且已获批的值创建两个 Profile。每个 Profile 必须满足：

| 字段 | 必填值 |
| --- | --- |
| `profileId` | `PicoLab` 或 `PicoPortable` |
| `voiceGatewayBaseUrl` | `http://<approved-host>:8787`；不得为 loopback，不得携带查询 Token |
| `requestTimeoutSeconds` | 大于 0 的获批超时时间，通常为 30 |
| `sttProvider` / `ttsProvider` | 真实、非空、非 mock 的提供方标识 |
| `microphonePolicy` | 获批策略，通常为 `runtime_permission_required` |
| `networkRequired` | `true` |
| `approvedForCollection` / `collectionAllowed` | 仅在研究负责人批准后设为 `true` |
| `target` | `Pico` |
| `picoRequired` | `true` |
| `loopbackAllowed` | `false` |
| Editor、Demo、Rehearsal 批准字段 | 除非另有明确依据，否则均为 `false` |
| `evidenceReference` | 真实且不可变的批准、工单或协议引用 |

在以下位置写入同一个已获批服务主机：

- `Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset`
  - `voiceGatewayBaseUrl=http://<approved-host>:8787`
  - `directLlmApiUrl=http://<approved-host>:8788/api/llm/chat/completions`
- `Assets/SceneTalkVR/Voice/VoiceGatewaySettings.asset`
  - `gatewayBaseUrl=http://<approved-host>:8787`

不得在没有核对真实 PICO 网络的情况下，直接使用审计时看到的 WLAN 地址。在 PICO 上
确认两个 `/health` 端点都返回预期 JSON。如果头显没有可用浏览器，可通过已连接设备的
Shell 做可达性初检：

```powershell
& $adb shell ping -c 3 <approved-host>
& $adb shell "toybox nc -z -w 3 <approved-host> 8787"
& $adb shell "toybox nc -z -w 3 <approved-host> 8788"
```

`ping` 可能被防火墙拦截，部分 PICO 系统也可能没有 `nc`；应用内真实健康请求成功才是
权威证据。不得用 `adb reverse` 作为采集证据，因为它绕过了真实局域网部署路径。

完成配置后，从终端重新运行 Preflight：

```powershell
& 'E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath '<repo>\Client' `
  -executeMethod SceneTalkVR.EditorTools.SceneTalkPreflightMenu.RunPreflightCheck `
  -logFile '<evidence>\preflight.log'
```

重新生成的报告必须显示：无 Missing Script、两个 PICO Profile 均已批准，并且 Formal、
Pilot 的准备状态符合本次部署意图。

## 4. 连接、识别并安装到 PICO

使用 Unity 随附的 ADB：

```powershell
$adb = 'E:\ProgramFile\UnityEditor\6000.3.16f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
$apk = '<已获批的候选 APK>'
& $adb start-server
& $adb devices -l
```

如果列表为空或显示 `unauthorized`，立即停止。开启头显的开发者模式和 USB 调试，在
头显中接受授权提示，然后重复执行，直到列表中出现一个已授权设备。

安装前采集设备身份：

```powershell
& $adb shell getprop ro.product.manufacturer
& $adb shell getprop ro.product.model
& $adb shell getprop ro.serialno
& $adb shell getprop ro.build.version.release
& $adb shell getprop ro.build.fingerprint
& $adb install -r $apk
& $adb shell dumpsys package com.scenetalkvr.demo | Select-String 'versionCode|versionName|RECORD_AUDIO'
```

仅在干净的 Rehearsal 开始前，可以使用
`adb shell pm clear com.scenetalkvr.demo`。参与者会话结束后，在 Bundle 尚未导出、计算
哈希并备份前，绝对不得清除应用数据。

在第二个终端开始采集日志，然后按 APK Manifest 中的准确 Activity 启动应用：

```powershell
& $adb logcat -c
& $adb logcat -v threadtime > '<evidence>\pico-logcat.txt'
& $adb shell am start -n com.scenetalkvr.demo/com.unity3d.player.UnityPlayerActivity
```

首次启动时，必须真实触发运行时麦克风权限提示。根据测试用例要求，在头显 UI 中允许
或拒绝；主权限流程测试不得预先授予权限。完成后使用 `dumpsys package` 确认实际权限
状态。

## 5. 强制人工视觉检查

必须在头显内部检查，不能只看桌面镜像。每个页面都要保存截图或视频，并记录以下人工
观察：字体是否可读、是否有裁切或重叠、世界空间深度是否正确、控件是否可触达、按钮
选中和禁用状态是否正确、控制器射线交互是否有效。

必须检查的页面：

1. Developer 模式：来源、风格、具身控制项和 History。
2. Formal 参与者开始页及分配、任务页；手动纠错控制项和 History 必须隐藏。
3. Formal 目标面板、条件问卷、最终排序和访谈边界。
4. Pilot 参与者开始页、任务和目标面板、条件问卷及最终排序。
5. 错误或 `technical-invalid` 状态及重试路径。
6. 麦克风权限先拒绝、后允许时的行为。

可靠的截图采集命令：

```powershell
& $adb shell screencap -p /sdcard/scenetalk-check.png
& $adb pull /sdcard/scenetalk-check.png '<evidence>\screenshots\<view-id>.png'
```

操作人员还必须亲自通过头显观察。单张截图无法证明双眼舒适度、空间深度、控制器可达性
或实际阅读体验。

## 6. Formal 16 单元 Rehearsal

使用明确标记为 Rehearsal 的参与者身份，并使用锁定的分配流程。不得人工覆盖条件顺序。
完成并记录全部组合：

| 条件 | 任务 |
| --- | --- |
| `NE`、`NR`、`SE`、`SR` | `hotel_check_in`、`furniture_shopping`、`gym_membership`、`tourist_assistance` |

对 16 个单元中的每一个，都要核对：分配的固定全景、准确的任务 Avatar、任务目标、
至少一个含反馈轮次、问卷关联、完成边界，以及保存的 condition-run、task-assignment ID。
完成全部四个条件后，还要核对最终排序和访谈关联。

## 7. Pilot 9 单元 Rehearsal

完成并记录全部组合：

| 具身条件 | 任务 |
| --- | --- |
| `voice_only`、`floating_orb`、`humanoid_agent` | `pilot_restaurant_walk_in`、`pilot_restaurant_ordering`、`pilot_restaurant_wrong_dish` |

每个单元都要确认：反馈文本哈希一致、使用共享 Voice Profile、语速为 `1`、音量为 `1`、
字幕策略为 `feedback_only`。各条件专项检查：

- `voice_only`：无视觉实体；头锁定、非空间音频（`spatialBlend=0`）。
- `floating_orb`：仅 Orb 可见；世界空间定位的空间音频（`spatialBlend=1`）。
- `humanoid_agent`：仅获批 Humanoid 可见，具备 Idle、Talking 行为，并使用世界空间定位
  的空间音频（`spatialBlend=1`）。

确认只有三个条件全部完成后，Pilot 最终排序才可用。

## 8. 真人语音与 Feedback First 证据

使用真人麦克风话语，至少完成一个“有反馈”轮次和一个“无反馈”轮次。必须调用真实
服务链路：

`麦克风 -> STT -> LLM 纠错和对话 -> TTS -> 反馈播放 -> 对话播放`

有反馈轮次的时序证据必须显示：

- `DialogueGateClosed` 早于对话播放。
- `CorrectionPlaybackStarted`、`CorrectionPlaybackEnded` 依次出现，且均早于
  `DialogueGateOpened` 和 `DialoguePlaybackStarted`。
- `DialoguePlaybackEnded` 后出现 `TurnCompleted`。
- 时间戳单调递增，派生延迟字段均为非负值。

无反馈轮次中不得存在任何纠错播放事件；闸门必须在对话播放前开放。还必须在 Rehearsal
中人为制造一次受控服务失败。锁定的 Formal、Pilot 必须记录 `TurnTechnicalInvalid`，
保持对话闸门关闭，并且不得将降级输出伪装为有效参与者轮次。

人工听取并记录：语音可懂度、削波、重复播放、空间方向、Avatar 口型和身体动画，以及
视觉实体是否在正确边界出现、消失或切换。仅有日志顺序不足以构成音频证据。

## 9. Bundle 导出与非修改式审计

分析前，对源 Bundle 中每个文件计算哈希：

```powershell
Get-ChildItem '<bundle>' -Recurse -File | Sort-Object FullName |
  Get-FileHash -Algorithm SHA256 |
  Export-Csv '<evidence>\bundle-hashes-before.csv' -NoTypeInformation
```

按文档要求从源码布局运行分析管线；普通 wheel 安装不是受支持的运行布局：

```powershell
Set-Location '<repo>\Client\Analysis'
$env:PYTHONPATH = (Resolve-Path '.\src').Path
py -3.11 -m scenetalkvr_analysis validate-bundle '<bundle>'
py -3.11 -m scenetalkvr_analysis analyze-bundle '<bundle>' --config '<获批的分析配置>'
```

再次计算源 Bundle 哈希并比较：

```powershell
Get-ChildItem '<bundle>' -Recurse -File | Sort-Object FullName |
  Get-FileHash -Algorithm SHA256 |
  Export-Csv '<evidence>\bundle-hashes-after.csv' -NoTypeInformation
Compare-Object (Import-Csv '<evidence>\bundle-hashes-before.csv') `
               (Import-Csv '<evidence>\bundle-hashes-after.csv') `
               -Property Path,Hash
```

预期比较输出为空。审计必须确认：校验和有效、Feedback First 顺序正确、问卷、排序和访谈
关联完整，并且所有 `technical-invalid` 尝试均被保留。

## 10. 最终 GO/NO-GO 签字

只有以下全部证据绑定到同一个候选 SHA 和 APK 哈希时，正式采集才可以判定为 GO：

- 凭据撤销和轮换证据；
- 已获批的 `PicoLab`、`PicoPortable` Profile；
- 全绿的 Preflight、EditMode、PlayMode、Python、Formal 16、Pilot 9 结果；
- 已授权 PICO 的身份和安装证据；
- 头显视觉检查表及截图；
- 真实语音、有反馈、无反馈和失败轮次的时序证据；
- 不可变的 Bundle 哈希和完整性审计报告；
- 构建签名决策及研究负责人批准。

签字字段：

- 操作人员及日期：`<实际值>`
- 技术审核人员及日期：`<实际值>`
- 研究负责人及日期：`<实际值>`
- 决策：`<GO | NO-GO>`
- 证据根目录及校验和：`<实际值>`
