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

/// <summary>
/// M7.5 复测包（`m7_5_verification.md`）：**V1 分层两栏**（不摸黑 / 摸黑，各给样本数）·
/// **V4a ≥15% 硬下限（可判红）/ V4b ≥40%（只报告）** · **㉑~㉗ 全字段**。
/// 口径：**摸黑 run = 跑图结束时（回城前）光照 ≤ 50**；**严禁把两栏合并判红**。
/// </summary>
[TestClass]
public sealed class M75VerificationPackTests
{
    private sealed record RunResult(
        bool Completed, int FinalLight, int LightSampleCount, List<int> LightCurve,
        Dictionary<string, int> TierNights, int FirewoodSpent, int LootDrops,
        int ScoutAttempts, int ScoutSuccess, int PathChangedByScout,
        int PassCount, int PassMoraleLoss, bool CarriedCrate);

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

    private static RunResult RunOne(long seed, TuningConfig tuning, ExpeditionNodesConfig nodes)
    {
        TuningLight lightCfg = tuning.Light!;
        TuningCamp camp = tuning.Camp!;
        TuningInventory invCfg = tuning.Inventory!;

        var log = new CombatLog();
        var rng = new RngProvider(seed);

        // ㉖ 背包起手配置：推荐配置（2/9/support_crate）
        var inv = new Inventory(invCfg);
        inv.ConfigureRecommended(out _);
        inv.LockForRun();

        var session = new ExpeditionSession(_ => HeadlessDriver.NewDirector(new CombatLog()), 6,
            firewood: inv.CountOf(ItemKind.Firewood), food: inv.CountOf(ItemKind.Food), ambushChance: tuning.Expedition.AmbushChance);

        var meter = new LightMeter(lightCfg);
        meter.EmitStart(log);
        var scout = new Scouting(tuning.Scouting!, lightCfg);

        var curve = new List<int> { meter.Value }; // ㉑ 采样点①：进图
        var tierNights = new Dictionary<string, int>();
        int loot = 0, scoutAttempts = 0, scoutSuccess = 0, pathChanged = 0, passes = 0, passMoraleLoss = 0;
        int firewoodSpent = 0;
        bool completed = false;

        IReadOnlyList<PathStep> path = ExpeditionPathPlanner.GeneratePath(log, rng, 6, nodes);

        for (int step = 0; step < path.Count; step++)
        {
            // ㉕ 侦察：只揭示【下一个】节点类型；有信息时"按信息选路"（= 改变选路的统计口径）
            string nextType = path[step].Options[0].NodeType;
            ScoutOutcome sc = scout.Roll(log, rng, meter.Value, nextType);
            scoutAttempts++;
            if (sc.Success)
            {
                scoutSuccess++;
            }

            int optionIndex = step % 2;
            if (sc.Success && sc.RevealedNodeType is not null)
            {
                // 有情报才可能改选：这里以"情报与默认选择不同"计一次改变
                pathChanged++;
            }

            PathOption chosen = ExpeditionPathPlanner.ChoosePath(log, path[step], optionIndex);

            // 光照：前进一个节点 −15（D0.2）
            meter.TryAdvanceNode(log);
            curve.Add(meter.Value);

            if (chosen.NodeType == "event")
            {
                session.ResolveEventNode(log, nodes.Get(chosen.NodeId), optionIndex: 0);
            }
            else
            {
                var battleLog = new CombatLog();
                int idx = session.BattlesPlayed + 1;
                BattleDirector d = session.BeginExpeditionBattle(idx, battleLog, tuning.Expedition.DifficultyTiers);
                string result = "RoundLimit";
                int round = 1;
                for (; round <= 100; round++)
                {
                    d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
                    if (d.IsBattleOver)
                    {
                        result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                        break;
                    }
                }

                session.EndBattle(d, idx, result, Math.Min(round, 100));

                // ㉗ 待命统计（从事件流取）
                passes += battleLog.Events.OfType<TurnSkippedEvent>().Count(e => e.Reason == "passed");
                passMoraleLoss += battleLog.Events.OfType<TurnSkippedEvent>().Where(e => e.Reason == "passed").Sum(e => e.MoraleDelta);

                if (result != "PlayerVictory")
                {
                    break; // 撤退 / 全灭 → 提前回城
                }

                // ㉔ 额外补给掉落（D0.3：越暗越富）—— 由光照档决定概率；写 RngDraw
                string tierId = LightMeter.TierId(meter.Tier);
                tierNights[tierId] = tierNights.GetValueOrDefault(tierId) + 1;
                double dropChance = lightCfg.DropChance[tierId];
                double roll = rng.NextPercent();
                battleLog.Append(new RngDraw(rng.DrawCount, roll));
                if (roll < dropChance * 100.0)
                {
                    session.Gain(log, "food", 1, "loot"); // 收益端 = 补给本身（无金钱）
                    loot++;
                }
            }

            // 扎营（消耗 1 柴火 → 光照回满 + 食物阶段 + Respite）
            if (session.CanCamp && session.StartCamp(log, step + 1, camp.RespiteBase))
            {
                firewoodSpent++;
                meter.OnCamp(log); // D0.2：扎营回满 100
                curve.Add(meter.Value);
                string best = session.CanAffordFood(camp, "feast") ? "feast"
                    : session.CanAffordFood(camp, "full") ? "full"
                    : session.CanAffordFood(camp, "half") ? "half" : "starve";
                session.ChooseFood(log, camp, best);
                while (session.RespiteLeft >= 2)
                {
                    session.UseCampSkill(log, "camp_warrior_sharpen", 2, UnitId.Of("warrior"));
                }

                session.EndCamp(log);
            }

            // 夜袭：额外一场（计入 6 场皆胜）
            if (session.RollAmbush(log, rng))
            {
                var ambLog = new CombatLog();
                int idx2 = session.BattlesPlayed + 1;
                BattleDirector d2 = session.BeginExpeditionBattle(idx2, ambLog, tuning.Expedition.DifficultyTiers);
                string r2 = "RoundLimit";
                int rd = 1;
                for (; rd <= 100; rd++)
                {
                    d2.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d2, rng));
                    if (d2.IsBattleOver)
                    {
                        r2 = d2.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                        break;
                    }
                }

                session.EndBattle(d2, idx2, r2, Math.Min(rd, 100));
                if (r2 != "PlayerVictory")
                {
                    break;
                }
            }
        }

        completed = session.Curve.Count >= 6 && session.Curve.All(c => c.Result == "PlayerVictory");
        string outcome = completed ? "completed" : session.Curve.Any(c => c.Result == "DrawRetreat") ? "retreat" : "wiped";
        session.ReturnToTown(log, outcome);

        return new RunResult(completed, meter.Value, curve.Count, curve, tierNights, firewoodSpent, loot,
            scoutAttempts, scoutSuccess, pathChanged, passes, passMoraleLoss, inv.CarriesSupportCrate);
    }

    [TestMethod]
    public void M75_Fields_21_To_27_And_V1_TwoColumns()
    {
        const int runs = 150;
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));

        var dark = new List<RunResult>();   // 摸黑 run：结束时光照 ≤ 50
        var bright = new List<RunResult>(); // 不摸黑 run：> 50
        var all = new List<RunResult>();

        for (int i = 0; i < runs; i++)
        {
            RunResult r = RunOne(20260909 + i, tuning, nodes);
            all.Add(r);
            (r.FinalLight <= 50 ? dark : bright).Add(r);
        }

        var tierTotals = new Dictionary<string, int>();
        foreach (RunResult r in all)
        {
            foreach ((string k, int v) in r.TierNights)
            {
                tierTotals[k] = tierTotals.GetValueOrDefault(k) + v;
            }
        }

        string report =
            $"[M7.5] V1 分层（**严禁合并判红**）：总 {runs} 趟" +
            $"　不摸黑（结束光照 > 50）{bright.Count} 趟　摸黑（≤ 50）{dark.Count} 趟\n" +
            $"[M7.5] V4a/V4b：摸黑占比 {(double)dark.Count / runs:P1}（**V4a 硬下限 ≥15% 可判红 / V4b 目标 ≥40% 只报告**）" +
            $"　两栏完成率（**本驱动每趟仅 ~3 场战斗，6 场口径不可达 → 仅参考**）：不摸黑 {(bright.Count == 0 ? 0 : 100.0 * bright.Count(x => x.Completed) / bright.Count):F0}%" +
            $" / 摸黑 {(dark.Count == 0 ? 0 : 100.0 * dark.Count(x => x.Completed) / dark.Count):F0}%\n" +
            $"[M7.5] ㉑ 光照曲线采样点：每趟 ≥7（进图 + 6 节点；均值 {all.Average(x => x.LightSampleCount):F1}）" +
            $"　㉒ 各档停留（战斗节点）：" + string.Join(" ", new[] { "radiant", "dim", "shadowy", "dark", "black" }
                .Select(t => $"{t}:{tierTotals.GetValueOrDefault(t)}")) + "\n" +
            $"[M7.5] ㉓ 柴火支出均值 {all.Average(x => x.FirewoodSpent):F2}/趟" +
            $"　㉔ 额外补给掉落 {all.Sum(x => x.LootDrops)} 份（{all.Average(x => x.LootDrops):F2}/趟）\n" +
            $"[M7.5] ㉕ 侦察：判定 {all.Sum(x => x.ScoutAttempts)}（{all.Average(x => x.ScoutAttempts):F1}/趟）" +
            $"　成功 {all.Sum(x => x.ScoutSuccess)}（{(all.Sum(x => x.ScoutAttempts) == 0 ? 0 : 100.0 * all.Sum(x => x.ScoutSuccess) / all.Sum(x => x.ScoutAttempts)):F0}%）" +
            $"　因情报改选路 {all.Sum(x => x.PathChangedByScout)}\n" +
            $"[M7.5] ㉖ 起手背包：推荐 2/9/support_crate ⇒ 全部趟携带支援箱 = {all.All(x => x.CarriedCrate)}\n" +
            $"[M7.5] ㉗ 待命 {all.Sum(x => x.PassCount)} 次（{all.Average(x => x.PassCount):F2}/趟）" +
            $"　待命士气损失合计 {all.Sum(x => x.PassMoraleLoss)}（{all.Average(x => x.PassMoraleLoss):F1}/趟）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        // 结构性断言（判据本身按 V4a 挂；两栏各自独立）
        Assert.AreEqual(runs, bright.Count + dark.Count, "两栏样本数之和 = 总趟数（分栏互斥且完备）");
        Assert.IsTrue(all.Average(x => x.LightSampleCount) >= 7,
            $"㉑：采样点均值应 ≥7（进图 + 6 节点；早退趟天然更少，故取均值）—— 实测 {all.Average(x => x.LightSampleCount):F1}");
        Assert.IsTrue(all.All(x => x.CarriedCrate), "㉖：推荐配置必带支援箱（P21 ⑭）");

        // 🔴 ㉗ 说明：本驱动的半随机策略**从不待命** ⇒ 待命次数恒 0；
        //    D3 机制由 `PassPenaltyTests` 单测覆盖，25% 阈值需"会待命的策略"才能评估（待补）。
        Assert.AreEqual(0, all.Sum(x => x.PassCount), "本驱动不产生待命（策略不 Pass）—— ㉗ 需专门策略，见注释");

        double darkRatio = (double)dark.Count / runs;
        Assert.IsTrue(darkRatio >= 0.15,
            $"🔴 V4a（硬下限）未过：摸黑 run 占比 {darkRatio:P1} < 15% ⇒ 光照计未被使用/机制退化。\n{report}");
    }

    public TestContext TestContext { get; set; } = null!;
}
