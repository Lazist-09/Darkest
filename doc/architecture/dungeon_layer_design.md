# 地牢层（Dungeon Layer）架构设计与功能拆解

> **方法论**：以 **DD 官方 wiki**（`darkestdungeon.wiki.gg`，见 `dd_reference.md` / `dungeon_view.md §1` 摘录的 8 条原文）
> 为**需求基线**，以 **F 盘代码实际实现**为**现状**，逐条核对 ⇒ 得出**没做到的清单** ⇒ 再做架构设计与功能拆解。
> ⚠️ 本文档**不**以项目自身的设计文档为基线（按你的要求）——项目文档仅用于**定位代码**。
>
> 🔴 **核验纪律**：本轮每条"缺失"都经过**多形态交叉 grep 反证**（初次 grep 曾把"光照掉落"误判为缺失，
> 实为 `ExpeditionFlow.cs:439` 已消费 `Light.Loot`）⇒ 下文每条都带**代码证据**，❌ 表示"多形态搜索均无"。
>
> 红线合规（**A1 / 红线 27**）：不引用 DD 安装目录任何文件、不复制其资源；只对齐**机制构造**，重写实现 ✓

---

## 0. 一句话结论

> **项目的地牢层做到了"能走"（网格拓扑 + 走格 + 光照守恒 + 派生地图），但没做到"探索的压力"。**
>
> DD 地牢层的灵魂是 8 条里的 **⑦⑧**（**回头要付代价 · 重访会刷新威胁**）——它让"已清空的走廊"仍有紧张感、
> 让"路线的选择"成为资源决策。**这两条代码里一条都没有，且 ⑦ 的实现方向还和 DD 相反。**
> 其余缺口集中在：**三态揭示只做了二态 · 陷阱/障碍/饥饿/隐藏房四类格内容缺失 · 探索层的折磨 act-out 缺失**。
> 🔄 **2026-09-20 更新**：三态揭示（`D-3`）· 陷阱（`D-4`）· 饥饿（`D-5`）· 隐藏房（`D-6`）· **探索层 act-out（`D-7`）** 皆已落地；仅"走廊障碍分层"（F3b）与"手写瓷砖图"（F7）未动。

---

## 1. wiki 基线：DD 地牢层到底由哪些机制构成

来源：`dungeon_view.md §1` 摘录的 wiki **Dungeon Map** 原文 + `dd_reference.md`（wiki 40+ 页研究）。

| wiki # | DD 机制（原文要点） | 设计意图（为什么它重要） |
|---|---|---|
| ① | 地图在**右下角**；房间=大灰方块，走廊=深灰小格 | 空间感来自"地图"，不是"列表" |
| ② | **走廊 = 1~8 格**连接两房间 | 走廊长度**可变** ⇒ 路线长度有差异 |
| ③ | 🔴 **`traversing each tile counts as a single round`** | **走一格 = 一回合** ⇒ 状态效果/光照**按格计** |
| ④ | 🔴 走廊格可含：**战斗**・**Curio**・**障碍**・**陷阱**・**隐藏房**・**饥饿**(隐藏)，**先以图标显示在地图上** | 走廊**不是空的路**，它是"内容容器" |
| ⑤ | 🔴 **走到那格才触发** | **先看见、走到才发生** ⇒ 无缝 + 预判 |
| ⑥ | **三态**：未探索=暗 ／ 已侦察=暗但轮廓亮 ／ 已走过=浅灰 | 给"侦察"一个**视觉回报** |
| ⑦ | 🔴 **往回走 ⇒ 士气损失** | 🔴 **回头的代价** —— 防止"清空地图再回头捡" |
| ⑧ | 🔴 **重进已走过的走廊 ⇒ 小概率生成新战斗/饥饿/陷阱；光越暗概率越高**（≤50 ⇒ **+2.5%**；=0 ⇒ **+5%**） | 🔴 **已清区域不永久安全** ⇒ 探图不是"一次性投资" |
| ⑨ | **光照计**：越暗 ⇒ 怪 ACC+12.5 / DMG+25~30% / 暴击+5~6% / 压力×1.4~1.5，**但战利品额外掉率最高 95%** | 🔴 **玩家自选难度换收益**（DD 最精妙的一层） |
| ⑩ | **折磨 act-out 大多发生在地牢探索**（拒绝摸奇物 / 拒绝进食） | 折磨的**丰富度来自战斗外** |
| ⑪ | 撤退成功每英雄 **+25** 压力；放弃远征 **+20**（不受修正） | 逃跑**有代价** |

---

## 2. 代码现状逐条核对（带证据）

