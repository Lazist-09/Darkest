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

    /// <summary>汇总英雄特质效果（供伤害管线 / 士气通道接线时读取；本切片只计算、不施加）。</summary>
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
