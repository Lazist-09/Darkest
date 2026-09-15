# 地牢【瓷砖网格】规则草案 v0.1（主程序 → 架构定契约 / 策划定规则）

> 依据：用户 2026-09-15 裁定 —— 远征采用 **(B) 瓷砖网格自由走**（上下左右一格一格走）；
> **每格都可能遇敌，但规则后续安排（暂留）**；并要求"充分参考 DD 的做法"。
> 🔴 合规声明：**不复制 DD 的代码/资源**（DD1 闭源；且我方判据 A1 禁止 DD 安装目录的任何文件）⇒
>    本草案是**照其结构与方法、从第一性原理重写**，并落成**我们自己的数据契约** ✓

## 1. 目标形态（与用户描述一致）
```
选择界面 → 城池 → 远征 = 【与战斗同一场景/同一套 UI 语汇】，但主画面是**瓷砖网格**：
  · 队伍在网格上按格移动（上下左右；一格 = 一步 = 1 段 = 1 回合 ⇒ 与 `#327`「1 段 = 1 格」一致 ✓）
  · 每格可有内容/触发（地板 / 墙 / 门 / 房间 / 走廊 / Curio / 战斗 / 事件 / 营地 / 楼梯/终点）
  · 未探索区域是黑的（视野 + 光照）；光照值仍按 `tuning.light` 那套（可见性 + 战斗效果）
  · 踩到触发格 ⇒ **不切场景**地切模式（Curio 面板 / 战斗屏 / 事件）
```

## 2. 数据模型（建议键名；**待架构登记后生效**）
### 2.1 `dungeon_grid.json`（新文件；与现 `expedition_map.json` **并存**，见 §5 迁移）
| 键 | 语义 | 初值/占位 |
|---|---|---|
| `width` / `height` | 网格尺寸（格） | 待策划（<placeholder>） |
| `tiles` | **瓷砖串**（每行一串字符 ⇒ 与地图编辑器友好） | 例：`"#..#"`、`"."` 地板、`"#"` 墙、`"+"` 门、`"R"` 房间、`"C"` 走廊、`"?"` Curio、`"!"` 战斗、`"E"` 事件、`"A"` 营地、`"G"` 终点 |
| `start` / `goal` | 起点/终点坐标 `{x,y}` | 必填 |
| `room_anchors` | 房间锚点：`[{ "id": "r1", "x": .., "y": .., "type": "battle|event|camp|treasure", "content_ref": "..." }]` | 复用现有房间内容表引用 ✓ |
| `encounter` | **每格遭遇规则（暂留）** | 🔴 **本次只留字段与占位**：`{ "per_tile_chance": null, "cooldown_tiles": null, "note": "规则后续安排" }` ⇒ 加载期**允许 null**，UI/内核**不得**自行编一个默认值 ⚠️ |
| `vision` | 视野：`{ "radius": .., "reveal_on_enter": true }` | 待策划（占位） |
| `move` | 移动：`{ "cost_light_per_tile": .. }` | 🔴 **与现值等价**：`move.new_area = −30`（语义"每段"）⇒ 因 **1 段 = 1 格** ⇒ 平移为"每格 −30"，**总消耗不变** ✓（架构 `#327`⑤ 的 N=1 情形）✓ |

### 2.2 内核模型（建议类名，`scripts/gameplay/sim/run/DungeonGrid.cs`）
```
`DungeonTileKind { Floor, Wall, Door, Room, Corridor, Curio, Battle, Event, Camp, Goal }`（**枚举 ⇔ 瓷砖字符**的映射表，
   与 `TierFromId` 同法 ⇒ "枚举到数字/字符的映射"属结构性常量 ✓ 可进白名单）
`DungeonGrid`（不可变）：`Width` / `Height` / `TileAt(x,y)` / `InBounds` / `RoomAt(x,y)` / `Goal`
`DungeonWalker`（可变状态，单一真值）：`Position` / `RevealedTiles` / `StepsTaken`
   · `TryStep(int dx, int dy)` ⇒ 4 向；墙/越界 ⇒ false（**不扣任何东西** ✓）
   · `PathTo(goal)` ⇒ BFS/A*（表现层"点远处 ⇒ 自动走"用；内核保证确定性）
   · `LandingTrigger(Position)` ⇒ 落格触发（Curio/战斗/事件/营地/终点）
```

## 3. 与现有系统的接缝（**零返工**的要点）
| 现有 | 如何接 |
|---|---|
| `FlowPhase`（Walking/Camp/Battle/Resolved）+ 三谓词 | **不动** ✓ 踩到战斗格 ⇒ `Battle`；扎营 ⇒ `Camp`；回到走格 ⇒ `Walking` ✓ |
| `ExpeditionFlow` 的房间内容/Curio/扎营/收益/跨场 buff | **不动** ✓ 只是"进入某格"触发它（`EnterRoom(roomId)` 语义保留）✓ |
| 光照 `LightMeter` / 档位 / 战斗效果 | **不动** ✓ 每格 −30 与现值等价（§2.1 `move`）✓ |
| 血结转 `O-83` / 跨场 buff / 阵型槽位映射 | **不动** ✓（与"走格"无关）✓ |
| 战斗（同场景、`StartExpeditionBattleInScene`） | **不动** ✓ 战斗格 ⇒ 起战斗；打完 ⇒ 回网格模式 ✓ |

## 4. 引擎侧（Godot 内置工具，用户要求"充分利用"）
```
· 表现层用 `TileMapLayer`（Godot 4.3+ 的 TileMap 后继）画网格；`AStarGrid2D` 做寻路（比自写 BFS 更省、且与引擎一致）✓
· 视野/雾：`TileMapLayer` 的 `set_cell` + 遮罩层（或 `Light2D` + `CanvasModulate` 做黑暗）✓
· 相机：`Camera2D` 跟随队伍（回到战斗模式时恢复战斗构图）✓
· ⚠️ 这些属**表现层** ⇒ 由 UI 设计师落地；我只提供 §2.2 的只读状态与 §3 的相位门禁 ✓
```

## 5. 迁移路径（**建议：并存 → 派生 → 替换**，避免一次性推翻）
```
阶段 1（契约）：本草案 ⇒ 架构定 `data_schema` 条目（含 `encounter` 暂留的**允许 null** 条款）✓
阶段 2（数据+模型）：落 `dungeon_grid.json` + `DungeonGrid/Walker` + 加载校验 + 用例（**不接 UI**）✓
阶段 3（派生桥）：现有 `ExpeditionMap`（房间+连线）**派生**出网格（房间→格块、连线→走廊格）
   ⇒ 现有内容表/Curio/战斗全部**照旧可用**，先用"派生的网格"把行走跑起来 ✓（旧图页退役）
阶段 4（真地图）：策划手写 `tiles`（DD 式关卡设计）⇒ 替换派生 ⇒ 每格遭遇规则此时才启（**暂留项**）✓
```

## 6. 必须由策划/架构裁的空位（我**不自行填**）
```
① `width/height` 与瓷砖图（内容）② 每格遭遇规则（**用户已说暂留** ⇒ 先留字段与 null）✓
③ 视野半径与是否"进格揭示"④ 每格光照消耗是否就用现值（N=1 平移）⑤ 是否需要"格级障碍/陷阱"
⚠️ 涉及数字的一律先 `placeholder: true`（守 `#307`）；机制类改动报策划 ✓
```