| wiki # | 项目实现 | 证据 | 状态 |
|---|---|---|---|
| ① | `BattleUI.Dungeon.cs`(491) · `WalkMapView.cs`(402) · `ui_spec §11.1` | 地图在右下 | ✅ |
| ② | `DungeonTileKind`(11 种) + `DungeonGridDeriver`(281) 从房间图派生 | `DungeonGrid.cs:9` | ✅(**派生**) |
| ③ | `DungeonWalker.StepsTaken`；光照**按段分摊到格**（`WalkLightCost` 余数结转） | `DungeonGrid.cs:199` `WalkLightCost.cs:44` | ✅ 且**守恒**✓ |
| ④ 战斗 | `DungeonTileKind.Battle`、`KindForRoomType:"battle"→Battle` | `DungeonGridDeriver.cs:51` | ✅ |
| ④ 奇物 | `CurioResolver.cs`(78) + `curios.json` | ✅ | ✅ |
| ④ 陷阱 | 🔴 枚举位**有**（`Trap/'^'`），但注释明写"**可通行且无任何效果**" | `DungeonGrid.cs:22` | ⚠️**占位** |
| ④ 障碍 | `ObstacleRuntime.cs` 在 **`sim/board/`**（**战斗层**），非走廊 | — | ⚠️**层不对** |
| ④ 饥饿 | ❌ 无 per-step 口粮消耗；`Inventory.TryConsumeFoodToFreeSlot`(183) 只是**包满腾格** | `Inventory.cs:148` | ❌ |
| ④ 隐藏房 | ✅ `DungeonTileKind.Secret`（第 12 位）+ `DungeonTileMap` `'*'` | `DungeonGrid.cs` | ✅ |
| ⑤ | `DungeonWalker.CurrentTile` / `LandingTrigger` 语义 | `DungeonGrid.cs:207` | ✅ |
| ⑥ | 🔴 只有 **`_revealed` HashSet（二态）** + UI `TileVisited=1`；**无 `scouted` 中间态** | `DungeonGrid.cs:180` `WalkMapSkeleton.cs:36` | ⚠️**二态** |
| ⑦ | ❌ 网格层**无回头惩罚**。房间图层有 `RevisitCost`，但口径是 **"回头更便宜"**（`\|revisit\| < \|new\|`） | `ExpeditionMapConfig.cs:28,153` | ❌ **且方向相反** |
| ⑧ | ❌ 无重访生成、无按暗度刷新的战斗/饥饿/陷阱（多形态搜索无 `Revisit/Respawn`） | — | ❌ |
| ⑨ | 🟡 **机制在**：越暗 ⇒ 掉落**柴火/口粮份数**更多（black: firewood2+food2）；`ExpeditionFlow.cs:439` 已消费 `Light.Loot` | `tuning.json light.loot` | 🟡 **载荷不是战利品** |
| ⑩ | ✅ **已落地**（`D-7`，2026-09-20）：探索层 act-out = **拒绝用道具（被迫空手）/ 拒绝进食（强制挨饿）**；判据 = **士气 < `morale.start`**（折磨的解除线）| `ExplorationActOut.cs` | ✅ |
| ⑪ | 🟡 事件已立：`RetreatResolved` / `ExpeditionAbandoned` | `BattleEventTypes.cs:68,75` | 🟡 **加压待核** |

**未清偿/暂留（契约已立，值待定）**：

| 项 | 状态 | 证据 |
|---|---|---|
| `dungeon_grid.json` | 🔴 **文件不存在**（还在"派生桥"阶段，未到手写瓷砖图） | `data/` 下无此文件；`DungeonGridConfig.ResPath` 已声明 |
| `encounter`（每格遭遇） | 契约**强制为 null**（非 null 加载期拒绝） | `DungeonGridConfig.cs:163` |
| `vision` 字段 | 契约有（`reveal_on_enter/radius/scout_bonus`），**内核未消费** | `DungeonGridConfig.cs:38` + `#380` |
| `move.cost_light_per_tile` | 强制 null（用 `WalkLightCost` 守恒替代） | `DungeonGridConfig.cs:171` |

---

## 3. Gap 清单（差的东西，按优先级）

