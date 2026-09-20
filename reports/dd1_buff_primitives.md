# M2 buff 原语层：**参考项目 ↔ 我们**的映射表（含双向未映射清单）

> 来源：`F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonBuffs.json`（本地参考项目，用户指定可参考）
> 🔴 **规则（架构）**：**每条原语要么被引用、要么进未映射清单** —— **不许静默跳过** ✓
> ⚠️ 下面的映射是**提案**（两套 schema 不同源：他们是 stat 导向、我们是 effect 导向）⇒ **请策划/架构裁定** ✓

## 1. 参考项目：原语用量（`stat_type / stat_sub_type`，共 1801 条 buff）

| 原语 | 次数 |
|---|---|
| `combat_stat_multiply / damage_low` | 177 |
| `combat_stat_multiply / damage_high` | 177 |
| `combat_stat_add / attack_rating` | 134 |
| `combat_stat_add / crit_chance` | 126 |
| `stress_dmg_received_percent / ` | 126 |
| `combat_stat_add / speed_rating` | 102 |
| `combat_stat_add / defense_rating` | 90 |
| `combat_stat_add / protection_rating` | 90 |
| `resistance / poison` | 58 |
| `resistance / bleed` | 51 |
| `debuff_chance / ` | 46 |
| `hp_heal_percent / ` | 44 |
| `resolve_check_percent / ` | 44 |
| `resistance / move` | 42 |
| `scouting_chance / ` | 41 |
| `hp_heal_received_percent / ` | 38 |
| `resistance / debuff` | 38 |
| `resistance / disease` | 37 |
| `combat_stat_multiply / max_hp` | 36 |
| `resistance / stun` | 36 |
| `resistance / death_blow` | 34 |
| `resistance / trap` | 33 |
| `stress_heal_received_percent / ` | 32 |
| `resolve_xp_bonus_percent / ` | 26 |
| `monsters_surprise_chance / ` | 19 |
| `food_consumption_percent / ` | 18 |
| `stun_chance / ` | 14 |
| `poison_chance / ` | 14 |
| `move_chance / ` | 13 |
| `bleed_chance / ` | 12 |
| `hp_heal_amount / ` | 12 |
| `starving_damage_percent / ` | 11 |
| `remove_negative_quirk_chance / ` | 9 |
| `party_surprise_chance / ` | 9 |
| `damage_received_percent / ` | 5 |
| `stress_heal_percent / ` | 2 |
| `stress_dmg_percent / ` | 1 |
| `stress_dmg_received_percent / mode` | 1 |
| `combat_stat_multiply / defense_rating` | 1 |
| `upgrade_discount / weapon` | 1 |
| …（其余 1 种） | |

### 规则类型分布（`rule_type`）

| rule | 次数 |
|---|---|
| `always` | 793 |
| `in_rank` | 140 |
| `monsterType` | 136 |
| `lightbelow` | 121 |
| `lightabove` | 102 |
| `firstroundonly` | 80 |
| `hpbelow` | 66 |
| `rangedonly` | 65 |
| `meleeonly` | 57 |
| `at_deaths_door` | 42 |
| `in_camp` | 37 |
| `in_dungeon` | 36 |
| `hpabove` | 29 |
| `afflicted` | 26 |
| `actorStatus` | 20 |
| `skill` | 19 |
| `in_activity` | 14 |
| `virtued` | 8 |
| `monsterSize` | 4 |
| `stress_above` | 3 |
| `walking_backwards` | 1 |
| `in_corridor` | 1 |
| `in_mode` | 1 |

## 2. 我们：`modifiers[].kind` 用量（共 22 条 buff）

| 我们的 kind | 次数 | 提案映射 |
|---|---|---|
| `damage_mod` | 9 | **UNMAPPED** |
| `prob_mod` | 7 | **UNMAPPED** |
| `state_flag` | 3 | (no direct counterpart -- our engine-side flag, e.g. stunned) |
| `stat_mod` | 1 | **UNMAPPED** |

## 3. 🔴 未映射清单（**必须可见**）

- **我们侧未映射的 kind**：`damage_mod`, `prob_mod`, `stat_mod`
- **参考侧未被提案引用的 `stat_type`**：`hp_heal_percent`, `hp_heal_received_percent`, `stress_dmg_received_percent`, `stress_heal_received_percent`, `resolve_check_percent`, `resolve_xp_bonus_percent`, `resistance`, `stun_chance`, `poison_chance`, `bleed_chance`, `move_chance`, `debuff_chance`, `scouting_chance`, `remove_negative_quirk_chance`, `food_consumption_percent`, `starving_damage_percent`, `party_surprise_chance`, `stress_dmg_percent`, `monsters_surprise_chance`, `stress_heal_percent`, `hp_heal_amount`, `upgrade_discount`, `damage_received_percent`

## 4. 数量核对（可测）

- 参考 buff 条数 = **1801** · 原语种类 = **41** · rule_type 种类 = **23**
- 我们 buff 条数 = **22** · modifier kind 种类 = **4** · **未映射 = 3**
