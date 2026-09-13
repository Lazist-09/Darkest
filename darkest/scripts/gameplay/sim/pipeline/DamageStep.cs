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
        BalanceTable balance,
        Darkest.Core.Contracts.IBuffLedger? buffs = null,
        string? skillId = null,
        int bonusVsMarkedPercent = 0)
    {
        if (multipliers is null || multipliers.Count == 0)
        {
            throw new ArgumentException("damage.segments 不可为空（M2 夹具必填）。");
        }

        // F2（#193）：buff modifiers 通用消费——暴击 +15pp（专注）/ 伤害 +25%（勇猛）/ 下次攻击 +20%（突进，消耗后移除）
        int critBonus = buffs?.PercentMod(attacker.Id, "crit_bonus") ?? 0;
        int dealtMult = buffs?.PercentMod(attacker.Id, "dealt_damage_mult") ?? 0;
        int nextAttackMult = buffs?.PercentMod(attacker.Id, "next_attack_mult") ?? 0;
        double buffDamageMult = 1.0 + (dealtMult + nextAttackMult) / 100.0;

        // D2（#204）Mark：目标带 mark → 声明 bonus_vs_marked_percent 的技能加伤（乘法阶段）
        if (bonusVsMarkedPercent > 0 && buffs is not null && buffs.Has(target.Id, "mark"))
        {
            buffDamageMult *= 1.0 + bonusVsMarkedPercent / 100.0;
        }

        // D4（#206）死门后遗症：承受伤害 +10%
        int takenMult = buffs?.PercentMod(target.Id, "taken_damage_mult") ?? 0;
        if (takenMult != 0)
        {
            buffDamageMult *= 1.0 + takenMult / 100.0;
        }

        int total = 0;
        bool anyCrit = false;
        for (int i = 0; i < multipliers.Count; i++)
        {
            int critRate = Math.Clamp(attacker.Base.Crit + critMod + critBonus, 0, 100);
            double critRoll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, critRoll));
            bool crit = critRoll < critRate;
            anyCrit |= crit;
            log.Append(new CritEvent(crit, attacker.Id, target.Id));

            double critMult = crit ? balance.CritMultiplier : 1.0;
            double dmgFloat = 1.0;
            if (balance.DamageFloatEnabled)
            {
                // 伤害浮动（O-01 默认关闭）：开启时 0.9~1.1（tuning damage_float），固定调用点抽取
                double floatRoll = rng.NextPercent();
                log.Append(new RngDraw(rng.DrawCount, floatRoll));
                double t = floatRoll / 100.0;
                dmgFloat = balance.Tuning.DamageFloat.Min + t * (balance.Tuning.DamageFloat.Max - balance.Tuning.DamageFloat.Min);
            }

            double raw;
            if (axis == "mental")
            {
                double mitig = BattleMath.MentalMitigation(
                    target.EffectiveResilience, balance.MentalReductionDivisor, balance.MentalReductionCapPercent);
                raw = attacker.EffectiveAttack * multipliers[i] * (1.0 - mitig) * critMult * dmgFloat * buffDamageMult; // §2.2 + F2 buff 乘法阶段
            }
            else
            {
                double mitig = BattleMath.PhysicalMitigation(target.EffectivePhysDef);
                raw = attacker.EffectiveAttack * multipliers[i] * (1.0 - mitig) * critMult * dmgFloat * buffDamageMult; // §2.1 + F2
            }

            // 🔴 M8.0 ③（`#289` 裁定 (B)）：**特质伤害修正**与 `buffDamageMult` **同层相乘**
            //    —— 特质与 buff 是同一层东西（都是"单位状态"，不属于数学层 ⇒ 不给 BattleMath 加参）
            raw *= attacker.DamageMultiplier;

            if (attacker.Weak)
            {
                raw *= balance.WeakDamageMultPercent / 100.0; // 虚弱单位输出 −50%（tuning weak.damage_mult）
            }

            int damage = BattleMath.ApplyDamageRounding(raw + attacker.DamageCarry, balance.DamageFloor); // §2.3
            // 🔴 ㉟ 对称性：把本次被【取整 + 下限】丢掉的小数**结转**到下一次命中
            attacker.DamageCarry = raw + attacker.DamageCarry - damage;
            if (!target.Weak)
            {
                target.CurrentHp -= damage; // 虚弱目标：不改 HP（恒 1），直接进死门（O-15）
            }

            total += damage;
            log.Append(new DamageEvent(target.Id, damage, raw, crit, i, axis, attacker.Id, skillId)); // G0：技能来源
        }

        // F2：下次攻击增伤为一次性——本动作命中后移除
        if (buffs is not null && nextAttackMult > 0)
        {
            buffs.Remove(attacker.Id, "next_attack_boost");
        }

        return new DamageOutcome(total, anyCrit, target.Weak, multipliers.Count);    }
}