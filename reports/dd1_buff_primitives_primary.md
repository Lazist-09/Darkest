# M2 buff 原语层：**参考项目 ↔ 我们**的映射表（含双向未映射清单）

> 来源：`E:\SteamLibrary\steamapps\common\DarkestDungeon\shared\buffs\base.buffs.json`（本地参考项目，用户指定可参考）
> 🔴 **规则（架构）**：**每条原语要么被引用、要么进未映射清单** —— **不许静默跳过** ✓
> ⚠️ 下面的映射是**提案**（两套 schema 不同源：他们是 stat 导向、我们是 effect 导向）⇒ **请策划/架构裁定** ✓

## 1. 参考项目：原语用量（`stat_type / stat_sub_type`，共 2020 条 buff）

| 原语 | 次数 |
|---|---|
| `combat_stat_multiply / damage_low` | 198 |
| `combat_stat_multiply / damage_high` | 198 |
| `combat_stat_add / crit_chance` | 155 |
| `combat_stat_add / attack_rating` | 145 |
| `stress_dmg_received_percent / ` | 134 |
| `combat_stat_add / speed_rating` | 116 |
| `combat_stat_add / defense_rating` | 95 |
| `combat_stat_add / protection_rating` | 94 |
| `resistance / poison` | 65 |
| `resistance / bleed` | 60 |
| `hp_heal_received_percent / ` | 54 |
| `hp_heal_percent / ` | 51 |
| `debuff_chance / ` | 50 |
| `resolve_check_percent / ` | 48 |
| `resistance / move` | 45 |
| `scouting_chance / ` | 43 |
| `resistance / disease` | 42 |
| `resistance / debuff` | 40 |
| `combat_stat_multiply / max_hp` | 39 |
| `resistance / stun` | 38 |
| `resistance / death_blow` | 35 |
| `resistance / trap` | 33 |
| `stress_heal_received_percent / ` | 32 |
| `poison_chance / ` | 26 |
| `resolve_xp_bonus_percent / ` | 26 |
| `stun_chance / ` | 21 |
| `food_consumption_percent / ` | 20 |
| `bleed_chance / ` | 19 |
| `monsters_surprise_chance / ` | 19 |
| `damage_received_percent / ` | 15 |
| `move_chance / ` | 15 |
| `starving_damage_percent / ` | 11 |
| `remove_negative_quirk_chance / ` | 9 |
| `party_surprise_chance / ` | 9 |
| `crit_received_chance / ` | 5 |
| `stress_heal_percent / ` | 3 |
| `ignore_stealth / ` | 1 |
| `combat_stat_multiply / defense_rating` | 1 |
| `upgrade_discount / weapon` | 1 |
| `upgrade_discount / armour` | 1 |
| …（其余 8 种） | |

### 规则类型分布（`rule_type`）

| rule | 次数 |
|---|---|
| `always` | 960 |
| `in_rank` | 144 |
| `lightbelow` | 127 |
| `lightabove` | 103 |
| `monsterType` | 97 |
| `firstroundonly` | 80 |
| `rangedonly` | 67 |
| `hpbelow` | 66 |
| `meleeonly` | 61 |
| `at_deaths_door` | 42 |
| `in_camp` | 37 |
| `in_dungeon` | 36 |
| `hpabove` | 29 |
| `actorStatus` | 28 |
| `afflicted` | 26 |
| `skill` | 25 |
| `monster_type_count_min` | 22 |
| `in_activity` | 18 |
| `attacking_monster_type` | 17 |
| `is_guarded` | 10 |
| `virtued` | 10 |
| `is_actor_status` | 5 |
| `monsterSize` | 4 |
| `stress_above` | 3 |
| `walking_backwards` | 1 |
| `in_corridor` | 1 |
| `in_mode` | 1 |

## 2. 我们：`modifiers[].kind` 用量（共 22 条 buff）

| 我们的 kind | 次数 | 提案映射 |
|---|---|---|
| `damage_mod` | 9 | combat_stat_multiply / damage_low + damage_high (以及 damage_received_percent 家族) |
| `prob_mod` | 7 | *_chance 家族（stun_chance / debuff_chance / poison_chance / bleed_chance / move_chance ...） |
| `state_flag` | 3 | (参考件无直接对应：他们是 stat 导向；我们的引擎侧标志位，如 stunned) |
| `stat_mod` | 1 | combat_stat_add / combat_stat_multiply（按 stat_sub_type 细分：attack_rating / crit_chance / speed_rating / defense_rating / protection_rating ...） |

## 3. 🔴 未映射清单（**必须可见**）

- **我们侧未映射的 kind**：（无）
- **参考侧未被提案引用的 `stat_type`**：`resolve_check_percent`, `combat_stat_multiply`, `stress_dmg_received_percent`, `combat_stat_add`, `damage_received_percent`, `crit_received_chance`, `resistance`, `poison_chance`, `bleed_chance`, `hp_heal_percent`, `stress_heal_percent`, `ignore_stealth`, `hp_heal_received_percent`, `stress_heal_received_percent`, `resolve_xp_bonus_percent`, `stun_chance`, `move_chance`, `debuff_chance`, `scouting_chance`, `remove_negative_quirk_chance`, `party_surprise_chance`, `monsters_surprise_chance`, `food_consumption_percent`, `upgrade_discount`, `activity_side_effect_chance`, `starving_damage_percent`, `stress_dmg_percent`

## 4. 数量核对（可测）

- 参考 buff 条数 = **2020** · 原语种类 = **48** · rule_type 种类 = **27**
- 我们 buff 条数 = **22** · modifier kind 种类 = **4** · **未映射 = 0**
