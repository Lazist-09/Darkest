using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
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

    public TestContext TestContext { get; set; } = null!;
}
