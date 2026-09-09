using System.Collections.Generic;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Director;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M0 最小确定性冒烟（T-M0-03 / 里程碑判据 #5）：
/// 同 seed 构造 BattleSession 两次 → 注入 RNG 的抽取序列逐一相等、DrawCount 单调递增；
/// CombatLog 追加 RngDraw 后原内容不可变（防篡改）。
/// </summary>
[TestClass]
public sealed class RngSmokeTests
{
    private const long Seed = 20260909L;

    [TestMethod]
    public void SameSeed_Sessions_ProduceIdenticalPercentDraws_AndMonotonicDrawCount()
    {
        BattleSession sessionA = new(Seed);
        BattleSession sessionB = new(Seed);

        const int draws = 64;
        var sequenceA = new double[draws];
        var sequenceB = new double[draws];

        for (int i = 0; i < draws; i++)
        {
            Assert.AreEqual(sessionA.Rng.DrawCount, sessionB.Rng.DrawCount,
                $"DrawCount diverged before draw {i}.");
            Assert.AreEqual((ulong)i, sessionA.Rng.DrawCount,
                $"DrawCount should equal {i} before draw {i} (monotonic audit).");

            sequenceA[i] = sessionA.Rng.NextPercent();
            sequenceB[i] = sessionB.Rng.NextPercent();

            Assert.AreEqual((ulong)(i + 1), sessionA.Rng.DrawCount,
                $"DrawCount must increase by exactly 1 after draw {i}.");
        }

        CollectionAssert.AreEqual(sequenceA, sequenceB,
            "Same seed must yield the identical draw sequence.");
    }

    [TestMethod]
    public void SameSeed_Sessions_ProduceIdenticalIntDraws_WithinRange()
    {
        BattleSession a = new(Seed);
        BattleSession b = new(Seed);

        for (int i = 0; i < 256; i++)
        {
            int va = a.Rng.NextInt(-3, 9);
            int vb = b.Rng.NextInt(-3, 9);
            Assert.AreEqual(va, vb, $"NextInt diverged at draw {i}.");
            Assert.IsTrue(va is >= -3 and < 9, $"NextInt out of range: {va}");
        }
    }

    [TestMethod]
    public void DifferentSeeds_Diverge()
    {
        BattleSession a = new(1L);
        BattleSession b = new(2L);
        var seqA = new List<double>();
        var seqB = new List<double>();
        for (int i = 0; i < 16; i++)
        {
            seqA.Add(a.Rng.NextPercent());
            seqB.Add(b.Rng.NextPercent());
        }

        CollectionAssert.AreNotEqual(seqA, seqB,
            "Different seeds must not share the full 16-draw sequence.");
    }

    [TestMethod]
    public void ExplicitRngInjection_IsHonoured()
    {
        var injected = new RngProvider(Seed);
        BattleSession session = new(Seed, injected);
        Assert.AreSame(injected, session.Rng, "BattleSession must expose the injected provider.");
    }

    [TestMethod]
    public void CombatLog_AppendsImmutableAuditedDraws_InOrder()
    {
        BattleSession session = new(Seed);
        CombatLog log = session.Log;
        var appended = new List<RngDraw>();

        for (int i = 0; i < 5; i++)
        {
            double value = session.Rng.NextPercent();
            // Audit serial = provider DrawCount right after the draw (blueprint §8.1).
            appended.Add(log.Append(new RngDraw(session.Rng.DrawCount, value)));
        }

        Assert.AreEqual(5, log.Count);
        Assert.AreEqual(5, log.Events.Count);
        for (int i = 0; i < appended.Count; i++)
        {
            Assert.AreSame(appended[i], log.Events[i],
                $"Event {i} instance must be the exact stamped record returned by Append.");
            Assert.AreEqual((ulong)i, log.Events[i].Sequence,
                $"Append sequence must be 0-based and in order at {i}.");
            Assert.AreEqual((ulong)(i + 1), ((RngDraw)log.Events[i]).DrawCount,
                "RngDraw must carry its monotonic provider audit serial.");
        }

        // 防篡改断言：追加新事件不得改动已追加内容。
        RngDraw firstBefore = (RngDraw)log.Events[0];
        _ = log.Append(new RngDraw(session.Rng.DrawCount, session.Rng.NextPercent()));
        Assert.AreEqual(6, log.Count);
        Assert.AreSame(firstBefore, log.Events[0], "Earlier events must not be replaced.");
        Assert.AreEqual(0UL, firstBefore.Sequence, "Earlier event content must stay unchanged.");
        Assert.AreEqual(1UL, ((RngDraw)log.Events[0]).DrawCount);
        Assert.AreEqual(appended[1], log.Events[1]);
    }
}
