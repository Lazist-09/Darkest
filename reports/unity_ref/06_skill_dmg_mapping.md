# 06 · 技能 dmg% 映射（A2）—— 我方 44 技能 ↔ 参考项目 `.dmg`

> **机器可读版**：`reports/unity_ref/skill_dmg_mapping.json`（同一份数据，逐行 evidence）✓
> **来源**：参考项目 `F:\GithubPro\Darkest-Dungeon-Unity`（用户指令 2026-09-25：数值采用该项目）· 我方 `darkest/data/skills.json` ✓
> **任务**：`reports/unity_ref/PLAN_adoption.md` A2 ✓ · **判据**：`doc/modules/observe_list.md` D5（逐条读 `.effect` 全串）✓
> 🕒 本轮**只读出报告**，未改 `darkest/data/` 或任何其它文件（隔离声明）✓

## 0. 方法与口径（**先读这里**）

```
① 取数：参考 `<Hero>.bytes` 里 `combat_skill: .id X .level 0 ... .dmg V%` 的 V（整数百分比）。
   `.dmg` 在 0~4 级恒定（reports/unity_ref/PLAN_adoption.md §2.3）⇒ 一个技能取一个值 ✓
② 三档（沿用本仓词表，**不是**新造词）：
   明确 = 参考 `.effect` 全串与我方 effects/行为**语义相同** 且 同 .type 且 .launch/.target 覆盖相容
          （必要时用 Mechanics/Effects.txt 把不透明 effect id 解码成具体机制 —— 例如 `Stun 1` = .stun 1）
          ⚠️ 仅名字像（commissar_pistol_shot ↔ pistol_shot）**永不**为明确 ⇒ 一律候选
   候选 = 效果比较未做 / 不确定 / 只有名字像 / 只在跨职业英雄上找到对应 ⇒ **不得落库**（纪律 BL）
   无对应 = 效果串明确不同且全 15 英雄均无对应者
③ 效果解码器：Assets\Resources\Data\Mechanics\Effects.txt（952 个 effect: .name ... 定义）
   形状解码器：FormationSet.cs:33-69（`~`=多目标 `@`=自身阵营 `?`=随机；逐字符 → rank 列表）、CombatSkill.cs:265-340
④ 槽位口径：我方 1~6 槽 vs 参考 1~4 rank ⇒ 只求方向相容，不做数值等价断言
⑤ 5 条 target.side="player" 的技能（melee_heavy_slash / ranged_precise_shot / ranged_intimidating_shot /
   caster_fear_whisper / caster_mental_shock）是 NPC 技能，而参考只有英雄数据 ⇒ 只能取代理值，已在 evidence 标注
⑥ ref_dmg_pct=null 有两种情形：无对应（按 schema）· 或参考该行本就**无 `.dmg` 字段**（纯治疗/纯位移技能）—— 后者不等于无对应
```

## 1. 44 行总表

