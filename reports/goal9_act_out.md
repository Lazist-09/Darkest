# 第 9 刀 · `D-7` 探索层 act-out —— 交付报告

> 日期：2026-09-20 · 排期位：`dd_replication_roadmap §5` 第 5 刀
> 上一刀：`D-6` 隐藏房（测试 709 → **739**）
> 本刀：**`D-7` 探索层 act-out**（测试 739 → **763**，净增 **24**）

---

## 1. 一句话结论

**折磨走出了战斗** —— 带折磨的英雄现在会在**走廊 / 奇物前 / 饭点**上**拒绝**：拒绝用道具（被迫空手）、拒绝进食（强制挨饿）。
两条门禁都**必写 `RngDraw`**、**必写事件文案**（红线 21：绝不静默），且**未配置即整个关闭**（opt-in，既有行为逐字不变）。

---

## 2. `D-7` 的原始要求 vs 交付

| 路线图 `D-7` 行原文 | 交付 |
|---|---|
| ⚠️ 探索层 act-out 缺失（折磨 ⇒ 拒绝摸奇物/拒绝进食） | ✅ 两条都落 |
| `CurioResolver` 前置门禁 | ✅ 落地在 **`ExpeditionFlow.ResolveCurio` 最前面**（比 `ResolveBare` **早**）|
| 带特定 Affliction ⇒ 概率拒绝 | ✅ 判据 = **士气 < 50**（见 §3），概率 = `tuning` 配置 |
| 拒绝**必须有文案事件**（写 `CombatLog`，不静默） | ✅ `EventNodeResolvedEvent` + `EffectEvent` 双留痕 |
| 依赖 `D-5` | ✅ 拒绝进食正挂在 `ResolveHunger`（D-5 的结算入口）|

---

## 3. 🔴 本刀最大的设计裁定：**「带折磨」在探索层怎么判**

### 3.1 问题（调研阶段发现，无法从代码直接读出答案）

`D-7` 要求"带特定 Affliction ⇒ 拒绝"。但探索层面临三个现实：

1. **`ExpeditionFlow` 拿不到 `Roster`** —— 唯一的跨趟士气持有者从未被注入流程层；
2. **会话的 `Retained`（存士气）首战之前为空** —— 与 `D-4`/`D-5`/`D-6` **同源的有界缺口**；
3. **战斗内 affliction 台账（`IBuffLedger`）每场新建、不跨场** —— 探索层根本读不到那个 buff 集合。

### 3.2 裁定：阈值 50，**它不是近似，而是模型自身的边界**

实证（`MoraleLedger.cs`）：

- `GrantAffliction` **只**在 **士气 == 0** 时挂折磨（`CheckCollapseTrigger` / `TriggerCollapseForHpZero` 路径）；
- 第 127~134 行：**只有士气回到 `morale.start`（= 50）才解除**折磨与捆缚；
- 美德走 **士气 == 100**（`HandleMoraleMax`）且**立即把士气拉回 50**。

⇒ **美德与折磨永不并存**（士气从 100 往下是连续变化的，必然经过 50 ⇒ 到 50 时折磨已解除）。

**∴「士气 < 50」区间与「带折磨」在模型上等价。** 阈值 50 不是"挑了个好看的数"，而是 `MoraleLedger` 自己的解除线。

### 3.3 附带收益

- **零新载体**：士气本就跨趟累积（`#245`/`#287`）⇒ 不需要给 `Retained` 加 affliction 字段（那是**第二份真值**，`#325` D6 禁止）；
- **不需要注入 `Roster`**：判据走会话的 `LowestSurvivorMorale()`（只算存活者、取**最低者** —— 折磨是**个体**状态，取平均会把"一崩溃 + 五满状态"抹平）。

### 3.4 加载期 fail-fast（本刀最关键的一条校验）

```
dungeon_layer.exploration.morale_affliction_threshold 必须 == tuning.morale.start
```

若策划把阈值调成 30，就会出现「士气 40 时折磨**实际已挂上**（崩溃过），但探索层判他**不带折磨**」⇒ **规则自相矛盾**。
故两者必须同值，改一个就改另一个（fail-fast，不给静默分歧）。有一条专门的用例守它。

---

## 4. 落地清单

### 4.1 新增文件（2 个）

| 文件 | 职责 |
|---|---|
| `scripts/gameplay/sim/run/ExplorationActOut.cs` | **纯函数解析内核**（零 Godot）—— `IsAfflicted` / `RollCurioRefuse` / `RollEatRefuse` + 两条文案常量 |
| `tests/ExplorationActOutTests.cs` | 内核层 **18 例**（判据边界 / 掷骰留痕 / 文案 / 配置 fail-fast / 阈值同源） |

### 4.2 新增文件（流程层，1 个）

