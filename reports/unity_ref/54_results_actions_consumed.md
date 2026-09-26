# 收口 + 更正：`results[].type` 是 **7 种**（不是 8）—— 且 **7/7 全部有代码消费**

> 🕒 2026-09-26 · 更正 `53_side_effects_crosscheck.md` 的"8 种"✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴 我先犯了一个**层级混淆**（第 15 次同族）

```
🔴 `53_*.md` 我报：**"建筑 `results[].type` 有 8 种，含 `gold` ×4"** ⚠️
✅ **实测更正**：**动作类型是 7 种** —— `gold` **不是动作类型** ✓
   📌 **那 4 处 `gold` 是 `change_currency` 的 `data[].type`（= 货币名）** ⚠️
   🔴 **根因**：我的扫描条件只要求**路径里有 `.results[`** ⇒
      而 **`change_currency` 的 `data[]` 【也在 `results` 内部】** ⇒
      📌 **`data[].type` 被当成了 `results[].type`** ✓
   🎖️ **判据（第 16 条）**：**"这个 `type` 是【动作】还是【数据项】？看它【直接挂在谁下面】"** ✓
      · **直接挂在 `results[N]` 下** ⇒ **动作类型** ✓
      · **挂在 `results[N].data[M]` 下** ⇒ **数据项**（如货币名）✓
   📌 **与第 15 条（动作 vs 标记）同族，但更细**：这次是**同一棵树里【不同深度】的同名字段** ✓
```

## 2. ✅ 修正后的正确读数：**7 种 · 40 处**（全部只在 `abbey`/`tavern`）

| 动作 | 处数 | 建筑 |
|---|---|---|
| **`add_quirk`** | **17** | abbey · tavern |
| **`go_missing`** | **6** | abbey · tavern |
| **`activity_lock`** | **6** | abbey · tavern |
| **`apply_buff`** | **5** | abbey · tavern |
| **`change_currency`** | **3** | abbey · tavern |
| **`remove_trinket`** | **2** | **仅 tavern** |
| **`add_trinket`** | **1** | **仅 tavern** |

## 3. 🎖️🎖️ 而**7/7 全部有代码消费**（读到了执行器）

```
🎖️ 全部消费在**同一个 `switch`** 里（`DarkestDatabase.cs:963-1034`）：
   ```csharp
   foreach (var jsonTownEffect in jsonActivity.side_effects.results) {
       switch (jsonTownEffect.type) {
           case "activity_lock":   ⇒ new ActivityLockTownEffect()      // L965
           case "go_missing":      ⇒ new MissingTownEffect()           // L970（读 data[] 的 chance/duration）
           case "add_quirk":       ⇒ new AddQuirkTownEffect()          // L982（读 data[] 的 quirk_library_name）
           case "change_currency": ⇒ new CurrencyTownEffect()          // L994（读 data[] 的 type/amount）
           case "apply_buff":      ⇒ new AddBuffTownEffect()           // L1007（读 buff_library_ids）
           case "add_trinket":     ⇒ new AddTrinketTownEffect()        // L1021
           case "remove_trinket":  ⇒ new RemoveTrinketTownEffect()     // L1026
           **default: Debug.LogError("Missing activity town effect type: " + …)**   // L1031-1032
       }
   }
   ```
⇒ ✅ **7/7 有 case · 且 `default` 会【响亮报错】** ⇒ 🎖️ **不会静默** ✓
   📌 **即：这一层的质量与 act-out 那层（27 可抄 / 2 死）不同** ⇒ **7/7 都是活的** ✓
   🎖️ **判据复用**：**"有 `default` 吗？"** ⇒ **有报错的 `default` ⇒ 未匹配是【缺陷】** ✓
      （与 `nothing` 那次相反：那次**没有 `default`** ⇒ 缺失是**语义**）✓
```

## 4. 🔴 顺带发现一条**机制**：`change_currency` 能**扣钱**

```
🔴 原文（`abbey.prayer.side_effects.results[4].data[0]`）：
   `{"chance": 1, "type": "gold", "amount": **-1000**}` ⚠️
🔴 `tavern.bar`：`{"chance": 1, "type": "gold", "amount": **-500**}` ⚠️
🎖️ `tavern.gambling`：`[{gold, +500}, {gold, **-500**}]` ⇒ **赌桌：赢了 +500、输了 −500** ✓
⇒ 🎖️ **即：城镇活动的副作用【可以负向】** —— 修道院祈祷可能**罚 1000 金** ✓
   📌 **而这是"扣玩家钱"的机制** ⇒ ⚠️ 采用时要注意（与 `remove_trinket` 同族）✓
```

## 5. 🎖️ 对采用的意义（本件最有用的两条）

```
✅ **① 这 7 种动作是【可照抄】的**（7/7 有消费 + 有报错 `default`）✓
   ⇒ 📌 建筑"效果侧"的第一层（活动 → `results`）**质量高** ✓
🔴 **② 但它依赖**：
   · `apply_buff`（5 处）⇒ 🔴 **指回 buff 池**（A1 已落）✓
   · `add_quirk`（17 处）⇒ 🔴 **指回 `quirk_library_names`**（A7 已抽 163 怪癖）✓
   · `go_missing` 的 `duration` ⇒ 与 `activity_lock` 都是**城镇状态机** ⇒ ⚠️ **我方没有** ✓
⇒ 🎖️ **即：依赖链是 `建筑活动 → results 动作 → {buff池 / 怪癖库 / 城镇状态机}`** ✓
   📌 **前两个我方已有，第三个（城镇状态机）没有** ⇒ ✅ **可分批做** ✓
```

## 6. 诚实边界

```
✅ **能验**：**7 种动作 · 40 处（逐种计数 + 建筑归属）** · **7/7 的 `case` 行号** ·
   **`default` 会 `Debug.LogError` 的原文** · **`change_currency` 负数的 4 处原文** ·
   **执行器读到 `data[]` 的字段名** —— **全部当场跑出** ✓
🎖️ 并**更正了我自己"8 种"的层级混淆**（`data[].type` 当成 `results[].type`）✓
🔴 **不能验**：那 7 种动作的**运行时行为未实跑**（只读了构造代码）⇒ 记**未测** ✓
🔴 **不能验**：`TownActivity` / `SideEffects` 的**实际结算时机**（什么时候掷 chance）⇒ 记**未核** ✓
🔴 **不能验**：**没有落任何数据** ⇒ 新建这层后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/res_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **建筑效果侧的第一层【查清】**：7 种动作 · 7/7 有消费 · 有报错 `default` ✓
🔴 **仍缺的一层**：**城镇状态机**（`go_missing` 的 duration · `activity_lock`）⇒ 我方没有
🆕 **可做**：① 核 `TownActivity` 的**结算时机**（何时掷 `side_effects.chance`）
   ② 核 `highest_dungeon_level` 三件套 vs A8 的 `town_progression_*`（`52_*.md` 待核）
   ③ 核英雄升级消耗的「我方 vs 一手 E 盘」（`51_*.md` 局限）✓
⏸️ **等策划**：八张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
