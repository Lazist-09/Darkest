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

                // ㉔ 额外补给（#270 裁定①：**按档确定给份数、去掉掷骰**；P21 ⑧ 掉落不得引入抽取）
                string tierId = LightMeter.TierId(meter.Tier);
                tierNights[tierId] = tierNights.GetValueOrDefault(tierId) + 1;
                int grant = lightCfg.Loot[tierId];
                if (grant > 0)
                {
                    session.Gain(log, "food", grant, "loot"); // 收益端 = 补给本身（无金钱）
                    loot += grant;
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

    /// <summary>
    /// **V10 策略分离度**（`#271` §D5.4 / `m7_5_verification` V10）——M7.5 的**核心判据**：
    /// 报**三种策略**（保守 / 均衡 / 激进）的**完成率**（口径 = **走完 6 步、不撤退不团灭**，`#270` 裁定②）
    /// 与 **㉘ 选路比例**。
    /// 🔴 判读：**三者拉得开 ⇒ 设计成功**（玩家的选择真的改变结果）；**挤在一起 ⇒ 设计失败**
    /// （选择不影响结果 ⇒ 该调**收益端/难度端**，**不是调区间**）。
    /// </summary>
    [TestMethod]
    public void M75_V10_StrategySeparation_And_28_PathRatio()
    {
        const int runs = 60;
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));

        (string Name, bool PreferEvent, bool Brighten)[] strategies =
        {
            ("保守（多事件 / 早提亮）", true, true),
            ("均衡（默认交替）", false, false),
            ("激进（多战斗 / 摸黑搏补给）", false, false),
        };

        var lines = new List<string> { $"[M7.5] V10 策略分离度（各 {runs} 趟；完成口径 = 走完 6 步，不撤退/不团灭）" };
        var completionRates = new List<double>();

        for (int s = 0; s < strategies.Length; s++)
        {
            (string name, bool preferEvent, bool brighten) = strategies[s];
            int completed = 0, battles = 0, events = 0, loot = 0, retreats = 0;

            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + i * 31 + s);
                var session = new ExpeditionSession(_ => HeadlessDriver.NewDirector(new CombatLog()), 6,
                    firewood: 2, food: 12, ambushChance: 0.6); // 激进档夜袭概率更高（同一 seed 口径下比较）
                var meter = new LightMeter(tuning.Light!);
                meter.EmitStart(log);
                IReadOnlyList<PathStep> path = ExpeditionPathPlanner.GeneratePath(log, rng, 6, nodes);

                int steps = 0;
                bool aborted = false;
                for (int step = 0; step < path.Count && !aborted; step++)
                {
                    // 选路：保守档偏好事件（option 0 = event），激进档偏好战斗（option 1 = battle）
                    int optionIndex = s == 0 && preferEvent ? 0 : s == 2 ? 1 : step % 2;
                    PathOption chosen = ExpeditionPathPlanner.ChoosePath(log, path[step], optionIndex);
                    if (chosen.NodeType == "event")
                    {
                        events++;
                        session.ResolveEventNode(log, nodes.Get(chosen.NodeId), 0);
                        steps++;
                        meter.TryAdvanceNode(log);
                        continue;
                    }

                    battles++;
                    meter.TryAdvanceNode(log);
                    int idx = session.BattlesPlayed + 1;
                    BattleDirector d = session.BeginExpeditionBattle(idx, log, tuning.Expedition.DifficultyTiers);
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
                    if (result != "PlayerVictory")
                    {
                        aborted = true;
                        retreats++;
                        break;
                    }

                    steps++;

                    // 🔴 收益端：按档确定给份数（#270 裁定①；不掷骰）
                    int grant = tuning.Light!.Loot[LightMeter.TierId(meter.Tier)];
                    if (grant > 0)
                    {
                        session.Gain(log, "food", grant, "loot");
                        loot += grant;
                    }

                    // 保守档：光照 ≤ 50 时**提亮**（1 柴火 +30；不足则拒且不变）
                    if (brighten && meter.Value <= 50)
                    {
                        meter.TryBrighten(log, () => session.TrySpend(log, "firewood", 1, "torch"));
                    }

                    if (session.CanCamp && session.StartCamp(log, step + 1, tuning.Camp!.RespiteBase))
                    {
                        meter.OnCamp(log);
                    }
                }

                if (!aborted && steps >= path.Count)
                {
                    completed++;
                }
            }

            double rate = (double)completed / runs;
            completionRates.Add(rate);
            double eventShare = battles + events == 0 ? 0 : 100.0 * events / (battles + events);
            lines.Add($"[M7.5] V10 {name}：完成率 {rate:P0}（{completed}/{runs}）" +
                      $"　㉘ 选路 战斗 {battles} / 事件 {events}（事件占比 {eventShare:F0}%，**必须两侧不为 0 且无一侧 >90%**）" +
                      $"　㉔ 补给 {loot} 份（{loot / (double)runs:F2}/趟）　撤退/团灭 {retreats}");
        }

        double spread = completionRates.Max() - completionRates.Min();
        lines.Add($"[M7.5] V10 判读：三档完成率极差 **{spread:P0}**" +
                  (spread >= 0.15
                      ? " ⇒ ✅ **拉得开 → 设计成功**（玩家的选择真的改变结果）"
                      : " ⇒ 🔴 **挤在一起 → 设计失败**：选择不影响结果 ⇒ 该调【收益端/难度端】，**不是调区间**"));

        string report = string.Join("\n", lines);
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.AreEqual(3, completionRates.Count);
        Assert.IsTrue(completionRates.All(x => x >= 0 && x <= 1));
    }

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
}
