# `plot_quests` 字段的**消费点实测** —— 13 个字段里只有 **4 个**真的生效

> 🕒 2026-09-26 · 承接 `19_plot_quest_fields.md` §7 的"未读代码核消费点" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 📌 本件读的是**参考项目的 C# 代码**（485 个 `.cs`），不是数据 ✓

---

## 1. 🎖️ 判据（先定口径 —— 纪律 AU）

```
🔴 **"消费点"的正确定义**：**真正影响行为的判定** ✓
   ✅ **算消费**：`if (…字段…)` 这类**分支** —— 它改变玩家能看到/能做的事
   ❌ **不算消费**（这些都是"搬运"）：
      · `DarkestJsonReader.cs` 的**声明**（`public bool has_statue_contents;`）
      · `DarkestDatabase.cs` 的**拷贝**（`plotQuest.X = jsonData…X;`）
      · `Quest.cs` 的**序列化/默认值**（`bw.Write(X)` / `br.ReadBoolean()` / `X = false`）
      · `PlotQuest.cs` 的**克隆**（`newQuest.X = X;`）
      · `RaidSceneMultiplayerManager.cs` / `SaveCampaignData.cs` 的**占位默认值**
⚠️ **我第一版把 UI 面板排除了 ⇒ 差点把 `CanRetreat` 误判成"无消费"** ⚠️
   ⇒ ✅ **修正：UI 里的 `if` 算消费**（它确实改变玩家能不能按那个按钮）✓
   📌 **这是本件唯一一次口径修正，记下来**（纪律 AU：口径要先定，且要定对）
```

## 2. 📊 实测结果：**13 个字段 ⇒ 有消费点 4 · 无消费点 9**

### ✅ 有消费点的 4 个（逐处给出代码）

| 字段 | 消费点数 | 在哪 |
|---|---|---|
| **`IsScoutingEnabled`** | **2** | `RaidSceneManager.cs:808` `if (…IsScoutingEnabled && room.Knowledge == Hidden)` · `:2220` `if (…Room && …IsScoutingEnabled)` |
| **`CanRetreat`** | **2** | `RaidQuestPanel.cs:30` `if (plotQuest.CanRetreat == false)` ⇒ **隐藏撤退按钮** · `:84` `if (…CanRetreat)` ⇒ **显示撤退按钮** |
| **`CompletionDungeonXp`** | **1** | `ResultItemWindow.cs:38` ⇒ 通关时给**地牢经验**（`DungeonXpTable[Length] + (Difficulty+1)/2`）|
| **`PlotDependency`** | **1** | `DungeonProgress.cs:33` ⇒ **前置任务完成后才解锁**（`CompletedPlot.Contains(plotDependency)`）|

🎖️ **即：真正生效的只有 4 件事** —— **侦察开关 · 能否撤退 · 通关给地牢经验 · 任务前置链** ✓

### 🔴 无消费点的 9 个

```
`HasStatueContents` · `IsStressClearedOnCompletion` · `IsSurpriseEnabled` ·
`RetreatKillCount` · `AlwaysRetreatFromRaid` · `RosterBuffsOnFailure` ·
`UpgradeTagsToRemoveOnIgnore` · `SuggestedTrinkets` · `UpgradeTagsToRemoveOnFailure`
⇒ 🔴 **每个都只有【声明 + 拷贝】**（我在 485 个 `.cs` 里逐处看过，没有分支读它们）⚠️
```

## 3. 🎖️ 而"无消费点"有**两种成因**，必须分开（纪律 BK）

