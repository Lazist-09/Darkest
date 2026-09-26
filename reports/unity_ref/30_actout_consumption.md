# 29 种 act-out 的**消费点实测** —— 15 种反应里 **2 种没人读**

> 🕒 2026-09-26 · 承接 `28_actout_layer_gap.md` §6 的"逐种语义未读代码" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 📌 读的是参考项目 **485 个 `.cs`** ✓

---

## 1. 🎖️ 两张表的**枚举全集**（读代码得到，与数据侧逐条相符）

```
**`StartTurnActType`（回合开始）14 种**（`Character\Trait.cs`）：
   1 `Nothing` · 2 `BarkStress` · 3 `ChangePosition` · 4 `IgnoreCommand` · 5 `RandomCommand` ·
   6 `RetreatFromCombat` · 7 `AttackFriendly` · 8 `AttackSelf` · 9 `MarkSelf` ·
   10 `StressHealSelf` · 11 `StressHealParty` · **12 `BuffAlly`** · 13 `BuffParty` · 14 `HealSelf` ✓
   🎖️ **数值侧的名字是 `buff_random_party_member`**，而枚举里叫 **`BuffAlly`**
      ⇒ 📌 **两者对得上，只是命名风格不同** ✓（`29_*.md` 记的 `buff_random_party_member`）
**`ReactionType`（反应）15 种**：
   `BlockMove` · `BlockHeal` · `BlockBuff` · `BlockItem` · `BlockRetreat` ·
   `CommentSelfHit` · `CommentSelfMissed` · `CommentAllyHit` · `CommentAllyMissed` ·
   `CommentAllyAttackHit` · **`CommentAllyAttackMiss`** · `CommentMove` ·
   `CommentCurioInteraction` · `CommentTrapTriggered` · `BlockEffect` ✓
```

## 2. 🎖️ 结构：**两种 act-out 的类形状不同**（读代码）

```csharp
// Character\Trait.cs:52
public class CombatStartTurnActOut : IProportionValue {
    public StartTurnActType ActType { get; set; }
    public float NumberParameter { get; set; }      // ← 数值参数（如"减压多少"）
    public string StringParameter { get; set; }     // ← 字符串参数（如 bark 文本）
    public int Chance { get; set; }                 // ← 权重（= 数据里的 `chance`）
}
// Character\Trait.cs:60
public class ReactionActOut {
    public ReactionType ActType { get; set; }
    public Effect Effect { get; set; }              // ← 🔴 一个 Effect 对象
    public float Chance { get; set; }               // ← 🔴 这里是 **float**（不是 int）⚠️
}
```
🎖️ **两处值得记**：
```
① **回合开始的 `Chance` 是 `int`、反应的是 `float`** ⇒ 🔴 **量纲不一致** ⚠️
   📌 与 A5 的"技能欲望 `1e7` vs 目标欲望 `1..100`"同族 ⇒ **参考在同类字段上用不同量纲** ✓
② **反应带一个 `Effect` 对象**（不只是"拦/不拦"）⇒
   📌 即：`comment_*` 那 9 种**不是纯台词**，它们**自带一个 effect**（多为 `Stress`）✓
   🎖️ 消费点证实：`RaidSceneManager.cs:4783` `ReactionType.CommentMove].Effect` ⇒
      **取出来当 `barkStressEffect` 用** ⇒ ✅ **"台词 + 压力"是一体的** ✓
   ⇒ 🔴 **订正 `28_*.md §1` 的"9 种只是台词"**：
      准确说法是 **"9 种 `comment_*` 带一个 effect（多为压力），但不阻止动作"** ✓
```

## 3. 🔴🔴 15 种反应的**消费点实测**：**13 有 · 2 无**

| 反应类型 | 代码消费次数 | 判定 |
|---|---|---|
| `BlockMove` | **3** | ✅ |
| `BlockHeal` | **1** | ✅ |
| `BlockBuff` | **1** | ✅ |
| `BlockItem` | **5** | ✅ |
| **`BlockRetreat`** | 🔴 **0** | 🔴 **无消费** |
| `CommentSelfHit` / `CommentSelfMissed` | 各 **1** | ✅ |
| `CommentAllyHit` / `CommentAllyMissed` | 各 **1** | ✅ |
| `CommentAllyAttackHit` / `CommentAllyAttackMiss` | 各 **1** | ✅（**在三元里**，见 §4）|
| `CommentMove` | **2** | ✅ |
| `CommentCurioInteraction` | **2** | ✅ |
| `CommentTrapTriggered` | **2** | ✅ |
| **`BlockEffect`** | 🔴 **0** | 🔴 **无消费** |

