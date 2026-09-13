using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.0（`#287` = **`#245` 的落地**）：**士气是跨趟状态**（存在名册里）。
/// 锁：① 数据层 P22 ⑦（morale ∈[0,100]；**新兵入场 = 50**）；② **跨趟存活 / 不重置**；
/// ③ **V2 可观测**：减压一次 ⇒ **下一趟开局士气确实更高**；④ 变更必写事件。
/// </summary>
[TestClass]
public sealed class RosterMoraleTests
{
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

    private static RosterConfig Cfg() => RosterConfig.Parse(ReadData("roster.json"));

    [TestMethod]
    public void P22_7_MoraleLivesInRoster_RookieIsFifty()
    {
        RosterConfig r = Cfg();
        Assert.IsTrue(r.Heroes.All(h => h.Morale is >= 0 and <= 100), "每人士气 ∈ [0,100]（P22 ⑦）");
        Assert.IsTrue(r.Heroes.Where(h => h.Level == 1).All(h => h.Morale == RosterConfig.RookieMorale),
            "新兵（1 级）入场士气 = 50（否则新兵与老兵两套语义）");
        Assert.IsTrue(r.Heroes.Any(h => h.Morale < RosterConfig.RookieMorale), "样本里应有「带着旧伤」的老兵（士气低于 50）");
    }

    [TestMethod]
    public void P22_7_BadMoraleData_ThrowsOnLoad()
    {
        string raw = ReadData("roster.json");

        Assert.ThrowsException<InvalidDataException>(
            () => RosterConfig.Parse(raw.Replace("\"morale\": 40", "\"morale\": 140", StringComparison.Ordinal)),
            "士气越界 ⇒ 报错（P22 ⑦）");

        Assert.ThrowsException<InvalidDataException>(
            () => RosterConfig.Parse(raw.Replace("\"level\": 1,", "\"level\": 1, \"morale\": 10,", StringComparison.Ordinal)),
            "新兵士气不是 50 ⇒ 报错（P22 ⑦）");
    }

    [TestMethod]
    public void Morale_SurvivesAcrossRuns_AndIsNotResetOnReturn()
    {
        var log = new CombatLog();
        var roster = new Roster(Cfg());
        string hero = roster.Heroes.First(h => h.Morale < RosterConfig.RookieMorale).Id; // 一个"带旧伤"的老兵

        int before = roster.MoraleOf(hero);
        roster.ApplyReturnFromRun(log, new[] { (hero, before - 20) }); // 本趟结束士气更低
        Assert.AreEqual(before - 20, roster.MoraleOf(hero), "归来**写回**本趟结果（不重置、不解算）");

        // 🔴 #245：回城**不恢复** —— 下一趟开局士气 = 上一趟结束时的值
        var opening = roster.OpeningMorale(new[] { hero });
        Assert.AreEqual(before - 20, opening[hero], "#245：回城不恢复 ⇒ 下一趟开局沿用上一趟结束值");
        Assert.IsTrue(log.Events.OfType<HeroMoraleChangedEvent>().Any(e => e.Reason == "run_return"),
            "士气变更必写事件（数字必须来自事件流）");
    }

    [TestMethod]
    public void V2_ReliefOnce_NextRunOpeningMoraleIsHigher()
    {
        var log = new CombatLog();
        var roster = new Roster(Cfg());
        string hero = roster.Heroes.OrderBy(h => h.Morale).First().Id;

        int openingBefore = roster.OpeningMorale(new[] { hero })[hero];
        int after = roster.ApplyRelief(log, hero, restore: 30, building: "tavern");
        int openingAfter = roster.OpeningMorale(new[] { hero })[hero];

        Assert.AreEqual(Math.Min(100, openingBefore + 30), after, "减压恢复 30（与 ④ 的数据一致）");
        Assert.IsTrue(openingAfter > openingBefore,
            $"🔴 V2 可观测判据：回城减压一次 ⇒ **下一趟开局士气确实更高**（{openingBefore} 到 {openingAfter}）");
        Assert.IsTrue(log.Events.OfType<HeroMoraleChangedEvent>().Any(e => e.Reason.StartsWith("relief", StringComparison.Ordinal)),
            "减压变更必写事件（且带建筑名，便于审计「同价同效风险不同」）");
    }

    [TestMethod]
    public void Morale_Clamped_AndUnknownHeroThrows()
    {
        var log = new CombatLog();
        var roster = new Roster(Cfg());
        string hero = roster.Heroes[0].Id;

        roster.SetMorale(log, hero, 999, "test");
        Assert.AreEqual(100, roster.MoraleOf(hero), "上钳 100");
        roster.SetMorale(log, hero, -50, "test");
        Assert.AreEqual(0, roster.MoraleOf(hero), "下钳 0");
        Assert.ThrowsException<InvalidOperationException>(() => roster.MoraleOf("not_a_hero"),
            "名册里没有的人 ⇒ 报错（不给默认值）");
    }
}
