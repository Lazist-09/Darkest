# 融合触发条件的**全部三条路径查清**：① 无 · ② 结构上不可能 · ③ **可能** ⇒ 结论回到"要看设计"

> 🕒 2026-09-26 · 工具 `tools/dsh/check_two_stress_paths.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `103_*.md §6①` 的"同一队列会不会有两条 stress"** ✓

---

## 1. 📊 三条路径逐个查（**判据第 97 条**）

```
🎖️ **判据（第 97 条）**：**"这个运行时状态由【哪几条路径】产生？
   ⇒ 要逐条路径查，不是只看一条"** ✓
```

| 路径 | 机制 | 实测 |
|---|---|---|
| **① 一条技能带多个 effect** | 技能的 `combat_skill` 块里引用 ≥2 个带 stress 的 Effect | 🔴 **0 处**（查了 `Heroes/Info/*` 与 `Monsters/*`）✓ |
| **② 同一技能打多目标** | 各目标有**各自的** `EventQueue` ⇒ **不同单位** | ✅ **结构上不可能融合** ✓ |
| **③ 两条技能先后入队**（同回合内）| 若两次施加**之间没有执行**，就同在一条队列里 | 🔴 **可能** ⇒ 见 §2 ✓ |

```
📊 **① 的实测**：带 `stress`/`healstress` 的 effect 共 **103** 个 ✓
   逐技能块（`combat_skill:`）扫其 `.effect "X"` ⇒ **没有一条技能引用 ≥2 个这类 Effect** ✓
   ⇒ ✅ **路径①不产生** ✓
```

## 2. 🔴 而**路径③ 是"可能"的** —— 关键在 `StackEvents()` 的**调用时机**

```
📊 **原文**（`RaidSceneManager.cs:4807`）：
   ```csharp
   protected virtual IEnumerator **ExecuteEffectEvents**(bool includeMonsters, float waitAfter = 0.0f)
   {
       executingEffectEvent = true;
       for (…) BattleGround.HeroParty.Units[i].**StackEvents()**;        // 🔴 英雄先合并
       if (includeMonsters)
           for (…) BattleGround.MonsterParty.Units[i].**StackEvents()**;  // 🔴 怪物再合并
       do { … 逐个 eventUnit.EventQueue[0].Execute() … } while (executedEvent);
   }
   ⇒ 🎖️ **即：`StackEvents()` 在【执行前】调用一次** ⇒
      **它合并的是"上次执行以来累积的所有事件"** ✓
   🔴 **而 `ExecuteEffectEvents` 是【被调用的】**（不是每加一条就调）⇒
      若**两次加事件之间没有调用** ⇒ ✅ **它们同在一条队列里 ⇒ 融合** ✓
```

### 🎖️ 而现场证据：`4790-4793` 就是这种模式

```
   `4790| for (int i = 0; i < barkStressEffect.SubEffects.Count; i++)`
   `4791|     barkStressEffect.SubEffects[i].**Apply**(heroBarker, barkTarget, barkStressEffect);`
   `4792| yield return new WaitForSeconds(0.1f);`
   `4793| yield return StartCoroutine(**ExecuteEffectEvents**(false));`
⇒ 🎖️ **即：先把 `SubEffects` **全部 `Apply`**（逐条入队），再【一次性】执行** ✓
   📌 **所以同一单位若收到两条 stress ⇒ 它们确实会同时躺在队列里** ✓
   🔴 **而 `barkStressEffect.SubEffects.Count`** 暗示**可以有多个 SubEffect** ⚠️
      ⇒ 📌 但那来自**同一个 effect**（路径①）⇒ **而路径①实测为 0** ✓
```

## 3. 🎖️ 所以"融合要不要实现"的**最终答案**

```
📊 **三条路径的判定**：
   · **① 无**（数据保证）✓
   · **② 不可能**（结构保证）✓
   · **③ 可能** ⇒ ℹ️ **取决于"同回合内是否有两次施加之间不执行"** ✓
⇒ 🎖️ **即：融合是【可达的】，但不是【必然发生】** ✓
   📌 **判据（第 98 条）**：**"我说到这个机制'可能触发' ——
      那我要不要实现它？⇒ 看它【可达】还是【必然】：
      可达 ⇒ 实现（否则是隐藏 bug）；必然 ⇒ 实现（否则主线断）"** ✓
      ⇒ ✅ **结论：应当实现**（代码量小：`StackEvents` 20 行 + 2 个子类的方法）✓
```

## 4. 🔴 并更正 `102_*.md` 的**第三层**（本轮的连续更正）

```
📊 **三轮的演进**：
   · `101_*.md` ⇒ **"2/29 子类 ⇒ 个别特例"** ✓（**仍成立**）
   · `102_*.md` ⇒ 🔴 **"数据 0 条多条 stress ⇒ 不触发 ⇒ 预留机制"** ⚠️
   · `103_*.md` ⇒ 🔴 **"按 `Type` 配对 ⇒ 跨 effect 也融合 ⇒ `102` 结论下早了"** ✓
   · **本件** ⇒ ✅ **"三条路径里 ③ 可达 ⇒ 应当实现"** ✓
⇒ 🎖️ **判据（第 99 条）**：**"我连续更正了同一个结论 ——
      每次更正的【原因】是什么？⇒ 记下来，它是判断力的来源"** ✓
      · `102` 错在：**只查"数据形状"，未查"运行时状态"** ✓
      · 本件补上：**查了三条路径，并定"可达 vs 必然"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**103 个带 stress 的 effect** · **路径①逐技能块扫 ⇒ 0 处** ·
   **路径②的结构论证（各目标各自队列）** ·
   **`StackEvents()` 在 `ExecuteEffectEvents` 开头调用（`4813`/`4816`）** ·
   **`4790-4793` 的"先全 Apply 再执行"现场** —— **全部当场跑出** ✓
🎖️ 并**把三条触发路径【逐条查完】**，定出"可达 vs 必然" ✓
🔴 **不能验**：**路径③ 在真实战斗里【多久发生一次】** ⇒
   📌 那要跑战斗或读完整流程 ⇒ 记**未量化** ✓
   🎖️ **但"可达"已足够支持"应当实现"** ⇒ ✅ **结论不依赖该量化** ✓
🔴 **不能验**：**`ExecuteEffectEvents` 的全部调用点**（有几处、间隔多长）⇒ 记**未读全** ✓
🔴 **不能验**：**`barkStressEffect` 的 `SubEffects` 来自哪条 effect** ⇒
   📌 它应有 ≥1 个 SubEffect ⇒ 与路径①的"0 处"**不矛盾**（那是"≥2 个带 stress 的"）✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/stack_calls.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **融合的三条触发路径【查完】**：① 无 · ② 不可能 · ③ 可达 ⇒ **应当实现** ✓
🆕 **可做**：① 把第 83~99 条判据补进 `observe_list` D11（**本段 17 条**）
   ② **把 `queue` + 融合（按 Type 配对）+ A6a 必做集写进 `PLAN_adoption`** ✓
   ③ 读 `ExecuteEffectEvents` 的全部调用点（量化路径③）
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
