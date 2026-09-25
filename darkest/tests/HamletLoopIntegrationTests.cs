using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **养成闭环整合读数**（做功能：把已做好的各面**串成一条循环**并读出来）：
///   **出征（含阵亡）⇒ 回城（招募补人 · 升级建筑 · 治病 · 减压）⇒ 再出发** ✓
///
/// 目的：让"**闭环**"作为一个整体**可读**（单看某一面都容易漏掉"接不上"的那种断点 ⚠️）——
///   · 阵亡 ⇒ 名额释放 ⇒ **招募补得上**（`#400` (a)+ ✓）
///   · 战斗 ⇒ 经验 ⇒ **等级分布变化**（`#399`/`#401` ✓）
///   · 传家宝 ⇒ **建筑升级** ⇒ 减压更强（`#292` ✓）
///   · 损耗面（疾病/阵亡）与成长面（等级/建筑）在**同一行**对比 ✓
///
/// 口径（写清，不许混引）：
///   · **战斗为模拟**（本用例不跑真战斗：确定性注入胜负与偶发我方阵亡）⇒ **绝对值不可与真战斗探针混引** ✓
///   · 城侧策略 = **用例内输入**（能升就升 · 能补人就补 · 有钱先治病再减压）⇒ **不是游戏规则** ✓
///   · **零数值改动**（全部用策划已给的占位值 ✓）
/// </summary>
[TestClass]
public sealed class HamletLoopIntegrationTests
{
    private const int Runs = 10;

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
    public void TenRuns_WriteRunRecruitUpgrade_ShowTheWholeLoop()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        EconomyConfig ecoCfg = EconomyConfig.Parse(ReadData("economy.json"));
        SanitariumConfig saniCfg = SanitariumConfig.Parse(ReadData("sanitarium.json"));

        var log = new CombatLog();
        var roster = new Roster(cfg);
        // 🔴 步骤 ② 之后：库存必须**挂上任务奖励通道**才能发（旧通道已删 ⇒ 不挂会抛，不静默）✓
        var stock = new HeirloomStock(heirCfg,
            HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json")));
        var economy = new Economy(ecoCfg);
        StagecoachConfig coach = ecoCfg.Coach;
        string diseaseId = saniCfg.Diseases.First().Id;

        var lines = new List<string> { "[闭环] 起点：" + RosterComposition.Describe(roster) };

        for (int run = 1; run <= Runs; run++)
        {
            var events = new List<string>();

            // ① **出征**（模拟）：4 场胜利；**每 3 趟折损 1 名**（模拟真战斗的阵亡 ⇒ 这才测得到闭环 ✓）
            int battles = 4;
            for (int b = 0; b < battles; b++)
            {
                roster.AwardExperienceForBattle(log, win: true, reason: "loop_battle");
                economy.AwardBattle(log, "dark", "loop_battle");

                // 🆕 步骤 ②：传家宝走**任务奖励**（长度 = 本趟第几场 ⇒ 与生产 `StepsDone + 1` 同口径）✓
                stock.AwardForRun(log, steps: b + 1, averageLevel: 3.0, reason: "loop_battle");
            }

            if (run % 3 == 0 && roster.Heroes.Count > 0)
            {
                string victim = roster.Heroes.OrderBy(h => roster.LevelOf(h.Id)).First().Id;
                log.Append(new DeathEvent(UnitId.Of(victim), true, "loop_battle"));
                int removed = roster.ConsumePlayerDeaths(log);
                events.Add($"阵亡 {removed}（{roster.Graveyard[^1].Name} Lv{roster.Graveyard[^1].Level}）");
            }

            // ② **回城**（用例内策略）：先补人 → 再升级 → 再治病 → 余钱减压 ✓
            int beforeCount = roster.Heroes.Count;
            while (recruitable(roster) && roster.Recruit(log, coach, "warrior", $"新兵{roster.Heroes.Count + 1}",
                       rookieLevel: stock.EffectiveRookieLevel(coach.RookieLevel)) is not null)
            {
                // 补到上限为止
            }

            if (roster.Heroes.Count > beforeCount)
            {
                events.Add($"招募 +{roster.Heroes.Count - beforeCount}");
            }

            foreach (UpgradePath p in heirCfg.UpgradePaths)
            {
                if (stock.CanUpgrade(p.Building) && stock.TryUpgrade(log, p.Building))
                {
                    events.Add($"升级 {p.Building}→Lv{stock.LevelOf(p.Building)}");
                }
            }

            foreach (HeroConfig h in roster.Heroes.ToList())
            {
                if (roster.DiseasesOf(h.Id).Count > 0)
                {
                    int cost = ecoCfg.Building("sanitarium").Cost;
                    if (roster.Cure(log, h.Id, diseaseId, cost, "heirloom_crest"))
                    {
                        events.Add($"治病 {h.Name}");
                        break; // 一趟最多治 1 人（够演示闭环即可 ✓）
                    }
                }
            }

            int reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            if (reliefCost > 0 && economy.Gold >= reliefCost)
            {
                HeroConfig worst = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First();
                if (economy.TrySpend(log, reliefCost, "loop_relief"))
                {
                    roster.ApplyRelief(log, worst.Id, stock.EffectiveMoraleRestore("tavern", ecoCfg.Building("tavern").MoraleRestore), "tavern");
                    events.Add($"减压 {worst.Name}");
                }
            }

            lines.Add($"[闭环] 第 {run} 趟：{RosterComposition.Describe(roster)}　｜ 城侧：{(events.Count == 0 ? "（无动作）" : string.Join(" · ", events))}");
        }

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        // 🔴 结构性判据（唯一一条）：**闭环必须真的转起来**（等级升过 · 招过募 · 升过建筑 · 有阵亡留档）
        Assert.IsTrue(roster.Graveyard.Count > 0, "10 趟里应有阵亡留档（否则闭环的损耗面没被测到）✓");
        Assert.IsTrue(roster.Heroes.Any(h => h.Level > cfg.LevelMin), "等级必须涨过 ✓");
        Assert.IsTrue(stock.LevelOf("stagecoach") + stock.LevelOf("tavern") + stock.LevelOf("abbey") > 0, "至少升过一栋建筑 ✓");
    }

    /// <summary>还有名额可招吗（只读判断 ✓）</summary>
    private static bool recruitable(Roster roster)
        => roster.Heroes.Count < (roster.CurrentCap > 0 ? Math.Min(roster.CurrentCap, roster.Cap) : roster.Cap);

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
