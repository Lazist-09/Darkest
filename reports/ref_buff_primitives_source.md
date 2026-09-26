# buff 原语层：来源与实测（由提取器生成 · 参考项目）

> 源：`F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonBuffs.json`（**本地参考项目** —— 用户指令 2026-09-25：数值采用该项目的来源）
> 用途：`darkest/data/*.json` 里 578 个 buff 引用**此前一个都解析不到**；本层给出定义池 ✓
> 解析器：`darkest/scripts/data/BuffPrimitivesConfig.cs`（零 Godot）· 用语料：`BuffPrimitiveTranslation.cs`

## 实测（每次运行重新测，不信任历史数字）

| 读数 | 实测 | 指令定义 | 一致 |
|---|---|---|---|
| primitives | **1801** | 1801 | OK |
| distinct_stat_type | **25** | 25 | OK |
| distinct_stat_combo | **41** | 41 | OK |
| distinct_rule_type | **23** | 23 | OK |
| with_duration | **63** | 63 | OK |
| is_false_rule_true | **60** | 60 | OK |
| remove_if_not_active_true | **1** | 1 | OK |
| amount_zero | **6** | 6 | OK |
- 重复 id = **0**（要求 0）· duration_type 与 duration 不同时出现/缺失的条目 = **0**（要求 0）

## 词表（实测闭集 —— 解析器按它 fail-fast）

### `stat_type` ×25

| stat_type | 条数 |
|---|---|
| `combat_stat_add` | 542 |
| `combat_stat_multiply` | 391 |
| `resistance` | 329 |
| `stress_dmg_received_percent` | 127 |
| `debuff_chance` | 46 |
| `hp_heal_percent` | 44 |
| `resolve_check_percent` | 44 |
| `scouting_chance` | 41 |
| `hp_heal_received_percent` | 38 |
| `stress_heal_received_percent` | 32 |
| `resolve_xp_bonus_percent` | 26 |
| `monsters_surprise_chance` | 19 |
| `food_consumption_percent` | 18 |
| `poison_chance` | 14 |
| `stun_chance` | 14 |
| `move_chance` | 13 |
| `bleed_chance` | 12 |
| `hp_heal_amount` | 12 |
| `starving_damage_percent` | 11 |
| `party_surprise_chance` | 9 |
| `remove_negative_quirk_chance` | 9 |
| `damage_received_percent` | 5 |
| `stress_heal_percent` | 2 |
| `upgrade_discount` | 2 |
| `stress_dmg_percent` | 1 |

### `rule_type` ×23

| rule_type | 条数 |
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
| `in_corridor` | 1 |
| `in_mode` | 1 |
| `walking_backwards` | 1 |

### `duration_type`（仅 63 条有）

| duration_type | duration | 条数 |
|---|---|---|
| `combat_end` | 4 | 35 |
| `quest_end` | 1 | 14 |
| `activity_end` | 1 | 12 |
| `idle_start_town_visit` | 1 | 1 |
| `quest_complete` | 1 | 1 |

## 我方引用的解析率（实测）

- 我方 `quirks.json` + `trinkets.json` 引用的**去重 buff id** = **556**
- 在参考项目定义池里能找到 = **482**
- 找不到 = **74** ⇒ 分成两类（下表逐条列出，**不静默跳过**）
  - **参考项目已改名**（同一 quirk/trinket 在参考项目里有另一套 buff 列表）= **50**
  - **参考项目未覆盖**（我方有、参考项目连条目都没有）= **24**

### 参考项目已改名（A7 待改清单 —— 按指令应采用参考项目的列表）

