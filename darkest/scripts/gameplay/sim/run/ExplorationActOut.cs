using System;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>🔴 `D-7`：一次"带折磨者是否拒绝"的掷骰结果（**空 = 不拒绝**，不是 null）✓</summary>
public sealed record ActOutRollResult(bool Refused, double PercentUsed, string Text)
{
    /// <summary>"没拒绝" —— 显式建出来，**不使用 null**（避免调用方漏判）✓</summary>
    public static readonly ActOutRollResult None = new(false, 0, string.Empty);
}

/// <summary>
/// 🔴🔴 **`D-7` 探索层 act-out**（2026-09-20 新增；`dd_replication_roadmap` D-7 /
/// `dungeon_layer_design.md §F5` / DD ⑩）—— 带**折磨**的英雄在**战斗之外**的行为表现。
///
/// <para>**DD 的行为**：折磨的影响**大多发生在地牢探索**（⑩）—— 拒绝摸奇物 / 拒绝进食。
/// 而本项目的折磨此前**只**在战斗内有效（`AfflictionProcs`）⇒ 走廊上完全感受不到 ⇒ 本类补上 ✓</para>
///
/// <para><b>本类分工</b>（与 `HungerSpawner` / `RevisitSpawner` 完全同构）：</para>
/// <para>· **本类只管"判 + 掷"**（纯函数、零 Godot）；</para>
/// <para>· **状态的持有在调用方**（`ExpeditionFlow` 持 Curio 门禁、`ExpeditionSession` 持进食门禁）；</para>
/// <para>· **文案**在本类给（两条拒绝各一句），由调用方写进 `CombatLog` ✓</para>
///
/// <para>🔴 **折磨判据 = 士气 &lt; 阈值**（默认 = `morale.start` = 50）—— 该阈值**不是近似，
/// 而是模型自身的边界**：折磨只在士气 == 0 挂上、**只有回到 50 才解除**（`MoraleLedger` 第 127~134 行），
/// 美德走 100 且立即回 50 ⇒ **「士气 &lt; 50」⇔「带折磨」** ✓（详见 `TuningExplorationActOut` 注释）</para>
///
/// <para>**纪律**：① **随机必写 `RngDraw`**；② **不带折磨 ⇒ 不掷不写**（零随机流污染）；
/// ③ **未配置 ⇒ 一点副作用都没有**；④ **拒绝必给非空文案**（红线 21：绝不静默失败）✓</para>
/// </summary>
public static class ExplorationActOut
{
    /// <summary>🔴 拒绝用道具时的文案（`curio.md §1.2 ⑤`：DD 原文 = 被折磨驱使，**伸手就碰**）✓</summary>
    public const string CurioRefuseText = "他被自己的恐惧攫住，顾不上用什么工具，伸手就去碰。";

    /// <summary>🔴 拒绝进食时的文案（`§F5 ③`）✓</summary>
    public const string EatRefuseText = "他死死盯着那份口粮，怎么也不肯吃。";

    /// <summary>
    /// 🔴 **是否带折磨**（== 士气 &lt; `MoraleAfflictionThreshold`）。
    /// <para>🔴 **纯查询**：**不掷骰、不写日志**（掷骰是"是否拒绝"那一步的事）✓</para>
    /// <para>· `config is null` ⇒ **一律 `false`**（未配置 = 该机制关闭）✓</para>
    /// <para>· `morale == threshold` ⇒ **`false`** —— 那正是 `morale.start` / 折磨的解除点 ✓</para>
    /// </summary>
    public static bool IsAfflicted(TuningExplorationActOut? config, int morale, IRngProvider rng, CombatLog log)
    {
        if (rng is null || log is null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        // 🔴 未配置 ⇒ 关闭（**不掷、不写**）—— 参数里的 rng/log 只为签名一致（本方法确实一个都不用）✓
        _ = rng;
        _ = log;
        return config is not null && morale < config.MoraleAfflictionThreshold;
    }

    /// <summary>
    /// 🔴 **掷一次"带折磨者是否拒绝用道具"**（`curio.md §1.2 ⑤`）。
    /// <para>· 未配置 / 不带折磨 ⇒ `None`，**不掷不写**（零副作用）✓</para>
    /// <para>· 否则 ⇒ 掷 `[0,100)`，`roll &lt; CurioRefusePercent` ⇒ 拒绝；**必写 `RngDraw`** ✓</para>
    /// </summary>
    public static ActOutRollResult RollCurioRefuse(
        CombatLog log, IRngProvider rng, TuningExplorationActOut? config, int morale)
    {
        if (log is null || rng is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (!IsAfflicted(config, morale, rng, log))
        {
            return ActOutRollResult.None; // 🔴 不掷、不写：**零随机流污染**（D-4 踩过同源坑）✓
        }

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 确定性红线：抽取必写日志 ✓
        bool refused = roll < config!.CurioRefusePercent;
        return new ActOutRollResult(refused, config.CurioRefusePercent, refused ? CurioRefuseText : string.Empty);
    }

    /// <summary>
    /// 🔴 **掷一次"带折磨者是否拒绝进食"**（`§F5 ③`，与 F3c 饥饿联动）。
    /// <para>· 未配置 / 不带折磨 ⇒ `None`，**不掷不写** ✓</para>
    /// <para>· 否则 ⇒ 掷 `[0,100)`，`roll &lt; EatRefusePercent` ⇒ 拒绝；**必写 `RngDraw`** ✓</para>
    /// </summary>
    public static ActOutRollResult RollEatRefuse(
        CombatLog log, IRngProvider rng, TuningExplorationActOut? config, int morale)
    {
        if (log is null || rng is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (!IsAfflicted(config, morale, rng, log))
        {
            return ActOutRollResult.None; // 🔴 不掷、不写 ✓
        }

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 确定性红线 ✓
        bool refused = roll < config!.EatRefusePercent;
        return new ActOutRollResult(refused, config.EatRefusePercent, refused ? EatRefuseText : string.Empty);
    }
}
