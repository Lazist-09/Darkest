# 我方数据总账（`darkest/data/*.json`）

> 由 `reports/unity_ref/_gen_our_inventory.py` 生成（python，实测计数）✓
> 用途：与参考项目 `Darkest-Dungeon-Unity` 的数据总账对账 ⇒ 得出「采用映射」✓

## 1. 一表看清

| 文件 | 字节 | 解析 | 条目容器 | 条目数 | 顶层 key |
|---|---:|---|---|---:|---|
| `buff_defs.json` | 13739 | ok | `buffs` | 22 | _field_classes, buffs |
| `buildings.json` | 66007 | ok | `buildings` | 8 | _note, _source, _field_classes, buildings |
| `camp_skills.json` | 2580 | ok | `camp_skills` | 12 | camp_skills |
| `curios.json` | 3691 | ok | `curios` | 7 | curios |
| `economy.json` | 2102 | ok | `stagecoach (dict)` | 8 | currency, note, battle_reward, light_tier_bonus, event_reward, stress_relief_cost, stress_relief_note, stress_relief, stagecoach_note, stagecoach, ratio_check |
| `encounters.json` | 1246 | ok | `encounters` | 2 | config, _note, encounters |
| `enemy_ai.json` | 1271 | ok | `archetypes` | 3 | taunt_weight, archetypes |
| `expedition_map.json` | 1317 | ok | `-` | 0 | note, map, move, move_note, scout, scout_note |
| `expedition_nodes.json` | 1809 | ok | `nodes` | 4 | nodes |
| `formation.json` | 1064 | ok | `player (dict)` | 3 | player, enemy, numeration, initial_roster, obstacles, rules |
| `heirloom_exchange.json` | 3014 | ok | `exchange_rates` | 12 | _note, _source, _design, exchange_rates |
| `heirlooms.json` | 4933 | ok | `quest_reward (dict)` | 6 | note, kinds, kind_note, quest_reward, upgrade_paths |
| `hero_upgrades.json` | 410036 | ok | `heroes (dict)` | 15 | _note, _source, heroes |
| `morale_events.json` | 3526 | ok | `events` | 18 | events |
| `quirks.json` | 105113 | ok | `quirks` | 170 | _note, _source, _field_classes, quirks |
| `room_contents.json` | 800 | ok | `rooms (dict)` | 3 | config, _note, rooms |
| `roster.json` | 2899 | ok | `heroes` | 8 | roster_cap, level_min, level_max, level_growth, experience, note, heroes |
| `sanitarium.json` | 1545 | ok | `diseases` | 3 | note, diseases, services, service_note, trait_rule |
| `skills.json` | 32639 | ok | `skills` | 44 | _dmg_pct_note, skills |
| `traits.json` | 3387 | ok | `traits` | 7 | _note, traits |
| `trap_defs.json` | 3187 | ok | `traps` | 4 | traps, unscouted_dodge_percent, disarm_bonus_percent, stress_damage, disarm_stress_heal, note |
| `trinkets.json` | 77713 | ok | `trinkets` | 196 | _note, _source, _ruling, _field_classes, rarities, trinkets |
| `tuning.json` | 13415 | ok | `light (dict)` | 12 | morale, consecutive_miss, heal_crit, virtue_rate, mental_reduction, physical_mitigation, weak, weak_recovery, weak_exit_hp_ratio, deaths_door, retreat, light |
| `units.json` | 10384 | ok | `units` | 7 | units |
| `unlocks.json` | 1040 | ok | `config (dict)` | 3 | config, _note, unlocks |

## 2. 逐文件字段频次（前 40）

### `buff_defs.json`（`buffs` · 22 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 22 |
| `name` | 22 |
| `polarity` | 22 |
| `duration` | 22 |
| `stack` | 22 |
| `source` | 22 |
| `modifiers` | 16 |
| `hooks` | 12 |
| `extra_rules` | 3 |

### `buildings.json`（`buildings` · 8 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 8 |
| `trees` | 8 |

### `camp_skills.json`（`camp_skills` · 12 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 12 |
| `name` | 12 |
| `owner_unit` | 12 |
| `cost` | 12 |
| `target` | 12 |
| `effect` | 12 |
| `source` | 12 |
| `effect_number` | 6 |

### `curios.json`（`curios` · 7 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 7 |
| `name` | 7 |
| `type` | 7 |
| `curio_type` | 6 |
| `bare_hands` | 6 |
| `item_results` | 6 |

