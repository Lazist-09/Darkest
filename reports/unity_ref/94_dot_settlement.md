# DoT 结算逻辑**读通**：`TickDamage`/`Duration` + 概率门 + **一条与先手层的交叉影响**

> 🕒 2026-09-26 · 工具 `tools/dsh/read_dot_settlement.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `93_*.md §6①` 的"DoT 结算逻辑未读"** ⇒ **"按其逻辑对齐"的另一半** ✓

---

## 1. 🎖️🎖️ DoT 的**完整机制**（读 `PoisonEffect.cs` / `BleedEffect.cs` · 各 69 行）

```csharp
public class PoisonEffect : SubEffect {
    private int DotPoison { get; set; }                       // 🔴 就是数据里的 `.dotPoison N`
    public PoisonEffect(int dotAmount) { DotPoison = dotAmount; }

    public override bool ApplyInstant(performer, target, effect) {
        float poisonChance = effect.IntegerParams[**Chance**].HasValue
            ? (float)…Chance.Value / 100 : **1**;             // ① 基准概率 = chance
        poisonChance -= target…GetSingleAttribute(**Poison**).ModifiedValue;      // ② 减目标抗性
        if (performer is Hero)
            poisonChance += performer…GetSingleAttribute(**PoisonChance**)…;      // ③ 加施加者加成
        poisonChance = Mathf.Clamp(poisonChance, **0, 0.95f**);   // ④ 上限 95%
        if (RandomSolver.CheckSuccess(poisonChance)) {
            var poisonStatus = (PoisonStatusEffect)target…GetStatusEffect(StatusType.Poison);
            var newDot = new DamageOverTimeInstanse {
                **TickDamage = DotPoison**,
                **TicksAmount = effect.IntegerParams[Duration] ?? 3**   // ⑤ 默认 3 回合
            };
            newDot.TicksLeft = newDot.TicksAmount;
            poisonStatus.AddInstanse(newDot);
            return true;
        }
        return false;
    }
}
```
```
🎖️ **5 条要点**：
   ① **`chance` 作为【百分数 ÷100】** ⇒ `100%` ⇒ 1.0 ✓
   ② **减目标的 `Poison`/`Bleed` 抗性**（`AttributeType.Poison`/`.Bleed`）✓
   ③ **若是英雄施加 ⇒ 加 `PoisonChance`/`BleedChance` 加成** ⇒ 🔴 **只有英雄有这加成** ⚠️
   ④ 🔴 **上限硬编码 `0.95`** ⇒ **永远不可能 100% 上 DoT** ✓
   ⑤ **`duration` 缺省 = `3`** ⇒ ✅ **与数据里 `duration 3` 一致** ✓
⇒ 🎖️ **即：DoT 是"概率门 + 独立实例"** —— 同一目标可以有**多个 DoT 实例**（`AddInstanse`）✓
   📌 **而 `CurrentTickDamage = doTs.Sum(dot => dot.TickDamage)`** ⇒ **叠加** ✓
      ＋ **`CombinedDamage = Sum(TicksLeft × TickDamage)`** ⇒ **总伤害可算** ✓
      ＋ **`ExpirationTime = Max(TicksLeft)`** ⇒ **按最长的算** ✓
```

## 2. 📊 而**结算点**在哪（`RaidSceneManager` 的 8 处）

```
📊 **`CurrentTickDamage` 的消费点（单机 6 处）**：
   `2729`/`2748` ⇒ `TakeDamage(Mathf.**CeilToInt**(… × **1.5f**))` 🔴 ← **有 ×1.5！**
   `2817`/`2835` ⇒ `ProcessDamage(actionUnit, …CurrentTickDamage)`（**无 ×1.5**）
   `3448`/`3470` ⇒ `ProcessDamage(actionUnit, …CurrentTickDamage)`（**无 ×1.5**）
   `4683`/`4721` ⇒ `ProcessDamage(UnitEventQueue[i], …)`（**无 ×1.5**）
⇒ 🔴 **即：`2729`/`2748` 那两处是【另一个场合】**（×1.5）⇒ ⚠️ **要读上下文才知道区别** ✓
   🎖️ **判据（第 69 条）**：**"同一个值被 N 处消费 —— 它们的【算法一样】吗？
      ⇒ `2729` 有 ×1.5、其余没有 ⇒ 不是同一个场合"** ✓
