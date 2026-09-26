# 融合系统**只有 2/29 个子类实现** —— 且是**成对的 stress 增减**，用于**合批结算**

> 🕒 2026-09-26 · 工具 `tools/dsh/find_fusable_subclasses.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `100_*.md §6①` 的"哪些子类覆盖 `Fusable`/`Fuse` 未查"** ✓

---

## 1. 📊 读数：**2 / 29**

```
📊 `Effects/` 目录：**29** 个文件 · **全是 `SubEffect` 的子类** ✓
🎖️ **覆盖 `Fusable`**：**2** 个 ⇒ **`StressEffect.cs`** · **`StressHealEffect.cs`** ✓
🎖️ **覆盖 `Fuse(...)`**：**2** 个 ⇒ **同上** ✓
🎖️ **覆盖 `ApplyFused(...)`**：**2** 个 ⇒ **同上** ✓
⇒ 🎖️ **即：融合系统只有【一对】实现** —— **压力增/减** ✓
   📌 **判据（第 87 条）**：**"这个基类能力有 N 处覆盖 ——
      是【广泛使用】还是【个别特例】？⇒ 2/29 ⇒ 个别特例"** ✓
```

## 2. 🎖️🎖️ 而三者**各司其职**（读 `StressEffect.cs` 的三段）

| 方法 | 行 | 做什么 |
|---|---|---|
| **`Fusable`** | `L6` | **`return **true**`**（**声明"我能被融合"**）✓ |
| **`Fuse(performer, target, effect)`** | **`L113-134`** | 🔴 **只算【数值】，不施加** —— 走完概率门与两个修正后 **`return damage`** ✓ |
| **`ApplyFused(performer, target, effect, fuseParameter)`** | **`L87-111`** | 🔴 **直接 `IncreaseValue(fuseParameter)`** —— **用【传进来的合并值】** ✓ |

```
🎖️ **即：三步分工**
   ① `Fuse()` ⇒ **算这一次贡献多少**（**不落地**）✓
   ② `EffectEvent.Fuse(next)` ⇒ **累加进 `StackParameter`** ✓
   ③ `ApplyFused(param)` ⇒ **一次性施加【合计值】** ✓
⇒ 🎖️ **而"不落地"是关键**：`Fuse()` 里**没有** `Stress.IncreaseValue` ✓
   📌 **所以融合 = "先算总和，再施加一次"** ⇒ ⚠️ **而不是"施加 N 次"** ✓
   🎖️ **判据（第 88 条）**：**"这个 `Fuse()` 是【算值】还是【落地】？
      ⇒ 看里面有没有 `IncreaseValue`/`DecreaseValue`"** ✓
```

## 3. 🎖️ 而 `StressEffect` / `StressHealEffect` 的**三段实现同形**

```
📊 **`StressEffect.cs`（142 行）的四个方法**：
   · `ApplyInstant`（`L14`）· `ApplyQueued`（`L49`）⇒ **同形**（算 damage ⇒ `IncreaseValue`）
      🔴 **唯一差别**：`ApplyQueued` 末尾**多了提示与光晕**
         （`ShowPopupMessage(… Stress …)` · `SetHalo("afflicted")`）✓
      ⇒ 🎖️ **即：立即/延迟的【数值逻辑完全相同】，差的是【表现】** ✓
         📌 与 `94_*.md` 的 `PoisonEffect` **同形**（那次也是"queued 包了提示"）✓
   · `ApplyFused`（`L87`）⇒ **用 `fuseParameter` 直接施加** ✓
   · `Fuse`（`L113`）⇒ **只 return damage** ✓
⇒ 🎖️ **而 `Fuse()` 与 `ApplyInstant()` 的【算法完全一致】**（同 4 步）✓
   📌 **判据（第 89 条）**：**"`Fuse()` 与 `ApplyInstant()` 的算法一样吗？
      ⇒ 一样 ⇒ 融合只是【把 N 次合成 1 次】，不改变数值"** ✓
