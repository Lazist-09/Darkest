# 全库取整审计：**5 种方式 · 战斗侧 35 处** —— 规律是「**伤害走 Ceil（向下保守）· 其它走 Round**」

> 🕒 2026-09-26 · 工具 `tools/dsh/audit_rounding_all.py`（新·可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `101_*.md` 起的"取整从未一次扫全"** ✓

---

## 1. 📊 全库 5 种取整（`Assets/Scripts/**`）

| 方式 | 次数 | 说明 |
|---|---|---|
| **`(int)` 强转** | **142** | 🔴 **截断**（不是四舍五入）|
| **`RoundToInt`** | **73** | 四舍五入 |
| **`CeilToInt`** | **19** | 🔴 **向上取整** |
| `Math.Round` | 10 | — |
| `FloorToInt` | **3** | 🔴 **向下取整（最少）** |

```
🎖️ **判据（第 100 条）**：**"同一个项目里有几种取整？
   ⇒ 全库扫【函数名】，而不是遇到一个记一个"** ✓
   📌 **而这条一扫就发现**：本任务零散记过 4 处，**实际有 5 种** ✓
   🔴 `(int)` 强转占 **142/247（57%）** ⇒ ⚠️ **它是截断**（负数时朝 0，不是朝下）✓
```

## 2. 🎖️🎖️ 而**战斗侧只有 35 处**，且规律很清楚

```
📊 **`Mechanics`/`Character`/`Managers`/`Raid` 四个目录：35 处**
   · **`RoundToInt` 28** · **`CeilToInt` 7** ✓
```

### 🔴 而 `CeilToInt` 的那 7 处**全部与「伤害/治疗」有关**

| 位置 | 用途 | 取整 |
|---|---|---|
| **`Character.Heal`** `L1095`/`L1096` | **治疗**（含/不含修正）| 🔴 **`CeilToInt`** |
| **`BattleSolver`** `L387` | **伤害**（`×(1−Protection)`）| 🔴 **`CeilToInt`** |
| **`BattleSolver`** `L450`/`L453` | **伤害上下限**（预览用）| 🔴 **`CeilToInt`** |
| **`RaidSceneManager`** `L2729`/`L2748` | **空闲怪 DoT**（`×1.5`）| 🔴 **`CeilToInt`** |
| （＋`Character.cs:1095` 的另一分支）| | |

```
⇒ 🎖️ **即：`CeilToInt` 是【伤害/治疗专用】** ✓
```

### 🎖️ 而 `RoundToInt` 的 28 处是**压力 / 火把 / 折扣**等

```
`StressEffect` `L30`/`L65`/`L129` ⇒ **压力**（三处：instant/queued/fused）✓
`StressHealEffect` `L32`/`L61`/`L109` ⇒ **减压** ✓
`RaidSceneManager` `L441` ⇒ 某伤害 · `L465` ⇒ **减压** · `L1843` ⇒ **压力** ·
   `L5588` ⇒ **解除概率** · `L5775` ⇒ **火把惩罚** ✓
`TownManager` `L120`/`L160` ⇒ **折扣后价格** ✓
`TorchMeter` `L104`/`L405` ⇒ **火把长度** ✓
```

## 3. 🔴 所以**真正的规律**是（本件最重要的更正）

```
🎖️ **参考的取整规律**：
   · **伤害 / 治疗** ⇒ **`CeilToInt`**（**向上** ⇒ 对玩家略「慷慨」）✓
   · **压力 / 减压 / 火把 / 折扣** ⇒ **`RoundToInt`**（四舍五入）✓
⇒ 📌 **而 `101_*.md` 我写的是"压力用 `RoundToInt`，伤害用 `Ceil`"** ——
   ✅ **方向对**，但**当时只当成"散落的差异"** ⚠️
   🎖️ **现在看清了：它是【按语义分工的一致规则】** ✓
      · **"HP 相关" ⇒ Ceil** · **"非 HP 资源" ⇒ Round** ✓
   ⇒ 🎖️ **判据（第 101 条）**：**"这些看似散落的差异，
      是不是【按某个维度】分工的？⇒ 按【数据种类】归类看看"** ✓
```

## 4. 🔴🔴 而 `TakeDamage` 与 `BattleSolver` 是**两段取整**（`39_*.md` 的结论得到确认）

