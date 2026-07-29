# PICO 正式采集与预实验任务重设计汇总

日期：2026-07-29

## 工作范围

本次未提交改动基于提交 `7de4eab`，汇总了以下连续工作：

1. 重设计三个预实验餐厅任务及目标识别规则。
2. 将正式实验中的高楼层目标文案改为 “Request a room on a higher floor.”。
3. 将普通 PICO Android 运行路径从设备验证演练改为正式参与者采集。
4. 移除 PICO 设备验证顶部提示及右侧状态面板。
5. 补充任务识别、部署配置、正式/预实验流程和界面回归测试。

本次没有修改场景文件或 `ProjectSettings`，也没有删除已有资源。

## 预实验餐厅任务

任务 ID、场景、角色、语音、呈现条件和平衡分配规则保持不变。任务目录版本由 `1.2.1-pilot-collection` 升级为 `1.3.0-pilot-task-redesign`。

### 无预约到店

任务 ID：`pilot_restaurant_walk_in`

开场语：

> Good evening! Welcome to Riverside Restaurant. Do you have a reservation?

| 目标 ID | 英文目标 | 中文显示 |
| --- | --- | --- |
| `no_reservation` | Explain that you do not have a reservation. | 说明自己没有预约。 |
| `party_size` | State the number of diners. | 确定用餐人数。 |
| `window_table_availability` | Ask whether a window table is available. | 询问是否有靠窗的空桌。 |
| `menu_request` | Ask for a menu. | 要一份菜单。 |

### 餐厅点餐

任务 ID：`pilot_restaurant_ordering`

开场语：

> Here is the menu. Are you ready to order, or would you like a recommendation?

| 目标 ID | 英文目标 | 中文显示 |
| --- | --- | --- |
| `recommendation` | Ask for a recommended dish. | 询问推荐菜品。 |
| `main_course` | Order one main course. | 点一份主菜。 |
| `dish_price` | Ask about the price of a dish. | 询问菜品价格。 |
| `drink` | Order a drink. | 点一份饮品。 |

### 餐品错误处理

任务 ID：`pilot_restaurant_wrong_dish`

开场语：

> Excuse me, is everything all right with your meal?

| 目标 ID | 英文目标 | 中文显示 |
| --- | --- | --- |
| `wrong_dish` | Explain that the received dish is incorrect. | 说明收到的餐品不正确。 |
| `dietary_restriction` | State a dietary restriction or allergen. | 声明自己的忌口或过敏原。 |
| `extra_charge` | Ask whether there will be an extra charge. | 询问是否有额外收费。 |
| `replacement_preparation_time` | Ask how long the replacement dish will take to prepare. | 询问重新制作餐品所需时间。 |

## 目标识别与兼容策略

- 目标评估器审计版本升级为 `goal_evaluator_v1.3.0`，结构化 LLM 输出版本改为直接引用该常量，避免代码与提示词版本漂移。
- 靠窗空桌要求同时出现靠窗语义、桌位语义以及询问或请求语气。
- 菜单目标要求明确索取或查看菜单，仅提到菜单不会直接完成。
- 菜品价格要求明确询价；无上下文的 “How much is it?” 交给携带近期用户回合的结构化 LLM 判断。
- 忌口目标要求具体限制或过敏原，“没有过敏”或否定表达不会完成目标。
- 额外收费要求询问附加费用，普通费用陈述不会完成目标。
- 制作时间要求询问新餐品或替换餐品的准备、烹饪或就绪时间。
- 增加否定、历史事件、陈述、引用和假设表达的误判保护。
- 目录版本变化会使旧正式实验和预实验的未完成分配返回 `task_catalog_version_changed`；已完成历史记录不迁移、不重写。
- 旧目标 ID 只保留历史中文显示，不映射到新目标，也不会被解释为新目标进度。

## 正式实验目标文案

正式任务 `hotel_check_in` 的 `higher_floor` 目标由：

> Ask whether a high-floor room can be arranged.

改为：

> Request a room on a higher floor.

中文显示同步改为“请求高楼层房间。”，相关 EditMode 和 PlayMode 期望值已更新。

## PICO 正式采集模式

普通非编辑器 Android/PICO 运行现在默认使用正式采集资格，不再自动进入设备验证演练。

新增运行模式：

- `PicoCollectionFormal`
- `PicoCollectionPilot`

运行上下文固定为：

