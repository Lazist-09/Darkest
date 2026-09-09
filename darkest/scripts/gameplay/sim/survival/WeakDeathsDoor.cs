using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Pipeline;

namespace Darkest.Gameplay.Sim.Survival;

/// <summary>
/// 虚弱 / 死门（T-M2-09）：HP 归零（非虚弱，仅我方）→ 不死亡：士气置 0 + 崩溃判定调用点
/// （CollapseRollEvent，池解析归 M4）→ 进虚弱：伤害 −50%/速度 −30%/HP 锁 1（tuning weak）；
/// 虚弱中再受伤 → 死门 roll &lt; 存活概率（抗性 − 折磨 10%，afflicted 由 M4 buff 语义注入，
/// M2 恒 false）；O-15 已定 HP 算术；敌方无虚弱/死门。零 Godot 引用。
/// </summary>
public static class WeakDeathsDoor
{
    public static void EnterWeak(UnitRuntime unit, MoraleLedger ledger,
        IRngProvider rng, CombatLog log, BalanceTable balance)
    {
        // 事件序（combat_math §6）：HP 归零 → 士气立即置 0 → 崩溃判定 → 进入虚弱
        ledger.Apply(unit, -unit.Morale, "weak_enter_morale_zero", log);

        double collapseRoll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, collapseRoll));
        log.Append(new CollapseRollEvent(unit.Id, collapseRoll)); // 调用点（M4 解析美德/折磨池）

        unit.Weak = true;
        unit.CurrentHp = balance.WeakHpLock; // HP 锁 1
        log.Append(new WeakEnterEvent(unit.Id));
    }

    /// <summary>死门判定：仅虚弱中受伤时调用；返回是否存活（失败 = 真死）。</summary>
    public static bool Roll(UnitRuntime unit, bool afflicted,
        IRngProvider rng, CombatLog log, BalanceTable balance)
    {
        int survivePercent = BattleMath.DeathDoorSurvivePercent(
            unit.Base.DeathsDoorResist ?? 0,
            afflicted,
            balance.DeathsDoorAfflictionPenaltyPercent); // #123 折磨 −10%

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));
        bool survived = roll < survivePercent; // data_schema §2.3
        log.Append(new DeathDoorEvent(unit.Id, survivePercent, roll, survived));
        return survived;
    }
}