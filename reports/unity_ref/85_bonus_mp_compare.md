# 联机 vs 单机 `BonusTurn`：**我的"两份实现"怀疑被推翻** —— 是**复制品少了第三个参数**

> 🕒 2026-09-26 · 工具 `tools/dsh/compare_bonus_mp.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `83_*.md §5` 的"联机 3 处怀疑"** ✓

---

## 1. 🔴 我的怀疑**方向错了**（第 29 次同族）

```
🔴 `83_*.md §5` 我怀疑：**"联机侧又是【两份实现】"**（与 `36_actout_multiplayer.md` 同族）⚠️
✅ **实测：不是** ✓
   · **联机【没有自己的 `BonusTurn`】**
     （`BonusTurn` 在联机里只出现 **2 次** —— 都是**调用**，无定义）
   · **联机【没有重写 `CheckBonusInitiative` 的遍历逻辑】** ——
     🔴 **它把单机那段 `while(desires…)` 【复制】了三份进来**（L638/946/979）⚠️
   ⇒ 📌 **即：不是"两份实现"，是"一段逻辑被复制了三遍到另一个文件"** ✓
      🎖️ **判据（第 43 条）**：**"这是【两份实现】还是【一份被复制的】？
         —— 看有没有【独立的定义】（`protected virtual` 的 override）"** ✓
         · **有 override** ⇒ 两份实现（如 `MonsterTurn`）✓
         · **只是复制了调用方** ⇒ 一份逻辑、多个拷贝点 ⚠️
```

## 2. 🎖️ 而**真差异只有两处**（逐项比出来的）

### ① 🔴🔴 **联机漏了第三个参数 `true`**（**行为不同**）

```
单机（`RaidSceneManager.cs:3543`）：
   `MonsterTurn(monsterUnit, currentDesire.CombatSkillOverride, **true**)` ✓
联机（`RaidSceneMultiplayerManager.cs:646/955/987`）：
   `MonsterTurn(monsterUnit, currentDesire.CombatSkillOverride)`         🔴 **少了 `true`** ⚠️
```
```
🎖️ 而签名是 `MonsterTurn(unit, override = null, fromBonusTurn = **false**)` ⇒
   🔴 **联机走的是 `fromBonusTurn = false`** ⇒ **不跳过那 3 处** ✓
⇒ 🎖️ **即：同一只怪的先手奖励【单机与联机行为不同】** ⚠️

| | 单机 | 联机 |
|---|---|---|
| `PreMonsterTurn` | 🔴 **跳过** | ✅ **执行** |
| **状态效果（DoT）** | 🔴 **跳过**（不出血/不中毒）| ✅ **执行** |
| **`OnMonsterTurn()`** | 🔴 **跳过**（**不记"已行动"**）| ✅ **执行**（**记为已行动**）|
| `PostMonsterTurn` | 🔴 跳过 | ✅ 执行 |

⇒ 🔴 **后果**：**联机的先手是"完整回合"** ⇒ ⚠️ **它会吃掉该怪的正常回合**，
   而且**会多掉一次 DoT** ✓
   📌 **与 `84_*.md` 的"轻量回合 = 白赚一次行动"【正好相反】** ✓
```

### ② 🎖️ 联机**只有一处**多了 `WaitForOneTwo`（L954）

```
联机的 `IsPostTurn && IsRoundInProgress` 那一处：
   `if (currentDesire.CheckBonusInitiative(monsterUnit)) {`
   `    yield return **WaitForOneTwo**;                        // 🔴 只有这一处有`
   `    yield return StartCoroutine(MonsterTurn(...));`
   `    break; }` ✓
⇒ 🎖️ **即：联机在"回合中·回合后"那个时机加了【同步等待】**（网络同步需要）✓
   📌 而另两处（`IsRoundStart` L646 · `IsRoundFinish` L987）**没有** ⚠️
      ⇒ ⚠️ **可能是刻意，也可能是漏了** ⇒ 记**未判** ✓
