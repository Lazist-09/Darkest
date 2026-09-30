using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>特质效果汇总（M8.0 ①，`#283` 7.7）：伤害%（正=更强）与**受到**士气伤害%（正=更脆）。</summary>
public sealed record TraitEffects(int DamagePct, int MoraleDamagePct)
{
    public static readonly TraitEffects None = new(0, 0);

    public bool IsNone => DamagePct == 0 && MoraleDamagePct == 0;
}

/// <summary>
/// M8.0 ①(b)（`#286`）：**英雄个体 → 单位运行时的投影**（组合根在装配时调用）。
///
/// 🔴 三条约定（与既有 `ApplyMaxHpMultiplier` / `ApplyStunResistBonus` 同法）：
/// · **运行时投影，不改基准数据**（P4 同源：`units.json` 永远是原型基准）；
/// · **`BattleDirector` 保持单场纯** —— 本类不持状态、不读名册；组合根把"名册选出的快照"喂进来即可；
/// · **只给属性**（7.6：HP+2/攻击+1 每级）；🔴 **不升技能**（7.8）。
///
/// ⚠️ 本切片只接线**等级成长**；**特质的两项效果（伤害% / 受士气伤害%）已可计算**（<see cref="TraitEffectsOf"/>），
/// 但**尚未接进伤害管线**（那需要与 `damage_mod` / 士气通道对齐）⇒ 留下一步，**不在此处近似**（红线 19：不给模糊实现）。
/// </summary>
public static class HeroProjection
{
    /// <summary>按英雄等级投影到单位（HP +2/级、攻击 +1/级，幂等前提：每次装配只调一次）。</summary>
    public static void ApplyLevel(HeroConfig hero, UnitRuntime unit, RosterLevelGrowth growth)
    {
        if (hero is null || unit is null || growth is null)
        {
            return;
        }

        unit.ApplyLevelGrowth(hero.Level, growth.HpPerLevel, growth.AttackPerLevel);
    }

    /// <summary>
    /// 🆕 **H-1（2026-09-27）：装备阶 → 单位减伤读数**（"当前阶"问题的**减伤半** ✓）。
    ///
    /// 🔴 **为什么挂这里**（与 `ApplyLevel` 同法）："英雄个体 → 单位运行时"的投影**本来就在这层**，
    ///   阶的取值为 `HeroConfig.ArmourTier`（`O-101` 裁定：由 Roster/Hero 持有 ✓）⇒ 无需新载体 ✓
    /// 🔴 **无 5 阶（敌人）⇒ 退回顶层** ⇒ **敌人零影响**（`TierDefence` 的既有语义 ✓）
    /// ⚠️ **玩家单位是行为变更**：顶层 `prot` 8/12/4/5 → 护甲第 0 阶 **0**（差异已实测登记，
    ///    `reports/top_level_vs_tier0_consistency.md` §1；策划裁定"直接接线"✓）
    /// </summary>
    public static void ApplyGearTier(HeroConfig hero, UnitRuntime unit)
    {
        if (hero is null || unit is null)
        {
            return;
        }

        unit.ApplyGearTier(hero.ArmourTier);
    }

    /// <summary>
    /// M8.0 ③（`#289` 裁定 (B)）：**特质 → 单位修正** —— 伤害类与士气类**各归其道**：
    /// · 伤害% ⇒ `DamageModPct`（在 `DamageStep` 里与 `buffDamageMult` **同层相乘**）；
    /// · 受士气伤害% ⇒ `MoraleDamageTakenPct`（供士气通道读取）。
    /// 🔴 仍是**运行时投影**（不改基准数据）；幅度由 P22 ③ 可测定义约束（≤15% / ≤20%）。
    /// </summary>
    public static TraitEffects ApplyTraits(HeroConfig hero, UnitRuntime unit)
    {
        TraitEffects eff = TraitEffectsOf(hero);
        if (eff.IsNone || unit is null)
        {
            return eff;
        }

        unit.ApplyTraitDamagePct(eff.DamagePct);
        unit.ApplyTraitMoraleDamagePct(eff.MoraleDamagePct);
        return eff;
    }

    /// <summary>
    /// M8.2 / V15：**按"当前特质效果"投影**（来源可以是**可变的名册**，而不是只读的 `HeroConfig`）——
    /// 这样 Sanitarium 清除/固化特质后，**下一次出征立刻反映**（否则又是"写了但没接上"，红线 21）。
    /// </summary>
    public static TraitEffects ApplyTraitEffects(TraitEffects effects, UnitRuntime unit)
    {
        if (effects is null || effects.IsNone || unit is null)
        {
            return effects ?? TraitEffects.None;
        }

        unit.ApplyTraitDamagePct(effects.DamagePct);
        unit.ApplyTraitMoraleDamagePct(effects.MoraleDamagePct);
        return effects;
    }

    /// <summary>
    /// M8.2（**V14**）：**疾病 → 单位**（属性惩罚投影；运行时投影、不改基准）。返回实际施加的惩罚。
    /// </summary>
    public static DiseasePenalty ApplyDisease(DiseasePenalty penalty, UnitRuntime unit)
    {
        if (penalty is null || penalty.IsNone || unit is null)
        {
            return penalty ?? new DiseasePenalty();
        }

        unit.ApplyDiseasePenalty(penalty.HpDelta, penalty.AttackDelta, penalty.MoraleDelta);
        return penalty;
    }

    /// <summary>汇总英雄特质效果（供伤害管线 / 士气通道接线时读取）。</summary>
    public static TraitEffects TraitEffectsOf(HeroConfig hero)
    {
        if (hero?.Traits is null || hero.Traits.Count == 0)
        {
            return TraitEffects.None;
        }

        return new TraitEffects(
            hero.Traits.Sum(t => t.DamagePct),
            hero.Traits.Sum(t => t.MoraleDamagePct));
    }
}
