# 参考项目采用计划（`F:\GithubPro\Darkest-Dungeon-Unity`）

> 🕒 **建立：2026-09-26** · **依据：用户直接指令**「**不论所有问题、数值、逻辑，都先学习 Darkest-Dungeon-Unity 的逻辑代码和数值；数值就采用这个项目的**」✓
> 🔴 **这条指令【改判了来源优先级】**：我方 `dd1_baseline §32.1/§32.3` 原定
>    「**一手（E 盘原版）> 二手（wiki）> 第三方（参考项目）**；冲突以一手为准并记录」✓
>    ⇒ ✅ **现在：数值一律【采用参考项目】** ✓
>    📌 **后果要说清**：`reports/edrive_vs_reference_hero_tables.md` 里那 **97 条冲突**
>       （例如 `hellion.weapon[*].crit` 一手 5/6/7/8/9 vs 参考 2.5/3/3.5/4/4.5）
>       ⇒ **按新指令取【参考值】** ⇒ 该报告从"冲突清单"变成"**待改清单**" ✓
>    ⚠️ **GPL 义务不变**：`doc/assets_credits.md §10` 的登记仍然有效（不逐行誊写代码；不分发 ⇒ 主要义务不触发）✓

---

## 0. 一句话

```
参考项目的数据量级**全面大于我方**（buff 1801 vs 22 · 怪物 230 vs 7 · AI 160 vs 3 · 饰品 488 vs 196），
且它的**技能伤害百分比字段 `.dmg`（485 条）语义与我方 M1c 要换的公式【同构】**：
   参考 `BattleSolver.cs:444` = `MinDamage * (1 + DamageMod)`，`CombatSkill.cs:285` = `DamageMod = v/100`
   ⇒ ✅ **与我方 P7 的目标公式 `damage = 武器区间 × (1 + 技能 dmg%)` 完全一致** ✓
⇒ 🔴 **采用它 = 同时解决「数值来源」与「逻辑对齐」两件事** ✓
```

## 1. 数据在哪（参考项目）

| 位置 | 内容 | 量级 |
|---|---|---|
| `Assets\Resources\Data\Heroes\Info\*.bytes` | **DD1 `.info.darkest` 文本**：`combat_skill:` 两块（表现块 + **数值块 `.level 0..4`**）· `weapon:` ×5 阶 · `armour:` ×5 阶 | 15 英雄 · **525 数值记录** |
| `Assets\Resources\Data\Upgrades\Heroes\*.upgrades.json` | 英雄技能升级树（`trees`/`requirements`/`currency_cost`/`prerequisite_requirements`/`prerequisite_resolve_level`） | 16 文件 · 各 ~21KB |
| `Assets\Resources\Data\JsonBuffs.json` | **buff 原语池** | **1801** 条 |
| `Assets\Resources\Data\JsonAI.json` | 怪物 AI（欲望/权重） | **160** brain |
| `Assets\Resources\Data\JsonQuirks.json` / `JsonTraits.json` | 怪癖（含疾病）／折磨+美德 | **163** / **12** |
| `Assets\Resources\Data\JsonTrinkets.json` | 饰品（**数值全靠 buff id 数组**） | **488** |
| `Assets\Resources\Data\JsonQuests.json` / `JsonLoot.json` / `JsonCamping.json` | 任务／战利品表／扎营 | 149.6KB / 31.7KB / 62.8KB |
| `Assets\Resources\Data\Monsters\*.txt` | 怪物（DD1 文本） | **230** |
| `Assets\Resources\Data\Buildings\*.building.json` + `Upgrades\Building\*.upgrades.json` | 建筑与升级 | 9 / 8 |
| `Assets\Resources\Data\Mechanics\*.json` | Campaign／**HeirloomExchange**／**Provision**／Roster／TownEvents | 5 文件 |
| `Assets\Resources\Data\Dungeons\*.bytes` · `Maps\*.bytes` · `Inventory\Items.bytes` | 地牢／地图／物品 | 7 / 7 / 1 |
| `Assets\Scripts\**` | 逻辑代码（778 个 `.cs`） | — |