```

## 3. 🎖️ 而 `MonsterTurn` **确实是 override**（那是真·两份实现）

```
🎖️ **联机 `MonsterTurn`**（`RaidSceneMultiplayerManager.cs:1785`）：
   ```csharp
   protected override IEnumerator MonsterTurn(FormationUnit actionUnit,
       string combatSkillOverride = null, bool fromBonusTurn = false)
   {
       yield return StartCoroutine(PhotonGameManager.PreparationCheck());   // ← 联机特有
       yield return StartCoroutine(base.MonsterTurn(actionUnit, combatSkillOverride, fromBonusTurn));
   }
   ```
⇒ ✅ **即：联机的 `MonsterTurn` 是【薄包装】** ——
   **加一次网络准备检查，然后 `fromBonusTurn` 被【如实透传】** ✓
   ⇒ 📌 **所以 §2① 的差异【不是 `MonsterTurn` 的锅】，是【调用方没传 `true`】** ✓
      🎖️ **判据（第 44 条）**：**"差异在【被调方】还是【调用方】？
         —— 被调方如实透传 ⇒ 那就是调用方的参数漏了"** ✓
```

## 4. 🎖️ 所以"采用时抄哪一份"—**答案明确了**

```
✅ **按用户指令"采用参考"** ⇒ 参考**单机那份**（`true`）**才是【本意】** ✓
   理由：
   ① 单机那份与 `84_*.md` 的"轻量回合 = 白赚一次行动"**语义自洽** ✓
   ② 联机那份会让先手**吃掉正常回合** ⇒ ⚠️ **与"奖励"的命名矛盾** ✓
   ③ 而 `MonsterTurn` 的 `fromBonusTurn` **默认 `false`** ⇒ 联机是**依赖默认值**
      ⇒ 📌 **典型的"漏传参"，不是"刻意的另一种设计"** ✓
⇒ 🎖️ **判据**：**"两份不同 —— 哪份是【本意】？看它【与命名/注释/其它调用】是否自洽"** ✓
   📌 **而这一条要报给策划**（因为它是参考自身的一个 bug，采用时不该照抄）✓
```

## 5. 诚实边界

```
✅ **能验**：**单机 1 处带 `true` · 联机 3 处不带** · **标签计数的差异表** ·
   **联机无 `BonusTurn` 定义（只 2 处调用）** · **`MonsterTurn` override 的薄包装原文** ·
   **`WaitForOneTwo` 只在 1 处** —— **全部当场跑出** ✓
🎖️ 并**推翻了我自己"两份实现"的怀疑**，且**把差异缩到确切的 2 处** ✓
🔴 **不能验**：**联机漏传 `true` 是【刻意】还是【笔误】** ⇒ 无 commit ⇒ 记**未判** ✓
   📌 但 §4 的三条理由**指向笔误** ⇒ 记**倾向笔误（推断）** ✓
🔴 **不能验**：**`WaitForOneTwo` 只加一处是否有意** ⇒ 记**未判** ✓
🔴 **不能验**：**联机的"完整回合"先手是否【真的会发生】**（联机模式可能不走该路径）⇒
   📌 记**未实跑** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/mp_diff.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **联机 vs 单机【逐项比完】**：非两份实现 · 真差异 2 处（漏 `true` · `WaitForOneTwo`）✓
🆕 **一条要报策划的**：🔴 **参考自身有一处"漏传参"**（联机先手变完整回合）
   ⇒ 采用时**取单机那份** ✓
🆕 **可做**：① 用**同法**核联机的 `act-out` switch（`36_*.md` 那次只读了单机 side 的差异）
   ② 把第 43/44 条判据补进 `observe_list` D11
   ③ **把这条差别写进 `PLAN_adoption` 的 A5 行**（含"取单机那份"的裁定依据）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
