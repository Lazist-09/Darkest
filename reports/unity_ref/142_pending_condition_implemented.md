# 实现架构裁定 ②：**「待接条件」列已落地** —— 24 条 ⇒ **7 缺载体 + 17 未接线**

> 🕒 2026-09-26 · 承接 `141_*.md` 发现的"架构要求未执行" ✓
> 🎖️ **提交 `e31a045`**（代码）· **`7710033`**（窗口更新）✓
> 🔴 **`darkest/data/**` 零改动** ⇒ **数值零改动** ✓

---

## 1. 🎖️ 做了什么（**只加不改**）

```
📊 **改 `darkest/scripts/data/BuffPrimitiveTranslation.cs`** ⇒ 新增两个成员：
   · **`PendingCondition(name)`** ⇒ 按 `Classify` 的**去向**返回待接条件 ✓
   · **`PendingWithConditions`** ⇒ 清单 + 条件（供报表与用例打印）✓
📊 **三类条件**：
   | 去向 | 条件 |
   |---|---|
   | **`ExpeditionLayer`** | 🔴 **缺载体**（落点在趟级/城池级，而 buff 修正载体是**战斗级** ⇒ 需先造趟级修正载体）|
   | **战斗级 6 种**（`DamageMod`/`MoraleMod`/`HealMod`/`ProbMod`/`StatMod`/`StateFlag`）| ⚠️ **未接线**（落点在战斗结算，只差连上）|
   | **`UnitResistance`** | ⚠️ **未接线**（落点是 `units.json` 的抗性属性，不走 buff 台账）|
⇒ 🎖️ **即：架构要求的"区分两种缺"已做到** ✓
```

## 2. 🎖️ 而**关键设计：这一列【不手工维护】**

```
📊 **它是从 `ByStatType` / `Classify` 的【去向】推出来的**（`where switch { … }`）✓
⇒ 🎖️ **即：不与前两张清单（`Activated`/`Pending`）漂移** ✓
   📌 **判据（第 216 条复用）**：**"同一个名单在几处存在？
      ⇒ 只允许【一处本体 + 其余转发】"** ✓
   ＋ 🔴 **并明确划界**（注释原文）：
      > ⚠️ **它【不是】"能不能接"的判决** —— 判决在策划/架构；
      >   本列只陈述**前置条件** ✓
   ⇒ 🎖️ **这很重要**：**它陈述事实，不作判决** ⇒ ✅ **不越权** ✓
      📌 判据（第 226 条）：**"我加的这一列 ——
         是【陈述事实】还是【替人做判决】？（只能前者）"** ✓
```

## 3. 📊 独立复算：**24 ⇒ 7 + 17**（覆盖完整）

```
📊 **`tools/dsh/verify_pending_conditions.py`**（新工具，独立于 C# 实现）：
   从源码抽 `ByStatType` 映射 ⇒ 按去向独立分类 ⇒ 与代码输出对照 ✓
   🎖️ **判据（第 225 条）**：**"我刚加的列 ——
      能【独立复算】吗？（否则它只是我的断言）"** ✓

📊 **结果 —— 🔴 缺载体 7 条**（全 `ExpeditionLayer`）：
   `scouting_chance` · `monsters_surprise_chance` · `food_consumption_percent` ·
   `starving_damage_percent` · `remove_negative_quirk_chance` · `party_surprise_chance` ·
   `upgrade_discount` ✓
📊 **结果 —— ⚠️ 未接线 17 条**（战斗级 + 1 条 `resistance`）：
   `combat_stat_add` · `combat_stat_multiply` · `resistance` · `stress_dmg_received_percent` ·
   `debuff_chance` · `resolve_check_percent` · `hp_heal_received_percent` ·
   `stress_heal_received_percent` · `resolve_xp_bonus_percent` · `stun_chance` · `poison_chance` ·
   `move_chance` · `bleed_chance` · `hp_heal_amount` · `damage_received_percent` ·
   `stress_heal_percent` · `stress_dmg_percent` ✓
⇒ 🎖️ **7 + 17 = 24** ⇒ ✅ **覆盖完整** ✓
```

