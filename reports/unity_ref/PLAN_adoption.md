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

### 2.1 🔴🔴 我方 buff 引用链 **100% 断裂**（最高优先）
```
实测：扫描 `darkest/data/*.json` 全部 `"buffs":[...]` ⇒ 引用 **556 个不同 buff id**
      （quirks 182 + trinkets 374）· 而 `buff_defs.json` **只定义 22 个** · 且 **556/556 全部未定义**
      · 且那 **22 个无一被引用** ⚠️
⇒ 🔴 **即：`quirks.json`(170 条) 与 `trinkets.json`(196 条) 的效果【一条都不会生效】**
⇒ ✅ 参考项目有完整池（**1801 条**）且 `DarkestDatabase.cs:393-399/416-422/1829-1830` 三处共用同一池 ✓
⇒ 📌 **这是"数据就位 ≠ 功能就位"（纪律 BK）最典型的一例** ✓
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
| **A1** | 🔴 **buff 原语池** | `JsonBuffs.json`（1801） | 新 `data/dd1_buffs.json` + 新原语层 | **先修那条 100% 断裂的引用链** ⇒ 一落地，怪癖与饰品**立刻开始生效** |
| **A2** | 技能 `dmg%`（44 条） | `Heroes/Info/*.bytes` `.dmg`（485） | `skills.json` 的 `dmg_pct` | **P7 的前置**；且**同时判决 D5** ✓ |
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

## 7. 本目录的产物

| 文件 | 内容 |
|---|---|
| `00_our_data_inventory.md` | 我方 `darkest/data/*.json` 25 文件总账（条目数 + 字段频次）✓ |
| `01_data_inventory.md` | 参考项目数据总账（**全部**数据文件：json/bytes/txt/xml）✓ |
| `02a_where_skill_numbers_live.md` | 技能数值在哪个文件（含 `combat_skill:` 28 个 key 全集）✓ |
| `02b_hero_skills_from_ref.md` | 🔴 **15 英雄 × 7 技能 × 5 级 = 525 条数值**（4 个原型逐级明细）✓ |
| `hero_skills_from_ref.json` | 上表的机器可读版（采用 A2/A3 的输入）✓ |
| `03_combat_logic.md` | 战斗逻辑（伤害/命中/暴击/抗性/回合/死门/士气）—— **分队进行中** |
| `04_town_economy_quests.md` | 城镇/经济/任务/建筑/补给 —— **分队进行中** |
| `05_buffs_ai_quirks_trinkets.md` | buff/AI/怪癖/特质/饰品 数据与逻辑 ✓ |
| `_gen_*.py` · `_scan_*.py` · `_q*_*.py` | 可复跑的抽取/统计脚本 ✓ |
