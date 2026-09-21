using System;
using System.Collections.Generic;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **M7 第 ③ 步（策划 `#423`）· 今日新兵的生成规则**（**纯函数 · 零 Godot · 可单测** ✓）。
///
/// 数据（全部来自策划给的三条曲线，`economy.json` 的 `stagecoach` ✓）：
///   · `num_recruits_by_level`        = **2 → 3 → 4 → 5 → 6 → 7**（索引 = 马车等级 ✓）
///   · `upgraded_recruit_chances_pct` = **18.75 / 12.5 / 6.25 %**（`upgraded_recruits` 的 a/b/c ✓）
///   · `rookie_level`                 = 基础新兵等级（缺省 1 ✓）
///
/// 🔴 **确定性**（`#423` 要求"用 `IRngProvider` 掷"✓）：每次"是否高级"都用**传入的 rng** 掷一次，
///   并把每一次掷骰写进 `CombatLog`（`StagecoachRecruitRolledEvent`）⇒ **可复现、可审计** ✓
///
/// 🔴 **零行为**：本件目前**无人调用**（招募屏在 UI 域）⇒ 激活条件写在下方 ✓
/// </summary>
public static class StagecoachRecruits
{
    /// <summary>一个招募位：`Upgraded=true` ⇒ 高级新兵（起始等级 = 基础 + 1 ✓）。</summary>
    public readonly record struct Offering(bool Upgraded);

    /// <summary>曲线缺省值（与 `economy.json` 一致；数据缺失时的忠实退化 ⇒ 不是新数值 ✓）。</summary>
    private const int DefaultRecruits = 2;

    /// <summary>
    /// **今日新兵**（确定性 ✓）：数量取 `曲线[马车等级]`，逐个掷"是否高级" ✓
    /// </summary>
    /// <param name="coach">马车配置（三条曲线都在这里 ✓）</param>
    /// <param name="stagecoachLevel">马车等级（越界会被钳制 ✓）</param>
    /// <param name="rng">随机源（**必须传入** ⇒ 保证确定性/可复现 ✓）</param>
    /// <param name="log">可选：写入每次掷骰（审计 ✓）</param>
    public static IReadOnlyList<Offering> Roll(
        StagecoachConfig coach,
        int stagecoachLevel,
        IRngProvider rng,
        CombatLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(coach);
        ArgumentNullException.ThrowIfNull(rng);

        int count = CountAt(coach, stagecoachLevel);
        double upgradedPct = UpgradedChancePctAt(coach, stagecoachLevel);

        var list = new List<Offering>(count);
        for (int i = 0; i < count; i++)
        {
            double draw = rng.NextPercent();
            bool upgraded = draw < upgradedPct;
            log?.Append(new StagecoachRecruitRolledEvent(i, stagecoachLevel, draw, upgradedPct, upgraded));
            list.Add(new Offering(upgraded));
        }

        return list;
    }

    /// <summary>该等级的新兵**数量**（曲线缺省/越界 ⇒ 忠实退化到未升级档 ✓）</summary>
    public static int CountAt(StagecoachConfig coach, int level)
    {
        if (coach.NumRecruitsByLevel is not { Count: > 0 } curve)
        {
            return DefaultRecruits;
        }

        return curve[Math.Clamp(level, 0, curve.Count - 1)];
    }

    /// <summary>该等级的**高级新兵概率（百分比）**；曲线缺失 ⇒ 0（= 不出现高级 ⇒ 忠实退化 ✓）</summary>
    public static double UpgradedChancePctAt(StagecoachConfig coach, int level)
    {
        if (coach.UpgradedRecruitChancesPct is not { Count: > 0 } curve)
        {
            return 0;
        }

        return curve[Math.Clamp(level, 0, curve.Count - 1)];
    }

    /// <summary>高级新兵的**起始等级**（= 基础新兵等级 + 1 ✓；`#423` 只给了概率，等级沿用原版"高级=+1"✓）</summary>
    public static int StartingLevel(StagecoachConfig coach, Offering offering)
        => coach.RookieLevel + (offering.Upgraded ? 1 : 0);
}
