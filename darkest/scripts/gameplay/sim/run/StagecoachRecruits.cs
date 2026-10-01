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

    /// <summary>
    /// 🔴 **M7③（`dd1_workstreams.md §3` · M5u 2026-10-01）· 高级新兵带 Quirk** ——
    /// 从 `quirks.json` 里**掷一条怪癖**（**纯函数 · 零 Godot · 可单测** ✓；同种子同结果 ✓）。
    ///
    /// 🔴 **候选池口径（如实标注：过滤规则是【我方推的】，属 `#307` 家族）**：
    ///   ① `is_disease == true` 的 23 条**不进池**（疾病走 `Roster.Infect` ／ 疗养院，不走招募）✓
    ///   ② `random_chance &lt;= 0` 的 6 条不进池（数据自己声明"不可随机获得"⇒ 尊重数据 ✓）
    ///   ③ 与**现持**任一条互斥的候选**不进池**（判据 = `QuirksConfig.AreIncompatible`，单一落点 ✓）
    ///   ⇒ 一手实测：170 条里 **141 条**可进池（读数见 `reports/m5u_quirk_ui_20261001.md`）✓
    ///
    /// ⚠️ 当前唯一生产调用点 = 高级新兵（新兵**现持恒为空集** ⇒ ③ 在招募路径上尚不触发）——
    /// 但它是"**同一英雄永不出现互斥怪癖**"这条不变式的**唯一守卫** ⇒ 怪癖一旦有第二个来源
    /// （curio ／ 趟末）即生效；已登记观察项（不假装它今天在跑）✓
    /// </summary>
    /// <returns>抽中的怪癖 id；池为空 ⇒ `null`（如实回答"抽不出"，不编一条）✓</returns>
    public static string? RollQuirk(QuirksConfig quirks, IReadOnlyCollection<string> current, IRngProvider rng)
    {
        ArgumentNullException.ThrowIfNull(quirks);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(rng);

        var pool = new List<string>();
        foreach (QuirkConfig q in quirks.Quirks)
        {
            if (q.IsDisease || q.RandomChance <= 0)
            {
                continue; // ① + ② ⇒ 不进池
            }

            bool conflicts = false;
            foreach (string held in current)
            {
                if (quirks.AreIncompatible(q.Id, held))
                {
                    conflicts = true;
                    break;
                }
            }

            if (!conflicts)
            {
                pool.Add(q.Id); // ③ ⇒ 不与现持互斥才进池
            }
        }

        return pool.Count == 0 ? null : pool[rng.NextInt(0, pool.Count)];
    }
}
