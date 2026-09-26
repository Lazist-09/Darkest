# A5 前置查清：**24 个条件键【全部有代码 case】** —— 机制是「硬排除 + 加权抽签」

> 🕒 2026-09-26 · 承接 `79_brains_deep_analysis.md §6` 的"19 个条件键未读代码" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **这是"按参考逻辑对齐"的【真正前置】** ⇒ 本件把它读通了 ✓

---

## 1. 🎖️🎖️ 机制全貌（读 `SkillSelectionDesire.cs` · 178 行）

```
🎖️ **基类** `abstract class SkillSelectionDesire : IProportionValue` ✓
   · **`Chance`**（`IProportionValue` ⇒ **加权抽签的权重**）✓
   · **`restrictions`**：`Dictionary<SkillSelectRestriction, int?>`（**每项可空**）✓

📊 **决策流程**（`SelectSkill(performer, decision)`）：
   ① **`if (IsRestricted(performer)) return false;`** ⇒ 🔴 **先做硬排除** ✓
   ② `availableSkills = monster.Data.CombatSkills.FindAll(IsValidSkill)` ✓
   ③ **`decision.SelectedSkill = availableSkills[RandomSolver.Next(...)]`**
      ⇒ 🎖️ **技能是【等概率随机】选的**（**不是按权重**）⚠️
   ④ `decision.TargetInfo.Targets = GetSkillAvailableTargets(...)` ＋ `RemoveAll(!IsValidTarget)` ✓
   ⑤ **目标侧才用权重**：`availableTargetDesires.FindAll(IsValidTargetDesire)` ⇒
      **`RandomSolver.ChooseByRandom(availableTargetDesires)`** ⇒ 🎖️ **这里才按权重抽** ✓
      · 抽中后 `if (desire.SelectTarget(...)) return true;` ⇒ **失败就换下一个**（while 循环）✓
```

### 🔴🔴 而 `IsRestricted` 是**一整面"排除墙"**（14 个检查）

```
🎖️ **实测原文**（`IsRestricted`）：14 个 `if`，全是同一个形状：
   `if (restrictions[X] != null) if (restrictions[X].Value > BattleGround.Y) return true;`
   ⇒ ✅ **即：条件键是【排除条件】，不是【加分条件】** ✓
   📌 具体 14 个：`MonstersMin/Max` · `MonstersSizeMin/Max` · `MarkedHeroesMin/Max` ·
      `NonVirtuedHeroesMin` · `ControlCountMin/Max` · `HeroesMin` · `VirtuedHeroesMax` ·
      `GuardedMonstersMin/Max` · **`NonDeathsDorrHeroesMin`** ✓
   🔴 而 `NonDeathsDorrHeroesMin` 在**代码里拼错了**（`Dorr` 不是 `Door`）⚠️
      ⇒ 📌 但**数据键是 `non_deaths_door_heroes_min`**（拼对的）⇒ ✅ **只是内部枚举名拼错** ✓
```

## 2. 📊 而那 24 个键**全部有 `case`**（逐个验证）

### ① 基类认 15 个（`ProcessBaseDataToken`）

```
`base_chance` **⇒ `Chance = (int)((double)token.Value * 100)`** ⚠️
   🎖️ **即：数据里的 `1.0` 变成权重 `100`** ⇒ ✅ **与 A5 的"×100 vs ×1"那次更正一致** ✓
`marked_heroes_min/max` · `monsters_min/max` · `monsters_size_min/max` ·
`non_virtued_heroes_min` · `virtued_heroes_max` · **`non_deaths_door_heroes_min`** ·
`control_count_min/max` · `heroes_min` · `guarded_monsters_min/max` ✓
⇒ **15 / 15** ✓
```

### ② 子类认 9 个（逐个查到 `case`）

| 键 | `case` 数 | 在哪个子类 |
|---|---|---|
| `combat_skill_id` | **6** | `AllyAlive` · `AllyDead` · … |
| `ally_base_class_id` | **6** | … ＋ **`BonusInitiativeAllyClassCount`** · **`BonusInitiativeAllyLastDamaged`** |
| `first_initiative_only` | 4 | `AllyDead` · `FillEmptyCaptor` |
| **`can_target_deaths_door`** | 4 | `FillEmptyCaptor` ＋ **`TargetSelectionFillCaptor`** |
| **`can_target_last_hero`** | 4 | 同上 |
| `hp_ratio_treshold` | 2 | `Heal` · `Specific` |
| `per_round_chance` | 1 | **`SkillSelectionSpecific`** |
| `effect_key_status` | 1 | `SkillSelectionStatus` |
| `performing_turn` | 1 | `SkillSelectionPerformingTurn` |

