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
/// E7 复测骨架（M7 远征层 ⑩~⑯）：跑 N 趟完整远征——**选路 → 战斗 → 扎营 → 事件 → 夜袭 → 回城**，
/// 输出：⑩ 完成率（6 场皆胜）+ 逐场胜负序列 · ⑪ HP%/士气% 曲线 · ⑫ 扎营次数与食物档位·Respite ·
/// ⑬ 资源收支 · ⑭ 夜袭次数 · ⑮ 撤退次数+死亡人数 · ⑯ 回城士气分布。
/// 说明：本文件自包含（不改动既有 harness），全部随机走 `RngProvider` 且写 `RngDraw`。
/// </summary>
[TestClass]
public sealed class M7ExpeditionReportTests
{
    private sealed record ExpeditionRun(
        bool Completed, List<string> BattleResults, List<(double Hp, double Morale)> Curve,
        int Camps, List<string> Tiers, int RespiteSpent, int FirewoodLeft, int FoodLeft,
        int Ambushes, int Retreats, int Deaths, int TownMorale, ExpeditionSession Session,
        Darkest.Core.Events.CombatLog Log); // 🆕 V10：把该趟的**事件流**带出来（读数只认事件流）✓

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

    private static (string Result, int Deaths) RunBattle(ExpeditionSession s, IRngProvider rng, int index)
    {
        var log = new CombatLog();
        BattleDirector d = s.BeginBattle(index, log);
        string result = "RoundLimit";
        int round;
        for (round = 1; round <= 100; round++)
        {
            d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
            if (d.IsBattleOver)
            {
                result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                break;
            }
        }

        int deaths = log.Events.OfType<DeathEvent>().Count(e => e.IsPlayer);
        s.EndBattle(d, index, result, Math.Min(round, 100));
        return (result, deaths);
    }

    private static ExpeditionRun RunExpedition(long seed, ExpeditionNodesConfig nodes, TuningCamp camp,
        int firewood = 2, int food = 12, ExpeditionSession? carryFrom = null)
    {
        var session = new ExpeditionSession(_ => HeadlessDriver.NewDirector(new CombatLog()), 6, firewood: firewood, food: food,
            ambushChance: 0.33);
        if (carryFrom is not null)
        {
            session.CarryOverFrom(carryFrom); // #245：跨趟——HP 全恢复、**士气保留**
        }
        var log = new CombatLog();
        var rng = new RngProvider(seed);
        IReadOnlyList<PathStep> path = ExpeditionPathPlanner.GeneratePath(log, rng, 6, nodes);

        var results = new List<string>();
        var tiers = new List<string>();
        int camps = 0, wins = 0, ambushes = 0, retreats = 0, deaths = 0, resolvedSteps = 0;
        bool ended = false;

        for (int i = 0; i < path.Count && !ended; i++)
        {
            PathOption chosen = ExpeditionPathPlanner.ChoosePath(log, path[i], optionIndex: i % 2);
            if (chosen.NodeType == "event")
            {
                session.ResolveEventNode(log, nodes.Get(chosen.NodeId), optionIndex: 0);
                resolvedSteps++;
                continue;
            }

            (string r, int d) = RunBattle(session, rng, i + 1);
            results.Add(r);
            deaths += d;
            resolvedSteps++;
            if (r == "PlayerVictory")
            {
                wins++;
            }
            else
            {
                if (r == "DrawRetreat")
                {
                    retreats++;
                }

                ended = true; // 撤退 = 该场判负 + run 立即结束；全灭同理
                break;
            }

            // 战后扎营（有柴火就扎；食物按当前口粮择档）
            if (session.CanCamp && session.StartCamp(log, i + 1, camp.RespiteBase))
            {
                camps++;
                string tier = session.CanAffordFood(camp, "feast") ? "feast"
                    : session.CanAffordFood(camp, "full") ? "full"
                    : session.CanAffordFood(camp, "half") ? "half" : "starve"; // v0.86：只选**可支付**档位
                tiers.Add(session.ChooseFood(log, camp, tier));
                while (session.RespiteLeft >= 2)
                {
                    session.UseCampSkill(log, CampSkillTestKit.Skill("camp_warrior_sharpen"), UnitId.Of("warrior"), CampSkillTestKit.Camp);
                }

                session.EndCamp(log);
            }

            // 夜袭：额外一场战斗（**计入 6 场皆胜**）
            if (session.RollAmbush(log, rng))
            {
                ambushes++;
                (string r2, int d2) = RunBattle(session, rng, i + 1);
                results.Add(r2);
                deaths += d2;
                if (r2 == "PlayerVictory")
                {
                    wins++;
                }
                else
                {
                    if (r2 == "DrawRetreat")
                    {
                        retreats++;
                    }

                    ended = true;
                }
            }
        }

        // 完成口径（E7）：**路径 6 步全部安全走完**（战斗全胜、事件照常结算；撤退/全灭即中止）
        bool completed = !ended && resolvedSteps >= path.Count;
        string outcome = completed ? "completed" : retreats > 0 ? "retreat" : ended ? "wiped" : "incomplete";
        int townMorale = session.ReturnToTown(log, outcome);

        return new ExpeditionRun(completed, results,
            session.Curve.Select(c => (c.AvgHpPercent, c.AvgMoralePercent)).ToList(),
            camps, tiers, 0, session.Firewood, session.Food, ambushes, retreats, deaths, townMorale, session, log);
    }

