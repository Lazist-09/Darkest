// 🔴 从 M76TopologyProbeTests.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）：
//    支路类探针（O79 支路战斗 / O298 免费光照支路） —— 只搬家、零行为改动（[TestClass] 只在主文件上，MSTest 仍会发现本 partial）
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
                        // 🔴 `#305`⑤：注入当前光照档（否则读数不含光照）
                        BattleDirector d = session.BeginExpeditionBattle(idx, log, tuning.Expedition.DifficultyTiers, flow.Meter.Effect);
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
}
//    【依赖主类私有成员】(partial 使封装在文件级失效 => 必须声明)：MapCfg x3 · ReadData x3 · Tuning x3
