# 参考项目 · 英雄【技能数值】抽取（`Heroes/Info/*.bytes` 的 stat 块）

> 由 `reports/unity_ref/_gen_hero_skills.py` 生成（实测抽取，非目测）✓
> 🔴 每个英雄文件里有**两块** `combat_skill:`：**无 `.level` 的表现块** 与 **带 `.level 0..4` 的数值块**；
>    本表只取**数值块** ✓（表现块只有 `.icon/.anim/.fx`）

## 1. 总账

| 英雄 | 文件行数 | 数值记录（技能×等级） | 技能数（去重 id） | 表现记录 | weapon 阶 | armour 阶 |
|---|---:|---:|---:|---:|---:|---:|
| `Abomination` | 73 | 35 | 7 | 7 | 5 | 5 |
| `Antiquarian` | 72 | 35 | 7 | 7 | 5 | 5 |
| `Arbalest` | 69 | 35 | 7 | 7 | 5 | 5 |
| `BountyHunter` | 70 | 35 | 7 | 7 | 5 | 5 |
| `Crusader` | 69 | 35 | 7 | 7 | 5 | 5 |
| `GraveRobber` | 69 | 35 | 7 | 7 | 5 | 5 |
| `Hellion` | 69 | 35 | 7 | 7 | 5 | 5 |
| `Highwayman` | 71 | 35 | 7 | 7 | 5 | 5 |
| `HoundMaster` | 69 | 35 | 7 | 7 | 5 | 5 |
| `Jester` | 69 | 35 | 7 | 7 | 5 | 5 |
| `Leper` | 69 | 35 | 7 | 7 | 5 | 5 |
| `ManAtArms` | 71 | 35 | 7 | 7 | 5 | 5 |
| `Occultist` | 69 | 35 | 7 | 7 | 5 | 5 |
| `PlagueDoctor` | 69 | 35 | 7 | 7 | 5 | 5 |
| `Vestal` | 69 | 35 | 7 | 7 | 5 | 5 |

**合计**：英雄 **15** · 数值记录 **525** 条 · 英雄数 × 技能数 = 15 × 7 = 105

## 2. 数值块的字段全集（含频次）

| 字段 | 出现次数 | 含义（读自参考项目代码/本地化） |
|---|---:|---|
| `.id` | 525 | 技能 id |
| `.level` | 525 | 等级 0..4（= 5 级 ✓） |
| `.launch` | 525 | 发动位（1..4） |
| `.target` | 525 | 目标表达式（如 `~.enemy 1 2`） |
| `.type` | 485 | 技能类型（melee/ranged/…） |
| `.atk` | 485 | **命中修正**（百分数） |
| `.dmg` | 485 | 🔴 **伤害修正（百分数）** ⇒ 我们要的 `dmg_pct` |
| `.crit` | 485 | 暴击修正（百分数） |
| `.is_crit_valid` | 485 | 能否暴击 |
| `.effect` | 430 | 效果名（→ `Mechanics/Effects.txt`） |
| `.move` | 60 | 位移 |
| `.heal` | 40 | 治疗量（区间） |
| `.valid_modes` | 35 | 可用模式 |
| `.self_target_valid` | 15 | 可自选为目标 |
| `.generation_guaranteed` | 14 | 必定生成 |
| `.human_effects` | 5 | 对人类额外效果 |
| `.beast_effects` | 5 | 对兽类额外效果 |
| `.is_continue_turn` | 5 | 连续行动 |
| `.per_turn_limit` | 5 | 每回合次数上限 |
| `.per_battle_limit` | 5 | 每场次数上限 |

## 3. 我们 4 个原型需要的英雄 · 逐技能逐级数值

> 对应关系：`warrior←hellion` · `tank←man_at_arms` · `medic←plague_doctor` · `commissar←highwayman` ✓

### `Hellion`（数值记录 35 条）