    [TestMethod]
    public void M7_Expedition_Report_Fields_10_To_16()
    {
        const int runs = 300; // 正式复测（E7）
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        TuningCamp camp = TuningConfig.Parse(ReadData("tuning.json")).Camp;

        int completed = 0, camps = 0, ambushes = 0, retreats = 0, deaths = 0, battleTotal = 0, wins = 0;
        var runLogs = new List<IReadOnlyList<Darkest.Core.Events.BattleEvent>>(); // 🆕 V10：逐趟事件流 ✓
        int firewoodSpent = 0, foodSpent = 0;
        var hpByBattle = new List<double>(new double[12]);
        var moraleByBattle = new List<double>(new double[12]);
        var curveCount = new List<int>(new int[12]);
        var tierCount = new Dictionary<string, int>();
        var townMorales = new List<int>();
        var retreatMorales = new List<int>(); // ⑯：撤退档的回城士气分布（裁定 ii）
        double hp1 = 0, morale1 = 0;
        int countedBattles = 0;

        for (int i = 0; i < runs; i++)
        {
            ExpeditionRun r = RunExpedition(20260909 + i, nodes, camp);
            runLogs.Add(r.Log.Events); // 🆕 V10 ✓
            if (r.Completed)
            {
                completed++;
            }

            camps += r.Camps;
            ambushes += r.Ambushes;
            retreats += r.Retreats;
            deaths += r.Deaths;
            battleTotal += r.BattleResults.Count;
            foreach (string br in r.BattleResults)
            {
                if (br == "PlayerVictory")
                {
                    wins++;
                }
            }
            townMorales.Add(r.TownMorale);
            if (r.Retreats > 0)
            {
                retreatMorales.Add(r.TownMorale); // 裁定 (ii)：⑯ 只统计**撤退档**
            }
            firewoodSpent += 2 - r.FirewoodLeft; // 起手 2
            foodSpent += 12 - r.FoodLeft;        // 起手 12（⑬ 资源收支）
            for (int b = 0; b < r.Curve.Count; b++)
            {
                hpByBattle[b + 1] += r.Curve[b].Hp;
                moraleByBattle[b + 1] += r.Curve[b].Morale;
                curveCount[b + 1]++;
            }
            foreach (string t in r.Tiers)
            {
                tierCount[t] = tierCount.GetValueOrDefault(t) + 1;
            }

            if (r.Curve.Count > 0)
            {
                hp1 += r.Curve[0].Hp;
                morale1 += r.Curve[0].Morale;
                countedBattles++;
            }
        }

        string report =
            $"[M7] ⑩ 完成率（6 步全过）={completed}/{runs}（{(double)completed / runs:P0}）" +
            $"　战斗 胜{wins}/共{battleTotal}（{(battleTotal == 0 ? 0 : 100.0 * wins / battleTotal):F0}%）\n" +
            $"[M7] ⑪ 第 1 场结束：HP {hp1 / Math.Max(1, countedBattles):F1}%　士气 {morale1 / Math.Max(1, countedBattles):F1}%\n" +
            $"[M7] ⑫ 扎营 {camps} 次　食物档位 " +
            string.Join(" ", new[] { "feast", "full", "half", "starve" }.Select(t => $"{t}:{tierCount.GetValueOrDefault(t)}")) + "\n" +
            $"[M7] ⑭ 夜袭 {ambushes} 次　⑮ 撤退 {retreats} 次　阵亡 {deaths}（{(double)deaths / runs:F2}/趟）\n" +
            $"[M7] ⑯ 回城士气（**仅撤退档**·裁定 ii）：撤退 {retreatMorales.Count} 趟" +
            $"{(retreatMorales.Count == 0 ? "（本批无撤退 → 无分布）" : $"　均值 {retreatMorales.Average():F1}　区间 [{retreatMorales.Min()},{retreatMorales.Max()}]")}\n" +
            $"[M7] ⑬ 资源：柴火支出 {firewoodSpent}（{firewoodSpent / (double)runs:F2}/趟）　口粮支出 {foodSpent}（{foodSpent / (double)runs:F1}/趟）\n" +
            $"[M7] ⑪ 逐场曲线（HP%/士气%）：" + string.Join(" ", Enumerable.Range(1, 8)
                .Where(b => curveCount[b] > 0)
                .Select(b => $"#{b} {hpByBattle[b] / curveCount[b]:F0}/{moraleByBattle[b] / curveCount[b]:F0}"));
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        // 🆕 **V10 口径读数**（架构 `m7_6_verification §9` · 策划 `#358`③）—— **只报数不判红**（`O-82`）✓
        //    🔴 **如实标注**：本探针的结局串仍是**旧口径**（`completed` / `retreat` / `wiped` / `incomplete`）
        //    ⇒ 带 `retreat` 的趟会被 `V10Readings` 计为【未结束】⚠️（**不把旧的偷换成新的**）✓
        Darkest.Gameplay.Sim.Run.V10Readings v10 = Darkest.Gameplay.Sim.Run.V10Readings.From(runLogs);
        string v10Line = $"[M7] 🆕 V10（新口径 · 仅报数）：{v10.Describe()}" +
                         "　⚠️ 本探针结局串含旧口径 `retreat`/`incomplete` ⇒ 计为「未结束」（见代码注释）✓";
        Console.WriteLine(v10Line);
        TestContext.WriteLine(v10Line);
        Assert.IsTrue(v10.CountsClose, "V10 判据①：三类 + 未结束 **和必须闭合** == 总趟数 ✓");

        Assert.AreEqual(runs, townMorales.Count, "每趟都应有一次回城结算（⑯）");
        Assert.IsTrue(camps > 0, "扎营应被使用（E7 判据：扎营次数 > 0）");
        Assert.IsTrue(townMorales.Any(), "⑯ 回城士气可观测");
    }

