# 交叉核对：`side_effects.results[].type` 与 Effect **是两套词汇**（交集 0）

> 🕒 2026-09-26 · 工具 `tools/dsh/crosscheck_side_effects.py`（新，可重跑）·
> 产物 `reports/unity_ref/side_effects_crosscheck.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴 我先比错了字段（第 14 次同族）

```
🔴 **我第一版**拿 `Effect.type` 去比 ⇒ 得 `Effect.type` 只有 **2 种**：
   **`positive` ×370 · `negative` ×369** ⚠️
   ⇒ 🔴 **交集 0**，看起来"两套完全不同的词汇" ✓
✅ **但读几条 effect 原文才发现**：
   `kill_self`: `{target, chance, **kill: 1**, on_hit, on_miss, apply_once, queue}` ✓
   📌 **即：Effect 的「动作」是【字段名本身】**（`kill`/`stress`/`heal`/`stun`/`push`…），
      **不是 `type`** ⚠️ —— 而 `type`（`positive`/`negative`）只是**倾向标记** ✓
🎖️ **判据（第 15 条）**：**"这个 `type` 字段是【动作】还是【标记】？"** ——
   ✅ **同名 `type` 在不同表里可以是完全不同的东西** ✓
   📌 **与第 6/10 条同族**（"名字有几种来源"），这次是**"同名不同义"** ✓
```

## 2. 📊 用**动作字段**重比：**交集仍然是 0**

| | 词汇 |
|---|---|
| **建筑** `side_effects.results[].type`（**8 种 · 44 处**）| `add_quirk` 17 · `go_missing` 6 · `activity_lock` 6 · `apply_buff` 5 · `gold` 4 · `change_currency` 3 · `remove_trinket` 2 · `add_trinket` 1 |
| **Effect** 的动作字段（高频）| `combat_stat_buff` 353 · `damage_low_multiply` 139 · `buff_ids` 90 · `healstress` 52 · `stress` 51 · `heal` 47 · `summon_monsters` 47 · `stun` 46 · `push` 37 · `tag` 28 · `pull` 20 · `torch_increase` 19 · `cure` 15 · `disease` 12 · `kill` 4 |
| 🔴 **交集** | **`[]`（空）** ✓ |

```
🎖️ **即：即使比对了正确的字段，两侧【仍然没有共同词汇】** ✓
   📌 **两者在回答不同的问题**：
      · **Effect 的动作** = **"对角色做什么"**（打/治/推/拉/上 buff/召唤）✓
      · **建筑的 `results[].type`** = **"城镇里发生什么"**（加怪癖/失踪/锁活动/改钱/给饰品）✓
   ⇒ ✅ **即：不是"同一套的两种写法"，是【两个不同的动作域】** ✓
      🎖️ **判据**：**"这两个词汇表，是【同一个域】还是【两个域】？"** ——
         **看它们作用在【谁】身上**（战斗中的角色 vs 城镇里的英雄）✓
```

## 3. 🎖️ 三条副产物（都有用）

```
① 🎖️ **建筑那 8 种只有 `abbey` 与 `tavern` 用**（**44 处全部**来自这两个建筑）
   ⇒ 📌 **即：`side_effects` 是"修道院/酒馆"两个建筑的专属机制** ✓
      🎖️ 这与 `52_*.md` 的键名分类**一致**（`side_effects` 出现 2/8 个建筑）✓
② 🎖️ **`gold` ×4 与 `change_currency` ×3 并存** ⇒ 📌 **一个是"给钱"、一个是"换钱"** ✓
   ⇒ ⚠️ 与 `23_*.md` 的"金币分通道"**相关** ⇒ 记**待核** ✓
③ 🔴 **`remove_trinket` ×2 · `add_trinket` ×1** ⇒ 📌 **这两个是"赌桌上的输赢"**
   （`tavern` 的 `gambling`）⇒ 🎖️ **即：酒馆的"赌博"会**从玩家身上拿走饰品** ⚠️
   ⇒ 📌 **采用时要特别注意**（**惩罚性机制**）✓
```

## 4. 🎖️ 所以建筑那一层的**依赖链**又长了一节

```
🔴 **实测的完整依赖**：
   `buildings.json`（我方只到"消耗"）
     ⇒ **建筑的"活动/服务"层**（`meditation`/`bar`/`gambling`…）← 🔴 **我方没有**
        ⇒ **`side_effects.results`**（8 种动作，**自己的词汇**）← 🔴 **我方没有**
           ⇒ 其中 `apply_buff`（5 处）⇒ 🔴 **又要指回 buff 池** ⚠️
⇒ 🎖️ **即：建筑的"效果侧"不是一层，是【两到三层】** ✓
   📌 与 A6 那条（`act-out → Effect → buff`）**结构相同** ⇒
      ✅ **判据复用**：**"这段数据引用了哪张表？那张表我方有吗？"**（第 8 条）✓
      ⇒ 🔴 **已第四次用到这条判据** ✓
```

## 5. 诚实边界

```
✅ **能验**：**建筑 8 种 `results[].type` / 44 处** · **Effect 的 `type` 只有 2 种（倾向标记）** ·
   **Effect 的动作用【字段名】表达** · **两套动作词汇的交集为 0** ·
   **8 种全部只出现在 `abbey`/`tavern`** · **`remove_trinket`/`add_trinket` 是赌桌** ——
   **全部当场跑出** ✓
🎖️ 并**订正了自己"比错字段"**（拿 `type` 而非动作字段比）✓
🔴 **不能验**：**建筑的 `results[].type` 到底由哪段代码消费** ⇒ 记**未核** ✓
   📌 若发现它**其实复用**某个通用 effect 执行器 ⇒ 上面的"两个域"结论要改 ⇒ 记**前提** ✓
🔴 **不能验**：`gold` 与 `change_currency` 的**精确语义** ⇒ 记**待核** ✓
🔴 **不能验**：**没有落任何数据** ⇒ 新建这一层后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/x_type.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **交叉核对【完成】**：两套动作词汇 · 交集 0 · 且 `side_effects` 只属 2 个建筑 ✓
🆕 **新暴露**：🔴 **建筑"效果侧"是【两到三层】**（活动层 → `results` 动作 → buff）
🆕 **可做**：① 核 `results[].type` 的**代码消费点**（关掉上面那个前提）
   ② 核 `highest_dungeon_level` 三件套 vs A8 的 `town_progression_*`（`52_*.md` 的待核）
   ③ 核英雄升级消耗的「我方 vs 一手 E 盘」（`51_*.md` 的局限）✓
⏸️ **等策划**：八张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