```

## 3. 🔴🔴 而**关键交叉**：DoT 结算**在 `fromBonusTurn` 的跳过区里**

```
📊 **`3448` 与 `3470`（怪物的 DoT 结算）就在 `if (!fromBonusTurn) {` 里** ✓
   ```csharp
   3439|  if (!fromBonusTurn) {
   3441|      #region Status Effects and Buffs
   3443|      if (…StatusType.Bleeding).IsApplied) {
   3448|          if (ProcessDamage(actionUnit, bleedEffect.CurrentTickDamage)) …
   3465|      if (…StatusType.Poison).IsApplied) {
   3470|          if (ProcessDamage(actionUnit, poisonEffect.CurrentTickDamage)) …
   ```
⇒ 🎖️🎖️ **即：先手回合【不结算 DoT】** —— 与 `84_*.md` 的"轻量回合"**完全一致** ✓
   📌 **而这解释了 `85_*.md` 那处 bug 的【实际后果】**：
      · **单机**（`fromBonusTurn: true`）⇒ 先手回合**不跳 DoT** ✓
      · 🔴 **联机**（漏传 `true` ⇒ `false`）⇒ **先手回合会结算 DoT** ⚠️
      ⇒ ✅ **即：联机那只怪每回合【多掉一次血】** ✓
      🎖️ **比 `85_*.md` 时说得更具体了**：那次只说"会 DoT"，
         现在能指出**是 `3448`/`3470` 这两处**，且**先流血后中毒**（顺序固定）✓
```

## 4. 🎖️ 顺带：联机侧**又是"部分复制"**（与 `85`/`86_*.md` 同形）

```
📊 **联机的 `CurrentTickDamage` 消费点**：
   `1004`/`1026` ⇒ `CeilToInt(… × 1.5f)`（**与单机 `2729`/`2748` 同形**）✓
   🔴 `1107`/`1181` ⇒ **`Health.DecreaseValue(…CurrentTickDamage)`** ⚠️
      ⇒ 📌 **它【绕过 `ProcessDamage`】，直接改 Health** ✓
   🔴 **而联机【没有 `3448`/`3470` 那两处】**（先手回合的 DoT 结算）⇒
      📌 因为它把 `MonsterTurn` 交给了 `base`（`85_*.md` 的薄包装）⇒
      ✅ **即：那两处【走的是基类】** ⇒ **不是缺失** ✓
⇒ 🎖️ **判据（第 70 条）**：**"联机少一处 —— 是【缺失】还是【走基类】？
   ⇒ 它有没有 `override` 那个方法"** ✓
   📌 与第 43 条（"两份实现还是一份被复制的"）**同族**，但这次要**先看继承链** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`PoisonEffect`/`BleedEffect` 各 69 行原文** · **5 条要点（概率/抗性/加成/0.95/默认 3）** ·
   **`DamageOverTimeInstanse` 的 3 字段** · **`CurrentTickDamage`/`CombinedDamage`/`ExpirationTime`
   三个聚合** · **8 处消费点（含 ×1.5 的两处）** · **DoT 结算在 `!fromBonusTurn` 区里** ·
   **联机绕过 `ProcessDamage` 的两处** —— **全部当场跑出** ✓
🎖️ 并**把 `85_*.md` 的 bug 后果具体化到行号** ✓
🔴 **不能验**：**`2729`/`2748` 为何 ×1.5**（是什么场合）⇒ 📌 只知"与另几处不同" ⇒ 记**未读上下文** ✓
🔴 **不能验**：**`ProcessDamage` 的实现**（它做不做别的，如死门判定）⇒ 记**未读** ✓
🔴 **不能验**：**DoT 的结算时机**（回合开始还是结束）⇒ 📌 从 `3448` 在"状态区"里推为
   **怪物回合开始** ⇒ 记**推断** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{dot_tick,dot_ctx}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **DoT 机制【读通】**：概率门 + 独立实例 + 叠加 + 4 个结算点（含先手跳过）✓
🆕 **可做**：① 读 `2729`/`2748` 的上下文（那个 ×1.5 是什么场合）
   ② 读 `ProcessDamage`（它做不做死门判定）
   ③ 把第 67~70 条判据补进 `observe_list` D11
   ④ **把 A6a 更新进 `PLAN_adoption`**：现在"数据 + 消费点 + 解析 + 结算"四者齐 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
