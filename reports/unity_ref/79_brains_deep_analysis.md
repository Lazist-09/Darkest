# A5 深析：160 brains 的**结构全貌** · 我方 3 个 archetype 是**另一套机制**（且缺两张表）

> 🕒 2026-09-26 · 工具 `tools/dsh/analyze_brains.py`（新，可重跑）✓
> 产物 `reports/unity_ref/brains_analysis.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **承接 A5 记的"规模差很大"⇒ 本件把它做成【逐字段对账】** ✓

---

## 1. 📊 160 brains 的**结构全貌**（**5 个顶层字段，160/160 全有**）

| 字段 | 出现 |
|---|---|
| `id` | **160** |
| `skill_cooldowns` | **160** |
| `skill_selection_desires` | **160** |
| `target_selection_desires` | **160** |
| 🔴 **`bonus_initiative_desires`** | **160** ← ⚠️ **A5 没单独记过它** ✓ |

```
🔴 **即：参考 brain 有【三张 desires 表】，不是两张** ⚠️
   （A5 的 `16_ai_from_ref.md` 记的是"skill_selection_desires + target_selection_desires"）
   ⇒ ✅ **本件补上第三张** ✓
```

## 2. 🎖️ 按 id 前缀分组 —— **按【怪物家族】，不是按副本/难度**

```
`skeleton` **21** · `swine` **16** · `brigand` **12** · `cultist` **10** ·
`carrion` 6 · `fishman` 6 · `ancestor` 6 · **`default` 4** · `swinetaur` 4 ·
`collector` 4 · `templar` 4 · `hag` 3 · `giant` 3 · `prophet` 3 · `cannon` 3 … ✓
⇒ 🎖️ **即：160 = 各怪物家族的变体之和**（与 A4 的 94 base / 230 变体**同一套命名**）✓
   📌 而 **`default` ×4** ⇒ ⚠️ **有兜底 brain** ⇒ 🎖️ **与我方 `target_preference` 的
      `fallback_probability` 是【同一个概念】** ✓（见 §4）
```

## 3. 🎖️🎖️ **三张 desires 表的完整内容**（逐 type 计数 + data 键）

### ① `skill_selection_desires`（**9 种**）

| type | 次数 | data 键数 | 关键 data |
|---|---|---|---|
| **`specific_skill`** | **347** | **19** | `combat_skill_id` · `hp_ratio_treshold` · **13 个 `*_min`/`*_max` 条件** · `per_round_chance` · `first_initiative_only` |
| **`performing_turn_skill`** | **52** | 10 | `performing_turn` · `combat_skill_id` · 6 个 min/max |
| `ally_alive_skill` | 16 | 7 | `ally_base_class_id` · … |
| `heal_skill` | 11 | 6 | `hp_ratio_treshold` · `first_initiative_only` |
| **`random_skill`** | 7 | **1** | 只有 `base_chance` |
| `effect_key_status_skill` | 6 | 3 | `effect_key_status` |
| **`preferred_skill`** | 4 | **1** | 只有 `base_chance` |
| `ally_dead_skill` | 3 | 6 | `ally_base_class_id` · `monsters_min/max` |
| `fill_ally_captor_empty_skill` | 3 | 4 | `can_target_deaths_door` · `can_target_last_hero` |

### ② `target_selection_desires`（**8 种**）

| type | 次数 | data 键数 | 关键 data |
|---|---|---|---|
| **`random_target`** | **174** | **10** | `is_enemy_target_desire` · `is_exclusive_desire` · `can_target_afflicted/deaths_door/virtued` · `specific_combat_skill_id` |
| **`resistance_target`** | **43** | 9 | `resistance_type_id` · `is_greater_comparison` |
| `marked_target` | 31 | 5 | — |
| `health_target` | 16 | 7 | `is_greater_comparison` · `ally_base_class_id` |
| `stress_target` | 12 | 9 | `can_target_not_overstressed` · … |
| `ally_class_target` | 11 | 6 | `ally_base_class_id` |
| `rank_target` | 4 | 5 | — |
| `fill_ally_captor_empty_target` | 3 | 7 | — |

### ③ 🔴 `bonus_initiative_desires`（**6 种** —— 本件新量出）

```
`guaranteed` 10 · `last_skill` 3 · `ally_actor_class_count` 3 · `death` 2 ·
`hp_ratio_threshold` 2 · `ally_last_damaged` 1 ✓
🎖️ **示例**（`swine_prince`）：
   `{"type":"ally_last_damaged","data":{"is_round_start":false,"is_round_in_progress":true,`
   ` "is_round_finish":true,"is_pre_turn":false,"is_post_turn":true,`
   ` "combat_skill_id_override":"**obliterate_enraged**","ally_base_class_id":"swine_piglet",`
   ` "ignore_if_stun":true}}` ✓
