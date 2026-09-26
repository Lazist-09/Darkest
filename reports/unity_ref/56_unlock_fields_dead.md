# 🎖️ 参考的**建筑解锁三件套是死数据** —— 解析了但**一次都没读过**

> 🕒 2026-09-26 · 承接 `55_unlock_reconcile.md §6` 的"任务数 vs 出征数未核" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️🎖️ 结论：**三个字段在整个 `Assets/Scripts` 里都没有消费点**

```
📊 **实测（逐字段全仓计数）**：
   `on_start_town_visit_priority` ⇒ **只在 `DarkestJsonReader.cs` 的 7 个 DTO 类里声明** ✓
   `number_of_quests_finished`    ⇒ **同上** ✓
   `highest_dungeon_level`        ⇒ **同上** ✓
   🔴 **在 `DarkestDatabase.cs`（唯一把 JSON 变成运行时对象的地方）里，**
      **`.number_of_quests_finished` / `.highest_dungeon_level` /**
      **`.on_start_town_visit_priority` 各出现 `0` 次** ⚠️
⇒ 🎖️ **即：它们被 `JsonConvert.DeserializeObject` 解析进 DTO，然后【就没人碰了】** ✓
```

### 证据链（三步都读了原文）

```
① **DTO 定义**（`DarkestJsonReader.cs:230-304`）：
   `JsonStageCoach` / `JsonNomadWagon` / `JsonCampingTrainer` / `JsonGuild` /
   `JsonBlacksmith` / `JsonSanitarium` / `JsonTavern` —— **7 个类各含这三个字段** ✓
② **反序列化**（同文件 `GetJsonCoach` / `GetJsonTavern` … 7 个静态方法）：
   `JsonConvert.DeserializeObject<JsonTavern>(tavernString)` ⇒ **值确实进来了** ✓
③ 🔴 **消费点**（`DarkestDatabase.cs:1101-1125` 等 7 处）：
   `jsonTavern` 用了 `bar` / `gambling` / `brothel`（**活动**）✓
   `jsonStageCoach` 用了 `number_of_recruits_upgrades` / `roster_size_upgrades`（**升级曲线**）✓
   `jsonSanitarium` 用了 `treatment.quirk_treatment_chance` / `positive_quirk_cost_upgrades` ✓
   ⇒ 🔴 **三个解锁字段【一个都没被读】** ⚠️
```

## 2. 🎖️ 这条**推翻了我上一件的推断**

```
🔴 `55_*.md §2` 我写：**"两个维度是【互补的】——'完成 N 个任务' 或 '到达地牢等级 2'"** ⚠️
   ⇒ 📌 那是**从数据形状推断的**（`quests=0` 的两栋恰好 `dungeon_lv=2`）✓
✅ **本件实测**：**那三个字段【根本没有代码读】** ⇒
   🔴 **"或/与"这个问题【不存在】** —— 因为它**没有被判定过** ✓
🎖️ **判据**：**"我从数据形状推断的语义，有没有【代码在实现它】？"** ——
   **没有 ⇒ 那不是"语义"，那是"一个没人读的字段"** ✓
   📌 **与 `plot_quests` 那次同族**（6 个字段有数据没人读），
      但这次更彻底：**连"或/与"的判定逻辑都不存在** ✓
```

## 3. 🔴 那"任务数 vs 出征数"这个问题**也自动关闭了**

```
🔴 `55_*.md §4①` 我记："**任务数 ≠ 出征数** ⇒ 记待核"（采用的前置）⚠️
✅ **本件答**：**参考的 `number_of_quests_finished` 从来没被用过** ⇒
   📌 **不存在"参考用任务数解锁"这回事** ⇒ 🔴 **没有口径要对齐** ✓
   ⇒ ✅ **即：我方的 `required_runs_finished` 是【我方自定】的机制，**
      **不是"参考用任务数、我方用出征数"的差异** ⚠️
🎖️ **这改变了采用结论**：**解锁阈值不能"照抄参考"，因为参考没在用它们** ✓
```

## 4. 🎖️ 那**参考实际怎么解锁建筑的**？（下一步的问题）

```
🎖️ **已知**：
   · 三个解锁字段 ⇒ 🔴 **死数据** ✓
   · **`stage_coach` 是起点**（`priority = 0`，且我方也是起手态）✓
   · **`buildings` 的加载**（`DarkestDatabase.cs`）**无条件 Add** ⇒
     📌 `buildings.Add(tavern)` / `buildings.Add(stageCoach)` …
     ⇒ 🔴 **没有"解锁判断"** ⚠️
🔴 **所以真实机制可能在**：
   · **`Campaign` 的存档字段**（"已解锁的建筑"列表）✓
   · 或 **`estate`/`town` 的初始化逻辑** ✓
   ⇒ 📌 记**待核**（**这是"照抄参考解锁机制"的真正前置**）⚠️
```

## 5. 诚实边界

```
✅ **能验**：**三个字段在 7 个 DTO 类里各声明一次** · **7 个 `GetJson*` 反序列化方法** ·
   **7 个消费点读了【别的】字段（逐行读过）** ·
   **`.字段名` 在 `DarkestDatabase.cs` 里各 0 次** —— **全部当场跑出** ✓
🎖️ 并**推翻了我上一件"互补/或"的推断**（那字段没人读）✓
🔴 **不能验**：**"0 次"是【我这三个字段名】的 0 次** ⇒
   ⚠️ 若代码用了**别的写法**（如反射、`dynamic`）⇒ 我看不到 ⇒ 记**可能漏** ✓
   📌 但 `DarkestJsonReader` 是**手写 DTO + `JsonConvert`** ⇒ 反射可能性低 ✓
🔴 **不能验**：**参考真实的解锁机制在哪** ⇒ 记**待核**（见 §4）✓
🔴 **不能验**：**没有落任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/quest_count*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
🎖️ **解锁这条线【查清了一半】**：三件套是死数据 ⇒ **不能照抄** ✓
🆕 **可做**：① 🔴 **找参考真实的建筑解锁机制**（`Campaign` 存档 / `estate` 初始化）
      —— 这是"照抄解锁"的真正前置
   ② 核英雄升级消耗的「我方 vs 一手 E 盘」（`51_*.md` 局限）
   ③ 把"推断的语义要有代码实现"补进 `observe_list` D11 ✓
⏸️ **等策划**：八张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
