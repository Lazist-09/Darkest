# Unity 参考项目：buff / AI / 怪癖 / 特质 / 饰品 数据与逻辑勘查

**参考项目**：`F:\GithubPro\Darkest-Dungeon-Unity`（DD1 Unity 重实现，GPL —— 本报告只描述语义，不逐行誊抄）
**数据目录**：`Assets/Resources/Data/`
**代码根**：`Assets/Scripts/`
**勘查方式**：只读。用 python 统计顶层 key / 条目数 / 字段频次 / 样例，脚本片段见 §9；**没有把任何大文件整体读入上下文**。

> ⚠️ **JSON 不是严格 JSON**：`JsonAI.json` 第 7438 行有尾随逗号，`System.Text.Json`（严格解析）会直接报错。Newtonsoft 宽容所以 Unity 端能读。我方若用严格解析器读参考数据需先清洗。

---

## 0. 总览

| 文件 | 顶层 key | 条目数 | 一句话用途 |
|---|---|---|---|
| `JsonBuffs.json` | `buffs` | **1801** | 全部 buff 定义（原语 = (stat_type, stat_sub_type) × 23 种触发规则） |
| `JsonAI.json` | `monster_brains` | **160** | 每个怪一套「技能欲望权重 + 目标欲望权重 + 先手欲望」 |
| `JsonQuirks.json` | `quirks` | **163** | 怪癖 + 疾病同表，`is_disease` 区分 |
| `JsonTraits.json` | `traits` | **12** | 7 个折磨(affliction) + 5 个美德(virtue)，含回合开始/反应 abt-out 表 |
| `JsonTrinkets.json` | `rarities`, `trinkets` | **488**（12 稀有度） | 饰品：价格/限装数/职业限制 + buff id 数组 |
| `JsonCamping.json` | `configuration`, `skills` | **64** | 扎营技能：耗时/限次/职业/效果/升级花费 |
| `JsonLoot.json` | `darkness_bonuses`, `loot_tables` | 2 + **54** | 战利品表（按难度×地牢分表，可嵌套） |
| `Narration.json` | `filters`, `entries` | 1 + **36**（465 条音频事件） | 旁白/语音：按事件 id → 音频路径 + 标签 + 出现次数上限 |
| `PartyNames.json` | `party_names` | **186** | 命名队伍组合（4 人职业组合 → 队伍名 id） |

统一加载入口：`Assets/Scripts/Database/DarkestDatabase.cs:33-42`（路径常量）、`:94-117`（加载顺序）。
反序列化 DTO：`Assets/Scripts/Database/DarkestJsonReader.cs`。

---

## 1. 🔴 JsonBuffs.json（最重要）

### 1.1 顶层结构与字段全集

```
{ "buffs": [ {...}, ... ] }        # 1801 条，id 全部唯一（1801 distinct）
```

| 字段 | 出现次数 | 类型 | 含义 |
|---|---|---|---|
| `id` | 1801 | string | 全局唯一 buff id（`Buffs` 字典的 key，`DarkestDatabase.cs:1459-1470`） |
| `stat_type` | 1801 | string | **原语族名 / 修改器种类**，25 种（见 §1.3） |
| `stat_sub_type` | 1801 | string | **被修改的属性名**，20 种（21% 为空串 —— 当 `stat_type` 自身就是属性时） |
| `amount` | 1801 | float | 数值。区间 [-10, 10]，含 0（6 条）和负数（减益） |
| `remove_if_not_active` | 1801 | bool | 条件不再满足时是否移除该 buff。**1800 条是 false，仅 1 条 true** → 近乎死字段 |
| `rule_type` | 1801 | string | **触发条件原语**，23 种（见 §1.4） |
| `is_false_rule` | 1801 | bool | 规则取反。`false` 1741 / `true` 60（例：`in_rank` + `rule_data.float=3` + `is_false_rule=true` = "**不在** 3 号位时生效"） |
| `rule_data` | 1801 | object | 规则参数容器，键**恒为** `{float, string}` 两个（1801/1801） |
| `rule_data.float` | 1801 | float | 数值参数（阈值/档位）。非 0 的有 615 条，取值分布：26×83, 75×75, 3×62, 0.25×49, 50×39, 0.75×29, 51×16, 0.5×14, 25×12 … |
| `rule_data.string` | 1801 | string | 字符串参数（怪物类型 / 地牢 / 状态键 / 活动名）：`eldritch`46, `unholy`32, `beast`31, `man`28, `tagged`20, `rake`10, `crypts/weald/warrens/cove`各 9, `hounds_rush`5, `darkest_eye`3, `meditation`3, `brothel`3, `prayer`/`flagellation`/`bar`/`gambling`各 2, `transform`1 |
| `duration_type` | **63**（仅 3.5%） | string | 生效时长语义（见 §1.5） |
| `duration` | **63** | int | 时长数值，只有 4 或 1 两个值 |

> 关键结论：**字段是"9 + 2"结构**。1801 条里 1738 条只有 9 个字段，63 条额外带 `duration_type`/`duration`。这是一个非常干净的扁平 schema —— 极适合直接映射成我方 `buff_defs` 的两层结构。

### 1.2 真实样例（3 条）

```jsonc
// (a) 最典型的饰品 buff：把 attack_rating 加 4%
{ "id": "TRINKET_ACC_B1", "stat_type": "combat_stat_add", "stat_sub_type": "attack_rating",
  "amount": 0.04, "remove_if_not_active": false, "rule_type": "always",
  "is_false_rule": false, "rule_data": { "float": 0, "string": "" } }

// (b) 带时长 + 怪物类型条件的扎营 buff（只对大体积怪 +20% 伤害）
{ "id": "campingDMGLowBuffLargeMonsters", "stat_type": "combat_stat_multiply",
  "stat_sub_type": "damage_low", "amount": 0.25,
  "duration_type": "combat_end", "duration": 4,
  "remove_if_not_active": false, "rule_type": "monsterSize", "is_false_rule": false,
  "rule_data": { "float": 2, "string": "" } }

// (c) 否定规则：不在 3 号位就掉 3% 命中
{ "id": "TRINKET_NOTBACKRANK_ACC_D1", "stat_type": "combat_stat_add",
  "stat_sub_type": "attack_rating", "amount": -0.03, "remove_if_not_active": false,
  "rule_type": "in_rank", "is_false_rule": true, "rule_data": { "float": 3, "string": "" } }
```

### 1.3 原语清单 A：`stat_type`（25 种）+ (stat_type, stat_sub_type) 组合（**41 种**）

**41 个 (stat_type, stat_sub_type) 组合就是 DD1 的 buff 原语全集。** 按语义分四类：

**(A) 战斗属性修改 —— 用 `stat_sub_type` 指属性**（9 个组合）
| stat_type | stat_sub_type | 频次 | 例 id |
|---|---|---|---|
| `combat_stat_multiply` | `damage_low` | 177 | `TRINKET_DMGL_B1` |
| `combat_stat_multiply` | `damage_high` | 177 | `TRINKET_DMGH_B1` |
| `combat_stat_multiply` | `max_hp` | 36 | `TRINKET_MAXHP_B1` |
| `combat_stat_multiply` | `defense_rating` | 1 | `virtueVigorousBuff2` |
| `combat_stat_add` | `attack_rating` | 134 | `TRINKET_ACC_B1` |
| `combat_stat_add` | `crit_chance` | 126 | `TRINKET_CRIT_B1` |
| `combat_stat_add` | `speed_rating` | 102 | `TRINKET_SPD_B1` |
| `combat_stat_add` | `defense_rating` | 90 | `TRINKET_DEF_B1` |
| `combat_stat_add` | `protection_rating` | 90 | `TRINKET_PROT_B1` |

**(B) 抗性 —— `stat_type="resistance"` + 9 个属性**（378 条，最大的一块）
| stat_sub_type | 频次 | 例 id |
|---|---|---|
| `poison` | 58 | `TRINKET_BLIGHTRESIST_B1` |
| `bleed` | 51 | `TRINKET_BLEEDRESIST_B1` |
| `move` | 42 | `TRINKET_MOVERESIST_B1` |
| `debuff` | 38 | `TRINKET_DEBUFFRESIST_B1` |
| `disease` | 37 | `TRINKET_DISEASERESIST_B1` |
| `stun` | 36 | `TRINKET_STUNRESIST_B1` |
| `death_blow` | 34 | `TRINKET_DEATHBLOWRESIST_B1` |
| `trap` | 33 | `TRINKET_TRAPRESIST_B1` |

**(C) "自指属性"型 —— `stat_type` 自己就是属性，`stat_sub_type` 为空串**（21 个组合）
| stat_type | 频次 | 例 id |
|---|---|---|
| `stress_dmg_received_percent` | 127 | `TRINKET_STRESSDMG_B1` |
| `debuff_chance` | 46 | `TRINKET_DEBUFFSKILL_B1` |
| `hp_heal_percent` | 44 | `TRINKET_HEALDONE_B1` |
| `resolve_check_percent` | 44 | `TRINKET_RESOLVECHECK_B1` |
| `scouting_chance` | 41 | `TRINKET_SCOUTING_B1` |
| `hp_heal_received_percent` | 38 | `TRINKET_HPHEALRECEIVED_B1` |
| `stress_heal_received_percent` | 32 | `TRINKET_STRESSRECOVERY_B1` |
| `resolve_xp_bonus_percent` | 26 | `TRINKET_RESOLVEXPBONUS_B1` |
| `monsters_surprise_chance` | 19 | `TRINKET_ancestors_lantern_MONSTERSURPRISE_BUFF` |
| `food_consumption_percent` | 18 | `TRINKET_EATSASZERO` |
| `stun_chance` | 14 | `TRINKET_STUNSKILL_B1` |
| `poison_chance` | 14 | `TRINKET_BLIGHTSKILL_B1` |
| `move_chance` | 13 | `TRINKET_MOVESKILL_B1` |
| `bleed_chance` | 12 | `TRINKET_BLEEDSKILL_B1` |
| `hp_heal_amount` | 12 | `HPRECOVERY10` |
| `starving_damage_percent` | 11 | `TRINKET_NOSTARVEDMG` |
| `remove_negative_quirk_chance` | 9 | `TRINKET_SANITARIUMTREATMENTCHANCE_B1` |
| `party_surprise_chance` | 9 | `TRINKET_PARTYSURPRISE_B1` |
| `damage_received_percent` | 5 | `skill_whistle_1` |
| `stress_heal_percent` | 2 | `TRINKET_bright_tambourine_STRESSHEALDONE_BUFF` |
| `stress_dmg_percent` | 1 | `TRINKET_ABOM_TRANSFORM_STRESS_BUFF` |

