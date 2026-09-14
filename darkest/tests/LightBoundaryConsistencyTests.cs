using System;
using System.Linq;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **光档边界（P21 ① 规定写死，不得改成 data）的单一常量源**：
/// 内核 `LightMeter.TierFor` 用它判档；UI `LightBarPanel` 的刻度**必须引用它**（否则刻度与判定会漂移）⚠️
///
/// ⚠️ 为什么这里**不**直接断言"UI 的刻度 == 内核常量"：**测试项目引用不到 `Darkest.Ui`**
///    （内核/测试刻意零表现层 —— 这是**正确的边界**，不是缺陷）⇒ 跨层一致性只能由**表现层自己的审计**去查 ✓
///    ⇒ 我已把"引用同一个常量"或"加一条一致性断言"作为**两条可照抄的改法**投给 UI 设计师 ✓
/// </summary>
[TestClass]
public sealed class LightBoundaryConsistencyTests
{
    [TestMethod]
    public void TierFor_UsesTheNamedConstants_AndKeepsContractSemantics()
    {
        // 边界语义（P21 ①）：>75 Radiant ／ 51..75 Dim ／ 26..50 Shadowy ／ 1..25 Dark ／ 0 Black ✓
        Assert.AreEqual(LightTier.Radiant, LightMeter.TierFor(LightMeter.RadiantMinExclusive + 1));
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(LightMeter.RadiantMinExclusive));
        Assert.AreEqual(LightTier.Dim, LightMeter.TierFor(LightMeter.DimMinExclusive + 1));
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(LightMeter.DimMinExclusive));
        Assert.AreEqual(LightTier.Shadowy, LightMeter.TierFor(LightMeter.ShadowyMinExclusive + 1));
        Assert.AreEqual(LightTier.Dark, LightMeter.TierFor(LightMeter.ShadowyMinExclusive));
        Assert.AreEqual(LightTier.Black, LightMeter.TierFor(0));
    }

    [TestMethod]
    public void TierBoundaries_ExposeExactlyTheThreeNamedConstants()
    {
        Assert.AreEqual(3, LightMeter.TierBoundaries.Length, "只有三个边界（25/50/75）✓");
        CollectionAssert.AreEquivalent(
            new[] { LightMeter.ShadowyMinExclusive, LightMeter.DimMinExclusive, LightMeter.RadiantMinExclusive },
            LightMeter.TierBoundaries,
            "边界数组必须与三个命名常量同源（防止只改一边）✓");
        Assert.IsTrue(LightMeter.TierBoundaries.SequenceEqual(new[] { 25, 50, 75 }),
            "契约数值：25 / 50 / 75（P21 ①）✓");
    }
}

