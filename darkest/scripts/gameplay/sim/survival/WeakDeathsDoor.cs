using System;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Pipeline;

namespace Darkest.Gameplay.Sim.Survival;

/// <summary>
/// 虚弱 / 死门（T-M2-09 + T-M4-06）：HP 归零（非虚弱，仅我方）→ 不死亡：士气置 0 + 崩溃判定
/// （交由 MoraleLedger.TriggerCollapseForHpZero，T-M4-02 事件触发/去重；美德·折磨池挂 buff）
/// → 进虚弱：伤害 −50%/速度 −30%/HP 锁 1（tuning weak）；虚弱中再受伤 → 死门 roll &lt; 存活概率
/// （抗性 − 折磨 10%，afflicted 自 IBuffLedger 折磨标记注入）；O-15 已定 HP 算术；
/// 归队（#164）：士气 ≥ 初始值 → 脱离虚弱 → HP = 最大血量 weak_exit_hp_ratio。敌方无虚弱/死门。零 Godot 引用。
/// </summary>
public static class WeakDeathsDoor
{
    public static void EnterWeak(UnitRuntime unit, MoraleLedger ledger,
        IRngProvider rng, CombatLog log, BalanceTable balance)
    {
        // 事件序（combat_math §6）：HP 归零 → 士气立即置 0 → 崩溃判定 → 进入虚弱
        ledger.Apply(unit, -unit.Morale, "weak_enter_morale_zero", log);
        ledger.TriggerCollapseForHpZero(unit, rng, log); // T-M4-02：判定一次（与跨 0 路径去重）

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

    /// <summary>
    /// 归队（#164）：士气回升 ≥ 初始值（50）→ 脱离虚弱，HP = 最大血量 × weak_exit_hp_ratio（10%）；
    /// 返回是否归队。位置保持不变（换人保护语义，M4 回合钩子调用）。
    /// </summary>
    public static bool TryRecover(UnitRuntime unit, BalanceTable balance)
    {
        if (!unit.Weak)
        {
            return false;
        }

        if (unit.Morale < balance.MoraleStart)
        {
            return false;
        }

        unit.Weak = false;
        unit.CollapseEmber = false;
        unit.CurrentHp = Math.Max(1, (int)Math.Round(unit.MaxHp * balance.Tuning.WeakExitHpRatio, MidpointRounding.AwayFromZero));
        return true;
    }
}