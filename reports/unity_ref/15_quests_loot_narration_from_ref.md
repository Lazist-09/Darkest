# A8：任务 / 战利品 / 旁白 / 队伍名 —— 抽取与引用完整性

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_quests_loot_narration.py`（可重跑）·
> 产物 `reports/unity_ref/quests_loot_narration_from_ref.json`（330,387 bytes）✓
> 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

## 1. 四份数据（**我方全部无对应** ⇒ 整表缺失）

| 文件 | 大小 | 顶层 | 关键读数 |
|---|---|---|---|
| `JsonQuests.json` | 148,877 B | **7 键** | 🔴 **有尾随逗号** · `goals` **45** · `types` **6** · `plot_quests` **30** |
| `JsonLoot.json` | 31,728 B | 2 键 | `loot_tables` **54 张 / 33 个 id** · `darkness_bonuses` **2** |
| `Narration.json` | 275,271 B | 2 键 | `entries` **36** · `filters` **1** |
| `PartyNames.json` | 20,748 B | 1 键 | `party_names` **186** |

🎖️ **四项与我方现状**（实测）：`provisions.json` / `items.json` 之外，
   我方**也没有** `quests` / `loot` / `narration` / `party_names` ⇒ **全缺** ✓

## 2. `JsonQuests.json`：**顶层 7 键里没有"任务数组"**（复核 `§10.2 ①`）

```
`stress_damage`(int **20**) · `goals`(**45**) · `town_progression_goal_ids`(**4**) ·
`types`(**6**) · **`plot_quests`(30)** · `generation`(5 子键) · `restriction`(1 子键) ✓
⇒ 🔴 **只有那 30 条 `plot_quests` 是真任务** ✓（与 `§10.2 ①` 一致）

`types` 6 个 id：`kill_boss` · `explore` · `cleanse` · `gather` · `activate` ·
   `inventory_activate` ✓
`goals` 45 条的字段：`id` · `type` · `data` · **`show_as_quest`** · **`ignore_fog_of_war`** ·
   `starting_items` ✓
   🔴 而 `§10.2 ④` 已实测：**`show_as_quest`（28 真/17 假）与 `ignore_fog_of_war`
      在 DTO 里但从不拷进 `QuestGoal`** ⇒ **加载会丢真数据** ⚠️
`plot_quests` 30 条的字段（**19 个**，含大量任务级开关）：
   `id` · `quest` · `dungeon_level` · `is_progression` · `plot_quest_dependency` ·
   `can_retreat` · `retreat_always_from_raid` · `retreat_party_kill_count` ·
   `is_scouting_enabled` · `is_surprise_enabled` · `is_roster_stress_cleared_on_completion` ·
   `completion_dungeon_xp` · `has_statue_contents` · `additional_provisions` ·
   `additional_trinket_completion_rewards` · `suggested_trinkets` ·
   `roster_buffs_to_apply_on_failure` · `roster_buff_on_failure_minimum_party_resolve_level` ·
   `upgrade_tags_to_remove_on_failure` · `upgrade_tags_to_remove_on_ignore` ✓
   🎖️ **信息量很大**：这 19 个字段里有 **7 个是"失败/忽略"路径**的
      （`..._on_failure` ×3 · `..._on_ignore` ×1 + `can_retreat`/`retreat_*` ×3）
      ⇒ **任务不只是"目标"，还有成败分支的后果** ✓
```

## 3. `JsonLoot.json`：**54 张表 / 33 个 id**（复核 `§10.2 ②`）

```
变体最多的：**`H` 13 个** · `C` 4 · `G` 4 · `T` 4 · 其余 29 个各 1 个 ✓
   ⇒ 🎖️ **与 `§10.2 ②` 的"H 一个 id 就有 13 个变体"逐数相符** ✓
表的字段：`id` · `difficulty` · `dungeon` · `entries[]` ✓
`entries[]` 的 `type` 分布：**`item` 126** · **`trinket` 51** · `nothing` 48 · **`table` 25** ·
   `journal_page` 3 ✓
   🎖️ **即：战利品表能产出 4 类东西**（物品 / 饰品 / 空 / 子表 / 日志页）✓
```

## 4. 🔴🔴 引用完整性：**我先数错了一次，然后数对了**

```
🔴 **第一版（错）**：把所有像 id 的字符串**混成一个集合**去比 ⇒ 报出 **24 个"未命中"** ⚠️
🔴 **根因**：`.data.table` 里是**表名**（33 个命名空间）· `.data.id` 里是**物品名**
   （57 个命名空间）⇒ 🔴 **两个不同的命名空间被我混成了一个** ⚠️
   ⇒ 📌 **纪律 AU 的又一实例**（本任务第 N 次）：**两个命名空间不能混成一个集合** ✓
✅ **修正后（按 `entry.type` 分命名空间查）**：

