using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 🔴 **M1c 阶段 3 的【纯机制】**：`武器区间 × (1 + 技能 dmg%)` ✓（架构裁"**阶数要影响伤害**" ✓）
///
/// WHY 只做纯函数、**不接生产**：
///   阶段 3 = "**切换默认**" ⇒ 走**解冻四件**（逐条登记 + 原版证据 + 前后读数 + 改完回冻结）✓
///   🔴 而它现在有**两个硬前置**（我实测出来的，**不硬造** ✓）：
///     ① **单位没有"当前阶"** ✗ —— `UnitRuntime` 只有 `EffectiveAttack`（`AttackMod` ✓），
///        **没有 `tier` 这个概念** ⇒ 而"武器区间**按阶取**"必须先有阶 ✓
///        （一手数据里 5 阶是**装在 `UnitStats.WeaponTiers` 上**的 ✓ ⇒ 缺的是"**这个单位现在第几阶**"）
///     ② **技能 `dmg%` 数据未给** ✗ —— 策划只给了**两个试点值**（`smite 0%` / `zealous_accusation −40%` ✓），
///        而我们 23 个技能的 `dmg%` **还没有** ✓
///   ⇒ 所以本件只提供**可单测的机制**：给定（阶 · dmg% · 掷骰）算出基础伤害 ✓
///      ⇒ 阶段 3 真正落地时，"取阶 + 取 dmg%"两步接上即可 ✓
///
/// 🔴 **零行为**：本文件**无生产调用方**（激活条件见上 ✓）
/// </summary>
public static class WeaponBaseDamage
{
    /// <summary>
    /// 新模型的基础伤害（**未取整**）：`Σ段倍率 × 武器区间 × (1 + dmg%/100)` ✓
    /// </summary>
    /// <param name="tiers">武器的 5 阶（一手数据 ✓）</param>
    /// <param name="tier">**当前阶**（0~4 ✓；越界会被钳制 ✓）</param>
    /// <param name="sumSegmentMultipliers">技能段倍率之和（旧模型里乘在 `attack` 上的那个 ✓）</param>
    /// <param name="dmgPct">技能 `dmg%`（策划给；`smite 0` / `zealous_accusation −40` ✓）</param>
    /// <param name="roll01">区间内掷骰（0~1 ✓ 确定性由调用方的 rng 保证 ✓）</param>
    public static double Raw(
        IReadOnlyList<WeaponTier> tiers,
        int tier,
        double sumSegmentMultipliers,
        int dmgPct,
        double roll01)
    {
        if (tiers is null || tiers.Count == 0)
        {
            throw new ArgumentException("武器 5 阶不可为空（M1c 阶段 3 需要区间 ✓）。", nameof(tiers));
        }

        WeaponTier w = tiers[Math.Clamp(tier, 0, tiers.Count - 1)];
        double mid = Darkest.Core.Math.BattleMath.WeaponRoll(w.DmgMin, w.DmgMax, roll01);
        return mid * sumSegmentMultipliers * (1.0 + dmgPct / 100.0);
    }

    /// <summary>同上 + 取整（与旧路径同一套取整 ✓ ⇒ 可逐位对照 ✓）</summary>
    public static int Damage(
        IReadOnlyList<WeaponTier> tiers,
        int tier,
        double sumSegmentMultipliers,
        int dmgPct,
        double roll01,
        int damageFloor = 1)
        => Darkest.Core.Math.BattleMath.ApplyDamageRounding(Raw(tiers, tier, sumSegmentMultipliers, dmgPct, roll01), damageFloor);
}
