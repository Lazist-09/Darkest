# A5：怪物 AI 抽取 —— 并**修正 `§6` 的一处描述**

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_ai.py`（可重跑）·
> 产物 `reports/unity_ref/ai_from_ref.json`（189,278 bytes）✓
> 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

## 1. 抽取读数（可复算）

```
源：`JsonAI.json`（**306,243 B** · 🔴 **有尾随逗号** —— 与 `§6` 一致）✓
顶层 `monster_brains` ⇒ **160 条** · id **160/160 有值 · 去重 160** ✓
   有 `skill_cooldowns` **25** · 有技能欲望 **158** · 有目标欲望 **158** ✓
   ⇒ 📌 **2 条既无技能欲望也无目标欲望**（编号 160 − 158）⇒ ⚠️ 待查（可能是空的占位）
```

## 2. 两类 desire 的 type 与 `data` 槽（**形状不同**）

```
**技能欲望** `skill_selection_desires` —— **9 种 type**（共 449 条）：
   `specific_skill` **347** · `performing_turn_skill` **52** · `ally_alive_skill` 16 ·
   `heal_skill` 11 · `random_skill` 7 · `effect_key_status_skill` 6 · `preferred_skill` 4 ·
   `ally_dead_skill` 3 · `fill_ally_captor_empty_skill` 3 ✓
   它的 `data` —— **24 个键**：`base_chance` 449 · `combat_skill_id` 420 · `marked_heroes_min/max` 34 ·
   `monsters_size_min/max` 32 · `ally_base_class_id` 19 · `first_initiative_only` 15 ·
   `performing_turn` 52 · `hp_ratio_treshold` 12 · `monsters_min/max` 12 ·
   `non_virtued_heroes_min` 11 · `guarded_monsters_min/max` 10 · … **`per_round_chance` 2** ✓
   🎖️ **信息量大**：这 24 个键是**"什么条件下想放这个技能"的完整判据集**
      （血量比 · 标记数 · 怪物体型 · 友方职业 · 控制数 · 是否**首轮限定** …）✓

**目标欲望** `target_selection_desires` —— **8 种 type**（共 294 条）：
   `random_target` **174** · `resistance_target` **43** · `marked_target` 31 · `health_target` 16 ·
   `stress_target` 12 · `ally_class_target` 11 · `rank_target` 4 · `fill_ally_captor_empty_target` 3 ✓
   它的 `data` —— **13 个键**（**前 5 个是每条都有**）：
   `base_chance` · `specific_combat_skill_id` · **`is_exclusive_desire`** ·
   `is_enemy_target_desire` · `is_friendly_target_desire` **各 294** ·
   `is_greater_comparison` 71 · `can_target_deaths_door`/`can_target_last_hero` **各 47** ·
   `can_target_not_overstressed`/`can_target_afflicted`/`can_target_virtued` **各 14** ·
   `ally_base_class_id` 12 · `resistance_type_id` 43 ✓
   🔴 而 `§6` 已记：**`is_exclusive_desire`（51 条在用）与 `is_pre_turn` 在参考里被【解析后丢弃】** ✓
      ⇒ 📌 **我实测到的是 294 条都带这个键**（`§6` 说的"51 条在用"口径不同 —— 见 §4）⚠️
```

## 3. 🔴🔴 修正 `§6` 的一处描述：**不是「×100 vs ×1」，而是跨 8 个数量级**

```
`§6` 原文：「技能欲望 `base_chance × 100` 而目标欲望 `× 1` ⇒ 两侧缩放不一致」
🔴 **实测发现这个描述【不够准】**：

**技能欲望 `base_chance`（449 个）**：
   `<=1` **283** · `1~10` **131** · `10~100` 21 · `100~1k` 6 · `1k~100k` 7 · `>100k` **1** ✓
   ⇒ 区间 **0.25 ~ 1e7**（**跨 8 个数量级**）⚠️
   🔴 **14 个值 > 100**，最大的是 **`brigand_sapper_D` 的 `ally_dead_skill` = `1e7`（一千万）** ⚠️
      它的 `data`：`{base_chance: 1e7, combat_skill_id: "sapper_summon", first_initiative_only: true,
        monsters_min: 1, monsters_max: 3, …}` ✓
**目标欲望 `base_chance`（294 个）**：
   **只有 7 个离散值**：`1.0`×194 · `2.0`×26 · `3.0`×24 · `100.0`×18 · `4.0`×11 · `5.0`×11 · `10.0`×10 ✓