| 技能 id | lv | type | atk | 🔴 dmg | crit | launch | target | effect |
|---|---:|---|---:|---:|---:|---:|---|---|
| `adrenaline_rush` | 0 | melee | 0% | **0%** | 0% | 4321 | `` | `Adrenaline 1" "HellionHealSelf 1` |
| `adrenaline_rush` | 1 | melee | 0% | **0%** | 0% | 4321 | `` | `Adrenaline 2" "HellionHealSelf 2` |
| `adrenaline_rush` | 2 | melee | 0% | **0%** | 0% | 4321 | `` | `Adrenaline 3" "HellionHealSelf 3` |
| `adrenaline_rush` | 3 | melee | 0% | **0%** | 0% | 4321 | `` | `Adrenaline 4" "HellionHealSelf 4` |
| `adrenaline_rush` | 4 | melee | 0% | **0%** | 0% | 4321 | `` | `Adrenaline 5" "HellionHealSelf 5` |
| `barbaric_yawp` | 0 | melee | 95% | **-100%** | 0% | 21 | `~12` | `Strong Stun 1" "Hellion Exhaust` |
| `barbaric_yawp` | 1 | melee | 100% | **-100%** | 0% | 21 | `~12` | `Strong Stun 2" "Hellion Exhaust` |
| `barbaric_yawp` | 2 | melee | 105% | **-100%** | 0% | 21 | `~12` | `Strong Stun 3" "Hellion Exhaust` |
| `barbaric_yawp` | 3 | melee | 110% | **-100%** | 0% | 21 | `~12` | `Strong Stun 4" "Hellion Exhaust` |
| `barbaric_yawp` | 4 | melee | 115% | **-100%** | 0% | 21 | `~12` | `Strong Stun 5" "Hellion Exhaust` |
| `bleed_out` | 0 | melee | 85% | **15%** | 5% | 1 | `1` | `Strong Bleed 1" "Hellion Exhaust` |
| `bleed_out` | 1 | melee | 90% | **15%** | 6% | 1 | `1` | `Strong Bleed 2" "Hellion Exhaust` |
| `bleed_out` | 2 | melee | 95% | **15%** | 6% | 1 | `1` | `Strong Bleed 3" "Hellion Exhaust` |
| `bleed_out` | 3 | melee | 100% | **15%** | 6% | 1 | `1` | `Strong Bleed 4" "Hellion Exhaust` |
| `bleed_out` | 4 | melee | 105% | **15%** | 7% | 1 | `1` | `Strong Bleed 5" "Hellion Exhaust` |
| `breakthru` | 0 | melee | 85% | **-55%** | 0% | 4321 | `~123` | `Hellion Exhaust` |
| `breakthru` | 1 | melee | 90% | **-55%** | 0% | 4321 | `~123` | `Hellion Exhaust` |
| `breakthru` | 2 | melee | 95% | **-55%** | 1% | 4321 | `~123` | `Hellion Exhaust` |
| `breakthru` | 3 | melee | 100% | **-55%** | 2% | 4321 | `~123` | `Hellion Exhaust` |
| `breakthru` | 4 | melee | 105% | **-55%** | 2% | 4321 | `~123` | `Hellion Exhaust` |
| `if_it_bleeds` | 0 | melee | 85% | **-35%** | 0% | 321 | `23` | `Bleed 1` |
| `if_it_bleeds` | 1 | melee | 90% | **-35%** | 0% | 321 | `23` | `Bleed 2` |
| `if_it_bleeds` | 2 | melee | 95% | **-35%** | 1% | 321 | `23` | `Bleed 3` |
| `if_it_bleeds` | 3 | melee | 100% | **-35%** | 2% | 321 | `23` | `Bleed 4` |
| `if_it_bleeds` | 4 | melee | 105% | **-35%** | 2% | 321 | `23` | `Bleed 5` |
| `iron_swan` | 0 | melee | 85% | **0%** | 5% | 1 | `4` | `` |
| `iron_swan` | 1 | melee | 90% | **0%** | 6% | 1 | `4` | `` |
| `iron_swan` | 2 | melee | 95% | **0%** | 6% | 1 | `4` | `` |
| `iron_swan` | 3 | melee | 100% | **0%** | 6% | 1 | `4` | `` |
| `iron_swan` | 4 | melee | 105% | **0%** | 7% | 1 | `4` | `` |
| `wicked_hack` | 0 | melee | 85% | **0%** | 5% | 21 | `12` | `` |
| `wicked_hack` | 1 | melee | 90% | **0%** | 6% | 21 | `12` | `` |
| `wicked_hack` | 2 | melee | 95% | **0%** | 6% | 21 | `12` | `` |
| `wicked_hack` | 3 | melee | 100% | **0%** | 6% | 21 | `12` | `` |
| `wicked_hack` | 4 | melee | 105% | **0%** | 7% | 21 | `12` | `` |

### `ManAtArms`（数值记录 35 条）

