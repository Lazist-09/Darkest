using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M7 · 名册/招募对齐**（策划 `#423`）：**上限的单一来源 = 马车** ✓
///   · 上限曲线 **9 → 12 → 16 → 20 → 24 → 28**（6 值 = 未升级 + `stage_coach.rostersize` 的 a~e 五档）
///   · 招募刷新 **2 ~ 7 人**（端点由策划给）
///   · 高级新兵 **18.75 / 12.5 / 6.25 %**（对应 `upgraded_recruits` 的 a/b/c）
///
/// 🔴 本用例还做一次**跨文件一致性**断言：曲线的**长度**必须 = `buildings.json` 里
///   `stage_coach.rostersize` 的**档数 + 1** ⇒ 把"单一来源"从口号变成**可测** ✓
/// </summary>
[TestClass]
public sealed class StagecoachCurvesTests
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
    public void TheThreeCurves_MatchTheRuledNumbers_AndTheCapLengthTracksTheStagecoachTree()
    {
        EconomyConfig econ = EconomyConfig.Parse(ReadData("economy.json"));
        StagecoachConfig c = econ.Coach;

        CollectionAssert.AreEqual(new[] { 9, 12, 16, 20, 24, 28 }, c.RosterCapByLevel!.ToArray(),
            "上限曲线必须是 9→12→16→20→24→28（策划 #423）✓");
        Assert.AreEqual(28, c.CapCeiling, "最终硬上限 = 曲线末值 = 28 ✓");
        Assert.AreEqual(c.CapCeiling, c.MaxRoster, "max_roster 必须等于曲线末值（单一来源自洽）✓");

        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7 }, c.NumRecruitsByLevel!.ToArray(),
            "招募刷新 2~7（端点由策划给；中间为等步长 ramp）✓");
        CollectionAssert.AreEqual(new[] { 18.75, 12.5, 6.25 }, c.UpgradedRecruitChancesPct!.ToArray(),
            "高级新兵 18.75 / 12.5 / 6.25 %（对应 upgraded_recruits 的 a/b/c）✓");

        // 🔴 跨文件：曲线长度 = stage_coach.rostersize 的档数 + 1（未升级那一档）
        BuildingsConfig b = BuildingsConfig.Parse(ReadData("buildings.json"));
        int rosterSizeLevels = b.Get("stage_coach").Trees.Single(t => t.Id == "stage_coach.rostersize").Levels.Count;
        Assert.AreEqual(rosterSizeLevels + 1, c.RosterCapByLevel!.Count,
            $"上限曲线长度必须 = rostersize 档数({rosterSizeLevels}) + 1 ⇒ '单一来源' 可测 ✓");

        Console.WriteLine($"[M7] 曲线验收：cap {string.Join("→", c.RosterCapByLevel)}（rostersize {rosterSizeLevels} 档 + 1）"
            + $" · 招募 {string.Join("~", new[] { c.NumRecruitsByLevel![0], c.NumRecruitsByLevel[^1] })}"
            + $" · 高级新兵 {string.Join("/", c.UpgradedRecruitChancesPct)}% ✓");
        TestContext.WriteLine("[M7] 三条曲线 + 跨文件长度一致 ✓");
    }

    [TestMethod]
    public void Validation_RejectsADecreasingCapCurve_AndACapCeilingThatDisagreesWithMaxRoster()
    {
        string good = ReadData("economy.json");

        // ① 末值 ≠ max_roster ⇒ 红（"两处真值"防线）
        var mismatch = Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(good.Replace("\"max_roster\": 28", "\"max_roster\": 12")));
        StringAssert.Contains(mismatch.Message, "单一来源");

        // ② 曲线递减 ⇒ 红
        var dec = Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(good.Replace("[9, 12, 16, 20, 24, 28]", "[9, 12, 11, 20, 24, 28]")));
        StringAssert.Contains(dec.Message, "非递减");

        // ③ 招募曲线递减 ⇒ 红
        var rec = Assert.ThrowsException<InvalidDataException>(
            () => EconomyConfig.Parse(good.Replace("[2, 3, 4, 5, 6, 7]", "[2, 3, 2, 5, 6, 7]")));
        StringAssert.Contains(rec.Message, "非递减");

        Console.WriteLine($"[M7] 三条校验都拦得住：{mismatch.Message.Split('：').Last()} / {dec.Message.Split('：').Last()} / {rec.Message.Split('：').Last()} ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
