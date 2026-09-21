using System;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Survival;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>侦察判定结果（D1）。</summary>
public sealed record ScoutOutcome(bool Success, double Roll, string? RevealedNodeType);

/// <summary>
/// M7.5 D1 侦察（`#259`）：基础命中率 + **光照加成**；**只揭示【下一个】节点类型**。
/// 🔴 两条硬约束（策划 v1.02 表态 + `data_schema` §3.11）：
/// ① **只揭示下一节点** —— 不得揭示更远、不得改变节点内容（否则选路退化成"照着最优解走"，决策消失）；
/// ② **`success == false` ⇒ `revealedNodeType` 必须为 `null`**（避免"失败却也带类型"的歧义）。
/// 🔴 每次判定**必写 `RngDraw`**（确定性红线）。
/// </summary>
public interface IScouting
{
    ScoutOutcome Roll(CombatLog log, IRngProvider rng, int lightValue, string? nextNodeType);
}

/// <summary>默认实现（纯函数式，不持状态）。</summary>
public sealed class Scouting : IScouting
{
    private readonly TuningScouting _config;
    private readonly TuningLight _light;

    public Scouting(TuningScouting config, TuningLight light)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _light = light ?? throw new ArgumentNullException(nameof(light));
    }

    /// <summary>当前光照下的侦察概率 = 基础（25%）+ 该档光照加成（Radiant +15 / Dim +7.5 / 其余 0）。</summary>
    public double ChanceFor(int lightValue)
        => _config.BasePct + new LightMeter(_light, lightValue).Effect.ScoutingPct;

    public ScoutOutcome Roll(CombatLog log, IRngProvider rng, int lightValue, string? nextNodeType)
    {
        double chance = ChanceFor(lightValue);
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 确定性红线：侦察判定必写

        bool success = roll < chance;
        // 🔴 失败必须为 null；成功也**只揭示下一个节点类型**（不得外扩）
        string? revealed = success ? nextNodeType : null;
        log.Append(new ScoutResultEvent(roll, success, revealed));
        return new ScoutOutcome(success, roll, revealed);
    }
}
