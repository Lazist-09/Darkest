using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Buffs;

/// <summary>
/// F2（#193）折磨 proc 执行器：`prob_mod` 的通用落地——按 `tuning.affliction_proc_percent` 掷骰，
/// **必写 `RngDraw`**（确定性红线：同 seed 可复现）。三种折磨各自在自己的时机调用本类：
/// 恐惧（使用技能前）/ 自私（被治疗或鼓舞时）/ 失控（攻击时）。
/// </summary>
public static class AfflictionProcs
{
    /// <summary>掷骰判定是否触发该折磨的 proc（持有该折磨才掷；勇猛 immune_fear 只豁免恐惧）。</summary>
    public static bool Triggered(IBuffLedger? buffs, BalanceTable balance, UnitId unit, string affliction,
        IRngProvider rng, CombatLog log)
    {
        if (buffs is null || !buffs.Has(unit, affliction))
        {
            return false;
        }

        if (affliction == "affliction_fear" && buffs.HasStateFlag(unit, "immune_fear"))
        {
            return false; // 勇猛豁免恐惧（不掷骰，避免无意义抽取）
        }

        int proc = balance.Tuning.AfflictionProcPercent; // 唯一来源（禁硬编码 33）
        int roll = rng.NextInt(0, 100);
        log.Append(new RngDraw(rng.DrawCount, roll));
        bool triggered = roll < proc;
        if (triggered)
        {
            log.Append(new EffectEvent(unit, affliction + "_proc", proc, true));
        }

        return triggered;
    }
}