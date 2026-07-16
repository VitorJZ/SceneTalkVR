应广大师生的需求，网络信息中心推出本地大模型API服务，用户可基于模型API将 DeepSeek、MiniMax、Qwen 等大模型集成到自己的项目或应用中。

## 当前提供的大模型

| 模型名称 | 模型参数 | 调用名 | 模式 | 侧重领域 | 上下文长度 |
| --- | --- | --- | --- | --- | --- |
| DeepSeek V4 Flash（常规模式） | 284B | `deepseek-chat` | 非思考模式 | 通用文本处理 | 256k |
| DeepSeek V4 Flash（思考模式） | 284B | `deepseek-reasoner` | 深度思考模式 | 复杂逻辑深度推理 | 256k |
| MiniMax-M2.7 | 230B | `minimax` 或 `minimax-m2.7` | 文本生成 | 智能体任务 | 192k |
| Qwen3.6-27B | 27B | `qwen` 或 `qwen3.6-27b` | 多模态处理 | 视觉与文本理解 | 256k |

**API 调用约定**：同时支持 **OpenAI 兼容格式** 与 **Anthropic 格式** 两种调用协议，您可根据项目技术栈灵活选择。

| 特性 | OpenAI 兼容格式 | Anthropic 格式 |
| --- | --- | --- |
| 请求端点 | `/api/v1/chat/completions` | `/api/v1/messages` |

若您使用 OpenAI 兼容格式，直接参考「3. 模型调用示例」即可，无需改动；若需使用 Anthropic 格式，仅需调整请求端点，消息体结构保持一致；两种方式功能等价，任选其一即可正常调用全部模型。

## 1\. 登录交我办申请 API-KEY