```

## 4. 🎖️ 而这两段里的**数值链**（顺带量出 stress 的算法）

```
📊 **`StressEffect` 的 damage 算法**（`L26-32`，与 `Fuse` 同）：
   `initialDamage = **StressAmount**`（= 数据里的 `.stress N`）✓
   `if (performer != null) initialDamage *= (1 + performer[**StressDmgPercent**])`   // 施加者加成
   `damage = RoundToInt(initialDamage * (1 + target[**StressDmgReceivedPercent**]))` // 目标易感
   `if (damage < **1**) damage = **1**` ⇒ 🔴 **下限 1**（**不为 0**）⚠️
⇒ 🎖️ **三条**：
   ① **两个修正乘子**（`StressDmgPercent` × `StressDmgReceivedPercent`）✓
      📌 与 `§6` 记的"`DmgReceivedPercent` **参考完全没实现**"**形成对照** ——
         🔴 **"受到伤害 %"没实现，但"受到压力 %"【实现了】** ⚠️（`38_*.md` 已记同族）
   ② 🔴 **下限 `1`** ⇒ **与伤害的"下限 0"不同** ⚠️
      📌 **判据（第 90 条）**：**"同一套公式的两个变体，下限一样吗？
         ⇒ 伤害下限 0（`41_*.md`）vs 压下下限 1 ⇒ 不一样"** ✓
   ③ **`RoundToInt`**（不是 `Ceil`/`Floor`）✓
      📌 与 `39_*.md` 的"伤害走 `Ceil`"**又一处取整不同** ✓
```

## 5. 🎖️ 于是"融合"这一层的**采用结论**

```
✅ **规模极小**：**2/29** 子类 ⇒ 📌 **采用时【可以后做】** ✓
   ＋ 而**数据里有没有用到它** ⇒ 要看**同一次伤害里是否出现同目标的多条 `stress`**
      ⇒ 📌 **而 `Fusable` 只在 `Stress*` 两个类上** ⇒ ✅ **范围明确** ✓
🔴 **但 `100_*.md` 的"447 处 `queue`"仍要处理**（那是**另一个**机制）✓
⇒ 🎖️ **判据（第 91 条）**：**"这个机制有 N 处覆盖 ——
   是【必须先做】还是【可以后补】？⇒ N 小且限定在少数类 ⇒ 可后补"** ✓
```

## 6. 诚实边界

```
✅ **能验**：**29 个子类里 2 个覆盖（逐个文件）** · **三个方法的行号** ·
   **`Fusable => true`** · **`Fuse()` 只 `return damage`（无 `IncreaseValue`）** ·
   **`ApplyFused` 直接 `IncreaseValue(fuseParameter)`** ·
   **`ApplyInstant` 与 `ApplyQueued` 只差提示** ·
   **stress 的 4 步算法（含下限 1 与 `RoundToInt`）** —— **全部当场跑出** ✓
🎖️ 并**把融合系统的范围缩到 2 个类**，且**确认它不改变数值** ✓
🔴 **不能验**：**数据里是否真有"同目标多条 stress"** ⇒
   📌 那决定融合**是否被实际触发** ⇒ 记**未查** ✓
   🎖️ **而那是"要不要做"的关键** ⇒ ⚠️ **记为重点待办** ✓
🔴 **不能验**：**`StressHealEffect` 是否与 `StressEffect` 完全镜像** ⇒
   📌 本件只见它**同样覆盖三方法** ⇒ 记**未读全文** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（工具本身入库）✓
```

## 7. 下一步

```
✅ **融合系统【查清】**：2/29 子类 · 三步分工 · 不改变数值 ✓
🆕 **可做**：① 🔴 **查数据里有没有"同目标多条 stress"**（决定融合是否被触发）
   ② 读 `StressHealEffect` 全文（核它与 `StressEffect` 是否镜像）
   ③ 把第 83~91 条判据补进 `observe_list` D11
   ④ **把 `queue` + 融合补进 `PLAN_adoption` 的 A6a 行** ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
