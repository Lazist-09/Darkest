# 策划请求 · 技能 `dmg%` 的 30 行【无参考值可落】—— 请逐行裁定

> 起因：主程序 `#473` 指令 —— **数值一律采用 `F:\GithubPro\Darkest-Dungeon-Unity`** ✅
> 本件是执行该指令时**顶不动**的那一段：参考项目**没有**这 30 条技能对应的数值可抄 ✓
> 全部读数可复跑：`python tools/dsh/land_ref_skill_dmg.py --check`（**只读，不写数据**）

---

## 0. 一句话

**44 条技能里 14 条的 `dmg%` 已按参考项目落库；剩 30 条参考项目"答不上来"** ——
不是没找到，是**参考项目里根本没有能对上的技能 `.dmg`**（或能对上的那条也被证伪）。
⇒ 需要你二选一：**① 认可我提的默认规则**（一次裁完 30 条）；或 **② 逐行给出你定的值**。

---

## 1. 已经落库的 14 条（不用你管，列出来是为了让你看到口径）

| 类别 | 条数 | 值 | 依据 |
|---|---:|---|---|
| 映射【明确】+ 参考有整数值 | **6** | `warrior_cleave 0` · `warrior_battle_fury 0` · `tank_guard_wall 0` · `tank_war_cry 0` · `medic_anesthetic **−100**` · `commissar_command_blade **+15**` | 逐条比对参考技能 `.effect` **全串**（方法见 `reports/unity_ref/06_skill_dmg_mapping.md` §3）|
| 库里已有值，且**我们自己的注记**就点名了参考技能，实测**相等** ⇒ 保留 | **6** | `warrior_sweep 0`（←`iron_swan`）· `warrior_war_cry **−100**`（←`barbaric_yawp`）· `tank_shield_bash 0`（←`crush`）· `tank_heavy_ram **−60**`（←`rampart`）· `medic_scalpel 0`（←`incision`）· `medic_lethal_injection **−80**`（←`noxious_blast`）| 数值 = 该参考技能 `.dmg` 的**实测值** |
| 同上但实测**不等** ⇒ **纠正** | **2** | `warrior_lunge` −50 ⇒ **−55**（←`breakthru`）· `commissar_burst_fire` −50 ⇒ **−60**（←`grape_shot_blast`）| `Hellion.bytes:47` · `Highwayman.bytes:42` 实测 |

🔴 **这 14 条现在是【零行为】**：伤害路径（P7 / M1c 阶段 3）**还没读** `dmg_pct` ⇒ 游戏里一点没变 ✓

---

## 2. 为什么另外 30 条落不了（**实测，不是我懒**）

三条硬事实：

```
① 参考项目每个英雄只有 **7** 个技能（15 英雄 × 7 = 105 条技能记录）；
   我方 4 个主职 **9×4 = 36** 条 + 3 个 NPC 单位 **2+3+2 = 7** 条 + 通用 `move` **1** 条 = 44 条
   ⇒ **条数就对不上**（36 ≠ 28），必然有落不进去的 ✓
② 参考的 `.dmg` **不是"默认 0"**：105 条里 0% 只占 **39 条**，其余是
   −100 ×11 · −90 ×5 · −85 ×1 · −80 ×4 · −75 ×6 · −67 ×2 · −60 ×4 · −55 ×1 · −50 ×6 · −40 ×3 ·
   −35 ×1 · −33 ×2 · −25 ×3 · −20 ×2 · −15 ×1 · −10 ×1 · +15 ×2 · +40 ×1 · +50 ×1 · +150 ×1 · 无字段 ×8
   ⇒ **同一个英雄的 7 个技能里就有 3~7 个不同的 `.dmg`**（实测：0 个英雄是"整组同值"）
   ⇒ 所以"随便挑一个算 0"**会错**：挑错一条，伤害能差 **250 个百分点**（−100 ↔ +150）✓
③ 参考项目**没有**这些概念 ⇒ 我方这些技能在它那儿**原理上不存在**：
   `taunt`（Effects.txt 全表 952 条 effect 里 `taunt` **0 命中**）· 多段攻击（参考 15×7×5 = **525** 条
   记录的字段集里没有任何多段字段）· 对敌施压（英雄侧 keyword `stress` 只命中 3 处，**全是友方减压**）✓
```

---

## 3. 请裁的 30 行（**按"值有没有用"分成两堆，只有第一堆真需要你想**）

