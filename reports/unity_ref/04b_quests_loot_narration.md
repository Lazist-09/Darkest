# 04b — 任务 / 掉落 / 旁白（Unity 参考项目实测报告）

- 参考项目：`F:\GithubPro\Darkest-Dungeon-Unity`
- 数据根：`Assets\Resources\Data\**`
- 代码根：`Assets\Scripts\**`（共 485 个 `.cs` 文件，`Get-ChildItem -Recurse -Filter *.cs | Measure-Object` 实测）
- 计数方法：所有计数由 Python 脚本在 `reports\unity_ref\_probe\*.py` 中对 JSON 解析后的对象直接 `len()`/`Counter()` 得出；行号由 `grep`/`read` 在原始文件上取得。
- 标注约定：
  - 无标注 = **数据字面陈述**（JSON 原文 / 代码原文可逐字核对）
  - `[推断]` = 我从代码读出来的推断，数据本身没有明说

> 本文件为**滚动编写**版本；全部 8 章均已完成。

---

## 1. Quests — `JsonQuests.json`

### 1.0 文件与解析
- 路径：`Assets\Resources\Data\JsonQuests.json`，4298 行，UTF-8 BOM。
- 实测：全文只有 **1 处**尾随逗号（正则 `,\s*[\]}]` 命中 1 次）。因此严格 JSON 解析失败，必须先做 `re.sub(r",(\s*[\]}])", r"\1", raw)` 预处理。（对照：`Curios\Traps.json` 有 8 处尾随逗号，`JsonLoot.json` / `Narration.json` / `PartyNames.json` / `Curios\Obstacles.json` 均为 0 处。）
- 顶层是 **object**，恰好 **7** 个键（不是 quest 数组）：

| 键 | 类型 | 数量 | JSON 行号 |
|---|---|---|---|
| `stress_damage` | 标量 | `20` | 2 |
| `goals` | list | **45** | 4 |
| `town_progression_goal_ids` | list | **4** | 609 |
| `types` | list | **6** | 615 |
| `plot_quests` | list | **30** | 777 |
| `generation` | dict | 5 个子键 | 2542 |
| `restriction` | dict | 1 个子键 | 4282 |

> **结论：这个文件里没有"任务定义"数组。** 真正可被接受/发布的"任务"是 `plot_quests` 的 **30** 条；`goals` 是**目标定义表**（45 条），`types` 是**任务类型表**（6 条），`generation` 是**随机任务生成参数**。

### 1.1 `goals` — 45 条目标定义
- 数量 **45**（`len(q["goals"])`）。
- 字段数 **6**，且 45 条的键集**完全一致**（`Counter(tuple(sorted(keys)))` 只产生 1 个 keyset）：
  `id`, `type`, `starting_items`, `ignore_fog_of_war`, `show_as_quest`, `data`。
- `type` 分布（45 条）：

| type | 条数 |
|---|---|
| `kill_monster` | 27 |
| `activate` | 7 |
| `gather` | 4 |
| `explore_room` | 2 |
| `tutorial_room` | 1 |
| `battle_room` | 1 |
| `battle` | 1 |
| `trait_applied` | 1 |
| `deaths_door` | 1 |

- `show_as_quest`：`true` **28** / `false` **17**。
- `ignore_fog_of_war`：**45 条全为 `false`** —— 即该字段在数据中从未被置为 true。
- `starting_items` 非空的只有 **5** 条，全是 `inventory_activate_*`，且数量恒为 3：

| goal id | starting_items[0] |
|---|---|
| `inventory_activate_corrupted_altar` | `{"type":"quest_item","id":"holy_water","amount":3}` |
| `inventory_activate_infected_corpse` | `{"type":"quest_item","id":"antivenom","amount":3}` |
| `inventory_activate_animalistic_shrine` | `{"type":"quest_item","id":"pickaxe","amount":3}` |
| `inventory_activate_wards` | `{"type":"quest_item","id":"eldritch_lantern","amount":3}` |
| `inventory_activate_beacons` | `{"type":"quest_item","id":"beacon_light","amount":3}` |

- 45 条 id 完整列表（`grep`/Python 实测顺序）：
  `tutorial_final_room, kill_necromancer_A..C, kill_hag_A..C, kill_swine_prince_A..C, kill_prophet_A..C, kill_formless_flesh_A..C, kill_brigand_cannon_A..C, kill_siren_A..C, kill_drowned_crew_A..C, kill_shuffler_D, kill_ancestor_heart_D, kill_brigand_sapper_D, explore_all_rooms, battle_all_rooms, gather_holy_relic, gather_medicines, gather_grain, gather_shipments, activate_iron_maiden, activate_teleporter, inventory_activate_corrupted_altar, inventory_activate_infected_corpse, inventory_activate_animalistic_shrine, inventory_activate_wards, inventory_activate_beacons, town_progression_explore, town_progression_battle, town_progression_trait, town_progression_deaths_door`
- `data` 子结构按 type 不同：`kill_monster` 用 `{monster_class_ids: [...], amount: N}`；`tutorial_room` 用 `{room_id: "room2_1"}`。`kill_formless_flesh_A` 的 `monster_class_ids` 有 4 个 id 且 `amount: 4`（一次遭遇战算达成）。

### 1.2 `town_progression_goal_ids` — 4 条
`["town_progression_explore","town_progression_battle","town_progression_trait","town_progression_deaths_door"]`。
这 4 个 id **同时**出现在 `goals` 的 45 条里（即它们是普通 goal，只是被额外登记为"城镇进度"目标）。

### 1.3 `types` — 6 条任务类型
每条字段数 **2**：`id`, `goal_lists`。`goal_lists` 是 `{dungeon, goals}` 的列表，`goals` 又是"字符串数组的数组"（每个内层数组是**一个可选目标组合**）。

| type id | goal_lists 数 | 覆盖 dungeon |
|---|---|---|
| `kill_boss` | 5 | all, cove, crypts, warrens, weald |
| `explore` | 1 | all |
| `cleanse` | 1 | all |
| `gather` | 5 | all, cove, crypts, warrens, weald |
| `activate` | 1 | all |
| `inventory_activate` | 5 | all, cove, crypts, warrens, weald |

- **实测缺陷（数据侧）**：`kill_boss` 的 **5 个 goal_lists 全部是 `[[]]`（空组）**，`gather` 的 `all` 组和 `inventory_activate` 的 `all` 组也是 `[[]]`。也就是说"全部地牢"这一档在这些类型下**没有任何 goal id**。具体展开：
  - `kill_boss`: `all=[[]], cove=[[]], crypts=[[]], warrens=[[]], weald=[[]]` → 全空
  - `gather`: `all=[[]]`（其余 4 档各 1 个 id：`gather_shipments`/`gather_holy_relic`/`gather_grain`/`gather_medicines`）
  - `inventory_activate`: `all=[[]]`（其余 4 档各 1 个 id）
  - `explore` → `explore_all_rooms`；`cleanse` → `battle_all_rooms`；`activate` → `activate_iron_maiden`
- `[推断]` `kill_boss` 的 goal id 依赖 `plot_quests` 里显式写死的 `goal_ids`（如 `plot_kill_necromancer_1` → `kill_necromancer_A`），因为随机生成路径拿不到任何 id。

### 1.4 `plot_quests` — 30 条任务定义
- 数量 **30**。外层字段：26 条有 **19** 个键，4 条多一个 `plot_quest_dependency`（共 **20** 个键）。
- 外层 19 个公共键：
  `id`, `quest`, `dungeon_level`, `additional_trinket_completion_rewards`, `is_progression`, `has_statue_contents`, `completion_dungeon_xp`, `can_retreat`, `retreat_always_from_raid`, `retreat_party_kill_count`, `is_surprise_enabled`, `is_scouting_enabled`, `is_roster_stress_cleared_on_completion`, `roster_buff_on_failure_minimum_party_resolve_level`, `upgrade_tags_to_remove_on_ignore`, `upgrade_tags_to_remove_on_failure`, `roster_buffs_to_apply_on_failure`, `suggested_trinkets`, `additional_provisions`
- 内层 `quest` 字段：24 条 **8** 键 / 6 条 **9** 键（多 `map_name`）。公共 8 键：
  `is_plot_quest`, `type`, `dungeon`, `difficulty`, `length`, `goal_ids`, `completion_reward`（+ 可选 `map_name`）。

**枚举值实测：**
- `dungeon_level` 分布：`0→6, 2→4, 3→4, 4→4, 5→4, 6→4, 7→4`（没有 `1`）
- `is_progression`：`true` 29 / `false` 1（唯一 false 是 `plot_town_invasion_0`）
- `has_statue_contents`：`true` 29 / `false` 1（同上）
- `completion_dungeon_xp`：`false` 29 / `true` 1（唯一 true 是 `plot_tutorial_crypts`）
- `can_retreat`：`true` 29 / `false` 1（唯一 false 是 `plot_darkest_dungeon_*`？—— 实测 false 出现在 DD 线；见下）
- `retreat_always_from_raid`：**30 条全为 `false`**
- `retreat_party_kill_count`：`0→25, 1→5`
- `is_surprise_enabled` / `is_scouting_enabled`：各 `true` 25 / `false` 5（两者总是同时为 false）
- `is_roster_stress_cleared_on_completion`：`false` 26 / `true` 4
- `roster_buff_on_failure_minimum_party_resolve_level`：`0→25, 5→5`
- `quest.type`：`kill_boss` 27 / `explore` 1 / `inventory_activate` 1 / `activate` 1
- `quest.dungeon`：`crypts` 7 / `weald` 6 / `warrens` 6 / `cove` 6 / `darkestdungeon` 4 / `town` 1
- `quest.difficulty`：`1→9, 3→8, 5→8, 6→5`（**没有 0、2、4**）
- `quest.length`：`1→3, 2→25, 3→1, 4→1`
- `quest.is_plot_quest`：**30 条全为 `true`**
- `quest.goal_ids`：**每条恰好 1 个 goal id**（30 条各 1 个），其中 `plot_darkest_dungeon_2` → `inventory_activate_beacons`、`plot_darkest_dungeon_3` → `activate_teleporter`
- `map_name` 只有 **6** 条非空：`tutorial_crypts`, `town_invasion_0`, `DD_map1`, `DD_map2`, `DD_map3`, `DD_map4`
- `plot_quest_dependency` 只有 **4** 条：`plot_town_invasion_0` = `""`（空串），`plot_darkest_dungeon_2` = `plot_darkest_dungeon_1`，`plot_darkest_dungeon_3` = `plot_darkest_dungeon_2`，`plot_darkest_dungeon_4` = `plot_darkest_dungeon_3`
- `additional_provisions`：30 条结构均为 `{system_config_type, items}`，`system_config_type` 实测 30 条全为 `"quest_provision"` 风格，**`items` 30 条全为空对象 `{}`** → 数据里没有任何额外补给
- `upgrade_tags_to_remove_on_failure`：**30 条全为空数组**
- `upgrade_tags_to_remove_on_ignore`：只有 `plot_town_invasion_0` 非空 = `[{"upgrade_tag":"building","amount":3}]`
- `roster_buffs_to_apply_on_failure`：只有 4 条 DD 任务非空 = `["darkest_dungeon_failure_roster_resolve_xp"]`
- `suggested_trinkets`：只有 `plot_darkest_dungeon_2` 非空 = `[{"trinket_id":"dd_trinket","amount":3}]`
- `additional_trinket_completion_rewards`：**26 条非空**，全部是 `[{rarity, amount:1}]`；唯一一条是 `rare`/`uncommon` 之类以外的差异：`plot_tutorial_crypts` = `very_common`，其余 25 条 = `very_rare`（`rare` 及以下未见）

### 1.5 `completion_reward` 结构（30/30 一致）
键恒为 `{resolve_xp, items_definition}`；`items_definition` 恒为 `{system_config_type:"quest_rewards", items:{...}}`，`items` 是**字符串数字键 → 对象**的 map。
- reward item 的键集 100% 是 `{id, type, amount}`（72 个 item 全部如此）。
- `type` 实测出现：`gold`（`id` 为空串）、`heirloom`（`id` = 传家宝名）、`trinket`（`id` = `dd_trinket`）。
- 例：`plot_kill_necromancer_1` = `{resolve_xp:4, items:{0:{gold,4500}, 1:{bust,3}, 2:{crest,4}}}`。
- 例：`plot_darkest_dungeon_2` = `{resolve_xp:16, items:{0:{gold,15000}, 1:{crest,18}, 2:{dd_trinket(trinket),3}}}`。
- `resolve_xp` 在 30 条里取值为 2/4/…/16（具体逐条见 §1.6 的 `resolve_xp_table`，二者数值一致）。

### 1.6 `generation` — 随机任务生成参数（5 个子键）
JSON 行号 2542 起。5 个子键：

**(a) `number`** — 1 个键 `number_of_quests_per_town_visit_table` = `[2,5,7,8,9,10,11,12]`（**8** 档；`[推断]` 按"已完成的城镇访问次数/进度档"索引，第 0 档也就至少生成 2 个任务）。

**(b) `dungeon`** — 2 个键：
- `max_number_of_generated_quests_per_dungeon`: `4`
- `generated_dungeons`: **4** 条，按解锁顺序：
  `crypts`(需完成 2 个任务) → `weald`(3) → `warrens`(4) → `cove`(4)

**(c) `difficulty`** — 1 个键 `generated_resolve_level_difficulties`，**3** 档：
| resolve_levels | difficulty |
|---|---|
| `[0,1,2]` | 1 |
| `[2,3,4]` | 3 |
| `[4,5,6]` | 5 |
（区间**互相重叠**：等级 2 和 4 同时属于两档。）

