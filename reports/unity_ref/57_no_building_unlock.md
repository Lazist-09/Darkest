# 🎖️🎖️ 参考项目**没有"建筑解锁"机制** —— 8 栋建筑**从一开始就全在**

> 🕒 2026-09-26 · 承接 `56_unlock_fields_dead.md §6` 的"找真实解锁机制" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 结论：**机制在代码里不存在**（四条证据，各自独立）

```
📊 **证据①：`Estate.cs:45-74` 的 `Buildings` 字典【无条件装入】** ✓
   `Buildings = new Dictionary<BuildingType, Building>();`
   `Abbey = …Data.Buildings["abbey"] as Abbey;  Buildings.Add(BuildingType.Abbey, Abbey);`
   …（**8 栋逐一 Add，中间【没有任何 if】**）✓
   ＋ `Graveyard = new Graveyard(); Buildings.Add(…)` · `Statue = new Statue(); Buildings.Add(…)`
   ⇒ 📌 **即：8 栋 + 墓地 + 雕像，一次全装** ✓
   🔴 **而后 `Abbey.InitializeBuilding(TownPurchases)` 等 8 行** ⇒
      📌 **`InitializeBuilding` 传的是【已购升级】（`TownPurchases` = `saveData.BuildingUpgrades`）** ⇒
      ⇒ 🎖️ **即：它恢复的是【升级进度】，不是【解锁状态】** ✓

📊 **证据②：`TownManager.buildingSlots` 是 `[SerializeField]`** ✓
   `[SerializeField] private List<BuildingSlot> buildingSlots;`
   ⇒ 📌 **即：建筑的显示列表是【Unity 场景里手配的】** ⇒
      而 `InitializeBuildings()` 只是 `for (i…) BuildingWindows[i].Initialize();`
      🔴 **循环里【没有条件】** ✓

📊 **证据③：`BuildingSlot` 类里【没有任何解锁状态】** ✓
   它只有 4 个序列化字段（`labelBackground` / `nameLabel` / `buildingDescription` / `currentState`）
   ＋ 3 个方法（`BuildingSelected` / `OnPointerEnter` / `OnPointerExit`）✓
   ⇒ 📌 **全是悬停/点击的 UI 表现** ⇒ 🔴 **没有 `IsUnlocked` / `SetActive` 之类** ✓

📊 **证据④：唯一的进度计数器 `QuestsComleted` 只喂【任务生成】** ✓
   全部 12 个使用点：`Campaign.cs` 读写 · `SaveCampaignData.cs` 存档 ·
   `ResultItemWindow.cs:36` **任务完成后 `++`** ·
   🔴 **`QuestGenerator.cs:62/80-92` 用它做【任务生成门槛】**（含 7 档分支）✓
   ⇒ 🎖️ **即：它影响"刷出什么任务"，【不影响"哪栋建筑可见"】** ✓
```

## 2. 🎖️ 这条与"三件套是死数据"**互相印证**

```
📊 `56_*.md` 已测：`number_of_quests_finished` / `highest_dungeon_level` /
   `on_start_town_visit_priority` **在 `DarkestDatabase.cs` 里各出现 0 次** ✓
📊 本件已测：**装建筑时没有条件** · **UI 列表是场景手配** · **进度计数器不管建筑** ✓
⇒ 🎖️ **两条独立读数指向同一结论**：
   🔴 **那三个字段【本来就没有机制在用它】** ⇒ **不是"我找不到"，是"它不存在"** ✓
   📌 **判据**：**"一个字段既没被读、又没对应的机制 —— 那它是【文件名里的残留】还是【未完成的功能】？"**
      ✅ **两者的处置相同：不能照抄** ✓
```

## 3. 🔴 而这**推翻了我方 `unlocks.json` 的一个隐含假设**

```
🔴 **我方** `unlocks.json`（2 条）：
   `unlock_tavern`         ⇒ 需 **1 趟出征** ⇒ 解锁 `building:tavern` ✓
   `unlock_abbey_and_curios` ⇒ 需 **3 趟出征** ⇒ 解锁 `building:abbey` + 2 个 curio ✓
   ＋ 起手态：**"城池只有 Stage Coach ＋ 4 种 Curio"** ✓
⇒ 🎖️ **而参考（= DD1）里【8 栋建筑起手全在】** ⇒ 🔴 **我方这套"逐趟解锁建筑"是自创** ⚠️
   📌 而 DD1 **原版 wiki** 也支持这点：Hamlet 的建筑**一开始就全开放**，
      **"解锁"针对的是【升级等级】**（`highest_dungeon_level` 那些其实是**升级门槛**）✓
   🎖️ **即：我方把"升级门槛"误当成了"建筑解锁门槛"** ⚠️
      —— 或者我方**有意**做了不同的设计（要问策划）✓
```

## 4. 🎖️ 那三个字段**真正想表达什么**？（本件的副产品）

```
🎖️ **从字段名与取值反推**（**推断，未证实**）：
   · `highest_dungeon_level` = 2 出现在 **camping_trainer** / **nomad_wagon** ✓
     ⇒ 📌 这两栋是 **DD1 里最晚开放的**（营地技能训练师 / 游牧货车）✓
   · `number_of_quests_finished` = 2/3/4 出现在 abbey/tavern/blacksmith/guild/sanitarium ✓
   · `on_start_town_visit_priority` 全 1（`stage_coach` 0）✓
   ⇒ 🎖️ **最可能的解释**：它们是**"城镇界面里建筑的摆放/出现顺序"**的**遗留设计**，
      后来改成了**场景手配**（证据②）⇒ 🔴 **于是数据留下了、代码没跟上** ✓
   📌 **这与 `plot_quests` 的 6 个字段、act-out 的 2 个、Effect 的 190 条【同族】** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`Estate.cs:45-74` 无条件装入（含 8 行 `InitializeBuilding`）** ·
   **`TownManager` 的 `[SerializeField]` 与无条件循环** ·
   **`BuildingSlot` 的完整类体（无解锁状态）** ·
   **`QuestsComleted` 的 12 个使用点（全部读完）** —— **全部当场跑出** ✓
🎖️ 并**与 `56_*.md` 互相印证**（两条独立读数同一结论）✓
🔴 **不能验**：**"DD1 原版建筑起手全开"** ⇒ 📌 **这是 wiki 常识，不是本仓读数** ⚠️
   ⇒ 记为**外部知识**，且**只作佐证**（主证据是前四条代码读数）✓
🔴 **不能验**：那三个字段的**设计意图** ⇒ §4 是**推断** ✓
🔴 **不能验**：**我方 `unlocks.json` 该不该改** ⇒ 归**策划裁**（我方可能是有意设计）✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{unlock,town}_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **解锁这条线【查完】**：参考没有建筑解锁 ⇒ **不能照抄** ⇒ 归策划裁 ✓
🆕 **可做**：① 核英雄升级消耗的「我方 vs 一手 E 盘」（`51_*.md` 记的局限）
   ② 核 `unlock_abbey_and_curios` 那 2 个 curio 与 A12 的 60 奇物的关系
   ③ 把"字段没被读 + 没机制 ⇒ 不能照抄"补进 `observe_list` D11 ✓
⏸️ **等策划**：八张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
