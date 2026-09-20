using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M15-P0（架构裁定取 (b)）**：`RunStartSnapshot` 记 **"上一次收尾时的队伍 HP%"**，
///   口径 = 「**上次收尾 vs 本次出发**」——而**出发按契约恒满血**（`#245`）⇒ 所以记的是**上趟收尾**的值 ✓
///
/// 本用例钉两件：
///   ① **给了就报**（`上次收尾 X% → 本次出发 100%`）+ **跨趟对比**（两边都有值才比）✓
///   ② 🔴 **没给就如实说"未接线"**（**不假装 100%** —— 这是"不给不可解释的默认"红线 21 家族）✓
/// </summary>
[TestClass]
public sealed class SnapshotHpPercentTests
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

    [TestMethod]
    public void GivenHpPercent_ReportsItAndComparesAcrossRuns_ElseSaysUnwired()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        var roster = new Roster(cfg);

        // ① 未接线 ⇒ 如实说"未接线"，**不假装** 100%
        RunStartSnapshot noWiring = RunStartSnapshot.Capture(1, roster);
        Assert.IsNull(noWiring.HpPercentLastRunEnd, "缺省 ⇒ 不假装有值 ✓");
        StringAssert.Contains(noWiring.Describe(), "未接线", "没接线就必须如实写「未接线」（不假装 ✓）");

        // ② 给了值 ⇒ 描述里出现"上次收尾 X% → 本次出发 100%"
        RunStartSnapshot first = RunStartSnapshot.Capture(1, roster, hpPercentLastRunEnd: 62);
        Assert.AreEqual(62, first.HpPercentLastRunEnd);
        StringAssert.Contains(first.Describe(), "上次收尾 62%");
        StringAssert.Contains(first.Describe(), "本次出发 100%");

        // ③ 跨趟对比：两边都有值 ⇒ 出对比行（62 → 48）
        RunStartSnapshot second = RunStartSnapshot.Capture(2, roster, hpPercentLastRunEnd: 48);
        var lines = second.DiffLines(first);
        string hit = lines.FirstOrDefault(l => l.Contains("队伍 HP%")) ?? "(没有对比行)";
        Assert.IsTrue(lines.Any(l => l.Contains("上趟收尾队伍 HP%")), $"跨趟应比「上趟收尾队伍 HP%」：{hit}");

        // ④ 只有一边有值 ⇒ **不比**（不与"未接线"混淆 ✓）
        Assert.IsFalse(second.DiffLines(noWiring).Any(l => l.Contains("上趟收尾队伍 HP%")),
            "一边未接线 ⇒ 不得凭空比较 ✓");

        Console.WriteLine($"[M15·HP%] 未接线 ⇒ 如实写 ✓ · 给了 ⇒ `{first.Describe().Split('　').Last()}` ✓ · 跨趟 ⇒ {hit} ✓");
        TestContext.WriteLine("[M15·HP%] (b) 口径：上次收尾 vs 本次出发 + 未接线不假装 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
