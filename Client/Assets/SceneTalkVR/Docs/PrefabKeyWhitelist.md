# PrefabKey 白名单 (v1.0)

为了保证 Unity 客户端在“混合渲染”模式下的稳定性和性能，后端 Holodeck 生成的任意复杂对象名称（如 `communal_table_with_live_edge`）必须在**后端 Adapter 处**被映射并降级为以下白名单中的 `prefabKey`。

Unity 客户端将只识别以下 Key 来加载对应的低模预制体（Prefab）。

## ☕ Coffee Shop 场景
| PrefabKey | 中文说明 | 英文说明 | 默认 Fallback 占位 |
| :--- | :--- | :--- | :--- |
| `coffee_counter` | 咖啡店点单吧台 | Coffee shop ordering counter | `generic_table` |
| `cafe_table` | 咖啡馆小桌 | Small cafe table | `generic_table` |
| `chair` | 普通椅子 | Generic chair | `generic_chair` |
| `sofa` | 沙发 | Sofa / Couch | `generic_chair` |
| `plant` | 盆栽植物 | Potted plant | `generic_decor` |
| `wall_shelf` | 墙上置物架 | Wall-mounted shelf | `generic_decor` |
| `menu_board` | 菜单黑板 | Menu board | `generic_decor` |
| `cash_register` | 收银机 | Cash register | `generic_decor` |
| `coffee_mug` | 咖啡杯 | Coffee mug | `generic_decor` |
| `lamp` | 台灯/落地灯 | Lamp | `generic_decor` |

## ✈️ Airport 场景
| PrefabKey | 中文说明 | 英文说明 | 默认 Fallback 占位 |
| :--- | :--- | :--- | :--- |
| `airport_counter` | 机场值机柜台 | Airport check-in counter | `generic_table` |
| `airport_sign` | 机场指示牌 | Airport directional sign | `generic_decor` |
| `queue_barrier` | 排队隔离带 | Queue stanchion / barrier | `generic_decor` |
| `suitcase` | 行李箱 | Suitcase / Luggage | `generic_decor` |
| `security_gate` | 安检门 | Security scanner gate | `generic_decor` |

## 🏢 Office 场景
| PrefabKey | 中文说明 | 英文说明 | 默认 Fallback 占位 |
| :--- | :--- | :--- | :--- |
| `office_desk` | 办公桌 | Office desk | `generic_table` |
| `office_chair` | 办公椅 | Office chair | `generic_chair` |
| `whiteboard` | 白板 | Whiteboard | `generic_decor` |
| `bookshelf` | 书架 | Bookshelf | `generic_decor` |
| `cabinet` | 储物柜 | Storage cabinet | `generic_decor` |

## 🍽️ Restaurant 场景
| PrefabKey | 中文说明 | 英文说明 | 默认 Fallback 占位 |
| :--- | :--- | :--- | :--- |
| `restaurant_table` | 餐厅餐桌 | Restaurant dining table | `generic_table` |
| `plate` | 餐盘 | Dining plate | `generic_decor` |
| `cup` | 水杯 | Drinking cup / Glass | `generic_decor` |

## ⚠️ 通用保底 (Generic Fallbacks)
当后端遇到无法归类的物体时，必须强制映射为以下之一：
| PrefabKey | 中文说明 | 英文说明 | 表现形式 |
| :--- | :--- | :--- | :--- |
| `generic_table` | 通用桌子/台面 | Generic flat surface / table | 简易方桌模型 |
| `generic_chair` | 通用座椅 | Generic seating | 简易椅子模型 |
| `generic_decor` | 通用装饰/杂物 | Generic decorative or small item | 小型白色占位方块 |