| ID | 缺口 | wiki # | 优先级 | 说明 |
|---|---|---|---|---|
| **G1** | 🔴 **回头/重访的代价（光照）+ 方向纠偏** | ⑦ | **P0** | 当前"回头更便宜"与 DD 相反；需在**网格层**立代价，而非只在房间图层 |
| **G2** | 🔴 **重访已走过格 ⇒ 按暗度刷新威胁**（战斗/饥饿/陷阱） | ⑧ | **P0** | DD 的"已清区域不安全"；让探图不是一次性投资 |
| **G3** | ~~🔴 **三类缺失的格内容**：饥饿 / 隐藏房 / 陷阱(转真)~~ ✅ **已完成**（`D-5` / `D-6` / `D-4`，2026-09-20）+ 障碍**分层** | ④ | **P1** | 走廊从"空的路"变成"内容容器"——**三件皆已落地**，仅"障碍分层"（F3b）未动 |
| **G4** | ⚠️ **三态揭示**（补 `scouted` 中间态） | ⑥ | **P1** | 给侦察一个视觉回报；契约已有字段未接线 |
| **G5** | ✅ **已完成**（`D-7`，2026-09-20）：折磨 ⇒ **拒绝用道具（被迫空手）/ 拒绝进食（强制挨饿）** | ⑩ | **P1** | 让折磨在战斗外也有存在感 —— **已落地**（`ExplorationActOut` + 两处门禁）|
| **G6** | ⚠️ 光照收益**从"补给"扩到"战利品"**（DD 是掉率 95%） | ⑨ | **P2** | 现有光照收益只作用于"补给份数"；缺 DD 的额外掉率 |
| **G7** | ⚠️ **手写瓷砖图**（`dungeon_grid.json` 落地，脱离派生） | ② | **P2** | 派生图只能跑通，做不出 DD 的关卡设计感 |
| **G8** | 🟡 撤退/放弃的**加压数值**落地 | ⑪ | **P2** | 事件已立，规则待核 |

---

## 4. 架构设计

### 4.1 分层（**内核零 Godot**，与既有 `sim/run` 一致）

```
┌─ L0 静态拓扑（不可变）────────────────────────────────────
│  DungeonGrid           瓷砖/Our inception图 Null; TileAt/InBounds/Goal/Passage        ✅ 已有
│  DungeonGridConfig     JSON 契约 + P30 七条加载期校验                              ✅ 已有
├─ L1 运行时状态（单一真值）────────────────────────────────
│  DungeonWalker         Position/StepsTaken/**RevealState(三态)**/VisitedCount(重访计数)  ⚠️ 需扩
├─ L2 规则服务（**纯函数**，围绕"走一格"的得失）──────────────
│  WalkLightCost/DungeonWalkLight   光照守恒分摊+余数结转                    ✅ 已有
│  MapScouting                      拓扑层侦察(base_pct+光照加成,1~3步)        ✅ 已有
│  ✅ GridVision                    格层视野：进格揭示四向相邻 radius        D-3 已接线（2026-09-20）
│  🆕 TileLanding                   落格分发：格内容 ⇒ 触发({战斗/奇物/陷阱/饥饿/事件/营地/终点})
│  🆕 BacktrackCost                 **回头代价**（士气/压力）← G1
│  🆕 RevisitSpawner                **重访按暗度刷新威胁** ← G2
│  🆕 HungerDrift                   **走格口粮消耗/Starving** ← G3
├─ L3 内容域（触发后的实际业务）─────────────────────────────
│  Battle / Curio(CurioResolver) / Event / Camp / Goal / Trap / Obstacle / Hunger
├─ L4 流程编排 ─────────────────────────────────────────────
│  FlowPhase(Walking/Camp/Battle/Resolved) ✅ + ExpeditionFlow + BattleRoot(宿主)
└─ L5 表现层（**只读 + 上报意图**）─────────────────────────
   WalkMapView / WalkMapSkeleton / BattleUI.Dungeon / CurioPanel / ScoutMarkPanel
```

### 4.2 数据流（**一步一闭环**，保持既有"先算后扣"的纪律）

```
玩家输入(方向键/点远处)
  └→ 宿主(BattleRoot) → ExpeditionFlow  【意图, 非 UI 自决】
       └→ 内核一格 Step()：
            ┌─ ① 合法性：InBounds && IsWalkable        （否 ⇒ 什么都不发生，不扣不计数 ✅ 已有）
            ├─ ② 位移 + StepsTaken++                    （✅ 已有）
            ├─ ③ 光照扣减：WalkLightCost.PlanPath 逐格  （✅ 已有，守恒）
            ├─ ④ 🆕 回头判定：是否进入【已走过的格】⇒ BacktrackCost
            ├─ ⑤ 🆕 重访判定：本次第 N 次进入 ⇒ RevisitSpawner（按当前光照档查概率）
            ├─ ⑥ 🆕 饥饿：HungerDrift（按格/段累计，临界 ⇒ Starving 事件）
            ├─ ⑦ 🆕 揭示：GridVision 更新三态(unexplored→scouted→visited)
            ├─ ⑧ 内容分发：TileLanding ⇒ [触发内容] ⇒ 切 FlowPhase
            └─ ⑨ 写 CombatLog（**每条随机必写 RngDraw**，保确定性）
       └→ 只读状态快照 → WalkMapView 渲染（三态雾 + 图标）
```

🔴 **口径铁律（承接既有纪律）**：
- **④~⑦ 全是"推进时才结算"**，不得在 UI 端预判或缓存；
- 任何**新数值**一律 `placeholder: true` + 契约登记，**禁止加载器填默认值**（红线 21 / P29 ①）；
- 所有随机**必须写 `RngDraw`**（`MapScouting` 已是范式）。

---

## 5. 功能拆解（每个模块可独立落地）

