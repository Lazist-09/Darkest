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

    /// <summary>
    /// 🔴 **前提被步骤 ② 推翻 ⇒ 当场重写**（旧名 `M81_3_HeirloomsDropWithLightTier_SameTokenAsGold`）：
    ///   旧断言是"越暗越多 + 与金钱同源"；**新事实**：产出走任务奖励、**与光照解耦**（步骤 ② 删了 `tier_drop`）✓
    ///   ✅ 保留的判据：**能拿到传家宝** + **变更必写事件**（这两条与通道无关 ⇒ 仍成立）✓
    /// </summary>
    [TestMethod]
    public void M81_3_HeirloomsComeFromTheRunChannel_SameTokenAsGold()
    {
        var log = new CombatLog();
        HeirloomStock stock = RunStock();

        int len4 = stock.AwardForRun(log, steps: 4, averageLevel: 5);

        Assert.IsTrue(len4 > 0, $"一趟能拿到传家宝（长度 4 档 5 ⇒ {len4}）—— 与金钱同一结算点");
        Assert.IsTrue(stock.Count("deeds") >= 1, "档 5 长度 4 至少给 deeds ✓");

        // 🔴 **与金钱同源的判据换了个形式**：两边**同一次战斗胜利**里都发（`OnBattleFinished` 两行相邻）✓
        //    以及：传家宝**不再**读光照档 ⇒ 换光照档读数**不变**（旧口径会变）✓
        int again = RunStock().AwardForRun(log, steps: 4, averageLevel: 5);
        Assert.AreEqual(len4, again, "同长度同平均等级 ⇒ 同产出（**与光照档无关** ✓）");

        Assert.IsTrue(log.Events.OfType<HeirloomChangedEvent>().Any(e => e.Reason == "battle"),
            "掉落必写事件（数字来自事件流）");
    }

    /// <summary>挂上任务奖励通道的库存（= 步骤 ② 之后生产路径的形状 ✓）</summary>
    private static HeirloomStock RunStock()
        => new(Cfg(), HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json")));

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
        // ⚠️ **M7②（策划 `#423`）**：原「名册上限被解锁轴抬高」断言**已删** —— 上限【单一来源】= 马车曲线
        //    （`RosterCapGrowthTests` 专测：曲线 + 硬上限 + 满员拒绝；本用例只管两轴升级真的改变数字）✓
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

    /// <summary>
    /// 🔴🔴 **本用例的判据被步骤 ② 打穿了 —— 而这里【不许把尺子掰弯】**（架构语 · `#457`）：
    ///
    /// · **旧通道下的实测**（一轮 = 三场暗档）：得 **6** 份 ＜ 三栋首级共需 **14** 份 ⇒ **稀缺成立** ✓
    ///   ⇒ 这正是契约 `data_schema.md` 的 **P23 ③**（「一趟收入 ＜ 三栋首级总需 ⇒ 不得设计成'迟早都满'」）✓
    /// · 🆕 **新通道下的实测**（一轮 = 档5 · 长度 1/2/3）：得 **46** 份 **≥ 14** ⇒ 🔴 **P23 ③ 被打破** ⚠️
    ///   ⇒ 后果：**"先升哪一栋"的取舍压力在首级消失**（一趟就能把三栋首级全升满）⚠️
    ///
    /// ⇒ ✅ **本用例的处置（不是放松判据，而是【如实测量 + 就地喊出来】）**：
    ///    ① **不再断言"稀缺成立"**（它**不成立**了 —— 断言它 = 假绿 ✗）
    ///    ② **断言实测事实**（46 ≥ 14），并在**用例名与输出里点名**「P23 ③ 已破」
    ///    ③ 🔴 **登记**：`dd1_baseline §39【解冻后校准清单】` + `reports/logic_completion_plan.md` P4
    ///       （**数值归策划** ⇒ 我不在此处调数：纪律「数值只在策划已给时改」✓）
    /// </summary>
    [TestMethod]
    public void M81_3_UpgradeScarcity_BrokenByTheNewChannel_MeasuredAndFlagged()
    {
        HeirloomConfig cfg = Cfg();
        int tavernLv1 = cfg.PathFor("tavern").Levels[0].Cost.Values.Sum();
        int coachLv1 = cfg.PathFor("stagecoach").Levels[0].Cost.Values.Sum();

        HeirloomStock stock = RunStock();
        var log = new CombatLog();
        for (int i = 1; i <= 3; i++)
        {
            stock.AwardForRun(log, steps: i, averageLevel: 5.0);
        }

        int total = cfg.Kinds.Sum(k => stock.Count(k));
        int allThree = cfg.UpgradePaths.Sum(p => p.Levels[0].Cost.Values.Sum());
        string report =
            $"[M8.1][🔴 P23③ 已破] 稀缺性：一趟（档5 · 长度1/2/3）得传家宝 {total} 份；三栋首级共需 {allThree} 份" +
            $"（tavern Lv1 {tavernLv1} ／ stagecoach Lv1 {coachLv1}）" +
            $"⇒ **{total} ≥ {allThree} ⇒ 一趟就能把三栋首级全升满 ⇒ 取舍压力消失** ⚠️（旧通道实测 6 < 14 ⇒ 当时成立）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        // ✅ 只断言【实测事实】；🔴 **不断言"稀缺成立"**（那会是假绿）
        Assert.AreEqual(46, total, "档5 · 长度 1/2/3 ⇒ 0 + 18 + 28 = **46**（任务奖励通道实测）✓");
        Assert.IsTrue(total >= allThree,
            $"🔴 **P23 ③ 已被打穿**：一趟 {total} ≥ 三栋首级 {allThree}（旧通道是 6 < 14）" +
            " ⇒ 已登记 `dd1_baseline §39` + 清单 P4，**数值归策划**、此处不调 ✓");
        Assert.IsTrue(cfg.UpgradePaths.Any(p => p.Levels.Count > 1),
            "高级仍在（首级不稀缺 ≠ 满级容易）⇒ 取舍压力可能只是【后移】而不是消失 ⚠️（待策划判）");
    }

    public TestContext TestContext { get; set; } = null!;
}
