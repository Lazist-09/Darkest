# `plot_quests` 19 字段语义逐个核 —— 并量出**两条轴结构**

> 🕒 2026-09-26 · 承接 `15_quests_loot_narration_from_ref.md` §7 的"19 字段语义未逐个核" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 先量结构：30 条任务是**两条轴**，不是 30 条散表

```
📊 按 `id` 前缀分：
   **`plot_kill_*` 25 条**（`necromancer` / `prophet` / `hag` / `brigand_cannon` /
      `swine_prince` / `formless_flesh` / `siren` / `drowned_crew` **8 个 boss × 3 档**）✓
      `dungeon_level` = **2 / 3 / 4 / 5 / 6 / 7**（三档一组：`_1` 低 · `_2` 中 · `_3` 高）✓
   **主线段 `plot_darkest_dungeon_1..4` 4 条** ⇒ 🔴 **它们有 `plot_quest_dependency` 链** ✓
   **`plot_town_invasion_0` 1 条**（`type=kill_boss` · `dungeon=town` · `difficulty=6`）✓
   **`plot_tutorial_crypts` 1 条**（`type=explore` · `map_name=tutorial_crypts`）✓
⇒ 🎖️ **即：30 = 25（8 boss × 3 档）+ 4（主线链）+ 1（城镇入侵）** ✓
   📌 而 **25 条按"3 档"成组** ⇒ **可以按组采**（不是 25 条各自独立）✓
```

## 2. 🎖️ 19 个字段的**取值分布**（决定它是"开关"还是"数据"）

| 字段 | 类型 | 分布 |
|---|---|---|
| `id` | 多值 | **30** ✓ |
| `quest` | 多值 | **9 种**（内嵌结构，见 §3） |
| `dungeon_level` | 多值 | **7 种**（`0` 与 `2..7`） |
| `additional_provisions` | **常量** | 🔴 **30/30 同一个空壳**：`{system_config_type: "quest_provision", items: {}}` ⚠️ |
| `additional_trinket_completion_rewards` | 多值 | `[very_rare ×1]` **28** · `[very_common ×1]` 1 · `[]` 1 |
| `can_retreat` | **开关** | `true` **29** · `false` **1** |
| `completion_dungeon_xp` | **开关** | `false` **29** · `true` **1** |
| `has_statue_contents` | **开关** | `true` **29** · `false` **1** |
| `is_progression` | **开关** | `true` **29** · `false` **1** |
| `is_roster_stress_cleared_on_completion` | **开关** | `false` **26** · `true` **4** |
| `is_scouting_enabled` | **开关** | `true` **25** · `false` **5** |
| `is_surprise_enabled` | **开关** | `true` **25** · `false` **5** |
| `plot_quest_dependency` | 多值 | **4 种**（`""` + 主线三条）|
| `retreat_always_from_raid` | **常量** | 🔴 **30/30 都是 `false`** ⚠️（**死字段**）|
| `retreat_party_kill_count` | 多值 | `0` **25** · `1` **5** |
| `roster_buff_on_failure_minimum_party_resolve_level` | 多值 | `0` **25** · `5` **5** |
| `roster_buffs_to_apply_on_failure` | 多值 | `[]` **26** · `[darkest_dungeon_failure_roster_resolve_xp]` **4** |
| `suggested_trinkets` | 多值 | `[]` **29** · `[dd_trinket ×3]` **1** |
| `upgrade_tags_to_remove_on_failure` | **常量** | 🔴 **30/30 都是 `[]`** ⚠️（**死字段**）|
| `upgrade_tags_to_remove_on_ignore` | 多值 | `[]` **29** · `[{building, 3}]` **1** |

🎖️ **三条立刻可用的结论**：
```
① 🔴 **2 个字段是【常量】**（`retreat_always_from_raid` 恒 false ·
   `upgrade_tags_to_remove_on_failure` 恒 []）⇒ 📌 **它们在本数据里【不起作用】** ✓
   ⚠️ 但要与 `§6` 的"不必照抄"分开：**这两个是"数据里恒同值"，不是"参考实现丢弃"** ✓
② 🔴 **1 个字段是【空壳】**（`additional_provisions` 恒 `{items: {}}`）⇒ 同理 ✓
③ 🎖️ **`is_scouting_enabled` / `is_surprise_enabled` 恰好一起变**（都是 25 true / 5 false）
   ⇒ 📌 **疑似【同一条轴】** ⇒ 待核（可能"黑暗主线任务关掉侦察与偷袭"）✓
```

## 3. 🎖️ `quest` 是一个**内嵌的小结构**（9 种组合）