**(D) 特殊型**（3 个）
| stat_type | stat_sub_type | 频次 | 说明 |
|---|---|---|---|
| `stress_dmg_received_percent` | `mode` | 1 | 獘化形态下受压伤害（`TRINKET_ABOM_STRESSDOT_BUFF`） |
| `upgrade_discount` | `weapon` | 1 | 武器升级折扣（`QUIRK_weapon_UPGRADEDISCOUNT_B`） |
| `upgrade_discount` | `armour` | 1 | 护甲升级折扣（`QUIRK_armour_UPGRADEDISCOUNT_B`） |

**`stat_sub_type` 全部 20 值频次**：`''`536, `damage_low`177, `damage_high`177, `attack_rating`134, `crit_chance`126, `speed_rating`102, `defense_rating`91, `protection_rating`90, `poison`58, `bleed`51, `move`42, `debuff`38, `disease`37, `max_hp`36, `stun`36, `death_blow`34, `trap`33, `mode`1, `weapon`1, `armour`1。

### 1.4 原语清单 B：`rule_type`（23 种，触发条件）

| rule_type | 频次 | 语义（对照 `CharacterHelper.StringToBuffRule`, `Character/Utils/CharacterHelper.cs:296-354`） |
|---|---|---|
| `always` | 793 | 无条件 |
| `in_rank` | 140 | 位于 `rule_data.float` 号位 |
| `monsterType` | 136 | 目标/自身怪物类型 == `rule_data.string`（eldritch/unholy/beast/man） |
| `lightbelow` | 121 | 火把光 < `rule_data.float` |
| `lightabove` | 102 | 火把光 > `rule_data.float` |
| `firstroundonly` | 80 | 仅第 1 回合 |
| `hpbelow` | 66 | HP% < `rule_data.float` |
| `rangedonly` | 65 | 仅远程技能 |
| `meleeonly` | 57 | 仅近战技能 |
| `at_deaths_door` | 42 | 死门状态 |
| `in_camp` | 37 | 扎营中 |
| `in_dungeon` | 36 | 副本内（`rule_data.string` = crypts/weald/warrens/cove） |
| `hpabove` | 29 | HP% > `rule_data.float` |
| `afflicted` | 26 | 自身处于折磨 |
| `actorStatus` | 20 | 自身状态键 == `rule_data.string`（`tagged` 20 次） |
| `skill` | 19 | 技能 id == `rule_data.string`（`rake`10, `hounds_rush`5） |
| `in_activity` | 14 | 镇上活动中（`meditation`/`brothel`/`prayer`/`flagellation`/`bar`/`gambling`） |
| `virtued` | 8 | 自身处于美德 |
| `monsterSize` | 4 | 怪物体积档位 == `rule_data.float` |
| `stress_above` | 3 | 压力 > 阈值 |
| `walking_backwards` | 1 | 后退移动中 |
| `in_corridor` | 1 | 走廊地形 |
| `in_mode` | 1 | 特定形态（`transform`） |

> 代码侧 `StringToBuffRule` 还认 `stress_below` 和 `riposte`，但**数据里 0 条使用** → 两个未启用规则。

典型案例组合（rule_type × stat_type 交叉，前几名）：`always×resistance` 241、`always×combat_stat_add` 150、`always×combat_stat_multiply` 95、`monsterType×combat_stat_multiply` 73、`in_rank×combat_stat_add` 60、`lightbelow×combat_stat_add` 50。

### 1.5 时长（`duration_type`，仅 63 条）

| duration_type | 频次 | duration 值 | 映射的 `BuffDurationType`（`DarkestDatabase.cs:299-322`） |
|---|---|---|---|
| `combat_end` | 35 | 4 | `Combat` |
| `quest_end` | 14 | 1 | `Raid` |
| `activity_end` | 12 | 1 | `Activity` |
| `idle_start_town_visit` | 1 | 1 | `IdleTownVisit` |
| `quest_complete` | 1 | 1 | `QuestComplete` |
| （缺失） | 1738 | — | `Undefined`（= 永久，靠 `remove_if_not_active` / 来源移除） |

### 1.6 buff 数值怎么被解释（关键语义）

`DarkestDatabase.cs:324-363` 的分派逻辑把 25 个 `stat_type` 归成两类 `BuffType`：

- `resistance` 和 `upgrade_discount` → `BuffType.StatAdd`，`AttributeType = StringToAttributeType(stat_sub_type)`
- `combat_stat_add` / `combat_stat_multiply` → `BuffType` 保持名字，`AttributeType = StringToAttributeType(stat_sub_type)`
- 其余 21 个「自指型」→ `BuffType.StatAdd`，`AttributeType = StringToAttributeType(stat_type)`（**用 stat_type 当属性名**）
- 兜底：`Debug.Log("Unexpected buff type")` + `continue`（该条被丢弃）

`AttributeType` 枚举在 `Mechanics/MechanicsDefines.cs:1`。
**`amount` 的符号即正负**：`amount` 负 = 减益。逐 stat_type 的符号分布（部分）：`resistance` 正 154 / 负 175、`combat_stat_add` 正 350 / 负 192、`combat_stat_multiply` 正 263 / 负 128。`stress_dmg_percent` 只出现负值。
**倍率约定**：`combat_stat_multiply` + `amount=0.2` 读作"×1.2"（+20%），不是 ×0.2。
**正负判定**：`Buff.IsPositive()`（`Character/Buff.cs:52-63`）—— 对 `StressDmgPercent`/`StressDmgReceivedPercent` 而言 `amount>0` 是**坏**的；其余 `amount>=0` 是好的。

### 1.7 谁在读它

| 位置 | 作用 |
|---|---|
| `Database/DarkestDatabase.cs:33` | 路径常量 `"Data/JsonBuffs"` |
| `Database/DarkestDatabase.cs:94, 1459-1470` | `LoadJsonBuffs()`：填 `Buffs: Dictionary<string,Buff>` |
| `Database/DarkestDatabase.cs:286-371` | `GetJsonBuffLibrary()`：**唯一 JSON→`Buff` 的翻译层**，含 stat_type / duration_type / rule_type 三次 switch |
| `Database/DarkestJsonReader.cs:600-624` | DTO `JsonBuffData` / `JsonBuff` / `JsonBuffRuleData` |
| `Database/DarkestJsonReader.cs:964-967` | `GetJsonBuffs(string)` |
| `Database/DarkestDatabase.cs:393-399` | 怪癖按 id 挂 buff：`quirk.Buffs.Add(Buffs[buffName])` |
| `Database/DarkestDatabase.cs:416-422` | 饰品按 id 挂 buff：`trinket.Buffs.Add(Buffs[buffName])` |
| `Database/DarkestDatabase.cs:1829-1830` | 特质按 id 挂 buff：`trait.Buffs.Add(Buffs[jsonTrait.buff_ids[i]])` |
| `Character/Character.cs:392-436` | 运行时：`AddBuff` / `ApplySingleBuffRule` / `ApplyAllBuffRules`（按 `BuffRule` 批量结算） |
| `Character/Character.cs:571` | 按 `RuleType` 的结算分派 |
| `Character/Buff.cs:1-17` | `BuffType{StatAdd,StatMultiply}` + `BuffRule`(25 值) 枚举 |
| `Character/Utils/CharacterHelper.cs:5-57` | 20 个 `stat_sub_type` → `AttributeType` |
| `Character/Utils/CharacterHelper.cs:254-266, 296-354` | `StringToBuffType` / `StringToBuffRule` |
| `Mechanics/Skills/Effects/CombatStatBuffEffect.cs`、`BuffEffect.cs` | 技能施加 buff 时从表里取 |
| `Mechanics/Skills/CampingSkillHelper.cs` | 扎营效果 `type="buff"` 用 `sub_type` 当 buff id |

---

## 2. 🔴 JsonAI.json

### 2.1 结构：**"每个怪一组技能+权重"，是的 —— 且是三组欲望表**

```
{ "monster_brains": [ { "id", "skill_cooldowns":[],
                        "skill_selection_desires":[], "target_selection_desires":[],
                        "bonus_initiative_desires":[] }, ... ] }   # 160 条，id 唯一
```

