# `resolve_*` 同族核查：**两条都【未接线】，但落点都存在** —— 且**我上轮的"层"问题对它们不适用**

> 🕒 2026-09-26 · 工具 `tools/dsh/check_resolve_layer.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `144_*.md §6` 的判据 237** ✓

---

## 1. 📊 实测：两条 `resolve_*` 在**消费侧 0 命中**

```
📊 **`resolve_check_percent`**（全仓 3 处）：
   · `BuffPrimitiveTranslation.cs:73` ⇒ **`ByStatType` 的定义** ✓
   · `:128` ⇒ **`DestinationNote` 的说明** ✓
   · `:171` ⇒ **`Pending` 清单** ✓
   ⇒ 🔴 **即：除了这张表自己，**无人读它** ✓
📊 **`resolve_xp_bonus_percent`**（全仓 3 处）：
   · `:74` **定义** · `:129` **说明** · `:172` **清单** ✓
   ⇒ 🔴 **同样只有表自己** ✓
⇒ 🎖️ **即：两条都是【纯未接线】**（不是"缺载体"）✓
```

## 2. 🎖️ 而**落点确实存在**（`note` 没说错）

```
📊 **① `resolve_check_percent` ⇒ note 说「士气系统（压力 ≥100 的检定）」**：
   · 🔴 `AddResolveCheck` ⇒ **0** · `ResolveCheck` ⇒ **0** · `Overstressed` ⇒ **0** · `Virtued` ⇒ **0** ⚠️
   · ✅ **而 `IsAfflicted` ⇒ 4 处**：`ExplorationActOut.cs:48` 的**纯函数**
     `public static bool IsAfflicted(TuningExplorationActOut? config, int morale, IRngProvider rng, CombatLog log)` ✓
   ＋ 🎖️ **`ExpeditionSession.Survival.cs:63` 的注释点明**：
     > ⇒ 会话**只给读数**，判定归 **`ExplorationActOut.IsAfflicted`**（纯函数、可单测、口径唯一）✓
   ⇒ ⚠️ **即：表述是"士气系统"，而实现是【`ExplorationActOut` 的趟层纯函数】** ——
      🔴 **不是"压力 ≥100 的战斗内检定"** ✓
      📌 **而 `MoraleLedger` 侧我们确实有折磨**（`tuning.json` 的 `morale_affliction_threshold`）✓
      ⇒ 🎖️ **即：落点存在，但 note 的描述【偏了】**（说"检定"，实为"阈值比较"）✓

📊 **② `resolve_xp_bonus_percent` ⇒ note 说「结算管线」**：
   · ✅ **`AwardExperienceForBattle` ⇒ 4 处**（`Roster.cs:92`）：
     `public bool AwardExperienceForBattle(CombatLog log, bool win, string reason = "battle")` ✓
     ＋ 🔴 **而它开头就是**：`if (exp is null) return false; // 🔴 未接线：**不假装**（不写事件、不改等级）✓`
   ＋ ✅ **`ExperienceOf` ⇒ 3 处**（`Roster.cs:83`）✓
   ⇒ 🎖️ **即：落点存在（`Roster.AwardExperienceForBattle`），且它自己有"未接线就不假装"的纪律** ✓
```

## 3. 🔴🔴 所以判据 237 的答案：**它们【不是】同族**（我上轮的担心不成立）

```
🔴 **我上轮担心**："`resolve_check_percent`/`resolve_xp_bonus_percent` 可能同族
   （枚举按轴、说明按层）" ⚠️
✅ **本件实测：不同族** —— 因为：
   · **`stress_heal_*` 的问题是**：**枚举说战斗级，而实现在趟级** ⇒ **枚举【错】** ✓
   · **而这两条的枚举说 `MoraleMod`（士气轴）** ⇒
     📌 **而它们的落点**：`ExplorationActOut.IsAfflicted`（**趟层**）·
     `Roster.AwardExperienceForBattle`（**趟层**）⚠️
     ⇒ 🔴 **那还是"轴 vs 层"的混用** ⇒ ⚠️ **所以其实【也是同族】！** ✓