| 字段 | 正式 PICO 采集值 |
| --- | --- |
| `qualification` | `Collection` |
| `dataOrigin` | `participant_collection` |
| `collectionEligible` | `true` |
| `deploymentTarget` | `Pico` |
| `deploymentProfile` | `pico_lab` |

正式实验与预实验协调器现在都允许 PICO 正式采集，并在创建和恢复分配时校验运行模式、部署配置、目录版本和采集资格。启动失败或结束会话时会恢复条件管理器状态，避免正式配置泄漏到后续会话。

正式与预实验 Bundle 导出不再硬编码编辑器模式，而是写入分配中的实际 `runtimeMode` 和 `deploymentProfile`；操作事件和回合记录也使用同一运行上下文。

## PICO 部署配置

正式部署目录版本升级为 `1.3-pico-collection`，新增 `PicoLab` 配置：

- Voice LAN 备用地址：`http://192.168.137.1:8787`
- LLM LAN 备用地址：`http://192.168.137.1:8788/api/llm/chat/completions`
- 传输策略：`UsbPreferred`
- 正式采集许可：`approvedForCollection=true`、`collectionAllowed=true`
- 设备目标：`Pico`，并要求 PICO 真机
- 配置证据：`project-lead-pico-collection-directive-2026-07-29`

USB 首选链路继续使用现有 ADB reverse loopback 端点；上述局域网地址只作为自动备用。部署校验现在同时检查采集许可、PICO 目标和设备要求，避免把未批准或错误平台的配置用于正式数据。

## 正式模式与设备验证模式的区别

| 项目 | 正式采集 | 设备验证 |
| --- | --- | --- |
| 资格 | `Collection` | `Rehearsal` |
| 数据来源 | `participant_collection` | `rehearsal` |
| 可作为正式数据 | 是 | 否 |
| 运行模式 | `PicoCollectionFormal` / `PicoCollectionPilot` | 显式验证入口 |
| 部署配置 | `pico_lab` | `pico_device_validation` |
| 用途 | 正式协议、任务、问卷、排序和 Bundle 审计 | 设备、网络、STT/LLM/TTS 链路和彩排检查 |

设备验证仍保留为显式测试入口，但不会由普通 PICO 构建自动触发。两种模式都不允许静默回退到 mock 服务。

## 界面行为

- PICO 设备验证顶部提示不再显示。
- 右侧设备验证状态面板不再显示。
- 正式 PICO 会话未正确创建时阻止无会话启动，避免意外回退为演练数据。
- 正式和预实验入口继续使用现有布局和中文界面。

## 测试与审查覆盖

新增或更新的测试覆盖：

- 三个预实验任务的新开场语、目标顺序、目标 ID、中文显示和目录版本。
- 新目标的自然表达、同义表达、否定表达和模糊表达。
- 带近期用户回合的菜品价格结构化 LLM 判定。
- 旧正式/预实验未完成分配的目录版本锁。
- PICO 正式部署的采集许可、USB 优先策略和资源校验。
- 正式实验与预实验的 PICO 采集上下文、分配模式及部署配置。
- 设备验证仍保持非采集资格，并隐藏顶部及右侧验证界面。
- Bundle 和操作事件写入正确运行模式及部署配置。

审查结果：

- 未发现意外删除、场景修改或 `ProjectSettings` 修改。
- `git diff --check` 通过。
- `Assembly-CSharp.csproj` 编译通过，0 个错误。
- `Assembly-CSharp-Editor.csproj` 编译通过，0 个错误。
- `SceneTalkVR.Stage2.PlayModeTests.csproj` 编译通过，0 个错误。
- 编译中的 `System.Net.Http`、`System.IO.Compression` 版本冲突及弃用提示为现有警告，本次没有新增代码错误。
- 当前会话未暴露 Unity MCP 编辑器资源，因此没有启动第二个 Unity 实例运行 Test Runner；新增 EditMode/PlayMode 测试已完成程序集编译。

## 部署注意事项

- 源码改动需要重新构建并安装 APK 后才会在 PICO 真机生效。
- 正式采集前应确认电脑端 Voice/LLM 网关健康，并建立 `8787`、`8788` 的 ADB reverse 映射。
- 电脑仍需访问腾讯云和 LLM 上游；USB 只替代 PICO 到电脑之间的无线链路。
- 建议新 APK 上分别完成一次正式实验、一次预实验以及 Bundle 导出审计，再开始参与者采集。
- 本次提交只写入本地 Git，不推送远端。