| 字段 | 频次 | 说明 |
|---|---|---|
| `id` | 160 | brain id。命名模式：`<怪名>_<难度档 A/B/C>`（78 个怪 × 3 档）+ `default`/`default_A/B/C` 兜底 + 特例（`swine_prince`, `shambler`, `siren`, `collector*`, `kill_marked_target_*`, `cauldron_empty/full`, `drowned_anchor/captain`, `ectoplasm`, `swinetaur`, `fuseman`, `drummer_A`, `giant_A/B/C`） |
| `skill_cooldowns[]` | 36 条 / 25 个 brain 非空 | `{combat_skill_id, amount}` —— 用过该技能后冷却 `amount` 回合。例：`giant_B` 对 `confusion_spores`/`poison_spores` 各设 2 |
| `skill_selection_desires[]` | **449/449 各带 `data.base_chance`** | 技能选择欲望。`data.combat_skill_id` 出现 420（28 条无技能 id 的类型不需要） |
| `target_selection_desires[]` | 294 | 目标选择欲望，必带 `base_chance` + `specific_combat_skill_id` + `is_exclusive_desire` + `is_enemy_target_desire` + `is_friendly_target_desire` |
| `bonus_initiative_desires[]` | 21（**148/160 brain 为空**） | 先手/抢动欲望 |

`cauldron_empty` / `cauldron_full` 两个 brain 三张表全空（纯道具，不行动）。

### 2.2 欲望类型全集（**这就是 AI 的"评分函数"词汇表**）

**(a) `skill_selection_desires[].type` —— 9 种**
| type | 频次 | data 独有字段 | 语义 |
|---|---|---|---|
| `specific_skill` | 347 | `combat_skill_id` | 想放某个具体技能 |
| `performing_turn_skill` | 52 | `combat_skill_id`, `performing_turn` | 该技能已被连放 N 回合时 |
| `ally_alive_skill` | 16 | `combat_skill_id`, `ally_base_class_id` | 某类友军存活时 |
| `heal_skill` | 11 | `hp_ratio_treshold` | 有人 HP% 低于阈值 |
| `random_skill` | 7 | 无 | 随便放 |
| `effect_key_status_skill` | 6 | `effect_key_status`（如 `tagged`） | 目标带某状态键时 |
| `preferred_skill` | 4 | 无 | 优先技能 |
| `ally_dead_skill` | 3 | `combat_skill_id`, `ally_base_class_id` | 某类友军死亡时（如 `swine_prince` 的 `obliterate_enraged`，`base_chance`=1000） |
| `fill_ally_captor_empty_skill` | 3 | 无技能 id | 补位被掳走的友军 |

**`data` 里的"条件门槛"字段（各类型的可选修饰）**，按出现频次：
`performing_turn`52, `marked_heroes_min/max`34/34, `monsters_size_min/max`32/32, `ally_base_class_id`19, `first_initiative_only`15, `hp_ratio_treshold`12, `monsters_min/max`12/12, `non_virtued_heroes_min`11, `guarded_monsters_min/max`10/10, `effect_key_status`6, `virtued_heroes_max`6, `control_count_min/max`4/4, `can_target_deaths_door`3, `can_target_last_hero`3, `heroes_min`3, `non_deaths_door_heroes_min`2, `per_round_chance`2, `ignore_if_stun`1。

**(b) `target_selection_desires[].type` —— 8 种**
`random_target`174, `resistance_target`43（+`resistance_type_id`）, `marked_target`31, `health_target`16（+`is_greater_comparison`71 次全局）, `stress_target`12, `ally_class_target`11, `rank_target`4, `fill_ally_captor_empty_target`3。
`data` 通用过滤器：`can_target_deaths_door`47, `can_target_last_hero`47, `can_target_not_overstressed`14, `can_target_afflicted`14, `can_target_virtued`14。

**(c) `bonus_initiative_desires[].type` —— 6 种**
`guaranteed`10, `last_skill`3, `ally_actor_class_count`3, `death`2, `hp_ratio_threshold`2, `ally_last_damaged`1。
`data` 字段：`is_round_start`/`is_round_in_progress`/`is_round_finish`/`is_pre_turn`/`is_post_turn` 各 21、`combat_skill_id_override`21、`ally_base_class_id`4、`monsters_size_limit`4、`last_combat_skill_id`3、`ally_count_min/max`3/3、`monsters_min/max`2/2、`health_ratio_threshold`2、`is_under_threshold`2、`heroes_min/max`2/2、`ignore_if_stun`1。

### 2.3 真实样例（2 条）

```jsonc
// 兜底 brain：三档优先级 —— 首选→随机→(HP<50%)治疗
{ "id": "default", "skill_cooldowns": [],
  "skill_selection_desires": [
    { "type": "preferred_skill", "data": { "base_chance": 1.0 } },
    { "type": "random_skill",    "data": { "base_chance": 1.0 } },
    { "type": "heal_skill",      "data": { "base_chance": 100.0, "hp_ratio_treshold": 0.5 } } ],
  "target_selection_desires": [
    { "type": "random_target", "data": { "base_chance": 2.0, "specific_combat_skill_id": "",
        "is_exclusive_desire": false, "is_enemy_target_desire": true,  "is_friendly_target_desire": false } },
    { "type": "marked_target", "data": { "base_chance": 1.0, "specific_combat_skill_id": "",
        "is_exclusive_desire": false, "is_enemy_target_desire": true,  "is_friendly_target_desire": false } },
    { "type": "health_target", "data": { "base_chance": 100.0, "specific_combat_skill_id": "",
        "is_exclusive_desire": false, "is_enemy_target_desire": false, "is_friendly_target_desire": true,
        "is_greater_comparison": false } } ],
  "bonus_initiative_desires": [] }

// 带冷却 + 带状态键条件的 boss brain
{ "id": "giant_B",
  "skill_cooldowns": [ { "combat_skill_id": "confusion_spores", "amount": 2 },
                       { "combat_skill_id": "poison_spores",    "amount": 2 } ],
  "skill_selection_desires": [
    { "type": "specific_skill", "data": { "base_chance": 2.0, "combat_skill_id": "club_smack" } },
    { "type": "specific_skill", "data": { "base_chance": 1.0, "combat_skill_id": "confusion_spores" } },
    { "type": "performing_turn_skill", "data": { "base_chance": 3.0,
        "combat_skill_id": "confusion_spores", "performing_turn": 1 } },
    { "type": "specific_skill", "data": { "base_chance": 2.0, "combat_skill_id": "poison_spores" } } ], ... }
```

### 2.4 AI 决策机制：**纯加权轮盘 + 拒绝重试，没有评分/效用函数**

一句话：**把 `base_chance` 当权重做加权随机抽一个"欲望"，让欲望自己声明能否成立（门槛判定），不成立就把它移出候选集重抽，直到抽中或抽空。**

依据行号：

| 步骤 | 位置 | 说明 |
|---|---|---|
| 1. 复制欲望集 | `Mechanics/Battle/BattleSolver.cs:169` | `new List<SkillSelectionDesire>(monster.Brain.SkillDesireSet)` |
| 2. **加权随机抽一个欲望** | `BattleSolver.cs:174` | `RandomSolver.ChooseByRandom(skillDesires)` |
| 3. 轮盘实现 | `Mechanics/RandomSolver.cs:33-44` | `rnd = random.Next(Σ Chance)`，逐个减 `Chance`；**`Chance` 就是权重**（`IProportionValue`） |
| 4. 欲望自己判定+选技能 | `Mechanics/AI/Skill Desires/SkillSelectionDesire.cs:19-50` | `IsRestricted` 门槛 → 候选技能 → `RandomSolver.Next` 随机选一个技能 → 挂目标 |
| 5. 门槛判定（13 个数值比较） | `SkillSelectionDesire.cs:52-95` | `monsters_min/max`、`monsters_size_min/max`、`marked_heroes_min/max`、`non_virtued_heroes_min`、`control_count_min/max`、`heroes_min`、`virtued_heroes_max`、`guarded_monsters_min/max` —— 全是 `RaidSceneManager.BattleGround.*` 计数器的 `>`/`<` 比较，**没有打分** |
| 6. 失败则剔除重试 | `BattleSolver.cs:172-184` | `while (skillDesires.Count != 0) { ... else skillDesires.Remove(desire); }`；全失败 → `BrainDecisionType.Pass` |
| 7. **`base_chance` 缩放不一致** | `SkillSelectionDesire.cs:128-129` | `Chance = (int)((double)value * 100)` ← 技能欲望 **×100** |
| 8. 目标欲望权重**不缩放** | `Target Desires/TargetSelectionDesire.cs:104-105` | `Chance = (int)(double)value` ← 直接取整 |
| 9. 目标欲望加权抽+重试 | `BattleSolver.cs:201-210`；`SkillSelectionDesire.cs:38-46` | 同样的 `ChooseByRandom` + `Remove` 循环 |
| 10. 目标过滤器 | `TargetSelectionDesire.cs:37-62` | `can_target_deaths_door` / `can_target_last_hero` / `can_target_not_overstressed` / `can_target_afflicted` / `can_target_virtued` 删候选 |
| 11. 多目标 & 溅射 | `TargetSelectionDesire.cs:64-92` | 全体技能全选；单目标随机 1 个；`ExtraTargetsChance` 再溅射 1 个 |
| 12. 类型→类分派 | `Database/DarkestDatabase.cs:180-213` | 9 个 `type` → `SkillSelectionRandom/Preferred/Heal/Specific/PerformingTurn/AllyAlive/FillEmptyCaptor/Status/AllyDead` |
| 13. 冷却落账 | `BattleSolver.cs:177-178` | 选中后把 `SkillCooldowns` 复制进 `CombatInfo` |
| 14. 冷却屏蔽技能 | `SkillSelectionDesire.cs:102-103` | 在冷却中的技能不进候选 |
| 15. 先手欲望 | `Mechanics/AI/Bonus Desires/BonusInitiativeDesire.cs` + 6 个子类 | 独立机制：`CheckBonusInitiative(performer)` 返回 bool，用于抢在正常顺序前行动；`is_round_start/in_progress/finish`、`is_post_turn` 是时序开关 |

