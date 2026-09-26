# `4880` 那处**不是第 5 个 DoT 结算点** —— 是**营火事件的延迟效果队列**

> 🕒 2026-09-26 · 承接 `98_*.md §6①` 的"可能是第 5 个结算点" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 判决：**不是**（它是 `#region Effects`，处理**事件队列**）

```
📊 **原文**（`RaidSceneManager.cs:4876-4911`）：
   `4876| #region **Effects**`
   `4877| do {`
   `4879|     executedEvent = false;`
   `4880|     UnitEventQueue.Clear();`
   `4881|     UnitEventQueue.AddRange(BattleGround.HeroParty.Units);`
   `4882|     **if (includeMonsters)**                                   // 🔴 有开关
   `4883|         UnitEventQueue.AddRange(BattleGround.MonsterParty.Units);`
   `4885|     while (UnitEventQueue.Count > 0) {`
   `4887|         var eventUnit = UnitEventQueue[0];`
   `4888|         UnitEventQueue.Remove(eventUnit);`
   `4890|         if (eventUnit != null && **eventUnit.EventQueue.Count > 0**) {`
   `4892|             var eventEffect = eventUnit.EventQueue[0];`
   `4893|             eventUnit.EventQueue.RemoveAt(0);`
   `4895|             **eventEffect.Execute()**;                        // 🔴 执行的是【Effect】
   `4897|             if (eventEffect.SubEffect is **StressEffect** || …**StressHealEffect**) …
   `4900|             if (eventUnit.CombatInfo.**MarkedForDeath**) { … ExecuteDeath(eventUnit); }`
   `4908|     if (executedEvent) yield return new WaitForSeconds(1f);`
   `4910| } while (executedEvent);`
⇒ 🎖️ **即：它处理的是【各单位 `EventQueue` 里的延迟 Effect】** ✓
   🔴 **`DotPoison`/`dotBleed` 的 `CurrentTickDamage`【完全没有出现】** ⇒ **不是 DoT 结算点** ✓
   📌 **而 `includeMonsters` 是参数**（不是"漏了怪物"）⇒ ✅ **`98_*.md` 的怀疑澄清** ✓
```

## 2. 🎖️ 而它揭示了**另一层机制**：**延迟效果队列**（`EventQueue`）

```
📊 **每个单位有 `EventQueue`**（`eventUnit.EventQueue[0]` ⇒ `RemoveAt(0)` ⇒ `Execute()`）✓
   ＋ **`do { … } while (executedEvent)`** ⇒ 🔴 **重复扫，直到没有新事件** ✓
      🎖️ **因为 `Execute()` 可能【再往队列里加事件】** ⇒ ✅ **是"连锁触发"模式** ✓
   ＋ **`StressEffect`/`StressHealEffect` 会 `yield WaitForSeconds(0.25f)`**（**演出节奏**）✓
   ＋ **`MarkedForDeath` ⇒ `IsDead = true` + `ExecuteDeath`** ⇒ 🔴 **死亡可被延迟到这一步** ✓
⇒ 🎖️ **判据（第 80 条）**：**"这个队列是【一次性处理】还是【循环到不动】？
   ⇒ `do…while(executedEvent)` ⇒ 它是【连锁触发】，因为 `Execute()` 会再入队"** ✓
```

## 3. 🎖️ 所以 DoT 的结算点**确认为 4 个**（本件的收官）

| # | 行 | 单位 | 场合 |
|---|---|---|---|
| ① | 2817/2835 | 英雄 | 英雄自己的回合 |
| ② | 3448/3470 | 怪物 | 怪物自己的回合 |
| ③ | 2729/2748 | `idleUnit` | 回合末补偿（×1.5）|
| ④ | 4683/4721 | 英雄 | 行走中（`ExecuteRoundAdvance`）|
| ~~⑤~~ | ~~4880~~ | — | 🔴 **不是**（是营火事件队列）✓ |

```
⇒ ✅ **即：`98_*.md` 的"可能第 5 个"【排除】** ⇒ **确认 4 个** ✓
   🎖️ **判据（第 81 条）**：**"我怀疑的第 N 个 —— 它处理的是【同一个量】吗？
      ⇒ 看有没有那个量的字段名（`CurrentTickDamage`）"** ✓
      📌 **本件靠"搜不到 `CurrentTickDamage`"直接排除** ✓
```

## 4. 🔴 而本件**又纠了一次自己**（第 32 次同族）

```
🔴 `98_*.md §4` 我写：**"`4880-4888` ⇒ 另一处清空+装入 ⇒ ⚠️ 另一处场合"**
   并**标为"可能的第 5 个结算点"** ⚠️
✅ **实测**：那是 **`#region Effects`**（**事件队列**），与 DoT **无关** ✓
   🔴 **我的错因**：**只按"`UnitEventQueue` 被重建"就怀疑**，
      **没看它【接着处理什么】** ✓
   🎖️ **判据（第 82 条）**：**"我看到一段相似的代码 —— 它是【同一件事】还是【同一个容器】？
      ⇒ 相似的是【容器】，不一定相似【用途】"** ✓
      📌 与第 43 条（"两份实现还是一份被复制的"）· 第 66 条（"提到 vs 解析"）
         **同族：**把"表面相似"当成"实质相同"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`4876-4911` 的逐行原文（含 `#region Effects` 与 `includeMonsters`）** ·
   **`CurrentTickDamage` 在那一区【完全没出现】** · **`do…while(executedEvent)` 的连锁模式** ·
   **`StressEffect` 的 0.25s 演出** · **`MarkedForDeath` 的延迟死亡** ·
   **4 个结算点的最终表** —— **全部当场跑出** ✓
🎖️ 并**排除了"第 5 个"**、**纠了自己的怀疑** ✓
🔴 **不能验**：**`eventUnit.EventQueue` 里装的 Effect【从哪来】** ⇒ 记**未读** ✓
   📌 **而那是"延迟效果"这一层的入口** ⇒ ⚠️ **与 A6a 的"效果"相关** ⇒ 记**待办** ✓
🔴 **不能验**：**`includeMonsters` 由谁传**（true/false 的场合）⇒ 记**未读** ✓
🔴 **不能验**：**`ProcessDamage` 的实现**（前几件已记）⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/site5.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **DoT 结算点【确认 4 个】**（排除第 5 个的可能）✓
🆕 **新暴露一层**：**`eventUnit.EventQueue`（延迟效果队列）** —— 与 A6a 的 Effect 相关
🆕 **可做**：① 🔴 **读 `EventQueue` 的入队点**（"延迟效果"这层的入口）
   ② 读 `ProcessDamage`（DoT 伤害落地时做不做死门判定）
   ③ 把第 67~82 条判据补进 `observe_list` D11
   ④ **把 DoT 的 4 个结算点写进 `PLAN_adoption`**（A6a 行）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
