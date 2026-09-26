# 04a — 城镇经济（Town Economy）Unity 参考实现取证报告

> 取证源（SOURCE OF TRUTH）：`F:\GithubPro\Darkest-Dungeon-Unity`
> （Unity 复刻，近乎逐字读取 DD1 数据；其**值**视为权威）。
> 范围：**仅城镇经济** —— 建筑、建筑升级、补给品/物品、Curios、价格修正。战斗/技能/增益/任务不在本文件范围。
> 规则：每条断言都带 `file:line` 或精确 JSON 路径；**数据直述**与 `[推断]`（来自代码）严格区分。
> 本文件在取证过程中被多次追加/重写，**全部小节已完成取证**（无「待填充」遗留）；所有计数均由 `_probe/` 下脚本实测得出，脚本清单见文末附录。

## 0. 取证方法与计数口径

- 数据根目录：`F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\`。
  计数方式：`Get-ChildItem -Path ...\Data -Recurse -File | Measure-Object` → **674** 个文件（含 `.meta`）。
- 建筑主数据：`Data/Buildings/`，共 **8** 个 `*.building.json`（`Get-ChildItem -Filter *.building.json`）。
  逐个为：`abbey`、`blacksmith`、`camping_trainer`、`guild`、`nomad_wagon`、`sanitarium`、`stage_coach`、`tavern`。
- 建筑升级树：`Data/Upgrades/Building/`，共 **8** 个 `*.upgrades.json`，与上面 8 个建筑**同名一一对应**。
  **没有** `Data/Upgrades/Building/*.building.json` 之类别的命名。
- Curios：`Data/Curios/Curios.csv`（另有 `Obstacles.json`、`Traps.json`，两者非经济）。
- 物品：`Data/Inventory/Items.bytes`（二进制/序列化，非 JSON）。
- **重要解析陷阱（实测，逐行复核）**：`abbey`、`nomad_wagon`、`sanitarium`、`tavern` 四个 `*.building.json` **含尾随逗号**，
  严格 `json.loads()` 直接抛 `JSONDecodeError`；其余 `blacksmith`、`camping_trainer`、`guild`、`stage_coach` 四个文件
  严格解析 **OK**。任务描述属实。
  判定方法（`_probe/q05_trailing.py`、`_probe/q06_trailing_detail.py`）：对整文件跑 `re.finditer(r",(\s+)([\]\}])", raw)`，
  取逗号所在行号（实测 `\s+` 与 `\s*` 两个版本在 8 个文件上给出**完全相同**的行号集合，
  即这批文件里**不存在** `,]`/`,}` 紧贴零空白的写法）；再用 `re.sub(r",(\s*[\]}])", r"\1", raw)` 删掉这些逗号后重新严格 `json.loads()`，
  **四个文件全部转为可解析**，说明该清单**完整且无遗漏**（不是「列出所有以逗号结尾的行」那种宽松匹配）。
  实测尾随逗号清单（行号，1-based）：
  - `Buildings/abbey.building.json`（299 行，5834 B）→ **10 处**：`19`、`28`、`36`、`44`、`52`、`60`、`68`、`69`、`160`、`258`。
    其中 `19/28/36/44/52/60/68` 是 `],` 后紧跟 `}`；`69/160/258` 是 `},` 后紧跟 `]`。
  - `Buildings/sanitarium.building.json`（67 行，3834 B）→ **2 处**：`41`、`65`，均为 `"quirk_treatment_chance": 1.0,` 后紧跟 `}`（最后一个成员后的逗号）。
  - `Buildings/tavern.building.json`（319 行，7011 B）→ **1 处**：`281`（`},` 后紧跟 `]`，位于 `brothel` 的 `side_effects` 结果数组末尾）。
  - `Buildings/nomad_wagon.building.json`（23 行，1220 B）→ **1 处**：`22`（`],` 后紧跟 `}`）。
  - 4 个严格合法文件：`blacksmith`（14 行）、`camping_trainer`（14 行）、`guild`（14 行）、`stage_coach`（33 行），尾随逗号 **0** 处。
- **对照实测**：`Data/Upgrades/Building/` 下 8 个 `*.upgrades.json` **全部 8/8 严格解析 OK**（0 处尾随逗号）；
  `Data/Mechanics/` 下 `Campaign.json` / `HeirloomExchange.json` / `Provision.json` / `Roster.json` 亦全部 OK。
  同目录的 `Traps.json`（非经济）含 **8** 处尾随逗号，`Items.bytes`、`Curios.csv` 不是 JSON（预期失败）。
  本报告统一使用的清洗手段：`re.sub(r",(\s*[\]}])", r"\1", raw)`（行感知状态机版本见 `_probe/ddulib.py:23-77`）。

---

## 1. 建筑总览（Buildings）

### 1.1 全部 8 个建筑的顶层字段（实测）

每个 `*.building.json` 都是「3 个全局标量 + 若干具名字段」的扁平结构。
三个标量在每个文件里都存在且**位置固定在前三/四行**：

| 字段 | 语义（数据直述） | 证据 |
|---|---|---|
| `on_start_town_visit_priority` | 城镇界面里建筑的排序/优先级 | `Buildings/stage_coach.building.json:2` = `0`；其余 7 个建筑 = `1` |
| `number_of_quests_finished` | 该建筑「解锁/可升级」所需已完成任务数 | `Buildings/sanitarium.building.json:3` = `4` |
| `highest_dungeon_level` | 所需最高地牢等级 | `Buildings/camping_trainer.building.json:4` = `2` |

逐建筑标量真值（数据直述，全部来自各文件第 2–4 行）：

| building id | `on_start_town_visit_priority` | `number_of_quests_finished` | `highest_dungeon_level` | 出处 |
|---|---|---|---|---|
| `abbey` | 1 | 2 | 0 | `abbey.building.json:2-4` |
| `blacksmith` | 1 | 3 | 0 | `blacksmith.building.json:2-4` |
| `camping_trainer` | 1 | 0 | 2 | `camping_trainer.building.json:2-4` |
| `guild` | 1 | 3 | 0 | `guild.building.json:2-4` |
| `nomad_wagon` | 1 | 0 | 2 | `nomad_wagon.building.json:2-4` |
| `sanitarium` | 1 | 4 | 0 | `sanitarium.building.json:2-4` |
| `stage_coach` | 0 | 0 | 0 | `stage_coach.building.json:2-4` |
| `tavern` | 1 | 2 | 0 | `tavern.building.json:2-4` |

`[推断]` 这三个字段是**该建筑整体（不是单个活动）**的解锁门槛，因为它们在每个文件里只出现一次且不在任何活动对象内部；
`stage_coach` 三项全为 `0` 也符合「运马车是开局就有的建筑」。具体消费代码见 §6。

### 1.2 八类建筑的字段构成（实测，逐个记录）

**A. `blacksmith`（铁匠铺）** — 顶层键：`on_start_town_visit_priority, number_of_quests_finished, highest_dungeon_level, equipment_cost_discount_upgrades`。
唯一经济字段 `equipment_cost_discount_upgrades`：**5** 个条目，每条 `discount_percent = 0.10`，
分别绑定 `blacksmith.cost` 树的 `a..e`（`blacksmith.building.json:6-13`）。
`[推断]` 即「cost 树 5 级，每级装备价格 −10%，共 −50%」。

**B. `guild`（公会）** — 顶层键：`..., combat_skill_cost_discount_upgrades`。
**5** 个条目，每条 `discount_percent = 0.10`，绑定 `guild.cost` 树的 `a..e`（`guild.building.json:6-13`）。

**C. `camping_trainer`（扎营教练）** — 顶层键：`..., camping_skill_cost_discount_upgrades`。
**5** 个条目，每条 `discount_percent = 0.10`，绑定 `camping_trainer.cost` 树的 `a..e`（`camping_trainer.building.json:6-13`）。
注意 `guild` 与 `camping_trainer` 的**唯一区别**就是绑定树的 id 与折扣字段名。

**D. `nomad_wagon`（游牧货车）** — 顶层键：`..., number_of_trinkets_upgrades, trinket_cost_discount_upgrades`。
- `number_of_trinkets_upgrades`：**5** 个条目，`number_of_slots` = `2, 4, 6, 8, 12`（`nomad_wagon.building.json:6-13`）。
  第 1 条**没有** `upgrade_tree_id`（默认等级），后 4 条绑定 `nomad_wagon.numitems` 的 `a..d`。
- `trinket_cost_discount_upgrades`：**5** 个条目，每条 `discount_percent = 0.10`，绑定 `nomad_wagon.cost` 的 `a..e`（`nomad_wagon.building.json:15-22`）。

**E. `stage_coach`（驿站马车）** — 顶层键：`..., number_of_recruits_upgrades, roster_size_upgrades, upgraded_recruits_upgrades`。
- `number_of_recruits_upgrades`：**6** 个条目，`number_of_slots` = `2, 3, 4, 5, 6, 7`（`stage_coach.building.json:6-14`），
  绑定 `stage_coach.numrecruits` 的 `a..e`。
- `roster_size_upgrades`：**6** 个条目，`number_of_slots` = `9, 12, 15, 18, 21, 25`（`stage_coach.building.json:15-23`），
  绑定 `stage_coach.rostersize` 的 `a..e`。注意**第 6 级只 +4**（21→25），不是 +3。
- `upgraded_recruits_upgrades`：**3** 个条目（`stage_coach.building.json:24-32`），字段为
  `level` / `chance` / `number_of_extra_positive_quirks` / `number_of_extra_negative_quirks` /
  `number_of_extra_combat_skills` / `number_of_extra_camping_skills` / `guaranteed_previous_raid_dead_hero_levels`：
  - `level:1, chance:0.1875`（=3/16），+1 正面怪癖，+1 负面怪癖，+0 战斗技能，+1 扎营技能，`guaranteed_previous_raid_dead_hero_levels:[3]`（`:26`）
  - `level:2, chance:0.125`（=2/16），+2/+2，+1 战斗技能，+1 扎营技能，`[4]`（`:28`）
  - `level:3, chance:0.0625`（=1/16），+3/+3，+2 战斗技能，+2 扎营技能，`[5,6]`（`:30`）
  `[推断]` chance 序列 3/16、2/16、1/16 是 DD1 经典的「招募位逐个判定、命中即停」叠加概率；
  参考数据未给出总概率，代码也未在此文件里给出，**故总概率不可从参考中直接读出**。

**F. `sanitarium`（疗养院）** — 顶层键：`..., treatment, disease_treatment` 两个「活动」对象。
`treatment` 子键：`positive_quirk_cost_upgrades`、`negative_quirk_cost_upgrades`、
`permanent_negative_quirk_cost_upgrades`、`slot_upgrades`、`quirk_treatment_chance`。
`disease_treatment` 子键：`disease_quirk_cost_upgrades`、`disease_quirk_cure_all_chance_upgrades`、
`slot_upgrades`、`quirk_treatment_chance`。

**G. `tavern`（酒馆）** — 顶层键：`..., bar, gambling, brothel` 三个「活动」对象。
每个活动子键固定 7 个：`side_effects`、`quirk_library_names`、`caretaker_friendly`、
`cost_upgrades`、`slot_upgrades`、`stress_upgrades`、`affliction_cure_upgrades`（`tavern.building.json:7-107`、`:110-218`、`:220-318`）。

**H. `abbey`（修道院）** — 顶层键：`..., meditation, prayer, flagellation` 三个活动对象，
结构与 `tavern` **完全同构**（同样 7 个子键），见 `abbey.building.json:6-111`、`:113-202`、`:203-298`。
差异只在数值与 `side_effects` 内容。

> 计数口径：上表「N 个条目」= 用 `json.loads` 解析清洗后对象，对该 key 取 `len(list)`，脚本 `_probe/q01_town.py` 输出 `_probe/o50_buildings.txt`。

### 1.3 每个等级「解锁什么」——活动类建筑（数据直述）

对 `abbey` 的 3 个活动与 `tavern` 的 3 个活动，四张升级表的语义由字段名直接给出：

| 字段 | 数据直述的含义 | 条目数（每个活动） |
|---|---|---|
| `cost_upgrades` | 该活动**治疗/服务单价**的逐级覆盖值（`cost_currency`） | 3 |
| `slot_upgrades` | 该活动**同时可服务的人数（槽位）** | 3 |
| `stress_upgrades` | 该活动每次服务**减压量**（`heal_low`/`heal_high`） | 3 |
| `affliction_cure_upgrades` | 该活动**治愈折磨（affliction）的概率** `chance` | 2 |

逐活动真值（全部为数据直述）：

| 活动 | cost 三级 (gold) | slots 三级 | stress 三级 (heal_low=heal_high) | affliction chance 二级 |
|---|---|---|---|---|
| `tavern.bar` | 1000 / 850 / 700 `tavern.building.json:83-85` | 1 / 2 / 3 `:90-92` | 45 / 56 / 70 `:97-99` | 1.00 / 0.10 `:104-105` |
| `tavern.gambling` | 1250 / 1050 / 900 `:194-196` | 1 / 2 / 3 `:201-203` | 55 / 69 / 86 `:208-210` | 1.00 / 0.10 `:215-216` |
| `tavern.brothel` | 1500 / 1300 / 1100 `:294-296` | 1 / 2 / 3 `:301-303` | 65 / 81 / 100 `:308-310` | 1.00 / 0.10 `:315-316` |
| `abbey.meditation` | 1000 / 850 / 700 `abbey.building.json:87-89` | 1 / 2 / 3 `:94-96` | 45 / 56 / 70 `:101-103` | 1.00 / 0.10 `:108-109` |
| `abbey.prayer` | 1250 / 1050 / 900 `:178-180` | 1 / 2 / 3 `:185-187` | 55 / 69 / 86 `:192-194` | 1.00 / 0.10 `:199-200` |
| `abbey.flagellation` | 1500 / 1300 / 1100 `:274-276` | 1 / 2 / 3 `:281-283` | 65 / 81 / 100 `:288-290` | 1.00 / 0.10 `:295-296` |

关键实测：**修道院三个活动与酒馆三个活动的成本/槽位/减压数值一一相同**，
即 `bar≡meditation`、`gambling≡prayer`、`brothel≡flagellation`（对齐上面两组的行号即可验证）。

`upgrade_requirement_code` 的绑定关系（数据直述，**两栋建筑完全一致**）：
- `cost_upgrades[1]` → `"b"`，`cost_upgrades[2]` → `"e"`
- `slot_upgrades[1]` → `"c"`，`slot_upgrades[2]` → `"f"`
- `stress_upgrades[1]` → `"a"`，`stress_upgrades[2]` → `"d"`
- `affliction_cure_upgrades[1]` → `"g"`
- `cost_upgrades[0]` / `slot_upgrades[0]` / `stress_upgrades[0]` / `affliction_cure_upgrades[0]` **没有该字段** = 默认等级。

注意：`tavern`/`abbey` 用**裸** `upgrade_requirement_code`（不带 `upgrade_tree_id`），
而 `sanitarium`/`nomad_wagon`/`stage_coach` 用 `upgrade_tree_id` + `upgrade_requirement_code` 成对出现
（例：`sanitarium.building.json:10`、`nomad_wagon.building.json:9`、`stage_coach.building.json:9`）。
`[推断]` `tavern`/`abbey` 的树 id 由 `<building>.<activity>` 命名约定隐式决定（`abbey.meditation`/`tavern.bar`…），
所以不需要显式写；这一点由 §2 的 upgrade 文件名/树名完全吻合佐证。

**活动建筑的额外经济字段**：
- `caretaker_friendly: true` —— 6 个活动全部为 `true`（`tavern.building.json:79, 190, 290`；`abbey.building.json:83, 174, 270`）。
  `[推断]` 控制「管家/看护者」类城镇事件是否作用于此活动；参考数据里**没有**其他 `caretaker_friendly` 出现位置。
- `quirk_library_names` —— 每活动 6 或 7 个怪癖 id，是**副作用候选池**，非价格。例：`abbey.prayer` 有 **7** 个（`abbey.building.json:164-173`），其余 5 个活动各 **6** 个。
  > 注意拼写：数据里是 `quirk_library_names`（复数 `names`），而 `side_effects` 内部的引用键是 `quirk_library_name`（单数，`abbey.building.json:35`）。
- `side_effects.chance` 与 `side_effects.results[].type` —— 与价格无关，属城镇风险机制，本报告不展开；
  但其中**与经济直接相关**的两种 `type` 值得点出：
  - `change_currency`：`tavern.bar` 可 `gold -500`（`tavern.building.json:60`）；
    `abbey.prayer` 可 `gold -1000`（`abbey.building.json:158`）；
    `tavern.gambling` 可 `gold +500` 或 `gold -500`（`tavern.building.json:162-163`）。
  - `remove_trinket` / `add_trinket`：`tavern.bar` 只会 `remove_trinket`（`:64-70`），`tavern.gambling` 两者都有（`:167-181`）。

### 1.4 每个等级「解锁什么」——功能类建筑（数据直述）

| 建筑 | 升级维度 | 逐级值 | 绑定树/code | 出处 |
|---|---|---|---|---|
| `nomad_wagon` | 饰品货位数 | 2 → 4 → 6 → 8 → 12 | `nomad_wagon.numitems` a/b/c/d | `nomad_wagon.building.json:8-12` |
| `nomad_wagon` | 饰品价格折扣 | 每级 −10%（5 级） | `nomad_wagon.cost` a..e | `nomad_wagon.building.json:17-21` |
| `stage_coach` | 可招募人数 | 2 → 3 → 4 → 5 → 6 → 7 | `stage_coach.numrecruits` a..e | `stage_coach.building.json:8-13` |
| `stage_coach` | 名册上限 | 9 → 12 → 15 → 18 → 21 → 25 | `stage_coach.rostersize` a..e | `stage_coach.building.json:17-22` |
| `stage_coach` | 高阶新兵概率/属性 | 3 档 `level` 1/2/3 | `stage_coach.upgraded_recruits` a/b/c | `stage_coach.building.json:26-31` |
| `blacksmith` | 装备价格折扣 | 每级 −10%（5 级） | `blacksmith.cost` a..e | `blacksmith.building.json:8-12` |
| `guild` | 战斗技能价格折扣 | 每级 −10%（5 级） | `guild.cost` a..e | `guild.building.json:8-12` |
| `camping_trainer` | 扎营技能价格折扣 | 每级 −10%（5 级） | `camping_trainer.cost` a..e | `camping_trainer.building.json:8-12` |
| `sanitarium` | 正面怪癖治疗单价 | 7500 → 6750 → 6000 → 5250 → 4500 → 3750 | `sanitarium.cost` a..e | `sanitarium.building.json:9-14` |
| `sanitarium` | 负面怪癖治疗单价 | 1500 → 1350 → 1200 → 1050 → 900 → 750 | `sanitarium.cost` a..e | `sanitarium.building.json:18-23` |
| `sanitarium` | 永久负面怪癖治疗单价 | 5000 → 4500 → 4000 → 3500 → 3000 → 2500 | `sanitarium.cost` a..e | `sanitarium.building.json:27-32` |
| `sanitarium` | 疗养院槽位 | 1 → 2 → 3 | `sanitarium.slots` b/d | `sanitarium.building.json:36-38` |
| `sanitarium` | 疾病治疗单价 | 750 → 650 → 550 → 450 | `sanitarium.disease_quirk_cost` a/c/e | `sanitarium.building.json:47-50` |
| `sanitarium` | 疾病「一次治愈全部」概率 | 0.33 → 0.67 → 1.00 | `sanitarium.disease_quirk_cost` b/d | `sanitarium.building.json:54-56` |
| `sanitarium` | 疾病治疗槽位 | 1 → 2 → 3 | `sanitarium.slots` a/c | `sanitarium.building.json:60-62` |

**实测要点（容易踩坑）**：`sanitarium` 的两套「槽位」**共用同一棵树 `sanitarium.slots`，但取的 code 不同**：
- `treatment.slot_upgrades` → `b`、`d`（`sanitarium.building.json:37-38`）
- `disease_treatment.slot_upgrades` → `a`、`c`（`sanitarium.building.json:61-62`）
`[推断]` 这不是笔误而是设计：疗养院的「怪癖槽」和「疾病槽」是两套独立进度，共用同一 4 级成本树。
另外 `disease_quirk_cost_upgrades` **只有 4 级**（首级无绑定 + a/c/e），而 `disease_quirk_cure_all_chance_upgrades`
有 3 级（首级无绑定 + b/d）；也就是说**同一棵树的不同效果取不同的 code 子集**，
`a/c/e` 走价格、`b/d` 走治愈率。这是数据里唯一出现这种「交错取码」的地方（数据直述）。

`sanitarium` 的 `quirk_treatment_chance` 在两个活动里都是 `1.0`（`sanitarium.building.json:41`、`:65`）。

---

## 2. 建筑升级树（`Data/Upgrades/Building/*.upgrades.json`）

### 2.1 文件结构（实测）

每个 `*.upgrades.json` 顶层只有 **1** 个键：`trees`（8/8 文件一致，例 `Upgrades/Building/camping_trainer.upgrades.json:2`）。
每个 tree 对象有 `id` / `is_instanced` / `tags` / `requirements`。
每个 requirement 对象有 `code` / `currency_cost` / `prerequisite_requirements`。
`is_instanced` 在**全部 20 棵树**上都是 `false`（实测；脚本见 `_probe/q01_town.py`）。
`tags` 在全部树上都是 `["building", "<building id>"]` 两元素数组（例 `camping_trainer.upgrades.json:7`）。

**计数（实测）**：8 个文件共 **20** 棵树；`requirements` 条目总数见下表末列。

| 建筑 | 树 id | 树数 | requirements 数 | 树 id 出处 |
|---|---|---|---|---|
| `abbey` | `abbey.meditation`, `abbey.prayer`, `abbey.flagellation` | 3 | 6+6+6 = 18 | `abbey.upgrades.json:5, 90, 175` |
| `blacksmith` | `blacksmith.weapon`, `blacksmith.armour`, `blacksmith.cost` | 3 | 4+4+5 = 13 | `blacksmith.upgrades.json:5, 64, 123` |
| `camping_trainer` | `camping_trainer.cost` | 1 | 5 | `camping_trainer.upgrades.json:5` |
| `guild` | `guild.skill_levels`, `guild.cost` | 2 | 4+5 = 9 | `guild.upgrades.json:5, 64` |
| `nomad_wagon` | `nomad_wagon.numitems`, `nomad_wagon.cost` | 2 | 4+5 = 9 | `nomad_wagon.upgrades.json:5, 60` |
| `sanitarium` | `sanitarium.cost`, `sanitarium.disease_quirk_cost`, `sanitarium.slots` | 3 | 5+5+4 = 14 | `sanitarium.upgrades.json:5, 67, 141` |
| `stage_coach` | `stage_coach.numrecruits`, `stage_coach.rostersize`, `stage_coach.upgraded_recruits` | 3 | 5+5+3 = 13 | `stage_coach.upgrades.json:5, 77, 149` |
| `tavern` | `tavern.bar`, `tavern.gambling`, `tavern.brothel` | 3 | 6+6+6 = 18 | `tavern.upgrades.json:5, 90, 175` |
| **合计** | | **20** | **99** | |

### 2.2 成本曲线全表（数据直述，gold 恒为 0）

**全局实测事实：全部 99 条 `currency_cost` 中的 `gold` 项都是 `amount: 0`。**
也就是说参考实现里**建筑升级从不花金币**，只花传家宝（`crest`/`bust`/`deed`/`portrait`）。
证据示例：`Upgrades/Building/camping_trainer.upgrades.json:14`、`:25`、`:37`、`:49`、`:61`；
`Upgrades/Building/stage_coach.upgrades.json:14`（同样模式）；8 个文件全部如此（脚本统计，见 §0 清洗器 + `_probe/q01_town.py`）。

四种传家宝类型实测出现：`crest`（纹章）、`bust`（半身像）、`deed`（地契）、`portrait`（画像）。
**没有**第五种。**没有**任何 requirement 使用 `gold` 以外的货币作为唯一成本。

**货币项出现次数直方图（实测，计数口径见下）**：

| 货币 key | 出现次数（作为某个 requirement 的 `currency_cost` 成员） |
|---|---|
| `gold` | **99**（每个 requirement 恰好 1 条，`amount` 全为 `0`）|
| `crest` | **96** |
| `bust` | **35** |
| `portrait` | **30** |
| `deed` | **23** |
| **合计** | **283** |

计数口径：遍历 8 个 `*.upgrades.json`（顶层唯一 key 是 `trees`）→ 20 个树 → 99 个 requirement 对象的
`currency_cost` 数组，对每个成员的 `type` 字段做 `Counter` 累加；`currency_cost` 长度之和 = **283**
（= 99 + 96 + 35 + 30 + 23）。即「平均每个 requirement 约 2.86 项货币」。
**两条同样重要的实测否定结论**（`_probe/q08_hist.py` → `_probe/o56_hist.txt`）：
- `amount == 0` 的成员**只有 `gold` 一种**（99/99）；**没有任何** `crest`/`bust`/`deed`/`portrait` 成员金额为 0。
- **不存在**「只花 gold 的 requirement」（0/99），也**不存在**「传家宝部分全为 0 的 requirement」（0/99）——
  99 个 requirement 每一个都至少带 1 项**非零**传家宝成本。
**注意**：`gold: 0` 成员是**数据事实**，不是「被省略」——它被显式写出但金额为 0；
代码侧 `Upgrade.cs:281-306` 的 `CanAfford`/`Pay` 逐项累加，0 项不构成门槛也不扣减。

| 树 id | code | crest | bust | deed | portrait | gold | 前置 | 出处 |
|---|---|---|---|---|---|---|---|---|
| `abbey.meditation` | a | 4 | 4 | – | – | 0 | 无 | `abbey.upgrades.json:11-18` |
| | b | 8 | 8 | – | – | 0 | `abbey.meditation:a` | `:23-30` |
| | c | 11 | 11 | – | – | 0 | `abbey.meditation:b` | `:36-43` |
| | d | 15 | 15 | – | – | 0 | `abbey.meditation:c` | `:49-56` |
| | e | 19 | 19 | – | – | 0 | `abbey.meditation:d` | `:62-69` |
| | f | 23 | 23 | – | – | 0 | `abbey.meditation:e` | `:75-82` |
| `abbey.prayer` | a..f | 4,8,11,15,19,23 | 4,8,11,15,19,23 | – | – | 0 | 链式 | `:96-167`（`id` 在 `:90`） |
| `abbey.flagellation` | a..f | 4,8,11,15,19,23 | 4,8,11,15,19,23 | – | – | 0 | 链式 | `:181-252`（`id` 在 `:175`） |
| `blacksmith.weapon` | a | 6 | – | 10 | – | 0 | 无 | `blacksmith.upgrades.json:11-18` |
| | b | 17 | – | 26 | – | 0 | `blacksmith.weapon:a` | `:23-30` |
| | c | 28 | – | 42 | – | 0 | `blacksmith.weapon:b` | `:36-43` |
| | d | 39 | – | 58 | – | 0 | `blacksmith.weapon:c` | `:49-56` |
| `blacksmith.armour` | a..d | 6,17,28,39 | – | 10,26,42,58 | – | 0 | 链式 | `:70-115`（`id` 在 `:64`） |
| `blacksmith.cost` | a | 3 | – | 5 | – | 0 | 无 | `:129-136` |
| | b | 8 | – | 12 | – | 0 | `blacksmith.cost:a` | `:141-148` |
| | c | 12 | – | 18 | – | 0 | `blacksmith.cost:b` | `:154-161` |
| | d | 16 | – | 24 | – | 0 | `blacksmith.cost:c` | `:167-174` |
| | e | 21 | – | 31 | – | 0 | `blacksmith.cost:d` | `:180-187` |
| `camping_trainer.cost` | a | 27 | – | – | – | 0 | 无 | `camping_trainer.upgrades.json:11-17` |
| | b | 62 | – | – | – | 0 | `camping_trainer.cost:a` | `:22-28` |
| | c | 96 | – | – | – | 0 | `camping_trainer.cost:b` | `:34-40` |
| | d | 130 | – | – | – | 0 | `camping_trainer.cost:c` | `:46-52` |
| | e | 165 | – | – | – | 0 | `camping_trainer.cost:d` | `:58-64` |
| `guild.skill_levels` | a | 11 | – | – | 6 | 0 | 无 | `guild.upgrades.json:11-18` |
| | b | 30 | – | – | 15 | 0 | `guild.skill_levels:a` | `:23-30` |
| | c | 48 | – | – | 24 | 0 | `guild.skill_levels:b` | `:36-43` |
| | d | 67 | – | – | 33 | 0 | `guild.skill_levels:c` | `:49-56` |
| `guild.cost` | a | 5 | – | – | 2 | 0 | 无 | `:70-77` |
| | b | 11 | – | – | 5 | 0 | `guild.cost:a` | `:82-89` |
| | c | 17 | – | – | 8 | 0 | `guild.cost:b` | `:95-102` |
| | d | 23 | – | – | 11 | 0 | `guild.cost:c` | `:108-115` |
| | e | 29 | – | – | 14 | 0 | `guild.cost:d` | `:121-128` |
| `nomad_wagon.numitems` | a | 17 | – | – | – | 0 | 无 | `nomad_wagon.upgrades.json:11-17` |
| | b | 46 | – | – | – | 0 | `nomad_wagon.numitems:a` | `:22-28` |
| | c | 74 | – | – | – | 0 | `nomad_wagon.numitems:b` | `:34-40` |
| | d | 103 | – | – | – | 0 | `nomad_wagon.numitems:c` | `:46-52` |
| `nomad_wagon.cost` | a | 14 | – | – | – | 0 | 无 | `:66-72` |
| | b | 31 | – | – | – | 0 | `nomad_wagon.cost:a` | `:77-83` |
| | c | 48 | – | – | – | 0 | `nomad_wagon.cost:b` | `:89-95` |
| | d | 65 | – | – | – | 0 | `nomad_wagon.cost:c` | `:101-107` |
| | e | 82 | – | – | – | 0 | `nomad_wagon.cost:d` | `:113-119` |
| `sanitarium.cost` | a | 2 | 3 | – | – | 0 | 无 | `sanitarium.upgrades.json:10-16` |
| | b | 4 | 8 | – | – | 0 | `sanitarium.cost:a` | `:20-26` |
| | c | 6 | 12 | – | – | 0 | `sanitarium.cost:b` | `:31-37` |
| | d | 8 | 16 | – | – | 0 | `sanitarium.cost:c` | `:42-48` |
| | e | 10 | 21 | – | – | 0 | `sanitarium.cost:d` | `:53-59` |
| `sanitarium.disease_quirk_cost` | a..e | 2,4,6,8,10 | 3,8,12,16,21 | – | – | 0 | 链式 | `:73-131`（`id` 在 `:67`） |
| `sanitarium.slots` | a | 5 | 10 | – | – | 0 | 无 | `:147-154` |
| | b | 10 | 20 | – | – | 0 | `sanitarium.slots:a` | `:160-167` |
| | c | 15 | 30 | – | – | 0 | `sanitarium.slots:b` | `:174-181` |
| | d | 30 | 60 | – | – | 0 | `sanitarium.slots:c` | `:188-195` |
| `stage_coach.numrecruits` | a | 3 | – | 3 | – | 0 | 无 | `stage_coach.upgrades.json:11-18` |
| | b | 8 | – | 8 | – | 0 | `stage_coach.numrecruits:a` | `:23-30` |
| | c | 12 | – | 12 | – | 0 | `stage_coach.numrecruits:b` | `:36-43` |
| | d | 16 | – | 16 | – | 0 | `stage_coach.numrecruits:c` | `:49-56` |
| | e | 21 | – | 21 | – | 0 | `stage_coach.numrecruits:d` | `:62-69` |
| `stage_coach.rostersize` | a..e | 3,8,12,16,21 | – | 3,8,12,16,21 | – | 0 | 链式 | `:83-141`（`id` 在 `:77`） |
| `stage_coach.upgraded_recruits` | a | – | 12 | – | 8 | 0 | `guild.skill_levels:b` + `blacksmith.weapon:b` + `blacksmith.armour:b` | `:157-172` |
| | b | – | 18 | – | 12 | 0 | `stage_coach.upgraded_recruits:a` + `guild.skill_levels:c` + `blacksmith.weapon:c` + `blacksmith.armour:c` | `:189-204` |
| | c | – | 24 | – | 16 | 0 | `stage_coach.upgraded_recruits:b` + `guild.skill_levels:d` + `blacksmith.weapon:d` + `blacksmith.armour:d` | `:225-240` |
| `tavern.bar` | a | 4 | – | – | 2 | 0 | 无 | `tavern.upgrades.json:11-18` |
| | b | 8 | – | – | 4 | 0 | `tavern.bar:a` | `:23-30` |
| | c | 11 | – | – | 6 | 0 | `tavern.bar:b` | `:36-43` |
| | d | 15 | – | – | 8 | 0 | `tavern.bar:c` | `:49-56` |
| | e | 19 | – | – | 10 | 0 | `tavern.bar:d` | `:62-69` |
| | f | 23 | – | – | 11 | 0 | `tavern.bar:e` | `:75-82` |
| `tavern.gambling` | a..f | 4,8,11,15,19,23 | – | – | 2,4,6,8,10,11 | 0 | 链式 | `:96-167`（`id` 在 `:90`） |
| `tavern.brothel` | a..f | 4,8,11,15,19,23 | – | – | 2,4,6,8,10,11 | 0 | 链式 | `:181-252`（`id` 在 `:175`） |

#### 2.2.1 成本曲线的实测规律（数据直述的差分，非推测公式）

对 20 棵树逐级做**相邻差分**（脚本 `_probe/q09_table.py` → `_probe/o57_table.txt`）：

- **等差/近等差是普遍形态**，没有「基础价 × 系数」的等比形态。典型实测差分：
  - `camping_trainer.cost`：27 → 62 → 96 → 130 → 165，差分 **+35 / +34 / +34 / +35**（≈每级 +34.5，取整方式不统一）。
  - `nomad_wagon.cost`：14 → 31 → 48 → 65 → 82，差分 **恒 +17**（完全等差）。
  - `nomad_wagon.numitems`：17 → 46 → 74 → 103，差分 **+29 / +28 / +29**。
  - `blacksmith.weapon` / `blacksmith.armour`：`crest` 6 → 17 → 28 → 39（**恒 +11**），`deed` 10 → 26 → 42 → 58（**恒 +16**）。
  - `guild.cost`：`crest` 5 → 11 → 17 → 23 → 29（**恒 +6**），`portrait` 2 → 5 → 8 → 11 → 14（**恒 +3**）。
  - `guild.skill_levels`：`crest` 11 → 30 → 48 → 67（+19/+18/+19），`portrait` 6 → 15 → 24 → 33（**恒 +9**）。
  - `abbey.*` / `tavern.*`：`crest` = `bust` = 4 → 8 → 11 → 15 → 19 → 23（差分 **+4/+3/+4/+4/+4**）；
    `tavern.*` 的 `portrait` 2 → 4 → 6 → 8 → 10 → 11（差分 **+2/+2/+2/+2/+1** —— 最后一级只 +1，是全局唯一的「差分断点」）。
- **同一建筑的重复树用完全相同的曲线**（`crest`/`bust` 逐项相等）：
  `abbey.prayer` ≡ `abbey.meditation` ≡ `abbey.flagellation`；`tavern.gambling` ≡ `tavern.bar` ≡ `tavern.brothel`；
  `stage_coach.rostersize` ≡ `stage_coach.numrecruits`（`crest` 与 `deed` 都相同）；
  `sanitarium.disease_quirk_cost` ≡ `sanitarium.cost`。**没有**任何一棵树的曲线是别的树的整体缩放。
- **`crest` 是全局主货币**：96/99 个 requirement 含非零 `crest`；**不含 `crest` 的恰好 3 个**，
  即 `stage_coach.upgraded_recruits` 的 a/b/c（它们改用 `bust` + `portrait`）。
- **`sanitarium.slots:d` 是唯一「翻倍跳变」**：c = `crest 15 / bust 30` → d = `crest 30 / bust 60`
  （**恰好 2×**，而 a→b→c 都是 +5/+10）。其余所有树的差分都是缓增。
- **满级全清总成本（金额求和，脚本实测）**：`crest` **2161**、`bust` **534**、`deed` **482**、`portrait` **277**、`gold` **0**。
  （注意与上面的「出现次数直方图」区分：这里是 **amount 求和**，直方图是 **成员出现次数**。
  对照可见 `deed` 只出现 23 次却累计 482，而 `portrait` 出现 30 次只累计 277 —— 因为 `deed` 的单级单价高得多。）
- `[推断]` 上述差分形态说明 DD1 的升级价目表是**手工填写的绝对数值**，不是算式生成；
  参考实现只**逐字读取**（§6.1），因此这份曲线就是权威。


### 2.3 前置依赖（`prerequisite_requirements`）——三处「跨建筑」硬门槛

绝大多数树是**纯链式**：第 N 级的 `prerequisite_requirements` 恰为 `[{tree_id: 同树, requirement_code: 上一级}]`，
第 1 级为 `[]`（空数组）。例：`camping_trainer.upgrades.json:17-19`（a 级为空）、`:28-31`（b 级指向 a）。

**唯一的例外是 `stage_coach.upgraded_recruits`**，它同时依赖三棵**别的建筑**的树（数据直述）：

| code | 前置（除自身上一级） | 出处 |
|---|---|---|
| a | `guild.skill_levels:b`、`blacksmith.weapon:b`、`blacksmith.armour:b` | `stage_coach.upgrades.json:172-187` |
| b | `stage_coach.upgraded_recruits:a`、`guild.skill_levels:c`、`blacksmith.weapon:c`、`blacksmith.armour:c` | `:204-224` |
| c | `stage_coach.upgraded_recruits:b`、`guild.skill_levels:d`、`blacksmith.weapon:d`、`blacksmith.armour:d` | `:240-261` |

`[推断]` 这就是 DD1 里「先升级公会/铁匠铺，才能让驿站马车贩售更高等级新兵」的跨建筑前置。

**其它实测**：
- **不存在**任何「任务数/地牢等级」类前置写在 upgrade 树里。`number_of_quests_finished` / `highest_dungeon_level`
  只出现在 `*.building.json`（见 §1.1），upgrade 树里没有对应字段（实测 20 棵树均无此键）。
- **不存在** `upgrade_discount` 这个字面 key。见 §5。
- 20 棵树的 `tags` 全部为 `["building", "<building_id>"]`；**不存在** `"upgrade_discount"` 之外的折扣 tag。

---

## 3. 补给品 / 物品（Provisions / Items）

### 3.1 物品定义：`Data/Inventory/Items.bytes`

`Items.bytes` **不是二进制**，而是**制表符/空格分隔的文本**，扩展名是 `.bytes` 但被当作 `TextAsset` 读入
（`DarkestDatabase.cs:14` `ItemDataPath = "Data/Inventory/Items"`；`:1880` `Resources.Load<TextAsset>(ItemDataPath).text`）。

**解析方式（代码直述）**：`DarkestDatabase.cs:1881-1890`
```
按 '\r' '\n' 切行（去掉空行）
按 '\t' 和 ' ' 切 token（去掉空 token）
Type          = token[2]  去掉引号
Id            = token[4]  去掉引号
StackLimit    = int(token[6])
PurchasePrice = int(token[8])
SellPrice     = int(token[10])
```
即**列是按 token 序号硬编码的**，每行形如：
`inventory_item:  .type "X"  .id "Y"  .base_stack_limit N  .purchase_gold_value P  .sell_gold_value S`
（token0=`inventory_item:`，token1=`.type`，token2=`"X"`，token3=`.id`，token4=`"Y"`，…，token11=`.sell_gold_value`，token12=`S`）。

> **实测**：`.bytes` 内 57 个非空行**全部**能被上述规则解析成 13 个 token（脚本 `_probe/q03_prov_curio.py`）。
> `Items` 载入后是 `Dictionary<type, Dictionary<id, ItemData>>`（`DarkestDatabase.cs:63`、`:1892-1894`），
> 类型键**不是**数据里 `inventory_item:`，而是 `.type` 字段值。

**计数（实测）**：非空行 = **57**，按 `.type` 分布：
`provision`=1、`gold`=1、`heirloom`=5、`gem`=10、`journal_page`=22、`supply`=9、`quest_item`=9。
（计数方式：读取文件 → 按行切 → 取 token[2] 去引号 → `collections.Counter`。）

#### 3.1.1 补给品（可购买）逐项真值（数据直述）

| type | id | `.base_stack_limit` | `.purchase_gold_value` | `.sell_gold_value` | 出处 |
|---|---|---|---|---|---|
| `provision` | `""`（空 id，即「口粮」） | 12 | **75** | 5 | `Items.bytes:1` |
| `supply` | `shovel` | 4 | **250** | 25 | `Items.bytes:42` |
| `supply` | `firewood` | 1 | 0 | 0 | `Items.bytes:43` |
| `supply` | `bandage` | 6 | **150** | 15 | `Items.bytes:44` |
| `supply` | `antivenom` | 6 | **150** | 15 | `Items.bytes:45` |
| `supply` | `skeleton_key` | 6 | **200** | 20 | `Items.bytes:46` |
| `supply` | `medicinal_herbs` | 6 | **200** | 20 | `Items.bytes:47` |
| `supply` | `torch` | 8 | **75** | 5 | `Items.bytes:48` |
| `supply` | `holy_water` | 6 | **150** | 15 | `Items.bytes:49` |
| `supply` | `dog_treats` | 2 | 0 | 0 | `Items.bytes:50` |

**实测**：`.purchase_gold_value != 0` 的记录**只有 8 条**（上表加粗的 8 条）；
`gold`/`heirloom`/`gem`/`journal_page`/`quest_item` 全部为 `0`（脚本统计，见 `_probe/o51_prov_curio.txt`）。
也就是说：**只有 `provision` 与 `supply` 两类能在商店里被买**，`heirloom`（传家宝）不可用金币购买。

#### 3.1.2 传家宝与堆叠上限

| id | `.base_stack_limit` | 出处 |
|---|---|---|
| `portrait` | 3 | `Items.bytes:3` |
| `bust` | 6 | `Items.bytes:4` |
| `crest` | 12 | `Items.bytes:5` |
| `deed` | 6 | `Items.bytes:6` |
| `urn` | 1 | `Items.bytes:7` |

**实测要点**：数据里有 **5** 种 `heirloom`，但 §2.2 的升级成本只用到 **4** 种（`crest`/`bust`/`deed`/`portrait`）；
`urn` 从未出现在任何 `currency_cost` 里。
`[推断]` `urn`（骨灰瓮）只作为战利品/消耗品存在，不是升级货币。`Items` 里 `gold` 的 stack limit = 1500（`Items.bytes:2`）。

#### 3.1.3 可卖出（换金）物品

`.sell_gold_value != 0` 的记录共 **34** 条（脚本统计）：`provision`(5) + 10 个 `gem` + 22 个 `journal_page` + `supply`(8)。
- `gem`：`ruby` 1000、`emerald` 500、`sapphire` 1000、`citrine` 250、`onyx` 500、`pewrelic` 1250、
  `jade` 250、`trapezohedron` 2500、`antiqrelicsmall` 275、`antiqrelic` 1000（`Items.bytes:8-17`）
- `journal_page`：id `0`..`21`，**22** 条，每条 sell = **2000**（`Items.bytes:19-40`）
- `supply` 的 sell 值恰为 buy 值的 **1/10**（`shovel` 25/250、`bandage` 15/150、`torch` 5/75…）
  —— 但**没有代码用 `.sell_gold_value`**（见 §3.3），实际回购价由商店槽位价格决定。

### 3.2 商店库存与「起始携带」：`Data/Mechanics/Provision.json`

顶层 **3** 个键（`Provision.json:2, 51, 104`）：
`raid_starting_length_inventory_item_lists`、`raid_starting_hero_class_item_lists`、`default_store_inventory_item_lists`。

**（a）`raid_starting_length_inventory_item_lists`：5 个数组**（`Provision.json:4, 5, 16, 27, 38`）
索引 0 = `[ ]`（空），索引 1..4 各 9 条记录（`firewood` + `provision` + 7 个 supply），
**只有 `firewood` 的 amount 非 0**：索引 1→0、2→1、3→2、4→4（`Provision.json:6, 17, 28, 39`）。
其余 8 条的 amount 全是 0。
`DarkestDatabase.cs:832-839` 把 **5 个数组全部**（含索引 0 的空数组）依次 `Add` 进 `StartingLengthInventories`，
**不做任何索引 0 的丢弃或偏移**；`PartyInventory.cs:60-61` 用 `StartingLengthInventories[quest.Length]` 取值。
`[推断]` 因此 `quest.Length` 是**从 1 开始**的（1=短、2=中、3=长、4=极长），`Length==0` 时起始携带为空。
`[推断]` 「起始只给火把数量、把别的格子留空」意味着**其余补给必须自费购买**，而不是免费发放。

**（b）`raid_starting_hero_class_item_lists`：7 条**（`Provision.json:53, 60, 67, 74, 81, 88, 95`），每条 1 个 item：

| hero_class | 免费起始道具 | 出处 |
|---|---|---|
| `houndmaster` | `dog_treats` × 2 | `Provision.json:57` |
| `plague_doctor` | `antivenom` × 1 | `Provision.json:64` |
| `grave_robber` | `shovel` × 1 | `Provision.json:71` |
| `arbalest` | `bandage` × 1 | `Provision.json:78` |
| `crusader` | `holy_water` × 1 | `Provision.json:85` |
| `leper` | `medicinal_herbs` × 1 | `Provision.json:92` |
| `jester` | `medicinal_herbs` × 1 | `Provision.json:99` |

**实测**：只有 **7** 个英雄职业有免费起始道具；`antiquarian`/`bounty_hunter`/`hellion`/`highwayman`/
`houndmaster`（已有）/`man_at_arms`/`musketeer`/`occultist`/`vestal`/`abomination` 等**没有**条目。
`DarkestDatabase.cs:841-849` 把它们装进 `HeroClassItemList`（key = `hero_class`）。
`PartyInventory.cs:64-66, 80-83` 在对局开始时按队伍成员的职业发放。

**（c）`default_store_inventory_item_lists`：5 个数组**（`Provision.json:106, 107, 117, 127, 137`），
索引 0 = `[ ]`，索引 1..4 各 8 条记录。**这就是城镇补给商店的默认货架**：

| 索引（=quest length） | `provision` | `torch` | `shovel` | `antivenom` | `bandage` | `medicinal_herbs` | `skeleton_key` | `holy_water` | 出处 |
|---|---|---|---|---|---|---|---|---|---|
| 0 | — （空货架） | | | | | | | | `Provision.json:106` |
| 1 | 18 | 18 | 4 | 6 | 6 | 6 | 6 | 6 | `:108-115` |
| 2 | 24 | 24 | 6 | 9 | 9 | 9 | 9 | 9 | `:118-125` |
| 3 | 36 | 36 | 8 | 12 | 12 | 12 | 12 | 12 | `:128-135` |
| 4 | 42 | 42 | 10 | 15 | 15 | 15 | 15 | 15 | `:138-145` |

**规律（数据直述）**：`provision` 与 `torch` 的库存相同（18/24/36/42）；
`antivenom`/`bandage`/`medicinal_herbs`/`skeleton_key`/`holy_water` 五者相同（6/9/12/15）；
`shovel` 为 (4/6/8/10)。**注意 length 3→4 的比例**：provision 36→42、torch 36→42、shovel 8→10、
五件套 12→15，是 **×7/6**（不是沿 1→2→3 的 +6/+12 等差）。
`DarkestDatabase.cs:851-858` 把 5 个数组原样存入 `ShopLengthInventories`；
`ShopInventory.cs:58` 用 `ShopLengthInventories[currentQuest.Length]` 直接索引。

**实测**：`firewood` 与 `dog_treats` **不出现在商店货架**（`Provision.json` 全文没有它们）；
它们只能由起始携带获得（`Provision.json:6-14` 的 firewood、`:57` 的 dog_treats）。

### 3.3 购买/出售价格的实际计算（代码，`[推断]` 部分明确标注）

**购买单价**（`ShopSlot.cs:36-37`）：
```
Cost = (int)(InventoryItem.PurchasePrice * EventModifiers.ProvisionCostModifier(newItem.Type))
```
- `InventoryItem` = `Data.Items[Item.Type][Item.Id]`（`ShopSlot.cs:30`），对 provision 类型 id 为 `""`，
  所以 `PurchasePrice = 75`（`Items.bytes:1`）。
- `ProvisionCostModifier(itemType)`（`EventModifiers.cs:212-218`）= `1 + ProvisionCostModifiers[type]`，无条目时 `1`。
  **注意它以「类型」（`provision`/`supply`）为 key，而不是具体物品 id。**

**购买数量**（`ShopSlot.cs:24-25`）：
```
amount = ceil(newItem.Amount * EventModifiers.ProvisionAmountModifier(newItem.Type))
```
`newItem.Amount` 来自 `Provision.json`（18/24/36/42 等）。

**扣价粒度 —— 实测的语义（重要）**：
`ShopInventory.BuyShopSlot`（`ShopInventory.cs:35-45`）每次点击
`Currencies["gold"] -= slot.Cost` **并只 `slot.Item.Amount--`（减 1）**；
`PartyInventory.DistributeFromShopItem`（`PartyInventory.cs:266-291`）里 `int spaceNeeded = 1;`，
即**每次只给 1 个单位**。
`[推断]` 因此 `PurchasePrice=75` 是**单价**：买 1 份口粮 75 金。
（若按 DD1 原版「整叠 12 份 75 金」理解会得到不同结论——参考实现的代码不支持整叠购买，这是实测差异。）

**回购/卖回**：`ShopInventory.cs:86` `Currencies["gold"] += itemsSold * thisItemShopSlot.Cost;
`、`:105` `+= thisItemShopSlot.Cost`（单件）——**卖回按同一单价回收**，
并使用商店槽当前 `Cost`（已含 `ProvisionCostModifier`），**不使用 `ItemData.SellPrice`**。
`[推断]` 所以 `Items.bytes` 里的 `.sell_gold_value`（1/10 价）在城镇补给商店里**从未被读取**。

**另一条真正用 `SellPrice` 的路径**：`ResultItemWindow.cs:112-114`（任务结算界面）
`gold += slot.SlotItem.ItemData.SellPrice * slot.SlotItem.Item.Amount`（先判 `SellPrice != 0`）。
`[推断]` 即：**宝石/日志页在任务结算时按 `sell_gold_value` 折算金币**；
而城镇商店卖回补给走的是 `ShopSlot.Cost`。

**饰品（trinket）价格**（`WagonSlot.cs:27, 42`）：
```
Cost = Mathf.RoundToInt(Trinket.PurchasePrice * (1 - discount))
```
其中 `discount = NomadWagon.Discount + EventModifiers.UpgradeTagDiscount("trinket")`
（`WagonInventory.cs:25, 31, 43, 47`）。见 §5。

**卖出饰品**：`TrinketSelloutZone.cs:14`、`InventoryItem.cs:594`
`AddGold(RoundToInt(ItemData.PurchasePrice * 0.15f))` —— 回收价 = **买入价的 15%**。

---

## 4. 奇物 `Data/Curios/Curios.csv`

### 4.1 文件形态与计数（实测）

**计数方式**：`csv.reader` 逐行读取（`utf-8-sig`、`newline=""`），并逐行打印有效列数。

| 指标 | 实测值 |
|---|---|
| 物理行数（`\n` 切分） | **898** |
| csv 解析行数 | **898** |
| 每行列数 | **18 列，898/898 行都是 18 列**（min = max = 18） |
| 完全空行 | 898 − 708 = **190** |
| 非空行 | **708** |
| 奇物条目（`col1` 为纯数字的「类别头」行） | **60** |
| 类别头出现的行号（1-based） | 3, 18, 33, 48, …, 888（**步长 15**） |

`[推断]` 「60 条」= 60 个 15 行块；任务描述中的「60 entries」与实测一致。
**实测的异常**：块步长是 15，但**最后一个块（`Ancestor's Knapsack`，`:888`）只有 11 行**，
文件到 `:898` 就结束了——**该块缺少 Item Interactions 段（4 行）**。

### 4.2 列语义（1-based 列号 + 0-based 索引）

表头在每块第 2 行（例 `Curios.csv:4`）：
```
[1]空 [2]空 [3]ID STRING [4]空 [5]RESULT TYPES [6]WEIGHT [7]% CHANCE
[8]RESULT 1 [9]R1 WEIGHT [10]R1 % [11]RESULT 2 [12]R2 WEIGHT [13]R2 %
[14]RESULT 3 [15]R3 WEIGHT [16]R3 % [17]STRING [18]NOTES
```

消费代码：`DarkestDatabase.LoadCsvCurios()`（`DarkestDatabase.cs:1725-1814`），
网格由 `CsvReader.SplitCsvGrid` 产生（`CsvReader.cs:7-24`；`outputGrid[x, y]`，x=行、y=列，均 0-based）。
`curioGrid[i + k, c]` 中的 `c` 是 0-based ⇒ **1-based 列号 = `c + 1`**。

| 1-based 列 | 4.2 表头名 | 代码读取位置（`file:line`） | 语义（实测 + `[推断]`） |
|---|---|---|---|
| 1, 2 | （空） | 从不读取 | 前导空列 |
| 3 | `ID STRING` | `DarkestDatabase.cs:1731` `curioGrid[i+2, 2]` | 奇物的 `StringId`（= 图标/音效/本地化键）。`Curio.cs:38-41` 存入 `StringId` |
| 3 | `REGION FOUND` / 值 | `:1733` `curioGrid[i+4, 2]` | 第 3 列同时充当「标签名」与「值」：`:i+4` 行第 3 列的值是地区名 → `Curio.RegionFound`（`:toLower()`） |
| 3, 4 | `FULL CURIO?` / 值 | `:1734` `curioGrid[i+6, 2] == "Yes"` | `Curio.IsFullCurio`（bool） |
| 3, 4 | `TAGS` / 值 | `:1735-1742` 读 `[i+8,2]`、`[i+8,3]`、`[i+9,2]`、`[i+9,3]` | 最多 **4** 个 tag，全部 `toLower()` 后加入 `Curio.Tags` |
| 5 | `RESULT TYPES` | `:1732` `curioGrid[i, 4]`（**类别头行**的列） | `Curio.ResultTypes` —— 注意它装的是**品质**（`good`/`mixed`/`bad`），**不是**交互类型 |
| 5 | `RESULT TYPES` | `:1751` `curioGrid[i+2+r, 4]` | 交互的 `CurioInteraction.ResultType`（`loot`/`effect`/`nothing`/`purge`/`quirk`/`disease`/`scouting`），`toLower()` |
| 5 | `ITEM` | `:1785` `curioGrid[i+11+k, 4]` | Item Interaction 的 `ItemInteraction.ItemId` |
| 6 | `WEIGHT` | `:1752` `int.Parse(curioGrid[i+2+r, 5])` | 交互的 `CurioInteraction.Chance`（**整数权重**） |
| 6 | `RESULT TYPE` | `:1786` `curioGrid[i+11+k, 5]` | Item Interaction 的 `ResultType`（字符串） |
| 7 | `% CHANCE` | **从不读取** | 只是预计算好的显示百分比（如 `75.00%`），与第 6 列的整数权重一一对应 |
| 8 / 11 / 14 | `RESULT 1/2/3` | `:1761` `curioGrid[i+2+r, 7 + t*3]` | `CurioResult.Item`（物品/loot 条目名/怪癖名/效果名/buff 名，含义由第 5 列决定） |
| 9 / 12 / 15 | `R1/R2/R3 WEIGHT` | `:1762` `int.Parse(curioGrid[i+2+r, 8 + t*3])` | `CurioResult.Chance` |
| 10 / 13 / 16 | `R1/R2/R3 %` | `:1763` `curioGrid[i+2+r, 9 + t*3] == "<- # Draws"` | 若等于字面量 `"<- # Draws"` → `Draws = Chance`、`IsCombined = true`；否则 `Draws = 1`（`:1765-1769`） |
| 17 | `STRING` | **从不读取** | 本地化/风味文本（游戏内走 `Localization` 的 `str_curio_*` 键，见 `RaidSceneManager.cs:5340`） |
| 18 | `NOTES` | **从不读取** | 备注 |

**实测**（脚本 `_probe/q03_prov_curio.py`）：
- 第 6 列的 `WEIGHT` **898 行内没有一处非整数**（对 60 个块 × 8 个结果槽全部尝试 `int()` 均成功），
  与 `:1752` 的 `int.Parse` 一致；**没有**任何分数权重。
- 有 **342** 个结果槽「第 5 列（RESULT TYPES）非空但第 6 列（WEIGHT）为空」→ 被 `:1747-1749` 的
  `!= ""` 判定**静默丢弃**（不是报错）。
- 另有 **29** 个结果槽「第 6 列非空但 3 个 RESULT 列全空」→ 会创建 `CurioInteraction` 但
  `Results` 为空列表。
- `[推断]` 上述 342 个被丢弃的行里既有真结果（如 `Altar of Light` 的 `Loot`，`Curios.csv:81` 第 5 列 = `Loot`、第 6 列空）
  也有纯粹充当标签的哑行；**参考实现因此可能丢失部分 curio 产出**。

### 4.3 15 行块的行布局（实测推导）

以块起点 `i`（0-based）为基准，代码读取的偏移固定：

| 偏移 | 行作用 | 依据 |
|---|---|---|
| `i+0` | 类别头：`<序号>, <名称>, , <品质>` | `:1732` 读 `[i,4]` |
| `i+1` | 列名表头 | 无代码读取 |
| `i+2` … `i+9` | **8 个结果槽**（每行可同时承载标签值） | `:1745-1747` `for resultIndex 0..7` |
| `i+4` | `REGION FOUND` 的值行 | `:1733` |
| `i+6` | `FULL CURIO?` 的值行 | `:1734` |
| `i+8`, `i+9` | `TAGS` 的值行 | `:1735-1742` |
| `i+10` | `Item Interactions` 段表头 | 无代码读取 |
| `i+11` … `i+13` | **3 个 Item Interaction 槽** | `:1780-1782` `for interactIndex 0..2` |
| `i+14` | 块尾（实测为空行） | — |

**关键实测**：结果槽（`i+2..i+9`）与元数据值行（`i+4`/`i+6`/`i+8`/`i+9`）**物理上是同一批行**——
一行可以在第 3 列写 `ALL`（地区值）同时在第 5–16 列写一个 `Quirk` 交互。
例：`Curios.csv:7` = `,,ALL,,Quirk,,,,…`（第 3 列 = 地区值 `ALL`，第 5 列 = 交互类型 `Quirk`，第 6 列空 → 该 Quirk 被丢弃）。

`Curios.csv:3-17` 是第 1 个块（`unlocked_strongbox`）的完整 15 行，可作为布局范本。

### 4.4 逐列取值统计（实测）

**品质（第 5 列，类别头行）**：`Good` = **23**、`Mixed` = **31**、`Bad` = **6**（合计 60）。
`[推断]` 消费位置：`RaidSceneManager.cs:5326-5331` 仅把它映射为 FMOD 音效参数
`result_category`（`mixed`→1、`good`→2、其它→0），**不参与任何经济计算**。

**地区（`RegionFound`，第 3 列 `i+4` 行）**：`ALL` = **14**、`Ruins` = **15**、`Warrens` = **9**、
`Weald` = **12**、`Cove` = **8**、`Darkest Dungeon` = **2**（合计 60）。
`[推断]` 单位置消费点：`DungeonGenerator.cs:640, 651, 687-688` 在生成地牢时按地区挑选 curio。

**`IsFullCurio`**：`Yes` = **50**、`No` = **10**。

**交互数量分布**：每奇物 1 个交互 = 22、2 个 = 11、3 个 = 15、4 个 = 11、5 个 = 1（合计 60）。
**Item Interaction 数量分布**：0 个 = 22、1 个 = 30、2 个 = 8（合计 60；因为最后一个块被截断，
若文件完整则此分布里的计数会略有不同）。

**交互 `ResultType` 取值（第 5 列，共 7 种）**：
`Loot` = 40、`Effect` = 38、`Nothing` = 29、`Purge` = 3、`Quirk` = 15、`Disease` = 9、`Scouting` = 4。

**`CurioResult.Item` 取值（第 8/11/14 列）**：**71** 个不同字符串。前列：
`Blight 1`(17)、`A`(11)、`B`(10)、`negative`(9)、`Bleed 1`(8)、`positive`(6)、`random`(6)、`P`(6)、
`Stress 3`(4)、`CT`(4)、`CH`(4)、`2 - all`(3)、`GT`(3)、`H`(2)、`JOURNALONLY`(2)、`TORCHONLY`(1)、
`0 - curios`(1) 等，另有怪癖 id（`claustrophobia`/`tetanus`/`bloodthirsty`/`the_black_plague`…）、
效果名（`Curio Damage Buff`/`HolyFountainEffects`）、buff 名（`Curio Damage BuffStrong`，`Curios.csv:89`）。
`[推断]` 这些字符串通过 `RaidSolver.GetLootEntry(curioResult.Item, raid)`（`RaidSolver.cs:108`）
当作 **loot 表 id** 解析；`A`/`B`/`P`/`H`/`S` 等单字母是 `JsonLoot.json` 里的 loot 表名。
（`JsonLoot.json` 属掉落表，超出本报告范围。）

**Item Interaction 的 ItemId 取值（第 5 列，共 9 种）**：
`holy_water`(10)、`shovel`(8)、`medicinal_herbs`(8)、`skeleton_key`(7)、`antivenom`(4)、`torch`(4)、
`bandage`(3)、`provision`(1)、`dog_treats`(1)。

**Tags 取值（第 3、4 列，`i+8`/`i+9` 行，共 14 种）**：
`All`(59)、`Scrounging`(16)、`Treasure`(17)、`Haunted`(13)、`Unholy`(9)、`Worship`(8)、`Body`(7)、
`Knowledge`(7)、`Reflective`(6)、`Drink`(4)、`Food`(4)、`Fountain`(3)、`Goal`(1)、`Torture`(1)。
注意 `All` 出现 59 次——**60 个块中只有最后一个（被截断的块）没被记为含 `All`**。

### 4.5 结果如何被消费（代码）

`RaidSceneManager.cs:5274-5560`：
1. 玩家手动交互 → `RandomSolver.ChooseByRandom(curio.Results)` 先选 `CurioInteraction`
   （按 `Chance` 加权），再 `ChooseByRandom(curioInteraction.Results)` 选 `CurioResult`（`:5300-5301, 5307-5308`）。
2. 用道具交互 → `curio.ItemInteractions.Find(itemId == selectedItem.Id)`（`:5295-5296`），再选结果（`:5297`）。
   `ItemInteraction` 的 `Chance` 由**构造函数固定为 1**（`ItemInteraction.cs:7`），
   `LoadCsvCurios` **不覆写它** ⇒ 道具交互永远只触发一个 interaction。
3. `Loot` 分支 → `ScrollEventLoot.LoadCurioLoot`（`:5408-5412`）→ `RaidSolver.GenerateLoot(curioResult, raid)`（`RaidSolver.cs:103-108`），
   循环 `curioResult.Draws` 次抽 loot。
   - 若 `curioResult.IsCombined`：遍历**同一次 interaction 的所有** `IsCombined` 结果（`ScrollEventLoot.cs:107-112`），
     且跳过 `Item == "Nothing"`。
   - 否则单条（`:114-116`）。
   - `QuestCurio` 特殊：只发 `quest_item`，数量 1（`:100-104`）。
4. 结果文案：`str_curio_<OriginalId>_<ResultString()>`（`RaidSceneManager.cs:5340`），
   找不到时再拼 `_<Item>`（`:5343-5347`）。
   `CurioInteraction.ResultString()` 对 `scouting` 特判返回 `"scout"`（`CurioInteraction.cs:20-26`）；
   `ItemInteraction.ResultString()` 返回 `"<ItemId>_<ResultType>"`（`ItemInteraction.cs:17-20`）。
5. `Curio.OriginalId`（`Curio.cs:6-19`）把 3 个 tutorial curio 映射回原 id：
   `tutorial_shovel→unlocked_strongbox`、`tutorial_key→discarded_pack`、`tutorial_holy→sack`。
   `[推断]` 这三个是 `Curios.csv:798, 813, 828` 的条目，复用原奇物的结果表 + 音效。

---

## 5. `upgrade_discount` 与影响城镇价格的一切

### 5.1 `upgrade_discount` 在数据里的确切出现位置（实测，穷举）

对 `Assets/Resources/Data/**` 做字面搜索 `upgrade_discount`，命中**恰好 2 个文件、18 处**：

**（a）唯一的「数据出处」：`Data/JsonBuffs.json`，2 处**（`Select-String -SimpleMatch` 全量扫描）：

| 文件:行 | 内容 |
|---|---|
| `Data/JsonBuffs.json:14794` | `"stat_type" : "upgrade_discount"`，`"stat_sub_type" : "weapon"`，`"amount" : 0.2`，`rule_type: "always"` |
| `Data/JsonBuffs.json:14807` | `"stat_type" : "upgrade_discount"`，`"stat_sub_type" : "armour"`，`"amount" : 0.2`，`rule_type: "always"` |

**（b）文本出处：`Data/Localization/Actors.xml`，16 处**（8 种语言 × 2 个 tooltip 键），
键名 `buff_stat_tooltip_upgrade_discount_weapon` / `..._armour`，英文原文见 `Actors.xml:50-51`：
```
<entry id="buff_stat_tooltip_upgrade_discount_weapon"> {0:+0.#%;-0.#%} Weapon Upgrade Cost </entry>
<entry id="buff_stat_tooltip_upgrade_discount_armour"> {0:+0.#%;-0.#%} Armor Upgrade Cost  </entry>
```
其余语言在 `Actors.xml:227-228`（fr）、`:404-405`（de）、`:581-582`（es）、`:758-759`（pt）、
`:935-936`（ru）、`:1112-1113`（pl）、`:1289-1290`（cs）。

`[推断]` 这条本地化文本是**很强的旁证**：该 stat 的**设计意图**确实是「降低武器/护甲升级费用」
（`{0:+0.#%;-0.#%}` 是百分比格式化），但 §5.3 的代码穷举表明它**从未被消费**——
即参考实现保留了原版文本，却丢掉了生效逻辑。
**`*.building.json`、`*.upgrades.json`、`Curios.csv`、`Items.bytes`、`Provision.json` 里都没有这个字符串。**

这两条 buff 的 id 是 `QUIRK_weapon_UPGRADEDISCOUNT_B`（`JsonBuffs.json:14793`）与
`QUIRK_armour_UPGRADEDISCOUNT_B`（`:14806`），被 `Data/JsonQuirks.json:2218` 与 `:2235` 的怪癖引用。

**注意**：数据里**没有**字面 key `upgrade_discount` 的字段——它只作为 `stat_type` 的**字符串值**出现。
`*.building.json` 里打折字段名是 `discount_percent`（§1.2），不是 `upgrade_discount`。

### 5.2 `upgrade_discount` 的解析（代码）

- `DarkestDatabase.cs:324-330`：`case "resistance": case "upgrade_discount":` 共用一支，
  `buff.Type = BuffType.StatAdd`，`AttributeType = CharacterHelper.StringToAttributeType(stat_sub_type)`。
- `CharacterHelper.cs:19-20`：`"armour" → AttributeType.ArmorDiscount`、`"weapon" → AttributeType.WeaponDiscount`。
- `CharacterHelper.cs:152, 167-168`：`"upgrade_discount" ↔ AttributeCategory.Discount`。
- `Character.cs:221-225, 276-277`：`ArmorDiscount`/`WeaponDiscount` 被注册为**每个角色**都持有的属性
  （`HeroDiscounts` 数组，初值 0）。
- `AttributeModifier.cs:45-46`：`AttributeCategory.Discount` 的 tooltip 键前缀为 `buff_stat_tooltip_upgrade_discount_`。

### 5.3 **实测的否定结论（重要）**

对 `Assets/Scripts/**` 全量搜索 `WeaponDiscount|ArmorDiscount`：命中**恰好 3 个文件、共 10 处**——
`MechanicsDefines.cs:34-35`（枚举定义）、`CharacterHelper.cs:19-20`（字符串→枚举）、
`CharacterHelper.cs:80-83`（枚举→字符串）、`CharacterHelper.cs:189-191`（枚举→`AttributeCategory.Discount`）、
`Character.cs:223-224`（注册数组）。**全部 10 处都是「映射/定义/注册」，没有一处是「读取后用于计算」**。
（另有 `AttributeModifier.cs:45-46`：`AttributeCategory.Discount` → `string.Format(LocalizationManager.GetString("buff_stat_tooltip_upgrade_discount_" + ...))`，
即纯粹把 §5.1(b) 的本地化键拼出来显示，**不参与任何计算**。）
另外对 `Assets/Scripts/**` 字面搜索 `upgrade_discount` 命中 **4 处**，与上面 10 处枚举命中互不重叠但性质相同：
`DarkestDatabase.cs:327`（`case "upgrade_discount"` 解析分支）、`CharacterHelper.cs:152, 168`、
`AttributeModifier.cs:46`（tooltip 键）。
**没有任何一处** `GetSingleAttribute(AttributeType.WeaponDiscount)` / `.ArmorDiscount` 调用，
也没有任何把这两个属性乘进 `Blacksmith`/`Guild` 价格的代码。

`[推断]` 因此在 Unity 参考实现里，**`upgrade_discount` 这个 stat 被完整解析并挂到英雄身上，但从未被消费**；
铁匠铺/公会/扎营教练/游牧货车的价格折扣**只**来自建筑升级（`DiscountUpgrades`），见 §5.4。
这对我们的实现是明确信息：**若照抄参考，`upgrade_discount` 怪癖不会改变城镇价格**。

同样实测：`DarkestDatabase.cs:1251-1316` 里，`DiscountUpgrade` 只由 4 个来源构建——
`jsonBlacksmith.equipment_cost_discount_upgrades`（`:1251`）、
`jsonGuild.combat_skill_cost_discount_upgrades`（`:1265`）、
`jsonCampingTrainer.camping_skill_cost_discount_upgrades`（`:1279`）、
`jsonNomadWagon.trinket_cost_discount_upgrades`（`:1310`）。
`DarkestJsonReader.cs:248, 258, 267, 276` 声明了对应 4 个字段，`:389-392` 定义 `JsonDiscountUpgrade{ discount_percent }`。
**没有**第 5 个 `DiscountUpgrade` 来源（例如「城镇事件直接给折扣」走的是另一条路，见 §5.5）。

### 5.4 建筑折扣的累加方式（代码）

`Blacksmith.cs:22-28`、`Guild.cs:22-28`、`CampingTrainer.cs:22-28`、`NomadWagon.cs:80-86` 同构：
```
for (int i = DiscountUpgrades.Count - 1; i >= 0; i--)
    if (purchases[DiscountUpgrades[i].TreeId].PurchasedUpgrades.Contains(DiscountUpgrades[i].UpgradeCode))
        Discount += DiscountUpgrades[i].Percent;
```
- `purchases[treeId].PurchasedUpgrades` = 该树已购买的 code 集合（`UpgradePurchases.cs`）。
- 因为数据里 5 级折扣**每级都是 `0.10`**（§1.2），全买 = `Discount = 0.5`。
- `[推断]` 其他建筑用 `cost` 树的 code `a..e` 判断已购；`NomadWagon` 额外把
  `TrinketSlotUpgrades` 与 `DiscountUpgrades` 一起放进 `GetUpgrades()`（`NomadWagon.cs:120-126`）。
- **实测不一致点**：`NomadWagon` 里 `Discount` 与 `TrinketSlots` 只有 `private set`
  （`NomadWagon.cs:8, 10`），且 `Reset()` 把 `Discount = 0`（`:130`）——
  `InitializeBuilding` 与 `UpdateBuilding`（`:74-118`）都用 `Discount += ` 而**没有**先清零；
  `Guild`/`Blacksmith`/`CampingTrainer` 的 `Reset()` 同样把 `Discount = baseDiscount(=0)`（`:51`）。
  `[推断]` 若二者被连续调用而中间没有 `Reset()`，折扣会被**重复累加**；
  这属于参考实现的潜在缺陷，我们的实现**不应**照抄，应改为「遍历折扣集重新求和」。

**折扣如何进入价格**（逐建筑，代码直述）：

| 建筑 | 折扣变量 | 应用处 | 公式 |
|---|---|---|---|
| `blacksmith` 武器 | `Estate.Blacksmith.Discount` | `BlacksmithHeroWindow.cs:51, 90` | `discountWep = 1 - Blacksmith.Discount` → `TownManager.UpdateUpgradeSlot(status, slot, discountWep)` |
| `blacksmith` 护甲 | 同上 | `BlacksmithHeroWindow.cs:67, 105` | `discountArm = 1 - Blacksmith.Discount` |
| `blacksmith` 实际扣费 | 同上 | `BlacksmithHeroWindow.cs:142-144` | `Estate.BuyUpgrade(slot.Tree.Id, slot.Hero, slot.Upgrade, discount, isFree)` |
| `guild` | `Estate.Guild.Discount` | `GuildHeroWindow.cs:51, 88, 116` | 同样 `1 - Discount` |
| `camping_trainer` | `Estate.CampingTrainer.Discount` | `CampingTrainerHeroWindow.cs:22, 34, 51` | `1 - Discount`，`BuyUpgrade(skill, hero, discount)` |
| `nomad_wagon` | `NomadWagon.Discount` | `WagonInventory.cs:25, 31, 43, 47` | `Cost = round(PurchasePrice * (1 - (NomadWagon.Discount + eventDiscount)))`（`WagonSlot.cs:27, 42`） |

**扣费与显示**（`Estate.cs:480-542`）：
```
CanPayPrice(cost, discount)  => Currencies[cost.Type] >= cost.Amount * discount      (:500-503)
RemoveCurrency(cost, discount)=> Currencies[cost.Type] -= RoundToInt(cost.Amount * discount)  (:540-543)
```
`TownManager.cs:120, 160` 与 `CampingSkillPurchaseSlot.cs:35, 53` 用
`RoundToInt(cost.Amount * discount)` 显示折后价。**实测：四舍五入用 `Mathf.RoundToInt`，不是向下取整。**

### 5.5 除建筑折扣外，还会改城镇价格的机制

**(1) 传家宝兑换（`Data/Mechanics/HeirloomExchange.json`）** —— 1 个市场 `default`（`:5`），**12** 条兑换率（`:8-22`）：
- `bust 3 → portrait 1`（`:8`）、`bust 3 → deed 2`（`:9`）、`bust 2 → crest 3`（`:10`）
- `portrait 2 → bust 3`（`:12`）、`portrait 2 → deed 3`（`:13`）、`portrait 1 → crest 3`（`:14`）
- `deed 3 → bust 2`（`:16`）、`deed 3 → portrait 1`（`:17`）、`deed 2 → crest 3`（`:18`）
- `crest 3 → bust 1`（`:20`）、`crest 6 → portrait 1`（`:21`）、`crest 3 → deed 1`（`:22`）
消费代码 `ExchangeEntry.cs:26-29`：先查 `Currencies[FromType] >= fromAmount`，然后
`Currencies[FromType] -= fromAmount; Currencies[ToType] += CurrentAmount;`
**实测：兑换不涉及金币；`gold` 完全不在兑换表里。**

**(2) 城镇事件修正（`Campaign.EventModifiers`）** —— `EventModifiers.cs:10-11` 两组字典：
- `ProvisionCostModifiers[itemType]`（`:212-218`，返回 `1 + v`）→ 乘进商店单价（`ShopSlot.cs:37`）
- `ProvisionAmountModifiers[itemType]`（`:220-226`，返回 `1 + v`）→ 乘进货架数量（`ShopSlot.cs:24-25`）
二者由 `TownEventDataType.ProvisionTypeCostChange` / `ProvisionTypeAmountChange` 填充（`EventModifiers.cs:70-80`），
事件类型字符串来自 `DarkestDatabase.cs:1640-1644`（`"provision_item_type_cost_change"` / `"provision_item_type_amount_change"`）。
`TownEventDataType` 枚举见 `TownEvent.cs:11-13`。
- `UpgradeTagDiscount(tag)`（`EventModifiers.cs:228-234`，返回 `v` 或 `0`）→ 加到饰品折扣上
  （`WagonInventory.cs:25`，tag 字面量 `"trinket"`）。
  事件字符串 `"upgrade_tag_discount"` → `DarkestDatabase.cs:1646-1647`。
- `HasFreeUpgrade(tag)` / `RemoveUpgradeTag(tag)`（`EventModifiers.cs:236-248`）→ 免费升级次数；
  在 `TownManager.cs:109, 149` 与各 `*HeroWindow.cs` 里以 `isFree` 影响是否扣费。
- 另有 `ActivityCostModifier(activityName)`（`EventModifiers.cs:204-210`，返回 `1 + v`），
  作用于酒馆/修道院活动价格（活动 id 如 `bar`/`meditation`）。
- `TownEventDataType` 还有 `ActivityLock` / `ActivityCostChange` / `FreeActivity`（`TownEvent.cs:11-12`）。

**(3) 金币阈值（纯 UI）**：`Data/Mechanics/Campaign.json:33` `gold_icon_thresholds: [250, 500, 750, 1000]`；
`:34` `provision_icon_thresholds: [0, 5, 11, 14]`。
`Items.bytes` 里 `gold` 的堆叠上限 1500（`Items.bytes:2`），
`RaidSceneManager.cs:6029, 6037` 临时 `ExtraStackLimit += 500` / `= 0`。
`[推断]` 阈值只决定图标分档，不影响价格。

**(4) 城镇活动带来的金币变化**：`TownActivity.cs:184` `Estate.AddGold(change.Amount)` —— 对应
`side_effects` 里的 `change_currency`（如 `tavern.bar` 的 `gold -500`，`tavern.building.json:60`）。

**实测的「不存在」清单（就城镇价格而言）**：
- 参考数据里**没有**任何「商品基础价 × 声望/难度」之类的乘数表；供给价格只有
  `Items.bytes` 的 `purchase_gold_value` 一个来源。
- **没有** `Data/Mechanics/*.json` 描述商店折扣；折扣全部由 `Buildings/*.building.json` + `Upgrades/Building/*.upgrades.json` 决定。
- `upgrade_discount` 不出现在 `JsonTrinkets.json`（实测只在 `JsonBuffs.json` 2 处）。

## 6. 经济相关代码语义（数据本身读不出来的部分）

以下全部为 `[推断]`（来自代码），除非标明「数据直述」。

### 6.1 建筑与升级树的装载流程

| 步骤 | 代码 | 说明 |
|---|---|---|
| 1 | `DarkestDatabase.cs:27` `CsvCurioDatabasePath = "Data/Curios/Curios"` | CSV 路径（无扩展名，Unity `Resources` 约定） |
| 2 | `DarkestDatabase.cs:32` `JsonProvisionPath = "Data/Mechanics/Provision"` | 补给表路径 |
| 3 | `DarkestDatabase.cs:14` `ItemDataPath = "Data/Inventory/Items"` | 物品表路径 |
| 4 | `DarkestDatabase.cs:1088-1379` `GetJsonBuildings()` | 硬编码依次 `Load`：`abbey.building`(`:1093`)、`tavern.building`(`:1102`)、`sanitarium.building`(`:1112`)、`blacksmith.building`(`:1248`)、`guild.building`(`:1262`)、`camping_trainer.building`(`:1276`)、`nomad_wagon.building`(`:1290`)、`stage_coach.building`(`:1322`) |
| 5 | `DarkestDatabase.cs:1096-1098, 1105-1107` | **treeId 是代码常量**：`GetJsonTownActivity(jsonAbbey.meditation, "abbey.meditation")` 等 6 个 —— 数据里没有 tree id 字符串 |
| 6 | `DarkestDatabase.cs:1099, 1108, 1244, 1259, 1273, 1287, 1318, 1377` | `buildings.Add(...)` 的**顺序** = abbey, tavern, sanitarium, blacksmith, guild, camping_trainer, nomad_wagon, stage_coach |
| 7 | `Estate.cs:46-61` | 从 `Data.Buildings[id]` 取出 8 个建筑装进 `Buildings` 字典；额外补 `Graveyard`/`Statue`（`:62-65`，无 JSON） |

**实测要点**：装载顺序（步骤 6）与各建筑数据里的 `on_start_town_visit_priority`（§1.1）**不一致**——
`stage_coach` 的优先级是 `0`（最优先）却排在最后，`camping_trainer`/`nomad_wagon` 也是 `1` 却排在 6/7 位。
这佐证了 §6.6 的结论：该字段在参考实现中未被使用。

### 6.2 「`upgrade_requirement_code == null` ⇒ 基础值」约定（`[推断]`，但被数据完美验证）

同一套代码在 9 处使用模式（`DarkestDatabase.cs:1039, 1055, 1071`；`:1194, 1212, 1229`；`:1295, 1328, 1346`）：
```
if (jsonUpgrade.upgrade_requirement_code == null)  -> 写入 BaseXxx 并把 Xxx 也设为 BaseXxx
else                                               -> 包装成 CostUpgrade/SlotUpgrade/StressUpgrade/ChanceUpgrade，只存 Xxx
```
**这解释了 §1.3 里「数组第 1 项没有 `upgrade_requirement_code`」的现象**：
它不是「等级 1 的升级」，而是**初始值/基础值**。因此：
- `tavern.bar` 的 `cost_upgrades[0] = 1000` = **初始价格 1000**（不需要购买），
  `[1]=850(code b)`、`[2]=700(code e)` 是**两个可购买的打折档**。
- `stage_coach.recruit slots` 初始 2（`:8`），升级档 3/4/5/6/7 对应 code `a..e`。
- `nomad_wagon` 初始 2 个饰品位（`nomad_wagon.building.json:8`），升级档 4/6/8/12。

### 6.3 升级选择算法：「从最高档往回找，找到第一个已购买的即停」

`ActivityBuilding.cs:24-47`（成本/槽位/减压三张表各一份）、`StageCoach.cs:263-288`、`NomadWagon.cs:88-95`：
```
for (int i = Table.Count - 1; i >= 0; i--)
    if (purchases[treeId].PurchasedUpgrades.Contains(Table[i].UpgradeCode))
    { Current = Table[i].Value; break; }
```
`[推断]` 语义 = **取已购买的最高档**，并假定数组按等级升序排列。
`[推断]` 这也意味着**同一棵树上的多个效果各自独立取最高档**——
用 §1.4 的 `sanitarium.slots` 例子：买齐 `a,b,c,d` 后，怪癖槽取 `b`/`d` 里较高者（→3），疾病槽取 `a`/`c` 里较高者（→3）。

### 6.4 活动（酒馆/修道院）价格的最终算式

`ActivityBuilding.cs:56-64`（以及 `UpdateBuilding:104-112` 的同构版本）：
```
for (int i = 1; i <= 3; i++)
    if (i <= activity.NumberOfSlots)
        ActivitySlot(!isActivityLocked, isActivityFree ? 0 : (int)(activity.ActivityCost.Amount * costModifier));
    else
        ActivitySlot(false, ...);
```
- **槽位上限硬编码为 3**（`i <= 3`），与数据里所有活动最多 3 个槽（§1.3）一致；
  即数据里若出现 >3 的 `number_of_slots` 会被**忽略**。
- `costModifier = EventModifiers.ActivityCostModifier(activity.Id)`（`ActivityBuilding.cs:54`）→ `1 + v`（`EventModifiers.cs:204-210`）。
- **截断而非四舍五入**：用的是 `(int)` 强制转换，不是 `Mathf.RoundToInt`（对比 `Estate.cs:529` 建筑/英雄升级用 `RoundToInt`）。
- `isActivityFree` / `isActivityLocked` 来自城镇事件（`EventModifiers`），锁定时槽位不可选。
- 压力恢复：`TownActivity.cs:81` `GetPairedAttribute(AttributeType.Stress).DecreaseValue(StressHealAmount)`，
  而 `StressHealAmount` 在装载时取的是 `heal_high`（`DarkestDatabase.cs:1073, 1079`）——
  **`heal_low` 被完全忽略**（当前数据里 `heal_low == heal_high`，所以暂无差异；改数据时需注意）。

### 6.5 **建筑升级本身不打折**（实测的重要区分）

- 建筑升级：`UpgradableBuildingWindow.cs:123` → `Estate.BuyUpgrade(treeId, upgradeInfo, isFree)`
  → `Estate.cs:389-419`：`CanPayPrice(upgrade.Cost)`（`:391`，**无 discount 参数**）、
  `RemoveCurrency(upgrade.Cost)`（`:408`，**无 discount 参数**）。
- 英雄装备/技能升级：`BlacksmithHeroWindow.cs:142-144`、`GuildHeroWindow.cs:88-90, 116-118`
  → `Estate.cs:340-366`：`CanPayPrice(upgrade.Cost, discount)`（`:342`）、`RemoveCurrency(upgrade.Cost, discount)`（`:362`）。
- 扎营技能：`CampingTrainerHeroWindow.cs:51-53` → `Estate.cs:368-387`：`CanPayPrice(skill.CurrencyCost, discount)`（`:370`）、
  `RemoveCurrency(skill.CurrencyCost, discount)`（`:384`）。

`[推断]` 结论：**`DiscountUpgrades` 只降低「英雄个人升级」与「饰品」的价格，不降低「城镇建筑升级」的金币/传家宝成本**。
本作若希望建筑升级也打折，需要自己扩展（参考实现没有）。

### 6.6 **被解析但从未被消费的字段（实测否定清单）**

用 `Select-String` 对 `Assets/Scripts/**/*.cs` 全量字面搜索这 5 个名字，**总共只有 28 处命中**，
且逐处核对后**没有一处是「读取建筑字段后使用」**。28 处的精确构成：
24 处 = 8 个建筑类 × 3 个标量的声明（`DarkestJsonReader.cs:232-234, 243-245, 254-256, 263-265, 272-274, 281-283, 291-293, 302-304`）
+ 1 处 `caretaker_friendly` 声明（`:334`）+ 1 处 `guaranteed_previous_raid_dead_hero_levels` 声明（`:369`）
+ 1 处**同名的任务字段**声明（`:788` `required_number_of_quests_finished`）
+ 1 处该任务字段的**唯一**真实消费（`DarkestDatabase.cs:680`，属任务/地牢生成系统）。

| 字段 | 声明位置 | 消费位置 | 结论 |
|---|---|---|---|
| `on_start_town_visit_priority` | `DarkestJsonReader.cs:232, 243, 254, 263, 272, 281, 291, 302`（8 处，每个 `Json*` 建筑类一个） | **无** | 解析进对象后**再没被读过** |
| `number_of_quests_finished`（建筑级） | 同上 8 处 | **无** | 同上。注意 `JsonQuests` 有**同名但不同**的 `required_number_of_quests_finished`（`DarkestJsonReader.cs:788`），被 `DarkestDatabase.cs:680` 消费，属任务系统，**不是**建筑字段 |
| `highest_dungeon_level` | 同上 8 处 | **无** | 同上 |
| `caretaker_friendly` | `DarkestJsonReader.cs:334`（`JsonActivity`） | **无**（`CaretakerFriendly` 亦无命中） | 参考实现里「管家/看护者」逻辑（`Estate.RedeployCaretaker`，`Estate.cs:78+`）**不读这个字段** |
| `guaranteed_previous_raid_dead_hero_levels` | `DarkestJsonReader.cs:369` | **无**（`DarkestDatabase.cs:1361-1375` 构造 `RecruitUpgrade` 时**没有**映射它） | 数据里有（`stage_coach.building.json:26`=`[3]`、`:28`=`[4]`、`:30`=`[5,6]`，是 `JsonRecruitUpgrade` 的字段），代码丢弃 |

**这两个字段在数据里的实际出现位置（实测穷举）**：
- `caretaker_friendly`：**6 处、全为 `true`** —— `abbey.building.json:83, 174, 270`（3 个活动各 1 次）
  与 `tavern.building.json:79, 190, 290`（3 个活动各 1 次）。其余 6 个建筑**没有**这个键
  （它们是建筑级还是活动级？实测为**活动级**，因为 `DarkestJsonReader.cs:334` 在 `JsonActivity` 里）。
- `guaranteed_previous_raid_dead_hero_levels`：**3 处，只在 `stage_coach.building.json`**（`:26/:28/:30`），
  值分别是 `[3]`、`[4]`、`[5,6]`（随 `level` 1/2/3 递增，`chance` 0.1875 / 0.125 / 0.0625 递减）。

`DarkestJsonReader.cs` 的**每个建筑类都各自重复声明**这 3 个标量（`JsonAbbey`/`JsonBlacksmith`/… 共 8 个类），
而不是抽公共基类 —— 这是实测出的一个结构性重复点。

`[推断]` 触发条件（`number_of_quests_finished` / `highest_dungeon_level`）在参考实现里**不起作用**：
建筑从一开始就全部可见可升级。若我们要实现 DD1 的「按任务数解锁」，必须自己实现。

**另外两个「名字与实际语义不符」的实测**：
1. `Curio.ResultTypes`（`Curio.cs:22`）装的是**品质** `good`/`mixed`/`bad`（来自 CSV 第 5 列的**类别头行**），
   不是「结果类型」。真正的结果类型在 `CurioInteraction.ResultType`。
2. `quirk_library_names`（`JsonActivity`，`DarkestJsonReader.cs:333`）被 `DarkestDatabase.cs:958-959`
   读进 `TownActivity.IncompatiableQuirks`（`TownActivity.cs:18`），唯一消费点是
   `TavernWindow.cs:119-123` / `AbbeyWindow.cs:119-123` 的 `Contains(...)` 判断 ——
   即它是一份**「不兼容怪癖」黑名单**（英雄若有该怪癖则不能选该活动），**不是**副作用候选池。
   真正决定副作用结果的怪癖来自 `side_effects.results[].data[].quirk_library_name`（`DarkestDatabase.cs:990` → `AddQuirkTownEffect.QuirkSet`）。

### 6.7 存档/运行时枚举出的 20 棵树（与 §2.1 独立互证）

`SaveCampaignData.cs:224-243`（新档初始化）与 `SaveLoadManager.cs:550-569`（测试档）各**硬编码列出 20 个** tree id，
与 `Upgrades/Building/*.upgrades.json` 里的 20 棵树**逐一对应**：

```
abbey.meditation, abbey.prayer, abbey.flagellation,
tavern.bar, tavern.gambling, tavern.brothel,
sanitarium.cost, sanitarium.disease_quirk_cost, sanitarium.slots,
blacksmith.weapon, blacksmith.armour, blacksmith.cost,
guild.skill_levels, guild.cost,
camping_trainer.cost,
nomad_wagon.numitems, nomad_wagon.cost,
stage_coach.numrecruits, stage_coach.rostersize, stage_coach.upgraded_recruits
```
（`SaveCampaignData.cs:224-243`；同一清单也在 `SaveLoadManager.cs:550-569`。）=> **20**，与 §2.1 的计数一致。

### 6.8 货币容器

`Estate.cs:35-40`：`Currencies` 恰好 **5** 个键：`gold`、`bust`、`deed`、`portrait`、`crest`。
`[推断]` 这与 `Items.bytes` 的 5 个 `heirloom`（含 `urn`）**不对应**——`urn` 不在 `Currencies` 里，
所以它既不能用于升级也不能兑换，只能作为物品存在。
`Estate.cs:546-561`：
- `AddCurrency(crest, deed, portrait, bust)` 用 `Mathf.Clamp(v, 0, int.MaxValue)`（`:548-551`）
- `AddGold(int)`（`:554-557`）、`SpendGold(int)`（`:559-561`）同样 `Clamp(0, int.MaxValue)`
- `BuyUpgrade` 等其他路径直接写 `Currencies[type] -= ...`（`Estate.cs:519, 529, 536, 542`），**不 Clamp**。

`Estate.cs:156-223` `GetBuildingUpgradeRatio(BuildingType)` 用「已购数 / 总档数」算出 0..1 的进度比：
- `Blacksmith`：`blacksmith.weapon + .armour + .cost`（`:185-191`）
- `Guild`：`guild.skill_levels + guild.cost`（`:192-197`）
- `CampingTrainer`：`camping_trainer.cost`（`:198-201`）
- `NomadWagon`：`nomad_wagon.numitems + nomad_wagon.cost`（`:202-207`）
- `StageCoach`：`stage_coach.numrecruits + .rostersize + .upgraded_recruits`（`:208-215`）
- `Graveyard`/`Statue` 恒为 1（`:216-218`）
消费点：`UpgradableBuildingWindow.cs:15, 41`。**Abbey/Tavern/Sanitarium 没有 case** → 落到 `default`（`:219-221`）
打 `Debug.LogError("...not handled in estate upgrade ratio.")` 并返回 0。
`[推断]` 这是参考实现的一个**明显缺口**：三个活动型建筑无法算出升级进度比。

### 6.9 饰品（NomadWagon）的刷新与定价

`NomadWagon.cs:23-50`：稀有度权重表**硬编码在代码里**，5 档各 `Chance = 1`：
`very_common`、`common`、`uncommon`、`rare`、`very_rare`（等权，各 1/5）。
- `RestockTrinkets()`（`:53-64`）：抽 `TrinketSlots` 次，每次 `ChooseByRandom(RarityTable)` 选稀有度，
  再在该稀有度池里等概率随机；最后 `Trinkets.Sort((x,y) => y.PurchasePrice.CompareTo(x.PurchasePrice))`
  → **按价格从高到低排列**（`:63`）。
- `GenerateTrinket()`（`:66-72`）：单个随机饰品（用于城镇事件「送你一个饰品」，`TownActivity.cs:118`）。
- 价格：`WagonSlot.cs:27` / `:42` `Cost = round(PurchasePrice * (1 - discount))`，
  `discount = NomadWagon.Discount + EventModifiers.UpgradeTagDiscount("trinket")`（`WagonInventory.cs:25, 43`）。
- 购买扣费 `WagonInventory.cs:56-60` `Currencies["gold"] -= slot.Cost`，检查 `:53`。
- `Reset()`（`:128-132`）`Discount = 0.0f; TrinketSlots = BaseTrinketSlots;`

### 6.10 StageCoach 的招募升级

`StageCoach.cs:255-288 / 291-321`：
`RosterSlots ← RosterSlotUpgrades[最高已购档].NumberOfSlots`（`:263-270`）；
`RecruitSlots ← RecruitSlotUpgrades[...]`（`:272-279`）；
`CurrentRecruitMaxLevel ← RecruitExperienceUpgrades[...].Level`（`:281-288`）。
`Reset()`（`:332-337`）`RecruitSlots = BaseRecruitSlots; RosterSlots = BaseRosterSlots; CurrentRecruitMaxLevel = 0;`
`[推断]` 因此 `stage_coach.upgraded_recruits` 的 `chance`（3/16、2/16、1/16）与
`number_of_extra_*` 字段**在此文件里没有被直接读取**——`RecruitUpgrade` 只用到 `Level`
（`DarkestDatabase.cs:1365` 赋值 `Level`，`:1366-1370` 赋值其余字段，但 `StageCoach` 只消费 `Level`）。
`[推断]` `Chance`/`ExtraPositiveQuirks` 等的实际使用点在城镇英雄生成流程（`StageCoachWindow`/Hero 生成），
超出本报告范围；**但从经济角度唯一影响价格无关的只有 `Level`（影响新兵质量，不影响价格）**。

### 6.11 城镇事件对价格的 3 条通道（汇总）

| 通道 | 数据源字符串 | 内部表示 | 作用点 |
|---|---|---|---|
| 补给价格 ×`(1+v)` | `provision_item_type_cost_change`（`DarkestDatabase.cs:1640-1641`） | `EventModifiers.ProvisionCostModifiers[type]`（`EventModifiers.cs:76-80`） | `ShopSlot.cs:37` |
| 补给货架数量 ×`(1+v)` | `provision_item_type_amount_change`（`DarkestDatabase.cs:1643-1644`） | `EventModifiers.ProvisionAmountModifiers[type]`（`:70-74`） | `ShopSlot.cs:24-25` |
| 饰品折扣 `+v` | `upgrade_tag_discount`（`DarkestDatabase.cs:1646-1647`） | `EventModifiers.UpgradeTagCostModifiers[tag]`（`:82`） | `WagonInventory.cs:25, 43` |
| 活动价格 ×`(1+v)` | `activity_cost_change`（枚举 `TownEvent.cs:11` `ActivityCostChange`） | `EventModifiers.ActivityCostModifiers[activity]` | `ActivityBuilding.cs:54, 60` |
| 免费活动 | 枚举 `TownEvent.cs:12` `FreeActivity` | `IsActivityFree(id)` | `ActivityBuilding.cs:53, 60` |
| 免费升级 | 枚举 `TownEvent.cs:12` `UpgradeTagDiscount` 的兄弟语义 | `FreeUpgradeTags[tag]`（`EventModifiers.cs:236-248`） | `TownManager.cs:109, 149`；`*HeroWindow.cs` 的 `isFree` |

`TownEventDataType` 完整枚举：`ActivityLock, ActivityCostChange, ProvisionTypeCostChange,
ProvisionTypeAmountChange, UpgradeTagDiscount, FreeActivity`（`TownEvent.cs:11-12`）。

### 6.12 传家宝兑换

`DarkestDatabase.cs:1384-1399`：**只读 `markets[0]`**（`jsonExchange.markets[0].exchange_rates`，`:1390`）——
数据里恰好只有 1 个 market `default`（`HeirloomExchange.json:5`），所以行为等价；
但如果数据里加了第二个 market，参考实现会**只读取第 1 个**。
`ExchangeEntry.cs:26-29`：`Currencies[FromType] -= fromAmount; Currencies[ToType] += CurrentAmount;`
（注意扣的是 `fromAmount`，加的是 UI 层算出的 `CurrentAmount`；`fromAmount` 与 `CurrentAmount` 的比例由 `HeirloomExchangePanel` 维护）。

---

## 7. 参考实现中**缺失**的内容（实测的否定清单）

以下各项经全量搜索 / 逐文件核对后**确认不存在**于 `Darkest-Dungeon-Unity` 参考工程中。
（搜索范围：`Assets/Resources/Data/**` 全文件 + `Assets/Scripts/**/*.cs`。）

1. **没有 `upgrade_discount` 的实际效果**。该 stat 只在 `JsonBuffs.json:14793-14817` 定义 2 条 buff、
   在 `JsonQuirks.json:2218, 2235` 被引用、在 `DarkestDatabase.cs:327-330` 与
   `CharacterHelper.cs:152, 167-168, 19-20` 被映射为 `AttributeType.ArmorDiscount/WeaponDiscount`，
   并在 `Character.cs:221-225, 276-277` 注册为英雄属性——**但没有任何读取点**（§5.3）。
   ⇒ **`upgrade_discount` 神志/怪癖在参考实现里不改变任何城镇价格。**
2. **没有建筑升级的折扣**。`Estate.cs:389-419` 的两条扣费路径都不带 `discount` 参数（§6.5）。
3. **没有建筑解锁门槛的实际逻辑**。`on_start_town_visit_priority` / `number_of_quests_finished` /
   `highest_dungeon_level` 三个字段被 JSON 反序列化（`DarkestJsonReader.cs:232-234, …, 302-304`）后无人读取（§6.6）。
4. **没有 `caretaker_friendly` 的消费点**（`DarkestJsonReader.cs:334` 声明后无人读）。
5. **没有 `guaranteed_previous_raid_dead_hero_levels` 的消费点**（声明于 `DarkestJsonReader.cs:369`，
   数据在 `stage_coach.building.json:26, 28, 30`，`DarkestDatabase.cs:1361-1375` 未映射）。
6. **没有第 5 种升级货币**。全部 99 条 `currency_cost` 的 `gold` 一律为 `0`（§2.2）；
   `Currencies` 只有 `gold/bust/deed/portrait/crest` 5 键（`Estate.cs:35-40`），`heirloom` 里的 `urn`（`Items.bytes:7`）不可用。
7. **没有商品价格随难度/地区/任务长度的乘数**。`default_store_inventory_item_lists` 只改变**数量**
   （`Provision.json:104-147`），价格始终来自 `Items.bytes` 的 `.purchase_gold_value`（`ShopSlot.cs:36`）。
8. **没有 `firewood` / `dog_treats` 的出售渠道**：二者 `purchase_gold_value = 0`（`Items.bytes:43, 50`）
   且不在 `default_store_inventory_item_lists` 中（§3.2c），只能由起始携带/职业加成获得。
9. **没有 `Items.bytes` 里 `gem`/`journal_page` 的购买渠道**：`purchase_gold_value` 全为 0（§3.1.3）。
10. **`Curios.csv` 的第 7 列（`% CHANCE`）、第 17 列（`STRING`）、第 18 列（`NOTES`）从未被读取**
    （`DarkestDatabase.cs:1725-1814` 只使用索引 2,3,4,5,7..15）——它们是**纯文档列**。
11. **`Curios.csv` 第 60 个块（`Ancestor's Knapsack`，`:888`）被截断**：只有 11 行（应为 15 行），
    缺少 `Item Interactions` 表头与 3 个交互槽（§4.1）。
