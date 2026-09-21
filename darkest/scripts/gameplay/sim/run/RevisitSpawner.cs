using System;
using System.Collections.Generic;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>🔴 `D-2`：一次"重访掷骰"的结果（**空 = 没出事**，不是 null）✓</summary>
public sealed record RevisitRollResult(bool Triggered, string Threat, double PercentUsed, int TierIndex)
{
    /// <summary>"什么都没发生" —— 显式建出来，**不使用 null**（避免调用方漏判）✓</summary>
    public static readonly RevisitRollResult None = new(false, "", 0, -1);
}

/// <summary>
/// 🔴🔴 **`D-2` 重访刷新威胁**（2026-09-20 新增；`dd_replication_roadmap` D-2 / wiki ⑧）。
///
/// **DD 的行为**（wiki 原文实据）：反复走同一格会刷新威胁，
/// 概率随**光照档**变化 —— **光照 ≤50（暗及更暗）⇒ +2.5%；光照 = 0（全黑）⇒ +5%**。
/// ⇒ 即"**越黑越危险**"，且**回头这件事本身**会累积风险（与 `D-1` 的"回头有代价"同向）。
///
/// **本类纪律**：
/// · **零 Godot、纯函数**（`DungeonWalkLight` 同族）—— 不持有状态，调用方决定"何时掷"；
/// · **随机必写 `RngDraw`**（红线：所有抽取必写日志；`MapScouting` 是范式）✓；
/// · **数值全部来自 data**（`tuning.dungeon_layer.revisit`，`placeholder: true`）—— **不写死** ✓；
/// · **未配置 ⇒ 完全不掷**（连 `RngDraw` 都不写）⇒ 既有调用点行为**逐字不变** ✓。
/// </summary>
public static class RevisitSpawner
{
    /// <summary>威胁种类（**本次只登记"是哪种"，不实现具体效果** —— 与 `DungeonTileKind.Trap` 同类"暂留"）✓</summary>
    public const string ThreatBattle = "battle";
    public const string ThreatTrap = "trap";

    /// <summary>
    /// 掷一次"这一格重访是否出事"。
    /// </summary>
    /// <param name="log">事件流（**必写 `RngDraw`**）</param>
    /// <param name="rng">随机源</param>
    /// <param name="config">`tuning.dungeon_layer.revisit`（`null` ⇒ **不掷**、返回 `None`）</param>
    /// <param name="lightValue">**掷骰当时**的光照值（决定用哪一档：越暗概率越高）</param>
    public static RevisitRollResult Roll(
        CombatLog log, IRngProvider rng, RevisitThreatConfig? config, int lightValue)
    {
        if (log is null || rng is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (config is null || config.Tiers.Count == 0)
        {
            return RevisitRollResult.None; // 🔴 未配置 ⇒ **一点副作用都没有**（不掷、不写日志）✓
        }

        // 档位选择：取**满足条件的最暗一档**
        // 🔴 数据按「暗 → 亮」(`max_light` 严格递增) 排列，由 `TuningConfig.Validate` 加载期强制；
        //    本类**不排序** ⇒ 首个 `lightValue <= max_light` 命中者即最暗档 ✓
        RevisitThreatTier? tier = null;
        int tierIndex = -1;
        for (int i = 0; i < config.Tiers.Count; i++)
        {
            RevisitThreatTier t = config.Tiers[i];
            if (lightValue <= t.MaxLight)
            {
                tier = t;
                tierIndex = i;
                break; // 数据按"暗 → 亮"排序（校验强制），首个命中即最暗档 ✓
            }
        }

        if (tier is null)
        {
            return RevisitRollResult.None; // 比所有档都亮 ⇒ 无事（不掷）✓
        }

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 确定性红线：抽取必写日志 ✓
        bool triggered = roll < tier.Percent;

        // 威胁种类：也要掷（同样是抽取 ⇒ 同样写日志）—— 只有真的触发了才掷（否则是"空掷"）✓
        string threat = "";
        if (triggered)
        {
            if (config.BattleWeight > 0 && config.TrapWeight > 0)
            {
                int pick = rng.NextInt(0, config.BattleWeight + config.TrapWeight);
                log.Append(new RngDraw(rng.DrawCount, pick)); // 🔴 int 隐式转 double — `RngDraw` 第二个参数是 double ✓
                threat = pick < config.BattleWeight ? ThreatBattle : ThreatTrap;
            }
            else
            {
                threat = config.BattleWeight > 0 ? ThreatBattle : ThreatTrap;
            }
        }

        return new RevisitRollResult(triggered, threat, tier.Percent, tierIndex);
    }
}
