# Holodeck 资产导入报告 (Phase 4)

## 1. 导入概述
按照阶段四目标，我已为 Unity 客户端准备了基于白名单的“第一版 Demo Prefabs”。

## 2. 资产列表 (Whitelist Prefabs)
目前在 `Assets/SceneTalkVR/Prefabs/` 下自动生成了 22 个低模预制体，涵盖了以下核心场景：
*   **Coffee Shop**: `PF_coffee_counter`, `PF_cafe_table`, `PF_sofa`, `PF_coffee_mug` 等。
*   **Airport**: `PF_airport_counter`, `PF_suitcase`, `PF_security_gate` 等.
*   **Office**: `PF_office_desk`, `PF_whiteboard`, `PF_bookshelf` 等.
*   **Restaurant**: `PF_restaurant_table`, `PF_plate`, `PF_cup` 等.

## 3. 映射逻辑
*   **后端返回**: `DiningTable`
*   **客户端处理**: `MapToPrefabKey` 会将其识别为 `cafe_table`
*   **实例化**: 加载 `PF_cafe_table.prefab`

## 4. 后续建议 (Objathor 原生资产)
若需替换为更精细的 Objathor 资产，请将 FBX 文件放入 `Assets/SceneTalkVR/Models/`，并重新指向对应的 `PF_xxx` 预制体。
推荐优先替换：`PF_coffee_counter` 和 `PF_cafe_table`。