（1）登录交我办网页端 [https://my.sjtu.edu.cn/](https://my.sjtu.edu.cn/)，或移动端 APP  
（2）搜索【“致远一号”AI模型API申请（测试）】 → 点击流程申请大模型API  
![ ](https://claw.sjtu.edu.cn/assets/image.jpg)

（3）填写申请表 → 提交申请

-   **联系邮箱**：填写用于接收开通信息的邮箱
-   **默认模型**：所有支持模型
-   **默认 token 有效期**：截止到 2026-09-30（测试结束）
-   **默认 token 额度**：每分钟请求数：10，每分钟token消耗：100000，每周token总量：1000000000
-   **用途说明**：请大致填写使用目的及具体应用场景

（4）申请通过后会在系统填写的邮箱及交我办消息中收到 `base_url` 和 `api-key`

## 2\. 使用前必读

-   请妥善保管大模型 `api-key`，避免泄露。一旦泄露，可能导致账号内资源被他人消耗；
-   禁止售卖、恶意泄漏 `api-key` 计算资源，一经发现将取消申请及延期资格，并追究相应责任；
-   目前平台 API 服务仅限校园网访问（校外通过 VPN 访问）；
-   如涉及校级大规模公开应用，需大量 Token 支持，请通过 [hpc@sjtu.edu.cn](mailto:hpc@sjtu.edu.cn) 联系我们，详细说明应用场景与用量需求，我们将为您配置相应额度。

## 3\. 模型调用示例

-   **模型地址 `base_url`**：`https://models.sjtu.edu.cn/api/v1`
-   **模型密钥 `api-key`**：`your_api_key`（申请到的密钥）

### （1）查看当前可调用模型名称

```
curl -v 'https://models.sjtu.edu.cn/api/v1/models' -H 'Authorization: Bearer your-api-key'
```

**返回示例**：

```
{"data": [
  {"id": "minimax", "object": "model", "created": 1677610602, "owned_by": "openai"},
  {"id": "qwen", "object": "model", "created": 1677610602, "owned_by": "openai"},
  {"id": "deepseek-chat", "object": "model", "created": 1677610602, "owned_by": "openai"},
  {"id": "deepseek-reasoner", "object": "model", "created": 1677610602, "owned_by": "openai"},
  {"id": "minimax-m2.7", "object": "model", "created": 1677610602, "owned_by": "openai"},
  {"id": "qwen3.6-27b", "object": "model", "created": 1677610602, "owned_by": "openai"}
], "object": "list"}
```

> **重要提醒**：

-   调用模型时，请使用 API 返回的 `id` 字段（如 `deepseek-chat`）作为调用参数。请勿直接使用您在申请表格里填写的模型名称。
-   部分模型对应多个调用名，均可正常使用。

___

### （2）常规文本对话（以 deepseek-chat 为例，Windows用户建议使用WSL，macOS、Linux用户终端运行即可）

```
curl --location --request POST 'https://models.sjtu.edu.cn/api/v1/chat/completions' \
--header 'Accept: application/json' \
--header 'Content-Type: application/json' \
--header 'Authorization: Bearer your-api-key' \
--data '{
  "model": "deepseek-chat",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "请介绍上海交通大学"
    }
  ],
  "stream": false,
  "max_tokens": 1024
}'
```

**返回示例**：

```
{
    "id": "chatcmpl-18d0f79d-e479-4134-ab19-ccaca69840ed",
    "created": 1774332696,
    "model": "deepseek",
    "object": "chat.completion",
    "system_fingerprint": null,
    "choices": [
        {
            "finish_reason": "stop",
            "index": 0,
            "message": {
                "content": "上海交通大学（Shanghai Jiao Tong University，简称“上海交大”或“SJTU”）是中国一所历史悠久、声誉卓著的顶尖高等学府，属于国家“双一流”建设高校（A类）和“985工程”“211工程”重点建设大学。以下是对其的详细介绍：\n\n### 一、历史沿革\n- **创立于1896年**：由晚清实业家盛宣怀创办，初名“南洋公学”，是中国近代最早的现代大学之一。\n- **发展历程**：历经多次更名与调整，1959年正式定名为“上海交通大学”，并与西安交通大学同根同源（1956年部分院系西迁支援建设西安交通大学）。\n- **文化传承**：以“饮水思源，爱国荣校”为校训，融合中西学术传统，培养了大批杰出人才。\n\n### 二、学术实力与排名\n- **国际声誉**：在QS、THE、ARWU等世界大学排名中稳居中国内地高校前列，工程、医学、商科等领域尤其突出。\n- **学科优势**：\n  - **工程与技术**：机械、船舶与海洋工程、电子信息、材料科学等学科全球领先。\n  - **医学**：合并原上海第二医科大学后，临床医学、药学实力雄厚，拥有多家顶级附属医院。\n  - **商科与管理**：安泰经济与管理学院、上海高级金融学院（SAIF）在国内外享有盛誉。\n- **科研平台**：拥有多个国家级重点实验室、工程研究中心，牵头多项国家重大科研项目。\n\n### 三、校区与设施\n- **主校区**：\n  - **徐汇校区**：历史最悠久的校区，充满文化底蕴。\n  - **闵行校区**：现代化主校区，占地广阔，设施先进。\n  - **其他校区**：黄浦校区（医学院）、长宁校区（经管学院）等。\n- **资源丰富**：图书馆藏书数百万册，校园网络、实验室、体育场馆等设施完善。\n\n### 四、人才培养\n- **多元化教育**：提供本科、硕士、博士全层次教育，推行通识教育与专业教育结合。\n- **国际化特色**：与全球200多所高校合作，设有联合学位、交换项目，留学生比例高。\n- **创新创业**：积极推动学生创新实践，孵化众多科技企业，被誉为“创业者的摇篮”。\n\n### 五、社会贡献与影响力\n- **科研成果转化**：在人工智能、生物医药、航空航天等领域贡献显著，助力国家科技自立自强。\n- **校友网络**：涌现出钱学森、吴文俊、江泽民等杰出校友，涵盖政界、学界、商界精英。\n- **社会服务**：通过智库咨询、医疗援助、文化传播等方式服务社会。\n\n### 六、校园文化\n- **学生活动**：拥有数百个学生社团，涵盖科技、文艺、体育等领域。\n- **品牌活动**：如“交大讲坛”“校园艺术节”等，丰富学生生活。\n- **体育传统**：注重体育教育，在国内外赛事中屡获佳绩。\n\n### 结语\n上海交通大学以其深厚的学术积淀、卓越的科研实力和全球化的视野，持续推动教育创新与科技进步，是中国乃至世界高等教育的重要力量。它不仅是一所传授知识的学府，更是培养未来领袖、推动社会发展的引擎。",
                "role": "assistant",
                "tool_calls": null,
                "function_call": null
            },
            "provider_specific_fields": {
                "stop_reason": null,
                "token_ids": null
            }
        }
    ],
    "usage": {
        "completion_tokens": 706,
        "prompt_tokens": 14,
        "total_tokens": 720,
        "completion_tokens_details": null,
        "prompt_tokens_details": null
    },
    "service_tier": null,
    "prompt_logprobs": null,
    "prompt_token_ids": null,
    "kv_transfer_params": null
}
```

___

___

### （3）深度推理对话（以 deepseek-reasoner 为例）

```
curl --location --request POST 'https://models.sjtu.edu.cn/api/v1/chat/completions' \
--header 'Accept: application/json' \
--header 'Content-Type: application/json' \
--header 'Authorization: Bearer your-api-key' \
--data '{
  "model": "deepseek-reasoner",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "请介绍上海"
    }
  ],
  "stream": false,
  "max_tokens": 1024
}'
```

**返回示例**：

```
{
    "id": "chatcmpl-69739704-64fd-4f04-85ef-4bd46a8642b8",
    "created": 1774404218,
    "model": "deepseek",
    "object": "chat.completion",
    "system_fingerprint": null,
    "choices": [
        {
            "finish_reason": "stop",
            "index": 0,
            "message": {
                "content": "上海是中国的一座国际化大都市，位于中国东部沿海，长江入海口，是国家的经济、金融、贸易和航运中心。以下是对上海的简要介绍：\n\n### 1. **城市概况**\n   - **行政地位**：上海是中国四个直辖市之一，也是国家中心城市。\n   - **人口**：常住人口超过2400万，是中国人口最多的城市之一。\n   - **面积**：总面积约6340平方公里，包括16个行政区。\n\n### 2. **经济与金融**\n   - **经济中心**：上海是中国最大的经济中心，GDP长期位居全国城市首位。\n   - **金融中心**：陆家嘴金融贸易区是中国最重要的金融区之一，聚集了众多国内外银行、证券和保险机构。\n   - **国际贸易**：上海港是全球最繁忙的集装箱港口之一，浦东新区和自贸区进一步推动了国际贸易和投资。\n\n### 3. **文化与历史**\n   - **历史底蕴**：上海历史悠久，拥有丰富的历史遗产，如外滩的近代建筑群、豫园、城隍庙等。\n   - **文化融合**：作为中西文化交汇的城市，上海融合了传统与现代元素，既有石库门弄堂，也有现代化的摩天大楼。\n   - **艺术与娱乐**：上海拥有众多博物馆、美术馆、剧院和音乐厅，如上海博物馆、上海大剧院等。每年的上海国际电影节和艺术展览也吸引全球关注。\n\n### 4. **交通与基础设施**\n   - **交通枢纽**：上海拥有两个国际机场（浦东国际机场和虹桥国际机场），以及发达的高铁和地铁网络。\n   - **公共交通**：上海地铁是全球最长的城市轨道交通系统之一，覆盖全市主要区域。\n\n### 5. **旅游与景点**\n   - **外滩**：黄浦江畔的标志性景观，以近代建筑群和夜景闻名。\n   - **东方明珠塔**：上海的地标性建筑，位于陆家嘴，可俯瞰城市全景。\n   - **迪士尼乐园**：中国内地首座迪士尼主题公园，位于浦东新区。\n   - **新天地**：融合了石库门建筑与现代商业的休闲区，适合购物和餐饮。\n\n### 6. **教育与科技**\n   - **高等教育**：上海拥有多所知名高校，如复旦大学、上海交通大学、同济大学等。\n   - **科技创新**：张江高科技园区是中国重要的科技创新中心，聚焦生物医药、信息技术等领域。\n\n### 7. **生活方式**\n   - **美食**：上海菜以本帮菜为主，口味偏甜，如小笼包、红烧肉等。此外，上海也汇聚了全球各地的美食。\n   - **购物**：南京路步行街、淮海路等是著名的购物区，既有传统老字号，也有国际奢侈品牌。\n\n### 总结\n上海是一座充满活力的城市，既保留了深厚的历史文化底蕴，又展现了现代化的国际风貌。无论是经济、文化还是生活，上海都在中国乃至全球扮演着重要角色。",
                "role": "assistant",
                "tool_calls": null,
                "function_call": null,
                "reasoning_content": "嗯，用户想了解上海的基本情况。这是一个比较宽泛的请求，需要提供全面但不过于冗长的介绍。可以从城市概况、经济、文化、交通、旅游等几个核心维度来组织信息。\n\n上海作为国际化大都市，首先应该突出其经济金融中心的地位。陆家嘴和金融贸易区是标志性区域，可以提及。文化方面，石库门和豫园能体现传统与现代的结合。交通枢纽的地位很重要，浦东机场和地铁网络都是亮点。\n\n旅游部分可以列出最具代表性的景点，外滩、迪士尼这些国内外知名的地点应该包括。最后用一句话总结上海的特点，让用户感受到这座城市的活力与魅力。不需要具体数据，保持整体描述的流畅性即可。"
            },
            "provider_specific_fields": {
                "stop_reason": null,
                "token_ids": null
            }
        }
    ],
    "usage": {
        "completion_tokens": 771,
        "prompt_tokens": 13,
        "total_tokens": 784,
        "completion_tokens_details": null,
        "prompt_tokens_details": null
    },
    "service_tier": null,
    "prompt_logprobs": null,
    "prompt_token_ids": null,
    "kv_transfer_params": null
}
```

___

### （4）图像模型调用示例

```
# 先下载图片并转为 base64
# base64_logo=$(curl -s "https://studio.hpc.sjtu.edu.cn/dex/theme/logo.png" | base64 -w 0)

curl --location --request POST 'https://models.sjtu.edu.cn/api/v1/chat/completions' \
--header 'Authorization: Bearer your-api-key' \
--header 'Content-Type: application/json' \
--data '{
  "model": "qwen",
  "messages": [{
    "role": "user",
    "content": [
      {
        "type": "image_url",
        "image_url": {
          "url": "data:image/png;base64,'"$base64_logo"'"
        }
      },
      {"type": "text", "text": "Explain the details in the image."}
    ]
  }],
  "stream": false,
  "max_tokens": 1024
}'
```

**返回示例**：

```
{
  "id": "chatcmpl-8744cb4e-666d-4e2e-9312-7ab2b6699caa",
  "created": 1777535336,
  "model": "qwen3.6-27B",
  "object": "chat.completion",
  "system_fingerprint": null,
  "choices": [
    {
      "finish_reason": "stop",
      "index": 0,
      "message": {
        "content": "The image displays a stylized logo for what appears to be a brand or studio named **“HPC Studio”**, with additional graphical and typographic elements that add character and meaning.\n\nHere’s a detailed breakdown:\n\n---\n\n### 1. **Leftmost Element: Red Stylized “π” (Pi) Symbol**\n- The symbol is the Greek letter **pi (π)**, rendered in a bold, brush-stroke style.\n- It’s colored **red** and has an artistic, hand-painted look — suggesting creativity, passion, or perhaps a connection to mathematics/science (since π is fundamental in math and physics).\n- Below the main curve of the π, there’s a small red dot or accent mark — possibly representing a decimal point, a period, or just a stylistic flourish.\n- This could imply the studio’s focus on precision, calculation, or scientific computing — especially since “HPC” often stands for **High-Performance Computing**.\n\n---\n\n### 2. **Center Text: “HPC Studio”**\n- Written in a **bold, black, sans-serif font**.\n- The letters are tightly spaced and have a modern, clean, professional appearance.\n- “HPC” likely stands for **High-Performance Computing**, indicating the studio may specialize in computationally intensive work — such as scientific simulation, data analysis, AI/ML, or engineering modeling.\n- “Studio” suggests a creative or collaborative workspace — perhaps where developers, researchers, or designers work on HPC-related projects.\n\n---\n\n### 3. **Rightmost Element: Red Circular Icon with White Triangle**\n- To the right of “Studio,” there’s a **red circle** containing a **white triangle** pointing to the right.\n- This resembles a **play button** or **forward arrow**, commonly associated with media playback, progress, or action.\n- In the context of HPC, it might symbolize:\n  - Running simulations or computations (“start the job”)\n  - Progress or forward momentum\n  - A call to action (“launch your project”)\n- The color red matches the pi symbol, creating visual balance and branding consistency.\n\n---\n\n### Overall Interpretation:\n\nThis logo combines **scientific symbolism (π)**, **technical identity (HPC)**, and **creative energy (Studio + play button)** to represent a brand focused on **high-performance computing solutions delivered with creativity and dynamism**.\n\nIt could belong to:\n- A research lab or consultancy specializing in HPC\n- A software development studio building tools for scientists or engineers\n- An educational platform teaching HPC concepts\n- A startup offering cloud-based HPC services\n\nThe design is modern, energetic, and memorable — effectively communicating both technical rigor and innovative spirit.\n\n---\n\n✅ **Summary**:  \nThe logo reads “π HPC Studio” with a red pi symbol on the left, bold black text in the center, and a red play-button-style icon on the right — suggesting a dynamic, science-driven studio focused on high-performance computing.",
        "role": "assistant",
        "tool_calls": null,
        "function_call": null
      },
      "provider_specific_fields": {
        "stop_reason": null,
        "token_ids": null
      }
    }
  ],
  "usage": {
    "completion_tokens": 626,
    "prompt_tokens": 92,
    "total_tokens": 718,
    "completion_tokens_details": null,
    "prompt_tokens_details": null
  },
  "service_tier": null,
  "prompt_logprobs": null,
  "prompt_token_ids": null,
  "kv_transfer_params": null
}
```

___

### Python 调用示例

```
# 模型仅限校内网络调用，数据不出校.
# Powered by hpc@sjtu.edu.cn, 2026.03.24

import time
import requests

def query(messages):
    url = "https://models.sjtu.edu.cn/api/v1/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": "Bearer your-api-key",  # 关键：加 Bearer
    }

    data = {
        "model": "deepseek-chat",
        "messages": [{"role": "user", "content": str(messages)}],
        "stream": False,
        "temperature": 0.7,
        "max_tokens": 1024,
    }

    for attempt in range(1, 6):  # 最多重试 5 次，避免无限循环
        try:
            resp = requests.post(url, headers=headers, json=data, timeout=(30, 600))
        except requests.RequestException as e:
            print(f"请求异常(第{attempt}次): {e}")
            time.sleep(2)
            continue

        if resp.status_code == 200:
            return resp.json()["choices"][0]["message"]["content"].strip()

        # 非 200：打印出来你就知道是不是 401/403/429 等
        print(f"HTTP {resp.status_code} (第{attempt}次): {resp.text[:500]}")
        if 400 <= resp.status_code < 500 and resp.status_code != 429:
            break  # 4xx(非429)通常重试没意义
        time.sleep(2)

    raise RuntimeError("请求失败，见上方 HTTP 输出")

if __name__ == "__main__":
    question = "请介绍下你自己？"
    print(query(question))
```

**运行结果**：

```
[Running] python test.py
--- 开始提问 ---
问题: 请介绍下你自己？
message: [{'role': 'user', 'content': '请介绍下你自己？'}]

--- 模型答案 ---
你好！我是DeepSeek，由深度求索公司创造的AI助手，很高兴认识你！😊

让我简单介绍一下自己：

**基本信息：**
- 我是一个纯文本AI模型，擅长理解和生成自然语言
- 知识截止到2024年7月，会尽我所能为你提供准确、有用的信息

**主要能力：**
- 📝 文本处理：写作、翻译、总结、分析等
- 💡 问题解答：各领域知识问答、学习辅导
- 🔍 信息处理：文档解读（支持上传图像、PDF、Word、Excel等文件）
- 🧮 逻辑推理：数学计算、编程帮助、逻辑分析
- 🌐 联网搜索：需要时可以获取最新信息（需手动开启）

**特色功能：**
- 完全免费使用，没有收费计划
- 可通过官方应用商店下载App使用
- 回复风格热情细腻，注重用户体验

**我的特点：**
我会用温暖、细腻的方式与你交流，认真对待每一个问题。虽然我是纯文本模型，但通过文件上传功能，我可以“读取”各种格式文档中的文字信息来帮助你。

有什么我可以帮你的吗？无论是学习、工作还是生活中的问题，我都很乐意协助你！✨
```