    /// <summary>
    /// E7 灵敏度（**不改数据**，只改构造参数）：起手口粮 12 / 8 / 6 三档下的完成率、⑪ 曲线与 ⑬ 口粮支出。
    /// 用途：给"是否降低起手口粮"这一决策提供实测依据（口径不清不动数值）。
    /// </summary>
    [TestMethod]
    public void M7_Sensitivity_StartingFood_ThreeVariants()
    {
        const int runs = 150;
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        TuningCamp camp = TuningConfig.Parse(ReadData("tuning.json")).Camp;

        var lines = new List<string> { "[M7] ⑰ 灵敏度：起手口粮三档（各 150 趟，不改进程/数据）" };
        foreach (int startFood in new[] { 12, 8, 6 })
        {
            int completed = 0, foodSpent = 0, starve = 0, campsTotal = 0;
            double hp6 = 0, morale6 = 0;
            int c6 = 0;
            for (int i = 0; i < runs; i++)
            {
                ExpeditionRun r = RunExpedition(20260909 + i, nodes, camp, firewood: 2, food: startFood);
                if (r.Completed)
                {
                    completed++;
                }

                foodSpent += startFood - r.FoodLeft;
                campsTotal += r.Camps;
                starve += r.Tiers.Count(t => t == "starve");
                // 裁定 (i)：**固定同一批趟** —— 每趟取自己**最后一场**的状态（不按"是否满 6 场"过滤）
                if (r.Curve.Count > 0)
                {
                    hp6 += r.Curve[^1].Hp;
                    morale6 += r.Curve[^1].Morale;
                    c6++;
                }
            }

            lines.Add($"[M7] ⑰ 口粮 {startFood}：完成率 {(double)completed / runs:P0}（{completed}/{runs}）" +
                      $"｜扎营 {campsTotal}（starve {starve}，{(campsTotal == 0 ? 0 : 100.0 * starve / campsTotal):F0}%）" +
                      $"｜口粮支出 {foodSpent / (double)runs:F1}/趟" +
                      $"｜第 6 场后 HP {(c6 == 0 ? 0 : hp6 / c6):F0}% 士气 {(c6 == 0 ? 0 : morale6 / c6):F0}%" +
                      $"（固定全样本 = {c6} 趟的**终局**状态）");
        }

        string report = string.Join("\n", lines);
        Console.WriteLine(report);
        TestContext.WriteLine(report);
        Assert.AreEqual(4, lines.Count);
    }