| 我方 id | 中文名 | owner | ref hero | ref skill | ref `.dmg` | 置信度 | 我方存量 | 一致? |
|---|---|---|---|---|---|---|---|---|
| `warrior_cleave` | 劈砍 | warrior | Hellion | `wicked_hack` | 0% | **明确** | +0 | ✅ true |
| `warrior_sweep` | 横扫 | warrior | Hellion | `iron_swan` | 0% | **候选** | +0 | ✅ true |
| `warrior_lunge` | 突刺 | warrior | Hellion | `breakthru` | -55% | **候选** | -50 | 🔴 false |
| `warrior_javelin` | 投掷短矛 | warrior | GraveRobber | `thrown_dagger` | 0% | **候选** | — | — |
| `warrior_shield_bash` | 盾击 | warrior | Hellion | `barbaric_yawp` | -100% | **候选** | — | — |
| `warrior_war_cry` | 战吼 | warrior | Jester | `battle_ballad` | 0% | **候选** | -100 | 🔴 false |
| `warrior_battle_fury` | 战意 | warrior | Hellion | `adrenaline_rush` | 0% | **明确** | — | — |
| `warrior_catch_breath` | 喘息 | warrior | Abomination | `absolution` | 0% | **候选** | — | — |
| `warrior_last_stand` | 殊死一搏 | warrior | Jester | `heroic_end` | +150% | **候选** | — | — |
| `tank_guard_wall` | 盾墙 | tank | ManAtArms | `defender` | 0% | **明确** | +0 | ✅ true |
| `tank_taunt` | 嘲讽 | tank | — | — | — | **无对应** | — | — |
| `tank_shield_bash` | 盾击 | tank | ManAtArms | `crush` | 0% | **候选** | +0 | ✅ true |
| `tank_heavy_ram` | 重盾猛撞 | tank | ManAtArms | `rampart` | -60% | **候选** | -60 | ✅ true |
| `tank_iron_wall` | 铁壁 | tank | ManAtArms | `bolster` | 0% | **候选** | — | — |
| `tank_war_cry` | 战吼 | tank | ManAtArms | `command` | 0% | **明确** | — | — |
| `tank_hunker` | 坚守 | tank | ManAtArms | `bolster` | 0% | **候选** | — | — |
| `tank_catch_breath` | 喘息 | tank | Abomination | `absolution` | 0% | **候选** | — | — |
| `tank_selfless_charge` | 舍身 | tank | Jester | `heroic_end` | +150% | **候选** | — | — |
| `medic_scalpel` | 手术刀 | medic | PlagueDoctor | `incision` | 0% | **候选** | +0 | ✅ true |
| `medic_cross_slash` | 十字斩 | medic | Vestal | `mace_bash` | 0% | **候选** | — | — |
| `medic_anesthetic` | 麻醉针 | medic | PlagueDoctor | `blinding_gas` | -100% | **明确** | — | — |
| `medic_medicine_flask` | 投掷药瓶 | medic | Vestal | `gods_illumination` | -50% | **候选** | — | — |
| `medic_double_hit` | 双连击 | medic | — | — | — | **无对应** | — | — |
| `medic_field_strike` | 战地搏击 | medic | ManAtArms | `rampart` | -60% | **候选** | — | — |
| `medic_lethal_injection` | 致命注射 | medic | PlagueDoctor | `noxious_blast` | -80% | **候选** | -80 | ✅ true |
| `medic_first_aid` | 急救 | medic | Vestal | `divine_grace` | — | **候选** | — | — |
| `medic_group_bandage` | 群体绷带 | medic | Vestal | `gods_comfort` | — | **候选** | — | — |
| `commissar_command_blade` | 指挥刀 | commissar | Highwayman | `wicked_slice` | +15% | **明确** | +15 | ✅ true |
| `commissar_charge_order` | 冲锋令 | commissar | Highwayman | `wicked_slice` | +15% | **候选** | — | — |
| `commissar_pistol_shot` | 手枪射击 | commissar | Highwayman | `pistol_shot` | -25% | **候选** | — | — |
| `commissar_supervise` | 督战 | commissar | BountyHunter | `target_tag` | -100% | **候选** | — | — |
| `commissar_battle_inspiration` | 战场鼓舞 | commissar | Crusader | `inspiring_cry` | — | **候选** | — | — |
| `commissar_mobilize` | 动员令 | commissar | ManAtArms | `command` | 0% | **候选** | — | — |
| `commissar_execution_order` | 处决令 | commissar | BountyHunter | `collect_bounty` | 0% | **候选** | — | — |
| `commissar_burst_fire` | 连射 | commissar | Highwayman | `grape_shot_blast` | -60% | **候选** | -50 | 🔴 false |
| `commissar_total_mobilization` | 总动员 | commissar | ManAtArms | `bolster` | 0% | **候选** | — | — |
| `melee_heavy_slash` | 重劈 | melee_soldier | Leper | `chop` | 0% | **候选** | — | — |
| `melee_charge` | 突进 | melee_soldier | Hellion | `adrenaline_rush` | 0% | **候选** | — | — |
| `ranged_precise_shot` | 精准射击 | ranged_archer | Arbalest | `sniper_shot` | 0% | **候选** | — | — |
| `ranged_intimidating_shot` | 威吓箭 | ranged_archer | — | — | — | **无对应** | — | — |
| `ranged_retreat` | 后撤 | ranged_archer | Hellion | `move` | — | **候选** | — | — |
| `caster_fear_whisper` | 恐惧低语 | caster | Occultist | `weakening_curse` | -75% | **候选** | — | — |
| `caster_mental_shock` | 精神震荡 | caster | Highwayman | `grape_shot_blast` | -60% | **候选** | — | — |
| `move` | 移动 | —(通用) | Hellion | `move` | — | **明确** | — | — |

## 2. 计数汇总

```
44 行 =  明确 7  ·  候选 34  ·  无对应 3
已存 dmg_pct 的 11 行： agree=true 8 · 🔴 agree=false 3

🔴 agree=false 的 2 类（必须分开看）：
   (a) **数值读错**（存量 ≠ 其所指参考技能的实测值）：warrior_lunge（-50 vs -55）· commissar_burst_fire（-50 vs -60）
   (b) **映射被改指**（存量等于其所指技能的值，但该映射被 `.effect` 判据推翻）：warrior_war_cry（-100 = barbaric_yawp 的值，
       但 barbaric_yawp 的效果串与我方不符 ⇒ 本表改指 Jester/battle_ballad 的 0%）

另有 5 行的**映射未经 effect 确证**（存量值本身与其所指技能一致，但判据不同）：
   warrior_sweep（.target 4 = 仅第 4 位，与我方 AoE 不相交）· tank_shield_bash 与 tank_heavy_ram（互为镜像的交叉映射，见 §6）
   · medic_scalpel（参考带 Minor Bleed，我方无）· medic_lethal_injection（参考为远程中毒，我方为近战流血）
```

