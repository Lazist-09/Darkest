using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.0 ④（`#283` 7.3 + 硬要求② + **P22 ⑤**）：Tavern 与 Abbey **同价同效、风险不同** ⇒ 选风格而非选更优；
/// 副作用 = 下一趟开局士气 −N；随机**必写 `RngDraw`**；钱不够 ⇒ 拒绝且不扣、不掷骰。
/// </summary>
[TestClass]
public sealed class StressReliefTests
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

    [TestMethod]
    public void P22_5_SamePriceSameEffect_DifferentRisk()
    {
        EconomyConfig c = Cfg();
        StressReliefBuildingConfig tavern = c.Building("tavern");
        StressReliefBuildingConfig abbey = c.Building("abbey");

        Assert.AreEqual(tavern.Cost, abbey.Cost, "同价（P22 ⑤）");
        Assert.AreEqual(tavern.MoraleRestore, abbey.MoraleRestore, "同效：恢复量相同");
        Assert.AreEqual(tavern.NextRunPenalty, abbey.NextRunPenalty, "同效：下趟惩罚相同");
        Assert.AreNotEqual(tavern.PenaltyChance, abbey.PenaltyChance, "**风险不同**（否则两栋等价 ⇒ 选风格不成立）");
        Assert.AreEqual(c.StressReliefCost, tavern.Cost, "价格与 7.1 的比例一致（一次减压 = 3）");
    }

    [TestMethod]
    public void P22_5_BadStressReliefData_ThrowsOnLoad()
    {
        string raw = ReadData("economy.json");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("\"penalty_chance\": 0.2", "\"penalty_chance\": 0.5", StringComparison.Ordinal)),
            "两栋风险相同 ⇒ 报错（P22 ⑤）");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("{ \"id\": \"abbey\", \"name\": \"修道院\", \"cost\": 3", "{ \"id\": \"abbey\", \"name\": \"修道院\", \"cost\": 4", StringComparison.Ordinal)),
            "不同价 ⇒ 报错（P22 ⑤）");
    }

    [TestMethod]
    public void Relief_RestoresMorale_WritesRngAndEvent_PenaltyByRisk()
    {
        EconomyConfig cfg = Cfg();
        var log = new CombatLog();
        var econ = new Economy(cfg, gold: 100);
        var rng = new RngProvider(20260909);
        var tavern = cfg.Building("tavern");

        StressReliefOutcome o = StressRelief.Apply(cfg, econ, rng, log, "tavern", "hero_tank_1", 40);

        Assert.IsTrue(o.Paid, "付得起 ⇒ 成交");
        Assert.AreEqual(70, o.NewMorale, "恢复 30（同效）");
        Assert.AreEqual(97, econ.Gold, "花掉一次减压的钱（3）");
        Assert.IsTrue(log.Events.OfType<RngDraw>().Any(), "副作用判定是随机 ⇒ **必写 RngDraw**（红线）");
        StressReliefEvent e = log.Events.OfType<StressReliefEvent>().Single();
        Assert.AreEqual("tavern", e.Building);
        Assert.AreEqual(o.PenaltyTriggered, e.PenaltyTriggered, "事件与返回值对账");
        Assert.IsTrue(e.PenaltyRoll is >= 0 and < 100, "掷骰留痕（0~100，供审计）");
        Assert.AreEqual(e.PenaltyRoll < (tavern.PenaltyChance * 100.0), e.PenaltyTriggered,
            "触发判定 = roll 小于 penalty_chance 乘 100（与契约的掷骰约定一致）");
        Assert.AreEqual(StressRelief.NextRunOpeningMorale(70, o.NextRunPenalty), 70 - o.NextRunPenalty, "下趟开局士气 −N（7.3）");
    }

    [TestMethod]
    public void Relief_RefusedWhenPoor_NoRollNoEvent()
    {
        EconomyConfig cfg = Cfg();
        var log = new CombatLog();
        var econ = new Economy(cfg, gold: 1);
        var rng = new RngProvider(20260909);

        StressReliefOutcome o = StressRelief.Apply(cfg, econ, rng, log, "abbey", "hero_medic_1", 10);

        Assert.IsFalse(o.Paid, "钱不够 ⇒ 拒绝");
        Assert.AreEqual(1, econ.Gold, "拒绝时不得扣钱");
        Assert.IsFalse(log.Events.OfType<RngDraw>().Any(), "拒绝时**不得掷骰**（不给白掷）");
        Assert.IsFalse(log.Events.OfType<StressReliefEvent>().Any(), "拒绝时不写结算事件");
    }

    [TestMethod]
    public void Relief_RiskSeparates_RollIsWritten_AndTavernRiskier()
    {
        EconomyConfig cfg = Cfg();
        var tavernLog = new CombatLog();
        var abbeyLog = new CombatLog();
        var tavernEcon = new Economy(cfg, gold: 100000);
        var abbeyEcon = new Economy(cfg, gold: 100000);
        var rng = new RngProvider(424242);

        const int n = 400;
        int tavernTriggers = 0, abbeyTriggers = 0;
        for (int i = 0; i < n; i++)
        {
            if (StressRelief.Apply(cfg, tavernEcon, rng, tavernLog, "tavern", "h", 50).PenaltyTriggered)
            {
                tavernTriggers++;
            }

            if (StressRelief.Apply(cfg, abbeyEcon, rng, abbeyLog, "abbey", "h", 50).PenaltyTriggered)
            {
                abbeyTriggers++;
            }
        }

        double tavernRate = tavernTriggers / (double)n;
        double abbeyRate = abbeyTriggers / (double)n;
        Console.WriteLine($"[M8.0] ④ 减压副作用触发率：Tavern {tavernRate:P0}（配置 {cfg.Building("tavern").PenaltyChance:P0}）／ Abbey {abbeyRate:P0}（配置 {cfg.Building("abbey").PenaltyChance:P0}）");

        Assert.IsTrue(tavernRate > abbeyRate, "**Tavern 更不稳**（风险不同 ⇒ 选风格成立）");
        Assert.AreEqual(2 * n, tavernLog.Events.OfType<RngDraw>().Count() + abbeyLog.Events.OfType<RngDraw>().Count(),
            "每次判定都写了一条 RngDraw（红线：随机必写）");
    }
}
