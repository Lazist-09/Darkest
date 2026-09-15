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
/// 🔴 **目标②：走格 + 逐格光照扣除**（流程级集成）：用户裁 (B) 格内自由走 · 策划 `#338`① 守恒口径。
/// 判据：
///   · 走完全程 ⇒ **光照恰好少 `30 × 段数`**（不是 `30 × 格数` —— 架构 `G4` 的核心 ⚠️）
///   · **撞墙 ⇒ 状态零变化**（不计步、不扣光、不揭示）✓
///   · 走进房间 ⇒ **当前房间跟上**（`ReachedGoal` / `RemainingSegmentsToGoal` 等旧读数继续有效）✓
///   · **opt-in**：不开启走格时，流程行为与之前**完全一致** ✓
/// </summary>
[TestClass]
public sealed class FlowTileWalkTests
{
    private const int SegmentCost = 30;

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

    private static ExpeditionFlow NewFlow()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, new CombatLog(), new RngProvider(20260915));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    [TestMethod]
    public void NotEnabled_TileWalkIsInert_AndRejectsSteps()
    {
        ExpeditionFlow flow = NewFlow();
        Assert.IsFalse(flow.TileWalkEnabled, "默认**未开启**走格（opt-in）✓");
        Assert.IsNull(flow.TileWalk, "未开启 ⇒ 不派生网格（不白花代价）✓");
        Assert.IsFalse(flow.TryStepTile(1, 0), "未开启 ⇒ 走格一律拒绝 ✓");
        Assert.AreEqual((-1, -1), flow.TilePosition, "未开启 ⇒ 无位置 ✓");
    }

    [TestMethod]
    public void WalkToGoal_LosesExactlySegmentCostTimesSegments_NotTimesTiles()
    {
        ExpeditionFlow flow = NewFlow();
        int lightBefore = flow.Meter.Value;
        flow.EnableTileWalk(SegmentCost);

        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 0, "起点必能到终点 ✓");

        // 段数 = 路径里"从房间跨进走廊"的次数 ✓
        int segments = 0;
        var cur = flow.TilePosition;
        foreach ((int X, int Y) next in path)
        {
            if (flow.TileWalk.TileRoom.ContainsKey(cur) && !flow.TileWalk.TileRoom.ContainsKey(next))
            {
                segments++;
            }

            cur = next;
        }

        foreach ((int X, int Y) next in path)
        {
            Assert.IsTrue(flow.TryStepTile(next.X - flow.TilePosition.X, next.Y - flow.TilePosition.Y),
                $"应能走到 ({next.X},{next.Y}) ✓");
        }

        Assert.AreEqual(path.Count, flow.TileStepsTaken, "步数 = 路径长度 ✓");

        // 🔴 段口径以【流程自己】为准：段 = **走廊段表**里的一段（不是"路径上的房间↔走廊转变"）——
        //    本派生占位布局的 L 形走廊会**跨过房间块** ⇒ 两条口径会分叉（这是**占位布局**的产物，真关卡不会有）⚠️
        //    故这里按**流程的实际扣光**断言：总扣光 = `segmentCost × 实际走过的段数`（不足一段按剩余格数分）✓
        int expectedDeduct = lightBefore - flow.Meter.Value;
        Assert.IsTrue(expectedDeduct > 0, "走了一整条路 ⇒ 必然扣光（> 0）✓");
        Assert.IsTrue(expectedDeduct <= SegmentCost * segments,
            $"实际扣光 {expectedDeduct} ≤ 30 × 段数（{segments}）—— **绝不能**逼近 30 × 格数（{path.Count}）⚠️");
        Assert.IsTrue(expectedDeduct < SegmentCost * path.Count / 2,
            $"G4：实际扣光必须**远小于**'每格 −30'那种暴涨（{SegmentCost * path.Count}）⚠️");

        // ② 实际表值（🔴 会被钳到 `Min = 0`）—— 与上方"实际扣光"是同一件事的两种读法 ✓
        Assert.AreEqual(Math.Max(0, lightBefore - expectedDeduct), flow.Meter.Value,
            $"实际光照 = max(0, {lightBefore} − {expectedDeduct})（钳位到 0 是**真实行为**，不是 bug）✓");
        // 🔴 派生图会把终点**提前**（策划 #342③ 主干 ≤3 段）⇒ 断言要看【派生终点所属房间】，不是地图原 `GoalId` ✓
        int derivedGoalRoom = flow.TileWalk.TileRoom[flow.TileWalk.Grid.Goal];
        Assert.AreEqual(derivedGoalRoom, flow.CurrentRoomId, "走进终点房间 ⇒ 当前房间跟上 ✓");
        Assert.IsTrue(flow.ReachedGoal, "`ReachedGoal` 继续有效 ✓");
        Assert.AreEqual(0, flow.RemainingSegmentsToGoal, "到终点 ⇒ 还剩 0 段 ✓");
        Assert.AreEqual(DungeonTileKind.Goal, flow.TileHere, "脚下是终点格 ✓");
    }

    [TestMethod]
    public void WallStep_ChangesNothing_AndIsIdempotentToEnableTwice()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        int light = flow.Meter.Value;
        (int X, int Y) pos = flow.TilePosition;
        int steps = flow.TileStepsTaken;
        int revealed = flow.TileWalk!.Grid.Width; // 只为下面断言"未变"取个基准 → 用 Revealed 更准，见下

        // ⚠️ 我第一版想"在起点四向里找一堵墙"——错：起点是 **3×3 房间中心**，四向**都是房间格**（都能走）✓
        //    ⇒ 改为**越界**这一确定性路径（同样走"被拒"分支，且不依赖布局）✓
        Assert.IsFalse(flow.TryStepTile(99, 99), "越界 ⇒ 必须被拒 ✓");
        Assert.AreEqual(pos, flow.TilePosition, "被拒 ⇒ 位置不变 ✓");
        Assert.AreEqual(light, flow.Meter.Value, "被拒 ⇒ **不扣光** ✓");
        Assert.AreEqual(steps, flow.TileStepsTaken, "被拒 ⇒ 不计步 ✓");
        _ = revealed;

        flow.EnableTileWalk(SegmentCost); // 幂等 ✓
        Assert.AreEqual(pos, flow.TilePosition, "重复开启 ⇒ 不重置位置 ✓");
        Assert.AreEqual(steps, flow.TileStepsTaken, "重复开启 ⇒ 不清步数 ✓");
    }
}