**(d) `type`** — 1 个键 `available_quests_table`，**4** 个地牢条目（`crypts` / `weald` / `warrens` / `cove`）。每条的 `generated_quest_table` 是"**按难度档索引的列表的列表**"：
- `crypts`: 7 档，各档候选数 = `1, 4, 6, 8, 8, 8, 8`
- 每条候选 = `{type, chance, length}`，**实测 `chance` 全部为 `1`**（即均匀权重，没有真正的概率差异）
- `type` 取值只用到 `explore` / `cleanse` / `gather` / `inventory_activate`（**没有 `kill_boss`、没有 `activate`**）
- 难度档 0 只给 1 条（`cleanse` + `length:1`），难度档 ≥1 才出现 `length:2/3`

**(e) `rewards`** — **6** 个子键（JSON 行 3712 起），是奖励表的核心：

| 子键 | 行号 | 形状 |
|---|---|---|
| `heirloom_type_map` | 3713 | **4** 条 `{dungeon, types[2]}` |
| `comment` | ~3742 | 字符串（说明 ROWS=DIFFICULTY, COLs=QUEST LENGTH） |
| `heirloom_amount_table` | 3744 | **4** 条（每种传家宝一条），每条 `{type, amounts[6]}` |
| `item_table` | 3850 | **7** 行，每行 0/4 列 |
| `resolve_xp_table` | 3951 | **7** 行 × **4** 列 |
| `trinket_chance_table` | 3995 | **6** 条（每种稀有度一条），每条 `{rarity, chances[7][4]}` |

`heirloom_type_map`（4 条，每条 2 种传家宝）：
- `crypts` → `[bust, crest]`
- `warrens` → `[portrait, crest]`
- `weald` → `[deed, crest]`
- `cove` → `[portrait, deed]`
（`crest` 出现在 3 个地牢里，是通用传家宝。）

`heirloom_amount_table`（`type` → `amounts`，**每条只有 6 项**，而 `resolve_xp_table` 有 **7** 行 → 二者索引基数不一致，`[推断]` 这是一个隐患）：
| type | amounts |
|---|---|
| `bust` | `[[], [0,2,2,4], [], [0,2,3,6], [], [0,3,5,9]]` |
| `portrait` | `[[], [0,2,2,4], [], [0,2,3,6], [], [0,3,5,9]]` |
| `deed` | `[[], [0,3,5,9], [], [0,4,6,12], [], [0,6,9,18]]` |
| `crest` | `[[], [0,3,5,9], [], [0,4,6,12], [], [0,6,9,18]]` |
内部 4 列 = 4 种任务长度；奇数档（索引 0/2/4）全为 `[]` 空数组。

`item_table`（7 行，行 = 难度 0..6；**行 0/2/4 为空数组 `[]`**；有效行 1/3/5/6 各 4 列）：
| 难度行 | 列 1 | 列 2 | 列 3 |
|---|---|---|---|
| 1 | gold **3000** | gold **4500** | gold **7500** |
| 3 | gold **4500** | gold **6750** | gold **11250** |
| 5 | gold **6000** | gold **9000** | gold **15000** |
| 6 | gold **9000** | gold **13500** | gold **22500** |
（每行第 0 列是空数组 → `length` 索引 0 不给金币；3 个有效列对应 `length` 1/2/3。`length:4` 的任务在表里**没有对应列**，`[推断]` 会越界或被夹取。）

`resolve_xp_table`（7 行 × 4 列，行 = 难度，列 = 长度）：
| 难度行 | 值 |
|---|---|
| 0 | `[0,0,0,0]` |
| 1 | `[0,2,3,4]` |
| 2 | `[0,0,0,0]` |
| 3 | `[0,4,6,8]` |
| 4 | `[0,0,0,0]` |
| 5 | `[0,8,12,16]` |
| 6 | `[0,0,0,0]` |

`trinket_chance_table`（6 种稀有度，各 7×4）——**实测绝大多数格子为 0**，非零格只有 6 个：
| rarity | 非零格 (难度行, 长度列) → 值 |
|---|---|
| `very_common` | 全部为 0（**整表全 0**） |
| `common` | (1,1) → 1 |
| `uncommon` | (1,2) → 1；(3,1) → 1 |
| `rare` | (1,3) → 1；(3,2) → 1；(5,1) → 1 |
| `very_rare` | (3,3) → 1；(5,2) → 1 |
| `ancestral` | (5,3) → 1 |

`[推断]` 这张表的语义是"**在 (难度,长度) 组合下，该稀有度是必出（1）还是不出（0）**"——因为值只有 0 和 1，且沿对角线递进（难度 1 长度 1 出 common，难度 1 长度 2 出 uncommon，难度 1 长度 3 出 rare；难度 3 长度 1 出 uncommon，长度 2 出 rare，长度 3 出 very_rare；难度 5 长度 1 出 rare，长度 2 出 very_rare，长度 3 出 ancestral）。这是一张**确定性**的"难度×长度 → 最高稀有度"映射，不是概率表。

### 1.7 `restriction` — 1 个子键
`{"difficulty": {"resolve_level_threshold_table": [2,2,3,4,5,99,99]}}`
- **7** 项。`[推断]` 按难度 0..6 索引，表示"接受该难度任务所需的最低 resolve level"；`99` 是"不可接受"的哨兵值（难度 5、6 在生成阶段被禁用）。
- 与 `plot_quests` 的 `quest.difficulty` 取值（1/3/5/6）对齐。

### 1.8 `stress_damage: 20`
顶层标量（行 2）。`[推断]`：放弃/失败任务时的队伍压力伤害基数 20。

---

## 2. Loot — `JsonLoot.json`

### 2.0 文件与解析
- 路径 `Assets\Resources\Data\JsonLoot.json`，715 行，UTF-8 BOM，**0 尾随逗号**，严格 JSON 合法。
- 顶层 object **2** 个键：`darkness_bonuses`（行 2，数组，**2** 条）、`loot_tables`（行 27，数组，**实测 54** 条）。
- 加载入口：`Assets\Scripts\Database\DarkestDatabase.cs:742` `GetJsonLootDatabase()`；资源路径常量 `Data/JsonLoot`（`DarkestDatabase.cs:42`）；反序列化 `DarkestDatabase.cs:744-745` → `DarkestJsonReader.cs:974` `GetJsonLootDatabase(string)`（`JsonConvert.DeserializeObject<JsonLootDatabase>`）。
- 运行时模型：`Assets\Scripts\Database\LootDatabase.cs`（`LootDatabase` / `DarknessBonus` / `LootTable` / `LootEntry` + 4 个子类）。
- **注意**：`loot_tables` 是**数组**，不是按 id 索引的字典；代码在 `DarkestDatabase.cs:818-821` 手动折叠成 `Dictionary<string, List<LootTable>>`（同 id 的多条变体进同一个 list）。

### 2.1 "54 张表" 的真相：54 条记录 / **33** 个唯一 id
- `len(loot_tables) == 54`（实测）。
- `Counter(t["id"])` → **33** 个唯一 id。重复情况（实测）：

| id | 定义条数 | 变体 |
|---|---|---|
| `H` | **13** | 4 地牢 × 3 难度（crypts/weald/warrens/cove × diff 1/3/5）+ 1 条 `dungeon="" diff=6` |
| `C` | 4 | `dungeon=""` × diff 1/3/5/6 |
| `G` | 4 | `dungeon=""` × diff 1/3/5/6 |
| `T` | 4 | `dungeon=""` × diff 1/3/5/6 |
| 其余 29 个 id | 各 1 | — |

- 4 条字段恒为 `{id, difficulty, dungeon, entries}`（54/54，唯一 keyset）。
- `difficulty` 实测分布：`0→29, 1→7, 3→7, 5→7, 6→4`。**没有 2 和 4**，与 `JsonQuests` 的 `resolve_xp_table`/`quest.difficulty` 取值（1/3/5/6）完全一致。
- `dungeon` 实测分布：`""→42, crypts→3, weald→3, warrens→3, cove→3`。**只有 4 个地牢名字，没有 `darkestdungeon`、没有 `town`。**

### 2.2 `entries` 与 `LootType`
- 54 张表共 **253** 条 entry（`sum(len(t["entries"]))`）。entry keyset **100%** 是 `{type, chances, data}`（253/253）。
- `type` 分布：`item` **126** / `trinket` **51** / `nothing` **48** / `table` **25** / `journal_page` **3**。
- 与代码枚举 `LootType { Nothing, Table, Item, Trinket, Journal }`（`LootDatabase.cs:3`）一一对应（JSON 的 `journal_page` ↔ 枚举的 `Journal`，映射在 `DarkestDatabase.cs:799`）。
- `data` 形状按 type 固定（实测 keyset 计数）：

| entry type | data keyset | 条数 |
|---|---|---|
| `nothing` | `{}` | 48 |
| `table` | `{table}` | 25 |
| `item` | `{type, id, amount}` | 126 |
| `trinket` | `{rarity}` | 51 |
| `journal_page` | `{min_page_index, max_page_index}` | 2 |
| `journal_page` | `{specific_page_index}` | 1 |

- `chances` 的取值范围（实测去重）：`[0, 0.05, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 15, 20, 30, 35, 40, 45]`。
  - **唯一非整数是 `0.05`**（`table A` 的 `journal_page`，`JsonLoot.json:43`）。
  - **253 条里有 65 条 `chances == 0`**（即"占位/被禁用"的 entry，占 25.7%）。
- `item.data.type` 实测分布：`heirloom` **53** / `gold` **28** / `gem` **28** / `supply` **12** / `provision` **5**。
- `item.data.id` 实测分布：空串 **33**（全是 `gold`/`provision`，没有具体 id）、`bust`14 / `portrait`13 / `deed`13 / `crest`13、6 种宝石各 4（`ruby`/`sapphire`/`emerald`/`citrine`/`onyx`/`jade`）、`shovel`2 / `skeleton_key`2 / `holy_water`2 / `torch`2、`firewood`1 / `antivenom`1 / `bandage`1 / `medicinal_herbs`1 / `pewrelic`1 / `trapezohedron`1 / `antiqrelicsmall`1 / `antiqrelic`1。
- `trinket.data.rarity` 实测分布（51 条）：`very_rare`8 / `very_common`7 / `common`7 / `uncommon`7 / `rare`7 / `ancestral`7 / `ancestral_shambler`6 / `collector`1 / `madman`1。
  - `collector`、`madman`、`ancestral_shambler` 这 3 个稀有度**不在** `JsonQuests.generation.rewards.trinket_chance_table` 的 6 档里（那张表只有 very_common/common/uncommon/rare/very_rare/ancestral）。

### 2.3 全部 54 张表逐条清单（id / dungeon / difficulty / entries 数）

| # | id | dungeon | diff | entries |
|---|---|---|---|---|
| 0 | `A` | `""` | 0 | 9 |
| 1 | `B` | `""` | 0 | 3 |
| 2 | `CH` | `""` | 0 | 3 |
| 3 | `CS` | `""` | 0 | 3 |
| 4 | `CT` | `""` | 0 | 3 |
| 5 | `GH` | `""` | 0 | 3 |
| 6 | `GT` | `""` | 0 | 3 |
| 7 | `SC` | `""` | 0 | 3 |
| 8–11 | `C` | `""` | 1 / 3 / 5 / 6 | 8 |
| 12–15 | `G` | `""` | 1 / 3 / 5 / 6 | 7 |
| 16–19 | `H` | crypts/weald/warrens/cove | 1 | 5 |
| 20–23 | `H` | crypts/weald/warrens/cove | 3 | 5 |
| 24–27 | `H` | crypts/weald/warrens/cove | 5 | 5 |
| 28 | `H` | `""` | 6 | 5 |
| 29 | `NONE` | `""` | 0 | 2 |
| 30 | `P` | `""` | 0 | 5 |
| 31 | `S` | `""` | 0 | 10 |
| 32 | `Tbase` | `""` | 0 | 2 |
| 33–36 | `T` | `""` | 1 / 3 / 5 / 6 | 8 |
| 37 | `T_COLLECTOR` | `""` | 0 | 7 |
| 38 | `T_ANTIQ_CAMP` | `""` | 0 | 8 |
| 39 | `T_MADMAN` | `""` | 0 | 7 |
| 40 | `T_INCURSIONSACK` | `""` | 0 | 3 |
| 41 | `TORCHONLY` | `""` | 0 | 1 |
| 42 | `SHOVELONLY` | `""` | 0 | 1 |
| 43 | `KEYONLY` | `""` | 0 | 1 |
| 44 | `HOLYONLY` | `""` | 0 | 1 |
| 45 | `SHAMBLER` | `""` | 0 | 2 |
| 46 | `PEW` | `""` | 0 | 2 |
| 47 | `COLLECTOR` | `""` | 0 | 3 |
| 48 | `MADMAN` | `""` | 0 | 3 |
| 49 | `T_ANCESTOR` | `""` | 0 | 2 |
| 50 | `ANTIQ` | `""` | 0 | 3 |
| 51 | `JOURNALONLY` | `""` | 0 | 1 |
| 52 | `THANKS` | `""` | 0 | 1 |
| 53 | `test` | `""` | 0 | 1 |

### 2.4 关键表内容（可核对的数据）

**金币表 `C`（按难度变化，实测）**
| 难度 | gold 25 | 50 | 100 | 250 | 500 | 750 | 1000 |
|---|---|---|---|---|---|---|---|
| 1 | 4 | 4 | 6 | 3 | 2 | 1 | 1 |
| 3 | 2 | 4 | 4 | 6 | 4 | 2 | 1 |
| 5 | 1 | 4 | 4 | 4 | 4 | 4 | 2 |
| 6 | 1 | 4 | 4 | 4 | 4 | 4 | 2 |
（数值 = `chances`；难度 5 与 6 **完全相同**。）

**宝石表 `G`**
| 难度 | ruby | sapphire | emerald | citrine | onyx | jade |
|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 4 | 4 | 10 | 10 |
| 3 | 1 | 1 | 5 | 5 | 3 | 3 |
| 5 | 3 | 3 | 5 | 5 | 1 | 1 |
| 6 | 5 | 5 | 5 | 5 | 1 | 1 |
（每条的 `amount` 恒为 1。难度越高越偏向高价值宝石。）

