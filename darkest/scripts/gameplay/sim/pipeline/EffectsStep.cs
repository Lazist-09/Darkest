using System;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 附加效果请求（M2 夹具，data_schema §3.2 effects 的最小形态）。
/// LabeledPercent=null = 无概率标注 → 直挂不掷骰（O-24 已定）。
/// </summary>
public sealed record EffectRequest(string Type, int? LabeledPercent, string ResistAxis, string? Stat, int Delta);

/// <summary>
/// 附加效果判定与登记（T-M2-06）：实际触发率 = 标注概率 × (1 − 对应抗性)（乘法，§4）；
/// roll &lt; 实际率；障碍不吃状态（由调用方以槽位三态跳过）；眩晕→Stunned、流血→登记回合数、
/// 属性减益→固定值修正（速度亦固定值，GDD §2.5）。零 Godot 引用。
/// </summary>
public static class EffectsStep
{
    public static bool Apply(UnitRuntime target, EffectRequest req,
        IRngProvider rng, CombatLog log, BalanceTable balance)
    {
        int resist = req.ResistAxis switch
        {
            "stun_resist" => target.Base.StunResist,
            "bleed_resist" => target.Base.BleedResist,
            "stat_debuff_resist" => target.Base.StatDebuffResist,
            _ => 0,
        };

        double actualChance;
        bool triggered;
        if (req.LabeledPercent is { } labeled)
        {
            actualChance = BattleMath.ActualEffectChance(labeled, resist);
            double roll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, roll));
            triggered = roll < actualChance;
        }
        else
        {
            actualChance = 100.0; // 无概率标注：直挂不掷骰
            triggered = true;
        }

        if (triggered)
        {
            switch (req.Type)
            {
                case "stun":
                    target.Stunned = true; // 跳过 1 次行动随即结束（GDD §2.5）
                    break;
                case "bleed":
                    target.BleedRoundsRemaining = balance.BleedRounds; // 每回合 3 点（tuning bleed）
                    break;
                case "stat_mod":
                    ApplyStatMod(target, req.Stat, req.Delta);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(req.Type), $"未知效果类型 {req.Type}。");
            }
        }

        log.Append(new EffectEvent(target.Id, req.Type, actualChance, triggered));
        return triggered;
    }

    private static void ApplyStatMod(UnitRuntime target, string? stat, int delta)
    {
        // 属性减益固定值（攻/防/韧/速；速度也走固定值，GDD §2.5 / combat_math §4）
        switch (stat)
        {
            case "attack":
                target.AttackMod += delta;
                break;
            case "phys_def":
                target.PhysDefMod += delta;
                break;
            case "resilience":
                target.ResilienceMod += delta;
                break;
            case "speed":
                target.SpeedMod += delta;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), $"未知属性修正 {stat}。");
        }
    }
}