### 3.1 🔴 需要你定值：**17 行**（我方有伤害段 ⇒ `dmg%` 会真的改变伤害）

| 我方技能 | 我方Σ段倍率 | 当前映射判定 | 参考侧最接近的一条（实测值） | 🔴 若采用 | 备注 |
|---|---:|---|---|---:|---|
| `warrior_last_stand` | 2 | 候选 | `Jester/heroic_end` = **+150** | 0 | 差 150 点，最大的一条 |
| `tank_selfless_charge` | 1.8 | 候选 | `Jester/heroic_end` = **+150** | 0 | 同上 |
| `commissar_total_mobilization` | 1.5 | 候选 | `ManAtArms/bolster` = 0 | 0 | |
| `medic_cross_slash` | 1.2 | 候选 | `Vestal/mace_bash` = 0 | 0 | |
| `warrior_javelin` | 1 | 候选 | `GraveRobber/thrown_dagger` = 0 | 0 | |
| `medic_double_hit` | 1 | 无对应 | —（参考无多段概念）| 0 | |
| `medic_field_strike` | 1 | 候选 | `ManAtArms/rampart` = **−60** | 0 | |
| `commissar_charge_order` | 1 | 候选 | `Highwayman/wicked_slice` = **+15** | 0 | |
| `melee_heavy_slash` | 1 | 候选 | `Leper/chop` = 0 | 0 | |
| `commissar_pistol_shot` | 0.95 | 候选 | `Highwayman/pistol_shot` = **−25** | 0 | **名字完全相同**；但参考那条的 `.effect` = "Highwayman Pistol Dmg Marked"（对**被标记**目标加伤），我方 `effects: []` ⇒ 判"名字像陷阱" |
| `medic_medicine_flask` | 0.9 | 候选 | `Vestal/gods_illumination` = **−50** | 0 | 策划 `#461④` 亲口点过的"名字像陷阱" |
| `commissar_supervise` | 0.9 | 候选 | `BountyHunter/target_tag` = **−100** | 0 | |
| `ranged_precise_shot` | 0.9 | 候选 | `Arbalest/sniper_shot` = 0 | 0 | |
| `caster_fear_whisper` | 0.8 | 候选 | `Occultist/weakening_curse` = **−75** | 0 | |
| `caster_mental_shock` | 0.7 | 候选 | `Highwayman/grape_shot_blast` = **−60** | 0 | |
| `warrior_shield_bash` | 0.6 | 候选 | `Hellion/barbaric_yawp` = **−100** | 0 | |
| `ranged_intimidating_shot` | 0.5 | 无对应 | —（参考英雄侧无对敌施压）| 0 | |

**我提的默认规则（一行话）**：
> 我方技能**有伤害**（Σ段倍率 > 0）而参考项目**没有可确证的对应** ⇒ `dmg% = 0`
> （= "普通武器伤害"，参考项目 105 条里最常见的一档 39/105 ✓）

🔴 **反例警告**：这张表里有 5 行的"最接近候选"是**非零**（−100 / −75 / −60 / −50 / −25 / +15 / +150）——
若你认为其中某几行**确实**该用候选值，请**点名哪几行**；否则一律 0 ✓

### 3.2 值**无所谓**：**13 行**（我方 Σ段倍率 = 0 ⇒ 是新模型的公式让伤害必为 0，写几都不影响）

```
公式（已落在代码里，tools 之外）：伤害 = Σ段倍率 × 武器区间 × (1 + dmg%/100)
                                  ↑ 这一项 = 0 ⇒ 乘任何 dmg% 都是 0 ✓
```

| 我方技能 | 当前映射判定 | 参考候选 |
|---|---|---|
| `tank_taunt` | 无对应 | —（参考无 `taunt`）|
| `warrior_catch_breath` · `tank_catch_breath` | 候选 | `Abomination/absolution` = 0 |
| `tank_iron_wall` · `tank_hunker` | 候选 | `ManAtArms/bolster` = 0 |
| `medic_first_aid` | 候选 | `Vestal/divine_grace` = 无 `.dmg` |
| `medic_group_bandage` | 候选 | `Vestal/gods_comfort` = 无 `.dmg` |
| `commissar_battle_inspiration` | 候选 | `Crusader/inspiring_cry` = 无 `.dmg` |
| `commissar_mobilize` | 候选 | `ManAtArms/command` = 0 |
| `commissar_execution_order` | 候选 | `BountyHunter/collect_bounty` = 0 |
| `melee_charge` | 候选 | `Hellion/adrenaline_rush` = 0 |
| `ranged_retreat` | 候选 | `Hellion/move` = 无 `.dmg` |
| `move`（通用移动）| **明确** | `Hellion/move` = 无 `.dmg`（参考**整行没有** `.dmg` 字段）|

