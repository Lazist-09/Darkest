# 「合并 `def`」的**前置基线**（架构硬要求 · `M1a`）

> 🔴 **架构要求原文**：`M1a` —— "**合并 `def`（⚠️ **合并前先留命中率基线**）**"
> 🔴 **为什么要先留**：我们（疑似）把原版**一个 `def`** 拆成了 **`PhysDef`（减伤）+ `Dodge`（命中）** 两个字段
> （一手证据：参考项目 `Character.cs` 的 `Character.Dodge` 读的就是 `AttributeType.DefenseRating`）
> ⇒ 合并**会改判定读数** ⇒ **没有"合并前"的数字，就分不清"对齐成功"与"改坏了"** ✓

## 0. 基线的来源与口径（可复核）
```
代码路径（真实路径，不是我推的公式）：
  `BattleProjector.HitRateFor(targetDodge, hitMod)` ⇒ `Core.Math.BattleMath.HitRate(dodge, hitMod, clampMin, clampMax)`
  `Core.Math.BattleMath.PhysicalMitigation(physDef, divisor)`
参数取自数据：`tuning.json` ⇒ `BalanceTable.FromTuning(...)`
  · **命中钳制 = [55, 100]** ✓　· **物理减伤除数 = 30** ✓
单位取自数据：`units.json` ⇒ **7 个单位**（我方 4：warrior/tank/medic/commissar · 敌方 3：melee_soldier/ranged_archer/caster）✓
读数用例：`darkest/tests/DefMergeBaselineTests.cs`（**只拍照、不改数值**；并把两个关键值**钉死**防静默漂移 ✓）
```
## 1. 命中% 基线（**只看目标 `Dodge`**；攻击方 atk 不影响命中 ✓）
| 目标 | dodge | 命中%（hitMod 0） | 物理减伤% | phys_def |
|---|---|---|---|---|
| warrior | 10 | **90** | 21 | 8 |
| tank | 5 | **95** | 29 | 12 |
| medic | 10 | **90** | 12 | 4 |
| commissar | 10 | **90** | 14 | 5 |
| melee_soldier | 10 | **90** | 21 | 8 |
| ranged_archer | 15 | **85** | 12 | 4 |
| caster | 10 | **90** | 9 | 3 |

```
⇒ 矩阵读数共 **28 条**（4 攻击方 × 7 防御方）—— 攻击方只改"打击量"，**不改命中/减伤**（两者只由目标决定 ✓）
⇒ 例：`warrior(atk 12) → tank(dodge 5 / phys_def 12) ⇒ 命中 95% · 减伤 29%` ✓
```
## 2. 合并 `def` 之后，这份基线怎么用（**对照口径**）
```
✅ **合并后重跑同一用例** ⇒ 与本表**逐行对照**：
   · 若"命中% 列"变化 ⇒ 说明**命中口径**动了（原版 `def` = Dodge ⇒ 命中应保持同源 ✓）
   · 若"减伤% 列"变化 ⇒ 说明**减伤口径**动了（原版减伤是 `prot` ⇒ `PhysDef` 的去留是**决策点** ✓）
🔴 **不允许静默漂移**：用例里钉了 `dodge 10 ⇒ 90%`、`dodge 0 ⇒ 100%`、`触下限` 三条；
   真要对齐 ⇒ **按登记表逐条改**（解冻四件之一：前后读数对照 ✓）
```
## 3. 参考项目给出的一手事实（合并方向的依据）
```
· 原版**只有一个** `def`（= 我们的 `Dodge`）　⇒ `PhysDef` 是**我们自加**的拆分结果
· 原版的**减伤**走的是 `prot`：`Protection = Clamp(prot, -1, max(0.85, raw))` ⇒ **0~0.85 的比例**（封顶 85%）✓
· 4 个原型对到的原版职业：**`prot` 均为 0**（`ManAtArms`/`PlagueDoctor`/`Hellion`/`Highwayman` ✓）
  ⇒ ⚠️ 即：若严格照原版，四原型的**减伤为 0**，而我们现在是 9%~29% ⇒ **这是"对齐会让减伤归零"的重大读数变化** ✓
  ⇒ 🔴 **所以这必须先由策划/架构裁定**（不是工程能自行决定的平衡问题）✓
```
## 4. 结论（我这边的状态）
```
✅ **前置条件已满足**：基线已拍、已入库、已钉住关键值（合并前后可逐条对照 ✓）
⏳ **等裁定**：`PhysDef` 是①退场（严格照原版，减伤归 0 或改走 `prot`）②还是保留并改名/改语义
   ⇒ 无论哪条，**这份基线都是对照的"合并前"一侧** ✓
```
