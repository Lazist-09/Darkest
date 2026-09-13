using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.1（`m8_roadmap §1.4` 验收）：**传家宝可获取**（与光照档挂钩，与金钱同源）· **升级真的改变数字** ·
/// **两轴都在** · **升级有代价**（不足即拒绝）· 变更必写事件。
/// </summary>
[TestClass]
public sealed class HeirloomStockTests
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

    private static HeirloomConfig Cfg() => HeirloomConfig.Parse(ReadData("heirlooms.json"));

    private static EconomyConfig Econ() => EconomyConfig.Parse(ReadData("economy.json"));

    [TestMethod]
    public void M81_3_HeirloomsDropWithLightTier_SameTokenAsGold()
    {
        var log = new CombatLog();
        var stock = new HeirloomStock(Cfg());

        int dim = stock.AwardForTier(log, "dim");
        int dark = new HeirloomStock(Cfg()).AwardForTier(log, "dark");

        Assert.IsTrue(dim >= 1 && dark > dim, $"越暗越多（dim {dim} 到 dark {dark}）—— 与金钱同源");
        Assert.IsTrue(stock.Count("crests") >= 1, "dim 档至少给 crests");
        Assert.IsTrue(log.Events.OfType<HeirloomChangedEvent>().Any(e => e.Reason == "battle"),
            "掉落必写事件（数字来自事件流）");
    }

    [TestMethod]
    public void M81_3_UpgradeChangesNumbers_BothAxes()
    {
        var log = new CombatLog();
        HeirloomConfig cfg = Cfg();
        EconomyConfig econ = Econ();
        var stock = new HeirloomStock(cfg);

        int baseCost = econ.StressReliefCost;
        int baseRestore = econ.Building("tavern").MoraleRestore;
        int baseRookie = econ.Coach.RookieLevel;

        Assert.AreEqual(baseCost, stock.EffectiveReliefCost(baseCost), "未升级 ⇒ 生效值 = 基准");
        Assert.AreEqual(baseRestore, stock.EffectiveMoraleRestore("tavern", baseRestore), "未升级 ⇒ 恢复量 = 基准");

        // 备足传家宝（模拟多趟积累）
        foreach (string k in cfg.Kinds)
        {
            stock.Add(log, k, 50, "test");
        }

        Assert.IsTrue(stock.TryUpgrade(log, "tavern"), "酒馆可升级（降费轴）");
        Assert.IsTrue(stock.EffectiveReliefCost(baseCost) < baseCost,
            $"🔴 升级**真的改变数字**：减压价 {baseCost} 到 {stock.EffectiveReliefCost(baseCost)}");

        Assert.IsTrue(stock.TryUpgrade(log, "abbey"), "修道院可升级（增强轴）");
        Assert.IsTrue(stock.EffectiveMoraleRestore("abbey", econ.Building("abbey").MoraleRestore) > econ.Building("abbey").MoraleRestore,
            "修道院恢复量上升（升级真的改变数字）");

        Assert.IsTrue(stock.TryUpgrade(log, "stagecoach"), "驿站马车可升级（解锁轴）");
        Assert.IsTrue(stock.EffectiveRosterCap(8, hardCap: 12) > 8, "名册上限被解锁轴抬高");
        Assert.AreEqual(baseRookie, stock.EffectiveRookieLevel(baseRookie), "Lv1 尚未解锁新兵起始等级 ⇒ 仍为基准");

        Assert.IsTrue(log.Events.OfType<BuildingUpgradedEvent>().Count() == 3, "每次升级必写事件");
    }

    [TestMethod]
    public void M81_3_UpgradeRefused_WhenHeirloomsInsufficient_NoPartialSpend()
    {
        var log = new CombatLog();
        HeirloomConfig cfg = Cfg();
        var stock = new HeirloomStock(cfg);

        Assert.IsFalse(stock.TryUpgrade(log, "tavern"), "零传家宝 ⇒ 拒绝");
        Assert.AreEqual(0, stock.LevelOf("tavern"), "拒绝时等级不变");

        // 只给"刚好差一点"的量：tavern Lv1 需要 crests3 与 portraits2
        stock.Add(log, "crests", 2, "test");
        stock.Add(log, "portraits", 2, "test");
        Assert.IsFalse(stock.TryUpgrade(log, "tavern"), "crests 少 1 ⇒ 拒绝");
        Assert.AreEqual(2, stock.Count("portraits"), "🔴 **拒绝时不得部分扣**（portraits 仍是 2）");
        Assert.AreEqual(0, stock.LevelOf("tavern"), "等级不变");
    }

    [TestMethod]
    public void M81_3_UpgradeIsScarce_SoChoiceMatters()
    {
        HeirloomConfig cfg = Cfg();
        int tavernLv1 = cfg.PathFor("tavern").Levels[0].Cost.Values.Sum();
        int coachLv1 = cfg.PathFor("stagecoach").Levels[0].Cost.Values.Sum();

        // 一趟（三场暗档）拿到的传家宝**不足以**一次升满三栋 ⇒ 存在"先升哪个"的决策
        var stock = new HeirloomStock(cfg);
        var log = new CombatLog();
        for (int i = 0; i < 3; i++)
        {
            stock.AwardForTier(log, "shadowy");
        }

        int total = cfg.Kinds.Sum(k => stock.Count(k));
        int allThree = cfg.UpgradePaths.Sum(p => p.Levels[0].Cost.Values.Sum());
        string report = $"[M8.1] 稀缺性：一趟（shadowy×3）得传家宝 {total} 份；三栋首级共需 {allThree} 份" +
                        $"（tavern Lv1 {tavernLv1} ／ stagecoach Lv1 {coachLv1}）⇒ 一趟**升不满三栋** ⇒ 必须选先升哪个";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(total < allThree, "🔴 一趟的收入必须**升不满全部**（否则没有取舍）");
    }

    public TestContext TestContext { get; set; } = null!;
}
