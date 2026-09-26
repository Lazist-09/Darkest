# 回合开始 act-out 的**执行体细读**：11 个 case + 🔴 **3 个枚举值【无 case】**

> 🕒 2026-09-26 · 承接 `31_actout_turn_consumption.md §5` 的"只读了首几行" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴🔴 本件最重要的发现：**3 个枚举值在 `switch` 里【没有 case】，且 `switch` 没有 `default`**

```
🎖️ 实测 `RaidSceneManager.cs` 的 switch 体（L2912–L3140）：
   **只有 11 个 `case`**：`AttackSelf` · `BarkStress` · `BuffAlly` · `BuffParty` ·
      `ChangePosition` · `HealSelf` · `IgnoreCommand` · `MarkSelf` · `RandomCommand` ·
      `StressHealParty` · `StressHealSelf` ✓
   🔴 **没有 `default` 分支** ⇒ ⚠️ **未匹配的枚举值会【静默穿过整个 switch】** ✓
🔴 **枚举里有、switch 里没有 case 的 3 个**：
   **`Nothing`** · **`RetreatFromCombat`** · **`AttackFriendly`** ⚠️
```

## 2. 🎖️ 而这 3 个的**数据侧读数完全不同**（这才是关键）

```
📊 数据（A6 抽的 12 条 trait）：
   · **`nothing`**：12 条都有 · 🔴 **chance>0 的 12/12**
     （7 折磨 `4~10` · 5 美德 `3`）⇒ 🎖️ **这正是"什么都不做"的权重** ✓
   · 🔴 **`retreat_from_combat`**：12 条都有 · **chance>0 的 0 条**（**全是 0**）✓
   · 🔴 **`attack_friendly`**：12 条都有 · **chance>0 的 0 条**（**全是 0**）✓
⇒ 🎖️ **即：三者的性质不同**：
   · **`nothing`** ⇒ **"什么都不做"** ⇒ 🔴 **在 switch 里没有 case 是【正确的】** ✓
     因为"穿过 switch 什么都不做"**正好就是 `nothing` 的语义** ✓
     🎖️ **所以它不是缺陷，是【靠"无 case + 无 default"实现的语义】** ✓
   · **`retreat_from_combat` / `attack_friendly`** ⇒ **数据里 chance 全 0** ⇒
     ⚠️ **它们是"定义了但从未启用"的枚举值** ✓
     📌 即：**代码没有 case、数据权重全 0** ⇒ 🔴 **双向未启用** ✓
```

## 3. 🎖️ 由此**订正我上一轮的一处说法**（第 8 次同族）

```
🔴 **`31_*.md` 我写**：「14/14 全部有消费，且是真分支」 ⚠️
✅ **本件实测**：**`StartTurnActType.` 出现次数 ≥1 ≠ 有 case** ——
   那 3 个之所以"出现 ≥1 次"，是因为：
      · `CharacterHelper.cs` 里有 **string↔enum 映射**（`case StartTurnActType.Nothing:` 在**映射函数**里）
      · `Trait.cs` 里有**枚举声明** ✓
   ⇒ 🔴 **它们【没有行为 case】** ⚠️
🎖️ **所以正确的数是**：**11 个有 case（真分支）· 3 个没有** ✓
   📌 **判据（本件修正版）**：**"这个符号出现 ≥1 次" 要问【出现在哪一类位置】？**
      —— **映射函数里的 `case` 长得跟行为 `case` 一样** ⚠️
      ⇒ ✅ **必须【看它所在的 switch 是干什么的】** ✓
   🎖️ **而这与 `30_*.md` 我修的那次【同族】**：
      上次是"计数不体现分支"，这次是"**计数不区分【映射 switch】与【行为 switch】**" ✓
      ⇒ 📌 **共同形状**：**我用的度量没有区分【语义上不同的同形结构】** ✓
```

## 4. 🎖️ 11 个真分支的**执行体机制**（逐 case 提取）

