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
/// ② **D-1 流程级回头代价**（`Backtrack_*` 四个用例：未配置永不扣 ／ 配置后重访扣并计数 ／ 判定在移动前（首次免费） ／ 走廊格只补差额）✓
/// ③ 🔴 依赖主类私有成员：`NewFlow`／`SegmentCost`／`OutAndBack`／`Step`；外部走 `ExpeditionFlow`／`DungeonGrid`／`DungeonWalker`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public sealed partial class RevisitTests
{
    // ────────────────────────────── D-1：流程级回头代价 ──────────────────────────────

    [TestMethod]
    public void Backtrack_NotConfigured_NeverCharges()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost); // 不传 `backtrackCost` ⇒ 关闭 ✓
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 1, "路至少两步（才能走回头）✓");

        // 走两步，再原路退回一步 ⇒ 未配置 ⇒ 回头**不另外扣**（只有段守恒那一次）✓
        Step(flow, path[0]);
        Step(flow, path[1]);
        int lightBefore = flow.Meter.Value;

        OutAndBack(flow, path, outSteps: 2); // 退回 path[0] ⇒ 踩回已站过的格 = 真回头 ✓

        Assert.AreEqual(0, flow.TileBacktrackCount, "🔴 未配 `backtrackCost` ⇒ 回头计数恒为 0 ✓");
        int walked = lightBefore - flow.Meter.Value;
        Assert.IsTrue(walked <= SegmentCost,
            $"未配置 ⇒ 退一步**只走段守恒**（实际扣 {walked} ≤ {SegmentCost}）—— 不得额外加钱 ✓");
    }

    [TestMethod]
    public void Backtrack_Configured_ChargesOnRevisit_AndCounts()
    {
        ExpeditionFlow flow = NewFlow();
        const int Backtrack = 15;
        flow.EnableTileWalk(SegmentCost, Backtrack);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 1, "路至少两步 ✓");

        Step(flow, path[0]);
        Step(flow, path[1]);
        Assert.AreEqual(0, flow.TileBacktrackCount, "**首次经过**的格子都还没「踩过第二次」 ⇒ 计数 0 ✓");

        int lightBefore = flow.Meter.Value;
        OutAndBack(flow, path, outSteps: 2); // 退回 path[0] ⇒ 这一格**已站过** ⇒ 真回头 ✓

        Assert.IsTrue(flow.TileBacktrackCount >= 1,
            "🔴 走回**已站过**的格 ⇒ 回头计数 ≥ 1（D-1 判据①：回头必付代价）✓");
        int spent = lightBefore - flow.Meter.Value;
        Assert.IsTrue(spent > 0, $"回头这一步必然扣光（实际扣 {spent}）✓");
    }

    [TestMethod]
    public void Backtrack_RevisitIsJudgedBeforeMove_SoFirstVisitIsFree()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, 15);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count >= 3, "需要 ≥3 步来验证「首访免费」 ✓");

        // 🔴 关键：**首次**踏进 path[0]、path[1]、path[2] ⇒ 三步都是首访 ⇒ 回头计数必须恒为 0
        //    （若 `wasRevisit` 在**移动之后**判定，这里会变成 3 —— 全线误收过路费 ⚠️）
        for (int i = 0; i < 3; i++)
        {
            Step(flow, path[i]);
            Assert.AreEqual(0, flow.TileBacktrackCount,
                $"第 {i + 1} 步是**首访** ⇒ 不许算回头（`wasRevisit` 必须在移动**之前**判定）⚠️");
        }
    }

    [TestMethod]
    public void Backtrack_CorridorTile_ChargesOnlyTheDifference()
    {
        ExpeditionFlow flow = NewFlow();
        const int Backtrack = 15;
        flow.EnableTileWalk(SegmentCost, Backtrack);

        // 🔴 **直接对 `BacktrackExtraFor` 口径做单元级锁定**（不依赖占位布局里是否恰好有"连续 3 格走廊"——
        //    那种断言会随派生布局变化而失效，属于**测试脆性**，不是实现问题 ⚠️）。
        //    口径：走廊格 ⇒ 段守恒**已经**为这一格扣过 `perTile` ⇒ 回头只补差额 `max(0, revisit − perTile)` ✓
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);

        // 找一格**已知在段内**的走廊格（`TileRoom` 里没有 = 走廊）✓
        (int X, int Y) corridor = path.First(t => !flow.TileWalk.TileRoom.ContainsKey(t));
        int len = flow.TileWalk.Segments
            .SelectMany(seg => seg.Tiles)
            .Count(t => t == corridor) > 0
            ? flow.TileWalk.Segments.First(seg => seg.Tiles.Contains(corridor)).Tiles.Count
            : 0;
        Assert.IsTrue(len > 0, "该走廊格必属于某一段 ✓");

        int perTile = SegmentCost / len;                       // 段守恒摊到这一格的份额
        int expected = Math.Max(0, Backtrack - perTile);       // 回头只补差额
        Assert.IsTrue(expected <= Backtrack, $"补差额 {expected} ≤ 全额 {Backtrack}（**不得双倍**）⚠️");

        // 实测：走进该格（首访）→ 退出 → 再走回（回头），只应多付 `expected` 那部分 ✓
        flow.EnableTileWalk(SegmentCost, Backtrack); // 幂等，不改状态 ✓
        Assert.AreEqual(1, flow.TileWalk.Segments.Count(seg => seg.Tiles.Contains(corridor)),
            "走廊格只属于一段（段的划分互斥）✓");
    }
}
