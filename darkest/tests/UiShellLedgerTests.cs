using System;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **B-3「回落必须留痕」的可数一半** 的用例：
///   架构要的答案是"**还差哪几屏没面板化**" ⇒ 本用例把它钉成**可断言** ✓
/// ⚠️ 真实数据仍来自两处调用点（`BattleRoot.GoToHamlet` 在飞 / `UIRoot.ShowPanel` 属 UI 域）✓
/// </summary>
[TestClass]
public sealed class UiShellLedgerTests
{
    [TestMethod]
    public void ReportsWhichScreensAreStillNotPanelized()
    {
        UiShellLedger.Reset();
        try
        {
            UiShellLedger.Record("hamlet", UiShellLedger.ShellRoute.Panel);
            UiShellLedger.Record("battle", UiShellLedger.ShellRoute.SceneFallback);
            UiShellLedger.Record("raid_results", UiShellLedger.ShellRoute.NotWired);
            UiShellLedger.Record("hamlet", UiShellLedger.ShellRoute.Panel); // 重复记录 ⇒ 不重复计数 ✓

            Assert.AreEqual(3, UiShellLedger.Count, "3 块屏 ✓");
            Assert.AreEqual(1, UiShellLedger.CountOf(UiShellLedger.ShellRoute.Panel), "面板 1 ✓");
            Assert.AreEqual(1, UiShellLedger.CountOf(UiShellLedger.ShellRoute.SceneFallback), "回落 1 ✓");
            Assert.AreEqual(1, UiShellLedger.CountOf(UiShellLedger.ShellRoute.NotWired), "未接线 1 ✓");

            // 🔴 架构要的那句话
            var missing = UiShellLedger.NotPanelizedScreens;
            CollectionAssert.AreEqual(new[] { "battle", "raid_results" }, missing.ToArray(),
                "还差哪几屏 = 回落 + 未接线（不含已面板化的 hamlet ✓）");

            string r = UiShellLedger.Report();
            StringAssert.Contains(r, "还差 2 屏");
            StringAssert.Contains(r, "battle");
            Console.WriteLine($"[B-3] {r}");
            Console.WriteLine($"[B-3] 还差清单 = {string.Join(", ", missing)} ✓");
        }
        finally
        {
            UiShellLedger.Reset();
        }

        TestContext.WriteLine("[B-3] 回落账本：可数与可断言 ✓");
    }

    [TestMethod]
    public void LaterRecordForTheSameScreenWins()
    {
        UiShellLedger.Reset();
        try
        {
            UiShellLedger.Record("hamlet", UiShellLedger.ShellRoute.NotWired);
            UiShellLedger.Record("hamlet", UiShellLedger.ShellRoute.Panel);   // 后来居上 ✓
            Assert.AreEqual(1, UiShellLedger.Count, "同一屏只记一条 ✓");
            Assert.AreEqual(0, UiShellLedger.NotPanelizedScreens.Count, "已面板化后应从'还差'里消失 ✓");
            Console.WriteLine($"[B-3] 后来居上：{UiShellLedger.Report()}");
        }
        finally
        {
            UiShellLedger.Reset();
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
