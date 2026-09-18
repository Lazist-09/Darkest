using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **P2 ③ 战斗层对照读数**（策划裁定 · `hamlet_loop.md §9.2`：「**要补 ③**」）——
/// 问题原话：**"士气更高 ⇒ **同 seed** 战斗更不容易崩"**。
///
/// 🔴 **为什么必须补**（策划的理由，我照抄）：①②（规则层/经济层）证明的是**「数字变了」** ⇒
///    **必要条件**；而 **A4（感受到）** 要的是**玩家感受到**的「**这趟没那么容易崩**」⇒ **③ 才是充分条件** ✓
///
/// 口径与纪律：
///   · **只报数不判红**（`#387`/探针纪律）· **零数值改动**（`data/` 一字不动 ✓）
///   · 开局士气 = **用例输入**（不是游戏数值）；**同 seed** ⇒ 唯一变量就是士气 ✓
///   · 唯一的结构性判据：**注入必须真的生效**（两档结果不能完全相同）✓
/// </summary>
[TestClass]
public sealed class P2BattleLayerReadingsTests
{
    private const int Seeds = 12;          // 12 个 seed（确定性）
    private const int LowMorale = 40;      // 低压档（用例输入）
    private const int HighMorale = 75;     // 高压档（用例输入）

    [TestMethod]
    public void SameSeeds_HigherOpeningMorale_IsItLessLikelyToCollapse()
    {
        var rows = new List<string>();
        int lowCollapse = 0, highCollapse = 0;
        int lowWeak = 0, highWeak = 0;
        int lowWins = 0, highWins = 0;
        long lowRounds = 0, highRounds = 0;
        bool anyDifference = false;

        for (int i = 0; i < Seeds; i++)
        {
            long seed = 20260921 + i;
            (GameOutcome lo, _) = HeadlessDriver.Run(seed, PolicyKind.SemiRandom, openingMorale: LowMorale);
            (GameOutcome hi, _) = HeadlessDriver.Run(seed, PolicyKind.SemiRandom, openingMorale: HighMorale);

            lowCollapse += lo.CollapseCount;
            highCollapse += hi.CollapseCount;
            lowWeak += lo.WeakCount;
            highWeak += hi.WeakCount;
            lowWins += lo.Result == GameResult.PlayerVictory ? 1 : 0;
            highWins += hi.Result == GameResult.PlayerVictory ? 1 : 0;
            lowRounds += lo.Rounds;
            highRounds += hi.Rounds;

            if (lo.Result != hi.Result || lo.Rounds != hi.Rounds || lo.CollapseCount != hi.CollapseCount)
            {
                anyDifference = true;
            }

            rows.Add($"[P2·战斗层] seed {seed}：士气 {LowMorale} ⇒ {lo.Result}／回合 {lo.Rounds}／崩 {lo.CollapseCount}　" +
                     $"｜ 士气 {HighMorale} ⇒ {hi.Result}／回合 {hi.Rounds}／崩 {hi.CollapseCount}");
        }

        var lines = new List<string>
        {
            $"[P2·战斗层] 同 seed × 开局士气 **{LowMorale} vs {HighMorale}**（{Seeds} 个 seed · 只报数不判红）：",
            $"  · 崩溃次数（合计）：{LowMorale} ⇒ **{lowCollapse}**　｜　{HighMorale} ⇒ **{highCollapse}**" +
            $"　（差 {highCollapse - lowCollapse}）",
            $"  · 虚弱次数（合计）：{LowMorale} ⇒ {lowWeak}　｜　{HighMorale} ⇒ {highWeak}",
            $"  · 胜场（合计）：{LowMorale} ⇒ {lowWins}/{Seeds}　｜　{HighMorale} ⇒ {highWins}/{Seeds}",
            $"  · 平均回合：{LowMorale} ⇒ {(double)lowRounds / Seeds:F1}　｜　{HighMorale} ⇒ {(double)highRounds / Seeds:F1}",
            $"  ⇒ 📌 **读数结论**：{(highCollapse < lowCollapse ? "高士气**崩溃更少** ⇒ §9.2 的 ③ **成立** ✓" : highCollapse == lowCollapse ? "两档崩溃数**相同** ⇒ ③ **在这批 seed 上不成立**（如实报，不美化）⚠️" : "高士气崩溃**更多** ⇒ ③ 不成立 ⚠️（如实报）")}",
        };

        lines.AddRange(rows);

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        // 🔴 唯一的结构性判据：**注入必须真的生效**（否则整份对照都是在测同一件事）✓
        Assert.IsTrue(anyDifference, "开局士气注入必须真的改变战斗过程（否则本对照无效）✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