**⚠️ 两个被解析但被丢弃的字段（对方实现的坑，我们不要照抄）**
- `is_exclusive_desire`：`TargetSelectionDesire.cs:110-111` → `break;`，**完全无视**。
- `is_pre_turn`：`BonusInitiativeDesire.cs:36-37` → `break;`，**完全无视**（`is_post_turn` 才生效）。

### 2.5 谁在读它

| 位置 | 作用 |
|---|---|
| `Database/DarkestDatabase.cs:38, 98, 1446-1457` | `LoadJsonAI()` → `Brains: Dictionary<string,MonsterBrain>` |
| `Database/DarkestDatabase.cs:168-283` | `GetJsonMonsterBrains()`：三次 switch（skill/target/bonus-init desires） |
| `Database/DarkestJsonReader.cs:460-471` | DTO `JsonMonsterBrainsDatabase` / `JsonMonsterBrains` |
| `Database/DarkestJsonReader.cs:949-956` | `GetJsonAI(string)` |
| `Character/Monster.cs:39, 46` | `Brain = Data.Brains[monsterData.MonsterBrainId]` ← 怪数据里存 brain id |
| `Mechanics/Battle/BattleSolver.cs:161-256` | `UseMonsterBrain()` 主入口 |
| `Mechanics/AI/MonsterBrain.cs:8-15` | `SkillDesireSet` / `TargetDesireSet` 容器 |
| `Mechanics/AI/MonsterBrainDecision.cs` | 决策结果（`Pass` / `Perform` + 技能 + 目标） |
| `RaidSceneManager.cs:3099, 3231`；`RaidSceneMultiplayerManager.cs:1634, 1706` | 战斗流程调用点 |

---

## 3. JsonQuirks.json / JsonTraits.json

### 3.1 JsonQuirks.json

**顶层**：`{"quirks": [...]}` —— **163 条，id 唯一**。字段 10 个，**全部 163/163 齐备**：

| 字段 | 类型 | 含义 |
|---|---|---|
| `id` | string | 怪癖 id |
| `show_explicit_description` | bool | true 128 / false 35 —— true 时用 buff 自动拼描述，false 时用本地化串 `str_quirk_description_<id>`（`Quirk.cs:26-55`） |
| `is_positive` | bool | 正面 64 / 负面 99 |
| `is_disease` | bool | **疾病 23 / 普通怪癖 140** |
| `classification` | string | `mental` 107 / `physical` 56（怪癖本身的"属性归类"，用于分类展示） |
| `incompatible_quirks` | string[] | **互斥关系，有！** 92/163 条非空（见下） |
| `curio_tag` | string | 互动奇物标签。非空仅 19 条：`Worship`3, `Treasure`3, `Food`2, `Unholy`2, `Torture`2, `All`2, `Fountain`1, `Reflective`1, `Body`1, `Drink`1, `Haunted`1 |
| `curio_tag_chance` | float | 触发该标签奇物时的特殊反应概率（0.2 ~ 0.6） |
| `keep_loot` | bool | 触发后**私吞战利品**（`tapeworm`/`kleptomaniac`/`egomania` = true） |
| `buffs` | string[] | **buff id 数组**（不是内联数值！），全部指向 JsonBuffs |

**区分"怪癖 vs 疾病"：是 —— 靠 `is_disease` 布尔位，同表混装。** 23 个疾病：
- `physical` 17：`bad_humours, creeping_cough, hemophilia, the_runs, wasting_sickness, tetanus, rabies, syphilis, stomach_cramp, spotted_fever, the_ague, hysterical_blindness, scurvy, the_fits, tapeworm, the_red_plague, the_black_plague`
- `mental` 6：`bulimic, lethargy, vertigo, vampiric_spirits, ennui, the_worries`
- **全部 23 个都是 `is_positive=false`**（疾病都是负面）
- **全部 23 个 `incompatible_quirks` 为空** → 疾病之间互不互斥

**互斥关系：有，且是双向 N:N（`incompatible_quirks` 数组）。** 92 条非空，最典型是同组"上瘾/信仰"互斥：

| id | incompatible_quirks |
|---|---|
| `alcoholism` | `resolution, gambler, love_interest, enlightened, god_fearing, flagellant` |
| `gambler` | `alcoholism, love_interest, enlightened, god_fearing, flagellant` |
| `god_fearing` | `witness, faithless, alcoholism, gambler, love_interest, enlightened, flagellant` |
| `enlightened` | `alcoholism, gambler, love_interest, god_fearing, flagellant, unquiet_mind` |
| `tough` ↔ `fragile`、`hard_skinned` ↔ `soft` | 成对 |

四象限分布（怪癖数量）：
| is_disease | is_positive | classification | n |
|---|---|---|---|
| false | false | mental | 58 |
| false | false | physical | 18 |
| false | true | mental | 43 |
| false | true | physical | 21 |
| **true** | **false** | mental | **6** |
| **true** | **false** | physical | **17** |

**真实样例**
```jsonc
{ "id": "tough", "show_explicit_description": true, "is_positive": true, "is_disease": false,
  "classification": "physical", "incompatible_quirks": ["fragile"], "curio_tag": "",
  "curio_tag_chance": 0.0, "keep_loot": false, "buffs": ["MAXHP10"] }

{ "id": "tapeworm", "show_explicit_description": true, "is_positive": false, "is_disease": true,
  "classification": "physical", "incompatible_quirks": [], "curio_tag": "Food",
  "curio_tag_chance": 0.6, "keep_loot": true, "buffs": ["..." ] }

{ "id": "creeping_cough", "show_explicit_description": true, "is_positive": false, "is_disease": true,
  "classification": "physical", "incompatible_quirks": [], "curio_tag": "",
  "curio_tag_chance": 0.0, "keep_loot": false, "buffs": ["DMGL-20", "DMGH-20"] }
```

**互斥怎么被消费**（这是我们要抄的核心规则）：
| 位置 | 逻辑 |
|---|---|
| `Character/Hero.cs:420-425` | `AddQuirk`：若已有怪癖的 `IncompatibleQuirks` 含新 id → **拒绝添加** |
| `Character/Hero.cs:427-431` | 可替换集：疾病只能互相替换 / 正面只能换正面 / 负面（非疾病）只能换负面；再 `AddOrReplaceQuirk` |
| `Character/Hero.cs:448-483` | `AddPositiveQuirk` / `AddNegativeQuirk` / `AddDisease`：候选池过滤互斥 |
| `Character/Hero.cs:271-291` | 战斗结算后概率获得怪癖时的同一套互斥过滤 |
| `Campaign/Town/TownActivity.cs:107` | 疗养院治疗按 `QuirkName` 查表 |
| `RaidSceneManager.cs:5449-5451` | 奇物互动 `Data.Quirks.ContainsKey(curioResult.Item)` → 加怪癖 |
| `RaidSceneManager.cs:5511-5512` | 奇物互动 → 加疾病 |
| `Mechanics/Skills/Effect.cs:445-446` | 技能效果 `disease` 用 `Data.Quirks[data[i]]` 造 `DiseaseEffect` |

### 3.2 JsonTraits.json

**顶层**：`{"traits": [...]}` —— **12 条**。注意：**这不是"英雄特质"，是 DD1 的"折磨/美德"（affliction / virtue）**，即压力满 100 / 归零时抽的那个状态。

| 字段 | 频次 | 含义 |
|---|---|---|
| `id` | 12 | trait id |
| `overstress_type` | 12 | `affliction` 7 / `virtue` 5 → **这就是"折磨 vs 美德"的判别字段** |
| `curio_tag` | 12 | 同上怪癖的奇物标签（`Worship`/`Treasure`/`Torture`/`All`/`none`） |
| `curio_tag_chance` | 12 | 概率 |
| `keep_loot` | 12 | 私吞战利品（如 `selfish` = Treasure） |
| `buff_ids` | 12 | buff id 数组（7~13 个/条） |
| `combat_start_turn_act_outs[]` | 168（12×14） | **回合开始行为抽签**：`{id, data:{number_value,string_value}, chance}` |
| `reaction_act_outs[]` | 180（12×15） | **反应行为抽签**：`{id, data:{effect}, chance}` |

**12 条**：
`fearful, paranoid, selfish, masochistic, abusive, depressed, irrational`（affliction）
`stalwart, courageous, focused, powerful, vigorous`（virtue）

**`combat_start_turn_act_outs[].id` 全集（14 个，每条 trait 都齐 14 项）**：
`nothing, bark_stress, change_pos, ignore_command, random_command, retreat_from_combat, attack_friendly, attack_self, mark_self, stress_heal_self, stress_heal_party, buff_random_party_member, buff_party, heal_self`

**`reaction_act_outs[].id` 全集（15 个）**：
`block_move, block_heal, block_buff, block_item, block_combat_retreat, comment_self_hit, comment_self_missed, comment_ally_hit, comment_ally_missed, comment_ally_attack_hit, comment_ally_attack_missed, comment_move, comment_curio_interaction, comment_trap_triggered, block_effect`

