# 两处待读收口：`TotalInitiatives` 的真相 —— **「先攻 0」= 本回合【还没轮到】**，不是"废弃怪"

> 🕒 2026-09-26 · 工具 `tools/dsh/read_initiative_and_2817.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `95_*.md §7` 的两条"未读"** ✓

---

## 1. 🔴🔴 先纠一处**我自己的推断**（第 30 次同族）

```
🔴 `95_*.md §2` 我推断：**"`TotalInitiatives == 0` ⇒ 被跳过/未轮到"**，
   并**猜成因**："**怪物被击败/被替换/生成后未入队**" ⚠️
✅ **本件实测（写入点在 `FormationUnitInfo.cs`）**：
   · **`L46`** `PrepareForBattle(id, monster, checkLoot)` ⇒
     **`TotalInitiatives = monster.Initiative.NumberOfTurns`** ✓
   · **`L70`** `PrepareForBattle(id)`（**另一个重载**）⇒ **`TotalInitiatives = 1`** ✓
   · 🔴 **`L98` `UpdateNextRound()` ⇒ `CurrentInitiative = 0`**（**不是 `TotalInitiatives`**）✓
   · **`L92` `UpdateNextTurn()` ⇒ `CurrentInitiative++`** ✓
⇒ 🎖️ **即：`TotalInitiatives` 是【不变量】**（战斗开始时设一次，**之后再没被写**）✓
   📌 **而它会 `== 0` 的唯一可能**：**`monster.Initiative.NumberOfTurns == 0`** ✓
      ⇒ 关联 `Round.cs:158`：`if (Initiative == null || **NumberOfTurns < 1**) return;`
         ⇒ **那样的怪【根本不入 `OrderedUnits`】** ✓
🔴 **所以我 `95_*.md` 的猜测（"被击败/替换"）是错的** ——
   **真相是"它从一开始就没有行动额度"** ✓
```

## 2. 🎖️ 于是 `idleUnit` 的**真实语义**变了（这是本件最重要的修正）

```
📊 **链条**：
   ① `Initiative.NumberOfTurns`（`Initiative.cs:20`，从数据 `.initiative` 读）✓
   ② `Round.BuildOrder`：**按 `NumberOfTurns` 次把该怪**加入 `OrderedUnits`（`Round.cs:123`）✓
      ⇒ 🔴 **`NumberOfTurns == 0` ⇒ 一次都不加** ⇒ **本回合完全没有它的行动位** ✓
   ③ `RaidSceneManager:2721`：**`FindAll(TotalInitiatives == 0)`** ⇒ 找出那些怪 ✓
   ④ `2729`/`2748`：**在回合末给它们结算 DoT（×1.5）** ✓
⇒ 🎖️ **即：`idleUnit` = 【本回合没有行动位的怪】** ✓
   📌 **而这不是"异常"，是【数据允许的正常状态】**（`NumberOfTurns` 可以为 0）✓
   🎖️ **所以 `×1.5` 的意图更可能是**：
      **"不行动的怪，DoT 反而扣得更多"** ⇒ ⚠️ **仍然只能记【推断】** ✓
      📌 **但已排除"它是异常兜底"这个解释** ✓
