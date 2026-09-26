# 升级的**效果侧**：参考的建筑效果表有 **53 个键**，而我方 **0 个**

> 🕒 2026-09-26 · 承接 `50`/`51_*.md` 的"效果侧未比" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 先答"英雄升级有没有效果侧" —— **两侧都没有**

```
📊 参考 `Upgrades/Heroes/<hero>.upgrades.json` 的键名（**13 个**）：
   `amount` · `code` · `currency_cost` · `id` · `is_instanced` ·
   `prerequisite_requirements` · `prerequisite_resolve_level` · `requirement_code` ·
   `requirements` · `tags` · `tree_id` · `trees` · `type` ✓
   ⇒ 🔴 **没有 `effect` / `stat` / `bonus` 之类的键** ⚠️
📊 我方 `hero_upgrades.json` 的 level 键全集（**5 个**）：
   `code` · `currency_cost` · `origin` · `prerequisite_resolve_level` · `prerequisites` ✓
   ⇒ 🔴 **也没有效果字段** ✓
⇒ ✅ **即：英雄升级树【两侧都只存"消耗 + 前置"】** ⇒
   📌 **"这一级给什么加成"不在升级树里** ⇒ 而在 **`Data/Heroes/Info/*.bytes`**（各阶数值）✓
      ⇒ 🎖️ **而那一层 A3 已落 39 字段** ⇒ ✅ **所以英雄这条线【没有未比的效果层】** ✓
```

## 2. 🔴🔴 但**建筑**不一样：参考有**一整张效果表**，我方**没有**

```
🎖️ **参考 `Data/Buildings/<id>.building.json`** —— 8 个文件，键名**并集 53 个**：
   abbey **26** · tavern **26** · sanitarium **19** · stage_coach **16** ·
   nomad_wagon 9 · blacksmith 7 · camping_trainer 7 · guild 7 ✓
🔴 **而我方 `buildings.json`** 的 level 键全集只有 **4 个**：
   `code` · `currency_cost` · `origin` · `prerequisites` ✓
⇒ 🔴 **即：我方建筑表【只有消耗与前置】，【没有效果】** ⚠️
   📌 而**我方自己的 `_field_classes` 也这么说**：
      `code = constraint+behavior · currency_cost = behavior（升级花费）·`
      `prerequisites = constraint · tags/is_instanced = doc` ✓
      ⇒ ✅ **只列了这 4 类** ⇒ **确实没有效果字段** ✓
```

## 3. 📊 那 53 个键的**内容分类**（实测归类）

| 类 | 键（出现次数） |
|---|---|
| **升级曲线（`*_upgrades`）** | `slot_upgrades`(3) · `cost_upgrades`(2) · `stress_upgrades`(2) · `affliction_cure_upgrades`(2) · `roster_size_upgrades` · `number_of_recruits_upgrades` · `number_of_trinkets_upgrades` · `disease_quirk_cost_upgrades` · `disease_quirk_cure_all_chance_upgrades` · `positive_quirk_cost_upgrades` · `negative_quirk_cost_upgrades` · `permanent_negative_quirk_cost_upgrades` · `trinket_cost_discount_upgrades` · `equipment_cost_discount_upgrades` · `camping_skill_cost_discount_upgrades` · `combat_skill_cost_discount_upgrades` |
| **解锁门槛**（**8/8 全有**）| `highest_dungeon_level` · `number_of_quests_finished` · `on_start_town_visit_priority` |
| **活动定义**（每个建筑的"服务"）| `meditation` · `prayer` · `flagellation`（abbey）· `bar` · `brothel` · `gambling`（tavern）· `treatment` · `disease_treatment`（sanitarium）|
| **活动参数** | `number_of_slots`(5) · `discount_percent`(4) · `chance`(4) · `cost_currency`(3) · `amount`(3) · `heal_low`/`heal_high`(2) · `duration`(2) · `quirk_treatment_chance` · `level` |
| **副作用** | `side_effects`(2) · `results`(2) · `data`(2) |
| **怪癖/增益库** | `quirk_library_name`(2) · `quirk_library_names`(2) · `buff_library_ids`(2) · `caretaker_friendly`(2) |
| **其它** | `number_of_extra_*`（远征战/营技/正负怪癖 4 个）· `guaranteed_previous_raid_dead_hero_levels` |

```
🎖️ **三条立刻可用的读数**：
   ① **`highest_dungeon_level` / `number_of_quests_finished` / `on_start_town_visit_priority`
      出现在 8/8 个建筑里** ⇒ 📌 **这是"建筑的解锁条件"三件套** ✓
      ⇒ ⚠️ **与我方 `goals` 的 `town_progression_*`（A8 抽过）可能是同一件事** ⇒ 记**待核** ✓
   ② **活动是"按建筑命名的子对象"**（`meditation`/`prayer`/`bar`/`treatment`…）⇒
      每个活动**自带 `side_effects` + `results`** ⇒ 📌 **这是"城镇服务"的数据层** ✓
      ⇒ 🔴 **我方完全没有这一层** ⚠️
   ③ **`side_effects` 的 `results` 用了 `type` + `data`**（如 `activity_lock` · `go_missing` ·
      `add_quirk`）⇒ 🎖️ **与 `Effects.txt` 的 `type` 风格同族** ⇒ 记**待核是否同一套** ✓
```

## 4. 🎖️ 所以"升级的效果侧"这条线的**结论分两半**

```
✅ **英雄**：**两侧都只存消耗/前置** ⇒ **没有未比的效果层** ⇒
   "加成"在 `Heroes/Info/*.bytes`（**A3 已落**）✓
🔴 **建筑**：**参考有一整张效果表（53 键）· 我方 0** ⇒
   ⚠️ **采用的不是"改数值"，而是【新建一层】** ✓
   📌 与 A6b 的 act-out、A6a 的 Effect 表**同族**（**整层缺失**）✓
⇒ 🎖️ **判据（第 14 条）**：**"这条'效果'是【附在升级树上】还是【另有一张表】？"** ——
   · **附在树上**（英雄）⇒ 找树的字段 ✓
   · **另有表**（建筑）⇒ **树只管消耗，效果在别处** ⚠️
   ⇒ ✅ **两种都存在** ⇒ **不能假设"升级树 = 全部"** ✓
   📌 而我上一件正是**假设了"效果在 upgrade 树里"** ⇒ 差点得出"没有效果层"的错误结论 ✓
```

## 5. 诚实边界

```
✅ **能验**：**参考英雄升级的 13 键 · 我方 5 键（两侧都无效果字段）** ·
   **参考建筑的 8 文件键名并集 53 个** · **逐键的出现次数** ·
   **我方建筑 level 键只有 4 个 + `_field_classes` 原文** · **键名的内容分类** ——
   **全部当场跑出** ✓
🎖️ 并**纠正了我上一件的假设**（"效果在升级树里"⇒ 建筑不在）✓
🔴 **不能验**：那 **53 个键的语义未逐个读代码** ⇒ 本件只做**键名与分类** ✓
🔴 **不能验**：`side_effects.results[].type` 与 `Effects.txt` 的 `type` **是否同一套** ⇒ 记**待核** ✓
🔴 **不能验**：`highest_dungeon_level` 等三件套与 A8 的 `town_progression_*` **是否同一件事** ⇒ 记**待核** ✓
🔴 **不能验**：**没有落任何数据** ⇒ 新建这一层后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{eff,bld}_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **升级的效果侧【查清】**：英雄无此层 · 建筑**整层缺失**（53 键）✓
🆕 **新暴露一项**：🔴 **建筑的"城镇服务/活动"层（我方 0）** ⇒ 与 A6a/A6b 同族
🆕 **可做**：① 核 `side_effects.results[].type` 与 `Effects.txt` 是否同一套
   ② 核 `highest_dungeon_level` 三件套 vs A8 的 `town_progression_*`
   ③ 核英雄升级消耗的「我方 vs 一手 E 盘」（`51_*.md` 记的局限）✓
⏸️ **等策划**：八张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的抽取/核对 ✓
```