### 🔴 F1 · `BacktrackCost`（回头代价）—— **G1，P0，且需先纠偏**

| 项 | 内容 |
|---|---|
| **职责** | 判定"这一格是否为**回头**"，是则施加**士气/压力代价** |
| **DD 原文** | ⑦ 往回走 ⇒ 士气损失 |
| **当前冲突** | `ExpeditionMapConfig.RevisitCost` 的校验是 `\|revisit\| < \|new\|`（**回头更便宜**）⇒ 与 DD **相反** ⚠️ |
| **建议裁法** | ① **两层分开**（承接 `#380` 的教训）：<br>　- **房间图层**（旧）：保留"段级重走"的现有口径，**不动**（避免返工）<br>　- **网格层**（新）：引入"**进入已 visited 的格**"⇒ 士气代价<br>② 数值 `placeholder: true`（**不写死**），登记进 `tuning.dungeon_layer.backtrack`<br>③ **给玩家可见反馈**：回头那一刻弹一次"**回头的代价**"读数（红字） |
| **输入** | `(grid, position, nextPos, visitedSet)` |
| **输出** | `(bool IsBacktrack, int MoraleDelta)` |
| **判据** | T1 首次进入某格 ⇒ 无代价；T2 二次及以上进入已访格 ⇒ 有代价；T3 代价出现在 `CombatLog` |

> 📌 **为什么它是 P0**：没有它，"清完走廊再回头捡东西"是**零成本**的 ⇒ DD 的路线紧张感直接消失。

### 🔴 F2 · `RevisitSpawner`（重访刷新威胁）—— **G2，P0**

| 项 | 内容 |
|---|---|
| **职责** | 每次进入**已走过的格**：按**当前光照档**查概率 ⇒ 掷骰 ⇒ 可能生成 战斗/饥饿/陷阱 |
| **DD 原值** | ≤50 ⇒ **+2.5%**；=0（全黑）⇒ **+5%** |
| **落地建议** | ① 概率表进 `tuning.dungeon_layer.revisit`（**每档一个 `# per tier`**，先 `placeholder` 对齐 DD 的 2.5/5 方向）<br>② 刷新种类 = 战斗 / 饥饿 / 陷阱（**先做战斗 + 陷阱**，饥饿依赖 F4）<br>③ 掷骰 **必须写 `RngDraw`**<br>④ 刷新后该格**重置为"有新威胁"** ⇒ UI 图标变化（但**不重建节点**，守 S1） |
| **输入** | `(tileKind, visitedCount, lightTier, rng, log)` |
| **输出** | `(bool Spawned, DungeonTileKind NewContent)` |
| **判据** | T1 重访同一格 N 次 ⇒ 出现至少一次刷新（统计可验）；T2 全黑档概率 > 有光档；T3 每次掷骰都有 `RngDraw` |

### ⚠️ F3 · 补齐三类格内容 —— **G3，P1**

| 子项 | 内容 | 建议 |
|---|---|---|
| **F3a 陷阱 Trap** | ✅ **转真**（`D-4`，2026-09-20） | ~~枚举已有(`'^'`)，规则暂留~~ ⇒ **已落地**：`TrapDefs`（`data/trap_defs.json`）+ `TrapResolver`（唯一判定面）+ 三态门禁 `TrapGate{Hidden,Disarmable,Consumed} ← RevealState` + `DungeonGridDeriver.ScatterTraps`（**补上 `^` 的产出源** —— 此前全仓无人生成陷阱格）+ `ExpeditionSession.ResolveTrapByResist`。<br>**DD 口径**：踏中 ⇒ 随机一人掉 % 最大 HP + 压力**恒定 +15**；未侦察按 **Trap Resist**（**不是 SPD/DODGE**）掷闪避但**照样触发**；已侦察 ⇒ 可拆除（`Trap Resist + 40%`，**允许 > 100%**），拆成功 ⇒ **无害 + 回 8 压力**。<br>⚠️ **本刀修正了本行原描述**：wiki 原文的规避依据是 **Trap Resist**（每职业抗性），**不是 `SPD` 或"侦察加成"** —— 原描述系推测，已按实据改正 ✓ |
| **F3b 障碍 Obstacle** | 现有 `ObstacleRuntime` 在 `sim/board`（战斗层） | **走廊障碍**应新立：挡路 ⇒ 需 道具/铲子/时间 才过，或绕路。与战斗层**同名不同物**，勿复用 ⚠️ |
| **F3c 饥饿 Hunger** | ✅ **已完成（2026-09-20，D-5）** | 🔴 **实测 wiki 原文后修正口径**：DD 的饥饿**不是"累计漂移"**（那是我的初版推测），而是**走廊格上的隐藏事件**（与 Fight/Obstacle/Curio 同类，**地图永不显示、侦察也不显示**）。详见下方 §F3c-规格 |

#### 🔴 §F3c-规格 · 饥饿（DD wiki "Hunger" 原文实据）

