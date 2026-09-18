using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **§9.2 裁定 (d)**（策划 `DELIVERY-DESIGNER-BATTLE-METRIC-20260921`）：战斗层要比的是
/// **「整趟走完 / 没走完」**（= **A4 感受到的那个量**），**不是**「崩了几次」（机制指标）✓
///
/// 🔴 **而报告 (d) 时必须【四个数一起给】**（他写的硬要求）：
///   **完成率 / 撤退率 / 场数 / 撤退场次** —— 📌 **"率"必须带【分母】**
///   （否则会重演"高士气崩溃更多"那种误导：**士气高 ⇒ 更敢打 ⇒ 场数多 ⇒ 绝对量更大**）✓
///
/// 口径（**必须写明，不许混引**）：
///   · 本对照 = **线性 6 场、不含扎营/事件节点**（为把变量收敛到"开局士气"）⇒ **绝对值不可与外层探针混引** ✓
///   · 开局士气 = **用例输入**（不是游戏数值）；**同 seed** ⇒ 唯一变量是士气 ✓
///   · **只报数不判红**；唯一结构性判据 = 注入必须真的生效 ✓
/// </summary>
[TestClass]
public sealed class P2BattleLayerExpeditionReadingsTests
{
    private const int Seeds = 12;
    private const int LowMorale = 40;
    private const int HighMorale = 75;

    private sealed record Row(bool Completed, int Battles, int Retreats, int Deaths);

    private static Row RunExpedition(long seed, int morale)
    {
        var rng = new RngProvider(seed);
        var session = new ExpeditionSession(
            _ =>
            {
                BattleDirector d = HeadlessDriver.NewDirector(new CombatLog());
                foreach (var u in d.Player.UnitsInSlotOrder())
                {
                    u.ApplyOpeningMorale(morale); // 与 HeroProjection.ApplyOpeningMorale 同款：只改运行时投影 ✓
                }

                return d;
            },
            6, firewood: 2, food: 12, ambushChance: 0.33);

        int battles = 0;
        int retreats = 0;
        int deaths = 0;
        bool completed = false;

        for (int i = 1; i <= 6; i++)
        {
            var blog = new CombatLog();
            BattleDirector d = session.BeginBattle(i, blog);
            string result = "RoundLimit";
            int round;
            for (round = 1; round <= 100; round++)
            {
                // 与 `HeadlessDriver.Run` 同款：每回合一次 6% 撤退尝试（让"撤退"自然发生）✓
                if (rng.NextPercent() < 6.0 && d.PlayerRetreat(rng))
                {
                    result = "DrawRetreat";
                    break;
                }

                d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
                if (d.IsBattleOver)
                {
                    result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                    break;
                }
            }

            deaths += blog.Events.OfType<DeathEvent>().Count(e => e.IsPlayer);
            battles++;

            // 撤退场次：以**场级事件**为准（`#352`：撤退不是一趟结局，所以只能数"场"）✓
            if (blog.Events.OfType<RetreatResolved>().Any())
            {
                retreats++;
            }

            session.EndBattle(d, i, result, Math.Min(round, 100));
            if (result != "PlayerVictory")
            {
                break;
            }

            completed = i == 6;
        }

        return new Row(completed, battles, retreats, deaths);
    }

    [TestMethod]
    public void ExpeditionLevel_SameSeeds_MoraleLowVsHigh_FourNumbersWithDenominators()
    {
        int lowDone = 0, highDone = 0;
        int lowRetreats = 0, highRetreats = 0;
        int lowBattles = 0, highBattles = 0;
        int lowDeaths = 0, highDeaths = 0;

        for (int i = 0; i < Seeds; i++)
        {
            long seed = 20260921 + i;
            Row lo = RunExpedition(seed, LowMorale);
            Row hi = RunExpedition(seed, HighMorale);
            lowDone += lo.Completed ? 1 : 0;
            highDone += hi.Completed ? 1 : 0;
            lowRetreats += lo.Retreats;
            highRetreats += hi.Retreats;
            lowBattles += lo.Battles;
            highBattles += hi.Battles;
            lowDeaths += lo.Deaths;
            highDeaths += hi.Deaths;
        }

        string Pct(int n, int d) => d == 0 ? "N/A（无样本：0 场）" : $"{(double)n / d:P1}（{n}/{d}）";
        var lines = new List<string>
        {
            $"[P2·整趟] 同 seed × 开局士气 **{LowMorale} vs {HighMorale}**（{Seeds} 个 seed · 线性 6 场、不含扎营/事件 · 只报数不判红）：",
            $"  · **完成率**：{LowMorale} ⇒ {Pct(lowDone, Seeds)}　｜　{HighMorale} ⇒ {Pct(highDone, Seeds)}",
            $"  · **撤退率（按场）**：{LowMorale} ⇒ {Pct(lowRetreats, lowBattles)}　｜　{HighMorale} ⇒ {Pct(highRetreats, highBattles)}",
            $"  · **场数（分母）**：{LowMorale} ⇒ {lowBattles}　｜　{HighMorale} ⇒ {highBattles}",
            $"  · **撤退场次**：{LowMorale} ⇒ {lowRetreats}　｜　{HighMorale} ⇒ {highRetreats}",
            $"  · 附：阵亡（我方）：{LowMorale} ⇒ {lowDeaths}　｜　{HighMorale} ⇒ {highDeaths}",
            "  ⇒ 📌 **四数齐（率带分母）** —— 若要判「士气更高是否更不容易崩」，**看完成率与撤退率**，不要看绝对次数 ✓",
        };

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        Assert.IsTrue(lowBattles > 0 && highBattles > 0, "两档都必须真的打过（否则本对照无效）✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