| 我方引用 | 属主 | 参考项目的列表 |
|---|---|---|
| `CRIT-2` | quirks.json:inaccurate | inaccurate ⇒ `CRIT-1` |
| `CRIT2` | quirks.json:accurate | accurate ⇒ `CRIT1` |
| `CRITMELEE-5` | quirks.json:weak_grip | weak_grip ⇒ `CRITMELEE-3` |
| `CRITMELEE5` | quirks.json:precision_striker | precision_striker ⇒ `CRITMELEE3` |
| `CRITRANGED-5` | quirks.json:flawed_release | flawed_release ⇒ `CRITRANGED-3` |
| `CRITRANGED5` | quirks.json:eagle_eye | eagle_eye ⇒ `CRITRANGED3` |
| `STUNSKILL25` | trinkets.json:cudgel_weight | cudgel_weight ⇒ `TRINKET_STUNSKILL_B3`, `TRINKET_SPD_D1` |
| `TRINKET_ANCESTOR_TENTACLE_RESOLVE_B` | trinkets.json:ancestors_tentacle_idol | ancestors_tentacle_idol ⇒ `TRINKET_RESOLVECHECK_B4` |
| `TRINKET_ANTIQ_DEBUFF_BUFF` | trinkets.json:antiq_4 | antiq_4 ⇒ `TRINKET_ANTIQ_SPEED_BUFF` |
| `TRINKET_ANTIQ_PROT_BUFF` | trinkets.json:antiq_3 | antiq_3 ⇒ `TRINKET_ANTIQ_HP_BUFF` |
| `TRINKET_BACKRANK_CRIT_B3` | trinkets.json:snipers_ring | snipers_ring ⇒ `TRINKET_BACKRANK_ACC_B3`, `TRINKET_SPD_D2` |
| `TRINKET_BLEEDSKILL_D15` | trinkets.json:cleansing_crystal | cleansing_crystal ⇒ `TRINKET_BLIGHTRESIST_B3`, `TRINKET_BLEEDRESIST_B3`, `TRINKET_DEBUFFRESIST_B3`, `TRINKET_BLIGHTSKILL_D3`, `TRINKET_BLEEDSKILL_D3`, `TRINKET_DEBUFFSKILL_D3` |
| `TRINKET_BLIGHTSKILL_D15` | trinkets.json:cleansing_crystal | cleansing_crystal ⇒ `TRINKET_BLIGHTRESIST_B3`, `TRINKET_BLEEDRESIST_B3`, `TRINKET_DEBUFFRESIST_B3`, `TRINKET_BLIGHTSKILL_D3`, `TRINKET_BLEEDSKILL_D3`, `TRINKET_DEBUFFSKILL_D3` |
| `TRINKET_DEBUFFSKILL_D15` | trinkets.json:cleansing_crystal | cleansing_crystal ⇒ `TRINKET_BLIGHTRESIST_B3`, `TRINKET_BLEEDRESIST_B3`, `TRINKET_DEBUFFRESIST_B3`, `TRINKET_BLIGHTSKILL_D3`, `TRINKET_BLEEDSKILL_D3`, `TRINKET_DEBUFFSKILL_D3` |
| `TRINKET_EATSASHALF` | trinkets.json:ancestors_bottle | ancestors_bottle ⇒ `TRINKET_MAXHP_B4`, `TRINKET_ANCESTOR_STRESSDMG` |
| `TRINKET_STUNSKILL_B15` | trinkets.json:witchs_vial | witchs_vial ⇒ `TRINKET_STUNSKILL_B2` |
| `TRINKET_ancestors_scroll_HEALDONE_BUFF` | trinkets.json:ancestors_scroll | ancestors_scroll ⇒ `TRINKET_ancestors_scroll_HEALRECEIVED_BUFF`, `TRINKET_ancestors_scroll_STRESSHEALRECEIVED_BUFF`, `TRINKET_ANCESTOR_STRESSDMG` |
| `TRINKET_ancestors_scroll_STRESSHEALDONE_BUFF` | trinkets.json:ancestors_scroll | ancestors_scroll ⇒ `TRINKET_ancestors_scroll_HEALRECEIVED_BUFF`, `TRINKET_ancestors_scroll_STRESSHEALRECEIVED_BUFF`, `TRINKET_ANCESTOR_STRESSDMG` |
| `TRINKET_berserk_mask_CRIT_B1` | trinkets.json:berserk_mask | berserk_mask ⇒ `TRINKET_CRIT_B3`, `TRINKET_SPD_B3`, `TRINKET_RESOLVECHECK_D3`, `TRINKET_HPHEALRECEIVED_D3` |
| `TRINKET_blasphemous_vial_STRESS_D1` | trinkets.json:blasphemous_vial | blasphemous_vial ⇒ `TRINKET_rangedonly_ACC_B3`, `TRINKET_STUNSKILL_B3`, `TRINKET_BLIGHTSKILL_B1`, `TRINKET_STRESSDMG_D3` |
| `TRINKET_bleed_stone_SKILL_B1` | trinkets.json:bleed_stone | bleed_stone ⇒ `TRINKET_BLEEDSKILL_B1`, `TRINKET_SPD_D1` |
| `TRINKET_bleeding_pendant_BLEEDSKILL_B1` | trinkets.json:bleeding_pendant | bleeding_pendant ⇒ `TRINKET_BLEEDSKILL_B2` |
| `TRINKET_blight_stone_SKILL_B1` | trinkets.json:blight_stone | blight_stone ⇒ `TRINKET_BLIGHTSKILL_B1`, `TRINKET_SPD_D1` |
| `TRINKET_blighting_satchel_SKILL_B1` | trinkets.json:seers_satchel | seers_satchel ⇒ `TRINKET_BLIGHTSKILL_B3`, `TRINKET_SPD_B1`, `TRINKET_DEF_D1` |
| `TRINKET_bloodthirst_ring_HEALRECEIVED_D1` | trinkets.json:bloodthirst_ring | bloodthirst_ring ⇒ `TRINKET_EATSASZERO`, `TRINKET_MAXHP_B1`, `TRINKET_DEF_D2` |
| `TRINKET_dark_tambourine_RESOLVECHECK_B1` | trinkets.json:dark_tambourine | dark_tambourine ⇒ `TRINKET_DEATHBLOWRESIST_B3`, `TRINKET_lightbelow_STRESSDMG_B3`, `TRINKET_RESOLVECHECK_D3` |
| `TRINKET_dazzling_charm_STUNSKILL_B1` | trinkets.json:dazzling_charm | dazzling_charm ⇒ `TRINKET_STUNSKILL_B1` |
| `TRINKET_debuff_stone_SKILL_B1` | trinkets.json:debuff_stone | debuff_stone ⇒ `TRINKET_DEBUFFSKILL_B1`, `TRINKET_SPD_D1` |
| `TRINKET_fasting_seal_NOSTRESSCAMPINGEAT` | trinkets.json:fasting_seal | fasting_seal ⇒ `TRINKET_fasting_seal_EATSASZERO`, `TRINKET_fasting_seal_NOSTARVEDMG`, `TRINKET_fasting_seal_DODGE_BUFF` |
| `TRINKET_fasting_seal_NOSTRESSHUNGER` | trinkets.json:fasting_seal | fasting_seal ⇒ `TRINKET_fasting_seal_EATSASZERO`, `TRINKET_fasting_seal_NOSTARVEDMG`, `TRINKET_fasting_seal_DODGE_BUFF` |
| `TRINKET_focus_ring_ACC_BUFF` | trinkets.json:focus_ring | focus_ring ⇒ `TRINKET_ACC_B3`, `TRINKET_CRIT_B1`, `TRINKET_DEF_D3` |
| `TRINKET_focus_ring_CRIT_BUFF` | trinkets.json:focus_ring | focus_ring ⇒ `TRINKET_ACC_B3`, `TRINKET_CRIT_B1`, `TRINKET_DEF_D3` |
| `TRINKET_heros_ring_RESOLVECHECK_B1` | trinkets.json:heros_ring | heros_ring ⇒ `TRINKET_RESOLVECHECK_B3` |
| `TRINKET_hunters_talon_EATSASHALF` | trinkets.json:hunters_talon | hunters_talon ⇒ `TRINKET_hunters_talon_CRIT_BUFF`, `TRINKET_hunters_talon_ACC_BUFF`, `TRINKET_hunters_talon_EATSASTWO` |
| `TRINKET_move_stone_SKILL_B1` | trinkets.json:move_stone | move_stone ⇒ `TRINKET_MOVESKILL_B1`, `TRINKET_SPD_D1` |
| `TRINKET_rampart_shield_DMGH_D1` | trinkets.json:rampart_shield | rampart_shield ⇒ `TRINKET_MOVESKILL_B3`, `TRINKET_STUNSKILL_B3`, `TRINKET_DMGL_D1`, `TRINKET_DMGH_D1` |
| `TRINKET_rampart_shield_DMGL_D1` | trinkets.json:rampart_shield | rampart_shield ⇒ `TRINKET_MOVESKILL_B3`, `TRINKET_STUNSKILL_B3`, `TRINKET_DMGL_D1`, `TRINKET_DMGH_D1` |
| `TRINKET_sacred_scroll_DMGH_D1` | trinkets.json:sacred_scroll | sacred_scroll ⇒ `TRINKET_sacred_scroll_STRESSDMG_BUFF`, `TRINKET_sacred_scroll_HEALDONE_BUFF`, `TRINKET_sacred_scroll_STUNSKILL_DEBUFF`, `TRINKET_sacred_scroll_SPD_DEBUFF` |
| `TRINKET_sacred_scroll_DMGL_D1` | trinkets.json:sacred_scroll | sacred_scroll ⇒ `TRINKET_sacred_scroll_STRESSDMG_BUFF`, `TRINKET_sacred_scroll_HEALDONE_BUFF`, `TRINKET_sacred_scroll_STUNSKILL_DEBUFF`, `TRINKET_sacred_scroll_SPD_DEBUFF` |
| `TRINKET_sickening_satchel_DMGH` | trinkets.json:stunning_satchel | stunning_satchel ⇒ `TRINKET_STUNSKILL_B2` |
| `TRINKET_sickening_satchel_DMGL` | trinkets.json:stunning_satchel | stunning_satchel ⇒ `TRINKET_STUNSKILL_B2` |
| `TRINKET_spiked_collar_HEALDONE_D1` | trinkets.json:spiked_collar | spiked_collar ⇒ `TRINKET_DMGL_B2`, `TRINKET_DMGH_B2`, `TRINKET_BLEEDSKILL_B2`, `TRINKET_HPHEALRECEIVED_D3` |
| `TRINKET_spiked_collar_HEALRECEIVED_D1` | trinkets.json:spiked_collar | spiked_collar ⇒ `TRINKET_DMGL_B2`, `TRINKET_DMGH_B2`, `TRINKET_BLEEDSKILL_B2`, `TRINKET_HPHEALRECEIVED_D3` |
| `TRINKET_stun_stone_SKILL_B1` | trinkets.json:stun_stone | stun_stone ⇒ `TRINKET_STUNSKILL_B1`, `TRINKET_DEF_D1` |
| `TRINKET_swordsmans_crest_HEALDONE_D1` | trinkets.json:swordsmans_crest | swordsmans_crest ⇒ `TRINKET_meleeonly_DMGL_B1`, `TRINKET_meleeonly_DMGH_B1`, `TRINKET_HEALDONE_D1` |
| `TRINKET_warriors_bracer_DMGH_B1` | trinkets.json:warriors_bracer | warriors_bracer ⇒ `TRINKET_meleeonly_DMGL_B1`, `TRINKET_meleeonly_DMGH_B1`, `TRINKET_DEF_D2` |
| `TRINKET_warriors_bracer_DMGL_B1` | trinkets.json:warriors_bracer | warriors_bracer ⇒ `TRINKET_meleeonly_DMGL_B1`, `TRINKET_meleeonly_DMGH_B1`, `TRINKET_DEF_D2` |
| `TRINKET_wrathful_bandana_HEALDONE_D1` | trinkets.json:wrathful_bandana | wrathful_bandana ⇒ `TRINKET_BACKRANK_DMGL_B2`, `TRINKET_BACKRANK_DMGH_B2`, `TRINKET_DEBUFFSKILL_B2`, `TRINKET_HEALDONE_D3`, `TRINKET_HPHEALRECEIVED_D3` |
| `bad_gambler_gold` | quirks.json:bad_gambler | bad_gambler ⇒ (空) |
| `bad_gambler_trinket` | quirks.json:bad_gambler | bad_gambler ⇒ (空) |