```
⇒ 🎖️ **即：24 个数据键【全部有代码认】** ⇒ ✅ **无一"没人读"** ✓
```

## 3. 🎖️ 而这条**靠 `default` 保证了**

```
🎖️ **基类的 `default` 分支**（原文）：
   `default: Debug.LogError("Unknown token in skill desire: " + token.Key + " Type: " + this);` ✓
⇒ ✅ **即：认不出的键会【响亮报错】** ⇒ 🔴 **不会静默吞掉** ✓
   📌 所以"24 个键全部有 case"这个结论**不是巧合** —— 若有没认的，原版就会报错 ✓
   🎖️ **判据（第 34 条）**：**"这个解析器遇到未知键会【报错】还是【静默】？"
      · 报错 ⇒ ✅ "数据里的键都被认了"可以【反推】✓
      · 静默 ⇒ ⚠️ 必须逐个查 ✓
      📌 与 `nothing` 那次（"有无 `default`"决定缺失是语义还是缺陷）**同族** ✓
```

## 4. 🔴 所以"按参考逻辑对齐"要做的**三件事**（本件定出）

```
① 🎖️ **机制要对齐的是【两层】**：
   · **技能层** ⇒ **硬排除（14 个条件）＋ 等概率随机**（**不是加权！**）⚠️
   · **目标层** ⇒ **硬排除 ＋ 加权抽签（`ChooseByRandom`）** ✓
   ⇒ 📌 **即：参考【技能不按权重、目标才按权重】** ⇒ ⚠️ **这个不对称很容易抄错** ✓
      🎖️ **判据（第 35 条）**：**"同一套'欲望'里，【技能】和【目标】用的是【同一种抽取】吗？"**
         ⇒ **不是** ⇒ ⚠️ **不能统一处理** ✓

② 🔴 **我方缺的**（`79_*.md` 已记）：`skill_cooldowns` · `bonus_initiative_desires` ✓
   ＋ 📌 **而我方的 `when` 条件与我方 `rules[].mark_weight`** 是**另一套切法** ✓
      🎖️ **注意**：我方的 `mark_weight` **只用在目标侧** ⇒ ✅ **与参考的"目标侧加权"【同侧】** ✓
         而我方**技能侧**是 `when` 布尔 ⇒ ✅ **与参考的"技能侧硬排除"【同侧】** ✓
         ⇒ 🎖️ **即：两侧的【分工】其实一致** —— **只是表达方式与粒度不同** ✓
         📌 这是本件**最重要的发现**：**不是"两套完全无关的机制"，是"同分工的两种表达"** ✓

③ 🔴 **`base_chance` 要乘 100**（`(int)(value * 100)`）⇒ ✅ **一条必须照抄的换算** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`SkillSelectionDesire.cs` 的 178 行原文** · **决策流程 5 步** ·
   **`IsRestricted` 的 14 个检查** · **`base_chance` 的 `* 100` 换算** ·
   **15 个基类键 + 9 个子类键的逐个 `case`（含文件与行号）** ·
   **`default` 会 `LogError`** —— **全部当场跑出** ✓
🎖️ 并**把"两侧分工其实一致"这个发现量了出来**（此前只记"两套机制"）✓
🔴 **不能验**：**`TargetSelectionDesire.cs` 的 `IsRestricted`** 未逐行读（只读了技能侧）⇒
   📌 **目标侧的排除条件数【未核】** ⇒ 记**未读** ✓
🔴 **不能验**：**`bonus_initiative_desires` 的 6 种 type 消费代码** ⇒ 记**未读** ✓
🔴 **不能验**：**`NonDeathsDorrHeroesMin` 的拼写错是否有别处引用** ⇒ 记**未核** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{ai_code,desire_base,key_cover,sub_keys}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **A5 的前置【读通】**：24 键全有 case · 机制是"硬排除 + 加权抽签" · 两侧分工一致 ✓
🆕 **可做**：① 🔴 **读 `TargetSelectionDesire.cs`**（目标侧的排除墙 —— 补上另一半）
   ② 读 `bonus_initiative_desires` 的 6 种 type 消费代码
   ③ 把第 34/35 条判据（解析器静默否 · 技能与目标是否同一种抽取）补进 `observe_list` ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