## 3. 明确 7 行 —— 逐条证据（file:line / JSON path）

### 3.1 `warrior_cleave`（劈砍）← `Hellion/wicked_hack` · ref `.dmg` = 0%

type/launch/target 三项全同 + 两侧 .effect 皆空 ⇒ 效果同。详证：F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\Hellion.bytes:26 `.type "melee"` = 我方 range_axis=melee（skills.json skills[0].range_axis）；`.launch 21` = {1,2} = 我方 self_slots[1,2]；`.target 12` = {1,2} = 我方 target.slots[1,2]（FormationSet.cs:65-68 逐字符解析）；两侧均无 `.effect` 字段（参考第 26 行整行无 .effect；我方 skills.json:32 `"effects": []`）⇒ 效果同（皆空）；`.dmg 0%` = 存量 0（skills.json:6）⇒ agree=true。

### 3.2 `warrior_battle_fury`（战意）← `Hellion/adrenaline_rush` · ref `.dmg` = 0%

参考 .effect "Adrenaline 1"（自身 +20% 伤害）与我方自身攻击增益同类 ⇒ D5 假设成立。详证：F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\Hellion.bytes:52 `adrenaline_rush`：`.launch 4321` ⊇ 我方 self_slots{1,2} ✓；`.target` 为空 ⇒ FormationSet.cs:46-51 判为自靶（IsSelfTarget=true），与我方 `"scope": "self"`（skills.json:250-252）一致 ✓；`.atk 0% .dmg 0%` ⇒ 非攻击型；`.effect "Adrenaline 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:773 performer：`.cure 1` + attack_rating_add +5% + damage_low/high_multiply +20%）+ `"HellionHealSelf 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:779 performer `.heal 1`）与我方 {stat_mod, apply_to:self, stat:attack, delta:+4, duration_rounds:2}（skills.json:255-263）同为【自身攻击增益】⇒ 效果同 ⇒ 明确。参考 `.dmg 0%`（可按 A2 无条件落库）。

### 3.3 `tank_guard_wall`（盾墙）← `ManAtArms/defender` · ref `.dmg` = 0%

参考 .effect "MAA Guard 1"（guard）+ "Defender 1"（自身 PROT）与我方 guard_attach + 自身防御同类 ⇒ 效果同。详证：F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\ManAtArms.bytes:42 `defender`：`.type "melee"`、`.launch 1234` ⊇ 我方 self_slots{1,2} ✓、`.target @1234`（自身阵营全体）⊇ 我方 target.scope=adjacent_ally_and_self ✓；`.effect "MAA Guard 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:1009 target `.guard 1`、`.duration 2`）= 我方 effects[0] = {type:guard_attach}（skills.json:371-375）✓；`"Defender 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:1011 performer protection_rating_add 25%）= 我方 effects[1] = {stat_mod, apply_to:self, stat:phys_def, delta:+6}（skills.json:376-382）✓ ⇒ 效果同 ⇒ 明确。`.dmg 0%` = 存量 0（skills.json:358）⇒ agree=true。

### 3.4 `tank_war_cry`（战吼）← `ManAtArms/command` · ref `.dmg` = 0%

参考 .effect "Command 1"（全队 +命中/+暴击）与我方全队士气增益同类 ⇒ D5 假设成立。详证：F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\ManAtArms.bytes:53 `command`：`.atk 0% .dmg 0%` ⇒ 非攻击型（与我方无 damage 字段一致）✓；`.launch 1234` ⊇ 我方 self_slots="all" ✓；`.target ~@1234` = 全体友方 = 我方 target.scope=team（skills.json:548-551）✓；`.effect "Command 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:1025 target：attack_rating_add +10% + crit_chance_add +1%）= 【对全队的号令型增益】与我方 morale_effects = [{scope:team, delta:+5}]（skills.json:559-564）同为全队正向增益 ⇒ 明确。⚠️ 载荷不同（参考为命中/暴击，我方为士气）—— 本条是本批 4 条升级里最弱的一条，理由写在下文 §4.2。参考 `.dmg 0%`（可按 A2 落库）。

### 3.5 `medic_anesthetic`（麻醉针）← `PlagueDoctor/blinding_gas` · ref `.dmg` = -100%

参考 .effect "Stun 1"（Effects.txt:143 .stun 1）与我方 stun 同类同机制 ⇒ D5 假设成立。详证：F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\PlagueDoctor.bytes:36 `blinding_gas`：`.type "ranged"` = 我方 range_axis=ranged（skills.json:795）✓；`.launch 43` = {3,4} = 我方 self_slots{3,4}（skills.json:757-760）✓；`.target ~34` = 多目标{3,4} = 我方 target.slots{3,4}（skills.json:761-767）✓；`.effect "Stun 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:143 target `.stun 1`、chance 100%）与我方 effects[0] = {type:stun, probability:30, resist_axis:stun_resist}（skills.json:779-785）**同类同机制** ⇒ 明确。实测参考 `.dmg = -100%`（可按 A2 落库）。