**真实样例**
```jsonc
// 折磨：恐惧（12 个 buff + 回合开始 6/14 概率发呆、1/14 换位、1/14 抗命…；反应：33% 阻止移动、20% 各种抱怨）
{ "id": "fearful", "overstress_type": "affliction", "curio_tag": "Worship", "curio_tag_chance": 0.5,
  "keep_loot": false,
  "buff_ids": ["STUNRESIST-15","BLIGHTRESIST-15","BLEEDRESIST-15","DISEASERESIST-15",
               "DEBUFFRESIST-15","MOVERESIST-15","TRAPRESIST-15","MAXHP-10",
               "DMGL-25","DMGH-25","DEF10","SPD2"],
  "combat_start_turn_act_outs": [
    { "id": "nothing",          "data": {"number_value":0.0,"string_value":""}, "chance": 6 },
    { "id": "bark_stress",      "data": {"number_value":0.0,"string_value":"BarkStress"}, "chance": 1 },
    { "id": "change_pos",       "data": {"number_value":0.0,"string_value":""}, "chance": 1 },
    { "id": "ignore_command",   "data": {"number_value":0.0,"string_value":""}, "chance": 1 }, ... ],
  "reaction_act_outs": [
    { "id": "block_move",       "data": {"effect":""}, "chance": 0.33 },
    { "id": "comment_self_hit", "data": {"effect":"BarkStress"}, "chance": 0.2 }, ... ] }

// 美德：坚毅（20 抗性 + 专属 buff；只有 3/14 发呆、1/14 自愈压力）
{ "id": "stalwart", "overstress_type": "virtue", "curio_tag": "none", "curio_tag_chance": 0.5,
  "keep_loot": false,
  "buff_ids": ["STUNRESIST20","BLIGHTRESIST20","BLEEDRESIST20","DISEASERESIST20",
               "DEBUFFRESIST20","MOVERESIST20","virtueStalwartBuff1"],
  "combat_start_turn_act_outs": [
    { "id": "nothing",         "data": {...}, "chance": 3 },
    { "id": "stress_heal_self","data": {"number_value":0.0,"string_value":"HealStressVirtued"}, "chance": 1 }, ... ] }
```

每条 trait 的 buff 数量：`fearful/paranoid/selfish/abusive` 12, `irrational` 13, `depressed` 11, `masochistic` 9, `focused/powerful/vigorous` 8, `stalwart/courageous` 7。

`att_outs` 的 `chance` 是**整数权重**（折磨侧：fearful 的 `nothing` 权重 6；美德侧 3），走 `RandomSolver.ChooseByRandom`。`chance` 为 0 的项=不可能发生（保留占位）。

**谁在读它**
| 位置 | 作用 |
|---|---|
| `Database/DarkestDatabase.cs:36, 1816-1850+` | `LoadTraits()` → `Traits: List<Trait>` |
| `Database/DarkestJsonReader.cs:150-160+` | DTO `JsonTraits` / `JsonTrait` |
| `Database/DarkestJsonReader.cs:899-902` | `GetJsonTraits(string)` |
| `Character/Trait.cs:32-47` | 运行时 `Trait`（`StartTurnActs` / `ReactionActs`） |
| `Character/Hero.cs:227` | 存档加载时按 id 找 trait |
| `Managers/RaidSceneManager.cs:4581-4582` | **压力爆表时按 `OverstressType.Virtue/Affliction` 过滤候选池再抽** |
| `Managers/RaidSceneManager.cs:2911` | `RandomSolver.ChooseByRandom(actionHero.Trait.StartTurnActs)` ← 回合开始行为抽签 |
| `Managers/RaidSceneManager.cs:4619, 4624` | 旁白 `virtue` / `afflicted` 带 trait id 播报 |
| `Networking/RaidSceneMultiplayerManager.cs:1335` | 同上（联机版） |

---

## 4. JsonTrinkets.json

### 4.1 顶层结构

```
{ "rarities": [ "darkest_dungeon","ancestral_shambler","ancestral","collector","madman",
                "very_rare","rare","uncommon","common","very_common","trophy","kickstarter" ],
  "trinkets": [ ... ] }     # 488 条，id 唯一
```

`rarities` 是**纯字符串数组（12 个，只有顺序，无附加字段）**。我方 `trinkets.json` 把它加厚成了 `{id, award_category}` 对象数组（14 个），这是我们的改进点。

### 4.2 字段全集（7 个，**488/488 齐备**）

| 字段 | 类型 | 含义 / 取值分布 |
|---|---|---|
| `id` | string | 饰品 id |
| `buffs` | string[] | **数值修改的全部表达方式：buff id 数组**。每条 1~8 个：1×31, 2×85, 3×96, 4×96, 5×60, 6×79, 7×38, 8×3 |
| `hero_class_requirements` | string[] | **职业限制**。84/488 非空，**只出现长度=1 的集合**（即"单职业专属"，没有多职业白名单）：vestal 6, highwayman 6, plague_doctor 6, crusader 6, 其余 12 职业各 5 |
| `rarity` | string | 12 档。**kickstarter 294**（众筹饰品，价格全为 1）/ common 53 / uncommon 41 / rare 31 / very_rare 23 / very_common 16 / ancestral 9 / trophy 9 / ancestral_shambler 5 / madman 3 / collector 3 / darkest_dungeon 1 |
| `price` | int | **价格**，与稀有度严格一一对应：`common 7500, uncommon 10000, rare 15000, very_rare 25000, very_common 5000, ancestral 50000, collector/madman 1000, trophy 0, darkest_dungeon 1, kickstarter 1` |
| `limit` | int | **可持有上限**。`1`×323（唯一）/ `0`×164（=不限，全部是 common/uncommon/rare/very_rare/very_common 这 5 档）/ `3`×1（`dd_trinket`） |
| `origin_dungeon` | string | 掉落来源地牢，仅 29 条非空：`warrens`11, `crypts`10, `weald`8 |

### 4.3 一个饰品改多个属性怎么表示？—— **buff id 数组，一个属性一个 buff**

**没有任何"内联数值"字段。** 饰品 = 纯粹的 buff id 列表。要改 4 个属性就列 4 个 buff id，每个 buff 在 `JsonBuffs.json` 里各自声明 `(stat_type, stat_sub_type, amount, rule_type)`。**这是 DD1 数据模型最关键的一层解耦**：

- 同一 buff id 可被多个饰品/怪癖/特质共享
- 条件型修正 = 换一个 buff id（如 `TRINKET_rangedonly_ACC_B4` 自带 `rangedonly` 规则）
- 因此单个饰品带 8 个 buff 很常见（如 `blood_of_innocent`、`numinis`、`trenche_fist`）

**真实样例**
```jsonc
// 无职业限制、2 个 buff
{ "id": "ancestors_coat", "buffs": ["TRINKET_DEF_B4", "TRINKET_ANCESTOR_STRESSDMG"],
  "hero_class_requirements": [], "rarity": "ancestral", "price": 50000,
  "limit": 1, "origin_dungeon": "" }

// 单职业专属 + 4 个 buff（修 false 值 = 4 个独立 buff）
{ "id": "sacred_scroll",
  "buffs": ["TRINKET_sacred_scroll_STRESSDMG_BUFF", "TRINKET_sacred_scroll_HEALDONE_BUFF",
            "TRINKET_sacred_scroll_STUNSKILL_DEBUFF", "TRINKET_sacred_scroll_SPD_DEBUFF"],
  "hero_class_requirements": ["vestal"], "rarity": "very_rare", "price": 25000,
  "limit": 0, "origin_dungeon": "" }

// 8 个 buff + 条件型（正面4档 / 负面1档，正负抵消设计）
{ "id": "numinis",
  "buffs": ["TB_DMG_B_4_L","TB_DMG_B_4_H","TB_PROT_B_4","TB_MAXHP_B_4",
            "TB_DMG_D_1_LOWHP_L","TB_DMG_D_1_LOWHP_H","TB_PROT_D_1_LOWHP","TB_CRIT_D_1_LOWHP"], ... }
```

**价格怎么被用（注意不是直接售价）**
| 位置 | 逻辑 |
|---|---|
| `Database/DarkestDatabase.cs:424` | `trinket.PurchasePrice = jsonTrinkets[i].price` |
| `UI/Slots/WagonSlot.cs:27, 42` | 流浪货车：`PurchasePrice * (1 - discount)` |
| `UI/Slots/ShopSlot.cs:36` | 商店：`(int)(PurchasePrice * ...)` |
| `UI/Inventory/InventoryItem.cs:594` | 卖出：`PurchasePrice * 0.15` |
| `UI/Controls/TrinketSelloutZone.cs:14` | 丢弃/卖出：`PurchasePrice * 0.15` |
| `Campaign/Town/NomadWagon.cs:63` | 货车按 `PurchasePrice` 降序排列 |

**职业限制怎么被用**
| 位置 | 逻辑 |
|---|---|
| `Database/DarkestDatabase.cs:423` | `trinket.ClassRequirements = hero_class_requirements` |
| `UI/Panels/CharEquipmentPanel.cs:113-115` | 装备时 `!ClassRequirements.Contains(CurrentHero.Class)` → 拒绝 |
| `UI/Inventory/PartyInventory.cs:430-435` | 拖到队伍栏时的同一校验 |
| `UI/Windows/RealmInventoryWindow.cs:250-256` | 同上（仓库界面） |
| `UI/Windows/RealmInventoryWindow.cs:116-125` | 排序时职业专属排后面 |

**限装数**
| 位置 | 逻辑 |
|---|---|
| `Character/Trinket.cs:7` | `EquipLimit` |
| `UI/Panels/CharEquipmentPanel.cs:99` | `EquipLimit == 1` 且已装 → 拒绝 |
| `UI/Inventory/PartyInventory.cs:430` | 同上 |

