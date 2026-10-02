using System;
using System.Collections.Generic;
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
/// ① 从 `RevisitTests.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **D-2 流程级接线**（`Flow_*` 三个用例：重访掷威胁并写 `RngDraw` ／ 写入 `LastRevisitThreat` ／ 未开回头代价则不掷）＋ 走位夹具 `OutAndBack`／`Step`✓
/// ③ 🔴 依赖主类私有成员：`_lastLog`／`NewFlow`／`SegmentCost`／`OutAndBack`／`Step`；外部走 `ExpeditionFlow`／`DungeonWalker`／`CombatLog`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public sealed partial class RevisitTests
{
    // ────────────────────────────── D-2：流程级接线 ──────────────────────────────

    [TestMethod]
    public void Flow_Revisit_RollsThreat_AndWritesRngDraw()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, 15);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 1, "路至少两步 ✓");

        Step(flow, path[0]);
        Step(flow, path[1]);
        int drawsBefore = _lastLog.Events.Count(e => e is RngDraw);

        Step(flow, path[0]); // 回头（退回已站过的 path[0]）⇒ 应触发 D-2 掷骰

        int drawsAfter = _lastLog.Events.Count(e => e is RngDraw);
        Assert.IsTrue(drawsAfter > drawsBefore,
            $"🔴 回头后**必须**留下 `RngDraw`（D-2 判据③）—— 实际 {drawsBefore} → {drawsAfter} ⚠️");
    }

    [TestMethod]
    public void Flow_Revisit_SetsLastRevisitThreat()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, 15);
        Assert.AreEqual(RevisitRollResult.None, flow.LastRevisitThreat, "未走过 ⇒ `None`（不是 null）✓");

        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Step(flow, path[0]);
        Step(flow, path[1]);
        Step(flow, path[0]); // 真回头 ✓

        Assert.AreNotEqual(RevisitRollResult.None, flow.LastRevisitThreat,
            "🔴 回头后 ⇒ 必须留下本次掷骰结果（光照从 0 档起的档位信息）✓");
        Assert.IsTrue(flow.LastRevisitThreat.PercentUsed > 0, "档位概率来自数据（> 0）✓");
    }

    [TestMethod]
    public void Flow_NoBacktrack_NoThreatRoll_EvenIfConfigured()
    {
        // 🔴 语义澄清：D-2 的前提是「重走」；若 D-1 未开（`backtrackCost` 为空）⇒ **同一步不算'重访'分支**吗？
        //    答：`wasRevisit` 由 `DungeonWalker` 给（**与 D-1 是否配置无关**）——
        //    但 `RevisitSpawner` 仍会按 tuning 掷。本用例锁死**这个真实行为**，避免以后被误"优化" ⚠️
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost); // 不开 D-1 代价，但 tuning 里 D-2 已配 ✓
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Step(flow, path[0]);
        Step(flow, path[1]);

        int drawsBefore = _lastLog.Events.Count(e => e is RngDraw);
        Step(flow, path[0]); // 真回头（退回已站过的格子）✓

        Assert.AreEqual(0, flow.TileBacktrackCount, "D-1 未开 ⇒ 回头计数 0 ✓");
        Assert.IsTrue(_lastLog.Events.Count(e => e is RngDraw) > drawsBefore,
            "🔴 但 **D-2 照掷**（`wasRevisit` 是行走器事实，与 D-1 配置无关）—— 两条轴**解耦** ✓");
    }



    /// <summary>
    /// 🔴 走一条「出去 N 步、再原路退回 1 步」的路线 —— **回头必须真的踩回已站过的格**。
    /// ⚠️ 我第一版直接用 `path[0], path[1], path[0]`，误以为那是"回头"——
    ///    实际 `path` 是**从起点出发的单向序列**，`path[0]` 是**新格**，踩它不算重访。
    ///    正确做法：先 `path[0]`，再**退回起点**（起点在构造时就 `_visited.Add(start)`）✓
    /// </summary>
    private static void OutAndBack(ExpeditionFlow flow, IReadOnlyList<(int X, int Y)> path, int outSteps = 1)
    {
        for (int i = 0; i < outSteps; i++)
        {
            Step(flow, path[i]);
        }

        // 退回**上一格** = 原路返回 ⇒ 这一格已经在 `_visited` 里（无论是起点还是刚踩过）✓
        (int X, int Y) back = outSteps >= 2 ? path[outSteps - 2] : flow.TileWalk!.Start;
        Step(flow, back);
    }


    /// <summary>走一格（按差分算方向）✓</summary>
    private static void Step(ExpeditionFlow flow, (int X, int Y) target)
    {
        (int X, int Y) here = flow.TilePosition;
        bool ok = flow.TryStepTile(target.X - here.X, target.Y - here.Y);
        Assert.IsTrue(ok, $"应能从 ({here.X},{here.Y}) 走到 ({target.X},{target.Y}) ✓");
    }
}