| 项 | DD 口径 | 本项目落地 |
|---|---|---|
| **触发位置** | 走廊格（隐藏事件） | `ExpeditionFlow.TryStepTile` 的「**新格 + 走廊格**」分支<br>🔴 **≠ D-2**：D-2 挂「重走」、D-5 挂「前行」 |
| **概率（按光照档）** | `Radiant/Dim 7.5%` · `Shadowy/Dark 10%` · `Black as Pitch 12.5%`<br>（**越黑越频繁**，与 D-2 同向） | `tuning.dungeon_layer.hunger.tiers`：`≤0→12.5` / `≤50→10` / `≤75→7.5` / `≤100→7.5`<br>（本项目 `light.tiers` 是 0..100 五档 ⇒ 刻度换算依据写进 `note`） |
| **吃** | 每人 1 口粮 ⇒ 全队各回 **5% 自己的最大 HP** | `food_per_hero: 1` / `eat_heal_percent: 5.0`（**逐人算**，向上取整） |
| **不吃 / 凑不齐** | 全队掉 **20% 最大 HP** + **20 压力** | `starve_hp_percent: 20.0` / `starve_morale: 20` |
| 🔴 **不能只喂一部分人** | "the entire party is forced to starve, and **no Food will be eaten**, regardless of any Food you may have below the threshold" | `CanEatForHunger` = `Food ≥ 存活人数 × 1`；不足 ⇒ **选"吃"也必然挨饿且一口粮都不扣** |
| **缓冲（Buffer）** | 开局/扎营后 **2 条走廊**；每次**遇到检查**后 **1 条**；🔴 **只在前行时递减**（"moving backwards … does not reduce the hunger buffer"） | `buffer_at_start: 2` / `buffer_after_trigger: 1`；`_hungerBuffer` 在**新格+走廊格**分支递减，**回头不递减**<br>🔴 未触发也重置缓冲（DD："after **encountering a hunger check**" = 遇检查即给，非"真饿了才给"） |

**代码落点**：`HungerSpawner`（纯函数，与 `RevisitSpawner` **刻意同构**）· `HungerConfig`/`HungerTier`（`TuningConfig.ExpeditionAndCombat.cs`）· 加载期校验（`TuningConfig.Validate`，🔴 **末档必须覆盖满光照** —— 复制 D-2 那个"亮着走永远不触发"的静默失效防护）· `ExpeditionFlow`（缓冲状态 + 触发 + 读数）· `ExpeditionSession.ResolveHunger`（吃/不吃结算）· `BattleUI.TryOpenHunger`（吃/不吃面板，复用 `CurioPanel`）。

**⚠️ 已知的有界缺口（诚实标注）**：`Retained` 只在**战后**落账 ⇒ **首场战斗之前**掷中的饥饿**无人可结算**。此时 `HasPendingHunger` 返回 `false`（**不弹面板**，弹了就是骗玩家）、`ResolveHunger` 抛错、并留痕 `hunger_no_roster_pending`（不静默）。暴露窗口 = "开局 2 条缓冲走廊之后、首战之前"。🔴 **为什么不补**：唯一真值 `RosterMaxHp` 由 `BeginBattle` 从**投影后的** `UnitRuntime.MaxHp` 记录；首战前要拿它就得自己跑一遍投影 ⇒ 那是 `#325` D6 明令禁止的「两份真值」⚠️ ⇒ 宁可留一个有界且有痕的缺口，也不制造第二份血量公式。

**测试**：`tests/HungerTests.cs` 26 例（纯函数档位/边界/取整 · 加载期校验 6 项 · 缓冲前行递减 vs 回头不动 · 每掷写 `RngDraw` · 吃/不吃结算 · 凑不齐强制挨饿且不扣粮 · 漏配 fail-fast）
| **F3d 隐藏房 Secret** | 完全缺失 | 新增 `'*'`；**不显示**在地图，靠 侦察/success 揭示 ⇒ 揭示后成 rewards 房（给侦察一个**非信息**的回报） |

> 🔴 **每加一个枚举 ⇒ 必须同时登记 ① `DungeonTileMap` 往返 ② `DungeonGridConfig` P30② 字符集校验**（`ToChar(FromChar(c)) != c` 会自动拦 ⇒ 这是**既有护栏**，别绕过）。

#### ✅ `D-6` 隐藏房 —— **2026-09-20 已落地**