**传家宝表 `H`（13 条变体；difficulty 1/3/5 的样子）**
- 恒定 4 个候选：`bust` / `portrait` / `deed` / `crest`，各自的 `amount` 随难度上升：diff1 = `2/1/2/4`，diff3 = `3/2/3/6`，diff5 = `4/2/4/8`。
- `chances` 是一个"地形亲和度"编码：**该地牢对应的 2 种传家宝中，主传家宝权重 3、副传家宝权重 1**，与 `JsonQuests.generation.rewards.heirloom_type_map` 的地牢→类型映射**完全一致**（crypts 主=bust、warrens 主=portrait、weald 主=deed、cove 主=portrait），`crest` 在 4 个地牢里都是权重 3。实测逐条：
  - `H(crypts,*)` = bust 3 / portrait 1 / deed 1 / crest 3
  - `H(weald,*)` = bust 1 / portrait 1 / deed 3 / crest 3
  - `H(warrens,*)` = bust 1 / portrait 3 / deed 1 / crest 3
  - `H(cove,*)` = bust 1 / portrait 1 / deed 1 / crest 3
- `H("",6)` 是**唯一的双权重**变体：`bust 1 / portrait 1 / deed 2 / crest 2`。

**饰品表 `T`（按难度）**
| 难度 | very_common | common | uncommon | rare | very_rare | ancestral | ancestral_shambler | nothing |
|---|---|---|---|---|---|---|---|---|
| 1 | 45 | 40 | 10 | 5 | 0 | 0 | 0 | 0 |
| 3 | 20 | 35 | 35 | 8 | 2 | 0 | 0 | 0 |
| 5 | 15 | 30 | 35 | 12 | 5 | 3 | 0 | 0 |
| 6 | 15 | 30 | 35 | 12 | 5 | 3 | 0 | 0 |
（难度 5 与 6 **完全相同**；`ancestral_shambler` 在 4 档里**全为 0** → 该稀有度只能从 `SHAMBLER` 表拿。）

**`S`（杂项补给表，10 条）**：`firewood` chances **0**、`provision ""` ×2 **3** / ×4 **3** / ×6 **2** / ×8 **1**、`shovel`1 / `antivenom`1 / `bandage`1 / `medicinal_herbs`1 / `skeleton_key`1 / `holy_water`1 / `torch`2。
> `firewood` 的 `chances = 0` → **游戏中永远掉不出篝火**（`[推断]`：与 `RandomSolver.ChooseBySingleRandom` 把 `Chance<=0` 计入分母但永远选不中有关，见 §2.6）。

**`P`**：4 条 `provision`（`id` 为空串），`amount` = 2/4/6/8，`chances` = 3/3/2/1。
**`NONE`**：`nothing` chances **1** + `item heirloom bust amount 1` chances **0** → 实际恒为空。
**`Tbase`**：`nothing` 3 + `table T` 2。
**`test`**：只有 1 条 `nothing` chances 1 —— `[推断]` 开发残留表，代码里没有被引用（见 §2.5）。
**`JOURNALONLY`**：`journal_page` chances 1，`{min_page_index:1, max_page_index:21}`。
**`THANKS`**：`journal_page` chances 1，`{specific_page_index: 0}` —— **全库唯一**使用 `specific_page_index` 的 entry。
**`TORCHONLY`/`SHOVELONLY`/`KEYONLY`/`HOLYONLY`**：各 1 条 `supply`（torch / shovel / skeleton_key / holy_water），chances 1。
**`PEW`**：1 条 `gem pewrelic` chances 1（+ `nothing` 0）。
**`ANTIQ`**：`gem antiqrelicsmall` 5 / `gem antiqrelic` 1。
**`COLLECTOR`**：`gem trapezohedron` 3 + `table T_COLLECTOR` 1。**`MADMAN`**：`table S` 5 + `table T_MADMAN` 1。
**`SHAMBLER`**：`trinket ancestral_shambler` chances 1。**`T_ANCESTOR`**：`trinket ancestral` 1。**`T_INCURSIONSACK`**：`very_rare` 1 + `ancestral` 1。
**`T_COLLECTOR`**：6 档稀有度 chances 全 0 + `collector` 1。**`T_MADMAN`**：`very_common/common/uncommon` 各 1，`rare/very_rare` 0，`madman` 1。

### 2.5 表引用图：**25** 条 `table` entry，**1 个悬空引用**
- 所有 `table` entry 的 `data.table` 去重后共 **9** 个被引用 id：`C`(6) `G`(4) `S`(4) `T`(4) `H`(3) `J`(**1**) `P`(1) `T_COLLECTOR`(1) `T_MADMAN`(1)。
- ⚠️ **`J` 在 `loot_tables` 中不存在**（`JsonLoot.json:39`，`table A` 的一条 entry `{"type":"table","chances":0,"data":{"table":"J"}}`）。由于 `chances` 也是 **0**，正常随机不会选中它；但 `[推断]` 一旦被递归解析会走 `GetLootEntry("J")` → `LootDatabase.LootTables["J"]` → **KeyNotFoundException**（`RaidSolver.cs:151` 是裸字典索引，没有 `TryGetValue`）。
- **24 个 id 定义了却从未被任何 `table` entry 引用**（孤儿表）：`ANTIQ, B, CH, COLLECTOR, CS, CT, GH, GT, HOLYONLY, JOURNALONLY, KEYONLY, MADMAN, NONE, PEW, SC, SHAMBLER, SHOVELONLY, THANKS, TORCHONLY, T_ANCESTOR, T_ANTIQ_CAMP, T_INCURSIONSACK, Tbase, test`。
  - 其中 `B`/`CH`/`CS`/`CT`/`GH`/`GT`/`SC` 与 `Tbase` 看起来是 DR 内部表，`[推断]` 在参考实现中**从未被引用**，属于死数据（DD1 原版里它们由 `darkness_bonuses.codes` 或 curio 表引用；本项目的 `darkness_bonuses` 只引用了 `A` 和 `B`，见 §2.6）。

### 2.6 `darkness_bonuses` — 2 条，代码只用了其中 1 条
数据原文（`JsonLoot.json:2-26`）：

| type | darkness | chance | codes |
|---|---|---|---|
| `battle` | 0 | 0.75 | `["B","B"]` |
| `battle` | 1 | 0.75 | `["B"]` |
| `battle` | 26 | 0.5 | `["B"]` |
| `battle` | 51 | 0.25 | `["B"]` |
| `battle` | 76 | 0 | `["B"]` |
| `chest` | 0 | 0.95 | `["A"]` |
| `chest` | 1 | 0.75 | `["A"]` |
| `chest` | 26 | 0.5 | `["A"]` |
| `chest` | 51 | 0.25 | `["A"]` |
| `chest` | 76 | 0 | `["A"]` |

- 解析：`DarkestDatabase.cs:748-761` → `DarknessBonus { DarknessLevel, Chance, Codes }`（`LootDatabase.cs:17-22`）。
- **消费方只有 1 处**：`Assets\Scripts\Raid\Battle\BattleGround.cs:480-489`，且**硬编码取 `["battle"]`**：
  ```
  var darkenessLoot = DarkestDungeonManager.Data.LootDatabase.DarknessLoot["battle"];
  ...
  if (darkenessLoot[i].DarknessLevel == RaidSceneManager.TorchMeter.CurrentRange.Min)
      if (RandomSolver.CheckSuccess(darkenessLoot[i].Chance))
          for (int j = 0; j < darkenessLoot[i].Codes.Count; j++)
              BattleLoot.Add(new LootDefinition() { Code = darkenessLoot[i].Codes[j], Count = 1 });
  ```
- **实测缺陷**：`DarknessLoot["chest"]` 这 5 条**被完整解析进字典但从未被读取**（全仓库 `DarknessLoot` 只出现在 `DarkestDatabase.cs:760` 写入与 `BattleGround.cs:480` 读取，`grep -n "DarknessLoot"` 实测只有这两处）。
- `[推断]` 匹配逻辑用的是 `DarknessLevel == CurrentRange.Min`，而数据里 `darkness` 取值是 `0/1/26/51/76`；`TorchMeter` 的 `CurrentRange.Min` 是否恰好等于这 5 个值需另行核对（本报告不展开光照系统）。

### 2.7 参考代码如何 roll 一张表 —— `RaidSolver.GetLootEntry`
`Assets\Scripts\Mechanics\RaidSolver.cs:149-157`（全文）：
```
private static LootEntry GetLootEntry(string tableId, RaidInfo raid)
{
    LootTable lootTable = LootDatabase.LootTables[tableId.ToUpper()].
            Find(table => ((table.Difficulty == raid.Quest.Difficulty) || (table.Difficulty == 0))
                    && ((table.Dungeon == raid.Dungeon.Name) || (table.Dungeon == "")));

    LootEntry lootEntry = RandomSolver.ChooseBySingleRandom(lootTable.Entries);
    return lootEntry.Type != LootType.Table ? lootEntry : GetLootEntry(((LootEntryTable)lootEntry).TableId, raid);
}
```
逐条结论（全部可由上列代码字面核对）：
1. **表 id 大小写不敏感**：`tableId.ToUpper()`。数据里所有 id 本身就是大写（实测 33 个 id 全部 == `toUpper()`）。
2. **选择变体的规则**：在**同一 id 的 list 中取第一个**满足 `(difficulty == questDifficulty || difficulty == 0) && (dungeon == raidDungeon || dungeon == "")` 的元素 —— 用的是 `List.Find`（返回**首个**匹配），不是随机。
   - `[推断]` 因为 `H` 的定义顺序是 diff1 的四地牢 → diff3 的四地牢 → diff5 的四地牢 → `""`/diff6，所以当 `raid.Quest.Difficulty == 6` 时，diff1 的 `H(crypts,1)`…`H(cove,5)` 都不匹配，会落到第 28 条 `H("",6)` —— **恰好正确**，但这是靠数据顺序巧合成立的。
   - `[推断]` 反过来看 `C`/`G`/`T`：它们 `dungeon == ""`，所以永远是"精确难度"和"difficulty 0 的兜底"竞争；这 4 个 id 里**没有 difficulty 0 的定义**（实测 `C`/`G`/`T` 只有 1/3/5/6），所以 `Find` 能命中精确难度。**但如果只匹配到 `Difficulty == 0` 的兜底就该返回 null** —— `[推断]` 若表 id 下没有任何 diff-0 定义而 quest 难度又不在 1/3/5/6 中，`lootTable` 为 `null`，第 155 行会抛 **NullReferenceException**。
3. **真正的随机只有一层**：`RandomSolver.ChooseBySingleRandom(lootTable.Entries)`。
   `RandomSolver.cs:46-57` 实现：
   ```
   var rnd = UnityEngine.Random.Range(0, enumerable.Sum(item => item.Chance > 0 ? item.Chance : 0));
   foreach (var item in enumerable) { if (rnd < item.Chance) return item; rnd -= item.Chance; }
   return default(T);
   ```
   - `UnityEngine.Random.Range(int,int)` → **整数**上界（对 float 重载传入 int 参数会走 int 重载）。总权重是 `float` 求和后隐式转 int（截断）。
   - **`Chance <= 0` 的 entry 被计入分母（`Chance > 0 ? Chance : 0` 实际等价于直接加 0）但永远选不中** —— 实测 253 条里有 **65 条** `chances == 0`，它们纯粹占位。
   - `table A` 的 `journal_page` entry `chances = 0.05` → 在总权重求和时被**截断**为 0，`[推断]` 该 entry **实际永远抽不中**（除非它是唯一正权重项，那样整张表都会返回 `default(T)` = null）。这是"解析了但实际不可达"的字段实例。
   - 遍历时 `rnd -= item.Chance` 对负数 chance 会**增加** `rnd`（本数据中没有负数，实测最小 0）。
4. **递归展开**：选中的 entry 若是 `Table` 类型，则用其 `TableId` **递归**调用 `GetLootEntry`（无递归深度上限、无环检测）。`[推断]`：`COLLECTOR → T_COLLECTOR`、`MADMAN → S, T_MADMAN`、`Tbase → T` 都是 2 层；数据里没有自环（实测引用图无环）。
5. **`raid.Quest.Difficulty` / `raid.Dungeon.Name` 才决定选哪个变体** —— 也就是说 **同一份 `JsonLoot.json` 在不同难度/地牢下会选出不同的表定义**，这是"54 条 vs 33 个 id"存在的唯一理由。

### 2.8 三种 `GenerateLoot` 重载的差异（`RaidSolver.cs`）
| 重载 | 行 | 入口 code 来源 | 抽取次数 |
|---|---|---|---|
| `GenerateLoot(string code, int amount, RaidInfo)` | 8 | 直接传表 id（curio / 战斗掉落等） | `amount` 次 |
| `GenerateLoot(List<LootDefinition> battleLoot, RaidInfo)` | 54 | 战斗单位的 `Loot` 定义 | `Σ battleLoot[i].Count` 次 |
| `GenerateLoot(CurioResult curioResult, RaidInfo)` | 103 | `curioResult.Item` = 表 id | `curioResult.Draws` 次 |