### 3.6 `commissar_command_blade`（指挥刀）← `Highwayman/wicked_slice` · ref `.dmg` = 15%

type/launch/target 三项全同 + 两侧 .effect 皆空 ⇒ 效果同。详证：F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\Highwayman.bytes:27 `wicked_slice`：`.type "melee"` = 我方 range_axis=melee（skills.json:1062）✓；`.launch 321` ⊇ {1,2} ✓；`.target 12` = {1,2} = 我方 target.slots{1,2}（skills.json:1036-1042）✓；**整行无 `.effect` 字段** = 我方 `"effects": []`（skills.json:1054）✓ ⇒ 效果同（皆空）⇒ 明确。实测 `.dmg = 15%` = 存量 +15（skills.json:1028）⇒ agree=true。

### 3.7 `move`（移动）← `Hellion/move` · ref `.dmg` = null（参考无 .dmg 字段）

同名同类型（.type "move"）+ 两侧无 .effect、无 .dmg ⇒ 效果同（纯位移）。详证：我方 id=move（skills.json:1678-1702：`pool_external: true`、target.scope="move_range"、无 damage/无 effects）与参考每条英雄记录里的 `combat_move_skill: .id "move"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\Hellion.bytes:46、ManAtArms.bytes:47、PlagueDoctor.bytes:46、Highwayman.bytes:47；15 英雄同构，见 reports/unity_ref/hero_skills_from_ref.json 的 `combat_move_skill` 记录）**同名同类型**：`.type "move"` = 我方通用位移 ✓、`.launch 4321` ⊇ 我方 self_slots{1,2,3,4}（skills.json:1681-1686）✓、无 `.effect` ✓、无 `.dmg` ✓ ⇒ 明确。⚠️ 参考无 `.dmg` 字段（非伤害技能）⇒ ref_dmg_pct=null。

## 4. 候选 34 行 —— 缺什么（**每一行都不得落库**）

### 4.1 逐行「缺什么」

| 我方 id | ref | 缺的那一项 |
|---|---|---|
| `warrior_sweep` | `iron_swan` | `.target 4` 是**单个 rank 4**，与我方 target.slots{1,2,3} 交集为空（目标覆盖面不符） |
| `warrior_lunge` | `breakthru` | `.effect "Hellion Exhaust"` = 自身减益，我方是**对敌流血**（效果不同）＋数值 -50 ≠ -55 |
| `warrior_javelin` | `thrown_dagger` | 本职业（Hellion）**无远程技能**；跨职业 ref 带自身增益，我方空效果 |
| `warrior_shield_bash` | `barbaric_yawp` | 参考多一个 `Hellion Exhaust`（自身减益）；且参考是 -100% 群体喊叫 vs 我方 0.6 倍单体 |
| `warrior_war_cry` | `battle_ballad` | 原映射 barbaric_yawp 的形状（敌方群体）与效果（眩晕+自身减益）**都与**我方全队士气相反 ⇒ 改指跨职业 Jester/battle_ballad |
| `warrior_catch_breath` | `absolution` | 本职业无自愈技能；跨职业 ref 为 Abomination（机制同：自身回血+减压，但英雄不同） |
| `warrior_last_stand` | `heroic_end` | bleed_out 的 `Strong Bleed`/`Hellion Exhaust` 与我方空效果不同；跨职业线索 heroic_end 的机制（自身减益）也不同 |
| `tank_shield_bash` | `crush` | crush **整行无 .effect**，而我方有 stun ⇒ 效果不同；且与 tank_heavy_ram 存在交叉映射（§6） |
| `tank_heavy_ram` | `rampart` | rampart 带 `Push 1A` + `Stun 1`，我方无效果、无位移 ⇒ 效果不同；且与 tank_shield_bash 存在交叉映射（§6） |
| `tank_iron_wall` | `bolster` | DD1 **无护盾/吸收机制**（Effects.txt 关键字 absorb 0 命中）⇒ 效果比较不确定（我方 shield vs 参考 +5% 闪避/+2 速度） |
| `tank_hunker` | `bolster` | 参考 bolster 作用域是全队、我方是自身；且 DD1 无 `phys_def` 等价量（PLAN_adoption §7.2） |
| `tank_catch_breath` | `absolution` | 本职业无自愈技能；跨职业 ref 为 Abomination |
| `tank_selfless_charge` | `heroic_end` | retribution 的核心是 `MAA Riposte`（自身反击），我方无；跨职业线索 heroic_end 机制不同 |
| `medic_scalpel` | `incision` | incision 带 `Minor Bleed 1`（Effects.txt:111），我方 `effects: []` ⇒ 效果不同 |
| `medic_cross_slash` | `mace_bash` | 本职业（PD）无"无效果近战"技能；跨职业 ref 为 Vestal（英雄不同） |
| `medic_medicine_flask` | `gods_illumination` | plague_grenade 是**中毒 DoT**，我方是韧性减益 ⇒ 原假设（名字像陷阱）被推翻，只能退到跨职业 gods_illumination |
| `medic_field_strike` | `rampart` | 本职业无位移技能；跨职业 ref（rampart）多一个 `Stun 1`，与我方"仅 push"不完全同 |
| `medic_lethal_injection` | `noxious_blast` | noxious_blast 为**远程中毒**，我方为**近战流血**（.type/.launch/.effect 三处不符，仅数值一致） |
| `medic_first_aid` | `divine_grace` | 参考为 Vestal（跨职业）；且参考该行**无 `.dmg` 字段** ⇒ 取不到数值 |
| `medic_group_bandage` | `gods_comfort` | 同上（跨职业 + 参考无 `.dmg` 字段） |
| `commissar_charge_order` | `wicked_slice` | duelist_advance 的核心是 `Hwy Riposte`（自身反击），我方无 ⇒ 退到本职业的 wicked_slice（与我方另一条共用 ref） |
| `commissar_pistol_shot` | `pistol_shot` | 参考多一条 `Highwayman Pistol Dmg Marked`（对已标记目标 +25%）条件附加（且名字像 ⇒ 永不明确） |
| `commissar_supervise` | `target_tag` | take_aim 是**自身增益**，我方是对敌标记+韧性减益（方向相反）⇒ 退到跨职业 BountyHunter/target_tag |
| `commissar_battle_inspiration` | `inspiring_cry` | 参考为 Crusader（跨职业）；且参考该行**无 `.dmg` 字段** |
| `commissar_mobilize` | `command` | 参考为 MaA（跨职业），且与 tank_war_cry 共用同一 ref（我方两条 ≈ 参考一条 command） |
| `commissar_execution_order` | `collect_bounty` | opened_vein 的核心载荷是 `Bleed`，我方是 `missing_hp` 斩杀 + 对标记增伤 ⇒ 退到跨职业 collect_bounty |
| `commissar_burst_fire` | `grape_shot_blast` | `.launch 23`/`.target ~123` 与我方 {3,4} 只重叠 1 位；且我方是**两段**伤害，DD1 无多段概念 ＋数值 -50 ≠ -60 |
| `commissar_total_mobilization` | `bolster` | 参考 bolster 不含伤害，我方是"攻击 + 全队增益"，只对上后半 |
| `melee_heavy_slash` | `chop` | 我方是 NPC 技能、参考只有英雄数据（错位） |
| `melee_charge` | `adrenaline_rush` | NPC 错位；且参考里"攻击增益"与"自身前移"分属不同技能 |
| `ranged_precise_shot` | `sniper_shot` | NPC 错位；参考 sniper_shot 带"对已标记增伤"且 `.launch 43` ⊊ 我方{2,3,4} |
| `ranged_retreat` | `move` | 参考的 `move` 是**双向可选**的通用位移，我方是本条固定"后撤"+cooldown 2 |
| `caster_fear_whisper` | `weakening_curse` | NPC 错位 + damage_axis=mental 在 DD1 无对应概念；参考减伤害、我方减韧性 |
| `caster_mental_shock` | `grape_shot_blast` | NPC 错位 + mental 轴无对应；跨职业 ref 的 `.launch 23` 与我方{2,3,4} 只部分重叠 |

### 4.2 三条「明确」的风险自标（本代理主动标出）

```
· `tank_war_cry` ← `command`：`.effect "Command 1"` = +10% 命中 / +1% 暴击（Effects.txt:1025），我方载荷是【全队士气 +5】
  ⇒ 范围（全队）与正负号同，**具体属性不同**。升明确的理由是"同为号令型全队增益"，属本批最弱。