| 项 | 内容 |
|---|---|
| **枚举 / 字符** | `DungeonTileKind.Secret`（第 12 位）· `DungeonTileMap` **`'*'` 双向登记**（P30② 护栏自动生效，有正/反两条用例） |
| **可通行** | ✅ `IsWalkable(Secret) == true` —— 🔴 不可走 ⇒ "揭示后成 rewards 房"**永远进不去**（静默失效）⚠️ |
| **地图显示** | 🔴 **完全不画**：`WalkMapView.FromTileWalk` 对 `stateOf == Unexplored` 的 `Secret` 格 **`continue`**（不画格/不加连线/不加热区）。⚠️ **不能画成"未知格"** —— 那也会被玩家**数格子看穿**（等于地图上明示）|
| **撒布** | `DungeonGridDeriver.ScatterCorridorContent`：**只在走廊格**、**与陷阱共用一趟扫描**（`DungeonTileKind` **单值** ⇒ 分趟会互相覆盖，"陷阱密度"变成"隐藏房概率的函数"）· 每格**至多一次**掷骰（**先判隐藏房**，更稀有）· 每次必写 `RngDraw` · 无 `rng` 且有概率 ⇒ **抛错** |
| **揭示入口** | `ExpeditionFlow.RevealSecrets(within)` / `RevealSecretsWithinRooms(roomIds)` —— 挂在既有 Curio `scout` 效果上，**复用** `MapScouting.RevealWithin` 的同一批结果（`#325` D6：不重写距离） |
| **揭示后的态** | 🔴 `Scouted`（**不是** `Visited`）—— **不写 `Visited` 集合**（侦察 ≠ 站过 ⇒ 否则 `D-1` 回头代价会算错）⚠️ |
| **非信息类回报** | `tuning.dungeon_layer.secrets.reward_gold` ⇒ `Economy.AwardContent`（金币的**唯一**记账通道 `GoldChangedEvent`）—— 🔴 **绝不用 `ExpeditionSession.Gain`**（它会把 `"gold"` **当口粮加** = 静默串账 ⚠️）|
| **幂等 / 配额** | 同一格只结算一次；`max_rewards_per_run`（0 = 不限）用尽 ⇒ 停 + **留痕** `secret_quota_reached:` |
| **加载期校验** | 概率 ∈ [0,100] · 🔴 **`reward_gold > 0`**（**意图检查**：回报为 0 ⇒ 玩家理性地永不侦察 ⇒ D-3/D-4 整条链路沦为装饰）· 配额 ≥ 0 |
| **测试** | `SecretTests` 18 例 + `SecretFlowTests` 12 例（全量 **739 通过 / 0 失败**）|

### ✅ F4 · `GridVision`（三态揭示）—— **G4，P1** —— 2026-09-20 **已落地（`D-3`）**

| 项 | 内容 |
|---|---|
| **职责** | 把 `_revealed` HashSet 升级为 **三态状态机** |
| **三态** | `unexplored`（暗）/ `scouted`（暗+亮轮廓，**拓扑层侦察**揭示）/ `visited`（浅灰，**走过**或**格视野**揭示） |
| **口径** | 承接 `#380`：**侦察=段级(看多远)**、**视野=格级(看清什么)**，**两者独立不互相加减** |
| **判据** | T1 未入过=暗；T2 侦察成功揭示但未走到=轮廓态；T3 走到=visited ⇒ 三者 UI 可分辨 |

**落地实况（`D-3`，2026-09-20）**

