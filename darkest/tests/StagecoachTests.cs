using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.0 ⑤（`#283` 硬要求③ + **P22 ⑥**）：**招募免费**、**新兵 level = 1 / morale = 50**（**补的人不比老的强**）、
/// **满员即拒绝**（不悄悄顶替）；招募必写事件。
/// </summary>
[TestClass]
public sealed class StagecoachTests
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

    private static EconomyConfig Cfg() => EconomyConfig.Parse(ReadData("economy.json"));

// 🔴 `P22_6` 的**配置层读数**（`recruit_cost=0` · `rookie_level=1` · `rookie_morale=50` · `max_roster=28`）
    //    已由三处覆盖 ⇒ **2026-10-01 测试减量删去该用例**（判据不丢、用例数 −1 ✓）：
    //      ① `recruit_cost != 0` / `rookie_level != 1` ⇒ `EconomyConfig` **加载期 fail-fast**
    //         （见本文件 `P22_6_BadStagecoachData_ThrowsOnLoad`）✓
    //      ② 行为层：`Recruit_AddsRookie_LevelOne_MoraleFifty_Free_AndWritesEvent`（level / morale / 免费 + 事件 Cost=0）✓
    //      ③ `max_roster = 28` = 曲线末值 ⇒ `StagecoachCurvesTests` 断言 ✓

    [TestMethod]
    public void P22_6_BadStagecoachData_ThrowsOnLoad()
    {
        string raw = ReadData("economy.json");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("\"recruit_cost\": 0", "\"recruit_cost\": 5", StringComparison.Ordinal)),
            "招募收费 ⇒ 报错（P22 ⑥）");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("\"rookie_level\": 1", "\"rookie_level\": 3", StringComparison.Ordinal)),
            "新兵不是 1 级 ⇒ 报错（补的人比老的强了；P22 ⑥）");
    }

    [TestMethod]
    public void Recruit_AddsRookie_LevelOne_MoraleFifty_Free_AndWritesEvent()
    {
        var log = new CombatLog();
        var roster = new Roster(RosterConfig.Parse(ReadData("roster.json")));
        EconomyConfig cfg = Cfg();
        var econ = new Economy(cfg, gold: 10);

        int before = roster.Heroes.Count;
        HeroConfig? rookie = roster.Recruit(log, cfg.Coach, "warrior", "新兵甲");

        Assert.IsNotNull(rookie, "名册未满 ⇒ 招募成功");
        Assert.AreEqual(before + 1, roster.Heroes.Count);
        Assert.AreEqual(1, rookie!.Level, "新兵 level = 1（不比老的强）");
        Assert.AreEqual(RosterConfig.RookieMorale, rookie.Morale, "新兵士气 = 50");
        Assert.AreEqual(RosterConfig.RookieMorale, roster.MoraleOf(rookie.Id), "名册里也记 50（跨趟持有）");
        Assert.AreEqual(10, econ.Gold, "**招募不花钱**（免费）");
        Assert.AreEqual(2, rookie.Traits.Count, "新兵 2~3 条特质（P22 ③）");
        Assert.IsTrue(log.Events.OfType<HeroRecruitedEvent>().Any(e => e.HeroId == rookie.Id && e.Cost == 0),
            "招募必写事件且 Cost = 0（数字来自事件流）");

        // 🔴 补的人不比老的强：名册里存在等级更高者
        Assert.IsTrue(roster.Heroes.Any(h => h.Level > rookie.Level), "名册里的老兵等级高于新兵（老兵仍有价值）");
    }

    [TestMethod]
    public void Recruit_RefusedWhenRosterFull()
    {
        var log = new CombatLog();
        var roster = new Roster(RosterConfig.Parse(ReadData("roster.json")));
        EconomyConfig cfg = Cfg();

        while (roster.Heroes.Count < cfg.Coach.MaxRoster)
        {
            Assert.IsNotNull(roster.Recruit(log, cfg.Coach, "medic", "补员"), "未满时都能招");
        }

        Assert.AreEqual(cfg.Coach.MaxRoster, roster.Heroes.Count, "已达上限（= cfg.Coach.MaxRoster，M7② 后为 28）");
        Assert.IsNull(roster.Recruit(log, cfg.Coach, "medic", "超员"), "**满员即拒绝**（返回 null，不悄悄顶替）");
        Assert.AreEqual(cfg.Coach.MaxRoster, roster.Heroes.Count, "拒绝时人数不变");
    }
}
