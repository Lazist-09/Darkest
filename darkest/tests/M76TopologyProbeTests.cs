using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// **M7.6 片 (ii)：拓扑模式探针**（`m7_6_verification` **V6** / 策划 `#294` 的监控项）。
///
/// 🔴 目的：**只把选路升级为拓扑之后**，"光照档位还有没有选择空间" —— 即
/// **玩家是否还能通过"走捷径 / 绕支路 / 提亮 / 扎营"决定自己停在哪个档**。
/// ⚠️ 口径声明（**不虚报**）：本探针**只走图、不跑战斗** ⇒ 报的是
/// **"到达终点率 / 光照曲线 / 各档占比"**；**完整 V10 完成率（含 battle_goal）在跑战斗的线性探针里**。
/// 三档策略按"探索范围"划分（O-76 原则②：只动选路，不动光照/掉落/扎营数值）。
/// </summary>
[TestClass]
public sealed class M76TopologyProbeTests
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

    private static TuningConfig Tuning() => TuningConfig.Parse(ReadData("tuning.json"));

    private static ExpeditionMapConfig MapCfg() => ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));

    /// <summary>走一趟（策略：是否绕支路 ／ 是否提亮 ／ 是否扎营）。返回该趟读数。</summary>
    private static (bool Reached, int Segments, int Branches, int AvgLight, int MinLight, int Camp, int Brighten,
        Dictionary<string, int> Tiers) Walk(ExpeditionMapConfig mapCfg, TuningConfig tuning, long seed,
        bool exploreBranches, bool brighten, bool camp)
    {
        var log = new CombatLog();
        var rng = new RngProvider(seed);
        var meter = new LightMeter(tuning.Light!);
        meter.EmitStart(log);

        ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, mapCfg);
        var visited = new HashSet<int> { map.StartId };
        int cur = map.StartId;
        int segments = 0, branches = 0, campCount = 0, brightenCount = 0, lightSum = 0, samples = 0, minLight = 100;
        var tiers = new Dictionary<string, int>();

        int guard = 0;
        while (cur != map.GoalId && guard++ < 60)
        {
            // 候选：**优先未探索的相邻房**；若没有（走到死路尽头）⇒ **允许回头**（重走）继续
            var all = map.Edges
                .Where(e => e.From == cur || e.To == cur)
                .Select(e => e.From == cur ? e.To : e.From)
                .Distinct()
                .Select(id => map.Rooms.First(r => r.Id == id))
                .ToList();
            var options = all.Where(r => !visited.Contains(r.Id)).ToList();
            bool backtracking = options.Count == 0;
            if (backtracking)
            {
                options = all; // 死路尽头 ⇒ 回头（按重走计价；🔴 这才是"有去有回"）
            }

            if (options.Count == 0)
            {
                break;
            }

            MapRoom next = exploreBranches && options.Any(o => o.IsBranch) && !backtracking
                ? options.First(o => o.IsBranch)
                : options.OrderBy(o => Math.Abs(o.Depth - map.Rooms.First(r => r.Id == map.GoalId).Depth)).First(); // 默认直奔终点（走捷径）

            if (next.IsBranch)
            {
                branches++;
            }

            // 采样本段出发前的档位与光照（㉑/㉒）
            string tierAt = LightMeter.TierId(meter.Tier);
            tiers[tierAt] = tiers.GetValueOrDefault(tierAt) + 1;
            lightSum += meter.Value;
            samples++;
            minLight = Math.Min(minLight, meter.Value);

            MoveOutcome o = MapTraversal.Step(log, map, mapCfg.Move!, meter, cur, next.Id, visited);
            if (!o.Moved)
            {
                break;
            }

            cur = next.Id;
            if (!o.Revisited)
            {
                segments++;
            }

            // 保守策略：光照低就提亮（消耗柴火；这里只记次数与代价）
            if (brighten && meter.Value <= 50 && brightenCount == 0)
            {
                meter.TryBrighten(log, () => true);
                brightenCount++;
            }

            // 扎营：每趟一次（回满 100）
            if (camp && campCount == 0 && meter.Value <= 40)
            {
                meter.OnCamp(log);
                campCount++;
            }
        }

        return (cur == map.GoalId, segments, branches,
            samples == 0 ? 0 : lightSum / samples, minLight, campCount, brightenCount, tiers);
    }

    [TestMethod]
    public void V6_Topology_LightCurve_And_TierShare_ThreePolicies()
    {
        TuningConfig tuning = Tuning();
        ExpeditionMapConfig mapCfg = MapCfg();

        (string Name, bool Branches, bool Brighten, bool Camp)[] policies =
        {
            ("只走主干·不摸黑（提亮＋扎营）", false, true, true),
            ("只走主干·不提亮", false, false, true),
            ("绕全部支路·不提亮", true, false, true),
        };

        const int runs = 60;
        var lines = new List<string>();
        foreach ((string name, bool br, bool bright, bool camp) in policies)
        {
            int reached = 0, segSum = 0, brSum = 0, avgSum = 0, minSum = 0, campSum = 0, brightSum = 0;
            var tiers = new Dictionary<string, int>();
            for (int i = 0; i < runs; i++)
            {
                var r = Walk(mapCfg, tuning, 20260909 + (i * 13), br, bright, camp);
                reached += r.Reached ? 1 : 0;
                segSum += r.Segments;
                brSum += r.Branches;
                avgSum += r.AvgLight;
                minSum += r.MinLight;
                campSum += r.Camp;
                brightSum += r.Brighten;
                foreach ((string k, int v) in r.Tiers)
                {
                    tiers[k] = tiers.GetValueOrDefault(k) + v;
                }
            }

            int total = tiers.Values.Sum();
            string share = string.Join(" ", new[] { "radiant", "dim", "shadowy", "dark", "black" }
                .Select(t => $"{t}:{(total == 0 ? 0 : 100.0 * tiers.GetValueOrDefault(t) / total):F0}%"));

            lines.Add($"[M7.6] (ii) {name}：到达终点 **{reached / (double)runs:P0}**　段数均值 {segSum / (double)runs:F1}" +
                      $"　支路 {brSum / (double)runs:F2}/趟　㉑ 平均光照 {avgSum / (double)runs:F0}（最暗 {minSum / (double)runs:F0}）" +
                      $"　扎营 {campSum / (double)runs:F2} ／ 提亮 {brightSum / (double)runs:F2}　㉒ {share}");
        }

        string report = string.Join("\n", lines) +
            "\n[M7.6] (ii) 🔴 判读行（策划 #294 的监控项）：**档位还有没有选择空间** —— 见上方三档的 ㉒ 分布差异；" +
            "若三档的 dark/black 占比几乎相同 ⇒ **摸黑是「走到终点的必然」、不是「玩家的选择」**（需上报再议）" +
            "\n[M7.6] (ii) ⚠️ 口径：本探针**只走图、不跑战斗** ⇒ 报的是「到达终点率」；" +
            "**完整 V10 完成率（含 battle_goal=3）仍以跑战斗的探针为准**（不混用）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(lines.Count == 3);
    }

    /// <summary>
    /// 🔴 **片 (ii) 收口：拓扑版 V10**（完整完成率，含 `battle_goal`）——
    /// 走图 + **按房间类型进战斗/事件**；完成口径照 `O-78` 拍板：
    /// **到达主干终点 且 打赢 ≥ battle_goal(3)**。并与**旧线性 V10**读数对照（两者不混用，只并排看）。
    /// </summary>
    [TestMethod]
    public void V6_Topology_V10_FullCompletion_WithBattles()
    {
        TuningConfig tuning = Tuning();
        ExpeditionMapConfig mapCfg = MapCfg();
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));

        (string Name, bool Branches, bool Brighten)[] policies =
        {
            ("主干·提亮（保守）", false, true),
            ("主干·不提亮（均衡）", false, false),
            ("绕支路·不提亮（激进）", true, false),
        };

        const int runs = 40;
        var lines = new List<string>();
        foreach ((string name, bool br, bool bright) in policies)
        {
            int completed = 0, winsSum = 0, battlesSum = 0, lootSum = 0, segSum = 0, retreats = 0;
            var tiers = new Dictionary<string, int>();
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 29));
                var bag = new Inventory(tuning.Inventory!);
                bag.ConfigureRecommended(out _);
                bag.LockForRun();
                var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
                    tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
                    tuning.Expedition.AmbushChance);
                var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                    new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, rng);
                ExpeditionMap map = flow.BeginTopology(mapCfg);
                var visitedTiers = new HashSet<int> { map.StartId };
                bool aborted = false;
                int localWins = 0; // 🔴 本驱动器直接调 session.EndBattle ⇒ **自己数胜场**（flow.Wins 不会被更新）

                int guard = 0;
                while (!flow.ReachedGoal && !aborted && guard++ < 60)
                {
                    var all = map.Edges
                        .Where(e => e.From == flow.CurrentRoomId || e.To == flow.CurrentRoomId)
                        .Select(e => e.From == flow.CurrentRoomId ? e.To : e.From)
                        .Distinct()
                        .Select(id => map.Rooms.First(r => r.Id == id))
                        .ToList();
                    var options = all.Where(r => !visitedTiers.Contains(r.Id)).ToList();
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

                    string tierBefore = LightMeter.TierId(flow.Meter.Tier);
                    tiers[tierBefore] = tiers.GetValueOrDefault(tierBefore) + 1;

                    if (!flow.StepTo(next.Id).Moved)
                    {
                        break;
                    }

                    visitedTiers.Add(next.Id);

                    if (bright && flow.Meter.Value <= 50)
                    {
                        flow.Meter.TryBrighten(log, () => true);
                    }

                    // 🔴 按房间类型进战斗 / 事件
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
                        battlesSum++;
                        if (result != "PlayerVictory")
                        {
                            aborted = true;
                            retreats++;
                            break;
                        }

                        winsSum++;
                        localWins++;
                        TuningLootSpec drop = tuning.Light!.Loot[LightMeter.TierId(flow.Meter.Tier)];
                        lootSum += drop.Firewood + drop.Food;
                    }
                    else
                    {
                        // ⚠️ 最小版映射：房间类型 → 事件节点（取第一个事件节点；"房间↔节点"的正式映射属 UI 片 (iii)）
                        session.ResolveEventNode(log, nodes.Nodes.First(n => n.Type == "event"), 0);
                    }
                }

                segSum += flow.StepsDone;
                // 🔴 完成口径（O-78）：到达主干终点 且 打赢 ≥ battle_goal
                if (!aborted && flow.ReachedGoal && localWins >= tuning.Expedition.BattleGoal)
                {
                    completed++;
                }
            }

            int total = tiers.Values.Sum();
            string share = string.Join(" ", new[] { "radiant", "dim", "shadowy", "dark", "black" }
                .Select(t => $"{t}:{(total == 0 ? 0 : 100.0 * tiers.GetValueOrDefault(t) / total):F0}%"));
            lines.Add($"[M7.6] (ii) 拓扑V10 {name}：**完成率 {completed / (double)runs:P0}**（{completed}/{runs}）" +
                      $"　战斗 {battlesSum / (double)runs:F1}/趟（胜 {winsSum / (double)runs:F1}）　㉔ 补给 {lootSum / (double)runs:F2}/趟" +
                      $"　段数 {segSum / (double)runs:F1}　㉒ {share}　撤退 {retreats}");
        }

        string report = string.Join("\n", lines) +
            "\n[M7.6] (ii) 🔴 **与旧线性 V10 对照**（不混用、只并排）：旧线性 = 保守 100% ／ 均衡 98% ／ 激进 67%（`#278` 那次，`node_step` −30）" +
            "\n[M7.6] (ii) 口径：完成 = **到达主干终点 且 打赢 ≥ 3 场**（`O-78` 拍板；数值不变）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(lines.Count == 3);
    }

    public TestContext TestContext { get; set; } = null!;
}