**谁在读（总入口）**
| 位置 | 作用 |
|---|---|
| `Database/DarkestDatabase.cs:39, 101, 1472-1486` | `LoadJsonTrinkets()` → `Items["trinket"]: Dictionary<string,ItemData>` |
| `Database/DarkestDatabase.cs:405-429` | `GetJsonTrinketLibrary()`：按 id 从 `Buffs` 字典解析 buff 对象 |
| `Database/DarkestJsonReader.cs:628-640` | DTO `JsonTrinket` / `JsonTrinketDatabase` |
| `Character/Trinket.cs:4-32` | 运行时 `Trinket : ItemData`（`Buffs`/`ClassRequirements`/`EquipLimit`/`RarityId`） |
| `Character/Hero.cs:188-193` | 存档左右饰品槽按 id 还原 |
| `Generation/QuestGenerator.cs:262`；`Mechanics/RaidSolver.cs:37, 85, 132` | 奖励/战利品里按稀有度筛饰品 |
| `Campaign/Town/NomadWagon.cs:56-68` | 货车库存 |

---

## 5. 其余四表

### 5.1 JsonCamping.json

**顶层**：`{"configuration": {"class_specific_number_of_classes_threshold": 4}, "skills": [...]}` —— **64 个扎营技能**。

| 字段 | 频次 | 含义 |
|---|---|---|
| `id` | 64 | 技能 id |
| `level` | 64 | **全部为 0** —— 死字段（升级体系没落地） |
| `cost` | 64 | 消耗营地点数：3×26, 4×17, 2×15, 1×5, 5×1 |
| `use_limit` | 64 | 每次扎营可用次数：1×63, 3×1 |
| `effects[]` | 169 | 效果数组（1~5 个/技能）。每条 `{selection, requirements[], chance{code,amount}, type, sub_type, amount}` |
| `hero_classes[]` | 64 | 可学该技能的职业列表。**0 个（通用）的如 `hobby`；1 个的如 `restring_crossbow`；16 个（全职业）的如 `encourage/first_aid/pep_talk`** |
| `upgrade_requirements[]` | 64 | 升级费用。**恒为 1 条**：`{code:"0", currency_cost:[{type:"gold", amount:1750}], prerequisite_requirements:[]}` —— **所有技能升级花费都是 1750 金币，无前置，无层级** |

`effects[].type` 全集（12 种）：
`buff`96, `stress_heal_amount`30, `stress_damage_amount`12, `health_heal_max_health_percent`9, `reduce_ambush_chance`5, `remove_disease`4, `remove_bleeding`3, `remove_poison`3, `loot`3, `remove_deaths_door_recovery_buffs`2, `reduce_torch`1, `health_damage_max_health_percent`1。

`effects[].selection` 3 值：`self` / `party_other` / `individual`。
`effects[].requirements` 5 值：空 154, `religious`6, `not_religious`5, `afflicted`2, `has_deaths_door_recovery_buffs`2。
`effects[].chance` 恒为 `{code: "a"/"b"/"c"/..., amount: 1.0}` —— `code` 只是**同一个技能内效果的槽位字母**，不是概率；`amount` 才是概率（这里全是 1.0）。
`effects[].type == "buff"` 时 **`sub_type` 就是 JsonBuffs 里的 buff id**（34 个 `camping*` 前缀：`campingStressResistBuff`11, `campingACCBuff`7, `campingCRITBuff`7, `campingSPDBuff`5, `campingDEFBuff`5, … `campingHealReceivedBuff`1）。

**真实样例**
```jsonc
{ "id": "encourage", "level": 0, "cost": 2, "use_limit": 1,
  "effects": [ { "selection": "individual", "requirements": [], "chance": {"code":"a","amount":1.0},
                 "type": "stress_heal_amount", "sub_type": "", "amount": 15 } ],
  "hero_classes": ["bounty_hunter","crusader","vestal","occultist","hellion","grave_robber",
                   "highwayman","plague_doctor","jester","leper","arbalest","man_at_arms",
                   "houndmaster","abomination","antiquarian","musketeer"],
  "upgrade_requirements": [ { "code":"0", "currency_cost":[{"type":"gold","amount":1750}],
                              "prerequisite_requirements": [] } ] }

{ "id": "pep_talk", "level": 0, "cost": 2, "use_limit": 1,
  "effects": [ { "selection": "individual", "requirements": [], "chance": {"code":"a","amount":1.0},
                 "type": "buff", "sub_type": "campingStressResistBuff", "amount": -0.15 } ], ... }

{ "id": "first_aid", "level": 0, "cost": 2, "use_limit": 1,
  "effects": [ { "selection":"individual", "requirements":[], "chance":{"code":"a","amount":1.0},
                 "type":"health_heal_max_health_percent", "sub_type":"", "amount":0.15 },
               { "selection":"individual", "requirements":[], "chance":{"code":"b","amount":1.0},
                 "type":"remove_bleeding", "sub_type":"", "amount":0 },
               { "selection":"individual", "requirements":[], "chance":{"code":"c","amount":1.0},
                 "type":"remove_poison", "sub_type":"", "amount":0 } ], ... }
```

**谁在读**：`DarkestDatabase.cs:37, 100, 1501-1538`（`LoadJsonCampingSkills`，含 `StringToCampTargetType`/`StringToCampEffectType` 映射）；`DarkestDatabase.cs:1275`；`DarkestJsonReader.cs:189-250`；`DarkestJsonReader.cs:904-907, 919-922`；`Mechanics/Skills/CampingSkill.cs`、`CampingSkillHelper.cs`。

### 5.2 JsonLoot.json

**顶层**：`{"darkness_bonuses": [2 项], "loot_tables": [54 项]}`

`darkness_bonuses[]`：`{type: "battle"|"chest", bonuses: [{darkness, chance, codes[]}]}` —— **火把亮度 → 战利品码数量**。如 `battle`：亮度 0 → `chance 0.75, codes ["B","B"]`；亮度 76 → `chance 0, codes ["B"]`。

`loot_tables[]`：`{id, difficulty, dungeon, entries[]}`；**54 个表，`(id, difficulty, dungeon)` 是复合主键** —— 同一个 `C` 在 diff 1/3/5/6 各一条；`H` 在 4 个地牢 × 3 难度共 12 条。
难度档：0 / 1 / 3 / 5 / 6。地牢：`""` / `crypts` / `weald` / `warrens` / `cove`。
表 id：`A B C CH CS CT G GH GT H NONE P S SC T Tbase T_COLLECTOR T_ANTIQ_CAMP T_MADMAN T_INCURSIONSACK TORCHONLY SHOVELONLY KEYONLY HOLYONLY SHAMBLER PEW COLLECTOR MADMAN T_ANCESTOR ANTIQ JOURNALONLY THANKS test`

`entries[]`：`{type, chances, data}`，**253 条**。`type` 5 种 → `data` 形状固定：
| type | n | data 字段 |
|---|---|---|
| `item` | 126 | `{type, id, amount}`（物品） |
| `trinket` | 51 | `{rarity}`（按稀有度随机抽） |
| `nothing` | 48 | `{}`（空，配 `chances: 0`） |
| `table` | 25 | `{table}`（**嵌套子表**，递归） |
| `journal_page` | 3 | `{min_page_index, max_page_index}` 或 `{specific_page_index}` |

`chances` 是**权重**（整数为主，也见 0.05 这种小数）。

**真实样例**
```jsonc
{ "id": "A", "difficulty": 0, "dungeon": "", "entries": [
    { "type": "nothing", "chances": 0, "data": {} },
    { "type": "table",   "chances": 14, "data": { "table": "C" } },
    { "type": "table",   "chances": 8,  "data": { "table": "G" } },
    { "type": "table",   "chances": 12, "data": { "table": "H" } },
    { "type": "table",   "chances": 7,  "data": { "table": "S" } },
    { "type": "table",   "chances": 2,  "data": { "table": "T" } },
    { "type": "journal_page", "chances": 0.05, "data": { "min_page_index": 1, "max_page_index": 21 } } ] }
```

**谁在读**：`DarkestDatabase.cs:42, 103, 742-823, 1706`（`GetJsonLootDatabase`：建 `DarknessLoot: Dictionary<string,List<LootEntry>>` 与 `LootTables: Dictionary<string,List<LootTable>>`）；`DarkestJsonReader.cs:527-560, 974-977`；`Database/LootDatabase.cs:5-30`；`Mechanics/RaidSolver.cs:6-156`（`GetLootEntry` 递归解 `type=="table"`，在 `:151-156`）；`Raid/Battle/BattleGround.cs:480`（战斗中按 `DarknessLoot["battle"]` 判定）。

### 5.3 Narration.json

**顶层**：`{"filters": ["plot_darkest_dungeon_4"], "entries": [...36 条...], "total audio_events": 465}`

| 字段 | 频次 | 含义 |
|---|---|---|
| `id` | 36 | 事件 id（代码里 `ExecuteNarration("kill_monster", ...)` 的 key） |
| `tone` | 36 | `bad`20 / `good`9 / `neutral`7（决定本地化/音频子目录） |
| `chance` | 36 | 该事件整体触发概率（多为 1，少数 0.7/0.75/0.85/0.4/0.3/0.9/0） |
| `priority` | 1 | 只在 1 条 entry 上出现 → 近乎死字段 |
| `audio_events[]` | 465 | 每条 1~75 个候选音频 |

`audio_events[]` 10 个字段 **465/465 齐备**：
| 字段 | 含义 |
|---|---|
| `audio_event` | 音频资源路径，`/vo/<tone>/<名>`（443 条 `/vo/`，22 条无斜杠） |
| `chance` | 该条被选中的概率（`1`×193, `0.2`×63, `0.5`×53, `0.25`×39, `0.33`×34, …, `4`×1） |
| `priority` | 优先级（多为 0） |
| `queue_only_on_empty` | 队列非空时不入队 |
| `queue_while_audio_playing` | 正在播也可排队 |
| `max_raid_occurrences` | 单次副本内最多出现次数（1 = 不重复） |
| `max_town_visit_occurrences` | 单次镇上访问上限（0 = 禁用） |
| `max_campaign_occurrences` | 整个战役上限（99 = 近似无限） |
| `filter` | 需满足的剧情过滤器（对应顶层 `filters`）如 `plot_darkest_dungeon_4` |
| `check_all_tags` | `tags` 是全满足还是任一满足 |
| `tags[]` | 剧情标签，如 `plot_kill_necromancer_1`、`crypts`、`quest_start` |

