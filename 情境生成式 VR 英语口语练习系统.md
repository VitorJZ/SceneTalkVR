## 情境生成式 VR 英语口语练习系统

### 研究背景

尽管当前 VR 语言学习系统已具备一定的 AI 对话能力（如调整语气、语速、人格等），但仍受限于固定的虚拟环境与 Avatar 形象，无法根据学习任务动态调整，导致情境多样性与沉浸感不足。 

本项目旨在构建 **SceneTalk VR**，一个基于自然语言指令动态生成情境化 **虚拟场景** 与 **Avatar** 的 VR 英语**口语练习**系统，为用户提供更灵活、且贴合真实任务的语言交互体验。

![img](https://sjtu.feishu.cn/space/api/box/stream/download/asynccode/?code=ZjdmODQ5MzgxODZkYWViMjlkZjRjZWE4NmNkY2E0OWFfN0xFRHpIbjFDNmdsNWZmTGIyR2d0ZUVoUjZWcmJUV2ZfVG9rZW46UjdIemJiWHJZbzVpOVh4aEpDMWNjU3RGbk9nXzE3Nzk5NDgzNDM6MTc3OTk1MTk0M19WNA)

### 技术路线

1. 搭建基础 VR 交互框架，接入LLM解析用户的自然语言情境指令（如“我想练习和语速快的外国咖啡店员点单”）
   1.  解析结果包括：
   2. 任务类型（如点单、问路、面试）
   3. 环境类型（咖啡店、机场大厅、会议室）
   4. Avatar 角色特征（外观、语速、态度、口音等）
2. 根据解析后的指令，生成对应的虚拟场景资产
3. 生成当前场景身份的 Avatar 外观（如服务员、海关人员）
4. 由 LLM 生成口语对话，确保 Avatar 的语速、口音、态度等符合人设。

### 预期成果

实现一套完整的 VR 英语口语练习 Demo。通过自然语言设定目标，系统自动生成对应环境与合适外观的 Avatar，该Avatar以合适的语速、口音、态度跟用户进行沟通。

### 注意事项

- 如果需要用到unity6，可以从这个链接下载：https://www.nounitycn.top/（要保证梯子的纯净）
- Avatar的生成可以先考虑使用预设库
- 虚拟场景可以考虑
  - 生成一张360场景图片（工程量更小，如果效果好可以使用）
  - 调用场景模块库，并依据解析结果自动组合空间布局。
- 项目后续会投CCF A论文

### 参考资料

- 场景生成：https://github.com/allenai/Holodeck
- Pico4配置1：https://developer.picoxr.com/document/unity/
- Pico4配置2：https://blog.learnxr.io/xr-development/pico-4-with-pico-unity-integration-sdk
- 文本转语音（仅供参考，路线可以自己选择）：
  - Unity TTS：https://www.youtube.com/watch?v=qn0FiPj6Iug
  - ElevenLabs：https://www.davideaversa.it/blog/elevenlabs-text-to-speech-unity-script/
- LLM（仅供参考，路线可以自己选择）：https://towardsdatascience.com/how-to-use-llms-in-unity-308c9c0f637c/