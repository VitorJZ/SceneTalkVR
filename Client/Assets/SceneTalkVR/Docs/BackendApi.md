# SceneTalkVR 后端 API 接口文档 (v1.0)

本引擎通过 FastAPI 桥接 Holodeck Python 环境，为 Unity 客户端提供场景生成服务。

## 基础信息
*   **Base URL**: `http://localhost:8080`
*   **协议**: HTTP/JSON

## 接口列表

### 1. 健康检查
*   **路径**: `GET /api/health` (注：Agent B 的实现可能为 `/docs` 或根路径，建议根据实际 app.py 确认)
*   **说明**: 检查后端服务是否启动。

### 2. 场景布局生成
*   **路径**: `POST /generate_scene`
*   **说明**: 根据自然语言描述生成 3D 布局数据。
*   **请求体**:
```json
{
  "environment": "a cozy coffee shop"
}
```
*   **成功响应**:
```json
{
  "environment": "coffee_shop",
  "objects": [
    {
      "name": "DiningTable",
      "position": [1.2, 0.0, 0.5],
      "rotation": 90.0
    }
  ]
}
```

## 注意事项
1.  **延迟**: 首次请求涉及模型加载，可能需要 30s-60s。
2.  **数据过滤**: 后端会自动过滤 3米 半径以外的物体，以保证移动端性能。
3.  **坐标系**: `position` 数组顺序为 `[x, y, z]`。
