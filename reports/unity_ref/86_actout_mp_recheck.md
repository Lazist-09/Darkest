# 联机 act-out **重核**：11 个 case 名称相同，但 **① `RandomCommand` 是空桩 ② 反应只有 1/21**

> 🕒 2026-09-26 · 承接 `36_actout_multiplayer.md`（那次只比了名称与守卫）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **本件把"联机侧"从"名称相同"推进到【体与覆盖面】** ✓

---

## 1. ✅ 名称与守卫：**与 `36_*.md` 一致**（复核通过）

```
📊 **两边都是 11 个 case，集合完全相同** ✓
   `AttackSelf` `BarkStress` `BuffAlly` `BuffParty` `ChangePosition` `HealSelf`
   `IgnoreCommand` `MarkSelf` `RandomCommand` `StressHealParty` `StressHealSelf` ✓
```

## 2. 🔴🔴 而**体**不同：`RandomCommand` 在联机里是**空桩**

```
🔴 **联机**（`RaidSceneMultiplayerManager.cs:1508-1509`）：
   ```csharp
   case StartTurnActType.RandomCommand:
       break;                                  // 🔴 【2 行，什么都不做】⚠️
   ```
🎖️ **单机**（`RaidSceneManager.cs:3081` 起，**36 行**）：
   `yield WaitForSeconds(1f);`
   `var brainDesicion = **BattleSolver.UseMonsterBrain(actionUnit)**;`   ← 🔴 **真正的"随机行动"**
   `Formations.ShowUnitOverlay();`
   `BattleGround.Round.OnHeroTurn();`
   `if (brainDesicion.Decision == BrainDecisionType.Pass) { … PopupMessage(… Pass); }` ✓
⇒ 🔴 **即：联机的 `RandomCommand` 【不执行随机行动】** ⚠️
   📌 而**它是 24 种 act-out 里"可照抄"的那一档**（`31_*.md` 实测有真分支）✓
   ⇒ ⚠️ **采用时若抄联机那份 ⇒ `RandomCommand` 会变成空操作** ✓
   🎖️ **判据（第 45 条）**：**"两边 case【名】相同 —— 那【体】呢？
      ⇒ 逐 case 算行跨度，2 行的那个就是桩"** ✓
```

## 3. 🎖️ 逐 case **行跨度对照**（体量的读数）

| case | 单机（`31_*.md`）| 联机（本件）|
|---|---|---|
| `AttackSelf` | 49 | **50** ✓ |
| `ChangePosition` | 39 | **38** ✓ |
| 🔴 **`RandomCommand`** | **36** | 🔴 **2** ⚠️ |
| `BarkStress` / `BuffAlly` | 16 | **16** ✓ |
| `HealSelf` | 16 | **18** ✓（已知有差异）|
| `BuffParty` | 14 | **14** ✓ |
| `StressHealParty` | 13 | **13** ✓ |
| `MarkSelf` / `StressHealSelf` | 10 | **10** ✓ |
| `IgnoreCommand` | 7 | **8** ✓ |

```
🎖️ **即：9/11 的体量【几乎一致】（±1~2 行 = 缩进差）** ⇒ ✅ **确实是复制品** ✓
   🔴 **而 `RandomCommand` 是唯一【数量级不同】的** ⇒ **它是空桩** ✓
   📌 **而 `StressHealSelf` 显示 120 行是【我的算法错】** —— 它是最后一个 case，
      后面接的是 `switch` 收尾与**联机特有的 `while(true)` 同步循环**（L1539 起）⚠️
      ⇒ 🎖️ **判据（第 46 条）**：**"最后一个 case 的行跨度别信 —— 它后面可能是【方法剩余部分】"** ✓
         📌 与 `32_*.md` 那次（"`StressHealSelf` 90 行"实为兜底值）**同族**：**同一个坑第 2 次** ✓
```

## 4. 🔴 而**覆盖面**差得更多：反应只有 **1 / 21**

