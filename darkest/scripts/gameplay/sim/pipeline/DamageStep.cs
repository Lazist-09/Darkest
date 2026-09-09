using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>单次技能命中的伤害汇总（T-M2-05）。</summary>
public sealed record DamageOutcome(int TotalDealt, bool AnyCrit, bool TargetIsWeak, int SegmentCount);

/// <summary>
/// 伤害结算主链（T-M2-05）：暴击步（roll &lt; 暴击率 → ×crit_multiplier）→ 物理/精神双减免
/// → 段独立成伤（O-13 各段独立实例）→ 取整下限 damage_floor → 虚弱者输出 −50%
/// （tuning weak.damage_mult）→ 虚弱目标不改 HP（O-15 已定：直接进死门）。零 Godot 引用。
/// </summary>
public static class DamageStep
{
    public static DamageOutcome Deal(
        UnitRuntime attacker,
        UnitRuntime target,
        string axis,
        IReadOnlyList<double> multipliers,
        int critMod,
        IRngProvider rng,
        CombatLog log,
        BalanceTable balance)
    {
        if (multipliers is null || multipliers.Count == 0)
        {
            throw new ArgumentException("damage.segments 不可为空（M2 夹具必填）。");
        }

        int total = 0;
        bool anyCrit = false;
        for (int i = 0; i < multipliers.Count; i++)
        {
            int critRate = Math.Clamp(attacker.Base.Crit + critMod, 0, 100);
            double critRoll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, critRoll));
            bool crit = critRoll < critRate;
            anyCrit |= crit;
            log.Append(new CritEvent(crit, attacker.Id, target.Id));

            double critMult = crit ? balance.CritMultiplier : 1.0;
            double raw;
            if (axis == "mental")
            {
                double mitig = BattleMath.MentalMitigation(
                    target.EffectiveResilience, balance.MentalReductionDivisor, balance.MentalReductionCapPercent);
                raw = attacker.EffectiveAttack * multipliers[i] * (1.0 - mitig) * critMult; // §2.2
            }
            else
            {
                double mitig = BattleMath.PhysicalMitigation(target.EffectivePhysDef);
                raw = attacker.EffectiveAttack * multipliers[i] * (1.0 - mitig) * critMult; // §2.1
            }

            if (attacker.Weak)
            {
                raw *= balance.WeakDamageMultPercent / 100.0; // 虚弱单位输出 −50%（tuning weak.damage_mult）
            }

            int damage = BattleMath.ApplyDamageRounding(raw, balance.DamageFloor); // §2.3
            if (!target.Weak)
            {
                target.CurrentHp -= damage; // 虚弱目标：不改 HP（恒 1），直接进死门（O-15）
            }

            total += damage;
            log.Append(new DamageEvent(target.Id, damage, raw, crit, i, axis));
        }

        return new DamageOutcome(total, anyCrit, target.Weak, multipliers.Count);
    }
}