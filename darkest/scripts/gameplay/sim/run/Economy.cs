using System;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// M8.0 ②（`#283` 7.1 + 硬要求①）：**金钱（跨趟状态）**。
///
/// 🔴 归属（`blueprint §9.15`）：**经济 = 跨会话状态**，与"一趟"、"单场纯"**分属不同拥有者** ——
/// 组合根持有本对象并注入；**`BattleDirector` 不持有它**（不得反向依赖）。
///
/// 🔴 来源（硬要求①）：**与【光照档 + 战斗数】挂钩** ——
/// · **战斗数**：每打赢一场 `battle_reward`（由远征流程每场调用一次 ⇒ 打得越多越多）；
/// · **光照档**：再按当前档加 `light_tier_bonus`（**越暗越多** ⇒ "冒险"有了**可跨趟积累**的回报）。
/// 所有变更**必写 `GoldChangedEvent`**（数字必须来自事件流）。
/// </summary>
public sealed class Economy
{
    private readonly EconomyConfig _cfg;

    public Economy(EconomyConfig config, int gold = 0)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        Gold = Math.Max(0, gold);
    }

    /// <summary>当前金钱（跨趟持有）。</summary>
    public int Gold { get; private set; }

    /// <summary>本对象已记入事件流的累计变更（供对账；与 <see cref="Gold"/> 同步）。</summary>
    public int AwardedTotal { get; private set; }

    /// <summary>打赢一场的金钱（`battle_reward + light_tier_bonus[档]`）—— 战斗数 × 光照档。</summary>
    public int RewardFor(string tierId) => _cfg.RewardFor(tierId);

    /// <summary>记一场胜利的金钱（远征流程每场调用一次）；返回本次所得。</summary>
    public int AwardBattle(CombatLog log, string tierId, string reason = "battle")
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        int delta = RewardFor(tierId);
        if (delta != 0)
        {
            Gold += delta;
            AwardedTotal += delta;
            log.Append(new GoldChangedEvent(delta, reason, Gold));
        }

        return delta;
    }

    /// <summary>花钱（回城减压 / 其它）；不足即**拒绝且不扣**（返回 false）。</summary>
    public bool TrySpend(CombatLog log, int amount, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (amount <= 0 || Gold < amount)
        {
            return false;
        }

        Gold -= amount;
        log.Append(new GoldChangedEvent(-amount, reason, Gold));
        return true;
    }

    /// <summary>一次减压的价格（7.1：先定比例 —— 一次减压 = 3 单位）。</summary>
    public int StressReliefCost => _cfg.StressReliefCost;
}