## 4. 🎖️🎖️ 而这个 **7 : 17** 的比**本身就是一条有用的读数**

```
📊 **对照架构当年的判断**（`141_*.md` 引的原文）：
   > 6 条属【趟级/城池级】⇒ buff 修正到不了
⇒ ✅ **实测 7 条**（架构说 6）⇒ 📌 **多 1 条**：**`upgrade_discount`**
   ⇒ 🎖️ **即：架构当年列的 6 条漏了 `upgrade_discount`**（它也归 `ExpeditionLayer`）✓
   📌 而架构列表里的 6 条是：`party_surprise_chance` · `monsters_surprise_chance` ·
      `scouting_chance` · `starving_damage_percent` · `food_consumption_percent` ·
      `remove_quirk_chance` ⇒ ✅ **本件实测的 7 条 = 那 6 条 + `upgrade_discount`** ✓
   🎖️ **判据（第 227 条）**：**"旧报告列了 N 项 ——
      我实测是 M 项；M > N ⇒ 那次【漏了一两项】"** ✓
```

## 5. 🎖️ 零行为证据（**三类**）

```
📊 **三类证据**：
   ① **编译**：`dotnet build Darkest.sln` ⇒ **0 错误 / 99 警告**（**与基线同**）✓
   ② **全量测试**：`dotnet test` ⇒ **失败 3 · 通过 843 · 总计 846** ⇒
      🔴 **与改动前【完全一致】**（那 3 个是既有红）✓
   ③ **数值**：**`darkest/data/**` 零改动** ⇒ ✅ **数值零改动** ✓
⇒ 🎖️ **即：这是【纯结构新增】**（`141_*.md` 说的 `dormant-by-data` 的对立面：
   **它连数据都没动** ⇒ 是**纯新增的查询能力**）✓
```

## 6. 诚实边界

```
✅ **能验**：**新增两个成员的源码** · **`build` 0 错误/99 警告** ·
   **全量 843/846（与基线一致）** · **独立复算 7 + 17 = 24** ·
   **7 条名单逐个** · **`darkest/data/**` 零改动** —— **全部当场跑出** ✓
🎖️ 并**发现架构当年漏了 `upgrade_discount`**（说 6 条，实为 7）✓
🔴 **不能验**：**那 17 条"未接线"里有没有【也缺载体】的** ⇒
   📌 本件按`Target` 去向分类 ⇒ ⚠️ **而"落点在战斗结算"是从 `Target` 推的**，
      不是逐个查落点 ⇒ 记**推断**（**逐个查落点是更大的一件**）✓
   🎖️ **判据（第 228 条）**：**"我按【枚举值】分类 ——
      那个枚举是【权威】吗？（它有可能是粗的）"** ✓
🔴 **不能验**：**这一列该不该【进测试】**（防它被改坏）⇒
   📌 目前**没有断言** ⇒ ⚠️ **与 `Activated`/`Pending` 的"双向断言"相比，这一列没有测试** ⇒
   记**待补** ✓
   🎖️ **判据（第 229 条）**：**"我新加的东西 —— 有测试钉住吗？
      没有 ⇒ 它会被改坏而无人知（与第 211 条对照）"** ✓
🔴 **不能验**：**是否该把 `PendingWithConditions` 接进报表/用例打印** ⇒
   📌 本件只提供 API，**未接** ⇒ 记**未接** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（工具本身入库）✓
```

## 7. 下一步

```
✅ **架构裁定 ②【已实现】**：待接条件列 · 7 缺载体 + 17 未接线 · 零行为 ✓
🆕 **两条可做**：① 🔴 **给这一列补测试**（判据 229 —— 与"双向断言"对齐）
   ② 逐个查那 17 条的【真落点】（判据 228 —— 别只信枚举）
🆕 **并报一条**：🎖️ **架构当年的"6 条"实为 7 条**（漏了 `upgrade_discount`）
🆕 **可做**：③ 把第 225~229 条判据补进 `observe_list` D11
   ④ **把"1/25 进度 + 待接条件 + 7:17 比"写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十二张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
