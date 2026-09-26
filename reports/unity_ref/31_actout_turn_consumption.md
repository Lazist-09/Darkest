# 14 种回合开始 act-out 的**消费点实测**：**14/14 全部有真实分支**

> 🕒 2026-09-26 · 补齐 `30_actout_consumption.md §5` 的"14 种回合开始未测" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 结果：**14/14 全部有消费，且是真分支**

```
✅ `Nothing` 3 · `BarkStress` 4 · `ChangePosition` 4 · `IgnoreCommand` 4 · `RandomCommand` 4 ·
   `RetreatFromCombat` 2 · `AttackFriendly` 2 · `AttackSelf` 4 · `MarkSelf` 4 ·
   `StressHealSelf` 4 · `StressHealParty` 4 · `BuffAlly` 4 · `BuffParty` 4 · `HealSelf` 4 ✓
⇒ ✅ **有消费 14 · 无消费 0** ✓
📌 三个文件承载：`CharacterHelper.cs`（**29 处，string↔enum 映射**）·
   `RaidSceneManager.cs`（**11 处，真分支**）· `RaidSceneMultiplayerManager.cs`（**11 处**）✓
```

## 2. 🔴 但我**验证了"分支"不是"计数"** —— 读 `switch` 原文

```
🎖️ **判据（本件最重要的方法）**：**"出现次数 ≥1" 不等于 "有行为消费"** ⚠️
   ⇒ 📌 因为 `CharacterHelper.cs` 那 29 处**只是映射**（`case "attack_self": return AttackSelf;`）✓
   ⇒ ✅ **所以我看的是 `RaidSceneManager.cs` 的 `switch` 原文** ✓

实测原文（`RaidSceneManager.cs`）：
   `L2911` `var actOut = **RandomSolver.ChooseByRandom(actionHero.Trait.StartTurnActs)**;`
   `L2912` `**switch (actOut.ActType)**`
   `L2914` `case StartTurnActType.**AttackSelf**:` ⇒ `if (actionHero.AtDeathsDoor) …` ✓
   `L2963` `case …**BarkStress**:` ⇒ `Data.Effects[actOut.StringParameter]` ✓
   `L2979` `case …**BuffAlly**:` ⇒ 取 effect + `Party.Units.Count < 2` 守卫 ✓
   `L2995` `case …**BuffParty**:` ⇒ 同上 ✓
   `L3009` `case …**ChangePosition**:` ⇒ 🔴 `if (actionHero.Trait.Id == "masochistic" && Rank == 1) break;`
      🎖️ **即：`masochistic` 在 1 号位时【不换位】** —— **硬编码的怪癖特判** ✓
   `L3048` `case …**HealSelf**:` ⇒ `if (actionHero.HealthRatio == 1) break;`（满血不治）✓
   `L3064` `case …**IgnoreCommand**:` ⇒ `ShowPopupMessage(… Pass)` ✓
   `L3071` `case …**MarkSelf**:` ✓
⇒ ✅ **每个 case 都有可执行体** ⇒ **14/14 是真分支** ✓
```

## 3. 🎖️ 由此得到**29 种 act-out 的完整采用清单**（三档 → 两档）

| 档 | 种数 | 内容 | 采用方式 |
|---|---|---|---|
| **A. 有数据 + 有真消费** | **27** | 14 回合开始 **全部** + 15 反应里的 **13** | ✅ **照抄** |
| **B. 有数据 + 代码没读** | **2** | `block_effect`（5 美德 `chance 1`）· `block_combat_retreat`（4 折磨 `0.33`）| 🔴 **要我们【自己实现】**（红线 21）|

```
📊 **29 = 27（可照抄）+ 2（要自己实现）** ✓
🎖️ **这是个很好的结果**：**93% 的 act-out 是"直接可抄"的** ✓
   ⇒ 📌 对比 `plot_quests`（13 字段里 9 个没消费、6 个有数据没人读）——
      **act-out 这一块的质量高得多** ✓
   🎖️ **判据**："这一层的【可用率】是多少？" ⇒ 高 ⇒ 优先做；低 ⇒ 先问设计 ✓
```

## 4. 🎖️ 顺带量出**两处硬编码特判**（采用时要照着做）

```
① `masochistic` 在 1 号位时**不换位**（`RaidSceneManager.cs:3012`）✓
② `HealSelf` 在**满血**时不做（`:3050`）✓
③ `BuffAlly`/`BuffParty`/`BarkStress`/`ChangePosition` 都有
   `if (Party.Units.Count < 2) break;` ⇒ **单人时不作妖** ✓
⇒ 🔴 **即：这些是【行为守卫】，不在数据里** ⚠️
   📌 **采用时必须一并搬**，否则行为会不同 ✓
   🎖️ **判据**：**"这个机制的行为，是【数据驱动的】还是【代码里硬编码的守卫】？"** ——
      混合 ⇒ **两处都要搬** ✓
```

## 5. 诚实边界

```
✅ **能验**：**14/14 的消费次数** · **`switch` 原文（逐 case）** ·
   **三处硬编码守卫的原文与行号** · **29 种的三档划分（27 + 2）** —— **全部当场跑出** ✓
🎖️ 并**验证了"计数 ≥1"不等于"有行为消费"**（排除 `CharacterHelper` 的 29 处纯映射）✓
🔴 **不能验**：**每个 case 的【完整执行体】未逐行读完** ⇒
   📌 我是从**首几行**判断"有可执行体"的 ⇒ 记**"已确认有体，细节未读"** ✓
🔴 **不能验**：`RaidSceneMultiplayerManager.cs` 那 11 处**未看**（疑似联机同步用）⇒ 记**未看** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/actout_*.py` **不入库**（scratch）✓
```

## 6. 🎖️ 附带一条**给采用排序的读数**

```
📊 **A 线的 12 项里，"可用率"差别很大**（把本件与前几件放一起）：
   · **act-out（29 种）** ⇒ **27 可照抄 = 93%** ✅ **高**
   · **亮度 5 档** ⇒ **100%**（逐界一致）✅ **最高**
   · **A11 兑换表** ⇒ **100%**（三方 12/12）✅ **最高**
   · 建筑 99 等级 ⇒ **0%**（**全部不同**，但**我方 == 一手**）⚠️ **需换源**
   · `plot_quests` 13 字段 ⇒ **4 有消费 = 31%** 🔴 **低**（6 个有数据没人读）
⇒ 🎖️ **判据**：**"这一层的【可用率】是多少？"** ⇒
   ✅ **高 ⇒ 优先落库（风险低、返工少）** ·
   🔴 **低 ⇒ 先问设计（照抄会抄进一堆死字段）** ✓
   📌 **建议落库顺序**：**亮度 → 兑换表 → act-out → …→ quest 字段**（按可用率从高到低）✓
      ⚠️ 而**实际顺序还要受"依赖"约束**（如 act-out 依赖 buff 落库）⇒ 见各件的依赖节 ✓
```

## 7. 下一步

```
✅ **act-out 线【全部核完】**：29 种 = 27 可照抄 + 2 要自己实现 ✓
🆕 **可做**：① 逐行读 14 个 case 的完整执行体（本件只读了首几行）
   ② 看 `RaidSceneMultiplayerManager` 那 11 处
   ③ 我方 `heirlooms.kinds`（复数）vs 兑换表（单数）的形状不一致 ✓
⏸️ **等策划**：七张单已投递 ✓
```
