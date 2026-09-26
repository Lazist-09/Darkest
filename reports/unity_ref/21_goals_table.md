# `goals` 45 条：结构、交叉校验、以及**两类 goal**

> 🕒 2026-09-26 · 承接 `15_quests_loot_narration_from_ref.md` §7 的"`goals` 内部结构未展开" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 结构：45 条 / 9 种 `type` / 6 个字段

```
字段全集（6 个）：`id` · **`type`** · **`data`** · `show_as_quest` · `ignore_fog_of_war` ·
   **`starting_items`** ✓
`type` 分布（**9 种**）：
   **`kill_monster` 27** · **`activate` 7** · **`gather` 4** · `explore_room` 2 ·
   `tutorial_room` 1 · `battle_room` 1 · `battle` 1 · `trait_applied` 1 · `deaths_door` 1 ✓
```

🎖️ **一眼看出两类**（不是按 `type` 分的，是按**用途**分的）：

| 类 | 条数 | `id` 特征 | 被任务引用？ |
|---|---|---|---|
| **A. 剧情/boss 目标** | **30** | `kill_*`(27) · `explore_all_rooms` · `battle_all_rooms` · `inventory_activate_beacons` | ✅ **被引用** |
| **B. 城镇进展目标** | **4** | **`town_progression_*`** | 🔴 **无任务引用** |
| **C. 备用/未用** | **11** | `gather_*`(4) · `inventory_activate_*`(5) · `activate_iron_maiden` · `tutorial_final_room` | 🔴 **无任务引用** |

## 2. 🎖️ 交叉校验：**三张表全部命中**

```
① **`kill_monster` ×27** ⇒ 引用怪物 **36** 个 ⇒ 🔴 **未在 A4 表里的 0** ✓
   （`A4` 的 230 条怪物表**再次被验证** —— 这是第 2 次独立交叉）
② **`item` / `starting_items`** ⇒ 引用 **9** 个 ⇒ 🔴 **未在物品表里的 0** ✓
③ **`goal_ids`（30 条任务引用的）** ⇒ **30 个** ⇒ 🔴 **未在 45 条 goals 里的 0** ✓
⇒ 🎖️ **即：`goals` 这张表【引用链完全闭合】** ✓
```

### 🔴 而 ② 里有一批"未命中"，实测是**两个命名空间**（纪律 AU）

