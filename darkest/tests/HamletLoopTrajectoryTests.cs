using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **策划 `#394` 的判据**：P0 那条"第 4 趟全同（平台期）"**是 (a) 探针不真实 还是 (b) 成长早期饱和**？
/// ⇒ **判据 = 跑 8~10 趟，看"起点变化"的轨迹**：
///    · 1~4 趟持续变、**5 趟起全同** ⇒ 倾向 **(b)**（数值域 ⇒ 登记"解冻后校准"）⚠️
///    · **始终在变** ⇒ (a) 不成立 ✅
///
/// 本用例 = **读数工具**（`O-82`：只报数不判红）· **零数值改动**（不碰任何 `data/*.json`）✓
/// 城镇侧策略（写死、可复现）：**能升级就升级，余钱减压士气最低者** ✓
/// </summary>
[TestClass]
public sealed class HamletLoopTrajectoryTests
{
    private const int Runs = 10; // 策划要的 8~10 ✓

    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    [TestMethod]
    public void TenRuns_ShowWhetherTheStartPointKeepsChanging()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        EconomyConfig ecoCfg = EconomyConfig.Parse(ReadData("economy.json"));
        ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));

        var log = new CombatLog();
        var roster = new Roster(rosterCfg);
        var stock = new HeirloomStock(heirCfg);
        var economy = new Economy(ecoCfg);

        var lines = new List<string>();
        RunStartSnapshot? prev = null;

        for (int run = 1; run <= Runs; run++)
        {
            // ① **出发前快照** ⇒ 与上一趟对比（这就是"起点变化"的轨迹）✓
            RunStartSnapshot snap = RunStartSnapshot.Capture(run, roster, stock, economy);
            foreach (string l in snap.DiffLines(prev))
            {
                lines.Add(l);
            }

            prev = snap;

            // ② **跑一趟**（本用例用"确定性推图"代替真实战斗：走到终点 = 完成；只为产生**光照档收益**）✓
            int heirloomGainPerTier = 0;
            foreach (string tier in new[] { "dim", "shadowy", "dark" })
            {
                heirloomGainPerTier += stock.AwardForTier(log, tier, "trajectory");
            }

            int goldGain = economy.AwardBattle(log, "dark", "trajectory");
            _ = heirloomGainPerTier;
            _ = goldGain;

            // ③ **城镇动作**（写死的策略，可复现）：① 能升级就升级 ② 余钱减压士气最低者 ✓
            foreach (UpgradePath path in heirCfg.UpgradePaths)
            {
                if (stock.CanUpgrade(path.Building))
                {
                    stock.TryUpgrade(log, path.Building);
                }
            }

            int reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            while (economy.Gold >= reliefCost)
            {
                HeroConfig? worst = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).FirstOrDefault();
                if (worst is null)
                {
                    break;
                }

                int restore = stock.EffectiveMoraleRestore("tavern", ecoCfg.Building("tavern").MoraleRestore);
                if (!economy.TrySpend(log, reliefCost, "trajectory_relief"))
                {
                    break;
                }

                roster.ApplyRelief(log, worst.Id, restore, "tavern");
                reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            }
        }

        // ④ **轨迹汇总**（策划要看的就是"哪一段开始平"）✓
        var equalRuns = lines.Where(l => l.Contains("十项全同")).Count();
        lines.Add($"[P0·轨迹] 共 {Runs} 趟：**全同的趟数 = {equalRuns}**（若从第 5 趟起连续全同 ⇒ 倾向 (b) 早期饱和 ⚠️）" +
                  $"　建筑与传家宝轨迹见上（`O-82`：只报数不判红）✓");

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        // 🔴 结构性判据（唯一一条）：**10 趟里至少要有一次"起点变了"** ⇒ 否则说明城镇动作根本没接线 ⚠️
        Assert.IsTrue(lines.Any(l => l.Contains("变了")), "10 趟里必须至少有一次起点变化（否则城镇动作没接线）✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