· `warrior_battle_fury` ← `adrenaline_rush`：`.effect "Adrenaline 1"` = 自身 +5% 命中 / +20% 伤害 + cure（Effects.txt:773）
  ⇒ 与我方【自身攻击 +4 / 2 回合】同类；且参考 `.atk 0% .dmg 0%`（非攻击型）与我方无 damage 字段一致 ⇒ 比上一条强。
· 另：`move` ← 参考 `combat_move_skill: move` 是同名同类型，属明确，但参考无 `.dmg` ⇒ ref_dmg_pct=null。
```

## 5. D5 十二条逐条裁定（**引用参考 `.effect` 全串**）

> 台账：`doc/modules/observe_list.md:217-246`（D5）· 方法：逐条读 `.effect` 全串，同 ⇒ 升"明确"，不同 ⇒ 归"无对应"
> 下表 `D5 裁定` 针对**策划假设的那一条**；`本表最终` 是本代理按同一判据给的落点（若与假设不同，会写明改指）。

| # | 我方 | D5 假设参考 | 参考 `.effect` 全串 | 我方效果 | D5 裁定 | 本表最终 |
|---|---|---|---|---|---|---|
| 1 | `medic_anesthetic` | `blinding_gas` | `"Stun 1"` | `{type:stun, probability:30}` | **升明确** | 明确 ← blinding_gas（-100%） |
| 2 | `medic_medicine_flask` | `plague_grenade` | `"PD Blight 1"` | `{stat_mod, resilience:-15}` | **降无对应** | 候选 ← Vestal/gods_illumination（-50%，跨职业） |
| 3 | `tank_taunt` | `bellow` | `"Disrupt 1"` | `{type:taunt}` | **降无对应** | 无对应（全 15 英雄无对应；Effects.txt 无 taunt） |
| 4 | `tank_war_cry` | `command` | `"Command 1"` | `morale team +5` | **升明确** | 明确 ← command（0%） |
| 5 | `tank_iron_wall` | `bolster` | `"Bolster 1"` | `{type:shield, charges:2}` | 不升级（效果不确定） | 候选 ← bolster（0%）—— **bolster 实测确有 .dmg 0%** |
| 6 | `tank_selfless_charge` | `retribution` | `"MAA Riposte 1" "Mark Self"` | `effects: [] + self_damage 8` | **降无对应** | 候选线索 ← Jester/heroic_end（+150%，跨职业） |
| 7 | `warrior_battle_fury` | `adrenaline_rush` | `"Adrenaline 1" "HellionHealSelf 1"` | `{stat_mod, attack:+4}` | **升明确** | 明确 ← adrenaline_rush（0%） |
| 8 | `warrior_last_stand` | `bleed_out` | `"Strong Bleed 1" "Hellion Exhaust"` | `effects: [] + self_damage 6` | **降无对应** | 候选线索 ← Jester/heroic_end（+150%，跨职业） |
| 9 | `commissar_pistol_shot` | `pistol_shot` | `"Highwayman Pistol Dmg Marked"` | `effects: []` | 不升级（名字像 ⇒ 候选） | 候选 ← pistol_shot（-25%） |
| 10 | `commissar_charge_order` | `duelist_advance` | `"Hwy Riposte 1"` | `effects: []` | **降无对应** | 候选 ← Highwayman/wicked_slice（+15%，本职业） |
| 11 | `commissar_execution_order` | `opened_vein` | `"Bleed 1" "Bleed Debuff 1"` | `effects: [] + missing_hp + 对标记 +25%` | **降无对应** | 候选 ← BountyHunter/collect_bounty（0%，跨职业） |
| 12 | `commissar_supervise` | `take_aim` | `"Highwayman Buff 1"` | `{stat_mod, resilience:-10} + {mark}` | **降无对应** | 候选 ← BountyHunter/target_tag（-100%，跨职业） |

```
📊 D5 结论：**升明确 3 条**（medic_anesthetic · tank_war_cry · warrior_battle_fury）
          **降无对应 7 条**（medic_medicine_flask · tank_taunt · tank_selfless_charge · warrior_last_stand ·
                            commissar_charge_order · commissar_execution_order · commissar_supervise）
          **保留候选 2 条**（tank_iron_wall：DD1 无护盾机制 ⇒ 效果比较不确定；
                            commissar_pistol_shot：技能名完全相同 ⇒ 按架构判据"名字像只能进候选"，本代理**不**把它升为明确）