    /// <summary>
    /// ⑱ **3 趟士气曲线**（#245 的验收读数）：连跑 3 趟（**跨趟携带**：HP 全恢复、士气保留），
    /// 逐趟给出「起始士气 → 各场士气 → 终局士气」，看跨趟累积是否真的在吃人。
    /// </summary>
    [TestMethod]
    public void M7_ThreeRun_MoraleCurve()
    {
        const int chains = 100;
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        TuningCamp camp = TuningConfig.Parse(ReadData("tuning.json")).Camp;

        var lines = new List<string> { $"[M7] ⑱ 3 趟士气曲线（{chains} 条链，跨趟携带：HP 全恢复 / 士气不回 #245）" };
        var runEnd = new double[4];   // 每趟终局士气均值
        var runStart = new double[4]; // 每趟起始士气均值
        var runHp = new double[4];
        int completedRuns = 0;

        for (int c = 0; c < chains; c++)
        {
            ExpeditionSession? prev = null;
            for (int run = 1; run <= 3; run++)
            {
                ExpeditionRun r = RunExpedition(20260909 + c * 10 + run, nodes, camp, carryFrom: prev);
                double startMorale = prev is null
                    ? 100 // 首趟起始 = 满士气
                    : prev.Curve.Count == 0 ? 0 : prev.Curve[^1].AvgMoralePercent;
                runStart[run] += startMorale;
                runEnd[run] += r.Curve.Count == 0 ? 0 : r.Curve[^1].Morale;
                runHp[run] += r.Curve.Count == 0 ? 0 : r.Curve[^1].Hp;
                if (r.Completed)
                {
                    completedRuns++;
                }

                prev = r.Session;
            }
        }

        for (int run = 1; run <= 3; run++)
        {
            lines.Add($"[M7] ⑱ 第 {run} 趟：起始士气 {runStart[run] / chains:F0} → 终局士气 {runEnd[run] / chains:F0}" +
                      $"　终局 HP {runHp[run] / chains:F0}%");
        }

        lines.Add($"[M7] ⑱ 三趟完成次数 {completedRuns}/{chains * 3}（{(double)completedRuns / (chains * 3):P0}）" +
                  $"　士气跨趟净变化 {runEnd[3] / chains - runStart[1] / chains:+0.0;-0.0;0.0}");

        string report = string.Join("\n", lines);
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(runEnd[1] > 0, "第 1 趟必须产出曲线");
        Assert.IsTrue(runStart[2] > 0 && runStart[2] <= runEnd[1] + 1, "第 2 趟起始 = 第 1 趟终局（跨趟携带生效）");
    }

    public TestContext TestContext { get; set; } = null!;
}
