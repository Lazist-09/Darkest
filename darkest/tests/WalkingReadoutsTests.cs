using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🆕 **行走模式的表现层读数**（DD 式侧视走廊要用的两个）：
/// ① `flow.RevealedRoomIds`（已揭示集合）② `flow.NextRoomToward(id)` / `MapTraversal.FirstStepToward`（下一跳）
///
/// 设计口径（写死在这里，避免以后各写一份）：
///   · 内核只回答"**图上的下一跳是哪一间**"，**不回答"左/右"** —— 方向是表现层的排版决定 ✓
///   · 判据用**最短路递减**：`dist(start → 下一跳) == dist(start → 目标) - 1` ✓（比"看起来对"硬）
/// </summary>
[TestClass]
public sealed class WalkingReadoutsTests
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

    private static ExpeditionMap NewMap(long seed)
    {
        ExpeditionMapConfig cfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        return ExpeditionMapGenerator.Generate(new CombatLog(), new RngProvider(seed), cfg);
    }

    [TestMethod]
    public void FirstStepToward_ReducesTheDistanceByExactlyOne()
    {
        ExpeditionMap map = NewMap(20260915);
        int start = map.StartId;
        int goal = map.GoalId;
        int total = MapTraversal.ShortestPathLength(map, start, goal);
        Assert.IsTrue(total > 0, "前置：起点≠终点且连通 ✓");

        int next = MapTraversal.FirstStepToward(map, start, goal);
        Assert.IsTrue(next >= 0, "应给出下一跳 ✓");
        Assert.IsTrue(MapTraversal.IsAdjacent(map, start, next), "下一跳必须**与当前位置相邻**（不能跳格）✓");
        Assert.AreEqual(total - 1, MapTraversal.ShortestPathLength(map, next, goal),
            "🔴 判据：走到该下一跳后，到终点的最短路必须**恰好少 1**（否则不是朝目标走）✓");
    }

    [TestMethod]
    public void FirstStepToward_ReturnsMinusOne_WhenAlreadyThereOrUnreachable()
    {
        ExpeditionMap map = NewMap(4242);
        Assert.AreEqual(-1, MapTraversal.FirstStepToward(map, map.StartId, map.StartId), "已在目标 ⇒ -1 ✓");
        Assert.AreEqual(-1, MapTraversal.FirstStepToward(map, map.StartId, 99999), "不存在的房间 ⇒ -1（不抛、不猜）✓");
    }

    [TestMethod]
    public void Flow_RevealedRoomsAndNextRoom_AreConsistent()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, new CombatLog(), new RngProvider(7));
        flow.BeginTopology(mapCfg);

        Assert.AreEqual(flow.CurrentRoomId, flow.RevealedRoomIds.Single(), "开局只揭示起点一间 ✓");
        int next = flow.NextRoomToward(flow.Map!.GoalId);
        Assert.IsTrue(next >= 0 && MapTraversal.IsAdjacent(flow.Map, flow.CurrentRoomId, next),
            "读数的下一跳必须与当前间相邻（表现层据此推进走廊）✓");
        Assert.IsFalse(flow.RevealedRoomIds.Contains(next), "未走过的那间**还没揭示**（表现层据此画雾）✓");
    }

    /// <summary>
    /// 🔴 策划 `#335`① 的**语义写死**用例：
    /// · 入口 ⇒ = 全程段数（**不是 0**）· 走到终点 ⇒ **0** · 同时 `HasReachedGoal` 为真（"距离 0"与"已到达"在呈现上是两件事）
    /// · 线性模式（历史路径）⇒ 等价口径 = `NBattles − StepsDone` ✓
    /// </summary>
    [TestMethod]
    public void RemainingSegmentsToGoal_SemanticsArePinned()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        ExpeditionFlow flow = NewFlow(tuning);

        // 线性模式（未进拓扑）：等价口径 = 剩余场数 ✓
        Assert.AreEqual(tuning.Expedition.NBattles, flow.RemainingSegmentsToGoal, "未开始 ⇒ 全程（线性口径）✓");

        flow.BeginTopology(mapCfg);
        int full = flow.RemainingSegmentsToGoal;
        Assert.AreEqual(MapTraversal.ShortestPathLength(flow.Map!, flow.Map!.StartId, flow.Map!.GoalId), full,
            "入口 ⇒ = 全程段数（**不是 0**）✓");
        Assert.IsFalse(flow.HasReachedGoal, "刚入口 ⇒ 未到达 ✓");

        // 顺着"下一跳"一路走到终点 ⇒ 每次都必须减少（单调递减 = 朝目标走）✓
        int guard = 0;
        int last = full;
        while (!flow.HasReachedGoal && guard++ < 200)
        {
            int step = flow.NextRoomToward(flow.Map!.GoalId);
            Assert.IsTrue(step >= 0, "未到终点就应给出下一跳 ✓");
            MoveOutcome mv = flow.StepTo(step);
            Assert.IsTrue(mv.Moved, $"走一步应成功（{mv.Reason}）✓");
            Assert.IsTrue(flow.RemainingSegmentsToGoal < last, "每走一步 ⇒ 到终点**必须更近**（否则不是朝目标走）✓");
            last = flow.RemainingSegmentsToGoal;
        }

        Assert.IsTrue(flow.HasReachedGoal, "应能走到终点 ✓");
        Assert.AreEqual(0, flow.RemainingSegmentsToGoal, "🔴 已到终点 ⇒ **0**（不是 -1 / 不是 null）✓");
    }

    private static ExpeditionFlow NewFlow(TuningConfig tuning)
    {
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        return new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, new CombatLog(), new RngProvider(7));
    }
}