🔴 12 条里 10 条给出二选一裁定；剩 2 条按判据本身只能落在"候选"（理由逐条写在 JSON 的 evidence 里，未硬凑二选一）
📌 三条被推翻的"名字像/语义近似"假设都保留了实测值：bellow **-90%** · bleed_out **+15%** · pistol_shot **-25%**
```

## 6. 🔴 三处与既有文档不一致 / 需人工裁定（**本代理实测**）

```
① 🔴 数值：`doc/modules/dd1_baseline.md §43.4`（`warrior_lunge` 行）把 breakthru 记 **-50%**，实测 **-55%**（Hellion.bytes:47）；
   `§43.5`（`commissar_burst_fire` 行）把 grape_shot_blast 记 **-50%**，实测 **-60%**（Highwayman.bytes:42）
   ⇒ 我方存量 -50（warrior_lunge）与 -50（commissar_burst_fire）都偏了 5~10 个点（`PLAN_adoption §3` 的表里没有这两条）
② 🔴 `tank_iron_wall` ← `bolster`：§43.3 记 "(—)"、PLAN_adoption §3 记 "bolster 无 .dmg" —— **两者都不准确**：
   ManAtArms.bytes:58 整行为 `.type "ranged" .atk 0% .dmg 0% .crit 0% .launch 1234 .target ~@1234 .is_crit_valid True .effect "Bolster 1"`
   ⇒ 有 `.dmg 0%` ✓（已按要求实测核实）
