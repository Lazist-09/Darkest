using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// v0.66 复测口径：**3 连战**（HP 与士气跨战斗完全保留、场间无任何恢复）。
/// 判据 = 完成率（3 场全部存活）∈ [40%, 70%]；并输出每场结束的 HP%/士气% 曲线（看斜率是否 ≈33%/场）。
/// </summary>
[TestClass]
public sealed class CampaignTests
{
    [TestMethod]
    public void ThreeBattleCampaign_CompletionRate_AndCurve()
    {
        const int runs = 300;
        const long seedBase = 20260909;
        const int battles = 3;

        int survived = 0;   // 口径 A：3 场都没全灭（撤退成功也算活下来）
        int strict = 0;     // 口径 B：撤退 = 放弃 run → 出现撤退即不算完成
        var hpByBattle = new double[battles + 1];
        var moraleByBattle = new double[battles + 1];
        var aliveByBattle = new double[battles + 1];
        var roundsByBattle = new double[battles + 1];
        var counted = new int[battles + 1];

        for (int i = 0; i < runs; i++)
        {
            RunOutcome c = HeadlessDriver.RunCampaign(seedBase + i, PolicyKind.SemiRandom, battles);
            if (c.CompletedCountingRetreat)
            {
                survived++;
            }

            if (c.CompletedStrict)
            {
                strict++;
            }

            foreach (RunBattleSnapshot s in c.Curve)
            {
                hpByBattle[s.Battle] += s.AvgHpPercent;
                moraleByBattle[s.Battle] += s.AvgMoralePercent;
                aliveByBattle[s.Battle] += s.AliveCount;
                roundsByBattle[s.Battle] += s.Rounds;
                counted[s.Battle]++;
            }
        }

        var lines = new List<string>
        {
            $"[M6v5] 3 连战（O-63 未裁定 → 仅作参考，不作验收基线）：runs={runs}",
            $"[M6v5] 完成率（口径 A 含撤退）：{(double)survived / runs:P0}（{survived}/{runs}）" +
            $"　｜口径 B 撤退即放弃：{(double)strict / runs:P0}（{strict}/{runs}）",
        };

        for (int b = 1; b <= battles; b++)
        {
            if (counted[b] == 0)
            {
                continue;
            }

            double hp = hpByBattle[b] / counted[b];
            double morale = moraleByBattle[b] / counted[b];
            double alive = aliveByBattle[b] / counted[b];
            double rounds = roundsByBattle[b] / counted[b];
            string slope = b == 1 ? string.Empty : $"（HP 斜率 {hp - (hpByBattle[b - 1] / Math.Max(1, counted[b - 1])):+0.0;-0.0;0.0}%/场）";
            lines.Add($"[M6v5] 第 {b} 场结束：存活 {alive:F2}/6　HP {hp:F1}%　士气 {morale:F1}%　回合 {rounds:F1}{slope}");
        }

        string report = string.Join("\n", lines);
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(counted[1] == runs, "第 1 场曲线必须全量记录");
        Assert.IsTrue(hpByBattle[1] > 0, "曲线非空（判据 A2 的判定待 O-63 裁定后在 M6Acceptance 挂载）");
    }

    public TestContext TestContext { get; set; } = null!;
}