```
📊 **原文**：
   ① **`BattleSolver.cs:387`** ⇒ `int damage = Mathf.**CeilToInt**(initialDamage * (1 - Protection))` ✓
      ＋ `if (damage < 0) damage = 0;` 🔴 **下限 0** ✓
   ② **`Character.cs:1109`** ⇒ `int damage = Mathf.**RoundToInt**(damageAmount)` ✓
      🔴 **没有下限**（不再 clamp）✓
⇒ 🎖️ **即：`Ceil` → 传给 `TakeDamage` → 再 `Round`** ⇒ ✅ **两次取整** ✓
   📌 **而 `Ceil` 之后再 `Round`** ⇒ 对整数**无影响**（Round 整数 = 自身）✓
      ⇒ 🔴 **所以第二次 `Round` 只在【倍率】场合才起作用**：
         `TakeDamage(damage * 1.5f)`（暴击 `L399`）· `TakeDamage(CeilToInt(DoT × 1.5f))`（`2729`）✓
      🎖️ **即在 `BattleSolver` 里**：`Ceil` 后的整数 × 1.5 ⇒ **小数** ⇒ `Round` 收口 ✓
      ⇒ 📌 **判据（第 102 条）**：**"第二次取整在哪一步起作用？
         ⇒ 只有当【中间又乘了一次小数】时"** ✓
```

## 5. 🎖️ 而**下限的完整表**（修正 `41_*.md` 的"参考下限 0"）

```
📊 **逐个下限**：
   · **`BattleSolver` 伤害** ⇒ `if (damage < 0) damage = 0` ⇒ 🔴 **下限 0** ✓
   · **`BattleSolver` 预览上下限** ⇒ 同样 **下限 0** ✓
   · **`StressEffect`** ⇒ `if (damage < 1) damage = 1` ⇒ 🔴 **下限 1** ✓
   · **`Character.TakeDamage`** ⇒ 🔴 **无下限**（由调用方保证）✓
   · **`Character.Heal`** ⇒ 🔴 **无下限**（`CeilToInt` 可为 0）✓
⇒ 🎖️ **即：下限不统一** —— **伤害 0 · 压力 1 · 治疗/落地无** ✓
   📌 与 `41_*.md`（"我方下限 1 · 参考 0"）**一致**，且**现在知道压力那条是 1** ✓
```

## 6. 诚实边界

```
✅ **能验**：**全库 5 种取整的逐个计数（142/73/19/10/3）** ·
   **战斗侧 35 处（RoundToInt 28 · CeilToInt 7）** · **7 处 `CeilToInt` 全在伤害/治疗** ·
   **`BattleSolver:387` + `Character:1109` 的两段取整** ·
   **`if (damage < 0) damage = 0` 与 `if (damage < 1) damage = 1`** ·
   **`Heal` 用 `CeilToInt`** —— **全部当场跑出** ✓
🎖️ 并把"散落差异"升级为**按语义分工的一致规则** ✓
🔴 **不能验**：**`(int)` 强转的 142 处里有没有战斗相关的** ⇒
   📌 本件只看四个战斗目录（那 35 处**不含强转**）⇒ ✅ **战斗侧无强转** ✓
   🎖️ **但非战斗侧（UI/城镇/存档）的 142 处未逐个读** ⇒ 记**未读** ✓
🔴 **不能验**：**`Math.Round` 10 处 / `FloorToInt` 3 处在哪** ⇒
   📌 从文件表看多在 `UI/`（`MonsterTooltip` · `RaidCharStatsPanel`）⇒ **表现层** ⇒ 记**未逐个读** ✓
🔴 **不能验**：**`Character.TakeDamage` 是否真的无下限**（会不会有别的 clamp）⇒
   📌 本件读了 `L1107-1112` 全文（**只有 `RoundToInt` + `DecreaseValue`**）⇒ ✅ **确认无** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{round_combat,dmg_heal,bs_round}.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **取整【一次扫全】**：5 种 · 战斗侧 35 处 · 规律 = **HP 走 Ceil · 非 HP 走 Round** ✓
🆕 **可做**：① 读 `Math.Round` 10 处 / `FloorToInt` 3 处（多为 UI ⇒ 可选）
   ② 把第 100/101/102 条判据补进 `observe_list` D11
   ③ **把我方 `BattleMath.ApplyDamageRounding`（`Round` + 下限 1）与参考对照**
      ⇒ 出"要改什么"的清单 ✓
   ④ 把取整规律补进 `PLAN_adoption`（`§7.1` 那条）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
