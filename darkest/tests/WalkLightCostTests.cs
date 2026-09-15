using System;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴🔴 **走格光照：总消耗守恒**（策划 `#338`① ＋ 架构 `P30` 附注④）——
/// 我先前草案写"1 段 = 1 格 ⇒ 每格 −30 平移"⇒ **前提不成立**（新模型里"一张图的格数远多于 6~8 段"）
/// ⇒ 照字面平移 = **总消耗 ×N = 静默改难度** ❌
///
/// ✅ 裁定规则：进段 `acc = segmentCost`；每格扣 `floor(acc / 本段剩余格数)`；**余数结转**；末格扣清 ✓
/// ⇒ 本用例的**唯一判据**：**一段走完，扣的总量【恰好等于】`segmentCost`**（与格数无关）✓
/// </summary>
[TestClass]
public sealed class WalkLightCostTests
{
    private const int SegmentCost = 30; // 现值（`move.new_area`）；用例里显式给，**代码里不写死** ✓

    private static int TotalDeductedOverSegment(int tiles)
    {
        int acc = SegmentCost;
        int total = 0;
        for (int i = 0; i < tiles; i++)
        {
            int remaining = tiles - i; // 含本格 ✓
            (int deduct, int newAcc) = WalkLightCost.StepCost(acc, remaining);
            total += deduct;
            acc = newAcc;
        }

        Assert.AreEqual(0, acc, "走完一段 ⇒ 余额必须为 0（余数不留到下一段，否则总消耗漂移）✓");
        return total;
    }

    [TestMethod]
    public void TotalDeducted_IsAlwaysExactlyTheSegmentCost_RegardlessOfTileCount()
    {
        // 🔴 关键判据：1 格 / 2 格 / 3 格 / 7 格 / 8 格 走廊 ⇒ **总扣除都是 30** ✓
        foreach (int tiles in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 13, 40 })
        {
            Assert.AreEqual(SegmentCost, TotalDeductedOverSegment(tiles),
                $"走廊 {tiles} 格 ⇒ 总消耗必须仍 = {SegmentCost}（否则就是【静默改难度】）⚠️");
        }
    }

    [TestMethod]
    public void EachTileDeduction_IsIntegerNonNegative_AndNeverOvershoots()
    {
        int acc = SegmentCost;
        for (int i = 0; i < 8; i++)
        {
            (int deduct, int newAcc) = WalkLightCost.StepCost(acc, remainingTilesInSegment: 8 - i);
            Assert.IsTrue(deduct >= 0, "扣量非负 ✓");
            Assert.IsTrue(deduct <= acc, "不许扣超过余额（否则会出现负余额）✓");
            Assert.AreEqual(acc - deduct, newAcc, "余额 = 原余额 − 扣量 ✓");
            acc = newAcc;
        }

        Assert.AreEqual(0, acc, "8 格走完 ⇒ 清零 ✓");
        Assert.AreEqual((0, 0), WalkLightCost.StepCost(0, remainingTilesInSegment: 5), "余额 0 ⇒ 不再扣 ✓");
    }

    [TestMethod]
    public void LastTile_SettlesTheRemainder()
    {
        // 30 分 7 格：前 6 格各扣 floor(…)= …，最后格一次扣清 ⇒ 不漂移 ✓
        int acc = SegmentCost;
        int total = 0;
        for (int remaining = 7; remaining >= 1; remaining--)
        {
            (int deduct, int newAcc) = WalkLightCost.StepCost(acc, remaining);
            total += deduct;
            acc = newAcc;
            if (remaining == 1)
            {
                Assert.IsTrue(deduct > 0, "末格要把**余数**扣清（否则余额不为 0）✓");
            }
        }

        Assert.AreEqual(SegmentCost, total, "7 格总计 = 30 ✓");
    }

    [TestMethod]
    public void ZeroRemainingTiles_Throws_InsteadOfSilentFallback()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => WalkLightCost.StepCost(30, remainingTilesInSegment: 0),
            "剩余格数 0 ⇒ **抛错**（不静默兜底：那是调用方状态错误）✓");
    }

    [TestMethod]
    public void TrapTile_IsAnEnumSlotOnly_WithNoEffectYet()
    {
        // 🔴 策划 `#338`③：陷阱**本次只留枚举位**（行为/触发规则暂留）⇒ 可通行、且**当前没有任何效果** ✓
        DungeonGrid g = DungeonGrid.Parse("t", new[] { "R^G" });
        Assert.AreEqual(DungeonTileKind.Trap, g.TileAt(1, 0), "`^` ⇒ 陷阱枚举位 ✓");
        Assert.IsTrue(DungeonTileMap.IsWalkable(DungeonTileKind.Trap), "当前**可通行**（陷阱行为暂留）✓");
        Assert.AreEqual('^', DungeonTileMap.ToChar(DungeonTileKind.Trap), "字符往返一致 ✓");

        var w = new DungeonWalker(g, (0, 0));
        Assert.IsTrue(w.TryStep(1, 0), "可以走进陷阱格（暂未接规则）✓");
        Assert.AreEqual(DungeonTileKind.Trap, w.CurrentTile,
            "落格**如实报出**是陷阱 ⇒ 将来规则接入时由调用方在此触发（现在什么都不做）✓");
        Assert.AreEqual(1, w.StepsTaken, "走格计数不受「暂留」影响 ✓");
    }
}