## 2. 🔴 三条最值钱的发现（决定执行顺序）

### 2.1 🔴🔴 我方 buff 引用链 **100% 断裂**（最高优先）—— ✅ **A1 已落地（`c74954e`）**
```
实测：扫描 `darkest/data/*.json` 全部 `"buffs":[...]` ⇒ 引用 **556 个不同 buff id**
      （quirks 182 + trinkets 374）· 而 `buff_defs.json` **只定义 22 个** · 且 **556/556 全部未定义**
      · 且那 **22 个无一被引用** ⚠️
⇒ 🔴 **即：`quirks.json`(170 条) 与 `trinkets.json`(196 条) 的效果【一条都不会生效】**
⇒ ✅ 参考项目有完整池（**1801 条**）且 `DarkestDatabase.cs:393-399/416-422/1829-1830` 三处共用同一池 ✓
⇒ 📌 **这是"数据就位 ≠ 功能就位"（纪律 BK）最典型的一例** ✓

🆕 **A1 已落地（提交 `c74954e`）** —— 实测读数：
   · 池子：`darkest/data/buff_primitives.json` **1801 条 / stat_type 25 / rule_type 23 / 组合 41 /
     duration 63 / is_false_rule 60 / remove_if_not_active 1** ✓
   · 解析率：**556 个去重引用 ⇒ 可解析 482 / 未解析 74**（50 已改名 + 24 未覆盖，逐条在
     `reports/ref_buff_primitives_source.md`）✓
   · 去向分布：`StatMod 579 · DamageMod 359 · UnitResistance 329 · MoraleMod 232 ·
     ExpeditionLayer 109 · ProbMod 99 · HealMod 94` ⇒ **Frozen 0** ✓
   · 🔴 **顺带修掉 3 个上游不存在的名字**（红线 21 实例）：`resolve_xp_percent` /
     `remove_quirk_chance` / `dmg_received_percent` ⇒ 真名见 `BuffPrimitiveTranslation.DestinationNote`；
     M2 冻结清单 **13 → 24**（= 实测 25 个 `stat_type` − 已激活 1）✓
   · ⚠️ **消费侧仍未接线**（怪癖/饰品至今没被生产代码加载）⇒ **"能解析" ≠ "效果生效"**（纪律 BK）✓
   · 🔴 **A1 暴露的一条硬前置（只挡 buff 数值，不挡技能）**：参考项目 **`JsonBuffs.json` 的 `amount` 是【分数】**
     （0.04 = 4%，**非整数 1676/1801**），我方是【整数百分比】⇒ **搬 buff 数值必须 ×100**，且**舍入口径未定**
     ⇒ 🔴 **A7（饰品/怪癖 = 数值全靠 buff id）被它阻塞** ⇒ 见
     `reports/contract_change_request_buff_primitives.md §1.3` ✓
   · ✅ **A2 不受影响**（已实测）：`Heroes/Info/*.bytes` 的 `.dmg` 是**整数百分比字符串**（`"-40%"` / `"0%"`），
     与我方 `dmg_pct` **同量纲** ⇒ **A2 可以直接做** ✓
     🎖️ 这条更正很重要：我先前把"分数"问题**误扩到 A2**，实测后当场收窄（纪律：**怀疑断言先于相信结论**）✓
```

### 2.2 ✅ DD1 buff 的 schema **可直接照抄**（比我方粒度更省代码）
```
`(stat_type, stat_sub_type, amount)` × `(rule_type, rule_data{float,string}, is_false_rule)` × `(duration_type, duration)`
   · `stat_type` **25 种** · `stat_sub_type` 组合出 **41 组**（战斗属性 add/multiply · 8 种抗性 · 21 种自指型）
   · `rule_type` **23 种**（`always`793 · `in_rank`140 · `monsterType`136 · `lightbelow`121 · `hpbelow`66 …）
   · `is_false_rule` = **通用条件取反开关**（60 条在用 ⇒ **不需要正反两版规则**）✓
   · `rule_data` 恒为 `{float, string}` 两槽 ⇒ 数值阈值与字符串枚举同槽（不需要 union 类型）✓
⇒ ✅ **68 个字符串常量表达 1801 条** ⇒ 采纳它 = 我方 P8（M2 buff 原语层）直接有 schema ✓
```

### 2.3 ✅ 技能 `.dmg%` 已抽全（**P7 的前置之一当场解决**）
```
抽出：`reports/unity_ref/hero_skills_from_ref.json` + `02b_hero_skills_from_ref.md`（**525 条**逐级数值）✓
结构结论（实测）：**525 = 485（伤害技能，带 `.dmg`） + 40（治疗技能，带 `.heal`）** ✓
🔴 且 `.dmg` **在 0~4 级【恒定不变】**（`.atk`/`.crit` 才随级数变）
   ⇒ ✅ **我方"一个技能一个 `dmg_pct`"的形状是对的**（不需要按级存）✓
```

## 3. 🎖️ 顺带**判决了 D5 那 12 条候选**（观察清单 D5 可结案）

```
策划 §43 的 12 条【候选】（**当时因为我方"按名字像推"而不许落库**，纪律 BL）
   ⇒ 🆕 **现在有真实来源**：参考项目 `Heroes/Info/*.bytes` 的数值块 ✓
📊 **逐条对照（实测值）**：
| 我方技能 | 原版技能 | 策划猜的值 | 🔴 **参考项目实测** | 判定 |
|---|---|---|---|---|
| `medic_anesthetic` | `blinding_gas` | −100 | **−100%** | ✅ **一致** |
| `medic_medicine_flask` | `plague_grenade` | −90 | **−90%** | ✅ **一致** |
| `tank_taunt` | `bellow` | −100 | **−90%** | 🔴 **猜错 ⇒ 改 −90** |
| `tank_war_cry` | `command` | 0 | **0%** | ✅ **一致** |
| `tank_iron_wall` | `bolster` | — | **0%**（bolster 无 `.dmg` ⇒ 纯 buff 技）| ✅ **一致** |
| `tank_selfless_charge` | `retribution` | −75 | **−75%** | ✅ **一致** |
| `warrior_battle_fury` | `adrenaline_rush` | 0 | **0%** | ✅ **一致** |
| `warrior_last_stand` | `bleed_out` | +20 | **+15%** | 🔴 **猜错 ⇒ 改 +15** |
| `commissar_pistol_shot` | `pistol_shot` | −15 | **−25%** | 🔴 **猜错 ⇒ 改 −25** |
| `commissar_charge_order` | `duelist_advance` | −20 | **−20%** | ✅ **一致** |
| `commissar_execution_order` | `opened_vein` | −15 | **−15%** | ✅ **一致** |
| `commissar_supervise` | `take_aim` | −80 | **−80%** | ✅ **一致** |
⇒ 📊 **9 条一致 · 3 条猜错**（`tank_taunt` · `warrior_last_stand` · `commissar_pistol_shot`）✓
⇒ ✅ **结论：D5 可以结案**（12 条全部有了可指的来源）—— 但**落地要按下面 A2 的步骤**，并留前后读数 ✓
```

## 4. 采用映射与执行顺序（**按杠杆排序**）

| # | 项 | 参考来源 | 我方目标 | 为什么这个顺序 |
|---|---|---|---|---|
| **A1** | ✅ **buff 原语池（已完成 `c74954e`）** | `JsonBuffs.json`（1801） | 新 `data/buff_primitives.json` + 新原语层 | **先修那条 100% 断裂的引用链** ⇒ 引用链 **0 → 482** 可解析 ✓ |
| **A2** | 技能 `dmg%`（44 条） | `Heroes/Info/*.bytes` `.dmg`（485） | `skills.json` 的 `dmg_pct` | **P7 的前置**；且**同时判决 D5** ✓ ✅ **同量纲（整数百分比）⇒ 不被量纲问题阻塞，可以现在做** |
| **A3** | 英雄武器/护甲 5 阶 | 同上 `weapon:`/`armour:` | `hero_upgrades.json` / `units.json` | 按新指令**取参考值** ⇒ 消化那 97 条冲突 ✓ |
| **A4** | 怪物 | `Monsters/*.txt`（230） | `units.json`（现 7） | 量级差最大（7 → 230） |
| **A5** | 怪物 AI | `JsonAI.json`（160） | `enemy_ai.json`（现 3） | 依赖 A4 的怪名 |
| **A6** | 折磨/美德 + act-out 行为表 | `JsonTraits.json`（12） | `traits.json`（现 7 折磨） | 我方**缺 5 美德 + 14 项回合开始 / 15 项反应行为** |
| **A7** | 饰品（488）/ 怪癖（163） | `JsonTrinkets`/`JsonQuirks` | `trinkets.json` / `quirks.json` | 依赖 A1（数值全靠 buff id） |
| **A8** | 任务 / 战利品表 / 旁白 / 队伍名 | `JsonQuests` / `JsonLoot` / `Narration` / `PartyNames` | 我方**整表缺失** | 其中 `JsonQuests` 是 **P17（M14 Quest 层）** 的载体 |
| **A9** | 建筑与升级 | `Buildings/*.building.json` + `Upgrades/Building/*` | `buildings.json`(8) | 我方已有 8 栋，按参考校准 |
| **A10** | 补给 / 物品 | `Mechanics/Provision.json` + `Inventory/Items.bytes` | 我方缺失 | **P16（M12 补给 kernel）** 的载体 |
| **A11** | 传家宝兑换 | `Mechanics/HeirloomExchange.json` | `heirloom_exchange.json`(12) | 与已完成的步骤② 对账 |
| **A12** | 地牢 / 地图 | `Dungeons/*.bytes` · `Maps/*.bytes` | `expedition_map/nodes` | — |

## 5. 每一项的做完判据（**沿用本仓纪律**）

```
① **前后读数**：条目数 / 关键字段值 / 相关用例数，改前改后各一份 ✓
② **来源标注**：每条数据带 `_source`（**参考项目相对路径 : 行号**）⇒ 可复算 ✓（纪律 AT/AZ）
③ **门禁**：`check_data_discipline.py` 三扫 0 处 · `check_file_size.py` · B6 · godot 引用 ✓
④ **全量测试 + 冒烟**：数值类改动**必须**跑冒烟（数值会穿透到战斗读数）✓
⑤ **提交号**：每项独立提交，记录写进 `dd1_baseline` 新节 ✓
```

## 6. ⚠️ 不能照抄的部分（**实测出来的**）

```
· `is_exclusive_desire`（51 条在用）⇒ 参考项目**解析后 `break;` 丢弃** ⇒ 我方**不要实现** ✓
· `is_pre_turn`（21 条在用）⇒ 同样被丢弃 ✓
· `JsonCamping.json` 的 `level` ⇒ **64/64 全为 0**（死字段）✓
· `JsonBuffs.remove_if_not_active` ⇒ 1800/1801 为 `false`（近乎死字段）✓
· `Narration.priority` ⇒ 仅 1 条在用 ✓
· 🔴 技能欲望 `base_chance × 100` 而目标欲望 `× 1` ⇒ **两侧缩放不一致**（各自独立归一故行为无错）
  ⇒ ✅ 我方实现时**应统一**，不要照抄这个不一致 ✓
· 🔴 `JsonAI.json` 第 **7438 行有尾随逗号** ⇒ 不是严格合法 JSON
  ⇒ ✅ 我方读取器**必须容错**（`AllowTrailingCommas`）或写入时修掉 ✓
```

## 7. 🆕 战斗逻辑结论（`03_combat_logic.md` · 2026-09-26 分队交付）

### 7.1 🔴 伤害一行版（**这就是 P7 要落的公式**）

```
dmg = ceil( Lerp(weapon.DamageLow, weapon.DamageHigh, rnd) * (1 + skill.DamageMod) * (1 - target.Protection) )
      暴击 ⇒ round(dmg * 1.5)
依据：BattleSolver.cs:383-413 · Character.cs:1107-1112 ✓
⇒ ✅ 与 §0 的判断一致：`(1 + DamageMod)` 正是我方要换的那个因子 ✓
```

### 7.2 🎖️ 解开了 P5 的阻塞（`def` 合并）

```
实测（一手代码）：
   · `dodge`（= `DefenseRating`）⇒ **只进命中**：`hitChance = Clamp(Skill.Acc + Perf.Acc - Dodge, 0, 0.95)`
   · `prot`（= `ProtectionRating`）⇒ **只进伤害**（乘算后 `ceil`）
   · 🔴 **原版【只有这两个字段】，没有第三个"护甲减伤"** ⇒ **`def` 就是闪避**
⇒ ✅ **对 P5 的意义**：我方那个自加的 `PhysDef` **不是原版概念** ⇒
   它与 `Dodge` 的关系**不是"合并"，而是"删除/改名"**（`Prot` 才是减伤，且封顶 `max(0.85, raw)`）✓
   📌 与架构此前的判断一致（「原版只有一个 `def`（= 我们的 Dodge）」）✓
```

### 7.3 🔴 最可能与我方不一致的 5 点（**采用时要逐条对齐**）

```
① 把 `def` 当护甲减伤 / 让 `prot` 参与命中 ⇒ **系统性错位** ⚠️
② 取整与封顶：`ceil(× (1-prot))` → 暴击 `×1.5` → `round`；`prot` 封顶 `max(0.85, raw)`；
   命中上限 **0.95**；**暴击【无】封顶** ⇒ 我方若处处 clamp 就会错 ✓
③ 回合顺序：每轮一次性 `OrderByDescending(Speed + rand[0,2] + rand[0,1))`；
   **轮内不重排**；怪物按 `number_of_turns_per_round` 多次入池 ✓
④ DoT 在**受影响者自己回合开始时**结算（Bleed→Poison 顺序），**绕开 prot 与命中**；
   地牢推进对英雄**再结算一次**；DoT **实例叠加不覆盖** ✓
⑤ 🔴 **未命中仍会施加效果**（除非显式 `on_miss false`）⚠️
```

### 7.4 其它阈值（采用时的对照表）

```
· 压力上限 **200** · `>=50` 有压力 · `>=100` 过压（首次触发 resolve check）· `==200` 心衰 ✓
· 美德率 `Clamp(0.25 + ResolveCheckPercent, 0.01, 0.6)`；美德后压力置 `Next(20, 40)` ✓
· 死门：HP 归零且未在死门 ⇒ 进死门（挂职业 deaths_door buff + BarkStress）；
  已在死门 ⇒ 每次独立 `CheckSuccess(Clamp(DeathResist, 0, 0.87))`，失败即死 ✓
· 心衰且未在死门 ⇒ `当前最大HP × 100%` 伤害 + 压力设 75% 后进死门 ✓
· 抗性：**英雄 8 种**（Stun/Poison/Bleed/Disease/Move/Debuff/DeathBlow/Trap）· 怪物 5 种；
  统一 `Clamp(chance - Resist + (英雄 ? XChance : 0), 0, 0.95)` **线性相减无递减**；
  🔴 **疾病例外**：`1 - Resist` 且**无夹取** ✓
```

### 7.5 ⚠️ 参考项目的**缺陷**（并入 §6 的"不要照抄"）

```
🔴 `DmgReceivedPercent` 在参考项目里**完全没实现**（只是解析）✓
🔴 `BuffEffect.ApplyQueued` 的**抗性误用 `Move`**（应为 `Debuff`）⇒ **它的 bug，别抄** ✓
🟡 `OnHit` / `ApplyWithResult` / `CritDoesntApplyToRoll` **只解析不使用**（死字段）✓
```

---

## 9. 🆕 数据总账的 5 条更正（`01_data_inventory.md` · 2026-09-26 分队交付）

```
① 🔴 **我先前的手扫漏了一个文件形态**：真实是 **49 json / 30 bytes / 232 txt / 18 xml / 1 csv = 330 个数据文件**；
   而 **奇物不在 `Curios.json`（不存在），在 `Curios/Curios.csv` ⇒ 60 条**（我方 `curios.json` 只有 **7** 条）⚠️
   ⇒ ✅ **A 系列要加一项：csv 形态**（我方现有抽取器只处理 json/bytes/txt）✓
② 🔴 **7 个 JSON 是【非法 JSON】（尾随逗号）**：`abbey` / `nomad_wagon` / `sanitarium` / `tavern` 的 `.building.json` ·
   `Curios/Traps.json` · `JsonAI.json` · `JsonQuests.json`
   ⇒ 🔴 **`json.loads` 直接抛 `Illegal trailing comma`** ⇒ ✅ **任何导入工具必须先清洗**（我方 C# 侧要 `AllowTrailingCommas`）✓
③ 🎖️ **营地技能的位置被我猜错了**：`Heroes/Info/*.bytes` 里 **`camp_skill:` 出现 0 次** ⇒
   营地技能**只在 `JsonCamping.json` 的 64 条跨职业共享技能里**（带 `hero_classes` 白名单）✓
   ⇒ ✅ **A6/A8 的取数位置按此更正**（不要再去 Info bytes 里找）✓
④ 🔴 **两个 join 陷阱（会静默少配）**：
   · `JsonAI` 的 **brain id ≠ 怪物名**：230 怪 ⇒ **107 精确命中** · **50 需去掉 `_A/_B/_C/_D` 后缀回退** ·
     **73 无 AI**；另有 **28 个 brain 无对应怪物** ⇒ ✅ **A5 必须写"回退规则 + 未命中清单"，不许静默跳过**⚠️
   · `Heroes/Info` **15** 个 vs `Upgrades/Heroes` **16** 个（**Musketeer 无 Info，共用 Arbalest**）✓
   · 每个英雄 `combat_skill:` **42 条 = 7 技能的 `art:` 段 + 35 条数值**（与我方抽取结果 **art=7/stat=35** 一致 ✓）
⑤ ⚠️ **`Maps/*.bytes` 是 Unity 二进制序列化**（控制字节占 50%+，**没有** DD1 前缀，只能抽到 `room:`/`plot_*`/`*_to_*` 可打印串）
   ⇒ 🔴 **它是"已烘焙布局"，与我方参数化 `expedition_map.json` 语义不对等** ⇒ ✅ **A12 只能作参考，不能当数据源**⚠️
```

### 9.1 顺带确认的**大缺口**（我方整表缺）

```
· `Localization/*.xml` **12.37 MB = 全部字节的 80%**：**12617 唯一 key × 8 语言 = 100875 entry** ⇒ 我方无对应
  📌 **但它只是文本，与逻辑侧无关** ⇒ ✅ **明确列入"不采用"**（采用计划里划掉）✓
· `JsonQuests`（**任务系统的唯一数据源**）· `JsonLoot` · `Narration` · `PartyNames` · `Obstacles` ⇒ 我方**全部无对应** ✓
· **奇物 60 → 我方 7** 也是大缺口（先前只看到 curios.json 7 条，没意识到参考有 60）✓
```

| 文件 | 内容 |
|---|---|
| `00_our_data_inventory.md` | 我方 `darkest/data/*.json` 25 文件总账（条目数 + 字段频次）✓ |
| `01_data_inventory.md` | 参考项目数据总账（**全部**数据文件：json/bytes/txt/xml/csv）✓ |
| `02a_where_skill_numbers_live.md` | 技能数值在哪个文件（含 `combat_skill:` 28 个 key 全集）✓ |
| `02b_hero_skills_from_ref.md` | 🔴 **15 英雄 × 7 技能 × 5 级 = 525 条数值**（4 个原型逐级明细）✓ |
| `hero_skills_from_ref.json` | 上表的机器可读版（采用 A2/A3 的输入）✓ |
| `03_combat_logic.md` | 🔴 战斗逻辑（伤害/命中/暴击/抗性/回合/死门/士气）—— **已交付**（516 行 · 结论见本文 §7）✓ |
| `04a_town_economy.md` | 🔴 **城镇经济**（8 建筑 / 20 升级树 / 99 前置 / 补给 / `Curios.csv` 18 列）—— **已交付**（86.8KB · 结论见本文 §10.1）✓ |
| `04b_quests_loot_narration.md` | 🔴 **任务 / 战利品 / 旁白 / 队伍名 / 障碍陷阱** —— **已交付**（89.2KB · 结论见本文 §10.2）✓ |
| `05_buffs_ai_quirks_trinkets.md` | buff/AI/怪癖/特质/饰品 数据与逻辑 ✓ |
| `ref_buff_primitives_source.md` | 🆕 **A1 原语层的来源与实测**（由提取器每次重跑生成；含 74 条未解析的**逐条待改清单**）✓ |
| `../contract_change_request_buff_primitives.md` | 🆕 A1 之后契约侧要改的 **5 处**（文件名/形状/量纲/判据源/覆盖差集）✓ |

---

## 10. 🆕 两份分队交付的关键结论（`04a` / `04b` · 2026-09-26）

> 🔴 **过程留痕（诚实）**：城镇/任务这条线**第一次派发失败了**（分队跑到一半崩掉，留下一个 82KB 的
> 半成品 `04_town_economy_quests.md`）⇒ 我**把它拆成两条更窄的任务重派**（`04a` 城镇经济 / `04b` 任务·战利品·旁白），
> 并在任务书里加了硬约束「**先写骨架、每次探测后落盘、不许攒着最后写**」⇒ 两条都按时交付 ✓
> 📌 那个半成品**已删除**（它的范围被 `04a` + `04b` 完整覆盖，且那两份每条断言都带 `file:line` 并各自纠正了自己的先验错）✓

### 10.1 城镇经济（`04a_town_economy.md` · 86.8KB）

```
① 🔴 **升级只花传家宝**：99 条 `currency_cost` 里 `gold` 成员**一条不缺但恒为 0**；
   成员出现次数 `gold 99 / crest 96 / bust 35 / portrait 30 / deed 23`；
   **只有 `gold` 存在 amount=0** ⇒ 没有 gold-only、也没有"传家宝部分全零"的 requirement ✓
   满级全清求和 = **crest 2161 / bust 534 / deed 482 / portrait 277 / gold 0** ✓
   重复树曲线逐项相同（abbey 三树 · tavern 三树 · `rostersize ≡ numrecruits` · `disease_quirk_cost ≡ cost`）✓
② 🔴 **`upgrade_discount` 被解析但【从未被消费】** ⇒ 那个怪癖**不改变任何城镇价格**！
   字面量只在 `JsonBuffs.json:14794/14807`（weapon·armour · amount 0.2）+ 本地化 tooltip 16 处
   （英文原文 "…Weapon Upgrade Cost" ⇒ **设计意图存在、逻辑被丢**）✓
   ⇒ ✅ 我方 `BuffPrimitiveTranslation` 已把它归 **`ExpeditionLayer`（城镇层）** ⇒ 与实测一致 ✓
③ 🔴 **建筑升级本身不打折**（`Estate.cs:389-419` 的 `CanPayPrice` 无 discount 参数）⇒
   0.5 的建筑折扣只作用于英雄技能/装备/饰品 ✓
④ ⚠️ **参考实现缺陷（不要照抄）**：`NomadWagon.UpdateBuilding` 用 `Discount +=` 而**未清零** ⇒
   重复调用会**叠加折扣** ⇒ 我方应"重新求和"而不是照抄 ✓
⑤ 🎖️ **尾随逗号清单是完整的**：4/8 建筑文件非法 —— `abbey` 10 处（19,28,36,44,52,60,68,69,160,258）·
   `sanitarium` 2 处（41,65）· `tavern` 1 处（281）· `nomad_wagon` 1 处（22）；两种形态 `],`→`}` 与 `},`→`]` ✓
⑥ 🔴 **3 个建筑级字段被解析从不被读**（`on_start_town_visit_priority` / `number_of_quests_finished` /
   `highest_dungeon_level`，各声明 8 次，全仓 28 处命中**无一处是"读建筑字段"**）⇒
   **建筑从不按任务数/地牢等级解锁** ⇒ 想要 DD1 行为必须**我们自己做** ✓
```

### 10.2 任务 / 战利品 / 旁白（`04b_quests_loot_narration.md` · 89.2KB）

```
① 🔴 **`JsonQuests.json` 里【没有】任务定义数组**：顶层恰好 7 键 —— `stress_damage`(20) · `goals`(**45**) ·
   `town_progression_goal_ids`(**4**) · `types`(**6**) · `plot_quests`(**30**) · `generation`(5 子键) ·
   `restriction`(1 子键) ⇒ **只有那 30 条 plot_quests 是真任务**（全 `is_plot_quest: true`）✓
   尾随逗号 **恰好 1 处**（`Curios/Traps.json` 是 8 处）✓
② 🔴 **战利品"54 张表"其实只有 33 个不同 id**（`H` 一个 id 就有 13 个变体）；
   变体选择是 `List.Find`（**首个匹配，不是随机**）⇒ **正确性依赖数据顺序**；
   ⚠️ **参考实现缺陷**：`table A` 引用了不存在的 `table J`（`JsonLoot.json:39`），
   而 `RaidSolver.cs:151` 是**裸字典索引** ⇒ 有 `KeyNotFoundException` 风险 ✓
   **24 个 id 定义了却没人引用** ✓
③ 🔴 **旁白：36 事件 / 465 audio_events，其中【5 个字段被解析后从不读】**
   （顶层 `filters` · `entries[].tone` · `entries[].priority`（**连属性都没有**）·
   `queue_while_audio_playing` · `filter`）；另有 **3 个事件 id 完全没有调用点**
   （`half_health_half_stress` / `hunger` / `enter_hallway`）✓
④ 🔴 **任务加载会丢真数据**：`show_as_quest`（28 真/17 假）与 `ignore_fog_of_war` 在 DTO 里但**从不拷进 `QuestGoal`**；
   `is_affliction`/`is_virtue` 与 goal 的 `amount` 从不读；奖励物品**写死最多 3 个**；
   `types[].goal_lists[].goals` 被**拍平**（组合语义丢失）✓
⑤ 📊 **计数更正**：障碍 = **5**（不是 4）· 陷阱 = **4**（只有 3/5 两档变体 ⇒ **难度 6 的陷阱与难度 5 数值相同**，
   代码硬编码回落）· `PartyNames` = **186 条**，键只有 `{id, required_hero_class}`，
   🔴 **文件里【没有名字字符串】** ⇒ 名字来自本地化分类 `"PartyNames"` ✓
⑥ 🎖️ 分队**自己纠了两个先前的错**（`data.percentage` 确实存在 · 陷阱 `Variations` 确实被消费），
   并**撤回**了"`99` 哨兵 = 禁止"的猜测（`RaidPartyPanel.cs:32-36` 证明它是**等级上限**）✓
```

| `_gen_*.py` · `_scan_*.py` · `_q*_*.py` | 可复跑的抽取/统计脚本 ✓ |
