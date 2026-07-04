# SceneTalkVR 统一场景 JSON 协议 (v1.0)

本协议定义了 Unity 客户端与 Python 后端（LLM 中枢 + Holodeck 适配器）之间的数据交互格式。

## 1. 坐标与缩放约定
*   **单位**：后端输出的所有坐标和尺寸单位必须统一为**米 (meters)**。
*   **坐标系**：
    *   Unity 采用左手坐标系。
    *   `x` / `z` 轴代表地面平面（Floor plan）。
    *   `y` 轴代表高度（Elevation）。地面放置的物体，其底部基准通常对应 `y = 0`。
*   **旋转**：`rotationY` 表示绕着 Unity 垂直 Y 轴旋转的**角度 (Degrees)**，顺时针为正。

## 2. 场景模式 (scene.mode)
*   `layout`：仅使用本地 prefab 构建 3D 空间。
*   `skybox`：仅渲染 360 全景背景，无近景交互物体。
*   `hybrid`：【推荐模式】混合渲染，远景使用 360 Skybox，近景加载本地交互 prefab。

## 3. 请求结构 (Request)
Unity 客户端发送给后端的 POST 请求。

```json
{
  "userText": "I want to practice ordering coffee in a coffee shop.",
  "language": "en",
  "targetScene": "coffee_shop"
}
```

## 4. 响应结构 (Response)
后端（或集成了 Holodeck 数据的代理层）返回给 Unity 的完整 Payload。

```json
{
  "taskType": "ordering_coffee",
  "environmentType": "coffee_shop",
  "dialogueReply": "Welcome to the coffee shop. What would you like to order?",
  "avatarRole": {
      "role": "barista",
      "speakingSpeed": "normal",
      "accent": "american",
      "attitude": "friendly",
      "appearance": {
        "styleId": "semi_realistic_v1",
        "genderPresentation": "female",
        "ageBucket": "young_adult",
        "bodyBuild": "average",
        "outfitRole": "barista",
        "outfitColor": "green"
      }
  },
  "scene": {
    "mode": "hybrid",
    "skyboxUrl": "https://s3.siliconflow.cn/.../image.png",
    "skyboxKey": "", 
    "layoutObjects": [
      {
        "prefabKey": "coffee_counter",
        "position": { "x": 0.0, "y": 0.0, "z": 3.0 },
        "rotationY": 0.0
      },
      {
        "prefabKey": "cafe_table",
        "position": { "x": 1.5, "y": 0.0, "z": 1.2 },
        "rotationY": 45.0
      }
    ]
  }
}
```

### 字段说明
*   `skyboxUrl`：外部生成的全景图下载地址（如 SiliconFlow 提供）。
*   `skyboxKey`：本地预置的天空盒资源名。若存在此值，Unity 优先读取本地资源以节省时间。
*   `avatarRole.appearance.genderPresentation`：Avatar 性别表现字段。核心取值为 `male` / `female` / `unknown`；Unity Avatar resolver 会用它选择同职业的男/女 prefab，TTS 也会用最终解析到的 Avatar 性别选择 `default_male_en` 或 `default_female_en`。
*   `layoutObjects`：包含 3米 内近距离可交互物体的列表。
    *   `prefabKey`：**必须严格遵守**预定义的白名单。未知物体必须被后端映射为 `generic_decor` 等通用占位符。