- 三者的 `Item` / `Journal` 分支逻辑**逐字相同**；`Trinket` 分支里 **`GenerateLoot(List<LootDefinition>, ...)` 的 `trinketDef.Amount` 有 bug**：`RaidSolver.cs:92` 写的是 `trinketDef.Amount = trinketDef.Amount;`（自赋值，恒为 0），另外两个重载都是 `= 1`（`RaidSolver.cs:44` 与 `RaidSolver.cs:139`）—— 实测确认这是一个**参考项目的真实缺陷**。
- `Journal` 分支：有 `SpecificId` 就用它，否则 `RandomSolver.Next(MinIndex, MaxIndex + 1)` → **闭区间** `[min, max]`。数据里唯一用 `specific_page_index` 的是 `THANKS`（`JsonLoot.json:THANKS` 表），值 0。
- `Trinket` 分支：`DarkestDungeonManager.Data.Items["trinket"].Values.ToList().FindAll(t => ((Trinket)t).RarityId == trinketEntry.Rarity)` 然后 `RandomSolver.Next(count)` 取一个 —— **无视该稀有度在 `trinket_chance_table` 里的任何东西，也不检查库存/是否已拥有**；`[推断]` 若某稀有度一件饰品都没有，`trinketList.Count == 0` → `RandomSolver.Next(0)` 返回 0 → `trinketList[0]` **IndexOutOfRange**。数据里出现过但 `JsonTrinkets.json` 未必存在的稀有度：`collector`(1)、`madman`(1)、`ancestral_shambler`(6)（本报告未核验 JsonTrinkets，故仅标 `[推断]`）。
- `LootType.Nothing`：三个重载里都是空 `break`（不产出任何东西），因此"nothing"的命中会**静默吞掉一次抽取**。

---

## 3. Narration — `Narration.json`

### 3.0 文件与解析
- 路径 `Assets\Resources\Data\Narration.json`，6935 行，UTF-8 BOM，**0 尾随逗号**，严格 JSON 合法。
- 顶层 object **2** 个键：`filters`（行 2）、`entries`（行 5）。
- 加载：`Assets\Scripts\Database\DarkestDatabase.cs:1415-1440` `LoadNarration()`；资源路径常量见 `DarkestDatabase.cs`（`JsonNarrationDataPath`）；反序列化 → `DarkestJsonReader` 的 `GetJsonNarration`。
- 运行时模型：`Assets\Scripts\Campaign\NarrationEntry.cs`（14 行）与 `Assets\Scripts\Campaign\NarrationAudioEvent.cs`（84 行）。

### 3.1 `filters` — **1** 个元素，且**从未被代码读取**
- 实测：`filters` 是**字符串数组**，长度为 **1**，内容 `["plot_darkest_dungeon_4"]`（`Narration.json:2-4`）。
- **实测缺陷**：`grep` 全 `Assets\Scripts\**` 搜索 `filters` 在旁白上下文中**没有任何命中**；`DarkestDatabase.LoadNarration()`（`DarkestDatabase.cs:1415-1440+`）**只遍历 `jsonNarration.entries`**，完全没有触碰 `jsonNarration.filters`。C# 反序列化 DTO 里也不存在对应字段（`DarkestJsonReader.cs` 的 Narration 区块同理）。
- **结论：`filters` 是一个被完整解析进 JSON 对象、随后被 100% 丢弃的顶层字段。**

### 3.2 `entries` — **36** 条事件
- 实测 `len(entries) == 36`（与任务描述的"36 events?"一致，确认为 **36**）。
- 每条 entry 字段数 **4**（35 条）/ **5**（1 条）：`id`, `tone`, `chance`, `audio_events`（+ 仅 `obstacle` 有的 `priority`）。
- `tone` 实测分布：`bad` **20** / `good` **9** / `neutral` **7**（3 种取值）。
- entry 级 `chance` 实测分布：`1` → **29** 条；`0.7`/`0.75`/`0.9`/`0.85`/`0.3`/`0.4` 各 1 条；**`0` → 1 条**。
  - ⚠️ **哪一条是 `chance: 0`**：`[推断]` 该 entry **永远不会触发**（`DarkestSoundManager.cs:61` `if (!RandomSolver.CheckSuccess(narrationEntry.Chance)) return;`，且 `RandomSolver.cs:88` `random.NextDouble() < chance`，chance=0 时恒为 false）。
  - ⚠️ **`chance: 4` 出现在 audio_event 级**（见 §3.4），因为 `CheckSuccess` 在 `chance >= 1` 时**直接返回 true**（`RandomSolver.cs:85-86`），所以 4 等价于"必定成功"。

**36 个 event id 完整清单（含 JSON 行号，来自 `grep`）**：
`loading_screen_start`(7), `quest_start`(419), `quest_end_completed`(849), `quest_end_not_completed`(1304), `combat_start`(1454), `kill_monster`(1625), `kill_hero`(2293), `crit_monster`(2365), `crit_hero`(2489), `deaths_door`(2626), `victory`(2698), `battle_retreat`(2822), `battle_retreat_fail`(2868), `enter_hallway`(2914), `torchlight_out`(2964), `torchlight_full`(3036), `half_health_half_stress`(3108), `afflicted`(3128), `virtue`(3357), `hunger`(3527), `hunger_starve`(3547), `obstacle`(3632), `obstacle_clear_no_item`(3760), `curio`(3780), `trap`(3800), `loot`(3924), `camp`(4022), `recruit_hero`(4094), `dismiss_hero`(4581), `upgrade_building`(4692), `town_visit_start`(5659), `enter_quest_select`(6646), `enter_provision_select`(6666), `enter_building`(6686), `ancestor_talk`(6828), `change_monster_class`(6880)

### 3.3 `audio_events` — **465** 条，字段形状 100% 一致
- 每条 entry 的 `audio_events` 是**数组**（36/36）；总计 **465** 条（`sum(len(x["audio_events"]))` 实测）。
- 每条 audio_event 的键集 **100%** 是这 **11** 个（465/465，实测唯一 keyset）：
  `queue_only_on_empty`, `queue_while_audio_playing`, `audio_event`, `chance`, `priority`, `max_raid_occurrences`, `max_town_visit_occurrences`, `max_campaign_occurrences`, `filter`, `check_all_tags`, `tags`
- 与 C# `NarrationAudioEvent` 的 11 个属性（`NarrationAudioEvent.cs:7-17`）**一一对应**，名字是 `snake_case ↔ PascalCase` 的直接映射（`DarkestDatabase.cs:1429-1439`）。

**逐字段实测分布（465 条）**：
| 字段 | 分布 |
|---|---|
| `priority` | `0→287, 1→105, 2→49, 3→22, 4→2` |
| `queue_only_on_empty` | `true→266, false→199` |
| `queue_while_audio_playing` | `false→266, true→199`（**与上一列完全互补**） |
| `chance` | `1→193, 0.2→63, 0.5→53, 0.25→39, 0.33→34, 0.75→16, 0.4→16, 0.3→11, 0.85→9, 0.45→8, 0.12→7, 0.23→5, 0.7→5, 0.55→5, **4→1**` |
| `max_raid_occurrences` | `1→267, 0→198` |
| `max_town_visit_occurrences` | `0→322, 1→143` |
| `max_campaign_occurrences` | `0→427, 1→31, **99→7**` |
| `filter` | `""→458`, `"plot_darkest_dungeon_4"→7` |
| `check_all_tags` | `true→454, false→11` |
| `tags` 为空 | 143 条为空数组 / 322 条非空 |

每条 entry 的 audio_events 条数（实测）：
`loading_screen_start`27, `quest_start`26, `quest_end_completed`28, `quest_end_not_completed`11, `combat_start`11, `kill_monster`43, `kill_hero`5, `crit_monster`9, `crit_hero`10, `deaths_door`5, `victory`9, `battle_retreat`3, `battle_retreat_fail`3, `enter_hallway`3, `torchlight_out`5, `torchlight_full`5, `half_health_half_stress`1, `afflicted`16, `virtue`11, `hunger`1, `hunger_starve`6, `obstacle`8, `obstacle_clear_no_item`1, `curio`1, `trap`9, `loot`7, `camp`5, `recruit_hero`32, `dismiss_hero`8, `upgrade_building`64, `town_visit_start`75, `enter_quest_select`1, `enter_provision_select`1, `enter_building`9, `ancestor_talk`3, `change_monster_class`3

### 3.4 代码**实际读取**哪些字段 —— `DarkestSoundManager.ExecuteNarration`
`Assets\Scripts\Managers\DarkestSoundManager.cs:54-127`（全文见上）。逐行对照：

| 字段 | 代码是否读取 | 证据 |
|---|---|---|
| `entries[].id` | ✅ | `DarkestSoundManager.cs:56,59` 用 id 做字典键 |
| `entries[].chance` | ✅ | `DarkestSoundManager.cs:61` `RandomSolver.CheckSuccess(narrationEntry.Chance)` |
| `entries[].tone` | ❌ **只解析、从不读取** | `DarkestDatabase.cs:1425` 赋值给 `NarrationEntry.Tone`，但全仓库对 `.Tone` 的读取**只有这一处赋值** |
| `entries[].audio_events` | ✅ | `DarkestSoundManager.cs:64` |
| `entries[].priority`（entry 级，仅 `obstacle` 有） | ❌ **完全没有解析** | `DarkestDatabase.cs:1422-1425` 只赋 `Id`/`Chance`/`Tone`，**没有读 `jsonNarrationEntry.priority`**；`NarrationEntry.cs:4-8` 也**没有 `Priority` 属性**。`obstacle` 的 entry 级 `priority = 0` 被彻底丢弃。 |
| `filters`（顶层） | ❌ | 见 §3.1 |
| `audio_events[].queue_only_on_empty` | ✅ | `DarkestSoundManager.cs:74` |
| `audio_events[].queue_while_audio_playing` | ❌ **只解析、从不读取** | `DarkestDatabase.cs:1430` 赋值给 `QueueWhilePlaying`，此后全仓库再无读取（`NarrationAudioEvent.cs:8` 是唯一声明处）。**它恰好是 `queue_only_on_empty` 的布尔取反**（实测 266/199 完全互补），所以冗余。 |
| `audio_events[].audio_event` | ✅ | `DarkestSoundManager.cs:93,102,105,...` 拼 `"event:" + AudioEvent`；也用作 occurrence 计数键 |
| `audio_events[].chance` | ✅ | `DarkestSoundManager.cs:81,90` |
| `audio_events[].priority` | ✅ | `DarkestSoundManager.cs:68-69` `possibleEvents.Max(a => a.Priority)` 然后 `RemoveAll(p < maxPriority)` |
| `audio_events[].max_raid_occurrences` | ✅ | `NarrationAudioEvent.cs:37-42` + `DarkestSoundManager.cs:109` |
| `audio_events[].max_town_visit_occurrences` | ✅ | `NarrationAudioEvent.cs:45-50` + `DarkestSoundManager.cs:118` |
| `audio_events[].max_campaign_occurrences` | ✅ | `NarrationAudioEvent.cs:29-34` + `DarkestSoundManager.cs:100` |
| `audio_events[].filter` | ❌ **只解析、从不读取** | `DarkestDatabase.cs:1437` 赋值给 `Filter`；`NarrationAudioEvent.IsPossible()`（`NarrationAudioEvent.cs:24-83`）**从没碰 `Filter`**。7 条 `filter = "plot_darkest_dungeon_4"` 的过滤**完全无效**。 |
| `audio_events[].check_all_tags` | ✅ | `NarrationAudioEvent.cs:56` |
| `audio_events[].tags` | ✅ | `NarrationAudioEvent.cs:54-81` |

**`ExecuteNarration` 的算法（代码字面）**：
1. `Narration` 字典里没有该 id → **静默 return**（`:56-57`）。
2. entry 级 `Chance` 掷骰失败 → **静默 return**（`:61-62`）。**注意：entry 掷骰失败时不会 fallback 到任何东西。**
3. 用 `IsPossible(place, tags)` 过滤出候选（`:64`）；空 → return（`:65-66`）。
4. **取候选中的最高 `priority`，丢弃所有低优先级候选**（`:68-69`）。
5. 选一个：**若 id 是 `combat_start` 则恒取 `possibleEvents[0]`（不随机）**，否则**均匀随机**（`:71-72`）。`[推断]` 这是硬编码特例，`combat_start` 的 11 条候选里靠 priority 筛完后通常只剩 1 条，所以 `[0]` 与随机等价；但若筛完仍有多条，则**永远选第一条**。
6. `QueueOnlyOnEmpty` 且队列非空 → return（`:74-75`）。
7. **`town_visit_start` 特例**：最多重掷 **3** 次 audio_event 的 `Chance`，3 次都失败则 return（`:77-89`）。
8. 其它 id：一次 audio_event 级 `Chance` 掷骰（`:90-91`）。
9. `RuntimeManager.CreateInstance("event:" + AudioEvent)` 并压入 `NarrationQueue`（`:93-95`）。
10. 按 `place` 累加 occurrence 计数（`:97-126`）：`Raid` 会 `goto case Campaign`（**同时累加 Raid 和 Campaign**），`Town` 也会 `goto case Campaign`（**同时累加 TownVisit 和 Campaign**）。
    - `[推断]` 这是一个副作用缺陷：只要 `MaxCampaignOccurrences > 0`，任何 Raid/Town 播放都会消耗 campaign 配额。

### 3.5 `IsPossible` 的 tag 匹配逻辑（`NarrationAudioEvent.cs:54-82`）
- `Tags.Count == 0` → **无条件通过**（`:82`）—— 实测 143 条 audio_event 的 `tags` 为空。
- `CheckAllTags == true` → 对 entry 的每个 tag，必须在传入 `tags` 里找到（内层循环用 `j == tags.Length - 1` 判失败，`[推断]` 边界写法)，**全部命中才通过**（`:56-71`）。
- `CheckAllTags == false` → 只要传入 tags 里有**任意一个**在 `Tags` 里就通过（`:73-80`）—— 实测只有 **11** 条是 false。
- ⚠️ **实测陷阱**：`CheckAllTags == true` 且 `tags.Length == 0` 时，`:60-61` 直接 `return false`。因此**任何带 tags 的 audio_event 在调用方不传 tags 时都会被排除**。而调用方大量不传 tags，例如 `DarkestSoundManager` 的调用点 `"loot"`（`RaidSceneManager.cs:5049`）、`"trap"`（`RaidSceneManager.cs:5593`）、`"victory"`（`RaidSceneManager.cs:2082`）都是零 tag 调用 —— `[推断]` 这些事件下所有带 tag 的候选会被全部剔除。

