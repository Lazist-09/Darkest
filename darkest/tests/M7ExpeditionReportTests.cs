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
        int Ambushes, int Retreats, int Deaths, int TownMorale);

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

    private static ExpeditionRun RunExpedition(long seed, ExpeditionNodesConfig nodes, TuningCamp camp)
    {
        var session = new ExpeditionSession(_ => HeadlessDriver.NewDirector(new CombatLog()), 6, firewood: 2, food: 12,
            ambushChance: 0.33);
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
                string tier = session.Food >= 12 ? "feast" : session.Food >= 6 ? "full" : session.Food >= 3 ? "half" : "starve";
                tiers.Add(session.ChooseFood(log, camp, tier));
                while (session.RespiteLeft >= 2)
                {
                    session.UseCampSkill(log, "camp_warrior_sharpen", 2, UnitId.Of("warrior"));
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
            camps, tiers, 0, session.Firewood, session.Food, ambushes, retreats, deaths, townMorale);
    }

    [TestMethod]
    public void M7_Expedition_Report_Fields_10_To_16()
    {
        const int runs = 60; // 骨架：先小样本看形状（正式跑用 300）
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        TuningCamp camp = TuningConfig.Parse(ReadData("tuning.json")).Camp;

        int completed = 0, camps = 0, ambushes = 0, retreats = 0, deaths = 0, battleTotal = 0, wins = 0;
        var tierCount = new Dictionary<string, int>();
        var townMorales = new List<int>();
        double hp1 = 0, morale1 = 0;
        int countedBattles = 0;

        for (int i = 0; i < runs; i++)
        {
            ExpeditionRun r = RunExpedition(20260909 + i, nodes, camp);
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
            $"[M7] ⑯ 回城士气：均值 {townMorales.Average():F1}　低于50 的趟数 {townMorales.Count(m => m < 50)}/{runs}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.AreEqual(runs, townMorales.Count, "每趟都应有一次回城结算（⑯）");
        Assert.IsTrue(camps > 0, "扎营应被使用（E7 判据：扎营次数 > 0）");
        Assert.IsTrue(townMorales.Any(), "⑯ 回城士气可观测");
    }

    public TestContext TestContext { get; set; } = null!;
}
