using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🆕 **进度可见性读数**（做功能 · A4 同族）：`RunProgress.NextUnlock(...)` ⇒
///   **"下一个解锁是什么 + 还差几趟/几胜"**（玩家能看见"离下一个目标还有多远" ✓）。
/// 本用例只做一件事：**把随趟数推进的变化读出来**（并钉一条最弱的结构性判据：缺口**单调不增**）✓
/// </summary>
[TestClass]
public sealed class NextUnlockProgressionTests
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
    public void NextUnlock_ReportsRemainingRuns_AndNeverIncreases()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        UnlocksConfig unlocks = UnlocksConfig.Parse(ReadData("unlocks.json"),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            rosterHardCap: 12);

        var progress = new RunProgress();
        var log = new CombatLog();
        // 🔴 我第一版断言"**最近那条**的缺口单调不增" ⇒ **错了**（目标切换时它会跳增 ⚠️）
        //    ⇒ 改用两个**真正单调**的量：**已解锁项数不减** · **全部缺口之和（含所有条目）不增** ✓
        int prevUnlocked = -1;
        int prevTotalGap = int.MaxValue;
        var lines = new List<string>();

        foreach (int target in new[] { 0, 2, 6, 10, 14 })
        {
            while (progress.RunsFinished < target)
            {
                progress.FinishRun(log, "completed", battlesWon: 3);
            }

            (UnlockEntry Entry, int RunsRemaining, int BattlesRemaining)? next = progress.NextUnlock(unlocks);
            string desc = next is { } n
                ? $"{string.Join("/", n.Entry.Unlocks)}（还差 **{n.RunsRemaining} 趟** ／ {n.BattlesRemaining} 胜）"
                : "**全部已解锁** ✓";
            int remaining = next is { } m ? m.RunsRemaining + m.BattlesRemaining : 0;
            int totalGap = unlocks.Unlocks.Sum(e =>
                Math.Max(0, e.RequiredRunsFinished - progress.RunsFinished)
                + Math.Max(0, e.RequiredBattlesWon - progress.BattlesWon));

            lines.Add($"[下一解锁] 已完成 {progress.RunsFinished} 趟 ⇒ {desc}　（全部条目缺口合计 {totalGap}）");
            int unlockedNow = progress.UnlockedIds(unlocks).Count;
            Assert.IsTrue(unlockedNow >= prevUnlocked, "**已解锁项数不减**（进度不能倒退 ✓）");
            Assert.IsTrue(totalGap <= prevTotalGap || prevTotalGap == int.MaxValue,
                "**全部缺口之和必须不增**（总量单调 ✓）");
            prevUnlocked = unlockedNow;
            prevTotalGap = totalGap;
        }

        lines.Add($"[下一解锁] {progress.Audit(unlocks, 28)}");   // 🔴 M7②：硬上限 12→28 ✓

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        Assert.IsTrue(lines.Count == 6, "每个采样点各一行 + 一行 Audit ✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