### 3.6 `ExecuteNarration` 的全部调用点（`grep` 实测，含行号）
Raid / `NarrationPlace.Raid`：
`RaidSceneManager.cs:155,157` `quest_start` (tags = `CurrentRaid.Quest.Id` 等) · `:877` `camp` · `:1130` `quest_end_completed` · `:1135` `quest_end_not_completed` · `:1613` `kill_monster` · `:1658` `kill_hero` · `:1983` `combat_start` · `:2082` `victory` · `:3337` `battle_retreat_fail` · `:3344` `battle_retreat` · `:3996` `crit_hero`/`crit_monster` · `:4619` `virtue` (tag = `resolveTrait.Id`) · `:4624` `afflicted` (tag = `resolveTrait.Id`) · `:4827` `deaths_door` · `:5049` `loot` · `:5593` `trap` · `:5757` `ancestor_talk` (tag = `"ancestor_talk_" + Raid.AncestorTalk`) · `:5772` `obstacle_clear_no_item` · `:5806` `obstacle` (tag = `Raid.Quest.Dungeon`) · `:5933` `hunger_starve`
Town：`EstateSceneManager.cs:209,234` `town_visit_start` · `:671` `enter_quest_select` · `:703` `enter_provision_select` · `CharacterWindow.cs:145` `dismiss_hero` · `HeroRosterPanel.cs:64` `recruit_hero` (tag = `recruitSlot.Hero.Class`) · `UpgradableBuildingWindow.cs:69` / `TavernWindow.cs:51` / `StatueWindow.cs:35` / `SanitariumWindow.cs:62` / `GraveyardWindow.cs:59` / `AbbeyWindow.cs:51` `enter_building` · `Estate.cs:414` `upgrade_building`
其它：`ScreenLoader.cs:34,49` `loading_screen_start` · `BattleGround.cs:925` `change_monster_class` · `TorchMeter.cs:417,419` `torchlight_out`/`torchlight_full` · `RaidHallSector.cs:229` `curio` · `RaidSceneMultiplayerManager.cs`（联机路径的重复调用）

> **实测：36 个 event id 里，只有约 30 个存在调用点。** 无调用点的 id（`grep` 全仓库无 `ExecuteNarration("<id>"` 命中）：`half_health_half_stress`, `hunger`（只有 `hunger_starve` 被调）, `enter_hallway`。
> `[推断]` 这 3 个 id 在数据里定义完整（分别有 1/1/3 条 audio_event），但**参考实现从未触发它们** —— 这是"数据有、代码没用"的实例。

### 3.7 另一条旁白路径：`PlayStatueAudioEntry`（不经过 Narration 数据）
`DarkestSoundManager.cs:129-137`：
```
public static void PlayStatueAudioEntry(string id)
{
    if (CurrentNarration != null && NarrationQueue.Count > 0) return;
    var narrationInstanse = RuntimeManager.CreateInstance(id);
    ...
}
```
- 直接拿一个**原始 FMOD 事件路径**建实例，**完全绕过** `Narration.json` 的 id / chance / priority / tags 过滤。
- `[推断]` 这与 `plot_quests[].has_statue_contents`（实测 29/30 为 true）配合使用 —— 雕像台词不走旁白表。

---

## 4. PartyNames — `PartyNames.json`

### 4.0 文件与解析
- 路径 `Assets\Resources\Data\PartyNames.json`，750 行，UTF-8 BOM，**0 尾随逗号**，严格 JSON 合法。
- 顶层 object **1** 个键：`party_names`，是数组，**实测 186 条**。
- 加载：`Assets\Scripts\Database\DarkestDatabase.cs:1401-1413` `LoadPartyNames()`；路径常量 `Data/PartyNames`（`DarkestDatabase.cs:35`）；反序列化 `DarkestJsonReader.cs:879-881` `GetJsonPartyNames`。
- **纯 C# 映射类**（不依赖 Unity 资源）：`Assets\Scripts\Database\DarkestJsonReader.cs:29-35`：
  ```
  public class JsonPartyNameDictionary { public List<JsonPartyNameEntry> party_names; }
  public class JsonPartyNameEntry { public string id; public List<string> required_hero_class; }
  ```
  运行时类：`Assets\Scripts\Campaign\PartyNameEntry.cs`（`Id` + `ClassIds`）。

### 4.1 形状 — 186 条，字段数 **2**
- 实测：186 条的键集**完全一致**，恒为 `{id, required_hero_class}`（`Counter` 只产生 1 个 keyset）。
- `id` 是**字符串化的从 `"0"` 到 `"185"` 的连续序号**（实测首条 `"0"`、次条 `"1"`、末条 `"185"`）。
- `required_hero_class` 是**恰好 4 个英雄职业 id**的字符串数组（实测每条长度 4）。
  - 实例：`{"id":"0","required_hero_class":["vestal","plague_doctor","highwayman","crusader"]}`
  - `{"id":"1","required_hero_class":["occultist","bounty_hunter","highwayman","grave_robber"]}`
  - `{"id":"185","required_hero_class":["vestal","grave_robber","highwayman","highwayman"]}`
- ⚠️ **实测：没有 `name` 字段。** 这个文件里**只有队伍配置，没有队伍名字符串**。名字来自**本地化表**：`LocalizationManager.cs:36` 的类别列表里包含 `"PartyNames"`（与 `Monsters`/`Names`/`Quirks`/`Kickstarter`/`TownEvents`/`Journal` 并列）。
  > `[推断]` 显示名通过 `LocalizationManager` 用 `id` 去 `PartyNames` 本地化类别里取翻译串，而不是从本 JSON 读。
- ⚠️ 允许**重复职业**：`["vestal","grave_robber","highwayman","highwayman"]` 里 `highwayman` 出现两次。

### 4.2 唯一使用点
`Assets\Scripts\UI\Panels\PartyCompositionPanel.cs:16`（实测全仓库唯一读取 `Data.PartyNames` 的地方）：
```
var compEntry = DarkestDungeonManager.Data.PartyNames.Find(entry => ...);
```
- 用 `List.Find`（**首个匹配**）按职业顺序匹配当前队伍，返回该 `PartyNameEntry`，再拿 `Id`（`[推断]`）去本地化表取队伍名。
- ⚠️ **实测缺陷**：`PartyNames` 只在这里被读一次；186 条配置里未被匹配到的条目**永远不会被用到**（`Find` 只返回首个）。
- `DarkestDatabase.cs:84` 声明 `public List<PartyNameEntry> PartyNames`；`:117` 在初始化链里调用 `LoadPartyNames()`。

---

## 5. Obstacles / Traps

### 5.1 `Curios\Obstacles.json` — **5** 条
- 路径 `Assets\Resources\Data\Curios\Obstacles.json`，40 行，UTF-8 BOM，**严格 JSON 合法（0 尾随逗号）**。
- 顶层 object 只有 1 个键 `props`（行 2），是数组，**实测 5 条**（`_probe\p10.py` 输出 `OBSTACLES props len = 5`）。
- 每条字段数 **5**，5 条键集完全一致：`name`, `fail_effects`, `health`, `torchlight`, `ancestor_talk`。

| name | fail_effects | health | torchlight | ancestor_talk | 行号 |
|---|---|---|---|---|---|
| `thorny_thicket` | `["Stress 2"]` | `-0.05` | `-20.0` | false | 5–10 |
| `rubble` | `["Stress 2"]` | `-0.05` | `-20.0` | false | 12–17 |
| `shipwreck` | `["Stress 2"]` | `-0.05` | `-20.0` | false | 19–24 |
| `ancestor` | `[]`（空） | `0.0` | `0.0` | **true** | 26–31 |
| `town_rubble` | `["Stress 2"]` | `-0.05` | `-20.0` | false | 33–38 |

（数组长度实测 = **5**，与上表 5 行一致。）

- 没有 `success_effects` 字段：**障碍物只有失败效果**。
- `health` 是**浮点比例**（`-0.05` = -5% 最大生命），不是绝对值。
- 无 `difficulty_variations`：障碍物**不随难度变化**。

### 5.2 `Curios\Traps.json` — 4 条陷阱
- 路径 `Assets\Resources\Data\Curios\Traps.json`，93 行，UTF-8 BOM，**8 处尾随逗号**（行 16、17、23、38、39、45、60、61、66、67… → 实测 8 次命中），严格 JSON 解析**失败**，必须同样先 strip。
- 顶层 1 个键 `props`，**实测 4 条**。
- 每条字段数 **5**：`name`, `success_effects`, `fail_effects`, `health`, `difficulty_variations`。
- `difficulty_variations` 是数组，**每条恰好 2 个变体**：`level: 3` 和 `level: 5`；每个变体字段为 `{level, success_effects, fail_effects, health}`。

| name | base fail_effects | base health | level3 fail / health | level5 fail / health | 行号 |
|---|---|---|---|---|---|
| `poison_cloud` | `["Blight 1","Stress 2"]` | `0` | `Blight 2` / 0 | `Blight 3` / 0 | 5–24 |
| `spikes` | `["Stress 2"]` | `-0.25` | `Stress 2` / **-0.28** | `Stress 2` / **-0.30** | 27–47 |
| `blade_wheel` | `["Bleed 1","Stress 2"]` | `-0.10` | `Bleed 2` / -0.10 | `Bleed 3` / -0.10 | 49–69 |
| `lurker` | `["Lurker Trap Debuff 1","Stress 2"]` | `-0.10` | 同 base（不变） | 同 base（不变） | 71–91 |

- **4 条陷阱的 `success_effects` 全部是同一个字符串数组 `["Heal Stress TrapD"]`**，包括所有难度变体（共 4×3=12 处）—— 即"拆陷阱成功"的奖励在所有陷阱、所有难度下完全相同。
- 只有 `poison_cloud` / `spikes` / `blade_wheel` 的差异是有意义的（DOT 层数递增 / 伤害递增）；`lurker` 的两个难度变体与 base **逐字完全相同**（`[推断]` 冗余数据）。
- `health: 0`（`poison_cloud`）表示**不掉血**，只上 Blight。

### 5.3 参考代码如何解析障碍物/陷阱
**障碍物** — `Assets\Scripts\Database\DarkestDatabase.cs:431-451` `GetJsonObstaclesLibrary()`：
```
Obstacle obstacle = new Obstacle(jsonObstacles[i].name);
for (int j = 0; j < jsonObstacles[i].fail_effects.Count; j++)
    obstacle.FailEffects.Add(Effects[jsonObstacles[i].fail_effects[j]]);
obstacle.HealthPenalty = jsonObstacles[i].health;
obstacle.TorchlightPenalty = jsonObstacles[i].torchlight;
obstacle.AncestorTalk = jsonObstacles[i].ancestor_talk;
```
- DTO：`DarkestJsonReader.cs:511-523`（`JsonObstacleDatabase{props}` / `JsonObstacle{name, fail_effects, health, torchlight, ancestor_talk}`）—— **5 个字段，与 JSON 100% 对齐，无遗漏**。
- `fail_effects` 里的字符串（如 `"Stress 2"`）是**效果 DSL**，通过 `Effects[...]` 字典查表变成 `Effect` 对象（`:442`）。`[推断]` 这个字典由别处（`JsonBuffs`/效果表）构建，`grep` 未在本报告范围内展开。
- 运行时类 `Assets\Scripts\Raid\Props\Obstacle.cs:4-28`：`FailEffects` / `HealthPenalty` / `TorchlightPenalty` / `AncestorTalk`。
- **消费点**：`Assets\Scripts\Managers\RaidSceneManager.cs:5740-5823` `ObstacleEvent(sector, handActivation)`：
  - `:5755-5764` 若 `AncestorTalk` → 播放 `ancestor_talk` 旁白（tag = `"ancestor_talk_" + Raid.AncestorTalk`）并把 `Raid.AncestorTalk++`，然后**等旁白播完**（`:5762-5763`）。
  - `:5770-5803` **用手清理**（`handActivation == true`）：播 `obstacle_clear_no_item` 旁白 + `event:/props/obstacles/{StringId}_by_hand` 音效；`TorchlightPenalty < 0` → `TorchMeter.DecreaseTorch(|round(TorchlightPenalty)|)`；`HealthPenalty != 0` → 对**每个英雄**`TakeDamagePercent(|HealthPenalty|)`；`FailEffects` **逐个对每个英雄** `Apply(null, heroUnit, ...)`。
  - `:5804-5808` **不用手**：播 `obstacle` 旁白（tag = `Raid.Quest.Dungeon`，即 `"crypts"` 等）+ `event:/props/obstacles/{StringId}` 音效。**没有任何惩罚**（惩罚留给后续战斗/继续）。
- ✅ 数据字段全部被用上。`health`/`torchlight` 的负浮点是**比例**，代码用 `TakeDamagePercent` / `DecreaseTorch(round|penalty|)` 处理，与 §5.1 的 `-0.05`/`-20.0` 语义一致。
- ⚠️ `[推断]` **`AncestorTalk` 障碍物（`ancestor`，`health: 0.0`, `torchlight: 0.0`, `fail_effects: []`）是一个纯粹的旁白触发器**，不产生任何数值惩罚 —— 与数据完全吻合。

