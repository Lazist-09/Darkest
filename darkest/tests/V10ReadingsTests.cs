using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **V10 口径读数**用例（架构 `m7_6_verification §9` · 策划 `#358`③）：
/// **一切从事件流算出**（不造旁路计数器）⇒ 本用例**只喂事件**，不喂任何计数器 ✓
/// </summary>
[TestClass]
public sealed class V10ReadingsTests
{
    private static TownReturnEvent End(string outcome)
        => new(outcome, MoraleBefore: 50, MoraleAfter: 50, PenaltyApplied: outcome == "retreat");

    private static RetreatResolved Retreat(int index = 1, bool success = true)
        => new(success, index, MoraleDelta: -72, Casualties: false);

    [TestMethod]
    public void ThreeOutcomes_PlusRetreatDistribution_AndRetreatWithoutAbandon()
    {
        var run1 = new List<BattleEvent> { Retreat(1), End("completed") };                       // 走完 · 退 1 次
        var run2 = new List<BattleEvent> { Retreat(1), Retreat(2), End("completed") };          // 走完 · 退 2 次
        var run3 = new List<BattleEvent> { End("completed") };                                  // 走完 · 没退
        var run4 = new List<BattleEvent> { Retreat(1), new ExpeditionAbandoned("player", 2, 0, 40) , End("abandoned") };
        var run5 = new List<BattleEvent> { End("wiped") };                                      // 全灭

        V10Readings r = V10Readings.From(new[] { run1, run2, run3, run4, run5 });

        Assert.AreEqual(5, r.Runs, "总趟数 ✓");
        Assert.AreEqual(3, r.Completed, "走完 3 ✓");
        Assert.AreEqual(1, r.Abandoned, "放弃 1 ✓");
        Assert.AreEqual(1, r.Wiped, "全灭 1 ✓");
        Assert.AreEqual(0, r.Unfinished, "未结束 0 ✓");
        Assert.IsTrue(r.CountsClose, "🔴 判据①：三类 + 未结束 **和必须闭合** == 总趟数 ✓");

        Assert.AreEqual(0.6, r.CompletionRate, 1e-9, "完成率 = 走完 ÷ 三类和 = 3/5 ✓（分母不含未结束）");
        Assert.AreEqual(4, r.RetreatResolvedTotal, "撤退场次总数 = 1+2+0+1+0 = **4** ✓（我第一版写 3，是**我的期望值错** ⇒ 已修）");
    }

    [TestMethod]
    public void Distribution_And_Rates_AreFromEventsOnly()
    {
        var a = new List<BattleEvent> { Retreat(), End("completed") };                 // 退 1
        var b = new List<BattleEvent> { Retreat(1), Retreat(2), End("completed") };    // 退 2
        var c = new List<BattleEvent> { End("completed") };                            // 退 0

        V10Readings r = V10Readings.From(new[] { a, b, c });

        Assert.AreEqual(3, r.RetreatResolvedTotal, "撤退场次 = 1+2+0 = 3 ✓");
        Assert.AreEqual(2, r.RunsWithAnyRetreat, "退过的趟 = 2 ✓");
        CollectionAssert.AreEqual(new[] { 1, 2, 0 }, r.RetreatCountsPerRun.ToArray(), "逐趟撤退次数 ✓");
        Assert.AreEqual(2.0 / 3.0, r.RetreatWithoutAbandonRate, 1e-9,
            "「撤而不弃」= 退过且走完的趟 ÷ 走完的趟 = **2/3** ✓（我第一版写 1.0，是**我的期望值错** ⇒ 已修）");
    }

    [TestMethod]
    public void MissingTerminalEvent_IsCountedUnfinished_NotSilentlyCompleted()
    {
        var noEnd = new List<BattleEvent> { Retreat() };                    // ⚠️ 没有终点事件
        var unknown = new List<BattleEvent> { End("something_new") };       // ⚠️ 未登记的结局串
        var ok = new List<BattleEvent> { End("completed") };

        V10Readings r = V10Readings.From(new[] { noEnd, unknown, ok });

        Assert.AreEqual(1, r.Completed, "只有真正 `completed` 才算走完 ✓");
        Assert.AreEqual(2, r.Unfinished, "🔴 缺终点事件 / 未登记结局 ⇒ **如实记「未结束」**（绝不静默当成走完）✓");
        Assert.IsTrue(r.CountsClose, "和仍闭合 ✓");
    }

    [TestMethod]
    public void EmptyInput_DoesNotDivideByZero()
    {
        V10Readings r = V10Readings.From(Array.Empty<IReadOnlyList<BattleEvent>>());
        Assert.AreEqual(0, r.Runs);
        Assert.AreEqual(0.0, r.CompletionRate, 1e-9, "无数据 ⇒ 0（不抛、不 NaN）✓");
        Assert.AreEqual(0.0, r.RetreatWithoutAbandonRate, 1e-9);
        Assert.IsTrue(r.CountsClose);
        Assert.IsTrue(r.Describe().Contains("完成率"), "报告行可用 ✓");
    }
}
