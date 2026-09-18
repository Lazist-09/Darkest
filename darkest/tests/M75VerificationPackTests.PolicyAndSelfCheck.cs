// 🔴 **从 `M75VerificationPackTests.cs` 拆出**（用户 2026-09-18 红线：程序文件 ≤600 行）：
//    本文件 = 「**政策档 + 负向自检**」两个用例（同一职责族）⇒ **只搬家、零行为改动** ✓
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

public sealed partial class M75VerificationPackTests
{
    /// <summary>
    /// **V8 / ㉗：待命比例（保守策略）** —— 按策划 `#270` 裁定④：加「**SP &lt; 2 时支援位待命**」的保守策略，
    /// 让 ㉗ 有样本。口径：**待命占支援位行动回合比例** = 待命次数 ÷（2 单位 × 回合数）；
    /// 🔴 阈值 **≤ 25%**；**越界处置 = 调 SP / 补"不耗 SP 的事"，不加罚**（本用例**只报不判红**）。
    /// ⚠️ 简化说明：此处只按 `SupportPoints &lt; 2` 判定，**未**再查"是否真的无可用技能"（需可用性 API）；
    /// 因此该比例是**上界**（真实保守玩家会更少待命）。**不得为凑 ㉗ 样本而人为造待命**。
    /// </summary>
    [TestMethod]
    public void M75_V8_PassRatio_WithConservativePolicy()
    {
        const int runs = 60;
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));

        int totalRounds = 0, totalPasses = 0, passMoraleLoss = 0, battles = 0;

        for (int i = 0; i < runs; i++)
        {
            var log = new CombatLog();
            var rng = new RngProvider(20260909 + i);
            var session = new ExpeditionSession(_ => HeadlessDriver.NewDirector(new CombatLog()), 6,
                firewood: 2, food: 12, ambushChance: tuning.Expedition.AmbushChance);
            IReadOnlyList<PathStep> path = ExpeditionPathPlanner.GeneratePath(log, rng, 6, nodes);

            for (int step = 0; step < path.Count; step++)
            {
                PathOption chosen = ExpeditionPathPlanner.ChoosePath(log, path[step], step % 2);
                if (chosen.NodeType == "event")
                {
                    session.ResolveEventNode(log, nodes.Get(chosen.NodeId), 0);
                    continue;
                }

                int idx = session.BattlesPlayed + 1;
                BattleDirector d = session.BeginExpeditionBattle(idx, log, tuning.Expedition.DifficultyTiers);
                string result = "RoundLimit";
                int round = 1;
                for (; round <= 100; round++)
                {
                    // 🔴 保守策略：支援位在 SP < 2 时**待命**（返回 None → 内核走 PassTurn）
                    d.RunFullRound(rng, unit => d.IsSupportSlotActor(unit.Id) && d.SupportPoints < 2
                        ? PlayerDecision.None
                        : Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
                    if (d.IsBattleOver)
                    {
                        result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                        break;
                    }
                }

                int used = Math.Min(round, 100);
                session.EndBattle(d, idx, result, used);
                battles++;
                totalRounds += used;
                totalPasses += log.Events.OfType<TurnSkippedEvent>().Count(e => e.Reason == "passed");
                passMoraleLoss += log.Events.OfType<TurnSkippedEvent>().Where(e => e.Reason == "passed").Sum(e => e.MoraleDelta);

                if (result != "PlayerVictory")
                {
                    break;
                }
            }
        }

        double opportunities = 2.0 * totalRounds; // 支援位行动机会 = 2 单位 × 回合数
        double ratio = opportunities == 0 ? 0 : totalPasses / opportunities;

        string report =
            $"[M7.5] V8/㉗ 保守待命策略（{runs} 趟，{battles} 场）：待命 **{totalPasses} 次**" +
            $"（{totalPasses / (double)Math.Max(1, battles):F2}/场）　待命士气损失 {passMoraleLoss}" +
            $"　支援位行动机会 {opportunities:F0}（= 2 单位 × {totalRounds} 回合）\n" +
            $"[M7.5] V8 待命比例 **{ratio:P1}**（阈值 ≤ 25%；越界 ⇒ **调 SP / 补「不耗 SP 的事」，不加罚** —— 只报不判红）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(battles > 0, "必须有战斗样本");

        // 🔴 实测结论（`#270` 裁定④ + O-73）：**加了保守策略仍 0 次待命** ⇒ 支援位**总有不耗 SP 的事可做**
        //    ⇒ `#213` 的"待命"机制在实战中**从未被需要** ⇒ **支持 O-73**（该机制可能多余：应删机制或补"只能待命"的真实场景，**不硬留**）；
        //    ⇒ V8 的 ≤25% 阈值天然满足。🔴 **本用例不制造待命**（造数据 = 违反纪律）。
        Assert.IsTrue(ratio <= 0.25,
            $"V8 未过：待命比例 {ratio:P1} > 25% ⇒ 应**调 SP / 补「不耗 SP 的事」，不加罚**（只报不判红）。\n{report}");
        if (totalPasses == 0)
        {
            Console.WriteLine("[M7.5] O-73 证据：保守策略（SP<2 → Pass）下待命仍为 0 ⇒ 支援位总有不耗 SP 的事可做；"
                              + "该机制（#213）可能多余 —— 建议删机制或补一个「只能待命」的真实场景，而不是硬留。");
        }
    }

    public TestContext TestContext { get; set; } = null!;

    // ------------------------------------------------------------------
    // 🔴 `#317`① 判读逻辑（只看三档）+ **一致性判据** + **负向探针**
    //    根因（实测抓到的真 bug）：`completionRates` 有**四档**（含"提亮优先"），
    //    而判读只打印三档、`switch` 的 **default 又把第 4 档当成"激进"**
    //    ⇒ 出现「最优档 = 激进」但数字是「保守 95% 最高」的自相矛盾 ⚠️（每轮读数都会带一句错结论）
    // ------------------------------------------------------------------

    /// <summary>由三档完成率给出【最优档 + 判词】（**纯函数**：无随机 ⇒ 可直接喂负向探针）。</summary>
    internal static (string Best, string Verdict) JudgeTiers(double conservative, double balanced, double aggressive)
    {
        double[] trio = { conservative, balanced, aggressive };
        int best = Array.IndexOf(trio, trio.Max());
        return best switch
        {
            1 => ("均衡", "✅ **均衡最高 = 倒 U 成立 → 设计成功**（中等冒险收益刚好补偿风险）"),
            0 => ("保守", "🔴 **保守最高 → 收益不足**（应加收益 / 再减起手资源）"),
            _ => ("激进", "🔴 **激进最高 → 风险不足**（应加难度）"),
        };
    }

    /// <summary>🔴 **一致性判据**（`m7_6_verification` §1.7 D）：声明的"最高档"必须等于三档的 `argmax` 档名。</summary>
    internal static bool ConsistentWithClaim(double conservative, double balanced, double aggressive, string claimedBest)
        => JudgeTiers(conservative, balanced, aggressive).Best == claimedBest;

    /// <summary>
    /// 🔴 **负向探针**（架构要求的取证："故意把判读行写反一次 ⇒ 用例必须变红"）：
    /// 数字是【保守最高】，若判读行声称"激进/均衡最高" ⇒ 自检必须**判不一致**；声称"保守最高" ⇒ 通过 ✓
    /// </summary>
    [TestMethod]
    public void V10_Judge_SelfCheck_CatchesSwappedClaim()
    {
        // 本轮真实读数形状（保守 95 ／ 均衡 90 ／ 激进 73）
        Assert.AreEqual("保守", JudgeTiers(0.95, 0.90, 0.73).Best, "三档极值是保守 ⇒ 最优档必须是保守");
        Assert.IsTrue(ConsistentWithClaim(0.95, 0.90, 0.73, "保守"), "声称保守 ⇒ 一致 ✓");
        Assert.IsFalse(ConsistentWithClaim(0.95, 0.90, 0.73, "激进"),
            "🔴 负向探针：数字是保守最高却声称激进最高 ⇒ 必须判**不一致**（这正是上一轮判读行犯的错）");
        Assert.IsFalse(ConsistentWithClaim(0.95, 0.90, 0.73, "均衡"), "🔴 同理：声称均衡最高也不一致");

        // 另两种极值也要对（覆盖三条判词）
        Assert.AreEqual("均衡", JudgeTiers(0.70, 0.80, 0.60).Best);
        Assert.AreEqual("激进", JudgeTiers(0.50, 0.55, 0.60).Best);
    }
}