**36 个事件 id**：`loading_screen_start, quest_start, quest_end_completed, quest_end_not_completed, combat_start, kill_monster, kill_hero, crit_monster, crit_hero, deaths_door, victory, battle_retreat, battle_retreat_fail, enter_hallway, torchlight_out, torchlight_full, half_health_half_stress, afflicted, virtue, hunger, hunger_starve, obstacle, obstacle_clear_no_item, curio, trap, loot, camp, recruit_hero, dismiss_hero, upgrade_building, town_visit_start, enter_quest_select, enter_provision_select, enter_building, ancestor_talk, change_monster_class`

**真实样例**
```jsonc
{ "id": "ancestor_talk", "tone": "neutral", "chance": 1, "audio_events": [
    { "queue_only_on_empty": false, "queue_while_audio_playing": true,
      "audio_event": "/vo/neutral/darkest_04_obstacle_01", "chance": 1, "priority": 0,
      "max_raid_occurrences": 1, "max_town_visit_occurrences": 0, "max_campaign_occurrences": 99,
      "filter": "plot_darkest_dungeon_4", "check_all_tags": true, "tags": ["ancestor_talk_1"] }, ... ] }
```

**谁在读**：`DarkestDatabase.cs:34, 116, 1415-1444`（`LoadNarration` → `Dictionary<string,NarrationEntry>`）；`DarkestJsonReader.cs:40-60, 884-887`；`Campaign/NarrationEntry.cs`、`Campaign/NarrationAudioEvent.cs:24-52`（`IsPossible` 按 `max_*_occurrences` 与存档计数判定）；`Managers/DarkestSoundManager.cs:54-146`（`ExecuteNarration` 主逻辑：入队、计次、过滤）；**36 个 id 在 20+ 处被调用**（`RaidSceneManager.cs:155-5933` 共 30 次、`EstateSceneManager.cs:209-725`、`RaidSceneMultiplayerManager.cs:344-2026`、`ScreenLoader.cs:34-49`、`CharacterWindow.cs:145`、各建筑窗口等）。

> 用途：**这不是数据表，是"语音播报规则表"**。核心创新是 `max_raid/town/campaign_occurrences` 三级计数上限 —— 保证同一个祖先旁白在一个副本/一次镇访/整个战役里不重复。我方若无此表，等于没有旁白系统。

### 5.4 PartyNames.json

**顶层**：`{"party_names": [...]}` —— **186 条**，字段只有 2 个（`id` 186 / `required_hero_class` 186）。

- `id`：`"0"` ~ `"185"`（纯数字字符串 → **是索引，不是名字**；真实队名在本地化表 `PartyNames` 分类里，见 `LocalizationManager.cs:36`）
- `required_hero_class`：**恒为 4 个职业字符串**（186/186 长度=4，允许重复，如 `["occultist","occultist","hellion","hellion"]`）

**真实样例**
```jsonc
{ "id": "0", "required_hero_class": ["vestal","plague_doctor","highwayman","crusader"] }
{ "id": "3", "required_hero_class": ["occultist","occultist","hellion","hellion"] }
```

**用途**：队伍组建时按（无序？有重复的）职业组合匹配 → 用 `id` 去本地化表取"这支队伍的名字"（DD1 里记忆点/播报彩蛋）。

**谁在读**：`DarkestDatabase.cs:35, 117, 1401-1411`（`LoadPartyNames` → `List<PartyNameEntry>`）；`DarkestJsonReader.cs:879-882`；`Campaign/PartyNameEntry.cs`；`UI/Panels/PartyCompositionPanel.cs:16`（`Data.PartyNames.Find(entry => ...)`）。

---

## 6. 🆕 我方对应与缺口

