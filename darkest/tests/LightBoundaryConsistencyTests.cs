using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **光档边界 = 数据**（策划 `#328`① 裁 (b)：两侧都读 data ⇒ 唯一真相）：
/// 边界本来就在 `tuning.json` 的 `light.tiers`（每档 `min`/`max`）里，而**内核此前没用它**
/// （在本文件硬写 `>75/>50/>25`，UI 侧又抄一份 ⇒ 只改一处就"刻度与判定不符"）⚠️
///
/// 现在：内核 `TierFor(value, tiers)` 按**数据**取档；`BoundariesFrom(tiers)` 给 UI 刻度用 ✓
/// 本用例锁：① 语义与契约一致（76..100 Radiant ／ 51..75 Dim ／ 26..50 Shadowy ／ 1..25 Dark ／ 0 Black）
///           ② 边界**来自 data**（改数据 ⇒ 判定跟着变 ⇒ 不是硬编码）✓
/// </summary>
[TestClass]
public sealed class LightBoundaryConsistencyTests
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

    private static TuningLight Light() => TuningConfig.Parse(ReadData("tuning.json")).Light!;

    [TestMethod]
    public void TierSemantics_MatchContract_FromData()
    {
        IReadOnlyList<TuningLightTier> tiers = Light().Tiers;
        Assert.AreEqual(LightTier.Radiant, LightMeter.TierFor(100, tiers));
        Assert.AreEqual(LightTier.Radiant, LightMeter.TierFor(76, tiers));
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(75, tiers));
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(51, tiers));
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(50, tiers));
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(26, tiers));
        Assert.AreEqual(LightTier.Dark, LightMeter.TierFor(25, tiers));
        Assert.AreEqual(LightTier.Dark, LightMeter.TierFor(1, tiers));
        Assert.AreEqual(LightTier.Black, LightMeter.TierFor(0, tiers));
    }

    [TestMethod]
    public void Boundaries_AreDerivedFromData_NotHardcoded()
    {
        TuningLight light = Light();
        int[] marks = LightMeter.BoundariesFrom(light.Tiers);
        CollectionAssert.AreEqual(new[] { 25, 50, 75 }, marks, "契约数值（P21 ①）✓ —— 但它现在**来自 data** ✓");

        // 🔴「改数据就变」：给一组**不同的**档边（21..40 / 41..60 / 61..100，暗档 1..25 不变）
        //    ⇒ 判定与刻度**一起**跟着变（证明取值来自**传入的 data**，而不是文件里的硬编码）
        //    （我第一版用字符串补丁去改 JSON ⇒ 依赖格式、且失败信息不直观 ⇒ 改为**直接构造** ✓）
        var changed = new List<TuningLightTier>
        {
            new("black", 0, 0),
            new("dark", 1, 25),
            new("shadowy", 26, 40),
            new("dim", 41, 60),
            new("radiant", 61, 100),
        };
        CollectionAssert.AreEqual(new[] { 25, 40, 60 }, LightMeter.BoundariesFrom(changed));
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(55, changed), "改数据后 55 归 Dim（数据驱动 ⇒ 判定跟着变）✓");
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(30, changed), "30 也在新边界下归 Shadowy ✓");
    }

    [TestMethod]
    public void TiersCoverFullRange_Contiguously()
    {
        IReadOnlyList<TuningLightTier> tiers = Light().Tiers.OrderBy(t => t.Min).ToList();
        Assert.AreEqual(0, tiers.First().Min, "最低档从 0 起 ✓");
        Assert.AreEqual(100, tiers.Last().Max, "最高档到 100 ✓");
        for (int i = 1; i < tiers.Count; i++)
        {
            Assert.AreEqual(tiers[i - 1].Max + 1, tiers[i].Min,
                $"档位必须**无缝衔接**（否则某些值落到数据之外 ⇒ 会被判成 Black）⚠️ 断点：{tiers[i - 1].Id}→{tiers[i].Id}");
        }
    }
}
