# 技能 `dmg_pct`：来源与实测（由 `tools/dsh/land_ref_skill_dmg.py` 生成 · **本地参考项目**）

> 源：`F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\*.bytes`（**本地参考项目** —— 用户指令 2026-09-25：数值采用该项目的来源）
> 映射表：`reports/unity_ref/skill_dmg_mapping.json`（逐条带 `effect` 全串依据）· 参考技能实测``：`reports/unity_ref/hero_skills_from_ref.json`
> 落库口径：(一)【明确】+整数值 ⇒ 落；(二)已有值且**自己的注记点名了参考技能** ⇒ 只核数值（相等保留 / 不等纠正）；其余不落

## 实测（每次运行重新测）

| 读数 | 值 |
|---|---|
| 技能总数 | **44** |
| 映射【明确】 | **7** |
| 映射【候选】 | **34** |
| 映射【无对应】 | **3** |
| 落库前带 `dmg_pct` | **14** |
| 落库后带 `dmg_pct` | **14** |
| 其中由 (a) 落库 | **6** |
| 其中由 (b) **保留**（数值经参考核对相等） | **8** |
| 其中由 (b) **纠正** | **0** |
| **撤出**（无可解析声明） | **0** |
| 未落、待策划裁定 | **30** |

## 逐条结果

| 我方技能 | 参考英雄 | 参考技能 | `dmg_pct` | 落库前 | 判定 |
|---|---|---|---:|---:|---|
| `warrior_cleave` | Hellion | wicked_hack | **0** | 0 | **落库** |
| `warrior_sweep` | Hellion | iron_swan | **0** | 0 | 保留（参考核对相等） |
| `warrior_lunge` | Hellion | breakthru | **-55** | -55 | 保留（参考核对相等） |
| `warrior_javelin` | GraveRobber | thrown_dagger | — | — | 不落（候选） |
| `warrior_shield_bash` | Hellion | barbaric_yawp | — | — | 不落（候选） |
| `warrior_war_cry` | Jester | battle_ballad | **-100** | -100 | 保留（参考核对相等） |
| `warrior_battle_fury` | Hellion | adrenaline_rush | **0** | 0 | **落库** |
| `warrior_catch_breath` | Abomination | absolution | — | — | 不落（候选） |
| `warrior_last_stand` | Jester | heroic_end | — | — | 不落（候选） |
| `tank_guard_wall` | ManAtArms | defender | **0** | 0 | **落库** |
| `tank_taunt` | - | - | — | — | 不落（无对应） |
| `tank_shield_bash` | ManAtArms | crush | **0** | 0 | 保留（参考核对相等） |
| `tank_heavy_ram` | ManAtArms | rampart | **-60** | -60 | 保留（参考核对相等） |
| `tank_iron_wall` | ManAtArms | bolster | — | — | 不落（候选） |
| `tank_war_cry` | ManAtArms | command | **0** | 0 | **落库** |
| `tank_hunker` | ManAtArms | bolster | — | — | 不落（候选） |
| `tank_catch_breath` | Abomination | absolution | — | — | 不落（候选） |
| `tank_selfless_charge` | Jester | heroic_end | — | — | 不落（候选） |
| `medic_scalpel` | PlagueDoctor | incision | **0** | 0 | 保留（参考核对相等） |
| `medic_cross_slash` | Vestal | mace_bash | — | — | 不落（候选） |
| `medic_anesthetic` | PlagueDoctor | blinding_gas | **-100** | -100 | **落库** |
| `medic_medicine_flask` | Vestal | gods_illumination | — | — | 不落（候选） |
| `medic_double_hit` | - | - | — | — | 不落（无对应） |
| `medic_field_strike` | ManAtArms | rampart | — | — | 不落（候选） |
| `medic_lethal_injection` | PlagueDoctor | noxious_blast | **-80** | -80 | 保留（参考核对相等） |
| `medic_first_aid` | Vestal | divine_grace | — | — | 不落（候选） |
| `medic_group_bandage` | Vestal | gods_comfort | — | — | 不落（候选） |
| `commissar_command_blade` | Highwayman | wicked_slice | **15** | 15 | **落库** |
| `commissar_charge_order` | Highwayman | wicked_slice | — | — | 不落（候选） |
| `commissar_pistol_shot` | Highwayman | pistol_shot | — | — | 不落（候选） |
| `commissar_supervise` | BountyHunter | target_tag | — | — | 不落（候选） |
| `commissar_battle_inspiration` | Crusader | inspiring_cry | — | — | 不落（候选） |
| `commissar_mobilize` | ManAtArms | command | — | — | 不落（候选） |
| `commissar_execution_order` | BountyHunter | collect_bounty | — | — | 不落（候选） |
| `commissar_burst_fire` | Highwayman | grape_shot_blast | **-60** | -60 | 保留（参考核对相等） |
| `commissar_total_mobilization` | ManAtArms | bolster | — | — | 不落（候选） |
| `melee_heavy_slash` | Leper | chop | — | — | 不落（候选） |
| `melee_charge` | Hellion | adrenaline_rush | — | — | 不落（候选） |
| `ranged_precise_shot` | Arbalest | sniper_shot | — | — | 不落（候选） |
| `ranged_intimidating_shot` | - | - | — | — | 不落（无对应） |
| `ranged_retreat` | Hellion | move | — | — | 不落（候选） |
| `caster_fear_whisper` | Occultist | weakening_curse | — | — | 不落（候选） |
| `caster_mental_shock` | Highwayman | grape_shot_blast | — | — | 不落（候选） |
| `move` | Hellion | move | — | — | 不落（明确） |