**我提的默认规则**：这 13 行**照参考的写法记 `−100`**（参考对"纯功能技能"就是这么写的：
105 条里 11 条是 −100，**每一条都是纯功能**）⇒ 好处是**形状与参考一致**，将来谁读数据都看得懂；
**即使写 0 也不会改变任何伤害** ✓

---

## 4. 两个"顺带发现"，请你顺便裁（不影响本次落库）

```
① 🔴 我方的 `Σ段倍率` 与参考的 `.dmg` 是【两根不同的轴】，而参考只有后一根：
   参考：伤害 = 武器区间(按英雄阶) × (1 + 该技能 .dmg) —— 技能之间**仅**靠 `.dmg` 区分强弱
   我方：伤害 = 武器区间 × **Σ段倍率**(0.6/0.9/1.2/1.5/1.8/2 各技能不同) × (1 + dmg%)
   ⇒ 若"数值全面采用参考"，那**技能间的强弱就该只由 `dmg%` 表达** ⇒ 我方 Σ段倍率**是否该全部归 1**？
      （**这是模型问题，不是数值问题** ⇒ 我同时问了架构；你只需裁"技能强弱该由哪个字段表达"）
② 🔴 `tank` 的两条伤害技能存在**交叉映射**，两种读法给出的配对相反（`reports/unity_ref/06_skill_dmg_mapping.md` §6③）：
   按**名字**：`tank_shield_bash ← crush(0%)` · `tank_heavy_ram ← rampart(−60%)`
   按**效果**：带眩晕的 `tank_shield_bash` 应指 `rampart(−60%)`（MaA 只有它有 `Stun 1`）；
             无 `.effect` 的 `tank_heavy_ram` 应指 `crush(0%)` ⇒ **两值互换**
   ⇒ 我方现状用的是**名字**口径（0 / −60，实测与参考相符 ⇒ 本轮**没动**）✓ 请裁哪个口径对
```

---

## 5. 我要你回什么（**可执行的步骤串**）

```
① 3.1 那 17 行：写「默认规则可行」 —— 或点名哪几行改用候选值（格式：`技能名 = 值`）
② 3.2 那 13 行：写「−100」还是「0」（二者伤害相同，只影响数据可读性）
③ 第 4 节 ① ：技能强弱由 `dmg%` 单字段表达（Σ段倍率归 1）——  是 / 否
④ 第 4 节 ② ：tank 两条交叉映射取 名字口径 / 效果口径
```

⇒ 你回完，我把 30 行一次性落库（**不再逐条问你**），并把本轮 14 条 + 新增 30 条一起做
   前后读数与提交号；**P7（M1c 换伤害模型）就等这一个裁定** ✅

---

## 6. 诚实边界（我能验 / 不能验）

```
✅ 能验（已实测、可复跑）：
   · 44 条技能的参考对应、参考 `.dmg` 实测值、`.effect` 全串比对 —— 见
     `reports/unity_ref/skill_dmg_mapping.json`（44 行逐条带 file:line 证据）
   · 105 条参考技能的 `.dmg` 分布 —— 见本件第 2 节 ②
   · "0 个英雄整组同值" —— 复跑 `reports/unity_ref/_probe/a2_robust.py`
   · 落库前后读数 —— `python tools/dsh/land_ref_skill_dmg.py --check`
⚠️ 不能验（我没做的，别当我做了）：
   · 我没读参考的 **C# 运行逻辑**去反推"策划当年为什么给这个技能 −60" ⇒ `.dmg` 的**设计意图**是推的
   · `Heroes/Info/*.bytes` 是**参考项目自己的副本**，不是原版 `.darkest`；
     本件所有值都出自**参考项目**（这是 `#473` 指定的判据源 ✓）⇒ 不再回原版对账
   · Σ段倍率归 1 会造成多大的平衡位移 —— **没测**（要等第 4 节 ① 裁完才能测）
```