③ 🔴 **交叉映射（[推断]，需人工裁定）**：tank 的两条伤害技能按 **效果** 口径与按 **技能名** 口径给出相反配对 ——
   名字口径（§43 现用）：tank_shield_bash ← crush(0%) · tank_heavy_ram ← rampart(-60%)
   效果口径（本代理）：MaA 只有 rampart 带 `Stun 1`（Effects.txt:143）⇒ **带眩晕的 tank_shield_bash 应指 rampart(-60%)**；
                        crush 整行无 `.effect` ⇒ **无效果的 tank_heavy_ram 应指 crush(0%)** ⇒ 两值互换
   旁证：伤害排序一致 —— 参考 rampart(-60%) < crush(0%)，我方 shield_bash(0.7 倍) < heavy_ram(0.9 倍)
   ⇒ 本表**两行都保持候选、不入库**，把两种读法都写进 evidence，等人工裁定（不自行改数）
```

## 7. 三个「已知猜错」的实测值（**measure 出来，不是抄提示词**）

```
| 我方技能 | 策划撤出时的猜测 | 🔴 实测参考值 | 依据（file:line） |
|---|---|---|---|
| tank_taunt | -100（撤出前的猜测；现库存**无**该值） | **bellow = -90%** | ManAtArms.bytes:37 `.dmg -90%` |
| warrior_last_stand | +20（撤出前的猜测；现库存**无**该值） | **bleed_out = +15%** | Hellion.bytes:57 `.dmg 15%` |
| commissar_pistol_shot | -15（撤出前的猜测；现库存**无**该值） | **pistol_shot = -25%** | Highwayman.bytes:32 `.dmg -25%` |

📌 与 `PLAN_adoption §3` 的 -90 / +15 / -25 三个数**一致**（该表的三处"猜错"结论成立）✓
⚠️ 但要注意：这 3 条**本来就没有存量值**（它们是 12 条撤出候选里的 3 条）⇒ 它们不属于"11 条已存值"的范畴；
   11 条已存值里真正数值读错的是 **warrior_lunge（-50 vs -55）** 与 **commissar_burst_fire（-50 vs -60）** 两条。