```
📊 **`ReactionType.` 的出现次数**：
   · **单机**：**21 处**（`BlockMove` 2 · `BlockHeal` 1 · `BlockBuff` 1 ·
     `BlockItem` **5** · `CommentSelfHit/Missed` 各 1 · `CommentAllyHit/Missed` 各 1 ·
     `CommentAllyAttackHit/Miss` 各 1 · `CommentMove` 2 · `CommentCurioInteraction` 2 ·
     `CommentTrapTriggered` 2）✓
   · 🔴 **联机**：**1 处** —— 只有 **`BlockMove`**（`L1669`）✓
⇒ 🔴 **即：联机【只实现了 1 个反应 act-out】**（`BlockMove`）⚠️
   📌 而 `30_*.md` 实测单机侧 **13/15 有消费** ⇒ ⚠️ **联机侧只有 1/15** ✓
   🎖️ **判据（第 47 条）**：**"我比了两边，【一边 21 处一边 1 处】——
      是【实现深度不同】还是【我漏搜了】？"** ⇒ ✅ **看有没有别的写法**（本件确认无）✓
```

## 5. 🎖️ 于是"联机侧"的**完整结论**

```
📊 **联机 = 单机的【部分复制】**：
   · **11 个回合开始 act-out** ⇒ ✅ **9/11 体量一致**（复制品）✓
     🔴 但 **`RandomCommand` 是空桩** ⚠️
   · 🔴 **15 个反应 act-out** ⇒ **只有 1 个**（`BlockMove`）⚠️
   · 🔴 **`BonusTurn`** ⇒ **3 份复制，且都漏传 `true`**（`85_*.md`）⚠️
   · ✅ **`MonsterTurn` 是薄包装**（`PreparationCheck` + 透传）✓
⇒ 🎖️ **即：联机侧是"为联网改写的部分副本"** ——
   **它在【回合开始】上基本忠实，在【反应】与【先手】上不完整** ✓
   📌 **判据（第 48 条）**：**"一份'部分复制'—— 哪部分忠实、哪部分残缺？
      ⇒ 用【逐 case 体量】与【符号计数】两个尺子量"** ✓
```

## 6. 诚实边界

```
✅ **能验**：**两边 11 个 case 名称集合相同** · **逐 case 行跨度（9/11 ±1~2 行）** ·
   **`RandomCommand` 联机 2 行 vs 单机 36 行（两边原文）** ·
   **`ReactionType.` 单机 21 处 vs 联机 1 处（逐 kind 计数）** ·
   **`StressHealSelf` 的 120 行是算法错（后面是方法剩余）** —— **全部当场跑出** ✓
🎖️ 并**把联机侧从"名称相同"推进到"体与覆盖面"** ✓
🔴 **不能验**：**联机的 `RandomCommand` 空桩是【刻意】还是【未完成】** ⇒ 记**未判** ✓
   📌 但"单机那 36 行用了 `UseMonsterBrain`"⇒ 联机可能**无法在主客户端跑 brain** ⇒
      记为**可能的刻意原因（推断）** ✓
🔴 **不能验**：**联机的 1 个 `BlockMove` 是否为联网所需的最小集** ⇒ 记**未判** ✓
🔴 **不能验**：**联机侧是否还有【别处】实现反应**（如别的文件）⇒
   📌 本件只查了 `RaidSceneMultiplayerManager` ⇒ 记**未全盘查** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/actout_mp{2,3,4,5}.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **联机 vs 单机【三类都比完】**：act-out（9/11 忠实 · 1 空桩 · 反应 1/21）· BonusTurn（漏参）✓
🆕 **一条要报策划的**：🔴 **联机侧是"部分复制"，采用时必须【取单机那份】** ✓
🆕 **可做**：① 全盘查联机侧还有没有别的反应实现（关掉 §6 的口子）
   ② 把第 43~48 条判据补进 `observe_list` D11
   ③ **把"采用时一律取单机实现"写进 `PLAN_adoption`** —— 这是本轮的通用结论 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
