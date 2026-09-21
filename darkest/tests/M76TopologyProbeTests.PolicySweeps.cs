// 🔴 从 M76TopologyProbeTests.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）：
//    策略扫描类探针（V10 带 C / O299 光照增益三臂） —— 只搬家、零行为改动（[TestClass] 只在主文件上，MSTest 仍会发现本 partial）
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

public sealed partial class M76TopologyProbeTests
{
    /// <summary>
    /// 🔴 **`#300`⑤ 诊断：报【每档】扎营次数 ／ 段数 ／ 是否触底（=0）**。
    /// ⚠️ **先自查**：我此前拓扑 V10 驱动**没有实现扎营**（那可能正是"必然触底"的原因）⇒ 本用例**补上扎营**
    /// （照既有口径：HP/光照低时**有柴火就扎营**，扎营回满 100），再看触底是否仍发生。
    /// 判据二分（架构写死）：**扎营 ≥1 却仍触底 ⇒ 结构问题（才动光照标定）**；**扎营 = 0 或变少 ⇒ 拓扑挤掉扎营机会 ⇒ 修拓扑**。
    /// </summary>
    [TestMethod]
    public void O300_Diagnostic_CampSegmentsBottomOut()
    {
        TuningConfig tuning = Tuning();
        ExpeditionNodesConfig nodesG = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionMapConfig cfg = MapCfg();

        (string Name, bool Branches, bool Brighten)[] policies =
        {
            ("保守·主干提亮", false, true),
            ("均衡·主干不提亮", false, false),
            ("激进·绕支路", true, false),
        };

        var lines = new List<string>();
        foreach ((string name, bool br, bool bright) in policies)
        {
            const int runs = 20;
            int campSum = 0, segSum = 0, bottomSum = 0, minSum = 0, retreats = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 47));
                var bag = new Inventory(tuning.Inventory!);
                bag.ConfigureRecommended(out _);
                bag.LockForRun();
                var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
                    tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
                    tuning.Expedition.AmbushChance);
                var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                    new Scouting(tuning.Scouting!, tuning.Light!), nodesG, tuning, log, rng);
                ExpeditionMap map = flow.BeginTopology(cfg);
                var visited = new HashSet<int> { map.StartId };
                bool aborted = false, bottomed = false;
                int camps = 0, minLight = 100;

                int guard = 0;
                while (!flow.ReachedGoal && !aborted && guard++ < 60)
                {
                    var all = map.Edges
                        .Where(e => e.From == flow.CurrentRoomId || e.To == flow.CurrentRoomId)
                        .Select(e => e.From == flow.CurrentRoomId ? e.To : e.From)
                        .Distinct()
                        .Select(id => map.Rooms.First(r => r.Id == id))
                        .ToList();
                    var options = all.Where(r => !visited.Contains(r.Id)).ToList();
                    bool back = options.Count == 0;
                    if (back)
                    {
                        options = all;
                    }

                    if (options.Count == 0)
                    {
                        break;
                    }

                    MapRoom next = br && options.Any(o => o.IsBranch) && !back
                        ? options.First(o => o.IsBranch)
                        : options.First(o => o.Id == map.GoalId || !o.IsBranch);

                    if (!flow.StepTo(next.Id).Moved)
                    {
                        break;
                    }

                    visited.Add(next.Id);
                    if (flow.Meter.Value < minLight)
                    {
                        minLight = flow.Meter.Value;
                    }

                    if (flow.Meter.Value <= 0)
                    {
                        bottomed = true;
                    }

                    // 🔴 之前缺失的一环：**扎营**（有柴火且光照低就扎；回满 100）
                    if (flow.Meter.Value <= 40 && session.CanCamp
                        && session.StartCamp(log, flow.StepsDone, tuning.Camp!.RespiteBase))
                    {
                        camps++;
                        flow.Meter.OnCamp(log);
                        session.ChooseFood(log, tuning.Camp, "half");
                        session.EndCamp(log);
                    }

                    if (bright && flow.Meter.Value <= 50)
                    {
                        flow.Meter.TryBrighten(log, () => true);
                    }

                    if (next.Type == "battle")
                    {
                        int idx = session.BattlesPlayed + 1;
                        BattleDirector d = session.BeginExpeditionBattle(idx, log, tuning.Expedition.DifficultyTiers);
                        string result = "RoundLimit";
                        int round = 1;
                        for (; round <= 100; round++)
                        {
                            d.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                                MonteCarlo.PolicyKind.SemiRandom, unit, d, rng));
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
                    }
                    else
                    {
                        session.ResolveEventNode(log, nodesG.Nodes.First(n => n.Type == "event"), 0);
                    }
                }

                campSum += camps;
                segSum += flow.StepsDone;
                bottomSum += bottomed ? 1 : 0;
                minSum += minLight;
            }

            lines.Add($"[M7.6] #300 诊断 {name}（{runs} 趟）：**扎营 {campSum / (double)runs:F2}/趟**　段数 {segSum / (double)runs:F1}" +
                      $"　**触底（光照=0）{bottomSum / (double)runs:P0}**　最暗 {minSum / (double)runs:F0}　撤退 {retreats}");
        }

        lines.Add("[M7.6] #300 判据二分（架构写死）：**扎营 ≥1 却仍触底 ⇒ 结构问题（才动光照标定）**；" +
                  "**扎营 = 0 或变少 ⇒ 拓扑挤掉扎营机会 ⇒ 修拓扑，不必动 −30**");
        Console.WriteLine(string.Join("\n", lines));
        TestContext.WriteLine(string.Join("\n", lines));
        Assert.AreEqual(4, lines.Count);
    }

    /// <summary>
    /// 🔴 **`#300` (g1)：带扎营重跑 `(c) 关 / 开 × 三档`** —— 用**修正后的口径**（探针已补扎营）。
    /// ⚠️ 此前两轮读数按"探针无扎营"归档、**不用于裁定**（红线 17 ⑧）。
    /// 纪律：`battle_goal` 仍 3 · 判据未改 · 发版数据未动。
    /// </summary>
    [TestMethod]
    public void O300g1_WithCamp_C_OnOff_ThreePolicies()
    {
        TuningConfig tuning = Tuning();
        ExpeditionNodesConfig nodesG = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionMapConfig shipped = MapCfg();
        ExpeditionMapConfig withC = shipped with
        {
            Map = shipped.Map with { BranchSpecialWeight = 100, BranchSpecialKind = "free_light", BranchSpecialLightGain = 20 },
        };

        Func<bool, bool, ExpeditionMapConfig, string> run = (br, bright, cfg) =>
        {
            const int runs = 25;
            int completed = 0, campsSum = 0, bottomSum = 0, retreats = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 53));
                var bag = new Inventory(tuning.Inventory!);
                bag.ConfigureRecommended(out _);
                bag.LockForRun();
                var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
                    tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
                    tuning.Expedition.AmbushChance);
                var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                    new Scouting(tuning.Scouting!, tuning.Light!), nodesG, tuning, log, rng);
                ExpeditionMap map = flow.BeginTopology(cfg);
                var visited = new HashSet<int> { map.StartId };
                bool aborted = false, bottomed = false;
                int camps = 0, localWins = 0;

                int guard = 0;
                while (!flow.ReachedGoal && !aborted && guard++ < 60)
                {
                    var all = map.Edges
                        .Where(e => e.From == flow.CurrentRoomId || e.To == flow.CurrentRoomId)
                        .Select(e => e.From == flow.CurrentRoomId ? e.To : e.From)
                        .Distinct()
                        .Select(id => map.Rooms.First(r => r.Id == id))
                        .ToList();
                    var options = all.Where(r => !visited.Contains(r.Id)).ToList();
                    bool back = options.Count == 0;
                    if (back)
                    {
                        options = all;
                    }

                    if (options.Count == 0)
                    {
                        break;
                    }

                    MapRoom next = br && options.Any(o => o.IsBranch) && !back
                        ? options.First(o => o.IsBranch)
                        : options.First(o => o.Id == map.GoalId || !o.IsBranch);

                    if (!flow.StepTo(next.Id).Moved)
                    {
                        break;
                    }

                    visited.Add(next.Id);
                    if (flow.Meter.Value <= 0)
                    {
                        bottomed = true;
                    }

                    if (next.Type == "free_light")
                    {
                        flow.Meter.TryAdvanceBy(log, +20, "free_light");
                    }

                    // 🔴 扎营（本口径的关键：有柴火且光照低就扎，回满 100）
                    if (flow.Meter.Value <= 40 && session.CanCamp
                        && session.StartCamp(log, flow.StepsDone, tuning.Camp!.RespiteBase))
                    {
                        camps++;
                        flow.Meter.OnCamp(log);
                        session.ChooseFood(log, tuning.Camp, "half");
                        session.EndCamp(log);
                    }

                    if (bright && flow.Meter.Value <= 50)
                    {
                        flow.Meter.TryBrighten(log, () => true);
                    }

                    if (next.Type == "battle")
                    {
                        int idx = session.BattlesPlayed + 1;
                        BattleDirector d = session.BeginExpeditionBattle(idx, log, tuning.Expedition.DifficultyTiers);
                        string result = "RoundLimit";
                        int round = 1;
                        for (; round <= 100; round++)
                        {
                            d.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                                MonteCarlo.PolicyKind.SemiRandom, unit, d, rng));
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

                        localWins++;
                    }
                    else if (next.Type != "free_light")
                    {
                        session.ResolveEventNode(log, nodesG.Nodes.First(n => n.Type == "event"), 0);
                    }
                }

                campsSum += camps;
                bottomSum += bottomed ? 1 : 0;
                if (!aborted && flow.ReachedGoal && localWins >= tuning.Expedition.BattleGoal)
                {
                    completed++;
                }
            }

            return $"完成 {completed / (double)runs:P0}（扎营 {campsSum / (double)runs:F2}／触底 {bottomSum / (double)runs:P0}／撤退 {retreats}）";
        };

        var lines = new List<string>
        {
            "[M7.6] #300(g1) (c) 关（带扎营）：保守 " + run(false, true, shipped) + "　均衡 " + run(false, false, shipped) + "　激进 " + run(true, false, shipped),
            "[M7.6] #300(g1) (c) 开（带扎营）：保守 " + run(false, true, withC) + "　均衡 " + run(false, false, withC) + "　激进 " + run(true, false, withC),
            "[M7.6] #300(g1) 判读：倒 U = 均衡最高；若仍「激进最高」⇒ 回报；若「保守最高」⇒ 收益端仍不足（按 #274 规则）",
        };
        Console.WriteLine(string.Join("\n", lines));
        TestContext.WriteLine(string.Join("\n", lines));
        Assert.AreEqual(3, lines.Count);
    }

    public TestContext TestContext { get; set; } = null!;
}
//    【依赖主类私有成员】(partial 使封装在文件级失效 => 必须声明)：MapCfg x2 · ReadData x2 · Tuning x2