| 文件 | 职责 |
|---|---|
| `tests/ExplorationActOutFlowTests.cs` | 流程层 **6 例**（拒绝进食真的强制挨饿 / 不带折磨不掷 / 拒绝道具被迫空手 / 未配置行为逐字不变） |

### 4.3 改动的文件（8 个）

| 文件 | 改动 |
|---|---|
| `data/tuning.json` | 🆕 `dungeon_layer.exploration` 段（阈值 50 / 33% / 25% + note）—— **保持短对象单行 + 键序不动** |
| `scripts/data/TuningConfig.ExpeditionAndCombat.cs` | 🆕 `TuningExplorationActOut` record；`TuningDungeonLayer` 加 `Exploration` 属性 |
| `scripts/data/TuningConfig.cs` | 🆕 加载期 fail-fast 校验（4 条：阈值范围 / 阈值==morale.start / 两条概率 ∈ (0,100]） |
| `scripts/gameplay/sim/run/ExpeditionSession.cs` | 🆕 `LowestSurvivorMorale()` / `ActOutCanApply` / `ActOutEatRefuseCount`；`ResolveHunger` 加拒绝进食门禁 |
| `scripts/gameplay/sim/run/ExpeditionFlow.RoomInteractions.cs` | 🆕 `ResolveCurio` **最前面**的 Curio 门禁；`LastActOutRefused` / `LastActOutText` / `ActOutCurioRefuseCount` |
| `scripts/gameplay/sim/run/ExpeditionFlow.cs` | `ResolveHunger` 把 act-out 配置传进会话 |
| `scripts/gameplay/scene/ExpeditionComposition.cs` | 🆕 `[片4·D-7]` 启动自证打印 |
| `scripts/ui/BattleUI.RoomInteractions.cs` | 🆕 拒绝的**玩家可见提示**（Curio 面板 + 饥饿面板） |

---

## 5. 验证结果

| 项 | 结果 |
|---|---|
| `Darkest.Tests.csproj` 构建 | ✅ 0 error |
| `Darkest.csproj`（主工程）构建 | ✅ 0 error |
| **全量测试** | ✅ **763 passed / 0 failed**（基线 739 ⇒ **+24**）|
| `check_godot_refs.py`（内核零 Godot） | ✅ 0 hits |
| `check_data_discipline.py --all` | ✅ 数字 0 可疑 / 死函数 0 / 死键 0 |

🔴 **注**：`tuning.json` 的**格式契约**（键序 + 短对象单行 + `retreat_formula` 物理位置）已保持 —— 那 19 个依赖文本级插入的既有用例**全部通过**（含 `HungerTests` / `RevisitTests`）。

---

## 6. 本刀刻意守住的三个"静默失效"陷阱（前几刀都真踩过）

1. **门禁算晚了**（`D-4` 踩过：门禁在 `TryStep` 之后 ⇒ 恒 `Consumed` ⇒ 机制整个静默失效）
   ⇒ Curio 门禁放在 `ResolveBare` **之前**，并用"**不带折磨时 `DrawCount == 0`**"用例守（随机流零污染）。
2. **配了但不生效** ⇒ 用"**配置 100% + 带折磨 ⇒ 必须 `Refused`**"用例守（含事件留痕断言）。
3. **静默默认** ⇒ 概率 0 被加载期**拒绝**（"配了但永远不生效"家族）；要关闭必须**整段删掉** `exploration`。

---

## 7. 遗留与后续（诚实标注）

| 项 | 状态 |
|---|---|
| **首战之前的暴露窗口** | 🔴 有界缺口：`Retained` 战后落账 ⇒ 开局到首战之间门禁**不生效**（不掷不写，与 `HungerCanApply`/`TrapCanApply` **同因同果**，非本刀引入）|
| `curio.md §1.2 ⑤` 的**类型标签匹配**（只对某几类 Curio 生效，如 `Treasure`/`Body`） | ⏳ 未做：本刀对所有 Curio 一律适用（更简单、更可预期）；类型标签属内容层，待 M8 内容定实 |
| `Kleptomaniac`（**私吞战利品**）| ⏳ 未做：那是**怪癖**而非折磨，属另一条线（DD ⑩ 提到但本刀范围外）|
| Godot 冒烟 `--tile-walk` / `--dungeon-in-scene` | ⏳ 本会话无 PowerShell ⇒ **未实跑**（与 D-4/D-5/D-6 同一遗留）；已在组合根加 `[片4·D-7]` 自证打印，下次补跑 |
| 数值全部 `placeholder` | ⏳ 33% / 25% 待 M8 内容层定实 |

---

## 8. 下一步（按 `§5` 排期）

`D-7` ✅ ⇒ 下一刀：**`D-8` 手写瓷砖图**（`dungeon_grid.json` 落地、脱离派生图 ⇒ 解锁 `encounter`）。