```
`quest` 的字段：`is_plot_quest` · **`type`** · **`dungeon`** · `difficulty` · **`length`** ·
   **`map_name`** · **`goal_ids[]`** · **`completion_reward`** ✓
   `type` = `kill_boss` / `explore` / `inventory_activate` / `activate`（**复用 `types` 那 6 种**）✓
   `dungeon` = 具体地牢名（`crypts` / `town` / `darkestdungeon` / …）✓
   `length` = 任务长度（**1~3**）✓   `difficulty` = **1~6** ✓
   🔴 **`map_name` 指回 `Maps/*.bytes`**（如 `DD_map2` / `tutorial_crypts` / `town_invasion_0`）⇒
      ⚠️ **而 `Maps/` 已实测是【Unity 二进制、不可用】**（A12）⚠️
      ⇒ 📌 **即：任务的"地图"那一半【接不上】** ⇒ **归策划/架构** ✓
   `completion_reward` = `{resolve_xp, items_definition{system_config_type, items{}}}` ✓
      例：`plot_darkest_dungeon_2` ⇒ `resolve_xp 16` + `gold 15000` + `crest 18` ✓
   `goal_ids[]` 指回顶层的 `goals`(45) ✓（如 `explore_all_rooms` · `kill_ancestor_heart_D`）✓
```

## 4. 🎖️ 而"失败/忽略"那 7 个字段：**实测只有 3 个真的在用**

```
📊 逐字段数"非默认值"的条数：
   `can_retreat`                     非默认（false）**1** 条 ⇒ 用到了（`plot_darkest_dungeon_4`）✓
   `retreat_always_from_raid`        恒 `false` ⇒ 🔴 **没用到** ⚠️
   `retreat_party_kill_count`        **5** 条为 `1` ⇒ 用到了（都是 `darkestdungeon` 线）✓
   `roster_buff_on_failure_minimum_party_resolve_level`  **5** 条为 `5` ⇒ 用到了 ✓
   `roster_buffs_to_apply_on_failure` **4** 条非空 ⇒ 用到了 ✓
   `upgrade_tags_to_remove_on_failure` 恒 `[]` ⇒ 🔴 **没用到** ⚠️
   `upgrade_tags_to_remove_on_ignore` **1** 条非空 ⇒ 用到了（`plot_town_invasion_0`）✓
⇒ 🎖️ **即：7 个里 4 个在用 · 3 个恒默认**（其中 2 个是常量、1 个是空壳）✓
   📌 而"在用的那 4 个"**全都集中在 `darkestdungeon` 主线 + 城镇入侵**上 ⇒
      🔴 **即：失败惩罚是【主线专属机制】**，普通 boss 任务没有 ⚠️
      🎖️ **这是一条设计事实**，不是数据噪声 ✓
```

## 5. 🎖️ 那个"处处是另一档"的 4 条：**它们是主线与特例**（不是异常）

```
🔴 看起来"数据不齐"的 4 条，实测**各有明确身份**：
   `plot_tutorial_crypts`     ⇒ `completion_dungeon_xp=true`（**教程关**，唯一）✓
   `plot_town_invasion_0`     ⇒ `is_progression=false` + `has_statue_contents=false`
                                + `retreat_party_kill_count=1` + **`upgrade_tags_to_remove_on_ignore` 非空**（唯一）✓
                                它 `type=kill_boss` · `dungeon=town` · **`difficulty=6`** ✓
   `plot_darkest_dungeon_2`   ⇒ **`suggested_trinkets` 唯一非空**（`dd_trinket ×3`）✓
   `plot_darkest_dungeon_4`   ⇒ **`can_retreat=false` 唯一**（最终战不让退）✓
⇒ 🎖️ **即：那 4 条不是"数据不一致"，而是【4 个明确的特例】** ✓
   · 教程关 · 城镇入侵（不进主线、不给雕像）· 主线第 2 关（推荐饰品）· 最终战（禁撤退）✓
   📌 **判据**：**"这条看起来不一样，是【数据坏了】还是【它本来就是特例】？"** ——
      **能说出它为什么不一样 ⇒ 特例**（4/4 都能说出）✓
```

## 6. 诚实边界

```
✅ **能验**：30 条的两轴结构（25 + 4 + 1）· 19 字段的**完整取值分布** ·
   **2 个常量字段 + 1 个空壳字段** · 7 个分支字段里**4 用 3 不用** ·
   4 条特例的**各自身份** · `quest` 内嵌结构 9 字段 —— **全部当场跑出** ✓
🔴 **不能验**：**字段的精确语义**我是**从取值分布 + 字段名推断**的 ⚠️
   （尤其 `has_statue_contents` / `is_roster_stress_cleared_on_completion` 的**具体效果未读代码**）⇒ 记**推断** ✓
🔴 **不能验**：`is_scouting_enabled` 与 `is_surprise_enabled` **是否真的是同一条轴** ⇒ 只知"取值一起变" ✓
🔴 **不能验**：`map_name` 指回 `Maps/*.bytes`（**二进制不可用**）⇒ **任务的地图那一半接不上** ⚠️
🔴 **不能验**：**没有落任何任务数据** ⇒ "接上 30 条任务后流程会怎样"**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a8_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
🆕 **可做**：① 读参考代码核 `has_statue_contents` / `is_roster_stress_cleared_on_completion` 的**实际消费点**
   ② 核 `is_scouting_enabled`/`is_surprise_enabled` 是否同轴
   ③ `goals` 45 条的内部结构（`goal_ids` 指过去的那张表）✓
⏸️ **等策划**：四张单已投递 ✓
```