| 落点 | 实际实现 |
|---|---|
| 内核状态机 | `DungeonWalker._state: Dictionary<(int,int), RevealState>` **取代** `_revealed: HashSet`（不存在的键 = `Unexplored`，不预填全图）✓<br>⚠️ **不是** `DungeonWalkerRevealState` 新类型 —— `RevealState` 就放在 `DungeonGrid.cs`（与 `DungeonTileKind` 同文件，一个网格层的两个枚举）✓ |
| 三态**单向** | `Unexplored → Scouted → Visited`，**永不降级**（已 `Visited` 的格被侦察仍是 `Visited`）⇒ "走过去的记忆"不会被侦察抹掉 ✓ |
| `D-1` 隔离 | `_visited: HashSet` **保留**（"亲自站过"）⇒ **`Visited` 态 ≠ `HasVisited`** —— 前者含"视野照到"，后者只认"站过"。若混用，第一次走到被照到的格就会被当成**回头**多扣光 ⚠️（本轮最容易踩的坑） |
| `vision` 接线 | `DungeonWalker.StateAt(pos, vision)` —— 🔴 `vision` 是**显式参数、不是内部状态**（"能看多远"是关卡/装备属性，不是走格者属性；塞进构造会牵动 20+ 调用点，且"同一图不同光照下视野不同"无处安放）✓ |
| 视线（不穿墙） | `HasLineOfSight`：沿"先 x 后 y"的四向格子路径，**任一中间格是墙 ⇒ 看不见**（用的是与 `PathTo` 同一取向的 L 形，**确定性**；不用 Bresenham——我们本来就是四向网格）✓ |
| 墙/越界 | **永不揭示**（`IsWallOrOutOfBounds` 单一判定口）✓ |
| 数据载体 | ⚠️ `dungeon_grid.json` **仍不存在**（F7）⇒ `vision` 走 **`tuning.dungeon_layer.vision`**（与 `D-1` 的 `revisit_light_cost`、`D-5` 的 `hunger` 同位置、同纪律）✓<br>新类型 `TuningGridVision(RevealOnEnter, Radius, ScoutBonus)` 与 `DungeonGridVision` **字段一一对应**，**刻意不合并**（关卡级要校验"半径 < 图边长"，全局级不知道图尺寸）✓ |
| 加载期校验 | `DungeonGridConfig` ⑦ 加**上界**：`radius ≥ 图最长边` ⇒ 拒绝（"开局整张图全亮"= 探索层被**静默架空**）；`scout_bonus < 0` ⇒ 拒绝（负加成让侦察**永远失败**）✓<br>`TuningConfig` 同族：`radius ≥ 0`、`scout_bonus ≥ 0` ✓ |
| 侦察落点（段级） | `ExpeditionFlow.RevealScoutedRooms(roomIds)` + `RevealScoutedTiles(tiles)` ⇒ 这是"段级 → 格级"的**唯一桥**；`scout` curio 分支已接线（借 `MapScouting.RevealWithin`，**不重写距离口径**，纪律 `#325` D6）✓ |
| UI 三态配色 | `WalkMapView`: `SketchCell` 加 `State`（保留 `Revealed` 便捷读法）· 配色 `MapUnknown / MapScouted / MapVisited` · 骨架瓦片 **4 → 5 格**（`TileScouted = 4`，**前 4 格列号不动**）· 文字速写字形 `·`（未知）/ `▒`（只有轮廓）/ `□`（已看清）/ `■`（当前）/ `◆`（终点）✓ |
| 缓存键 | `Refresh` 的 `key` 必须把**三态分布**算进去（只数 `Revealed` ⇒ "Scouted 升 Visited" 不触发重绘 ⚠️）✓ |
| 读数（纪律 V） | `TileStateAt` / `TileVision` / `ScoutedTileCount` / `RevealedTileCount` / `VisitedTileCount` — 🔴 **可加性自证**：`已揭示 ≡ 站过 + 只侦察过`（有用例锁）✓ |

🔴 **`scout_bonus` 本阶段只登记不消费**（恒 0 时改动它**不会有任何效果**）：它是"侦察临时 +N 格"的加成，属**地图层侦察口径**，而 `MapScouting` 已有自己的 `reveal_depth_min/max` ⇒ 接线时**必须先统一真值**（纪律 `#325` D6：不许为同一语义造第二份公式），否则就是"两份真值"⚠️ 已在 `tuning.json` 的 note 里写明。

**测试**：`GridVisionTests.cs`（内核 12 例）+ `GridVisionFlowTests.cs`（流程 6 例）⇒ 全量 **660 → 683**（净增 23）✓

**`D-7` 落地记录（2026-09-20）**：
- 内核：`ExplorationActOut`（`IsAfflicted` / `RollCurioRefuse` / `RollEatRefuse` + 两条文案）—— 与 `HungerSpawner` **同构**（纯函数、零 Godot）
- 数值：`tuning.dungeon_layer.exploration`（阈值 50 / 拒绝摸奇物 33% / 拒绝进食 25%）+ **四条**加载期校验
- 门禁：`ExpeditionFlow.ResolveCurio` **最前面**（拒绝道具 ⇒ **被迫空手**）· `ExpeditionSession.ResolveHunger` 入口（拒绝进食 ⇒ **强制挨饿**）
- 🔴🔴 **判据 = `士气 < morale.start`** —— 折磨只在士气 0 挂上、**只有回到 50 才解除**（`MoraleLedger`）
  ⇒ 阈值 50 **不是近似而是模型自身的边界**；美德走 100 且立即回 50 ⇒ **美德与折磨永不并存** ✓
  ⇒ 附带收益：**零新载体**（士气本就跨趟）⇒ 不给 `Retained` 加 affliction 字段（不造第二份真值，`#325` D6）
- 测试：`ExplorationActOutTests`（内核 18 例）+ `ExplorationActOutFlowTests`（流程 6 例）⇒ 全量 **739 → 763**（净增 24）✓

### ✅ F5 · 探索层 act-out —— **G5，P1（已落地 `D-7`，2026-09-20）**

| 项 | 内容 |
|---|---|
| **职责** | 折磨状态在**探索层**的行为表现（不只是战斗内 `refuse_skill`） |
| **DD 原文** | ⑩ 拒绝摸奇物 / 拒绝进食 |
| **建议** | ① `CurioResolver` 前置门禁：单位带特定 Affliction ⇒ 有一定概率**拒绝交互**（ three-stage: 空手/道具/离开）<br>② 拒绝时**不是静默失败**，要有文案事件（写 `CombatLog`）<br>③ 与 F3c 饥饿联动：**拒绝进食**是自然组合 |
| **依赖** | F3c（饥饿 ✅）+ 现有 `morale/` Affliction 体系 |

