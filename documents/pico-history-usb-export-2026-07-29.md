# PICO 实验历史 USB 导出说明

## 功能

PICO 首页的“导出历史数据”按钮会通过 USB/ADB reverse 将实验历史发送到电脑。该功能只使用 `127.0.0.1:8789`，不会通过局域网传输实验数据，也不会修改或删除 PICO 上的 SQLite 历史数据库。

导出范围包括全部预实验和正式实验，无论状态为进行中、暂停或已完成。JSON 包含实验、attempt、完整对话回合、问卷及最终排序；独立的非实验“对话历史”和录音、图片等资源文件不包含在内。

## 使用方法

1. 在 PICO 上开启开发者模式和 USB 调试，并授权当前电脑。
2. 使用可传输数据的数据线连接 PICO。
3. 在仓库根目录启动统一后台：

   ```bash
   python Server/gateway-launcher/scenetalk_gateway_launcher.py
   ```

4. 回到 PICO 首页，点击“导出历史数据”。

默认电脑目录为：

```text
Documents/SceneTalkVRExports/yyyyMMddTHHmmssZ_<exportId>/
├── experiment_history.json
└── questionnaire_records.xlsx
```

可使用 `--export-dir <folder>` 修改保存根目录。

## 文件内容与排序

- `experiment_history.json`：schema `1.0`，实验及其子记录均按时间从早到晚排序；相同时间使用 ID 稳定排序。
- `questionnaire_records.xlsx`：保留 `Questionnaires`、`Responses`、`Scores` 三个原始数据工作表，并新增 `FormalSceneStats`、`FormalRankingStats` 两个正式实验统计工作表。
- `FormalSceneStats`：每行是一份已提交或已跳过的正式场景问卷，依次展示 `participantId`、北京时间完成时间、`taskId` 和各题校正后得分；没有得分的题目使用数值 `-1`。
- `FormalRankingStats`：每名参与者一行；若同一 `participantId` 存在多份正式最终排序，只保留完成时间最新的一份。列中展示 `participantId`、北京时间完成时间、NE/NR/SE/SR 到 `taskId` 的映射、四项排名，以及首选条件、首选任务和理由。
- 两个统计工作表只排除 Pilot；设备验证、彩排、演示等 `ExperimentKind=Formal` 记录仍会进入统计。完成时间按 `dd/mm/yyyy hh:mm:ss` 显示，记录从早到晚排序。
- 已跳过且没有回答的问卷仍保留在 `Questionnaires` 中；不会伪造未填写的回答。
- 对话引用缺少明细时，导出继续完成，并在 JSON 的 `warnings` 中记录缺失项。

电脑端先写入临时目录，只有 JSON 和 Excel 都成功后才发布最终目录。相同 `exportId` 和相同内容的重复请求会返回已有导出结果。

## 常见提示

- “未检测到电脑导出服务”：检查数据线、USB 调试授权和统一后台是否正在运行。
- “暂无可导出的实验历史数据”：PICO 当前数据库没有实验历史记录。
- “电脑无法写入导出目录”：检查磁盘空间、目录权限或通过 `--export-dir` 更换目录。