```
📊 **13 有消费 · 2 无消费** ✓
🔴 而**那 2 个在数据里【都有非零 chance】** ⚠️ ⇒ **红线 21 同族**：
   · `block_effect`：**12 条都有该键**，其中 **5 条美德 `chance = 1`**
     （`stalwart` · `courageous` · `focused` · `powerful` · `vigorous`）✓
     ⇒ 🎖️ **即"5 个美德都会拦效果"** —— 而**代码里 0 次读它** ⚠️
   · `block_combat_retreat`：**12 条都有**，其中 **4 条折磨 `chance = 0.33`**
     （`paranoid` · `masochistic` · `depressed` · `irrational`）✓
     ⇒ 🎖️ **即"这 4 个折磨有 1/3 概率拦撤退"** —— 也**没人读** ⚠️
   ⇒ ✅ **两个都是"数据写了、代码没接"** ⇒ **与 `plot_quests` 那 6 个字段同族** ✓
```

## 4. 🎖️ 并订正我自己的**一处误判**（第 7 次同族）

```
🔴 **我第一版**按"`ReactionType.CommentAllyAttackMiss` 只出现 1 次"判它**消费不足** ⚠️
✅ **看上下文才发现**：那 1 次是**三元表达式的否定分支**：
   `RaidSceneManager.cs:4360-4361`
      `? ReactionType.CommentAllyAttackHit : ReactionType.CommentAllyAttackMiss;` ✓
   ⇒ 📌 **即：它确实被消费了**（`miss` 的路径上取它），只是**我的计数口径没体现"分支"** ✓
🎖️ **判据（新增）**：**"这个符号出现 1 次，是【真的只用到一次】还是【它在一条分支上】？"** ——
   **要看【上下文】，不能只看计数** ✓
   📌 与前面几次同族：**我用了一个【不体现结构】的度量**（纯计数）⚠️
```

## 5. 🎖️ 由此得到**采用时的行动清单**（29 种分成三档）

```
✅ **A 档（13 种）—— 有数据、有代码消费** ⇒ **照抄即可** ✓
   5 种 `block_*`（去掉 `block_combat_retreat`）+ 8 种 `comment_*` ✓
🔴 **B 档（2 种）—— 有数据、代码没读** ⇒ **要我们【自己实现】**（红线 21）
   `block_effect`（5 美德 `chance 1`）· `block_combat_retreat`（4 折磨 `0.33`）✓
🟡 **C 档（14 种回合开始）—— 有数据、代码消费情况【本件未逐个测】** ⚠️
   📌 **如实记**：本件**只测了 15 种反应的消费点**，**14 种回合开始的没测** ✓
      ⇒ ✅ **归下一轮**（`StartTurnActType` 的消费点在 `RaidSceneManager` 的回合开始处）
```

## 6. 诚实边界

```
✅ **能验**：两个枚举的**完整 29 项** · 两种 act-out 类的**字段形状**（含 `int` vs `float`）·
   **`Effect` 对象的存在** · **13/15 的消费次数** · **2 种无消费（且数据有值）** ·
   三元分支的原文 —— **全部当场跑出** ✓
🎖️ 并**订正两处我自己的说法**：
   ① `28_*.md` 的"9 种 comment 只是台词" ⇒ **准确说法：带 effect（压力），但不阻止动作** ✓
   ② 本件第一版的"`CommentAllyAttackMiss` 消费不足" ⇒ **它在三元分支上，确实被消费** ✓
🔴 **不能验**：**14 种回合开始的消费点【未测】** ⇒ 记**未测**（本件只覆盖反应那 15）✓
🔴 **不能验**：`Effect` 对象的**内部结构未读** ⇒ 记**未做** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/actout_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
🆕 **可做**：① **14 种回合开始的消费点**（补齐本件的缺口）
   ② 读 `Effect` 类的内部结构
   ③ 我方 `heirlooms.kinds`（复数）vs 兑换表（单数）的形状不一致 ✓
⏸️ **等策划**：七张单已投递 ✓
```