12. **没有 `upgrade_discount` 之外的第二类折扣 stat**；`JsonTrinkets.json` 中无 `upgrade_discount`（实测 §5.1）。
13. **没有 Abbey/Tavern/Sanitarium 的升级进度比**：`Estate.GetBuildingUpgradeRatio` 的 `switch`
    只有 Blacksmith/Guild/CampingTrainer/NomadWagon/StageCoach 五个 case（`Estate.cs:185-215`），
    其余落到 `default` 报错返回 0（`:219-221`）。
14. **没有「按等级覆盖」的成本模型**：`currency_cost` 是**每一档的绝对值**，不是「基础价 × 系数」
    （数据直述：`abbey.upgrades.json:11-82` 六档各不相同，无系数可推）。
15. **没有 `Upgrades/Building/*.upgrades.json` 的 `is_instanced = true`**：20/20 棵树都是 `false`（§2.1），
    即城镇升级是**全局共享**而非每个英雄独立（对比 `Upgrades/Heroes/*.upgrades.json` 才是 per-hero）。

---

## 附：本报告使用的复核脚本

| 脚本 | 产物 | 用途 |
|---|---|---|
| `_probe/q01_town.py` | `_probe/o50_buildings.txt`（199 行） | 解析 8 个 building + 8 个 upgrades 文件，dump 全部标量/列表/树/需求 |
| `_probe/q03_prov_curio.py` | `_probe/o51_prov_curio.txt`（79 行） | Curios.csv 逐列统计；Items.bytes 逐行解析与价格/堆叠汇总 |
| `_probe/q04_counts.py` | `_probe/o52_counts.txt` | 20 棵树 / 99 个 requirement / `currency_cost` 直方图 / 被弃用字段的全量 grep |
| `_probe/q05_trailing.py` | `_probe/o53_trailing.txt` | 8 个 building 文件的**严格 `json.loads()`** 通过性 + 非法尾随逗号行号（§0 的权威来源） |
| `_probe/q06_trailing_detail.py` | `_probe/o54_trailing_detail.txt` | 逐条打印尾随逗号所在行的原文与「下一个非空白字符」，确认 `],`→`}` / `},`→`]` 两种形态 |
| `_probe/q07_strict.py` | `_probe/o55_strictjson.txt` | 删逗号后重新严格解析的**闭合验证**（证明行号清单完整）；并给出 `Upgrades/`、`Mechanics/`、`Curios/` 全目录的严格 JSON 通过性对照表 |
| `_probe/q08_hist.py` | `_probe/o56_hist.txt` | `currency_cost` 成员出现次数直方图 + `amount==0` 分类统计（独立复算，用于交叉验证 §2.2） |
| `_probe/q09_table.py` | `_probe/o57_table.txt` | 逐 requirement dump `crest/bust/deed/portrait` 金额与前置，并给出**金额求和**（2161/534/482/277）——§2.2 全表的独立复算源 |
| `_probe/ddulib.py` | — | 容错 JSON 加载器（行感知状态机，去尾随逗号 /* */ 注释） |

