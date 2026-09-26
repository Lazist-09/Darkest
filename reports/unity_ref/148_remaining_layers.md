# 其余 10 条原语的落点层：**全部落在战斗侧** —— **误判是 `MoraleMod` 独有的，不是普遍的**

> 🕒 2026-09-26 · 工具 `tools/dsh/audit_remaining_layers.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `147_*.md §6②` 的判据 247** ✓

---

## 1. 📊 实测：其余 10 条**消费侧全 0**（与 `MoraleMod` 同一现象）

```
📊 **逐条（排除表自身）**：
   · `damage_received_percent` ⇒ **0** ✓
   · `hp_heal_received_percent` · `hp_heal_amount` ⇒ **0** ✓
   · `stun_chance` · `poison_chance` · `bleed_chance` · `move_chance` · `debuff_chance` ⇒ **0** ✓
   · `resistance` ⇒ **0** ✓
   · `combat_stat_add` · `combat_stat_multiply` ⇒ **各 1**（🔴 **都在 `BuffPrimitivesConfig.cs`
     的注释里**，不算消费点）✓
⇒ 🔴 **即：10/10 都是"纯未接线"** ⇒ ⚠️ **"有没有消费点"这个判据再次分不出层** ✓
   📌 **判据（第 240 条复用）**：**"我的判据能区分我要区分的两个类吗？
      不能 ⇒ 换判据"** ⇒ ✅ **继续用"按语义找落点"** ✓
```

## 2. 🎖️ 按语义找 ⇒ **全部落在【战斗侧】**

```
📊 **逐组证据**：
   | 组 | 语义探针 | 找到的落点 | 层 |
   |---|---|---|---|
   | **`resistance`** | `UnitStats.cs:19-21` ⇒ `StunResist` · `BleedResist` · `StatDebuffResist` | **`UnitStats`**（单位属性）| ✅ **战斗侧** |
   | **`*_chance`（5 条）** | **`BattleMath.ActualEffectChance`**（`labeled × (1 − resist/100)`）| **`EffectsStep.cs:47`** 在调它 | ✅ **战斗侧** |
   | **`hp_heal_*`（3 条）** | `HealPercent` ⇒ `EatHealPercent` ⚠️（那是**饥饿**）| **`HealAmount.Scale`**（`SkillExecutor.cs:367`）| ✅ **战斗侧** |
   | **`damage_received_percent`** | `damage_received` ⇒ 只在表内 | **`DamageStep` 的 `raw` 层**（`note` 说的）| ✅ **战斗侧** |
   | **`combat_stat_*`（2 条）** | **`BuffModifierKind.StatMod`/`DamageMod`** + `IBuffLedger.StatModTotal` | **`BuffDefsConfig`** 的按名分发 | ✅ **战斗侧** |
⇒ 🎖️🎖️ **即：其余 10 条的落点【全在战斗侧】** ⇒ ✅ **枚举（轴=战斗）对它们【都成立】** ✓
   📌 **而 `EffectsStep.cs:47` 是实打实的调用点**：
     `actualChance = BattleMath.ActualEffectChance(labeled, resist);` ✓
     ⇒ ✅ **`*_chance` 那 5 条的战斗侧落点【存在且已接线】**（虽然用的是别的名字）✓
```

## 3. 🔴 所以判据 247 的答案：**不是"普遍误判"**

```
📊 **两类对照**：
   | 类 | 条数 | 枚举 vs 落点 |
   |---|---|---|
   | **`MoraleMod` 里的 4 条**（`stress_heal_*` ×2 · `resolve_*` ×2）| **4** | 🔴 **不符**（枚举战斗、落点趟）|
   | **其余 10 条** | **10** | ✅ **相符**（枚举战斗、落点战斗）|
   | `ExpeditionLayer` 的 7 条 | **7** | ✅ **相符**（枚举层、落点层）|