**陷阱** — `Assets\Scripts\Database\DarkestDatabase.cs:453-484` `GetJsonTrapLibrary()`：
```
Trap trap = new Trap(jsonTraps[i].name);
... trap.SuccessEffects / trap.FailEffects ...
trap.HealthPenalty = jsonTraps[i].health;
foreach(var variant in jsonTraps[i].difficulty_variations)
{
    var trapVariation = new TrapVariation();
    trapVariation.Level = variant.level;
    ... SuccessEffects / FailEffects ...
    trapVariation.HealthPenalty = variant.health;
    trap.Variations.Add(trapVariation.Level, trapVariation);
}
```
- DTO：`DarkestJsonReader.cs:486-507`（`JsonTrapDatabase{props}` / `JsonTrap{name, success_effects, fail_effects, health, difficulty_variations}` / `JsonTrapVariation{level, success_effects, fail_effects, health}`）—— **字段 100% 对齐**。
- 运行时类 `Assets\Scripts\Raid\Props\Trap.cs:4-46`：`TrapVariation{Level, SuccessEffects, FailEffects, HealthPenalty}` 与 `Trap{SuccessEffects, FailEffects, HealthPenalty, Variations: Dictionary<int, TrapVariation>}`。
- ⚠️ **实测缺陷**：`trap.Variations` 是**按 `level` 索引的字典**（`Trap.cs:24`, `DarkestDatabase.cs:478`）。数据里 `level` 只有 **3** 和 **5**（每条陷阱各一个）。
- ✅ **变体确实被使用**，且规则是**硬编码**的 —— `RaidSceneManager.cs:5627-5642`（解陷阱成功）与 `:5659-5680`（踩中受伤）：
  ```
  if (CurrentRaid.Quest.Difficulty == 1)        // 只有难度 1 用 base
      { ... trap.SuccessEffects / trap.HealthPenalty / trap.FailEffects ... }
  else
  {
      TrapVariation variation;
      if (trap.Variations.ContainsKey(CurrentRaid.Quest.Difficulty))
          variation = trap.Variations[CurrentRaid.Quest.Difficulty];
      else
          variation = trap.Variations[5];        // 其余一律回落到 level 5
      ...
  }
  ```
  - 即：**难度 1 → base**；**难度 3 → `Variations[3]`**；**难度 5 → `Variations[5]`**；**难度 6（以及任何非 1/3/5 值）→ `Variations[5]`**。
  - ⚠️ `[推断]` **难度 6 的陷阱数值与难度 5 完全相同** —— 数据里没有 `level: 6` 的变体，代码硬回落到 5。这是一处**数据/代码之间靠约定弥合**的地方。
  - ⚠️ **`trap.Variations[5]` 是裸索引**（`:5638`, `:5673`），**没有 `ContainsKey` 保护**。若某条陷阱的数据缺少 `level: 5` 变体，会抛 `KeyNotFoundException`。实测 4 条陷阱**都**有 level 3 和 level 5，所以当前安全。
  - ⚠️ **base 分支（难度 1）与变体分支互斥**：难度 1 时 `trap.SuccessEffects` / `trap.FailEffects` / `trap.HealthPenalty`（base）被使用；难度 ≠ 1 时它们**被完全忽略**。`[推断]` 因此每条陷阱有 **2 组**永远用不到的字段中的一组（要么 base 三个字段，要么 2 个变体）。
- **消费点（其余）**：`DarkestSoundManager.ExecuteNarration("trap", NarrationPlace.Raid)` 在 `RaidSceneManager.cs:5593`（**只在解陷阱失败时播放**）；解陷阱成功率 `:5587-5588`：
  ```
  float disarmChance = RaidPanel.SelectedUnit.Character.GetSingleAttribute(AttributeType.Trap).ModifiedValue;
  disarmChance -= Mathf.RoundToInt(CurrentRaid.Quest.Difficulty / 2.0f) * 0.2f;
  ```
  `[推断]` 难度越高解陷阱越难，**每 2 点难度扣 20%**（难度 1→0、3→20%、5→40%、6→60%）。
  闪避上限 `:5646` `RandomSolver.CheckSuccess(Mathf.Clamp(trapTarget.Character.Dodge, 0, 0.9f))`（**dodge 被 clamp 到 0.9**）。
  音效：`event:/props/traps/{StringId}_disarm`（`:5625`）/ `_dodge`（`:5648`）/ 踩中 `:5653`。

### 5.4 `restriction.resolve_level_threshold_table` 的消费点
`Assets\Scripts\UI\Panels\RaidPartyPanel.cs:27-39`：
```
public static bool IsResolveEligible(Hero hero)
{
    if (DarkestDungeonManager.Campaign.EventModifiers.NoLevelRestrictions) return true;
    int maxLevel = DarkestDungeonManager.Data.QuestDatabase.
            LevelRestrictions[DarkestDungeonManager.RaidManager.Quest.Difficulty];
    if (hero.Resolve.Level > maxLevel) return false;
    return true;
}
```
- ✅ 实测确认语义：`LevelRestrictions` 是**上限表**（"最高允许的 resolve level"），不是下限 —— 与 §1.7 的 `[推断]`**一致**。
- ⚠️ `[推断]` 但表里的 `99`（索引 5、6）在**上限**语义下意味着"任何等级都允许"，这与"难度 5/6 不可接受"的直觉相反 —— 即 `99` 是"**无上限**"哨兵，不是"禁用"哨兵。**§1.7 的原推断（"不可接受"）是错的，此处更正为"无等级上限"。**
- ⚠️ 裸索引 `LevelRestrictions[Quest.Difficulty]`，**无边界检查**；实测表长 7（索引 0-6），而 `plot_quests` 的 `quest.difficulty` 实测取值 ∈ {1,3,5,6}，安全。

---

## 6. 消费上述数据的代码

### 6.1 任务（Quest）数据加载 — `DarkestDatabase.GetJsonQuestDatabase`
`Assets\Scripts\Database\DarkestDatabase.cs:486-740`（全文共 **255 行**）。资源路径常量 `JsonQuestDatabasePath`（`DarkestDatabase.cs:26` 附近），反序列化 → `DarkestJsonReader.cs:653` `JsonQuestDatabase`。

**顶层字段的落点**：
| JSON 路径 | 代码 | 行 |
|---|---|---|
| `stress_damage` | `questData.FailStressPenalty` | `:491` |
| `restriction.difficulty.resolve_level_threshold_table` | `questData.LevelRestrictions` | `:492` |
| `goals` | `questData.QuestGoals`（`Dictionary<string, QuestGoal>`） | `:494-576` |
| `town_progression_goal_ids` | `questData.TownProgressionGoalIds` | `:578` |
| `types` | `questData.QuestTypes` | `:580-595` |
| `plot_quests` | `questData.PlotQuests` | `:598-670` |
| `generation.*` | `questData.QuestGeneration` | `:672-737` |

**`goals` 的 `switch(goal.Type)`（`:507-574`）—— 实测缺陷清单**：
- JSON 里 `goals` 的 `type` 实测有 **9** 种，但代码 `switch` 只处理 **8** 种 case（`tutorial_room`, `kill_monster`, `explore_room`, `battle_room`+`battle` 合并, `gather`, `activate`, `trait_applied`, `deaths_door`）。
- ✅ **更正**：`explore_room` / `battle_room` / `battle` 的 `data` **确实有 `percentage`**（实测 dump 见下）。代码 `:527`/`:538` 用 `double.TryParse` 优先按浮点解析，失败才按 long 取 —— `[推断]` 这个两段式写法是为了兼容 `percentage` 写成整数（如 `1`、`0`）或小数（如 `0.9`）两种情况。
- ✅ **`gather` 的 `data` 结构实测**：`{curio_name, item:{type,id,amount}}` —— 与 `:546-547` 的读取一致（`data["curio_name"]` + `JsonDarkestDeserializer.GetJsonQuestItem(data["item"].ToString())`）。
- ✅ **`activate` 的 `data` 实测**：`{curio_name, amount}` —— 与 `:557-558` 一致。
- ⚠️ **实测发现：7 条 goal 的 `type` 是 `activate`（不是数据里独立的 `inventory_activate`）**。实测 `inventory_activate_corrupted_altar` 等 5 条的 `type` 都是 `"activate"`，`data.amount` 都是 **3**（对应 §1.1 的 `starting_items` 3 个任务道具）；而 `activate_iron_maiden` / `activate_teleporter` 的 `data.amount` 是 **1**。所以"inventory_activate 需要消耗背包道具"这个语义**只由 `amount == 3` + `starting_items` 隐含表达，没有独立的 type 区分**（`[推断]`）。
- ⚠️ **`town_progression_trait` 的 `data` 里有 `is_affliction` 和 `is_virtue`（实测 `{is_affliction:true, is_virtue:false, amount:1}`），但代码 `:561-565` 只读 `amount`！** `is_affliction` / `is_virtue` **从未被读取**。这是一个**确凿的"解析了但从未使用"缺陷**（该 goal 是 4 个 `town_progression_goal_ids` 之一）。
- ⚠️ **`town_progression_explore` / `town_progression_battle` 的 `data.amount`（3 / 2）也没有被对应的 visit data 读取**：`:523-542` 的 `explore_room`/`battle_room`/`battle` 分支**只读 `percentage`，不读 `amount`**。实测这两条的 `percentage` 都是 `0`，`amount` 分别是 3 和 2。`[推断]` **`amount` 被静默丢弃** —— "探索 3 个房间"这个语义在加载后不存在。
- 45 条 goal 的 `data` 完整 dump（`_probe\p19.py` 实测，供逐条核对）：
  ```
  tutorial_final_room      tutorial_room  {"room_id":"room2_1"}
  kill_* (27 条)           kill_monster   {"monster_class_ids":[...],"amount":1|4}
  explore_all_rooms        explore_room   {"amount":0,"percentage":0.9}
  battle_all_rooms         battle_room    {"amount":0,"percentage":1}
  gather_holy_relic        gather         {"curio_name":"reliquary","item":{"type":"quest_item","id":"holy_relic","amount":3}}
  gather_medicines         gather         {"curio_name":"chirurgeons_satchel","item":{...medicines,3}}
  gather_grain             gather         {"curio_name":"foodstuff_crate","item":{...grain_sack,3}}
  gather_shipments         gather         {"curio_name":"shipment_crates","item":{...ancestors_crate,3}}
  activate_iron_maiden     activate       {"curio_name":"iron_maiden","amount":1}
  activate_teleporter      activate       {"curio_name":"teleporter","amount":1}
  inventory_activate_* (5) activate       {"curio_name":"...","amount":3}
  town_progression_explore explore_room   {"amount":3,"percentage":0}
  town_progression_battle  battle         {"amount":2,"percentage":0}
  town_progression_trait   trait_applied  {"is_affliction":true,"is_virtue":false,"amount":1}
  town_progression_deaths_door deaths_door {"amount":1}
  ```
  4 个 `gather` 的 `curio_name` 分别是 `reliquary` / `chirurgeons_satchel` / `foodstuff_crate` / `shipment_crates`；5 个 `inventory_activate_*` 的 `curio_name` 分别是 `corrupted_altar` / `infected_corpse` / `animalistic_shrine` / `protective_ward` / `beacon`。
- ⚠️ **`kill_monster` 的完整目标名与 goal id 不一致**：`kill_drowned_crew_A` 的 `monster_class_ids[0]` 其实是 **`drowned_captain_A`**（不是 `drowned_crew_A`）—— 实测，逐条 dump 可核对。`[推断]` 这是 DD1 原版数据里就存在的命名差异。
- ⚠️ **`kill_monster` 的 `monster_class_ids` 用 `ToString()` + 字符串切割解析**（`:516-519`）：`jsonData.goals[i].data["monster_class_ids"].ToString()` 把 JArray 转成字符串后用 `{'[', ']', ',', '\r', '\n', '"', ' '}` 切割。`[推断]` 这是**极其脆弱**的写法，但该参考项目没有 `data` 的强类型 DTO（`data` 是 `Dictionary<string, object>`），只能这么做。
- ⚠️ **`default` 分支只 `Debug.Log`（`:571-573`）**：实测 `goals` 里 **9 种 type 都能命中 case**（`battle_room`/`battle` 共用 case），所以没有 type 会落到 default。**但 `traits_applied` 之外的语义**（如 `goal.QuestData` 为 null）需要按 type 各自核对。
- ⚠️ **`ignore_fog_of_war` 与 `show_as_quest` 在 DTO 里存在（`DarkestJsonReader.cs:670-671`），但加载逻辑 `:496-505` 从未把它们复制到 `QuestGoal`！** `:496-505` 只赋 `Id` / `Type` / `StartingItems`，**既没读 `show_as_quest` 也没读 `ignore_fog_of_war`**。`grep` 全仓库对 `ShowAsQuest` / `IgnoreFogOfWar`（PascalCase，即运行时属性名）**零命中** —— 说明连运行时类 `QuestGoal` 都没有这两个属性。实测这两个字段在数据里分别有 28/17 和 45 个值 —— **全部在加载环节被丢弃**。这是本报告的"解析了但从未使用"缺陷之一。

**`types` 的展开（`:580-595`）**：
- 关键点：`foreach(var goal in jsonData.types[i].goal_lists[j].goals[k])` —— **内层二维数组被完全"压平"**，`goalList.Goals` 只是把所有可选组合里的 id 依次 `Add` 进去（`:588-590`）。**"组合"（每个内层数组是一组同选的目标）的语义被彻底丢弃**，变成一个大平表。
  - `[推断]` 因为在数据里每个内层数组**最多只有 1 个 id**（实测：除 `kill_boss` 全是空组外，其余都是 `[[x]]` 或 `[[]]`），所以这个压平在当前数据下**没有产生错误结果**，但它对未来的多 id 组合是错的。
- `kill_boss` 的 5 个空组 `[[]]` → `goalList.Goals` 为空列表，**不报错**（`:588` 的 `Count == 0` 使循环不执行）。

**`plot_quests` 的展开（`:598-670`）—— 实测缺陷清单**：
1. ⚠️ **`:610-612` 只允许 1 个 goal id**：
   ```
   if (jsonData.plot_quests[i].quest.goal_ids.Count > 1)
       Debug.LogError("Multiple goals in " + jsonData.plot_quests[i].id);
   plotQuest.Goal = questData.QuestGoals[jsonData.plot_quests[i].quest.goal_ids[0]];
   ```
   实测 30 条 `plot_quests` **每条恰好 1 个 goal id**，所以这里从不报错 —— 但它是硬约束，不是设计。且 `goal_ids` 为**空数组**时会 **IndexOutOfRange**（数据里没有空数组，实测）。