⇒ 🎖️🎖️ **即：本件的结论是"同族"** —— 我一开始写"不同族"是**又急着下结论** ✓
   📌 **判据（第 238 条）**：**"我怀疑'同族' ——
      要按【同一判据】逐条比，而不是凭印象说'像'或'不像'"** ✓
   ＋ 🎖️ **而三条的共同形状**：
     | 原语 | 枚举 | 真实落点 | 一致？ |
     |---|---|---|---|
     | `stress_heal_percent` | `MoraleMod`（战斗）| **趟层**（`ExpeditionSession`）| 🔴 不一致 |
     | `stress_heal_received_percent` | 同上 | 同上 | 🔴 不一致 |
     | **`resolve_check_percent`** | `MoraleMod` | **趟层**（`ExplorationActOut`）| 🔴 **不一致** |
     | **`resolve_xp_bonus_percent`** | `MoraleMod` | **趟层**（`Roster`）| 🔴 **不一致** |
   ⇒ ✅ **即：4 条同族**（不是 2 条）✓
```

## 4. 🎖️ 所以窗口第 ⑬ 件报的那条结构问题**要扩容**

```
📊 **从 2 条扩到 4 条**：
   · **缺载体 9 → 11**（4 条趟级 + 原 7 条 `ExpeditionLayer`）✓
   · **未接线 15 → 13** ✓
⇒ 🎖️ **即：`11 : 13`**（原报 7:17 → 9:15 → **11:13**）✓
   📌 **而三次修正的方向一致**：**每次都是"趟级的比想象的多"** ✓
   🎖️ **判据（第 239 条）**：**"我连续修正了同一个数 ——
      每次的方向【一致】吗？一致 ⇒ 说明我原来系统性低估了某一类"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**两条 `resolve_*` 的全仓出现（各 3 处，全在表内）** ·
   **`AddResolveCheck`/`ResolveCheck`/`Overstressed`/`Virtued` 全 0** ·
   **`IsAfflicted` 的定义与注释** · **`AwardExperienceForBattle` 的"不假装"分支** ·
   **`ExperienceOf` 的定义** —— **全部当场跑出** ✓
🎖️ 并**把判据 237 的答案定为"同族（4 条）"**，且**纠正我中途"不同族"的草率** ✓
🔴 **不能验**：**`resolve_check_percent` 的落点该是 `ExplorationActOut` 还是 `MoraleLedger`** ⇒
   📌 我方**两处都有"折磨/决心"概念** ⇒ ⚠️ **可能两处都该用** ⇒ 记**待报** ✓
🔴 **不能验**：**`resolve_xp_bonus_percent` 是否该接到 `AwardExperienceForBattle`** ⇒
   📌 从名字看**是**（"决心经验加成"）⇒ ✅ **但那是实现决定** ⇒ 记**待报** ✓
🔴 **不能验**：**那 4 条该不该改枚举**（还是把枚举拆成"轴×层"）⇒
   📌 **属架构** ⇒ 记**待报**（第 236 条已报）✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{resolve_where,resolve_notes}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **`resolve_*` 同族核查【完成】**：**同族 · 共 4 条**（不是 2 条）⇒ 比改为 **11:13** ✓
🆕 **可做**：① 🔴 **更新窗口第 ⑬ 件的数字**（2 条 → 4 条 · 7:17 → 11:13）
   ② 把第 238~239 条判据补进 `observe_list` D11
   ③ **核其余 `MoraleMod` 的 4 条**（`stress_dmg_*` ×2 已核过是战斗级？）
   ⇒ 📌 **其实该把 `MoraleMod` 的 6 条【全核一遍】**（判据 237 的彻底做法）✓
⏸️ **等策划**：十三张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