| `case` | 行数 | 关键机制 |
|---|---|---|
| **`AttackSelf`** | **49** | 用 `NumberParameter`（**按百分比扣血**）· **分死门/非死门两路** · 弹提示 · 触发效果事件 |
| **`HealSelf`** | 16 | 用 `NumberParameter` · **按 `HealthRatio` 守卫**（满血不做）· 弹提示 |
| **`BarkStress`** | 16 | 用 `StringParameter`（取 `Data.Effects[…]`）· **单人跳过** · **随机选友方** |
| **`BuffAlly`** | 16 | 用 `StringParameter` · 单人跳过 · 随机选友方 |
| **`BuffParty`** | 14 | 用 `StringParameter` · 单人跳过 |
| **`StressHealParty`** | 13 | 用 `StringParameter` · 触发效果事件 · **读站位** |
| **`StressHealSelf`** | **90** | 用 `StringParameter` · 触发效果事件 · **读站位**（🔴 **最长的一个**）|
| **`ChangePosition`** | 39 | 单人跳过 · 🔴 **硬编码怪癖特判**（`masochistic` 在 1 号位不换）· 随机选 · 读站位 |
| **`RandomCommand`** | 36 | 弹提示 · 有动画时序（**36 行**）|
| **`MarkSelf`** | 10 | 用 `StringParameter` · 触发效果事件 |
| **`IgnoreCommand`** | **7** | **只有弹提示 + 时序**（`PopupMessageType.Pass`）⇒ **最短** ✓ |

🎖️ **规律（可复用）**：
```
① **`StringParameter` 的用法**：`Data.Effects[actOut.StringParameter]` ⇒
   📌 **即：act-out 的"效果"是【引用一个 Effect 对象】**（与 buff 原语同族）✓
② **`NumberParameter` 只被 2 个用**（`AttackSelf` · `HealSelf`）⇒ 都是**百分比数值** ✓
③ **"单人跳过"出现在 5 个里**（`BarkStress`/`BuffAlly`/`BuffParty`/`ChangePosition`/…）⇒
   📌 **它是一条【通用前置守卫】** ✓
④ **`AttackSelf` 有"死门/非死门"两路** ⇒
   🔴 **非死门 ⇒ `TakeDamagePercent(NumberParameter)`；死门 ⇒ 直接 `PrepareDeath`** ⚠️
   ⇒ 🎖️ **即：自己打自己时，若已在死门 ⇒ 【必死】** ✓
```

## 5. 诚实边界

```
✅ **能验**：switch 体的 **11 个 case 逐个列出** · **无 `default`（实测）** ·
   **3 个缺失者的数据侧 chance 分布**（`nothing` 12/12 非零 · 另两个 **全 0**）·
   11 个 case 的**机制矩阵**（逐 case 逐机制） —— **全部当场跑出** ✓
🎖️ 并**订正了 `31_*.md` 的"14/14"**（正确是 **11 有 case / 3 无**）✓
🔴 **不能验**：`StressHealSelf` **90 行为何这么长**（未读全文）⇒ 记**未读** ✓
🔴 **不能验**：`RaidSceneMultiplayerManager.cs` 的 11 处**仍未看** ⇒ 记**未看** ✓
🔴 **不能验**：**每个 case 的完整执行体未逐行读完** ⇒ 本件给的是**机制矩阵**，不是全文 ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/turn_*.py` **不入库**（scratch）✓
```

## 6. 🎖️ 对采用清单的**修正**

```
📊 **29 种 act-out 的最终分档**（修正 `31_*.md` 的 27+2）：

| 档 | 种数 | 内容 |
|---|---|---|
| **A. 可照抄** | **24** | 回合开始 **11**（有 case）+ 反应 **13**（有消费）|
| **B. 有数据、代码没读 → 要自己实现** | **2** | `block_effect` · `block_combat_retreat` |
| **C. 未启用**（数据权重全 0 + 代码无 case） | **2** | `retreat_from_combat` · `attack_friendly` |
| **D. 靠"无 case"实现语义** | **1** | **`nothing`**（**不需要 case，也不能加 `default`**）|

🎖️ **D 档最值得记**：🔴 **"什么都不做"是靠【没有 case + 没有 default】实现的** ⚠️
   ⇒ 📌 **采用时若给 switch 加一个 `default`（例如"未知就选第一个"）⇒
      会把 `nothing` 的语义【悄悄改掉】** ⚠️
   🎖️ **判据**：**"这个 `switch` 的【缺失分支】是缺陷还是语义？"** ——
      **有 `default` ⇒ 缺失是缺陷；没有 `default` ⇒ 缺失【可能是语义】** ✓
      📌 **而这条与我方 `LightMeter` 的"兜底到 Black"是【同一类判断】** ✓
      （那次我方**选择兜底**并注明"不静默选一个看起来合理的档"）✓
```
