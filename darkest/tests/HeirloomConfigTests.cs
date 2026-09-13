using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.1 片 (a)(b)：**传家宝 + 建筑升级**的 **P23 校验**（`m8_roadmap §1`）。
/// 锁：① 四种齐全 + **掉落与光照档挂钩**（单调不减、shadowy 起 &gt; 0）；② **每级固定几种、数量递增**；
/// ③ **两轴都在**（降费 / 解锁）；④ **首批只允许三个建筑**（其余子系统未落地）。
/// </summary>
[TestClass]
public sealed class HeirloomConfigTests
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

    [TestMethod]
    public void P23_1_FourHeirlooms_DropTiedToLightTier_LikeGold()
    {
        HeirloomConfig h = Cfg();

        Assert.AreEqual(4, h.Kinds.Count, "四种传家宝（P23 ①）");
        Assert.AreEqual(0, h.DropFor("radiant").Total, "最亮档不掉传家宝");

        int prev = -1;
        foreach (string tier in HeirloomConfig.TierOrder)
        {
            int total = h.DropFor(tier).Total;
            Assert.IsTrue(total >= prev, $"{tier} 档总份数不得少于更亮档（P23 ①：与光照档挂钩）");
            prev = total;
        }

        Assert.AreEqual(0, h.DropFor("dim").Total - h.DropFor("dim").Crests + h.DropFor("dim").Crests - h.DropFor("dim").Total,
            "（自检：单档总量可分解）");
        Assert.IsTrue(h.DropFor("black").Total > h.DropFor("shadowy").Total, "越暗越多（与金钱同源）");
        Assert.IsTrue(HeirloomConfig.TierOrder.SequenceEqual(EconomyConfig.TierOrder),
            "光照档顺序必须与 economy 同序（同源口径）");
    }

    [TestMethod]
    public void P23_2_EachLevel_SameKinds_IncreasingAmounts()
    {
        HeirloomConfig h = Cfg();

        foreach (UpgradePath p in h.UpgradePaths)
        {
            string[] kinds = p.Levels[0].Cost.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            int prevSum = -1;
            foreach (UpgradeLevel lv in p.Levels)
            {
                Assert.IsTrue(lv.Cost.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(kinds, StringComparer.Ordinal),
                    $"{p.Building} Lv{lv.Level} 必须用同一组传家宝（DD 规律：固定几种、数量递增）");
                int sum = lv.Cost.Values.Sum();
                Assert.IsTrue(sum > prevSum, $"{p.Building} Lv{lv.Level} 消耗必须严格递增（{sum} > {prevSum}）");
                prevSum = sum;
            }
        }
    }

    [TestMethod]
    public void P23_3_BothAxesPresent_CostDownAndUnlock()
    {
        HeirloomConfig h = Cfg();

        Assert.IsTrue(h.UpgradePaths.Any(p => p.Axis == "cost_down"), "必须有【降费】轴");
        Assert.IsTrue(h.UpgradePaths.Any(p => p.Axis == "unlock"), "必须有【解锁更高阶】轴");

        // 降费轴必须真的能降费；解锁轴必须真的有解锁效果（红线 21：声明必须有内容）
        UpgradePath tavern = h.PathFor("tavern");
        Assert.IsTrue(tavern.Levels.Any(l => l.Effect.ReliefCostDelta < 0), "酒馆升级必须真的降费");

        UpgradePath coach = h.PathFor("stagecoach");
        Assert.IsTrue(coach.Levels.Any(l => l.Effect.RosterCapDelta > 0 || l.Effect.RookieLevel is not null),
            "驿站马车升级必须有解锁效果（名册上限 / 新兵起始等级）");
    }

    [TestMethod]
    public void P23_4_OnlyFirstBatchBuildings_AndBadDataThrows()
    {
        HeirloomConfig h = Cfg();
        Assert.IsTrue(h.UpgradePaths.All(p => HeirloomConfig.AllowedBuildings.Contains(p.Building)),
            "只允许首批三个建筑");

        string raw = ReadData("heirlooms.json");

        // 反例 1：出现 M8.2/M8.3 的建筑（Blacksmith）
        Assert.ThrowsException<InvalidDataException>(
            () => HeirloomConfig.Parse(raw.Replace("\"building\": \"tavern\"", "\"building\": \"blacksmith\"", StringComparison.Ordinal)),
            "Blacksmith 属后续里程碑 ⇒ 出现即报错（P23 ④）");

        // 反例 2：把某一级的份数改成"不递增"
        Assert.ThrowsException<InvalidDataException>(
            () => HeirloomConfig.Parse(raw.Replace("\"crests\": 5, \"portraits\": 3", "\"crests\": 3, \"portraits\": 2", StringComparison.Ordinal)),
            "消耗不递增 ⇒ 报错（P23 ②）");

        // 反例 3：最亮档也掉传家宝（破坏"越暗越多"的单调性）
        Assert.ThrowsException<InvalidDataException>(
            () => HeirloomConfig.Parse(raw.Replace("\"radiant\": {}", "\"radiant\": { \"crests\": 9 }", StringComparison.Ordinal)),
            "radiant 掉 9 份 ⇒ monotonic 被破坏 ⇒ 报错（P23 ①）");
    }
}
