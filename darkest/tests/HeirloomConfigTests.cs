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

    /// <summary>
    /// 🔴 **本用例的前提在步骤 ② 被推翻，故当场重写**（不是删掉了事 —— 纪律：不许把过期用例静默丢掉）：
    ///   · **旧前提**：`P23 ①` 要求「四种传家宝齐备 **且** 掉落与光照档挂钩（越暗越多）」
    ///   · **新事实**：策划裁定传家宝**不按光照档掉，改走任务奖励** ⇒ `tier_drop` 与其校验**已删**（步骤 ②）
    ///   ⇒ ✅ **保留 ① 里与光照无关的那半条**（四种齐备）；把"越暗越多"换成**与光照解耦**的正向判据 ✓
    /// </summary>
    [TestMethod]
    public void P23_1_FourHeirlooms_AndNoLightTierCouplingAnymore()
    {
        HeirloomConfig h = Cfg();

        Assert.AreEqual(4, h.Kinds.Count, "四种传家宝齐备（P23 ① 的另一半 ⇒ 保留）");
        Assert.IsTrue(h.Kinds.Contains("busts") && h.Kinds.Contains("crests")
            && h.Kinds.Contains("deeds") && h.Kinds.Contains("portraits"), "四种种名齐备 ✓");

        // 🔴 步骤 ② 的判据：**产出通道不再挂在光照档上** ⇒ `heirlooms.json` 里 must NOT 再有 tier_drop ✓
        string raw = ReadData("heirlooms.json");
        Assert.IsFalse(raw.Contains("\"tier_drop\"", StringComparison.Ordinal),
            "🔴 步骤 ② 已删 `tier_drop` ⇒ 数据里不得再有它（否则 = 两处真值 ⚠️）");
        Assert.IsFalse(raw.Contains("\"drop_note\"", StringComparison.Ordinal), "它的说明键同批删掉 ✓");
        Assert.IsTrue(raw.Contains("\"quest_reward\"", StringComparison.Ordinal), "替代通道仍在 ✓");

        // 🔴 而**光照档顺序**这个口径本身没消失（经济侧 `EconomyConfig.TierOrder` 仍在用 ✓）
        Assert.AreEqual(5, EconomyConfig.TierOrder.Count, "光照档仍是 5 档（经济侧在用 ⇒ 不许跟着删）✓");
        Assert.AreEqual("radiant", EconomyConfig.TierOrder[0], "同序（radiant 最亮）✓");

        Console.WriteLine("[传家宝·步骤②] P23 ① 的「越暗越多」已退出；四种齐备保留；"
            + "`tier_drop`/`drop_note` 已从数据删除 ✓；光照档口径由经济侧保留 ✓");
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

        // 反例 3：🔴 **已随 `tier_drop` 删除**（步骤 ②）——
        //    它原本构造「`radiant` 也掉 9 份 ⇒ 破坏越暗越多」来验 P23 ① 的单调性校验；
        //    `tier_drop` 与其校验**已删** ⇒ 这个反例**没有对应的被验对象了** ⇒ 如实删除（不是松掉它）✓
        //    🗑️ 替代覆盖：**「通道缺失 ⇒ 抛」**由 `HeirloomRunRewardWiringTests.NoChannel_ThrowsInsteadOfSilentlyAwardingZero` 承担 ✓
        Assert.IsFalse(raw.Contains("\"tier_drop\"", StringComparison.Ordinal),
            "🔴 反例 3 的前提已不存在（`tier_drop` 已删）⇒ 本用例不再验它，且**数据里确实没有它** ✓");
    }
}
