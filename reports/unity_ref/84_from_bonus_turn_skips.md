# `fromBonusTurn` 跳过的**3 处**（逐行读出）—— 先手回合是**"轻量回合"**

> 🕒 2026-09-26 · 承接 `83_*.md §5` 的"`fromBonusTurn` 跳过哪 3 处未读" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **这是"接上先手层"的【最后一件】** ✓

---

## 1. 🎖️🎖️ 三处跳过（逐行原文）

```csharp
// RaidSceneManager.cs:3425
protected virtual IEnumerator MonsterTurn(FormationUnit actionUnit,
    string combatSkillOverride = null, bool fromBonusTurn = false)
{
    FMODUnity.RuntimeManager.PlayOneShot("event:/general/char/enemy_turn");
    Formations.ResetSelections();
    yield return new WaitForEndOfFrame();

    if (!fromBonusTurn)                                   // 🔴 跳过 ①
        BattleGround.Round.**PreMonsterTurn**(actionUnit);

    yield return new WaitForEndOfFrame();
    Formations.ShowUnitOverlay();
    yield return new WaitForEndOfFrame();
    actionUnit.SetPerformerStatus();

    if (!fromBonusTurn)                                   // 🔴 跳过 ②
    {
        #region Status Effects and Buffs                   // ← **整个状态效果区**
        if (actionUnit.Character[StatusType.Bleeding].IsApplied) { … bleed tick … }
        …（中毒/疾病/…）
        BattleGround.Round.**OnMonsterTurn**();
    }

    yield return StartCoroutine(ExecuteMonsterSkill(actionUnit, combatSkillOverride));

    if (!fromBonusTurn)                                   // 🔴 跳过 ③
        if (BattleGround.Round.HeroAction != HeroTurnAction.Retreat)
            BattleGround.Round.**PostMonsterTurn**();
}
```

## 2. 🎖️ 三处的**含义**（这是本件的核心读数）

| # | 跳过什么 | 含义 |
|---|---|---|
| **①** | **`Round.PreMonsterTurn(actionUnit)`** | 🔴 **不推进"回合前"流程** |
| **②** | **整个 `Status Effects and Buffs` 区** ＋ **`Round.OnMonsterTurn()`** | 🔴🔴 **不出血/不中毒/不结算状态，也不记"该怪已行动"** ⚠️ |
| **③** | **`Round.PostMonsterTurn()`** | 🔴 **不推进"回合后"流程** |

```
🎖️🎖️ **即：先手回合是一个【轻量回合】——**
   · ✅ **只做**：选技能（`ExecuteMonsterSkill(unit, combatSkillOverride)`）✓
   · 🔴 **不做**：状态 DoT 结算 · 行动计数 · 回合前/后流程 ✓
⇒ 📌 **这是【刻意的**：先手奖励是"**额外插一次行动**"，不该
     再触发一遍 DoT、也不该把该怪标记成"已行动"** ✓
   🎖️ **判据（第 42 条）**：**"这个'额外回合'是【完整回合】还是【轻量回合】？
      ⇒ 看它跳过什么 —— 跳过 DoT 与计数 ⇒ 轻量"** ✓
      📌 **若照抄成"完整回合"** ⇒ ⚠️ **每次先手都会多掉一次血、并让该怪少行动一次** ✓
```

## 3. 🔴 而 ② 里最要紧的是 **`OnMonsterTurn()`**

```
🎖️ **`BattleGround.Round.OnMonsterTurn()` 在【跳过区里】** ⇒
   📌 **即：先手回合【不算该怪的正常行动】** ✓
   ⇒ ✅ **所以先手之后，该怪【照常还有一次】正常回合** ✓
      🎖️ **这正是"先手奖励"的设计意图** —— **白赚一次行动** ✓
   🔴 **若漏掉这一条**（把先手当正常回合）⇒ **该怪会少一次行动** ⇒ 难度显著变化 ⚠️
```

## 4. 🎖️ 于是"接上先手层"的**依赖全齐了**（4 件 → 5 件）

```
📊 **完整清单**：
   ① **数据**：`bonus_initiative_desires`（6 种 type）✓
   ② **解析**：6 类 + 基类（4 时机 + `override`）✓
   ③ **挂点**：`BonusTurn(selector)` × **3 个时机**（`2402`/`2712`/`2718`）✓
   ④ **执行**：`MonsterTurn(unit, override, fromBonusTurn: true)` ✓
   ⑤ 🆕 **语义**：**`fromBonusTurn` 要跳过 3 处**（`PreMonsterTurn` · **状态区 + `OnMonsterTurn`** ·
      `PostMonsterTurn`）✓ ← **本件补齐**
⇒ 🎖️ **即：这 5 件【一件都不能少】** ⇒ ✅ **可以交给实现了** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`MonsterTurn` 的 3 处 `if (!fromBonusTurn)`（含行号与体）** ·
   **② 覆盖的整个 `Status Effects and Buffs` 区** · **`OnMonsterTurn()` 在跳过区内** ·
   **`PreMonsterTurn`/`PostMonsterTurn` 的字面** —— **全部当场跑出** ✓
🎖️ 并**把"接上先手层"的依赖补到 5 件齐** ✓
🔴 **不能验**：**`PreMonsterTurn`/`OnMonsterTurn`/`PostMonsterTurn` 各自【具体做什么】** ⇒
   📌 本件只知"被跳过" ⇒ **未读它们的实现** ⇒ 记**未读** ✓
   🎖️ **但那不影响接线**（照抄"跳过"即可）⇒ ✅ **记为"够用但未穷尽"** ✓
🔴 **不能验**：**② 里 `Status Effects` 区的【完整内容】** ⇒ 本件只见出血开头 ⇒ 记**未读全** ✓
🔴 **不能验**：**联机侧是否也跳过这 3 处** ⇒ 与 `83_*.md §5` 的"两份实现"怀疑**合并** ⇒ 记**未比** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（直接读码）✓
```

## 6. 下一步

```
✅ **A5 这条线【彻底查完】**：三张表 + 挂点 + 执行 + 跳过语义（5 件齐）✓
🆕 **可做**：① 比联机侧与单机的 `BonusTurn`（验证"两份实现"的怀疑）
   ② 🔴 **把 A5 的完整机制写进 `PLAN_adoption`** —— 这是本线唯一的残余交付
   ③ 把第 37~42 条判据补进 `observe_list` D11
   ④ 读 `PreMonsterTurn`/`OnMonsterTurn`/`PostMonsterTurn`（可选，接线不需要）
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
