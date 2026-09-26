# 最后一口关闭：**治疗向上取整（`Ceil`）· 伤害四舍五入（`Round`）—— 两侧【不对称】**

> 🕒 2026-09-26 · 承接 `37_heal_impl_verified.md §4` 的"`Heal(...)` 取整未读" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️🎖️ 实测：**两条公式的取整规则不同**

```
🎖️ **`Character.cs:1092-1100`** —— 治疗：
   ```csharp
   public virtual int Heal(float healAmount, bool includeModifier)
   {
       int heal = includeModifier
           ? Mathf.**CeilToInt**(healAmount * (1 + this[HpHealReceivedPercent].ModifiedValue))
           : Mathf.**CeilToInt**(healAmount);          // ← 向上取整
       this[AttributeType.HitPoints, true].IncreaseValue(heal);
       return heal;
   }
   ```
🎖️ **`Character.cs:1107-1112`** —— 伤害：
   ```csharp
   public int TakeDamage(float damageAmount)
   {
       int damage = Mathf.**RoundToInt**(damageAmount); // ← 四舍五入
       GetPairedAttribute(AttributeType.HitPoints).DecreaseValue(damage);
       return damage;
   }
   ```
⇒ 🔴 **即：同一个"按最大血量百分比"的换算，两侧取整【不同】** ⚠️
   · **治疗 ⇒ `Ceil`（向上）** ⇒ **偏向玩家**（治疗总是至少给到整数）✓
   · **伤害 ⇒ `Round`（四舍五入）** ⇒ **中性** ✓
```

## 2. 🎖️ 而这是**第二个 `Heal` 重载**（`Hero` 覆盖了它）

```
🎖️ **`Hero.cs:672-677`**：
   ```csharp
   public override int Heal(float healAmount, bool includeModifier)
   {
       **RevertDeathsDoor();**              // ← 🔴 英雄治疗【会自动解除死门】
       return base.Heal(healAmount, includeModifier);
   }
   ```
⇒ 🎖️ **即：`RevertDeathsDoor()` 被【三层】调用**：
   ① `Hero.Heal` 里（**任何治疗都先解除死门**）✓
   ② `HealSelf` 的 act-out 里（**单机 1 次 · 联机 2 次**）✓
⇒ 📌 **所以联机那处重复调用【更加无害】了** —— 因为 `Hero.Heal` 自己也会调它 ✓
   🎖️ **但仍不照抄**（冗余 + 误导读者）✓
```

## 3. 🎖️🎖️ 对本任务的意义：**取整规则要在契约里写死**

```
🔴 **P7 的伤害公式**（我方要换的那个）现在**已知取整**：
   · 参考 `TakeDamage` ⇒ **`RoundToInt`** ✓ ⇒ 与 `§7.1` 的"`ceil(…)`"**不同** ⚠️
   📌 `PLAN_adoption §7.1` 记的是 `dmg = **ceil**( Lerp(...) × (1+DamageMod) × (1-Prot) )`
      ⇒ 🔴 **而 `TakeDamage` 用的是 `Round`** ⇒ ⚠️ **两处说法不一致** ✓
      🎖️ **可能的解释（未核）**：`§7.1` 那个 `ceil` 是 **`BattleSolver` 算完**的取整，
         而 `TakeDamage` 是**落地时的第二次取整** ⇒ 📌 **可能是"两道取整"** ✓
      ⇒ 🔴 **记为【待核】**（这是我方 P7 的直接前置）✓
   · 参考 `Heal` ⇒ **`CeilToInt`** ✓
⇒ 🎖️ **判据**：**"这个公式的取整是【向上/四舍五入/向下】？"** ——
   **参考在【同一套血量的两个方向】上用了【两种取整】** ⚠️
   ⇒ ✅ **采用时必须【分别照抄】，不能统一** ✓
      📌 而这条**很容易被"顺手统一"掉**（因为看起来像不一致）⚠️
      🎖️ **与前面几件同族**：**参考有很多"看似不一致"的地方，其中一部分是【刻意的】** ✓
         （如 `darkness` 是标识 · `Σ` vs `dmg%` 两根轴 · 亮度方向 · `int` vs `float` 的 chance）
```

## 4. 🎖️ 顺带量出 `Heal` 的**第二项修正**

```
🎖️ `includeModifier = true` 时：
   `healAmount × (1 + this[**HpHealReceivedPercent**].ModifiedValue)` ✓
⇒ 🎖️ **即：治疗量会被【"受到治疗的百分比"]修正** ✓
   📌 而 `PLAN_adoption §2.1` 记过 A1 的 `dmg_received_percent`（**参考里完全没实现**）⚠️
   🎖️ **对照**：**`HpHealReceivedPercent` 是【实现了的】**（`Heal` 里在读它）✓
      ⇒ 📌 **即：参考实现了"受到治疗 %"，但没实现"受到伤害 %"** —— **不对称** ✓
      🎖️ **这与 `§6` 的"`DmgReceivedPercent` 完全没实现"【互相印证】** ✓
```

## 5. 诚实边界

```
✅ **能验**：`Heal` 的**完整实现（逐行）** · **`CeilToInt` vs `RoundToInt` 的不对称** ·
   `Hero.Heal` 的**覆盖 + `RevertDeathsDoor()`** · **`HpHealReceivedPercent` 确实被读** ·
   与 `§6` 的 `DmgReceivedPercent` **形成对照** —— **全部当场跑出** ✓
🎖️ 并**关掉了 `37_*.md` 记的最后一个口子** ✓
🔴 **不能验**：`§7.1` 的 `ceil` 与 `TakeDamage` 的 `Round` **是不是"两道取整"** ⇒
   📌 **要读 `BattleSolver` 的伤害路径** ⇒ 记**待核**（**这是我方 P7 的直接前置**）⚠️
🔴 **不能验**：`HpHealReceivedPercent` 的**取值来源**（哪个 buff 在改它）⇒ 记**未做** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/heal_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **act-out 线【全查完】**（含联机侧、执行体、两条推断、取整规则）✓
🆕 **可做**：① 🔴 **读 `BattleSolver` 的伤害路径** ⇒ 核"两道取整"（**P7 直接前置**）
   ② 扫 `darkest/scenes/**` 节点名一致性（`35_*.md` 的覆盖缺口）
   ③ 核 A6 数据里 `NumberParameter` 的取值 ✓
⏸️ **等策划**：七张单已投递 ✓
```