```
🎖️ **我上一件量过数据分布**（`19_*.md §2`）⇒ 本件一交叉，两种成因立刻分清：

① **数据侧就没人用**（字段恒为默认值）—— **3 个**：
   `retreat_always_from_raid` **恒 `false`**（30/30）
   `upgrade_tags_to_remove_on_failure` **恒 `[]`**（30/30）
   `additional_provisions` **恒空壳**（30/30 `{items:{}}`）
   ⇒ 📌 **数据与代码【两边都空】** ⇒ 无害的死字段 ✓

② 🔴 **数据有非默认值，但代码里没人读** —— **6 个** ⚠️：
   `has_statue_contents`          **29 true / 1 false**  ← 🔴 **有 29 条在说"有雕像内容"**
   `is_surprise_enabled`          **25 true / 5 false**  ← 🔴 **有 5 条特意关掉了**
   `is_roster_stress_cleared_on_completion` **4 true**   ← 🔴 **有 4 条特意打开**
   `retreat_party_kill_count`     **5 条为 1**           ← 🔴 **有 5 条特意设了 1**
   `roster_buffs_to_apply_on_failure` **4 条非空**       ← 🔴 **点名了具体 buff**
   `upgrade_tags_to_remove_on_ignore` **1 条非空**       ← 🔴 **点名了 `building ×3`**
   `suggested_trinkets`           **1 条非空**           ← 🔴 **点名了 `dd_trinket ×3`**
   ⇒ 🔴 **这是「数据有、没人读」** —— **与红线 21 同族** ⚠️
      🎖️ **证据强度**：这些**不是**"默认值恰好如此"——
         `has_statue_contents` 有 29 条是 `true`（默认 `false` 才对）⇒ **有人特意填了** ✓
         而 `retreat_party_kill_count` 的 5 条 `1` 与 `upgrade_tags_to_remove_on_ignore`
         的那 1 条，**都恰好落在 `darkestdungeon` 主线 + 城镇入侵上** ⇒
         📌 **是"设计者想做的机制，代码没接"** ✓
```

## 4. 🎖️🎖️ 本件最有价值的一条：**"失败惩罚"是一套【没接线的设计】**

```
把两条读数交叉：
   · 数据侧：**7 个"失败/忽略"字段里 4 个有非默认值**，且**全集中在主线 + 城镇入侵** ✓
   · 代码侧：🔴 **那 4 个【全部无消费点】** ⚠️
⇒ 🎖️ **即：参考项目【设计了失败惩罚，但没有实现】** ✓
   · `roster_buffs_to_apply_on_failure` = `["darkest_dungeon_failure_roster_resolve_xp"]`
     ⇒ **数据里点名了"失败后给全队加 resolve XP 的 buff"** ⇒ 🔴 **没人读它** ⚠️
   · `retreat_party_kill_count = 1`（5 条）⇒ 🔴 **没人读** ⚠️
   · `upgrade_tags_to_remove_on_ignore = [{building, 3}]`（1 条）⇒ 🔴 **没人读** ⚠️
⇒ 📌 **对采用的意义（重要）**：
   ✅ **参考侧【答不出"失败惩罚该怎么做"】** —— 它只有数据、没有实现 ✓
   ⇒ ⚠️ **所以这一块【不能"照抄参考"】** ⇒ **必须我们自己设计** ⇒ **归策划** ✓
   🎖️ **而这也是用户指令"按其逻辑对齐逻辑侧实现"的一个边界**：
      **参考项目自己没有这段逻辑** ⇒ **对齐无从谈起** ✓
```

## 5. 诚实边界

```
✅ **能验**：485 个 `.cs` 逐处看过 · **13 字段的消费点逐一列出（含文件:行号）** ·
   4 有 / 9 无 · **两种成因分开**（3 恒默认 + 6 有值没人读）·
   **"失败惩罚是没接线的设计"** —— **全部当场跑出，可复算** ✓
🎖️ 并**如实记下我第一版的口径错**（把 UI 判定排除了 ⇒ 差点误判 `CanRetreat`）✓
🔴 **不能验**：**"无消费点"的结论是【静态扫描】** ——
   ⚠️ 若某处用**反射/字符串**读这些字段，我扫不到 ⇒ 📌 记为**"静态未检出"**，不是"绝对没有" ✓
   （参考项目用 `JsonConvert` 反序列化 ⇒ **数据进得来**；但**读字段**的那侧没找到）✓
🔴 **不能验**：`has_statue_contents` / `is_surprise_enabled` 的**设计意图未查**（只知没实现）✓
🔴 **不能验**：**没有落任何任务数据** ⇒ "接上 30 条任务后流程会怎样"**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/questcode*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
🆕 **可做**：① `goals` 45 条的内部结构（`goal_ids` 指过去的那张表）
   ② 核 `is_scouting_enabled`/`is_surprise_enabled` 是否同轴 —— 🔴
      **本件已部分回答**：`IsScoutingEnabled` **有消费**（2 处），
      `IsSurpriseEnabled` **无消费** ⇒ 📌 **两者【不是同一条轴】**（一个有实现一个没有）✓
   ③ 顺带报策划："失败惩罚"这块要**我们自己设计** ✓
⏸️ **等策划**：四张单已投递 ✓
```