```

## 8. 无对应 3 行

### `tank_taunt`（嘲讽）

【D5 假设：bellow（策划撤出时猜 -100）⇒ 降为无对应】F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info\ManAtArms.bytes:37 `bellow` 实测 `.dmg = **-90%**`（猜 -100 ⇒ 与实测不符；该行另有 `.type "ranged"`、`.atk 90%`、`.launch 123`、`.target ~1234`）；其 `.effect "Disrupt 1"`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:1002 target：defense_rating_add -5% + speed_rating_add -5）= 【敌方闪避/速度减益】，与我方 effects[0] = {type:taunt, duration_rounds:2}（skills.json:412-417）明确不同 ⇒ 假设不成立。🔴 并且 F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt 全表 952 个 effect 定义中关键字 `taunt` **0 命中** ⇒ DD1 无 taunt 机制；最接近的"吸引攻击"手段是 `.tag 1`（Mark，F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:320 "Mark Target" / :324 "Mark Self"），但那是【被标记】而非我方 taunt ⇒ 全 15 英雄无对应 ⇒ 无对应（ref=null）。📌 实测假设技能值 **-90%** 已记录（本行不落库）。

### `medic_double_hit`（双连击）

我方为**两段**伤害：damage.segments = 两个 {type:flat, multiplier:0.5}（skills.json:860-871）。参考侧一个技能只有**一个** `.dmg` 修正 —— `Assets\\Scripts\\Mechanics\\Skills\\CombatSkill.cs:283-291` 把 `.dmg` 读成单值 `DamageMod`（`DamageMod = float.Parse(data[++i]) / 100`），且 15 英雄 × 7 技能 × 5 级共 525 条记录的字段集（`reports/unity_ref/hero_skills_from_ref.json`：atk/crit/dmg/effect/heal/launch/move/per_turn_limit/per_battle_limit/target/type/...）**无任何多段字段** ⇒ DD1 无"多段攻击"概念 ⇒ 无对应。

### `ranged_intimidating_shot`（威吓箭）

我方 payload 是 morale_effects = [{scope:targets, delta:-4}]（skills.json:1541-1546）= 【对敌士气/压力减益】，另有 damage_axis="mental"（skills.json:1552）。实测：15 英雄的 `Heroes/Info/*.bytes` 里**没有任何英雄技能对敌施加压力** —— 关键字 `stress` 的全量扫描只命中 Abomination 的 `beast_stress_party` / `human_stress_heal_party`（Abomination.bytes:27-31）、Crusader `inspiring_cry` 的 `Crusader HealStress 1`（Crusader.bytes:57-61）、Leper `withstand` 的 `LeperHealSelfStress 1`（Leper.bytes:47-51）—— **全为友方减压**；F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt 的 `Stress 1/2/3`（:63-65，`.stress 10/15/25`）也只被怪物侧使用；英雄侧最接近的 `Distract Enemies 1`（F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt:921）是 attack_rating_add -10% 而非压力。⇒ 无对应。⚠️ 我方的 `damage_axis: "mental"` 在 DD1 也无对应概念（DD1 伤害只有武器伤害 + DoT，压力是独立轨道）。

## 9. 落库建议（**本轮不动数据**）

```
✅ 可落（明确 7 条，其中 4 条带数值）：
   warrior_cleave    0     （= 现有存量 0，不变）
   warrior_battle_fury 0   （新增；D5 升级）
   tank_guard_wall   0     （= 现有存量 0，不变）
   tank_war_cry      0     （新增；D5 升级）
   medic_anesthetic  -100  （新增；D5 升级）
   commissar_command_blade +15 （= 现有存量 15，不变）
   move              —     （参考无 .dmg 字段 ⇒ 无可落之值）
🔴 需先改数（数值读错）：warrior_lunge -50 → **-55** · commissar_burst_fire -50 → **-60**
⚠️ 需先裁定（映射存疑，暂不落）：warrior_war_cry（现 -100 无依据）· tank_shield_bash / tank_heavy_ram（交叉映射）·
   medic_scalpel · medic_lethal_injection · warrior_sweep（映射未经 effect 确证，值本身与所指技能一致）
🔴 候选 34 条一律**不得**落库（纪律 BL）
```

## 10. 落库投影（对本仓既有落库器 `tools/dsh/land_ref_skill_dmg.py` 的预期读数）

> 该脚本已存在（`tools/dsh/land_ref_skill_dmg.py:14-20`），规则是「**只有 `confidence="明确"` 且 `ref_dmg_pct` 是整数才落**；
> **库里已有值但映射非明确 ⇒ 一律撤出**」。下面是按本表的只读投影（本代理**未**运行落库、未改 `darkest/data/`）✓

```
rows=44 clear=7 candidate=34 none=3
with dmg_pct: before=11 -> after=6 (landed=6 corrected=0 kept=3 withdrawn=8)

✅ 落库（6 条）：warrior_cleave=+0 · warrior_battle_fury=+0 · tank_guard_wall=+0 · tank_war_cry=+0 · medic_anesthetic=-100 · commissar_command_blade=+15
   （其中 kept=3：warrior_cleave/tank_guard_wall/commissar_command_blade 三条值不变）
🔴 撤出（8 条，映射非明确）：warrior_sweep(旧 +0) · warrior_lunge(旧 -50) · warrior_war_cry(旧 -100) · tank_shield_bash(旧 +0) · tank_heavy_ram(旧 -60) · medic_scalpel(旧 +0) · medic_lethal_injection(旧 -80) · commissar_burst_fire(旧 -50)
⚠️ 副作用：warrior_lunge(-50) 与 commissar_burst_fire(-50) 的**数值错误**在这一版规则下表现为"撤出"而不是"纠正"
   ⇒ 修好映射（或人工裁定交叉映射）之后才会以正确值 -55 / -60 回来 ⇒ 见 §6
```