```
`curio_name` 引用 **11** 个 ⇒ 🔴 **10 个不在奇物表里**：
   `animalistic_shrine` · `beacon` · `chirurgeons_satchel` · `corrupted_altar` ·
   `foodstuff_crate` · `infected_corpse` · `protective_ward` · `reliquary` ·
   `shipment_crates` · `teleporter` ✓
   ✅ **唯一命中的是 `iron_maiden`**（而它**确实**在 `Curios.csv` 的 60 条里）✓
🔴 **那 10 个在全参考数据里【只有 `JsonQuests.json` + 本地化】提到**
   （我逐个搜过 `Assets/Resources/Data/**`）⇒ 📌 **它们【没有数据定义】** ⚠️
🎖️ **但这不是"缺失"，是【任务专属交互物】**：
   ① 名字都是任务语义（`reliquary` 圣物匣 · `beacon` 烽火 · `protective_ward` 防护结界）✓
   ② **6 个带 `starting_items`**（**任务直接发你对应道具**）：
      `inventory_activate_corrupted_altar` ⇒ 发 `holy_water ×3` ✓
      `inventory_activate_infected_corpse` ⇒ 发 `antivenom ×3` ✓
      `inventory_activate_animalistic_shrine` ⇒ 发 `pickaxe ×3` ✓
      `inventory_activate_wards` ⇒ 发 `eldritch_lantern ×3` ✓
      `inventory_activate_beacons` ⇒ 发 `beacon_light ×3` ✓
   ③ 而 `iron_maiden` **在**通用表里 ⇒ **两类都合法** ✓
⇒ ✅ **结论：`Curios.csv` 的 60 条（通用奇物）与 goal 的 `curio_name`（任务专属交互物）
   是【两张不同的表】** ⇒ **与 A12 的 `props` 段 57/57 命中【不矛盾】** ✓
```

## 3. 🔴 `ignore_fog_of_war` **45/45 全是 `False`** ⇒ 恒默认

```
`show_as_quest`：**`True` 28 · `False` 17**（有分布）✓
`ignore_fog_of_war`：🔴 **`False` 45/45** ⇒ **恒默认值** ⚠️
⇒ 📌 与 `19_*.md` 里那 3 个"恒默认"字段**同族**（数据侧就没人用）✓
   🎖️ **而 `§10.2 ④` 说"`show_as_quest`（28 真/17 假）与 `ignore_fog_of_war`
      在 DTO 里但从不拷进 `QuestGoal`"** ⇒ 🔴 **本件实测三方对上**：
      · `show_as_quest` **28/17** —— 与 `§10.2 ④` 的"28 真/17 假"**逐数相符** ✓
      · `ignore_fog_of_war` **恒 False** ⇒ 它连"有分布"都谈不上 ⚠️
      ⇒ ✅ **两者都"不拷进 `QuestGoal`"** ⇒ **加载会丢** ✓
```

## 4. 🎖️ 而 `data` 的形状**随 `type` 变**（又一处"口径随类型变"）

| `type` | `data` 的键 | 例 |
|---|---|---|
| `kill_monster` | `monster_class_ids[]` · `amount` | `{monster_class_ids: [necromancer_A], amount: 1}` |
| `explore_room` / `battle_room` | **`amount` · `percentage`** | `{amount: 0, percentage: 0.9}`（**90% 房间**）|
| `battle` / `deaths_door` | **`amount`**（无 percentage）| `{amount: 2}` |
| `gather` | **`curio_name` · `item{}`** | `{curio_name: reliquary, item: {type: quest_item, id: holy_relic, amount: 3}}` |
| `activate` | **`curio_name` · `amount`** | `{curio_name: beacon, amount: 3}` |
| `trait_applied` | **`is_affliction` · `is_virtue` · `amount`** | `{is_affliction: true, is_virtue: false, amount: 1}` |
| `tutorial_room` | **`room_id`** | `{room_id: room2_1}` |

🎖️ **两处值得记**：
```
① **`gather` 与 `activate` 的 `curio_name` 语义不同**：
   · `gather` = **"从哪个奇物里收集到 `item`"**（`data.item` 是**产出**）✓
   · `activate` = **"去激活哪个 `curio_name`"**（`data.amount` 是**要几个**）✓
   ⇒ 📌 **同一个键名，两种语义**（纪律 AU）✓
② 🔴 **`trait_applied` 的 `is_affliction`/`is_virtue`** —— 而 `§10.2 ④` 说
   **"`is_affliction`/`is_virtue` 与 goal 的 `amount` 从不读"** ⇒ ⚠️ **同族：数据有、没人读** ✓
③ 🎖️ **`explore_room` 的 `percentage: 0.9`**（90%）与 `battle_room` 的 `1`（100%）
   ⇒ **这是"完成度阈值"** ⇒ 📌 与 `§10.2 ⑤` 提的 `data.percentage` **确实存在**且**被消费** ✓
      （分队自己在 `04b` 里纠过这条 —— 本件**独立复核为真**）✓
```

## 5. 诚实边界

```
✅ **能验**：45 条 / 9 种 type / 6 字段 · **两类 goal 的划分**（30 被引用 + 15 未被引用）·
   **三张表交叉校验全命中**（怪物 36/36 · 物品 9/9 · goal_ids 30/30）·
   **10 个 `curio_name` 的在全数据里的出现位置**（只有 JsonQuests + 本地化）·
   `ignore_fog_of_war` 45/45 恒 False · `show_as_quest` 28/17 —— **全部当场跑出** ✓
🎖️ 并**独立复核了 `§10.2 ④⑤` 的两条**（28/17 逐数相符 · `data.percentage` 确实存在）✓
🔴 **不能验**：**"那 10 个交互物没有数据定义"是【我搜了 `Assets/Resources/Data/**`】的结论** ⚠️
   ⇒ 若定义在代码里（而非数据文件）我扫不到 ⇒ 📌 记为**"数据文件里没有"**，不是"绝对不存在" ✓
🔴 **不能验**：那 **15 条未被引用的 goal**（含 4 条 `town_progression_*`）**是给谁用的** ✅
   ⇒ 📌 `town_progression_*` 有 4 条、且顶层有 `town_progression_goal_ids`(**4**) ⇒
      🎖️ **数目相等** ⇒ **疑似那 4 条就是它引用的** ⇒ ⚠️ **但本件未核那个数组** ⇒ 记**待核** ✓
🔴 **不能验**：**没有落任何 goal 数据** ⇒ "接上后任务判定会怎样"**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a8_goals*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
🆕 **可做**：① 核 `town_progression_goal_ids`(4) 是否就是那 4 条 `town_progression_*` goal ✓
   ② 核 `types`(6) 与 goal 的 9 种 type 的**关系**（前者是任务的、后者是目标的）✓
   ③ 那 11 条备用 goal（`gather_*` 4 + `inventory_activate_*` 5 + 2）**是给谁用的** ✓
⏸️ **等策划**：四张单已投递 ✓
```