| 技能 id | lv | type | atk | 🔴 dmg | crit | launch | target | effect |
|---|---:|---|---:|---:|---:|---:|---|---|
| `bellow` | 0 | ranged | 90% | **-90%** | 0% | 123 | `~1234` | `Disrupt 1` |
| `bellow` | 1 | ranged | 95% | **-90%** | 0% | 123 | `~1234` | `Disrupt 2` |
| `bellow` | 2 | ranged | 100% | **-90%** | 1% | 123 | `~1234` | `Disrupt 3` |
| `bellow` | 3 | ranged | 105% | **-90%** | 2% | 123 | `~1234` | `Disrupt 4` |
| `bellow` | 4 | ranged | 110% | **-90%** | 2% | 123 | `~1234` | `Disrupt 5` |
| `bolster` | 0 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Bolster 1` |
| `bolster` | 1 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Bolster 2` |
| `bolster` | 2 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Bolster 3` |
| `bolster` | 3 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Bolster 4` |
| `bolster` | 4 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Bolster 5` |
| `command` | 0 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Command 1` |
| `command` | 1 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Command 2` |
| `command` | 2 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Command 3` |
| `command` | 3 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Command 4` |
| `command` | 4 | ranged | 0% | **0%** | 0% | 1234 | `~@1234` | `Command 5` |
| `crush` | 0 | melee | 85% | **0%** | 5% | 12 | `123` | `` |
| `crush` | 1 | melee | 90% | **0%** | 6% | 12 | `123` | `` |
| `crush` | 2 | melee | 95% | **0%** | 6% | 12 | `123` | `` |
| `crush` | 3 | melee | 100% | **0%** | 6% | 12 | `123` | `` |
| `crush` | 4 | melee | 105% | **0%** | 7% | 12 | `123` | `` |
| `defender` | 0 | melee | 0% | **0%** | 0% | 1234 | `@1234` | `MAA Guard 1" "Defender 1` |
| `defender` | 1 | melee | 0% | **0%** | 0% | 1234 | `@1234` | `MAA Guard 1" "Defender 2` |
| `defender` | 2 | melee | 0% | **0%** | 0% | 1234 | `@1234` | `MAA Guard 1" "Defender 3` |
| `defender` | 3 | melee | 0% | **0%** | 0% | 1234 | `@1234` | `MAA Guard 1" "Defender 4` |
| `defender` | 4 | melee | 0% | **0%** | 0% | 1234 | `@1234` | `MAA Guard 1" "Defender 5` |
| `rampart` | 0 | melee | 90% | **-60%** | 5% | 123 | `123` | `Push 1A" "Stun 1` |
| `rampart` | 1 | melee | 95% | **-60%** | 6% | 123 | `123` | `Push 1B" "Stun 2` |
| `rampart` | 2 | melee | 100% | **-60%** | 6% | 123 | `123` | `Push 1C" "Stun 3` |
| `rampart` | 3 | melee | 105% | **-60%** | 6% | 123 | `123` | `Push 1D" "Stun 4` |
| `rampart` | 4 | melee | 110% | **-60%** | 7% | 123 | `123` | `Push 1E" "Stun 5` |
| `retribution` | 0 | melee | 85% | **-75%** | 0% | 123 | `123` | `MAA Riposte 1" "Mark Self` |
| `retribution` | 1 | melee | 90% | **-75%** | 0% | 123 | `123` | `MAA Riposte 2" "Mark Self` |
| `retribution` | 2 | melee | 95% | **-75%** | 1% | 123 | `123` | `MAA Riposte 3" "Mark Self` |
| `retribution` | 3 | melee | 100% | **-75%** | 2% | 123 | `123` | `MAA Riposte 4" "Mark Self` |
| `retribution` | 4 | melee | 105% | **-75%** | 2% | 123 | `123` | `MAA Riposte 5" "Mark Self` |

### `PlagueDoctor`（数值记录 35 条）

| 技能 id | lv | type | atk | 🔴 dmg | crit | launch | target | effect |
|---|---:|---|---:|---:|---:|---:|---|---|
| `battlefield_medicine` | 0 |  |  | **** |  | 43 | `@1234` | `Cure" "Cureself` |
| `battlefield_medicine` | 1 |  |  | **** |  | 43 | `@1234` | `Cure" "Cureself` |
| `battlefield_medicine` | 2 |  |  | **** |  | 43 | `@1234` | `Cure" "Cureself` |
| `battlefield_medicine` | 3 |  |  | **** |  | 43 | `@1234` | `Cure" "Cureself` |
| `battlefield_medicine` | 4 |  |  | **** |  | 43 | `@1234` | `Cure" "Cureself` |
| `blinding_gas` | 0 | ranged | 95% | **-100%** | 0% | 43 | `~34` | `Stun 1` |
| `blinding_gas` | 1 | ranged | 100% | **-100%** | 0% | 43 | `~34` | `Stun 2` |
| `blinding_gas` | 2 | ranged | 105% | **-100%** | 0% | 43 | `~34` | `Stun 3` |
| `blinding_gas` | 3 | ranged | 110% | **-100%** | 0% | 43 | `~34` | `Stun 4` |
| `blinding_gas` | 4 | ranged | 115% | **-100%** | 0% | 43 | `~34` | `Stun 5` |
| `disorienting_blast` | 0 | ranged | 95% | **-100%** | 0% | 4321 | `234` | `Disorient 1" "clear_corpses" "PD Disorienting Stun 1` |
| `disorienting_blast` | 1 | ranged | 100% | **-100%** | 0% | 4321 | `234` | `Disorient 2" "clear_corpses" "PD Disorienting Stun 2` |
| `disorienting_blast` | 2 | ranged | 105% | **-100%** | 1% | 4321 | `234` | `Disorient 3" "clear_corpses" "PD Disorienting Stun 3` |
| `disorienting_blast` | 3 | ranged | 110% | **-100%** | 2% | 4321 | `234` | `Disorient 4" "clear_corpses" "PD Disorienting Stun 4` |
| `disorienting_blast` | 4 | ranged | 115% | **-100%** | 2% | 4321 | `234` | `Disorient 5" "clear_corpses" "PD Disorienting Stun 5` |
| `emboldening_vapours` | 0 | melee | 0% | **0%** | 0% | 4321 | `@1234` | `PD Vapours Buff 1` |
| `emboldening_vapours` | 1 | melee | 0% | **0%** | 0% | 4321 | `@1234` | `PD Vapours Buff 2` |
| `emboldening_vapours` | 2 | melee | 0% | **0%** | 0% | 4321 | `@1234` | `PD Vapours Buff 3` |
| `emboldening_vapours` | 3 | melee | 0% | **0%** | 0% | 4321 | `@1234` | `PD Vapours Buff 4` |
| `emboldening_vapours` | 4 | melee | 0% | **0%** | 0% | 4321 | `@1234` | `PD Vapours Buff 5` |
| `incision` | 0 | melee | 85% | **0%** | 5% | 321 | `12` | `Minor Bleed 1` |
| `incision` | 1 | melee | 90% | **0%** | 6% | 321 | `12` | `Minor Bleed 2` |
| `incision` | 2 | melee | 95% | **0%** | 6% | 321 | `12` | `Minor Bleed 3` |
| `incision` | 3 | melee | 100% | **0%** | 6% | 321 | `12` | `Minor Bleed 4` |
| `incision` | 4 | melee | 105% | **0%** | 7% | 321 | `12` | `Minor Bleed 5` |
| `noxious_blast` | 0 | ranged | 95% | **-80%** | 2% | 432 | `12` | `PD Single Blight 1` |
| `noxious_blast` | 1 | ranged | 100% | **-80%** | 2% | 432 | `12` | `PD Single Blight 2` |
| `noxious_blast` | 2 | ranged | 105% | **-80%** | 3% | 432 | `12` | `PD Single Blight 3` |
| `noxious_blast` | 3 | ranged | 110% | **-80%** | 4% | 432 | `12` | `PD Single Blight 4` |
| `noxious_blast` | 4 | ranged | 115% | **-80%** | 4% | 432 | `12` | `PD Single Blight 5` |
| `plague_grenade` | 0 | ranged | 95% | **-90%** | 0% | 43 | `~34` | `PD Blight 1` |
| `plague_grenade` | 1 | ranged | 100% | **-90%** | 0% | 43 | `~34` | `PD Blight 2` |
| `plague_grenade` | 2 | ranged | 105% | **-90%** | 1% | 43 | `~34` | `PD Blight 3` |
| `plague_grenade` | 3 | ranged | 110% | **-90%** | 2% | 43 | `~34` | `PD Blight 4` |
| `plague_grenade` | 4 | ranged | 115% | **-90%** | 2% | 43 | `~34` | `PD Blight 5` |

### `Highwayman`（数值记录 35 条）

| 技能 id | lv | type | atk | 🔴 dmg | crit | launch | target | effect |
|---|---:|---|---:|---:|---:|---:|---|---|
| `duelist_advance` | 0 | melee | 85% | **-20%** | 2% | 432 | `1234` | `Hwy Riposte 1` |
| `duelist_advance` | 1 | melee | 90% | **-20%** | 2% | 432 | `1234` | `Hwy Riposte 2` |
| `duelist_advance` | 2 | melee | 95% | **-20%** | 3% | 432 | `1234` | `Hwy Riposte 3` |
| `duelist_advance` | 3 | melee | 100% | **-20%** | 4% | 432 | `1234` | `Hwy Riposte 4` |
| `duelist_advance` | 4 | melee | 105% | **-20%** | 4% | 432 | `1234` | `Hwy Riposte 5` |
| `grape_shot_blast` | 0 | ranged | 80% | **-60%** | -7% | 23 | `~123` | `` |
| `grape_shot_blast` | 1 | ranged | 85% | **-60%** | -6% | 23 | `~123` | `` |
| `grape_shot_blast` | 2 | ranged | 90% | **-60%** | -6% | 23 | `~123` | `` |
| `grape_shot_blast` | 3 | ranged | 95% | **-60%** | -6% | 23 | `~123` | `` |
| `grape_shot_blast` | 4 | ranged | 100% | **-60%** | -5% | 23 | `~123` | `` |
| `opened_vein` | 0 | melee | 85% | **-15%** | 0% | 321 | `12` | `Bleed 1" "Bleed Debuff 1` |
| `opened_vein` | 1 | melee | 90% | **-15%** | 0% | 321 | `12` | `Bleed 2" "Bleed Debuff 2` |
| `opened_vein` | 2 | melee | 95% | **-15%** | 1% | 321 | `12` | `Bleed 3" "Bleed Debuff 3` |
| `opened_vein` | 3 | melee | 100% | **-15%** | 2% | 321 | `12` | `Bleed 4" "Bleed Debuff 4` |
| `opened_vein` | 4 | melee | 105% | **-15%** | 2% | 321 | `12` | `Bleed 5" "Bleed Debuff 5` |
| `pistol_shot` | 0 | ranged | 85% | **-25%** | 10% | 432 | `234` | `Highwayman Pistol Dmg Marked` |
| `pistol_shot` | 1 | ranged | 90% | **-25%** | 11% | 432 | `234` | `Highwayman Pistol Dmg Marked` |
| `pistol_shot` | 2 | ranged | 95% | **-25%** | 11% | 432 | `234` | `Highwayman Pistol Dmg Marked` |
| `pistol_shot` | 3 | ranged | 100% | **-25%** | 12% | 432 | `234` | `Highwayman Pistol Dmg Marked` |
| `pistol_shot` | 4 | ranged | 105% | **-25%** | 12% | 432 | `234` | `Highwayman Pistol Dmg Marked` |
| `point_blank_shot` | 0 | ranged | 95% | **50%** | 0% | 1 | `1` | `Push 1A` |
| `point_blank_shot` | 1 | ranged | 100% | **50%** | 0% | 1 | `1` | `Push 1B` |
| `point_blank_shot` | 2 | ranged | 105% | **50%** | 1% | 1 | `1` | `Push 1C` |
| `point_blank_shot` | 3 | ranged | 110% | **50%** | 2% | 1 | `1` | `Push 1D` |
| `point_blank_shot` | 4 | ranged | 115% | **50%** | 2% | 1 | `1` | `Push 1E` |
| `take_aim` | 0 | ranged | 95% | **-80%** | 0% | 4321 | `234` | `Highwayman Buff 1` |
| `take_aim` | 1 | ranged | 100% | **-80%** | 0% | 4321 | `234` | `Highwayman Buff 2` |
| `take_aim` | 2 | ranged | 105% | **-80%** | 0% | 4321 | `234` | `Highwayman Buff 3` |
| `take_aim` | 3 | ranged | 110% | **-80%** | 0% | 4321 | `234` | `Highwayman Buff 4` |
| `take_aim` | 4 | ranged | 115% | **-80%** | 1% | 4321 | `234` | `Highwayman Buff 5` |
| `wicked_slice` | 0 | melee | 85% | **15%** | 5% | 321 | `12` | `` |
| `wicked_slice` | 1 | melee | 90% | **15%** | 6% | 321 | `12` | `` |
| `wicked_slice` | 2 | melee | 95% | **15%** | 6% | 321 | `12` | `` |
| `wicked_slice` | 3 | melee | 100% | **15%** | 6% | 321 | `12` | `` |
| `wicked_slice` | 4 | melee | 105% | **15%** | 7% | 321 | `12` | `` |