## 纠正清单（旧值与参考项目实测不符 ⇒ 按新来源纠正）

（无）

## 保留清单（库里已有值、且等于自己注记所点名参考技能的实测 `.dmg`）

| 技能 | 值 | 声明点名的参考技能 |
|---|---:|---|
| `warrior_sweep` | 0 | `Hellion/iron_swan` |
| `warrior_lunge` | -55 | `Hellion/breakthru` |
| `warrior_war_cry` | -100 | `Hellion/barbaric_yawp` |
| `tank_shield_bash` | 0 | `ManAtArms/crush` |
| `tank_heavy_ram` | -60 | `ManAtArms/rampart` |
| `medic_scalpel` | 0 | `PlagueDoctor/incision` |
| `medic_lethal_injection` | -80 | `PlagueDoctor/noxious_blast` |
| `commissar_burst_fire` | -60 | `Highwayman/grape_shot_blast` |

## 撤出清单

（无）

## 待策划逐行裁定（本轮**未落库**）

参考项目对"纯功能/无伤害"技能的写法是 `.dmg -100%`（实测：15 英雄 × 7 技能里 **11** 条是 -100，且每条都是纯功能）；普通伤害技能写 `.dmg 0%`。下表 `Σ段倍率` 是我们自己的伤害形状（来自 `damage.segments`），`建议值` 是按上述约定**推**的，不是实测值。

| 我方技能 | 武器段倍率 Σ | 当前映射判定 | 参考候选 | 建议值 |
|---|---:|---|---|---:|
| `warrior_javelin` | 1 | 候选 | GraveRobber/thrown_dagger=0 | **0** |
| `warrior_shield_bash` | 0.6 | 候选 | Hellion/barbaric_yawp=-100 | **0** |
| `warrior_catch_breath` | 0 | 候选 | Abomination/absolution=0 | **-100** |
| `warrior_last_stand` | 2 | 候选 | Jester/heroic_end=150 | **0** |
| `tank_taunt` | 0 | 无对应 | — | **-100** |
| `tank_iron_wall` | 0 | 候选 | ManAtArms/bolster=0 | **-100** |
| `tank_hunker` | 0 | 候选 | ManAtArms/bolster=0 | **-100** |
| `tank_catch_breath` | 0 | 候选 | Abomination/absolution=0 | **-100** |
| `tank_selfless_charge` | 1.8 | 候选 | Jester/heroic_end=150 | **0** |
| `medic_cross_slash` | 1.2 | 候选 | Vestal/mace_bash=0 | **0** |
| `medic_medicine_flask` | 0.9 | 候选 | Vestal/gods_illumination=-50 | **0** |
| `medic_double_hit` | 1 | 无对应 | — | **0** |
| `medic_field_strike` | 1 | 候选 | ManAtArms/rampart=-60 | **0** |
| `medic_first_aid` | 0 | 候选 | Vestal/divine_grace=None | **-100** |
| `medic_group_bandage` | 0 | 候选 | Vestal/gods_comfort=None | **-100** |
| `commissar_charge_order` | 1 | 候选 | Highwayman/wicked_slice=15 | **0** |
| `commissar_pistol_shot` | 0.95 | 候选 | Highwayman/pistol_shot=-25 | **0** |
| `commissar_supervise` | 0.9 | 候选 | BountyHunter/target_tag=-100 | **0** |
| `commissar_battle_inspiration` | 0 | 候选 | Crusader/inspiring_cry=None | **-100** |
| `commissar_mobilize` | 0 | 候选 | ManAtArms/command=0 | **-100** |
| `commissar_execution_order` | 0 | 候选 | BountyHunter/collect_bounty=0 | **-100** |
| `commissar_total_mobilization` | 1.5 | 候选 | ManAtArms/bolster=0 | **0** |
| `melee_heavy_slash` | 1 | 候选 | Leper/chop=0 | **0** |
| `melee_charge` | 0 | 候选 | Hellion/adrenaline_rush=0 | **-100** |
| `ranged_precise_shot` | 0.9 | 候选 | Arbalest/sniper_shot=0 | **0** |
| `ranged_intimidating_shot` | 0.5 | 无对应 | — | **0** |
| `ranged_retreat` | 0 | 候选 | Hellion/move=None | **-100** |
| `caster_fear_whisper` | 0.8 | 候选 | Occultist/weakening_curse=-75 | **0** |
| `caster_mental_shock` | 0.7 | 候选 | Highwayman/grape_shot_blast=-60 | **0** |
| `move` | 0 | 明确 | Hellion/move=None | **-100** |