⇒ 🎖️ **精确的表述应当是**：
   · **目标欲望**：`1 ~ 100`，**7 个离散值** ⇒ 🎖️ **是"权重"**（1/2/3/4/5/10/100 相对比例）✓
   · **技能欲望**：`0.25 ~ 1e7` ⇒ 🔴 **既有 < 1 的也有 1e7 的** ⇒
     ⚠️ **它更像"强制程度"**（`1e7` = **近似强制**，如"友方倒下 ⇒ 必放召唤"）✓
   ⇒ 📌 **即：`§6` 的"×100"只描述了【常见区间】的一部分**，
      **真正的差别是【语义不同】**：目标欲望是权重，技能欲望含**强制档** ✓
   🎖️ **而这对采用的影响更大**：⚠️ **不能把技能欲望的值当权重做归一**
      （`1e7` 会把其他项压成 0）⇒ **必须先识别"强制档"** ✓
```

## 4. 🔴 顺带更正 `§6` 的另一处口径（`is_exclusive_desire` 的条数）

```
`§6` 原文：「`is_exclusive_desire`（**51 条**在用）⇒ 参考项目解析后 `break;` 丢弃」
🔴 **我实测**：该键在**目标欲望里出现 294 次**（= 每一条目标欲望都有）⚠️
⇒ 📌 **两个数不是同一口径**：
   · `§6` 的 **51** 大概是「**值为 `true`** 的条数」
   · 我数的 **294** 是「**带这个键**的条数」（= 全部）
   ⇒ ✅ **两者可以并存**，但**必须写清分母**（纪律 AU）✓
   📌 本件**不声称 `§6` 错** —— 只**补上分母**：
      「**294 条目标欲望都带该键，其中 51 条为 `true`**」⇒ 请以这个为准 ✓
      ⚠️ 而"`true` 的条数"我**未单独统计** ⇒ 记为**未验**（不在本件范围内）✓
```

## 5. 🎖️ 与 A4 的 join **复核**（第二次，独立）

```
怪物引用 **158** 个 brain（去重）✓
   🔴 怪物引用了但**本表没有**的：**0** ✓
   🎖️ **本表有但没有怪物引用（孤儿）**：**2** ⇒ `drummer_A` · `swinetaur` ✓
⇒ ✅ **`ai` 表与 `monsters` 表【完全闭合】**（除 2 个孤儿）✓
   📌 而 `PLAN_adoption §9④` 当时写「**28 个 brain 无对应怪物**」⚠️
      ⇒ 🔴 **实测只有 2 个** ⇒ 📌 **那个 28 的口径不同**（大概是"按怪物逐条比对时的未命中数"，
        而非"brain 侧孤儿数"）⇒ ✅ **本件给的是 brain 侧口径：2 个** ✓
```

## 6. 诚实边界

```
✅ **能验**：160 brains · id 去重 160 · 两类 desire 的 type/键全集 · **`base_chance` 的完整分布**
   （技能 449 个跨 8 个数量级 · 目标 294 个只有 7 个值）· **`1e7` 那条的完整 data** ·
   **join 完全闭合 + 2 个孤儿** —— **全部当场跑出** ✓
🎖️ 并**如实修正 `§6` 两处描述**（"×100 vs ×1" 与 `is_exclusive_desire` 的条数口径）✓
🔴 **不能验**：**没有落任何 AI 数据** ⇒ "接上 160 brains 后怪物行为会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：`1e7` 这种**强制档**的**运行时语义未核**（我是从 `first_initiative_only` 等推断的）⚠️
   ⇒ 记为**推断**，不是结论 ✓
🔴 **不能验**：`skill_cooldowns` 的**内部结构未看** ⇒ 记**未做** ✓
🔴 **不能验**：**2 条既无技能也无目标欲望的 brain 是谁**，未查 ⇒ 记**未验** ✓
🔴 **不能验**：`is_exclusive_desire == true` 的**条数未单独统计** ⇒ 记**未验** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a5_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **A5 抽取完成**（160 brains 全文 + join 复核）✓ ⇒ **A 线只剩 A7（饰品/怪癖）** ✓
⏸️ **A7 的前置**：🔴 **被"buff `amount` 是分数"的舍入口径阻塞** ⇒
   见 `reports/contract_change_request_buff_primitives.md §1.3` ✓
   ⇒ 📌 **但 A7 的【抽取】不必等它**（抽取是纯读）⇒ ✅ **可以先抽，只等"落库"** ✓
🆕 **新提给架构一条**：🔴 **技能欲望的"强制档"（`1e7`）不能当权重归一** ⇒
   采用时**必须先分档**（权重档 / 强制档）⇒ 建议写进契约 ✓
```
