# DoT 4 个结算点**全齐**：`ExecuteRoundAdvance` 是**行走中的 DoT**（只对【英雄】）

> 🕒 2026-09-26 · 承接 `97_*.md §6①` 的"`4683`/`4721` 场合未读" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 答案：它在 **`ExecuteRoundAdvance()`** 里 —— **回合推进时的 DoT**

```
📊 **原文**（`RaidSceneManager.cs`）：
   `4653| protected virtual IEnumerator **ExecuteRoundAdvance**()`
   `4657|     while (UnitEventQueue.Count > 0) yield return null;`
   `4660|     **UnitEventQueue.AddRange(Formations.Heroes.Party.Units);**   // 🔴 只加【英雄】⚠️
   `4669|     #region Bleeding`
   `4671|     for (int i = UnitEventQueue.Count - 1; i >= 0; i--) {`
   `4673|         if (…IsDead) continue;`
   `4676|         if (!…StatusType.Bleeding).IsApplied) continue;`
   `4683|         if (ProcessDamage(UnitEventQueue[i], bleedEffect.CurrentTickDamage)) {`
   `4685|             if (PartyController.**MovementAllowed**) {`          // 🔴 与【移动】相关
   `4687|                 movementStopped = true; DisablePartyMovement();`
   `4690|             bleedDeaths.Add(UnitEventQueue[i]); } }`
   `4707|     #region Poisoning` …（同形）
⇒ 🎖️ **即：这是"回合推进（走一步）时"的 DoT 结算** ✓
   📌 证据：`MovementAllowed` / `DisablePartyMovement()` ⇒ **与队伍移动绑定** ✓
   🔴 **而它只 `AddRange(Formations.Heroes.Party.Units)`** ⇒ **只结算英雄** ⚠️
```

## 2. 🎖️🎖️ 所以 **4 个结算点全齐**（本件的收官）

| # | 行 | 单位 | 算法 | **场合** |
|---|---|---|---|---|
| ① | **2817**/2835 | **英雄** | `ProcessDamage` ×1.0 | **英雄自己的回合**（`HeroTurn`）|
| ② | **3448**/3470 | **怪物** | `ProcessDamage` ×1.0 | **怪物自己的回合**（`MonsterTurn` · `!fromBonusTurn` 区）|
| ③ | **2729**/2748 | **`idleUnit`**（先攻 0）| `TakeDamage(CeilToInt(× **1.5f**))` | **回合末补偿**（`Idle units status effects`）|
| ④ | **4683**/4721 | **英雄**（`UnitEventQueue`）| `ProcessDamage` ×1.0 | 🔴 **行走中**（`ExecuteRoundAdvance`）|

```
⇒ 🎖️ **规律（完整）**：
   · **只有 ③ 用 `TakeDamage` + ×1.5** ⇒ **它是唯一的补偿结算** ✓
   · **只有 ④ 的队列【只含英雄】** ⇒ ⚠️ **怪物在行走中不结算 DoT** ✓
   · ①②④ 都是 **`ProcessDamage` ×1.0** ✓
   🎖️ **判据（第 74 条已立，本件补全）**：**"4 个结算点里哪一个不一样？
      ⇒ ③ 特殊在"没有自己的回合"· ④ 特殊在"只对英雄""** ✓
```

## 3. 🎖️ 而这两个"特殊"各有**机制解释**

```
📊 **③ 为什么补偿 `idleUnit`**（`97_*.md` 已证）：
   它们是 **`NumberOfTurns == 0` 的【非行动单位】**（大炮/大锅/尸体，**88/438 = 20%**）⇒
   **没有回合 ⇒ 必须由回合末代结算** ✓
📊 **④ 为什么只含英雄**：
   `ExecuteRoundAdvance` 是**队伍【走一步】**时触发的 ⇒
   📌 **只有英雄在地图上"移动"**（怪物没有地图位置）⇒ ✅ **合理** ✓
   🎖️ **判据（第 78 条）**：**"这个队列只加了【一方】—— 是【漏了】还是【另一方本来就不在这里】？
      ⇒ 看这个场合的单位【有没有该能力（如移动）】"** ✓
```

## 4. 🎖️ 顺带：`UnitEventQueue` 的**完整生命周期**

```
📊 **它的 30 处引用**：
   · **`104`** `protected readonly List<FormationUnit> UnitEventQueue = new List<FormationUnit>(**8**);`
     ⇒ 🔴 **容量 8**（英雄最多 4 · 但队列会被复用）✓
   · **`1559`/`1723`/`1740`/`1753`/`1784`/`1804`** ⇒ **6 处 `RemoveAll(item => item == targetUnit)`**
     ⇒ 🎖️ **即：它是"待处理单位"队列，某单位处理完就移出** ✓
   · **`4660`/`4701`/`4739`** ⇒ **`AddRange(Heroes)`**（三次，**逐段重建**）✓
   · **`4880-4888`** ⇒ **另一处清空+装入**（`HeroParty` + `MonsterParty`）⇒ ⚠️ **另一处场合** ✓
   · **`4772`/`4880`** ⇒ `Clear()` ✓
⇒ 🎖️ **判据（第 79 条）**：**"这个容器在【几处被重建】？
   ⇒ 多处置 ⇒ 它是【复用的临时表】，每个场合各自装填"** ✓
   📌 **而它的容量 `8` 与"6 处 RemoveAll"暗示它被【反复重用】** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`ExecuteRoundAdvance` 的签名与体内 DoT（逐行）** ·
   **`AddRange(Formations.Heroes.Party.Units)` 只含英雄** ·
   **`MovementAllowed`/`DisablePartyMovement` 的移动绑定** ·
   **4 个结算点的完整表** · **`UnitEventQueue` 的 30 处与容量 8** ——
   **全部当场跑出** ✓
🎖️ 并**把 DoT 的 4 个结算点【全部查清】** ✓
🔴 **不能验**：**`ExecuteRoundAdvance` 被谁调用**（哪一步触发"走一步"）⇒ 记**未读** ✓
🔴 **不能验**：**`4880` 那处（`HeroParty` + `MonsterParty`）是什么场合** ⇒ 记**未读** ✓
   📌 它与 ④ 不同（**含怪物**）⇒ ⚠️ **可能是第 5 个结算点** ⇒ **记为重点待查** ✓
🔴 **不能验**：**`ProcessDamage` 的实现**（做不做死门判定）⇒ 记**未读**（前件已记）✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{ueq,ueq2}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **DoT 的 4 个结算点【全清】**（①②③④ 各自场合、单位、算法）✓
🆕 **发现可能的【第 5 个】**：`4880` 那处含 `MonsterParty` ⇒ 待查
🆕 **可做**：① 🔴 **读 `4880` 那处**（是不是第 5 个结算点）
   ② 读 `ExecuteRoundAdvance` 的调用点
   ③ 把第 67~79 条判据补进 `observe_list` D11（**本段已积累 13 条**）
   ④ **把 DoT 四层 + 4 个结算点写进 `PLAN_adoption`**（A6a 行）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