### `encounters.json`（`encounters` · 2 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 2 |
| `note` | 2 |
| `enemy` | 2 |

### `enemy_ai.json`（`archetypes` · 3 条）

| 字段 | 出现次数 |
|---|---:|
| `archetype_id` | 3 |
| `target_preference` | 3 |
| `random` | 3 |
| `rules` | 3 |

### `expedition_nodes.json`（`nodes` · 4 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 4 |
| `type` | 4 |
| `name` | 4 |
| `cost` | 4 |
| `options` | 4 |
| `source` | 4 |

### `heirloom_exchange.json`（`exchange_rates` · 12 条）

| 字段 | 出现次数 |
|---|---:|
| `exchange_from_type` | 12 |
| `exchange_from_amount` | 12 |
| `exchange_to_type` | 12 |
| `exchange_to_amount` | 12 |
| `origin` | 12 |
| `placeholder` | 12 |

### `morale_events.json`（`events` · 18 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 18 |
| `name` | 18 |
| `delta` | 18 |
| `scope` | 18 |
| `occurrence` | 18 |
| `source` | 18 |
| `note` | 10 |

### `quirks.json`（`quirks` · 170 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 170 |
| `show_explicit_buff_description` | 170 |
| `show_flavor_description` | 170 |
| `show_explicit_curio_tag_description` | 170 |
| `random_chance` | 170 |
| `is_positive` | 170 |
| `is_disease` | 170 |
| `classification` | 170 |
| `incompatible_quirks` | 170 |
| `curio_tag` | 170 |
| `curio_tag_chance` | 170 |
| `keep_loot` | 170 |
| `buffs` | 170 |
| `can_modify_in_activity` | 170 |
| `can_be_replaced_by_new_quirk` | 170 |
| `origin` | 170 |

### `roster.json`（`heroes` · 8 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 8 |
| `name` | 8 |
| `archetype` | 8 |
| `level` | 8 |
| `traits` | 8 |
| `morale` | 2 |

### `sanitarium.json`（`diseases` · 3 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 3 |
| `name` | 3 |
| `penalty` | 3 |
| `contract_chance_per_run` | 3 |

### `skills.json`（`skills` · 44 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 44 |
| `name` | 44 |
| `self_slots` | 44 |
| `target` | 44 |
| `hit_mod` | 44 |
| `crit_mod` | 44 |
| `effects` | 44 |
| `use_limit` | 44 |
| `morale_effects` | 44 |
| `tags` | 44 |
| `range_axis` | 44 |
| `damage_axis` | 44 |
| `owner_unit` | 43 |
| `damage` | 28 |
| `dmg_pct` | 11 |
| `_dmg_pct_source` | 11 |
| `displacement` | 4 |
| `heal_fixed` | 4 |
| `self_damage_fixed` | 2 |
| `requires` | 2 |
| `bonus_vs_marked_percent` | 2 |
| `support_point_cost` | 1 |
| `pool_external` | 1 |

### `traits.json`（`traits` · 7 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 7 |
| `name` | 7 |
| `kind` | 7 |
| `ends_at` | 7 |
| `origin` | 7 |
| `source` | 7 |
| `modifiers` | 6 |
| `hooks` | 4 |

### `trap_defs.json`（`traps` · 4 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 4 |
| `region` | 4 |
| `hp_percent` | 4 |
| `weight` | 4 |
| `note` | 4 |

### `trinkets.json`（`trinkets` · 196 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 196 |
| `buffs` | 196 |
| `hero_class_requirements` | 196 |
| `rarity` | 196 |
| `price` | 196 |
| `limit` | 196 |
| `origin_dungeon` | 196 |
| `award_category` | 196 |
| `origin` | 196 |

### `units.json`（`units` · 7 条）

| 字段 | 出现次数 |
|---|---:|
| `id` | 7 |
| `name` | 7 |
| `side` | 7 |
| `hp` | 7 |
| `attack` | 7 |
| `speed` | 7 |
| `dodge` | 7 |
| `crit` | 7 |
| `resilience` | 7 |
| `stun_resist` | 7 |
| `bleed_resist` | 7 |
| `stat_debuff_resist` | 7 |
| `displace_resist` | 7 |
| `deaths_door_resist` | 7 |
| `skills` | 7 |
| `prot` | 7 |
| `weapon` | 4 |
| `armour` | 4 |
| `poison_resist` | 4 |
| `disease_resist` | 4 |
| `trap_resist` | 4 |
| `_align` | 4 |
| `move_distance` | 4 |

