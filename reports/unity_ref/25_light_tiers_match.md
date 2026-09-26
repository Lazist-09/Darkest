# 亮度 5 档：我方与参考 **逐档逐界完全一致**（我第一版算错了方向）

> 🕒 2026-09-26 · 承接 `24_economy_correction.md` §6 的"`darkness` 5 档 vs `light.json` 未核" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴🔴 结论：**逐档逐界完全一致**

```
🎖️ **参考侧**（`Raid\TorchMeter.cs:123-129`，**读代码得到**）：
   `new TorchRange(Radiant,  …, **76, 100**)`
   `new TorchRange(Dim,      …, **51,  75**)`
   `new TorchRange(Shadowy,  …, **26,  50**)`
   `new TorchRange(Dark,     …, ** 1,  25**)`
   `new TorchRange(Out,      …, ** 0,   0**)`
🎖️ **我方侧**（`darkest/data/tuning.json → light.tiers`）：
   `{id: radiant,  min: **76**, max: **100**}`
   `{id: dim,      min: **51**, max: ** 75**}`
   `{id: shadowy,  min: **26**, max: ** 50**}`
   `{id: dark,     min: ** 1**, max: ** 25**}`
   `{id: black,    min: ** 0**, max: **  0**}`
⇒ ✅ **5 档的名称与两个边界【逐个相同】**（只有 `Out` ↔ `black` 的名称不同）✓
   🎖️ **这是第 5 处"两侧完全一致"**（前 4：A11 兑换表 · `amount_table` · 引用闭合 · 饰品价）✓
```

## 2. 🔴 而我第一版**算错了方向**（如实记下）

```
❌ **我第一版**用 `darkness = 100 − light` 去换算 ⇒ 得出"交集只有 1 个（`0`）"、
   "只有头尾对得上" ⇒ ⚠️ **结论完全错** ✓
🔴 **根因**：**我假设 `darkness` 与 `light` 是【互补量】** ——
   而**读代码才知道**：参考的 `darkness` **不是"亮度"**，而是
   **"该档的 `Min` 值"** ⇒ 🎖️ 见消费点：
   ```csharp
   // Raid\Battle\BattleGround.cs:484
   if (darkenessLoot[i].DarknessLevel == RaidSceneManager.TorchMeter.CurrentRange.**Min**)
   ```
   ⇒ ✅ **即：`darkness_bonuses[].darkness` 存的是"档的 Min"**
      （`0 / 1 / 26 / 51 / 76` **正好就是 5 档的 5 个 Min**）✓
⇒ 🎖️ **判据**：**"这个数，是【量】还是【标识】？"** ——
   · **量** ⇒ 可做算术（加减/换算）✓
   · **标识** ⇒ **只能做相等比较**，做算术就错 ⚠️
   📌 **`darkness` 是【标识】** ⇒ 我拿它当"量"去减 ⇒ 必错 ✓
   🔴 **这与我上一件的"通道"错【同族】** ⇒ **共同形状：我用错了这个字段的语义** ✓
```

## 3. 🎖️ 顺带读出了参考的**第 5 档语义**（我方缺的一环）

```
参考的 5 个 `TorchRangeType`：`Radiant` · `Dim` · `Shadowy` · `Dark` · **`Out`** ✓
   而第 5 档是 **`Out`**（**"火把灭了"**），`Min = Max = 0` ✓
   我方第 5 档叫 **`black`**（`0, 0`）⇒ 🎖️ **语义相同，命名不同** ✓
   🔴 而参考**只在 `Out` 档做一件特别的事**（`TorchMeter.cs:416`）：
      `if (CurrentRange.RangeType == TorchRangeType.Out) …`
      ⇒ 📌 **"火把全灭"是一个【独立分支】**（不是"最暗的普通档"）⚠️
      🎖️ **而我方 `black` 档【有没有这个特判】未核** ⇒ 记为**待核** ✓
```

## 4. 🎖️ 而我方的**每档收益**与参考是**两套形状**

```
我方（两块数据）：
   `economy.light_tier_bonus`（**给几份**）：`{radiant: 0, dim: 0, shadowy: 1, dark: 2, black: 3}`
   `tuning.light.loot`（**给什么**）：
      `radiant: {}` · `dim: {food: 1}` · `shadowy: {firewood: 1}` ·
      `dark: {firewood: 1, food: 1}` · `black: {firewood: 2, food: 2}` ✓
   ⇒ 🎖️ **即：越暗给越多**（`radiant/dim` 是 0，`black` 是 2 柴 + 2 粮）✓

参考（`JsonLoot.darkness_bonuses`）：
   `battle`：`darkness 0 ⇒ chance 0.75 codes [B, B]` · `1 ⇒ 0.75 [B]` ·
      `26 ⇒ 0.5 [B]` · `51 ⇒ 0.25 [B]` · `76 ⇒ 0 [B]` ✓
   `chest`：`darkness 0 ⇒ 0.95 [A]` · `1 ⇒ 0.75 [A]` · `26 ⇒ 0.5` · `51 ⇒ 0.25` · `76 ⇒ 0` ✓
   ⇒ 🔴 **即：越【亮】给越多**（`darkness 0` = 最亮 ⇒ 0.75 概率给 **2 个 B**；
      `darkness 76` = 最暗 ⇒ **0**）⚠️
   🎖️ **而这与我方【方向相反】**：
      · **我方**：越暗给越多（`black` 2+2）✓
      · **参考**：越亮给越多（`darkness 0` 给 2 个）⚠️
      📌 **但两者针对的东西不同**：
         · 我方 `light_tier_bonus`/`light.loot` = **"走到暗处捡到柴火/口粮"**
         · 参考 `darkness_bonuses` = **"战斗结束/开箱时额外给掉落表"**（`battle` / `chest`）
      ⇒ ✅ **即：不是矛盾，是【两个不同的机制】**（纪律 AU：先看它挂在什么事件上）✓
      🔴 **而我方【有没有 `darkness_bonuses` 对应的机制】未核** ⇒ 记**待核** ✓
```

## 5. 诚实边界

```
✅ **能验**：参考 `Ranges` 的 **5 档逐界**（读代码 `TorchMeter.cs:123-129`）·
   我方 `light.tiers` 的 5 档逐界 · **两侧逐个相同** ·
   **`darkness` 是"档的 Min"**（消费点 `BattleGround.cs:484` 原文）·
   我方两块收益数据 · 参考 `battle`/`chest` 两组 ·
   **方向相反但是两个不同机制** —— **全部当场跑出** ✓
🎖️ 并**如实记下我第一版算错方向**（把"标识"当"量"去做减法）✓
🔴 **不能验**：**没有改任何数值** ⇒ "接上后掉落会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：我方 `black` 档**有没有 `Out` 那样的特判** ⇒ 记**待核** ✓
🔴 **不能验**：我方**有没有 `darkness_bonuses` 的对应机制**（战斗/开箱的亮度加成）⇒ 记**待核** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/light_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **已核**：亮度 5 档**完全一致**（**第 5 处两侧一致**）⇒ 这一块**无需改动** ✓
🆕 **可做**：① 核我方 `black` 档是否有 `Out` 特判
   ② 核我方是否有 `darkness_bonuses`（战斗/开箱的亮度加成）的对应机制
   ③ 补一条给策划：**"亮度 5 档无需改动"**（正面结论也要报）✓
⏸️ **等策划**：六张单已投递 ✓
```
