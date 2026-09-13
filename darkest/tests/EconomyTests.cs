using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.0 ②（`#283` 7.1 + 硬要求① + **P22 ④**）：**金钱来源必须与【光照档 + 战斗数】挂钩**；
/// 所有变更**必写事件**（数字必须来自事件流）；花钱不足**拒绝且不扣**。
/// </summary>
[TestClass]
public sealed class EconomyTests
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
    public void P22_4_Income_TiedToBothBattlesAndLightTier()
    {
        EconomyConfig c = Cfg();

        Assert.IsTrue(c.BattleReward >= 1, "与【战斗数】挂钩：每场至少 1 单位（P22 ④）");
        Assert.IsTrue(c.LightTierBonus.Values.Sum() >= 1, "与【光照档】挂钩：越暗有实际加成（P22 ④）");

        // 越暗越多（单调不减 + 两端有差）
        int radiant = c.RewardFor("radiant");
        int black = c.RewardFor("black");
        Assert.IsTrue(black > radiant, $"Black({black}) 必须多于 Radiant({radiant})（冒险要有跨趟回报）");

        // 比例（7.1）：一场战斗 1 单位 / 一趟 3~6 / 一次减压 3
        Assert.AreEqual(1, c.BattleReward, "一场战斗 = 1 单位");
        Assert.AreEqual(3, c.StressReliefCost, "一次减压 = 3 单位");
        Assert.IsTrue(c.RatioCheck.ExpectedRunIncomeLow is >= 3 and <= 6
                      && c.RatioCheck.ExpectedRunIncomeHigh is >= 3 and <= 6,
            "一趟收入 3~6 ⇒ 一趟能减 1~2 次压");
    }

    [TestMethod]
    public void P22_4_BadEconomy_ThrowsOnLoad()
    {
        string raw = ReadData("economy.json");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("\"battle_reward\": 1", "\"battle_reward\": 0", StringComparison.Ordinal)),
            "battle_reward 0 ⇒ 与战斗数脱钩 ⇒ 报错（P22 ④）");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("\"black\": 3", "\"black\": 0", StringComparison.Ordinal)),
            "Black 加成归零 ⇒ 与光照档脱钩 ⇒ 报错（P22 ④）");

        Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(raw.Replace("\"stress_relief_cost\": 3", "\"stress_relief_cost\": 0", StringComparison.Ordinal)),
            "减压价格为 0 ⇒ 报错（P22 ④ / 7.1）");
    }

    [TestMethod]
    public void Gold_ComesFromEventStream_AndSpendRefusedWhenPoor()
    {
        var log = new CombatLog();
        var econ = new Economy(Cfg());

        int radiant = econ.AwardBattle(log, "radiant");
        int black = econ.AwardBattle(log, "black");

        Assert.IsTrue(black > radiant, "同场数下，越暗拿得越多");
        Assert.AreEqual(radiant + black, econ.Gold, "金钱 = 事件流累计");
        Assert.AreEqual(2, log.Events.OfType<GoldChangedEvent>().Count(), "每次变更**必写事件**（数字必须来自事件流）");
        Assert.AreEqual(econ.Gold, log.Events.OfType<GoldChangedEvent>().Last().Total, "事件 Total 与状态对账");

        Assert.IsFalse(econ.TrySpend(log, econ.Gold + 1, "stress_relief"), "钱不够 ⇒ 拒绝");
        Assert.AreEqual(econ.Gold, log.Events.OfType<GoldChangedEvent>().Last().Total, "拒绝时不得改状态、不得写事件");
        Assert.IsTrue(econ.TrySpend(log, 3, "stress_relief"), "够钱 ⇒ 成交（一次减压 = 3）");
    }
}
