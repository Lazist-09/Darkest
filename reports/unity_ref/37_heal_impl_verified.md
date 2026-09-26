# 两条推断查证：`HealPercent` **确认**为正典 · `RevertDeathsDoor` **确认幂等**

> 🕒 2026-09-26 · 承接 `36_actout_multiplayer.md §6` 的两条"记为推断" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. ✅ 推断①**确认**：单机那份 `HealPercent` 就是联机那份的**等价实现**

```
🎖️ **`Character.cs:1102-1105`**：
   ```csharp
   public int HealPercent(float healPercent, bool includeModifier)
   {
       return Heal(this[AttributeType.HitPoints, true].ModifiedValue * healPercent, includeModifier);
   }
   ```
⇒ 📌 **即：`HealPercent(p)` = `Heal(最大血量ModifiedValue × p)`** ✓
🎖️ **对照联机那份**：`Mathf.RoundToInt(actOut.NumberParameter * actionHero.Health.ModifiedValue)` ✓
⇒ ✅ **两者是【同一个公式】**（`NumberParameter × ModifiedValue`，再取整）✓
   🔴 **唯一可能的差异**：**取整方式** ——
      · 单机走 `Heal(...)` ⇒ **取整方式要看 `Heal` 的实现**（本件**未读**）⚠️
      · 联机显式 `Mathf.RoundToInt(...)` ⇒ **明确是四舍五入** ✓
   ⇒ 🎖️ **所以我的推断【方向对但理由要改】**：
      · ❌ 我原先说"单机走 `HealPercent` ⇒ **更可能是正典**"（暗示两者实现不同）
      · ✅ **实际是：两者【同一公式】** ⇒ **差异只可能在取整上** ✓
      📌 **判据**：**"两条不同的写法，是真的不同，还是同一个公式的两种写法？"** ——
         **读被调用的那个函数** ⇒ **本件读了才发现是同一个公式** ✓
      🎖️ **即：我上次的"更可能是正典"是个【不必要的假设】** ——
         它们本来就是一回事 ✓（**如实订正**）
```

## 2. ✅ 推断②**确认**：`RevertDeathsDoor()` **是幂等的** ⇒ 重复调用**无害**

```
🎖️ **`Hero.cs:368-377`**：
   ```csharp
   public void RevertDeathsDoor()
   {
       if (GetStatusEffect(StatusType.DeathsDoor).IsApplied)     // ← 前置守卫
       {
           GetStatusEffect(StatusType.DeathsDoor).ResetStatus();
           foreach (var removedBuff in BuffInfo.FindAll(
               buffEntry => buffEntry.SourceType == BuffSourceType.DeathsDoor))
               RemoveBuff(removedBuff);
           ApplyMortality();                                      // ← 也是幂等的
       }
   }
   // Hero.cs:380-386
   public void ApplyMortality()
   {
       var mortalityStatus = GetStatusEffect(StatusType.DeathRecovery) as DeathRecoveryStatusEffect;
       if (mortalityStatus.IsApplied) **return**;                 // ← 守卫
       else mortalityStatus.AtDeathRecovery = true;
   }
   ```
⇒ ✅ **两道守卫都在**：
   ① `RevertDeathsDoor` 开头 `if (…DeathsDoor).IsApplied)` ⇒ **第一次调用后 `IsApplied` 变假
      ⇒ 第二次【整块跳过】** ✓
   ② `ApplyMortality` 开头 `if (mortalityStatus.IsApplied) return;` ⇒ **自己也是幂等的** ✓
⇒ ✅ **即：联机侧那处重复调用【无害】** ✓
   🎖️ **但结论不变**：**它是复制粘贴的重复，不要照抄** ✓
      📌 理由：**"无害"不等于"该留"** —— 它增加阅读成本且暗示"可能有副作用"⚠️
      🎖️ **判据**：**"这段重复代码【有害】还是【只是冗余】？"** ——
         **只是冗余 ⇒ 仍应删**（**因为它会误导读者以为有副作用**）✓
```

## 3. 🎖️ 顺带量出**两个相邻的百分比函数**（同一形状）

```
🎖️ `Character.cs:1107-1117` 里 `TakeDamage` / `TakeDamagePercent` 与 `Heal` / `HealPercent`
   **形状完全对称**：
      `TakeDamagePercent(p) => TakeDamage(最大血量 × p)` ✓
      `HealPercent(p)       => Heal(最大血量 × p)` ✓
   ⇒ 📌 **即：`AttackSelf` 与 `HealSelf` 用的是【同一套百分比换算】** ✓
   🎖️ **对本任务的意义**：
      · `AttackSelf` 的 `NumberParameter`（**按百分比扣血**）⇒ `TakeDamagePercent` ✓
      · `HealSelf` 的 `NumberParameter` ⇒ `HealPercent` ✓
      ⇒ ✅ **两者【同量纲】**（都是"最大血量的百分比"）✓
      📌 而 A6 的数据里这两个的 `NumberParameter` **取值未核** ⇒ 记**待核** ✓
```

## 4. 诚实边界

```
✅ **能验**：`HealPercent` / `TakeDamagePercent` 的**原文** ·
   **"两者是同一公式"** · `RevertDeathsDoor` / `ApplyMortality` 的**两道守卫原文** ·
   **幂等性成立** · 四个函数的**对称形状** —— **全部当场跑出** ✓
🎖️ 并**订正我上一件的一个不必要假设**（"单机更可能是正典" ⇒ 实际两者同公式）✓
🔴 **不能验**：**`Heal(...)` 的取整方式未读** ⇒ 单机与联机的取整**是否真的一致，未判** ⚠️
   ⇒ 📌 **这是本件唯一还开着的口子**（记**未判**）✓
🔴 **不能验**：`NumberParameter` 的**实际取值**（A6 的数据里有没有）⇒ 记**待核** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/heal_impl.py` **不入库**（scratch）✓
```

## 5. 下一步

```
✅ **两条推断【都查完了】**：一条确认（幂等）· 一条订正（两者同公式）✓
🆕 **可做**：① 读 `Heal(...)` 的取整方式（关掉最后那个口子）
   ② 核 A6 数据里 `NumberParameter` 的取值
   ③ 扫 `darkest/scenes/**` 节点名一致性（`35_*.md` 的覆盖缺口）✓
⏸️ **等策划**：七张单已投递 ✓
```
