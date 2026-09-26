# 目标侧读通：**`FilterTargets` 5 个过滤 + `ChooseTargets` 随机** —— 两半机制齐了

> 🕒 2026-09-26 · 承接 `80_ai_mechanism_read.md §5` 的"目标侧未读" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 目标侧基类（`TargetSelectionDesire.cs` · 123 行）

```
🎖️ **字段**：
   · `Type`（`TargetDesireType`）· **`Chance`**（`IProportionValue` ⇒ 权重）✓
   · **`Parameters`**：`Dictionary<TargetSelectParameter, bool?>` ⇒ 🔴 **是 `bool?` 不是 `int?`** ⚠️
   · `SpecificCombatSkillId` · `IsEnemyTargetDesire` · `IsFriendlyTargetDesire` ✓
⇒ 🎖️ **即：技能侧的限制是【整数阈值】（`int?`），目标侧是【布尔开关】（`bool?`）** ✓
   📌 **两侧的"排除"形状不同** ⇒ ✅ 与第 35 条判据（技能/目标是否同一种抽取）**呼应** ✓
```

## 2. 🎖️ `SelectTarget` 的三道前置门（**不是过滤，是直接拒**）

```
① `if (!(SpecificCombatSkillId == "" || SpecificCombatSkillId == decision.SelectedSkill.Id))`
   ⇒ 🔴 **这条欲望只对【特定技能】生效** ✓
② `if (TargetRanks.IsSelfFormation && !IsFriendlyTargetDesire) return false;`
   ⇒ **打自己人但没标"友方欲望" ⇒ 拒** ✓
③ `if (!(IsSelfFormation || IsSelfTarget) && !IsEnemyTargetDesire) return false;`
   ⇒ **打敌人但没标"敌方欲望" ⇒ 拒** ✓
⇒ 🎖️ **即：②③ 是【方向校验】** —— 数据里的 `is_enemy_target_desire`/`is_friendly_target_desire`
   **就是为这两条服务的** ✓
   📌 而这两个键在**数据里 100% 出现**（`79_*.md` 实测：`random_target` 10 键里有它们）✓
```

## 3. 🎖️ `FilterTargets` —— **5 个布尔过滤**（全是否定式）

| 参数 | 行为 |
|---|---|
| `CanTargetDeathsDoor = false` | `RemoveAll(unit => unit.Character.AtDeathsDoor)` |
| `CanTargetLastHero = false` | `RemoveAll(unit => performer.CombatInfo.LastCombatSkillTarget == unit.CombatInfo.CombatId)` ⇒ 🔴 **排除"上次打过的"** |
| `CanTargetNotOverstressed = false` | `RemoveAll(unit => !unit.Character.IsOverstressed)` ⇒ 🎖️ **注意双重否定**：`false` ⇒ **只留【已过压】的** ⚠️ |
| `CanTargetAfflicted = false` | `RemoveAll(unit => unit.Character.IsAfflicted)` |
| `CanTargetVirtued = false` | `RemoveAll(unit => unit.Character.IsVirtued)` |

```
🎖️ **判据（第 36 条）**：**"这个键是【黑名单】还是【白名单】？看它 `RemoveAll` 的条件"** ✓
   · `CanTargetAfflicted = false` ⇒ `RemoveAll(IsAfflicted)` ⇒ **"不能选有折磨的"** ✓
   · `CanTargetNotOverstressed = false` ⇒ `RemoveAll(!IsOverstressed)` ⇒
     🔴 **"不能选【没】过压的"** = **"只能选已过压的"** ⚠️
   ⇒ 📌 **两个键名字一正一反，语义也一正一反** ⇒ ⚠️ **抄错一个就反了** ✓
   🎖️ **且 5 个都是【只在值为 `false` 时生效】**（`if (...Value)` 取反才 RemoveAll）
      ⇒ ✅ **即：`true` = 不过滤**（默认允许）✓
```

## 4. 🎖️ 而 `ChooseTargets` —— **多目标 vs 单目标**

```
· **`IsMultitarget`** ⇒ `Targets.AddRange(availableTargets)` ⇒ **全选** ✓
· **否则** ⇒ `int index = Random.Range(0, availableTargets.Count)` ⇒ 🔴 **等概率随机** ✓
   ＋ `ExtraTargetsChance > 0` 且 `CheckSuccess(...)` ⇒ **再加一个随机目标** ✓
⇒ 🎖️ **即：目标侧【在欲望抽中之后】也是等概率随机** ✓
   📌 **所以权重的角色是：决定【哪条欲望】被抽中**，而不是"选哪个目标" ✓
      🎖️ 与技能侧完全对称（**权重选"哪条欲望"，然后等概率选实际对象**）✓
```

## 5. 🎖️🎖️ 于是**两半机制彻底齐了**（本件最有用的汇总）

| | **技能侧** | **目标侧** |
|---|---|---|
| 基类限制 | `Dictionary<…, **int?**>` | `Dictionary<…, **bool?**>` |
| 限制形状 | **14 个整数阈值**（`> N` / `< N`）| **5 个布尔开关** |
| 前置门 | — | **3 道**（特定技能 / 友方 / 敌方）|
| 权重 | `Chance`（`base_chance × 100`）| `Chance` |
| **抽签方式** | **等概率**（`RandomSolver.Next`）| **等概率**（`Random.Range`）|
| 权重的用途 | 决定**哪条欲望** | 决定**哪条欲望** |
| 失败处理 | 换下一条（`while`）| 换下一条（`while`）|

```
⇒ 🎖️ **即：两侧【结构高度对称】** ——
   都是"**硬排除 → 按权重选欲望 → 等概率选实际对象**" ✓
   📌 差异只在**排除条件的形状**（整数阈值 vs 布尔开关）⇒ ✅ **可分别实现** ✓
   🎖️ **这比 `79_*.md` 时的判断更乐观**：那次说"两套机制"，现在看是**同一骨架的两种参数** ✓
```

## 6. 诚实边界

```
✅ **能验**：**`TargetSelectionDesire.cs` 的 123 行原文** · **3 道前置门** ·
   **5 个布尔过滤的逐个 `RemoveAll` 条件** · **`bool?` vs `int?` 的差异** ·
   **`IsMultitarget` 与 `Random.Range` 的等概率** ·
   **两侧结构对照表** —— **全部当场跑出** ✓
🎖️ 并**把"两套机制"修正为"同一骨架的两种参数"** ✓
🔴 **不能验**：**`TargetSelectParameter` 枚举的【完整项数】** ⇒
   📌 本件只见 5 个被用 ⇒ ⚠️ 枚举可能更长 ⇒ 记**未核** ✓
🔴 **不能验**：**`ExtraTargetsChance` 的数据来源**（哪个技能字段）⇒ 记**未查** ✓
🔴 **不能验**：**`bonus_initiative_desires` 的消费代码**（第三张表）⇒ 记**仍未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（本件为直接读码）✓
```

## 7. 下一步

```
✅ **A5 的机制【两半都读通】**：技能侧 14 阈值 · 目标侧 5 布尔 · 骨架对称 ✓
🆕 **可做**：① 🔴 **读 `bonus_initiative_desires` 的消费代码**（三张表的最后一张）
   ② 核 `TargetSelectParameter` 枚举的完整项数（关掉 §6 的口子）
   ③ 把第 34/35/36 条判据补进 `observe_list` D11 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