### 参考项目未覆盖（需策划裁：保留我方 / 删除）

| 我方引用 | 属主 |
|---|---|
| `CROW_QUIRK_ACC_BUFF` | quirks.json:corvids_eye |
| `CROW_QUIRK_DEF_BUFF` | quirks.json:corvids_grace |
| `CROW_QUIRK_DISEASERESIST_BUFF` | quirks.json:corvids_resilience |
| `CROW_QUIRK_EATSASTWO` | quirks.json:corvids_appetite |
| `CROW_QUIRK_LIGHTBLINDED_DEBUFF` | quirks.json:corvids_blindness |
| `CROW_QUIRK_MOVERESIST_BUFF` | quirks.json:corvids_grace |
| `CROW_QUIRK_SCOUTING_BUFF` | quirks.json:corvids_eye |
| `TRINKET_CROW_DISEASERESIST_BUFF` | trinkets.json:crow_wingfeather · trinkets.json:crow_tailfeather · trinkets.json:crow_talon |
| `TRINKET_CROW_EYE_BUFF` | trinkets.json:crow_eye |
| `TRINKET_CROW_EYE_BUFF2` | trinkets.json:crow_eye |
| `TRINKET_CROW_STRESS_DEBUFF` | trinkets.json:crow_wingfeather · trinkets.json:crow_tailfeather · trinkets.json:crow_talon |
| `TRINKET_CROW_TAILFEATHER_BUFF` | trinkets.json:crow_tailfeather |
| `TRINKET_CROW_TAILFEATHER_BUFF2` | trinkets.json:crow_tailfeather |
| `TRINKET_CROW_TALON_BUFF` | trinkets.json:crow_talon |
| `TRINKET_CROW_TALON_BUFF2` | trinkets.json:crow_talon |
| `TRINKET_CROW_WINGFEATHER_BUFF` | trinkets.json:crow_wingfeather |
| `TRINKET_CROW_WINGFEATHER_BUFF2` | trinkets.json:crow_wingfeather |
| `TRINKET_ethereal_crucifix_MAXHP_D1` | trinkets.json:ethereal_crucifix |
| `TRINKET_fortifying_garlic_BLEEDRESIST_B1` | trinkets.json:fortifying_garlic |
| `TRINKET_fortifying_garlic_BLIGHTRESIST_B1` | trinkets.json:fortifying_garlic |
| `TRINKET_fortifying_garlic_DISEASERESIST_B1` | trinkets.json:fortifying_garlic |
| `TRINKET_tempting_goblet_STRESS_DEBUFF` | trinkets.json:tempting_goblet |
| `skilled_gambler_gold` | quirks.json:skilled_gambler |
| `skilled_gambler_trinket` | quirks.json:skilled_gambler |