```

## 3. 🎖️ 而 `2817`/`2835` 的场合：**英雄侧**（补全 4 个结算点）

```
📊 **原文**（`RaidSceneManager.cs:2799`）：
   `2799| protected virtual IEnumerator **HeroTurn**(FormationUnit actionUnit, bool fromBattleSave = false)`
   `2811|     #region Status Effect and Buffs`
   `2812|     if (…StatusType.Bleeding).IsApplied) {`
   `2817|         if (ProcessDamage(actionUnit, bleedEffect.CurrentTickDamage)) …
   `2830|     if (…StatusType.Poison).IsApplied) {`
   `2835|         if (ProcessDamage(actionUnit, poisonEffect.CurrentTickDamage)) …
   ＋ 🔴 **而它没有 `!fromBonusTurn` 那样的开关** ——
      `HeroTurn` 只判 `if (fromBattleSave == false)` ✓
⇒ 🎖️ **即：英雄的 DoT 在【自己的回合】结算** ✓
   📌 **与怪物侧对称**（`MonsterTurn:3448`/`3470`）⇒ ✅ **两边都走 `ProcessDamage`** ✓
```

### 🎖️🎖️ 所以 DoT 的 **4 个结算点全齐了**（本件的收官）

| # | 行 | 单位 | 算法 | 场合 |
|---|---|---|---|---|
| ① | **2817**/2835 | **英雄** | `ProcessDamage`（×1.0）| **英雄自己的回合** |
| ② | **3448**/3470 | **怪物** | `ProcessDamage`（×1.0）| **怪物自己的回合**（`!fromBonusTurn` 区） |
| ③ | **2729**/2748 | **`idleUnit`** | `TakeDamage(CeilToInt(× **1.5f**))` | **回合末**（先攻 0 的怪）|
| ④ | **4683**/4721 | **`UnitEventQueue[i]`** | `ProcessDamage`（×1.0）| **事件队列** |

```
⇒ 🎖️ **规律**：**只有 ③ 用 `TakeDamage` + ×1.5**，其余三处都走 **`ProcessDamage` ×1.0** ✓
   📌 **判据（第 74 条）**：**"4 个结算点里，哪一个【不一样】？
      ⇒ 不一样的那个往往对应【一个特殊的场合】"** ✓
   🎖️ **而 ③ 特殊在哪**：**它没有"自己的回合"** ⇒
      所以**必须由回合末代它结算**（否则那怪的 DoT 永远不生效）✓
       ⇒ ✅ **这才是 ×1.5 的存在理由**（**补偿**，而非惩罚）✓
       📌 **与 `95_*.md` 的"惩罚"推断相反** ⇒ ✅ **本件修正为"补偿"**（**倾向更强**：若不补，DoT 就废了）✓
```

## 4. 🎖️ 顺带：`CurrentInitiative` vs `TotalInitiatives` 的**分工**

```
📊 **两个字段（`FormationUnitInfo.cs:17`/`18`）**：
   · **`TotalInitiatives`** ⇒ **不变**（战斗开始时 = `NumberOfTurns`）✓
   · **`CurrentInitiative`** ⇒ **`UpdateNextTurn()` ++** · **`UpdateNextRound()` = 0** ✓
⇒ 🎖️ **即：一个是【本回合总共有几次】，一个是【已经用了几次】** ✓
   📌 **而 `FirstInitiativeOnly`（4 处技能欲望用）判的是**
      `performer.CombatInfo.CurrentInitiative != 1` ⇒ 🎖️ **"是否第 1 次行动"** ✓
      ⇒ ✅ **这解释了 A5 的 `first_initiative_only` 键**（`79_*.md` 提过它）✓
      🎖️ **判据（第 75 条）**：**"两个相似字段 —— 哪个会被改？
         ⇒ 看 `++`/`= 0` 落在谁身上"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`TotalInitiatives` 的 6 个出现点（含 2 处赋值）** ·
   **`CurrentInitiative` 的 `++`/`= 0` 位置** · **`NumberOfTurns` 的 4 处** ·
   **`Round.cs:123` 的"按 `NumberOfTurns` 次加入"** · **`Round.cs:158` 的 `< 1` 守卫** ·
   **`HeroTurn:2799` 的签名与体内 DoT** · **4 个结算点的完整表** —— **全部当场跑出** ✓
🎖️ 并**连纠自己两处**：`95_*.md` 的"猜测成因"错 · "×1.5 是惩罚"改为**补偿**（倾向更强）✓
🔴 **不能验**：**`×1.5` 的【确切意图】** ⇒ 无注释 ⇒ 仍记**推断**（但**理由比上次强**）✓
🔴 **不能验**：**`NumberOfTurns == 0` 的怪【实际有哪些】** ⇒ 要读数据 ⇒ 记**未查** ✓
   📌 **而那是"实现时要不要处理这个分支"的依据** ⇒ ⚠️ **记为重点待办** ✓
🔴 **不能验**：**`4683`/`4721` 的 `UnitEventQueue` 是什么场合** ⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{fui,initiative,initiative2}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **两处待读【收口】**：`TotalInitiatives` 是**不变量**（非"被改"）· `2817` 是**英雄侧** ✓
🎖️ **并修正了 `95_*.md` 的两处**（成因猜测 · ×1.5 的意图）
🆕 **可做**：① 🔴 **查数据里 `NumberOfTurns == 0` 的怪有多少**（决定要不要处理该分支）
   ② 读 `4683`/`4721` 的 `UnitEventQueue` 场合（4 个结算点的最后一块）
   ③ 把第 67~75 条判据补进 `observe_list` D11
   ④ 把"`first_initiative_only` 的解释"补进 A5 行 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
