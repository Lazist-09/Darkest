using System;
using Darkest.Core.Rng;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M2 确定性收口（T-M2-02/T-M2-10 骨架）：同 seed 双跑逐值一致、DrawCount 连续无跳号、
/// 骰子比较约定 `rand(0,100) &lt; 判定值`（阈值本身不命中）的边界。
/// 完整"同命令流事件日志一致"用例随 DamagePipeline 集成（T-M2-04~09）后收口。
/// </summary>
[TestClass]
public sealed class DeterminismTests
{
    private const long Seed = 20260909L;

    [TestMethod]
    public void SameSeed_TwoProviders_IdenticalAndDrawCountContinuous()
    {
        var a = new RngProvider(Seed);
        var b = new RngProvider(Seed);

        // Percent 通道：逐次断言 抽前 DrawCount==i、抽后 ==i+1（连续无跳号）
        for (int i = 0; i < 64; i++)
        {
            Assert.AreEqual((ulong)i, a.DrawCount, $"Percent 第 {i} 次抽前 DrawCount 应为 {i}");
            Assert.AreEqual(a.DrawCount, b.DrawCount);
            double va = a.NextPercent();
            double vb = b.NextPercent();
            Assert.AreEqual(va, vb, 1e-12, $"Percent 第 {i} 次应一致");
            Assert.AreEqual((ulong)(i + 1), a.DrawCount);
            Assert.AreEqual(a.DrawCount, b.DrawCount);
        }

        // Int 通道：连续性与一致性
        for (int i = 0; i < 32; i++)
        {
            ulong before = a.DrawCount;
            int ia = a.NextInt(0, 100);
            int ib = b.NextInt(0, 100);
            Assert.AreEqual(ia, ib);
            Assert.AreEqual(before + 1, a.DrawCount, "NextInt 也必须使 DrawCount +1");
            Assert.AreEqual(a.DrawCount, b.DrawCount);
        }
    }

    [TestMethod]
    public void DifferentSeeds_FirstDrawsDiverge()
    {
        var a = new RngProvider(1);
        var b = new RngProvider(2);
        bool diverged = false;
        for (int i = 0; i < 8 && !diverged; i++)
        {
            if (Math.Abs(a.NextPercent() - b.NextPercent()) > 1e-9)
            {
                diverged = true;
            }
        }

        Assert.IsTrue(diverged, "不同 seed 的抽取序列必须迅速分叉");
    }

    [TestMethod]
    public void RollThreshold_Boundary_LessThanMeansSuccess()
    {
        // 通用判定约定（combat_math 文档头）：rand(0,100) < 判定值 算成功，阈值本身不命中。
        static bool Hit(double roll, int threshold) => roll < threshold;

        Assert.IsTrue(Hit(50.0, 55), "50 < 55 → 命中");
        Assert.IsFalse(Hit(55.0, 55), "roll==阈值 → 未命中（不做必中）");
        Assert.IsFalse(Hit(100.0, 100), "上限 100 仍可不命中");
        Assert.IsTrue(Hit(99.99, 100), "99.99 < 100 → 命中");
    }
}