⇒ 🎖️ **即：它是【先手奖励】机制** —— "若小猪被打过 ⇒ 王子下回合改用 `obliterate_enraged`" ✓
   📌 **这是第三张表，且带 `*_override`**（**换技能**）⇒ ⚠️ **A5 完全没提** ✓
      🎖️ **判据（第 32 条）**：**"这个结构我数了【几张表】？会不会还有第三张？"
         ⇒ 先列【顶层字段全集】，而不是只看我已知的那几个"** ✓
```

## 4. 🔴 我方 `enemy_ai.json` 是**另一套机制**（且缺两张表）

| 参考字段 | 我方对应 | 判定 |
|---|---|---|
| `id` | `archetype_id` | ✅ 有 |
| **`skill_cooldowns`** | 🔴 **（无）** | 缺 |
| `skill_selection_desires` | **`rules[].{skill_id, when}`** | ⚠️ **另一种表达** |
| `target_selection_desires` | `target_preference` / `random` / **`taunt_weight`** | ⚠️ 同上 |
| **`bonus_initiative_desires`** | 🔴 **（无）** | 缺 |

```
📊 **我方只有 4 个\"决策维度\"**：`archetype_id` · `random` · `rules` · `target_preference` ✓
   而 **3 个 archetype**：`melee_soldier` · `ranged_archer` · `caster` ✓
   每个：`target_preference: "random"` · `random: {enabled: false, fallback_probability: 0.15}` ·
      `rules: [{skill_id, when: {self_slot_in: [...]}, mark_weight}]` ✓
⇒ 🎖️ **即：**
   · **我方 = 布尔条件 + 一个权重**（`when` 命中就选）✓
   · **参考 = 欲望权重池**（`base_chance` + **19 个条件键** + `is_exclusive_desire`）✓
   ⇒ ✅ **与 A5 的判定一致**（"两套机制"），而本件给出**逐字段的差** ✓
```

## 5. 🎖️ 而**规模差的真正含义**

```
📊 **参考 160 brains** vs **我方 3 archetypes** ⇒ 比值 **53 : 1** ✓
🔴 **但不只是"少"** —— 是**维度不同**：
   · 参考那 **160** 覆盖了**每个怪物家族的每个变体**（skeleton 21 · swine 16 · brigand 12…）✓
   · 我方 **3 个**是**抽象兵种**（近战/远程/施法）⇒ ⚠️ **不是同一个切法** ✓
⇒ 🎖️ **判据（第 33 条）**：**"两个集合规模差很大 —— 是【少了一部分】还是【切法不同】？"**
   · **少了一部分** ⇒ 补数量 ✓
   · **切法不同** ⇒ ⚠️ **要先决定用哪种切法** ⇒ 归策划/架构 ✓
   ⇒ ✅ **本件判定为后者**（我方按兵种、参考按怪家族）✓
      📌 与 A5 的"技能欲望是权重不是百分比"（`§6` 那次更正）**同族** ✓
```

## 6. 诚实边界

```
✅ **能验**：**160 brains 的 5 个顶层字段（160/160）** · **按 id 前缀的分组（逐家族）** ·
   **三张 desires 表的逐 type 计数与 data 键（含 `bonus_initiative_desires`）** ·
   **我方 3 个 archetype 的完整结构** · **5 字段对照表** · **规模比 53:1** ——
   **全部当场跑出** ✓
🎖️ 并**补上了 A5 漏记的第三张表**，且给出**逐字段差异** ✓
🔴 **不能验**：**`bonus_initiative_desires` 的 6 种 type 各自语义** ⇒
   📌 只读了 `ally_last_damaged` 一例 ⇒ 记**未逐个读** ✓
🔴 **不能验**：**参考的代码怎么消费这 19 个条件键** ⇒ 记**未查代码** ✓
   📌 而那是"按参考逻辑对齐"的**真正前置** ⇒ ⚠️ **记为重点待办** ✓
🔴 **不能验**：**我方 3 个 archetype 与 160 brains 的【具体映射】** ⇒
   📌 本件判"切法不同"⇒ 映射需先定切法 ⇒ 记**待裁** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{desires,our_ai_cmp}.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **A5 深析【完成】**：结构全貌 · 三张表 · 逐字段差 · 切法不同 ✓
🔴 **新暴露（重点）**：**参考的 19 个条件键【未读代码】** ⇒ 那是"按参考逻辑对齐"的前置
🆕 **可做**：① 🔴 **读代码：`skill_selection_desires` 的 19 个条件键怎么算**
   ② 核 `bonus_initiative_desires` 的 6 种 type 语义
   ③ 把第 32/33 条判据（先列顶层字段全集 · 规模差是少还是切法不同）补进 `observe_list` ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
