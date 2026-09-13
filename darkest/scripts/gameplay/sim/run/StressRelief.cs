using System;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>减压结果（M8.0 ④）。</summary>
public sealed record StressReliefOutcome(bool Paid, int NewMorale, bool PenaltyTriggered, int NextRunPenalty);

/// <summary>
/// M8.0 ④（`#283` 7.3 + 硬要求②）：**回城减压**。
///
/// 🔴 规格要点：**Tavern 与 Abbey 同价同效、风险不同**（照抄 DD）⇒ **"选风格"而非"选更优"**；
/// 副作用 = **下一趟开局士气 −N**（由 `penalty_chance` 决定是否触发 —— DD 里酒馆更"快而不稳"）。
///
/// 🔴 纪律：副作用是**随机** ⇒ **必写 `RngDraw`**（红线：所有新随机必写 + 预览不掷骰）；
/// 结算**必写 `StressReliefEvent`**（数字必须来自事件流）；钱不够 ⇒ **拒绝且不扣、不掷骰**。
/// </summary>
public static class StressRelief
{
    public static StressReliefOutcome Apply(EconomyConfig config, Economy economy, IRngProvider rng,
        CombatLog log, string buildingId, string heroId, int currentMorale)
    {
        if (config is null || economy is null || rng is null || log is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        StressReliefBuildingConfig b = config.Building(buildingId);

        // 钱不够 ⇒ 拒绝（**不掷骰、不扣钱、不写结算事件** —— 不给"白掷"的机会）
        if (!economy.TrySpend(log, b.Cost, "stress_relief"))
        {
            return new StressReliefOutcome(false, currentMorale, false, 0);
        }

        int restored = Math.Clamp(currentMorale + b.MoraleRestore, 0, 100);

        // 🔴 掷骰约定（IRngProvider 契约）：`NextPercent()` ∈ [0,100)，**roll 小于阈值 = 触发**
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 随机必写

        bool triggered = roll < (b.PenaltyChance * 100.0);
        int penalty = triggered ? b.NextRunPenalty : 0;
        log.Append(new StressReliefEvent(b.Id, heroId, b.Cost, b.MoraleRestore, roll, triggered, penalty));

        return new StressReliefOutcome(true, restored, triggered, penalty);
    }

    /// <summary>下一趟开局士气（`#283` 7.3：副作用 = **下一趟开局士气 −N**）。</summary>
    public static int NextRunOpeningMorale(int morale, int nextRunPenalty)
        => Math.Clamp(morale - Math.Max(0, nextRunPenalty), 0, 100);
}