### ⚠️ F6 · 光照收益扩到"战利品" —— **G6，P2**

| 项 | 内容 |
|---|---|
| **现状** | ✅ 已有"越暗 ⇒ 柴火/口粮**份数**更多"（`ExpeditionFlow.cs:439` 消费 `Light.Loot`） |
| **DD** | ⑨ 越暗 ⇒ **战利品额外掉率最高 95%** |
| **建议** | 在现有 `Loot` 旁**并列**加 `loot_bonus_pct`（**不改现有 struct 语义**，避免返工），作用于**战斗结算**的战利品掷骰 |

### ⚠️ F7 · 手写瓷砖图 —— **G7，P2**

| 项 | 内容 |
|---|---|
| **现状** | `dungeon_grid.json` **不存在**，跑的是 `DungeonGridDeriver`（从房间图派生） |
| **为什么 P2 但重要** | 派生图**永远只是"房间+连线的展开"** ⇒ 它的**宽度恒定、没有死路、没有开阔/狭窄对比** ⇒ **做不出 DD 的关卡设计感**（DD 手工 intake got it，Alduin corridor 1~8 格的节奏感来自设计） |
| **建议** | 阶段：先在 `data/` 落 1~2 张**手画图**（含死路/环路/宽窄），再逐步替换派生；届时**启用 `encounter`**（F3a 陷阱同族） |

---

## 6. 落地顺序建议

```
第 1 刀（补"探索的压力" whybe Interactive the soul）：
   F1 回头代价(纠偏) + F2 重访刷新       ⇒ G1+G2  ← 这两个是 DD 地牢层的灵魂，缺它们地牢就是"传送带"

第 2 刀（让走廊有内容）：
   F3a 陷阱转真 + F3c 饥饿 + F4 三态      ⇒ G3(部分)+G4  ← F3c ✅（D-5）· F4 ✅（D-3）· F3a ✅（D-4）
   （F3c 顺带为 F5 铺路）
   补充（2026-09-20）：F3d 隐藏房 ✅（D-6）· F3b 走廊障碍 ❌（未动）

第 3 刀（折磨走出战斗）：
   F5 探索层 act-out                     ⇒ G5   ✅ 已落地（D-7，2026-09-20）

第 4 刀（内容层打磨）：
   F3b 走廊障碍 + F3d 隐藏房 + F6 光照战利品 ⇒ G3(完)+G6   ← F3d ✅（D-6，2026-09-20）

第 5 刀（脱离派生）：
   F7 手写瓷砖图                          ⇒ G7 + 解锁 encounter
```

> 🔴 **每刀的通行条件**（守项目既有纪律）：
> ① 新数值先进 `tuning` + **`placeholder: true`**，禁写死（`#307`）；
> ② 新枚举同时登记字符表与 P30② 校验；
> ③ 随机必写 `RngDraw`；
> ④ 加载期 fail-fast，不留静默默认（红线 21）；
> ⑤ 先写**失败测试**（xUnit 零 Godot），再实现。

---

## 7. 与"已有清单"的关系（不返工）

| 既有资产 | 处置 |
|---|---|
| `DungeonGrid` / `DungeonWalker` / `DungeonTileMap` | ✅ **不动**（只扩 `RevealState`） |
| `WalkLightCost` / `DungeonWalkLight` 守恒 | ✅ **不动**（它是本层最漂亮的一处设计） |
| `MapScouting` 拓扑侦察 | ✅ **不动**（F4 只在网格层加一层"格视野"，两者独立）—— `D-3` 已把它的结果**单向喂给**格层（`RevealScoutedRooms`），**未改它一行** ✓ |
| `FlowPhase` 四相 + 三谓词 | ✅ **不动**（新触发**复用现有相位**，不新增） |
| `ExpeditionFlow` 房间交互/Curio/扎营 | ✅ **不动**（触发点从"进房间"扩到"落格"，`EnterRoom` 语义保留） |
| `ExpeditionMapConfig.RevisitCost` | ⚠️ **保留但需注意方向相反** —— 建议**只在网格层**(F1)引入 DD 口径，不去翻转旧口径以免返工 |

---

## 8. 待你裁决的空位（我不自行填）

```
① F1 回头代价的【数值】与【形态】：纯士气？还是"士气 + 概率性即兴事件"？(DD 只给士气)
② F1 是否要【翻转】房间图层那个"回头更便宜"的旧口径？—— 建议不动(免返工)，但需你确认可接受"两层口径不同"
③ F2 是否照 DD 的 2.5%/5% 方向，还是等 `encounter` 一起定？
④ F3c 饥饿是否做成"每格消耗"还是"每段消耗"？(与 WalkLightCost 的段/格口径需统一)
⑤ F7 手写图的优先级 —— 它决定"地牢到底像不像 DD"，但成本高，放最后还是提前？
```