2. ⚠️ **奖励物品被硬编码为最多 3 个**（`:615-636`）：代码写死读 `items.questItem0`、`items.questItem1`、`items.questItem2` 三个键。实测 JSON 里 `items` 是**字符串数字键 map**（`"0"`, `"1"`, `"2"`），而 DTO 用的是 `questItem0/1/2` 命名 —— `[推断]` 靠 Json.NET 的命名策略或 DTO 属性名映射（见 §6.3 的 `JsonRewardItems`）。**超过 3 个奖励物品会被静默丢弃。** 实测 30 条里每条最多 3 个 item（共 72 个），所以当前数据不触发。
3. ⚠️ **`additional_trinket_completion_rewards` 只读 `[0]`**（`:639-645`）：`if (...Count > 0)` 然后只取 `[0].rarity` / `[0].amount`。实测 26 条非空且**每条恰好 1 个元素**，所以当前不丢数据；但**多于 1 个会被丢弃**。
4. ⚠️ **`upgrade_tags_to_remove_on_failure` 完全没有被解析！** `:664-667` 只遍历了 `upgrade_tags_to_remove_on_ignore` → `plotQuest.UpgradeTagsRemovedOnIgnore`。**`upgrade_tags_to_remove_on_failure` 在 `:598-670` 的 73 行里一次都没出现**。实测该字段 30 条全为空数组，所以**当前无实际影响**，但它是一个漏读的字段。
5. ⚠️ **`additional_provisions` 完全没有被解析！** `:598-670` 里没有任何 `additional_provisions` 的引用。实测 30 条的 `items` 全为 `{}`，所以当前无影响 —— **又一个漏读字段**。
6. ⚠️ **`quest.completion_reward.items_definition.system_config_type` 没有被读取** —— `:613-619` 只读 `resolve_xp` 和 `items`。实测 30 条全为 `"quest_rewards"`（常量），所以无影响。
7. ⚠️ **`is_plot_quest` 被读进 `plotQuest.IsPlotQuest`（`:604`），但实测 30/30 全为 true** → 这个字段在数据里没有区分度。
8. ✅ `plot_quest_dependency` 被读取（`:602`），实测 26 条为 `null`（键不存在时 Json.NET 给 `null`），4 条有值。

### 6.2 任务生成参数 — `QuestGenerationData`（`:672-737`）
| JSON 路径 | 代码目标 | 行 | 备注 |
|---|---|---|---|
| `generation.number.number_of_quests_per_town_visit_table` | `QuestsPerVisit` | `:674` | 8 档 |
| `generation.dungeon.max_number_of_generated_quests_per_dungeon` | `MaxQuestsPerDungeon` | `:675` | = 4 |
| `generation.dungeon.generated_dungeons` | `Dungeons` 字典 | `:676-682` | 4 条 |
| `generation.difficulty.generated_resolve_level_difficulties` | `Difficulties` 列表 | `:683-689` | 3 档 |
| `generation.type.available_quests_table` | `QuestTypes` 字典 | `:690-708` | 4 地牢 |
| `generation.type...chance` | `GeneratedQuestType.Chance` | `:702` | **实测全部 = 1** |
| `generation.rewards.heirloom_type_map` | `HeirloomTypes` | `:709-710` | 4 条 |
| `generation.rewards.heirloom_amount_table` | `HeirloomAmounts` | `:711-712` | 4 条 × amounts[6] |
| `generation.rewards.item_table` | `ItemTable`（**三维交错数组** `ItemDefinition[][][]`） | `:714-729` | 逐层 `Length`，**保留空数组** |
| `generation.rewards.resolve_xp_table` | `ResolveXpReward`（`int[][]`） | `:731` | 7×4 |
| `generation.rewards.trinket_chance_table` | `TrinketChances` 字典（rarity → `int[][]`） | `:732-734` | 6 条 |
| `restriction.difficulty.resolve_level_threshold_table` | `ResolveThreshold` | `:736` | 7 项 |

- ✅ **`item_table` 的空数组被保留**：`:714-720` 用 `Length` 逐层分配，所以 `item_table[0] = new ItemDefinition[0][]`（长度为 0），**不是 null**。`[推断]` 消费方必须自己处理 `Length == 0`，否则难度 0/2/4 会越界。
- ⚠️ **`generation.rewards.comment` 是唯一没有被读取的 rewards 子键**（它是说明字符串，无功能影响）。
- ⚠️ `[推断]` **`heirloom_amount_table` 的 `amounts` 只有 6 项，而 `resolve_xp_table` / `trinket_chance_table.chances` 都是 7 行**。代码在 `:711-712` 直接把 `amounts` 存进字典，不做长度校验 → **用同一个"难度"索引去取 `amounts` 时，难度 6 会 IndexOutOfRange**（若消费方按 `[difficulty]` 取）。

### 6.3 任务相关 DTO（`DarkestJsonReader.cs:653-870`）—— 逐类实测字段数
| 类 | 行 | 声明字段 |
|---|---|---|
| `JsonQuestDatabase` | 653 | `stress_damage`, `goals`, `town_progression_goal_ids`, `types`, `plot_quests`, `generation`, `restriction` |
| `JsonQuestGoal` | 665 | `id`, `type`, `starting_items`, `ignore_fog_of_war`(670), `show_as_quest`(671), `data`(672) —— ✅ **6 个字段，DTO 完整**；但加载逻辑不读后两个（见 §6.1） |
| `JsonQuestItem` | 674 | `id`, `type`, `amount` |
| `JsonQuestType` | 683 | `id`, `goal_lists` |
| `JsonQuestTypeGoal` | 688 | `dungeon`, `goals` |
| `JsonPlotQuest` | 696 | （见下，含 20 个字段） |
| `JsonPlotQuestData` | 718 | （内层 `quest`，8-9 个字段） |
| `JsonRewardItems` | 754 | `questItem0`(756 `[JsonProperty("0")]`), `questItem1`(758 `[JsonProperty("1")]`), `questItem2`(760 `[JsonProperty("2")]`) —— ⚠️ **硬编码上限 3**，JSON 的 `items` map 只映射 `"0"/"1"/"2"` |
| `JsonQuestGeneration` | 766 | `number`, `dungeon`, `difficulty`, `type`, `rewards` |
| `JsonQuestGenerationNumbers` | 776 | `number_of_quests_per_town_visit_table` |
| `JsonQuestGenerationDungeons` | 780 | `max_number_of_generated_quests_per_dungeon`, `generated_dungeons` |
| `JsonQuestGenerationDungeonData` | 785 | `id`, `required_number_of_quests_finished` |
| `JsonQuestGenerationDifficulties` | 793 | `generated_resolve_level_difficulties` |
| `JsonQuestGenerationResolveDifficulty` | 797 | `resolve_levels`, `difficulty` |
| `JsonQuestGenerationTypes` | 805 | `available_quests_table` |
| `JsonQuestGenerationTypeData` | 809 | `dungeon`, `generated_quest_table` |
| `JsonQuestGenerationRewards` | 823 | `heirloom_type_map`, `heirloom_amount_table`, `item_table`, `resolve_xp_table`, `trinket_chance_table` —— ⚠️ **没有 `comment`** |
| `JsonQuestGeneratedItemTableData` | 846 | `type`, `id`, `amount` |
| `JsonQuestRestrictions` | 856 | `difficulty` |
| `JsonQuestRestrictionsDifficulty` | 860 | `resolve_level_threshold_table` |

- ✅ **`JsonQuestGoal` 缺少 `show_as_quest` / `ignore_fog_of_war`** 与 §6.1 观察到的"这两个字段从未被读取"**完全一致** —— 即**在 DTO 层就已经丢掉了**，不是加载逻辑漏读。

### 6.4 其它任务的消费点（`grep "Quest"` 的高价值命中）
- `Assets\Scripts\Raid\Dungeon.cs:108-117`：为 quest goal 的 `StartingItems[0]` **动态往 curio 上挂一条 `"loot"` 交互**（把 `goal.StartingItems` 与掉落系统连起来）：
  ```
  curio.ItemInteractions.Add(new ItemInteraction(1, quest.Goal.StartingItems[0].Id, "loot"));
  curio.Results.Add(new CurioInteraction(1, "loot"));
  ```
  ⚠️ `[推断]` 硬取 `StartingItems[0]`，若 goal 没有 starting item 会 **IndexOutOfRange**（实测 45 条 goal 里只有 5 条有 starting items）。
- `RaidSceneManager.cs:155,157`：`quest_start` 旁白，tag 用 `CurrentRaid.Quest.Id`（即 `plot_quests[].id`，如 `"plot_kill_necromancer_1"`）；`ScreenLoader.cs:34,49` 用 `currentQuest.Id` 或 `currentQuest.Type, currentQuest.Dungeon` —— 与 `Narration.json` 里 `audio_events[].tags` 的取值（实测 tags 大量使用 `plot_kill_necromancer_1` 这类 id）**对应**。
- `RaidSceneManager.cs:5806`：`obstacle` 旁白 tag = `Raid.Quest.Dungeon`。
- `Campaign.cs:138-141`：城镇事件用 `RandomSolver.CheckSuccess(EventsOption.Frequency[3])` —— **硬编码索引 3**（城镇事件不在本报告范围，仅记录）。

### 6.5 掉落（Loot）的消费点（汇总）
已在 §2.6 / §2.7 / §2.8 详述，此处只列索引：
- 加载：`DarkestDatabase.cs:742-824`（+ `:1704-1707` `LoadJsonLoot()`）
- 掷骰核心：`RaidSolver.cs:149-157` `GetLootEntry`
- 三种产出：`RaidSolver.cs:8-52` / `:54-101` / `:103-147`
- 战斗掉落触发：`BattleGround.cs:480-498`（`darkness_bonuses["battle"]` + `ExtraBattleLoot`）、`BattleGround.cs:1013-1028`（死亡单位 `Loot`）
- 事件层：`RaidEvents.cs:451` `LoadCurioLoot(...)`；`RaidSceneManager.cs:395-398, 5405-5416, 2143-2150, 5039-5073`
- 怪物/职业侧 `LootDefinition` 解析：`DarkestDatabase.cs:2217-2219`（`"loot:"`）、`HeroClass.cs:202-212`（`extra_battle_loot:` / `extra_curio_loot:`）

### 6.6 旁白（Narration）的消费点
- 加载：`DarkestDatabase.cs:1415-1440+` `LoadNarration()`
- 执行：`DarkestSoundManager.cs:54-127` `ExecuteNarration`（唯一入口）
- 可能性判定：`NarrationAudioEvent.cs:24-83` `IsPossible`
- 队列播放：`DarkestSoundManager.cs:32-52` `Update()`（FIFO，一次只播一个）
- 雕像特例：`DarkestSoundManager.cs:129-137` `PlayStatueAudioEntry`（绕过 Narration 数据）
- 全部调用点见 §3.6

### 6.7 队伍名（PartyNames）的消费点
- 加载：`DarkestDatabase.cs:1401-1413`
- 唯一使用：`PartyCompositionPanel.cs:16`
- 本地化类别：`LocalizationManager.cs:36`

---

## 7. 解析了但从未使用的字段 / 数据（缺陷清单）

> 判定方法：字段在 DTO / 加载代码里**被赋值**，然后在全仓库 `grep` 搜索其**运行时属性名的读取处**（排除赋值行与声明行），无命中即判为"从未使用"。所有条目都给出反证位置。

### 7.1 确凿缺陷（有明确反证）

