# SceneTalkVR 中英文界面切换工作汇总

日期：2026-08-10

提交前基线：`2edeabe`（`main`）

## 1. 工作范围

本次改动为设置系统增加中英文界面切换，并将现有参与者可见的系统界面、按钮、状态和错误提示纳入统一语言管理。默认语言保持中文，用户可在设置页即时切换为英文并切换回来。

本次只调整系统界面语言，不改变语音识别、LLM、语音合成、任务判定、实验流程和数据导出协议。参与者输入、角色原始回复、历史原文及导出原始数据不会被翻译或改写。

## 2. 设置状态与持久化

- 新增 `SceneTalkLanguage`，当前支持 `Chinese` 和 `English`。
- 语言保存在现有 `SceneTalkUserSettings` 与 `PlayerPrefs` 设置记录中，不另建独立流程状态。
- 继续使用现有 `SceneTalkState.Settings` 管理设置页；语言是设置页内部状态，切换后仍停留在设置页。
- 新增 `SetLanguage` 和 `ToggleLanguage`，通过现有 `Changed` 事件通知界面与协调器刷新。
- 默认值为中文；旧设置缺少语言字段或保存了非法枚举值时均安全回退为中文。
- 设置页新增“语言”行。中文界面按钮显示 `English`，英文界面按钮显示 `Chinese`。

## 3. 本地化结构

- 将原 `SceneTalkChineseUiText` 扩展并重命名为通用的 `SceneTalkUiText`。
- 新文件沿用原 `.meta` GUID `4e33a08b214f463ab0744e495550ea8a`，避免 Unity 资源引用因重命名失效。
- 集中管理固定界面文案，并为任务名称、任务情境、任务目标、实验状态、问卷状态、传输状态、纠错状态、历史记录值和错误码提供语义化解析方法。
- 英文模式下，未知且仅有中文的系统值使用安全占位符，不回退显示中文。
- 正式问卷直接使用问卷目录已有的 `promptChinese` / `promptEnglish` 与中英文章节名称。
- 运行时创建的按钮、标题、提示、输入框占位符统一通过语言解析器生成，避免各面板自行维护零散翻译。

## 4. 界面覆盖

已覆盖以下参与者与操作界面：

- 首页、设置、退出与实验退出确认。
- 预实验、正式实验、任务选择、目标面板和对话状态。
- 录音、识别、思考、纠错播放、角色播放、失败与重试提示。
- USB / LAN 传输状态和 PICO 历史数据导出状态。
- 正式问卷、预实验问卷、跳过与二次确认提示。
- 正式最终排序、预实验最终排序及实验完成界面。
- 对话历史、实验历史、详情、删除确认和错误界面。
- 编辑器演示、彩排和设备验证相关运行时提示。

语言切换时会重建运行时 Canvas，并清理问卷、预实验和正式排序面板保存的旧 Canvas 引用。这样可以保证全部静态节点立即使用新语言，同时避免重复面板、重复按钮监听和残留旧语言节点。

## 5. 自动化测试

新增或更新的测试覆盖：

- 旧设置缺少语言字段时默认中文。
- 非法语言值归一化为中文。
- 语言切换事件、持久化和重新加载。
- 全部现有任务、目标、状态和传输状态均可生成无中文残留的英文文案。
- 设置页双向切换后仍保持 `Settings` 状态。
- Canvas 重建后主界面、设置面板和退出按钮均只有一个实例。
- 正式问卷使用英文题目，正式排序无中文系统文案。
- 预实验界面、问卷与既有任务目标显示保持回归兼容。

验证结果：

| 验证项目 | 结果 |
| --- | --- |
| `LanguageSystemTests` EditMode | 6 / 6 通过 |
| Pilot 与问卷相关 EditMode 回归 | 38 / 38 通过 |
| 设置、Pilot、正式问卷与排序 PlayMode | 18 / 18 通过 |
| `Assembly-CSharp.csproj` | 0 错误 |
| `Assembly-CSharp-Editor.csproj` | 0 错误 |
| `SceneTalkVR.Stage2.PlayModeTests.csproj` | 0 错误、0 警告 |
| Unity Console | 0 error |
| 旧 `SceneTalkChineseUiText` 代码引用 | 0 处 |
| `git diff --check` | 通过 |

主程序集命令行编译仍报告 Unity MCP 编辑器程序集引入的 `System.Net.Http` 与 `System.IO.Compression` 版本选择警告；这是现有外部依赖警告，本次没有新增编译错误。

## 6. 一并保留的项目配置改动

工作区中原有的以下未提交配置改动一并纳入本次提交：

- `ExperimentBuildInfo.asset` 的构建基线由 `bef0763` 更新为 `2edeabe`，并刷新编辑器构建时间；协议版本保持 `1.5.0-immediate-goal-advance`。
- `ProjectSettings.asset` 的 `preloadedAssets` 移除了三个 XR 设置资源条目：`XRGeneralSettingsPerBuildTarget`、`OpenXRPackageSettings` 和 `PXR_Settings`；`InputSystem_Actions` 仍保留为预加载资源。

上述配置差异在语言系统实施前已存在，审查期间未回退或改写。

## 7. 审查结论

- 未发现旧本地化类残留引用、Unity GUID 变化、缺失 `.meta` 文件或新增敏感凭据。
- 本次没有数据库迁移，不修改实验历史、问卷记录或导出 schema。
- 未改变正式实验与预实验的状态迁移、任务目标判定或传输路由策略。
- 后续新增参与者可见的固定文案时，应同步加入 `SceneTalkUiText` 或使用明确的中英文语义解析接口。