我方数据目录：`F:\GithubPro\Darkest\darkest\data\`

| 参考表 | 我方对应文件 | 状态 | 差距 |
|---|---|---|---|
| `JsonBuffs.json` (1801) | `buff_defs.json` (**22**) | ⚠️ **数量级不足** | 我方 22 条只覆盖"状态型/旗标型" buff（`stun/taunt/mark/bleed/shield/guard_attach/affliction_*/virtue_*/next_*` + 少量 curio），**完全没有数值修正型 buff**（命中/暴击/伤害/速度/防御/保护/各抗性） |
| `JsonBuffs.json` 的 `rule_type` | **无对应** | 🔴 **整块缺失** | 我方无"buff 触发条件"概念（亮暗/回合/位次/HP 阈值/怪物类型/远近战/地牢/形态/活动） |
| `JsonAI.json` (160) | `enemy_ai.json` (**3 个 archetype**) | ⚠️ 骨架对上了，规模差 50× | 我方已有 `archetype_id / target_preference / random{enabled,fallback_probability} / rules[{skill_id, when{...}, mark_weight}]`；**有 `when` 条件（`self_slot_in` / `player_morale_all_at_least` / `target_slots_occupied_min`）和 `mark_weight` 权重**，方向正确。缺：技能冷却、per-brain 难度档 A/B/C、"欲望类型"分类、boss 专属 scripted brain、先手欲望 |
| `JsonQuirks.json` (163) | `quirks.json` (**170**) | ✅ **最好的一张** | 条目数甚至更多，字段是超集：多了 `show_flavor_description` / `show_explicit_curio_tag_description` / `random_chance` / `can_modify_in_activity` / `can_be_replaced_by_new_quirk` / `origin`。**`is_disease` 和 `incompatible_quirks` 都在** ✅ |
| `JsonTraits.json` (12) | `traits.json` (**7**) | ⚠️ **语义错位** | 我方 `traits.json` 是**自研的 7 个折磨**（`affliction_fear/selfish/...` + `id/name/kind/ends_at/modifiers/hooks/source`），**没有 DD1 的 5 个美德**，**没有 14 项回合开始行为表**（`nothing/bark_stress/change_pos/ignore_command/random_command/retreat_from_combat/attack_friendly/attack_self/mark_self/buff_party/heal_self`…），**没有 15 项反应行为表**（`block_move/comment_*/block_effect`…），**没有 `overstress_type` 的 virtue/affliction 二态过滤抽签** |
| `JsonTrinkets.json` (488) | `trinkets.json` (**196**) | ⚠️ 已有但对齐的是**另一套源** | 我方按策划 #452 从 E 盘一手数据转写，结构**与参考表逐字段同构**（`id/buffs/hero_class_requirements/rarity/price/limit/origin_dungeon`，多 `award_category`/`origin`）。⚠️ 但缺口在别处：**`buffs` 字段引用的 656 个 `TRINKET_*` id 在 `buff_defs.json` 里一个都不存在**（见 §7 发现 1） |
| `JsonTrinkets.json` 的 `rarities` | `trinkets.json.rarities`（14 个对象） | ✅ 我方更强 | 我方加了 `award_category` |
| `JsonCamping.json` (64) | `camp_skills.json` (**12**) | ⚠️ **规模不足，且结构过简** | 我方 12 条只有 `id/name/owner_unit/cost/target/effect("grant_buff:xxx")/source`。**没有 `effects[]` 数组**（一招多效）、**没有 `requirements`**（religious/not_religious/afflicted）、**没有 `hero_classes` 全职业归属表**（用 `owner_unit` 单职业代替）、**没有扎营技能升级费用** |
| `JsonLoot.json` (54 表) | `economy.json`（只有 `battle_reward`/`light_tier_bonus`/`event_reward`）⚠️ 部分对应；`room_contents.json`(0.8KB) | 🔴 **缺"战利品表"整表** | 我方只有"战斗奖励公式 + 亮度档加成"，**没有 id 化、难度×地牢分表、可嵌套子表的 `loot_tables`**，**没有按稀有度抽饰品的 `trinket` 条目类型**，**没有 `journal_page`** |
| `Narration.json` (36 事件/465 音频) | **无对应** | 🔴 **整表缺失** | 我方 `darkest/data/` 下没有任何旁白/语音表 |
| `PartyNames.json` (186) | **无对应**（`roster.json` 是 roster 上限/等级/经验，不同用途） | 🔴 **整表缺失** | 我方无"队伍职业组合 → 队名"表 |
| `JsonAI.json` 的怪物 brain 绑定 | `units.json`(10KB) | — | 参考项目在怪数据里存 `MonsterBrainId`（`Monster.cs:39`）；我方需确认 `units.json` 是否有 brain id 字段 |

**我方 `darkest/data/` 里参考项目没有的表（我们的增量）**：`buildings.json`, `curios.json`, `expedition_map.json`, `expedition_nodes.json`, `formation.json`, `heirlooms.json`, `heirloom_exchange.json`, `hero_upgrades.json`, `sanitarium.json`, `skills.json`, `trap_defs.json`, `tuning.json`, `unlocks.json`, `room_contents.json`, `morale_events.json`, `tuning.json`。

---

## 7. 🏆 三条最值钱的发现

### 发现 1 🔴 我方 buff 引用链**完全断裂**：556 个被引用的 buff id，`buff_defs.json` 里定义了 0 个

用脚本扫描 `darkest/data/*.json` 里所有 `"buffs": [...]` 数组：
- 全库共引用 **556 个不同 buff id**（`quirks.json` 182 个 + `trinkets.json` 374 个，有交集）
- `buff_defs.json` 只定义 **22 个**，且 **22 个全部无人引用**（`defined but never referenced` = 22/22）
- **`referenced but NOT defined` = 556/556 = 100% 断裂**

样例悬挂引用：`ACC-10`, `ACC-5`, `ACC5`, `ACCRANGED-5`, `BLEEDRESIST-10`, `BLEEDRESIST-40`, `MAXHP10`, `DMGL-25`, `STUNRESIST20`, `TRINKET_ANCESTOR_STRESSDMG` …
即：**我方 `quirks.json`(170) 和 `trinkets.json`(196) 的效果目前一条都不会生效**。参考项目里 `DarkestDatabase.cs:393-399 / 416-422 / 1829-1830` 三处都是 `Buffs[id]` 字典查找（饰品/怪癖/特质共用一个 buff 池）。

**动作**：把 `JsonBuffs.json` 的 1801 条（或至少被引用的子集）导入成我方 buff 定义，并把 `buff_defs.json` 从"22 条状态型"扩成"**状态型 + 数值修正型**"两层。

### 发现 2 🔴 DD1 的 buff 原语只有两层、41 个组合、字段 9+2 —— 我方可以直接照抄这个 schema

**参考项目的 buff 不是"一个原语一个类"，而是 `(修改器种类, 属性) × 触发规则 × 参数` 的纯数据笛卡尔积**：
```
buff = (stat_type, stat_sub_type, amount)          # 25 × 20 → 实际用到 41 组合
     + (rule_type, rule_data.float, rule_data.string, is_false_rule)   # 23 种触发条件
     + (duration_type, duration)                    # 5 种时长（仅 63/1801 用）
```
- **`stat_type` 只有 25 个，`stat_sub_type` 只有 20 个，`rule_type` 只有 23 个** —— 加起来 68 个字符串常量就能表达 1801 条 buff。
- **`is_false_rule` 是"条件取反"的通用开关**，所以 `in_rank(3, false)` = 在 3 号位，`in_rank(3, true)` = 不在 3 号位，**不需要为每个规则写正反两版**（60 条在用）。这是极省 code 的设计。
- **`rule_data` 恒为 `{float, string}` 两个槽**，用同一张表同时承载"数值阈值"（lightbelow 50 / hpbelow 0.25 / monsterSize 2）和"字符串枚举"（monsterType=eldritch / actorStatus=tagged / in_dungeon=crypts / in_activity=meditation）。**不需要 union 类型。**
- 我方现有的 4 个 modifier kind（`damage_mod` 9 / `prob_mod` 7 / `state_flag` 3 / `stat_mod` 1）在语义上和 `combat_stat_multiply` / `*_chance` / `state_flag` / `combat_stat_add` **一一对应** —— 分级结构是对的，只是原语粒度太粗（我方的 `effect: "refuse_skill"` 是"复合语义"，参考项目会拆成 `(combat_stat_multiply, damage_low, -0.33, always)` 或专门的 `*_chance` 原语）。

**动作**：`buff_defs.json` 增加 `rule` 子对象（`{type, is_false_rule, float_param, string_param}`）与 `modifier_kind` 枚举（`add` / `multiply` / `percent`），把 41 个原语名固化成我方 `data_schema` 的受控词表。

### 发现 3 🟡 AI 决策是"加权轮盘 + 拒绝重试"，**没有评分/效用函数**，且参考实现有两个被丢弃的字段

`BattleSolver.cs:161-185`：`while(欲望集非空) { 加权随机抽一个 → 让它自己 `IsRestricted()` 判门槛 → 成则返回，败则 Remove 重抽 }`；权重就是 `base_chance`（`RandomSolver.cs:33-44` 轮盘）。目标选择同构（`SkillSelectionDesire.cs:38-46` / `BattleSolver.cs:201-210`）。

**可抄的部分**：
- **"欲望声明自己是否成立"（`IsRestricted` 13 个数值比较，`SkillSelectionDesire.cs:52-95`）把条件判定下推到数据层** —— 我方 `enemy_ai.json` 的 `when{self_slot_in / target_slots_occupied_min / player_morale_all_at_least}` 方向一致，可扩成同一套 13 个计数器。
- **"抽取失败就剔除重抽"**天然实现优先级回退（高权重不成立 → 自动落到低权重），不需要 if-else 优先级链。
- **`base_chance` 缩放不一致是 bug 级别的不一致**：技能欲望 `×100`（`SkillSelectionDesire.cs:129`），目标欲望 `×1`（`TargetSelectionDesire.cs:105`）。两边各自独立归一，所以行为上没错，但**我方实现应当统一**。

**不要抄的部分（参考项目的实现缺陷）**：
- `is_exclusive_desire`（51 条 `trinket`/target 数据在用）→ `TargetSelectionDesire.cs:110-111` 解析后 `break;` 丢弃
- `is_pre_turn`（21 条在用）→ `BonusInitiativeDesire.cs:36-37` 解析后 `break;` 丢弃
- `JsonCamping.json` 的 `level`（64/64 全为 0）与 `Narration.json` 的 `priority`（只有 1 条）→ 死字段
- `JsonBuffs.json` 的 `remove_if_not_active`（1800/1801 为 false）→ 近乎死字段

---

## 8. 我方优先级建议（供 parent 决策）

| 优先级 | 事项 | 依据 |
|---|---|---|
| **P0** | 修 **556 条悬挂 buff 引用**（发现 1）—— 否则 quirks/trinkets 全是死数据 | §7 发现 1 |
| **P0** | 给 `buff_defs.json` 加 **`rule`（触发条件）子层 + 41 个受控原语名**（发现 2） | §1.3 / §1.4 |
| **P1** | `enemy_ai.json` 从 3 个 archetype 扩到覆盖全部怪 + 加难度档 A/B/C + 技能冷却 | §2.1 |
| **P1** | `traits.json` 补 **5 个美德** + **14 项回合开始行为表** + **15 项反应行为表** + `overstress_type` 二态抽签 | §3.2 |
| **P2** | `camp_skills.json` 从 12 → 64，补 `effects[]` 数组 / `requirements` / `hero_classes` / 升级花费 | §5.1 |
| **P2** | **新建 loot 表**（`loot_tables` id 化 + 难度×地牢 + 可嵌套） | §5.2 / §6 |
| **P3** | 新建 `narration.json`（36 事件 + 三级出现次数上限）与 `party_names.json`（186 组合） | §5.3 / §5.4 |

---

## 9. 附录：统计脚本（可复现）

脚本落在 `reports/unity_ref/_scan_tables.py`（主）与 `_scan_tables2.py`（补充），输出 `_scan_out.txt` / `_scan_out2.txt`。核心片段：

```python
# -*- coding: utf-8 -*-
# 关键点 1：宽容解析（JsonAI.json 有尾随逗号，严格 json 会炸）
import io, json, os, collections, re
REF  = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
TRAILING = re.compile(r",(\s*[\]\}])")
def load(name):
    with io.open(os.path.join(REF, name), "r", encoding="utf-8-sig") as f:
        raw = f.read()
    try:
        return json.loads(raw)
    except Exception:
        return json.loads(TRAILING.sub(r"\1", raw))

# 关键点 2：递归收集点号路径字段频次（不打印正文，只打印计数）
def walk_keys(obj, prefix, freq, depth=0, maxdepth=6):
    if depth > maxdepth: return
    if isinstance(obj, dict):
        for k, v in obj.items():
            p = prefix + "." + k if prefix else k
            freq[p] += 1
            walk_keys(v, p, freq, depth + 1, maxdepth)
    elif isinstance(obj, list):
        for el in obj: walk_keys(el, prefix + "[]", freq, depth + 1, maxdepth)

# 关键点 3：buff 原语 = (stat_type, stat_sub_type) 组合，直接 Counter
buffs = load("JsonBuffs.json")["buffs"]
primitives = collections.Counter((x.get("stat_type"), x.get("stat_sub_type")) for x in buffs)
for (a, c), n in primitives.most_common():
    print(a, "|", repr(c), n)

# 关键点 4：只取 2~3 条样例，切片后 json.dumps，绝不整体读入
print(json.dumps(buffs[:3], ensure_ascii=False, indent=1)[:3000])

# 关键点 5：中文只写文件，stdout 只出 ASCII
OUT = io.open(r"F:\GithubPro\Darkest\reports\unity_ref\_scan_out.txt", "w", encoding="utf-8")
OUT.write(u"### JsonBuffs\n条目数: %d\n" % len(buffs))
```

补充脚本还统计了：
- 交叉表 `(rule_type, stat_type)`、`(desire_type, tuple(sorted(data.keys())))`
- 我方悬挂引用检测：遍历 `darkest/data/*.json` 里所有 `"buffs": [...]`，与 `buff_defs.json` 的 id 集合做差

```python
# 发现 1 的检测代码
defs = set(x["id"] for x in json.load(io.open("buff_defs.json", encoding="utf-8"))["buffs"])
refs = set()
for fn in glob.glob("*.json"):
    d = json.load(io.open(fn, encoding="utf-8"))
    def walk(o):
        if isinstance(o, dict):
            for k, v in o.items():
                if k == "buffs" and isinstance(v, list):
                    refs.update(x for x in v if isinstance(x, str))
                else: walk(v)
        elif isinstance(o, list):
            for x in o: walk(x)
    walk(d)
print(len(refs), len(defs), len(refs - defs))   # -> 556 22 556
```

---

*报告完。所有结论均由脚本统计产出，未将任何大文件整体读入上下文；仅描述语义，未逐行誊抄 GPL 代码。*
