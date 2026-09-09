using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 命中步（T-M2-04）：命中率 = 100 − 目标闪避 + 技能命中修正，钳制 [55,100]（tuning hit_clamp）；
/// `roll &lt; 命中率` 算成功（阈值本身不命中）。零 Godot 引用。
/// </summary>
public static class HitStep
{
    public static bool Resolve(UnitRuntime attacker, UnitRuntime target, int hitMod,
        IRngProvider rng, CombatLog log, BalanceTable balance)
    {
        int rate = BattleMath.HitRate(target.Base.Dodge, hitMod, balance.HitClampMin, balance.HitClampMax);
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));
        bool hit = roll < rate;
        log.Append(new HitEvent(hit, rate, attacker.Id, target.Id));
        return hit;
    }
}