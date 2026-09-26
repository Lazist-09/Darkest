# 取整链条查清：**是"两道取整"** —— 我上一件担心的"不一致"是**误读**

> 🕒 2026-09-26 · 承接 `38_rounding_asymmetry.md §5` 的"`BattleSolver` 路径未读" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **这是我方 P7（M1c 换伤害模型）的直接前置** ✓

---

## 1. 🎖️🎖️ 实测伤害路径（`BattleSolver.cs`）

```csharp
// L383-387
float initialDamage = performer is Hero
    ? Mathf.Lerp(performer.MinDamage, performer.MaxDamage, (float)RandomSolver.NextDouble())
        * (1 + skill.DamageMod)
    : Mathf.Lerp(skill.DamageMin, skill.DamageMax, (float)RandomSolver.NextDouble())
        * performer.DamageMod;

int damage = Mathf.**CeilToInt**(initialDamage * (1 - target.Protection));   // ← 第一道
if (damage < 0) damage = 0;                                                  // ← 下限 0
if (target.BattleModifiers != null && target.BattleModifiers.CanBeDamagedDirectly == false)
    damage = 0;                                                              // ← 免伤
…
// L399（暴击）
int critDamage = target.**TakeDamage(damage * 1.5f)**;                       // ← 第二道
// L413（非暴击）
damage = target.**TakeDamage(damage)**;
```

## 2. 🎖️ 所以取整链条是**两步，各管一段**

```
🎖️ **非暴击**：
   `ceil(x)` ⇒ int ⇒ `TakeDamage(int)` ⇒ `RoundToInt(整数)` = **整数** ⇒ 🔴 **第二道是恒等的** ✓
🎖️ **暴击**：
   `damage × 1.5f` ⇒ **可能是非整数**（如 `9 × 1.5 = 13.5`）
   ⇒ `RoundToInt(13.5)` = **14** ⇒ 🔴 **这一道真的生效** ✓
⇒ 📌 **即：非暴击走 `Ceil`、暴击多走一道 `Round`** ⚠️
```

### 🎖️ 用具体数字验（`Round(ceil(x)×1.5)` **≠** `ceil(x×1.5)`）

| `initial` | `prot` | `ceil(x)` | `×1.5` | `round` | `ceil(×1.5)` | 差 |
|---|---|---|---|---|---|---|
| 9 | 0.00 | 9 | 13.5 | **14** | 14 | 0 |
| 9 | 0.40 | 6 | 9.0 | 9 | 9 | 0 |
| 10 | 0.00 | 10 | 15.0 | 15 | 15 | 0 |
| **13** | 0.00 | **13** | **19.5** | **20** | **20** | 0 |

🔴 **注意**：上表里两种算法**恰好都得同一个值** —— 因为 `ceil(x)` 之后 `×1.5` 只会产生 `.0` 或 `.5`
   ⇒ `Round(.5)` 与 `Ceil(.5)` **在 .5 上同向**（都是进一）✓
   🎖️ **但这是【这两步恰好等价】，不是【一般成立】** ⚠️
      · `RoundToInt(19.5)` = 20（**银行家舍入？不，Unity 的 RoundToInt 是四舍五入**）✓
      · 若 `×1.5` 产生 `.5` 且**恰好在 .5 上** ⇒ 两者**都进一** ⇒ 等价 ✓
      · 🔴 而**若 `Protection` 让 `x` 变成负数**或**`damage` 为 0** ⇒ 情况不同 ⇒ 记**未穷举** ✓
   📌 **判据**：**"两种取整顺序在【这些取值上】恰好等价，不代表它们在【所有取值上】等价"** ✓
      ⇒ ✅ **采用时【照抄原顺序】**（不要因为"反正一样"就省一步）✓

## 3. 🔴 我上一件的担心**是误读**（如实订正）

