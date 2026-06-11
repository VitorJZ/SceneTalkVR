# 给 Agent B 的修改指令：关闭 Holodeck GUI 弹窗并优化响应

@Agent B，你好！我是负责 Unity 客户端的 Agent A。

目前我们在进行前后端联调时遇到了两个问题，需要你在 FastAPI 侧进行调整：

## 问题 1：AI-THOR 弹出图形界面
我们在通过 Unity 调用你的 `/generate_scene` 接口时，WSL 环境中弹出了 AI-THOR 的 Unity 渲染窗口。
**目标：** 后端服务应该完全是 Headless 的（仅作为数据计算服务），不需要显示任何 3D 窗口。

**建议的解决方案：**
在调用 `ai2thor` 的 `Controller` 初始化，或通过 `Holodeck` 类生成场景时，请尝试传入或设置与 Headless 相关的参数。
可以参考 AI-THOR 的官方文档，尝试在环境初始化前设置：
```python
import os
os.environ["AI2THOR_VISIBILITY_DISTANCE"] = "0"
# 或者设置平台为无头模式 (视 ai2thor 版本而定)
```
或者如果在你的 `Holodeck` 封装层里有参数可以控制 `render=False`, `headless=True`, `start_unity=False` 等，请务必启用它们。如果必须启动图形界面，请指导用户在 WSL 下使用 `Xvfb` (X virtual framebuffer) 拦截图形输出：
```bash
# 提示用户运行后端时使用：
xvfb-run -a python -m uvicorn app:app --host 0.0.0.0 --port 8080
```

## 问题 2：首次请求严重超时
从记录看，首次调用大模型（加载 CLIP 等）以及生成布局花了极长的时间，导致 Unity 客户端抛出 120 秒超时错误。
虽然我已经把 Unity 的超时放宽到了 300 秒，但为了更好的体验，请你：
1. **预加载模型 (Warm-up)**：在 FastAPI 的 `@app.on_event("startup")` 钩子中，预先实例化并加载那些笨重的模型（如 CLIP、SentenceTransformer 等），避免它们在第一次用户请求时才阻塞加载。
2. **简化生成参数**：检查 Holodeck 的生成参数，是否有可以降低采样步数、减少候选资产数量的方法，以加快生成速度。

请你修改 `app.py`（以及相关封装逻辑）来解决上述问题，并更新运行文档！
