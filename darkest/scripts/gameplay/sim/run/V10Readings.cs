using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **V10 口径读数**（架构 `m7_6_verification §9` 规格 · 策划 `#358`③ 的新口径）——
/// 🔴 **一切从【事件流】算出**，**不造旁路计数器**（架构 `data_schema §3.11` 的硬要求）✓
///
/// 口径（写死，避免各写一份）：
///   · **一趟结局 = 三类**：**走完（completed）／放弃远征（abandoned）／全灭（wiped）** ✓
///     （来源：该趟 `TownReturnEvent.Outcome` —— **撤退不是结局**，见 `#352`）✓
///   · **完成率 = 走完 ÷ 三类之和** ✓（不是 ÷ 总趟数：未结束的趟单独报，不混进分母）✓
///   · **撤退 = 场级量**：`RetreatResolved` 的**条数** ⇒ 报「撤退场次/趟」与 **0 / 1 / ≥2** 的分布 ✓
///   · 🆕 **「撤而不弃」占比** = **至少退过 1 次、但最终【走完】**的趟 ÷ 走完的趟
///     ⇒ 它是"撤退能不能成为**有效策略**"的**可证伪形式**（策划 `#358`③）✓
/// </summary>
public sealed record V10Readings(
    int Runs,
    int Completed,
    int Abandoned,
    int Wiped,
    int Unfinished,
    int RetreatResolvedTotal,
    int RunsWithAnyRetreat,
    double CompletionRate,
    double RetreatWithoutAbandonRate,
    IReadOnlyList<int> RetreatCountsPerRun)
{
    /// <summary>🔴 **判据①**：三类结局计数 + 未结束 == 总趟数（**和必须闭合**，否则说明事件流缺读数 ✓）</summary>
    public bool CountsClose => Completed + Abandoned + Wiped + Unfinished == Runs;

    /// <summary>结局分布（供报告打印）✓</summary>
    public string Describe()
        => $"趟 {Runs}：走完 {Completed} ／ 放弃 {Abandoned} ／ 全灭 {Wiped} ／ 未结束 {Unfinished}" +
           $"　完成率 {(CompletionRate * 100):F1}%　撤退场次 {RetreatResolvedTotal}（退过的趟 {RunsWithAnyRetreat}，" +
           $"0/1/≥2 = {RetreatCountsPerRun.Count(c => c == 0)}/{RetreatCountsPerRun.Count(c => c == 1)}/{RetreatCountsPerRun.Count(c => c >= 2)}）" +
           $"　撤而不弃 {(RetreatWithoutAbandonRate * 100):F1}% ✓";

    /// <summary>从"每趟一份事件流"计算 ✓（`null`/空日志 ⇒ 该趟记为 `Unfinished`，**不静默当成走完**）✓</summary>
    public static V10Readings From(IEnumerable<IReadOnlyList<BattleEvent>> runEventStreams)
    {
        if (runEventStreams is null)
        {
            throw new ArgumentNullException(nameof(runEventStreams));
        }

        int runs = 0;
        int completed = 0;
        int abandoned = 0;
        int wiped = 0;
        int unfinished = 0;
        int retreatTotal = 0;
        int runsWithRetreat = 0;
        var perRun = new List<int>();

        foreach (IReadOnlyList<BattleEvent> stream in runEventStreams)
        {
            runs++;
            IReadOnlyList<BattleEvent> events = stream ?? Array.Empty<BattleEvent>();

            // 一趟结局 = **该趟最后一个** `TownReturnEvent`（多次回城时以最后一次为准 ⇒ 与"跨趟主循环"一致）✓
            TownReturnEvent? town = events.OfType<TownReturnEvent>().LastOrDefault();
            switch (town?.Outcome)
            {
                case "completed":
                    completed++;
                    break;
                case "abandoned":
                    abandoned++;
                    break;
                case "wiped":
                    wiped++;
                    break;
                default:
                    unfinished++; // ⚠️ 没有终点事件（或未登记的结局串）⇒ **如实记"未结束"**，绝不猜 ✓
                    break;
            }

            int retreats = events.OfType<RetreatResolved>().Count(); // 🔴 场级：**由事件算**（不查任何计数器）✓
            perRun.Add(retreats);
            retreatTotal += retreats;
            if (retreats > 0)
            {
                runsWithRetreat++;
            }
        }

        int decided = completed + abandoned + wiped;
        double completionRate = decided == 0 ? 0.0 : (double)completed / decided;
        // 「撤而不弃」= 退过 1 次以上、**且最终走完**的趟 ÷ 走完的趟 ✓
        int retreatAndCompleted = 0;
        {
            int i = 0;
            foreach (IReadOnlyList<BattleEvent> stream in runEventStreams)
            {
                IReadOnlyList<BattleEvent> events = stream ?? Array.Empty<BattleEvent>();
                bool isCompleted = events.OfType<TownReturnEvent>().LastOrDefault()?.Outcome == "completed";
                if (isCompleted && perRun[i] > 0)
                {
                    retreatAndCompleted++;
                }

                i++;
            }
        }

        double retreatWithoutAbandon = completed == 0 ? 0.0 : (double)retreatAndCompleted / completed;

        return new V10Readings(runs, completed, abandoned, wiped, unfinished,
            retreatTotal, runsWithRetreat, completionRate, retreatWithoutAbandon, perRun);
    }
}