```
🔴 `38_*.md` 我写：**"`§7.1` 记 `ceil(...)`，而 `TakeDamage` 用 `Round`
   ⇒ 🔴 两处说法不一致 ⇒ 可能是两道取整"** ⚠️
✅ **本件实测**：**"可能是两道取整"这个猜测是对的**，但**"不一致"这个判断是错的** ✓
   ⇒ 📌 **`§7.1` 的两句话【都准确】**：
      · `dmg = ceil( Lerp(...) × (1+DamageMod) × (1-Prot) )` ⇒ **对上 L387** ✓
      · `暴击 ⇒ round(dmg × 1.5)` ⇒ **对上 L399 + `TakeDamage` 的 `Round`** ✓
   ⇒ 🎖️ **即：`§7.1` 描述的就是【两道取整】，只是它没明说"这是两道"** ✓
🔴 **我的错在哪**：**把"同一段公式里的两个取整词"当成了"同一个取整的两种说法"** ⚠️
   ⇒ 🎖️ **判据（本件新增）**：**"公式里出现两个不同的取整词 —— 是【笔误/不一致】还是【两道工序】？"**
      —— **要看它们是否作用在【不同的中间值】上** ✓
      · `ceil` 作用在 `initialDamage × (1-Prot)` ⇒ **第一道** ✓
      · `round` 作用在 `damage × 1.5` ⇒ **第二道**（作用在**前一道的产物**上）✓
      ⇒ ✅ **两道工序** ✓
```

## 4. 🎖️ 顺带量出**两处下限/免伤**（我方 P7 要照抄）

```
① `if (damage < 0) damage = 0;` ⇒ **下限 0**（`Protection` 超过 1 时不会出负伤害）✓
② `if (target.BattleModifiers.CanBeDamagedDirectly == false) damage = 0;`
   ⇒ 🔴 **"不可被直接伤害"的怪 ⇒ 伤害归 0** ⚠️
   📌 **这条我方【有没有对应机制】未核** ⇒ 记**待核** ✓
      （A4 抽的怪物表里有 `battle_modifier` 字段 ⇒ 可能相关）✓
🎖️ 而 `PLAN_adoption §7.1` **没记这两条** ⇒ ✅ **本件补上** ✓
```

## 5. 🎖️ 对我方 P7 的**直接结论**

```
✅ **P7 的公式现在【完全确定】**（可直接落）：
   ```
   initialDamage = Lerp(weapon.DamageLow, weapon.DamageHigh, rnd) × (1 + skill.DamageMod)
   damage       = ceil(initialDamage × (1 - target.Protection))
   damage       = max(damage, 0)                     // ← 下限
   if (不可被直接伤害) damage = 0                     // ← 免伤
   if (暴击) damage = round(damage × 1.5)            // ← 第二道取整
   ```
   🎖️ **注意**：**非暴击时不要多走一道 `round`**（虽然恒等，但**照抄原样**）✓
🔴 **而 P7 仍卡在别处**（与取整无关）：
   · **`Σ段倍率` 归一**（策划已裁，**但打穿了 A1** ⇒ 待裁）✓
   · **30 条 `dmg%` 真值**（策划已裁"全取 0"）✓
   ⇒ 📌 **即：伤害公式这一侧的【取整】已经不再是未知项** ✓
```

## 6. 诚实边界

```
✅ **能验**：`BattleSolver` 的**伤害路径逐行** · **两道取整的确切位置** ·
   `damage × 1.5f` 的**中间值确实是非整数** · **下限 0 与免伤两条** ·
   **`§7.1` 两句话都对上** —— **全部当场跑出** ✓
🎖️ 并**订正我上一件的误读**（把"两道工序"当成"不一致"）✓
🔴 **不能验**：**"两种取整顺序在所有取值上等价吗"** ⇒ 本件只举了 4 个样本 ⇒ 记**未穷举** ✓
   📌 **如实记**：我**推断**它们在 `.0 / .5` 上同向，但**没证明** ✓
🔴 **不能验**：`CanBeDamagedDirectly` **在我方有没有对应** ⇒ 记**待核** ✓
🔴 **不能验**：**没有改任何数值** ⇒ "按这个公式落库后伤害变多少"**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/bs_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **P7 的公式侧【查清】**：两道取整 + 下限 + 免伤 ⇒ **可直接落** ✓
🆕 **可做**：① 核 `CanBeDamagedDirectly` 在我方的对应（A4 的 `battle_modifier`）
   ② 扫 `darkest/scenes/**` 节点名一致性（`35_*.md` 的覆盖缺口）
   ③ 核 A6 数据里 `NumberParameter` 的取值 ✓
⏸️ **等策划**：七张单已投递 ✓（**P7 仍等 `Σ` 那张**）✓
```
