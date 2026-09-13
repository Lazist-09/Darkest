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
/// **M7.6 片 ②：按段移动**（`m8_roadmap §4.3②` + **P25 ④**）——
/// 新区域 −30 ／ 重走已探索 −10（**回头更便宜**）· 只走相邻走廊 · 代价走 `LightChangedEvent` 可审计。
/// 🔴 并在报告里给出**与旧线性口径的对照**（O-76：这类改动必须能一眼看出"动没动到已调平的量级"）。
/// </summary>
[TestClass]
public sealed class MapTraversalTests
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

    private static ExpeditionMapConfig Cfg() => ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));

    private static TuningConfig Tuning() => TuningConfig.Parse(ReadData("tuning.json"));

    [TestMethod]
    public void P25_4_RevisitMustBeCheaper_AndBadDataThrows()
    {
        ExpeditionMapConfig c = Cfg();
        Assert.IsTrue(c.Move!.NewRoomCost < 0 && c.Move.RevisitCost < 0, "两者都为负（P25 ④）");
        Assert.IsTrue(Math.Abs(c.Move.RevisitCost) < Math.Abs(c.Move.NewRoomCost), "**回头更便宜**（P25 ④）");

        string raw = ReadData("expedition_map.json");
        Assert.ThrowsException<InvalidDataException>(
            () => ExpeditionMapConfig.Parse(raw.Replace("\"revisit_cost\": -10", "\"revisit_cost\": -40", StringComparison.Ordinal)),
            "回头比新区域还贵 ⇒ 报错（P25 ④）");
    }

    [TestMethod]
    public void Step_NewRoomCostsThirty_RevisitCostsTen_AndIsAudited()
    {
        ExpeditionMapConfig cfg = Cfg();
        var log = new CombatLog();
        var rng = new RngProvider(20260909);
        ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, cfg);
        var meter = new LightMeter(Tuning().Light!);
        meter.EmitStart(log);

        int start = map.StartId, first = 1, back = 0;

        Assert.IsTrue(MapTraversal.IsAdjacent(map, start, first), "0 与 1 相邻（主干）");
        Assert.IsTrue(MapTraversal.IsAdjacent(map, first, back), "1 与 0 相邻");

        var visited = new HashSet<int> { start };
        MoveOutcome toNew = MapTraversal.Step(log, map, cfg.Move!, meter, start, first, visited);
        Assert.IsTrue(toNew.Moved && !toNew.Revisited, "首次进入 = 新区域");
        Assert.AreEqual(cfg.Move!.NewRoomCost, toNew.Cost, "新区域 −30");
        Assert.AreEqual(100 + cfg.Move.NewRoomCost, meter.Value, "光照 100 → 70");

        MoveOutcome back2 = MapTraversal.Step(log, map, cfg.Move, meter, first, back, visited);
        Assert.IsTrue(back2.Moved && back2.Revisited, "回到已探索 = 重走");
        Assert.AreEqual(cfg.Move.RevisitCost, back2.Cost, "重走 −10（回头更便宜）");
        Assert.AreEqual(70 + cfg.Move.RevisitCost, meter.Value, "光照 70 → 60");

        Assert.IsTrue(log.Events.OfType<LightChangedEvent>().Any(e => e.Reason == "advance"), "新区域写 advance 事件");
        Assert.IsTrue(log.Events.OfType<LightChangedEvent>().Any(e => e.Reason == "revisit"), "重走写 revisit 事件（可审计）");
    }

    [TestMethod]
    public void Step_NonAdjacent_IsRefused_AndCostsNoLight()
    {
        ExpeditionMapConfig cfg = Cfg();
        var log = new CombatLog();
        var rng = new RngProvider(999);
        ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, cfg);
        var meter = new LightMeter(Tuning().Light!);
        meter.EmitStart(log);

        int valueBefore = meter.Value;
        var visited = new HashSet<int> { map.StartId };
        MoveOutcome bad = MapTraversal.Step(log, map, cfg.Move!, meter, map.StartId, map.GoalId, visited);

        Assert.IsFalse(bad.Moved, "不相邻 ⇒ 拒绝（不能瞬移）");
        Assert.AreEqual(valueBefore, meter.Value, "拒绝时**不耗光**");
        Assert.AreEqual("not_adjacent", bad.Reason);
    }

    [TestMethod]
    public void RouteLength_AndTotalLightCost_ComparedWithOldLinear()
    {
        ExpeditionMapConfig cfg = Cfg();
        TuningConfig tuning = Tuning();
        var log = new CombatLog();
        var rng = new RngProvider(31337);

        int segMin = int.MaxValue, segMax = 0, costMin = int.MaxValue, costMax = int.MinValue, disconnected = 0;
        const int n = 200;
        for (int i = 0; i < n; i++)
        {
            ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, cfg);
            int segs = MapTraversal.ShortestPathLength(map, map.StartId, map.GoalId);
            if (segs < 0)
            {
                disconnected++;
                continue;
            }

            segMin = Math.Min(segMin, segs);
            segMax = Math.Max(segMax, segs);
            costMin = Math.Min(costMin, segs * cfg.Move!.NewRoomCost);
            costMax = Math.Max(costMax, segs * cfg.Move.NewRoomCost);
        }

        Assert.AreEqual(0, disconnected, "🔴 200 张图必须都能从起点走到终点");

        int oldLinearSegs = tuning.Expedition.NBattles;             // 旧：6 步
        int oldLinearCost = oldLinearSegs * tuning.Light!.NodeStep; // 旧：6 × −30 = −180
        string report = $"[M7.6] 片② 按段移动（{n} 张图）：起点→终点 **{segMin}~{segMax} 段**（新区域 −30 ／ 重走 −10）；" +
                        $"只走新区域的光照总消耗 {costMin}~{costMax}　｜　对照旧线性：{oldLinearSegs} 步 × −30 = {oldLinearCost}" +
                        $"　⇒ 段数区间与旧 6 步**同量级**（这正是「没动到量级、但分布会变」的证据 ⇒ 必须重跑 V10/A1/A2）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(segMin >= 4 && segMax <= 9, $"段数应在 4~9（实测 {segMin}~{segMax}）");
    }

    public TestContext TestContext { get; set; } = null!;
}
