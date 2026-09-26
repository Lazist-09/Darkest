# 先手奖励的**接线查清**：3 个调用点 · `BonusTurn(选择器)` · 命中即**换技能并 break**

> 🕒 2026-09-26 · 承接 `82_*.md §6` 的"调用点未查" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **这是"接上先手层"的前置** ⇒ 本件读完 ✓

---

## 1. 🎖️🎖️ `BonusTurn` —— 一个**按时机选择器**驱动的统一挂点

```csharp
// RaidSceneManager.cs:3524
protected virtual IEnumerator BonusTurn(Predicate<BonusInitiativeDesire> desireSelector)
{
    TempList.AddRange(BattleGround.MonsterParty.Units);
    while (TempList.Count > 0) {
        var monsterUnit = TempList[0]; TempList.RemoveAt(0);
        if (monsterUnit.Character is Hero) continue;          // 🔴 只对怪
        var monster = monsterUnit.Character as Monster;
        var desires = monster.Brain.BonusDesireSet.FindAll(**desireSelector**);
        while (desires.Count > 0) {
            var currentDesire = desires[0]; desires.RemoveAt(0);
            if (currentDesire.CheckBonusInitiative(monsterUnit)) {
                yield return StartCoroutine(MonsterTurn(monsterUnit,
                    currentDesire.**CombatSkillOverride**, **true**));   // ← fromBonusTurn
                **break**;                                     // 🔴 命中即停
            }
        }
    }
    TempList.Clear();
}
```
```
🎖️ **即：三个要点**
   ① **按【时机】筛**：`FindAll(desireSelector)` ⇒ **同一份 `BonusDesireSet` 被三个时机各自筛一遍** ✓
   ② **命中即 `break`** ⇒ 🔴 **同一回合【最多触发一次】先手奖励** ⚠️
      📌 **不是"逐条都试"** —— 而是**第一条成立的就执行，然后停止** ✓
   ③ **`MonsterTurn(..., override, fromBonusTurn: true)`** ⇒
      🎖️ **走的是"打过标记的回合"** ⇒ 而 `MonsterTurn` 里 **`if (!fromBonusTurn)` 出现 3 次**
      ⇒ 📌 **即：先手回合【跳过】某些常规步骤**（`3431`/`3439`/`3519`）⚠️
```

## 2. 📊 三个调用点（**对应 3 个时机**）

| 行 | 选择器 | 时机 |
|---|---|---|
| **`2402`** | `desire => desire.**IsRoundStart**` | 回合开始 |
| **`2712`** | `desire => desire.**IsPostTurn** && desire.**IsRoundInProgress**` | 回合中·回合后 |
| **`2718`** | `desire => desire.**IsRoundFinish**` | 回合结束 |

```
🔴 **只有 3 个调用点，但开关有 4 个** ⇒ 📌 **`IsRoundInProgress` 没有单独调用点** ✓
   —— 它**只作为 `2712` 的【附加条件】**（`IsPostTurn && IsRoundInProgress`）✓
   🎖️ **判据（第 41 条）**：**"我数了 N 个开关 —— 是不是就有 N 个调用点？"
      ⇒ 不是 ⇒ 有的开关是【组合条件】，不是【独立挂点】"** ✓
```

## 3. 🎖️ 而 `BonusDesireSet` 的**装载**在 `DarkestDatabase.cs`

```
`258` `brain.BonusDesireSet.Add(new **BonusInitiativeHpRatio**(jsonBonusDesire.data));`
`261` `… new **BonusInitiativeDeath**(…)`
`264` `… new **BonusInitiativeGuaranteed**(…)`
`267` `… new **BonusInitiativeAllyLastDamaged**(…)`
`270` `… new **BonusInitiativeLastSkill**(…)`
`273` `… new **BonusInitiativeAllyClassCount**(…)`
⇒ 🎖️ **6 个 type ↔ 6 个类【一一对应】** ✓ 与 `82_*.md` 的"6 子类"**完全吻合** ✓
```

## 4. 🎖️ 采用的**完整接线清单**（本件最有用的产出）

```
📊 **要接上"先手奖励"这一层，需要 4 件**：
   ① **数据**：`bonus_initiative_desires`（6 种 type · 数据里已抽 ✓）
   ② **解析**：6 个类 + 基类（**含 4 个时机开关 + `combat_skill_id_override`**）✓
   ③ **挂点**：**`BonusTurn(选择器)`** × **3 个时机**（`2402`/`2712`/`2718`）✓
   ④ **执行**：`MonsterTurn(unit, override, **fromBonusTurn: true**)`
      ⇒ ⚠️ **且要先实现"`fromBonusTurn` 跳过哪 3 处"** ✓
⇒ 🎖️ **即：这一层不是"再加个字段"，而是【一个完整的子系统】** ✓
   📌 与我方现状（`79_*.md`：`enemy_ai.json` 完全没有它）**一致** ⇒ 归**新建** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`BonusTurn` 的 25 行全文** · **命中即 `break`** · **3 个调用点及其选择器** ·
   **`IsRoundInProgress` 只是组合条件** · **`BonusDesireSet` 的 6 个 `Add`（逐行）** ·
   **`MonsterTurn(..., fromBonusTurn)` 的签名** —— **全部当场跑出** ✓
🎖️ 并**把"接上这一层"的 4 件依赖列全** ✓
🔴 **不能验**：**`fromBonusTurn` 到底跳过哪 3 处**（`3431`/`3439`/`3519` 的体未读）⇒
   📌 **那是接线的第 4 件，必须读** ⇒ 记**未读**（**已标为待办**）✓
🔴 **不能验**：**联机侧 3 个 `CombatSkillOverride`（`RaidSceneMultiplayerManager` 646/955/987）
   与单机的差异** ⇒ 记**未比** ✓
   📌 与 `36_actout_multiplayer.md` 那次（"同一 switch 的第二份实现"）**同族**
      ⇒ ⚠️ **很可能又是"两份实现"** ⇒ **记下这个怀疑** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/bonus_callsite.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **A5 三张表 + 接线【全部查清】**：技能 · 目标 · 先手（含 3 个挂点与执行路径）✓
🆕 **一条疑点**：🔴 **联机侧 3 处 `CombatSkillOverride`** ⇒ 很可能又是"两份实现"
🆕 **可做**：① 🔴 **读 `fromBonusTurn` 跳过的 3 处**（接线的最后一件）
   ② 比联机侧与单机的 `BonusTurn`（验证"两份实现"的怀疑）
   ③ 把第 37~41 条判据补进 `observe_list` D11
   ④ **把 A5 的完整机制写进 `PLAN_adoption`**（三张表 + 挂点 + 原始拼写错）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