清洗规则（严格按任务要求）：`re.sub(r",(\s*[\]}])", r"\1", raw)`（`_probe/q01_town.py` 早期版本）
或 `ddulib.py:23-77` 的字符串感知状态机版本。**结果：8/8 building 文件、8/8 upgrades 文件均可解析。**

补充实测（`_probe/o55_strictjson.txt`，字节数/行数均为脚本直接测量，UTF-8 字节）：

| 文件 | 字节 | 行数 | 严格 JSON | 删除 `,(\s*[\]}])` 后 |
|---|---|---|---|---|
| `Buildings/abbey.building.json` | 5834 | 299 | FAIL | OK（删 10 处）|
| `Buildings/blacksmith.building.json` | 692 | 14 | OK | OK（删 0 处）|
| `Buildings/camping_trainer.building.json` | 721 | 14 | OK | OK（删 0 处）|
| `Buildings/guild.building.json` | 669 | 14 | OK | OK（删 0 处）|
| `Buildings/nomad_wagon.building.json` | 1220 | 23 | FAIL | OK（删 1 处）|
| `Buildings/sanitarium.building.json` | 3834 | 67 | FAIL | OK（删 2 处）|
| `Buildings/stage_coach.building.json` | 2398 | 33 | OK | OK（删 0 处）|
| `Buildings/tavern.building.json` | 7011 | 319 | FAIL | OK（删 1 处）|
| `Upgrades/Building/abbey.upgrades.json` | 7618 | 260 | OK | OK |
| `Upgrades/Building/blacksmith.upgrades.json` | 5640 | 195 | OK | OK |
| `Upgrades/Building/camping_trainer.upgrades.json` | 1938 | 72 | OK | OK |
| `Upgrades/Building/guild.upgrades.json` | 3902 | 136 | OK | OK |
| `Upgrades/Building/nomad_wagon.upgrades.json` | 3466 | 127 | OK | OK |
| `Upgrades/Building/sanitarium.upgrades.json` | 6163 | 203 | OK | OK |
| `Upgrades/Building/stage_coach.upgrades.json` | 8348 | 262 | OK | OK |
| `Upgrades/Building/tavern.upgrades.json` | 7645 | 260 | OK | OK |
| `Mechanics/Provision.json` | 6136 | 148 | OK | OK |
| `Mechanics/HeirloomExchange.json` | 1958 | 28 | OK | OK |
| `Mechanics/Roster.json` | 360 | 24 | OK | OK |
| `Mechanics/Campaign.json` | 408 | 35 | OK | OK |
| `Inventory/Items.bytes` | 6792 | 62 | FAIL（非 JSON，预期）| FAIL |
| `Curios/Curios.csv` | 50172 | 898 | FAIL（非 JSON，预期）| FAIL |

**结论：8 个建筑文件中只有 4 个需要清洗，且清洗规则 `,(\s*[\]}])` 对这批文件既不漏也不误伤**
（删后 4/4 转为 OK，其余 4 个本来 OK 且删 0 处）。