| # | 数据路径 | 证据 | 影响 |
|---|---|---|---|
| 1 | `Narration.filters`（顶层，**1** 个元素 `["plot_darkest_dungeon_4"]`） | `DarkestDatabase.cs:1415+` `LoadNarration()` **只遍历 `entries`**；DTO 无 `filters` 字段 | 整个顶层字段被丢弃 |
| 2 | `Narration.entries[].tone`（36 条，`bad`20/`good`9/`neutral`7） | `DarkestDatabase.cs:1425` 赋值 `NarrationEntry.Tone`；`NarrationEntry.cs:6` 声明；**全仓库无读取** | `tone` 完全无用 |
| 3 | `Narration.entries[].priority`（仅 `obstacle` 有，值 `0`） | `DarkestDatabase.cs:1422-1425` **只赋 Id/Chance/Tone，没读 `priority`**；`NarrationEntry.cs:4-8` **无 `Priority` 属性** | 解析阶段即丢失 |
| 4 | `Narration.entries[].audio_events[].queue_while_audio_playing`（465 条） | `DarkestDatabase.cs:1430` 赋值 `QueueWhilePlaying`；`NarrationAudioEvent.cs:8` 声明；**`IsPossible`/`ExecuteNarration` 均无读取** | 与 `queue_only_on_empty` 布尔取反冗余（实测 266/199 完全互补） |
| 5 | `Narration.entries[].audio_events[].filter`（465 条，其中 **7** 条非空） | `DarkestDatabase.cs:1437` 赋值 `Filter`；`NarrationAudioEvent.cs:15` 声明；**`IsPossible`（`:24-83`）从没碰 `Filter`** | 7 条 `"plot_darkest_dungeon_4"` 过滤**完全无效** |
| 6 | `JsonQuests.goals[].show_as_quest`（45 条，`true`28/`false`17） | DTO **有**（`DarkestJsonReader.cs:671`）；加载 `DarkestDatabase.cs:496-505` **不读**；运行时 `QuestGoal`（`Campaign\Quests\QuestGoal.cs:5-9`）**无该属性** | 全部丢弃 |
| 7 | `JsonQuests.goals[].ignore_fog_of_war`（45 条，**全 false**） | DTO **有**（`DarkestJsonReader.cs:670`）；加载 `:496-505` **不读**；`QuestGoal` **无该属性** | 全部丢弃 |
| 8 | `JsonQuests.goals[].data.is_affliction` / `is_virtue`（`town_progression_trait`，实测 `true`/`false`） | `DarkestDatabase.cs:561-565` 的 `trait_applied` 分支**只读 `data["amount"]`** | 该 goal 的 affliction/virtue 语义丢失 |
| 9 | `JsonQuests.goals[].data.amount`（`explore_room` / `battle_room` / `battle`，实测 0/0/2/3） | `DarkestDatabase.cs:523-542` **只读 `percentage`，不读 `amount`** | "探索 N 个房间"的 N 丢失 |
| 10 | `JsonQuests.plot_quests[].upgrade_tags_to_remove_on_failure`（30 条**全空**） | DTO **有**（`DarkestJsonReader.cs:714`）；加载 `:598-670`（73 行）**从未引用** | 当前无影响（数据为空），但字段漏读 |
| 11 | `JsonQuests.plot_quests[].additional_provisions`（30 条，`items` **全 `{}`**） | 加载 `:598-670` **从未引用**；DTO `JsonPlotQuest`（`:696-717`）**也没有该字段** | 当前无影响（数据为空） |
| 12 | `JsonQuests.plot_quests[].quest.completion_reward.items_definition.system_config_type`（30 条全 `"quest_rewards"`） | 加载 `:613-619` 只读 `resolve_xp` + `items`；DTO `JsonItemsDefinition`（`:744-748`）**有该字段但无读取** | 常量，无影响 |
| 13 | `JsonLoot.darkness_bonuses` 的 **`chest`** 那 5 条 | `grep "DarknessLoot"` 全仓库只有 2 处：`DarkestDatabase.cs:760`（写入）与 `BattleGround.cs:480`（**硬取 `["battle"]`**） | `chest` 的 5 条奖励**永不生效** |
| 14 | `JsonLoot.loot_tables` 里 **24** 个孤儿表 id | §2.5 的引用图实测（`A` 之外的 24 个 id 从未被任何 `table` entry 引用） | 死数据 |
| 15 | `JsonQuests.types[].goal_lists[].goals` 的**内层组合语义** | `DarkestDatabase.cs:588-590` 把二维数组**压平**进一个 `GoalList.Goals` | 当前数据每个内层数组最多 1 个 id，故无错误；对未来多 id 组合是错的 |
| 16 | `JsonLoot` 里 `chances == 0` 的 **65** 条 entry | `RandomSolver.cs:49` `Sum(item => item.Chance > 0 ? item.Chance : 0)` → 计入分母但选不中 | 纯占位 |
| 17 | `table A` 的 `journal_page` entry（`chances = 0.05`） | 同上，`RandomSolver.cs:49` 的 `UnityEngine.Random.Range(int, int)` 会把总权重**截断为 int** | `[推断]` 该 entry **实际不可达** |

### 7.2 数据存在但代码从不触发（"死"事件 id）
- `Narration` 的 **36** 个 event id 中，实测 **3** 个在 `Assets\Scripts\**` 里没有任何 `ExecuteNarration("<id>"` 调用点：
  `half_health_half_stress`（1 条 audio_event）、`hunger`（1 条）、`enter_hallway`（3 条）。
  - 注：`hunger_starve` **有**调用点（`RaidSceneManager.cs:5933`），但它的兄弟 `hunger` **没有**。
- `Narration` 的 `half_health_half_stress` 与 `hunger` 的 `chance`：实测 `half_health_half_stress` = 1，`hunger` = **0**（§7.1 表格外的额外确认）。

### 7.3 数据侧的"空/常量"字段（不是代码缺陷，但无区分度）
| 数据 | 实测 |
|---|---|
| `JsonQuests.goals[].ignore_fog_of_war` | 45/45 全 `false` |
| `JsonQuests.plot_quests[].retreat_always_from_raid` | 30/30 全 `false` |
| `JsonQuests.plot_quests[].quest.is_plot_quest` | 30/30 全 `true` |
| `JsonQuests.plot_quests[].additional_provisions.items` | 30/30 全 `{}` |
| `JsonQuests.plot_quests[].upgrade_tags_to_remove_on_failure` | 30/30 全 `[]` |
| `JsonQuests.generation.type.*.chance` | 全部为 `1` |
| `JsonQuests.generation.rewards.trinket_chance_table.very_common` | 7×4 **全 0** |
| `JsonLoot.loot_tables[T].ancestral_shambler` | 4 个难度变体**全 0** |
| `JsonLoot.loot_tables[S].firewood` | `chances = 0`（**永远掉不出**） |
| `Curios\Obstacles.json` 的 `ancestor` | `fail_effects: []`、`health/torchlight: 0.0`（纯触发器） |
| `Curios\Traps.json` 的 `lurker` | 2 个 `difficulty_variations` 与 base **逐字相同** |
| `Curios\Traps.json` 的 4 条陷阱 | 12 处 `success_effects` **全是 `["Heal Stress TrapD"]`** |

### 7.4 代码侧的真实 bug（本报告实测确认）
1. **`RaidSolver.cs:92` 自赋值**：`trinketDef.Amount = trinketDef.Amount;`（另两个重载在 `:44` / `:139` 是 `= 1`）→ 战斗掉落路径拿到的饰品 `Amount` 恒为 **0**。
2. **`RaidSolver.cs:151` 裸字典索引**：`LootDatabase.LootTables[tableId.ToUpper()]` → 未知表 id 抛 `KeyNotFoundException`；实测数据里 **`J` 就是这样一个悬空引用**（`JsonLoot.json:39`）。
3. **`RaidSolver.cs:155` 可能为 null**：`List.Find` 返回 `null` 时下一行 `lootTable.Entries` 抛 `NullReferenceException`。
4. **`RaidSolver.cs:156` 无环/深度保护**的递归。
5. **`DarkestSoundManager.cs:108-125` 的 `goto case` 双重计数**：`Raid` 与 `Town` 都会 fallthrough 到 `Campaign`，同时消耗两种配额。
6. **`DarkestSoundManager.cs:71-72` 的 `combat_start` 硬编码特例**：恒取 `possibleEvents[0]`，不随机。
7. **`NarrationAudioEvent.cs:60-61`**：`CheckAllTags == true` 且调用方不传 tags 时**必然 `return false`** —— 实测 454/465 条 audio_event 是 `check_all_tags: true`，而很多调用点（`loot`/`trap`/`victory`）不传 tags。
8. **`DarkestDatabase.cs:610-612`**：`goal_ids.Count > 1` 只 `LogError` 不处理；`goal_ids` 为**空数组**会 `IndexOutOfRange`。
9. **`DarkestDatabase.cs:615-636` / `:754-762`**：奖励物品硬编码上限 **3**（`questItem0/1/2`）。
10. **`DarkestDatabase.cs:639-645`**：`additional_trinket_completion_rewards` 只读 `[0]`。
11. **`RaidSceneManager.cs:5638` / `:5673`**：`trap.Variations[5]` 裸索引，无 `ContainsKey` 保护。
12. **`Dungeon.cs:108`**：`quest.Goal.StartingItems[0]` 裸索引。
13. **`RaidSolver.cs:38-40` / `:86-88` / `:133-135`**：`trinketList[RandomSolver.Next(trinketList.Count)]` —— 空列表时 `Next(0)` 返回 0 → `IndexOutOfRange`。
14. **`RaidPartyPanel.cs:33`**：`LevelRestrictions[Quest.Difficulty]` 裸索引，无边界检查。

---

## 8. 参考项目中**缺失**的东西（明确声明）

以下内容在参考项目中**确实不存在**（已 `grep`/`glob` 确认），在设计移植时不能指望从这里抄：

1. **没有独立的"任务定义列表"**。`JsonQuests.json` 只有 `plot_quests` 30 条（全是 `is_plot_quest: true`）；**没有任何一条 `is_plot_quest: false` 的静态任务定义**。随机任务完全靠 `generation` 参数在运行时合成。
2. **没有战术/条件任务**（如"只带 2 人""不使用火把"）。`JsonQuests` 里既无对应字段也无代码路径。
3. **没有 boss 目标的随机生成支持**。`types[kill_boss]` 的 5 个 `goal_lists` **全部是空组**（§1.3），所以随机任务永远拿不到 boss goal；boss 只出现在写死 `goal_ids` 的 `plot_quests` 里。
4. **`Narration.json` 里没有"字幕/文本"字段**。只有 `audio_event`（FMOD 路径，如 `/vo/load/crypts_01`）、`tone`、`chance`、`priority`、配额与 tags —— **没有任何可显示的字符串**。字幕必须来自本地化表（`LocalizationManager.cs:36` 的类别列表，含 `PartyNames`）。
5. **`PartyNames.json` 里没有"名字"**。只有 `id`（`"0"`–`"185"`）与 `required_hero_class`（4 个职业）；队伍名的显示字符串**完全不在这个文件里**。
6. **没有掉落表与 curio 表之间的静态关联数据**。`JsonLoot.json` 里**没有** `curio` / `monster` 的归属字段；关联写在别处：怪物/职业用 `Assets\Scripts` 里的 `"loot:"` 一条条（`DarkestDatabase.cs:2217-2219`、`HeroClass.cs:202-212`），curio 用 `Dungeon.cs:108-117` 动态挂 `"loot"`。
7. **没有 `darkestdungeon` / `town` 地牢的掉落表**。`JsonLoot.loot_tables[].dungeon` 实测只有 `""` / `crypts` / `weald` / `warrens` / `cove` 五种取值（§2.1）—— 而 `JsonQuests` 的 `quest.dungeon` 实测含 `darkestdungeon`(4) 和 `town`(1)。`[推断]` DD 与城镇任务只能落到 `dungeon == ""` 的通用表（`C`/`G`/`T`/`H(6)`）。
8. **没有陷阱的"等级 6"变体**。`Curios\Traps.json` 的 `difficulty_variations` 只有 `level: 3` 和 `level: 5`（4 条 × 2）—— 难度 6 靠代码硬回落到 5（§5.3）。
9. **没有障碍物的难度变体**。`Curios\Obstacles.json` 的 5 条**没有** `difficulty_variations` 字段（对照陷阱有）。
10. **没有 `JsonLoot.json` 里 `table P` 之外的第二张"补给"表被主流程引用** —— 实测 `P` 只被 `table A` 以 `chances = 0` 引用（§2.4），即**也拿不到**。
11. **没有 `JsonQuests` 的 `goals` 与 `types` 之间的显式"反向索引"**。`types` 里写的是 goal id 字符串，代码**不做存在性校验**（`DarkestDatabase.cs:588-590` 直接 `Add`）。`[推断]` 若 `types` 里写了不存在的 goal id，会在**后续使用**时才炸（`QuestGoals[...]` 裸索引）。
12. **`Curios\Obstacles.json` / `Curios\Traps.json` 都没有 `id` 字段，只有 `name`**；而 `RaidSceneManager.cs:5773`/`:5807`/`:5625` 用 `obstacle.StringId` / `trap.StringId` 拼 FMOD 路径 `event:/props/obstacles/{StringId}` / `event:/props/traps/{StringId}` —— `[推断]` `StringId` 就是 JSON 的 `name`（`DarkestDatabase.cs:440` / `:462` 传入构造函数）。
13. **没有静态 JSON 里定义"任务失败惩罚"的完整表**：只有顶层标量 `stress_damage: 20`（`JsonQuests.json:2`），以及 `plot_quests[].roster_buffs_to_apply_on_failure`（只有 4 条 DD 任务非空）。

---

## 探测日志（可复现）

所有计数由下列脚本直接产出（脚本位于 `F:\GithubPro\Darkest\reports\unity_ref\_probe\`）：

| 脚本 | 产出 |
|---|---|
| `p2.py` | 6 个 JSON 的原始长度 / 尾随逗号数 / 顶层形状（JsonQuests 1 处尾随逗号，Traps 8 处，其余 0） |
| `p3.py` | `JsonQuests` 顶层 7 键及类型/数量 |
| `p4.py` | `plot_quests[0]` 全文、`types` 全文 |
| `p6.py` | `goals` 键集/type 分布、`types` 展开、`generation.rewards` 全文 |
| `p7.py` | rewards 5 张表的维度 |
| `p8.py` | `plot_quests` 全部枚举字段统计 |
| `p9.py` | `completion_reward` / `additional_provisions` / `plot_quest_dependency` 结构 |
| `p10.py` | Obstacles=**5**、Traps=**4**、loot 顶层 / entry keyset / entry type 分布 |
| `p11.py` | 54 张掉落表逐条 dump（id/dungeon/difficulty/每一条 entry） |
| `p12.py` | 掉落表引用图（含悬空 `J`）、24 个孤儿 id、`chances` 取值域、`darkness_bonuses` 全文 |
| `p13/p15/p16/p17/p18.py` | Narration 顶层、36 个 entry id、465 条 audio_event 的 11 字段分布、异常值（`chance:0`=`hunger`、`chance:4`=`town_visit_start`） |
| `p14.py` | PartyNames=**186**、唯一 keyset `{id, required_hero_class}` |
| `p19.py` | 45 条 goal 的 `data` 逐条 dump |

**计数方法声明**：
- 「N 条」= Python `len()` 作用于 `json.loads(re.sub(r",(\s*[\]}])", r"\1", raw))` 的结果。
- 「字段数」= `len(obj.keys())`；「键集一致」= `collections.Counter(tuple(sorted(x.keys())) for x in items)` 只有 1 个键。
- 「分布」= `collections.Counter(x[field] for x in items)`。
- 「行号」= `grep` / `read` 工具在**原始文件**上返回的行号（不是解析后的）。
- 本报告所有"从未被使用"的判定 = `grep` 运行时属性名（PascalCase）在 `Assets\Scripts\**\*.cs` 上的命中分析。
