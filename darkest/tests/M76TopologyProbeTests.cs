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

    /// <summary>
    /// 🔴 **`O-79` 候选 (a) 的对照组读数**：**让支路也能含战斗房**（`branch_battle_weight`）
    /// ⇒ 看**"绕支路"档的战斗数与完成率是否上升**（即**完成率是否与风险挂钩**）。
    /// ⚠️ **不改任何已调平数值、不改判据**（`battle_goal` 仍 3；`light.*`/掉落不动）—— 只是新增旋钮。
    /// </summary>
    [TestMethod]
    public void O79_BranchBattle_Comparison_RaisesWinsForBranchExplorer()
    {
        TuningConfig tuning = Tuning();
        ExpeditionMapConfig shipped = MapCfg();                                   // (a) 关（现状：0）
        ExpeditionMapConfig withA = shipped with { Map = shipped.Map with { BranchBattleWeight = 100 } }; // (a) 开

        (string Name, ExpeditionMapConfig Cfg)[] arms =
        {
            ("(a) 关：支路按主干权重（现状）", shipped),
            ("(a) 开：支路必含战斗房", withA),
        };

        var lines = new List<string>();
        foreach ((string name, ExpeditionMapConfig cfg) in arms)
        {
            ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
            const int runs = 30;
            int completed = 0, battlesSum = 0, winsSum = 0, lootSum = 0, retreats = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 31));
                var bag = new Inventory(tuning.Inventory!);
                bag.ConfigureRecommended(out _);
                bag.LockForRun();
                var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
                    tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
                    tuning.Expedition.AmbushChance);
                var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                    new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, rng);
                ExpeditionMap map = flow.BeginTopology(cfg);
                var visited = new HashSet<int> { map.StartId };
                bool aborted = false;
                int localWins = 0;

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

                    // 🔴 激进档：**优先绕支路**（这正是 (a) 要影响的行为）
                    MapRoom next = options.FirstOrDefault(o => o.IsBranch && !back)
                                   ?? options.First(o => o.Id == map.GoalId || !o.IsBranch);

                    if (!flow.StepTo(next.Id).Moved)
                    {
                        break;
                    }

                    visited.Add(next.Id);

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
                        session.ResolveEventNode(log, nodes.Nodes.First(n => n.Type == "event"), 0);
                    }
                }

                if (!aborted && flow.ReachedGoal && localWins >= tuning.Expedition.BattleGoal)
                {
                    completed++;
                }
            }

            lines.Add($"[M7.6] O-79 {name}（绕支路档，{runs} 趟）：完成率 **{completed / (double)runs:P0}**" +
                      $"　战斗 {battlesSum / (double)runs:F1}/趟　胜 {winsSum / (double)runs:F1}　㉔ 补给 {lootSum / (double)runs:F2}/趟" +
                      $"　撤退 {retreats}");
        }

        string report = string.Join("\n", lines) +
            "\n[M7.6] O-79 判读：若 **(a) 开** 的战斗数与完成率【明显高于 (a) 关】⇒ **完成率与风险挂钩成立**（架构倾向 (a) 有了数据支持）" +
            "\n[M7.6] O-79 纪律：**`battle_goal` 仍为 3**（未调）；`light.*`／掉落／判据**均未动** —— 本对照只是新增旋钮";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(lines.Count == 2);
    }

    /// <summary>
    /// 🔴 **`#298` (c) 的对照读数**：支路新增**降低撤退风险**的特殊房（本片接线 `free_light` = **+光照且不耗柴火**）。
    /// ⚠️ **新旋钮默认 0 = 现状不变**；**不改判据、不调 `battle_goal`、不碰其他数值**。
    /// 只报**"绕支路"档**（那里才看得出差别）—— 看**完成率/撤退**是否改善。
    /// </summary>
    [TestMethod]
    public void O298_C_FreeLightBranch_Comparison()
    {
        TuningConfig tuning = Tuning();
        ExpeditionMapConfig shipped = MapCfg();                                        // (c) 关（现状：0）
        ExpeditionMapConfig withC = shipped with
        {
            Map = shipped.Map with { BranchSpecialWeight = 100, BranchSpecialKind = "free_light" },
        };

        (string Name, ExpeditionMapConfig Cfg)[] arms =
        {
            ("(c) 关：支路按主干权重（现状）", shipped),
            ("(c) 开：支路＝免费光合房（+光照、不耗柴火）", withC),
        };

        var lines = new List<string>();
        foreach ((string name, ExpeditionMapConfig cfg) in arms)
        {
            ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
            const int runs = 30;
            int completed = 0, battlesSum = 0, winsSum = 0, retreats = 0, freeLightSum = 0, minLightSum = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 37));
                var bag = new Inventory(tuning.Inventory!);
                bag.ConfigureRecommended(out _);
                bag.LockForRun();
                var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
                    tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
                    tuning.Expedition.AmbushChance);
                var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                    new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, rng);
                ExpeditionMap map = flow.BeginTopology(cfg);
                var visited = new HashSet<int> { map.StartId };
                bool aborted = false;
                int localWins = 0, freeLight = 0, minLight = 100;

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

                    MapRoom next = options.FirstOrDefault(o => o.IsBranch && !back)
                                   ?? options.First(o => o.Id == map.GoalId || !o.IsBranch);

                    if (!flow.StepTo(next.Id).Moved)
                    {
                        break;
                    }

                    visited.Add(next.Id);
                    if (flow.Meter.Value < minLight)
                    {
                        minLight = flow.Meter.Value;
                    }

                    // 🔴 (c) 的效果：**免费光合房**（+20 光照，**不耗柴火**）
                    if (next.Type == "free_light")
                    {
                        flow.Meter.TryAdvanceBy(log, +20, "free_light");
                        freeLight++;
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
                        battlesSum++;
                        if (result != "PlayerVictory")
                        {
                            aborted = true;
                            retreats++;
                            break;
                        }

                        winsSum++;
                        localWins++;
                    }
                    else if (next.Type != "free_light")
                    {
                        session.ResolveEventNode(log, nodes.Nodes.First(n => n.Type == "event"), 0);
                    }
                }

                freeLightSum += freeLight;
                minLightSum += minLight;
                if (!aborted && flow.ReachedGoal && localWins >= tuning.Expedition.BattleGoal)
                {
                    completed++;
                }
            }

            lines.Add($"[M7.6] #298(c) {name}（绕支路档，{runs} 趟）：完成率 **{completed / (double)runs:P0}**" +
                      $"　战斗 {battlesSum / (double)runs:F1}/趟　胜 {winsSum / (double)runs:F1}　撤退 {retreats}" +
                      $"　免费光合房 {freeLightSum / (double)runs:F2}/趟　最暗 {minLightSum / (double)runs:F0}");
        }

        string report = string.Join("\n", lines) +
            "\n[M7.6] #298(c) 判读：若 **(c) 开** 的完成率【明显高于 (c) 关】⇒ **\"补当前稀缺的东西（光照/安全）\"这条路成立**；" +
            "若仍打平 ⇒ 按裁定回报策划，那时才轮到检讨判据 (d)" +
            "\n[M7.6] #298(c) 纪律：`battle_goal` 仍 3；**未动其他数值**；**新旋钮默认 0 = 现状**；只接线了 `free_light`（免费恢复房 / 士气房**待其效果接线后**再加入白名单 —— 红线 21）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(lines.Count == 2);
    }

    /// <summary>
    /// 🔴 **`#298` ⑤ 的下一步**：**(c) 开 / 关 × 三档**（保守=主干提亮 ／ 均衡=主干不提亮 ／ 激进=绕支路不提亮）
    /// ⇒ 看**倒 U 是否出现**（均衡档是否最高）。若两臂都仍打平 ⇒ **按裁定回报，检讨判据 (d)**。
    /// ⚠️ 纪律：`battle_goal` 仍 3、判据未改、只切 (c) 旋钮。
    /// </summary>
    [TestMethod]
    public void V6_Topology_V10_WithC_ThreePolicies()
    {
        ExpeditionNodesConfig nodesG = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        Func<string, bool, bool, ExpeditionMapConfig, string> runOne = (name, br, bright, cfg) =>
        {
            TuningConfig tuning = Tuning();
            const int runs = 25;
            int completed = 0, battlesSum = 0, retreats = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 41));
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
                bool aborted = false;
                int localWins = 0;

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
                    if (bright && flow.Meter.Value <= 50)
                    {
                        flow.Meter.TryBrighten(log, () => true);
                    }

                    if (next.Type == "free_light")
                    {
                        flow.Meter.TryAdvanceBy(log, +20, "free_light");
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
                        battlesSum++;
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

                if (!aborted && flow.ReachedGoal && localWins >= tuning.Expedition.BattleGoal)
                {
                    completed++;
                }
            }

            return $"{name}：完成率 **{completed / (double)runs:P0}**（战斗 {battlesSum / (double)runs:F1}／撤退 {retreats}）";
        };

        ExpeditionMapConfig shipped = MapCfg();
        ExpeditionMapConfig withC = shipped with
        {
            Map = shipped.Map with { BranchSpecialWeight = 100, BranchSpecialKind = "free_light" },
        };

        var lines = new List<string> { "[M7.6] #298⑤ (c) 关 × 三档：" + runOne("保守·主干提亮", false, true, shipped) +
                                       "　" + runOne("均衡·主干不提亮", false, false, shipped) +
                                       "　" + runOne("激进·绕支路", true, false, shipped),
            "[M7.6] #298⑤ (c) 开 × 三档：" + runOne("保守·主干提亮", false, true, withC) +
            "　" + runOne("均衡·主干不提亮", false, false, withC) +
            "　" + runOne("激进·绕支路", true, false, withC) };
        lines.Add("[M7.6] #298⑤ 判读：**倒 U = 均衡档最高**；若 (c) 开后仍**三档打平**（或保守最高）⇒ 回报策划，检讨判据 (d)");

        Console.WriteLine(string.Join("\n", lines));
        TestContext.WriteLine(string.Join("\n", lines));
        Assert.AreEqual(3, lines.Count);
    }

    /// <summary>
    /// 🔴 **`#299` ③ 三臂对照**：**只切 `light_gain`**（X = 30 / 20 / 10；净 = −30 + X ⇒ 0 / −10 / −20）× 三档
    /// ⇒ 判读：**哪一个让三档出现【倒 U】**（均衡最高、两端次优）；若三者都"激进最高" ⇒ 回报，那时才谈 (d)。
    /// 🔴 并校验 **V3 形态之二**：`branch_special_weight` 开启后，**"绕支路"档完成率不得高于保守/均衡**。
    /// ⚠️ **X = 30 违反 P25 ⑨（净代价必须 > 0）** ⇒ 仅作对照臂保留（探针直接构造配置、不走加载校验）。
    /// </summary>
    [TestMethod]
    public void O299_LightGain_Sweep_ThreeArms()
    {
        TuningConfig tuning = Tuning();
        ExpeditionNodesConfig nodesG = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionMapConfig shipped = MapCfg();

        Func<bool, bool, int, ExpeditionMapConfig, string> run = (br, bright, gain, cfg) =>
        {
            const int runs = 25;
            int completed = 0, retreats = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 43));
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
                bool aborted = false;
                int localWins = 0;

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
                    if (bright && flow.Meter.Value <= 50)
                    {
                        flow.Meter.TryBrighten(log, () => true);
                    }

                    if (next.Type == "free_light" && gain > 0)
                    {
                        flow.Meter.TryAdvanceBy(log, +gain, "free_light");
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

                if (!aborted && flow.ReachedGoal && localWins >= tuning.Expedition.BattleGoal)
                {
                    completed++;
                }
            }

            return $"{completed / (double)runs:P0}（撤退 {retreats}）";
        };

        var lines = new List<string>();
        foreach (int gain in new[] { 30, 20, 10 })
        {
            ExpeditionMapConfig cfg = shipped with
            {
                Map = shipped.Map with { BranchSpecialWeight = 100, BranchSpecialLightGain = gain },
            };
            string keep = run(false, true, gain, cfg);
            string bal = run(false, false, gain, cfg);
            string aggr = run(true, false, gain, cfg);
            lines.Add($"[M7.6] #299 X={gain}（净 {gain - 30}）：保守 {keep}　均衡 {bal}　激进 {aggr}" +
                      (gain == 30 ? "　🔴 **违反 P25 ⑨（净代价必须 > 0）**，仅作对照臂" : string.Empty));
        }

        lines.Add("[M7.6] #299 判读：**倒 U = 均衡最高**；若三臂都" +
                  "激进最高 ⇒ 回策划谈 (d)；🔴 **V3 形态之二**：激进完成率【不得高于】保守/均衡（否则=纯赚）");
        Console.WriteLine(string.Join("\n", lines));
        TestContext.WriteLine(string.Join("\n", lines));
        Assert.AreEqual(4, lines.Count);
    }

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