| 引用类别 | 引用数 | 命中 | 🔴 悬空 |
|---|---|---|---|
| **表名**（`type=table` 的 `data.table`） | **9** | 8 | **1（`J`）** |
| **物品名**（其余 entry 的 `data.id`） | **22** | **22** | **0** ✅ |

⇒ 🎖️ **即：那 24 个"未命中"里有 23 个是【假报】** ✓
```

## 5. 🎖️ 复核 `§10.2 ②` 报的"`table A` 引用了不存在的 `table J`"—— **确认，且它是不可达的**

```
实测表 `A` 的 `entries`：`table C (14)` · `table G (8)` · `table H (12)` ·
   **`table J`（`chances` = 0）** · `table P (0)` · `table S (7)` · `table T (…)` ✓
⇒ ✅ **`§10.2 ②` 的结论【成立】**：`J` **确实被引用而表中不存在** ✓
🎖️ **而本件补了一条它没说的**：🔴 **`J` 的 `chances` 是 0** ⇒ **该分支不可达** ✓
   ⇒ 📌 **即：这是"数据里有死引用"，不是"运行时会崩"** ⚠️
   ⇒ 而 `§10.2 ②` 说 `RaidSolver.cs:151` 是**裸字典索引** ⇒
      ⚠️ **若某天有人把 `chances` 从 0 改成正数 ⇒ 立刻 `KeyNotFoundException`** ✓
   🆕 **全部 `chances == 0` 的表引用只有 2 个**：`J`（**目标不存在**）· `P`（存在）✓
      ⇒ ✅ **判据**：**"这个悬空引用，现在可达吗？"** ——
         **不可达 ⇒ 是隐患不是故障；可达 ⇒ 是故障** ✓（与红线 20 ⑤ 同族：**要看"有没有信息"**）✓
```

## 6. `Narration.json` / `PartyNames.json`

```
`Narration.json` ⇒ `entries` **36** 个事件 · `filters` **1** ✓
   entry 字段：`id` · `chance` · `priority` · **`tone`** · `audio_events` ✓
   事件 id 样例：`loading_screen_start` · `quest_start` · `quest_end_completed` ·
      `quest_end_not_completed` · `combat_start` · `kill_monster` · `kill_hero` · `crit_monster` ✓
   🔴 而 `§10.2 ③` 已实测：**5 个字段被解析后从不读**（顶层 `filters` · `entries[].tone` ·
      `entries[].priority`（**连属性都没有**）· `queue_while_audio_playing` · `filter`），
      且 **3 个事件 id 完全没有调用点**（`half_health_half_stress` / `hunger` / `enter_hallway`）✓

`PartyNames.json` ⇒ `party_names` **186** 条 ✓
   🔴 **键只有 `{id, required_hero_class}`** ⇒ **文件里【没有名字字符串】** ✓
      🎖️ **与 `§10.2 ⑤` 完全一致** ⇒ **名字来自本地化分类 `"PartyNames"`** ✓
      样例：`{id:"0", required_hero_class:[vestal, plague_doctor, highwayman, crusader]}` ✓
      ⇒ 📌 **即：这 186 条是"队伍组成的约束"，不是"名字"** ⇒ **文件名有误导性** ⚠️
```

## 7. 诚实边界

```
✅ **能验**：四份的规模与字段全集 · `types` 6 个 id · `plot_quests` 19 字段 ·
   `loot_tables` 54/33 + `type` 分布 · **引用完整性按命名空间分开查（表 8/9 · 物品 22/22）** ·
   **`J` 的 `chances` = 0（不可达）** · `PartyNames` 无名字串 —— **全部当场跑出** ✓
🎖️ 并**如实记下我把两个命名空间混成一个的错**（24 个"未命中"里 23 个是假报）✓
🔴 **不能验**：**没有落任何任务/战利品/旁白数据** ⇒ "接上后任务系统会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：`plot_quests` 那 19 个字段的**语义未逐个核**（尤其 7 个"失败/忽略"分支）⇒ 记**未验** ✓
🔴 **不能验**：`generation`(5 子键) 与 `restriction`(1 子键) 的**内部结构未展开** ⇒ 记**未做** ✓
🔴 **不能验**：`Narration` 的 36 个事件与 `§10.2 ③` 那 3 个"无调用点"的 id **未逐个复核** ⇒ 记**未验** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a8_*.py` **不入库**（scratch）✓
```

## 8. 下一步

```
🆕 **可做（归下一轮）**：① `plot_quests` 19 字段逐个核（尤其 7 个失败/忽略分支）
   ② `generation` / `restriction` 子结构展开
   ③ `Narration` 36 事件 vs `§10.2 ③` 的 3 个无调用点 id 复核 ✓
⏸️ **等策划**：三张单已投递（Σ / A6 / A9+A10+总口径）✓
📌 **A 线状态：A1~A12 全部【抽取/读数】完成** —— 剩下的都是「落库」与「等裁定」✓
```