⇒ 🎖️ **即：21 条里 4 条不符（19%）** ✓
   📌 **判据（第 247 条）**：**"我核了 1 个取值就下结论 ——
      依据够吗？（够说'设计'，不够说'普遍误判'）"** ✓
      ⇒ ✅ **本件补完 10 条 ⇒ 可以说"不是普遍误判，是 `MoraleMod` 一类独有"** ✓
   🎖️ **判据（第 248 条）**：**"我说'某类有 X 问题'——
      要核【其它类】才能说'独有'还是'普遍'"** ✓
```

## 4. 🎖️ 而这条**让第 236 条的报告更精确**

```
📊 **报给架构的措辞应改为**：
   · 🔴 **`Target` 枚举确实混了【轴】与【层】**（8 按轴 · 1 按层 · 定义原文为证）✓
   · 🎖️ **而实际误判只有 4 条**（`MoraleMod` 里的趟级项），**不是全部** ✓
   · ⇒ ✅ **所以修法有两种**：
     **(甲) 拆维度**（根治，但动 9 个取值）✓
     **(乙) 只补 1 个取值**（如 `ExpeditionMorale`）⇒ ⚠️ **治 4 条，不动结构** ✓
   ⇒ 🎖️ **判据（第 249 条）**：**"我提的修法 ——
      是【根治】还是【补特例】？⇒ 按【误判占比】选（4/21 ⇒ 特例也可接受）"** ✓
      📌 **这是个诚实的权衡**：**拆维度成本高（动枚举 + `Tally` + `PendingCondition` + 测试）**，
         而**补特例只动 1 处** ⇒ ✅ **两条路都该给架构** ✓
```

## 5. 诚实边界

```
✅ **能验**：**其余 10 条的消费侧计数（全 0，含 2 条只在注释）** ·
   **按语义找到的落点（`UnitStats` · `EffectsStep.cs:47` · `HealAmount.Scale` ·
     `DamageStep` · `BuffDefsConfig`）** · **`BattleMath.ActualEffectChance` 的真实调用** ·
   **21 条里 4 条不符（19%）** —— **全部当场跑出** ✓
🎖️ 并把结论从"枚举设计有问题"精确到**"误判只有 4 条，非普遍"** ✓
🔴 **不能验**：**`damage_received_percent` 的落点**（`DamageStep` 的 `raw` 层）
   ⇒ 📌 **只有 `note` 说，未见调用** ⇒ ⚠️ **记"仅 note"** ✓
🔴 **不能验**：**`hp_heal_*` 3 条是否真接在 `HealAmount.Scale` 上** ⇒
   📌 `Scale` 目前只处理 `hp_heal_percent`（已激活那条）⇒
   ⚠️ **另 2 条【未接】** ⇒ ✅ **与本件"未接线"一致** ✓
🔴 **不能验**：**那 5 条 `*_chance` 的"落点已接线"是否等于"原语已接"** ⇒
   📌 `EffectsStep.cs:47` 用的是 `labeled`/`resist`（**不是原语名**）⇒
   ⚠️ **即：机制在，而原语名未接** ⇒ ✅ **正是"未接线"的含义** ✓
   🎖️ **判据（第 250 条）**：**"机制在 ≠ 原语接了 ——
      后者要【按名字】读到才算"** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/semantic_layers.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **判据 247【闭环】**：其余 10 条全在战斗侧 ⇒ **误判只有 4 条（19%），非普遍** ✓
🆕 **可做**：① 🔴 **把"根治(甲) vs 补特例(乙)"两条路报给架构**（判据 249）
   ② 把第 243~250 条判据补进 `observe_list` D11（**本段 8 条**）
   ③ **把"21 条中 4 条不符"写进 `PLAN_adoption`** ✓
   ④ **回到 A 线**：核 A6a/A6b 之外还有什么可推进的（本任务已 60 轮）
⏸️ **等策划**：十三张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
